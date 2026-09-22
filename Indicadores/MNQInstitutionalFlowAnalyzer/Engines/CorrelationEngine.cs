namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class CorrelationEngine
	{
		private readonly RollingWindow _mnq;
		private readonly RollingWindow _nq;
		private readonly RollingWindow _es;
		private readonly RollingWindow _rty;
		private readonly RollingWindow _ym;
		private readonly double[] _a;
		private readonly double[] _b;
		private readonly int _period;
		private double _prevMnq;
		private double _prevNq;
		private double _prevEs;
		private double _prevRty;
		private double _prevYm;

		public double NqPearson;
		public double EsPearson;
		public double RtyPearson;
		public double YmPearson;
		public double EsCrossMarketScore;
		public double RtyCorrelationScore;
		public double LeadershipScore;
		public double CombinedScore;
		public bool HasNq;
		public bool HasEs;
		public bool HasRty;
		public bool HasYm;

		public CorrelationEngine(int period)
		{
			_period = period < 5 ? 50 : period;
			_mnq = new RollingWindow(_period);
			_nq = new RollingWindow(_period);
			_es = new RollingWindow(_period);
			_rty = new RollingWindow(_period);
			_ym = new RollingWindow(_period);
			_a = new double[_period];
			_b = new double[_period];
		}

		public void Push(double mnqClose, double nqClose, double esClose, double rtyClose, double ymClose,
			bool nqOn, bool esOn, bool rtyOn, bool ymOn)
		{
			HasNq = nqOn && nqClose > 0;
			HasEs = esOn && esClose > 0;
			HasRty = rtyOn && rtyClose > 0;
			HasYm = ymOn && ymClose > 0;

			if (_prevMnq > 0)
				_mnq.Add(FlowMath.PercentReturn(_prevMnq, mnqClose));
			_prevMnq = mnqClose;

			if (HasNq)
			{
				if (_prevNq > 0) _nq.Add(FlowMath.PercentReturn(_prevNq, nqClose));
				_prevNq = nqClose;
			}
			if (HasEs)
			{
				if (_prevEs > 0) _es.Add(FlowMath.PercentReturn(_prevEs, esClose));
				_prevEs = esClose;
			}
			if (HasRty)
			{
				if (_prevRty > 0) _rty.Add(FlowMath.PercentReturn(_prevRty, rtyClose));
				_prevRty = rtyClose;
			}
			if (HasYm)
			{
				if (_prevYm > 0) _ym.Add(FlowMath.PercentReturn(_prevYm, ymClose));
				_prevYm = ymClose;
			}

			NqPearson = Pair(_mnq, _nq);
			EsPearson = Pair(_mnq, _es);
			RtyPearson = Pair(_mnq, _rty);
			YmPearson = Pair(_mnq, _ym);

			double mnqRet = _mnq.Last();
			double nqRet = HasNq ? _nq.Last() : 0;
			double esRet = HasEs ? _es.Last() : 0;
			double rtyRet = HasRty ? _rty.Last() : 0;

			if (HasEs)
			{
				if (mnqRet > 0 && esRet > 0) EsCrossMarketScore = FlowMath.ClampScore(40 + EsPearson * 50);
				else if (mnqRet < 0 && esRet < 0) EsCrossMarketScore = FlowMath.ClampScore(-40 + EsPearson * 50);
				else EsCrossMarketScore = FlowMath.ClampScore(-EsPearson * 40 + (mnqRet - esRet) * 5000);
			}
			else EsCrossMarketScore = 0;

			if (HasRty)
			{
				bool riskOn = mnqRet > 0 && rtyRet > 0;
				RtyCorrelationScore = FlowMath.ClampScore((riskOn ? 1 : (mnqRet < 0 && rtyRet < 0 ? -1 : 0)) * 40 + RtyPearson * 40);
			}
			else RtyCorrelationScore = 0;

			LeadershipScore = FlowMath.LeadershipScore(mnqRet, nqRet, esRet, rtyRet, HasNq, HasEs, HasRty);

			int n = 0;
			double s = 0;
			if (HasNq) { s += NqPearson * 40 + (mnqRet * nqRet > 0 ? 20 : -20); n++; }
			if (HasEs) { s += EsCrossMarketScore; n++; }
			if (HasRty) { s += RtyCorrelationScore; n++; }
			CombinedScore = n == 0 ? 0 : FlowMath.ClampScore(s / n);
		}

		private double Pair(RollingWindow a, RollingWindow b)
		{
			if (a.Count < 5 || b.Count < 5)
				return 0;
			a.CopyTo(_a, out int na);
			b.CopyTo(_b, out int nb);
			int n = na < nb ? na : nb;
			return FlowMath.Pearson(_a, _b, n);
		}
	}
}
