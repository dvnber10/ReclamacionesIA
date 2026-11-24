using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Interfaces;
using Business.Services;
using Common.Dto;
using Common.Enums;
using Data.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Data.Adapters
{
    public class HttpClassificationService : IClassificationService
    {
        private const string SystemPrompt = """
        Eres un asistente de radicación de una aseguradora de vida.
        TU ÚNICA TAREA: clasificar el tipo de reclamación y reconocer los soportes mencionados.
        NUNCA decidas si se paga, se objeta o si el expediente está completo: eso lo decide una persona.

        Tipos permitidos (elige exactamente uno):
        Desconocido, MuerteNatural, MuerteAccidental, IncapacidadTotalPermanente.
        - MuerteNatural: fallecimiento sin mención de accidente.
        - MuerteAccidental: accidente, policía, fiscalía, vía pública.
        - IncapacidadTotalPermanente: incapacidad, dictamen, pérdida de capacidad laboral.
        Si el texto no alcanza para decidir, elige Desconocido y usa confianza menor a 0.5.

        Soportes posibles:
        FormularioReclamacion, RegistroCivilDefuncion, CertificadoMedicoDefuncion,
        IdAsegurado, IdBeneficiario, SarlaftFirmado, CertificacionBancaria,
        InformeAutoridad, DictamenPerdidaCapacidad, HistoriaClinicaResumida.

        Responde SOLO con un objeto JSON válido, sin markdown ni explicación.
        El campo "confianza" debe ser un número JSON entre 0 y 1, con punto decimal.
        Nunca lo escribas como texto, porcentaje ni con coma. No copies el valor del ejemplo.

        Forma exacta:
        {"tipo":"<TIPO>","confianza":0.85,"documentos":[{"nombre":"<NOMBRE>","tipo":"<SOPORTE>","legible":true,"firmado":true,"personaCoincide":true,"campos":""}]}

        No inventes documentos que no estén en la lista de archivos recibidos.
        Si no puedes evaluar algún dato, usa true y deja que un humano lo confirme.

        Calibra "confianza" según el texto:
        - Texto concreto y sin ambigüedad: 0.80 a 0.95.
        - Texto útil pero con duda: 0.55 a 0.79.
        - Texto insuficiente: elige Desconocido y usa 0.10 a 0.49.

        Ejemplos de salida (JSON, no prosa):
        {"tipo":"MuerteAccidental","confianza":0.63,"documentos":[]}
        {"tipo":"MuerteNatural","confianza":0.87,"documentos":[]}
        {"tipo":"MuerteAccidental","confianza":0.50,"documentos":[]}
        {"tipo":"IncapacidadTotalPermanente","confianza":0.70,"documentos":[]}
        """;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _factoria;
        private readonly IaOptions _options;
        private readonly ILogger<HttpClassificationService> _logger;

        public HttpClassificationService(
            IHttpClientFactory factoria,
            IOptions<IaOptions> options,
            ILogger<HttpClassificationService> logger)
        {
            _factoria = factoria;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ClasificacionResultado> ClasificarAsync(
            ClasificacionRequest request, CancellationToken ct = default)
        {
            if (string.Equals(_options.Proveedor, "SinIA", StringComparison.OrdinalIgnoreCase))
                return ClasificarConReglas(request);
            var esGemini = string.Equals(_options.Proveedor, "Gemini",
                StringComparison.OrdinalIgnoreCase);
            var cfg = esGemini ? _options.Gemini : _options.Ollama;
            var proveedor = esGemini ? AiProvider.Gemini : AiProvider.Ollama;

            var texto = "Relato del beneficiario:\n" + request.TextoLibre
                      + "\n\nArchivos recibidos:\n"
                      + string.Join("\n", request.NombresArchivos.Select(n => "- " + n));

            object contenidoUsuario = texto;

            if (_options.UsarVision && request.Imagenes.Count > 0)
            {
                var partes = new List<object> { new { type = "text", text = texto } };
                foreach (var imagen in request.Imagenes.Take(4))
                    partes.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = $"data:{imagen.ContentType};base64,{imagen.Base64}" }
                    });
                contenidoUsuario = partes;
            }

            var cuerpo = new Dictionary<string, object?>
            {
                ["model"] = cfg.Modelo,
                ["messages"] = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = contenidoUsuario }
                }
            };

            if (_options.UsarResponseFormat)
                cuerpo["response_format"] = new { type = "json_object" };

            var json = JsonSerializer.Serialize(cuerpo, JsonOptions);
            var url = cfg.BaseUrl.TrimEnd('/') + "/chat/completions";

            using var http = _factoria.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(60);

            using var mensaje = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
                mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            var cronometro = Stopwatch.StartNew();

            try
            {
                using var respuesta = await http.SendAsync(mensaje, ct);
                var crudo = await respuesta.Content.ReadAsStringAsync(ct);
                cronometro.Stop();

                if (!respuesta.IsSuccessStatusCode)
                {
                    var error = $"HTTP {(int)respuesta.StatusCode}: {Truncar(crudo, 300)}";
                    _logger.LogWarning("IA {Proveedor} falló en {Ms}ms: {Error}",
                        proveedor, cronometro.ElapsedMilliseconds, error);
                    return FalloInfraestructura(error, crudo, cronometro.ElapsedMilliseconds, request, TipoFalloIA.Proveedor);
                }

                using var doc = JsonDocument.Parse(crudo);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                var modelo = JsonSerializer.Deserialize<ModeloRespuesta>(
                    QuitarFences(content), JsonOptions);

                if (modelo is null)
                    return SalidaInvalida("El modelo devolvió un JSON vacío", content, cronometro.ElapsedMilliseconds);

                var tipo = ClaimType.Desconocido;
                if (!string.IsNullOrWhiteSpace(modelo.Tipo)
                    && Enum.TryParse<ClaimType>(modelo.Tipo, true, out var t)
                    && t is ClaimType.MuerteNatural
                       or ClaimType.MuerteAccidental
                       or ClaimType.IncapacidadTotalPermanente)
                {
                    tipo = t;
                }

                var confianza = Math.Clamp(modelo.Confianza ?? 0.5, 0, 1);
                if (tipo == ClaimType.Desconocido)
                    confianza = Math.Min(confianza, 0.49);

                // validar documentos
                var documentos = (modelo.Documentos ?? Array.Empty<ModeloRespuesta.DocRespuesta>())
                    .Select(d => new DocumentoDetectadoDto
                    {
                        NombreArchivo = d.Nombre ?? "",
                        Tipo = Enum.TryParse<RequiredDocument>(d.Tipo ?? "", true, out var td)
                            ? td : ClaimRules.DetectarPorNombre(d.Nombre ?? ""),
                        Validacion = _options.UsarVision && request.Imagenes.Count > 0
                            ? ObtenerValidacion(d)
                            : DocumentValidation.Valido,
                            Campos = d.Campos
                    })
                    .ToList();
                // La IA puede omitir archivos; ninguno se queda sin tipo.
                foreach (var nombre in request.NombresArchivos)
                {
                    if (documentos.Any(d => string.Equals(
                            d.NombreArchivo, nombre, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    documentos.Add(new DocumentoDetectadoDto
                    {
                        NombreArchivo = nombre,
                        Tipo = ClaimRules.DetectarPorNombre(nombre),
                        Validacion = DocumentValidation.Valido,
                        Campos = null
                    });
                }

                _logger.LogInformation("IA {Proveedor} {Modelo} OK en {Ms}ms tipo={Tipo} conf={Conf}",
                    proveedor, cfg.Modelo, cronometro.ElapsedMilliseconds, tipo, confianza);

                return new ClasificacionResultado
                {
                    Exito = true,
                    Tipo = tipo,
                    Proveedor = proveedor,
                    Modelo = cfg.Modelo,
                    Confianza = confianza,
                    Raw = content,
                    Documentos = documentos,
                    LatenciaMs = (int)cronometro.ElapsedMilliseconds,
                };
            }
            catch (HttpRequestException ex)
            {
                cronometro.Stop();
                var error = $"Sin conexión con {proveedor}: {ex.Message}";
                _logger.LogWarning("IA {Proveedor} no disponible en {Ms}ms: {Error}",
                    proveedor, cronometro.ElapsedMilliseconds, error);
                return FalloInfraestructura(error, "", cronometro.ElapsedMilliseconds, request, TipoFalloIA.Conexion);
            }
            catch (JsonException ex)
            {
                cronometro.Stop();
                var error = "El modelo no devolvió un JSON válido: " + ex.Message;
                _logger.LogWarning("IA {Proveedor} JSON inválido en {Ms}ms",
                    proveedor, cronometro.ElapsedMilliseconds);
                return SalidaInvalida(error, "", cronometro.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                cronometro.Stop();
                return FalloInfraestructura("Timeout esperando al modelo", "", cronometro.ElapsedMilliseconds, request, TipoFalloIA.Conexion);
            }
        }

        private ClasificacionResultado ClasificarConReglas(ClasificacionRequest request)
        {
            var texto = (request.TextoLibre + " " + string.Join(" ", request.NombresArchivos))
            .ToLowerInvariant();

            ClaimType tipo;

            if (Contiene(texto, "accidente", "polic", "fiscal", "choque", "atropello", "vía pública"))
                tipo = ClaimType.MuerteAccidental;
            else if (Contiene(texto, "incapacidad", "dictamen", "pérdida de capacidad", "laboral"))
                tipo = ClaimType.IncapacidadTotalPermanente;
            else if (Contiene(texto, "fallec", "murió", "defunción", "funeraria"))
                tipo = ClaimType.MuerteNatural;
            else
                tipo = ClaimType.Desconocido;

            var documentos = request.NombresArchivos
                .Select(n => new DocumentoDetectadoDto
                {
                    NombreArchivo = n,
                    Tipo = ClaimRules.DetectarPorNombre(n),
                    Validacion = DocumentValidation.Valido
                })
                .ToList();

            return new ClasificacionResultado
            {
                Exito = true,
                Tipo = tipo,
                Proveedor = AiProvider.SinIA,
                Modelo = "reglas-locales",
                Confianza = tipo == ClaimType.Desconocido ? 0.30 : 0.75,
                Raw = "{\"modo\":\"reglas-locales\"}",
                Documentos = documentos
            };
        }

        private static bool Contiene(string texto, params string[] claves) => claves.Any(texto.Contains);

        private static RequiredDocument? DetectarTipo(string nombre)
        {
            var n = nombre.ToLowerInvariant();

            if (n.Contains("sarlaft")) return RequiredDocument.SarlaftFirmado;
            if (n.Contains("bancar")) return RequiredDocument.CertificacionBancaria;
            if (n.Contains("registro") && n.Contains("civil")) return RequiredDocument.RegistroCivilDefuncion;
            if (n.Contains("certificado") && n.Contains("medic")) return RequiredDocument.CertificadoMedicoDefuncion;
            if (n.Contains("dictamen")) return RequiredDocument.DictamenPerdidaCapacidad;
            if (n.Contains("historia")) return RequiredDocument.HistoriaClinicaResumida;
            if (n.Contains("polic") || n.Contains("fiscal")) return RequiredDocument.InformeAutoridad;
            if (n.Contains("reclam") || n.Contains("formulario")) return RequiredDocument.FormularioReclamacion;
            if (n.Contains("cedula") || n.Contains("documento")) return RequiredDocument.IdAsegurado;

            // IdAsegurado vs IdBeneficiario es ambiguo por nombre de archivo:
            // se deja pendiente de corrección humana, no se inventa.
            return null;
        }

        private static DocumentValidation ObtenerValidacion(ModeloRespuesta.DocRespuesta d)
        {
            if (d.Legible == false) return DocumentValidation.Ilegible;

            if (d.Firmado == false && string.Equals(d.Tipo,
                    nameof(RequiredDocument.SarlaftFirmado), StringComparison.OrdinalIgnoreCase))
                return DocumentValidation.SinFirma;

            if (d.PersonaCoincide == false) return DocumentValidation.PersonaNoCoincide;

            return DocumentValidation.Valido;
        }

        private ClasificacionResultado FalloInfraestructura(string error, string raw, long ms, ClasificacionRequest request, TipoFalloIA tipoFallo)
        {
            var reglas = ClasificarConReglas(request);
            var esGemini = string.Equals(_options.Proveedor, "Gemini",
                StringComparison.OrdinalIgnoreCase);
            var cfg = esGemini ? _options.Gemini : _options.Ollama;

            _logger.LogWarning(
                "IA {TipoFallo} ({Error}). Degradando a reglas locales. Tipo={Tipo}",
                tipoFallo, error, reglas.Tipo);

            return new ClasificacionResultado
            {
                Exito = false,
                Error = $"{error} ({ms}ms)",
                Raw = raw,
                FueFallback = true,
                TipoFallo = tipoFallo,
                Proveedor = esGemini ? AiProvider.Gemini : AiProvider.Ollama,
                Modelo = cfg.Modelo,
                LatenciaMs = (int)ms,
                Tipo = reglas.Tipo,
                Confianza = Math.Min(reglas.Confianza, 0.49),
                Documentos = reglas.Documentos
            };
        }

        private ClasificacionResultado SalidaInvalida(string error, string raw, long ms)
        {
            var esGemini = string.Equals(_options.Proveedor, "Gemini",
                StringComparison.OrdinalIgnoreCase);
            var cfg = esGemini ? _options.Gemini : _options.Ollama;

            return new ClasificacionResultado
            {
                Exito = false,
                Error = $"{error} ({ms}ms)",
                Raw = raw,
                FueFallback = false,
                TipoFallo = TipoFalloIA.SalidaInvalida,
                Proveedor = esGemini ? AiProvider.Gemini : AiProvider.Ollama,
                Modelo = cfg.Modelo,
                LatenciaMs = (int)ms,
                Tipo = ClaimType.Desconocido,
                Confianza = 0,
                Documentos = Array.Empty<DocumentoDetectadoDto>()
            };
        }
        private static string QuitarFences(string s)
        {
            s = s.Trim();
            if (!s.StartsWith("```")) return s;

            var inicio = s.IndexOf('\n');
            var fin = s.LastIndexOf("```");
            return inicio >= 0 && fin > inicio ? s[(inicio + 1)..fin].Trim() : s;
        }

        private static string Truncar(string s, int n)
            => s.Length <= n ? s : s[..n] + "...";

        private sealed class ModeloRespuesta
        {
            public string? Tipo { get; set; }
            public double? Confianza { get; set; }
            public DocRespuesta[]? Documentos { get; set; }

            public sealed class DocRespuesta
            {
                public string? Nombre { get; set; }
                public string? Tipo { get; set; }
                public bool? Legible { get; set; }
                public bool? Firmado { get; set; }
                public bool? PersonaCoincide { get; set; }
                public string? Campos { get; set; }
            }
        }
    }
}