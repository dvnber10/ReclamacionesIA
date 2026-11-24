using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Enums;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebUI.Options;

namespace WebUI.Workers
{
    public class CorreoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly EmailOptions _options;
        private readonly ILogger<CorreoWorker> _logger;

        public CorreoWorker(
            IServiceScopeFactory scopes,
            IOptions<EmailOptions> options,
            ILogger<CorreoWorker> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CorreoWorker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(_options.Usuario) ||
                    string.IsNullOrWhiteSpace(_options.Password) ||
                    string.IsNullOrWhiteSpace(_options.From))
                {
                    _logger.LogWarning(
                        "Email:Usuario/Password/From no configurados. Revisando en 30 s.");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    continue;
                }

                try
                {
                    await ProcesarAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el CorreoWorker.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcesarAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReclamacionesDbContext>();

            var pendientes = await db.Outbox
                .Where(o => o.Canal == OutboxChannel.Web
                         && o.Estado == OutboxStatus.Pendiente)
                .OrderBy(o => o.CreadoEn)
                .Take(10)
                .ToListAsync(ct);

            if (pendientes.Count == 0) return;

            var ahora = DateTimeOffset.UtcNow;

            using var smtp = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.Usuario, _options.Password),
                Timeout = 10000
            };

            foreach (var m in pendientes)
            {
                try
                {
                    using var payload = JsonDocument.Parse(m.PayloadJson);
                    var mensaje = payload.RootElement.GetProperty("mensaje").GetString() ?? "";
                    var token = payload.RootElement.TryGetProperty("token", out var t)
                        ? t.GetString()
                        : null;

                    using var correo = new MailMessage
                    {
                        From = new MailAddress(_options.From, _options.FromName),
                        Subject = string.IsNullOrWhiteSpace(token)
                            ? "Novedades de su reclamación"
                            : $"Reclamación {token} · confirmación y novedades",
                        Body = $"Estimado beneficiario:\n\n{mensaje}\n\n" +
                               (string.IsNullOrWhiteSpace(token)
                                   ? ""
                                   : $"Número de seguimiento: {token}\n") +
                               "\nEste mensaje es automático. No responda a este correo.",
                        IsBodyHtml = false
                    };

                    correo.To.Add(m.Destino);

                    await smtp.SendMailAsync(correo, ct);

                    m.MarcarEnviado(ahora);

                    _logger.LogInformation(
                        "Correo enviado a {Destino} · radicado {Token}", m.Destino, token);
                }
                catch (Exception ex)
                {
                    var error = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                    m.RegistrarFallo(error);

                    _logger.LogWarning(
                        "Fallo envío correo {Id} a {Destino}: {Error}", m.Id, m.Destino, error);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}