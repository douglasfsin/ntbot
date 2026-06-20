namespace NtBot.Domain.Entities
{
    public class Position
    {
        public string Symbol { get; set; }
        public string Action { get; set; }
        public int Quantity { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public string OrderNumber { get; set; }
    }
}
