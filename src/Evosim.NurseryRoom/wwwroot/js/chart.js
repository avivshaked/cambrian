// SVG charts drawn by script to each element's own size, in the mockups' look. Paddings are in rem
// so they scale with the type. Nothing here knows what a series means.

import { NS, fmt } from './common.js';

export const el = (p, tag, a = {}, t) => {
  const e = document.createElementNS(NS, tag);
  for (const k in a) e.setAttribute(k, a[k]);
  if (t != null) e.textContent = t;
  p.appendChild(e);
  return e;
};

export const rem = () => parseFloat(getComputedStyle(document.documentElement).fontSize);

export function runMean(a, w = 10) {
  const out = [];
  for (let i = 0; i < a.length; i++) {
    let s = 0, n = 0;
    for (let j = Math.max(0, i - w + 1); j <= i; j++) if (a[j] != null) { s += a[j]; n++; }
    out.push(n ? s / n : null);
  }
  return out;
}

/**
 * Calls `redraw` (once per frame, however many boxes moved) whenever one of `elements` changes size. A chart is drawn to the size its box had when it was drawn; a box
 * is moved by its neighbours (a grid that reflows when another panel takes a row) as well as by the window, and only this hears of the first.
 */
export function watchBoxes(elements, redraw) {
  if (typeof ResizeObserver === 'undefined') return null;
  const last = new WeakMap();
  let queued = false;
  const ro = new ResizeObserver(entries => {
    let moved = false;
    for (const e of entries) {
      const r = e.contentRect, k = Math.round(r.width) + 'x' + Math.round(r.height);
      if (last.get(e.target) !== k) { last.set(e.target, k); moved = true; }
    }
    if (moved && !queued) { queued = true; requestAnimationFrame(() => { queued = false; redraw(); }); }
  });
  for (const e of elements) ro.observe(e);
  return ro;
}

/** Ticks at round numbers across [lo, hi]. */
export function niceTicks(lo, hi, count = 4) {
  if (!(hi > lo)) return [lo];
  const raw = (hi - lo) / count, mag = Math.pow(10, Math.floor(Math.log10(raw)));
  const step = [1, 2, 2.5, 5, 10].map(m => m * mag).find(s => s >= raw) || raw;
  const out = [];
  for (let v = Math.ceil(lo / step) * step; v <= hi + step * 1e-9; v += step) out.push(+v.toPrecision(12));
  return out;
}

export function extent(arrays, pad = 0.06) {
  let lo = Infinity, hi = -Infinity;
  for (const a of arrays) for (const v of a) if (v != null && Number.isFinite(v)) { lo = Math.min(lo, v); hi = Math.max(hi, v); }
  if (!Number.isFinite(lo)) return [0, 1];
  if (hi === lo) { hi = lo + (Math.abs(lo) || 1) * 0.5; lo = lo - (Math.abs(lo) || 1) * 0.5; }
  const d = (hi - lo) * pad;
  return [lo - d, hi + d];
}

/** Clears an svg and returns its axes: X(i) maps a point's index, Y(v) a value. */
export function frame(svg, { n, lo, hi, xvals, unit = '', padL = 3.3, padB = 1.5, padT = .5, padR = .5, xlab = true, ticks = 4 }) {
  svg.innerHTML = '';
  const R = rem(), w = svg.clientWidth, h = svg.clientHeight;
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  padL *= R; padB *= R; padT *= R; padR *= R;
  const X = i => padL + i * (w - padL - padR) / Math.max(n - 1, 1);
  const Y = v => padT + (hi - v) / (hi - lo) * (h - padT - padB);
  const yt = niceTicks(lo, hi, h / R < 9 ? 2 : ticks);
  for (const t of yt) {
    el(svg, 'line', { class: 'gridl', x1: padL, x2: w - padR, y1: Y(t), y2: Y(t) });
    el(svg, 'text', { x: padL - .45 * R, y: Y(t) + .25 * R, 'text-anchor': 'end' }, fmt(t, 3));
  }
  const ax = el(svg, 'g', { class: 'ax' });
  el(ax, 'line', { x1: padL, x2: padL, y1: padT, y2: h - padB });
  el(ax, 'line', { x1: padL, x2: w - padR, y1: h - padB, y2: h - padB });
  if (xlab && n > 0) {
    const idx = n > 2 ? [0, Math.round((n - 1) / 2), n - 1] : n === 2 ? [0, 1] : [0];
    for (const i of idx) {
      el(ax, 'line', { x1: X(i), x2: X(i), y1: h - padB, y2: h - padB + 3 });
      el(svg, 'text', { x: X(i), y: h - .2 * R, 'text-anchor': i === 0 ? 'start' : i === n - 1 && n > 1 ? 'end' : 'middle' }, xvals ? xvals[i] : i);
    }
  }
  if (unit) el(svg, 'text', { x: padL + .35 * R, y: padT + .75 * R, style: 'fill:var(--ink-dim)' }, unit);
  return { svg, X, Y, w, h, padL, padB, padR, padT, R, n };
}

