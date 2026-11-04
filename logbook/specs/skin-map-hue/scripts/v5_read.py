"""Step Ӆ's read, the v5 sheet: the same 30 animals plus six of r62-h6202's family tree in four looks (v5-pick, v5-wheel, v5-value1,
v5-value05; full-colour/<look>/png). Per look: the circular spread of the rendered hue across the 30, how many sit within 30 deg of
pink (340), how many of the twelve 30-degree hue sectors are occupied, the mean saturation, the bodies' lightness (CIELAB L*, median
and 10th to 90th percentile), and the kin check (CIELAB delta E between mean body colours of the tree's five parent-child pairs
against the stranger median). Writes full-colour/read.md, read.json, sheet.png and family.png, with the owner's best-so-far frame
(best-so-far-53880.png) on both sheets.

    python -I scratch/skin-map/full-colour/v5_read.py [v5|v5b|v5c|v5d|v6]

v5b writes read-v5b.md, read-v5b.json, sheet-v5b.png and family-v5b.png, with v4's pick as the first look.
"""
import colorsys
import itertools
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, 'D:/Projects/experiments/evolution-simulator/scratch/skin-map')
from hue_dials import BG, DIM, INK, PAIRS, TREE, body, font, lab, pinkish, rendered, spread, tile  # noqa: E402

R = 'D:/Projects/experiments/evolution-simulator'
S = R + '/scratch/skin-map'
D = S + '/full-colour'
SETS = {
    'v5': [('v5-pick', 'v4 pick: hybrid, deepen 0.5'), ('v5-wheel', 'full wheel'), ('v5-value05', 'wheel + value 0.5'),
           ('v5-value1', 'wheel + value 1')],
    'v5b': [('v5-pick', 'v4 pick'), ('v5b-c14-v05', 'chroma 0.14, value 0.5'), ('v5b-c14-v1', 'chroma 0.14, value 1'),
            ('v5b-c20-v05', 'chroma 0.20, value 0.5'), ('v5b-c20-v1', 'chroma 0.20, value 1')],
    'v5c': [('v5-pick', 'v4 pick'), ('v5c-l58-v05', 'L 0.58, value 0.5'), ('v5c-l50-v05', 'L 0.50, value 0.5'),
            ('v5c-l42-v05', 'L 0.42, value 0.5'), ('v5c-l50-v1', 'L 0.50, value 1')],
    'v5d': [('v5-pick', 'v4 pick'), ('v5c-l50-v05', 'v5 pick, wheel from 165'), ('v5d-s145', 'wheel from 145'),
            ('v5d-s130', 'wheel from 130'), ('v5d-s115', 'wheel from 115')],
    'v6': [('v5-pick', 'v4 pick'), ('v5c-l50-v05', 'v5 pick'), ('v6-axes', 'v6: four axes'), ('v6-grey', 'v6, more greys'),
           ('v6-brown', 'v6, brown bias'), ('v6-narrow', 'v6, narrower light range'), ('v6-multi-far', 'v6 multicolour, far pattern'),
           ('v6-multi-near', 'v6 multicolour, near pattern')],
    'v6b': [('v5-pick', 'v4 pick'), ('v5c-l50-v05', 'v5 pick'), ('v6-axes', 'v6 as first set'), ('v6b-muted', 'v6b muted'),
            ('v6b-accents', 'v6b muted, quiet accents'), ('v6b-darkpattern', 'v6b dark, near pattern')],
    'v6c': [('v5-pick', 'v4 pick'), ('v6b-accents', 'v6b pick: muted, quiet accents'), ('v6c-mid345', 'v6c, middle at 345'),
            ('v6c-mid0', 'v6c, middle at 0'), ('v6c-mid15', 'v6c, middle at 15 (overshoot)')],
    'v6d': [('v5-pick', 'v4 pick'), ('v6c-mid0', 'v6c pick: middle at 0'), ('v6d-mild', 'v6d mild'), ('v6d-mid', 'v6d middle'),
            ('v6d-over', 'v6d overshoot')],
    'v7': [('v5-pick', 'v4 pick'), ('v6d-mid', 'v6d pick'), ('v7-reef', 'v7 reef: chroma, wheel, zones'),
           ('v7-reef-pattern', 'v7 reef, pattern in the zone colour'), ('v7-reef-over', 'v7 reef overshoot')],
    'v7b': [('v5-pick', 'v4 pick'), ('v7-reef', 'v7 reef'), ('v7b-a', 'v7 1b: pale 0.58, arc 185 to 68'),
            ('v7b-b', 'v7 1b: pale 0.55, arc 190 to 62, chroma 0.22'), ('v7b-over', 'v7 1b overshoot')],
    'v7c': [('v7b-b', 'v7b pick'), ('v7c-family', 'families as the genome picks'), ('v7c-bars', 'all bars'),
            ('v7c-eyespot', 'all eyespots'), ('v7c-edge', 'all edge outlines')],
}
# Colour families of a mean body colour by its CIELAB hue (degrees) and chroma: under C* 8 a body is neutral, named by its L*.
FAMILIES = [('mauve, pink', 330, 15), ('red, rust, brown', 15, 80), ('yellow, olive', 80, 120), ('green', 120, 170),
            ('teal, cyan', 170, 240), ('blue', 240, 290), ('violet, purple', 290, 330)]
