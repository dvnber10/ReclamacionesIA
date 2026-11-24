using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Entities;
using Business.Interfaces;
using Business.Services;
using Common.Enums;
using Data;

namespace WebUI.Workers
{
    public class VencimientoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<VencimientoWorker> _logger;

        public VencimientoWorker(
            IServiceScopeFactory scopes,
            ILogger<VencimientoWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("VencimientoWorker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcesarAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el VencimientoWorker.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcesarAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IClaimRepository>();
            var db = scope.ServiceProvider.GetRequiredService<ReclamacionesDbContext>();

            var candidatas = await repo.ListarEnRiesgoSinAvisoAsync(ct);
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var ahora = DateTimeOffset.UtcNow;
            var generados = 0;

            foreach (var c in candidatas)
            {
                if (!c.FechaLimite.HasValue) continue;
                if (!LegalClock.EnRiesgo(c.FechaLimite.Value, hoy)) continue;
                if (!c.MarcarAvisoVencimiento(ahora)) continue;

                var mensaje = ClaimRules.MensajeVencimiento(c.FechaLimite.Value);
                await repo.AddAvisoAsync(c, mensaje, ahora, ct);

                generados++;
            }

            if (generados == 0) return;

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Avisos de vencimiento generados: {N}", generados);
        }
    }
}