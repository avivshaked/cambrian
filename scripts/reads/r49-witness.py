"""Round 49's two lineage instruments, read from one run: a founder's food at landing, and the
gestation account a body died holding.

The landing readings (logbook/0120, "The placing rule failed its own check"). A founder's birth
row carries, from round 49's build, the edible density of the food its body eats at the point it
was admitted at, read before it has fed once: `fsnow` for the snow (a body with an absorptive
part) and `fmat` for the dissolved matter (one with a photosynthetic part), each beside its
column's mean over the column's live water (`fcol`, `fmcol`). The depth rule (D122) sets a
founder in its column's richest cell, so at landing it claims fsnow >= fcol. This prints each
snow-eating founder's two readings beside its first absorptive.jsonl `densityHere`, the witness
0120 found blind (a founder empties about half its 1 m cell a step, so that row reads the refill),
and the claim's share by founder source and diet. The share is taken with a relative tolerance of
1e-6, because a column of equal cells can read its mean an ulp above its cells.

The gestation account at death (0120, "Gestation entered every seed, and selection pushed it
down"). A death row carries `ga`, the account the body died holding, when above 0, and `res`, its
reserve, when above 0 (a body that died solvent). This prints the gestating bodies' deaths (birth
row `gm` 1): how many died holding an account, the median held, and the total held at death
against what the run banked (stats.jsonl `gestatedJoules`) and what the living held at the last
sample (`gestationJoulesHeld`). The remainder, banked less held less died holding, is derived and
not measured: it is what the accounts paid for children, and anything else that draws on them.

A run recorded before the build carries none of the six fields, and the read says so rather than
printing zeros.

    python scripts/reads/r49-witness.py runs/r49-s1                 # an arm: its newest run
    python scripts/reads/r49-witness.py <run dir> --limit 0          # every founder's line
    python scripts/reads/r49-witness.py r49-s1 --runs-root <main tree>/runs

Standard library only.
"""
import argparse
import json
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import runrec  # noqa: E402

TOLERANCE = 1e-6


def resolve(path, runs_root):
    """A run directory from a run directory, an arm directory, or an arm's name under runs/."""
    if not os.path.isdir(path):
        path = os.path.join(runs_root or os.path.join(REPO, 'runs'), path)
    try:
        return runrec.run_directory(path)
    except runrec.Refusal as e:
        raise SystemExit('r49-witness: %s' % e)


def rows(path):
    """The complete JSON rows of a JSONL file, a live writer's half row dropped."""
    for line in runrec._jsonl_lines(path):
        yield line


def median(xs):
    return statistics.median(xs) if xs else None


def fmt(x, spec='%.4g'):
    return '-' if x is None else spec % x


def at_or_over(here, column):
    return here >= column or here >= column * (1.0 - TOLERANCE)


# ---------------------------------------------------------------- lineage

def read_lineage(run):
    """Founder rows, every birth's mode, and every death row, from lineage.jsonl."""
    path = os.path.join(run, 'lineage.jsonl')
    if not os.path.isfile(path):
        raise SystemExit('r49-witness: %s has no lineage.jsonl' % run)

    founders = {}
    mode = {}
    deaths = []

    for line in rows(path):
        # A kill row ("e":"k") is skipped, as every reader here skips it.
        if line.startswith('{"e":"b"'):
            r = json.loads(line)
            mode[r['id']] = r.get('gm')
            if 'src' in r:
                founders[r['id']] = r
        elif line.startswith('{"e":"d"'):
            deaths.append(json.loads(line))

    return founders, mode, deaths


def first_absorptive(run, wanted, born):
    """Each wanted id's first absorptive.jsonl row at or after its birth: id -> (t, densityHere)."""
    path = os.path.join(run, 'absorptive.jsonl')
    if not os.path.isfile(path) or not wanted:
        return {}

    out = {}
    for line in rows(path):
        k = line.find('"id":')
        if k < 0:
            continue
        end = line.find(',', k)
        try:
            i = int(line[k + 5:end])
        except ValueError:
            continue
        if i not in wanted or i in out:
            continue

        r = json.loads(line)
        if r['t'] < born[i]:
            continue
        out[i] = (r['t'], r.get('densityHere'))

    return out


