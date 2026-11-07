"""D325's height read: whether gripping plants stand taller with light at each part, from the height census.

    python -I logbook/specs/plants-hold/height-read/height-read.py <census folder>

The census folder holds one JSONL file per run and stretch, written by the farm's --height-census
(height-census dfbdab27): plp-<seed>a/b.jsonl for the screen (light and matter at each part, plp-* then
its resume plp2-*), and pldr-<seed>[a/b].jsonl for its controls. Rows of a world's files are joined by
second. For each world and second it prints, over gripping plants (plant and grips both true): the count,
the median parts, the median rise of the highest part centre over the root (max_y - root_y), its 90th
percentile, the median rise of the highest photosynthetic part, the share that rise 0.5 m or more, and the
median depth of the highest part centre (-max_y). Every y is world metres, negative below the surface.
"""
import glob
import json
import os
import re
import sys
from collections import defaultdict


def median(xs):
    xs = sorted(xs)
    n = len(xs)
    if n == 0:
        return None
    return xs[n // 2] if n % 2 else 0.5 * (xs[n // 2 - 1] + xs[n // 2])


def pct(xs, p):
    xs = sorted(xs)
    if not xs:
        return None
    return xs[min(len(xs) - 1, int(p * len(xs)))]


def fmt(v, f='%.2f'):
    return '-' if v is None else f % v


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2
    rows = defaultdict(lambda: defaultdict(list))
    for path in sorted(glob.glob(os.path.join(argv[1], '*.jsonl'))):
        m = re.match(r'(plp|pldr)-(\d+)[ab]?\.jsonl$', os.path.basename(path))
        if not m:
            continue
        world = '%s-%s' % (m.group(1), m.group(2))
        with open(path, encoding='utf-8') as f:
            for line in f:
                if line.strip():
                    r = json.loads(line)
                    rows[world][r['t']].append(r)
    print('world      t      grip  parts  rise   rise90  photo  >=0.5m  top depth')
    for world in sorted(rows, key=lambda w: (w.split('-')[1], w.split('-')[0])):
        for t in sorted(rows[world]):
            g = [r for r in rows[world][t] if r['plant'] and r['grips']]
            rise = [r['max_y'] - r['root_y'] for r in g]
            photo = [r['photo_max_y'] - r['root_y'] for r in g if r['photo_max_y'] is not None]
            tall = sum(1 for x in rise if x >= 0.5)
            print('%-9s %6d %6d %6s %6s %7s %6s %7s %9s' % (
                world, t, len(g), fmt(median([r['parts'] for r in g]), '%.1f'), fmt(median(rise)),
                fmt(pct(rise, 0.9)), fmt(median(photo)), fmt(100.0 * tall / len(g) if g else None, '%.0f%%'),
                fmt(median([-r['max_y'] for r in g]), '%.1f')))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
