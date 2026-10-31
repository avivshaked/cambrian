// The run screen: one run's files, parsed by the room and drawn here. A panel whose file the run did
// not write says so in one line. Every name on screen is read from the data.

import { esc, getResource, callOp, bindOps, ask, poll, onResize, fmt, fmtSigned, ageText, clock, statusHtml, trialBadge, noData, mountMachine, machineText, refuse, getSel, setSel, actRow, showPollError } from './common.js';
import { el, frame, line, pts, liveDot, heroArea, runMean, runSe, band, extent, small, rem, niceTicks, watchBoxes } from './chart.js';

const $ = id => document.getElementById(id);
const params = new URLSearchParams(location.search);
let runId = params.get('id');
// Where the run on screen came from: the address (a link, or the RUN tab carrying the selection), what was last looked at (kept in this browser), or, only when
// there is nothing, the newest run, which the page says.
let runFrom = runId ? 'url' : null;
let data = null, drawn = null, logTab = 'log', tankDraw = 0;

// The control: a second run laid over this one (`&vs=` in the address keeps it), and the exam columns the reader pinned (`&pin=` keeps them, as brain indexes).
let controlId = params.get('vs') || null;
let ctl = null;                  // { id, data, cmp, diff, error } as last read; null with no control
let act = null;                  // the activity log's rows about this run, as last read (read while its tab is open)
let registryItems = [], registryReadAt = 0;
const parsePins = v => (v == null ? null : v === '' ? [] : v.split(',').map(x => (/^\d+$/.test(x.trim()) ? +x : NaN)).filter(Number.isInteger));
let pins = parsePins(params.get('pin'));

// ---- discovering what the rows hold

/** The value at a dotted path of a row, or null. */
const at = (row, path) => path.split('.').reduce((o, k) => (o && typeof o === 'object' ? o[k] : undefined), row);

/** Every numeric leaf under the given object keys (or at the top level when none), as dotted paths, in first-seen order. */
function leaves(rows, under) {
  const out = [];
  const seen = new Set();
  const walk = (o, prefix, depth) => {
    for (const [k, v] of Object.entries(o || {})) {
      const p = prefix ? prefix + '.' + k : k;
      if (typeof v === 'number') { if (!seen.has(p)) { seen.add(p); out.push(p); } }
      else if (v && typeof v === 'object' && !Array.isArray(v) && depth < 3) walk(v, p, depth + 1);
    }
  };
  for (const r of rows) {
    if (under) { const o = at(r, under); if (o && typeof o === 'object') walk(o, under, 1); }
    else for (const [k, v] of Object.entries(r)) if (typeof v === 'number' && !seen.has(k)) { seen.add(k); out.push(k); }
  }
  return out;
}

const series = (rows, path) => rows.map(r => { const v = at(r, path); return typeof v === 'number' && Number.isFinite(v) ? v : null; });

const genOf = rows => rows.map((r, i) => (typeof r.gen === 'number' ? r.gen : i));

/**
 * Measures to plot, the first being the default: the reward's mean per generation (tank-scores.jsonl, with
 * the top member beside it), else the top genome's score; then each NameMean field with its NameBest. Each
 * carries its series, the generations they belong to and the names of the generation-row fields it reads.
 */
function measures(d) {
  const rows = d.generations || [];
  const keys = leaves(rows);
  const out = [];
  const sc = (d.scores || []).filter(s => s.mean != null);
  if (sc.length) {
    const reward = d.reward ? ` (${d.reward})` : '';
    out.push({ id: 'reward.mean', label: `reward mean${reward}`, mean: sc.map(s => s.mean), best: sc.map(s => (s.top && typeof s.top.value === 'number' ? s.top.value : null)),
      bestLabel: 'top member', gens: sc.map(s => s.gen), source: 'tank-scores.jsonl' });
  }
  if (rows.some(r => typeof at(r, 'top.score') === 'number'))
    out.push({ id: 'top.score', label: "top genome's score", mean: series(rows, 'top.score'), best: null, gens: genOf(rows), reward: true, source: 'tank-generations.jsonl' });
  for (const k of keys) {
    if (!k.endsWith('Mean')) continue;
    const stem = k.slice(0, -4), bk = keys.includes(stem + 'Best') ? stem + 'Best' : null;
    out.push({ id: k, label: stem + ' (mean)', mean: series(rows, k), best: bk ? series(rows, bk) : null, bestLabel: bk, gens: genOf(rows), meanKey: k, bestKey: bk, source: 'tank-generations.jsonl' });
  }
  return out;
}

// ---- drawing

function drawBand(d) {
  const p = d.progress || {}, st = d.state || {};
  // The registry number beside the name; a run that is only a folder has its folder's name, which is its name, so it is said once (and the folder's name only where it differs from the run's).
  const tag = d.number != null ? '#' + d.number : (d.id !== d.name ? d.id : null);
  $('name').innerHTML = `${tag ? `<small>${esc(tag)}</small>` : ''}${esc(d.name)}`;
  $('desc').textContent = d.description || d.folderShort || d.folder || '';
  $('desc').title = d.folder || '';
  // What the registry says of a folder that has no run.json is said as it is ("completed, from the registry; this run predates run.json"), never as "not recorded" alone.
  const stall = d.stall || {};
  const limitText = stall.limitSeconds != null ? ageText(stall.limitSeconds) : null;
  const stallWhole = stall.stalled
    ? `This run is marked running, and none of its files has been written for ${ageText(stall.sinceWriteSeconds)} (the last write: ${stall.lastWriteFile || 'a file'}). The alarm is up past three times its median generation time (${ageText(stall.medianGenerationSeconds)} × 3 = ${limitText}).`
    : stall.evaluated ? `The alarm goes up past three times this run's median generation time (${ageText(stall.medianGenerationSeconds)} × 3 = ${limitText}) with no file written. The last write was ${ageText(stall.sinceWriteSeconds)} ago (${stall.lastWriteFile || 'a file'}).` : (stall.reason || '');
  $('state').innerHTML = `<span class="state">${statusHtml(st.phase)}${stall.stalled ? `<span class="plate plate--alarm" title="${esc(stallWhole)}">STALLED</span>` : ''}</span>${d.trial ? ' ' + trialBadge() : ''}` +
    (st.fromRegistry ? `<span class="ds fromreg" title="${esc(st.note || '')}">${esc(st.note || '')}</span>` : '');
  $('why').textContent = runFrom === 'kept' ? 'No run was named in the address, so this is the run last looked at. Pick another in the registry.'
    : runFrom === 'newest' ? 'No run was chosen yet, so this is the newest one. Pick another in the registry.' : '';
  // RERUN honours the registry's own word: an entry that cannot be rerun, and a reference entry, are not offered one that will be refused.
  const rerunBtn = document.querySelector('[data-op="run.rerun"]');
  if (rerunBtn) {
    const e = d.entry;
    const why = d.number == null ? 'only a registry entry can be rerun: this run has no entry (it is listed by room.json, or was started here)'
      : e && e.reference ? 'a reference entry is a check, not a rerun'
      : e && e.rerunnable === false ? (e.notRerunnableBecause || 'this entry cannot be rerun') : null;
    rerunBtn.disabled = !!why;
    rerunBtn.classList.toggle('btn--off', !!why);
    rerunBtn.title = why || 'run this entry again on this build, into a new folder';
    $('rerun-why').textContent = why || '';
    $('rerun-why').title = why || '';
  }
  // STOP is offered while the record says the run runs. For a run that is not running it is truly disabled (it cannot be clicked or tabbed to), with the reason as its
  // title and beside it, as RERUN is.
  const stopBtn = document.querySelector('[data-op="run.stop"]');
  if (stopBtn) {
    const live = ['search', 'exam', 'running'].includes(st.phase);
    const phase = String(st.phase || 'unknown').toLowerCase();
    const none = ['unrecorded', 'empty', 'missing'].includes(st.phase);
    // the line beside the button is short; the title says it whole
    const why = live ? null : none ? 'not running: no record of a run' : `not running: ${phase}`;
    const whole = live ? null : none ? 'this run has no record of running, so there is nothing to stop' : `this run is ${phase}${st.fromRegistry ? ' (' + st.note + ')' : ''}, so there is nothing to stop`;
    stopBtn.disabled = !!why;
    stopBtn.classList.toggle('btn--off', !!why);
    stopBtn.setAttribute('aria-disabled', why ? 'true' : 'false');
    stopBtn.title = whole || 'write a STOP file: the run ends between generations';
    $('stop-why').textContent = why || '';
    $('stop-why').title = whole || '';
  }
  if (st.note) $('state').title = st.note;
  // Each stat is one column of the band's grid (see #stats in run.html): labels share a row and numbers a baseline.
  let col = 0;
  // (a stat is display: contents, so a title goes on the label and the number, which have boxes)
  const stat = (k, v, sm, extra = '', cls = '', title = '') => { const t = title ? ` title="${esc(title)}"` : ''; return `<div class="stat ${cls}" style="--c:${++col}"><span class="k"${t}>${esc(k)}</span><span class="v ${sm ? 'sm' : ''}"${t}>${v}</span>${extra}</div>`; };
  const parts = [];
  const inExam = st.phase === 'exam';
  parts.push(stat('generations', p.lastGen == null ? '—' : `${p.lastGen}${p.planned != null ? `<small>of ${p.planned}</small>` : ''}`, false, '', 'lit'));
  // The exam's progress: the brains whose every genome's row is written, of the brains its plan names (a finished run says "18 of 18"). While it swims, the state above says so, the
  // end is estimated beside it and the process is shown alive or gone: whether a live run is alive is what the band is for.
  if (p.examBrains != null || (inExam && p.examExpected != null)) {
    const done = p.examDone != null ? p.examDone : (inExam ? null : p.examBrains);
    parts.push(stat('exam brains', `${done == null ? '—' : done}${p.examExpected != null ? `<small>of ${p.examExpected}</small>` : ''}`, false,
      p.examGenomes != null ? `<span class="watch">${p.examGenomes} genomes each${done != null && p.examBrains > done ? ` · brain ${done + 1} swimming` : ''}</span>` : '', inExam ? 'lit' : '',
      `brains whose every genome's row is in tank-exam.jsonl, of the brains the exam's plan names${p.examGenomes != null ? ` (${p.examGenomes} genomes each)` : ''}`));
  }
  parts.push(stat('pace', p.paceMinutes == null ? '—' : `${fmt(p.paceMinutes, 3)}<small>min / gen</small>`, true));
  // An exam under way: when it ends, at the pace of its rows so far, and what that stands on (in the title, and in the watch line in short).
  const en = d.examEnd;
  if (en) {
    const when = en.endsUtc ? whenText(en.endsUtc) : null;
    const left = en.endsUtc ? ageText(Math.max(0, (Date.parse(en.endsUtc) - Date.now()) / 1000)) : null;
    parts.push(stat('exam ends', en.state === 'estimated' ? `≈ ${when}` : en.state === 'complete' ? 'written' : '—', true,
      `<span class="watch" title="${esc(en.basis || en.reason || '')}">${en.state === 'estimated' ? `${en.rowsDone} of ${en.rowsExpected} rows · ${fmt(en.secondsPerRow, 3)} s a row · in ${esc(left)}` : en.state === 'complete' ? `${en.rowsDone} rows` : 'no estimate: ' + esc(shortReason(en.reason))}</span>`, ''));
  }
  parts.push(stat('last row', `<span id="age">${ageText(p.lastRowAgeSeconds)}</span>`, true,
    `<span class="watch" title="${esc(stallWhole || "the generation file's last write")}">${stall.evaluated ? `stall alarm past ${esc(limitText)} of silence` : "the generation file's last write"}</span>`));
  if (stall.stalled) parts.push(stat('stalled', `no write for ${esc(ageText(stall.sinceWriteSeconds))}`, true, `<span class="watch" title="${esc(stallWhole)}">limit ${esc(limitText)} · last: ${esc(stall.lastWriteFile || '—')}</span>`, 'alarm'));
  if (d.reward) parts.push(stat('reward', esc(d.reward), true, '', 'opt'));
  if (st.processId != null) parts.push(stat('process', `${fmt(st.processId, 8)}<small>${st.processAlive === true ? 'alive' : st.processAlive === false ? 'gone' : ''}</small>`, true, '', ['search', 'exam', 'running'].includes(st.phase) ? '' : 'opt'));
  $('stats').innerHTML = parts.join('');
  drawControlBox(d);
}

