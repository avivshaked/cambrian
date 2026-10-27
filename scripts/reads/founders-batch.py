"""D182's patch measures, the founders' baseline: write the --restep --patches requests for every tank of a founders recipe's exam
(patches_founders in leaf-recipes.py), which has no run of its own. Each draw's deal is the one --restep computes: a request whose
members are no tank of the deal is checked (--check, which steps nothing), and the refusal names the deal.

    python scripts/reads/founders-batch.py <dotnet> <dll> <worktree root> <recipe, relative to it> <recipe hash> <out dir> [--brains own,brainless,knockout] [--lineage <file>]

The pool is the recipe's own (bodies.pool), already in the worktree. Writes <out>/req/, <out>/jobs.txt as patch-batch.py does, and
<out>/brains.json, the label of each exam control by its index, for patch-read.py. With --lineage, a map from pool line to founder
lineage (inocula/r59-direction-blades/lineage.json for the curriculum's twenty) is checked to name every line of the pool, and copied
to <out>/lineage.json, where scripts/reads/nursery-travel-read.py reads it.
"""
import argparse
import json
import os
import re
import subprocess

ap = argparse.ArgumentParser()
ap.add_argument('dotnet')
ap.add_argument('dll')
ap.add_argument('wt')
ap.add_argument('recipe')
ap.add_argument('hash')
ap.add_argument('out')
ap.add_argument('--brains', default='own,brainless,knockout')
ap.add_argument('--lineage')
a = ap.parse_args()

rec = json.load(open(os.path.join(a.wt, a.recipe), encoding='utf-8'))
labels = []
for c in rec['exam']['controls']:
    labels.append('knockout ' + ','.join(c['channels']) if c['kind'] == 'knockout' else c['kind'])
wanted = [b.strip() for b in a.brains.split(',') if b.strip()]
os.makedirs(os.path.join(a.out, 'req'), exist_ok=True)
with open(os.path.join(a.out, 'brains.json'), 'w', encoding='utf-8', newline='\n') as f:
    json.dump(labels, f)
if a.lineage:
    with open(os.path.join(a.wt, rec['bodies']['pool']), encoding='utf-8') as f:
        count = sum(1 for l in f if l.strip())
    lineage = json.load(open(a.lineage, encoding='utf-8'))
    if sorted(lineage, key=int) != [str(i) for i in range(count)]:
        raise SystemExit('%s does not name exactly the %d lines of %s' % (a.lineage, count, rec['bodies']['pool']))
    with open(os.path.join(a.out, 'lineage.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(lineage, f, indent=1)
        f.write('\n')


def base(episode, tank):
    return {'recipe': a.recipe, 'recipeHash': a.hash, 'pool': rec['bodies']['pool'], 'seed': rec['seed'], 'salt': rec['exam']['salt'],
            'generation': 0, 'episode': episode, 'brain': 0, 'tank': tank, 'members': [0]}  # one member, which no tank of 8 or 2 is


probe = os.path.join(a.out, 'probe.json')
jobs = []
for e in range(rec['exam']['episodes']):
    deal, t = [], 0
    while True:
        with open(probe, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(base(e, t), f)
        p = subprocess.run([a.dotnet, a.dll, '--restep', probe, '--check'], cwd=a.wt, capture_output=True, text=True)
        m = re.search(r'which is \[([0-9,]*)\]', p.stderr)
        if m:
            deal.append([int(x) for x in m.group(1).split(',') if x])
            t += 1
            continue
        if re.search(r'has \d+ tank\(s\)', p.stderr):
            break
        raise SystemExit('episode %d tank %d: unexpected answer from --check: %s %s' % (e, t, p.stdout, p.stderr))
    for b, lab in enumerate(labels):
        short = next((w for w in wanted if lab.startswith(w)), None)
        if short is None:
            continue
        for t, members in enumerate(deal):
            req = dict(base(e, t), brain=b, members=members)
            stem = '%s-d%02d-t%d' % (short, e, t)
            path = os.path.join(a.out, 'req', stem + '.json')
            with open(path, 'w', encoding='utf-8', newline='\n') as f:
                json.dump(req, f, indent=2)
            jobs.append((os.path.abspath(path), os.path.abspath(os.path.join(a.out, 'ep', stem))))
os.remove(probe)
with open(os.path.join(a.out, 'jobs.txt'), 'w', encoding='utf-8', newline='\n') as f:
    for r, o in jobs:
        f.write(r + '\t' + o + '\n')
print('%s: %d requests over %d episodes for %s' % (a.recipe, len(jobs), rec['exam']['episodes'], ','.join(wanted)))
