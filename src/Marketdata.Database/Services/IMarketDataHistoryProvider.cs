using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marketdata.Database.Models;

namespace Marketdata.Database.Services;

public interface IMarketDataHistoryProvider
{
    Task<List<HistoricalTradeDto>> GetHistoricalTradesAsync(string ticker, DateTime date, CancellationToken cancellationToken = default);

    Task<List<HistoricalTradeDto>> GetHistoricalTradesAsync(
        string ticker,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
