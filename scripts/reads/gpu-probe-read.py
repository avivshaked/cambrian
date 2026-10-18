"""Where the card's own time goes, from one card run with the card probe on (scripts/card/card-run.ps1 -Probe, which
sets EVOSIM_GPU_PROBE=1): the runner's prints on the farm's stderr (scratch/logs/<arm>.err), averaged over the run.

The runner joins the card after each part and prints every 20 blocks (card-next-r57.md, part 3):
  - ms a step for the water and fluid passes, the links' contacts, the grid and its five parts, each body class
    and the tail, which sum to the card's time a physics step while joined;
  - ms a block for the host's own parts (slots, packs, block packs, snow and instants, up, down, mirror), and
    the scent grid's copy and upload inside them;
  - once, at the first print, each kernel's group, groups launched and the groups the SMs hold, its registers,
    and an empty launch's cost joined and queued;
  - ms a step for the water pass and the fluid pass of each class apart, each launch joined, when the classes
    share a stream (from card-r57 after 36b6f2c2);
  - with a GPU_PHASE_PROBE build (-p:GpuPhaseProbe=1 on Evosim.Farm.Gpu), kilocycles a step in ten phases for
    each class's mean body and slowest body, and from card-r57 after 36b6f2c2 the water pass's three parts and
    the fluid pass's four for each class's mean link and slowest link in each pass;
  - from card-r57 after 9460bb64 (W0, card-next-r57.md Part 8): the copies a block in the up and down
    sections and the rest of the block, the bytes they moved and the bytes in used slots, each class's capacity
    against its used slots; once, how many host arrays are page-locked; and at the run's end, on a line of the
    farm's own, the writers' clock split into poses, steering, rows, snapshots and checkpoints.
The first print is left out of the means, since it carries the first blocks' warm-up. The card's ms a step
times the physics steps a simulated second (from the run's config.json) is set against the stepping wall per
simulated second (the growth of the stats rows' wallTotalMs, as reads/pace-calc.py takes it). The joins slow
the run a little, so the shares are of a probed run's wall.

The slowest body's kilocycles are turned into ms at --mhz, the SM clock (2520 MHz, the 4090's boost, by
default; a guess unless the run read the clock). A class whose ms a step is close to its slowest body's time
waits on that body; one far above it is bound by its number of bodies.

    python -I scripts/reads/gpu-probe-read.py <runs root> <arm> <err log> [--mhz 2520]
"""
import glob
import json
import os
import re
import statistics
import sys

NUM = r'([0-9.]+)'
MAIN = re.compile(r'^gpu-probe(?: concurrent \([^)]*\))? t=(\d+) s, ms a step: grid ' + NUM +
                  r', classes ' + NUM + ' / ' + NUM + ' / ' + NUM + ' / ' + NUM + r', tail ' + NUM +
                  r'; bodies a class (\d+) / (\d+) / (\d+) / (\d+);')
GRID = re.compile(r'^gpu-probe grid parts, ms a step: zero ' + NUM + ', mean ' + NUM + ', ranges ' + NUM +
                  ', scan ' + NUM + ', scatter ' + NUM + r'; rows (\d+), buckets (\d+); the water and fluid passes '
                  r'before them ' + NUM + r"; the links' contacts before each class[^0-9]*" + NUM)
HOST = re.compile(r'^gpu-probe host, ms a block: slots ' + NUM + ', packs ' + NUM + ', block packs ' + NUM +
                  ', snow and instants ' + NUM + ', up ' + NUM + '; down ' + NUM + ', mirror ' + NUM +
                  r"(?:; the scent's copy " + NUM + r' \(in snow and instants\) and upload ' + NUM +
                  r' \(in up\), ' + NUM + ' MB)?')
PHASES = re.compile(r'^gpu-probe phases, class (\d), kilocycles a step: the mean body ' + NUM + r' \((.*?)\); '
                    r'the slowest body, up to (\d+) links, ' + NUM + r' \((.*)\)$')
PASSES = re.compile(r'^gpu-probe passes, ms a step, each launch joined: water a class ' +
                    ' / '.join([NUM] * 4) + '; fluid a class ' + ' / '.join([NUM] * 4) + '$')
