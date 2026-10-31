// The recipe screen: a recipe resolved against its base, as sections made from its own structure,
// and the check's result. A field is a row; nothing here knows which fields exist.

import { esc, getJson, getResource, callOp, bindOps, ask, noData, mountMachine, machineText, poll, refuse, setSel, showPollError } from './common.js';

const $ = id => document.getElementById(id);
const path = new URLSearchParams(location.search).get('path');
let room = null;
let cur = null;   // the recipe as last read, with the hash of its file and its diff against its base

// ---- plain-word labels: the room's one table (RecipeLabels.cs), carried by the recipe resource

let labels = {};

/**
 * A field's plain label, from the table the recipe resource carries: the field's path with array positions as [] (every exam brain shares one label), the
 * heading section's 'recipe.' taken off; a map whose keys are data (world.overrides, reward.parameters) has an entry ending '.*' with {last} for the entry's
 * name. A path the table does not know (a field this build's table lacks) is labelled with its last word.
 */
function labelOf(p) {
  const n = String(p).replace(/\[\d+\]/g, '[]').replace(/^recipe\./, '');
  if (labels[n]) return labels[n];
  for (const k of Object.keys(labels)) if (k.endsWith('.*') && n.startsWith(k.slice(0, -1)) && n.length > k.length - 1) return labels[k].replace('{last}', n.slice(k.length - 1));
  return n.split('.').pop().replace(/\[\]$/, '');
}

// ---- whether the recipe is committed (gitState, read by the room from git)

/** The one plate: the state in words, the sentence git's answer makes in its title. Committed is the plain plate; every other state is the alarm plate. */
function gitPlate(g) {
  if (!g) return '<span class="plate plate--alarm" id="git-plate" data-state="none" title="the room gave no reading of whether this file is committed">COMMITTED? NOT READ</span>';
  const text = String(g.label || g.state || '').toUpperCase();
  return `<span class="plate${g.state === 'committed' ? '' : ' plate--alarm'}" id="git-plate" data-state="${esc(g.state)}" title="${esc(g.detail || '')}">${esc(text)}</span>`;
}

/**
 * The plate for a recipe the nursery read but whose world this build cannot load (world.loaded false): its title names the config and the build's refusal.
 * Nothing for a recipe whose world loads, and nothing for one the nursery refused outright (the room gave no world reading then).
 */
function worldPlate(w) {
  if (!w || w.loaded) return '';
  return `<span class="plate plate--alarm" id="world-plate" data-state="not-loaded" title="${esc('the world ' + (w.config || '(not named)') + ' does not load on this build: ' + (w.problem || ''))}">WORLD NOT LOADED</span>`;
}

/** A value as the row shows it: scalars as they are, arrays and objects compact. */
function show(v) {
  if (v === undefined) return '—';
  if (v === null) return 'null';
  if (Array.isArray(v)) return v.length && v.every(x => typeof x !== 'object' || x === null) ? v.map(show).join(' ') : `${v.length} item${v.length === 1 ? '' : 's'}`;
  if (typeof v === 'object') return Object.entries(v).map(([k, x]) => `${k}=${show(x)}`).join(' ');
  return String(v);
}

/** Rows of one section: every leaf under it, objects opened, an array of objects one row an item. */
function rowsOf(value, prefix) {
  const out = [];
  const walk = (v, p, depth) => {
    if (v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length) { for (const [k, x] of Object.entries(v)) walk(x, p + '.' + k, depth + 1); return; }
    if (Array.isArray(v) && v.some(x => x && typeof x === 'object')) { v.forEach((x, i) => out.push({ path: `${p}[${i}]`, value: x, depth, item: i + 1, array: p })); return; }
    out.push({ path: p, value: v, depth });
  };
  walk(value, prefix, 0);
  return out;
}

// ---- matching the items of an array with the base's

const sortKeys = v => (Array.isArray(v) ? v.map(sortKeys) : v && typeof v === 'object' ? Object.fromEntries(Object.keys(v).sort().map(k => [k, sortKeys(v[k])])) : v);
const canon = v => JSON.stringify(sortKeys(v));
/** What names an item among its fellows: an exam control's kind and label, a thing's name. Null for an item that has none (it is then matched by its place). */
const identity = v => (v && typeof v === 'object' && !Array.isArray(v) && (typeof v.kind === 'string' || typeof v.label === 'string' || typeof v.name === 'string')
  ? JSON.stringify([v.kind ?? null, v.label ?? v.name ?? null]) : null);

