"""Newborn joints in the run's own pose stream: does a child's body move between its first two
recorded frames, does its parent's, and how close are they to each other and to anything else.

    python -I scripts/reads/newborn-joints-read.py IDS POSES OUT.tsv [--end 24000] [--remains]

IDS    a tab-separated file with columns id, parent, group (standstill, slow or moving); other
       columns are ignored. Its order is the order of the output.
POSES  a poses.bin (or a run directory holding one), read with scripts/poses-read.py's own
       scan() and read_frame(). The stream is walked once, in order, and left after --end seconds
       or as soon as every id has its two frames, whichever comes first.
OUT    the TSV: one row per id, in the ids file's order.

Columns: id, parent, group, t1, t2 (the child's first two recorded frames, seconds), nJoints,
dq (the change in each joint coordinate, frame 2 minus frame 1, comma-separated, in the solver's
order), maxAbsDq and rmsDq (over those coordinates), rootMove (the root's 3D displacement in
metres, x and z the short way round the torus), parentDist1 (root to root at t1, metres, nan when
the parent is not in that frame), nearestDist1 and nearestId (the nearest other body's root at t1),
nWithin1m (other bodies within 1 m of the root at t1), parentMaxAbsDq, parentRmsDq and
nearestRemains1 and remainsWithin10m (the nearest remains, flag 8, at t1, and how many lie within
10 m, the carrion scent's range in r62p-t3), parentRootMove (the parent's own change between the same t1 and t2, nan when the parent is missing
from either frame or its joint count differs between them).

Units. The joint coordinates are the stream's own: the solver's coordinates in its order, which
the state-stream spec calls "the solver's own order" and does not name a unit for. They are
presumably radians (a ball joint's three are a rotation vector, spec line 70); that is an
inference, not something the spec says. So maxAbsDq and rmsDq are in the stream's units, and every
threshold below (1e-4, 1e-2) is in those units. Distances are metres.

The world. The box is periodic in x and z. Its extent is not stored in config.json; it is derived
the way World.cs (1729-1736) and SharedVolume do: patch = sqrt(world.worldAreaSquareMetres /
horizontalPatches), x extent = patch * (horizontalPatches // patchesAcross), z extent = patch *
patchesAcross. For this run that is 300 m by 300 m. If worldShape is Tank, or a field is missing,
plain distance is used and the output says so.

Bodies that count as "other bodies" for nearestDist1 and nWithin1m are the live ones; remains
(flag 8, D220) are left out unless --remains is given. A child's siblings and its parent count.

A child with a joint count of 0 has no dq: its maxAbsDq and rmsDq are nan, and it is left out of
the dq summaries and counted apart.
"""
import argparse
import importlib.util
import json
import math
import os
import sys

NAN = float('nan')
DQ_DECIMALS = 8
LEN_DECIMALS = 6


