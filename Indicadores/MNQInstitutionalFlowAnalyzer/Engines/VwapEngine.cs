namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class VwapEngine
	{
		private double _pv;
		private double _vol;
		private double _m2;
		private int _n;

		public double Vwap;
		public double StdDev;
		public double VwapScore;
		public string Label;

		public void ResetSession()
		{
			_pv = 0;
			_vol = 0;
			_m2 = 0;
			_n = 0;
			Vwap = 0;
			StdDev = 0;
		}

		public void Add(double typical, double volume)
		{
			if (volume <= 0)
				return;
			_pv += typical * volume;
			_vol += volume;
			_n++;
			Vwap = _vol <= 0 ? typical : _pv / _vol;
			double d = typical - Vwap;
			_m2 += volume * d * d;
			StdDev = _vol <= 0 ? 0 : System.Math.Sqrt(_m2 / _vol);
		}

		public void ScoreAt(double close)
		{
			if (Vwap <= 0)
			{
				VwapScore = 0;
				Label = "N/A";
				return;
			}
			double dist = close - Vwap;
			double z = StdDev > 1e-8 ? dist / StdDev : 0;
			VwapScore = FlowMath.ClampScore(z * 35.0);
			if (close > Vwap) Label = "ABOVE";
			else if (close < Vwap) Label = "BELOW";
			else Label = "AT";
		}
	}
}
