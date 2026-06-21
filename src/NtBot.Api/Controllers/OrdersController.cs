using Microsoft.AspNetCore.Mvc;
using NtBot.Api.Services.Macro;
using NtBot.Api.Strategies;
using NtBot.Domain.Entities;

namespace NtBot.Api.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IStrategyBase<Position> _strategy;
        private readonly IMacroOrderGate _macroGate;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IStrategyBase<Position> strategy,
            IMacroOrderGate macroGate,
            ILogger<OrdersController> logger)
        {
            _strategy = strategy;
            _macroGate = macroGate;
            _logger = logger;
        }

        [HttpGet("next")]
        public async Task<IActionResult> PostNextOrder(
            [FromQuery] string Symbol,
            double Bid,
            double Ask,
            string Time,
            [FromQuery] Guid? tenantId = null)
        {
            var position = await _strategy.Execute(Symbol, Bid, Ask, Time);

            if (tenantId is null || position.Action is not ("BUY" or "SELL"))
                return Ok(position);

            var direction = position.Action == "BUY" ? TradeDirection.LONG : TradeDirection.SHORT;
            var gate = await _macroGate.EvaluateAsync(tenantId.Value, Symbol, direction);

            if (gate.Allowed)
                return Ok(position);

            _logger.LogInformation(
                "CHoCH signal {Action} blocked for {Symbol}: {Reason}",
                position.Action,
                Symbol,
                gate.Reason);

            return Ok(new Position
            {
                Symbol = Symbol,
                Action = "CALC",
                Quantity = 0
            });
        }
    }
}
