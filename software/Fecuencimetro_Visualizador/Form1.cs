using System;
using System.Globalization;
using System.IO.Ports;
using System.Windows.Forms;

namespace Fecuencimetro_Visualizador
{
    public partial class Form1 : Form
    {
        private bool contadorPausado = false;
        private SerialPort puertoSerie;
        private List<Muestra> listaMuestras = new List<Muestra>();
        public Form1()
        {
            InitializeComponent();
            CargarPuertosDisponibles();
            ConfigurarComboEscala();
            ConfigurarTabla();
            // Configuramos el ComboBox para que solo permita elegir elementos de la lista
            ComboBox_Com.DropDownStyle = ComboBoxStyle.DropDownList;

            // Cada vez que despliegues el menú, refresca los puertos por si conectaste la Pico después
            ComboBox_Com.DropDown += (s, e) => CargarPuertosDisponibles();

            textBox_FrecGen.Text = "1000";
            textBox_DutyCycleGen.Text = "50";
            textBox_targetfrec.Text = "1000";
            textBox_targetdutycicle.Text = "50";


        }

        // Funciones de configuración
        private void ConfigurarTabla()
        {
            dataGridView_visualizador.Columns.Clear();

            // Definimos las columnas (NombreInterno, TextoCabecera)
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

            // Ajustes visuales para que se comporte como un visor limpio
            dataGridView_visualizador.ReadOnly = true;
            dataGridView_visualizador.AllowUserToAddRows = false;
            dataGridView_visualizador.RowHeadersVisible = false;
            dataGridView_visualizador.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }
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
        private void ConfigurarComboEscala()
        {
            comboBox_Escala.Items.Clear();
            comboBox_Escala.Items.AddRange(new string[] { "Auto", "Hz", "kHz", "MHz" });
            comboBox_Escala.DropDownStyle = ComboBoxStyle.DropDownList; // Evita que escriban texto libre
            comboBox_Escala.SelectedIndex = 0; // Selecciona "Auto" por defecto
        }
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


        private string FormatearPeriodo(double seg)
        {
            if (seg <= 0) return "—";
            if (seg < 1e-6) return $"{seg * 1e9:F2} ns";
            if (seg < 1e-3) return $"{seg * 1e6:F2} µs";
            if (seg < 1.0) return $"{seg * 1e3:F3} ms";
            return $"{seg:F4} s";
        }
        private string CalcErrorFrecuencia(string target, double medida)
        {
            double targetVal = double.TryParse(target, out double t) ? t : 0;

            if (targetVal <= 0) return "—";
            double error = (Math.Abs(targetVal - medida) / targetVal) * 100.0;
            return $"{error:F2} %";
        }
        private string CalcErrorPeriodo(string target, double frec)
        {
            double medida = 1 / frec;
            double targetfrec = double.TryParse(target, out double t) ? t : 0;

            double targetVal = 1 / (targetfrec > 0 ? targetfrec : 1);
            if (targetVal <= 0) return "—";
            double error = (Math.Abs(targetVal - medida) / targetVal) * 100.0;
            return $"{error:F2} %";
        }
        private string CalcErrorDutyCycle(string target, double medida)
        {
            double targetVal = double.TryParse(target, out double t) ? t : 0;
            if (targetVal <= 0) return "—";
            double error = (Math.Abs(targetVal - medida) / targetVal) * 100.0;
            return $"{error:F2} %";
        }
        private void GuardarYMostrarMuestra(Muestra m)
        {
            listaMuestras.Add(m);

            string hora = m.dateTime.ToString("HH:mm:ss");
            string frec = FormatearFrecuenciaConEscala(m.Frecuencia);// Si dejaste el nombre anterior, usa m.Frencuencia
            string per = FormatearPeriodo(m.Periodo);
            string duty = m.DutyCycle > 0 ? $"{m.DutyCycle:F2} %" : "—";

            string errorFrec = CalcErrorFrecuencia(textBox_targetfrec.Text, m.Frecuencia);
            string errorPer = CalcErrorPeriodo(textBox_targetfrec.Text, m.Frecuencia);
            string errorDuty = CalcErrorDutyCycle(textBox_targetdutycicle.Text, m.DutyCycle);

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

            textBox_frecMed.Text = frec;
            textBox_Periodo.Text = per;
            textBox_dutycycleMed.Text = duty;
            textBox_cnt.Text = m.Contador.ToString();

            if (dataGridView_visualizador.Rows.Count > 500)
            {
                dataGridView_visualizador.Rows.RemoveAt(dataGridView_visualizador.Rows.Count - 1);
            }
        }

