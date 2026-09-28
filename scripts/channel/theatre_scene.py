"""The theatre's creature skin in Blender, as a module: bodies (rigged at their joints), the
water, the snow, the lights and the grade of the intro look ("v2", which the owner chose on
2026-09-28). intro.py animates it and outro.py arranges it round the end screen.

Each part is built the way the theatre builds it (TheatreMeshes, TheatrePalette,
PhenotypeBuilder, as read on the round-51-tests worker): a box drawn as a pillowed cube that the
shader tapers and bends, or as a kelp blade when it is a leaf or flat; a sphere squashed by the
genome's proportions; a capsule as a shaft and two caps, or one ellipsoid when short; a knuckle
on each side of every moving joint; every part carved inward, deeper at a joint. The vertex
stage runs in numpy (theatre_shape.py), the fragment stage as a node material
(theatre_material.py). Core's frame is Unity's, Y up; a point (x, y, z) is drawn at (x, z, y).
"""
import bpy, bmesh, json, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import theatre_shape as ts
import theatre_material as tm

BODIES = json.load(open(os.path.join(HERE, "bodies.json")))

INSET = 0.995
MESH_HALF = 0.5 * INSET
CARVE_FRACTION = 0.35
PINCH_GAIN, CARVE_MAX, PINCH_REACH = 1.6, 0.5, 0.45

def lin(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

# TheatrePalette, reserve 1 and brightness 1 (a snapshot's paint): rim, through, body, in sRGB.
COLOURS = {
    "photosynthetic": ((0.4102, 0.7498, 0.4281), (0.4383, 0.7617, 0.4553), (0.1072, 0.2528, 0.1149)),
    "absorptive": ((0.8404, 0.5723, 0.3196), (0.8480, 0.5927, 0.3520), (0.2610, 0.1776, 0.0990)),
    "link": ((0.8404, 0.3196, 0.6234), (0.8480, 0.3520, 0.6413), (0.2610, 0.0990, 0.1935)),
    "other": ((0.5296, 0.5632, 0.6304), (0.5520, 0.5840, 0.6480), (0.1584, 0.1728, 0.2016)),
}
TRANS_GAIN = {"photosynthetic": 1.0, "absorptive": 0.12}
SHEEN = {"photosynthetic": 0.04, "absorptive": 0.35}
CHARACTER = {"photosynthetic": (1.00, 0.42, 1.05), "absorptive": (0.60, 0.78, 1.00)}

def guild(cell):
    return cell if cell in ("photosynthetic", "absorptive", "link") else "other"

def frac(x):
    return x - math.floor(x)

def qrot(q, v):
    """Rotate rows of v by the quaternion (x, y, z, w)."""
    u = np.array(q[:3]); w = q[3]
    return v + 2.0 * np.cross(u, np.cross(u, v) + w * v)

def qinv(q):
    return (-q[0], -q[1], -q[2], q[3])

# ---------------------------------------------------------------- base meshes (object units)
def rounded_cube(pillow, segs=48):
    half = MESH_HALF
    radius = min(max(pillow, 0.0), 0.5) * half
    flat = half - radius
    faces_dirs = [((1, 0, 0), (0, 0, -1), (0, 1, 0)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)),
                  ((0, 1, 0), (1, 0, 0), (0, 0, -1)), ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
                  ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (-1, 0, 0), (0, 1, 0))]
    g = np.linspace(-1.0, 1.0, segs + 1)
    U, Vv = np.meshgrid(g, g, indexing="ij")
    verts, faces, off = [], [], 0
    for n, r, u in faces_dirs:
        n, r, u = np.array(n, float), np.array(r, float), np.array(u, float)
        v = half * (n[None, :] + r[None, :] * U.reshape(-1, 1) + u[None, :] * Vv.reshape(-1, 1))
        verts.append(v)
        idx = np.arange((segs + 1) ** 2).reshape(segs + 1, segs + 1) + off
        q = np.stack([idx[:-1, :-1], idx[1:, :-1], idx[1:, 1:], idx[:-1, 1:]], axis=-1).reshape(-1, 4)
        faces.append(q)
        off += (segs + 1) ** 2
    v = np.concatenate(verts)
    inner = np.clip(v, -flat, flat)
    d = v - inner
    ln = np.linalg.norm(d, axis=1)
    nrm = np.where(ln[:, None] > 1e-6, d / np.maximum(ln, 1e-12)[:, None], np.array([0, 1.0, 0]))
    return inner + nrm * radius, nrm, np.concatenate(faces)

