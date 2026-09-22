using Microsoft.Extensions.Options;
using NtBot.TradingIntelligence.Cache;
using NtBot.TradingIntelligence.Configuration;
using NtBot.TradingIntelligence.Engine;
using NtBot.TradingIntelligence.Models;

namespace NtBot.UnitTests.TradingIntelligence;

public class TradingEngineCacheServiceTests
{
    private static TradingEngineCacheService CreateService(int ttlSeconds = 300) =>
        new(Options.Create(new TradingIntelligenceOptions { CacheTtlSeconds = ttlSeconds }));

    [Fact]
    public void SetAndGet_ReturnsStoredEngineResult()
    {
        var cache = CreateService();
        var result = EngineAnalysisResult.Known(
            "Trend", 72, 80m, InstitutionalWeights.Trend, EngineMarketBias.Bullish, dataSource: "Profit");

        cache.Set("WIN", "Trend", result, DateTime.UtcNow, "Profit");
        var cached = cache.Get<EngineAnalysisResult>("WIN", "Trend");

        Assert.NotNull(cached);
        Assert.Equal(72, cached!.Value.Score);
        Assert.Equal("Profit", cached.Source);
    }

    [Fact]
    public void IsFresh_ReturnsFalse_WhenNewCandleArrives()
    {
        var cache = CreateService();
        var candleTime = new DateTime(2026, 7, 9, 14, 0, 0, DateTimeKind.Utc);
        var result = EngineAnalysisResult.Known(
            "Momentum", 65, 70m, InstitutionalWeights.Momentum, EngineMarketBias.Bullish, dataSource: "Profit");

        cache.Set("WIN", "Momentum", result, candleTime, "Profit");

        Assert.True(cache.IsFresh("WIN", "Momentum", candleTime));
        Assert.False(cache.IsFresh("WIN", "Momentum", candleTime.AddMinutes(60)));
    }

    [Fact]
    public void InvalidateAsset_RemovesAllEngineEntriesForAsset()
    {
        var cache = CreateService();
        var result = EngineAnalysisResult.Known(
            "Trend", 70, 80m, InstitutionalWeights.Trend, EngineMarketBias.Bullish, dataSource: "Profit");

        cache.Set("WIN", "Trend", result, DateTime.UtcNow, "Profit");
        cache.Set("WIN", "SMC:60", new SmcAnalysisResult(), DateTime.UtcNow, "Profit");
        cache.Set("WDO", "Trend", result, DateTime.UtcNow, "Profit");

        cache.InvalidateAsset("WIN");

        Assert.Null(cache.Get<EngineAnalysisResult>("WIN", "Trend"));
        Assert.Null(cache.Get<SmcAnalysisResult>("WIN", "SMC:60"));
        Assert.NotNull(cache.Get<EngineAnalysisResult>("WDO", "Trend"));
    }
}
