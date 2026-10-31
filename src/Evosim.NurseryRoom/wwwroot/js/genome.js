// The genome screen: one genome of a run, as the room computed it from the genome with Core's own code
// (nursery://genomes/{run}, nursery://genome/{run}/{id}). The body is drawn from what development grew (positions,
// half-extents, orientations), never from the genome's graph; the genome's graph is drawn from its own nodes and edges;
// the brain from its ports, hidden units and links.
// Nothing about any experiment is named here: every name on screen is read from the data.

import { esc, getResource, onResize, poll, fmt, noData, mountMachine, getSel, setSel, refuse } from './common.js';
import { el, rem } from './chart.js';

const $ = id => document.getElementById(id);
const params = new URLSearchParams(location.search);
let runId = params.get('run');
let wanted = params.get('id');
let gone = false;                          // the screen has said it cannot show what was asked for
let wantedFrom = wanted ? 'url' : null;   // 'kept' when the genome came from what this browser last looked at
let list = null, g = null, listVersion = null, shownKey = null;
let showUnlinked = false, selected = null, pollSeconds = 30;
let filter = 'all', threshold = 0.2;   // the brain's link filter: all | changed | weight (|w| above the threshold)

// One colour a cell type, in the order the body shows them. Never the only carrier: parts are numbered, and a legend names each type.
const PALETTE = ['#5ef0ff', '#ffb86b', '#a08cff', '#e6cf86', '#8fe388', '#ff7eb6', '#9fb4ff'];

const href = (run, id) => `genome.html?run=${encodeURIComponent(run)}&id=${encodeURIComponent(id)}`;

// ---------------------------------------------------------------- the band and the picker

function genomeLabel(x) {
  const bits = [];
  if (x.generation != null && x.kinds.includes('top')) bits.push('gen ' + x.generation);
  if (x.kinds.includes('exam')) bits.push('exam');
  if (x.body != null) bits.push('body ' + x.body);
  if (x.score != null) bits.push('score ' + fmt(x.score, 4));
  return x.id + (bits.length ? ' · ' + bits.join(' · ') : '');
}

function fillPicker(lst) {
  const sel = $('pick');
  const tops = lst.genomes.filter(x => x.kinds.includes('top'));
  const exam = lst.genomes.filter(x => !x.kinds.includes('top') && x.kinds.includes('exam'));
  const opt = x => `<option value="${esc(x.id)}">${esc(genomeLabel(x))}</option>`;
  sel.innerHTML = (tops.length ? `<optgroup label="the top genome of each generation">${tops.map(opt).join('')}</optgroup>` : '') +
    (exam.length ? `<optgroup label="the exam's other genomes">${exam.map(opt).join('')}</optgroup>` : '');
  if (!lst.genomes.length) sel.innerHTML = '<option value="">no genome kept</option>';
}

function step(by) {
  if (!list) return;
  const i = list.genomes.findIndex(x => x.id === (g ? g.id : wanted));
  const j = Math.min(Math.max(i + by, 0), list.genomes.length - 1);
  if (list.genomes[j]) choose(list.genomes[j].id);
}

function choose(id) {
  wanted = id;
  history.replaceState(null, '', href(runId, id));
  tick();
}

function drawBand(lst, d) {
  $('crumb').innerHTML = `<b>${esc(runId)}</b> · ${esc(d ? d.id : wanted || '')}`;
  setSel({ run: runId, genome: d ? d.id : wanted });   // the tabs carry the run and the genome looked at
  document.title = `Nursery room · genome ${d ? d.id : ''} of ${runId}`;
  const i = lst.genomes.findIndex(x => x.id === (d ? d.id : wanted));
  $('prev').disabled = i <= 0;
  $('next').disabled = i < 0 || i >= lst.genomes.length - 1;
  if (d) $('pick').value = d.id;
  setExport(d);
  setSwim(d);
  if (!d) { $('name').innerHTML = `<small>${esc(runId)}</small>no genome`; $('desc').textContent = ''; $('stats').innerHTML = ''; $('state').innerHTML = ''; return; }
  $('name').innerHTML = `<small>${esc(runId)}</small>${esc(d.id)}`;
  const what = [];
  if (d.kinds.includes('top') && d.generation != null) what.push(`top genome of generation ${d.generation}`);
  if (d.kinds.includes('exam')) what.push(`exam genome${d.examIndex != null ? ' ' + d.examIndex : ''}`);
  $('desc').textContent = what.join(' · ') + (d.aliases && d.aliases.length ? ' · also ' + d.aliases.join(', ') : '');
  $('desc').title = d.source || '';
  const plan = d.bodyPlan, br = d.brain;
  const stat = (k, v) => `<div class="stat"><span class="k">${esc(k)}</span><span class="v sm">${v}</span></div>`;
  const parts = [];
  if (plan) { parts.push(stat('parts', plan.partCount), stat('joints', plan.joints), stat('degrees of freedom', plan.dof)); }
  if (br && br.model === 'central') parts.push(stat('hidden units', br.counts.hidden), stat('links', br.counts.links));
  if (d.score != null) parts.push(stat('score', fmt(d.score, 4)));
  $('stats').innerHTML = parts.join('');
  $('state').innerHTML = d.kinds.map(k => `<span class="badge kind">${esc(k)}</span>`).join('') +
    (d.development && d.development.truncated ? ' <span class="badge kind" title="development stopped early at the limits">truncated by limits</span>' : '');
}

/**
 * "Swim it in the tank" is a link to the tank screen, enabled for an exam genome: the screen opens on the first draw's tank that holds it (its
 * number in the exam is its line in the genomes file). A genome that is not one of the exam's is not dealt into any tank, so there is nothing to
 * open: the link is drawn off, with the reason in its title.
 */
function setSwim(d) {
  const a = $('swim');
  if (!a) return;
  if (!d || !runId || !d.kinds.includes('exam') || d.examIndex == null) {
    a.removeAttribute('href'); a.setAttribute('aria-disabled', 'true'); a.classList.add('btn--off');
    a.title = "only one of the exam's genomes can be swum in the tank: the exam's draws deal tanks of them";
    return;
  }
  a.href = `tank.html?run=${encodeURIComponent(runId)}&genome=${d.examIndex}`;
  a.removeAttribute('aria-disabled'); a.classList.remove('btn--off');
  a.title = `open the tank screen on the exam tank that holds exam genome ${d.examIndex}: re-step the episode and watch it`;
}

/** The export button is a plain link to the room's read of the genome's text: a read, never an operation. */
function setExport(d) {
  const a = $('export');
  if (!d || !runId) { a.removeAttribute('href'); a.removeAttribute('download'); a.setAttribute('aria-disabled', 'true'); a.classList.add('btn--off'); return; }
  a.href = `/api/genome-file?run=${encodeURIComponent(runId)}&id=${encodeURIComponent(d.id)}`;
  a.setAttribute('download', `${runId}-${d.id}.json`.replace(/[^A-Za-z0-9._-]/g, '_'));
  a.removeAttribute('aria-disabled');
  a.classList.remove('btn--off');
}

// ---------------------------------------------------------------- the body, from above and from the side

/** Rotates v = [x, y, z] by the unit quaternion q = {x, y, z, w}. */
function rot(q, v) {
  const tx = 2 * (q.y * v[2] - q.z * v[1]), ty = 2 * (q.z * v[0] - q.x * v[2]), tz = 2 * (q.x * v[1] - q.y * v[0]);
  return [v[0] + q.w * tx + (q.y * tz - q.z * ty), v[1] + q.w * ty + (q.z * tx - q.x * tz), v[2] + q.w * tz + (q.x * ty - q.y * tx)];
}

const V = o => [o.x, o.y, o.z];
const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];