/** Points as an svg polyline's string, breaking nothing: a missing value is skipped. */
export function pts(c, a) {
  const p = [];
  a.forEach((v, i) => { if (v != null && Number.isFinite(v)) p.push(c.X(i).toFixed(1) + ',' + c.Y(v).toFixed(1)); });
  return p.join(' ');
}

export function line(c, a, cls, style) {
  const p = pts(c, a);
  if (p) el(c.svg, 'polyline', { class: cls, points: p, ...(style ? { style } : {}) });
}

export function liveDot(c, a, r = .35) {
  for (let i = a.length - 1; i >= 0; i--)
    if (a[i] != null) { el(c.svg, 'circle', { class: 'live-dot', cx: c.X(i), cy: c.Y(a[i]), r: r * c.R }); return i; }
  return -1;
}

export function heroArea(c, a, base, id) {
  const p = pts(c, a);
  if (!p) return;
  const first = a.findIndex(v => v != null), last = a.length - 1 - [...a].reverse().findIndex(v => v != null);
  const d = el(c.svg, 'defs'), g = el(d, 'linearGradient', { id, x1: 0, y1: 0, x2: 0, y2: 1 });
  el(g, 'stop', { offset: '0%', style: 'stop-color:var(--run);stop-opacity:.18' });
  el(g, 'stop', { offset: '100%', style: 'stop-color:var(--run);stop-opacity:0' });
  el(c.svg, 'polygon', { points: p + ` ${c.X(last)},${c.Y(base)} ${c.X(first)},${c.Y(base)}`, fill: `url(#${id})` });
}

/** A small plain-line chart of one or more series (the first is the strong one). */
export function small(svg, series, { xvals, padL = 2.6, padB = 1.1 } = {}) {
  const n = Math.max(...series.map(s => s.length), 0);
  const [lo, hi] = extent(series);
  const c = frame(svg, { n, lo, hi, xvals, padL, padB, ticks: 2 });
  series.slice().reverse().forEach((s, k, all) => line(c, s, k === all.length - 1 ? 'line' : 'line-lo'));
  return c;
}

/** The standard error of the mean over a window of the series (the last `w` values up to each point); 0 where fewer than two values lie in it. */
export function runSe(a, w = 10) {
  const out = [];
  for (let i = 0; i < a.length; i++) {
    const v = [];
    for (let j = Math.max(0, i - w + 1); j <= i; j++) if (a[j] != null && Number.isFinite(a[j])) v.push(a[j]);
    if (v.length < 2) { out.push(0); continue; }
    const m = v.reduce((s, x) => s + x, 0) / v.length;
    out.push(Math.sqrt(v.reduce((s, x) => s + (x - m) ** 2, 0) / (v.length - 1) / v.length));
  }
  return out;
}

/** A ghost band: the mean ± its standard error, one polygon for each unbroken stretch of the mean. */
export function band(c, mean, se, cls = 'ctl-band') {
  let from = -1;
  const flush = to => {
    if (from < 0) return;
    const top = [], bottom = [];
    for (let i = from; i <= to; i++) { top.push(c.X(i).toFixed(1) + ',' + c.Y(mean[i] + se[i]).toFixed(1)); bottom.unshift(c.X(i).toFixed(1) + ',' + c.Y(mean[i] - se[i]).toFixed(1)); }
    el(c.svg, 'polygon', { class: cls, points: top.concat(bottom).join(' ') });
    from = -1;
  };
  for (let i = 0; i < mean.length; i++) {
    if (mean[i] == null || !Number.isFinite(mean[i])) flush(i - 1);
    else if (from < 0) from = i;
  }
  flush(mean.length - 1);
}
