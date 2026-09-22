namespace NinjaTrader.NinjaScript.Indicators.NtBot
{
	public sealed class VolumeEngine
	{
		private readonly RollingWindow _volumes;

		public VolumeEngine(int lookback)
		{
			_volumes = new RollingWindow(lookback);
		}

		public double RelativeVolume;
		public double VolumeZScore;
		public double VolumeScore;
		public string VolumeLabel;

		public void PushBarVolume(double volume)
		{
			if (_volumes.Count > 0)
			{
				double avg = _volumes.Mean();
				RelativeVolume = avg <= 0 ? 1 : volume / avg;
				VolumeZScore = _volumes.ZScore(volume);
			}
			else
			{
				RelativeVolume = 1;
				VolumeZScore = 0;
			}
			_volumes.Add(volume);
			VolumeScore = FlowMath.VolumeZToScore(VolumeZScore);
			if (RelativeVolume >= 2.0) VolumeLabel = "EXTREME";
			else if (RelativeVolume >= 1.4) VolumeLabel = "HIGH";
			else VolumeLabel = "NORMAL";
		}

		public double Average { get { return _volumes.Mean(); } }
	}
}
