# MISSÃO

Você é um Principal Software Architect especializado em:

.NET 9
Windows Forms
Background Services
SignalR
Clean Architecture
DDD
CQRS
Redis
PostgreSQL
QuestDB
System.Threading.Channels
DDE
Market Data Systems
Trading Platforms

Sua missão é transformar o projeto

NtBot.Connector.Windows

em um Market Data Gateway profissional e resiliente.

=========================================================
OBJETIVO
=========================================================

Remover dependência de:

- LibreOffice
- CSV
- XLSX
- Arquivos intermediários

Implementar leitura DDE nativa do Profit.

Os dados deverão ser consumidos diretamente do DDE e publicados para a API através de SignalR.

=========================================================
ARQUITETURA ALVO
=========================================================

Profit

↓

DDE

↓

ProfitDdeProvider

↓

Normalization Engine

↓

MarketDataBus

↓

Memory Cache

↓

SignalR

↓

NtBot.Api

↓

Redis

↓

Blazor

↓

Quant Engine

↓

Macro Engine

↓

Recommendation Engine

↓

AI Agents

=========================================================
PROJETO
=========================================================

Criar estrutura

NtBot.Connector.Windows

Providers

Profit

MT5

Tryd

NinjaTrader

Replay

Mock

Core

Engine

Configuration

SignalR

Cache

Workers

Monitoring

Diagnostics

Services

=========================================================
CONTRATOS
=========================================================

Criar

IMarketDataProvider

IMarketDataBus

IMarketDataPublisher

IProviderHealth

IProviderMonitor

=========================================================
MARKET TICK PADRONIZADO
=========================================================

Criar

MarketTick

Provider

Symbol

Timestamp

LastPrice

Bid

Ask

Volume

Open

High

Low

Close

Trades

Source

=========================================================
PROFIT DDE PROVIDER
=========================================================

Criar

ProfitDdeProvider

Responsabilidades:

Conectar ao DDE

Monitorar canais

Receber atualizações

Normalizar

Publicar eventos

Reconectar automaticamente

Validar saúde

Registrar logs

=========================================================
ATIVOS SUPORTADOS
=========================================================

WIN

WDO

IND

DOL

PETR4

VALE3

ITUB4

BBDC4

WEGE3

ABEV3

e qualquer ativo configurado.

=========================================================
CONFIGURAÇÃO
=========================================================

Criar tela

Configurações

↓

Market Data

↓

Profit DDE

Campos

Ativo

Servidor DDE

Heartbeat

Timeout

Reconexão automática

Máximo de tentativas

Batch Interval

Log Verbose

=========================================================
EVENT BUS
=========================================================

Criar

MarketDataBus

Utilizar

System.Threading.Channels

Proibido:

Thread.Sleep

Polling

Timer para leitura

=========================================================
Fluxo

Provider

↓

Channel

↓

Normalization

↓

Cache

↓

SignalR

=========================================================
CACHE
=========================================================

Criar

MarketDataCache

Utilizar

ConcurrentDictionary

Manter snapshot atual.

Nunca consultar DDE diretamente na UI.

=========================================================
NORMALIZATION ENGINE
=========================================================

Todos providers deverão retornar

MarketTick

independente da origem.

Profit

MT5

Tryd

Ninja

Replay

=========================================================
BATCH PUBLISHER
=========================================================

Criar

BatchPublisher

Objetivo

Agrupar atualizações.

Intervalo padrão

50 ms

Configuração dinâmica.

=========================================================
HEALTH ENGINE
=========================================================

Criar

ProviderHealthEngine

Monitorar

Último Tick

Heartbeat

Latência

Tempo sem atualização

Reconexões

Falhas

=========================================================
WATCHDOG
=========================================================

Criar

ProviderWatchdog

Regras

Sem Tick por 2 segundos

↓

Status Warning

Sem Tick por 5 segundos

↓

Reconectar

Sem Tick por 10 segundos

↓

Reinicializar Provider

=========================================================
SIGNALR
=========================================================

Criar

MarketDataHub

Publicar apenas mudanças.

Nunca enviar snapshot completo.

Implementar

Delta Updates

=========================================================
REDIS
=========================================================

Criar

MarketDataDistributedCache

Objetivo

Sincronizar múltiplas instâncias.

=========================================================
QUESTDB
=========================================================

Criar persistência opcional.

Salvar

Ticks

OHLC

Volume

Trades

=========================================================
REPLAY ENGINE
=========================================================

Criar

ReplayProvider

Permitir

Reproduzir pregões históricos

Backtest visual

Treinamento IA

=========================================================
MONITORAMENTO
=========================================================

Criar página

/connector/health

Exibir

Provider

Status

Heartbeat

Latência

Último Tick

Reconexões

CPU

Memória

Quantidade Ativos

Ticks por segundo

=========================================================
LOGGING
=========================================================

Registrar

Connect

Disconnect

Reconnect

Error

Timeout

Heartbeat

Performance

=========================================================
DIAGNÓSTICO
=========================================================

Criar página

/connector/diagnostics

Exibir

Taxa de ticks

Latência média

Latência máxima

Perda de eventos

Fila Channel

Uso memória

Uso CPU

=========================================================
MULTI PROVIDER
=========================================================

Preparar suporte para

Profit

MT5

Tryd

NinjaTrader

Replay

Mock

Todos devem implementar

IMarketDataProvider

=========================================================
DOCUMENTAÇÃO
=========================================================

Criar

/docs/connector

architecture.md

market-data-gateway.md

profit-dde-provider.md

normalization-engine.md

market-data-bus.md

watchdog.md

health-engine.md

signalr.md

redis.md

questdb.md

diagnostics.md

README.md

=========================================================
OBJETIVO FINAL
=========================================================

Transformar o Connector em um Market Data Gateway profissional.

A aplicação inteira deverá consumir apenas MarketTick.

Nenhum módulo poderá depender diretamente do Profit.

Toda origem de dados deve ser intercambiável.

Trocar Profit por MT5, Tryd ou NinjaTrader não deverá exigir alterações nos módulos:

- Quant
- Macro
- Drivers
- Recommendations
- Performance
- AI

A arquitetura deve ser orientada a eventos, resiliente, escalável e preparada para operar continuamente durante pregões completos sem degradação de performance.