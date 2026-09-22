namespace MarketData.API.Configuration
{
    public class ProfitDllSettings
    {
        public string ActivationKey { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string DllPath { get; set; } = "ProfitDLL.dll";
        public bool UseRouting { get; set; } = true;
        public bool UseDayTrade { get; set; } = false;
        public bool EnableLogToDebug { get; set; } = false;
        public int WaitConnectionTimeoutSeconds { get; set; } = 30;
        public string DefaultExchange { get; set; } = "F";
        public int NiveisBook { get; set; } = 20; // Default: 20 (10 buy + 10 sell)
        public List<Ticker> Tickers { get; set; } = new();
        public bool EnableDetailedLogging { get; set; } = false;
    }

    public class Ticker
    {
        public string Code { get; set; } = string.Empty;
        public string DefaultExchange { get; set; } = "F";
        /// <summary>
        /// A=BCB, B=Bovespa, D=Cambio, E=Economic, F=BMF,
        /// K=Metrics, M=CME, N=Nasdaq, O=OXR, P=Pioneer, X=DowJones, Y=Nyse
        /// </summary>
        public string Bag { get; set; } = "F";
    }
    public enum Bag
    {
        Bovespa = 'B',
        BMF = 'F',
        Nasdaq = 'N',
        Pioneer = 'P',
        DowJones = 'X',
        Nyse = 'Y',
        Cambios = 'D',
        Economic = 'E',
        Metrics = 'K',
        CME = 'M',
        OXR = 'O',
        BCBC = 'A'
    }

    public class MarketSettings
    {
        public string DefaultExchange { get; set; } = "B";
        public int WaitConnectionTimeoutMs { get; set; } = 30000;
    }

    public class AppSettings
    {
        public ProfitDllSettings ProfitDLL { get; set; } = new();
        public MarketSettings Market { get; set; } = new();
    }
}