def last_stats(run):
    """The last stats.jsonl row, or None."""
    path = os.path.join(run, 'stats.jsonl')
    if not os.path.isfile(path):
        return None

    last = None
    for line in rows(path):
        last = line
    return json.loads(last) if last else None


# ---------------------------------------------------------------- the landing

def diet(r):
    snow, matter = r.get('abs') == 1, r.get('pho') == 1
    return 'mixotroph' if snow and matter else 'snow' if snow else 'matter' if matter else 'none'


def landing_table(label, groups, show_first):
    head = '  %-22s %5s %7s %6s %11s %11s' % ('group', 'n', 'at/over', 'share', 'med here', 'med column')
    if show_first:
        head += ' %11s %12s %9s' % ('med first', 'first/here', 'lag s')
    print(label)
    print(head)

    for name in sorted(groups):
        g = groups[name]
        n = len(g)
        over = sum(1 for x in g if at_or_over(x['here'], x['column']))
        line = '  %-22s %5d %7d %6s %11s %11s' % (
            name, n, over, fmt(over / n if n else None, '%.3f'),
            fmt(median([x['here'] for x in g])), fmt(median([x['column'] for x in g])))
        if show_first:
            firsts = [x['first'] for x in g if x['first'] is not None]
            ratios = [x['first'] / x['here'] for x in g if x['first'] is not None and x['here'] > 0]
            lags = [x['lag'] for x in g if x['lag'] is not None]
            line += ' %11s %12s %9s' % (fmt(median(firsts)), fmt(median(ratios), '%.3f'), fmt(median(lags), '%.3g'))
        print(line)


def landing(run, founders, limit):
    snow = {i: r for i, r in founders.items() if 'fsnow' in r}
    matter = {i: r for i, r in founders.items() if 'fmat' in r}

    eaters = sum(1 for r in founders.values() if r.get('abs') == 1)
    leaves = sum(1 for r in founders.values() if r.get('pho') == 1)

    print('founders %d: %d eat the snow, %d take up dissolved matter' % (len(founders), eaters, leaves))

    if not snow and not matter:
        print('no founder row carries fsnow or fmat: a run recorded before round 49\'s landing '
              'readings, a world without a grid, or a placer that keeps no coordinates')
        return

    if eaters and len(snow) < eaters:
        print('  %d snow-eating founders carry no fsnow' % (eaters - len(snow)))

    born = {i: r['t'] for i, r in snow.items()}
    first = first_absorptive(run, set(snow), born)

    groups = {}
    lines = []
    for i in sorted(snow, key=lambda i: snow[i]['t']):
        r = snow[i]
        f = first.get(i)
        x = dict(here=r['fsnow'], column=r['fcol'],
                 first=f[1] if f else None, lag=(f[0] - r['t']) if f else None)
        kind = diet(r)
        groups.setdefault(r['src'] if kind == 'snow' else '%s (%s)' % (r['src'], kind), []).append(x)
        groups.setdefault('all', []).append(x)
        lines.append((i, r, x))

    print()
    landing_table('the snow at landing (fsnow against fcol, J/m3), and the first absorptive row', groups, True)

    shown = lines if limit == 0 else lines[:limit]
    if shown:
        print()
        print('  %8s %-8s %4s %9s %9s %9s %8s %9s %9s %9s' % (
            'id', 'src', 'pool', 't', 'fsnow', 'fcol', 'here/col', 'first', 'at t', 'first/here'))
        for i, r, x in shown:
            ratio = x['here'] / x['column'] if x['column'] > 0 else None
            back = x['first'] / x['here'] if x['first'] is not None and x['here'] > 0 else None
            print('  %8d %-8s %4s %9g %9.4g %9.4g %8s %9s %9s %9s' % (
                i, r['src'], r.get('pool', '-'), r['t'], x['here'], x['column'], fmt(ratio, '%.3g'),
                fmt(x['first']), fmt(r['t'] + x['lag'] if x['lag'] is not None else None, '%g'),
                fmt(back, '%.3g')))
        if len(shown) < len(lines):
            print('  ... %d more (--limit 0 prints every one)' % (len(lines) - len(shown)))

    if matter:
        groups = {}
        for r in matter.values():
            x = dict(here=r['fmat'], column=r['fmcol'], first=None, lag=None)
            kind = diet(r)
            groups.setdefault(r['src'] if kind == 'matter' else '%s (%s)' % (r['src'], kind), []).append(x)
            groups.setdefault('all', []).append(x)

        print()
        landing_table('the dissolved matter at landing (fmat against fmcol, units/m3)', groups, False)

    print()
    print('A mixotroph is set at the richer share of its two foods, so its reading of the other '
          'food is not the rule\'s claim.')


