import { useEffect, useState } from 'react';
import { profitChartApi } from '../../services/profitchart.api';
import { Activity, Database, Zap, Clock } from 'lucide-react';
import type { RtdStatistics } from '../../types/profitchart';

interface ProfitChartStatsProps {
  refreshInterval?: number; // milliseconds
}

export function ProfitChartStats({ refreshInterval = 5000 }: ProfitChartStatsProps) {
  const [stats, setStats] = useState<RtdStatistics | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await profitChartApi.getStatistics();
        setStats(data);
        setError(null);
      } catch (err) {
        setError('Falha ao carregar estatísticas');
        console.error('Failed to fetch ProfitChart stats:', err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchStats();
    const interval = setInterval(fetchStats, refreshInterval);

    return () => clearInterval(interval);
  }, [refreshInterval]);

  if (isLoading) {
    return (
      <div className="bg-slate-800 rounded-lg border border-slate-700 p-6">
        <div className="animate-pulse space-y-4">
          <div className="h-4 bg-slate-700 rounded w-1/3"></div>
          <div className="h-8 bg-slate-700 rounded"></div>
        </div>
      </div>
    );
  }

  if (error || !stats) {
    return (
      <div className="bg-slate-800 rounded-lg border border-red-900/50 p-6">
        <p className="text-red-500">{error || 'Sem dados disponíveis'}</p>
      </div>
    );
  }

  const formatDataRate = (rate: number): string => {
    return `${rate.toFixed(2)} data/s`;
  };

  const formatUptime = (startTime: string): string => {
    const start = new Date(startTime);
    const now = new Date();
    const diffMs = now.getTime() - start.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    
    if (diffMins < 60) {
      return `${diffMins}min`;
    }
    
    const hours = Math.floor(diffMins / 60);
    const mins = diffMins % 60;
    return `${hours}h ${mins}min`;
  };

  return (
    <div className="bg-slate-800 rounded-lg border border-slate-700 p-6">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-white flex items-center gap-2">
          <Activity className="w-5 h-5 text-blue-500" />
          ProfitChart RTD Stats
        </h3>
        <div className={`flex items-center gap-2 px-3 py-1 rounded-full ${
          stats.isConnected 
            ? 'bg-green-500/20 text-green-500' 
            : 'bg-red-500/20 text-red-500'
        }`}>
          <div className={`w-2 h-2 rounded-full ${
            stats.isConnected ? 'bg-green-500' : 'bg-red-500'
          } ${stats.isConnected ? 'animate-pulse' : ''}`} />
          <span className="text-sm font-medium">
            {stats.isConnected ? 'Conectado' : 'Desconectado'}
          </span>
        </div>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <Database className="w-4 h-4" />
            <span>Dados Recebidos</span>
          </div>
          <p className="text-2xl font-bold text-white">
            {stats.totalDataReceived.toLocaleString('pt-BR')}
          </p>
        </div>

        <div className="space-y-1">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <Zap className="w-4 h-4" />
            <span>Taxa de Dados</span>
          </div>
          <p className="text-2xl font-bold text-white">
            {formatDataRate(stats.dataRatePerSecond)}
          </p>
        </div>

        <div className="space-y-1">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <Activity className="w-4 h-4" />
            <span>Tópicos Ativos</span>
          </div>
          <p className="text-2xl font-bold text-white">
            {stats.topicsWithData} / {stats.totalTopicsConnected}
          </p>
        </div>

        <div className="space-y-1">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <Clock className="w-4 h-4" />
            <span>Uptime</span>
          </div>
          <p className="text-2xl font-bold text-white">
            {formatUptime(stats.serviceStarted)}
          </p>
        </div>
      </div>

      <div className="mt-4 pt-4 border-t border-slate-700">
        <div className="flex items-center justify-between text-sm">
          <span className="text-slate-400">Última atualização</span>
          <span className="text-slate-300">
            {stats.secondsSinceLastData < 60 
              ? `Há ${Math.floor(stats.secondsSinceLastData)}s` 
              : 'Há mais de 1min'}
          </span>
        </div>
      </div>
    </div>
  );
}
