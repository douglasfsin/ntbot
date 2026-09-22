namespace ProfitDLLClient.Strategies;

/// <summary>
/// Informações sobre uma posição aberta
/// </summary>
public class PositionInfo
{
    public string Asset { get; set; }
    public string Exchange { get; set; }
    public string AccountID { get; set; }
    public int BrokerID { get; set; }
    public TConnectorOrderSide Side { get; set; }
    public long Quantity { get; set; }
    public double AveragePrice { get; set; }
    public double CurrentPrice { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime LastUpdate { get; set; }

    // Cálculos
    public double TotalValue => Quantity * AveragePrice;
    public double CurrentValue => Quantity * CurrentPrice;
    public double GainLoss => CurrentValue - TotalValue;
    public double GainLossPercent => TotalValue != 0 ? (GainLoss / TotalValue) * 100 : 0;
    public string GainLossFormatted => GainLoss >= 0 ? $"+R$ {GainLoss:N2}" : $"R$ {GainLoss:N2}";
    public string GainLossPercentFormatted => GainLossPercent >= 0 ? $"+{GainLossPercent:N2}%" : $"{GainLossPercent:N2}%";

    public override string ToString()
    {
        var sideStr = Side == TConnectorOrderSide.Buy ? "COMPRA" : "VENDA";
        return $"{Asset} | {sideStr} | Qtd: {Quantity} | Médio: R$ {AveragePrice:N2} | Atual: R$ {CurrentPrice:N2} | Gain: {GainLossFormatted} ({GainLossPercentFormatted})";
    }
}

/// <summary>
/// Gerenciador de posições
/// </summary>
public class PositionManager
{
    private readonly Dictionary<string, PositionInfo> _positions = new();
    private readonly object _lock = new();

    /// <summary>
    /// Adiciona ou atualiza uma posição
    /// </summary>
    public void UpdatePosition(string asset, string exchange, string accountId, int brokerId,
                              TConnectorOrderSide side, long quantity, double averagePrice, double currentPrice)
    {
        lock (_lock)
        {
            var key = GetPositionKey(asset, exchange, accountId, brokerId);

            if (quantity == 0)
            {
                _positions.Remove(key);
                return;
            }

            if (_positions.TryGetValue(key, out var position))
            {
                position.Quantity = quantity;
                position.AveragePrice = averagePrice;
                position.CurrentPrice = currentPrice;
                position.LastUpdate = DateTime.Now;
            }
            else
            {
                _positions[key] = new PositionInfo
                {
                    Asset = asset,
                    Exchange = exchange,
                    AccountID = accountId,
                    BrokerID = brokerId,
                    Side = side,
                    Quantity = quantity,
                    AveragePrice = averagePrice,
                    CurrentPrice = currentPrice,
                    OpenTime = DateTime.Now,
                    LastUpdate = DateTime.Now
                };
            }
        }
    }

    /// <summary>
    /// Atualiza o preço atual de um ativo
    /// </summary>
    public void UpdatePrice(string asset, string exchange, double price)
    {
        lock (_lock)
        {
            var positions = _positions.Values.Where(p => p.Asset == asset && p.Exchange == exchange).ToList();
            foreach (var position in positions)
            {
                position.CurrentPrice = price;
                position.LastUpdate = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// Obtém todas as posições abertas
    /// </summary>
    public List<PositionInfo> GetOpenPositions()
    {
        lock (_lock)
        {
            return _positions.Values.ToList();
        }
    }

    /// <summary>
    /// Obtém posições de um ativo específico
    /// </summary>
    public List<PositionInfo> GetPositionsByAsset(string asset, string exchange)
    {
        lock (_lock)
        {
            return _positions.Values
                .Where(p => p.Asset == asset && p.Exchange == exchange)
                .ToList();
        }
    }

    /// <summary>
    /// Obtém posições de uma conta específica
    /// </summary>
    public List<PositionInfo> GetPositionsByAccount(string accountId, int brokerId)
    {
        lock (_lock)
        {
            return _positions.Values
                .Where(p => p.AccountID == accountId && p.BrokerID == brokerId)
                .ToList();
        }
    }

    /// <summary>
    /// Calcula o gain/loss total de todas as posições
    /// </summary>
    public double GetTotalGainLoss()
    {
        lock (_lock)
        {
            return _positions.Values.Sum(p => p.GainLoss);
        }
    }

    /// <summary>
    /// Exibe resumo das posições
    /// </summary>
    public void PrintPositionsSummary()
    {
        lock (_lock)
        {
            if (_positions.Count == 0)
            {
                Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              NENHUMA POSIÇÃO ABERTA                            ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                return;
            }

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                                                    POSIÇÕES ABERTAS                                                                ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║ Ativo      │ Lado    │ Quantidade │ Preço Médio │ Preço Atual │ Valor Total  │ Gain/Loss    │ Gain/Loss %  │ Conta         ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");

            double totalGain = 0;

            foreach (var position in _positions.Values.OrderBy(p => p.Asset))
            {
                var sideStr = position.Side == TConnectorOrderSide.Buy ? "COMPRA" : "VENDA ";
                var gainColor = position.GainLoss >= 0 ? "+" : "";

                Console.WriteLine($"║ {position.Asset,-10} │ {sideStr} │ {position.Quantity,10} │ R$ {position.AveragePrice,8:N2} │ R$ {position.CurrentPrice,8:N2} │ R$ {position.TotalValue,9:N2} │ {gainColor}{position.GainLoss,10:N2} │ {gainColor}{position.GainLossPercent,10:N2}% │ {position.BrokerID}:{position.AccountID,-8} ║");
                totalGain += position.GainLoss;
            }

            Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
            var totalColor = totalGain >= 0 ? "+" : "";
            Console.WriteLine($"║ TOTAL GAIN/LOSS: {totalColor}{totalGain,10:N2}                                                                                                          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╝\n");
        }
    }

    private string GetPositionKey(string asset, string exchange, string accountId, int brokerId)
    {
        return $"{brokerId}:{accountId}:{asset}:{exchange}";
    }
}
