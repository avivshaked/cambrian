"""Checks a frame sequence before anyone sees it: mean luma per frame, the frame-to-frame change
(a pop is a change far above its neighbours'), and a contact sheet of every 15th frame.

    python check_anim.py <frames dir> <sheet.png> [first] [last]
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

d, sheet = sys.argv[1], sys.argv[2]
first = int(sys.argv[3]) if len(sys.argv) > 3 else 1
last = int(sys.argv[4]) if len(sys.argv) > 4 else 150
prev, luma, diff = None, [], []
for f in range(first, last + 1):
    a = np.asarray(Image.open(os.path.join(d, "frame_%04d.png" % f)).convert("RGB").resize((480, 270)), np.float32)
    y = a @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    luma.append(float(y.mean()))
    diff.append(float(np.abs(y - prev).mean()) if prev is not None else 0.0)
    prev = y
luma, diff = np.array(luma), np.array(diff)
print("luma: min %.2f max %.2f (first %.2f, last %.2f)" % (luma.min(), luma.max(), luma[0], luma[-1]))
print("change per frame: median %.3f, max %.3f at frame %d" % (np.median(diff[1:]), diff[1:].max(), first + 1 + int(diff[1:].argmax())))
for i in range(2, len(diff) - 1):
    local = np.median(diff[max(1, i - 4):i + 5])
    if diff[i] > 3 * local and diff[i] > 0.5:
        print("  POP? frame %d: %.3f against a local median of %.3f" % (first + i, diff[i], local))
picks = list(range(first, last + 1, 15))
if picks[-1] != last:
    picks.append(last)
cols = 4
rows = (len(picks) + cols - 1) // cols
S = Image.new("RGB", (cols * 480, rows * 270))
for k, f in enumerate(picks):
    im = Image.open(os.path.join(d, "frame_%04d.png" % f)).convert("RGB").resize((480, 270))
    S.paste(im, ((k % cols) * 480, (k // cols) * 270))
    ImageDraw.Draw(S).text(((k % cols) * 480 + 6, (k // cols) * 270 + 4), str(f), fill=(255, 255, 0))
S.save(sheet)
print("sheet", sheet)
