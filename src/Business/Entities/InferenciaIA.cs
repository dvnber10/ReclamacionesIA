using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace Business.Entities
{
    public class InferenciaIA : EntityBase
    {
        private InferenciaIA() { } // EF Core

        private InferenciaIA(Guid claimId, AiProvider proveedor, string modelo, string promptVersion,
            string solicitudJson, string? respuestaRaw, bool exito, string? error,
            int latenciaMs, double? confianza, DateTimeOffset creadoEn)
        {
            ClaimId = claimId;
            Proveedor = proveedor;
            Modelo = modelo;
            PromptVersion = promptVersion;
            SolicitudJson = solicitudJson;
            RespuestaRaw = respuestaRaw;
            Exito = exito;
            Error = error;
            LatenciaMs = latenciaMs;
            Confianza = confianza;
            CreadoEn = creadoEn;
        }
        // propiedades de la entidad
        public Guid ClaimId { get; private set; }
        public AiProvider Proveedor { get; private set; }
        public string Modelo { get; private set; } = default!;
        public string PromptVersion { get; private set; } = default!;
        public string SolicitudJson { get; private set; } = default!;
        public string? RespuestaRaw { get; private set; }
        public bool Exito { get; private set; }
        public string? Error { get; private set; }
        public int LatenciaMs { get; private set; }
        public double? Confianza { get; private set; }
        public DateTimeOffset CreadoEn { get; private set; }

        // metodos de las reglas de negocio

        public static InferenciaIA Correcto(Guid claimId, AiProvider proveedor, string modelo,
            string promptVersion, string solicitud, string raw, double? confianza,
            int latenciaMs, DateTimeOffset ahora)
            => new(claimId, proveedor, modelo, promptVersion, solicitud, raw, true, null,
                   latenciaMs, confianza, ahora);

        public static InferenciaIA Fallido(Guid claimId, AiProvider proveedor, string modelo,
            string promptVersion, string solicitud, string error, int latenciaMs, DateTimeOffset ahora)
            => new(claimId, proveedor, modelo, promptVersion, solicitud, null, false, error,
                   latenciaMs, null, ahora);
    }
}