def icosphere(subdiv=5):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    v = np.array([x.co[:] for x in bm.verts])
    f = np.array([[x.index for x in face.verts] for face in bm.faces])
    bm.free()
    n = v / np.linalg.norm(v, axis=1)[:, None]
    return n * MESH_HALF, n, f

def lathe_cylinder(sides=56):
    radius, half = MESH_HALF, INSET
    fillet = 0.34 * min(radius, half)
    flat_r, flat_y = radius - fillet, half - fillet
    prof = []
    for i in range(0, 9):
        prof.append((flat_r * i / 8, half, 0.0, 1.0))
    for i in range(1, 9):
        a = math.radians(90.0 * i / 8)
        prof.append((flat_r + fillet * math.sin(a), flat_y + fillet * math.cos(a), math.sin(a), math.cos(a)))
    for i in range(1, 29):
        prof.append((radius, flat_y + (-flat_y - flat_y) * i / 28, 1.0, 0.0))
    for i in range(1, 9):
        a = math.radians(90.0 * i / 8)
        prof.append((flat_r + fillet * math.cos(a), -flat_y - fillet * math.sin(a), math.cos(a), -math.sin(a)))
    for i in range(1, 9):
        prof.append((flat_r * (1 - i / 8), -half, 0.0, -1.0))
    P = len(prof)
    th = np.linspace(0, 2 * math.pi, sides + 1)
    v, n = [], []
    for (r, y, nr, ny) in prof:
        v.append(np.stack([np.cos(th) * r, np.full_like(th, y), np.sin(th) * r], axis=1))
        n.append(np.stack([np.cos(th) * nr, np.full_like(th, ny), np.sin(th) * nr], axis=1))
    v, n = np.concatenate(v), np.concatenate(n)
    n = n / np.maximum(np.linalg.norm(n, axis=1), 1e-9)[:, None]
    idx = np.arange(P * (sides + 1)).reshape(P, sides + 1)
    f = np.stack([idx[:-1, :-1], idx[:-1, 1:], idx[1:, 1:], idx[1:, :-1]], axis=-1).reshape(-1, 4)
    return v, n, f

def lamina_grid(stations=192, rows=32):
    s = np.arange(stations + 1) / stations
    step = 2.0 * np.arange(rows + 1) / rows - 1.0
    t = np.sin(0.5 * np.pi * step)
    S, T = np.meshgrid(s, t, indexing="ij")
    ss, tt, side, faces, off = [], [], [], [], 0
    for sd in (1.0, -1.0):
        ss.append(S.ravel()); tt.append(T.ravel()); side.append(np.full(S.size, sd))
        idx = np.arange(S.size).reshape(S.shape) + off
        faces.append(np.stack([idx[:-1, :-1], idx[1:, :-1], idx[1:, 1:], idx[:-1, 1:]], axis=-1).reshape(-1, 4))
        off += S.size
    return np.concatenate(ss), np.concatenate(tt), np.concatenate(side), np.concatenate(faces)

CUBES = {}
def cube_for(cubicness):
    t = min(max((cubicness - 0.55) / 0.30, 0.0), 1.0)
    pillow = 0.34 * (1.0 + (0.35 - 1.0) * t)
    bucket = min(max(round(pillow * 100 / 4) * 4, 0), 50) / 100.0
    if bucket not in CUBES:
        CUBES[bucket] = rounded_cube(bucket)
    return CUBES[bucket]

