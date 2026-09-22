using System;

namespace Orbital.Core.Models;

public record PriceZone
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = "OrderBlock"; // OrderBlock, LiquiditySweep, Reference, etc.
    public string Side { get; init; } = "Compra";      // Compra, Venda, Neutra
    public string Description { get; init; } = string.Empty;
    public double HighPrice { get; init; }
    public double LowPrice { get; init; }
    public double TrackedVolume { get; init; }
    public bool IsActive { get; set; } = true;
}
