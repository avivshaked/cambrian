"""The leaf tank's leaves, from a round 58 run (logbook/specs/nursery-leaf-tank-plan.md, step 1).

    python scripts/reads/leaf-pool.py <run> --second 44000 --out rounds/nursery/pools --tank-area 1600

Reads one run at one recorded second and writes:

- a leaf pool: one living leaf a line, its id, a tab, its height y (m, negative down), a tab, its body
  fraction as the snapshot holds it, a tab, and its genome's JSON (the genome row without its id). The
  text has LF line ends and is named by the first 16 hex digits of its SHA-256, as the nursery's other
  pools are, and is written gzipped;
- a record beside it (`<name>.json`) of how it was made and the three numbers the tank copies from the
  run: the leaves' density in the band, the count a tank of the given area holds at that density, and the
  spent matter the water held in the band.

A leaf is a living body with photosynthetic tissue and no absorptive tissue: positions-read.py's guild
`leaf`, by the flags the farm writes. The band is the leaves' interquartile range of height, which holds
half of them around their median. The band's water is the volume between the band's top and bottom
above the floor, inside the tank's circle, read column by column from fields/bed.f32. The spent matter is
the matter grid's stock in the cells the band crosses, over those cells' water volume.

Standard library only. Prints what it read and what it assumed.
"""
import argparse
import gzip
import hashlib
import json
import math
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import runrec  # noqa: E402

ABSORPTIVE = 1
PHOTOSYNTHETIC = 4


