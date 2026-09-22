using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NtBot.Domain.Entities;

/// <summary>
/// Sessão diária de boletagem por tenant + símbolo.
/// Guarda meta, drawdown, estratégia e estado da automação.
/// </summary>
public class BoletaSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(32)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Data operacional UTC (meia-noite).</summary>
    [Required]
    public DateTime SessionDate { get; set; }

    /// <summary>Wyckoff | LinearGradient | ScalpShort</summary>
    [Required]
    [StringLength(32)]
    public string Strategy { get; set; } = BoletaStrategies.Wyckoff;

    /// <summary>Meta aproximada de lucro do dia (moeda da conta).</summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal DailyProfitTarget { get; set; }

    /// <summary>Drawdown máximo permitido (% do saldo de referência).</summary>
    [Column(TypeName = "numeric(8,2)")]
    public decimal MaxDrawdownPercent { get; set; } = 2.0m;

    /// <summary>
    /// Trailing stop em % sobre o preço de pico favorável do trade.
    /// 0 = desligado. Ex.: 0.40 = SL a 0,40% atrás do melhor preço desde a entrada.
    /// </summary>
    [Column(TypeName = "numeric(8,4)")]
    public decimal TrailingStopPercent { get; set; }

    /// <summary>Volume base por ordem (lotes).</summary>
    [Column(TypeName = "numeric(18,8)")]
    public decimal LotSize { get; set; } = 0.01m;

    /// <summary>Saldo de referência no início da sessão (para % de resultado).</summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal ReferenceBalance { get; set; }

    /// <summary>PnL realizado + flutuante da sessão.</summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal RealizedPnl { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal FloatingPnl { get; set; }

    /// <summary>Pico de equity desde o início (para drawdown).</summary>
    [Column(TypeName = "numeric(18,2)")]
    public decimal PeakEquity { get; set; }

    [Column(TypeName = "numeric(8,2)")]
    public decimal CurrentDrawdownPercent { get; set; }

    /// <summary>Baixo | Moderado | Alto | Extremo — derivado do cenário TI.</summary>
    [StringLength(24)]
    public string RiskIndication { get; set; } = "Moderado";

    [StringLength(32)]
    public string ScenarioBias { get; set; } = "Sideways";

    [StringLength(64)]
    public string ScenarioRecommendation { get; set; } = "NEUTRO";

    public int ScenarioConfluenceScore { get; set; }

    public bool AutomationEnabled { get; set; }

    /// <summary>
    /// Start/Stop da boletagem: quando false (Stop), não abre novas ordens no MT5.
    /// Fechamento de posições e dry-run continuam permitidos. Default Stop (seguro).
    /// </summary>
    public bool TradingEnabled { get; set; }

    /// <summary>Permite abrir ordens mesmo com recomendação NEUTRO/AGUARDAR.</summary>
    public bool AllowNeutralEntries { get; set; } = true;

    /// <summary>Buy | Sell escolhido pelo operador; null = direção automática.</summary>
    public string? PreferredDirection { get; set; }

    public bool MetaReached { get; set; }

    public bool DrawdownBreached { get; set; }

    public bool PositionsClosedByRule { get; set; }

    /// <summary>Active | Paused | ClosedMeta | ClosedDrawdown | ClosedManual</summary>
    [Required]
    [StringLength(24)]
    public string Status { get; set; } = BoletaSessionStatus.Active;

    [StringLength(512)]
    public string? LastMessage { get; set; }

    /// <summary>% de ganho/perda vs ReferenceBalance após fechamento por regra.</summary>
    [Column(TypeName = "numeric(10,4)")]
    public decimal? ResultPercentOfBalance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }

    public ICollection<BoletaOrder> Orders { get; set; } = new List<BoletaOrder>();

    [NotMapped]
    public decimal SessionPnl => RealizedPnl + FloatingPnl;

    [NotMapped]
    public decimal SessionPnlPercent =>
        ReferenceBalance > 0 ? Math.Round(SessionPnl / ReferenceBalance * 100m, 2) : 0m;
}

public static class BoletaStrategies
{
    public const string Wyckoff = "Wyckoff";
    public const string LinearGradient = "LinearGradient";
    /// <summary>Scalp curto: TP $10 / SL técnico (2 ticks além mín/máx candle anterior) / BE $7→$2.</summary>
    public const string ScalpShort = "ScalpShort";

    public static readonly string[] All = [Wyckoff, LinearGradient, ScalpShort];
}

public static class BoletaSessionStatus
{
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string ClosedMeta = "ClosedMeta";
    public const string ClosedDrawdown = "ClosedDrawdown";
    public const string ClosedManual = "ClosedManual";
}
