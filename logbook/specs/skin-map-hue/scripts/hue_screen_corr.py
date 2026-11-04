"""How far each screened hue follows the other skin channels (Spearman over the fitting set): the lineage score leans on investment
and log_margin, which pattern_regime and pattern_scale already read. Writes hue-screen/correlations.md."""
import sys

import numpy as np

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
import hue_screen as S   # noqa: E402
import hue_spread as H   # noqa: E402


def ranks(x):
    order = np.argsort(x, kind='mergesort')
    r = np.empty(len(x))
    r[order] = np.arange(len(x))
    return r


bodies = S.load()
w, _, centre = S.lineage_weights(bodies)
w_orth, _, _ = S.lineage_weights(bodies, orthogonal=True)
S.apply(bodies, w, centre, w_orth)
alive = [b for b in bodies.values() if b['alive']]
names = [p['name'] for p in H.W['parameters']]
lines = ['# Each screened hue against the other skin channels (Spearman, %d animals alive at 24,000 s)' % len(alive), '',
         '| hue | ' + ' | '.join(names[1:]) + ' |', '|---|' + '---|' * (len(names) - 1)]
for v in ('today', 'lineage', 'rank', 'hybrid', 'orth'):
    hue = ranks(np.array([b['v'][v][0] for b in alive]))
    cells = []
    for k in range(1, len(names)):
        other = ranks(np.array([b['v'][v][k] for b in alive]))
        cells.append('%+.2f' % np.corrcoef(hue, other)[0, 1])
    lines.append('| %s | %s |' % (v, ' | '.join(cells)))
with open(S.OUT + '/correlations.md', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines) + '\n')
print('\n'.join(lines))
