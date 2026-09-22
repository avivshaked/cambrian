"""The islands, drawn: the matter field from a farm run's field dumps with the living bodies on it.

    python scripts/field-map.py <arm> [--runs-root scratch/r45-build/runs] [--at 100,1000,5000]
                                [--out scratch/fields/<arm>] [--layers N] [--snow]

A farm run on the island build (D109) writes `fields/NNNNNNNNN.matter.f32` beside every snapshot,
every cell of the matter grid in the grid's own index order, and `fields/layout.json` once naming
the shape. This reader sums each column over its layers (`--layers N` for the top N only, which is
where the light is), divides by the volume summed, and draws the map as units per cubic metre with
the bodies from `positions.jsonl`'s nearest sample scattered over it by guild, in
positions-read.py's colours. `--snow` adds the marine snow's column map as a second panel, from the
`snow-columns` dump.

One line per picture on stdout: the second, the living count, the field's column cv over the live
columns (the table's `mat cv` is over cells; this is the map's own), the fullest column's density,
and the share of bodies standing in a column above the live mean, which is the number the pictures
are for: whether the crowd is where the matter is. `--footprint 100` adds a second share, the
bodies standing in the columns that were above the mean at that dump — the islands as seeded, a
tenth of the water — which still reads once the field has become soup and the first share has
become a coin toss; a crowd spread evenly reads the footprint's own share of the live columns,
printed in the header, and a crowd that stayed where it founded reads near 1. 0114's J8.

Only a second the run dumped at can be drawn; `--at` picks the nearest dump to each second asked
and says which. The picture is a map and not a census: a body's depth is not drawn.
"""

import argparse
import glob
import importlib.util
import json
import math
import os
import struct
import sys


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


# positions-read.py owns the guild flags and colours; one source, so a leaf is the same green here.
_spec = importlib.util.spec_from_file_location(
    'positions_read', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'positions-read.py'))
positions_read = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(positions_read)

GUILDS = positions_read.GUILDS
GUILD_COLOURS = positions_read.GUILD_COLOURS
GUILD_LABELS = positions_read.GUILD_LABELS
guild_of = positions_read.guild_of
newest_run = positions_read.newest_run
find_field = positions_read.find_field


def read_floats(path):
    with open(path, 'rb') as f:
        data = f.read()
    count = len(data) // 4
    return struct.unpack('<%df' % count, data[:count * 4])


def dumps_of(run_dir, kind):
    """{second: path} for every dump of one kind."""
    found = {}
    for path in glob.glob(os.path.join(run_dir, 'fields', '*.%s.f32' % kind)):
        name = os.path.basename(path)
        found[int(name.split('.')[0])] = path
    return found


def nearest(keys, second):
    return min(keys, key=lambda k: abs(k - second))


def column_map(values, layout, layers):
    """(nx, nz) column densities, units/m3, over the top `layers` layers (0: all)."""
    nx, ny, nz = layout['cellsX'], layout['cellsY'], layout['cellsZ']
    cell = float(layout['cellMetres'])
    top = ny if not layers or layers > ny else layers

    total = [[0.0] * nz for _ in range(nx)]
    for iy in range(top):
        base = iy * nx * nz
        for ix in range(nx):
            row = base + ix * nz
            column = total[ix]
            for iz in range(nz):
                column[iz] += values[row + iz]

    volume = top * cell ** 3
    return [[v / volume for v in column] for column in total], cell


def positions_at(run_dir, second):
    """The nearest positions sample: (t, [(x, y, z, guild)])."""
    best = None
    with open(os.path.join(run_dir, 'positions.jsonl'), 'r', encoding='utf-8') as f:
        for line in f:
            if not line.strip():
                continue
            row = json.loads(line)
            t = float(row['t'])
            if best is None or abs(t - second) < abs(best[0] - second):
                best = (t, row['b'])
            elif t > second:
                break
    if best is None:
        return None, []
    return best[0], [(b[1], b[2], b[3], guild_of(int(b[4]))) for b in best[1]]


def live_mask(config, nx, nz, cell):
    """Which columns hold water: all of them in a box, those whose centre is in the circle in a tank."""
    shape = find_field(config, 'worldShape')
    if shape is None or str(shape).lower() != 'tank':
        return [[True] * nz for _ in range(nx)], None
    area = float(find_field(config, 'worldAreaSquareMetres'))
    radius = math.sqrt(area / math.pi)
    mask = [[((ix + 0.5) * cell - radius) ** 2 + ((iz + 0.5) * cell - radius) ** 2 <= radius * radius
             for iz in range(nz)] for ix in range(nx)]
    return mask, radius