/** What a part occupies, in the creature's frame: a box's eight corners, a sphere's centre and radius, a capsule's spine and radius. */
function solid(p) {
  const c = V(p.position), h = V(p.halfExtents);
  if (p.shape === 'sphere') return { kind: 'ball', c, r: p.radius };
  if (p.shape === 'capsule') {
    const a = add(c, rot(p.rotation, [0, -p.halfSpan, 0])), b = add(c, rot(p.rotation, [0, p.halfSpan, 0]));
    return { kind: 'cap', a, b, c, r: p.radius };
  }
  const corners = [];
  for (const sx of [-1, 1]) for (const sy of [-1, 1]) for (const sz of [-1, 1]) corners.push(add(c, rot(p.rotation, [sx * h[0], sy * h[1], sz * h[2]])));
  return { kind: 'box', corners, c };
}

function hull(points) {
  const p = points.slice().sort((a, b) => a[0] - b[0] || a[1] - b[1]);
  if (p.length < 3) return p;
  const cross = (o, a, b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
  const lo = [], up = [];
  for (const q of p) { while (lo.length >= 2 && cross(lo[lo.length - 2], lo[lo.length - 1], q) <= 0) lo.pop(); lo.push(q); }
  for (const q of p.slice().reverse()) { while (up.length >= 2 && cross(up[up.length - 2], up[up.length - 1], q) <= 0) up.pop(); up.push(q); }
  return lo.slice(0, -1).concat(up.slice(0, -1));
}

// Parts are drawn farthest first. Seen from above (looking down the y axis) the higher part is nearer; seen along z, the part with the larger z is.
const VIEWS = {
  top:  { to: v => [v[0], v[2]], depth: v => v[1] },     // x across, z down the page
  side: { to: v => [v[0], -v[1]], depth: v => v[2] },    // x across, y up
};

function niceLength(maxMetres) {
  for (const s of [1, .5, .2, .1, .05, .02, .01, .005, .002, .001]) if (s <= maxMetres) return s;
  return maxMetres;
}

/** The outline of one part's projection as an svg element's tag and attributes, given the projection P (metres to pixels) and k (pixels per metre). */
function shapeOf(s, P, k) {
  const f = n => n.toFixed(1);
  if (s.kind === 'box') return ['polygon', { points: hull(s.corners.map(P)).map(q => f(q[0]) + ',' + f(q[1])).join(' ') }];
  if (s.kind === 'ball') { const c = P(s.c); return ['circle', { cx: c[0], cy: c[1], r: s.r * k }]; }
  const a = P(s.a), b = P(s.b), r = s.r * k, L = Math.hypot(b[0] - a[0], b[1] - a[1]);
  // a capsule end-on is a disc
  if (L < .5) { const c = P(s.c); return ['circle', { cx: c[0], cy: c[1], r }]; }
  const nx = -(b[1] - a[1]) / L * r, ny = (b[0] - a[0]) / L * r;
  return ['path', { d: `M${f(a[0] + nx)},${f(a[1] + ny)} L${f(b[0] + nx)},${f(b[1] + ny)} A${f(r)},${f(r)} 0 0 0 ${f(b[0] - nx)},${f(b[1] - ny)} L${f(a[0] - nx)},${f(a[1] - ny)} A${f(r)},${f(r)} 0 0 0 ${f(a[0] + nx)},${f(a[1] + ny)} Z` }];
}

/** The body's extent in one view, in metres: what is drawn, part by part. */
function extentOf(plan, view) {
  let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
  const grow = (q, r = 0) => { x0 = Math.min(x0, q[0] - r); x1 = Math.max(x1, q[0] + r); y0 = Math.min(y0, q[1] - r); y1 = Math.max(y1, q[1] + r); };
  for (const p of plan.parts) {
    const s = solid(p);
    if (s.kind === 'box') s.corners.forEach(c => grow(view.to(c)));
    else if (s.kind === 'ball') grow(view.to(s.c), s.r);
    else { grow(view.to(s.a), s.r); grow(view.to(s.b), s.r); }
  }
  return { x0, x1, y0, y1, bw: Math.max(x1 - x0, 1e-6), bh: Math.max(y1 - y0, 1e-6) };
}

const FILL = .8;   // the share of a view's width or height the body fills, whichever binds first

/** Pixels per metre that makes the body fill FILL of the view's width or height, whichever binds first (the room under the figure is the scale bar's). */
function scaleIn(svg, ext) {
  const R = rem(), w = svg.clientWidth, h = svg.clientHeight - 1.2 * R;
  if (!(w > 0 && h > 0)) return Infinity;
  return Math.max(Math.min(FILL * w / ext.bw, FILL * h / ext.bh), 1e-6);
}

/** Draws the body in one view at k pixels per metre; both views are drawn at the one k, so that they can be compared. */
function drawView(svg, plan, colorOf, view, k, barMetres) {
  svg.innerHTML = '';
  const R = rem(), w = svg.clientWidth, h = svg.clientHeight;
  if (!(w > 0 && h > 0)) return;
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  const parts = plan.parts.map(p => ({ p, s: solid(p) }));
  parts.sort((a, b) => view.depth(a.s.c) - view.depth(b.s.c));   // farthest first
  const { x0, y0, bw, bh } = extentOf(plan, view);
  const ox = (w - bw * k) / 2 - x0 * k, oy = (h - 1.2 * R - bh * k) / 2 - y0 * k;
  const P = v => { const q = view.to(v); return [ox + q[0] * k, oy + q[1] * k]; };

  // the tree, behind the parts: each part to its parent's centre
  for (const { p } of parts) {
    if (p.parent == null) continue;
    const par = plan.parts[p.parent];
    el(svg, 'line', { class: 'pe', x1: P(V(par.position))[0], y1: P(V(par.position))[1], x2: P(V(p.position))[0], y2: P(V(p.position))[1] });
  }
  // every part's translucent fill, farthest first, so that where two overlap each shows through the other...
  for (const { p, s } of parts) {
    const [tag, attrs] = shapeOf(s, P, k);
    el(el(svg, tag, { ...attrs, class: 'pgf', style: `--g:${colorOf(p.cellType)}` }), 'title', {}, `part ${p.index} · ${p.cellType} · ${p.shape}${p.mirrored ? ' · mirrored' : ''}`);
  }
  // ...then every part's outline, above all the fills
  for (const { p, s } of parts) {
    const [tag, attrs] = shapeOf(s, P, k);
    el(svg, tag, { ...attrs, class: 'pgo' + (p.mirrored ? ' mir' : ''), style: `--g:${colorOf(p.cellType)}` });
  }
  // joints: where each child meets its parent
  for (const { p } of parts) {
    if (p.joint) {
      const par = plan.parts[p.parent];
      const j = P(add(V(par.position), rot(par.rotation, V(p.joint.parentAnchor))));
      const dot = el(svg, 'circle', { class: 'pj', cx: j[0], cy: j[1], r: .28 * R });
      el(dot, 'title', {}, `joint of part ${p.index}: ${p.joint.type}, ${p.joint.dof} degree(s) of freedom`);
    }
  }
  // the part numbers, last: each at its part's centre, moved along the page when another number is already there
  const placed = [];
  for (const { p, s } of parts) {
    const c = P(s.c);
    let at = c[1];
    for (const dy of [0, 1.2 * R, -1.2 * R, 2.4 * R, -2.4 * R]) {
      if (!placed.some(q => Math.abs(q[0] - c[0]) < 1.5 * R && Math.abs(q[1] - (c[1] + dy)) < 1.1 * R)) { at = c[1] + dy; break; }
    }
    placed.push([c[0], at]);
    el(svg, 'text', { x: c[0], y: at + .27 * R, 'text-anchor': 'middle', class: 'halo', style: 'fill:var(--ink-hi);font-size:.8rem' }, p.index);
  }
  // scale bar: its length in pixels is its length in metres at the scale the body is drawn at
  const len = barMetres, bx = .8 * R, by = h - .5 * R;
  el(svg, 'line', { x1: bx, x2: bx + len * k, y1: by, y2: by, style: 'stroke:var(--ink-lo);stroke-width:1.5' });
  el(svg, 'text', { x: bx + len * k + .4 * R, y: by + .28 * R }, len >= 1 ? `${len} m` : `${+(len * 100).toPrecision(3)} cm`);
}

function drawBody(d) {
  const plan = d.bodyPlan;
  if (!plan) { for (const id of ['v-top', 'v-side']) $(id).innerHTML = ''; $('partlist').innerHTML = noData(d.problem || 'No body to draw.'); $('body-legend').innerHTML = ''; return; }
  const types = [...new Set(plan.parts.map(p => p.cellType))];
  const colorOf = t => PALETTE[types.indexOf(t) % PALETTE.length];
  $('body-legend').innerHTML = types.map(t => `<span><i class="swatch" style="background:${colorOf(t)}"></i>${esc(t)}</span>`).join('') +
    `<span>dashed outline: a mirrored part · dot: a joint · numbers: part index</span>`;
  // One scale for both views: the smaller of the two that would each fill FILL of its own view.
  const k = Math.min(scaleIn($('v-top'), extentOf(plan, VIEWS.top)), scaleIn($('v-side'), extentOf(plan, VIEWS.side)));
  if (Number.isFinite(k)) {
    const bar = niceLength(.3 * Math.min($('v-top').clientWidth, $('v-side').clientWidth) / k);
    drawView($('v-top'), plan, colorOf, VIEWS.top, k, bar);
    drawView($('v-side'), plan, colorOf, VIEWS.side, k, bar);
  }
  const dev = d.development || {};
  const rows = plan.parts.map(p => {
    const lim = p.joint ? p.joint.limits.map(l => `${fmt(l.min, 3)}…${fmt(l.max, 3)}`).join(' ; ') : '';
    const he = p.halfExtents;
    return `<tr><td class="hi">${p.index}<span class="lbl">${p.parent == null ? 'root' : 'on ' + p.parent}</span></td>` +
      `<td class="l sans"><i class="swatch" style="background:${colorOf(p.cellType)}"></i>${esc(p.cellType)}</td>` +
      `<td class="l sans">${esc(p.shape)}${p.mirrored ? ' ·m' : ''}</td>` +
      `<td>${fmt(he.x, 3)} × ${fmt(he.y, 3)} × ${fmt(he.z, 3)}</td>` +
      `<td class="l sans">${p.joint ? esc(p.joint.type) + (lim ? `<span class="lbl">${lim} rad</span>` : '') : '—'}</td></tr>`;
  }).join('');
  $('partlist').innerHTML = `<table class="t"><tr><th>part</th><th class="l">cell</th><th class="l">shape</th><th>half-extents, m</th><th class="l">joint, limits</th></tr>${rows}</table>` +
    (dev.truncated ? `<p class="note">Development stopped at its limits: ${['Volume', 'Depth', 'Parts', 'Reach'].filter(k => dev['prunedFor' + k] > 0).map(k => `${dev['prunedFor' + k]} subtree(s) dropped for ${k.toLowerCase()}`).join(', ')}.</p>` : '');
}

// ---------------------------------------------------------------- what the genome was grown under

/**
 * Where the config behind the body's limits and the brain's ports came from, said beside the body and beside the brain: a config the run recorded
 * (run.json), one room.json names for a run made before the record, or none (the body and ports are then Core's defaults, and may differ from the run's).
 */
function drawConfig(d) {
  const c = d && d.config;
  for (const id of ['body-cfg', 'brain-cfg']) {
    const e = $(id);
    if (!c) { e.hidden = true; e.textContent = ''; continue; }
    e.hidden = false;
    e.classList.toggle('warn', !c.loaded);
    // a config that loaded is said in a few words (its path is in the tooltip); one that did not is said in full, beside what was drawn without it
    e.textContent = c.loaded
      ? `${id === 'body-cfg' ? 'Limits and shapes' : 'Sensor pool'}: the config recorded in ${c.source}.` + (c.note ? ' ' + c.note : '')
      : c.note;
    e.title = `config source: ${c.source}${c.file ? ' · ' + c.file : ''}${c.refusal ? ' · refused: ' + c.refusal : ''}`;
  }
}

// ---------------------------------------------------------------- the genome's graph

/** The genome's nodes and edges, as it stores them. Layers by distance from the root; an edge to an earlier layer (or to its own node) loops under the boxes. */
function drawGenomeGraph(d) {
  const wrap = $('ggwrap'), svg = $('c-graph'), note = $('gg-note');
  svg.innerHTML = '';
  const gr = d.graph;
  if (!gr || !gr.nodes || !gr.nodes.length) { svg.setAttribute('width', 0); svg.setAttribute('height', 0); note.textContent = d.problem || 'No genome graph to draw.'; return; }
  const R = rem(), n = gr.nodes.length;
  const edges = gr.edges.filter(e => e.from >= 0 && e.from < n && e.to >= 0 && e.to < n);
  const layer = new Array(n).fill(-1);
  const root = gr.root >= 0 && gr.root < n ? gr.root : 0;
  layer[root] = 0;
  for (const queue = [root]; queue.length;) {
    const u = queue.shift();
    for (const e of edges) if (e.from === u && layer[e.to] < 0) { layer[e.to] = layer[u] + 1; queue.push(e.to); }
  }
  const unreached = layer.some(x => x < 0);
  if (unreached) { const last = Math.max(...layer) + 1; for (let i = 0; i < n; i++) if (layer[i] < 0) layer[i] = last; }
  const cols = [];
  for (let i = 0; i < n; i++) (cols[layer[i]] = cols[layer[i]] || []).push(i);

  const lines = i => {
    const nd = gr.nodes[i];
    return [`node ${i}${i === root ? ' · root' : ''}`, `${nd.cellType} · ${nd.shape}`,
      `recursion limit ${nd.recursiveLimit}` + (nd.growth && nd.growth !== 'Determinate' ? ` · ${nd.growth.toLowerCase()}, up to ${Math.max(nd.recursiveLimit, nd.maxModules)}` : '')];
  };
  const cw = .6 * .74 * R;
  const bw = Math.max(10 * R, ...gr.nodes.map((_, i) => Math.max(...lines(i).map((s, k) => s.length * (k === 0 ? .6 * .82 * R : cw))) + 1.6 * R));
  const bh = 4.6 * R, gapX = 8.5 * R, gapY = 1.8 * R, pad = 1.2 * R;

  // loops (an edge that does not go forward) take room under the boxes
  const forward = e => layer[e.to] > layer[e.from];
  const loops = edges.filter(e => !forward(e));
  const colH = c => c.length * bh + (c.length - 1) * gapY;
  const maxH = Math.max(...cols.map(colH));
  const W = pad * 2 + cols.length * bw + (cols.length - 1) * gapX;
  const H = pad + maxH + (loops.length ? 2.2 * R + loops.length * 1.0 * R : 0) + pad + .6 * R;
  const pos = [];
  cols.forEach((c, li) => c.forEach((i, k) => { pos[i] = { x: pad + li * (bw + gapX), y: pad + (maxH - colH(c)) / 2 + k * (bh + gapY) }; }));

  const defs = el(svg, 'defs');
  const mk = el(defs, 'marker', { id: 'gg-arrow', viewBox: '0 0 10 10', refX: 9, refY: 5, markerWidth: 7, markerHeight: 7, orient: 'auto' });
  el(mk, 'path', { d: 'M0 0 L10 5 L0 10z', style: 'fill:var(--run);fill-opacity:.8' });

  // edges first, so that the boxes lie over their ends
  const outs = new Map(), ins = new Map();
  edges.filter(forward).forEach(e => { (outs.get(e.from) || outs.set(e.from, []).get(e.from)).push(e); (ins.get(e.to) || ins.set(e.to, []).get(e.to)).push(e); });
  const spread = (list, e, node) => pos[node].y + bh * (list.indexOf(e) + 1) / (list.length + 1);
  const shortId = e => e.id ? (e.id.replace(/^0+/, '') || '0') : `#${e.index}`;
  const flags = e => {
    const f = [];
    const axes = ['x', 'y', 'z'].filter(a => e.reflect && e.reflect[a]);
    if (axes.length) f.push(`mirror ${axes.join(',')} ×${e.copies}`);
    if (e.terminalOnly) f.push('terminal only');
    return f;
  };
  const labelAt = (e, x, y, below) => {
    const fl = flags(e);
    el(svg, 'text', { class: 'gg-lbl halo', x, y: y - .35 * R, 'text-anchor': 'middle' }, `edge ${shortId(e)}`);
    fl.forEach((t, i) => el(svg, 'text', { class: 'gg-lbl f halo', x, y: y + (1.0 + i * .95) * R, 'text-anchor': 'middle' }, t));
  };
  const tip = e => `edge ${e.id || '#' + e.index}: node ${e.from} → node ${e.to}${flags(e).length ? ' · ' + flags(e).join(' · ') : ''}`;
  for (const e of edges.filter(forward)) {
    const x1 = pos[e.from].x + bw, y1 = spread(outs.get(e.from), e, e.from), x2 = pos[e.to].x - 1, y2 = spread(ins.get(e.to), e, e.to);
    const mid = (x1 + x2) / 2;
    const p = el(svg, 'path', { class: 'gg-edge' + (e.terminalOnly ? ' term' : ''), d: `M${x1},${y1} C${mid},${y1} ${mid},${y2} ${x2},${y2}`, 'marker-end': 'url(#gg-arrow)' });
    el(p, 'title', {}, tip(e));
    labelAt(e, mid, (y1 + y2) / 2);
  }
  loops.forEach((e, i) => {
    const same = e.from === e.to;
    const a = pos[e.from], b = pos[e.to];
    const ax = a.x + bw * (same ? .3 : .5), bx = b.x + bw * (same ? .7 : .5);
    const ay = a.y + bh, by = b.y + bh, drop = 1.6 * R + (i + 1) * 1.0 * R;
    const p = el(svg, 'path', { class: 'gg-edge' + (e.terminalOnly ? ' term' : ''), d: `M${ax},${ay} C${ax},${ay + drop} ${bx},${by + drop} ${bx},${by + 1}`, 'marker-end': 'url(#gg-arrow)' });
    el(p, 'title', {}, tip(e));
    const fl = flags(e).join(' · ');
    el(svg, 'text', { class: 'gg-lbl halo', x: (ax + bx) / 2, y: Math.max(ay, by) + drop * .75 + .35 * R, 'text-anchor': 'middle' }, `edge ${shortId(e)}${fl ? ' · ' + fl : ''}`);
  });

  gr.nodes.forEach((nd, i) => {
    const gn = el(svg, 'g', { class: 'gg-node' + (i === root ? ' root' : '') });
    el(gn, 'title', {}, `node ${i}: ${nd.cellType}, ${nd.shape}, ${nd.joint} joint, ${nd.edges} outgoing edge${nd.edges === 1 ? '' : 's'}`);
    el(gn, 'rect', { x: pos[i].x, y: pos[i].y, width: bw, height: bh, rx: .6 * R });
    lines(i).forEach((t, k) => el(gn, 'text', { class: 't' + (k + 1), x: pos[i].x + .8 * R, y: pos[i].y + (1.5 + k * 1.15) * R }, t));
  });

  // fit: shrink to the panel, grow at most a little, and scroll when it would have to be too small to read
  const aw = Math.max(wrap.clientWidth, 80), ah = Math.max(wrap.clientHeight, 80);
  const s = Math.min(Math.max(Math.min(aw / W, ah / H), .62), 1.45);
  svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
  svg.setAttribute('width', Math.round(W * s));
  svg.setAttribute('height', Math.round(H * s));
  note.textContent = `${n} node${n === 1 ? '' : 's'} · ${gr.edges.length} edge${gr.edges.length === 1 ? '' : 's'} · the root is node ${root}` +
    (d.bodyPlan ? ` · development grew ${d.bodyPlan.partCount} part${d.bodyPlan.partCount === 1 ? '' : 's'} from it` : '') +
    (unreached ? ' · nodes in the last column are not reached from the root' : '');
}

// ---------------------------------------------------------------- the brain, as a graph

function partTitle(plan, i) {
  const p = plan && plan.parts[i];
  return p ? `part ${i} · ${p.cellType} · ${p.shape}${p.joint ? ' · ' + p.joint.type : ''}` : `part ${i}`;
}

/** What differs from the base parent, as the brain draws it: the links and units named in the change list. */
function changeIndex(d) {
  const out = { has: false, links: new Map(), units: new Set() };
  const c = d && d.changes;
  if (!c || !c.against || c.identical || !c.changes || !c.changes.length) return out;
  out.has = true;
  for (const x of c.changes) {
    const m = /^brain\.links\[([0-9a-f]{16})>([0-9a-f]{16})\]/.exec(x.path);
    if (m) { const k = m[1] + '>' + m[2]; if (!out.links.has(k) || x.kind === 'removed') out.links.set(k, x.kind); continue; }
    const u = /^brain\.(?:hidden|outputs)\[([0-9a-f]{16})\]/.exec(x.path);
    if (u) out.units.add(u[1]);
  }
  return out;
}

function setFilterUi(ch) {
  const chg = $('f-chg');
  chg.disabled = !ch.has;
  chg.title = ch.has ? 'only the links that differ from the base parent' : 'the links that differ from the base parent: this genome has no change list';
  if (filter === 'changed' && !ch.has) filter = 'all';
  const set = (id, on) => { $(id).classList.toggle('on', on); $(id).setAttribute('aria-pressed', on ? 'true' : 'false'); };
  set('f-all', filter === 'all'); set('f-chg', filter === 'changed');
  $('f-w-wrap').classList.toggle('on', filter === 'weight'); $('f-w').setAttribute('aria-pressed', filter === 'weight' ? 'true' : 'false');
}

function drawBrain(d) {
  const svg = $('graph'), wrap = $('graphwrap');
  svg.innerHTML = '';
  const b = d.brain;
  const bare = () => { $('brain-legend').innerHTML = ''; $('unlinked').style.display = 'none'; $('linkinfo').innerHTML = ''; $('b-counts').textContent = ''; };
  if (!b) { bare(); wrap.innerHTML = noData(d.problem || 'No brain to draw.'); return; }
  if (!wrap.contains(svg)) { wrap.innerHTML = ''; wrap.appendChild(svg); }
  if (b.model !== 'central') { bare(); svg.setAttribute('height', 0); wrap.insertAdjacentHTML('afterbegin', `<p class="note nodata">${esc(b.note)}</p>`); return; }
  wrap.querySelectorAll('p.nodata').forEach(x => x.remove());

  const R = rem();
  const ch = changeIndex(d);
  setFilterUi(ch);
  const nothingLabel = 'names nothing in the grown body';
  const keyOf = l => l.from + '>' + l.to;

  // the links to draw
  const vis = b.links.filter(l => filter === 'all'
    || (filter === 'changed' && ['changed', 'added'].includes(ch.links.get(keyOf(l))))
    || (filter === 'weight' && Math.abs(l.weight) > threshold));
  const fromCount = new Map();
  for (const l of vis) fromCount.set(l.from, (fromCount.get(l.from) || 0) + 1);

  const linkedPorts = b.ports.filter(p => p.links > 0).length;
  $('b-counts').textContent = `${linkedPorts} of ${b.counts.ports} senses linked · ${b.counts.hidden} hidden · ${b.counts.outputs} out · ${b.counts.links} links` + (filter === 'all' ? '' : ` · ${vis.length} drawn`);

  const unlinked = b.links.length ? b.ports.filter(p => p.links === 0 && p.exists).length : 0;
  const btn = $('unlinked');
  btn.style.display = unlinked ? '' : 'none';
  btn.textContent = `${unlinked} UNLINKED PORT${unlinked === 1 ? '' : 'S'}: ${showUnlinked ? 'SHOWN' : 'HIDDEN'}`;
  btn.setAttribute('aria-pressed', showUnlinked ? 'true' : 'false');

  const nodes = new Map();
  const left = [], right = [], mid = [];          // groups: {key, title, nothing, items}
  const groupOf = (arr, key, title, nothing) => { let x = arr.find(q => q.key === key); if (!x) { x = { key, title, nothing, items: [] }; arr.push(x); } return x; };
  const addNode = (n, arr, key, title, nothing) => { nodes.set(n.id, n); groupOf(arr, key, title, nothing).items.push(n); };

  for (const p of b.ports) {
    const cnt = fromCount.get(p.id) || 0;
    if (cnt === 0 && !showUnlinked && b.links.length > 0) continue;
    const gk = p.part == null ? 'x' : p.part;
    addNode({ id: p.id, kind: 'port', label: p.sense || p.id, tip: p.name, exists: p.exists, side: 'L', sub: cnt ? `×${cnt}` : '' },
      left, gk, p.part == null ? nothingLabel : partTitle(d.bodyPlan, p.part), p.part == null);
  }
  for (const o of b.outputs) {
    addNode({ id: o.id, kind: 'output', label: o.kind ? `${o.kind} · dof ${o.dof}` : o.id, tip: o.name, exists: o.exists, side: 'R', sub: o.bias != null ? `b ${fmt(o.bias, 2)}` : 'no unit', bias: o.bias },
      right, o.part == null ? 'x' : o.part, o.part == null ? nothingLabel : partTitle(d.bodyPlan, o.part), o.part == null);
  }
  for (const u of b.hidden) addNode({ id: u.id, kind: 'hidden', label: u.name.replace(/^hidden /, 'h '), tip: `${u.name} · bias ${fmt(u.bias, 3)} · ${u.inbound} in, ${u.outbound} out`, exists: true, side: 'M', sub: `b ${fmt(u.bias, 2)}`, bias: u.bias }, mid, 'h', 'hidden units', false);
  // an end a link names that no unit of the brain has
  for (const l of vis) {
    if (!nodes.has(l.to)) addNode({ id: l.to, kind: 'output', label: l.to, tip: `${l.to} (${nothingLabel})`, exists: false, side: 'R', sub: '' }, right, 'x', nothingLabel, true);
  }
  left.sort((a, z) => (a.nothing - z.nothing) || (a.key - z.key)); right.sort((a, z) => (a.nothing - z.nothing) || (a.key - z.key));

  // ---- sizes, from the text that has to fit
  const m = .6 * R;
  const fs = .82 * R, cwM = .6 * fs, cwT = .6 * .7 * R + .05 * R;
  const portW = Math.max(0, ...left.flatMap(q => q.items.map(n => (n.label.length + (n.sub ? n.sub.length + 2 : 0)) * cwM)));
  const outW = Math.max(0, ...right.flatMap(q => q.items.map(n => (n.label.length + (n.sub ? n.sub.length + 2 : 0)) * cwM)));
  const titleL = Math.max(0, ...left.map(q => q.title.length * cwT));
  const titleR = Math.max(0, ...right.map(q => q.title.length * cwT));
  const radP = .42 * R, radO = .66 * R;
  const avail = Math.max(wrap.clientWidth - 22, 360);   // the room a scrollbar may take is kept: a graph drawn to the full width would be clipped by it
  const xin = Math.min(avail * .4, m + Math.max(portW + .9 * R, titleL + .5 * R) + .4 * R) + radP;
  // measured from the output box's left edge: the box, its label to the right; or the group's title, which starts at the same edge
  const rightExtent = Math.max(2 * radO + .6 * R + outW, titleR) + .6 * R;
  const cut = (t, px) => { const room = Math.floor(px / cwT); return t.length <= room ? t : t.slice(0, Math.max(room - 1, 1)) + '…'; };

  // ---- rows
  const top = 1.9 * R, grpH = 1.5 * R, rh0 = 1.45 * R;
  const stack = (groups, rh, at) => {
    let y = at;
    for (const grp of groups) { grp.y = y + grpH * .7; y += grpH; for (const n of grp.items) { n.cy = y + rh / 2; y += rh; } y += .35 * R; }
    return y;
  };
  const rowsL = left.reduce((a, q) => a + q.items.length, 0);
  const natural = stack(left, rh0, top);
  const room = Math.max(wrap.clientHeight - 3 * R, 0);   // under it, the last hidden unit's label and the margin
  const rh = rowsL && natural < room ? Math.min(rh0 + (room - natural) / rowsL, 2.4 * R) : rh0;
  const bottomL = stack(left, rh, top);
  const itemsL = left.flatMap(q => q.items);
  const firstL = itemsL.length ? itemsL[0].cy : top + 2 * R, lastL = itemsL.length ? itemsL[itemsL.length - 1].cy : Math.max(room - 2 * R, top + 6 * R);
  stack(right, Math.max(rh, 1.9 * R), top);
  const itemsR = right.flatMap(q => q.items);
  if (itemsR.length) {
    // the outputs, centred on the senses column
    const mid0 = (firstL + lastL) / 2, midR = (itemsR[0].cy + itemsR[itemsR.length - 1].cy) / 2;
    const shift = Math.max(mid0 - midR, 2.3 * R - right[0].y);   // never so high that the first title meets the column heads
    for (const grp of right) { grp.y += shift; for (const n of grp.items) n.cy += shift; }
  }
  // the hidden units, spread over the height of the senses column
  const hid = mid.flatMap(q => q.items);
  hid.forEach((n, i) => { n.cy = firstL + (lastL - firstL) * (i + .5) / hid.length; });
  const spacing = hid.length ? (lastL - firstL) / hid.length : 4 * R;
  const radH = Math.max(.45 * R, Math.min(.95 * R, spacing * .3));
  const hidBelow = spacing >= 3.4 * R;   // room for a hidden unit's label under it; else it stands to the right of the unit

  // Links between two hidden units are arcs that bow out to the right of the column, beneath the nodes. Each is given a lane, so that two that
  // overlap in height do not run on one line, and the column is kept clear of every unit's label.
  const hidLabelW = Math.max(0, ...hid.map(n => (n.label.length + (hidBelow || !n.sub ? 0 : n.sub.length + 2)) * cwM));
  const hidReach = radH + (hidBelow ? Math.max(0, hidLabelW / 2 - radH) : .5 * R + hidLabelW + .7 * R) + 1.1 * R;   // from the column's axis to the first free lane
  const isHH = (a, z) => a && z && a.side === 'M' && z.side === 'M' && a !== z;
  const hhLink = [];
  for (const l of vis) { const a = nodes.get(l.from), z = nodes.get(l.to); if (isHH(a, z)) hhLink.push([l.from + '>' + l.to, a.cy, z.cy]); }
  if (ch.has && filter !== 'weight') for (const [k, kind] of ch.links) {
    if (kind !== 'removed') continue;
    const [f, t] = k.split('>'); const a = nodes.get(f), z = nodes.get(t);
    if (isHH(a, z) && !hhLink.some(q => q[0] === k)) hhLink.push([k, a.cy, z.cy]);
  }
  const lane = new Map(), laneEnd = [];
  for (const [k, y1, y2] of hhLink.map(([k, y1, y2]) => [k, Math.min(y1, y2), Math.max(y1, y2)]).sort((p, q) => p[1] - q[1] || p[2] - q[2])) {
    let i = laneEnd.findIndex(end => end < y1 - .2 * R);
    if (i < 0) { i = laneEnd.length; laneEnd.push(0); }
    laneEnd[i] = y2; lane.set(k, i);
  }
  const laneStep = laneEnd.length > 8 ? 4 * R / laneEnd.length : .5 * R;
  const hhNeed = laneEnd.length ? 2 * (hidReach + (laneEnd.length - 1) * laneStep + radO + .6 * R) : 0;
  const W = Math.max(avail, xin + Math.max(16 * R, hhNeed) + rightExtent + m);
  const xo = W - m - rightExtent + radO, xh = (xin + xo) / 2;
  const H = Math.max(bottomL, ...itemsR.map(n => n.cy + 2 * R), hid.length ? hid[hid.length - 1].cy + 2.4 * R : 0) + m;
  svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
  svg.setAttribute('width', W);
  svg.setAttribute('height', H);

  for (const n of nodes.values()) {
    if (n.side === 'L') { n.cx = xin; n.r = radP; }
    else if (n.side === 'R') { n.cx = xo; n.r = radO; }
    else { n.cx = xh; n.r = radH; }
  }

  // ---- links: beneath every node and every label
  const maxW = Math.max(...b.links.map(l => Math.abs(l.weight)), 1e-9);
  // Rounded corners along a polyline: each corner cut by r, or by half of the shorter side.
  const rounded = (pts, r) => {
    const f = q => q[0].toFixed(1) + ',' + q[1].toFixed(1);
    let d = 'M' + f(pts[0]);
    for (let i = 1; i < pts.length - 1; i++) {
      const p0 = pts[i - 1], p1 = pts[i], p2 = pts[i + 1];
      const l1 = Math.hypot(p1[0] - p0[0], p1[1] - p0[1]), l2 = Math.hypot(p2[0] - p1[0], p2[1] - p1[1]);
      const k = Math.min(r, l1 / 2, l2 / 2);
      const u = l1 > 0 ? k / l1 : 0, v = l2 > 0 ? k / l2 : 0;
      d += ' L' + f([p1[0] + (p0[0] - p1[0]) * u, p1[1] + (p0[1] - p1[1]) * u]) + ' Q' + f(p1) + ' ' + f([p1[0] + (p2[0] - p1[0]) * v, p1[1] + (p2[1] - p1[1]) * v]);
    }
    return d + ' L' + f(pts[pts.length - 1]);
  };
  const hiddenArc = (a, z, ln) => {
    const xp = a.cx + hidReach + ln * laneStep;                       // the lane: right of the nodes and of every label
    if (a === z) {                                                    // a unit's link to itself: a small loop on its right
      const x1 = a.cx + a.r;
      return [`M${x1.toFixed(1)},${(a.cy - a.r * .3).toFixed(1)} C${(x1 + 2.4 * R).toFixed(1)},${(a.cy - 1.6 * R).toFixed(1)} ${(x1 + 2.4 * R).toFixed(1)},${(a.cy + 1.6 * R).toFixed(1)} ${x1.toFixed(1)},${(a.cy + a.r * .3).toFixed(1)}`, x1 + 1.8 * R, a.cy];
    }
    const s = Math.sign(z.cy - a.cy) || 1;
    let pts;
    if (hidBelow) pts = [[a.cx + a.r, a.cy], [xp, a.cy], [xp, z.cy], [z.cx + z.r, z.cy]];   // labels are under the units: out of one's right edge and into the other's
    else {                                                                                  // labels are to the right: leave by the edge that faces the other unit, between the rows
      const ya = a.cy + s * (a.r + .55 * R), yz = z.cy - s * (z.r + .55 * R);
      pts = [[a.cx, a.cy + s * a.r], [a.cx, ya], [xp, ya], [xp, yz], [z.cx, yz], [z.cx, z.cy - s * z.r]];
    }
    return [rounded(pts, 1.1 * R), xp, (a.cy + z.cy) / 2];
  };
  const pathOf = (a, z, key) => {
    if (isHH(a, z) || (a === z && a.side === 'M')) return hiddenArc(a, z, lane.get(key) || 0);
    let x1, x2, c1, c2;
    if (a.cx < z.cx - 1) { x1 = a.cx + a.r; x2 = z.cx - z.r; const dx = (x2 - x1) / 2; c1 = x1 + dx; c2 = x2 - dx; }
    else if (a.cx > z.cx + 1) { x1 = a.cx - a.r; x2 = z.cx + z.r; const dx = (x1 - x2) / 2; c1 = x1 - dx; c2 = x2 + dx; }
    else { x1 = a.cx + a.r; x2 = z.cx + z.r; c1 = x1 + 4 * R; c2 = x2 + 4 * R; }
    return [`M${x1.toFixed(1)},${a.cy.toFixed(1)} C${c1.toFixed(1)},${a.cy.toFixed(1)} ${c2.toFixed(1)},${z.cy.toFixed(1)} ${x2.toFixed(1)},${z.cy.toFixed(1)}`, (x1 + x2) / 2, (a.cy + z.cy) / 2];
  };
  const touches = l => selected && (l.from === selected || l.to === selected);
  const gLinks = el(svg, 'g', { 'aria-hidden': 'true' });
  // in the base parent and gone here: dotted, under the rest
  if (ch.has && filter !== 'weight') {
    for (const [k, kind] of ch.links) {
      if (kind !== 'removed') continue;
      const [f, t] = k.split('>');
      const a = nodes.get(f), z = nodes.get(t);
      if (!a || !z) continue;
      el(el(gLinks, 'path', { class: 'lk gone' + (selected && f !== selected && t !== selected ? ' off' : ''), d: pathOf(a, z, k)[0] }), 'title', {}, `${a.tip} → ${z.tip}: in the base parent, gone here`);
    }
  }
  const draw = (l, changed) => {
    const a = nodes.get(l.from), z = nodes.get(l.to);
    if (!a || !z) return;
    const neg = l.weight < 0, t = Math.abs(l.weight) / maxW;
    const [dd, mx, my] = pathOf(a, z, keyOf(l));
    const cls = 'lk ' + (changed ? 'chg' : neg ? 'neg' : 'pos') + (selected && !touches(l) ? ' off' : '');
    const e = el(gLinks, 'path', { class: cls, d: dd, style: changed ? `stroke-width:${(1.6 + 2.2 * t).toFixed(2)}` : `stroke-width:${(.6 + 3.4 * t).toFixed(2)};stroke-opacity:${(.22 + .6 * t).toFixed(2)}` });
    el(e, 'title', {}, `${a.tip} → ${z.tip}: ${neg ? '−' : '+'}${fmt(Math.abs(l.weight), 4)}${changed ? ' · ' + (ch.links.get(keyOf(l)) === 'added' ? 'new since the base parent' : 'changed from the base parent') : ''}`);
    if (changed && !(selected && !touches(l))) el(gLinks, 'circle', { class: 'chgdot', cx: mx, cy: my, r: .28 * R });
  };
  const isChanged = l => ch.has && ['changed', 'added'].includes(ch.links.get(keyOf(l)));
  for (const l of vis) if (!isChanged(l)) draw(l, false);
  for (const l of vis) if (isChanged(l)) draw(l, true);

  // ---- column heads, groups and nodes, above the links
  el(svg, 'text', { class: 'col', x: m, y: .95 * R }, 'senses, by part');
  if (hid.length) el(svg, 'text', { class: 'col', x: xh, y: .95 * R, 'text-anchor': 'middle' }, `hidden units · ${hid.length}`);
  el(svg, 'text', { class: 'col', x: W - m, y: .95 * R, 'text-anchor': 'end' }, 'outputs, by part');
  for (const grp of left) el(el(svg, 'text', { class: 'grp', x: m, y: grp.y }, cut(grp.title, xin - m - .5 * R - radP)), 'title', {}, grp.title);
  for (const grp of right) el(el(svg, 'text', { class: 'grp', x: xo - radO, y: grp.y }, cut(grp.title, W - m - (xo - radO))), 'title', {}, grp.title);

  const dim = (text, sub) => (sub ? `${esc(text)}<tspan class="dim" dx="${(.7 * R).toFixed(1)}">${esc(sub)}</tspan>` : esc(text));
  for (const n of nodes.values()) {
    if (n.cy == null) continue;
    const faded = selected && selected !== n.id && !b.links.some(l => touches(l) && (l.from === n.id || l.to === n.id));
    const cls = 'gn' + (n.exists ? '' : ' nothing') + (selected === n.id ? ' sel' : '') + (faded ? ' faded' : '') + (ch.units.has(n.id) ? ' chgd' : '');
    const gn = el(svg, 'g', { class: cls, tabindex: 0, role: 'button', 'aria-pressed': selected === n.id ? 'true' : 'false', 'aria-label': n.tip });
    el(gn, 'title', {}, n.tip);
    if (n.side === 'L') {
      el(gn, 'rect', { class: 'hit', x: m, y: n.cy - rh / 2, width: n.cx + n.r - m, height: rh });
      el(gn, 'circle', { class: 'nd', cx: n.cx, cy: n.cy, r: n.r });
      if (n.exists) el(gn, 'circle', { class: 'core', cx: n.cx, cy: n.cy, r: n.r * .5 });
      const t = el(gn, 'text', { x: n.cx - n.r - .35 * R, y: n.cy + .3 * R, 'text-anchor': 'end' }); t.innerHTML = dim(n.label, n.sub);
    } else if (n.side === 'R') {
      el(gn, 'rect', { class: 'hit', x: n.cx - n.r, y: n.cy - 1.2 * R, width: rightExtent, height: 2.4 * R });
      el(gn, 'rect', { class: 'nd', x: n.cx - n.r, y: n.cy - n.r * 1.35, width: n.r * 2, height: n.r * 2.7, rx: n.r * .4 });
      if (n.exists) el(gn, 'circle', { class: 'core', cx: n.cx, cy: n.cy, r: n.r * .42 });
      const t = el(gn, 'text', { x: n.cx + n.r + .5 * R, y: n.cy + .3 * R }); t.innerHTML = dim(n.label, n.sub);
    } else {
      el(gn, 'circle', { class: 'hit', cx: n.cx, cy: n.cy, r: n.r + .9 * R });
      el(gn, 'circle', { class: 'nd', cx: n.cx, cy: n.cy, r: n.r });
      el(gn, 'circle', { class: 'core', cx: n.cx, cy: n.cy, r: n.r * .5 });
      const roomBelow = hidBelow;
      const t = el(gn, 'text', roomBelow ? { x: n.cx, y: n.cy + n.r + 1.05 * R, 'text-anchor': 'middle' } : { x: n.cx + n.r + .5 * R, y: n.cy + .3 * R });
      t.innerHTML = roomBelow ? esc(n.label) : dim(n.label, n.sub);
    }
    const toggle = () => { selected = selected === n.id ? null : n.id; drawBrain(g); };
    gn.addEventListener('click', toggle);
    gn.addEventListener('keydown', ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); toggle(); } });
  }

  // ---- the legend
  const goneShown = ch.has && filter !== 'weight' && [...ch.links.values()].some(k => k === 'removed');
  $('brain-legend').innerHTML =
    `<span><svg width="30" height="8" aria-hidden="true"><line x1="1" x2="29" y1="4" y2="4" style="stroke:var(--run);stroke-width:3"/></svg>&nbsp;positive · width is |weight|</span>` +
    `<span><svg width="30" height="8" aria-hidden="true"><line x1="1" x2="29" y1="4" y2="4" style="stroke:var(--ink);stroke-width:3;stroke-dasharray:5 3"/></svg>&nbsp;negative</span>` +
    (ch.has ? `<span><svg width="30" height="8" aria-hidden="true"><line x1="1" x2="29" y1="4" y2="4" style="stroke:var(--sig);stroke-width:2"/><circle cx="15" cy="4" r="3" style="fill:var(--sig)"/></svg>&nbsp;changed from the base parent</span>` : '') +
    (goneShown ? `<span><svg width="30" height="8" aria-hidden="true"><line x1="1" x2="29" y1="4" y2="4" style="stroke:var(--ink-dim);stroke-dasharray:1 4"/></svg>&nbsp;in the base parent, gone here</span>` : '') +
    `<span>dashed circle: ${esc(nothingLabel)}</span>`;
  drawLinkInfo(b, nodes);
}

