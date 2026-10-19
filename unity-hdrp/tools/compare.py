"""The HDRP spike's render check: each moment in the agreed film, today's theatre and HDRP, side by side.

    python -I compare.py --out <sheet.png> --moment "<name>|<film.mp4>@<seconds>|<urp.png>|<hdrp.png>" [...]

CLAUDE.md asks every test render to be checked against the last film the owner accepted before the
owner sees it: a sheet read for size, brightness, the bed, the reef, the caustics, the shafts and the
bodies, and a number for what a number can say. For the spike (D202) each row is one moment: a frame
of the round 57 film's final render of the same kind of shot (its Colony, Arrival, Floor or
Descent), then the moment in the URP theatre, then in HDRP. Every picture is scaled to one height.

Prints, for every picture, the mean luma (Rec. 709, 0-255), the 5th and 95th percentiles of luma,
and the mean of each channel, so a brightness or colour shift is a number and not an impression.
A film frame is taken with ffmpeg at the given second of the film. Any of the three may be "-" for
a cell left empty.
"""
import argparse
import subprocess
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

CELL_H = 540
COLUMNS = ("round 57 film (agreed)", "today's theatre (URP)", "HDRP spike")


def film_frame(spec, scratch):
    path, seconds = spec.rsplit("@", 1)
    out = Path(scratch) / f"film-{abs(hash(spec))}.png"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", seconds, "-i", path, "-frames:v", "1", str(out)],
                   check=True)
    return out


def numbers(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float64)
    luma = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    return (luma.mean(), np.percentile(luma, 5), np.percentile(luma, 95),
            a[..., 0].mean(), a[..., 1].mean(), a[..., 2].mean())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--moment", action="append", required=True)
    args = ap.parse_args()

    rows = []
    with tempfile.TemporaryDirectory(dir=Path(args.out).parent) as scratch:
        for spec in args.moment:
            name, film, urp, hdrp = spec.split("|")
            cells = []
            for i, src in enumerate((film, urp, hdrp)):
                if src == "-":
                    cells.append(None)
                    continue
                path = film_frame(src, scratch) if i == 0 else Path(src)
                img = Image.open(path).convert("RGB")
                img.load()
                cells.append(img)
            rows.append((name, cells))

        width = 0
        for _, cells in rows:
            for img in cells:
                if img is not None:
                    width = max(width, round(img.width * CELL_H / img.height))
        width = width or round(CELL_H * 16 / 9)

        head, label = 28, 24
        sheet = Image.new("RGB", (width * 3, head + len(rows) * (CELL_H + label)), (16, 16, 16))
        draw = ImageDraw.Draw(sheet)
        for c, title in enumerate(COLUMNS):
            draw.text((c * width + 8, 8), title, fill=(230, 230, 230))

        print(f"{'moment':<28}{'column':<26}{'luma':>7}{'p5':>7}{'p95':>7}{'R':>7}{'G':>7}{'B':>7}")
        for r, (name, cells) in enumerate(rows):
            y = head + r * (CELL_H + label)
            draw.text((8, y + 4), name, fill=(230, 230, 230))
            for c, img in enumerate(cells):
                if img is None:
                    continue
                scaled = img.resize((round(img.width * CELL_H / img.height), CELL_H), Image.LANCZOS)
                sheet.paste(scaled, (c * width, y + label))
                n = numbers(img)
                draw.text((c * width + 8, y + label + CELL_H - 18),
                          f"luma {n[0]:.1f}  p5 {n[1]:.0f}  p95 {n[2]:.0f}", fill=(255, 220, 120))
                print(f"{name:<28}{COLUMNS[c][:24]:<26}" + "".join(f"{v:>7.1f}" for v in n))

    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.out)
    print("wrote", args.out)


if __name__ == "__main__":
    main()
