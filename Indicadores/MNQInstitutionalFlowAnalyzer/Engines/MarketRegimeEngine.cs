namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class MarketRegimeEngine
	{
		public MarketRegimeKind Regime;
		public double RegimeScore;
		public string Text;

		public void Evaluate(double atr, double atrAvg, double adx, double vwapScore, double structureBias, double volumeZ, double deltaScore)
		{
			Regime = MarketRegimeKind.Unknown;
			bool highVol = atrAvg > 0 && atr > atrAvg * 1.4;
			bool lowVol = atrAvg > 0 && atr < atrAvg * 0.7;

			if (highVol)
				Regime = MarketRegimeKind.HighVolatility;
			else if (lowVol)
				Regime = MarketRegimeKind.LowVolatility;

			if (adx >= 25 && structureBias > 30)
				Regime = MarketRegimeKind.TrendUp;
			else if (adx >= 25 && structureBias < -30)
				Regime = MarketRegimeKind.TrendDown;
			else if (adx > 0 && adx < 18)
				Regime = MarketRegimeKind.Range;

			if (System.Math.Abs(volumeZ) > 2 && System.Math.Abs(structureBias) > 40)
				Regime = MarketRegimeKind.Breakout;
			if (structureBias > 20 && deltaScore > 20 && vwapScore > 0 && Regime != MarketRegimeKind.TrendUp)
			{
				if (System.Math.Abs(structureBias) < 35)
					Regime = MarketRegimeKind.Accumulation;
			}
			if (structureBias < -20 && deltaScore < -20 && vwapScore < 0 && Regime != MarketRegimeKind.TrendDown)
			{
				if (System.Math.Abs(structureBias) < 35)
					Regime = MarketRegimeKind.Distribution;
			}

			switch (Regime)
			{
				case MarketRegimeKind.TrendUp:
				case MarketRegimeKind.Accumulation:
					RegimeScore = FlowMath.ClampScore(30 + structureBias * 0.4 + deltaScore * 0.2);
					break;
				case MarketRegimeKind.Breakout:
					RegimeScore = FlowMath.ClampScore((structureBias >= 0 ? 30 : -30) + structureBias * 0.4 + deltaScore * 0.2);
					break;
				case MarketRegimeKind.TrendDown:
				case MarketRegimeKind.Distribution:
					RegimeScore = FlowMath.ClampScore(-30 + structureBias * 0.4 + deltaScore * 0.2);
					break;
				default:
					RegimeScore = FlowMath.ClampScore(structureBias * 0.3);
					break;
			}
			Text = Regime.ToString().ToUpperInvariant();
		}
	}
}