NEUTRAL_C = 8.0


VIVID_C = 40.0
# Bodies that are mostly plant tissue, drawn in the producers' colours (the owner's ruling of 2026-10-29): out of the reef counts.
PLANTS = {54039, 54842, 88392, 74768, 56782, 55485}


def zones(path):
    """True when the body shows two colour zones: among its saturated pixels (HSV saturation over 0.25, value over 0.15), the
    hue in twelve 30-degree bins has a second peak at least 60 degrees from the first holding at least 15 per cent of them, and the
    saturated pixels are at least a fifth of the body."""
    rgb = body(path)
    if len(rgb) < 50:
        return False
    mx, mn = rgb.max(axis=1), rgb.min(axis=1)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-9), 0)
    keep = (sat > 0.25) & (mx > 0.15)
    if keep.sum() < 0.2 * len(rgb):
        return False
    hsv = np.array([colorsys.rgb_to_hsv(*p) for p in rgb[keep][:: max(1, int(keep.sum()) // 4000)]])
    bins = np.bincount((hsv[:, 0] * 12).astype(int) % 12, minlength=12) / len(hsv)
    first = int(np.argmax(bins))
    far = [bins[i] for i in range(12) if min(abs(i - first), 12 - abs(i - first)) >= 2]
    return max(far) >= 0.15


def yellow(v):
    """A clear yellow: CIELAB hue 75 to 105, chroma over 30, L* over 65 (an olive is the same hue, darker or duller)."""
    L, a, b = (float(x) for x in v)
    h = math.degrees(math.atan2(b, a)) % 360
    return 75 <= h < 105 and math.hypot(a, b) > 30 and L > 65


def family(v):
    L, a, b = (float(x) for x in v)
    if math.hypot(a, b) < NEUTRAL_C:
        return 'charcoal' if L < 35 else ('bone, silver' if L >= 70 else 'grey, pewter')
    h = math.degrees(math.atan2(b, a)) % 360
    for name, lo, hi in FAMILIES:
        if (lo <= h < hi) if lo < hi else (h >= lo or h < hi):
            return name
    return '?'
SET = sys.argv[1] if len(sys.argv) > 1 else 'v5'
LOOKS = SETS[SET]
REFERENCE = R + '/scratch/theatre-realism/sheets/refs/best-so-far-53880.png'


def png(look, ident):
    return '%s/%s/png/id-%d.png' % (D, look, ident)


def texture(path):
    """The pattern and texture's contrast on a still: the standard deviation, over the body's inner pixels, of the luma less its
    blur at 4 pixels, so the broad shading drops out and the maze, pores and carve stay. Zero for a flat body."""
    img = Image.open(path).convert('RGBA')
    mask = np.asarray(img.getchannel('A').filter(ImageFilter.MinFilter(7)), dtype=np.float64) > 128
    luma = img.convert('RGB').convert('L')
    hp = np.asarray(luma, dtype=np.float64) - np.asarray(luma.filter(ImageFilter.GaussianBlur(4)), dtype=np.float64)
    return float(hp[mask].std()) if mask.sum() > 50 else 0.0


def measure(picks):
    lines = ['# Full colour as rendered (the %s sheet)' % SET, '',
             '| look | hue spread (deg) | within 30 deg of pink | hue sectors of 12 | mean saturation | L*, median (p10 to p90) | kin colour distance, median (max) | strangers, median | kin hue gap, max (deg) |',
             '|---|---|---|---|---|---|---|---|---|']
    table = {}
    for look, name in LOOKS:
        got = {p['id']: body(png(look, p['id'])) for p in picks if os.path.exists(png(look, p['id']))}
        tree = {i: body(png(look, i)) for i in TREE if os.path.exists(png(look, i))}
        if len(got) < len(picks) or len(tree) < len(TREE):
            lines.append('| %s | missing: %d of %d strangers, %d of %d kin drawn ||||||||' % (name, len(got), len(picks), len(tree), len(TREE)))
            continue
        hs = {i: rendered(v) for i, v in got.items()}
        labs = {i: lab(v) for i, v in got.items()}
        th = {i: rendered(v) for i, v in tree.items()}
        tl = {i: lab(v) for i, v in tree.items()}
        hues = [h for h, _ in hs.values()]
        ls = [float(labs[i][0]) for i in labs]
        stranger = [float(np.linalg.norm(labs[a] - labs[b])) for a, b in itertools.combinations(labs, 2)]
        kin = [float(np.linalg.norm(tl[a] - tl[b])) for a, b in PAIRS]
        gaps = [min(abs(th[a][0] - th[b][0]), 360 - abs(th[a][0] - th[b][0])) for a, b in PAIRS]
        sectors = len({int(h // 30) % 12 for h in hues})
        table[look] = {'spread': spread(hues), 'pink': sum(pinkish(h) for h in hues), 'sectors': sectors,
                       'sat': float(np.mean([s for _, s in hs.values()])), 'L': [float(np.percentile(ls, q)) for q in (10, 50, 90)],
                       'kin': kin, 'stranger': float(np.median(stranger)), 'hues': {str(i): hs[i][0] for i in hs},
                       'lightness': {str(i): float(labs[i][0]) for i in labs},
                       'family': {str(i): family(labs[i]) for i in labs},
                       'vivid': [str(i) for i in labs if i not in PLANTS and math.hypot(float(labs[i][1]), float(labs[i][2])) > VIVID_C],
                       'yellow': [str(i) for i in labs if i not in PLANTS and yellow(labs[i])],
                       'zones': [str(p['id']) for p in picks if p['id'] not in PLANTS and zones(png(look, p['id']))],
                       'texture': {str(p['id']): texture(png(look, p['id'])) for p in picks}}
        t = table[look]
        lines.append('| %s | %.0f | %d of %d | %d | %.2f | %.0f (%.0f to %.0f) | %.1f (%.1f) | %.1f | %.0f |' % (
            name, t['spread'], t['pink'], len(hues), sectors, t['sat'], t['L'][1], t['L'][0], t['L'][2], float(np.median(kin)), max(kin),
            t['stranger'], max(gaps)))
    lines += ['', 'Colour distance is CIELAB delta E between the mean body colours: about 2 is a just-visible difference, and the',
              'stranger median is the typical gap between two unrelated animals. The kin pairs are r62-h6202\'s 36766 to 38847 and 40020,',
              '38847 to 43308 and 43309, and 40020 to 42495. L* is CIELAB lightness of the mean body colour, 0 black and 100 white.']
    first = LOOKS[0][0]
    if first in table:
        lines += ['', '## Pattern and texture against %s' % LOOKS[0][1], '',
                  'Texture is the contrast of the fine detail on the body (maze, pores, carve), with the broad shading taken out. Each',
                  'animal is compared with itself in the first look; an animal under half of it has lost most of its pattern.', '',
                  '| look | texture, median ratio to the first look | animals under half (id: ratio, L*) |', '|---|---|---|']
        base = table[first]['texture']
        for look, name in LOOKS:
            if look not in table:
                continue
            tx = table[look]['texture']
            ratios = {i: tx[i] / base[i] for i in tx if base.get(i, 0) > 0.5}
            lost = sorted((r, i) for i, r in ratios.items() if r < 0.5)
            table[look]['texture_ratio'] = ratios
            lines.append('| %s | %.2f | %s |' % (name, float(np.median(list(ratios.values()))),
                         ', '.join('%s: %.2f, %.0f' % (i, r, table[look]['lightness'][i]) for r, i in lost) or 'none'))
    names = [n for n, _, _ in FAMILIES] + ['charcoal', 'grey, pewter', 'bone, silver']
    lines += ['', '## Colour families', '',
              'Each animal\'s mean body colour by its CIELAB hue; under chroma %.0f it is neutral, named by its lightness (charcoal under' % NEUTRAL_C,
              'L* 35, bone or silver from 70). Neutral animals are listed by id.', '',
              '| look | ' + ' | '.join(names) + ' | neutral ids |', '|---|' + '---|' * (len(names) + 1)]
    for look, name in LOOKS:
        if look not in table:
            continue
        fam = table[look]['family']
        lines.append('| %s | %s | %s |' % (name, ' | '.join(str(sum(1 for v in fam.values() if v == n)) for n in names),
                     ', '.join('%s (%s)' % (i, v.split(',')[0]) for i, v in sorted(fam.items()) if v in names[-3:]) or 'none'))
    lines += ['', '## Reef counts', '',
              'Vivid: mean body colour over CIELAB chroma %.0f. Yellow: CIELAB hue 75 to 105, chroma over 30, L* over 65. Two zones: a second' % VIVID_C,
              'hue peak at least 60 degrees from the first among the saturated pixels, holding 15 per cent of them. Blue: the families\' blue count.', '',
              '| look | vivid | yellow | blue | two zones | vivid ids | two-zone ids |', '|---|---|---|---|---|---|---|']
    for look, name in LOOKS:
        if look not in table:
            continue
        t = table[look]
        lines.append('| %s | %d | %d | %d | %d | %s | %s |' % (name, len(t['vivid']), len(t['yellow']),
                     sum(1 for v in t['family'].values() if v == 'blue'), len(t['zones']), ', '.join(sorted(t['vivid'])) or 'none',
                     ', '.join(sorted(t['zones'])) or 'none'))
    with open(D + '/read%s.md' % (''if SET=='v5' else '-'+SET), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    with open(D + '/read%s.json' % (''if SET=='v5' else '-'+SET), 'w', encoding='utf-8') as f:
        json.dump(table, f, indent=1)
    print('\n'.join(lines[2:]))


def reference(width, height):
    ref = Image.open(REFERENCE).convert('RGB')
    ref.thumbnail((width, height))
    return ref


def strangers_sheet(picks):
    size, pad, label, per_row, head = 150, 8, 24, (3 if len(LOOKS) <= 5 else 2), 330
    cell_w = len(LOOKS) * size + (len(LOOKS) - 1) * 2 + 2 * pad
    rows = (len(picks) + per_row - 1) // per_row
    sheet = Image.new('RGB', (per_row * cell_w + pad, head + rows * (size + label + pad) + pad), BG)
    d = ImageDraw.Draw(sheet)
    d.text((pad, 10), 'Thirty unrelated animals in %d skin looks, left to right: ' % len(LOOKS) + ', '.join(n for _, n in LOOKS), fill=INK, font=font(22))
    d.text((pad, 42), 'One animal per lineage from ten worlds, the same thirty as v4. At right, the owner\'s best-so-far frame.',
           fill=DIM, font=font(16))
    ref = reference(500, 290)
    sheet.paste(ref, (sheet.width - ref.width - pad, 30))
    small = font(13)
    for k, p in enumerate(picks):
        x0 = pad + (k % per_row) * cell_w
        y0 = head + (k // per_row) * (size + label + pad)
        for j, (look, _) in enumerate(LOOKS):
            sheet.paste(tile(png(look, p['id']), size), (x0 + j * (size + 2), y0))
        d.text((x0, y0 + size + 3), '%s  %d' % (p['world'], p['id']), fill=DIM, font=small)
    sheet.save(D + '/sheet%s.png' % (''if SET=='v5' else '-'+SET))
    print('wrote', D + '/sheet%s.png' % (''if SET=='v5' else '-'+SET), sheet.size)


def family_sheet():
    size, pad, head, left = 170, 8, 60, 200
    body_w = left + len(TREE) * (size + pad) + pad
    ref = reference(520, 300)
    sheet = Image.new('RGB', (body_w + ref.width + pad, max(head + len(LOOKS) * (size + pad) + pad, head + ref.height + pad)), BG)
    d = ImageDraw.Draw(sheet)
    d.text((pad, 10), 'One family of r62-h6202: 36766, its children 38847 and 40020, and their children 43308, 43309 (of 38847) and 42495 (of 40020)',
           fill=INK, font=font(18))
    d.text((pad, 34), 'Kin should look alike in every row. At right, the owner\'s best-so-far frame.', fill=DIM, font=font(15))
    for j, i in enumerate(TREE):
        d.text((left + j * (size + pad) + 4, head - 18), str(i), fill=DIM, font=font(14))
    for r, (look, name) in enumerate(LOOKS):
        y0 = head + r * (size + pad)
        d.text((pad, y0 + size // 2 - 8), name, fill=INK, font=font(15))
        for j, i in enumerate(TREE):
            sheet.paste(tile(png(look, i), size), (left + j * (size + pad), y0))
    sheet.paste(ref, (body_w, head))
    sheet.save(D + '/family%s.png' % (''if SET=='v5' else '-'+SET))
    print('wrote', D + '/family%s.png' % (''if SET=='v5' else '-'+SET), sheet.size)


def main():
    picks = json.load(open(S + '/hue-spread/picks.json'))
    measure(picks)
    strangers_sheet(picks)
    family_sheet()


if __name__ == '__main__':
    main()
