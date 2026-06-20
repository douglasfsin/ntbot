// ProfitChart Types - Integração RTD

export interface RtdStatistics {
  totalDataReceived: number;
  lastDataReceived: string;
  serviceStarted: string;
  totalTopicsConnected: number;
  topicsWithData: number;
  dataRatePerSecond: number;
  isConnected: boolean;
  secondsSinceLastData: number;
}

export interface TickerStatus {
  ticker: string;
  logicalName: string | null;
  isReceivingData: boolean;
  totalTopics: number;
  topicsWithData: number;
  lastUpdate: string | null;
  lastPrice: number | null;
  volume: number | null;
}

export interface RtdTickerConfig {
  tick: string;
  tickers: string[] | null;
  base: number;
  n_CONTRATO: number;
  description: string | null;
  assetType: string | null;
  isActive: boolean;
}

export interface TickerSnapshot {
  [topic: string]: number | string;
}

export interface TickerTopicValue {
  ticker: string;
  topic: string;
  value: number | string;
  timestamp: string;
}

export interface PriceData {
  price?: number;
  timestamp: string;
  error?: string;
}

export interface BookLevel {
  level: number;
  quantity: number;
  price: number;
}

export interface BookData {
  ticker: string;
  compra: BookLevel[];
  venda: BookLevel[];
  timestamp: string;
}

export interface HealthStatus {
  status: 'healthy' | 'unhealthy';
  isConnected: boolean;
  totalDataReceived: number;
  secondsSinceLastData: number;
  serviceStarted: string;
  topicsConnected: number;
  topicsWithData: number;
  dataRate: string;
  timestamp: string;
}

// SignalR Events
export interface TickUpdate {
  ticker: string;
  topic: string;
  value: number | string;
  timestamp: string;
}

export interface ConnectionStatus {
  connected: boolean;
  connectionId: string;
  serverTime: string;
  statistics: RtdStatistics | null;
}

export interface SubscriptionConfirmed {
  ticker: string;
  timestamp: string;
  count?: number;
  tickers?: string[];
}

// UI State
export interface ProfitChartState {
  statistics: RtdStatistics | null;
  tickers: Record<string, TickerStatus>;
  snapshots: Record<string, TickerSnapshot>;
  isConnected: boolean;
  isLoading: boolean;
  error: string | null;
}
