namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class AbsorptionEngine
	{
		public AbsorptionKind Kind;
		public double Score;

		public void Evaluate(
			double aggressiveBuy, double aggressiveSell,
			double avgAggression,
			double priceChange,
			double atr,
			double volume, double avgVolume,
			bool nearSupport, bool nearResistance,
			double threshold)
		{
			Kind = AbsorptionKind.None;
			Score = 0;
			if (atr <= 0 || avgAggression <= 0)
				return;

			double move = System.Math.Abs(priceChange);
			bool flat = move <= atr * 0.18;
			bool highVol = volume >= avgVolume * 1.15;
			double buyX = FlowMath.SafeDiv(aggressiveBuy, avgAggression, 0);
			double sellX = FlowMath.SafeDiv(aggressiveSell, avgAggression, 0);

			if (sellX >= threshold && flat && highVol)
			{
				Kind = AbsorptionKind.BuyerAbsorption;
				Score = FlowMath.ClampScore(40 + 20 * sellX + (nearSupport ? 20 : 0) - move / atr * 30);
			}
			else if (buyX >= threshold && flat && highVol)
			{
				Kind = AbsorptionKind.SellerAbsorption;
				Score = FlowMath.ClampScore(-(40 + 20 * buyX + (nearResistance ? 20 : 0) - move / atr * 30));
			}
		}
	}
}
