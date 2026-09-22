#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators.NtBot;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	/// <summary>
	/// MNQ institutional flow: Wyckoff + bid/ask aggression + delta + absorption + imbalance + correlation.
	/// Engines live in NinjaTrader.NinjaScript.Indicators.NtBot (pure math; unit-tested outside NT8).
	/// </summary>
	public class MNQInstitutionalFlowAnalyzer : Indicator
	{
		private readonly object _sync = new object();
		private OrderFlowEngine _of;
		private WyckoffEngine _wy;
		private CorrelationEngine _corr;
		private AbsorptionEngine _abs;
		private ImbalanceEngine _imb;
		private VolumeEngine _vol;
		private MarketRegimeEngine _reg;
		private FlowScoreEngine _flow;
		private SignalEngine _sig;
		private MarketStructureEngine _struct;
		private ExhaustionEngine _exh;
		private DivergenceEngine _div;
		private VwapEngine _vwap;
		private DataQualityEngine _dq;
		private CsvExportBuffer _csv;
		private ATR _atr;
		private ADX _adx;

		private int _idxNq = -1, _idxEs = -1, _idxRty = -1, _idxYm = -1;
		private int _idx60 = -1, _idx15 = -1, _idx5 = -1, _idx1 = -1, _idx15s = -1;
		private double _s60, _s15, _s5, _s1, _s15s;
		private double _lastBid, _lastAsk, _lastLastPrice, _lastLastVol;
		private double _barOpen;
		private double _prevDelta;
		private double _prevBuyAgg, _prevSellAgg;
		private double _bestBidVol, _bestAskVol;
		private bool _histLowQuality;
		private string _csvPath = "";
		private string _corrStatus = "correlation: off";
		private string _resolvedNq = "", _resolvedEs = "", _resolvedRty = "", _resolvedYm = "";

		// Hot-path caches (avoid unbounded Draw objects + per-tick string/TextFixed work)
		private readonly StringBuilder _dashSb = new StringBuilder(1280);
		private readonly StringBuilder _dashHdrSb = new StringBuilder(256);
		private readonly StringBuilder _whySb = new StringBuilder(256);
		private readonly StringBuilder _riskSb = new StringBuilder(256);
		private readonly StringBuilder _bannerSb = new StringBuilder(192);
		private readonly HashSet<string> _firedAlerts = new HashSet<string>();
		private static readonly string[] MarkerPrefixes = { "SPRING", "UT", "BA", "SA", "DIVp", "DIVm", "BO", "FBO" };
		private SimpleFont _dashFont;
		private SimpleFont _bannerFont;
		private DateTime _lastDashUtc = DateTime.MinValue;
		private double _lastDashScore = double.NaN;
		private double _lastDashConf = double.NaN;
		private SignalState _lastDashState = (SignalState)(-1);
		private DisplayBias _lastDashBias = (DisplayBias)(-1);
		private DisplayBias _lastPaintBias = (DisplayBias)(-1);
		private int _firedAlertBar = -1;
		private int _lastMarkerCleanupBar = -1;
		private double _cachedRangeHigh, _cachedRangeLow, _cachedAtrAvg;
		private double _cachedMaxHigh10, _cachedMinLow10;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "MNQ Institutional Flow Analyzer (Wyckoff + Order Flow + Correlation)";
				Name = "MNQInstitutionalFlowAnalyzer";
				// OnPriceChange: OF still updates via OnMarketData; lighter than OnEachTick for score/visuals.
				// Use OnEachTick only if you need score/dashboard every tape print. OnBarClose = lightest.
				Calculate = Calculate.OnPriceChange;
				IsOverlay = false;
				DisplayInDataBox = true;
				DrawOnPricePanel = true;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = true;
				PaintPriceMarkers = false;
				ScaleJustification = ScaleJustification.Right;
				IsSuspendedWhileInactive = true;

				EnableWyckoff = true;
				EnableOrderFlow = true;
				EnableAbsorption = true;
				EnableImbalance = true;
				EnableCorrelation = true;
				EnableVWAP = true;
				EnableVolume = true;
				EnableMarketDepth = false;
				EnableMultiTimeframe = true;
				EnableCsvExport = false;
				DebugMode = false;
				EnablePaintBars = false;
				EnableSignalBanner = true;
				MaxMarkerBars = 150;
				DashboardThrottleMs = 250;

				PrimaryInstrument = "MNQ";
				// Root symbols OK — expiry is auto-matched from the chart contract (e.g. MNQ 09-26 → NQ 09-26).
				// Or set full NT8 names: "NQ 09-26", "ES 09-26".
				CorrelationInstrument1 = "NQ";
				CorrelationInstrument2 = "ES";
				CorrelationInstrument3 = "RTY";
				CorrelationInstrument4 = "YM";
				EnableInstrument1 = true;
				EnableInstrument2 = true;
				EnableInstrument3 = true;
				EnableInstrument4 = false;
				AutoMatchCorrelationExpiry = true;

				CorrelationPeriod = 50;
				VolumeLookback = 50;
				DeltaLookback = 50;
				ImbalanceRatio = 3.0;
				AbsorptionThreshold = 1.8;
				ExhaustionThreshold = 1.0;
				MinDataQuality = 55;

				BuyThreshold = 60;
				SellThreshold = -60;
				StrongBuyThreshold = 75;
				StrongSellThreshold = -75;
				MinimumConfidence = 70;

				WeightWyckoff = 25;
				WeightAggression = 20;
				WeightDelta = 15;
				WeightAbsorption = 10;
				WeightImbalance = 10;
				WeightVolume = 5;
				WeightVwap = 5;
				WeightCorrelation = 5;
				WeightRegime = 5;

				Weight60m = 30;
				Weight15m = 25;
				Weight5m = 20;
				Weight1m = 15;
				Weight15s = 10;

				AlertStrongBuy = true;
				AlertStrongSell = true;
				AlertSpring = true;
				AlertUpthrust = true;
				AlertAbsorption = true;
				AlertExhaustion = true;
				AlertBullDiv = true;
				AlertBearDiv = true;
				AlertBreakout = true;
				AlertFailedBreakout = true;
				AlertScorePlus60 = true;
				AlertScoreMinus60 = true;

				AddPlot(Brushes.DodgerBlue, "InstitutionalFlowScore");
				AddPlot(Brushes.Goldenrod, "ConfidenceScore");
				AddLine(Brushes.Gray, 0, "Zero");
				AddLine(Brushes.SeaGreen, 60, "Plus60");
				AddLine(Brushes.IndianRed, -60, "Minus60");
			}
			else if (State == State.Configure)
			{
				_idxNq = _idxEs = _idxRty = _idxYm = -1;
				_idx60 = _idx15 = _idx5 = _idx1 = _idx15s = -1;
				var corrNotes = new System.Collections.Generic.List<string>();
				int next = 1;
				if (EnableCorrelation)
				{
					if (EnableInstrument1)
						TryAddCorrelationSeries(CorrelationInstrument1, ref _idxNq, ref next, corrNotes, out _resolvedNq);
					if (EnableInstrument2)
						TryAddCorrelationSeries(CorrelationInstrument2, ref _idxEs, ref next, corrNotes, out _resolvedEs);
					if (EnableInstrument3)
						TryAddCorrelationSeries(CorrelationInstrument3, ref _idxRty, ref next, corrNotes, out _resolvedRty);
					if (EnableInstrument4)
						TryAddCorrelationSeries(CorrelationInstrument4, ref _idxYm, ref next, corrNotes, out _resolvedYm);
				}
				_corrStatus = corrNotes.Count == 0
					? (EnableCorrelation ? "correlation: none enabled" : "correlation: off")
					: string.Join(" | ", corrNotes);
				if (DebugMode)
					Print("MNQIFA " + _corrStatus);

				if (EnableMultiTimeframe)
				{
					AddDataSeries(new BarsPeriod { BarsPeriodType = NinjaTrader.Data.BarsPeriodType.Minute, Value = 60 }); _idx60 = next++;
					AddDataSeries(new BarsPeriod { BarsPeriodType = NinjaTrader.Data.BarsPeriodType.Minute, Value = 15 }); _idx15 = next++;
					AddDataSeries(new BarsPeriod { BarsPeriodType = NinjaTrader.Data.BarsPeriodType.Minute, Value = 5 }); _idx5 = next++;
					AddDataSeries(new BarsPeriod { BarsPeriodType = NinjaTrader.Data.BarsPeriodType.Minute, Value = 1 }); _idx1 = next++;
					AddDataSeries(new BarsPeriod { BarsPeriodType = NinjaTrader.Data.BarsPeriodType.Second, Value = 15 }); _idx15s = next++;
				}
			}
			else if (State == State.DataLoaded)
			{
				_of = new OrderFlowEngine(DeltaLookback);
				_wy = new WyckoffEngine();
				_corr = new CorrelationEngine(CorrelationPeriod);
				_abs = new AbsorptionEngine();
				_imb = new ImbalanceEngine();
				_vol = new VolumeEngine(VolumeLookback);
				_reg = new MarketRegimeEngine();
				_flow = new FlowScoreEngine();
				_sig = new SignalEngine();
				_struct = new MarketStructureEngine();
				_exh = new ExhaustionEngine();
				_div = new DivergenceEngine(DeltaLookback);
				_vwap = new VwapEngine();
				_dq = new DataQualityEngine();
				_csv = new CsvExportBuffer(8192);
				_atr = ATR(14);
				_adx = ADX(14);
				_csvPath = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "MNQInstitutionalFlowAnalyzer.csv");
				_dashFont = new SimpleFont("Consolas", 11);
				_bannerFont = new SimpleFont("Consolas", 16);
				_firedAlerts.Clear();
				_lastDashUtc = DateTime.MinValue;
				_lastDashScore = double.NaN;
				_lastDashConf = double.NaN;
				_lastDashState = (SignalState)(-1);
				_lastDashBias = (DisplayBias)(-1);
				_lastPaintBias = (DisplayBias)(-1);
				_firedAlertBar = -1;
				_lastMarkerCleanupBar = -1;
			}
			else if (State == State.Historical)
			{
				_histLowQuality = Bars == null || !Bars.IsTickReplay;
				if (_dq != null)
					_dq.SetHistoricalWithoutTape(_histLowQuality);
			}
			else if (State == State.Realtime)
			{
				if (_dq != null)
					_dq.SetHistoricalWithoutTape(false);
				_histLowQuality = false;
			}
			else if (State == State.Terminated)
			{
				FlushCsv(true);
			}
		}

		protected override void OnMarketData(NinjaTrader.Data.MarketDataEventArgs e)
		{
			if (_of == null || !EnableOrderFlow)
				return;
			if (BarsInProgress != 0)
				return;

			if (e.MarketDataType == NinjaTrader.Data.MarketDataType.Bid)
			{
				lock (_sync) { _lastBid = e.Price; }
				return;
			}
			if (e.MarketDataType == NinjaTrader.Data.MarketDataType.Ask)
			{
				lock (_sync) { _lastAsk = e.Price; }
				return;
			}
			if (e.MarketDataType != NinjaTrader.Data.MarketDataType.Last)
				return;

			lock (_sync)
			{
				if (State == State.Historical && _histLowQuality)
				{
					_dq.Observe(e.Volume, 0, 0, false, false, false);
					return;
				}
				double bid = _lastBid > 0 ? _lastBid : GetCurrentBid();
				double ask = _lastAsk > 0 ? _lastAsk : GetCurrentAsk();
				bool dup = _dq.IsDuplicateSuspect(e.Volume, e.Price, _lastLastPrice, _lastLastVol);
				bool classified;
				_of.ProcessLast(e.Price, e.Volume, bid, ask, out classified);
				bool spike = _dq.IsSpike(e.Volume, Math.Max(_vol != null ? _vol.Average : 0, 1));
				_dq.Observe(e.Volume, classified && e.Price <= bid ? e.Volume : 0, classified && e.Price >= ask ? e.Volume : 0, classified, dup, spike);
				_lastLastPrice = e.Price;
				_lastLastVol = e.Volume;
			}
		}

		protected override void OnMarketDepth(NinjaTrader.Data.MarketDepthEventArgs e)
		{
			if (!EnableMarketDepth || _imb == null)
				return;
			if (BarsInProgress != 0 || e.Position != 0)
				return;

			if (e.MarketDataType == NinjaTrader.Data.MarketDataType.Bid)
			{
				lock (_sync) { _bestBidVol = e.Volume; }
			}
			else if (e.MarketDataType == NinjaTrader.Data.MarketDataType.Ask)
			{
				lock (_sync) { _bestAskVol = e.Volume; }
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBars[0] < 20)
				return;

			if (BarsInProgress != 0)
			{
				UpdateHigherTfScore();
				return;
			}

			// Once-per-bar structural work. OnPriceChange mid-bar must NOT count as newBar
			// (otherwise Volume/Correlation/Divergence double-count).
			bool newBar = IsFirstTickOfBar || Calculate == Calculate.OnBarClose;

			if (IsFirstTickOfBar && CurrentBar > 0)
			{
				lock (_sync)
				{
					_of.ResetBar();
					_barOpen = Open[0];
					if (Bars.IsFirstBarOfSession)
					{
						_of.ResetSession();
						_vwap.ResetSession();
					}
				}
			}

			double atr = _atr[0];
			double adx = _adx[0];
			double typical = (High[0] + Low[0] + Close[0]) / 3.0;
			double nq = SafeClose(_idxNq);
			double es = SafeClose(_idxEs);
			double rty = SafeClose(_idxRty);
			double ym = SafeClose(_idxYm);

			double buyAgg, sellAgg, barDelta, aggScore, deltaScore, dqScore;
			double bidVol, askVol;
			lock (_sync)
			{
				buyAgg = _of.AggressiveBuyVolume;
				sellAgg = _of.AggressiveSellVolume;
				barDelta = _of.BarDelta;
				aggScore = EnableOrderFlow ? _of.AggressionScore() : 0;
				deltaScore = EnableOrderFlow ? _of.DeltaScore() : 0;
				dqScore = _dq.Score();
				bidVol = EnableMarketDepth ? _bestBidVol : _of.BarSell;
				askVol = EnableMarketDepth ? _bestAskVol : _of.BarBuy;
			}

			if (EnableVolume && newBar && CurrentBar > 0)
			{
				double closedVol = Calculate == Calculate.OnBarClose ? Volume[0] : Volume[1];
				_vol.PushBarVolume(closedVol);
			}
			if (EnableVWAP)
			{
				_vwap.Add(typical, Volume[0]);
				_vwap.ScoreAt(Close[0]);
			}

			// Cache lookback extremes — MAX/MIN/SMA every tick is expensive under OnEachTick
			if (newBar || CurrentBar == 20)
			{
				_cachedRangeHigh = MAX(High, 20)[1];
				_cachedRangeLow = MIN(Low, 20)[1];
				_cachedMaxHigh10 = MAX(High, 10)[1];
				_cachedMinLow10 = MIN(Low, 10)[1];
				_cachedAtrAvg = SMA(_atr, 20)[0];
			}

			double rangeHigh = _cachedRangeHigh;
			double rangeLow = _cachedRangeLow;
			_struct.Update(High[0], Low[0], Close[0], High[1], Low[1], rangeHigh, rangeLow);

			bool nearSup = Close[0] - rangeLow <= atr * 0.4;
			bool nearRes = rangeHigh - Close[0] <= atr * 0.4;
			double avgAgg = Math.Max((buyAgg + sellAgg) / Math.Max(CurrentBar, 1), 1);

			if (EnableAbsorption)
				_abs.Evaluate(buyAgg, sellAgg, avgAgg, Close[0] - _barOpen, atr, Volume[0], Math.Max(_vol.Average, 1), nearSup, nearRes, AbsorptionThreshold);

			if (EnableImbalance)
				_imb.EvaluateLevel(askVol, bidVol, ImbalanceRatio);

			bool newHigh = High[0] >= _cachedMaxHigh10;
			bool newLow = Low[0] <= _cachedMinLow10;
			_exh.Evaluate(newHigh, newLow, buyAgg, _prevBuyAgg, sellAgg, _prevSellAgg, barDelta, _prevDelta, ExhaustionThreshold);

			if (EnableWyckoff)
			{
				_wy.Evaluate(Close[0], High[0], Low[0], rangeHigh, rangeLow, Volume[0], Math.Max(_vol.Average, 1),
					sellAgg, buyAgg, avgAgg, barDelta, _prevDelta, _abs.Score, _struct.StructureBias,
					Close[0] <= rangeHigh && Close[0] >= rangeLow);
			}

			double mnqRet = CurrentBar > 0 ? FlowMath.PercentReturn(Close[1], Close[0]) : 0;
			double esRet = 0;
			if (_idxEs > 0 && CurrentBars[_idxEs] > 1)
				esRet = FlowMath.PercentReturn(Closes[_idxEs][1], Closes[_idxEs][0]);

			if (EnableCorrelation && newBar)
				_corr.Push(Close[0], nq, es, rty, ym, _idxNq > 0, _idxEs > 0, _idxRty > 0, _idxYm > 0);

			if (newBar)
				_div.Update(Close[0], barDelta, Volume[0], aggScore, mnqRet, esRet, _idxEs > 0);

			double atrAvg = _cachedAtrAvg;
			_reg.Evaluate(atr, atrAvg, adx, _vwap.VwapScore, _struct.StructureBias, _vol.VolumeZScore, deltaScore);

			int confirms = 0;
			if (aggScore > 20) confirms++;
			if (deltaScore > 20) confirms++;
			if (_wy.WyckoffScore > 20) confirms++;
			if (_vwap.VwapScore > 0) confirms++;
			if (_corr.NqPearson > 0.5) confirms++;
			if (_corr.EsPearson > 0.5) confirms++;
			if (_struct.BullishStructure) confirms++;
			if (_abs.Kind == AbsorptionKind.BuyerAbsorption) confirms++;

			_flow.Compute(
				EnableWyckoff ? _wy.WyckoffScore : 0,
				EnableOrderFlow ? aggScore : 0,
				EnableOrderFlow ? deltaScore : 0,
				EnableAbsorption ? _abs.Score : 0,
				EnableImbalance ? _imb.Score : 0,
				EnableVolume ? _vol.VolumeScore : 0,
				EnableVWAP ? _vwap.VwapScore : 0,
				EnableCorrelation ? _corr.CombinedScore : 0,
				_reg.RegimeScore,
				WeightWyckoff, WeightAggression, WeightDelta, WeightAbsorption, WeightImbalance,
				WeightVolume, WeightVwap, WeightCorrelation, WeightRegime,
				dqScore, MinDataQuality, confirms, _div.Any, 70, Math.Max(0, (atr / Math.Max(atrAvg, 1e-8) - 1) * 15),
				Math.Abs(_corr.NqPearson));

			_s15s = _flow.InstitutionalFlowScore;
			double mtf = FlowMath.MultiTimeframeScore(_s60, _s15, _s5, _s1, _s15s, Weight60m, Weight15m, Weight5m, Weight1m, Weight15s);
			if (EnableMultiTimeframe)
				_flow.InstitutionalFlowScore = FlowMath.ClampScore(_flow.InstitutionalFlowScore * 0.7 + mtf * 0.3);

			_sig.Classify(_flow.InstitutionalFlowScore, _flow.ConfidenceScore, StrongBuyThreshold, BuyThreshold, SellThreshold, StrongSellThreshold, MinimumConfidence);

			Values[0][0] = _flow.InstitutionalFlowScore;
			Values[1][0] = _flow.ConfidenceScore;
			PlotBrushes[0][0] = _flow.InstitutionalFlowScore >= 0 ? Brushes.LimeGreen : Brushes.OrangeRed;

			bool scoreChanged = ShouldRedrawDashboard();
			if (scoreChanged)
			{
				BuildWhy(_whySb, _riskSb, dqScore);
				Brush signalBrush = DisplayTextBrush(_sig.Bias);
				SimpleFont dashFont = _dashFont ?? new SimpleFont("Consolas", 11);

				int hdrLines = (_histLowQuality || dqScore < 50) ? 6 : 5;
				DashboardBuilder.BuildSignalHeaderInto(_dashHdrSb, _sig.BiasText, dqScore, _histLowQuality);
				Draw.TextFixed(this, "MNQIFA_DASH_SIG", _dashHdrSb.ToString(), TextPosition.TopLeft, signalBrush,
					dashFont, Brushes.Transparent, Brushes.Black, 72);

				DashboardBuilder.BuildBodyInto(
					_dashSb,
					_flow.InstitutionalFlowScore, _flow.ConfidenceScore,
					_wy.WyckoffScore, _wy.PhaseText,
					aggScore, deltaScore, _abs.Score, _abs.Kind.ToString(),
					_imb.Score, _vol.VolumeScore, _vol.VolumeLabel ?? "",
					_vwap.VwapScore, _vwap.Label ?? "",
					_corr.NqPearson, _corr.EsPearson, _corr.RtyPearson,
					_reg.Text ?? "", _struct.StructureText ?? "",
					_exh.Kind.ToString(), _div.Any ? "YES" : "NONE",
					_s60, _s15, _s5, _s1, _s15s, mtf, _whySb.ToString(), _riskSb.ToString(),
					_corrStatus, hdrLines);
				// Transparent fill so blank header padding does not paint over MNQIFA_DASH_SIG.
				Draw.TextFixed(this, "MNQIFA_DASH", _dashSb.ToString(), TextPosition.TopLeft, Brushes.WhiteSmoke,
					dashFont, Brushes.Transparent, Brushes.Transparent, 0);

				ApplySignalBanner();
				_lastDashScore = _flow.InstitutionalFlowScore;
				_lastDashConf = _flow.ConfidenceScore;
				_lastDashState = _sig.State;
				_lastDashBias = _sig.Bias;
				_lastDashUtc = DateTime.UtcNow;
			}

			ApplyPaintBars(newBar);
			DrawMarkers(newBar || scoreChanged);
			FireAlerts(newBar || scoreChanged);

			if (EnableCsvExport && IsFirstTickOfBar)
			{
				_csv.Enqueue(Time[0].ToString("o"), Instrument.FullName, Close[0], Volume[0],
					_of.BarSell, _of.BarBuy, barDelta, _of.CumulativeDelta,
					aggScore, _abs.Score, _imb.Score, _wy.WyckoffScore, _vol.VolumeScore, _vwap.VwapScore,
					_corr.CombinedScore, _struct.StructureBias, _flow.InstitutionalFlowScore, _flow.ConfidenceScore,
					_sig.Text, _wy.PhaseText, _reg.Text);
				FlushCsv(false);
			}

			if (DebugMode && IsFirstTickOfBar)
				Print(Time[0] + " score=" + _flow.InstitutionalFlowScore.ToString("0") + " conf=" + _flow.ConfidenceScore.ToString("0") + " dq=" + dqScore.ToString("0") + " " + _sig.Text);

			_prevDelta = barDelta;
			_prevBuyAgg = buyAgg;
			_prevSellAgg = sellAgg;
		}

		private void UpdateHigherTfScore()
		{
			if (CurrentBars[BarsInProgress] < 5)
				return;
			double proxy = 0;
			if (Close[0] > Open[0]) proxy = 40;
			else if (Close[0] < Open[0]) proxy = -40;
			if (BarsInProgress == _idx60) _s60 = proxy;
			else if (BarsInProgress == _idx15) _s15 = proxy;
			else if (BarsInProgress == _idx5) _s5 = proxy;
			else if (BarsInProgress == _idx1) _s1 = proxy;
			else if (BarsInProgress == _idx15s) _s15s = proxy;
		}

		private double SafeClose(int idx)
		{
			if (idx <= 0 || CurrentBars.Length <= idx || CurrentBars[idx] < 0)
				return 0;
			return Closes[idx][0];
		}

		private bool ShouldRedrawDashboard()
		{
			if (IsFirstTickOfBar || Calculate == Calculate.OnBarClose)
				return true;
			if (_lastDashState != _sig.State || _lastDashBias != _sig.Bias)
				return true;
			if (double.IsNaN(_lastDashScore)
				|| Math.Abs(_flow.InstitutionalFlowScore - _lastDashScore) >= 1.0
				|| Math.Abs(_flow.ConfidenceScore - _lastDashConf) >= 2.0)
				return true;
			int throttle = DashboardThrottleMs < 50 ? 50 : DashboardThrottleMs;
			return (DateTime.UtcNow - _lastDashUtc).TotalMilliseconds >= throttle;
		}

		private void BuildWhy(StringBuilder why, StringBuilder risks, double dq)
		{
			why.Clear();
			risks.Clear();
			int n = 1;
			if (_wy.Phase == WyckoffPhase.SignOfStrength || _wy.SpringScore > 55)
			{ why.Append(n).Append(". Wyckoff ").Append(_wy.PhaseText).AppendLine(); n++; }
			if (_of.DeltaScore() > 20) { why.Append(n).Append(". Positive / dominant delta").AppendLine(); n++; }
			if (_of.AggressionScore() > 20) { why.Append(n).Append(". Aggressive buyers dominant").AppendLine(); n++; }
			if (_abs.Kind == AbsorptionKind.BuyerAbsorption) { why.Append(n).Append(". Buyer absorption").AppendLine(); n++; }
			if (_vwap.VwapScore > 0) { why.Append(n).Append(". Price above VWAP").AppendLine(); n++; }
			if (_corr.EsPearson > 0.5) { why.Append(n).Append(". ES confirms").AppendLine(); n++; }
			if (_corr.NqPearson > 0.5) { why.Append(n).Append(". NQ confirms").AppendLine(); n++; }
			if (n == 1) why.AppendLine("Insufficient confluence");

			if (dq < MinDataQuality) risks.AppendLine("- DATA QUALITY LOW");
			if (EnableCorrelation && _idxNq < 0 && _idxEs < 0 && _idxRty < 0 && _idxYm < 0)
				risks.AppendLine("- Correlation series FAIL — use full contracts (NQ 09-26) or AutoMatchCorrelationExpiry");
			if (_corr.HasRty && _corr.RtyPearson < 0.2) risks.AppendLine("- RTY weak / uncorrelated");
			if (_abs.Kind == AbsorptionKind.SellerAbsorption) risks.AppendLine("- Seller absorption");
			if (_div.Any) risks.AppendLine("- Divergence present");
			if (_exh.Kind != ExhaustionKind.None) risks.Append("- Exhaustion: ").Append(_exh.Kind).AppendLine();
			if (risks.Length == 0) risks.AppendLine("- None flagged");
		}

		private void DrawMarkers(bool allow)
		{
			if (!allow)
				return;

			CleanupOldMarkers();

			if (_wy.SpringScore >= 55)
				Draw.Text(this, "SPRING" + CurrentBar, "SPRING", 0, Low[0] - TickSize * 6, Brushes.Lime);
			if (_wy.UpthrustScore >= 55)
				Draw.Text(this, "UT" + CurrentBar, "UT", 0, High[0] + TickSize * 6, Brushes.OrangeRed);
			if (_abs.Kind == AbsorptionKind.BuyerAbsorption)
				Draw.Text(this, "BA" + CurrentBar, "BA", 0, Low[0] - TickSize * 4, Brushes.DeepSkyBlue);
			if (_abs.Kind == AbsorptionKind.SellerAbsorption)
				Draw.Text(this, "SA" + CurrentBar, "SA", 0, High[0] + TickSize * 4, Brushes.HotPink);
			if (_div.BullishDelta)
				Draw.Text(this, "DIVp" + CurrentBar, "DIV+", 0, Low[0] - TickSize * 8, Brushes.LimeGreen);
			if (_div.BearishDelta)
				Draw.Text(this, "DIVm" + CurrentBar, "DIV-", 0, High[0] + TickSize * 8, Brushes.Tomato);
			if (_struct.LastEvent == StructureLabel.Breakout)
				Draw.Text(this, "BO" + CurrentBar, "BO", 0, High[0] + TickSize * 10, Brushes.White);
			if (_struct.LastEvent == StructureLabel.FailedBreakout)
				Draw.Text(this, "FBO" + CurrentBar, "FBO", 0, Close[0], Brushes.Gold);
		}

		private void CleanupOldMarkers()
		{
			int keep = MaxMarkerBars < 20 ? 20 : MaxMarkerBars;
			if (_lastMarkerCleanupBar == CurrentBar)
				return;
			_lastMarkerCleanupBar = CurrentBar;
			int old = CurrentBar - keep;
			if (old < 0)
				return;
			for (int i = 0; i < MarkerPrefixes.Length; i++)
				RemoveDrawObject(MarkerPrefixes[i] + old);
		}

		private void FireAlerts(bool allow)
		{
			if (!allow)
				return;
			if (_firedAlertBar != CurrentBar)
			{
				_firedAlerts.Clear();
				_firedAlertBar = CurrentBar;
			}
			TryAlert("SB", AlertStrongBuy && _sig.State == SignalState.StrongBuy, "MNQ Strong Buy");
			TryAlert("SS", AlertStrongSell && _sig.State == SignalState.StrongSell, "MNQ Strong Sell");
			TryAlert("SP", AlertSpring && _wy.SpringScore >= 55, "MNQ Spring");
			TryAlert("UT", AlertUpthrust && _wy.UpthrustScore >= 55, "MNQ Upthrust");
			TryAlert("AB", AlertAbsorption && _abs.Kind != AbsorptionKind.None, "MNQ Absorption");
			TryAlert("EX", AlertExhaustion && _exh.Kind != ExhaustionKind.None, "MNQ Exhaustion");
			TryAlert("D+", AlertBullDiv && _div.BullishDelta, "MNQ Bullish Divergence");
			TryAlert("D-", AlertBearDiv && _div.BearishDelta, "MNQ Bearish Divergence");
			TryAlert("BO", AlertBreakout && _struct.LastEvent == StructureLabel.Breakout, "MNQ Breakout");
			TryAlert("FB", AlertFailedBreakout && _struct.LastEvent == StructureLabel.FailedBreakout, "MNQ Failed Breakout");
			TryAlert("P60", AlertScorePlus60 && _flow.InstitutionalFlowScore >= 60 && _flow.InstitutionalFlowScore - Values[0][1] < 60, "MNQ Score +60");
			TryAlert("M60", AlertScoreMinus60 && _flow.InstitutionalFlowScore <= -60, "MNQ Score -60");
		}

		private void TryAlert(string key, bool cond, string msg)
		{
			if (!cond)
				return;
			string id = key + CurrentBar;
			if (!_firedAlerts.Add(id))
				return;
			Alert(id, Priority.Medium, msg, NinjaTrader.Core.Globals.InstallDir + @"\sounds\Alert1.wav", 60, Brushes.White, Brushes.DarkSlateGray);
		}

		private void FlushCsv(bool force)
		{
			if (_csv == null || !EnableCsvExport)
				return;
			if (!force && !_csv.ShouldFlush)
				return;
			if (!_csv.HasPending)
				return;
			try
			{
				File.AppendAllText(_csvPath, _csv.Drain());
			}
			catch (Exception ex)
			{
				if (DebugMode)
					Print("CSV export failed: " + ex.Message);
			}
		}

		public bool IsTradeSetupValid(bool isBuy)
		{
			if (_sig == null || _flow == null)
				return false;
			return _sig.IsTradeSetupValid(isBuy, _flow.InstitutionalFlowScore, _flow.ConfidenceScore,
				BuyThreshold, SellThreshold, MinimumConfidence,
				isBuy ? _struct.BullishStructure : _struct.BearishStructure,
				isBuy ? _of.DeltaScore() > 0 : _of.DeltaScore() < 0,
				isBuy ? _of.AggressionScore() > 0 : _of.AggressionScore() < 0,
				isBuy ? _abs.Kind != AbsorptionKind.SellerAbsorption : _abs.Kind != AbsorptionKind.BuyerAbsorption,
				isBuy ? _wy.WyckoffScore >= 0 : _wy.WyckoffScore <= 0);
		}

		#region Properties
		[NinjaScriptProperty, Display(Name = "EnableWyckoff", GroupName = "Modules", Order = 1)]
		public bool EnableWyckoff { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableOrderFlow", GroupName = "Modules", Order = 2)]
		public bool EnableOrderFlow { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableAbsorption", GroupName = "Modules", Order = 3)]
		public bool EnableAbsorption { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableImbalance", GroupName = "Modules", Order = 4)]
		public bool EnableImbalance { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableCorrelation", GroupName = "Modules", Order = 5)]
		public bool EnableCorrelation { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableVWAP", GroupName = "Modules", Order = 6)]
		public bool EnableVWAP { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableVolume", GroupName = "Modules", Order = 7)]
		public bool EnableVolume { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableMarketDepth", GroupName = "Modules", Order = 8)]
		public bool EnableMarketDepth { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableMultiTimeframe", GroupName = "Modules", Order = 9)]
		public bool EnableMultiTimeframe { get; set; }
		[NinjaScriptProperty, Display(Name = "DebugMode", GroupName = "Modules", Order = 10)]
		public bool DebugMode { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableCsvExport", GroupName = "Modules", Order = 11)]
		public bool EnableCsvExport { get; set; }
		[NinjaScriptProperty, Display(Name = "EnablePaintBars", Description = "CUSTOSO: pinta candles no painel de preço. Desligado por padrão — ative só se precisar.", GroupName = "Visual", Order = 1)]
		public bool EnablePaintBars { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableSignalBanner", Description = "Banner COMPRA/VENDA (TextFixed) — redesenhado só quando score/sinal muda ou a cada DashboardThrottleMs", GroupName = "Visual", Order = 2)]
		public bool EnableSignalBanner { get; set; }
		[Range(20, 2000), NinjaScriptProperty, Display(Name = "MaxMarkerBars", Description = "Remove Draw.Text (SPRING/UT/BA/…) além deste lookback — evita freeze por objetos acumulados", GroupName = "Visual", Order = 3)]
		public int MaxMarkerBars { get; set; }
		[Range(50, 5000), NinjaScriptProperty, Display(Name = "DashboardThrottleMs", Description = "Intervalo mín. (ms) entre redesenhos do dashboard sob OnEachTick/OnPriceChange. Calculate=OnBarClose ignora throttle.", GroupName = "Visual", Order = 4)]
		public int DashboardThrottleMs { get; set; }

		[NinjaScriptProperty, Display(Name = "PrimaryInstrument", GroupName = "Instruments", Order = 1)]
		public string PrimaryInstrument { get; set; }
		[NinjaScriptProperty, Display(Name = "CorrelationInstrument1", GroupName = "Instruments", Order = 2)]
		public string CorrelationInstrument1 { get; set; }
		[NinjaScriptProperty, Display(Name = "CorrelationInstrument2", GroupName = "Instruments", Order = 3)]
		public string CorrelationInstrument2 { get; set; }
		[NinjaScriptProperty, Display(Name = "CorrelationInstrument3", GroupName = "Instruments", Order = 4)]
		public string CorrelationInstrument3 { get; set; }
		[NinjaScriptProperty, Display(Name = "CorrelationInstrument4", GroupName = "Instruments", Order = 5)]
		public string CorrelationInstrument4 { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableInstrument1", GroupName = "Instruments", Order = 6)]
		public bool EnableInstrument1 { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableInstrument2", GroupName = "Instruments", Order = 7)]
		public bool EnableInstrument2 { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableInstrument3", GroupName = "Instruments", Order = 8)]
		public bool EnableInstrument3 { get; set; }
		[NinjaScriptProperty, Display(Name = "EnableInstrument4", GroupName = "Instruments", Order = 9)]
		public bool EnableInstrument4 { get; set; }
		[NinjaScriptProperty, Display(Name = "AutoMatchCorrelationExpiry", Description = "If true, root symbols like NQ become NQ 09-26 using the chart contract expiry.", GroupName = "Instruments", Order = 10)]
		public bool AutoMatchCorrelationExpiry { get; set; }

		[Range(5, 500), NinjaScriptProperty, Display(Name = "CorrelationPeriod", GroupName = "Parameters", Order = 1)]
		public int CorrelationPeriod { get; set; }
		[Range(5, 500), NinjaScriptProperty, Display(Name = "VolumeLookback", GroupName = "Parameters", Order = 2)]
		public int VolumeLookback { get; set; }
		[Range(5, 500), NinjaScriptProperty, Display(Name = "DeltaLookback", GroupName = "Parameters", Order = 3)]
		public int DeltaLookback { get; set; }
		[Range(1.1, 20), NinjaScriptProperty, Display(Name = "ImbalanceRatio", GroupName = "Parameters", Order = 4)]
		public double ImbalanceRatio { get; set; }
		[Range(0.5, 10), NinjaScriptProperty, Display(Name = "AbsorptionThreshold", GroupName = "Parameters", Order = 5)]
		public double AbsorptionThreshold { get; set; }
		[Range(0.1, 10), NinjaScriptProperty, Display(Name = "ExhaustionThreshold", GroupName = "Parameters", Order = 6)]
		public double ExhaustionThreshold { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "MinDataQuality", GroupName = "Parameters", Order = 7)]
		public double MinDataQuality { get; set; }

		[Range(-100, 100), NinjaScriptProperty, Display(Name = "BuyThreshold", GroupName = "Signals", Order = 1)]
		public double BuyThreshold { get; set; }
		[Range(-100, 100), NinjaScriptProperty, Display(Name = "SellThreshold", GroupName = "Signals", Order = 2)]
		public double SellThreshold { get; set; }
		[Range(-100, 100), NinjaScriptProperty, Display(Name = "StrongBuyThreshold", GroupName = "Signals", Order = 3)]
		public double StrongBuyThreshold { get; set; }
		[Range(-100, 100), NinjaScriptProperty, Display(Name = "StrongSellThreshold", GroupName = "Signals", Order = 4)]
		public double StrongSellThreshold { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "MinimumConfidence", GroupName = "Signals", Order = 5)]
		public double MinimumConfidence { get; set; }

		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightWyckoff", GroupName = "Weights", Order = 1)]
		public double WeightWyckoff { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightAggression", GroupName = "Weights", Order = 2)]
		public double WeightAggression { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightDelta", GroupName = "Weights", Order = 3)]
		public double WeightDelta { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightAbsorption", GroupName = "Weights", Order = 4)]
		public double WeightAbsorption { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightImbalance", GroupName = "Weights", Order = 5)]
		public double WeightImbalance { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightVolume", GroupName = "Weights", Order = 6)]
		public double WeightVolume { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightVwap", GroupName = "Weights", Order = 7)]
		public double WeightVwap { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightCorrelation", GroupName = "Weights", Order = 8)]
		public double WeightCorrelation { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "WeightRegime", GroupName = "Weights", Order = 9)]
		public double WeightRegime { get; set; }

		[Range(0, 100), NinjaScriptProperty, Display(Name = "Weight60m", GroupName = "MTF Weights", Order = 1)]
		public double Weight60m { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "Weight15m", GroupName = "MTF Weights", Order = 2)]
		public double Weight15m { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "Weight5m", GroupName = "MTF Weights", Order = 3)]
		public double Weight5m { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "Weight1m", GroupName = "MTF Weights", Order = 4)]
		public double Weight1m { get; set; }
		[Range(0, 100), NinjaScriptProperty, Display(Name = "Weight15s", GroupName = "MTF Weights", Order = 5)]
		public double Weight15s { get; set; }

		[NinjaScriptProperty, Display(Name = "AlertStrongBuy", GroupName = "Alerts", Order = 1)]
		public bool AlertStrongBuy { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertStrongSell", GroupName = "Alerts", Order = 2)]
		public bool AlertStrongSell { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertSpring", GroupName = "Alerts", Order = 3)]
		public bool AlertSpring { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertUpthrust", GroupName = "Alerts", Order = 4)]
		public bool AlertUpthrust { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertAbsorption", GroupName = "Alerts", Order = 5)]
		public bool AlertAbsorption { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertExhaustion", GroupName = "Alerts", Order = 6)]
		public bool AlertExhaustion { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertBullDiv", GroupName = "Alerts", Order = 7)]
		public bool AlertBullDiv { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertBearDiv", GroupName = "Alerts", Order = 8)]
		public bool AlertBearDiv { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertBreakout", GroupName = "Alerts", Order = 9)]
		public bool AlertBreakout { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertFailedBreakout", GroupName = "Alerts", Order = 10)]
		public bool AlertFailedBreakout { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertScorePlus60", GroupName = "Alerts", Order = 11)]
		public bool AlertScorePlus60 { get; set; }
		[NinjaScriptProperty, Display(Name = "AlertScoreMinus60", GroupName = "Alerts", Order = 12)]
		public bool AlertScoreMinus60 { get; set; }

		[Browsable(false), XmlIgnore]
		public Series<double> InstitutionalFlowScoreSeries { get { return Values[0]; } }
		[Browsable(false), XmlIgnore]
		public Series<double> ConfidenceScoreSeries { get { return Values[1]; } }
		#endregion

		private void ApplySignalBanner()
		{
			if (!EnableSignalBanner)
				return;

			DisplayBias bias = _sig.Bias;
			Brush fg = DisplayTextBrush(bias);
			Brush bg = DisplayBgBrush(bias);
			string pt = DashboardBuilder.ToPortugueseBias(_sig.BiasText);

			_bannerSb.Clear();
			_bannerSb.AppendLine("================");
			_bannerSb.Append("  ").Append(pt).AppendLine();
			_bannerSb.Append("  score ").Append(DashboardBuilder.Signed(_flow.InstitutionalFlowScore))
				.Append("  conf ").Append(_flow.ConfidenceScore.ToString("0")).AppendLine();
			_bannerSb.Append("================");
			Draw.TextFixed(this, "MNQIFA_SIG", _bannerSb.ToString(), TextPosition.TopRight, fg,
				_bannerFont ?? new SimpleFont("Consolas", 16), Brushes.Transparent, bg, 88);
		}

		private void ApplyPaintBars(bool newBar)
		{
			if (!EnablePaintBars)
				return;

			DisplayBias bias = _sig.Bias;
			if (!newBar && bias == _lastPaintBias)
				return;
			_lastPaintBias = bias;

			if (bias == DisplayBias.StrongBuy || bias == DisplayBias.Buy)
			{
				BarBrushes[0] = bias == DisplayBias.StrongBuy ? Brushes.Lime : Brushes.LimeGreen;
				CandleOutlineBrushes[0] = Brushes.DarkGreen;
			}
			else if (bias == DisplayBias.BiasBuy)
			{
				BarBrushes[0] = Brushes.LightGreen;
				CandleOutlineBrushes[0] = Brushes.SeaGreen;
			}
			else if (bias == DisplayBias.StrongSell || bias == DisplayBias.Sell)
			{
				BarBrushes[0] = bias == DisplayBias.StrongSell ? Brushes.Red : Brushes.OrangeRed;
				CandleOutlineBrushes[0] = Brushes.DarkRed;
			}
			else if (bias == DisplayBias.BiasSell)
			{
				BarBrushes[0] = Brushes.Tomato;
				CandleOutlineBrushes[0] = Brushes.Firebrick;
			}
			else if (bias == DisplayBias.Wait || bias == DisplayBias.NoTrade)
			{
				BarBrushes[0] = bias == DisplayBias.NoTrade ? Brushes.Gray : Brushes.Goldenrod;
				CandleOutlineBrushes[0] = bias == DisplayBias.NoTrade ? Brushes.DimGray : Brushes.DarkGoldenrod;
			}
		}

		private static Brush DisplayTextBrush(DisplayBias b)
		{
			switch (b)
			{
				case DisplayBias.StrongBuy: return Brushes.Lime;
				case DisplayBias.Buy: return Brushes.LimeGreen;
				case DisplayBias.BiasBuy: return Brushes.LightGreen;
				case DisplayBias.StrongSell: return Brushes.Red;
				case DisplayBias.Sell: return Brushes.OrangeRed;
				case DisplayBias.BiasSell: return Brushes.Tomato;
				case DisplayBias.Wait: return Brushes.Gold;
				case DisplayBias.NoTrade: return Brushes.Gray;
				default: return Brushes.WhiteSmoke;
			}
		}

		private static Brush DisplayBgBrush(DisplayBias b)
		{
			switch (b)
			{
				case DisplayBias.StrongBuy:
				case DisplayBias.Buy: return Brushes.DarkGreen;
				case DisplayBias.BiasBuy: return Brushes.DarkOliveGreen;
				case DisplayBias.StrongSell:
				case DisplayBias.Sell: return Brushes.DarkRed;
				case DisplayBias.BiasSell: return Brushes.Maroon;
				case DisplayBias.Wait: return Brushes.DarkSlateGray;
				case DisplayBias.NoTrade: return Brushes.DimGray;
				default: return Brushes.Black;
			}
		}

		private void TryAddCorrelationSeries(string symbol, ref int idxField, ref int next,
			System.Collections.Generic.List<string> notes, out string resolved)
		{
			resolved = "";
			if (string.IsNullOrWhiteSpace(symbol))
				return;

			resolved = ResolveCorrelationSymbol(symbol.Trim());
			if (string.IsNullOrWhiteSpace(resolved))
			{
				notes.Add(symbol + "=FAIL(empty)");
				return;
			}

			try
			{
				// Same timeframe as the host chart so Pearson uses aligned bars.
				AddDataSeries(resolved, BarsPeriod.BarsPeriodType, BarsPeriod.Value);
				idxField = next++;
				notes.Add(resolved + "=OK");
			}
			catch (Exception ex)
			{
				idxField = -1;
				notes.Add(resolved + "=FAIL");
				if (DebugMode)
					Print("MNQIFA AddDataSeries failed for '" + resolved + "': " + ex.Message);
			}
		}

		/// <summary>
		/// Maps root futures (NQ) to full NT8 contracts (NQ 09-26) using the chart instrument expiry.
		/// Full names and continuous (^NQ) pass through unchanged.
		/// </summary>
		private string ResolveCorrelationSymbol(string configured)
		{
			if (string.IsNullOrWhiteSpace(configured))
				return null;

			configured = configured.Trim();

			// Continuous or already a full contract name ("NQ 09-26", "ES JUN25").
			if (configured.StartsWith("^", StringComparison.Ordinal) || configured.IndexOf(' ') > 0)
				return configured;

			if (!AutoMatchCorrelationExpiry)
				return configured;

			string expiry = ExtractExpirySuffix(Instrument != null ? Instrument.FullName : null);
			if (string.IsNullOrEmpty(expiry))
				return configured;

			return configured + " " + expiry;
		}

		private static string ExtractExpirySuffix(string fullName)
		{
			if (string.IsNullOrWhiteSpace(fullName))
				return null;

			// "MNQ 09-26" / "MNQ SEP26" / "MNQ 09-26 Globex"
			string[] parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				return null;

			// Prefer the token that looks like MM-YY (09-26) or month code (SEP26 / JUN25).
			for (int i = 1; i < parts.Length; i++)
			{
				string p = parts[i];
				if (p.IndexOf('-') > 0)
					return p;
				if (p.Length >= 5 && char.IsLetter(p[0]))
					return p;
			}
			return parts[1];
		}
	}
}
