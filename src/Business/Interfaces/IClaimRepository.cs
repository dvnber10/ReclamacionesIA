using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.Entities;
using Common.Enums;

namespace Business.Interfaces
{
    public interface IClaimRepository
    {
        IQueryable<Claim> Query();
        Task<Claim?> GetByExternalEventIdAsync(string eventId, CancellationToken ct = default);
        Task AddAsync(Claim claim, CancellationToken ct = default);
        Task AddOutboxAsync(OutboxMessage mensaje, CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
        Task<Claim?> GetConDetallesAsync(Guid id, CancellationToken ct = default);
        Task AddInferenciaAsync(InferenciaIA inferencia, CancellationToken ct = default);
        Task<Claim?> GetByPublicTokenConDetallesAsync(string token, CancellationToken ct = default);
        // funciones para el panel de analista
        Task<List<Claim>> ListarParaPanelAsync(CancellationToken ct = default);
        Task<Claim?> GetParaPanelAsync(Guid id, CancellationToken ct = default);
        Task<ClaimDocument?> GetDocumentoAsync(Guid id, CancellationToken ct = default);
        Task<bool> ExisteOutboxPendienteAsync(
            Guid claimId, string payloadJson, OutboxChannel canal,
            CancellationToken ct = default);

        Task AddAvisoAsync(
            Claim claim, string mensaje, DateTimeOffset ahora,
            CancellationToken ct = default);
        Task<List<Claim>> ListarEnRiesgoSinAvisoAsync(CancellationToken ct = default);
    }
}