SPHERE = icosphere(5)
CYL = lathe_cylinder()
LAMINA = lamina_grid()

# ---------------------------------------------------------------- the lamina's vertex stage
def shape_lamina(he, seed, own, leaf_anchor):
    s, t, side, faces = LAMINA
    L, T, W = ts.leaf_axes(he)
    hL, hT, hW = he @ np.abs(L), he @ np.abs(T), he @ np.abs(W)
    l = ts.leaf_of(seed, own)
    along = float(np.asarray(leaf_anchor[:3]) @ L)
    base = -1.0 if frac(seed * 13.37 + 0.271) < 0.5 else 1.0
    if leaf_anchor[3] > 0.5 and abs(along) > 0.05:
        base = 1.0 if along > 0.0 else -1.0
    curl = 0.1 * hW / max(1e-6, hT)
    flesh = min(1.0, 0.35 * hW / max(1e-6, hT))
    p, thick = ts.leaf_point(s, t, side, l, base, MESH_HALF, curl, 0.0, flesh)
    f, _ = ts.leaf_point(s, t, side, l, base, MESH_HALF, curl, 1.0, flesh)
    to_os = lambda q: np.outer(q[:, 0], L) + np.outer(q[:, 1], T) + np.outer(q[:, 2], W)
    metres = 2.0 * np.array([hL, hT, hW])
    sd, td = np.clip(s, 0.01, 0.98), np.clip(t, -0.985, 0.975)
    q = ts.leaf_point(sd, td, side, l, base, MESH_HALF, curl, 0.0, flesh)[0] * metres
    qs = ts.leaf_point(sd + 0.01, td, side, l, base, MESH_HALF, curl, 0.0, flesh)[0] * metres
    qt = ts.leaf_point(sd, td + 0.01, side, l, base, MESH_HALF, curl, 0.0, flesh)[0] * metres
    n = np.cross(qs - q, qt - q) * side[:, None]
    n = to_os(n)
    n = n / np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]
    leaf = (s, t * ts.leaf_width(s, l), t)
    return to_os(p), to_os(f), n, thick * 2.0 * hT, hW, leaf, faces

# ---------------------------------------------------------------- one visual, in its part's frame
def visual_geometry(kind, local_pos, scale, part, cell, seed, own, pinches, leaf_anchor, carve_fraction):
    scale = np.asarray(scale, float)
    he = 0.5 * np.abs(scale)
    smallest = float(he.min())
    extra_leaf = None
    if kind == "lamina":
        p_os, carve_os, n_part, half_thick, half_width, extra_leaf, faces = shape_lamina(he, seed, own, leaf_anchor)
        n_obj = None
    else:
        if kind == "cube":
            base, n0, faces = cube_for(float(np.abs(scale).min() / np.abs(scale).max()))
            carve_os = base
            p_os, n_obj = ts.shape_box(base, n0, he, smallest, MESH_HALF, seed)
        elif kind == "sphere":
            p_os, n_obj, faces = SPHERE
            carve_os = p_os
        else:
            p_os, n_obj, faces = CYL
            carve_os = p_os
        n_part = n_obj / scale[None, :]
        n_part = n_part / np.maximum(np.linalg.norm(n_part, axis=1), 1e-12)[:, None]
        half_thick = np.full(len(p_os), smallest)
        half_width = 0.0
    lobe, wrinkle, share = CHARACTER.get(cell, (0.85, 0.32, 0.62))
    pinch = sum(ts.pinch(carve_os, a) for a in pinches) if pinches else np.zeros(len(carve_os))
    depth = np.minimum(carve_fraction * share * (1.0 + PINCH_GAIN * pinch), CARVE_MAX) * smallest
    if kind == "lamina":
        depth = np.minimum(depth, 0.5 * half_thick)
    c = ts.carve(carve_os + ts.own_shift(own), seed, lobe, wrinkle, ts.carve_detail(smallest))
    inward = -n_part * (depth * c)[:, None]
    if kind == "cube":
        p_os = np.clip(p_os + inward / scale[None, :], -MESH_HALF, MESH_HALF)
        P = local_pos + scale[None, :] * p_os
    else:
        P = local_pos + scale[None, :] * p_os + inward
    trans = 0.06 + 0.08 * half_width
    thickness = np.clip(trans / np.maximum(1e-4, half_thick), 0.0, 1.0)
    if extra_leaf is not None:
        s, v, t = extra_leaf
        face, through = ts.leaf_veins(s, v, t)
        tone = ts.leaf_tone(s, t)
    else:
        face = np.ones(len(P)); through = np.ones(len(P)); tone = np.ones((len(P), 3))
    attrs = np.stack([thickness, face, through, np.zeros(len(P))], axis=1)
    return P, n_part, faces, attrs, tone

