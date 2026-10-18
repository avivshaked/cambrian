// Shared by every screen: reading the room's API, polling, small formatters, the machine strip.
// Nothing in here names a sense, an output, a reward or a body part: names come from the data.

export const NS = 'http://www.w3.org/2000/svg';

export function esc(s) {
  return String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
}

export async function getJson(url) {
  const r = await fetch(url, { cache: 'no-store' });
  const body = await r.json().catch(() => ({}));
  if (!r.ok) { const e = new Error(body.error || (r.status + ' ' + r.statusText)); e.status = r.status; throw e; }
  return body;
}

/**
 * Reads a resource. The room answers a refused read with HTTP 200 and `{ ok: false, refused: true, status, error }` (a 4xx would be logged in the console as an
 * error whatever the page does with it, and a run with no exam plan is an ordinary state); here it throws an Error with `.status` (404 nothing there, 400 a bad
 * address) and `.refused = true`, so a page asks `e.refused` to tell a refusal it can say from a fault of the room.
 */
export async function getResource(uri) {
  const body = await getJson('/api/resource?uri=' + encodeURIComponent(uri));
  if (body && body.refused) { const e = new Error(body.error || 'refused'); e.status = body.status || 400; e.refused = true; throw e; }
  return body;
}
export const getRoom = () => getJson('/api/room');

/** Calls an operation by id. Resolves to the envelope {operation, ok, result|error}; a refused call resolves too. */
export async function callOp(id, args) {
  const r = await fetch('/api/op/' + encodeURIComponent(id), {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(args || {}),
  });
  return r.json();
}

/**
 * Runs `fn` now and then every `seconds`; a run still going when the next is due is skipped. A read that fails is said on the page (a bar under the
 * strip, cleared by the next read that works) as well as in the console. When `fn` resolves to 'stop' the polling ends: a page that has said what is wrong
 * and has nothing more to read says so that way.
 */
export function poll(fn, seconds) {
  let busy = false, id = null, stopped = false;
  const once = async () => {
    if (busy || stopped) return;
    busy = true;
    try {
      const r = await fn();
      showPollError(null);
      if (r === 'stop') { stopped = true; clearInterval(id); }
    } catch (e) { if (!e.refused) console.error(e); showPollError(e); } finally { busy = false; }
  };
  once();
  id = setInterval(once, Math.max(1, seconds) * 1000);
  return id;
}

/** The bar that says a read failed (null clears it). One bar a page, under the strip. */
export function showPollError(e) {
  let bar = document.getElementById('poll-error');
  if (e == null) { if (bar) bar.remove(); return; }
  if (!bar) {
    bar = document.createElement('div');
    bar.id = 'poll-error';
    bar.className = 'pollerr';
    bar.setAttribute('role', 'alert');
    const strip = document.getElementById('strip');
    if (strip) strip.after(bar); else document.body.prepend(bar);
  }
  bar.innerHTML = `<span class="plate plate--alarm">READ FAILED</span><span>${esc((e && e.message) || String(e))}</span>`;
}

/**
 * A page that cannot show what was asked for says so in its main area, as the genome screen does: the crumb (in the strip), the message and a way
 * back. The main area's panels are replaced (an empty skeleton would read as a page with nothing in it), so the page's own polling ends after it.
 * `links` are [href, label] pairs.
 */
export function refuse({ crumb, kicker, title, message, links = [] }) {
  const main = document.querySelector('main.page');
  const c = document.getElementById('crumb');
  if (c && crumb != null) c.innerHTML = `<b>${esc(crumb)}</b>`;
  // The page has nothing it can show, so what its address names is not what the other tabs should carry: they fall back to the last good selection. And the
  // footer is bare: the hints of a screen that is not there (a player's keys) would be keys that do nothing.
  refusedPage = true;
  drawTabs();
  const bar = document.querySelector('footer.bar');
  if (bar) bar.querySelectorAll('.hint').forEach(h => h.remove());
  if (!main) return;
  main.className = 'page page--refused';
  main.removeAttribute('style');
  main.innerHTML = `<section class="panel hero refusal" aria-label="refused"><div class="head"><span class="kicker">${esc(kicker)}</span><span class="q">${esc(title)}</span></div>` +
    `<div class="msg"><span class="plate plate--alarm">REFUSED</span><span>${esc(message)}</span></div>` +
    `<div class="actions">${links.map(([h, t], i) => `<a class="btn ${i === 0 ? 'btn--primary' : ''}" href="${esc(h)}">${esc(t)}</a>`).join('')}</div></section>`;
}

