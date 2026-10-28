"""Ask a run's genomes questions without writing a one-off script.

Several rounds were read by tallying lineage flags (jnt, abs, atk) because the bodies themselves
sat in genomes.jsonl.gz and nobody wanted to write the loop that opens them. This is that loop,
four ways. It only reads: it never writes to a run, and it opens files shared, so it can look at a
run that is still being written. A half-written last line is skipped and a gzip cut mid-stream
ends the read at the last complete row (the same rule scratch/r63b-peek/plans.py followed).

Usage:

    python -I scripts/genome-query.py show <run> <id> [--json | --plain]
    python -I scripts/genome-query.py plans <run> [--at SECONDS] [--limbed] [--cell CELL] [--top N]
                                       [--json | --plain]
    python -I scripts/genome-query.py find <run> [--at SECONDS] [--attack] [--cell CELL]
                                       [--joint JOINT] [--min-nodes N] [--limit N]
    python -I scripts/genome-query.py ancestry <run> <id> [--depth N]

<run> is a run directory (the one holding genomes.jsonl.gz), or a round folder such as
runs/r63b-d-6202/ whose single stamped subfolder is the run. A bare name such as r62cf-6202 is
looked for under runs/.

--json (show, plans) prints the same facts as one JSON object per creature or plan, with no
header, for scripts. --plain prints one line in plain words, for a film's caption: it states
cells, longest extent, joint, repeats and mirroring, says "hunts" only when a grown node has
attack > 0 and "makes its own food" only for a photosynthetic node, and claims nothing else.
"mouth" is the word it uses for a consumer cell.

What the four read:

  genomes.jsonl.gz   one row per creature ever born; the row opens {"id":N, and ids ascend.
  lineage.jsonl      "b" births (t, p parent, g generation, k kind, jnt/abs/pho/atk/ink flags),
                     "d" deaths (t, c cause). Other event kinds are ignored.
  snapshots/*.jsonl.gz  the bodies alive at second N, named for N; --at picks the nearest.

A plan is a structural signature: for every node development reaches from the root (a node no
edge leads to is dormant, never grown, and is left out; show marks it NOT GROWN) its cell, shape and joint, and for every edge
its child index and whether it is mirrored (m) or terminal-only (t), and a node's repeats (xN)
when it repeats. Size, power and the brain are not part of it, so two bodies of one plan can
still differ in scale and drive.

Exits 0, or 2 on a usage error or a run it could not find.
"""
import argparse
import gzip
import json
import os
import sys
import zlib
from collections import Counter, defaultdict

GZ_ERRORS = (EOFError, OSError, zlib.error)


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


# ---------------------------------------------------------------- finding and reading files

def resolve_run(arg):
    """A run directory, or a round folder with one stamped subfolder, or a name under runs/."""
    here = os.path.dirname(os.path.abspath(__file__))
    candidates = [arg, os.path.join(os.path.dirname(here), 'runs', arg)]
    for path in candidates:
        if not os.path.isdir(path):
            continue
        if os.path.exists(os.path.join(path, 'genomes.jsonl.gz')):
            return path
        subs = sorted(d for d in os.listdir(path) if os.path.isdir(os.path.join(path, d)))
        holders = [d for d in subs if os.path.exists(os.path.join(path, d, 'genomes.jsonl.gz'))]
        if len(holders) == 1:
            return os.path.join(path, holders[0])
        if len(holders) > 1:
            fail('%s holds %d runs (%s); name one of them' % (path, len(holders), ', '.join(holders)))
    fail('no run with genomes.jsonl.gz at %s (or under runs/)' % arg)


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


def load_genomes(run, wanted):
    """{id: genome} for the wanted ids. Rows are scanned by their head and only wanted ones are
    parsed; the scan stops once every wanted id is in (rows ascend, so it also stops past the
    largest wanted id)."""
    wanted = set(wanted)
    out = {}
    if not wanted:
        return out
    top = max(wanted)
    for line in read_lines(os.path.join(run, 'genomes.jsonl.gz'), open_gz):
        gid = head_id(line)
        if gid is None:
            continue
        if gid in wanted:
            g = parse(line)
            if g is not None and g.get('id') == gid:
                out[gid] = g
                if len(out) == len(wanted):
                    break
        elif gid > top:
            break
    return out


