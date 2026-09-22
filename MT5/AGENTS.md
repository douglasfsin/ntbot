# MT5 — convenções para agentes

Fonte canônica de processo para qualquer trabalho em Indicators / Experts / Scripts com integração à API NTBot.

## Caminho vivo (MetaTrader 5)

```
C:\Users\dougl\AppData\Roaming\MetaQuotes\Terminal\D0E8209F77C8CF37AD8BF550E51FF075\MQL5
```

Este é o **único** Data Folder / MQL5 root padrão do usuário. Não inventar outro terminal ID sem confirmação explícita.

## Fonte no repositório

| Repo (`MT5/`) | Live (`.../MQL5/`) |
|---------------|--------------------|
| `MT5/Indicators/` | `Indicators/` |
| `MT5/Experts/` | `Experts/` |
| `MT5/Scripts/` | `Scripts/` |
| `MT5/Include/` | `Include/` |

Editar **sempre** em `MT5/` no repo; depois **copiar** para o caminho vivo (overwrite ok; nunca apagar arquivos alheios do Terminal).

## Deploy obrigatório

Após criar ou alterar artefatos MQL5:

1. Garantir pastas de destino (`Indicators`, `Experts`, `Scripts`, `Include`, …).
2. Copiar espelhando a árvore acima.
3. Listar origem → destino na resposta.
4. Lembrar o usuário de **compilar no MetaEditor (F7)** após a cópia.
5. Atualizar `MT5/README.md` se o artefato for novo ou mudar inputs/URLs.

## Naming

- Prefixo de arquivo: `NTBot_*` (ex.: `NTBot_OperationalZones.mq5`).
- Includes compartilhados: `MT5/Include/` (ex.: `NTBot.mqh`).
- Prefixo de objetos de gráfico: `NTBOT_*` quando aplicável.

## API / WebRequest

- Base URL padrão: `http://127.0.0.1:5053` (input tipicamente `InpApiBaseUrl`).
- Endpoints MT5 sob `/api/mt5/...` (ex.: `GET /api/mt5/zones`).
- MT5 exige allowlist: **Ferramentas → Opções → Expert Advisors → Permitir WebRequest** com a mesma base URL (scheme+host+port, sem path). Adicionar **ambos** `http://127.0.0.1:5053` e `http://localhost:5053`; após OK, **reiniciar o terminal**.
- Sem a allowlist, o Experts log mostra `WebRequest failed err=4014`. O indicador v1.32+ faz fallback para `FILE_COMMON:NTBot_zones_{symbol}.txt` gravado pela API (`Mt5ZonesFileBridge`).
- O indicador exibe status via `Comment` (HTTP, zonas, último erro, **URL exata**).

## Detalhes do indicador atual

Ver `MT5/README.md` (zonas operacionais, dual-mode HTTP+arquivo, inputs, filtros XAUUSD).