/** A clock time in this browser's zone: HH:MM today, with the date on another day. */
function whenText(iso) {
  const t = new Date(iso);
  if (isNaN(t)) return '—';
  const p = n => String(n).padStart(2, '0'), hm = p(t.getHours()) + ':' + p(t.getMinutes());
  return t.toDateString() === new Date().toDateString() ? hm : p(t.getMonth() + 1) + '-' + p(t.getDate()) + ' ' + hm;
}

const shortReason = r => (/no brain count|no recipe/.test(r || '') ? 'the run has no recipe' : /no exam row/.test(r || '') ? 'no row yet' : /start/.test(r || '') ? "the exam's start is unknown" : 'see the title');

/** A series laid out by generation number: slot g holds generation g's value, and null where the run has none. */
function byGen(vals, gs, n) {
  const out = Array(n).fill(null);
  vals.forEach((v, i) => { const g = gs[i]; if (Number.isInteger(g) && g >= 0 && g < n) out[g] = v; });
  return out;
}

/** The control's series for the measure on screen: the same measure, found by its id in the control's own rows; null when it records none. */
function controlMeasure(id) {
  if (!ctl || !ctl.data || ctl.error) return null;
  return measures(ctl.data).find(x => x.id === id) || null;
}

const ctlName = c => (c && c.data ? `${c.data.number != null ? '#' + c.data.number + ' ' : ''}${c.data.name || c.data.id}` : c ? String(c.id) : '');

function drawHero(d) {
  const rows = d.generations || [];
  const ms = measures(d);
  const sel = $('measure');
  const wasValue = sel.value;
  const body = $('hero-body');
  $('measure-wrap').style.display = ms.length ? '' : 'none';   // an empty picker is not drawn (it showed as a white box)
  if (!ms.length) {
    $('hero-legend').innerHTML = '';
    sel.innerHTML = '';
    body.innerHTML = noData(rows.length ? 'No numeric measure in the generation rows.' : 'Not recorded by this run: no tank-generations.jsonl or tank-scores.jsonl rows yet.');
    return;
  }
  if (!body.querySelector('svg')) {
    body.innerHTML = '<div class="hero-charts"><svg class="chart" id="c-search"></svg></div><div class="decades"><div class="kicker" style="margin-bottom:.4rem" id="dec-title">decades</div><table class="t" id="t-dec"></table></div>';
  }
  sel.innerHTML = ms.map(m => `<option value="${esc(m.id)}">measure: ${esc(m.label)}</option>`).join('');
  sel.value = ms.some(m => m.id === wasValue) ? wasValue : ms[0].id;
  const m = ms.find(x => x.id === sel.value);
  let gens = m.gens, mean = m.mean, best = m.best;
  // With a control, both runs are laid out by generation number, so that a generation is the same place in each.
  const cm0 = controlMeasure(m.id);
  let cmean = null;
  if (cm0) {
    const n = Math.max(...m.gens, ...cm0.gens, -1) + 1;
    mean = byGen(m.mean, m.gens, n); best = m.best ? byGen(m.best, m.gens, n) : null; cmean = byGen(cm0.mean, cm0.gens, n);
    gens = [...Array(n).keys()];
  }
  const smooth = runMean(mean, 10);
  const cname = ctlName(ctl);
  const rewardsDiffer = cm0 && m.id === 'reward.mean' && d.reward && ctl.data.reward && d.reward !== ctl.data.reward;

  $('hero-legend').innerHTML =
    `<span><i style="background:var(--run)"></i>10-generation mean of ${esc(m.label)}</span>` +
    `<span><i style="background:var(--run);height:1px;opacity:.5"></i>each generation</span>` +
    (best ? `<span><i style="background:var(--ink-lo);height:1px"></i>${esc(m.bestLabel || 'best')}</span>` : '') +
    (cm0 ? `<span><i class="band" style="background:var(--ctl)"></i>control ${esc(cname)}, 10-generation mean ± se</span>` : '') +
    (ctl && !cm0 ? `<span class="dim">control ${esc(cname)}: ${ctl.error ? esc(ctl.error) : 'its files hold no ' + esc(m.label)}</span>` : '') +
    (rewardsDiffer ? `<span class="dim">the rewards differ (${esc(d.reward)}, the control's ${esc(ctl.data.reward)}): pick a measure both record to compare them</span>` : '') +
    (cm0 ? '<span class="dim">matched on generation number</span>' : '') +
    (m.reward && d.reward ? `<span class="dim">reward ${esc(d.reward)}</span>` : '') +
    `<span class="dim">from ${esc(m.source)}</span>`;

  const svg = $('c-search');
  const [lo, hi] = extent([...(best ? [mean, best] : [mean]), ...(cmean ? [cmean] : [])]);
  const c = frame(svg, { n: mean.length, lo, hi, xvals: gens, unit: m.label });
  let cm = null;
  if (cmean) { cm = runMean(cmean, 10); band(c, cm, runSe(cmean, 10)); line(c, cm, 'ctl-line'); }   // the control's, under this run's
  if (best) line(c, best, 'line-lo');
  heroArea(c, smooth, lo, 'hg');
  line(c, mean, 'run-raw');
  line(c, smooth, 'run-line');
  const i = liveDot(c, smooth, .35);
  if (i >= 0) el(c.svg, 'text', { x: c.X(i) - .5 * c.R, y: c.Y(smooth[i]) - .8 * c.R, 'text-anchor': 'end', class: 'halo', style: 'fill:var(--run);font-size:1rem' }, fmt(smooth[i], 4));
  if (cm) {
    const j = cm.length - 1 - [...cm].reverse().findIndex(v => v != null);
    if (j >= 0 && j < cm.length) {
      // The label sits on the side of its point away from this run's line, and runs rightward from a point near the left edge (it would be cut off otherwise).
      const above = i >= 0 && cm[j] > smooth[i], left = c.X(j) - c.X(0) < .3 * (c.X(mean.length - 1) - c.X(0));
      el(c.svg, 'text', { x: c.X(j) + (left ? .5 : -.5) * c.R, y: c.Y(cm[j]) + (above ? -.8 : 1.4) * c.R, 'text-anchor': left ? 'start' : 'end', class: 'halo', style: 'fill:var(--ctl);font-size:.85rem' }, `${cname} ${fmt(cm[j], 4)}`);
    }
  }

  // decades: blocks of ten generations by index (by generation number, with a control)
  $('dec-title').textContent = `decades · ${m.label}`;
  $('dec-title').title = `decades · ${m.label}`;
  let h = cmean ? '<tr><th>gens</th><th>run</th><th>ctl</th><th title="this run less the control, over the block; the title of a row says in how many of its generations this run was ahead">Δ</th></tr>'
    : `<tr><th>gens</th><th>mean</th><th>${best ? 'best' : 'max'}</th><th>Δ</th></tr>`;
  let prev = null;
  for (let k = 0; k * 10 < mean.length; k++) {
    const idx = [...Array(10).keys()].map(j => k * 10 + j).filter(j => j < mean.length && mean[j] != null);
    if (!idx.length) continue;
    const mm = idx.reduce((s, j) => s + mean[j], 0) / idx.length;
    if (cmean) {
      const both = idx.filter(j => cmean[j] != null);
      const cmm = both.length ? both.reduce((s, j) => s + cmean[j], 0) / both.length : null;
      const ahead = both.filter(j => mean[j] > cmean[j]).length;
      const mine = both.length ? both.reduce((s, j) => s + mean[j], 0) / both.length : null;
      h += `<tr><td>${gens[idx[0]]}–${gens[idx[idx.length - 1]]}</td><td class="hi">${fmt(mm, 4)}</td><td>${cmm == null ? '—' : fmt(cmm, 4)}</td><td class="hi" title="${both.length ? `ahead in ${ahead} of ${both.length} generations both runs have` : 'the control has no generation here'}">${cmm == null ? '—' : fmtSigned(mine - cmm, 3)}</td></tr>`;
      continue;
    }
    const bb = Math.max(...idx.map(j => (best ? best[j] : mean[j]) ?? -Infinity));
    h += `<tr><td>${gens[idx[0]]}–${gens[idx[idx.length - 1]]}</td><td class="hi">${fmt(mm, 4)}</td><td>${fmt(bb, 4)}</td><td class="hi">${prev == null ? '—' : fmtSigned(mm - prev, 3)}</td></tr>`;
    prev = mm;
  }
  $('t-dec').innerHTML = h;
}

