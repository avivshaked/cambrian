"""The safari's guide: a run's clades, named, ranked and described in cards of facts.

    python scripts/guide.py r46-s1
    python scripts/guide.py r46-s1 --runs-root D:/Projects/experiments/evolution-simulator/runs --top 10
    python scripts/guide.py r46-s1 --out scratch/safari-guide/r46-s1 --check

logbook/specs/safari-spec.md, "The guide", items 1 to 5. It reads a recorded run and writes
`guide/guide.json` and `guide/guide.md` beside it (under `runs/<arm>/<run>/guide/`, or wherever
`--out` names). The theatre's director reads the JSON; an entry that cites a guide copies the
`.md` into its round's read directory. Nothing else in the run directory is written.

A clade is the unit (item 1). It comes from the parent walk `scripts/clade-score.ps1` uses,
generalised from one flag to three: a birth whose expressed flags (absorptive, jointed,
photosynthetic, the lineage row's `abs`, `jnt` and `pho`) differ from its parent's founds a
clade, a founder founds one, and a child with its parent's flags belongs to its parent's clade.
So the guide's clades are finer than the scorer's. Every scorer clade over one flag is the
disjoint union of the guide clades that carry that flag and descend inside it, and `--check`
prints that relation and the scorer's largest clades as the walk reproduces them.

A name (item 2) is a deterministic binomial. The genus comes from the flag triple through
GENERA below, so every eater's genus sounds like an eater's. The epithet comes from the SHA-256
of the founder's genome, the snapshot row nearest the founding that holds the founder, through a
syllable table. When no snapshot holds the founder (it died inside its first 100 s), the epithet
hashes the founder's lineage row instead, and the card says so. Collisions take a Roman
numeral. Names depend on nothing but the run's files, so every machine gets the same ones.

A card (item 3) holds what the recording knows. The body's adult volume and part count come from
a port of `Developer.Develop` (`src/Evosim.Core/Development/Developer.cs`) that counts parts and
volumes and places nothing: it is exact while the reach bound is off, which it is in every round
through 46, and the guide says so when a config turns it on. The port is checked on every run it
reads: the flags it develops from a founder's genome are compared with the lineage row's, and the
agreement is printed in the JSON's `checks`. Height above the floor reads `fields/bed.f32` when
the run wrote it; otherwise the floor is not rebuilt (it needs Core's bed code and the seed) and
the card gives the depth alone.

The economics come from the ledger (`scripts/ledger.ps1`, one .NET process a call, run one at a
time) on every trip and picker founder's genome, at clearance 10, the card's median depth rounded,
densities 0.5, 1 and 2 J/m3 and a spent density of 0.25 units/m3: the standing watts, the
break-even density, and the net watts, R0, first child and lifetime at each density. The genome
line is written to `<out>/genomes/<id>.json` first. A founder no snapshot holds keeps `economics`
null and `economics_note` says why. `--no-economics` skips the pass.

Interesting (item 4) is a weighted score over five readings, each 0 to 1. WEIGHTS and the
readings' definitions are below and printed in the guide. The top `--top` by score among clades
the positions ever show are the default trip; every clade with PICKER_MIN_MEMBERS members ever is
in the picker.

Standard library only. lineage.jsonl, positions.jsonl and the snapshots are streamed one row at a
time. Exits 0 on a run it could read and 2 when it could not find or read one.
"""
import argparse
import bisect
import hashlib
import json
import math
import os
import struct
import sys
import re
import shutil
import subprocess
import time

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# ------------------------------------------------------------------------------ the score

# The interesting score is sum(WEIGHTS[k] * reading[k]); the weights sum to 1.
WEIGHTS = {
    'novelty': 0.20,
    'success': 0.30,
    'persistence': 0.20,
    'rarity': 0.10,
    'firsts': 0.20,
}

# The readings, as the guide prints them.
READINGS = {
    'novelty': '1 when it is the first clade with its flag triple to reach ten members ever; '
               '0.5 when no earlier clade carried the triple at all but it never reached ten; '
               '0 otherwise. (A founder in the first seconds is first by the lottery, so first '
               'appearance alone counts for half.)',
    'success': 'the largest share of the living it held at any sample with at least '
               '100 bodies alive (so the founding minutes, when a clade of three is a third of '
               'the world, do not count).',
    'persistence': 'its life, founding to extinction or to the end of the run, over the run.',
    'rarity': 'persistence times (1 - log10(members ever) / log10(the most members any clade '
              'had)): few members over a long life.',
    'firsts': '0.5 for each first it holds, capped at 1.',
}

MIN_LIVING_FOR_SHARE = 100
PICKER_MIN_MEMBERS = 10
PEAK_BODY_SAMPLE = 25

# -------------------------------------------------------------------------------- the names

# One table of genera per flag triple (absorptive, jointed, photosynthetic), each a name and its
# grammatical gender, so the epithet agrees with it. Stems: phyll-, thall-, frond-, lamin- for
# leaves; gastr-, phag-, vor- for eaters; remi-, necto-, pinn-, natat- for swimmers; mix- where
# both tissues meet.
GENERA = {
    (0, 0, 0): [('Globulus', 'm'), ('Saccula', 'f'), ('Vesicula', 'f'), ('Inanium', 'n')],
    (0, 0, 1): [('Phyllina', 'f'), ('Thallus', 'm'), ('Lamina', 'f'), ('Frondium', 'n')],
    (1, 0, 0): [('Gastrella', 'f'), ('Phagus', 'm'), ('Voratrix', 'f'), ('Stomachium', 'n')],
    (1, 0, 1): [('Mixophyllum', 'n'), ('Gastrophylla', 'f'), ('Phagothallus', 'm'), ('Vorafrons', 'f')],
    (0, 1, 0): [('Remigia', 'f'), ('Natator', 'm'), ('Pinnula', 'f'), ('Nectium', 'n')],
    (1, 1, 0): [('Remiphagus', 'm'), ('Nectophaga', 'f'), ('Pinnivora', 'f'), ('Natogastrum', 'n')],
    (0, 1, 1): [('Remiphylla', 'f'), ('Nectothallus', 'm'), ('Pinnifrons', 'f'), ('Natophyllum', 'n')],
    (1, 1, 1): [('Nectomixus', 'm'), ('Remimixa', 'f'), ('Pinnimixum', 'n'), ('Natovora', 'f')],
}

ONSETS = ['b', 'c', 'd', 'f', 'g', 'l', 'm', 'n', 'p', 'r', 's', 't', 'v', 'cr', 'tr', 'pl']
VOWELS = ['a', 'e', 'i', 'o', 'u', 'a', 'e', 'i']
ENDINGS = {'m': ['us', 'is', 'ens', 'ax'], 'f': ['a', 'is', 'ens', 'ax'], 'n': ['um', 'e', 'ens', 'ax']}
ROMAN = ['', ' II', ' III', ' IV', ' V', ' VI', ' VII', ' VIII', ' IX', ' X']

GUILDS = {
    (0, 0, 0): 'no tissue and no joint',
    (0, 0, 1): 'leaf',
    (1, 0, 0): 'stomach',
    (1, 0, 1): 'mixotroph',
    (0, 1, 0): 'jointed body with no tissue',
    (1, 1, 0): 'jointed stomach',
    (0, 1, 1): 'jointed leaf',
    (1, 1, 1): 'jointed mixotroph',
}

