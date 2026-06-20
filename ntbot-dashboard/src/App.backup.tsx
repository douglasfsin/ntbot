import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { MainLayout } from './layouts/MainLayout';
import { Dashboard } from './pages/Dashboard';
import { WyckoffAnalysis } from './pages/WyckoffAnalysis';
import { MacroAnalysis } from './pages/MacroAnalysis';
import { Signals } from './pages/Signals';
import { Trades } from './pages/Trades';
import { Settings } from './pages/Settings';
import { signalRService } from './services/signalr.service';
import { useTradingStore } from './stores/trading.store';

function App() {
  console.log('🚀 App component rendering...');
  const setConnected = useTradingStore((state) => state.setConnected);

  useEffect(() => {
    console.log('✅ App useEffect running...');
    // Connect to SignalR
    signalRService.connect();

    // Listen for connection changes
    const unsubscribe = signalRService.onConnectionChange((connected) => {
      setConnected(connected);
    });

    return () => {
      unsubscribe();
      signalRService.disconnect();
    };
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
