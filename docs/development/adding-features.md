# Guia: adicionar uma nova feature

Checklist para manter Clean Architecture e facilitar review.

## 1. Definir escopo

- Qual bounded context? (Trading, Billing, Identity, MarketData)
- Precisa de nova entidade? Nova API? Apenas UI?

## 2. Domain

```
src/NtBot.Domain/Entities/NovaEntidade.cs
```

- Propriedades, enums, métodos de domínio puros
- Sem referência a EF, HTTP ou Stripe

## 3. Application (preferido para lógica nova)

```
src/NtBot.Application/
├── Commands/NovaFeature/
│   ├── NovaFeatureCommand.cs
│   ├── NovaFeatureHandler.cs
│   └── NovaFeatureValidator.cs
└── Queries/...
```

Registre no `DependencyInjection.cs` de Application (MediatR assembly scan).

## 4. Infrastructure

- Adicionar `DbSet<>` em `NtBotDbContext`
- Configurar `OnModelCreating`
- Nova migration (ver [migrations.md](migrations.md))

## 5. Api

```csharp
[ApiController]
[Route("api/[controller]")]
public class NovaFeatureController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Request dto, IMediator mediator)
        => Ok(await mediator.Send(new NovaFeatureCommand(...)));
}
```

SignalR: novo hub em `Hubs/` + `MapHub` em `Program.cs`.

## 6. Web / Dashboard

- Blazor: `src/NtBot.Web/Components/Pages/App/`
- React (temporário): `ntbot-dashboard/src/pages/`

## 7. Multi-tenant

Se a entidade é por tenant:

- Adicionar `TenantId` + implementar `ITenantEntity` em Shared
- Planejar query filter global (quando Fase 3 completar)

## 8. Documentar

Atualize:

- `docs/api/rest-and-signalr.md` se novo endpoint
- `docs/status/current.md` se feature de fase concluída

## Onde NÃO colocar código

| Evitar | Preferir |
|--------|----------|
| Lógica de negócio em Controller | MediatR handler |
| Entidades duplicadas em Api | `NtBot.Domain` |
| DbContext na Api | `NtBot.Infrastructure` |
| Secrets em appsettings commitado | Local.json / env vars |

## Extrair módulos (fases futuras)

Quando `NtBot.Billing` ou `NtBot.Identity` forem implementados, mova services e controllers para o projeto de módulo e referencie na Api via extension methods `AddBilling()`, `AddIdentity()`.