/** A heading's text with a line break allowed between the words of a CamelCase name, so that a long name wraps whole and is never cut. */
const wbr = s => esc(s).replace(/([a-z])([A-Z])/g, '$1<wbr>$2');

/** The pinned columns as brain indexes (never labels): what the address names, else the best constant and the first knockout, found by the kind each heading carries. */
function defaultPins(e) {
  const pooled = label => e.pooled.find(x => x.brain === label);
  let bestConst = null;
  for (const h of e.headers) {
    if (h.kind !== 'constant' || h.label === e.own) continue;
    const c = pooled(h.label);
    // the best constant is the one the own brain beats least: the smallest difference
    if (c && c.diff != null && (!bestConst || c.diff < bestConst.diff)) bestConst = { index: h.index, diff: c.diff };
  }
  const ko = e.headers.find(h => h.kind === 'knockout');
  return [bestConst && bestConst.index, ko && ko.index].filter(x => x != null);
}

function pinnedOf(e) {
  const valid = i => Number.isInteger(i) && i >= 0 && i < e.brains.length && e.brains[i] !== e.own;
  return pins == null ? defaultPins(e) : [...new Set(pins.filter(valid))];
}

/** The dots of one brain's column: this run's pooled difference (filled) and the control's (open), each with its standard error, on the scale every column shares. */
function dotsCell(rs, cs, lo, hi, label) {
  const W = 62, Hh = 89, pad = 9, Y = v => pad + (hi - v) / (hi - lo) * (Hh - 2 * pad);
  const bar = (x, side, stroke, fill) => side && side.diff != null
    ? (side.se != null ? `<line x1="${x}" x2="${x}" y1="${Y(side.diff + side.se).toFixed(1)}" y2="${Y(side.diff - side.se).toFixed(1)}" stroke="${stroke}" stroke-width="1.6"/>` : '') +
      `<circle cx="${x}" cy="${Y(side.diff).toFixed(1)}" r="3.3" fill="${fill}" stroke="${stroke}" stroke-width="1.5"/>` : '';
  const say = (n, s) => (s && s.diff != null ? `${n} ${fmtSigned(s.diff, 3)}${s.se != null ? ' ±' + fmt(s.se, 2) : ''}` : `${n} —`);
  return `<svg viewBox="0 0 ${W} ${Hh}" role="img" aria-label="${esc(label)}: ${esc(say('this run', rs))}, ${esc(say('control', cs))}"><title>${esc(label)} · ${esc(say('this run', rs))} · ${esc(say('control', cs))}</title>` +
    (lo < 0 && hi > 0 ? `<line class="zero" x1="0" x2="${W}" y1="${Y(0).toFixed(1)}" y2="${Y(0).toFixed(1)}"/>` : '') +
    bar(22, cs, 'var(--ctl)', 'none') + bar(40, rs, 'var(--run-mid)', 'var(--run)') + '</svg>';
}

function axisCell(lo, hi) {
  const W = 144, Hh = 89, pad = 9, Y = v => pad + (hi - v) / (hi - lo) * (Hh - 2 * pad);
  return `<svg viewBox="0 0 ${W} ${Hh}" aria-hidden="true">` + niceTicks(lo, hi, 3).map(t => `<line class="gridl" x1="${W - 6}" x2="${W}" y1="${Y(t).toFixed(1)}" y2="${Y(t).toFixed(1)}"/><text x="${W - 9}" y="${(Y(t) + 3).toFixed(1)}" text-anchor="end">${esc(fmt(t, 3))}</text>`).join('') +
    `<text x="4" y="10" style="fill:var(--ink-dim)">pooled difference</text></svg>`;
}

/** The pin menu: every column but the own brain's, each checked where pinned. */
function fillPinMenu(e, H) {
  const m = $('pinmenu'), cur = pinnedOf(e);
  m.innerHTML = '<div class="pm-t">pin columns: they lead the table</div>' +
    e.brains.map((c, i) => [c, i]).filter(([c]) => c !== e.own).map(([c, i]) =>
      `<label title="${esc(H(c).title)}"><input type="checkbox" data-pin="${i}"${cur.includes(i) ? ' checked' : ''}><span>${esc(H(c).short)}</span><small>${esc(H(c).title)}</small></label>`).join('') +
    '<div class="pm-row"><button class="btn" type="button" data-pin-default title="the best constant and the first knockout">DEFAULTS</button><button class="btn" type="button" data-pin-none style="margin-left:.5rem">NONE</button><button class="btn" type="button" data-pin-close style="margin-left:auto">CLOSE</button></div>';
  m.querySelectorAll('input[data-pin]').forEach(x => x.addEventListener('change', () => {
    const i = +x.dataset.pin, now = pinnedOf(e).filter(k => k !== i);
    if (x.checked) now.push(i);
    pins = now; syncUrl(); drawExam(data);
  }));
  m.querySelector('[data-pin-default]').addEventListener('click', () => { pins = null; syncUrl(); drawExam(data); });
  m.querySelector('[data-pin-none]').addEventListener('click', () => { pins = []; syncUrl(); drawExam(data); });
  m.querySelector('[data-pin-close]').addEventListener('click', () => setPinMenu(false));
}

function setPinMenu(open) {
  $('pinmenu').hidden = !open;
  $('pin-btn').setAttribute('aria-expanded', open ? 'true' : 'false');
}

/**
 * The own brain's net on each body, this run against the control: one compact plot under the exam table, with a one-line label. Each run is on its own genomes and draws, so the
 * two are not paired; the standard error is of each mean. Where the runs' bodies differ the plot says so in a line under it. (The by-brain rows of the table are the other reading.)
 */
function bodyNetHtml(cmp) {
  if (!cmp || !cmp.bodies || !cmp.bodies.length) return '';
  const hRem = Math.min(cmp.bodies.length * .85 + 1.5, 12).toFixed(1);
  return `<div class="bodynet" id="bodynet" style="--bn-h:${hRem}rem"><div class="bn-head"><span class="kicker">by body</span><span>own brain's net: this run <b class="hi" style="font-weight:400">●</b>, the control <b style="color:var(--ctl);font-weight:400">○</b>, ± one se; each on its own draws, not paired</span></div>` +
    `<svg class="chart" id="c-bodynet" role="img" aria-label="own brain's net on each body, this run and the control"></svg>` +
    (cmp.bodiesNote ? `<p class="bn-note" title="${esc(cmp.bodiesNote)}">${esc(cmp.bodiesNote)}.</p>` : '') + '</div>';
}

function drawBodyNet() {
  const svg = $('c-bodynet');
  if (!svg || !ctl || !ctl.cmp || !ctl.cmp.available) return;
  const bodies = ctl.cmp.comparison.bodies || [];
  svg.innerHTML = '';
  const R = rem(), w = svg.clientWidth, h = svg.clientHeight;
  if (!w || !h || !bodies.length) return;
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  const vals = [];
  for (const b of bodies) for (const s of [b.run, b.control]) if (s && s.net != null) { vals.push(s.net); if (s.se != null) vals.push(s.net - s.se, s.net + s.se); }
  if (!vals.length) { el(svg, 'text', { x: 0, y: 1.2 * R }, 'no body has a net in both exams'); return; }
  let lo = Math.min(...vals), hi = Math.max(...vals);
  const pad = ((hi - lo) || Math.abs(hi) || 1) * .06; lo -= pad; hi += pad;
  const L = 4.2 * R, padR = 1.2 * R, axisH = 1.4 * R;
  const X = v => L + (v - lo) / (hi - lo) * (w - L - padR), rh = (h - axisH) / bodies.length;
  for (const t of niceTicks(lo, hi, 5)) {
    el(svg, 'line', { class: 'gridl', x1: X(t), x2: X(t), y1: 0, y2: h - axisH + 3 });
    el(svg, 'text', { x: X(t), y: h - .2 * R, 'text-anchor': 'middle' }, fmt(t, 3));
  }
  const say = (n, s) => (s && s.net != null ? `${n} ${fmt(s.net, 4)}${s.se != null ? ' ± ' + fmt(s.se, 2) : ''} (${s.n} ${s.over}${s.over === 'draws' ? ' of ' + s.genomes + ' genome' + (s.genomes === 1 ? '' : 's') : ''})` : `${n}: not in its exam`);
  bodies.forEach((b, i) => {
    const y = rh * i + rh / 2, g = el(svg, 'g', {});
    el(g, 'title', {}, `body ${b.body} · ${say('this run', b.run)} · ${say('the control', b.control)}`);
    el(g, 'text', { x: 0, y: y + .3 * R }, 'body ' + b.body);
    el(g, 'line', { x1: L, x2: w - padR, y1: y, y2: y, style: 'stroke:var(--track)' });
    const mark = (s, stroke, fill, r) => {
      if (!s || s.net == null) return;
      if (s.se != null) el(g, 'line', { x1: X(s.net - s.se), x2: X(s.net + s.se), y1: y, y2: y, style: `stroke:${stroke};stroke-width:1.6` });
      el(g, 'circle', { cx: X(s.net), cy: y, r: r * R, style: `fill:${fill};stroke:${stroke};stroke-width:1.5` });
    };
    mark(b.control, 'var(--ctl)', 'var(--panel-bot)', .28);
    mark(b.run, 'var(--run-mid)', 'var(--run)', .3);
  });
}

