namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public enum AbsorptionKind
	{
		None,
		BuyerAbsorption,
		SellerAbsorption
	}

	public enum ExhaustionKind
	{
		None,
		BuyerExhaustion,
		SellerExhaustion
	}

	public enum ImbalanceKind
	{
		None,
		BuyImbalance,
		SellImbalance
	}

	public enum WyckoffPhase
	{
		Unknown,
		Accumulation,
		Markup,
		Distribution,
		Markdown,
		TradingRange,
		Spring,
		Upthrust,
		SignOfStrength,
		SignOfWeakness,
		Test,
		Absorption,
		Exhaustion
	}

	public enum MarketRegimeKind
	{
		Unknown,
		TrendUp,
		TrendDown,
		Range,
		HighVolatility,
		LowVolatility,
		Breakout,
		Accumulation,
		Distribution
	}

	public enum SignalState
	{
		StrongBuy,
		Buy,
		WeakBuy,
		Neutral,
		WeakSell,
		Sell,
		StrongSell,
		NoTrade,
		WaitConfirmation,
		PossibleReversal,
		Breakout,
		FailedBreakout,
		Absorption,
		Exhaustion
	}

	/// <summary>
	/// UI-only bias for banner/dashboard/paint colors. Does not affect IsTradeSetupValid gates.
	/// </summary>
	public enum DisplayBias
	{
		StrongBuy,
		Buy,
		BiasBuy,
		Wait,
		Neutral,
		BiasSell,
		Sell,
		StrongSell,
		NoTrade
	}

	public enum StructureLabel
	{
		None,
		HH,
		HL,
		LH,
		LL,
		BOS,
		CHoCH,
		Breakout,
		FailedBreakout
	}
}
