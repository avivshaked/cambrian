// The tank screen, first cut: one exam episode of a run, re-stepped by the nursery and played back from above.
// It reads nursery://exam-plan/{run} (the pickers), nursery://episode/{run}/{b}/{d}/{t} (the state, the verdict and the provenance) and, for a
// finished episode, the three files the nursery wrote (/api/episode-file: the header, the events, the frames). The only operation is the STEP button.
// Nothing about any experiment is named here: the channels, their units and the number of members are read from the episode's own header, and a
// channel is found by the suffix the nursery's frame recorder gives it. Later steps of the build (the 3D follow view, the live brain, side by side,
// pictures, video, the Blender export) are drawn disabled in the page, each with the step that builds it.

import { esc, getJson, getResource, callOp, bindOps, ask, poll, onResize, fmt, clock, mountMachine, getSel, setSel, refuse } from './common.js';
import { el, frame, rem, extent } from './chart.js';

const $ = id => document.getElementById(id);
const params = new URLSearchParams(location.search);
const whole = k => { const v = params.get(k); return v != null && /^\d+$/.test(v) ? +v : null; };
const sel = { run: params.get('run'), brain: whole('brain'), draw: whole('draw'), tank: whole('tank') };
// What the address asked for, kept as it was written: a brain, a draw or a tank the run does not have is said on the page, and never clamped silently.
const asked = { brain: params.get('brain'), draw: params.get('draw'), tank: params.get('tank') };
let planLoaded = false;          // the run's exam plan was read: only then is the address a selection worth keeping for the other tabs
const wantGenome = whole('genome');                       // an exam genome's number: the tank of the draw that holds it is picked
const startAt = params.has('t') && Number.isFinite(+params.get('t')) ? +params.get('t') : null;   // ?t=15 opens the episode paused at 15 s

// One colour a member, with a dash and a number besides, so that colour is never the only carrier.
const PALETTE = ['#5ef0ff', '#ffb86b', '#a08cff', '#e6cf86', '#8fe388', '#ff7eb6', '#9fb4ff', '#f0a050'];
const DASH = ['', '7 3', '2 3', '8 3 2 3', '', '7 3', '2 3', '8 3 2 3'];
const colour = slot => PALETTE[slot % PALETTE.length];

// The events the nursery's recorder writes: a glyph and a few words each. An event it does not know is shown by its own type.
const EVENT = {
  carrionEaten: { glyph: '+', words: 'carrion eaten' },
  death: { glyph: '×', words: 'death' },
  islandEntered: { glyph: '▲', words: 'entered an island' },
  birthHeld: { glyph: '◆', words: 'birth held' },
};
const eventInfo = type => EVENT[type] || { glyph: '•', words: String(type) };

let plan = null, planError = null, planStatus = null, runs = [], lastRoom = null, pollSeconds = 30;
let gone = false;                // the screen said that it cannot show what was asked for (refuse): its pickers and its map are no longer in the page
let view = null;                 // the episode resource as last read
let model = null, modelKey = null, loadingKey = null;
let t = 0, playing = false, speed = 1, lastNow = 0, dirty = true, opened = false;
let islandLayer = null, islandKey = '';

const runUri = run => encodeURIComponent(run).replace(/%2F/gi, '/');
const episodeUri = () => `nursery://episode/${runUri(sel.run)}/${sel.brain}/${sel.draw}/${sel.tank}`;
const filesQuery = file => `run=${encodeURIComponent(sel.run)}&b=${sel.brain}&d=${sel.draw}&t=${sel.tank}&file=${file}`;
const selKey = () => [sel.run, sel.brain, sel.draw, sel.tank].join('|');
const secs = v => (Number.isFinite(v) ? (+v.toFixed(1)) + ' s' : '—');

// ---------------------------------------------------------------- the pickers

function syncUrl() {
  const q = new URLSearchParams();
  if (sel.run != null) q.set('run', sel.run);
  if (sel.brain != null) q.set('brain', sel.brain);
  if (sel.draw != null) q.set('draw', sel.draw);
  if (sel.tank != null) q.set('tank', sel.tank);
  history.replaceState(null, '', 'tank.html?' + q.toString());
  // The tabs carry the run, brain, draw and tank looked at, once the run's plan has been read: a run that is not there is not kept as the last selection.
  if (planLoaded) setSel({ run: sel.run, brain: sel.brain, draw: sel.draw, tank: sel.tank });
  $('crumb').innerHTML = `<b>${esc(sel.run)}</b> · brain <b>${sel.brain ?? '—'}</b> · draw <b>${sel.draw ?? '—'}</b> · tank <b>${sel.tank ?? '—'}</b>`;
  document.title = `Nursery room · tank · run ${sel.run}`;
}

function fillRuns() {
  const s = $('s-run');
  const known = runs.map(r => r.id);
  const list = sel.run != null && !known.includes(sel.run) ? [{ id: sel.run, name: sel.run }, ...runs] : runs;
  s.innerHTML = list.map(r => `<option value="${esc(r.id)}">${esc((r.number != null ? '#' + r.number + ' ' : '') + (r.name || r.id))}</option>`).join('') || '<option value="">no run</option>';
  if (sel.run != null) s.value = sel.run;
}

function fillBrains() {
  const s = $('s-brain');
  if (!plan) { s.innerHTML = '<option value="">—</option>'; return; }
  s.innerHTML = plan.brains.map(b => `<option value="${b.index}">${b.index} · ${esc(b.label)}</option>`).join('');
  if (sel.brain == null || sel.brain >= plan.brains.length) sel.brain = 0;
  s.value = String(sel.brain);
}

function fillDraws() {
  const s = $('s-draw');
  if (!plan) { s.innerHTML = '<option value="">—</option>'; return; }
  s.innerHTML = plan.draws.map(d => `<option value="${d.draw}">draw ${d.draw} of ${plan.episodes}</option>`).join('');
  if (sel.draw == null || sel.draw >= plan.draws.length) sel.draw = 0;
  s.value = String(sel.draw);
}

function fillTanks() {
  const s = $('s-tank');
  const d = plan && plan.draws[sel.draw];
  if (!d || !d.tanks) { s.innerHTML = '<option value="">no tank dealt</option>'; sel.tank = null; return; }
  s.innerHTML = d.tanks.map((m, k) => `<option value="${k}">tank ${k} · genomes ${m.join(', ')}</option>`).join('');
  if (wantGenome != null && !opened) {
    const k = d.tanks.findIndex(m => m.includes(wantGenome));
    if (k >= 0) sel.tank = k;
  }
  if (sel.tank == null || sel.tank >= d.tanks.length) sel.tank = 0;
  s.value = String(sel.tank);
}

async function loadPlan() {
  plan = null; planError = null; planStatus = null;
  if (sel.run == null) { planError = 'no run is picked'; return; }
  try { plan = await getResource('nursery://exam-plan/' + runUri(sel.run)); }
  catch (e) { planError = e.message; planStatus = e.refused ? e.status : null; }
  fillBrains(); fillDraws(); fillTanks();
  if (plan) { planLoaded = true; checkAddress(); }
}

