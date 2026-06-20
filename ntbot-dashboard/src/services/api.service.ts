import axios, { type AxiosError } from 'axios';
import toast from 'react-hot-toast';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5053';

class ApiService {
  private client: ReturnType<typeof axios.create>;
  private token: string | null = null;

  constructor() {
    this.client = axios.create({
      baseURL: `${API_BASE_URL}/api`,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
    });

    // Request interceptor
    this.client.interceptors.request.use(
      (config) => {
        if (this.token) {
          config.headers.Authorization = `Bearer ${this.token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError) => {
        this.handleError(error);
        return Promise.reject(error);
      }
    );
  }

  private handleError(error: AxiosError) {
    if (error.response) {
      const status = error.response.status;
      const message = (error.response.data as any)?.message || error.message;

      switch (status) {
        case 401:
          toast.error('Sessão expirada. Faça login novamente.');
          this.clearToken();
          break;
        case 403:
          toast.error('Acesso negado.');
          break;
        case 404:
          toast.error('Recurso não encontrado.');
          break;
        case 500:
          toast.error('Erro interno do servidor.');
          break;
        default:
          toast.error(message || 'Erro desconhecido.');
      }
    } else if (error.request) {
      toast.error('Servidor não está respondendo. Verifique sua conexão.');
    } else {
      toast.error('Erro ao fazer requisição.');
    }
  }

  setToken(token: string) {
    this.token = token;
    localStorage.setItem('ntbot_token', token);
  }

  getToken(): string | null {
    if (!this.token) {
      this.token = localStorage.getItem('ntbot_token');
    }
    return this.token;
  }

  clearToken() {
    this.token = null;
    localStorage.removeItem('ntbot_token');
  }

  // Health Check
  async healthCheck() {
    const response = await this.client.get('/health');
    return response.data;
  }

  // Tenants
  async getTenants() {
    const response = await this.client.get('/tenants');
    return response.data;
  }

  async getTenant(id: number) {
    const response = await this.client.get(`/tenants/${id}`);
    return response.data;
  }

  async createTenant(data: any) {
    const response = await this.client.post('/tenants', data);
    return response.data;
  }

  async updateTenant(id: number, data: any) {
    const response = await this.client.put(`/tenants/${id}`, data);
    return response.data;
  }

  async deleteTenant(id: number) {
    await this.client.delete(`/tenants/${id}`);
  }

  // Analysis
  async getWyckoffAnalysis(symbol: string, timeframe: string = '5m') {
    const response = await this.client.get(`/analysis/wyckoff/${symbol}`, {
      params: { timeframe }
    });
    return response.data;
  }

  async getMacroAnalysis(symbol: string) {
    const response = await this.client.get(`/analysis/macro/${symbol}`);
    return response.data;
  }

  async getCompleteAnalysis(symbol: string, timeframe: string = '5m') {
    const response = await this.client.get(`/analysis/complete/${symbol}`, {
      params: { timeframe }
    });
    return response.data;
  }

  // Orders (Legacy)
  async getNextOrder(symbol: string, bid: number, ask: number, time: string) {
    const response = await this.client.get('/orders/next', {
      params: { symbol, bid, ask, time }
    });
    return response.data;
  }

  // Market Data (will be implemented via SignalR)
  async getCandles(symbol: string, timeframe: string, count: number = 100) {
    // This will be replaced with SignalR streaming
    const response = await this.client.get(`/market/candles/${symbol}`, {
      params: { timeframe, count }
    });
    return response.data;
  }

  // Trading Signals
  async getSignals(tenantId?: number, status?: string) {
    const response = await this.client.get('/signals', {
      params: { tenantId, status }
    });
    return response.data;
  }

  async getSignal(id: number) {
    const response = await this.client.get(`/signals/${id}`);
    return response.data;
  }

  async createSignal(data: any) {
    const response = await this.client.post('/signals', data);
    return response.data;
  }

  async updateSignalStatus(id: number, status: string) {
    const response = await this.client.patch(`/signals/${id}/status`, { status });
    return response.data;
  }

  // Trades
  async getTrades(tenantId?: number, status?: string) {
    const response = await this.client.get('/trades', {
      params: { tenantId, status }
    });
    return response.data;
  }

  async getTrade(id: number) {
    const response = await this.client.get(`/trades/${id}`);
    return response.data;
  }

  async getTradeStatistics(tenantId?: number, startDate?: string, endDate?: string) {
    const response = await this.client.get('/trades/statistics', {
      params: { tenantId, startDate, endDate }
    });
    return response.data;
  }

  // Economic Calendar
  async getEconomicEvents(startDate?: string, endDate?: string, impact?: string) {
    const response = await this.client.get('/economic-events', {
      params: { startDate, endDate, impact }
    });
    return response.data;
  }

  // News Analysis
  async getNewsAnalyses(symbol?: string, limit: number = 20) {
    const response = await this.client.get('/news', {
      params: { symbol, limit }
    });
    return response.data;
  }
}

export const apiService = new ApiService();
