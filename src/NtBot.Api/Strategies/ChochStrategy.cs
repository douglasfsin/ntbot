using NtBot.Domain.Entities;
using System.Collections.Concurrent;

namespace NtBot.Api.Strategies
{
    public class ChochStrategy : IStrategyBase<Position>
    {
        private static readonly ConcurrentDictionary<string, List<(DateTime Time, double Bid, double Ask)>> AssetData = new();
        private static readonly ConcurrentDictionary<string, List<Asset>> AssetManagerData = new();

        public async Task<Position> AssetManager(string Symbol, string OrderNumber, double StopLossValue, double TakeProfitValue, Position Position)
        {
            var data = AssetManagerData.GetOrAdd(Symbol, _ => new List<Asset>());
            lock (data)
            {
                // Busca ou cria o asset
                var asset = data.FirstOrDefault(x => x.Symbol == Symbol);
                if (asset == null)
                {
                    asset = new Asset
                    {
                        Symbol = Symbol,
                        StopLossValue = StopLossValue,
                        TakeProfitValue = TakeProfitValue,
                        Operations = 0,
                        Positions = new List<Position>(),
                        // Supondo que você queira adicionar um campo de saldo:
                        Balance = 0
                    };
                    data.Add(asset);
                }

                // Atualiza valores de SL/TP
                asset.StopLossValue = StopLossValue;
                asset.TakeProfitValue = TakeProfitValue;

                // Atualiza ou adiciona a posição
                var pos = asset.Positions.FirstOrDefault(p => p.OrderNumber == OrderNumber);
                if (pos == null)
                {
                    asset.Positions.Add(Position);
                }
                else
                {
                    // Atualiza a posição existente
                    pos.StopLoss = Position.StopLoss;
                    pos.TakeProfit = Position.TakeProfit;
                    pos.Quantity = Position.Quantity;
                    pos.Action = Position.Action;
                }

                // Atualiza saldo (exemplo: supondo que Position tenha um campo Profit)
                if (Position.Action == "CLOSE" && Position.Quantity > 0)
                {
                    // Exemplo: atualiza saldo ao fechar posição
                    asset.Balance += (Position.TakeProfit - Position.StopLoss) * Position.Quantity;
                }

                // Gerenciamento de proteção de lucro
                if (pos != null && pos.Action == "OPEN")
                {
                    double entryPrice = pos.StopLoss; // Supondo que StopLoss seja o preço de entrada
                    double currentPrice = Position.TakeProfit; // Supondo que TakeProfit seja o preço atual
                    double profit = currentPrice - entryPrice;
                    double profitPercent = profit / entryPrice;

                    if (profitPercent >= 0.3)
                    {
                        // Protege 30% do lucro
                        pos.StopLoss = entryPrice + profit * 0.3;

                        // Melhora o percentual conforme o progresso
                        if (profitPercent >= 0.5)
                            pos.StopLoss = entryPrice + profit * 0.5;
                        if (profitPercent >= 0.7)
                            pos.StopLoss = entryPrice + profit * 0.7;
                        if (profitPercent >= 1.0)
                            pos.StopLoss = entryPrice + profit * 0.9;
                    }
                    Position = pos;
                }
            }

            return Position;
        }

