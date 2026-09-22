# Indicadores (NinjaTrader 8)



Pasta de **indicadores NinjaScript** do NTBot. Não misturar com `profit/` (Profit Chart).



| Indicador | Descrição |

|-----------|-----------|

| [MNQInstitutionalFlowAnalyzer](MNQInstitutionalFlowAnalyzer/README.md) | Fluxo institucional MNQ: Wyckoff + Bid/Ask + delta + absorção + correlação NQ/ES/RTY |

| [MNQAggressionDollarBalance](MNQAggressionDollarBalance/README.md) | Saldo de agressão por volume financeiro (Compra Ask − Venda Bid) |



## Instalação (obrigatório)



**Guia completo (passo a passo PT-BR):**  

[MNQInstitutionalFlowAnalyzer/README-INSTALACAO-NT8.md](MNQInstitutionalFlowAnalyzer/README-INSTALACAO-NT8.md)



### Layout no Custom (crítico)



O arquivo principal deve ser **flat** sob `Indicators\` (mesmo nível que `@ADL.cs`). Engines ficam na **subpasta**:



```

Documents\NinjaTrader 8\bin\Custom\Indicators\

  MNQInstitutionalFlowAnalyzer.cs              ← arquivo (lista de indicadores)

  MNQInstitutionalFlowAnalyzer\Engines\...     ← só helpers (não registra indicador)

```



Nesta máquina (OneDrive):  

`C:\Users\dougl\OneDrive\Documentos\NinjaTrader 8\bin\Custom\Indicators\`



**Nunca** deixe o `.cs` principal só dentro da subpasta — o NT8 não mostra o indicador na lista Available.



### Deploy recomendado



```powershell

cd C:\Projetos\ntbot\Indicadores\MNQInstitutionalFlowAnalyzer

powershell -ExecutionPolicy Bypass -File .\deploy-to-nt8.ps1

```



**Nunca** copie para o Custom: `Core/`, `Tests/`, `obj/`, `bin/`, `.csproj`, `AssemblyInfo` gerado, `MEAI.cs` vazio.



**CS0579** = segundo AssemblyInfo no Custom. Oficial: só `Custom\AssemblyInfo.cs`. Qualquer outro em `Core\obj\...` é duplicata — apague. Use o script de deploy.



Depois: NinjaScript Editor → Compile (F5) → gráfico MNQ → Indicators → buscar `MNQ` ou `Institutional`.


