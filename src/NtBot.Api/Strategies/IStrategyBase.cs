using NtBot.Domain.Entities;

namespace NtBot.Api.Strategies
{
    public interface IStrategyBase<T> where T : class
    {
        Task<T> Execute(string Symbol, double Bid, double Ask, string Time);
        Task<T> AssetManager(string Symbol, string OrderNumber, double StopLossValue, double TakeProfitValue, Position Position);
    }
}
