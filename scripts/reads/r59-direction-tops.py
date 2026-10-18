"""Round 59's planted grazers (D174): the ten best genomes of leaf-direction-sparse's exam by their own brain's eating on
held-out seeds, cut into inocula/r59-direction-tops/ as round 55's nursery genomes were (inocula/r55-nursery/README.md).

    python scripts/reads/r59-direction-tops.py <run folder> [--count 10]

Reads the exam's tank-exam-pool.jsonl (where each genome came from), tank-exam.jsonl (its eating under each control) and
tank-exam-genomes.tsv (the genomes, one a line, as the run wrote them). Writes:
- one file a genome, its JSON exactly as the run wrote it, one line and a newline, named for its exam id;
- origins.tsv: each file's SHA-256, the run, the exam id, the generations it topped, whether it was a last elite, every id
  it had, its line in the source, and its eating under each control;
- pool.tsv: a weighted list the farm's trickle reads (EVOSIM_TRICKLE_POOL=@path; TricklePoolFiles.PrepareList), the ten
  at equal weight. The round's own list, and the mix it plants, are set by its pre-registration.
"""
import argparse
import hashlib
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'inocula', 'r59-direction-tops')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('run')
    ap.add_argument('--count', type=int, default=10)
    a = ap.parse_args()

    run = os.path.basename(os.path.normpath(a.run))
    pool = [json.loads(l) for l in open(os.path.join(a.run, 'tank-exam-pool.jsonl'), encoding='utf-8') if l.strip()]
    rows = [json.loads(l) for l in open(os.path.join(a.run, 'tank-exam.jsonl'), encoding='utf-8') if l.strip()]
    with open(os.path.join(a.run, 'tank-exam-genomes.tsv'), encoding='utf-8', newline='') as f:
        lines = [l.rstrip('\r\n') for l in f if l.strip()]
    if len(lines) != len(pool):
        raise SystemExit('tank-exam-genomes.tsv has %d lines and tank-exam-pool.jsonl %d rows' % (len(lines), len(pool)))
    brains = []
    for r in rows:
        if r['brain'] not in brains:
            brains.append(r['brain'])
    eaten = {b: {r['genome']: r['eaten'] for r in rows if r['brain'] == b} for b in brains}
    own = brains[0]
    best = sorted(range(len(pool)), key=lambda i: (-eaten[own][i], i))[:a.count]

    os.makedirs(OUT, exist_ok=True)
    origins = ['\t'.join(['file', 'sha256', 'run', 'id', 'tops', 'elite', 'ids', 'body', 'source', 'line'] + ['eaten ' + b for b in brains])]
    listed = ['\t'.join(['path', 'weight', 'run', 'id', 'body', 'ownEaten'])]
    for i in best:
        p = pool[i]
        body, text = lines[i].split('\t', 1)
        name = 'direction-sparse-%s.json' % p['id'].replace('.', '-')
        data = (text + '\n').encode('utf-8')
        with open(os.path.join(OUT, name), 'wb') as f:
            f.write(data)
        sha = hashlib.sha256(data).hexdigest()
        origins.append('\t'.join([name, sha, run, p['id'], ','.join(str(g) for g in p['tops']), 'yes' if p['elite'] else 'no',
                                  ','.join(p['ids']), body, 'nursery-d172:runs/nursery/%s/tank-exam-genomes.tsv' % run, str(i + 1)]
                                 + ['%.1f' % eaten[b][i] for b in brains]))
        listed.append('\t'.join([name, '%g' % (1.0 / len(best)), run, p['id'], body, '%.1f' % eaten[own][i]]))
        print('%s %s own %.1f J' % (name, sha[:16], eaten[own][i]))
    for path, text in (('origins.tsv', origins), ('pool.tsv', listed)):
        with open(os.path.join(OUT, path), 'w', encoding='utf-8', newline='\n') as f:
            f.write('\n'.join(text) + '\n')


if __name__ == '__main__':
    main()
