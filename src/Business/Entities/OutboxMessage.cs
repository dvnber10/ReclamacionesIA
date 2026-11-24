using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace Business.Entities
{
    public class OutboxMessage : EntityBase
    {
        private OutboxMessage() { } // EF Core

    public OutboxMessage(Guid claimId, OutboxChannel canal, string destino,
        string payloadJson, DateTimeOffset? creadoEn = null)
    {
        ClaimId = claimId;
        Canal = canal;
        Destino = destino;
        PayloadJson = payloadJson;
        CreadoEn = creadoEn ?? DateTimeOffset.UtcNow;
        Estado = OutboxStatus.Pendiente;
        Intentos = 0;
    }

    public Guid ClaimId { get; private set; }
    public OutboxChannel Canal { get; private set; }
    public string Destino { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public OutboxStatus Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? EnviadoEn { get; private set; }
    public int Intentos { get; private set; }
    public string? UltimoError { get; private set; }
    public void SetDestino(string destino) => Destino = destino;

    //reglas de negocio
    public void MarcarEnviado(DateTimeOffset ahora)
    {
        Estado = OutboxStatus.Enviado;
        EnviadoEn = ahora;
        UltimoError = null;
    }

    public void RegistrarFallo(string error)
    {
        Intentos++;
        UltimoError = error;
        Estado = OutboxStatus.Fallido;
    }
    }
}