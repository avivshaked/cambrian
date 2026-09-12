"""Read a run report's markdown table by column NAME (never by position).

Usage as a module: rows(arm) -> list of dicts {colname: raw string}, plus t as int.
"""
import re, os
ROOT = r'D:\Projects\experiments\evolution-simulator'

def _clean(c):
    return c.replace('**','').strip()

def rows(arm, root=ROOT):
    path = os.path.join(root, 'runs', arm + '.md')
    header = None
    out = []
    with open(path, encoding='utf-8') as f:
        for line in f:
            if not line.startswith('|'):
                continue
            cells = [_clean(c) for c in line.strip().strip('|').split('|')]
            if header is None:
                if cells and cells[0].startswith('t ('):
                    header = cells
                continue
            if set(''.join(cells)) <= set('-: '):
                continue
            if not cells or not re.match(r'^\d+$', cells[0]):
                continue
            d = dict(zip(header, cells))
            d['t'] = int(cells[0])
            out.append(d)
    if header is None:
        raise SystemExit('no header in ' + path)
    return out, header

def num(s):
    s = s.strip()
    if s in ('', '—', '-'):
        return None
    if s.endswith('%'):
        return float(s[:-1])
    if '/' in s:
        return float(s.split('/')[0])
    return float(s)

if __name__ == '__main__':
    import sys
    r, h = rows(sys.argv[1])
    print(len(r), 'rows', r[0]['t'], r[-1]['t'])
    print(h)
