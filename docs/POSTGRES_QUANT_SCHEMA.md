# PostgreSQL schema `quant`

Uses the existing `ConnectionStrings:DefaultConnection` / `DATABASE_URL`. No new database, user, or password.

On PostgreSQL tables live in schema **`quant`**. On SQLite/SQL Server fallback they are prefixed `quant_`.

## Tables

| Table | Unique key |
|-------|------------|
| `market_features` | (symbol, timeframe, timestamp) |
| `opening_auction` | (symbol, date) |
| `opening_range` | (symbol, date, range_window) |
| `signal_events` | id (+ trace_id index) |
| `signal_outcomes` | signal_id |
| `asset_correlations` | (symbol_a, symbol_b, window, timestamp) |

Indexes: symbol+timestamp, symbol+timeframe+timestamp, regime+timestamp, session+timestamp, feature_id.

BRIN on `market_features.timestamp` is created by the migration for PostgreSQL. Partitioning is **not** enabled yet (volume not measured).

Materialized views (PostgreSQL, refreshed by aggregation job SQL when present):

- `quant.mv_signal_statistics`
- `quant.mv_opening_statistics`
- `quant.mv_strategy_performance`
- `quant.mv_regime_statistics`
- `quant.mv_hourly_statistics`

Retention: `EnableRetentionJob` defaults **false**. Raw feature retention months is configurable only.

## Migration

```bash
cd src/NtBot.Api
export ASPNETCORE_ENVIRONMENT=Development
dotnet ef migrations add AddQuantStatisticalSchema \
  --project ../NtBot.Infrastructure \
  --startup-project .
```

Api already calls `Database.Migrate()` on startup.
