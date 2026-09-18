# -*- coding: utf-8 -*-
"""BedShape rebuilt in Python, for round 39's read.

A line-for-line port of `src/Evosim.Core/Environment/BedShape.cs` (D092,
`logbook/specs/bed-spec.md`) and of `Rng` (PCG-XSH-RR 64/32) and `Rng.SeedFor`
(SplitMix64's finaliser), so a reader can rebuild a seed's floor from its
`config.json` and its seed alone.  The C# is the authority; this is checked
against `run.json`'s `bedHollows`, `bedRidges`, `bedRangeMetres` and
`bedSteepestDegrees` before any number taken from it is used (`check()` below).

Why it exists: `run.json` records the bed's counts but not the tilt's bearing,
and E9, E11 and the hollows' occupancy are all readings along that bearing.

Every place the C# holds a `float`, this holds a `numpy.float32`, because the
seed's draws and the tank's radius are single precision and the map is a sum of
cosines of them.
"""
import math
import numpy as np

MULT = 6364136223846793005
MASK = (1 << 64) - 1
BANDS = 3
MODES_PER_BAND = 4
BAND_RATIO = 3.0
SPECTRUM_EXPONENT = 1.5
STEEPEST_SLOPE = 0.57735026918962576
STEEPEST_TILT_SLOPE = 0.57735026918962576
SLOPE_FIT_MARGIN = 0.95
MEASURE_STEP = 0.5
BED_SHAPE_INDEX = (1 << 64) - 1 - 6          # World.BedShapeIndex


def seed_for(stream, index):
    z = (stream * 0x9E3779B97F4A7C15 + index) & MASK
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK
    return z ^ (z >> 31)


class Rng:
    def __init__(self, seed, sequence=1):
        self._inc = ((sequence << 1) | 1) & MASK
        self._state = 0
        self.next_uint()
        self._state = (self._state + seed) & MASK
        self.next_uint()

    def next_uint(self):
        old = self._state
        self._state = (old * MULT + self._inc) & MASK
        xorshifted = (((old >> 18) ^ old) >> 27) & 0xFFFFFFFF
        rot = old >> 59
        return ((xorshifted >> rot) | (xorshifted << ((-rot) & 31))) & 0xFFFFFFFF

    def next_float(self):
        return np.float32((self.next_uint() >> 8) * (1.0 / 16777216.0))


def radius_for(area):
    """TankGeometry.RadiusFor: (float)sqrt(area/pi)."""
    return np.float32(math.sqrt(area / math.pi))


