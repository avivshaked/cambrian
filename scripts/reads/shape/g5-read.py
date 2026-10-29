"""G5's read for the shape screen: do family limbs take hold, and do grazers swim no worse (g5-prereg-draft.md)?

One invocation reads one seed's pair: ON, the world that lists D233's superellipsoid family and turns on D229's shaped
added mass, and OFF, the same world and seed with both off. It reads and prints; it writes nothing and opens files
shared, so it can look at a run that is still being written.

Usage: python -I scripts/reads/shape/g5-read.py --on ON --off OFF [--at 24000] [--from 12000] [--to 24000]
                                                [--still 0.005] [--gap 10] [--every 1]

ON and OFF are each a run directory (the one holding genomes.jsonl.gz and stats.jsonl) or an arm folder, which means its
latest dated run (the last stamped subfolder that holds genomes.jsonl.gz), or a name under runs/.

Definitions, in plain words.

  Living bodies        the ids in the snapshot nearest --at (genome-query.py's alive_at). "Genomes read" is how many of
                       them have a birth row and a genome; shares are taken over those.
  Jointed body         a living body with a grown non-root node whose joint is not Fixed. A grown node is one that
                       development reaches from the root (genome-query.py's reachable()); the root is g['root'].
  Family limb          such a node whose shape is "superellipsoid". Family-jointed body: a body with at least one.
  Gate 2 share         family-jointed bodies over jointed bodies, in ON. PASS at 0.10 or more. OFF gets the same three
                       counts as a sanity line; its family should be 0.
  Node share           family limbs over all jointed non-root grown nodes of living bodies in ON (nodes, not bodies).
  Form walk            for each family limb, the distance of (ln profile, ln section, ln taper) from the nearest of the
                       three entry members, box (0.1, 0.1, 1), sphere (1, 1, 1), capsule (0.5, 1, 1), each axis's
                       difference divided by the log width of its range (profile 0.1-1, section 0.1-2, taper 0.25-4),
                       as a Euclidean norm. Printed for family limbs, and again for every grown superellipsoid node.
  Grazer               a body born mouthed: atk 1 and ink 1 on its birth row, born at or before --to
                       (grazer-motion-read.py's rule).
  Pose pair            two consecutive kept frames of poses.bin that hold the same living grazer (flags & 8, remains,
                       excluded), at most --gap * max(1, --every) world seconds apart. Its speed is the horizontal
                       (x, z) distance between the two frames' root positions over the gap, in metres per world second.
                       Frames after --to are dropped, then every --every'th frame is kept, exactly as
                       grazer-motion-read.py does.
  Moving pair          a pair whose speed is at least --still and whose later frame's t is from --from to --to inclusive.
  Gate 3               the median speed of ON's moving pairs over OFF's, PASS at 0.80 or more. Medians are taken the way
                       grazer-motion-read.py's q() takes them: the sorted value at index int(p * n).
  Carriers             ON's moving pairs split by whether the grazer's own genome carries a family limb as defined
                       above (genome read by body id from ON's genomes.jsonl.gz).
  Pace                 world seconds per wall second: (last t - first t) over (last wallTotalMs - first wallTotalMs) / 1000
                       across the stats.jsonl rows with t at or below --to (pace-check.py's pace(), whose rows are
                       "t" and the cumulative "wallTotalMs"), and the same over the --from to --to window. The two
                       worlds ran at different times, so the ratio is a reading to take with caution.

Exits 0 when both gates pass, 1 when one fails, 2 on a usage error or a run it could not find.
"""
import argparse
import importlib.util
import json
import math
import os
import sys
from array import array

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.normpath(os.path.join(HERE, '..', '..'))
ROOT = os.path.dirname(SCRIPTS)


def load_module(name, filename):
    """Load a script with a hyphen in its name by file path; neither runs anything at import."""
    spec = importlib.util.spec_from_file_location(name, os.path.join(SCRIPTS, filename))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


gq = load_module('genome_query', 'genome-query.py')
poses = load_module('poses_read', 'poses-read.py')

FAMILY = 'superellipsoid'
# D233's ranges, and the members a part enters the family at (shape-family-build.md, "The genes piece").
RANGES = {'profile': (0.1, 1.0), 'section': (0.1, 2.0), 'taper': (0.25, 4.0)}
GENES = ('profile', 'section', 'taper')
MEMBERS = {'box': (0.1, 0.1, 1.0), 'sphere': (1.0, 1.0, 1.0), 'capsule': (0.5, 1.0, 1.0)}
LOG_WIDTH = [math.log(RANGES[k][1] / RANGES[k][0]) for k in GENES]


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def resolve(arg):
    """A run directory, or an arm folder (its latest dated run), or a name under runs/. This differs from
    genome-query.py's resolve_run only where an arm folder holds several runs: that one refuses, this takes the
    last in name order, the way pace-check.py's latest() does."""
    for path in (arg, os.path.join(ROOT, 'runs', arg)):
        if not os.path.isdir(path):
            continue
        if os.path.exists(os.path.join(path, 'genomes.jsonl.gz')):
            return path
        subs = sorted(d for d in os.listdir(path) if os.path.exists(os.path.join(path, d, 'genomes.jsonl.gz')))
        if subs:
            return os.path.join(path, subs[-1])
    fail('no run with genomes.jsonl.gz at %s (or under runs/)' % arg)