function drawExam(d) {
  const b = $('b-exam');
  const e = d.exam;
  const page = document.querySelector('main.page');
  // A run with no exam file, or an exam of one brain (nothing to pair it with), gets a short panel that says so (the page's grid gives it a row of its own, as high as its text).
  const narrow = !!e && e.brains.length < 2;
  page.classList.toggle('noexam', !e || narrow);
  $('pin-btn').hidden = !e || narrow;
  $('pinned').textContent = '';
  if (!e || narrow) setPinMenu(false);
  if (!e) {
    const ph = (d.state || {}).phase;
    b.innerHTML = noData(ph === 'search' ? 'The exam has not started: tank-exam.jsonl appears when the search ends.' : 'Not recorded by this run: no tank-exam.jsonl.');
    return;
  }
  const own = x => (e.basis === 'score' ? x.ownScore : (x.ownNet ?? x.ownScore));
  if (narrow) {
    const cmpN = ctl && ctl.cmp && ctl.cmp.available ? ctl.cmp.comparison : null;
    b.innerHTML = `<p class="note nodata">The exam swam one brain (${esc(e.own)}), so there is no control to set it against: nothing is paired, and no standard error or tint can be drawn. ` +
      `Its own ${e.basis === 'score' ? 'score' : 'net'}, a genome at a time: ${e.bodies.map(r => `<a class="glink" href="genome.html?run=${encodeURIComponent(d.id)}&id=exam.${r.genome}" title="open this genome">${r.genome}</a>${r.body == null ? '' : ' (body ' + r.body + ')'} <span class="hi">${fmt(own(r), 4)}</span>`).join(' · ')}.</p>` +
      (ctl && ctl.error ? `<p class="note"><span class="hi">The control cannot be read:</span> ${esc(ctl.error)}</p>` : '') + bodyNetHtml(cmpN);
    drawBodyNet();
    return;
  }
  const hdr = new Map((e.headers || []).map(h => [h.label, h]));
  const H = c => hdr.get(c) || { label: c, short: c, title: c, kind: '' };
  const pinList = pinnedOf(e);
  const rest = e.brains.map((c, i) => i).filter(i => e.brains[i] !== e.own && !pinList.includes(i));
  const cols = [...pinList, ...rest].map(i => e.brains[i]);
  const basis = { per: 'net per held-out draw, paired draw by draw', score: "the reward's value (score), one number a genome", net: 'net, one number a genome' }[e.basis] || e.basis;
  const paired = e.basis === 'per';
  // The control's exam beside this run's: the room's own comparison (nursery://exam-compare), brain by brain, by label.
  const cmp = ctl && ctl.cmp && ctl.cmp.available ? ctl.cmp.comparison : null;
  const cmpOf = c => (cmp ? cmp.brains.find(x => x.label === c) : null);
  // A control is uninformative for a genome when it leaves that genome exactly as it is: its brain then swims as own does, and own less it is zero by construction. The run's
  // resource lists, per genome, the columns whose control cuts nothing from it (uninformative: a knockout of channels the genome reads at no port, brainless on a genome with no
  // sense link); no other kind is ever listed. A cell is greyed where its genome lists its column; a heading, the pooled cells and the comparison cells only when every genome does.
  const unin = d.uninformative && Array.isArray(d.uninformative.controls) && Array.isArray(d.uninformative.genomes) ? d.uninformative : null;
  const ctlOf = new Map((unin ? unin.controls : []).map(x => [x.col, x]));
  const offSet = new Map((unin ? unin.genomes : []).map(x => [x.genome, new Set(x.columns)]));
  const listOf = a => (a.length < 2 ? a.join('') : a.slice(0, -1).join(', ') + ' and ' + a[a.length - 1]);
  const offCell = (c, genome) => { const i = e.brains.indexOf(c); return ctlOf.has(i) && offSet.has(genome) && offSet.get(genome).has(i); };
  const offCol = c => e.bodies.length > 0 && e.bodies.every(r => offCell(c, r.genome));
  const offCount = c => e.bodies.filter(r => offCell(c, r.genome)).length;
  const offKnockout = c => { const x = ctlOf.get(e.brains.indexOf(c)); return x && x.kind === 'brainless' ? null : x; };
  // The reason, for one genome (a cell's title) and for the genomes the column is greyed on (the note and a greyed heading's title).
  const offOne = c => { const x = offKnockout(c); return x ? `this genome reads no port of ${listOf(x.channels)}, so the knockout leaves it unchanged and its difference is zero by construction` : 'this genome has no sense link, so brainless leaves it unchanged and its difference is zero by construction'; };
  // The reason for a column greyed whole (every genome), which a greyed heading, pooled cell and comparison cell give in its title.
  const offText = c => { const x = offKnockout(c); return x ? `no genome of this exam reads a port of ${listOf(x.channels)}, so the knockout leaves every genome unchanged and its difference is zero by construction` : 'no genome of this exam has a sense link, so brainless leaves every genome unchanged and its difference is zero by construction'; };
  const greyed = cols.filter(c => offCount(c) > 0);
  const offNote = greyed.length
    ? `<p class="note compact">${greyed.map(c => {
      const x = offKnockout(c), n = offCount(c), all = offCol(c);
      const some = n === 1 ? ['that genome', 'it'] : ['those genomes', 'them'];
      const why = all ? offText(c)
        : x ? `${some[0]} ${n === 1 ? 'reads' : 'read'} no port of ${listOf(x.channels)}, so the knockout leaves ${some[1]} unchanged and its difference is zero by construction`
        : `${some[0]} ${n === 1 ? 'has' : 'have'} no sense link, so brainless leaves ${some[1]} unchanged and its difference is zero by construction`;
      return `<span class="hi">${esc(H(c).short)} is greyed${all ? '' : ` in ${n} of its ${e.bodies.length} genomes`}:</span> ${esc(why)}.`;
    }).join(' ')}</p>` : '';
  // Each brain's heading opens the tank screen on that brain (matched by its place in the file, never by its label) and the draw picked below. A heading is a short
  // form of the label that never cuts a word (every column on one screen at 1920 wide), with the whole label in its title.
  const tankLink = c => { const i = e.brains.indexOf(c); return `<a class="glink" data-tank="${i}" href="${tankHref(d.id, i)}" title="${esc(H(c).title)}${offCol(c) ? ' (greyed: ' + esc(offText(c)) + ')' : ''} — open the tank screen on this brain and the draw picked below: re-step the episode and watch it">${wbr(H(c).short)}</a>`; };
  let h = `<tr><th title="the genome's number in the exam, and its body">genome · body</th><th title="${esc(e.own)}${e.hasFresh ? ', and its fresh draws' : ''}">${tankLink(e.own)}${e.hasFresh ? '<span class="fr">· fresh</span>' : ''}</th>` +
    cols.map(c => `<th class="${pinList.includes(e.brains.indexOf(c)) ? 'lead' : ''}${offCol(c) ? ' off' : ''}">${tankLink(c)}</th>`).join('') + '</tr>';
  const cell = (c, withFresh, genome) => {
    if (!c || c.diff == null) return '<td class="dim">—</td>';
    const off = offCell(c.brain, genome);
    return `<td class="${off ? 'off' : c.clears ? 'sig' : ''}"${off ? ` title="${esc(offOne(c.brain))}"` : ''}><span class="d">${fmtSigned(c.diff, 3)}</span>${c.se != null ? `<span class="se">±${fmt(c.se, 2)}</span>` : ''}${withFresh && e.hasFresh ? `<span class="fr">${fmt(c.fresh, 3)}</span>` : ''}</td>`;
  };
  const poolRow = (label, title) => `<tr class="pool"><td class="rowname" colspan="2" title="${esc(title)}">${esc(label)}</td>` + cols.map(c => {
    const cc = e.pooled.find(x => x.brain === c), off = offCol(c);
    return cc && cc.diff != null ? `<td class="${off ? 'off' : cc.clears ? 'sig' : ''}"${off ? ` title="${esc(offText(c))}"` : ''}><span class="d">${fmtSigned(cc.diff, 3)}</span>${cc.se != null ? `<span class="se">±${fmt(cc.se, 2)}</span>` : ''}</td>` : '<td>—</td>';
  }).join('') + '</tr>';
  // With a control: this run's pooled row, the control's, whether they differ, and the dots, lead the table in a block of their own, so that the comparison is on screen.
  let vs = '';
  if (cmp) {
    const vals = [];
    for (const c of cols) { const x = cmpOf(c); if (!x) continue; for (const s of [x.run, x.control]) if (s && s.diff != null) { vals.push(s.diff); if (s.se != null) vals.push(s.diff + s.se, s.diff - s.se); } }
    vals.push(0);
    let lo = Math.min(...vals), hi = Math.max(...vals);
    const padv = (hi - lo || 1) * .08; lo -= padv; hi += padv;
    // Only this run's side is greyed: the grey is read off this run's genomes, and says nothing of the control's.
    const sideCell = (s, cls, c, mine) => (s && s.diff != null ? `<td class="${mine && offCol(c) ? 'off' : cls}"${mine && offCol(c) ? ` title="${esc(offText(c))}"` : ''}><span class="d">${fmtSigned(s.diff, 3)}</span>${s.se != null ? `<span class="se">±${fmt(s.se, 2)}</span>` : ''}</td>` : '<td class="dim">—</td>');
    const name = esc(ctlName(ctl));
    vs = '<tbody class="vs">' +
      `<tr class="pool"><td class="rowname" colspan="2" title="by brain: this run's exam, pooled over its genomes: own brain less each brain, ± the standard error across genomes">this run, pooled</td>${cols.map(c => sideCell((cmpOf(c) || {}).run, '', c, true)).join('')}</tr>` +
      `<tr class="pool"><td class="rowname" colspan="2" title="by brain: the control's exam, pooled the same way: ${name}">control, pooled</td>${cols.map(c => sideCell((cmpOf(c) || {}).control, 'ctlcell', c)).join('')}</tr>` +
      `<tr class="pool"><td class="rowname" colspan="2" title="${esc(cmp.rule)}">differs from the control</td>` + cols.map(c => {
        const x = cmpOf(c);
        if (!x) return '<td class="dim">—</td>';
        const tip = x.delta != null && x.deltaSe != null
          ? `${c}: this run less the control = ${fmtSigned(x.delta, 4)}; standard error √(${fmt(x.run.se, 3)}² + ${fmt(x.control.se, 3)}²) = ${fmt(x.deltaSe, 3)}; two of them = ${fmt(2 * x.deltaSe, 3)}; ${x.differs === true ? 'the difference is more than that, so it differs' : x.differs === false ? 'the difference is not more than that, so it is marked as not differing' : (x.why || 'not judged')}. ${cmp.rule}.`
          : `${c}: ${x.why || 'not judged'}`;
        // Greyed only where the cell holds a value the knockout leaves at zero by construction: a "—" (the control did not swim this brain) holds none, and keeps its own title.
        const held = x.differs === true || x.differs === false, grey = held && offCol(c);
        return `<td class="${grey ? 'off' : x.differs === true ? 'differs' : x.differs === false ? '' : 'nocmp'}" title="${esc(tip)}${grey ? ' (greyed: ' + esc(offText(c)) + ')' : ''}"><span class="d">${x.differs === true ? 'differs' : x.differs === false ? 'no' : '—'}</span></td>`;
      }).join('') + '</tr>' +
      `<tr class="dots"><td colspan="2">${axisCell(lo, hi)}</td>${cols.map(c => `<td>${dotsCell((cmpOf(c) || {}).run, (cmpOf(c) || {}).control, lo, hi, c)}</td>`).join('')}</tr></tbody>`;
  }
  let rows = '<tbody>';
  for (const r of e.bodies) {
    // The genome's number in the exam is its line in the genomes file: the genome screen answers to exam.<n> for it.
    rows += `<tr><td class="hi"><a class="glink" href="genome.html?run=${encodeURIComponent(d.id)}&id=exam.${r.genome}" title="open this genome: its body, brain and lineage">${r.genome}</a>${r.body == null ? '' : ' · ' + r.body}</td><td class="hi"><span class="d">${fmt(own(r), 4)}</span>${e.hasFresh ? `<span class="fr">${fmt(r.ownFresh, 3)}</span>` : ''}</td>`;
    for (const c of cols) rows += cell(r.cells.find(x => x.brain === c), true, r.genome);
    rows += '</tr>';
  }
  if (!cmp) rows += poolRow('pooled', 'pooled over the genomes: own brain less this brain, ± the standard error across genomes');
  rows += '</tbody>';
  // Fixed widths: the genome column and the own column, then one narrow column a brain. The table is as wide as its columns need, and scrolls inside its panel when
  // the panel is narrower than that (at 1366), with the room's own scrollbar.
  const colgroup = `<colgroup><col style="width:4.6rem"><col style="width:4.4rem">${cols.map(() => '<col style="width:3.9rem">').join('')}</colgroup>`;
  const tableMin = (4.6 + 4.4 + 3.9 * cols.length).toFixed(1);
  // A control whose exam swam none of the brains this run sets against its own is said in one line of its own above the comparison rows, not only in the cells' titles.
  const sharedBrains = cmp ? cols.filter(c => (cmpOf(c) || {}).control).length : 0;
  const noShared = cmp && sharedBrains === 0
    ? `<p class="note compact"><span class="plate plate--alarm">NO BRAIN IN COMMON</span> <span class="hi">The control's exam swam none of the brains this run sets against its own</span> (${esc(cols.map(c => H(c).title).join(', ') || 'none')}); the control, ${esc(ctlName(ctl))}, swam ${esc(((ctl.data.exam && ctl.data.exam.brains) || []).join(', ') || 'nothing')}. Its rows below are empty and nothing is set against it.</p>` : '';
  const ctlNote = !ctl ? '' : ctl.error ? `<p class="note"><span class="hi">The control cannot be read:</span> ${esc(ctl.error)}</p>`
    : cmp ? (noShared || `<p class="note compact"><span class="hi">By brain, against the control ${esc(ctlName(ctl))}:</span> this run's pooled difference less the control's, ± √(se₁² + se₂²), <span style="color:var(--sig);background:var(--sig-bg);padding:0 .25rem">marked</span> where more than two of those apart; ${cmp.note ? esc(cmp.note) : `each pooled over its own genomes (${cmp.runGenomes} here, ${cmp.controlGenomes} there)`}.</p>`)
    : `<p class="note"><span class="hi">The exam is not set beside the control's:</span> ${esc((ctl.cmp && ctl.cmp.reason) || 'it could not be read')}.</p>`;
  b.innerHTML =
    `<p class="note compact">Own brain less each control${paired ? ', ± one standard error' : ''}, on ${esc(basis)}${d.reward ? `; reward ${esc(d.reward)}` : ''}. ` +
    (paired ? `<span style="color:var(--sig);background:var(--sig-bg);padding:0 .25rem">Tinted</span> where it clears two standard errors. `
            : 'One number a genome has no standard error, so no cell is tinted. ') +
    `Columns are the brains the file names, the pinned ones first.</p>${ctlNote}${offNote}` +
    (e.draws > 0 ? `<p class="guard" style="margin:0 0 .5rem"><label for="tank-draw">Re-step one episode in the tank: a brain's heading opens it on draw</label> <select id="tank-draw" class="tag" aria-label="the exam draw the tank screen opens on">${
      [...Array(e.draws).keys()].map(i => `<option value="${i}"${i === tankDraw ? ' selected' : ''}>${i}</option>`).join('')}</select> of ${e.draws}.</p>` : '') +
    `<div class="scroll"><table class="t exam-t" style="width:${tableMin}rem;min-width:${tableMin}rem">${colgroup}<thead>${h}</thead>${vs}${rows}</table></div>` + bodyNetHtml(cmp);
  drawBodyNet();
  const pick = document.getElementById('tank-draw');
  if (pick) pick.addEventListener('change', () => {
    tankDraw = +pick.value;
    b.querySelectorAll('a[data-tank]').forEach(a => { a.href = tankHref(d.id, a.dataset.tank); });
  });
  $('pinned').innerHTML = pinList.length ? `pinned first: <b>${pinList.map(i => esc(H(e.brains[i]).short)).join('</b> · <b>')}</b>` : 'nothing pinned';
  fillPinMenu(e, H);
}

