using System.Globalization;
using System.Security.Claims;
using Business.Interfaces;
using Common.Dto;
using Common.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebUI.Controllers
{
    [Authorize]
    public sealed class PanelController : Controller
    {
        private readonly IPanelService _panel;
        private readonly IClaimRepository _repo;
        public PanelController(IPanelService panel, IClaimRepository claimRepository) {
            _panel = panel;
            _repo = claimRepository;
            }

        public async Task<IActionResult> Index(
            [Bind(Prefix = "Filtro")] PanelFiltro? filtro,
            CancellationToken ct)
            => View(await _panel.ObtenerResumenAsync(filtro, ct));

        public async Task<IActionResult> Detalle(Guid id, Guid? doc, CancellationToken ct)
        {
            var detalle = await _panel.ObtenerDetalleAsync(id, ct);
            if (detalle is null) return NotFound();

            detalle.DocumentoSeleccionado = doc ?? detalle.Documentos.FirstOrDefault()?.Id;
            return View(detalle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarTipo(
            Guid id, ClaimType tipo, CancellationToken ct)
        {
            await _panel.ConfirmarTipoAsync(
                id, tipo, User.Identity?.Name ?? "analista", ct);
            return RedirectToAction(nameof(Detalle), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarDecision(
            Guid id,
            DecisionAction accion,
            string? notas,
            string? montoIndemnizacion,
            CancellationToken ct)
        {
            decimal? monto = null;

            if (accion == DecisionAction.Pagar)
            {
                var texto = (montoIndemnizacion ?? "")
                    .Trim()
                    .Replace("\u00A0", "")
                    .Replace(" ", "");

                var ultimaComa = texto.LastIndexOf(',');
                var ultimoPunto = texto.LastIndexOf('.');

                texto = ultimaComa > ultimoPunto
                    ? texto.Replace(".", "").Replace(",", ".")
                    : texto.Replace(",", "");

                if (!decimal.TryParse(texto, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var valor) || valor <= 0)
                {
                    TempData["Error"] =
                        "Para registrar un pago debe digitar un valor de indemnización válido.";
                    return RedirectToAction(nameof(Detalle), new { id });
                }

                monto = valor;
            }

            var analistaId = Guid.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

            await _panel.RegistrarDecisionAsync(
                id, accion, notas, monto, analistaId,
                User.Identity?.Name ?? "analista", ct);

            return RedirectToAction(nameof(Detalle), new { id });
        }
        [HttpGet]
        public async Task<IActionResult> Ver(Guid id, CancellationToken ct)
        {
            var doc = await _repo.GetDocumentoAsync(id, ct);
            if (doc is null || !System.IO.File.Exists(doc.Ruta)) return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(doc.Ruta, ct);
            var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
                ? "application/octet-stream"
                : doc.ContentType;

            Response.Headers["X-Content-Type-Options"] = "nosniff";

            return new FileContentResult(bytes, contentType)
            {
                EnableRangeProcessing = true
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarRevisionDocumento(
            Guid id, Guid documentoId, RequiredDocument? tipo,
            DocumentValidation validacion, string? notas, CancellationToken ct)
        {
            await _panel.RegistrarRevisionDocumentoAsync(
                id, documentoId, tipo, validacion, notas,
                User.Identity?.Name ?? "analista", ct);

            return RedirectToAction(nameof(Detalle), new { id, doc = documentoId });
        }
    }
}