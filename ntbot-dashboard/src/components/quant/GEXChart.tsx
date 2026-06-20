import type { GammaExposureData } from '../../types/quantStrategy';
import { Shield, TrendingUp, Activity } from 'lucide-react';

interface GEXChartProps {
  data: GammaExposureData;
}

export function GEXChart({ data }: GEXChartProps) {
  const getRegimeColor = (regime: string) => {
    switch (regime) {
      case 'NEGATIVE_HIGH':
        return 'text-red-400';
      case 'NEGATIVE_LOW':
        return 'text-orange-400';
      case 'POSITIVE_LOW':
        return 'text-blue-400';
      case 'POSITIVE_HIGH':
        return 'text-green-400';
      default:
        return 'text-gray-400';
    }
  };

  const getRegimeIcon = (regime: string) => {
    if (regime.startsWith('NEGATIVE')) return <TrendingUp className="w-5 h-5" />;
    if (regime.startsWith('POSITIVE')) return <Shield className="w-5 h-5" />;
    return <Activity className="w-5 h-5" />;
  };

  const sortedWalls = [...data.gammaWalls].sort((a, b) => a.strike - b.strike);

  return (
    <div className="space-y-6">
      {/* GEX Overview */}
      <div className="bg-gray-900/50 rounded-lg p-4">
        <div className="flex items-center justify-between mb-3">
          <span className="text-sm text-gray-400">Regime GEX</span>
          <div className={`flex items-center gap-2 ${getRegimeColor(data.regime)}`}>
            {getRegimeIcon(data.regime)}
          </div>
        </div>
        <div className={`text-xl font-bold mb-2 ${getRegimeColor(data.regime)}`}>
          {data.regime.replace('_', ' ')}
        </div>
        <div className="text-sm text-gray-400">
          Total GEX: <span className="text-white font-semibold">{data.totalGEX.toFixed(0)}</span>
        </div>
      </div>

      {/* Potential Bars */}
      <div className="space-y-3">
        <div>
          <div className="flex justify-between items-center mb-1">
            <span className="text-xs text-gray-400">Potencial de Expansão</span>
            <span className="text-xs font-semibold text-green-400">
              {data.expansionPotential.toFixed(0)}%
            </span>
          </div>
          <div className="h-2 bg-gray-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-green-500 transition-all duration-500"
              style={{ width: `${data.expansionPotential}%` }}
            />
          </div>
        </div>

        <div>
          <div className="flex justify-between items-center mb-1">
            <span className="text-xs text-gray-400">Potencial Mean Reversion</span>
            <span className="text-xs font-semibold text-blue-400">
              {data.meanReversionPotential.toFixed(0)}%
            </span>
          </div>
          <div className="h-2 bg-gray-700 rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-500 transition-all duration-500"
              style={{ width: `${data.meanReversionPotential}%` }}
            />
          </div>
        </div>
      </div>

      {/* Gamma Flip Level */}
      {data.gammaFlipLevel && (
        <div className="bg-gray-900/50 rounded-lg p-4">
          <h4 className="text-sm font-semibold text-gray-300 mb-2">Gamma Flip Level</h4>
          <div className="text-2xl font-bold text-yellow-400">
            {data.gammaFlipLevel.toFixed(2)}
          </div>
          <p className="text-xs text-gray-500 mt-1">
            Nível onde GEX muda de sinal (positivo ↔ negativo)
          </p>
        </div>
      )}

      {/* Gamma Walls */}
      {sortedWalls.length > 0 && (
        <div className="bg-gray-900/50 rounded-lg p-4">
          <h4 className="text-sm font-semibold text-gray-300 mb-3">Gamma Walls</h4>
          <div className="space-y-2">
            {sortedWalls.map((wall, idx) => {
              const isResistance = wall.type === 'Resistance';
              const distanceFromPrice = ((wall.strike - data.currentPrice) / data.currentPrice) * 100;

              return (
                <div
                  key={idx}
                  className="flex items-center justify-between p-2 rounded bg-gray-800/50"
                >
                  <div className="flex items-center gap-2">
                    <Shield
                      className={`w-4 h-4 ${
                        isResistance ? 'text-red-400' : 'text-green-400'
                      }`}
                    />
                    <span className={`text-sm font-semibold ${
                      isResistance ? 'text-red-400' : 'text-green-400'
                    }`}>
                      {wall.type}
                    </span>
                  </div>
                  <div className="text-right">
                    <div className="text-sm font-semibold text-white">
                      {wall.strike.toFixed(2)}
                    </div>
                    <div className="text-xs text-gray-400">
                      {distanceFromPrice > 0 ? '+' : ''}{distanceFromPrice.toFixed(2)}%
                    </div>
                  </div>
                  <div className="text-xs text-gray-500">
                    {wall.gammaConcentration.toFixed(0)}%
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Visual Representation */}
      <div className="bg-gray-900/50 rounded-lg p-4">
        <h4 className="text-sm font-semibold text-gray-300 mb-3">Distribuição Visual</h4>
        <div className="relative h-24">
          {/* Current Price Line */}
          <div className="absolute left-1/2 top-0 bottom-0 w-0.5 bg-white z-10">
            <div className="absolute -left-8 top-1/2 -translate-y-1/2 text-xs text-white font-semibold whitespace-nowrap">
              Preço Atual
            </div>
          </div>

          {/* Gamma Walls Visualization */}
          {sortedWalls.map((wall, idx) => {
            const distancePercent = ((wall.strike - data.currentPrice) / data.currentPrice) * 100;
            const position = 50 + (distancePercent * 5); // Scale for visualization
            const clampedPosition = Math.max(5, Math.min(95, position));

            return (
              <div
                key={idx}
                className="absolute top-0 bottom-0 w-1"
                style={{ left: `${clampedPosition}%` }}
              >
                <div
                  className={`h-full opacity-60 ${
                    wall.type === 'Resistance' ? 'bg-red-500' : 'bg-green-500'
                  }`}
                  style={{ width: `${Math.max(2, wall.gammaConcentration / 10)}px` }}
                />
              </div>
            );
          })}
        </div>
        <div className="flex justify-between mt-2 text-xs text-gray-500">
          <span>Suporte</span>
          <span>Resistência</span>
        </div>
      </div>

      {/* Info */}
      <div className="text-xs text-gray-500">
        <p>Preço Atual: {data.currentPrice.toFixed(2)}</p>
        <p>Atualizado: {new Date(data.calculatedAt).toLocaleString('pt-BR')}</p>
      </div>
    </div>
  );
}
