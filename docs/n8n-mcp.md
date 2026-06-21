# n8n MCP Integration

## Configuração

```json
"TradingIntelligence": {
  "N8nWebhookUrl": "https://seu-n8n/webhook/trading-intelligence"
}
```

## Fluxo

1. `TradingIntelligenceService` gera snapshot
2. `N8nAiProvider` POST payload JSON ao webhook
3. Resposta mapeada para `MasterAgentSummary`
4. Fallback: `N8nAiProviderStub` se URL vazia ou erro

## Payload enviado

- asset, confluenceScore, classification, explanation
- positive/negative factors, operational zone labels

## Cursor MCP

Workflows n8n podem ser descobertos via MCP `user-n8n-mcp` no ambiente de desenvolvimento.
