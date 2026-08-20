# Quantitative Statistical Engine

NtBot transforms existing market data into a historical statistical base that answers:

> "When similar conditions occurred, what happened next?"

It never claims a signal *will* work. It reports sample size, probability, confidence interval, expectancy, MFE and MAE.

## Architecture

```
Market data (Connector ticks + Candles in PostgreSQL)
        → FeatureEngine (no look-ahead)
        → signal_events (observation only; trading rules unchanged)
        → OutcomeEngine (async, 15s–60m)
        → StatisticalEngine (Wilson CI, expectancy, profit factor)
        → API / SigNoz
```

Project: `NtBot.Analytics`. Hosted in `NtBot.Api` via `AddQuantAnalytics`. Workers run in-process so they share the existing `DefaultConnection` PostgreSQL.

## Integration points (read-only)

| Source | Hook | Does not change |
|--------|------|-----------------|
| Live ticks | `ConnectorEventPublisher` → 15s bars | ingest / SignalR |
| OHLCV | `Candles` table (MT5 `OhlcvSyncWorker`) | candle upsert |
| QuantStrategy | `QuantStrategyController` records emitted signals | `QuantStrategy` rules |
| Opening | first M5 bar after configured session open | no auction feed yet |

## Engines

- `IFeatureEngine` — VWAP, ATR, delta, z-score, percentiles, regime, absorption, quant score
- `IOutcomeEngine` — directional return, MFE, MAE, R
- `IStatisticalEngine` — mean/median/std/percentiles, Wilson interval, expectancy, profit factor
- `IBacktestEngine` — interface stub only

## Configuration

Section `QuantStatistics` in `appsettings.json`. Session hours default to B3 (`America/Sao_Paulo` 09:00–18:25) and are overridable. `AssetConfiguration.TradingStartTime` is per-tenant and is **not** overwritten.

## API

All under `[Authorize]`:

| Method | Path |
|--------|------|
| GET | `/api/quant/statistics` |
| GET | `/api/quant/statistics/signals` |
| GET | `/api/quant/statistics/strategies` |
| GET | `/api/quant/statistics/regimes` |
| GET | `/api/quant/statistics/opening-auction` |
| GET | `/api/quant/statistics/time` |
| GET | `/api/quant/statistics/outcomes` |
| GET | `/api/quant/statistics/correlations` |
| GET | `/api/quant/probabilities` |
| GET | `/api/quant/ranking/strategies` |

Filters: `symbol`, `strategy`, `timeframe`, `direction`, `regime`, `session`, `from`, `to`, `horizon`, `deltaBucket`, `volumeZMin`, `bookImbalanceMin`, `priceVsVwap`, `trend`, `alignment`.

Insufficient samples return `sampleClass=INSUFFICIENT_SAMPLE` and hide `successProbability`.

## Real data sources

| Input | Source |
|-------|--------|
| OHLCV WIN/WDO | PostgreSQL `Candles` (MT5 `OhlcvSyncWorker`) |
| Live ticks | Windows connector `POST /api/connector/ingest` |
| Signals | `QuantStrategyController` after emit (rules unchanged) |

Unavailable: ProfitDLL/DDE, tape, persisted DOM, B3 indicative auction, 15s history without connector.

## Validation (WIN/WDO)

1. Ensure `ConnectionStrings:DefaultConnection` / `DATABASE_URL` already points at `ntquant`.
2. Deploy API so `Database.Migrate()` creates schema `quant`.
3. Confirm candles exist: `select symbol, timeframe, count(*) from "Candles" group by 1,2`.
4. Wait for `QuantFeatureWorker` (M5 + auction) then `GET /api/quant/statistics/opening-auction?symbol=WIN`.
5. Generate/analyze a signal, wait outcome windows, then `GET /api/quant/probabilities?symbol=WIN&deltaBucket=EXTREME_BUY&volumeZMin=1.5&bookImbalanceMin=0.7&priceVsVwap=ABOVE&trend=UP`.
