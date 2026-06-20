import { useEffect, useState } from 'react';
import { Activity, TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { profitChartSignalR } from '../../services/profitchart.signalr';
import type { TickUpdate } from '../../types/profitchart';

interface TickerCardProps {
  ticker: string;
  logicalName?: string;
  autoSubscribe?: boolean;
}

export function TickerCard({ ticker, logicalName, autoSubscribe = true }: TickerCardProps) {
  const [price, setPrice] = useState<number | null>(null);
  const [volume, setVolume] = useState<number | null>(null);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [priceChange, setPriceChange] = useState<number>(0);
  const [previousPrice, setPreviousPrice] = useState<number | null>(null);

  useEffect(() => {
    if (!autoSubscribe) return;

    // Conectar e inscrever no ticker
    const subscribe = async () => {
      try {
        if (!profitChartSignalR.isConnected()) {
          await profitChartSignalR.connect();
        }
        await profitChartSignalR.subscribeTicker(ticker);
      } catch (error) {
        console.error('Failed to subscribe to ticker:', error);
      }
    };

    // Callback para atualizações
    const unsubscribeCallback = profitChartSignalR.onTickUpdate((data: TickUpdate) => {
      if (data.ticker !== ticker) return;

      if (data.topic === 'ULT' && typeof data.value === 'number') {
        const newPrice = data.value;
        
        if (previousPrice !== null) {
          const change = ((newPrice - previousPrice) / previousPrice) * 100;
          setPriceChange(change);
        }
        
        setPreviousPrice(price);
        setPrice(newPrice);
        setLastUpdate(new Date(data.timestamp));
      } else if (data.topic === 'VOL' && typeof data.value === 'number') {
        setVolume(data.value);
      }
    });

    subscribe();

    return () => {
      unsubscribeCallback();
      profitChartSignalR.unsubscribeTicker(ticker).catch(console.error);
    };
  }, [ticker, autoSubscribe, price, previousPrice]);

  const formatPrice = (value: number | null): string => {
    if (value === null) return '--';
    return value.toLocaleString('pt-BR', { 
      minimumFractionDigits: 2,
      maximumFractionDigits: 2 
    });
  };

  const formatVolume = (value: number | null): string => {
    if (value === null) return '--';
    if (value >= 1000000) {
      return `${(value / 1000000).toFixed(1)}M`;
    } else if (value >= 1000) {
      return `${(value / 1000).toFixed(1)}K`;
    }
    return value.toLocaleString('pt-BR');
  };

  const getTrendIcon = () => {
    if (priceChange > 0.1) return <TrendingUp className="w-5 h-5 text-green-500" />;
    if (priceChange < -0.1) return <TrendingDown className="w-5 h-5 text-red-500" />;
    return <Minus className="w-5 h-5 text-gray-500" />;
  };

  const getPriceColor = () => {
    if (priceChange > 0.1) return 'text-green-500';
    if (priceChange < -0.1) return 'text-red-500';
    return 'text-gray-300';
  };

  return (
    <div className="bg-slate-800 rounded-lg border border-slate-700 p-4 hover:border-slate-600 transition-colors">
      <div className="flex items-start justify-between mb-3">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-lg font-semibold text-white">{ticker}</h3>
            {getTrendIcon()}
          </div>
          {logicalName && (
            <p className="text-sm text-slate-400 mt-1">{logicalName}</p>
          )}
        </div>
        <div className={`flex items-center gap-1 ${price !== null ? 'opacity-100' : 'opacity-0'}`}>
          <Activity className="w-4 h-4 text-blue-500 animate-pulse" />
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex items-baseline justify-between">
          <span className="text-sm text-slate-400">Preço</span>
          <div className="text-right">
            <span className={`text-2xl font-bold ${getPriceColor()}`}>
              {formatPrice(price)}
            </span>
            {priceChange !== 0 && (
              <span className={`ml-2 text-sm ${getPriceColor()}`}>
                {priceChange > 0 ? '+' : ''}{priceChange.toFixed(2)}%
              </span>
            )}
          </div>
        </div>

        <div className="flex items-baseline justify-between">
          <span className="text-sm text-slate-400">Volume</span>
          <span className="text-lg font-semibold text-white">
            {formatVolume(volume)}
          </span>
        </div>

        {lastUpdate && (
          <div className="flex items-baseline justify-between text-xs text-slate-500 mt-3 pt-3 border-t border-slate-700">
            <span>Última atualização</span>
            <span>{lastUpdate.toLocaleTimeString('pt-BR')}</span>
          </div>
        )}
      </div>
    </div>
  );
}
