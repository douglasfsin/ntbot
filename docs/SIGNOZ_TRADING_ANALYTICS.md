# SigNoz — Trading analytics

Statistical history stays in PostgreSQL. SigNoz receives **aggregates and events**, not every tick.

## Metrics (`NtBot.Quant`)

- `quant.features.persisted`
- `quant.signals.persisted`
- `quant.outcomes.persisted`
- `quant.statistics.calculation_count`
- `quant.calculation.errors` / `quant.database.errors`
- histograms: `quant.feature|outcome|statistics.processing_latency`
- `market.volume` / `market.delta` / `market.vwap` / `market.volatility` / `market.book_imbalance` (bar close only)
- `auction.score`, `signal.score`, `signal.count`

Traces: `NtBot.Quant` (`FeatureCalculation`, `OutcomeCalculation`, `SignalGeneration`, `StatisticalAggregation`).

## Log events

`SignalGenerated`, `AuctionEnded`, `OpeningDriveDetected` (structured properties).

## Dashboards

`scripts/signoz/provision_observability.py --trading` creates:

- Trading — Market Overview (quant metric/log volume)
- Trading — Opening Auction
- Trading — Order Flow
- Trading — Statistical Edge
- Trading — Strategy Statistics
- Trading — Market Regimes
- Trading — Time Statistics
- Trading — Signal Probability
- Trading — Market Data Health

Filters use `service.namespace = 'NtBot'` plus message templates. Detailed probability tables are served by `/api/quant/probabilities` (SigNoz is the operational overlay).

## Alerts (log-based)

Provisioned as explorer views: calculation errors, database errors, feature worker warnings.