LINKS = re.compile(r'^gpu-probe link phases, class (\d), kilocycles a step over ' + NUM + r' links a block: (.*)$')
LINK_PART = re.compile(r"the (mean|slowest) link's (water|fluid) " + NUM + r' \((.*?)\)')
# Two spellings: card-r57 a664761d, every copy waiting; and from W1a, each section's waiting copies counted.
WAITING = r'(?: \(' + NUM + r' waiting\))?'
COPIES = re.compile(r'^gpu-probe copies, a block(?:, each one waiting for its stream)?: up ' + NUM + ' copies' + WAITING +
                    ', ' + NUM + ' MB moved, ' + NUM + ' MB in used slots; down ' + NUM + ' copies' + WAITING + ', ' +
                    NUM + ' MB moved, ' + NUM + ' MB in used slots; the rest of the block ' + NUM + ' copies' +
                    WAITING + ', ' + NUM + r' MB; capacity against used a class (.*); slots (\d+)/(\d+)$')
WRITERS = re.compile(r'^writers-probe, ms a simulated second over ' + NUM + ' s: poses ' + NUM + ', steering ' + NUM +
                     ', rows ' + NUM + ', snapshots ' + NUM + ', checkpoints ' + NUM + r'; calls (.*); ' +
                     r"the writers' clock (\d+) ms$")
ITEM = re.compile(r'([a-z]+) ' + NUM)
HOST_NAMES = ['slots', 'packs', 'block packs', 'snow and instants', 'up', 'down', 'mirror']


def find_key(node, key):
    if isinstance(node, dict):
        if key in node:
            return node[key]
        for v in node.values():
            got = find_key(v, key)
            if got is not None:
                return got
    elif isinstance(node, list):
        for v in node:
            got = find_key(v, key)
            if got is not None:
                return got
    return None


def mean(rows):
    """Column means of a list of equal-length tuples of floats."""
    return [statistics.fmean(c) for c in zip(*rows)] if rows else []


