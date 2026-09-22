using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orbital.Core.Models;

namespace Orbital.Core.Services;

public class TapeSpeedDbClient
{
    private readonly string _connectionString;
    private readonly ILogger<TapeSpeedDbClient> _logger;

    public TapeSpeedDbClient(string connectionString, ILogger<TapeSpeedDbClient> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TapeSpeedRawData> GetRawTapeSpeedDataAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var rawData = new TapeSpeedRawData();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT 
                    sum(case when trade_type = '2' and timestamp > dateadd('s', -5, now()) then quantity else 0 end) as compra_5s,
                    sum(case when trade_type = '3' and timestamp > dateadd('s', -5, now()) then quantity else 0 end) as venda_5s,
                    count(case when timestamp > dateadd('s', -5, now()) then 1 else null end) as negocios_5s,

                    sum(case when trade_type = '2' and timestamp > dateadd('s', -15, now()) then quantity else 0 end) as compra_15s,
                    sum(case when trade_type = '3' and timestamp > dateadd('s', -15, now()) then quantity else 0 end) as venda_15s,
                    count(case when timestamp > dateadd('s', -15, now()) then 1 else null end) as negocios_15s,

                    sum(case when trade_type = '2' then quantity else 0 end) as compra_60s,
                    sum(case when trade_type = '3' then quantity else 0 end) as venda_60s,
                    count() as negocios_60s,
                    
                    avg(case when timestamp > dateadd('s', -5, now()) then quantity else null end) as media_lotes_5s,
                    avg(quantity) as media_lotes_60s
                FROM trades 
                WHERE ticker = @ticker 
                  AND timestamp > dateadd('s', -60, now());";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("ticker", ticker);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                rawData.Compra5s = reader.IsDBNull(0) ? 0.0 : Convert.ToDouble(reader.GetValue(0));
                rawData.Venda5s = reader.IsDBNull(1) ? 0.0 : Convert.ToDouble(reader.GetValue(1));
                rawData.Negocios5s = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);

                rawData.Compra15s = reader.IsDBNull(3) ? 0.0 : Convert.ToDouble(reader.GetValue(3));
                rawData.Venda15s = reader.IsDBNull(4) ? 0.0 : Convert.ToDouble(reader.GetValue(4));
                rawData.Negocios15s = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);

                rawData.Compra60s = reader.IsDBNull(6) ? 0.0 : Convert.ToDouble(reader.GetValue(6));
                rawData.Venda60s = reader.IsDBNull(7) ? 0.0 : Convert.ToDouble(reader.GetValue(7));
                rawData.Negocios60s = reader.IsDBNull(8) ? 0 : reader.GetInt64(8);

                rawData.MediaLotes5s = reader.IsDBNull(9) ? 0.0 : Convert.ToDouble(reader.GetValue(9));
                rawData.MediaLotes60s = reader.IsDBNull(10) ? 0.0 : Convert.ToDouble(reader.GetValue(10));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar dados crus de Tape Speed no QuestDB para {Ticker}", ticker);
        }

        return rawData;
    }
}
