using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Dto;

namespace Business.Interfaces
{
    public interface IClassificationService
    {
        Task<ClasificacionResultado> ClasificarAsync(ClasificacionRequest request, CancellationToken ct = default);
    }
}