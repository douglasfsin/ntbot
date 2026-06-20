import { create } from 'zustand';
import type { TradingSignal, Trade, Candle } from '../types';

interface TradingState {
  // Market Data
  candles: Record<string, Candle[]>;
  latestCandle: Candle | null;
  
  // Signals
  signals: TradingSignal[];
  activeSignals: TradingSignal[];
  
  // Trades
  trades: Trade[];
  openTrades: Trade[];
  
  // Connection
  isConnected: boolean;
  
  // Actions
  setCandles: (symbol: string, candles: Candle[]) => void;
  addCandle: (candle: Candle) => void;
  setSignals: (signals: TradingSignal[]) => void;
  addSignal: (signal: TradingSignal) => void;
  updateSignal: (id: number, signal: Partial<TradingSignal>) => void;
  setTrades: (trades: Trade[]) => void;
  addTrade: (trade: Trade) => void;
  updateTrade: (id: number, trade: Partial<Trade>) => void;
  setConnected: (connected: boolean) => void;
}

export const useTradingStore = create<TradingState>((set) => ({
  candles: {},
  latestCandle: null,
  signals: [],
  activeSignals: [],
  trades: [],
  openTrades: [],
  isConnected: false,

  setCandles: (symbol, candles) => {
    set((state) => ({
      candles: { ...state.candles, [symbol]: candles },
      latestCandle: candles[candles.length - 1] || null,
    }));
  },

  addCandle: (candle) => {
    set((state) => {
      const key = `${candle.symbol}_${candle.timeframe}`;
      const existing = state.candles[key] || [];
      const updated = [...existing, candle].slice(-500); // Keep last 500 candles

      return {
        candles: { ...state.candles, [key]: updated },
        latestCandle: candle,
      };
    });
  },

  setSignals: (signals) => {
    set({
      signals,
      activeSignals: signals.filter(s => s.status === 'ACTIVE' || s.status === 'PENDING'),
    });
  },

  addSignal: (signal) => {
    set((state) => {
      const signals = [signal, ...state.signals];
      return {
        signals,
        activeSignals: signals.filter(s => s.status === 'ACTIVE' || s.status === 'PENDING'),
      };
    });
  },

  updateSignal: (id, signalUpdate) => {
    set((state) => {
      const signals = state.signals.map(s => 
        s.id === id ? { ...s, ...signalUpdate } : s
      );
      return {
        signals,
        activeSignals: signals.filter(s => s.status === 'ACTIVE' || s.status === 'PENDING'),
      };
    });
  },

  setTrades: (trades) => {
    set({
      trades,
      openTrades: trades.filter(t => t.status === 'OPEN'),
    });
  },

  addTrade: (trade) => {
    set((state) => {
      const trades = [trade, ...state.trades];
      return {
        trades,
        openTrades: trades.filter(t => t.status === 'OPEN'),
      };
    });
  },

  updateTrade: (id, tradeUpdate) => {
    set((state) => {
      const trades = state.trades.map(t => 
        t.id === id ? { ...t, ...tradeUpdate } : t
      );
      return {
        trades,
        openTrades: trades.filter(t => t.status === 'OPEN'),
      };
    });
  },

  setConnected: (connected) => {
    set({ isConnected: connected });
  },
}));
