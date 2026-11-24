using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Entities;
using Business.Interfaces;
using Common.Dto;
using Common.Enums;

namespace Business.Services
{
    public class ComplementacionService : IComplementacionService
    {
        private readonly IClaimRepository _repo;
        private readonly IClassificationService _ia;

        public ComplementacionService(IClaimRepository repo, IClassificationService ia)
        {
            _repo = repo;
            _ia = ia;
        }
        public async Task ComplementarAsync(string token, IReadOnlyList<ArchivoDto> archivos, string rutaUploads, CancellationToken ct = default)
        {
            var claim = await _repo.GetByPublicTokenConDetallesAsync(token, ct)
                ?? throw new InvalidOperationException("No existe esa reclamación.");

            var ahora = DateTimeOffset.UtcNow;
            Directory.CreateDirectory(rutaUploads);
            var nuevos = new List<ClaimDocument>();

            foreach (var archivo in archivos)
            {
                var hash = Convert.ToHexString(
                    SHA256.HashData(archivo.Contenido)).ToLowerInvariant();

                if (claim.Documentos.Any(d => d.ContentHash == hash))
                {
                    claim.AgregarEvento("DuplicadoIgnorado", "beneficiario",
                        new { archivo.Nombre }, ahora);
                    continue;
                }

                var ruta = Path.Combine(rutaUploads, $"{hash}.bin");
                if (!File.Exists(ruta))
                    await File.WriteAllBytesAsync(ruta, archivo.Contenido, ct);

                var doc = new ClaimDocument(claim.Id, Path.GetFileName(archivo.Nombre), hash,
                    ruta, archivo.ContentType, archivo.TamanoBytes, ahora);
                // doc.SetValidacion(null, DocumentValidation.Pendiente, null);
                doc.SetValidacion(ClaimRules.DetectarPorNombre(archivo.Nombre),
                    DocumentValidation.Valido, null);

                if (claim.AgregarDocumento(doc, ahora) is not null)
                    nuevos.Add(doc);
            }

            if (nuevos.Count > 0)
            {
                var resultado = await _ia.ClasificarAsync(new ClasificacionRequest
                {
                    TextoLibre = claim.FreeText,
                    NombresArchivos = nuevos.Select(d => d.NombreArchivo).ToList()
                }, ct);

                foreach (var detectado in resultado.Documentos)
                {
                    var doc = nuevos.FirstOrDefault(d => string.Equals(
                        d.NombreArchivo, detectado.NombreArchivo,
                        StringComparison.OrdinalIgnoreCase));
                    if (doc is null) continue;

                    var tipo = detectado.Tipo ?? ClaimRules.DetectarPorNombre(detectado.NombreArchivo);
                    var validacion = detectado.Validacion;
                    // var validacion = resultado.Exito
                    //     ? detectado.Validacion
                    //     : DocumentValidation.Pendiente;
                    doc.SetValidacion(tipo, validacion, detectado.Campos);
                }
            }

            var evaluacion = ClaimRules.Evaluar(claim.TipoEfectivo, claim.Documentos);

            // MarcarCompleto es idempotente: si el reloj ya arrancó, nadie lo reinicia.
            if (evaluacion.EsCompleto)
                claim.MarcarCompleto(DateOnly.FromDateTime(ahora.UtcDateTime), ahora);
            else
                claim.MarcarIncompleto(ahora);

            if (!evaluacion.EsCompleto)
                claim.MarcarPrimerAviso(ahora);

            var mensaje = ClaimRules.MensajeParaBeneficiario(evaluacion);
            await _repo.AddAvisoAsync(claim, mensaje, ahora, ct);
            await _repo.SaveAsync(ct);
        }
    }
}