/** The tank screen's address for one exam brain of a run, on the draw picked in the exam panel. */
function tankHref(id, brain) { return `tank.html?run=${encodeURIComponent(id)}&brain=${brain}&draw=${tankDraw}`; }

function drawEnergy(d) {
  const b = $('b-energy');
  const rows = d.generations || [];
  const m = measures(d);
  const used = new Set(m.flatMap(x => [x.meanKey, x.bestKey]).filter(Boolean));
  const keys = leaves(rows).filter(k => !used.has(k) && k !== 'gen' && k !== 'seconds');
  if (!rows.length || !keys.length) { b.innerHTML = noData(rows.length ? 'No other numeric field in the generation rows.' : 'Not recorded by this run: no generation rows yet.'); return; }
  const gens = rows.map((r, i) => (typeof r.gen === 'number' ? r.gen : i));
  b.innerHTML = `<div class="mult">${keys.map((k, i) => {
    const s = series(rows, k), last = [...s].reverse().find(v => v != null);
    return `<div><span class="lab">${esc(k)}<b>${fmt(last, 4)}</b></span><svg class="chart" data-k="${i}"></svg></div>`;
  }).join('')}</div>`;
  b.querySelectorAll('svg').forEach(svg => small(svg, [series(rows, keys[+svg.dataset.k])], { xvals: gens }));
}

const PART_COLORS = ['var(--p1)', 'var(--p2)', '#a08cff', '#e6cf86', '#8fe388', '#ff7eb6'];

