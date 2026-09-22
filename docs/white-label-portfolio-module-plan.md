# Plano: White-label + Módulo de Assessoria / Portfólio

**Status:** Phase 1–4 **DONE** (OF = shell + simulação; produção OF = pós-Phase-4 / parceiro ITP)

**Data:** 2026-09-22 (Phase 4 finalizada)

**Enrichment:** [`modulo-assessoria-enrichment.md`](./modulo-assessoria-enrichment.md)

---

## Decisões default (execução)

1. **White-label** = `Tenant.WhiteLabelEnabled` **ou** plano Stripe slug `partner` **ou** `FeaturesJson` (`branding.enabled`)

2. **Roles novas:** `ADVISOR`, `CLIENT` (mantém `ADMIN`/`TRADER`/`VIEWER`)

3. Partner vende **acesso branded** apenas — **sem Stripe Connect** no v1

4. **CustomDomain:** campo armazenado; sem DNS/TLS automático

5. **Sem SSO** no v1

6. Aceite de oferta = **intenção** (sem execução em corretora)

7. Posições: **manual + CSV** + **Open Finance simulado** (`SimulatedOpenFinanceProvider`); interface `IOpenFinanceProvider` pronta para adapter FAPI-BR real

---

## Status de implementação

| Fase | Status |
|------|--------|
| **1 — White-label MVP** | ✅ `TenantBranding`, CSS `--nt-*`, `/t/{slug}/login`, Admin UI `/app/settings/branding`, feature gate |
| **2 — Assessoria / Portfólio core** | ✅ entidades, API, cockpit assessor, view cliente, CSV, ofertas, seed FIF/FII/FIDC |
| **3 — AI / reports / TWR** | ✅ análise AI, relatório HTML+MailKit, TWR/MWR aproximado, catálogo + filtros |
| **4 — OF + MtM + risco auditável** | ✅ **DONE** — ver checklist abaixo |

### Phase 4 — checklist finalizado

| Item | Status |
|------|--------|
| Snapshots MtM (`PortfolioValuationSnapshot`) + capturar das holdings | ✅ |
| Cashflows (`PortfolioCashflow`) aporte/resgate | ✅ |
| TWR/MWR caminho real via snapshots + fluxos (fallback aproximação) | ✅ + unit tests |
| Relatório de risco auditável HTML (concentração, suitability, performance, timestamp) | ✅ |
| PDF via QuestPDF + versionamento (`Version`, `PdfContent`) | ✅ |
| Branding white-label no HTML/PDF | ✅ |
| Open Finance: ciclo Pending → Grant → Active → Revoke | ✅ |
| `SimulatedOpenFinanceProvider` importa mock XP/BTG/Safra | ✅ |
| `IOpenFinanceProvider` pronto para FAPI-BR | ✅ |
| Catálogo CVM 175: FIAGRO/FIP + RiskRating + filtros | ✅ |
| UI PT: Performance (capturar valuation), Open Finance, catálogo | ✅ |

**Schema:** bootstrap idempotente + tabelas `PortfolioValuationSnapshots`, `PortfolioCashflows`; colunas `PdfContent`, `Version`, `ReportType`, `RiskRating`.

**Código principal:**

- Domain: `PortfolioValuationSnapshot`, `PortfolioCashflow`, `PortfolioReturnMath` (subperíodos + MWR signed)
- Api: `PortfolioValuationService`, `PerformanceService`, `SimulatedOpenFinanceProvider`, `PortfolioReportService` (QuestPDF)
- Web: Performance, OpenFinanceShell, ProductCatalog, AdvisorPortfolioDetail

---

## Como testar / demo Phase 4

1. Subir Api (bootstrap) + Web.
2. White-label: `UPDATE "Tenants" SET "WhiteLabelEnabled" = TRUE` → branding → `/t/{slug}/login`.
3. **Snapshots + TWR:** carteira com posições → `/app/portfolio/performance/{id}` → **Capturar valuation** (≥2 dias ou re-capture após mudar MtM) → TWR marca `oficial`.
4. **Cashflows:** assessor registra aporte/resgate na mesma página → MWR atualiza.
5. **Open Finance simulado:** `/app/portfolio/open-finance` → Solicitar (XP) → **Conceder** → escolher carteira → **Importar mock** → holdings com custódia XP/BTG/Safra.
6. **PDF risco:** assessor no detalhe da carteira → **Gerar relatório de risco** → HTML branded + PDF (`GET api/portfolio/reports/{id}/pdf`).
7. Catálogo: `/app/advisory/catalog` — filtrar FIAGRO/FIP, ver RiskRating.
8. Testes: `dotnet test tests/NtBot.UnitTests --filter PortfolioReturnMath`.
9. Flag opcional: `OpenFinance:Provider=NoOp` para desligar simulação.

---

## Pós-Phase 4 (explicitamente deferido)

- **Open Finance produção** com parceiro ITP / FAPI-BR / credenciais XP·BTG·Safra reais
- Stripe Connect, SSO, DNS/TLS custom domain automático
- Execução de ordens / assinatura digital / boletagem de fundos
- MtM live Cedro/B3 (hoje: snapshot manual/simulado das holdings)

Detalhe de produto / fontes **[WM][BF][IG]:** ver `modulo-assessoria-enrichment.md`.