class BedShape:
    def __init__(self, tank_radius, depth, relief, tilt, scale, seed):
        tank_radius = np.float32(tank_radius)
        self.RadiusMetres = tank_radius
        self.DepthMetres = np.float32(depth)
        self.ReliefMetres = np.float32(relief)
        self.TiltMetres = np.float32(tilt)
        self._radius = float(tank_radius)

        self.ScaleMetres = (np.float32(scale) if scale > 0
                            else np.float32(2.0 * float(tank_radius) / 3.0))
        self.HasRelief = relief > 0 or tilt > 0
        self.HollowRadiusMetres = 0.25 * float(self.ScaleMetres)
        if not self.HasRelief:
            self._amp = np.zeros(0)
            return

        modes = BANDS * MODES_PER_BAND
        kx = np.zeros(modes)
        kz = np.zeros(modes)
        amp = np.zeros(modes)
        phase = np.zeros(modes)

        rng = Rng(seed)
        self.TiltDirectionRadians = np.float32(2.0 * math.pi * float(rng.next_float()))
        td = float(self.TiltDirectionRadians)
        self._tiltX = tilt * math.cos(td) / (2.0 * self._radius)
        self._tiltZ = tilt * math.sin(td) / (2.0 * self._radius)

        for band in range(BANDS):
            wavelength = float(self.ScaleMetres) / (BAND_RATIO ** band)
            band_k = 2.0 * math.pi / wavelength
            for i in range(MODES_PER_BAND):
                m = band * MODES_PER_BAND + i
                theta = 2.0 * math.pi * float(rng.next_float())
                k = band_k * (0.8 + 0.4 * float(rng.next_float()))
                kx[m] = k * math.cos(theta)
                kz[m] = k * math.sin(theta)
                phase[m] = 2.0 * math.pi * float(rng.next_float())
                amp[m] = (k / (2.0 * math.pi / float(self.ScaleMetres))) ** (-SPECTRUM_EXPONENT)

        # ---------------------------------------------------------- the fit
        side = int(math.ceil(2.0 * self._radius / MEASURE_STEP))
        if side < 2:
            side = 2
        grid = (np.arange(side) + 0.5) * MEASURE_STEP
        X, Z = np.meshgrid(grid, grid, indexing='ij')
        dX = X - self._radius
        dZ = Z - self._radius
        inside = (dX * dX + dZ * dZ) <= self._radius * self._radius
        cx = dX[inside]
        cz = dZ[inside]

        chi = np.outer(cx, kx) + np.outer(cz, kz) + phase
        raw_h = np.cos(chi) @ amp
        raw_gx = -(np.sin(chi) @ (amp * kx))
        raw_gz = -(np.sin(chi) @ (amp * kz))

        raw_range = raw_h.max() - raw_h.min()
        alpha = relief / raw_range if raw_range > 0 and relief > 0 else 0.0

        def steepest_bands(a):
            return float(np.max(np.hypot(a * raw_gx, a * raw_gz)))

        def steepest_total(a):
            return float(np.max(np.hypot(a * raw_gx + self._tiltX, a * raw_gz + self._tiltZ)))

        target = SLOPE_FIT_MARGIN * STEEPEST_SLOPE
        binds = False
        if alpha > 0 and steepest_bands(alpha) > target:
            binds = True
            low, high = 0.0, alpha
            for _ in range(40):
                mid = 0.5 * (low + high)
                if steepest_bands(mid) > target:
                    high = mid
                else:
                    low = mid
            alpha = low
        self.SlopeBoundBinds = binds

        amp = amp * alpha
        self._kx, self._kz, self._amp, self._phase = kx, kz, amp, phase

        band_h = alpha * raw_h
        full = band_h + self._tiltX * cx + self._tiltZ * cz
        self._offset = float(full.sum() / full.size)
        h = full - self._offset

        self.RangeMetres = float(band_h.max() - band_h.min())
        self.TotalRangeMetres = float(h.max() - h.min())
        self.HighestMetres = float(h.max())
        self.LowestMetres = float(h.min())
        self.SteepestSlopeRadians = math.atan(steepest_bands(alpha))
        self.SteepestTotalSlopeRadians = math.atan(steepest_total(alpha))

        self.Hollows, self.Ridges = self._count_places(band_h, cx, cz)

    def _count_places(self, height, cx, cz):
        """BedShape.CountPlaces, on the same half-metre lattice."""
        r = self.HollowRadiusMetres
        r2 = r * r
        inner2 = 0.64 * r2
        threshold = 0.25 * self.RangeMetres
        n = height.size
        from_axis = np.hypot(cx, cz)
        judge = (from_axis + r) <= self._radius

        # The lattice is regular, so a neighbourhood is a window of offsets.
        step = MEASURE_STEP
        ix = np.rint((cx + self._radius - 0.5 * step) / step).astype(int)
        iz = np.rint((cz + self._radius - 0.5 * step) / step).astype(int)
        side = max(ix.max(), iz.max()) + 1
        idx = -np.ones((side + 1, side + 1), dtype=int)
        idx[ix, iz] = np.arange(n)

        reach = int(math.floor(r / step)) + 1
        lowest = judge.copy()
        highest = judge.copy()
        ring_sum = np.zeros(n)
        ring_count = np.zeros(n, dtype=int)

        for ox in range(-reach, reach + 1):
            for oz in range(-reach, reach + 1):
                if ox == 0 and oz == 0:
                    continue
                d2 = (ox * step) ** 2 + (oz * step) ** 2
                if d2 > r2:
                    continue
                jx = ix + ox
                jz = iz + oz
                ok = (jx >= 0) & (jx <= side) & (jz >= 0) & (jz <= side)
                j = np.where(ok, idx[np.clip(jx, 0, side), np.clip(jz, 0, side)], -1)
                have = j >= 0
                hj = np.where(have, height[np.clip(j, 0, n - 1)], 0.0)
                lowest &= ~(have & (hj <= height))
                highest &= ~(have & (hj >= height))
                if d2 >= inner2:
                    ring_sum += np.where(have, hj, 0.0)
                    ring_count += have.astype(int)

        ok = judge & (ring_count > 0)
        ring = np.where(ring_count > 0, ring_sum / np.maximum(ring_count, 1), 0.0)
        is_hollow = ok & lowest & ((ring - height) >= threshold)
        is_ridge = ok & highest & ((height - ring) >= threshold)
        # Kept so a read can ask where a hollow is, not only how many there are.
        self.HollowCentres = [(float(cx[i] + self._radius), float(cz[i] + self._radius))
                              for i in np.flatnonzero(is_hollow)]
        self.RidgeCentres = [(float(cx[i] + self._radius), float(cz[i] + self._radius))
                             for i in np.flatnonzero(is_ridge)]
        return int(np.count_nonzero(is_hollow)), int(np.count_nonzero(is_ridge))

    def height(self, x, z):
        """BedShape.Height, vectorised."""
        if not self.HasRelief:
            return np.zeros_like(np.asarray(x, dtype=float))
        dx = np.asarray(x, dtype=float) - self._radius
        dz = np.asarray(z, dtype=float) - self._radius
        h = self._tiltX * dx + self._tiltZ * dz - self._offset
        for m in range(self._amp.size):
            h = h + self._amp[m] * np.cos(self._kx[m] * dx + self._kz[m] * dz + self._phase[m])
        return h

    def floor_y(self, x, z):
        return -float(self.DepthMetres) + self.height(x, z)

    def tilt_unit(self):
        """The unit vector pointing UP the ramp, toward the shallow arc."""
        n = math.hypot(self._tiltX, self._tiltZ)
        return (self._tiltX / n, self._tiltZ / n)