/**
 * The items of this recipe's array against the base's. An item with the same content is the base's own; one with the same identity (kind and label) and other
 * content is that item changed; an item with no identity is matched by its place; what is left of this recipe's is new, and what is left of the base's is dropped.
 * So a recipe with fewer items than its base says which of the base's it drops, and an item is not read as a change because the base's item at its index is another.
 */
function matchItems(a, b) {
  const used = new Set(), pairs = new Array(a.length).fill(null);
  const byContent = new Map();
  b.forEach((x, j) => { const k = canon(x); if (!byContent.has(k)) byContent.set(k, []); byContent.get(k).push(j); });
  a.forEach((x, i) => { const js = byContent.get(canon(x)); if (js && js.length) { const j = js.shift(); used.add(j); pairs[i] = { base: b[j], same: true }; } });
  const byId = new Map();
  b.forEach((x, j) => { const k = used.has(j) ? null : identity(x); if (k) { if (!byId.has(k)) byId.set(k, []); byId.get(k).push(j); } });
  a.forEach((x, i) => { const k = pairs[i] ? null : identity(x); const js = k && byId.get(k); if (js && js.length) { const j = js.shift(); used.add(j); pairs[i] = { base: b[j], same: false }; } });
  a.forEach((x, i) => { if (pairs[i] || identity(x)) return; if (i < b.length && !used.has(i) && !identity(b[i])) { used.add(i); pairs[i] = { base: b[i], same: canon(x) === canon(b[i]) }; } });
  const dropped = b.filter((x, j) => !used.has(j));
  return { pairs, dropped, added: a.filter((x, i) => !pairs[i]), changed: a.filter((x, i) => pairs[i] && !pairs[i].same) };
}

/**
 * What the recipe.diff operation said differs, by path: the source of the side list, and of the marks in the form (the screen decides nothing about which fields
 * differ). Where the field is an array of objects, its items are matched with the base's (see matchItems), so that each row can say what it differs from.
 */
function diffLookup(diff) {
  const m = new Map();
  if (diff && diff.ok) for (const x of diff.differs) {
    const entry = { ...x };
    const objects = v => Array.isArray(v) && v.some(i => i && typeof i === 'object');
    if ((objects(x.a) || objects(x.b)) && (x.a == null || Array.isArray(x.a)) && (x.b == null || Array.isArray(x.b))) entry.items = matchItems(x.a || [], x.b || []);
    m.set(x.path, entry);
  }
  return m;
}

/** A path as the diff names it: a section's own path, without the heading section's 'recipe.' prefix. */
const diffPath = (section, p) => (section === 'recipe' ? p.slice('recipe.'.length) : p);

/** For a row of a section: whether it differs from the base, and the base's value. Every diff a row accounts for is noted in ctx.marked. */
function rowDiff(ctx, section, r) {
  if (!ctx.hasBase) return { same: true };
  const x = ctx.diff.get(diffPath(section, r.path));
  if (x) { ctx.marked.add(x.path); return { same: false, base: x.side === 'a' ? undefined : x.b, none: x.side === 'a' }; }
  if (r.array) {
    const y = ctx.diff.get(diffPath(section, r.array));
    if (y) {
      ctx.marked.add(y.path);
      const pair = y.items ? y.items.pairs[r.item - 1] : null;
      if (pair && pair.same) return { same: true };
      return pair ? { same: false, base: pair.base } : { same: false, none: true };
    }
  }
  return { same: true };
}

/** The base's items that a recipe's array does not keep, as one row under the array's items: how many of the base's, and which. */
function droppedRow(ctx, x) {
  const n = x.items.dropped.length, baseN = (x.b || []).length;
  return `<div class="row row--wide row--diff row--dropped"><label title="${esc(labelOf(x.path) + ' · ' + x.path)}">${esc(labelOf(x.path))} · dropped<code>${esc(x.path)}</code></label>` +
    `<span class="val">drops ${n} of the base's ${baseN} item${baseN === 1 ? '' : 's'}</span>` +
    `<span class="base">${x.items.dropped.map(d => `<span class="chipv" title="${esc(JSON.stringify(d))}">${esc(show(d))}</span>`).join('')}</span></div>`;
}

