"""D182's patch measures, read: every episode folder of a patch-batch.py batch, against the run it re-stepped.

    python scripts/reads/patch-read.py <out dir> <run folder>
    python scripts/reads/patch-read.py <out dir>          (a founders-batch.py batch, which has no run)

First the identity check: each re-stepped net against the run's own record of that genome on that draw under that brain (the per-draw
nets of tank-exam.jsonl or tank-bench.jsonl), bit for bit as the round-trip text gives it. Then, by brain, the bodies' measures: the share
of alive time in a leaf's smell, the exits from a patch and what followed them (turn-back within the window, late return, transit to
another patch, unended), and for bodies that started in water smelling of nothing, how many found a smell and how soon. For an exam, the
same by the generation whose top each genome was (elites apart).
"""
import json
import os
import statistics as st
import sys

out, run = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else None)
label, record = {}, {}
if run is None:
    # founders-batch.py's batch: no run, so no record to check against; the brains' labels are the recipe's controls.
    kind = 'founders'
    label = dict(enumerate(json.load(open(os.path.join(out, 'brains.json'), encoding='utf-8'))))
else:
    kind = 'exam' if os.path.exists(os.path.join(run, 'tank-exam-draws.jsonl')) else 'bench'
    for l in open(os.path.join(run, 'tank-%s-draws.jsonl' % kind), encoding='utf-8'):
        if l.strip():
            r = json.loads(l)
            label[r['b']] = r['brain']
    for l in open(os.path.join(run, 'tank-%s.jsonl' % kind), encoding='utf-8'):
        if l.strip():
            r = json.loads(l)
            record[(r['brain'], r['genome'])] = r['per']
gen_of = {}
if kind == 'exam':
    for l in open(os.path.join(run, 'tank-exam-pool.jsonl'), encoding='utf-8'):
        if l.strip():
            g = json.loads(l)
            gen_of[g['genome']] = ('elite' if g['elite'] and not g['tops'] else 'g%d' % min(g['tops']))

rows, checked, mismatched, missing = [], 0, [], []
ep = os.path.join(out, 'ep')
for stem in sorted(os.listdir(ep)) if os.path.isdir(ep) else []:
    folder = os.path.join(ep, stem)
    if not os.path.isdir(folder):
        continue  # the job's own .log and .err lie beside its folder
    res, pat = os.path.join(folder, 'result.json'), os.path.join(folder, 'patches.jsonl')
    if not os.path.exists(res):
        missing.append(stem)
        continue
    req = json.load(open(os.path.join(out, 'req', stem + '.json'), encoding='utf-8'))
    brain = label[req['brain']]
    for m in json.load(open(res, encoding='utf-8'))['members']:
        if not m['landed'] or not record:
            continue
        want = record[(brain, m['pool'])][req['episode']]
        checked += 1
        if float(m['net']) != want:
            mismatched.append((stem, m['slot'], m['net'], want))
    for l in open(pat, encoding='utf-8'):
        if l.strip():
            r = json.loads(l)
            r['brainLabel'] = brain
            r['gen'] = gen_of.get(r['pool'], 'founder')
            rows.append(r)

print('%s %s: %d episodes read, %d missing; %d nets checked against the record, %d differ' %
      (os.path.basename(os.path.normpath(run or out)), kind, len(set((r['episode'], r['brain'], r['tank']) for r in rows)), len(missing), checked, len(mismatched)))
for x in mismatched[:10]:
    print('  differs:', x)


def summary(rs):
    n = len(rs)
    share = st.mean(r['scentShare'] for r in rs if r['scentShare'] is not None)
    exits = sum(r['exits'] for r in rs)
    tb, late, tr, un = (sum(r[k] for r in rs) for k in ('turnBacks', 'lateReturns', 'transits', 'unended'))
    bare = [r for r in rs if r['startedInScent'] is False]
    found = [r['firstScent'] for r in bare if r['firstScent'] is not None]
    alive = st.mean(r['alive'] for r in rs)
    return ('n %3d  in smell %.2f  exits/body %5.2f  of exits: turn-back %.2f late %.2f transit %.2f unended %.2f  '
            'transits/body %.2f  bare starts %3d, found smell %.2f, median first %s s  alive %.0f s' %
            (n, share, exits / n, *(x / exits if exits else float('nan') for x in (tb, late, tr, un)), tr / n, len(bare),
             len(found) / len(bare) if bare else float('nan'), ('%.0f' % st.median(found)) if found else '-', alive))


for b in sorted(set(r['brainLabel'] for r in rows)):
    rs = [r for r in rows if r['brainLabel'] == b]
    print('\n%s: %s' % (b, summary(rs)))
    if kind == 'exam':
        def key(g):
            return (1, 0) if g == 'elite' else (0, int(g[1:]))
        for g in sorted(set(r['gen'] for r in rs), key=key):
            print('  %-6s %s' % (g, summary([r for r in rs if r['gen'] == g])))
