# Auth API

Base: `/api/auth` (público, exceto `/me`).

## Fluxos

### Registro (2 etapas)

1. `POST /api/auth/register/init` — cria tenant + usuário pendente e envia OTP por email  
2. `POST /api/auth/register/verify` — valida OTP e retorna JWT

### Login

`POST /api/auth/login` — email + senha → JWT

### Recuperação de senha

1. `POST /api/auth/forgot-password` — envia OTP  
2. `POST /api/auth/reset-password` — OTP + nova senha

### Perfil autenticado

`GET /api/auth/me` — requer header `Authorization: Bearer {token}`

## JWT claims

| Claim | Descrição |
|-------|-----------|
| `sub` / NameIdentifier | User ID |
| `email` | Email |
| `tenant_id` | Tenant ID |
| `role` | `ADMIN`, `USER`, etc. |

## SMTP / OTP em dev

Sem SMTP configurado, o código OTP aparece nos **logs da API**:

```
[Email] SMTP not configured — OTP/log only. To=... Body=...708135...
```

Configure a seção `Smtp` em `appsettings.json` ou variáveis de ambiente no Coolify.

## Frontend Blazor

| Rota | Página |
|------|--------|
| `/login` | Login |
| `/register` | Início do cadastro |
| `/register/verify` | Confirmação OTP |
| `/forgot-password` | Esqueci senha |
| `/reset-password` | Redefinir senha |

Rotas `/app/*` exigem autenticação (`[Authorize]` + `AuthorizeRouteView`).
