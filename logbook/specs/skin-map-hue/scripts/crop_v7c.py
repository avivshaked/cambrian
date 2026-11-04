"""Full-size crops of v7c's stills for the animals whose family the sheet leaves in doubt: one row per animal, the five looks
left to right (v7b pick, families as the genome picks, all bars, all eyespots, all edge outlines). Writes crop-v7c.png."""
import json, sys
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
LOOKS = ['v7b-b', 'v7c-family', 'v7c-bars', 'v7c-eyespot', 'v7c-edge']
IDS = [int(a) for a in sys.argv[1].split(',')] if len(sys.argv) > 1 else [54035, 58958, 62119, 61406, 77286, 53356, 50990]


def still(look, body):
    hits = sorted((HERE / look / 'png').glob(f'*{body}*.png'))
    return Image.open(hits[0]).convert('RGB') if hits else None


rows = []
for body in IDS:
    tiles = [still(look, body) for look in LOOKS]
    tiles = [t for t in tiles if t is not None]
    if not tiles:
        print(f'{body}: no stills'); continue
    h = 360
    tiles = [t.resize((int(t.width * h / t.height), h)) for t in tiles]
    row = Image.new('RGB', (sum(t.width for t in tiles), h), (24, 28, 34))
    x = 0
    for t in tiles:
        row.paste(t, (x, 0)); x += t.width
    rows.append(row)
W = max(r.width for r in rows)
out = Image.new('RGB', (W, sum(r.height for r in rows)), (24, 28, 34))
y = 0
for r in rows:
    out.paste(r, (0, y)); y += r.height
out.save(HERE / 'crop-v7c.png')
print('wrote', HERE / 'crop-v7c.png', out.size)