def stats(density, mask):
    live = [density[ix][iz] for ix in range(len(density)) for iz in range(len(density[0])) if mask[ix][iz]]
    n = len(live)
    mean = sum(live) / n if n else 0.0
    var = sum((v - mean) ** 2 for v in live) / n if n else 0.0
    cv = math.sqrt(var) / mean if mean > 0 else 0.0
    return mean, cv, max(live) if live else 0.0


def draw(plt, arm, second, sample_t, bodies, density, cell, mask, radius, snow, out_dir, layers, depth):
    nx, nz = len(density), len(density[0])
    panels = 2 if snow is not None else 1
    fig, axes = plt.subplots(1, panels, figsize=(8.5 * panels, 8))
    if panels == 1:
        axes = [axes]

    def panel(ax, field, title, label, cmap):
        # imshow wants rows = z, columns = x; mask the glass out so the deserts read as deserts
        # and not as the outside.
        grid = [[field[ix][iz] if mask[ix][iz] else float('nan') for ix in range(nx)] for iz in range(nz)]
        image = ax.imshow(grid, origin='lower', extent=(0, nx * cell, 0, nz * cell),
                          cmap=cmap, interpolation='nearest')
        fig.colorbar(image, ax=ax, fraction=0.046, pad=0.02, label=label)
        if radius is not None:
            ax.add_patch(plt.Circle((radius, radius), radius, fill=False, edgecolor='#3b6fb6',
                                    linewidth=0.8, alpha=0.8))
        for g in GUILDS:
            picked = [b for b in bodies if b[3] == g]
            if not picked:
                continue
            ax.scatter([b[0] for b in picked], [b[2] for b in picked], s=9, c=GUILD_COLOURS[g],
                       label=GUILD_LABELS[g], alpha=0.85, linewidths=0.3, edgecolors='black')
        ax.set_xlim(0, nx * cell)
        ax.set_ylim(0, nz * cell)
        ax.set_aspect('equal')
        ax.set_xlabel('x (m)')
        ax.set_ylabel('z (m)')
        ax.set_title(title, fontsize=10)

    where = 'top %d layer%s' % (layers, '' if layers == 1 else 's') if layers else 'whole column'
    mean, cv, peak = stats(density, mask)
    panel(axes[0], density,
          '%s | t = %d s (bodies at %d s) | alive %d\nspent matter, %s: cv %.2f, max %.4f units/m3'
          % (arm, second, sample_t, len(bodies), where, cv, peak),
          'units / m3', 'YlGn')
    if bodies:
        axes[0].legend(loc='upper left', bbox_to_anchor=(0.0, -0.08), fontsize=8, ncol=3, framealpha=0.9)

    if snow is not None:
        s_density, s_cell = snow
        s_mask, _ = live_mask_for(s_density, s_cell, radius)
        s_mean, s_cv, s_peak = stats(s_density, s_mask)
        panel(axes[1], s_density,
              'marine snow, column mean: cv %.2f, max %.3f J/m3' % (s_cv, s_peak),
              'J / m3', 'YlOrBr')

    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, '%s-t%07d-map.png' % (arm, second))
    fig.savefig(path, dpi=110, bbox_inches='tight')
    plt.close(fig)
    return path, cv, peak, mean


