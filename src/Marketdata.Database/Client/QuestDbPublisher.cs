using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Marketdata.Database.Configuration;
using Marketdata.Database.Models;
using Marketdata.Database.Constants;
using QuestDB;
using QuestDB.Senders;

namespace Marketdata.Database.Client;

public class QuestDbPublisher : IQuestDbClient, IAsyncDisposable
{
    private readonly QuestDbOptions _options;
    private readonly ILogger<QuestDbPublisher> _logger;
    private ISender? _sender;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public QuestDbPublisher(IOptions<QuestDbOptions> options, ILogger<QuestDbPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private async Task<ISender> GetOrCreateSenderAsync(CancellationToken cancellationToken)
    {
        if (_sender != null) return _sender;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_sender == null)
            {
                var address = $"tcp::addr={_options.Host}:{_options.IlpPort};";
                _logger.LogInformation("Conectando ao QuestDB em {Address}", address);
                _sender = Sender.New(address);
            }
            return _sender;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task PublishTradesAsync(IEnumerable<TradeRecord> trades, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var sender = await GetOrCreateSenderAsync(cancellationToken);

        foreach (var trade in trades)
        {
            await sender.Table(QuestDbDictionary.Tables.Trades)
                  .Symbol(QuestDbDictionary.TradesColumns.Ticker, trade.Ticker)
                  .Symbol(QuestDbDictionary.TradesColumns.Side, trade.Side)
                  .Symbol(QuestDbDictionary.TradesColumns.BuyAgent, trade.BuyAgent)
                  .Symbol(QuestDbDictionary.TradesColumns.SellAgent, trade.SellAgent)
                  .Column(QuestDbDictionary.TradesColumns.Price, trade.Price)
                  .Column(QuestDbDictionary.TradesColumns.Quantity, trade.Quantity)
                  .Column(QuestDbDictionary.TradesColumns.TradeType, trade.TradeType)
                  .Column(QuestDbDictionary.TradesColumns.TradeNumber, trade.TradeNumber)
                  .Column(QuestDbDictionary.TradesColumns.IsAuction, trade.IsAuction)
                  .AtAsync(trade.Timestamp);
        }

        await sender.SendAsync();
    }

    public async Task PublishAgentAsync(AgentRecord agent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var sender = await GetOrCreateSenderAsync(cancellationToken);
        
        await sender.Table(QuestDbDictionary.Tables.Agents)
              .Symbol(QuestDbDictionary.AgentsColumns.FullName, agent.AgentName)
              .Column(QuestDbDictionary.AgentsColumns.AgentId, agent.AgentId)
              .AtNowAsync();
              
        await sender.SendAsync();
    }

    public async Task PublishTradeTypeAsync(TradeTypeRecord tradeType, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var sender = await GetOrCreateSenderAsync(cancellationToken);
        
        await sender.Table(QuestDbDictionary.Tables.TradeTypes)
              .Symbol(QuestDbDictionary.TradeTypesColumns.Description, tradeType.Description)
              .Symbol(QuestDbDictionary.TradeTypesColumns.Comment, tradeType.Comment)
              .Column(QuestDbDictionary.TradeTypesColumns.TypeId, tradeType.TypeId)
              .AtNowAsync();
              
        await sender.SendAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender != null)
        {
            _sender.Dispose();
            _sender = null;
        }
        await Task.CompletedTask;
    }
}
