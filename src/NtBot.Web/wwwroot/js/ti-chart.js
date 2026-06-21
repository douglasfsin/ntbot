window.tiChart = {
  _charts: {},

  render(containerId, candles, zones) {
    const container = document.getElementById(containerId);
    if (!container || !window.LightweightCharts) return;

    if (this._charts[containerId]) {
      this._charts[containerId].remove();
      delete this._charts[containerId];
    }

    container.innerHTML = '';

    const chart = LightweightCharts.createChart(container, {
      width: container.clientWidth,
      height: 160,
      layout: {
        background: { color: 'transparent' },
        textColor: '#848e9c'
      },
      grid: {
        vertLines: { color: 'rgba(255,255,255,0.04)' },
        horzLines: { color: 'rgba(255,255,255,0.04)' }
      },
      timeScale: { borderColor: 'rgba(255,255,255,0.08)', timeVisible: true },
      rightPriceScale: { borderColor: 'rgba(255,255,255,0.08)' }
    });

    const series = chart.addCandlestickSeries({
      upColor: '#0ecb81',
      downColor: '#f6465d',
      borderVisible: false,
      wickUpColor: '#0ecb81',
      wickDownColor: '#f6465d'
    });

    const data = (candles || []).map(c => ({
      time: c.time ?? c.Time,
      open: Number(c.open ?? c.Open),
      high: Number(c.high ?? c.High),
      low: Number(c.low ?? c.Low),
      close: Number(c.close ?? c.Close)
    }));
    series.setData(data);

    (zones || []).forEach(z => {
      if (z.priceLow > 0) {
        series.createPriceLine({
          price: Number(z.priceLow),
          color: z.type?.includes('Sell') ? 'rgba(246,70,93,0.7)' : 'rgba(14,203,129,0.7)',
          lineWidth: 1,
          lineStyle: 2,
          axisLabelVisible: true,
          title: z.label || 'Zona'
        });
      }
      if (z.priceHigh > 0 && z.priceHigh !== z.priceLow) {
        series.createPriceLine({
          price: Number(z.priceHigh),
          color: 'rgba(148,163,184,0.5)',
          lineWidth: 1,
          lineStyle: 2,
          axisLabelVisible: false
        });
      }
    });

    chart.timeScale().fitContent();
    this._charts[containerId] = chart;

    const ro = new ResizeObserver(() => {
      chart.applyOptions({ width: container.clientWidth });
    });
    ro.observe(container);
  },

  dispose(containerId) {
    if (this._charts[containerId]) {
      this._charts[containerId].remove();
      delete this._charts[containerId];
    }
  }
};
