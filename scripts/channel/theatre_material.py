"""The theatre skin's fragment stage (TheatreBody.shader, Fragment) as a Blender node material.

The theatre does not light a body the way a path tracer would: it computes its own light in the
shader, a wrapped key, light through thin tissue, a sheen, a rim carrying the guild, an inner
glow, a near black ambient and the mottle. So this material does the same arithmetic in shader
nodes and hands the result to an Emission shader, and Cycles adds only what the theatre also has
in its grade and camera: the depth of field and the bloom. Bodies cast no shadow in the theatre,
and an emission shader casts none here.

Per object (custom properties, read by an Attribute node of type OBJECT): base, rim, trans_tint,
trans_gain, sheen, reserve, rim_scale, shade, warmth. Per vertex (colour attributes baked from
the vertex stage): "evo_a" = (thickness factor, vein face, vein through, 0), "evo_tone" = the
blade's tone (1, 1, 1 off a blade).
"""
import bpy

class G:
    """A small builder over one node tree, so the arithmetic below reads like the shader's."""
    def __init__(self, nt):
        self.nt = nt
    def n(self, kind, **kw):
        node = self.nt.nodes.new(kind)
        for k, v in kw.items():
            setattr(node, k, v)
        return node
    def link(self, a, b):
        self.nt.links.new(a, b)
    def _in(self, node, i, x):
        if isinstance(x, (int, float)):
            node.inputs[i].default_value = x
        elif isinstance(x, tuple):
            node.inputs[i].default_value = x
        else:
            self.link(x, node.inputs[i])
    def math(self, op, a, b=None, clamp=False):
        m = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        self._in(m, 0, a)
        if b is not None:
            self._in(m, 1, b)
        return m.outputs[0]
    def vmath(self, op, a, b=None, scale=None):
        m = self.n("ShaderNodeVectorMath", operation=op)
        self._in(m, 0, a)
        if b is not None:
            self._in(m, 1, b)
        if scale is not None:
            self._in(m, 3 if len(m.inputs) > 3 else 2, scale)
        out = m.outputs["Value"] if op in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else m.outputs["Vector"]
        return out
    def vec(self, x, y, z):
        c = self.n("ShaderNodeCombineXYZ")
        self._in(c, 0, x); self._in(c, 1, y); self._in(c, 2, z)
        return c.outputs[0]
    def rgb_scale(self, color, s):
        return self.vmath("SCALE", color, scale=s)
    def attr(self, name, kind="OBJECT"):
        a = self.n("ShaderNodeAttribute", attribute_type=kind, attribute_name=name)
        return a

