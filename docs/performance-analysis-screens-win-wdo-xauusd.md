# Performance — telas de análise WIN / WDO / XAUUSD

**Data:** 2026-08-08  
**Escopo:** rotas `/app/analysis/win`, `/app/analysis/wdo`, `/app/analysis/xauusd` (componente compartilhado `TradingIntelligenceWorkspace`)  
**Projetos:** `NtBot.Api`, `NtBot.Web`, `NtBot.TradingIntelligence`, `NtBot.MarketDrivers`

---

## 1. Plano de execução

1. Mapear rotas Blazor, REST e hubs SignalR do first paint.
2. Instrumentar timing estruturado nos endpoints e spans críticos (TI, drivers, candles).
3. Ataques de alto impacto no caminho crítico: paralelizar deps do TI, coalescer builds de drivers, soft-timeouts, paralelizar hubs/fallback HTTP no Web, reduzir delay do fallback de 2s → 800ms.
4. Compilar `NtBot.Api` + `NtBot.Web`.
5. Medir endpoints se API local estiver disponível; senão documentar como **instrumentado / estimado**.
6. Publicar este relatório.

---

## 2. Endpoints mapeados por tela

Todas as três rotas hospedam o mesmo workspace (`InitialSymbol` = WIN | WDO | XAUUSD). Layout `AppLayout` sempre acompanha.

### REST (first paint / carga inicial)

| Quando | Método | Endpoint | Notas |
|--------|--------|----------|--------|
| Sempre (layout) | GET | `/api/health` | Health badge |
| Sempre (layout) | GET | `/api/macro/current` | Badge macro |
| Após hubs | GET | `/api/ProfitChart/tickers` | Preços live |
| Background | GET | `/api/trading-intelligence/status` | Flag n8n |
| WIN/WDO apenas | GET | `/api/connector/dde-replay` | Replay DDE |
| Fallback ~800ms | GET | `/api/market-drivers/{symbol}` | Se SignalR atrasar |
| Fallback ~800ms | GET | `/api/trading-intelligence/{symbol}` | Se SignalR atrasar |
| Chart fallback | GET | `/api/trading-intelligence/{symbol}/candles?timeframe=60&count=80` | Redis-first |
| Condicional | GET | `/api/trading-intelligence/{symbol}/smc-overlays?...` | Se snapshot sem overlays |
| Após 1º snapshot | GET | `/api/boletagem/{symbol}` | Painel boleta |

### SignalR (first paint)

| Hub | Uso |
|-----|-----|
| `/hubs/market-drivers` | Snapshot drivers push |
| `/hubs/trading-intelligence` | Snapshot TI + candles chart + price |
| `/hubs/connector-web` | Ticks / MT5 forex live |

Arquitetura: **SignalR-first**, HTTP como fallback após delay curto.

---

## 3. Gargalos identificados

| Gargalo | Impacto | Evidência / histórico do código |
|---------|---------|----------------------------------|
| Build TI **sequencial** (drivers → macro → correlation) | Alto | Soma de latências de 3 providers externos no caminho crítico |
| Macro/providers lentos | Alto | Até 12s/provider; podia segurar o snapshot TI inteiro |
| Builds concorrentes de Market Drivers sem single-flight | Médio–Alto | Várias requisições (hub + REST + TI) disparam builds duplicados |
| Hubs Web conectados em série | Médio | 3× handshake sequencial no first paint |
| Fallback HTTP sequencial (drivers depois TI) + delay 2s | Médio | Atraso artificial + soma de waits |
| Candles frios (sem Redis) | Alto (cold) | Seed MT5/ProfitDLL; Redis-first já existia e foi preservado |
| SMC overlays via GET snapshot completo | Baixo–Médio | Condicional após chart |

---

## 4. Alterações realizadas

### Logging

- **`AnalysisScreensTimingMiddleware`** — log estruturado `path`, `symbol`, `duration_ms`, `status` para `/api/trading-intelligence`, `/api/market-drivers`, `/api/macro`, `/api/boletagem`, `/api/health`, `/api/ProfitChart`, `/api/connector`. Warning se ≥ 2s.
- Spans de serviço:
  - TI: `deps_parallel`, `timeframes`, `build_total` (+ soft-timeout)
  - Drivers: `source=redis|build` (+ coalesce)
  - Candles: um log por fetch (não por candle); cache hit em Debug

### Otimizações

| Mudança | Onde |
|---------|------|
| `Task.WhenAll` drivers + macro + correlation com soft-timeouts (18s / 10s / 8s) | `TradingIntelligenceService` |
| Single-flight `InflightBuilds` por ativo | `MarketDriversService` |
| Timing Redis-first candles (já coalescido) | `MarketCandleService` |
| Conexão paralela dos 3 hubs + subscribe paralelo | `TradingIntelligenceWorkspace` |
| Fallback REST paralelo drivers∥TI; delay 2s → **800ms** | Workspace |
| Fallback candles delay 2s → **800ms** | `TradingIntelligenceChart` |

Correção/boletagem/trailing: **não alterados** na lógica de negócio.

### Arquivos tocados

