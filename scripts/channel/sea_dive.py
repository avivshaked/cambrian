"""The dive's crossing: the camera passes from above the open sea, through the surface, into the water. The scene
is the approved still's (sea_still.py's build(): its sky, its water, its view transform and dither), with the sea
set moving and the camera on one of four paths.

    blender -b --factory-startup --python sea_dive.py -- <out-prefix> <samples> --path A|B|C|D|E [options]

Frames are written as <out-prefix>NNNN.png (8-bit, dithered as the still is), and, with --keep-16bit, beside each
its 16-bit master.

The sea moves. The still's swells are the Ocean modifier's displacement at one instant, scaled to the still's
significant wave height and faded flat beyond 140-400 m. Here the same modifier is evaluated afresh at every frame,
its time running at one second a second from the still's instant, and the same scale and fade are applied, on a new
mesh: a disc round the crossing point rather than the still's fan, because the camera ends up over and inside the
water near its centre. The disc's rings are 15 cm apart near the centre and then spread with distance; its columns
are dense over the hundred degrees in front of the camera and sparse behind it. The bump ripples drift down the
wind, each octave at the phase speed of capillary-gravity waves twice its lattice spacing long, and they evolve by
moving through the noise's third coordinate, which the surface does not otherwise use. The handover of the slope
from ripples to roughness by the pixel's footprint is the still's material, evaluated per shading point, so it
holds on every frame and from below as from above.

The camera's near clip plane stands for the port of an underwater housing (--port, metres in front of the lens).
Where the lens is just above the water and the plane is under it, the frame is split by a waterline, as an
over-under shot is.

Path A, the slow sink (6 s): the camera floats a little above the water facing the sun, riding the swell as a
float does (it follows the surface particle under it), and sinks; the waterline climbs across the frame; once
under, it tilts up to the surface from beneath and then levels. Path B, the fast plunge (4 s): the still's own
frame, 10 m up, then a fall at g that pitches down, a crossing at about 14 m/s, a burst of air bubbles, a
quadratic-drag slowdown, and a level-out a few metres down. Both keyframe the camera, so Cycles blurs its motion
over a 180-degree shutter (--shutter).

Path C, the blend (about 11.5 s): the still's frame held for three seconds with a slow drift forward (C_HOLD,
C_DRIFT), then B's fall into the glitter. Round the crossing the film slows (C_RAMP): the simulated time, which
drives the camera, the sea, the ripples, the bubbles and the white water, runs eight times slower than the film
for the last half metre and the first moments under, and 32 times slower for the instant the port goes through
the surface, as a high-speed camera would film it; the camera's motion per frame, and so its blur, shrinks with
it. At the crossing the entering camera throws a spray (Spray) and is wrapped in white water (WhiteWater), then
in B's burst of bubbles, unchanged. Under water it brakes as B does, turns up to the sun through the moving
surface, turns down and levels, and goes on forward at a steady 0.9 m/s in open water. The light under water
carries shafts (Shafts): the sun's light refracted through the same moving surface, focused into a net that is
traced and binned per frame, and laid on the water sun through the surface's shadow switch.

Path D, the glide (13.7 s): C with the fall replaced by a glide. After the same hold the camera moves forward
and down together, its speed, course and pitch each rising from the hold's along a quintic smoothstep, so that
none of them, nor their rates, has a step; it reaches 6 m/s on a course 65 degrees below level before the port
meets the water, and crosses at that steady speed (C crossed at 14 m/s after a free fall). C's 8x and 32x slow
motion are kept but eased in and out in proportion (D_RAMP); its burst of bubbles is kept; its white water builds
over the housing's entry (D_WW_ONSET); its spray becomes a fine mist streaked by its own motion (Spray).
Under water the drag builds as the housing goes in and draws the speed down towards the swim speed smoothly, and
the course turns level later than C's so that the camera levels at C's depth (path_D's constants, D_*).

Path E is D with two faults fixed; its camera, bubbles, spray and white water are D's. First, in the last half
metre above the water one ripple tilted towards the camera turned its mirror direction under the surface, and the
patch rendered grey inside a bright ring where the mirror direction crossed the low sky towards the sun: take 3's
pale disc (frames 199-211). The ripples' octaves stop at 4 cm, a pixel 20 m away, so within half a metre the sea
was smooth below that and the ripple a clean mirror tens of centimetres across. Near the camera E carries the
spectrum on to half a centimetre (capillaries, E_CAPILLARY, E_NEAR), with the slope it carries kept the still's.
Second, the glide's middle was darker than its ends, because the frame turned from the sky to the deep sea faster
than the exposure track rose; that is the track's (--ae-track), which E takes from its own file.

--layer matte renders the bubbles as a layer to lay over other footage, by triangulation matting: each frame
twice over two known backgrounds that stand in for the water behind the bubbles, solved for colour and opacity
per pixel (setup_matte, solve_matte), written as RGBA PNGs at the frame's exposure.

Above water the exposure is the still's. Under water it is metered as a camera's automatic exposure would meter
it: each rendered frame's centre-weighted geometric mean luminance is driven towards that of the theatre's shallow
water as the theatre shows it (AE_TARGET), in proportion to the share of the frame under water, lagging 0.15 s
and, while the frame still holds sky, never darker than the still's. A run of consecutive frames writes the
exposures it used to <out-prefix>-exposure-<first>-<last>.json; --ae-resume carries the metering on from such a
file into the next run of frames, and --ae-track reads merged ones back, so that frames rendered alone (the
full-size ones) take the animatic's exposure.

Above water the light is the still's: shadow rays pass the surface. Under water (WaterSun, weighted by the share of
the frame under water) the surface stops them, the sky reaches the water only refracted (Cycles's refractive
caustics on), and the sun is a lamp along its refracted direction seen by volume scattering, with a twin that only
the bubbles see. --light caustics is the earlier test: the surface stops shadow rays everywhere and no lamp.

Options (defaults in brackets), besides sea_still.py's: --path, --frames (all, or a list and ranges such as
0,30,60-90) [all], --step render every nth of them [1], --port [0.12], --shutter fraction of a frame, 0 for none
[0.5], --ae-track [none], --ae-resume [none], --under-stops a fixed exposure change under water instead of the
metering [none], --bubbles a multiplier on the burst's count, 0 for none [1], --water-sun off, under or always
[under], --light trick or caustics [trick], --keep-16bit (with --layer matte, keep each frame's two EXRs); for paths
C and D, --layer full, bubbles (the bubbles alone on a transparent background, RGBA, the water between them clear)
or matte (the triangulation-matted layer) [full], --shafts on or off [on], --white-water and
--spray multipliers on their density and count, 0 for none [1], --shaft-debug depth (a test pattern by depth
instead of the traced net), --dry (build the scene and the path, print them, render nothing). Paths C and D write their
camera, frame by frame, to <out-prefix>-camera.json.

The landing (paths D and E only): --pose-file <file> replaces path D's course with the per-frame course of a pose file
(landing_io.py, formats.md), a row to a frame: the camera's eye, forward and vertical field of view are keyed from the rows, the
simulated clock is the rows' sim_s, the surface crossing is found along them, and the sea, ripples and white water are centred
under it. The pose file's world is moved so that the crossing sits at the origin and turned about the vertical so that the
theatre's sun (az 44, el 67, as a direction through landing_io.to_blender) falls where the scene's sky has its sun; the sky's
elevation is set to the theatre's unless --sun is given. The camera file gains a `pose_f` a row and gives positions in the
pose file's frame. --bubble-export <file> writes the burst's arrays (crossing-bubbles 1) in Unity axes and pose-file
positions; --export-only builds just what it needs (the scene's setup, the sea's probe, the bubbles), writes it and exits.
--bubble-export without --pose-file (path D or E) writes B3's own burst in B3's own frame, not placed: the sea frame's arrays
through landing_io.to_unity (axes only), with --world-minus-sim (required there) as the file's; map_bubbles.py places it in the
theatre's world. --export-only works there too.
"""
import json, math, os, sys, time
import bpy
import bmesh
import numpy as np
from mathutils import Vector, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sea_still as SS
import landing_io as LIO
import sea_dive_poses as PZ

FPS = 30
G = 9.81
PATH_SECONDS = {"A": 6.0, "B": 4.0}           # C's length follows from its speed ramp
DR0, CR, DPHI_FRONT, DPHI_BACK = 0.15, 0.004, 0.0012, 0.012   # the disc at full size: rings, columns
FRONT_HALF, FRONT_BLEND = math.radians(50.0), math.radians(20.0)
PORT = 0.12                               # m: set from --port
FAR_RATIO = 1.08                          # ring spacing beyond the waves, where the sea is flat
AE_LAG = 0.15                             # s: the automatic exposure's lag
# The automatic exposure's target: the centre-weighted geometric mean of the frame's display-linear luminance
# that a frame of the theatre's shallow water would have as the theatre shows it (SHALLOW at the water world's
# gain 0.9, exposure 0.55 and PBR Neutral, about sRGB 2, 71, 90).
AE_TARGET = 0.0526

def parse():
    ap = SS.still_parser("sea_dive.py")
    ap.add_argument("--path", choices=("A", "B", "C", "D", "E"), required=True)
    ap.add_argument("--frames", default="all")
    ap.add_argument("--step", type=int, default=1)
    ap.add_argument("--port", type=float, default=0.12)
    ap.add_argument("--shutter", type=float, default=0.5)
    ap.add_argument("--under-stops", type=float, default=None)
    ap.add_argument("--ae-track", default=None)
    ap.add_argument("--ae-resume", default=None)
    ap.add_argument("--bubbles", type=float, default=1.0)
    ap.add_argument("--light", choices=("trick", "caustics"), default="trick")
    ap.add_argument("--keep-16bit", action="store_true")
    ap.add_argument("--water-sun", choices=("off", "under", "always"), default="under")
    ap.add_argument("--layer", choices=("full", "bubbles", "matte"), default="full")
    ap.add_argument("--shafts", choices=("on", "off"), default="on")
    ap.add_argument("--white-water", type=float, default=1.0)
    ap.add_argument("--foam-gain", type=float, default=1.0)
    ap.add_argument("--foam-light", type=float, default=1.0)
    ap.add_argument("--foam-window", default=None)
    ap.add_argument("--spray", type=float, default=1.0)
    ap.add_argument("--shaft-debug", choices=("depth",), default=None)
    ap.add_argument("--shaft-ab", action="store_true")
    ap.add_argument("--shaft-medium", type=float, default=None)
    ap.add_argument("--dry", action="store_true")
    ap.add_argument("--pose-file", default=None)
    ap.add_argument("--bubble-export", default=None)
    ap.add_argument("--export-only", action="store_true")
    ap.add_argument("--fizz-export", default=None)
    ap.add_argument("--world-minus-sim", type=float, default=None)
    args = ap.parse_args(SS.script_argv())
    args.foam = None
    if args.foam_window is not None:
        try:
            win = [float(x) for x in args.foam_window.split(",")]
        except ValueError:
            ap.error("--foam-window takes four numbers, a,b,c,d, in sim seconds")
        if len(win) != 4:
            ap.error("--foam-window takes four numbers, a,b,c,d, in sim seconds")
        if not (win[0] < win[1] <= win[2] < win[3]):
            ap.error("--foam-window a,b,c,d must be in increasing order (a < b <= c < d)")
        if args.foam_gain == 1.0 and args.foam_light == 1.0:
            ap.error("--foam-window with --foam-gain and --foam-light both 1 does nothing")
        args.foam = (win[0], win[1], win[2], win[3], args.foam_gain, args.foam_light)
        print("FOAM window %.4f %.4f %.4f %.4f s, density x%g, light x%g" % (*win, args.foam_gain, args.foam_light), flush=True)
    elif args.foam_gain != 1.0 or args.foam_light != 1.0:
        ap.error("--foam-gain and --foam-light need --foam-window")
    if args.pose_file:
        if args.path not in ("D", "E"):
            ap.error("--pose-file takes the place of path D's course: use it with --path D or E")
        if not any(a == "--sun" or a.startswith("--sun=") for a in SS.script_argv()):
            args.sun = PZ.THEATRE_SUN_EL            # the theatre's sun, as a direction (see pose_setup for the bearing)
    if args.bubble_export and not (args.bubbles > 0 and (args.pose_file or args.path in ("D", "E"))):
        ap.error("--bubble-export needs --bubbles above 0, and --pose-file or --path D or E")
    if args.bubble_export and not args.pose_file and args.world_minus_sim is None:
        ap.error("--bubble-export without --pose-file (B3's own burst) needs --world-minus-sim")
    if args.world_minus_sim is not None and (args.pose_file or not args.bubble_export):
        ap.error("--world-minus-sim is for --bubble-export without --pose-file only (a pose file carries its own)")
    if args.export_only and not args.bubble_export:
        ap.error("--export-only needs --bubble-export")
    if args.fizz_export and not (args.export_only and args.path == "D" and not args.pose_file
                                 and args.world_minus_sim is not None):
        ap.error("--fizz-export needs --export-only, --path D, no --pose-file and --world-minus-sim (B3's own frame)")
    return args

def frame_list(spec, n, step):
    if spec == "all":
        fs = list(range(n))
    else:
        fs = []
        for part in spec.split(","):
            if "-" in part:
                a, b = part.split("-")
                fs += list(range(int(a), int(b) + 1))
            else:
                fs.append(int(part))
    return [f for f in fs[::step] if 0 <= f < n]

def sstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)

def pchip(keys, t):
    """Monotone cubic through (time, value) keys, level at both ends."""
    x = np.array([k[0] for k in keys], float)
    y = np.array([k[1] for k in keys], float)
    h, d = np.diff(x), np.diff(y) / np.diff(x)
    m = np.zeros_like(y)
    for i in range(1, len(y) - 1):
        if d[i - 1] * d[i] > 0:
            w1, w2 = 2 * h[i] + h[i - 1], h[i] + 2 * h[i - 1]
            m[i] = (w1 + w2) / (w1 / d[i - 1] + w2 / d[i])
    t = min(max(t, x[0]), x[-1])
    i = int(min(max(np.searchsorted(x, t, "right") - 1, 0), len(x) - 2))
    s = (t - x[i]) / h[i]
    return float((2 * s ** 3 - 3 * s ** 2 + 1) * y[i] + (s ** 3 - 2 * s ** 2 + s) * h[i] * m[i]
                 + (-2 * s ** 3 + 3 * s ** 2) * y[i + 1] + (s ** 3 - s ** 2) * h[i] * m[i + 1])