GUILD_PLURALS = {
    (0, 0, 0): 'bodies with no tissue and no joint',
    (0, 0, 1): 'leaves',
    (1, 0, 0): 'stomachs',
    (1, 0, 1): 'mixotrophs',
    (0, 1, 0): 'jointed bodies with no tissue',
    (1, 1, 0): 'jointed stomachs',
    (0, 1, 1): 'jointed leaves',
    (1, 1, 1): 'jointed mixotrophs',
}

FOUNDER_SOURCES = {
    'floor': 'a random founder with no parent, added by the population floor while the world '
             'was small',
    'trickle': 'a random founder with no parent, added by the trickle of founders that arrives '
               'at a steady rate',
}

FLAG_WORDS = {0: 'absorptive tissue', 1: 'a joint', 2: 'photosynthetic tissue'}

FIRST_WORDS = {
    'first_jointed_breeder': 'the first jointed body to breed',
    'first_producer': 'the first photosynthetic body to breed',
    'first_eater_breeder': 'the first absorptive body to breed',
    'deepest': 'the deepest median depth of the clades with ten members',
    'shallowest': 'the shallowest median depth of the clades with ten members',
    'longest_lived': 'the longest life of any clade',
}


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def find_field(node, name):
    """The value stored under <name> anywhere in the config's group tree, or None."""
    if isinstance(node, dict):
        if name in node:
            return node[name]
        for value in node.values():
            found = find_field(value, name)
            if found is not None:
                return found
    return None


def read_floats(path):
    with open(path, 'rb') as f:
        data = f.read()
    count = len(data) // 4
    return struct.unpack('<%df' % count, data[:count * 4])


# ----------------------------------------------------------------------- the developer's port

JOINT_DOF = {'Fixed': 0, 'Hinge': 1, 'Twist': 1, 'HingeTwist': 2, 'TwistHinge': 2,
             'Universal': 2, 'Spherical': 3}


def shape_volume(shape, h):
    hx, hy, hz = abs(h[0]), abs(h[1]), abs(h[2])
    if shape == 'sphere':
        r = (hx + hy + hz) / 3.0
        return 4.0 / 3.0 * math.pi * r ** 3
    if shape == 'capsule':
        r = (hx + hz) / 2.0
        span = max(0.0, hy - r)
        return math.pi * r * r * 2.0 * span + 4.0 / 3.0 * math.pi * r ** 3
    return 8.0 * hx * hy * hz


def develop(genome, limits):
    """(parts, volume) of the adult body: parts is a list of (cell, shape, joint, volume).

    Developer.Expand without the transforms. The order of admission, the volume tails, the part
    cap, the depth cap, the recursion rule (terminal edges fire only when every non-terminal edge
    is spent, and the limit binds every edge) and the mirror copies are the C#'s. Placement only
    matters to the reach bound, which this port does not apply; the caller reports it.
    """
    nodes = genome['nodes']
    counts = genome.get('moduleCounts')
    occ = [0] * len(nodes)
    root = genome.get('root', 0)
    occ[root] = 1
    parts = []
    s0 = float(genome['adultScale'])
    floor = limits['minPartHalfExtent']

    def count_for(i):
        n = nodes[i]
        lim = n['recursiveLimit']
        if n.get('growth') != 'Indeterminate' or not counts or i >= len(counts):
            return lim
        return max(lim, counts[i])

    def expand(i, scale, depth, joint):
        n = nodes[i]
        d = n['dimensions']
        h = []
        for v, s in ((d['x'], scale[0]), (d['y'], scale[1]), (d['z'], scale[2])):
            a = abs(v * s)
            h.append(a if a > floor else floor)
        vol = shape_volume(n['shape'], h)
        if vol < limits['minPartVolume'] or vol > limits['maxPartVolume']:
            return
        if len(parts) >= limits['maxParts']:
            return
        parts.append((n['cell'], n['shape'], joint, vol))
        if depth >= limits['maxDepth']:
            return
        exhausted = True
        for e in n['edges']:
            if not e['terminalOnly'] and occ[e['child']] < count_for(e['child']):
                exhausted = False
                break
        for e in n['edges']:
            if e['terminalOnly'] != exhausted:
                continue
            c = e['child']
            if not occ[c] < count_for(c):
                continue
            es = e['scale']
            child_scale = (scale[0] * es['x'], scale[1] * es['y'], scale[2] * es['z'])
            r = e.get('reflect', {})
            copies = 1 << sum(1 for k in ('x', 'y', 'z') if r.get(k))
            for _ in range(copies):
                occ[c] += 1
                expand(c, child_scale, depth + 1, nodes[c]['joint'])
                occ[c] -= 1
                if len(parts) >= limits['maxParts']:
                    return

    expand(root, (s0, s0, s0), 0, 'Fixed')
    return parts, sum(p[3] for p in parts)


def body_of(genome, limits):
    parts, volume = develop(genome, limits)
    cells = {}
    for p in parts:
        cells[p[0]] = cells.get(p[0], 0) + 1
    dof_parts = sum(1 for p in parts if JOINT_DOF.get(p[2], 1) > 0)
    return {
        'parts': len(parts),
        'adult_volume_m3': volume,
        'nodes': len(genome['nodes']),
        'adult_scale': float(genome['adultScale']),
        'part_cells': dict(sorted(cells.items())),
        'jointed_parts': dof_parts,
        'node_cells': count_values(n['cell'] for n in genome['nodes']),
        'flags': (1 if cells.get('absorptive') else 0, 1 if dof_parts > 0 else 0,
                  1 if cells.get('photosynthetic') else 0),
    }


def count_values(values):
    out = {}
    for v in values:
        out[v] = out.get(v, 0) + 1
    return dict(sorted(out.items()))


# --------------------------------------------------------------------------------- the names

def canonical_genome_bytes(row):
    g = {k: v for k, v in row.items() if k != 'id'}
    return json.dumps(g, sort_keys=True, separators=(',', ':')).encode('utf-8')


def lineage_row_bytes(birth_id, b):
    keys = sorted(b.keys())
    return json.dumps({'id': birth_id, **{k: b[k] for k in keys}}, sort_keys=True,
                      separators=(',', ':')).encode('utf-8')


def binomial(triple, digest):
    genera = GENERA[triple]
    genus, gender = genera[digest[0] % len(genera)]
    syllables = 2 + digest[1] % 2
    word = ''
    for s in range(syllables):
        word += ONSETS[digest[2 + 2 * s] % len(ONSETS)] + VOWELS[digest[3 + 2 * s] % len(VOWELS)]
    word += ONSETS[digest[8] % len(ONSETS)]
    word += ENDINGS[gender][digest[9] % len(ENDINGS[gender])]
    return genus, word


# ------------------------------------------------------------------------------- formatting

def fmt_s(t):
    if t is None:
        return 'unknown'
    if abs(t - round(t)) < 1e-9:
        return '{:,} s'.format(int(round(t)))
    return '{:,.1f} s'.format(t)


def fmt_n(n):
    return '{:,}'.format(n)


def fmt_m(v):
    return 'unknown' if v is None else '%.1f m' % v


def fmt_vol(v):
    if v is None:
        return 'unknown'
    if v >= 0.01:
        return '%.3f m³' % v
    return '%.2g m³' % v


def rnd(v, k=3):
    return None if v is None else round(v, k)


def plural(n, noun):
    if n == 0:
        return 'no ' + noun
    number = fmt_n(int(n)) if n == int(n) else '%g' % n
    return '%s %s%s' % (number, noun, '' if n == 1 else 's')


def versus(pair, differ, same):
    """'has 3 nodes where the parent's had 2', or the same count phrased as the same."""
    a, b = pair
    if a == b:
        return (same % a) + ('' if a == 1 else 's')
    return (differ % b) + ('' if b == 1 else 's') + ' where the parent\'s had %d' % a


