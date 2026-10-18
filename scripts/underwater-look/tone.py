"""TheatreToneStep.shader's curve applied offline to a set taken with the tone step off, for tuning
its three dials without a Unity run per guess. The PNGs hold the display-encoded colour the shader
works on (sRGB), so the arithmetic is the shader's own, short of 8-bit rounding.

    python tone.py <set> <out-set> --black 0.04 --power 1.25 --pivot 0.3
"""
import argparse
import os
import shutil
import numpy as np
from PIL import Image

IT = r'D:\Projects\experiments\evolution-simulator\scratch\look-build\iter'


def curve(y, black, power, pivot, shoulder=None):
    shoulder = power if shoulder is None else shoulder
    t = np.clip((y - black) / max(1e-4, 1 - black), 0, 1)
    low = pivot * np.power(np.clip(t / pivot, 0, None), power)
    high = 1 - (1 - pivot) * np.power(np.clip((1 - t) / (1 - pivot), 0, None), shoulder)
    w = 0.5 * min(pivot, 1 - pivot)
    x = np.clip((t - (pivot - w)) / (2 * w), 0, 1)
    s = x * x * (3 - 2 * x)
    return low + (high - low) * s   # blended across the pivot, as TheatreToneStep.shader


def apply(a, black, power, pivot, shoulder=None):
    e = a / 255.0
    y = e @ np.array([0.2126, 0.7152, 0.0722])
    ratio = curve(y, black, power, pivot, shoulder) / np.maximum(1e-5, y)
    most = e.max(axis=2)
    cap = np.maximum(1.0, 1.0 / np.maximum(1e-5, most))
    ratio = np.where(ratio > 1, np.minimum(ratio, cap), ratio)
    return np.clip(e * ratio[..., None] * 255.0 + 0.5, 0, 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('dst')
    ap.add_argument('--black', type=float, default=0.04)
    ap.add_argument('--power', type=float, default=1.25)
    ap.add_argument('--pivot', type=float, default=0.3)
    ap.add_argument('--shoulder', type=float, default=None)
    ap.add_argument('--only', default='')
    args = ap.parse_args()
    src, dst = os.path.join(IT, args.src), os.path.join(IT, args.dst)
    os.makedirs(dst, exist_ok=True)
    for name in os.listdir(src):
        if not name.endswith('.png'):
            continue
        if args.only and not any(k in name for k in args.only.split(',')):
            continue
        a = np.asarray(Image.open(os.path.join(src, name)).convert('RGB'), dtype=np.float64)
        # The label strip is burnt in after the render, so it is kept as it was.
        out = apply(a, args.black, args.power, args.pivot, args.shoulder)
        top = int(a.shape[0] * 0.025)
        out[:top] = a[:top].astype(np.uint8)
        Image.fromarray(out).save(os.path.join(dst, name))


if __name__ == '__main__':
    main()
