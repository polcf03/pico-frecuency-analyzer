using System;

namespace Fecuencimetro_Visualizador
{
    /// <summary>
    /// Modelo de datos que representa una medición individual recibida desde la Raspberry Pi Pico.
    /// Encapsula la marca temporal, el modo de adquisición aplicado por el firmware y los
    /// parámetros físicos derivados (frecuencia, periodo, ciclo de trabajo y tiempos en alto/bajo).
    /// </summary>
    public class Muestra
    {
        // ====================================================================
        // PROPIEDADES Y CAMPOS PÚBLICOS DE LA MUESTRA
        // ====================================================================

        public DateTime dateTime;   // Instante exacto de recepción de la trama en el PC
        public string Modo;         // Descripción del método de medida utilizado por el firmware
        public double Frecuencia;   // Frecuencia calculada en Hercios (Hz)
        public double DutyCycle;    // Ciclo de trabajo en porcentaje (0.0 % a 100.0 %)
        public double Periodo;      // Periodo completo de la señal en segundos (s)
        public double Contador;     // Total acumulado de flancos ascendentes registrados
        public double TiempoAlto;   // Duración del nivel lógico ALTO en microsegundos (µs)
        public double TiempoBajo;   // Duración del nivel lógico BAJO en microsegundos (µs)

        // ====================================================================
        // CONSTRUCTOR Y CONVERSIÓN DE TRAMAS DEL PROTOCOLO
        // ====================================================================

        /// <summary>
        /// Construye una instancia de <see cref="Muestra"/> calculando las magnitudes físicas
        /// según el identificador de trama enviado por el microcontrolador.
        /// </summary>
        /// <param name="tipo">
        /// Identificador de trama:
        /// <list type="bullet">
        ///   <item><description><c>"H"</c>: Alta frecuencia (&ge; 10 kHz) con medición hardware de Duty Cycle.</description></item>
        ///   <item><description><c>"T"</c>: Baja frecuencia (&lt; 10 kHz) mediante captura recíproca de tiempos.</description></item>
        ///   <item><description><c>"C"</c>: Conteo puro en ventana temporal (compatibilidad heredada).</description></item>
        ///   <item><description><c>"Z"</c> (u otro): Timeout / ausencia de señal.</description></item>
        /// </list>
        /// </param>
        /// <param name="dato1">Pulsos en ventana (tramas 'H'/'C') o tiempo en nivel ALTO en µs (trama 'T').</param>
        /// <param name="dato2">Duración de ventana en µs (tramas 'H'/'C') o tiempo en nivel BAJO en µs (trama 'T').</param>
        /// <param name="totalEventos">Contador acumulado de flancos ascendentes.</param>
        /// <param name="dutyH">Porcentaje de Duty Cycle medido por hardware (solo aplicable a trama 'H').</param>
        public Muestra(string tipo, ulong dato1, ulong dato2, ulong totalEventos, double dutyH = 0.0)
        {
            dateTime = DateTime.Now;
            Contador = totalEventos;

            switch (tipo)
            {
                // MODO CONTEO PURO (Alta Frecuencia sin medición de Duty Cycle)
                case "C":
                    Modo = "Conteo (AF)";
                    double ventanaSegC = dato2 / 1_000_000.0;
                    if (ventanaSegC > 0) Frecuencia = dato1 / ventanaSegC;
                    if (Frecuencia > 0) Periodo = 1.0 / Frecuencia;
                    DutyCycle = 0;
                    TiempoAlto = 0;
                    TiempoBajo = 0;
                    break;

                // MODO RECÍPROCO (Baja Frecuencia < 10 kHz: medición directa de t_alto y t_bajo)
                case "T":
                    Modo = "Recíproco (BF)";
                    TiempoAlto = dato1;
                    TiempoBajo = dato2;
                    double periodoUs = dato1 + dato2;
                    if (periodoUs > 0)
                    {
                        Periodo = periodoUs / 1_000_000.0;
                        Frecuencia = 1.0 / Periodo;
                        DutyCycle = (dato1 / periodoUs) * 100.0;
                    }
                    break;

                // MODO HARDWARE DUAL (Alta Frecuencia >= 10 kHz: conteo de flancos + tiempo en alto a 240 MHz)
                case "H":
                    Modo = "Hardware (AF + Duty)";
                    double ventanaSegH = dato2 / 1_000_000.0;
                    if (ventanaSegH > 0) Frecuencia = dato1 / ventanaSegH;
                    if (Frecuencia > 0)
                    {
                        Periodo = 1.0 / Frecuencia;
                        DutyCycle = dutyH;

                        // Reconstrucción de tiempos en alto y bajo a partir del periodo y el Duty Cycle hardware
                        double periodoTotalUs = Periodo * 1_000_000.0;
                        TiempoAlto = periodoTotalUs * (DutyCycle / 100.0);
                        TiempoBajo = periodoTotalUs - TiempoAlto;
                    }
                    break;

                // MODO SIN SEÑAL (Trama 'Z' tras expirar timeout de captura)
                default:
                    Modo = "Sin señal";
                    Frecuencia = 0;
                    Periodo = 0;
                    DutyCycle = 0;
                    TiempoAlto = 0;
                    TiempoBajo = 0;
                    break;
            }
        }
    }
}