function drawParts(d) {
  const b = $('b-parts');
  const sc = (d.scores || []).filter(s => s.top);
  if (!sc.length) { b.innerHTML = noData('Not recorded by this run: no tank-scores.jsonl.'); return; }
  const names = [];
  for (const s of sc) for (const k of Object.keys(s.top.parts || {})) if (!names.includes(k)) names.push(k);
  const value = sc.map(s => s.top.value);
  const parts = names.map(n => sc.map(s => (s.top.parts || {})[n] ?? null));
  b.innerHTML = `<div class="legend">${names.map((n, i) => `<span><i style="background:${PART_COLORS[i % PART_COLORS.length]}"></i>${esc(n)}</span>`).join('')}` +
    `<span><i style="background:var(--ink-hi)"></i>value${d.reward ? ' · ' + esc(d.reward) : ''}</span></div><svg class="chart" id="c-parts"></svg>` +
    `<div class="legend dim" style="margin:.3rem 0 0">the top member each generation · tank-scores.jsonl</div>`;
  const [lo, hi] = extent([value, ...parts]);
  const c = frame($('c-parts'), { n: sc.length, lo, hi, xvals: sc.map(s => s.gen), padL: 2.9, padB: 1.2 });
  parts.forEach((p, i) => line(c, runMean(p, 10), '', `fill:none;stroke:${PART_COLORS[i % PART_COLORS.length]};stroke-width:1.4`));
  if (lo < 0 && hi > 0) el(c.svg, 'line', { class: 'zero', x1: c.padL, x2: c.w - c.padR, y1: c.Y(0), y2: c.Y(0) });
  line(c, runMean(value, 10), '', 'fill:none;stroke:var(--ink-hi);stroke-width:1.8');
}

function drawLeader(d) {
  const b = $('b-leader');
  const rows = d.generations || [];
  const bodies = rows.map(r => at(r, 'top.body'));
  const uniq = [...new Set(bodies.filter(x => typeof x === 'number'))].sort((a, z) => a - z);
  if (!uniq.length) { b.innerHTML = noData(rows.length ? 'The generation rows do not name a leading body (no top.body).' : 'Not recorded by this run: no generation rows yet.'); return; }
  // The newest generations' top genomes, each a link to its genome screen (best.g<n> answers for a generation's top whether or not the run's files tie it to a member id).
  const recent = rows.map((r, i) => ({ gen: typeof r.gen === 'number' ? r.gen : i, body: at(r, 'top.body'), score: at(r, 'top.score') })).slice(-3).reverse();
  const best = recent.map(x => `<a class="glink" href="genome.html?run=${encodeURIComponent(d.id)}&id=best.g${x.gen}" title="open the top genome of generation ${x.gen}">gen ${x.gen}</a> · body ${x.body ?? '—'}${typeof x.score === 'number' ? ' · ' + fmt(x.score, 4) : ''}`).join('<br>');
  b.innerHTML = `<svg class="chart" id="c-lead"></svg><div class="bestrows"><span class="kicker">top genome, newest first</span><br>${best}</div>`;
  const svg = $('c-lead'), R = rem(), w = svg.clientWidth, h = svg.clientHeight;
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  const L = 4.6 * R, rh = (h - 1.4 * R) / uniq.length, cw = (w - L) / Math.max(rows.length, 1), bh = Math.max(Math.min(rh * .34, 1.2 * R), 3);
  const last = bodies[bodies.length - 1];
  // A label needs a line's height of its own: where the rows are closer than that, every k-th is named (the lines are all drawn), and the body that leads now always is.
  const every = Math.max(1, Math.ceil(1.15 * R / Math.max(rh, 1)));
  const lastRow = uniq.indexOf(last);
  uniq.forEach((body, r) => {
    const y = r * rh + rh / 2;
    if (r % every === 0 || r === lastRow) {
      // the leader's own label wins a place when a neighbouring one would sit on it
      if (r === lastRow || Math.abs(r - lastRow) * rh >= 1.15 * R || lastRow < 0)
        el(svg, 'text', { x: 0, y: y + .3 * R, style: body === last ? 'fill:var(--ink-hi)' : '' }, 'body ' + body);
    }
    el(svg, 'line', { x1: L, x2: w, y1: y, y2: y, style: 'stroke:var(--track)' });
  });
  bodies.forEach((body, g) => {
    if (typeof body !== 'number') return;
    const r = uniq.indexOf(body);
    el(svg, 'rect', { x: L + g * cw, y: r * rh + rh / 2 - bh / 2, width: Math.max(cw - .6, 1), height: bh,
      style: g === bodies.length - 1 ? 'fill:var(--run);filter:var(--glow)' : 'fill:var(--run);opacity:.3' });
  });
  const gens = rows.map((r, i) => (typeof r.gen === 'number' ? r.gen : i));
  // the generations named under the chart: first, middle, last, each only once and only where the one before it has left room (a short run has fewer than three)
  const drawn = [];
  const firstG = 0, lastG = rows.length - 1, midG = Math.floor((rows.length - 1) / 2);
  [[firstG, 'start'], [lastG, 'end'], [midG, 'middle']].filter(([g], k, a) => a.findIndex(x => x[0] === g) === k).forEach(([g, anchor]) => {
    const text = 'gen ' + gens[g], x = L + g * cw, wd = text.length * .66 * R;
    const from = anchor === 'start' ? x : anchor === 'end' ? x - wd : x - wd / 2;
    if (drawn.some(([a, b]) => from < b + .6 * R && from + wd > a - .6 * R)) return;
    drawn.push([from, from + wd]);
    el(svg, 'text', { x, y: h - .2 * R, 'text-anchor': anchor }, text);
  });
}

function drawBrains(d) {
  const b = $('b-brains');
  const rows = d.generations || [];
  const bk = leaves(rows, 'brain'), mk = leaves(rows, 'made');
  const x = d.crossovers;
  if (!rows.length || (!bk.length && !mk.length && !x)) { b.innerHTML = noData(rows.length ? 'The rows record no brain or breeding fields.' : 'Not recorded by this run: no generation rows yet.'); return; }
  const gens = rows.map((r, i) => (typeof r.gen === 'number' ? r.gen : i));
  // a field and its Max become one chart with two lines
  const charts = [];
  for (const k of bk) {
    if (k.endsWith('Max') && bk.includes(k.slice(0, -3))) continue;
    charts.push({ title: k.replace(/^brain\./, ''), lines: [k, ...(bk.includes(k + 'Max') ? [k + 'Max'] : [])] });
  }
  let h = bk.length ? `<div class="mult">${charts.map((c, i) => {
    const last = [...series(rows, c.lines[0])].reverse().find(v => v != null), lm = c.lines[1] ? [...series(rows, c.lines[1])].reverse().find(v => v != null) : null;
    return `<div><span class="lab">${esc(c.title)}${c.lines[1] ? ' · max' : ''}<b>${fmt(last, 3)}${lm != null ? ' · ' + fmt(lm, 3) : ''}</b></span><svg class="chart" data-c="${i}"></svg></div>`;
  }).join('')}</div>` : '';
  const lastRow = rows[rows.length - 1];
  const fields = [];
  for (const k of mk) { const v = at(lastRow, k); if (typeof v === 'number') fields.push([k.replace(/^made\./, ''), fmt(v, 3)]); }
  if (x) {
    fields.push(['crossovers read', fmt(x.rows, 6)], ['transferred', fmt(x.transferred, 6)], ['base better', fmt(x.baseBetter, 6)], ['refused', fmt(x.refused, 6)], ['mutated', fmt(x.mutated, 6)]);
    if (x.meanDistance != null) fields.push(['mean distance, all', fmt(x.meanDistance, 3)]);
    for (const [k, v] of Object.entries(x.ends || {})) fields.push(['ends ' + k, fmt(v, 6)]);
  }
  h += `<div class="kicker brk">breeding · last generation, and the whole file</div><div class="fields">${fields.map(([k, v]) => `<div class="field"><span class="field__label">${esc(k)}</span><span class="field__value">${v}</span></div>`).join('')}</div>`;
  b.innerHTML = h;
  b.querySelectorAll('svg[data-c]').forEach(svg => small(svg, charts[+svg.dataset.c].lines.map(k => series(rows, k)), { xvals: gens }));
}

const TAB_TITLE = { log: 'Log', files: 'Files', activity: 'Activity', diff: 'Recipe differs' };

/** A recipe value as one short line: a string as it is, a number as it is, a list or an object by its size (the whole is in the title). */
function showValue(v) {
  if (v === undefined) return '—';
  if (v === null) return 'null';
  if (typeof v === 'string') return v.length > 90 ? v.slice(0, 89) + '…' : v;
  if (Array.isArray(v)) return `${v.length} item${v.length === 1 ? '' : 's'}`;
  if (typeof v === 'object') return `{ ${Object.keys(v).length} field${Object.keys(v).length === 1 ? '' : 's'} }`;
  return String(v);
}

/**
 * How telling a recipe field is as the headline of what differs: a reward's class or parameter (0), then the exam's controls (1), the search's settings (2), the world's
 * overrides (3), the bodies (4), then everything else (5). The run's wording, `description` and `name`, tells nothing about what was changed and is last (9): it is never the headline.
 */
const WORDING = /^(description|name)$/;
const DIFF_RANKS = [[/^reward(\.|$)/, 0], [/^exam\.controls(\.|$)/, 1], [/^search(\.|$)/, 2], [/^world(\.|$)/, 3], [/^bodies(\.|$)/, 4]];
function diffRank(path) {
  if (WORDING.test(path)) return 9;
  for (const [re, r] of DIFF_RANKS) if (re.test(path)) return r;
  return 5;
}
/** The differences, most telling first; fields of one rank keep the order the diff gave them. */
const rankedDiffs = list => list.map((x, i) => [x, i]).sort((a, b) => diffRank(a[0].path) - diffRank(b[0].path) || a[1] - b[1]).map(([x]) => x);

