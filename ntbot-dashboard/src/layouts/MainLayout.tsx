import { Outlet, NavLink } from 'react-router-dom';
import { 
  LayoutDashboard, 
  TrendingUp, 
  Globe, 
  Signal, 
  Briefcase, 
  Settings as SettingsIcon,
  Activity,
  PieChart,
  Zap,
  Grid3X3,
  Target,
  Shield
} from 'lucide-react';
import { useTradingStore } from '../stores/trading.store';

export function MainLayout() {
  const isConnected = useTradingStore((state) => state.isConnected);

  const navItems = [
    { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
    { to: '/scalping', icon: Zap, label: 'Scalping' },
    { to: '/grid', icon: Grid3X3, label: 'Grid Manager' },
    { to: '/positions', icon: Target, label: 'Positions' },
    { to: '/risk', icon: Shield, label: 'Risk Mgmt' },
    { to: '/wyckoff', icon: TrendingUp, label: 'Wyckoff' },
    { to: '/macro', icon: Globe, label: 'Macro' },
    { to: '/quant', icon: Activity, label: 'Quant' },
    { to: '/profitchart', icon: PieChart, label: 'ProfitChart' },
    { to: '/signals', icon: Signal, label: 'Sinais' },
    { to: '/trades', icon: Briefcase, label: 'Trades' },
    { to: '/settings', icon: SettingsIcon, label: 'Config' },
  ];

  return (
    <div className="min-h-screen bg-slate-900">
      {/* Header */}
      <header className="bg-slate-800 border-b border-slate-700">
        <div className="px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-4">
              <h1 className="text-2xl font-bold text-white">
                NTBot <span className="text-primary-500">Dashboard</span>
              </h1>
              <div className="flex items-center space-x-2">
                <Activity className={`w-4 h-4 ${isConnected ? 'text-success animate-pulse' : 'text-slate-500'}`} />
                <span className="text-sm text-slate-400">
                  {isConnected ? 'Conectado' : 'Desconectado'}
                </span>
              </div>
            </div>
            
            <div className="flex items-center space-x-4">
              <select className="input text-sm py-1.5">
                <option>MNQ</option>
                <option>NQ</option>
                <option>ES</option>
              </select>
            </div>
          </div>
        </div>
      </header>

      <div className="flex">
        {/* Sidebar */}
        <aside className="w-64 bg-slate-800 border-r border-slate-700 min-h-[calc(100vh-73px)]">
          <nav className="p-4 space-y-2">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `flex items-center space-x-3 px-4 py-3 rounded-lg transition-colors ${
                    isActive
                      ? 'bg-primary-600 text-white'
                      : 'text-slate-300 hover:bg-slate-700'
                  }`
                }
              >
                <item.icon className="w-5 h-5" />
                <span className="font-medium">{item.label}</span>
              </NavLink>
            ))}
          </nav>
        </aside>

        {/* Main Content */}
        <main className="flex-1 p-6 overflow-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
