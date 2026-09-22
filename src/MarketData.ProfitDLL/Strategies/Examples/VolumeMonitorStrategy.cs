namespace ProfitDLLClient.Strategies.Examples;

/// <summary>
/// Estratégia de exemplo para monitoramento de volume
/// Esta é apenas uma demonstração - NÃO usar em produção sem testes adequados
/// </summary>
public class VolumeMonitorStrategy : BaseStrategy
{
    private readonly Dictionary<string, VolumeData> _volumeData = new();
    private readonly long _alertThreshold;

    public override string Name => "Volume Monitor";
    public override string Description => $"Monitora volume e alerta quando supera {_alertThreshold:N0} contratos";

    private class VolumeData
    {
        public long TotalVolume { get; set; }
        public int TradeCount { get; set; }
        public DateTime LastReset { get; set; }
        public double HighPrice { get; set; }
        public double LowPrice { get; set; }
    }

    public VolumeMonitorStrategy(PositionManager positionManager, long alertThreshold = 1000)
        : base(positionManager)
    {
        _alertThreshold = alertThreshold;
    }

    public override void OnTrade(string ticker, string exchange, TConnectorTrade trade)
    {
        base.OnTrade(ticker, exchange, trade);

        var key = $"{ticker}:{exchange}";

        if (!_volumeData.TryGetValue(key, out var data))
        {
            data = new VolumeData
            {
                LastReset = DateTime.Now,
                HighPrice = trade.Price,
                LowPrice = trade.Price
            };
            _volumeData[key] = data;
        }

        // Atualiza dados de volume
        data.TotalVolume += trade.Quantity;
        data.TradeCount++;
        data.HighPrice = Math.Max(data.HighPrice, trade.Price);
        data.LowPrice = Math.Min(data.LowPrice, trade.Price);

        // Verifica se passou 1 minuto desde o último reset
        if ((DateTime.Now - data.LastReset).TotalMinutes >= 1)
        {
            var avgPrice = (data.HighPrice + data.LowPrice) / 2;
            Log($"{ticker} | Volume 1min: {data.TotalVolume:N0} | Trades: {data.TradeCount} | H: {data.HighPrice:N2} | L: {data.LowPrice:N2} | Avg: {avgPrice:N2}");

            // Alerta se volume exceder threshold
            if (data.TotalVolume >= _alertThreshold)
            {
                Log($"⚠️  ALERTA: Volume alto detectado em {ticker} - {data.TotalVolume:N0} contratos!");
            }

            // Reset dos dados
            data.TotalVolume = 0;
            data.TradeCount = 0;
            data.LastReset = DateTime.Now;
            data.HighPrice = trade.Price;
            data.LowPrice = trade.Price;
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        _volumeData.Clear();
        Log("Iniciando monitoramento de volume");
    }

    public override void Stop()
    {
        base.Stop();
        _volumeData.Clear();
    }
}
