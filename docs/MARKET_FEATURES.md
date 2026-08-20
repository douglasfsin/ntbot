# Market Features

Persisted in `quant.market_features` (SQLite fallback: `quant_market_features`).

## Resolutions

| Timeframe | Source | Notes |
|-----------|--------|--------|
| 15s | Live ticks (`LiveBarSeconds`) | OHLC from ticks; volume 0 on Profit RTD |
| 1m / 5m / 15m / 60m / 1d | `public.Candles` | Primary path today is **M5** from MT5 |
| Order flow columns | Candle.BuyVolume/SellVolume or tick rule | Null when unavailable — never invented as fact |

15-second bars are stored when the Windows connector is ingesting. They are **not** synthesized from 5m candles.

## Formulas

- **Delta** = buy − sell. If missing, signed candle body × volume (proxy, `Source` still `candles`).
- **VWAP** = Σ(typical × volume) / Σ(volume), typical = (H+L+C)/3, using only bars `<= as-of`.
- **ATR** = average true range (14).
- **Z-score** = (x − mean) / std of the configured window (`ZScoreWindow`).
- **Book imbalance** = bidVolume / (bid+ask) when book sizes exist.
- **Absorption** = high volume z + high |aggression z| + low range/ATR → `POSSIBLE_BUY_ABSORPTION` / `POSSIBLE_SELL_ABSORPTION`.
- **Quant score** (−100..+100) = weighted components stored individually.

## Look-ahead

`LookAheadGuard` rejects any feature bar with timestamp after the as-of instant. Outcomes use only prices **after** the signal.
