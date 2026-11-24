using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Business.Entities;
using Common.Enums;

namespace Business.Services
{
    public sealed record DocumentoObservacion(RequiredDocument Tipo, DocumentValidation Motivo);

    public sealed record ResultadoExpediente(
        bool EsCompleto,
        IReadOnlyList<RequiredDocument> Faltan,
        IReadOnlyList<DocumentoObservacion> PresentesPeroInvalidos);
    public static class ClaimRules
    {
        private static readonly Dictionary<ClaimType, RequiredDocument[]> Tabla = new()
        {
            [ClaimType.MuerteNatural] = new[]
        {
            RequiredDocument.FormularioReclamacion,
            RequiredDocument.RegistroCivilDefuncion,
            RequiredDocument.CertificadoMedicoDefuncion,
            RequiredDocument.IdAsegurado,
            RequiredDocument.IdBeneficiario,
            RequiredDocument.SarlaftFirmado,
            RequiredDocument.CertificacionBancaria
        },
            [ClaimType.MuerteAccidental] = new[]
        {
            RequiredDocument.FormularioReclamacion,
            RequiredDocument.RegistroCivilDefuncion,
            RequiredDocument.CertificadoMedicoDefuncion,
            RequiredDocument.IdAsegurado,
            RequiredDocument.IdBeneficiario,
            RequiredDocument.SarlaftFirmado,
            RequiredDocument.CertificacionBancaria,
            RequiredDocument.InformeAutoridad
        },
            [ClaimType.IncapacidadTotalPermanente] = new[]
        {
            RequiredDocument.FormularioReclamacion,
            RequiredDocument.DictamenPerdidaCapacidad,
            RequiredDocument.HistoriaClinicaResumida,
            RequiredDocument.IdAsegurado,
            RequiredDocument.SarlaftFirmado,
            RequiredDocument.CertificacionBancaria
        }
        };
        // Datos requeridos para verificar la reclamacion
        public static IReadOnlyList<RequiredDocument> Requeridos(ClaimType tipo)
            => Tabla.TryGetValue(tipo, out var lista) ? lista : Array.Empty<RequiredDocument>();

        // Regla para evaluar la reclamacion si esta bien o mal
        public static ResultadoExpediente Evaluar(ClaimType tipo, IEnumerable<ClaimDocument> documentos)
        {
            var requeridos = Requeridos(tipo);

            
            if (requeridos.Count == 0)
                return new ResultadoExpediente(false, Array.Empty<RequiredDocument>(),
                    Array.Empty<DocumentoObservacion>());

            var docs = documentos.ToList();
            var faltan = new List<RequiredDocument>();
            var observaciones = new List<DocumentoObservacion>();

            foreach (var req in requeridos)
            {
                var delTipo = docs.Where(d => d.TipoDetectado == req).ToList();

                if (delTipo.Count == 0)
                {
                    faltan.Add(req);
                    continue;
                }

                if (delTipo.All(d => d.Validacion != DocumentValidation.Valido))
                {
                    var motivo = delTipo.Select(d => d.Validacion).OrderBy(Prioridad).First();
                    observaciones.Add(new DocumentoObservacion(req, motivo));
                }
            }

            return new ResultadoExpediente(faltan.Count == 0 && observaciones.Count == 0,
                faltan, observaciones);
        }
        // regla para evaluar la Prioridad 
        private static int Prioridad(DocumentValidation v) => v switch
        {
            DocumentValidation.SinFirma => 0,
            DocumentValidation.PersonaNoCoincide => 1,
            DocumentValidation.Ilegible => 2,
            DocumentValidation.TipoNoCorresponde => 3,
            DocumentValidation.Pendiente => 4,
            _ => 5
        };

        public static string MensajeParaBeneficiario(ResultadoExpediente r)
        {
            if (r.EsCompleto)
                return "Su expediente está completo. Un analista revisará su caso con atención.";

            if (r.Faltan.Count == 0 && r.PresentesPeroInvalidos.Count == 0)
                return "Estamos determinando el tipo de reclamación. Un analista revisará su caso.";

            var partes = new List<string>();

            if (r.Faltan.Count > 0)
                partes.Add("Faltan: " + string.Join(", ", r.Faltan.Select(Nombre)) + ".");

            foreach (var o in r.PresentesPeroInvalidos)
            {
                var accion = o.Motivo == DocumentValidation.Pendiente
                    ? "Lo revisaremos y le avisaremos."
                    : "Por favor envíe una copia válida.";
                partes.Add($"Recibimos {Nombre(o.Tipo)} pero {Motivo(o.Motivo)}. {accion}");
            }

            return string.Join(" ", partes);
        }