def body_material(name, key_dir, key_color, extras, ambient,
                  fog_color, fog_density, sky_dir=(0.0, 0.0, 1.0),
                  wrap=0.45, key_gain=1.0, trans_scale=1.3, trans_power=4.0, trans_distortion=0.35,
                  rim_power=1.75, rim_strength=1.3, glow_strength=0.22, leaf_sky_glow=0.6,
                  mottle_cells=26.0, mottle_strength=0.14):
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    nt.nodes.clear()
    g = G(nt)
    out = g.n("ShaderNodeOutputMaterial")
    geo = g.n("ShaderNodeNewGeometry")
    N, V = geo.outputs["Normal"], geo.outputs["Incoming"]
    tc = g.n("ShaderNodeTexCoord")

    o = lambda k: g.attr(k).outputs
    base = o("base")["Vector"]
    rim_c = o("rim")["Vector"]
    tint = o("trans_tint")["Vector"]
    tgain = o("trans_gain")["Fac"]
    sheen = o("sheen")["Fac"]
    reserve = o("reserve")["Fac"]
    rim_scale = o("rim_scale")["Fac"]
    shade = o("shade")["Fac"]
    warmth = o("warmth")["Fac"]
    va = g.attr("evo_a", "GEOMETRY").outputs["Vector"]
    sep = g.n("ShaderNodeSeparateXYZ"); g.link(va, sep.inputs[0])
    thickness = g.math("MULTIPLY", sep.outputs[0], sep.outputs[2])   # the lens's, times the vein's through
    vein_face = sep.outputs[1]
    leaf_tone = g.attr("evo_tone", "GEOMETRY").outputs["Vector"]

    # The mottle: Voronoi in metres on the part's own coordinates, walls from F2 - F1 of the
    # squared distances, the tone the nearest cell's.
    pos = g.vmath("SCALE", tc.outputs["Object"], scale=mottle_cells)
    v1 = g.n("ShaderNodeTexVoronoi", feature="F1"); g.link(pos, v1.inputs["Vector"])
    v2 = g.n("ShaderNodeTexVoronoi", feature="F2"); g.link(pos, v2.inputs["Vector"])
    for v in (v1, v2):
        v.inputs["Randomness"].default_value = 1.0
    f1 = g.math("POWER", v1.outputs["Distance"], 2.0)
    f2 = g.math("POWER", v2.outputs["Distance"], 2.0)
    wall = g.math("MULTIPLY", g.math("SUBTRACT", f2, f1), 2.5, clamp=True)
    sepc = g.n("ShaderNodeSeparateColor"); g.link(v1.outputs["Color"], sepc.inputs[0])
    tone = sepc.outputs[0]
    mot = g.math("ADD", 1.0 - mottle_strength, g.math("MULTIPLY", tone, 2.0 * mottle_strength))
    mot2 = g.math("ADD", 1.0 - 0.7 * mottle_strength, g.math("MULTIPLY", wall, 0.7 * mottle_strength))
    mottle = g.math("MULTIPLY", mot, mot2)
    body = g.rgb_scale(g.rgb_scale(base, mottle), vein_face)

    def wrapped(L):
        d = g.vmath("DOT_PRODUCT", N, L)
        return g.math("DIVIDE", g.math("ADD", d, wrap), 1.0 + wrap, clamp=True)

    def transmission(L, color):
        bent = g.vmath("NORMALIZE", g.vmath("ADD", L, g.vmath("SCALE", N, scale=trans_distortion)))
        back = g.math("POWER", g.math("MAXIMUM", g.math("MULTIPLY", g.vmath("DOT_PRODUCT", V, bent), -1.0), 0.0), trans_power)
        k = g.math("MULTIPLY", g.math("MULTIPLY", back, trans_scale), thickness)
        return g.vmath("SCALE", color, scale=k)

    Lk, Ls = g.vec(*key_dir), g.vec(*sky_dir)
    Ck = g.vec(*key_color)

    lit = g.vmath("MULTIPLY", body, g.vmath("SCALE", Ck, scale=g.math("MULTIPLY", wrapped(Lk), key_gain)))
    tk = g.vmath("MULTIPLY", tint, g.vmath("SCALE", transmission(Lk, Ck), scale=tgain))
    lit = g.vmath("ADD", lit, tk)
    # the sky glow through a blade: gated by the rim scale, which is 0.45 on a blade and 1 off it
    is_leaf = g.math("LESS_THAN", rim_scale, 0.9)
    ts = g.vmath("MULTIPLY", tint, g.vmath("SCALE", transmission(Ls, Ck), scale=g.math("MULTIPLY", g.math("MULTIPLY", tgain, leaf_sky_glow), is_leaf)))
    lit = g.vmath("ADD", lit, ts)
    half = g.vmath("NORMALIZE", g.vmath("ADD", Lk, V))
    spec = g.math("MULTIPLY", sheen, g.math("POWER", g.math("MAXIMUM", g.vmath("DOT_PRODUCT", N, half), 0.0), 28.0))
    lit = g.vmath("ADD", lit, g.vmath("SCALE", Ck, scale=spec))
    # every other light: the fill, and a portrait's back light (the shader's additional lights)
    for d, c in extras:
        Lx, Cx = g.vec(*d), g.vec(*c)
        lit = g.vmath("ADD", lit, g.vmath("MULTIPLY", body, g.vmath("SCALE", Cx, scale=wrapped(Lx))))
        lit = g.vmath("ADD", lit, g.vmath("MULTIPLY", tint, g.vmath("SCALE", transmission(Lx, Cx), scale=tgain)))
    # ambient: the trilight (sky, equator, ground) by how far the normal faces up or down
    sky, equator, ground = ambient
    sepn = g.n("ShaderNodeSeparateXYZ"); g.link(N, sepn.inputs[0])
    up = g.math("MAXIMUM", sepn.outputs[2], 0.0)
    down = g.math("MAXIMUM", g.math("MULTIPLY", sepn.outputs[2], -1.0), 0.0)
    amb = g.vmath("ADD", g.vec(*equator), g.vmath("SCALE", g.vec(*[a - b for a, b in zip(sky, equator)]), scale=up))
    amb = g.vmath("ADD", amb, g.vmath("SCALE", g.vec(*[a - b for a, b in zip(ground, equator)]), scale=down))
    lit = g.vmath("ADD", lit, g.vmath("MULTIPLY", body, amb))
    facing = g.math("SUBTRACT", 1.0, g.math("MAXIMUM", g.math("MINIMUM", g.vmath("DOT_PRODUCT", N, V), 1.0), 0.0))
    rim = g.math("POWER", facing, rim_power)
    lit = g.vmath("ADD", lit, g.vmath("SCALE", rim_c, scale=g.math("MULTIPLY", g.math("MULTIPLY", rim, rim_strength), rim_scale)))
    glow = g.math("MULTIPLY", g.math("MULTIPLY", g.math("MULTIPLY", reserve, glow_strength), rim_scale),
                  g.math("ADD", 0.30, g.math("MULTIPLY", 0.70, g.math("POWER", facing, 0.7))))
    lit = g.vmath("ADD", lit, g.vmath("SCALE", rim_c, scale=glow))
    # the intro's own light: an object may glow outright in its rim colour (a knuckle, say)
    emit = o("emit")["Fac"]
    lit = g.vmath("ADD", lit, g.vmath("SCALE", rim_c, scale=emit))
    # the blade's tone, with the mottle's share of it (leafTone *= lerp(1, mottle, 0.7))
    mshare = g.math("ADD", 0.3, g.math("MULTIPLY", mottle, 0.7))
    is_leaf_t = g.math("LESS_THAN", rim_scale, 0.9)
    tone_m = g.math("ADD", 1.0, g.math("MULTIPLY", g.math("SUBTRACT", mshare, 1.0), is_leaf_t))
    lit = g.vmath("MULTIPLY", lit, g.vmath("SCALE", leaf_tone, scale=tone_m))
    # every cell's own shade and warmth
    sh = g.math("ADD", 1.0, g.math("MULTIPLY", shade, 0.08))
    warm = g.vec(g.math("ADD", 1.0, g.math("MULTIPLY", warmth, 0.05)), 1.0, g.math("SUBTRACT", 1.0, g.math("MULTIPLY", warmth, 0.07)))
    lit = g.vmath("MULTIPLY", g.vmath("SCALE", lit, scale=sh), warm)
    # the fog: exponential squared in the distance to the eye, towards the water's colour
    dist = g.n("ShaderNodeLightPath").outputs["Ray Length"]
    fog = g.math("EXPONENT", g.math("MULTIPLY", -1.0, g.math("POWER", g.math("MULTIPLY", dist, fog_density), 2.0)))
    mix = g.n("ShaderNodeMix", data_type="RGBA")
    g.link(fog, mix.inputs["Factor"])
    mix.inputs["A"].default_value = (*fog_color, 1.0)
    g.link(lit, mix.inputs["B"])
    em = g.n("ShaderNodeEmission")
    g.link(mix.outputs["Result"], em.inputs["Color"])
    em.inputs["Strength"].default_value = 1.0
    g.link(em.outputs[0], out.inputs["Surface"])
    return m