/** The notice about the address (a brain, draw or tank it named that the run does not have), or null to clear it. */
function addressNote(text) {
  const n = $('addr-note');
  if (!n) return;
  if (!text) { n.hidden = true; n.innerHTML = ''; return; }
  n.hidden = false;
  n.innerHTML = `<span class="plate plate--alarm">ADDRESS</span><span>${esc(text)}</span>`;
}

/** After the plan is read: says what the address asked for that the run does not have, and what is shown in its place. */
function checkAddress() {
  if (!plan) return;
  const notes = [];
  const check = (what, raw, count, shown) => {
    if (raw == null) return;
    if (!/^\d+$/.test(raw)) { notes.push(`${what}=${raw} in the address is not a whole number, so ${what} ${shown} is shown`); return; }
    if (+raw >= count) notes.push(`the address asked for ${what} ${raw}, but this run has ${count} ${what}${count === 1 ? '' : 's'} (0 to ${count - 1}), so ${what} ${shown} is shown`);
  };
  check('brain', asked.brain, plan.brains.length, sel.brain);
  check('draw', asked.draw, plan.draws.length, sel.draw);
  const d = plan.draws[sel.draw];
  check('tank', asked.tank, d && d.tanks ? d.tanks.length : 0, sel.tank);
  addressNote(notes.length ? notes.join('; ') + '.' : null);
}

function changed(reset) {
  addressNote(null);
  view = null; model = null; modelKey = null; loadingKey = null; playing = false;
  syncUrl();
  renderAll();
  if (reset) reset();
  tick();
}

// Another run is another page load: a run that has no exam plan, or that is not there, is then the refusal the address would give, and not a half-drawn screen.
$('s-run').addEventListener('change', e => { location.href = 'tank.html?run=' + encodeURIComponent(e.target.value); });
$('s-brain').addEventListener('change', e => { sel.brain = +e.target.value; changed(); });
$('s-draw').addEventListener('change', e => { sel.draw = +e.target.value; fillTanks(); changed(); });
$('s-tank').addEventListener('change', e => { sel.tank = +e.target.value; changed(); });

// ---------------------------------------------------------------- the step

function stepRefusal() {
  if (!plan) return planError ? 'the exam plan cannot be read, so there is nothing to step' : 'the exam plan is being read';
  const d = plan.draws[sel.draw];
  if (!d || !d.tanks || sel.tank == null) return "the plan does not know this draw's tanks, so there is no tank to step";
  if (view && view.state === 'running') return 'this episode is being stepped now';
  if (!lastRoom) return 'the room has not answered yet, so the cap cannot be checked';
  if (lastRoom.stepRefusal) return lastRoom.stepRefusal.reason;
  return null;
}

function updateStep() {
  if (gone) return;
  const b = $('step'), why = $('stepwhy');
  const off = stepRefusal();
  const done = !!(view && view.state === 'done');
  b.textContent = done ? 'STEP AGAIN' : 'STEP';
  b.disabled = !!off;
  b.title = off || (done ? 'step this episode again; the finished files are kept beside the new ones' : "re-step this brain, draw and tank with the nursery's own code");
  b.setAttribute('aria-disabled', off ? 'true' : 'false');
  why.textContent = off ? 'not now: ' + off : '';
  why.dataset.kind = off && lastRoom && lastRoom.stepRefusal ? lastRoom.stepRefusal.kind : '';
}

bindOps({
  'episode.step': () => {
    if ($('step').disabled) return;
    const done = !!(view && view.state === 'done');
    ask({
      key: 'step', title: `${done ? 'Step again' : 'Step'}: run ${sel.run}, brain ${sel.brain}, draw ${sel.draw}, tank ${sel.tank}`, send: done ? 'STEP AGAIN' : 'STEP',
      note: "Re-steps this exam episode with the nursery's own code, as a process of its own on one thread, pinned to the cores room.json names. It counts 1 thread against the cap while it lives, and its frames and result are written into the episode's cache folder. " +
        (done ? 'A finished episode of this request is there already: the new one is made beside it, and the old files are kept.' : 'The verdict compares each net with the run\'s own number for the same brain and draw, bit for bit.'),
      run: reason => callOp('episode.step', { run: sel.run, brain: sel.brain, draw: sel.draw, tank: sel.tank, fresh: done, reason }),
      done: r => (r.cached ? 'served from the cache: a finished episode of this request is there already.' : `started (pid ${esc(r.pid)}). This page asks again every ${pollSeconds} s.`),
      after: () => { view = null; tick(); },
    });
  },
});

// ---------------------------------------------------------------- reading the episode

async function getText(url) {
  const r = await fetch(url, { cache: 'no-store' });
  if (!r.ok) {
    let m = r.status + ' ' + r.statusText;
    try { m = (await r.json()).error || m; } catch (e) { /* not JSON */ }
    throw new Error(m);
  }
  return r;
}

async function tick() {
  if (sel.run == null || sel.brain == null || sel.draw == null || sel.tank == null) { renderAll(); return; }
  const key = selKey();
  let v;
  try { v = await getResource(episodeUri()); }
  catch (e) { v = { state: 'error', note: e.message }; }
  if (key !== selKey()) return;     // the pickers moved while the room answered
  view = v;
  if (v.state === 'done') await ensureModel(v);
  else { model = null; modelKey = null; }
  renderAll();
}

async function ensureModel(v) {
  const key = selKey() + '|' + (v.startedUtc || '');
  if (modelKey === key && model) return;
  if (loadingKey === key) return;
  loadingKey = key;
  try {
    const q = file => '/api/episode-file?' + filesQuery(file);
    const [epi, evText, buf] = await Promise.all([
      getJson(q('episode')),
      getText(q('events')).then(r => r.text()).catch(() => null),
      getText(q('frames')).then(r => r.arrayBuffer()),
    ]);
    if (loadingKey !== key) return;
    model = buildModel(epi, evText, buf);
  } catch (e) {
    if (loadingKey !== key) return;
    model = { problem: 'the finished episode could not be read: ' + e.message };
  }
  modelKey = key;
  loadingKey = null;
  islandKey = '';
  t = model.problem ? 0 : (startAt != null && !opened ? Math.min(Math.max(startAt, model.t0), model.t1) : model.t0);
  if (startAt == null && !opened && !model.problem) { /* a first look opens paused at the start */ }
  opened = true;
}

