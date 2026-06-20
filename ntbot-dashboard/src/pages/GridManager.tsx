import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Switch } from '../components/ui/switch';
import { Play, Pause, Settings } from 'lucide-react';

interface GridOrder {
  id: string;
  symbol: string;
  basePrice: number;
  stepSize: number;
  maxLevels: number;
  lotSize: number;
  isActive: boolean;
  currentLevel: number;
  totalProfit: number;
  levels: GridLevel[];
}

interface GridLevel {
  level: number;
  price: number;
  volume: number;
  isFilled: boolean;
  profit: number;
}

const GridManager: React.FC = () => {
  const [gridOrders, setGridOrders] = useState<GridOrder[]>([]);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [newOrder, setNewOrder] = useState({
    symbol: 'EURUSD',
    basePrice: 1.0820,
    stepSize: 0.0010,
    maxLevels: 10,
    lotSize: 0.01,
    useMartingale: false
  });

  useEffect(() => {
    // Load existing grid orders
    const mockOrders: GridOrder[] = [
      {
        id: '1',
        symbol: 'EURUSD',
        basePrice: 1.0820,
        stepSize: 0.0010,
        maxLevels: 10,
        lotSize: 0.01,
        isActive: true,
        currentLevel: 3,
        totalProfit: 45.50,
        levels: [
          { level: 1, price: 1.0830, volume: 0.01, isFilled: true, profit: 15.00 },
          { level: 2, price: 1.0810, volume: 0.01, isFilled: true, profit: 12.50 },
          { level: 3, price: 1.0840, volume: 0.01, isFilled: false, profit: 0 },
        ]
      }
    ];
    setGridOrders(mockOrders);
  }, []);

  const handleCreateGrid = () => {
    const order: GridOrder = {
      id: Date.now().toString(),
      symbol: newOrder.symbol,
      basePrice: newOrder.basePrice,
      stepSize: newOrder.stepSize,
      maxLevels: newOrder.maxLevels,
      lotSize: newOrder.lotSize,
      isActive: true,
      currentLevel: 0,
      totalProfit: 0,
      levels: []
    };

    // Generate levels
    for (let i = 1; i <= newOrder.maxLevels; i++) {
      order.levels.push({
        level: i,
        price: newOrder.basePrice + (i * newOrder.stepSize),
        volume: newOrder.lotSize,
        isFilled: false,
        profit: 0
      });
      order.levels.push({
        level: -i,
        price: newOrder.basePrice - (i * newOrder.stepSize),
        volume: newOrder.lotSize,
        isFilled: false,
        profit: 0
      });
    }

    setGridOrders(prev => [...prev, order]);
    setShowCreateForm(false);
    setNewOrder({
      symbol: 'EURUSD',
      basePrice: 1.0820,
      stepSize: 0.0010,
      maxLevels: 10,
      lotSize: 0.01,
      useMartingale: false
    });
  };

  const toggleGrid = (id: string) => {
    setGridOrders(prev => prev.map(order =>
      order.id === id ? { ...order, isActive: !order.isActive } : order
    ));
  };

  const deleteGrid = (id: string) => {
    setGridOrders(prev => prev.filter(order => order.id !== id));
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-white">Grid Manager</h1>
        <Button
          onClick={() => setShowCreateForm(!showCreateForm)}
          className="bg-blue-600 hover:bg-blue-700"
        >
          <Settings className="h-4 w-4 mr-2" />
          Create Grid
        </Button>
      </div>

      {showCreateForm && (
        <Card className="bg-slate-800 border-slate-700">
          <CardHeader>
            <CardTitle className="text-white">Create New Grid</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="symbol" className="text-white">Symbol</Label>
                <Input
                  id="symbol"
                  value={newOrder.symbol}
                  onChange={(e) => setNewOrder(prev => ({ ...prev, symbol: e.target.value }))}
                  className="bg-slate-700 border-slate-600 text-white"
                />
              </div>
              <div>
                <Label htmlFor="basePrice" className="text-white">Base Price</Label>
                <Input
                  id="basePrice"
                  type="number"
                  step="0.0001"
                  value={newOrder.basePrice}
                  onChange={(e) => setNewOrder(prev => ({ ...prev, basePrice: parseFloat(e.target.value) }))}
                  className="bg-slate-700 border-slate-600 text-white"
                />
              </div>
              <div>
                <Label htmlFor="stepSize" className="text-white">Step Size</Label>
                <Input
                  id="stepSize"
                  type="number"
                  step="0.0001"
                  value={newOrder.stepSize}
                  onChange={(e) => setNewOrder(prev => ({ ...prev, stepSize: parseFloat(e.target.value) }))}
                  className="bg-slate-700 border-slate-600 text-white"
                />
              </div>
              <div>
                <Label htmlFor="maxLevels" className="text-white">Max Levels</Label>
                <Input
                  id="maxLevels"
                  type="number"
                  value={newOrder.maxLevels}
                  onChange={(e) => setNewOrder(prev => ({ ...prev, maxLevels: parseInt(e.target.value) }))}
                  className="bg-slate-700 border-slate-600 text-white"
                />
              </div>
              <div>
                <Label htmlFor="lotSize" className="text-white">Lot Size</Label>
                <Input
                  id="lotSize"
                  type="number"
                  step="0.01"
                  value={newOrder.lotSize}
                  onChange={(e) => setNewOrder(prev => ({ ...prev, lotSize: parseFloat(e.target.value) }))}
                  className="bg-slate-700 border-slate-600 text-white"
                />
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="martingale"
                  checked={newOrder.useMartingale}
                  onCheckedChange={(checked) => setNewOrder(prev => ({ ...prev, useMartingale: checked }))}
                />
                <Label htmlFor="martingale" className="text-white">Use Martingale</Label>
              </div>
            </div>
            <div className="flex space-x-2">
              <Button onClick={handleCreateGrid} className="bg-green-600 hover:bg-green-700">
                Create Grid
              </Button>
              <Button
                onClick={() => setShowCreateForm(false)}
                variant="outline"
                className="border-slate-600 text-white hover:bg-slate-700"
              >
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6">
        {gridOrders.map((order) => (
          <Card key={order.id} className="bg-slate-800 border-slate-700">
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="text-white flex items-center space-x-2">
                  <span>{order.symbol} Grid</span>
                  <Badge variant={order.isActive ? "default" : "secondary"}>
                    {order.isActive ? "Active" : "Paused"}
                  </Badge>
                </CardTitle>
                <div className="flex space-x-2">
                  <Button
                    onClick={() => toggleGrid(order.id)}
                    size="sm"
                    variant="outline"
                    className="border-slate-600 text-white hover:bg-slate-700"
                  >
                    {order.isActive ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                  </Button>
                  <Button
                    onClick={() => deleteGrid(order.id)}
                    size="sm"
                    variant="destructive"
                  >
                    Delete
                  </Button>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
                <div>
                  <p className="text-sm text-slate-400">Base Price</p>
                  <p className="text-lg font-semibold text-white">{order.basePrice.toFixed(4)}</p>
                </div>
                <div>
                  <p className="text-sm text-slate-400">Step Size</p>
                  <p className="text-lg font-semibold text-white">{order.stepSize.toFixed(4)}</p>
                </div>
                <div>
                  <p className="text-sm text-slate-400">Current Level</p>
                  <p className="text-lg font-semibold text-blue-500">{order.currentLevel}</p>
                </div>
                <div>
                  <p className="text-sm text-slate-400">Total Profit</p>
                  <p className={`text-lg font-semibold ${order.totalProfit >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                    ${order.totalProfit.toFixed(2)}
                  </p>
                </div>
              </div>

              <div>
                <h4 className="text-white font-semibold mb-2">Grid Levels</h4>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
                  {order.levels.slice(0, 6).map((level) => (
                    <div
                      key={level.level}
                      className={`p-2 rounded border ${
                        level.isFilled
                          ? 'bg-green-900 border-green-600'
                          : 'bg-slate-700 border-slate-600'
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <span className="text-white text-sm">Level {level.level}</span>
                        {level.isFilled ? (
                          <Badge variant="default" className="bg-green-600">Filled</Badge>
                        ) : (
                          <Badge variant="secondary">Pending</Badge>
                        )}
                      </div>
                      <p className="text-white font-mono text-xs">{level.price.toFixed(4)}</p>
                      <p className="text-slate-400 text-xs">Vol: {level.volume}</p>
                    </div>
                  ))}
                </div>
                {order.levels.length > 6 && (
                  <p className="text-slate-400 text-sm mt-2">
                    And {order.levels.length - 6} more levels...
                  </p>
                )}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
};

export default GridManager;