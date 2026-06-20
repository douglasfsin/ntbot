# Getting Started

## Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Acesso ao PostgreSQL `ntquant` (dev compartilhado com BarberAI)
- Node.js 20+ (opcional — apenas para `ntbot-dashboard`)
- ProfitChart + DLL COM (opcional — apenas integração RTD Brasil)

## Primeiro run (5 minutos)

```powershell
# 1. Clonar e entrar na solution
cd C:\Projetos\ntbot\src

# 2. Build
dotnet build NtBot.sln

# 3. API (usa appsettings.Development.json → banco ntquant)
cd NtBot.Api
dotnet run
# → http://localhost:5053/swagger

# 4. Web Blazor (outro terminal)
cd ..\NtBot.Web
dotnet run
# → http://localhost:5001
```

## Verificar saúde

```powershell
Invoke-RestMethod http://localhost:5053/api/health
```

Resposta esperada: `"database": "connected"`, `"version": "3.0.0"`.

## Configuração local opcional

Para sobrescrever secrets sem commitar:

```powershell
cd src\NtBot.Api
copy appsettings.Local.json.example appsettings.Local.json
# Edite ConnectionStrings, Jwt, Stripe
```

Prioridade de config: `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json` → variáveis de ambiente (`DATABASE_URL`, `JWT_SECRET`).

## Frontends disponíveis

| App | Porta | Stack | Status |
|-----|-------|-------|--------|
| `NtBot.Web` | 5001 | Blazor Interactive Server | Landing + dashboard shell |
| `ntbot-dashboard` | 5173 | React 19 + Vite | Telas completas (ProfitChart, Quant) |

```powershell
# React dashboard (opcional)
cd C:\Projetos\ntbot\ntbot-dashboard
npm install
npm run dev
# VITE_API_URL=http://localhost:5053 (padrão no .env)
```

## Testes

```powershell
dotnet test C:\Projetos\ntbot\tests\NtBot.UnitTests
```

## Próximos passos

- [Setup local detalhado](development/local-setup.md)
- [Migrations](development/migrations.md)
- [Guia de nova feature](development/adding-features.md)
