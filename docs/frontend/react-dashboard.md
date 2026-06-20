# React Dashboard (`ntbot-dashboard`)

UI legada em React 19 + Vite 7. **Manter até paridade com Blazor** (Fase 6).

## Run

```powershell
cd C:\Projetos\ntbot\ntbot-dashboard
npm install
npm run dev
# http://localhost:5173
```

## Backend

Api deve estar em `http://localhost:5053`:

```env
# ntbot-dashboard/.env
VITE_API_URL=http://localhost:5053
```

## Páginas

| Página | Status |
|--------|--------|
| Dashboard | ✅ |
| ProfitChart | ✅ SignalR + RTD |
| QuantStrategy | ✅ |
| Wyckoff, Macro, Signals, Trades | parcial |
| GridManager, Scalping, Risk, Positions | parcial |
| Settings | placeholder |

## Stack

- React 19, TypeScript, Vite 7
- Ant Design / componentes custom
- `@microsoft/signalr` para hubs

## Documentação local

- `ntbot-dashboard/INSTALL.md` — instalação
- `ntbot-dashboard/BACKEND_SETUP.md` — conectar à Api v3
- `ntbot-dashboard/PROFITCHART_DASHBOARD.md` — ProfitChart UI

## Depreciação

Quando `NtBot.Web` atingir paridade, este projeto será arquivado — não deletar antes disso.