# ---------------------------------------------------------------- the account at death

def accounts(run, mode, deaths):
    gestating = [d for d in deaths if mode.get(d['id']) == 1]
    lump = [d for d in deaths if mode.get(d['id']) == 0]
    unknown = [d for d in deaths if d['id'] not in mode]

    carries_ga = any('ga' in d for d in deaths)
    carries_res = any('res' in d for d in deaths)

    print('deaths %d: %d gestating (birth row gm 1), %d lump, %d with no birth row read'
          % (len(deaths), len(gestating), len(lump), len(unknown)))

    held = [d['ga'] for d in gestating if d.get('ga', 0) > 0]
    total = sum(held)

    if not carries_ga:
        # Nothing to count from: a row without ga is a body that died with an empty account only
        # on a build that writes it, and the file cannot say which build wrote it.
        print('no death row carries ga: a run recorded before round 49\'s death readings, or one '
              'in which no body died holding an account; the held figures below are not printed')
    else:
        print('gestating deaths holding an account: %d of %d (%s); median held %s J; median over '
              'every gestating death %s J; total held at death %s J'
              % (len(held), len(gestating),
                 fmt(len(held) / len(gestating) if gestating else None, '%.3f'),
                 fmt(median(held)), fmt(median([d.get('ga', 0.0) for d in gestating])),
                 fmt(total, '%.6g')))

    stray = [d for d in lump if d.get('ga', 0) > 0]
    if stray:
        print('  %d lump deaths carry ga, which a lump breeder cannot hold: read the build' % len(stray))

    s = last_stats(run)
    if s is None or 'gestatedJoules' not in s:
        print('no stats.jsonl row with gestatedJoules to set the total against')
    else:
        banked = s['gestatedJoules']
        living = s.get('gestationJoulesHeld')
        print('the run banked %s J into accounts by t=%g (gestatedJoules), the living held %s J then '
              '(gestationJoulesHeld), and %d births were paid from an account (gestationBirths)'
              % (fmt(banked, '%.6g'), s['t'], fmt(living, '%.6g'), s.get('gestationBirths', 0)))
        if banked > 0 and carries_ga:
            print('  died holding / banked: %s' % fmt(total / banked, '%.4f'))
            if living is not None:
                print('  banked less held less died holding: %s J (derived, not measured; the lineage '
                      'may run a sample past the last stats row)' % fmt(banked - living - total, '%.6g'))

    if not carries_res:
        print('no death row carries res: a run recorded before the build, or one in which no body '
              'died solvent')
    else:
        print()
        print('reserve at death (res) by cause:')
        by = {}
        for d in deaths:
            by.setdefault(d['c'], []).append(d.get('res', 0.0))
        for cause in sorted(by):
            xs = by[cause]
            solvent = [x for x in xs if x > 0]
            print('  %-9s %7d deaths, %7d solvent, median res %s J'
                  % (cause, len(xs), len(solvent), fmt(median(solvent))))


def main():
    parser = argparse.ArgumentParser(
        description="Round 49's two lineage instruments: a founder's food at landing, and the "
                    'gestation account a body died holding.')
    parser.add_argument('run', help='a run directory, an arm directory, or an arm under runs/')
    parser.add_argument('--runs-root', help='where an arm name is looked up (default: <repo>/runs)')
    parser.add_argument('--limit', type=int, default=30,
                        help='founder lines to print (default 30; 0 prints every one)')
    args = parser.parse_args()

    run = resolve(args.run, args.runs_root)
    print('run %s' % run)

    founders, mode, deaths = read_lineage(run)

    print()
    print('== a founder\'s food at landing')
    landing(run, founders, args.limit)

    print()
    print('== the gestation account at death')
    accounts(run, mode, deaths)


if __name__ == '__main__':
    main()
