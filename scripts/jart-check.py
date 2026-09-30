"""Check a JART against the paper it reviews.

Usage: python scripts/jart-check.py research/papers/175-* [more package folders]

Each folder must hold JART.md and the source.md that pdf-clean-markdown built.
Two checks, both mechanical:
  1. Every direct quote (text in double quotes, three words or more) is found
     word for word in source.md, and on the page it cites when a (p.N) follows it.
  2. Every page it cites, p.N or pp.N-M, exists as a "### Page N" heading.
Matching ignores case, whitespace, markdown emphasis and curly-versus-straight
punctuation, and nothing else. Exit code 1 if any folder fails.
"""
import re
import sys
from pathlib import Path

QUOTE = re.compile(r'["“]([^"“”]+?)["”]\s*(\((?:[^()]*?\b)?pp?\.\s*(\d+))?')
PAGE_REF = re.compile(r'\bpp?\.\s*(\d+)(?:\s*[-–]\s*(\d+))?')


def norm(s):
    s = s.replace('‘', "'").replace('’', "'").replace('“', '"').replace('”', '"')
    s = s.replace('–', '-').replace('—', '-').replace('­', '')
    s = re.sub(r'[_*`]', '', s)
    return re.sub(r'\s+', ' ', s).strip().lower()


def pages(source):
    out, cur, buf = {}, None, []
    for line in source.splitlines():
        m = re.match(r'^### Page (\d+)\s*$', line)
        if m:
            if cur is not None:
                out[cur] = norm('\n'.join(buf))
            cur, buf = int(m.group(1)), []
        elif cur is not None:
            buf.append(line)
    if cur is not None:
        out[cur] = norm('\n'.join(buf))
    return out


def check(folder):
    jart, src = folder / 'JART.md', folder / 'source.md'
    if not jart.exists() or not src.exists():
        return [f'missing {"JART.md" if not jart.exists() else "source.md"}']
    text = jart.read_text(encoding='utf-8')
    pg = pages(src.read_text(encoding='utf-8'))
    whole = ' '.join(pg[k] for k in sorted(pg))
    problems, nq = [], 0
    for m in QUOTE.finditer(text):
        q = norm(m.group(1))
        if len(q.split()) < 3:
            continue
        nq += 1
        where = [k for k, v in pg.items() if q in v]
        if not where:
            hint = ' (spans a page break?)' if q in whole else ''
            problems.append(f'quote not found{hint}: "{m.group(1)[:80]}"')
        elif m.group(3) and int(m.group(3)) not in where:
            problems.append(f'quote cited p.{m.group(3)} but found on p.{",".join(map(str, where))}: "{m.group(1)[:60]}"')
    cited = set()
    for m in PAGE_REF.finditer(text):
        a = int(m.group(1)); b = int(m.group(2) or a)
        cited.update(range(a, b + 1) if b >= a else [a, b])
    for n in sorted(cited - set(pg)):
        problems.append(f'cites p.{n}, which source.md has no heading for (it has 1-{max(pg) if pg else 0})')
    print(f'{folder.name}: {nq} quotes, {len(cited)} pages cited, {len(problems)} problems')
    return problems


def main(args):
    if not args:
        print(__doc__); return 2
    failed = 0
    for a in args:
        f = Path(a)
        if not f.is_dir():
            continue
        probs = check(f)
        for p in probs:
            print('   ', p)
        failed += bool(probs)
    print(f'{failed} folder(s) with problems')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
