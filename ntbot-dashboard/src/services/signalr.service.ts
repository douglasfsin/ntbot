import * as signalR from '@microsoft/signalr';
import toast from 'react-hot-toast';
import type { Candle, TradingSignal, Trade } from '../types';

const SIGNALR_URL = import.meta.env.VITE_API_URL || 'http://localhost:5053';

type CandleCallback = (candle: Candle) => void;
type SignalCallback = (signal: TradingSignal) => void;
type TradeCallback = (trade: Trade) => void;
type ConnectionCallback = (connected: boolean) => void;

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private candleCallbacks: Set<CandleCallback> = new Set();
  private signalCallbacks: Set<SignalCallback> = new Set();
  private tradeCallbacks: Set<TradeCallback> = new Set();
  private connectionCallbacks: Set<ConnectionCallback> = new Set();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 3;

  async connect(token?: string) {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      console.log('SignalR already connected');
      return;
    }

    try {
      const builder = new signalR.HubConnectionBuilder()
        .withUrl(`${SIGNALR_URL}/hubs/trading`, {
          accessTokenFactory: () => token || '',
          skipNegotiation: false,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
              return null; // Stop reconnecting
            }
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          },
        })
        .configureLogging(signalR.LogLevel.Warning); // Reduce logs

      this.connection = builder.build();

      // Setup event handlers
      this.setupEventHandlers();

      await this.connection.start();
      console.log('✅ SignalR Connected');
      toast.success('Conexão em tempo real estabelecida');
      this.notifyConnection(true);
      this.reconnectAttempts = 0;
    } catch (error) {
      // Only log on first attempt
      if (this.reconnectAttempts === 0) {
        console.warn('⚠️ SignalR não disponível (tempo real desabilitado)');
      }
      this.notifyConnection(false);
      
      // Try to reconnect (limited attempts)
      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        this.reconnectAttempts++;
        setTimeout(() => this.connect(token), 10000); // Wait 10s between retries
      }
    }
  }

  private setupEventHandlers() {
    if (!this.connection) return;

    // Candle updates
    this.connection.on('ReceiveCandle', (candle: Candle) => {
      this.candleCallbacks.forEach(callback => callback(candle));
    });

    // New signals
    this.connection.on('ReceiveSignal', (signal: TradingSignal) => {
      this.signalCallbacks.forEach(callback => callback(signal));
      toast.success(`Novo sinal: ${signal.direction} ${signal.symbol} @ ${signal.confidence}%`);
    });

    // Trade updates
    this.connection.on('ReceiveTrade', (trade: Trade) => {
      this.tradeCallbacks.forEach(callback => callback(trade));
      
      if (trade.status === 'CLOSED_PROFIT') {
        toast.success(`✅ Trade lucrativo: ${trade.symbol} +${trade.pnlPercentage?.toFixed(2)}%`);
      } else if (trade.status === 'CLOSED_LOSS') {
        toast.error(`❌ Trade com loss: ${trade.symbol} ${trade.pnlPercentage?.toFixed(2)}%`);
      }
    });

    // Reconnection events
    this.connection.onreconnecting(() => {
      console.log('🔄 SignalR Reconnecting...');
      toast.loading('Reconectando...', { id: 'signalr-reconnect' });
      this.notifyConnection(false);
    });

    this.connection.onreconnected(() => {
      console.log('✅ SignalR Reconnected');
      toast.success('Reconectado!', { id: 'signalr-reconnect' });
      this.notifyConnection(true);
    });

    this.connection.onclose(() => {
      console.log('⚠️ SignalR Disconnected');
      toast.error('Conexão perdida');
      this.notifyConnection(false);
    });
  }

  async disconnect() {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.notifyConnection(false);
    }
  }

  // Subscribe to market data
  async subscribeToSymbol(symbol: string, timeframe: string = '5m') {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR not connected');
    }

    try {
      await this.connection.invoke('SubscribeToSymbol', symbol, timeframe);
      console.log(`📊 Subscribed to ${symbol} ${timeframe}`);
    } catch (error) {
      console.error('Subscribe error:', error);
      toast.error(`Erro ao se inscrever em ${symbol}`);
    }
  }

  async unsubscribeFromSymbol(symbol: string, timeframe: string = '5m') {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke('UnsubscribeFromSymbol', symbol, timeframe);
      console.log(`📊 Unsubscribed from ${symbol} ${timeframe}`);
    } catch (error) {
      console.error('Unsubscribe error:', error);
    }
  }

  // Callback management
  onCandle(callback: CandleCallback) {
    this.candleCallbacks.add(callback);
    return () => this.candleCallbacks.delete(callback);
  }

  onSignal(callback: SignalCallback) {
    this.signalCallbacks.add(callback);
    return () => this.signalCallbacks.delete(callback);
  }

  onTrade(callback: TradeCallback) {
    this.tradeCallbacks.add(callback);
    return () => this.tradeCallbacks.delete(callback);
  }

  onConnectionChange(callback: ConnectionCallback) {
    this.connectionCallbacks.add(callback);
    return () => this.connectionCallbacks.delete(callback);
  }

  private notifyConnection(connected: boolean) {
    this.connectionCallbacks.forEach(callback => callback(connected));
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const signalRService = new SignalRService();
