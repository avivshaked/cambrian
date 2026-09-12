"""Where the bodies are: reads a run's positions.jsonl and draws it.

No file in a run said where a body was. The report carried a mean depth and per-patch bins, a
lineage row carried a patch index, a snapshot carried a genome. On 2026-09-10 the owner opened
round 33 seed 3 in the theatre and saw the whole world as two vertical ribbons a metre wide in a
box twenty metres long (logbook/0083); thirty-three rounds had been read on numbers that cannot
show a ribbon. positions.jsonl is the recording, and this is the reader: the same question asked
from the maths side, so that a ribbon can be seen in a number rather than only by eye.

Usage:

    python scripts/positions-read.py <arm> --summary [--every 10]
    python scripts/positions-read.py <arm> --at 1000,5000,20000 [--out scratch/positions/<arm>]

--summary prints, per printed sample: how many bodies the row carries, how many 1 m columns of
the footprint they stand on, the spread of x and z (the same statistic the report's `x sd` is:
circular in a box, because it wraps, and plain in a tank, because it does not), the median
nearest-neighbour distance flat and in three dimensions, the count and mean depth of each guild,
and, when lineage.jsonl is present, how many bodies have another body of their own clade within
a metre.

The water is whatever shape the run's config.json says it is: D077's periodic box, or the walled
cylinder of fable-propose-aquarium.md ruling 1 (logbook/specs/tank-spec.md). Everything that
depends on the shape lives in the two geometry classes below and nowhere else.

--at prints the same numbers for the sample nearest each time and, when matplotlib is installed,
writes four pictures per time: the box from the side, from the end, from above, and the side again
coloured by clade.

matplotlib is optional and is never installed by this script: without it the numbers still print
and the pictures say what is missing.

Exits 0 on a run it could read, 2 on a usage error or a run it could not find.
"""
import argparse
import json
import math
import os
import re
import sys


def fail(message):
    """Print to stderr and exit 2, so a launcher or a test can gate on the code rather than the
    prose (the same rule digest-diff.py's exit codes were written under)."""
    print(message, file=sys.stderr)
    sys.exit(2)


# ---------------------------------------------------------------- the file's own definitions

# The flag bits PositionsRow writes. Absorptive, jointed and photosynthetic are decided in the
# harness by the same tests that produce `absorpt`, `jointed` and `photo` in the report, so a
# guild read here and a count read there cannot disagree.
ABSORPTIVE = 1
JOINTED = 2
PHOTOSYNTHETIC = 4

# Ecosystem.ColumnMetres. A creature is metre-scale and the ribbon the instrument was built for
# was about a metre wide, so the footprint is counted in metre squares and this reader counts the
# same ones: its `cols` must reproduce the report's.
COLUMN_METRES = 1.0

# "Near" for the clade count, metres. One body length, which is the scale at which two bodies are
# competing for the same cell of water rather than merely living in the same world.
CLADE_RADIUS_METRES = 1.0

# Mutually exclusive, in this order, and the order is the point: a body carrying both tissues is a
# mixotroph and not a stomach that happens to photosynthesise. `plain` is not in the spec's four
# and is not decoration: a body of nothing but structure and buoyancy carries none of the three
# flags, and calling it a leaf because it is not a stomach would invent a producer.
GUILDS = ['leaf', 'stom', 'mixo', 'jnt', 'plain']

GUILD_LABELS = {
    'leaf': 'leaf (photosynthetic)',
    'stom': 'stomach (absorptive)',
    'mixo': 'photosynthetic mixotroph',
    'jnt': 'jointed, no tissue',
    'plain': 'no tissue, no joint',
}

GUILD_COLOURS = {
    'leaf': '#2e8b57',
    'stom': '#c1442e',
    'mixo': '#d9a441',
    'jnt': '#3b6fb6',
    'plain': '#8a8a8a',
}


def guild_of(flags):
    absorptive = flags & ABSORPTIVE
    photosynthetic = flags & PHOTOSYNTHETIC

    if absorptive and photosynthetic:
        return 'mixo'
    if absorptive:
        return 'stom'
    if photosynthetic:
        return 'leaf'
    if flags & JOINTED:
        return 'jnt'
    return 'plain'


# ---------------------------------------------------------------- finding the run

