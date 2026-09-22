# Enrichment: Módulo de Assessoria — fontes e síntese

**Status:** material de apoio — Phase 1–4 **DONE** no NtBot (OF = simulação; produção OF = parceiro ITP pós-Phase-4)  
**Data:** 2026-09-22  
**Escopo:** síntese de fontes; implementação no plano principal  
**Plano principal:** [`white-label-portfolio-module-plan.md`](./white-label-portfolio-module-plan.md)
---

## 1. Fontes analisadas

| Fonte | Caminho (referência) | Tipo | Conteúdo útil |
|-------|----------------------|------|---------------|
| **Wealth Management Blueprint** | `C:\Users\dougl\OneDrive\Documentos\Projetos\ntbot\Modulo de Assesoria\Wealth Management Blueprint.pptx` | Deck (10 slides, image-based) | Visão B2B2C, pilares de produto, UX cliente/assessor/catálogo, jornada do dado, arquitetura 4 camadas, DER core |
| **Brazil's Financial Blueprint** | `...\Modulo de Assesoria\Brazil's Financial Blueprint.pptx` | Deck (19 slides, image-based) | Contexto CVM 175, classes/subclasses, matriz de concentração, FIDC varejo, FIAGRO, Open Finance/consentimento, AI cross-sell, MissionOps |
| **Infográfico WhatsApp** | `...\Modulo de Assesoria\WhatsApp Image 2026-09-22 at 11.51.46.jpeg` | Infográfico | Quatro blocos de jornada: assessoria+ofertas, multi-carteira, Open Finance, TWR/MWR+riscos |

> **Nota:** a pasta `Modulo de Assesoria` existe no OneDrive do usuário; **não** há cópia versionada em `c:\Projetos\ntbot\`. Manter apenas como path de referência (não copiar PPTX/imagens grandes para o git sem decisão explícita).

---

## 2. Síntese por fonte

### 2.1 Wealth Management Blueprint

**Posicionamento:** plataforma **B2B2C de Wealth Management** — consolidação + recomendação; framework declarado **CVM 175 & Open Finance Brasil**; status “ready for development” (spec de arquitetura/UX/dados).

**Quatro pilares do motor:**

| Pilar | Ideia-chave | Fase citada no deck |
|-------|-------------|---------------------|
| Multi-custódia & Open Finance | APIs / OF; agregação XP, BTG, Safra; fim do upload manual de planilhas | **Phase 4** no deck |
| Behavioral Asset Allocation | Mental accounting; sub-carteiras por horizonte (Reserva, Crescimento, Aposentadoria) | Core produto |
| Precificação & MtM | Dados Cedro/B3; cálculo automático **TWR** e **MWR** | Core analytics |
| Compliance & Suitability | Monitoramento CVM 19 / ANBIMA; travas de alocação **CVM 175** | Core compliance |

**UX (protótipos):**

- **Dashboard cliente:** saldo consolidado, rentabilidade YTD, carteiras por objetivo (+ Adicionar Objetivo), donut classe (RF / RV / Fundos estruturados / Caixa).
- **Detalhe + aceite:** tabela MtM com custodiante, TWR, status ENQUADRADO/DESENQUADRADO; painel “Proposta recebida” (ex.: FIDC Sênior AAA) com impacto Sharpe simulado e Aceitar/Recusar.
- **Cockpit assessor:** AUM, clientes, alertas “fora do suitability”; tags CRM (risco CVM 19, vencimento CRA, oportunidade cross-sell); gráfico risco×retorno vs CDI; aviso limite offshore CVM 175.
- **Catálogo + simulação:** filtros CVM 175 (varejo, FIDC exige rating, limite offshore); cards FIDC/FIAGRO; Antes/Depois (Drawdown, CVaR); checklist suitability; “Enviar proposta via app”.

**Jornada do dado (6 etapas):** Conexão & Consentimento (FAPI-BR) → Importação → Enriquecimento & MtM (Cedro/B3 WSS) → Análise & Alertas (suitability + AI) → Orquestração (assessor monta proposta) → Aceite & Execução (assinatura digital + corretora).

**Arquitetura de referência (deck):** Presentation (Client RN / Advisor React SPA) → Application (Aggregator, Calculation Engine MtM/TWR/Sharpe, Recommendation CVM 175) → Integration (OF FAPI-BR mTLS, Cedro, Broker Orders B3) → Data (PostgreSQL + time-series).  
**Adaptação NtBot:** Blazor white-label + API existente; RN/React são referência de UX, não stack mandatória.

**DER (rascunho do deck):**

- `Cliente` — perfil_risco (Conservador/Moderado/Arrojado), segmento (Varejo/Alta Renda/Private), cpf_cnpj  
- `Assessor` — registro_cvm  
- `CarteiraPorObjetivo` — nome_objetivo, data_alvo, status  
- `ProdutoCatalogo` — cnpj/ticker, classe_ativo, flag_varejo_permitido, rating_credito, limite_offshore  
- `Ativo_Posicao` — qtd, preco_medio, valor_mtm_atual  
- `Recomendacao` — impacto_sharpe_estimado, status_aceite (Pendente/Aceito/Recusado)

### 2.2 Brazil's Financial Blueprint

**Tese:** convergência **Regulação de Capital + Infraestrutura de Dados + Inteligência Comercial**.

| Bloco | Conteúdo relevante ao módulo |
|-------|------------------------------|
| Contexto mercado | PL fundos ~R$ 10,8 tri; 32k+ fundos CVM — justifica catálogo + gating |
| Tríade | CVM 175 (classes, FIDC, FIAGRO) · Open Finance (consentimentos em escala) · IA & cross-selling |
| Firewall classes | Fundo principal → Classes/Subclasses com **segregação patrimonial** (varejo vs qualificado) |
| Matriz concentração | Limites por perfil (Varejo/Qualificado/Profissional) em FIP, CRI/CRA, carbono, cripto; offshore até 100% com requisitos IOSCO/liquidez |
| FIDC varejo | Só cotas sênior; rating obrigatório; amortização definida; proíbe subordinada / resgate >180d / crédito sem lastro |
| FIAGRO | Veículo híbrido (FIDC/FII/FIP + CBIOs/carbono) |
| Consentimento OF | Identidade · Escopo · Temporalidade · Proteção (+ log LGPD); “sem consentimento válido não há compartilhamento” |
| Manual vs API | Consolida reconciliação contínua e auditoria nativa |
| Cross vs Up-sell | Horizontal (nova categoria) vs vertical (upgrade de margem) |
| Jornada de vida | Gatilhos transacionais → ofertas (ex.: pico de carreira → Wealth + FIDC) |
| Banking OS | Interaction / Orchestration (CRM+Core) / Intelligence (Next Best Action) |
| Tributação | Matriz WHT FIDC/FII/FIAGRO/FIP; diferimento come-cotas como argumento comercial |
| MissionOps | 1 Mapear/conectar APIs → 2 Unificar dados (Customer State Graph) → 3 Automação IA/OF → 4 Estruturação capital CVM 175 |

### 2.3 Infográfico “Investimentos Inteligentes”

Quatro capacidades de produto (alinhadas ao deck Wealth + narrativa Brazil):

1. **Assessoria especializada + ofertas personalizadas** — push de FIF / FII / FIDC alinhado ao **Perfil de Investidor**.  
2. **Múltiplas carteiras por objetivo** — ex.: Aposentadoria, Reserva de Emergência; classes e subclasses segregadas.  
3. **Consolidação Open Finance** — XP, BTG, Safra; saldos/posições automáticos (sem planilha).  
4. **Performance & risco** — TWR / MWR; riscos auditáveis e transparentes; UI white-label ready.

---

## 3. Mapeamento → plano NtBot (resumo)

| Capacidade | Fase NtBot proposta | Fonte principal |
|------------|---------------------|-----------------|
| Multi-carteira por objetivo + classes/subclasses | **Fase 2** | Infográfico + Wealth UX/DER |
| Perfil investidor + gating básico de ofertas | **Fase 2** | Infográfico + Wealth + Brazil matriz |
| Push assessor→cliente (proposta Pendente/Aceito/Recusado) | **Fase 2** (manual) → 3 (AI) | Wealth UX/DER |
| Catálogo FIF/FII/FIDC (+ FIAGRO/FIP depois) | **Fase 3** seed → **Fase 4** completo CVM 175 | Brazil + Wealth catálogo |
| Importação CSV/manual de posições | **Fase 2** | Pragmática NtBot (OF depois) |
| Relatórios AI + email + disclaimer | **Fase 3** | Plano original + Wealth análise |
| MtM Cedro/B3 + TWR/MWR completo | **Fase 3** (TWR simplificado) → **Fase 4** (TWR/MWR + MtM live) | Wealth pilares |
| Relatórios de risco auditáveis (Sharpe, drawdown, CVaR, enquadramento) | **Fase 3** básico → **Fase 4** completo | Wealth + Infográfico |
| Open Finance (FAPI-BR, consentimento) | **Fase 4** | Wealth Phase 4 + Brazil II |
| Execução/assinatura → corretora | **Fora do v1** / pós-Fase 4 | Wealth jornada etapa 6 |
| White-label branding | **Fase 1** (já no plano) | Infográfico + plano A |

Detalhamento operacional e critérios de aceite: ver seções atualizadas em `white-label-portfolio-module-plan.md`.

---

## 4. Questões abertas adicionais (só enriquecimento)

Ver também seção 8 expandida do plano principal. Destaques vindos das fontes:

1. Parceiro / ITP Open Finance (build próprio vs Belvo/Pluggy/etc.)?  
2. ANBIMA suitability + “CVM 19” (deck) — qual norma exata e quem é responsável regulado?  
3. Fonte MtM: Cedro obrigatório no MVP analytics ou B3/outro feed?  
4. Escopo de catálogo MVP: só FIF/FII/FIDC ou já FIAGRO/FIP/cripto?  
5. Aceite de proposta no app implica **execução** (ordem) ou só registro de intenção?  
6. Assessor precisa de `registro_cvm` no cadastro desde Fase 2?
