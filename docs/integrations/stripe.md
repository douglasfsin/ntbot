# Stripe / Billing

**Status:** Fase 5 implementada — módulo `NtBot.Billing` + API + UI `/pricing`.

## Schema (PostgreSQL)

- `Plans` — catálogo (Free, Pro, Enterprise) com limites
- `Subscriptions` — ciclo por tenant, status Stripe
- `BillingHistories` — faturas
- `WebhookEvents` — idempotência de webhooks

## API

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/billing/config` | — | Stripe configurado + publishable key |
| GET | `/api/billing/plans` | — | Planos ativos |
| GET | `/api/billing/subscription` | JWT | Assinatura do tenant |
| POST | `/api/billing/checkout` | JWT | Cria Checkout Session Stripe |
| POST | `/api/billing/confirm` | JWT | Confirma sessão (`sessionId`) |
| POST | `/api/billing/cancel` | JWT | Cancela assinatura (fim do período) |
| POST | `/api/webhooks/stripe` | — | Webhook Stripe (raw body) |

### Checkout

Body `{ "planSlug": "pro" }` → retorna `{ checkoutUrl }` e cria `Subscription` com status `pending`.

### Webhooks tratados

- `checkout.session.completed` — ativa assinatura e plano do tenant
- `customer.subscription.created/updated/deleted` — sync status
- `invoice.paid` — grava `BillingHistory`
- `invoice.payment_failed` — suspende assinatura

Idempotência via `WebhookEvents.EventId`.

## Config

`appsettings.Development.json` / env vars Coolify:

```json
"Stripe": {
  "SecretKey": "sk_test_...",
  "PublishableKey": "pk_test_...",
  "WebhookSecret": "whsec_...",
  "BackUrl": "http://localhost:5001"
},
"Subscription": {
  "TrialDays": 7,
  "GracePeriodDays": 3
}
```

Env vars alternativas: `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_BACK_URL`.

Em Coolify use o padrão ASP.NET: `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__BackUrl` (URL pública do **NTBot.Web**).

## Webhook endpoint (produção)

Configure no Stripe Dashboard:

```
https://<api-host>/api/webhooks/stripe
```

Eventos: `checkout.session.completed`, `customer.subscription.*`, `invoice.paid`, `invoice.payment_failed`.

## UI

- `/pricing` — planos da API + botão Assinar (redirect Stripe)
- `/billing/success?session_id=...` — confirmação pós-checkout
- `/billing/cancel` — checkout cancelado

## Produção

Secrets via Coolify env vars, nunca em appsettings commitado.
