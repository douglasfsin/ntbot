namespace MarketData.API.Worker
{
    public interface IMarketDataProvider
    {
        Task StartAsync(CancellationToken ct);
        Task StopAsync(CancellationToken ct);
        Task StartSimulationAsync(string date, CancellationToken ct);
        void RegisterInterest(string ticker, int niveis);
    }
}