/** The finished episode as arrays: every channel found by name in the header's list, never by position. */
function buildModel(epi, eventsText, buf) {
  const F = epi.frameFloats, N = epi.frames;
  if (!(F > 0) || !(N > 0) || !Array.isArray(epi.channels)) return { problem: "the episode's header names no frames or no channels" };
  if (epi.channels.length !== F) return { problem: `the header lists ${epi.channels.length} channels and says a frame holds ${F} floats` };
  if (buf.byteLength !== N * F * 4) return { problem: `the stream holds ${buf.byteLength} bytes, and ${N} frames of ${F} floats of 4 bytes make ${N * F * 4}: it is damaged or it is not the stream of this header` };
  const data = new Float32Array(buf);
  const col = new Map(epi.channels.map((c, i) => [c.name, i]));
  const unitOf = name => (col.has(name) ? epi.channels[col.get(name)].unit : '');
  const time = new Float32Array(N);
  for (let k = 0; k < N; k++) time[k] = data[k * F];
  const m = { epi, data, F, N, col, time, t0: time[0], t1: time[N - 1], members: [], corpses: [], problem: null };
  m.dt = N > 1 ? (m.t1 - m.t0) / (N - 1) : 1;

  for (const h of epi.members || []) {
    const p = `m${h.slot}.`;
    const links = [];
    for (let j = 0; j < (h.links || 0); j++) links.push({ x: col.get(`${p}link${j}.x`), z: col.get(`${p}link${j}.z`) });
    const mem = {
      slot: h.slot, pool: h.pool, body: h.body, landed: !!h.landed, links,
      alive: col.get(p + 'alive'), reserve: col.get(p + 'reserve'), snow: col.get(p + 'snow'),
      px: null, pz: null, deathK: -1, everAlive: false,
    };
    if (links.length && links[0].x != null && links[0].z != null) {
      mem.px = new Float32Array(N); mem.pz = new Float32Array(N);
      for (let k = 0; k < N; k++) { mem.px[k] = data[k * F + links[0].x]; mem.pz[k] = data[k * F + links[0].z]; }
    }
    if (mem.alive != null) {
      for (let k = 0; k < N; k++) {
        const a = data[k * F + mem.alive];
        if (a > 0.5) mem.everAlive = true;
        else if (mem.everAlive && mem.deathK < 0) mem.deathK = k;
      }
    }
    m.members.push(mem);
  }
  m.energyUnit = (m.members.find(x => x.reserve != null) ? unitOf(`m${m.members.find(x => x.reserve != null).slot}.reserve`) : '') || 'J';
  m.snowUnit = (m.members.find(x => x.snow != null) ? unitOf(`m${m.members.find(x => x.snow != null).slot}.snow`) : '');

  for (let c = 0; col.has(`corpse${c}.x`); c++)
    m.corpses.push({ x: col.get(`corpse${c}.x`), z: col.get(`corpse${c}.z`), j: col.get(`corpse${c}.joules`) });
  m.joules0 = m.corpses.map(c => (c.j != null ? data[c.j] : NaN));

  // the extent of what moved, so that the map is scaled to the water that was used
  let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
  const grow = (x, z) => { if (Number.isFinite(x) && Number.isFinite(z)) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); z0 = Math.min(z0, z); z1 = Math.max(z1, z); } };
  for (const mem of m.members) if (mem.px) for (let k = 0; k < N; k++) grow(mem.px[k], mem.pz[k]);
  for (const c of m.corpses) grow(data[c.x], data[c.z]);
  if (!Number.isFinite(x0)) { x0 = 0; x1 = 10; z0 = 0; z1 = 10; }
  m.extent = { x0, x1, z0, z1 };

  // the snow the bodies met: every sample whose snow was at or above the header's island rule
  const isl = epi.islands;
  m.island = [];
  m.islandRule = isl && isl.read && Number.isFinite(isl.enterAt) ? isl : null;
  if (m.islandRule)
    for (const mem of m.members) {
      if (mem.snow == null || !mem.px) continue;
      for (let k = 0; k < N; k++) if (data[k * F + mem.snow] >= isl.enterAt) m.island.push(mem.px[k], mem.pz[k]);
    }
  if (m.island.length > 16000) { const keep = []; const stride = Math.ceil(m.island.length / 2 / 8000); for (let i = 0; i < m.island.length / 2; i += stride) keep.push(m.island[2 * i], m.island[2 * i + 1]); m.island = keep; }

  m.events = [];
  m.eventsNote = null;
  if (eventsText == null) m.eventsNote = 'the episode holds no events file';
  else
    for (const l of eventsText.split('\n')) {
      if (!l.trim()) continue;
      try { const e = JSON.parse(l); if (Number.isFinite(e.t)) m.events.push(e); } catch (err) { m.eventsNote = 'a line of the events file is not JSON and is left out'; }
    }
  m.events.sort((a, b) => a.t - b.t);
  return m;
}

/** The two frames around a moment and how far between them it lies. */
function at(tt) {
  if (!model || model.N < 2 || tt <= model.t0) return [0, Math.min(1, (model ? model.N : 1) - 1), 0];
  const f = (tt - model.t0) / model.dt;
  const k = Math.min(model.N - 2, Math.floor(f));
  return [k, k + 1, Math.min(1, f - k)];
}

const cell = (k, c) => model.data[k * model.F + c];
function lerpCol(c, k, k2, u) {
  if (c == null) return NaN;
  const a = cell(k, c), b = cell(k2, c);
  if (!Number.isFinite(a)) return NaN;
  return Number.isFinite(b) ? a + (b - a) * u : a;
}

// ---------------------------------------------------------------- the side panel: state, verdict, table, provenance

const MARK = { none: ['draft', 'NO EPISODE'], running: ['running', 'STEPPING'], stopped: ['killed', 'STOPPED'], failed: ['failed', 'FAILED'], done: ['done', 'DONE'], error: ['failed', 'NOT READ'] };

function mb(bytes) { return bytes == null ? '—' : bytes >= 1e6 ? fmt(bytes / 1e6, 3) + ' MB' : fmt(bytes / 1e3, 3) + ' kB'; }

function renderState() {
  const host = $('state');
  if (planError && !plan) { host.innerHTML = `<span class="status"><i class="mk mk--failed"></i>NO PLAN</span>`; return; }
  if (!view) { host.innerHTML = `<span class="status"><i class="mk mk--draft"></i>READING</span>`; return; }
  const [mk, label] = MARK[view.state] || ['draft', String(view.state || '').toUpperCase()];
  let h = `<span class="status"><i class="mk mk--${mk}"></i>${esc(label)}</span>`;
  if (view.state === 'done' && view.verdict) {
    const c = view.comparison || {};
    const kind = view.verdict.toLowerCase();
    const glyph = { exact: '=', differs: '≠', uncompared: '?' }[kind] || '';
    const count = c.of != null ? `${c.equal} of ${c.of} slots equal` : '';
    h += `<span class="verdict ${esc(kind)}" aria-label="${esc(view.verdict)}"><span aria-hidden="true">${glyph}</span>&nbsp;${esc(view.verdict)}${count ? `<small>${esc(count)}</small>` : ''}</span>`;
  }
  host.innerHTML = h;
}

