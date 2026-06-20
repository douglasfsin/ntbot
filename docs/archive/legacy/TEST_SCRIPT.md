# 🧪 Script de Teste Completo - NTBot

## Execute este script para validar toda a implementação

### Teste 1: Verificar Instalações

```powershell
# Verificar .NET SDK
Write-Host "==> Verificando .NET SDK..." -ForegroundColor Cyan
dotnet --version
if ($LASTEXITCODE -eq 0) { Write-Host "✓ .NET OK" -ForegroundColor Green } else { Write-Host "✗ .NET não encontrado" -ForegroundColor Red }

# Verificar SQL Server
Write-Host "`n==> Verificando SQL Server..." -ForegroundColor Cyan
$sqlService = Get-Service -Name "MSSQLSERVER","MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
if ($sqlService) { 
    Write-Host "✓ SQL Server OK ($($sqlService.Name) - $($sqlService.Status))" -ForegroundColor Green 
} else { 
    Write-Host "✗ SQL Server não encontrado" -ForegroundColor Red 
}

# Verificar EF Tools
Write-Host "`n==> Verificando EF Tools..." -ForegroundColor Cyan
dotnet ef --version
if ($LASTEXITCODE -eq 0) { Write-Host "✓ EF Tools OK" -ForegroundColor Green } else { Write-Host "✗ EF Tools não instalado" -ForegroundColor Red }
```

### Teste 2: Compilação

```powershell
Write-Host "`n==> Compilando projeto..." -ForegroundColor Cyan
cd c:\Projetos\ntbot\NTBot.Api
dotnet restore
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Compilação OK" -ForegroundColor Green
} else {
    Write-Host "✗ Erro na compilação" -ForegroundColor Red
    exit 1
}
```

### Teste 3: Database

```powershell
Write-Host "`n==> Criando Database..." -ForegroundColor Cyan

# Criar migration
dotnet ef migrations add InitialCreate --force

# Aplicar no banco
dotnet ef database update

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Database criado com sucesso" -ForegroundColor Green
} else {
    Write-Host "✗ Erro ao criar database" -ForegroundColor Red
    Write-Host "Dica: Verifique se SQL Server está rodando e connection string no appsettings.json" -ForegroundColor Yellow
}
```

### Teste 4: Rodar API

```powershell
Write-Host "`n==> Iniciando API..." -ForegroundColor Cyan
Write-Host "Aguarde alguns segundos para a API iniciar..." -ForegroundColor Yellow

# Inicia API em background
Start-Process powershell -ArgumentList "-Command", "cd c:\Projetos\ntbot\NTBot.Api; dotnet run" -WindowStyle Minimized

# Aguarda 10 segundos
Start-Sleep -Seconds 10
```

### Teste 5: Health Check

```powershell
Write-Host "`n==> Testando Health Check..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5053/api/health" -Method Get
    Write-Host "✓ Health Check OK" -ForegroundColor Green
    Write-Host "Status: $($response.status)" -ForegroundColor Cyan
    Write-Host "Version: $($response.version)" -ForegroundColor Cyan
} catch {
    Write-Host "✗ Health Check falhou" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Yellow
}
```

### Teste 6: Tenants

```powershell
Write-Host "`n==> Testando endpoint Tenants..." -ForegroundColor Cyan

try {
    $tenants = Invoke-RestMethod -Uri "http://localhost:5053/api/tenants" -Method Get
    Write-Host "✓ Tenants OK - Encontrados: $($tenants.Count)" -ForegroundColor Green
    
    if ($tenants.Count -gt 0) {
        Write-Host "Primeiro tenant: $($tenants[0].name) ($($tenants[0].plan))" -ForegroundColor Cyan
    }
} catch {
    Write-Host "✗ Erro ao buscar tenants" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Yellow
}
```

### Teste 7: Swagger

```powershell
Write-Host "`n==> Abrindo Swagger UI..." -ForegroundColor Cyan
Start-Process "http://localhost:5053"
Write-Host "✓ Swagger deve abrir no navegador" -ForegroundColor Green
```

---

## Script Completo (Copy & Paste)

Copie e cole este script inteiro no PowerShell:

```powershell
# NTBot - Script de Teste Completo
# Execute este script para validar toda a instalação

Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║   🤖 NTBot - Sistema de Trading Automatizado               ║
║                                                              ║
║   Script de Validação Completa                              ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# 1. Verificações
Write-Host "`n[1/7] Verificando pré-requisitos..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

