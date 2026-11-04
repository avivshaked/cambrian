"""Does the skin map's animal hue bunch? (The coordinator's ask of 2026-10-30, after the four-worlds sheet landed three of four
unrelated lineages in pink to violet.)

Over the pooled fit's own fitting set, the non-producers (photosynthetic share under 0.5) alive at 24,000 s in its ten reference
worlds, each body's skin vector is computed as the C# does (pick_worlds2.py's rule: the pooled projection and folded weights, the
brain shrink, the clip), and its base hue is put on the marine palette as TheatreSkinMap.PaletteHue does: 220 degrees plus 190 times
the base hue, round the wheel (blue, violet, magenta, red, gold; no teal or green by design). Writes hue-spread/:

  hue-histogram.png   the palette hue's histogram, each bar in its own hue, all ten worlds and each world beside it
  hue-spread.md       the counts by band and by world, and the base hue's quantiles
  picks.json          for the sheet: one animal (3 parts or more) per clade, three per world where a world has them, chosen by a
                      seeded shuffle and not by hue, so the sheet shows the spread as it is

    python -I scratch/skin-map/hue_spread.py
"""
import colorsys
import csv
import json
import math
import os
import random

from PIL import Image, ImageDraw, ImageFont

R = 'D:/Projects/experiments/evolution-simulator'
S = R + '/scratch/skin-map'
OUT = S + '/hue-spread'
P = json.load(open(S + '/pooled-24000/projection.json'))
W = json.load(open(S + '/pooled-24000/weights.json'))
MEAN = dict(zip(P['features_all'], P['mean_all']))
SD = dict(zip(P['features_all'], P['sd_all']))
K = P['brain_shrink_links']
START, SPAN = 220.0, 190.0
PER_WORLD = 3

WORLDS = ['r62-h6202', 'r63b-a-6202', 'r63b-a-6203', 'r63b-b-6202', 'r63b-b-6203', 'r63b-c-6202', 'r63b-c-6203',
          'r62cf-6202', 'r62cf-6203', 'r62cf-6204']
BANDS = [('blue', 200, 250), ('violet', 250, 285), ('magenta', 285, 330), ('pink-red', 330, 360), ('red-orange', 0, 25),
         ('gold', 25, 60)]


def folder(w):
    return S + ('/pooled-24000' if w == 'r62-h6202' else '/pooled-' + w)


def vec(r):
    n = math.expm1(float(r['brain_log_links']))
    shrink = n / (n + K)
    out = []
    for prm in W['parameters']:
        s = prm.get('bias', 0.0)
        used = [i for i in prm['inputs'] if i['used']]
        for i in used:
            f = i['feature']
            z = (float(r[f]) - MEAN[f]) / SD[f] if SD[f] > 0 else 0.0
            if f.startswith('brain_w_'):
                z *= shrink
            s += i['weight'] * max(-3.0, min(3.0, z))
        out.append(1 / (1 + math.exp(-s)) if used else 0.5)
    return out


def palette(base):
    return (START + SPAN * min(1.0, max(0.0, base))) % 360.0


def band(h):
    for name, lo, hi in BANDS:
        if lo <= h < hi:
            return name
    return 'outside'


