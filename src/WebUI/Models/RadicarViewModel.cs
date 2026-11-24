using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.Models
{
    public class RadicarViewModel
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        [Required(ErrorMessage = "Necesitamos su nombre.")]
        public string BeneficiaryName { get; set; } = "";

        [Required(ErrorMessage = "Necesitamos su documento.")]
        public string BeneficiaryDocument { get; set; } = "";

        [EmailAddress(ErrorMessage = "El correo no parece válido.")]
        public string? BeneficiaryEmail { get; set; }

        public string? BeneficiaryPhone { get; set; }

        [Required(ErrorMessage = "Necesitamos el nombre del asegurado.")]
        public string InsuredName { get; set; } = "";

        [Required(ErrorMessage = "Necesitamos el documento del asegurado.")]
        public string InsuredDocument { get; set; } = "";

        [Required(ErrorMessage = "Cuéntenos qué pasó.")]
        public string FreeText { get; set; } = "";

        public List<IFormFile> Archivos { get; set; } = new();
    }
}