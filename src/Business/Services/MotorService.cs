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
    public class MotorService : IMotorService
    {
        private const double UmbralConfianza = 0.60;

        private readonly IClaimRepository _repo;
        private readonly IClassificationService _ia;

        public MotorService(IClaimRepository repo, IClassificationService ia)
        {
            _repo = repo;
            _ia = ia;
        }

        public async Task<string> ProcesarAsync(Guid claimId, CancellationToken ct = default)
        {
            var claim = await _repo.GetConDetallesAsync(claimId, ct)
                ?? throw new InvalidOperationException("No existe esa reclamación.");

            var ahora = DateTimeOffset.UtcNow;

            var resultado = await _ia.ClasificarAsync(new ClasificacionRequest
            {
                TextoLibre = claim.FreeText,
                NombresArchivos = claim.Documentos.Select(d => d.NombreArchivo).ToList()
            }, ct);

            // Auditoría SIEMPRE, incluso cuando falla.
            var solicitud = JsonSerializer.Serialize(new
            {
                texto = claim.FreeText,
                archivos = claim.Documentos.Select(d => d.NombreArchivo)
            });

            var inferencia = resultado.Exito
                ? InferenciaIA.Correcto(claim.Id, resultado.Proveedor, resultado.Modelo, "v1",
                    solicitud, resultado.Raw, resultado.Confianza, resultado.LatenciaMs, ahora)
                : InferenciaIA.Fallido(claim.Id, resultado.Proveedor, resultado.Modelo, "v1",
                    solicitud, resultado.Error ?? "Error sin detalle", resultado.LatenciaMs, ahora);

            await _repo.AddInferenciaAsync(inferencia, ct);
            var mensaje = "";
            // Sin tipo o confianza baja: nadie inventa y nadie arranca el reloj.
            if (resultado.Tipo == ClaimType.Desconocido
                || (resultado.Exito && resultado.Confianza < UmbralConfianza))
            {
                mensaje = "Estamos revisando su caso. Un analista le confirmará qué necesitamos.";
                claim.MarcarRequiereRevision(resultado.Error ?? "Confianza insuficiente en la clasificación", ahora);
                await AvisarAsync(claim, mensaje, ahora, ct);
                await _repo.SaveAsync(ct);
                return mensaje;
            }

            claim.SetClasificacion(resultado.Tipo, resultado.Confianza, resultado.Raw, ahora);

            foreach (var detectado in resultado.Documentos)
            {
                var doc = claim.Documentos.FirstOrDefault(d =>
                    string.Equals(d.NombreArchivo, detectado.NombreArchivo,
                        StringComparison.OrdinalIgnoreCase));

                doc?.SetValidacion(detectado.Tipo, detectado.Validacion, detectado.Campos);
            }

            // Fallback por reglas: se deja la sugerencia, pero NO arranca el plazo
            // hasta que un humano confirme el tipo.
            if (resultado.FueFallback)
            {
                mensaje = "Estamos revisando su caso. Un analista confirmará la información.";
                claim.MarcarRequiereRevision("Clasificado por reglas locales (IA no disponible). Confirmar tipo.", ahora);
                await AvisarAsync(claim, mensaje, ahora, ct);
                await _repo.SaveAsync(ct);
                return mensaje;
            }

            var evaluacion = ClaimRules.Evaluar(claim.TipoEfectivo, claim.Documentos);

            if (evaluacion.EsCompleto)
            {
                claim.MarcarCompleto(DateOnly.FromDateTime(ahora.UtcDateTime), ahora);
            }
            else
            {
                claim.MarcarIncompleto(ahora);
            }

            mensaje = ClaimRules.MensajeParaBeneficiario(evaluacion);
            await AvisarAsync(claim, mensaje, ahora, ct);
            await _repo.SaveAsync(ct);
            return mensaje;
        }

         private async Task AvisarAsync(Claim claim, string mensaje, DateTimeOffset ahora,
            CancellationToken ct)
        {
            claim.MarcarPrimerAviso(ahora);
            await _repo.AddAvisoAsync(claim, mensaje, ahora, ct);
        }
    }
}