def load_lineage(run, only=None, births=True, deaths=True):
    """(births, deaths): {id: row}. With `only`, rows of those ids alone are kept."""
    b, d = {}, {}
    prefixes = []
    if births:
        prefixes.append('{"e":"b"')
    if deaths:
        prefixes.append('{"e":"d"')
    prefixes = tuple(prefixes)
    for line in read_lines(os.path.join(run, 'lineage.jsonl'), open_text):
        if not line.startswith(prefixes):
            continue
        r = parse(line)
        if r is None or 'id' not in r:
            continue
        if only is not None and r['id'] not in only:
            continue
        (b if r['e'] == 'b' else d)[r['id']] = r
    return b, d


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
    """(seconds, set of ids) for the snapshot nearest `at` (latest when at is None)."""
    times = snapshot_times(run)
    if not times:
        fail('%s has no snapshots' % run)
    t = times[-1] if at is None else min(times, key=lambda x: (abs(x - at), x))
    path = os.path.join(run, 'snapshots', '%09d.jsonl.gz' % t)
    ids = set()
    for line in read_lines(path, open_gz):
        r = parse(line)
        if r is not None and 'id' in r:
            ids.add(r['id'])
    return t, ids


# ---------------------------------------------------------------- reading a genome

def is_mirrored(edge):
    return any((edge.get('reflect') or {}).values())


def reachable(g):
    """Indices of the nodes development can reach from the root along edges. A node nothing
    leads to is never grown: it rides in the genome as a dormant gene."""
    seen, stack = set(), [g.get('root', 0)]
    while stack:
        i = stack.pop()
        if i in seen or not 0 <= i < len(g['nodes']):
            continue
        seen.add(i)
        stack.extend(e['child'] for e in g['nodes'][i]['edges'])
    return seen


def node_count(g):
    """'4 nodes' or '6 nodes (4 grown)' when some are unreachable from the root."""
    n, r = len(g['nodes']), len(reachable(g))
    return '%d nodes' % n if n == r else '%d nodes (%d grown)' % (n, r)


def signature(g):
    """Over the nodes development reaches; a dormant node would split one body into many plans."""
    parts = []
    live = reachable(g)
    for i, n in enumerate(g['nodes']):
        if i not in live:
            continue
        edges = ','.join('%s%s%s' % (e['child'], 'm' if is_mirrored(e) else '',
                                     't' if e.get('terminalOnly') else '')
                         for e in n['edges'])
        repeats = 'x%d' % n['recursiveLimit'] if n['recursiveLimit'] > 1 else ''
        parts.append('%d:%s/%s/%s%s[%s]' % (i, n['cell'], n['shape'], n['joint'], repeats, edges))
    return ' '.join(parts)


def max_attack(g):
    live = reachable(g)
    return max((n.get('attack', 0) for i, n in enumerate(g['nodes']) if i in live), default=0)


def cells_of(g):
    live = reachable(g)
    c = Counter(n['cell'] for i, n in enumerate(g['nodes']) if i in live)
    return ', '.join(k if v == 1 else '%s x%d' % (k, v) for k, v in c.items())


def size_cm(n):
    d = n['dimensions']
    return '%.0fx%.0fx%.0f cm' % (2 * d['x'] * 100, 2 * d['y'] * 100, 2 * d['z'] * 100)


def describe(g, indent='    '):
    lines = []
    root = g.get('root', 0)
    live = reachable(g)
    for i, n in enumerate(g['nodes']):
        extra = []
        if i not in live:
            extra.append('NOT GROWN: no edge leads here from the root')
        if n['joint'] != 'Fixed':
            extra.append('joint %s power %.2f' % (n['joint'], n['power']))
        if n['recursiveLimit'] > 1:
            extra.append('repeats x%d' % n['recursiveLimit'])
        for key in ('attack', 'intake', 'protection', 'lift'):
            if n.get(key, 0) > 0:
                extra.append('%s %.2f' % (key, n[key]))
        if n['neurons']:
            extra.append('%d neurons' % len(n['neurons']))
        kids = []
        for e in n['edges']:
            tags = ('mirrored ' if is_mirrored(e) else '') + ('terminal ' if e.get('terminalOnly') else '')
            kids.append('%snode %d' % (tags, e['child']))
        lines.append('%snode %d%s: %s %s %s%s%s' % (
            indent, i, ' (root)' if i == root else '', n['cell'], n['shape'], size_cm(n),
            ', ' + ', '.join(extra) if extra else '',
            ' -> ' + ', '.join(kids) if kids else ''))
    b = g['brain']
    lines.append('%sbrain: %d hidden, %d links, %d outputs; adult scale %.2f, lifespan %.2f' % (
        indent, len(b['hidden']), len(b['links']), len(b['outputs']), g['adultScale'], g['lifespan']))
    return '\n'.join(lines)


