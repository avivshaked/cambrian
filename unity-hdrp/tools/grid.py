"""A labelled grid of frames: columns are takes, rows are frame pairs.

    python -I grid.py --out sheet.png --width 960 --col "label|take dir|f0,f1,f2" [--col ...] [--crop x,y,w,h]

Each column names its frames by number (frame-NNNNNN.png); the rows are the n-th frame of every
column. --crop cuts the same box out of every frame first (pixels of the frame), for a close look.
Prints each cell's mean luma (Rec. 709, 0-255).
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def font(size):
    for path in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--width", type=int, default=960)
    ap.add_argument("--col", action="append", required=True)
    ap.add_argument("--crop", default=None)
    args = ap.parse_args()

    cols = []
    for spec in args.col:
        label, folder, frames = spec.split("|")
        cols.append((label, folder, [int(f) for f in frames.split(",")]))
    rows = max(len(c[2]) for c in cols)
    crop = tuple(int(v) for v in args.crop.split(",")) if args.crop else None

    cells = []
    for label, folder, frames in cols:
        col = []
        for n in frames:
            img = Image.open(os.path.join(folder, f"frame-{n:06d}.png")).convert("RGB")
            if crop:
                scale = img.size[1] / 1080.0
                x, y, w, h = (int(round(v * scale)) for v in crop)
                img = img.crop((x, y, x + w, y + h))
            h = int(round(img.size[1] * args.width / img.size[0]))
            img = img.resize((args.width, h), Image.LANCZOS)
            a = np.asarray(img, dtype=np.float64)
            luma = (0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]).mean()
            print(f"{label:28s} frame {n:6d}  luma {luma:5.1f}")
            col.append((img, n, luma))
        cells.append(col)

    head = 28
    cell_h = max(img.size[1] for col in cells for img, _, _ in col)
    sheet = Image.new("RGB", (args.width * len(cols), head + rows * cell_h), (16, 16, 16))
    draw = ImageDraw.Draw(sheet)
    f = font(16)
    for c, (label, _, _) in enumerate(cols):
        draw.text((c * args.width + 8, 5), label, font=f, fill=(230, 230, 230))
        for r, (img, n, luma) in enumerate(cells[c]):
            sheet.paste(img, (c * args.width, head + r * cell_h))
            draw.text((c * args.width + 8, head + r * cell_h + 6), f"frame {n}  luma {luma:.1f}", font=f, fill=(255, 220, 120))
    sheet.save(args.out)
    print("wrote", args.out)


if __name__ == "__main__":
    main()
