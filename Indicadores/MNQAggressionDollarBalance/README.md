# MNQAggressionDollarBalance

Indicador NinjaTrader 8 — **saldo de agressão por volume financeiro**.

## O que o saldo significa

| Conceito | Definição |
|----------|-----------|
| **Compra (agressão)** | Negócios `Last` no **Ask** → volume financeiro de compra |
| **Venda (agressão)** | Negócios `Last` no **Bid** → volume financeiro de venda |
| **Volume financeiro** | `preço × volume × multiplicador` (`PointValue` ou `DollarPerPoint`) |
| **Saldo** | Compra − Venda |

- **Saldo positivo** → mais dólares/pontos agressivos na compra (pressão compradora).
- **Saldo negativo** → mais agressão na venda (pressão vendedora).
- Com `ResetOnSession = true` (padrão), o saldo zera no início de cada sessão.
- Histórico **sem Tick Replay**: não inventa Bid/Ask — mostra **DATA QUALITY: LOW**.

## Plots

| Plot | Tipo | Significado |
|------|------|-------------|
| `SaldoAcumulado` | Linha | Saldo financeiro da sessão (ou cumulativo) |
| `SaldoBarra` | Histograma | Delta financeiro da barra atual |
| `Zero` | Linha | Referência neutra |

Cores: verde = pressão de compra; vermelho = pressão de venda.

## Propriedades

| Propriedade | Padrão | Descrição |
|-------------|--------|-----------|
| `Calculate` | OnPriceChange | Tape em `OnMarketData`; OnBarClose = mais leve; OnEachTick = mais pesado |
| `ResetOnSession` | true | Zera Compra/Venda/Saldo na sessão |
| `Lookback` | 20 | Barras do saldo rolante no dashboard |
| `ShowDashboard` | true | Painel Compra / Venda / Saldo (TextFixed com throttle) |
| `DashboardThrottleMs` | 250 | Intervalo mín. entre redesenhos do painel |
| `UsePointValue` | true | Usa `Instrument.MasterInstrument.PointValue` |
| `DollarPerPoint` | 0.50 | Fallback $/ponto (MNQ típico) se `UsePointValue=false` |
| `DebugMode` | false | Print no Output |

**Live MNQ:** mantenha `OnPriceChange` + throttle 250–500 ms. Se o gráfico ainda travar, `Calculate = On bar close` ou `ShowDashboard = false`.

## Estrutura

```
MNQAggressionDollarBalance/
  MNQAggressionDollarBalance.cs   ← principal (deploy flat)
  Engines/DollarAggressionEngine.cs
  deploy-to-nt8.ps1
  README.md
  README-INSTALACAO-NT8.md
```

Independente de `MNQInstitutionalFlowAnalyzer` — não altera aquele indicador.

## Instalação rápida

```powershell
cd C:\Projetos\ntbot\Indicadores\MNQAggressionDollarBalance
powershell -ExecutionPolicy Bypass -File .\deploy-to-nt8.ps1
```

Depois: fechar NT8 → reabrir → NinjaScript Editor → **F5** → gráfico MNQ → Indicators → `MNQAggressionDollarBalance`.

Guia completo: [README-INSTALACAO-NT8.md](README-INSTALACAO-NT8.md).