def q(xs, p):
    """grazer-motion-read.py's q(): the sorted value at index int(p * n), nan when empty."""
    if not len(xs):
        return float('nan')
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(p * len(xs)))]


def fnum(x, spec='%.4f'):
    return 'n/a' if x != x else spec % x


# ---------------------------------------------------------------- gate 2: the shapes in living bodies

def jointed_nodes(g):
    """(index, node) for each grown non-root node whose joint is not Fixed: genome-query.py find's rule."""
    live = gq.reachable(g)
    root = g.get('root', 0)
    return [(i, n) for i, n in enumerate(g['nodes']) if i in live and i != root and n['joint'] != 'Fixed']


def is_family(n):
    return n['shape'].lower() == FAMILY


def family_limbs(g):
    return [n for _, n in jointed_nodes(g) if is_family(n)]


def sitting(run, at):
    """(t, alive count, genomes read, jointed bodies, family-jointed bodies, jointed nodes, family limbs, form rows
    for family limbs, form rows for every grown family node, nodes with no form)."""
    t, alive, births, genomes = gq.alive_genomes(run, at, False)
    jointed = family_bodies = jnodes = flimbs = 0
    walk_limbs, walk_all, no_form = [], [], 0
    for gid in sorted(genomes):
        g = genomes[gid]
        nodes = jointed_nodes(g)
        if nodes:
            jointed += 1
        jnodes += len(nodes)
        fam = [n for _, n in nodes if is_family(n)]
        if fam:
            family_bodies += 1
        flimbs += len(fam)
        live = gq.reachable(g)
        for n in fam:
            d = form_distance(n)
            if d is None:
                no_form += 1
            else:
                walk_limbs.append(d)
        for i, n in enumerate(g['nodes']):
            if i in live and is_family(n):
                d = form_distance(n)
                if d is not None:
                    walk_all.append(d)
    return {'t': t, 'alive': len(alive), 'genomes': len(genomes), 'jointed': jointed, 'family': family_bodies,
            'jnodes': jnodes, 'flimbs': flimbs, 'walk_limbs': walk_limbs, 'walk_all': walk_all, 'no_form': no_form}


def form_distance(n):
    """Distance of a node's form from the nearest entry member, in the units of the ranges' log widths."""
    f = n.get('form')
    if not f:
        return None
    try:
        v = [math.log(float(f[k])) for k in GENES]
    except (KeyError, TypeError, ValueError):
        return None
    best = None
    for m in MEMBERS.values():
        d = math.sqrt(sum(((v[j] - math.log(m[j])) / LOG_WIDTH[j]) ** 2 for j in range(3)))
        best = d if best is None or d < best else best
    return best


# ---------------------------------------------------------------- gate 3: the grazers' pose pairs

def grazer_ids(run, end):
    """grazer-motion-read.py's kinds(), grazers only: born mouthed (atk 1 and ink 1), born at or before `end`."""
    grazers = set()
    for line in open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8'):
        if not line.strip():
            continue
        try:
            r = json.loads(line)
        except ValueError:
            continue
        if r.get('e') != 'b':
            continue
        if r.get('t', 0) > end:
            break
        if r.get('atk') == 1 and r.get('ink') == 1:
            grazers.add(r['id'])
    return grazers


def grazer_pairs(run, grazers, end, every, gap):
    """Every pose pair as three parallel arrays (later frame's t, horizontal speed, grazer id), made as
    grazer-motion-read.py's read() makes them: frames after `end` dropped, every `every`'th kept, remains skipped,
    a pair kept when 0 < dt <= gap * max(1, every) + 1e-6. Only grazers are tracked, which leaves the pairs unchanged."""
    ts, vs, ids = array('d'), array('d'), array('i')
    try:
        path = poses.stream_path(run)
        size = os.path.getsize(path)
        with open(path, 'rb') as f:
            header = poses.read_header(f)
            version = header['version']
            frames = [fr for fr in poses.scan(f, size, version) if fr[0] <= end]
            frames = frames[::max(1, every)] if every > 1 else frames
            prev_t, prev = None, {}
            for seconds, offset, payload in frames:
                _, bodies = poses.read_frame(f, offset, payload, version)
                now = {}
                for b in bodies:
                    if (b['flags'] or 0) & 8:
                        continue
                    if b['id'] in grazers:
                        now[b['id']] = b['p']
                if prev_t is not None and 0 < seconds - prev_t <= gap * max(1, every) + 1e-6:
                    dt = seconds - prev_t
                    for ident, p in now.items():
                        if ident in prev:
                            q0 = prev[ident]
                            ts.append(seconds)
                            vs.append(math.hypot(p[0] - q0[0], p[2] - q0[2]) / dt)
                            ids.append(ident)
                prev_t, prev = seconds, now
    except poses.Refusal as e:
        fail('%s: %s' % (run, e))
    return ts, vs, ids


