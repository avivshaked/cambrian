"""The shared scene of round 67's seven graphics: the water, the shafts, the snow, the grade, the camera in the
design's pixels, the team skins, the props (tank, tube, leaf) and the motion helpers. It builds on theatre_scene.py
(the theatre's creature skin, the intro's and outro's look) and does not fork it.

A scene script (g_tank.py) is run as

    blender -b --factory-startup --python g_tank.py -- <out dir> <percent> <samples> [frames] [--slot NAME] [--t S,S] [--dry]

frames: "all" (the default), a list "1,300,600", or a start and a step "1:2" (every odd frame), as outro.py takes
them; they count the scene's own frames from 1, the scene being a graphic's slots played back to back (cues.json's
"scenes"). --slot keeps only one slot's range of them, and --t takes slot-relative seconds for the stills of a
check. --dry builds the scene and prints the layout check against layout/<frame>.json at the check frames, and
renders nothing. EVOSIM_PREVIEW=eevee renders quick previews in Eevee. A frame already on disk is skipped.

Every body, light and prop is placed by g_layout.Camera.to_world(px, py, depth) at the pixel the design draws it at.
Three custom properties on a body's root, driven into every part, are what an animation keys besides the root's own
transform: `lit` (1 = lit, 0 = dim: the key and rim and the whole shade fall to 35%), `fade` (0-1: toward the water's
colour, for "fades to 40%") and `grey` (0-1: desaturates, for the winter's dead).

A body is one smooth skin, as the film draws it (TheatreOneSkin.cs): add_skin builds a metaball family, one ellipsoid element
per part and one ball per joint, each parented to its part's joint pivot, and g_skin.py holds the arithmetic (k, the balls, the
field's radius and stiffness). EVOSIM_ONE_SKIN=0 draws the old parts and, with bridge=True, the bridges instead.
"""
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector, Matrix, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
CHANNEL = os.path.normpath(os.path.join(HERE, ".."))
for p in (CHANNEL, HERE):
    if p not in sys.path:
        sys.path.insert(0, p)
import theatre_scene as S          # noqa: E402
import g_layout as L               # noqa: E402
import bodies_sheet as BS          # noqa: E402  (numpy only until draw())
import g_skin as SK                # noqa: E402  (numpy only: the one smooth skin's arithmetic)

ONE_SKIN = os.environ.get("EVOSIM_ONE_SKIN", "1") != "0"    # every body one smooth skin, as the film draws it; 0 = the old parts and bridges
SKIN_CELL = 0.004                   # a skin's cell in the body's own metres until place() fits it to the pixel (g_skin.cell_for)

BODIES_FILE = os.path.join(HERE, "bodies-r67.json")
CUES_FILE = os.path.join(HERE, "cues.json")
FPS = 30
EXPOSURE = 0.55                     # the intro's; the water is set 2**-EXPOSURE darker so it lands on the design's hexes
S.BODIES.update(json.load(open(BODIES_FILE, encoding="utf-8")))

lin = S.lin


def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def hexlin(h, gain=1.0):
    return tuple(c * gain for c in lin(hexrgb(h)))


# ------------------------------------------------------------------ arguments and frames
class Args:
    pass


def parse(argv=None):
    argv = list(argv) if argv is not None else sys.argv[sys.argv.index("--") + 1:]
    a = Args()
    a.slot, a.times, a.dry, pos = None, None, False, []
    i = 0
    while i < len(argv):
        x = argv[i]
        if x == "--slot":
            a.slot = argv[i + 1]; i += 2
        elif x == "--t":
            a.times = [float(v) for v in argv[i + 1].split(",")]; i += 2
        elif x == "--dry":
            a.dry = True; i += 1
        else:
            pos.append(x); i += 1
    a.out, a.percent, a.samples = pos[0], int(pos[1]), int(pos[2])
    a.which = pos[3] if len(pos) > 3 else "all"
    return a


def load_cues():
    if not os.path.isfile(CUES_FILE):
        sys.exit("g_scene: cues.json is missing; run `python -I cues.py` (it reads the round's salvage folder)")
    return json.load(open(CUES_FILE, encoding="utf-8"))


def frame_list(args, cues, scene):
    """The scene frames (1-based) to render."""
    n = cues["scenes"][scene]["frames"]
    lo, hi = 1, n
    slot = args.slot
    if slot:
        row = next(r for r in cues["scenes"][scene]["slots"] if r["slot"] == slot)
        lo = row["frame_offset"] + 1
        hi = lo + cues["slots"][slot]["frames"] - 1
    if args.times:
        return [lo + int(round(t * FPS)) for t in args.times]
    w = args.which
    if w == "all":
        return list(range(lo, hi + 1))
    if ":" in w:
        first, step = (int(x) for x in w.split(":"))
        return [f for f in range(first, n + 1, step) if lo <= f <= hi]
    return [int(x) for x in w.split(",")]


# ------------------------------------------------------------------ the render and the camera
class Run:
    """One scene's state: the render settings, the design camera, the anchors and the per-frame loop."""

    def __init__(self, args, cues, scene, cam_model, motion_blur=True):
        self.args, self.cues, self.scene_name = args, cues, scene
        self.N = cues["scenes"][scene]["frames"]
        self.cam_model = cam_model
        os.makedirs(args.out, exist_ok=True)
        S.clear_scene()
        self.sc = S.setup_render(args.percent, args.samples, exposure=EXPOSURE, motion_blur=motion_blur)
        self.sc.frame_start, self.sc.frame_end = 1, self.N
        self.sc.cycles.use_auto_tile = False
        self.sc.cycles.transparent_max_bounces = 128     # a ray through more than 8 fully transparent leaves (the default) ends black
        self.sc.render.fps = FPS
        cd = bpy.data.cameras.new("cam")
        cd.sensor_fit = "VERTICAL"
        cd.sensor_height = L.SENSOR_H
        cd.lens = cam_model.lens
        cd.dof.use_dof = True
        cd.dof.aperture_fstop = cam_model.fstop
        cd.dof.focus_distance = cam_model.focus or 8.0
        cd.clip_start, cd.clip_end = 0.05, 400.0
        self.cam = bpy.data.objects.new("cam", cd)
        self.cam.location = cam_model.pos
        self.cam.rotation_euler = (math.pi / 2, 0.0, 0.0)          # looks along +y, z up
        self.sc.collection.objects.link(self.cam)
        self.sc.camera = self.cam
        self.anchors = {}               # name -> (empty, depth along the view axis is read live)
        self.words = {}
        self.bodies = []
        self.zoom = 1.0
        self.apply_zoom(1.0)

    # a zoom about the middle of the frame: lens and shift scale, the camera stays put
    def zoomed(self, k):
        m = self.cam_model
        pp = (L.W / 2 + (m.pp[0] - L.W / 2) * k, L.H / 2 + (m.pp[1] - L.H / 2) * k)
        return L.Camera(m.lens * k, m.pos, pp, m.fstop, m.focus)

    def apply_zoom(self, k, frame=None):
        z = self.zoomed(k)
        cd = self.cam.data
        cd.lens = z.lens
        cd.shift_x, cd.shift_y = z.shift
        self.zoom = k
        if frame is not None:
            cd.keyframe_insert("lens", frame=frame)
            cd.keyframe_insert("shift_x", frame=frame)
            cd.keyframe_insert("shift_y", frame=frame)

    def focus_at(self, t, frame=None):
        self.cam.data.dof.focus_distance = t
        if frame is not None:
            self.cam.data.dof.keyframe_insert("focus_distance", frame=frame)

    # ---- anchors: an empty per layout point, projected per frame by Blender's own camera
    def anchor(self, name, px, py, t, parent=None):
        e = bpy.data.objects.new("A:" + name, None)
        e.empty_display_size = 0.02
        self.sc.collection.objects.link(e)
        w = self.cam_model.to_world(px, py, t)
        if parent is None:
            e.location = w
        else:
            e.parent = parent
            e.location = (0, 0, 0)
        self.anchors[name] = e
        return e

    def project(self, name):
        """(design px, design py, blur px) of an anchor at the current frame, by Blender's camera."""
        from bpy_extras.object_utils import world_to_camera_view
        e = self.anchors[name]
        co = world_to_camera_view(self.sc, self.cam, e.matrix_world.translation)
        px, py = co.x * L.W, (1.0 - co.y) * L.H
        blur = L.Camera(self.cam.data.lens, (0, 0, 0), fstop=self.cam_model.fstop).coc_px(
            co.z, self.cam.data.dof.focus_distance)
        return px, py, blur

    def projected_at(self, frame):
        self.sc.frame_set(frame)
        return {n: self.project(n) for n in self.anchors}

    def write_anchors(self, frames, extra=None):
        out = {}
        for f in frames:
            out[str(f)] = {n: [round(v, 2) for v in p] for n, p in self.projected_at(f).items()}
        path = os.path.join(self.args.out, "anchors.json")
        json.dump({"fps": FPS, "frames": out, "words": self.words, **(extra or {})}, open(path, "w"))
        return path

    # ---- the layout check, the same arithmetic as the plan's check 1
    def layout_check(self, layout_name, frames, offset=0):
        """Print every anchor's projected pixel at `frames` beside layout/<name>.json. Returns True when all are
        within the file's tolerance."""
        layout = L.load_layout(layout_name)
        allok = True
        for f in frames:
            proj = {n: p[:2] for n, p in self.projected_at(f).items()}
            rows, ok = L.compare(proj, layout, at=round((f - 1 - offset) / FPS, 2))
            allok = allok and ok
            print("LAYOUT %s at frame %d (t %.2f s), tolerance %d px: %s" % (
                layout_name, f, (f - 1 - offset) / FPS, layout.get("tolerance_px", 19), "ok" if ok else "OUT OF TOLERANCE"))
            for name, want, got, miss in rows:
                if got is None:
                    print("  %-12s want (%7.1f, %7.1f)  no such anchor" % (name, want[0], want[1]))
                else:
                    print("  %-12s want (%7.1f, %7.1f)  got (%7.1f, %7.1f)  miss %5.1f px%s" % (
                        name, want[0], want[1], got[0], got[1], miss, "" if miss <= layout.get("tolerance_px", 19) else "  <--"))
        return allok

    # ---- rendering
    def render(self, frames, blend_name):
        a = self.args
        sc = self.sc
        sc.render.image_settings.file_format = "PNG"
        if a.dry:
            print("DRY: scene %s built, %d frames of %d would render: %s" % (
                self.scene_name, len(frames), self.N, frames[:8]))
            return
        if 1 in frames or a.which == "all":
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.out, blend_name))
        if os.environ.get("EVOSIM_PREVIEW") == "eevee":
            for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
                try:
                    sc.render.engine = engine
                    break
                except TypeError:
                    pass
            sc.eevee.taa_render_samples = a.samples
            sc.render.use_motion_blur = False
        for f in frames:
            sc.render.filepath = os.path.join(a.out, "frame-%04d.png" % f)
            if os.path.exists(sc.render.filepath):
                continue
            if ONE_SKIN and any(b.skin is not None for b in self.bodies):
                bake_frame(self, f)
            else:
                sc.frame_set(f)
            bpy.ops.render.render(write_still=True)
            print("RENDERED", f, flush=True)
        print("DONE", flush=True)


# ------------------------------------------------------------------ materials
def mat_new(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes.clear()
    return m


def out_node(nt):
    return nt.nodes.new("ShaderNodeOutputMaterial")


def wrap_dim(mat):
    """Mix the material's surface toward transparency by the object property `dimk` (0 shown, 1 gone)."""
    nt = mat.node_tree
    out = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputMaterial")
    surf = out.inputs["Surface"]
    if not surf.is_linked:
        return mat
    src = surf.links[0].from_socket
    a = nt.nodes.new("ShaderNodeAttribute"); a.attribute_type, a.attribute_name = "OBJECT", "dimk"
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(a.outputs["Fac"], mix.inputs["Fac"])
    nt.links.new(src, mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], surf)
    return mat