def run_of(w):
    for r in P['reference_runs']:
        if w in r['run']:
            return r['run']
    raise KeyError(w)


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = []
    for w in WORLDS:
        with open(folder(w) + '/features.csv', encoding='utf-8') as f:
            for r in csv.DictReader(f):
                if r['alive'] != '1' or float(r['share_photosynthetic']) >= 0.5:
                    continue
                v = vec(r)
                rows.append({'world': w, 'id': int(r['id']), 'clade': r['clade'], 'parts': round(math.exp(float(r['log_parts']))),
                             'base_hue': v[0], 'hue': palette(v[0])})

    lines = ['# The skin map\'s animal hue over its fitting set', '',
             'Non-producers alive at 24,000 s in the pooled fit\'s ten reference worlds: %d (the fit records %d).' % (len(rows), P['n_reference']),
             'Palette hue = 220 + 190 x base hue degrees, round the wheel.', '']
    bases = sorted(x['base_hue'] for x in rows)
    q = lambda p: bases[min(len(bases) - 1, int(p * len(bases)))]
    lines.append('Base hue quantiles: 5%% %.3f, 25%% %.3f, 50%% %.3f, 75%% %.3f, 95%% %.3f; at the clip 0: %d, at 1: %d.' % (
        q(0.05), q(0.25), q(0.5), q(0.75), q(0.95), sum(1 for b in bases if b <= 1e-6), sum(1 for b in bases if b >= 1 - 1e-6)))
    lines += ['', '| World | n | ' + ' | '.join(b[0] for b in BANDS) + ' |', '|---|---|' + '---|' * len(BANDS)]
    for w in WORLDS + ['all']:
        sel = [x for x in rows if w == 'all' or x['world'] == w]
        counts = [sum(1 for x in sel if band(x['hue']) == b[0]) for b in BANDS]
        lines.append('| %s | %d | ' % (w, len(sel)) + ' | '.join('%d (%.0f%%)' % (c, 100.0 * c / max(1, len(sel))) for c in counts) + ' |')
    clades = {}
    for x in rows:
        clades.setdefault((x['world'], x['clade']), []).append(x)
    lines += ['', 'Clades with an animal: %d. Clade-weighted (each clade its median hue):' % len(clades)]
    med = []
    for members in clades.values():
        hs = sorted(m['base_hue'] for m in members)
        med.append(palette(hs[len(hs) // 2]))
    lines.append(', '.join('%s %d' % (b[0], sum(1 for h in med if band(h) == b[0])) for b in BANDS))

    rng = random.Random(6302)
    picks = []
    for w in WORLDS:
        cands = [x for x in rows if x['world'] == w and x['parts'] >= 3]
        rng.shuffle(cands)
        seen = set()
        for x in cands:
            if x['clade'] in seen:
                continue
            seen.add(x['clade'])
            picks.append(dict(x, run=run_of(w)))
            if len([p for p in picks if p['world'] == w]) >= PER_WORLD:
                break
    lines += ['', 'Sheet picks (seeded shuffle, one per clade, 3 parts or more, up to %d a world): %d' % (PER_WORLD, len(picks))]
    for p in picks:
        lines.append('- %s %d clade %s, %d parts, base hue %.3f, palette %.0f deg (%s)' % (
            p['world'], p['id'], p['clade'], p['parts'], p['base_hue'], p['hue'], band(p['hue'])))

    with open(OUT + '/hue-spread.md', 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    with open(OUT + '/picks.json', 'w', encoding='utf-8') as f:
        json.dump(picks, f, indent=1)
    draw(rows)
    print('\n'.join(lines))


def draw(rows):
    bins = 38   # 5 degrees each over the palette's 190
    width, height, pad = 1400, 900, 60
    img = Image.new('RGB', (width, height), (24, 30, 36))
    d = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    panels = [('all ten worlds', rows)] + [(w, [x for x in rows if x['world'] == w]) for w in WORLDS]
    cols, ph = 4, (height - pad) // 3
    big_w = (width - pad) // 2

    def panel(x0, y0, w, h, title, sel):
        counts = [0] * bins
        for x in sel:
            k = min(bins - 1, int(((x['hue'] - START) % 360.0) / SPAN * bins))
            counts[k] += 1
        top = max(1, max(counts))
        bw = w / bins
        for k, c in enumerate(counts):
            hue = (START + (k + 0.5) * SPAN / bins) % 360.0
            rgb = tuple(int(255 * v) for v in colorsys.hsv_to_rgb(hue / 360.0, 0.75, 0.95))
            bh = (h - 30) * c / top
            d.rectangle([x0 + k * bw, y0 + h - bh, x0 + (k + 1) * bw - 1, y0 + h], fill=rgb)
        d.text((x0, y0), '%s  n=%d' % (title, len(sel)), fill=(225, 230, 235), font=font)

    panel(pad // 2, pad // 2, big_w - 20, ph * 2 - 40, panels[0][0], panels[0][1])
    small = panels[1:]
    sw = (width - big_w - pad) // 2
    for i, (title, sel) in enumerate(small[:6]):
        panel(big_w + pad // 2 + (i % 2) * sw, pad // 2 + (i // 2) * (ph * 2 // 3), sw - 20, ph * 2 // 3 - 30, title, sel)
    for i, (title, sel) in enumerate(small[6:]):
        panel(pad // 2 + i * ((width - pad) // 4), pad // 2 + ph * 2, (width - pad) // 4 - 20, ph - 40, title, sel)
    img.save(OUT + '/hue-histogram.png')


if __name__ == '__main__':
    main()
