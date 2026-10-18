// The registry screen: the entries and the runs room.json adds, newest first, from nursery://registry.

import { esc, getResource, callOp, bindOps, ask, poll, onResize, fmt, clock, statusHtml, trialBadge, noData, mountMachine, machineText, getSel, setSel, actRow } from './common.js';
import { el } from './chart.js';

const $ = id => document.getElementById(id);
let data = null, drawn = null, selected = null, filter = 'all', query = '';
let pollSeconds = 30;
const keyOf = i => i.id + '|' + i.kind;
const recipeHref = i => 'recipe.html?path=' + encodeURIComponent(i.recipePath);
/** A recipe link where the recipe is known and its file is in this tree; null otherwise. */
const recipeLink = (i, cls, text) => (i.recipePath && i.recipeExists ? `<a class="${cls}" href="${esc(recipeHref(i))}" title="open the recipe ${esc(i.recipePath)}">${text}</a>` : null);

const PHASE_LABEL = p => (p || 'unknown').toLowerCase();

function spark(svg, values, live) {
  svg.innerHTML = '';
  const w = svg.clientWidth, h = svg.clientHeight;
  const v = values.filter(x => x != null);
  if (v.length < 2 || !w) return;
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  const lo = Math.min(...v), hi = Math.max(...v), span = hi - lo || 1;
  const X = i => i * (w - 4) / Math.max(values.length - 1, 1), Y = x => h - 2 - (x - lo) / span * (h - 4);
  const p = []; values.forEach((x, i) => { if (x != null) p.push(X(i) + ',' + Y(x)); });
  el(svg, 'polyline', { points: p.join(' '), style: `fill:none;stroke:${live ? 'var(--run)' : 'var(--run-mid)'};stroke-width:1.4` });
  if (live) { const i = values.length - 1; if (values[i] != null) el(svg, 'circle', { class: 'live-dot', cx: X(i), cy: Y(values[i]), r: 2.6 }); }
}

/** The filter box's text, over the name, the base and the reward (case does not matter). */
function textMatches(item) {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  return [item.name, item.basePath, item.reward].some(v => v != null && String(v).toLowerCase().includes(q));
}

function matches(item) {
  if (!textMatches(item)) return false;
  if (filter === 'all') return true;
  if (filter === 'reruns') return item.rerunOf != null;
  if (filter === 'entries') return item.kind === 'entry';
  if (filter === 'runs') return item.kind === 'run';
  return PHASE_LABEL(item.phase) === filter;
}

function drawFilters(d) {
  const counts = {};
  for (const i of d.items) counts[PHASE_LABEL(i.phase)] = (counts[PHASE_LABEL(i.phase)] || 0) + 1;
  const chips = [['all', d.items.length]];
  if (d.entries) chips.push(['entries', d.entries]);
  if (d.runs) chips.push(['runs', d.runs]);
  const re = d.items.filter(i => i.rerunOf != null).length;
  if (re) chips.push(['reruns', re]);
  for (const [k, n] of Object.entries(counts)) chips.push([k, n]);
  $('filters').innerHTML = chips.map(([k, n]) => `<button class="chip ${k === filter ? 'on' : ''}" data-f="${esc(k)}">${esc(k)} ${n}</button>`).join('');
  $('filters').querySelectorAll('button').forEach(b => b.addEventListener('click', () => { filter = b.dataset.f; draw(); }));
  $('crumb').innerHTML = `<span title="${esc(d.file)}">${esc(d.fileShort || d.file)}</span> · <b>${d.entries}</b> entries · <b>${d.runs}</b> room.json runs`;
}