def emissive(name, color_hex, strength, alpha=1.0):
    """An emission mixed over transparency: a glow that adds nothing where alpha is 0."""
    m = mat_new(name)
    nt = m.node_tree
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*hexlin(color_hex), 1)
    em.inputs["Strength"].default_value = strength
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    mix.inputs["Fac"].default_value = alpha
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    return wrap_dim(m)


def soft_mat(name, color_hex, strength, alpha):
    """An emission whose alpha is `alpha` at the middle of a facing surface and 0 at its rim (squared), so the edge is soft."""
    m = mat_new(name)
    nt = m.node_tree
    # A flattened ellipsoid faces the camera everywhere, so a facing-based edge gives a hard plate. The falloff is radial in the
    # object's own unit-sphere coordinates instead: alpha = (1 - (x^2 + z^2))^2 x alpha, 0 at the rim, so the glow is soft.
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Object"], sep.inputs[0])
    xx = nt.nodes.new("ShaderNodeMath"); xx.operation = "MULTIPLY"
    nt.links.new(sep.outputs[0], xx.inputs[0]); nt.links.new(sep.outputs[0], xx.inputs[1])
    zz = nt.nodes.new("ShaderNodeMath"); zz.operation = "MULTIPLY"
    nt.links.new(sep.outputs[2], zz.inputs[0]); nt.links.new(sep.outputs[2], zz.inputs[1])
    r2 = nt.nodes.new("ShaderNodeMath"); r2.operation = "ADD"
    nt.links.new(xx.outputs[0], r2.inputs[0]); nt.links.new(zz.outputs[0], r2.inputs[1])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0; inv.use_clamp = True
    nt.links.new(r2.outputs[0], inv.inputs[1])
    pw = nt.nodes.new("ShaderNodeMath"); pw.operation = "POWER"; pw.inputs[1].default_value = 2.0
    nt.links.new(inv.outputs[0], pw.inputs[0])
    k = nt.nodes.new("ShaderNodeMath"); k.operation = "MULTIPLY"; k.inputs[1].default_value = alpha
    nt.links.new(pw.outputs[0], k.inputs[0])
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*hexlin(color_hex), 1)
    em.inputs["Strength"].default_value = strength
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(k.outputs[0], mix.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    return wrap_dim(m)


def veil(name, top_hex, bot_hex, a_top, a_bot, strength=1.0):
    """A glass pane's tint: emission fading in alpha down the object's own height (generated z, 1 at the top)."""
    m = mat_new(name)
    nt = m.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    nt.links.new(sep.outputs[2], ramp.inputs["Value"])
    ramp.inputs["To Min"].default_value, ramp.inputs["To Max"].default_value = a_bot, a_top
    col = nt.nodes.new("ShaderNodeMix")
    col.data_type = "RGBA"
    nt.links.new(sep.outputs[2], col.inputs["Factor"])
    col.inputs["A"].default_value = (*hexlin(bot_hex), 1)
    col.inputs["B"].default_value = (*hexlin(top_hex), 1)
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(col.outputs["Result"], em.inputs["Color"])
    em.inputs["Strength"].default_value = strength
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(ramp.outputs["Result"], mix.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    return m


def glass(name, ior=1.13, mix_fac=0.22):
    """Thin glass: Glass BSDF over transparency, so a pane shows its edges and reflections and not a lens."""
    m = mat_new(name)
    nt = m.node_tree
    g = nt.nodes.new("ShaderNodeBsdfGlass")
    g.inputs["IOR"].default_value = ior
    g.inputs["Roughness"].default_value = 0.02
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    mix.inputs["Fac"].default_value = mix_fac
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(g.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    return m


def _mn(nt, op, a, b, clamp=False):
    """A Math node on sockets or numbers; returns its output socket."""
    n = nt.nodes.new("ShaderNodeMath"); n.operation = op; n.use_clamp = clamp
    for k, x in enumerate((a, b)):
        if x is None:
            continue
        if isinstance(x, (int, float)):
            n.inputs[k].default_value = x
        else:
            nt.links.new(x, n.inputs[k])
    return n.outputs[0]


def volume_emission(name, color_hex, strength, gradient_down=True, bell_pow=1.0, along_pow=1.8, geom=None):
    """A volume that only emits: strength fading along the slab's height, 1 at the top, and a bell across it."""
    m = mat_new(name)
    nt = m.node_tree
    M = lambda op, a=None, b=None, clamp=False: _mn(nt, op, a, b, clamp)
    if geom is None:
        tc = nt.nodes.new("ShaderNodeTexCoord")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Generated"], sep.inputs[0])
        px_, pz_ = sep.outputs[0], sep.outputs[2]
        v = M("MULTIPLY", pz_, 1.0, True)
        u = M("MULTIPLY", M("SUBTRACT", px_, 0.5), 2.0)
    else:
        # world position against the slab's own edges: v 0 at the bottom to 1 at the top, u -1..1 across at that height, so the
        # bell reaches 0 at the slab's sides whatever its taper or lean (Generated coordinates proved flat in a volume).
        g = nt.nodes.new("ShaderNodeNewGeometry")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(g.outputs["Position"], sep.inputs[0])
        cam_ = geom["cam"]                           # divide out the depth, so the slab's sides are the same at every depth in it
        dn = M("SUBTRACT", sep.outputs[1], cam_[1])
        xn = M("DIVIDE", M("SUBTRACT", sep.outputs[0], cam_[0]), dn)
        zn = M("DIVIDE", M("SUBTRACT", sep.outputs[2], cam_[2]), dn)
        v = M("DIVIDE", M("SUBTRACT", zn, geom["zb"]), geom["zt"] - geom["zb"], True)
        xc = M("ADD", geom["xcb"], M("MULTIPLY", v, geom["xct"] - geom["xcb"]))
        xh = M("ADD", geom["xhb"], M("MULTIPLY", v, geom["xht"] - geom["xhb"]))
        u = M("DIVIDE", M("SUBTRACT", xn, xc), M("MAXIMUM", xh, 1e-4))
    along = M("POWER", v, along_pow)
    bell = M("SUBTRACT", 1.0, M("MULTIPLY", u, u), True)
    if bell_pow != 1.0:
        bell = M("POWER", bell, bell_pow)
    mulb = nt.nodes.new("ShaderNodeMath"); mulb.operation = "MULTIPLY"
    nt.links.new(along, mulb.inputs[0]); nt.links.new(bell, mulb.inputs[1])
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*hexlin(color_hex), 1)
    sk = nt.nodes.new("ShaderNodeMath"); sk.operation = "MULTIPLY"; sk.inputs[1].default_value = strength
    nt.links.new(mulb.outputs[0], sk.inputs[0])
    dk = nt.nodes.new("ShaderNodeAttribute"); dk.attribute_type, dk.attribute_name = "OBJECT", "dimk"
    keep = nt.nodes.new("ShaderNodeMath"); keep.operation = "SUBTRACT"; keep.inputs[0].default_value = 1.0
    nt.links.new(dk.outputs["Fac"], keep.inputs[1])
    kd = nt.nodes.new("ShaderNodeMath"); kd.operation = "MULTIPLY"
    nt.links.new(sk.outputs[0], kd.inputs[0]); nt.links.new(keep.outputs[0], kd.inputs[1])
    nt.links.new(kd.outputs[0], em.inputs["Strength"])
    nt.links.new(em.outputs[0], out_node(nt).inputs["Volume"])
    return m


def link_obj(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def mesh_obj(name, verts, faces, material, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.update()
    for poly in me.polygons:
        poly.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    if material is not None:
        ob.data.materials.append(material)
    ob.visible_shadow = False
    return link_obj(ob)


# ------------------------------------------------------------------ the water
WATER_STOPS = [(0.0, "#14596a"), (0.45, "#0a3746"), (1.0, "#031821")]


def water_gradient(strength=None):
    """The design's water: #14596a at the top of the frame to #0a3746 at 45% to #031821 at the bottom, with the
    surface glow's radial #5fb6c4 (cx .5, cy -.15, r .75) over it, in camera-window space. Set 2**-EXPOSURE darker
    to land on the design's hexes after the view exposure."""
    k = strength if strength is not None else 2.0 ** -EXPOSURE
    sc = bpy.context.scene
    world = bpy.data.worlds.new("water")
    sc.world = world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Window"], sep.inputs[0])
    down = nt.nodes.new("ShaderNodeMath")                 # 0 at the top of the frame, 1 at the bottom
    down.operation = "SUBTRACT"
    down.inputs[0].default_value = 1.0
    nt.links.new(sep.outputs[1], down.inputs[1])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = 0.0, (*hexlin(WATER_STOPS[0][1], k), 1)
    for pos, h in WATER_STOPS[1:]:
        el = ramp.color_ramp.elements.new(pos)
        el.color = (*hexlin(h, k), 1)
    ramp.color_ramp.interpolation = "LINEAR"
    nt.links.new(down.outputs[0], ramp.inputs["Fac"])
    # the surface glow: an ellipse in window space
    dx = nt.nodes.new("ShaderNodeMath"); dx.operation = "SUBTRACT"; dx.inputs[1].default_value = 0.5
    nt.links.new(sep.outputs[0], dx.inputs[0])
    dy = nt.nodes.new("ShaderNodeMath"); dy.operation = "SUBTRACT"; dy.inputs[1].default_value = 1.15
    nt.links.new(sep.outputs[1], dy.inputs[0])
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    sx = nt.nodes.new("ShaderNodeMath"); sx.operation = "DIVIDE"; sx.inputs[1].default_value = 0.75
    sy = nt.nodes.new("ShaderNodeMath"); sy.operation = "DIVIDE"; sy.inputs[1].default_value = 0.75
    nt.links.new(dx.outputs[0], sx.inputs[0]); nt.links.new(dy.outputs[0], sy.inputs[0])
    nt.links.new(sx.outputs[0], comb.inputs[0]); nt.links.new(sy.outputs[0], comb.inputs[1])
    ln = nt.nodes.new("ShaderNodeVectorMath"); ln.operation = "LENGTH"
    nt.links.new(comb.outputs[0], ln.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0; inv.use_clamp = True
    nt.links.new(ln.outputs["Value"], inv.inputs[1])
    glow = nt.nodes.new("ShaderNodeMath"); glow.operation = "MULTIPLY"; glow.inputs[1].default_value = 0.55
    nt.links.new(inv.outputs[0], glow.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"
    nt.links.new(glow.outputs[0], mix.inputs["Factor"])
    nt.links.new(ramp.outputs["Color"], mix.inputs["A"])
    mix.inputs["B"].default_value = (*hexlin("#5fb6c4", k), 1)
    bg = nt.nodes.new("ShaderNodeBackground")
    nt.links.new(mix.outputs["Result"], bg.inputs["Color"])
    wo = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(bg.outputs[0], wo.inputs["Surface"])
    return world


def shafts(run, seed=11, depth=18.0, thickness=3.0, gain=0.45):
    """water.js's seven rays: the LCG's x, width and lean per ray, drawn as hexahedra of emission far behind the
    scene, top at design y -40 to the bottom at 1100, fading downward, at the design's opacity 0.22 x (0.5-1)."""
    r = L.lcg(seed)
    cam = run.cam_model
    out = []
    for i in range(7):
        x, w, lean = 200 + r() * 1500, 60 + r() * 140, -180 + r() * 120
        op = 0.5 + r() * 0.5
        if x < 700:
            w *= 0.5          # the design's left shafts are narrow
        top = [(x, -40), (x + w, -40)]
        bot = [(x + w * 2.2 + lean, 1100), (x + lean, 1100)]
        out.append(slab(run, "ray%d" % i, top, bot, depth, thickness, "#d8fffb", 0.22 * op * gain / thickness))
    # far, out-of-focus leaves and swimmers: depth without detail (water.js: 9 ellipses, 45% opacity, blur 30)
    for i in range(9):
        x, y, sz = r() * 1920, 120 + r() * 800, 30 + r() * 60
        green = r() < 0.7
        if x < 960 and y > 540:
            continue          # the queue meets the tank there; nothing may pull the eye from it
        out.append(blob(run, "far%d" % i, x, y, 45.0, sz, sz * 0.6, "#3f9e60" if green else "#4fc9b8", 0.4, 0.5))
    return out


def blob(run, name, px, py, t, rx_px, ry_px, color_hex, strength, alpha, rot_deg=0.0):
    """A soft emissive ellipse of the design's size at a pixel and depth; the depth of field blurs it."""
    m = run.cam_model.metres_per_px(t)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(soft_mat("blob-" + name, color_hex, strength, alpha))
    ob.visible_shadow = False
    link_obj(ob)
    ob.location = run.cam_model.to_world(px, py, t)
    ob.scale = (rx_px * m, 0.05 * rx_px * m, ry_px * m)
    ob.rotation_euler = (0.0, math.radians(rot_deg), 0.0)
    return ob


def slab(run, name, top_px, bot_px, depth, thickness, color_hex, strength, bell_pow=1.0, along_pow=1.8):
    """A hexahedron of volume emission: its top edge (two design pixels) to its bottom edge, at depths
    depth +- thickness/2, fading from `strength` at the top to 0 at the bottom."""
    cam = run.cam_model
    pts = []
    for t in (depth - thickness / 2, depth + thickness / 2):
        for (px, py) in top_px + bot_px:
            pts.append(cam.to_world(px, py, t))
    # vertex order per depth: top0, top1, bot1, bot0
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    TL, TR = cam.to_world(*top_px[0], depth), cam.to_world(*top_px[1], depth)
    BL, BR = cam.to_world(*bot_px[1], depth), cam.to_world(*bot_px[0], depth)       # bot_px runs right to left
    cp = cam.pos
    nx = lambda P: (P[0] - cp[0]) / depth
    nz = lambda P: (P[2] - cp[2]) / depth
    geom = {"cam": cp, "zt": nz(TL), "zb": nz(BL), "xct": (nx(TL) + nx(TR)) / 2, "xht": abs(nx(TR) - nx(TL)) / 2,
            "xcb": (nx(BL) + nx(BR)) / 2, "xhb": abs(nx(BR) - nx(BL)) / 2}
    ob = mesh_obj(name, pts, faces, volume_emission("m-" + name, color_hex, strength, bell_pow=bell_pow, along_pow=along_pow, geom=geom), smooth=False)
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)      # a volume needs a closed mesh with outward normals
    bm.to_mesh(ob.data); bm.free()
    return ob


def snow(run, rng, count=1100, strength=1.2, depth=(4.0, 30.0)):
    """Marine snow over the view frustum: theatre_scene.marine_snow in a box that covers the frame between the
    depths given."""
    cam = run.cam_model
    far = depth[1]
    half_x = L.W / 2 / cam.f_px * far * 1.6
    half_z = L.H / 2 / cam.f_px * far * 1.6
    box = ((-half_x, half_x), (cam.pos[1] + depth[0], cam.pos[1] + depth[1]), (cam.pos[2] - half_z, cam.pos[2] + half_z))
    # the camera's own principal point is far off axis in these frames; centre the box on the frame's middle ray
    return S.marine_snow(rng, count=count, strength=strength, box=box)


# ------------------------------------------------------------------ bodies and their skins
# The taleplum rows are sampled off the designer's frames (frames-v5/founder-1..3.png, the founder at about (1146-1281, 486-590);
# the same colours in all three): the disc's pale gaps #a0beb3 (lightest quarter of its pixels in x 1210-1265, y 500-580), its mid
# #6c8a77 and its olive lines #52674c (darkest quarter). The pink tip sampled #d5bcce / #a08aa1 / #776781 (pixels (1180-1210,
# 515-545)) but the blue dots on it wash the mean toward lilac, so the tip keeps the design's own hexes, which read pink in the frame.
# The frame is darker than the design's hexes by about the factor measured on the amber Guzzler beside it in founder-2 (sampled
# #cfb588 / #937345 / #63431b against the design's #ffeccb / #e0a24a / #86561a, about 0.76 / 0.67 / 0.75), so the sampled
# values, scaled up 1.30 / 1.45 / 1.35, rendered pale blue-white under the scene's light, so the rows are now the sampled hexes
# themselves with the mid pulled toward the olive of its lines (#7a9a76), and the maze pigment is multiplied by TILE_TINT.
TEAM = {   # light, mid, shadow: rim, through, base on the theatre's props
    "nibbler": ("#e2fff9", "#8ae9da", "#2a9d95"),
    "guzzler": ("#ffeccb", "#e0a24a", "#86561a"),
    "pale": ("#e2fff9", "#8ae9da", "#2a9d95"),           # the parents graphic's Guzzlers, claiming no team
    "taleplum": ('#a0beb3', '#7a9a76', '#52674c'),   # sampled off frames-v5/founder-2.png, see the note under TEAM
    "taleplum-tip": ("#ffe0ec", "#d78fae", "#7f4a68"),    # the founder's spotted tip (design creatures.js "shell")
    "bone": ("#fffbe8", "#e8e2c4", "#9c9a80"),            # the queue's variety, after creatures.js's fills
    "yellow": ("#fbffc0", "#e2e85a", "#8f9a2a"),
    "brown": ("#c9a27a", "#8a6540", "#4a3320"),
}
FIELDS = ("lit", "fade", "grey")


def patch_state(mat, fade_hex="#0a3746", lit_floor=0.35, lit_ceil=1.0):
    """Adds the three keyable controls in front of the body material's fog: lit scales the light (0..1 -> lit_floor..lit_ceil),
    grey desaturates, fade mixes toward the water."""
    nt = mat.node_tree
    mix = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMix")
    src = mix.inputs["B"].links[0].from_socket
    attr = {}
    for k in FIELDS:
        a = nt.nodes.new("ShaderNodeAttribute")
        a.attribute_type, a.attribute_name = "OBJECT", k
        attr[k] = a.outputs["Fac"]
    mr = nt.nodes.new("ShaderNodeMapRange")                 # lit 0..1 -> lit_floor..1
    mr.inputs["To Min"].default_value, mr.inputs["To Max"].default_value = lit_floor, lit_ceil
    nt.links.new(attr["lit"], mr.inputs["Value"])
    scale = nt.nodes.new("ShaderNodeVectorMath"); scale.operation = "SCALE"
    nt.links.new(src, scale.inputs[0]); nt.links.new(mr.outputs["Result"], scale.inputs[3])
    lum = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(scale.outputs["Vector"], lum.inputs[0])
    gmix = nt.nodes.new("ShaderNodeMix"); gmix.data_type = "RGBA"
    nt.links.new(attr["grey"], gmix.inputs["Factor"])
    nt.links.new(scale.outputs["Vector"], gmix.inputs["A"]); nt.links.new(lum.outputs[0], gmix.inputs["B"])
    fmix = nt.nodes.new("ShaderNodeMix"); fmix.data_type = "RGBA"
    nt.links.new(attr["fade"], fmix.inputs["Factor"])
    nt.links.new(gmix.outputs["Result"], fmix.inputs["A"])
    fmix.inputs["B"].default_value = (*hexlin(fade_hex), 1)
    nt.links.new(fmix.outputs["Result"], mix.inputs["B"])


def part_objects(name):
    prefix = name + "-"
    return [o for o in bpy.data.objects if o.name.startswith(prefix) and "rim" in o.keys()]


BASE_DARKEN = 0.5      # the intro's bodies carry a base about half the design's shadow hex in sRGB: the key lights it


def hexlin_scaled(h, k):
    return lin(tuple(c * k for c in hexrgb(h)))


def skin(name, team):
    """The team's hexes on the theatre's props: the rim and the light through the tissue are the design's mid hex (the
    intro's guild colours are mid-valued too), the base is its shadow hex darkened as the theatre's bases are."""
    light, mid, shadow = TEAM[team]
    base = hexlin_scaled(shadow, BASE_DARKEN)
    for o in part_objects(name):
        b = 0.85 if o.name[len(name) + 1:].startswith("k") else 1.0
        o["rim"] = tuple(c * b for c in hexlin(mid))
        o["trans_tint"] = hexlin(mid)
        o["base"] = tuple(c * b for c in base)


TILE = {"nibbler": "net-cyan.png", "pale": "net-cyan.png", "guzzler": "stripes-amber.png", "taleplum": "maze-pale.png", "taleplum-tip": "spots-pink.png",
        "bone": "spots-bone.png", "yellow": "stripes-yellow.png", "brown": "stripes-amber.png"}
PATTERN_OPACITY = 0.9      # the theatre's PatternOpacity
TILE_TINT = {"taleplum": (0.62, 0.70, 0.46)}
# The skin path's markings. The tile repeats every 1/scale metres: 2.2 gave the Guzzlers' stripes and the Nibblers' net a period about
# three times finer than the design's (measured on the founder-1 and parents-1 frames), which a 960-wide render averages to flat colour,
# so they take 0.75; the taleplum's maze reads at 2.2 and keeps it. PATTERN_DARK: the share of the whole light the cover removes, for
# the teams whose pigment is darker than the body (the stripes, the net's dots); the additive terms (light through the tissue, rim,
# glow) otherwise fill the markings in, because the tile only ever tinted the albedo.
PATTERN_SCALE = {"guzzler": 0.75, "brown": 0.75, "yellow": 0.75, "nibbler": 0.8, "pale": 0.8, "bone": 0.8}
PATTERN_DARK = {"guzzler": 0.55, "brown": 0.55, "yellow": 0.5, "nibbler": 0.6, "pale": 0.6, "bone": 0.4}
# The lit ceiling (lit 1 -> this, of the full light) for the cyan teams: lit 1 put a cyan skin's rim and light through the tissue past
# 1.0 on both green and blue, which reads white, where an amber one only reddens. The dim end (lit 0 -> 0.35) is unchanged.
LIT_CEIL = {"pale": 0.55, "nibbler": 0.55}


def add_pattern(mat, team, scale=2.2, dark=0.0):
    """The design's own solved tile (alpha = cover, colour = pigment) as a Box-projected image in object coordinates,
    mixing the base toward the pigment by cover x 0.9, as the theatre does."""
    nt = mat.node_tree
    base_node = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeAttribute" and n.attribute_name == "base")
    targets = [l.to_socket for l in base_node.outputs["Vector"].links]
    for l in list(base_node.outputs["Vector"].links):
        nt.links.remove(l)
    img = bpy.data.images.load(os.path.join(HERE, "skins", TILE[team]), check_existing=True)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sc = nt.nodes.new("ShaderNodeVectorMath"); sc.operation = "SCALE"
    nt.links.new(tc.outputs["Object"], sc.inputs[0]); sc.inputs[3].default_value = scale
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image, tex.projection, tex.projection_blend, tex.extension = img, "BOX", 0.2, "REPEAT"
    nt.links.new(sc.outputs["Vector"], tex.inputs["Vector"])
    k = nt.nodes.new("ShaderNodeMath"); k.operation = "MULTIPLY"; k.inputs[1].default_value = PATTERN_OPACITY
    nt.links.new(tex.outputs["Alpha"], k.inputs[0])
    mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"
    nt.links.new(k.outputs[0], mx.inputs["Factor"])
    nt.links.new(base_node.outputs["Vector"], mx.inputs["A"])
    if team in TILE_TINT:      # the taleplum's pale gaps are olive-cream in the design, not mint: multiply the pigment
        tn = nt.nodes.new("ShaderNodeMix"); tn.data_type = "RGBA"; tn.blend_type = "MULTIPLY"
        tn.inputs["Factor"].default_value = 1.0
        nt.links.new(tex.outputs["Color"], tn.inputs["A"]); tn.inputs["B"].default_value = (*TILE_TINT[team], 1)
        nt.links.new(tn.outputs["Result"], mx.inputs["B"])
    else:
        nt.links.new(tex.outputs["Color"], mx.inputs["B"])
    for t in targets:
        nt.links.new(mx.outputs["Result"], t)
    if dark > 0:       # the cover darkens the whole light, not just the albedo
        fog = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMix" and n.outputs["Result"].links
                   and n.outputs["Result"].links[0].to_node.bl_idname == "ShaderNodeEmission")
        b_in = _mix_in(fog, "B")
        src = b_in.links[0].from_socket
        m1 = nt.nodes.new("ShaderNodeMath"); m1.operation = "MULTIPLY"; m1.inputs[1].default_value = -dark
        nt.links.new(k.outputs[0], m1.inputs[0])
        m2 = nt.nodes.new("ShaderNodeMath"); m2.operation = "ADD"; m2.inputs[1].default_value = 1.0
        nt.links.new(m1.outputs[0], m2.inputs[0])
        dk = nt.nodes.new("ShaderNodeVectorMath"); dk.operation = "SCALE"
        nt.links.new(src, dk.inputs[0]); nt.links.new(m2.outputs[0], dk.inputs[3])
        nt.links.new(dk.outputs[0], b_in)
    return {"tex": tex, "cover": k, "mix": mx, "coords": sc}


def _mix_in(node, name):
    """The enabled input of a Mix node by name (a Mix node holds a float, a vector and a colour socket of each name)."""
    return next(s for s in node.inputs if s.name == name and s.enabled)


def _relink(nt, out_socket, new_socket):
    """Everything that read `out_socket` reads `new_socket` instead; returns the targets."""
    targets = [l.to_socket for l in out_socket.links]
    for l in list(out_socket.links):
        nt.links.remove(l)
    for t in targets:
        nt.links.new(new_socket, t)
    return targets


def skin_surface(mat):
    """Makes a body material fit a metaball skin, which has no vertex attributes and whose object coordinates are the family's
    own (U times the body's metres, U = FIELD_CELL / cell): the geometry colour attributes `evo_a` and `evo_tone` become the
    object's `skin_thick` and a constant, and every Object texture coordinate is scaled by the object's `skin_s` (1/U) so the
    markings and the mottle are in the body's metres whatever the cell."""
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.bl_idname == "ShaderNodeAttribute" and n.attribute_type == "GEOMETRY":
            if n.attribute_name == "evo_a":
                th = nt.nodes.new("ShaderNodeAttribute"); th.attribute_type, th.attribute_name = "OBJECT", "skin_thick"
                c = nt.nodes.new("ShaderNodeCombineXYZ")
                nt.links.new(th.outputs["Fac"], c.inputs[0])
                c.inputs[1].default_value = 1.0; c.inputs[2].default_value = 1.0
                _relink(nt, n.outputs["Vector"], c.outputs[0])
                nt.nodes.remove(n)
            elif n.attribute_name == "evo_tone":
                rgb = nt.nodes.new("ShaderNodeRGB")
                rgb.outputs[0].default_value = (1.0, 1.0, 1.0, 1.0)
                _relink(nt, n.outputs["Vector"], rgb.outputs[0])
                nt.nodes.remove(n)
    for tc in [n for n in nt.nodes if n.bl_idname == "ShaderNodeTexCoord"]:
        if not tc.outputs["Object"].links:
            continue
        s = nt.nodes.new("ShaderNodeAttribute"); s.attribute_type, s.attribute_name = "OBJECT", "skin_s"
        sc = nt.nodes.new("ShaderNodeVectorMath"); sc.operation = "SCALE"
        targets = [l.to_socket for l in tc.outputs["Object"].links]
        for l in list(tc.outputs["Object"].links):
            nt.links.remove(l)
        nt.links.new(tc.outputs["Object"], sc.inputs[0]); nt.links.new(s.outputs["Fac"], sc.inputs[3])
        for t in targets:
            nt.links.new(sc.outputs[0], t)
    return mat


def add_tip(mat, info, tip_team="taleplum-tip"):
    """Makes a body material two-toned: the tip part's own pigment tile (spots) and colours (`base2`, `rim2`, `trans_tint2`)
    take over on the tip part's ellipsoid, a smoothstep of its normalised distance q (1 below TIP_BLEND[0], 0 beyond
    TIP_BLEND[1]). q comes from the object's `tipr0..2` and `tipd`, an affine map from the body's metres to the tip's ellipsoid
    coordinates that the body re-keys every swim, so the pink goes where the tip goes. Call after skin_surface."""
    nt = mat.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    s = nt.nodes.new("ShaderNodeAttribute"); s.attribute_type, s.attribute_name = "OBJECT", "skin_s"
    m = nt.nodes.new("ShaderNodeVectorMath"); m.operation = "SCALE"
    nt.links.new(tc.outputs["Object"], m.inputs[0]); nt.links.new(s.outputs["Fac"], m.inputs[3])
    d = nt.nodes.new("ShaderNodeAttribute"); d.attribute_type, d.attribute_name = "OBJECT", "tipd"
    dsep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(d.outputs["Vector"], dsep.inputs[0])
    sq = None
    for i in range(3):
        r = nt.nodes.new("ShaderNodeAttribute"); r.attribute_type, r.attribute_name = "OBJECT", "tipr%d" % i
        dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
        nt.links.new(m.outputs[0], dot.inputs[0]); nt.links.new(r.outputs["Vector"], dot.inputs[1])
        u = nt.nodes.new("ShaderNodeMath"); u.operation = "ADD"
        nt.links.new(dot.outputs["Value"], u.inputs[0]); nt.links.new(dsep.outputs[i], u.inputs[1])
        u2 = nt.nodes.new("ShaderNodeMath"); u2.operation = "MULTIPLY"
        nt.links.new(u.outputs[0], u2.inputs[0]); nt.links.new(u.outputs[0], u2.inputs[1])
        if sq is None:
            sq = u2.outputs[0]
        else:
            a = nt.nodes.new("ShaderNodeMath"); a.operation = "ADD"
            nt.links.new(sq, a.inputs[0]); nt.links.new(u2.outputs[0], a.inputs[1])
            sq = a.outputs[0]
    q = nt.nodes.new("ShaderNodeMath"); q.operation = "SQRT"; nt.links.new(sq, q.inputs[0])
    mr = nt.nodes.new("ShaderNodeMapRange"); mr.data_type, mr.interpolation_type, mr.clamp = "FLOAT", "SMOOTHSTEP", True
    nt.links.new(q.outputs[0], mr.inputs["Value"])
    mr.inputs["From Min"].default_value, mr.inputs["From Max"].default_value = SK.TIP_BLEND[1], SK.TIP_BLEND[0]
    mr.inputs["To Min"].default_value, mr.inputs["To Max"].default_value = 0.0, 1.0
    mask = mr.outputs["Result"]
    # the tile: the tip's own pigment and cover over the body's
    img = bpy.data.images.load(os.path.join(HERE, "skins", TILE[tip_team]), check_existing=True)
    tex = info["tex"]
    tex2 = nt.nodes.new("ShaderNodeTexImage")
    tex2.image, tex2.projection, tex2.projection_blend, tex2.extension = img, "BOX", 0.2, "REPEAT"
    nt.links.new(info["coords"].outputs[0], tex2.inputs["Vector"])
    cov = nt.nodes.new("ShaderNodeMix"); cov.data_type = "FLOAT"
    nt.links.new(mask, _mix_in(cov, "Factor"))
    nt.links.new(tex.outputs["Alpha"], _mix_in(cov, "A")); nt.links.new(tex2.outputs["Alpha"], _mix_in(cov, "B"))
    nt.links.new(cov.outputs["Result"], info["cover"].inputs[0])
    mx = info["mix"]
    pig = nt.nodes.new("ShaderNodeMix"); pig.data_type = "RGBA"
    nt.links.new(mask, _mix_in(pig, "Factor"))
    old = _mix_in(mx, "B").links[0].from_socket
    nt.links.new(old, _mix_in(pig, "A")); nt.links.new(tex2.outputs["Color"], _mix_in(pig, "B"))
    nt.links.new(pig.outputs["Result"], _mix_in(mx, "B"))
    # the colours: base, rim and the light through the tissue, by the same mask
    for name in ("base", "rim", "trans_tint"):
        for n in [n for n in nt.nodes if n.bl_idname == "ShaderNodeAttribute" and n.attribute_type == "OBJECT" and n.attribute_name == name]:
            if not n.outputs["Vector"].links:
                continue
            n2 = nt.nodes.new("ShaderNodeAttribute"); n2.attribute_type, n2.attribute_name = "OBJECT", name + "2"
            c = nt.nodes.new("ShaderNodeMix"); c.data_type = "RGBA"
            nt.links.new(mask, _mix_in(c, "Factor"))
            targets = [l.to_socket for l in n.outputs["Vector"].links]
            for l in list(n.outputs["Vector"].links):
                nt.links.remove(l)
            nt.links.new(n.outputs["Vector"], _mix_in(c, "A")); nt.links.new(n2.outputs["Vector"], _mix_in(c, "B"))
            for t in targets:
                nt.links.new(c.outputs["Result"], t)
    return mat


def drive_state(root, name):
    """lit, fade and grey live on the root and drive every part."""
    STATE = (("lit", 1.0), ("fade", 0.0), ("grey", 0.0), ("dimk", 0.0))      # dimk: 0 shown, 1 transparent (wrap_dim)
    for k, v in STATE:
        root[k] = v
    for o in part_objects(name):
        for k, v in STATE:
            o[k] = v
            fc = o.driver_add('["%s"]' % k)
            d = fc.driver
            d.type = "AVERAGE"
            var = d.variables.new()
            var.name = "v"
            var.type = "SINGLE_PROP"
            var.targets[0].id = root
            var.targets[0].data_path = '["%s"]' % k


def body_length(key):
    """Core's z extent of a body (its long axis, head to tail), in metres at scale 1."""
    lo, hi = BS.extent(S.BODIES[key])
    return float(hi[2] - lo[2])


class Body:
    def __init__(self, key, name, team, material, root, joints, length, scale, skin=None, run=None, tip=None):
        self.key, self.name, self.team, self.root, self.joints = key, name, team, root, joints
        self.length, self.scale = length, scale
        self.wave = (14.0, 0.45, 0.0)
        self.skin, self.run, self.tip = skin, run, tip        # the metaball skin's basis object, the scene, the pink tip's chain
        self._cell = None
        self.area = SK.surface_area(S.BODIES[key]["parts"])
        root.rotation_mode = "QUATERNION"

    def place(self, pos, yaw_deg, roll_deg=0.0, scale=None, frame=None):
        """Facing is yaw about z: the tail is body +y, so -90 faces +x (head right) when the camera sees it side on."""
        r = self.root
        r.location = pos
        r.rotation_quaternion = Quaternion((0, 1, 0), math.radians(roll_deg)) @ Quaternion((0, 0, 1), math.radians(yaw_deg))
        s = scale if scale is not None else self.scale
        r.scale = (s, s, s)
        if frame is not None:
            r.keyframe_insert("location", frame=frame)
            r.keyframe_insert("rotation_quaternion", frame=frame)
            r.keyframe_insert("scale", frame=frame)
        self.fit_cell(pos, s)

    def fit_cell(self, pos, scale):
        """The skin's cell: 0.5 of a render pixel at the body's depth, and no finer than the vertex budget allows (g_skin.cell_for);
        the finest it has been placed at stands."""
        if self.skin is None or self.run is None:
            return
        m = self.run.cam_model
        r = self.run.sc.render
        ratio = L.W / max(r.resolution_x * r.resolution_percentage / 100.0, 1.0)
        c = SK.cell_for(float(pos[1]) - float(m.pos[1]), m.f_px, float(scale), area=self.area, px_ratio=ratio)
        self._cell = c if self._cell is None else min(self._cell, c)
        set_skin_cell(self.skin, self._cell)

    def swim(self, t, frame, amp_scale=1.0):
        A, hz, ph = self.wave
        w = 2 * math.pi * hz
        for j in self.joints:
            ang = math.radians(A * amp_scale) * math.sin(w * t + ph - 0.9 * j["depth"] + j["jitter"])
            j["pivot"].rotation_quaternion = Quaternion(Vector(j["axis"]), ang)
            j["pivot"].keyframe_insert("rotation_quaternion", frame=frame)
        if self.tip is not None:
            self.key_tip(frame)

    def key_tip(self, frame=None):
        """Re-aims the skin's pink mask at the tip part's pivots as they stand now (and keys it)."""
        tp = self.tip
        chain = [(tuple(p.location), tuple(p.rotation_quaternion)) for p in tp["pivots"]]
        rows, off = SK.tip_rows(tp["parts"], tp["index"], chain, tp["element"])
        for i in range(3):
            self.skin["tipr%d" % i] = [float(x) for x in rows[i]]
        self.skin["tipd"] = [float(x) for x in off]
        if frame is not None:
            for k in ("tipr0", "tipr1", "tipr2", "tipd"):
                self.skin.keyframe_insert('["%s"]' % k, frame=frame)

    def state(self, frame, lit=None, fade=None, grey=None):
        for k, v in (("lit", lit), ("fade", fade), ("grey", grey)):
            if v is not None:
                self.root[k] = v
                self.root.keyframe_insert('["%s"]' % k, frame=frame)


def add_bridges(key, name, radius_k=0.7):
    """A bridging skin between every part and its parent, so a body reads as one rounded body (the theatre's own build draws a
    knuckle at each joint and nothing across the gap between two parts that do not touch, as the Guzzler's two spheres do).
    Two capsules meet at the joint's anchor: one to the child's centre, riding the child's pivot, one to the parent's centre,
    riding the parent's pivot, so the join bends with the joint. Radius radius_k x the smaller of the two parts' smallest
    half-extents. It takes the child part's material and properties (and so its skin colours and the lit/fade/grey/dimk
    drivers, which drive every object named <name>-... that carries a `rim`). Nothing here moves an anchor."""
    import numpy as np
    from mathutils import Matrix
    parts = S.BODIES[key]["parts"]
    centre = np.mean([p["pos"] for p in parts], axis=0)
    tb = lambda v: np.asarray(v, float)[[0, 2, 1]]
    made = []

    def capsule(label, pivot_name, offset, a, b, r, src):
        a, b = tb(a) - offset, tb(b) - offset
        d = b - a
        L = float(np.linalg.norm(d))
        if L < 1e-5 or r < 1e-4:
            return
        bm = bmesh.new()
        mid = (a + b) / 2
        q = Vector(d / L).to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=r, radius2=r, depth=L,
                              matrix=Matrix.Translation(Vector(mid)) @ q)
        for end in (a, b):
            bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r, matrix=Matrix.Translation(Vector(end)))
        me = bpy.data.meshes.new(label)
        bm.to_mesh(me); bm.free()
        for poly in me.polygons:
            poly.use_smooth = True
        for cname in ("evo_a", "evo_tone"):
            sa = src.data.color_attributes.get(cname)
            ca = me.color_attributes.new(cname, "FLOAT_COLOR", "POINT")
            c0 = tuple(sa.data[0].color) if sa is not None and len(sa.data) else (0.5, 0.5, 0.5, 1.0)
            ca.data.foreach_set("color", list(c0) * len(me.vertices))
        ob = bpy.data.objects.new(label, me)
        ob.data.materials.append(src.material_slots[0].material if src.material_slots else None)
        for k in src.keys():
            if k in ("lit", "fade", "grey", "dimk"):
                continue
            ob[k] = src[k]
        ob.parent = bpy.data.objects[pivot_name]
        ob.visible_shadow = False
        bpy.context.scene.collection.objects.link(ob)
        made.append(ob)

    for p in parts:
        if p["parent"] < 0:
            continue
        par = parts[p["parent"]]
        srcs = [o for o in bpy.data.objects if o.name.startswith("%s-p%d-" % (name, p["i"])) and "rim" in o.keys()]
        if not srcs:
            continue
        src = srcs[0]
        r = radius_k * min(min(p["half"]), min(par["half"]))
        J = np.asarray(p["anchor"], float) - centre
        cc = np.asarray(p["pos"], float) - centre
        pc = np.asarray(par["pos"], float) - centre
        child_off = tb(J)
        par_off = tb(np.asarray(par["anchor"] if par["parent"] >= 0 else par["pos"], float) - centre)
        capsule("%s-b%d-c" % (name, p["i"]), "%s-joint%d" % (name, p["i"]), child_off, J, cc, r, src)
        capsule("%s-b%d-p" % (name, p["i"]), "%s-joint%d" % (name, par["i"]), par_off, J, pc, r, src)
    return made


BAKE_MARGIN = 0.6          # a body is kept for a frame while its centre is within this many body lengths of the picture


def body_visible(run, b):
    """Whether a body's skin is worth making for the frame now set: its root is not shrunk away (the scenes hide a body at scale
    1e-4), its `dimk` is not 1, and it is not far off the picture. Read from the original objects after frame_set."""
    from bpy_extras.object_utils import world_to_camera_view
    r = b.root
    if r.scale[0] < 1e-3 or r.get("dimk", 0.0) >= 0.999:
        return False
    co = world_to_camera_view(run.sc, run.cam, r.matrix_world.translation)
    if co.z <= 0.0:
        return False
    px, py = co.x * L.W, (1.0 - co.y) * L.H
    f_px = run.cam.data.lens / L.SENSOR_H * L.H
    reach = BAKE_MARGIN * b.length * r.scale[0] * f_px / co.z + 200.0       # design pixels, with room for the defocus
    return -reach <= px <= L.W + reach and -reach <= py <= L.H + reach


def bake_frame(run, f):
    """Sets frame f and makes every visible body's skin a plain mesh for it, so the renderer sees meshes and never a metaball.
    Polygonising is the cost of a skin (about 1.5 us a vertex) and Blender does it only when a skin's mesh is asked for, so it is
    paid here, once, for the bodies the frame shows and no others. Every metaball object is off in the render, where a family
    would be polygonised again (and, in the 10-minute still, was). Each baked mesh rides its body's root, so the root's motion still
    blurs; the bend inside the skin does not (a mesh whose vertex count changes between steps cannot be deformation-blurred
    anyway). Returns (bodies baked, bodies with a skin)."""
    sc = run.sc
    bodies = [b for b in run.bodies if b.skin is not None]
    if not getattr(run, "_metaballs_off", False):
        for o in bpy.data.objects:
            if o.type == "META":
                o.hide_render = True
        run._metaballs_off = True
    for b in bodies:
        old = bpy.data.objects.get(b.name + "-bake")
        if old is not None:
            me = old.data
            bpy.data.objects.remove(old, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)
    sc.frame_set(f)
    shown = [b for b in bodies if body_visible(run, b)]
    dg = bpy.context.evaluated_depsgraph_get()
    for b in shown:
        ev = b.skin.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        me.name = b.name + "-bake"
        me.materials.clear()
        me.materials.append(b.skin.material_slots[0].material)
        ob = bpy.data.objects.new(b.name + "-bake", me)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = b.root
        ob.scale = b.skin.scale
        for k in ev.keys():
            v = ev[k]
            ob[k] = list(v) if hasattr(v, "__len__") and not isinstance(v, str) else v
    return len(shown), len(bodies)


def set_skin_cell(basis, cell):
    """The metaball cell, in the body's own metres: FIELD_CELL is Blender's floor (0.005 in the family's units), so the basis
    object is scaled to 1/U and the elements, seen from it, are U larger. `skin_s` is the same 1/U, for the shader's coordinates."""
    s = SK.basis_scale(cell)
    basis.scale = (s, s, s)
    basis["skin_s"] = s


def add_skin(key, name, root, material, tip_index=None, cell=SKIN_CELL):
    """The body's one smooth skin (g_skin.plan): a metaball family named <name>-skin whose basis object, parented to the body root,
    carries the mesh, the material and the properties every scene drives (rim, base, lit, fade, grey, dimk), and whose other
    objects (<name>-skin.001 ...) hold one ellipsoid element each: one per part and one per joint's ball, parented to their
    part's joint pivot so the skin bends with the joints. The parts S.build drew (its boxes, capsules and knuckles) are removed;
    its pivots stay. Returns (basis object, tip dict or None)."""
    body = S.BODIES[key]
    parts = body["parts"]
    for o in [o for o in bpy.data.objects if o.name.startswith(name + "-") and "rim" in o.keys()]:
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
    sk = SK.plan(parts)
    fam = name + "-skin"

    def meta(label):
        mb = bpy.data.metaballs.new(label)
        mb.threshold = sk["threshold"]
        mb.resolution = mb.render_resolution = SK.FIELD_CELL
        mb.update_method = "UPDATE_ALWAYS"
        return mb

    bmb = meta(fam)
    bmb.materials.append(material)
    basis = bpy.data.objects.new(fam, bmb)
    bpy.context.scene.collection.objects.link(basis)
    basis.parent = root
    pivots = {p["i"]: bpy.data.objects["%s-joint%d" % (name, p["i"])] for p in parts}
    for n, e in enumerate(sk["elements"], 1):
        mb = meta("%s-%s" % (fam, e["label"]))
        el = mb.elements.new(type="ELLIPSOID")
        el.co = (0.0, 0.0, 0.0)
        el.radius = e["rho"]
        el.size_x, el.size_y, el.size_z = e["half"]
        el.stiffness = e["stiff"]
        ob = bpy.data.objects.new("%s.%03d" % (fam, n), mb)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = pivots[e["owner"]]
        ob.rotation_mode = "QUATERNION"
        ob.location = e["co"]
        ob.rotation_quaternion = e["rot"]
    # what the body material reads off the object: the theatre's per-part properties carried by the one skin (the guild is
    # neither photosynthetic nor absorptive, so the colours are the 'other' guild's until skin() sets the team's), the knuckles'
    # glow gone with the knuckles, the shade and warmth of the first part's individual
    rim, through, bodyc = S.COLOURS["other"]
    bseed, bown = S.body_seeds(body["id"])
    individual = S.frac(bown)
    basis["rim"] = S.lin(rim)
    basis["trans_tint"] = S.lin(through)
    basis["base"] = S.lin(bodyc)
    basis["trans_gain"] = 0.4
    basis["sheen"] = 0.08
    basis["reserve"] = 1.0
    basis["rim_scale"] = 1.0
    basis["emit"] = 0.0
    basis["shade"] = S.frac(individual * 17.37 + 0.41) * 2 - 1
    basis["warmth"] = S.frac(individual * 31.91 + 0.07) * 2 - 1
    # the light through the tissue: the theatre's 0.06 over each part's half thickness, here the average over the parts weighted by their area
    basis["skin_thick"] = SK.skin_thickness(parts)
    set_skin_cell(basis, cell)
    tip = None
    if tip_index is not None:
        chain, p = [], pivots[tip_index]
        while p is not None and p.name.startswith(name + "-joint"):
            chain.append(p)
            p = p.parent
        tip = {"index": tip_index, "parts": parts, "element": SK.tip_element(parts, tip_index), "pivots": chain[::-1]}
    return basis, tip


def add_body(run, key, name, team, material, scale, rng, wave=(14.0, 0.45, 0.0), bridge=False, skin_on=None):
    skin_on = ONE_SKIN if skin_on is None else skin_on
    tip_material = None
    two_tone = None
    if isinstance(material, dict):
        tip_material = material.get("taleplum-tip")
        two_tone = material.get("taleplum-two") if (skin_on and team == "taleplum") else None
        material = two_tone if two_tone is not None else material[team]
    root, joints = S.build(key, name, (0, 0, 0), (0, 0, 0), 1.0, material, 2.2)
    for j in joints:
        j["jitter"] = rng.uniform(-0.25, 0.25)
    if skin_on:
        basis, tip = add_skin(key, name, root, material, tip_index=1 if two_tone is not None else None)
        skin(name, team)
        if two_tone is not None:
            light, mid, shadow = TEAM["taleplum-tip"]      # the founder's second part is the design's pink-spotted tip
            basis["rim2"] = hexlin(mid); basis["trans_tint2"] = hexlin(mid); basis["base2"] = hexlin_scaled(shadow, BASE_DARKEN)
        drive_state(root, name)
        b = Body(key, name, team, material, root, joints, body_length(key), scale, skin=basis, run=run, tip=tip)
        if tip is not None:
            b.key_tip()
        b.wave = wave
        run.bodies.append(b)
        return b
    skin(name, team)
    if team == "taleplum" and isinstance(material, type(None)) is False and tip_material is not None:
        # the founder's second part is the design's pink-spotted tip
        light, mid, shadow = TEAM["taleplum-tip"]
        for o in part_objects(name):
            if o.name.startswith(name + "-p1-"):
                o["rim"] = hexlin(mid); o["trans_tint"] = hexlin(mid); o["base"] = hexlin_scaled(shadow, BASE_DARKEN)
                for sl in o.material_slots:
                    sl.material = tip_material
    if bridge:
        add_bridges(key, name)
    drive_state(root, name)
    b = Body(key, name, team, material, root, joints, body_length(key), scale)
    b.wave = wave
    run.bodies.append(b)
    return b


def body_material(run, teams=("guzzler", "nibbler", "pale", "taleplum", "taleplum-tip", "bone", "yellow", "brown")):
    """One material per team (each has its own pattern tile): {team: material}. With ONE_SKIN each is also made to fit a metaball
    skin (skin_surface), and the taleplum gets a second, "taleplum-two", whose second part is the pink-spotted tip (add_tip)."""
    out = {}
    names = list(teams) + (["taleplum-two"] if ONE_SKIN and "taleplum" in teams and "taleplum-tip" in teams else [])
    for team in names:
        mat = S.body_material_for(run.cam)
        mat.name = "theatre-body-" + team
        patch_state(mat, lit_ceil=LIT_CEIL.get(team, 1.0) if ONE_SKIN else 1.0)
        if ONE_SKIN:
            info = add_pattern(mat, "taleplum" if team == "taleplum-two" else team, scale=PATTERN_SCALE.get(team, 2.2), dark=PATTERN_DARK.get(team, 0.0))
        else:
            info = add_pattern(mat, team)
        if ONE_SKIN:
            skin_surface(mat)
            if team == "taleplum-two":
                add_tip(mat, info)
        wrap_dim(mat)
        out[team] = mat
    return out


# ------------------------------------------------------------------ props
def box_mesh(name, p0, p1, material, thick):
    """A bar from p0 to p1 (world points), square section `thick` metres."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    length = d.length
    q = d.to_track_quat("Z", "Y")
    verts, faces = [], []
    h = thick / 2
    for z in (0, length):
        for sx, sy in ((-h, -h), (h, -h), (h, h), (-h, h)):
            verts.append(Vector((sx, sy, z)))
    for i in range(4):
        j = (i + 1) % 4
        faces.append((i, j, 4 + j, 4 + i))
    faces += [(0, 3, 2, 1), (4, 5, 6, 7)]
    ob = mesh_obj(name, [tuple(v) for v in verts], faces, material, smooth=False)
    ob.location = a
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = q
    return ob


def plane(name, corners, material):
    return mesh_obj(name, corners, [(0, 1, 2, 3)], material, smooth=False)


EDGE_MATS = {}
EDGE_GAIN = 1.2          # the design draws thin, faint lines (stroke opacity .28-.9 of #dcfffa), not neon


def edge_mat(strength, color="#dcfffa"):
    k = (round(strength, 2), color)
    if k not in EDGE_MATS:
        EDGE_MATS[k] = emissive("edge-%s-%.2f" % (color, strength), color, EDGE_GAIN * strength)
    return EDGE_MATS[k]


def tank(run, front, depth_m, t_front, edge_px=1.6, edge_scale=1.0):
    """The glass box with an open top. `front` is the front face's design pixels (x0, y0, x1, y1) at depth t_front;
    the box runs depth_m away from the camera. Returns a dict of its corners and objects.
    Panes are thin glass with a teal veil on the inside; edges are emissive bars, brightest at the top."""
    cam = run.cam_model
    x0, y0, x1, y1 = front
    FTL, FTR = cam.to_world(x0, y0, t_front), cam.to_world(x1, y0, t_front)
    FBL, FBR = cam.to_world(x0, y1, t_front), cam.to_world(x1, y1, t_front)
    add = lambda p: (p[0], p[1] + depth_m, p[2])
    BTL, BTR, BBL, BBR = add(FTL), add(FTR), add(FBL), add(FBR)
    m = cam.metres_per_px(t_front)
    th = edge_px * m
    veil_back = veil("veil-inside", "#2a8c9a", "#0d4c5a", 0.38, 0.25)
    gl = glass("tank-glass")
    objs = {}
    objs["back"] = plane("pane-back", [BBL, BBR, BTR, BTL], veil_back)
    objs["left"] = plane("pane-left", [FBL, BBL, BTL, FTL], veil_back)
    objs["right"] = plane("pane-right", [BBR, FBR, FTR, BTR], veil_back)
    objs["floor"] = plane("pane-floor", [FBL, FBR, BBR, BBL], emissive("tank-floor", "#0d4c5a", 0.4, 0.25))
    objs["front"] = plane("pane-front", [FBL, FBR, FTR, FTL], gl)
    bars = [("front-top", FTL, FTR, 0.9), ("front-left", FTL, FBL, 0.7), ("front-right", FTR, FBR, 0.7), ("front-bottom", FBL, FBR, 0.7),
            ("top-left", FTL, BTL, 0.9), ("top-right", FTR, BTR, 0.9), ("top-back", BTL, BTR, 0.9),
            ("back-right", BTR, BBR, 0.45), ("right-bottom", FBR, BBR, 0.45),
            ("back-left", BTL, BBL, 0.28), ("back-bottom", BBL, BBR, 0.28), ("left-bottom", FBL, BBL, 0.28)]
    for n, a, b, s in bars:
        objs["edge-" + n] = box_mesh("edge-" + n, a, b, edge_mat(s * edge_scale), th * (0.7 if s < 0.5 else 1.0))
    corners = {"FTL": FTL, "FTR": FTR, "FBL": FBL, "FBR": FBR, "BTL": BTL, "BTR": BTR, "BBL": BBL, "BBR": BBR}
    return {"corners": corners, "objs": objs}


def caustic_floor(run, corners, strength=0.5, scale=11.0):
    """Moving caustics on the tank's floor: Voronoi distance-to-edge as emission, its W keyed through the
    object property `w` (the caller keys it per frame)."""
    c = corners
    ob = plane("caustics", [c["FBL"], c["FBR"], c["BBR"], c["BBL"]], None)
    ob.location = (0, 0, 0.0015)
    ob["w"] = 0.0
    m = mat_new("caustics")
    nt = m.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.voronoi_dimensions, vor.feature = "4D", "DISTANCE_TO_EDGE"
    vor.inputs["Scale"].default_value = scale
    nt.links.new(tc.outputs["Generated"], vor.inputs["Vector"])
    w = nt.nodes.new("ShaderNodeAttribute"); w.attribute_type, w.attribute_name = "OBJECT", "w"
    nt.links.new(w.outputs["Fac"], vor.inputs["W"])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = 0.02, 0.0
    ramp.inputs["To Min"].default_value, ramp.inputs["To Max"].default_value = 0.0, 1.0
    ramp.clamp = True
    nt.links.new(vor.outputs["Distance"], ramp.inputs["Value"])
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*hexlin("#c9fff6"), 1)
    em.inputs["Strength"].default_value = strength
    mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"; mul.inputs[1].default_value = 0.32
    nt.links.new(ramp.outputs["Result"], mul.inputs[0])
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(mul.outputs[0], mix.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    ob.data.materials.append(m)
    return ob


def capsule_verts(radius, height, sides=40, rings=10):
    """A capsule along z from 0 to height: hemispherical caps of `radius`, rows of `sides` vertices."""
    prof = []
    for i in range(rings + 1):
        a = math.pi / 2 * (1 - i / rings)
        prof.append((radius * math.cos(a), radius - radius * math.sin(a)))
    for i in range(rings + 1):
        a = math.pi / 2 * (i / rings)
        prof.append((radius * math.cos(a), height - radius + radius * math.sin(a)))
    verts, faces = [], []
    for (r, z) in prof:
        for s in range(sides):
            th = 2 * math.pi * s / sides
            verts.append((r * math.cos(th), r * math.sin(th), z))
    rows = len(prof)
    for i in range(rows - 1):
        for s in range(sides):
            a, b = i * sides + s, i * sides + (s + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    return verts, faces


def tube(run, name, px, top_px, bot_px, w_px, t, fill=0.0, leaf_rx=21.6, spacing=15):
    """The measure: a glass capsule on the tank's front, standing at design pixel x px between top_px and bot_px of
    the plane at depth t, with a glowing green fill whose level is `fill` (0-1) and is keyed through the object
    property `level`, scaling an inner cylinder. Returns {glass, fill, meniscus, top (world), bottom (world)}."""
    cam = run.cam_model
    m = cam.metres_per_px(t)
    bottom, top = cam.to_world(px, bot_px, t), cam.to_world(px, top_px, t)
    height, radius = top[2] - bottom[2], w_px / 2 * m
    v, f = capsule_verts(radius, height)
    gl = mesh_obj(name + "-glass", v, f, veil_glass(name), smooth=True)
    gl.location = bottom
    # fill: a cylinder of the inner radius, height scaled by level; origin at its base
    inner = radius * 0.86
    cv, cf = cyl_verts(inner, height - 2 * radius * 0.86)
    fl = mesh_obj(name + "-fill", cv, cf, emissive("tube-fill", "#2f9a52", 0.7, 0.45), smooth=True)
    fl.location = (bottom[0], bottom[1], bottom[2] + radius * 0.86)
    fl["level"] = fill
    fl.scale = (1, 1, max(fill, 1e-4))
    men = mesh_obj(name + "-meniscus", *disc_verts(inner * 0.95), emissive("meniscus", "#d8ffc0", 4.0, 0.8), smooth=True)
    men.location = (fl.location[0], fl.location[1], fl.location[2])
    # the settled leaves, stacked 15 design px apart alternating a little either side, as the design's tube draws them
    stack = []
    n = int((bot_px - top_px - 12) / spacing)
    for k in range(n):
        y = bot_px - 12 - spacing * k
        x = px + (3 if k % 2 else -3)
        tilt = 6 if k % 2 else -6
        lf = leaf(run, name + "-s%d" % k, x, y, t, leaf_rx, tilt, strength=0.95)
        lf.scale = (1e-4, 1e-4, 1e-4)
        stack.append((lf, y, x, tilt))
    return {"glass": gl, "fill": fl, "meniscus": men, "bottom": bottom, "top": top, "inner_height": height - 2 * radius * 0.86,
            "base_z": bottom[2] + radius * 0.86, "stack": stack, "run": run, "t": t, "top_px": top_px, "bot_px": bot_px, "leaf_rx": leaf_rx}


def set_tube_level(tb, level, frame=None):
    fl, men = tb["fill"], tb["meniscus"]
    lvl = max(level, 1e-4)
    fl.scale = (1, 1, lvl)
    men.location = (men.location[0], men.location[1], tb["base_z"] + tb["inner_height"] * level)
    sm = max(min(1.0, max(level, 0.0) / 0.03), 1e-4)          # grows from nothing, no hide cut
    men.scale = (sm, sm, 1.0)
    run, t = tb["run"], tb["t"]
    level_px = tb["bot_px"] - (tb["bot_px"] - tb["top_px"]) * level
    for lf, y, x, tilt in tb["stack"]:
        place_leaf(run, lf, x, y, t, max(0.01, tb["leaf_rx"] * min(1.0, max(0.0, (y - (level_px + 5)) / 10.0))), tilt, frame=frame)
    if frame is not None:
        fl.keyframe_insert("scale", frame=frame)
        men.keyframe_insert("location", frame=frame)
        men.keyframe_insert("scale", frame=frame)


def veil_glass(name):
    m = mat_new(name + "-glass")
    nt = m.node_tree
    lw = nt.nodes.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*hexlin("#d8fffa"), 1)
    em.inputs["Strength"].default_value = 1.2
    edge = nt.nodes.new("ShaderNodeMath"); edge.operation = "POWER"; edge.inputs[1].default_value = 5.0
    nt.links.new(lw.outputs["Facing"], edge.inputs[0])
    edgek = nt.nodes.new("ShaderNodeMath"); edgek.operation = "MULTIPLY"; edgek.inputs[1].default_value = 0.8
    nt.links.new(edge.outputs[0], edgek.inputs[0])
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    dark = nt.nodes.new("ShaderNodeEmission")
    dark.inputs["Color"].default_value = (*hexlin("#06303a"), 1)
    dark.inputs["Strength"].default_value = 0.6
    body = nt.nodes.new("ShaderNodeMixShader")        # the tube's faint dark fill, 55%
    body.inputs["Fac"].default_value = 0.2
    nt.links.new(tr.outputs[0], body.inputs[1]); nt.links.new(dark.outputs[0], body.inputs[2])
    rim = nt.nodes.new("ShaderNodeMixShader")         # the glass's bright edges: facing term
    nt.links.new(edgek.outputs[0], rim.inputs["Fac"])
    nt.links.new(body.outputs[0], rim.inputs[1]); nt.links.new(em.outputs[0], rim.inputs[2])
    nt.links.new(rim.outputs[0], out_node(nt).inputs["Surface"])
    return wrap_dim(m)


def cyl_verts(radius, height, sides=32):
    verts, faces = [], []
    for z in (0, height):
        for s in range(sides):
            th = 2 * math.pi * s / sides
            verts.append((radius * math.cos(th), radius * math.sin(th), z))
    for s in range(sides):
        a, b = s, (s + 1) % sides
        faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range(sides - 1, -1, -1)))
    faces.append(tuple(range(sides, 2 * sides)))
    return verts, faces


def disc_verts(radius, sides=32):
    verts = [(radius * math.cos(2 * math.pi * s / sides), radius * math.sin(2 * math.pi * s / sides), 0.0) for s in range(sides)]
    return verts, [tuple(range(sides))]


LEAF_MAT = {}


def leaf_material(strength=1.1):
    if strength in LEAF_MAT:
        return LEAF_MAT[strength]
    m = mat_new("leaf-%.1f" % strength)
    nt = m.node_tree
    lw = nt.nodes.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.45
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    e = ramp.color_ramp.elements
    e[0].position, e[0].color = 0.0, (*hexlin("#c4f5a6"), 1)
    e[1].position, e[1].color = 1.0, (*hexlin("#2f9a52"), 1)
    mid = e.new(0.45); mid.color = (*hexlin("#6fd27c"), 1)
    nt.links.new(lw.outputs["Facing"], ramp.inputs["Fac"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs["Facing"], inv.inputs[1])
    nt.links.new(inv.outputs[0], ramp.inputs["Fac"])
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(ramp.outputs["Color"], em.inputs["Color"])
    em.inputs["Strength"].default_value = strength
    nt.links.new(em.outputs[0], out_node(nt).inputs["Surface"])
    wrap_dim(m)
    LEAF_MAT[strength] = m
    return m


def leaf(run, name, px, py, t, rx_px, tilt_deg=0.0, strength=1.1):
    """One leaf, the design's ellipse (rx, ry = 0.3 rx) seen from the front, as a thin ellipsoid at the pixel."""
    m = run.cam_model.metres_per_px(t)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(leaf_material(strength))
    ob.visible_shadow = False
    link_obj(ob)
    ob.location = run.cam_model.to_world(px, py, t)
    ob.rotation_mode = "XYZ"
    ob["rx_m"] = rx_px * m
    ob.scale = (rx_px * m, rx_px * m * 0.18, rx_px * m * 0.3)
    ob.rotation_euler = (0.0, math.radians(tilt_deg), 0.0)
    return ob


def place_leaf(run, ob, px, py, t, rx_px, tilt_deg, frame=None):
    m = run.cam_model.metres_per_px(t)
    ob.location = run.cam_model.to_world(px, py, t)
    ob.scale = (rx_px * m, rx_px * m * 0.18, rx_px * m * 0.3)
    ob.rotation_euler = (0.0, math.radians(tilt_deg), 0.0)
    if frame is not None:
        ob.keyframe_insert("location", frame=frame)
        ob.keyframe_insert("scale", frame=frame)
        ob.keyframe_insert("rotation_euler", frame=frame)


def pixel_pos(run, px, py, t):
    return run.cam_model.to_world(px, py, t)


# ------------------------------------------------------------------ the lighting and grade, in the pattern of outro.py
def finish_look(run, rng, snow_count=1100):
    S.grade()
    return snow(run, rng, count=snow_count)



# ================================================================== stage 2: parents, bars, founder
class Keyer:
    """Keys a property only where its value changes, with a hold key one frame before the change, so a long constant
    stretch costs two keys and a step stays a step. attr is a bpy attribute ("location", "scale", ...) or the name
    of a custom property."""

    def __init__(self):
        self.last = {}

    @staticmethod
    def _norm(v):
        if isinstance(v, bool):
            return v
        try:
            return tuple(float(x) for x in v)
        except TypeError:
            return float(v) if isinstance(v, (int, float)) else v

    def _apply(self, obj, attr, v, frame):
        if hasattr(obj, attr) and attr not in obj.keys():
            setattr(obj, attr, v)
            obj.keyframe_insert(attr, frame=frame)
        else:
            obj[attr] = v
            obj.keyframe_insert('["%s"]' % attr, frame=frame)

    def set(self, obj, attr, value, frame):
        v = self._norm(value)
        key = (obj.name, attr)
        prev = self.last.get(key)
        if prev is None:
            self._apply(obj, attr, v if not isinstance(v, tuple) else value, frame)
            self.last[key] = (frame, v, v)
            return
        pf, pv, cur = prev
        if v == cur:
            return
        if pf < frame - 1:
            self._apply(obj, attr, pv, frame - 1)
        self._apply(obj, attr, v if not isinstance(v, tuple) else value, frame)
        self.last[key] = (frame, v, v)


def tank_back(run, front, t_back=18.0, offset_px=None, edge_scale=0.6):
    """The tank, further back and soft with distance: the same glass box at depth t_back, its back face offset up and
    right by the drawn 0.2 w, -0.14 w. Returns (root empty, corners). The root empty carries the transform
    a later slot uses to move and shrink it (scaled in x and z about the world origin, so its depth stays)."""
    cam = run.cam_model
    x0, y0, x1, y1 = front
    fc = ((x0 + x1) / 2, (y0 + y1) / 2)
    off = offset_px if offset_px is not None else 0.2 * (x1 - x0)
    k = off / (cam.pp[0] - fc[0])                       # 1 - s for the drawn offset
    depth_m = t_back * k / (1 - k)
    tk = tank(run, front, depth_m, t_back, edge_px=2.0, edge_scale=edge_scale)
    root = bpy.data.objects.new("tank-back-root", None)
    run.sc.collection.objects.link(root)
    for o in tk["objs"].values():
        o.parent = root
    root.rotation_mode = "XYZ"
    return root, tk


def tank_back_pose(run, root, front0, front1, t_back, frame, K):
    """Moves and shrinks the tank root from the drawing at front0 to the one at front1 (design pixels)."""
    cam = run.cam_model
    s = (front1[2] - front1[0]) / (front0[2] - front0[0])
    p0 = cam.to_world(front0[0], front0[1], t_back)
    p1 = cam.to_world(front1[0], front1[1], t_back)
    K.set(root, "scale", (s, 1.0, s), frame)
    K.set(root, "location", (p1[0] - s * p0[0], 0.0, p1[2] - s * p0[2]), frame)


def beam(run, x, y_end, w, depth=10.0, thickness=3.0, gain=0.9, name="beam"):
    """Light falling from the surface onto one place (pass5.js beam): a slab from the top of the frame, its width
    .7 w at the top to 1.4 w at y_end, fading down its length, and the pool it makes, a soft ellipse at y_end - 70.
    Returns the objects; each takes `dimk` (1 = off)."""
    top = [(x - w * 0.35, -20), (x + w * 0.35, -20)]
    bot = [(x + w * 0.7, y_end), (x - w * 0.7, y_end)]
    sl = slab(run, name, top, bot, depth, thickness, "#e4fffb", 0.24 * gain / thickness, bell_pow=1.6)
    pool = blob(run, name + "-pool", x, y_end - 70, depth, w * 1.1, w * 0.7, "#d8fffb", 0.8, 0.12)
    return [sl, pool]


def set_dim(objs, k, frame, K):
    for o in objs:
        K.set(o, "dimk", k, frame)


def tube_group(tb):
    return [tb["glass"], tb["fill"], tb["meniscus"]] + [lf for lf, *_ in tb["stack"]]


def tube_level_k(tb, level, frame, K):
    """set_tube_level, keyed only where it changes."""
    fl, men, run, t = tb["fill"], tb["meniscus"], tb["run"], tb["t"]
    K.set(fl, "scale", (1, 1, max(level, 1e-4)), frame)
    K.set(men, "location", (men.location[0], men.location[1], tb["base_z"] + tb["inner_height"] * level), frame)
    s = min(1.0, max(level, 0.0) / 0.03)                      # a disc that grows from nothing, never a hide cut
    K.set(men, "scale", (max(s, 1e-4), max(s, 1e-4), 1.0), frame)
    level_px = tb["bot_px"] - (tb["bot_px"] - tb["top_px"]) * level
    m = run.cam_model.metres_per_px(t)
    for lf, y, x, tilt in tb["stack"]:
        r = max(0.01, tb["leaf_rx"] * min(1.0, max(0.0, (y - (level_px + 5)) / 10.0)))      # a leaf eases in as the fill drops past
        K.set(lf, "scale", (r * m, r * m * 0.18, r * m * 0.3), frame)


def ledge(run, x0=260, x1=1660, wide0=200, wide1=1720, y=640, y_front=700, t=7.65, strength=1.0):
    """The ledge of the bars: a thin sheet of light seen a little from above, receding, with a bright front edge.
    Horizontal: its back edge is design row `y` at depth t, its front edge row `y_front` and wider, nearer."""
    cam = run.cam_model
    t_near = t * (cam.pp[1] - y) / (cam.pp[1] - y_front)
    BL, BR = cam.to_world(x0, y, t), cam.to_world(x1, y, t)
    FL, FR = cam.to_world(wide0, y_front, t_near), cam.to_world(wide1, y_front, t_near)
    sheet = plane("ledge-sheet", [FL, FR, BR, BL], emissive("ledge-sheet", "#bffcf4", 0.9 * strength, 0.12))
    edge = box_mesh("ledge-edge", BL, BR, emissive("ledge-edge", "#bffcf4", 3.0 * strength, 0.9), cam.metres_per_px(t) * 3.0)
    return [sheet, edge]


def ring(run, name, px, py, rx_px, ry_px, t, color_hex="#bffcf4", alpha=0.3, width_frac=0.06):
    """A faint dashed-looking ring (an annulus of emission) lying on the ledge: where a stack will stand."""
    m = run.cam_model.metres_per_px(t)
    sides = 48
    verts, faces = [], []
    for s_ in range(sides):
        th = 2 * math.pi * s_ / sides
        for rr in (1.0, 1.0 - width_frac * 2):
            verts.append((rr * math.cos(th), rr * math.sin(th), 0.0))
    for s_ in range(sides):
        a, b = 2 * s_, 2 * ((s_ + 1) % sides)
        if s_ % 2 == 0:
            faces.append((a, a + 1, b + 1, b))
    ob = mesh_obj(name, verts, faces, emissive("ring-" + name, color_hex, 2.0, alpha), smooth=False)
    ob.location = run.cam_model.to_world(px, py, t)
    ob.scale = (rx_px * m, ry_px * m, 1.0)
    return ob


def liquid_mat(name, store, a_bottom=0.5, a_top=0.3, strength=0.75, top_share=0.4):
    """The vial's liquid, as liquid in glass: a vertical gradient from the deep store colour at the bottom to a paler tint of it at
    the top (store[0] mixed top_share into store[1]), more transparent as it rises, and a dimmer glow than a flat emissive's. It runs
    along the object's Generated z, which is 0 at the foot and 1 at the surface however the keyed scale stretches it."""
    m = mat_new(name)
    nt = m.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    z = nt.nodes.new("ShaderNodeMath"); z.operation = "ADD"; z.use_clamp = True; z.inputs[1].default_value = 0.0
    nt.links.new(sep.outputs[2], z.inputs[0])
    deep, light = hexlin(store[1]), hexlin(store[0])
    top = tuple(d + (l - d) * top_share for d, l in zip(deep, light))
    col = nt.nodes.new("ShaderNodeMix"); col.data_type = "RGBA"
    nt.links.new(z.outputs[0], _mix_in(col, "Factor"))
    _mix_in(col, "A").default_value = (*deep, 1.0); _mix_in(col, "B").default_value = (*top, 1.0)
    al = nt.nodes.new("ShaderNodeMix"); al.data_type = "FLOAT"
    nt.links.new(z.outputs[0], _mix_in(al, "Factor"))
    _mix_in(al, "A").default_value = a_bottom; _mix_in(al, "B").default_value = a_top
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = strength
    nt.links.new(col.outputs["Result"], em.inputs["Color"])
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(al.outputs["Result"], mix.inputs["Fac"])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    return wrap_dim(m)


def vial(run, name, px, top_px, bot_px, w_px, ledge_px, t, store=("#ffe2a8", "#b07a22"), notch=True):
    """A glass vial standing through the ledge: its liquid fills between the ledge and `level` when the level is
    above the ledge's start (a bead of savings above it), and an empty warm-edged hollow below the ledge when the
    level is under. Keyed by set_vial. Returns a dict of its objects."""
    cam = run.cam_model
    m = cam.metres_per_px(t)
    bottom, top = cam.to_world(px, bot_px, t), cam.to_world(px, top_px, t)
    height, radius = top[2] - bottom[2], w_px / 2 * m
    v, f = capsule_verts(radius, height)
    gl = mesh_obj(name + "-glass", v, f, veil_glass(name), smooth=True)
    gl.location = bottom
    ledge_z = cam.to_world(px, ledge_px, t)[2]
    inner = radius * 0.84
    cv, cf = cyl_verts(inner, 1.0)
    # the savings: from the ledge up, gradient store colours (one flat colour: the lighter), scaled by the keyed level
    liq = mesh_obj(name + "-liquid", cv, cf, liquid_mat(name + "-liq", store), smooth=True)
    liq.location = (bottom[0], bottom[1], ledge_z)
    liq.scale = (1, 1, 1e-4)
    cap = mesh_obj(name + "-cap", *disc_verts(inner * 0.95), emissive(name + "-capm", store[0], 1.2, 0.26), smooth=True)
    cap.location = (bottom[0], bottom[1], ledge_z)
    # the hollow: a sleeve below the ledge, tinted warm, its edge glowing in bands (the design's dashed outline)
    hm = mat_new(name + "-hollow")
    nt = hm.node_tree
    lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.4
    edge = nt.nodes.new("ShaderNodeMath"); edge.operation = "POWER"; edge.inputs[1].default_value = 1.5
    nt.links.new(lw.outputs["Facing"], edge.inputs[0])
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    band = nt.nodes.new("ShaderNodeMath"); band.operation = "MULTIPLY"; band.inputs[1].default_value = 5.0
    nt.links.new(sep.outputs[2], band.inputs[0])
    fr = nt.nodes.new("ShaderNodeMath"); fr.operation = "FRACT"
    nt.links.new(band.outputs[0], fr.inputs[0])
    on = nt.nodes.new("ShaderNodeMath"); on.operation = "LESS_THAN"; on.inputs[1].default_value = 0.64
    nt.links.new(fr.outputs[0], on.inputs[0])
    glowk = nt.nodes.new("ShaderNodeMath"); glowk.operation = "MULTIPLY"
    nt.links.new(edge.outputs[0], glowk.inputs[0]); nt.links.new(on.outputs[0], glowk.inputs[1])
    tint = nt.nodes.new("ShaderNodeMath"); tint.operation = "ADD"; tint.inputs[1].default_value = 0.0
    nt.links.new(glowk.outputs[0], tint.inputs[0])
    tint.inputs[1].default_value = 0.22                  # the faint #e0a24a tint of the hollow's body
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*hexlin("#ffc56e"), 1); em.inputs["Strength"].default_value = 1.8
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    cl = nt.nodes.new("ShaderNodeMath"); cl.operation = "MINIMUM"; cl.inputs[1].default_value = 1.0
    nt.links.new(tint.outputs[0], cl.inputs[0])
    nt.links.new(cl.outputs[0], mx.inputs["Fac"]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out_node(nt).inputs["Surface"])
    wrap_dim(hm)
    hv, hf = cyl_verts(inner * 0.92, 1.0)
    hol = mesh_obj(name + "-hollow", hv, hf, hm, smooth=True)
    hol.location = (bottom[0], bottom[1], ledge_z)
    hol.scale = (1, 1, -1e-4)                            # grows downward: a negative z scale
    parts = {"glass": gl, "liquid": liq, "cap": cap, "hollow": hol}
    if notch:
        n_ = ring(run, name + "-notch", px, ledge_px, w_px / 2 + 8, 5, t, "#e4fffb", 0.9, 0.5)
        parts["notch"] = n_
    parts.update({"m": m, "ledge_z": ledge_z, "px": px, "ledge_px": ledge_px, "t": t, "run": run})
    return parts


def vial_level_k(v, level_px, frame, K):
    """Sets a vial's content: level_px is the design row of the liquid's surface; above the ledge row (smaller y)
    is savings, below is a hollow."""
    m, ledge_px = v["m"], v["ledge_px"]
    up = max(0.0, ledge_px - level_px) * m
    down = max(0.0, level_px - ledge_px) * m
    K.set(v["liquid"], "scale", (1, 1, max(up, 1e-4)), frame)
    K.set(v["cap"], "location", (v["cap"].location[0], v["cap"].location[1], v["ledge_z"] + up - down), frame)
    s = max(1e-4, min(1.0, abs(level_px - ledge_px) / 4.0))
    K.set(v["cap"], "scale", (s, s, 1.0), frame)
    K.set(v["hollow"], "scale", (1, 1, -max(down, 1e-4)), frame)


def vial_group(v):
    return [v["glass"], v["liquid"], v["cap"], v["hollow"]] + ([v["notch"]] if "notch" in v else [])


def thread(run, name, p0, p1, t, op=1.0):
    """A faint thread of light from a parent to its child: a thin tube along the design's quadratic (mid control at
    the higher end minus 40 px), emission fading from .55 to .12 along it, with five motes. `grow` (0-1, keyed) reveals it
    from the parent's end. Returns [thread, motes...]."""
    cam = run.cam_model
    mx_, my_ = (p0[0] + p1[0]) / 2, min(p0[1], p1[1]) - 40
    def at(u):
        return ((1 - u) ** 2 * p0[0] + 2 * (1 - u) * u * mx_ + u * u * p1[0], (1 - u) ** 2 * p0[1] + 2 * (1 - u) * u * my_ + u * u * p1[1])
    n, sides, r = 40, 6, 1.6 * cam.metres_per_px(t)
    verts, faces = [], []
    for i in range(n + 1):
        x, y = at(i / n)
        c = cam.to_world(x, y, t)
        for sd in range(sides):
            th = 2 * math.pi * sd / sides
            verts.append((c[0], c[1] + r * math.cos(th), c[2] + r * math.sin(th)))
    for i in range(n):
        for sd in range(sides):
            a, b = i * sides + sd, i * sides + (sd + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    m = mat_new(name + "-mat")
    nt = m.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    gr = nt.nodes.new("ShaderNodeAttribute"); gr.attribute_type, gr.attribute_name = "OBJECT", "grow"
    vis = nt.nodes.new("ShaderNodeMath"); vis.operation = "LESS_THAN"
    nt.links.new(sep.outputs[0], vis.inputs[0]); nt.links.new(gr.outputs["Fac"], vis.inputs[1])
    fall = nt.nodes.new("ShaderNodeMapRange"); fall.inputs["To Min"].default_value, fall.inputs["To Max"].default_value = 0.55, 0.12
    nt.links.new(sep.outputs[0], fall.inputs["Value"])
    a = nt.nodes.new("ShaderNodeMath"); a.operation = "MULTIPLY"; a.inputs[1].default_value = op
    nt.links.new(fall.outputs["Result"], a.inputs[0])
    a2 = nt.nodes.new("ShaderNodeMath"); a2.operation = "MULTIPLY"
    nt.links.new(a.outputs[0], a2.inputs[0]); nt.links.new(vis.outputs[0], a2.inputs[1])
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*hexlin("#bffcf4"), 1); em.inputs["Strength"].default_value = 3.0
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(a2.outputs[0], mix.inputs["Fac"]); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_node(nt).inputs["Surface"])
    th_ = mesh_obj(name, verts, faces, wrap_dim(m), smooth=True)
    th_["grow"] = 1.0
    out = [th_]
    for i in range(1, 6):
        u = i / 6
        x, y = at(u)
        mote = blob(run, "%s-mote%d" % (name, i), x, y, t, 3.5 - 2 * u + 3, 3.5 - 2 * u + 3, "#e4fffb", 1.0, 0.8 * op * (0.8 - 0.4 * u))
        out.append(mote)
    return out


