"""Shared by the parents, bars and founder scenes (stage 2): the measured numbers, the design camera, and the small
helpers each scene uses. Imports Blender's bpy through g_scene.

MEASURED (all from the sea run's tank exam; nothing here is drawn):
  EATEN[pool]  joules eaten in the 1,800 s tank, own brain: pool/index.tsv's ownEaten, equal to the exam's `eaten`.
  NET[pool]    the same exam's `net`: end energy less start energy, in joules (src/Evosim.Nursery/Tank.cs, Net =
               end - start, end 0 for a body that died). Read from scratch/kit-arm/runs/forager-kit-shaped/tank-exam.jsonl,
               the rows with brain "own"; genome N of the exam is pool index N (g2.13 = genome 2: eaten 408.75, net +179.47,
               the figures of facts.md L907-920 and fact-check-1's founder row).
  BRACKET_MEAN the mean ownEaten of the Guzzler pool genomes in each generation bracket (index.tsv ids gN.M).
The tube scale is one for every graphic that draws the Guzzler pool: K_J puts the fullest pool eater (pool 23, 2,449.9 J)
at the design's fullest level, 0.86 of a tube; a vial's savings or hollow is read on the same joule scale.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import g_layout as L
import g_scene as G
import g_tank

EATEN = {0: 243.8, 1: 273.3, 2: 408.8, 3: 371.3, 4: 515.3, 5: 531.8, 6: 442.7, 7: 957.0, 8: 1066.8, 9: 799.3, 10: 1156.8,
         11: 1236.2, 12: 1041.8, 13: 1298.6, 14: 1400.9, 15: 1288.1, 16: 1847.5, 17: 1664.1, 18: 1631.3, 19: 1963.9,
         20: 1997.0, 21: 2076.3, 22: 1994.3, 23: 2449.9, 24: 1664.6, 25: 1999.4, 26: 2057.8, 27: 1853.2}
NET = {2: 179.47, 5: 319.6, 7: 143.2, 9: 86.4, 10: -400.6, 11: -243.9, 14: 162.2, 19: -97.0, 21: -57.5, 23: 45.3, 26: -255.0}
BRACKET_MEAN = {"g0-4": 362.5, "g8-12": 699.9, "g16-20": 1196.5, "g32-36": 1980.5, "g37-40": 2013.6}
K_J = 0.86 / 2449.9
FPS = G.FPS


def frac(joules):
    return joules * K_J


def key_of(pool):
    return "founder" if pool == 2 else "guz-%d" % pool


def camera(focus=None, fstop=1.4):
    """The tank's design camera (long lens, lens shift), refocused. Returns (Camera, plane depth)."""
    m, d = g_tank.camera_model()
    return L.Camera(m.lens, m.pos, m.pp, fstop, focus if focus is not None else d), d


def kplace(K, b, pos, yaw, roll, scale, frame):
    """Place a body root through the Keyer (keys only changes)."""
    from mathutils import Quaternion
    import math
    K.set(b.root, "location", pos, frame)
    K.set(b.root, "rotation_quaternion",
          Quaternion((0, 1, 0), math.radians(roll)) @ Quaternion((0, 0, 1), math.radians(yaw)), frame)
    K.set(b.root, "scale", (scale, scale, scale), frame)
    b.fit_cell(pos, scale)             # the skin's cell follows the pixel; the finest placement stands


HIDDEN = 1e-4