function row(i) {
  const p = i.progress || {};
  const gens = p.lastGen == null ? '—' : `${p.lastGen}${p.planned != null ? ' / ' + p.planned : ''}`;
  const share = p.planned ? Math.min(100, 100 * (p.lastGen || 0) / p.planned) : null;
  const live = i.phase === 'search' || i.phase === 'exam' || i.phase === 'running';
  // The rerun column never runs wider than its share: a long reason is cut with an ellipsis, and the whole of it is in the title.
  const cut = (text, why) => `<span class="cut" title="${esc(why || text)}">${esc(text)}</span>`;
  const rerun = i.rerunOf != null
    ? `<span class="badge ${i.identical ? 'badge--hi' : ''}">${i.identical == null ? 'rerun of #' + i.rerunOf : i.identical ? 'identical' : 'differs'}</span> <span class="dim" style="font-size:.8rem">#${i.rerunOf}</span>`
    : i.rerunnable === false ? `<span class="dim">${cut(i.notRerunnableBecause || 'cannot rerun')}</span>`
    : i.kind === 'entry' ? '<span class="badge">not rerun</span>' : '<span class="dim">—</span>';
  const key = keyOf(i);
  const link = recipeLink(i, 'hash glink', i.recipeHash ? esc(i.recipeHash.slice(0, 8)) : 'recipe');
  const recipeCell = (link || (i.recipeHash ? `<span class="hash" title="${i.recipePath ? esc(i.recipePath + ' is not in this tree') : ''}">${esc(i.recipeHash.slice(0, 8))}</span>` : '<span class="dim">—</span>'))
    + (i.gitCommit ? `<span class="ds">${esc(i.gitCommit.slice(0, 7))}</span>` : '');
  // One folder is one row: an entry that room.json also lists carries the folder beside its number.
  const showFolder = !!i.folderName || (i.kind === 'run' && !i.description);
  const folder = showFolder ? `<span class="ds" title="${esc(i.folder)}">${esc(i.folderShort || i.folder)}</span>` : '';
  const desc = i.description ? `<span class="ds" title="${esc(i.description)}">${esc(i.description)}</span>` : '';
  // The recipe's base, linked to the base recipe where this tree holds it.
  const baseName = i.basePath ? i.basePath.slice(i.basePath.lastIndexOf('/') + 1).replace(/\.json$/i, '') : null;
  const baseCell = !i.basePath ? '<span class="dim">—</span>'
    : i.baseExists ? `<a class="base glink" href="recipe.html?path=${encodeURIComponent(i.basePath)}" title="open the base recipe ${esc(i.basePath)}">${esc(baseName)}</a>`
    : `<span class="hash" title="${esc(i.basePath + ' is not in this tree')}">${esc(baseName)}</span>`;
  return `<tr class="${i.rerunOf != null ? 'rerun' : ''} ${selected === key ? 'sel' : ''}" data-key="${esc(key)}" tabindex="0" aria-selected="${selected === key}">
    <td class="n">${i.number ?? '—'}</td><td class="l">${statusHtml(i.phase)}</td>
    <td class="l exp"><span class="nm" title="${esc(i.name)}">${esc(i.name)}</span>${i.trial ? ' ' + trialBadge() : ''}${desc}${folder}</td>
    <td class="l" title="${esc(i.basePath || 'no base: this recipe is not a variation of another')}">${baseCell}</td>
    <td class="l">${esc(i.reward || '—')}</td>
    <td class="l ${i.startedUtc ? '' : 'dim'}">${clock(i.startedUtc)}</td>
    <td>${share == null ? esc(gens) : `<span class="gens ${live ? 'live' : ''}">${esc(gens)}<i><b style="width:${share.toFixed(0)}%"></b></i></span>`}</td>
    <td class="l" title="${esc(i.measureLabel || '')}">${i.series ? `<svg class="spark" data-id="${esc(key)}" data-live="${live ? 1 : ''}"></svg><span class="ds">${esc(i.measureLabel)}</span>` : '<span class="dim">—</span>'}</td>
    <td class="hi">${i.best == null ? '<span class="dim">—</span>' : fmt(i.best, 4)}</td>
    <td class="l">${rerun}</td>
    <td class="l">${recipeCell}</td></tr>`;
}

