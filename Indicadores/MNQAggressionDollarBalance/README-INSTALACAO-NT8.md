# Passo a passo — instalar MNQAggressionDollarBalance no NinjaTrader 8

Siga **na ordem**. O indicador só aparece na lista se o arquivo principal for um `.cs` **flat** (mesmo nível que `@ADL.cs`).

**Caminho Custom nesta máquina (OneDrive):**

`C:\Users\dougl\OneDrive\Documentos\NinjaTrader 8\bin\Custom\`

(Em outras PCs: `Documents\NinjaTrader 8\bin\Custom\`.)

**Layout correto:**

```
Custom\Indicators\
  MNQAggressionDollarBalance.cs          ← ARQUIVO (flat)
  MNQAggressionDollarBalance\            ← PASTA (só Engines)
    Engines\
  MNQInstitutionalFlowAnalyzer.cs        ← outro indicador (não mexer)
  @ADL.cs
  ...
```

---

## A. Fechar o NinjaTrader

Feche todas as janelas e confirme no Gerenciador de Tarefas que não há processo `NinjaTrader`.

---

## B. Deploy do repositório

```powershell
cd C:\Projetos\ntbot\Indicadores\MNQAggressionDollarBalance
powershell -ExecutionPolicy Bypass -File .\deploy-to-nt8.ps1
```

Confirme no Explorer:

1. Arquivo `Indicators\MNQAggressionDollarBalance.cs` (ao lado de `@ADL.cs`)
2. Pasta `Indicators\MNQAggressionDollarBalance\Engines\` com `DollarAggressionEngine.cs`

---

## C. Compilar (F5)

1. Abra o NinjaTrader 8 → **New** → **NinjaScript Editor**
2. Em Indicators: pasta + **script** `MNQAggressionDollarBalance`
3. Abra o **script** → **Compile** (F5)
4. Aguarde *compiled successfully*

---

## D. Adicionar no gráfico

1. Gráfico **MNQ**
2. Botão direito → **Indicators**
3. Buscar `MNQ` / `Aggression` / `Dollar`
4. Selecione **MNQAggressionDollarBalance** → OK

---

## E. Tick Replay

- Agressão Bid/Ask usa `OnMarketData`.
- Histórico **sem Tick Replay** → `DATA QUALITY: LOW` (não fabrica Bid/Ask).
- Para estudar o passado: ative **Tick Replay** nas Data Series + `Calculate = On each tick`.

---

## Checklist

- [ ] NT8 fechado
- [ ] `deploy-to-nt8.ps1` com OK
- [ ] Flat `.cs` ao lado de `@ADL.cs`
- [ ] F5 → compiled successfully
- [ ] Indicador aparece na lista Available
