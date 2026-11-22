using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace Fecuencimetro_Visualizador
{
    /// <summary>
    /// Formulario principal de la aplicación de escritorio.
    /// Gestiona la comunicación serie (USB-CDC) con la Raspberry Pi Pico, el control remoto
    /// del generador PWM, el procesamiento y cálculo de errores de las muestras recibidas,
    /// su visualización en tiempo real y la exportación del historial a CSV.
    /// </summary>
    public partial class Form1 : Form
    {
        // ====================================================================
        // CAMPOS PRIVADOS Y ESTADO DE LA APLICACIÓN
        // ====================================================================

        private bool contadorPausado = false;                     // Estado local del botón de pausa/reanudación de eventos
        private SerialPort puertoSerie;                           // Instancia del puerto serie USB-CDC hacia la Raspberry Pi Pico
        private List<Muestra> listaMuestras = new List<Muestra>(); // Historial completo en memoria de las mediciones capturadas

        // ====================================================================
        // CONSTRUCTOR E INICIALIZACIÓN DE INTERFAZ
        // ====================================================================

        /// <summary>
        /// Inicializa los componentes visuales, configura los controles desplegables,
        /// estructura la tabla de datos y establece los valores por defecto del generador.
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            CargarPuertosDisponibles();
            ConfigurarComboEscala();
            ConfigurarTabla();

            // Restringe el selector de puertos COM a elementos válidos de la lista
            ComboBox_Com.DropDownStyle = ComboBoxStyle.DropDownList;

            // Actualiza dinámicamente los puertos COM cada vez que se despliega el menú
            ComboBox_Com.DropDown += (s, e) => CargarPuertosDisponibles();

            // Parámetros iniciales por defecto (1 kHz, 50 % Duty Cycle)
            textBox_FrecGen.Text = "1000";
            textBox_DutyCycleGen.Text = "50";
            textBox_targetfrec.Text = "1000";
            textBox_targetdutycicle.Text = "50";
        }

        // ====================================================================
        // MÉTODOS DE CONFIGURACIÓN DE CONTROLES
        // ====================================================================

        /// <summary>
        /// Define las columnas y propiedades visuales del DataGridView principal
        /// para actuar como un registro limpio de solo lectura.
        /// </summary>
        private void ConfigurarTabla()
        {
            dataGridView_visualizador.Columns.Clear();

            // Definición de columnas: (NombreInterno, TextoCabecera)
            dataGridView_visualizador.Columns.Add("colHora", "Hora");
            dataGridView_visualizador.Columns.Add("colFrecuencia", "Frec.");
            dataGridView_visualizador.Columns.Add("colErrorFrec", "Err. Frec.");
            dataGridView_visualizador.Columns.Add("colPeriodo", "Periodo");
            dataGridView_visualizador.Columns.Add("colErrorPer", "Err. Periodo");
            dataGridView_visualizador.Columns.Add("colDuty", "Duty Cycle");
            dataGridView_visualizador.Columns.Add("colErrorDuty", "Err. Duty");
            dataGridView_visualizador.Columns.Add("colPulsos", "Pulsos");
            dataGridView_visualizador.Columns.Add("colTAlto", "T. Alto (µs)");
            dataGridView_visualizador.Columns.Add("colTBajo", "T. Bajo (µs)");

            // Ajustes de comportamiento y presentación
            dataGridView_visualizador.ReadOnly = true;
            dataGridView_visualizador.AllowUserToAddRows = false;
            dataGridView_visualizador.RowHeadersVisible = false;
            dataGridView_visualizador.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        /// <summary>
        /// Escanea los puertos COM activos en el sistema operativo y conserva
        /// la selección previa si el dispositivo sigue conectado.
        /// </summary>
        private void CargarPuertosDisponibles()
        {
            string seleccionPrevia = ComboBox_Com.SelectedItem?.ToString();
            ComboBox_Com.Items.Clear();

            string[] puertos = SerialPort.GetPortNames();
            ComboBox_Com.Items.AddRange(puertos);

            if (seleccionPrevia != null && ComboBox_Com.Items.Contains(seleccionPrevia))
            {
                ComboBox_Com.SelectedItem = seleccionPrevia;
            }
            else if (ComboBox_Com.Items.Count > 0)
            {
                ComboBox_Com.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Inicializa el selector de unidades de frecuencia con autoescalado por defecto.
        /// </summary>
        private void ConfigurarComboEscala()
        {
            comboBox_Escala.Items.Clear();
            comboBox_Escala.Items.AddRange(new string[] { "Auto", "Hz", "kHz", "MHz" });
            comboBox_Escala.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox_Escala.SelectedIndex = 0; // "Auto" por defecto
        }

        // ====================================================================
        // FORMATEO DE MAGNITUDES Y CÁLCULO DE ERRORES RELATIVOS
        // ====================================================================

        /// <summary>
        /// Formatea un valor de frecuencia en Hz aplicando la escala seleccionada
        /// por el usuario ("Auto", "Hz", "kHz" o "MHz").
        /// </summary>
        private string FormatearFrecuenciaConEscala(double hz)
        {
            if (hz <= 0) return "0.00 Hz";

            string escala = comboBox_Escala.SelectedItem?.ToString() ?? "Auto";

            switch (escala)
            {
                case "Hz":
                    return $"{hz:F2} Hz";

                case "kHz":
                    return $"{hz / 1_000.0:F4} kHz";

                case "MHz":
                    return $"{hz / 1_000_000.0:F6} MHz";

                case "Auto":
                default:
                    if (hz >= 1_000_000) return $"{hz / 1_000_000.0:F4} MHz";
                    if (hz >= 1_000) return $"{hz / 1_000.0:F3} kHz";
                    return $"{hz:F2} Hz";
            }
        }

        /// <summary>
        /// Convierte un periodo expresado en segundos a su subunidad más legible (ns, µs, ms o s).
        /// </summary>
        private string FormatearPeriodo(double seg)
        {
            if (seg <= 0) return "—";
            if (seg < 1e-6) return $"{seg * 1e9:F2} ns";
            if (seg < 1e-3) return $"{seg * 1e6:F2} µs";
            if (seg < 1.0) return $"{seg * 1e3:F3} ms";
            return $"{seg:F4} s";
        }

        /// <summary>
        /// Calcula el error porcentual relativo entre la frecuencia objetivo y la medida.
        /// </summary>
        private string CalcErrorFrecuencia(string target, double medida)
        {
            double targetVal = double.TryParse(target, out double t) ? t : 0;

            if (targetVal <= 0) return "—";
            double error = (Math.Abs(targetVal - medida) / targetVal) * 100.0;
            return $"{error:F2} %";
        }

        /// <summary>
        /// Calcula el error porcentual relativo del periodo a partir de las frecuencias objetivo y medida.
        /// </summary>
        private string CalcErrorPeriodo(string target, double frec)
        {
            double medida = 1 / frec;
            double targetfrec = double.TryParse(target, out double t) ? t : 0;

            double targetVal = 1 / (targetfrec > 0 ? targetfrec : 1);
            if (targetVal <= 0) return "—";
            double error = (Math.Abs(targetVal - medida) / targetVal) * 100.0;
            return $"{error:F2} %";
        }

        /// <summary>
        /// Calcula el error porcentual relativo del ciclo de trabajo respecto al valor de referencia.
        /// </summary>
        private string CalcErrorDutyCycle(string target, double medida)
        {
            double targetVal = double.TryParse(target, out double t) ? t : 0;
            if (targetVal <= 0) return "—";
            double error = (Math.Abs(targetVal - medida) / targetVal) * 100.0;
            return $"{error:F2} %";
        }

        /// <summary>
        /// Almacena una muestra en el historial, inserta una nueva fila en la parte superior
        /// de la tabla y actualiza los indicadores numéricos instantáneos de la interfaz.
        /// </summary>
        private void GuardarYMostrarMuestra(Muestra m)
        {
            listaMuestras.Add(m);

            string hora = m.dateTime.ToString("HH:mm:ss");
            string frec = FormatearFrecuenciaConEscala(m.Frecuencia);
            string per = FormatearPeriodo(m.Periodo);
            string duty = m.DutyCycle > 0 ? $"{m.DutyCycle:F2} %" : "—";

            string errorFrec = CalcErrorFrecuencia(textBox_targetfrec.Text, m.Frecuencia);
            string errorPer = CalcErrorPeriodo(textBox_targetfrec.Text, m.Frecuencia);
            string errorDuty = CalcErrorDutyCycle(textBox_targetdutycicle.Text, m.DutyCycle);

            // Inserción en índice 0 para mostrar siempre la medición más reciente arriba
            dataGridView_visualizador.Rows.Insert(
                0,
                hora,
                frec,
                errorFrec,
                per,
                errorPer,
                duty,
                errorDuty,
                m.Contador,
                m.TiempoAlto,
                m.TiempoBajo
            );

            // Actualización de displays numéricos en tiempo real
            textBox_frecMed.Text = frec;
            textBox_Periodo.Text = per;
            textBox_dutycycleMed.Text = duty;
            textBox_cnt.Text = m.Contador.ToString();

            // Limita el historial visible en tabla a 500 filas para mantener fluidez gráfica
            if (dataGridView_visualizador.Rows.Count > 500)
            {
                dataGridView_visualizador.Rows.RemoveAt(dataGridView_visualizador.Rows.Count - 1);
            }
        }

        // ====================================================================
        // GESTIÓN DE CONEXIÓN SERIE (USB-CDC) Y PARSEO DE TELEMETRÍA
        // ====================================================================

        /// <summary>
        /// Abre o cierra la conexión serie con la Raspberry Pi Pico.
        /// Habilita DTR (Data Terminal Ready) para activar la transmisión USB-CDC en el RP2040.
        /// </summary>
        private void Bt_Connect_Click(object sender, EventArgs e)
        {
            // Desconexión si el puerto ya se encuentra abierto
            if (puertoSerie != null && puertoSerie.IsOpen)
            {
                try
                {
                    puertoSerie.DataReceived -= PuertoSerie_DataReceived;
                    puertoSerie.Close();
                    puertoSerie.Dispose();
                    Bt_Connect.Text = "Connect";
                    ComboBox_Com.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al desconectar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // Validación de selección de puerto antes de conectar
            if (ComboBox_Com.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un puerto COM válido.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string puertoElegido = ComboBox_Com.SelectedItem.ToString();
                puertoSerie = new SerialPort(puertoElegido, 115200);

                // DtrEnable es imprescindible en Raspberry Pi Pico para iniciar el flujo USB-CDC
                puertoSerie.DtrEnable = true;
                puertoSerie.NewLine = "\n";
                puertoSerie.ReadTimeout = 500;

                // Suscripción al evento asíncrono de recepción de datos
                puertoSerie.DataReceived += PuertoSerie_DataReceived;

                puertoSerie.Open();

                Bt_Connect.Text = "Disc.";
                ComboBox_Com.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir {ComboBox_Com.SelectedItem}:\n{ex.Message}", "Error de conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Garantiza el cierre limpio del puerto serie al cerrar la ventana principal.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (puertoSerie != null && puertoSerie.IsOpen)
            {
                puertoSerie.Close();
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Analiza una trama CSV recibida desde el firmware ('H', 'T' o 'Z') y construye
        /// una instancia de <see cref="Muestra"/> con los valores validados.
        /// </summary>
        /// <param name="tramaCruda">Línea ASCII recibida por el puerto serie.</param>
        /// <returns>Objeto <see cref="Muestra"/> válido o <c>null</c> si la trama está corrupta.</returns>
        public static Muestra NuevaMuestra(string tramaCruda)
        {
            if (string.IsNullOrWhiteSpace(tramaCruda)) return null;

            string[] trozos = tramaCruda.Trim().Split(',');

            // Las tramas válidas contienen 4 campos ('T', 'Z', 'C') o 5 campos ('H')
            if (trozos.Length != 4 && trozos.Length != 5) return null;

            if (!ulong.TryParse(trozos[1].Trim(), out ulong dato1) ||
                !ulong.TryParse(trozos[2].Trim(), out ulong dato2) ||
                !ulong.TryParse(trozos[3].Trim(), out ulong totalEventos))
            {
                return null;
            }

            double dutyH = 0.0;
            if (trozos.Length == 5)
            {
                // Se usa InvariantCulture para asegurar el punto '.' como separador decimal
                if (!double.TryParse(trozos[4].Trim(),
                                     NumberStyles.Any,
                                     CultureInfo.InvariantCulture,
                                     out dutyH))
                {
                    return null;
                }
            }

            return new Muestra(trozos[0].Trim(), dato1, dato2, totalEventos, dutyH);
        }

        /// <summary>
        /// Manejador del evento de recepción serie (ejecutado en un hilo secundario de E/S).
        /// Lee las tramas pendientes y delega la actualización de la UI al hilo principal mediante BeginInvoke.
        /// </summary>
        private void PuertoSerie_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (puertoSerie == null || !puertoSerie.IsOpen) return;

            try
            {
                while (puertoSerie.BytesToRead > 0)
                {
                    string linea = puertoSerie.ReadLine();
                    Muestra medida = NuevaMuestra(linea);

                    if (medida != null)
                    {
                        BeginInvoke(new Action(() => GuardarYMostrarMuestra(medida)));
                    }
                }
            }
            catch (Exception)
            {
                // Ignora excepciones de lectura provocadas por el cierre simultáneo del puerto
            }
        }

        // ====================================================================
        // EVENTOS DE CONTROL REMOTO Y EXPORTACIÓN DE DATOS
        // ====================================================================

        /// <summary>
        /// Valida los parámetros introducidos por el usuario y envía el comando de configuración
        /// al generador PWM de la Pico con formato <c>S,&lt;frecuencia&gt;,&lt;duty&gt;</c>.
        /// </summary>
        private void Bt_set_Click(object sender, EventArgs e)
        {
            if (puertoSerie == null || !puertoSerie.IsOpen)
            {
                MessageBox.Show("Conecta primero la Raspberry Pi Pico.", "Puerto cerrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Normaliza comas decimales a punto para parseo independiente de la configuración regional
            string textoFrec = textBox_FrecGen.Text.Trim().Replace(',', '.');
            string textoDuty = textBox_DutyCycleGen.Text.Trim().Replace(',', '.');

            if (!double.TryParse(textoFrec, NumberStyles.Any, CultureInfo.InvariantCulture, out double frecuencia) ||
                !double.TryParse(textoDuty, NumberStyles.Any, CultureInfo.InvariantCulture, out double duty))
            {
                MessageBox.Show("Introduce valores numéricos válidos para Frecuencia y Duty Cycle.", "Datos inválidos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if ((frecuencia != 0 && (frecuencia < 0.01 || frecuencia > 120_000_000.0)) || duty < 0.0 || duty > 100.0)
            {
                MessageBox.Show("Rango permitido:\n- Frecuencia: 0.01 Hz a 120 MHz (o 0 para apagar)\n- Duty Cycle: 0 % a 100 %",
                                "Fuera de rango", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string comando = string.Format(CultureInfo.InvariantCulture, "S,{0},{1}", frecuencia, duty);

            try
            {
                puertoSerie.WriteLine(comando);
                // Actualiza los valores de referencia empleados para el cálculo de errores
                textBox_targetfrec.Text = textBox_FrecGen.Text;
                textBox_targetdutycicle.Text = textBox_DutyCycleGen.Text;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al enviar comando a la Pico:\n{ex.Message}", "Error de envío", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Envía el comando <c>R</c> para poner a cero el contador acumulado de eventos en el firmware.
        /// </summary>
        private void bt_rst_Click(object sender, EventArgs e)
        {
            try
            {
                if (puertoSerie != null && puertoSerie.IsOpen)
                {
                    puertoSerie.WriteLine("R");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al resetear el contador: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Evento de carga del formulario; asegura que no exista una conexión serie abierta residual.
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            if (puertoSerie != null && puertoSerie.IsOpen)
            {
                try
                {
                    puertoSerie.DataReceived -= PuertoSerie_DataReceived;
                    puertoSerie.Close();
                    puertoSerie.Dispose();
                    Bt_Connect.Text = "Connect";
                    ComboBox_Com.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al desconectar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }
        }

        /// <summary>
        /// Exporta todas las filas visibles de la tabla de mediciones a un archivo CSV
        /// codificado en UTF-8 con BOM y separado por punto y coma (<c>;</c>) para compatibilidad directa con Excel.
        /// </summary>
        private void bt_CSV_Click(object sender, EventArgs e)
        {
            if (dataGridView_visualizador.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Archivo CSV (*.csv)|*.csv";
                sfd.FileName = $"mediciones_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string separador = ";";
                        var sb = new StringBuilder();

                        // 1. Escribe la fila de cabeceras de las columnas visibles
                        var cabeceras = new List<string>();
                        foreach (DataGridViewColumn col in dataGridView_visualizador.Columns)
                        {
                            if (col.Visible)
                                cabeceras.Add(col.HeaderText);
                        }
                        sb.AppendLine(string.Join(separador, cabeceras));

                        // 2. Recorre y serializa las filas registradas
                        int totalFilas = dataGridView_visualizador.Rows.Count;
                        for (int i = 0; i < totalFilas; i++)
                        {
                            DataGridViewRow fila = dataGridView_visualizador.Rows[i];
                            if (fila.IsNewRow) continue;

                            var celdas = new List<string>();
                            foreach (DataGridViewColumn col in dataGridView_visualizador.Columns)
                            {
                                if (!col.Visible) continue;
                                string valor = fila.Cells[col.Index].Value?.ToString() ?? "";
                                celdas.Add(valor.Replace(separador, ","));
                            }
                            sb.AppendLine(string.Join(separador, celdas));
                        }

                        // Guarda con firma UTF-8 BOM para preservar símbolos especiales (µs, %) en Excel
                        System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));

                        MessageBox.Show("Datos exportados correctamente.", "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error al exportar el CSV: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Envía el comando <c>P</c> para pausar o reanudar el incremento del contador de eventos
        /// en la Pico y actualiza la etiqueta del botón en consecuencia.
        /// </summary>
        private void btnPausarContador_Click(object sender, EventArgs e)
        {
            if (puertoSerie != null && puertoSerie.IsOpen)
            {
                puertoSerie.WriteLine("P");
                contadorPausado = !contadorPausado;

                // Alterna el texto del botón reflejando la siguiente acción disponible
                btnPausarContador.Text = contadorPausado ? "Continuar" : "Pausar";
            }
        }
    }
}