import type { QuantSignal } from '../../types/quantStrategy';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Badge } from '../ui/badge';
import { TrendingUp, TrendingDown, AlertCircle, Target, Shield } from 'lucide-react';

interface SignalCardProps {
  signal: QuantSignal;
}

export function SignalCard({ signal }: SignalCardProps) {
  const getDirectionColor = (direction: string) => {
    if (direction === 'LONG') return 'bg-green-500/20 text-green-400 border-green-500/50';
    if (direction === 'SHORT') return 'bg-red-500/20 text-red-400 border-red-500/50';
    return 'bg-gray-500/20 text-gray-400 border-gray-500/50';
  };

  const getDirectionIcon = (direction: string) => {
    if (direction === 'LONG') return <TrendingUp className="w-5 h-5" />;
    if (direction === 'SHORT') return <TrendingDown className="w-5 h-5" />;
    return <AlertCircle className="w-5 h-5" />;
  };

  const getStrategyBadgeColor = (strategyType: string) => {
    if (strategyType === 'BREAKOUT') return 'bg-purple-500/20 text-purple-400 border-purple-500/50';
    if (strategyType === 'MEAN_REVERSION') return 'bg-blue-500/20 text-blue-400 border-blue-500/50';
    return 'bg-gray-500/20 text-gray-400 border-gray-500/50';
  };

  const getConfidenceColor = (score: number) => {
    if (score >= 80) return 'text-green-400';
    if (score >= 60) return 'text-blue-400';
    if (score >= 40) return 'text-yellow-400';
    return 'text-red-400';
  };

  return (
    <Card className="bg-gradient-to-br from-gray-800/80 to-gray-900/80 border-gray-700 shadow-xl">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className={`p-3 rounded-lg ${getDirectionColor(signal.direction)}`}>
              {getDirectionIcon(signal.direction)}
            </div>
            <div>
              <CardTitle className="text-2xl text-white flex items-center gap-2">
                {signal.direction} {signal.symbol}
                <Badge className={getStrategyBadgeColor(signal.strategyType)}>
                  {signal.strategyType.replace('_', ' ')}
                </Badge>
              </CardTitle>
              <CardDescription className="text-gray-400 mt-1">
                {signal.description}
              </CardDescription>
            </div>
          </div>
          <div className="text-right">
            <div className="text-sm text-gray-400">Confiança</div>
            <div className={`text-3xl font-bold ${getConfidenceColor(signal.confidenceScore)}`}>
              {signal.confidenceScore.toFixed(0)}%
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Entry & Risk Management */}
          <div className="space-y-4">
            <h4 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
              <Target className="w-4 h-4" />
              Preços de Entrada
            </h4>
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Entrada:</span>
                <span className="text-lg font-bold text-white">
                  {signal.entryPrice.toLocaleString('pt-BR', {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2,
                  })}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Stop Loss:</span>
                <span className="text-sm font-semibold text-red-400">
                  {signal.stopLoss.toLocaleString('pt-BR', {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2,
                  })}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Take Profit 1:</span>
                <span className="text-sm font-semibold text-green-400">
                  {signal.takeProfit1.toLocaleString('pt-BR', {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2,
                  })}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Take Profit 2:</span>
                <span className="text-sm font-semibold text-green-500">
                  {signal.takeProfit2.toLocaleString('pt-BR', {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2,
                  })}
                </span>
              </div>
            </div>
          </div>

          {/* Alignment Scores */}
          <div className="space-y-4">
            <h4 className="text-sm font-semibold text-gray-300 flex items-center gap-2">
              <Shield className="w-4 h-4" />
              Alinhamento dos Componentes
            </h4>
            <div className="space-y-3">
              <div>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs text-gray-400">Correlação</span>
                  <span className="text-xs font-semibold text-white">
                    {signal.correlationStrength.toFixed(0)}%
                  </span>
                </div>
                <div className="h-2 bg-gray-700 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-blue-500 transition-all"
                    style={{ width: `${signal.correlationStrength}%` }}
                  />
                </div>
              </div>

              <div>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs text-gray-400">GEX Alignment</span>
                  <span className="text-xs font-semibold text-white">
                    {signal.gexAlignment.toFixed(0)}%
                  </span>
                </div>
                <div className="h-2 bg-gray-700 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-purple-500 transition-all"
                    style={{ width: `${signal.gexAlignment}%` }}
                  />
                </div>
              </div>

              <div>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs text-gray-400">Wyckoff</span>
                  <span className="text-xs font-semibold text-white">
                    {signal.wyckoffAlignment.toFixed(0)}%
                  </span>
                </div>
                <div className="h-2 bg-gray-700 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-green-500 transition-all"
                    style={{ width: `${signal.wyckoffAlignment}%` }}
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Additional Info */}
          <div className="space-y-4">
            <h4 className="text-sm font-semibold text-gray-300">Informações Adicionais</h4>
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Bias Global:</span>
                <Badge
                  className={
                    signal.globalBias === 'BULLISH'
                      ? 'bg-green-500/20 text-green-400'
                      : signal.globalBias === 'BEARISH'
                      ? 'bg-red-500/20 text-red-400'
                      : 'bg-gray-500/20 text-gray-400'
                  }
                >
                  {signal.globalBias}
                </Badge>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Regime GEX:</span>
                <span className="text-xs font-semibold text-white">
                  {signal.gexRegime.replace('_', ' ')}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Fase Wyckoff:</span>
                <span className="text-xs font-semibold text-white">{signal.wyckoffPhase}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">ATR:</span>
                <span className="text-xs font-semibold text-white">
                  {signal.atrValue.toFixed(2)}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-400">Risk/Reward:</span>
                <span className="text-xs font-semibold text-green-400">
                  1:{signal.riskRewardRatio.toFixed(2)}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Observations */}
        {signal.observations && signal.observations.length > 0 && (
          <div className="mt-6 pt-4 border-t border-gray-700">
            <h4 className="text-sm font-semibold text-gray-300 mb-2">Observações:</h4>
            <ul className="space-y-1">
              {signal.observations.map((obs, idx) => (
                <li key={idx} className="text-xs text-gray-400 flex items-start gap-2">
                  <span className="text-blue-400 mt-0.5">•</span>
                  <span>{obs}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Timestamp */}
        <div className="mt-4 text-xs text-gray-500">
          Gerado em: {new Date(signal.createdAt).toLocaleString('pt-BR')}
        </div>
      </CardContent>
    </Card>
  );
}
