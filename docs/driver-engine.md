# Driver Engine

## Configuração dinâmica

Tabela `DriverCompositions`:

| Campo | Descrição |
|-------|-----------|
| TargetAsset | Ativo alvo (WIN, PETR4, …) |
| DriverAsset | Fator (PETR4, MACRO, FLOW, …) |
| Weight | Peso 0–1 |
| Enabled | Ativo/inativo |
| DisplayOrder | Ordem de exibição |
| Category | Macro, Correlacao, … |

## Seed WIN (padrão)

| Driver | Peso |
|--------|------|
| PETR4 | 18% |
| VALE3 | 12% |
| ITUB4 | 8% |
| BBDC4 | 5% |
| WEGE3 | 4% |
| ABEV3 | 3% |
| MACRO | 50% |

## Fluxo

1. `DriverCompositionStore` lê PostgreSQL
2. `MarketDriverContextBuilder` injeta `DriverSources` no contexto
3. `AssetDriverRule` aplica regras (fallback: `MarketDriversCatalog` estático)

## Interface

Configurações → Trading Intelligence → Market Drivers: adicionar, editar, excluir, duplicar, importar/exportar.
