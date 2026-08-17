# Observabilidade — OpenTelemetry + SigNoz

Logs, traces e métricas das APIs NtBot seguem o padrão **OpenTelemetry** e são exportados por OTLP para o SigNoz self-hosted no Coolify.

| | |
|--|--|
| Serviço Coolify | `eva3s2kbg9a48onb3ws2hvgd` |
| UI | http://signoz-eva3s2kbg9a48onb3ws2hvgd.46.225.161.55.sslip.io |
| Versão | `v0.97.1` (`/api/v1/health` → `ok`) |
| OTLP HTTP | collector `otel-collector:4318` (rede Docker; não expor na internet) |
| OTLP gRPC | `4317` |

A partição por produto usa o resource attribute `service.namespace` (`NtBot`, `Orbital`, `Montescar`).

## Serviços instrumentados

| Host | `service.name` | `service.namespace` |
|------|----------------|---------------------|
| `NtBot.Api` | `ntbot-api` | `NtBot` |
| `NtBot.Web` | `ntbot-web` | `NtBot` |
| `NtBot.Worker` | `ntbot-worker` | `NtBot` |

Biblioteca compartilhada: `NtBot.Observability`.

## Variáveis de ambiente

| Variável | Função |
|----------|--------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP HTTP, ex. `http://<uuid>-otel-collector:4318` |
| `OTEL_EXPORTER_OTLP_HEADERS` | Só SigNoz Cloud (`signoz-ingestion-key=`). Self-hosted Coolify: vazio |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` (padrão) ou `grpc` |
| `OTEL_SERVICE_NAME` | Sobrescreve `service.name` |
| `OTEL_RESOURCE_ATTRIBUTES` | Ex. `service.namespace=NtBot,deployment.environment=Production` |
| `SIGNOZ_OTLP_ENDPOINT` | Alias do endpoint (Stripe Projects) |
| `SIGNOZ_INGESTION_KEY` | Alias da ingestion key |
| `OTEL_SDK_DISABLED` | `true` desliga o export |

Sem endpoint configurado, a API continua logando em console/arquivo e **não** tenta enviar para `localhost:4317`.

## Coolify (NTBot.Api / NTBot.Web)

Ver inventário em [deployment/coolify.md](deployment/coolify.md). Sincronize as env vars OTEL:

```bash
export COOLIFY_ACCESS_TOKEN=...
python3 scripts/coolify/inspect_and_sync.py --sync-otel --deploy
```

## Dashboards e views no SigNoz

Por projeto (`NtBot`, `Orbital`, `Montescar`):

- Dashboard **`{projeto} — Logs`**: volume, erros, warnings, breakdown por severity e por `service.name`
- Views no Logs Explorer: **All logs**, **Errors**, **Warnings**

```bash
export SIGNOZ_URL="http://signoz-eva3s2kbg9a48onb3ws2hvgd.46.225.161.55.sslip.io"
export SIGNOZ_API_KEY="..."   # Settings → API Keys no SigNoz
python3 scripts/signoz/provision_observability.py
```

A instância SigNoz está em **v0.97.1** (API de dashboards v1). O script detecta a versão automaticamente.

Dashboards criados na instância (2026-08-17): **NtBot — Logs**, **Orbital — Logs**, **Montescar — Logs**, mais views All/Errors/Warnings no Logs Explorer (`sourcePage=logs`).

`--dry-run` lista o que seria criado/atualizado. `--print-payloads` imprime o JSON.
