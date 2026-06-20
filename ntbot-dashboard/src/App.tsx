import { useEffect, lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { MainLayout } from './layouts/MainLayout';
import { Dashboard } from './pages/Dashboard';
import { WyckoffAnalysis } from './pages/WyckoffAnalysis';
import { MacroAnalysis } from './pages/MacroAnalysis';
import { Signals } from './pages/Signals';
import { Trades } from './pages/Trades';
import { Settings } from './pages/Settings';
import { ProfitChartPage } from './pages/ProfitChart';
import ScalpingPanel from './pages/ScalpingPanel';
import GridManager from './pages/GridManager';
import Positions from './pages/Positions';
import RiskManagement from './pages/RiskManagement';
import { useTradingStore } from './stores/trading.store';

// Lazy load Quant Strategy to prevent blocking app startup
const QuantStrategyPage = lazy(() => import('./pages/QuantStrategy').then(module => ({ default: module.QuantStrategyPage })));

function App() {
  console.log('🚀 App component rendering...');
  const setConnected = useTradingStore((state) => state.setConnected);

  useEffect(() => {
    console.log('✅ App useEffect running...');
    
    // SignalR disabled - WebSocket real-time updates not implemented yet
    // Uncomment when SignalR hub is implemented on backend
    /*
    signalRService.connect().catch((error) => {
      console.warn('SignalR connection failed (optional):', error);
    });

    const unsubscribe = signalRService.onConnectionChange((connected) => {
      setConnected(connected);
    });

    return () => {
      unsubscribe();
      signalRService.disconnect();
    };
    */
  }, [setConnected]);

  return (
    <BrowserRouter>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            background: '#1e293b',
            color: '#fff',
            border: '1px solid #334155',
          },
          success: {
            iconTheme: {
              primary: '#10b981',
              secondary: '#fff',
            },
          },
          error: {
            iconTheme: {
              primary: '#ef4444',
              secondary: '#fff',
            },
          },
        }}
      />
      
      <Routes>
        <Route path="/" element={<MainLayout />}>
          <Route index element={<Dashboard />} />
          <Route path="wyckoff" element={<WyckoffAnalysis />} />
          <Route path="macro" element={<MacroAnalysis />} />
          <Route path="quant" element={
            <Suspense fallback={<div className="flex items-center justify-center h-screen">Carregando...</div>}>
              <QuantStrategyPage />
            </Suspense>
          } />
          <Route path="profitchart" element={<ProfitChartPage />} />
          <Route path="scalping" element={<ScalpingPanel />} />
          <Route path="grid" element={<GridManager />} />
          <Route path="positions" element={<Positions />} />
          <Route path="risk" element={<RiskManagement />} />
          <Route path="signals" element={<Signals />} />
          <Route path="trades" element={<Trades />} />
          <Route path="settings" element={<Settings />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;