function renderSide() {
  const host = $('b-side');
  renderState();
  if (!view) { host.innerHTML = planError ? `<p class="note nodata">The exam plan cannot be read: ${esc(planError)}</p>` : ''; return; }
  const v = view;
  if (planError && !plan) { host.innerHTML = `<p class="note nodata">The exam plan cannot be read, so there is nothing to step: ${esc(planError)}</p>`; return; }
  if (v.state === 'error') { host.innerHTML = `<p class="note nodata">${esc(v.note)}</p>`; return; }
  if (v.state === 'none') {
    host.innerHTML = `<p class="note nodata">${esc(v.note || 'No episode has been stepped for this brain, draw and tank.')} STEP re-steps it with the nursery's own code on one thread, and the verdict compares each net with the run's own number, bit for bit.</p>` + timingNote(v);
    return;
  }
  if (v.state === 'running') {
    const p = v.progress || {};
    host.innerHTML = `<p class="why">Started ${esc(clock(v.startedUtc))}${v.pid != null ? ` · pid ${esc(v.pid)}` : ''}. The stream written so far is <b class="hi">${esc(mb(p.framesBytes))}</b>${p.frames != null ? ` (${p.frames} frames)` : ''}; the header, and with it the frame count, is written when the episode ends, so no share done can be shown. This page asks again every ${pollSeconds} s.</p>` +
      `<p class="why"><button class="tag" id="check" type="button" style="margin-left:0">CHECK NOW</button></p>` + (v.reason ? `<p class="note">reason given: ${esc(v.reason)}</p>` : '');
    $('check').addEventListener('click', () => tick());
    return;
  }
  if (v.state === 'stopped') {
    const s = v.stoppedBy || {};
    host.innerHTML = `<p class="why"><b class="hi">${esc(v.reason || 'stopped')}</b>${s.who ? ` · ${esc(s.who)} was timing: ${esc(s.why || '')}` : ''}${s.until ? ` until ${esc(clock(s.until))}` : ''}. ${s.note ? esc(s.note) + '. ' : ''}Step it again when the timing read is over; the stopped files are kept beside the new ones.</p>` + timingNote(v);
    return;
  }
  if (v.state === 'failed') {
    host.innerHTML = `<p class="why">${esc(v.note || 'The process ended without a result.')}</p>` + (v.logTail && v.logTail.length ? `<pre class="log" aria-label="the log's tail">${esc(v.logTail.join('\n'))}</pre>` : '<p class="note nodata">Its log is empty.</p>');
    return;
  }
  // done
  const c = v.comparison || {};
  let h = '';
  if (c.reason) h += `<p class="why">${esc(c.reason)}.</p>`;
  const slots = c.slots || [];
  h += `<div class="scroll"><table class="t slots" aria-label="each slot's expected and re-stepped net"><thead><tr><th class="l">member</th><th>body</th><th>expected<br>run's per[${v.draw}]</th><th>re-stepped<br>net</th><th class="l">bits equal</th></tr></thead><tbody>` +
    slots.map(s => slotRow(s)).join('') + '</tbody></table></div>';
  h += `<p class="note" style="margin:.5rem 0 0">${esc(c.rule || '')} The verdict is the numbers' alone: a different build is shown below, not judged.</p>`;
  h += provenance(v.provenance);
  host.innerHTML = h;
}

function timingNote(v) {
  const tr = v.timingRead;
  return tr && tr.state === 'declared' ? `<p class="note">A timing read is declared: ${esc(tr.who || 'someone')} is timing, because ${esc(tr.why || '')}, until ${esc(clock(tr.until))}.</p>` : '';
}

function slotRow(s) {
  const eq = s.equal;
  const cls = eq === true ? 'eq' : eq === false ? 'ne' : 'un';
  const exp = s.expected || {}, act = s.actual || {};
  const expNet = exp.known ? (exp.net == null ? 'null' : exp.net) : '—';
  const expSub = exp.known ? (exp.net == null ? 'did not land' : exp.bits || '') : (exp.note ? 'not in the run' : '');
  const actNet = act.net == null ? 'null' : act.net;
  const actSub = act.net == null ? 'did not land' : act.bits || '';
  const mark = eq === true ? '<b>=</b>equal' : eq === false ? '<b>≠</b>DIFFERS' : '<b>·</b>not compared';
  const tip = esc([exp.note, s.note].filter(Boolean).join(' · '));
  return `<tr class="${cls}"><td class="l"><i class="sw" style="background:${colour(s.slot)}"></i>${s.slot} · ${esc(s.genome ?? 'pool ' + s.pool)}</td>` +
    `<td>${s.body ?? '—'}</td>` +
    `<td title="${esc(exp.known ? 'the run\'s per[' + view.draw + '] as the nursery wrote it' : exp.note || '')}"><span class="v">${esc(expNet)}</span><span class="fr">${esc(expSub)}</span></td>` +
    `<td title="the re-stepped net as the nursery writes it"><span class="v">${esc(actNet)}</span><span class="fr">${esc(actSub)}</span></td>` +
    `<td class="mark${eq == null ? ' dim' : ''}" title="${tip}">${mark}</td></tr>`;
}

const BUILD = [['gitCommit', 'commit'], ['coreHash', 'core'], ['dynamicsHash', 'dynamics'], ['farmHash', 'farm'], ['nurseryHash', 'nursery']];
const short = s => (s ? String(s).slice(0, 8) : '—');

function provenance(p) {
  if (!p) return '';
  const e = p.episodeBuild || {}, r = p.runBuild || {};
  const differ = p.buildsDiffer || [];
  const named = BUILD.filter(([k]) => differ.includes(k));
  const build = p.sameBuild === true ? 'same build as the run'
    : differ.length ? 'the build differs from the run\'s: ' + named.map(([k, w]) => `${w} ${short(e[k])} against ${short(r[k])}`).join(', ')
    : 'the build cannot be compared with the run\'s (' + ((p.buildsNotComparable || []).map(k => (BUILD.find(b => b[0] === k) || [0, k])[1]).join(', ') || 'nothing recorded') + ')';
  const planWord = p.planSource === 'recorded' ? 'recorded' : p.planSource === 'rebuilt' ? 'rebuilt' : 'unknown';
  const rows = BUILD.map(([k, w]) => {
    const ne = differ.includes(k);
    return `<tr class="${ne ? 'ne' : ''}"><td class="k">${w}</td><td>${esc(short(e[k]))}</td><td>${ne ? '≠ ' : ''}${esc(short(r[k]))}</td></tr>`;
  }).join('');
  return `<details class="prov"><summary><b>provenance</b> · plan ${esc(planWord)} · ${esc(build)}</summary>` +
    `<div>${esc(p.planSourceNote || '')}</div>` +
    `<table class="hashes"><tr><th></th><th>this episode</th><th>the run (${esc(p.runBuildFrom || 'not recorded')})</th></tr>${rows}</table>` +
    `<div>stepped on ${esc(e.runtime || '—')}, tiered compilation ${esc(e.tieredCompilation ?? '—')}${e.gitDirty ? ', from a dirty tree' : ''}${e.note ? ' · ' + esc(e.note) : ''}</div></details>`;
}

// ---------------------------------------------------------------- the map

function mapGeometry(W, H, D) {
  const x = model.extent;
  const pad = 2.8 * rem() * D;
  const wx = Math.max(x.x1 - x.x0, 12), wz = Math.max(x.z1 - x.z0, 12);
  const s = Math.min((W - 2 * pad) / (wx + 4), (H - 2 * pad) / (wz + 4));
  const cx = (x.x0 + x.x1) / 2, cz = (x.z0 + x.z1) / 2;
  return { s, ox: W / 2 - cx * s, oz: H / 2 - cz * s, P: (px, pz) => [W / 2 + (px - cx) * s, H / 2 + (pz - cz) * s] };
}

