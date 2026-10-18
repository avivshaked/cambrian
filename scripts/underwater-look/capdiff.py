"""The caps' shade on the marched shafts, from the two debug stills (EVOSIM_THEATRE_SHAFT_DEBUG 1, with
the caps, and 2, without): a sheet of the two at 1280x720 and their difference, brightened, below.

    python capdiff.py <with-caps.png> <without-caps.png> <out.png>
"""
import sys
import numpy as np
from PIL import Image

a = np.asarray(Image.open(sys.argv[1]).convert('RGB'), dtype=np.float64)
b = np.asarray(Image.open(sys.argv[2]).convert('RGB'), dtype=np.float64)
d = np.clip(b - a, 0, None).mean(axis=2)
print(f'mean luma with caps {a.mean():.1f}, without {b.mean():.1f}; share of pixels the caps darken by 5+: '
      f'{(d > 5).mean():.3f}; the most {d.max():.0f}')
size = (1280, 720)
diff = Image.fromarray(np.clip(d * 4, 0, 255).astype(np.uint8)).convert('RGB')
sheet = Image.new('RGB', (size[0] * 3 + 8, size[1]), (255, 255, 255))
for i, im in enumerate([Image.fromarray(a.astype(np.uint8)), Image.fromarray(b.astype(np.uint8)), diff]):
    sheet.paste(im.resize(size), (i * (size[0] + 4), 0))
sheet.save(sys.argv[3])
