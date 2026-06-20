# NTBot

Plataforma SaaS de trading automatizado — .NET 9, Clean Architecture, PostgreSQL, Blazor.

[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-blue)](https://www.postgresql.org/)

## O que é

Sistema multi-tenant para trading com análise Wyckoff, contexto macro, estratégias quant (GEX), integração ProfitChart/NinjaTrader/MT5 e billing Stripe (em implementação).

## Quick start

```powershell
cd src
dotnet build NtBot.sln

cd NtBot.Api && dotnet run      # http://localhost:5053/swagger
cd ..\NtBot.Web && dotnet run   # http://localhost:5001
```

```powershell
Invoke-RestMethod http://localhost:5053/api/health
```

## Documentação

Toda a documentação está em **[docs/](docs/README.md)**:

- [Getting Started](docs/getting-started.md)
- [Arquitetura](docs/architecture/overview.md)
- [API REST + SignalR](docs/api/rest-and-signalr.md)
- [Roadmap](docs/roadmap/phases.md)
- [Status atual](docs/status/current.md)

## Estrutura

```
ntbot/
├── src/NtBot.sln           # Solution principal
├── src/NtBot.Api/          # API v3 (5053)
├── src/NtBot.Web/          # Blazor (5001)
├── ntbot-dashboard/        # React UI (transição)
├── Simulador/              # Backtest CSV
├── MT5/ / NinjaScript/     # Brokers
├── docker/
└── docs/
```

## Stack

- ASP.NET Core 9, EF Core, MediatR, SignalR
- Blazor Interactive Server + React (legado UI)
- PostgreSQL (`ntquant`)
- Docker, Coolify (deploy)

## Disclaimer

Software fornecido "como está". Trading envolve risco substancial. Não constitui aconselhamento financeiro. Teste sempre em conta demo.

**Versão API:** 3.0.0