def floats(path):
    with open(path, 'rb') as f:
        data = f.read()
    return struct.unpack('<%df' % (len(data) // 4), data)


def quantile(sorted_values, q):
    if not sorted_values:
        raise SystemExit('no leaves to take a quantile of')
    at = q * (len(sorted_values) - 1)
    lo = int(math.floor(at))
    hi = min(lo + 1, len(sorted_values) - 1)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (at - lo)


def nearest_positions(run, second):
    """The positions row whose t is nearest the second, read in order and stopped once past it."""
    best = None
    for row in runrec.positions(run):
        t = row['t']
        if best is None or abs(t - second) < abs(best['t'] - second):
            best = row
        if t > second:
            break
    if best is None:
        raise SystemExit('%s holds no positions rows' % run)
    return best


def overlap(lo, hi, a, b):
    return max(0.0, min(hi, b) - max(lo, a))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('run')
    ap.add_argument('--second', type=int, required=True)
    ap.add_argument('--out', required=True, help='the folder the pool and its record are written to')
    ap.add_argument('--tank-area', type=float, required=True, help="the nursery tank's area, m^2")
    args = ap.parse_args()

    run = runrec.run_directory(args.run)
    config = json.load(open(os.path.join(run, 'config.json'), encoding='utf-8'))
    world = config['world']
    area = float(world['worldAreaSquareMetres'])
    depth = float(world['worldDepthMetres'])
    if world.get('worldShape') != 'Tank':
        raise SystemExit('the run is not a tank world; this reader knows only the tank')
    radius = math.sqrt(area / math.pi)

    # The living leaves at the second.
    row = nearest_positions(run, args.second)
    leaves = [(b[0], b[1], b[2], b[3]) for b in row['b']
              if (b[4] & PHOTOSYNTHETIC) and not (b[4] & ABSORPTIVE)]
    print('positions: t %.1f (asked %d), %d bodies, %d leaves' % (row['t'], args.second, len(row['b']), len(leaves)))

    ys = sorted(l[2] for l in leaves)
    median = quantile(ys, 0.5)
    band_lo, band_hi = quantile(ys, 0.25), quantile(ys, 0.75)
    in_band = [l for l in leaves if band_lo <= l[2] <= band_hi]
    print('leaf height: median %.3f m, band %.3f to %.3f m (%.3f m thick), %d leaves in it'
          % (median, band_lo, band_hi, band_hi - band_lo, len(in_band)))

    # The band's water, column by column.
    fields = os.path.join(run, 'fields')
    layout = json.load(open(os.path.join(fields, 'layout.json'), encoding='utf-8'))
    bed_info = layout['bed']
    bed = floats(os.path.join(fields, bed_info['file']))
    bcell = float(bed_info['cellMetres'])
    volume = 0.0
    for ix in range(bed_info['cellsX']):
        for iz in range(bed_info['cellsZ']):
            cx, cz = (ix + 0.5) * bcell, (iz + 0.5) * bcell
            if math.hypot(cx - radius, cz - radius) > radius:
                continue
            floor = bed[ix * bed_info['cellsZ'] + iz]
            volume += overlap(band_lo, band_hi, floor, 0.0) * bcell * bcell
    density = len(in_band) / volume
    band_thickness = band_hi - band_lo
    tank_count = density * args.tank_area * band_thickness
    print('band water: %.1f m^3 inside the circle above the floor; leaf density %.6f per m^3' % (volume, density))
    print('a tank of %.0f m^2 holds %.2f leaves at that density, if its floor lies below the band' % (args.tank_area, tank_count))

    # The spent matter in the band.
    m = layout['matter']
    stock = floats(os.path.join(fields, '%09d.matter.f32' % args.second))
    nx, ny, nz, mcell = m['cellsX'], m['cellsY'], m['cellsZ'], float(m['cellMetres'])
    # A layer is as thick as a cell is wide (GridField.LayerMetres), counted down from the surface, and the
    # grid reaches the floor's deepest point, which a tilted bed puts below the world's depth.
    layer = mcell
    print('matter grid: %d x %d x %d, cells %.2f m, so %d layers reach %.1f m (world depth %.1f m, deepest floor %.1f m)'
          % (nx, ny, nz, mcell, ny, ny * layer, depth, -min(bed)))
    per_layer = []
    for iy in range(ny):
        total = sum(stock[(iy * nx + ix) * nz + iz] for ix in range(nx) for iz in range(nz))
        per_layer.append(total)
    print('matter stock by layer, top down: ' + ', '.join('%.1f' % t for t in per_layer))
    held, water = 0.0, 0.0
    for iy in range(ny):
        top, bottom = -iy * layer, -(iy + 1) * layer
        share = overlap(band_lo, band_hi, bottom, top) / layer
        if share <= 0.0:
            continue
        for ix in range(nx):
            for iz in range(nz):
                cx, cz = (ix + 0.5) * mcell, (iz + 0.5) * mcell
                if math.hypot(cx - radius, cz - radius) > radius:
                    continue
                held += stock[(iy * nx + ix) * nz + iz] * share
                water += mcell * mcell * layer * share
    spent = held / water if water > 0 else float('nan')
    print('spent matter in the band: %.4f units per m^3 (%.1f units over %.1f m^3 of cells)' % (spent, held, water))

    # The pool: the band's leaves with their genomes and body fractions.
    ids = set(l[0] for l in in_band)
    snap = runrec.snapshot(run, args.second, ids=ids)
    if snap is None:
        raise SystemExit('no snapshot at %d' % args.second)
    height = dict((l[0], l[2]) for l in in_band)
    lines = []
    for line, ident, bf in zip(snap.lines, snap.ids, snap.fractions):
        genome = runrec.genome_line_of(line)
        genome = '{' + genome[len('{"id":%d,' % ident):]
        lines.append('%d\t%r\t%r\t%s\n' % (ident, height[ident], bf, genome))
    missing = len(ids) - len(lines)
    print('snapshot %d: %d of the band\'s %d leaves joined, %d not in it' % (args.second, len(lines), len(ids), missing))
    data = ''.join(sorted(lines, key=lambda s: int(s.split('\t', 1)[0]))).encode('utf-8')
    name = hashlib.sha256(data).hexdigest()[:16]
    os.makedirs(args.out, exist_ok=True)
    # Gzipped, since a band of a round 58 world holds thousands of leaves and the pre-commit hook takes no
    # file over 5 MB. The name is the hash of the text, so it does not depend on the compressor; the
    # header's time is 0, so the file is the same each time it is written.
    with open(os.path.join(args.out, 'leaves-%s.tsv.gz' % name), 'wb') as raw:
        with gzip.GzipFile(filename='', mode='wb', fileobj=raw, compresslevel=9, mtime=0) as f:
            f.write(data)

    record = {
        'run': os.path.relpath(run).replace('\\', '/'),
        'second': args.second,
        'positionsT': row['t'],
        'leaves': len(leaves),
        'band': {'median': median, 'low': band_lo, 'high': band_hi, 'leaves': len(in_band)},
        'bandWaterCubicMetres': volume,
        'leafDensityPerCubicMetre': density,
        'tankAreaSquareMetres': args.tank_area,
        'tankLeaves': tank_count,
        'spentUnitsPerCubicMetre': spent,
        'pool': 'leaves-%s.tsv.gz' % name,
        'poolRows': len(lines),
        'notInSnapshot': missing,
    }
    with open(os.path.join(args.out, 'leaves-%s.json' % name), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(record, f, indent=2)
        f.write('\n')
    print('wrote leaves-%s.tsv.gz (%d rows) and its record' % (name, len(lines)))


if __name__ == '__main__':
    main()