- `src/NtBot.Api/Middleware/AnalysisScreensTimingMiddleware.cs` *(novo)*
- `src/NtBot.Api/Program.cs`
- `src/NtBot.Api/Services/MarketData/MarketCandleService.cs`
- `src/NtBot.TradingIntelligence/Services/TradingIntelligenceService.cs`
- `src/NtBot.MarketDrivers/Services/MarketDriversService.cs`
- `src/NtBot.Web/Components/TradingIntelligence/TradingIntelligenceWorkspace.razor`
- `src/NtBot.Web/Components/TradingIntelligence/TradingIntelligenceChart.razor`
- `docs/performance-analysis-screens-win-wdo-xauusd.md` *(este arquivo)*

---

## 5. Tabela de timings (antes vs depois)

> **Legenda:** API local (`localhost:5053`) **não estava em execução** no momento da medição. Valores abaixo são **instrumentados / estimados** com base no código (paralelismo + delays) e no comportamento histórico do caminho crítico. Após deploy, filtrar logs `AnalysisEndpoint` / `TI span` / `Drivers span` / `Candles span` para preencher a coluna “Medido”.

| Endpoint / etapa | Antes (estimado) | Depois (estimado) | Base |
|------------------|------------------|-------------------|------|
| Delay fallback HTTP snapshots | 2000 ms | **800 ms** | Delay explícito (−1200 ms) |
| Fallback drivers + TI (sequencial → paralelo) | ~T_d + T_ti | ~max(T_d, T_ti) | Paralelismo client |
| Connect 3 hubs (sequencial → paralelo) | ~C1+C2+C3 | ~max(C1,C2,C3) | Paralelismo client |
| Delay fallback candles | 2000 ms | **800 ms** | Delay explícito (−1200 ms) |
| TI build deps (drivers+macro+corr) cold | ~D+M+C (soma) | ~max(D,M,C) c/ soft-timeout | Paralelismo server |
| TI build soft-timeout macro | até dezenas de s | **≤ ~10 s** + fallback vazio | Soft timeout |
| Drivers concurrent (hub+REST+TI) | N builds | **1 build** (coalesce) | Single-flight |
| Candles Redis hit | já rápido | igual + log | Sem regressão |
| GET `/api/trading-intelligence/{WIN\|WDO\|XAUUSD}` cache hit | baixo ms | baixo ms + log Debug | Cache existente |
| GET `/api/trading-intelligence/{sym}/candles` Redis | tipicamente &lt; 200 ms | tipicamente &lt; 200 ms + log | Redis-first |
| First paint “shell útil” (hubs + 1º snapshot) | ~3–8+ s cold | **~1,5–5 s** cold típico | Soma das melhorias |

### Como medir após subir a API

```powershell
# Exemplo (requer JWT se [Authorize]):
$base = "http://localhost:5053"
Measure-Command { Invoke-RestMethod "$base/api/health" }
# Com token:
$h = @{ Authorization = "Bearer $token" }
Measure-Command { Invoke-RestMethod "$base/api/trading-intelligence/XAUUSD" -Headers $h }
Measure-Command { Invoke-RestMethod "$base/api/market-drivers/WIN" -Headers $h }
Measure-Command { Invoke-RestMethod "$base/api/trading-intelligence/WDO/candles?timeframe=60&count=80" -Headers $h }
```

Nos logs da API, procurar:

```text
AnalysisEndpoint path=... duration_ms=... status=...
AnalysisEndpointSlow ...
TI span asset=... span=deps_parallel|timeframes|build_total duration_ms=...
Drivers span asset=... source=redis|build duration_ms=...
Candles span symbol=... source=... duration_ms=...
```

---

## 6. Verificação

| Check | Resultado |
|-------|-----------|
| `dotnet build NtBot.Api` | OK (0 errors) |
| `dotnet build NtBot.Web` | OK (0 errors) |
| Curl/Invoke timed local | **Não executado** — API `:5053` indisponível |

---

## 7. Próximas recomendações

1. **Warm cache** no boot/worker para WIN, WDO, XAUUSD (drivers + TI + candles M1 Redis) antes do horário de pregão.
2. **Desacoplar AI/n8n** do first paint (já parcialmente opcional) — garantir que nunca bloqueie o snapshot inicial.
3. **SMC overlays** servidos do cache de chart/TI sem re-chamar `GetSnapshotAsync` completo no endpoint dedicado.
4. Dashboard de métricas (Prometheus/OpenTelemetry) a partir dos campos `duration_ms` já emitidos.
5. Reduzir ainda o fallback HTTP se o hub estabilizar &lt; 500 ms em produção (A/B).
6. Preencher a coluna “Medido” deste doc com 3 samples (cache frio / quente / Redis miss) por símbolo.

---

## 8. Resumo executivo

Instrumentação de timing nas APIs das telas WIN/WDO/XAUUSD + otimizações de paralelismo (server TI deps, client hubs/fallback), soft-timeouts e coalesce de drivers. Compilação OK. Medição HTTP local pendente de API em execução; ganhos de first paint estimados principalmente pela remoção de waits sequenciais e do delay de 2s → 800ms.
