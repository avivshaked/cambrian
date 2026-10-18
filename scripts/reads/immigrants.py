"""Round 58's exchange, read from each world's own record (D159): what each world was sent, what it planted,
and how much of it is the immigrants' descendants as the run goes on.

    python scripts/reads/immigrants.py <run dir> [<run dir> ...] [--every 1000] [--split 3000]

Each argument is one world's run directory, the folder holding run.json; a migrating group's worlds are read
in the order given. A world that is not migrating (no migrateEverySeconds in run.json) is read too, as the
isolated arm's control, and shows no offers.

Per world, from run.json: its place in the group, whom it sends to and hears from, the cadence and count.
From immigrants.jsonl: the offers, how many were planted and how many refused, by the world they came from,
and the refusals by reason ("floor", "room", "stillborn"; a record written before the reason was kept has
none, read as "?") before and after --split seconds.
From lineage.jsonl: every planted immigrant is a birth of kind "m" with no parent, so it roots a clade of its
own in this world, as a founder does; each later birth belongs to its parent's root. The table then gives, at
every --every seconds and at the end: the living, the living in an immigrant's clade, that share, and the
immigrant clades with a member alive. The last line names the largest immigrant clade alive at the end.

A clade here is a world's own: an immigrant's descendants in the world it was sent to. Its ancestry in its
home world is the row's "from" and "fid", which this reader prints for the largest only.
"""
import json
import os
import sys


def rows(path):
    """Every complete row of a JSONL file, skipping a last line a live writer has not finished."""
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            if not line.endswith('\n'):
                break
            yield json.loads(line)


def world(run, every, split):
    manifest = json.load(open(os.path.join(run, 'run.json'), encoding='utf-8'))
    name = os.path.basename(os.path.dirname(os.path.normpath(run))) or run
    migrating = 'migrateEverySeconds' in manifest
    print(f'== {name} ({run})')
    if migrating:
        print(f'   place {manifest["groupPlace"]}, sends to {manifest["migrateTo"]}, hears from '
              f'{manifest["migrateFrom"]}, every {manifest["migrateEverySeconds"]:g} s, '
              f'{manifest["migrateCount"]} a time')
    else:
        print('   not migrating (no exchange in run.json)')

    record = os.path.join(run, 'immigrants.jsonl')
    offers, planted, refused, by_home = 0, 0, 0, {}
    why = {}  # (before split, reason) -> count
    if os.path.exists(record):
        for r in rows(record):
            offers += 1
            home = by_home.setdefault(r['from'], [0, 0])
            if r['id'] >= 0:
                planted += 1
                home[0] += 1
            else:
                refused += 1
                home[1] += 1
                key = (r['t'] <= split, r.get('why', '?'))
                why[key] = why.get(key, 0) + 1
    if migrating or offers:
        homes = ', '.join(f'from {h}: {p} planted, {x} refused' for h, (p, x) in sorted(by_home.items()))
        print(f'   offers {offers}, planted {planted}, refused {refused}' + (f' ({homes})' if homes else ''))
        if refused:
            for before, label in ((True, f'to {split:g} s'), (False, f'after {split:g} s')):
                parts = ', '.join(f'{w} {n}' for (b_, w), n in sorted(why.items()) if b_ == before)
                print(f'   refused {label}: {parts or "none"}')

    root = {}          # id -> the id of its clade's root
    imm = {}           # an immigrant root's id -> (t, from, fid)
    alive_in = {}      # root -> members alive
    alive = set()
    in_imm = 0         # the living in an immigrant's clade
    table = []
    next_at = every
    births_m = 0
    t = 0.0

    def mark(at):
        living_imm = sum(1 for r, n in alive_in.items() if n > 0 and r in imm)
        share = in_imm / len(alive) if alive else 0.0
        table.append((at, len(alive), in_imm, share, living_imm))

    for r in rows(os.path.join(run, 'lineage.jsonl')):
        e = r.get('e')
        if e not in ('b', 'd'):
            continue
        t = r['t']
        while t > next_at:
            mark(next_at)
            next_at += every
        i = r['id']
        if e == 'b':
            p = r.get('p', -1)
            if p >= 0 and p in root:
                root[i] = root[p]
            else:
                root[i] = i
                if r.get('k') == 'm':
                    imm[i] = (t, r.get('from'), r.get('fid'))
                    births_m += 1
            alive.add(i)
            alive_in[root[i]] = alive_in.get(root[i], 0) + 1
            if root[i] in imm:
                in_imm += 1
        elif i in alive:
            alive.discard(i)
            alive_in[root[i]] -= 1
            if root[i] in imm:
                in_imm -= 1
    if not table or t > table[-1][0]:
        mark(t)

    if births_m != planted:
        print(f'   NOTE: lineage holds {births_m} immigrant births and immigrants.jsonl {planted} planted')
    print(f'   {"t":>8} {"alive":>7} {"in imm.":>8} {"share":>6} {"imm. clades":>11}')
    for at, n, k, share, c in table:
        print(f'   {at:>8g} {n:>7} {k:>8} {share:>6.1%} {c:>11}')
    best = max(((n, r) for r, n in alive_in.items() if r in imm and n > 0), default=None)
    if best:
        n, r = best
        bt, home, fid = imm[r]
        print(f'   largest immigrant clade alive at the end: root {r}, planted at {bt:g} s from world {home} '
              f'(id {fid} there), {n} alive')
    else:
        print('   no immigrant clade alive at the end')


def main(argv):
    every = 1000.0
    split = 3000.0
    runs = []
    i = 0
    while i < len(argv):
        if argv[i] == '--split':
            split = float(argv[i + 1])
            i += 2
        elif argv[i] == '--every':
            every = float(argv[i + 1])
            i += 2
        else:
            runs.append(argv[i])
            i += 1
    if not runs:
        print(__doc__)
        return 2
    for run in runs:
        world(run, every, split)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
