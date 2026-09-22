# NinjaTrader 8 — specs neste repo

| Arquivo | Uso |
|---------|-----|
| [MNQInstitutionalFlowAnalyzer-prompt.md](MNQInstitutionalFlowAnalyzer-prompt.md) | Spec-fonte do indicador NT8 |
| Implementação | `Indicadores/MNQInstitutionalFlowAnalyzer/` |

## Copiar para o Custom (NT8)

Instale só os `.cs` do indicador + engines (`MNQInstitutionalFlowAnalyzer.cs`, `Engines/`, `Helpers/`, `Math/`, `Models/`).

**Não** copie `Core/` (`.csproj`, `obj/`, `AssemblyInfo` gerado), `Tests/` nem `AssemblyInfo.cs`. O Editor do NT8 já gera atributos de assembly; arquivos extras geram **CS0579**. Passo a passo: `Indicadores/MNQInstitutionalFlowAnalyzer/README.md`.
