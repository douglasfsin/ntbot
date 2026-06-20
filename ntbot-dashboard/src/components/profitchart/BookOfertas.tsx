import { useEffect, useState } from 'react';
import { profitChartApi } from '../../services/profitchart.api';
import { profitChartSignalR } from '../../services/profitchart.signalr';
import { RefreshCw } from 'lucide-react';
import type { BookData, TickUpdate } from '../../types/profitchart';

interface BookOfertasProps {
  ticker: string;
  levels?: number;
  autoRefresh?: boolean;
}

export function BookOfertas({ ticker, levels = 5, autoRefresh = true }: BookOfertasProps) {
  const [book, setBook] = useState<BookData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchBook = async () => {
      try {
        const data = await profitChartApi.getBook(ticker, levels);
        setBook(data);
        setError(null);
      } catch (err) {
        setError('Falha ao carregar book');
        console.error('Failed to fetch book:', err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchBook();

    if (autoRefresh) {
      // Atualizar book via SignalR quando houver mudanças
      const unsubscribe = profitChartSignalR.onTickUpdate((data: TickUpdate) => {
        if (data.ticker === ticker && (data.topic.startsWith('QC') || data.topic.startsWith('QV'))) {
          fetchBook(); // Refetch book quando houver mudança nas quantidades
        }
      });

      // Refresh periódico como fallback
      const interval = setInterval(fetchBook, 2000);

      return () => {
        unsubscribe();
        clearInterval(interval);
      };
    }
  }, [ticker, levels, autoRefresh]);

  if (isLoading) {
    return (
      <div className="bg-slate-800 rounded-lg border border-slate-700 p-6">
        <div className="animate-pulse space-y-2">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-8 bg-slate-700 rounded"></div>
          ))}
        </div>
      </div>
    );
  }

  if (error || !book) {
    return (
      <div className="bg-slate-800 rounded-lg border border-red-900/50 p-6">
        <p className="text-red-500 text-center">{error || 'Book não disponível'}</p>
        <button
          onClick={() => window.location.reload()}
          className="mt-4 w-full px-4 py-2 bg-slate-700 hover:bg-slate-600 text-white rounded-lg transition-colors flex items-center justify-center gap-2"
        >
          <RefreshCw className="w-4 h-4" />
          Tentar novamente
        </button>
      </div>
    );
  }

  const maxCompra = Math.max(...book.compra.map(b => b.quantity), 1);
  const maxVenda = Math.max(...book.venda.map(b => b.quantity), 1);

  return (
    <div className="bg-slate-800 rounded-lg border border-slate-700 p-6">
      <h3 className="text-lg font-semibold text-white mb-4">
        Book de Ofertas - {ticker}
      </h3>

      <div className="grid grid-cols-2 gap-4">
        {/* Compra */}
        <div className="space-y-1">
          <div className="text-xs font-semibold text-green-500 mb-2 flex items-center justify-between">
            <span>COMPRA</span>
            <span>QTD</span>
          </div>
          {book.compra.map((level, idx) => (
            <div
              key={`compra-${idx}`}
              className="relative overflow-hidden rounded px-2 py-1"
            >
              <div
                className="absolute inset-0 bg-green-500/10"
                style={{ width: `${(level.quantity / maxCompra) * 100}%` }}
              />
              <div className="relative flex items-center justify-between text-sm">
                <span className="font-mono text-green-400">
                  {level.price.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </span>
                <span className="font-mono text-slate-300">
                  {level.quantity.toLocaleString('pt-BR')}
                </span>
              </div>
            </div>
          ))}
        </div>

        {/* Venda */}
        <div className="space-y-1">
          <div className="text-xs font-semibold text-red-500 mb-2 flex items-center justify-between">
            <span>VENDA</span>
            <span>QTD</span>
          </div>
          {book.venda.map((level, idx) => (
            <div
              key={`venda-${idx}`}
              className="relative overflow-hidden rounded px-2 py-1"
            >
              <div
                className="absolute inset-0 bg-red-500/10"
                style={{ width: `${(level.quantity / maxVenda) * 100}%` }}
              />
              <div className="relative flex items-center justify-between text-sm">
                <span className="font-mono text-red-400">
                  {level.price.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </span>
                <span className="font-mono text-slate-300">
                  {level.quantity.toLocaleString('pt-BR')}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="mt-4 pt-4 border-t border-slate-700 text-xs text-slate-500 text-center">
        Atualização em tempo real via SignalR
      </div>
    </div>
  );
}