function drawList(d) {
  const items = d.items.filter(matches);
  if (!d.items.length) { $('list').innerHTML = noData('No entry in the registry and no run in room.json.') ; return; }
  if (!items.length) {
    $('list').innerHTML = noData(`No row matches ${query.trim() ? '“' + query.trim() + '”' : 'this filter'}${filter !== 'all' ? ' under “' + filter + '”' : ''}. Escape clears the box.`);
    return;
  }
  $('list').innerHTML = `<table class="t reg"><colgroup><col class="c-n"><col class="c-state"><col class="c-exp"><col class="c-base"><col class="c-reward"><col class="c-start"><col class="c-gens"><col class="c-spark"><col class="c-best"><col class="c-rerun"><col class="c-recipe"></colgroup><thead><tr><th class="l">#</th><th class="l">state</th><th class="l">experiment</th><th class="l" title="the recipe this one is a variation of">base</th><th class="l">reward</th><th class="l">started</th>
    <th>generations</th><th class="l">per generation</th><th>best</th><th class="l">rerun</th><th class="l">recipe</th></tr></thead><tbody>${items.map(row).join('')}</tbody></table>
    <p class="note" style="margin-top:.8rem">Newest first. Best is the top genome's score at the last generation row (the reward's value, as the nursery ranks by). The line is the reward's mean per generation (tank-scores.jsonl) where the run has one, else the top genome's score, and says which under it. A rerun compares digests: identical, or the first file and line that differ.${d.problem ? ' <b class="hi">' + esc(d.problem) + '</b>' : ''}</p>`;
  $('list').querySelectorAll('tr[data-key]').forEach(tr => tr.addEventListener('click', () => select(tr.dataset.key)));
  $('list').querySelectorAll('a.glink').forEach(a => a.addEventListener('click', ev => ev.stopPropagation()));
  $('list').querySelectorAll('svg.spark').forEach(svg => { const i = d.items.find(x => keyOf(x) === svg.dataset.id); spark(svg, i.series, !!svg.dataset.live); });
}

/** Picks a row: the panel beside shows it, and the tabs and the other screens follow it. With `focus`, the row keeps the keyboard (a redraw would drop it). */
function select(key, focus = false) {
  selected = key;
  const i = data && data.items.find(x => keyOf(x) === selected);
  if (i) setSel({ run: i.id, recipe: i.recipePath && i.recipeExists ? i.recipePath : null });
  draw();
  if (focus) focusSelected();
}

function focusSelected() {
  const tr = [...$('list').querySelectorAll('tr[data-key]')].find(x => x.dataset.key === selected);
  if (tr) { tr.focus({ preventScroll: true }); tr.scrollIntoView({ block: 'nearest' }); }
}

