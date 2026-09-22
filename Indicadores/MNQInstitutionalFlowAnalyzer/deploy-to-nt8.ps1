# Copies ONLY NinjaScript .cs files into NT8 Custom. Removes Core/Tests/obj/csproj/AssemblyInfo junk first.
# Main indicator .cs goes FLAT under Indicators\ (NT8 wrapper requirement). Engines stay in subfolder.

$ErrorActionPreference = "Stop"

$src = $PSScriptRoot

$candidates = @(
    (Join-Path $env:USERPROFILE "Documents\NinjaTrader 8\bin\Custom\Indicators"),
    (Join-Path $env:USERPROFILE "OneDrive\Documentos\NinjaTrader 8\bin\Custom\Indicators")
)

$indicatorsRoot = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $indicatorsRoot) {
    throw "Pasta NinjaTrader 8\bin\Custom\Indicators nao encontrada. Abra o NT8 uma vez ou ajuste o caminho."
}

$engineRoot = Join-Path $indicatorsRoot "MNQInstitutionalFlowAnalyzer"
$flatMain = Join-Path $indicatorsRoot "MNQInstitutionalFlowAnalyzer.cs"
$nestedMain = Join-Path $engineRoot "MNQInstitutionalFlowAnalyzer.cs"

New-Item -ItemType Directory -Force -Path $engineRoot | Out-Null

foreach ($junk in @("Core", "Tests", "obj", "bin")) {
    $p = Join-Path $engineRoot $junk
    if (Test-Path -LiteralPath $p) {
        cmd /c "rmdir /s /q `"$p`""
        Write-Host "Removido: $p"
    }
}

Get-ChildItem -LiteralPath $engineRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -eq "AssemblyInfo.cs" -or
        $_.Extension -eq ".csproj" -or
        $_.Name -like "*AssemblyAttributes.cs" -or
        $_.Name -eq "README.md" -or
        $_.Name -eq "README-INSTALACAO-NT8.md" -or
        $_.Name -eq "deploy-to-nt8.ps1"
    } |
    ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
        Write-Host "Removido: $($_.FullName)"
    }

$meai = Join-Path $indicatorsRoot "MEAI.cs"
if (Test-Path -LiteralPath $meai) {
    Remove-Item -LiteralPath $meai -Force
    Write-Host "Removido wrapper vazio: $meai"
}

Copy-Item -LiteralPath (Join-Path $src "MNQInstitutionalFlowAnalyzer.cs") -Destination $flatMain -Force
if (Test-Path -LiteralPath $nestedMain) {
    Remove-Item -LiteralPath $nestedMain -Force
    Write-Host "Removido layout antigo: $nestedMain"
}

foreach ($dir in @("Engines", "Helpers", "Math", "Models")) {
    $from = Join-Path $src $dir
    $to = Join-Path $engineRoot $dir
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    Get-ChildItem -LiteralPath $from -Filter "*.cs" -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $to -Force
    }
}

if (-not (Test-Path -LiteralPath $flatMain)) {
    throw "FALHA: arquivo flat nao existe apos copy: $flatMain"
}

$adl = Join-Path $indicatorsRoot "@ADL.cs"
Write-Host ""
Write-Host "OK: indicador principal (FLAT) -> $flatMain"
Write-Host "OK: engines/helpers           -> $engineRoot"
Write-Host "OK: tamanho flat              -> $((Get-Item -LiteralPath $flatMain).Length) bytes"
if (Test-Path -LiteralPath $adl) {
    Write-Host "OK: mesmo nivel que @ADL.cs   -> SIM"
} else {
    Write-Host "AVISO: @ADL.cs nao encontrado (pasta Indicators pode estar incompleta)"
}
Write-Host ""
Write-Host "Proximos passos:"
Write-Host "  1. Feche o NinjaTrader por completo e reabra"
Write-Host "  2. New -> NinjaScript Editor"
Write-Host "  3. Em Indicators: deve haver PASTA e SCRIPT 'MNQInstitutionalFlowAnalyzer' (mesmo nivel que ADL)"
Write-Host "  4. Abra o SCRIPT (nao a pasta) -> Compile (F5)"
Write-Host "  5. Grafico MNQ -> Indicators -> buscar 'MNQ' ou 'Institutional'"
Write-Host "Nao altere Custom\AssemblyInfo.cs (oficial do NT8)."
Write-Host "Guia: Indicadores\MNQInstitutionalFlowAnalyzer\README-INSTALACAO-NT8.md"
