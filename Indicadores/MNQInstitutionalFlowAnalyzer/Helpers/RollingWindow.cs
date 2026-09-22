namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	/// <summary>Incremental rolling mean / variance (Welford) plus a circular sample window.</summary>
	public sealed class RollingWindow
	{
		private readonly CircularBuffer _buffer;
		private readonly double[] _scratch;

		public RollingWindow(int capacity)
		{
			_buffer = new CircularBuffer(capacity);
			_scratch = new double[capacity];
		}

		public int Count { get { return _buffer.Count; } }

		public void Clear()
		{
			_buffer.Clear();
		}

		public void Add(double value)
		{
			_buffer.Add(value);
		}

		public double Last()
		{
			return _buffer.Last();
		}

		public double Mean()
		{
			_buffer.CopyTo(_scratch, out int n);
			return FlowMath.Mean(_scratch, n);
		}

		public double StdDev()
		{
			_buffer.CopyTo(_scratch, out int n);
			return FlowMath.StdDev(_scratch, n, true);
		}

		public double ZScore(double value)
		{
			_buffer.CopyTo(_scratch, out int n);
			return FlowMath.ZScore(value, _scratch, n);
		}

		public double Min() { return _buffer.Min(); }
		public double Max() { return _buffer.Max(); }

		public void CopyTo(double[] dest, out int count)
		{
			_buffer.CopyTo(dest, out count);
		}
	}
}
