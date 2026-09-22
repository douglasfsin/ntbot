namespace NtBot.Shared.Trading;

/// <summary>
/// Trava de direção: boletagem não abre posição contrária ao Market Drivers.
/// COMPRA* → só Buy; VENDA* → só Sell; NEUTRO/AGUARDAR → sem trava de lado.
/// </summary>
public static class MarketDriversDirectionGuard
{
    public enum Bias
    {
        Neutral,
        BuyOnly,
        SellOnly
    }

    public static Bias Parse(string? recommendation)
    {
        if (string.IsNullOrWhiteSpace(recommendation))
            return Bias.Neutral;

        var r = recommendation.Trim();
        if (r.Contains("COMPRA", StringComparison.OrdinalIgnoreCase))
            return Bias.BuyOnly;
        if (r.Contains("VENDA", StringComparison.OrdinalIgnoreCase))
            return Bias.SellOnly;
        return Bias.Neutral;
    }

    public static string? NormalizeDirection(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "buy" or "compra" or "long" => "Buy",
            "sell" or "venda" or "short" => "Sell",
            _ => null
        };

    /// <summary>null = permitido; mensagem = bloqueado.</summary>
    public static string? BlockReason(string? recommendation, string? direction)
    {
        var normalized = NormalizeDirection(direction);
        if (normalized is null)
            return null;

        return Parse(recommendation) switch
        {
            Bias.BuyOnly when normalized == "Sell" =>
                $"Market Drivers = {recommendation?.Trim()} — posições de VENDA bloqueadas.",
            Bias.SellOnly when normalized == "Buy" =>
                $"Market Drivers = {recommendation?.Trim()} — posições de COMPRA bloqueadas.",
            _ => null
        };
    }

    public static bool IsAllowed(string? recommendation, string? direction) =>
        BlockReason(recommendation, direction) is null;

    public static string? AllowedDirectionOrNull(string? recommendation) =>
        Parse(recommendation) switch
        {
            Bias.BuyOnly => "Buy",
            Bias.SellOnly => "Sell",
            _ => null
        };
}