def fmt_row(r):
    return ' '.join('%s=%s' % (k, r[k]) for k in r)


def one_line(gid, g, birth):
    root = g['nodes'][g.get('root', 0)]
    joints = Counter(n['joint'] for i, n in enumerate(g['nodes']) if i in reachable(g) and n['joint'] != 'Fixed')
    bits = ['id %d' % gid]
    if birth:
        bits.append('g%s born %ss' % (birth.get('g', '?'), birth.get('t', '?')))
    bits.append('%s: %s' % (node_count(g), cells_of(g)))
    if joints:
        bits.append('joints ' + ', '.join('%s x%d' % kv for kv in joints.items()))
    atk = max_attack(g)
    if atk > 0:
        bits.append('attack %.2f' % atk)
    if any(is_mirrored(e) for i, n in enumerate(g['nodes']) if i in reachable(g) for e in n['edges']):
        bits.append('mirrored')
    bits.append('root %s' % size_cm(root))
    return '  '.join(bits)


# ---------------------------------------------------------------- machine and plain readings

JOINT_WORDS = {'Hinge': 'hinged', 'Twist': 'twisting', 'Spherical': 'ball-jointed',
               'Universal': 'universal-jointed', 'TwistHinge': 'twist-and-hinge-jointed',
               'HingeTwist': 'hinge-and-twist-jointed'}
CELL_WORDS = {'consumer': 'mouth', 'photosynthetic': 'light-gathering', 'absorptive': 'absorbing',
              'buoyancy': 'float', 'link': 'link', 'neural': 'neural', 'structural': 'structural'}


def node_facts(g, i):
    n = g['nodes'][i]
    d = n['dimensions']
    return {
        'index': i, 'grown': i in reachable(g), 'cell': n['cell'], 'shape': n['shape'],
        'joint': n['joint'], 'power': n['power'], 'repeats': n['recursiveLimit'],
        'size_cm': [round(2 * d[k] * 100, 1) for k in 'xyz'],
        'attack': n.get('attack', 0), 'intake': n.get('intake', 0),
        'protection': n.get('protection', 0), 'lift': n.get('lift', 0),
        'neurons': len(n['neurons']),
        'children': [{'child': e['child'], 'mirrored': is_mirrored(e),
                      'terminal_only': bool(e.get('terminalOnly'))} for e in n['edges']],
    }


def genome_facts(g):
    b = g['brain']
    return {
        'format': g.get('format'), 'root': g.get('root', 0), 'signature': signature(g),
        'nodes_total': len(g['nodes']), 'nodes_grown': len(reachable(g)),
        'nodes': [node_facts(g, i) for i in range(len(g['nodes']))],
        'brain': {'hidden': len(b['hidden']), 'links': len(b['links']), 'outputs': len(b['outputs'])},
        'adult_scale': g['adultScale'], 'lifespan': g['lifespan'], 'reproduction': g['reproduction'],
        'plain': plain_line(g),
    }


def article(text):
    """'an ' before a number said with a vowel sound (8, 11, 18, 80-89, 800-899 ...), else 'a '."""
    number = text.split(' ', 1)[0]
    return 'an ' if number.startswith('8') or number in ('11', '18') else 'a '


def plain_line(g):
    """One line of plain words built from the genome's stated facts only: cell, shape, longest
    extent, joint, repeats, mirrored. 'hunts' only when a grown node has attack > 0, 'makes its
    own food' only when one is photosynthetic. No behaviour beyond that is claimed."""
    live = reachable(g)
    root = g.get('root', 0)
    mirrored_child = {e['child'] for i in live for e in g['nodes'][i]['edges'] if is_mirrored(e)}
    order = [root] + [i for i in range(len(g['nodes'])) if i in live and i != root]
    descs = []
    for i in order:
        n = g['nodes'][i]
        d = n['dimensions']
        longest = max(d['x'], d['y'], d['z']) * 2 * 100
        words = []
        if i in mirrored_child:
            words.append('mirrored')
        if n['joint'] != 'Fixed':
            words.append(JOINT_WORDS.get(n['joint'], n['joint'].lower()))
        words.append('%s %s' % (CELL_WORDS.get(n['cell'], n['cell']), n['shape']))
        text = '%.0f cm %s' % (longest, ' '.join(words))
        if n['recursiveLimit'] > 1:
            text += ' repeated x%d' % n['recursiveLimit']
        descs.append(text)
    line = article(descs[0]) + descs[0]
    if len(descs) > 1:
        line += ' with ' + ', '.join(article(x) + x for x in descs[1:])
    tail = []
    if any(g['nodes'][i].get('attack', 0) > 0 for i in live):
        tail.append('hunts')
    if any(g['nodes'][i]['cell'] == 'photosynthetic' for i in live):
        tail.append('makes its own food')
    return line + ''.join('; ' + t for t in tail)


