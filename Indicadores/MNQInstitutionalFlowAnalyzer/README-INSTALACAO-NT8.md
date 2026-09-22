# Passo a passo — instalar MNQInstitutionalFlowAnalyzer no NinjaTrader 8

Siga **na ordem**. Não pule etapas. O indicador só aparece na lista se o arquivo principal for um `.cs` **flat** (mesmo nível que `@ADL.cs`), **não** só dentro da pasta de engines.

**Caminho Custom nesta máquina (OneDrive):**

`C:\Users\dougl\OneDrive\Documentos\NinjaTrader 8\bin\Custom\`

(Em outras PCs: `Documents\NinjaTrader 8\bin\Custom\` ou `OneDrive\Documentos\NinjaTrader 8\bin\Custom\`.)

**Layout correto (obrigatório):**

```
Custom\Indicators\
  MNQInstitutionalFlowAnalyzer.cs          ← ARQUIVO (flat) — gera o indicador na lista
  MNQInstitutionalFlowAnalyzer\            ← PASTA (só engines/helpers)
    Engines\
    Helpers\
    Math\
    Models\
  @ADL.cs
  @ADX.cs
  ...
```

Se existir **só** a pasta e **não** o `.cs` flat ao lado de `@ADL`, o NT8 **não** registra o indicador.

---

## A. Fechar o NinjaTrader completamente

1. Feche **todas** as janelas do NinjaTrader 8 (gráficos, Control Center, NinjaScript Editor).
2. Confirme no Gerenciador de Tarefas que não há processo `NinjaTrader` / `NinjaTrader.Editor` rodando.
3. Só depois continue.

---

## B. Limpar lixo no Custom (ou pule para C)

Na pasta  
`...\Custom\Indicators\MNQInstitutionalFlowAnalyzer\`  
**apague**, se existirem:

- pastas `Core`, `Tests`, `obj`, `bin`
- arquivos `*.csproj`
- qualquer `AssemblyInfo.cs` / `*AssemblyAttributes.cs` **dentro** dessa pasta
- `MEAI.cs` em `Indicators\` (wrapper vazio — confunde o nome)
- `README.md`, `deploy-to-nt8.ps1` (não devem ficar no Custom)

**NÃO apague** o oficial:

`Custom\AssemblyInfo.cs` (raiz do Custom)

**NÃO** deixe o principal aninhado:

`Indicators\MNQInstitutionalFlowAnalyzer\MNQInstitutionalFlowAnalyzer.cs` ← apague se existir (layout antigo)

---

## C. Preferido: rodar o deploy do repositório

No PowerShell:

```powershell
cd C:\Projetos\ntbot\Indicadores\MNQInstitutionalFlowAnalyzer
powershell -ExecutionPolicy Bypass -File .\deploy-to-nt8.ps1
```

O script deve imprimir algo como:

```text
OK: indicador principal -> ...\Indicators\MNQInstitutionalFlowAnalyzer.cs
OK: engines/helpers -> ...\Indicators\MNQInstitutionalFlowAnalyzer
```

**Confirme no Explorer do Windows** (não só no NT8):

1. Abra `...\Custom\Indicators\`
2. Deve existir o **arquivo** `MNQInstitutionalFlowAnalyzer.cs` (ícone de arquivo, tamanho ~30 KB)
3. Deve existir a **pasta** `MNQInstitutionalFlowAnalyzer` com `Engines`, `Helpers`, `Math`, `Models`
4. O arquivo flat fica no **mesmo nível** que `@ADL.cs` e `@ADX.cs`

Se o arquivo flat **não** existir, o deploy falhou — corrija o caminho e rode de novo. Não abra o NT8 ainda.

---

## D. Abrir NinjaTrader → NinjaScript Editor

1. Abra o NinjaTrader 8.
2. Menu **New** → **NinjaScript Editor**.

---

## E. Confirmar no Explorer do NinjaScript (crítico)

Na árvore à esquerda, expanda **Indicators**.

Você deve ver **dois** itens com nome parecido:

| Item | Tipo | O que contém |
|------|------|----------------|
| `MNQInstitutionalFlowAnalyzer` | **Pasta** (ícone de pasta) | Engines, Helpers, Math, Models |
| `MNQInstitutionalFlowAnalyzer` | **Script** (ícone de arquivo/.cs) | O indicador principal |

O **script** (arquivo) deve estar no **mesmo nível** que `ADL` / `ADX` (indicadores `@`), **não** só dentro da pasta.

- Se só aparecer a **pasta** com Engines/Helpers: o flat `.cs` não está no disco ou o Editor não atualizou → volte a **A**, rode **C**, confirme no Windows Explorer, reabra o NT8.
- Editor no centro **preto/vazio** é normal até você **abrir** o arquivo do script (clique no item script, não na pasta).

---

## F. Abrir o arquivo e compilar (F5)

1. No Explorer, clique no **script** `MNQInstitutionalFlowAnalyzer` (não na pasta).
2. O código deve aparecer no editor (classe `MNQInstitutionalFlowAnalyzer : Indicator`).
3. Menu **Compile** ou tecla **F5**.
4. Aguarde a mensagem de sucesso (ex.: *NinjaScript code compiled successfully* / *compiled successfully*).
5. Após compilar com sucesso, o NT8 costuma acrescentar no final do `.cs` a região `#region NinjaScript generated code` — **não remova** esse bloco.