function drawLinkInfo(b, nodes) {
  const box = $('linkinfo');
  const nameOf = id => (nodes.get(id) || {}).tip || id;
  if (!selected || !nodes.has(selected)) {
    selected = null;
    const top = b.links.slice().sort((a, z) => Math.abs(z.weight) - Math.abs(a.weight)).slice(0, 4);
    box.innerHTML = `<div>Click a port, a hidden unit or an output to list the links that touch it.${top.length ? ' Strongest: ' : ''}</div>` +
      top.map(l => `<div>${esc(nameOf(l.from))} → ${esc(nameOf(l.to))} <span class="w">${l.weight < 0 ? '−' : '+'}${fmt(Math.abs(l.weight), 3)}</span></div>`).join('');
    return;
  }
  const n = nodes.get(selected);
  const ins = b.links.filter(l => l.to === selected), outs = b.links.filter(l => l.from === selected);
  box.innerHTML = `<div class="hi">${esc(n.tip)}${n.bias != null ? ` · bias ${fmt(n.bias, 3)}` : ''} · id ${esc(selected)}</div>` +
    ins.map(l => `<div>← ${esc(nameOf(l.from))} <span class="w">${l.weight < 0 ? '−' : '+'}${fmt(Math.abs(l.weight), 4)}</span></div>`).join('') +
    outs.map(l => `<div>→ ${esc(nameOf(l.to))} <span class="w">${l.weight < 0 ? '−' : '+'}${fmt(Math.abs(l.weight), 4)}</span></div>`).join('') +
    (ins.length + outs.length === 0 ? '<div>no link touches it</div>' : '');
}