# ---------------------------------------------------------------- the plan of a body
def drawn_as_leaf(part):
    if part["shape"] != "box":
        return False
    h = sorted(abs(x) for x in part["half"])
    return part["cell"] == "photosynthetic" or h[0] <= 0.4 * h[1]

def visuals_of(part):
    h = np.abs(np.array(part["half"], float))
    if part["shape"] == "box":
        return [("lamina" if drawn_as_leaf(part) else "cube", np.zeros(3), 2.0 * h)]
    if part["shape"] == "sphere":
        r = h.mean()
        return [("sphere", np.zeros(3), 2.0 * r * h / h.max())]
    r = 0.5 * (h[0] + h[2]); span = max(0.0, h[1] - r); L = max(h[0], h[2])
    asp = np.array([h[0] / L, 1.0, h[2] / L])
    if h[1] < h[0] + h[2]:
        st = asp.copy(); st[1] = max(h[1], r) / r if r > 0 else 1.0
        return [("sphere", np.zeros(3), 2.0 * r * st)]
    out = []
    if span > 0:
        out.append(("cylinder", np.zeros(3), np.array([2 * r * asp[0], span, 2 * r * asp[2]])))
    for sgn in (1.0, -1.0):
        out.append(("sphere", np.array([0, sgn * span, 0]), 2.0 * r * asp))
    return out

def face_of(host, across, anchor):
    h = np.abs(np.array(host["half"], float))
    anchor = np.asarray(anchor, float)
    towards = anchor
    if anchor @ anchor <= 1e-8:
        d = np.array(across["pos"], float) - np.array(host["pos"], float)
        towards = qrot(qinv(host["rot"]), d[None, :])[0]
        if towards @ towards <= 1e-12:
            towards = np.array([0, 1.0, 0])
    axis = int(np.argmax(np.abs(towards) / np.maximum(h, 1e-9)))
    a, b = (axis + 1) % 3, (axis + 2) % 3
    ca, cb = h[a] - abs(anchor[a]), h[b] - abs(anchor[b])
    room = anchor[axis] + h[axis] if towards[axis] >= 0 else h[axis] - anchor[axis]
    if ca <= 0 or cb <= 0 or room <= 0:
        return None
    return axis, a, b, ca, cb, room

def knuckle(own, other, own_anchor, other_anchor):
    fo, ft = face_of(own, other, own_anchor), face_of(other, own, other_anchor)
    if fo is None or ft is None:
        return None
    axis, a, b, ca, cb, room = fo
    _, _, _, oa, ob, other_room = ft
    across = min(oa, ob)
    ra, rb = 0.9 * min(ca, across), 0.9 * min(cb, across)
    if drawn_as_leaf(own):
        mid = sorted(abs(x) for x in own["half"])[1]
        ra, rb = min(ra, 0.2 * mid), min(rb, 0.2 * mid)
    length = min(min(ra, rb), min(room, other_room))
    if min(ra, rb, length) <= 1e-4:
        return None
    sc = np.zeros(3); sc[axis] = 2 * length; sc[a] = 2 * ra; sc[b] = 2 * rb
    return np.asarray(own_anchor, float), sc

