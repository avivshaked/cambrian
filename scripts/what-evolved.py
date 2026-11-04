"""What has evolved in a world run that was not there at the start, and is any of it worth filming?

A report for one run at one second. It reads lineage.jsonl (one row per creature ever born), stats.jsonl,
genomes.jsonl.gz (the bodies), snapshots/ (the body fraction), positions.jsonl.gz, config.json and run.json, and the
report beside the run (runs/<arm>.md) for the islands. It only reads: it never writes to a run, it opens every file
shared so it can look at a run that is still being written, and a half-written last line or a gzip cut mid-stream is
skipped, as genome-query.py skips them. It photographs nothing: the ids it names are for the theatre to photograph.

Usage:

    python -I scripts/what-evolved.py <run> [--at SECONDS] [--json] [--traits TSV] [--report FILE]
                                      [--top N] [--rows N] [--from SECONDS] [--window SECONDS]
                                      [--stride SECONDS] [--crossing-gap METRES] [--few N] [--many N]
                                      [--min-plan N] [--min-crash N] [--no-positions]

<run> is a run directory, or a round folder such as runs/r65-c-6202/ whose single stamped subfolder is the run, or a
bare name such as r65-c-6202 looked for under runs/. --at is the second to read (default the last sample); the read
counts everything born and dead by then. --json prints the same facts as one JSON object, for scripts. --traits
takes the TSV that the farm's `--grazer-traits <run dir> <out tsv>` writes (src/Evosim.Farm/GrazerTraits.cs): it
holds body mass, developed part count, limb push and the brain's drive, which only the world's own Developer and
Brain can compute, so without it section 3 says they are not available. The tool also looks for
grazer-traits.tsv in the run directory and in the arm folder above it.

The six sections:

  1. Who is alive. The classes, and their counts over time, from lineage.jsonl (a body is alive between its birth row
     and its death row). An animal is a body born with a mouth: atk 1 and ink 1 in its birth row, which is the
     definition G2 (logbook/specs/grazer-film-plan.md) and scripts/reads/r61-grazer-economy.py call a grazer. A
     hunter is an animal the record shows cutting a part off a body that is not a producer (a lineage kill row, "k");
     until then it is a grazer. A producer is a body without a mouth that carries photosynthetic tissue (pho 1).
     Then the busts (g2-read.py's rule: a fall to half or less of the highest count in the 1,500 s before it) and the
     returns to that count.
  2. Lineages. A founder is a body born without a parent in the record (the floor's, the trickle's, the pool's, the
     inoculation's). A lineage is a founder and all its descendants (scripts/positions-read.py's clades). How many
     are alive, the ones holding the most animals and the most bodies, and those that rose from few to many.
  3. Trait drift against the founders. For each trait read off the genome: the founder animals' mean and standard
     deviation, the living animals' mean now, and the shift in founder standard deviations, flagged at 1 or more
     (G2's measure). Where the founders are clones the spread is zero, so, as G2 does, the scale is the larger of the
     founders' spread and the spread of the animals born in the first window (--from, --window); the line says
     which was used. The genome's traits stand in for the developed body: the body itself is not recorded.
  4. New body plans. genome-query.py's plan (a structural signature) of every living body against every founder's.
     The plans that no founder had, counted, with the largest of them described in plain words, and the deepest
     generation alive.
  5. Behaviour. Bites that cut a part off a body, by who and what (lineage kill rows), the joules kept from leaves,
     other living bodies and remains (stats.jsonl), deaths by cause, and movement between places: crossings and
     strolls between the islands' groves (g2-read.py's rule, from the islands the report beside the run lists), and
     how far animals got from where they were first seen.
  6. Standouts. Up to five living creature ids most worth filming, each with a line on why: a new plan in many
     bodies, a lineage that took over, the most drifted, the deepest descendant, then a crosser or a hunter.

What it cannot measure is listed at the end of the report, with the reason.

Exits 0, or 2 on a usage error or a run it could not find.
"""
import sys

# The imports below must leave no .pyc beside the scripts they load: this tool writes nothing but its own stdout.
sys.dont_write_bytecode = True

import argparse
import importlib.util
import json
import math
import os
import statistics
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)


def load_script(name, path):
    """A script with a hyphen in its name, imported by path (the way scripts/reads/island-guard-read.py does)."""
    if not os.path.exists(path):
        print('%s is missing, and this tool reuses it' % path, file=sys.stderr)
        sys.exit(2)
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


gq = load_script('genome_query', os.path.join(HERE, 'genome-query.py'))      # genomes, plans, plain words
pr = load_script('positions_read', os.path.join(HERE, 'positions-read.py'))  # positions, clades, the config's fields
g2 = load_script('g2_read', os.path.join(HERE, 'reads', 'bloom', 'g2-read.py'))  # busts, groves, shifts
pr.runrec.VERBOSE = False

# ---------------------------------------------------------------- constants

CLASSES = ('producer', 'grazer', 'hunter', 'absorber', 'jointed', 'other')
ANIMAL_CLASSES = ('grazer', 'hunter')
REMAINS_FLAG = g2.REMAINS_FLAG      # a positions row's bit 8: a corpse, no living body
SHIFT_GATE = g2.SHIFT_GATE          # one founder standard deviation (G2 gate 3)
MIN_BIRTHS = g2.MIN_BIRTHS          # a window with fewer animal births than this is not used for the scale
BUST_BEFORE = g2.BUST_BEFORE
BODY_FRACTION_GROWN = 0.9           # a body at or over this share of its adult size is "grown" for a picture
STEPS = (100, 200, 250, 500, 1000, 1500, 2000, 2500, 3000, 4000, 5000, 6000, 8000, 10000, 12000, 15000, 20000,
         25000, 30000, 40000, 50000, 100000)
G2_TRAITS = (('mass_kg', 'body mass (kg)'), ('parts', 'developed part count'), ('limb_push', 'limb push (sum of power)'),
             ('drive_rest', 'brain mean drive, at rest'), ('drive_fed', 'brain mean drive, fed'))


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


# ---------------------------------------------------------------- small numbers

def mean(xs):
    return sum(xs) / len(xs) if xs else None


def sd(xs):
    """The sample standard deviation, 0 for fewer than two values (g2-read.py's sample_sd)."""
    return g2.sample_sd(xs) if len(xs) >= 2 else 0.0


def median(xs):
    return statistics.median(xs) if xs else None


def share(a, b):
    return a / b if b else None


def num(x, spec='{:.3f}'):
    if x is None or (isinstance(x, float) and math.isnan(x)):
        return '-'
    if isinstance(x, float) and math.isinf(x):
        return 'inf'
    return spec.format(x)


def pct(x):
    return '-' if x is None else '%.1f%%' % (100 * x)


