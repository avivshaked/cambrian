"""The four seas: (a) "We released both teams together into four separate open seas. Every sea got both teams. This film follows
one of the four." and (d) "And in the other three seas, the Guzzlers lost. So what happened in this sea was not a win for the
Guzzlers. It was a win for one family." Slots seas-a (216 frames, beat 5 4.40-11.60) and seas-d (300 frames, beat 10 0-10 s),
after the designer's frames-v5/seas-a1, a2, d1, d2 (design/src/seas.html).

    blender -b --factory-startup --python g_seas.py -- <out dir> <percent> <samples> [frames] --slot seas-a|seas-d [--t S,S] [--dry]

Four `sea_column`s at the design's x (270, 723, 1177, 1630), eight bodies each, placed by g_sealay.py (the design's own scatter).
Counts, as the owner approved on 2026-11-14: (a) every sea 4 Guzzlers and 4 Nibblers; (d) this sea (the third) 4 taleplums,
1 other Guzzler and 3 Nibblers, the others 2, 1 and 0 Guzzlers with Nibblers making up eight. Bodies come from the real sea run's
pools: the Guzzlers the pool genomes guz-N (the shaped arm's tank pool, the teams the round released), the Nibblers nib-N
(pools 28-72) and the taleplums tp-ID (the sea run's own taleplums: 13929's descendants, drawn in the founder's palette, the pale
maze skin with the pink tip). One body is one sea's body in every frame, so they differ in size by their genome; each is scaled so
its long axis is the design's size (112 Guzzler, 100 Nibbler, 118 taleplum).

Motion (notes/graphics.md). (a): bodies sink in from the surface and settle, 0.1 s apart from 0.0 s, over 0.6 s each; "this film
follows" (cue this_film) the third sea comes forward 22% over 1.2 s, as a dolly toward it, and the others go to fade .72, further
back (out of focus). (d): the cut opens on the end counts; "this sea" (cue this_sea) the third sea comes forward as in (a); just
before "family" (cue family less 0.3 s, over 0.6 s) the four taleplums are lit and the sea's others dim, so they are the last thing
seen. Layout anchors are computed from the design's scatter, never measured off the PNG.
"""
import math, os, random, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G
import g_common as C
import g_sealay as SL

PLANE = 7.65
BACK_Z = 2.4           # a back sea sits this much deeper (the same pixels, blurred by the lens)
GUZ_POOLS = [23, 21, 26, 19, 20, 22, 25, 27, 24, 18, 17, 16, 14, 15]
TP_KEYS = ["tp-42605", "tp-74039", "tp-118251", "tp-166382", "tp-179291", "tp-131141"]


def main():
    args = G.parse()
    scene = args.slot or "seas-a"
    after = scene == "seas-d"
    cues = G.load_cues()
    model, d = C.camera()
    run = G.Run(args, cues, scene, model)
    rng = random.Random(41)
    K = G.Keyer()
    END = run.N / G.FPS
    cu = cues["cues"]

    G.water_gradient(2.0 ** -G.EXPOSURE * 0.4)           # the design lays #020c11 at .62 over the water
    cols = []
    for i, cx in enumerate(SL.CX):
        cols.append(G.sea_column(run, cx, SL.W, 100 + i))
    mats = G.body_material(run)
    nib_keys = sorted(k for k in G.S.BODIES if k.startswith("nib-"))
    ng = nn = 0
    seas = []
    for i in range(4):
        spec = SL.sea_bodies(i, after)
        bl = []
        for j, sp in enumerate(spec):
            if sp["kind"] == "shell":
                key = TP_KEYS[len([b for b in bl if b["kind"] == "shell"])]
                team = "taleplum"
            elif sp["kind"] == "guzzler":
                key = "guz-%d" % GUZ_POOLS[ng % len(GUZ_POOLS)]; ng += 1
                team = "guzzler"
            else:
                key = nib_keys[(nn * 5) % len(nib_keys)]; nn += 1
                team = "nibbler"
            name = "s%d_b%d" % (i, j)
            b = G.add_body(run, key, name, team, mats, 1.0, rng)
            m = model.metres_per_px(PLANE)
            bl.append(dict(sp, body=b, key=key, scale=sp["size"] * m / b.length, name=name))
        seas.append(bl)

    G.snow(run, rng, count=1100)
    G.S.grade()

    OURS = SL.OURS
    t_fwd = cu["this_film"]["t"] if not after else cu["this_sea"]["t"]
    FWD = (t_fwd, t_fwd + 1.2)
    LIT = (cu["family"]["t"] - 0.3, cu["family"]["t"] + 0.3) if after else None

    def sea_state(i, t):
        """(k, back): the dolly factor and how far back the sea has gone, at scene time t."""
        e = L.span(t, *FWD)
        e = e * e * (3 - 2 * e)
        if (after or True) and i == OURS:
            return 1.0 + 0.22 * e, 0.0
        return 1.0, e

    def pos(sp, i, t):
        k, back = sea_state(i, t)
        x, y = SL.forward_k(k, SL.CX[i])(sp["x"], sp["y"])
        return x, y, sp["size"] * k, back

    # anchors: one per body, on its root
    for i, bl in enumerate(seas):
        for j, sp in enumerate(bl):
            run.anchor("s%d_b%d" % (i, j), 0, 0, 1, parent=sp["body"].root)

    for f in range(1, run.N + 1):
        t = (f - 1) / G.FPS
        for i, bl in enumerate(seas):
            k, back = sea_state(i, t)
            for o in cols[i]:
                K.set(o, "dimk", 0.72 * back, f)
            for j, sp in enumerate(bl):
                b = sp["body"]
                x, y, size, back = pos(sp, i, t)
                if not after:
                    delay = 0.1 * j
                    s = L.span(t, delay, delay + 0.6)
                    if t < delay:
                        C.kplace(K, b, (0, 0, 0), 0, 0, C.HIDDEN, f)
                        continue
                    y = L.lerp(120.0, y, 1 - (1 - s) ** 2)
                    appear = L.span(t, delay, delay + 0.4)
                else:
                    appear = 1.0
                depth = PLANE * (1 + (BACK_Z - 1) * back)
                sc = sp["scale"] * (size / sp["size"]) * (depth / PLANE)
                C.kplace(K, b, model.to_world(x, y, depth), -65 if sp["flip"] else 65, sp["rot"], sc, f)
                b.swim(t + 0.37 * j + 0.9 * i, f)
                fade = 0.85 * back
                K.set(b.root, "dimk", 1 - appear, f)
                if LIT and i == OURS:
                    lit = L.span(t, *LIT)
                    fade = max(fade, 0.55 * lit * (0.0 if sp["kind"] == "shell" else 1.0))
                K.set(b.root, "fade", fade, f)
                K.set(b.root, "grey", 0.0, f)

    frames = G.frame_list(args, cues, scene)
    lay_sets = ("seas-d1", "seas-d2") if after else ("seas-a1", "seas-a2")
    ok = True
    for name in lay_sets:
        lay = L.load_layout(name)
        check = [1 + int(round(s * G.FPS)) for s in lay["check_s"]]
        ok = run.layout_check(name, check, 0) and ok
    print("LAYOUT %s" % ("ALL WITHIN TOLERANCE" if ok else "SOME ANCHORS OUT OF TOLERANCE"))
    if args.dry:
        run.write_anchors(sorted(set(frames[:3])))
    else:
        run.write_anchors(range(1, run.N + 1))
    run.render(frames, "%s.blend" % scene)


if __name__ == "__main__":
    main()