# ---------------------------------------------------------------- Blender objects
def to_blender(v):
    return v[:, [0, 2, 1]]

def make_object(name, P, N, faces, attrs, tone, props, material, parent, offset=None):
    Pb, Nb = to_blender(P), to_blender(N)
    f = faces.copy()
    fn = np.cross(Pb[f[:, 1]] - Pb[f[:, 0]], Pb[f[:, 2]] - Pb[f[:, 0]])
    nn = Nb[f].mean(axis=1)
    flip = np.sum(fn * nn, axis=1) < 0
    f[flip] = f[flip][:, ::-1]
    me = bpy.data.meshes.new(name)
    if offset is not None:
        Pb = Pb - np.asarray(offset)
    me.from_pydata(Pb.tolist(), [], f.tolist())
    ca = me.color_attributes.new("evo_a", "FLOAT_COLOR", "POINT")
    ca.data.foreach_set("color", attrs.astype(np.float32).ravel())
    ct = me.color_attributes.new("evo_tone", "FLOAT_COLOR", "POINT")
    ct.data.foreach_set("color", np.concatenate([tone, np.ones((len(tone), 1))], axis=1).astype(np.float32).ravel())
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(material)
    for k, v in props.items():
        ob[k] = v
    ob.parent = parent
    bpy.context.scene.collection.objects.link(ob)
    return ob

def body_seeds(id_):
    own = (((id_ * 2654435761) >> 13) & 0xFFFF) / 65536.0
    seed = (((id_ * 2246822519 + 374761393) >> 11) & 0xFFFF) / 65536.0
    return seed, own