function sectionHtml(name, value, ctx) {
  const rows = rowsOf(value, name);
  let body = rows.map((r, k) => {
    const label = r.item ? `${labelOf(r.path)} · ${r.item}` : labelOf(r.path);
    const { same, base: bv, none } = rowDiff(ctx, name, r);
    const refused = ctx.field && (ctx.field === r.path || ctx.field.startsWith(r.path + '.') || ctx.field.startsWith(r.path + '['));
    const msg = refused ? `<div class="msg"><span class="plate plate--alarm">REFUSED</span>${esc(ctx.message)}</div>` : '';
    const nb = ctx.hasBase ? '' : ' nobase';
    const mine = show(r.value), theirs = same ? 'same' : none ? 'not in the base' : show(bv);
    // a long value (a description, a path) wraps under its label, and the base's value under it, rather than being cut
    const wide = mine.length > 26 || (!same && theirs.length > 18);
    let html = `<div class="row${nb}${!same ? ' row--diff' : ''}${refused ? ' row--err' : ''}${wide ? ' row--wide' : ''}"><label style="padding-left:${Math.min(r.depth, 3) * .7}rem" title="${esc(label + ' · ' + r.path)}">${esc(label)}<code>${esc(r.path)}</code></label>` +
      `<span class="val ${r.value === null ? 'null' : ''}" title="${esc(JSON.stringify(r.value))}">${esc(mine)}</span>` +
      (ctx.hasBase ? `<span class="base" title="${esc(same ? '' : JSON.stringify(bv) ?? '')}">${esc(theirs)}</span>` : '') + msg + '</div>';
    // after an array's last item: what the base has that this recipe does not
    const last = r.array && !(rows[k + 1] && rows[k + 1].array === r.array);
    if (last && ctx.hasBase) {
      const x = ctx.diff.get(diffPath(name, r.array));
      if (x && x.items && x.items.dropped.length) { ctx.marked.add(x.path); html += droppedRow(ctx, x); }
    }
    return html;
  }).join('');
  // an array of objects that this recipe has none of, where the base has some: every one of them dropped
  if (ctx.hasBase) {
    const shown = new Set(rows.filter(r => r.array).map(r => diffPath(name, r.array)));
    for (const [ap, x] of ctx.diff) {
      if (!x.items || shown.has(ap) || !x.items.dropped.length) continue;
      const own = ap === name || ap.startsWith(name + '.') || (name === 'recipe' && !ap.includes('.'));
      if (own && !ctx.marked.has(x.path)) { ctx.marked.add(x.path); body += droppedRow(ctx, x); }
    }
  }
  return `<fieldset id="s-${esc(name)}"><legend>${esc(name)}<span>${rows.length} field${rows.length === 1 ? '' : 's'}</span></legend>${body}</fieldset>`;
}

/** One line of the side list: the field, and what it was in the base and is here. An array of items says how many it drops, adds and changes, and which. */
function diffLine(x) {
  const key = `<span class="k">${esc(x.path)}</span><span class="pl">${esc(labelOf(x.path))}</span>`;
  if (x.items) {
    const it = x.items, list = arr => arr.map(v => `<span class="chipv" title="${esc(JSON.stringify(v))}">${esc(show(v))}</span>`).join('');
    const n = (x.a || []).length, bn = (x.b || []).length;
    return `<div class="diffline">${key}<span class="v">${n} item${n === 1 ? '' : 's'} here, ${bn} in the base</span>` +
      (it.dropped.length ? `<div class="sub"><b>drops ${it.dropped.length} of the base's ${bn}:</b> ${list(it.dropped)}</div>` : '') +
      (it.added.length ? `<div class="sub"><b>adds ${it.added.length}:</b> ${list(it.added)}</div>` : '') +
      (it.changed.length ? `<div class="sub"><b>changes ${it.changed.length}:</b> ${list(it.changed)}</div>` : '') + '</div>';
  }
  return `<div class="diffline">${key}<span class="v">${x.side === 'a' ? 'not in the base' : esc(show(x.b))} → <b>${x.side === 'b' ? 'not here' : esc(show(x.a))}</b></span></div>`;
}

let drawnKey = null;