def newest_run(runs_root, arm):
    """runs/<arm>/<newest run directory>, by name, the way clade-score.ps1 picks one."""
    arm_dir = os.path.join(runs_root, arm)
    if not os.path.isdir(arm_dir):
        fail(f'{arm}: no directory at {arm_dir}')

    runs = sorted(d for d in os.listdir(arm_dir) if os.path.isdir(os.path.join(arm_dir, d)))
    if not runs:
        fail(f'{arm}: no run directory under {arm_dir}')

    return os.path.join(arm_dir, runs[-1])


def find_field(node, name):
    """The value stored under <name> anywhere in the config's group tree, or None.

    By name rather than by path, because the groups are derived from a Tunable attribute in the
    C# and a knob can be moved between them without changing what it means. The box is 20 by 5 by
    60 m in the campaign and nowhere in this script is allowed to know that.
    """
    if isinstance(node, dict):
        if name in node:
            return node[name]
        for value in node.values():
            found = find_field(value, name)
            if found is not None:
                return found
    return None


def world_of(config):
    """The water this run was in — a Box or a Tank, read from config.json and never guessed.

    The shape is a world rule (RunConfig.WorldShape, fable-propose-aquarium.md ruling 1), and the
    two geometries disagree about everything this reader prints: what a separation is, what a
    spread means, how many columns the footprint has, which patch a position is in. Defaulted to
    the box where the key is absent, for the same reason the layout is: every config written
    before the knob describes a periodic box, and those runs have no other shape to be.
    """
    area = find_field(config, 'worldAreaSquareMetres')
    patches = find_field(config, 'horizontalPatches')
    depth = find_field(config, 'worldDepthMetres')

    missing = [n for n, v in (('worldAreaSquareMetres', area),
                              ('horizontalPatches', patches),
                              ('worldDepthMetres', depth)) if v is None]
    if missing:
        fail('config.json carries none of: ' + ', '.join(missing))

    shape = find_field(config, 'worldShape')

    if shape is not None and str(shape).lower() == 'tank':
        return Tank(float(area), patches, float(depth))

    return Box(config, float(area), patches, float(depth))


class Water:
    """What both shapes have in common: a depth, a bounding rectangle, and a grid of columns.

    The bounding rectangle is the box itself in a box and the circle's bounding square in a tank
    (TankGeometry), so the pictures, the column grid and everything that wants an extent can be
    written once.
    """

    def __init__(self, length, width, depth):
        self.length = length
        self.width = width
        self.depth = float(depth)

        self.columns_x = max(1, math.ceil(self.length / COLUMN_METRES))
        self.columns_z = max(1, math.ceil(self.width / COLUMN_METRES))

    def column_of(self, x, z):
        """Which 1 m column a position stands in, clamped — Ecosystem.MeasureHorizontalSpread."""
        ix = min(self.columns_x - 1, max(0, int(math.floor(x / COLUMN_METRES))))
        iz = min(self.columns_z - 1, max(0, int(math.floor(z / COLUMN_METRES))))
        return ix, iz

    def occupied_columns(self, xs, zs):
        return len({self.column_of(xs[i], zs[i]) for i in range(len(xs))})

    @property
    def rim(self):
        """(x, z, radius) of the glass, or None in a box. What a picture draws the water as."""
        return None