function drawSel(d) {
  // the row the keyboard is on is the one shown, unless the filter has taken it off the list
  const i = d.items.find(x => keyOf(x) === selected) || d.items.find(matches) || d.items[0];
  const sel = $('sel');
  if (!i) { sel.innerHTML = '<div class="head"><span class="kicker">Selected</span></div>' + noData('Nothing to select.'); return; }
  selected = keyOf(i);
  // The RECIPE tab follows the row on show, also when the page opened on it (select() sets the selection only when a row is chosen): a recipe kept from an earlier visit is not the one.
  setSel({ recipe: i.recipePath && i.recipeExists ? i.recipePath : null });
  const p = i.progress || {};
  // A value too long for the line beside its label (a hash, a verdict) goes under it, left-aligned, whole.
  const f = (k, v, title) => `<div class="field${String(v).replace(/<[^>]*>/g, '').length > 34 ? ' field--long' : ''}"${title ? ` title="${esc(title)}"` : ''}><span class="field__label">${esc(k)}</span><span class="field__value">${v}</span></div>`;
  const runHref = 'run.html?id=' + encodeURIComponent(i.id);
  sel.innerHTML = `<div class="head"><span class="kicker">Selected</span><span class="aside">${statusHtml(i.phase)}</span></div>
    <h2><small>${i.number != null ? '#' + i.number : 'run'}</small>${esc(i.name)}</h2>
    ${i.description ? `<p class="desc">${esc(i.description)}</p>` : ''}
    <div class="section">
      ${f('generations · pace', `${p.lastGen == null ? '—' : p.lastGen}${p.planned != null ? ' of ' + p.planned : ''} · ${p.paceMinutes == null ? '—' : fmt(p.paceMinutes, 3) + ' min / gen'}`)}
      ${f('reward', esc(i.reward || '—'))}
      ${f('best', i.best == null ? '—' : fmt(i.best, 5))}
      ${f('started · ended', `${clock(i.startedUtc)} · ${clock(i.endedUtc)}`)}
    </div>
    <div class="section">
      <div class="kicker" style="margin-bottom:.3rem">identity</div>
      ${f('folder', esc(i.folderShort || i.folder || '—'), i.folder || '')}
      ${i.recipePath ? f('recipe', esc(i.recipePath), i.recipePath) : ''}
      ${f('recipe hash', esc(i.recipeHash || '—'))}
      ${f('commit', esc(i.gitCommit || '—'))}
      ${i.kind === 'run' ? f('source', i.trial ? 'started by this room, as a trial ' + trialBadge() : i.launched ? 'started by this room' : 'room.json, predates the registry') : ''}
      ${i.reference ? f('reference entry', esc(i.reference.verdict || '')) : ''}
    </div>
    <div class="actions">
      ${i.exists ? `<a class="btn btn--primary" href="${runHref}">OPEN RUN</a>` : '<span class="btn btn--off" role="button" aria-disabled="true">FOLDER MISSING</span>'}
      ${recipeLink(i, 'btn', 'OPEN RECIPE') || `<span class="btn btn--off" role="button" aria-disabled="true" title="${esc(i.recipePath ? i.recipePath + ' is not in this tree, so there is no recipe to open' : 'no recipe is recorded for this run: it has no registry entry, and this room did not launch it')}">OPEN RECIPE</span>`}
      ${stoppable(i) ? '<button class="btn" data-op="run.stop">STOP</button>' : `<span class="btn btn--off" role="button" aria-disabled="true" title="${esc(i.exists ? 'this run is not running' : 'its folder is not on this machine')}">STOP</span>`}
      ${i.kind === 'entry' && i.rerunnable !== false && i.reference == null ? '<button class="btn" data-op="run.rerun">RERUN</button>' : `<span class="btn btn--off" role="button" aria-disabled="true" title="${esc(i.kind !== 'entry' ? 'only a registry entry can be rerun' : i.reference ? 'a reference entry is a check, not a rerun' : (i.notRerunnableBecause || 'this entry cannot be rerun'))}">RERUN</span>`}
    </div>
    <p class="note" style="margin:0">Stop writes a STOP file, which the run reads between generations; it never kills a process. A rerun runs on this build and is compared with the entry. Each asks why, and the answer goes in the activity feed.</p>`;
  bindOps({
    'run.stop': btn => ask({
      key: 'stop', title: 'Stop ' + i.name, send: 'WRITE STOP FILE',
      note: 'Writes STOP into the run\'s folder. The run ends between generations (or between the exam\'s brains) with status stopped, and is not entered in the registry unless it is already swimming its last exam brain. Nothing is killed.',
      run: reason => callOp('run.stop', { id: i.id, reason }),
      done: r => esc(r.note),
    }),
    'run.rerun': btn => {
      // The form opens at once; the machine's load (which takes seconds to read) is filled in when the room answers.
      ask({
        key: 'rerun', title: 'Rerun entry #' + i.number, send: 'RERUN',
        note: "Runs the entry's recipe again on this build into a new folder, and compares the two.",
        machine: room => machineText(room, true),
        fields: [{ name: 'name', label: 'new folder name (a-z, 0-9, -)', value: slug(i.name) + '-rerun' }, { name: 'threads', label: 'threads', type: 'number', value: 4, min: 1 }],
        run: (reason, v) => callOp('run.rerun', { entry: String(i.number), name: v.name.trim(), threads: Number(v.threads), reason }),
        done: r => `started <b>${esc(r.id)}</b> (pid ${esc(r.pid)}). <a href="run.html?id=${encodeURIComponent(r.id)}">open the run</a>`,
        after: () => { drawn = null; tick(); },
      });
    },
  });
}

// A run is stoppable while its record says it runs. A folder with no record ('unrecorded', 'empty') is not offered STOP: the room writes one there only for a run it launched itself.
const LIVE = ['search', 'exam', 'running'];
const stoppable = i => i.exists && LIVE.includes(i.phase);
const slug = s => (String(s || 'run').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'run').slice(0, 50);

let feedKey = null;

