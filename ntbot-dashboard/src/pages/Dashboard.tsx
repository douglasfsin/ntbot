import { useEffect, useState } from 'react';
import { TrendingUp, TrendingDown, Activity, DollarSign, PieChart } from 'lucide-react';
import { Link } from 'react-router-dom';
import { apiService } from '../services/api.service';
import { profitChartApi } from '../services/profitchart.api';
import { useTradingStore } from '../stores/trading.store';
import { TickerCard } from '../components/profitchart';
import type { TickerStatus } from '../types/profitchart';

export function Dashboard() {
  const { activeSignals, openTrades } = useTradingStore();
  const [profitChartTickers, setProfitChartTickers] = useState<Record<string, TickerStatus>>({});
  const [isLoadingTickers, setIsLoadingTickers] = useState(true);
  const [stats, setStats] = useState({
    todayPnL: 0,
    todayTrades: 0,
    winRate: 0,
    activeSignals: 0
  });

  const loadDashboardData = async () => {
    try {
      const [signalsData, tradesData] = await Promise.all([
        apiService.getSignals(),
        apiService.getTrades()
      ]);

      // Calculate stats
      const today = new Date().toISOString().split('T')[0];
      const todayTrades = tradesData.filter((t: { enteredAt?: string }) => 
        t.enteredAt?.startsWith(today)
      );
      
      const wins = todayTrades.filter((t: { status?: string }) => t.status === 'CLOSED_PROFIT').length;
      const todayPnL = todayTrades.reduce((sum: number, t: { pnl?: number }) => sum + (t.pnl || 0), 0);

      setStats({
        todayPnL,
        todayTrades: todayTrades.length,
        winRate: todayTrades.length > 0 ? (wins / todayTrades.length) * 100 : 0,
        activeSignals: signalsData.filter((s: { status?: string }) => s.status === 'ACTIVE').length
      });
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
    }
  };

  useEffect(() => {
    loadDashboardData();
  }, []);

  // Carregar tickers disponíveis do ProfitChart RTD
  useEffect(() => {
    const loadProfitChartTickers = async () => {
      try {
        const tickers = await profitChartApi.getAllTickers();
        setProfitChartTickers(tickers);
      } catch (error) {
        console.error('Failed to load ProfitChart tickers:', error);
      } finally {
        setIsLoadingTickers(false);
      }
    };

    loadProfitChartTickers();
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-white mb-2">Dashboard</h2>
        <p className="text-slate-400">Visão geral do sistema de trading</p>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <div className="card">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-slate-400 mb-1">P&L Hoje</p>
              <p className={`text-2xl font-bold ${stats.todayPnL >= 0 ? 'text-success' : 'text-danger'}`}>
                ${stats.todayPnL.toFixed(2)}
              </p>
            </div>
            <DollarSign className={`w-10 h-10 ${stats.todayPnL >= 0 ? 'text-success' : 'text-danger'}`} />
          </div>
        </div>

        <div className="card">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-slate-400 mb-1">Trades Hoje</p>
              <p className="text-2xl font-bold text-white">{stats.todayTrades}</p>
            </div>
            <Activity className="w-10 h-10 text-primary-500" />
          </div>
        </div>

        <div className="card">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-slate-400 mb-1">Win Rate</p>
              <p className="text-2xl font-bold text-white">{stats.winRate.toFixed(1)}%</p>
            </div>
            {stats.winRate >= 50 ? (
              <TrendingUp className="w-10 h-10 text-success" />
            ) : (
              <TrendingDown className="w-10 h-10 text-danger" />
            )}
          </div>
        </div>

        <div className="card">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-slate-400 mb-1">Sinais Ativos</p>
              <p className="text-2xl font-bold text-white">{stats.activeSignals}</p>
            </div>
            <Activity className="w-10 h-10 text-warning" />
          </div>
        </div>
      </div>

      {/* ProfitChart Real-Time Section */}
      <div className="card">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-xl font-bold text-white flex items-center gap-2">
            <PieChart className="w-6 h-6 text-blue-500" />
            ProfitChart - Tempo Real
          </h3>
          <Link 
            to="/profitchart" 
            className="text-sm text-blue-500 hover:text-blue-400 transition-colors"
          >
            Ver Todos →
          </Link>
        </div>
        
        {isLoadingTickers ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="bg-slate-700 rounded-lg p-4 animate-pulse h-32" />
            ))}
          </div>
        ) : Object.keys(profitChartTickers).length > 0 ? (
          <>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {Object.entries(profitChartTickers).slice(0, 3).map(([logicalName, status]) => (
                <TickerCard 
                  key={logicalName}
                  ticker={logicalName}
                  logicalName={status.logicalName || logicalName}
                  autoSubscribe={true} 
                />
              ))}
            </div>
            <p className="text-sm text-slate-400 mt-4">
              Dados em tempo real via RTD do ProfitChart. {Object.keys(profitChartTickers).length > 3 && 'Clique em "Ver Todos" para mais tickers.'}
            </p>
          </>
        ) : (
          <div className="text-center py-8">
            <Activity className="w-12 h-12 text-slate-600 mx-auto mb-3" />
            <p className="text-slate-400">Nenhum ticker configurado</p>
            <p className="text-sm text-slate-500 mt-1">
              Configure tickers em <code className="bg-slate-700 px-2 py-0.5 rounded">rtd_config.json</code>
            </p>
          </div>
        )}
      </div>

      {/* Active Signals */}
      <div className="card">
        <h3 className="text-xl font-bold text-white mb-4">Sinais Ativos</h3>
        {activeSignals.length > 0 ? (
          <div className="space-y-3">
            {activeSignals.slice(0, 5).map((signal) => (
              <div key={signal.id} className="flex items-center justify-between p-3 bg-slate-700 rounded-lg">
                <div>
                  <span className="font-semibold text-white">{signal.symbol}</span>
                  <span className={`ml-2 ${signal.direction === 'LONG' ? 'text-success' : 'text-danger'}`}>
                    {signal.direction}
                  </span>
                  <span className="ml-2 text-sm text-slate-400">{signal.timeframe}</span>
                </div>
                <div className="text-right">
                  <div className="text-white font-semibold">${signal.entryPrice.toFixed(2)}</div>
                  <div className="text-sm text-slate-400">Conf: {signal.confidence}%</div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-slate-400">Nenhum sinal ativo no momento</p>
        )}
      </div>

      {/* Open Trades */}
      <div className="card">
        <h3 className="text-xl font-bold text-white mb-4">Posições Abertas</h3>
        {openTrades.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Símbolo</th>
                  <th>Direção</th>
                  <th>Entrada</th>
                  <th>Stop</th>
                  <th>Target</th>
                  <th>P&L</th>
                </tr>
              </thead>
              <tbody>
                {openTrades.map((trade) => (
                  <tr key={trade.id}>
                    <td className="font-semibold">{trade.symbol}</td>
                    <td>
                      <span className={trade.direction === 'LONG' ? 'text-success' : 'text-danger'}>
                        {trade.direction}
                      </span>
                    </td>
                    <td>${trade.entryPrice.toFixed(2)}</td>
                    <td>${trade.stopLoss.toFixed(2)}</td>
                    <td>${trade.takeProfit.toFixed(2)}</td>
                    <td className={trade.pnl && trade.pnl >= 0 ? 'text-success' : 'text-danger'}>
                      ${trade.pnl?.toFixed(2) || '0.00'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-slate-400">Nenhuma posição aberta</p>
        )}
      </div>
    </div>
  );
}
