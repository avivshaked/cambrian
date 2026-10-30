"""Genome-to-skin map, step 1 (logbook/specs/theatre-skin-plan.md, section 5): features, projection, skin vector, check.

    python -I skin_features.py <run_dir> --at 24000 --out <folder> [--ref RUN@SECONDS ...] [options]

Reads one run's genomes.jsonl.gz, lineage.jsonl, snapshots/*.jsonl.gz and config.json, read-only and shared, streaming:
only the ids it needs are parsed and each genome is reduced to a feature row and a compact copy for the species
distance at once. Python and numpy only; single-threaded.

What it writes under --out:
  features.csv, parent_child_check.md, projection.json, weights.json, weights_table.md, pairs.csv, skin_vectors.csv,
  summary.md, and pairs_scatter.png when matplotlib is importable.

The reference population. With --ref (repeatable) the projection's mean and spread, and the re-centring of each
parameter, are taken over the non-producers (photosynthetic share of volume under 0.5) alive at each reference run's
snapshot nearest its second, pooled across worlds. Each parameter's weights are then multiplied by a gain and given a bias
so that the reference's median body sits at 0.5 and its p10 and p90 near 0.1 and 0.9. The pairs and the one number are
still the primary run's.

Approximations (also repeated in summary.md): see APPROXIMATIONS below. The development port is exact for box, sphere
and capsule parts and for the part count, volume pruning, mirroring and recursion rules; it takes the genome's own
module counts (the snapshot's per-body counts and lost parts are not read, so a kin pair is compared on genomes alone);
a superellipsoid part's volume and anchor point are exact but its world bounding box is taken from its half-extents.
"""
import argparse
import bisect
import csv
import gzip
import json
import math
import os
import random
import re
import sys
import zlib
from array import array
from collections import Counter, defaultdict

import numpy as np

GZ_ERRORS = (EOFError, OSError, zlib.error)

APPROXIMATIONS = [
    "Adult body = the genome developed at its own adultScale with the genome's own module counts "
    "(snapshot moduleCounts and lostPaths are not read, so an ancestor and a living body are measured alike).",
    "Part volumes, mirroring, recursion, depth/part/volume pruning and the box/sphere/capsule anchor points are a port "
    "of Developer.Expand; MaxBodyReachMetres pruning is applied when the config sets it.",
    "A part's world bounding box uses its half-extents (capsule: radius plus span along its local Y); a "
    "superellipsoid part is boxed by its half-extents and so under-covers a strongly tapered one.",
    "SpeciesDistance is computed in double precision; the C# accumulates in float32, so values agree to about 1e-6 "
    "relative, not bit for bit.",
]

# ------------------------------------------------------------------ reading a run (after scripts/genome-query.py)


class ToolError(Exception):
    """A read or setup failure: main prints it and exits 2; a reference run's failure is reported and the run skipped."""


def fail(message):
    raise ToolError(message)


def read_lines(path, opener):
    """Yield the lines of a file, stopping quietly at a gzip cut mid-stream."""
    try:
        with opener(path) as f:
            while True:
                try:
                    line = f.readline()
                except GZ_ERRORS:
                    return
                if not line:
                    return
                yield line
    except FileNotFoundError:
        return


def open_gz(path):
    return gzip.open(path, 'rt', encoding='utf-8')


def open_text(path):
    return open(path, 'r', encoding='utf-8')


def parse(line):
    try:
        return json.loads(line)
    except ValueError:
        return None


def head_id(line):
    """The id out of a row that opens {"id":N, without parsing the row; None if it does not."""
    if not line.startswith('{"id":'):
        return None
    end = line.find(',', 6)
    if end < 0:
        return None
    try:
        return int(line[6:end])
    except ValueError:
        return None


def snapshot_times(run):
    names = []
    try:
        for n in os.listdir(os.path.join(run, 'snapshots')):
            if n.endswith('.jsonl.gz') and n[:-9].isdigit():
                names.append(int(n[:-9]))
    except FileNotFoundError:
        pass
    return sorted(names)


def alive_at(run, at):
    """(seconds, ordered list of ids) for the snapshot nearest `at`."""
    times = snapshot_times(run)
    if not times:
        fail('%s has no snapshots/*.jsonl.gz' % run)
    t = min(times, key=lambda x: (abs(x - at), x))
    path = os.path.join(run, 'snapshots', '%09d.jsonl.gz' % t)
    ids = []
    seen = set()
    for line in read_lines(path, open_gz):
        r = parse(line)
        if r is not None and 'id' in r and r['id'] not in seen:
            seen.add(r['id'])
            ids.append(r['id'])
    return t, ids


BIRTH_RE = re.compile(r'"id":(\d+),"p":(-?\d+)')
GEN_RE = re.compile(r'"g":(\d+)')


class ParentMap:
    """id -> parent id for every birth in lineage.jsonl, in two flat arrays (rows ascend by id)."""

    def __init__(self):
        self.ids = array('q')
        self.ps = array('q')
        self._ascending = True
        self._dict = None

    def add(self, i, p):
        if self.ids and i <= self.ids[-1]:
            self._ascending = False
        self.ids.append(i)
        self.ps.append(p)

    def finish(self):
        if not self._ascending:
            self._dict = dict(zip(self.ids, self.ps))

    def parent(self, i):
        """The parent id (-1 for a founder), or None when the id has no birth row."""
        if self._dict is not None:
            return self._dict.get(i)
        k = bisect.bisect_left(self.ids, i)
        if k < len(self.ids) and self.ids[k] == i:
            return self.ps[k]
        return None

    def __len__(self):
        return len(self.ids)


def load_parent_map(runs):
    """Parent links from the birth rows of every given run directory (earlier segments of a resumed world first)."""
    pm = ParentMap()
    for run in ([runs] if isinstance(runs, str) else runs):
        for line in read_lines(os.path.join(run, 'lineage.jsonl'), open_text):
            if not line.startswith('{"e":"b"'):
                continue
            m = BIRTH_RE.search(line)
            if m:
                pm.add(int(m.group(1)), int(m.group(2)))
    pm.finish()
    return pm


def load_config(run):
    path = os.path.join(run, 'config.json')
    try:
        with open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        fail('no config.json in %s' % run)


_MISSING = object()


def cfg_get(cfg, key, default=_MISSING, group=None):
    """A config value from the named group, else from any group (config.json is {group: {key: value}})."""
    if group is not None and isinstance(cfg.get(group), dict) and key in cfg[group]:
        return cfg[group][key]
    for group in cfg.values():
        if isinstance(group, dict) and key in group:
            return group[key]
    if key in cfg:
        return cfg[key]
    if default is _MISSING:
        fail('config.json has no "%s" in any group; present groups: %s' % (key, ', '.join(cfg.keys())))
    return default


def species_weights_of(cfg):
    return tuple(float(cfg_get(cfg, k, group='species')) for k in (
        'speciesCellTypeWeight', 'speciesTopologyWeight', 'speciesParameterWeight', 'speciesBrainWeight'))


class Limits:
    def __init__(self, cfg=None):
        d = {} if cfg is None else cfg
        g = (lambda k, dv: cfg_get(d, k, dv, group='development')) if cfg is not None else (lambda k, dv: dv)
        self.max_parts = int(g('maxParts', 16))
        self.max_depth = int(g('maxDepth', 8))
        self.min_part_volume = float(g('minPartVolume', 1e-4))
        self.max_part_volume = float(g('maxPartVolume', 1e6))
        self.min_half_extent = float(g('minPartHalfExtent', 0.01))
        self.max_reach = float(g('maxBodyReachMetres', 0.0))
        self.floors_rigid = bool(g('floorsWeighRigidGroups', False))
        self.scale_once = bool(g('scaleLengthensOnce', False))


def cell_ids_of(cfg):
    ct = cfg.get('cellTypes') if cfg is not None else None
    ids = []
    if isinstance(ct, list):
        for e in ct:
            if isinstance(e, dict) and 'id' in e:
                ids.append(e['id'])
            elif isinstance(e, str):
                ids.append(e)
    elif isinstance(ct, dict):
        for e in ct.get('types', ct.get('items', [])):
            if isinstance(e, dict) and 'id' in e:
                ids.append(e['id'])
    return ids or list(DEFAULT_CELL_IDS)


DEFAULT_CELL_IDS = ['structural', 'link', 'neural', 'photosynthetic', 'absorptive', 'consumer', 'buoyancy',
                    'anchoring', 'fat']
SHAPE_IDS = ['box', 'sphere', 'capsule', 'superellipsoid']

# ------------------------------------------------------------------ SpeciesDistance.Between, ported

LOG_GR = math.log(300.0 / 1.0)  # GrowthReserveRangeMin 1, Max 300 (SpeciesDistance.cs, D241)
FORM_RANGES = ((0.1, 1.0), (0.1, 2.0), (0.25, 4.0))  # profile, section, taper (PartForm.cs)

# node tuple layout
N_CELL, N_SHAPE, N_JOINT, N_DIMS, N_POWER, N_LIFT, N_REC, N_BUOY, N_FORM, N_LIMITS, N_EDGES, N_NEURONS = range(12)


def _f3(d):
    return (d['x'], d['y'], d['z'])


def _neurons(lst):
    out = []
    for n in lst:
        ins = tuple((i['kind'], i['index'], i['channel'], i['constant'], i['weight']) for i in n['inputs'])
        out.append((n['op'], n['frequency'], n['phase'], n['amplitude'], n['bias'], ins))
    return tuple(out)


def compact_for_distance(g):
    """Everything SpeciesDistance.Between reads from a genome row, as tuples (cheap to keep for thousands)."""
    nodes = []
    for n in g['nodes']:
        f = n.get('form')
        form = (f['profile'], f['section'], f['taper']) if f else None
        edges = []
        for e in n['edges']:
            r = e['reflect']
            o = e['orientation']
            edges.append((e['child'], bool(e['terminalOnly']), (bool(r['x']), bool(r['y']), bool(r['z'])),
                          _f3(e['parentAnchor']), _f3(e['childAnchor']), _f3(e['scale']),
                          (o['x'], o['y'], o['z'], o['w'])))
        nodes.append((n['cell'], n['shape'], n['joint'], _f3(n['dimensions']), n['power'], n['lift'],
                      n['recursiveLimit'], n['buoyancyOffset'], form,
                      tuple(l['max'] for l in n['jointLimits']), tuple(edges), _neurons(n['neurons'])))
    rep = g['reproduction']
    gr = g.get('growthReserve')
    return (tuple(nodes), g.get('root', 0), _neurons(g['globalBrain']),
            (rep['brood'], rep['investment'], rep['margin'], rep['mode'], rep['gestation']),
            g['adultScale'], gr)