def build(key, name, location, rot_deg, scale, material, knuckle_glow):
    """One body, rigged: a pivot at each part's joint anchor, chained parent to child, so a
    rotation of a pivot turns that part and everything it carries about the joint."""
    body = BODIES[key]
    parts = body["parts"]
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    bseed, bown = body_seeds(body["id"])
    children = {i: [] for i in range(len(parts))}
    for p in parts:
        if p["parent"] >= 0:
            assert p["parent"] < p["i"], "parents come first in the developer's order"
            children[p["parent"]].append(p)
    centre = np.mean([p["pos"] for p in parts], axis=0)
    pivot_at, pivots = {}, {}
    for p in parts:
        a = np.asarray(p["anchor"] if p["parent"] >= 0 else p["pos"], float) - centre
        pivot_at[p["i"]] = to_blender(a[None, :])[0]
    for p in parts:
        e = bpy.data.objects.new("%s-joint%d" % (name, p["i"]), None)
        bpy.context.scene.collection.objects.link(e)
        e.empty_display_size = 0.02
        if p["parent"] >= 0:
            e.parent = pivots[p["parent"]]
            e.location = tuple(pivot_at[p["i"]] - pivot_at[p["parent"]])
        else:
            e.parent = root
            e.location = tuple(pivot_at[p["i"]])
        e.rotation_mode = "QUATERNION"
        pivots[p["i"]] = e

    def place(part, P, N):
        Pw = np.asarray(part["pos"], float) + qrot(part["rot"], P) - centre
        Nw = qrot(part["rot"], N)
        return Pw, Nw

    def props_for(cell, rim_scale, individual, brightness=1.0, emit=0.0):
        rim, through, bodyc = COLOURS[guild(cell)]
        return {"base": lin(tuple(c * brightness for c in bodyc)), "rim": lin(tuple(c * brightness for c in rim)),
                "trans_tint": lin(through), "trans_gain": TRANS_GAIN.get(cell, 0.4), "sheen": SHEEN.get(cell, 0.08),
                "reserve": 1.0, "rim_scale": rim_scale, "emit": emit,
                "shade": frac(individual * 17.37 + 0.41) * 2 - 1, "warmth": frac(individual * 31.91 + 0.07) * 2 - 1}

    for i, part in enumerate(parts):
        seed = frac(bseed + 0.61803399 * i)
        own = frac(bown + 0.38196601 * i)
        for vi, (kind, lp, sc) in enumerate(visuals_of(part)):
            cands = []
            if part["parent"] >= 0:
                cands.append(np.asarray(part["canchor"], float))
            for ch in children[i]:
                cands.append(np.asarray(ch["panchor"], float))
            here = [(c - lp) / sc for c in cands]
            here.sort(key=lambda x: float(np.linalg.norm(x)))
            pinches = [tuple(x) + (PINCH_REACH,) for x in here[:2]]
            leaf_anchor = (tuple((np.asarray(part["canchor"], float) - lp) / sc) + (1.0,)) if part["parent"] >= 0 else (0, 0, 0, 0)
            P, N, faces, attrs, tone = visual_geometry(kind, lp, sc, part, part["cell"], seed, own, pinches, leaf_anchor, CARVE_FRACTION)
            Pw, Nw = place(part, P, N)
            make_object("%s-p%d-%s%d" % (name, i, kind, vi), Pw, Nw, faces, attrs, tone,
                        props_for(part["cell"], 0.45 if kind == "lamina" else 1.0, own), material,
                        pivots[i], pivot_at[i])
        if part["parent"] >= 0 and part["joint"] != "Fixed":
            parent = parts[part["parent"]]
            for side, (own_p, oth_p, oa, ta) in enumerate(((part, parent, part["canchor"], part["panchor"]),
                                                          (parent, part, part["panchor"], part["canchor"]))):
                k = knuckle(own_p, oth_p, oa, ta)
                if k is None:
                    continue
                lp, sc = k
                oi = own_p["i"]
                kseed = frac(bseed + 0.61803399 * oi + 0.5)
                kown = frac(bown + 0.38196601 * oi)
                P, N, faces, attrs, tone = visual_geometry("sphere", lp, sc, own_p, own_p["cell"], kseed, kown, [], (0, 0, 0, 0), 0.5 * CARVE_FRACTION)
                Pw, Nw = place(own_p, P, N)
                make_object("%s-k%d-%d" % (name, i, side), Pw, Nw, faces, attrs, tone,
                            props_for(own_p["cell"], 1.0, kown, 0.85, knuckle_glow), material,
                            pivots[oi], pivot_at[oi])
    joints = []
    for p in parts:
        if p["parent"] >= 0 and p["joint"] != "Fixed":
            d = to_blender((np.asarray(p["pos"], float) - np.asarray(p["anchor"], float))[None, :])[0]
            ax = np.cross(d, [0.0, 0.0, 1.0])
            if np.linalg.norm(ax) < 0.3 * np.linalg.norm(d):
                ax = np.cross(d, [1.0, 0.0, 0.0])
            ax = ax / max(1e-9, np.linalg.norm(ax))
            joints.append({"pivot": pivots[p["i"]], "axis": ax, "depth": p["depth"], "joint": p["joint"]})
    root.rotation_euler = [math.radians(a) for a in rot_deg]
    root.location = location
    root.scale = (scale, scale, scale)
    return root, joints

# ---------------------------------------------------------------- the scene around the bodies
DEEP, SHALLOW, REACH = lin((0.012, 0.032, 0.048)), lin((0.09, 0.26, 0.32)), 18.0

def water(y):
    t = math.exp(-max(0.0, -y) / REACH)
    return tuple(d + (s - d) * t for d, s in zip(DEEP, SHALLOW))

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