// ---- the five screens, and what they share: one top bar, and the run, genome, brain, draw and tank being looked at

const SCREENS = [['registry', 'REGISTRY'], ['recipe', 'RECIPE'], ['run', 'RUN'], ['tank', 'TANK'], ['genome', 'GENOME']];
export const PAGE = (location.pathname.match(/([a-z]+)\.html$/) || [, 'registry'])[1];
const SEL_KEY = 'nursery-room.selection';
let refusedPage = false;     // set by refuse(): the page's own address is not a selection to carry
const SEL_FIELDS = ['run', 'genome', 'brain', 'draw', 'tank', 'recipe'];

function readStored() {
  try { const s = JSON.parse(localStorage.getItem(SEL_KEY)); return s && typeof s === 'object' ? s : {}; } catch (e) { return {}; }
}

/** What this page's address names, as selection fields (each screen names its own: the run screen's id is the run, the genome screen's id a genome). */
function fromUrl() {
  const q = new URLSearchParams(location.search), n = k => (q.has(k) && /^\d+$/.test(q.get(k)) ? +q.get(k) : undefined);
  const out = {};
  if (PAGE === 'run' && q.get('id')) out.run = q.get('id');
  if (PAGE === 'genome') { if (q.get('run')) out.run = q.get('run'); if (q.get('id')) out.genome = q.get('id'); }
  if (PAGE === 'tank') { if (q.get('run')) out.run = q.get('run'); out.brain = n('brain'); out.draw = n('draw'); out.tank = n('tank'); }
  if (PAGE === 'recipe' && q.get('path')) out.recipe = q.get('path');
  for (const k of Object.keys(out)) if (out[k] === undefined) delete out[k];
  return out;
}

/** The selection: what the page's own address names, over what was last kept (the address is the stronger: a link someone sent). */
export function getSel() {
  const url = fromUrl(), stored = readStored();
  // a run's genome, brain, draw and tank belong to it: they are not carried over to another run
  if (url.run != null && stored.run != null && url.run !== stored.run) for (const k of ['genome', 'brain', 'draw', 'tank']) delete stored[k];
  return { ...stored, ...url, fromUrl: Object.keys(url) };
}

/** Keeps what a page learns of the selection (a field set to null is forgotten) and draws the tabs again. A run that changes takes its genome, brain, draw and tank with it. */
export function setSel(patch) {
  const cur = readStored();
  if (patch.run !== undefined && patch.run !== cur.run) for (const k of ['genome', 'brain', 'draw', 'tank']) delete cur[k];
  for (const [k, v] of Object.entries(patch)) if (SEL_FIELDS.includes(k)) { if (v == null) delete cur[k]; else cur[k] = v; }
  try { localStorage.setItem(SEL_KEY, JSON.stringify(cur)); } catch (e) { /* private window or blocked storage: the tabs still follow the address */ }
  drawTabs();
}

function tabHref(name, s) {
  const q = new URLSearchParams();
  const put = (k, v) => { if (v != null && v !== '') q.set(k, v); };
  if (name === 'recipe') put('path', s.recipe);
  if (name === 'run') put('id', s.run);
  if (name === 'tank') { put('run', s.run); put('brain', s.brain); put('draw', s.draw); put('tank', s.tank); }
  if (name === 'genome') { put('run', s.run); put('id', s.genome); }
  const t = q.toString();
  return name + '.html' + (t ? '?' + t : '');
}

function drawTabs() {
  const nav = document.querySelector('#strip .tabs');
  if (!nav) return;
  // On a page that refused what its address named, the tabs carry what was last kept (which a refused address never reached), not the address.
  const s = refusedPage ? readStored() : getSel();
  nav.innerHTML = SCREENS.map(([name, label], i) =>
    `<a class="tab${name === PAGE ? ' on' : ''}" id="tab-${name}" href="${esc(tabHref(name, s))}"${name === PAGE ? ' aria-current="page"' : ''}><b>${i + 1}</b>${label}</a>`).join('');
}

