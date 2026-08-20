# Data quality

Invalid bars are **not silently repaired**. The feature worker logs the error codes and skips persist.

| Check | Code |
|-------|------|
| empty symbol | `symbol_empty` |
| default timestamp | `timestamp_invalid` |
| price ≤ 0 | `price_invalid` |
| high < low | `high_lt_low` |
| volume < 0 | `volume_negative` |
| buy/sell < 0 | `side_volume_negative` |
| buy+sell > volume | `side_volume_exceeds_total` |

Concurrency: per-symbol dictionaries in the 15s aggregator; EF scopes per worker cycle. No static mutable market state.

## What is still unavailable

- ProfitDLL / DDE
- Time & sales tape
- Persisted DOM (MT5 Python has book SSE; C# connector does not consume it)
- True opening-auction indicative/equilibrium price from B3
- 15s history when the connector is offline (only 5m+ candles remain)
