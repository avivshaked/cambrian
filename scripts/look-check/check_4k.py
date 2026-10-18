"""The round 57 film's 4K pass, first take read against the look test.

  python scratch/look-check/check_4k.py <take folder of frame-NNNNNN.png> <name>

Prints mean luma (0-255) of every 20th frame, and the largest frame-to-frame jump (mean absolute difference at
480x270) against the median jump. Writes scratch/look-check/4k/<name>-sheet.png: a row of the take's first,
middle and last frames, a row of the look test's 1.1a and 7.1b middle frames, and the liked clip's middle frame.
"""
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
LOOK = ROOT / "scratch" / "owner" / "round57-look-test"
LIKED = ROOT / "scratch" / "owner" / "underwater-look-2" / "clip-new-look.mp4"
OUT = ROOT / "scratch" / "look-check" / "4k"
W, H = 640, 360


def mid_frame(clip, name):
    OUT.mkdir(parents=True, exist_ok=True)
    out = OUT / ("ref-" + name + ".png")
    if not out.exists():
        d = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0",
                                  str(clip)], capture_output=True, text=True, check=True).stdout.strip())
        subprocess.run(["ffmpeg", "-loglevel", "error", "-ss", str(d / 2), "-i", str(clip), "-frames:v", "1",
                        str(out)], check=True)
    return Image.open(out).convert("RGB")


def main():
    take, name = Path(sys.argv[1]), sys.argv[2]
    if take.is_file():  # an encoded take: its frames at 960x540, for the numbers and the sheet
        size = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
                               "stream=width,height", "-of", "csv=p=0", str(take)],
                              capture_output=True, text=True, check=True).stdout.strip()
        print("%s is %s" % (take.name, size.replace(",", "x")))
        d = OUT / name
        d.mkdir(parents=True, exist_ok=True)
        if not any(d.glob("frame-*.png")):
            subprocess.run(["ffmpeg", "-loglevel", "error", "-i", str(take), "-vf", "scale=960:540",
                            str(d / "frame-%06d.png")], check=True)
        take = d
    frames = sorted(take.glob("frame-*.png"))
    small, lumas = [], []
    for i, f in enumerate(frames):
        im = Image.open(f).convert("RGB")
        s = np.asarray(im.resize((480, 270), Image.BILINEAR), dtype=np.float32)
        small.append(s)
        if i % 20 == 0:
            lumas.append(float((0.2126 * s[..., 0] + 0.7152 * s[..., 1] + 0.0722 * s[..., 2]).mean()))
    jumps = [float(np.abs(small[i] - small[i - 1]).mean()) for i in range(1, len(small))]
    big = int(np.argmax(jumps))
    print("%s: %d frames at %s; mean luma %.1f (least %.1f, most %.1f)" % (
        name, len(frames), "x".join(map(str, Image.open(frames[0]).size)), np.mean(lumas), min(lumas), max(lumas)))
    print("jump: median %.2f, largest %.2f between frames %d and %d (%.1f times the median)" % (
        np.median(jumps), jumps[big], big, big + 1, jumps[big] / max(np.median(jumps), 1e-6)))
    first_jump = jumps[0]
    print("first jump (frame 0 to 1): %.2f" % first_jump)
    picks = [frames[0], frames[len(frames) // 2], frames[-1]]
    refs = [("look 1.1a", mid_frame(LOOK / "1.1a.mp4", "1.1a")), ("look 7.1b", mid_frame(LOOK / "7.1b.mp4", "7.1b")),
            ("liked clip", mid_frame(LIKED, "liked"))]
    sheet = Image.new("RGB", (W * 3, H * 2), (20, 20, 20))
    draw = ImageDraw.Draw(sheet)
    for c, f in enumerate(picks):
        sheet.paste(Image.open(f).convert("RGB").resize((W, H), Image.BILINEAR), (c * W, 0))
        draw.text((c * W + 6, 4), "4K %s %s" % (name, f.stem), fill=(255, 255, 0))
    for c, (label, im) in enumerate(refs):
        sheet.paste(im.resize((W, H), Image.BILINEAR), (c * W, H))
        draw.text((c * W + 6, H + 4), label, fill=(255, 255, 0))
    out = OUT / (name + "-sheet.png")
    sheet.save(out)
    print("sheet:", out)


if __name__ == "__main__":
    main()