def moving(ts, vs, ids, still, t0, t1):
    """The indices of the moving pairs: speed at least `still`, later frame's t from t0 to t1 inclusive."""
    return [i for i in range(len(vs)) if vs[i] >= still and t0 <= ts[i] <= t1]


# ---------------------------------------------------------------- pace

def stats_rows(run):
    """(t, wallTotalMs) for each row of stats.jsonl that has both; a torn last line is skipped."""
    rows = []
    try:
        with open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') as f:
            for line in f:
                try:
                    r = json.loads(line)
                except ValueError:
                    continue
                if 't' in r and 'wallTotalMs' in r:
                    rows.append((float(r['t']), float(r['wallTotalMs'])))
    except FileNotFoundError:
        pass
    return rows


def pace(rows, t0, t1):
    """pace-check.py's pace(): world seconds per wall second across the rows from t0 to t1, None if too few."""
    span = [r for r in rows if t0 <= r[0] <= t1]
    if len(span) < 2 or span[-1][1] <= span[0][1]:
        return None
    return (span[-1][0] - span[0][0]) / ((span[-1][1] - span[0][1]) / 1000.0)


def pstr(x):
    return 'n/a' if x is None else '%.3f' % x


def ratio(a, b):
    if a is None or b is None or b == 0:
        return None
    return a / b


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--on', required=True, help='the world with the family and the shaped added mass: run or arm folder')
    ap.add_argument('--off', required=True, help='the same seed with both off: run or arm folder')
    ap.add_argument('--at', type=float, default=24000.0, help='seconds; the snapshot nearest this (default 24000)')
    ap.add_argument('--from', dest='t_from', type=float, default=12000.0, help='moving pairs from this later-frame t')
    ap.add_argument('--to', dest='t_to', type=float, default=24000.0, help='moving pairs up to this t; frames after it are dropped')
    ap.add_argument('--still', type=float, default=0.005, help='metres per world second below which a pair is still')
    ap.add_argument('--gap', type=float, default=10.0, help="the run's pose cadence in world seconds")
    ap.add_argument('--every', type=int, default=1, help='read every Nth frame, to keep a long run light')
    a = ap.parse_args()

    on_run, off_run = resolve(a.on), resolve(a.off)
    print('G5 read: shape screen, one seed')
    print('ON  run: %s' % on_run)
    print('OFF run: %s' % off_run)
    print('snapshot at %g s; moving pairs from %g to %g s; still bar %g m/s; gap %g s; every %d' % (
        a.at, a.t_from, a.t_to, a.still, a.gap, a.every))

    # ---- gate 2
    print('\n== Gate 2: evolution uses the shapes (ON, snapshot nearest %g s)' % a.at)
    on = sitting(on_run, a.at)
    off = sitting(off_run, a.at)
    print('ON  snapshot at %d s' % on['t'])
    print('ON  living bodies: %d (genomes read: %d)' % (on['alive'], on['genomes']))
    print('ON  jointed bodies: %d' % on['jointed'])
    print('ON  family-jointed bodies: %d' % on['family'])
    share = on['family'] / on['jointed'] if on['jointed'] else float('nan')
    print('ON  family share, family-jointed over jointed: %s' % fnum(share))
    print('OFF snapshot at %d s (sanity)' % off['t'])
    print('OFF living bodies: %d (genomes read: %d)' % (off['alive'], off['genomes']))
    print('OFF jointed bodies: %d' % off['jointed'])
    print('OFF family-jointed bodies: %d (should be 0)' % off['family'])
    if on['alive'] != on['genomes'] or off['alive'] != off['genomes']:
        print('note: some living bodies have no birth row or genome, so shares are over the genomes read')

    print('\n== Reported (ON): the family among nodes, and the form walk')
    nshare = on['flimbs'] / on['jnodes'] if on['jnodes'] else float('nan')
    print('ON  jointed non-root nodes: %d, of them family limbs: %d' % (on['jnodes'], on['flimbs']))
    print('ON  family share of jointed non-root nodes: %s' % fnum(nshare))
    for label, rows in (('family limbs', on['walk_limbs']), ('every grown family node', on['walk_all'])):
        print('ON  form walk, %s: n %d, median %s, p90 %s (distance from the nearest entry member, in range log widths)' % (
            label, len(rows), fnum(q(rows, .5), '%.3f'), fnum(q(rows, .9), '%.3f')))
    if on['no_form']:
        print('ON  family limbs with no readable form: %d' % on['no_form'])

    # ---- gate 3
    print('\n== Gate 3: swimming does not suffer (grazer pose pairs)')
    grazers = {}
    pairs = {}
    for label, run in (('ON', on_run), ('OFF', off_run)):
        grazers[label] = grazer_ids(run, a.t_to)
        pairs[label] = grazer_pairs(run, grazers[label], a.t_to, a.every, a.gap)
    med = {}
    sel = {}
    for label in ('ON', 'OFF'):
        ts, vs, ids = pairs[label]
        sel[label] = moving(ts, vs, ids, a.still, a.t_from, a.t_to)
        mv = [vs[i] for i in sel[label]]
        allv = [vs[i] for i in range(len(vs)) if a.t_from <= ts[i] <= a.t_to]
        med[label] = q(mv, .5)
        print('%-3s grazers born by %g s: %d; pose pairs up to %g s: %d, in the window: %d' % (
            label, a.t_to, len(grazers[label]), a.t_to, len(vs), len(allv)))
        print('%-3s moving pairs: %d, median speed %s m/s' % (label, len(mv), fnum(med[label])))
        print('%-3s median speed over all pairs in the window, moving or not: %s m/s (n %d)' % (
            label, fnum(q(allv, .5)), len(allv)))
        print('%-3s median speed over all pairs up to %g s, any t: %s m/s (n %d)' % (
            label, a.t_to, fnum(q(vs, .5)), len(vs)))
    r3 = ratio(med['ON'], med['OFF']) if med['ON'] == med['ON'] and med['OFF'] == med['OFF'] else None
    print('moving-pair median speed ratio ON/OFF: %s' % ('n/a' if r3 is None else '%.3f' % r3))

    # ---- carriers
    print('\n== Reported (ON): grazers carrying a family limb against the rest (moving pairs)')
    ts, vs, ids = pairs['ON']
    wanted = {ids[i] for i in sel['ON']}
    genomes = gq.load_genomes(on_run, wanted)
    carriers = {gid for gid, g in genomes.items() if family_limbs(g)}
    carry, rest, missing = [], [], 0
    for i in sel['ON']:
        gid = ids[i]
        if gid not in genomes:
            missing += 1
        elif gid in carriers:
            carry.append(vs[i])
        else:
            rest.append(vs[i])
    print('ON  moving pairs of carriers: %d, median speed %s m/s (%d grazers)' % (
        len(carry), fnum(q(carry, .5)), len(carriers)))
    print('ON  moving pairs of the rest: %d, median speed %s m/s (%d grazers)' % (
        len(rest), fnum(q(rest, .5)), len(genomes) - len(carriers)))
    if missing:
        print('ON  moving pairs whose grazer has no genome row: %d (left out of both groups)' % missing)

    # ---- pace
    print('\n== Reported: pace, world seconds per wall second (different sittings: read with caution)')
    pr = {}
    for label, run in (('ON', on_run), ('OFF', off_run)):
        rows = stats_rows(run)
        whole = pace(rows, 0.0, a.t_to)
        window = pace(rows, a.t_from, a.t_to)
        pr[label] = (whole, window)
        print('%-3s pace up to %g s: %s; in %g to %g s: %s (%d stats rows)' % (
            label, a.t_to, pstr(whole), a.t_from, a.t_to, pstr(window), len(rows)))
    rw, rwin = ratio(pr['ON'][0], pr['OFF'][0]), ratio(pr['ON'][1], pr['OFF'][1])
    print('pace ratio ON/OFF, whole span: %s; window: %s' % (
        'n/a' if rw is None else '%.3f' % rw, 'n/a' if rwin is None else '%.3f' % rwin))

    # ---- verdicts
    g2 = share == share and share >= 0.10
    g3 = r3 is not None and r3 >= 0.80
    print('\n== Gates')
    print('Gate 2 (family share of jointed bodies >= 0.10): %s (share %s)' % ('PASS' if g2 else 'FAIL', fnum(share)))
    print('Gate 3 (moving-grazer median speed ON/OFF >= 0.80): %s (ratio %s)' % (
        'PASS' if g3 else 'FAIL', 'n/a' if r3 is None else '%.3f' % r3))
    return 0 if g2 and g3 else 1


if __name__ == '__main__':
    try:
        sys.exit(main())
    except BrokenPipeError:
        sys.exit(0)
