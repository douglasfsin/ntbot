# Profit DDE Provider

Implementação: `ProfitDdeProvider` + `ProfitDdeSession` (DDEML via `user32.dll`).

Protocolo oficial Profit (manual Nelogica):

```
=profitchart|cot!WINFUT.ULT
```

- **Serviço:** `profitchart`
- **Tópico:** `COT`
- **Item:** `{ATIVO}.{QUALIFICADOR}` (ex: `WINFUT.ULT`)

## Configuração

`Configuration/profit_dde_config.json`:

```json
{
  "DdeServer": "profitchart",
  "DdeTopic": "COT",
  "Assets": [
    { "LogicalSymbol": "WIN", "DdeSymbol": "WINFUT", "PriceItem": "ULT" }
  ]
}
```

## Ativação

```json
"EnableProfitDde": true
```

## Threading

DDE roda em thread **STA** dedicada com message pump (requerido pelo DDEML).

## RTD vs DDE

| Modo | Ticks | Posições/conta |
|------|-------|----------------|
| RTD only | RTD | RTD |
| DDE + RTD | DDE | RTD |

Quando `EnableProfitDde=true`, o RTD deixa de publicar ticks (evita duplicata).
