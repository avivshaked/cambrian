"""A moving clip's check: per frame mean luma, the largest frame-to-frame jump in mean luma and in
mean absolute pixel change against its neighbours' median, and a sheet of six frames.

    python clipcheck.py <frames-dir> <sheet.png>
"""
import glob
import os
import sys
import numpy as np
from PIL import Image

frames = sorted(glob.glob(os.path.join(sys.argv[1], 'frame-*.png')))
means, changes = [], []
prev = None
for f in frames:
    a = np.asarray(Image.open(f).convert('RGB'), dtype=np.float32)
    y = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    y = y[int(y.shape[0] * 0.03):]
    means.append(y.mean())
    if prev is not None:
        changes.append(np.abs(y - prev).mean())
    prev = y
means, changes = np.array(means), np.array(changes)
print(f'{len(frames)} frames; mean luma {means.min():.1f} to {means.max():.1f}; largest step in mean {np.abs(np.diff(means)).max():.2f}')
med = np.median(changes)
worst = int(changes.argmax())
print(f'frame-to-frame change: median {med:.2f}, largest {changes.max():.2f} (between {worst} and {worst + 1}), '
      f'ratio {changes.max() / max(med, 1e-6):.1f}')
pick = np.linspace(0, len(frames) - 1, 6).astype(int)
tiles = [Image.open(frames[i]).convert('RGB').resize((960, 540)) for i in pick]
sheet = Image.new('RGB', (960 * 3 + 8, 540 * 2 + 4), (255, 255, 255))
for k, t in enumerate(tiles):
    sheet.paste(t, ((k % 3) * 964, (k // 3) * 544))
sheet.save(sys.argv[2])
print('sheet frames', list(pick))
