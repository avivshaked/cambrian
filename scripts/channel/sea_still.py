"""The open sea from above, one still: the first frame of the planned dive shot, in which the
camera will later fall through the surface into the theatre's water. This script makes the
still only.

    blender -b --factory-startup --python sea_still.py -- <out.png> <samples> [--eevee] [options]

The sky is Blender's physical sky texture (Multiple Scattering where this Blender has it), with
a 2D cloud layer drawn on the sky dome. The sea is a fan of quads under the camera, displaced
by the Ocean modifier (FFT spectrum), scaled to a set significant wave height, and faded flat
beyond a few hundred metres. The swells the mesh carries hold only a small part of the sea's
slope, so the rest is put back in two ways that hand over to each other by the pixel's footprint
on the sea. Near the camera, fine ripples drawn as a bump on the normal (five octaves of noise,
64 cm down to 4 cm, crests longer across the wind than along it) carry it, so that each ripple
throws its own spark of the sun: the glitter path. A small share stays with the microfacet
roughness even there (about 0.14), for the capillaries finer than any pixel, and it keeps each
spark wide enough, a fraction of a pixel, for the sampler to find. Each octave fades out as it shrinks
under a pixel, and the roughness takes up the slope it carried, until far away, where nothing is
resolved, the roughness alone carries Cox and Munk's whole mean square slope for the wind. The
surface is a clear dielectric with Beckmann microfacets, whose slopes are Gaussian as the sea's
nearly are. The bump's slope is measured before the render, on
the same node graph evaluated by geometry nodes, and printed beside Cox and Munk's. Under the
surface is a closed volume of absorbing, forward-scattering water (one Volume Coefficients
closure) whose colour, seen sideways from just below, is close to the theatre's shallow water
(theatre_scene.SHALLOW). The view transform is PBR Neutral's with a longer shoulder, built in the
compositor (neutral_log_view), so that the glitter keeps its gradations. The picture is written twice: a 16-bit PNG, and the 8-bit PNG asked for,
quantised from it with triangular dither so that smooth gradients do not band. Everything is
seeded: the ocean, the clouds, Cycles, the dither.

Before the scene is built, a small upward-looking render of the sky alone finds where the sun
really is and how bright the sky is. The camera is then aimed at the sun's azimuth and the
exposure set from the sky's brightness, so neither depends on a convention or a unit of the
sky texture that this script cannot check without running. --no-probe and --exposure skip
either.

Options (defaults in brackets): --sun elevation in degrees [28], --pitch the camera's downward
angle [11], --lens mm on the theatre's 24 mm vertical sensor [35], --height of the camera in
metres [10], --hs significant wave height in metres [0.9], --wind m/s [6], --clouds amount 0..1
[0.35], --exposure absolute, overriding the probe's, --percent of 1920x1080 [100],
--save-blend writes <out>.blend beside the picture. The 16-bit picture is <out>-16bit.png.
"""
import argparse, math, os, sys, time
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import theatre_scene as S

T0 = time.time()
_last = [T0]

def stage(name):
    now = time.time()
    print("STAGE %7.2f s (+%6.2f) %s" % (now - T0, now - _last[0], name), flush=True)
    _last[0] = now

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser(prog="sea_still.py")
ap.add_argument("out")
ap.add_argument("samples", type=int)
ap.add_argument("--eevee", action="store_true")
ap.add_argument("--sun", type=float, default=28.0)
ap.add_argument("--pitch", type=float, default=11.0)
ap.add_argument("--lens", type=float, default=35.0)
ap.add_argument("--height", type=float, default=10.0)
ap.add_argument("--hs", type=float, default=0.9)
ap.add_argument("--wind", type=float, default=6.0)
ap.add_argument("--clouds", type=float, default=0.35)
ap.add_argument("--exposure", type=float, default=None)
ap.add_argument("--percent", type=int, default=100)
ap.add_argument("--no-probe", action="store_true")
ap.add_argument("--save-blend", action="store_true")
A = ap.parse_args(argv)
OUT = os.path.abspath(A.out)
os.makedirs(os.path.dirname(OUT), exist_ok=True)

# ---------------------------------------------------------------- fixed choices
OCEAN_SEED, OCEAN_TIME = 1102, 12.0
OCEAN_TILE, OCEAN_RES = 150, 20          # metres; FFT grid is OCEAN_RES**2 a side (0.375 m cells)
WAVE_FADE = (140.0, 400.0)               # metres: full waves inside, flat beyond
FAR = 40000.0                            # metres: the fan's far edge, under half a pixel below the horizon
CLOUD_SEED = 7.31                        # the 4D noise's W
CLOUD_BASE_KM, CLOUD_SCALE = 1.6, 0.45   # layer height; noise cycles per km
AEROSOL = 1.0                            # Blender's default; at 2.5 the pale low sky the sea mirrors made it grey
SKY_STRENGTH = 1.0
HAZE_L, HAZE_MAX = 14000.0, 0.6          # metres: aerial perspective over the far sea
SKY_TARGET = 0.21                        # scene-linear luminance the probe's sky ring is exposed to: half a
                                         # stop under 0.30, as for a frame with the sun's glitter in it
GLARE_T = 1.2                            # bloom threshold after exposure
BLOOM = 0.2                              # bloom strength: at 0.45 the sparks' bloom doubled the water between them
# The tone curve (see neutral_log_view): PBR Neutral up to TONE_KNEE after exposure, then a log shoulder that
# reaches white at TONE_WHITE, about five stops above 1, where PBR Neutral's own shoulder reaches it about
# 1.3 stops above 1.
TONE_KNEE, TONE_WHITE, TONE_DESAT = 0.6, 30.0, 0.15
# Sea water per metre: absorption and scattering (R, G, B), Henyey-Greenstein anisotropy. The
# single-scatter albedo's ratio, 0.06 : 0.69 : 1, is SHALLOW's linear ratio, 0.10 : 0.65 : 1, so the
# water seen sideways from just under the surface is close to the theatre's colour.
WATER_ABSORB = (0.40, 0.085, 0.055)
WATER_SCATTER = (0.010, 0.035, 0.040)
WATER_G = 0.8
# The ripples: each octave's noise lattice spacing in metres, the finest about a pixel at the sun's
# reflection 20 m away in a full-size frame. An octave fades out as its spacing shrinks from 1.5 to 0.75
# of the pixel's footprint on the sea (the geometric mean of its two sides), and the roughness takes over.
RIPPLE_LATTICE = (0.64, 0.32, 0.16, 0.08, 0.04)
RIPPLE_FADE = (0.75, 1.5)
# The share of Cox and Munk's mean square slope left to the microfacet roughness even where every octave is
# drawn: the capillaries under 4 cm, which are under a pixel at any distance in this frame. It sets the near
# roughness, (SUBPIXEL_SHARE * mss) ** 0.25. At 0 (roughness 0.01) each ripple's image of the sun is about a
# fiftieth of a pixel across, a sampler at 128 samples misses most of them, and the denoiser smears the rest
# into blotches. At 0.05 (roughness 0.20) every glint converged, but each spread over several pixels and
# they merged into a flat mosaic. At 0.012 (roughness 0.14) each is a fraction of a pixel to a pixel.
SUBPIXEL_SHARE = 0.012
ROUGH_FLOOR = 0.01
DITHER_SEED = 0