// ---------------------------------------------------------------- lineage and changes

function drawLineage(lst, d) {
  const page = $('page'), b = $('b-lineage');
  const lin = d.lineage || {};
  page.classList.toggle('nolineage', !!lin.predatesLineage);
  if (lin.predatesLineage) {
    b.innerHTML = noData(`This run predates lineage: it wrote no ${lin.file}, so nothing in its files records where a genome came from or which genome it was made from.`);
    return;
  }
  if (lin.present === false && lin.note) { b.innerHTML = noData(lin.note); return; }
  const f = (k, v) => `<div class="field"><span class="field__label">${esc(k)}</span><span class="field__value">${v}</span></div>`;
  const row = lin.row;
  const parentsHtml = (d.parents || []).map(p => {
    const link = p.held ? `<a class="glink" href="${href(runId, p.heldAs)}">${esc(p.id)}</a>` : `${esc(p.id)} <span style="color:var(--ink-lo)">(not kept)</span>`;
    return `${link} <span style="color:var(--ink-lo)">${esc(p.role)}</span>`;
  }).join('<br>');
  let h = '';
  h += f('member id', esc(d.memberId || 'not recorded'));
  if (row) {
    h += f('generation', row.gen) + f('born', row.born) + f('body', row.body) + f('elite', row.elite ? 'yes: carried over unchanged' : 'no');
    h += f('parents', parentsHtml || 'none: a founder');
  }
  h += f('kept as', esc([d.id, ...(d.aliases || [])].join(', '))) + f('read from', esc(d.source || ''));
  if (!row && lin.note) h += `<p class="note" style="margin-top:.6rem">${esc(lin.note)}</p>`;
  for (const t of (lin.problems || [])) h += `<p class="note" style="margin-top:.4rem">${esc(t)}</p>`;
  if (d.idNote) h += `<p class="note" style="margin-top:.6rem">How the id was found: ${esc(d.idNote)}.</p>`;
  b.innerHTML = h;
}

