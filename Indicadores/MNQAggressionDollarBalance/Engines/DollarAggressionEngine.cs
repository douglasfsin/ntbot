using System;

namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	/// <summary>
	/// Aggression balance by financial volume (price × volume × multiplier).
	/// NT-free: unit-testable outside NinjaTrader.
	/// Last at Ask = buy; Last at Bid = sell.
	/// </summary>
	public sealed class DollarAggressionEngine
	{
		private readonly double[] _barDeltas;
		private int _lookback;
		private int _count;
		private int _head;

		private double _sessionBuy;
		private double _sessionSell;
		private double _cumBuy;
		private double _cumSell;
		private double _cumBalance;
		private double _barBuy;
		private double _barSell;
		private double _barDelta;
		private double _rollingBalance;
		private double _lastPrice;
		private double _lastVol;

		public DollarAggressionEngine(int lookback)
		{
			_lookback = Math.Max(1, lookback);
			_barDeltas = new double[_lookback];
		}

		public double SessionBuyFinancial { get { return _sessionBuy; } }
		public double SessionSellFinancial { get { return _sessionSell; } }
		public double SessionBalance { get { return _sessionBuy - _sessionSell; } }
		public double CumulativeBuyFinancial { get { return _cumBuy; } }
		public double CumulativeSellFinancial { get { return _cumSell; } }
		public double CumulativeBalance { get { return _cumBalance; } }
		public double BarBuyFinancial { get { return _barBuy; } }
		public double BarSellFinancial { get { return _barSell; } }
		public double BarDelta { get { return _barDelta; } }
		public double RollingBalance { get { return _rollingBalance; } }
		public double LastPrice { get { return _lastPrice; } }
		public double LastVolume { get { return _lastVol; } }

		public void ResetSession()
		{
			_sessionBuy = 0;
			_sessionSell = 0;
		}

		public void ResetAll()
		{
			_sessionBuy = 0;
			_sessionSell = 0;
			_cumBuy = 0;
			_cumSell = 0;
			_cumBalance = 0;
			_barBuy = 0;
			_barSell = 0;
			_barDelta = 0;
			_rollingBalance = 0;
			_count = 0;
			_head = 0;
			Array.Clear(_barDeltas, 0, _barDeltas.Length);
		}

		/// <summary>Close prior bar into rolling window, then zero bar accumulators.</summary>
		public void ResetBar()
		{
			if (_count > 0 || _barDelta != 0 || _barBuy != 0 || _barSell != 0)
				PushBarDelta(_barDelta);

			_barBuy = 0;
			_barSell = 0;
			_barDelta = 0;
		}

		/// <summary>
		/// Process a Last print. financial = price * volume * multiplier.
		/// classified = true when bid/ask allowed assignment.
		/// </summary>
		public bool ProcessLast(double price, double volume, double bid, double ask, double multiplier, out bool classified)
		{
			classified = false;
			if (volume <= 0 || price <= 0)
				return false;

			_lastPrice = price;
			_lastVol = volume;
			double mult = multiplier > 0 ? multiplier : 1.0;
			double financial = price * volume * mult;

			if (ask > 0 && price >= ask)
			{
				_sessionBuy += financial;
				_cumBuy += financial;
				_barBuy += financial;
				_barDelta += financial;
				_cumBalance += financial;
				classified = true;
				return true;
			}
			if (bid > 0 && price <= bid)
			{
				_sessionSell += financial;
				_cumSell += financial;
				_barSell += financial;
				_barDelta -= financial;
				_cumBalance -= financial;
				classified = true;
				return true;
			}
			return false;
		}

		public double AggressionRatio()
		{
			double tot = _sessionBuy + _sessionSell;
			if (tot <= 0)
				return 0;
			return (_sessionBuy - _sessionSell) / tot;
		}

		private void PushBarDelta(double delta)
		{
			if (_count < _lookback)
			{
				_barDeltas[_count++] = delta;
				_rollingBalance += delta;
				_head = _count % _lookback;
				return;
			}
			_rollingBalance -= _barDeltas[_head];
			_barDeltas[_head] = delta;
			_rollingBalance += delta;
			_head = (_head + 1) % _lookback;
		}
	}
}
