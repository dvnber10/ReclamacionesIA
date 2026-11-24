using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Dto;

namespace Business.Interfaces
{
    public interface IComplementacionService
    {
        Task ComplementarAsync(string token, IReadOnlyList<ArchivoDto> archivos, string rutaUploads, CancellationToken ct = default);
    }
}