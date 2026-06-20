import { useEffect, useState } from 'react';
import { quantStrategyApi } from '../services/quantStrategyApi';
import type { DashboardData } from '../types/quantStrategy';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { TrendingUp, TrendingDown, Activity } from 'lucide-react';
import { CorrelationChart } from '../components/quant/CorrelationChart';
import { GEXChart } from '../components/quant/GEXChart';
import { SignalCard } from '../components/quant/SignalCard';

export function QuantStrategyPage() {
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [symbol, setSymbol] = useState('WINFUT');
  const [leaderSymbol] = useState('NQ');

  useEffect(() => {
    loadData();
    const interval = setInterval(loadData, 30000); // Atualiza a cada 30s
    return () => clearInterval(interval);
  }, [symbol, leaderSymbol]);

  const loadData = async () => {
    try {
      setLoading(true);
      const dashboardData = await quantStrategyApi.getDashboard(symbol, leaderSymbol);
      setData(dashboardData);
    } catch (error) {
      console.error('Erro ao carregar dashboard:', error);
    } finally {
      setLoading(false);
    }
  };

  const getBiasColor = (bias: string) => {
    if (bias === 'BULLISH') return 'text-green-500';
    if (bias === 'BEARISH') return 'text-red-500';
    return 'text-gray-500';
  };

  const getBiasIcon = (bias: string) => {
    if (bias === 'BULLISH') return <TrendingUp className="w-4 h-4" />;
    if (bias === 'BEARISH') return <TrendingDown className="w-4 h-4" />;
    return <Activity className="w-4 h-4" />;
  };

  const getGEXColor = (regime: string) => {
    switch (regime) {
      case 'NEGATIVE_HIGH':
        return 'bg-red-500/20 text-red-400 border-red-500/50';
      case 'NEGATIVE_LOW':
        return 'bg-orange-500/20 text-orange-400 border-orange-500/50';
      case 'POSITIVE_LOW':
        return 'bg-blue-500/20 text-blue-400 border-blue-500/50';
      case 'POSITIVE_HIGH':
        return 'bg-green-500/20 text-green-400 border-green-500/50';
      default:
        return 'bg-gray-500/20 text-gray-400 border-gray-500/50';
    }
  };

  const getGEXDescription = (regime: string) => {
    switch (regime) {
      case 'NEGATIVE_HIGH':
        return 'Alta volatilidade - Breakouts prováveis';
      case 'NEGATIVE_LOW':
        return 'Início de expansão';
      case 'POSITIVE_LOW':
        return 'Transição - Compressão leve';
      case 'POSITIVE_HIGH':
        return 'Forte compressão - Mean reversion';
      default:
        return 'Mercado neutro';
    }
  };

  if (loading && !data) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500 mx-auto mb-4"></div>
          <p className="text-gray-400">Carregando estratégia quantitativa...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-white">Estratégia Quantitativa</h1>
          <p className="text-gray-400 mt-1">
            Integração: Correlação Global + GEX + Wyckoff
          </p>
        </div>
        <div className="flex items-center gap-4">
          <Select value={symbol} onValueChange={setSymbol}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Selecione o ativo" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="WINFUT">WIN (Ibovespa)</SelectItem>
              <SelectItem value="WDOFUT">WDO (Dólar)</SelectItem>
            </SelectContent>
          </Select>
          <Button onClick={loadData} variant="outline">
            Atualizar
          </Button>
        </div>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Current Price */}
        <Card className="bg-gray-800/50 border-gray-700">
          <CardHeader className="pb-2">
            <CardDescription>Preço Atual</CardDescription>
            <CardTitle className="text-2xl text-white">
              {data?.currentPrice.toLocaleString('pt-BR', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              })}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-gray-400">{symbol}</p>
          </CardContent>
        </Card>

        {/* Global Bias */}
        <Card className="bg-gray-800/50 border-gray-700">
          <CardHeader className="pb-2">
            <CardDescription>Bias Global (NQ)</CardDescription>
            <div className={`flex items-center gap-2 ${getBiasColor(data?.correlation?.leaderBias || 'NEUTRAL')}`}>
              {getBiasIcon(data?.correlation?.leaderBias || 'NEUTRAL')}
              <CardTitle className="text-2xl">
                {data?.correlation?.leaderBias || 'NEUTRAL'}
              </CardTitle>
            </div>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-gray-400">
              Correlação: {((data?.correlation?.pearsonCorrelation || 0) * 100).toFixed(0)}%
            </p>
          </CardContent>
        </Card>

        {/* GEX Regime */}
        <Card className="bg-gray-800/50 border-gray-700">
          <CardHeader className="pb-2">
            <CardDescription>Regime GEX</CardDescription>
            <Badge className={getGEXColor(data?.gex?.regime || 'NEUTRAL')}>
              {data?.gex?.regime || 'NEUTRAL'}
            </Badge>
          </CardHeader>
          <CardContent>
            <p className="text-xs text-gray-400">
              {getGEXDescription(data?.gex?.regime || 'NEUTRAL')}
            </p>
          </CardContent>
        </Card>

        {/* Wyckoff Phase */}
        <Card className="bg-gray-800/50 border-gray-700">
          <CardHeader className="pb-2">
            <CardDescription>Fase Wyckoff</CardDescription>
            <CardTitle className="text-xl text-white">
              {data?.signal?.wyckoffPhase || 'N/A'}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-gray-400">Estrutura de mercado</p>
          </CardContent>
        </Card>
      </div>

      {/* Signal Card */}
      {data?.signal && (
        <SignalCard signal={data.signal} />
      )}

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Correlation Chart */}
        {data?.correlation && (
          <Card className="bg-gray-800/50 border-gray-700">
            <CardHeader>
              <CardTitle className="text-white">Correlação NQ / {symbol}</CardTitle>
              <CardDescription>
                Análise de correlação e momentum do líder global
              </CardDescription>
            </CardHeader>
            <CardContent>
              <CorrelationChart data={data.correlation} />
            </CardContent>
          </Card>
        )}

        {/* GEX Chart */}
        {data?.gex && (
          <Card className="bg-gray-800/50 border-gray-700">
            <CardHeader>
              <CardTitle className="text-white">Gamma Exposure (GEX)</CardTitle>
              <CardDescription>
                Distribuição de gamma e identificação de walls
              </CardDescription>
            </CardHeader>
            <CardContent>
              <GEXChart data={data.gex} />
            </CardContent>
          </Card>
        )}
      </div>

      {/* Detailed Info */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Correlation Details */}
        {data?.correlation && (
          <Card className="bg-gray-800/50 border-gray-700">
            <CardHeader>
              <CardTitle className="text-white">Detalhes da Correlação</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Pearson Correlation:</span>
                  <span className="text-white font-semibold">
                    {(data.correlation.pearsonCorrelation * 100).toFixed(2)}%
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Spearman Correlation:</span>
                  <span className="text-white font-semibold">
                    {(data.correlation.spearmanCorrelation * 100).toFixed(2)}%
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Momentum (NQ):</span>
                  <span className={`font-semibold ${data.correlation.leaderMomentum > 0 ? 'text-green-400' : 'text-red-400'}`}>
                    {data.correlation.leaderMomentum.toFixed(2)}%
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Força da Tendência:</span>
                  <span className="text-white font-semibold">
                    {data.correlation.trendStrength.toFixed(1)}/100
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">EMA 20:</span>
                  <span className="text-white font-semibold">
                    {data.correlation.leaderEMA20.toFixed(2)}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">EMA 50:</span>
                  <span className="text-white font-semibold">
                    {data.correlation.leaderEMA50.toFixed(2)}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>
        )}

        {/* GEX Details */}
        {data?.gex && (
          <Card className="bg-gray-800/50 border-gray-700">
            <CardHeader>
              <CardTitle className="text-white">Detalhes do GEX</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Total GEX:</span>
                  <span className="text-white font-semibold">
                    {data.gex.totalGEX.toFixed(0)}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Net Gamma:</span>
                  <span className="text-white font-semibold">
                    {data.gex.netGamma.toFixed(2)}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Gamma Flip Level:</span>
                  <span className="text-white font-semibold">
                    {data.gex.gammaFlipLevel?.toFixed(2) || 'N/A'}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Potencial de Expansão:</span>
                  <span className="text-green-400 font-semibold">
                    {data.gex.expansionPotential.toFixed(0)}%
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-400">Potencial Mean Reversion:</span>
                  <span className="text-blue-400 font-semibold">
                    {data.gex.meanReversionPotential.toFixed(0)}%
                  </span>
                </div>

                {/* Gamma Walls */}
                {data.gex.gammaWalls.length > 0 && (
                  <div className="mt-4 pt-4 border-t border-gray-700">
                    <p className="text-sm font-semibold text-gray-300 mb-2">Gamma Walls:</p>
                    <div className="space-y-2">
                      {data.gex.gammaWalls.slice(0, 3).map((wall, idx) => (
                        <div key={idx} className="flex justify-between items-center text-sm">
                          <span className={wall.type === 'Resistance' ? 'text-red-400' : 'text-green-400'}>
                            {wall.type}
                          </span>
                          <span className="text-white">{wall.strike.toFixed(2)}</span>
                          <span className="text-gray-400">
                            {wall.distance > 0 ? '+' : ''}{wall.distance.toFixed(2)}%
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