/** The top bar every screen shares: the name, the five tabs (each carrying the selection), the crumb the page fills, and the machine. */
function mountStrip() {
  const host = document.getElementById('strip');
  if (!host) return;
  host.className = 'strip';
  host.innerHTML = `<span class="brand">nursery room</span><nav class="tabs" aria-label="the five screens"></nav><span class="crumb" id="crumb"></span>` +
    (host.dataset.verdict != null ? '<span class="status" id="verdict" style="margin-left:1.2rem"></span>' : '') +
    `<span class="spacer"></span><div class="machine" id="machine"></div>`;
  drawTabs();
}

mountStrip();

export function onResize(fn) {
  let t;
  addEventListener('resize', () => { clearTimeout(t); t = setTimeout(fn, 120); });
}

// ---- formatting

export function fmt(v, digits = 4) {
  if (v == null || !Number.isFinite(v)) return '—';
  const a = Math.abs(v);
  let s;
  if (a >= 1000) s = Math.round(v).toLocaleString('en').replace(/,/g, ' ');
  else s = String(+v.toPrecision(digits));
  return s.replace('-', '−');
}

export function fmtSigned(v, digits = 3) {
  if (v == null || !Number.isFinite(v)) return '—';
  return (v >= 0 ? '+' : '−') + fmt(Math.abs(v), digits).replace('−', '');
}

export function ageText(seconds) {
  if (seconds == null) return '—';
  const s = Math.round(seconds);
  if (s < 60) return s + ' s';
  const m = Math.floor(s / 60);
  if (m < 60) return m + ' min ' + (s % 60) + ' s';
  const h = Math.floor(m / 60);
  return h + ' h ' + (m % 60) + ' min';
}

export function clock(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d)) return '—';
  const p = n => String(n).padStart(2, '0');
  return p(d.getMonth() + 1) + '-' + p(d.getDate()) + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
}

/** Status mark and label for a run's phase. The words are the room's own, not the nursery's. */
export const PHASES = {
  search: ['running', 'IN ITS SEARCH'], exam: ['running', 'IN ITS EXAM'], running: ['running', 'RUNNING'],
  completed: ['done', 'COMPLETED'], stopped: ['killed', 'STOPPED'], killed: ['killed', 'KILLED'], exited: ['killed', 'EXITED'],
  failed: ['failed', 'FAILED'], unrecorded: ['draft', 'NOT RECORDED'], empty: ['draft', 'NO FILES YET'], missing: ['draft', 'FOLDER MISSING'],
};

export function statusHtml(phase) {
  const [mk, label] = PHASES[phase] || ['draft', String(phase || 'unknown').toUpperCase()];
  return `<span class="status"><i class="mk mk--${mk}"></i>${esc(label)}</span>`;
}

/** The badge a trial run carries: it was started on a recipe outside rounds/nursery/, so the nursery does not enter it in the registry. */
export function trialBadge() {
  return `<span class="badge" title="started on a recipe outside rounds/nursery/: the nursery does not enter it in the registry, and it cannot be rerun">trial, unregistered</span>`;
}

/** One activity row, as a line: time, who, which operation with what arguments, why, and how it ended. */
export function actRow(e) {
  const args = Object.entries(e.args || {}).map(([k, v]) => `${k}=${k.endsWith('Sha256') ? String(v).slice(0, 8) : typeof v === 'object' ? JSON.stringify(v) : v}`).join(' ');
  const out = e.outcome === 'ok' ? '' : `<span class="plate ${e.outcome === 'failed' ? 'plate--alarm' : ''}">${esc(String(e.outcome).toUpperCase())}</span>`;
  return `<div class="act"><span class="t">${esc(clock(e.timeUtc))}</span><span class="who">${esc(e.who)}</span><span class="op">${esc(e.operation)}</span>${out}` +
    `<span class="args">${esc(args)}</span>${e.reason ? `<span class="why"><q>${esc(e.reason)}</q></span>` : ''}${e.detail ? `<span class="why">${esc(e.detail)}</span>` : ''}</div>`;
}

export function noData(text) { return `<p class="note nodata">${esc(text)}</p>`; }

