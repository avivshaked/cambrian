"""A tank search's saved tops as a pool the bench can swim (D172; logbook/specs/nursery-leaf-tank-plan.md, step 8).

    python scripts/reads/tops-pool.py <repository root> <run folder>

The search writes each generation's top genome to tank-best.jsonl, one bare genome a line, and the top's body to the
same generation's row of tank-generations.jsonl. A pool is one genome a line, the body's index, a tab, and the genome's
JSON, so this joins the two by generation, keeps each genome's text byte for byte, and writes the pool into
rounds/nursery/pools/ under the first 16 hex digits of its SHA-256 (the pools' README). Beside it, tops-<hash>.json
records the run, its build, and each line's generation, body and search score. A top carried over unchanged is a line
for every generation it topped, and the record counts the distinct genomes. Refuses a run that has not completed;
--partial writes a running run's tops to --out instead, for a look at the shape.
"""
import argparse
import hashlib
import json
import os


def rows(path):
    with open(path, encoding='utf-8') as f:
        return [line.rstrip('\r\n') for line in f if line.strip()]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('root')
    ap.add_argument('run')
    ap.add_argument('--partial', action='store_true')
    ap.add_argument('--out')
    a = ap.parse_args()

    with open(os.path.join(a.run, 'run.json'), encoding='utf-8') as f:
        run = json.load(f)
    if run['status'] != 'completed' and not a.partial:
        raise SystemExit('%s is %s; a pool is made from a completed run' % (a.run, run['status']))
    if a.partial and not a.out:
        raise SystemExit('--partial writes to --out, never into the pools')

    best = rows(os.path.join(a.run, 'tank-best.jsonl'))
    gens = [json.loads(r) for r in rows(os.path.join(a.run, 'tank-generations.jsonl'))]
    if len(best) != len(gens):
        raise SystemExit('tank-best.jsonl has %d lines and tank-generations.jsonl %d rows' % (len(best), len(gens)))
    if [g['gen'] for g in gens] != list(range(len(gens))):
        raise SystemExit('tank-generations.jsonl is not generations 0 to %d in order' % (len(gens) - 1))

    text = ''.join('%d\t%s\n' % (g['top']['body'], genome) for g, genome in zip(gens, best))
    digest = hashlib.sha256(text.encode('utf-8')).hexdigest()[:16]
    folder = a.out or os.path.join(a.root, 'rounds', 'nursery', 'pools')
    os.makedirs(folder, exist_ok=True)
    pool = os.path.join(folder, digest + '.tsv')
    with open(pool, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)

    first = {}
    for i, genome in enumerate(best):
        first.setdefault(genome, i)
    record = {
        'pool': digest + '.tsv',
        'run': os.path.basename(os.path.normpath(a.run)),
        'status': run['status'],
        'recipe': run['recipe']['name'],
        'recipeHash': run['recipe'].get('hash'),
        'gitCommit': run['build']['gitCommit'],
        'gitDirty': run['build']['gitDirty'],
        'lines': len(best),
        'distinct': len(first),
        'tops': [{'line': i, 'gen': g['gen'], 'body': g['top']['body'], 'score': g['top']['score'],
                  'episodes': g['top']['episodes'], 'born': g['top']['born'], 'sameAsLine': first[genome]}
                 for i, (g, genome) in enumerate(zip(gens, best))],
    }
    with open(os.path.join(folder, 'tops-%s.json' % digest), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(record, f, indent=2)
        f.write('\n')
    print('%s: %d tops, %d distinct, from %s at %s' % (pool, len(best), len(first), record['run'], record['gitCommit'][:8]))


if __name__ == '__main__':
    main()