# .NET
Write-Host ".NET SDK: " -NoNewline
$dotnetVersion = dotnet --version 2>&1
if ($LASTEXITCODE -eq 0) { 
    Write-Host "✓ $dotnetVersion" -ForegroundColor Green 
} else { 
    Write-Host "✗ Não instalado" -ForegroundColor Red
    exit 1
}

# SQL Server
Write-Host "SQL Server: " -NoNewline
$sqlService = Get-Service -Name "MSSQLSERVER","MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
if ($sqlService) { 
    Write-Host "✓ $($sqlService.Name) - $($sqlService.Status)" -ForegroundColor Green 
} else { 
    Write-Host "✗ Não encontrado" -ForegroundColor Red
    Write-Host "   Instale SQL Server Express: https://aka.ms/ssmsfullsetup" -ForegroundColor Yellow
    exit 1
}

# EF Tools
Write-Host "EF Core Tools: " -NoNewline
$efVersion = dotnet ef --version 2>&1
if ($LASTEXITCODE -eq 0) { 
    Write-Host "✓ Instalado" -ForegroundColor Green 
} else { 
    Write-Host "⚠ Não instalado - Instalando..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# 2. Compilação
Write-Host "`n[2/7] Compilando projeto..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

Set-Location c:\Projetos\ntbot\NTBot.Api
dotnet restore --verbosity quiet
$buildResult = dotnet build --verbosity quiet

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Compilação OK" -ForegroundColor Green
} else {
    Write-Host "✗ Erro na compilação" -ForegroundColor Red
    exit 1
}

# 3. Database
Write-Host "`n[3/7] Configurando Database..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

# Remove migrations antigas se existirem
Remove-Item -Path "Migrations" -Recurse -Force -ErrorAction SilentlyContinue

# Cria nova migration
Write-Host "Criando migration... " -NoNewline
dotnet ef migrations add InitialCreate --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓" -ForegroundColor Green
} else {
    Write-Host "✗" -ForegroundColor Red
    exit 1
}

# Aplica migration
Write-Host "Aplicando migration... " -NoNewline
dotnet ef database update --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓" -ForegroundColor Green
} else {
    Write-Host "✗" -ForegroundColor Red
    Write-Host "   Verifique connection string em appsettings.json" -ForegroundColor Yellow
    exit 1
}

# 4. Iniciar API
Write-Host "`n[4/7] Iniciando API..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

Write-Host "Iniciando servidor... " -NoNewline
$apiProcess = Start-Process powershell -ArgumentList "-Command", "cd c:\Projetos\ntbot\NTBot.Api; dotnet run" -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 8
Write-Host "✓" -ForegroundColor Green

# 5. Health Check
Write-Host "`n[5/7] Testando Health Check..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

$maxRetries = 3
$retryCount = 0
$healthOk = $false

while ($retryCount -lt $maxRetries -and -not $healthOk) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5053/api/health" -Method Get -TimeoutSec 5
        Write-Host "✓ API respondendo" -ForegroundColor Green
        Write-Host "  Status: $($response.status)" -ForegroundColor DarkGray
        Write-Host "  Version: $($response.version)" -ForegroundColor DarkGray
        $healthOk = $true
    } catch {
        $retryCount++
        if ($retryCount -lt $maxRetries) {
            Write-Host "⚠ Tentativa $retryCount/$maxRetries falhou, aguardando..." -ForegroundColor Yellow
            Start-Sleep -Seconds 3
        } else {
            Write-Host "✗ Health Check falhou após $maxRetries tentativas" -ForegroundColor Red
            Stop-Process -Id $apiProcess.Id -Force
            exit 1
        }
    }
}

