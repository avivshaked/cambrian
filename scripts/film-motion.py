"""Motion check on a clip's frames: does anything jump? For each pair of consecutive frames, three
bands of the creature close-up at 1920x1080 (scaled for other sizes):

  snow    changed pixels (|luma change| > 10) in a strip of dark water, rows 40-300, where the
          motes are and the bodies are not;
  shafts  mean |luma change| of the luma box-blurred over 25 px (motes and edges blurred away),
          rows 40-600, where the sheets show;
  bodies  changed pixels (|luma change| > 10) in the bodies' region, x 250-1650, rows 300-700.

Prints, per band, the median, the largest value and its frame, the largest ratio to the median, and
every frame above 2x the median (the bar: none). Optionally writes a strip of six consecutive frames
around a given frame, zoomed on a window of the snow strip.

    python motion.py <frames-dir> [--strip <frame> <out.png>]
"""
import glob
import os
import sys
import numpy as np
from PIL import Image


def luma(path):
    a = np.asarray(Image.open(path).convert('RGB'), dtype=np.float32)
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]


def box(y, r):
    p = np.pad(y, r, mode='edge')
    c = np.cumsum(np.cumsum(p, 0), 1)
    c = np.pad(c, ((1, 0), (1, 0)))
    n = 2 * r + 1
    return (c[n:, n:] - c[:-n, n:] - c[n:, :-n] + c[:-n, :-n]) / (n * n)


def main():
    folder = sys.argv[1]
    frames = sorted(glob.glob(os.path.join(folder, 'frame-*.png')))
    h, w = luma(frames[0]).shape
    sx, sy = w / 1920.0, h / 1080.0
    S = lambda x0, y0, x1, y1: (int(x0 * sx), int(y0 * sy), int(x1 * sx), int(y1 * sy))
    bands = {'snow': S(0, 40, 1920, 300), 'shafts': S(0, 40, 1920, 600), 'bodies': S(250, 300, 1650, 700)}
    values = {k: [] for k in bands}
    prev = prevb = None
    for f in frames:
        y = luma(f)
        yb = box(y[bands['shafts'][1]:bands['shafts'][3], bands['shafts'][0]:bands['shafts'][2]], 12)
        if prev is not None:
            d = np.abs(y - prev)
            for k in ('snow', 'bodies'):
                x0, y0, x1, y1 = bands[k]
                values[k].append(int((d[y0:y1, x0:x1] > 10).sum()))
            values['shafts'].append(float(np.abs(yb - prevb).mean()))
        prev, prevb = y, yb
    print(f'{len(frames)} frames, {w}x{h}; value i is the change from frame i-1 to frame i')
    for k, v in values.items():
        v = np.array(v, dtype=float)
        med = np.median(v)
        i = int(v.argmax())
        over = [j + 1 for j in np.nonzero(v > 2 * max(med, 1e-9))[0]]
        print(f'  {k:<7} median {med:9.3f}  largest {v.max():9.3f} at frame {i + 1}  ratio {v.max() / max(med, 1e-9):5.2f}  '
              f'above 2x: {len(over)}{" " + str(over[:20]) if over else ""}')
    if len(sys.argv) > 3 and sys.argv[2] == '--strip':
        c = int(sys.argv[3])
        pick = range(max(0, c - 3), min(len(frames), c + 3))
        x0, y0 = int(700 * sx), int(60 * sy)
        cw, ch = int(480 * sx), int(240 * sy)
        tiles = [Image.open(frames[j]).convert('RGB').crop((x0, y0, x0 + cw, y0 + ch)).resize((cw * 2, ch * 2), Image.NEAREST)
                 for j in pick]
        sheet = Image.new('RGB', (cw * 2 * 3 + 8, ch * 2 * 2 + 4), (255, 255, 255))
        for n, t in enumerate(tiles):
            sheet.paste(t, ((n % 3) * (cw * 2 + 4), (n // 3) * (ch * 2 + 4)))
        sheet.save(sys.argv[4])
        print('strip of frames', list(pick), '->', sys.argv[4])


if __name__ == '__main__':
    main()
