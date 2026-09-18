# -*- coding: utf-8 -*-
"""E9's two-sided reading: the crowd's centre along the tilt over the whole run, so the
entry can say whether the deep bias is there from founding or grows.

s > 0 is toward the shallow arc. The null is the centre a crowd spread evenly through the
WATER would have, which is negative because the deep side holds more water.
"""
import json, math, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, HERE)
import bed as bedmod

RUNS = {'r39-s1': '2026-09-15-213428-8eab1085', 'r39-s2': '2026-09-15-213503-8eab1085',
        'r39-s3': '2026-09-17-085758-8eab1085', 'r39-s4': '2026-09-17-085833-8eab1085',
        'r39-s5': '2026-09-17-085303-8eab1085'}
WANT = list(range(1000, 31000, 1000))
PHOTO, ABS = 4, 1

out = ['## The crowd\'s median position along the tilt at every 1,000 s. s > 0 is toward'
       ' the shallow arc; the null (a crowd spread evenly through the water) is about'
       ' -2.22 m in every seed.', 'arm\tguild\t' + '\t'.join(str(t) for t in WANT)]
for a, rd in RUNS.items():
    d = os.path.join(ROOT, 'runs', a, rd)
    cfg = json.load(open(os.path.join(d, 'config.json'), encoding='utf-8'))
    man = json.load(open(os.path.join(d, 'run.json'), encoding='utf-8'))
    b = bedmod.from_config(cfg, man['seed'])
    R = float(b.RadiusMetres)
    ux, uz = b.tilt_unit()
    got = {}
    with open(os.path.join(d, 'positions.jsonl'), encoding='utf-8') as f:
        for line in f:
            if not line.startswith('{"t":'):
                continue
            t = int(line[5:line.index(',', 5)])
            if t not in WANT:
                continue
            row = json.loads(line)
            arr = np.array([[x[1], x[3], x[4]] for x in row['b']], dtype=float)
            s = (arr[:, 0] - R) * ux + (arr[:, 1] - R) * uz
            fl = arr[:, 2].astype(int)
            got[t] = (float(np.median(s)),
                      float(np.median(s[(fl & PHOTO) != 0])) if ((fl & PHOTO) != 0).any() else None,
                      float(np.median(s[(fl & ABS) != 0])) if ((fl & ABS) != 0).any() else None)
    for i, name in enumerate(('all', 'leaf', 'stomach')):
        out.append('%s\t%s\t%s' % (a, name, '\t'.join(
            ('%.2f' % got[t][i]) if t in got and got[t][i] is not None else '-'
            for t in WANT)))

with open(os.path.join(HERE, 'tilt-course.tsv'), 'w', encoding='utf-8') as f:
    f.write('\n'.join(out) + '\n')
print('wrote tilt-course.tsv')

# ---------------------------------------------------------------- the light on the shelf
# E9's second branch: "the shallow floor is no better lit than the band they float in".
# The light falls as exp(-depth / attenuationDepth) with attenuationDepth from the config.
extra = ['', '## E9\'s second branch, the light. Irradiance goes as exp(-depth / L) with L'
         ' the config\'s attenuationDepth. The comparison is the leaves\' own median depth'
         ' at 15,000 s against the shallowest floor the seed\'s map has.',
         'arm\tL m\tleaf median depth m at 15000\tshallowest floor m\tlight in the leaf band'
         '\tlight on the shallowest floor\tband / shelf']
LEAFY = {'r39-s1': -18.66, 'r39-s2': -15.94, 'r39-s3': -16.08,
         'r39-s4': -16.54, 'r39-s5': -20.08}      # from depth-band.tsv, 15,000 s
for a, rd in RUNS.items():
    d = os.path.join(ROOT, 'runs', a, rd)
    cfg = json.load(open(os.path.join(d, 'config.json'), encoding='utf-8'))
    man = json.load(open(os.path.join(d, 'run.json'), encoding='utf-8'))
    b = bedmod.from_config(cfg, man['seed'])
    L = cfg['light']['attenuationDepth']
    R = float(b.RadiusMetres)
    n = int(math.ceil(2 * R))
    g = np.arange(n) + 0.5
    X, Z = np.meshgrid(g, g, indexing='ij')
    ins = ((X - R) ** 2 + (Z - R) ** 2) <= R * R
    top = float(b.floor_y(X[ins], Z[ins]).max())
    y = LEAFY[a]
    a1, a2 = math.exp(y / L), math.exp(top / L)
    extra.append('%s\t%g\t%.2f\t%.3f\t%.4f\t%.4f\t%.2f' % (a, L, y, top, a1, a2, a1 / a2))
with open(os.path.join(HERE, 'tilt-course.tsv'), 'a', encoding='utf-8') as f:
    f.write('\n'.join(extra) + '\n')
print('appended the light block')
