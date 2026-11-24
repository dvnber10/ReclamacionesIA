using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Entities;
using Business.Interfaces;
using Common.Dto;
using Common.Enums;

namespace Business.Services
{
    public class PanelService : IPanelService
    {

        private readonly IClaimRepository _repo;
        public PanelService(IClaimRepository repo) => _repo = repo;

        public async Task<PanelResumen> ObtenerResumenAsync(
            PanelFiltro? filtro = null, CancellationToken ct = default)
        {
            filtro ??= new PanelFiltro();

            var todos = await _repo.ListarParaPanelAsync(ct);
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var hoyUtc = DateTime.UtcNow.Date;
            var conAviso = todos.Where(c => c.PrimerAvisoEn.HasValue
                && !c.PublicToken.StartsWith("DEMO-", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Primero construyo las filas: así los KPIs y el filtro usan
            // exactamente el mismo cálculo de riesgo/vencimiento.
            var filas = todos.OrderByDescending(c => c.CreadoEn).Select(c =>
            {
                var decisionFinal = c.Decisiones
                    .Where(d => d.Accion != DecisionAction.PedirMasDocumentos)
                    .OrderByDescending(d => d.CreadoEn)
                    .FirstOrDefault();

                var finalizadoEn = c.EsFinal ? decisionFinal?.CreadoEn : null;
                var fechaReferencia = finalizadoEn.HasValue
                    ? DateOnly.FromDateTime(finalizadoEn.Value.UtcDateTime)
                    : hoy;

                var plazoIniciado = c.CompletadoEl.HasValue && c.FechaLimite.HasValue;

                var diasRestantes = plazoIniciado
                    ? LegalClock.DiasRestantes(c.FechaLimite!.Value, fechaReferencia)
                    : (int?)null;

                var diaDelPlazo = plazoIniciado
                    ? LegalClock.DiaDelPlazo(c.CompletadoEl!.Value, fechaReferencia)
                    : (int?)null;

                var vencido = c.FechaLimite.HasValue
                    && !c.EsFinal
                    && LegalClock.Vencido(c.FechaLimite.Value, hoy);

                var enRiesgo = c.FechaLimite.HasValue
                    && !c.EsFinal
                    && !vencido
                    && LegalClock.EnRiesgo(c.FechaLimite.Value, hoy);

                return new PanelFila
                {
                    Id = c.Id,
                    Token = c.PublicToken,
                    Beneficiario = c.BeneficiaryName,
                    Tipo = c.TipoEfectivo,
                    Estado = c.Estado,
                    CreadoEn = c.CreadoEn,
                    FechaLimite = c.FechaLimite,
                    EnRiesgo = enRiesgo,
                    Vencido = vencido,
                    EsFinal = c.EsFinal,
                    FinalizadoEn = finalizadoEn,
                    DiaDelPlazo = diaDelPlazo,
                    DiasRestantes = diasRestantes,
                    RecienLlegado = !c.EsFinal
                        && c.CreadoEn.UtcDateTime.Date == hoyUtc,
                    Critico10Dias = diasRestantes.HasValue
                        && !c.EsFinal
                        && diasRestantes.Value >= 0
                        && diasRestantes.Value <= 10
                        && !enRiesgo,
                    Documentos = c.Documentos.Count
                };
            }).ToList();

            var texto = filtro.Texto?.Trim();
            var desde = filtro.Desde?.ToDateTime(TimeOnly.MinValue);
            var hasta = filtro.Hasta?.ToDateTime(TimeOnly.MinValue);

            var filtradas = filas.Where(f =>
                (string.IsNullOrWhiteSpace(texto)
                    || f.Token.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || f.Beneficiario.Contains(texto, StringComparison.OrdinalIgnoreCase))
                && (!filtro.Estado.HasValue || f.Estado == filtro.Estado.Value)
                && (!filtro.Tipo.HasValue || f.Tipo == filtro.Tipo.Value)
                && (filtro.Alerta switch
                {
                    PanelAlerta.EnRiesgo => f.EnRiesgo,
                    PanelAlerta.Vencido => f.Vencido,
                    PanelAlerta.SinAlerta => !f.EnRiesgo && !f.Vencido,
                    _ => true
                })
                && (!desde.HasValue || f.CreadoEn.UtcDateTime.Date >= desde.Value.Date)
                && (!hasta.HasValue || f.CreadoEn.UtcDateTime.Date <= hasta.Value.Date)
            ).ToList();

            return new PanelResumen
            {
                Abiertos = filas.Count(f => f.Estado is not (ClaimStatus.Pagado or ClaimStatus.Objetado or ClaimStatus.Cerrado)),
                RadicadosHoy = filas.Count(f => f.CreadoEn.UtcDateTime.Date == hoyUtc),
                EnRiesgo = filas.Count(f => f.EnRiesgo),
                Vencidos = filas.Count(f => f.Vencido && f.Estado is not (ClaimStatus.Pagado or ClaimStatus.Objetado or ClaimStatus.Cerrado)),
                CorreccionesHumanas = todos.Count(c =>
                    c.Eventos.Any(e => e.Tipo == "ClasificacionCorregida")),
                LatenciaPrimerAvisoMin = Mediana(conAviso.Select(c =>
                    (c.PrimerAvisoEn!.Value - c.CreadoEn).TotalMinutes)),
                Filtro = filtro,
                TotalGeneral = filas.Count,
                TotalFiltrados = filtradas.Count,
                Casos = filtradas
            };
        }

        public async Task<PanelDetalle?> ObtenerDetalleAsync(Guid id, CancellationToken ct = default)
        {
            var c = await _repo.GetParaPanelAsync(id, ct);
            if (c is null) return null;

            var eval = ClaimRules.Evaluar(c.TipoEfectivo, c.Documentos);
            var opcionesJson = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var revisiones = c.Eventos
                .Where(e => e.Tipo == "DocumentoValidado")
                .OrderBy(e => e.OcurridoEn)
                .Select(e =>
                {
                    var datos = JsonSerializer.Deserialize<RevisionDocumentoDatos>(
                        e.PayloadJson, opcionesJson);
                    return datos is null
                        ? null
                        : new { e.Actor, e.OcurridoEn, Datos = datos };
                })
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var ahora = DateTimeOffset.UtcNow;

            var decisionFinal = c.Decisiones
                .Where(d => d.Accion != DecisionAction.PedirMasDocumentos)
                .OrderByDescending(d => d.CreadoEn)
                .FirstOrDefault();

            var finalizadoEn = c.EsFinal ? decisionFinal?.CreadoEn : null;
            var respuestaHasta = finalizadoEn ?? ahora;

            var fechaReferencia = finalizadoEn.HasValue
                ? DateOnly.FromDateTime(finalizadoEn.Value.UtcDateTime)
                : hoy;

            var plazoIniciado = c.CompletadoEl.HasValue && c.FechaLimite.HasValue;

            var diasRestantes = plazoIniciado
                ? LegalClock.DiasRestantes(c.FechaLimite!.Value, fechaReferencia)
                : (int?)null;

            var enRiesgo = !c.EsFinal && plazoIniciado
                && LegalClock.EnRiesgo(c.FechaLimite!.Value, fechaReferencia);

            var vencido = !c.EsFinal && plazoIniciado
                && LegalClock.Vencido(c.FechaLimite!.Value, fechaReferencia);

            var dentroDelPlazo = !plazoIniciado
                || finalizadoEn is null
                || DateOnly.FromDateTime(finalizadoEn.Value.UtcDateTime) <= c.FechaLimite!.Value;


            return new PanelDetalle
            {
                Id = c.Id,
                Token = c.PublicToken,
                Beneficiario = c.BeneficiaryName,
                Asegurado = c.InsuredName,
                Email = c.BeneficiaryEmail,
                Telefono = c.BeneficiaryPhone,
                Relato = c.FreeText,
                TipoInferido = c.TipoInferido,
                Confianza = c.Confianza,
                TipoConfirmado = c.TipoConfirmado,
                TipoEfectivo = c.TipoEfectivo,
                Estado = c.Estado,
                CompletadoEl = c.CompletadoEl,
                FechaLimite = c.FechaLimite,
                CreadoEn = c.CreadoEn,
                PrimerAvisoEn = c.PrimerAvisoEn,
                EsCompleto = eval.EsCompleto,
                EsFinal = c.EsFinal,
                FinalizadoEn = finalizadoEn,
                TiempoRespuesta = respuestaHasta - c.CreadoEn,
                PlazoIniciado = plazoIniciado,
                DiasRestantes = diasRestantes,
                EnRiesgo = enRiesgo,
                Vencido = vencido,
                DentroDelPlazo = dentroDelPlazo,
                MontoIndemnizacion = decisionFinal?.MontoIndemnizacion,
                Faltan = eval.Faltan.Select(ClaimRules.Nombre).ToList(),
                Invalidos = eval.PresentesPeroInvalidos
                    .Select(o => ClaimRules.Nombre(o.Tipo) + " — " + o.Motivo).ToList(),
                Documentos = c.Documentos.OrderBy(d => d.SubidoEn).Select(d =>
                {
                    var rev = revisiones.LastOrDefault(r => r.Datos.DocumentoId == d.Id);
                    return new PanelDocumento
                    {
                        Id = d.Id,
                        NombreArchivo = d.NombreArchivo,
                        TipoDetectado = d.TipoDetectado,
                        Validacion = d.Validacion,
                        SubidoEn = d.SubidoEn,
                        RevisadoPor = rev?.Actor,
                        RevisadoEn = rev?.OcurridoEn,
                        RevisionNotas = rev?.Datos.Notas
                    };
                }).ToList(),
                Eventos = c.Eventos.OrderBy(e => e.OcurridoEn).Select(e => new PanelEvento
                {
                    Tipo = e.Tipo,
                    Actor = e.Actor,
                    PayloadJson = e.PayloadJson,
                    OcurridoEn = e.OcurridoEn
                }).ToList(),
                Decisiones = c.Decisiones.OrderBy(d => d.CreadoEn).Select(d => new PanelDecision
                {
                    Accion = d.Accion,
                    AnalistaNombre = d.AnalistaNombre,
                    Notas = d.Notas,
                    MontoIndemnizacion = d.MontoIndemnizacion,
                    CreadoEn = d.CreadoEn
                }).ToList()
            };
        }

        public async Task ConfirmarTipoAsync(
            Guid id, ClaimType tipo, string analista, CancellationToken ct = default)
        {
            var c = await _repo.GetParaPanelAsync(id, ct)
                ?? throw new InvalidOperationException("No existe esa reclamación.");

            if (c.EsFinal)
                throw new InvalidOperationException("El caso ya está cerrado.");

            var ahora = DateTimeOffset.UtcNow;
            c.ConfirmarTipo(tipo, analista, ahora);

            var eval = ClaimRules.Evaluar(c.TipoEfectivo, c.Documentos);
            if (eval.EsCompleto)
                c.MarcarCompleto(DateOnly.FromDateTime(ahora.UtcDateTime), ahora);
            else
                c.MarcarIncompleto(ahora);


            await _repo.SaveAsync(ct);
        }

                public async Task RegistrarDecisionAsync(
            Guid id,
            DecisionAction accion,
            string? notas,
            decimal? montoIndemnizacion,
            Guid analistaId,
            string analistaNombre,
            CancellationToken ct = default)
        {
            var c = await _repo.GetParaPanelAsync(id, ct)
                ?? throw new InvalidOperationException("No existe esa reclamación.");

            if (c.EsFinal)
                throw new InvalidOperationException(
                    "El caso ya tiene una decisión final y no puede modificarse.");

            if (c.TipoEfectivo == ClaimType.Desconocido)
                throw new InvalidOperationException("Confirme el tipo antes de decidir.");

            if (accion == DecisionAction.Pagar
                && (!montoIndemnizacion.HasValue || montoIndemnizacion.Value <= 0))
                throw new InvalidOperationException(
                    "Para registrar un pago debe digitar un valor de indemnización mayor a cero.");

            if (accion != DecisionAction.Pagar)
                montoIndemnizacion = null;

            var ahora = DateTimeOffset.UtcNow;

            var dentroDelPlazo = c.FechaLimite is null
                || !LegalClock.Vencido(
                    c.FechaLimite.Value,
                    DateOnly.FromDateTime(ahora.UtcDateTime));

            c.RegistrarDecision(
                new Decision(
                    id,
                    analistaId,
                    analistaNombre,
                    accion,
                    notas,
                    montoIndemnizacion,
                    ahora),
                ahora,
                dentroDelPlazo);

            if (accion == DecisionAction.PedirMasDocumentos)
            {
                var eval = ClaimRules.Evaluar(c.TipoEfectivo, c.Documentos);
                await AvisarAsync(
                    c, ClaimRules.MensajePedirDocumentos(eval, notas), ahora, ct);
            }

            await _repo.SaveAsync(ct);
        }

        // registro de validacion de los documentos
        public async Task RegistrarRevisionDocumentoAsync(Guid claimId, Guid documentoId, RequiredDocument? tipo, DocumentValidation validacion, string? notas, string analista, CancellationToken ct = default)
        {
            var c = await _repo.GetParaPanelAsync(claimId, ct)
                ?? throw new InvalidOperationException("No existe esa reclamación.");

            var ahora = DateTimeOffset.UtcNow;
            var huboCambio = c.RegistrarRevisionDocumento(
                documentoId, tipo, validacion, analista, notas, ahora);

            if (huboCambio && !c.EsFinal)
            {
                var eval = ClaimRules.Evaluar(c.TipoEfectivo, c.Documentos);

                if (eval.EsCompleto)
                {
                    c.MarcarCompleto(DateOnly.FromDateTime(ahora.UtcDateTime), ahora);
                }
                else if (c.CompletadoEl.HasValue)
                {
                    c.MarcarRequiereRevision("Un soporte fue observado por el analista", ahora);
                }
                else
                {
                    c.MarcarIncompleto(ahora);
                }

                await AvisarAsync(
                    c, ClaimRules.MensajeParaBeneficiario(eval), ahora, ct);
            }

            await _repo.SaveAsync(ct);
        }

        // metodos privados, servicios internos
        private async Task AvisarAsync(Claim claim, string mensaje, DateTimeOffset ahora, CancellationToken ct)
            => await _repo.AddAvisoAsync(claim, mensaje, ahora, ct);
        private static double? Mediana(IEnumerable<double> valores)
        {
            var ordenados = valores.OrderBy(x => x).ToList();
            if (ordenados.Count == 0) return null;

            var medio = ordenados.Count / 2;
            return ordenados.Count % 2 == 1
                ? ordenados[medio]
                : (ordenados[medio - 1] + ordenados[medio]) / 2.0;
        }

    }
}