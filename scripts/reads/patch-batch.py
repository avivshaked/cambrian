"""D182's patch measures: write the --restep --patches requests for every tank of a finished run's exam (or bench), from the run's own
deal, for the brains named.

    python scripts/reads/patch-batch.py <run folder> <worktree root> <recipe, relative to that root> <out dir> [--brains own,knockout]

The run's genome file (tank-exam-genomes.tsv, or tank-bench-genomes.tsv) is copied into <worktree>/runs/patch-pools/, since a re-step
reads nothing outside its own checkout. Each request goes to <out>/req/, its episode's folder will be <out>/ep/<name>/, and
<out>/jobs.txt lists one "<request>\t<folder>" a line for patch-run.ps1. A brain is chosen by the start of its label (own, brainless,
knockout).
"""
import argparse
import json
import os
import shutil

ap = argparse.ArgumentParser()
ap.add_argument('run')
ap.add_argument('wt')
ap.add_argument('recipe')
ap.add_argument('out')
ap.add_argument('--brains', default='own,knockout')
a = ap.parse_args()

kind = 'exam' if os.path.exists(os.path.join(a.run, 'tank-exam-draws.jsonl')) else 'bench'
deal = [json.loads(l) for l in open(os.path.join(a.run, 'tank-%s-draws.jsonl' % kind), encoding='utf-8') if l.strip()]
runj = json.load(open(os.path.join(a.run, 'run.json'), encoding='utf-8'))
rec = json.load(open(os.path.join(a.run, 'recipe.json'), encoding='utf-8'))

# The labels in a deal: an exam names genomes by id (tank-exam-pool.jsonl maps each id to its line); a bench by p<line>.
if kind == 'exam':
    line_of = {}
    for l in open(os.path.join(a.run, 'tank-exam-pool.jsonl'), encoding='utf-8'):
        if l.strip():
            g = json.loads(l)
            for i in g['ids']:
                line_of[i] = g['genome']
else:
    line_of = None

name = os.path.basename(os.path.normpath(a.run))
pools = os.path.join(a.wt, 'runs', 'patch-pools')
os.makedirs(pools, exist_ok=True)
pool_abs = os.path.join(pools, '%s-%s-genomes.tsv' % (name, kind))
shutil.copyfile(os.path.join(a.run, 'tank-%s-genomes.tsv' % kind), pool_abs)
pool_rel = os.path.relpath(pool_abs, a.wt).replace('\\', '/')

wanted = [b.strip() for b in a.brains.split(',') if b.strip()]
os.makedirs(os.path.join(a.out, 'req'), exist_ok=True)
jobs = []
for row in deal:
    short = next((b for b in wanted if row['brain'].startswith(b)), None)
    if short is None:
        continue
    for t, tank in enumerate(row['tanks']):
        members = [line_of[x] for x in tank] if line_of is not None else [int(x[1:]) for x in tank]
        req = {
            'recipe': a.recipe,
            'recipeHash': runj['recipe']['hash'],
            'pool': pool_rel,
            'seed': rec.get('seed', '0x1'),
            'salt': rec['exam']['salt'],
            'generation': 0,
            'episode': row['draw'],
            'brain': row['b'],
            'tank': t,
            'members': members,
        }
        stem = '%s-d%02d-t%d' % (short, row['draw'], t)
        path = os.path.join(a.out, 'req', stem + '.json')
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(req, f, indent=2)
        jobs.append((os.path.abspath(path), os.path.abspath(os.path.join(a.out, 'ep', stem))))

with open(os.path.join(a.out, 'jobs.txt'), 'w', encoding='utf-8', newline='\n') as f:
    for r, o in jobs:
        f.write(r + '\t' + o + '\n')
print('%s %s: %d requests for %s, pool %s' % (name, kind, len(jobs), ','.join(wanted), pool_rel))
