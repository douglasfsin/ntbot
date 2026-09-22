using Marketdata.Database.Models;

namespace Marketdata.Database.Client;

public interface IQuestDbClient
{
    Task PublishTradesAsync(IEnumerable<TradeRecord> trades, CancellationToken cancellationToken = default);
    Task PublishAgentAsync(AgentRecord agent, CancellationToken cancellationToken = default);
    Task PublishTradeTypeAsync(TradeTypeRecord tradeType, CancellationToken cancellationToken = default);
}
