#!/usr/bin/env python
"""Fat step 0: how many bodies a reserve cap would bind, read from reserve-census TSVs.

Usage: python -I scripts/reads/fat/step0-bind.py <census.tsv> [<census.tsv> ...]

Each file is the output of `Evosim.Farm --reserve-census` (src/Evosim.Farm/ReserveCensus.cs).
For each file, second and guild it prints the count, the median, p90, p99 and max of
seconds_of_reserve, and the share of bodies whose reserve is above each cap, in two forms:

  plain   energy_j > s * standing_w
  D247    energy_j > P + R + s * standing_w
            P = litter_price_j, R = adult_tissue_j - tissue_j

at s = 600, 1000, 2000 and 3000 seconds. A gestating body's cap binds its reserve (energy_j)
alone, never gestation_j. The guilds are positions-read.py's: leaf, stom, mixo, jnt, plain.

Stdlib only. Exit 2 on a missing or malformed file.
"""
import csv
import math
import sys

COLUMNS = [
    'second', 'creature', 'absorptive', 'jointed', 'photosynthetic', 'gestating',
    'energy_j', 'gestation_j', 'tissue_j', 'adult_tissue_j', 'standing_w', 'seconds_of_reserve',
    'volume_m3', 'litter_price_j', 'breeding_gate_j',
]
GUILDS = ['leaf', 'stom', 'mixo', 'jnt', 'plain']
CAPS = [600, 1000, 2000, 3000]
FLOATS = COLUMNS[6:]


def fail(message):
    sys.stderr.write('step0-bind: ' + message + '\n')
    sys.exit(2)


def guild_of(absorptive, jointed, photosynthetic):
    """positions-read.py's guild_of, on the three flags."""
    if absorptive and photosynthetic:
        return 'mixo'
    if absorptive:
        return 'stom'
    if photosynthetic:
        return 'leaf'
    if jointed:
        return 'jnt'
    return 'plain'


def read_census(path):
    try:
        handle = open(path, 'r', encoding='utf-8', newline='')
    except OSError as e:
        fail('cannot open ' + path + ': ' + str(e))

    with handle:
        reader = csv.reader(handle, delimiter='\t')
        header = next(reader, None)
        if header != COLUMNS:
            fail(path + ': the header is not the census\'s columns.')

        rows = []
        for n, cells in enumerate(reader, start=2):
            if len(cells) != len(COLUMNS):
                fail('%s line %d: %d cells, expected %d.' % (path, n, len(cells), len(COLUMNS)))
            try:
                row = {
                    'second': float(cells[0]),
                    'creature': cells[1],
                    'gestating': cells[5] == '1',
                }
                flags = [cells[2] == '1', cells[3] == '1', cells[4] == '1']
                for bit in cells[2:6]:
                    if bit not in ('0', '1'):
                        raise ValueError('flag ' + bit)
                for name, cell in zip(FLOATS, cells[6:]):
                    row[name] = float(cell)
                    if math.isnan(row[name]):
                        raise ValueError(name + ' is NaN')
            except ValueError as e:
                fail('%s line %d: %s' % (path, n, e))
            row['guild'] = guild_of(*flags)
            rows.append(row)
        return rows


def quantile(sorted_values, q):
    """Nearest-rank quantile of an ascending list."""
    if not sorted_values:
        return float('nan')
    rank = max(1, math.ceil(q * len(sorted_values)))
    return sorted_values[rank - 1]


def fmt(value):
    if math.isinf(value):
        return 'inf'
    if math.isnan(value):
        return 'nan'
    return '%.0f' % value if abs(value) >= 100 else '%.2f' % value


def pct(count, total):
    return '%5.1f%%' % (100.0 * count / total) if total else '    - '


def report(path, rows):
    print('== ' + path)
    print('%d rows' % len(rows))

    for second in sorted({r['second'] for r in rows}):
        at = [r for r in rows if r['second'] == second]
        print()
        print('second %g' % second)
        head = '%-6s %6s %5s %9s %9s %9s %9s' % ('guild', 'count', 'gest', 'med s', 'p90 s', 'p99 s', 'max s')
        head += ''.join('  plain%-5d' % s for s in CAPS)
        head += ''.join('  D247 %-5d' % s for s in CAPS)
        print(head)

        for label in GUILDS + ['all']:
            group = at if label == 'all' else [r for r in at if r['guild'] == label]
            if not group:
                print('%-6s %6d' % (label, 0))
                continue

            reserve = sorted(r['seconds_of_reserve'] for r in group)
            line = '%-6s %6d %5d %9s %9s %9s %9s' % (
                label, len(group), sum(1 for r in group if r['gestating']),
                fmt(quantile(reserve, 0.5)), fmt(quantile(reserve, 0.9)),
                fmt(quantile(reserve, 0.99)), fmt(reserve[-1]))

            plain = []
            d247 = []
            for s in CAPS:
                plain.append(sum(1 for r in group if r['energy_j'] > s * r['standing_w']))
                d247.append(sum(
                    1 for r in group
                    if r['energy_j'] > r['litter_price_j'] + (r['adult_tissue_j'] - r['tissue_j'])
                    + s * r['standing_w']))

            line += ''.join('  %-10s' % pct(c, len(group)) for c in plain)
            line += ''.join('  %-10s' % pct(c, len(group)) for c in d247)
            print(line)

        negative = sum(1 for r in at if r['adult_tissue_j'] < r['tissue_j'])
        infinite = sum(1 for r in at if math.isinf(r['seconds_of_reserve']))
        if negative or infinite:
            print('(%d bodies with adult tissue under tissue, R negative and used as it stands; '
                  '%d with no standing cost, seconds_of_reserve inf)' % (negative, infinite))
    print()


def main(argv):
    if len(argv) < 2:
        fail('usage: step0-bind.py <census.tsv> [<census.tsv> ...]')
    for path in argv[1:]:
        report(path, read_census(path))


if __name__ == '__main__':
    main(sys.argv)
