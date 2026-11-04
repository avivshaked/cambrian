"""Computes a world's blade stops, so the theatre's algal blades land on a target colour in that world's own light.

The blades take their colour from the photographed ramp's two stops (EVOSIM_THEATRE_ALGA_RAMP_STOP_A and _B, linear colour),
and the light through the water moves what reaches the screen: the same stops give a different green in a different world.
Given one rendered frame and the stops it was rendered with, this measures the blades' mean colour on screen and prints the
stops that would move it to the wanted hue, saturation and lightness (default hue 105 degrees, saturation 0.40, lightness
0.25: the photographs' sea lettuce).

Usage: python -I scripts/theatre-blade-stops.py --frame FRAME.png --stop-a R,G,B --stop-b R,G,B
           [--want-hue 105 --want-sat 0.40 --want-light 0.25] [--crop X,Y,W,H]
       python -I scripts/theatre-blade-stops.py --measured R,G,B --stop-a R,G,B --stop-b R,G,B     (skips the frame; sRGB 0-255)

The blade mask is a = rgb/255, mx = max channel, s = (mx - min) / max(mx, 1e-4): s above 0.3, mx above 0.15, r and g above b,
and hue in 70 to 125 degrees. The hue band leaves the round producers out (their hue is about 130 to 145); the pixels in 125
to 150 degrees are printed as "round producers" for information only.

The correction is one step of arithmetic: the ratio of the wanted colour (rounded to 8-bit sRGB) to the measured one in linear light, per channel;
its luma (weights 0.2126, 0.7152, 0.0722) squared as the scale, because a blade's lightness goes as the square root of the
stop's luma (AlgaBladeLumaShare 0.5); and its chromaticity (the ratio over its luma) as the hue. The new stop is the old one
times chromaticity times scale, for both stops, clamped into 0 to 1. It is inexact, because the ramp's share is 0.85, the
ratio is bounded in the shader, and the light through adds terms the stop does not scale. Render the new stops and measure
again; a second step converges. Standard library, numpy and Pillow only; writes nothing.
"""
import argparse
import colorsys
import sys

import numpy as np

LUMA = np.array([0.2126, 0.7152, 0.0722])
HUE_BAND = (70.0, 125.0)
ROUND_BAND = (125.0, 150.0)


def srgb_to_linear(c):
    c = np.asarray(c, dtype=float)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def parse3(text, name):
    try:
        v = [float(p) for p in text.split(",")]
    except ValueError:
        v = []
    if len(v) != 3:
        sys.exit("%s must be three numbers r,g,b ('%s')" % (name, text))
    return np.array(v)


def hue_degrees(a):
    """Hue in degrees of an (..., 3) array of rgb in 0 to 1, with the saturation s and the max channel mx."""
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(axis=-1)
    mn = a.min(axis=-1)
    d = np.maximum(mx - mn, 1e-9)
    h = np.where(mx == r, ((g - b) / d) % 6.0, np.where(mx == g, (b - r) / d + 2.0, (r - g) / d + 4.0)) * 60.0
    s = (mx - mn) / np.maximum(mx, 1e-4)
    return h, s, mx


def measure(frame, crop):
    from PIL import Image
    img = np.asarray(Image.open(frame).convert("RGB"), dtype=float) / 255.0
    if crop:
        x, y, w, h = [int(v) for v in crop.split(",")]
        img = img[y:y + h, x:x + w]
    hue, s, mx = hue_degrees(img)
    r, g, b = img[..., 0], img[..., 1], img[..., 2]
    base = (s > 0.3) & (mx > 0.15) & (g > b)
    blade = base & (r > b) & (hue >= HUE_BAND[0]) & (hue <= HUE_BAND[1])
    # No red over blue for the round producers: above hue 120 blue passes red, so that test would leave none of them.
    rnd = base & (hue > ROUND_BAND[0]) & (hue <= ROUND_BAND[1])
    total = float(blade.size)
    if blade.sum() == 0:
        sys.exit("no blade pixels in the frame (mask is empty)")
    mean = img[blade].mean(axis=0) * 255.0
    rnd_info = None
    if rnd.sum() > 0:
        rnd_info = (rnd.sum() / total, img[rnd].mean(axis=0) * 255.0)
    return mean, blade.sum() / total, rnd_info