def setup_render(percent, samples, exposure=0.55, motion_blur=False):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.denoiser = "OPENIMAGEDENOISE"
    sc.cycles.denoising_use_gpu = True    # off in a factory start-up: 28 s a 4K frame on the processor
    sc.render.compositor_device = "GPU"
    sc.cycles.max_bounces = 4
    sc.cycles.volume_bounces = 1
    sc.render.threads_mode = "FIXED"
    sc.render.threads = 4
    sc.render.resolution_x, sc.render.resolution_y = 3840, 2160
    sc.render.resolution_percentage = percent
    sc.render.fps = 30
    sc.render.use_motion_blur = motion_blur
    sc.render.use_persistent_data = True   # keep the scene between frames: only transforms move
    sc.render.motion_blur_shutter = 0.5
    for vt in ("Khronos PBR Neutral", "Standard"):
        try:
            sc.view_settings.view_transform = vt
            break
        except Exception:
            pass
    sc.view_settings.exposure = exposure
    return sc

def make_camera(location, target, fstop=1.6, lens=59.3):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("cam")
    cd.sensor_fit = "VERTICAL"
    cd.sensor_height = 24.0
    cd.lens = lens
    cd.dof.use_dof = True
    cd.dof.aperture_fstop = fstop
    cam = bpy.data.objects.new("cam", cd)
    cam.location = location
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam

def body_material_for(cam, eye_depth=-15.0, key_gain=0.4, back_gain=3.6, rim_strength=2.3, glow_strength=0.35):
    """The theatre's key, fill and portrait back light, aimed from this camera as the theatre aims
    them from its own (TheatreSkin.Aim); the intro's dark field is the key at 0.4 and the back
    light at 1.5 times the theatre's."""
    R = cam.rotation_euler.to_matrix()
    fwd = (R @ Vector((0, 0, -1))).normalized()
    key_dir = (R @ Vector((0.345, 0.616, 0.708))).normalized()
    fill_dir = (R @ Vector((-0.202, -0.242, -0.949))).normalized()
    back_dir = (fwd + Vector((0, 0, 0.45))).normalized()
    key_col = tuple(c * 3.0 for c in lin((0.86, 0.94, 1.0)))
    fill_col = tuple(c * 0.8 for c in lin((0.30, 0.52, 0.68)))
    back_col = tuple(c * back_gain for c in lin((0.55, 0.85, 1.0)))
    ambient = (lin((0.081, 0.234, 0.288)), lin((0.020, 0.038, 0.046)), lin((0.012, 0.032, 0.048)))
    return tm.body_material("theatre-body", tuple(key_dir), key_col,
                            [(tuple(fill_dir), fill_col), (tuple(back_dir), back_col)], ambient,
                            water(eye_depth), 0.035, rim_strength=rim_strength,
                            glow_strength=glow_strength, key_gain=key_gain)

def water_world(eye_y, gain=0.9):
    sc = bpy.context.scene
    world = bpy.data.worlds.new("water")
    sc.world = world
    nt = world.node_tree
    nt.nodes.clear()
    tcw = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tcw.outputs["Generated"], sep.inputs[0])
    def m(op, a, b=None):
        n = nt.nodes.new("ShaderNodeMath"); n.operation = op
        for i, x in enumerate((a, b)):
            if x is None: continue
            if isinstance(x, (int, float)): n.inputs[i].default_value = x
            else: nt.links.new(x, n.inputs[i])
        return n.outputs[0]
    y = m("ADD", m("MULTIPLY", sep.outputs[2], 3.0 * REACH), eye_y)
    tw = m("EXPONENT", m("DIVIDE", m("MAXIMUM", m("MULTIPLY", y, -1.0), 0.0), -REACH))
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"
    nt.links.new(tw, mix.inputs["Factor"])
    mix.inputs["A"].default_value = (*DEEP, 1)
    mix.inputs["B"].default_value = (*[c * gain for c in SHALLOW], 1)
    bgn = nt.nodes.new("ShaderNodeBackground")
    nt.links.new(mix.outputs["Result"], bgn.inputs["Color"])
    wo = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(bgn.outputs[0], wo.inputs["Surface"])

