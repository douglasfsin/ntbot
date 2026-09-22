namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class DivergenceEngine
	{
		private readonly RollingWindow _price;
		private readonly RollingWindow _delta;
		private readonly RollingWindow _volume;
		private readonly RollingWindow _agg;

		public bool BullishDelta;
		public bool BearishDelta;
		public bool VolumeDiv;
		public bool AggressionDiv;
		public bool CrossMarketDiv;
		public bool Any { get { return BullishDelta || BearishDelta || VolumeDiv || AggressionDiv || CrossMarketDiv; } }

		public DivergenceEngine(int lookback)
		{
			_price = new RollingWindow(lookback);
			_delta = new RollingWindow(lookback);
			_volume = new RollingWindow(lookback);
			_agg = new RollingWindow(lookback);
		}

		public void Update(double close, double barDelta, double volume, double aggressionScore, double mnqRet, double esRet, bool hasEs)
		{
			bool hadPrice = _price.Count > 3;
			double prevMinP = _price.Min();
			double prevMaxP = _price.Max();
			double prevMinD = _delta.Min();
			double prevMaxD = _delta.Max();

			_price.Add(close);
			_delta.Add(barDelta);
			_volume.Add(volume);
			_agg.Add(aggressionScore);

			BullishDelta = false;
			BearishDelta = false;
			VolumeDiv = false;
			AggressionDiv = false;
			CrossMarketDiv = false;

			if (!hadPrice)
				return;

			if (close < prevMinP && barDelta > prevMinD)
				BullishDelta = true;
			if (close > prevMaxP && barDelta < prevMaxD)
				BearishDelta = true;
			if (close > prevMaxP && volume < _volume.Mean() * 0.85)
				VolumeDiv = true;
			if (close > prevMaxP && aggressionScore < _agg.Mean())
				AggressionDiv = true;
			if (hasEs && ((mnqRet > 0 && esRet < 0) || (mnqRet < 0 && esRet > 0)))
				CrossMarketDiv = true;
		}
	}
}
