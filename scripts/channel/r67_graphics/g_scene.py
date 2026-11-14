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
            sc.render.filepath = os.path.join(a.out, "frame_%04d.png" % f)
            if os.path.exists(sc.render.filepath):
                continue
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
    return m


def soft_mat(name, color_hex, strength, alpha):
    """An emission whose alpha is `alpha` at the middle of a facing surface and 0 at its rim (squared), so the edge is soft."""
    m = mat_new(name)
    nt = m.node_tree
    lw = nt.nodes.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs["Facing"], inv.inputs[1])
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
    return m


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


def volume_emission(name, color_hex, strength, gradient_down=True):
    """A volume that only emits: strength fading along the object's own height (generated z), 1 at the top."""
    m = mat_new(name)
    nt = m.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    nt.links.new(sep.outputs[2], ramp.inputs["Value"])
    ramp.inputs["To Min"].default_value, ramp.inputs["To Max"].default_value = 0.0, 1.0
    along = nt.nodes.new("ShaderNodeMath"); along.operation = "POWER"; along.inputs[1].default_value = 1.8
    nt.links.new(ramp.outputs["Result"], along.inputs[0])
    # across the slab: a bell, 1 in the middle, 0 at both sides, so a shaft has soft edges
    cx = nt.nodes.new("ShaderNodeMath"); cx.operation = "SUBTRACT"; cx.inputs[1].default_value = 0.5
    nt.links.new(sep.outputs[0], cx.inputs[0])
    ax = nt.nodes.new("ShaderNodeMath"); ax.operation = "MULTIPLY"; ax.inputs[1].default_value = 2.0
    nt.links.new(cx.outputs[0], ax.inputs[0])
    sq = nt.nodes.new("ShaderNodeMath"); sq.operation = "POWER"; sq.inputs[1].default_value = 2.0
    nt.links.new(ax.outputs[0], sq.inputs[0])
    bell = nt.nodes.new("ShaderNodeMath"); bell.operation = "SUBTRACT"; bell.inputs[0].default_value = 1.0; bell.use_clamp = True
    nt.links.new(sq.outputs[0], bell.inputs[1])
    mulb = nt.nodes.new("ShaderNodeMath"); mulb.operation = "MULTIPLY"
    nt.links.new(along.outputs[0], mulb.inputs[0]); nt.links.new(bell.outputs[0], mulb.inputs[1])
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*hexlin(color_hex), 1)
    sk = nt.nodes.new("ShaderNodeMath"); sk.operation = "MULTIPLY"; sk.inputs[1].default_value = strength
    nt.links.new(mulb.outputs[0], sk.inputs[0])
    nt.links.new(sk.outputs[0], em.inputs["Strength"])
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


def slab(run, name, top_px, bot_px, depth, thickness, color_hex, strength):
    """A hexahedron of volume emission: its top edge (two design pixels) to its bottom edge, at depths
    depth +- thickness/2, fading from `strength` at the top to 0 at the bottom."""
    cam = run.cam_model
    pts = []
    for t in (depth - thickness / 2, depth + thickness / 2):
        for (px, py) in top_px + bot_px:
            pts.append(cam.to_world(px, py, t))
    # vertex order per depth: top0, top1, bot1, bot0
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    ob = mesh_obj(name, pts, faces, volume_emission("m-" + name, color_hex, strength), smooth=False)
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
TEAM = {   # light, mid, shadow: rim, through, base on the theatre's props
    "nibbler": ("#e2fff9", "#8ae9da", "#2a9d95"),
    "guzzler": ("#ffeccb", "#e0a24a", "#86561a"),
    "pale": ("#e2fff9", "#8ae9da", "#2a9d95"),           # the parents graphic's Guzzlers, claiming no team
    "taleplum": ("#ecfff9", "#a6e4e2", "#5aa8ae"),
    "bone": ("#fffbe8", "#e8e2c4", "#9c9a80"),            # the queue's variety, after creatures.js's fills
    "yellow": ("#fbffc0", "#e2e85a", "#8f9a2a"),
    "brown": ("#c9a27a", "#8a6540", "#4a3320"),
}
FIELDS = ("lit", "fade", "grey")


def patch_state(mat, fade_hex="#0a3746", lit_floor=0.35):
    """Adds the three keyable controls in front of the body material's fog: lit scales the light, grey
    desaturates, fade mixes toward the water."""
    nt = mat.node_tree
    mix = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMix")
    src = mix.inputs["B"].links[0].from_socket
    attr = {}
    for k in FIELDS:
        a = nt.nodes.new("ShaderNodeAttribute")
        a.attribute_type, a.attribute_name = "OBJECT", k
        attr[k] = a.outputs["Fac"]
    mr = nt.nodes.new("ShaderNodeMapRange")                 # lit 0..1 -> lit_floor..1
    mr.inputs["To Min"].default_value, mr.inputs["To Max"].default_value = lit_floor, 1.0
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


