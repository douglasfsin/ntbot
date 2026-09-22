using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Marketdata.Database.Models;
using Marketdata.Database.Configuration;

namespace Marketdata.Database.Services;

public class HistoricalDataLoader
{
    private readonly string _connectionString;
    private readonly ILogger<HistoricalDataLoader> _logger;
    private readonly IMarketDataHistoryProvider _historyProvider;

    public HistoricalDataLoader(IOptions<QuestDbOptions> options, ILogger<HistoricalDataLoader> logger, IMarketDataHistoryProvider historyProvider)
    {
        _connectionString = Environment.GetEnvironmentVariable("QUESTDB_CONNECTION_STRING")
                            ?? $"Host={options.Value.Host};Port=8812;Database=qdb;Username=admin;Password=quest;Server Compatibility Mode=NoTypeLoading;";
        _logger = logger;
        _historyProvider = historyProvider;
    }

    public async Task ExecuteLoadAsync(List<string> tickers, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando rotina de carga histórica de trades de {StartDate} até {EndDate}", startDate.ToShortDateString(), endDate.ToShortDateString());

        DateTime currentDay = startDate.Date;
        DateTime finalDay = endDate.Date;

        while (currentDay <= finalDay)
        {
            if (cancellationToken.IsCancellationRequested) break;

            _logger.LogInformation("🟩 Processando o dia: {CurrentDay}", currentDay.ToShortDateString());
            
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // QuestDB não suporta DELETE FROM tradicional com ANDs complexos em versões estáveis.
            // A forma idiomática de limpar um dia é dropar a partição inteira.
            try 
            {
                var dropSql = $"ALTER TABLE trades_history DROP PARTITION LIST '{currentDay:yyyy-MM-dd}';";
                using var dropCmd = new NpgsqlCommand(dropSql, connection);
                await dropCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Partição do dia {Day} limpa com sucesso.", currentDay.ToShortDateString());
            } 
            catch (Exception ex) 
            {
                _logger.LogDebug("A partição do dia {Day} ainda não existia ou não pôde ser limpa: {Msg}", currentDay.ToShortDateString(), ex.Message);
            }

            foreach (var ticker in tickers)
            {
                var upperTicker = ticker.ToUpper();
                _logger.LogInformation("Buscando dados de histórico para {Ticker} na Nelogica...", upperTicker);

                // Requisita da ProfitDLL via ponte injetada
                List<HistoricalTradeDto> historicalTrades = await _historyProvider.GetHistoricalTradesAsync(upperTicker, currentDay);

                if (historicalTrades == null || historicalTrades.Count == 0)
                {
                    _logger.LogWarning("Nenhum dado encontrado para {Ticker} no dia {Day}", upperTicker, currentDay.ToShortDateString());
                    continue;
                }

                await ProcessDayPartitionAsync(connection, upperTicker, currentDay, historicalTrades);
            }

            currentDay = currentDay.AddDays(1);
        }

        _logger.LogInformation("🏁 Rotina de carga histórica finalizada com sucesso.");
    }

    private async Task ProcessDayPartitionAsync(NpgsqlConnection connection, string ticker, DateTime date, List<HistoricalTradeDto> trades)
    {
        try
        {
            _logger.LogInformation("Inserindo {Count} novos registros para {Ticker}...", trades.Count, ticker);
            
            var insertSql = @"
                INSERT INTO trades_history 
                (ticker, side, buy_agent, sell_agent, price, quantity, trade_type, trade_number, is_auction, timestamp) 
                VALUES 
                (@ticker, @side, @buy_agent, @sell_agent, @price, @quantity, @trade_type, @trade_number, @is_auction, @timestamp);";

            // QuestDB via Npgsql NÃO suporta transações explícitas (BEGIN/COMMIT). 
            // Fazemos o loop parametrizado diretamente.
            using (var insertCmd = new NpgsqlCommand(insertSql, connection))
            {
                insertCmd.Parameters.Add("@ticker", NpgsqlTypes.NpgsqlDbType.Varchar);
                insertCmd.Parameters.Add("@side", NpgsqlTypes.NpgsqlDbType.Varchar);
                insertCmd.Parameters.Add("@buy_agent", NpgsqlTypes.NpgsqlDbType.Varchar);
                insertCmd.Parameters.Add("@sell_agent", NpgsqlTypes.NpgsqlDbType.Varchar);
                insertCmd.Parameters.Add("@price", NpgsqlTypes.NpgsqlDbType.Double);
                insertCmd.Parameters.Add("@quantity", NpgsqlTypes.NpgsqlDbType.Bigint);
                insertCmd.Parameters.Add("@trade_type", NpgsqlTypes.NpgsqlDbType.Bigint);
                insertCmd.Parameters.Add("@trade_number", NpgsqlTypes.NpgsqlDbType.Bigint);
                insertCmd.Parameters.Add("@is_auction", NpgsqlTypes.NpgsqlDbType.Boolean);
                insertCmd.Parameters.Add("@timestamp", NpgsqlTypes.NpgsqlDbType.TimestampTz);

                foreach (var trade in trades)
                {
                    insertCmd.Parameters["@ticker"].Value = trade.Ticker;
                    insertCmd.Parameters["@side"].Value = trade.Side ?? "";
                    insertCmd.Parameters["@buy_agent"].Value = trade.BuyAgent ?? "";
                    insertCmd.Parameters["@sell_agent"].Value = trade.SellAgent ?? "";
                    insertCmd.Parameters["@price"].Value = trade.Price;
                    insertCmd.Parameters["@quantity"].Value = trade.Quantity;
                    insertCmd.Parameters["@trade_type"].Value = trade.TradeType;
                    insertCmd.Parameters["@trade_number"].Value = trade.TradeNumber;
                    insertCmd.Parameters["@is_auction"].Value = trade.IsAuction;
                    insertCmd.Parameters["@timestamp"].Value = trade.Timestamp;

                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            _logger.LogInformation("Dia {Date} finalizado para {Ticker}.", date.ToShortDateString(), ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal ao processar inserção do dia {Date} para o ativo {Ticker}.", date.ToShortDateString(), ticker);
            throw;
        }
    }
}