# ---------------------------------------------------------------- the ripples' motion
def ripple_motion(wind_dir):
    """Each octave drifts at the phase speed of a capillary-gravity wave twice its lattice spacing long, in the
    direction the Ocean modifier's swells travel (measured here, since the modifier's sign convention is not
    documented), and decorrelates over about three of its periods."""
    n, step = 512, 0.375
    u = np.array([math.cos(wind_dir), math.sin(wind_dir), 0.0])
    P = (np.arange(n)[:, None] * step) * u[None, :]
    me = bpy.data.meshes.new("swell-line")
    me.vertices.add(n)
    me.vertices.foreach_set("co", P.astype(np.float32).ravel())
    ob = bpy.data.objects.new("swell-line", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = SS.ocean_modifier(ob, wind_dir, SS.OCEAN_TIME)
    z0 = SS.ocean_offsets(ob, P)[:, 2]
    mod.time = SS.OCEAN_TIME + 0.25
    z1 = SS.ocean_offsets(ob, P)[:, 2]
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    best = None
    for s in range(-12, 13):                      # cells of 0.375 m: up to 4.5 m in 0.25 s
        a, b = (z0[:n - s], z1[s:]) if s >= 0 else (z0[-s:], z1[:n + s])
        c = float(np.corrcoef(a, b)[0, 1])
        if best is None or c > best[1]:
            best = (s, c)
    sign = 1.0 if best[0] >= 0 else -1.0
    print("SWELL travels %s the wind direction: best shift %+.2f m in 0.25 s (r %.3f), %.1f m/s"
          % ("along" if sign > 0 else "against", best[0] * step, best[1], best[0] * step / 0.25), flush=True)
    drift, evolve = [], []
    for lat in SS.RIPPLE_LATTICE:
        lam = 2.0 * lat
        k = 2 * math.pi / lam
        c = math.sqrt(G / k + 0.0728 * k / 1025.0)
        drift.append(sign * c)
        evolve.append(0.35 * c / lam)
    print("RIPPLE motion: drift %s m/s, evolve %s lattice units/s"
          % (", ".join("%.2f" % abs(v) for v in drift), ", ".join("%.2f" % v for v in evolve)), flush=True)
    return {"drift": drift, "evolve": evolve}

# ---------------------------------------------------------------- the disc of sea
def column_angles(az, s):
    """Column azimuths round the full circle, dense within FRONT_HALF of the view's azimuth `az`."""
    out, a = [], -math.pi
    while a < math.pi - 1e-9:
        out.append(a)
        off = abs(a)
        k = sstep(FRONT_HALF, FRONT_HALF + FRONT_BLEND, off)
        a += s * (DPHI_FRONT * (1 - k) + DPHI_BACK * k)
    return az + np.array(out)

def ring_radii(s):
    r, out = 0.0, []
    while r < SS.WAVE_FADE[1]:
        r += max(DR0, s * CR * r)
        out.append(r)
    while r < SS.FAR:
        r = min(SS.FAR, r * FAR_RATIO)
        out.append(r)
    return np.array(out)

def build_disc(az, s):
    rr = ring_radii(s)
    phi = column_angles(az, s)
    nr, nc = len(rr), len(phi)
    X = rr[:, None] * np.cos(phi)[None, :]
    Y = rr[:, None] * np.sin(phi)[None, :]
    P = np.concatenate([np.zeros((1, 3)), np.stack([X, Y, np.zeros_like(X)], -1).reshape(-1, 3)])
    rv = np.concatenate([[0.0], np.repeat(rr, nc)])
    idx = 1 + np.arange(nr * nc, dtype=np.int64).reshape(nr, nc)
    nxt = np.roll(idx, -1, axis=1)
    tris = np.stack([np.zeros(nc, np.int64), idx[0], nxt[0]], -1)
    quads = np.stack([idx[:-1], idx[1:], nxt[1:], nxt[:-1]], -1).reshape(-1, 4)
    loops = np.concatenate([tris.ravel(), quads.ravel()]).astype(np.int32)
    starts = np.concatenate([np.arange(len(tris)) * 3, len(tris) * 3 + np.arange(len(quads)) * 4]).astype(np.int32)
    me = bpy.data.meshes.new("sea-disc")
    me.vertices.add(len(P))
    me.vertices.foreach_set("co", P.astype(np.float32).ravel())
    me.loops.add(len(loops))
    me.loops.foreach_set("vertex_index", loops)
    me.polygons.add(len(starts))
    me.polygons.foreach_set("loop_start", starts)
    me.update(calc_edges=True)
    if me.validate(verbose=True):
        raise SystemExit("the disc mesh was invalid")
    me.shade_smooth()
    print("DISC %d rings x %d columns, %d vertices, %d faces; rings %.2f..%.0f m" % (nr, nc, len(P), len(starts), rr[0], rr[-1]),
          flush=True)
    return me, P, rv

def no_deform_blur(ob):
    """The mesh is rewritten between frames, not animated, so there is no deformation to blur; this spares Cycles
    evaluating it at the shutter's ends."""
    for holder, attr in ((ob, "use_motion_blur"), (getattr(ob, "cycles", None), "use_motion_blur"),
                         (getattr(ob, "cycles", None), "use_deform_motion")):
        if holder is not None and hasattr(holder, attr):
            try:
                setattr(holder, attr, False)
            except Exception:
                pass

class Sea:
    """The render mesh, closed into a volume by the still's solidify, and a flat twin carrying the Ocean modifier,
    whose displacement is scaled and faded as the still's is and written into the render mesh each frame."""
    def __init__(self, old, az, s, k, wind_dir):
        mat, old_me = old.data.materials[0], old.data
        bpy.data.objects.remove(old, do_unlink=True)
        bpy.data.meshes.remove(old_me)
        me, self.P, self.rv = build_disc(az, s)
        self.k = k
        self.w = 1.0 - SS.smoothstep(SS.WAVE_FADE[0], SS.WAVE_FADE[1], self.rv)
        self.ob = bpy.data.objects.new("sea", me)
        bpy.context.scene.collection.objects.link(self.ob)
        me.materials.append(mat)
        sol = self.ob.modifiers.new("deep", "SOLIDIFY")
        sol.thickness, sol.offset, sol.use_rim, sol.use_even_offset = 300.0, -1.0, True, False
        twin = me.copy()
        self.twin = bpy.data.objects.new("sea-ocean", twin)
        bpy.context.scene.collection.objects.link(self.twin)
        self.twin.hide_render = True
        self.mod = SS.ocean_modifier(self.twin, wind_dir)
        no_deform_blur(self.ob)
    def at(self, t):
        self.mod.time = SS.OCEAN_TIME + t
        D = SS.ocean_offsets(self.twin, self.P)
        Q = self.P + D * (self.k * self.w)[:, None]
        me = self.ob.data
        me.vertices.foreach_set("co", Q.astype(np.float32).ravel())
        me.update()

class Probe:
    """A small grid of the same ocean round a point, for the surface under the camera, frame by frame."""
    def __init__(self, centre, wind_dir, k, n=41, step=0.2):
        h = (n - 1) * step / 2
        xs = centre[0] - h + step * np.arange(n)
        ys = centre[1] - h + step * np.arange(n)
        X, Y = np.meshgrid(xs, ys, indexing="ij")
        self.P = np.stack([X, Y, np.zeros_like(X)], -1).reshape(-1, 3)
        self.x0, self.y0, self.step, self.n, self.k = xs[0], ys[0], step, n, k
        me = bpy.data.meshes.new("probe-grid")
        me.vertices.add(len(self.P))
        me.vertices.foreach_set("co", self.P.astype(np.float32).ravel())
        self.ob = bpy.data.objects.new("probe-grid", me)
        bpy.context.scene.collection.objects.link(self.ob)
        self.ob.hide_render = True
        self.mod = SS.ocean_modifier(self.ob, wind_dir)
        self.cache = {}
    def field(self, t):
        if t not in self.cache:
            self.mod.time = SS.OCEAN_TIME + t
            self.cache[t] = (SS.ocean_offsets(self.ob, self.P) * self.k).reshape(self.n, self.n, 3)
        return self.cache[t]
    def disp(self, t, x, y):
        """The displacement of the surface particle whose rest position is (x, y), bilinear."""
        D = self.field(t)
        fx = min(max((x - self.x0) / self.step, 0.0), self.n - 1.001)
        fy = min(max((y - self.y0) / self.step, 0.0), self.n - 1.001)
        i, j = int(fx), int(fy)
        a, b = fx - i, fy - j
        return ((1 - a) * (1 - b) * D[i, j] + a * (1 - b) * D[i + 1, j] + (1 - a) * b * D[i, j + 1] + a * b * D[i + 1, j + 1])
    def height(self, t, x, y):
        """The surface's height at (x, y): the rest position whose particle lands there, found by iteration."""
        bx, by = x, y
        for _ in range(8):
            d = self.disp(t, bx, by)
            bx, by = x - d[0], y - d[1]
        return float(self.disp(t, bx, by)[2])

# ---------------------------------------------------------------- the landing: a pose file in place of path D's course
def pose_setup(args, ctx):
    """The states, the entry and the probe for --pose-file. The pose file's rows become frames (sea_dive_poses.rows_to_states);
    the world is moved so that the crossing sits at the origin and turned by psi about the vertical, where psi is the bearing the
    sky's sun really has less the theatre's sun bearing (the sun as a Blender vector, through landing_io.to_blender), so that the
    camera sees the sun where the theatre's camera does; the probe is then centred under the crossing and the crossing found
    along the frames with the moving sea. Returns the parts main needs."""
    doc = LIO.read_poses(args.pose_file)
    raw = PZ.rows_to_states(doc["rows"])
    sun_b = PZ.theatre_sun_blender()
    psi = ctx["az"] - PZ.sun_bearing(sun_b)
    _, c0 = PZ.flat_crossing(raw, PORT)
    centre = (float(c0[0]), float(c0[1]))
    st = PZ.place(raw, centre, psi)
    probe = Probe((0.0, 0.0), ctx["wind_dir"], ctx["ocean"]["k"], n=161, step=0.25)
    e = PZ.find_crossing(st, PORT, lambda t, x, y: probe.height(round(t * FPS) / FPS, x, y))
    n = len(st["t"])
    states = [{"t": float(st["t"][i]), "tf": float(st["tf"][i]), "loc": Vector(st["loc"][i].tolist()),
               "pitch": float(st["pitch"][i]), "roll": 0.0, "vel": Vector(st["vel"][i].tolist()),
               "dir": Vector(st["dir"][i].tolist()), "vfov": float(st["vfov"][i]), "pose_f": int(st["f"][i])}
              for i in range(n)]
    entry = {"t": e["t"], "frame": e["frame"], "loc": Vector(e["loc"].tolist()), "v": e["v"], "beta": e["beta"], "hold": 0.0,
             "dir": Vector(e["dir"].tolist())}
    d = e["dir"]
    az_disc = math.atan2(d[1], d[0]) if math.hypot(d[0], d[1]) > 1e-3 else math.atan2(states[e["frame"]]["dir"].y,
                                                                                   states[e["frame"]]["dir"].x)
    print("POSE %s: %d rows, sim_s %.3f..%.3f; sun az %.2f el %.2f deg in Blender axes (%.4f %.4f %.4f), the scene's sun at %.2f"
          " deg: turned by %.2f deg; crossing at pose-frame (%.3f, %.3f), frame %d (pose f %d), sim %.4f s, %.2f m/s, %.1f deg below"
          " level; sea centred there" % (os.path.basename(args.pose_file), n, st["t"][0], st["t"][-1],
                                          math.degrees(PZ.sun_bearing(sun_b)), math.degrees(math.asin(sun_b[2])), *sun_b,
                                          math.degrees(ctx["az"]), math.degrees(psi), centre[0], centre[1], e["frame"],
                                          states[e["frame"]]["pose_f"], e["t"], e["v"], math.degrees(e["beta"])), flush=True)
    return {"doc": doc, "states": states, "entry": entry, "probe": probe, "az_disc": az_disc, "centre": centre, "psi": psi}

def git_source():
    """This script's commit, and whether sea_dive.py or sea_dive_poses.py differ from it."""
    import subprocess
    try:
        head = subprocess.run(["git", "-C", HERE, "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
        dirty = subprocess.run(["git", "-C", HERE, "status", "--porcelain", "--", "sea_dive.py", "sea_dive_poses.py"],
                               capture_output=True, text=True, check=True).stdout.strip()
        return head + (" (sea_dive.py modified since)" if dirty else "")
    except Exception as ex:
        return "unknown (%s)" % ex

def write_bubble_export(args, pose, bubbles):
    """The Bubbles instance's arrays, in Unity axes and the pose file's frame, through landing_io.write_bubbles."""
    x0, v0 = PZ.bubble_export_arrays(bubbles.x0, bubbles.v0, pose["centre"], pose["psi"])
    src = "sea_dive.py commit %s; --path %s --bubbles %g --port %g, bubble seed 11; pose file %s sha256 %s" % (
        git_source(), args.path, args.bubbles, args.port, os.path.basename(args.pose_file), LIO.sha256(args.pose_file))
    os.makedirs(os.path.dirname(os.path.abspath(args.bubble_export)), exist_ok=True)
    LIO.write_bubbles(args.bubble_export, x0, v0, bubbles.ts, bubbles.r, bubbles.rise, bubbles.tau, bubbles.flat,
                      pose["doc"]["world_minus_sim"], src)
    print("BUBBLE EXPORT %d bubbles to %s" % (bubbles.n, args.bubble_export), flush=True)

def write_own_bubble_export(args, bubbles):
    """B3's own frame, not placed: the Bubbles instance's arrays in the sea frame this script renders in, converted with
    landing_io.to_unity (the axis swap only, no turn and no translation). map_bubbles.py places them in the theatre's world.
    world_minus_sim comes from --world-minus-sim."""
    src = ("sea_dive.py commit %s; B3's own frame, not placed (axes swapped by landing_io.to_unity only); --path %s --bubbles %g"
           " --port %g --world-minus-sim %.9g, bubble seed 11" % (git_source(), args.path, args.bubbles, args.port,
                                                                 args.world_minus_sim))
    os.makedirs(os.path.dirname(os.path.abspath(args.bubble_export)), exist_ok=True)
    LIO.write_bubbles(args.bubble_export, LIO.to_unity(bubbles.x0), LIO.to_unity(bubbles.v0), bubbles.ts, bubbles.r,
                      bubbles.rise, bubbles.tau, bubbles.flat, args.world_minus_sim, src)
    print("BUBBLE EXPORT (B3's own frame) %d bubbles to %s" % (bubbles.n, args.bubble_export), flush=True)

def write_own_fizz_export(args, fizz):
    """The fizz's arrays in B3's own frame, as write_own_bubble_export writes the burst's: the same closed form, with tau
    FZ_TAU and flat 1 for every bubble."""
    src = ("sea_dive.py commit %s; B3's own frame, not placed (axes swapped by landing_io.to_unity only); the fizz (class Fizz)"
           "; --path %s --white-water %g --port %g --world-minus-sim %.9g, fizz seed 31" % (
               git_source(), args.path, args.white_water, args.port, args.world_minus_sim))
    os.makedirs(os.path.dirname(os.path.abspath(args.fizz_export)), exist_ok=True)
    LIO.write_bubbles(args.fizz_export, LIO.to_unity(fizz.x0), LIO.to_unity(fizz.v0), fizz.ts, fizz.r, fizz.rise,
                      np.full(fizz.n, Fizz.FZ_TAU), np.ones(fizz.n), args.world_minus_sim, src)
    print("FIZZ EXPORT (B3's own frame) %d bubbles to %s" % (fizz.n, args.fizz_export), flush=True)

def run_export_only(args, ctx):
    """--export-only: the pose (or B3's own path), the probe the surface test needs and the bubbles; written, and no render."""
    if args.pose_file:
        pose = pose_setup(args, ctx)
        bubbles = Bubbles(pose["states"], pose["entry"], pose["probe"], 6000 * args.bubbles)
        write_bubble_export(args, pose, bubbles)
    else:
        az = ctx["az"]
        probe = Probe((math.cos(az) * 9.0, math.sin(az) * 9.0), ctx["wind_dir"], ctx["ocean"]["k"], n=113, step=0.25)
        states, entry = path_D(probe, az)
        bubbles = Bubbles(states, entry, probe, 6000 * args.bubbles)
        write_own_bubble_export(args, bubbles)
        if args.fizz_export:
            write_own_fizz_export(args, Fizz(entry, states, probe, 40000 * args.white_water))
    print("EXPORT-ONLY done", flush=True)

def key_camera_pose(cam, states):
    """key_camera for a pose file: the view direction is each frame's own, and the lens follows each frame's vertical field of
    view (the sensor fit is vertical)."""
    prefs = bpy.context.preferences.edit
    try:
        prefs.keyframe_new_interpolation_type = "LINEAR"
    except Exception as e:
        print("WARN keyframe interpolation:", e, flush=True)
    cam.data.sensor_fit = "VERTICAL"
    prev = None
    for f, s in enumerate(states):
        q = s["dir"].normalized().to_track_quat("-Z", "Y")
        e = q.to_euler("XYZ", prev) if prev is not None else q.to_euler("XYZ")
        prev = e
        cam.location = s["loc"]
        cam.rotation_euler = e
        cam.data.lens = 0.5 * cam.data.sensor_height / math.tan(0.5 * s["vfov"])
        cam.keyframe_insert("location", frame=f)
        cam.keyframe_insert("rotation_euler", frame=f)
        cam.data.keyframe_insert("lens", frame=f)

# ---------------------------------------------------------------- the two camera paths
def path_A(probe, n):
    """The slow sink: a float that rides the surface particle at the origin, sinks through it and tilts up."""
    hrel_k = [(0.0, 0.55), (1.0, 0.32), (1.9, 0.045), (3.0, -0.085), (3.5, -0.22), (4.4, -0.95), (5.2, -1.55), (6.0, -1.9)]
    pitch_k = [(0.0, 2.0), (1.5, 13.0), (3.0, 13.0), (3.5, 16.0), (4.5, 40.0), (4.9, 40.0), (6.0, 8.0)]
    follow_k = [(0.0, 1.0), (3.0, 1.0), (4.6, 0.3), (6.0, 0.2)]
    out = []
    for f in range(n):
        t = f / FPS
        d = probe.disp(t, 0.0, 0.0)
        fo = pchip(follow_k, t)
        loc = Vector((fo * d[0], fo * d[1], fo * d[2] + pchip(hrel_k, t)))
        out.append({"t": t, "loc": loc, "pitch": pchip(pitch_k, t), "roll": 0.0})
    return out, None

def path_B(probe, n, az):
    """The fast plunge: the still's frame, then a fall at g pitching down, timed so that a frame falls exactly
    on the crossing; under water a quadratic-drag slowdown, the course and the camera levelling."""
    fwd = Vector((math.cos(az), math.sin(az), 0.0))
    dt = 1.0 / (FPS * 20)
    def fall(hold, upto, above=0.0):
        """Integrate the fall from rest; return the states every substep until the camera is `above` metres over
        the surface, or `upto` s."""
        x, z, t, vz = 0.0, 10.0, 0.0, 0.0
        st = []
        while t <= upto + 1e-9:
            vf = 1.2 * sstep(0.0, 1.0, t)
            st.append((t, x, z, vf, vz))
            if t > hold:
                vz -= G * dt
            x += vf * dt
            z += vz * dt
            t += dt
            if z <= probe.height(round(t * FPS) / FPS, fwd.x * x, fwd.y * x) + above:
                break
        return st
    # The port's centre meets the surface when the lens is port * sin(pitch) above it. A hold before the fall puts
    # that moment on a frame, so that frame's shutter straddles the crossing.
    p_e = -62.0
    above = PORT * math.sin(math.radians(-p_e))
    st = fall(0.0, 3.0, above)
    t_free = st[-1][0]
    f_e = int(math.ceil(t_free * FPS))
    hold = f_e / FPS - t_free
    st = fall(hold, 3.0)
    t_e, x_e, z_e, vf_e, vz_e = st[-1]
    v_e = math.hypot(vf_e, vz_e)
    beta_e = math.atan2(-vz_e, vf_e)
    print("PLUNGE hold %.3f s; the port meets the surface on frame %d (%.3f s), the lens at %.3f s, %.2f m/s, %.1f deg below"
          " level" % (hold, f_e, f_e / FPS, t_e, v_e, math.degrees(beta_e)), flush=True)
    L, v_min, beta_end = 0.9, 0.8, math.radians(6.0)
    out = []
    x, z = x_e, z_e
    tau_prev = 0.0
    under = []
    for f in range(n):
        t = f / FPS
        if t < t_e - 1e-9:
            i = min(range(len(st)), key=lambda j: abs(st[j][0] - t))
            _, xx, zz, _, _ = st[i]
            pitch = -11.0 + (p_e + 11.0) * sstep(0.05, t_e, t)
            out.append({"t": t, "loc": Vector((fwd.x * xx, fwd.y * xx, zz)), "pitch": pitch, "roll": 0.0})
        else:
            tau = t - t_e
            sub = max(1, int(round((tau - tau_prev) / dt)))
            for j in range(sub):
                tt = tau_prev + (j + 0.5) * (tau - tau_prev) / sub
                v = max(v_min, v_e / (1 + v_e * tt / L))
                beta = beta_e + (beta_end - beta_e) * sstep(0.15, 1.6, tt)
                h = (tau - tau_prev) / sub
                x += v * math.cos(beta) * h
                z -= v * math.sin(beta) * h
            tau_prev = tau
            pitch = p_e + (-3.0 - p_e) * sstep(0.1, 2.1, tau)
            out.append({"t": t, "loc": Vector((fwd.x * x, fwd.y * x, z)), "pitch": pitch, "roll": 0.0})
    entry = {"t": t_e, "frame": f_e, "loc": Vector((fwd.x * x_e, fwd.y * x_e, z_e)), "v": v_e, "beta": beta_e}
    # the velocity at every frame, for the bubbles
    for i in range(len(out)):
        a, b = out[max(0, i - 1)], out[min(len(out) - 1, i + 1)]
        out[i]["vel"] = (b["loc"] - a["loc"]) / max(1e-6, b["t"] - a["t"])
        p = math.radians(out[i]["pitch"])
        out[i]["dir"] = Vector((math.cos(az) * math.cos(p), math.sin(az) * math.cos(p), math.sin(p)))
    return out, entry

def key_camera(cam, states, az):
    prefs = bpy.context.preferences.edit
    try:
        prefs.keyframe_new_interpolation_type = "LINEAR"
    except Exception as e:
        print("WARN keyframe interpolation:", e, flush=True)
    prev = None
    for f, s in enumerate(states):
        p = math.radians(s["pitch"])
        d = Vector((math.cos(az) * math.cos(p), math.sin(az) * math.cos(p), math.sin(p)))
        q = d.to_track_quat("-Z", "Y")
        if s["roll"]:
            q = Quaternion(d, math.radians(s["roll"])) @ q
        e = q.to_euler("XYZ", prev) if prev is not None else q.to_euler("XYZ")
        prev = e
        cam.location = s["loc"]
        cam.rotation_euler = e
        cam.keyframe_insert("location", frame=f)
        cam.keyframe_insert("rotation_euler", frame=f)

# ---------------------------------------------------------------- path C, the blend
C_HOLD = 3.0              # s: the still's own frame, held before the fall (a fraction of a frame is added: see path_C)
C_DRIFT = 0.10            # m/s: the hold's drift forward, towards the sun
C_FWD = 1.2               # m/s: the fall's forward speed, as B's
C_PITCH_E = -58.0         # deg: the pitch at the crossing (B's -62, a little less steep, to keep the glitter in frame)
C_UNDER = 6.0             # s of simulated time after the crossing
C_DRAG_L = 0.9            # m: B's quadratic-drag length under water
C_COURSE = (0.15, 1.6)    # s after the crossing over which the course turns from the entry's to level
C_SWIM = [(0.0, 0.6), (2.6, 0.6), (4.6, 0.9), (C_UNDER, 0.9)]        # m/s: the floor of the speed under water
C_PITCH_UNDER = [(0.0, C_PITCH_E), (0.25, -56.0), (0.7, -38.0), (1.2, -8.0), (1.8, 24.0), (2.4, 36.0), (3.2, 38.0),
                 (3.8, 26.0), (4.6, 2.0), (5.2, -4.0), (C_UNDER, -4.0)]
# The speed ramp, in s of simulated time from the crossing: each window rises from the one below it over its first
# pair of times to its top (film seconds per simulated second) and falls back over its second pair.
C_RAMP = (((-0.075, -0.035, 0.030, 0.120), 8.0), ((-0.008, -0.003, 0.003, 0.010), 32.0))
C_DT = 1.0 / 6000.0       # s: the integration step

def c_ramp(tau):
    k, below = 1.0, 1.0
    for (a, b, c, d), top in C_RAMP:
        k += (top - below) * sstep(a, b, tau) * (1.0 - sstep(c, d, tau))
        below = top
    return k

def c_extra_before(ramp=None):
    """The film seconds the ramp adds before the crossing."""
    ramp = ramp or c_ramp
    taus = np.arange(-0.4, 0.0, C_DT) if ramp is not c_ramp else np.arange(-0.2, 0.0, C_DT)
    return float(sum(ramp(x) - 1.0 for x in taus) * C_DT)

# Path D's speed ramp: C's two windows, 8x and 32x, but each rises and falls in proportion (the log of the slowing
# follows a smoothstep) and over a longer span: the first over the last 1.2 m before the crossing rather than C's
# last 0.5 m, the second over 15 ms rather than 5. So the speed on film eases down into the slow motion and back out
# of it over 10 to 20 frames; C's ramps took it from 6 m/s to 0.75 within five frames and to 0.19 within three.
D_RAMP = (((-0.20, -0.035, 0.030, 0.180), 8.0), ((-0.020, -0.005, 0.005, 0.020), 32.0))

def d_ramp(tau):
    lk, below = 0.0, 1.0
    for (a, b, c, d), top in D_RAMP:
        lk += math.log(top / below) * sstep(a, b, tau) * (1.0 - sstep(c, d, tau))
        below = top
    return math.exp(lk)

def path_C(probe, az):
    """The blend: the still's frame held, B's fall, a speed ramp round the crossing, and under water B's braking,
    a turn up to the sun, a turn down and a steady course forward. Returns one state a film frame, each with its
    simulated time "t" and its film time "tf"."""
    fwd = Vector((math.cos(az), math.sin(az), 0.0))
    above = PORT * math.sin(math.radians(-C_PITCH_E))
    def fall(hold):
        x, z, vz, t = 0.0, 10.0, 0.0, 0.0
        T, X, Z, VF, VZ = [], [], [], [], []
        while True:
            vf = C_DRIFT + (C_FWD - C_DRIFT) * sstep(hold, hold + 1.0, t)
            T.append(t); X.append(x); Z.append(z); VF.append(vf); VZ.append(vz)
            if t > hold:
                vz -= G * C_DT
            x += vf * C_DT
            z += vz * C_DT
            t += C_DT
            if z < 1.5 and z <= probe.height(round(t * FPS) / FPS, fwd.x * x, fwd.y * x) + above:
                T.append(t); X.append(x); Z.append(z); VF.append(vf); VZ.append(vz)
                return np.array(T), np.array(X), np.array(Z), np.array(VF), np.array(VZ)
            if t > hold + 4.0:
                raise SystemExit("path C: the fall never met the surface")
    # Lengthen the hold by a fraction of a frame so that a film frame falls on the crossing.
    extra = c_extra_before()
    hold = C_HOLD
    for it in range(3):
        T, X, Z, VF, VZ = fall(hold)
        F_e = (T[-1] + extra) * FPS
        hold += ((math.ceil(F_e) if it == 0 else round(F_e)) - F_e) / FPS
    T, X, Z, VF, VZ = fall(hold)
    t_e, x_e, z_e, vf_e, vz_e = T[-1], X[-1], Z[-1], VF[-1], VZ[-1]
    v_e = math.hypot(vf_e, vz_e)
    beta_e = math.atan2(-vz_e, vf_e)
    pitch_above = -11.0 + (C_PITCH_E + 11.0) * np.array([sstep(hold + 0.05, t_e, t) for t in T])
    # under water
    n_u = int(round(C_UNDER / C_DT))
    tu = np.arange(1, n_u + 1) * C_DT
    tab = np.linspace(0.0, C_UNDER, 1201)
    tm = tu - 0.5 * C_DT
    v = np.maximum(np.interp(tm, tab, [pchip(C_SWIM, x) for x in tab]), v_e / (1.0 + v_e * tm / C_DRAG_L))
    beta = beta_e * (1.0 - np.array([sstep(C_COURSE[0], C_COURSE[1], x) for x in tm]))
    xu = x_e + np.cumsum(v * np.cos(beta) * C_DT)
    zu = z_e - np.cumsum(v * np.sin(beta) * C_DT)
    pitch_under = np.interp(tu, tab, [pchip(C_PITCH_UNDER, x) for x in tab])
    t_all = np.concatenate([T, t_e + tu])
    x_all = np.concatenate([X, xu])
    z_all = np.concatenate([Z, zu])
    p_all = np.concatenate([pitch_above, pitch_under])
    k_all = np.array([c_ramp(t - t_e) for t in t_all])
    F_all = np.concatenate([[0.0], np.cumsum(0.5 * (k_all[1:] + k_all[:-1]) * np.diff(t_all))])
    n = int(math.floor(F_all[-1] * FPS + 1e-9)) + 1
    F = np.arange(n) / FPS
    tf = np.interp(F, F_all, t_all)
    xf, zf, pf = np.interp(tf, t_all, x_all), np.interp(tf, t_all, z_all), np.interp(tf, t_all, p_all)
    states = [{"t": float(tf[f]), "tf": float(F[f]), "loc": Vector((fwd.x * xf[f], fwd.y * xf[f], zf[f])),
               "pitch": float(pf[f]), "roll": 0.0} for f in range(n)]
    f_e = int(np.argmin(np.abs(tf - t_e)))
    for i in range(n):
        a, b = states[max(0, i - 1)], states[min(n - 1, i + 1)]
        states[i]["vel"] = (b["loc"] - a["loc"]) / max(1e-9, b["t"] - a["t"])
        p = math.radians(states[i]["pitch"])
        states[i]["dir"] = Vector((math.cos(az) * math.cos(p), math.sin(az) * math.cos(p), math.sin(p)))
    entry = {"t": t_e, "frame": f_e, "loc": Vector((fwd.x * x_e, fwd.y * x_e, z_e)), "v": v_e, "beta": beta_e,
             "hold": hold, "dir": Vector((fwd.x * math.cos(beta_e), fwd.y * math.cos(beta_e), -math.sin(beta_e)))}
    print("PATH C: hold %.4f s, the fall %.3f s; the port meets the surface at %.4f s simulated, %.4f s on film, frame %d"
          " (simulated %.5f s, %.2f ms off); %.2f m/s, %.1f deg below level; %d frames, %.2f s of film for %.2f s simulated"
          % (hold, t_e - hold, t_e, F_all[np.searchsorted(t_all, t_e)], f_e, tf[f_e], 1e3 * (tf[f_e] - t_e), v_e,
             math.degrees(beta_e), n, F[-1], t_all[-1]), flush=True)
    return states, entry

# ---------------------------------------------------------------- path D, the glide
# Path C with the fall replaced: from the hold the camera glides forward and down, its speed, its course and its
# pitch each rising from the hold's along a quintic smoothstep (zero rate and zero acceleration at both ends), so
# that speed, course and pitch, and their rates, are continuous. All three have reached their entry values before
# the port meets the water, so the camera crosses at a steady D_VE on a straight course. Under water the drag builds
# over D_DRAG_IN as the housing goes in (no step in the deceleration) and draws the speed down towards the swim
# speed rather than clipping at it.
D_VE = 6.0                # m/s: the speed at the crossing
D_GAMMA_E = 65.0          # deg below level: the course at the crossing
D_TV, D_TG, D_TP = 2.4, 2.6, 2.8      # s from the glide's start: speed, course and pitch reach their entry values
D_DRAG_L = 0.9            # m: C's quadratic drag length under water: the burst streams past as in C
D_DRAG_IN = 0.06          # s: the drag's build-up as the housing goes in
D_COURSE = (0.15, 4.4)    # s after the crossing over which the course turns level: later than C's, to reach its depth
D_UNDER = 5.6             # s of simulated time after the crossing
D_SWIM = [(0.0, 0.6), (2.6, 0.6), (4.6, 0.9), (D_UNDER, 0.9)]
D_PITCH_UNDER = C_PITCH_UNDER[:-1] + [(D_UNDER, -4.0)]
D_SPRAY = 1200            # droplets: a mist (see Spray)
D_WW_ONSET = 0.006        # s: the white water builds over the 6 ms the housing takes to go in, some ten frames in the slow motion
                          # (C's appears whole in one frame, the shot's one brightness spike)

def quintic(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)

def path_D(probe, az):
    """The glide: the still's frame held, a glide that steepens into the glitter, C's speed ramp round the crossing,
    and under water C's turn up to the sun and level course, braked by a drag that builds as the housing goes in.
    Returns one state a film frame, as path_C does."""
    fwd = Vector((math.cos(az), math.sin(az), 0.0))
    above = PORT * math.sin(math.radians(-C_PITCH_E))
    ge = math.radians(D_GAMMA_E)
    def glide(hold):
        x, z, t = 0.0, 10.0, 0.0
        T, X, Z, V, GA, P = [], [], [], [], [], []
        while True:
            s = t - hold
            v = C_DRIFT + (D_VE - C_DRIFT) * quintic(s / D_TV)
            g = ge * quintic(s / D_TG)
            p = -11.0 + (C_PITCH_E + 11.0) * quintic(s / D_TP)
            T.append(t); X.append(x); Z.append(z); V.append(v); GA.append(g); P.append(p)
            x += v * math.cos(g) * C_DT
            z -= v * math.sin(g) * C_DT
            t += C_DT
            if z < 1.5 and z <= probe.height(round(t * FPS) / FPS, fwd.x * x, fwd.y * x) + above:
                T.append(t); X.append(x); Z.append(z); V.append(v); GA.append(g); P.append(p)
                return [np.array(a) for a in (T, X, Z, V, GA, P)]
            if t > hold + 8.0:
                raise SystemExit("path D: the glide never met the surface")
    extra = c_extra_before(d_ramp)
    hold = C_HOLD
    for it in range(3):
        T = glide(hold)[0]
        F_e = (T[-1] + extra) * FPS
        hold += ((math.ceil(F_e) if it == 0 else round(F_e)) - F_e) / FPS
    T, X, Z, V, GA, P = glide(hold)
    t_e, x_e, z_e, v_e, beta_e = T[-1], X[-1], Z[-1], V[-1], GA[-1]
    if t_e - hold < max(D_TV, D_TG, D_TP):
        print("WARN path D: the port meets the water %.2f s into the glide, before speed, course and pitch have settled"
              % (t_e - hold), flush=True)
    # under water
    n_u = int(round(D_UNDER / C_DT))
    tu = np.arange(1, n_u + 1) * C_DT
    tab = np.linspace(0.0, D_UNDER, 1201)
    tm = tu - 0.5 * C_DT
    vs = np.interp(tm, tab, [pchip(D_SWIM, x) for x in tab])
    v = np.empty(n_u)
    u = v_e - vs[0]
    for i in range(n_u):
        w = quintic(tm[i] / D_DRAG_IN)
        u -= w * u * (u + 2.0 * vs[i]) / D_DRAG_L * C_DT
        v[i] = vs[i] + u
    beta = beta_e * (1.0 - np.array([sstep(D_COURSE[0], D_COURSE[1], x) for x in tm]))
    xu = x_e + np.cumsum(v * np.cos(beta) * C_DT)
    zu = z_e - np.cumsum(v * np.sin(beta) * C_DT)
    pitch_under = np.interp(tu, tab, [pchip(D_PITCH_UNDER, x) for x in tab])
    t_all = np.concatenate([T, t_e + tu])
    x_all = np.concatenate([X, xu])
    z_all = np.concatenate([Z, zu])
    p_all = np.concatenate([P, pitch_under])
    k_all = np.array([d_ramp(t - t_e) for t in t_all])
    F_all = np.concatenate([[0.0], np.cumsum(0.5 * (k_all[1:] + k_all[:-1]) * np.diff(t_all))])
    n = int(math.floor(F_all[-1] * FPS + 1e-9)) + 1
    F = np.arange(n) / FPS
    tf = np.interp(F, F_all, t_all)
    xf, zf, pf = np.interp(tf, t_all, x_all), np.interp(tf, t_all, z_all), np.interp(tf, t_all, p_all)
    states = [{"t": float(tf[f]), "tf": float(F[f]), "loc": Vector((fwd.x * xf[f], fwd.y * xf[f], zf[f])),
               "pitch": float(pf[f]), "roll": 0.0} for f in range(n)]
    f_e = int(np.argmin(np.abs(tf - t_e)))
    for i in range(n):
        a, b = states[max(0, i - 1)], states[min(n - 1, i + 1)]
        states[i]["vel"] = (b["loc"] - a["loc"]) / max(1e-9, b["t"] - a["t"])
        p = math.radians(states[i]["pitch"])
        states[i]["dir"] = Vector((math.cos(az) * math.cos(p), math.sin(az) * math.cos(p), math.sin(p)))
    entry = {"t": t_e, "frame": f_e, "loc": Vector((fwd.x * x_e, fwd.y * x_e, z_e)), "v": v_e, "beta": beta_e,
             "hold": hold, "dir": Vector((fwd.x * math.cos(beta_e), fwd.y * math.cos(beta_e), -math.sin(beta_e)))}
    acc = np.gradient(V, C_DT)
    print("PATH D: hold %.4f s, the glide %.3f s (peak acceleration %.2f m/s2); the port meets the surface at %.4f s simulated,"
          " %.4f s on film, frame %d (simulated %.5f s, %.2f ms off), %.2f m along the heading; %.2f m/s, %.1f deg below level;"
          " %d frames, %.2f s of film for %.2f s simulated; under water the speed is %.2f m/s after 0.1 s, %.2f after 1 s, and"
          " the camera levels %.2f m under the mean surface"
          % (hold, t_e - hold, float(acc.max()), t_e, F_all[np.searchsorted(t_all, t_e)], f_e, tf[f_e], 1e3 * (tf[f_e] - t_e),
             x_e, v_e, math.degrees(beta_e), n, F[-1], t_all[-1], float(np.interp(0.1, tm, v)), float(np.interp(1.0, tm, v)),
             -float(zu[-1])), flush=True)
    return states, entry

# ---------------------------------------------------------------- path E's ripples near the camera
E_CAPILLARY = (0.02, 0.01, 0.005)   # m: lattice spacings of the octaves path E adds below the still's finest (4 cm)
E_NEAR = (1.0, 2.5)                 # m from the camera: the added octaves wholly within the first, not at all beyond
                                    # the second

def capillaries(mat, rip):
    """Path E: carry the ripples' spectrum on below the still's finest octave near the camera. The still draws five
    octaves, 64 cm to 4 cm, each fading out as it shrinks under a pixel, because its finest is about a pixel 20 m
    away; nothing fades in as the camera comes close enough to resolve finer ones. Within half a metre a pixel is a
    fraction of a millimetre, so the sea there was smooth below 4 cm, and one ripple tilted towards the camera made a
    clean curved mirror tens of centimetres across: take 3's disc. Here, within E_NEAR of the camera, the octaves of
    E_CAPILLARY are added (each drifting at the phase speed of a capillary-gravity wave twice its spacing long, and
    each, as the still's, carrying an equal share of slope), and all the octaves are scaled together so that the
    slope they carry is the still's: the coarse octaves' tilt drops by the square root of 5/8, and the finer ones
    break each facet up. Beyond E_NEAR the surface is D's."""
    nt = mat.node_tree
    t = SS.Tree(nt)
    bump = next(nd for nd in nt.nodes if nd.bl_idname == "ShaderNodeBump")
    old = bump.inputs["Height"].links[0].from_socket
    clock = nt.nodes["ripple-clock"].outputs[0]
    wind_dir, q, amp = rip["wind_dir"], rip["q"], rip["amp"]
    geo = t.new("ShaderNodeNewGeometry")
    px, py, _ = t.sep(geo.outputs["Position"])
    ca, sa = math.cos(wind_dir), math.sin(wind_dir)
    along = t.m("ADD", t.m("MULTIPLY", px, ca), t.m("MULTIPLY", py, sa))
    across = t.m("SUBTRACT", t.m("MULTIPLY", py, ca), t.m("MULTIPLY", px, sa))
    sign = 1.0 if rip["drift"][0] >= 0 else -1.0       # the still's octaves drift with the swells (ripple_motion)
    fine = None
    for i, lat in enumerate(E_CAPILLARY):
        k = 2 * math.pi / (2.0 * lat)
        c = sign * math.sqrt(G / k + 0.0728 * k / 1025.0)
        ev = 0.35 * abs(c) / (2.0 * lat)
        x = t.m("SUBTRACT", t.m("MULTIPLY", along, q / lat), t.m("MULTIPLY", clock, c * q / lat))
        w = t.m("ADD", t.m("MULTIPLY", clock, ev), 17.31 * (len(SS.RIPPLE_LATTICE) + i + 1))
        v = t.xyz(x, t.m("MULTIPLY", across, 1.0 / lat), w).outputs[0]
        h = t.m("MULTIPLY", t.m("SUBTRACT", t.noise(v, 1.0, 0.0, 0.5), 0.5), amp * lat)
        fine = h if fine is None else t.m("ADD", fine, h)
    n0 = len(SS.RIPPLE_LATTICE)
    scale = math.sqrt(n0 / (n0 + len(E_CAPILLARY)))
    near = t.maprange(t.new("ShaderNodeCameraData").outputs["View Distance"], E_NEAR[0], E_NEAR[1], 1.0, 0.0)
    # within E_NEAR every one of the still's octaves is at full weight (the footprint is under a millimetre)
    k_old = t.m("SUBTRACT", 1.0, t.m("MULTIPLY", near, 1.0 - scale))
    t.feed(bump.inputs["Height"], t.m("ADD", t.m("MULTIPLY", old, k_old), t.m("MULTIPLY", t.m("MULTIPLY", near, scale), fine)))
    print("PATH E capillaries: octaves %s cm added within %.1f..%.1f m of the camera, every octave there scaled by %.3f"
          % (", ".join("%g" % (100 * l) for l in E_CAPILLARY), *E_NEAR, scale), flush=True)

def make_instancer(name, mat, subdiv=3, rotate=False):
    """An object whose mesh holds points, each drawn as a sphere scaled by its "bscale" attribute (as the bubbles),
    and with `rotate`, turned by its quaternion attribute "brot"."""
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ng = bpy.data.node_groups.new(name + "-gn", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    N = ng.nodes
    gi, go = N.new("NodeGroupInput"), N.new("NodeGroupOutput")
    ico = N.new("GeometryNodeMeshIcoSphere")
    ico.inputs["Radius"].default_value = 1.0
    ico.inputs["Subdivisions"].default_value = subdiv
    sm = N.new("GeometryNodeSetMaterial")
    sm.inputs["Material"].default_value = mat
    ss = N.new("GeometryNodeSetShadeSmooth")
    iop = N.new("GeometryNodeInstanceOnPoints")
    na = N.new("GeometryNodeInputNamedAttribute")
    na.data_type = "FLOAT_VECTOR"
    na.inputs["Name"].default_value = "bscale"
    L = ng.links
    L.new(ico.outputs[0], sm.inputs[0])
    L.new(sm.outputs[0], ss.inputs[0])
    L.new(gi.outputs[0], iop.inputs[0])
    L.new(ss.outputs[0], SS.sock(iop.inputs, "Instance"))
    L.new(SS.sock(na.outputs, "Attribute", "VECTOR"), SS.sock(iop.inputs, "Scale"))
    if rotate:
        nr = N.new("GeometryNodeInputNamedAttribute")
        nr.data_type = "QUATERNION"
        nr.inputs["Name"].default_value = "brot"
        L.new(SS.sock(nr.outputs, "Attribute", "ROTATION"), SS.sock(iop.inputs, "Rotation"))
    L.new(iop.outputs[0], go.inputs[0])
    ob.modifiers.new(name, "NODES").node_group = ng
    no_deform_blur(ob)
    return ob

def set_points(ob, X, scale, rot=None):
    me = bpy.data.meshes.new(ob.name)
    me.vertices.add(len(X))
    if len(X):
        me.vertices.foreach_set("co", X.astype(np.float32).ravel())
        at = me.attributes.new("bscale", "FLOAT_VECTOR", "POINT")
        at.data.foreach_set("vector", scale.astype(np.float32).ravel())
        if rot is not None:
            ar = me.attributes.new("brot", "QUATERNION", "POINT")
            ar.data.foreach_set("value", rot.astype(np.float32).ravel())
    old = ob.data
    ob.data = me
    bpy.data.meshes.remove(old)

class Spray:
    """The spray the entering camera throws: droplets ejected from the line where its housing meets the water,
    from a few milliseconds before the port's centre crosses, outward and upward, fast and low as the jet a blunt
    body throws at impact, and falling ballistically back to the surface. The camera sees them only while its
    port is still partly above the water, which the speed ramp stretches to a few frames.

    With `mist` (path D) the droplets are a fine spray, 0.04 to 0.25 mm, thrown at speeds scaled by the entry's
    speed over C's 14.14 m/s, starting earlier by the same ratio, and each is drawn stretched along its own velocity
    by the distance it travels in the shutter (`at`'s `shutter`, simulated seconds): Cycles blurs them only by the
    camera's motion, since their mesh is rewritten each frame. C's droplets, 0.3 to 2.5 mm a few tenths of a metre
    from the lens, came out as sharp glass beads smeared by the camera's 7 mm of travel in the shutter: a real lens
    focused beyond them would have blurred them into discs, and its slow motion would have streaked their own motion."""
    def __init__(self, entry, probe, count, seed=23, mist=False):
        mat = bpy.data.materials.new("droplet")
        nt = mat.node_tree
        nt.nodes.clear()
        g = nt.nodes.new("ShaderNodeBsdfGlass")
        g.distribution = "GGX"
        g.inputs["Roughness"].default_value = 0.0
        g.inputs["IOR"].default_value = 1.333
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(g.outputs[0], o.inputs["Surface"])
        self.mist = mist
        self.ob = make_instancer("spray", mat, subdiv=2, rotate=mist)
        rng = np.random.default_rng(seed)
        n = int(count)
        self.n = n
        e = entry["dir"]
        c = entry["loc"] + e * (PORT * math.sin(math.radians(-C_PITCH_E)) / max(0.2, -e.z))
        self.c = np.array(c[:])
        vk = entry["v"] / 14.14 if mist else 1.0
        th = rng.uniform(0.0, 2 * math.pi, n)
        rad = rng.uniform(0.07, 0.14, n)
        el = np.radians(rng.uniform(6.0, 55.0, n))
        sp = np.exp(rng.uniform(math.log(3.0), math.log(24.0), n)) * vk
        if mist:
            self.ts = entry["t"] + (-0.004 + 0.03 * rng.random(n) ** 2) / vk
        else:
            self.ts = entry["t"] - 0.004 + 0.03 * rng.random(n) ** 2
        u = np.stack([np.cos(th), np.sin(th), np.zeros(n)], -1)
        self.x0 = self.c[None, :] + u * rad[:, None]
        self.v0 = u * (sp * np.cos(el))[:, None]
        self.v0[:, 2] = sp * np.sin(el)
        rr = (4e-5, 2.5e-4) if mist else (3e-4, 2.5e-3)
        self.r = np.exp(rng.uniform(math.log(rr[0]), math.log(rr[1]), n))
        self.probe, self.entry = probe, entry
        print("SPRAY %d droplets, radius %.2f..%.2f mm, %.1f..%.1f m/s, from %.4f s%s" % (n, 1e3 * self.r.min(), 1e3 * self.r.max(),
              sp.min(), sp.max(), self.ts.min(), ", a mist streaked by its own motion" if mist else ""), flush=True)
    def at(self, t, show, shutter=0.0):
        tau = t - self.ts
        live = (tau >= 0) & show
        tau = np.maximum(tau, 0.0)[:, None]
        X = self.x0 + self.v0 * tau
        X[:, 2] -= 0.5 * G * tau[:, 0] ** 2
        surf = self.probe.height(round(t * FPS) / FPS, self.c[0], self.c[1])
        live &= X[:, 2] > surf + self.r
        r = self.r[live]
        if self.mist:
            V = self.v0[live].copy()
            V[:, 2] -= G * tau[live, 0]
            sp = np.linalg.norm(V, axis=1)
            d = V / np.maximum(sp, 1e-9)[:, None]
            # the quaternion turning +z onto d (w, x, y, z)
            ax = np.stack([-d[:, 1], d[:, 0], np.zeros(len(d))], -1)
            s = np.linalg.norm(ax, axis=1)
            ang = np.arctan2(s, d[:, 2])
            ax = ax / np.maximum(s, 1e-12)[:, None]
            q = np.concatenate([np.cos(ang / 2)[:, None], ax * np.sin(ang / 2)[:, None]], -1)
            half = r + 0.5 * sp * shutter
            set_points(self.ob, X[live], np.stack([r, r, half], -1), q)
        else:
            set_points(self.ob, X[live], np.stack([r, r, r], -1))
        self.ob.hide_render = not bool(live.any())
        return int(live.sum())

class WhiteWater:
    """White water at the entry: a cloud of air entrained round the entering camera, as fine bubbles that scatter
    light without absorbing it. Scattering by bubbles a few tenths of a millimetre across goes as 1.5 times the
    air's volume fraction over the radius, so a cloud of a per cent of air is opaque within a few centimetres. Its
    shape is not simulated: a sleeve round the camera's path from the surface down to a little ahead of the port,
    flaring at the surface, widening with time, its density broken into pockets by noise; the camera leaves it
    through its lower end at WW_DEPTH. Its density is set by eye. The render follows one volume bounce, so light
    cannot diffuse into a cloud this dense and its inside renders black, which white water is not: the many
    scatterings it does not follow are put back as emission, each unit of scattering emitting the radiance a
    diffusing white cloud holds, WW_DIFFUSE times the downwelling irradiance in the water over pi, dimmed with
    depth by the water's absorption and by WW_DIM a metre (the light lost out of the cloud's sides). The
    downwelling irradiance is the refracted sun's on a level surface plus the sky's: the sky probe's radiance taken
    to vary as the cosine of the zenith angle from its level at the horizon to its level at the zenith,
    integrated over the sky, times SKY_T for the surface's transmittance of diffuse light. In this sky it is
    several times the sun's."""
    WW_SIGMA, WW_DEPTH, WW_G, WW_DECAY, WW_DIFFUSE, WW_DIM = 12.0, 0.5, 0.3, 0.8, 0.8, 1.5
    WW_GRAIN = 26.0            # 1/m: the noise's scale, pockets a few centimetres across
    SKY_T = 0.85
    def __init__(self, entry, probe, strength, wsun=None, sky=None, onset=0.0, foam=None):
        self.foam = foam        # (a, b, c, d, gain, light): the peak at contact, see at()
        self.onset = onset      # s: path D's white water builds over this as the housing goes in (C's starts whole)
        E = [wsun.strength * c * wsun.d.z for c in wsun.ob.data.color] if wsun is not None else [0.0, 0.0, 0.0]
        if sky is not None and sky.get("zenith") is not None and SS.HORIZON is not None:
            Z, H = sky["zenith"], [0.5 * (a + b) for a, b in zip(SS.HORIZON["toward"], SS.HORIZON["away"])]
            Es = [math.pi * (h / 3.0 + 2.0 * z / 3.0) * self.SKY_T for z, h in zip(Z, H)]
            print("WHITE WATER downwelling in the water: sun %.2f %.2f %.2f, sky %.2f %.2f %.2f" % (*E, *Es), flush=True)
            E = [a + b for a, b in zip(E, Es)]
        self.E = E
        e = entry["dir"]
        self.e = e
        self.o = entry["loc"] + e * (PORT * math.sin(math.radians(-C_PITCH_E)) / max(0.2, -e.z))
        surf = probe.height(round(entry["t"] * FPS) / FPS, self.o.x, self.o.y)
        self.o.z = surf
        self.entry, self.probe = entry, probe
        me = bpy.data.meshes.new("white-water")
        hx, top, depth = 0.95, -0.005, 1.1
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)               # outward normals: a volume's boundary
        for vt in bm.verts:
            vt.co.x *= 2 * hx
            vt.co.y *= 2 * hx
            vt.co.z = top - (0.5 - vt.co.z) * (depth + top)
        bm.to_mesh(me)
        bm.free()
        self.ob = bpy.data.objects.new("white-water", me)
        bpy.context.scene.collection.objects.link(self.ob)
        self.ob.location = self.o
        self.ob.data.materials.append(self.material(strength))
        no_deform_blur(self.ob)
        self.ob.hide_render = True
        print("WHITE WATER at %.2f %.2f %.3f, axis %.2f %.2f %.2f, %.0f /m at full density, left through its end %.2f m down"
              % (self.o.x, self.o.y, self.o.z, e.x, e.y, e.z, self.WW_SIGMA * strength, self.WW_DEPTH), flush=True)
    def material(self, strength):
        mat = bpy.data.materials.new("white-water")
        t = SS.Tree(mat.node_tree)
        t.nt.nodes.clear()
        P = t.new("ShaderNodeTexCoord").outputs["Object"]
        self.clock = t.new("ShaderNodeValue")
        self.clock.name = self.clock.label = "ww-clock"
        self.front = t.new("ShaderNodeValue")
        self.front.name = self.front.label = "ww-front"
        tau = self.clock.outputs[0]
        e = tuple(self.e)
        dn = t.new("ShaderNodeVectorMath", operation="DOT_PRODUCT")
        t.feed(dn.inputs[0], P)
        dn.inputs[1].default_value = e
        s = dn.outputs["Value"]
        radial = t.vm("SUBTRACT", P, t.vm("SCALE", t.xyz(*e).outputs[0], scale=s))
        ln = t.new("ShaderNodeVectorMath", operation="LENGTH")
        t.feed(ln.inputs[0], radial)
        r = ln.outputs["Value"]
        R = t.m("ADD", t.m("ADD", 0.16, t.m("MULTIPLY", tau, 0.8)), t.m("MULTIPLY", t.m("MAXIMUM", t.m("SUBTRACT", 0.35, s), 0.0), 0.6))
        fr = t.maprange(t.m("DIVIDE", r, R), 0.55, 1.0, 1.0, 0.0)
        fs = t.m("MULTIPLY", t.maprange(s, -0.03, 0.04, 0.0, 1.0),
                 t.maprange(s, t.m("SUBTRACT", self.front.outputs[0], 0.12), self.front.outputs[0], 1.0, 0.0))
        nz = t.new("ShaderNodeTexNoise")
        nz.noise_dimensions = "4D"
        t.feed(nz.inputs["Vector"], P)
        nz.inputs["Scale"].default_value = self.WW_GRAIN
        nz.inputs["Detail"].default_value = 2.0
        nz.inputs["Roughness"].default_value = 0.5
        t.feed(nz.inputs["W"], t.m("MULTIPLY", tau, 6.0))
        pockets = t.maprange(nz.outputs["Fac"], 0.3, 0.7, 0.0, 1.0)
        decay = t.m("EXPONENT", t.m("DIVIDE", tau, -self.WW_DECAY))
        self.build = t.new("ShaderNodeValue")
        self.build.name = self.build.label = "ww-onset"
        self.build.outputs[0].default_value = 1.0
        decay = t.m("MULTIPLY", decay, self.build.outputs[0])
        dens = t.m("MULTIPLY", t.m("MULTIPLY", t.m("MULTIPLY", fr, fs), t.m("ADD", 0.2, t.m("MULTIPLY", pockets, 0.8))),
                   t.m("MULTIPLY", decay, self.WW_SIGMA * strength))
        # the foam peak: weight w of the trapezoid at the frame; the multipliers exist only when a window is set
        self.foam_w = t.new("ShaderNodeValue")
        self.foam_w.name = self.foam_w.label = "ww-foam"
        self.foam_w.outputs[0].default_value = 0.0
        lboost = None
        if self.foam is not None:
            w = self.foam_w.outputs[0]
            if self.foam[4] != 1.0:
                dens = t.m("MULTIPLY", dens, t.m("ADD", 1.0, t.m("MULTIPLY", w, self.foam[4] - 1.0)))
            if self.foam[5] != 1.0:
                lboost = t.m("ADD", 1.0, t.m("MULTIPLY", w, self.foam[5] - 1.0))
        vc = t.new("ShaderNodeVolumeCoefficients")
        vc.inputs["Absorption Coefficients"].default_value = (0.0, 0.0, 0.0)
        t.feed(vc.inputs["Scatter Coefficients"], t.xyz(dens, dens, dens).outputs[0])
        vc.inputs["Anisotropy"].default_value = self.WW_G
        # the diffused light, standing for the scatterings the render does not follow
        self.light = t.new("ShaderNodeValue")
        self.light.name = self.light.label = "ww-light"
        _, _, pz = t.sep(P)
        depth = t.m("MAXIMUM", t.m("MULTIPLY", pz, -1.0), 0.0)
        em = []
        for c in range(3):
            L = t.m("EXPONENT", t.m("MULTIPLY", depth, -(SS.WATER_ABSORB[c] + self.WW_DIM)))
            L = t.m("MULTIPLY", L, self.WW_DIFFUSE * self.E[c] / math.pi)
            e_c = t.m("MULTIPLY", t.m("MULTIPLY", L, dens), self.light.outputs[0])
            if lboost is not None:
                e_c = t.m("MULTIPLY", e_c, lboost)
            em.append(e_c)
        t.feed(vc.inputs["Emission Coefficients"], t.xyz(*em).outputs[0])
        out = t.new("ShaderNodeOutputMaterial")
        t.feed(out.inputs["Volume"], vc.outputs[0])
        return mat
    def at(self, t, cam, light=1.0):
        tau = t - self.entry["t"]
        if tau < 0.0 or tau > 2.5:
            self.ob.hide_render = True
            return 0.0
        self.ob.hide_render = False
        self.light.outputs[0].default_value = light
        if self.foam is not None:
            a, b, c, d, G, L = self.foam
            w = sstep(a, b, t) * (1.0 - sstep(c, d, t))
            self.foam_w.outputs[0].default_value = w
            if a <= t <= d:
                print("FOAM t %.4f weight %.4f density x%.3f light x%.3f" % (t, w, 1.0 + (G - 1.0) * w, 1.0 + (L - 1.0) * w), flush=True)
        if self.onset > 0.0:
            self.build.outputs[0].default_value = quintic(tau / self.onset)
        s_cam = (cam - self.o).dot(self.e)
        front = min(self.WW_DEPTH, s_cam + 0.06 + 0.12 * math.exp(-tau / 0.02))
        self.clock.outputs[0].default_value = tau
        self.front.outputs[0].default_value = front
        return front

class Fizz:
    """The white water's resolved bubbles: air torn from the cavity's walls and the splash as the camera goes in,
    in the size spectrum Deane and Stokes (2002) measured under breaking waves (number per radius falling as r to
    the -3/2 below the Hinze scale, about a millimetre, and as r to the -10/3 above it), 0.1 to 4 mm. They are
    released round the camera's path as its port passes each depth, from the surface down to FZ_DEPTH: most round
    the camera and just behind its port, some pushed ahead of it; each starts with a share of the camera's
    velocity, an outward push and a turbulent swirl, and relaxes within FZ_TAU to its rise speed. The camera
    soon outruns them. Their material is the bubbles' own."""
    FZ_DEPTH, FZ_TAU, FZ_SWIRL = 0.5, 0.035, 0.7
    def __init__(self, entry, states, probe, count, seed=31):
        self.mat = bubble_material()
        self.ob = make_instancer("fizz", self.mat, subdiv=2)
        rng = np.random.default_rng(seed)
        n = int(count)
        self.n = n
        e = entry["dir"]
        self.e = np.array(e[:])
        o = entry["loc"] + e * (PORT * math.sin(math.radians(-C_PITCH_E)) / max(0.2, -e.z))
        o.z = probe.height(round(entry["t"] * FPS) / FPS, o.x, o.y)
        self.o = np.array(o[:])
        # the port's progress along the axis, and the camera's speed, against time
        ts = np.array([s["t"] for s in states])
        prog = np.array([(np.array(s["loc"][:]) - self.o) @ self.e + PORT for s in states])
        speed = np.array([s["vel"].length for s in states])
        after = ts >= entry["t"] - 0.01
        ts, prog, speed = ts[after], prog[after], speed[after]
        # sizes: a piecewise power law, inverted
        r0, rh, r1 = 1e-4, 1e-3, 4e-3
        a1, a2 = 1.5, 10.0 / 3.0
        m1 = (rh ** (1 - a1) - r0 ** (1 - a1)) / (1 - a1)
        m2 = rh ** (a2 - a1) * (r1 ** (1 - a2) - rh ** (1 - a2)) / (1 - a2)
        u = rng.random(n)
        lo = u < m1 / (m1 + m2)
        q = u * (m1 + m2)
        r = np.empty(n)
        r[lo] = (r0 ** (1 - a1) + (1 - a1) * q[lo]) ** (1 / (1 - a1))
        r[~lo] = (rh ** (1 - a2) + (1 - a2) * (q[~lo] - m1) / rh ** (a2 - a1)) ** (1 / (1 - a2))
        self.r = r
        # where and when
        s = -0.01 + self.FZ_DEPTH * rng.random(n) ** 0.7
        # A third are the air cushion a flat port traps as it meets the water, broken into bubbles on its face and
        # carried ahead of it until they are swept off round its edge.
        ahead = rng.random(n) < 0.35
        self.ts = np.interp(np.maximum(s, prog.min()), prog, ts) - 0.004 * rng.random(n)
        lead = np.where(ahead, rng.uniform(0.01, 0.12, n), rng.uniform(-0.06, 0.0, n))
        rho = np.where(ahead, 0.12 * np.sqrt(rng.random(n)), 0.06 + 0.24 * np.sqrt(rng.random(n)))
        b1 = np.cross(self.e, [1.0, 0.0, 0.0])
        b1 /= np.linalg.norm(b1)
        b2 = np.cross(self.e, b1)
        th = rng.uniform(0.0, 2 * math.pi, n)
        radial = np.cos(th)[:, None] * b1 + np.sin(th)[:, None] * b2
        self.x0 = self.o + (s + lead)[:, None] * self.e + rho[:, None] * radial
        vcam = np.interp(self.ts, ts, speed)
        share = np.where(ahead, rng.uniform(0.85, 1.0, n), rng.uniform(0.2, 0.7, n))
        self.v0 = (share * vcam)[:, None] * self.e + rng.uniform(0.5, 1.5, n)[:, None] * radial \
                  + rng.normal(0.0, self.FZ_SWIRL, (n, 3))
        self.rise = np.minimum(0.25, 0.136 * (r / 2.5e-4) ** 2)
        self.probe, self.entry = probe, entry
        print("FIZZ %d bubbles, radius %.2f..%.1f mm (median %.2f mm, %d over 1 mm), released %.4f..%.4f s, %d ahead"
              % (n, 1e3 * r.min(), 1e3 * r.max(), 1e3 * float(np.median(r)), int((r > 1e-3).sum()), self.ts.min(),
                 self.ts.max(), int(ahead.sum())), flush=True)
    def positions(self, t):
        tau = t - self.ts
        live = tau >= 0
        tau = np.maximum(tau, 0.0)[:, None]
        w = np.zeros((self.n, 3))
        w[:, 2] = self.rise
        X = self.x0 + w * tau + (self.v0 - w) * self.FZ_TAU * (1 - np.exp(-tau / self.FZ_TAU))
        surf = self.probe.height(round(t * FPS) / FPS, self.o[0], self.o[1])
        live &= X[:, 2] < surf - self.r
        return X, live
    def at(self, t):
        X, live = self.positions(t)
        r = self.r[live]
        set_points(self.ob, X[live], np.stack([r, r, r], -1))
        self.ob.hide_render = not bool(live.any())
        return int(live.sum())

class Shafts:
    """The sun's light under the moving surface, focused by it. Cycles's caustics through a specular surface lit by
    a sun the size of the real one do not converge, so the focusing is traced here: per frame, the surface's
    ripples (the same node graph and clock as the material's bump, every octave drawn) and its swells (the same
    Ocean modifier) are sampled on a grid SH_H apart over the water in front of the camera; the sun is refracted
    through each grid point's slope by Snell's law, weighted by Fresnel's transmittance and the facet's
    foreshortening, and followed down to SH_DEPTHS, where the rays are binned and smoothed by one bin. A bin's count
    over a flat surface's is the irradiance there over a flat surface's: 1 everywhere under a flat sea, more in the
    net where rays converge, less between. Each depth's map is kept by where the flat sea's refracted sun ray
    through that point meets the surface, so that the water sun, whose shadow rays already pass the surface only
    along that ray, is coloured by the map as they cross it: the depth is the shadow ray's length times the ray's
    cosine, interpolated between the depths, faded to 1 at the surface (where nothing has focused yet) and below
    the deepest. Approximations: geometric optics; the sun a point but for the one-bin blur (its 0.4-degree disc
    blurs less than a bin above 3 m); each ray's light carried to the camera as if it travelled along the mean
    refracted direction (rays deviate from it by a few degrees); the swell's slope taken from its height field
    without its horizontal shift."""
    SH_H = 0.02
    SH_DEPTHS = tuple(0.25 * 1.6 ** k for k in range(8))      # 0.25 .. 6.7 m
    SH_FAR = 15.0                                           # m: the net faded to 1 here
    SH_AHEAD, SH_SIDE, SH_EDGE = (-3.0, 15.0), 7.0, 0.8      # m: the region round the camera, and its faded border
    SH_SWELL_H = 0.25
    # Seawater's particles scatter mostly within a few degrees of forward (Petzold's measurements; an equivalent
    # Henyey-Greenstein g of about 0.92), and that sharp forward peak is what carries shafts to an eye looking
    # towards the sun. The still's water, one g of 0.8 calibrated for its colour, has none; this adds a particulate
    # share, SH_B a metre with g SH_G, as its own medium from SH_TOP below the mean surface (under the troughs).
    SH_B, SH_G, SH_TOP = 0.05, 0.92, -0.6
    def __init__(self, sea_mat, wsun, ctx, debug=None, medium=None):
        az = ctx["az"]
        self.debug = debug
        self.ea = np.array([math.cos(az), math.sin(az)])
        self.eb = np.array([-math.sin(az), math.cos(az)])
        h = self.SH_H
        self.na = int(round((self.SH_AHEAD[1] - self.SH_AHEAD[0]) / h)) + 1
        self.nb = int(round(2 * self.SH_SIDE / h)) + 1
        self.k = ctx["ocean"]["k"]
        d = np.array(wsun.d[:])
        s = np.array(ctx["sun"]["sun"][:])
        s /= np.linalg.norm(s)
        self.d3 = np.array([d[:2] @ self.ea, d[:2] @ self.eb, d[2]])
        self.s3 = np.array([s[:2] @ self.ea, s[:2] @ self.eb, s[2]])
        self.T_flat = self.fresnel_t(np.array([s[2]]))[0]
        rip = ctx["rip"]
        # the ripples' heights, by the material's own graph, on a grid of points moved each frame
        ng = bpy.data.node_groups.new("shaft-ripples", "GeometryNodeTree")
        ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
        ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
        t = SS.Tree(ng)
        gi, go = t.new("NodeGroupInput"), t.new("NodeGroupOutput")
        self.clock = t.new("ShaderNodeValue")
        hgt = SS.ripple_height(t, t.new("GeometryNodeInputPosition").outputs[0], rip["wind_dir"], rip["q"], rip["amp"],
                               None, self.clock.outputs[0], rip["drift"], rip["evolve"])
        sp = t.new("GeometryNodeSetPosition")
        t.feed(sp.inputs["Geometry"], gi.outputs[0])
        t.feed(sp.inputs["Offset"], t.xyz(0.0, 0.0, hgt).outputs[0])
        t.feed(go.inputs[0], sp.outputs[0])
        me = bpy.data.meshes.new("shaft-grid")
        me.vertices.add(self.na * self.nb)
        self.grid = bpy.data.objects.new("shaft-grid", me)
        bpy.context.scene.collection.objects.link(self.grid)
        self.grid.modifiers.new("ripples", "NODES").node_group = ng
        self.grid.hide_render = True
        # the swells, on a coarser grid with the Ocean modifier
        hs = self.SH_SWELL_H
        self.nas = int(math.ceil((self.SH_AHEAD[1] - self.SH_AHEAD[0]) / hs)) + 4
        self.nbs = int(math.ceil(2 * self.SH_SIDE / hs)) + 4
        me2 = bpy.data.meshes.new("shaft-swell")
        me2.vertices.add(self.nas * self.nbs)
        self.swell = bpy.data.objects.new("shaft-swell", me2)
        bpy.context.scene.collection.objects.link(self.swell)
        self.swell.hide_render = True
        self.swell_mod = SS.ocean_modifier(self.swell, ctx["wind_dir"])
        # the maps: eight depths in the channels of two float images
        self.img = []
        for i in range(2):
            im = bpy.data.images.new("shaft-net-%d" % i, self.na, self.nb, alpha=True, float_buffer=True)
            im.colorspace_settings.name = "Non-Color"
            im.alpha_mode = "CHANNEL_PACKED"
            im.pixels.foreach_set(np.ones(self.na * self.nb * 4, np.float32))
            self.img.append(im)
        self.material(sea_mat, wsun)
        self.last = None
        # the particulate medium
        self.b = self.SH_B if medium is None else medium
        self.box = None
        if self.b > 0:
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.0)
            for vt in bm.verts:
                vt.co.x *= 80.0
                vt.co.y *= 80.0
                vt.co.z = self.SH_TOP - (0.5 - vt.co.z) * 20.0
            me3 = bpy.data.meshes.new("particles")
            bm.to_mesh(me3)
            bm.free()
            self.box = bpy.data.objects.new("particles", me3)
            bpy.context.scene.collection.objects.link(self.box)
            pm = bpy.data.materials.new("particles")
            pn = pm.node_tree
            pn.nodes.clear()
            vc = pn.nodes.new("ShaderNodeVolumeCoefficients")
            vc.inputs["Absorption Coefficients"].default_value = (0.0, 0.0, 0.0)
            vc.inputs["Scatter Coefficients"].default_value = (self.b, self.b, self.b)
            vc.inputs["Anisotropy"].default_value = self.SH_G
            po = pn.nodes.new("ShaderNodeOutputMaterial")
            pn.links.new(vc.outputs[0], po.inputs["Volume"])
            try:
                pm.cycles.homogeneous_volume = True
            except Exception:
                pass
            me3.materials.append(pm)
            no_deform_blur(self.box)
            self.box.hide_render = True
            print("SHAFTS particulate medium %.3f /m, g %.2f, from %.1f m down" % (self.b, self.SH_G, self.SH_TOP), flush=True)
        print("SHAFTS a %d x %d grid %.0f cm apart (%.0f x %.0f m), depths %s m; refracted sun %.1f deg up; flat"
              " transmittance %.3f" % (self.na, self.nb, 100 * h, self.SH_AHEAD[1] - self.SH_AHEAD[0], 2 * self.SH_SIDE,
                                       ", ".join("%.2f" % z for z in self.SH_DEPTHS), math.degrees(math.asin(d[2])),
                                       self.T_flat), flush=True)
    @staticmethod
    def fresnel_t(ci):
        ct = np.sqrt(np.maximum(0.0, 1.0 - (1.0 - ci ** 2) / N_WATER ** 2))
        rs = ((ci - N_WATER * ct) / (ci + N_WATER * ct)) ** 2
        rp = ((N_WATER * ci - ct) / (N_WATER * ci + ct)) ** 2
        return 1.0 - 0.5 * (rs + rp)
    def material(self, sea_mat, wsun):
        nt = sea_mat.node_tree
        t = SS.Tree(nt)
        tr = next(nd for nd in nt.nodes if nd.bl_idname == "ShaderNodeBsdfTransparent")
        geo = t.new("ShaderNodeNewGeometry")
        lp = t.new("ShaderNodeLightPath")
        z = t.m("MULTIPLY", lp.outputs["Ray Length"], float(self.d3[2]))
        px, py, _ = t.sep(geo.outputs["Position"])
        a = t.m("ADD", t.m("MULTIPLY", px, float(self.ea[0])), t.m("MULTIPLY", py, float(self.ea[1])))
        b = t.m("ADD", t.m("MULTIPLY", px, float(self.eb[0])), t.m("MULTIPLY", py, float(self.eb[1])))
        self.a0, self.b0, self.weight = t.new("ShaderNodeValue"), t.new("ShaderNodeValue"), t.new("ShaderNodeValue")
        for nd, nm in ((self.a0, "shaft-a0"), (self.b0, "shaft-b0"), (self.weight, "shaft-weight")):
            nd.name = nd.label = nm
        u = t.m("MULTIPLY", t.m("ADD", t.m("DIVIDE", t.m("SUBTRACT", a, self.a0.outputs[0]), self.SH_H), 0.5), 1.0 / self.na)
        v = t.m("MULTIPLY", t.m("ADD", t.m("DIVIDE", t.m("SUBTRACT", b, self.b0.outputs[0]), self.SH_H), 0.5), 1.0 / self.nb)
        uv = t.xyz(u, v, 0.0).outputs[0]
        C = []
        for im in self.img:
            tex = t.new("ShaderNodeTexImage")
            tex.image = im
            tex.interpolation = "Linear"
            tex.extension = "EXTEND"
            t.feed(tex.inputs["Vector"], uv)
            sep = t.new("ShaderNodeSeparateColor")
            t.feed(sep.inputs[0], tex.outputs["Color"])
            C += [sep.outputs[0], sep.outputs[1], sep.outputs[2], tex.outputs["Alpha"]]
        sidx = t.m("MINIMUM", t.m("MAXIMUM", t.m("DIVIDE", t.m("LOGARITHM", t.m("DIVIDE", t.m("MAXIMUM", z, 1e-3),
                                                                                    self.SH_DEPTHS[0]), math.e),
                                                     math.log(1.6)), 0.0), float(len(self.SH_DEPTHS) - 1))
        raw = None
        for k, ck in enumerate(C):
            wk = t.m("MAXIMUM", t.m("SUBTRACT", 1.0, t.m("ABSOLUTE", t.m("SUBTRACT", sidx, float(k)))), 0.0)
            term = t.m("MULTIPLY", wk, ck)
            raw = term if raw is None else t.m("ADD", raw, term)
        near = t.m("DIVIDE", z, self.SH_DEPTHS[0], clamp=True)
        far = t.m("SUBTRACT", 1.0, t.m("DIVIDE", t.m("SUBTRACT", z, self.SH_DEPTHS[-1]), self.SH_FAR - self.SH_DEPTHS[-1],
                                       clamp=True))
        dev = t.m("MULTIPLY", t.m("MULTIPLY", t.m("SUBTRACT", raw, 1.0), near), far)
        col = t.m("ADD", 1.0, t.m("MULTIPLY", t.m("MULTIPLY", dev, wsun.aimed), self.weight.outputs[0]))
        t.feed(tr.inputs["Color"], t.xyz(col, col, col).outputs[0])
    def region(self, cam):
        h = self.SH_H
        ca, cb = float(np.array(cam[:2]) @ self.ea), float(np.array(cam[:2]) @ self.eb)
        a0 = math.floor((ca + self.SH_AHEAD[0]) / h) * h
        b0 = math.floor((cb - self.SH_SIDE) / h) * h
        return a0, b0
    def update(self, t, cam, w, medium=True):
        self.weight.outputs[0].default_value = w
        if self.box is not None:
            self.box.hide_render = not (w > 0.0 and medium)
        if w <= 0.0:
            return 0.0
        key = (round(t, 6), self.region(cam))
        if key == self.last:
            return 0.0
        t0 = time.time()
        a0, b0 = key[1]
        h, na, nb = self.SH_H, self.na, self.nb
        self.a0.outputs[0].default_value = a0
        self.b0.outputs[0].default_value = b0
        if self.debug == "depth":
            C = [np.full((nb, na), 0.4 + 0.2 * k) for k in range(len(self.SH_DEPTHS))]
        else:
            A = a0 + h * np.arange(na)
            B = b0 + h * np.arange(nb)
            AA, BB = np.meshgrid(A, B)
            XY = AA[..., None] * self.ea + BB[..., None] * self.eb
            P = np.concatenate([XY, np.zeros((nb, na, 1))], -1).reshape(-1, 3)
            me = self.grid.data
            me.vertices.foreach_set("co", P.astype(np.float32).ravel())
            me.update()
            self.clock.outputs[0].default_value = t
            hr = SS.ocean_offsets(self.grid, P)[:, 2].reshape(nb, na)
            # the swells on the coarse grid, their slope interpolated to the fine one
            hs = self.SH_SWELL_H
            a0s, b0s = math.floor(a0 / hs) * hs - hs, math.floor(b0 / hs) * hs - hs
            As, Bs = a0s + hs * np.arange(self.nas), b0s + hs * np.arange(self.nbs)
            AAs, BBs = np.meshgrid(As, Bs)
            XYs = AAs[..., None] * self.ea + BBs[..., None] * self.eb
            Ps = np.concatenate([XYs, np.zeros((self.nbs, self.nas, 1))], -1).reshape(-1, 3)
            self.swell.data.vertices.foreach_set("co", Ps.astype(np.float32).ravel())
            self.swell.data.update()
            self.swell_mod.time = SS.OCEAN_TIME + t
            eta = SS.ocean_offsets(self.swell, Ps)[:, 2].reshape(self.nbs, self.nas) * self.k
            gb_s, ga_s = np.gradient(eta, hs)
            fa = (A - a0s) / hs
            fb = (B - b0s) / hs
            def interp(Fd):
                i0 = np.clip(np.floor(fa).astype(int), 0, self.nas - 2)
                j0 = np.clip(np.floor(fb).astype(int), 0, self.nbs - 2)
                xa, xb = (fa - i0)[None, :], (fb - j0)[:, None]
                return ((1 - xa) * (1 - xb) * Fd[j0][:, i0] + xa * (1 - xb) * Fd[j0][:, i0 + 1]
                        + (1 - xa) * xb * Fd[j0 + 1][:, i0] + xa * xb * Fd[j0 + 1][:, i0 + 1])
            gb_r, ga_r = np.gradient(hr, h)
            ga, gb = ga_r + interp(ga_s), gb_r + interp(gb_s)
            nrm = np.sqrt(ga ** 2 + gb ** 2 + 1.0)
            n = np.stack([-ga / nrm, -gb / nrm, 1.0 / nrm], -1)
            s = self.s3
            ci = np.clip(n @ s, 1e-3, 1.0)
            eta_r = 1.0 / N_WATER
            k = np.sqrt(np.maximum(0.0, 1.0 - eta_r ** 2 * (1.0 - ci ** 2)))
            r = -eta_r * s[None, None, :] + (eta_r * ci - k)[..., None] * n
            wgt = (ci / n[..., 2]) / s[2] * self.fresnel_t(ci) / self.T_flat
            dz = self.d3
            Da = r[..., 0] / -r[..., 2] + dz[0] / dz[2]
            Db = r[..., 1] / -r[..., 2] + dz[1] / dz[2]
            I, J = np.meshgrid(np.arange(na), np.arange(nb))
            wf = wgt.ravel()
            C = []
            for z in self.SH_DEPTHS:
                fi, fj = (I + z * Da / h).ravel(), (J + z * Db / h).ravel()
                i0, j0 = np.floor(fi).astype(np.int64), np.floor(fj).astype(np.int64)
                xa, xb = fi - i0, fj - j0
                hist = np.zeros(na * nb)
                for di, dj, ww in ((0, 0, (1 - xa) * (1 - xb)), (1, 0, xa * (1 - xb)), (0, 1, (1 - xa) * xb), (1, 1, xa * xb)):
                    ii, jj = i0 + di, j0 + dj
                    ok = (ii >= 0) & (ii < na) & (jj >= 0) & (jj < nb)
                    hist += np.bincount((jj[ok] * na + ii[ok]), weights=(wf * ww)[ok], minlength=na * nb)
                c = hist.reshape(nb, na)
                kern = np.array([1.0, 4.0, 6.0, 4.0, 1.0]) / 16.0
                c = np.pad(c, 2, mode="edge")
                c = sum(kern[i] * c[:, i:i + na] for i in range(5))
                c = sum(kern[i] * c[i:i + nb, :] for i in range(5))
                C.append(c)
        ea_f = SS.smoothstep(0.0, self.SH_EDGE, np.minimum(np.arange(na), na - 1 - np.arange(na)) * h)
        eb_f = SS.smoothstep(0.0, self.SH_EDGE, np.minimum(np.arange(nb), nb - 1 - np.arange(nb)) * h)
        fade = eb_f[:, None] * ea_f[None, :]
        for i, im in enumerate(self.img):
            px = np.stack([1.0 + (C[4 * i + j] - 1.0) * fade for j in range(4)], -1).astype(np.float32)
            im.pixels.foreach_set(px.ravel())
            im.update()
        self.last = key
        stats = C[4]
        print("SHAFTS t %.4f region a %.2f b %.2f: net at %.1f m mean %.3f, 1st-99th percentile %.2f..%.2f, max %.1f; %.2f s"
              % (t, a0, b0, self.SH_DEPTHS[4], float(stats.mean()), float(np.percentile(stats, 1)),
                 float(np.percentile(stats, 99)), float(stats.max()), time.time() - t0), flush=True)
        return time.time() - t0

def gate_camera_rays(mat):
    """The bubbles-only layer: the water's volume is clear to camera rays and as before to every other ray."""
    nt = mat.node_tree
    t = SS.Tree(nt)
    vc = next(nd for nd in nt.nodes if nd.bl_idname == "ShaderNodeVolumeCoefficients")
    lp = t.new("ShaderNodeLightPath")
    keep = t.m("SUBTRACT", 1.0, lp.outputs["Is Camera Ray"])
    for name, val in (("Absorption Coefficients", SS.WATER_ABSORB), ("Scatter Coefficients", SS.WATER_SCATTER)):
        t.feed(vc.inputs[name], t.vm("SCALE", t.xyz(*val).outputs[0], scale=keep))

# ---------------------------------------------------------------- the matted bubbles layer (--layer matte)
# Triangulation matting (Smith and Blinn, 1996): each frame is rendered twice, over two known backgrounds, and each
# pixel's colour and opacity are solved from the pair. The background here is not a card behind the bubbles but
# the water itself as the theatre will supply it: every "straight" ray (one that has met only refractions since
# the camera: a camera ray, or one gone straight through bubbles) sees, in place of Blender's water, a medium with
# the water's extinction (absorption plus scattering) that emits the background radiance B times that extinction,
# so that its own radiance tends to B and a ray that meets nothing reads exactly B; the surface emits B to
# such rays (and the sky is B to them too). A bubble at distance d is then dimmed by the water's transmittance
# T(d) and veiled by (1 - T(d)) B, the water in front of it, which the solve hands back to the background: over the
# theatre's footage a far bubble fades into the theatre's water as it fades into Blender's. Every other ray (a
# reflection off a bubble, a scattering in the white water, a shadow ray) sees Blender's water and light, as in the
# full render: that light is the layer's colour. With B1 = 0 the colour is the first render itself, and the
# opacity is 1 - (C2 - C1).B2 / B2.B2.
MATTE_B2_DISPLAY = (0.008, 0.25, 0.40)    # display-linear: the second background, SHALLOW's hue (sRGB 2, 71, 90) x 4

def straight_ray(t):
    """1 on a non-shadow ray that has met only transmissions since the camera, else 0."""
    lp = t.new("ShaderNodeLightPath")
    turned = t.m("SUBTRACT", lp.outputs["Ray Depth"], lp.outputs["Transmission Depth"])
    return t.m("MULTIPLY", t.m("LESS_THAN", turned, 0.5), t.m("SUBTRACT", 1.0, lp.outputs["Is Shadow Ray"]))

def setup_matte(sc, mat, world, sea_ob):
    """Rewire the sea's volume and surface and the world for the matte pass; returns the background's three Value
    nodes (scene-linear radiance B)."""
    nt = mat.node_tree
    t = SS.Tree(nt)
    vc = next(nd for nd in nt.nodes if nd.bl_idname == "ShaderNodeVolumeCoefficients")
    st = straight_ray(t)
    keep = t.m("SUBTRACT", 1.0, st)
    ext = [a + s for a, s in zip(SS.WATER_ABSORB, SS.WATER_SCATTER)]
    B = []
    for c in "rgb":
        v = t.new("ShaderNodeValue")
        v.name = v.label = "matte-B-" + c
        B.append(v)
    absorb = [t.m("ADD", t.m("MULTIPLY", keep, a), t.m("MULTIPLY", st, e)) for a, e in zip(SS.WATER_ABSORB, ext)]
    t.feed(vc.inputs["Absorption Coefficients"], t.xyz(*absorb).outputs[0])
    t.feed(vc.inputs["Scatter Coefficients"], t.vm("SCALE", t.xyz(*SS.WATER_SCATTER).outputs[0], scale=keep))
    em = [t.m("MULTIPLY", t.m("MULTIPLY", st, e), b.outputs[0]) for e, b in zip(ext, B)]
    t.feed(vc.inputs["Emission Coefficients"], t.xyz(*em).outputs[0])
    out = next(nd for nd in nt.nodes if nd.bl_idname == "ShaderNodeOutputMaterial")
    src = out.inputs["Surface"].links[0].from_socket
    # The surface ends a straight ray with the background's radiance. (A transparent surface, which let the ray on to a
    # sky of B, left a bright band over the upper frame once the camera turned level: the world shader did not see
    # those rays as straight, so they brought back the sky through the water.)
    tr = t.new("ShaderNodeEmission")
    t.feed(tr.inputs["Color"], t.xyz(*[b.outputs[0] for b in B]).outputs[0])
    tr.inputs["Strength"].default_value = 1.0
    mx = t.new("ShaderNodeMixShader")
    t.feed(mx.inputs[0], st)
    t.feed(mx.inputs[1], src)
    t.feed(mx.inputs[2], tr.outputs[0])
    nt.links.new(mx.outputs[0], out.inputs["Surface"])
    # the world: B to straight rays
    wt = SS.Tree(world.node_tree)
    wo = next(nd for nd in world.node_tree.nodes if nd.bl_idname == "ShaderNodeOutputWorld")
    wsrc = wo.inputs["Surface"].links[0].from_socket
    bg = wt.new("ShaderNodeBackground")
    bg.inputs["Strength"].default_value = 1.0
    wB = []
    for c in "rgb":
        v = wt.new("ShaderNodeValue")
        v.name = v.label = "matte-B-" + c
        wB.append(v)
    wt.feed(bg.inputs["Color"], wt.xyz(*[v.outputs[0] for v in wB]).outputs[0])
    wmx = wt.new("ShaderNodeMixShader")
    wt.feed(wmx.inputs[0], straight_ray(wt))
    wt.feed(wmx.inputs[1], wsrc)
    wt.feed(wmx.inputs[2], bg.outputs[0])
    world.node_tree.links.new(wmx.outputs[0], wo.inputs["Surface"])
    # every bubble a straight ray may cross: two refractions each
    sc.cycles.max_bounces = 16
    sc.cycles.transmission_bounces = 16
    sc.render.film_transparent = False
    sc.render.use_compositing = False
    print("LAYER matte: straight rays see a medium of the water's extinction %s emitting B times it, a surface"
          " emitting B and a sky of B; max and transmission bounces 16; the compositor off, the render saved scene-linear"
          % ", ".join("%.3f" % e for e in ext), flush=True)
    return B + wB

def tone_np(c):
    """sea_still.neutral_log_view's shoulder and desaturation on display-linear colour (its toe left out: it acts
    on a whole pixel, not on a layer's share of it)."""
    k = SS.TONE_KNEE
    lo_s, hi_s = 1e-4, 4.0
    for _ in range(80):
        s = 0.5 * (lo_s + hi_s)
        lo_s, hi_s = (lo_s, s) if k + s * math.log1p((SS.TONE_WHITE - k) / s) > 1.0 else (s, hi_s)
    peak = c.max(-1, keepdims=True)
    over = np.maximum(peak - k, 0.0)
    newpeak = np.minimum(peak, k) + s * np.log1p(over / s)
    g = 1.0 - 1.0 / ((peak - newpeak) * SS.TONE_DESAT + 1.0)
    ratio = newpeak / np.maximum(peak, 1e-6) * (1.0 - g)
    return c * ratio + newpeak * g

def read_exr(path):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"
    W, H, ch = img.size[0], img.size[1], img.channels
    px = np.empty(W * H * ch, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(H, W, ch)[..., :3].astype(np.float64)

def solve_matte(p1, p2, B2, gain, dst, seed, keep_exr=False):
    """Solve the pair rendered over B1 = 0 and B2 (scene-linear), and write the layer as an 8-bit RGBA PNG: straight
    colour, sRGB-encoded, in the display-linear units of the full render at this frame's exposure `gain`. Light a
    clear bubble adds (a glint over an opacity near 0) cannot be held as straight colour; the opacity is raised to
    the colour's brightest channel there, which darkens what is behind a glint by that much."""
    C1, C2 = read_exr(p1), read_exr(p2)
    b = np.array(B2)
    alpha = 1.0 - ((C2 - C1) @ b) / float(b @ b)
    alpha = np.clip(alpha, 0.0, 1.0)
    Fd = tone_np(np.maximum(C1, 0.0) * gain)
    a = np.maximum(alpha, np.clip(Fd.max(-1), 0.0, 1.0))
    F = np.clip(Fd / np.maximum(a, 1e-6)[..., None], 0.0, 1.0)
    F = np.where(F <= 0.0031308, F * 12.92, 1.055 * F ** (1 / 2.4) - 0.055)
    H, W = alpha.shape
    rgba = np.concatenate([F, a[..., None]], -1)
    rng = np.random.default_rng(seed)
    lv = np.clip(np.floor(rgba * 255.0 + 0.5 + rng.random(rgba.shape) - rng.random(rgba.shape)), 0.0, 255.0)
    lv[..., 3] = np.where(a <= 0.0, 0.0, lv[..., 3])
    out = bpy.data.images.new("matte", W, H, alpha=True)
    out.alpha_mode = "CHANNEL_PACKED"
    out.pixels.foreach_set((lv / 255.0).astype(np.float32).ravel())
    out.filepath_raw = dst
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    if not keep_exr:
        os.remove(p1)
        os.remove(p2)
    # the residual in the red channel, which the opacity (fitted on B2's green and blue) does not see
    corner = alpha[: H // 10, : W // 10]
    print("MATTE %s: opacity mean %.4f, %.2f%% over 0.5, %.2f%% over 0.05; raised for added light on %.2f%%; the top-left"
          " tenth's opacity %.4f (0 where nothing is); colour max %.3f"
          % (os.path.basename(dst), float(alpha.mean()), 100 * float((alpha > 0.5).mean()), 100 * float((alpha > 0.05).mean()),
             100 * float((a > alpha + 1e-3).mean()), float(corner.mean()), float(Fd.max())), flush=True)

def write_dithered_rgba(src, dst, seed):
    """write_dithered, keeping the alpha channel (straight, as the 16-bit PNG holds it)."""
    img = bpy.data.images.load(src)
    img.colorspace_settings.name = "Non-Color"
    img.alpha_mode = "CHANNEL_PACKED"
    W, H, ch = img.size[0], img.size[1], img.channels
    px = np.empty(W * H * ch, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    px = px.reshape(H, W, ch).astype(np.float64)
    if ch < 4:
        px = np.concatenate([px[..., :3], np.ones((H, W, 1))], -1)
    rng = np.random.default_rng(seed)
    lv = np.clip(np.floor(px * 255.0 + 0.5 + rng.random(px.shape) - rng.random(px.shape)), 0.0, 255.0)
    out = bpy.data.images.new("dithered", W, H, alpha=True)
    out.alpha_mode = "CHANNEL_PACKED"
    out.pixels.foreach_set((lv / 255.0).astype(np.float32).ravel())
    out.filepath_raw = dst
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    a = px[..., 3]
    print("DITHER-RGBA %s: alpha mean %.4f, %.2f%% of pixels with any alpha" % (os.path.basename(dst), float(a.mean()),
                                                                           100.0 * float((a > 1e-3).mean())), flush=True)

def in_view(X, st, hfov, vfov):
    """How many of the points X the camera of state `st` sees, in front of its port."""
    if not len(X):
        return 0
    p = math.radians(st["pitch"])
    d = np.array(st["dir"][:])
    right = np.cross(d, [0.0, 0.0, 1.0])
    right /= np.linalg.norm(right)
    up = np.cross(right, d)
    V = X - np.array(st["loc"][:])
    zc = V @ d
    ok = zc > PORT
    return int((ok & (np.abs(V @ right) < zc * math.tan(hfov / 2)) & (np.abs(V @ up) < zc * math.tan(vfov / 2))).sum())

# ---------------------------------------------------------------- the bubbles
def bubble_material():
    mat = bpy.data.materials.new("bubble")
    nt = mat.node_tree
    nt.nodes.clear()
    g = nt.nodes.new("ShaderNodeBsdfGlass")
    g.distribution = "GGX"
    g.inputs["Roughness"].default_value = 0.02
    g.inputs["IOR"].default_value = 1.0 / 1.333        # air inside water
    g.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(g.outputs[0], o.inputs["Surface"])
    return mat

class Bubbles:
    """The entry's burst: air dragged down with the camera, round and ahead of its course, each bubble slowing
    from a share of the camera's velocity towards its own rise speed and gone when it reaches the surface."""
    def __init__(self, states, entry, probe, count, seed=11):
        self.mat = bubble_material()
        me = bpy.data.meshes.new("bubbles")
        self.ob = bpy.data.objects.new("bubbles", me)
        bpy.context.scene.collection.objects.link(self.ob)
        ng = bpy.data.node_groups.new("bubble-gn", "GeometryNodeTree")
        ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
        ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
        N = ng.nodes
        gi, go = N.new("NodeGroupInput"), N.new("NodeGroupOutput")
        ico = N.new("GeometryNodeMeshIcoSphere")
        ico.inputs["Radius"].default_value = 1.0
        ico.inputs["Subdivisions"].default_value = 3
        sm = N.new("GeometryNodeSetMaterial")
        sm.inputs["Material"].default_value = self.mat
        ss = N.new("GeometryNodeSetShadeSmooth")
        iop = N.new("GeometryNodeInstanceOnPoints")
        na = N.new("GeometryNodeInputNamedAttribute")
        na.data_type = "FLOAT_VECTOR"
        na.inputs["Name"].default_value = "bscale"
        L = ng.links
        L.new(ico.outputs[0], sm.inputs[0])          # the first socket of each is its geometry
        L.new(sm.outputs[0], ss.inputs[0])
        L.new(gi.outputs[0], iop.inputs[0])
        L.new(ss.outputs[0], SS.sock(iop.inputs, "Instance"))
        L.new(SS.sock(na.outputs, "Attribute", "VECTOR"), SS.sock(iop.inputs, "Scale"))
        L.new(iop.outputs[0], go.inputs[0])
        self.ob.modifiers.new("bubbles", "NODES").node_group = ng
        no_deform_blur(self.ob)
        rng = np.random.default_rng(seed)
        n = int(count)
        self.n = n
        t_e = entry["t"]
        big = rng.random(n) < 0.04
        r = np.exp(rng.uniform(math.log(4e-4), math.log(6e-3), n))
        r[big] = rng.uniform(6e-3, 2.0e-2, int(big.sum()))
        # Half travel in the cavity's front, ahead of the lens and nearly at its speed; half in the wake round
        # and behind it, slower. Both slow towards their rise speed, the front ones a little later.
        front = rng.random(n) < 0.5
        ts = t_e + np.where(front, 0.10, 0.30) * rng.random(n) ** 1.5
        tau = np.where(front, 0.22, 0.16)
        times = np.array([s["t"] for s in states])
        def at(t, key):
            i = int(np.clip(np.searchsorted(times, t) - 1, 0, len(times) - 2))
            a = (t - times[i]) / (times[i + 1] - times[i])
            return states[i][key] * (1 - a) + states[i + 1][key] * a
        x0, v0 = np.zeros((n, 3)), np.zeros((n, 3))
        for i in range(n):
            c, v, vd = at(ts[i], "loc"), at(ts[i], "vel"), at(ts[i], "dir").normalized()
            e1 = vd.orthogonal().normalized()
            e2 = vd.cross(e1)
            th = rng.uniform(0, 2 * math.pi)
            if front[i]:
                rho, a, u = 0.45 * math.sqrt(rng.random()), rng.uniform(0.15, 1.1), rng.uniform(0.7, 1.0)
            else:
                rho, a, u = 0.70 * math.sqrt(rng.random()), rng.uniform(-0.8, 0.2), rng.uniform(0.3, 0.9)
            p = c + vd * a + (e1 * math.cos(th) + e2 * math.sin(th)) * rho
            x0[i] = p[:]
            v0[i] = (v * u)[:] + rng.normal(0.0, 0.4, 3)
        surf = probe.height(round(t_e * FPS) / FPS, entry["loc"].x, entry["loc"].y)
        x0[:, 2] = np.minimum(x0[:, 2], surf - 0.02 - 0.1 * rng.random(n))
        self.x0, self.v0, self.ts, self.r = x0, v0, ts, r
        self.rise = np.minimum(0.25, 0.136 * (r / 2.5e-4) ** 2)     # m/s: Stokes below a millimetre, then ~0.25
        self.tau = tau[:, None]
        self.flat = np.where(r > 2e-3, 0.75, 1.0)                    # the larger ones are oblate
        self.probe, self.entry = probe, entry
        print("BUBBLES %d (%d ahead of the lens), radius %.1f..%.1f mm (%d over 6 mm), released %.2f..%.2f s"
              % (n, int(front.sum()), 1e3 * r.min(), 1e3 * r.max(), int(big.sum()), ts.min(), ts.max()), flush=True)
    def positions(self, t):
        """The bubbles' centres at `t` and which of them are alive."""
        tau = t - self.ts
        live = tau >= 0
        tau = np.maximum(tau, 0.0)[:, None]
        w = np.zeros((self.n, 3))
        w[:, 2] = self.rise
        e = np.exp(-tau / self.tau)
        X = self.x0 + w * tau + (self.v0 - w) * self.tau * (1 - e)
        surf = self.probe.height(round(t * FPS) / FPS, self.entry["loc"].x, self.entry["loc"].y)
        live &= X[:, 2] < surf - self.r
        return X, live
    def at(self, t):
        X, live = self.positions(t)
        X, r, fl = X[live], self.r[live], self.flat[live]
        me = bpy.data.meshes.new("bubbles")
        me.vertices.add(len(X))
        if len(X):
            me.vertices.foreach_set("co", X.astype(np.float32).ravel())
            at = me.attributes.new("bscale", "FLOAT_VECTOR", "POINT")
            sc = np.stack([r, r, r * fl], -1).astype(np.float32)
            at.data.foreach_set("vector", sc.ravel())
        old = self.ob.data
        self.ob.data = me
        bpy.data.meshes.remove(old)
        return int(live.sum())

def meter(path):
    """The centre-weighted geometric mean of the display-linear luminance of a 16-bit PNG."""
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"
    W, H, ch = img.size[0], img.size[1], img.channels
    px = np.empty(W * H * ch, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    c = px.reshape(H, W, ch)[..., :3].astype(np.float64)
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    lum = c @ np.array([0.2126, 0.7152, 0.0722])
    y, x = np.mgrid[0:H, 0:W]
    w = np.exp(-(((x + 0.5) / W - 0.5) ** 2 + ((y + 0.5) / H - 0.5) ** 2) / (2 * 0.3 ** 2))
    return float(np.exp((w * np.log(lum + 1e-4)).sum() / w.sum()))

# ---------------------------------------------------------------- the sun in the water
N_WATER = 1.333

class WaterSun:
    """The light in the water, as physics gives it, once the camera is under. The still lets every shadow ray
    through the surface, so the sky and the sun light the water along their directions in the air. In the water
    that light travels bent: the whole sky is squeezed into a cone 48.6 degrees round the zenith, and the sun, 28
    degrees up in the air, is 48.5 degrees up. Unbent, the bright low sky towards the sun shone into the water
    nearly level and, forward-scattered, made a bright glow 20-30 degrees up that the water does not have.
    Under water (weight 1) the surface instead stops shadow rays, so the sky reaches the water only refracted, along
    paths Cycles finds by sampling the scattering and the surface (refractive caustics on); and the sun, which those
    paths almost never hit, is a Sun lamp seen by volume scattering alone, along the refracted direction, with the
    irradiance the surface lets through (Fresnel's transmittance and the beam's widening). The surface lets through
    only the shadow rays aimed within a degree of that lamp, so the water still dims its light with depth. Light
    focused by the waves (shafts, caustics) is not in it: the lamp's light enters through a flat surface. The weight
    blends the still's light (0) into this (1) frame by frame; above water it is 0 and the frame is the still's."""
    def __init__(self, sc, sun_dir, sea_mat):
        self.sc = sc
        E = self.measure(sc, sun_dir)
        zen = math.acos(min(1.0, sun_dir.z))
        zw = math.asin(math.sin(zen) / N_WATER)
        ci, ct = math.cos(zen), math.cos(zw)
        rs = ((ci - N_WATER * ct) / (ci + N_WATER * ct)) ** 2
        rp = ((N_WATER * ci - ct) / (N_WATER * ci + ct)) ** 2
        T = 1.0 - 0.5 * (rs + rp)
        self.factor = T * ci / ct                                       # normal irradiance in the water over the air's
        Ew = [e * self.factor for e in E]
        d = Vector((sun_dir.x, sun_dir.y, 0.0)).normalized() * math.sin(zw) + Vector((0, 0, math.cos(zw)))
        self.d = d                       # towards the sun, in the water
        ld = bpy.data.lights.new("water-sun", "SUN")
        peak = max(Ew)
        ld.color = tuple(e / peak for e in Ew)
        self.strength = peak
        ld.energy = peak
        ld.angle = math.radians(0.545) / N_WATER
        self.ob = bpy.data.objects.new("water-sun", ld)
        self.ob.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        for v in ("visible_camera", "visible_diffuse", "visible_glossy", "visible_transmission"):
            setattr(self.ob, v, False)
        self.ob.visible_volume_scatter = True
        sc.collection.objects.link(self.ob)
        self.ob.hide_render = True
        self.glint = None
        # The surface's shadow switch: transparent to shadow rays by (1 - weight), and always to those aimed at the
        # water sun. "Incoming" points back along the ray, so a ray towards the lamp has Incoming = -d.
        t = SS.Tree(sea_mat.node_tree)
        link = next(l for l in sea_mat.node_tree.links if l.from_node.bl_idname == "ShaderNodeLightPath")
        mix, shadow = link.to_socket, link.from_socket
        sea_mat.node_tree.links.remove(link)
        self.gate = t.new("ShaderNodeValue")
        self.gate.name = self.gate.label = "water-weight"
        self.gate.outputs[0].default_value = 0.0
        geo = t.new("ShaderNodeNewGeometry")
        dn = t.new("ShaderNodeVectorMath", operation="DOT_PRODUCT")
        t.feed(dn.inputs[0], geo.outputs["Incoming"])
        dn.inputs[1].default_value = tuple(-c for c in d)
        aimed = t.m("GREATER_THAN", dn.outputs["Value"], math.cos(math.radians(1.0)))
        self.aimed = aimed               # path C's shafts colour these rays
        through =t.m("ADD", t.m("SUBTRACT", 1.0, self.gate.outputs[0]), t.m("MULTIPLY", self.gate.outputs[0], aimed))
        t.feed(mix, t.m("MULTIPLY", shadow, through))
        print("WATER SUN irradiance in air %.4g %.4g %.4g (normal); transmittance %.3f, in the water %.3f of it, %.1f deg up"
              % (E[0], E[1], E[2], T, self.factor, 90.0 - math.degrees(zw)), flush=True)
    def measure(self, sc, sun_dir):
        """Render the sky round the sun through a 3-degree camera with the disc on and off, and integrate the
        difference over the pixels' solid angles."""
        keep = dict(cam=sc.camera, rx=sc.render.resolution_x, ry=sc.render.resolution_y,
                    pct=sc.render.resolution_percentage, spp=sc.cycles.samples, dn=sc.cycles.use_denoising,
                    comp=sc.render.use_compositing, fmt=sc.render.image_settings.file_format,
                    depth=sc.render.image_settings.color_depth, mb=sc.render.use_motion_blur)
        cd = bpy.data.cameras.new("sun-probe")
        cd.sensor_fit = "HORIZONTAL"
        cd.sensor_width = 36.0
        fov = math.radians(3.0)
        cd.lens = 18.0 / math.tan(fov / 2)
        po = bpy.data.objects.new("sun-probe", cd)
        po.rotation_euler = sun_dir.to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(po)
        sc.camera = po
        W = 160
        sc.render.resolution_x = sc.render.resolution_y = W
        sc.render.resolution_percentage = 100
        sc.cycles.samples, sc.cycles.use_denoising = 16, False
        sc.render.use_compositing = False
        sc.render.use_motion_blur = False
        SS.set_format(sc.render.image_settings, "OPEN_EXR")
        sky = SS.SKY_NODES[0]
        res = []
        for disc in (True, False):
            SS.setp(sky, "sun_disc", disc, "Sun Disc")
            path = os.path.join(os.path.dirname(SS.OUT), "sun-probe-%d.exr" % int(disc))
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(path)
            ch = img.channels
            px = np.empty(W * W * ch, np.float32)
            img.pixels.foreach_get(px)
            bpy.data.images.remove(img)
            os.remove(path)
            res.append(px.reshape(W, W, ch)[..., :3].astype(np.float64))
        SS.setp(sky, "sun_disc", True, "Sun Disc")
        u = (np.arange(W) + 0.5) / W - 0.5
        X, Y = np.meshgrid(u, u)
        tanh = math.tan(fov / 2) * 2
        cos3 = (1 + (X * tanh) ** 2 + (Y * tanh) ** 2) ** -1.5
        omega = (tanh / W) ** 2 * cos3
        E = [float(((res[0][..., c] - res[1][..., c]) * omega).sum()) for c in range(3)]
        sc.camera = keep["cam"]
        sc.render.resolution_x, sc.render.resolution_y = keep["rx"], keep["ry"]
        sc.render.resolution_percentage = keep["pct"]
        sc.cycles.samples, sc.cycles.use_denoising = keep["spp"], keep["dn"]
        sc.render.use_compositing = keep["comp"]
        sc.render.use_motion_blur = keep["mb"]
        SS.set_format(sc.render.image_settings, keep["fmt"])
        sc.render.image_settings.color_depth = keep["depth"]
        bpy.data.objects.remove(po, do_unlink=True)
        return E
    def light_bubbles(self, ob):
        """A twin of the water sun that the bubbles alone see, by reflection and refraction, so that the sun
        glints in them; the sea's surface must not see it, for to the surface it would be a second sun."""
        ld = self.ob.data.copy()
        ld.name = "water-sun-glints"
        self.glint = bpy.data.objects.new("water-sun-glints", ld)
        self.glint.rotation_euler = self.ob.rotation_euler.copy()
        for v in ("visible_camera", "visible_diffuse", "visible_volume_scatter"):
            setattr(self.glint, v, False)
        self.glint.visible_glossy = self.glint.visible_transmission = True
        coll = bpy.data.collections.new("water-sun-receivers")
        coll.objects.link(ob)
        self.glint_coll = coll           # path C's fizz joins it
        self.glint.light_linking.receiver_collection = coll
        self.sc.collection.objects.link(self.glint)
        self.glint.hide_render = True
    def set(self, w):
        self.gate.outputs[0].default_value = w
        self.ob.hide_render = w <= 0.0
        self.ob.data.energy = self.strength * w
        if self.glint is not None:
            self.glint.hide_render = w <= 0.0
            self.glint.data.energy = self.strength * w
        self.sc.cycles.caustics_refractive = w > 0.0

# ---------------------------------------------------------------- main
def main():
    global PORT
    args = parse()
    PORT = args.port
    T = PATH_SECONDS.get(args.path)
    n = int(round(T * FPS)) if T else None
    prefix = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(prefix), exist_ok=True)
    ctx = SS.build(args, prefix + ".png", ripple_motion=ripple_motion, fan_res=(1920, 1080))
    sc, cam, az, exposure = ctx["sc"], ctx["cam"], ctx["az"], ctx["exposure"]
    k, wind_dir = ctx["ocean"]["k"], ctx["wind_dir"]
    if args.export_only:
        run_export_only(args, ctx)
        return
    s = max(1.0, 1920.0 / ctx["rx"])
    pose = pose_setup(args, ctx) if args.pose_file else None
    sea = Sea(ctx["sea"], az if pose is None else pose["az_disc"], s, k, wind_dir)
    SS.stage("disc")
    mat = sea.ob.data.materials[0]
    clock = mat.node_tree.nodes["ripple-clock"].outputs[0]
    if args.light == "caustics":
        # The still's switch: a mix whose factor is "Is Shadow Ray", picking a transparent BSDF for shadow rays.
        # Unlinked and set to 0, every ray meets the dielectric, so sunlight reaches the water only refracted.
        for l in list(mat.node_tree.links):
            if l.from_node.bl_idname == "ShaderNodeLightPath":
                mix = l.to_node
                mat.node_tree.links.remove(l)
                mix.inputs[0].default_value = 0.0
        sc.cycles.caustics_refractive = True
        sc.cycles.caustics_reflective = True
        print("LIGHT caustics: the surface blocks shadow rays; refractive and reflective caustics on; filter glossy %.2f"
              % sc.cycles.blur_glossy, flush=True)
    wsun = None
    if args.water_sun != "off" and args.light == "trick":
        wsun = WaterSun(sc, ctx["sun"]["sun"], mat)
        SS.stage("water sun")
    if pose is not None:
        probe, states, entry = pose["probe"], pose["states"], pose["entry"]
        n = len(states)
        if args.path == "E":
            capillaries(mat, ctx["rip"])
    elif args.path == "C":
        probe = Probe((math.cos(az) * 2.0, math.sin(az) * 2.0), wind_dir, k, n=81, step=0.25)
        states, entry = path_C(probe, az)
        n = len(states)
    elif args.path in ("D", "E"):
        probe = Probe((math.cos(az) * 9.0, math.sin(az) * 9.0), wind_dir, k, n=113, step=0.25)
        states, entry = path_D(probe, az)
        n = len(states)
        if args.path == "E":
            capillaries(mat, ctx["rip"])
    else:
        centre = (0.0, 0.0) if args.path == "A" else (math.cos(az) * 1.0, math.sin(az) * 1.0)
        probe = Probe(centre, wind_dir, k)
        if args.path == "A":
            states, entry = path_A(probe, n)
        else:
            states, entry = path_B(probe, n, az)
    cam.animation_data_clear()
    if pose is None:
        key_camera(cam, states, az)
    else:
        key_camera_pose(cam, states)
    cam.data.clip_start = PORT
    sc.frame_start, sc.frame_end = 0, n - 1
    sc.render.use_motion_blur = args.shutter > 0
    sc.render.motion_blur_shutter = max(args.shutter, 0.01)
    # the surface under the camera, and the share of the frame under water
    for st in states:
        st["surf"] = probe.height(st["t"], st["loc"].x, st["loc"].y)
        st["hrel"] = st["loc"].z - st["surf"]
        p = math.radians(st["pitch"])
        # the share of the frame whose port point is under water, for a flat surface (port plane at args.port)
        lo = st["hrel"] + args.port * (math.sin(p) - math.tan(math.radians(18.9)) * math.cos(p))
        hi = st["hrel"] + args.port * (math.sin(p) + math.tan(math.radians(18.9)) * math.cos(p))
        under = 1.0 if hi < 0 else 0.0 if lo > 0 else -lo / (hi - lo)
        st["under"] = under
    for f in range(0, n, 6):
        st = states[f]
        print("PATH f %3d t %.2f cam %+.2f %+.2f %+.3f surf %+.3f hrel %+.3f pitch %+.1f under %.2f"
              % (f, st["t"], st["loc"].x, st["loc"].y, st["loc"].z, st["surf"], st["hrel"], st["pitch"], st["under"]),
              flush=True)
    track = None
    if args.ae_track:
        with open(args.ae_track) as fh:
            track = {int(k): v for k, v in json.load(fh).items() if k.isdigit()}
        print("AE from the track %s" % args.ae_track, flush=True)
    bubbles = None
    if args.path in ("B", "C", "D", "E") and args.bubbles > 0:
        bubbles = Bubbles(states, entry, probe, 6000 * args.bubbles)
        if pose is not None and args.bubble_export:
            write_bubble_export(args, pose, bubbles)
        elif args.bubble_export:
            write_own_bubble_export(args, bubbles)
        if wsun is not None:
            wsun.light_bubbles(bubbles.ob)
    ng = sc.compositing_node_group
    glare = [nd for nd in ng.nodes if nd.bl_idname == "CompositorNodeGlare"]
    spray = ww = shafts = fizz = None
    if args.path in ("C", "D", "E"):
        if args.spray > 0 and args.layer == "full":
            if args.path == "C":
                spray = Spray(entry, probe, 2500 * args.spray)
            else:
                spray = Spray(entry, probe, D_SPRAY * args.spray, mist=True)
        if args.white_water > 0:
            ww = WhiteWater(entry, probe, args.white_water, wsun, ctx["sun"], D_WW_ONSET if args.path in ("D", "E") else 0.0,
                           args.foam)
            fizz = Fizz(entry, states, probe, 40000 * args.white_water)
            if wsun is not None and getattr(wsun, "glint_coll", None) is not None:
                wsun.glint_coll.objects.link(fizz.ob)
        if wsun is not None and args.shafts == "on":
            shafts = Shafts(mat, wsun, ctx, args.shaft_debug, args.shaft_medium)
        matte_B = None
        if args.layer == "matte":
            matte_B = setup_matte(sc, mat, ctx["world"]["world"], sea.ob)
            try:
                sc.cycles.volume_biased = True
            except Exception as e:
                print("WARN volume_biased:", e, flush=True)
        if args.layer == "bubbles":
            # the bubbles alone: the film transparent, the sea unseen by the camera, its water clear to camera rays
            sc.render.film_transparent = True
            sea.ob.visible_camera = False
            gate_camera_rays(mat)
            comb = next(nd for nd in ng.nodes if nd.bl_idname == "CompositorNodeCombineColor")
            rl = next(nd for nd in ng.nodes if nd.bl_idname == "CompositorNodeRLayers")
            ng.links.new(rl.outputs["Alpha"], comb.inputs[3])
            # The denoiser does not touch alpha, and the unbiased volume sampler's transmittance through the white
            # water is a random estimate: its alpha came out grainy. Ray marching estimates it step by step.
            try:
                sc.cycles.volume_biased = True
            except Exception as e:
                print("WARN volume_biased:", e, flush=True)
            print("LAYER bubbles: film transparent, the sea hidden from the camera, its water clear to camera rays; volumes"
                  " ray-marched", flush=True)
        # the camera, frame by frame, for the hand-over to the theatre
        vfov, hfov = ctx["vfov"], ctx["hfov"]
        rows = []
        for f, st in enumerate(states):
            nv = nfz = 0
            if pose is not None:
                vfov_f = st["vfov"]
                hfov_f = 2 * math.atan(math.tan(vfov_f / 2) * math.tan(hfov / 2) / math.tan(vfov / 2))
            else:
                vfov_f, hfov_f = vfov, hfov
            if bubbles:
                X, live = bubbles.positions(st["t"])
                nv = in_view(X[live], st, hfov_f, vfov_f)
            if fizz:
                X, live = fizz.positions(st["t"])
                nfz = in_view(X[live], st, hfov_f, vfov_f)
            st["in_view"], st["fizz_in_view"] = nv, nfz
            rows.append({"f": f, "tf": st["tf"], "t": st["t"], "loc": list(st["loc"]), "surf": st["surf"],
                         "depth": st["surf"] - st["loc"].z, "pitch": st["pitch"], "dir": list(st["dir"]),
                         "speed": st["vel"].length, "under": st["under"], "bubbles_in_view": nv, "fizz_in_view": nfz})
            if pose is not None:
                # positions and directions in the pose file's frame, and the pose file's own frame number
                rows[-1]["loc"] = PZ.unplace_points(st["loc"][:], pose["centre"], pose["psi"]).tolist()
                rows[-1]["dir"] = PZ.unplace_vectors(st["dir"][:], pose["psi"]).tolist()
                rows[-1]["pose_f"] = st["pose_f"]
        with open(prefix + "-camera.json", "w") as fh:
            json.dump({"entry": {"t": entry["t"], "frame": entry["frame"], "hold": entry["hold"], "v": entry["v"],
                                 "beta_deg": math.degrees(entry["beta"])},
                       "hfov_deg": math.degrees(hfov), "vfov_deg": math.degrees(vfov), "lens_mm": args.lens,
                       "still_exposure": exposure, "ae_target": AE_TARGET, "frames": rows}, fh, indent=0)
        dense = max(range(n), key=lambda f: states[f]["in_view"])
        print("CAMERA-C written; the densest bubble frame %d (%d in view); last frame with a bubble in view %d"
              % (dense, states[dense]["in_view"], max([f for f in range(n) if states[f]["in_view"] > 0] or [-1])), flush=True)
        for f in range(n):
            st = states[f]
            if f % 3 == 0 or abs(f - entry["frame"]) <= 12:
                print("PATHC f %3d tf %.3f t %.4f cam %+.3f %+.3f %+.3f depth %+.3f pitch %+.1f speed %.2f under %.2f bubbles %d"
                      " fizz %d" % (f, st["tf"], st["t"], st["loc"].x, st["loc"].y, st["loc"].z, st["surf"] - st["loc"].z,
                                    st["pitch"], st["vel"].length, st["under"], st["in_view"], st["fizz_in_view"]), flush=True)
        if args.dry:
            print("DRY run: nothing rendered", flush=True)
            return
    SS.stage("paths")
    print("RENDER %s, %d frames of %d, %dx%d, %d samples, port %.2f m, shutter %.2f, under-stops %s, light %s"
          % (args.path, len(frame_list(args.frames, n, args.step)), n, ctx["rx"], ctx["ry"], args.samples, args.port,
             args.shutter, args.under_stops, args.light), flush=True)
    SS.set_format(sc.render.image_settings, "PNG")
    sc.render.image_settings.color_depth = "16"
    if args.layer == "bubbles":
        sc.render.image_settings.color_mode = "RGBA"
    if args.layer == "matte":
        SS.set_format(sc.render.image_settings, "OPEN_EXR")
        sc.render.image_settings.color_depth = "32"
        sc.render.image_settings.color_mode = "RGB"
        g0 = 2.0 ** exposure
        B2 = [c / g0 for c in MATTE_B2_DISPLAY]     # rescaled per frame below
    times, used, ae, f_prev = [], {}, 0.0, None
    metered = {}
    if args.ae_resume:
        # carry the metering on from an earlier run of the frames before these
        with open(args.ae_resume) as fh:
            prev = json.load(fh)
        ae, f_prev = prev["next"], prev["last"]
        print("AE resumed from %s: %+.3f after frame %d" % (args.ae_resume, ae, f_prev), flush=True)
    for f in frame_list(args.frames, n, args.step):
        t0 = time.time()
        st = states[f]
        sc.frame_set(f)
        sea.at(st["t"])
        clock.default_value = st["t"]
        nb = bubbles.at(st["t"]) if bubbles else 0
        ws = 0.0
        if wsun is not None:
            ws = 1.0 if args.water_sun == "always" else st["under"]
            wsun.set(ws)
        extra = ""
        if args.path in ("C", "D", "E"):
            if spray and spray.mist:
                ns = spray.at(st["t"], st["under"] < 1.0, args.shutter / FPS / d_ramp(st["t"] - entry["t"]))
            else:
                ns = spray.at(st["t"], st["under"] < 1.0) if spray else 0
            front = ww.at(st["t"], st["loc"]) if ww else 0.0
            nf = fizz.at(st["t"]) if fizz else 0
            tsh = shafts.update(st["t"], st["loc"], ws, args.layer == "full") if shafts else 0.0
            extra = " spray %d white-water front %.2f fizz %d shafts %.2f s" % (ns, front, nf, tsh)
        if track is not None:
            ae = track[min(track, key=lambda g: abs(g - f))]
        elif args.under_stops is not None:
            ae = args.under_stops * st["under"]
        used[f] = ae
        e = exposure + ae
        for nd in ctx["gain_nodes"]:
            nd.inputs[1].default_value = 2.0 ** e
        for gl in glare:
            for i in gl.inputs:
                if i.name == "Threshold":
                    i.default_value = SS.GLARE_T * 2.0 ** (-e)
        sc.cycles.seed = f
        t1 = time.time()
        if args.layer == "matte":
            # the pair: over B1 = 0 and over B2, the second background, held at the same display level every frame
            g = 2.0 ** e
            B2 = [c / g for c in MATTE_B2_DISPLAY]
            pair = []
            for i, Bk in enumerate(([0.0, 0.0, 0.0], B2)):
                for j, nd in enumerate(matte_B):
                    nd.outputs[0].default_value = Bk[j % 3]
                sc.render.filepath = "%s%04d-B%d.exr" % (prefix, f, i + 1)
                bpy.ops.render.render(write_still=True)
                pair.append(sc.render.filepath)
            t2 = time.time()
            solve_matte(pair[0], pair[1], B2, g, "%s%04d.png" % (prefix, f), f, keep_exr=args.keep_16bit)
            t3 = time.time()
            times.append((t1 - t0, t2 - t1, t3 - t0))
            print("FRAME %3d t %.3f under %.2f exposure %+.2f bubbles %d: sea %.2f s, two renders %.2f s, all %.2f s%s"
                  % (f, st["t"], st["under"], used[f], nb, t1 - t0, t2 - t1, t3 - t0, extra), flush=True)
            continue
        master = "%s%04d-16bit.png" % (prefix, f) if args.keep_16bit else prefix + "-master-16bit.png"
        sc.render.filepath = master
        bpy.ops.render.render(write_still=True)
        t2 = time.time()
        if args.layer == "bubbles":
            write_dithered_rgba(master, "%s%04d.png" % (prefix, f), f)
        else:
            SS.write_dithered(master, "%s%04d.png" % (prefix, f), f)
        m = meter(master)
        metered[f] = (used[f], m, st["under"])
        if args.shaft_ab and shafts is not None and ws > 0:
            # the same frame without the shafts, for comparison
            shafts.weight.outputs[0].default_value = 0.0
            sc.render.filepath = prefix + "-noshafts-master-16bit.png"
            ta = time.time()
            bpy.ops.render.render(write_still=True)
            print("SHAFT-AB frame %d: with %.2f s, without %.2f s (render only)" % (f, t2 - t1, time.time() - ta), flush=True)
            SS.write_dithered(sc.render.filepath, "%s%04d-noshafts.png" % (prefix, f), f)
            shafts.weight.outputs[0].default_value = ws
        if track is None and args.under_stops is None:
            # the automatic exposure: under water, towards the theatre's level, lagging AE_LAG
            want = st["under"] * min(4.0, max(-4.0, math.log2(AE_TARGET / max(m / 2.0 ** ae, 1e-6))))
            if st["under"] < 1.0:
                want = max(0.0, want)       # while the frame holds sky, the sky keeps the still's exposure
            dt = (f - f_prev) / FPS if f_prev is not None else 1.0 / FPS
            ae += (want - ae) * (1 - math.exp(-dt / AE_LAG))
        f_prev = f
        t3 = time.time()
        times.append((t1 - t0, t2 - t1, t3 - t0))
        print("FRAME %3d t %.3f hrel %+.3f pitch %+.1f under %.2f water-sun %.2f exposure %+.2f metered %.4f bubbles %d: sea %.2f s,"
              " render %.2f s, all %.2f s%s" % (f, st["t"], st["hrel"], st["pitch"], st["under"], ws, used[f], m, nb, t1 - t0,
                                               t2 - t1, t3 - t0, extra), flush=True)
    if used and not args.ae_track:
        out = {str(k): v for k, v in sorted(used.items())}
        out.update({"next": ae, "last": f_prev})
        with open(prefix + "-exposure-%04d-%04d.json" % (min(used), max(used)), "w") as fh:
            json.dump(out, fh, indent=0)
    if metered and args.path in ("C", "D", "E"):
        # each frame's exposure change, its metering and its share under water, for grading the exposure track
        with open(prefix + "-metered-%04d-%04d.json" % (min(metered), max(metered)), "w") as fh:
            json.dump({str(k): v for k, v in sorted(metered.items())}, fh, indent=0)
    if times:
        a = np.array(times)
        print("TIMES %d frames: sea and set-up %.2f s, render %.2f s, whole frame %.2f s on average; total %.1f s"
              % (len(a), a[:, 0].mean(), a[:, 1].mean(), a[:, 2].mean(), a[:, 2].sum()), flush=True)
    print("DONE", prefix, flush=True)

if __name__ == "__main__":
    main()
