using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace Common.Dto
{
    public sealed class ArchivoImagenDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Base64 { get; set; } = string.Empty;
        public string ContentType { get; set; } = "image/jpeg";
    }

    public sealed class ClasificacionRequest
    {
        public string TextoLibre { get; set; } = string.Empty;
        public IReadOnlyList<string> NombresArchivos { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ArchivoImagenDto> Imagenes { get; set; } = Array.Empty<ArchivoImagenDto>();
    }

    public sealed class DocumentoDetectadoDto
    {
        public string NombreArchivo { get; set; } = string.Empty;
        public RequiredDocument? Tipo { get; set; }
        public DocumentValidation Validacion { get; set; } = DocumentValidation.Pendiente;
        public string? Campos { get; set; }
    }

    public sealed class ClasificacionResultado
    {
        public bool Exito { get; set; }
        public string? Error { get; set; }
        public ClaimType Tipo { get; set; } = ClaimType.Desconocido;
        public double Confianza { get; set; }
        public string Raw { get; set; } = string.Empty;
        public bool FueFallback { get; set; }
        public TipoFalloIA TipoFallo { get; set; } = TipoFalloIA.Ninguno;
        public AiProvider Proveedor { get; set; } = AiProvider.Ollama;
        public string Modelo { get; set; } = string.Empty;
        public int LatenciaMs { get; set; }
        public IReadOnlyList<DocumentoDetectadoDto> Documentos { get; set; } = Array.Empty<DocumentoDetectadoDto>();
    }
}