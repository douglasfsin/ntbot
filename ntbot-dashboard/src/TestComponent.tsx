export function TestComponent() {
  return (
    <div style={{ 
      backgroundColor: '#1e293b', 
      color: 'white', 
      padding: '20px',
      minHeight: '100vh'
    }}>
      <h1>🎯 NTBot Dashboard - Test Page</h1>
      <p>Se você está vendo isto, o React está funcionando!</p>
      <div style={{ marginTop: '20px', padding: '10px', backgroundColor: '#334155', borderRadius: '8px' }}>
        <h2>✅ Status:</h2>
        <ul>
          <li>React: OK</li>
          <li>TypeScript: OK</li>
          <li>Vite: OK</li>
        </ul>
      </div>
    </div>
  );
}
