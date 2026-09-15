# -*- coding: utf-8 -*-
"""D5's whole course: the eaters' boom and bust, from lineage.jsonl (who was whose child)
and absorptive.jsonl (what each living eater's books read at every sample).

An "absorptive line" here is the maximal chain of absorptive ancestry above a body: walk
the parent chain up while the parent's own `abs` flag is 1 and stop at the first parent
that is not an eater. The top of that chain is the line's founder, which is what the
clade scorer's connected-clade walk builds for the goal rule. Two booms are "the same
line" when the bodies of the second carry the same founder as the bodies of the first.
"""
import glob, json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'logbook', 'specs', 'r37-read'))
from report_read import rows, num

A = ['r38-s%d' % i for i in range(1, 6)]


def run_dir(a):
    return os.path.dirname(sorted(glob.glob(os.path.join(ROOT, 'runs', a, '*', 'run.json')))[-1])


def lineage(a):
    """id -> (parent, born, abs, jnt, pho, gen). Streamed; the file is large."""
    born = {}
    with open(os.path.join(run_dir(a), 'lineage.jsonl'), encoding='utf-8') as f:
        for line in f:
            if '"e":"b"' not in line:
                continue
            r = json.loads(line)
            born[r['id']] = (r['p'], r['t'], r['abs'], r['jnt'], r['pho'], r['g'])
    return born


