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
    public class RadicacionService : IRadicacionService
    {
        private readonly IClaimRepository _repo;
        private readonly IMotorService _motor;
        public RadicacionService(IClaimRepository repo, IMotorService motor)
        {
            _repo = repo;
            _motor = motor;
        }

        public async Task<RadicarResultado> RadicarAsync(
            RadicarCommand cmd, string rutaUploads, CancellationToken ct = default)
        {
            // Doble clic / reenvío: mismo EventId => mismo caso, sin reiniciar nada.
            var existente = await _repo.GetByExternalEventIdAsync(cmd.EventId, ct);
            if (existente is not null)
                return new RadicarResultado(false, existente.PublicToken,
                    "Ya recibimos esta reclamación. No la duplicamos.",
                    Correo: existente.BeneficiaryEmail);

            var ahora = DateTimeOffset.UtcNow;
            var token = Guid.NewGuid().ToString("N");

            var claim = new Claim(
                token, cmd.BeneficiaryName, cmd.BeneficiaryDocument,
                cmd.BeneficiaryEmail, cmd.BeneficiaryPhone,
                cmd.InsuredName, cmd.InsuredDocument, cmd.FreeText,
                cmd.EventId, ahora);

            Directory.CreateDirectory(rutaUploads);

            foreach (var archivo in cmd.Archivos)
            {
                var hash = Convert.ToHexString(
                    SHA256.HashData(archivo.Contenido)).ToLowerInvariant();
                var ruta = Path.Combine(rutaUploads, $"{hash}.bin");

                if (!File.Exists(ruta))
                    await File.WriteAllBytesAsync(ruta, archivo.Contenido, ct);

                var doc = new ClaimDocument(claim.Id,
                    Path.GetFileName(archivo.Nombre), hash, ruta,
                    archivo.ContentType, archivo.TamanoBytes, ahora);

                doc.SetValidacion(null, DocumentValidation.Pendiente, null);
                claim.AgregarDocumento(doc, ahora); // hash repetido => null, se ignora
            }

            await _repo.AddAsync(claim, ct);

            await _repo.SaveAsync(ct);

            // Primer aviso = comprobante de radicación por correo y Telegram.
            claim.MarcarPrimerAviso(ahora);
            var confirmacion = "Recibimos su reclamación. Su número de seguimiento es " + token + ".";
            await _repo.AddAvisoAsync(claim, confirmacion, ahora, ct);
            await _repo.SaveAsync(ct);

            var mensaje = await _motor.ProcesarAsync(claim.Id, ct); // poner en marcha el agente

            return new RadicarResultado(true, token, "Recibimos su reclamación. " + mensaje, Correo: claim.BeneficiaryEmail);
        }
    }
}