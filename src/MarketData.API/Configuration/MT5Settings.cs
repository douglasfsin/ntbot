namespace MarketData.API.Configuration
{
    public class MT5Settings
    {
        public bool Enabled { get; set; } = false;

        /// <summary>URL base da API Python Flask (ex: "http://127.0.0.1:8228")</summary>
        public string PythonApiBaseUrl { get; set; } = "http://127.0.0.1:8228";

        /// <summary>
        /// Allowlist de símbolos SSE — única fonte para o worker. Deve coincidir com
        /// Connector <c>mt5_config.json</c> / <c>MT5_SYMBOLS</c> (não usa Market Watch completo).
        /// </summary>
        public List<string> Symbols { get; set; } = new();

        public int Port { get; set; } = 7189;
        public string HubPath { get; set; } = "/mt5Hub";

        /// <summary>
        /// Intervalo máximo de inatividade antes de considerar o MT5 desconectado (segundos).
        /// </summary>
        public int InactivityTimeoutSeconds { get; set; } = 60;
    }
}