def live_mask_for(field, cell, radius):
    nx, nz = len(field), len(field[0])
    if radius is None:
        return [[True] * nz for _ in range(nx)], None
    return [[((ix + 0.5) * cell - radius) ** 2 + ((iz + 0.5) * cell - radius) ** 2 <= radius * radius
             for iz in range(nz)] for ix in range(nx)], radius


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('arm')
    parser.add_argument('--runs-root', default='runs')
    parser.add_argument('--run', help='a run directory, instead of the newest under the arm')
    parser.add_argument('--at', help='comma-separated seconds; default every dump')
    parser.add_argument('--out', help='where the pictures go (default scratch/fields/<arm>)')
    parser.add_argument('--layers', type=int, default=0, help='sum the top N layers only (0: all)')
    parser.add_argument('--snow', action='store_true', help='add the marine snow column map')
    parser.add_argument('--no-pictures', action='store_true', help='print the numbers only')
    parser.add_argument('--footprint', type=float, default=None,
                        help='the dump whose above-mean columns are the islands for the second share')
    args = parser.parse_args()

    run_dir = args.run or newest_run(args.runs_root, args.arm)
    layout_path = os.path.join(run_dir, 'fields', 'layout.json')
    if not os.path.isfile(layout_path):
        fail('%s: no fields/layout.json — not a run of the island build, or it wrote no dump yet' % run_dir)
    with open(layout_path, 'r', encoding='utf-8') as f:
        layout = json.load(f)
    if 'matter' not in layout:
        fail('%s: the layout names no matter grid' % layout_path)
    with open(os.path.join(run_dir, 'config.json'), 'r', encoding='utf-8') as f:
        config = json.load(f)

    matter_dumps = dumps_of(run_dir, 'matter')
    if not matter_dumps:
        fail('%s: no matter dumps under fields/' % run_dir)
    snow_dumps = dumps_of(run_dir, 'snow-columns') if args.snow else {}

    if args.at:
        seconds = [nearest(matter_dumps.keys(), float(s)) for s in args.at.split(',') if s.strip()]
    else:
        seconds = sorted(matter_dumps.keys())

    plt = None
    if not args.no_pictures:
        try:
            import matplotlib
            matplotlib.use('Agg')
            import matplotlib.pyplot as plt
        except ImportError:
            print('matplotlib is not installed; numbers only', file=sys.stderr)

    out_dir = args.out or os.path.join('scratch', 'fields', args.arm)
    depth = float(find_field(config, 'worldDepthMetres'))
    m = layout['matter']

    # The islands as seeded: the columns above the live mean at the footprint dump, if asked.
    footprint = None
    if args.footprint is not None:
        f_second = nearest(matter_dumps.keys(), args.footprint)
        f_density, f_cell = column_map(read_floats(matter_dumps[f_second]), m, args.layers)
        f_mask, _ = live_mask(config, m['cellsX'], m['cellsZ'], f_cell)
        f_mean, _, _ = stats(f_density, f_mask)
        footprint = [[f_mask[ix][iz] and f_density[ix][iz] > f_mean for iz in range(m['cellsZ'])]
                     for ix in range(m['cellsX'])]
        live_count = sum(1 for ix in range(m['cellsX']) for iz in range(m['cellsZ']) if f_mask[ix][iz])
        in_count = sum(1 for ix in range(m['cellsX']) for iz in range(m['cellsZ']) if footprint[ix][iz])
        print('footprint: the %d of %d live columns above the mean at %d s (%.2f of the water)'
              % (in_count, live_count, f_second, in_count / live_count if live_count else float('nan')))

    print('second  bodies-at  alive  col-cv   max-units/m3  mean-units/m3  in-islands'
          + ('  in-footprint' if footprint else ''))
    for second in seconds:
        density, cell = column_map(read_floats(matter_dumps[second]), m, args.layers)
        mask, radius = live_mask(config, m['cellsX'], m['cellsZ'], cell)
        sample_t, bodies = positions_at(run_dir, second)
        mean, cv, peak = stats(density, mask)

        # The share of bodies in a column denser than the live mean: the crowd on the islands.
        # And, with a footprint, the share in the columns that were islands when seeded.
        on = 0
        on_footprint = 0
        for x, y, z, g in bodies:
            ix = min(m['cellsX'] - 1, max(0, int(x // cell)))
            iz = min(m['cellsZ'] - 1, max(0, int(z // cell)))
            if density[ix][iz] > mean:
                on += 1
            if footprint and footprint[ix][iz]:
                on_footprint += 1
        share = on / len(bodies) if bodies else float('nan')
        footprint_share = on_footprint / len(bodies) if (bodies and footprint) else float('nan')

        snow = None
        if snow_dumps:
            s = layout['snowColumns']
            s_key = nearest(snow_dumps.keys(), second)
            values = read_floats(snow_dumps[s_key])
            s_cell = float(s['cellMetres'])
            s_field = [[values[ix * s['cellsZ'] + iz] / (s_cell * s_cell * depth) for iz in range(s['cellsZ'])]
                       for ix in range(s['cellsX'])]
            snow = (s_field, s_cell)

        line = '%6d  %9s  %5d  %6.2f  %12.4f  %13.5f  %10s' % (
            second, ('%.0f' % sample_t) if sample_t is not None else '-', len(bodies), cv, peak, mean,
            ('%.2f' % share) if bodies else '-')
        if footprint:
            line += '  %12s' % (('%.2f' % footprint_share) if bodies else '-')
        if plt is not None:
            path, _, _, _ = draw(plt, args.arm, second, sample_t if sample_t is not None else second,
                                 bodies, density, cell, mask, radius, snow, out_dir, args.layers, depth)
            line += '  ' + path
        print(line)


if __name__ == '__main__':
    main()
