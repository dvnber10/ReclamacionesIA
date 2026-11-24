using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Options
{
    public class IaOptions
    {
        public const string Seccion = "IA";

        public string Proveedor { get; set; } = "Ollama";
        public string PromptVersion { get; set; } = "v1";
        public bool UsarVision { get; set; } = false;
        public bool UsarResponseFormat { get; set; } = true;

        public ProveedorOptions Ollama { get; set; } = new();
        public ProveedorOptions Gemini { get; set; } = new();

        public sealed class ProveedorOptions
        {
            public string BaseUrl { get; set; } = string.Empty;
            public string Modelo { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
        }
    }
}