/** Reads the recipe again and redraws only when what it says has changed. A recipe the room does not have is a refusal in the main area, and ends the polling. */
async function loadRecipe() {
  let d;
  try { d = await getResource('nursery://recipe/' + path.split('/').map(encodeURIComponent).join('/')); }
  catch (e) {
    if (e.status === 404 || e.status === 400) {
      refuse({
        crumb: path, kicker: 'Recipe', title: e.status === 404 ? 'No such recipe' : 'Not a recipe the room can open', message: e.message,
        links: [['recipe.html', 'CHOOSE A RECIPE'], ['registry.html', 'BACK TO THE REGISTRY']],
      });
      document.title = 'Nursery room · recipe ' + path + ' not found';
      return 'stop';
    }
    throw e;
  }
  setSel({ recipe: path });
  // What differs from the base is recipe.diff's answer and nothing the screen works out itself.
  let diff = null;
  if (d.ok && d.base) {
    const r = await callOp('recipe.diff', { path });
    diff = r.ok ? r.result : { ok: false, error: r.error };
  }
  const key = JSON.stringify([d.ok, d.hash, d.check, diff, d.canonical, d.fileSha256, d.gitState, d.world]);
  $('foot').textContent = `polls every ${room ? room.pollSeconds : 30} s · read ${new Date().toLocaleTimeString()}`;
  if (key === drawnKey) return;
  drawnKey = key;
  cur = { d, diff };
  draw(d, diff);
}

const slug = s => (String(s || 'run').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'run').slice(0, 50);

/** The free cores line of the launch section: filled when the room's reading arrives, which takes seconds (the page's own reading does not wait for it). */
function capHtml() {
  if (!room) return '<span class="dim">reading the machine…</span>';
  const m = room.machine || {};
  return m.problem ? `<span class="dim" title="${esc(m.problem)}">no slot in the queue</span>` : `<span title="${esc(m.explain || '')}">${m.freeCount} <span class="unit">of slot ${esc(m.slot)}</span></span>`;
}

