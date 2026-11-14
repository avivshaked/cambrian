"""The graphics' camera as arithmetic, with no Blender: the pinhole of theatre_scene.make_camera (24 mm vertical sensor,
lens shift) in the design's 1920x1080 pixels, so every object is put at the pixel the designer drew it at, at a chosen
depth, and the layout is read off the design and not guessed.

Axes are Blender's: x right, y away from the camera (the camera looks along +y), z up. The principal point
(ppx, ppy) is the design pixel where the optical axis lands, the vanishing point of everything running away from the
camera; a lens shift moves it off the middle of the frame. `m` below is metres per design pixel on the plane through
the camera's own height at distance `t`.

    cam = Camera(lens=85.0, position=(0, -7.65, 0), pp=(3360, -1100))
    cam.to_world(1000, 600, t=7.85)       # where the pixel is at that depth
    cam.to_pixel((x, y, z))               # and back
"""
import json, math, os

W, H = 1920, 1080
SENSOR_H = 24.0


class Camera:
    def __init__(self, lens, position, pp=(W / 2, H / 2), fstop=1.6, focus=None):
        self.lens = lens
        self.pos = tuple(position)
        self.pp = tuple(pp)
        self.fstop = fstop
        self.focus = focus
        self.f_px = lens / SENSOR_H * H          # focal length in design pixels

    # Blender's shift, as a fraction of the frame height: the frame window moves against the lens axis, so the
    # principal point sits left of centre for a positive shift_x and below centre for a positive shift_y... the
    # signs are fixed here and checked against world_to_camera_view by g_scene's --dry.
    @property
    def shift(self):
        return ((W / 2 - self.pp[0]) / H, (self.pp[1] - H / 2) / H)

    def to_world(self, px, py, t):
        """The point `t` metres along the view axis that lands on design pixel (px, py)."""
        x = (px - self.pp[0]) * t / self.f_px
        z = (self.pp[1] - py) * t / self.f_px
        return (self.pos[0] + x, self.pos[1] + t, self.pos[2] + z)

    def to_pixel(self, p):
        t = p[1] - self.pos[1]
        return (self.pp[0] + self.f_px * (p[0] - self.pos[0]) / t, self.pp[1] - self.f_px * (p[2] - self.pos[2]) / t)

    def metres_per_px(self, t):
        return t / self.f_px

    def coc_px(self, t, focus=None):
        """Diameter of a point's circle of confusion on the design frame at depth t, thin lens, focused at `focus`."""
        focus = focus if focus is not None else self.focus
        if focus is None:
            return 0.0
        f = self.lens / 1000.0
        aperture = f / self.fstop
        return abs(aperture * f * (t - focus) / (t * (focus - f))) * self.f_px / f * 1.0 if t > 0 else 0.0


def smoother(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)


def span(t, t0, t1):
    """0 before t0, 1 after t1, smootherstep between (D361's move easing)."""
    return smoother((t - t0) / (t1 - t0)) if t1 > t0 else (1.0 if t >= t0 else 0.0)


def lerp(a, b, k):
    return a + (b - a) * k


def lerp_pt(a, b, k):
    return tuple(lerp(x, y, k) for x, y in zip(a, b))


def quad_bezier(p0, p1, p2, t):
    return tuple((1 - t) ** 2 * a + 2 * (1 - t) * t * b + t * t * c for a, b, c in zip(p0, p1, p2))


def lcg(seed):
    """water.js's rng, so the design's random shafts and caustics can be reproduced."""
    s = [seed & 0xFFFFFFFF]

    def nxt():
        s[0] = (s[0] * 1664525 + 1013904223) & 0xFFFFFFFF
        return s[0] / 4294967296.0
    return nxt


def compare(projected, layout, tolerance=None, at=None):
    """projected {name: (px, py)} against a layout dict {"anchors": {name: [px, py]}, "tolerance_px": n}: the rows
    (name, want, got, miss in px) and whether every miss is within the tolerance (1% of the frame width, 19 px). `at` is the check time in slot seconds, for the layout's "only_at"."""
    tol = tolerance if tolerance is not None else layout.get("tolerance_px", 0.01 * W)
    rows, ok = [], True
    for name, want in layout["anchors"].items():
        if at is not None and name in layout.get("only_at", {}) and at not in layout["only_at"][name]:
            continue            # an anchor the design reaches only at some of its check times (the tube's level)
        if name not in projected:
            rows.append((name, want, None, None))
            ok = False
            continue
        got = projected[name]
        miss = math.hypot(got[0] - want[0], got[1] - want[1])
        rows.append((name, want, got, miss))
        ok = ok and miss <= tol
    return rows, ok


def load_layout(name):
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "layout", name + ".json"), encoding="utf-8") as f:
        return json.load(f)
