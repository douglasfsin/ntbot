# NTBot — Indicadores Profit Chart (Nelogica)

Indicadores **NTSL** para importar no **Profit / Profit Chart** (Nelogica, Brasil).  
**Não** são scripts MetaTrader (MQL5). Não copie esta pasta para o terminal MT5.

## Conteúdo

| Arquivo | Uso |
|---|---|
| `NTBot_WyckoffCorr.txt` | Indicador principal — aplicar em **1 minuto** e **5 minutos** |
| `NTBot_WyckoffCorr_15s.txt` | Companheiro — aplicar em **15 segundos** (se o Profit tiver esse TF) |

Nome interno: **NTBot_WyckoffCorr**.  
Objetos/plots usam o prefixo conceitual `NTBOT_WYCK_*`.

---

## Como importar no Profit

O Profit não carrega `.mq5`. O caminho usual é o **Editor de Estratégias / Indicador personalizado**.

### Opção A — Colar no Editor (mais confiável)

1. Abra o **Profit**.
2. Menu **Estratégias** → **Editor de Estratégias** (ou atalho do Editor de fórmulas / Indicador personalizado, conforme a versão: Profit Pro, Profit One, etc.).
3. **Novo** → tipo **Indicador** (não “Execução” / robô).
4. Apague o modelo e **cole** o conteúdo de `NTBot_WyckoffCorr.txt`.
5. **Verificar sintaxe** e **Compilar**.
6. **Salvar** com o nome `NTBot_WyckoffCorr`.
7. No gráfico: clique direito → **Inserir** → **Indicador** → **Personalizado** / **Estratégias** → selecione `NTBot_WyckoffCorr`.

### Opção B — Importar arquivo

1. **Estratégias** → **Gerenciador de estratégias** → **Importar**.
2. Se o gerenciador exigir `.psf`, primeiro cole o código no Editor, salve e **exporte** `.psf`; depois importe em outro PC.
3. Arquivos `.txt` desta pasta servem como fonte para colar/compilar.

### Onde aplicar

| Timeframe do gráfico | `BarrasMacro` | Observação |
|---|---|---|
| **1 minuto** (recomendado) | **15** | Melhor equilíbrio: vê as zonas 15m e aproxima 15s |
| **5 minutos** | **3** | Mesmo contexto 15m; micro 15s é mais grosseiro |
| **15 segundos** | **60** | Use `NTBot_WyckoffCorr_15s.txt` |

O NTSL **não lista** intervalo `itSecond` na função `Asset()` (há `itMinute`, `itMinute5`, `itMinute15`, …). Por isso o indicador **não pede 15s a partir de um gráfico de 1m**. A abordagem mais estável para usuários Profit é:

1. **Padrão (1m e 5m):** agregar **15 minutos** com `BarrasMacro` e estimar microestrutura de **15s** com **4 quartos da barra de 1m** (`UsarProxy15s = True`).
2. **Se a sua versão tiver gráfico 15s:** aplique também o companheiro `NTBot_WyckoffCorr_15s` no 15s (confirmação nativa). As regiões de demanda/oferta referem o **mesmo bloco de 15 minutos**.

Não use `Asset("TICKER", …, itMinute15)` neste indicador: o ticker teria de ser `input`/`const` fixo e quebraria WIN, WDO, ações, etc.

---

## O que o indicador mostra

Conceito alinhado ao motor Wyckoff do NTBot (acumulação, distribuição, markup, markdown, spring, upthrust, SOS/SOW, climax), adaptado a NTSL.

### Regiões de correlação (15s × 15m)

Uma **região operacional** aparece quando a estrutura **micro** (15s real ou proxy) **concorda** com a fase **macro de 15 minutos**:

