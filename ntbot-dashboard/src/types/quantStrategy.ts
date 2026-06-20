// Types for Quant Strategy
export interface OptionData {
  id: string;
  symbol: string;
  strike: number;
  type: 'CALL' | 'PUT';
  gamma: number;
  delta: number;
  openInterest: number;
  lastPrice: number;
  impliedVolatility: number;
  expiration: string;
  timestamp: string;
}

export interface GammaWall {
  strike: number;
  gammaConcentration: number;
  type: 'Support' | 'Resistance';
  distance: number;
}

export interface GammaExposureData {
  id: string;
  symbol: string;
  currentPrice: number;
  totalGEX: number;
  netGamma: number;
  gammaFlipLevel: number | null;
  gammaWalls: GammaWall[];
  regime: 'POSITIVE_HIGH' | 'POSITIVE_LOW' | 'NEUTRAL' | 'NEGATIVE_LOW' | 'NEGATIVE_HIGH';
  expansionPotential: number;
  meanReversionPotential: number;
  calculatedAt: string;
}

export interface CorrelationData {
  id: string;
  symbol1: string;
  symbol2: string;
  pearsonCorrelation: number;
  spearmanCorrelation: number;
  lookbackPeriod: number;
  leaderBias: 'BULLISH' | 'BEARISH' | 'NEUTRAL';
  leaderMomentum: number;
  leaderEMA20: number;
  leaderEMA50: number;
  trendStrength: number;
  calculatedAt: string;
}

export interface QuantSignal {
  id: string;
  tenantId: string;
  symbol: string;
  globalBias: 'BULLISH' | 'BEARISH' | 'NEUTRAL';
  gexRegime: 'POSITIVE_HIGH' | 'POSITIVE_LOW' | 'NEUTRAL' | 'NEGATIVE_LOW' | 'NEGATIVE_HIGH';
  wyckoffPhase: string;
  direction: 'LONG' | 'SHORT' | 'FLAT';
  strategyType: 'BREAKOUT' | 'MEAN_REVERSION' | 'HYBRID';
  confidenceScore: number;
  correlationStrength: number;
  gexAlignment: number;
  wyckoffAlignment: number;
  entryPrice: number;
  stopLoss: number;
  takeProfit1: number;
  takeProfit2: number;
  atrValue: number;
  riskRewardRatio: number;
  description: string;
  observations: string[];
  status: 'PENDING' | 'ACTIVE' | 'EXECUTED' | 'CANCELLED' | 'EXPIRED';
  createdAt: string;
  executedAt: string | null;
}

export interface Candle {
  symbol: string;
  timestamp: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface DashboardData {
  symbol: string;
  leaderSymbol: string;
  currentPrice: number;
  timestamp: string;
  correlation: CorrelationData | null;
  gex: GammaExposureData | null;
  signal: QuantSignal | null;
  recentCandles: Candle[];
}