def hls_of_srgb255(rgb):
    h, l, s = colorsys.rgb_to_hls(*(np.asarray(rgb, dtype=float) / 255.0))
    return h * 360.0, l, s


def wanted_srgb255(hue, sat, light):
    # Rounded to the 8-bit colour a picture would hold, which is what the worked case (49,63,34 to 105 / 0.40 / 0.25) used.
    return np.round(np.array(colorsys.hls_to_rgb(hue / 360.0, light, sat)) * 255.0)


def new_stops(measured255, wanted255, stop_a, stop_b):
    """One step: returns (new A, new B, clamped, ratio, chromaticity, scale)."""
    ratio = srgb_to_linear(np.asarray(wanted255) / 255.0) / np.maximum(srgb_to_linear(np.asarray(measured255) / 255.0), 1e-6)
    luma = float(np.dot(LUMA, ratio))
    chroma = ratio / luma
    scale = luma ** 2
    a = np.asarray(stop_a) * chroma * scale
    b = np.asarray(stop_b) * chroma * scale
    clamped = bool((a < 0).any() or (a > 1).any() or (b < 0).any() or (b > 1).any())
    return np.clip(a, 0.0, 1.0), np.clip(b, 0.0, 1.0), clamped, ratio, chroma, scale


def fmt(v):
    return ",".join("%.4f" % x for x in v)


def main():
    ap = argparse.ArgumentParser(description="Blade stops for a world's own light (see the module docstring).")
    ap.add_argument("--frame", help="one rendered frame (png)")
    ap.add_argument("--measured", help="blade mean sRGB 0-255 as r,g,b, in place of --frame")
    ap.add_argument("--stop-a", required=True, help="linear r,g,b the frame was rendered with")
    ap.add_argument("--stop-b", required=True, help="linear r,g,b the frame was rendered with")
    ap.add_argument("--want-hue", type=float, default=105.0)
    ap.add_argument("--want-sat", type=float, default=0.40)
    ap.add_argument("--want-light", type=float, default=0.25)
    ap.add_argument("--crop", help="x,y,w,h of the part of the frame to measure")
    args = ap.parse_args()
    if bool(args.frame) == bool(args.measured):
        sys.exit("give exactly one of --frame and --measured")

    stop_a = parse3(args.stop_a, "--stop-a")
    stop_b = parse3(args.stop_b, "--stop-b")
    if args.frame:
        measured, share, rnd = measure(args.frame, args.crop)
        print("blade pixels: %.2f%% of the frame (mask: hue %g-%g)" % (share * 100.0, *HUE_BAND))
        if rnd:
            h, l, s = hls_of_srgb255(rnd[1])
            print("round producers (hue %g-%g, information only): %.2f%% of the frame, mean HLS %.1f %.3f %.3f"
                  % (*ROUND_BAND, rnd[0] * 100.0, h, l, s))
    else:
        measured = parse3(args.measured, "--measured")

    h, l, s = hls_of_srgb255(measured)
    print("blade mean sRGB %.1f,%.1f,%.1f  HLS hue %.1f deg, lightness %.3f, saturation %.3f" % (*measured, h, l, s))
    wanted = wanted_srgb255(args.want_hue, args.want_sat, args.want_light)
    print("wanted sRGB %.1f,%.1f,%.1f  (hue %g deg, saturation %g, lightness %g)"
          % (*wanted, args.want_hue, args.want_sat, args.want_light))

    a, b, clamped, ratio, chroma, scale = new_stops(measured, wanted, stop_a, stop_b)
    print("ratio %s  chromaticity %s  scale %.4f" % (fmt(ratio), fmt(chroma), scale))
    if clamped:
        print("CLAMPED: a channel left 0 to 1 and was clamped; the stops will not reach the wanted colour in one step.")
    print("EVOSIM_THEATRE_ALGA_RAMP_STOP_A=" + fmt(a))
    print("EVOSIM_THEATRE_ALGA_RAMP_STOP_B=" + fmt(b))
    print("One step of arithmetic: render with these stops and measure again. It is inexact because the ramp's share is "
          "0.85, the ratio is bounded in the shader, and the light through adds terms the stop does not scale.")


if __name__ == "__main__":
    main()
