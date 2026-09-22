using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NtBot.Domain.Entities;

public class BoletaOrder
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(32)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Buy | Sell</summary>
    [Required]
    [StringLength(8)]
    public string Direction { get; set; } = "Buy";

    [Column(TypeName = "numeric(18,8)")]
    public decimal Volume { get; set; }

    [Column(TypeName = "numeric(18,8)")]
    public decimal? EntryPrice { get; set; }

    [Column(TypeName = "numeric(18,8)")]
    public decimal? StopLoss { get; set; }

    [Column(TypeName = "numeric(18,8)")]
    public decimal? TakeProfit { get; set; }

    /// <summary>Melhor preço favorável desde a entrada (high Buy / low Sell) para trailing.</summary>
    [Column(TypeName = "numeric(18,8)")]
    public decimal? PeakFavorablePrice { get; set; }

    /// <summary>SL trailing recalculado (último aplicado).</summary>
    [Column(TypeName = "numeric(18,8)")]
    public decimal? TrailingStopPrice { get; set; }

    /// <summary>Para LinearGradient: alvo parcial 1..N</summary>
    public int TargetLevel { get; set; }

    [StringLength(32)]
    public string Strategy { get; set; } = BoletaStrategies.Wyckoff;

    /// <summary>Pending | Submitted | Filled | PartiallyClosed | Closed | Rejected | Failed</summary>
    [Required]
    [StringLength(24)]
    public string Status { get; set; } = BoletaOrderStatus.Pending;

    [StringLength(64)]
    public string? Mt5Ticket { get; set; }

    [StringLength(64)]
    public string? Mt5OrderId { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal? RealizedPnl { get; set; }

    [StringLength(512)]
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExecutedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public BoletaSession? Session { get; set; }
}

public static class BoletaOrderStatus
{
    public const string Pending = "Pending";
    public const string Submitted = "Submitted";
    public const string Filled = "Filled";
    public const string PartiallyClosed = "PartiallyClosed";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";
}
