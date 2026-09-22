using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Marketdata.Database.Models;

namespace Orbital.Core.Services;

public class SMCQuestDbClient
{
    private string _connectionString;
    private readonly ILogger<SMCQuestDbClient> _logger;

    public void UpdateConnectionString(string newConnectionString)
    {
        _connectionString = newConnectionString ?? throw new ArgumentNullException(nameof(newConnectionString));
        _logger.LogInformation("QuestDB Connection String atualizada dinamicamente.");
    }

    public SMCQuestDbClient(string connectionString, ILogger<SMCQuestDbClient> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public record OrderBlockCandidate(double Price, double VolCompra, double VolVenda, int TotalTicks);
    public record VolumeHeatmapPoint(double Price, double Volume);

    public async Task<List<OrderBlockCandidate>> DetectOrderBlocksAsync(string ticker, int secondsWindow, int minTicks, int minVolumeThreshold, CancellationToken cancellationToken = default)
    {
        var candidates = new List<OrderBlockCandidate>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // A coluna padrao de tempo no questdb geralmente é o designated timestamp da tabela.
            // Para "trades", comumente usam 'timestamp'.
            var sql = $@"
                SELECT 
                    price,
                    sum(case when trade_type = '2' then quantity else 0 end) as vol_compra,
                    sum(case when trade_type = '3' then quantity else 0 end) as vol_venda,
                    count() as total_ticks
                FROM trades
                WHERE ticker = @ticker 
                  AND timestamp > dateadd('s', -@window, now())
                GROUP BY price
                HAVING total_ticks > @minTicks 
                   AND (vol_compra > @minVol OR vol_venda > @minVol);";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("ticker", ticker);
            cmd.Parameters.AddWithValue("window", secondsWindow);
            cmd.Parameters.AddWithValue("minTicks", minTicks);
            cmd.Parameters.AddWithValue("minVol", minVolumeThreshold);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add(new OrderBlockCandidate(
                    reader.GetDouble(0),
                    reader.GetDouble(1),
                    reader.GetDouble(2),
                    reader.GetInt32(3)
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao detectar Order Blocks no QuestDB para o ativo {Ticker}", ticker);
        }

        return candidates;
    }

    public async Task SaveSMCPointAsync(PointOfInterestRecord poi, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var insertSql = $@"
                INSERT INTO points_of_interest (poi_id, created_at, ticker, type, side, description, price_start, price_end, is_active) 
                VALUES (
                    '{poi.PoiId}',
                    to_timestamp('{poi.CreatedAt:yyyy-MM-ddTHH:mm:ss.fffZ}', 'yyyy-MM-ddTHH:mm:ss.SSSZ'),
                    '{poi.Ticker}',
                    '{poi.Type}',
                    '{poi.Side}',
                    '{poi.Description}',
                    {poi.PriceStart.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                    {(poi.PriceEnd.HasValue ? poi.PriceEnd.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")},
                    {poi.IsActive.ToString().ToLower()}
                );";
                
            await using var insertCmd = new NpgsqlCommand(insertSql, connection);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            
            _logger.LogInformation("Novo estudo SMC salvo no DB: {Desc} em {PriceStart}", poi.Description, poi.PriceStart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar marcação SMC no banco de dados.");
        }
    }

    public async Task<TrendQueryOutput?> GetMacroTrendDataAsync(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                WITH vwap_calc AS (
                    SELECT 
                        sum(price * quantity) / sum(quantity) as vwap_diaria,
                        sum(case when trade_type = '2' then quantity else 0 end) as total_vol_compra,
                        sum(case when trade_type = '3' then quantity else 0 end) as total_vol_venda
                    FROM trades
                    WHERE ticker = @ticker 
                      AND timestamp >= date_trunc('day', now())
                ),
                ultimo_preco AS (
                    SELECT price as preco_atual 
                    FROM trades 
                    WHERE ticker = @ticker 
                    ORDER BY timestamp DESC 
                    LIMIT 1
                )
                SELECT 
                    p.preco_atual,
                    v.vwap_diaria,
                    v.total_vol_compra,
                    v.total_vol_venda,
                    (v.total_vol_compra - v.total_vol_venda) as delta_acumulado_dia,
                    case when (v.total_vol_compra + v.total_vol_venda) > 0 
                         then (v.total_vol_compra / (v.total_vol_compra + v.total_vol_venda)) * 100 
                         else 50.0 end as percentual_comprador
                FROM vwap_calc v, ultimo_preco p;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("ticker", ticker);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new TrendQueryOutput
                {
                    PrecoAtual = reader.IsDBNull(0) ? 0 : reader.GetDouble(0),
                    VwapDiaria = reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                    TotalVolCompra = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    TotalVolVenda = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                    DeltaAcumuladoDia = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                    PercentualComprador = reader.IsDBNull(5) ? 50.0 : reader.GetDouble(5)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dados de Macro Tendência no QuestDB para {Ticker}", ticker);
        }

        return null;
    }

    public async Task<double> GetAjusteAnteriorAsync(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT price_start 
                FROM points_of_interest 
                WHERE ticker = @ticker 
                  AND (description ILIKE '%ajuste%' OR type = 'AJUSTE')
                  AND is_active = true 
                ORDER BY created_at DESC 
                LIMIT 1;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("ticker", ticker);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToDouble(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter Ajuste Anterior no QuestDB para {Ticker}", ticker);
        }
        return 0;
    }

    public async Task<List<VolumeHeatmapPoint>> GetVolumeProfileDataAsync(string ticker, DateTime start, DateTime end, double stepSize = 0.5, CancellationToken cancellationToken = default)
    {
        var points = new List<VolumeHeatmapPoint>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Grouping by price bucket. Example: ROUND(price / 0.5) * 0.5
            var sql = $@"
                SELECT 
                    round(price / @stepSize) * @stepSize as bucket_price,
                    sum(quantity) as total_volume
                FROM trades
                WHERE ticker = @ticker 
                  AND timestamp BETWEEN @start AND @end
                GROUP BY bucket_price
                ORDER BY bucket_price;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("ticker", ticker);
            cmd.Parameters.AddWithValue("start", start);
            cmd.Parameters.AddWithValue("end", end);
            cmd.Parameters.AddWithValue("stepSize", stepSize);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                points.Add(new VolumeHeatmapPoint(
                    reader.IsDBNull(0) ? 0 : reader.GetDouble(0),
                    reader.IsDBNull(1) ? 0 : reader.GetDouble(1)
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dados do Volume Profile no QuestDB para {Ticker}", ticker);
        }

        return points;
    }
}
