using Microsoft.AspNetCore.Mvc;
using NtBot.Domain.Entities;
using NtBot.Api.Strategies;

namespace NtBot.Api.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IStrategyBase<Position> _strategy;
        public OrdersController(IStrategyBase<Position> strategy)
        {
            _strategy = strategy;
        }

        [HttpGet("next")]
        public async Task<IActionResult> PostNextOrder([FromQuery] string Symbol, double Bid, double Ask, string Time)
        {
            var position = await _strategy.Execute(Symbol, Bid, Ask, Time);
            return Ok(position);
        }

        //[HttpGet("manager")]
        //public async Task<IActionResult> AssetManager([FromQuery] string Symbol, string OrderNumber,  double StopLossValue, double TakeProfitValue)
        //{
        //    var position = await _strategy.AssetManager(Symbol, OrderNumber, StopLossValue, TakeProfitValue);
        //    return Ok(position);
        //}

        public string Symbol { get; set; }
        public double StopLossValue { get; set; }
        public double TakeProfitValue { get; set; }
        public int Operations { get; set; }
    }
}