function drawChanges(d) {
  const c = d.changes;
  const put = html => { $('b-changes').innerHTML = html; $('b-changes-in').innerHTML = html; };
  const count = t => { $('chg-n').textContent = t; $('chg-n-in').textContent = t; };
  count('');
  if (!c) { $('chg-q').textContent = 'Changed from base parent'; put(noData(d.problem || 'No comparison.')); return; }
  if (!c.against) {
    $('chg-q').textContent = 'Changed from base parent';
    put(noData(c.parent ? `${c.reason}. The parent is ${c.parent}.` : c.reason + '.'));
    return;
  }
  $('chg-q').innerHTML = `Changed from <a class="glink" href="${href(runId, c.against)}">${esc(c.against)}</a>`;
  count(c.count);
  if (c.identical) { put(noData(`Identical to ${c.against}, field for field.`)); return; }
  const ops = { changed: '~', added: '+', removed: '−' };
  const rows = c.changes.map(x => {
    const v = x.kind === 'changed' ? `${esc(x.from)} → <b>${esc(x.to)}</b>` : x.kind === 'added' ? `<b>${esc(x.to)}</b>` : `was ${esc(x.from)}`;
    return `<div class="chgrow${x.kind === 'added' ? ' new' : ''}"><span class="op">${ops[x.kind] || '?'}</span><span class="what">${esc(x.path)}${x.label ? `<span class="lbl">${esc(x.label)}</span>` : ''}<span class="v">${v}</span></span></div>`;
  }).join('');
  put(`<p class="note">${c.count} difference${c.count === 1 ? '' : 's'} from ${esc(c.against)}, by path${c.truncated ? `; the first ${c.changes.length} are listed` : ''}. Edges and brain units are named by their ids, links by their two ends.</p>${rows}`);
}

