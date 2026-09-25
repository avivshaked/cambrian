# -*- coding: utf-8 -*-
"""E2's two-sided reading, as far as the map alone can take it.

0102 says that if E2 fails the entry "reads the streams' speed over a hollow against over a
ridge from the field itself (Core, from the config) before saying the relief is too small".
That measurement is `CurrentField`'s, and it needs a Core probe (a console program against
Evosim.Core, as `logbook/specs/transport-conserves-probe/Program.cs` was) rather than anything in the run's
output — the report carries no per-cell velocity.  This read did not build one, and says so.

What the map alone gives exactly is the squeeze: the streams are the flat field pulled
through a floor-following map, so the water thins over a rise and thickens over a hollow in
proportion to the column's own water depth (bed-spec.md item 6).  The ratio of a hollow's
column to its ring's, and of a ridge's to its ring's, therefore bounds the speed difference
the pullback can produce there.  Everything here is arithmetic on the rebuilt map.

Writes relief-squeeze.tsv beside this file.
"""
import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, HERE)
import bed as bedmod

RUNS = {
    'r39-s1': '2026-09-15-213428-8eab1085',
    'r39-s2': '2026-09-15-213503-8eab1085',
    'r39-s3': '2026-09-17-085758-8eab1085',
    'r39-s4': '2026-09-17-085833-8eab1085',
    'r39-s5': '2026-09-17-085303-8eab1085',
}
ARMS = ['r39-s%d' % i for i in range(1, 6)]

out = ['## The floor-following map\'s squeeze at each place the map calls a hollow or a'
       ' ridge. "ring" is the annulus from 0.8 to 1.0 of the hollow radius (4.41 m), the'
       ' same ring BedShape.CountPlaces judges a place by. water depth = -FloorY. The last'
       ' column is the most the pullback can quicken or slow the water there, since a'
       ' sigma map carries the column\'s thickness and nothing else about the place.',
       'arm\tkind\tx\tz\tfloor y m\twater depth m\tring mean water depth m'
       '\tdepth / ring depth\tspeed change if the flux is held (1/ratio - 1)']
for a in ARMS:
    d = os.path.join(ROOT, 'runs', a, RUNS[a])
    cfg = json.load(open(os.path.join(d, 'config.json'), encoding='utf-8'))
    man = json.load(open(os.path.join(d, 'run.json'), encoding='utf-8'))
    b = bedmod.from_config(cfg, man['seed'])
    r = b.HollowRadiusMetres
    ang = np.linspace(0, 2 * math.pi, 64, endpoint=False)
    rr = np.linspace(0.8 * r, r, 5)
    places = ([('hollow', p) for p in b.HollowCentres] +
              [('ridge', p) for p in b.RidgeCentres])
    if not places:
        out.append('%s\t(no hollow and no ridge)\t-\t-\t-\t-\t-\t-\t-' % a)
        continue
    for kind, (px, pz) in places:
        fy = float(b.floor_y(px, pz))
        rx = px + np.outer(rr, np.cos(ang)).ravel()
        rz = pz + np.outer(rr, np.sin(ang)).ravel()
        ring = float(np.mean(-b.floor_y(rx, rz)))
        here = -fy
        ratio = here / ring
        out.append('%s\t%s\t%.2f\t%.2f\t%.3f\t%.3f\t%.3f\t%.4f\t%+.2f%%' % (
            a, kind, px, pz, fy, here, ring, ratio, 100.0 * (1.0 / ratio - 1.0)))

out += ['', '## the scale of the thing: the bands\' whole range is 1.50 m in every seed,'
        ' and the water column over the disc is 29.8 to 60.7 m deep, so no column can be'
        ' squeezed by more than about 5% however the bands fall. The 30 m tilt is much'
        ' larger but it is a ramp, not a place: it changes the column\'s thickness'
        ' monotonically across the disc rather than making a pocket.',
        'arm\tband range m\tshallowest floor m\tdeepest floor m\tlargest possible squeeze']
for a in ARMS:
    d = os.path.join(ROOT, 'runs', a, RUNS[a])
    cfg = json.load(open(os.path.join(d, 'config.json'), encoding='utf-8'))
    man = json.load(open(os.path.join(d, 'run.json'), encoding='utf-8'))
    b = bedmod.from_config(cfg, man['seed'])
    R = float(b.RadiusMetres)
    n = int(math.ceil(2 * R))
    g = np.arange(n) + 0.5
    X, Z = np.meshgrid(g, g, indexing='ij')
    ins = ((X - R) ** 2 + (Z - R) ** 2) <= R * R
    fl = b.floor_y(X[ins], Z[ins])
    out.append('%s\t%.2f\t%.3f\t%.3f\t%.2f%%' % (
        a, b.RangeMetres, fl.max(), fl.min(),
        100.0 * b.RangeMetres / (-fl.max())))

with open(os.path.join(HERE, 'relief-squeeze.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('\n'.join(out[-8:]))