        private void Bt_Connect_Click(object sender, EventArgs e)
        {
            // Si ya está conectado, desconectamos
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

            // Si está desconectado, intentamos conectar
            if (ComboBox_Com.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un puerto COM válido.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string puertoElegido = ComboBox_Com.SelectedItem.ToString();
                puertoSerie = new SerialPort(puertoElegido, 115200);

                // DtrEnable es necesario en la Raspberry Pi Pico para que active el flujo USB CDC
                puertoSerie.DtrEnable = true;
                puertoSerie.NewLine = "\n";
                puertoSerie.ReadTimeout = 500;

                // Aquí le decimos qué método ejecutar cuando lleguen bytes
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

        // Asegura que el puerto COM se libere al cerrar la ventana
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (puertoSerie != null && puertoSerie.IsOpen)
            {
                puertoSerie.Close();
            }
            base.OnFormClosing(e);
        }

        // Método para crear una nueva muestra a partir de la trama cruda recibida
        public static Muestra NuevaMuestra(string tramaCruda)
        {
            if (string.IsNullOrWhiteSpace(tramaCruda)) return null;

            string[] trozos = tramaCruda.Trim().Split(',');

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
                if (!double.TryParse(trozos[4].Trim(),
                                     System.Globalization.NumberStyles.Any,
                                     System.Globalization.CultureInfo.InvariantCulture,
                                     out dutyH))
                {
                    return null;
                }
            }

            return new Muestra(trozos[0].Trim(), dato1, dato2, totalEventos, dutyH);
        }

        private void PuertoSerie_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (puertoSerie == null || !puertoSerie.IsOpen) return;

            try
            {
                // Leemos todas las líneas completas que hayan llegado al buffer
                while (puertoSerie.BytesToRead > 0)
                {
                    // Llamamos a tu clase Muestra
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
                // Ignora errores puntuales si se cierra el puerto mientras estaba leyendo
            }
        }

        private void Bt_set_Click(object sender, EventArgs e)
        {
            if (puertoSerie == null || !puertoSerie.IsOpen)
            {
                MessageBox.Show("Conecta primero la Raspberry Pi Pico.", "Puerto cerrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string textoFrec = textBox_FrecGen.Text.Trim().Replace(',', '.');
            string textoDuty = textBox_DutyCycleGen.Text.Trim().Replace(',', '.');

            if (!double.TryParse(textoFrec, NumberStyles.Any, CultureInfo.InvariantCulture, out double frecuencia) ||
                !double.TryParse(textoDuty, NumberStyles.Any, CultureInfo.InvariantCulture, out double duty))
            {
                MessageBox.Show("Introduce valores numéricos válidos para Frecuencia y Duty Cycle.", "Datos inválidos", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
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
                textBox_targetfrec.Text = textBox_FrecGen.Text;
                textBox_targetdutycicle.Text = textBox_DutyCycleGen.Text;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al enviar comando a la Pico:\n{ex.Message}", "Error de envío", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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

        private void Form1_Load(object sender, EventArgs e)
        {
            // Si está conectado, desconectamos
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
                        // En España, Excel abre directamente las columnas separadas si usas ';' y UTF-8 con BOM
                        string separador = ";";
                        var sb = new System.Text.StringBuilder();

                        // 1. Escribir cabeceras de las columnas visibles
                        var cabeceras = new List<string>();
                        foreach (DataGridViewColumn col in dataGridView_visualizador.Columns)
                        {
                            if (col.Visible)
                                cabeceras.Add(col.HeaderText);
                        }
                        sb.AppendLine(string.Join(separador, cabeceras));

                        // 2. Escribir cada fila de datos (haciendo copia segura por si siguen entrando tramas)
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

                        // Guardar con UTF-8 BOM para que Excel reconozca tildes y símbolos (µs, %)
                        System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));

                        MessageBox.Show("Datos exportados correctamente.", "Exportar CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error al exportar el CSV: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
       
        private void btnPausarContador_Click(object sender, EventArgs e)
        {

            if (puertoSerie != null && puertoSerie.IsOpen)
            {
                puertoSerie.WriteLine("P");
                contadorPausado = !contadorPausado;

                // Cambia el texto del botón para indicar la acción disponible
                btnPausarContador.Text = contadorPausado ? "Continuar" : "Pausar";
            }
            
        }
    }
}