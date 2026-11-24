using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Enums;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebUI.Options;

namespace WebUI.Workers
{
    public class TelegramWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IHttpClientFactory _httpFactory;
        private readonly TelegramOptions _options;
        private readonly ILogger<TelegramWorker> _logger;
        private readonly HttpClient _http;

        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _proximosIntentos = new();
        private long _offset;

        public TelegramWorker(
            IServiceScopeFactory scopes,
            IHttpClientFactory httpFactory,
            IOptions<TelegramOptions> options,
            ILogger<TelegramWorker> logger)
        {
            _scopes = scopes;
            _httpFactory = httpFactory;
            _options = options.Value;
            _logger = logger;
            _http = httpFactory.CreateClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TelegramWorker iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(_options.BotToken))
                {
                    _logger.LogWarning("Telegram:BotToken no configurado. Revisando en 30 s.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                try
                {
                    await ProcesarVinculacionesAsync(stoppingToken);
                    await ProcesarOutboxAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el ciclo del TelegramWorker.");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        private async Task ProcesarVinculacionesAsync(CancellationToken ct)
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/getUpdates?offset={_offset}&timeout=5";
            var json = await _http.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.GetProperty("ok").GetBoolean())
            {
                _logger.LogWarning("getUpdates devolvió ok=false: {Json}", json);
                return;
            }

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReclamacionesDbContext>();
            var ahora = DateTimeOffset.UtcNow;

            foreach (var upd in doc.RootElement.GetProperty("result").EnumerateArray())
            {
                var updateId = upd.GetProperty("update_id").GetInt64();

                try
                {
                    if (!upd.TryGetProperty("message", out var msg)) continue;
                    if (!msg.TryGetProperty("text", out var textEl)) continue;

                    var texto = textEl.GetString() ?? "";
                    var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64().ToString();

                    var partes = texto.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length == 0 ||
                        !partes[0].StartsWith("/start", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var token = partes.Length > 1 ? partes[1].Trim() : null;

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        await EnviarAsync(chatId,
                            "Abra la página de seguimiento de su radicado y toque «Activar Telegram».", ct);
                        continue;
                    }

                    var claim = await db.Reclamaciones
                        .FirstOrDefaultAsync(c => c.PublicToken == token, ct);

                    if (claim is null)
                    {
                        await EnviarAsync(chatId,
                            "No encontré ese radicado. Verifique el enlace de seguimiento.", ct);
                        continue;
                    }

                    if (!claim.VincularTelegram(chatId, ahora)) continue;

                    var pendientes = await db.Outbox
                        .Where(o => o.ClaimId == claim.Id
                            && o.Canal == OutboxChannel.Telegram
                            && o.Estado == OutboxStatus.Pendiente)
                        .ToListAsync(ct);

                    foreach (var m in pendientes) m.SetDestino(chatId);

                    await db.SaveChangesAsync(ct);

                    await EnviarAsync(chatId,
                        $"Listo. Su radicado {claim.PublicToken} recibirá avisos aquí.", ct);

                    _logger.LogInformation(
                        "Telegram vinculado al radicado {Token}.", claim.PublicToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo procesar el update {UpdateId}.", updateId);
                }
                finally
                {
                    _offset = updateId + 1;
                }
            }
        }

        private async Task ProcesarOutboxAsync(CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReclamacionesDbContext>();
            var ahora = DateTimeOffset.UtcNow;

            var pendientes = await db.Outbox
                .Where(o => o.Canal == OutboxChannel.Telegram
                    && o.Estado == OutboxStatus.Pendiente)
                .OrderBy(o => o.CreadoEn)
                .Take(20)
                .ToListAsync(ct);

            foreach (var m in pendientes)
            {
                if (_proximosIntentos.TryGetValue(m.Id, out var next) && next > ahora)
                    continue;

                var claim = await db.Reclamaciones.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == m.ClaimId, ct);

                if (string.IsNullOrWhiteSpace(claim?.TelegramChatId))
                    continue; // aún no vinculó; se reintenta en el próximo ciclo

                using var payload = JsonDocument.Parse(m.PayloadJson);
                var mensaje = payload.RootElement.GetProperty("mensaje").GetString() ?? "";

                var resultado = await EnviarAsync(claim.TelegramChatId, mensaje, ct);

                if (resultado.Ok)
                {
                    m.MarcarEnviado(ahora);
                    _proximosIntentos.TryRemove(m.Id, out _);
                }
                else
                {
                    m.RegistrarFallo(resultado.Error);
                    var segundos = 4 << Math.Min(m.Intentos, 6);
                    _proximosIntentos[m.Id] = ahora.Add(TimeSpan.FromSeconds(segundos));
                    _logger.LogWarning("Fallo envío outbox {Id}: {Error}", m.Id, resultado.Error);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        private async Task<(bool Ok, string Error)> EnviarAsync(
            string chatId, string texto, CancellationToken ct)
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            var body = new { chat_id = chatId, text = texto };

            var resp = await _http.PostAsJsonAsync(url, body, ct);
            if (resp.IsSuccessStatusCode) return (true, "");

            var error = await resp.Content.ReadAsStringAsync(ct);
            return (false, error.Length > 300 ? error[..300] : error);
        }

        public override void Dispose()
        {
            _http.Dispose();
            base.Dispose();
        }
    }
}