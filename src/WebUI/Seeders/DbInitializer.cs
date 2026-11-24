using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Business.Entities;
using Business.Services;
using Common.Enums;
using Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WebUI.Seeders
{
    public class DbInitializer
    {
        public static async Task SeedAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReclamacionesDbContext>();

            await db.Database.MigrateAsync();
            if (await db.Reclamaciones.AnyAsync()) return;

            var hasher = new PasswordHasher<Analyst>();
            var analista = new Analyst("analista@demo.local", "Ana Analista",
                hasher.HashPassword(null!, "Demo123*"));
            db.Analistas.Add(analista);

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

            for (var i = 1; i <= 30; i++)
            {
                var tipo = (ClaimType)(1 + i % 3);
                var creado = hoy.AddDays(-(i * 2 + 1));

                var claim = new Claim(
                    publicToken: $"DEMO-{i:000}",
                    beneficiaryName: $"Beneficiario {i}",
                    beneficiaryDocument: $"CC{1000000 + i}",
                    beneficiaryEmail: $"beneficiario{i}@demo.local",
                    beneficiaryPhone: $"300000{i:0000}",
                    insuredName: $"Asegurado {i}",
                    insuredDocument: $"CC{2000000 + i}",
                    freeText: Texto(i, tipo),
                    externalEventId: i == 7 ? "EVT-REPETIDO-007" : $"EVT-{i:000}",
                    ahora: Momento(creado));

                db.Reclamaciones.Add(claim);

                // Caso donde el modelo falla: no se inventa un tipo.
                if (i == 19)
                {
                    claim.MarcarRequiereRevision("El modelo no devolvió un JSON válido",
                        Momento(creado).AddMinutes(5));
                    db.Inferencias.Add(InferenciaIA.Fallido(claim.Id, Proveedor(i), Modelo(i), "v1",
                        "{\"texto\": \"" + claim.FreeText + "\"}", "JSON inválido", 1800,
                        Momento(creado).AddMinutes(5)));
                    continue;
                }

                var confianza = i % 6 == 0 ? 0.41 : 0.90 + (i % 5) / 100.0;
                claim.SetClasificacion(tipo, confianza, "{\"tipo\": \"" + tipo + "\"}",
                    Momento(creado).AddMinutes(3));

                db.Inferencias.Add(InferenciaIA.Correcto(claim.Id, Proveedor(i), Modelo(i), "v1",
                    "{\"texto\": \"" + claim.FreeText + "\"}", "{\"tipo\": \"" + tipo + "\"}",
                    confianza, 900 + i * 10, Momento(creado).AddMinutes(3)));

                // Corrección humana -> métrica #2.
                if (i % 6 == 0)
                {
                    var corregido = tipo == ClaimType.MuerteNatural
                        ? ClaimType.MuerteAccidental
                        : ClaimType.MuerteNatural;
                    claim.ConfirmarTipo(corregido, analista.Nombre, Momento(creado).AddHours(1));
                }

                var requeridos = ClaimRules.Requeridos(claim.TipoEfectivo);
                var incompleto = i % 5 == 0 || i % 7 == 0 || i == 3 || i == 13 || i == 17;

                foreach (var req in requeridos)
                {
                    if (incompleto && req == requeridos[^1]) continue; // dejo faltar el último

                    var doc = new ClaimDocument(claim.Id, $"{req}.pdf", Hash($"demo-{i}-{req}"),
                        $"seed/{i}/{req}.pdf", "application/pdf", 1024 + i, Momento(creado));

                    var validation = req switch
                    {
                        RequiredDocument.SarlaftFirmado when i == 3 => DocumentValidation.SinFirma,
                        RequiredDocument.IdAsegurado when i == 13 => DocumentValidation.Ilegible,
                        RequiredDocument.CertificacionBancaria when i == 17 => DocumentValidation.PersonaNoCoincide,
                        _ => DocumentValidation.Valido
                    };

                    doc.SetValidacion(req, validation, "{\"extraido\": true}");
                    if (claim.AgregarDocumento(doc, Momento(creado).AddMinutes(10)) is null) continue;

                    // Misma radicación dos veces: se ignora, no crea otro documento.
                    if (i == 9 && req == requeridos[0])
                    {
                        var repetido = new ClaimDocument(claim.Id, $"{req}-copia.pdf",
                            Hash($"demo-{i}-{req}"), $"seed/{i}/{req}-copia.pdf",
                            "application/pdf", 1024 + i, Momento(creado).AddMinutes(11));
                        claim.AgregarDocumento(repetido, Momento(creado).AddMinutes(11));
                    }
                }

                if (incompleto)
                {
                    claim.MarcarIncompleto(Momento(creado).AddHours(2));
                }
                else
                {
                    var completado = i == 6
                        ? hoy.AddDays(6).AddMonths(-1)   // límite = hoy + 5
                        : i <= 5 ? hoy.AddDays(-35)
                        : i <= 10 ? hoy.AddDays(-28)
                        : hoy.AddDays(-(i % 8));

                    claim.MarcarCompleto(completado, Momento(completado).AddHours(1));

                    // Primer aviso en horas, no en días -> métrica #3.
                    var aviso = Momento(creado).AddHours(2 + i % 12);
                    claim.MarcarPrimerAviso(aviso);

                    var mensaje = ClaimRules.MensajeParaBeneficiario(
                        ClaimRules.Evaluar(claim.TipoEfectivo, claim.Documentos));

                    var outbox = new OutboxMessage(claim.Id, OutboxChannel.Telegram,
                        "@beneficiario_demo",
                        JsonSerializer.Serialize(new { claim.PublicToken, mensaje }), aviso);
                    outbox.MarcarEnviado(aviso.AddMinutes(1));
                    db.Outbox.Add(outbox);
                }
            }

            await db.SaveChangesAsync();
        }

        static DateTimeOffset Momento(DateOnly d)
            => new(d.Year, d.Month, d.Day, 9, 0, 0, TimeSpan.Zero);

        static string Hash(string s)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

        static AiProvider Proveedor(int i) => i % 2 == 0 ? AiProvider.Gemini : AiProvider.Ollama;
        static string Modelo(int i) => i % 2 == 0 ? "gemini-flash-demo" : "llama3.2";

        static string Texto(int i, ClaimType tipo)
        {
            var familiares = new[] { "padre", "madre", "esposo" };
            return tipo switch
            {
                ClaimType.MuerteNatural =>
                    $"Falleció mi {familiares[(i - 1) % familiares.Length]}. " +
                    "Adjunto los soportes que me dieron en la funeraria y el médico.",
                ClaimType.MuerteAccidental =>
                    "Mi familiar falleció en un accidente de tránsito. " +
                    "Tengo el informe de la policía y los certificados.",
                ClaimType.IncapacidadTotalPermanente =>
                    "Me diagnosticaron una incapacidad total y permanente. " +
                    "Aporto el dictamen y la historia clínica.",
                _ => "No estoy seguro de qué pasó, necesito ayuda con mi reclamación."
            };
        }
    }
}