"""The dive's crossing: the camera passes from above the open sea, through the surface, into the water. The scene
is the approved still's (sea_still.py's build(): its sky, its water, its view transform and dither), with the sea
set moving and the camera on one of two paths.

    blender -b --factory-startup --python sea_dive.py -- <out-prefix> <samples> --path A|B [options]

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
[under], --light trick or caustics [trick], --keep-16bit.
"""
import json, math, os, sys, time
import bpy
import numpy as np
from mathutils import Vector, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sea_still as SS

FPS = 30
G = 9.81
PATH_SECONDS = {"A": 6.0, "B": 4.0}
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
    ap.add_argument("--path", choices=("A", "B"), required=True)
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
    return ap.parse_args(SS.script_argv())

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
    def at(self, t):
        tau = t - self.ts
        live = tau >= 0
        tau = np.maximum(tau, 0.0)[:, None]
        w = np.zeros((self.n, 3))
        w[:, 2] = self.rise
        e = np.exp(-tau / self.tau)
        X = self.x0 + w * tau + (self.v0 - w) * self.tau * (1 - e)
        surf = self.probe.height(round(t * FPS) / FPS, self.entry["loc"].x, self.entry["loc"].y)
        live &= X[:, 2] < surf - self.r
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
        through = t.m("ADD", t.m("SUBTRACT", 1.0, self.gate.outputs[0]), t.m("MULTIPLY", self.gate.outputs[0], aimed))
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
    T = PATH_SECONDS[args.path]
    n = int(round(T * FPS))
    prefix = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(prefix), exist_ok=True)
    ctx = SS.build(args, prefix + ".png", ripple_motion=ripple_motion, fan_res=(1920, 1080))
    sc, cam, az, exposure = ctx["sc"], ctx["cam"], ctx["az"], ctx["exposure"]
    k, wind_dir = ctx["ocean"]["k"], ctx["wind_dir"]
    s = max(1.0, 1920.0 / ctx["rx"])
    sea = Sea(ctx["sea"], az, s, k, wind_dir)
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
    centre = (0.0, 0.0) if args.path == "A" else (math.cos(az) * 1.0, math.sin(az) * 1.0)
    probe = Probe(centre, wind_dir, k)
    if args.path == "A":
        states, entry = path_A(probe, n)
    else:
        states, entry = path_B(probe, n, az)
    cam.animation_data_clear()
    key_camera(cam, states, az)
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
    if args.path == "B" and args.bubbles > 0:
        bubbles = Bubbles(states, entry, probe, 6000 * args.bubbles)
        if wsun is not None:
            wsun.light_bubbles(bubbles.ob)
    ng = sc.compositing_node_group
    glare = [nd for nd in ng.nodes if nd.bl_idname == "CompositorNodeGlare"]
    SS.stage("paths")
    print("RENDER %s, %d frames of %d, %dx%d, %d samples, port %.2f m, shutter %.2f, under-stops %s, light %s"
          % (args.path, len(frame_list(args.frames, n, args.step)), n, ctx["rx"], ctx["ry"], args.samples, args.port,
             args.shutter, args.under_stops, args.light), flush=True)
    SS.set_format(sc.render.image_settings, "PNG")
    sc.render.image_settings.color_depth = "16"
    times, used, ae, f_prev = [], {}, 0.0, None
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
        master = "%s%04d-16bit.png" % (prefix, f) if args.keep_16bit else prefix + "-master-16bit.png"
        sc.render.filepath = master
        bpy.ops.render.render(write_still=True)
        t2 = time.time()
        SS.write_dithered(master, "%s%04d.png" % (prefix, f), f)
        m = meter(master)
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
              " render %.2f s, all %.2f s" % (f, st["t"], st["hrel"], st["pitch"], st["under"], ws, used[f], m, nb, t1 - t0, t2 - t1,
                                             t3 - t0), flush=True)
    if used and not args.ae_track:
        out = {str(k): v for k, v in sorted(used.items())}
        out.update({"next": ae, "last": f_prev})
        with open(prefix + "-exposure-%04d-%04d.json" % (min(used), max(used)), "w") as fh:
            json.dump(out, fh, indent=0)
    if times:
        a = np.array(times)
        print("TIMES %d frames: sea and set-up %.2f s, render %.2f s, whole frame %.2f s on average; total %.1f s"
              % (len(a), a[:, 0].mean(), a[:, 1].mean(), a[:, 2].mean(), a[:, 2].sum()), flush=True)
    print("DONE", prefix, flush=True)

if __name__ == "__main__":
    main()
