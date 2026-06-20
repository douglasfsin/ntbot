# Stripe / Billing

**Status:** entidades e schema prontos; serviços em `NtBot.Billing` ainda stub (Fase 5).

## Schema (PostgreSQL)

- `Plans` — catálogo (Free, Pro, Enterprise) com limites
- `Subscriptions` — ciclo por tenant, status Stripe
- `BillingHistories` — faturas
- `WebhookEvents` — idempotência de webhooks

## Config (dev)

`src/NtBot.Api/appsettings.Development.json`:

```json
"Stripe": {
  "SecretKey": "sk_test_...",
  "PublishableKey": "pk_test_...",
  "WebhookSecret": "...",
  "BackUrl": "http://localhost:5001"
},
"Subscription": {
  "TrialDays": 7,
  "GracePeriodDays": 3
}
```

## Implementação planejada (port BarberAI)

Referência: `C:\Projetos\barberai\src\BarberAI.Infrastructure\Services\StripeService.cs`

1. Checkout Session → cria `Subscription` pending
2. Webhook `checkout.session.completed` → ativa tenant
3. Webhook `invoice.paid` / `customer.subscription.updated` → sync status
4. Registrar eventos em `WebhookEvents` (dedupe por `EventId`)

## UI

- `/pricing` em `NtBot.Web` — landing de planos
- Checkout redirect Stripe → `BackUrl` após pagamento

## Produção

Secrets via Coolify env vars, nunca em appsettings commitado.