# 6. Tenants
Write-Host "`n[6/7] Testando Tenants..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

try {
    $tenants = Invoke-RestMethod -Uri "http://localhost:5053/api/tenants" -Method Get
    Write-Host "✓ Endpoint OK - Tenants: $($tenants.Count)" -ForegroundColor Green
    
    if ($tenants.Count -gt 0) {
        $tenant = $tenants[0]
        Write-Host "  Nome: $($tenant.name)" -ForegroundColor DarkGray
        Write-Host "  Plano: $($tenant.plan)" -ForegroundColor DarkGray
        Write-Host "  Assets: $($tenant.assetConfigurations.Count)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "✗ Erro ao buscar tenants" -ForegroundColor Red
}

# 7. Swagger
Write-Host "`n[7/7] Abrindo Swagger UI..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor DarkGray

Start-Sleep -Seconds 2
Start-Process "http://localhost:5053"
Write-Host "✓ Swagger UI aberto no navegador" -ForegroundColor Green

# Resumo
Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║   ✅ VALIDAÇÃO COMPLETA!                                    ║
║                                                              ║
║   O sistema está rodando em:                                ║
║   http://localhost:5053                                     ║
║                                                              ║
║   Próximos passos:                                          ║
║   1. Testar endpoints no Swagger                            ║
║   2. Implementar Economic Calendar Service                  ║
║   3. Implementar Decision Engine                            ║
║   4. Rodar primeiro backtest                                ║
║                                                              ║
║   Documentação completa:                                    ║
║   - README.md                                               ║
║   - QUICK_START.md                                          ║
║   - GETTING_STARTED.md                                      ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Green

Write-Host "`nPressione qualquer tecla para fechar ou Ctrl+C para manter API rodando..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Cleanup
Write-Host "`nParando API..." -ForegroundColor Cyan
Stop-Process -Id $apiProcess.Id -Force
Write-Host "✓ API encerrada" -ForegroundColor Green
```

---

## Comandos Individuais (Para Debug)

Se algo falhar, rode os comandos individualmente:

```powershell
# Navegar para o projeto
cd c:\Projetos\ntbot\NTBot.Api

# Limpar e reconstruir
dotnet clean
dotnet restore
dotnet build

# Recriar database do zero
Remove-Item -Path "Migrations" -Recurse -Force
dotnet ef database drop --force
dotnet ef migrations add InitialCreate
dotnet ef database update

# Rodar com logs detalhados
dotnet run --verbosity detailed

# Verificar se API está respondendo
curl http://localhost:5053/api/health
```

---

## Testes Manuais no Swagger

Após abrir http://localhost:5053, teste:

1. **GET /api/health** → Deve retornar `{"status": "healthy"}`
2. **GET /api/tenants** → Deve retornar array com 1 tenant
3. **GET /api/tenants/{id}** → Use o ID do tenant anterior
4. **POST /api/tenants** → Crie um novo tenant
5. **GET /api/analysis/macro/MNQ** → Teste análise macro (vai falhar sem dados reais de NT)

---

## Troubleshooting

### Problema: "Port 5053 already in use"

```powershell
# Encontrar processo
netstat -ano | findstr :5053

# Matar processo (substitua <PID>)
taskkill /PID <PID> /F
```

### Problema: "Unable to connect to SQL Server"

```powershell
# Verificar serviço
Get-Service -Name MSSQLSERVER,MSSQL*

# Iniciar serviço
net start MSSQLSERVER
# OU
net start MSSQL$SQLEXPRESS

# Testar conexão
sqlcmd -S localhost -E -Q "SELECT @@VERSION"
```

### Problema: Migration errors

```powershell
# Limpar tudo e recomeçar
Remove-Item -Path "Migrations" -Recurse -Force
dotnet ef database drop --force
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

**Última atualização:** 28/12/2025  
**Testado em:** Windows 11, .NET 8.0, SQL Server 2022