TILE = {"nibbler": "net-cyan.png", "pale": "net-cyan.png", "guzzler": "stripes-amber.png", "taleplum": "maze-pale.png",
        "bone": "spots-bone.png", "yellow": "stripes-yellow.png", "brown": "stripes-amber.png"}
PATTERN_OPACITY = 0.9      # the theatre's PatternOpacity


def add_pattern(mat, team, scale=2.2):
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
    nt.links.new(tex.outputs["Color"], mx.inputs["B"])
    for t in targets:
        nt.links.new(mx.outputs["Result"], t)


def drive_state(root, name):
    """lit, fade and grey live on the root and drive every part."""
    for k, v in (("lit", 1.0), ("fade", 0.0), ("grey", 0.0)):
        root[k] = v
    for o in part_objects(name):
        for k, v in (("lit", 1.0), ("fade", 0.0), ("grey", 0.0)):
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
    def __init__(self, key, name, team, material, root, joints, length, scale):
        self.key, self.name, self.team, self.root, self.joints = key, name, team, root, joints
        self.length, self.scale = length, scale
        self.wave = (14.0, 0.45, 0.0)
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

    def swim(self, t, frame, amp_scale=1.0):
        A, hz, ph = self.wave
        w = 2 * math.pi * hz
        for j in self.joints:
            ang = math.radians(A * amp_scale) * math.sin(w * t + ph - 0.9 * j["depth"] + j["jitter"])
            j["pivot"].rotation_quaternion = Quaternion(Vector(j["axis"]), ang)
            j["pivot"].keyframe_insert("rotation_quaternion", frame=frame)

    def state(self, frame, lit=None, fade=None, grey=None):
        for k, v in (("lit", lit), ("fade", fade), ("grey", grey)):
            if v is not None:
                self.root[k] = v
                self.root.keyframe_insert('["%s"]' % k, frame=frame)


def add_body(run, key, name, team, material, scale, rng, wave=(14.0, 0.45, 0.0)):
    if isinstance(material, dict):
        material = material[team]
    root, joints = S.build(key, name, (0, 0, 0), (0, 0, 0), 1.0, material, 2.2)
    for j in joints:
        j["jitter"] = rng.uniform(-0.25, 0.25)
    skin(name, team)
    drive_state(root, name)
    b = Body(key, name, team, material, root, joints, body_length(key), scale)
    b.wave = wave
    run.bodies.append(b)
    return b


def body_material(run, teams=("guzzler", "nibbler", "pale", "taleplum", "bone", "yellow", "brown")):
    """One material per team (each has its own pattern tile): {team: material}."""
    out = {}
    for team in teams:
        mat = S.body_material_for(run.cam)
        mat.name = "theatre-body-" + team
        patch_state(mat)
        add_pattern(mat, team)
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


def tank(run, front, depth_m, t_front, edge_px=1.6):
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
        objs["edge-" + n] = box_mesh("edge-" + n, a, b, edge_mat(s), th * (0.7 if s < 0.5 else 1.0))
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


def tube(run, name, px, top_px, bot_px, w_px, t, fill=0.0):
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
    n = int((bot_px - top_px - 12) / 15)
    for k in range(n):
        y = bot_px - 12 - 15 * k
        x = px + (3 if k % 2 else -3)
        tilt = 6 if k % 2 else -6
        lf = leaf(run, name + "-s%d" % k, x, y, t, 21.6, tilt, strength=2.6)
        lf.scale = (1e-4, 1e-4, 1e-4)
        stack.append((lf, y, x, tilt))
    return {"glass": gl, "fill": fl, "meniscus": men, "bottom": bottom, "top": top, "inner_height": height - 2 * radius * 0.86,
            "base_z": bottom[2] + radius * 0.86, "stack": stack, "run": run, "t": t, "top_px": top_px, "bot_px": bot_px}


def set_tube_level(tb, level, frame=None):
    fl, men = tb["fill"], tb["meniscus"]
    lvl = max(level, 1e-4)
    fl.scale = (1, 1, lvl)
    men.location = (men.location[0], men.location[1], tb["base_z"] + tb["inner_height"] * level)
    men.hide_render = level < 0.01
    run, t = tb["run"], tb["t"]
    level_px = tb["bot_px"] - (tb["bot_px"] - tb["top_px"]) * level
    for lf, y, x, tilt in tb["stack"]:
        place_leaf(run, lf, x, y, t, 21.6 if y > level_px + 5 else 0.01, tilt, frame=frame)
    if frame is not None:
        fl.keyframe_insert("scale", frame=frame)
        men.keyframe_insert("location", frame=frame)
        men.keyframe_insert("hide_render", frame=frame)


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
    return m


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