# ------------------------------------------------------------------ stage 2b: the seas and the winter
def sea_column(run, cx, w, seed, depth=14.0, thickness=3.0, gain=1.0):
    """One open sea (pass5.js sea): a column of lit water under its own bright surface, darkness either side. A tall
    volume slab (the design's seaBody gradient, #3aa6b4 at the top fading out), the surface's soft ellipse at row 110,
    its ripples as small bright blobs, and three shafts down through it. Every object takes `dimk`. Untuned looks."""
    r = L.lcg(seed)
    out = []
    top = [(cx - w / 2, 40), (cx + w / 2, 40)]
    bot = [(cx + w / 2, 1140), (cx - w / 2, 1140)]
    out.append(slab(run, "sea%d-body" % seed, top, bot, depth, thickness, "#3aa6b4", 0.30 * gain / thickness, bell_pow=3.0, along_pow=1.0))
    out.append(blob(run, "sea%d-surface" % seed, cx, 110, depth - 1.0, w * 0.5, 34, "#d8fffb", 1.0 * gain, 0.32))
    for i in range(9):
        x, y, s = cx - w * 0.38 + r() * w * 0.76, 92 + r() * 36, 20 + r() * 30
        out.append(blob(run, "sea%d-rip%d" % (seed, i), x + s / 2, y - 3, depth - 1.0, s / 2, 2.5, "#e4fffb", 1.4 * gain, 0.3 + 0.3 * r()))
    for i in range(3):
        x, rw = cx - w * 0.3 + r() * w * 0.5, 20 + r() * 40
        out.append(slab(run, "sea%d-ray%d" % (seed, i), [(x, 110), (x + rw, 110)], [(x + rw * 1.6 + 30, 1000), (x + 30, 1000)],
                        depth - 0.5, thickness, "#d8fffb", 0.22 * gain / thickness))
    return out


def die_keys(K, body, trail, t0, dur, frame, t, x0, y0, x1, y1, size, size_dead, flip, rot0, place):
    """The winter's die() for one body at scene time t: from t0 over dur seconds the body turns over (to +-150 degrees),
    loses its colour (grey 0 -> 1, fade to .68), shrinks to size_dead and sinks from (x0, y0) to (x1, y1), and its faint
    trail of four soft dots appears. `place(b, px, py, size, roll)` puts the body (keys only changes). Before t0 the body
    is alive at (x0, y0) with its own roll rot0."""
    k = L.span(t, t0, t0 + dur)
    e = 1 - (1 - k) ** 2                                       # a sink that slows as it settles
    px, py = L.lerp(x0, x1, e), L.lerp(y0, y1, e)
    roll = L.lerp(rot0, -150.0 if flip else 150.0, e)
    place(body, px, py, L.lerp(size, size_dead, e), roll)
    K.set(body.root, "grey", k, frame)
    K.set(body.root, "fade", 0.68 * k, frame)
    for o in trail:
        K.set(o, "dimk", 1.0 - 0.68 * L.span(t, t0 + 0.2, t0 + 0.7), frame)
