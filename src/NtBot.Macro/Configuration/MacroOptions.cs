namespace NtBot.Macro.Configuration;



/// <summary>

/// Macro intelligence configuration bound from the <c>Macro</c> configuration section.

/// </summary>

public sealed class MacroOptions

{

    public const string SectionName = "Macro";



    /// <summary>

    /// FRED API key (32-character lowercase alphanumeric).

    /// Configure via <c>Macro:FredApiKey</c> in appsettings, environment variable

    /// <c>Macro__FredApiKey</c>, or user secrets.

    /// Register a free key at <see href="https://fredaccount.stlouisfed.org/"/>.

    /// </summary>

    public string FredApiKey { get; set; } = string.Empty;



    /// <summary>

    /// FRED API base URL. Default: <c>https://api.stlouisfed.org/fred</c>.

    /// </summary>

    public string FredBaseUrl { get; set; } = "https://api.stlouisfed.org/fred";



    public bool UseRedis { get; set; }

    public string RedisConnectionString { get; set; } = string.Empty;

}



public static class MacroProviderNames

{

    public const string Fred = "FRED";

    public const string Mt5Calendar = "MT5 Economic Calendar";

    public const string CentralBank = "Banco Central";

    public const string YahooFinance = "Yahoo Finance";

    public const string Mock = "Mock";

}


