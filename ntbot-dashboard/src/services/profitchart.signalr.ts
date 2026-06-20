import * as signalR from '@microsoft/signalr';
import toast from 'react-hot-toast';
import type {
  TickUpdate,
  ConnectionStatus,
  SubscriptionConfirmed,
  RtdStatistics,
  TickerStatus,
  TickerSnapshot,
} from '../types/profitchart';

const SIGNALR_URL = import.meta.env.VITE_API_URL || 'http://localhost:5053';

type TickUpdateCallback = (data: TickUpdate) => void;
type ConnectionStatusCallback = (data: ConnectionStatus) => void;
type TickerSnapshotCallback = (data: { ticker: string; data: TickerSnapshot; timestamp: string }) => void;
type StatisticsCallback = (data: RtdStatistics) => void;
type AllTickersStatusCallback = (data: Record<string, TickerStatus>) => void;

class ProfitChartSignalRService {
  private connection: signalR.HubConnection | null = null;
  private tickUpdateCallbacks: Set<TickUpdateCallback> = new Set();
  private connectionStatusCallbacks: Set<ConnectionStatusCallback> = new Set();
  private tickerSnapshotCallbacks: Set<TickerSnapshotCallback> = new Set();
  private statisticsCallbacks: Set<StatisticsCallback> = new Set();
  private allTickersStatusCallbacks: Set<AllTickersStatusCallback> = new Set();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private subscribedTickers: Set<string> = new Set();

