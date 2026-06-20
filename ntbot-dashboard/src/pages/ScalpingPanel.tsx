import React, { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { TrendingUp, TrendingDown, DollarSign, Activity } from 'lucide-react';
import { useTradingStore } from '../stores/trading.store';
import { profitChartSignalR } from '../services/profitchart.signalr';

interface TickerData {
  symbol: string;
  bid: number;
  ask: number;
  spread: number;
  lastUpdate: Date;
  trend: 'up' | 'down' | 'neutral';
}

const ScalpingPanel: React.FC = () => {
  const [tickers, setTickers] = useState<TickerData[]>([]);
  // const [selectedSymbol] = useState<string>('WIN');
  const { isConnected } = useTradingStore();

  useEffect(() => {
    // Initialize scalping symbols
    const initialTickers: TickerData[] = [
      { symbol: 'WIN', bid: 0, ask: 0, spread: 0, lastUpdate: new Date(), trend: 'neutral' },
      { symbol: 'WDO', bid: 0, ask: 0, spread: 0, lastUpdate: new Date(), trend: 'neutral' },
      { symbol: 'PETR4', bid: 0, ask: 0, spread: 0, lastUpdate: new Date(), trend: 'neutral' },
    ];
    setTickers(initialTickers);

    // Connect to SignalR for real-time updates
    if (isConnected) {
      profitChartSignalR.connect().then(() => {
        profitChartSignalR.subscribeAll();
      });

      const unsubscribe = profitChartSignalR.onTickUpdate((update) => {
        setTickers(prev => prev.map(ticker =>
          ticker.symbol === update.ticker
            ? {
                ...ticker,
                bid: Number(update.value),
                ask: Number(update.value) + (ticker.spread / 100000),
                lastUpdate: new Date(),
                trend: Number(update.value) > ticker.bid ? 'up' : Number(update.value) < ticker.bid ? 'down' : ticker.trend
              }
            : ticker
        ));
      });

      return () => unsubscribe();
    }
  }, [isConnected]);

  const handleBuy = (symbol: string) => {
    console.log('Buy order:', symbol);
    // Implement buy logic
  };

  const handleSell = (symbol: string) => {
    console.log('Sell order:', symbol);
    // Implement sell logic
  };

  const formatPrice = (price: number) => price.toFixed(2);
  const formatSpread = (spread: number) => `${spread} pts`;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-white">Scalping Panel</h1>
        <Badge variant={isConnected ? "default" : "destructive"}>
          {isConnected ? "Connected" : "Disconnected"}
        </Badge>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {tickers.map((ticker) => (
          <Card key={ticker.symbol} className="bg-slate-800 border-slate-700">
            <CardHeader className="pb-3">
              <CardTitle className="text-white flex items-center justify-between">
                <span>{ticker.symbol}</span>
                <div className="flex items-center space-x-2">
                  {ticker.trend === 'up' && <TrendingUp className="h-4 w-4 text-green-500" />}
                  {ticker.trend === 'down' && <TrendingDown className="h-4 w-4 text-red-500" />}
                  <Activity className="h-4 w-4 text-blue-500" />
                </div>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-slate-400">Bid</p>
                  <p className="text-lg font-semibold text-green-500">
                    {formatPrice(ticker.bid)}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-slate-400">Ask</p>
                  <p className="text-lg font-semibold text-red-500">
                    {formatPrice(ticker.ask)}
                  </p>
                </div>
              </div>

              <div>
                <p className="text-sm text-slate-400">Spread</p>
                <p className="text-sm text-yellow-500">{formatSpread(ticker.spread)}</p>
              </div>

              <div className="flex space-x-2">
                <Button
                  onClick={() => handleBuy(ticker.symbol)}
                  className="flex-1 bg-green-600 hover:bg-green-700"
                  size="sm"
                >
                  BUY
                </Button>
                <Button
                  onClick={() => handleSell(ticker.symbol)}
                  className="flex-1 bg-red-600 hover:bg-red-700"
                  size="sm"
                >
                  SELL
                </Button>
              </div>

              <p className="text-xs text-slate-500">
                Updated: {ticker.lastUpdate.toLocaleTimeString()}
              </p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Quick Stats */}
      <Card className="bg-slate-800 border-slate-700">
        <CardHeader>
          <CardTitle className="text-white">Quick Stats</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="text-center">
              <DollarSign className="h-8 w-8 text-green-500 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">$1,250.00</p>
              <p className="text-sm text-slate-400">Daily P&L</p>
            </div>
            <div className="text-center">
              <Activity className="h-8 w-8 text-blue-500 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">24</p>
              <p className="text-sm text-slate-400">Trades Today</p>
            </div>
            <div className="text-center">
              <TrendingUp className="h-8 w-8 text-green-500 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">75%</p>
              <p className="text-sm text-slate-400">Win Rate</p>
            </div>
            <div className="text-center">
              <TrendingDown className="h-8 w-8 text-red-500 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">2.1%</p>
              <p className="text-sm text-slate-400">Max DD</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default ScalpingPanel;