namespace NtBot.Connector.Windows.Configuration;

public sealed class ProfitDdeAssetConfig
{
    public string LogicalSymbol { get; set; } = string.Empty;

    /// <summary>Código do ativo no Profit (ex: WINFUT, WDOFUT, PETR4).</summary>
    public string DdeSymbol { get; set; } = string.Empty;

    /// <summary>Legado — se preenchido sem DdeSymbol, trata como código do ativo.</summary>
    public string DdeTopic { get; set; } = string.Empty;

    /// <summary>Qualificador DDE (ULT, MAX, MIN…) ou item completo (WINFUT.ULT).</summary>
    public string PriceItem { get; set; } = "ULT";

    public string? BidItem { get; set; }
    public string? AskItem { get; set; }
    public List<string>? DdeAliases { get; set; }

    /// <summary>Replica ticks de outro símbolo lógico (ex: WINQ26 ← WIN no replay).</summary>
    public string? MirrorFromSymbol { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsMirrorOnly => !string.IsNullOrWhiteSpace(MirrorFromSymbol);

    public string ResolveSymbol() =>
        !string.IsNullOrWhiteSpace(DdeSymbol) ? DdeSymbol.Trim()
        : !string.IsNullOrWhiteSpace(DdeTopic) ? DdeTopic.Trim()
        : LogicalSymbol.Trim();

    public IEnumerable<string> EnumerateDdeSymbols()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in new[] { DdeSymbol, LogicalSymbol }
                     .Concat(DdeAliases ?? [])
                     .Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var trimmed = symbol.Trim();
            if (seen.Add(trimmed))
                yield return trimmed;
        }
    }

    public string ResolvePriceItem()
    {
        var field = PriceItem.Trim();
        if (field.Contains('.'))
            return field.ToUpperInvariant();

        return $"{ResolveSymbol()}.{field.ToUpperInvariant()}";
    }
}

public sealed class ProfitDdeConfig
{
    /// <summary>Serviço DDE — manual Profit: profitchart.</summary>
    public string DdeServer { get; set; } = "profitchart";

    /// <summary>Tópico DDE — manual Profit: COT (cotações).</summary>
    public string DdeTopic { get; set; } = "COT";

    public int HeartbeatMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 5000;
    public bool AutoReconnect { get; set; } = true;
    public int MaxReconnectAttempts { get; set; } = 10;
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Quando true: bloqueia RTD live e prioriza DDE do contínuo (WINFUT/WDOFUT),
    /// onde o replay do ProfitChart normalmente atualiza as cotações.
    /// </summary>
    public bool ReplayMode { get; set; }

    /// <summary>
    /// Contratos preferenciais no replay. Use o contínuo (WINFUT/WDOFUT) se o gráfico
    /// em replay for do contínuo; só use o mês (WINQ26/WDOQ26) se o replay for nesse código.
    /// </summary>
    public Dictionary<string, string> ReplayContracts { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WIN"] = "WINFUT",
        ["WDO"] = "WDOFUT"
    };

    public List<ProfitDdeAssetConfig> Assets { get; set; } = [];
}
