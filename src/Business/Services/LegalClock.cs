using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Services
{
    public class LegalClock
    {
        public const int DiasAlerta = 3;

        public static DateOnly CalcularFechaLimite(DateOnly inicio) => inicio.AddMonths(1);

        public static int DiaDelPlazo(DateOnly inicio, DateOnly hoy)
            => Math.Max(1, hoy.DayNumber - inicio.DayNumber + 1);

        public static int DiasRestantes(DateOnly limite, DateOnly hoy)
            => limite.DayNumber - hoy.DayNumber;

        public static bool EnRiesgo(DateOnly limite, DateOnly hoy)
        {
            var restantes = DiasRestantes(limite, hoy);
            return restantes >= 0 && restantes <= DiasAlerta;
        }

        public static bool Vencido(DateOnly limite, DateOnly hoy) => hoy > limite;
    }
}