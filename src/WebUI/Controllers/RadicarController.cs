using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Business.Interfaces;
using Common.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebUI.Models;

namespace WebUI.Controllers
{
    [Route("[controller]")]
    public class RadicarController : Controller
    {
        private readonly ILogger<RadicarController> _logger;
        private const long MaximoPorArchivo = 10L * 1024 * 1024;

        private readonly IRadicacionService _servicio;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public RadicarController(ILogger<RadicarController> logger, IRadicacionService radicacionService, IWebHostEnvironment env, IConfiguration config)
        {
            _logger = logger;
            _servicio = radicacionService;
            _env = env;
            _config = config;
        }

        [HttpGet]
        public IActionResult Index() => View(new RadicarViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(RadicarViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            var archivos = new List<ArchivoDto>();

            foreach (var archivo in vm.Archivos.Where(a => a.Length > 0))
            {
                if (archivo.Length > MaximoPorArchivo)
                {
                    ModelState.AddModelError("", "Cada archivo debe pesar menos de 10 MB.");
                    return View(vm);
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

            var resultado = await _servicio.RadicarAsync(new RadicarCommand
            {
                EventId = vm.EventId,
                BeneficiaryName = vm.BeneficiaryName,
                BeneficiaryDocument = vm.BeneficiaryDocument,
                BeneficiaryEmail = vm.BeneficiaryEmail,
                BeneficiaryPhone = vm.BeneficiaryPhone,
                InsuredName = vm.InsuredName,
                InsuredDocument = vm.InsuredDocument,
                FreeText = vm.FreeText,
                Archivos = archivos
            }, Path.Combine(_env.ContentRootPath, "App_Data", "uploads"), ct);

            var bot = _config["Telegram:BotUsername"];
            var telegramLink = string.IsNullOrWhiteSpace(bot)
                ? null
                : $"https://t.me/{bot}?start={Uri.EscapeDataString(resultado.PublicToken)}";

            return View("Confirmacion", resultado with { TelegramLink = telegramLink });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}