def cox_munk(wind):
    """Cox and Munk's (1954) slope variances of a clean sea surface, upwind and crosswind, for a wind
    measured 12.5 m up; their sum is the mean square slope, 0.003 + 0.00512 W."""
    return 0.00316 * wind, 0.003 + 0.00192 * wind

def body_colour():
    """EEVEE has no deep volume: the diffuse colour of an infinitely deep layer of the same water
    (two-flux, with the reduced albedo), halved for the light the surface turns back inside."""
    out = []
    for a, s in zip(WATER_ABSORB, WATER_SCATTER):
        r = s * (1 - WATER_G) / (a + s * (1 - WATER_G))
        q = math.sqrt(1 - r)
        out.append(0.55 * (1 - q) / (1 + q))
    return tuple(out)

# ---------------------------------------------------------------- node helpers
def sock(sockets, name, kind=None):
    """A socket by name, preferring an enabled one of the given type: the Mix node carries a
    float, a vector and a colour socket under each name."""
    cand = [s for s in sockets if s.name == name]
    if kind:
        typed = [s for s in cand if s.type == kind]
        cand = typed or cand
    live = [s for s in cand if getattr(s, "enabled", True)]
    return (live or cand)[0]

class Tree:
    def __init__(self, nt):
        self.nt = nt
    def new(self, kind, **props):
        n = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n
    def feed(self, s, x):
        if x is None:
            return
        if isinstance(x, (int, float, tuple, list)):
            s.default_value = x
        else:
            self.nt.links.new(x, s)
    def m(self, op, a, b=None, clamp=False):
        n = self.new("ShaderNodeMath", operation=op, use_clamp=clamp)
        self.feed(n.inputs[0], a)
        self.feed(n.inputs[1], b)
        return n.outputs[0]
    def vm(self, op, a, b=None, scale=None):
        n = self.new("ShaderNodeVectorMath", operation=op)
        self.feed(n.inputs[0], a)
        self.feed(n.inputs[1], b)
        if scale is not None:
            self.feed(sock(n.inputs, "Scale"), scale)
        return sock(n.outputs, "Vector")
    def xyz(self, x, y, z):
        n = self.new("ShaderNodeCombineXYZ")
        for i, v in enumerate((x, y, z)):
            self.feed(n.inputs[i], v)
        return n
    def sep(self, v):
        n = self.new("ShaderNodeSeparateXYZ")
        self.feed(n.inputs[0], v)
        return n.outputs
    def maprange(self, v, a, b, c=0.0, d=1.0, smooth=True):
        n = self.new("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP" if smooth else "LINEAR", clamp=True)
        self.feed(sock(n.inputs, "Value", "VALUE"), v)
        for name, x in (("From Min", a), ("From Max", b), ("To Min", c), ("To Max", d)):
            self.feed(sock(n.inputs, name, "VALUE"), x)
        return sock(n.outputs, "Result", "VALUE")
    def noise(self, vec, scale, detail, rough, w=None):
        n = self.new("ShaderNodeTexNoise")
        n.noise_dimensions = "4D" if w is not None else "3D"
        self.feed(n.inputs["Vector"], vec)
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        if w is not None:
            n.inputs["W"].default_value = w
        return n.outputs["Fac"]
    def mix(self, fac, a, b):
        n = self.new("ShaderNodeMix", data_type="RGBA")
        self.feed(sock(n.inputs, "Factor", "VALUE"), fac)
        self.feed(sock(n.inputs, "A", "RGBA"), a)
        self.feed(sock(n.inputs, "B", "RGBA"), b)
        return sock(n.outputs, "Result", "RGBA")

def setp(node, attr, value, *names):
    """Set a node setting whether this Blender keeps it as a property or as an input socket."""
    if hasattr(node, attr):
        try:
            setattr(node, attr, value)
            return True
        except Exception:
            pass
    for n in names or (attr.replace("_", " ").title(),):
        for s in node.inputs:
            if s.name.lower() == n.lower() and hasattr(s, "default_value"):
                s.default_value = value
                return True
    print("WARN could not set %s.%s" % (node.bl_idname, attr), flush=True)
    return False

SKY_NODES = []

def sky_node(t, sun_size=math.radians(0.545)):
    n = t.new("ShaderNodeTexSky")
    for kind in ("MULTIPLE_SCATTERING", "SINGLE_SCATTERING", "NISHITA"):
        try:
            n.sky_type = kind
            break
        except Exception:
            continue
    setp(n, "sun_disc", True, "Sun Disc")
    setp(n, "sun_size", sun_size, "Sun Size")
    setp(n, "sun_intensity", 1.0, "Sun Intensity")
    setp(n, "sun_elevation", math.radians(A.sun), "Sun Elevation")
    setp(n, "sun_rotation", 0.0, "Sun Rotation")
    setp(n, "altitude", 0.0, "Altitude")
    setp(n, "air_density", 1.0, "Air")
    if not setp(n, "aerosol_density", AEROSOL, "Aerosols", "Aerosol"):
        setp(n, "dust_density", AEROSOL, "Dust")
    setp(n, "ozone_density", 1.0, "Ozone")
    SKY_NODES.append(n)
    return n