function drawMap() {
  const canvas = $('map'), box = $('mapbox');
  const D = devicePixelRatio || 1;
  const W = Math.max(2, Math.round(box.clientWidth * D)), H = Math.max(2, Math.round(box.clientHeight * D));
  if (canvas.width !== W || canvas.height !== H) { canvas.width = W; canvas.height = H; islandKey = ''; }
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, W, H);
  const empty = $('map-empty');
  if (!model || model.problem) {
    empty.style.display = 'flex';
    empty.innerHTML = `<p class="note nodata">${esc(model && model.problem ? model.problem : mapEmptyText())}</p>`;
    return;
  }
  empty.style.display = 'none';
  const g = mapGeometry(W, H, D), { P, s } = g, R = rem() * D;
  const x = model.extent;

  // a metre grid, every 5 m, and its labels along the edges
  ctx.lineWidth = D; ctx.strokeStyle = 'rgba(150,230,255,.07)'; ctx.fillStyle = '#9dbdc6'; ctx.font = `${.78 * R}px "Plex Mono", Consolas, monospace`;
  const gx0 = Math.floor((x.x0 - 3) / 5) * 5, gx1 = Math.ceil((x.x1 + 3) / 5) * 5, gz0 = Math.floor((x.z0 - 3) / 5) * 5, gz1 = Math.ceil((x.z1 + 3) / 5) * 5;
  ctx.beginPath();
  for (let gxv = gx0; gxv <= gx1; gxv += 5) { const [u] = P(gxv, 0); ctx.moveTo(u, 0); ctx.lineTo(u, H); }
  for (let gzv = gz0; gzv <= gz1; gzv += 5) { const [, v] = P(0, gzv); ctx.moveTo(0, v); ctx.lineTo(W, v); }
  ctx.stroke();
  ctx.textAlign = 'left';
  for (let gxv = gx0; gxv <= gx1; gxv += 10) { const [u] = P(gxv, 0); if (u > 3 * R && u < W - 3 * R) ctx.fillText(gxv + ' m', u + .3 * R, H - .5 * R); }
  for (let gzv = gz0; gzv <= gz1; gzv += 10) { const [, v] = P(0, gzv); if (v > 2 * R && v < H - 2 * R) ctx.fillText(gzv + ' m', .5 * R, v - .3 * R); }

  // the extent of the paths: the glass is not in the episode's header, so this is the water the bodies used and not the tank's outline
  const [fx0, fz0] = P(x.x0, x.z0), [fx1, fz1] = P(x.x1, x.z1);
  ctx.setLineDash([6 * D, 5 * D]); ctx.strokeStyle = 'rgba(150,230,255,.3)'; ctx.lineWidth = D;
  ctx.strokeRect(fx0, fz0, fx1 - fx0, fz1 - fz0); ctx.setLineDash([]);
  ctx.fillStyle = '#9dbdc6'; ctx.fillText('extent of the paths', fx0 + .4 * R, fz0 - .4 * R);

  // the snow the bodies met, drawn once to a layer of its own
  const key = [W, H, model.island.length, s.toFixed(3), g.ox.toFixed(1)].join('|');
  if (islandKey !== key) {
    islandLayer = islandLayer || document.createElement('canvas');
    islandLayer.width = W; islandLayer.height = H;
    const ic = islandLayer.getContext('2d');
    ic.clearRect(0, 0, W, H);
    // binned to 1.5 m cells, so that a body that lingered in an island does not saturate it: a cell is lit once, however many samples fell in it
    const cellM = 1.5, cells = new Set();
    for (let i = 0; i < model.island.length; i += 2) cells.add(Math.floor(model.island[i] / cellM) + ',' + Math.floor(model.island[i + 1] / cellM));
    ic.fillStyle = 'rgba(80,220,180,.13)';
    for (const c of cells) {
      const [cx, cz] = c.split(',').map(Number), [u, v] = P((cx + .5) * cellM, (cz + .5) * cellM);
      ic.beginPath(); ic.arc(u, v, Math.max(2 * D, cellM * s * .6), 0, 6.2832); ic.fill();
    }
    islandKey = key;
  }
  ctx.drawImage(islandLayer, 0, 0);

  const [k, k2, u] = at(t);

  // the placed carrion: filled while it holds energy, a ring once it is emptied
  model.corpses.forEach((c, i) => {
    const [cu, cv] = P(cell(k, c.x), cell(k, c.z));
    const j = c.j != null ? cell(k, c.j) : NaN, j0 = model.joules0[i];
    const frac = Number.isFinite(j) && Number.isFinite(j0) && j0 > 0 ? j / j0 : 1;
    const r = Math.max(3.2 * D, s * .45 * Math.sqrt(Math.max(frac, .15)));
    ctx.beginPath(); ctx.arc(cu, cv, r, 0, 6.2832);
    if (frac < .02) { ctx.strokeStyle = '#f0a050'; ctx.lineWidth = 1.5 * D; ctx.stroke(); }
    else { ctx.fillStyle = '#f0a050'; ctx.fill(); }
  });

  // each body's root path so far, then the body where it is
  model.members.forEach(m => {
    if (!m.px) return;
    ctx.strokeStyle = colour(m.slot); ctx.lineWidth = 1.7 * D; ctx.globalAlpha = .75; ctx.setLineDash(DASH[m.slot % DASH.length] ? DASH[m.slot % DASH.length].split(' ').map(n => n * D * .8) : []);
    ctx.beginPath();
    let pen = false;
    for (let i = 0; i <= k; i++) {
      const px = m.px[i], pz = m.pz[i];
      if (!Number.isFinite(px)) { pen = false; continue; }
      const [a, b] = P(px, pz);
      if (pen) ctx.lineTo(a, b); else { ctx.moveTo(a, b); pen = true; }
    }
    const rx = lerpCol(m.links[0].x, k, k2, u), rz = lerpCol(m.links[0].z, k, k2, u);
    if (pen && Number.isFinite(rx)) { const [a, b] = P(rx, rz); ctx.lineTo(a, b); }
    ctx.stroke(); ctx.setLineDash([]); ctx.globalAlpha = 1;

    const alive = m.alive != null && cell(k, m.alive) > .5;
    if (alive && Number.isFinite(rx)) {
      const pts = m.links.map(l => P(lerpCol(l.x, k, k2, u), lerpCol(l.z, k, k2, u)));
      ctx.strokeStyle = colour(m.slot); ctx.lineWidth = 3.2 * D; ctx.lineCap = 'round'; ctx.beginPath();
      pts.forEach(([a, b], i) => (i ? ctx.lineTo(a, b) : ctx.moveTo(a, b)));
      ctx.stroke(); ctx.lineCap = 'butt';
      const [a, b] = pts[0];
      ctx.beginPath(); ctx.arc(a, b, Math.max(4.2 * D, s * .22), 0, 6.2832); ctx.fillStyle = colour(m.slot); ctx.fill(); ctx.strokeStyle = '#03111a'; ctx.lineWidth = 1.5 * D; ctx.stroke();
      label(ctx, a + 7 * D, b - 7 * D, String(m.slot), D, R);
    } else if (!alive && m.deathK > 0) {
      const dk = Math.max(0, m.deathK - 1), px = m.px[dk], pz = m.pz[dk];
      if (Number.isFinite(px)) {
        const [a, b] = P(px, pz), q = 5 * D;
        ctx.strokeStyle = colour(m.slot); ctx.lineWidth = 2.2 * D; ctx.beginPath(); ctx.moveTo(a - q, b - q); ctx.lineTo(a + q, b + q); ctx.moveTo(a + q, b - q); ctx.lineTo(a - q, b + q); ctx.stroke();
        label(ctx, a + 8 * D, b - 8 * D, String(m.slot), D, R);
      }
    }
  });

  // a scale bar
  const bar = Math.max(x.x1 - x.x0, x.z1 - x.z0) > 30 ? 10 : 5, bx = 1.4 * R, by = H - 2.2 * R;
  ctx.strokeStyle = '#c4dde3'; ctx.lineWidth = 2 * D; ctx.beginPath(); ctx.moveTo(bx, by); ctx.lineTo(bx + bar * s, by); ctx.moveTo(bx, by - .3 * R); ctx.lineTo(bx, by + .3 * R); ctx.moveTo(bx + bar * s, by - .3 * R); ctx.lineTo(bx + bar * s, by + .3 * R); ctx.stroke();
  ctx.fillStyle = '#c4dde3'; ctx.font = `${.85 * R}px "Plex Mono", Consolas, monospace`; ctx.fillText(bar + ' m', bx + bar * s + .6 * R, by + .3 * R);
}