def load_poses_reader():
    here = os.path.dirname(os.path.abspath(__file__))
    path = os.path.join(here, '..', 'poses-read.py')
    spec = importlib.util.spec_from_file_location('poses_read', os.path.normpath(path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def find_key(node, key):
    """The first value stored under key anywhere in a nested JSON document, or None."""
    if isinstance(node, dict):
        if key in node:
            return node[key]
        for v in node.values():
            found = find_key(v, key)
            if found is not None:
                return found
    elif isinstance(node, list):
        for v in node:
            found = find_key(v, key)
            if found is not None:
                return found
    return None


def torus_from_config(stream_path):
    """(lx, lz, note): the box's extents from the run's config.json, or (None, None, why)."""
    config_path = os.path.join(os.path.dirname(os.path.abspath(stream_path)), 'config.json')
    if not os.path.isfile(config_path):
        return None, None, 'no config.json beside the stream, so plain distance'

    with open(config_path, encoding='utf-8-sig') as f:
        config = json.load(f)

    shape = find_key(config, 'worldShape')
    area = find_key(config, 'worldAreaSquareMetres')
    count = find_key(config, 'horizontalPatches')
    across = find_key(config, 'patchesAcross')

    if shape == 'Tank':
        return None, None, 'worldShape is Tank (walled, no seam), so plain distance'

    if area is None or count is None or across is None:
        return None, None, 'config.json lacks worldAreaSquareMetres, horizontalPatches or patchesAcross, so plain distance'

    count = max(1, int(count))
    across = min(max(1, int(across)), count)
    patch = math.sqrt(float(area) / count)
    lx = patch * max(1, count // across)
    lz = patch * across

    return lx, lz, ('torus %.3f m (x) by %.3f m (z), from config.json worldAreaSquareMetres=%g, '
                    'horizontalPatches=%d, patchesAcross=%d (World.cs 1729-1736); worldShape=%s'
                    % (lx, lz, float(area), count, across, shape))


def read_ids(path):
    rows = []
    seen = set()
    dups = 0

    with open(path, encoding='utf-8-sig') as f:
        header = f.readline().rstrip('\r\n').split('\t')
        for need in ('id', 'parent', 'group'):
            if need not in header:
                sys.exit('%s has no %s column (it has %s)' % (path, need, ', '.join(header)))
        ci, cp, cg = header.index('id'), header.index('parent'), header.index('group')

        for line in f:
            line = line.rstrip('\r\n')
            if not line:
                continue
            cells = line.split('\t')
            ident = int(cells[ci])
            if ident in seen:
                dups += 1
                continue
            seen.add(ident)
            parent = cells[cp].strip()
            try:
                parent = int(parent)
            except ValueError:
                parent = None
            rows.append({'id': ident, 'parent': parent, 'group': cells[cg].strip()})

    return rows, dups


class Space:
    def __init__(self, lx, lz):
        self.lx = lx
        self.lz = lz

    def delta(self, a, b):
        dx = a[0] - b[0]
        dy = a[1] - b[1]
        dz = a[2] - b[2]
        if self.lx:
            dx = abs(dx) % self.lx
            dx = min(dx, self.lx - dx)
        if self.lz:
            dz = abs(dz) % self.lz
            dz = min(dz, self.lz - dz)
        return dx, dy, dz

    def dist(self, a, b):
        dx, dy, dz = self.delta(a, b)
        return math.sqrt(dx * dx + dy * dy + dz * dz)


def dq_stats(q1, q2):
    """(list of changes, max abs, rms), or ([], nan, nan) when there is nothing to compare."""
    if q1 is None or q2 is None or len(q1) != len(q2) or len(q1) == 0:
        return [], NAN, NAN
    dq = [b - a for a, b in zip(q1, q2)]
    return dq, max(abs(v) for v in dq), math.sqrt(sum(v * v for v in dq) / len(dq))


def fmt(v, decimals):
    if v is None or (isinstance(v, float) and math.isnan(v)):
        return 'nan'
    return '%.*f' % (decimals, v)


def quantile(sorted_values, p):
    n = len(sorted_values)
    if n == 0:
        return NAN
    if n == 1:
        return sorted_values[0]
    pos = p * (n - 1)
    lo = int(math.floor(pos))
    hi = min(lo + 1, n - 1)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (pos - lo)


def finite(values):
    return sorted(v for v in values if not math.isnan(v))


def sci(v):
    return 'nan' if math.isnan(v) else '%.3e' % v


def pct(k, n):
    return 'n/a' if n == 0 else '%.1f%% (%d/%d)' % (100.0 * k / n, k, n)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('ids')
    ap.add_argument('poses')
    ap.add_argument('out')
    ap.add_argument('--end', type=float, default=24000.0, help='stop after this many seconds (default 24000)')
    ap.add_argument('--remains', action='store_true', help='count remains (flag 8) as other bodies')
    args = ap.parse_args()

    pr = load_poses_reader()
    children, dups = read_ids(args.ids)
    wanted = {c['id']: c for c in children}

    try:
        path = pr.stream_path(os.path.abspath(args.poses))
    except pr.Refusal as e:
        sys.exit('refused: %s' % e)

    lx, lz, torus_note = torus_from_config(path)
    space = Space(lx, lz)

    size = os.path.getsize(path)
    state = {}      # id -> what its first frame left behind
    result = {}     # id -> its finished row (a dict)
    frames_read = 0
    last_t = None

    with open(path, 'rb') as f:
        try:
            header = pr.read_header(f)
            version = header['version']
            frames = pr.scan(f, size, version)
        except pr.Refusal as e:
            sys.exit('refused: %s' % e)

        todo = len(wanted)

        for seconds, offset, payload in frames:
            if seconds > args.end:
                break
            if len(result) >= todo:
                break

            # Is any id's first or second frame here? Decode only if some id could be in it.
            try:
                t, bodies = pr.read_frame(f, offset, payload, version)
            except pr.Refusal as e:
                sys.exit('refused: %s' % e)

            frames_read += 1
            last_t = t
            by_id = {b['id']: b for b in bodies}

            # Second frames first: an id that was seen in an earlier frame and is here again.
            for ident in list(state.keys()):
                body = by_id.get(ident)
                if body is None:
                    continue
                s = state.pop(ident)
                c = wanted[ident]
                dq, mx, rms = dq_stats(s['q1'], body['q'])
                parent2 = by_id.get(c['parent']) if c['parent'] is not None else None
                if s['parent1'] is not None and parent2 is not None:
                    pdq, pmx, prms = dq_stats(s['parent1']['q'], parent2['q'])
                    proot = space.dist(s['parent1']['p'], parent2['p'])
                else:
                    pmx = prms = proot = NAN
                result[ident] = {
                    't1': s['t1'], 't2': t, 'nJoints': len(s['q1']), 'dq': dq, 'maxAbsDq': mx, 'rmsDq': rms,
                    'rootMove': space.dist(s['p1'], body['p']),
                    'parentDist1': s['parentDist1'], 'nearestDist1': s['nearestDist1'],
                    'nearestId': s['nearestId'], 'nWithin1m': s['nWithin1m'],
                    'parentMaxAbsDq': pmx, 'parentRmsDq': prms, 'parentRootMove': proot,
                    'nearestRemains1': s['nearestRemains1'], 'remainsWithin10m': s['remainsWithin10m'],
                }

            # First frames: an id not yet seen.
            fresh = [i for i in wanted if i in by_id and i not in state and i not in result]
            if fresh:
                others = [(b['id'], b['p']) for b in bodies
                          if args.remains or not (b['flags'] and b['flags'] & 8)]
                remains = [b['p'] for b in bodies if b['flags'] and b['flags'] & 8]
                for ident in fresh:
                    body = by_id[ident]
                    c = wanted[ident]
                    nearest = math.inf
                    nearest_id = -1
                    within = 0
                    for oid, op in others:
                        if oid == ident:
                            continue
                        d = space.dist(body['p'], op)
                        if d < nearest:
                            nearest = d
                            nearest_id = oid
                        if d <= 1.0:
                            within += 1
                    parent1 = by_id.get(c['parent']) if c['parent'] is not None else None
                    rd = [space.dist(body['p'], rp) for rp in remains]
                    state[ident] = {
                        'nearestRemains1': min(rd) if rd else NAN,
                        'remainsWithin10m': sum(1 for d in rd if d <= 10.0),
                        't1': t, 'q1': list(body['q']), 'p1': body['p'],
                        'parent1': parent1,
                        'parentDist1': space.dist(body['p'], parent1['p']) if parent1 is not None else NAN,
                        'nearestDist1': nearest if nearest_id >= 0 else NAN,
                        'nearestId': nearest_id,
                        'nWithin1m': within,
                    }
                    if parent1 is not None:
                        # keep only what the parent's comparison needs
                        state[ident]['parent1'] = {'p': parent1['p'], 'q': list(parent1['q'])}

    # ------------------------------------------------------------------ the TSV
    cols = ['id', 'parent', 'group', 't1', 't2', 'nJoints', 'dq', 'maxAbsDq', 'rmsDq', 'rootMove',
            'parentDist1', 'nearestDist1', 'nearestId', 'nWithin1m',
            'parentMaxAbsDq', 'parentRmsDq', 'parentRootMove', 'nearestRemains1', 'remainsWithin10m']

    with open(args.out, 'w', encoding='utf-8', newline='') as out:
        out.write('\t'.join(cols) + '\n')
        for c in children:
            r = result.get(c['id'])
            t1 = state[c['id']]['t1'] if (r is None and c['id'] in state) else None
            if r is None:
                cells = [str(c['id']), '' if c['parent'] is None else str(c['parent']), c['group'],
                         fmt(t1 if t1 is not None else NAN, 3), 'nan', 'nan', 'nan', 'nan', 'nan', 'nan',
                         'nan', 'nan', 'nan', 'nan', 'nan', 'nan', 'nan', 'nan', 'nan']
            else:
                cells = [str(c['id']), '' if c['parent'] is None else str(c['parent']), c['group'],
                         fmt(r['t1'], 3), fmt(r['t2'], 3), str(r['nJoints']),
                         ','.join(fmt(v, DQ_DECIMALS) for v in r['dq']) if r['dq'] else 'nan',
                         fmt(r['maxAbsDq'], DQ_DECIMALS), fmt(r['rmsDq'], DQ_DECIMALS),
                         fmt(r['rootMove'], LEN_DECIMALS),
                         fmt(r['parentDist1'], LEN_DECIMALS), fmt(r['nearestDist1'], LEN_DECIMALS),
                         str(r['nearestId']), str(r['nWithin1m']),
                         fmt(r['parentMaxAbsDq'], DQ_DECIMALS), fmt(r['parentRmsDq'], DQ_DECIMALS),
                         fmt(r['parentRootMove'], LEN_DECIMALS),
                         fmt(r['nearestRemains1'], LEN_DECIMALS), str(r['remainsWithin10m'])]
            out.write('\t'.join(cells) + '\n')

    # ------------------------------------------------------------------ the summary
    print('newborn-joints-read: %d ids from %s%s' % (len(children), args.ids,
                                                     (' (%d duplicate ids dropped)' % dups) if dups else ''))
    print('stream %s: version %d, %d frames in all, %d read, last read t=%s s (--end %g)'
          % (path, version, len(frames), frames_read, 'none' if last_t is None else '%g' % last_t, args.end))
    print('world: %s' % torus_note)
    print('units: maxAbsDq and rmsDq are in the stream\'s joint-coordinate units (the solver\'s own; '
          'presumed radians, unconfirmed); distances in metres')
    print('other bodies for nearest/nWithin1m: %s' % ('live and remains' if args.remains else 'live bodies only (remains left out)'))

    never = [c['id'] for c in children if c['id'] not in result and c['id'] not in state]
    one = [c['id'] for c in children if c['id'] in state]
    print('children with one frame in range: %d (left out of the summary); with none: %d (also left out)'
          % (len(one), len(never)))

    done = [c for c in children if c['id'] in result]
    group_names = []
    for c in children:
        if c['group'] not in group_names:
            group_names.append(c['group'])
    order = [g for g in ('standstill', 'slow', 'moving') if g in group_names] + \
            [g for g in group_names if g not in ('standstill', 'slow', 'moving')]

    for g in order:
        rows = [(c, result[c['id']]) for c in done if c['group'] == g]
        total = sum(1 for c in children if c['group'] == g)
        dqrows = [r for _, r in rows if not math.isnan(r['maxAbsDq'])]
        mx = finite(r['maxAbsDq'] for r in dqrows)
        print()
        print('group %s: %d ids, %d with two frames, %d of those with joints (dq stats on these)'
              % (g, total, len(rows), len(dqrows)))
        if rows:
            print('  maxAbsDq median %s, quartiles %s .. %s'
                  % (sci(quantile(mx, 0.5)), sci(quantile(mx, 0.25)), sci(quantile(mx, 0.75))))
            print('  maxAbsDq under 1e-4: %s; under 1e-2: %s'
                  % (pct(sum(1 for v in mx if v < 1e-4), len(mx)), pct(sum(1 for v in mx if v < 1e-2), len(mx))))
            rm = finite(r['rootMove'] for _, r in rows)
            print('  rootMove median %s m' % sci(quantile(rm, 0.5)))
            pd = finite(r['parentDist1'] for _, r in rows)
            print('  parentDist1 median %s m (parent in frame 1 for %d of %d)'
                  % (sci(quantile(pd, 0.5)), len(pd), len(rows)))
            print('  nWithin1m >= 1: %s' % pct(sum(1 for _, r in rows if r['nWithin1m'] >= 1), len(rows)))
            print('  parent within 1 m: %s of those with a parent in frame 1 (%s of all with two frames)'
                  % (pct(sum(1 for v in pd if v <= 1.0), len(pd)),
                     pct(sum(1 for v in pd if v <= 1.0), len(rows))))
            nr = finite(r['nearestRemains1'] for _, r in rows)
            print('  nearestRemains1 median %s m; any remains within 10 m (the carrion scent range): %s; '
                  'median count within 10 m %s'
                  % (sci(quantile(nr, 0.5)), pct(sum(1 for _, r in rows if r['remainsWithin10m'] >= 1), len(rows)),
                     sci(quantile(sorted(float(r['remainsWithin10m']) for _, r in rows), 0.5))))
            pm = finite(r['parentMaxAbsDq'] for _, r in rows)
            print('  parentMaxAbsDq median %s; parents under 1e-4: %s (of %d with a parent compared)'
                  % (sci(quantile(pm, 0.5)), pct(sum(1 for v in pm if v < 1e-4), len(pm)), len(pm)))

    def table(title, row_label_fn, rows_in, col_label, row_labels):
        print()
        print(title)
        counts = {}
        for item in rows_in:
            counts[item] = counts.get(item, 0) + 1
        print('  %-34s %12s %12s' % ('', col_label[0], col_label[1]))
        for rl in row_labels:
            print('  %-34s %12d %12d' % (rl, counts.get((rl, 0), 0), counts.get((rl, 1), 0)))

    slow_world = [(c, result[c['id']]) for c in done
                  if c['group'] in ('standstill', 'slow') and not math.isnan(result[c['id']]['maxAbsDq'])]
    moving = [(c, result[c['id']]) for c in done
              if c['group'] == 'moving' and not math.isnan(result[c['id']]['maxAbsDq'])]
    items = [('slow in the world (standstill+slow)', 0 if r['maxAbsDq'] < 1e-4 else 1) for _, r in slow_world] + \
            [('moving', 0 if r['maxAbsDq'] < 1e-4 else 1) for _, r in moving]
    table('2x2: group against whether the child\'s joints held still (maxAbsDq < 1e-4)',
          None, items, ('still', 'moved'),
          ['slow in the world (standstill+slow)', 'moving'])

    items = []
    parent_unknown = 0
    for _, r in slow_world:
        if math.isnan(r['parentMaxAbsDq']):
            parent_unknown += 1
            continue
        child = 'child still' if r['maxAbsDq'] < 1e-4 else 'child moved'
        items.append((child, 0 if r['parentMaxAbsDq'] < 1e-4 else 1))
    table('2x2, slow in the world only: child against parent (still = maxAbsDq < 1e-4)',
          None, items, ('parent still', 'parent moved'), ['child still', 'child moved'])
    print('  (%d slow-in-the-world children left out here: parent missing from a frame or without joints)'
          % parent_unknown)

    return 0


if __name__ == '__main__':
    sys.exit(main())
