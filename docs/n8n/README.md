# NTBot Trading Intelligence — n8n Workflows

Import these JSON files in n8n (**Settings → Import workflow**), activate each workflow, and copy the production webhook URLs into NTBot.Api environment variables.

## Workflows

| File | Purpose | Env var |
|------|---------|---------|
| `ti-master-agent.json` | Master agent (default for all assets) | `TradingIntelligence__N8nWebhookUrl` |
| `ti-specialist-win.json` | WIN specialist override | `TradingIntelligence__N8nAssetWebhookUrls__WIN` |
| `ti-specialist-wdo.json` | WDO specialist override | `TradingIntelligence__N8nAssetWebhookUrls__WDO` |

## Expected request payload

NTBot POSTs JSON with: `asset`, `confluenceScore`, `recommendation`, `explanation`, `positive`, `negative`, `zones`, `intersections`, `engines`, `agentInsights`.

## Expected response

```json
{
  "master": { "summary": "...", "strengths": [], "weaknesses": [], "probability": "...", "risk": "..." },
  "agentInsights": [{ "agentId": "...", "specialization": "...", "summary": "...", "highlights": [] }]
}
```

Legacy responses with only a `summary` object are still supported.
