# Testes

## Unit tests

```powershell
dotnet test C:\Projetos\ntbot\tests\NtBot.UnitTests
```

Projeto: `tests/NtBot.UnitTests/` — xUnit, referencia `NtBot.Application`.

Exemplo existente: `GetHealthQueryTests`.

## Adicionar testes

1. Crie classe em `tests/NtBot.UnitTests/`
2. Mock de `NtBotDbContext` via InMemory ou Testcontainers (PostgreSQL) para integração futura
3. Handlers MediatR são bons candidatos a unit test puro

## Simulador (backtest CSV)

Pasta: `Simulador/`

Envia ordens para a API em execução:

```
POST http://localhost:5053/orders/next
```

```powershell
cd C:\Projetos\ntbot\Simulador
dotnet run
```

Requer `NtBot.Api` rodando na porta 5053.

## Swagger / manual

`http://localhost:5053/swagger` — teste de controllers.

## Health

```powershell
Invoke-RestMethod http://localhost:5053/api/health
```

## CI (futuro — Fase 14)

```yaml
# planejado
- dotnet build src/NtBot.sln
- dotnet test tests/NtBot.UnitTests
```
