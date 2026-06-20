# Blazor Web (`NtBot.Web`)

Frontend alvo da plataforma — Blazor Interactive Server (.NET 9).

## Run

```powershell
cd C:\Projetos\ntbot\src\NtBot.Web
dotnet run
# http://localhost:5001
```

## Rotas

| Rota | Status | Descrição |
|------|--------|-----------|
| `/` | ✅ | Landing SSR |
| `/pricing` | ✅ | Planos |
| `/app` | ✅ | Dashboard (health da Api) |
| `/app/scalping` | stub | Scalping panel |
| `/app/grid` | stub | Grid manager |
| `/app/quant` | stub | Quant strategy |
| `/app/profitchart` | stub | ProfitChart |
| `/app/*` | stub | Demais módulos |

## Layouts

- `MainLayout.razor` — páginas públicas
- `AppLayout.razor` — sidebar dashboard (dark theme)

## Design system

`wwwroot/css/design-system.css` — tokens de cor, tipografia, componentes base.

## Api client

HttpClient configurado para `http://localhost:5053` (Development).

## Migração React → Blazor (Fase 6)

Prioridade de port:

1. ProfitChart + SignalR
2. Quant strategy
3. Grid / Scalping / Risk
4. Settings / auth (após Fase 4)

Referência UI funcional: [react-dashboard.md](react-dashboard.md)

## Deploy

Container: `docker/Dockerfile.Web` — porta 8080 interna, 5001 mapeada no compose.