/** The recipe differences between this run and its control, as recipe.diff answered. */
function diffHtml(d) {
  if (!ctl) return noData('Pick a control (the picker in the band) to see what its recipe differs by.');
  if (ctl.error) return noData('The control cannot be read: ' + ctl.error);
  const df = ctl.diff;
  if (!df) return noData('Reading the two recipes…');
  if (df.missing) return `<p class="note"><span class="plate plate--alarm">NOT FOUND</span> ${esc(df.missing)}</p>`;
  if (!df.ok) return `<p class="note"><span class="plate plate--alarm">REFUSED</span> ${esc(df.error || 'recipe.diff refused')}</p>`;
  const recorded = (x, rec) => (rec && x.hash && rec.toLowerCase() !== x.hash.toLowerCase() ? ` <span class="badge" title="the run recorded recipe hash ${esc(rec)}, but the file ${esc(x.path)} hashes to ${esc(x.hash)} now: the recipe was edited after the run">file changed since</span>` : '');
  const recA = (d.entry && d.entry.recipeHash) || d.recipeHash, recB = (ctl.data.entry && ctl.data.entry.recipeHash) || ctl.data.recipeHash;
  const head = `<p class="note"><em style="font-style:normal;color:var(--run)">this run</em> ${esc(df.a.name)} <code>${esc((df.a.hash || '').slice(0, 8))}</code>${recorded(df.a, recA)}<br>` +
    `<b style="font-weight:400;color:var(--ctl)">control</b> ${esc(df.b.name)} <code>${esc((df.b.hash || '').slice(0, 8))}</code>${recorded(df.b, recB)}<br>` +
    `${df.identical ? 'The two recipes resolve to the same settings.' : `${df.differs.length} field${df.differs.length === 1 ? '' : 's'} differ, each as the control's value → this run's. Both are resolved with their bases first.`}</p>`;
  const side = x => (x.side === 'a' ? ' (only in this run)' : x.side === 'b' ? ' (only in the control)' : '');
  const list = rankedDiffs(df.differs).map(x => `<div class="dline" title="${esc(JSON.stringify({ control: x.b, thisRun: x.a }))}"><span class="k">${esc(x.path)}</span>` +
    `<span class="v"><b>${esc(x.side === 'a' ? 'not there' : showValue(x.b))}</b> → <em>${esc(x.side === 'b' ? 'not there' : showValue(x.a))}</em>${esc(side(x))}</span></div>`).join('');
  return head + `<div class="scrolly">${list}</div>`;
}

function drawLog(d) {
  if (d.exists === false) return;
  const b = $('b-log');
  $('log-kicker').textContent = TAB_TITLE[logTab] || 'Log';
  $('tab-diff').hidden = !controlId;
  if (!controlId && logTab === 'diff') { logTab = 'log'; document.querySelectorAll('.ptabs span').forEach(x => x.classList.toggle('on', x.dataset.tab === 'log')); $('log-kicker').textContent = 'Log'; }
  if (logTab === 'diff') { b.innerHTML = diffHtml(d); return; }
  if (logTab === 'activity') {
    if (!act) { b.innerHTML = noData('Reading the activity log…'); return; }
    if (act.error) { b.innerHTML = noData('The activity log could not be read: ' + act.error); return; }
    b.innerHTML = act.entries.length
      ? `<p class="note" style="margin:0 0 .3rem">${act.shown} operation${act.shown === 1 ? '' : 's'} of the room touched this run, newest first (launch, stop, rerun, episode steps), of ${act.total} in the log.</p><div class="scrolly">${act.entries.map(actRow).join('')}</div>`
      : noData(`No operation of the room has touched this run (${act.total} in the log in all): its launch, stops, reruns and episode steps would be listed here, with who asked and why.`);
    return;
  }
  if (logTab === 'files') {
    const f = d.files || [];
    b.innerHTML = f.length ? `<table class="t"><tr><th class="l">file</th><th>rows</th><th>bytes</th><th>written</th></tr>${f.map(x =>
      `<tr><td class="l sans">${esc(x.name)}${x.bad ? ` <span class="badge">${x.bad} unreadable</span>` : ''}${x.problem ? ` <span class="badge">${esc(x.problem)}</span>` : ''}</td><td>${x.rows ?? ''}</td><td>${fmt(x.bytes, 8)}</td><td>${clock(x.modifiedUtc)}</td></tr>`).join('')}</table>` : noData('The folder holds none of the files the room reads.');
    return;
  }
  const lg = d.log;
  if (!lg || !lg.lines.length) { b.innerHTML = noData('Not recorded by this run: no nursery.log.'); return; }
  b.innerHTML = `<pre class="log">${esc(lg.lines.join('\n'))}</pre>`;
  const pre = b.querySelector('pre');
  pre.scrollTop = pre.scrollHeight;
}

// ---- the control: a second run laid over this one

function syncUrl() {
  const q = new URLSearchParams(location.search);
  if (!q.has('id') && runId) q.set('id', runId);
  if (controlId) q.set('vs', controlId); else q.delete('vs');
  if (pins == null) q.delete('pin'); else q.set('pin', pins.join(','));
  history.replaceState(null, '', location.pathname + '?' + q.toString());
}

/** The control picker: every other run whose folder is here, and the band's summary of what its recipe differs by. */
function fillControlPicker(d) {
  const s = $('control');
  const others = registryItems.filter(i => i.exists && i.id !== d.id && i.folderName !== d.id);
  const known = others.some(i => i.id === controlId || i.folderName === controlId);
  s.innerHTML = '<option value="">no control</option>' + others.map(i => `<option value="${esc(i.id)}">${esc((i.number != null ? '#' + i.number + ' ' : '') + i.name)}</option>`).join('') +
    (controlId && !known ? `<option value="${esc(controlId)}">${esc(controlId)} (not found)</option>` : '');
  const hit = others.find(i => i.id === controlId || i.folderName === controlId);
  s.value = controlId ? (hit ? hit.id : controlId) : '';
  $('ctl-swatch').hidden = !controlId;
}

function drawControlBox(d) {
  fillControlPicker(d);
  const pd = $('pick-diff');
  pd.hidden = !controlId;
  $('tab-diff').hidden = !controlId;
  if (!controlId) return;
  const df = ctl && ctl.diff;
  let kicker, text, dashed = false;
  if (!ctl) { kicker = 'differs from control'; text = 'reading…'; }
  else if (ctl.error) { kicker = 'control not read'; text = ctl.error; dashed = true; }
  else if (!df) { kicker = 'differs from control'; text = 'reading…'; }
  else if (df.missing) { kicker = 'recipe differs'; text = df.short; dashed = true; }
  else if (!df.ok) { kicker = 'recipe differs'; text = 'refused: ' + (df.error || ''); dashed = true; }
  else if (df.identical) { kicker = 'differs from control · 0'; text = 'no recipe field differs'; }
  else {
    // The headline is the most telling field (see diffRank); where only the run's wording differs, it says so and names no field's value.
    const ranked = rankedDiffs(df.differs), x = ranked[0];
    kicker = `differs from control · ${df.differs.length}`;
    text = diffRank(x.path) === 9 ? `only the wording: ${ranked.map(y => y.path).join(', ')}`
      : `${x.path} ${x.side === 'a' ? '' : showValue(x.b) + ' → '}${x.side === 'b' ? 'not there' : showValue(x.a)}`;
    dashed = false;
  }
  pd.innerHTML = `<span class="kicker">${esc(kicker)}</span><span class="selbox${dashed ? ' dashed' : ''}" style="border-style:dashed"><span class="t">${esc(text)}</span></span>`;
  // The box cuts a long headline with an ellipsis (1366 wide), so its title holds the whole headline, then the words about opening the list.
  pd.title = df && df.missing ? df.missing : ctl && ctl.error ? ctl.error : `${kicker}: ${text}. What this run's recipe differs by from its control's: opens the list`;
}

/** Reads the control: its run, its exam beside this run's, and the two recipes' difference. A control that is not there is said so, and nothing is drawn of it. */
async function loadControl(d) {
  if (!controlId) { ctl = null; return; }
  const c = { id: controlId, data: null, cmp: null, diff: null, error: null };
  try { c.data = await getResource('nursery://run/' + encodeURIComponent(controlId)); }
  catch (e) { if (!e.refused) throw e; c.error = e.message; ctl = c; return; }
  if (c.data.id === d.id) { c.error = 'a run cannot be its own control'; ctl = c; return; }
  if (c.data.exists === false) c.error = c.data.problem || 'the control run\'s folder is not on this machine';
  if (!c.error && d.exam && c.data.exam) {
    try { c.cmp = await getResource(`nursery://exam-compare/${encodeURIComponent(d.id)}/vs/${encodeURIComponent(c.data.id)}`); }
    catch (e) { if (!e.refused) throw e; c.cmp = { available: false, reason: e.message }; }
  } else if (!c.error) c.cmp = { available: false, reason: !d.exam ? 'this run has no exam rows yet' : 'the control has no exam rows' };
  // the recipes: each run's own recipe file, as the room records it; a run whose recipe is not here is said so, with which
  const pa = d.recipePath && d.recipeExists ? d.recipePath : null, pb = c.data.recipePath && c.data.recipeExists ? c.data.recipePath : null;
  if (!pa || !pb) {
    const why = (who, x) => (!x.recipePath ? `${who} has no recipe on record (it is not a registry entry, and this room did not launch it)` : `${who}'s recipe ${x.recipePath} is not in this tree`);
    c.diff = { missing: !pa && !pb ? `neither run's recipe can be found: ${why('this run', d)}; ${why('the control', c.data)}.` : !pb ? `The control's recipe cannot be found: ${why('the control', c.data)}.` : `This run's recipe cannot be found: ${why('this run', d)}.`,
      short: !pb ? "the control's recipe was not found" : "this run's recipe was not found" };
    if (!pa && !pb) c.diff.short = "neither run's recipe was found";
  } else {
    const r = await callOp('recipe.diff', { path: pa, other: pb });
    c.diff = r.ok ? r.result : { ok: false, error: r.error };
  }
  ctl = c;
}

