# 🚀 Guia de Instalação - NTBot Dashboard

## ⚠️ Requisitos do Sistema

### Node.js
- **Versão Mínima**: 20.19+ ou 22.12+
- **Versão Atual no Sistema**: 18.20.5 ❌ (DESATUALIZADA)

### ✅ Atualize o Node.js

#### Opção 1: Download Direto
```
https://nodejs.org/
Baixe a versão LTS (Long Term Support)
```

#### Opção 2: Via NVM (Node Version Manager)
```powershell
# Instalar NVM
https://github.com/coreybutler/nvm-windows

# Instalar Node 20
nvm install 20
nvm use 20

# Verificar
node --version
```

#### Opção 3: Via Chocolatey
```powershell
choco install nodejs-lts
```

---

## 📦 Instalação do Dashboard

### 1. Verificar Node.js
```powershell
node --version
# Deve mostrar v20.x.x ou v22.x.x
```

### 2. Instalar Dependências
```powershell
cd c:\Projetos\ntbot\ntbot-dashboard
npm install
```

### 3. Executar em Desenvolvimento
```powershell
npm run dev
```

**Dashboard estará disponível em**: http://localhost:3000

---

## 🐛 Troubleshooting

### Erro: "crypto.hash is not a function"
**Causa**: Node.js 18.x não suporta Vite 7.x
**Solução**: Atualize para Node.js 20+

### Erro: "EBADENGINE"
**Causa**: Versão incompatível do Node.js
**Solução**: Atualize para Node.js 20+

### Erro: "Cannot find module"
**Causa**: Dependências não instaladas
**Solução**:
```powershell
rm -rf node_modules
rm package-lock.json
npm install
```

---

## 🎯 Estrutura de Execução Completa

### Terminal 1: Backend API
```powershell
cd C:\Projetos\ntbot\src\NtBot.Api
dotnet run
```
✅ API rodando em: http://localhost:5053

### Terminal 2: Dashboard
```powershell
cd c:\Projetos\ntbot\ntbot-dashboard
npm run dev
```
✅ Dashboard rodando em: http://localhost:3000

---

## 🔧 Configuração Opcional

### 1. Variáveis de Ambiente
Crie um arquivo `.env` na pasta `ntbot-dashboard`:

```env
VITE_API_URL=http://localhost:5053
VITE_SIGNALR_HUB=/hubs/trading
VITE_DEFAULT_SYMBOL=MNQ
VITE_DEFAULT_TIMEFRAME=5m
```

### 2. Configurar Proxy (opcional)
Já está configurado no `vite.config.ts`:
```typescript
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5053'
    }
  }
}
```

---

## 📊 Features Disponíveis

### ✅ Já Implementado
- [x] Estrutura de projeto completa
- [x] TypeScript configurado
- [x] Tailwind CSS + tema dark
- [x] React Router v6
- [x] Zustand state management
- [x] Axios + SignalR services
- [x] PWA configurado
- [x] Layout principal
- [x] Dashboard page
- [x] Navigation sidebar
- [x] Real-time connection indicator

### 🚧 Em Desenvolvimento
- [ ] Componentes de gráfico (TradingView)
- [ ] Páginas de análise completas
- [ ] Gestão de sinais
- [ ] Gestão de trades
- [ ] Configurações avançadas

---

## 🎨 Temas e Estilos

### Classes Tailwind Personalizadas
```css
.btn-primary      /* Botão azul */
.btn-success      /* Botão verde */
.btn-danger       /* Botão vermelho */
.card             /* Card container */
.input            /* Input field */
.badge-success    /* Badge verde */
.table            /* Tabela estilizada */
```

### Cores do Sistema
```
Primary:  #0ea5e9 (azul)
Success:  #10b981 (verde)
Danger:   #ef4444 (vermelho)
Warning:  #f59e0b (laranja)
Background: #0f172a (slate-900)
```

---

## 🔌 API Endpoints Disponíveis

### Health Check
```
GET http://localhost:5053/api/health
```

### Analysis
```
GET http://localhost:5053/api/analysis/wyckoff/MNQ?timeframe=5m
GET http://localhost:5053/api/analysis/macro/MNQ
GET http://localhost:5053/api/analysis/complete/MNQ?timeframe=5m
```

### Tenants
```
GET http://localhost:5053/api/tenants
```

### Swagger UI
```
http://localhost:5053/swagger
```

---

## 📱 PWA Features

### Instalação
1. Abra o dashboard no Chrome/Edge
2. Clique no ícone de instalação na barra de endereço
3. Clique em "Instalar"

### Offline Mode
- Service Worker ativo
- Cache de assets estáticos
- NetworkFirst para API calls

### Manifest
- Nome: "NTBot Trading Dashboard"
- Ícones: 192x192, 512x512
- Display: standalone
- Theme color: #0f172a

---

## 🚀 Build para Produção

### 1. Build
```powershell
npm run build
```

Output: `dist/`

### 2. Preview
```powershell
npm run preview
```

### 3. Deploy
```powershell
# Copiar dist/ para servidor web
# Ou usar Docker (ver abaixo)
```

---

## 🐳 Docker Deploy

### Dockerfile (já incluído)
```dockerfile
FROM nginx:alpine
COPY dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### Build & Run
```powershell
# Build da imagem
docker build -t ntbot-dashboard .

# Executar
docker run -d -p 3000:80 ntbot-dashboard
```

---

## 📊 Status do Projeto

```
Dashboard Status: 65% Completo

[████████████████████░░░░░] 65%

✅ Estrutura:     100%
✅ Serviços:      100%
✅ State:         100%
✅ Routing:       100%
✅ Layout:        100%
🟡 Componentes:   40%
🟡 Páginas:       50%
⏳ Testes:        0%
```

---

## 🎯 Próximos Passos

1. **Atualizar Node.js** para versão 20+
2. **Executar `npm install`**
3. **Iniciar o dashboard** com `npm run dev`
4. **Garantir que a API** está rodando
5. **Acessar** http://localhost:3000

---

## 📞 Suporte

- **GitHub Issues**: Para bugs e features
- **Documentação**: README.md em cada pasta
- **API Docs**: Swagger UI do backend

---

## 🎓 Tecnologias

| Categoria | Tecnologia | Versão |
|-----------|------------|--------|
| Runtime | Node.js | 20.19+ |
| Framework | React | 19.2.0 |
| Language | TypeScript | 5.9.3 |
| Build Tool | Vite | 7.2.4 |
| Styling | Tailwind CSS | 3.4.1 |
| State | Zustand | 4.5.0 |
| Routing | React Router | 6.22.0 |
| Real-time | SignalR | 8.0.0 |
| HTTP | Axios | 1.6.7 |
| Charts | Lightweight Charts | 4.1.3 |
| Icons | Lucide React | 0.344.0 |
| PWA | Vite Plugin PWA | 0.19.0 |

---

**Desenvolvido para traders profissionais** 🚀📈

**NTBot - Automated Trading System with AI & Wyckoff Analysis**
