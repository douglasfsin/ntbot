namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class DataQualityEngine
	{
		private int _ticks;
		private int _invalid;
		private int _classified;
		private int _unclassified;
		private double _lastVolume;
		private bool _historicalWithoutTape;

		public void Reset()
		{
			_ticks = 0;
			_invalid = 0;
			_classified = 0;
			_unclassified = 0;
			_lastVolume = 0;
			_historicalWithoutTape = false;
		}

		public void SetHistoricalWithoutTape(bool value)
		{
			_historicalWithoutTape = value;
		}

		public void Observe(double lastVolume, double bidVol, double askVol, bool classified, bool duplicateSuspect, bool spike)
		{
			_ticks++;
			if (lastVolume < 0 || bidVol < 0 || askVol < 0)
				_invalid++;
			if (duplicateSuspect)
				_invalid++;
			if (spike)
				_invalid++;
			if (lastVolume == 0 && (bidVol + askVol) > 0)
				_invalid++;
			if (classified) _classified++;
			else _unclassified++;
			_lastVolume = lastVolume;
		}

		public bool VolumeConsistencyCheck(double total, double bid, double ask)
		{
			if (total < 0 || bid < 0 || ask < 0)
				return false;
			double sum = bid + ask;
			if (sum <= 0 && total <= 0)
				return true;
			double diff = System.Math.Abs(total - sum);
			double tol = System.Math.Max(1.0, total * 0.15);
			return diff <= tol;
		}

		public double Score()
		{
			if (_historicalWithoutTape)
				return 25;
			if (_ticks == 0)
				return 40;
			double validRatio = 1.0 - (_invalid / (double)System.Math.Max(_ticks, 1));
			double classRatio = _classified / (double)System.Math.Max(_classified + _unclassified, 1);
			double s = 100.0 * (0.55 * validRatio + 0.45 * classRatio);
			return FlowMath.Clamp01To100(s);
		}

		public bool IsSpike(double volume, double avgVolume)
		{
			if (avgVolume <= 0)
				return false;
			return volume > avgVolume * 25.0;
		}

		public bool IsDuplicateSuspect(double volume, double price, double lastPrice, double lastVol)
		{
			return volume == lastVol && System.Math.Abs(price - lastPrice) < 1e-12 && volume > 0;
		}
	}
}