def main():
    args = sys.argv[1:]
    mhz = 2520.0
    if '--mhz' in args:
        at = args.index('--mhz')
        mhz = float(args[at + 1])
        del args[at:at + 2]
    if len(args) != 3:
        raise SystemExit(__doc__)
    root, arm, err = args

    runs = sorted(glob.glob(os.path.join(root, arm, '*', 'run.json')), key=os.path.getmtime)
    if not runs:
        raise SystemExit(f'{arm}: no run.json under {root}')
    run_dir = os.path.dirname(runs[-1])
    j = json.load(open(runs[-1], encoding='utf-8'))
    config = json.load(open(os.path.join(run_dir, 'config.json'), encoding='utf-8'))
    dt = find_key(config, 'physicsStepSeconds')
    if not dt:
        raise SystemExit(f'{arm}: no physicsStepSeconds in config.json')
    per_second = 1.0 / float(dt)
    rows = [json.loads(line) for line in open(os.path.join(run_dir, 'stats.jsonl'), encoding='utf-8')]
    secs = rows[-1]['t'] - rows[0]['t']
    wall = (rows[-1]['wallTotalMs'] - rows[0]['wallTotalMs']) / secs
    print(f"{arm}: engine {j['engine']}, transport {j.get('transport')}, {j['status']} ({j['reason']}); "
          f"{secs:.0f} s over the stats rows; stepping wall {wall:.1f} ms per simulated second (pace {1000 / wall:.3f}); "
          f"{per_second:.0f} physics steps a simulated second")

    every = [l.rstrip('\n') for l in open(err, encoding='utf-8', errors='replace')]
    lines = [l for l in every if l.startswith('gpu-probe')]
    writers = [m for m in (WRITERS.match(l) for l in every) if m]
    main_rows, grid_rows, host_rows, scent_rows, pass_rows, copy_rows = [], [], [], [], [], []
    last_caps = None
    phases = {}
    link_phases = {}
    prints = 0
    for l in lines:
        m = MAIN.match(l)
        if m:
            prints += 1
            if prints > 1:
                main_rows.append(tuple(float(x) for x in m.groups()[1:7]) + tuple(float(x) for x in m.groups()[7:11]))
            continue
        if prints <= 1:
            continue
        m = GRID.match(l)
        if m:
            g = m.groups()
            grid_rows.append(tuple(float(g[k]) for k in (0, 1, 2, 3, 4, 7, 8)))
            continue
        m = HOST.match(l)
        if m:
            g = m.groups()
            host_rows.append(tuple(float(x) for x in g[:7]))
            if g[7] is not None:
                scent_rows.append(tuple(float(x) for x in g[7:10]))
            continue
        m = PASSES.match(l)
        if m:
            pass_rows.append(tuple(float(x) for x in m.groups()))
            continue
        m = COPIES.match(l)
        if m:
            g = m.groups()
            # The first spelling counted no waits: every copy then waited.
            waits = [float(g[k]) if g[k] is not None else float(g[k - 1]) for k in (1, 5, 9)]
            copy_rows.append(tuple(float(g[k]) for k in (0, 2, 3, 4, 6, 7, 8, 10)) + tuple(waits))
            last_caps = (g[11], g[12], g[13])
            continue
        m = LINKS.match(l)
        if m:
            c = int(m.group(1))
            parts = {(who, which): (float(total), [(n, float(v)) for n, v in ITEM.findall(items)])
                     for who, which, total, items in LINK_PART.findall(m.group(3))}
            link_phases.setdefault(c, []).append((float(m.group(2)), parts))
            continue
        m = PHASES.match(l)
        if m:
            c = int(m.group(1))
            mean_items = [(n, float(v)) for n, v in ITEM.findall(m.group(3))]
            slow_items = [(n, float(v)) for n, v in ITEM.findall(m.group(6))]
            phases.setdefault(c, []).append((float(m.group(2)), float(m.group(5)), int(m.group(4)), mean_items, slow_items))

    once = [l for l in lines if l.startswith('gpu-probe card:') or l.startswith('gpu-probe kernel ') or
            l.startswith('gpu-probe launches:') or l.startswith('gpu-probe host arrays page-locked:')]
    if once:
        print('\nthe kernels, once')
        for l in once:
            print('  ' + l[len('gpu-probe '):])

    if not main_rows:
        print(f'\nno probe prints after the first ({prints} in all): nothing to average')
        return
    mr = mean(main_rows)
    gr = mean(grid_rows)
    grid, c0, c1, c2, c3, tail = mr[:6]
    bodies = mr[6:10]
    water, contacts = (gr[5], gr[6]) if gr else (0.0, 0.0)
    card = water + grid + c0 + c1 + c2 + c3 + tail + contacts
    print(f'\nthe card a physics step, joined, over {len(main_rows)} prints (ms a step; ms a simulated second; share of the stepping wall)')

    def row(name, ms):
        print(f'  {name:<26} {ms:7.3f}  {ms * per_second:8.1f}  {100 * ms * per_second / wall:5.1f}%')
    row('water and fluid', water)
    if pass_rows:
        pr = mean(pass_rows)
        for c in range(4):
            row(f'  water, class {c}', pr[c])
        for c in range(4):
            row(f'  fluid, class {c}', pr[4 + c])
    row("links' contacts", contacts)
    row('grid', grid)
    if gr:
        for name, v in zip(['zero', 'mean', 'ranges', 'scan', 'scatter'], gr[:5]):
            row('  ' + name, v)
    for c, v in enumerate((c0, c1, c2, c3)):
        row(f'class {c} ({bodies[c]:.0f} bodies)', v)
    row('tail', tail)
    row('the card in all', card)
    idle = wall - card * per_second
    print(f'  the rest of the wall, the card idle while the host works: {idle:.1f} ms a simulated second, '
          f'{100 * idle / wall:.1f}%')

    if host_rows:
        hr = mean(host_rows)
        print('\nthe host a block (ms a block, mean)')
        for name, v in zip(HOST_NAMES, hr):
            print(f'  {name:<20} {v:8.2f}')
        if scent_rows:
            sr = mean(scent_rows)
            print(f"  the scent grid's copy {sr[0]:.2f} (in snow and instants), its upload {sr[1]:.2f} (in up), "
                  f"{sr[2]:.1f} MB a block")
        if copy_rows:
            cr = mean(copy_rows)
            print('\nthe copies a block (mean)')
            for name, n, mb, used, ms, w in (('up', cr[0], cr[1], cr[2], hr[4], cr[8]),
                                             ('down', cr[3], cr[4], cr[5], hr[5], cr[9])):
                rate = mb / 1024 / (ms / 1000) if ms > 0 else 0.0
                each = ms * 1000 / n if n > 0 else 0.0
                print(f'  {name:<5} {n:6.1f} copies ({w:.1f} waiting), {mb:7.1f} MB moved, {used:7.1f} MB of it in used '
                      f'slots ({100 * used / max(1e-9, mb):.0f}%); {ms:.2f} ms, {rate:.1f} GB/s, {each:.1f} us a copy')
            print(f'  the rest of the block {cr[6]:.1f} copies ({cr[10]:.1f} waiting), {cr[7]:.1f} MB')
            caps = ', '.join(f'class {c} {pair}' for c, pair in enumerate(last_caps[0].split(' / ')))
            print(f'  capacity/used at the last print: {caps}; slots {last_caps[1]}/{last_caps[2]}')

    for w in writers:
        g = w.groups()
        print(f'\nthe writers, ms a simulated second over {g[0]} s: poses {g[1]}, steering {g[2]}, rows {g[3]}, '
              f"snapshots {g[4]}, checkpoints {g[5]}; calls (in that order) {g[6]}; the writers' clock {g[7]} ms")

    # From card-r57 after 7eed0c11 (W3-0): the checkpoint and the row split, and the finiteness check's brackets.
    for l in every:
        if not l.startswith('farm-probe, '):
            continue
        head, _, body = l.partition(': ')
        print('\n' + head[len('farm-probe, '):])
        for part in body.split('; '):
            print('  ' + part)

    if phases:
        print(f'\nthe bodies on the card, kilocycles a step and ms at {mhz:.0f} MHz (a guess unless the clock was read)')
        for c in sorted(phases):
            got = phases[c][1:] or phases[c]
            m_kc = statistics.fmean(p[0] for p in got)
            s_kc = statistics.fmean(p[1] for p in got)
            links = max(p[2] for p in got)
            class_ms = mr[1 + c]
            print(f'  class {c}: mean body {m_kc:.1f} kc ({m_kc / mhz:.3f} ms); slowest, up to {links} links, '
                  f'{s_kc:.1f} kc ({s_kc / mhz:.3f} ms); the class {class_ms:.3f} ms a step, '
                  f'{class_ms / max(1e-9, s_kc / mhz):.2f} times its slowest body')
            names = [n for n, _ in got[0][4]]
            slow = [statistics.fmean(dict(p[4]).get(n, 0.0) for p in got) for n in names]
            mean_ph = [statistics.fmean(dict(p[3]).get(n, 0.0) for p in got) for n in names]
            print('    mean body: ' + ', '.join(f'{n} {v:.1f}' for n, v in zip(names, mean_ph)))
            print('    slowest:   ' + ', '.join(f'{n} {v:.1f}' for n, v in zip(names, slow)))

    if link_phases:
        print(f'\nthe links in the water and fluid passes, kilocycles a step and ms at {mhz:.0f} MHz')
        for c in sorted(link_phases):
            got = link_phases[c][1:] or link_phases[c]
            links = statistics.fmean(p[0] for p in got)
            print(f'  class {c}, {links:.0f} links a block:')
            for which in ('water', 'fluid'):
                for who in ('mean', 'slowest'):
                    have = [p[1][(who, which)] for p in got if (who, which) in p[1]]
                    if not have:
                        continue
                    total = statistics.fmean(h[0] for h in have)
                    names = [n for n, _ in have[0][1]]
                    vals = [statistics.fmean(dict(h[1]).get(n, 0.0) for h in have) for n in names]
                    print(f'    {which}, {who + " link:":<13} {total:7.2f} kc ({total / mhz * 1000:6.2f} us): ' +
                          ', '.join(f'{n} {v:.2f}' for n, v in zip(names, vals)))


if __name__ == '__main__':
    main()
