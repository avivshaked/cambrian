# -*- coding: utf-8 -*-
"""D7's anatomy: every diverged dump of round 38, read from the dump itself rather than
from the `diverged` column. Two kinds appear and the brief's clause only fits one of them,
so the kind is the first column: a non-finite blow-up (the dump's `firstNonFiniteStep` is a
real step and CheckFinite's test fails on its partStates) or the tank's radius guard (every
number finite, the root more than R + 1 m from the axis; Ecosystem.cs's own comment says it
should never fire).
"""
import glob, json, math, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
A = ['r38-s%d' % i for i in range(1, 6)]


def run_dir(a):
    return os.path.dirname(sorted(glob.glob(os.path.join(ROOT, 'runs', a, '*', 'run.json')))[-1])


def finite(v):
    return all(isinstance(v[k], (int, float)) and math.isfinite(v[k]) for k in ('x', 'y', 'z'))


out = ['## D7  every diverged dump of round 38, anatomy from the dump file. R = 11.2838 m'
       ' (400 m2), so the radius guard fires past 12.2838 m from the axis. "kind" is derived:'
       ' non-finite if any partState position is not finite, radius guard if every number is'
       ' finite and the root sits past R + 1.',
       'arm\tid\tkind\tt\tage s\tparts\tjointed\tdof\tmaxJointMassRatio\tmass min kg'
       '\tmass max kg\tmass ratio\tcellTypes\troot y\troot r at dump (m)\tr - (R+1)'
       '\troot r last intact (m)'
       '\tlastResizeStep\tsteps since resize\tfirstNonFiniteStep\tsteps NF to dump'
       '\tframesHeld\tframesFinite\ttrace file\tfinite frame before first NF']
R = math.sqrt(400.0 / math.pi)
rows = []
for a in A:
    dd = os.path.join(run_dir(a), 'diverged')
    if not os.path.isdir(dd):
        continue
    for p in sorted(glob.glob(os.path.join(dd, '*.json'))):
        if p.endswith('-trace.json'):
            continue
        d = json.load(open(p, encoding='utf-8'))
        tp = p[:-5] + '-trace.json'
        tr = json.load(open(tp, encoding='utf-8')) if os.path.exists(tp) else None
        ps = d['partStates']
        masses = [q['massKg'] for q in ps]
        nonfin = any(not finite(q['position']) for q in ps)
        rp = d['lastRootPosition']
        # The guard's own test is on the CURRENT root position; lastRootPosition is the last
        # one the check saw intact, which for a body that drifted through the glass is a
        # fraction of a millimetre inside the bound. Read both.
        root = next(q for q in ps if q['parentIndex'] == -1)['position']
        rr = (math.hypot(root['x'] - R, root['z'] - R) if finite(root)
              else math.hypot(rp['x'] - R, rp['z'] - R))
        rr_last = math.hypot(rp['x'] - R, rp['z'] - R)
        kind = 'non-finite' if nonfin else ('radius guard' if rr > R + 1 - 1e-3 else 'other')
        pre = '-'
        if tr:
            fnf = tr.get('firstNonFiniteStep', -1)
            steps = [f.get('step') for f in tr.get('frames', [])]
            pre = 'yes' if (fnf is not None and fnf >= 0 and steps and
                            all(s is not None and s < fnf for s in steps)) else 'no'
        rows.append((a, d, tr, kind, masses, rr, pre, os.path.basename(tp) if tr else '-'))
        out.append('\t'.join(str(x) for x in [
            a, d['creatureId'], kind, d['t'], d['ageSeconds'], d['parts'], d['jointed'],
            d['totalDof'],
            '%.4f' % tr['maxJointMassRatio'] if tr else '-',
            '%.4f' % min(masses), '%.4f' % max(masses),
            '%.3f' % (max(masses) / min(masses)) if min(masses) > 0 else '-',
            '|'.join(sorted({q['cellTypeId'] for q in ps})),
            '%.3f' % rp['y'], '%.4f' % rr, '%+.4f' % (rr - (R + 1)), '%.4f' % rr_last,
            tr['lastResizeStep'] if tr else '-',
            tr['stepsSinceLastResize'] if tr else '-',
            tr['firstNonFiniteStep'] if tr else '-',
            tr['stepsFromFirstNonFiniteToDump'] if tr else '-',
            tr['framesHeld'] if tr else '-',
            tr['framesFinite'] if tr else '-',
            os.path.basename(tp) if tr else 'none',
            pre]))

out += ['', '## D7\'s clause "every dump a jointed body under 10 s old with a mass ratio'
        ' under 3", asked of each dump',
        'arm\tid\tkind\tjointed\tage < 10 s\tmass ratio < 3\tall three']
for a, d, tr, kind, masses, rr, pre, tf in rows:
    j = bool(d['jointed'])
    y = d['ageSeconds'] < 10
    mr = (max(masses) / min(masses)) if min(masses) > 0 else None
    m3 = mr is not None and mr < 3
    out.append('%s\t%d\t%s\t%s\t%s\t%s\t%s' % (
        a, d['creatureId'], kind, j, y, m3, 'yes' if (j and y and m3) else 'NO'))

out += ['', '## the frames the two traced dumps held (each frame is one physics step of'
        ' 0.01 s). Every frame is finite by construction from this build; the numbers say'
        ' how far the body had already gone by the last finite step.',
        'arm\tid\tframe\tstep\tt\tlink\t|position| m\t|velocity| m/s\t|angularVelocity| rad/s'
        '\t|drag| N']
for a, d, tr, kind, masses, rr, pre, tf in rows:
    if not tr:
        continue
    for i, f in enumerate(tr['frames']):
        for l in f['links']:
            def mag(v):
                return math.sqrt(v['x'] ** 2 + v['y'] ** 2 + v['z'] ** 2)
            out.append('%s\t%d\t%d\t%s\t%.6f\t%d\t%.4g\t%.4g\t%.4g\t%.4g' % (
                a, d['creatureId'], i, f.get('step'), f['t'], l['index'],
                mag(l['position']), mag(l['velocity']), mag(l['angularVelocity']),
                mag(l['drag'])))

out += ['', '## comparison, round 37b (0097): 43 throws in three seeds of ten, every one a'
        ' two-part jointed body dumped within seconds of its birth with a joint mass ratio'
        ' under 2.4, away from the wall and from any resize, clustered by parent; and in 42'
        ' of the 43 every held frame was already non-finite, so the trace could not name the'
        ' onset. Round 38 is the first round whose ring keeps only finite frames.']

open(os.path.join(HERE, 'diverged-anatomy.tsv'), 'w', encoding='utf-8').write(
    '\n'.join(out) + '\n')
print('\n'.join(out))
