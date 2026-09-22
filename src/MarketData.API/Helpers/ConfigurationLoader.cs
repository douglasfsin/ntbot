using MarketData.API.Configuration;

namespace MarketData.API.Helpers
{
    public static class ConfigurationLoader
    {
        private static AppSettings? _settings;

        public static AppSettings Settings => _settings ??= Load();

        private static AppSettings Load()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables("PROFIT_")  // Sobrescreve com variáveis de ambiente
                .Build();

            var settings = new AppSettings();
            config.GetSection("ProfitDLL").Bind(settings.ProfitDLL);
            config.GetSection("Market").Bind(settings.Market);
            return settings;
        }
    }
}