def clean(x):
    """A value JSON can hold: no NaN or infinity (None instead), sets as sorted lists, tuples as lists."""
    if isinstance(x, dict):
        return {str(k): clean(v) for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [clean(v) for v in x]
    if isinstance(x, set):
        return [clean(v) for v in sorted(x)]
    if isinstance(x, float) and (math.isnan(x) or math.isinf(x)):
        return None
    return x


# ---------------------------------------------------------------- finding and reading the run

def find_run(arg):
    """A run directory (holding lineage.jsonl), or a round folder with one stamped subfolder, or a name under runs/."""
    for path in (arg, os.path.join(REPO, 'runs', arg)):
        if not os.path.isdir(path):
            continue
        if os.path.exists(os.path.join(path, 'lineage.jsonl')):
            return os.path.normpath(path)
        subs = sorted(d for d in os.listdir(path) if os.path.exists(os.path.join(path, d, 'lineage.jsonl')))
        if len(subs) == 1:
            return os.path.normpath(os.path.join(path, subs[0]))
        if len(subs) > 1:
            fail('%s holds %d runs (%s); name one of them' % (path, len(subs), ', '.join(subs)))
    fail('no run with lineage.jsonl at %s (or under runs/)' % arg)


def read_json(path):
    try:
        with open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


BIRTH_KEEP = ('t', 'p', 'g', 'k', 'src', 'abs', 'jnt', 'pho', 'atk', 'ink', 'prt', 'fat', 's')


def guild_of(b):
    """The class a birth row puts a body in, before any hunting: animal (a mouth: atk 1 and ink 1, G2's grazer),
    producer (photosynthetic tissue), absorber, jointed (a joint and none of those), other."""
    if b.get('atk') == 1 and b.get('ink') == 1:
        return 'animal'
    if b.get('pho') == 1:
        return 'producer'
    if b.get('abs') == 1:
        return 'absorber'
    if b.get('jnt') == 1:
        return 'jointed'
    return 'other'


def read_lineage(run, t_max):
    """(births, death_t, death_cause, kills): births {id: slim row}, the death second and cause of each, and the kill
    rows (t, victim, attacker, root lost, parts) as tuples. Rows after t_max are left out."""
    births, death_t, death_cause, kills = {}, {}, {}, []
    for line in gq.read_lines(os.path.join(run, 'lineage.jsonl'), gq.open_text):
        r = gq.parse(line)
        if r is None:
            continue
        t = r.get('t', 0)
        if t > t_max:
            continue
        e = r.get('e')
        if e == 'b':
            b = {k: r[k] for k in BIRTH_KEEP if k in r}
            for k in ('as', 'ls', 'rm', 'grs'):
                if k in r:
                    b[k] = r[k]
            births[r['id']] = b
        elif e == 'd':
            death_t[r['id']] = t
            death_cause[r['id']] = r.get('c')
        elif e == 'k':
            kills.append((t, r['id'], r.get('by', -1), r.get('root', 0), r.get('parts', 0)))
    return births, death_t, death_cause, kills


KEEP_STATS = ('t', 'alive', 'biteKeptLeaves', 'biteKeptOthers', 'biteKeptRemains', 'mouthBlows',
              'mouthBlowsFinishing', 'killsSwallowed', 'partsKilled', 'bodiesEaten', 'corpsesEaten')


def read_stats(run, t_max):
    rows = []
    for line in gq.read_lines(os.path.join(run, 'stats.jsonl'), gq.open_text):
        r = gq.parse(line)
        if r is None or 't' not in r or r['t'] > t_max:
            continue
        rows.append({k: r[k] for k in KEEP_STATS if k in r})
    return rows


def read_snapshot_fractions(run, at):
    """(second, {id: body fraction}) of the snapshot nearest `at`, or (None, {}) when the run holds none that gq reads."""
    times = gq.snapshot_times(run)
    if not times:
        return None, {}
    t = min(times, key=lambda x: (abs(x - at), x))
    out = {}
    for line in gq.read_lines(os.path.join(run, 'snapshots', '%09d.jsonl.gz' % t), gq.open_gz):
        r = gq.parse(line)
        if r is not None and 'id' in r:
            out[r['id']] = r.get('bf')
    return t, out


def read_island_line(run, explicit):
    """(islands, path): the islands of the report's `island bloom, N islands` line, from --report, else runs/<arm>.md
    beside the run's arm folder, else report.md in the run. (None, None) when no report holds the line."""
    arm_dir = os.path.dirname(run)
    cands = [explicit] if explicit else []
    cands += [os.path.normpath(arm_dir) + '.md', os.path.join(run, 'report.md')]
    for c in cands:
        if c and os.path.isfile(c):
            try:
                with open(c, encoding='utf-8', errors='replace') as f:
                    text = f.read()
                islands = g2.parse_islands(text)
            except (OSError, ValueError):
                continue
            if islands:
                return islands, c
    return None, None


def find_traits_tsv(run, explicit):
    cands = [explicit] if explicit else [os.path.join(run, 'grazer-traits.tsv'),
                                         os.path.join(os.path.dirname(run), 'grazer-traits.tsv')]
    for c in cands:
        if c and os.path.isfile(c):
            return c
    return None


def read_traits_tsv(path):
    """{id: {column: float}} from a farm --grazer-traits table: '#' lines skipped, columns by name."""
    rows, header = {}, None
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\n')
            if not line or line.startswith('#'):
                continue
            cells = line.split('\t')
            if header is None:
                header = cells
                continue
            r = dict(zip(header, cells))
            try:
                gid = int(r['id'])
            except (KeyError, ValueError):
                continue
            vals = {}
            for k, v in r.items():
                if k in ('id', 'src'):
                    continue
                try:
                    vals[k] = float(v)
                except ValueError:
                    pass
            rows[gid] = vals
    return rows


# ---------------------------------------------------------------- the genome's traits

def grown_nodes(g, live):
    return [g['nodes'][i] for i in sorted(live)]


def trait_values(g):
    """{trait: value} off a genome row, over the nodes development reaches. A trait the row cannot give is left out."""
    live = gq.reachable(g)
    nodes = grown_nodes(g, live)
    rep = g.get('reproduction') or {}
    brain = g.get('brain') or {}
    scale = g.get('adultScale')
    t = {}
    if scale is not None:
        t['adult_scale'] = scale
        # Each grown node's box once (8 x y z, half-extents in metres), edge scales and repeats ignored, times the
        # adult scale cubed. A stand-in for the developed body's mass, which only Developer can build.
        litres = sum(8000.0 * n['dimensions']['x'] * n['dimensions']['y'] * n['dimensions']['z'] for n in nodes)
        t['volume_l'] = litres * scale ** 3
    if 'lifespan' in g:
        t['lifespan_scale'] = g['lifespan']
    if 'growthReserve' in g:
        t['growth_reserve_s'] = g['growthReserve']
    for key, name in (('brood', 'brood'), ('investment', 'birth_investment'), ('margin', 'reserve_margin_s'),
                      ('gestation', 'gestation_share')):
        if key in rep:
            t[name] = rep[key]
    t['grown_nodes'] = len(live)
    jointed = [n for n in nodes if n['joint'] != 'Fixed']
    t['jointed_nodes'] = len(jointed)
    t['limb_power'] = sum(n['power'] for n in jointed)
    t['mirrored_edges'] = sum(1 for n in nodes for e in n['edges'] if gq.is_mirrored(e))
    t['attack_sum'] = sum(n.get('attack', 0) for n in nodes)
    t['intake_sum'] = sum(n.get('intake', 0) for n in nodes)
    t['protection_max'] = max((n.get('protection', 0) for n in nodes), default=0)
    t['lift_sum'] = sum(n.get('lift', 0) for n in nodes)
    if brain:
        t['brain_hidden'] = len(brain.get('hidden', []))
        t['brain_links'] = len(brain.get('links', []))
        t['brain_outputs'] = len(brain.get('outputs', []))
        ws = [abs(l['w']) for l in brain.get('links', [])]
        if ws:
            t['brain_mean_abs_w'] = sum(ws) / len(ws)
    return t


TRAIT_LABELS = (
    ('adult_scale', 'adult scale (gene; mass goes as its cube)'),
    ('volume_l', 'genome volume, litres (stand-in for body mass)'),
    ('grown_nodes', 'grown nodes (stand-in for part count)'),
    ('jointed_nodes', 'jointed grown nodes'),
    ('limb_power', 'limb power (sum of power over jointed nodes)'),
    ('mirrored_edges', 'mirrored edges'),
    ('attack_sum', 'attack (sum over grown nodes)'),
    ('intake_sum', 'intake (sum over grown nodes)'),
    ('protection_max', 'protection (largest grown node)'),
    ('lift_sum', 'buoyant lift (sum)'),
    ('brain_hidden', 'brain hidden units'),
    ('brain_links', 'brain links'),
    ('brain_outputs', 'brain outputs'),
    ('brain_mean_abs_w', 'brain mean |link weight|'),
    ('lifespan_scale', 'lifespan scale'),
    ('growth_reserve_s', 'growth reserve (s)'),
    ('brood', 'brood size'),
    ('birth_investment', 'birth investment'),
    ('reserve_margin_s', 'reserve margin (s)'),
    ('gestation_share', 'gestation share'),
)


def scan_genomes(run, wanted, describe_ids):
    """One pass over genomes.jsonl.gz. For every wanted id: {'traits', 'sig', 'cells'}, and for ids in describe_ids
    also 'plain' (genome-query's plain-words line). Genomes are not kept: a run holds tens of thousands."""
    out = {}
    if not wanted:
        return out
    top = max(wanted)
    for line in gq.read_lines(os.path.join(run, 'genomes.jsonl.gz'), gq.open_gz):
        gid = gq.head_id(line)
        if gid is None:
            continue
        if gid in wanted:
            g = gq.parse(line)
            if g is None or g.get('id') != gid:
                continue
            try:
                rec = {'traits': trait_values(g), 'sig': gq.signature(g), 'cells': gq.cells_of(g)}
                if gid in describe_ids:
                    rec['plain'] = gq.plain_line(g)
                out[gid] = rec
            except (KeyError, TypeError):
                pass
            if len(out) == len(wanted):
                break
        elif gid > top:
            break
    return out


# ---------------------------------------------------------------- the sweep: who is alive, when

def class_of(b):
    g = guild_of(b)
    return 'grazer' if g == 'animal' else g


def sweep(births, death_t, hunter_t, record_times, root_times, root_of):
    """Walk births, first bites and deaths in time order, recording at each of record_times the count of every class,
    and at root_times the living bodies and animals of every lineage. Returns (series, roots, cls, root_all, root_anim):
    series [(t, counts in CLASSES order)], roots {t: {root: (bodies, animals)}}, cls {id: class} of the bodies alive
    at the last record time, and that moment's per-lineage counts."""
    ev = [(b['t'], 0, i) for i, b in births.items()]
    ev += [(t, 2, i) for i, t in death_t.items() if i in births]
    ev += [(t, 1, i) for i, t in hunter_t.items() if i in births]
    ev.sort()
    cls, count = {}, Counter()
    root_all, root_anim = Counter(), Counter()
    series, roots = [], {}
    root_set = set(root_times)
    j, n = 0, len(ev)
    for T in record_times:
        while j < n and ev[j][0] <= T + 1e-9:
            _, kind, i = ev[j]
            j += 1
            if kind == 0:
                c = class_of(births[i])
                cls[i] = c
                count[c] += 1
                r = root_of.get(i, i)
                root_all[r] += 1
                if c == 'grazer':
                    root_anim[r] += 1
            elif kind == 1:
                if cls.get(i) == 'grazer':
                    cls[i] = 'hunter'
                    count['grazer'] -= 1
                    count['hunter'] += 1
            else:
                c = cls.pop(i, None)
                if c is None:
                    continue
                count[c] -= 1
                r = root_of.get(i, i)
                root_all[r] -= 1
                if c in ANIMAL_CLASSES:
                    root_anim[r] -= 1
        series.append((T, [count[c] for c in CLASSES]))
        if T in root_set:
            roots[T] = {r: (v, root_anim.get(r, 0)) for r, v in root_all.items() if v > 0}
    return series, roots, cls, root_all, root_anim


def grid_times(at, rows, first):
    """About `rows` round-number seconds up to `at`, and the first sample ahead of them."""
    step = next((s for s in STEPS if at / s <= rows), STEPS[-1])
    ts = [float(step * k) for k in range(1, int(at // step) + 1)]
    if not ts or ts[-1] < at - 1e-9:
        ts.append(float(at))
    if first is not None and first < ts[0] - 1e-9:
        ts.insert(0, float(first))
    return ts


def busts_of(series, index):
    times = [t for t, _ in series]
    counts = [c[index] if isinstance(index, int) else sum(c[i] for i in index) for _, c in series]
    peak = max(counts) if counts else 0
    return g2.find_busts(times, counts, before=BUST_BEFORE), peak


# ---------------------------------------------------------------- picking an example to photograph

def pick(ids, births, fractions):
    """The id to photograph out of ids: grown if the snapshot says who is, then the deepest generation, then the
    newest. A body the snapshot does not list is treated as not known to be grown."""
    ids = list(ids)
    if not ids:
        return None
    grown = [i for i in ids if (fractions.get(i) or 0) >= BODY_FRACTION_GROWN]
    pool = grown or ids
    return max(pool, key=lambda i: (births[i].get('g', 0), fractions.get(i) or 0, i))


# ---------------------------------------------------------------- the report

def build(args):
    run = find_run(args.run)
    manifest = read_json(os.path.join(run, 'run.json')) or {}
    config = read_json(os.path.join(run, 'config.json')) or {}
    arm = manifest.get('arm') or os.path.basename(os.path.dirname(run))
    R = {'run': run, 'arm': arm, 'seed': manifest.get('seed'), 'configHash': manifest.get('configHash'),
         'status': manifest.get('status'), 'reason': manifest.get('reason'),
         'simulatedSeconds': manifest.get('simulatedSeconds'), 'notes': [], 'not_available': []}
    if manifest.get('resumedFrom'):
        R['notes'].append('run.json says this run resumed %s; only this run\'s own files are read, so lineages '
                          'whose founders lie in the earlier part are rooted at their first body here'
                          % manifest.get('resumedFrom'))

    # ---- the second to read
    probe = read_stats(run, float('inf'))
    stats_times = [r['t'] for r in probe]
    last_t = stats_times[-1] if stats_times else None
    if last_t is None:
        snaps = gq.snapshot_times(run)
        last_t = float(snaps[-1]) if snaps else None
    if last_t is None:
        fail('%s has neither stats.jsonl rows nor snapshots, so there is no second to read' % run)
    if args.at is None:
        at = float(last_t)
    else:
        at = float(args.at)
        if stats_times:
            at = float(min(stats_times, key=lambda x: (abs(x - at), x)))
        if at > last_t + 1e-9:
            at = float(last_t)
        if abs(at - args.at) > 0.5:
            R['notes'].append('asked for %g s; the nearest sample the run holds is %g s, so it is read at %g s'
                              % (args.at, at, at))
    stats = [r for r in probe if r['t'] <= at + 1e-9]
    R['at'] = at
    R['stats_cadence'] = median([b - a for a, b in zip(stats_times, stats_times[1:])]) if len(stats_times) > 1 else None

    births, death_t, death_cause, kills = read_lineage(run, at)
    if not births:
        fail('%s: lineage.jsonl holds no births by %g s' % (run, at))
    root_of, _ = pr.read_clades(os.path.join(run, 'lineage.jsonl'))
    root_of = root_of or {}
    snap_t, fractions = read_snapshot_fractions(run, at)
    R['snapshot_second'] = snap_t

    class_name = {i: guild_of(b) for i, b in births.items()}
    founders = sorted(i for i, b in births.items() if b['p'] < 0)
    animal_founders = [i for i in founders if class_name[i] == 'animal']

    # ---- the first bite on a body that is not a producer makes a hunter
    hunter_t, bites_on, bites_by = {}, Counter(), Counter()
    for t, victim, attacker, _, _ in sorted(kills):
        vc = class_name.get(victim)
        ac = class_name.get(attacker)
        if ac is None or vc is None:
            continue
        bites_on[(ac, vc)] += 1
        if ac == 'animal' and vc != 'producer':
            hunter_t.setdefault(attacker, t)
            bites_by[attacker] += 1

    # ---- the sweep
    table_ts = grid_times(at, args.rows, stats_times[0] if stats_times else None)
    lin_ts = grid_times(at, 48, None)
    record = sorted({t for t in stats_times if t <= at + 1e-9} | set(table_ts) | set(lin_ts) | {at})
    series, roots_at, cls, root_all, root_anim = sweep(births, death_t, hunter_t, record, lin_ts, root_of)
    alive = set(cls)
    animals_alive = [i for i, c in cls.items() if c in ANIMAL_CLASSES]
    by_class = {c: sum(1 for v in cls.values() if v == c) for c in CLASSES}

    # ---- checks against other records
    checks = {'alive_by_lineage': len(alive)}
    srow = next((r for r in reversed(stats) if abs(r['t'] - at) < 1e-6), None)
    checks['alive_in_stats_row'] = srow.get('alive') if srow else None
    if snap_t is not None:
        checks['snapshot_second'] = snap_t
        checks['alive_in_snapshot'] = len(fractions)
        checks['snapshot_ids_equal_lineage_ids'] = (set(fractions) == alive) if snap_t == at else None
    R['checks'] = checks

    # ---- the header facts
    srcs = Counter(births[i].get('src', '?') for i in founders)
    R['founders'] = {'bodies': len(founders), 'animals': len(animal_founders), 'by_source': dict(srcs),
                     'first_t': births[founders[0]]['t'] if founders else None,
                     'last_t': max(births[i]['t'] for i in founders) if founders else None}
    R['classes'] = {c: by_class[c] for c in CLASSES}
    R['alive'] = len(alive)

    # ---- 1. who is alive
    rows = []
    sidx = {t: c for t, c in series}
    for T in table_ts:
        c = sidx.get(T)
        if c is None:
            continue
        rows.append({'t': T, 'alive': sum(c), **{CLASSES[k]: c[k] for k in range(len(CLASSES))}})
    section1 = {'columns': ['t', 'alive'] + list(CLASSES), 'rows': rows}
    busts = {}
    for name, idx in (('animals', (1, 2)), ('hunters', 2), ('producers', 0)):
        b, peak = busts_of(series, idx)
        big = [x for x in b if x['ref'] >= args.min_crash]
        busts[name] = {'peak': peak, 'busts': big, 'small_falls': len(b) - len(big), 'min_reference': args.min_crash}
    section1['busts'] = busts
    R['alive_over_time'] = section1

    # ---- 2. lineages
    n_roots_alive = sum(1 for v in root_all.values() if v > 0)
    n_roots_anim = sum(1 for v in root_anim.values() if v > 0)
    # the series of every lineage at the lineage grid, for the rise from few to many
    def lineage_row(r, which):
        ser = [roots_at.get(T, {}).get(r, (0, 0))[which] for T in lin_ts]
        now = root_all.get(r, 0) if which == 0 else root_anim.get(r, 0)
        first = next(((T, v) for T, v in zip(lin_ts, ser) if v >= 1), None)
        peak = max(ser + [now])
        b = births.get(r, {})
        return {'root': r, 'born_t': b.get('t'), 'source': b.get('src'), 'root_class': class_name.get(r),
                'bodies': root_all.get(r, 0), 'animals': root_anim.get(r, 0),
                'share_of_animals': share(root_anim.get(r, 0), len(animals_alive)),
                'first_seen_t': first[0] if first else None, 'first_seen_size': first[1] if first else None,
                'peak': peak, 'rose': bool(first and first[1] <= args.few and now >= args.many)}
    top_anim = sorted((r for r, v in root_anim.items() if v > 0), key=lambda r: (-root_anim[r], r))
    top_all = sorted((r for r, v in root_all.items() if v > 0), key=lambda r: (-root_all[r], r))
    rose_anim = [lineage_row(r, 1) for r in top_anim if root_anim[r] >= args.many]
    rose_anim = [x for x in rose_anim if x['rose']]
    R['lineages'] = {
        'founder_lineages_alive': n_roots_alive, 'founder_lineages_with_living_animals': n_roots_anim,
        'founder_bodies': len(founders), 'founder_animals': len(animal_founders),
        'living_bodies': len(alive), 'living_animals': len(animals_alive),
        'top_by_animals': [lineage_row(r, 1) for r in top_anim[:args.top]],
        'top_by_bodies': [lineage_row(r, 0) for r in top_all[:max(3, args.top // 2)]],
        'rose_few_to_many': rose_anim[:args.top], 'few': args.few, 'many': args.many,
        'grid_seconds': lin_ts}

    # ---- genomes: founders, the living, and the animals born in the first window
    w0, w1 = args.t_from, args.t_from + args.window
    first_window = [i for i, b in births.items() if class_name[i] == 'animal' and w0 <= b['t'] < w1]
    use_window = len(first_window) >= MIN_BIRTHS and w1 <= at + 1e-9
    wanted = set(founders) | alive | (set(first_window) if use_window else set())
    describe = set(animals_alive)
    has_genomes = os.path.exists(os.path.join(run, 'genomes.jsonl.gz'))
    G = scan_genomes(run, wanted, describe) if has_genomes else {}
    R['genomes_read'] = {'wanted': len(wanted), 'found': len(G), 'file_present': has_genomes}
    if not has_genomes:
        R['not_available'].append('sections 3, 4 and 6 (traits, plans, picks by plan or drift): no genomes.jsonl.gz in the '
                                  'run (record format 1 holds genomes inside its snapshots, which this tool does not read)')

    # ---- 3. trait drift
    tsv_path = find_traits_tsv(run, args.traits)
    tsv = read_traits_tsv(tsv_path) if tsv_path else {}
    trait_rows = []
    drift_ref = {}

    def series_of(ids, key, source):
        out = []
        for i in ids:
            v = (source.get(i) or {}).get(key) if source is not G else (G.get(i) or {}).get('traits', {}).get(key)
            if v is not None:
                out.append(v)
        return out

    def trait_row(key, label, source, fnd, now, win):
        f, n_, w = series_of(fnd, key, source), series_of(now, key, source), series_of(win, key, source)
        if not f or not n_:
            return None
        f_mean, f_sd = mean(f), sd(f)
        w_sd = sd(w) if use_window and len(w) >= MIN_BIRTHS else None
        scale, which = (g2.founder_scale(f_sd, w_sd) if w_sd is not None else (f_sd, 'founders'))
        n_mean = mean(n_)
        # With no spread to measure against (the founders are clones and so are the first window's animals) the
        # shift is undefined: G2's arithmetic calls any change infinite, which would flag every trait that mutated
        # at all. Here such a trait is not judged; the share of living animals that differ from the founders' value
        # is given instead.
        differ = sum(1 for v in n_ if abs(v - f_mean) > 1e-9 * max(1.0, abs(f_mean)))
        if scale > 0:
            shift = g2.shift_in_scale(f_mean, n_mean, scale)
            signed = shift if n_mean >= f_mean else -shift
        else:
            shift = signed = None
        return {'trait': key, 'label': label, 'founders_n': len(f), 'founders_mean': f_mean, 'founders_sd': f_sd,
                'first_window_sd': w_sd, 'scale': scale, 'scale_from': which if scale > 0 else 'none (no spread)',
                'now_n': len(n_), 'now_mean': n_mean, 'now_sd': sd(n_), 'shift': shift, 'shift_signed': signed,
                'flag': bool(shift is not None and shift >= SHIFT_GATE),
                'differ_share': share(differ, len(n_)) if scale <= 0 else None,
                'source': 'genome' if source is G else 'farm traits tsv'}

    if G:
        for key, label in TRAIT_LABELS:
            row = trait_row(key, label, G, animal_founders, animals_alive, first_window)
            if row:
                trait_rows.append(row)
    if tsv:
        for key, label in G2_TRAITS:
            row = trait_row(key, label, tsv, animal_founders, animals_alive, first_window)
            if row:
                trait_rows.append(row)
    for row in trait_rows:
        drift_ref[row['trait']] = row
    if any(key not in drift_ref for key, _ in G2_TRAITS):
        R['not_available'].append(
            'G2\'s four heritable traits (body mass, developed part count, limb push, the brain\'s mean drive): they need '
            'the world\'s own Developer and Brain, which only the farm builds (`Evosim.Farm --grazer-traits <run dir> '
            '<out tsv>`, then pass the table with --traits). The run\'s files hold the genome, not the developed body, so '
            'section 3 gives genome stand-ins: adult scale (mass goes as its cube), genome volume, grown nodes, limb power.')
    R['traits'] = {'rows': trait_rows, 'tsv': tsv_path, 'first_window': [w0, w1],
                   'first_window_births': len(first_window),
                   'window_used': use_window, 'gate': SHIFT_GATE,
                   'founder_animals': len(animal_founders), 'living_animals': len(animals_alive),
                   'living_founders_among_animals': sum(1 for i in animals_alive if births[i]['p'] < 0),
                   'flagged': [r['trait'] for r in trait_rows if r['flag']]}

    # ---- 4. new body plans
    plans = {'available': bool(G)}
    if G:
        founder_sigs = {G[i]['sig'] for i in founders if i in G}
        groups = defaultdict(list)
        for i in alive:
            if i in G:
                groups[G[i]['sig']].append(i)
        new_groups = {s: ids for s, ids in groups.items() if s not in founder_sigs}

        def kind_of(i):
            return 'animal' if cls[i] in ANIMAL_CLASSES else ('producer' if cls[i] == 'producer' else 'other')

        def plan_row(sig, ids, rank):
            ex = pick(ids, births, fractions)
            gens = [births[i].get('g', 0) for i in ids]
            return {'rank': rank, 'bodies': len(ids), 'distinct_lineages': len({root_of.get(i, i) for i in ids}),
                    'generations': [min(gens), max(gens)], 'oldest_living_born_t': min(births[i]['t'] for i in ids),
                    'example_id': ex, 'example_body_fraction': fractions.get(ex), 'cells': G[ex]['cells'],
                    'plain': G[ex].get('plain'), 'signature': sig}
        animal_new = {s: [i for i in ids if kind_of(i) == 'animal'] for s, ids in new_groups.items()}
        animal_new = {s: ids for s, ids in animal_new.items() if ids}
        other_new = {s: [i for i in ids if kind_of(i) != 'animal'] for s, ids in new_groups.items()}
        other_new = {s: ids for s, ids in other_new.items() if ids}
        animals_in_G = [i for i in animals_alive if i in G]
        deep_all = max(alive, key=lambda i: (births[i].get('g', 0), fractions.get(i) or 0, i)) if alive else None
        top_g = max((births[j].get('g', 0) for j in animals_alive), default=None)
        deep_anim = pick([i for i in animals_alive if births[i].get('g', 0) == top_g], births, fractions) if animals_alive else None
        plans.update({
            'founder_bodies': len(founders), 'founder_plans': len(founder_sigs),
            'living_bodies': len([i for i in alive if i in G]), 'living_plans': len(groups),
            'new_plans': len(new_groups), 'bodies_in_new_plans': sum(len(v) for v in new_groups.values()),
            'animals': {'living': len(animals_in_G), 'plans': len({G[i]['sig'] for i in animals_in_G}),
                        'new_plans': len(animal_new), 'bodies_in_new_plans': sum(len(v) for v in animal_new.values())},
            'top_new_animal_plans': [plan_row(s, ids, k + 1) for k, (s, ids) in enumerate(
                sorted(animal_new.items(), key=lambda kv: (-len(kv[1]), kv[0]))[:args.top])],
            'other_new_plans': {'plans': len(other_new), 'bodies': sum(len(v) for v in other_new.values())},
            'top_new_other_plans': [plan_row(s, ids, k + 1) for k, (s, ids) in enumerate(
                sorted(other_new.items(), key=lambda kv: (-len(kv[1]), kv[0]))[:max(2, args.top // 2)])],
            'deepest_alive': ({'id': deep_all, 'generation': births[deep_all].get('g'), 'class': cls[deep_all]}
                              if deep_all is not None else None),
            'deepest_animal_alive': ({'id': deep_anim, 'generation': births[deep_anim].get('g')} if deep_anim else None)})
    else:
        deep_all = max(alive, key=lambda i: (births[i].get('g', 0), i)) if alive else None
        plans['deepest_alive'] = ({'id': deep_all, 'generation': births[deep_all].get('g'), 'class': cls[deep_all]}
                                  if deep_all is not None else None)
    R['plans'] = plans

    # ---- 5. behaviour
    beh = {}
    last0 = max(0.0, at - args.window)
    n_kills = len(kills)
    victims = Counter()
    for (ac, vc), n in bites_on.items():
        victims[(ac, vc)] += n
    beh['cut_rows'] = {'total': n_kills,
                       'by_animals_on': {vc: n for (ac, vc), n in sorted(victims.items()) if ac == 'animal'},
                       'by_others': sum(n for (ac, vc), n in victims.items() if ac != 'animal'),
                       'rows_with_unknown_body': n_kills - sum(victims.values()),
                       'root_lost': sum(1 for k in kills if k[3] == 1),
                       'eaten_deaths': sum(1 for c in death_cause.values() if c == 'eaten')}
    last_cut = Counter()
    for t, victim, attacker, _, _ in kills:
        if t > last0:
            last_cut[(class_name.get(attacker), class_name.get(victim))] += 1
    beh['cut_rows_last_window'] = {'seconds': [last0, at],
                                   'animals_on_producers': last_cut[('animal', 'producer')],
                                   'animals_on_non_producers': sum(n for (a, v), n in last_cut.items() if a == 'animal' and v != 'producer')}
    beh['hunters'] = {'ever': len(bites_by), 'alive_now': by_class['hunter'], 'animals_alive': len(animals_alive),
                      'most_cuts_on_non_producers': [{'id': i, 'cuts': n, 'alive': i in alive}
                                                     for i, n in bites_by.most_common(3)]}
    deaths_last = Counter(death_cause.get(i) for i, t in death_t.items() if t > last0 and class_name.get(i) == 'animal')
    deaths_all = Counter(death_cause.get(i) for i in death_t if class_name.get(i) == 'animal')
    beh['animal_deaths_by_cause'] = {'all': dict(deaths_all), 'last_window': dict(deaths_last)}
    first_s = stats[0] if stats else None
    last_s = stats[-1] if stats else None
    win_s = next((r for r in stats if r['t'] >= last0 - 1e-9), None)
    if last_s and 'biteKeptLeaves' in last_s:
        def kept(row):
            return (row.get('biteKeptLeaves', 0), row.get('biteKeptOthers', 0), row.get('biteKeptRemains', 0))
        tot, base = kept(last_s), kept(win_s) if win_s else (0, 0, 0)
        win = tuple(a - b for a, b in zip(tot, base))
        beh['joules_kept_by_mouths'] = {
            'whole_run': dict(zip(('leaves', 'other_living', 'remains'), tot)),
            'whole_run_shares': dict(zip(('leaves', 'other_living', 'remains'), [share(v, sum(tot)) for v in tot])),
            'last_window': dict(zip(('leaves', 'other_living', 'remains'), win)),
            'last_window_shares': dict(zip(('leaves', 'other_living', 'remains'), [share(v, sum(win)) for v in win])),
            'mouth_blows': last_s.get('mouthBlows'), 'kills_swallowed': last_s.get('killsSwallowed')}
    else:
        beh['joules_kept_by_mouths'] = None
        R['not_available'].append('joules kept from leaves, living bodies and remains: this run\'s stats rows carry no '
                                  'biteKept columns (contact eating was off)')

    # movement
    animal_ids = {i for i, c in class_name.items() if c == 'animal'}
    islands, island_src = read_island_line(run, args.report)
    cell = float(pr.find_field(config, 'fieldMatterCellMetres') or g2.CELL_DEFAULT)
    shape = pr.find_field(config, 'worldShape')
    area = pr.find_field(config, 'worldAreaSquareMetres')
    side = math.sqrt(float(area)) if (shape == 'Box' and area) else None
    groves = g2.make_groves(islands, cell) if islands else []
    move = {'groves': len(groves), 'island_source': island_src, 'side_metres': side, 'stride': args.stride,
            'crossing_gap': args.crossing_gap}
    crossers = Counter()
    if args.no_positions or not pr.runrec.has_positions(run):
        move['read'] = False
        move['why'] = 'switched off by --no-positions' if args.no_positions else 'the run holds no positions file'
        R['not_available'].append('movement: ' + move['why'])
    else:
        mv = read_movement(run, animal_ids, groves, side, at, args.stride, args.crossing_gap, crossers, args.window)
        move.update(mv)
        move['read'] = True
        if not groves:
            move['crossings_note'] = ('no island line in a report beside the run (runs/%s.md) or in --report, so there '
                                      'are no places to cross between' % arm)
            R['not_available'].append('crossings between oases: ' + move['crossings_note'])
    beh['movement'] = move
    R['behaviour'] = beh

    # ---- 6. standouts
    R['standouts'] = standouts(args, R, births, class_name, cls, alive, animals_alive, fractions, G, drift_ref,
                               root_of, root_anim, top_anim, bites_by, crossers, move)

    R['summary'] = summary_lines(R)

    # ---- what cannot be measured
    if all(births[i].get('s', 0) == 0 for i in list(births)[:2000]) and (config.get('species') or {}).get('speciesDriftThreshold', 0) == 0:
        R['not_available'].append('species: speciesDriftThreshold is 0 in this run\'s config, so every body is species 0 and '
                                  'no species counts exist')
    R['not_available'].append('the developed body of any creature (part count, mass, limb push as built): only genomes '
                              'are recorded; poses.bin holds root poses and joint angles, not the part list')
    R['not_available'].append('where a creature was born: a birth row carries no position (the first positions row after '
                              'it is the nearest thing), and the patch field `pt` is a horizontal cell, not a part count')
    R['not_available'].append('cause of a death beyond aged, starved or eaten, and bites that did not cut a part off: only '
                              'a cut is a lineage row ("k"); the cumulative bite counters in stats.jsonl are not per body')
    return R


# ---------------------------------------------------------------- movement from positions

def on_stride(t, s):
    return abs(t / s - round(t / s)) < 1e-6


def read_movement(run, animal_ids, groves, side, t_max, stride, crossing_gap, crossers, window):
    """One pass over positions rows at every `stride` seconds up to t_max, animals only (remains bodies left out).
    Per animal: the furthest it got from its first fix (the shorter way round in a joined box) and, with groves, its
    moves between groves as g2-read.py's section 5 walks them (a fix over grove B whose last fix over a grove was over
    grove A; fixes outside every grove do not reset A; a crossing when the banded edge gap is at least crossing_gap)."""
    state = {}                  # id -> [x0, z0, furthest, last grove fix]
    moves = []
    rows_read = 0
    fixes = 0
    for t, line in pr.stream(run):
        if t > t_max + 1e-9:
            break
        if not on_stride(t, stride):
            continue
        try:
            bodies = json.loads(line)['b']
        except (ValueError, KeyError):
            continue
        rows_read += 1
        for b in bodies:
            i = b[0]
            if i not in animal_ids or b[4] & REMAINS_FLAG:
                continue
            x, z = b[1], b[3]
            fixes += 1
            st = state.get(i)
            if st is None:
                st = state[i] = [x, z, 0.0, None]
            d = math.hypot(g2.gap(st[0], x, side), g2.gap(st[1], z, side))
            if d > st[2]:
                st[2] = d
            if groves:
                k = g2.assign_grove(x, z, groves, side)
                if k >= 0:
                    was, st[3] = st[3], (k, t, x, z)
                    if was is not None and was[0] != k:
                        moves.append({'i': i, 't2': t, 'dur': t - was[1], 'a': was[0], 'b': k,
                                      'dist': math.hypot(g2.gap(was[2], x, side), g2.gap(was[3], z, side)),
                                      'egap': g2.edge_gap(was[0], k, groves, side)})
    out = {'rows_read': rows_read, 'animal_fixes': fixes, 'animals_fixed': len(state)}
    far = [s[2] for s in state.values()]
    out['furthest_from_first_fix'] = {
        'median_m': median(far), 'p90_m': (sorted(far)[int(0.9 * (len(far) - 1))] if far else None),
        'max_m': max(far) if far else None,
        'animals_over_50_m': sum(1 for v in far if v >= 50), 'animals_over_100_m': sum(1 for v in far if v >= 100)}
    if groves:
        cross = [m for m in moves if m['egap'] >= crossing_gap]
        strolls = [m for m in moves if m['egap'] < crossing_gap]
        for m in cross:
            crossers[m['i']] += 1
        out['moves'] = {'strolls': len(strolls), 'crossings': len(cross),
                        'animals_that_crossed': len({m['i'] for m in cross}),
                        'animals_that_moved': len({m['i'] for m in moves}),
                        'crossing_median_distance_m': median([m['dist'] for m in cross]),
                        'crossing_median_seconds': median([m['dur'] for m in cross]),
                        'crossing_longest_m': max((m['dist'] for m in cross), default=None),
                        'groves': [{'k': g['k'], 'columns': g['columns'], 'cx': g['cx'], 'cz': g['cz'],
                                    'reach_m': round(g['r'], 1)} for g in groves]}
        wins = []
        w = window
        t0 = 0.0
        while t0 < t_max - 1e-9:
            t1 = min(t0 + w, t_max)
            sub = [m for m in moves if t0 < m['t2'] <= t1 + 1e-9]
            wins.append({'from': t0, 'to': t1, 'strolls': sum(1 for m in sub if m['egap'] < crossing_gap),
                         'crossings': sum(1 for m in sub if m['egap'] >= crossing_gap)})
            t0 = t1
        out['moves']['per_window'] = wins
    return out


# ---------------------------------------------------------------- standouts

MOVED_SHARE = 0.10      # a trait with no scale counts as moved when this share of the living animals differs from the founders


def moved_rows(drift_ref):
    """The genome traits that moved: flagged, or with no scale and at least MOVED_SHARE of living animals different.
    All the genome traits when none moved."""
    rows = [r for r in drift_ref.values() if r['source'] == 'genome']
    moved = [r for r in rows if r['flag'] or (r['shift'] is None and (r['differ_share'] or 0) >= MOVED_SHARE)]
    return moved or rows


def drift_score(i, G, rows):
    """The root mean square, over `rows`, of how far a body's trait is from the founders' mean in units of the living
    animals' own spread in it (the larger of that and G2's scale). Per-body distances in G2's scale alone are huge for
    every body when the founders are clones, and say nothing of which body is the odd one."""
    zs = []
    for r in rows:
        v = (G.get(i) or {}).get('traits', {}).get(r['trait'])
        unit = max(r['scale'] or 0.0, r['now_sd'] or 0.0)
        if v is not None and unit > 0:
            zs.append((v - r['founders_mean']) / unit)
    return math.sqrt(sum(z * z for z in zs) / len(zs)) if zs else None


def drift_detail(i, G, rows, n=4):
    """The n traits where a body is furthest from the founders' mean, in the same units drift_score uses."""
    out = []
    for r in rows:
        v = (G.get(i) or {}).get('traits', {}).get(r['trait'])
        unit = max(r['scale'] or 0.0, r['now_sd'] or 0.0)
        if v is not None and unit > 0:
            out.append({'trait': r['trait'], 'label': r['label'], 'value': v, 'founders_mean': r['founders_mean'],
                        'units': (v - r['founders_mean']) / unit})
    return sorted(out, key=lambda d: -abs(d['units']))[:n]


def standouts(args, R, births, class_name, cls, alive, animals_alive, fractions, G, drift_ref, root_of, root_anim,
              top_anim, bites_by, crossers, move):
    cands, notes = [], []

    def add(i, why, kind):
        if i is not None and i in alive:
            cands.append({'id': i, 'kind': kind, 'why': why})

    plans = R['plans']
    # a new plan in many bodies
    tops = plans.get('top_new_animal_plans') or []
    if tops and tops[0]['bodies'] >= args.min_plan:
        p = tops[0]
        add(p['example_id'], 'a plan no founder had, held by %d living animals from %d lineage(s), generations %d to %d: %s'
            % (p['bodies'], p['distinct_lineages'], p['generations'][0], p['generations'][1], p['plain'] or p['cells']),
            'new plan')
    elif plans.get('available'):
        notes.append('new plan: none picked, the largest plan no founder had holds %d living animal(s), under --min-plan %d'
                     % (tops[0]['bodies'] if tops else 0, args.min_plan))
    # a lineage that took over
    L = R['lineages']
    took = None
    if top_anim and animals_alive:
        r = top_anim[0]
        row = next((x for x in L['top_by_animals'] if x['root'] == r), None)
        if row and row['share_of_animals'] >= 0.10:
            took = row
    if took:
        members = [i for i in animals_alive if root_of.get(i, i) == took['root']]
        ex = pick(members, births, fractions)
        rise = ('rose from %s at %g s' % (took['first_seen_size'], took['first_seen_t'])
                if took['first_seen_size'] is not None else '')
        add(ex, 'the lineage of founder %d holds %d of %d living animals (%s); %s; its deepest grown member, generation %d'
            % (took['root'], took['animals'], len(animals_alive), pct(took['share_of_animals']),
               rise + ('; rose from few to many' if took['rose'] else ''), births[ex].get('g', 0)), 'lineage')
    else:
        notes.append('lineage: none picked, no lineage holds 10% of the living animals' if animals_alive else
                     'lineage: none picked, no animals alive')
    # the most drifted
    if G and drift_ref and animals_alive:
        rows = moved_rows(drift_ref)
        scored = [(drift_score(i, G, rows), i) for i in animals_alive if i in G]
        scored = [(s, i) for s, i in scored if s is not None]
        if scored:
            grown = [(s, i) for s, i in scored if (fractions.get(i) or 0) >= BODY_FRACTION_GROWN] or scored
            s, i = max(grown)
            add(i, 'the living animal furthest from the founders over the %d trait(s) that moved: %.1f of the living animals\' '
                'own standard deviations from the founders\' mean, root mean square' % (len(rows), s), 'most drifted')
    # the deepest descendant
    da = plans.get('deepest_animal_alive')
    if da:
        add(da['id'], 'the deepest animal alive: generation %d' % da['generation'], 'deepest')
    # a crosser, then a hunter
    if crossers:
        grown_c = [i for i in crossers if i in alive and (fractions.get(i) or 0) >= BODY_FRACTION_GROWN] or \
                  [i for i in crossers if i in alive]
        if grown_c:
            i = max(grown_c, key=lambda j: (crossers[j], j))
            add(i, 'crossed between groves %d time(s), each over a banded gap of at least %g m' % (crossers[i], args.crossing_gap),
                'crosser')
        else:
            notes.append('crosser: none picked, no animal that crossed is alive at %g s' % R['at'])
    hunters_alive = [i for i in bites_by if i in alive]
    if hunters_alive:
        grown_h = [i for i in hunters_alive if (fractions.get(i) or 0) >= BODY_FRACTION_GROWN] or hunters_alive
        i = max(grown_h, key=lambda j: (bites_by[j], j))
        add(i, 'a hunter: %d bite(s) that cut a part off a body that was not a producer' % bites_by[i], 'hunter')
    # merge one id's reasons, keep the first five
    merged, order = {}, []
    for c in cands:
        if c['id'] in merged:
            merged[c['id']]['why'] += '; also ' + c['kind'] + ': ' + c['why']
            merged[c['id']]['kinds'].append(c['kind'])
        else:
            merged[c['id']] = {'id': c['id'], 'kinds': [c['kind']], 'why': c['why']}
            order.append(c['id'])
    out = []
    for i in order[:5]:
        m = merged[i]
        m['generation'] = births[i].get('g')
        m['class'] = cls.get(i)
        m['body_fraction'] = fractions.get(i)
        m['plain'] = (G.get(i) or {}).get('plain')
        m['traits'] = (G.get(i) or {}).get('traits')
        if 'most drifted' in m['kinds'] and G and drift_ref:
            m['drift_detail'] = drift_detail(i, G, moved_rows(drift_ref))
        out.append(m)
    return {'picks': out, 'not_picked': notes, 'dropped_for_the_cap': [merged[i]['kinds'] for i in order[5:]]}


# ---------------------------------------------------------------- the short version

def summary_lines(R):
    """A few lines that say the report in numbers, composed from the sections below them and nothing else."""
    out = []
    c = R['classes']
    animals = c['grazer'] + c['hunter']
    out.append('alive at %g s: %d bodies, of them %d animals (%d grazers, %d hunters) and %d producers'
               % (R['at'], R['alive'], animals, c['grazer'], c['hunter'], c['producer']))
    crashes = {k: len(v['busts']) for k, v in R['alive_over_time']['busts'].items()}
    out.append('crashes of %d or more bodies: animals %d, hunters %d, producers %d'
               % (R['alive_over_time']['busts']['animals']['min_reference'], crashes['animals'], crashes['hunters'],
                  crashes['producers']))
    L = R['lineages']
    top = L['top_by_animals'][0] if L['top_by_animals'] else None
    out.append('lineages: %d of %d founder lineages have living animals; %s; %d rose from few to many'
               % (L['founder_lineages_with_living_animals'], L['founder_bodies'],
                  ('the largest holds %s of the animals' % pct(top['share_of_animals'])) if top else 'no animals alive',
                  len(L['rose_few_to_many'])))
    T = R['traits']
    if T['rows']:
        out.append('traits: %d of %d compared shifted by %g or more founder standard deviations (%s); %d had no spread to judge by'
                   % (len(T['flagged']), len(T['rows']), T['gate'], ', '.join(T['flagged']) or 'none',
                      sum(1 for r in T['rows'] if r['shift'] is None)))
    else:
        out.append('traits: none could be read')
    P = R['plans']
    if P.get('animals'):
        a = P['animals']
        best = P['top_new_animal_plans'][0]['bodies'] if P['top_new_animal_plans'] else 0
        out.append('plans: %s of living animals carry a plan no founder had (%d such plans; the largest is held by %d animals)'
                   % (pct(share(a['bodies_in_new_plans'], a['living'])), a['new_plans'], best))
    m = R['behaviour']['movement']
    if m.get('moves'):
        last = m['moves']['per_window'][-1] if m['moves']['per_window'] else None
        out.append('movement: %d crossings between groves by %d animals%s'
                   % (m['moves']['crossings'], m['moves']['animals_that_crossed'],
                      '' if not last else ', %d of them in %g to %g s' % (last['crossings'], last['from'], last['to'])))
    elif m.get('read'):
        out.append('movement: no crossings count (no islands to cross between); %d animals got 50 m or more from where first seen'
                   % m['furthest_from_first_fix']['animals_over_50_m'])
    out.append('worth filming: %s' % (', '.join('%d (%s)' % (s['id'], '/'.join(s['kinds'])) for s in R['standouts']['picks'])
                                       or 'nothing picked'))
    return out


# ---------------------------------------------------------------- printing

def rule(title):
    print()
    print('== ' + title)


def print_report(R):
    p = print
    p('what-evolved: %s   (%s)' % (R['arm'], os.path.relpath(R['run'], REPO) if R['run'].startswith(REPO) else R['run']))
    bits = ['read at %g s' % R['at']]
    if R.get('simulatedSeconds') is not None:
        bits.append('the run reached %g s (status %s%s)' % (R['simulatedSeconds'], R.get('status'),
                                                          ', ' + str(R['reason']) if R.get('reason') else ''))
    bits.append('seed %s' % R.get('seed'))
    bits.append('configHash %s' % R.get('configHash'))
    p('   ' + '; '.join(bits))
    for n in R['notes']:
        p('   note: ' + n)
    c = R['checks']
    p('   cross-check at %g s: alive by lineage %d, in the stats row %s, in the snapshot at %s s %s%s' % (
        R['at'], c['alive_by_lineage'], c['alive_in_stats_row'], c.get('snapshot_second', '-'), c.get('alive_in_snapshot', '-'),
        ' (snapshot ids equal the lineage ids)' if c.get('snapshot_ids_equal_lineage_ids') else
        (' (snapshot ids differ from the lineage ids)' if c.get('snapshot_ids_equal_lineage_ids') is False else '')))
    f = R['founders']
    p('   founders (born without a parent in the record): %d bodies, %d of them animals; sources %s; born from %g to %g s'
      % (f['bodies'], f['animals'], ', '.join('%s %d' % kv for kv in sorted(f['by_source'].items())),
         f['first_t'], f['last_t']))
    p('   classes: animal = a mouth (atk 1 and ink 1; G2\'s grazer); hunter = an animal that has cut a part off a body that is '
      'not a producer; producer = no mouth, photosynthetic tissue')
    p('   in short:')
    for line in R['summary']:
        p('     - ' + line)

    # 1
    rule('1. Who is alive')
    s1 = R['alive_over_time']
    cols = s1['columns']
    p('   %9s %7s %9s %8s %8s %9s %8s %6s' % ('second', 'alive', 'producers', 'grazers', 'hunters', 'absorbers', 'jointed', 'other'))
    for r in s1['rows']:
        p('   %9g %7d %9d %8d %8d %9d %8d %6d' % (r['t'], r['alive'], r['producer'], r['grazer'], r['hunter'],
                                                 r['absorber'], r['jointed'], r['other']))
    p('   (jointed = a joint and no mouth or tissue; other = none of these)')
    p('   crashes (a fall to half or less of the highest count in the %g s before it) and their recoveries (a return to that count):'
      % BUST_BEFORE)
    for name, label in (('animals', 'animals (grazers + hunters)'), ('hunters', 'hunters'), ('producers', 'producers')):
        d = s1['busts'][name]
        small = ('' if not d['small_falls'] else ' (%d smaller fall(s) from fewer than %d bodies not listed)'
                 % (d['small_falls'], d['min_reference']))
        if not d['busts']:
            p('     %s: none; highest count %d%s' % (label, d['peak'], small))
            continue
        p('     %s: highest count %d, %d crash(es)%s' % (label, d['peak'], len(d['busts']), small))
        for b in d['busts']:
            rec = ('back to %d at %g s, %g s after it began' % (b['ref'], b['recovery'], b['recovery'] - b['t'])
                   if b['recovery'] is not None else 'not back to %d by %g s' % (b['ref'], R['at']))
            p('        began at %g s (the highest count in the %g s before was %d); lowest %d at %g s, %.2f of it; %s'
              % (b['t'], BUST_BEFORE, b['ref'], b['low'], b['low_t'], b['low'] / b['ref'], rec))

    # 2
    rule('2. Lineages')
    L = R['lineages']
    p('   %d founder bodies have started lineages; %d lineages have a living member (%d living bodies); %d have a living animal (%d living animals).'
      % (L['founder_bodies'], L['founder_lineages_alive'], L['living_bodies'], L['founder_lineages_with_living_animals'],
         L['living_animals']))
    hdr = '   %9s %-9s %-8s %7s %7s %8s %7s  %s'
    p('   the lineages holding the most living animals:')
    p(hdr % ('founder', 'root is', 'source', 'born', 'animals', 'bodies', 'share', 'first seen (size) -> peak, few to many'))
    for r in L['top_by_animals']:
        fs = '%g s (%s)' % (r['first_seen_t'], r['first_seen_size']) if r['first_seen_t'] is not None else '-'
        p(hdr % (r['root'], r['root_class'], r['source'] or 'bred', num(r['born_t'], '{:g}'), r['animals'], r['bodies'],
                 pct(r['share_of_animals']), '%s -> %d%s' % (fs, r['peak'], '  ROSE' if r['rose'] else '')))
    p('   the lineages holding the most living bodies of any kind:')
    for r in L['top_by_bodies']:
        p(hdr % (r['root'], r['root_class'], r['source'] or 'bred', num(r['born_t'], '{:g}'), r['animals'], r['bodies'],
                 pct(share(r['bodies'], L['living_bodies'])), '-'))
    if L['rose_few_to_many']:
        p('   animal lineages that rose from few (%d or fewer at the first %g-s look that saw them) to many (%d or more now): %d'
          % (L['few'], (L['grid_seconds'][1] - L['grid_seconds'][0]) if len(L['grid_seconds']) > 1 else 0, L['many'],
             len(L['rose_few_to_many'])))
        for r in L['rose_few_to_many']:
            p('     founder %d (%s, born %g s): %s animals at %g s -> peak %d -> %d now'
              % (r['root'], r['source'] or 'bred', r['born_t'], r['first_seen_size'], r['first_seen_t'], r['peak'], r['animals']))
    else:
        p('   animal lineages that rose from few to many: none (few = %d or fewer at the first look that saw them, many = %d or more now)'
          % (L['few'], L['many']))

    # 3
    rule('3. Trait drift against the founders')
    T = R['traits']
    p('   founder animals: %d. living animals now: %d (%d of them founders). shift = |living mean - founder mean| / scale; '
      'a * marks %g or more.' % (T['founder_animals'], T['living_animals'], T['living_founders_among_animals'], T['gate']))
    if T['window_used']:
        p('   scale = the larger of the founders\' sd and the sd of the %d animals born in %g to %g s (G2\'s rule: founders are '
          'often clones).' % (T['first_window_births'], T['first_window'][0], T['first_window'][1]))
    else:
        p('   scale = the founders\' sd. The first window (animals born in %g to %g s) is not used: it holds %d births, and needs '
          '%d and an end at or before %g s.' % (T['first_window'][0], T['first_window'][1], T['first_window_births'],
                                                MIN_BIRTHS, R['at']))
    if T['rows']:
        p('   %-48s %5s %9s %8s %7s %8s %9s %9s' % ('trait', 'n fnd', 'fnd mean', 'fnd sd', 'scale', 'now n', 'now mean', 'shift'))
        for r in T['rows']:
            if r['shift'] is None:
                cell = 'no scale; %s differ' % pct(r['differ_share'])
            else:
                cell = '%s%s' % ('-' if r['shift_signed'] < 0 else '+', num(r['shift'], '{:.2f}'))
            p('   %-48s %5d %9s %8s %7s %8d %9s  %s%s' % (
                r['label'][:48], r['founders_n'], num(r['founders_mean'], '{:.4g}'), num(r['founders_sd'], '{:.3g}'),
                num(r['scale'], '{:.3g}'), r['now_n'], num(r['now_mean'], '{:.4g}'), cell, ' *' if r['flag'] else ''))
        if T['flagged']:
            p('   shifted by %g or more: %s' % (T['gate'], ', '.join(T['flagged'])))
        else:
            p('   no trait shifted by %g founder standard deviation(s)' % T['gate'])
        nos = [r['trait'] for r in T['rows'] if r['shift'] is None]
        if nos:
            p('   not judged, no spread to measure against (founders and the first window are all alike in them): %s' % ', '.join(nos))
    else:
        p('   no trait could be read (see the list at the end).')
    if T['tsv']:
        p('   the farm traits table %s supplied body mass, part count, limb push and the drives (the last rows above)' % T['tsv'])

    # 4
    rule('4. New body plans')
    P = R['plans']
    if not P.get('available'):
        p('   not available: the run holds no genomes.jsonl.gz')
    else:
        p('   a plan is genome-query.py\'s structural signature (cells, shapes, joints, edges, repeats; not size, power or brain).')
        p('   founders: %d bodies, %d plans. living: %d bodies, %d plans.' % (
            P['founder_bodies'], P['founder_plans'], P['living_bodies'], P['living_plans']))
        p('   plans no founder had: %d plans on %d of %d living bodies (%s)' % (
            P['new_plans'], P['bodies_in_new_plans'], P['living_bodies'], pct(share(P['bodies_in_new_plans'], P['living_bodies']))))
        a = P['animals']
        p('   animals only: %d living, %d plans, %d plans no founder had, on %d animals (%s)' % (
            a['living'], a['plans'], a['new_plans'], a['bodies_in_new_plans'], pct(share(a['bodies_in_new_plans'], a['living']))))
        p('   the largest new plans among animals:')
        for r in P['top_new_animal_plans']:
            p('     #%d: %d bodies, %d lineage(s), generations %d to %d, oldest living born %g s, example id %d%s'
              % (r['rank'], r['bodies'], r['distinct_lineages'], r['generations'][0], r['generations'][1],
                 r['oldest_living_born_t'], r['example_id'],
                 '' if r['example_body_fraction'] is None else ' (body fraction %.2f)' % r['example_body_fraction']))
            p('         %s' % (r['plain'] or r['cells']))
        o = P['other_new_plans']
        p('   bodies that are not animals: %d plans no founder had, on %d bodies; the largest:' % (o['plans'], o['bodies']))
        for r in P['top_new_other_plans']:
            p('     #%d: %d bodies, %d lineage(s), generations %d to %d, example id %d, cells %s' % (
                r['rank'], r['bodies'], r['distinct_lineages'], r['generations'][0], r['generations'][1], r['example_id'], r['cells']))
    d = P['deepest_alive']
    if d:
        p('   deepest generation alive: %d (id %d, %s)' % (d['generation'], d['id'], d['class']))
    if P.get('deepest_animal_alive'):
        p('   deepest animal alive: generation %d (id %d)' % (P['deepest_animal_alive']['generation'], P['deepest_animal_alive']['id']))

    # 5
    rule('5. Behaviour')
    B = R['behaviour']
    cr = B['cut_rows']
    p('   bites that cut a part off a body (lineage kill rows) up to %g s: %d, of which %d took the body\'s root part '
      '(deaths recorded as eaten: %d)' % (R['at'], cr['total'], cr['root_lost'], cr['eaten_deaths']))
    p('     by animals, on: %s; by bodies without a mouth: %d; rows naming a body not in the record: %d' % (
        ', '.join('%s %d' % kv for kv in cr['by_animals_on'].items()) or 'nothing', cr['by_others'], cr['rows_with_unknown_body']))
    lw = B['cut_rows_last_window']
    p('     in the last %g s: %d on producers, %d on other bodies' % (lw['seconds'][1] - lw['seconds'][0], lw['animals_on_producers'],
                                                                      lw['animals_on_non_producers']))
    h = B['hunters']
    p('   hunters: %d animals ever cut a part off a non-producer; %d of the %d living animals are hunters' % (
        h['ever'], h['alive_now'], h['animals_alive']))
    dc = B['animal_deaths_by_cause']
    p('   animal deaths by cause, whole run: %s; last window: %s' % (
        ', '.join('%s %d' % kv for kv in sorted(dc['all'].items(), key=lambda kv: -kv[1])) or '-',
        ', '.join('%s %d' % kv for kv in sorted(dc['last_window'].items(), key=lambda kv: -kv[1])) or '-'))
    jk = B['joules_kept_by_mouths']
    if jk:
        def line(d, sh):
            return ', '.join('%s %.3g J (%s)' % (k, d[k], pct(sh[k])) for k in d)
        p('   joules kept by mouths, whole run: %s' % line(jk['whole_run'], jk['whole_run_shares']))
        p('     last window: %s; mouth blows %s, kills swallowed %s' % (
            line(jk['last_window'], jk['last_window_shares']), jk['mouth_blows'], jk['kills_swallowed']))
    mv = B['movement']
    if not mv['read']:
        p('   movement: not read (%s)' % mv['why'])
    else:
        p('   movement, from %d positions rows at every %g s (%d animal fixes, %d animals):' % (
            mv['rows_read'], mv['stride'], mv['animal_fixes'], mv['animals_fixed']))
        fo = mv['furthest_from_first_fix']
        p('     furthest each animal got from where it was first seen: median %s m, 90th percentile %s m, longest %s m; '
          '%d animals reached 50 m, %d reached 100 m' % (num(fo['median_m'], '{:.1f}'), num(fo['p90_m'], '{:.1f}'),
                                                        num(fo['max_m'], '{:.1f}'), fo['animals_over_50_m'], fo['animals_over_100_m']))
        if mv.get('moves') is None:
            p('     between places: not available (%s)' % mv.get('crossings_note', 'no groves'))
        else:
            m = mv['moves']
            p('     %d groves (island centre, radius + 10 m) from %s; %s' % (
                mv['groves'], os.path.relpath(mv['island_source'], REPO) if mv['island_source'].startswith(REPO) else mv['island_source'],
                'edges joined, side %g m' % mv['side_metres'] if mv['side_metres'] else 'edges not joined'))
            p('     moves from one grove to another: %d crossings (banded gap of at least %g m) and %d strolls, by %d animals of the %d that moved'
              % (m['crossings'], mv['crossing_gap'], m['strolls'], m['animals_that_crossed'], m['animals_that_moved']))
            if m['crossings']:
                p('     crossings: median %s m fix to fix in a median %s s; longest %s m' % (
                    num(m['crossing_median_distance_m'], '{:.0f}'), num(m['crossing_median_seconds'], '{:.0f}'),
                    num(m['crossing_longest_m'], '{:.0f}')))
            p('     by window of %g s (crossings, strolls): %s' % (
                m['per_window'][0]['to'] - m['per_window'][0]['from'] if m['per_window'] else 0,
                '; '.join('%g-%g s %d, %d' % (w['from'], w['to'], w['crossings'], w['strolls']) for w in m['per_window'])))

    # 6
    rule('6. Standouts (alive at %g s, for the theatre to photograph; nothing was photographed)' % R['at'])
    S = R['standouts']
    if not S['picks']:
        p('   none picked')
    for k, s in enumerate(S['picks'], 1):
        p('   %d. id %d  [%s]  generation %s%s' % (k, s['id'], ', '.join(s['kinds']), s['generation'],
                                                  '' if s['body_fraction'] is None else ', body fraction %.2f' % s['body_fraction']))
        p('      why: ' + s['why'])
        if s.get('plain'):
            p('      body: ' + s['plain'])
        if s.get('drift_detail'):
            p('      furthest in: ' + '; '.join('%s %s (founders %s)' % (d['label'].split(' (')[0], num(d['value'], '{:.4g}'),
                                                                        num(d['founders_mean'], '{:.4g}'))
                                                for d in s['drift_detail']))
    for n in S['not_picked']:
        p('   not picked: ' + n)
    if S['dropped_for_the_cap']:
        p('   over the cap of five and dropped: ' + '; '.join('/'.join(k) for k in S['dropped_for_the_cap']))

    rule('What this report cannot measure')
    seen = set()
    for n in R['not_available']:
        if n in seen:
            continue
        seen.add(n)
        p('   - ' + n)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('run', help='a run directory, a round folder with one run, or a name under runs/')
    ap.add_argument('--at', type=float, default=None, help='seconds to read (default the last sample)')
    ap.add_argument('--json', action='store_true', help='one JSON object of the facts, no text')
    ap.add_argument('--traits', default=None, help='the farm --grazer-traits TSV (body mass, parts, limb push, drive)')
    ap.add_argument('--report', default=None, help='the run\'s report .md holding the island line (default runs/<arm>.md)')
    ap.add_argument('--top', type=int, default=5, help='rows in the top lists')
    ap.add_argument('--rows', type=int, default=12, help='about this many rows in the counts-over-time table')
    ap.add_argument('--from', dest='t_from', type=float, default=6000.0,
                    help='the first window for the trait scale starts here (G2: 6000)')
    ap.add_argument('--window', type=float, default=3000.0, help='the window\'s length, and the last-window length (G2: 3000)')
    ap.add_argument('--stride', type=float, default=50.0, help='positions rows kept every this many seconds')
    ap.add_argument('--crossing-gap', type=float, default=10.0, help='banded edge gap that makes a move a crossing (G2: 10 m)')
    ap.add_argument('--few', type=int, default=5, help='"few" for a lineage that rose from few to many')
    ap.add_argument('--many', type=int, default=50, help='"many" for a lineage that rose from few to many')
    ap.add_argument('--min-plan', type=int, default=5, help='living animals a new plan needs to be picked as a standout')
    ap.add_argument('--min-crash', type=int, default=10,
                    help='a fall counts as a crash only from at least this many bodies (smaller ones are counted, not listed)')
    ap.add_argument('--no-positions', action='store_true', help='skip positions.jsonl.gz (the slowest read)')
    args = ap.parse_args()
    if args.rows < 2 or args.stride <= 0 or args.window <= 0:
        ap.error('--rows must be 2 or more; --stride and --window must be above 0')
    R = build(args)
    try:
        if args.json:
            print(json.dumps(clean(R), indent=1))
        else:
            print_report(R)
    except BrokenPipeError:
        pass
    return 0


if __name__ == '__main__':
    sys.exit(main())
