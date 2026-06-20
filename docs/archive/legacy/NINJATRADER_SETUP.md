# Configuração NinjaTrader API

## 📋 Checklist de Configuração

### 1. Habilitar API REST no NinjaTrader

#### Caminho 1: Via Options (Mais Comum)
```
NinjaTrader 8
  → Tools
    → Options
      → Automated Trading Interface (ATI)
        → ☑️ Enable outbound connection
        → ☑️ Enable inbound connection  
        → Port: 8080
        → ☑️ Allow external connections (se disponível)
```

#### Caminho 2: Via Add-on
Se não tiver opção nativa, instale:
- **NinjaTrader REST API** (buscar no NinjaTrader Ecosystem)
- **ATI (Automated Trading Interface)**

### 2. Configurar Firewall Windows

Permitir porta 8080:

```powershell
# Abrir PowerShell como Administrador
New-NetFirewallRule -DisplayName "NinjaTrader API" -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow
```

### 3. Verificar Configuração

Testar conexão:

```powershell
# PowerShell
Invoke-WebRequest -Uri "http://localhost:8080" -Method GET

# Ou no navegador
# http://localhost:8080
```

### 4. Configuração no NTBot

Arquivo: `appsettings.json`

```json
{
  "NinjaTrader": {
    "ApiBaseUrl": "http://localhost:8080",
    "Timeout": 30,
    "RetryAttempts": 3
  }
}
```

## 🔍 Troubleshooting

### Problema: "Connection refused"

**Solução:**
1. Verificar se NinjaTrader está rodando
2. Verificar se API REST está habilitada
3. Verificar porta correta (8080)
4. Verificar firewall

### Problema: "API not found"

**Solução:**
1. Instalar Add-on REST API do NinjaTrader
2. Reiniciar NinjaTrader após instalar

### Problema: "Unauthorized"

**Solução:**
1. Configurar autenticação no NinjaTrader
2. Adicionar token/credentials no `appsettings.json`

## 📚 Documentação Oficial

- [NinjaTrader Developer Documentation](https://ninjatrader.com/support/helpGuides/nt8/)
- [NinjaTrader REST API Guide](https://ninjatrader.com/support/helpGuides/nt8/automated_trading_interface.htm)

## 🎯 Endpoints Disponíveis

Após habilitar, você terá acesso a:

```
GET  /api/accounts          - Lista de contas
GET  /api/positions         - Posições abertas
POST /api/orders           - Criar ordem
GET  /api/orders           - Listar ordens
GET  /api/instruments      - Instrumentos disponíveis
GET  /api/marketdata       - Dados de mercado
```

## ⚙️ Alternativas

Se NinjaTrader não tiver API REST nativa:

### Opção 1: NinjaScript Custom
Criar um NinjaScript addon que exponha endpoints HTTP

### Opção 2: InteractiveBrokers TWS API
Usar Interactive Brokers como alternativa

### Opção 3: CQG, Rithmic ou outras plataformas
Avaliar outras plataformas com APIs REST nativas

## 📞 Suporte

Se precisar de ajuda:
1. Abrir ticket no suporte do NinjaTrader
2. Verificar fóruns da comunidade NinjaTrader
3. Consultar documentação do desenvolvedor

## ✅ Checklist Final

Antes de rodar o NTBot:

- [ ] NinjaTrader 8 instalado
- [ ] API REST habilitada
- [ ] Porta 8080 aberta no firewall
- [ ] Conexão testada e funcionando
- [ ] `appsettings.json` configurado
- [ ] Conta demo/live configurada no NinjaTrader
- [ ] Símbolos adicionados (ES, NQ, etc)
