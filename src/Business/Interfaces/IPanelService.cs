using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Dto;
using Common.Enums;

namespace Business.Interfaces
{
    public interface IPanelService
    {
        Task<PanelResumen> ObtenerResumenAsync(PanelFiltro? filtro = null, CancellationToken ct = default);
        Task<PanelDetalle?> ObtenerDetalleAsync(Guid id, CancellationToken ct = default);
        Task ConfirmarTipoAsync(Guid id, ClaimType tipo, string analista, CancellationToken ct = default);
        Task RegistrarDecisionAsync(Guid id, DecisionAction accion, string? notas, decimal? montoIndemnizacion, Guid analistaId, string analistaNombre, CancellationToken ct = default);

        Task RegistrarRevisionDocumentoAsync(Guid claimId, Guid documentoId,
            RequiredDocument? tipo, DocumentValidation validacion,
            string? notas, string analista, CancellationToken ct = default);
    }
}