def join_words(words):
    words = list(words)
    if not words:
        return ''
    if len(words) == 1:
        return words[0]
    return ', '.join(words[:-1]) + ' and ' + words[-1]


# ------------------------------------------------------------------------------ the reading

def median_of_hist(hist, scale):
    total = sum(hist.values())
    if total == 0:
        return None
    want_lo, want_hi = (total - 1) // 2, total // 2
    run = 0
    lo = hi = None
    for key in sorted(hist):
        run += hist[key]
        if lo is None and run > want_lo:
            lo = key
        if run > want_hi:
            hi = key
            break
    return (lo + hi) / 2.0 / scale


def resolve_run(runs_root, arm, run):
    arm_dir = os.path.join(runs_root, arm)
    if run:
        path = run if os.path.isabs(run) else os.path.join(arm_dir, run)
        if not os.path.isdir(path):
            fail('%s: no run directory at %s' % (arm, path))
        return path
    if not os.path.isdir(arm_dir):
        fail('%s: no directory at %s' % (arm, arm_dir))
    runs = sorted(d for d in os.listdir(arm_dir) if os.path.isdir(os.path.join(arm_dir, d)))
    if not runs:
        fail('%s: no run directory under %s' % (arm, arm_dir))
    return os.path.join(arm_dir, runs[-1])


def read_lineage(path):
    births = {}
    deaths = {}
    other = 0
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            if not line.strip():
                continue
            row = json.loads(line)
            e = row.get('e')
            if e == 'b':
                births[row['id']] = row
            elif e == 'd':
                deaths[row['id']] = row['t']
            else:
                other += 1
    return births, deaths, other


def build_clades(births):
    """The parent walk over three flags. Returns (clade_of, clades) in founding order."""
    clade_of = {}
    clades = []
    for i in sorted(births):
        b = births[i]
        flags = (b.get('abs', 0), b.get('jnt', 0), b.get('pho', 0))
        p = b['p']
        pb = births.get(p) if p != -1 else None
        if pb is not None:
            pflags = (pb.get('abs', 0), pb.get('jnt', 0), pb.get('pho', 0))
        if p == -1 or pb is None or pflags != flags:
            c = len(clades)
            clades.append({
                'index': c,
                'founder': i,
                'founded_at': b['t'],
                'kind': 'founder' if p == -1 else ('split' if pb is not None else 'orphan'),
                'parent_body': None if p == -1 else p,
                'parent_clade': clade_of.get(p) if pb is not None else None,
                'flags': flags,
                'members': [i],
                'founder_generation': b.get('g', 0),
                'max_generation': b.get('g', 0),
            })
            clade_of[i] = c
        else:
            c = clade_of[p]
            clade_of[i] = c
            cl = clades[c]
            cl['members'].append(i)
            if b.get('g', 0) > cl['max_generation']:
                cl['max_generation'] = b.get('g', 0)
    return clade_of, clades


def scorer_clades(births, trait):
    """clade-score.ps1's Get-Clades, one flag: root -> members, in the scorer's order."""
    root = {}
    for i in sorted(births):
        b = births[i]
        if b.get(trait, 0) != 1:
            continue
        p = b['p']
        pb = births.get(p)
        if p == -1 or pb is None or pb.get(trait, 0) != 1:
            root[i] = i
        else:
            root[i] = root.get(p, p)
    out = {}
    for i in root:
        out.setdefault(root[i], []).append(i)
    return out


def read_positions(path, clade_of, n_clades, radius, bed):
    """One pass over positions.jsonl: per-clade peaks, success share and location histograms."""
    peak = [0] * n_clades
    peak_t = [None] * n_clades
    peak_living = [0] * n_clades
    best_share = [0.0] * n_clades
    first_seen = [None] * n_clades
    last_seen = [None] * n_clades
    depth_h = [None] * n_clades
    radius_h = [None] * n_clades
    height_h = [None] * n_clades
    seen_samples = [0] * n_clades
    samples = []
    last_counts = {}
    unknown_ids = 0
    cx = cz = radius
    if bed is not None:
        bed_values, bnx, bnz, bcell = bed
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            if not line.strip():
                continue
            row = json.loads(line)
            t = row['t']
            bodies = row['b']
            living = len(bodies)
            samples.append((t, living))
            counts = {}
            for body in bodies:
                c = clade_of.get(body[0])
                if c is None:
                    unknown_ids += 1
                    continue
                counts[c] = counts.get(c, 0) + 1
                y = body[2]
                dh = depth_h[c]
                if dh is None:
                    dh = depth_h[c] = {}
                    radius_h[c] = {}
                    height_h[c] = {}
                k = int(round(-y * 10))
                dh[k] = dh.get(k, 0) + 1
                if radius is not None:
                    r = math.hypot(body[1] - cx, body[3] - cz)
                    k = int(round(r * 10))
                    rh = radius_h[c]
                    rh[k] = rh.get(k, 0) + 1
                if bed is not None:
                    ix = min(max(int(body[1] / bcell), 0), bnx - 1)
                    iz = min(max(int(body[3] / bcell), 0), bnz - 1)
                    k = int(round((y - bed_values[ix * bnz + iz]) * 10))
                    hh = height_h[c]
                    hh[k] = hh.get(k, 0) + 1
            for c, n in counts.items():
                seen_samples[c] += 1
                if n > peak[c]:
                    peak[c] = n
                    peak_t[c] = t
                    peak_living[c] = living
                if living >= MIN_LIVING_FOR_SHARE and n / living > best_share[c]:
                    best_share[c] = n / living
                if first_seen[c] is None:
                    first_seen[c] = t
                last_seen[c] = t
            last_counts = counts
    return {
        'peak': peak, 'peak_t': peak_t, 'peak_living': peak_living, 'best_share': best_share,
        'first_seen': first_seen, 'last_seen': last_seen, 'seen_samples': seen_samples,
        'depth_h': depth_h, 'radius_h': radius_h, 'height_h': height_h, 'samples': samples,
        'last_counts': last_counts, 'unknown_ids': unknown_ids,
    }


def snapshot_seconds(run_dir):
    d = os.path.join(run_dir, 'snapshots')
    if not os.path.isdir(d):
        return []
    out = []
    for name in os.listdir(d):
        if name.endswith('.jsonl') and name.split('.')[0].isdigit():
            out.append(int(name.split('.')[0]))
    return sorted(out)


def first_snapshot_holding(snaps, born, died):
    """The first snapshot second at or after a birth at which the body was still alive."""
    k = bisect.bisect_left(snaps, born)
    if k >= len(snaps):
        return None
    s = snaps[k]
    if died is not None and died <= s:
        return None
    return s


def read_snapshot_rows(run_dir, requests, raw=None):
    """{(second, id): row} for every requested id, one pass per requested file.

    When `raw` is a dict, the row's own line is kept in it under the same key, so a genome file
    written for the ledger is the snapshot's bytes and not a re-serialisation.
    """
    found = {}
    for s in sorted(requests):
        wanted = requests[s]
        path = os.path.join(run_dir, 'snapshots', '%09d.jsonl' % s)
        with open(path, 'r', encoding='utf-8') as f:
            for line in f:
                if not line.startswith('{"id":'):
                    continue
                comma = line.find(',', 6)
                try:
                    i = int(line[6:comma])
                except ValueError:
                    continue
                if i in wanted:
                    found[(s, i)] = json.loads(line)
                    if raw is not None:
                        raw[(s, i)] = line.rstrip('\r\n')
    return found