        public async Task<Position> Execute(string Symbol, double Bid, double Ask, string Time)
        {
            if (!DateTime.TryParse(Time, out var dateTime))
                return new Position() { Symbol = Symbol, Action = "CALC" }; // Retorna posição padrão em vez de default

            // Armazena os dados recebidos
            var data = AssetData.GetOrAdd(Symbol, _ => new List<(DateTime, double, double)>());
            lock (data)
            {
                data.Add((dateTime, Bid, Ask));
                data.RemoveAll(x => x.Time < dateTime.AddHours(-8));
            }

            // Estratégia simplificada CHoCH (exemplo didático)
            // 1. Busca candles de 5min e 60min
            var candles1m = GetCandles(data, dateTime, TimeSpan.FromMinutes(1));
            var candles5m = GetCandles(data, dateTime, TimeSpan.FromMinutes(5));
            var candles15m = GetCandles(data, dateTime, TimeSpan.FromMinutes(15));
            var candles60m = GetCandles(data, dateTime, TimeSpan.FromMinutes(60));


            // 2. Lógica de CHoCH (exemplo: se o último candle de 5min fechou acima do anterior, sinal de compra)
            string action = "CALC";
            int quantity = 0;
            double stopLoss = 0, takeProfit = 0;

            if (candles1m.Count >= 2)
            {
                var t1BuyOk = candles1m[^1] != null && candles1m[^2] != null && candles1m[^1].Close > candles1m[^2].Close;
                var t5BuyOk = candles1m[^1] != null && candles5m.Count >= 1 && candles1m[^1].Close > candles5m[^1].Close;
                var t15BuyOk = candles15m[^1] != null && candles60m.Count >= 1 && candles15m[^1].Close > candles60m[^1].Close;

                var t1SellOk = candles1m[^1] != null && candles1m[^2] != null && candles1m[^1].Close < candles1m[^2].Close;
                var t5SellOk = candles1m[^1] != null && candles5m.Count >= 1 && candles1m[^1].Close < candles5m[^1].Close;
                var t15SellOk = candles15m[^1] != null && candles60m.Count >= 1 && candles15m[^1].Close < candles60m[^1].Close;

                if (/*(!t1SellOk && !t5SellOk) && */t1BuyOk && t5BuyOk)
                    Console.WriteLine($"{DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")} Symbol: {Symbol} Tendency UP ");

                if (/*(!t1BuyOk && !t5BuyOk) && */t1SellOk && t5SellOk)
                    Console.WriteLine($"{DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")} Symbol: {Symbol} Tendency DOWN ");

                var prev = candles1m[^2];
                var last = candles1m[^1];

                if ((!t1SellOk && !t5SellOk) && t1BuyOk && t15BuyOk && last.Close > prev.Close)
                {
                    action = "BUY";
                    quantity = 1;
                    stopLoss = (double)(last.Close - 500); // Exemplo: 10 pontos abaixo
                    takeProfit = (double)(last.Close + 30000); // Exemplo: 20 pontos acima
                }
                else if ((!t1BuyOk && !t5BuyOk) && t1SellOk && t15SellOk && last.Close < prev.Close)
                {
                    action = "SELL";
                    quantity = 1;
                    stopLoss = (double)(last.Close + 500);
                    takeProfit = (double)(last.Close - 30000);
                }
            }

            // 3. Se não houver sinal, retorna CALC
            if (action == "CALC")
                return new Position() { Symbol = Symbol, Action = "CALC" };

            var position = new Position()
            {
                Symbol = Symbol,
                Action = action,
                Quantity = quantity,
                StopLoss = stopLoss,
                TakeProfit = takeProfit
            };

            Console.WriteLine($"{DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")} Symbol: {position.Symbol} Action: {position.Action} Qnt: {position.Quantity} StopLoss: {position.StopLoss} TakeProfit: {position.TakeProfit}");

            return position;
        }

        // Função auxiliar para gerar candles
        public List<Candle> GetCandles(List<(DateTime Time, double Bid, double Ask)> data, DateTime now, TimeSpan timeframe)
        {
            var candles = new List<Candle>();
            var grouped = data
                .Where(x => x.Time >= now - TimeSpan.FromHours(2))
                .GroupBy(x => new DateTime((x.Time.Ticks / timeframe.Ticks) * timeframe.Ticks))
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var prices = group.Select(x => (x.Bid + x.Ask) / 2).ToList();
                candles.Add(new Candle
                {
                    Open = (decimal)prices.First(),
                    Close = (decimal)prices.Last(),
                    High = (decimal)prices.Max(),
                    Low = (decimal)prices.Min(),
                    Time = group.Key
                });
            }
            return candles;
        }
    }
}
