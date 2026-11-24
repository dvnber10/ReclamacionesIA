using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Dto;

namespace Business.Interfaces
{
    public interface IRadicacionService
    {
        Task<RadicarResultado> RadicarAsync(RadicarCommand cmd, string rutaUploads,CancellationToken ct = default);
    }
}