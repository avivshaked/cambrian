"""The theatre skin's vertex stage, transcribed from TheatreBody.shader and TheatreWater.hlsl.

Everything here is the shader's arithmetic run once on the CPU, in numpy, so Blender draws the
same shapes the theatre draws: a box pillowed, tapered and bent; a flat box drawn as a blade;
every part carved inward by two octaves of value noise, deeper at a joint. The hash is the
shader's own (sin times 43758.5453), evaluated in double rather than the GPU's float, so the
dents fall in other places than the theatre's; their scale, depth and character are the same.

Units follow the shader: a part's object units are its full size on each axis (the visual's
localScale), so an object coordinate of 0.5 is the box's face.
"""
import numpy as np

def frac(x):
    return x - np.floor(x)

def saturate(x):
    return np.clip(x, 0.0, 1.0)

def smoothstep(a, b, x):
    t = saturate((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)

# ---------------------------------------------------------------- TheatreWater.hlsl
def hash1(p):
    return frac(np.sin(p @ np.array([127.1, 311.7, 74.7])) * 43758.5453123)

def value_noise(p):
    cell = np.floor(p)
    f = p - cell
    u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0)
    c = lambda dx, dy, dz: hash1(cell + np.array([dx, dy, dz], dtype=float))
    a, b, cc, d = c(0, 0, 0), c(1, 0, 0), c(0, 1, 0), c(1, 1, 0)
    e, g, h, i = c(0, 0, 1), c(1, 0, 1), c(0, 1, 1), c(1, 1, 1)
    k0, k1, k2, k3 = a, b - a, cc - a, e - a
    k4, k5, k6 = a - b - cc + d, a - cc - e + h, a - b - e + g
    k7 = -a + b + cc - d + e - g - h + i
    ux, uy, uz = u[:, 0], u[:, 1], u[:, 2]
    return (k0 + k1 * ux + k2 * uy + k3 * uz + k4 * ux * uy + k5 * uy * uz + k6 * uz * ux
            + k7 * ux * uy * uz)

def carve_offset(seed):
    return 64.0 * frac(seed * np.array([97.13, 61.71, 43.37]) + np.array([0.13, 0.57, 0.91]))

def carve_detail(smallest):
    return float(np.clip(np.sqrt(smallest / 0.08), 1.0, 3.0))

def carve(p, seed, lobe_gain, wrinkle_gain, detail):
    lobe_cycles = 1.8
    wrinkle_cycles = 5.0 * lobe_cycles * detail
    off = carve_offset(seed)
    lobe = value_noise(p * lobe_cycles + off)
    wrinkle = value_noise(p * wrinkle_cycles + off * 1.7)
    total = max(1e-4, lobe_gain + wrinkle_gain)
    raw = (lobe_gain * lobe + wrinkle_gain * wrinkle) / total
    t = saturate((raw - 0.28) / (0.72 - 0.28))
    return t * t * (3.0 - 2.0 * t)

def pinch(p, anchor):
    if anchor[3] <= 0.0:
        return np.zeros(len(p))
    d = np.linalg.norm(p - np.asarray(anchor[:3]), axis=1) / anchor[3]
    return 1.0 - smoothstep(0.0, 1.0, saturate(d))

def own_shift(individual):
    return (np.array([frac(individual * 11.31 + 0.17), frac(individual * 23.93 + 0.61),
                      frac(individual * 37.17 + 0.43)]) - 0.5) * 0.16

