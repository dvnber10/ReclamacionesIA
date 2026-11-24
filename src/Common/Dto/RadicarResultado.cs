using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Dto
{
    public sealed record RadicarResultado(bool EsNuevo, string PublicToken, string Mensaje, string? TelegramLink = null, string? Correo = null);
}