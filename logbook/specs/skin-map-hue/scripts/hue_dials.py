"""Step Ӂ's read: the hue sheet's 30 animals plus six of r62-h6202's family tree, drawn in six looks (map1, map2, turn1, turn05,
floor, turn1floor; scratch/skin-map/hue-dials/<look>/png). For each look: the circular spread of the rendered hue across the 30,
how many sit within 30 deg of pink (340), the bodies' mean saturation, and the kin check: the CIELAB distance between the mean
body colours of the tree's five parent-child pairs against the median over the 435 stranger pairs. Writes hue-dials/read.md and
lays the sheet out as hue-dials/sheet.png (the strangers, six looks each) and hue-dials/family.png (the tree, six looks).

    python -I scratch/skin-map/hue_dials.py
"""
import colorsys
import itertools
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

R = 'D:/Projects/experiments/evolution-simulator'
S = R + '/scratch/skin-map'
D = S + '/hue-dials'
LOOKS = [('map1', 'today'), ('map2', 'hybrid'), ('turn05', 'turn 0.5'), ('turn1', 'turn 1'), ('floor', 'floor 0.5'),
         ('turn1floor', 'turn 1 + floor'), ('today-deepen05', 'today, deepen 0.5'), ('deepen05', 'hybrid, deepen 0.5'),
         ('deepen1', 'hybrid, deepen 1')]
# The sheets show the looks that differ to the eye; turn and floor drew as the hybrid does (read.md has their numbers).
SHEET = [('map1', 'today'), ('today-deepen05', 'today, deepen 0.5'), ('map2', 'hybrid'), ('deepen05', 'hybrid, deepen 0.5'),
         ('deepen1', 'hybrid, deepen 1')]
TREE = [36766, 38847, 40020, 43308, 43309, 42495]
PAIRS = [(36766, 38847), (36766, 40020), (38847, 43308), (38847, 43309), (40020, 42495)]
REFERENCE = R + '/scratch/owner/genome-to-skin-family-sheet/reference-your-pick-of-the-creature-look.png'
BG = (24, 30, 36)
INK = (225, 230, 235)
DIM = (150, 160, 170)


def body(path):
    a = np.asarray(Image.open(path).convert('RGBA'), dtype=np.float64) / 255.0
    return a[..., :3][a[..., 3] > 0.5]


def rendered(rgb):
    hsv = np.array([colorsys.rgb_to_hsv(*p) for p in rgb[:: max(1, len(rgb) // 4000)]])
    w = hsv[:, 1]
    ang = hsv[:, 0] * 2 * math.pi
    x, y = (w * np.cos(ang)).sum(), (w * np.sin(ang)).sum()
    return math.degrees(math.atan2(y, x)) % 360.0, float(w.mean())


def lab(rgb):
    c = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4).mean(axis=0)
    xyz = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]) @ c
    xyz /= np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.array([116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])])


def spread(hues):
    ang = np.radians(hues)
    r = math.hypot(np.cos(ang).mean(), np.sin(ang).mean())
    return math.degrees(math.sqrt(-2 * math.log(max(r, 1e-9))))


def pinkish(h):
    return min(abs(h - 340), 360 - abs(h - 340)) <= 30


