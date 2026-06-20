import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Badge } from '../components/ui/badge';
import { Progress } from '../components/ui/progress';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { AlertTriangle, Shield, TrendingDown, DollarSign, Activity } from 'lucide-react';

interface RiskMetrics {
  dailyPnL: number;
  maxDrawdown: number;
  maxDrawdownPercent: number;
  totalExposure: number;
  openPositions: number;
  riskLimit: number;
  dailyLossLimit: number;
  correlationLimit: number;
}

interface RiskAlert {
  id: string;
  type: 'warning' | 'critical' | 'info';
  message: string;
  timestamp: Date;
  resolved: boolean;
}

const RiskManagement: React.FC = () => {
  const [riskMetrics] = useState<RiskMetrics>({
    dailyPnL: 1250.50,
    maxDrawdown: -320.00,
    maxDrawdownPercent: 2.1,
    totalExposure: 50000,
    openPositions: 8,
    riskLimit: 100000,
    dailyLossLimit: 2000,
    correlationLimit: 0.7
  });

  const [riskAlerts, setRiskAlerts] = useState<RiskAlert[]>([]);
  const [settings, setSettings] = useState({
    maxDrawdownLimit: 5.0,
    dailyLossLimit: 2000,
    maxOpenPositions: 10,
    maxExposurePerSymbol: 10000,
    autoStopTrading: true
  });

  useEffect(() => {
    // Mock risk alerts
    const mockAlerts: RiskAlert[] = [
      {
        id: '1',
        type: 'warning',
        message: 'Daily loss limit approaching (85% used)',
        timestamp: new Date(Date.now() - 300000),
        resolved: false
      },
      {
        id: '2',
        type: 'info',
        message: 'High correlation detected between EURUSD and GBPUSD (0.85)',
        timestamp: new Date(Date.now() - 600000),
        resolved: false
      },
      {
        id: '3',
        type: 'critical',
        message: 'Max drawdown limit exceeded on account #2',
        timestamp: new Date(Date.now() - 900000),
        resolved: true
      }
    ];
    setRiskAlerts(mockAlerts);
  }, []);

  const getRiskLevel = () => {
    const drawdownPercent = Math.abs(riskMetrics.maxDrawdownPercent);
    if (drawdownPercent > 5) return { level: 'critical', color: 'text-red-500', bg: 'bg-red-900' };
    if (drawdownPercent > 3) return { level: 'high', color: 'text-orange-500', bg: 'bg-orange-900' };
    if (drawdownPercent > 1) return { level: 'medium', color: 'text-yellow-500', bg: 'bg-yellow-900' };
    return { level: 'low', color: 'text-green-500', bg: 'bg-green-900' };
  };

  const riskLevel = getRiskLevel();

  const resolveAlert = (id: string) => {
    setRiskAlerts(prev => prev.map(alert =>
      alert.id === id ? { ...alert, resolved: true } : alert
    ));
  };

  const updateSettings = () => {
    console.log('Updating risk settings:', settings);
    // Implement settings update logic
  };

  const formatCurrency = (amount: number) => `$${amount.toLocaleString()}`;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold text-white">Risk Management</h1>
        <Badge className={`${riskLevel.bg} ${riskLevel.color}`}>
          Risk Level: {riskLevel.level.toUpperCase()}
        </Badge>
      </div>

      {/* Risk Overview */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Daily P&L</p>
                <p className={`text-2xl font-bold ${riskMetrics.dailyPnL >= 0 ? 'text-green-500' : 'text-red-500'}`}>
                  {formatCurrency(riskMetrics.dailyPnL)}
                </p>
              </div>
              <DollarSign className={`h-8 w-8 ${riskMetrics.dailyPnL >= 0 ? 'text-green-500' : 'text-red-500'}`} />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Max Drawdown</p>
                <p className="text-2xl font-bold text-red-500">
                  {riskMetrics.maxDrawdownPercent.toFixed(1)}%
                </p>
                <p className="text-sm text-slate-400">{formatCurrency(riskMetrics.maxDrawdown)}</p>
              </div>
              <TrendingDown className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Total Exposure</p>
                <p className="text-2xl font-bold text-blue-500">
                  {formatCurrency(riskMetrics.totalExposure)}
                </p>
                <p className="text-sm text-slate-400">
                  {((riskMetrics.totalExposure / riskMetrics.riskLimit) * 100).toFixed(1)}% of limit
                </p>
              </div>
              <Shield className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-slate-800 border-slate-700">
          <CardContent className="p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-slate-400">Open Positions</p>
                <p className="text-2xl font-bold text-white">{riskMetrics.openPositions}</p>
                <p className="text-sm text-slate-400">Max: 10</p>
              </div>
              <Activity className="h-8 w-8 text-white" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Risk Limits Progress */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <Card className="bg-slate-800 border-slate-700">
          <CardHeader>
            <CardTitle className="text-white">Risk Limits</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-slate-400">Daily Loss Limit</span>
                <span className="text-white">
                  {formatCurrency(Math.abs(riskMetrics.dailyPnL))} / {formatCurrency(riskMetrics.dailyLossLimit)}
                </span>
              </div>
              <Progress
                value={(Math.abs(riskMetrics.dailyPnL) / riskMetrics.dailyLossLimit) * 100}
                className="h-2"
              />
            </div>

            <div>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-slate-400">Exposure Limit</span>
                <span className="text-white">
                  {formatCurrency(riskMetrics.totalExposure)} / {formatCurrency(riskMetrics.riskLimit)}
                </span>
              </div>
              <Progress
                value={(riskMetrics.totalExposure / riskMetrics.riskLimit) * 100}
                className="h-2"
              />
            </div>

            <div>
              <div className="flex justify-between text-sm mb-2">
                <span className="text-slate-400">Drawdown Limit</span>
                <span className="text-white">
                  {riskMetrics.maxDrawdownPercent.toFixed(1)}% / {settings.maxDrawdownLimit}%
                </span>
              </div>
              <Progress
                value={(riskMetrics.maxDrawdownPercent / settings.maxDrawdownLimit) * 100}
                className="h-2"
              />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-slate-800 border-slate-700">
          <CardHeader>
            <CardTitle className="text-white">Risk Settings</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <Label htmlFor="maxDrawdown" className="text-white">Max Drawdown (%)</Label>
              <Input
                id="maxDrawdown"
                type="number"
                step="0.1"
                value={settings.maxDrawdownLimit}
                onChange={(e) => setSettings(prev => ({ ...prev, maxDrawdownLimit: parseFloat(e.target.value) }))}
                className="bg-slate-700 border-slate-600 text-white mt-1"
              />
            </div>

            <div>
              <Label htmlFor="dailyLoss" className="text-white">Daily Loss Limit ($)</Label>
              <Input
                id="dailyLoss"
                type="number"
                value={settings.dailyLossLimit}
                onChange={(e) => setSettings(prev => ({ ...prev, dailyLossLimit: parseInt(e.target.value) }))}
                className="bg-slate-700 border-slate-600 text-white mt-1"
              />
            </div>

            <div>
              <Label htmlFor="maxPositions" className="text-white">Max Open Positions</Label>
              <Input
                id="maxPositions"
                type="number"
                value={settings.maxOpenPositions}
                onChange={(e) => setSettings(prev => ({ ...prev, maxOpenPositions: parseInt(e.target.value) }))}
                className="bg-slate-700 border-slate-600 text-white mt-1"
              />
            </div>

            <Button onClick={updateSettings} className="w-full bg-blue-600 hover:bg-blue-700">
              Update Settings
            </Button>
          </CardContent>
        </Card>
      </div>

      {/* Risk Alerts */}
      <Card className="bg-slate-800 border-slate-700">
        <CardHeader>
          <CardTitle className="text-white flex items-center space-x-2">
            <AlertTriangle className="h-5 w-5 text-yellow-500" />
            <span>Risk Alerts</span>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {riskAlerts.map((alert) => (
              <div
                key={alert.id}
                className={`p-4 rounded-lg border ${
                  alert.type === 'critical'
                    ? 'bg-red-900 border-red-600'
                    : alert.type === 'warning'
                    ? 'bg-yellow-900 border-yellow-600'
                    : 'bg-blue-900 border-blue-600'
                } ${alert.resolved ? 'opacity-50' : ''}`}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center space-x-3">
                    <AlertTriangle className={`h-5 w-5 ${
                      alert.type === 'critical' ? 'text-red-500' :
                      alert.type === 'warning' ? 'text-yellow-500' : 'text-blue-500'
                    }`} />
                    <div>
                      <p className="text-white font-medium">{alert.message}</p>
                      <p className="text-slate-400 text-sm">
                        {alert.timestamp.toLocaleString()}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center space-x-2">
                    <Badge variant={alert.resolved ? "secondary" : "default"}>
                      {alert.resolved ? "Resolved" : alert.type.toUpperCase()}
                    </Badge>
                    {!alert.resolved && (
                      <Button
                        onClick={() => resolveAlert(alert.id)}
                        size="sm"
                        variant="outline"
                        className="border-slate-600 text-white hover:bg-slate-700"
                      >
                        Resolve
                      </Button>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default RiskManagement;