async function loadActivity(d) {
  try { act = await getResource('nursery://activity/' + encodeURIComponent(d.id)); }
  catch (e) { if (!e.refused) throw e; act = { error: e.message }; }
}

function draw(d) {
  $('crumb').innerHTML = `<b title="${esc(d.folder || '')}">${esc(d.folderShort || d.folder || '')}</b>`;
  document.title = `Nursery room · run ${d.name || d.id}`;
  drawBand(d);
  if (d.exists === false) {
    document.querySelector('main.page').classList.add('noexam');
    for (const id of ['b-exam', 'b-energy', 'b-parts', 'b-leader', 'b-brains', 'b-log']) $(id).innerHTML = noData(d.problem || 'The run folder is missing.');
    $('hero-body').innerHTML = noData(d.problem || 'The run folder is missing.');
    return;
  }
  // The exam goes first: it decides whether the page gives the search the full width (see main.noexam), and a chart is drawn to the box it finds. Whatever box moves later is
  // heard by the observers below, which draw the charts again.
  drawExam(d); drawHero(d); drawEnergy(d); drawParts(d); drawLeader(d); drawBrains(d); drawLog(d);
}

/** Every chart again, to the boxes they are in now. */
function redrawCharts() {
  if (!data || data.exists === false || document.querySelector('main.page--refused')) return;
  drawHero(data); drawEnergy(data); drawParts(data); drawLeader(data); drawBrains(data); drawBodyNet();
}

/** The newest run whose folder exists, by the registry's own order (newest first), or null. */
async function newestRun() {
  const reg = await getResource('nursery://registry');
  const first = reg.items.find(i => i.exists);
  return first ? first.id : null;
}

/** A run the room does not know, said in the main area: the crumb, the message and the way back. The page has nothing more to read, so its polling ends. */
function refuseRun(message) {
  refuse({
    crumb: 'run ' + (runId || ''), kicker: 'Run', title: 'No such run', message,
    links: [['registry.html', 'BACK TO THE REGISTRY']],
  });
  document.title = 'Nursery room · run ' + (runId || '') + ' not found';
  return 'stop';
}

async function tick() {
  if (!runId) {
    // No run in the address: the one last looked at (kept in this browser), and only when there is none, the newest, which the page says.
    const kept = getSel().run;
    if (kept != null) { runId = kept; runFrom = 'kept'; }
    else {
      runId = await newestRun();
      runFrom = 'newest';
      if (runId == null) {
        refuse({ crumb: 'no run', kicker: 'Run', title: 'No run to show', message: 'The registry is empty and room.json names no run folder that exists.', links: [['registry.html', 'BACK TO THE REGISTRY']] });
        return 'stop';
      }
    }
  }
  let d;
  try { d = await getResource('nursery://run/' + encodeURIComponent(runId)); }
  catch (e) {
    if (e.status !== 404 && e.status !== 400) throw e;
    // A run kept from an earlier visit may be gone: fall back to the newest and say so. A run named in the address that is not there is a refusal.
    if (runFrom === 'kept') {
      setSel({ run: null });
      runId = await newestRun(); runFrom = 'newest';
      if (runId != null) return tick();
    }
    return refuseRun(e.message);
  }
  setSel({ run: d.id });
  data = d;
  // The registry's list is what the control picker offers; it is read when the page opens and now and then after.
  if (!registryItems.length || Date.now() - registryReadAt > 120000) {
    try { registryItems = (await getResource('nursery://registry')).items; registryReadAt = Date.now(); } catch (e) { if (!e.refused) throw e; }
  }
  await loadControl(d);
  if (logTab === 'activity') await loadActivity(d);
  $('foot').textContent = `polls every ${pollSeconds} s, new bytes only · read ${new Date().toLocaleTimeString()}`;
  const key = [d.version, controlId, ctl && ctl.error, ctl && ctl.data && ctl.data.version, ctl && JSON.stringify(ctl.cmp), ctl && JSON.stringify(ctl.diff), act && act.shown, act && act.total].join('~');
  if (drawn === key) {
    // nothing new in the files: the age, the stall alarm and the exam's end move with the clock
    drawBand(d);
    return;
  }
  drawn = key;
  draw(d);
}

let pollSeconds = 30;
function openTab(name) {
  logTab = name;
  document.querySelectorAll('.ptabs span').forEach(x => x.classList.toggle('on', x.dataset.tab === name));
  document.querySelectorAll('.ptabs span').forEach(x => x.setAttribute('aria-selected', x.dataset.tab === name ? 'true' : 'false'));
  if (!data) return;
  drawLog(data);
  if (name === 'activity') loadActivity(data).then(() => { if (logTab === 'activity') drawLog(data); }).catch(e => { console.error(e); showPollError(e); });
}
document.querySelectorAll('.ptabs span').forEach(s => {
  s.addEventListener('click', () => openTab(s.dataset.tab));
  s.addEventListener('keydown', ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); openTab(s.dataset.tab); } });
});

// The control: picking one (or none) is kept in the address, and the page is read again with it.
const retick = () => { drawn = null; ctl = null; tick().catch(e => { console.error(e); showPollError(e); }); };
$('control').addEventListener('change', () => { controlId = $('control').value || null; syncUrl(); retick(); });
$('pick-diff').addEventListener('click', () => openTab('diff'));
$('pin-btn').addEventListener('click', () => setPinMenu($('pinmenu').hidden));
// The path is read from the event, not from its target: a click on DEFAULTS or NONE redraws the menu, and the button is gone by the time this runs.
document.addEventListener('click', ev => { if (!$('pinmenu').hidden && !ev.composedPath().some(n => n === $('pinmenu') || n === $('pin-btn'))) setPinMenu(false); });
document.addEventListener('keydown', ev => {
  if (ev.key === 'Escape' && !$('pinmenu').hidden) { setPinMenu(false); $('pin-btn').focus(); return; }
  if (ev.ctrlKey || ev.metaKey || ev.altKey) return;
  const t = ev.target;
  if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || (t.closest && t.closest('form.ask')))) return;
  if (ev.key === 'c' || ev.key === 'C') { ev.preventDefault(); $('control').focus(); }
  else if (ev.key === 'l' || ev.key === 'L') {
    const names = ['log', 'files', 'activity', ...(controlId ? ['diff'] : [])];
    ev.preventDefault(); openTab(names[(names.indexOf(logTab) + 1) % names.length]);
  }
});
$('measure').addEventListener('change', () => { if (data) drawHero(data); });
onResize(() => { if (data && data.exists !== false) draw(data); });
// A chart's box moves when a neighbouring panel takes a row (the exam's, for a run with a compact exam or none) as well as when the window does, and the window's resize
// does not say so: each panel that holds a chart is watched, and the charts are drawn again to the new box.
watchBoxes(document.querySelectorAll('main.page > section'), redrawCharts);

const slug = s => (String(s || 'run').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'run').slice(0, 50);

// The band's two controls: each asks why in a small form, then calls its operation.
bindOps({
  'run.stop': btn => {
    if (!data) return;
    if (!['search', 'exam', 'running'].includes((data.state || {}).phase)) { ask({ key: 'stop-none', info: true, title: 'Stop', note: 'This run is not running, or has no record, so there is nothing for a STOP file to stop.' }); return; }
    ask({
      key: 'stop', title: 'Stop ' + (data.name || data.id), send: 'WRITE STOP FILE',
      note: "Writes STOP into the run's folder. The run ends between generations (or between the exam's brains) with status stopped, and is not entered in the registry unless it is already swimming its last exam brain. Nothing is killed.",
      run: reason => callOp('run.stop', { id: data.id, reason }),
      done: r => esc(r.note),
      after: () => { drawn = null; tick(); },
    });
  },
  'run.rerun': btn => {
    // The button is disabled where the registry says the entry cannot be rerun (see drawBand), so a click here is one the room would take.
    if (!data || btn.disabled) return;
    ask({
      key: 'rerun', title: 'Rerun entry #' + data.number, send: 'RERUN',
      note: "Runs the entry's recipe again on this build into a new folder, and compares the two.",
      machine: r => machineText(r, true),   // the form opens at once; the room's slot and the heat are filled in when the room answers, with the threads that fit as the launch dialog says them
      fields: [{ name: 'name', label: 'new folder name (a-z, 0-9, -)', value: slug(data.name) + '-rerun' }, { name: 'threads', label: 'threads', type: 'number', value: 4, min: 1 }],
      run: (reason, v) => callOp('run.rerun', { entry: String(data.number), name: v.name.trim(), threads: Number(v.threads), reason }),
      done: x => `started <b>${esc(x.id)}</b> (pid ${esc(x.pid)}). <a href="run.html?id=${encodeURIComponent(x.id)}">open the run</a>`,
    });
  },
});

// The room's reading of the machine takes seconds (the heat check runs a script): the page's own reading does not wait for it.
mountMachine().then(room => { if (room) pollSeconds = room.pollSeconds; });
poll(tick, pollSeconds);
