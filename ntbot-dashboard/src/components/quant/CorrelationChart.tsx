import type { CorrelationData } from '../../types/quantStrategy';
import { TrendingUp, TrendingDown, Activity } from 'lucide-react';

interface CorrelationChartProps {
  data: CorrelationData;
}

export function CorrelationChart({ data }: CorrelationChartProps) {
  const getCorrelationColor = (value: number) => {
    const abs = Math.abs(value);
    if (abs >= 0.8) return 'bg-green-500';
    if (abs >= 0.6) return 'bg-blue-500';
    if (abs >= 0.4) return 'bg-yellow-500';
    return 'bg-gray-500';
  };

  const getCorrelationLabel = (value: number) => {
    const abs = Math.abs(value);
    if (abs >= 0.8) return 'Muito Forte';
    if (abs >= 0.6) return 'Forte';
    if (abs >= 0.4) return 'Moderada';
    if (abs >= 0.2) return 'Fraca';
    return 'Muito Fraca';
  };

  const getBiasIcon = (bias: string) => {
    if (bias === 'BULLISH') return <TrendingUp className="w-5 h-5 text-green-400" />;
    if (bias === 'BEARISH') return <TrendingDown className="w-5 h-5 text-red-400" />;
    return <Activity className="w-5 h-5 text-gray-400" />;
  };

  return (
    <div className="space-y-6">
      {/* Correlation Bar */}
      <div>
        <div className="flex justify-between items-center mb-2">
          <span className="text-sm text-gray-400">Correlação de Pearson</span>
          <span className="text-sm font-semibold text-white">
            {(data.pearsonCorrelation * 100).toFixed(1)}%
          </span>
        </div>
        <div className="relative h-4 bg-gray-700 rounded-full overflow-hidden">
          <div
            className={`absolute top-0 left-0 h-full transition-all duration-500 ${getCorrelationColor(data.pearsonCorrelation)}`}
            style={{ width: `${Math.abs(data.pearsonCorrelation) * 100}%` }}
          />
        </div>
        <p className="text-xs text-gray-500 mt-1">
          {getCorrelationLabel(data.pearsonCorrelation)}
        </p>
      </div>

      {/* Leader Bias */}
      <div className="bg-gray-900/50 rounded-lg p-4">
        <div className="flex items-center justify-between mb-3">
          <span className="text-sm text-gray-400">Bias do Líder ({data.symbol1})</span>
          {getBiasIcon(data.leaderBias)}
        </div>
        <div className="text-2xl font-bold text-white mb-2">{data.leaderBias}</div>
        <div className="grid grid-cols-2 gap-3 text-sm">
          <div>
            <span className="text-gray-400">Momentum:</span>
            <p className={`font-semibold ${data.leaderMomentum > 0 ? 'text-green-400' : 'text-red-400'}`}>
              {data.leaderMomentum > 0 ? '+' : ''}{data.leaderMomentum.toFixed(2)}%
            </p>
          </div>
          <div>
            <span className="text-gray-400">Força:</span>
            <p className="font-semibold text-white">{data.trendStrength.toFixed(1)}</p>
          </div>
        </div>
      </div>

      {/* EMA Visualization */}
      <div className="bg-gray-900/50 rounded-lg p-4">
        <h4 className="text-sm font-semibold text-gray-300 mb-3">Médias Móveis (EMA)</h4>
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm text-gray-400">EMA 20</span>
            <span className="text-sm font-semibold text-blue-400">
              {data.leaderEMA20.toFixed(2)}
            </span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-sm text-gray-400">EMA 50</span>
            <span className="text-sm font-semibold text-purple-400">
              {data.leaderEMA50.toFixed(2)}
            </span>
          </div>
          {data.leaderEMA20 > data.leaderEMA50 ? (
            <p className="text-xs text-green-400 mt-2">✓ EMA 20 acima de EMA 50 (Bullish)</p>
          ) : (
            <p className="text-xs text-red-400 mt-2">✓ EMA 20 abaixo de EMA 50 (Bearish)</p>
          )}
        </div>
      </div>

      {/* Info */}
      <div className="text-xs text-gray-500">
        <p>Lookback: {data.lookbackPeriod} períodos</p>
        <p>Atualizado: {new Date(data.calculatedAt).toLocaleString('pt-BR')}</p>
      </div>
    </div>
  );
}
