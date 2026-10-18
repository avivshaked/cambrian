"""The creature close-up's near rock: a 100% crop of it in each set, its mean luma and its local
contrast (mean s.d. in 16 px blocks), and a sheet of the crops side by side.

    python rock.py <out.png> <label=path> ...
"""
import sys
import numpy as np
from PIL import Image

BOX = (1600, 1590, 3700, 1950)   # the near reef cap (the "rock") in the 3840x2160 creature frame


def luma(a):
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]


def main():
    out = sys.argv[1]
    crops = []
    for arg in sys.argv[2:]:
        label, path = arg.split('=', 1)
        a = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64)
        x0, y0, x1, y1 = BOX
        c = a[y0:y1, x0:x1]
        y = luma(c)
        h, w = (y.shape[0] // 16) * 16, (y.shape[1] // 16) * 16
        local = y[:h, :w].reshape(h // 16, 16, w // 16, 16).std(axis=(1, 3)).mean()
        p5, p95 = np.percentile(y, [5, 95])
        print(f'{label:<12} mean {y.mean():6.1f}  local16 {local:5.2f}  5-95 {p95 - p5:6.1f}')
        crops.append(Image.fromarray(c.astype(np.uint8)))
    w, h = crops[0].size
    s = Image.new('RGB', (w, h * len(crops) + 4 * (len(crops) - 1)), (255, 255, 255))
    for i, c in enumerate(crops):
        s.paste(c, (0, i * (h + 4)))
    s.save(out)


if __name__ == '__main__':
    main()