| Cor no gráfico | Significado |
|---|---|
| Verde claro (candle + banda `NTBOT_WYCK_DEMAND`) | **Demanda / compra** — micro mostra spring, SOS ou pressão compradora **e** o 15m está em acumulação ou markup |
| Vermelho claro (candle + banda `NTBOT_WYCK_SUPPLY`) | **Oferta / venda** — micro mostra upthrust, SOW ou pressão vendedora **e** o 15m está em distribuição ou markdown |

Linhas cinza: teto e piso do range (`NTBOT_WYCK_MACRO_HIGH` / `NTBOT_WYCK_MACRO_LOW`) — contexto 15m projetado no 1m/5m.

Rótulos (se `MostrarRotulos = True`): `Regiao DEMANDA`, `Regiao OFERTA`, `Spring demanda`, `Upthrust oferta`, `SOS forca`, `SOW fraqueza`, fases 15m.

### Parâmetros

| Input | Padrão | Função |
|---|---|---|
| `Lookback` | 48 | Janela para range e eventos |
| `Sensibilidade` | 1.0 | Maior = menos sinais (movimento/ATR mais exigente) |
| `MostrarRotulos` | True | Textos no gráfico |
| `UsarConfirmacao15m` | True | Só pinta região se a fase 15m confirmar |
| `BarrasMacro` | 15 | Quantas barras do gráfico = **15 minutos** |
| `UsarProxy15s` | True | No 1m/5m, usa pavio/quartos da barra como micro 15s |

No 5m, defina `BarrasMacro = 3`. No 15s, use o arquivo companheiro (`BarrasMacro = 60`).

---

## Metodologia 15s × 15m no 1m e no 5m

```
15 minutos  = 15 barras de 1m  = 3 barras de 5m  = 60 barras de 15s
15 segundos = 1/4 da barra de 1m (proxy)        = 1 barra nativa no TF 15s
```

1. **Macro 15m:** OHLC agregado das últimas `BarrasMacro` barras; fase por tendência (markup/markdown) ou range (acumulação/distribuição), como no `WyckoffEngine` do NTBot.
2. **Micro 15s:** no gráfico 15s, cada candle; no 1m, pavios e direção intra-barra (quatro quartos); no 5m o proxy é mais fraco — prefira 1m ou o companheiro 15s.
3. **Correlação:** só destaca região se micro e macro apontam o mesmo lado (demanda na alta estrutural, oferta na baixa estrutural).

Isso permite **ver no 1m e no 5m** as zonas do contexto de 15 minutos sem abrir obrigatoriamente um gráfico 15s.

---

## Se a compilação falhar

Versões do Profit diferem um pouco na NTSL:

- Troque `RGB(r,g,b)` por `clVerdeClaro` / `clVermelho` se `RGB` não existir.
- Se `PlotText` não existir, desligue `MostrarRotulos` ou apague o bloco de rótulos; `PaintBar` + `Plot`/`Plot2`/`Plot3`/`Plot4` bastam.
- Se `SetPlotColor` falhar, remova essas linhas e pinte os plots na janela de propriedades do indicador.
- Use `Volume` ou, se o compilador pedir, `Quantidade`.
- O tipo deve ser **Indicador**, não estratégia de execução (não há `BuyAtMarket`).

Documentação oficial Nelogica: [Documentação NTSL](https://ajuda.nelogica.com.br/hc/pt-br/articles/360046443212-Documenta%C3%A7%C3%A3o-NTSL-Compilado-de-fun%C3%A7%C3%B5es-e-instru%C3%A7%C3%B5es-de-usabilidade) e [Editor de Estratégias](https://ajuda.nelogica.com.br/hc/pt-br/articles/9165042993691-Editor-de-Estrat%C3%A9gias-Crie-estrat%C3%A9gias-pr%C3%B3prias-atrav%C3%A9s-do-Profit).

---

## O que isto não faz

- Não altera indicadores em `MT5/`.
- Não acessa a API do NTBot; é análise local no gráfico Profit.
- Não substitui o score Wyckoff da boletagem / Trading Intelligence — apenas reutiliza a mesma ideia de fases e eventos.
