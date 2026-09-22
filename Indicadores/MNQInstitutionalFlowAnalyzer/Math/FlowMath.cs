using System;

namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	/// <summary>Deterministic math shared by engines and unit tests (no NinjaTrader types).</summary>
	public static class FlowMath
	{
		public static double Clamp(double value, double min, double max)
		{
			if (value < min) return min;
			if (value > max) return max;
			return value;
		}

		public static double ClampScore(double value)
		{
			return Clamp(value, -100.0, 100.0);
		}

		public static double Clamp01To100(double value)
		{
			return Clamp(value, 0.0, 100.0);
		}

		public static double SafeDiv(double numerator, double denominator, double fallback)
		{
			if (Math.Abs(denominator) < 1e-12)
				return fallback;
			return numerator / denominator;
		}

		public static double AggressionRatio(double buyVolume, double sellVolume)
		{
			double sell = Math.Max(sellVolume, 1.0);
			double raw = buyVolume / sell;
			return Clamp(raw, 0.0, 20.0);
		}

		public static double AggressionScore(double buyVolume, double sellVolume)
		{
			double total = buyVolume + sellVolume;
			if (total <= 0)
				return 0;
			double delta = buyVolume - sellVolume;
			return ClampScore(100.0 * delta / total);
		}

		public static double PercentReturn(double previous, double current)
		{
			if (Math.Abs(previous) < 1e-12)
				return 0;
			return (current - previous) / previous;
		}

		public static double Mean(double[] values, int count)
		{
			if (count <= 0)
				return 0;
			double sum = 0;
			for (int i = 0; i < count; i++)
				sum += values[i];
			return sum / count;
		}

		public static double StdDev(double[] values, int count, bool sample)
		{
			if (count <= 1)
				return 0;
			double mean = Mean(values, count);
			double acc = 0;
			for (int i = 0; i < count; i++)
			{
				double d = values[i] - mean;
				acc += d * d;
			}
			double denom = sample ? (count - 1) : count;
			return Math.Sqrt(acc / denom);
		}

		public static double ZScore(double value, double[] window, int count)
		{
			if (count < 2)
				return 0;
			double sd = StdDev(window, count, true);
			if (sd < 1e-12)
				return 0;
			return (value - Mean(window, count)) / sd;
		}

		public static double Pearson(double[] x, double[] y, int count)
		{
			if (count < 3)
				return 0;
			double avgX = 0;
			double avgY = 0;
			for (int i = 0; i < count; i++)
			{
				avgX += x[i];
				avgY += y[i];
			}
			avgX /= count;
			avgY /= count;

			double num = 0;
			double denX = 0;
			double denY = 0;
			for (int i = 0; i < count; i++)
			{
				double dx = x[i] - avgX;
				double dy = y[i] - avgY;
				num += dx * dy;
				denX += dx * dx;
				denY += dy * dy;
			}
			double den = Math.Sqrt(denX * denY);
			if (den < 1e-12)
				return 0;
			return Clamp(num / den, -1.0, 1.0);
		}

		public static string ClassifyCorrelation(double pearson)
		{
			if (pearson >= 0.80) return "STRONG_CONFIRM";
			if (pearson >= 0.50) return "MODERATE_CONFIRM";
			if (pearson >= 0.20) return "WEAK";
			if (pearson > -0.20) return "NEUTRAL";
			return "DIVERGENCE";
		}

		public static double RelativeStrength(double returnA, double returnB)
		{
			return ClampScore((returnA - returnB) * 10000.0);
		}

		public static double LeadershipScore(double mnqRet, double nqRet, double esRet, double rtyRet, bool hasNq, bool hasEs, bool hasRty)
		{
			int n = 1;
			double others = 0;
			if (hasNq) { others += nqRet; n++; }
			if (hasEs) { others += esRet; n++; }
			if (hasRty) { others += rtyRet; n++; }
			double avgOthers = n > 1 ? others / (n - 1) : 0;
			return ClampScore((mnqRet - avgOthers) * 8000.0);
		}

		public static double WeightedFlowScore(
			double wyckoff, double aggression, double delta, double absorption, double imbalance,
			double volume, double vwap, double correlation, double regime,
			double wWyckoff, double wAgg, double wDelta, double wAbs, double wImb,
			double wVol, double wVwap, double wCorr, double wReg)
		{
			double sumW = wWyckoff + wAgg + wDelta + wAbs + wImb + wVol + wVwap + wCorr + wReg;
			if (sumW <= 0)
				return 0;
			double raw =
				wyckoff * wWyckoff +
				aggression * wAgg +
				delta * wDelta +
				absorption * wAbs +
				imbalance * wImb +
				volume * wVol +
				vwap * wVwap +
				correlation * wCorr +
				regime * wReg;
			return ClampScore(raw / sumW);
		}

		public static double ApplyOrderFlowQuality(double orderFlowComponent, double dataQualityScore, double minQuality)
		{
			if (dataQualityScore >= minQuality)
				return orderFlowComponent;
			double factor = Clamp(dataQualityScore / Math.Max(minQuality, 1.0), 0.15, 1.0);
			return orderFlowComponent * factor;
		}

		public static double ConfidenceScore(
			double dataQuality,
			int confirmationCount,
			int maxConfirmations,
			bool hasDivergence,
			double flowStability,
			double volatilityPenalty,
			double correlationAbs,
			double signalStrength)
		{
			double conf = 0;
			conf += dataQuality * 0.28;
			double confRatio = maxConfirmations <= 0 ? 0 : Clamp(confirmationCount / (double)maxConfirmations, 0, 1);
			conf += confRatio * 22.0;
			if (hasDivergence)
				conf -= 12.0;
			conf += Clamp(flowStability, 0, 100) * 0.18;
			conf -= Clamp(volatilityPenalty, 0, 40);
			conf += Clamp(correlationAbs, 0, 1) * 12.0;
			conf += Clamp(Math.Abs(signalStrength), 0, 100) * 0.12;
			return Clamp01To100(conf);
		}

		public static double MultiTimeframeScore(double s60, double s15, double s5, double s1, double s15s,
			double w60, double w15, double w5, double w1, double w15s)
		{
			double sum = w60 + w15 + w5 + w1 + w15s;
			if (sum <= 0)
				return 0;
			return ClampScore((s60 * w60 + s15 * w15 + s5 * w5 + s1 * w1 + s15s * w15s) / sum);
		}

		public static double VolumeZToScore(double z)
		{
			return ClampScore(z * 20.0);
		}

		public static double DeltaToScore(double delta, double avgAbsDelta)
		{
			double scale = Math.Max(avgAbsDelta, 1.0);
			return ClampScore(100.0 * delta / (scale * 4.0));
		}
	}
}