C_NODES, C_ROOT, C_GLOBAL, C_REPRO, C_ADULT, C_GROWTH = range(6)


def _rel(x, y):
    d = abs(x) + abs(y)
    return 0.0 if d < 1e-9 else abs(x - y) / d


def _bounded(a, b):
    return (abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])) / 6.0


def _ang_q(a, b):
    dot = abs(a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3])
    if dot > 1.0:
        dot = 1.0
    return (2.0 * math.acos(dot)) / math.pi


def _ang_phase(a, b):
    two_pi = 6.28318530718
    delta = math.fmod(abs(a - b), two_pi)
    if delta > two_pi * 0.5:
        delta = two_pi - delta
    return delta / math.pi


def _log_gap(x, y, lo, hi):
    return abs(math.log(x) - math.log(y)) / math.log(hi / lo)


def _cell_units(a, b):
    counts = Counter(n[N_CELL] for n in a[C_NODES])
    counts.subtract(Counter(n[N_CELL] for n in b[C_NODES]))
    return sum(abs(v) for v in counts.values()) * 0.5


def _neuron_topology(na, nb):
    units = float(abs(len(na) - len(nb)))
    for i in range(min(len(na), len(nb))):
        if na[i][0] != nb[i][0]:
            units += 1.0
        ia, ib = na[i][5], nb[i][5]
        units += abs(len(ia) - len(ib))
        for k in range(min(len(ia), len(ib))):
            if ia[k][0] != ib[k][0]:
                units += 1.0
            elif ia[k][0] == 'Sensor':
                if ia[k][2] != ib[k][2]:
                    units += 1.0
            elif ia[k][1] != ib[k][1]:
                units += 1.0
    return units


def _topology_units(a, b):
    na, nb = a[C_NODES], b[C_NODES]
    units = float(abs(len(na) - len(nb)))
    if a[C_ROOT] != b[C_ROOT]:
        units += 1.0
    for i in range(min(len(na), len(nb))):
        x, y = na[i], nb[i]
        if x[N_SHAPE] != y[N_SHAPE]:
            units += 1.0
        if x[N_JOINT] != y[N_JOINT]:
            units += 1.0
        ea, eb = x[N_EDGES], y[N_EDGES]
        units += abs(len(ea) - len(eb))
        for k in range(min(len(ea), len(eb))):
            if ea[k][0] != eb[k][0]:
                units += 1.0
            if ea[k][1] != eb[k][1]:
                units += 1.0
            if ea[k][2] != eb[k][2]:
                units += 1.0
        units += _neuron_topology(x[N_NEURONS], y[N_NEURONS])
    units += _neuron_topology(a[C_GLOBAL], b[C_GLOBAL])
    return units


def _parameter_units(a, b):
    ra, rb = a[C_REPRO], b[C_REPRO]
    units = _rel(ra[0], rb[0]) + _rel(ra[1], rb[1]) + _rel(ra[2], rb[2]) + _rel(a[C_ADULT], b[C_ADULT])
    gestation = (1.0 if ra[3] != rb[3] else 0.0) + _rel(ra[4], rb[4])
    if gestation != 0.0:
        units += gestation
    if a[C_GROWTH] is not None and b[C_GROWTH] is not None:
        units += _log_gap(a[C_GROWTH], b[C_GROWTH], 1.0, 300.0)
    na, nb = a[C_NODES], b[C_NODES]
    for i in range(min(len(na), len(nb))):
        x, y = na[i], nb[i]
        units += _rel(x[N_DIMS][0], y[N_DIMS][0]) + _rel(x[N_DIMS][1], y[N_DIMS][1]) + _rel(x[N_DIMS][2], y[N_DIMS][2])
        units += _rel(x[N_POWER], y[N_POWER])
        units += _rel(x[N_LIFT], y[N_LIFT])
        units += _rel(x[N_REC], y[N_REC])
        units += abs(x[N_BUOY] - y[N_BUOY]) / 2.0
        if x[N_FORM] is not None and y[N_FORM] is not None:
            s = 0.0
            for j in range(3):
                s += _log_gap(x[N_FORM][j], y[N_FORM][j], FORM_RANGES[j][0], FORM_RANGES[j][1])
            units += s / 3.0
        la, lb = x[N_LIMITS], y[N_LIMITS]
        for d in range(min(len(la), len(lb))):
            units += _rel(la[d], lb[d])
        ea, eb = x[N_EDGES], y[N_EDGES]
        for k in range(min(len(ea), len(eb))):
            p, q = ea[k], eb[k]
            units += _bounded(p[3], q[3]) + _bounded(p[4], q[4])
            units += _rel(p[5][0], q[5][0]) + _rel(p[5][1], q[5][1]) + _rel(p[5][2], q[5][2])
            units += _ang_q(p[6], q[6])
    return units


def _neuron_param_units(na, nb):
    units = 0.0
    for i in range(min(len(na), len(nb))):
        x, y = na[i], nb[i]
        units += _rel(x[1], y[1]) + _ang_phase(x[2], y[2]) + _rel(x[3], y[3]) + _rel(x[4], y[4])
        ia, ib = x[5], y[5]
        for k in range(min(len(ia), len(ib))):
            units += _rel(ia[k][4], ib[k][4]) + _rel(ia[k][3], ib[k][3])  # weight, constant
    return units


def _brain_units(a, b):
    units = _neuron_param_units(a[C_GLOBAL], b[C_GLOBAL])
    na, nb = a[C_NODES], b[C_NODES]
    for i in range(min(len(na), len(nb))):
        units += _neuron_param_units(na[i][N_NEURONS], nb[i][N_NEURONS])
    return units


def species_distance(a, b, weights):
    """Port of SpeciesDistance.Between(a, b, cell, topology, parameter, brain) on compact_for_distance tuples."""
    cw, tw, pw, bw = weights
    d = 0.0
    if cw > 0:
        d += cw * _cell_units(a, b)
    if tw > 0:
        d += tw * _topology_units(a, b)
    if pw > 0:
        d += pw * _parameter_units(a, b)
    if bw > 0:
        d += bw * _brain_units(a, b)
    return d


# ------------------------------------------------------------------ development (Developer.Expand, sizes and poses)


def _beta(x, y):
    return math.exp(math.lgamma(x) + math.lgamma(y) - math.lgamma(x + y))


class Superellipsoid:
    """The factors of Superellipsoid.cs that volume and anchor points need (Beta via lgamma)."""

    def __init__(self, profile, section, taper):
        self.e1, self.e2 = float(profile), float(section)
        self.k = (taper - 1.0) / (taper + 1.0)
        s = self.e2 * _beta(self.e2 / 2.0, self.e2 / 2.0)
        w0 = self.e1 * _beta(0.5 * self.e1, self.e1 + 1.0)
        w2 = self.e1 * _beta(1.5 * self.e1, self.e1 + 1.0)
        along = w0 + self.k * self.k * w2
        self.volume_factor = s * along
        self.centre_factor = 2.0 * self.k * w2 / along

    def _unit_f(self, x, t, z):
        r = abs(x) ** (2.0 / self.e2) + abs(z) ** (2.0 / self.e2)
        return r ** (self.e2 / self.e1) + abs(t) ** (2.0 / self.e1)

    def surface_point(self, anchor, h):
        a, b, c = abs(h[0]), abs(h[1]), abs(h[2])
        cube = [max(-1.0, min(1.0, v)) for v in anchor]
        extent = max(abs(v) for v in cube)
        if not extent > 0.0:
            return np.array([0.0, -self.centre_factor * b, 0.0])
        on = [v / extent for v in cube]
        scale = self._unit_f(on[0], on[1], on[2]) ** (-self.e1 / 2.0)
        u = [v * scale for v in on]
        taper = 1.0 + self.k * u[1]
        return np.array([a * u[0] * taper, b * (u[1] - self.centre_factor), c * u[2] * taper])


_SUP_CACHE = {}


def _sup(form):
    s = _SUP_CACHE.get(form)
    if s is None:
        if len(_SUP_CACHE) > 64:
            _SUP_CACHE.clear()
        s = _SUP_CACHE[form] = Superellipsoid(*form)
    return s


def shape_volume(shape, h, form):
    hx, hy, hz = abs(h[0]), abs(h[1]), abs(h[2])
    if shape == 'box':
        return 8.0 * hx * hy * hz
    if shape == 'sphere':
        r = (hx + hy + hz) / 3.0
        return (4.0 / 3.0) * math.pi * r ** 3
    if shape == 'capsule':
        r = (hx + hz) / 2.0
        span = max(0.0, hy - r)
        return math.pi * r * r * 2.0 * span + (4.0 / 3.0) * math.pi * r ** 3
    if shape == 'superellipsoid':
        if form is None:
            raise ValueError('superellipsoid part without a form')
        return _sup(form).volume_factor * hx * hy * hz
    raise ValueError('unknown shape %r' % shape)


def _normalize(v):
    n = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    if n > 1e-9:
        return np.array([v[0] / n, v[1] / n, v[2] / n])
    return np.array([1.0, 0.0, 0.0])


def surface_point(shape, anchor, h, form):
    hx, hy, hz = abs(h[0]), abs(h[1]), abs(h[2])
    if shape == 'box':
        cl = lambda v: -1.0 if v < -1.0 else (1.0 if v > 1.0 else v)
        return np.array([cl(anchor[0]) * hx, cl(anchor[1]) * hy, cl(anchor[2]) * hz])
    if shape == 'sphere':
        return _normalize(anchor) * ((hx + hy + hz) / 3.0)
    if shape == 'capsule':
        r = (hx + hz) / 2.0
        span = max(0.0, hy - r)
        d = _normalize(anchor)
        target = d * (span + r)
        y = max(-span, min(span, target[1]))
        spine = np.array([0.0, y, 0.0])
        outward = target - spine
        length = math.sqrt(float(outward @ outward))
        return spine + (outward * (r / length) if length > 1e-9 else d * r)
    if shape == 'superellipsoid':
        return _sup(form).surface_point(anchor, h)
    raise ValueError('unknown shape %r' % shape)


