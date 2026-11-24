using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IMotorService
    {
        Task<string> ProcesarAsync(Guid claimId, CancellationToken ct = default);
    }
}