function draw(d, diff) {
  labels = d.labels || {};
  const resolved = d.resolved || d.raw || {};
  const check = d.check || {};
  // what a launch of this recipe would do, as the room decides it: registered, trial, or refused with the reason
  const worldNotLoaded = !!(d.world && !d.world.loaded);
  const launchState = worldNotLoaded ? 'refused' : (d.launch && d.launch.state ? d.launch.state : (d.trial ? 'trial' : 'registered'));
  const launchReason = d.launch && d.launch.reason ? d.launch.reason : (worldNotLoaded ? 'WORLD NOT LOADED: ' + (d.world.problem || '') : '');
  $('crumb').innerHTML = `<b>${esc(path)}</b>${d.base ? ' · base <b>' + esc(d.base.path) + '</b>' : ''}`;
  $('verdict').innerHTML = d.ok ? '<i class="mk mk--done"></i>CHECKS OUT' : '<i class="mk mk--failed"></i>REFUSED';
  document.title = 'Nursery room · recipe ' + (d.name || path);

  const heads = ['format', 'name', 'description', 'seed'].filter(k => k in resolved);
  const objs = Object.keys(resolved).filter(k => !heads.includes(k) && k !== 'base');
  const ctx = { hasBase: !!(d.base && diff && diff.ok), diff: diffLookup(diff), field: d.ok ? null : check.field, message: check.message, marked: new Set() };
  const diffs = ctx.hasBase ? diff.differs : [];
  const diffCount = diffs.length;

  $('toc').innerHTML = (heads.length ? `<a data-s="recipe">recipe${diffs.filter(x => heads.includes(x.path)).length ? `<span class="count hi">${diffs.filter(x => heads.includes(x.path)).length}</span>` : ''}</a>` : '') + objs.map(k => {
    const refused = ctx.field && (ctx.field === k || ctx.field.startsWith(k + '.') || ctx.field.startsWith(k + '['));
    const changed = diffs.filter(x => x.path === k || x.path.startsWith(k + '.')).length;
    return `<a data-s="${esc(k)}">${esc(k)}${refused ? '<span class="count plate plate--alarm" style="font-size:.77rem;padding:0 .38rem;color:var(--plate-ink)">1</span>' : changed ? `<span class="count hi">${changed}</span>` : ''}</a>`;
  }).join('');
  $('toc').querySelectorAll('a').forEach(a => a.addEventListener('click', () => document.getElementById('s-' + a.dataset.s)?.scrollIntoView({ block: 'start' })));
  $('toc-foot').innerHTML = `<div class="field"><span class="field__label">differs from base</span><span class="field__value">${ctx.hasBase ? diffCount : '—'}</span></div>` +
    `<div class="field"><span class="field__label">refused</span><span class="field__value">${d.ok ? 0 : 1}</span></div>` +
    `<div class="field"><span class="field__label">format</span><span class="field__value">${esc(show(resolved.format))}</span></div>`;

  $('author').innerHTML = `<div class="line"><h1>${esc(resolved.name ?? path)}</h1>${gitPlate(d.gitState)}${worldPlate(d.world)}<span class="mono dim" style="font-size:.8rem">${resolved.seed != null ? 'seed ' + esc(resolved.seed) : ''}${d.base ? ' · base ' + esc(d.base.name || d.base.path) : ''}</span><span class="spacer"></span>` +
    (worldNotLoaded
      ? `<button class="btn btn--off" disabled title="${esc('WORLD NOT LOADED: ' + (d.world.problem || '') + ' The nursery would refuse any text that names this world, so the file is not edited here.')}">EDIT BY HAND</button></div>`
      : `<button class="btn" data-op="recipe.write" title="edit the file's text and save it, checked by the nursery first">EDIT BY HAND</button></div>`) +
    `<div class="desc">${esc(resolved.description ?? '')}</div>`;

  // two columns, filled in file order by field count so neither runs far longer; the heading section holds the description too, so that it is a row like any other
  const a = [], b = [];
  let na = 0, nb = 0;
  const heading = heads.length ? [['recipe', Object.fromEntries(heads.map(h => [h, resolved[h]]))]] : [];
  for (const [k, v] of [...heading, ...objs.map(k => [k, resolved[k]])]) {
    const n = rowsOf(v, k).length + 1;
    if (na <= nb) { a.push([k, v]); na += n; } else { b.push([k, v]); nb += n; }
  }
  const colhead = ctx.hasBase ? '<div class="colhead"><span>FIELD</span><span>THIS RECIPE</span><span>BASE, RESOLVED</span></div>' : '<div class="colhead nobase"><span>FIELD</span><span>THIS RECIPE</span></div>';
  const colA = a.map(([k, v]) => sectionHtml(k, v, ctx)).join(''), colB = b.map(([k, v]) => sectionHtml(k, v, ctx)).join('');
  // every difference the side list counts is marked in the form: one the rows could not carry (a field only the base has) is listed here, with its base value
  const stray = diffs.filter(x => !ctx.marked.has(x.path));
  const strayHtml = stray.length ? `<fieldset id="s-base-only"><legend>only in the base<span>${stray.length} field${stray.length === 1 ? '' : 's'}</span></legend>${stray.map(x =>
    `<div class="row row--wide row--diff"><label title="${esc(labelOf(x.path) + ' · ' + x.path)}">${esc(labelOf(x.path))}<code>${esc(x.path)}</code></label><span class="val">not here</span><span class="base" title="${esc(JSON.stringify(x.b) ?? '')}">${esc(show(x.b))}</span></div>`).join('')}</fieldset>` : '';
  $('col-a').innerHTML = colhead + colA;
  $('col-b').innerHTML = colhead + colB + strayHtml;

  // Check, then launch (so that its buttons are in view), then what differs (a list that may be long), then the canonical text.
  $('side').innerHTML = `<div class="panel__head"><span class="title">Check</span><span class="aside">${d.ok ? 'ok' : 'refused'}</span></div>
    <div id="check-out">${d.ok ? '' : `<div class="msg" style="margin:0 0 .6rem"><span class="plate plate--alarm">REFUSED</span><span>${check.field ? `<b class="mono">${esc(check.field)}</b> ` : ''}${esc(check.message)}</span></div>`}</div>
    <div class="field" title="${esc(d.hash || '')}"><span class="field__label">resolved hash</span><span class="field__value clip" style="font-size:.7rem">${esc(d.hash || '—')}</span></div>
    <div class="field" title="${esc(path)}"><span class="field__label">file</span><span class="field__value clip" style="font-size:.85rem">${esc(path)}</span></div>
    <div style="margin:.6rem 0"><button class="btn btn--primary" data-op="recipe.check" id="btn-check">CHECK AGAIN</button></div>
    <div class="section" id="launch">
      <div class="title" style="margin-bottom:.4rem">Launch</div>
      <div class="field"><span class="field__label">free cores</span><span class="field__value" id="launch-cap">${capHtml()}</span></div>
      <div style="display:flex;margin-top:.7rem">
        ${launchState === 'refused'
          ? `<button class="btn btn--off" disabled style="margin-right:.5rem" title="${esc(launchReason)}">LAUNCH</button>
        <button class="btn btn--off" disabled title="${esc(launchReason)}">LAUNCH AS TRIAL</button>`
          : launchState === 'trial'
            ? `<button class="btn btn--off" disabled style="margin-right:.5rem" title="this recipe is outside rounds/nursery/, so its run is a trial and is not registered">LAUNCH</button>
        <button class="btn btn--primary" data-op="run.launch" data-trial="1"${d.ok ? ' title="starts the run unregistered: the nursery does not enter it in the registry"' : ' title="the nursery refuses this recipe, so a launch will be refused"'}>LAUNCH AS TRIAL</button>`
            : `<button class="btn btn--primary" style="margin-right:.5rem" data-op="run.launch"${d.ok ? '' : ' title="the nursery refuses this recipe, so a launch will be refused"'}>LAUNCH</button>
        <button class="btn btn--off" disabled title="this recipe is directly under rounds/nursery/, so its run is registered; a recipe in a trials folder is launched as a trial">LAUNCH AS TRIAL</button>`}
      </div>
      ${launchState === 'refused' ? `<p class="dim" style="font-size:.8rem;line-height:1.5"><span class="plate plate--alarm">NOT LAUNCHED</span> ${esc(launchReason)}</p>` : ''}
      <p class="dim" style="font-size:.8rem;line-height:1.5">A launch starts the nursery on this recipe as a process of its own, in a new folder. The threads, with those the room has already granted, are counted against the free cores of the room's slot in the machine's queue, and a launch past them, or on a machine the heat check reads HOT, is refused with the numbers. It asks why, and the answer goes in the activity feed.${d.trial ? ' A trial runs unregistered: its run is not entered in the registry and cannot be rerun.' : ''}</p>
    </div>
    ${d.base ? `<div class="section"><div class="title" style="margin-bottom:.4rem">Differs from ${esc(d.base.name || d.base.path)} <span class="aside">${ctx.hasBase ? diffCount : '—'}</span></div>${
      !ctx.hasBase ? noData('The diff could not be made: ' + ((diff && diff.error) || 'no answer') + '.')
        : diffCount ? diffs.map(x => diffLine(ctx.diff.get(x.path) || x)).join('') : noData('No field differs from the base.')}
      <p class="dim" style="font-size:.8rem;line-height:1.5">The recipe.diff operation's answer: both recipes resolved, each with its base merged under it, then compared field by field. Each is marked in the form, with the base's value.</p></div>` : ''}
    ${d.canonical ? `<div class="section"><div class="title">Canonical text</div><pre class="canon">${esc(d.canonical)}</pre></div>` : ''}`;

  bindOps({
    'recipe.write': btn => {
      const basedOn = d.fileSha256;   // the hash of the text the editor starts from, so that a changed file is refused
      ask({
        key: 'write', title: 'Edit ' + path, send: 'CHECK AND SAVE',
        note: 'The text is checked by the nursery first, and nothing is written if it refuses. To keep this file as it is, change the path to save a copy; a copy of a name that exists is refused.',
        fields: [{ name: 'path', label: 'path (a .json file directly under rounds/nursery/)', value: path }, { name: 'text', label: 'the recipe, as JSON', type: 'area', rows: 16, value: d.text ?? '' }],
        run: (reason, v) => callOp('recipe.write', { path: v.path.trim(), text: v.text, ...(v.path.trim() === path ? { expectHash: basedOn } : {}), reason }),
        done: r => r.path === path ? `saved ${esc(r.path)}, hash <span class="mono">${esc(String(r.hash).slice(0, 16))}</span>.` : `saved <a href="recipe.html?path=${encodeURIComponent(r.path)}">${esc(r.path)}</a>.`,
        after: r => { if (r.result.path === path) { drawnKey = null; loadRecipe().catch(() => {}); } },
      });
    },
    'run.launch': btn => {
      // The form opens at once; the room's slot and the heat (which take seconds to read) are filled in when the room answers, and so is whether the file is committed
      // (read again now, not as it was at the last poll).
      ask({
        key: 'launch', title: (d.trial ? 'Launch as trial ' : 'Launch ') + (d.name || path), send: d.trial ? 'LAUNCH AS TRIAL' : 'LAUNCH',
        note: `Starts the nursery on this recipe in a new folder${d.trial ? ', unregistered (a trial)' : ''}. <span class="ask__git dim">reading git…</span>`,
        machine: rm => machineText(rm, true),
        fields: [{ name: 'name', label: 'folder name (a-z, 0-9, -)', value: slug(d.name) }, { name: 'threads', label: 'threads', type: 'number', value: 4, min: 1 }],
        run: (reason, v) => callOp('run.launch', { recipe: path, name: v.name.trim(), threads: Number(v.threads), reason }),
        done: r => `started <b>${esc(r.id)}</b>${r.trial ? ' as a trial, unregistered' : ''} (pid ${esc(r.pid)}). <a href="run.html?id=${encodeURIComponent(r.id)}">open the run</a>`,
      });
      fillLaunchGit(d.gitState, !!d.trial);
    },
    'recipe.check': async btn => {
      btn.disabled = true;
      const r = await callOp('recipe.check', { path });
      btn.disabled = false;
      const out = $('check-out');
      if (r.ok && r.result.ok) out.innerHTML = `<p class="note">Checked now: ok, hash <span class="mono hi">${esc(r.result.hash.slice(0, 16))}</span>.</p>`;
      else if (r.ok) out.innerHTML = `<div class="msg" style="margin:0 0 .6rem"><span class="plate plate--alarm">REFUSED</span><span>${r.result.field ? `<b class="mono">${esc(r.result.field)}</b> ` : ''}${esc(r.result.message)}</span></div>`;
      else out.innerHTML = `<p class="note">${esc(r.error)}</p>`;
    },
  });
}

