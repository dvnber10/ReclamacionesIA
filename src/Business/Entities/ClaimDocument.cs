using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace Business.Entities
{
    public class ClaimDocument : EntityBase
    {
        private ClaimDocument() { } // EF Core

        public ClaimDocument(
            Guid claimId,
            string nombreArchivo,
            string contentHash,
            string ruta,
            string contentType,
            long tamanoBytes,
            DateTimeOffset subidoEn)
        {
            ClaimId = claimId;
            NombreArchivo = nombreArchivo;
            ContentHash = contentHash;
            Ruta = ruta;
            ContentType = contentType;
            TamanoBytes = tamanoBytes;
            SubidoEn = subidoEn;
            Validacion = DocumentValidation.Pendiente;
        }

        public Guid ClaimId { get; private set; }
        public string NombreArchivo { get; private set; } = default!;
        public string ContentHash { get; private set; } = default!;   // SHA-256
        public string Ruta { get; private set; } = default!;
        public string ContentType { get; private set; } = default!;
        public long TamanoBytes { get; private set; }

        public RequiredDocument? TipoDetectado { get; private set; }
        public DocumentValidation Validacion { get; private set; }
        public string? ExtraidoJson { get; private set; }
        public DateTimeOffset SubidoEn { get; private set; }

        public void SetValidacion(RequiredDocument? tipo, DocumentValidation validacion, string? extraido)
        {
            TipoDetectado = tipo;
            Validacion = validacion;
            ExtraidoJson = extraido;
        }
    }
}