# ---------------------------------------------------------------- the sky and its clouds
def build_world():
    sc = bpy.context.scene
    world = bpy.data.worlds.new("sea-sky")
    sc.world = world
    t = Tree(world.node_tree)
    t.nt.nodes.clear()
    tc = t.new("ShaderNodeTexCoord")
    d = t.vm("NORMALIZE", tc.outputs["Generated"])
    dx, dy, dz = t.sep(d)
    sky = sky_node(t)
    # The zenith's radiance, the clouds' brightness in the sky's own units. Blender 5.1's multiple-scattering sky takes no
    # Vector input (it reads the ray), so a second sky node cannot be asked for the zenith; the probe measures it instead.
    zen = t.new("ShaderNodeRGB")
    zen.outputs[0].default_value = (1.0, 1.0, 1.0, 1.0)
    # the layer: the view ray met with a plane CLOUD_BASE_KM up
    dist = t.m("DIVIDE", CLOUD_BASE_KM, t.m("MAXIMUM", dz, 0.004))
    pos = t.xyz(t.m("MULTIPLY", dx, dist), t.m("MULTIPLY", dy, dist), 0.0).outputs[0]
    towards_sun = t.xyz(0.0, 0.25, 0.0)    # km; aimed at the probed sun later
    n1 = t.noise(pos, CLOUD_SCALE, 7.0, 0.55, CLOUD_SEED)
    n2 = t.noise(t.vm("ADD", pos, towards_sun.outputs[0]), CLOUD_SCALE, 7.0, 0.55, CLOUD_SEED)
    c0 = 0.75 - 0.30 * max(0.0, min(1.0, A.clouds))
    cover = t.maprange(n1, c0, c0 + 0.14)
    fade = t.maprange(dz, 0.003, 0.05)
    gate = t.new("ShaderNodeValue")
    gate.outputs[0].default_value = 1.0 if A.clouds > 0 else 0.0
    alpha = t.m("MULTIPLY", t.m("MULTIPLY", cover, fade), gate.outputs[0])
    # cheap self-shadow: denser towards the sun means less light reaches this point
    light = t.m("ADD", t.m("MULTIPLY", t.m("SUBTRACT", n1, n2), 6.0), 0.6, clamp=True)
    gain = t.m("ADD", t.m("MULTIPLY", light, 1.7), 0.9)
    cloud = t.vm("SCALE", zen.outputs["Color"], scale=gain)
    haze = t.m("SUBTRACT", 1.0, t.m("EXPONENT", t.m("DIVIDE", dist, -35.0)))
    cloud = t.mix(haze, cloud, sky.outputs["Color"])
    final = t.mix(alpha, sky.outputs["Color"], cloud)
    bg = t.new("ShaderNodeBackground")
    t.feed(bg.inputs["Color"], final)
    bg.inputs["Strength"].default_value = SKY_STRENGTH
    wo = t.new("ShaderNodeOutputWorld")
    t.feed(wo.inputs["Surface"], bg.outputs[0])
    return {"world": world, "gate": gate, "towards_sun": towards_sun, "zenith": zen}

# ---------------------------------------------------------------- the probe
def set_format(settings, fmt):
    if hasattr(settings, "media_type"):
        try:
            settings.media_type = "IMAGE"
        except Exception:
            pass
    settings.file_format = fmt

def probe_sky(world_info):
    """Render the sky alone through a 160-degree camera looking straight up, with no clouds and
    the sun disc enlarged so that a pixel cannot miss it; return the sun's direction and the
    median luminance of the sky 15-35 degrees up and well away from the sun."""
    sc = bpy.context.scene
    keep = dict(cam=sc.camera, rx=sc.render.resolution_x, ry=sc.render.resolution_y,
                pct=sc.render.resolution_percentage, spp=sc.cycles.samples, dn=sc.cycles.use_denoising,
                exp=sc.view_settings.exposure, fmt=sc.render.image_settings.file_format)
    world_info["gate"].outputs[0].default_value = 0.0
    for n in SKY_NODES:
        setp(n, "sun_size", math.radians(3.0), "Sun Size")
    cd = bpy.data.cameras.new("probe")
    cd.sensor_fit = "HORIZONTAL"
    cd.sensor_width = 36.0
    cd.lens = 18.0 / math.tan(math.radians(80.0))
    cd.clip_end = 1000.0
    cam = bpy.data.objects.new("probe", cd)
    cam.rotation_euler = (math.pi, 0.0, 0.0)
    sc.collection.objects.link(cam)
    sc.camera = cam
    W = H = 384
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = W, H, 100
    sc.cycles.samples, sc.cycles.use_denoising = 4, False
    sc.view_settings.exposure = 0.0
    path = None
    for fmt, ext in (("OPEN_EXR", ".exr"), ("HDR", ".hdr")):
        try:
            set_format(sc.render.image_settings, fmt)
            path = os.path.splitext(OUT)[0] + "_probe" + ext
            break
        except Exception as e:
            print("WARN probe format", fmt, e, flush=True)
    result = None
    try:
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path)
        ch = img.channels
        px = np.empty(W * H * ch, np.float32)
        img.pixels.foreach_get(px)
        bpy.data.images.remove(img)
        px = px.reshape(H, W, ch)
        lum = 0.2126 * px[..., 0] + 0.7152 * px[..., 1] + 0.0722 * px[..., 2]
        bpy.context.view_layer.update()
        tr, br, bl, tl = [np.array(v[:]) for v in cd.view_frame(scene=sc)]
        u = (np.arange(W) + 0.5) / W
        v = (np.arange(H) + 0.5) / H
        U, V = np.meshgrid(u, v)
        P = bl + U[..., None] * (br - bl) + V[..., None] * (tl - bl)
        R = np.array(cam.matrix_world.to_3x3())
        D = P @ R.T
        D /= np.linalg.norm(D, axis=-1, keepdims=True)
        peak, med = float(lum.max()), float(np.median(lum))
        up = D[..., 2] > math.cos(math.radians(10.0))
        zenith = tuple(float(c) for c in px[..., :3][up].mean(axis=0)) if up.any() else None
        low = np.degrees(np.arcsin(np.clip(D[..., 2], -1.0, 1.0)))
        low = (low > 4.0) & (low < 14.0)
        if peak < 20 * med:
            print("WARN probe: no sun found (peak %.4g, median %.4g)" % (peak, med), flush=True)
        else:
            sel = lum > 0.3 * peak
            s = (D[sel] * lum[sel][:, None]).sum(axis=0)
            s /= np.linalg.norm(s)
            elev = np.degrees(np.arcsin(D[..., 2]))
            away = D @ s < math.cos(math.radians(45.0))
            ring = lum[(elev > 15) & (elev < 35) & away]
            flat = np.array([s[0], s[1], 0.0])
            flat /= max(1e-9, float(np.linalg.norm(flat)))
            side = D @ flat
            horizon = {}
            for key, sel_h in (("toward", low & (side > 0.0) & away), ("away", low & (side <= 0.0))):
                horizon[key] = tuple(float(c) for c in px[..., :3][sel_h].mean(axis=0)) if sel_h.any() else None
            result = {"sun": Vector(s.tolist()), "sky": float(np.median(ring)) if ring.size else med, "peak": peak,
                      "zenith": zenith, "horizon": horizon}
    except Exception as e:
        print("WARN probe failed:", e, flush=True)
    sc.camera = keep["cam"]
    sc.render.resolution_x, sc.render.resolution_y = keep["rx"], keep["ry"]
    sc.render.resolution_percentage = keep["pct"]
    sc.cycles.samples, sc.cycles.use_denoising = keep["spp"], keep["dn"]
    sc.view_settings.exposure = keep["exp"]
    set_format(sc.render.image_settings, "PNG")
    world_info["gate"].outputs[0].default_value = 1.0 if A.clouds > 0 else 0.0
    for n in SKY_NODES:
        setp(n, "sun_size", math.radians(0.545), "Sun Size")
    bpy.data.objects.remove(cam, do_unlink=True)
    return result

