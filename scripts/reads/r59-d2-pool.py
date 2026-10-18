"""Round 59's clause D2 (logbook/0140, on branch round-59): the planted-descended grazers alive at 48,000 s, as a nursery pool per world.

    python scripts/reads/r59-d2-pool.py <round 59's r59-read.py> <out root> <run> [<run> ...] [--at 48000] [--drop <world>:<lines>]

The lines, the guilds and the stitching of a resumed run onto the run it resumed are round 59's own reader's, imported from the path
given, so nothing of its rules is copied here. Per world: the living are the ids of the snapshot at --at in the world's last run; the
ones kept are those whose line is planted (planted founder or planted descendant) and that are grazers by their birth row (atk 1, ink 1,
pho 0); their genomes come from genomes.jsonl.gz, read across the world's runs; and they are collapsed to distinct genomes, compared
without the id. Prints the count against D2's bar of 8 and the bodies each genome stands for, and writes
<out root>/rounds/nursery/pools/<hash>.tsv, one line a genome (0, a tab, the genome's JSON without its id), named by the first 16 hex
digits of the pool's SHA-256 as every nursery pool is. The order is the genomes' first ids, ascending.
"""
import argparse
import gzip
import hashlib
import importlib.util
import json
import os

D2_MIN_GENOMES = 8


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('reader')
    ap.add_argument('out_root')
    ap.add_argument('runs', nargs='+')
    ap.add_argument('--at', type=float, default=48000.0)
    ap.add_argument('--drop', action='append', default=[],
                    help="<world>:<line>,<line>: leave out those lines of the world's pool as first written, by their 1-based number "
                         "(the genomes the nursery's --check refuses beside leaves)")
    a = ap.parse_args()
    drop = {}
    for d in a.drop:
        world, lines = d.split(':')
        drop[world] = sorted(int(x) for x in lines.split(','))

    spec = importlib.util.spec_from_file_location('r59read', a.reader)
    r = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(r)
    r.runrec.VERBOSE = False
    swimmers = r.Swimmers(r.SWIMMERS)
    opts = argparse.Namespace(year=r.YEAR, every=r.EVERY, end=a.at)

    for run in a.runs:
        w = r.World(os.path.abspath(run), opts, swimmers)
        snap = os.path.join(w.dir, 'snapshots', '%09d.jsonl.gz' % int(a.at))
        with gzip.open(snap, 'rt', encoding='utf-8') as f:
            living = [json.loads(l)['id'] for l in f if l.strip()]
        planted = [i for i in living if w.line_class(i) in ('planted founder', 'planted descendant')]
        grazers = [i for i in planted if w.is_grazer(i)]
        unborn = [i for i in living if i not in w.born]

        want, genomes = set(grazers), {}
        for seg in w.segs:
            path = os.path.join(seg[0], 'genomes.jsonl.gz')
            if not os.path.exists(path):
                continue
            with gzip.open(path, 'rt', encoding='utf-8') as f:
                for l in f:
                    if not l.strip():
                        continue
                    g = json.loads(l)
                    if g['id'] in want and g['id'] not in genomes:
                        genomes[g['id']] = g
        missing = sorted(want - set(genomes))

        distinct = {}  # canonical text -> (first id, compact text, bodies)
        for i in sorted(genomes):
            g = dict(genomes[i])
            del g['id']
            key = json.dumps(g, sort_keys=True, separators=(',', ':'))
            if key in distinct:
                distinct[key][2].append(i)
            else:
                distinct[key] = (i, json.dumps(g, separators=(',', ':')), [i])
        rows = sorted(distinct.values())

        print('%s (%s): %d living at %g s, %d planted-descended, %d of them grazers, %d genomes found, %d missing, '
              '%d living with no birth row; %d distinct genomes (D2 needs %d): %s' %
              (w.name, ' + '.join(os.path.basename(os.path.dirname(s[0])) for s in w.segs), len(living), a.at, len(planted),
               len(grazers), len(genomes), len(missing), len(unborn), len(rows), D2_MIN_GENOMES,
               'read' if len(rows) >= D2_MIN_GENOMES else 'not read'))
        for note in w.notes:
            print('  note:', note)
        for first, _, bodies in rows:
            print('  genome of %d (line %s, %s): %d bodies' % (first, w.root.get(first), w.line_class(first), len(bodies)))
        if missing:
            print('  missing genomes for ids', missing[:20])
        if not rows:
            continue
        if w.name in drop:
            gone = [rows[n - 1] for n in drop[w.name]]
            rows = [row for n, row in enumerate(rows, 1) if n not in drop[w.name]]
            print('  left out (lines %s of the pool as first written): genomes of %s; %d distinct genomes remain' %
                  (','.join(map(str, drop[w.name])), ', '.join(str(g[0]) for g in gone), len(rows)))
        text =''.join('0\t' + t + '\n' for _, t, _ in rows)
        name = hashlib.sha256(text.encode('utf-8')).hexdigest()[:16]
        path = os.path.join(a.out_root, 'rounds', 'nursery', 'pools', name + '.tsv')
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
        print('  pool rounds/nursery/pools/%s.tsv' % name)


if __name__ == '__main__':
    main()
