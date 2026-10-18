"""D178's founders: the ten direction tops of inocula/r59-direction-tops/ (D174) as a nursery pool, each on body 0, brains and all.

    python scripts/reads/bodies-pool.py <repository root>

Reads the folder's pool.tsv for the order (the ten by their own brain's eating, best first) and origins.tsv for each file's
SHA-256, and refuses a file whose bytes no longer match. Writes rounds/nursery/pools/<hash>.tsv, one line a genome: 0, a tab,
and the genome's JSON exactly as the file holds it, without its newline. The name is the first 16 hex digits of the pool's
SHA-256, as every nursery pool's is (rounds/nursery/pools/README.md). Prints the name.
"""
import argparse
import hashlib
import os

FOLDER = os.path.join('inocula', 'r59-direction-tops')


def rows(path):
    with open(path, encoding='utf-8', newline='') as f:
        lines = [l.rstrip('\n') for l in f if l.strip()]
    head = lines[0].split('\t')
    return [dict(zip(head, l.split('\t'))) for l in lines[1:]]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('root')
    a = ap.parse_args()
    folder = os.path.join(a.root, FOLDER)
    sha = {r['file']: r['sha256'] for r in rows(os.path.join(folder, 'origins.tsv'))}
    out = []
    for r in rows(os.path.join(folder, 'pool.tsv')):
        with open(os.path.join(folder, r['path']), 'rb') as f:
            data = f.read()
        # A Windows checkout may end the line with CRLF; the file as committed, and as origins.tsv hashed it, ends with LF.
        if data.endswith(b'\r\n'):
            data = data[:-2] + b'\n'
        if hashlib.sha256(data).hexdigest() != sha[r['path']]:
            raise SystemExit('%s does not match its SHA-256 in origins.tsv' % r['path'])
        text = data.decode('utf-8')
        if not text.endswith('\n') or '\n' in text[:-1]:
            raise SystemExit('%s is not one line and a newline' % r['path'])
        if r['body'] != '0':
            raise SystemExit('%s is on body %s, and the tops are all on body 0' % (r['path'], r['body']))
        out.append('0\t' + text[:-1])
    text = '\n'.join(out) + '\n'
    name = hashlib.sha256(text.encode('utf-8')).hexdigest()[:16]
    path = os.path.join(a.root, 'rounds', 'nursery', 'pools', name + '.tsv')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('%s: %d genomes' % (os.path.relpath(path, a.root), len(out)))


if __name__ == '__main__':
    main()
