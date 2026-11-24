using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Dto
{
    public class ArchivoDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
        public long TamanoBytes => Contenido.LongLength;
    }
}