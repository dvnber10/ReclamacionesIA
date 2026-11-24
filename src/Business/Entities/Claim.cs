using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Enums;

namespace Business.Entities
{
    public sealed class Claim : EntityBase
    {
        private Claim() { } // EF Core

        public Claim(
            string publicToken,
            string beneficiaryName,
            string beneficiaryDocument,
            string? beneficiaryEmail,
            string? beneficiaryPhone,
            string insuredName,
            string insuredDocument,
            string freeText,
            string? externalEventId = null,
            DateTimeOffset? ahora = null)
        {
            PublicToken = publicToken;
            BeneficiaryName = beneficiaryName;
            BeneficiaryDocument = beneficiaryDocument;
            BeneficiaryEmail = beneficiaryEmail;
            BeneficiaryPhone = beneficiaryPhone;
            InsuredName = insuredName;
            InsuredDocument = insuredDocument;
            FreeText = freeText;
            ExternalEventId = externalEventId;
            CreadoEn = ahora ?? DateTimeOffset.UtcNow;
            Estado = ClaimStatus.Radicado;
            TipoInferido = ClaimType.Desconocido;

            AgregarEvento("Radicado", "beneficiario", new { freeText }, CreadoEn);
        }

        public string PublicToken { get; private set; } = default!;
        public string? ExternalEventId { get; private set; }
        public string BeneficiaryName { get; private set; } = default!;
        public string BeneficiaryDocument { get; private set; } = default!;
        public string? BeneficiaryEmail { get; private set; }
        public string? BeneficiaryPhone { get; private set; }
        public string InsuredName { get; private set; } = default!;
        public string InsuredDocument { get; private set; } = default!;
        public string FreeText { get; private set; } = default!;

        public ClaimType TipoInferido { get; private set; }
        public double? Confianza { get; private set; }
        public string? InferenciaRaw { get; private set; }
        public ClaimType? TipoConfirmado { get; private set; }
        public ClaimStatus Estado { get; private set; }

        public DateTimeOffset CreadoEn { get; private set; }
        public DateOnly? CompletadoEl { get; private set; }
        public DateOnly? FechaLimite { get; private set; }
        public DateTimeOffset? PrimerAvisoEn { get; private set; }
        public string? TelegramChatId { get; private set; }
        public DateTimeOffset? TelegramVinculadoEn { get; private set; }

        // navegacion a colecciones
        public ICollection<ClaimDocument> Documentos { get; private set; } = new List<ClaimDocument>();
        public ICollection<ClaimEvent> Eventos { get; private set; } = new List<ClaimEvent>();
        public ICollection<Decision> Decisiones { get; private set; } = new List<Decision>();

        // El tipo que manda es el del humano si corrigió; si no, el del modelo.
        public ClaimType TipoEfectivo => TipoConfirmado ?? TipoInferido;
        public bool EsFinal => Estado is ClaimStatus.Pagado
            or ClaimStatus.Objetado
            or ClaimStatus.Cerrado;

        public void SetClasificacion(ClaimType tipo, double confianza, string rawJson, DateTimeOffset ahora)
        {
            TipoInferido = tipo;
            Confianza = confianza;
            InferenciaRaw = rawJson;
            Estado = ClaimStatus.EnAnalisis;
            AgregarEvento("Clasificado", "modelo", new { tipo, confianza }, ahora);
        }

        public bool ConfirmarTipo(ClaimType tipo, string analista, DateTimeOffset ahora)
        {
            if (TipoConfirmado == tipo) return false;

            var anterior = TipoConfirmado ?? TipoInferido;
            TipoConfirmado = tipo;
            AgregarEvento("ClasificacionCorregida", analista, new { anterior, nuevo = tipo }, ahora);
            return true;
        }

        public ClaimDocument? AgregarDocumento(ClaimDocument documento, DateTimeOffset ahora)
        {
            if (documento.ClaimId != Id)
                throw new InvalidOperationException("El documento pertenece a otro caso.");

            // Idempotencia: el mismo archivo dos veces no agrega nada.
            if (Documentos.Any(d => d.ContentHash == documento.ContentHash))
            {
                AgregarEvento("DuplicadoIgnorado", "sistema", new { documento.NombreArchivo }, ahora);
                return null;
            }

            Documentos.Add(documento);
            AgregarEvento("DocumentoRecibido", "beneficiario",
                new { documento.NombreArchivo, documento.TipoDetectado }, ahora);
            return documento;
        }

