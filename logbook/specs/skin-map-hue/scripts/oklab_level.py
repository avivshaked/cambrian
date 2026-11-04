import math

OK_BODY_L, OK_CHROMA, DARK, PALE, PALE_REACH = 0.68, 0.14, 0.6, 0.95, 0.7


def to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def to_gam(c):
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def cbrt(x):
    return -((-x) ** (1 / 3)) if x < 0 else x ** (1 / 3)


def to_lch(c):
    r, g, b = (to_lin(v) for v in c)
    l = cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
    m = cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
    s = cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
    L = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s
    a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s
    bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s
    return L, math.hypot(a, bb), math.atan2(bb, a)


def try_lch(L, C, h):
    a, bb = C * math.cos(h), C * math.sin(h)
    l = (L + 0.3963377774 * a + 0.2158037573 * bb) ** 3
    m = (L - 0.1055613458 * a - 0.0638541728 * bb) ** 3
    s = (L - 0.0894841775 * a - 1.2914855480 * bb) ** 3
    lin = (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
           -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
           -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)
    inside = all(-1e-5 <= v <= 1 + 1e-5 for v in lin)
    return tuple(to_gam(min(1.0, max(0.0, v))) for v in lin), inside


def from_lch(L, C, h):
    rgb, ok = try_lch(L, C, h)
    if not ok:
        lo, hi = 0.0, C
        for _ in range(12):
            mid = 0.5 * (lo + hi)
            if try_lch(L, mid, h)[1]:
                lo = mid
            else:
                hi = mid
        rgb, _ = try_lch(L, lo, h)
    return rgb


def level(body, rim, through, value=0.0, rank=None, cap=OK_CHROMA):
    cb, cr, ct = to_lch(body), to_lch(rim), to_lch(through)
    t = OK_BODY_L
    if value > 0 and rank is not None:
        s = value * (2 * rank - 1)
        if s < 0:
            t *= 1 + DARK * s
        elif s > 0:
            t += (PALE - t) * PALE_REACH * s
    shift = t - cb[0]
    out = []
    for L, C, h in (cb, cr, ct):
        out.append(from_lch(min(0.98, max(0.02, L + shift)), min(C, cap), h))
    return out


if __name__ == '__main__':
    for name, c in (('yellow', (1, 1, 0)), ('magenta', (1, 0, 1)), ('blue', (0, 0, 1))):
        for value, rank in ((0.0, None), (1.0, 0.0), (1.0, 1.0)):
            body, rim, thr = level(c, c, c, value, rank)
            L, C, h = to_lch(body)
            print('%-8s value %.1f rank %s -> body L %.4f C %.4f rgb %s' %
                  (name, value, rank, L, C, tuple(round(v, 3) for v in body)))
