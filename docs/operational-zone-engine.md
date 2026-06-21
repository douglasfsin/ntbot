# Operational Zone Engine

Identifica regiões operacionais a partir de:

- Interseções de timeframes (5×15, 5×30, 5×60, 15×30, 15×60, 30×60)
- Scores Wyckoff + SMC + Volume por timeframe
- Confluence Score global

## Tipos de zona

- **Zona Forte Compradora** — score ≥ 70 bullish
- **Zona Forte Vendedora** — score ≤ 30 bearish
- **Zona Moderada / Neutra**

Implementação: `OperationalZoneEngine` + `TimeframeIntersectionEngine`