async function drawFeed() {
  const a = await getResource('nursery://activity');
  const key = a.total + '|' + a.unreadable;
  if (key === feedKey) return;
  feedKey = key;
  $('feed').innerHTML = a.entries.length
    ? a.entries.map(actRow).join('') + (a.total > a.shown ? `<p class="note">The latest ${a.shown} of ${a.total}; the whole log is ${esc(a.file)}.</p>` : '')
    : noData('No operation has changed anything yet. Each one that does will show here with who asked and why.');
}

function draw() {
  if (!data) return;
  // a redraw replaces the rows: where the keyboard was on a row, it goes back to it
  const onRow = document.activeElement && document.activeElement.closest && document.activeElement.closest('#list tr[data-key]');
  drawFilters(data); drawList(data); drawSel(data);
  if (onRow) focusSelected();
}

async function tick() {
  const d = await getResource('nursery://registry');
  $('reg-file').textContent = d.fileExists ? '' : 'no registry file yet';
  $('foot').textContent = `polls every ${pollSeconds} s · read ${new Date().toLocaleTimeString()}`;
  if (drawn !== d.version + '|' + d.items.length) {
    drawn = d.version + '|' + d.items.length;
    data = d;
    if (selected == null) {
      // the run last looked at, on any screen, is the one selected when the page opens
      const kept = getSel().run;
      const hit = kept != null && d.items.find(x => x.id === kept || x.folderName === kept);
      if (hit) selected = keyOf(hit);
    }
    draw();
  }
  await drawFeed();
}

// ---- the keyboard, as the footer says: ↑ ↓ move the selection, Enter opens the run, R the recipe, / the filter, Escape clears the filter

const visible = () => (data ? data.items.filter(matches) : []);
const current = () => data && data.items.find(x => keyOf(x) === selected);

function setQuery(v) {
  query = v;
  $('q').value = v;
  $('q-clear').hidden = !v;
  draw();
}

$('q').addEventListener('input', () => setQuery($('q').value));
$('q-clear').addEventListener('click', () => { setQuery(''); $('q').focus(); });
$('q').addEventListener('keydown', ev => {
  if (ev.key === 'Escape') { ev.preventDefault(); if ($('q').value) setQuery(''); else $('q').blur(); }
  else if (ev.key === 'ArrowDown' || ev.key === 'Enter') {
    // from the box into the list: the first row that matches, or the one already picked if it still does
    ev.preventDefault();
    const v = visible();
    if (!v.length) return;
    select(v.some(x => keyOf(x) === selected) ? selected : keyOf(v[0]), true);
  }
});

document.addEventListener('keydown', ev => {
  if (ev.ctrlKey || ev.metaKey || ev.altKey) return;
  const t = ev.target;
  if (t === $('q')) return;
  if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || (t.closest && t.closest('form.ask')))) return;
  if (ev.key === '/') { ev.preventDefault(); $('q').focus(); $('q').select(); return; }
  if (ev.key === 'Escape') { if (query) { ev.preventDefault(); setQuery(''); } return; }
  const v = visible();
  if (ev.key === 'ArrowDown' || ev.key === 'ArrowUp') {
    if (!v.length) return;
    ev.preventDefault();
    const at = v.findIndex(x => keyOf(x) === selected);
    const next = at < 0 ? 0 : Math.min(v.length - 1, Math.max(0, at + (ev.key === 'ArrowDown' ? 1 : -1)));
    select(keyOf(v[next]), true);
    return;
  }
  const i = current();
  if (!i) return;
  if (ev.key === 'Enter') {
    // a focused link or button does its own thing; on a row (or nowhere), Enter opens the run
    if (t && (t.tagName === 'A' || t.tagName === 'BUTTON')) return;
    if (i.exists) { ev.preventDefault(); location.href = 'run.html?id=' + encodeURIComponent(i.id); }
  } else if (ev.key === 'r' || ev.key === 'R') {
    if (i.recipePath && i.recipeExists) { ev.preventDefault(); location.href = recipeHref(i); }
  }
});

onResize(draw);
// The machine's load takes seconds to read: the registry's own reading does not wait for it.
mountMachine().then(room => { if (room) pollSeconds = room.pollSeconds; });
poll(tick, pollSeconds);
