namespace Evosim.NurseryRoom.Tests.E2e
{
    /// <summary>
    /// Scripts the flows run in the page to measure it. Layout is checked by measuring, never against a screenshot: a chart's drawn size against its box's, a panel's scroll height
    /// against its client height, and boxes that overlap. Each returns plain JSON.
    /// </summary>
    internal static class Probes
    {
        /// <summary>
        /// The page-wide audit: the width of the page against the window's, any text shown that must never be (NaN, [object Object], undefined), text running out of its box (a leaf
        /// whose content is wider than it, where nothing cuts it with an ellipsis or scrolls it) and text boxes that overlap each other.
        /// </summary>
        public const string Audit = @"(() => {
  const out = { innerW: innerWidth, scrollW: document.documentElement.scrollWidth, bodyW: document.body.scrollWidth, shown: [], runsOut: [], overlaps: [], title: document.title };
  const text = document.body.innerText;
  for (const m of text.matchAll(/\bNaN\b|\[object Object\]|\bundefined\b/g)) out.shown.push(text.slice(Math.max(0, m.index - 30), m.index + 40).replace(/\s+/g, ' '));
  const vis = e => { const r = e.getBoundingClientRect(), cs = getComputedStyle(e); return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none'; };
  const name = e => (e.id ? '#' + e.id : (typeof e.className === 'string' && e.className ? '.' + e.className.split(' ')[0] : e.tagName.toLowerCase())) + ' ' + JSON.stringify((e.textContent || '').trim().slice(0, 40));
  const leaves = [...document.querySelectorAll('body *')].filter(e => !e.closest('svg') && !e.closest('[hidden]') && e.children.length === 0 && (e.textContent || '').trim() && vis(e));
  for (const e of leaves) {
    const cs = getComputedStyle(e);
    if (e.clientWidth <= 0 || e.scrollWidth <= e.clientWidth + 1) continue;
    if (cs.overflowX === 'visible') out.runsOut.push(name(e) + ' is ' + e.scrollWidth + ' wide in a box of ' + e.clientWidth);
    else if ((cs.overflowX === 'hidden' || cs.overflowX === 'clip') && cs.textOverflow !== 'ellipsis') out.runsOut.push(name(e) + ' is cut at ' + e.clientWidth + ' of ' + e.scrollWidth);
  }
  // text boxes that overlap: the part of each leaf that can be seen (cut by every ancestor that clips it) against the same of every other
  const seen = e => { let r = e.getBoundingClientRect(); let x0 = r.left, y0 = r.top, x1 = r.right, y1 = r.bottom; for (let a = e.parentElement; a && a !== document.body; a = a.parentElement) { const o = getComputedStyle(a); if (o.overflowX !== 'visible' || o.overflowY !== 'visible') { const q = a.getBoundingClientRect(); x0 = Math.max(x0, q.left); y0 = Math.max(y0, q.top); x1 = Math.min(x1, q.right); y1 = Math.min(y1, q.bottom); } } x0 = Math.max(x0, 0); y0 = Math.max(y0, 0); x1 = Math.min(x1, innerWidth); y1 = Math.min(y1, innerHeight); return x1 > x0 && y1 > y0 ? { x0, y0, x1, y1 } : null; };
  const boxes = leaves.filter(e => !e.closest('form.ask')).map(e => ({ e, r: seen(e) })).filter(b => b.r);
  for (let i = 0; i < boxes.length && out.overlaps.length < 12; i++) for (let j = i + 1; j < boxes.length; j++) {
    const a = boxes[i].r, b = boxes[j].r;
    const ix = Math.min(a.x1, b.x1) - Math.max(a.x0, b.x0), iy = Math.min(a.y1, b.y1) - Math.max(a.y0, b.y0);
    if (ix > 3 && iy > 3) out.overlaps.push(name(boxes[i].e) + ' x ' + name(boxes[j].e) + ' by ' + Math.round(ix) + 'x' + Math.round(iy));
  }
  return out;
})()";

