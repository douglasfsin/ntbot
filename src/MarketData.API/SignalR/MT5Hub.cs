using Microsoft.AspNetCore.SignalR;

namespace MarketData.API.SignalR
{
    /// <summary>
    /// Hub SignalR para distribuição de dados de mercado vindos do MetaTrader 5.
    /// Os clientes se inscrevem em grupos por ticker para receber atualizações.
    /// </summary>
    public class MT5Hub : Hub
    {
        /// <summary>
        /// Subscreve o cliente a um grupo (ticker) para receber dados em tempo real.
        /// </summary>
        public async Task Subscribe(string ticker)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ticker);
        }

        /// <summary>
        /// Remove a subscrição do cliente a um grupo (ticker).
        /// </summary>
        public async Task Unsubscribe(string ticker)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ticker);
        }
    }
}