---

## G. Se der erro de compilação — ler o Log

1. Control Center → aba **Log** (ou Output do NinjaScript Editor).
2. Anote o código (ex.: `CS0579`, `CS0246`) e o **caminho do arquivo**.
3. Causas comuns:
   - **CS0579** (AssemblyInfo duplicado): apague `AssemblyInfo` / `AssemblyAttributes` **dentro** da pasta do indicador / `obj` / `Core`. Mantenha só `Custom\AssemblyInfo.cs`.
   - Pasta `Core` ou `Tests` copiada por engano → apague e rode o deploy de novo.
   - `.csproj` dentro do Custom do indicador → apague.
4. Corrija → F5 de novo até compilar com sucesso.

---

## H. Adicionar no gráfico MNQ

1. Abra um gráfico do **MNQ** (contínuo ou front month).
2. Clique com o botão direito no gráfico → **Indicators**.
3. Na caixa de busca / Available, digite **`MNQ`** ou **`Institutional`**.
4. Selecione **`MNQInstitutionalFlowAnalyzer`** na **lista raiz** (junto com EMA, SMA, etc. — **não** dentro de uma subpasta de engines).
5. OK / Apply.

Se compilou com sucesso e o flat `.cs` existe, o nome deve aparecer. Se não aparecer: Log do Control Center ao abrir a lista; confira se não há `MEAI.cs` com o mesmo `Name`.

---

## I. Tick Replay (order flow / Bid-Ask)

- Agressão Bid/Ask usa `OnMarketData` (Last no ask = compra agressora; Last no bid = venda).
- Em histórico **sem Tick Replay**, o indicador **não inventa** Bid/Ask: mostra qualidade baixa (`DATA QUALITY: LOW`) e reduz peso de order flow.
- Para estudar agressão no passado: nas Data Series do gráfico, ative **Tick Replay** e use `Calculate = On each tick` (padrão do indicador).

---

## J. Instrumentos de correlação (formato NT8)

Nos parâmetros do indicador (grupo **Instruments**), use o **nome completo** do instrumento no NinjaTrader, por exemplo:

- `NQ 09-26`
- `ES 09-26`
- `RTY 09-26`

**Não** use só `NQ` / `ES` / `RTY` (sem mês) sem `AutoMatchCorrelationExpiry`.

Com **AutoMatchCorrelationExpiry = true** (padrão), o gráfico `MNQ 09-26` resolve automaticamente:
- `NQ` → `NQ 09-26`
- `ES` → `ES 09-26`
- `RTY` → `RTY 09-26`

Ou digite o contrato completo NT8: `NQ 09-26`, `ES 09-26`.

No dashboard, a linha **CORR SERIES** mostra `NQ 09-26=OK` ou `=FAIL`. Se tudo for FAIL, a correlação fica em 0.

Ajuste o mês/ano ao contrato que você opera. Desligue instrumentos que não quiser com `EnableInstrument1`…`4`.

---

## K. Apêndice — remover AltaRenda (opcional)

A pasta **AltaRenda** na lista de Indicators vem de scripts de terceiros em `Custom\Indicators\AltaRenda\`. **Não** faz parte deste repositório.

1. Backup da pasta `Custom` inteira.
2. Feche o NinjaTrader.
3. Apague `Custom\Indicators\AltaRenda\`.
4. Se existir, remova linhas `Indicators\AltaRenda\...` de `Custom\NinjaTrader.Custom.csproj`.
5. Abra NT8 → NinjaScript Editor → Compile (F5).

**Não** apague pastas/arquivos que começam com `@` (indicadores oficiais).

---

## Checklist rápido

- [ ] NT8 fechado
- [ ] `deploy-to-nt8.ps1` rodou com OK
- [ ] Windows: existe `Indicators\MNQInstitutionalFlowAnalyzer.cs` **ao lado** de `@ADL.cs`
- [ ] Windows: pasta só com Engines/Helpers/Math/Models (sem Core/Tests/obj)
- [ ] NinjaScript Explorer: script flat + pasta (dois itens)
- [ ] F5 → compiled successfully
- [ ] Gráfico MNQ → Indicators → busca MNQ/Institutional → achou na lista raiz
