using System.Text;

namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class CsvExportBuffer
	{
		private readonly StringBuilder _sb = new StringBuilder(8192);
		private readonly int _flushChars;
		private bool _headerWritten;

		public CsvExportBuffer(int flushChars)
		{
			_flushChars = flushChars < 1024 ? 4096 : flushChars;
		}

		public bool HasPending { get { return _sb.Length > 0; } }

		public void Clear()
		{
			_sb.Length = 0;
			_headerWritten = false;
		}

		public void Enqueue(
			string timestamp, string instrument, double price, double volume,
			double bidVol, double askVol, double delta, double cumDelta,
			double aggression, double absorption, double imbalance, double wyckoff,
			double volumeScore, double vwapScore, double corrScore, double structure,
			double flow, double confidence, string signal, string phase, string regime)
		{
			if (!_headerWritten)
			{
				_sb.AppendLine("Timestamp,Instrument,Price,Volume,BidVolume,AskVolume,Delta,CumulativeDelta,AggressionScore,AbsorptionScore,ImbalanceScore,WyckoffScore,VolumeScore,VWAPScore,CorrelationScore,StructureScore,InstitutionalFlowScore,ConfidenceScore,Signal,WyckoffPhase,MarketRegime");
				_headerWritten = true;
			}
			_sb.Append(timestamp).Append(',').Append(instrument).Append(',')
				.Append(Inv(price)).Append(',').Append(Inv(volume)).Append(',')
				.Append(Inv(bidVol)).Append(',').Append(Inv(askVol)).Append(',')
				.Append(Inv(delta)).Append(',').Append(Inv(cumDelta)).Append(',')
				.Append(Inv(aggression)).Append(',').Append(Inv(absorption)).Append(',')
				.Append(Inv(imbalance)).Append(',').Append(Inv(wyckoff)).Append(',')
				.Append(Inv(volumeScore)).Append(',').Append(Inv(vwapScore)).Append(',')
				.Append(Inv(corrScore)).Append(',').Append(Inv(structure)).Append(',')
				.Append(Inv(flow)).Append(',').Append(Inv(confidence)).Append(',')
				.Append(Csv(signal)).Append(',').Append(Csv(phase)).Append(',').Append(Csv(regime))
				.AppendLine();
		}

		public bool ShouldFlush { get { return _sb.Length >= _flushChars; } }

		public string Drain()
		{
			string s = _sb.ToString();
			_sb.Length = 0;
			_headerWritten = false;
			return s;
		}

		private static string Inv(double v)
		{
			return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		private static string Csv(string s)
		{
			if (s == null) return "";
			if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0)
				return "\"" + s.Replace("\"", "\"\"") + "\"";
			return s;
		}
	}
}