class Box(Water):
    """D077's water: K patches of W metres laid K/A along x by A across z, D metres deep."""

    def __init__(self, config, area, patches, depth):
        # The layout (fable-propose-box.md), defaulted to 1 rather than demanded: every config
        # written before the knob describes a row of patches, and this reader's whole job is to
        # read runs that already exist. The C# refuses a missing key because a simulation that
        # guessed a world rule would run a world nobody asked for; a reader that guessed the
        # shape of a box already on disk would be wrong in a way the run could contradict, and
        # the runs on disk cannot: they have no other shape.
        across = find_field(config, 'patchesAcross')

        # SharedVolume's own arithmetic: K = max(1, patches), W = sqrt(area / K), one patch's
        # side; the box is K/A patches long and A wide.
        self.patches = max(1, int(patches))
        self.across = min(self.patches, max(1, int(across))) if across is not None else 1
        self.along = max(1, self.patches // self.across)
        self.patch_metres = math.sqrt(area / self.patches)

        Water.__init__(self, self.patch_metres * self.along, self.patch_metres * self.across, depth)

    @property
    def total_columns(self):
        """Every column of the box: a periodic footprint has no outside to exclude."""
        return self.columns_x * self.columns_z

    def dx(self, delta):
        return wrapped(delta, self.length)

    def dz(self, delta):
        return wrapped(delta, self.width)

    def spreads(self, xs, zs):
        """Circular standard deviations, as Ecosystem.CircularSpread takes them."""
        n = len(xs)
        sin_x = cos_x = sin_z = cos_z = 0.0

        for i in range(n):
            angle_x = 2.0 * math.pi * xs[i] / self.length
            angle_z = 2.0 * math.pi * zs[i] / self.width

            sin_x += math.sin(angle_x)
            cos_x += math.cos(angle_x)
            sin_z += math.sin(angle_z)
            cos_z += math.cos(angle_z)

        return (circular_spread(sin_x, cos_x, n, self.length),
                circular_spread(sin_z, cos_z, n, self.width))

    def patch_of(self, x, z):
        """The patch a position is in — World.PatchOfXZ, iz·(K/A) + ix, numbered along x first."""
        ix = int(math.floor((x % self.length) / self.patch_metres)) % self.along
        if self.across == 1:
            return ix

        iz = int(math.floor((z % self.width) / self.patch_metres)) % self.across
        return iz * self.along + ix

    def __str__(self):
        # The layout is named only where it is not a row, so a reading of a run recorded before
        # the knob says exactly what it always said.
        layout = '' if self.across == 1 else f' laid {self.along} x {self.across}'

        # The noun is the shape's own, not the caller's: "box 20 x 5 m" and "tank r=5.64 m"
        # are printed by one format string, and a reading of a box says exactly what it always
        # said.
        return (f'box {self.length:.3g} x {self.width:.3g} m, {self.depth:.3g} m deep, '
                f'{self.patches} patch(es){layout}')


class Tank(Water):
    """The aquarium: a cylinder of R = sqrt(area/pi) with a glass wall, D metres deep.

    fable-propose-aquarium.md ruling 1, logbook/specs/tank-spec.md. The bounding square is
    [0, 2R) x [0, 2R) with the axis at (R, R), so the storage and every picture stay rectangles
    (TankGeometry); the patches are rings of equal area rather than squares; and there is no seam
    anywhere, which is why the separations and the spreads below are the plain ones.
    """

    def __init__(self, area, patches, depth):
        self.rings = max(1, int(patches))
        self.radius = math.sqrt(area / math.pi)

        Water.__init__(self, 2.0 * self.radius, 2.0 * self.radius, depth)

        # The denominator is the footprint the population could be standing on, which is the
        # columns whose own centres are in the water -- the same test Ecosystem makes at the same
        # 1 m scale, so this reader's `cols` reproduces the report's. Counting the bounding square
        # instead would report a world packed into 79% of the columns as packed into 100% of them.
        self._live_columns = sum(
            1
            for ix in range(self.columns_x)
            for iz in range(self.columns_z)
            if self.inside((ix + 0.5) * COLUMN_METRES, (iz + 0.5) * COLUMN_METRES))

    def inside(self, x, z):
        """TankGeometry.Inside: (x-R)^2 + (z-R)^2 <= R^2."""
        dx = x - self.radius
        dz = z - self.radius
        return dx * dx + dz * dz <= self.radius * self.radius

    @property
    def total_columns(self):
        return self._live_columns

    @property
    def rim(self):
        return self.radius, self.radius, self.radius

    def dx(self, delta):
        """The plain separation. A tank has a wall where the box has a seam: nothing is ever
        translated, and folding a separation would make two bodies on opposite sides of the glass
        read as neighbours."""
        return abs(delta)

    def dz(self, delta):
        return abs(delta)

    def spreads(self, xs, zs):
        """Ordinary (population) standard deviations, as Ecosystem.PlainSpread takes them."""
        return plain_spread(xs), plain_spread(zs)

    def patch_of(self, x, z):
        """The ring of equal area a position falls in — TankGeometry.RingOf, floor(K.(r/R)^2)."""
        if self.radius <= 0:
            return 0

        dx = x - self.radius
        dz = z - self.radius
        ring = int((dx * dx + dz * dz) / (self.radius * self.radius) * self.rings)

        return min(self.rings - 1, max(0, ring))

    def __str__(self):
        return (f'tank r={self.radius:.3g} m ({math.pi * self.radius * self.radius:.3g} m2), '
                f'{self.depth:.3g} m deep, {self.rings} ring(s)')


# ---------------------------------------------------------------- the statistics

def circular_spread(sin_sum, cos_sum, count, extent):
    """Mardia's circular standard deviation in metres, as Ecosystem.CircularSpread computes it.

    The box is periodic, so x = 0.1 and x = 19.9 are neighbours and a linear standard deviation
    would call that pair the most spread population possible. Capped at one extent, where the
    formula is unbounded: a population spread perfectly evenly round the ring.
    """
    if count < 2 or extent <= 0:
        return 0.0

    r = math.sqrt(sin_sum * sin_sum + cos_sum * cos_sum) / count
    if r <= 0:
        return extent

    radians = math.sqrt(max(0.0, -2.0 * math.log(min(1.0, r))))
    return min(extent, radians * extent / (2.0 * math.pi))


def plain_spread(values):
    """The ordinary standard deviation, as Ecosystem.PlainSpread computes it — the tank's.

    The population form rather than the sample one, so that it is comparable with the depths
    beside it; 0 for fewer than two bodies, where there is no spread to speak of rather than a
    spread of nothing. There is no seam in a tank, so the circular statistic above would read a
    population packed against one side of the glass as evenly spread.
    """
    n = len(values)
    if n < 2:
        return 0.0

    mean = sum(values) / n
    variance = sum(v * v for v in values) / n - mean * mean

    return math.sqrt(variance) if variance > 0 else 0.0


def wrapped(delta, extent):
    """Separation on a periodic axis: the shorter way round."""
    delta = abs(delta)
    return extent - delta if delta > extent * 0.5 else delta


def nearest_neighbours(xs, ys, zs, water, max_queries):
    """Median nearest-neighbour distance, flat and in three dimensions.

    Brute force. A KD-tree is unnecessary at the populations this project runs and would be one
    more thing to be wrong; the cost is paid only at the samples actually printed, which is what
    --every is for. Above max_queries bodies the query set is thinned to every k-th body (the
    neighbours are still every body), and the caller marks the number approximate: the median of
    a thousand nearest-neighbour distances and of two thousand differ by less than the rounding.
    """
    n = len(xs)
    if n < 2:
        return None, None, False

    step = 1
    approximate = False
    if max_queries and n > max_queries:
        step = math.ceil(n / max_queries)
        approximate = True

    flat = []
    solid = []

    for i in range(0, n, step):
        xi, yi, zi = xs[i], ys[i], zs[i]
        best_flat = float('inf')
        best_solid = float('inf')

        for j in range(n):
            if j == i:
                continue

            dx = water.dx(xi - xs[j])
            dz = water.dz(zi - zs[j])
            dy = yi - ys[j]

            f = dx * dx + dz * dz
            s = f + dy * dy

            if f < best_flat:
                best_flat = f
            if s < best_solid:
                best_solid = s

        flat.append(math.sqrt(best_flat))
        solid.append(math.sqrt(best_solid))

    return median(flat), median(solid), approximate


def median(values):
    ordered = sorted(values)
    n = len(ordered)
    if n == 0:
        return None
    mid = n // 2
    return ordered[mid] if n % 2 else 0.5 * (ordered[mid - 1] + ordered[mid])


def close_kin(ids, xs, ys, zs, water, clade_of):
    """Bodies with another body of their own clade within CLADE_RADIUS_METRES, in three dimensions.

    The question logbook/0083 leaves behind: a clade packed around its founder's spot and a clade
    spread through the water have the same population and the same mean depth. None means the run
    has no lineage.jsonl to build clades from.
    """
    if clade_of is None:
        return None

    n = len(ids)
    clades = [clade_of.get(i) for i in ids]
    near = 0
    radius = CLADE_RADIUS_METRES * CLADE_RADIUS_METRES

    for i in range(n):
        ci = clades[i]
        if ci is None:
            continue

        for j in range(n):
            if j == i or clades[j] != ci:
                continue

            dx = water.dx(xs[i] - xs[j])
            dz = water.dz(zs[i] - zs[j])
            dy = ys[i] - ys[j]

            if dx * dx + dz * dz + dy * dy <= radius:
                near += 1
                break

    return near


# ---------------------------------------------------------------- lineage, for clades

BIRTH_ROW = re.compile(r'"id":(-?\d+).*?"p":(-?\d+)')


def read_clades(path):
    """id -> clade root, from lineage.jsonl. A founder is its own root.

    Streamed, and read with a regular expression rather than json.loads: a live run's
    lineage.jsonl runs into the hundreds of megabytes and parsing every row as a document to keep
    two integers costs minutes. Only birth rows carry a parent, and only their two fields are read.
    """
    if not os.path.exists(path):
        return None, 0

    parent = {}

    with open(path, encoding='utf-8') as f:
        for line in f:
            if '"e":"b"' not in line:
                continue

            found = BIRTH_ROW.search(line)
            if not found:
                continue

            parent[int(found.group(1))] = int(found.group(2))

    # Flattened iteratively rather than by recursion: a chain is one generation per link and a
    # long-lived world runs to tens of thousands of them, which is deeper than Python's stack.
    root = {}
    for start in parent:
        chain = []
        node = start

        while node not in root:
            if node not in parent or parent[node] < 0 or parent[node] == node:
                root[node] = node
                break

            chain.append(node)
            node = parent[node]

        top = root[node]
        for member in chain:
            root[member] = top

    return root, len(parent)


# ---------------------------------------------------------------- reading positions.jsonl

def sample_time(line):
    """The t of a row, without parsing the rest of it. None if the line is not a row."""
    if not line.startswith('{"t":'):
        return None

    end = line.find(',', 5)
    if end < 0:
        return None

    try:
        return float(line[5:end])
    except ValueError:
        return None


def stream(path):
    """Every complete row of positions.jsonl, one at a time, as (t, line).

    Never loads the file: 1,500 bodies is about 45 KB a sample and a full run is tens of
    megabytes, and a live run is being appended to while this reads it.
    """
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue

            t = sample_time(line)
            if t is not None:
                yield t, line


def unpack(line):
    """One row as (t, ids, xs, ys, zs, flags)."""
    row = json.loads(line)
    bodies = row['b']

    ids = [b[0] for b in bodies]
    xs = [b[1] for b in bodies]
    ys = [b[2] for b in bodies]
    zs = [b[3] for b in bodies]
    flags = [b[4] for b in bodies]

    if len(ids) != row['n']:
        fail(f"row at t={row['t']}: n says {row['n']} and the row carries {len(ids)}")

    return row['t'], ids, xs, ys, zs, flags


# ---------------------------------------------------------------- one sample, measured

class Sample:
    def __init__(self, t, ids, xs, ys, zs, flags, water, clade_of, max_queries):
        self.t = t
        self.ids = ids
        self.xs = xs
        self.ys = ys
        self.zs = zs
        self.flags = flags
        self.n = len(ids)
        self.water = water
        self.guilds = [guild_of(f) for f in flags]

        # Both of these are the water's own business and neither is computed here: a box counts
        # every column of its footprint and takes circular deviations, a tank counts the columns
        # inside the glass and takes plain ones, and a Sample that knew which was which would be a
        # second opinion about the shape of the world.
        self.columns = water.occupied_columns(xs, zs)
        self.x_spread, self.z_spread = water.spreads(xs, zs)

        self.nn_flat, self.nn_solid, self.nn_approximate = nearest_neighbours(
            xs, ys, zs, water, max_queries)

        self.guild_count = {g: 0 for g in GUILDS}
        depth_sum = {g: 0.0 for g in GUILDS}

        for i in range(self.n):
            g = self.guilds[i]
            self.guild_count[g] += 1
            depth_sum[g] += ys[i]

        # None rather than 0 for an empty guild: a stored 0 for "no such creature" is the trap the
        # report's own em-dash columns were written after.
        self.guild_depth = {
            g: (depth_sum[g] / self.guild_count[g] if self.guild_count[g] else None)
            for g in GUILDS
        }

        # The guilds above are exclusive, so a jointed leaf counts as a leaf and `jnt` is the
        # jointed body with no tissue. Round 34 (logbook/0080) reads jointed bodies against rigid
        # ones whatever they feed on, so the flag is counted again here on its own.
        jointed = [i for i in range(self.n) if flags[i] & JOINTED]
        self.jointed_count = len(jointed)
        self.jointed_depth = (sum(ys[i] for i in jointed) / len(jointed)) if jointed else None
        rigid_n = self.n - len(jointed)
        self.rigid_depth = ((sum(ys) - sum(ys[i] for i in jointed)) / rigid_n) if rigid_n else None

        self.close_kin = close_kin(ids, xs, ys, zs, water, clade_of)


# ---------------------------------------------------------------- printing

COLUMNS = [
    ('t', 9), ('n', 6), ('cols', 9), ('x sd', 7), ('z sd', 7), ('nn h', 8), ('nn 3d', 8),
    ('leaf', 6), ('leaf y', 8), ('stom', 6), ('stom y', 8), ('mixo', 6), ('mixo y', 8),
    ('jnt', 6), ('jnt y', 8), ('plain', 6), ('plain y', 8), ('clade 1m', 9),
    ('jointed', 8), ('joint y', 8), ('rigid y', 8),
]


def header_line():
    return ' '.join(name.rjust(width) for name, width in COLUMNS)


def number(value, places=2):
    return '-' if value is None else f'{value:.{places}f}'


def sample_line(s):
    tilde = '~' if s.nn_approximate else ''
    values = [
        f'{s.t:.1f}',
        str(s.n),
        f'{s.columns}/{s.water.total_columns}',
        number(s.x_spread),
        number(s.z_spread),
        tilde + number(s.nn_flat),
        tilde + number(s.nn_solid),
    ]

    for g in GUILDS:
        values.append(str(s.guild_count[g]))
        values.append(number(s.guild_depth[g]))

    values.append('-' if s.close_kin is None else str(s.close_kin))
    values.append(str(s.jointed_count))
    values.append(number(s.jointed_depth))
    values.append(number(s.rigid_depth))

    return ' '.join(v.rjust(width) for v, (_, width) in zip(values, COLUMNS))


# ---------------------------------------------------------------- pictures

def matplotlib_or_none():
    try:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        return plt
    except ImportError:
        return None


def draw(plt, sample, arm, out_dir, clade_of):
    """Four pictures of one sample: side, end, top, and the side again coloured by clade."""
    os.makedirs(out_dir, exist_ok=True)
    water = sample.water
    written = []

    views = [
        ('side', 'x (m)', 'y (m)', sample.xs, sample.ys, (0, water.length), (-water.depth, 0)),
        ('end', 'z (m)', 'y (m)', sample.zs, sample.ys, (0, water.width), (-water.depth, 0)),
        ('top', 'x (m)', 'z (m)', sample.xs, sample.zs, (0, water.length), (0, water.width)),
    ]

    for name, xlabel, ylabel, horizontal, vertical, xlim, ylim in views:
        fig, ax = plt.subplots(figsize=(9, 6))

        # The glass, on the one view that looks down it. In a tank the frame is the circle's
        # bounding square, so without this a viewer cannot tell a population against the wall
        # from one in open water -- the reason the theatre draws the same circle.
        if name == 'top' and water.rim is not None:
            cx, cz, radius = water.rim
            ax.add_patch(plt.Circle((cx, cz), radius, fill=False,
                                    edgecolor='#3b6fb6', linewidth=0.8, alpha=0.7))

        for g in GUILDS:
            picked = [i for i in range(sample.n) if sample.guilds[i] == g]
            if not picked:
                continue
            ax.scatter([horizontal[i] for i in picked], [vertical[i] for i in picked],
                       s=8, c=GUILD_COLOURS[g], label=GUILD_LABELS[g], alpha=0.75,
                       linewidths=0)

        frame(ax, xlim, ylim)
        ax.set_xlabel(xlabel)
        ax.set_ylabel(ylabel)
        ax.set_title(f'{arm} | t = {sample.t:.0f} s | alive {sample.n} | {name} view')
        if sample.n:
            # Outside the axes: the side and end views are tall and narrow, and a legend inside
            # them sat on the surface band, which is where most of the bodies are.
            ax.legend(loc='upper left', bbox_to_anchor=(1.02, 1.0), fontsize=8, framealpha=0.9)

        path = os.path.join(out_dir, f'{arm}-t{int(round(sample.t)):07d}-{name}.png')
        fig.savefig(path, dpi=110, bbox_inches='tight')
        plt.close(fig)
        written.append(path)

    clade_path = draw_clades(plt, sample, arm, out_dir, clade_of)
    if clade_path:
        written.append(clade_path)

    return written


def frame(ax, xlim, ylim):
    """The whole water in frame, so that two samples of one run can be laid side by side.

    A tank is framed by the circle's bounding square, for the reason SnapshotCamera.BoxOf frames
    one that way: the square and the circle touch on all four sides, so nothing is cropped and the
    corners cost a few percent of the picture.
    """
    ax.set_xlim(xlim)
    ax.set_ylim(ylim)
    ax.set_aspect('equal', adjustable='box')
    ax.grid(True, linewidth=0.3, alpha=0.4)

    for spine in ax.spines.values():
        spine.set_linewidth(0.8)


def draw_clades(plt, sample, arm, out_dir, clade_of):
    """The side view again, coloured by clade: the largest eight named, everything else grey."""
    if clade_of is None:
        return None

    clades = [clade_of.get(i) for i in sample.ids]

    counts = {}
    for c in clades:
        if c is not None:
            counts[c] = counts.get(c, 0) + 1

    largest = [c for c, _ in sorted(counts.items(), key=lambda kv: (-kv[1], kv[0]))[:8]]
    palette = ['#c1442e', '#2e8b57', '#3b6fb6', '#d9a441',
               '#7b4ea3', '#159c9c', '#b5651d', '#4a4a4a']

    fig, ax = plt.subplots(figsize=(9, 6))

    rest = [i for i in range(sample.n) if clades[i] not in largest]
    if rest:
        ax.scatter([sample.xs[i] for i in rest], [sample.ys[i] for i in rest],
                   s=8, c='#bbbbbb', label=f'{len(counts) - len(largest)} smaller clades',
                   alpha=0.7, linewidths=0)

    for rank, clade in enumerate(largest):
        picked = [i for i in range(sample.n) if clades[i] == clade]
        ax.scatter([sample.xs[i] for i in picked], [sample.ys[i] for i in picked],
                   s=8, c=palette[rank % len(palette)],
                   label=f'clade {clade} ({counts[clade]})', alpha=0.85, linewidths=0)

    frame(ax, (0, sample.water.length), (-sample.water.depth, 0))
    ax.set_xlabel('x (m)')
    ax.set_ylabel('y (m)')
    ax.set_title(f'{arm} | t = {sample.t:.0f} s | alive {sample.n} | side view by clade')
    if sample.n:
        ax.legend(loc='upper left', bbox_to_anchor=(1.02, 1.0), fontsize=7, framealpha=0.9)

    path = os.path.join(out_dir, f'{arm}-t{int(round(sample.t)):07d}-clades.png')
    fig.savefig(path, dpi=110, bbox_inches='tight')
    plt.close(fig)

    return path


# ---------------------------------------------------------------- the two modes

def summarise(args, run_dir, positions, water, clade_of, births):
    print(f'arm {args.arm} | run {os.path.basename(run_dir)} | {water}')
    print(f'columns of {COLUMN_METRES:.0f} m: {water.total_columns} | '
          + (f'lineage.jsonl: {births} births, '
             f'{len(set(clade_of.values()))} clades' if clade_of is not None
             else 'no lineage.jsonl: the clade column is a dash'))
    print()
    print(header_line())

    printed = 0
    read = 0

    for t, line in stream(positions):
        read += 1
        if (read - 1) % args.every:
            continue

        _, ids, xs, ys, zs, flags = unpack(line)
        sample = Sample(t, ids, xs, ys, zs, flags, water, clade_of, args.nn_max)
        print(sample_line(sample))
        printed += 1

    print()
    print(f'{printed} of {read} samples printed'
          + (f' (every {args.every})' if args.every > 1 else ''))

    if read == 0:
        print('positions.jsonl is empty: a tiled world writes no file at all, so an empty one '
              'means the run was killed before its first sample.')


def at_times(args, run_dir, positions, water, clade_of, births):
    wanted = []
    for piece in args.at.split(','):
        piece = piece.strip()
        if piece:
            wanted.append(float(piece))

    if not wanted:
        fail('--at needs at least one time')

    # One pass, keeping the best line so far for each wanted time. The file is streamed for the
    # reason it is written a row at a time: it is large, and it may still be being written.
    best = [(None, None)] * len(wanted)

    for t, line in stream(positions):
        for k, target in enumerate(wanted):
            gap = abs(t - target)
            if best[k][0] is None or gap < best[k][0]:
                best[k] = (gap, line)

    plt = matplotlib_or_none()
    if plt is None:
        print('matplotlib is not installed, so the numbers print and the pictures do not. '
              'Install it in an environment of your choosing; this script installs nothing.')
        print()

    out_dir = args.out

    print(f'arm {args.arm} | run {os.path.basename(run_dir)} | {water}')
    if clade_of is not None:
        print(f'lineage.jsonl: {births} births, {len(set(clade_of.values()))} clades')
    print()
    print(header_line())

    samples = []
    for k, target in enumerate(wanted):
        gap, line = best[k]
        if line is None:
            print(f'{target:.1f}: no sample in positions.jsonl')
            continue

        t, ids, xs, ys, zs, flags = unpack(line)
        sample = Sample(t, ids, xs, ys, zs, flags, water, clade_of, args.nn_max)
        samples.append((target, gap, sample))
        print(sample_line(sample))

    print()
    for target, gap, sample in samples:
        print(f'asked for t = {target:.1f} s, nearest sample t = {sample.t:.1f} s '
              f'({gap:.1f} s away)')

    if plt is None:
        return

    print()
    for _, _, sample in samples:
        for path in draw(plt, sample, args.arm, out_dir, clade_of):
            print(f'wrote {path}')


def main():
    parser = argparse.ArgumentParser(
        description='Read a run\'s positions.jsonl: where the bodies actually are.')
    parser.add_argument('arm', help='the arm name, resolved to the newest run under runs/<arm>/')
    parser.add_argument('--summary', action='store_true',
                        help='one line per sample of the numbers')
    parser.add_argument('--at', help='comma-separated simulated times to look at')
    parser.add_argument('--out', help='where the pictures go (default scratch/positions/<arm>)')
    parser.add_argument('--every', type=int, default=1,
                        help='with --summary, print every k-th sample (default 1)')
    parser.add_argument('--nn-max', type=int, default=1200,
                        help='bodies queried for the nearest-neighbour medians before the query '
                             'set is thinned and the number marked ~ (default 1200, 0 for all)')
    parser.add_argument('--runs-root', default=None,
                        help='the runs/ directory (default: the repository\'s own)')

    args = parser.parse_args()

    if not args.summary and not args.at:
        parser.error('one of --summary or --at is required')
    if args.every < 1:
        parser.error('--every must be at least 1')

    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    runs_root = args.runs_root or os.path.join(repo, 'runs')

    # Under the repository's own scratch/ rather than under whatever directory this was called
    # from, and never outside the repository at all: gitignored, so nothing lands in history, and
    # in-project, so writing there prompts nobody (the owner's rule, 2026-09-03).
    if not args.out:
        args.out = os.path.join(repo, 'scratch', 'positions', args.arm)

    run_dir = newest_run(runs_root, args.arm)
    positions = os.path.join(run_dir, 'positions.jsonl')

    if not os.path.exists(positions):
        fail(f'{args.arm}: no positions.jsonl in {run_dir}. A tiled world writes none, and '
                 'so does any run recorded before 2026-09-10.')

    config_path = os.path.join(run_dir, 'config.json')
    if not os.path.exists(config_path):
        fail(f'{args.arm}: no config.json in {run_dir}, so the box has no dimensions.')

    with open(config_path, encoding='utf-8') as f:
        water = world_of(json.load(f))

    clade_of, births = read_clades(os.path.join(run_dir, 'lineage.jsonl'))

    if args.summary:
        summarise(args, run_dir, positions, water, clade_of, births)

    if args.at:
        if args.summary:
            print()
        at_times(args, run_dir, positions, water, clade_of, births)

    return 0


if __name__ == '__main__':
    sys.exit(main())