# ---------------------------------------------------------------- TheatreBody.shader: the box
def shape_box(p, n, he, smallest, mesh_half, seed, taper_fraction=0.35, bend_fraction=0.15):
    """ShapeBox: taper down the longest axis, one half wave of bend across it, clamped."""
    he = np.asarray(he, dtype=float)
    if he[0] >= he[1] and he[0] >= he[2]:
        A = np.array([1.0, 0, 0])
    elif he[1] >= he[2]:
        A = np.array([0, 1.0, 0])
    else:
        A = np.array([0, 0, 1.0])
    B = np.array([A[2], A[0], A[1]])
    C = np.array([A[1], A[2], A[0]])
    across = 1.0 - A
    along = p @ A
    end = -1.0 if frac(seed * 13.37 + 0.271) < 0.5 else 1.0
    angle = 6.2831853 * frac(seed * 7.919 + 0.613)
    u = saturate(0.5 + 0.5 * end * along / mesh_half)
    ease = u * u * (3.0 - 2.0 * u)
    ease_slope = 6.0 * u * (1.0 - u) * (0.5 * end / mesh_half)
    taper = 1.0 - taper_fraction * ease
    taper_slope = -taper_fraction * ease_slope
    amp = (B * (np.cos(angle) / max(1e-6, 2.0 * (he @ B))) +
           C * (np.sin(angle) / max(1e-6, 2.0 * (he @ C))))
    amp = amp * bend_fraction * smallest
    t = saturate(0.5 + 0.5 * along / mesh_half)
    wave = np.sin(np.pi * t)
    wave_slope = np.pi * np.cos(np.pi * t) * (0.5 / mesh_half)
    room = 1.0 - saturate(np.abs(amp) / mesh_half)
    scale = A[None, :] + across[None, :] * (taper[:, None] * room[None, :])
    offset = across[None, :] * (amp[None, :] * wave[:, None])
    scale_slope = across[None, :] * (taper_slope[:, None] * room[None, :])
    offset_slope = across[None, :] * (amp[None, :] * wave_slope[:, None])
    scale = np.maximum(scale, 1e-3)
    q = np.clip(scale * p + offset, -mesh_half, mesh_half)
    shear = across[None, :] * ((scale_slope * p + offset_slope) / scale)
    m = n / scale - A[None, :] * np.sum(shear * n, axis=1)[:, None]
    return q, m

# ---------------------------------------------------------------- TheatreBody.shader: the leaf
def leaf_of(seed, own):
    r = [frac(seed * k + c) for k, c in ((17.137, 0.371), (29.713, 0.113), (41.307, 0.731), (53.911, 0.293),
                                         (61.717, 0.537), (73.019, 0.619), (83.231, 0.157), (97.103, 0.883),
                                         (101.51, 0.447))]
    r1, r2, r3, r4, r5, r6, r7, r8, r9 = r
    l = {}
    l["widest"] = 0.75 + (1.3 - 0.75) * r1
    l["baseFull"] = 0.65 + (1.1 - 0.65) * r2
    l["tipFull"] = 0.22 + (0.5 - 0.22) * r3
    l["lobe"] = 0.0 if r4 < 0.35 else 0.04 + (0.13 - 0.04) * r4
    l["lobes"] = 1.5 + (3.5 - 1.5) * r5
    l["frills"] = 3.0 + (5.5 - 3.0) * frac(r5 * 7.31 + r8)
    o = [frac(own * k + c) for k, c in ((13.713, 0.11), (27.191, 0.53), (39.517, 0.29), (51.313, 0.71),
                                        (67.919, 0.37), (79.137, 0.83), (91.373, 0.19))]
    o1, o2, o3, o4, o5, o6, o7 = o
    lerp = lambda a, b, t: a + (b - a) * t
    l["widest"] *= lerp(0.88, 1.12, o1)
    l["baseFull"] *= lerp(0.85, 1.15, o2)
    l["tipFull"] *= lerp(0.8, 1.2, o3)
    l["lobe"] *= lerp(0.6, 1.4, o4)
    l["frills"] += lerp(-0.6, 0.6, o5)
    r6 = min(1.0, max(0.0, r6 + lerp(-0.2, 0.2, o6)))
    r7 = min(1.0, max(0.0, r7 + lerp(-0.2, 0.2, o7)))
    r9 = frac(r9 + 0.3 * o5 + 0.2 * o6)
    cup, arch, wave = lerp(-0.6, 0.6, r6), lerp(-0.4, 0.4, r7), lerp(0.35, 0.8, r8)
    s = abs(cup) + abs(arch) + wave
    k = 1.0 / s if s > 1.0 else 1.0
    l["cup"], l["arch"], l["wave"], l["phase"] = cup * k, arch * k, wave * k, r9
    return l

