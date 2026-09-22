namespace Orbital.Core.Services
{
    public class TrendQueryOutput
    {
        public double PrecoAtual { get; set; }
        public double VwapDiaria { get; set; }
        public double TotalVolCompra { get; set; }
        public double TotalVolVenda { get; set; }
        public double DeltaAcumuladoDia { get; set; }
        public double PercentualComprador { get; set; }
    }

    public class TrendResult
    {
        public int Score { get; set; }
        public string State { get; set; } = string.Empty;
        public double Vwap { get; set; }
    }

    public class TrendAnalyzer
    {
        public TrendResult EvaluateMacroTrend(TrendQueryOutput data, double precoAjusteAnterior)
        {
            int score = 0;

            // 1. Avaliação do Preço contra o Ajuste do dia anterior
            if (data.PrecoAtual > precoAjusteAnterior) score += 1;
            else if (data.PrecoAtual < precoAjusteAnterior) score -= 1;

            // 2. Avaliação do Preço contra a VWAP (Preço médio institucional)
            if (data.PrecoAtual > data.VwapDiaria) score += 1;
            else if (data.PrecoAtual < data.VwapDiaria) score -= 1;

            // 3. Avaliação do Delta Acumulado (Apetite do dia)
            if (data.DeltaAcumuladoDia > 0) score += 1;
            else if (data.DeltaAcumuladoDia < 0) score -= 1;

            // 4. Avaliação de Urgência (Se a dominância de uma ponta passa de 55%)
            if (data.PercentualComprador > 55.0) score += 1;
            else if (data.PercentualComprador < 45.0) score -= 1;

            // Mapeamento do Estado com base no Score (-4 a +4)
            string trendState = score switch
            {
                >= 3 => "Macro Tendência: ALTA FORTE (Urgência Touro)",
                1 or 2 => "Macro Tendência: ALTA MODERADA",
                0 => "Mercado em Consolidação / Lateral",
                -1 or -2 => "Macro Tendência: BAIXA MODERADA",
                <= -3 => "Macro Tendência: BAIXA FORTE (Urgência Ursa)"
            };

            return new TrendResult { Score = score, State = trendState, Vwap = data.VwapDiaria };
        }
    }
}
