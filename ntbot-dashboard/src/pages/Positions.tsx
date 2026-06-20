import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { TrendingUp, TrendingDown, X, AlertTriangle } from 'lucide-react';

interface Position {
  id: string;
  symbol: string;
  side: 'buy' | 'sell';
  quantity: number;
  entryPrice: number;
  currentPrice: number;
  pnl: number;
  pnlPercent: number;
  stopLoss?: number;
  takeProfit?: number;
  broker: 'NTBot' | 'MT5' | 'NinjaTrader';
  timestamp: Date;
  status: 'open' | 'closing';
}

const Positions: React.FC = () => {
  const [positions, setPositions] = useState<Position[]>([]);
  const [filter, setFilter] = useState<'all' | 'profitable' | 'losing'>('all');

  useEffect(() => {
    // Mock positions data
    const mockPositions: Position[] = [
      {
        id: '1',
        symbol: 'EURUSD',
        side: 'buy',
        quantity: 0.1,
        entryPrice: 1.0820,
        currentPrice: 1.0850,
        pnl: 30.00,
        pnlPercent: 2.77,
        stopLoss: 1.0780,
        takeProfit: 1.0900,
        broker: 'MT5',
        timestamp: new Date(Date.now() - 3600000),
        status: 'open'
      },
      {
        id: '2',
        symbol: 'WIN',
        side: 'sell',
        quantity: 1,
        entryPrice: 135000,
        currentPrice: 134500,
        pnl: 500.00,
        pnlPercent: 0.37,
        stopLoss: 136000,
        takeProfit: 133000,
        broker: 'NinjaTrader',
        timestamp: new Date(Date.now() - 1800000),
        status: 'open'
      },
      {
        id: '3',
        symbol: 'PETR4',
        side: 'buy',
        quantity: 100,
        entryPrice: 28.50,
        currentPrice: 27.80,
        pnl: -70.00,
        pnlPercent: -2.46,
        stopLoss: 27.00,
        takeProfit: 30.00,
        broker: 'NTBot',
        timestamp: new Date(Date.now() - 900000),
        status: 'open'
      }
    ];
    setPositions(mockPositions);
  }, []);

  const filteredPositions = positions.filter(pos => {
    if (filter === 'profitable') return pos.pnl > 0;
    if (filter === 'losing') return pos.pnl < 0;
    return true;
  });

  const totalPnL = positions.reduce((sum, pos) => sum + pos.pnl, 0);
  const winningPositions = positions.filter(pos => pos.pnl > 0).length;
  const losingPositions = positions.filter(pos => pos.pnl < 0).length;

  const handleClosePosition = (id: string) => {
    setPositions(prev => prev.map(pos =>
      pos.id === id ? { ...pos, status: 'closing' as const } : pos
    ));

    // Simulate closing after 2 seconds
    setTimeout(() => {
      setPositions(prev => prev.filter(pos => pos.id !== id));
    }, 2000);
  };

  const formatPrice = (price: number, symbol: string) => {
    if (symbol.includes('USD') || symbol === 'EURUSD') {
      return price.toFixed(4);
    }
    if (symbol === 'WIN' || symbol === 'WDO') {
      return price.toLocaleString();
    }
    return price.toFixed(2);
  };

  const getBrokerColor = (broker: string) => {
    switch (broker) {
      case 'MT5': return 'bg-blue-600';
      case 'NinjaTrader': return 'bg-purple-600';
      case 'NTBot': return 'bg-green-600';
      default: return 'bg-gray-600';
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-white">Positions</h1>
        <div className="flex space-x-2">
          <Button
            onClick={() => setFilter('all')}
            variant={filter === 'all' ? 'default' : 'outline'}
            size="sm"
            className={filter === 'all' ? '' : 'border-slate-600 text-white hover:bg-slate-700'}
          >
            All ({positions.length})
          </Button>
          <Button
            onClick={() => setFilter('profitable')}
            variant={filter === 'profitable' ? 'default' : 'outline'}
            size="sm"
            className={filter === 'profitable' ? 'bg-green-600 hover:bg-green-700' : 'border-slate-600 text-white hover:bg-slate-700'}
          >
            Profitable ({winningPositions})
          </Button>
          <Button
            onClick={() => setFilter('losing')}
            variant={filter === 'losing' ? 'default' : 'outline'}
            size="sm"
            className={filter === 'losing' ? 'bg-red-600 hover:bg-red-700' : 'border-slate-600 text-white hover:bg-slate-700'}
          >
            Losing ({losingPositions})
          </Button>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Total P&L</p>
                <p className={`text-2xl font-bold ${totalPnL >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                  ${totalPnL.toFixed(2)}
                </p>
              </div>
              {totalPnL >= 0 ? (
                <TrendingUp className="h-8 w-8 text-green-500" />
              ) : (
                <TrendingDown className="h-8 w-8 text-red-500" />
              )}
            </div>
          </CardContent>
        </Card>

        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Open Positions</p>
                <p className="text-2xl font-bold text-white">{positions.length}</p>
              </div>
              <AlertTriangle className="h-8 w-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Win Rate</p>
                <p className="text-2xl font-bold text-blue-500">
                  {positions.length > 0 ? ((winningPositions / positions.length) * 100).toFixed(1) : 0}%
                </p>
              </div>
              <TrendingUp className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Positions Table */}
      <Card className="bg-slate-800 border-slate-700">
        <CardHeader>
          <CardTitle className="text-white">Open Positions</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow className="border-slate-700">
                <TableHead className="text-slate-400">Symbol</TableHead>
                <TableHead className="text-slate-400">Side</TableHead>
                <TableHead className="text-slate-400">Quantity</TableHead>
                <TableHead className="text-slate-400">Entry Price</TableHead>
                <TableHead className="text-slate-400">Current Price</TableHead>
                <TableHead className="text-slate-400">P&L</TableHead>
                <TableHead className="text-slate-400">Stop Loss</TableHead>
                <TableHead className="text-slate-400">Take Profit</TableHead>
                <TableHead className="text-slate-400">Broker</TableHead>
                <TableHead className="text-slate-400">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredPositions.map((position) => (
                <TableRow key={position.id} className="border-slate-700">
                  <TableCell className="text-white font-medium">{position.symbol}</TableCell>
                  <TableCell>
                    <Badge variant={position.side === 'buy' ? 'default' : 'destructive'}>
                      {position.side.toUpperCase()}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-white">{position.quantity}</TableCell>
                  <TableCell className="text-white font-mono">
                    {formatPrice(position.entryPrice, position.symbol)}
                  </TableCell>
                  <TableCell className="text-white font-mono">
                    {formatPrice(position.currentPrice, position.symbol)}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col">
                      <span className={`font-semibold ${position.pnl >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                        ${position.pnl.toFixed(2)}
                      </span>
                      <span className={`text-sm ${position.pnlPercent >= 0 ? 'text-green-400' : 'text-red-400'}`}>
                        ({position.pnlPercent >= 0 ? '+' : ''}{position.pnlPercent.toFixed(2)}%)
                      </span>
                    </div>
                  </TableCell>
                  <TableCell className="text-white font-mono">
                    {position.stopLoss ? formatPrice(position.stopLoss, position.symbol) : '-'}
                  </TableCell>
                  <TableCell className="text-white font-mono">
                    {position.takeProfit ? formatPrice(position.takeProfit, position.symbol) : '-'}
                  </TableCell>
                  <TableCell>
                    <Badge className={getBrokerColor(position.broker)}>
                      {position.broker}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      onClick={() => handleClosePosition(position.id)}
                      size="sm"
                      variant="destructive"
                      disabled={position.status === 'closing'}
                    >
                      {position.status === 'closing' ? 'Closing...' : <X className="h-4 w-4" />}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
};

export default Positions;