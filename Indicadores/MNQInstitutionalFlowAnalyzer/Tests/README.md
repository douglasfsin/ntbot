# Testes matemáticos (sem NinjaTrader)

As engines em `Engines/`, `Math/` e `Helpers/` não referenciam assemblies NT8.

- Projeto: `Core/NtBot.MnqFlow.Core.csproj` (netstandard2.0)
- xUnit: `tests/NtBot.UnitTests/MnqFlow/MnqFlowMathTests.cs`

```bash
dotnet test tests/NtBot.UnitTests/NtBot.UnitTests.csproj --filter FullyQualifiedName~MnqFlow
```

`MNQInstitutionalFlowAnalyzer.cs` só compila no NinjaScript Editor. Não copie `Core/` nem `Tests/` para `bin\Custom`.