        public bool MarcarCompleto(DateOnly fecha, DateTimeOffset ahora)
        {
            if (CompletadoEl.HasValue)
            {
                if (Estado == ClaimStatus.Completo) return false;
                Estado = ClaimStatus.Completo; // FechaLimite NO se toca
                AgregarEvento("ExpedienteReconfirmado", "sistema",
                    new { fechaLimite = FechaLimite }, ahora);
                return false;
            }

            CompletadoEl = fecha;
            FechaLimite = fecha.AddMonths(1);
            Estado = ClaimStatus.Completo;
            AgregarEvento("ExpedienteCompleto", "sistema",
                new { fecha, fechaLimite = FechaLimite }, ahora);
            return true;
        }

        public void MarcarIncompleto(DateTimeOffset ahora)
        {
            if (CompletadoEl.HasValue) return; // el plazo no se detiene
            Estado = ClaimStatus.Incompleto;
        }

        public void MarcarPrimerAviso(DateTimeOffset ahora)
        {
            if (PrimerAvisoEn.HasValue) return; // métrica #3: una sola vez
            PrimerAvisoEn = ahora;
            AgregarEvento("PrimerAviso", "sistema", new { }, ahora);
        }
        public bool MarcarAvisoVencimiento(DateTimeOffset ahora)
        {
            if (Eventos.Any(e => e.Tipo == "AvisoVencimiento")) return false;

            AgregarEvento("AvisoVencimiento", "sistema",
                new { fechaLimite = FechaLimite }, ahora);
            return true;
        }

        public void RegistrarDecision(
            Decision decision, DateTimeOffset ahora, bool dentroDelPlazo)
        {
            Decisiones.Add(decision);

            Estado = decision.Accion switch
            {
                DecisionAction.Pagar => ClaimStatus.Pagado,
                DecisionAction.Objetar => ClaimStatus.Objetado,
                _ => ClaimStatus.Incompleto
            };

            AgregarEvento("DecisionHumana", decision.AnalistaNombre,
                new
                {
                    decision.Accion,
                    decision.Notas,
                    montoIndemnizacion = decision.MontoIndemnizacion,
                    dentroDelPlazo
                }, ahora);
        }

        public void AgregarEvento(string tipo, string actor, object payload, DateTimeOffset ahora)
            => Eventos.Add(new ClaimEvent(Id, tipo, actor, JsonSerializer.Serialize(payload), ahora));

        public void MarcarRequiereRevision(string motivo, DateTimeOffset ahora)
        {
            if (Estado == ClaimStatus.RequiereRevision) return; // no repetir evento
            Estado = ClaimStatus.RequiereRevision;
            AgregarEvento("RequiereRevision", "sistema", new { motivo }, ahora);
        }
        public bool RegistrarRevisionDocumento(
            Guid documentoId,
            RequiredDocument? tipo,
            DocumentValidation validacion,
            string analista,
            string? notas,
            DateTimeOffset ahora)
        {
            var doc = Documentos.FirstOrDefault(d => d.Id == documentoId)
                ?? throw new InvalidOperationException("Ese documento no pertenece al caso.");

            var tipoAnterior = doc.TipoDetectado;
            var validacionAnterior = doc.Validacion;

            if (tipoAnterior == tipo && validacionAnterior == validacion)
                return false;

            doc.SetValidacion(tipo, validacion, doc.ExtraidoJson);

            AgregarEvento("DocumentoValidado", analista, new
            {
                documentoId,
                nombreArchivo = doc.NombreArchivo,
                tipoAnterior,
                tipoNuevo = tipo,
                validacionAnterior,
                validacionNueva = validacion,
                notas
            }, ahora);

            return true;
        }
        public bool VincularTelegram(string chatId, DateTimeOffset ahora)
        {
            if (string.IsNullOrWhiteSpace(chatId)) return false;
            if (TelegramChatId == chatId) return false; // idempotente

            TelegramChatId = chatId;
            TelegramVinculadoEn = ahora;
            AgregarEvento("TelegramVinculado", "beneficiario", new { chatId }, ahora);
            return true;
        }
    }

}