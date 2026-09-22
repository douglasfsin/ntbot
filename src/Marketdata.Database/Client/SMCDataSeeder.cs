using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Marketdata.Database.Models;

namespace Marketdata.Database.Client;

public class SMCDataSeeder
{
    private readonly ILogger<SMCDataSeeder> _logger;

    public SMCDataSeeder(ILogger<SMCDataSeeder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedPointsOfInterestAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        // Pontos iniciais simulados para validação
        var points = new List<PointOfInterestRecord>
        {
            new() { 
                PoiId = Guid.NewGuid(), 
                CreatedAt = DateTime.UtcNow, 
                Ticker = "WDOFUT", 
                Type = "Reference", 
                Side = "Neutra", 
                Description = "Ajuste", 
                PriceStart = 5001.00, 
                PriceEnd = null, 
                IsActive = true 
            },
            new() { 
                PoiId = Guid.NewGuid(), 
                CreatedAt = DateTime.UtcNow, 
                Ticker = "WDOFUT", 
                Type = "OrderBlock", 
                Side = "Compra", 
                Description = "OB M15", 
                PriceStart = 4950.00, 
                PriceEnd = 4945.00, 
                IsActive = true 
            }
        };

        _logger.LogInformation("Iniciando processo de Seed para points_of_interest...");

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var point in points)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Cria a tabela se nao existir
                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS points_of_interest (
                        poi_id UUID,
                        created_at TIMESTAMP,
                        ticker SYMBOL,
                        type SYMBOL,
                        side SYMBOL,
                        description STRING,
                        price_start DOUBLE,
                        price_end DOUBLE,
                        is_active BOOLEAN
                    ) timestamp(created_at);";
                await using (var createCmd = new NpgsqlCommand(createTableSql, connection))
                {
                    await createCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Insert Append-Only (QuestDB via PGWire nao lida bem com parametros Npgsql fortemente tipados)
                var insertSql = $@"
                    INSERT INTO points_of_interest (poi_id, created_at, ticker, type, side, description, price_start, price_end, is_active) 
                    VALUES (
                        '{point.PoiId}',
                        to_timestamp('{point.CreatedAt:yyyy-MM-ddTHH:mm:ss.fffZ}', 'yyyy-MM-ddTHH:mm:ss.SSSZ'),
                        '{point.Ticker}',
                        '{point.Type}',
                        '{point.Side}',
                        '{point.Description}',
                        {point.PriceStart.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                        {(point.PriceEnd.HasValue ? point.PriceEnd.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")},
                        {point.IsActive.ToString().ToLower()}
                    );";
                    
                await using var insertCmd = new NpgsqlCommand(insertSql, connection);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Ponto de interesse {Description} (Ticker: {Ticker}) inserido com sucesso no QuestDB.", point.Description, point.Ticker);
            }

            _logger.LogInformation("Processo de Seed finalizado. Total de pontos processados: {Total}", points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a execução do Seed de points_of_interest.");
            throw;
        }
    }
}
