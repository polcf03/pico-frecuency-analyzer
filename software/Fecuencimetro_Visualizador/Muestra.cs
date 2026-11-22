using System;
using System.Collections.Generic;
using System.Text;

namespace Fecuencimetro_Visualizador
{
    public class Muestra
    {
        // Class variables
        public DateTime dateTime;
        public string Modo;
        public double Frecuencia;
        public double DutyCycle;
        public double Periodo;
        public double Contador;
        public double TiempoAlto;
        public double TiempoBajo;

        // Constructor (con parámetro opcional dutyH para la trama 'H' de 5 campos)
        public Muestra(string tipo, ulong dato1, ulong dato2, ulong totalEventos, double dutyH = 0.0)
        {
            dateTime = DateTime.Now;
            Contador = totalEventos;

            switch (tipo)
            {
                case "C":
                    Modo = "Conteo (AF)";
                    double ventanaSegC = dato2 / 1_000_000.0;
                    if (ventanaSegC > 0) Frecuencia = dato1 / ventanaSegC;
                    if (Frecuencia > 0) Periodo = 1.0 / Frecuencia;
                    DutyCycle = 0;
                    TiempoAlto = 0;
                    TiempoBajo = 0;
                    break;

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

                case "H":
                    Modo = "Hardware (AF + Duty)";
                    double ventanaSegH = dato2 / 1_000_000.0;
                    if (ventanaSegH > 0) Frecuencia = dato1 / ventanaSegH;
                    if (Frecuencia > 0)
                    {
                        Periodo = 1.0 / Frecuencia;
                        DutyCycle = dutyH;
                        double periodoTotalUs = Periodo * 1_000_000.0;
                        TiempoAlto = periodoTotalUs * (DutyCycle / 100.0);
                        TiempoBajo = periodoTotalUs - TiempoAlto;
                    }
                    break;

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