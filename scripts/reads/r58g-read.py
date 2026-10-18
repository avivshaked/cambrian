"""The grazer control's read (logbook/specs/grazer-control-spec.md): one grazer run against r58-s1ir.

Usage: python -I r58g-read.py <grazer run dir> <r58-s1ir run dir> [--every N]

Prints, for the run's inoculants ("k":"i" birth rows) and their line (every birth whose parent is in it):
  admitted: the inoculant rows, their time, attack and intake flags, against the 20 asked for;
  deaths: every death of the line, by cause and age;
  kills: every kill row ("e":"k") in the run, split by whether the attacker is of the line, with parts and joules;
  stats: the mouth, module and admission counters at each sample, the run against r58-s1ir at the same t;
  positions: each sample's inoculant-line bodies, x, y, z and flags, and the leaves' mean y beside them.
Reads the files only; writes nothing.
"""
import argparse
import json
import os
import statistics
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'scripts', 'reads'))
import runrec  # noqa: E402

COUNTERS = ['alive', 'stillbirths', 'selfOverlapStillbirths', 'partsKilled', 'bodiesEaten', 'corpsesFromKills',
            'corpsesEaten', 'unitsEaten', 'corpses', 'moduleAdds', 'moduleAddsRefused', 'moduleAddsRefusedForReserve',
            'moduleAddsRefusedForShape', 'modulesStanding', 'moduleRebuilds']


def rows(path):
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('run')
    ap.add_argument('ref')
    ap.add_argument('--every', type=int, default=1, help='print every Nth stats and positions sample')
    a = ap.parse_args()

    lineage = list(rows(os.path.join(a.run, 'lineage.jsonl')))
    births = [r for r in lineage if r.get('e') == 'b']
    first_t = min(r['t'] for r in lineage) if lineage else None
    inoc = [r for r in births if r.get('k') == 'i']
    line = {r['id'] for r in inoc}
    born = {r['id']: r for r in births}
    for r in births:  # rows are in time order, so a parent precedes its child
        if r.get('p') in line:
            line.add(r['id'])
    print(f'run {a.run}')
    print(f'lineage rows {len(lineage)}, from t {first_t}')
    print(f'== admitted: {len(inoc)} inoculant rows against 20 asked')
    for r in inoc:
        print(f"  id {r['id']} t {r['t']} atk {r.get('atk')} ink {r.get('ink')} abs {r.get('abs')} pho {r.get('pho')} "
              f"jnt {r.get('jnt')} bf {r.get('bf')} as {r.get('as')}")
    kids = [r for r in births if r['id'] in line and r.get('k') != 'i']
    print(f'== the line: {len(line)} bodies, {len(kids)} born of it')
    for r in kids:
        print(f"  child id {r['id']} of {r['p']} at t {r['t']}, kind {r.get('k')}, atk {r.get('atk')} ink {r.get('ink')}")

    print('== deaths of the line')
    deaths = [r for r in lineage if r.get('e') == 'd' and r['id'] in line]
    for r in deaths:
        b = born.get(r['id'])
        age = r['t'] - b['t'] if b else None
        print(f"  id {r['id']} died t {r['t']} of {r.get('c')}, age {age}, reserve {r.get('res')}")
    by_cause = {}
    for r in deaths:
        by_cause[r.get('c')] = by_cause.get(r.get('c'), 0) + 1
    print(f'  {len(deaths)} deaths: {by_cause}; {len(line) - len(deaths)} of the line alive at the end')

    print('== kill rows')
    kills = [r for r in lineage if r.get('e') == 'k']
    ours = [r for r in kills if r.get('by') in line]
    print(f'  {len(kills)} in the run, {len(ours)} by the line')
    for r in kills:
        print(f"  t {r['t']} id {r['id']} by {r.get('by')} ({'line' if r.get('by') in line else 'other'}) root {r.get('root')} "
              f"parts {r.get('parts')} tj {r.get('tj')} rj {r.get('rj')} part {r.get('part')} byPart {r.get('byPart')}")

    print('== stats: run | r58-s1ir at the same t')
    ref = {r['t']: r for r in rows(os.path.join(a.ref, 'stats.jsonl')) if first_t is None or r['t'] >= first_t - 10}
    run_stats = list(rows(os.path.join(a.run, 'stats.jsonl')))
    present = [c for c in COUNTERS if run_stats and c in run_stats[0]]
    absent = [c for c in COUNTERS if c not in present]
    if absent:
        print(f'  not in this build\'s rows: {", ".join(absent)}')
    for i, r in enumerate(run_stats):
        if i % a.every and i != len(run_stats) - 1:
            continue
        q = ref.get(r['t'], {})
        print(f"  t {r['t']}: " + '; '.join(f"{c} {r.get(c)}|{q.get(c)}" for c in present))

    print('== positions: the line, and the leaves\' mean y')
    for i, (t, text) in enumerate((t, l) for t, l in ((json.loads(l)['t'], l) for l in runrec.positions_lines(a.run) if l.strip())):
        if i % a.every:
            continue
        row = json.loads(text)
        bodies = row['b']
        leaf_y = [b[2] for b in bodies if _leaf(b[4])]
        mine = [b for b in bodies if b[0] in line]
        lm = f'{statistics.mean(leaf_y):.2f}' if leaf_y else '-'
        print(f"  t {t}: {len(mine)} of the line among {len(bodies)}; leaves {len(leaf_y)} at mean y {lm}")
        for b in mine:
            print(f'    id {b[0]} x {b[1]:.2f} y {b[2]:.2f} z {b[3]:.2f} flags {b[4]}')


def _leaf(flags):
    # positions-read.py's guilds, by its own guild_of: leaf is photosynthetic and not absorptive (P only).
    return _guild(flags) == 'leaf'


_GUILD = None


def _guild(flags):
    global _GUILD
    if _GUILD is None:
        import importlib.util
        spec = importlib.util.spec_from_file_location(
            'positions_read', os.path.join(ROOT, 'scripts', 'positions-read.py'))
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        _GUILD = mod.guild_of
    return _GUILD(flags)


if __name__ == '__main__':
    main()