function label(ctx, x, y, text, D, R) {
  ctx.font = `${.95 * R}px "Plex Mono", Consolas, monospace`;
  ctx.lineWidth = 3 * D; ctx.strokeStyle = '#02080c'; ctx.strokeText(text, x, y);
  ctx.fillStyle = '#e8f8fc'; ctx.fillText(text, x, y);
}

function mapEmptyText() {
  if (sel.run == null || sel.brain == null || sel.draw == null || sel.tank == null) return "This draw has no tank dealt in the plan, so there is no episode to read.";
  if (!view) return 'Reading the episode.';
  if (view.state === 'none') return 'No episode has been stepped for this brain, draw and tank. STEP re-steps it; the paths appear here when it is done.';
  if (view.state === 'running') return 'The episode is being stepped. Its paths appear here when it is done.';
  if (view.state === 'stopped') return 'The step was stopped, so there are no frames to play.';
  if (view.state === 'failed') return 'The step failed, so there are no frames to play. Its log is on the right.';
  if (view.state === 'done') return 'Reading the finished episode.';
  return 'There is nothing to play.';
}

// ---------------------------------------------------------------- the key under the map, the chart, the timeline, the events

function renderMapKey() {
  const k = $('mapkey');
  if (!model || model.problem) { k.innerHTML = ''; $('map-aside').textContent = ''; $('map-q').textContent = 'Where did they go?'; return; }
  const e = model.epi;
  const slots = (view && view.comparison && view.comparison.slots) || [];
  $('map-q').textContent = `${model.members.length} bodies, ${model.corpses.length} carrion`;
  $('map-aside').textContent = `${secs(model.t1)} · ${model.N} frames at ${e.sampleRate} Hz · seed ${e.episodeSeed}`;
  const isl = model.islandRule;
  k.innerHTML = model.members.map(m => {
    const s = slots[m.slot];
    return `<span class="m"><b><i class="sw" style="background:${colour(m.slot)}"></i>${m.slot} · ${esc(s && s.genome ? s.genome : 'pool ' + m.pool)}</b><span id="mk-${m.slot}">body ${esc(m.body)}</span></span>`;
  }).join('') +
    `<span><i class="sw" style="background:#f0a050"></i>carrion, a ring once emptied</span>` +
    `<span><i class="sw" style="background:rgba(80,220,180,.55);border-color:rgba(80,220,180,.9)"></i>${isl ? `snow at or above ${esc(fmt(isl.enterAt, 3))} ${esc(model.snowUnit)}, where the bodies met it` : 'no snow scan was read'}</span>` +
    `<span><span class="glyph">×</span>death</span>` +
    `<span class="dim" style="white-space:normal">The tank's glass is not in the episode's header: the dashed frame is the extent of the paths.</span>`;
}

function updateKey() {
  if (!model || model.problem) return;
  const [k, k2, u] = at(t);
  for (const m of model.members) {
    const n = $('mk-' + m.slot);
    if (!n) continue;
    const alive = m.alive != null && cell(k, m.alive) > .5;
    const r = m.reserve != null ? lerpCol(m.reserve, k, k2, u) : NaN;
    n.textContent = !m.landed ? `body ${m.body} · did not land` : alive ? `body ${m.body} · ${fmt(r, 4)} ${model.energyUnit}` : `body ${m.body} · died${m.deathK >= 0 ? ' at ' + secs(model.time[m.deathK]) : ''}`;
  }
}

function renderChart() {
  const svg = $('c-energy');
  svg.innerHTML = '';
  if (!model || model.problem) { $('chart-legend').innerHTML = ''; $('chart-aside').textContent = ''; return; }
  const withE = model.members.filter(m => m.reserve != null && m.landed);
  const ds = Math.max(1, Math.ceil(model.N / 700));
  const series = withE.map(m => { const a = []; for (let k = 0; k < model.N; k += ds) a.push(cell(k, m.reserve)); a.push(cell(model.N - 1, m.reserve)); return a; });
  $('chart-aside').textContent = `reserve, ${model.energyUnit}`;
  $('chart-legend').innerHTML = withE.length ? `<span class="dim">one line a member, as the map; its number at the line's last point</span>` : '<span class="dim">no member holds an energy channel</span>';
  if (!withE.length) return;
  const [lo, hi] = extent(series);
  const xv = []; xv[0] = secs(model.t0); xv[Math.round((model.N - 1) / 2)] = secs((model.t0 + model.t1) / 2); xv[model.N - 1] = secs(model.t1);
  const c = frame(svg, { n: model.N, lo, hi, xvals: xv, unit: model.energyUnit, padL: 3.6 });
  const X = tt => c.padL + (tt - model.t0) / (model.t1 - model.t0 || 1) * (c.w - c.padL - c.padR);
  withE.forEach((m, i) => {
    let d = '', pen = false, lastX = 0, lastY = 0, lastOk = false;
    series[i].forEach((v, j) => {
      const tt = j === series[i].length - 1 ? model.t1 : model.time[Math.min(model.N - 1, j * ds)];
      if (!Number.isFinite(v)) { pen = false; return; }
      const x = X(tt), y = c.Y(v);
      d += (pen ? 'L' : 'M') + x.toFixed(1) + ' ' + y.toFixed(1);
      pen = true; lastX = x; lastY = y; lastOk = true;
    });
    el(svg, 'path', { d, style: `fill:none;stroke:${colour(m.slot)};stroke-width:1.6;stroke-linejoin:round${DASH[m.slot % DASH.length] ? ';stroke-dasharray:' + DASH[m.slot % DASH.length] : ''}` });
    if (lastOk) el(svg, 'text', { x: lastX + 4, y: lastY + 4, class: 'halo', style: 'fill:var(--ink-hi)' }, String(m.slot));
  });
  el(svg, 'line', { id: 'chart-mark', x1: X(t), x2: X(t), y1: c.padT, y2: c.h - c.padB, style: 'stroke:var(--ink-hi);stroke-width:1.2' });
  svg._geom = { X, padL: c.padL, padR: c.padR, w: c.w };
}

