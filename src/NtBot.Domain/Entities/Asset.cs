namespace NtBot.Domain.Entities
{
    public class Asset
    {
        public string Symbol { get; set; }
        public double StopLossValue { get; set; }
        public double TakeProfitValue { get; set; }
        public int Operations { get; set; }
        public List<Position> Positions { get; set; } = new();
        public double Balance { get; set; }
    }
}
