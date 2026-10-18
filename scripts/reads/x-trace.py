"""A migrating group's living, traced home through the ring (a read for round 58's exchange screen).

For each world: the guilds of the genomes it was offered (immigrants.jsonl's guild flags: absorptive 1, jointed 2,
photosynthetic 4), the guild mix of its living at the last positions sample, and every living body's ORIGIN: its
clade's root, followed back through each immigrant's sending world (from, fid) until a founder, so a body is credited
to the world whose founder began its line. Also the largest clades alive at the end, with their chain home.

    python -I scripts/reads/x-trace.py <world 0 run> <world 1 run> <world 2 run>   (in the group's order, as card-group's -Arms)
"""
import collections
import gzip
import json
import os
import sys


def rows(path):
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            if not line.endswith('\n'):
                break
            yield json.loads(line)


def guild(flags):
    return (('A' if flags & 1 else '') + ('J' if flags & 2 else '') + ('P' if flags & 4 else '')) or 'none'


class World:
    def __init__(self, run):
        self.run = run
        self.name = os.path.basename(os.path.dirname(os.path.normpath(run)))
        self.offers = list(rows(os.path.join(run, 'immigrants.jsonl')))
        self.imm = {r['id']: r for r in self.offers if 'why' not in r}
        self.root = {}
        self.alive = set()
        for r in rows(os.path.join(run, 'lineage.jsonl')):
            e = r.get('e')
            if e == 'b':
                i, p = r['id'], r['p']
                self.root[i] = self.root[p] if p >= 0 and p in self.root else i
                self.alive.add(i)
            elif e == 'd':
                self.alive.discard(r['id'])
        last = None
        with gzip.open(os.path.join(run, 'positions.jsonl.gz'), 'rt', encoding='utf-8') as f:
            for line in f:
                if line.endswith('\n'):
                    last = line
        p = json.loads(last)
        self.t = p['t']
        self.flags = {b[0]: b[4] for b in p['b']}


def origin(worlds, w, i, chain):
    """The world index whose founder began body i's line in world w, and the chain of hops."""
    r = worlds[w].root.get(i, i)
    if r in worlds[w].imm:
        row = worlds[w].imm[r]
        chain.append(f"{worlds[w].name} root {r} <- {worlds[row['from']].name} id {row['fid']} at {row['t']:g} s")
        if len(chain) > 50:
            return None
        return origin(worlds, row['from'], row['fid'], chain)
    return w


def main(argv):
    worlds = [World(a) for a in argv[1:]]
    for w, W in enumerate(worlds):
        print(f'== {W.name}: offers {len(W.offers)}, planted {len(W.imm)}; positions at {W.t:g} s, {len(W.flags)} bodies')
        print('   offered guilds:', dict(collections.Counter(guild(r.get('guild', 0)) for r in W.offers)))
        living = list(W.flags)
        mix = collections.Counter(guild(W.flags[i]) for i in living)
        print('   living guilds:', dict(mix))
        homes = collections.Counter()
        for i in living:
            o = origin(worlds, w, i, [])
            homes[worlds[o].name if o is not None else '?'] += 1
        print('   living by origin world:', ', '.join(f'{k} {v} ({100 * v / len(living):.1f}%)' for k, v in homes.most_common()))
        clades = collections.Counter(W.root.get(i, i) for i in living)
        for r, n in clades.most_common(3):
            chain = []
            o = origin(worlds, w, r, chain)
            g = collections.Counter(guild(W.flags[i]) for i in living if W.root.get(i, i) == r)
            kind = 'immigrant' if r in W.imm else 'founder'
            print(f'   clade {r} ({kind}): {n} alive, guilds {dict(g)}, origin {worlds[o].name if o is not None else "?"}')
            for c in chain:
                print(f'      {c}')


if __name__ == '__main__':
    main(sys.argv)
