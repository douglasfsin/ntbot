namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class ImbalanceEngine
	{
		private int _stackBuy;
		private int _stackSell;

		public ImbalanceKind Kind;
		public double Score;
		public int StackedImbalance;

		public void EvaluateLevel(double askVolume, double bidVolume, double ratio)
		{
			Kind = ImbalanceKind.None;
			if (ratio < 1.01)
				ratio = 3.0;

			if (askVolume >= bidVolume * ratio && askVolume > 0)
			{
				Kind = ImbalanceKind.BuyImbalance;
				_stackBuy++;
				_stackSell = 0;
			}
			else if (bidVolume >= askVolume * ratio && bidVolume > 0)
			{
				Kind = ImbalanceKind.SellImbalance;
				_stackSell++;
				_stackBuy = 0;
			}
			else
			{
				_stackBuy = 0;
				_stackSell = 0;
			}

			StackedImbalance = _stackBuy > 0 ? _stackBuy : -_stackSell;
			if (Kind == ImbalanceKind.BuyImbalance)
				Score = FlowMath.ClampScore(50 + 10 * _stackBuy + FlowMath.SafeDiv(askVolume, bidVolume + 1, 1) * 5);
			else if (Kind == ImbalanceKind.SellImbalance)
				Score = FlowMath.ClampScore(-(50 + 10 * _stackSell + FlowMath.SafeDiv(bidVolume, askVolume + 1, 1) * 5));
			else
				Score = FlowMath.ClampScore(FlowMath.SafeDiv(askVolume - bidVolume, askVolume + bidVolume, 0) * 100);
		}

		public void ResetStack()
		{
			_stackBuy = 0;
			_stackSell = 0;
			StackedImbalance = 0;
		}
	}
}
