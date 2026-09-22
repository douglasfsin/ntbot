namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class ExhaustionEngine
	{
		public ExhaustionKind Kind;
		public double Score;

		public void Evaluate(
			bool newHigh, bool newLow,
			double aggressionBuy, double prevAggressionBuy,
			double aggressionSell, double prevAggressionSell,
			double delta, double prevDelta,
			double threshold)
		{
			Kind = ExhaustionKind.None;
			Score = 0;

			if (newHigh && aggressionBuy < prevAggressionBuy * (1.0 - threshold * 0.05) && delta < prevDelta)
			{
				Kind = ExhaustionKind.BuyerExhaustion;
				Score = FlowMath.ClampScore(-50 - (prevDelta - delta) * 0.1);
			}
			else if (newLow && aggressionSell < prevAggressionSell * (1.0 - threshold * 0.05) && delta > prevDelta)
			{
				Kind = ExhaustionKind.SellerExhaustion;
				Score = FlowMath.ClampScore(50 + (delta - prevDelta) * 0.1);
			}
		}
	}
}
