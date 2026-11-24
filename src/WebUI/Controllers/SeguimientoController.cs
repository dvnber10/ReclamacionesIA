using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Business.Interfaces;
using Business.Services;
using Common.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebUI.Models;

namespace WebUI.Controllers
{
    // [Route("[controller]")]
    public class SeguimientoController : Controller
    {
        private readonly ILogger<SeguimientoController> _logger;
        private const long MaximoPorArchivo = 10L * 1024 * 1024;

        private readonly IClaimRepository _repo;
        private readonly IComplementacionService _servicio;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public SeguimientoController(
            ILogger<SeguimientoController> logger,
            IClaimRepository repository,
            IComplementacionService service,
            IWebHostEnvironment env,
            IConfiguration config)
        {
            _logger = logger;
            _repo = repository;
            _servicio = service;
            _env = env;
            _config = config;
        }

        public async Task<IActionResult> Index(string token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token)) return BadRequest();

            var claim = await _repo.GetByPublicTokenConDetallesAsync(token, ct);
            if (claim is null) return NotFound();

            var evaluacion = ClaimRules.Evaluar(claim.TipoEfectivo, claim.Documentos);
            var bot = _config["Telegram:BotUsername"];
            var telegramLink = string.IsNullOrWhiteSpace(bot)
                ? null
                : $"https://t.me/{bot}?start={Uri.EscapeDataString(claim.PublicToken)}";

            return View(new SeguimientoViewModel
            {
                PublicToken = claim.PublicToken,
                Tipo = claim.TipoEfectivo,
                Mensaje = ClaimRules.MensajeParaBeneficiario(evaluacion),
                EsCompleto = evaluacion.EsCompleto,
                CompletadoEl = claim.CompletadoEl,
                FechaLimite = claim.FechaLimite,
                RecibidosNombres = claim.Documentos
                    .Where(d => d.TipoDetectado.HasValue)
                    .Select(d => ClaimRules.Nombre(d.TipoDetectado!.Value))
                    .Distinct().ToList(),
                FaltanNombres = evaluacion.Faltan.Select(ClaimRules.Nombre).ToList(),
                InvalidosNombres = evaluacion.PresentesPeroInvalidos
                    .Select(o => ClaimRules.Nombre(o.Tipo)).ToList(),
                TelegramLink = telegramLink,
                TieneTelegram = !string.IsNullOrWhiteSpace(claim.TelegramChatId)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complementar(SeguimientoViewModel vm, CancellationToken ct)
        {
            var archivos = new List<ArchivoDto>();

            foreach (var archivo in vm.Archivos.Where(a => a.Length > 0))
            {
                if (archivo.Length > MaximoPorArchivo)
                {
                    ModelState.AddModelError("", "Cada archivo debe pesar menos de 10 MB.");
                    return RedirectToAction(nameof(Index), new { token = vm.PublicToken });
                }

                using var memoria = new MemoryStream();
                await archivo.CopyToAsync(memoria, ct);
                archivos.Add(new ArchivoDto
                {
                    Nombre = Path.GetFileName(archivo.FileName),
                    ContentType = archivo.ContentType,
                    Contenido = memoria.ToArray()
                });
            }

            await _servicio.ComplementarAsync(vm.PublicToken, archivos,
                Path.Combine(_env.ContentRootPath, "App_Data", "uploads"), ct);

            // PRG: al refrescar no se reenvía el formulario.
            return RedirectToAction(nameof(Index), new { token = vm.PublicToken });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}