def marine_snow(rng, count=900, strength=1.2, box=((-5, 5), (-3.2, 13), (-3, 3))):
    sc = bpy.context.scene
    snow_m = bpy.data.materials.new("snow")
    snt = snow_m.node_tree; snt.nodes.clear()
    e = snt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = (0.55, 0.95, 0.85, 1)
    e.inputs["Strength"].default_value = strength
    so = snt.nodes.new("ShaderNodeOutputMaterial"); snt.links.new(e.outputs[0], so.inputs["Surface"])
    bm = bmesh.new()
    for i in range(count):
        r = rng.uniform(0.002, 0.007)
        mt = Matrix.Translation((rng.uniform(*box[0]), rng.uniform(*box[1]), rng.uniform(*box[2]))) @ Matrix.Scale(r, 4)
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0, matrix=mt)
    me = bpy.data.meshes.new("snow"); bm.to_mesh(me); bm.free()
    snow = bpy.data.objects.new("snow", me); snow.data.materials.append(snow_m); sc.collection.objects.link(snow)
    return snow

def haze_and_shafts():
    sc = bpy.context.scene
    bpy.ops.mesh.primitive_cube_add(size=40, location=(0, 5, 0))
    vol = bpy.context.active_object
    vol.name = "haze"
    vm = bpy.data.materials.new("haze"); vnt = vm.node_tree; vnt.nodes.clear()
    pv = vnt.nodes.new("ShaderNodeVolumePrincipled")
    pv.inputs["Color"].default_value = (0.3, 0.75, 0.7, 1)
    pv.inputs["Density"].default_value = 0.004
    pv.inputs["Anisotropy"].default_value = 0.6
    vo = vnt.nodes.new("ShaderNodeOutputMaterial"); vnt.links.new(pv.outputs[0], vo.inputs["Volume"])
    vol.data.materials.append(vm)
    for i, x in enumerate((-2.6, -0.4, 1.8, 3.6)):
        ld = bpy.data.lights.new("shaft%d" % i, "SPOT")
        ld.energy = 30000; ld.spot_size = math.radians(4 + 1.5 * i); ld.spot_blend = 0.35
        ld.color = (0.6, 0.95, 0.9); ld.shadow_soft_size = 0.2
        lo = bpy.data.objects.new(ld.name, ld)
        lo.location = (x, 3.0 + 2.0 * i, 9.0)
        lo.rotation_euler = (Vector((x * 0.6 - 0.8, 2.0 + i, -3.0)) - Vector(lo.location)).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)

def grade(threshold=0.6, strength=0.8, size=0.7, vignette=0.35):
    sc = bpy.context.scene
    ng = bpy.data.node_groups.new("comp", "CompositorNodeTree")
    ng.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    sc.compositing_node_group = ng
    rl = ng.nodes.new("CompositorNodeRLayers")
    gl = ng.nodes.new("CompositorNodeGlare")
    go = ng.nodes.new("NodeGroupOutput")
    for i in gl.inputs:
        if i.name == "Type": i.default_value = "Bloom"
        elif i.name == "Threshold": i.default_value = threshold
        elif i.name == "Strength": i.default_value = strength
        elif i.name == "Size": i.default_value = size
    ng.links.new(rl.outputs["Image"], gl.inputs["Image"])
    ell = ng.nodes.new("CompositorNodeEllipseMask")
    for i in ell.inputs:
        if i.name == "Size": i.default_value = (0.8, 0.8)
        elif i.name == "Value": i.default_value = 1.0
    blur = ng.nodes.new("CompositorNodeBlur")
    for i in blur.inputs:
        if i.name == "Size":
            try: i.default_value = (500, 500)
            except Exception: i.default_value = 500
    ng.links.new(ell.outputs[0], blur.inputs["Image"])
    mul = ng.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = vignette
    ng.links.new(gl.outputs["Image"], mul.inputs["A"])
    ng.links.new(blur.outputs[0], mul.inputs["B"])
    ng.links.new(mul.outputs["Result"], go.inputs[0])
