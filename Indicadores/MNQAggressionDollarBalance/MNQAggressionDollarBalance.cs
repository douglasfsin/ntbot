#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
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
	/// Saldo de agressão por volume financeiro (price × volume × $/ponto).
	/// Compra = Last no Ask; Venda = Last no Bid. Engine: DollarAggressionEngine (NT-free).
	/// </summary>
	public class MNQAggressionDollarBalance : Indicator
	{
		private readonly object _sync = new object();
		private readonly StringBuilder _dashSb = new StringBuilder(512);
		private DollarAggressionEngine _eng;
		private SimpleFont _dashFont;
		private double _lastBid, _lastAsk;
		private bool _histLowQuality;
		private int _ticks, _classified, _unclassified;
		private DateTime _lastDashUtc = DateTime.MinValue;
		private double _lastDashBalance = double.NaN;
		private double _lastDashBar = double.NaN;
		private string _lastBias = "";

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Saldo de agressão por volume financeiro (Compra Ask − Venda Bid).";
				Name = "MNQAggressionDollarBalance";
				// OnPriceChange: tape still via OnMarketData; lighter than OnEachTick for dashboard.
				Calculate = Calculate.OnPriceChange;
				IsOverlay = false;
				DisplayInDataBox = true;
				DrawOnPricePanel = false;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = true;
				PaintPriceMarkers = false;
				ScaleJustification = ScaleJustification.Right;
				IsSuspendedWhileInactive = true;

				ResetOnSession = true;
				Lookback = 20;
				ShowDashboard = true;
				UsePointValue = true;
				DollarPerPoint = 0.50;
				DebugMode = false;
				DashboardThrottleMs = 250;

				AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Line, "SaldoAcumulado");
				AddPlot(new Stroke(Brushes.LimeGreen, 2), PlotStyle.Bar, "SaldoBarra");
				AddLine(Brushes.Gray, 0, "Zero");
			}
			else if (State == State.DataLoaded)
			{
				_eng = new DollarAggressionEngine(Lookback);
				_ticks = _classified = _unclassified = 0;
				_lastBid = _lastAsk = 0;
				_histLowQuality = false;
				_dashFont = new SimpleFont("Consolas", 11);
				_lastDashUtc = DateTime.MinValue;
				_lastDashBalance = double.NaN;
				_lastDashBar = double.NaN;
				_lastBias = "";
			}
			else if (State == State.Historical)
			{
				_histLowQuality = Bars == null || !Bars.IsTickReplay;
			}
			else if (State == State.Realtime)
			{
				_histLowQuality = false;
			}
		}

		protected override void OnMarketData(MarketDataEventArgs e)
		{
			if (_eng == null || BarsInProgress != 0)
				return;

			if (e.MarketDataType == MarketDataType.Bid)
			{
				lock (_sync) { _lastBid = e.Price; }
				return;
			}
			if (e.MarketDataType == MarketDataType.Ask)
			{
				lock (_sync) { _lastAsk = e.Price; }
				return;
			}
			if (e.MarketDataType != MarketDataType.Last)
				return;

			lock (_sync)
			{
				_ticks++;

				// Histórico sem Tick Replay: não inventar Bid/Ask
				if (State == State.Historical && _histLowQuality)
				{
					_unclassified++;
					return;
				}

				double bid = _lastBid > 0 ? _lastBid : GetCurrentBid();
				double ask = _lastAsk > 0 ? _lastAsk : GetCurrentAsk();
				double mult = ResolveMultiplier();

				bool classified;
				_eng.ProcessLast(e.Price, e.Volume, bid, ask, mult, out classified);
				if (classified)
					_classified++;
				else
					_unclassified++;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 0 || BarsInProgress != 0 || _eng == null)
				return;

			if (IsFirstTickOfBar && CurrentBar > 0)
			{
				lock (_sync)
				{
					_eng.ResetBar();
					if (ResetOnSession && Bars.IsFirstBarOfSession)
					{
						_eng.ResetSession();
						_ticks = _classified = _unclassified = 0;
					}
				}
			}

			double balance, barDelta, buy, sell, rolling;
			bool lowQ;
			lock (_sync)
			{
				balance = ResetOnSession ? _eng.SessionBalance : _eng.CumulativeBalance;
				barDelta = _eng.BarDelta;
				buy = ResetOnSession ? _eng.SessionBuyFinancial : _eng.CumulativeBuyFinancial;
				sell = ResetOnSession ? _eng.SessionSellFinancial : _eng.CumulativeSellFinancial;
				rolling = _eng.RollingBalance;
				lowQ = _histLowQuality;
			}

			Values[0][0] = balance;
			Values[1][0] = barDelta;

			PlotBrushes[0][0] = balance >= 0 ? Brushes.LimeGreen : Brushes.OrangeRed;
			PlotBrushes[1][0] = barDelta >= 0 ? Brushes.LimeGreen : Brushes.Crimson;

			if (ShowDashboard && ShouldRedrawDashboard(balance, barDelta))
			{
				DrawDashboard(buy, sell, balance, barDelta, rolling, lowQ);
				_lastDashBalance = balance;
				_lastDashBar = barDelta;
				_lastDashUtc = DateTime.UtcNow;
			}

			if (DebugMode && IsFirstTickOfBar)
			{
				Print(Time[0] + " saldo$=" + balance.ToString("0")
					+ " barra$=" + barDelta.ToString("0")
					+ " compra$=" + buy.ToString("0")
					+ " venda$=" + sell.ToString("0")
					+ " dq=" + (lowQ ? "LOW" : "OK"));
			}
		}

		private bool ShouldRedrawDashboard(double balance, double barDelta)
		{
			if (IsFirstTickOfBar || Calculate == Calculate.OnBarClose)
				return true;

			string bias = balance > 0 ? "COMPRA" : balance < 0 ? "VENDA" : "NEUTRO";
			if (bias != _lastBias)
			{
				_lastBias = bias;
				return true;
			}

			if (double.IsNaN(_lastDashBalance)
				|| Math.Abs(balance - _lastDashBalance) >= 50
				|| Math.Abs(barDelta - _lastDashBar) >= 25)
				return true;

			int throttle = DashboardThrottleMs < 50 ? 50 : DashboardThrottleMs;
			return (DateTime.UtcNow - _lastDashUtc).TotalMilliseconds >= throttle;
		}

		private double ResolveMultiplier()
		{
			if (UsePointValue && Instrument != null && Instrument.MasterInstrument != null)
			{
				double pv = Instrument.MasterInstrument.PointValue;
				if (pv > 0)
					return pv;
			}
			return DollarPerPoint > 0 ? DollarPerPoint : 1.0;
		}

		private double DataQualityScore()
		{
			if (_histLowQuality)
				return 25;
			int total = _classified + _unclassified;
			if (total == 0)
				return 40;
			return 100.0 * _classified / total;
		}

		private void DrawDashboard(double buy, double sell, double saldo, double barra, double rolling, bool lowQ)
		{
			double dq = DataQualityScore();
			string bias;
			if (saldo > 0) bias = "COMPRA";
			else if (saldo < 0) bias = "VENDA";
			else bias = "NEUTRO";
			_lastBias = bias;

			_dashSb.Clear();
			_dashSb.AppendLine("SALDO AGRESSÃO $");
			if (lowQ || dq < 50)
				_dashSb.AppendLine("DATA QUALITY: LOW (" + dq.ToString("0") + ")");
			_dashSb.AppendLine("================================");
			_dashSb.Append(">>>  ").Append(bias).AppendLine("  <<<");
			_dashSb.AppendLine("================================");
			_dashSb.Append("COMPRA           ").AppendLine(FormatMoney(buy));
			_dashSb.Append("VENDA            ").AppendLine(FormatMoney(sell));
			_dashSb.Append("SALDO            ").AppendLine(FormatMoney(saldo));
			_dashSb.Append("SALDO BARRA      ").AppendLine(FormatMoney(barra));
			if (Lookback > 0)
				_dashSb.Append("SALDO ").Append(Lookback).Append(" BARRAS  ").AppendLine(FormatMoney(rolling));
			_dashSb.Append("MULT $/PONTO     ").AppendLine(ResolveMultiplier().ToString("0.####"));
			_dashSb.Append("MODO             ").AppendLine(ResetOnSession ? "Sessão" : "Acumulado");

			Draw.TextFixed(this, "MNQADB_DASH", _dashSb.ToString(), TextPosition.TopLeft,
				Brushes.White, _dashFont ?? new SimpleFont("Consolas", 11),
				Brushes.Transparent, Brushes.Black, 70);
		}

		private static string FormatMoney(double v)
		{
			return v.ToString("+#,##0;-#,##0;0");
		}

		#region Properties
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> SaldoAcumulado
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> SaldoBarra
		{
			get { return Values[1]; }
		}

		[NinjaScriptProperty]
		[Display(Name = "ResetOnSession", Description = "Zera Compra/Venda/Saldo no início de cada sessão", GroupName = "Parameters", Order = 1)]
		public bool ResetOnSession { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Lookback", Description = "Barras para saldo rolante no dashboard", GroupName = "Parameters", Order = 2)]
		public int Lookback { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "ShowDashboard", Description = "Painel Compra / Venda / Saldo (TextFixed). Redesenhado com throttle — ver DashboardThrottleMs", GroupName = "Parameters", Order = 3)]
		public bool ShowDashboard { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "UsePointValue", Description = "Usa Instrument.MasterInstrument.PointValue como multiplicador $", GroupName = "Parameters", Order = 4)]
		public bool UsePointValue { get; set; }

		[NinjaScriptProperty]
		[Range(0.0001, double.MaxValue)]
		[Display(Name = "DollarPerPoint", Description = "Multiplicador $/ponto se UsePointValue=false (MNQ tipicamente 0.50)", GroupName = "Parameters", Order = 5)]
		public double DollarPerPoint { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "DebugMode", GroupName = "Parameters", Order = 6)]
		public bool DebugMode { get; set; }

		[NinjaScriptProperty]
		[Range(50, 5000)]
		[Display(Name = "DashboardThrottleMs", Description = "Intervalo mín. (ms) entre redesenhos do dashboard. Calculate=OnBarClose = mais leve.", GroupName = "Parameters", Order = 7)]
		public int DashboardThrottleMs { get; set; }
		#endregion
	}
}
