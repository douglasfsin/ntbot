using System;

namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class CircularBuffer
	{
		private readonly double[] _items;
		private int _count;
		private int _index;

		public CircularBuffer(int capacity)
		{
			if (capacity < 1)
				throw new ArgumentOutOfRangeException("capacity");
			_items = new double[capacity];
		}

		public int Capacity { get { return _items.Length; } }
		public int Count { get { return _count; } }

		public void Clear()
		{
			_count = 0;
			_index = 0;
			Array.Clear(_items, 0, _items.Length);
		}

		public void Add(double value)
		{
			_items[_index] = value;
			_index = (_index + 1) % _items.Length;
			if (_count < _items.Length)
				_count++;
		}

		public double Last()
		{
			if (_count == 0)
				return 0;
			int i = _index == 0 ? _items.Length - 1 : _index - 1;
			return _items[i];
		}

		public double Min()
		{
			if (_count == 0)
				return 0;
			double m = _items[OldestIndex(0)];
			for (int i = 1; i < _count; i++)
			{
				double v = _items[OldestIndex(i)];
				if (v < m) m = v;
			}
			return m;
		}

		public double Max()
		{
			if (_count == 0)
				return 0;
			double m = _items[OldestIndex(0)];
			for (int i = 1; i < _count; i++)
			{
				double v = _items[OldestIndex(i)];
				if (v > m) m = v;
			}
			return m;
		}

		public void CopyTo(double[] dest, out int count)
		{
			count = _count;
			for (int i = 0; i < _count; i++)
				dest[i] = _items[OldestIndex(i)];
		}

		private int OldestIndex(int sequential)
		{
			if (_count < _items.Length)
				return sequential;
			return (_index + sequential) % _items.Length;
		}
	}
}