/** Wires every enabled [data-op] button: handlers[opId](button) runs when it is clicked. */
export function bindOps(handlers) {
  for (const b of document.querySelectorAll('[data-op]')) {
    if (b.disabled) continue;
    const h = handlers[b.dataset.op];
    if (h) b.addEventListener('click', () => h(b));
  }
}

/** What the load's title says: % Processor Utility can pass 100 under boost, and a person is shown it capped at 100 as Task Manager does, with the counter's reading here. The cap rule reads the raw value. */
function loadTitle(percent) {
  return `% Processor Utility as the counter read it: ${fmt(percent, 4)}${percent > 100 ? ' (shown as 100, as Task Manager does; the cap reads the counter\'s value)' : ''}`;
}

/**
 * The strip's machine part: the cap, the logical processors, and the load the room read (% Processor Utility as Task Manager
 * reports it, and the logical processors in use that makes of it). It reads again every poll. `onRoom(room)`, when given, is called with
 * each reading of /api/room as it is drawn, so that a screen need not ask for it a second time.
 */
export async function mountMachine(onRoom) {
  const host = document.getElementById('machine');
  if (!host) return null;
  const draw = r => {
    if (onRoom) onRoom(r);
    const l = r.load || {};
    const load = l.percent != null
      ? `<span>load <span class="hi" title="${esc(loadTitle(l.percent))}">${esc(fmt(Math.min(l.percent, 100), 3))} %</span> · <span class="hi" title="the larger of the counter's reading (${l.read}) and the threads this room granted lately (${l.granted})">${l.inUse}</span> of <span class="hi">${r.logical}</span> in use</span>`
      : `<span class="dim" title="${esc(l.problem || '')}">load not read</span>`;
    // The coordinating session's timing-read declaration (the room only reads it): in force, or a file that counts as absent, said as it is.
    const tr = r.timingRead || {};
    const timing = tr.state === 'declared'
      ? `<span class="hi" title="${esc(tr.why || '')}">timing read: ${esc(tr.who || 'someone')} until ${esc(tr.until || '')} — no episode is stepped</span>`
      : tr.state === 'stale' || tr.state === 'unparsable' || tr.state === 'unreadable'
        ? `<span class="dim" title="${esc((tr.note || '') + (tr.problem ? ' (' + tr.problem + ')' : ''))}">timing-read file: ${esc(tr.state)}${tr.state === 'unreadable' ? ' — no episode is stepped' : ', counts as absent'}</span>`
        : '';
    host.innerHTML = `<span>machine</span><span class="meter"><u style="left:${(100 * r.capLogical / r.logical).toFixed(1)}%"></u></span>` +
      `<span>cap <span class="hi">${r.capLogical}</span> of <span class="hi">${r.logical}</span> logical</span>${load}${timing}`;
  };
  try {
    const r = await getRoom();
    draw(r);
    setInterval(() => getRoom().then(draw).catch(() => { host.textContent = 'the room did not answer'; }), Math.max(5, r.pollSeconds) * 1000);
    return r;
  } catch (e) {
    host.textContent = 'the room did not answer';
    return null;
  }
}

/** The machine's cap and load as a launch dialog says it, in HTML (the dialog's `machine`): `room` is /api/room's answer, or null when it did not answer. */
export function machineText(room, fit = false) {
  if (!room) return 'The room did not answer, so the cap and the load could not be read, which refuses a launch.';
  const l = room.load;
  const free = l && l.percent != null ? Math.max(0, room.capLogical - l.inUse) : 0;
  return `Cap ${room.capLogical} of ${room.logical} logical processors; ` +
    (l && l.percent != null
      ? `<span title="${esc(loadTitle(l.percent))}">${esc(fmt(Math.min(l.percent, 100), 3))} %</span> in use now (${l.inUse} processors)${fit ? `, so at most ${free} ${free === 1 ? 'thread fits' : 'threads fit'}` : ''}.`
      : 'the load could not be read, which refuses a launch.');
}

/** Said under the reason box of every dialog that asks for one: where what is typed goes. */
export const REASON_KEPT = "The reason is kept in the room's activity log, which is committed to git and may be published.";

