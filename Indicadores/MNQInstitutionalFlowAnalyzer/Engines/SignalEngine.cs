namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class SignalEngine
	{
		public SignalState State;
		public string Text;

		/// <summary>UI display bias (colors / banner). Independent of trade gates.</summary>
		public DisplayBias Bias;
		public string BiasText;

		public void Classify(double score, double confidence, double strongBuy, double buy, double sell, double strongSell, double minConf)
		{
			if (confidence < minConf * 0.6)
			{
				State = SignalState.NoTrade;
				Text = "NO_TRADE";
			}
			else if (score >= strongBuy && confidence >= minConf)
			{
				State = SignalState.StrongBuy;
				Text = "STRONG_BUY";
			}
			else if (score <= strongSell && confidence >= minConf)
			{
				State = SignalState.StrongSell;
				Text = "STRONG_SELL";
			}
			else if (score >= buy)
			{
				State = confidence >= minConf ? SignalState.Buy : SignalState.WeakBuy;
				Text = confidence >= minConf ? "BUY" : "WEAK_BUY";
			}
			else if (score <= sell)
			{
				State = confidence >= minConf ? SignalState.Sell : SignalState.WeakSell;
				Text = confidence >= minConf ? "SELL" : "WEAK_SELL";
			}
			else if (confidence < minConf)
			{
				State = SignalState.WaitConfirmation;
				Text = "WAIT_CONFIRMATION";
			}
			else
			{
				State = SignalState.Neutral;
				Text = "NEUTRAL";
			}

			ResolveDisplayBias(score, buy, sell);
		}

		/// <summary>
		/// Maps classified state + score into a UI bias so directional WAIT shows VIÉS COMPRA/VENDA
		/// (green/red-ish) instead of gold, without loosening trade thresholds.
		/// Directional wait when score &lt;= Sell*0.5 or score &lt;= -Buy*0.3 (and symmetric buy side).
		/// </summary>
		public void ResolveDisplayBias(double score, double buy, double sell)
		{
			switch (State)
			{
				case SignalState.StrongBuy:
					Bias = DisplayBias.StrongBuy;
					BiasText = "STRONG_BUY";
					return;
				case SignalState.Buy:
					Bias = DisplayBias.Buy;
					BiasText = "BUY";
					return;
				case SignalState.WeakBuy:
					Bias = DisplayBias.BiasBuy;
					BiasText = "BIAS_BUY";
					return;
				case SignalState.StrongSell:
					Bias = DisplayBias.StrongSell;
					BiasText = "STRONG_SELL";
					return;
				case SignalState.Sell:
					Bias = DisplayBias.Sell;
					BiasText = "SELL";
					return;
				case SignalState.WeakSell:
					Bias = DisplayBias.BiasSell;
					BiasText = "BIAS_SELL";
					return;
				case SignalState.NoTrade:
					Bias = DisplayBias.NoTrade;
					BiasText = "NO_TRADE";
					return;
				case SignalState.Neutral:
					Bias = DisplayBias.Neutral;
					BiasText = "NEUTRAL";
					return;
			}

			// WaitConfirmation / other: gold only when |score| is small; else directional bias.
			double sellGate = sell * 0.5;          // e.g. -30
			double sellGateAlt = -buy * 0.3;       // e.g. -18 — soft directional wait
			double buyGate = buy * 0.5;            // e.g. +30
			double buyGateAlt = buy * 0.3;         // e.g. +18

			if (score <= sellGate || score <= sellGateAlt)
			{
				Bias = DisplayBias.BiasSell;
				BiasText = "BIAS_SELL";
			}
			else if (score >= buyGate || score >= buyGateAlt)
			{
				Bias = DisplayBias.BiasBuy;
				BiasText = "BIAS_BUY";
			}
			else if (State == SignalState.WaitConfirmation)
			{
				Bias = DisplayBias.Wait;
				BiasText = "WAIT_CONFIRMATION";
			}
			else
			{
				Bias = DisplayBias.Neutral;
				BiasText = "NEUTRAL";
			}
		}

		public bool IsTradeSetupValid(
			bool isBuy,
			double score, double confidence,
			double buyTh, double sellTh, double minConf,
			bool structureOk, bool deltaOk, bool aggressionOk,
			bool noOpposingAbsorption, bool wyckoffOk)
		{
			if (confidence < minConf)
				return false;
			if (isBuy)
			{
				if (score < buyTh) return false;
				return structureOk && deltaOk && aggressionOk && noOpposingAbsorption && wyckoffOk;
			}
			if (score > sellTh) return false;
			return structureOk && deltaOk && aggressionOk && noOpposingAbsorption && wyckoffOk;
		}
	}
}