/**
 * The launch dialog says whether the file is committed. A pre-registered run launches only from a committed recipe; the launch itself goes ahead whatever
 * the state (a trial is allowed from an uncommitted recipe). The dialog is open already: the state is read again, and if that fails the last reading is said as such.
 */
function fillLaunchGit(last, trial) {
  const slot = document.querySelector('form.ask[data-ask="launch"] .ask__git');
  if (!slot) return;
  // What the launch is, said for this launch: a registered run from a recipe that is not committed is entered in the registry with its tree recorded as changed,
  // so it is not a pre-registration; a trial is never one, and the state is only information.
  // Only changed and untracked are known to be uncommitted: for outside, unreadable and no reading at all the room does not know, and says so. The record's state is the nursery's own reading of the tree at start, never the room's.
  const what = g => (g && g.state === 'committed' ? ''
    : trial ? ' A trial is never a pre-registration, so it launches whatever the state.'
    : g && (g.state === 'changed' || g.state === 'untracked')
      ? ' This launch goes ahead and is entered in the registry, but its record will show the recipe was not committed, so it does not count as a pre-registered run. Commit the recipe first for that.'
      : ' This launch goes ahead and is entered in the registry. The room could not read whether the recipe is committed: the nursery reads the tree itself as it starts, and the run counts as a pre-registered run only if the tree is clean then.');
  const say = (g, stale) => {
    if (!slot.isConnected) return;
    slot.classList.remove('dim');
    slot.innerHTML = (g
      ? `<br>${gitPlate(g).replace(' id="git-plate"', '')} ${esc(g.detail || '')}${stale ? ' (as last read; reading it again failed)' : ''}`
      : '<br>Whether the file is committed could not be read.') + what(g);
  };
  getResource('nursery://recipe/' + path.split('/').map(encodeURIComponent).join('/')).then(x => say(x.gitState, false)).catch(() => say(last, true));
}

async function pick() {
  const r = await getJson('/api/resources');
  const recipes = r.resources.filter(x => x.uri.startsWith('nursery://recipe/'));
  $('page').style.display = 'block';
  $('page').innerHTML = `<section class="panel pick" style="max-width:48rem;margin:1rem auto"><div class="head"><span class="kicker">Recipes</span><span class="q">Which recipe?</span></div>` +
    (recipes.length ? recipes.map(x => `<a href="recipe.html?path=${encodeURIComponent(x.uri.slice('nursery://recipe/'.length))}" title="${esc(x.description || '')}">${esc(x.uri.slice('nursery://recipe/'.length))}</a>`).join('') : noData('No recipe file under rounds/nursery.')) + '</section>';
  $('crumb').textContent = 'choose a recipe';
}

// The room's reading of the machine takes seconds (the heat check runs a script): the page's own reading does not wait for it.
mountMachine().then(r => { room = r; const cap = document.getElementById('launch-cap'); if (cap) cap.innerHTML = capHtml(); });
if (path) poll(loadRecipe, 30);
else {
  try { await pick(); } catch (e) { console.error(e); showPollError(e); }
}
