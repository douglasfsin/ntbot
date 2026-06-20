import { useEffect, useState } from 'react';
import { Activity, AlertCircle } from 'lucide-react';
import { profitChartApi } from '../services/profitchart.api';
import { profitChartSignalR } from '../services/profitchart.signalr';
import { TickerCard, ProfitChartStats, BookOfertas } from '../components/profitchart';
import type { TickerStatus } from '../types/profitchart';
import toast from 'react-hot-toast';

export function ProfitChartPage() {
  const [tickers, setTickers] = useState<Record<string, TickerStatus>>({});
  const [isLoading, setIsLoading] = useState(true);
  const [isConnected, setIsConnected] = useState(false);
  const [selectedTicker, setSelectedTicker] = useState<string | null>(null);

  useEffect(() => {
    const initializeProfitChart = async () => {
      try {
        // Conectar SignalR
        await profitChartSignalR.connect();
        setIsConnected(true);

        // Carregar tickers disponíveis
        const tickersData = await profitChartApi.getAllTickers();
        setTickers(tickersData);

        // Selecionar primeiro ticker por padrão
        const firstTicker = Object.keys(tickersData)[0];
        if (firstTicker) {
          setSelectedTicker(firstTicker);
        }

        // Inscrever em todos os tickers
        await profitChartSignalR.subscribeAll();

        setIsLoading(false);
      } catch (error) {
        console.error('Failed to initialize ProfitChart:', error);
        toast.error('Falha ao conectar com ProfitChart');
        setIsLoading(false);
      }
    };

    initializeProfitChart();

    return () => {
      profitChartSignalR.disconnect();
    };
  }, []);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <div className="text-center space-y-4">
          <Activity className="w-12 h-12 text-blue-500 animate-spin mx-auto" />
          <p className="text-slate-400">Conectando ao ProfitChart...</p>
        </div>
      </div>
    );
  }

  if (!isConnected) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <div className="text-center space-y-4 max-w-md">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto" />
          <h2 className="text-xl font-semibold text-white">
            Não foi possível conectar ao ProfitChart
          </h2>
          <p className="text-slate-400">
            Verifique se o serviço RTD está rodando e se o ProfitChart está aberto.
          </p>
          <button
            onClick={() => window.location.reload()}
            className="px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            Tentar Novamente
          </button>
        </div>
      </div>
    );
  }

  const tickersList = Object.entries(tickers);

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-white flex items-center gap-3">
          <Activity className="w-8 h-8 text-blue-500" />
          ProfitChart RTD Integration
        </h1>
        <div className="flex items-center gap-2 px-4 py-2 bg-green-500/20 text-green-500 rounded-lg">
          <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse" />
          <span className="font-medium">Tempo Real</span>
        </div>
      </div>

      {/* Estatísticas Gerais */}
      <ProfitChartStats refreshInterval={5000} />

      {/* Grid de Tickers */}
      <div>
        <h2 className="text-xl font-semibold text-white mb-4">Tickers Ativos</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {tickersList.map(([ticker, status]) => (
            <div
              key={ticker}
              onClick={() => setSelectedTicker(ticker)}
              className={`cursor-pointer transition-all ${
                selectedTicker === ticker ? 'ring-2 ring-blue-500 rounded-lg' : ''
              }`}
            >
              <TickerCard
                ticker={ticker}
                logicalName={status.logicalName || undefined}
                autoSubscribe={true}
              />
            </div>
          ))}
        </div>

        {tickersList.length === 0 && (
          <div className="text-center py-12 bg-slate-800 rounded-lg border border-slate-700">
            <Activity className="w-12 h-12 text-slate-500 mx-auto mb-4" />
            <p className="text-slate-400">Nenhum ticker configurado</p>
            <p className="text-sm text-slate-500 mt-2">
              Configure tickers no arquivo rtd_config.json
            </p>
          </div>
        )}
      </div>

      {/* Book de Ofertas do Ticker Selecionado */}
      {selectedTicker && (
        <div>
          <h2 className="text-xl font-semibold text-white mb-4">
            Book de Ofertas
          </h2>
          <div className="max-w-2xl">
            <BookOfertas ticker={selectedTicker} levels={10} autoRefresh={true} />
          </div>
        </div>
      )}
    </div>
  );
}
