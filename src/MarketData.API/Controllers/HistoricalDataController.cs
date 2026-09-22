using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marketdata.Database.Services;

namespace MarketData.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoricalDataController : ControllerBase
{
    private readonly HistoricalDataLoader _historicalDataLoader;

    public HistoricalDataController(HistoricalDataLoader historicalDataLoader)
    {
        _historicalDataLoader = historicalDataLoader;
    }

    /// <summary>
    /// Dispara a rotina de carga de dados históricos para o QuestDB sob demanda.
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> LoadHistoricalData(
        [FromBody] HistoricalLoadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tickers == null || request.Tickers.Count == 0)
        {
            return BadRequest("É necessário informar ao menos um Ticker.");
        }

        if (request.StartDate > request.EndDate)
        {
            return BadRequest("A data de início não pode ser maior que a data final.");
        }

        // Importante: Dispara de forma assíncrona para não prender a requisição HTTP
        // Para uma aplicação real de produção, avalie o uso de BackgroundJob (Hangfire/Quartz)
        _ = Task.Run(async () =>
        {
            try
            {
                await _historicalDataLoader.ExecuteLoadAsync(request.Tickers, request.StartDate, request.EndDate, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao executar carga histórica em background: {ex.Message}");
            }
        }, CancellationToken.None);

        return Accepted(new { Message = "Carga de dados históricos iniciada em background.", Request = request });
    }
}

public class HistoricalLoadRequest
{
    public List<string> Tickers { get; set; } = new List<string>();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
