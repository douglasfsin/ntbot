# Migrations EF Core

## Pré-requisitos

```powershell
dotnet tool install --global dotnet-ef
# ou atualizar: dotnet tool update --global dotnet-ef
```

Pacote design-time: `Microsoft.EntityFrameworkCore.Design` em `NtBot.Api`.

## Comandos

Sempre a partir de `src/NtBot.Api` com ambiente Development:

```powershell
cd C:\Projetos\ntbot\src\NtBot.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"

# Criar migration
dotnet ef migrations add NomeDaMigration `
  --project ..\NtBot.Infrastructure `
  --startup-project .

# Aplicar
dotnet ef database update `
  --project ..\NtBot.Infrastructure `
  --startup-project .

# Reverter última
dotnet ef migrations remove `
  --project ..\NtBot.Infrastructure `
  --startup-project .
```

## Design-time factory

`NtBot.Infrastructure/Persistence/NtBotDbContextFactory.cs` resolve connection string nesta ordem:

1. `appsettings.json` + `appsettings.{ASPNETCORE_ENVIRONMENT}.json` + `appsettings.Local.json` (pasta `NtBot.Api`)
2. `DATABASE_URL` / `ConnectionStrings__DefaultConnection`

**Importante:** defina `ASPNETCORE_ENVIRONMENT=Development` antes de rodar `dotnet ef`, senão pode cair em SQLite fallback.

## Migrations existentes

| Migration | Descrição |
|-----------|-----------|
| `InitialCreate` | Schema trading completo |
| `AddBillingTables` | Plans, Subscriptions, BillingHistory, WebhookEvents |

## Auto-migrate na startup

`Program.cs` da Api aplica migrations pendentes ao iniciar (Development/Production).

## Produção

Preferir pipeline CI/CD:

```powershell
dotnet ef database update --project NtBot.Infrastructure --startup-project NtBot.Api
```

Com `ASPNETCORE_ENVIRONMENT=Production` e secrets via Coolify.
