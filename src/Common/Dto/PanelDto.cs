using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Enums;

namespace Common.Dto
{
    public sealed class PanelFila
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = "";
        public string Beneficiario { get; set; } = "";
        public ClaimType Tipo { get; set; }
        public ClaimStatus Estado { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
        public DateOnly? FechaLimite { get; set; }
        public bool EnRiesgo { get; set; }
        public bool Vencido { get; set; }
        public bool EsFinal { get; set; }
        public DateTimeOffset? FinalizadoEn { get; set; }
        public int? DiaDelPlazo { get; set; }
        public int? DiasRestantes { get; set; }
        public bool RecienLlegado { get; set; }
        public bool Critico10Dias { get; set; }
        public int Documentos { get; set; }
    }

    public sealed class PanelDocumento
    {
        public Guid Id { get; set; }
        public string NombreArchivo { get; set; } = "";
        public RequiredDocument? TipoDetectado { get; set; }
        public DocumentValidation Validacion { get; set; }
        public DateTimeOffset SubidoEn { get; set; }
        public string? RevisadoPor { get; set; }
        public DateTimeOffset? RevisadoEn { get; set; }
        public string? RevisionNotas { get; set; }
    }

    public sealed class PanelEvento
    {
        public string Tipo { get; set; } = "";
        public string Actor { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public DateTimeOffset OcurridoEn { get; set; }
    }

    public sealed class PanelDecision
    {
        public DecisionAction Accion { get; set; }
        public string AnalistaNombre { get; set; } = "";
        public string? Notas { get; set; }
        public decimal? MontoIndemnizacion { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
    }

    public sealed class PanelDetalle
    {
        public Guid Id { get; set; }
        public Guid? DocumentoSeleccionado { get; set; }
        public string Token { get; set; } = "";
        public string Beneficiario { get; set; } = "";
        public string Asegurado { get; set; } = "";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string Relato { get; set; } = "";
        public bool EsFinal { get; set; }

        public ClaimType TipoInferido { get; set; }
        public double? Confianza { get; set; }
        public ClaimType? TipoConfirmado { get; set; }
        public ClaimType TipoEfectivo { get; set; }

        public ClaimStatus Estado { get; set; }
        public DateOnly? CompletadoEl { get; set; }
        public DateOnly? FechaLimite { get; set; }
        public DateTimeOffset CreadoEn { get; set; }
        public DateTimeOffset? PrimerAvisoEn { get; set; }
        public DateTimeOffset? FinalizadoEn { get; set; }
        public TimeSpan? TiempoRespuesta { get; set; }
        public int? DiasRestantes { get; set; }
        public bool PlazoIniciado { get; set; }
        public bool EnRiesgo { get; set; }
        public bool Vencido { get; set; }
        public bool DentroDelPlazo { get; set; }
        public decimal? MontoIndemnizacion { get; set; }
        public bool EsCompleto { get; set; }

        public List<string> Faltan { get; set; } = new();
        public List<string> Invalidos { get; set; } = new();
        public List<PanelDocumento> Documentos { get; set; } = new();
        public List<PanelEvento> Eventos { get; set; } = new();
        public List<PanelDecision> Decisiones { get; set; } = new();
    }

    public sealed class PanelResumen
    {
        public int Abiertos { get; set; }
        public int RadicadosHoy { get; set; }
        public int EnRiesgo { get; set; }
        public int Vencidos { get; set; }
        public int CorreccionesHumanas { get; set; }
        public double? LatenciaPrimerAvisoMin { get; set; }
        public PanelFiltro Filtro { get; set; } = new();
        public int TotalGeneral { get; set; }
        public int TotalFiltrados { get; set; }
        public List<PanelFila> Casos { get; set; } = new();
    }

    // acciones de filtrado para mas efectividad
    public sealed class PanelFiltro
    {
        public string? Texto { get; set; }
        public ClaimStatus? Estado { get; set; }
        public ClaimType? Tipo { get; set; }
        public PanelAlerta Alerta { get; set; } = PanelAlerta.Todas;
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }

        public bool HayFiltros =>
            !string.IsNullOrWhiteSpace(Texto)
            || Estado.HasValue
            || Tipo.HasValue
            || Alerta != PanelAlerta.Todas
            || Desde.HasValue
            || Hasta.HasValue;
    }
    // revision Umbral humano
    public sealed class RevisionDocumentoDatos
    {
        public Guid DocumentoId { get; set; }
        public RequiredDocument? TipoAnterior { get; set; }
        public RequiredDocument? TipoNuevo { get; set; }
        public DocumentValidation ValidacionAnterior { get; set; }
        public DocumentValidation ValidacionNueva { get; set; }
        public string? Notas { get; set; }
    }

}