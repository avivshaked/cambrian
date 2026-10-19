"""The HDRP spike's side-by-side film (D202): the URP theatre on the left, HDRP on the right.

    python -I sidebyside.py --out <film.mp4> --fps 24 --moment "<name>|<urp take dir>|<hdrp take dir>" [...]

Each moment is one safari take rendered in both theatres with the take clock
(EVOSIM_THEATRE_SAFARI_CLOCK=1), so frame N of the two takes shows the water at the same instant.
The frames are paired by number, laid side by side at full size, labelled small in the top left
of each half ("URP, today's theatre" and "HDRP") with the moment's name, and the moments follow
each other in the order given. A take that has fewer frames than its partner ends the moment at
the shorter one, and the tool says so.

Prints, for every moment and each half, the mean luma and the largest frame-to-frame jump over the
median jump, so a flicker or a drift is a number and not an impression.
"""
import argparse
import glob
import os
import subprocess

import numpy as np
from PIL import Image, ImageDraw, ImageFont

LABELS = ("URP, today's theatre", "HDRP")


def font(size):
    for path in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def label(img, text, f):
    draw = ImageDraw.Draw(img, "RGBA")
    box = draw.textbbox((0, 0), text, font=f)
    w, h = box[2] - box[0], box[3] - box[1]
    draw.rectangle((16, 14, 16 + w + 20, 14 + h + 16), fill=(0, 0, 0, 110))
    draw.text((26, 20 - box[1]), text, font=f, fill=(235, 240, 240, 255))


def luma(img):
    a = np.asarray(img.convert("RGB"), dtype=np.float64)
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--fps", type=int, default=24)
    ap.add_argument("--moment", action="append", required=True)
    args = ap.parse_args()

    moments = []
    for spec in args.moment:
        name, urp, hdrp = spec.split("|")
        a = sorted(glob.glob(os.path.join(urp, "frame-*.png")))
        b = sorted(glob.glob(os.path.join(hdrp, "frame-*.png")))
        if not a or not b:
            raise SystemExit(f"{name}: no frames in {urp if not a else hdrp}")
        n = min(len(a), len(b))
        if len(a) != len(b):
            print(f"{name}: URP has {len(a)} frames and HDRP {len(b)}; the moment ends at {n}")
        moments.append((name, a[:n], b[:n]))

    first = Image.open(moments[0][1][0])
    w, h = first.size
    f = font(max(14, h // 45))
    enc = subprocess.Popen(
        ["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{2 * w}x{h}",
         "-r", str(args.fps), "-i", "-", "-c:v", "libx264", "-crf", "16", "-preset", "slow",
         "-pix_fmt", "yuv420p", "-movflags", "+faststart", args.out],
        stdin=subprocess.PIPE)

    for name, a, b in moments:
        means = ([], [])
        prev = [None, None]
        jumps = ([], [])
        for pa, pb in zip(a, b):
            frame = Image.new("RGB", (2 * w, h))
            for side, path in enumerate((pa, pb)):
                img = Image.open(path).convert("RGB")
                if img.size != (w, h):
                    img = img.resize((w, h), Image.LANCZOS)
                y = luma(img)
                means[side].append(y.mean())
                if prev[side] is not None:
                    jumps[side].append(np.abs(y - prev[side]).mean())
                prev[side] = y
                label(img, f"{LABELS[side]}  ·  {name}", f)
                frame.paste(img, (side * w, 0))
            enc.stdin.write(frame.tobytes())
        for side in (0, 1):
            j = np.array(jumps[side]) if jumps[side] else np.zeros(1)
            med = max(1e-6, float(np.median(j)))
            print(f"{name:10s} {LABELS[side]:22s} frames {len(a):4d}  mean luma {np.mean(means[side]):5.1f}"
                  f"  jump median {med:5.2f}  largest {j.max():5.2f}  ratio {j.max() / med:4.1f}")

    enc.stdin.close()
    if enc.wait() != 0:
        raise SystemExit("ffmpeg failed")
    print("wrote", args.out)


if __name__ == "__main__":
    main()
