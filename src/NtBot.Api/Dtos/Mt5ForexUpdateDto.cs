using NtBot.MarketIntelligence.Models;

namespace NtBot.Api.Dtos;

public sealed class Mt5ForexUpdateDto
{
    public bool IsLive { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public IReadOnlyList<MarketSnapshot> Currencies { get; init; } = [];
}