# ---------------------------------------------------------------- subcommands

def cmd_show(args):
    run = resolve_run(args.run)
    births, deaths = load_lineage(run, only={args.id})
    gs = load_genomes(run, {args.id})
    if args.json or args.plain:
        if args.id not in gs:
            fail('no genome row for id %d in genomes.jsonl.gz' % args.id)
        if args.plain:
            print(plain_line(gs[args.id]))
        else:
            out = {'id': args.id, 'birth': births.get(args.id), 'death': deaths.get(args.id)}
            out.update(genome_facts(gs[args.id]))
            print(json.dumps(out))
        return 0
    print('run %s, creature %d' % (run, args.id))
    print('birth: ' + (fmt_row(births[args.id]) if args.id in births else 'not in lineage.jsonl'))
    print('death: ' + (fmt_row(deaths[args.id]) if args.id in deaths else 'none recorded (alive, or the run is still being written)'))
    if args.id not in gs:
        fail('no genome row for id %d in genomes.jsonl.gz' % args.id)
    g = gs[args.id]
    rep = g['reproduction']
    print('genome (format %s, %s, signature %s):' % (g.get('format'), node_count(g), signature(g)))
    print(describe(g))
    print('    reproduction: %s' % ', '.join('%s %s' % (k, ('%.3g' % v) if isinstance(v, float) else v)
                                         for k, v in rep.items()))
    return 0


def alive_genomes(run, at, limbed):
    t, alive = alive_at(run, at)
    births, _ = load_lineage(run, only=alive, deaths=False)
    ids = {i for i in alive if i in births and (not limbed or births[i].get('jnt'))}
    return t, alive, births, load_genomes(run, ids)


def has_cell(g, cell):
    live = reachable(g)
    return any(n['cell'] == cell for i, n in enumerate(g['nodes']) if i in live)


def cmd_plans(args):
    run = resolve_run(args.run)
    t, alive, births, genomes = alive_genomes(run, args.at, args.limbed)
    if args.cell:
        genomes = {i: g for i, g in genomes.items() if has_cell(g, args.cell)}
    groups = defaultdict(list)
    for gid, g in genomes.items():
        groups[signature(g)].append(gid)
    hunters = sum(1 for g in genomes.values() if max_attack(g) > 0)
    if not (args.json or args.plain):
        print('== %s at %d s: %d alive, %d matching%s%s, %d plans, %d with attack' % (
            os.path.basename(os.path.dirname(run.rstrip('/\\'))) or run, t, len(alive), len(genomes),
            ', limbed' if args.limbed else '', ', cell %s' % args.cell if args.cell else '',
            len(groups), hunters))
    for rank, (sig, ids) in enumerate(sorted(groups.items(), key=lambda kv: (-len(kv[1]), kv[0]))[:args.top], 1):
        gens = [births[i]['g'] for i in ids if 'g' in births.get(i, {})]
        grange = '%d-%d' % (min(gens), max(gens)) if gens else '?'
        ex = max(ids)
        if args.json:
            out = {'plan': rank, 'bodies': len(ids), 'generations': [min(gens), max(gens)] if gens else None,
                   'example_id': ex, 'at_seconds': t}
            out.update(genome_facts(genomes[ex]))
            print(json.dumps(out))
            continue
        if args.plain:
            print('plan %d (%d bodies, generations %s, example id %d): %s' % (
                rank, len(ids), grange, ex, plain_line(genomes[ex])))
            continue
        print('plan %d: %d bodies, generations %s, example id %d' % (rank, len(ids), grange, ex))
        print(describe(genomes[ex]))
    return 0