// ---------------------------------------------------------------- the loop

function drawAll(lst, d) {
  drawBand(lst, d);
  if (!d) return;
  drawConfig(d);
  drawBody(d);
  drawGenomeGraph(d);
  drawChanges(d);
  drawBrain(d);
  drawLineage(lst, d);
}

/**
 * A run or a genome the room cannot show: the shared refusal, once, in the main area (the crumb, the message and the way back), and the page's polling ends.
 * The export and every other control of a genome are not in the page then, so none of them is live with nothing to act on.
 */
function refuseGenome({ title, message, links }) {
  gone = true;
  refuse({ crumb: 'genome · ' + (runId || '—') + (wanted ? ' · ' + wanted : ''), kicker: 'Genome', title, message, links });
  document.title = `Nursery room · genome · ${title.toLowerCase()}`;
  return 'stop';
}
const runLinks = () => (runId ? [[`run.html?id=${encodeURIComponent(runId)}`, 'BACK TO THE RUN']] : []);

async function tick() {
  if (gone) return 'stop';
  if (!runId) {
    // the run last looked at on any screen, else the newest
    const kept = getSel();
    if (kept.run != null) { runId = kept.run; if (wanted == null && kept.genome != null) { wanted = kept.genome; wantedFrom = 'kept'; } }
    else {
      const reg = await getResource('nursery://registry');
      const first = reg.items.find(i => i.exists);
      if (!first) return refuseGenome({ title: 'No run to show', message: 'The registry is empty and room.json names no run folder that exists.', links: [['registry.html', 'BACK TO THE REGISTRY']] });
      runId = first.id;
    }
  }
  let lst;
  try { lst = await getResource('nursery://genomes/' + encodeURIComponent(runId)); }
  catch (e) {
    if (!e.refused) throw e;
    return refuseGenome({ title: e.status === 404 ? 'No such run' : 'Not a run the room can read', message: e.message, links: [['registry.html', 'BACK TO THE REGISTRY']] });
  }
  const fresh = lst.version !== listVersion;
  list = lst;
  if (fresh) { listVersion = lst.version; fillPicker(lst); }
  $('foot').textContent = `polls every ${pollSeconds} s · ${lst.count} genome${lst.count === 1 ? '' : 's'} kept · read ${new Date().toLocaleTimeString()}` +
    (lst.notes && lst.notes.length ? ` · ${lst.notes.length} note${lst.notes.length === 1 ? '' : 's'}: ${lst.notes.join(' · ')}` : '');
  if (!lst.genomes.length) return refuseGenome({ title: 'No genome to show', message: (lst.notes || []).join(' ') || 'This run holds no genome.', links: [...runLinks(), ['registry.html', 'BACK TO THE REGISTRY']] });
  // no genome asked for: the last generation's top, else the first the run holds
  const tops = lst.genomes.filter(x => x.kinds.includes('top'));
  const target = wanted || (tops.length ? tops[tops.length - 1].id : lst.genomes[0].id);
  const key = lst.version + '|' + target;
  if (key === shownKey) return;
  let d;
  try { d = await getResource(`nursery://genome/${encodeURIComponent(runId)}/${encodeURIComponent(target)}`); }
  catch (e) {
    if (!e.refused) throw e;
    // a genome kept from an earlier visit (not one named in the address) that this run does not hold: fall back to the run's own default rather than refuse
    if (wantedFrom === 'kept') { wanted = null; wantedFrom = null; return tick(); }
    return refuseGenome({ title: e.status === 404 ? 'No such genome' : 'Not a genome the room can read', message: e.message,
      links: [[`genome.html?run=${encodeURIComponent(runId)}`, "OPEN THIS RUN'S TOP GENOME"], ...runLinks(), ['registry.html', 'BACK TO THE REGISTRY']] });
  }
  shownKey = key;
  g = d;
  wanted = d.id;
  history.replaceState(null, '', href(runId, d.id));
  drawAll(lst, d);
}

