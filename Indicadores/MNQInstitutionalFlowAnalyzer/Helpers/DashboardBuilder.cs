using System.Text;

namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public static class DashboardBuilder
	{
		public static string Build(
			double flow, double confidence, string signal,
			double wyckoff, string wyckoffPhase,
			double aggression, double delta, double absorption, string absorptionKind,
			double imbalance, double volumeScore, string volumeLabel,
			double vwapScore, string vwapLabel,
			double nq, double es, double rty,
			string regime, string structure,
			string exhaustion, string divergence,
			double s60, double s15, double s5, double s1, double s15s,
			double mtf, string why, string risks,
			double dataQuality, bool historicalLowQuality,
			string corrStatus = null)
		{
			var sb = new StringBuilder(1280);
			BuildInto(sb, flow, confidence, signal, wyckoff, wyckoffPhase,
				aggression, delta, absorption, absorptionKind,
				imbalance, volumeScore, volumeLabel,
				vwapScore, vwapLabel, nq, es, rty,
				regime, structure, exhaustion, divergence,
				s60, s15, s5, s1, s15s, mtf, why, risks,
				dataQuality, historicalLowQuality, corrStatus);
			return sb.ToString();
		}

		/// <summary>
		/// Reuses caller StringBuilder to avoid per-tick allocations on the chart hot path.
		/// </summary>
		public static void BuildInto(
			StringBuilder sb,
			double flow, double confidence, string signal,
			double wyckoff, string wyckoffPhase,
			double aggression, double delta, double absorption, string absorptionKind,
			double imbalance, double volumeScore, string volumeLabel,
			double vwapScore, string vwapLabel,
			double nq, double es, double rty,
			string regime, string structure,
			string exhaustion, string divergence,
			double s60, double s15, double s5, double s1, double s15s,
			double mtf, string why, string risks,
			double dataQuality, bool historicalLowQuality,
			string corrStatus = null,
			string displaySignal = null)
		{
			if (sb == null)
				return;
			sb.Clear();
			string sigKey = string.IsNullOrEmpty(displaySignal) ? signal : displaySignal;
			string biasPt = ToPortugueseBias(sigKey);

			sb.AppendLine("MNQ INSTITUTIONAL FLOW");
			if (historicalLowQuality || dataQuality < 50)
				sb.AppendLine("DATA QUALITY: LOW (" + dataQuality.ToString("0") + ")");
			sb.AppendLine("================================");
			sb.Append(">>>  ").Append(biasPt).AppendLine("  <<<");
			sb.Append("SIGNAL       ").AppendLine(sigKey);
			sb.AppendLine("================================");
			sb.Append("FLOW SCORE        ").Append(Signed(flow)).AppendLine();
			sb.Append("CONFIDENCE         ").Append(confidence.ToString("0")).AppendLine();
			sb.Append("WYCKOFF            ").Append(Signed(wyckoff)).Append("  ").AppendLine(wyckoffPhase);
			sb.Append("AGGRESSION         ").Append(Signed(aggression)).AppendLine();
			sb.Append("DELTA              ").Append(Signed(delta)).AppendLine();
			sb.Append("ABSORPTION         ").Append(Signed(absorption)).Append("  ").AppendLine(absorptionKind);
			sb.Append("IMBALANCE          ").Append(Signed(imbalance)).AppendLine();
			sb.Append("VOLUME             ").Append(Signed(volumeScore)).Append("  ").AppendLine(volumeLabel);
			sb.Append("VWAP               ").Append(Signed(vwapScore)).Append("  ").AppendLine(vwapLabel);
			sb.Append("NQ CORRELATION     ").AppendLine(nq.ToString("+0.00;-0.00;0.00"));
			sb.Append("ES CORRELATION     ").AppendLine(es.ToString("+0.00;-0.00;0.00"));
			sb.Append("RTY CORRELATION    ").AppendLine(rty.ToString("+0.00;-0.00;0.00"));
			if (!string.IsNullOrEmpty(corrStatus))
				sb.Append("CORR SERIES        ").AppendLine(corrStatus);
			sb.Append("MARKET REGIME     ").AppendLine(regime);
			sb.Append("STRUCTURE         ").AppendLine(structure);
			sb.Append("EXHAUSTION         ").AppendLine(exhaustion);
			sb.Append("DIVERGENCE         ").AppendLine(divergence);
			sb.Append("MTF 60/15/5/1/15s ").Append(Signed(s60)).Append(' ').Append(Signed(s15)).Append(' ')
				.Append(Signed(s5)).Append(' ').Append(Signed(s1)).Append(' ').Append(Signed(s15s)).AppendLine();
			sb.Append("MULTI TF SCORE    ").AppendLine(Signed(mtf));
			sb.AppendLine("WHY?");
			sb.AppendLine(why);
			sb.AppendLine("RISKS:");
			sb.AppendLine(risks);
		}

		/// <summary>
		/// Builds only the colored header block (title + bias + SIGNAL) for a separate TextFixed draw.
		/// </summary>
		public static void BuildSignalHeaderInto(StringBuilder sb, string displaySignal, double dataQuality, bool historicalLowQuality)
		{
			if (sb == null)
				return;
			sb.Clear();
			string biasPt = ToPortugueseBias(displaySignal);
			sb.AppendLine("MNQ INSTITUTIONAL FLOW");
			if (historicalLowQuality || dataQuality < 50)
				sb.AppendLine("DATA QUALITY: LOW (" + dataQuality.ToString("0") + ")");
			sb.AppendLine("================================");
			sb.Append(">>>  ").Append(biasPt).AppendLine("  <<<");
			sb.Append("SIGNAL       ").AppendLine(displaySignal ?? "");
			sb.Append("================================");
		}

		/// <summary>
		/// Body-only dashboard (no SIGNAL header) so it can be drawn WhiteSmoke under the colored header.
		/// </summary>
		public static void BuildBodyInto(
			StringBuilder sb,
			double flow, double confidence,
			double wyckoff, string wyckoffPhase,
			double aggression, double delta, double absorption, string absorptionKind,
			double imbalance, double volumeScore, string volumeLabel,
			double vwapScore, string vwapLabel,
			double nq, double es, double rty,
			string regime, string structure,
			string exhaustion, string divergence,
			double s60, double s15, double s5, double s1, double s15s,
			double mtf, string why, string risks,
			string corrStatus,
			int headerLineCount)
		{
			if (sb == null)
				return;
			sb.Clear();
			// Leading blank lines so body stacks under the colored TextFixed header at TopLeft.
			for (int i = 0; i < headerLineCount; i++)
				sb.AppendLine();

			sb.Append("FLOW SCORE        ").Append(Signed(flow)).AppendLine();
			sb.Append("CONFIDENCE         ").Append(confidence.ToString("0")).AppendLine();
			sb.Append("WYCKOFF            ").Append(Signed(wyckoff)).Append("  ").AppendLine(wyckoffPhase);
			sb.Append("AGGRESSION         ").Append(Signed(aggression)).AppendLine();
			sb.Append("DELTA              ").Append(Signed(delta)).AppendLine();
			sb.Append("ABSORPTION         ").Append(Signed(absorption)).Append("  ").AppendLine(absorptionKind);
			sb.Append("IMBALANCE          ").Append(Signed(imbalance)).AppendLine();
			sb.Append("VOLUME             ").Append(Signed(volumeScore)).Append("  ").AppendLine(volumeLabel);
			sb.Append("VWAP               ").Append(Signed(vwapScore)).Append("  ").AppendLine(vwapLabel);
			sb.Append("NQ CORRELATION     ").AppendLine(nq.ToString("+0.00;-0.00;0.00"));
			sb.Append("ES CORRELATION     ").AppendLine(es.ToString("+0.00;-0.00;0.00"));
			sb.Append("RTY CORRELATION    ").AppendLine(rty.ToString("+0.00;-0.00;0.00"));
			if (!string.IsNullOrEmpty(corrStatus))
				sb.Append("CORR SERIES        ").AppendLine(corrStatus);
			sb.Append("MARKET REGIME     ").AppendLine(regime);
			sb.Append("STRUCTURE         ").AppendLine(structure);
			sb.Append("EXHAUSTION         ").AppendLine(exhaustion);
			sb.Append("DIVERGENCE         ").AppendLine(divergence);
			sb.Append("MTF 60/15/5/1/15s ").Append(Signed(s60)).Append(' ').Append(Signed(s15)).Append(' ')
				.Append(Signed(s5)).Append(' ').Append(Signed(s1)).Append(' ').Append(Signed(s15s)).AppendLine();
			sb.Append("MULTI TF SCORE    ").AppendLine(Signed(mtf));
			sb.AppendLine("WHY?");
			sb.AppendLine(why);
			sb.AppendLine("RISKS:");
			sb.AppendLine(risks);
		}

		public static string ToPortugueseBias(string signal)
		{
			if (string.IsNullOrEmpty(signal))
				return "NEUTRO";
			switch (signal.ToUpperInvariant())
			{
				case "STRONG_BUY": return "COMPRA FORTE";
				case "BUY": return "COMPRA";
				case "WEAK_BUY":
				case "BIAS_BUY": return "VIÉS COMPRA";
				case "STRONG_SELL": return "VENDA FORTE";
				case "SELL": return "VENDA";
				case "WEAK_SELL":
				case "BIAS_SELL": return "VIÉS VENDA";
				case "WAIT_CONFIRMATION": return "AGUARDAR";
				case "NO_TRADE": return "SEM TRADE";
				case "NEUTRAL": return "NEUTRO";
				default: return signal;
			}
		}

		public static string Signed(double v)
		{
			return v.ToString("+0;-0;0");
		}
	}
}
