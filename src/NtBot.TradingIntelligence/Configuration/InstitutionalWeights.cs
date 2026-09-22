namespace NtBot.TradingIntelligence.Configuration;

/// <summary>
/// Pesos institucionais do motor de confluência (XAUUSD-first).
/// Engines Unknown têm peso efetivo zero; a soma dos base weights = 1.0.
/// </summary>
public static class InstitutionalWeights
{
    /// <summary>Estrutura de preço + SMC (BOS/CHoCH/OB/FVG/Premium-Discount).</summary>
    public const decimal Structure = 0.30m;

    /// <summary>Sweeps, equal highs/lows, pools de liquidez.</summary>
    public const decimal Liquidity = 0.20m;

    /// <summary>Tick/real volume, spikes e divergências.</summary>
    public const decimal Volume = 0.15m;

    /// <summary>Fases e eventos Wyckoff.</summary>
    public const decimal Wyckoff = 0.10m;

    /// <summary>Correlação dinâmica + drivers do ativo.</summary>
    public const decimal Correlation = 0.10m;

    /// <summary>Regime macro e calendário.</summary>
    public const decimal Macro = 0.10m;

    /// <summary>ATR / regime de volatilidade (não direcional forte).</summary>
    public const decimal Volatility = 0.05m;

    public const decimal Risk = 0m;

    // Aliases legados — mantidos para adapters/tests antigos; preferir os nomes acima.
    public const decimal Trend = Structure;
    public const decimal Smc = 0.14m;
    public const decimal Momentum = 0.12m;
    public const decimal Drivers = 0.12m;
}

/// <summary>Pesos legados (Market Drivers era). Preferir <see cref="InstitutionalWeights"/>.</summary>
public static class ConfluenceWeights
{
    public const decimal Macro = InstitutionalWeights.Macro;
    public const decimal Drivers = InstitutionalWeights.Drivers;
    public const decimal Wyckoff = InstitutionalWeights.Wyckoff;
    public const decimal Smc = InstitutionalWeights.Smc;
    public const decimal Volume = InstitutionalWeights.Volume;
    public const decimal Momentum = InstitutionalWeights.Momentum;
    public const decimal Correlation = InstitutionalWeights.Correlation;
    public const decimal Liquidity = InstitutionalWeights.Liquidity;
    public const decimal Calendar = 0.02m;
}

/// <summary>Níveis de confiança operacional — nunca emitir Compra/Venda em Baixa/Muito Baixa.</summary>
public static class ConfidenceLevels
{
    public const string MuitoBaixa = "Muito Baixa";
    public const string Baixa = "Baixa";
    public const string Moderada = "Moderada";
    public const string Alta = "Alta";
    public const string MuitoAlta = "Muito Alta";
    public const string Institucional = "Institucional";

    public static string FromScore(decimal confidence) => confidence switch
    {
        >= 85 => Institucional,
        >= 72 => MuitoAlta,
        >= 58 => Alta,
        >= 45 => Moderada,
        >= 30 => Baixa,
        _ => MuitoBaixa
    };

    /// <summary>Confiança insuficiente para direção — força AGUARDAR / Neutro.</summary>
    public static bool BlocksDirectionalTrade(string level) =>
        level is MuitoBaixa or Baixa;
}

public static class ConfluenceClassification
{
    public static string Classify(int score) => score switch
    {
        >= 95 => "Confluência Extrema",
        >= 85 => "Muito Alta",
        >= 70 => "Alta",
        >= 55 => "Moderada",
        >= 40 => "Neutra",
        >= 20 => "Fraca",
        _ => "Muito Fraca"
    };

    public static string ClassifyRecommendation(int score) => score switch
    {
        >= 85 => "COMPRA FORTE",
        >= 70 => "COMPRA MODERADA",
        >= 58 => "COMPRA FRACA",
        <= 15 => "VENDA FORTE",
        <= 30 => "VENDA MODERADA",
        <= 42 => "VENDA FRACA",
        _ => "NEUTRO"
    };

    public static string ClassifyBias(int score) => score switch
    {
        >= 58 => "Bullish",
        <= 42 => "Bearish",
        _ => "Sideways"
    };

    public static string ClassifyRiskLevel(decimal confidence) => confidence switch
    {
        >= 80 => "Baixo",
        >= 60 => "Moderado",
        >= 40 => "Alto",
        _ => "Extremo"
    };
}