def _rot4(q):
    x, y, z, w = q
    n = math.sqrt(x * x + y * y + z * z + w * w)
    if n > 0:
        x, y, z, w = x / n, y / n, z / n, w / n
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    m = np.eye(4)
    m[0, :3] = [1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy)]
    m[1, :3] = [2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx)]
    m[2, :3] = [2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy)]
    return m


def _trans4(t):
    m = np.eye(4)
    m[:3, 3] = t
    return m


def _mirror4(axes):
    return np.diag([-1.0 if axes[0] else 1.0, -1.0 if axes[1] else 1.0, -1.0 if axes[2] else 1.0, 1.0])


def mirror_combinations(reflect):
    active = [i for i in range(3) if reflect[i]]
    n = len(active)
    for mask in range(1 << n):
        combo = [False, False, False]
        for bit in range(n):
            if mask & (1 << bit):
                combo[active[bit]] = True
        yield tuple(combo)


class Part:
    __slots__ = ('node', 'parent', 'depth', 'half', 'pos', 'lin', 'mirrored', 'cell', 'shape', 'form', 'joint',
                 'volume')


def develop(g, lim):
    """Parts of the genome developed at its adult scale: the sizes, poses, cells, shapes, joints of Developer.Develop."""
    nodes = g['nodes']
    root = g.get('root', 0)
    occ = [0] * len(nodes)
    occ[root] = 1
    parts = []
    s0 = float(g['adultScale'])

    def count_for(i):
        return nodes[i]['recursiveLimit']

    def can_enter(i):
        return occ[i] < count_for(i)

    def exhausted(node):
        for e in node['edges']:
            if e['terminalOnly']:
                continue
            if can_enter(e['child']):
                return False
        return True

    def thicken(h):
        f = lim.min_half_extent
        return np.array([v if v > f else f for v in h])

    def expand(ni, T, scale, parent, depth, joint, on_path):
        node = nodes[ni]
        dims = node['dimensions']
        d = np.array([dims['x'], dims['y'], dims['z']])
        half = thicken(np.abs(d * scale))
        f = node.get('form')
        form = (f['profile'], f['section'], f['taper']) if f else None
        shape = node['shape']
        volume = shape_volume(shape, half, form)
        welded = lim.floors_rigid and parent >= 0 and joint == 'Fixed'
        if (not welded and volume < lim.min_part_volume) or volume > lim.max_part_volume:
            return
        if len(parts) >= lim.max_parts:
            return
        pos = T[:3, 3].copy()
        lin = T[:3, :3].copy()
        if lim.max_reach > 0.0:
            hd = math.sqrt(float(half @ half))
            centre = math.sqrt(float(pos @ pos))
            if centre + hd > lim.max_reach:
                return
        p = Part()
        p.node, p.parent, p.depth = ni, parent, depth
        p.half, p.pos, p.lin = half, pos, lin
        p.mirrored = float(np.linalg.det(lin)) < 0.0
        p.cell, p.shape, p.form, p.joint, p.volume = node['cell'], shape, form, joint, volume
        parts.append(p)
        idx = len(parts) - 1
        if depth >= lim.max_depth:
            return
        ex = exhausted(node)
        for e_i, edge in enumerate(node['edges']):
            if bool(edge['terminalOnly']) != ex:
                continue
            c = edge['child']
            if not can_enter(c):
                continue
            child = nodes[c]
            es = np.array([edge['scale']['x'], edge['scale']['y'], edge['scale']['z']])
            key = (ni, e_i)
            if lim.scale_once and key in on_path:
                es = np.array([1.0 if v > 1.0 else v for v in es])
            cscale = scale * es
            cd = child['dimensions']
            chalf = np.abs(np.array([cd['x'], cd['y'], cd['z']]) * cscale)
            cf = child.get('form')
            cform = (cf['profile'], cf['section'], cf['taper']) if cf else None
            pa = edge['parentAnchor']
            ca = edge['childAnchor']
            a_on_parent = surface_point(shape, (pa['x'], pa['y'], pa['z']), half, form)
            a_on_child = surface_point(child['shape'], (ca['x'], ca['y'], ca['z']), chalf, cform)
            o = edge['orientation']
            rot = _rot4((o['x'], o['y'], o['z'], o['w']))
            r = edge['reflect']
            for mirror in mirror_combinations((bool(r['x']), bool(r['y']), bool(r['z']))):
                local = _mirror4(mirror) @ _trans4(a_on_parent) @ rot @ _trans4(-a_on_child)
                occ[c] += 1
                if lim.scale_once:
                    on_path.append(key)
                expand(c, T @ local, cscale, idx, depth + 1, child['joint'], on_path)
                if lim.scale_once:
                    on_path.pop()
                occ[c] -= 1
                if len(parts) >= lim.max_parts:
                    return

    expand(root, np.eye(4), np.array([s0, s0, s0]), -1, 0, 'Fixed', [])
    return parts


# ------------------------------------------------------------------ features (section 5(b))


def feature_table(cell_ids):
    """[(name, group, one-line meaning)] in column order."""
    rows = []
    for c in cell_ids:
        rows.append(('share_%s' % c, 'cell', 'share of the adult body volume in %s parts' % c))
    rows += [
        ('log_parts', 'body', 'natural log of the number of parts'),
        ('log_chain', 'body', 'natural log of the longest root-to-tip chain, in parts'),
        ('mirrored_share', 'body', 'share of parts that are reflected copies'),
        ('bbox_aspect', 'body', 'log of longest over shortest edge of the body bounding box (root frame)'),
        ('part_aspect_median', 'body', 'log of the median part aspect (longest over shortest effective extent)'),
        ('shape_box', 'body', 'share of parts (by count) that are boxes'),
        ('shape_sphere', 'body', 'share of parts that are spheres'),
        ('shape_capsule', 'body', 'share of parts that are capsules'),
        ('shape_superellipsoid', 'body', 'share of parts that are superellipsoids'),
        ('jointed_share', 'body', 'share of parts whose joint moves (not Fixed)'),
        ('investment', 'gene', 'reproductive investment (birth investment, a fraction of the body)'),
        ('log_margin', 'gene', 'log(1 + reserve margin), seconds of standing cost kept after a birth'),
        ('brain_log_neurons', 'brain', 'log(1 + neurons: outputs + hidden units, or node and global neurons)'),
        ('brain_log_links', 'brain', 'log(1 + connections)'),
        ('brain_w_mean', 'brain', 'mean connection weight'),
        ('brain_w_sd', 'brain', 'standard deviation of connection weights'),
        ('brain_w_absmean', 'brain', 'mean absolute connection weight'),
        ('brain_w_pos_share', 'brain', 'share of connection weights above zero'),
        ('brain_w_absp90', 'brain', '90th percentile of absolute connection weight'),
    ]
    return rows


BRAIN_SHRINK_LINKS = 4.0  # connections at which a brain-weight marker counts half (see run_tool)


def brain_summary(g):
    """(neuron count, weights list). Central brain (format 12/14) or the per-node neurons of the older model."""
    b = g.get('brain')
    if b:
        return len(b['outputs']) + len(b['hidden']), [l['w'] for l in b['links']]
    n = 0
    ws = []
    for lst in [nd['neurons'] for nd in g['nodes']] + [g.get('globalBrain', [])]:
        for neuron in lst:
            n += 1
            ws.extend(i['weight'] for i in neuron['inputs'])
    return n, ws


def _part_aspect(p):
    hx, hy, hz = p.half
    if p.shape == 'sphere':
        return 1.0
    if p.shape == 'capsule':
        r = (hx + hz) / 2.0
        return max(1.0, hy / r) if r > 0 else 1.0
    lo, hi = min(hx, hy, hz), max(hx, hy, hz)
    return hi / lo if lo > 0 else 1.0


def _aabb_half(p):
    """Half-extents of the part's world axis-aligned box (about its position)."""
    hx, hy, hz = p.half
    L = np.abs(p.lin)
    if p.shape == 'sphere':
        r = (hx + hy + hz) / 3.0
        return np.array([r, r, r])
    if p.shape == 'capsule':
        r = (hx + hz) / 2.0
        span = max(0.0, hy - r)
        return L[:, 1] * span + r
    return L @ np.array([hx, hy, hz])


def features_of(g, lim, cell_ids, parts=None):
    """One feature row (list of floats in feature_table order) or None when the genome develops nothing."""
    if parts is None:
        parts = develop(g, lim)
    n = len(parts)
    if n == 0:
        return None
    vol = sum(p.volume for p in parts)
    row = []
    for c in cell_ids:
        v = sum(p.volume for p in parts if p.cell == c)
        row.append(v / vol if vol > 0 else 0.0)
    row.append(math.log(n))
    row.append(math.log(max(p.depth for p in parts) + 1))
    row.append(sum(1 for p in parts if p.mirrored) / n)
    lo = np.min([p.pos - _aabb_half(p) for p in parts], axis=0)
    hi = np.max([p.pos + _aabb_half(p) for p in parts], axis=0)
    size = np.maximum(hi - lo, 1e-9)
    row.append(math.log(float(size.max() / size.min())))
    row.append(math.log(float(np.median([_part_aspect(p) for p in parts]))))
    for s in SHAPE_IDS:
        row.append(sum(1 for p in parts if p.shape == s) / n)
    row.append(sum(1 for p in parts if p.joint != 'Fixed') / n)
    rep = g['reproduction']
    row.append(float(rep['investment']))
    row.append(math.log1p(max(0.0, float(rep['margin']))))
    nn, ws = brain_summary(g)
    row.append(math.log1p(nn))
    row.append(math.log1p(len(ws)))
    if ws:
        w = np.array(ws, dtype=float)
        a = np.abs(w)
        row += [float(w.mean()), float(w.std()), float(a.mean()), float((w > 0).mean()), float(np.percentile(a, 90))]
    else:
        row += [0.0] * 5
    return row


# ------------------------------------------------------------------ pairs and statistics


