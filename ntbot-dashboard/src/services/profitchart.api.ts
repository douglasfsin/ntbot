import axios from 'axios';
import type {
  RtdStatistics,
  TickerStatus,
  RtdTickerConfig,
  TickerSnapshot,
  TickerTopicValue,
  PriceData,
  BookData,
  HealthStatus,
} from '../types/profitchart';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5053';

class ProfitChartApiService {
  private client = axios.create({
    baseURL: `${API_BASE_URL}/api/profitchart`,
    timeout: 10000,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  /**
   * Obtém estatísticas do serviço RTD
   */
  async getStatistics(): Promise<RtdStatistics> {
    const response = await this.client.get<RtdStatistics>('/statistics');
    return response.data;
  }

  /**
   * Obtém status de todos os tickers configurados
   */
  async getAllTickers(): Promise<Record<string, TickerStatus>> {
    const response = await this.client.get<Record<string, TickerStatus>>('/tickers');
    return response.data;
  }

  /**
   * Obtém snapshot completo de um ticker
   */
  async getTickerSnapshot(ticker: string): Promise<TickerSnapshot> {
    const response = await this.client.get<TickerSnapshot>(`/tickers/${ticker}`);
    return response.data;
  }

  /**
   * Obtém valor específico de um tópico
   */
  async getTickerTopic(ticker: string, topic: string): Promise<TickerTopicValue> {
    const response = await this.client.get<TickerTopicValue>(`/tickers/${ticker}/${topic}`);
    return response.data;
  }

  /**
   * Obtém configuração de um ticker
   */
  async getConfig(logical: string): Promise<RtdTickerConfig> {
    const response = await this.client.get<RtdTickerConfig>(`/config/${logical}`);
    return response.data;
  }

  /**
   * Health check do serviço RTD
   */
  async getHealth(): Promise<HealthStatus> {
    const response = await this.client.get<HealthStatus>('/health');
    return response.data;
  }

  /**
   * Obtém preços de múltiplos tickers
   */
  async getPrices(tickers: string[]): Promise<Record<string, PriceData>> {
    const tickersParam = tickers.join(',');
    const response = await this.client.get<Record<string, PriceData>>(
      `/prices?tickers=${encodeURIComponent(tickersParam)}`
    );
    return response.data;
  }

  /**
   * Obtém book de ofertas (DOM)
   */
  async getBook(ticker: string, levels: number = 5): Promise<BookData> {
    const response = await this.client.get<BookData>(`/book/${ticker}?levels=${levels}`);
    return response.data;
  }

  /**
   * Obtém último preço de um ticker
   */
  async getLastPrice(ticker: string): Promise<number | null> {
    try {
      const data = await this.getTickerTopic(ticker, 'ULT');
      return typeof data.value === 'number' ? data.value : null;
    } catch {
      return null;
    }
  }
}

export const profitChartApi = new ProfitChartApiService();
