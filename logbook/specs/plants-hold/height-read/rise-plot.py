"""D325's height read as a picture: how far a gripping plant's highest part stands above its holdfast, by arm.

Pools the four seeds after 3,000 s, as pooled.py does, and draws each arm's share of gripping plants in 0.1 m bins.

    python -I logbook/specs/plants-hold/height-read/rise-plot.py <census folder> <out.png>
"""
import glob, json, os, re, sys

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

ARMS = (('plp', 'Light at each part', '#2a78d6'), ('pldr', 'Control', '#eb6834'))
LABEL_AT = {'plp': (0.42, 21.0), 'pldr': (0.32, 39.0)}  # direct labels, set in the chart's empty space
INK, MUTED, GRID, SURFACE = '#1f1f1e', '#6b6a64', '#e6e5e0', '#fcfcfb'
BIN, TOP = 0.1, 1.5

def rises(folder):
    out = {arm: [] for arm, _, _ in ARMS}
    for path in sorted(glob.glob(os.path.join(folder, '*.jsonl'))):
        m = re.match(r'(plp|pldr)-(\d+)[ab]?\.jsonl$', os.path.basename(path))
        if not m:
            continue
        with open(path, encoding='utf-8') as f:
            rows = [json.loads(l) for l in f if l.strip()]
        out[m.group(1)] += [r['max_y'] - r['root_y'] for r in rows if r['t'] > 3000 and r['plant'] and r['grips']]
    return out

def main(folder, png):
    data = rises(folder)
    nbins = int(round(TOP / BIN))
    edges = [i * BIN for i in range(nbins + 1)]
    fig, ax = plt.subplots(figsize=(8, 4.5), dpi=200)
    fig.patch.set_facecolor(SURFACE)
    ax.set_facecolor(SURFACE)
    for arm, label, colour in ARMS:
        xs = data[arm]
        counts = [0] * nbins
        for x in xs:
            counts[min(max(int(x / BIN), 0), nbins - 1)] += 1
        share = [100.0 * c / len(xs) for c in counts]
        ax.stairs(share, edges, color=colour, linewidth=2, label='%s (%d plants)' % (label, len(xs)))
        lx, ly = LABEL_AT[arm]
        ax.plot([lx, lx + 0.05], [ly + 0.6, ly + 0.6], color=colour, linewidth=2, solid_capstyle='round')
        ax.text(lx + 0.07, ly, label, color=INK, fontsize=9)
    ax.set_xlim(0, TOP)
    ax.set_ylim(0, None)
    ax.set_xlabel('Rise of the highest part above the holdfast (m)', color=INK, fontsize=10)
    ax.set_ylabel('Gripping plants (%)', color=INK, fontsize=10)
    ax.grid(axis='y', color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)
    for side in ('top', 'right'):
        ax.spines[side].set_visible(False)
    for side in ('left', 'bottom'):
        ax.spines[side].set_color(MUTED)
    ax.tick_params(colors=MUTED, labelsize=9)
    ax.legend(frameon=False, fontsize=9, labelcolor=INK, loc='upper right')
    fig.tight_layout()
    fig.savefig(png, facecolor=SURFACE)
    for arm, label, _ in ARMS:
        xs = sorted(data[arm])
        print('%-20s %4d plants, median rise %.2f m, at or over 1 m: %d' % (label, len(xs), xs[len(xs) // 2], sum(x >= 1.0 for x in xs)))
    return 0

if __name__ == '__main__':
    sys.exit(main(sys.argv[1], sys.argv[2]))
