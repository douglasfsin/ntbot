using Microsoft.AspNetCore.SignalR;
using MarketData.API.Worker;

namespace MarketData.API.SignalR
{
    public class MarketHub : Hub
    {
        // Lazy breaks DI cycle: MarketHub → Worker → IHubContext<MarketHub>
        private readonly Lazy<MarketDataWorker> _worker;

        public MarketHub(Lazy<MarketDataWorker> worker)
        {
            _worker = worker;
        }

        public async Task Subscribe(string ticker, int niveis = 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ticker);

            if (niveis > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"{ticker}_{niveis}");
                _worker.Value.RegisterInterest(ticker, niveis);
            }
        }

        public async Task Unsubscribe(string ticker, int niveis = 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ticker);
            if (niveis > 0)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{ticker}_{niveis}");
            }
        }

        public double GetVolumeToPrice(string ticker, double targetPrice)
        {
            return _worker.Value.GetVolumeToPrice(ticker, targetPrice);
        }

        public long TestSellBook(string ticker, double targetPrice, int niveis)
        {
            return _worker.Value.TestSellBook(ticker, targetPrice, niveis);
        }

        public long GetTotalBookVolume(string ticker, int niveis)
        {
            return _worker.Value.GetTotalBookVolume(ticker, niveis);
        }

        public long TestBuyBook(string ticker, double targetPrice, int niveis)
        {
            return _worker.Value.TestBuyBook(ticker, targetPrice, niveis);
        }
    }
}
