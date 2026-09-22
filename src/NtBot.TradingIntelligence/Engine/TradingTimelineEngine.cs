using NtBot.TradingIntelligence.Models;



namespace NtBot.TradingIntelligence.Engine;



public sealed class InstitutionalTimelineInput

{

    public string Asset { get; init; } = string.Empty;

    public IReadOnlyList<EngineAnalysisResult> Engines { get; init; } = [];

    public ConfluenceScoreResult Confluence { get; init; } = new();

    public IReadOnlyList<TimeframeIntersection> Intersections { get; init; } = [];

    public IReadOnlyList<WyckoffStructureEvent> WyckoffEvents { get; init; } = [];

    public IReadOnlyList<SmcStructureEvent> SmcEvents { get; init; } = [];

    public IReadOnlyList<SmcOverlayBundle> SmcOverlays { get; init; } = [];

}



public static class TradingTimelineEngine

{

    public static IReadOnlyList<TradingTimelineEvent> Build(

        string asset,

        IReadOnlyList<EngineAnalysisResult> engines,

        ConfluenceScoreResult confluence,

        IReadOnlyList<TimeframeIntersection> intersections) =>

        Build(new InstitutionalTimelineInput

        {

            Asset = asset,

            Engines = engines,

            Confluence = confluence,

            Intersections = intersections

        });



    public static IReadOnlyList<TradingTimelineEvent> Build(InstitutionalTimelineInput input)

    {

        var events = new List<TradingTimelineEvent>();

        var now = DateTime.UtcNow;



        foreach (var wyckoffEvent in input.WyckoffEvents.OrderByDescending(e => e.Timestamp).Take(5))

        {

            events.Add(new TradingTimelineEvent

            {

                Timestamp = wyckoffEvent.Timestamp,

                Category = "Wyckoff",

                Title = FormatWyckoffTitle(wyckoffEvent.Event),

                Description = $"{wyckoffEvent.Description} · confiança {wyckoffEvent.Confidence:F0}%"

                    + (wyckoffEvent.PriceLevel.HasValue ? $" · nível {wyckoffEvent.PriceLevel:F0}" : ""),

                Severity = WyckoffSeverity(wyckoffEvent.Event)

            });

        }



        foreach (var smcEvent in input.SmcEvents

                     .GroupBy(e => $"{e.Type}:{e.Timestamp:O}")

                     .Select(g => g.First())

                     .OrderByDescending(e => e.Timestamp)

                     .Take(6))

        {

            events.Add(new TradingTimelineEvent

            {

                Timestamp = smcEvent.Timestamp,

                Category = "SMC",

                Title = smcEvent.Title,

                Description = smcEvent.Description

                    + (smcEvent.PriceLevel.HasValue ? $" · {smcEvent.PriceLevel:F0}" : ""),

                Severity = smcEvent.Severity

            });

        }



        foreach (var engine in input.Engines.Where(e => e.Status == EngineDataStatus.Known))

        {

            foreach (var signal in engine.Signals.Take(2))

            {

                if (IsDuplicateSignal(events, engine.Engine, signal))

                    continue;



                events.Add(new TradingTimelineEvent

                {

                    Timestamp = now,

                    Category = engine.Engine,

                    Title = $"{engine.Engine}: {signal}",

                    Description = $"Score {engine.Score}/100 · confiança {engine.Confidence:F0}%",

                    Severity = SeverityFromBias(engine.Bias, engine.Score)

                });

            }

        }



        if (input.Confluence.Bias is "Bullish" or "Bearish")

        {

            events.Add(new TradingTimelineEvent

            {

                Timestamp = now,

                Category = "Confluence",

                Title = $"Bias {input.Confluence.Bias} confirmado",

                Description = $"Confluence {input.Confluence.Score}/100 · {input.Confluence.Recommendation}",

                Severity = input.Confluence.Bias == "Bullish" ? "Bullish" : "Bearish"

            });

        }



        foreach (var intersection in input.Intersections.Where(i => i.HighConfluence).Take(3))

        {

            events.Add(new TradingTimelineEvent

            {

                Timestamp = now,

                Category = "SMC",

                Title = $"Alta confluência {intersection.Pair}",

                Description = $"Zona {intersection.PriceLow:F0}–{intersection.PriceHigh:F0} · score {intersection.ConfluenceScore}",

                Severity = "Info"

            });

        }



        foreach (var overlay in input.SmcOverlays.Where(o => o.Score >= 65 || o.Score <= 35).Take(2))

        {

            events.Add(new TradingTimelineEvent

            {

                Timestamp = now,

                Category = "SMC",

                Title = $"Estrutura {overlay.Timeframe}m — {overlay.Bias}",

                Description = overlay.Summary,

                Severity = overlay.Bias.Equals("Bullish", StringComparison.OrdinalIgnoreCase) ? "Bullish"

                    : overlay.Bias.Equals("Bearish", StringComparison.OrdinalIgnoreCase) ? "Bearish"

                    : "Info"

            });

        }



        var volume = input.Engines.FirstOrDefault(e => e.Engine == "Volume");

        if (volume?.Signals.Any(s => s.Contains("acima", StringComparison.OrdinalIgnoreCase)) == true)

        {

            events.Add(new TradingTimelineEvent

            {

                Timestamp = now,

                Category = "Volume",

                Title = "Entrada institucional / volume elevado",

                Description = string.Join(", ", volume.Signals),

                Severity = volume.Bias == EngineMarketBias.Bullish ? "Bullish" : "Info"

            });

        }



        return events

            .OrderByDescending(e => e.Severity is "Bullish" or "Bearish" or "Warning")

            .ThenByDescending(e => e.Timestamp)

            .Take(24)

            .ToList();

    }



    private static bool IsDuplicateSignal(IReadOnlyList<TradingTimelineEvent> existing, string category, string signal)

    {

        var normalized = signal.ToUpperInvariant();

        return existing.Any(e =>

            e.Category == category &&

            (e.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||

             e.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase)));

    }



    private static string FormatWyckoffTitle(WyckoffMarketEvent evt) => evt switch

    {

        WyckoffMarketEvent.Spring => "Spring detectado",

        WyckoffMarketEvent.Upthrust => "Upthrust detectado",

        WyckoffMarketEvent.SellingClimax => "Selling Climax (SC)",

        WyckoffMarketEvent.BuyingClimax => "Buying Climax (BC)",

        WyckoffMarketEvent.SignOfStrength => "Sign of Strength (SOS)",

        WyckoffMarketEvent.SignOfWeakness => "Sign of Weakness (SOW)",

        _ => evt.ToString()

    };



    private static string WyckoffSeverity(WyckoffMarketEvent evt) => evt switch

    {

        WyckoffMarketEvent.Spring or WyckoffMarketEvent.SignOfStrength or WyckoffMarketEvent.BuyingClimax => "Bullish",

        WyckoffMarketEvent.Upthrust or WyckoffMarketEvent.SignOfWeakness or WyckoffMarketEvent.SellingClimax => "Bearish",

        _ => "Warning"

    };



    private static string SeverityFromBias(EngineMarketBias bias, int? score) => bias switch

    {

        EngineMarketBias.Bullish when score >= 60 => "Bullish",

        EngineMarketBias.Bearish when score <= 40 => "Bearish",

        _ => "Info"

    };

}