def cmd_find(args):
    run = resolve_run(args.run)
    t, alive, births, genomes = alive_genomes(run, args.at, False)
    joint = args.joint.lower() if args.joint else None
    n = 0
    print('== %s at %d s: %d alive, %d genomes read' % (run, t, len(alive), len(genomes)))
    for gid in sorted(genomes):
        g = genomes[gid]
        if args.attack and max_attack(g) <= 0:
            continue
        if args.cell and not has_cell(g, args.cell):
            continue
        if joint and not any(nd['joint'].lower() == joint for i, nd in enumerate(g['nodes']) if i in reachable(g)):
            continue
        if len(reachable(g)) < args.min_nodes:
            continue
        n += 1
        if n <= args.limit:
            print(one_line(gid, g, births.get(gid)))
    print('%d match%s%s' % (n, '' if n == 1 else 'es',
                            ' (%d shown)' % args.limit if n > args.limit else ''))
    return 0


def cmd_ancestry(args):
    run = resolve_run(args.run)
    births, _ = load_lineage(run, deaths=False)
    chain, cur = [], args.id
    while cur in births and len(chain) <= args.depth:
        chain.append(cur)
        cur = births[cur].get('p', -1)
        if cur < 0:
            break
    if not chain:
        fail('id %d is not a birth in lineage.jsonl' % args.id)
    gs = load_genomes(run, set(chain))
    print('ancestry of %d in %s (%d shown)' % (args.id, run, len(chain)))
    prev_sig = None
    for depth, cid in enumerate(chain):
        b = births[cid]
        g = gs.get(cid)
        if g is None:
            body = 'genome unavailable'
            sig = None
        else:
            body = '%s: %s' % (node_count(g), cells_of(g))
            sig = signature(g)
        flag = ''
        if prev_sig is not None and sig is not None and sig != prev_sig:
            flag = '   <- plan differs from its child above'
        print('%2d  id %d  born %ss  g%s  %s  %s%s' % (
            depth, cid, b.get('t', '?'), b.get('g', '?'), 'founder' if b.get('p', -1) < 0 else 'from %d' % b['p'],
            body, flag))
        prev_sig = sig if sig is not None else prev_sig
    last = births[chain[-1]]
    if last.get('p', -1) >= 0:
        if last['p'] in births:
            print('(stopped at depth %d; parent %d is next)' % (len(chain) - 1, last['p']))
        else:
            print('(parent %d has no birth row)' % last['p'])
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    sub = ap.add_subparsers(dest='cmd', required=True)

    p = sub.add_parser('show', help='one creature: birth, death, genome node by node')
    p.add_argument('run'); p.add_argument('id', type=int)
    fm = p.add_mutually_exclusive_group()
    fm.add_argument('--json', action='store_true', help='one JSON object of the facts')
    fm.add_argument('--plain', action='store_true', help='one line in plain words, facts only')
    p.set_defaults(fn=cmd_show)

    p = sub.add_parser('plans', help='living bodies grouped by structural plan')
    p.add_argument('run')
    p.add_argument('--at', type=float, default=None, help='seconds; nearest snapshot (default latest)')
    p.add_argument('--limbed', action='store_true', help='only bodies with a joint (lineage jnt=1)')
    p.add_argument('--cell', help='only bodies with a node of this cell type')
    p.add_argument('--top', type=int, default=3)
    fm = p.add_mutually_exclusive_group()
    fm.add_argument('--json', action='store_true', help='one JSON object per plan, no header')
    fm.add_argument('--plain', action='store_true', help='one plain-words line per plan, no header')
    p.set_defaults(fn=cmd_plans)

    p = sub.add_parser('find', help='living ids matching every filter')
    p.add_argument('run')
    p.add_argument('--at', type=float, default=None)
    p.add_argument('--attack', action='store_true', help='any node with attack > 0')
    p.add_argument('--cell'); p.add_argument('--joint', help='e.g. Hinge, Twist, Spherical')
    p.add_argument('--min-nodes', type=int, default=1)
    p.add_argument('--limit', type=int, default=20)
    p.set_defaults(fn=cmd_find)

    p = sub.add_parser('ancestry', help='parent links back, flagging plan changes')
    p.add_argument('run'); p.add_argument('id', type=int)
    p.add_argument('--depth', type=int, default=30)
    p.set_defaults(fn=cmd_ancestry)

    args = ap.parse_args()
    try:
        sys.exit(args.fn(args))
    except BrokenPipeError:
        sys.exit(0)


if __name__ == '__main__':
    main()
