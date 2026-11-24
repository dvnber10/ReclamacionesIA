using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Entities
{
    public class ClaimEvent : EntityBase
    {
        private ClaimEvent() { } // EF Core

        public ClaimEvent(Guid claimId, string tipo, string actor, string payloadJson, DateTimeOffset ocurridoEn)
        {
            ClaimId = claimId;
            Tipo = tipo;
            Actor = actor;
            PayloadJson = payloadJson;
            OcurridoEn = ocurridoEn;
        }

        public Guid ClaimId { get; private set; }
        public string Tipo { get; private set; } = default!;
        public string Actor { get; private set; } = default!;
        public string PayloadJson { get; private set; } = default!;
        public DateTimeOffset OcurridoEn { get; private set; }
    }
}