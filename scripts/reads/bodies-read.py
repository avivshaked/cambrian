"""D178's arm, generation by generation: what the body search did to the body, and where the net energy came from.

    python scripts/reads/bodies-read.py <run folder>

Reads tank-best.jsonl (each generation's top genome), tank-scores.jsonl (every member's reward and its parts) and
tank-generations.jsonl (the row the search wrote, its stop reading included). Prints one line a generation:
- the population's mean score (each member's running mean, as selection ranks it) and its parts (eaten, upkeep, work, handling: NetEnergy's, the costs negative;
  under EnergyReturn the score is a ratio, printed to three places, and its one cost, spent, stands in the upkeep column), and the stop's smoothed
  reading and gain;
- the top's score and its body: the number of nodes, the root's full size in metres (twice its half-extents), the
  root's volume in litres, the largest attack and intake on any node, and the adult scale.
The body numbers are the genome's, not the grown body's: the adult scale multiplies every length when it grows.
"""
import argparse
import json
import os
import statistics


def rows(path):
    with open(path, encoding='utf-8') as f:
        return [json.loads(l) for l in f if l.strip()]


def mean(xs):
    xs = [x for x in xs if x is not None]
    return statistics.mean(xs) if xs else float('nan')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('run')
    a = ap.parse_args()
    best = rows(os.path.join(a.run, 'tank-best.jsonl'))
    scores = {r['gen']: r for r in rows(os.path.join(a.run, 'tank-scores.jsonl'))}
    gens = rows(os.path.join(a.run, 'tank-generations.jsonl'))
    print('gen   score   eaten  upkeep    work  handl | smoothed   gain | top score nodes  root size (m)       litres  attack intake scale')
    for g, top in zip(gens, best):
        members = scores[g['gen']]['members']
        part = {k: mean(m['parts'].get(k) for m in members) for k in ('eaten', 'upkeep', 'work', 'handling', 'spent')}
        # EnergyReturn's parts are eaten and spent; its spent goes in the upkeep column, as a cost, and work and handling are blank.
        if 'spent' in members[0]['parts']:
            part['upkeep'] = -part['spent']
        value = mean(m['value'] for m in members)
        stop = g.get('stop') or {}
        root = top['nodes'][top['root']]
        d = root['dimensions']
        size = (2 * d['x'], 2 * d['y'], 2 * d['z'])
        litres = size[0] * size[1] * size[2] * 1000
        attack = max(n['attack'] for n in top['nodes'])
        intake = max(n['intake'] for n in top['nodes'])
        sm, gn = stop.get('smoothed'), stop.get('gain')
        ratio = 'spent' in members[0]['parts']
        print(('%3d %7.3f' if ratio else '%3d %7.1f') % (g['gen'], value) + ' %7.1f %7.1f %7.1f %6.1f | %8s %6s | %9s %5d  %.2f x %.2f x %.2f  %7.1f  %6.2f %6.2f %5.2f' % ( part['eaten'], part['upkeep'], part['work'], part['handling'],
            '%.1f' % sm if sm is not None else '-', '%.3f' % gn if gn is not None else '-',
            ('%.3f' if ratio else '%.1f') % g['top']['score'], len(top['nodes']), size[0], size[1], size[2], litres, attack, intake, top['adultScale']))


if __name__ == '__main__':
    main()