/**
 * A small form that opens over the page and asks for a reason before an operation is sent: never a browser prompt.
 * `fields` are the operation's other inputs ({name, label, value, type: 'text' | 'number' | 'area', rows, min, max}); `run(reason, values)`
 * calls the operation (by a literal id, in the caller) and returns its envelope. A refusal's text is shown as it came;
 * a call that was done says so in `done(result)` (HTML the caller has escaped) and the form stays for another go or a close.
 * Asking again for the same `key` closes it. With `info`, only the note is shown, to be closed.
 */
export function ask(opts) {
  const { key, title, note = '', fields = [], send = 'SEND', run, done, after, info = false, machine } = opts;
  const prior = document.querySelector(`form.ask[data-ask="${key}"]`);
  document.querySelectorAll('form.ask').forEach(f => f.remove());
  if (prior) return;
  const form = document.createElement('form');
  form.className = 'ask';
  form.dataset.ask = key;
  form.autocomplete = 'off';
  const input = f => f.type === 'area'
    ? `<textarea name="${esc(f.name)}" rows="${f.rows || 10}" spellcheck="false">${esc(f.value ?? '')}</textarea>`
    : `<input name="${esc(f.name)}" type="${f.type === 'number' ? 'number' : 'text'}" value="${esc(f.value ?? '')}"${f.min != null ? ` min="${f.min}"` : ''}${f.max != null ? ` max="${f.max}"` : ''}>`;
  if (info) {
    form.innerHTML = `<div class="ask__title">${esc(title)}</div><p class="note">${note}</p><div class="ask__row"><button class="btn" type="button" data-cancel>CLOSE</button></div>`;
    document.body.appendChild(form);
    form.querySelector('[data-cancel]').addEventListener('click', () => form.remove());
    return;
  }
  // `machine(room)` says what the machine's load is, in HTML the caller has escaped; the form opens at once with "reading the machine…" and is filled when
  // /api/room answers (the load is a rate, and reading it takes seconds). A room that does not answer is said as that.
  const noteHtml = (note || machine) ? `<p class="note">${note}${machine ? ' <span class="ask__machine dim">reading the machine…</span>' : ''}</p>` : '';
  form.innerHTML = `<div class="ask__title">${esc(title)}</div>${noteHtml}` +
    fields.map(f => `<label class="ask__f"><span>${esc(f.label || f.name)}</span>${input(f)}</label>`).join('') +
    `<label class="ask__f"><span>reason</span><input name="reason" type="text" maxlength="500" placeholder="why, in one line" required><small class="ask__keep">${REASON_KEPT}</small></label>` +
    `<div class="ask__row"><button class="btn btn--primary" type="submit">${esc(send)}</button><button class="btn" type="button" data-cancel>CANCEL</button></div>` +
    `<div class="ask__out" role="status" aria-live="polite"></div>`;
  document.body.appendChild(form);
  if (machine) {
    const slot = form.querySelector('.ask__machine');
    const fill = r => { slot.classList.remove('dim'); slot.innerHTML = machine(r); };
    getRoom().then(fill).catch(() => fill(null));
  }
  const out = form.querySelector('.ask__out');
  const submit = form.querySelector('button[type=submit]');
  form.querySelector('[data-cancel]').addEventListener('click', () => form.remove());
  form.addEventListener('submit', async ev => {
    ev.preventDefault();
    const reason = form.elements.reason.value.trim();
    const values = {};
    for (const f of fields) values[f.name] = form.elements[f.name].value;
    submit.disabled = true;
    out.innerHTML = '<span class="dim">sending…</span>';
    let r;
    try { r = await run(reason, values); } catch (e) { r = { ok: false, error: 'the room did not answer: ' + e.message }; }
    submit.disabled = false;
    if (r && r.ok) {
      out.innerHTML = `<span class="plate">DONE</span> <span>${done ? done(r.result) : ''}</span>` + (r.warning ? `<div class="msg"><span class="plate plate--alarm">WARNING</span><span>${esc(r.warning)}</span></div>` : '');
      if (after) after(r);
    } else {
      out.innerHTML = `<span class="plate plate--alarm">REFUSED</span> <span>${esc((r && r.error) || 'no answer')}</span>`;
    }
  });
  form.elements[fields.length ? fields[0].name : 'reason'].focus();
}