        /// <summary>Every chart (an svg.chart with a box): the size it was drawn at (its viewBox) against the size of its box, and whether anything is drawn in it.</summary>
        public const string Charts = @"(() => [...document.querySelectorAll('svg.chart')].filter(s => s.clientWidth > 0 && s.clientHeight > 0).map(s => {
  const vb = (s.getAttribute('viewBox') || '').split(/\s+/).map(Number);
  const panel = s.closest('section');
  return { id: s.id || s.dataset.c || s.dataset.k || '(unnamed)', panel: panel ? panel.getAttribute('aria-label') : '', viewBox: [vb[2], vb[3]], box: [s.clientWidth, s.clientHeight], drawn: s.childElementCount > 0,
    matches: Math.abs(vb[2] - s.clientWidth) <= 2 && Math.abs(vb[3] - s.clientHeight) <= 2 };
}))()";

        /// <summary>The brains panel: its scroll height against its client height (it must not hold more than it shows), and the breeding fields against the panel's bottom.</summary>
        public const string BrainsPanel = @"(() => {
  const body = document.querySelector('.p-brains .body'), inner = document.getElementById('b-brains');
  const fields = [...inner.querySelectorAll('.field')], last = fields[fields.length - 1];
  return { bodyScrollH: body.scrollHeight, bodyClientH: body.clientHeight, innerScrollH: inner.scrollHeight, innerClientH: inner.clientHeight, fields: fields.length,
    lastFieldBelowPanel: last ? last.getBoundingClientRect().bottom > body.getBoundingClientRect().bottom + 1 : null, charts: inner.querySelectorAll('svg.chart').length };
})()";

        /// <summary>The run screen's band: every part of it on one row, and nothing past the band's edge.</summary>
        public const string Band = @"(() => {
  const band = document.querySelector('.band'), br = band.getBoundingClientRect();
  const kids = [...band.children].filter(k => k.getBoundingClientRect().width > 0 && k.getBoundingClientRect().height > 0 && !k.hidden).map(k => ({ n: k.id || k.className, r: k.getBoundingClientRect() }));
  const oneRow = kids.every(a => kids.every(b => a.r.top < b.r.bottom && b.r.top < a.r.bottom));
  return { height: br.height, oneRow, kids: kids.map(k => k.n + ' ' + Math.round(k.r.top) + '-' + Math.round(k.r.bottom)), pastEdge: kids.filter(k => k.r.right > br.right + 1).map(k => k.n), scrollW: band.scrollWidth, clientW: band.clientWidth };
})()";

        /// <summary>
        /// The exam table's greyed cells. A column is found by its heading; a row's cell by the columns it spans, so that the summary rows (whose first cell spans two) line up with it.
        /// </summary>
        public const string Exam = @"(() => {
  const t = document.querySelector('.exam-t');
  if (!t) return null;
  const heads = [...t.querySelectorAll('thead th')];
  const cellAt = (tr, col) => { let at = 0; for (const c of tr.children) { const span = c.colSpan || 1; if (col < at + span) return c; at += span; } return null; };
  const col = label => heads.findIndex(h => new RegExp(label, 'i').test(h.innerText.replace(/\s+/g, ' ')));
  const row = tr => ({ name: tr.children[0].innerText.replace(/\s+/g, ' ').trim(), cells: [...tr.children].map(c => ({ text: c.innerText.replace(/\s+/g, ' ').trim(), off: c.classList.contains('off'), title: c.title || '', cls: c.className })) });
  const body = [...t.querySelectorAll('tbody:not(.vs) tr')].filter(r => !r.classList.contains('pool')).map(row);
  const pooled = [...t.querySelectorAll('tbody:not(.vs) tr.pool')].map(row);
  const vs = [...t.querySelectorAll('tbody.vs tr.pool')].map(row);
  const flow = col('Flow');
  const at = tr => { const c = flow < 0 ? null : cellAt(tr, flow); return c ? { text: c.innerText.replace(/\s+/g, ' ').trim(), off: c.classList.contains('off'), title: c.title || '', cls: c.className } : null; };
  return {
    headings: heads.map(h => ({ text: h.innerText.replace(/\s+/g, ' ').trim(), off: h.classList.contains('off'), title: (h.querySelector('a') || h).title || '' })),
    offHeadings: heads.filter(h => h.classList.contains('off')).length,
    offCells: [...t.querySelectorAll('td.off')].map(c => ({ text: c.innerText.replace(/\s+/g, ' ').trim(), title: c.title || '', ctl: c.classList.contains('ctlcell') })),
    body, pooled, vs, flowCol: flow,
    flowBody: [...t.querySelectorAll('tbody:not(.vs) tr')].filter(r => !r.classList.contains('pool')).map(at),
    flowPooled: [...t.querySelectorAll('tbody:not(.vs) tr.pool')].map(at),
    flowVs: [...t.querySelectorAll('tbody.vs tr.pool')].map(at),
    note: [...document.querySelectorAll('#b-exam .note')].map(n => n.innerText.replace(/\s+/g, ' ').trim()),
  };
})()";
    }
}
