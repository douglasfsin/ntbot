# NTBot — MetaTrader 5 (MQL5)

## Caminho vivo (padrão do projeto)

Terminal Data Folder / MQL5 root **canônico**:

```
C:\Users\dougl\AppData\Roaming\MetaQuotes\Terminal\D0E8209F77C8CF37AD8BF550E51FF075\MQL5
```

Qualquer indicador, expert ou script com integração à API deve seguir este caminho. Detalhes para agentes: [`AGENTS.md`](./AGENTS.md).

## Fonte no repositório vs deploy

| No repo | Copiar para (live) |
|---------|-------------------|
| `MT5/Indicators/*.mq5` | `...\MQL5\Indicators\` |
| `MT5/Experts/*.mq5` | `...\MQL5\Experts\` |
| `MT5/Scripts/*.mq5` | `...\MQL5\Scripts\` |
| `MT5/Include/*` | `...\MQL5\Include\` |

**Fluxo:** editar em `MT5/` → copiar para o caminho vivo → **compilar no MetaEditor (F7)** → (se WebRequest) garantir URL na allowlist.

Não apagar arquivos alheios na pasta do Terminal; só sobrescrever artefatos NTBot.

## Naming

- Arquivos: prefixo `NTBot_*` (ex.: `NTBot_OperationalZones.mq5`)
- Biblioteca: `Include/NTBot.mqh`
- Objetos de gráfico (zonas): prefixo `NTBOT_OZ_`

## WebRequest / API

- Base URL padrão: `http://127.0.0.1:5053` (input `InpApiBaseUrl` / `NTBot_Server`)
- Em **Ferramentas → Opções → Expert Advisors**:
  - marcar *Permitir WebRequest para as URLs listadas*
  - adicionar exatamente a base (ex.: `http://127.0.0.1:5053`) **e** `http://localhost:5053`
  - OK → **reiniciar o terminal MT5** (a allowlist às vezes só aplica após restart)

Endpoints usados pelos artefatos atuais incluem `/api/mt5/zones`, `/api/mt5/heartbeat`, `/api/mt5/update`, `/api/marketdata/tick`.

### 4014 mesmo com URL listada

O erro `err 4014 | URL not allowed` pode **persistir** com a URL visível nas opções. Causas comuns:

1. Allowlist alterada sem **OK + restart completo** do MT5
2. Só `127.0.0.1` ou só `localhost` — o MT5 exige match exato do host usado no `WebRequest`
3. Caractere invisível / espaço / barra final na lista (`http://127.0.0.1:5053/` ≠ base)
4. Terminal errado (outro Data Folder) — confira `File → Open Data Folder`
5. Indicador antigo ainda anexado (recompile F7 e reanexe)

**Mitigação confiável:** bridge de arquivo (sem WebRequest). A API grava `NTBot_zones_XAUUSD.txt` em:

```
%APPDATA%\MetaQuotes\Terminal\Common\Files\
```

O indicador lê com `FileOpen(..., FILE_COMMON)` se o HTTP falhar.

---

## Indicador: zonas operacionais no gráfico

O indicador **`NTBot_OperationalZones.mq5`** (v1.32+) usa **dual-mode**:

1. `GET /api/mt5/zones` via WebRequest (tenta host alternativo localhost↔127.0.0.1 em 4014)
2. Fallback: arquivo `NTBot_zones_{symbol}.txt` em Common Files

Pipeline:

```
TradingIntelligence (OperationalZoneEngine + SmcEngine)
        ↓
GET /api/mt5/zones  +  Mt5ZonesFileBridgeWorker → Common\Files\NTBot_zones_XAUUSD.txt
        ↓
Indicator MT5 → WebRequest ou FileOpen → OBJ_RECTANGLE + OBJ_HLINE + OBJ_TEXT
```

### Instalação (zonas)

1. Copie (ou use o deploy padrão acima):
   - `MT5/Indicators/NTBot_OperationalZones.mq5` → `{DataFolder}/MQL5/Indicators/`
   - (opcional / shared) `MT5/Include/NTBot.mqh` → `{DataFolder}/MQL5/Include/`
2. No MetaEditor, compile o indicador (F7).
3. **Allowlist WebRequest** (ainda recomendada; fallback funciona sem ela):
   - Ferramentas → Opções → Expert Advisors
   - Marcar *Permitir WebRequest para as URLs listadas*
   - Adicionar **exatamente** `http://127.0.0.1:5053` **e** `http://localhost:5053` (sem path)
   - OK → **feche e reabra o MT5** → remova/recoloque o indicador
4. Suba a **NtBot.Api** (`Mt5ZonesFileBridge:Enabled=true`) — o worker grava o arquivo a cada ~30s; o endpoint `/api/mt5/zones` também grava como side-effect.
5. Arraste o indicador para o gráfico **XAUUSD** (ou GOLD / XAUUSDm — o mapeamento lógico vira `XAUUSD`).
6. Confirme no `Comment`:
   - HTTP ok: `... | ok_http | ... | http://127.0.0.1:5053/api/mt5/zones?...`
   - Fallback: `... | ok_file | ... | FILE_COMMON:NTBot_zones_XAUUSD.txt`

### Troubleshooting rápido

| Sintoma | Causa | Ação |
|---------|-------|------|
| Comment / log `err=4014` | Allowlist / restart / host mismatch | Passo 3; adicione ambos hosts; **reinicie MT5** |
| `4014` mesmo com URL listada | Gotcha clássico do terminal | Use file bridge; confira `Common\Files\NTBot_zones_XAUUSD.txt` |
| `err=5200` / não conecta | API parada ou porta errada | Subir NtBot.Api em `:5053` |
| `ok_file` com 0 zones | Arquivo ausente / API parada | Aguarde worker (~30s) ou `GET /api/mt5/zones?symbol=XAUUSD` |
| `HTTP 404` / 0 zones | Snapshot TI frio / sem dados | Aguardar warm-up TI; polling 30s |
| Zonas ok na API, nada no chart | Indicador antigo / não compilado | Recompilar F7 e reanexar |

### Inputs úteis

| Input | Default | Nota |
|-------|---------|------|
| `InpApiBaseUrl` | `http://127.0.0.1:5053` | Base da NtBot.Api (trim; sem `/` final) |
| `InpTryAltHost` | `true` | Em 4014 tenta localhost↔127.0.0.1 |
| `InpUseFileFallback` | `true` | Lê Common Files se HTTP falhar |
| `InpPreferFile` | `false` | `true` = só arquivo (ignora WebRequest primeiro) |
| `InpZonesFileName` | `NTBot_zones_{symbol}.txt` | Nome em FILE_COMMON |
| `InpTimeframeKey` | `60` | TF do snapshot TI (não precisa ser o TF do chart) |
| `InpMaxZones` | `6` | Cap de retângulos |
| `InpRefreshSec` | `30` | Poll |
| `InpHttpTimeoutMs` | `25000` | Timeout WebRequest (cold TI pode passar de 8s) |
| `InpShowComment` | `true` | Status + **URL exata** usada no WebRequest/arquivo |
| `InpShowLiquidity` | `true` | Desligue se Liq ↑/↓ ainda poluir |

### O que o operador vê

- **Verde pastel**: Demanda / OB compra / FVG ↑ / Desconto / Alvo  
- **Rosa pastel**: Oferta / OB venda / FVG ↓ / Prêmio  
- **Âmbar suave**: POC / OTE  
- **Roxo suave**: pools de liquidez de sessão  
- Preenchimento semi-transparente (velas legíveis) + bordas suaves  
- Labels em português ASCII com faixa de preço (`Demanda 2650.10-2655.40`, `FVG+ …`) — sem setas Unicode (evita corrupção ANSI → “SVG”)

### Removido / filtrado (anti-ruído)

- Áreas de valor mid-score (faixa full High–Low)  
- Prêmio/Desconto inativos  
- Sobreposições fortes (dedupe ~55%)  
- VWAP fraco em XAUUSD (só se confluence ≥ 60)  
- Cap de 6 zonas no ouro (7 nos demais)

### API (zonas)

`GET /api/mt5/zones?symbol=XAUUSD&timeframe=60&max=6`

Resposta inclui `zones[]` (JSON), `delim` (linhas `id|kind|side|lo|hi|r,g,b|alpha|style|label|score|source`) e `fileBridge` (path gravado, se habilitado).

#### File bridge (config API)

Seção `Mt5ZonesFileBridge` em `appsettings*.json`:

| Chave | Default | Nota |
|-------|---------|------|
| `Enabled` | `true` | Liga writer + worker |
| `OutputDirectory` | `""` | Vazio = `%APPDATA%\MetaQuotes\Terminal\Common\Files` |
| `Symbols` | `["XAUUSD"]` | Símbolos do worker periódico |
| `Timeframe` | `60` | TF TI |
| `MaxZones` | `6` | Cap |
| `RefreshSeconds` | `30` | Intervalo do worker |
| `FileNamePattern` | `NTBot_zones_{symbol}.txt` | Nome do arquivo |

Formato do arquivo:

```
#NTBot|symbol=XAUUSD|timeframe=60|count=6|updatedAt=...
z0|fvg|buy|4307.72|4350.33|14,203,129|45|dash|FVG …|74|operational
...
```

---

## Outros artefatos no repo

| Arquivo | Destino live |
|---------|--------------|
| `MT5/Experts/TradeAssistant.mq5` | `...\MQL5\Experts\` |
| `MT5/Include/NTBot.mqh` | `...\MQL5\Include\` |
