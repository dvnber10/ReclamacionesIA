using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace WebUI.Models
{
    public class SeguimientoViewModel
    {
        public string PublicToken { get; set; } = string.Empty;
        public ClaimType Tipo { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool EsCompleto { get; set; }
        public DateOnly? CompletadoEl { get; set; }
        public DateOnly? FechaLimite { get; set; }
        public string? TelegramLink { get; set; }
        public bool TieneTelegram { get; set; }
        public IReadOnlyList<string> RecibidosNombres { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> FaltanNombres { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> InvalidosNombres { get; set; } = Array.Empty<string>();
        public List<IFormFile> Archivos { get; set; } = new();
    }
}