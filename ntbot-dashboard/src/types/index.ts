// API Types matching C# backend models

export interface Tenant {
  id: number;
  name: string;
  apiKey: string;
  subscriptionPlan: SubscriptionPlan;
  isActive: boolean;
  maxConcurrentPositions: number;
  maxDailyTrades: number;
  maxRiskPerTrade: number;
  createdAt: string;
  users?: User[];
  assetConfigurations?: AssetConfiguration[];
}

export const enum SubscriptionPlan {
  FREE = 'FREE',
  PRO = 'PRO',
  ENTERPRISE = 'ENTERPRISE'
}

export interface User {
  id: number;
  tenantId: number;
  username: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export enum UserRole {
  ADMIN = 'ADMIN',
  TRADER = 'TRADER',
  VIEWER = 'VIEWER'
}

export interface AssetConfiguration {
  id: number;
  tenantId: number;
  symbol: string;
  timeframes: string[];
  isActive: boolean;
  minConfidenceScore: number;
  minRiskRewardRatio: number;
  useWyckoffAnalysis: boolean;
  useMacroContext: boolean;
  useNewsAnalysis: boolean;
  useEconomicCalendar: boolean;
  strategyParameters?: Record<string, any>;
}

export interface TradingSignal {
  id: number;
  tenantId: number;
  symbol: string;
  timeframe: string;
  direction: SignalDirection;
  confidence: number;
  wyckoffPhase?: string;
  wyckoffEvent?: string;
  macroBias?: string;
  newsImpact?: number;
  economicEventId?: number;
  entryPrice: number;
  stopLoss: number;
  takeProfit: number;
  riskRewardRatio: number;
  status: SignalStatus;
  generatedAt: string;
  validUntil: string;
  notes?: string;
}

export enum SignalDirection {
  LONG = 'LONG',
  SHORT = 'SHORT'
}

export enum SignalStatus {
  PENDING = 'PENDING',
  ACTIVE = 'ACTIVE',
  EXECUTED = 'EXECUTED',
  CANCELLED = 'CANCELLED',
  EXPIRED = 'EXPIRED'
}

export interface Trade {
  id: number;
  tenantId: number;
  signalId?: number;
  symbol: string;
  direction: TradeDirection;
  entryPrice: number;
  exitPrice?: number;
  stopLoss: number;
  takeProfit: number;
  quantity: number;
  commission: number;
  pnl?: number;
  pnlPercentage?: number;
  mae?: number;
  mfe?: number;
  status: TradeStatus;
  enteredAt: string;
  exitedAt?: string;
  notes?: string;
}

export enum TradeDirection {
  LONG = 'LONG',
  SHORT = 'SHORT'
}

export enum TradeStatus {
  OPEN = 'OPEN',
  CLOSED_PROFIT = 'CLOSED_PROFIT',
  CLOSED_LOSS = 'CLOSED_LOSS',
  CLOSED_BE = 'CLOSED_BE'
}

export interface Candle {
  symbol: string;
  timeframe: string;
  time: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  delta?: number;
  buyVolume?: number;
  sellVolume?: number;
  vwap?: number;
  poc?: number;
  atr?: number;
  rsi?: number;
  ema20?: number;
  ema50?: number;
  ema200?: number;
}

export interface WyckoffAnalysisResult {
  symbol: string;
  timeframe: string;
  phase: WyckoffPhase;
  phaseConfidence: number;
  event?: WyckoffEvent;
  eventConfidence?: number;
  bias: MarketBias;
  isRange: boolean;
  rangeHigh?: number;
  rangeLow?: number;
  rangeCandles?: number;
  structureLevels: StructureLevel[];
  volumeDivergent: boolean;
  recommendations: string[];
  analyzedAt: string;
}

export enum WyckoffPhase {
  ACCUMULATION = 'ACCUMULATION',
  MARKUP = 'MARKUP',
  DISTRIBUTION = 'DISTRIBUTION',
  MARKDOWN = 'MARKDOWN',
  RANGING = 'RANGING'
}

export enum WyckoffEvent {
  SPRING = 'SPRING',
  UPTHRUST = 'UPTHRUST',
  PS = 'PS',
  SC = 'SC',
  AR = 'AR',
  ST = 'ST',
  BC = 'BC',
  UT = 'UT',
  LPSY = 'LPSY',
  NONE = 'NONE'
}

export enum MarketBias {
  BULLISH = 'BULLISH',
  BEARISH = 'BEARISH',
  NEUTRAL = 'NEUTRAL'
}

export interface StructureLevel {
  price: number;
  type: 'SUPPORT' | 'RESISTANCE';
  strength: number;
  touches: number;
}

export interface MacroContextResult {
  symbol: string;
  dailyBias: MarketBias;
  riskMode: RiskMode;
  volatilityRegime: VolatilityRegime;
  correlations: Record<string, number>;
  vixLevel: number;
  recommendations: string[];
  analyzedAt: string;
}

export enum RiskMode {
  BLOCKED = 'BLOCKED',
  REDUCED = 'REDUCED',
  NORMAL = 'NORMAL'
}

export enum VolatilityRegime {
  LOW = 'LOW',
  NORMAL = 'NORMAL',
  HIGH = 'HIGH',
  EXTREME = 'EXTREME'
}

export interface EconomicEvent {
  id: number;
  title: string;
  date: string;
  time?: string;
  impact: EventImpact;
  country: string;
  currency: string;
  previous?: string;
  forecast?: string;
  actual?: string;
  blockTrading: boolean;
  affectedSymbols?: string[];
}

export enum EventImpact {
  LOW = 'LOW',
  MEDIUM = 'MEDIUM',
  HIGH = 'HIGH'
}

export interface NewsAnalysis {
  id: number;
  title: string;
  source: string;
  publishedAt: string;
  summary: string;
  sentiment: number;
  sentimentType: SentimentType;
  impactScore: number;
  relatedSymbols?: string[];
  url?: string;
}

export enum SentimentType {
  VERY_NEGATIVE = 'VERY_NEGATIVE',
  NEGATIVE = 'NEGATIVE',
  NEUTRAL = 'NEUTRAL',
  POSITIVE = 'POSITIVE',
  VERY_POSITIVE = 'VERY_POSITIVE'
}

export interface Position {
  symbol: string;
  action: string;
  quantity: number;
  price?: number;
  stopLoss?: number;
  takeProfit?: number;
  orderNumber?: string;
}

export interface CompleteAnalysisResult {
  wyckoff: WyckoffAnalysisResult;
  macro: MacroContextResult;
  recommendation: string;
  shouldTrade: boolean;
  confidence: number;
}
