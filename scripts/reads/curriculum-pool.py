"""D236's curriculum founders: the twenty of inocula/r59-direction-blades/pool-mixed.tsv (the ten tops, then the ten blades) as a nursery pool, each on body 0, brains and all.

    python -I scripts/reads/curriculum-pool.py <repository root>

Reads pool-mixed.tsv for the order (the ten tops of inocula/r59-direction-tops/ best eater first, then their ten blades), and for each
genome file the origins.tsv beside it for its SHA-256, and refuses a file whose bytes no longer match. Writes
rounds/nursery/pools/<hash>.tsv, one line a genome: 0, a tab, and the genome's JSON exactly as the file holds it, without its newline.
The name is the first 16 hex digits of the pool's SHA-256, as every nursery pool's is (rounds/nursery/pools/README.md). Prints the name.
The first ten lines are the bytes of rounds/nursery/pools/410a70db44a4b20a.tsv (D178's founders, made by scripts/reads/bodies-pool.py).
Also writes inocula/r59-direction-blades/lineage.json, the founder lineage of each pool line for scripts/reads/nursery-travel-read.py.
A blade is of its top's lineage (its id without "-blade"), so the twenty lines make ten lineages: a blade shares its top's brain and is
no independent draw of a lineage. founders-batch.py --lineage copies the file into a batch's out dir.
"""
import argparse
import hashlib
import json
import os

FOLDER = os.path.join('inocula', 'r59-direction-blades')


def rows(path):
    with open(path, encoding='utf-8', newline='') as f:
        lines = [l.rstrip('\r\n') for l in f if l.strip()]
    head = lines[0].split('\t')
    return [dict(zip(head, l.split('\t'))) for l in lines[1:]]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('root')
    a = ap.parse_args()
    folder = os.path.join(a.root, FOLDER)
    origins = {}
    out = []
    lineage = {}
    for r in rows(os.path.join(folder, 'pool-mixed.tsv')):
        path = os.path.normpath(os.path.join(folder, r['path']))
        home = os.path.dirname(path)
        if home not in origins:
            origins[home] = {o['file']: o['sha256'] for o in rows(os.path.join(home, 'origins.tsv'))}
        name = os.path.basename(path)
        with open(path, 'rb') as f:
            data = f.read()
        # A Windows checkout may end the line with CRLF; the file as committed, and as origins.tsv hashed it, ends with LF.
        if data.endswith(b'\r\n'):
            data = data[:-2] + b'\n'
        if hashlib.sha256(data).hexdigest() != origins[home][name]:
            raise SystemExit('%s does not match its SHA-256 in origins.tsv' % name)
        text = data.decode('utf-8')
        if not text.endswith('\n') or '\n' in text[:-1]:
            raise SystemExit('%s is not one line and a newline' % name)
        if r['body'] != '0':
            raise SystemExit('%s is on body %s, and the founders are all on body 0' % (name, r['body']))
        out.append('0\t' + text[:-1])
        lineage[str(len(out) - 1)] = r['id'][:-len('-blade')] if r['id'].endswith('-blade') else r['id']
    if len(out) != 20:
        raise SystemExit('pool-mixed.tsv holds %d genomes, not the twenty' % len(out))
    text = '\n'.join(out) + '\n'
    name = hashlib.sha256(text.encode('utf-8')).hexdigest()[:16]
    path = os.path.join(a.root, 'rounds', 'nursery', 'pools', name + '.tsv')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('%s: %d genomes' % (os.path.relpath(path, a.root), len(out)))
    if len(set(lineage.values())) != 10:
        raise SystemExit('the twenty make %d lineages, not ten' % len(set(lineage.values())))
    lin = os.path.join(folder, 'lineage.json')
    with open(lin, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(lineage, f, indent=1)
        f.write('\n')
    print('%s: %d lines, %d lineages' % (os.path.relpath(lin, a.root), len(lineage), len(set(lineage.values()))))


if __name__ == '__main__':
    main()
