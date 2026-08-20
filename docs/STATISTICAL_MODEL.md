# Statistical Model

## Outcome (BUY)

`return = future_price − signal_price`

SELL inverts. Also stored: points, percent, R (`return / |entry−stop|`).

## MFE / MAE

BUY MFE = max(high) − entry; MAE = min(low) − entry (≤ 0).  
SELL MFE = entry − min(low); MAE = entry − max(high) (≤ 0).

Horizons: 15s, 30s, 1m, 5m, 15m, 30m, 60m. Missing horizon stays null until data exists (5m candles cannot fill 15s).

## Probability

`P(return_horizon > 0 | filters)` with **Wilson score interval** at `ConfidenceLevel` (default 0.95).

Not shown as reliable when `SampleCount < MinimumSampleSize` (default 30):

| N | Class |
|---|--------|
| < 30 | INSUFFICIENT_SAMPLE |
| 30–99 | LOW_SAMPLE |
| 100–499 | MEDIUM_SAMPLE |
| ≥ 500 | HIGH_SAMPLE |

## Expectancy / profit factor

`Expectancy = P(win)×AvgWin − P(loss)×|AvgLoss|`  
`ProfitFactor = GrossProfit / GrossLoss`

Ranking default: expectancy, then sample size — not win rate alone.

## Buckets

Delta z: EXTREME_SELL … EXTREME_BUY  
Volume z: `< -2` … `> 3`  
Book: 0.00–0.20 … 0.80–1.00  
Volatility percentile: VERY_LOW … VERY_HIGH