def rankdata(x):
    """Ranks starting at 1, ties given the mean of their ranks."""
    x = np.asarray(x, dtype=float)
    u, inv, cnt = np.unique(x, return_inverse=True, return_counts=True)
    cum = np.cumsum(cnt)
    avg = cum - (cnt - 1) / 2.0
    return avg[inv]


def spearman(x, y):
    if len(x) < 3:
        return float('nan')
    rx, ry = rankdata(x), rankdata(y)
    rx -= rx.mean()
    ry -= ry.mean()
    den = math.sqrt(float(rx @ rx) * float(ry @ ry))
    return float(rx @ ry) / den if den > 0 else float('nan')


def bootstrap_rho(x, y, n_boot, seed):
    if n_boot <= 0 or len(x) < 10:
        return (float('nan'), float('nan'))
    rng = np.random.default_rng(seed)
    x = np.asarray(x)
    y = np.asarray(y)
    out = []
    for _ in range(n_boot):
        idx = rng.integers(0, len(x), len(x))
        out.append(spearman(x[idx], y[idx]))
    out = [v for v in out if not math.isnan(v)]
    if not out:
        return (float('nan'), float('nan'))
    return (float(np.percentile(out, 2.5)), float(np.percentile(out, 97.5)))


class Kin:
    """Ancestor lists (up to `depth` steps) and clades for the population, from the parent map."""

    def __init__(self, pm, ids, depth):
        self.pm = pm
        self.depth = depth
        self.anc = {}
        self._root = {}
        for i in ids:
            chain = []
            cur = i
            for _ in range(depth):
                p = pm.parent(cur)
                if p is None or p < 0:
                    break
                chain.append(p)
                cur = p
            chain += [-1] * (depth - len(chain))
            self.anc[i] = chain

    def A(self, x, j):
        """The ancestor j steps up (A(x, 0) is x), or -1."""
        if j == 0:
            return x
        c = self.anc.get(x)
        return c[j - 1] if c is not None else -1

    def root(self, i):
        """The founder the id descends from (by the full parent chain); the last known ancestor when the chain breaks."""
        path = []
        cur = i
        while True:
            if cur in self._root:
                r = self._root[cur]
                break
            p = self.pm.parent(cur)
            if p is None or p < 0:
                r = cur
                break
            path.append(cur)
            cur = p
        for q in path:
            self._root[q] = r
        self._root[i] = r
        return r

    def lineage_set(self, x):
        s = {x}
        c = self.anc.get(x)
        if c:
            s.update(v for v in c if v >= 0)
        return s