def from_config(cfg, seed):
    """The bed of a run, from its config.json tree and its seed."""
    def find(node, name):
        if isinstance(node, dict):
            if name in node:
                return node[name]
            for v in node.values():
                r = find(v, name)
                if r is not None:
                    return r
        return None

    area = find(cfg, 'worldAreaSquareMetres')
    depth = find(cfg, 'worldDepthMetres')
    relief = find(cfg, 'bedReliefMetres')
    tilt = find(cfg, 'bedTiltMetres')
    scale = find(cfg, 'bedScaleMetres')
    r = radius_for(area)
    return BedShape(r, depth, relief, tilt, scale, seed_for(seed, BED_SHAPE_INDEX))


def check(bed, manifest):
    """The rebuild against what the build recorded, field by field."""
    out = []
    out.append(('hollows', bed.Hollows, manifest['bedHollows']))
    out.append(('ridges', bed.Ridges, manifest['bedRidges']))
    out.append(('range m', round(bed.RangeMetres, 6), manifest['bedRangeMetres']))
    out.append(('steepest deg', round(math.degrees(bed.SteepestTotalSlopeRadians), 6),
                round(manifest['bedSteepestDegrees'], 6)))
    out.append(('scale m', round(float(bed.ScaleMetres), 6), round(manifest['bedScale'], 6)))
    return out


if __name__ == '__main__':
    import json, os, sys
    root = r'D:\Projects\experiments\evolution-simulator'
    for arm, rd in [('r39-s1', '2026-09-15-213428-8eab1085'),
                    ('r39-s2', '2026-09-15-213503-8eab1085'),
                    ('r39-s3', '2026-09-17-085758-8eab1085'),
                    ('r39-s4', '2026-09-17-085833-8eab1085'),
                    ('r39-s5', '2026-09-17-085303-8eab1085')]:
        d = os.path.join(root, 'runs', arm, rd)
        cfg = json.load(open(os.path.join(d, 'config.json'), encoding='utf-8'))
        man = json.load(open(os.path.join(d, 'run.json'), encoding='utf-8'))
        bed = from_config(cfg, man['seed'])
        print(arm, 'tilt bearing %.4f rad (%.2f deg)' % (
            float(bed.TiltDirectionRadians), math.degrees(float(bed.TiltDirectionRadians))))
        for name, mine, theirs in check(bed, man):
            print('   %-14s rebuilt %-12s recorded %-12s %s'
                  % (name, mine, theirs, 'ok' if abs(float(mine) - float(theirs)) < 1e-4 else 'DIFFERS'))