        public static string Nombre(RequiredDocument d) => d switch
        {
            RequiredDocument.FormularioReclamacion => "el formato de reclamación diligenciado",
            RequiredDocument.RegistroCivilDefuncion => "el registro civil de defunción",
            RequiredDocument.CertificadoMedicoDefuncion => "el certificado médico de defunción",
            RequiredDocument.IdAsegurado => "el documento del asegurado",
            RequiredDocument.IdBeneficiario => "el documento del beneficiario",
            RequiredDocument.SarlaftFirmado => "el SARLAFT firmado",
            RequiredDocument.CertificacionBancaria => "la certificación bancaria",
            RequiredDocument.InformeAutoridad => "el informe de la autoridad competente",
            RequiredDocument.DictamenPerdidaCapacidad => "el dictamen de pérdida de capacidad laboral",
            RequiredDocument.HistoriaClinicaResumida => "la historia clínica resumida",
            _ => d.ToString()
        };

        private static string Motivo(DocumentValidation v) => v switch
        {
            DocumentValidation.SinFirma => "llega sin firma",
            DocumentValidation.Ilegible => "no es legible",
            DocumentValidation.PersonaNoCoincide => "no corresponde a la persona esperada",
            DocumentValidation.TipoNoCorresponde => "no corresponde a este soporte",
            DocumentValidation.Pendiente => "está recibido pero aún no fue validado",
            _ => "no puede ser aceptado"
        };
        // deteccion de documentos proporcionados por el usuario
        public static RequiredDocument? DetectarPorNombre(string nombre)
        {
            var n = nombre.ToLowerInvariant();

            if (n.Contains("sarlaft")) return RequiredDocument.SarlaftFirmado;
            if (n.Contains("bancar") || n.Contains("cuenta") || n.Contains("extracto")) return RequiredDocument.CertificacionBancaria;
            if (n.Contains("registro") && n.Contains("civil")) return RequiredDocument.RegistroCivilDefuncion;
            if (n.Contains("certificado") && n.Contains("medic")) return RequiredDocument.CertificadoMedicoDefuncion;
            if (n.Contains("dictamen")) return RequiredDocument.DictamenPerdidaCapacidad;
            if (n.Contains("historia")) return RequiredDocument.HistoriaClinicaResumida;
            if (n.Contains("polic") || n.Contains("fiscal")) return RequiredDocument.InformeAutoridad;
            if (n.Contains("reclam") || n.Contains("formulario")) return RequiredDocument.FormularioReclamacion;
            if (n.Contains("beneficiario")) return RequiredDocument.IdBeneficiario;
            if (n.Contains("asegurado")) return RequiredDocument.IdAsegurado;
            if (n.Contains("cedula") || n.Contains("documento")) return RequiredDocument.IdAsegurado;

            return null; // no se inventa: queda pendiente de corrección humana
        }

         public static string MensajePedirDocumentos(ResultadoExpediente r, string? notas)
        {
            var partes = new List<string>();

            if (r.Faltan.Count > 0)
                partes.Add("Faltan: " + string.Join(", ", r.Faltan.Select(Nombre)) + ".");

            foreach (var o in r.PresentesPeroInvalidos)
                partes.Add($"Recibimos {Nombre(o.Tipo)} pero {Motivo(o.Motivo)}. Por favor envíe una copia válida.");

            if (partes.Count == 0)
                partes.Add("El analista solicita información adicional sobre su reclamación.");

            if (!string.IsNullOrWhiteSpace(notas))
                partes.Add("Nota del analista: " + notas.Trim());

            return "Su reclamación necesita información adicional. " + string.Join(" ", partes);
        }

        public static string MensajeVencimiento(DateOnly limite)
            => $"Su expediente tiene plazo de respuesta hasta el {limite:dd/MM/yyyy}. " +
               "Si aún falta documentación, envíela desde el enlace de seguimiento.";
    }
}