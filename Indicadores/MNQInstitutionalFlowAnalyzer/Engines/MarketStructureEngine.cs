namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class MarketStructureEngine
	{
		private double _lastSwingHigh;
		private double _lastSwingLow;
		private double _prevSwingHigh;
		private double _prevSwingLow;
		private int _dir;

		public StructureLabel LastEvent;
		public string StructureText;
		public double StructureBias;
		public bool BullishStructure;
		public bool BearishStructure;

		public void Update(double high, double low, double close, double prevHigh, double prevLow, double rangeHigh, double rangeLow)
		{
			LastEvent = StructureLabel.None;
			if (high > _lastSwingHigh && _lastSwingHigh > 0)
			{
				_prevSwingHigh = _lastSwingHigh;
				_lastSwingHigh = high;
				if (_lastSwingLow > _prevSwingLow && _prevSwingLow > 0)
				{
					LastEvent = StructureLabel.HH;
					StructureText = "HH / HL";
				}
				else
					StructureText = "HH";
			}
			else if (low < _lastSwingLow && _lastSwingLow > 0)
			{
				_prevSwingLow = _lastSwingLow;
				_lastSwingLow = low;
				if (_lastSwingHigh < _prevSwingHigh && _prevSwingHigh > 0)
				{
					LastEvent = StructureLabel.LL;
					StructureText = "LH / LL";
				}
				else
					StructureText = "LL";
			}

			if (_lastSwingHigh <= 0) _lastSwingHigh = high;
			if (_lastSwingLow <= 0) _lastSwingLow = low;

			bool bosUp = close > _prevSwingHigh && _prevSwingHigh > 0;
			bool bosDn = close < _prevSwingLow && _prevSwingLow > 0;
			if (bosUp && _dir <= 0)
			{
				LastEvent = _dir < 0 ? StructureLabel.CHoCH : StructureLabel.BOS;
				_dir = 1;
			}
			else if (bosDn && _dir >= 0)
			{
				LastEvent = _dir > 0 ? StructureLabel.CHoCH : StructureLabel.BOS;
				_dir = -1;
			}

			bool brk = high > rangeHigh && rangeHigh > 0;
			bool brkDn = low < rangeLow && rangeLow > 0;
			if (brk && close < rangeHigh)
			{
				LastEvent = StructureLabel.FailedBreakout;
				StructureText = "FBO";
			}
			else if (brkDn && close > rangeLow)
			{
				LastEvent = StructureLabel.FailedBreakout;
				StructureText = "FBO";
			}
			else if (brk && close > rangeHigh)
			{
				LastEvent = StructureLabel.Breakout;
				StructureText = "BO";
			}

			BullishStructure = _dir > 0 || (LastEvent == StructureLabel.HH);
			BearishStructure = _dir < 0 || (LastEvent == StructureLabel.LL);
			StructureBias = FlowMath.ClampScore(_dir * 55.0 + (close - (_lastSwingHigh + _lastSwingLow) * 0.5) / System.Math.Max(high - low, 1e-8) * 20);
			if (string.IsNullOrEmpty(StructureText))
				StructureText = _dir > 0 ? "BULL" : _dir < 0 ? "BEAR" : "RANGE";
		}
	}
}
