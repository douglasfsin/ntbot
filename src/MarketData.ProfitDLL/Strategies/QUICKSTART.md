# 🚀 QUICK START - Sistema de Estratégias

## ⚡ Começar em 5 minutos

### 1️⃣ Adicione no inicio do seu Program.cs

```csharp
using ProfitDLLClient.Strategies;
using ProfitDLLClient.Strategies.Examples;
```

### 2️⃣ Inicialize após conectar à DLL

Encontre onde você se conecta (depois de `DLLInitializeLogin` retornar sucesso):

```csharp
// Logo após conexão bem-sucedida
StrategyIntegration.Initialize();
StrategyIntegration.Start();
```

### 3️⃣ Adicione comandos ao menu

Encontre o `switch` que processa comandos e adicione:

```csharp
case "pos":
case "posicoes":
    StrategyIntegration.ShowPositions();
    break;

case "strategies":
case "estrategias":
    StrategyIntegration.ListStrategies();
    break;

case "testpos":
    // Testar com posições fake
    StrategyIntegration.AddTestPosition(
        "PETR4", "BOVESPA", "123456", 1,
        TConnectorOrderSide.Buy, 100, 38.50, 39.20
    );
    StrategyIntegration.AddTestPosition(
        "VALE3", "BOVESPA", "123456", 1,
        TConnectorOrderSide.Buy, 200, 68.20, 67.80
    );
    StrategyIntegration.ShowPositions();
    break;
```

### 4️⃣ Teste!

Compile e execute:
```bash
dotnet build
dotnet run
```

Digite no prompt:
- `testpos` → Cria posições de teste
- `pos` → Exibe posições
- `strategies` → Lista estratégias

## 📊 Você verá algo assim:

```
╔════════════════════════════════════════════════════════════════════════╗
║                        POSIÇÕES ABERTAS                                 ║
╠════════════════════════════════════════════════════════════════════════╣
║ Ativo  │ Lado  │ Qtd │ Médio   │ Atual   │ Gain/Loss │ Gain%         ║
╠════════════════════════════════════════════════════════════════════════╣
║ PETR4  │ COMPRA│ 100 │ R$ 38.50│ R$ 39.20│ +R$ 70.00 │ +1.82%        ║
║ VALE3  │ COMPRA│ 200 │ R$ 68.20│ R$ 67.80│ -R$ 80.00 │ -0.59%        ║
╠════════════════════════════════════════════════════════════════════════╣
║ TOTAL GAIN/LOSS: -R$ 10.00                                             ║
╚════════════════════════════════════════════════════════════════════════╝
```

## 🎯 Próximo Passo (Opcional)

Para integrar com dados reais, adicione nos seus callbacks:

### TradeCallback
```csharp
// No seu callback de trade existente
public static void SeuTradeCallback(/* params */)
{
    // ... seu código existente ...
    
    // ADICIONE:
    StrategyIntegration.OnTradeCallback(ticker, exchange, trade);
}
```

### OrderCallback
```csharp
// No seu callback de ordem existente
public static void SeuOrderCallback(TConnectorOrder order)
{
    // ... seu código existente ...
    
    // ADICIONE:
    StrategyIntegration.OnOrderCallback(order);
}
```

## ✅ Pronto!

Agora você tem:
- ✅ Sistema de rastreamento de posições
- ✅ Cálculo automático de gain/loss
- ✅ Framework para criar estratégias
- ✅ 2 estratégias de exemplo funcionando

## 📚 Quer mais?

- Ver `README.md` → Documentação completa
- Ver `VISUAL_GUIDE.txt` → Guia visual
- Ver `SUMMARY.md` → Resumo executivo
- Ver `Examples/` → Estratégias prontas

## 🆘 Problemas?

1. Não compila?
   - Verifique se todos os arquivos estão na pasta `Strategies/`
   - Rode `dotnet clean` e depois `dotnet build`

2. Callbacks não funcionam?
   - Certifique-se de chamar `StrategyIntegration.Initialize()` após conectar
   - Verifique os logs no console

3. Posições não aparecem?
   - Use `testpos` para criar posições de teste
   - Verifique se integrou os callbacks corretamente

---

**Tempo estimado:** 5 minutos para o básico funcionar! 🚀
