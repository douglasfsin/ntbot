namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class FlowScoreEngine
	{
		public double InstitutionalFlowScore;
		public double ConfidenceScore;

		public void Compute(
			double wyckoff, double aggression, double delta, double absorption, double imbalance,
			double volume, double vwap, double correlation, double regime,
			double wW, double wA, double wD, double wAb, double wI, double wV, double wVw, double wC, double wR,
			double dataQuality, double minQuality,
			int confirmations, bool divergence, double stability, double volPenalty, double corrAbs)
		{
			double aggQ = FlowMath.ApplyOrderFlowQuality(aggression, dataQuality, minQuality);
			double delQ = FlowMath.ApplyOrderFlowQuality(delta, dataQuality, minQuality);
			double imbQ = FlowMath.ApplyOrderFlowQuality(imbalance, dataQuality, minQuality);

			InstitutionalFlowScore = FlowMath.WeightedFlowScore(
				wyckoff, aggQ, delQ, absorption, imbQ, volume, vwap, correlation, regime,
				wW, wA, wD, wAb, wI, wV, wVw, wC, wR);

			ConfidenceScore = FlowMath.ConfidenceScore(
				dataQuality, confirmations, 8, divergence, stability, volPenalty, corrAbs, InstitutionalFlowScore);
		}
	}
}
