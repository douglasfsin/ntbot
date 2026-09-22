namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class WyckoffEngine
	{
		public WyckoffPhase Phase;
		public double WyckoffScore;
		public double SpringScore;
		public double UpthrustScore;
		public string PhaseText;

		public void Evaluate(
			double close, double high, double low,
			double rangeHigh, double rangeLow,
			double volume, double avgVolume,
			double sellAggression, double buyAggression, double avgAgg,
			double delta, double prevDelta,
			double absorptionScore,
			double structureBias,
			bool closeBackInRange)
		{
			SpringScore = 0;
			UpthrustScore = 0;
			Phase = WyckoffPhase.Unknown;

			bool brokeLow = low < rangeLow && rangeLow > 0;
			bool brokeHigh = high > rangeHigh && rangeHigh > 0;
			bool volUp = volume > avgVolume * 1.2;
			bool sellUp = sellAggression > avgAgg * 1.15;
			bool buyUp = buyAggression > avgAgg * 1.15;
			bool recovered = close > rangeLow && brokeLow && closeBackInRange;
			bool failedUp = close < rangeHigh && brokeHigh && closeBackInRange;
			bool deltaImprove = delta > prevDelta;
			bool deltaWorse = delta < prevDelta;

			if (brokeLow && volUp && sellUp && recovered && deltaImprove)
			{
				SpringScore = FlowMath.Clamp01To100(40 + (volUp ? 15 : 0) + (deltaImprove ? 20 : 0) + (closeBackInRange ? 20 : 0));
				Phase = WyckoffPhase.Spring;
			}
			if (brokeHigh && buyUp && volUp && failedUp && deltaWorse)
			{
				UpthrustScore = FlowMath.Clamp01To100(40 + 15 + (deltaWorse ? 20 : 0) + (closeBackInRange ? 20 : 0));
				Phase = WyckoffPhase.Upthrust;
			}

			if (Phase == WyckoffPhase.Unknown)
			{
				if (structureBias > 40 && delta > 0 && absorptionScore >= 0)
					Phase = WyckoffPhase.Markup;
				else if (structureBias < -40 && delta < 0 && absorptionScore <= 0)
					Phase = WyckoffPhase.Markdown;
				else if (absorptionScore > 40)
					Phase = WyckoffPhase.Accumulation;
				else if (absorptionScore < -40)
					Phase = WyckoffPhase.Distribution;
				else if (System.Math.Abs(structureBias) < 20)
					Phase = WyckoffPhase.TradingRange;
				else if (delta > 0 && structureBias > 0)
					Phase = WyckoffPhase.SignOfStrength;
				else if (delta < 0 && structureBias < 0)
					Phase = WyckoffPhase.SignOfWeakness;
			}

			if (SpringScore > 55)
				WyckoffScore = FlowMath.ClampScore(50 + SpringScore * 0.5);
			else if (UpthrustScore > 55)
				WyckoffScore = FlowMath.ClampScore(-(50 + UpthrustScore * 0.5));
			else
			{
				switch (Phase)
				{
					case WyckoffPhase.Accumulation:
					case WyckoffPhase.Markup:
					case WyckoffPhase.SignOfStrength:
					case WyckoffPhase.Spring:
						WyckoffScore = FlowMath.ClampScore(40 + structureBias * 0.4 + System.Math.Max(0, delta) * 0.05);
						break;
					case WyckoffPhase.Distribution:
					case WyckoffPhase.Markdown:
					case WyckoffPhase.SignOfWeakness:
					case WyckoffPhase.Upthrust:
						WyckoffScore = FlowMath.ClampScore(-40 + structureBias * 0.4 + System.Math.Min(0, delta) * 0.05);
						break;
					default:
						WyckoffScore = FlowMath.ClampScore(structureBias * 0.5);
						break;
				}
			}

			PhaseText = Phase.ToString().ToUpperInvariant();
		}
	}
}