$('pick').addEventListener('change', ev => { if (ev.target.value) choose(ev.target.value); });
$('prev').addEventListener('click', () => step(-1));
$('next').addEventListener('click', () => step(1));
$('unlinked').addEventListener('click', () => { showUnlinked = !showUnlinked; if (g) drawBrain(g); });
const pick = f => () => { if (f === 'changed' && $('f-chg').disabled) return; filter = f; if (g) drawBrain(g); };
$('f-all').addEventListener('click', pick('all'));
$('f-chg').addEventListener('click', pick('changed'));
$('f-w').addEventListener('click', pick('weight'));
$('f-wv').addEventListener('input', ev => {
  const v = parseFloat(ev.target.value);
  if (Number.isFinite(v) && v >= 0) { threshold = v; filter = 'weight'; if (g) drawBrain(g); }
});
const redrawAll = () => { if (g) { drawBody(g); drawGenomeGraph(g); drawBrain(g); } };
onResize(redrawAll);
// A figure is drawn to the pixels its panel has and the type size in force; when either changes without the window's resize event
// (a layout that reflows, a font that loads late) it is drawn again.
{
  let seen = new Map(), timer;
  const ro = new ResizeObserver(entries => {
    let changed = false;
    for (const e of entries) {
      const key = Math.round(e.contentRect.width) + 'x' + Math.round(e.contentRect.height) + '@' + rem();
      if (seen.get(e.target) !== undefined && seen.get(e.target) !== key) changed = true;
      seen.set(e.target, key);
    }
    if (changed) { clearTimeout(timer); timer = setTimeout(redrawAll, 100); }
  });
  for (const id of ['graphwrap', 'views', 'ggwrap']) ro.observe($(id));
}

// The room's reading of the machine takes seconds (the heat check runs a script): the page's own reading does not wait for it.
mountMachine().then(room => { if (room) pollSeconds = room.pollSeconds; });
poll(tick, pollSeconds);
