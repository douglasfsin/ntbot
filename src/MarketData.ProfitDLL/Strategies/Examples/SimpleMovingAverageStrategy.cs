namespace ProfitDLLClient.Strategies.Examples;

/// <summary>
/// Estratégia de exemplo baseada em Média Móvel Simples
/// Esta é apenas uma demonstração - NÃO usar em produção sem testes adequados
/// </summary>
public class SimpleMovingAverageStrategy : BaseStrategy
{
    private readonly string _asset;
    private readonly string _exchange;
    private readonly int _period;
    private readonly Queue<double> _prices = new();

    public override string Name => $"SMA Strategy ({_asset})";
    public override string Description => $"Média Móvel Simples de {_period} períodos para {_asset}";

    public SimpleMovingAverageStrategy(PositionManager positionManager, string asset, string exchange, int period = 20)
        : base(positionManager)
    {
        _asset = asset;
        _exchange = exchange;
        _period = period;
    }

    public override void OnTrade(string ticker, string exchange, TConnectorTrade trade)
    {
        base.OnTrade(ticker, exchange, trade);

        // Só processa trades do ativo configurado
        if (ticker != _asset || exchange != _exchange)
            return;

        // Adiciona o preço à fila
        _prices.Enqueue(trade.Price);

        // Mantém apenas os últimos N preços
        while (_prices.Count > _period)
        {
            _prices.Dequeue();
        }

        // Calcula a média móvel quando tivermos dados suficientes
        if (_prices.Count == _period)
        {
            var sma = _prices.Average();
            var currentPrice = trade.Price;

            Log($"Preço: {currentPrice:N2} | SMA({_period}): {sma:N2} | Diferença: {(currentPrice - sma):N2}");

            // Exemplo de lógica de trading (DESATIVADA por segurança)
            // if (currentPrice > sma * 1.01) // Preço 1% acima da média
            // {
            //     Log("SINAL DE COMPRA detectado");
            // }
            // else if (currentPrice < sma * 0.99) // Preço 1% abaixo da média
            // {
            //     Log("SINAL DE VENDA detectado");
            // }
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        _prices.Clear();
        Log($"Configurada para monitorar {_asset}:{_exchange} com período de {_period}");
    }

    public override void Stop()
    {
        base.Stop();
        _prices.Clear();
    }
}