def leaf_width(s, l):
    s = saturate(s)
    u = np.power(s, l["widest"])
    arch = np.maximum(1e-4, np.sin(np.pi * u))
    g = np.power(arch, np.where(u < 0.5, l["baseFull"], l["tipFull"]))
    lobes = 0.5 + 0.5 * np.sin(6.2831853 * (l["lobes"] * s + l["phase"]))
    return g * (1.0 - l["lobe"] * lobes * np.sin(np.pi * s))

def leaf_axes(he):
    x, y, z = he
    if x <= y and x <= z:
        T = np.array([1.0, 0, 0]); L = np.array([0, 1.0, 0]) if y >= z else np.array([0, 0, 1.0])
    elif y <= z:
        T = np.array([0, 1.0, 0]); L = np.array([1.0, 0, 0]) if x >= z else np.array([0, 0, 1.0])
    else:
        T = np.array([0, 0, 1.0]); L = np.array([1.0, 0, 0]) if x >= y else np.array([0, 1.0, 0])
    return L, T, np.cross(L, T)

def leaf_point(s, t, side, l, base_sign, mesh_half, curl_obj, flat, flesh):
    w = leaf_width(s, l)
    ends = np.power(np.maximum(1e-4, np.sin(np.pi * np.power(saturate(s), l["widest"]))), 0.3)
    across = t * w
    lens = np.sqrt(saturate(1.0 - t * t)) * (0.6 + 0.4 * np.exp(-t * t / 0.012))
    thickness = mesh_half * lens * ends * (1.0 + (0.55 - 1.0) * saturate(s)) * flesh
    arch = 1.0 - (2.0 * s - 1.0) ** 2
    a = np.abs(across)
    side2 = np.where(across < 0.0, 0.37, 0.0)
    x = l["frills"] * s + l["phase"] + side2
    ruffle = 0.72 * np.sin(6.2831853 * x) + 0.28 * np.sin(6.2831853 * (2.37 * x + 0.21))
    swell = 0.6 + 0.4 * np.sin(6.2831853 * (0.61 * x + l["phase"] * 1.7))
    frill = a * a * ruffle * swell * smoothstep(0.04, 0.3, s)
    lift = l["cup"] * across * across + l["arch"] * arch + l["wave"] * frill
    along = base_sign * mesh_half * (1.0 - 2.0 * s)
    p = np.stack([along, side * thickness + (1.0 - flat) * curl_obj * lift, base_sign * mesh_half * across], axis=1)
    return p, thickness

def leaf_veins(s, v, t, vein_strength=0.6):
    """LeafVeins without the pixel-width fade: face and through multipliers."""
    midrib = np.exp(-v * v / 0.0012) * (1.0 - smoothstep(0.1, 0.55, s))
    vein = saturate(midrib) * vein_strength
    margin = smoothstep(0.8, 1.0, np.abs(t))
    return 1.0 + 0.35 * vein - 0.08 * margin, 1.0 - 0.6 * vein

def leaf_tone(s, t):
    """The blade's tone on its whole light, before the mottle's share (the fragment's leafTone)."""
    across = saturate(np.abs(t))
    edge = smoothstep(0.35, 1.0, across)
    stalk = 1.0 - smoothstep(0.0, 0.4, s)
    tone = (0.5 + (1.12 - 0.5) * edge) * (1.0 - 0.35 * stalk)
    warm = np.array([1.12, 1.06, 0.78])
    return tone[:, None] * (1.0 + (warm[None, :] - 1.0) * (0.5 * edge)[:, None])
