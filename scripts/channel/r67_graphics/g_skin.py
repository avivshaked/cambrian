"""One smooth skin per body, as the film draws it, in numpy only (no bpy), so the arithmetic can be tested outside Blender.

The film's rule is in unity/Assets/Theatre/TheatreOneSkin.cs and TheatreNoBox.cs. Each part is the ellipsoid of its half
extents (a capsule squashed across by its aspect, a sphere the ellipsoid it is drawn as), inset by k/4. The parts are joined
by the quadratic polynomial smooth minimum of width k, k = min(0.5 x the median of the parts' smallest half extents, 3 cm),
and a ball of radius min(k, 0.5 x the thinner of the two joined parts' smallest half extent) is added at every joint's
anchor. A sphere part is drawn as the film draws it (drawn_half: the genome's ellipsoid at Core's ball radius, widened
by SPHERE_REACH to hold its joint anchors). The palette's knuckles and bridges are ported too (neck_list) but off by
default, since the film's one skin hides every neck between two merged parts.

Blender draws that as a metaball family: one ELLIPSOID element per part and one per joint ball, each parented to its
part's joint pivot so the skin bends with the joints. A Blender ellipsoid element of radius rho, sizes (a, b, c), stiffness
s, in a family of threshold t, has its isolated surface at rho x (a, b, c) x sqrt(1 - (t/s)^(1/3)) (measured in Blender 5.1,
tools/mb_probe.py) and its field reaches rho x (a, b, c). So an element drawn at half extents h = (a, b, c) takes sizes h,
radius rho = 1 + KAPPA x k / min(h), and stiffness s = t / (1 - 1/rho^2)^3: the surface lands on h and the field reaches
KAPPA x k beyond it along the thinnest axis. KAPPA = 1 makes the union's neck match the polynomial smooth minimum of width k
(two touching spheres, a/k from 1.5 to 15, tools/mb_probe5.py: the profile within 0.5 percent of the sphere's radius).

Coordinates: Core's frame is Unity's, Y up; a point (x, y, z) is drawn at (x, z, y), as theatre_scene.to_blender does.
Everything here is in a body's own metres at scale 1, with the body's centre (the mean of its parts' positions) at the origin.
"""
import math
import os
import numpy as np

K_CAP = 0.03          # the film's global cap on k, metres
INSET_SHARE = 0.25    # every part is inset by k/4, so the smooth minimum's dip leaves it at the part's real size
KAPPA = 1.0           # the field's reach beyond the surface, in k
T_MAX = 0.1           # the family's threshold, unless a stiffness would pass STIFF_MAX
STIFF_MAX = 8.0       # Blender's limit is 10
MIN_KEEP = 0.2        # an inset never takes more than 80 percent of a half extent
FIELD_CELL = 0.005    # Blender's floor on a metaball's resolution, in the family's own units
TIP_BLEND = (1.06, 1.5)   # the pink tip: 1 on the tip part's own drawn surface (q = 1, within a little of it), 0 from q = 1.5 on, a smoothstep between

_P = np.array([[1.0, 0, 0], [0, 0, 1.0], [0, 1.0, 0]])      # Core -> Blender, its own inverse


def to_blender(v):
    return np.asarray(v, float)[..., [0, 2, 1]]


