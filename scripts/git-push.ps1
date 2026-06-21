# Push manual — ignora GIT_SSH_COMMAND de sessões do Cursor/automação
$ErrorActionPreference = 'Stop'

if ($env:GIT_SSH_COMMAND) {
    Write-Host "Removendo GIT_SSH_COMMAND da sessao (chave temporaria inexistente)." -ForegroundColor Yellow
    Remove-Item Env:GIT_SSH_COMMAND
}

Set-Location (Join-Path $PSScriptRoot '..')
git push @args
