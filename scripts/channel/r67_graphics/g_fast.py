"""Faster Cycles settings for round 67's graphics, off unless asked for. g_scene.Run applies tune() after its own render setup.

EVOSIM_G_FAST=1 changes nothing in the picture on the founder scene (a pixel difference of at most 1 in 8 bits against the
default path, frames 92-94), and takes a 4K frame at 64 samples from about 12.4 s to about 7.9 s there:
- the light tree off. The scenes have no lamp; their light is the world and the shaders' own arithmetic. With the tree on,
  Cycles rebuilt it every frame over the skins' 1.7 million emissive triangles (about 1.3 s of sync, 2.2 s a frame in all).
- one tile. With use_auto_tile off, Blender 5.1 still rendered the 4K frame as four 2048 px tiles through a tile buffer on
  disk, then read it back for the denoiser. A single tile larger than the frame skips that (about 1.8 s a frame).

EVOSIM_G_FAST_VOL=1 adds Cycles' biased volume ray marching. Blender 5.1's unbiased volumes rebuild a density octree for every
volume object every frame (the seven shafts and the cones), even when none of them moves or is keyed: about 2.6 s a frame on
the founder scene. Biased marching builds none, and saves about 2.2 s more. It does change the picture slightly: mean 0.1 in
8 bits, but a few hundred pixels of speckle up to 50 levels on the founder's tube meniscus. Judge it by eye before use.
"""
import os

FAST = os.environ.get("EVOSIM_G_FAST") == "1"
FAST_VOL = os.environ.get("EVOSIM_G_FAST_VOL") == "1"


def tune(sc):
    """Applies the flags' settings to scene `sc` (a no-op without them). Returns the list of what it changed, for the log."""
    done = []
    if FAST:
        sc.cycles.use_light_tree = False
        sc.cycles.use_auto_tile = True
        side = max(sc.render.resolution_x, sc.render.resolution_y) * sc.render.resolution_percentage // 100
        sc.cycles.tile_size = max(4096, side)
        done += ["light tree off", "one tile of %d px" % sc.cycles.tile_size]
    if FAST_VOL:
        sc.cycles.volume_biased = True
        done.append("biased volumes")
    if done:
        print("G_FAST: " + ", ".join(done), flush=True)
    return done