def sample_same_generation(kin, members, js, count, rng):
    """Pairs of population members sharing a j-step ancestor with no closer equal-step ancestor, uniformly at random
    over the pairs of all given j (j=1 siblings, j=2 and 3 cousins). Returns a list of (a, b) with a < b."""
    groups = []
    for j in js:
        by_anc = defaultdict(list)
        for m in members:
            a = kin.A(m, j)
            if a >= 0:
                by_anc[a].append(m)
        for a, lst in by_anc.items():
            if len(lst) >= 2:
                groups.append((j, lst, len(lst) * (len(lst) - 1) // 2))
    total = sum(g[2] for g in groups)
    if total == 0:
        return []

    def valid(a, b, j):
        if a == b:
            return False
        for jj in range(1, j):
            x, y = kin.A(a, jj), kin.A(b, jj)
            if x >= 0 and x == y:
                return False
        return True

    out = []
    seen = set()
    if total <= 6 * count:  # small enough to enumerate every pair
        for j, lst, _ in groups:
            for ai in range(len(lst)):
                for bi in range(ai + 1, len(lst)):
                    a, b = lst[ai], lst[bi]
                    if valid(a, b, j):
                        key = (min(a, b), max(a, b))
                        if key not in seen:
                            seen.add(key)
                            out.append(key)
        rng.shuffle(out)
        return out[:count]
    cum = []
    acc = 0
    for g in groups:
        acc += g[2]
        cum.append(acc)
    attempts = 0
    while len(out) < count and attempts < 60 * count:
        attempts += 1
        j, lst, _ = rng.choices(groups, cum_weights=cum)[0]
        a, b = rng.sample(lst, 2)
        if not valid(a, b, j):
            continue
        key = (min(a, b), max(a, b))
        if key in seen:
            continue
        seen.add(key)
        out.append(key)
    return out


def sample_strangers(kin, members, count, rng):
    """Random pairs from different founders' clades; when fewer than two clades exist, pairs with no shared ancestor
    within the read depth. Returns (pairs, definition string)."""
    roots = {m: kin.root(m) for m in members}
    n_clades = len(set(roots.values()))
    use_clade = n_clades >= 2
    out = []
    seen = set()
    attempts = 0
    sets = {}
    while len(out) < count and attempts < 80 * count and len(members) >= 2:
        attempts += 1
        a, b = rng.sample(members, 2)
        if use_clade:
            if roots[a] == roots[b]:
                continue
        else:
            sa = sets.get(a) or sets.setdefault(a, kin.lineage_set(a))
            sb = sets.get(b) or sets.setdefault(b, kin.lineage_set(b))
            if sa & sb:
                continue
        key = (min(a, b), max(a, b))
        if key in seen:
            continue
        seen.add(key)
        out.append(key)
    if use_clade:
        definition = ('different founder clades (the founder reached by following parent links to the end of '
                      'lineage.jsonl); %d clades in the population' % n_clades)
    else:
        definition = ('fewer than two founder clades in the population (%d), so strangers are pairs with no shared '
                      'ancestor within %d generations' % (n_clades, kin.depth))
    return out, definition


# ------------------------------------------------------------------ parent-and-child check


def pc_check(F, names, alive_rows, pc_idx, str_idx, fail_ratio):
    """Per feature statistics over (child, parent) row pairs and stranger row pairs. F is rows x features."""
    sd = F[alive_rows].std(axis=0) if len(alive_rows) >= 2 else np.zeros(F.shape[1])
    out = []
    for j, name in enumerate(names):
        s = float(sd[j])
        rec = {'feature': name, 'sd': s, 'n_pc': int(len(pc_idx[0])), 'n_str': int(len(str_idx[0]))}
        if len(pc_idx[0]) == 0:
            rec.update(verdict='NO PAIRS', unch_exact=float('nan'), unch_1pct=float('nan'), med_pc=float('nan'),
                       med_str=float('nan'), ratio=float('nan'))
            out.append(rec)
            continue
        dpc = np.abs(F[pc_idx[0], j] - F[pc_idx[1], j])
        dst = np.abs(F[str_idx[0], j] - F[str_idx[1], j]) if len(str_idx[0]) else np.zeros(0)
        rec['unch_exact'] = float((dpc <= 1e-12).mean())
        if s <= 0:
            rec.update(unch_1pct=1.0, med_pc=0.0, med_str=0.0, mean_pc=0.0, mean_str=0.0, ratio=float('nan'),
                       verdict='CONSTANT')
            out.append(rec)
            continue
        rec['unch_1pct'] = float((dpc <= 0.01 * s).mean())
        rec['med_pc'] = float(np.median(dpc) / s)
        rec['med_str'] = float(np.median(dst) / s) if len(dst) else float('nan')
        rec['mean_pc'] = float(dpc.mean() / s)
        rec['mean_str'] = float(dst.mean() / s) if len(dst) else float('nan')
        if len(dst) == 0:
            ratio, verdict = float('nan'), 'NO STRANGERS'
        elif rec['med_str'] > 0:
            ratio = rec['med_pc'] / rec['med_str']
            verdict = 'FAIL' if ratio >= fail_ratio else 'pass'
        elif rec['mean_str'] > 0:
            ratio = rec['mean_pc'] / rec['mean_str']
            verdict = ('FAIL' if ratio >= fail_ratio else 'pass')
            rec['ratio_basis'] = 'mean'
        else:
            ratio, verdict = float('nan'), 'CONSTANT'
        rec['ratio'] = ratio
        rec['verdict'] = verdict
        out.append(rec)
    return out


def fmt_pct(v):
    return 'n/a' if v is None or (isinstance(v, float) and math.isnan(v)) else '%.1f%%' % (100.0 * v)


def fmt_num(v, nd=3):
    return 'n/a' if v is None or (isinstance(v, float) and math.isnan(v)) else '%.*f' % (nd, v)


def pc_check_markdown(stats, groups, header_lines):
    lines = ['# Parent-and-child check', ''] + header_lines + ['',
             '| feature | group | sd | unchanged exactly | unchanged within 1% of sd | median change / sd, parent-child | '
             'median change / sd, strangers | ratio | verdict |',
             '|---|---|---|---|---|---|---|---|---|']
    for r in stats:
        basis = ' (means)' if r.get('ratio_basis') == 'mean' else ''
        lines.append('| %s | %s | %s | %s | %s | %s | %s | %s%s | %s |' % (
            r['feature'], groups[r['feature']], fmt_num(r['sd'], 4), fmt_pct(r['unch_exact']), fmt_pct(r['unch_1pct']),
            fmt_num(r['med_pc']), fmt_num(r['med_str']), fmt_num(r['ratio']), basis, r['verdict']))
    return '\n'.join(lines) + '\n'


# ------------------------------------------------------------------ the skin vector (section 5(a), (b))

SKIN_PARAMS = [
    # (name, [(feature, weight, reason)])
    ('base_hue', [
        ('share_consumer', 0.9, 'a lineage with more mouth tissue is a hunter, and hunters drift warm'),
        ('share_absorptive', 0.7, 'absorbing tissue marks grazers and scavengers, which sit warmer than leaf-bearers'),
        ('share_photosynthetic', -0.6, 'leaf tissue keeps a body on the cool, green side')]),
    ('base_saturation', [
        ('log_parts', 0.7, 'more parts, richer colour: a complex body reads as more vivid than a single block'),
        ('share_buoyancy', -0.6, 'gas-float tissue reads pale, as a drifting float does'),
        ('mirrored_share', 0.4, 'bilateral copies read as a crisp, designed body')]),
    ('pattern_regime', [
        ('investment', 1.6, 'the regime follows reproductive investment, as it does today (spots to stripes)')]),
    ('pattern_scale', [
        ('log_margin', 1.2, 'the reserve a parent keeps sets the tile length, as it does today'),
        ('log_parts', 0.5, 'bigger, more segmented bodies carry coarser markings')]),
    ('rim_hue', [
        ('brain_w_mean', 0.9, 'family marker: the net sign of the wiring moves the edge colour between lineages '
                              'of one body plan'),
        ('brain_log_neurons', 0.5, 'family marker: a larger brain tips the edge colour further')]),
    ('rim_width', [
        ('bbox_aspect', 0.8, 'a long, narrow body wears a long, thin band along its length'),
        ('mirrored_share', 0.5, 'a symmetric body carries a clean paired band')]),
    ('mottle_strength', [
        ('brain_w_sd', 0.9, 'family marker: widely spread wiring shows as a more broken surface'),
        ('brain_log_links', 0.5, 'family marker: a more connected brain is a more mottled one')]),
    ('iridescence', [
        ('part_aspect_median', 0.7, 'flat and slender parts catch the light as sheen'),
        ('shape_capsule', 0.5, 'smooth round limbs shine where blocks do not')]),
    ('countershade', [
        ('share_link', 1.0, 'link (muscle) tissue is the swimming share: swimmers are dark above and pale below'),
        ('jointed_share', 0.7, 'jointed parts are the ones that move the body'),
        ('share_anchoring', -0.8, 'anchored bodies sit still and carry no countershade')]),
    ('carve_character', [
        ('log_chain', 0.9, 'a long chain of segments takes a deeper, more tapered carve'),
        ('shape_box', 0.6, 'blocky parts take the hardest carve')]),
]


def logistic(x):
    return 1.0 / (1.0 + math.exp(-x)) if x > -700 else 0.0


def resolve_skin_spec(spec_params, passing, constant, failing):
    """Mark each input used or dropped given the passing feature set. Returns list of dicts."""
    out = []
    for name, inputs in spec_params:
        rec = {'name': name, 'bias': 0.0, 'inputs': []}
        for feat, w, why in inputs:
            used = feat in passing
            if used:
                drop = ''
            elif feat in constant:
                drop = 'constant in the reference population'
            elif feat in failing:
                drop = 'failed the parent-and-child check'
            else:
                drop = 'not a feature of this run'
            rec['inputs'].append({'feature': feat, 'weight': w, 'reason': why, 'used': used, 'dropped': drop})
        out.append(rec)
    return out


def skin_vectors(Z, names_index, resolved, zclip):
    """Z: rows x features, standardised. Returns rows x 10 in [0,1]."""
    n = Z.shape[0]
    S = np.zeros((n, len(resolved)))
    Zc = np.clip(Z, -zclip, zclip)
    for k, rec in enumerate(resolved):
        s = np.full(n, rec['bias'])
        used = [i for i in rec['inputs'] if i['used']]
        for i in used:
            s = s + i['weight'] * Zc[:, names_index[i['feature']]]
        S[:, k] = 1.0 / (1.0 + np.exp(-s)) if used else 0.5
    return S


def weights_table_markdown(resolved, zclip):
    lines = ['# Skin vector weights (draft)', '',
             'Each parameter is `logistic(bias + sum of weight x z)`, in [0, 1], where `z` is the feature standardised by '
             'the reference population (mean and sd in projection.json) and clipped to +-%g. A parameter with no usable '
             'input stays at 0.5. Guild colour is not in the vector: the vector moves a body around its guild colour. '
             'The weights shown are folded: the input weight times the parameter\'s gain, with the bias that puts the '
             'reference population\'s median body at 0.5 (see summary.md). Weights are a draft for the coordinator and '
             'owner to change; `--weights-file` replaces them (it takes the weight before the fold).' % zclip, '',
             '| # | parameter | input | weight, folded | weight before fold | used | why a viewer can follow it |',
             '|---|---|---|---|---|---|---|']
    for k, rec in enumerate(resolved, 1):
        for n, i in enumerate(rec['inputs']):
            lines.append('| %s | %s | %s | %+.3f | %+.2f | %s | %s |' % (
                k if n == 0 else '', rec['name'] if n == 0 else '', i['feature'], i['weight'],
                i.get('weight_before_fold', i['weight']),
                'yes' if i['used'] else 'NO: ' + i['dropped'], i['reason']))
    lines += ['', '| parameter | gain | bias |', '|---|---|---|']
    for rec in resolved:
        lines.append('| %s | %s | %+.3f |' % (rec['name'], '%.3f' % rec['fold']['gain'] if rec.get('fold', {}).get('gain')
                                              else 'n/a', rec['bias']))
    return '\n'.join(lines) + '\n'


# ------------------------------------------------------------------ the pooled reference and the re-centring

PRODUCER_SHARE = 0.5  # a body whose photosynthetic share of volume is at least this is a producer
LOGIT_90 = math.log(0.9 / 0.1)
MIN_SPREAD = 1e-6     # a weighted sum whose p90 - p10 is below this has no spread to fold a gain from


def parse_ref(text):
    """'RUN@SECONDS' -> (run directory, seconds)."""
    if '@' not in text:
        raise ToolError('--ref wants RUN@SECONDS, got %r' % text)
    path, sec = text.rsplit('@', 1)
    try:
        return path, float(sec)
    except ValueError:
        raise ToolError('--ref %r: %r is not a number of seconds' % (text, sec))


def is_producer_row(row, names):
    """True when the photosynthetic share of volume (the feature row's share_photosynthetic) is at least PRODUCER_SHARE."""
    if 'share_photosynthetic' not in names:
        return False
    return row[names.index('share_photosynthetic')] >= PRODUCER_SHARE


def read_reference(refs, cell_ids, names, reuse):
    """Pool the non-producers alive at each reference run's snapshot nearest its second.

    cell_ids is the fixed set of cell-type columns, taken once from the primary run's registry; every run is read under
    that set, so no column moves. A cell type a run's registry has and the set lacks is in no column (its parts count in the
    body's volume, so the shares of such a body sum to under one); a type the set has and a run lacks is a share of zero.
    Each is counted and reported. reuse = (real path of the primary run, its snapshot second, {id: feature row}) lets the
    primary run's own rows stand in for a second read of it. Returns (rows, per-run report list); a run that cannot be
    read is reported with an error and contributes nothing.
    """
    rows = []
    report = []
    for spec in refs:
        info = {'run': spec, 'asked_seconds': None, 'snapshot_seconds': None, 'alive': 0, 'reference_bodies': 0,
                'producers_left_out': 0, 'develop_to_nothing': 0, 'genomes_missing': 0, 'registry': [],
                'columns_with_no_cell_type_in_registry': [], 'registry_types_with_no_column': [],
                'bodies_with_parts_outside_the_columns': 0, 'source': None, 'error': None}
        report.append(info)
        try:
            run, at = parse_ref(spec)
            info['run'] = run.replace('\\', '/')
            info['asked_seconds'] = at
            if not os.path.exists(os.path.join(run, 'genomes.jsonl.gz')):
                raise ToolError('no genomes.jsonl.gz in %s' % run)
            cfg = load_config(run)
            lim = Limits(cfg)
            registry = cell_ids_of(cfg)
            info['registry'] = registry
            info['columns_with_no_cell_type_in_registry'] = [c for c in cell_ids if c not in registry]
            info['registry_types_with_no_column'] = [c for c in registry if c not in cell_ids]
            t, alive = alive_at(run, at)
            info['snapshot_seconds'] = t
            info['alive'] = len(alive)
            got = []
            if reuse is not None and os.path.realpath(run) == reuse[0] and t == reuse[1]:
                for i in alive:
                    row = reuse[2].get(i)
                    if row is None:
                        info['genomes_missing'] += 1
                    elif is_producer_row(row, names):
                        info['producers_left_out'] += 1
                    else:
                        got.append(row)
                info['source'] = "the primary run's own rows"
            else:
                wanted = set(alive)
                top = max(wanted) if wanted else -1
                found = 0
                for line in read_lines(os.path.join(run, 'genomes.jsonl.gz'), open_gz):
                    gid = head_id(line)
                    if gid is None:
                        continue
                    if gid in wanted:
                        g = parse(line)
                        if g is None or g.get('id') != gid:
                            continue
                        found += 1
                        parts = develop(g, lim)
                        if not parts:
                            info['develop_to_nothing'] += 1
                        else:
                            if any(p.cell not in cell_ids for p in parts):
                                info['bodies_with_parts_outside_the_columns'] += 1
                            row = features_of(g, lim, cell_ids, parts=parts)
                            if is_producer_row(row, names):
                                info['producers_left_out'] += 1
                            else:
                                got.append(row)
                        if found == len(wanted):
                            break
                    elif gid > top:
                        break
                info['genomes_missing'] = len(wanted) - found
                info['source'] = 'genomes.jsonl.gz'
            info['reference_bodies'] = len(got)
            rows.extend(got)
        except Exception as e:  # reported, never hidden: the run contributes nothing
            info['error'] = '%s: %s' % (type(e).__name__, e)
            info['reference_bodies'] = 0
            log('REFERENCE RUN FAILED %s -> %s' % (spec, info['error']))
            continue
        log('reference %s @ %s: snapshot %s s, %d alive, %d producers left out, %d reference bodies' % (
            info['run'], info['asked_seconds'], info['snapshot_seconds'], info['alive'], info['producers_left_out'],
            info['reference_bodies']))
    return rows, report


def standardise(F, mean, sd, names, zclip):
    """z = (x - mean) / sd (sd 0 -> z 0), each brain_w_* z shrunk by n/(n + BRAIN_SHRINK_LINKS), clipped to +-zclip."""
    safe = np.where(sd > 0, sd, 1.0)
    Z = (F - mean) / safe
    Z[:, sd <= 0] = 0.0
    n_links = np.expm1(F[:, names.index('brain_log_links')])
    shrink = n_links / (n_links + BRAIN_SHRINK_LINKS)
    for j, name in enumerate(names):
        if name.startswith('brain_w_'):
            Z[:, j] *= shrink
    return np.clip(Z, -zclip, zclip)


def fold_gain(s):
    """The gain and the statistics that map a weighted sum's median to 0.5 and its p10 and p90 to about 0.1 and 0.9:
    g = logit(0.9) / ((p90 - p10) / 2), bias = -g * median. The gain is None when the spread is degenerate."""
    s = np.asarray(s, dtype=float)
    med = float(np.median(s))
    p10, p90 = (float(v) for v in np.percentile(s, [10, 90]))
    st = {'median': med, 'p10': p10, 'p90': p90}
    if not (p90 - p10) >= MIN_SPREAD:
        return None, st
    return LOGIT_90 / ((p90 - p10) / 2.0), st


def fold_parameters(resolved, Zref, names_index):
    """Fold a gain and a bias into each parameter's weights, in place, over the reference rows (Zref is standardised)."""
    for rec in resolved:
        used = [i for i in rec['inputs'] if i['used']]
        if not used:
            rec['fold'] = {'applied': False, 'reason': 'no input is used; the parameter stays at 0.5'}
            continue
        sums = np.zeros(Zref.shape[0])
        for i in used:
            sums = sums + i['weight'] * Zref[:, names_index[i['feature']]]
        g, st = fold_gain(sums)
        if g is None:
            rec['fold'] = dict(st, applied=False, gain=1.0,
                               reason='degenerate spread (p90 - p10 under %g): weights and bias kept as they were' % MIN_SPREAD)
            continue
        for i in used:
            i['weight_before_fold'] = i['weight']
            i['weight'] = i['weight'] * g
        rec['bias_before_fold'] = rec['bias']
        rec['bias'] = -g * st['median']
        rec['fold'] = dict(st, applied=True, gain=g)
    return resolved


def pooled_quantiles(S, names):
    """[(name, p10, p50, p90)] per column of S."""
    out = []
    for k, n in enumerate(names):
        a, b, c = np.percentile(S[:, k], [10, 50, 90])
        out.append((n, float(a), float(b), float(c)))
    return out


# ------------------------------------------------------------------ the one number

MAIN_CLASSES = ('parent_child', 'cousin', 'stranger')
ALL_CLASSES = ('parent_child', 'sibling', 'cousin', 'stranger')


def evaluate(recs, n_boot, seed):
    """Rho, its interval, the corner share and the cliff table over recs = [(class, a, b, species, skin)]."""
    sel = [r for r in recs if r[0] in MAIN_CLASSES]
    sp = np.array([r[3] for r in sel])
    sk = np.array([r[4] for r in sel])
    ev = {'n_sel': len(sel), 'counts': {c: sum(1 for r in recs if r[0] == c) for c in ALL_CLASSES}}
    ev['rho'] = spearman(sp, sk)
    ev['lo'], ev['hi'] = bootstrap_rho(sp, sk, n_boot, seed)
    within = [r for r in sel if r[0] != 'stranger']
    ev['rho_within'] = spearman([r[3] for r in within], [r[4] for r in within]) if within else float('nan')
    corner = {}
    if len(sel):
        p25, p75 = percentile(sp, 25), percentile(sk, 75)
        corner = {'p25_species': p25, 'p75_skin': p75,
                  'strict': float(np.mean((sp < p25) & (sk > p75))), 'inclusive': float(np.mean((sp <= p25) & (sk > p75))),
                  'n_strict': int(np.sum((sp < p25) & (sk > p75))), 'n_inclusive': int(np.sum((sp <= p25) & (sk > p75)))}
    ev['corner'] = corner
    cliff = {}
    for label in ALL_CLASSES:
        v = np.array([r[4] for r in recs if r[0] == label])
        s_ = np.array([r[3] for r in recs if r[0] == label])
        if len(v):
            cliff[label] = (len(v), float(np.median(v)), float(np.percentile(v, 95)), float(v.max()), float(np.median(s_)))
    ev['cliff'] = cliff
    ev['rho_ok'] = (not math.isnan(ev['rho'])) and ev['rho'] > 0.5
    # Judged on the inclusive count: with many unchanged children the 25th percentile is 0 and the strict count is empty.
    ev['corner_ok'] = max(corner.get('strict', 1.0), corner.get('inclusive', 1.0)) < 0.05
    return ev


def bars_markdown(ev, heading, n_boot, note=None):
    """The summary's section for one evaluation."""
    c = ev['counts']
    lines = ['', heading]
    if note:
        lines.append(note)
    lines += ['- Pairs: %d parent-child, %d cousin, %d stranger (%d in all). Siblings (%d) are in the cliff table but not in '
              'the two bars.' % (c['parent_child'], c['cousin'], c['stranger'], ev['n_sel'], c['sibling'])]
    if not ev['n_sel']:
        return lines + ['- No pairs.']
    lines += ['- Spearman rho, skin distance against species distance: **%s** (95%% bootstrap interval %s to %s, %d '
              'resamples). Bar: above 0.5. **%s**' % (fmt_num(ev['rho']), fmt_num(ev['lo']), fmt_num(ev['hi']), n_boot,
                                                     'PASS' if ev['rho_ok'] else 'FAIL'),
              '- rho over the within-clade pairs only (parent-child and cousin): %s.' % fmt_num(ev['rho_within'])]
    corner = ev['corner']
    lines += ['- Corner share (species distance below the 25th percentile %s, skin distance above the 75th percentile %s, '
              'both over these %d pairs): **%s** (%d pairs). Bar: under 5%%. **%s**' % (
                  fmt_num(corner['p25_species'], 4), fmt_num(corner['p75_skin'], 4), ev['n_sel'],
                  fmt_pct(corner['strict']), corner['n_strict'], 'PASS' if ev['corner_ok'] else 'FAIL'),
              '- The same with species distance at or below the 25th percentile: %s (%d pairs). Parent-child pairs often '
              'sit at species distance 0, so the 25th percentile can be 0 and the strict test then has no pairs to '
              'count; the inclusive count is the stricter reading.' % (fmt_pct(corner['inclusive']), corner['n_inclusive']),
              '', '| class | pairs | median skin distance | 95th percentile | max | median species distance |',
              '|---|---|---|---|---|---|']
    for label in ALL_CLASSES:
        if label in ev['cliff']:
            n, med, p95, mx, msp = ev['cliff'][label]
            lines.append('| %s | %d | %.4f | %.4f | %.4f | %.4f |' % (label, n, med, p95, mx, msp))
    cl = ev['cliff']
    if 'parent_child' in cl and 'stranger' in cl:
        lines += ['', 'Largest parent-child skin distance %.4f against the median stranger distance %.4f: the largest '
                  'parent-child jump is %.0f%% of a typical stranger gap.' % (
                      cl['parent_child'][3], cl['stranger'][1],
                      100.0 * cl['parent_child'][3] / cl['stranger'][1] if cl['stranger'][1] > 0 else float('nan'))]
    return lines


# ------------------------------------------------------------------ main


def log(msg):
    print(msg, file=sys.stderr, flush=True)


def percentile(a, q):
    return float(np.percentile(np.asarray(a, dtype=float), q))


def run_tool(args):
    run = args.run_dir
    if not os.path.exists(os.path.join(run, 'genomes.jsonl.gz')):
        fail('%s has no genomes.jsonl.gz (a record-format-2 run is expected)' % run)
    os.makedirs(args.out, exist_ok=True)
    cfg = load_config(run)
    weights = tuple(float(v) for v in args.species_weights.split(',')) if args.species_weights else species_weights_of(cfg)
    lim = Limits(cfg)
    cell_ids = cell_ids_of(cfg)
    table = feature_table(cell_ids)
    names = [t[0] for t in table]
    groups = {t[0]: t[1] for t in table}
    rng = random.Random(args.seed)

    t, alive = alive_at(run, args.at)
    log('snapshot %d s: %d bodies alive' % (t, len(alive)))
    dirs = [run] + list(args.prior or [])
    pm = load_parent_map(list(reversed(dirs)))
    log('lineage: %d births' % len(pm))
    pop_set = set(alive)
    for i in alive:
        cur = i
        for _ in range(args.depth):
            p = pm.parent(cur)
            if p is None or p < 0:
                break
            pop_set.add(p)
            cur = p
    log('population: %d genomes wanted (%d alive plus ancestors back %d)' % (len(pop_set), len(alive), args.depth))

    feats = {}
    compact = {}
    undevelopable = 0
    found = set()
    for d in dirs:
        missing = pop_set - found
        if not missing:
            break
        top = max(missing)
        for line in read_lines(os.path.join(d, 'genomes.jsonl.gz'), open_gz):
            gid = head_id(line)
            if gid is None:
                continue
            if gid in missing:
                g = parse(line)
                if g is None or g.get('id') != gid:
                    continue
                found.add(gid)
                row = features_of(g, lim, cell_ids)
                if row is None:
                    undevelopable += 1
                else:
                    feats[gid] = row
                    compact[gid] = compact_for_distance(g)
                if len(found) == len(pop_set):
                    break
            elif gid > top:
                break
    found = len(found)
    log('genomes read: %d found, %d usable, %d develop to nothing' % (found, len(feats), undevelopable))
    if len(feats) < 10:
        fail('only %d usable genomes; nothing to check' % len(feats))

    ids = sorted(feats)
    row_of = {i: k for k, i in enumerate(ids)}
    F = np.array([feats[i] for i in ids], dtype=float)
    alive_set = set(alive)
    alive_rows = np.array([row_of[i] for i in ids if i in alive_set], dtype=int)
    kin = Kin(pm, ids, args.depth)

    pair_pool = [i for i in ids if i in alive_set] if args.alive_only_pairs else ids
    # parent-child pairs: the child and its parent both in the usable population
    pc_pairs = []
    for c in ids:
        p = kin.A(c, 1)
        if p >= 0 and p in row_of:
            pc_pairs.append((c, p))
    if args.alive_only_pairs:
        pc_pairs = [(c, p) for c, p in pc_pairs if c in alive_set and p in alive_set]
    sib_pairs = sample_same_generation(kin, pair_pool, [1], args.max_siblings, rng)
    cousin_pairs = sample_same_generation(kin, pair_pool, [2, 3], args.max_cousins, rng)
    stranger_pairs, stranger_def = sample_strangers(kin, pair_pool, args.strangers, rng)
    check_pairs, _ = sample_strangers(kin, pair_pool, args.check_pairs, rng)
    log('pairs: %d parent-child, %d siblings, %d cousins, %d strangers' % (
        len(pc_pairs), len(sib_pairs), len(cousin_pairs), len(stranger_pairs)))

    # ---- parent-and-child check
    pc_idx = (np.array([row_of[c] for c, _ in pc_pairs], dtype=int), np.array([row_of[p] for _, p in pc_pairs], dtype=int))
    st_idx = (np.array([row_of[a] for a, _ in check_pairs], dtype=int),
              np.array([row_of[b] for _, b in check_pairs], dtype=int))
    stats = pc_check(F, names, alive_rows, pc_idx, st_idx, args.fail_ratio)
    passing = [r['feature'] for r in stats if r['verdict'] == 'pass']
    constant = {r['feature'] for r in stats if r['verdict'] == 'CONSTANT'}
    failing = [r['feature'] for r in stats if r['verdict'] not in ('pass', 'CONSTANT')]
    header = ['Run `%s`, snapshot %d s (asked for %s). Population: %d usable genomes, %d of them alive at the snapshot; '
              '%d parent-child pairs; %d unrelated pairs.' % (
                  os.path.basename(run.rstrip('/\\')), t, args.at, len(ids), len(alive_rows), len(pc_pairs),
                  len(check_pairs)),
              'Strangers: ' + stranger_def + '.',
              'Spread = population standard deviation over the bodies alive at the snapshot. A feature FAILS when the '
              'median parent-to-child change over spread is at least %.2f of the stranger median (the ratio of means '
              'when the stranger median is 0). CONSTANT: no spread in the reference population, so it carries '
              'nothing and is left out.' % args.fail_ratio]
    with open(os.path.join(args.out, 'parent_child_check.md'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(pc_check_markdown(stats, groups, header))

    # ---- features.csv
    with open(os.path.join(args.out, 'features.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['id', 'alive', 'parent', 'clade'] + names)
        for i in ids:
            w.writerow([i, int(i in alive_set), kin.A(i, 1), kin.root(i)] + ['%.9g' % v for v in feats[i]])

    # ---- the reference population: the non-producers of every --ref run, pooled; else the bodies alive at the snapshot
    prod = {i: is_producer_row(feats[i], names) for i in ids}
    ref_report = None
    if args.ref:
        reuse = (os.path.realpath(run), t, {i: feats[i] for i in alive if i in feats})
        ref_rows, ref_report = read_reference(args.ref, cell_ids, names, reuse)
        n_failed = sum(1 for r in ref_report if r['error'])
        if len(ref_rows) < 10:
            fail('only %d reference bodies were read from %d --ref runs (%d failed; see the log above); nothing to fit' % (
                len(ref_rows), len(args.ref), n_failed))
        ref = np.array(ref_rows, dtype=float)
        reference_population = ('non-producers (photosynthetic share of volume under %g) alive at each reference run\'s '
                                'snapshot, pooled across %d runs' % (PRODUCER_SHARE, len(args.ref) - n_failed))
    else:
        ref = F[alive_rows]
        reference_population = 'bodies alive at the snapshot'

    # ---- projection (passing features)
    mean_all = ref.mean(axis=0)
    sd_all = ref.std(axis=0)
    pidx = [names.index(n) for n in passing if sd_all[names.index(n)] > 0]
    Zref = (ref[:, pidx] - mean_all[pidx]) / sd_all[pidx] if pidx else np.zeros((len(ref), 0))
    if pidx:
        _, S, Vt = np.linalg.svd(Zref, full_matrices=False)
        for k in range(Vt.shape[0]):  # sign convention: the largest loading of each component is positive
            if Vt[k, int(np.argmax(np.abs(Vt[k])))] < 0:
                Vt[k] = -Vt[k]
        eig = (S ** 2) / max(1, len(ref) - 1)
        explained = eig / eig.sum() if eig.sum() > 0 else eig
    else:
        Vt, eig, explained = np.zeros((0, 0)), np.zeros(0), np.zeros(0)
    projection = {
        'run': os.path.basename(os.path.dirname(run.rstrip('/\\'))) + '/' + os.path.basename(run.rstrip('/\\')),
        'snapshot_seconds': t, 'asked_seconds': args.at, 'reference_population': reference_population,
        'pooled': bool(args.ref),
        'producer_rule': ('a body is a producer when its photosynthetic share of volume is at least %g; the reference '
                          'and the fit hold non-producers only' % PRODUCER_SHARE) if args.ref else
                         'none: every body alive at the snapshot is in the reference',
        'n_reference': int(len(ref)),
        'reference_runs': ref_report,
        'cell_type_columns': cell_ids,
        'cell_type_handling': ('The share_* columns are one fixed set, the primary run\'s registry; each reference run is '
                               'read under it. A registry type with no column counts in the body\'s volume and in no '
                               'column; a column whose type a registry lacks is a share of zero. Both are counted in '
                               'reference_runs.'),
        'features_all': names,
        'mean_all': [float(v) for v in mean_all], 'sd_all': [float(v) for v in sd_all],
        'features_passing': passing, 'features_failing': failing, 'features_constant': sorted(constant),
        'features_in_rotation': [names[j] for j in pidx],
        'rotation_rows_are_components': [[float(v) for v in row] for row in Vt],
        'eigenvalues': [float(v) for v in eig], 'explained_variance_ratio': [float(v) for v in explained],
        'how_used': 'z = (x - mean_all) / sd_all (sd 0 -> z 0); pc = rotation @ z over features_in_rotation',
        'brain_shrink_links': BRAIN_SHRINK_LINKS,
        'brain_shrink': 'for the skin vector each brain_w_* z is multiplied by n/(n + brain_shrink_links), n = expm1(brain_log_links)',
    }
    with open(os.path.join(args.out, 'projection.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(projection, f, indent=1)

    # ---- the skin vector
    spec = SKIN_PARAMS
    if args.weights_file:
        with open(args.weights_file, 'r', encoding='utf-8') as f:
            wf = json.load(f)
        spec = [(p['name'], [(i['feature'], i.get('weight_before_fold', i['weight']), i.get('reason', ''))
                             for i in p['inputs']]) for p in wf['parameters']]
    resolved = resolve_skin_spec(spec, set(passing), constant, set(failing))
    # A summary of one to three weights is noise: one added link or flipped sign swung rim hue and mottle by half their
    # range at a species distance near 0 (c8, 2026-10-29). Each brain-weight z shrinks toward the population by n/(n+4),
    # n the brain's connection count, so a small brain sits near the middle and a large one keeps its marker.
    Zall = standardise(F, mean_all, sd_all, names, args.zclip)
    Zref_all = standardise(ref, mean_all, sd_all, names, args.zclip)
    name_index = {n: k for k, n in enumerate(names)}
    if not args.no_fold:
        fold_parameters(resolved, Zref_all, name_index)
    pnames = [r['name'] for r in resolved]
    S = skin_vectors(Zall, name_index, resolved, args.zclip)
    S_ref = skin_vectors(Zref_all, name_index, resolved, args.zclip)
    with open(os.path.join(args.out, 'weights.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'run': projection['run'], 'snapshot_seconds': t, 'zclip': args.zclip,
                   'reference_population': reference_population, 'n_reference': int(len(ref)),
                   'form': 'parameter = logistic(bias + sum(weight * clip(z, -zclip, zclip)))',
                   'folded': not args.no_fold,
                   'fold': ('each parameter\'s weights were multiplied by a gain g = logit(0.9) / ((p90 - p10) / 2) and its '
                            'bias set to -g * median, p10, median and p90 being those of the raw weighted sum over the '
                            'reference bodies, so the median reference body sits at 0.5 and p10 and p90 near 0.1 and 0.9; '
                            'weight_before_fold keeps the input weight') if not args.no_fold else 'not applied',
                   'parameters': resolved}, f, indent=1)
    wt = weights_table_markdown(resolved, args.zclip)
    with open(os.path.join(args.out, 'weights_table.md'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(wt)
    with open(os.path.join(args.out, 'skin_vectors.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['id', 'alive'] + pnames)
        for k, i in enumerate(ids):
            w.writerow([i, int(i in alive_set)] + ['%.6f' % v for v in S[k]])

    # ---- the one number
    classes = [('parent_child', pc_pairs), ('sibling', sib_pairs), ('cousin', cousin_pairs),
               ('stranger', stranger_pairs)]

    def score(class_lists):
        out = []
        for label, plist in class_lists:
            for a, b in plist:
                sk_ = float(np.linalg.norm(S[row_of[a]] - S[row_of[b]]))
                sp_ = species_distance(compact[a], compact[b], weights)
                out.append((label, a, b, sp_, sk_))
        return out

    recs = score(classes)
    # A second sample drawn among the non-producers alone: the filter of the first sample leaves few pairs, because the
    # strangers and cousins were drawn from every body and most bodies are producers. Drawn after every other sample.
    np_pool = [i for i in pair_pool if not prod[i]]
    np_classes = [('parent_child', [(c, p) for c, p in pc_pairs if not prod[c] and not prod[p]]),
                  ('sibling', sample_same_generation(kin, np_pool, [1], args.max_siblings, rng)),
                  ('cousin', sample_same_generation(kin, np_pool, [2, 3], args.max_cousins, rng)),
                  ('stranger', sample_strangers(kin, np_pool, args.strangers, rng)[0])]
    recs_np_sample = score(np_classes)
    with open(os.path.join(args.out, 'pairs.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['class', 'a', 'b', 'species_distance', 'skin_distance', 'a_producer', 'b_producer'])
        for r in recs:
            w.writerow([r[0], r[1], r[2], '%.9g' % r[3], '%.9g' % r[4], int(prod[r[1]]), int(prod[r[2]])])
    with open(os.path.join(args.out, 'pairs_nonproducer_sample.csv'), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['class', 'a', 'b', 'species_distance', 'skin_distance'])
        for r in recs_np_sample:
            w.writerow([r[0], r[1], r[2], '%.9g' % r[3], '%.9g' % r[4]])
    recs_np = [r for r in recs if not prod[r[1]] and not prod[r[2]]]
    ev = evaluate(recs, args.bootstrap, args.seed)
    ev_np = evaluate(recs_np, args.bootstrap, args.seed)
    ev_npS = evaluate(recs_np_sample, args.bootstrap, args.seed)
    rho, corner, cliff, sel = ev['rho'], ev['corner'], ev['cliff'], [r for r in recs if r[0] in MAIN_CLASSES]
    sp = np.array([r[3] for r in sel])
    sk = np.array([r[4] for r in sel])
    scatter_written = False
    if not args.no_plot:
        try:
            import matplotlib
            matplotlib.use('Agg')
            import matplotlib.pyplot as plt
            fig, ax = plt.subplots(figsize=(7, 5.5))
            colour = {'parent_child': '#1b9e77', 'sibling': '#7570b3', 'cousin': '#d95f02', 'stranger': '#666666'}
            for label, _ in classes:
                pts = [(r[3], r[4]) for r in recs if r[0] == label]
                if pts:
                    ax.scatter([p[0] for p in pts], [p[1] for p in pts], s=6, alpha=0.35, c=colour[label], label=label)
            if len(sel):
                ax.axvline(corner['p25_species'], color='k', lw=0.6, ls='--')
                ax.axhline(corner['p75_skin'], color='k', lw=0.6, ls='--')
            ax.set_xlabel('species distance (SpeciesDistance.Between)')
            ax.set_ylabel('skin-vector distance (10 parameters)')
            ax.set_title('rho = %.3f, corner share %s' % (rho, fmt_pct(corner.get('strict'))))
            ax.legend(markerscale=3)
            fig.tight_layout()
            fig.savefig(os.path.join(args.out, 'pairs_scatter.png'), dpi=130)
            plt.close(fig)
            scatter_written = True
        except Exception as e:  # matplotlib missing or broken: pairs.csv has the points
            log('no scatter plot: %s' % e)

    n_prod_alive = sum(1 for i in alive if i in prod and prod[i])
    lines = ['# Summary', '',
             'Run `%s`, snapshot %d s (asked for %s). Species-distance weights (cell, topology, parameter, brain): %s.' % (
                 projection['run'], t, args.at, ', '.join('%g' % v for v in weights)), '',
             '## Reference population',
             '- %s; %d bodies in all.' % (reference_population, len(ref))]
    if ref_report:
        lines += ['- Fixed cell-type columns (the primary run\'s registry): %s.' % ', '.join(cell_ids),
                  '', '| run | asked s | snapshot s | alive | producers left out | reference bodies | notes |',
                  '|---|---|---|---|---|---|---|']
        for r in ref_report:
            notes = []
            if r['error']:
                notes.append('FAILED: %s' % r['error'])
            if r['columns_with_no_cell_type_in_registry']:
                notes.append('columns with no type in its registry: %s' % ', '.join(r['columns_with_no_cell_type_in_registry']))
            if r['registry_types_with_no_column']:
                notes.append('registry types with no column: %s' % ', '.join(r['registry_types_with_no_column']))
            if r['bodies_with_parts_outside_the_columns']:
                notes.append('%d bodies with parts outside the columns' % r['bodies_with_parts_outside_the_columns'])
            if r['develop_to_nothing']:
                notes.append('%d develop to nothing' % r['develop_to_nothing'])
            if r['genomes_missing']:
                notes.append('%d alive ids without a genome row' % r['genomes_missing'])
            lines.append('| %s | %s | %s | %d | %d | %d | %s |' % (
                r['run'], '%g' % r['asked_seconds'] if r['asked_seconds'] is not None else '', r['snapshot_seconds'] or '',
                r['alive'], r['producers_left_out'], r['reference_bodies'], '; '.join(notes) or 'none'))
        lines.append('')
    lines += ['## Population (the primary run, for the pairs)',
              '- %d usable genomes: %d alive at the snapshot (%d of them producers), %d ancestors back %d generations (%d '
              'genomes read, %d develop to nothing and are left out).' % (
                  len(ids), len(alive_rows), n_prod_alive, len(ids) - len(alive_rows), args.depth, found, undevelopable),
              '- Clades: %s.' % stranger_def,
              '- Features: %d computed, %d pass the parent-and-child check, %d fail, %d are constant.' % (
                  len(names), len(passing), len(failing), len(constant)),
              '- Failing: %s.' % (', '.join(failing) if failing else 'none'),
              '- Constant: %s.' % (', '.join(sorted(constant)) if constant else 'none')]
    lines += bars_markdown(ev, '## The two bars, all pairs (parent-child, cousin, stranger)', args.bootstrap,
                           note='- Cousins: same-generation pairs whose nearest common ancestor is 2 or 3 steps up from both '
                                '(so they share a great-grandparent), not siblings, not parent-child.')
    lines += bars_markdown(ev_np, '## The two bars, pairs where both bodies are non-producers (the same sample, filtered)',
                           args.bootstrap, note='- Percentiles are those of these pairs alone.')
    lines += bars_markdown(ev_npS, '## The two bars, a sample drawn among the non-producers alone', args.bootstrap,
                           note='- Parent-child pairs are the first sample\'s with both bodies non-producers; siblings, '
                                'cousins and strangers are drawn afresh from the living-or-ancestor non-producers, so '
                                'strangers are non-producers of different founder clades.')
    used_inputs = sorted({i['feature'] for p in resolved for i in p['inputs'] if i['used']})
    dropped = [(p['name'], i['feature'], i['dropped']) for p in resolved for i in p['inputs'] if not i['used']]
    q_ref = pooled_quantiles(S_ref, pnames)
    alive_np = [row_of[i] for i in alive if i in row_of and not prod[i]]
    alive_pr = [row_of[i] for i in alive if i in row_of and prod[i]]
    q_np = pooled_quantiles(S[alive_np], pnames) if alive_np else None
    q_pr = pooled_quantiles(S[alive_pr], pnames) if alive_pr else None
    lines += ['', '## Skin vector',
              '- Features feeding the vector: %s.' % (', '.join(used_inputs) if used_inputs else 'none'),
              '- Inputs dropped: %s.' % ('; '.join('%s <- %s (%s)' % d for d in dropped) if dropped else 'none'),
              '- Principal components of the %d features in the rotation: first three explain %s of the variance.' % (
                  len(pidx), ', '.join(fmt_pct(float(v)) for v in explained[:3])),
              '', '### Re-centring (gain and bias folded into each parameter)',
              '| parameter | gain | bias | raw sum median | raw sum p10 | raw sum p90 | note |', '|---|---|---|---|---|---|---|']
    for r in resolved:
        fo = r.get('fold', {})
        lines.append('| %s | %s | %s | %s | %s | %s | %s |' % (
            r['name'], fmt_num(fo.get('gain', float('nan')), 3), fmt_num(r['bias'], 3), fmt_num(fo.get('median', float('nan'))),
            fmt_num(fo.get('p10', float('nan'))), fmt_num(fo.get('p90', float('nan'))),
            'folded' if fo.get('applied') else fo.get('reason', 'not folded')))
    lines += ['', '### Each parameter over the reference non-producers pooled (p10 / p50 / p90), beside the primary run\'s '
              'living non-producers and living producers',
              '| parameter | pooled reference (n=%d) | primary run, living non-producers (n=%d) | primary run, living '
              'producers (n=%d) |' % (len(ref), len(alive_np), len(alive_pr)), '|---|---|---|---|']
    for k, n in enumerate(pnames):
        cells = []
        for q in (q_ref, q_np, q_pr):
            cells.append('%.3f / %.3f / %.3f' % q[k][1:] if q else 'n/a')
        lines.append('| %s | %s | %s | %s |' % (n, cells[0], cells[1], cells[2]))
    lines += ['', '## Approximations'] + ['- ' + a for a in APPROXIMATIONS]
    if scatter_written:
        lines += ['', 'Scatter: pairs_scatter.png.']
    text = '\n'.join(lines) + '\n'
    with open(os.path.join(args.out, 'summary.md'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print(wt)
    print(text)
    return {'rho': rho, 'corner': corner, 'passing': passing, 'failing': failing, 'constant': sorted(constant),
            'n_pairs': len(sel), 'n_ids': len(ids), 'cliff': cliff, 'S': S, 'ids': ids, 'S_ref': S_ref, 'ev': ev,
            'ev_np': ev_np, 'ev_np_sample': ev_npS, 'reference': ref_report, 'n_reference': int(len(ref)),
            'resolved': resolved, 'quantiles_ref': q_ref, 'prod': prod}


def build_parser():
    ap = argparse.ArgumentParser(description='Genome-to-skin map, step 1 (see the module docstring).')
    ap.add_argument('run_dir')
    ap.add_argument('--at', type=float, required=True, help='simulated seconds; the nearest snapshot is used')
    ap.add_argument('--out', required=True, help='output folder (created)')
    ap.add_argument('--depth', type=int, default=3, help='ancestor generations added to the population')
    ap.add_argument('--max-cousins', type=int, default=4000)
    ap.add_argument('--max-siblings', type=int, default=2000)
    ap.add_argument('--strangers', type=int, default=4000)
    ap.add_argument('--check-pairs', type=int, default=20000, help='unrelated pairs for the parent-and-child check')
    ap.add_argument('--seed', type=int, default=1)
    ap.add_argument('--fail-ratio', type=float, default=0.5)
    ap.add_argument('--zclip', type=float, default=3.0)
    ap.add_argument('--bootstrap', type=int, default=200)
    ap.add_argument('--species-weights', help='cell,topology,parameter,brain (default: from config.json)')
    ap.add_argument('--weights-file', help='a weights.json to use in place of the draft weights')
    ap.add_argument('--alive-only-pairs', action='store_true', help='form pairs among living bodies only')
    ap.add_argument('--prior', action='append', help='an earlier segment of the same world (repeatable), read for '
                                                     'lineage and genomes the run directory lacks')
    ap.add_argument('--no-plot', action='store_true')
    ap.add_argument('--ref', action='append', default=[], metavar='RUN@SECONDS',
                    help='a reference run (repeatable): its non-producer bodies alive at the snapshot nearest SECONDS are '
                         'pooled into the reference population; with none, the reference is the primary run\'s living bodies')
    ap.add_argument('--no-fold', action='store_true', help='keep the input weights and a zero bias (no re-centring)')
    return ap


def main(argv=None):
    args = build_parser().parse_args(argv)
    try:
        run_tool(args)
    except ToolError as e:
        print(e, file=sys.stderr)
        return 2
    return 0


if __name__ == '__main__':
    sys.exit(main())
