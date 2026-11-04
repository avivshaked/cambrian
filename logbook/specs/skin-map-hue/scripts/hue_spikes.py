"""What the bodies in hue_spread's spikes share: base hue 0.339 (violet, 284 deg), base hue 0 (the clip, blue) and the rest,
by the features the base hue reads. Writes hue-spread/spikes.md."""
import csv
import math
import statistics
import sys

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_spread as H   # noqa: E402

base = [p for p in H.W['parameters'] if p['name'] == 'base_hue'][0]
used = [i['feature'] for i in base['inputs'] if i['used']]
groups = {'violet spike (0.335-0.343)': [], 'clip 0': [], 'rest': []}
for w in H.WORLDS:
    with open(H.folder(w) + '/features.csv', encoding='utf-8') as f:
        for r in csv.DictReader(f):
            if r['alive'] != '1' or float(r['share_photosynthetic']) >= 0.5:
                continue
            b = H.vec(r)[0]
            key = 'violet spike (0.335-0.343)' if 0.335 <= b <= 0.343 else 'clip 0' if b <= 1e-6 else 'rest'
            groups[key].append(r)

lines = ['# What the hue spikes share', '', 'Base hue reads: ' + ', '.join(
    '%s %+.2f' % (i['feature'], i['weight']) for i in base['inputs'] if i['used']) + ' (bias %+.2f)' % base.get('bias', 0.0), '']
lines.append('| feature | ' + ' | '.join('%s (n=%d) median [min, max]' % (k, len(v)) for k, v in groups.items()) + ' |')
lines.append('|---|' + '---|' * len(groups))
for f in used + ['log_parts', 'brain_log_links', 'shape_box', 'jointed_share']:
    cells = []
    for v in groups.values():
        xs = [float(r[f]) for r in v]
        cells.append('%.3f [%.3f, %.3f]' % (statistics.median(xs), min(xs), max(xs)) if xs else '-')
    lines.append('| %s | %s |' % (f, ' | '.join(cells)))
with open(H.OUT + '/spikes.md', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines) + '\n')
print('\n'.join(lines))
