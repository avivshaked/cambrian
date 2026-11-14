"""One smooth skin per body, as the film draws it, in numpy only (no bpy), so the arithmetic can be tested outside Blender.

The film's rule is in unity/Assets/Theatre/TheatreOneSkin.cs and TheatreNoBox.cs. Each part is the ellipsoid of its half
extents (a capsule squashed across by its aspect, a sphere the ellipsoid it is drawn as), inset by k/4. The parts are joined
by the quadratic polynomial smooth minimum of width k, k = min(0.5 x the median of the parts' smallest half extents, 3 cm),
and a ball of radius min(k, 0.5 x the thinner of the two joined parts' smallest half extent) is added at every joint's
anchor.

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


def plan(parts):
    """The skin of a body: {"k", "threshold", "elements": [...]}.

    An element is {"label", "owner" (the part whose pivot carries it), "co" (relative to that pivot, Blender axes), "rot"
    (w, x, y, z, in Blender axes, relative to the pivot at rest), "half" (Blender axes, the drawn size), "rho", "stiff",
    "kind" ("part" or "ball"), "part" (the part's index, or the child's for a ball)}."""
    k = skin_k(parts)
    centre = body_centre(parts)
    inset = INSET_SHARE * k
    els = []
    for p in parts:
        h = np.abs(np.asarray(p["half"], float))
        h = np.maximum(h - inset, MIN_KEEP * h)
        hb = (float(h[0]), float(h[2]), float(h[1]))
        co = to_blender(np.asarray(p["pos"], float) - np.asarray(p["anchor"] if p["parent"] >= 0 else p["pos"], float))
        Rb = _P @ rot_matrix(p["rot"]) @ _P
        els.append({"label": "p%d" % p["i"], "kind": "part", "part": p["i"], "owner": p["i"], "co": tuple(float(c) for c in co),
                    "rot": quat_wxyz(Rb), "half": hb})
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
    for e in els:
        e["rho"] = element_field(e["half"], k)
    f = [(1.0 - 1.0 / e["rho"] ** 2) ** 3 for e in els]
    t = min(T_MAX, STIFF_MAX * min(f))
    for e, fi in zip(els, f):
        e["stiff"] = t / fi
    return {"k": k, "threshold": t, "elements": els}


# ------------------------------------------------------------------ the field, as Blender evaluates it (for the tests)
def field_at(sk, parts, pts, bends=None):
    """The summed metaball field at points `pts` (n x 3, the body frame, Blender axes) at rest: each element contributes
    s x (1 - d^2/rho^2)^3 where d is the normalised ellipsoid distance (1 on the isolated surface x ...). Blender's field has
    its element's reach at |u| = rho when u is the position in the element's own axes divided by its sizes."""
    pts = np.asarray(pts, float)
    centre = body_centre(parts)
    by_index = {p["i"]: p for p in parts}
    total = np.zeros(len(pts))
    for e in sk["elements"]:
        c = pivot_at(by_index[e["owner"]], centre) + np.asarray(e["co"], float)
        w, x, y, z = e["rot"]
        R = quat_matrix_wxyz(e["rot"])
        u = ((pts - c) @ R) / np.asarray(e["half"], float)
        d2 = (u * u).sum(axis=1)
        v = np.clip(1.0 - d2 / e["rho"] ** 2, 0.0, None)
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
