# 📊 Sistema de Estratégias de Trading - ProfitDLL

## 📁 Estrutura

```
Strategies/
├── IStrategy.cs                      # Interface base para estratégias
├── BaseStrategy.cs                   # Classe abstrata base
├── StrategyManager.cs                # Gerenciador de estratégias
├── PositionInfo.cs                   # Gerenciamento de posições
├── Examples/
│   ├── SimpleMovingAverageStrategy.cs  # Exemplo: Média Móvel
│   └── VolumeMonitorStrategy.cs        # Exemplo: Monitor de Volume
└── README.md                         # Este arquivo
```

## 🚀 Como Usar

### 1. Criar uma Nova Estratégia

```csharp
public class MinhaEstrategia : BaseStrategy
{
    public override string Name => "Minha Estratégia";
    public override string Description => "Descrição da estratégia";

    public MinhaEstrategia(PositionManager positionManager) 
        : base(positionManager)
    {
    }

    public override void OnTrade(TConnectorTrade trade)
    {
        base.OnTrade(trade);
        
        // Sua lógica aqui
        if (trade.AssetID.Ticker == "PETR4")
        {
            Log($"Novo trade: {trade.Price:N2}");
        }
    }

    public override void OnOrderUpdate(TConnectorOrder order)
    {
        base.OnOrderUpdate(order);
        
        // Processar atualizações de ordens
        Log($"Ordem atualizada: {order.OrderID}");
    }
}
```

### 2. Registrar e Usar no Program.cs

```csharp
// Criar o gerenciador
var strategyManager = new StrategyManager();

// Registrar estratégias
strategyManager.RegisterStrategy(new SimpleMovingAverageStrategy(
    strategyManager.PositionManager, 
    "PETR4", 
    "BOVESPA", 
    20
));

strategyManager.RegisterStrategy(new VolumeMonitorStrategy(
    strategyManager.PositionManager, 
    1000
));

// Iniciar todas
strategyManager.StartAll();

// Processar eventos
strategyManager.ProcessTrade(trade);
strategyManager.ProcessOrderUpdate(order);

// Exibir posições
strategyManager.ShowPositions();
```

## 📊 Sistema de Posições

O `PositionManager` rastreia automaticamente:

- ✅ Quantidade de lotes
- ✅ Preço médio de entrada
- ✅ Preço atual
- ✅ Gain/Loss em R$
- ✅ Gain/Loss em %
- ✅ Valor total da posição

### Exemplo de Exibição:

```
╔════════════════════════════════════════════════════════════════════════╗
║                        POSIÇÕES ABERTAS                                 ║
╠════════════════════════════════════════════════════════════════════════╣
║ Ativo    │ Lado   │ Qtd  │ Médio    │ Atual    │ Gain/Loss │ Gain/Loss%║
╠════════════════════════════════════════════════════════════════════════╣
║ PETR4    │ COMPRA │  100 │ R$ 38.50 │ R$ 39.20 │ +R$ 70.00 │ +1.82%    ║
║ VALE3    │ COMPRA │  200 │ R$ 68.20 │ R$ 67.80 │ -R$ 80.00 │ -0.59%    ║
╠════════════════════════════════════════════════════════════════════════╣
║ TOTAL GAIN/LOSS: -R$ 10.00                                             ║
╚════════════════════════════════════════════════════════════════════════╝
```

## 🎯 Callbacks Disponíveis

### OnTrade
Executado a cada novo trade no mercado.
```csharp
public override void OnTrade(TConnectorTrade trade)
{
    // Processar novo trade
}
```

### OnOfferBookUpdate
Executado quando o book de ofertas é atualizado.
```csharp
public override void OnOfferBookUpdate(TAssetID assetId, int side, double price, int quantity)
{
    // Processar atualização do book
}
```

### OnPositionUpdate
Executado quando uma posição é alterada.
```csharp
public override void OnPositionUpdate(TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId)
{
    // Processar mudança de posição
}
```

### OnOrderUpdate
Executado quando uma ordem é atualizada.
```csharp
public override void OnOrderUpdate(TConnectorOrder order)
{
    // Processar atualização de ordem
}
```

## 🛠️ Métodos Úteis da BaseStrategy

### Log
```csharp
Log("Mensagem de log com timestamp automático");
```

### GetLastPrice
```csharp
double preco = GetLastPrice("PETR4", "BOVESPA");
```

### PositionManager
```csharp
// Atualizar posição
PositionManager.UpdatePosition("PETR4", "BOVESPA", accountId, brokerId, 
                               TConnectorOrderSide.Buy, 100, 38.50, 39.20);

// Obter posições
var positions = PositionManager.GetOpenPositions();
var petr4Positions = PositionManager.GetPositionsByAsset("PETR4", "BOVESPA");

// Calcular gain/loss total
double totalGain = PositionManager.GetTotalGainLoss();

// Exibir resumo
PositionManager.PrintPositionsSummary();
```

## ⚠️ Avisos Importantes

1. **Backtesting**: Sempre teste suas estratégias com dados históricos antes de usar em produção
2. **Gerenciamento de Risco**: Implemente stop-loss e limites de exposição
3. **Monitoramento**: Sempre monitore o comportamento das estratégias em tempo real
4. **Logs**: Mantenha logs detalhados de todas as operações
5. **Tratamento de Erros**: Use try-catch para evitar crashes

## 📚 Exemplos Incluídos

### SimpleMovingAverageStrategy
Calcula média móvel simples e detecta cruzamentos.

### VolumeMonitorStrategy
Monitora volume por minuto e alerta sobre picos.

## 🔧 Comandos do StrategyManager

```csharp
// Listar estratégias
strategyManager.ListStrategies();

// Iniciar/Parar específica
strategyManager.StartStrategy("SMA Strategy (PETR4)");
strategyManager.StopStrategy("Volume Monitor");

// Iniciar/Parar todas
strategyManager.StartAll();
strategyManager.StopAll();

// Exibir posições
strategyManager.ShowPositions();
```

## 🎓 Próximos Passos

1. Criar suas próprias estratégias herdando de `BaseStrategy`
2. Implementar lógica de entrada/saída de posições
3. Adicionar gerenciamento de risco
4. Testar exaustivamente antes de usar com dinheiro real
5. Monitorar e ajustar parâmetros conforme necessário

## 📞 Suporte

Para dúvidas sobre a API ProfitDLL, consulte o manual em `dlls/doc/`.
