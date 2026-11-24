using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Entities;
using Business.Interfaces;
using Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories
{
    public class ClaimRepository : IClaimRepository
    {
        private readonly ReclamacionesDbContext _db;
        public ClaimRepository(ReclamacionesDbContext db) => _db = db;

        public IQueryable<Claim> Query() => _db.Reclamaciones.AsQueryable();

        public Task<Claim?> GetByExternalEventIdAsync(string eventId, CancellationToken ct = default)
            => _db.Reclamaciones.AsNoTracking()
                  .FirstOrDefaultAsync(c => c.ExternalEventId == eventId, ct);

        public async Task AddAsync(Claim claim, CancellationToken ct = default) => await _db.Reclamaciones.AddAsync(claim, ct);

        public async Task AddOutboxAsync(OutboxMessage mensaje, CancellationToken ct = default) => await _db.Outbox.AddAsync(mensaje, ct);

        public async Task SaveAsync(CancellationToken ct = default)
            => await _db.SaveChangesAsync(ct); public async Task<Claim?> GetConDetallesAsync(Guid id, CancellationToken ct = default)
             => await _db.Reclamaciones
             .Include(c => c.Documentos)
             .FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task AddInferenciaAsync(InferenciaIA inferencia, CancellationToken ct = default) => await _db.Inferencias.AddAsync(inferencia, ct);

        // Buscar por token publico
        public async Task<Claim?> GetByPublicTokenConDetallesAsync(string token, CancellationToken ct = default)
            => await _db.Reclamaciones
                .Include(c => c.Documentos)
                .FirstOrDefaultAsync(c => c.PublicToken == token, ct);

        public async Task<List<Claim>> ListarParaPanelAsync(CancellationToken ct = default)
            => await _db.Reclamaciones
                .Include(c => c.Documentos)
                .Include(c => c.Eventos)
                .Include(c => c.Decisiones)
                .AsNoTracking()
                .ToListAsync(ct);

        public async Task<Claim?> GetParaPanelAsync(Guid id, CancellationToken ct = default)
            => await _db.Reclamaciones
                .Include(c => c.Documentos)
                .Include(c => c.Eventos)
                .Include(c => c.Decisiones)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task<ClaimDocument?> GetDocumentoAsync(Guid id, CancellationToken ct = default) => await _db.Documentos.AsNoTracking()
        .FirstOrDefaultAsync(d => d.Id == id, ct);

        public async Task AddAvisoAsync(
            Claim claim, string mensaje, DateTimeOffset ahora,
            CancellationToken ct = default)
        {
            var payload = JsonSerializer.Serialize(new { token = claim.PublicToken, mensaje });

            foreach (var canal in new[] { OutboxChannel.Telegram, OutboxChannel.Web })
            {
                if (await ExisteOutboxPendienteAsync(claim.Id, payload, canal, ct))
                    continue;

                var destino = canal == OutboxChannel.Telegram
                    ? claim.TelegramChatId ?? ""
                    : claim.BeneficiaryEmail ?? "";

                await AddOutboxAsync(
                    new OutboxMessage(claim.Id, canal, destino, payload, ahora), ct);
            }
        }

        public Task<bool> ExisteOutboxPendienteAsync(
            Guid claimId, string payloadJson, OutboxChannel canal,
            CancellationToken ct = default)
            => _db.Outbox.AsNoTracking().AnyAsync(o =>
                o.ClaimId == claimId &&
                o.Canal == canal &&
                o.Estado == OutboxStatus.Pendiente &&
                o.PayloadJson == payloadJson, ct);

                public async Task<List<Claim>> ListarEnRiesgoSinAvisoAsync(
            CancellationToken ct = default)
            => await _db.Reclamaciones
                .Include(c => c.Eventos)
                .Where(c => c.FechaLimite.HasValue
                    && c.Estado != ClaimStatus.Cerrado
                    && c.Estado != ClaimStatus.Pagado
                    && c.Estado != ClaimStatus.Objetado
                    && !c.Eventos.Any(e => e.Tipo == "AvisoVencimiento"))
                .ToListAsync(ct);
    }
}