  /**
   * Conecta ao hub SignalR do ProfitChart
   */
  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      console.log('✅ ProfitChart SignalR already connected');
      return;
    }

    try {
      const builder = new signalR.HubConnectionBuilder()
        .withUrl(`${SIGNALR_URL}/hubs/profitchart`, {
          skipNegotiation: false,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
              toast.error('Falha ao reconectar ao ProfitChart');
              return null;
            }
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          },
        })
        .configureLogging(signalR.LogLevel.Information);

      this.connection = builder.build();

      this.setupEventHandlers();

      await this.connection.start();
      console.log('✅ ProfitChart SignalR Connected');
      toast.success('Conectado ao ProfitChart RTD');

      // Reinscrever em tickers após reconexão
      if (this.subscribedTickers.size > 0) {
        for (const ticker of this.subscribedTickers) {
          await this.subscribeTicker(ticker);
        }
      }

      this.reconnectAttempts = 0;
    } catch (error) {
      console.error('❌ Failed to connect to ProfitChart SignalR:', error);
      this.reconnectAttempts++;
      
      if (this.reconnectAttempts >= this.maxReconnectAttempts) {
        toast.error('Não foi possível conectar ao ProfitChart');
      }
      
      throw error;
    }
  }

  /**
   * Configura os event handlers do SignalR
   */
  private setupEventHandlers(): void {
    if (!this.connection) return;

    // ConnectionStatus
    this.connection.on('ConnectionStatus', (data: ConnectionStatus) => {
      console.log('📡 ProfitChart Connection Status:', data);
      this.connectionStatusCallbacks.forEach(cb => cb(data));
    });

    // TickUpdate - Atualização de tick em tempo real
    this.connection.on('TickUpdate', (data: TickUpdate) => {
      this.tickUpdateCallbacks.forEach(cb => cb(data));
    });

    // TickerSnapshot - Snapshot completo de ticker
    this.connection.on('TickerSnapshot', (data: any) => {
      this.tickerSnapshotCallbacks.forEach(cb => cb(data));
    });

    // Statistics - Estatísticas do servidor
    this.connection.on('Statistics', (data: RtdStatistics) => {
      this.statisticsCallbacks.forEach(cb => cb(data));
    });

    // AllTickersStatus - Status de todos tickers
    this.connection.on('AllTickersStatus', (data: Record<string, TickerStatus>) => {
      this.allTickersStatusCallbacks.forEach(cb => cb(data));
    });

    // SubscriptionConfirmed
    this.connection.on('SubscriptionConfirmed', (data: SubscriptionConfirmed) => {
      console.log('✅ Subscription confirmed:', data);
      if (data.ticker !== 'ALL') {
        this.subscribedTickers.add(data.ticker);
      }
    });

    // UnsubscriptionConfirmed
    this.connection.on('UnsubscriptionConfirmed', (data: any) => {
      console.log('✅ Unsubscription confirmed:', data);
      this.subscribedTickers.delete(data.ticker);
    });

    // Error handler
    this.connection.on('Error', (error: string) => {
      console.error('❌ ProfitChart SignalR Error:', error);
      toast.error(`Erro: ${error}`);
    });

    // Reconnecting
    this.connection.onreconnecting((error) => {
      console.warn('⚠️ ProfitChart SignalR reconnecting...', error);
      toast('Reconectando ao ProfitChart...', { icon: '🔄' });
    });

    // Reconnected
    this.connection.onreconnected((connectionId) => {
      console.log('✅ ProfitChart SignalR reconnected:', connectionId);
      toast.success('Reconectado ao ProfitChart');
    });

    // Closed
    this.connection.onclose((error) => {
      console.error('❌ ProfitChart SignalR connection closed:', error);
      if (error) {
        toast.error('Conexão com ProfitChart perdida');
      }
    });
  }

  /**
   * Inscreve em um ticker específico
   */
  async subscribeTicker(ticker: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Not connected to ProfitChart hub');
    }

    await this.connection.invoke('SubscribeTicker', ticker);
    console.log(`📊 Subscribed to ticker: ${ticker}`);
  }

  /**
   * Cancela inscrição de um ticker
   */
  async unsubscribeTicker(ticker: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('UnsubscribeTicker', ticker);
    this.subscribedTickers.delete(ticker);
    console.log(`📊 Unsubscribed from ticker: ${ticker}`);
  }

  /**
   * Inscreve em todos os tickers
   */
  async subscribeAll(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Not connected to ProfitChart hub');
    }

    await this.connection.invoke('SubscribeAll');
    console.log('📊 Subscribed to ALL tickers');
  }

  /**
   * Obtém estatísticas do servidor via SignalR
   */
  async getStatistics(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('GetStatistics');
  }

  /**
   * Obtém status de todos tickers via SignalR
   */
  async getAllTickersStatus(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('GetAllTickersStatus');
  }

  /**
   * Obtém snapshot de um ticker via SignalR
   */
  async getTickerSnapshot(ticker: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('GetTickerSnapshot', ticker);
  }

  /**
   * Desconecta do hub
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.subscribedTickers.clear();
      console.log('✅ Disconnected from ProfitChart SignalR');
    }
  }

  /**
   * Registra callback para atualizações de tick
   */
  onTickUpdate(callback: TickUpdateCallback): () => void {
    this.tickUpdateCallbacks.add(callback);
    return () => this.tickUpdateCallbacks.delete(callback);
  }

  /**
   * Registra callback para status de conexão
   */
  onConnectionStatus(callback: ConnectionStatusCallback): () => void {
    this.connectionStatusCallbacks.add(callback);
    return () => this.connectionStatusCallbacks.delete(callback);
  }

  /**
   * Registra callback para snapshots de ticker
   */
  onTickerSnapshot(callback: TickerSnapshotCallback): () => void {
    this.tickerSnapshotCallbacks.add(callback);
    return () => this.tickerSnapshotCallbacks.delete(callback);
  }

  /**
   * Registra callback para estatísticas
   */
  onStatistics(callback: StatisticsCallback): () => void {
    this.statisticsCallbacks.add(callback);
    return () => this.statisticsCallbacks.delete(callback);
  }

  /**
   * Registra callback para status de todos tickers
   */
  onAllTickersStatus(callback: AllTickersStatusCallback): () => void {
    this.allTickersStatusCallbacks.add(callback);
    return () => this.allTickersStatusCallbacks.delete(callback);
  }

  /**
   * Verifica se está conectado
   */
  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const profitChartSignalR = new ProfitChartSignalRService();