$('c-energy').addEventListener('click', ev => {
  const g = $('c-energy')._geom;
  if (!g || !model || model.problem) return;
  const r = $('c-energy').getBoundingClientRect();
  seek(model.t0 + (ev.clientX - r.left - g.padL) / (g.w - g.padL - g.padR) * (model.t1 - model.t0));
});

function glyphAttrs(type, x, y) {
  const q = 5;
  switch (type) {
    case 'carrionEaten': return ['path', { d: `M${x - q} ${y}h${2 * q}M${x} ${y - q}v${2 * q}` }];
    case 'death': return ['path', { d: `M${x - q + 1} ${y - q + 1}l${2 * q - 2} ${2 * q - 2}M${x + q - 1} ${y - q + 1}l${-(2 * q - 2)} ${2 * q - 2}` }];
    case 'islandEntered': return ['path', { d: `M${x} ${y - q}L${x + q} ${y + q - 1}L${x - q} ${y + q - 1}Z` }];
    case 'birthHeld': return ['path', { d: `M${x} ${y - q}L${x + q} ${y}L${x} ${y + q}L${x - q} ${y}Z` }];
    default: return ['circle', { cx: x, cy: y, r: 2.5 }];
  }
}

let tl = null;
function renderTimeline() {
  const svg = $('c-time');
  svg.innerHTML = '';
  tl = null;
  const legend = $('time-legend');
  if (!model || model.problem) {
    $('time-q').textContent = '—'; $('t-now').textContent = 't = —'; legend.innerHTML = '';
    svg.setAttribute('aria-valuemax', 0); svg.setAttribute('aria-valuenow', 0);
    return;
  }
  const w = svg.clientWidth, h = svg.clientHeight, R = rem();
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  const padL = R, padR = R, X = tt => padL + (tt - model.t0) / (model.t1 - model.t0 || 1) * (w - padL - padR);
  const yb = h * .6, th = .35 * R;
  $('time-q').textContent = `${secs(model.t1)} · ${model.events.length} events`;
  svg.setAttribute('aria-valuemin', model.t0); svg.setAttribute('aria-valuemax', model.t1);
  const types = [...new Set(model.events.map(e => e.type))];
  legend.innerHTML = types.map(ty => `<span><span class="glyph">${esc(eventInfo(ty).glyph)}</span>${esc(eventInfo(ty).words)}</span>`).join('') + (model.eventsNote ? `<span class="dim">${esc(model.eventsNote)}</span>` : '');
  el(svg, 'rect', { x: X(model.t0), y: yb, width: X(model.t1) - X(model.t0), height: th, rx: th / 2, style: 'fill:var(--track)' });
  const played = el(svg, 'rect', { x: X(model.t0), y: yb, width: 0, height: th, rx: th / 2, style: 'fill:var(--run)' });
  const span = model.t1 - model.t0, step = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600].find(s => span / s <= 10) || 600;
  for (let tt = Math.ceil(model.t0 / step) * step; tt <= model.t1 + 1e-6; tt += step) {
    el(svg, 'line', { x1: X(tt), x2: X(tt), y1: yb + th, y2: yb + th + .35 * R, style: 'stroke:var(--axis)' });
    el(svg, 'text', { x: X(tt), y: yb + th + 1.3 * R, 'text-anchor': 'middle' }, secs(tt));
  }
  for (const e of model.events) {
    const [tag, a] = glyphAttrs(e.type, X(e.t), yb - 1.1 * R);
    const g = el(svg, 'g');
    el(g, tag, { ...a, style: 'fill:none;stroke:var(--ink-hi);stroke-width:2;stroke-linejoin:round' });
    el(g, 'title', {}, `${secs(e.t)} · ${describe(e).text}`);
  }
  const head = el(svg, 'circle', { cx: X(t), cy: yb + th / 2, r: .55 * R, style: 'fill:var(--ink-hi);filter:var(--glow)' });
  tl = { X, played, head, padL, padR, w, track: { y: yb } };
}

function seek(tt) {
  if (!model || model.problem) return;
  setTime(Math.min(Math.max(tt, model.t0), model.t1));
}

function timeFromPointer(ev) {
  const svg = $('c-time');
  if (!tl) return null;
  const r = svg.getBoundingClientRect();
  return model.t0 + (ev.clientX - r.left - tl.padL) / (tl.w - tl.padL - tl.padR) * (model.t1 - model.t0);
}

(() => {
  const svg = $('c-time');
  let down = false;
  svg.addEventListener('pointerdown', ev => { if (!tl) return; down = true; svg.setPointerCapture(ev.pointerId); svg.focus(); seek(timeFromPointer(ev)); });
  svg.addEventListener('pointermove', ev => { if (down) seek(timeFromPointer(ev)); });
  const up = ev => { down = false; try { svg.releasePointerCapture(ev.pointerId); } catch (e) { /* not captured */ } };
  svg.addEventListener('pointerup', up); svg.addEventListener('pointercancel', up);
})();

function describe(e) {
  const info = eventInfo(e.type);
  const who = e.member != null ? `member ${e.member}` : '';
  const parts = [];
  if (e.type === 'carrionEaten') {
    parts.push(`${fmt(e.joules, 3)} J taken from carrion`);
    if (e.corpsesEmptied) parts.push(`${e.corpsesEmptied} emptied`);
    parts.push(e.member != null ? `${who}${e.memberIsInferred ? ' (inferred)' : ''}` : (e.candidates && e.candidates.length ? `by one of ${e.candidates.join(', ')} (inferred)` : 'by no body that earned food that step'));
  } else if (e.type === 'islandEntered') {
    parts.push(who); if (e.snow != null) parts.push(`snow ${fmt(e.snow, 3)}${model && model.snowUnit ? ' ' + model.snowUnit : ''}`);
  } else if (e.type === 'birthHeld') {
    parts.push(who); if (e.funds != null) parts.push(`funds ${fmt(e.funds, 4)} of a gate of ${fmt(e.gate, 4)}`);
  } else {
    if (who) parts.push(who);
    const rest = Object.entries(e).filter(([k]) => !['t', 'type', 'member'].includes(k)).map(([k, v]) => `${k} ${typeof v === 'number' ? fmt(v, 4) : JSON.stringify(v)}`);
    parts.push(...rest);
  }
  return { glyph: info.glyph, text: `${info.words}${parts.filter(Boolean).length ? ' · ' + parts.filter(Boolean).join(' · ') : ''}` };
}

