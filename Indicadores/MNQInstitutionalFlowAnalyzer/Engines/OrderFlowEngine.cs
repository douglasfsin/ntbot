namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class OrderFlowEngine
	{
		private readonly RollingWindow _barDeltaWindow;
		private readonly RollingWindow _absDeltaWindow;
		private double _aggressiveBuy;
		private double _aggressiveSell;
		private double _barBuy;
		private double _barSell;
		private double _sessionDelta;
		private double _barDelta;
		private double _prevBarDelta;
		private double _cumDelta;
		private double _lastPrice;
		private double _lastVol;

		public OrderFlowEngine(int deltaLookback)
		{
			_barDeltaWindow = new RollingWindow(deltaLookback);
			_absDeltaWindow = new RollingWindow(deltaLookback);
		}

		public double AggressiveBuyVolume { get { return _aggressiveBuy; } }
		public double AggressiveSellVolume { get { return _aggressiveSell; } }
		public double AggressiveDelta { get { return _aggressiveBuy - _aggressiveSell; } }
		public double BarDelta { get { return _barDelta; } }
		public double SessionDelta { get { return _sessionDelta; } }
		public double RollingDelta { get { return _barDeltaWindow.Mean() * _barDeltaWindow.Count; } }
		public double CumulativeDelta { get { return _cumDelta; } }
		public double LastPrice { get { return _lastPrice; } }
		public double LastVolume { get { return _lastVol; } }

		public void ResetSession()
		{
			_sessionDelta = 0;
			_aggressiveBuy = 0;
			_aggressiveSell = 0;
		}

		public void ResetBar()
		{
			_prevBarDelta = _barDelta;
			if (_barDeltaWindow.Count > 0 || _barDelta != 0)
			{
				_barDeltaWindow.Add(_barDelta);
				_absDeltaWindow.Add(System.Math.Abs(_barDelta));
			}
			_barDelta = 0;
			_barBuy = 0;
			_barSell = 0;
		}

		/// <summary>
		/// Last at Ask = buy aggression; Last at Bid = sell aggression.
		/// unclassified = last strictly inside spread or missing bid/ask.
		/// </summary>
		public bool ProcessLast(double price, double volume, double bid, double ask, out bool classified)
		{
			classified = false;
			if (volume <= 0)
				return false;

			_lastPrice = price;
			_lastVol = volume;

			if (ask > 0 && price >= ask)
			{
				_aggressiveBuy += volume;
				_barBuy += volume;
				_barDelta += volume;
				_sessionDelta += volume;
				_cumDelta += volume;
				classified = true;
				return true;
			}
			if (bid > 0 && price <= bid)
			{
				_aggressiveSell += volume;
				_barSell += volume;
				_barDelta -= volume;
				_sessionDelta -= volume;
				_cumDelta -= volume;
				classified = true;
				return true;
			}
			return false;
		}

		public double AggressionRatioValue()
		{
			return FlowMath.AggressionRatio(_aggressiveBuy, _aggressiveSell);
		}

		public double AggressionScore()
		{
			return FlowMath.AggressionScore(_barBuy + _aggressiveBuy * 0.02, _barSell + _aggressiveSell * 0.02);
		}

		public double DeltaAcceleration()
		{
			return _barDelta - _prevBarDelta;
		}

		public double DeltaMomentum()
		{
			return _barDeltaWindow.Mean();
		}

		public double DeltaZScore()
		{
			return _barDeltaWindow.ZScore(_barDelta);
		}

		public double DeltaScore()
		{
			return FlowMath.DeltaToScore(_barDelta, _absDeltaWindow.Mean());
		}

		public double BarBuy { get { return _barBuy; } }
		public double BarSell { get { return _barSell; } }
	}
}