# ---------------------------------------------------------------------------- the ledger

LEDGER_CLEARANCE = '10'
LEDGER_DENSITIES = ['0.5', '1', '2']
LEDGER_SPENT = '0.25'
LEDGER_TIMEOUT_SECONDS = 600


def powershell():
    return shutil.which('pwsh') or shutil.which('powershell')


def run_ledger(genome_path, config_path, depth):
    """One `scripts/ledger.ps1` call, parsed. Returns (economics, error)."""
    script = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'ledger.ps1')
    shell = powershell()
    if shell is None:
        return None, 'no PowerShell on the path'
    # All three sweep arguments are passed, or the script prompts for them and never returns
    # under a non-interactive shell (CLAUDE.md, the tunable gotcha). The density list reaches
    # the script as one string through -File; ledger.ps1 joins its array with commas, so one
    # comma-separated string passes through unchanged.
    cmd = [shell, '-NoProfile', '-NonInteractive', '-File', script,
           '-Genome', genome_path, '-Config', config_path,
           '-Clearance', LEDGER_CLEARANCE, '-Depth', str(depth),
           '-Density', ','.join(LEDGER_DENSITIES), '-Spent', LEDGER_SPENT]
    try:
        done = subprocess.run(cmd, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                              stderr=subprocess.STDOUT, timeout=LEDGER_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        return None, 'the ledger call timed out'
    text = done.stdout.decode('utf-8', errors='replace')
    if done.returncode != 0:
        return None, 'the ledger exited %d' % done.returncode
    m = re.search(r'Standing cost:\s*([-0-9.eE]+)\s*W', text)
    if not m:
        return None, 'no standing cost in the ledger output'
    standing = float(m.group(1))
    section = text.split('### Clearance = %s' % LEDGER_CLEARANCE, 1)
    rows = {}
    if len(section) == 2:
        for line in section[1].splitlines():
            cells = [c.strip() for c in line.strip().strip('|').split('|')]
            if len(cells) < 8 or not re.match(r'^-?[0-9.]+$', cells[0]):
                continue
            rows[cells[1]] = cells
    if not rows:
        return None, 'no clearance table in the ledger output'

    def num(v):
        v = v.rstrip('*')
        try:
            return float(v)
        except ValueError:
            return v or None

    first = rows[next(iter(rows))]
    economics = {
        'source': 'scripts/ledger.ps1 -Clearance %s -Depth %s -Density %s -Spent %s'
                  % (LEDGER_CLEARANCE, depth, ','.join(LEDGER_DENSITIES), LEDGER_SPENT),
        'depth_m': depth,
        'standing_w': standing,
        'break_even_j_per_m3': num(first[3]),
        'net_w': {},
        'r0': {},
        'first_child_s': {},
        'lifetime_s': {},
        'lifetime_censored': {},
    }
    for d in LEDGER_DENSITIES:
        cells = rows.get(d)
        if cells is None:
            continue
        economics['net_w'][d] = num(cells[2])
        economics['lifetime_s'][d] = num(cells[4])
        economics['lifetime_censored'][d] = cells[4].endswith('*')
        economics['r0'][d] = num(cells[5])
        economics['first_child_s'][d] = num(cells[6])
    return economics, None


# ------------------------------------------------------------------------------ the cards

def split_sentences(card, names):
    """What changed at the split, as sentences that state and stop."""
    out = []
    sp = card['split']
    if card['kind'] == 'founder':
        how = FOUNDER_SOURCES.get(card.get('founder_source'), 'a random founder with no parent')
        out.append('Body %s founded it at %s. It was %s.'
                   % (card['founder'], fmt_s(card['founded_at']), how))
        return out
    if card['kind'] == 'orphan':
        out.append('Body %s founded it at %s. Its parent, body %s, has no birth row in the lineage.'
                   % (card['founder'], fmt_s(card['founded_at']), card['parent_body']))
        return out
    parent_name = names.get(card['parent_clade'], 'clade %s' % card['parent_clade'])
    out.append('Body %s founded it at %s as a child of body %s in %s.'
               % (card['founder'], fmt_s(card['founded_at']), card['parent_body'], parent_name))
    parts = []
    if sp['flags_gained']:
        parts.append('gained ' + join_words(sp['flags_gained']))
    if sp['flags_lost']:
        parts.append('lost ' + join_words(sp['flags_lost']))
    out.append('The split %s.' % ' and '.join(parts))
    g = sp.get('genome')
    if g is None:
        out.append('The genome difference is not in the recording: %s.' % sp['genome_note'])
        lr = sp.get('lineage_row', {})
        if 'adult_scale' in lr:
            a, b = lr['adult_scale']
            if a != b:
                out.append('The lineage rows give an adult scale of %.3g against the parent\'s %.3g.'
                           % (b, a))
        return out
    out.append('Against the parent\'s genome, the founder\'s %s and %s.'
               % (versus(g['nodes'], 'has %s node', 'has the parent\'s %s node'),
                  versus(g['parts'], 'develops into %s part', 'develops into %s part')))
    changes = []
    for cell, (a, b) in sorted(g['part_cells'].items()):
        if b > a:
            changes.append('%d more %s part%s' % (b - a, cell, '' if b - a == 1 else 's'))
        elif a > b:
            changes.append('%d fewer %s part%s' % (a - b, cell, '' if a - b == 1 else 's'))
    if changes:
        out.append('The founder has %s.' % join_words(changes))
    ja, jb = g['jointed_parts']
    if ja != jb:
        out.append('It has %s where the parent had %d.' % (plural(jb, 'jointed part'), ja))
    a, b = g['adult_scale']
    if abs(a - b) > 1e-9:
        out.append('Its adult scale is %.3g against the parent\'s %.3g.' % (b, a))
    return out


def fmt_num(v):
    """A ledger number as the card prints it: two significant figures, or the ledger's word."""
    if isinstance(v, (int, float)):
        if v == int(v) and abs(v) < 1e6:
            return fmt_n(int(v))
        return '%.2g' % v
    return str(v)


def economics_sentence(ec):
    """One sentence of the ledger's facts for the founder, at the card's median depth.

    The ledger prints `none` for the break-even when no density it can reach balances the
    body: either it nets a positive watt with no food at all (a leaf, whose ledger does not move
    with the density) or it loses at every density. The sentence says which from the net watts.
    """
    be = ec['break_even_j_per_m3']
    nets = [v for v in ec['net_w'].values() if isinstance(v, (int, float))]
    flat = len(nets) > 1 and max(nets) - min(nets) < 1e-9
    head = 'At %d m the ledger gives its founder a standing cost of %.2f W' % (
        ec['depth_m'], ec['standing_w'])
    r0 = ec['r0'].get('1')
    if isinstance(r0, (int, float)) and r0 > 0:
        breed = 'it breeds with R0 %s' % fmt_num(r0)
    elif isinstance(r0, (int, float)):
        breed = 'it does not breed (R0 0)'
    else:
        breed = 'the ledger gives no R0'
    if flat and nets:
        return ('%s. Its net is %+.2f W whatever the food in the water, and %s.'
                % (head, nets[0], breed))
    if isinstance(be, (int, float)):
        if be == 0:
            be_text = 'It breaks even at 0 J/m³, so it pays its way with no food'
        else:
            be_text = 'It breaks even at %s J/m³' % fmt_num(be)
    elif nets and min(nets) > 0:
        be_text = 'It has no break-even, since it nets a positive watt at every density tried'
    else:
        be_text = 'It does not break even up to %s J/m³' % LEDGER_DENSITIES[-1]
    return '%s. %s, and at 1 J/m³ %s.' % (head, be_text, breed)


def card_prose(card, names, rank_line):
    guild = GUILD_PLURALS[tuple(card['flags_tuple'])]
    lines = []
    head = '### %s' % card['name']
    lines.append(head)
    lines.append('')
    s = ['%s is a clade of %s, clade %d of the run.' % (card['name'], guild, card['clade'])]
    s += split_sentences(card, names)
    lines.append(' '.join(s))
    lines.append('')

    s = []
    pk = card['peak']
    s.append('It had %s member%s over its life.' % (fmt_n(card['members_ever']),
                                                  '' if card['members_ever'] == 1 else 's'))
    if pk['count'] > 0:
        s.append('Its peak was %s living at %s, %.1f%% of the %s bodies alive then.'
                 % (fmt_n(pk['count']), fmt_s(pk['at']), 100.0 * pk['share_of_living'],
                    fmt_n(pk['living'])))
    else:
        s.append('No sample of the positions shows a member alive.')
    gen = card['generations']
    if gen['depth'] > 0:
        s.append('Its lines reached generation %d, %d below its founder.' % (gen['max'], gen['depth']))
    else:
        s.append('No member was born to a member.')
    if card['extinct_at'] is None:
        s.append('It was alive at the end with %s member%s.'
                 % (fmt_n(card['alive_at_end']), '' if card['alive_at_end'] == 1 else 's'))
    else:
        s.append('It died out at %s, %s after its founding.'
                 % (fmt_s(card['extinct_at']), fmt_s(card['extinct_at'] - card['founded_at'])))
    lines.append(' '.join(s))
    lines.append('')

    w = card['where']
    if w['body_samples'] > 0:
        where = 'Over its life it stood at a median depth of %s' % fmt_m(w['median_depth_m'])
        if w['median_radius_m'] is not None:
            where += ', %s from the tank\'s axis' % fmt_m(w['median_radius_m'])
        if w['median_height_above_floor_m'] is not None:
            where += ' and %s above the floor' % fmt_m(w['median_height_above_floor_m'])
        where += '. The medians are over %s body-samples.' % fmt_n(w['body_samples'])
        if w['median_height_above_floor_m'] is None:
            where += ' The floor is not rebuilt here, so the depth stands alone.'
        lines.append(where)
        lines.append('')

    s = []
    fb = card['body']['founder']
    if fb is not None:
        s.append('The founder\'s adult body is %d part%s and %s.'
                 % (fb['parts'], '' if fb['parts'] == 1 else 's', fmt_vol(fb['adult_volume_m3'])))
    else:
        s.append('No snapshot holds the founder, so its body is not in the recording.')
    pb = card['body'].get('at_peak')
    if pb:
        s.append('At %s, %s had a median of %s and %s.'
                 % (fmt_s(pb['snapshot_at']), plural(pb['sampled'], 'sampled member'),
                    plural(pb['median_parts'], 'part'), fmt_vol(pb['median_adult_volume_m3'])))
    if card['firsts']:
        s.append('It holds %s.' % join_words(FIRST_WORDS[f] for f in card['firsts']))
    ec = card.get('economics')
    if ec is not None:
        s.append(economics_sentence(ec))
    lines.append(' '.join(s))
    lines.append('')
    lines.append(rank_line)
    lines.append('')
    return lines


# ------------------------------------------------------------------------------------ main

def main():
    ap = argparse.ArgumentParser(description='The safari guide: a run\'s clades as cards.')
    ap.add_argument('arm')
    ap.add_argument('--runs-root', default=os.path.join(os.path.dirname(os.path.dirname(
        os.path.abspath(__file__))), 'runs'))
    ap.add_argument('--run', help='the run directory under the arm (default: the newest)')
    ap.add_argument('--top', type=int, default=10)
    ap.add_argument('--out', help='where to write guide.json and guide.md (default: <run>/guide)')
    ap.add_argument('--no-economics', action='store_true',
                    help='skip the ledger pass (economics stays null on every card)')
    ap.add_argument('--check', action='store_true',
                    help='print the relation to clade-score.ps1\'s clades and the port\'s agreement')
    args = ap.parse_args()

    started = time.time()
    run_dir = resolve_run(args.runs_root, args.arm, args.run)
    lineage_path = os.path.join(run_dir, 'lineage.jsonl')
    positions_path = os.path.join(run_dir, 'positions.jsonl')
    for p in (lineage_path, positions_path, os.path.join(run_dir, 'config.json')):
        if not os.path.isfile(p):
            fail('%s: no %s' % (args.arm, p))

    with open(os.path.join(run_dir, 'config.json'), 'r', encoding='utf-8') as f:
        config = json.load(f)
    manifest = {}
    if os.path.isfile(os.path.join(run_dir, 'run.json')):
        with open(os.path.join(run_dir, 'run.json'), 'r', encoding='utf-8') as f:
            manifest = json.load(f)
    status = manifest.get('status', 'no manifest')

    limits = {k: float(find_field(config, k)) for k in
              ('minPartHalfExtent', 'minPartVolume', 'maxPartVolume')}
    limits['maxParts'] = int(find_field(config, 'maxParts'))
    limits['maxDepth'] = int(find_field(config, 'maxDepth'))
    reach = find_field(config, 'maxBodyReachMetres') or 0

    shape = str(find_field(config, 'worldShape') or 'Box')
    radius = None
    if shape.lower() == 'tank':
        radius = math.sqrt(float(find_field(config, 'worldAreaSquareMetres')) / math.pi)

    bed = None
    layout_path = os.path.join(run_dir, 'fields', 'layout.json')
    bed_path = os.path.join(run_dir, 'fields', 'bed.f32')
    if os.path.isfile(layout_path) and os.path.isfile(bed_path):
        with open(layout_path, 'r', encoding='utf-8') as f:
            layout = json.load(f)
        b = layout.get('bed')
        if b:
            values = read_floats(bed_path)
            if len(values) == b['cellsX'] * b['cellsZ']:
                bed = (values, b['cellsX'], b['cellsZ'], float(b['cellMetres']))

    # 1. The clades.
    births, deaths, other_rows = read_lineage(lineage_path)
    if not births:
        fail('%s: no birth rows in %s' % (args.arm, lineage_path))
    clade_of, clades = build_clades(births)
    t_lineage = time.time()

    # 3. Where they lived, and their peaks, from the positions.
    pos = read_positions(positions_path, clade_of, len(clades), radius, bed)
    t_positions = time.time()
    samples = pos['samples']
    run_end = samples[-1][0] if samples else max(b['t'] for b in births.values())
    simulated = manifest.get('simulatedSeconds')
    if simulated:
        run_end = max(run_end, float(simulated))

    # Life and the end.
    for cl in clades:
        alive = [m for m in cl['members'] if m not in deaths]
        cl['alive_at_end'] = len(alive)
        last_death = max((deaths[m] for m in cl['members'] if m in deaths), default=None)
        cl['extinct_at'] = None if alive else last_death
        end = run_end if alive else last_death
        cl['life_seconds'] = max(0.0, end - cl['founded_at'])

    # 2 and 3. The genomes: every founder and every split's parent, from the first snapshot that
    # holds each, and a sample of members at each picker clade's peak.
    snaps = snapshot_seconds(run_dir)
    requests = {}
    genome_at = {}

    def ask(body):
        b = births[body]
        s = first_snapshot_holding(snaps, b['t'], deaths.get(body))
        genome_at[body] = s
        if s is not None:
            requests.setdefault(s, set()).add(body)

    for cl in clades:
        ask(cl['founder'])
        if cl['kind'] == 'split':
            ask(cl['parent_body'])

    picker_set = set(cl['index'] for cl in clades if len(cl['members']) >= PICKER_MIN_MEMBERS)
    peak_request = {}
    for c in picker_set:
        pt = pos['peak_t'][c]
        if pt is None or not snaps:
            continue
        s = min(snaps, key=lambda k: (abs(k - pt), k))
        living = [m for m in clades[c]['members']
                  if births[m]['t'] <= s and (m not in deaths or deaths[m] > s)]
        if not living:
            continue
        step = max(1, len(living) // PEAK_BODY_SAMPLE)
        chosen = living[::step][:PEAK_BODY_SAMPLE]
        peak_request[c] = (s, chosen)
        requests.setdefault(s, set()).update(chosen)

    raw_rows = {}
    rows = read_snapshot_rows(run_dir, requests, raw_rows)
    t_snapshots = time.time()

    def genome_of(body):
        s = genome_at.get(body)
        return None if s is None else rows.get((s, body))

    # The port's check: the flags developed from each founder's genome against its lineage row.
    port_checked = port_agree = 0
    port_disagree = []
    bodies = {}
    for cl in clades:
        for body in (cl['founder'], cl['parent_body'] if cl['kind'] == 'split' else None):
            if body is None or body in bodies:
                continue
            g = genome_of(body)
            if g is None:
                bodies[body] = None
                continue
            bodies[body] = body_of(g, limits)
            b = births[body]
            port_checked += 1
            if bodies[body]['flags'] == (b.get('abs', 0), b.get('jnt', 0), b.get('pho', 0)):
                port_agree += 1
            elif len(port_disagree) < 20:
                port_disagree.append(body)

    # The split's difference.
    for cl in clades:
        f = births[cl['founder']]
        cl['founder_source'] = f.get('src')
        if cl['kind'] != 'split':
            cl['split'] = None
            continue
        pb = births[cl['parent_body']]
        pflags = (pb.get('abs', 0), pb.get('jnt', 0), pb.get('pho', 0))
        gained = [FLAG_WORDS[k] for k in range(3) if cl['flags'][k] and not pflags[k]]
        lost = [FLAG_WORDS[k] for k in range(3) if pflags[k] and not cl['flags'][k]]
        lineage_row = {}
        for key, name in (('as', 'adult_scale'), ('ind', 'indeterminate_nodes'), ('atk', 'attack'),
                          ('ink', 'intake'), ('prt', 'protection'), ('rm', 'reserve_margin')):
            if key in f and key in pb:
                lineage_row[name] = [pb[key], f[key]]
        split = {'parent_flags': list(pflags), 'flags_gained': gained, 'flags_lost': lost,
                 'lineage_row': lineage_row, 'genome': None, 'genome_note': None}
        fb, pbody = bodies.get(cl['founder']), bodies.get(cl['parent_body'])
        if fb is not None and pbody is not None:
            cells = sorted(set(fb['part_cells']) | set(pbody['part_cells']))
            ncells = sorted(set(fb['node_cells']) | set(pbody['node_cells']))
            split['genome'] = {
                'parent_snapshot_at': genome_at[cl['parent_body']],
                'founder_snapshot_at': genome_at[cl['founder']],
                'nodes': [pbody['nodes'], fb['nodes']],
                'parts': [pbody['parts'], fb['parts']],
                'adult_volume_m3': [pbody['adult_volume_m3'], fb['adult_volume_m3']],
                'jointed_parts': [pbody['jointed_parts'], fb['jointed_parts']],
                'adult_scale': [pbody['adult_scale'], fb['adult_scale']],
                'part_cells': {c: [pbody['part_cells'].get(c, 0), fb['part_cells'].get(c, 0)]
                               for c in cells},
                'node_cells': {c: [pbody['node_cells'].get(c, 0), fb['node_cells'].get(c, 0)]
                               for c in ncells},
            }
        else:
            missing = []
            if fb is None:
                missing.append('no snapshot holds the founder')
            if pbody is None:
                missing.append('no snapshot holds the parent')
            split['genome_note'] = ' and '.join(missing)
        cl['split'] = split

    # The firsts.
    firsts = {}
    for i in sorted(births):
        b = births[i]
        p = b['p']
        if b.get('k') != 'r' or p not in births:
            continue
        pb = births[p]
        for key, flag in (('first_jointed_breeder', 'jnt'), ('first_producer', 'pho'),
                          ('first_eater_breeder', 'abs')):
            if key not in firsts and pb.get(flag, 0) == 1:
                firsts[key] = {'clade': clade_of[p], 'body': p, 'at': b['t']}
        if len(firsts) == 3:
            break
    medians = {}
    for cl in clades:
        c = cl['index']
        dh = pos['depth_h'][c]
        rh = pos['radius_h'][c]
        hh = pos['height_h'][c]
        medians[c] = (
            median_of_hist(dh, 10.0) if dh else None,
            median_of_hist(rh, 10.0) if rh and radius is not None else None,
            median_of_hist(hh, 10.0) if hh and bed is not None else None,
            sum(dh.values()) if dh else 0,
        )
    eligible = [c for c in picker_set if medians[c][0] is not None]
    if eligible:
        deep = max(eligible, key=lambda c: (medians[c][0], -c))
        shallow = min(eligible, key=lambda c: (medians[c][0], c))
        firsts['deepest'] = {'clade': deep, 'median_depth_m': rnd(medians[deep][0], 1)}
        firsts['shallowest'] = {'clade': shallow, 'median_depth_m': rnd(medians[shallow][0], 1)}
    longest = max(clades, key=lambda cl: (cl['life_seconds'], -cl['index']))
    firsts['longest_lived'] = {'clade': longest['index'], 'life_seconds': rnd(longest['life_seconds'], 1)}
    held = {}
    for key, v in firsts.items():
        held.setdefault(v['clade'], []).append(key)

    # 4. The score.
    max_members = max(len(cl['members']) for cl in clades)
    triples_seen = {}
    triples_succeeded = set()
    for cl in clades:
        c = cl['index']
        t3 = cl['flags']
        n = len(cl['members'])
        if t3 not in triples_succeeded and n >= PICKER_MIN_MEMBERS:
            novelty = 1.0
        elif t3 not in triples_seen:
            novelty = 0.5
        else:
            novelty = 0.0
        triples_seen[t3] = True
        if n >= PICKER_MIN_MEMBERS:
            triples_succeeded.add(t3)
        persistence = min(1.0, cl['life_seconds'] / run_end) if run_end > 0 else 0.0
        if max_members > 1:
            rarity = persistence * (1.0 - math.log10(n) / math.log10(max_members))
        else:
            rarity = persistence
        readings = {
            'novelty': novelty,
            'success': pos['best_share'][c],
            'persistence': persistence,
            'rarity': max(0.0, rarity),
            'firsts': min(1.0, 0.5 * len(held.get(c, []))),
        }
        cl['readings'] = readings
        cl['score'] = sum(WEIGHTS[k] * readings[k] for k in WEIGHTS)

    order = sorted(clades, key=lambda cl: (-round(cl['score'], 12), cl['founded_at'], cl['index']))
    for r, cl in enumerate(order, 1):
        cl['rank'] = r
    # The trip films clades a viewer can find: those in the picker and seen in the positions.
    # When fewer than --top qualify, it is filled from the rest of the seen clades in score order.
    seen = [cl['index'] for cl in order if pos['peak'][cl['index']] > 0]
    trip = [c for c in seen if c in picker_set][:args.top]
    trip += [c for c in seen if c not in trip][:max(0, args.top - len(trip))]
    picker = [cl['index'] for cl in order if cl['index'] in picker_set]

    # 2. The names, in founding order, so a collision's numeral is fixed by the recording.
    used = {}
    names = {}
    for cl in clades:
        g = genome_of(cl['founder'])
        if g is not None:
            digest = hashlib.sha256(canonical_genome_bytes(g)).digest()
            source = 'genome in the snapshot at %d s' % genome_at[cl['founder']]
        else:
            digest = hashlib.sha256(lineage_row_bytes(cl['founder'], births[cl['founder']])).digest()
            source = 'lineage row (no snapshot holds the founder)'
        genus, epithet = binomial(cl['flags'], digest)
        base = '%s %s' % (genus, epithet)
        k = used.get(base, 0)
        used[base] = k + 1
        name = base + (ROMAN[k] if k < len(ROMAN) else ' %d' % (k + 1))
        cl['name'] = name
        cl['genus'] = genus
        cl['epithet'] = epithet
        cl['name_source'] = source
        cl['name_hash'] = digest.hex()[:16]
        names[cl['index']] = name

    # The cards.
    cards = []
    for cl in clades:
        c = cl['index']
        med = medians[c]
        body = {'founder': None, 'at_peak': None}
        fb = bodies.get(cl['founder'])
        if fb is not None:
            body['founder'] = {
                'parts': fb['parts'], 'adult_volume_m3': rnd(fb['adult_volume_m3'], 6),
                'nodes': fb['nodes'], 'adult_scale': rnd(fb['adult_scale'], 4),
                'part_cells': fb['part_cells'], 'jointed_parts': fb['jointed_parts'],
                'snapshot_at': genome_at[cl['founder']],
            }
        if c in peak_request:
            s, chosen = peak_request[c]
            sampled = [body_of(rows[(s, m)], limits) for m in chosen if (s, m) in rows]
            if sampled:
                ps = sorted(x['parts'] for x in sampled)
                vs = sorted(x['adult_volume_m3'] for x in sampled)
                mid = len(sampled) // 2
                mp = ps[mid] if len(ps) % 2 else (ps[mid - 1] + ps[mid]) / 2.0
                mv = vs[mid] if len(vs) % 2 else (vs[mid - 1] + vs[mid]) / 2.0
                body['at_peak'] = {'snapshot_at': s, 'sampled': len(sampled),
                                   'median_parts': mp, 'median_adult_volume_m3': rnd(mv, 6)}
        split = cl['split']
        if split is not None and split['genome'] is not None:
            g = split['genome']
            g['adult_volume_m3'] = [rnd(v, 6) for v in g['adult_volume_m3']]
            g['adult_scale'] = [rnd(v, 4) for v in g['adult_scale']]
        card = {
            'clade': c,
            'name': cl['name'],
            'genus': cl['genus'],
            'epithet': cl['epithet'],
            'name_source': cl['name_source'],
            'name_hash': cl['name_hash'],
            'guild': GUILDS[cl['flags']],
            'flags': {'abs': cl['flags'][0], 'jnt': cl['flags'][1], 'pho': cl['flags'][2]},
            'flags_tuple': list(cl['flags']),
            'kind': cl['kind'],
            'founder': cl['founder'],
            'founded_at': cl['founded_at'],
            'founder_source': cl['founder_source'],
            'parent_body': cl['parent_body'],
            'parent_clade': cl['parent_clade'],
            'parent_clade_name': names.get(cl['parent_clade']) if cl['parent_clade'] is not None else None,
            'split': split,
            'members_ever': len(cl['members']),
            'peak': {'count': pos['peak'][c], 'at': pos['peak_t'][c], 'living': pos['peak_living'][c],
                     'share_of_living': rnd(pos['peak'][c] / pos['peak_living'][c], 4)
                     if pos['peak_living'][c] else 0.0},
            'generations': {'founder': cl['founder_generation'], 'max': cl['max_generation'],
                            'depth': cl['max_generation'] - cl['founder_generation']},
            'alive_at_end': cl['alive_at_end'],
            'extinct_at': cl['extinct_at'],
            'life_seconds': rnd(cl['life_seconds'], 1),
            'seen_in_positions': {'samples': pos['seen_samples'][c], 'first': pos['first_seen'][c],
                                  'last': pos['last_seen'][c]},
            'where': {'body_samples': med[3], 'median_depth_m': rnd(med[0], 1),
                      'median_radius_m': rnd(med[1], 1),
                      'median_height_above_floor_m': rnd(med[2], 1),
                      'floor': 'fields/bed.f32' if bed is not None else 'not rebuilt'},
            'body': body,
            'firsts': sorted(held.get(c, [])),
            'economics': None,
            'economics_note': None,
            'readings': {k: rnd(v, 4) for k, v in cl['readings'].items()},
            'score': rnd(cl['score'], 4),
            'rank': cl['rank'],
            'best_second': pos['peak_t'][c] if pos['peak_t'][c] is not None else cl['founded_at'],
            'in_picker': c in picker_set,
            'in_trip': c in trip,
        }
        cards.append(card)

    # The economics pass: the ledger on each trip and picker founder's genome, one call at a
    # time (each is a .NET process). A founder no snapshot holds keeps economics null.
    out_dir = args.out or os.path.join(run_dir, 'guide')
    ledger_calls = ledger_failures = 0
    t_ledger0 = time.time()
    if not args.no_economics:
        genome_dir = os.path.join(out_dir, 'genomes')
        config_path = os.path.join(run_dir, 'config.json')
        wanted = set(trip) | picker_set
        for card in cards:
            if card['clade'] not in wanted:
                continue
            founder = card['founder']
            s = genome_at.get(founder)
            depth = card['where']['median_depth_m']
            if s is None or (s, founder) not in raw_rows:
                card['economics_note'] = 'no snapshot holds the founder'
                continue
            if depth is None:
                card['economics_note'] = 'no positions sample shows the clade, so no depth'
                continue
            os.makedirs(genome_dir, exist_ok=True)
            genome_path = os.path.join(genome_dir, '%d.json' % founder)
            with open(genome_path, 'w', encoding='utf-8', newline='\n') as f:
                f.write(raw_rows[(s, founder)] + '\n')
            economics, error = run_ledger(os.path.abspath(genome_path), config_path,
                                          int(round(depth)))
            ledger_calls += 1
            if economics is None:
                ledger_failures += 1
                card['economics_note'] = error
            else:
                economics['genome_snapshot_at'] = s
                card['economics'] = economics
    t_ledger = time.time() - t_ledger0

    checks = {
        'ledger_calls': ledger_calls,
        'ledger_failures': ledger_failures,
        'port_flags_checked': port_checked,
        'port_flags_agree': port_agree,
        'port_flags_disagree_first': port_disagree,
        'reach_bound': reach,
        'reach_note': None if not reach else 'the reach bound is on and the port does not apply '
                                             'it, so part counts and volumes are upper bounds',
        'positions_ids_without_birth_row': pos['unknown_ids'],
        'lineage_rows_other_than_birth_or_death': other_rows,
        'alive_at_end_lineage': sum(cl['alive_at_end'] for cl in clades),
        'alive_at_last_sample_positions': samples[-1][1] if samples else None,
    }

    out_dir = args.out or os.path.join(run_dir, 'guide')
    os.makedirs(out_dir, exist_ok=True)
    guide = {
        'arm': args.arm,
        'run': os.path.basename(run_dir),
        'status': status,
        'run_end_seconds': run_end,
        'samples': len(samples),
        'clades': len(clades),
        'births': len(births),
        'weights': WEIGHTS,
        'readings': READINGS,
        'min_living_for_share': MIN_LIVING_FOR_SHARE,
        'picker_min_members': PICKER_MIN_MEMBERS,
        'trip': trip,
        'picker': picker,
        'names': {str(c): names[c] for c in sorted(names)},
        'ranking': [cl['index'] for cl in order],
        'best_second': {str(card['clade']): card['best_second'] for card in cards},
        'firsts': firsts,
        'checks': checks,
        'cards': cards,
    }
    with open(os.path.join(out_dir, 'guide.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(guide, f, indent=1, ensure_ascii=False)
        f.write('\n')

    write_markdown(os.path.join(out_dir, 'guide.md'), guide, cards, names, trip, picker, status)
    t_done = time.time()

    print('%s: %s, %s births, %d clades, %d in the picker, trip %s'
          % (args.arm, os.path.basename(run_dir), fmt_n(len(births)), len(clades), len(picker),
             ', '.join(names[c] for c in trip)))
    print('%s: port flags agree %d of %d; alive at end %d (lineage) against %s (last positions sample)'
          % (args.arm, port_agree, port_checked, checks['alive_at_end_lineage'],
             checks['alive_at_last_sample_positions']))
    print('%s: wrote %s; lineage %.1f s, positions %.1f s, snapshots %.1f s, total %.1f s'
          % (args.arm, out_dir, t_lineage - started, t_positions - t_lineage,
             t_snapshots - t_positions, t_done - started))
    if ledger_calls:
        print('%s: economics: %d ledger calls, %d failed, %.1f s (%.1f s a call)'
              % (args.arm, ledger_calls, ledger_failures, t_ledger, t_ledger / ledger_calls))

    if args.check:
        check_scorer(args.arm, births, deaths, clades, clade_of, samples)


def check_scorer(arm, births, deaths, clades, clade_of, samples):
    """The guide's clades against clade-score.ps1's walk over one flag."""
    last = samples[-1][0] if samples else None
    for trait, k in (('abs', 0), ('pho', 2)):
        sc = scorer_clades(births, trait)
        # Every guide clade carrying the flag lies inside one scorer clade.
        root_of = {}
        for rt, members in sc.items():
            for m in members:
                root_of[m] = rt
        inside = {}
        broken = 0
        for cl in clades:
            if not cl['flags'][k]:
                continue
            roots = set(root_of[m] for m in cl['members'])
            if len(roots) != 1:
                broken += 1
                continue
            inside.setdefault(roots.pop(), []).append(cl['index'])
        sums_ok = all(sum(len(clades[c]['members']) for c in inside.get(rt, [])) == len(members)
                      for rt, members in sc.items())

        def alive_at(members, t):
            return sum(1 for m in members if births[m]['t'] <= t and (m not in deaths or deaths[m] > t))

        largest = None
        for rt, members in sc.items():
            a = alive_at(members, last)
            if largest is None or a > largest[2]:
                largest = (rt, len(members), a)
        rt, n, a = largest
        subs = sorted(inside.get(rt, []), key=lambda c: -len(clades[c]['members']))
        print('%s: %s: scorer clades %d, guide clades with the flag %d; guide clades spanning two '
              'scorer clades %d; every scorer clade is the sum of its guide clades: %s'
              % (arm, trait, len(sc), sum(1 for cl in clades if cl['flags'][k]), broken, sums_ok))
        print('%s: %s: largest scorer clade root %d, %d members ever, %d alive at %s; it holds %d '
              'guide clades, the largest %s'
              % (arm, trait, rt, n, a, last, len(subs),
                 ', '.join('%d (%s, %d members, founder %d)' % (c, clades[c]['name'],
                                                                len(clades[c]['members']),
                                                                clades[c]['founder'])
                           for c in subs[:3])))


def write_markdown(path, guide, cards, names, trip, picker, status):
    by_index = {card['clade']: card for card in cards}
    L = []
    L.append('# The guide to %s' % guide['arm'])
    L.append('')
    intro = ('This guide lists the clades of run %s of arm %s, ranks them and describes the ones '
             'worth a visit. A clade here is a line of descent: a founder and every descendant '
             'that kept its three flags. The flags say whether a body carries absorptive tissue '
             '(a stomach, which eats), a joint (which can swim), and photosynthetic tissue (a '
             'leaf, which lives on light). A child whose flags differ from its parent\'s founds '
             'a new clade.'
             % (guide['run'], guide['arm']))
    L.append(intro)
    L.append('')
    run_line = ('The run reached %s over %s samples. Its %s births fall into %s clades. %s of '
                'them had at least %d members and are in the picker.'
                % (fmt_s(guide['run_end_seconds']), fmt_n(guide['samples']), fmt_n(guide['births']),
                   fmt_n(guide['clades']), fmt_n(len(picker)), guide['picker_min_members']))
    if status not in ('ended', 'stopped'):
        run_line += ' The manifest says %s, so every count here is provisional.' % status
    L.append(run_line)
    L.append('')
    L.append('Every fact below comes from the run\'s files. The names are made from the founder\'s '
             'genome and mean nothing. The economics from the ledger are a later pass.')
    L.append('')
    L.append('## The score ranks clades on five readings')
    L.append('')
    L.append('Each reading runs from 0 to 1, and the score is their weighted sum. The table gives '
             'the weights and what each reading measures.')
    L.append('')
    L.append('| reading | weight | what it measures |')
    L.append('|---|---|---|')
    for k, w in guide['weights'].items():
        L.append('| %s | %.2f | %s |' % (k, w, guide['readings'][k]))
    L.append('')
    L.append('## The default trip visits the top %d' % len(trip))
    L.append('')
    L.append('The trip takes the highest scores among clades the positions ever show. The table '
             'numbers its stops in order and gives each clade\'s rank among all the run\'s clades '
             'beside it, with each one\'s best second, which is its peak sample.')
    L.append('')
    L.append('| stop | rank in the run | name | guild | founded | peak | best second | score |')
    L.append('|---|---|---|---|---|---|---|---|')
    for stop, c in enumerate(trip, 1):
        card = by_index[c]
        L.append('| %d | %d | %s | %s | %s | %s | %s | %.2f |'
                 % (stop, card['rank'], card['name'], card['guild'], fmt_s(card['founded_at']),
                    fmt_n(card['peak']['count']), fmt_s(card['best_second']), card['score']))
    L.append('')
    L.append('## Each card states what the recording knows about one clade')
    L.append('')
    L.append('There is one card for each clade in the trip or the picker, in the order of the '
             'score. A leaf carries photosynthetic tissue, a stomach carries absorptive tissue, and '
             'a mixotroph carries both. A jointed body has at least one joint that can move. The '
             'depth is measured down from the surface, and the axis is the tank\'s centre line.')
    L.append('')
    shown = [c for c in guide['ranking'] if c in set(trip) | set(picker)]
    for c in shown:
        card = by_index[c]
        rd = card['readings']
        rank_line = ('It ranks %d with a score of %.2f. Its readings are novelty %.2f, success %.2f, '
                     'persistence %.2f, rarity %.2f and firsts %.2f. Its best second is %s.'
                     % (card['rank'], card['score'], rd['novelty'], rd['success'],
                        rd['persistence'], rd['rarity'], rd['firsts'], fmt_s(card['best_second'])))
        L += card_prose(card, names, rank_line)
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(L).rstrip() + '\n')


if __name__ == '__main__':
    main()