# ---------------------------------------------------------------- the sea
def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)

def build_sea(az, wind_dir, vfov, hfov, rx, ry, density=1.5):
    """A fan of quads from under the camera to FAR, one row and one column every `density`
    pixels of the frame, so that it is as fine near the camera as the picture can show. Returns
    the object and the mesh's resolved slope rms, radial and sideways, 20-120 m out."""
    h = A.height
    dth = math.radians(density * math.degrees(vfov) / ry)
    th_max = math.radians(A.pitch) + vfov / 2 + math.radians(3.0)
    th_min = math.atan(h / FAR)
    nr = int(math.ceil((th_max - th_min) / dth)) + 1
    r = h / np.tan(np.linspace(th_max, th_min, nr))
    half = hfov / 2 + math.radians(6.0)
    nc = int(math.ceil(2 * half / math.radians(density * math.degrees(hfov) / rx))) + 1
    phi = az + np.linspace(-half, half, nc)
    X = r[:, None] * np.cos(phi)[None, :]
    Y = r[:, None] * np.sin(phi)[None, :]
    P = np.stack([X, Y, np.zeros_like(X)], axis=-1).reshape(-1, 3)
    rv = np.repeat(r, nc)
    idx = np.arange(nr * nc, dtype=np.int32).reshape(nr, nc)
    # outward then anticlockwise: the faces look up
    faces = np.stack([idx[:-1, :-1], idx[1:, :-1], idx[1:, 1:], idx[:-1, 1:]], axis=-1).reshape(-1, 4)
    nv, nf = len(P), len(faces)
    me = bpy.data.meshes.new("sea")
    me.vertices.add(nv)
    me.loops.add(nf * 4)
    me.polygons.add(nf)
    me.vertices.foreach_set("co", P.astype(np.float32).ravel())
    me.polygons.foreach_set("loop_start", np.arange(nf, dtype=np.int32) * 4)
    me.polygons.foreach_set("vertices", faces.ravel())
    me.update(calc_edges=True)
    me.shade_smooth()
    ob = bpy.data.objects.new("sea", me)
    bpy.context.scene.collection.objects.link(ob)
    print("SEA %d rows x %d columns, %d vertices, %.1f..%.0f m" % (nr, nc, nv, r[0], r[-1]), flush=True)
    stage("sea mesh")

    mod = ob.modifiers.new("ocean", "OCEAN")
    for k, v in (("geometry_mode", "DISPLACE"), ("resolution", OCEAN_RES), ("viewport_resolution", OCEAN_RES),
                 ("spatial_size", OCEAN_TILE), ("size", 1.0), ("random_seed", OCEAN_SEED), ("time", OCEAN_TIME),
                 ("spectrum", "PIERSON_MOSKOWITZ"), ("wind_velocity", A.wind), ("wave_alignment", 0.6),
                 ("wave_direction", wind_dir), ("choppiness", 0.9), ("wave_scale", 1.0),
                 ("wave_scale_min", 0.05), ("damping", 0.5), ("depth", 200.0),
                 ("use_normals", False), ("use_foam", False)):
        try:
            setattr(mod, k, v)
        except Exception as e:
            print("WARN ocean.%s: %s" % (k, e), flush=True)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    em = ev.to_mesh()
    if len(em.vertices) != nv:
        raise SystemExit("the ocean modifier changed the vertex count (%d, not %d)" % (len(em.vertices), nv))
    co = np.empty(nv * 3, np.float32)
    em.vertices.foreach_get("co", co)
    ev.to_mesh_clear()
    ob.modifiers.remove(mod)
    D = co.reshape(-1, 3).astype(np.float64) - P
    near = rv < WAVE_FADE[0]
    area = (rv * (rv * rv + h * h))[near]       # rows are even in angle, so weight each vertex by its patch of sea
    def wstd(x):
        mu = np.average(x, weights=area)
        return math.sqrt(float(np.average((x - mu) ** 2, weights=area)))
    sigma = wstd(D[near, 2])
    if sigma < 1e-5:
        raise SystemExit("the ocean modifier displaced nothing")
    k = (A.hs / 4.0) / sigma
    if not 0.2 <= k <= 5.0:
        print("WARN ocean scale %.3f clipped: the spectrum's own height is far from --hs" % k, flush=True)
        k = min(5.0, max(0.2, k))
    w = 1.0 - smoothstep(WAVE_FADE[0], WAVE_FADE[1], rv)
    Q = P + D * (k * w)[:, None]
    me.vertices.foreach_set("co", Q.astype(np.float32).ravel())
    me.update()
    Z = Q[:, 2].reshape(nr, nc)
    band = (r[:-1] > 20) & (r[:-1] < 120)
    slope = (np.diff(Z, axis=0) / np.diff(r)[:, None])[band]
    side = (np.diff(Z, axis=1) / (r[:, None] * (phi[1] - phi[0])))[:-1][band]
    mesh = (float(slope.std()), float(side.std()))
    print("OCEAN raw Hs %.3f m, scaled by %.3f to Hs %.3f m; highest crest %.2f m; widest sideways shift %.2f m;"
          " resolved radial slope rms %.3f, sideways %.3f (Cox-Munk at %.0f m/s, per axis: %.3f)"
          % (4 * sigma, k, 4 * wstd(Q[near, 2]), float(Q[near, 2].max()),
             float(np.linalg.norm(D[near, :2], axis=1).max() * k), mesh[0], mesh[1], A.wind,
             math.sqrt(0.5 * sum(cox_munk(A.wind)))), flush=True)
    if me.polygons[nf // 2].normal.z <= 0:
        print("WARN the sea's faces look down", flush=True)
    stage("ocean")
    return ob, mesh

def ripple_height(t, pos, wind_dir, q, amp, weights=None):
    """The ripples' height in metres at the world position `pos`: one Perlin octave for each spacing in
    RIPPLE_LATTICE, each of an amplitude proportional to its spacing so that every octave carries the same
    slope, the along-wind coordinate stretched by `q` so that the crests run longer across the wind than
    along it. `weights`, one per octave (a number or a socket), fade octaves out. The same graph serves the
    material's bump and the geometry-nodes measurement of its slope."""
    px, py, _ = t.sep(pos)
    ca, sa = math.cos(wind_dir), math.sin(wind_dir)
    along = t.m("ADD", t.m("MULTIPLY", px, ca), t.m("MULTIPLY", py, sa))
    across = t.m("SUBTRACT", t.m("MULTIPLY", py, ca), t.m("MULTIPLY", px, sa))
    total = None
    for i, lat in enumerate(RIPPLE_LATTICE):
        v = t.xyz(t.m("MULTIPLY", along, q / lat), t.m("MULTIPLY", across, 1.0 / lat), 17.31 * (i + 1)).outputs[0]
        h = t.m("MULTIPLY", t.m("SUBTRACT", t.noise(v, 1.0, 0.0, 0.5), 0.5), amp * lat)
        if weights is not None:
            h = t.m("MULTIPLY", h, weights[i])
        total = h if total is None else t.m("ADD", total, h)
    return total

def measure_ripples(wind_dir, q, n=1024, step=0.005):
    """The slope rms, along and across the wind, of the ripples at unit amplitude with every octave on:
    the height graph evaluated by geometry nodes on an n x n grid of points `step` apart, aligned with the
    wind, and differenced. The slope scales with the amplitude, so one measurement sets it."""
    ng = bpy.data.node_groups.new("ripple-probe", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    t = Tree(ng)
    gi, go = t.new("NodeGroupInput"), t.new("NodeGroupOutput")
    h = ripple_height(t, t.new("GeometryNodeInputPosition").outputs[0], wind_dir, q, 1.0)
    sp = t.new("GeometryNodeSetPosition")
    t.feed(sp.inputs["Geometry"], gi.outputs[0])
    t.feed(sp.inputs["Offset"], t.xyz(0.0, 0.0, h).outputs[0])
    t.feed(go.inputs[0], sp.outputs[0])
    u = np.array([math.cos(wind_dir), math.sin(wind_dir), 0.0])
    c = np.array([-math.sin(wind_dir), math.cos(wind_dir), 0.0])
    i, j = np.meshgrid(np.arange(n), np.arange(n), indexing="ij")
    P = (np.array([23.7, -11.3, 0.0]) + step * (i[..., None] * u + j[..., None] * c)).reshape(-1, 3)
    me = bpy.data.meshes.new("ripple-probe")
    me.vertices.add(n * n)
    me.vertices.foreach_set("co", P.astype(np.float32).ravel())
    ob = bpy.data.objects.new("ripple-probe", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("probe", "NODES").node_group = ng
    bpy.context.view_layer.update()
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    em = ev.to_mesh()
    co = np.empty(len(em.vertices) * 3, np.float32)
    em.vertices.foreach_get("co", co)
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    bpy.data.node_groups.remove(ng)
    if co.size != n * n * 3:
        raise SystemExit("the ripple probe returned %d points, not %d" % (co.size // 3, n * n))
    Z = co.reshape(n, n, 3)[..., 2].astype(np.float64)
    if float(Z.std()) < 1e-6:
        raise SystemExit("the ripple probe displaced nothing")
    return float((np.diff(Z, axis=0) / step).std()), float((np.diff(Z, axis=1) / step).std())

def ripple_weights(g):
    """Each octave's weight for `g`, one over the pixel footprint's size in metres (numbers here; the
    material computes the same with nodes)."""
    return [float(smoothstep(RIPPLE_FADE[0] / lat, RIPPLE_FADE[1] / lat, g)) for lat in RIPPLE_LATTICE]

def sea_material(eevee, rip):
    """`rip` holds the ripples' wind direction, stretch and amplitude, the mean square slope each octave
    carries at full weight, the mesh's, Cox and Munk's, and the frame's angle per pixel."""
    mat = bpy.data.materials.new("sea")
    t = Tree(mat.node_tree)
    t.nt.nodes.clear()
    dist = t.new("ShaderNodeCameraData").outputs["View Distance"]
    geo = t.new("ShaderNodeNewGeometry")
    # One over the pixel's footprint on the sea: d * pixel across the view, that over cos(incidence) along
    # it, and the geometric mean of the two.
    dot = t.new("ShaderNodeVectorMath", operation="DOT_PRODUCT")
    t.feed(dot.inputs[0], geo.outputs["Incoming"])
    t.feed(dot.inputs[1], geo.outputs["Normal"])
    cosi = t.m("MAXIMUM", t.m("ABSOLUTE", dot.outputs["Value"]), 0.02)
    g = t.m("DIVIDE", t.m("SQRT", cosi), t.m("MULTIPLY", dist, rip["pixel"]))
    weights = [t.maprange(g, RIPPLE_FADE[0] / lat, RIPPLE_FADE[1] / lat) for lat in RIPPLE_LATTICE]
    bump = t.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 1.0
    bump.inputs["Distance"].default_value = 1.0          # the height is in metres, so the slope is its gradient
    t.feed(bump.inputs["Height"], ripple_height(t, geo.outputs["Position"], rip["wind_dir"], rip["q"], rip["amp"], weights))
    # The roughness carries whatever mean square slope neither the mesh nor the drawn ripples do: GGX alpha
    # is roughness squared, and alpha squared is the mean square slope.
    resolved = t.m("MULTIPLY", t.m("POWER", t.maprange(dist, WAVE_FADE[0], WAVE_FADE[1], 1.0, 0.0), 2.0), rip["mesh_mss"])
    for w in weights:
        resolved = t.m("ADD", resolved, t.m("MULTIPLY", t.m("MULTIPLY", w, w), rip["octave_mss"]))
    rest = t.m("MAXIMUM", t.m("SUBTRACT", rip["mss"], resolved), 0.0)
    rough = t.m("POWER", t.m("ADD", rest, ROUGH_FLOOR ** 4), 0.25)
    if eevee:
        pb = t.new("ShaderNodeBsdfPrincipled")
        t.feed(pb.inputs["Roughness"], rough)
        pb.inputs["IOR"].default_value = 1.333
        t.feed(pb.inputs["Normal"], bump.outputs["Normal"])
        pb.inputs["Base Color"].default_value = (*body_colour(), 1.0)
        pb.inputs["Transmission Weight"].default_value = 0.0
    else:
        # A clear dielectric with Beckmann microfacets, whose slopes are Gaussian as Cox and Munk's nearly
        # are. The Principled BSDF's GGX, at the far sea's alpha of 0.18, puts some twenty times a
        # Gaussian's density on a facet sloped 0.5 and three hundred times on one sloped 0.6: with the sun
        # in them, those tails laid a pale glow over the whole sea.
        pb = t.new("ShaderNodeBsdfGlass")
        pb.distribution = "BECKMANN"
        pb.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
        t.feed(pb.inputs["Roughness"], rough)
        pb.inputs["IOR"].default_value = 1.333
        t.feed(pb.inputs["Normal"], bump.outputs["Normal"])
    # aerial perspective: the far sea fades towards the sky just above the horizon behind it
    ix, iy, _ = t.sep(geo.outputs["Incoming"])
    flat = t.vm("NORMALIZE", t.xyz(t.m("MULTIPLY", ix, -1.0), t.m("MULTIPLY", iy, -1.0), 0.0).outputs[0])
    fx, fy, _ = t.sep(flat)
    # The sky just above the horizon, as the probe measured it on the sun's side and away from it (Blender 5.1's
    # multiple-scattering sky takes no Vector input, so a sky node cannot be asked along a chosen direction).
    tw = t.new("ShaderNodeRGB")
    tw.outputs[0].default_value = (*HORIZON["toward"], 1.0)
    aw = t.new("ShaderNodeRGB")
    aw.outputs[0].default_value = (*HORIZON["away"], 1.0)
    k = t.m("ADD", t.m("MULTIPLY", t.m("ADD", t.m("MULTIPLY", fx, math.cos(az)), t.m("MULTIPLY", fy, math.sin(az))), 0.5), 0.5,
            clamp=True)
    em = t.new("ShaderNodeEmission")
    t.feed(em.inputs["Color"], t.mix(k, aw.outputs[0], tw.outputs[0]))
    em.inputs["Strength"].default_value = SKY_STRENGTH
    haze = t.m("MULTIPLY", t.m("SUBTRACT", 1.0, t.m("EXPONENT", t.m("DIVIDE", dist, -HAZE_L))), HAZE_MAX)
    mx = t.new("ShaderNodeMixShader")
    t.feed(mx.inputs[0], haze)
    t.feed(mx.inputs[1], pb.outputs[0])
    t.feed(mx.inputs[2], em.outputs[0])
    surface = mx.outputs[0]
    out = t.new("ShaderNodeOutputMaterial")
    if not eevee:
        # Shadow rays pass the surface, so the sun and sky light the water under it directly
        # (refraction is ignored on them); camera and bounce rays meet the real dielectric.
        lp = t.new("ShaderNodeLightPath")
        tr = t.new("ShaderNodeBsdfTransparent")
        sh = t.new("ShaderNodeMixShader")
        t.feed(sh.inputs[0], lp.outputs["Is Shadow Ray"])
        t.feed(sh.inputs[1], surface)
        t.feed(sh.inputs[2], tr.outputs[0])
        surface = sh.outputs[0]
        # One volume closure, not an Absorption and a Scatter added: Cycles budgets every volume closure
        # node in a shader as a whole volume stack's worth of closures (32; three such nodes read 99 in a
        # test), so the pair alone took the 64 a shader may hold, and the Principled BSDF, the emission
        # and the transparent made it 77. This shader comes to 35.
        try:
            vc = t.new("ShaderNodeVolumeCoefficients")
            vc.inputs["Absorption Coefficients"].default_value = WATER_ABSORB
            vc.inputs["Scatter Coefficients"].default_value = WATER_SCATTER
            vc.inputs["Anisotropy"].default_value = WATER_G
            vol = vc.outputs[0]
        except Exception as e:
            print("WARN no Volume Coefficients node (%s): absorption and scatter as two closures" % e, flush=True)
            ab = t.new("ShaderNodeVolumeAbsorption")
            ab.inputs["Color"].default_value = (*[1.0 - a for a in WATER_ABSORB], 1.0)
            ab.inputs["Density"].default_value = 1.0
            sct = t.new("ShaderNodeVolumeScatter")
            sct.inputs["Color"].default_value = (*WATER_SCATTER, 1.0)
            sct.inputs["Density"].default_value = 1.0
            sct.inputs["Anisotropy"].default_value = WATER_G
            add = t.new("ShaderNodeAddShader")
            t.feed(add.inputs[0], ab.outputs[0])
            t.feed(add.inputs[1], sct.outputs[0])
            vol = add.outputs[0]
        t.feed(out.inputs["Volume"], vol)
        for k, v in (("homogeneous_volume", True),):
            try:
                setattr(mat.cycles, k, v)
            except Exception:
                pass
    t.feed(out.inputs["Surface"], surface)
    for holder in (mat, getattr(mat, "cycles", None)):        # the haze is not a light
        if holder is not None and hasattr(holder, "emission_sampling"):
            holder.emission_sampling = "NONE"
            break
    else:
        print("WARN could not set emission_sampling", flush=True)
    return mat

# ---------------------------------------------------------------- the scene
S.clear_scene()
sc = S.setup_render(100, A.samples, exposure=0.0, motion_blur=False)
sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
sc.render.resolution_percentage = A.percent
sc.render.use_persistent_data = False
sc.cycles.max_bounces = 6
sc.cycles.caustics_refractive = False    # no second, refracted count of the sun that the shadow rays already bring
sc.cycles.seed = 0
sc.cycles.use_animated_seed = False
stage("setup")

wi = build_world()
stage("world")

sun = None
if not A.no_probe:
    sun = probe_sky(wi)
    stage("probe")
if sun is not None:
    s = sun["sun"]
    az = math.atan2(s.y, s.x)
    elev = math.degrees(math.asin(max(-1.0, min(1.0, s.z))))
    print("PROBE sun azimuth %.2f deg, elevation %.2f deg (asked %.1f); sky ring %.4g, sun peak %.4g"
          % (math.degrees(az), elev, A.sun, sun["sky"], sun["peak"]), flush=True)
    if abs(elev - A.sun) > 2.0:
        print("WARN the probed sun is %.1f deg from the elevation asked" % (elev - A.sun), flush=True)
else:
    az = math.radians(90.0)               # sun_rotation 0 is believed to put the sun at +Y
    print("WARN no probe: the camera faces +Y, where the sky's sun is believed to be", flush=True)
exposure = A.exposure
if exposure is None:
    exposure = math.log2(SKY_TARGET / sun["sky"]) if sun else 0.0
    exposure = max(-14.0, min(14.0, exposure))
sc.view_settings.exposure = exposure
print("EXPOSURE %.3f%s" % (exposure, " (given)" if A.exposure is not None else " (from the probe)" if sun else " (default)"),
      flush=True)
if sun is not None and sun.get("zenith") is not None:
    z = sun["zenith"]
    wi["zenith"].outputs[0].default_value = (z[0], z[1], z[2], 1.0)
    print("PROBE zenith radiance %.4g %.4g %.4g" % z, flush=True)
else:
    s = sun["sky"] if sun else 1.0
    wi["zenith"].outputs[0].default_value = (0.6 * s, 0.8 * s, 1.0 * s, 1.0)
    print("WARN no zenith from the probe: the clouds take a blue-white guess at the sky ring's level", flush=True)
HORIZON = {"toward": None, "away": None}
if sun is not None:
    HORIZON.update({k: v for k, v in (sun.get("horizon") or {}).items() if v is not None})
for key, guess in (("toward", (1.0, 0.95, 0.9)), ("away", (0.7, 0.8, 1.0))):
    if HORIZON[key] is None:
        s0 = sun["sky"] if sun else 1.0
        HORIZON[key] = tuple(g * s0 for g in guess)
        print("WARN no %s horizon from the probe: a guess at the sky ring's level" % key, flush=True)
print("PROBE horizon toward the sun %.4g %.4g %.4g, away %.4g %.4g %.4g" % (HORIZON["toward"] + HORIZON["away"]), flush=True)
wi["towards_sun"].inputs[0].default_value = 0.25 * math.cos(az)
wi["towards_sun"].inputs[1].default_value = 0.25 * math.sin(az)

pitch = math.radians(A.pitch)
loc = Vector((0.0, 0.0, A.height))
aim = loc + Vector((math.cos(az) * math.cos(pitch), math.sin(az) * math.cos(pitch), -math.sin(pitch)))
cam = S.make_camera(tuple(loc), tuple(aim), lens=A.lens)
cam.data.dof.use_dof = False
cam.data.clip_start, cam.data.clip_end = 0.3, 60000.0
rx = sc.render.resolution_x * A.percent // 100
ry = sc.render.resolution_y * A.percent // 100
vfov = 2 * math.atan(0.5 * cam.data.sensor_height / A.lens)
hfov = 2 * math.atan(math.tan(vfov / 2) * sc.render.resolution_x / sc.render.resolution_y)
print("CAMERA %.0f m up, %.1f deg down, lens %.0f mm: %.1f x %.1f deg; frame top %.1f deg above the horizon"
      % (A.height, A.pitch, A.lens, math.degrees(hfov), math.degrees(vfov), math.degrees(vfov / 2) - A.pitch), flush=True)

wind_dir = math.remainder(az + math.radians(200.0), 2 * math.pi)   # the swells and the ripples run with it
sea, mesh_slope = build_sea(az, wind_dir, vfov, hfov, rx, ry)

# The ripples make up the slope the mesh lacks, per axis, less the sub-pixel share: the mesh's radial slope
# is taken as along the wind (which runs 20 degrees off the view) and its sideways slope as across it.
cm_up, cm_cross = cox_munk(A.wind)
keep = 1.0 - SUBPIXEL_SHARE
want_along = math.sqrt(keep * max(0.0, cm_up - mesh_slope[0] ** 2))
want_across = math.sqrt(keep * max(0.0, cm_cross - mesh_slope[1] ** 2))
iso = measure_ripples(wind_dir, 1.0)         # Blender's noise is not quite isotropic: measure, then stretch
q = (want_along / max(want_across, 1e-6)) / (iso[0] / iso[1])
unit = measure_ripples(wind_dir, q)
amp = math.sqrt((want_along ** 2 + want_across ** 2) / (unit[0] ** 2 + unit[1] ** 2))
bump_along, bump_across = amp * unit[0], amp * unit[1]
rip = {"wind_dir": wind_dir, "q": q, "amp": amp, "pixel": vfov / ry,
       "octave_mss": (bump_along ** 2 + bump_across ** 2) / len(RIPPLE_LATTICE),
       "mesh_mss": mesh_slope[0] ** 2 + mesh_slope[1] ** 2, "mss": cm_up + cm_cross}
sub_along = math.sqrt(SUBPIXEL_SHARE * max(0.0, cm_up - mesh_slope[0] ** 2))
sub_across = math.sqrt(SUBPIXEL_SHARE * max(0.0, cm_cross - mesh_slope[1] ** 2))
tot_along = math.sqrt(bump_along ** 2 + mesh_slope[0] ** 2 + sub_along ** 2)
tot_across = math.sqrt(bump_across ** 2 + mesh_slope[1] ** 2 + sub_across ** 2)
print("RIPPLES %d octaves, %.0f to %.0f cm, unstretched slope ratio %.3f, stretched %.3f along the wind, height scale"
      " %.4f; slope rms along the wind / across it: the bump (measured by geometry nodes) %.3f / %.3f, the mesh %.3f /"
      " %.3f, the near roughness %.3f / %.3f; all three %.3f / %.3f, per axis %.3f (Cox-Munk at %.0f m/s: upwind %.3f,"
      " crosswind %.3f, per axis %.3f)"
      % (len(RIPPLE_LATTICE), 100 * RIPPLE_LATTICE[0], 100 * RIPPLE_LATTICE[-1], iso[0] / iso[1], q, amp, bump_along,
         bump_across, mesh_slope[0], mesh_slope[1], sub_along, sub_across, tot_along, tot_across,
         math.sqrt(0.5 * (tot_along ** 2 + tot_across ** 2)), A.wind, math.sqrt(cm_up), math.sqrt(cm_cross),
         math.sqrt(0.5 * (cm_up + cm_cross))), flush=True)
cols = []
for r_m in (15, 20, 30, 50, 100, 200, 400, 1000):
    d = math.hypot(r_m, A.height)
    w = ripple_weights(math.sqrt(max(A.height / d, 0.02)) / (d * rip["pixel"]))
    res = sum(x * x for x in w) * rip["octave_mss"] + rip["mesh_mss"] * (1 - float(smoothstep(*WAVE_FADE, d))) ** 2
    cols.append("%d m: bump %3.0f%%, roughness %.3f" % (r_m, 100 * sum(x * x for x in w) / len(w),
                                                        (max(0.0, rip["mss"] - res) + ROUGH_FLOOR ** 4) ** 0.25))
print("ROUGH along the frame's centre line, the share of the ripples' slope drawn and the roughness: " + "; ".join(cols),
      flush=True)
stage("ripples")
sea.data.materials.append(sea_material(A.eevee, rip))
if not A.eevee:
    sol = sea.modifiers.new("deep", "SOLIDIFY")   # closes the water: a volume needs an inside
    sol.thickness = 300.0
    sol.offset = -1.0
    sol.use_rim = True
    sol.use_even_offset = False
stage("material")

def neutral_log_view(exposure):
    """The view transform moved into the compositor, after the grade, with a longer shoulder. theatre_scene
    sets Khronos PBR Neutral, which is the identity up to 0.76 and white by about 1.8: everything brighter
    than 1.8, which is the sun's glitter, came out as flat white, and the far glitter column, whose light
    rises smoothly through four stops towards its middle, showed a hard edge where it crossed 1. Here the
    exposure, PBR Neutral's toe and its desaturation of bright colours towards white are kept, and its
    shoulder is replaced: a peak p above TONE_KNEE becomes k + s*ln(1 + (p - k)/s), with slope 1 at the knee
    and white at TONE_WHITE. Under the knee, where all the water and the dive's underwater frames sit,
    the picture is PBR Neutral's to the bit. The scene's view transform becomes Standard, exposure 0."""
    k = TONE_KNEE
    lo_s, hi_s = 1e-4, 4.0
    for _ in range(80):                          # s such that the shoulder reaches 1 at TONE_WHITE
        s = 0.5 * (lo_s + hi_s)
        lo_s, hi_s = (lo_s, s) if k + s * math.log1p((TONE_WHITE - k) / s) > 1.0 else (s, hi_s)
    sc = bpy.context.scene
    ng = sc.compositing_node_group
    go = next(n for n in ng.nodes if n.bl_idname == "NodeGroupOutput")
    src = go.inputs[0].links[0].from_socket
    t = Tree(ng)
    sep = t.new("CompositorNodeSeparateColor")
    t.feed(sep.inputs[0], src)
    ch = [t.m("MULTIPLY", sep.outputs[i], 2.0 ** exposure) for i in range(3)]
    lo = t.m("MINIMUM", t.m("MINIMUM", ch[0], ch[1]), ch[2])
    toe = t.m("SUBTRACT", lo, t.m("MULTIPLY", t.m("MULTIPLY", lo, lo), 6.25))
    off = t.m("ADD", t.m("MULTIPLY", t.m("LESS_THAN", lo, 0.08), t.m("SUBTRACT", toe, 0.04)), 0.04)
    ch = [t.m("SUBTRACT", c, off) for c in ch]
    peak = t.m("MAXIMUM", t.m("MAXIMUM", ch[0], ch[1]), ch[2])
    over = t.m("MAXIMUM", t.m("SUBTRACT", peak, k), 0.0)
    newpeak = t.m("ADD", t.m("MINIMUM", peak, k),
                  t.m("MULTIPLY", t.m("LOGARITHM", t.m("ADD", t.m("DIVIDE", over, s), 1.0), math.e), s))
    g = t.m("SUBTRACT", 1.0, t.m("DIVIDE", 1.0, t.m("ADD", t.m("MULTIPLY", t.m("SUBTRACT", peak, newpeak), TONE_DESAT), 1.0)))
    ratio = t.m("MULTIPLY", t.m("DIVIDE", newpeak, t.m("MAXIMUM", peak, 1e-6)), t.m("SUBTRACT", 1.0, g))
    white = t.m("MULTIPLY", newpeak, g)
    comb = t.new("CompositorNodeCombineColor")
    for i, c in enumerate(ch):
        t.feed(comb.inputs[i], t.m("ADD", t.m("MULTIPLY", c, ratio), white))
    t.feed(comb.inputs[3], sep.outputs[3])
    ng.links.new(comb.outputs[0], go.inputs[0])
    vs = sc.view_settings
    was = (vs.view_transform, vs.look)
    vs.view_transform = "Standard"
    vs.look = "None"
    vs.exposure = 0.0
    print("VIEW theatre_scene set %s, look %s; now Standard, exposure 0, with exposure %.3f, PBR Neutral's toe and"
          " desaturation and a log shoulder (knee %.2f, s %.4f, white at %.0f, %.1f stops over 1) in the compositor"
          % (was[0], was[1], exposure, k, s, TONE_WHITE, math.log2(TONE_WHITE)), flush=True)

S.grade(threshold=GLARE_T * 2.0 ** (-exposure), strength=BLOOM, size=0.6, vignette=0.2)
neutral_log_view(exposure)

if A.eevee:
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except Exception:
            continue
    sc.eevee.taa_render_samples = A.samples
    w = sc.world
    try:
        thr = 30.0 * sun["sky"] if sun else 10.0
        if sun:
            thr = min(thr, sun["peak"] / 3.0)
        w.sun_threshold = thr
        w.sun_angle = math.radians(0.545)
        w.use_sun_shadow = False
        print("EEVEE world sun threshold %.4g" % thr, flush=True)
    except Exception as e:
        print("WARN EEVEE world sun:", e, flush=True)
print("RENDER engine", sc.render.engine, "samples", A.samples, "denoiser", sc.cycles.denoiser,
      "use_gpu", getattr(sc.cycles, "denoising_use_gpu", "?"), "compositor", getattr(sc.render, "compositor_device", "?"),
      "%dx%d" % (rx, ry), flush=True)
stage("grade")

if A.save_blend:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.splitext(OUT)[0] + ".blend")
    stage("save blend")

if os.environ.get("EVOSIM_PROFILE"):
    last = [None]
    def on_stats(st):
        key = st.split("|")[-1].strip()[:60]
        if key != last[0]:
            last[0] = key
            stage("stats " + key)
    bpy.app.handlers.render_stats.append(on_stats)
    bpy.app.handlers.render_pre.append(lambda *a: stage("render_pre"))
    bpy.app.handlers.render_post.append(lambda *a: stage("render_post"))
    bpy.app.handlers.render_write.append(lambda *a: stage("render_write"))

def write_dithered(src, dst, seed):
    """Quantise the 16-bit picture `src` to the 8-bit `dst` with triangular dither of one level either way.
    Blender 5.1's own dither_intensity left the first take's 8-bit PNG undithered (clean one-level contour
    lines in its smooth gradients), so the dither is done here. Read as non-colour data, the 16-bit PNG's
    values are the display-encoded levels, untouched."""
    img = bpy.data.images.load(src)
    img.colorspace_settings.name = "Non-Color"
    W, H, ch = img.size[0], img.size[1], img.channels
    px = np.empty(W * H * ch, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    px = px.reshape(H, W, ch)[..., :3].astype(np.float64)
    rng = np.random.default_rng(seed)
    lv = np.clip(np.floor(px * 255.0 + 0.5 + rng.random(px.shape) - rng.random(px.shape)), 0.0, 255.0)
    out = bpy.data.images.new("dithered", W, H, alpha=False)
    out.pixels.foreach_set(np.concatenate([lv / 255.0, np.ones((H, W, 1))], axis=-1).astype(np.float32).ravel())
    out.filepath_raw = dst
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    print("DITHER %s from %s: mean level %.3f before, %.3f after (0-255)" % (os.path.basename(dst), os.path.basename(src),
                                                                       255 * float(px.mean()), float(lv.mean())), flush=True)

MASTER = os.path.splitext(OUT)[0] + "-16bit.png"
set_format(sc.render.image_settings, "PNG")
sc.render.image_settings.color_depth = "16"
sc.render.filepath = MASTER
bpy.ops.render.render(write_still=True)
stage("render")
write_dithered(MASTER, OUT, DITHER_SEED)
stage("dither")

try:
    img = bpy.data.images.load(OUT)
    px = np.empty(img.size[0] * img.size[1] * img.channels, np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(-1, img.channels)[:, :3]
    print("PICTURE mean level %.3f, clipped %.2f%% of pixels" % (float(px.mean()), 100.0 * float((px.max(axis=1) > 0.98).mean())),
          flush=True)
except Exception as e:
    print("WARN could not read the picture back:", e, flush=True)
print("DONE", OUT, flush=True)
