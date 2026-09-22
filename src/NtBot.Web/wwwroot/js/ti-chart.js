window.tiChart = {
  _charts: {},

  /** Set true in DevTools (`tiChart.DEBUG_CHART_OVERLAYS = true`) to log band geometry. */
  DEBUG_CHART_OVERLAYS: false,

  MAX_SMC_OVERLAYS: 6,
  MAX_OP_ZONES: 5,

  _isXau(symbol) {
    const s = String(symbol || '').toUpperCase();
    return s === 'XAUUSD' || s === 'XAU' || s === 'GOLD' || s.includes('XAU');
  },

  _formatPrice(price, symbol) {
    const n = Number(price);
    if (!Number.isFinite(n)) return '';
    if (this._isXau(symbol)) return n.toFixed(2);
    if (Math.abs(n) >= 1000) return Math.round(n).toLocaleString('pt-BR');
    return n.toFixed(2);
  },

  _zoneMid(zone) {
    const low = Number(zone.priceLow ?? zone.PriceLow ?? 0);
    const high = Number(zone.priceHigh ?? zone.PriceHigh ?? low);
    return (low + high) / 2;
  },

  _zoneHeight(zone) {
    const low = Number(zone.priceLow ?? zone.PriceLow ?? 0);
    const high = Number(zone.priceHigh ?? zone.PriceHigh ?? low);
    return Math.abs(high - low);
  },

  _typeKey(type) {
    return String(type || '').toLowerCase();
  },

  _zoneColor(type, label) {
    const t = this._typeKey(type);
    const l = String(label || '').toLowerCase();

    if (t.includes('orderblocksell') || t.includes('fvgsell') || t === 'strongsell' || t === 'moderatesell'
        || (t.includes('sell') && !t.includes('buy')))
      return { line: 'rgba(246,70,93,0.92)', fill: 'rgba(246,70,93,0.16)' };

    if (t.includes('orderblockbuy') || t.includes('fvgbuy') || t === 'strongbuy' || t === 'moderatebuy'
        || t.includes('buy'))
      return { line: 'rgba(14,203,129,0.92)', fill: 'rgba(14,203,129,0.16)' };

    if (t.includes('premium'))
      return { line: 'rgba(246,70,93,0.75)', fill: 'rgba(246,70,93,0.09)' };

    if (t.includes('discount'))
      return { line: 'rgba(14,203,129,0.75)', fill: 'rgba(14,203,129,0.09)' };

    if (t.includes('ote') || l.includes('ote'))
      return { line: 'rgba(245,158,11,0.9)', fill: 'rgba(245,158,11,0.14)' };

    if (l.includes('poc') || l.includes('value area'))
      return { line: 'rgba(234,179,8,0.88)', fill: 'rgba(234,179,8,0.12)' };

    if (l.includes('vwap'))
      return { line: 'rgba(56,189,248,0.88)', fill: 'rgba(56,189,248,0.10)' };

    if (l.includes('liquidez') || l.includes('liquidity'))
      return { line: 'rgba(168,85,247,0.85)', fill: 'rgba(168,85,247,0.10)' };

    if (l.includes('target'))
      return { line: 'rgba(14,203,129,0.7)', fill: 'rgba(14,203,129,0.08)' };

    return { line: 'rgba(148,163,184,0.75)', fill: 'rgba(148,163,184,0.08)' };
  },

  _shortLabel(zone, symbol) {
    const raw = String(zone.label ?? zone.Label ?? '').trim();
    const low = Number(zone.priceLow ?? zone.PriceLow ?? 0);
    const high = Number(zone.priceHigh ?? zone.PriceHigh ?? low);
    const lo = this._formatPrice(Math.min(low, high), symbol);
    const hi = this._formatPrice(Math.max(low, high), symbol);
    const range = lo && hi && lo !== hi ? ` ${lo}–${hi}` : (lo ? ` ${lo}` : '');

    let name = raw;
    if (/demand/i.test(raw)) name = 'Demand';
    else if (/supply/i.test(raw)) name = 'Supply';
    else if (/fvg\s*\/\s*conflu/i.test(raw)) name = raw.replace(/FVG\s*\/\s*Confluência/i, 'FVG').slice(0, 18);
    else if (/poc/i.test(raw)) name = 'POC';
    else if (/vwap/i.test(raw)) name = 'VWAP';
    else if (/liquidez.*topo/i.test(raw)) name = 'Liq ↑';
    else if (/liquidez.*fundo/i.test(raw)) name = 'Liq ↓';
    else if (/target/i.test(raw)) name = 'Target';
    else if (raw.length > 16) name = raw.slice(0, 14) + '…';

    return (name + range).trim();
  },

  _smcPriority(zone) {
    const t = this._typeKey(zone.type ?? zone.Type);
    const l = String(zone.label ?? zone.Label ?? '').toLowerCase();
    if (t.includes('ote') || l.includes('ote')) return 100;
    if (t.includes('premium') || t.includes('discount')) return 90;
    if (t.includes('fvg')) return 80;
    if (t.includes('orderblock')) return 70;
    return 40;
  },

  _opPriority(zone) {
    const l = String(zone.label ?? zone.Label ?? '').toLowerCase();
    const score = Number(zone.confluenceScore ?? zone.ConfluenceScore ?? 0);
    let base = 0;
    if (l.includes('fvg') || l.includes('demand') || l.includes('supply')) base = 50;
    else if (l.includes('poc')) base = 40;
    else if (l.includes('target')) base = 30;
    else if (l.includes('liquidez') || l.includes('liquidity')) base = 20;
    else if (l.includes('vwap')) base = 15;
    return base + score;
  },

  _overlaps(a, b, ratio) {
    const aLow = Math.min(Number(a.priceLow ?? a.PriceLow ?? 0), Number(a.priceHigh ?? a.PriceHigh ?? 0));
    const aHigh = Math.max(Number(a.priceLow ?? a.PriceLow ?? 0), Number(a.priceHigh ?? a.PriceHigh ?? 0));
    const bLow = Math.min(Number(b.priceLow ?? b.PriceLow ?? 0), Number(b.priceHigh ?? b.PriceHigh ?? 0));
    const bHigh = Math.max(Number(b.priceLow ?? b.PriceLow ?? 0), Number(b.priceHigh ?? b.PriceHigh ?? 0));
    const oLow = Math.max(aLow, bLow);
    const oHigh = Math.min(aHigh, bHigh);
    if (oHigh <= oLow) return false;
    const smaller = Math.min(aHigh - aLow, bHigh - bLow);
    if (smaller <= 0) return false;
    return (oHigh - oLow) / smaller >= ratio;
  },

  _pickTop(zones, max, lastPrice, priorityFn) {
    const list = (zones || []).slice();
    list.sort((a, b) => {
      const pa = priorityFn(a);
      const pb = priorityFn(b);
      if (pb !== pa) return pb - pa;
      if (lastPrice > 0) {
        const da = Math.abs(this._zoneMid(a) - lastPrice);
        const db = Math.abs(this._zoneMid(b) - lastPrice);
        return da - db;
      }
      return 0;
    });

    const picked = [];
    for (const z of list) {
      if (!(this._zoneHeight(z) >= 0) && Number(z.priceLow ?? z.PriceLow) <= 0) continue;
      if (picked.some(p => this._overlaps(p, z, 0.6))) continue;
      picked.push(z);
      if (picked.length >= max) break;
    }
    return picked;
  },

  /**
   * Draw a price band fill strictly between `bottom` and `top`.
   * Lightweight Charts AreaSeries fills to the pane floor (no baseValue) — that was the
   * full-chart translucent rectangle bug. BaselineSeries with baseValue.price = bottom
   * confines the fill to the zone's own price range.
   */
  _addZoneBand(series, chart, zone, opts) {
    const options = opts || {};
    const low = Number(zone.priceLow ?? zone.PriceLow ?? 0);
    const high = Number(zone.priceHigh ?? zone.PriceHigh ?? low);
    if (!(low > 0 || high > 0)) return;

    const type = zone.type ?? zone.Type ?? '';
    const typeLower = this._typeKey(type);
    const rawLabel = zone.label ?? zone.Label ?? (options.isSmc ? 'SMC' : 'Zona');
    const symbol = options.symbol || '';
    const label = options.axisTitle || this._shortLabel(zone, symbol);
    const colors = this._zoneColor(type, rawLabel);

    const top = Math.max(low, high);
    const bottom = Math.min(low, high);
    const bandHeight = top - bottom;
    const candles = options.candles;
    const fillBand = options.fillBand !== false
      && candles
      && candles.length >= 2
      && bandHeight > 0;

    if (fillBand) {
      const first = candles[0].time ?? candles[0].Time;
      const last = candles[candles.length - 1].time ?? candles[candles.length - 1].Time;

      if (this.DEBUG_CHART_OVERLAYS) {
        console.debug('[tiChart] zone band', {
          name: label,
          type,
          origin: 'BaselineSeries+baseValue',
          timeStart: first,
          timeEnd: last,
          yTop: top,
          yBottom: bottom,
          h: bandHeight,
          fill: colors.fill
        });
      }

      const fillSeries = chart.addBaselineSeries({
        baseValue: { type: 'price', price: bottom },
        topLineColor: 'transparent',
        topFillColor1: colors.fill,
        topFillColor2: colors.fill,
        bottomLineColor: 'transparent',
        bottomFillColor1: 'transparent',
        bottomFillColor2: 'transparent',
        lineWidth: 1,
        lineVisible: false,
        priceLineVisible: false,
        lastValueVisible: false,
        crosshairMarkerVisible: false
      });
      fillSeries.setData([
        { time: first, value: top },
        { time: last, value: top }
      ]);
    } else if (this.DEBUG_CHART_OVERLAYS) {
      console.debug('[tiChart] zone lines only', { name: label, type, yTop: top, yBottom: bottom });
    }

    const isFvg = typeLower.includes('fvg');
    const isPoc = /poc|vwap/i.test(String(rawLabel));
    const lineWidth = options.isSmc ? 2 : (isPoc ? 2 : 1);
    const lineStyle = isFvg ? 2 : (isPoc ? 1 : 0); // 0 solid, 1 dotted, 2 dashed

    series.createPriceLine({
      price: bottom,
      color: colors.line,
      lineWidth,
      lineStyle,
      axisLabelVisible: true,
      title: label
    });

    if (top !== bottom) {
      series.createPriceLine({
        price: top,
        color: colors.line,
        lineWidth,
        lineStyle,
        axisLabelVisible: false,
        title: ''
      });
    }
  },

  render(containerId, candles, zones, smcOverlays, height, meta) {
    const container = document.getElementById(containerId);
    if (!container || !window.LightweightCharts) return;

    const chartHeight = Number(height) > 0 ? Number(height) : 320;
    const symbol = (meta && meta.symbol) || '';
    const lastPrice = Number(
      (meta && meta.lastPrice)
      || (candles && candles.length ? (candles[candles.length - 1].close ?? candles[candles.length - 1].Close) : 0)
    ) || 0;

    if (this._charts[containerId]) {
      const prev = this._charts[containerId];
      const chart = prev.chart ?? prev;
      if (chart && typeof chart.remove === 'function') chart.remove();
      delete this._charts[containerId];
    }

    container.innerHTML = '';

    const chart = LightweightCharts.createChart(container, {
      width: container.clientWidth,
      height: chartHeight,
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

    const smcPicked = this._pickTop(smcOverlays, this.MAX_SMC_OVERLAYS, lastPrice, z => this._smcPriority(z));
    const opPicked = this._pickTop(zones, this.MAX_OP_ZONES, lastPrice, z => this._opPriority(z));

    if (this.DEBUG_CHART_OVERLAYS) {
      console.debug('[tiChart] render', {
        symbol,
        lastPrice,
        candles: data.length,
        smcIn: (smcOverlays || []).length,
        smcOut: smcPicked.length,
        zonesIn: (zones || []).length,
        zonesOut: opPicked.length
      });
    }

    // SMC first (structure), then operational zones on top for operator focus.
    smcPicked.forEach(z => this._addZoneBand(series, chart, z, {
      isSmc: true,
      fillBand: true,
      candles: data,
      symbol
    }));
    opPicked.forEach(z => this._addZoneBand(series, chart, z, {
      isSmc: false,
      fillBand: true,
      candles: data,
      symbol
    }));

    chart.timeScale().fitContent();
    this._charts[containerId] = { chart, series, lastBar: data.length ? { ...data[data.length - 1] } : null };

    const ro = new ResizeObserver(() => {
      chart.applyOptions({ width: container.clientWidth });
    });
    ro.observe(container);
  },

  /**
   * Updates the forming candle close (and high/low) from a live price tick.
   * Does not redraw overlays — only mutates the last bar.
   */
  updateLastPrice(containerId, price, unixTimeSeconds) {
    const entry = this._charts[containerId];
    if (!entry?.series || !(Number(price) > 0)) return;

    const p = Number(price);
    let bar = entry.lastBar;
    if (!bar) {
      const t = Number(unixTimeSeconds);
      if (!(t > 0)) return;
      bar = { time: t, open: p, high: p, low: p, close: p };
    } else {
      bar = {
        time: bar.time,
        open: bar.open,
        high: Math.max(bar.high, p),
        low: Math.min(bar.low, p),
        close: p
      };
    }

    entry.lastBar = bar;
    try {
      entry.series.update(bar);
    } catch (e) {
      console.debug('[tiChart] updateLastPrice failed', e);
    }
  },

  dispose(containerId) {
    const entry = this._charts[containerId];
    if (entry) {
      const chart = entry.chart ?? entry;
      if (chart && typeof chart.remove === 'function') chart.remove();
      delete this._charts[containerId];
    }
  }
};
