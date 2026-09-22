# 📊 Sistema de Estratégias e Monitoramento de Posições - ProfitDLL

## ✅ O que foi criado

### 📁 Estrutura de Pastas

```
c:\Users\dougl\Downloads\ProfitDLL\Exemplo C#\
└── Strategies/
    ├── IStrategy.cs                          # Interface base
    ├── BaseStrategy.cs                       # Classe abstrata base
    ├── StrategyManager.cs                    # Gerenciador principal
    ├── PositionInfo.cs                       # Gerenciamento de posições
    ├── StrategyIntegration.cs                # Integração com Program.cs
    ├── Examples/
    │   ├── SimpleMovingAverageStrategy.cs    # Exemplo: Média Móvel
    │   └── VolumeMonitorStrategy.cs          # Exemplo: Monitor de Volume
    └── README.md                             # Documentação completa
```

## 🎯 Funcionalidades Implementadas

### 1. Sistema de Posições (PositionInfo.cs & PositionManager)

✅ **Rastreamento Automático de Posições:**
- Ativo (ticker e exchange)
- Lado da operação (COMPRA/VENDA)
- Quantidade de lotes
- Preço médio de entrada
- Preço atual
- Valor total da posição
- Gain/Loss em R$
- Gain/Loss em %
- Conta e corretora

✅ **Cálculos Automáticos:**
```csharp
public double TotalValue => Quantity * AveragePrice;
public double CurrentValue => Quantity * CurrentPrice;
public double GainLoss => CurrentValue - TotalValue;
public double GainLossPercent => (GainLoss / TotalValue) * 100;
```

✅ **Exibição Formatada:**
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

### 2. Framework de Estratégias

✅ **Interface IStrategy:**
- Métodos padronizados para todos os callbacks
- Controle de estado (ativa/parada)
- Callbacks: OnTrade, OnOfferBookUpdate, OnPositionUpdate, OnOrderUpdate

✅ **BaseStrategy:**
- Implementação base com funcionalidades comuns
- Sistema de logging com timestamp automático
- Cache de últimos preços
- Integração automática com PositionManager

✅ **StrategyManager:**
- Gerenciamento centralizado de múltiplas estratégias
- Iniciar/parar estratégias individualmente ou em grupo
- Distribuição automática de eventos para todas as estratégias ativas
- Monitoramento periódico de posições
- Tratamento de erros por estratégia

### 3. Estratégias de Exemplo

✅ **SimpleMovingAverageStrategy:**
- Calcula média móvel simples (SMA)
- Detecta cruzamentos de preço
- Sinais de compra/venda (desativados por segurança)

✅ **VolumeMonitorStrategy:**
- Monitora volume por minuto
- Rastreia máximas e mínimas
- Alertas de picos de volume
- Estatísticas por ativo

### 4. Integração com Program.cs

✅ **StrategyIntegration.cs:**
- Classe helper para integração fácil
- Métodos prontos para callbacks
- Documentação completa de integração
- Exemplos de uso

## 🚀 Como Usar

### Passo 1: Inicializar o Sistema

No `Program.cs`, adicione após a conexão:

```csharp
StrategyIntegration.Initialize();
StrategyIntegration.Start();
```

### Passo 2: Adicionar Comandos no Menu

```csharp
case "pos":
case "posicoes":
    StrategyIntegration.ShowPositions();
    break;

case "strategies":
case "estrategias":
    StrategyIntegration.ListStrategies();
    break;
```

### Passo 3: Integrar com Callbacks Existentes

Ver instruções completas em `StrategyIntegration.cs`

### Passo 4: Testar

```csharp
// Adicionar posição de teste
StrategyIntegration.AddTestPosition(
    "PETR4", "BOVESPA", "123456", 1,
    TConnectorOrderSide.Buy, 100, 38.50, 39.20
);

// Exibir
StrategyIntegration.ShowPositions();
```

## 📊 Métodos do PositionManager

```csharp
// Atualizar posição
UpdatePosition(asset, exchange, accountId, brokerId, side, quantity, avgPrice, currentPrice);

// Atualizar preço
UpdatePrice(asset, exchange, price);

// Obter posições
GetOpenPositions();
GetPositionsByAsset(asset, exchange);
GetPositionsByAccount(accountId, brokerId);

// Calcular totais
GetTotalGainLoss();

// Exibir
PrintPositionsSummary();
```

## 🎓 Criando Sua Própria Estratégia

```csharp
public class MinhaEstrategia : BaseStrategy
{
    public override string Name => "Minha Estratégia";
    public override string Description => "Descrição aqui";

    public MinhaEstrategia(PositionManager positionManager) 
        : base(positionManager)
    {
    }

    public override void OnTrade(string ticker, string exchange, TConnectorTrade trade)
    {
        base.OnTrade(ticker, exchange, trade);
        
        // Sua lógica aqui
        if (ticker == "PETR4")
        {
            Log($"Trade PETR4: {trade.Price:N2}");
            
            // Verificar posições atuais
            var positions = PositionManager.GetPositionsByAsset(ticker, exchange);
            
            // Implementar sua estratégia
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        Log("Inicializando...");
    }

    public override void Stop()
    {
        base.Stop();
        Log("Parando...");
    }
}
```

## 📈 Métricas Disponíveis

Para cada posição:
- ✅ Quantidade de lotes
- ✅ Preço médio
- ✅ Preço atual
- ✅ Valor total investido
- ✅ Valor atual
- ✅ Gain/Loss absoluto (R$)
- ✅ Gain/Loss percentual (%)
- ✅ Tempo em posição
- ✅ Última atualização

Totais:
- ✅ Gain/Loss total de todas as posições
- ✅ Número de posições abertas
- ✅ Posições por ativo
- ✅ Posições por conta

## ⚠️ Avisos Importantes

1. **Testes**: Sempre teste suas estratégias em ambiente de homologação
2. **Risco**: Implemente controles de risco adequados
3. **Monitoramento**: Monitore constantemente o comportamento
4. **Logs**: Mantenha logs detalhados
5. **Backtesting**: Teste com dados históricos antes de produção

## 📁 Arquivos Criados

1. ✅ `Strategies/IStrategy.cs` - Interface base
2. ✅ `Strategies/BaseStrategy.cs` - Classe base abstrata
3. ✅ `Strategies/StrategyManager.cs` - Gerenciador
4. ✅ `Strategies/PositionInfo.cs` - Sistema de posições
5. ✅ `Strategies/StrategyIntegration.cs` - Helper de integração
6. ✅ `Strategies/Examples/SimpleMovingAverageStrategy.cs` - Exemplo SMA
7. ✅ `Strategies/Examples/VolumeMonitorStrategy.cs` - Exemplo Volume
8. ✅ `Strategies/README.md` - Documentação completa
9. ✅ `Strategies/SUMMARY.md` - Este arquivo

## 🔧 Compilação

O projeto compila com sucesso:
```bash
dotnet build
# Build succeeded in 5,7s
```

## 📚 Documentação

Consulte:
- `Strategies/README.md` - Documentação detalhada
- `Strategies/StrategyIntegration.cs` - Instruções de integração
- Exemplos em `Strategies/Examples/`

## 🎉 Pronto para Usar!

O sistema está completo e pronto para ser integrado ao seu `Program.cs`. 

Basta seguir as instruções em `StrategyIntegration.cs` para começar a usar!
