using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Dto
{
    public class RadicarCommand
    {
        public string EventId { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public string BeneficiaryDocument { get; set; } = string.Empty;
        public string? BeneficiaryEmail { get; set; }
        public string? BeneficiaryPhone { get; set; }
        public string InsuredName { get; set; } = string.Empty;
        public string InsuredDocument { get; set; } = string.Empty;
        public string FreeText { get; set; } = string.Empty;
        public IReadOnlyList<ArchivoDto> Archivos { get; set; } = Array.Empty<ArchivoDto>();
    }
}