function renderEvents() {
  const host = $('b-events');
  if (!model || model.problem) {
    $('ev-q').textContent = '—';
    host.innerHTML = `<p class="note nodata">${esc(view && view.state === 'done' && model && model.problem ? model.problem : 'The events appear when the episode is done.')}</p>`;
    return;
  }
  $('ev-q').textContent = `${model.events.length} in ${secs(model.t1)}`;
  if (!model.events.length) { host.innerHTML = `<p class="note nodata">${esc(model.eventsNote || 'The episode recorded no event: no carrion taken, no death, no island entered.')}</p>`; return; }
  host.innerHTML = model.events.map((e, i) => `<div class="ev" data-i="${i}" role="button" tabindex="0" title="show this moment"><span class="tt">${esc(secs(e.t))}</span><span class="gl" aria-hidden="true">${esc(eventInfo(e.type).glyph)}</span><span class="dd">${esc(describe(e).text)}</span></div>`).join('') +
    (model.eventsNote ? `<p class="note nodata">${esc(model.eventsNote)}</p>` : '');
  host.querySelectorAll('.ev').forEach(n => {
    const go = () => seek(model.events[+n.dataset.i].t);
    n.addEventListener('click', go);
    n.addEventListener('keydown', ev => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); ev.stopPropagation(); go(); } });
  });
}

let shownEvent = -2;
function updateEvents() {
  if (!model || model.problem) return;
  let last = -1;
  model.events.forEach((e, i) => { if (e.t <= t + 1e-6) last = i; });
  if (last === shownEvent) return;
  shownEvent = last;
  $('b-events').querySelectorAll('.ev').forEach((n, i) => { n.classList.toggle('past', i <= last); n.classList.toggle('now', i === last); });
}

// ---------------------------------------------------------------- time

function setTime(tt) {
  t = tt;
  dirty = true;
  if (!model || model.problem) return;
  $('t-now').textContent = 't = ' + secs(t);
  const svg = $('c-time');
  svg.setAttribute('aria-valuenow', t); svg.setAttribute('aria-valuetext', secs(t));
  if (tl) { tl.played.setAttribute('width', Math.max(0, tl.X(t) - tl.X(model.t0))); tl.head.setAttribute('cx', tl.X(t)); }
  const mark = document.getElementById('chart-mark'), g = $('c-energy')._geom;
  if (mark && g) { const x = g.X(t); mark.setAttribute('x1', x); mark.setAttribute('x2', x); }
  updateKey();
  updateEvents();
}

function setPlaying(on) {
  playing = on && !!model && !model.problem;
  const b = $('play');
  b.textContent = playing ? '❚❚ PAUSE' : '▶ PLAY';
  b.setAttribute('aria-pressed', String(playing));
}

$('play').addEventListener('click', () => { if (model && t >= model.t1 - 1e-6) seek(model.t0); setPlaying(!playing); });
document.querySelectorAll('[data-speed]').forEach(b => b.addEventListener('click', () => {
  speed = +b.dataset.speed;
  document.querySelectorAll('[data-speed]').forEach(x => x.setAttribute('aria-pressed', String(x === b)));
}));

addEventListener('keydown', ev => {
  const a = document.activeElement && document.activeElement.tagName;
  if (['SELECT', 'INPUT', 'TEXTAREA', 'BUTTON', 'SUMMARY', 'A'].includes(a) || ev.ctrlKey || ev.metaKey || ev.altKey || !model || model.problem) return;
  const speeds = [...document.querySelectorAll('[data-speed]')];
  if (ev.key === ' ') { ev.preventDefault(); if (t >= model.t1 - 1e-6) seek(model.t0); setPlaying(!playing); }
  else if (ev.key === 'ArrowRight') { ev.preventDefault(); seek(t + 1); }
  else if (ev.key === 'ArrowLeft') { ev.preventDefault(); seek(t - 1); }
  else if (ev.key === 'Home') { ev.preventDefault(); seek(model.t0); }
  else if (ev.key === 'End') { ev.preventDefault(); seek(model.t1); }
  else if (ev.key === '[' || ev.key === ']') {
    const i = speeds.findIndex(b => +b.dataset.speed === speed), j = Math.min(speeds.length - 1, Math.max(0, i + (ev.key === ']' ? 1 : -1)));
    speeds[j].click();
  }
});

function loop(now) {
  if (gone) return;
  if (playing && model && !model.problem) {
    const next = Math.min(model.t1, t + (now - lastNow) / 1000 * speed);
    setTime(next);
    if (next >= model.t1) setPlaying(false);
  }
  lastNow = now;
  if (dirty) { dirty = false; drawMap(); }
  requestAnimationFrame(loop);
}

// ---------------------------------------------------------------- putting it together

function renderAll() {
  renderSide();
  renderMapKey();
  renderChart();
  renderTimeline();
  renderEvents();
  shownEvent = -2;
  setPlaying(playing && !!model);
  if (model && !model.problem) setTime(t); else { t = 0; dirty = true; }
  updateStep();
  dirty = true;
}

/** A run the screen cannot read an exam plan of (no such run, none recorded, or one that cannot be rebuilt): the shared refusal in the main area, with the way back. */
function refuseTank() {
  gone = true;
  const run = sel.run, unknown = planStatus === 404, none = run == null;
  const enc = encodeURIComponent;
  refuse({
    crumb: 'tank · run ' + (run ?? '—'), kicker: 'Tank',
    title: none ? 'No run to show' : unknown ? 'No such run' : 'No exam plan to read',
    message: planError,
    links: [...(none || unknown ? [] : [['run.html?id=' + enc(run), 'BACK TO THE RUN']]), ['registry.html', 'BACK TO THE REGISTRY']],
  });
  document.title = 'Nursery room · tank · ' + (none ? 'no run' : unknown ? 'run ' + run + ' not found' : 'run ' + run + ' has no exam plan');
  // a bare footer: the player's keys and its note belong to a screen that is not there
  const foot = document.getElementById('foot');
  if (foot) foot.textContent = '';
}

async function init() {
  try {
    const reg = await getResource('nursery://registry');
    runs = reg.items.filter(i => i.exists);
  } catch (e) { runs = []; }
  if (sel.run == null) {
    // the run last looked at on any screen (with its brain, draw and tank), else the newest
    const kept = getSel();
    const hit = kept.run != null && runs.find(r => r.id === kept.run || r.folderName === kept.run);
    if (hit) { sel.run = hit.id; if (sel.brain == null) sel.brain = kept.brain ?? null; if (sel.draw == null) sel.draw = kept.draw ?? null; if (sel.tank == null) sel.tank = kept.tank ?? null; }
    else if (runs.length) sel.run = runs[0].id;
  }
  fillRuns();
  syncUrl();
  await loadPlan();
  if (planError) { refuseTank(); return; }
  syncUrl();
  renderAll();
  poll(tick, pollSeconds);
}

onResize(() => { if (gone) return; islandKey = ''; renderChart(); renderTimeline(); if (model && !model.problem) setTime(t); dirty = true; });

// The machine's load takes seconds to read: the screen's own reading does not wait for it.
mountMachine(r => { lastRoom = r; updateStep(); }).then(room => { if (room) pollSeconds = room.pollSeconds; });
requestAnimationFrame(loop);
await init();
