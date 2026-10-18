"""The grazer control's kill and reach read: one grazer run against r58-s1ir, beside r58g-read.py.

Usage: python -I r58g-reach.py <grazer run dir> <r58-s1ir run dir> [--every N]

For each kill row whose attacker is of the inoculants' line:
  the victim's birth row (guild flags abs, pho, jnt, and its kind), root lost or a part, tissue and reserve lost;
  the killer's and the victim's centres at the last positions sample at or before the kill, and their distance;
  the corpse's reach, rule 6's: the radius of a sphere holding tj + rj joules at the tissue price, plus
  intakeReachMetres (World.cs Bury: remains = tissue + reserve, plus any gestation account, which a kill row does
  not carry, so the reach is a floor; how much of the remains becomes the corpse is not re-read here);
  the killer's distance from that point at the following samples, while the killer lives.
Then, per sample (every Nth): the line alive, the world alive and leaves (positions-read's guild rule), and
unitsEaten and corpsesEaten, the run against r58-s1ir at the same t.
The record keeps no per-eater meal row (WorldMouth.ApplyIntake adds only to the world's totals), so who ate a corpse
is not in it; a line body alive well past its unfed life is the only per-body sign of eating.
"""
import argparse
import importlib.util
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'scripts', 'reads'))
import runrec  # noqa: E402

_spec = importlib.util.spec_from_file_location('positions_read', os.path.join(ROOT, 'scripts', 'positions-read.py'))
_pr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_pr)


def rows(path):
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def samples(run):
    out = []
    for line in runrec.positions_lines(run):
        line = line.strip()
        if not line:
            continue
        r = json.loads(line)
        out.append((r['t'], {b[0]: (b[1], b[2], b[3], b[4]) for b in r['b']}))
    return out


def find(config, name):
    if isinstance(config, dict):
        for k, v in config.items():
            if k == name:
                return v
            got = find(v, name)
            if got is not None:
                return got
    return None


def dist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('run')
    ap.add_argument('ref')
    ap.add_argument('--every', type=int, default=10)
    a = ap.parse_args()

    config = json.load(open(os.path.join(a.run, 'config.json'), encoding='utf-8'))
    reach = float(find(config, 'intakeReachMetres'))
    lineage = list(rows(os.path.join(a.run, 'lineage.jsonl')))
    births = {r['id']: r for r in lineage if r.get('e') == 'b'}
    line = {r['id'] for r in births.values() if r.get('k') == 'i'}
    for r in sorted(births.values(), key=lambda r: r['t']):
        if r.get('p') in line:
            line.add(r['id'])
    died = {r['id']: r for r in lineage if r.get('e') == 'd'}
    # Structural tissue's price: the config's cell types are not parsed here; round 58's tissue is 500 J/m3
    # (the report's "tissue 500 J/m3"), stated rather than read.
    tissue = 500.0
    pos = samples(a.run)
    times = [t for t, _ in pos]
    print(f'run {a.run}; intakeReachMetres {reach}; tissue {tissue} J/m3 (stated); {len(pos)} samples')

    kills = [r for r in lineage if r.get('e') == 'k' and r.get('by') in line]
    print(f'== kills by the line: {len(kills)} (of {sum(1 for r in lineage if r.get("e") == "k")} kill rows in the run)')
    for k in kills:
        v = births.get(k['id'], {})
        kind = 'inoculant line' if k['id'] in line else (
            'leaf' if v.get('pho') == 1 and v.get('abs') == 0 else f"abs {v.get('abs')} pho {v.get('pho')} jnt {v.get('jnt')}")
        j = (k.get('tj') or 0) + (k.get('rj') or 0)
        r_corpse = (3 * (j / tissue) / (4 * math.pi)) ** (1 / 3) if j > 0 else 0.0
        print(f"  t {k['t']}: {k['by']} killed {k['id']} ({kind}), root {k.get('root')}, parts {k.get('parts')}, "
              f"part {k.get('part')} byPart {k.get('byPart')}, tissue {k.get('tj'):.2f} J, reserve {k.get('rj'):.2f} J; "
              f"corpse reach {r_corpse:.2f} + {reach} = {r_corpse + reach:.2f} m; victim died "
              f"{'t ' + str(died[k['id']]['t']) + ' of ' + str(died[k['id']].get('c')) if k['id'] in died else 'not'}")
        before = [i for i, t in enumerate(times) if t <= k['t']]
        if not before:
            print('    no sample before the kill')
            continue
        i = before[-1]
        t0, bodies = pos[i]
        kp, vp = bodies.get(k['by']), bodies.get(k['id'])
        if kp and vp:
            print(f'    at sample t {t0}: killer ({kp[0]:.2f}, {kp[1]:.2f}, {kp[2]:.2f}), victim ({vp[0]:.2f}, {vp[1]:.2f}, '
                  f'{vp[2]:.2f}), centres {dist(kp, vp):.2f} m apart')
            trail = []
            for t1, b1 in pos[i + 1:i + 7]:
                if k['by'] not in b1:
                    trail.append(f't {t1}: killer gone')
                    break
                trail.append(f't {t1}: {dist(b1[k["by"]], vp):.2f}')
            print('    killer from the victim\'s last centre: ' + '; '.join(trail))
        else:
            print(f'    at sample t {t0}: killer {"present" if kp else "absent"}, victim {"present" if vp else "absent"}')

    print('== the line\'s deaths')
    deaths = sorted((r for r in died.values() if r['id'] in line), key=lambda r: r['t'])
    for r in deaths:
        b = births[r['id']]
        print(f"  id {r['id']} ({'inoculant' if b.get('k') == 'i' else 'child of ' + str(b.get('p'))}) died t {r['t']} "
              f"of {r.get('c')}, age {r['t'] - b['t']:.1f} s")
    print(f'  {len(deaths)} deaths; {len(line) - len(deaths)} of {len(line)} alive at the end; '
          f'{len(line) - sum(1 for r in births.values() if r.get("k") == "i")} born of the line')

    print('== per sample: line alive | world alive run/ref | leaves run/ref | unitsEaten run/ref | corpsesEaten run/ref')
    ref_pos = {t: b for t, b in samples(a.ref) if t >= times[0] - 1}
    ref_stats = {r['t']: r for r in rows(os.path.join(a.ref, 'stats.jsonl')) if r['t'] >= times[0] - 1}
    run_stats = {r['t']: r for r in rows(os.path.join(a.run, 'stats.jsonl'))}
    for n, (t, bodies) in enumerate(pos):
        if n % a.every and n != len(pos) - 1:
            continue
        rb = ref_pos.get(t, {})
        leaves = sum(1 for v in bodies.values() if _pr.guild_of(v[3]) == 'leaf')
        rleaves = sum(1 for v in rb.values() if _pr.guild_of(v[3]) == 'leaf')
        s, q = run_stats.get(t, {}), ref_stats.get(t, {})
        print(f"  t {t}: line {sum(1 for i in bodies if i in line)} | {len(bodies)}/{len(rb)} | {leaves}/{rleaves} | "
              f"{s.get('unitsEaten', float('nan')):.3f}/{q.get('unitsEaten', float('nan')):.3f} | "
              f"{s.get('corpsesEaten')}/{q.get('corpsesEaten')}")


if __name__ == '__main__':
    main()
