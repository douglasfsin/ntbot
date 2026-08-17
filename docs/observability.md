# Observabilidade — OpenTelemetry + SigNoz

Logs, traces e métricas das APIs NtBot seguem o padrão **OpenTelemetry** e são exportados por OTLP para o SigNoz (`signoz-eva3s2kbg9a48onb3ws2hvgd`).

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
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP (ex. `https://ingest.<region>.signoz.cloud:443`) |
| `OTEL_EXPORTER_OTLP_HEADERS` | `signoz-ingestion-key=<key>` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` (padrão) ou `grpc` |
| `OTEL_SERVICE_NAME` | Sobrescreve `service.name` |
| `OTEL_RESOURCE_ATTRIBUTES` | Ex. `service.namespace=NtBot,deployment.environment=Production` |
| `SIGNOZ_OTLP_ENDPOINT` | Alias do endpoint (Stripe Projects) |
| `SIGNOZ_INGESTION_KEY` | Alias da ingestion key |
| `OTEL_SDK_DISABLED` | `true` desliga o export |

Sem endpoint configurado, a API continua logando em console/arquivo e **não** tenta enviar para `localhost:4317`.

## Coolify (NTBot.Api / NTBot.Web)

Copie as env vars OTLP do recurso SigNoz no Stripe Projects. Mantenha:

```
OTEL_RESOURCE_ATTRIBUTES=service.namespace=NtBot,project=NtBot,deployment.environment=Production
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
```

## Dashboards e views no SigNoz

Por projeto (`NtBot`, `Orbital`, `Montescar`):

- Dashboard **`{projeto} — Logs`**: volume, erros, warnings, breakdown por severity e por `service.name`
- Views no Logs Explorer: **All logs**, **Errors**, **Warnings**

```bash
export SIGNOZ_URL="https://<seu-signoz>"
export SIGNOZ_API_KEY="..."
python3 scripts/signoz/provision_observability.py
```

`--dry-run` lista o que seria criado/atualizado. `--print-payloads` imprime o JSON.
