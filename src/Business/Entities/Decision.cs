using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace Business.Entities
{
    public class Decision : EntityBase
    {
        private Decision() { } // EF Core

        public Decision(
            Guid claimId,
            Guid analistaId,
            string analistaNombre,
            DecisionAction accion,
            string? notas,
            decimal? montoIndemnizacion = null,
            DateTimeOffset? creadoEn = null)
        {
            ClaimId = claimId;
            AnalistaId = analistaId;
            AnalistaNombre = analistaNombre;
            Accion = accion;
            Notas = notas;
            MontoIndemnizacion = montoIndemnizacion;
            CreadoEn = creadoEn ?? DateTimeOffset.UtcNow;
        }

        public Guid ClaimId { get; private set; }
        public Guid AnalistaId { get; private set; }
        public string AnalistaNombre { get; private set; } = default!;
        public DecisionAction Accion { get; private set; }
        public string? Notas { get; private set; }
        public decimal? MontoIndemnizacion { get; private set; }
        public DateTimeOffset CreadoEn { get; private set; }
    }
}