def font(size):
    for name in ('segoeui.ttf', 'arial.ttf'):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def tile(path, size):
    out = Image.new('RGB', (size, size), BG)
    if not os.path.exists(path):
        return out
    img = Image.open(path).convert('RGBA')
    box = img.getbbox()
    if box:
        img = img.crop(box)
    img.thumbnail((size - 6, size - 6))
    out.paste(img, ((size - img.width) // 2, (size - img.height) // 2), img)
    return out


def png(look, ident):
    return '%s/%s/png/id-%d.png' % (D, look, ident)


def measure(picks):
    lines = ['# The hue dials as rendered', '',
             '| look | hue spread (deg) | within 30 deg of pink | mean saturation | kin colour distance, median (max) | strangers, median | kin hue gap, max (deg) |',
             '|---|---|---|---|---|---|---|']
    table = {}
    for look, name in LOOKS:
        got = {p['id']: body(png(look, p['id'])) for p in picks if os.path.exists(png(look, p['id']))}
        tree = {i: body(png(look, i)) for i in TREE if os.path.exists(png(look, i))}
        if len(got) < len(picks) or len(tree) < len(TREE):
            lines.append('| %s | missing: %d of %d strangers, %d of %d kin drawn |||||' % (name, len(got), len(picks), len(tree), len(TREE)))
            continue
        hs = {i: rendered(v) for i, v in got.items()}
        labs = {i: lab(v) for i, v in got.items()}
        th = {i: rendered(v) for i, v in tree.items()}
        tl = {i: lab(v) for i, v in tree.items()}
        hues = [h for h, _ in hs.values()]
        stranger = [float(np.linalg.norm(labs[a] - labs[b])) for a, b in itertools.combinations(labs, 2)]
        kin = [float(np.linalg.norm(tl[a] - tl[b])) for a, b in PAIRS]
        gaps = [min(abs(th[a][0] - th[b][0]), 360 - abs(th[a][0] - th[b][0])) for a, b in PAIRS]
        table[look] = {'spread': spread(hues), 'pink': sum(pinkish(h) for h in hues), 'sat': float(np.mean([s for _, s in hs.values()])),
                       'kin': kin, 'stranger': float(np.median(stranger)), 'hues': {str(i): hs[i][0] for i in hs}}
        lines.append('| %s | %.0f | %d of %d | %.2f | %.1f (%.1f) | %.1f | %.0f |' % (
            name, table[look]['spread'], table[look]['pink'], len(hues), table[look]['sat'], float(np.median(kin)), max(kin),
            table[look]['stranger'], max(gaps)))
    lines += ['', 'Colour distance is CIELAB delta E between the mean body colours, so about 2 is a just-visible difference and the',
              'stranger median is the typical gap between two unrelated animals. The kin pairs are r62-h6202\'s 36766 to 38847 and 40020,',
              '38847 to 43308 and 43309, and 40020 to 42495.']
    with open(D + '/read.md', 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')
    with open(D + '/read.json', 'w', encoding='utf-8') as f:
        json.dump(table, f, indent=1)
    print('\n'.join(lines[2:2 + 2 + len(LOOKS)]))
    return table


def strangers_sheet(picks):
    size, pad, label, per_row, head = 132, 8, 24, 3, 210
    cell_w = len(SHEET) * size + (len(SHEET) - 1) * 2 + 2 * pad
    rows = (len(picks) + per_row - 1) // per_row
    sheet = Image.new('RGB', (per_row * cell_w + pad, head + rows * (size + label + pad) + pad), BG)
    d = ImageDraw.Draw(sheet)
    d.text((pad, 10), 'Thirty unrelated animals in five skin looks, left to right: ' + ', '.join(n for _, n in SHEET), fill=INK, font=font(22))
    d.text((pad, 42), 'One animal per lineage from ten worlds, chosen by a seeded shuffle. Reference at right: the owner\'s best-so-far frame.',
           fill=DIM, font=font(16))
    if os.path.exists(REFERENCE):
        ref = Image.open(REFERENCE).convert('RGB')
        ref.thumbnail((420, 140))
        sheet.paste(ref, (sheet.width - ref.width - pad, 64))
    small = font(13)
    for k, p in enumerate(picks):
        x0 = pad + (k % per_row) * cell_w
        y0 = head + (k // per_row) * (size + label + pad)
        for j, (look, _) in enumerate(SHEET):
            sheet.paste(tile(png(look, p['id']), size), (x0 + j * (size + 2), y0))
        d.text((x0, y0 + size + 3), '%s  %d' % (p['world'], p['id']), fill=DIM, font=small)
    sheet.save(D + '/sheet.png')
    print('wrote', D + '/sheet.png', sheet.size)


def family_sheet():
    size, pad, head, left = 170, 8, 60, 160
    sheet = Image.new('RGB', (left + len(TREE) * (size + pad) + pad, head + len(SHEET) * (size + pad) + pad), BG)
    d = ImageDraw.Draw(sheet)
    d.text((pad, 10), 'One family of r62-h6202: 36766, its children 38847 and 40020, and their children 43308, 43309 (of 38847) and 42495 (of 40020)',
           fill=INK, font=font(18))
    d.text((pad, 34), 'Kin should look alike in every row.', fill=DIM, font=font(15))
    for j, i in enumerate(TREE):
        d.text((left + j * (size + pad) + 4, head - 18), str(i), fill=DIM, font=font(14))
    for r, (look, name) in enumerate(SHEET):
        y0 = head + r * (size + pad)
        d.text((pad, y0 + size // 2 - 8), name, fill=INK, font=font(15))
        for j, i in enumerate(TREE):
            sheet.paste(tile(png(look, i), size), (left + j * (size + pad), y0))
    sheet.save(D + '/family.png')
    print('wrote', D + '/family.png', sheet.size)


def main():
    picks = json.load(open(S + '/hue-spread/picks.json'))
    measure(picks)
    strangers_sheet(picks)
    family_sheet()


if __name__ == '__main__':
    main()
