using MarketData.API.Worker;

namespace MarketData.API.Services
{
    public class CorrelationService : ICorrelationService
    {
        private readonly Queue<double> _win = new();
        private readonly Queue<double> _wdo = new();

        private const int Window = 100;

        public void Process(MarketEvent ev)
        {
            if (ev.Ticker.Contains("WIN"))
                Enqueue(_win, ev.Price);

            if (ev.Ticker.Contains("WDO"))
                Enqueue(_wdo, ev.Price);

            if (_win.Count == Window && _wdo.Count == Window)
            {
                var corr = Calculate(_win, _wdo);
                Console.WriteLine($"Corr WIN/WDO: {corr}");
            }
        }

        private void Enqueue(Queue<double> q, double value)
        {
            q.Enqueue(value);
            if (q.Count > Window)
                q.Dequeue();
        }

        private double Calculate(IEnumerable<double> x, IEnumerable<double> y)
        {
            var xs = x.ToArray();
            var ys = y.ToArray();

            var avgX = xs.Average();
            var avgY = ys.Average();

            double num = 0, denX = 0, denY = 0;

            for (int i = 0; i < xs.Length; i++)
            {
                num += (xs[i] - avgX) * (ys[i] - avgY);
                denX += Math.Pow(xs[i] - avgX, 2);
                denY += Math.Pow(ys[i] - avgY, 2);
            }

            return num / Math.Sqrt(denX * denY);
        }
    }
}
