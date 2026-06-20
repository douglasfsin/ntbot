import axios from 'axios';
import type { DashboardData, CorrelationData, GammaExposureData, OptionData, QuantSignal } from '../types/quantStrategy';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5053/api';

export const quantStrategyApi = {
  // Obter dashboard completo
  getDashboard: async (symbol: string = 'WINFUT', leaderSymbol: string = 'NQ'): Promise<DashboardData> => {
    const response = await axios.get(`${API_BASE_URL}/quantstrategy/dashboard`, {
      params: { symbol, leaderSymbol },
    });
    return response.data;
  },

  // Analisar e gerar sinal
  analyze: async (symbol: string, leaderSymbol: string = 'NQ'): Promise<QuantSignal | null> => {
    const response = await axios.post(`${API_BASE_URL}/quantstrategy/analyze`, {
      symbol,
      leaderSymbol,
    });
    return response.data;
  },

  // Obter correlação
  getCorrelation: async (
    leaderSymbol: string = 'NQ',
    followerSymbol: string = 'WINFUT',
    lookback: number = 50
  ): Promise<CorrelationData> => {
    const response = await axios.get(`${API_BASE_URL}/quantstrategy/correlation`, {
      params: { leaderSymbol, followerSymbol, lookback },
    });
    return response.data;
  },

  // Obter GEX
  getGEX: async (symbol: string): Promise<GammaExposureData> => {
    const response = await axios.get(`${API_BASE_URL}/quantstrategy/gex`, {
      params: { symbol },
    });
    return response.data;
  },

  // Obter dados de opções
  getOptions: async (symbol: string): Promise<OptionData[]> => {
    const response = await axios.get(`${API_BASE_URL}/quantstrategy/options`, {
      params: { symbol },
    });
    return response.data;
  },

  // Obter histórico de sinais
  getSignalHistory: async (symbol?: string, limit: number = 50): Promise<QuantSignal[]> => {
    const response = await axios.get(`${API_BASE_URL}/quantstrategy/signals/history`, {
      params: { symbol, limit },
    });
    return response.data;
  },
};