def absorptive(a):
    """t -> list of rows, streamed."""
    out = {}
    with open(os.path.join(run_dir(a), 'absorptive.jsonl'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line.startswith('{'):
                continue
            r = json.loads(line)
            if r.get('dead'):
                continue
            out.setdefault(r['t'], []).append(r)
    return out


def mean(v):
    return sum(v) / len(v) if v else float('nan')


def median(v):
    s = sorted(v)
    n = len(s)
    return float('nan') if not n else (s[n // 2] if n % 2 else 0.5 * (s[n // 2 - 1] + s[n // 2]))


out = ['## D5  the eaters\' booms: where each boom\'s bodies came from. The line founder is'
       ' the top of the unbroken absorptive parent chain above a body.',
       'arm\twindow\tt\tliving eaters\tdistinct line founders\tlargest line founder id'
       '\tfounder born t\tfounder kind\tshare of the window on that founder\tgen min\tgen max'
       '\tgen median\tmean age s']
books = ['## D5  what the living eaters\' books read across the boom and the bust'
         ' (absorptive.jsonl, every living eater at the sample)',
         'arm\tt\tn\tmean netW\tmedian netW\tshare netW < 0\tmean foodW\tmean lightW'
         '\tmean upkeepW\tmean densityHere\tmedian y\tmean share\tmean tissue\tmixotroph share'
         '\tmean children']
same = ['## D5  is the second boom the same line as the first?',
        'arm\tfirst peak t\tfirst peak founders (top 3 by count)\tsecond window t'
        '\tsecond founders (top 3)\tshared founder\tverdict']


def founders(born, ids):
    """id -> founder of its absorptive line."""
    out2 = {}
    for i in ids:
        cur = i
        seen = set()
        while True:
            rec = born.get(cur)
            if rec is None:
                break
            p = rec[0]
            prec = born.get(p)
            if p < 0 or prec is None or prec[2] != 1 or p in seen:
                break
            seen.add(cur)
            cur = p
        out2[i] = cur
    return out2


REP = {}
for a in A:
    r, _ = rows(a)
    REP[a] = r

for a in A:
    born = lineage(a)
    ab = absorptive(a)
    r = REP[a]
    inh = [(x['t'], num(x['inherit'])) for x in r]
    pk = max(inh, key=lambda z: z[1])
    after = [z for z in inh if z[0] > pk[0]]
    tr = min(after, key=lambda z: z[1]) if after else pk
    post = [z for z in after if z[0] > tr[0]]
    sp = max(post, key=lambda z: z[1]) if post else None
    windows = [('first peak', pk[0]), ('trough', tr[0])]
    if sp:
        windows.append(('second peak', sp[0]))
    windows.append(('end', inh[-1][0]))

    fnd_by_window = {}
    for name, t in windows:
        srt = sorted(ab.keys(), key=lambda u: abs(u - t))
        tt = srt[0] if srt else None
        rowsx = ab.get(tt, [])
        ids = [q['id'] for q in rowsx]
        f = founders(born, ids)
        counts = {}
        for i in ids:
            counts[f[i]] = counts.get(f[i], 0) + 1
        top = sorted(counts.items(), key=lambda kv: -kv[1])
        fnd_by_window[name] = (tt, counts, top, rowsx)
        if not ids:
            out.append('%s\t%s\t%s\t0\t0\t-\t-\t-\t-\t-\t-\t-\t-' % (a, name, tt))
            continue
        fid = top[0][0]
        frec = born.get(fid, (None, None, None, None, None, None))
        kind = ('mixotroph' if frec[4] == 1 else 'rigid eater') if frec[2] == 1 else '(not an eater)'
        gens = [q['gen'] for q in rowsx]
        out.append('%s\t%s\t%s\t%d\t%d\t%d\t%s\t%s\t%.3f\t%d\t%d\t%.1f\t%.0f' % (
            a, name, tt, len(ids), len(counts), fid, frec[1], kind,
            top[0][1] / len(ids), min(gens), max(gens), median(gens),
            mean([q['age'] for q in rowsx])))

    for name, t in windows:
        tt, counts, top, rowsx = fnd_by_window[name]
        if not rowsx:
            books.append('%s\t%s\t0\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-' % (a, tt))
            continue
        net = [q['netW'] for q in rowsx]
        books.append('%s\t%s\t%d\t%.4f\t%.4f\t%.3f\t%.4f\t%.4f\t%.4f\t%.4f\t%.2f\t%.3f\t%.1f'
                     '\t%.3f\t%.2f' % (
            a, tt, len(rowsx), mean(net), median(net),
            sum(1 for v in net if v < 0) / len(net),
            mean([q['foodW'] for q in rowsx]), mean([q['lightW'] for q in rowsx]),
            mean([q['upkeepW'] for q in rowsx]), mean([q['densityHere'] for q in rowsx]),
            median([q['y'] for q in rowsx]), mean([q['share'] for q in rowsx]),
            mean([q['tissue'] for q in rowsx]),
            sum(1 for q in rowsx if q['mixotroph']) / len(rowsx),
            mean([q['children'] for q in rowsx])))

    ft, fc, ftop, _ = fnd_by_window['first peak']
    last = 'second peak' if 'second peak' in fnd_by_window else 'end'
    st, sc, stop, _ = fnd_by_window[last]
    shared = set(fc) & set(sc)
    same.append('%s\t%s\t%s\t%s\t%s\t%s\t%s' % (
        a, ft, ' '.join('%d(%d)' % kv for kv in ftop[:3]), st,
        ' '.join('%d(%d)' % kv for kv in stop[:3]) if stop else '-',
        ' '.join(str(x) for x in sorted(shared)) if shared else 'none',
        'same line' if shared else 'a fresh line'))

books += ['', '## the same books at every 1,000 s, mean netW and n, per seed']
hdr = 't'
for a in A:
    hdr += '\t%s n\t%s mean netW\t%s mean foodW\t%s mean densityHere' % (a, a, a, a)
books.append(hdr)
AB = {a: absorptive(a) for a in A}
for t in range(1000, 30001, 1000):
    cells = []
    for a in A:
        rowsx = AB[a].get(t, [])
        if rowsx:
            cells += ['%d' % len(rowsx), '%.4f' % mean([q['netW'] for q in rowsx]),
                      '%.4f' % mean([q['foodW'] for q in rowsx]),
                      '%.4f' % mean([q['densityHere'] for q in rowsx])]
        else:
            cells += ['0', '-', '-', '-']
    books.append('%d\t' % t + '\t'.join(cells))

open(os.path.join(HERE, 'eaters-lines.tsv'), 'w', encoding='utf-8').write(
    '\n'.join(out + [''] + same) + '\n')
open(os.path.join(HERE, 'eaters-books.tsv'), 'w', encoding='utf-8').write(
    '\n'.join(books) + '\n')
print('\n'.join(out + [''] + same))
print()
print('\n'.join(books[:40]))
