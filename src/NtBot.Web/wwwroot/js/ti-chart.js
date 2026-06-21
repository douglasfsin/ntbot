window.tiChart = {
  _charts: {},

  _zoneColor(type) {
    const t = (type || '').toLowerCase();
    if (t.includes('orderblocksell') || t.includes('fvgsell') || t.includes('sell'))
      return { line: 'rgba(246,70,93,0.85)', fill: 'rgba(246,70,93,0.12)' };
    if (t.includes('orderblockbuy') || t.includes('fvgbuy') || t.includes('buy'))
      return { line: 'rgba(14,203,129,0.85)', fill: 'rgba(14,203,129,0.12)' };
    if (t.includes('strongsell') || t.includes('moderatesell'))
      return { line: 'rgba(246,70,93,0.7)', fill: 'rgba(246,70,93,0.08)' };
    return { line: 'rgba(59,130,246,0.75)', fill: 'rgba(59,130,246,0.08)' };
  },

  _addZoneBand(series, chart, zone, isSmc) {
    const low = Number(zone.priceLow ?? zone.PriceLow ?? 0);
    const high = Number(zone.priceHigh ?? zone.PriceHigh ?? low);
    if (low <= 0 && high <= 0) return;

    const type = zone.type ?? zone.Type ?? '';
    const label = zone.label ?? zone.Label ?? (isSmc ? 'SMC' : 'Zona');
    const colors = this._zoneColor(type);

    const top = Math.max(low, high);
    const bottom = Math.min(low, high);

    series.createPriceLine({
      price: bottom,
      color: colors.line,
      lineWidth: isSmc ? 1 : 1,
      lineStyle: isSmc ? 0 : 2,
      axisLabelVisible: true,
      title: label
    });

    if (top !== bottom) {
      series.createPriceLine({
        price: top,
        color: colors.line,
        lineWidth: 1,
        lineStyle: isSmc ? 0 : 2,
        axisLabelVisible: false
      });
    }
  },

  render(containerId, candles, zones, smcOverlays) {
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

    (smcOverlays || []).forEach(z => this._addZoneBand(series, chart, z, true));
    (zones || []).forEach(z => this._addZoneBand(series, chart, z, false));

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
