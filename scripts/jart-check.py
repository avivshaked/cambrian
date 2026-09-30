"""Check a JART against the paper it reviews.

Usage: python scripts/jart-check.py research/papers/175-* [more package folders]

Each folder must hold JART.md and the source.md that pdf-clean-markdown built.

source.md is text pulled out of a PDF, and the pull is noisy: ligatures (the
single character for "fi"), words split at a line end ("navi- gates"), accents
stored apart from their letters, footnote marks glued to words, symbols lost,
and in scanned papers plain misreadings ("thc" for "the"). A correct quote can
therefore differ from source.md. So every direct quote (text in double quotes,
three words or more) gets one of three verdicts:

  found     Its letters and digits appear in that order in source.md, on the
            page it cites (or across that page's break). Case, spacing,
            punctuation, hyphens, accents and ligatures are ignored.
  near      Not found, but a passage on the cited page matches it closely
            (85% or more of its letters). Usually extraction noise, but it can
            be a misquote. Open the PDF page and look. If the PDF has the
            quoted words, keep the quote and write "PDF" inside its page
            reference, as (p.5, checked against the PDF); the checker then
            accepts it. If not, correct the quote or paraphrase it.
  missing   Nothing close. Paraphrase it. A "PDF" mark does not rescue this.

It also checks that every page cited, p.N or pp.N-M, exists as a "### Page N"
heading. Exit code 1 if any folder has a missing quote, an unresolved near
match, a quote on the wrong page, or a cited page that does not exist.
"""
import difflib
import re
import sys
import unicodedata
from pathlib import Path

QUOTE = re.compile(r'["“]([^"“”]+?)["”](\s*\(([^()]*)\))?')
PAGE_IN = re.compile(r'\bpp?\.\s*(\d+)(?:\s*[-–]\s*(\d+))?')
NEAR = 0.85
BREAK_JUNK = 150     # letters of footer, page number and running head allowed across a page break


def loose(s):
    """Letters and digits only, lower case, ligatures expanded, accents dropped."""
    s = re.sub(r'\[[^\]]{0,4}\]', '', s)          # footnote marks as the extractor writes them, [1] or [∗]
    s = unicodedata.normalize('NFKD', s)
    s = ''.join(c for c in s if not unicodedata.combining(c))
    return re.sub(r'[^a-z0-9]', '', s.lower())


def pages(source):
    out, cur, buf = {}, None, []
    for line in source.splitlines():
        m = re.match(r'^### Page (\d+)\s*$', line)
        if m:
            if cur is not None:
                out[cur] = loose('\n'.join(buf))
            cur, buf = int(m.group(1)), []
        elif cur is not None and not re.match(r'^> (Text source|Figure|Table)', line):
            buf.append(line)
    if cur is not None:
        out[cur] = loose('\n'.join(buf))
    return out


def cited_pages(ref):
    got = []
    for m in PAGE_IN.finditer(ref or ''):
        a = int(m.group(1)); b = int(m.group(2) or a)
        got += list(range(a, b + 1)) if b >= a else [a, b]
    return got


def on_page(q, p, pg):
    """q lies on page p, or runs across a break between p and a neighbour (not wholly on the neighbour)."""
    if q in pg[p]:
        return True
    # A break carries a footer, a page number and a running head, so allow some text between the halves.
    for a, b in ((p - 1, p), (p, p + 1)):
        if a in pg and b in pg:
            for k in range(8, len(q) - 7):
                if q[:k] in pg[a][-(k + BREAK_JUNK):] and q[k:] in pg[b][:len(q) - k + BREAK_JUNK]:
                    return True
    return False


def best_near(q, text):
    """Best similarity of q against any window of text, seeded from 6-letter anchors."""
    best, L = 0.0, len(q)
    seen = set()
    for i in range(0, max(1, L - 5), 3):
        anchor = q[i:i + 6]
        for m in re.finditer(re.escape(anchor), text):
            start = max(0, m.start() - i)
            if start in seen:
                continue
            seen.add(start)
            for w in (text[start:start + L], text[max(0, start - 4):start + L + 4]):
                r = difflib.SequenceMatcher(None, q, w, autojunk=False).ratio()
                best = max(best, r)
    return best


def check(folder):
    jart, src = folder / 'JART.md', folder / 'source.md'
    if not jart.exists() or not src.exists():
        return [f'missing {"JART.md" if not jart.exists() else "source.md"}'], {}
    text = jart.read_text(encoding='utf-8')
    pg = pages(src.read_text(encoding='utf-8'))
    order = sorted(pg)
    problems, tally = [], {'found': 0, 'near, checked': 0, 'near': 0, 'missing': 0}
    for m in QUOTE.finditer(text):
        raw, ref = m.group(1), m.group(3) or ''
        q = loose(raw)
        if len(raw.split()) < 3 or len(q) < 12:
            continue
        cites = cited_pages(ref)
        scope = cites or order
        # the cited page, and each break it shares with a neighbour, since a quote may run across one
        spans = {p: pg.get(p - 1, '') + pg[p] + pg.get(p + 1, '') for p in scope if p in pg}
        short = raw if len(raw) <= 70 else raw[:67] + '...'
        if any(on_page(q, p, pg) for p in scope if p in pg):
            tally['found'] += 1
            continue
        elsewhere = [p for p in order if q in pg[p]]
        if elsewhere:
            problems.append(f'wrong page: cited p.{",".join(map(str, cites))}, found on p.{",".join(map(str, elsewhere))}: "{short}"')
            continue
        score = max((best_near(q, t) for t in spans.values()), default=0.0)
        if score >= NEAR:
            if 'pdf' in ref.lower():
                tally['near, checked'] += 1
            else:
                tally['near'] += 1
                problems.append(f'near ({score:.0%}), look at the PDF page: "{short}"')
        else:
            tally['missing'] += 1
            problems.append(f'missing (best {score:.0%}): "{short}"')
    for n in sorted(set(cited_pages(text)) - set(pg)):
        problems.append(f'cites p.{n}, but source.md has no such page (it has 1-{max(pg) if pg else 0})')
    return problems, tally


def main(args):
    if not args:
        print(__doc__); return 2
    failed = 0
    for a in args:
        f = Path(a)
        if not f.is_dir():
            continue
        probs, tally = check(f)
        print(f'{f.name}: ' + ', '.join(f'{v} {k}' for k, v in tally.items()) + f'; {len(probs)} to fix')
        for p in probs:
            print('   ', p)
        failed += bool(probs)
    print(f'{failed} folder(s) with something to fix')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.exit(main(sys.argv[1:]))