def rot_matrix(q):
    """Core's quaternion (x, y, z, w) as a 3x3 matrix acting on column vectors."""
    x, y, z, w = (float(c) for c in q)
    n = math.sqrt(x * x + y * y + z * z + w * w) or 1.0
    x, y, z, w = x / n, y / n, z / n, w / n
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def quat_wxyz(m):
    """A rotation matrix as a quaternion (w, x, y, z), Blender's order."""
    m = np.asarray(m, float)
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        q = ((s / 4), (m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s)
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        q = ((m[2, 1] - m[1, 2]) / s, s / 4, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s)
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        q = ((m[0, 2] - m[2, 0]) / s, (m[0, 1] + m[1, 0]) / s, s / 4, (m[1, 2] + m[2, 1]) / s)
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        q = ((m[1, 0] - m[0, 1]) / s, (m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, s / 4)
    return tuple(float(c) for c in q)


def smallest(part):
    return float(min(abs(h) for h in part["half"]))


def skin_k(parts):
    """The film's k: min(0.5 x the median of the parts' smallest half extents, 3 cm)."""
    return min(0.5 * float(np.median([smallest(p) for p in parts])), K_CAP)


def skin_thickness(parts):
    """The light through the tissue: the theatre's per-part clip(0.06 / smallest half extent), averaged over the parts by their area,
    so the big lobes that fill the picture set it (the median of the parts' sizes read the thin capsules too much)."""
    w, t = [], []
    for p in parts:
        a, b, c = sorted(abs(float(x)) for x in p["half"])
        q = 1.6075
        w.append(4 * math.pi * (((a * b) ** q + (a * c) ** q + (b * c) ** q) / 3) ** (1 / q))
        t.append(min(max(0.06 / max(a, 1e-6), 0.0), 1.0))
    return float(np.average(t, weights=w))


def body_centre(parts):
    return np.mean([p["pos"] for p in parts], axis=0)


def pivot_at(part, centre):
    """Where a part's joint pivot sits in the body frame (Blender axes): its anchor, or its own position for a root part."""
    a = np.asarray(part["anchor"] if part["parent"] >= 0 else part["pos"], float) - centre
    return to_blender(a)


def element_field(half, k):
    """(rho, kappa-free reach) for an element of Blender half extents `half`: the radius that puts the field's reach KAPPA x k beyond
    the surface along the thinnest axis."""
    return 1.0 + KAPPA * k / float(min(half))


def _inset(h, inset):
    h = np.abs(np.asarray(h, float))
    return np.maximum(h - inset, MIN_KEEP * h)


# ------------------------------------------------------------------ a sphere part's drawn size
# The film draws a sphere part as the genome's ellipsoid with its longest semi-axis at Core's ball radius r = the mean of
# |h| (StandardShapes.cs:127, PartShape.cs:210; TheatrePalette.Aspect; TheatreOneSkin.PrimitiveOf), and with
# EVOSIM_THEATRE_SPHERE_REACH on, widened toward the ball until it holds its own joint anchors (D359). Ported from the
# finals branch (28c2f35c): TheatrePalette.cs 3308-3310 (Dress), 3461-3515 (SphereReachAxes, SphereReachInside,
# SphereReachTerm), TheatreOneSkin.cs 3722-3740 (PrimitiveOf's sphere: r x the reach axes).
SPHERE_REACH = True     # scripts/theatre-film-look.json: EVOSIM_THEATRE_SPHERE_REACH = 1 (D359)


def _reach_inside(anchors, basis, r, t):
    """SphereReachInside (3496) and SphereReachTerm (3510): every anchor inside or on the ellipsoid r x lerp(basis, 1, t)."""
    a = (basis + (1.0 - basis) * t) * r
    for p in anchors:
        s = 0.0
        for pc, ac in zip(p, a):
            if ac <= 1e-9:
                s += math.inf if abs(pc) > 1e-9 else 0.0
            else:
                s += (pc / ac) ** 2
        if not (s <= 1.0 + 1e-4):
            return False
    return True


def sphere_reach(parts, index):
    """SphereReachAxes (3461): (the drawn semi-axes over r, t). basis = |h| / longest; t is the least value in [0, 1], by a
    24-step bisection keeping the upper bound, that puts every joint anchor of the part (its own canchor unless it is the root,
    and each child's panchor) inside the ellipsoid r x lerp(basis, (1, 1, 1), t); 0 when they are inside already."""
    by_index = {p["i"]: p for p in parts}
    part = by_index[index]
    h = np.abs(np.asarray(part["half"], float))
    longest = float(h.max())
    if longest <= 0:
        return np.ones(3), 0.0
    basis = h / longest
    r = sphere_radius(part["half"])
    if r <= 0:
        return basis, 0.0
    anchors = []
    if part["parent"] >= 0:
        anchors.append(np.asarray(part["canchor"], float))
    for q in parts:
        if q["i"] != index and q["parent"] == index:
            anchors.append(np.asarray(q["panchor"], float))
    if _reach_inside(anchors, basis, r, 0.0):
        return basis, 0.0
    low, high = 0.0, 1.0
    for _ in range(24):
        mid = 0.5 * (low + high)
        if _reach_inside(anchors, basis, r, mid):
            high = mid
        else:
            low = mid
    return basis + (np.ones(3) - basis) * high, high


def capsule_drawn(part):
    """A capsule as TheatreOneSkin.PrimitiveOf (finals 28c2f35c, 3742-3772) draws it: (long, r, span, sx, sz). r = (|hx|+|hz|)/2
    and span = max(0, |hy| - r) (StandardShapes.cs:194, 197); the palette's squash sx, sz = hx, hz over the larger of the two
    (Aspect, 3086-3094; the skin is dressed). long is False for a short capsule (span < r, NearlyASphere), drawn as the one
    ellipsoid (r sx, max(hy, r), r sz); else a capsule along local y of radius r and half shaft span, squashed across by sx, sz
    on the shaft and both caps, y left alone."""
    h = np.abs(np.asarray(part["half"], float))
    r = capsule_radius(h)
    span = capsule_half_span(h)
    across = max(h[0], h[2])
    sx = h[0] / across if across > 0 else 1.0
    sz = h[2] / across if across > 0 else 1.0
    return (not span < r), r, span, sx, sz


def drawn_half(parts, part):
    """A part's drawn half extents in its own frame, before the inset: a sphere as the film draws it, r x the reach axes (or
    r x |h| / longest with SPHERE_REACH off); a capsule as capsule_drawn says (its bounding half extents, r sx, span + r,
    r sz, for a long one); a box its |h|, as before."""
    h = np.abs(np.asarray(part["half"], float))
    if part["shape"] == "capsule":
        long, r, span, sx, sz = capsule_drawn(part)
        if not long:
            return np.maximum(1e-6, np.array([r * sx, max(h[1], r), r * sz]))
        return np.maximum(1e-6, np.array([r * sx, span + r, r * sz]))
    if part["shape"] != "sphere":
        return h
    r = sphere_radius(part["half"])
    if SPHERE_REACH:
        axes, _ = sphere_reach(parts, part["i"])
        return np.maximum(1e-6, r * axes)
    longest = float(h.max())
    return np.maximum(1e-6, h * (r / longest if longest > 0 else 1.0))


CAPSULE_ALONG_Z = (math.sqrt(0.5), 0.0, -math.sqrt(0.5), 0.0)   # (w, x, y, z): -90 degrees about y, taking x to z


def plan(parts, necks=False):
    """The skin of a body: {"k", "threshold", "elements": [...]}.

    An element is {"label", "owner" (the part whose pivot carries it), "co" (relative to that pivot, Blender axes), "rot"
    (w, x, y, z, in Blender axes, relative to the pivot at rest), "half" (Blender axes, the drawn size), "rho", "stiff",
    "kind" ("part", "ball", "knuckle" or "bridge"), "part" (the part's index, or the joint's child for a ball, a knuckle or
    a bridge), and Blender's own: "type" ("ELLIPSOID" or "CAPSULE"), "mb_radius", "size" (the MetaElement's radius and
    size_x/y/z), "el_rot" (the MetaElement's rotation, w x y z) and "scale" (the element object's scale)}. A long capsule
    also carries "radius" (its round radius before the squash) and "span" (its half shaft). A sphere part takes the film's drawn size (drawn_half). necks=True adds the palette's knuckles and bridges,
    which the film does NOT draw in the one skin: TheatreOneSkin.Dress (finals, 2799-2813) disables every neck whose two
    parts are both merged into the skin, so the default leaves them out."""
    k = skin_k(parts)
    centre = body_centre(parts)
    inset = INSET_SHARE * k
    els = []
    for p in parts:
        h = _inset(drawn_half(parts, p), inset)
        hb = (float(h[0]), float(h[2]), float(h[1]))
        co = to_blender(np.asarray(p["pos"], float) - np.asarray(p["anchor"] if p["parent"] >= 0 else p["pos"], float))
        Rb = _P @ rot_matrix(p["rot"]) @ _P
        e = {"label": "p%d" % p["i"], "kind": "part", "part": p["i"], "owner": p["i"], "co": tuple(float(c) for c in co),
             "rot": quat_wxyz(Rb), "half": hb}
        if p["shape"] == "capsule" and capsule_drawn(p)[0]:
            # A long capsule is Blender's CAPSULE element (a ball swept along its local x by +-size_x, round across), turned
            # by the element's own rotation so that x runs along the part's axis (Core y, the object's local z in Blender
            # axes), and squashed across by the object's scale, so its caps are (r sx, r, r sz) and its shaft straight.
            # Inset as a part: the round radius R = r - k/4 sets the caps' reach along the axis (half extent span + R =
            # |hy| - k/4); the cross half extents r sx - k/4 and r sz - k/4 set the scale. hb stays the bounding half
            # extents in the part's Blender axes, so the field's reach rule and the tip mask read it as for an ellipsoid.
            _, r, span, sx, sz = capsule_drawn(p)
            R = max(r - inset, MIN_KEEP * r)
            e.update({"type": "CAPSULE", "radius": float(R), "span": float(span), "el_rot": CAPSULE_ALONG_Z,
                      "scale": (hb[0] / R, hb[1] / R, 1.0), "half": (hb[0], hb[1], float(span + R))})
        els.append(e)
    by_index = {p["i"]: p for p in parts}
    for p in parts:
        if p["parent"] < 0:
            continue
        par = by_index[p["parent"]]
        r = min(k, 0.5 * min(smallest(p), smallest(par)))
        if not r > 1e-5:
            continue
        els.append({"label": "j%d" % p["i"], "kind": "ball", "part": p["i"], "owner": p["i"], "co": (0.0, 0.0, 0.0),
                    "rot": (1.0, 0.0, 0.0, 0.0), "half": (r, r, r)})
    if necks:
        for n in neck_list(parts):
            own = by_index[n["own"]]
            R_own = rot_matrix(own["rot"])
            world = np.asarray(own["pos"], float) + R_own @ n["centre"]
            pivot = np.asarray(own["anchor"] if own["parent"] >= 0 else own["pos"], float)
            h = _inset(n["half"], inset)
            els.append({"label": ("k" if n["kind"] == "knuckle" else "b") + ("c" if n["child_side"] else "p") + "%d" % n["joint"],
                        "kind": n["kind"], "part": n["joint"], "owner": n["own"],
                        "co": tuple(float(c) for c in to_blender(world - pivot)),
                        "rot": quat_wxyz(_P @ (R_own @ n["rot"]) @ _P), "half": (float(h[0]), float(h[2]), float(h[1]))})
    for e in els:
        e["rho"] = element_field(e["half"], k)
        e.setdefault("type", "ELLIPSOID")
        e.setdefault("el_rot", (1.0, 0.0, 0.0, 0.0))
        e.setdefault("scale", (1.0, 1.0, 1.0))
        if e["type"] == "CAPSULE":
            # Blender's MetaElement for add_skin: radius and size_x in the element's own (unscaled) units
            e["mb_radius"] = e["rho"] * e["radius"]
            e["size"] = (e["span"], e["span"], e["span"])
        else:
            e["mb_radius"] = e["rho"]
            e["size"] = e["half"]
    f = [(1.0 - 1.0 / e["rho"] ** 2) ** 3 for e in els]
    t = min(T_MAX, STIFF_MAX * min(f))
    for e, fi in zip(els, f):
        e["stiff"] = t / fi
    return {"k": k, "threshold": t, "elements": els}


# ------------------------------------------------------------------ the film's necks: knuckles and bridges
# A port of unity/Assets/Theatre/TheatrePalette.cs (line numbers as of 2026-11-15): BuildNecks (3592), Knuckle (3906),
# FitKnuckle (4067), Face (4158), Direction (4671), CarveReach (4217), Bridge (4268), Orient (4447), DrawnHit (4515),
# RayEllipsoid (4547), RayShaft (4570), NearlyASphere (3133); SphereShape.Radius, CapsuleShape.Radius and HalfSpan from
# src/Evosim.Core/Shapes/StandardShapes.cs (127, 194, 197). At every joint the palette draws two knuckles, one on each side,
# and on a side whose drawn tissue stands back from the anchor a bridge too; each is a unit sphere scaled in its own part's
# frame and parented to that part, so it moves with it.
#
# Ported for a side whose own part is a sphere or a capsule, numbers included. A side whose own part is a box (a leaf
# among them) or a superellipsoid gets no element: the box knuckle's ClearanceAlong (3865), the leaf's seam and stalk
# (4107) and the box and leaf bridges (4337 on) are not ported, and the graphics draw no neck there, as before.
# Not ported either: the link dial's LinkStandBack (4320), since EVOSIM_THEATRE_LINK is off in the graphics (and off in
# scripts/theatre-film-look.json); and the blend dial's Flare (3991), which acts only between two boxes.
BRIDGE_OVERLAP = 0.25   # TheatrePalette.BridgeOverlap (4195)
BRIDGE_THINNEST = 0.2   # TheatrePalette.BridgeThinnest (4201)
CARVE_FRACTION = 0.35   # the film look's EVOSIM_THEATRE_CARVE (scripts/theatre-film-look.json), = TheatreSkin.CarveFraction's default (TheatreSkin.cs:127)


def carve_reach(fraction=CARVE_FRACTION):
    """TheatrePalette.CarveReach (4217): min(0.5, fraction x 1.05 x (1 + 1.6 x 2)); 0.5 at the film's 0.35."""
    fraction = min(max(fraction, 0.0), 0.5)
    return min(0.5, fraction * 1.05 * (1.0 + 1.6 * 2.0))


def sphere_radius(h):
    return float(np.mean(np.abs(np.asarray(h, float))))


def capsule_radius(h):
    return (abs(float(h[0])) + abs(float(h[2]))) / 2.0


def capsule_half_span(h):
    return max(0.0, abs(float(h[1])) - capsule_radius(h))


def _nearly_a_sphere(part):
    return part["shape"] == "capsule" and capsule_half_span(part["half"]) < capsule_radius(part["half"])


def _direction(part, parent, anchor):
    """Direction (4671): the anchor, or the child's offset in the parent's frame where the anchor is at its centre."""
    anchor = np.asarray(anchor, float)
    if anchor @ anchor > 1e-8:
        return anchor
    local = rot_matrix(parent["rot"]).T @ (np.asarray(part["pos"], float) - np.asarray(parent["pos"], float))
    return local if local @ local > 1e-8 else np.array([0.0, 1.0, 0.0])


def _face(host, across, anchor):
    """Face (4158): (axis, clearA, clearB, room, sign), or None."""
    h = np.abs(np.asarray(host["half"], float))
    if (h <= 0).any():
        return None
    towards = _direction(across, host, anchor)
    best, axis = -1.0, 0
    for j in range(3):
        reach = abs(towards[j]) / h[j]
        if reach > best:
            best, axis = reach, j
    sign = 1.0 if towards[axis] >= 0 else -1.0
    a, b = (axis + 1) % 3, (axis + 2) % 3
    clear_a = h[a] - abs(anchor[a])
    clear_b = h[b] - abs(anchor[b])
    room = anchor[axis] + h[axis] if sign > 0 else h[axis] - anchor[axis]
    if not (clear_a > 0 and clear_b > 0 and room > 0):
        return None
    return axis, float(clear_a), float(clear_b), float(room), sign


def fit_knuckle(part, parent, child_side):
    """FitKnuckle (4067) for a sphere or capsule side: {"anchor" (own frame), "axis", "sign", "ra", "rb", "length"}, or None."""
    own, other = (part, parent) if child_side else (parent, part)
    if own["shape"] not in ("sphere", "capsule"):
        return None                      # a box or leaf side: not ported (ClearanceAlong, the leaf's seam), no element
    own_anchor = np.asarray(part["canchor"] if child_side else part["panchor"], float)
    other_anchor = np.asarray(part["panchor"] if child_side else part["canchor"], float)
    f = _face(own, other, own_anchor)
    g = _face(other, own, other_anchor)
    if f is None or g is None:
        return None
    axis, ca, cb, room, sign = f
    _, oa, ob, other_room, _ = g
    across = min(oa, ob)                 # not a box: both cross radii take the other part's smaller clearance (4098-4101)
    ra = 0.9 * min(ca, across)           # 4103
    rb = 0.9 * min(cb, across)           # 4104
    length = min(min(ra, rb), min(room, other_room))     # 4132
    if ra <= 1e-4 or rb <= 1e-4 or length <= 1e-4:
        return None
    return {"anchor": own_anchor, "axis": axis, "sign": sign, "ra": ra, "rb": rb, "length": length}


def _ray_ellipsoid(origin, d, centre, semi):
    """RayEllipsoid (4547)."""
    semi = np.asarray(semi, float)
    if (semi <= 0).any():
        return math.inf
    o = (np.asarray(origin, float) - centre) / semi
    dd = np.asarray(d, float) / semi
    qa, qb, qc = dd @ dd, 2 * (o @ dd), o @ o - 1
    if qc <= 0:
        return 0.0
    if qa <= 0:
        return math.inf
    disc = qb * qb - 4 * qa * qc
    if disc < 0:
        return math.inf
    t = (-qb - math.sqrt(disc)) / (2 * qa)
    return t if t >= 0 else math.inf


def _ray_shaft(origin, d, rx, rz, span):
    """RayShaft (4570)."""
    if span <= 0 or rx <= 0 or rz <= 0:
        return math.inf
    ox, oz, dx, dz = origin[0] / rx, origin[2] / rz, d[0] / rx, d[2] / rz
    qa, qb, qc = dx * dx + dz * dz, 2 * (ox * dx + oz * dz), ox * ox + oz * oz - 1
    if qc <= 0:
        t = 0.0
    else:
        if qa <= 0:
            return math.inf
        disc = qb * qb - 4 * qa * qc
        if disc < 0:
            return math.inf
        t = (-qb - math.sqrt(disc)) / (2 * qa)
        if t < 0:
            return math.inf
    return t if abs(origin[1] + t * d[1]) <= span else math.inf


def _drawn_hit(own, h, radius, span, start, d):
    """DrawnHit (4515): (distance along the ray to the drawn surface, the part's smallest drawn half extent). The drawn shape
    is the film's: a sphere as its half extents scaled so the longest is the ball's radius (Aspect, 3072)."""
    if own["shape"] == "sphere":
        longest = float(h.max())
        s = h * (radius / longest) if longest > 0 else np.full(3, radius)
        return _ray_ellipsoid(start, d, np.zeros(3), s), float(s.min())
    across = max(h[0], h[2])
    sx = h[0] / across if across > 0 else 1.0
    sz = h[2] / across if across > 0 else 1.0
    smallest = radius * min(sx, sz)
    if _nearly_a_sphere(own):
        return _ray_ellipsoid(start, d, np.zeros(3), (radius * sx, max(h[1], radius), radius * sz)), smallest
    cap = (radius * sx, radius, radius * sz)
    hit = min(_ray_ellipsoid(start, d, np.array([0.0, span, 0.0]), cap),
              _ray_ellipsoid(start, d, np.array([0.0, -span, 0.0]), cap))
    return min(hit, _ray_shaft(start, d, radius * sx, radius * sz, span)), smallest


def _orient(d, axis):
    """Orient (4447): Unity's LookRotation(forward, d) as a matrix whose columns are the bridge's X, Y (= d) and Z."""
    eb = np.zeros(3); eb[(axis + 2) % 3] = 1.0
    fwd = eb - d * (eb @ d)
    if fwd @ fwd < 1e-6:
        ea = np.zeros(3); ea[(axis + 1) % 3] = 1.0
        fwd = ea - d * (ea @ d)
    z = fwd / np.linalg.norm(fwd)
    x = np.cross(d, z); x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.column_stack([x, y, z])


def fit_bridge(part, parent, child_side, fit=None, fraction=CARVE_FRACTION):
    """Bridge (4268), the sphere and capsule case (4294-4335): {"centre", "rot" (own frame), "half" (the bridge's own X, Y, Z),
    "a", "L", "r"} or None when the side needs none or none fits. a is the distance from the anchor to the collider's core
    (capped at its radius), L the half length and r the wider cross radius, for the theatre's L <= a, r^2 <= aL check."""
    own = part if child_side else parent
    if own["shape"] not in ("sphere", "capsule"):
        return None                      # box and leaf bridges (4337 on): not ported, no element
    if fit is None:
        fit = fit_knuckle(part, parent, child_side)
    if fit is None:
        return None
    h = np.abs(np.asarray(own["half"], float))
    if (h <= 0).any():
        return None
    anchor = fit["anchor"]
    ball = own["shape"] == "sphere"
    radius = sphere_radius(own["half"]) if ball else capsule_radius(own["half"])
    span = 0.0 if ball else capsule_half_span(own["half"])
    core = np.zeros(3) if ball else np.array([0.0, min(max(anchor[1], -span), span), 0.0])
    to_core = core - anchor
    away = float(np.linalg.norm(to_core))
    if away < 1e-4 or radius <= 1e-4:
        return None
    d = to_core / away
    a = min(away, radius)
    start = core - d * a
    hit, smallest = _drawn_hit(own, h, radius, span, start, d)
    # (LinkStandBack, 4320, is skipped: the link dial is off in the graphics)
    depth = min(min(hit, a) + (carve_reach(fraction) + BRIDGE_OVERLAP) * smallest, a)     # 4322
    if depth <= fit["length"]:
        return None                      # the knuckle already reaches the drawn tissue
    half = 0.5 * depth
    cap = 0.95 * math.sqrt(a * half)
    widest = max(fit["ra"], fit["rb"])
    f = cap / widest if widest > cap else 1.0
    if f < BRIDGE_THINNEST:
        return None
    return {"centre": start + d * half, "rot": _orient(d, fit["axis"]),
            "half": np.array([fit["ra"] * f, half, fit["rb"] * f]), "a": a, "L": half, "r": widest * f}


def neck_list(parts, fraction=CARVE_FRACTION):
    """BuildNecks' list (3657-3678) with Knuckle's (3906) and Bridge's sizes: one row per knuckle and bridge, {"kind", "joint"
    (the child's index), "child_side", "own" (the part that carries it), "centre" and "rot" (its own part's frame), "half"
    (the theatre's scale / 2, on the element's own axes)}, before any inset."""
    by_index = {p["i"]: p for p in parts}
    out = []
    for part in parts:
        if part["parent"] < 0:
            continue
        parent = by_index[part["parent"]]
        fits = {}
        for child_side in (False, True):
            fit = fit_knuckle(part, parent, child_side)
            fits[child_side] = fit
            if fit is None:
                continue
            ax, a, b = fit["axis"], (fit["axis"] + 1) % 3, (fit["axis"] + 2) % 3
            half = np.zeros(3)
            half[ax], half[a], half[b] = fit["length"], fit["ra"], fit["rb"]
            out.append({"kind": "knuckle", "joint": part["i"], "child_side": child_side,
                        "own": part["i"] if child_side else parent["i"], "centre": fit["anchor"].copy(), "rot": np.eye(3),
                        "half": half})
        for child_side in (False, True):
            br = fit_bridge(part, parent, child_side, fits[child_side], fraction) if fits[child_side] else None
            if br is None:
                continue
            out.append({"kind": "bridge", "joint": part["i"], "child_side": child_side,
                        "own": part["i"] if child_side else parent["i"], "centre": br["centre"], "rot": br["rot"],
                        "half": br["half"], "a": br["a"], "L": br["L"], "r": br["r"]})
    return out


# ------------------------------------------------------------------ the field, as Blender evaluates it (for the tests)
def field_at(sk, parts, pts, bends=None):
    """The summed metaball field at points `pts` (n x 3, the body frame, Blender axes) at rest, as Blender's densfunc
    (blenkernel mball_tessellate) evaluates it: the point is taken into the element's own frame (the object's location,
    rotation and scale, then the element's rotation); an ELLIPSOID divides it by its sizes, a CAPSULE moves its x to the
    nearest point of the segment +-size_x and leaves it in units; each element then contributes s x (1 - |u|^2/rad^2)^3.
    The ellipsoid rule (surface at rad x size x sqrt(1 - (t/s)^(1/3))) was measured in Blender 5.1 (tools/mb_probe.py); the
    capsule's is the same code path and is not yet measured here."""
    pts = np.asarray(pts, float)
    centre = body_centre(parts)
    by_index = {p["i"]: p for p in parts}
    total = np.zeros(len(pts))
    for e in sk["elements"]:
        c = pivot_at(by_index[e["owner"]], centre) + np.asarray(e["co"], float)
        R = quat_matrix_wxyz(e["rot"])
        u = ((pts - c) @ R) / np.asarray(e.get("scale", (1.0, 1.0, 1.0)), float)
        u = u @ quat_matrix_wxyz(e.get("el_rot", (1.0, 0.0, 0.0, 0.0)))
        if e.get("type", "ELLIPSOID") == "CAPSULE":
            x = u[:, 0]
            u = u.copy()
            u[:, 0] = np.sign(x) * np.maximum(np.abs(x) - e["span"], 0.0)
            rad = e["mb_radius"]
        else:
            u = u / np.asarray(e["half"], float)
            rad = e["rho"]
        d2 = (u * u).sum(axis=1)
        v = np.clip(1.0 - d2 / rad ** 2, 0.0, None)
        total += e["stiff"] * v ** 3
    return total


def quat_matrix_wxyz(q):
    w, x, y, z = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


# ------------------------------------------------------------------ resolution
def basis_scale(cell_m):
    """Blender's metaball cell is FIELD_CELL in the family's own units. The basis object is scaled by 1/U, so the elements,
    seen from it, are U times larger and the cell is FIELD_CELL/U real metres. Returns 1/U for a wanted cell in metres."""
    return float(cell_m) / FIELD_CELL


VERTS_PER_AREA = 1.25      # vertices of an evaluated skin per (area / cell^2): measured 1.06 to 1.33 on four bodies, tools/skin_probe.py
VERT_BUDGET = int(os.environ.get("EVOSIM_SKIN_VERTS", "120000"))   # at most about 0.1 s of polygonising per body


def surface_area(parts):
    """The sum of the parts' ellipsoid areas (Knud Thomsen's approximation), in square metres: what a skin's vertex count scales with."""
    total = 0.0
    p = 1.6075
    for part in parts:
        a, b, c = sorted(abs(float(x)) for x in part["half"])
        total += 4 * math.pi * (((a * b) ** p + (a * c) ** p + (b * c) ** p) / 3) ** (1 / p)
    return total


def cell_for(depth, f_px, scale, area=None, px_ratio=1.0, share=0.5, lo=0.0015, hi=0.012, budget=None):
    """The cell, in a body's own metres, that is `share` of a render pixel at `depth` for a body drawn at `scale`. f_px is the
    camera's focal length in design pixels (lens / sensor x 1080); px_ratio is the render's pixel in design pixels (1920 over the
    render's width). Bounded below by `lo`, and, when the body's surface `area` is given, by the cell at which the skin would pass
    `budget` vertices (default VERT_BUDGET); bounded above by `hi`, so a body far off is not smoother than its silhouette needs."""
    px = depth / (f_px * max(scale, 1e-6)) * px_ratio
    c = min(max(share * px, lo), hi)
    if area:
        c = max(c, math.sqrt(VERTS_PER_AREA * area / float(budget or VERT_BUDGET)))
    return float(c)


# ------------------------------------------------------------------ the pink tip's mask, in the body frame
def tip_element(parts, tip):
    """The plan's element of the tip part."""
    return next(e for e in plan(parts)["elements"] if e["kind"] == "part" and e["part"] == tip)


def tip_rows(parts, tip, chain, el=None):
    """The affine map from a point of the body frame NOW to the tip part's normalised ellipsoid coordinates at rest, as
    (rows (3x3), offset (3)) with u_i = rows[i] . p + offset[i]; |u| is 1 on the tip's drawn surface.

    el: the tip's element from tip_element (the plan is not rebuilt on every frame).
    chain: [(location, quaternion (w, x, y, z)), ...] from the body root's child pivot down to the tip's pivot: each pivot's
    location relative to its parent and its rotation now. The pivots' rest pose is the same chain with identity rotations."""
    centre = body_centre(parts)
    by_index = {p["i"]: p for p in parts}
    tp = by_index[tip]
    if el is None:
        el = tip_element(parts, tip)
    M = np.eye(4)
    Mr = np.eye(4)
    for loc, q in chain:
        T = np.eye(4); T[:3, 3] = loc
        R = np.eye(4); R[:3, :3] = quat_matrix_wxyz(q)
        M = M @ T @ R
        Mr = Mr @ T
    Q = Mr @ np.linalg.inv(M)
    Rb = quat_matrix_wxyz(el["rot"])
    C = to_blender(np.asarray(tp["pos"], float) - centre)
    h = np.asarray(el["half"], float)
    rows = np.zeros((3, 3)); off = np.zeros(3)
    for i in range(3):
        axis = Rb[:, i]
        rows[i] = (axis @ Q[:3, :3]) / h[i]
        off[i] = (axis @ (Q[:3, 3] - C)) / h[i]
    return rows, off


def tip_share(q):
    """The pink share at normalised distance q from the tip part: 1 inside, 0 beyond, a smoothstep between."""
    lo, hi = TIP_BLEND
    x = min(max((hi - q) / (hi - lo), 0.0), 1.0)
    return x * x * (3 - 2 * x)
