using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;

namespace MarketData.API.Controllers
{
    [ApiController]
    [Route("api/poi")]
    public class PointsOfInterestController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<PointsOfInterestController> _logger;

        public PointsOfInterestController(IConfiguration configuration, ILogger<PointsOfInterestController> logger)
        {
            // Busca da seção ConnectionStrings ou do fallback local de desenvolvimento
            _connectionString = configuration.GetConnectionString("QuestDB") 
                ?? "Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest;Server Compatibility Mode=NoTypeLoading;";
            _logger = logger;
        }

        public class PoiSyncRequest
        {
            public string Ticker { get; set; } = string.Empty;
            public double Abertura { get; set; }
            public double Maxima { get; set; }
            public double Minima { get; set; }
            public double Ajuste { get; set; }
            public double Fechamento { get; set; }
        }

        public class PoiResponse
        {
            public string Name { get; set; } = string.Empty;
            public double Price { get; set; }
            public double? EndPrice { get; set; }
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncPoints([FromBody] PoiSyncRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Ticker))
                return BadRequest("Ticker é obrigatório.");

            _logger.LogInformation("Recebido pedido de sincronização de POIs para {Ticker}", request.Ticker);

            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                // 1. Garantir que a tabela existe
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
                    await createCmd.ExecuteNonQueryAsync();
                }

                // 2. Inserir os 5 pontos de interesse
                var nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var points = new[]
                {
                    new { Name = "Abertura", Price = request.Abertura },
                    new { Name = "Máxima", Price = request.Maxima },
                    new { Name = "Mínima", Price = request.Minima },
                    new { Name = "Ajuste", Price = request.Ajuste },
                    new { Name = "Fechamento anterior", Price = request.Fechamento }
                };

                foreach (var p in points)
                {
                    if (p.Price <= 0) continue;

                    var insertSql = $@"
                        INSERT INTO points_of_interest (poi_id, created_at, ticker, type, side, description, price_start, price_end, is_active) 
                        VALUES (
                            '{Guid.NewGuid()}',
                            to_timestamp('{nowStr}', 'yyyy-MM-ddTHH:mm:ss.SSSZ'),
                            '{request.Ticker}',
                            'Reference',
                            'Neutra',
                            '{p.Name}',
                            {p.Price.ToString(CultureInfo.InvariantCulture)},
                            null,
                            true
                        );";

                    await using var insertCmd = new NpgsqlCommand(insertSql, connection);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // 3. Buscar os pontos atualizados utilizando LATEST BY (QuestDB temporal query)
                var querySql = $@"
                    SELECT description, price_start, price_end
                    FROM points_of_interest
                    WHERE ticker = '{request.Ticker}' AND is_active = true
                    LATEST BY description;";

                var resultList = new List<PoiResponse>();
                await using var selectCmd = new NpgsqlCommand(querySql, connection);
                await using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var desc = reader.GetString(0);
                    var priceStart = reader.GetDouble(1);
                    double? priceEnd = null;
                    if (!reader.IsDBNull(2))
                    {
                        var val = reader.GetDouble(2);
                        if (!double.IsNaN(val)) priceEnd = val;
                    }

                    resultList.Add(new PoiResponse
                    {
                        Name = desc,
                        Price = priceStart,
                        EndPrice = priceEnd
                    });
                }

                return Ok(resultList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar POIs para {Ticker}", request.Ticker);
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        public class CustomPoiSyncRequest
        {
            public List<CustomPoiItem> Points { get; set; } = new();
        }

        public class CustomPoiItem
        {
            public string Name { get; set; } = string.Empty;
            public double Price { get; set; }
            public double? EndPrice { get; set; }
            public bool IsActive { get; set; } = true;
        }

        [HttpPost("custom")]
        public async Task<IActionResult> SyncCustomPoints([FromBody] CustomPoiSyncRequest request)
        {
            _logger.LogInformation("Recebido pedido de sincronização de POIs customizados.");

            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var nowStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var ticker = "CUSTOM";

                foreach (var p in request.Points)
                {
                    if (p.Price <= 0 && p.IsActive) continue;

                    string priceStr = p.Price.ToString(CultureInfo.InvariantCulture);
                    string priceEndStr = p.EndPrice.HasValue ? p.EndPrice.Value.ToString(CultureInfo.InvariantCulture) : "null";
                    string activeStr = p.IsActive ? "true" : "false";

                    var insertSql = $@"
                        INSERT INTO points_of_interest (poi_id, created_at, ticker, type, side, description, price_start, price_end, is_active) 
                        VALUES (
                            '{Guid.NewGuid()}',
                            to_timestamp('{nowStr}', 'yyyy-MM-ddTHH:mm:ss.SSSZ'),
                            '{ticker}',
                            'Manual',
                            'Neutra',
                            '{p.Name}',
                            {priceStr},
                            {priceEndStr},
                            {activeStr}
                        );";

                    await using var insertCmd = new NpgsqlCommand(insertSql, connection);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                var querySql = $@"
                    SELECT description, price_start, price_end
                    FROM points_of_interest
                    WHERE ticker = '{ticker}' AND is_active = true
                    LATEST BY description;";

                var resultList = new List<PoiResponse>();
                await using var selectCmd = new NpgsqlCommand(querySql, connection);
                await using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var desc = reader.GetString(0);
                    var priceStart = reader.GetDouble(1);
                    double? priceEnd = null;
                    if (!reader.IsDBNull(2))
                    {
                        var val = reader.GetDouble(2);
                        if (!double.IsNaN(val)) priceEnd = val;
                    }

                    resultList.Add(new PoiResponse
                    {
                        Name = desc,
                        Price = priceStart,
                        EndPrice = priceEnd
                    });
                }

                return Ok(resultList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar POIs customizados");
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }
    }
}
