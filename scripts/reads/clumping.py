"""How clumped a world is from above, by second: the footprint cut into square cells, bodies counted
per cell, and the variance of the counts over their mean.

    python scripts/reads/clumping.py r42-s2 [--cell 5] [--every 1000]

A world mixed flat reads about 1 (a Poisson scatter). A world of clouds, kin drifting together or
bodies gathering on purpose, reads well above 1. It is the measure `cols` over uniform cannot be:
at a few hundred bodies in two thousand columns nearly every body has a column to itself whatever
the large pattern, so `cols` read 0.94 on round 42's seed 2 at 15,000 s while the picture from
above showed a crescent with the centre empty (HANDOFF, 2026-09-20). The count is taken over the
cells whose centre lies inside the disc, from the run's config. The second column splits the
index by the jointed flag, since a cloud is usually one clade. Streams positions.jsonl row by row.
"""
import argparse, glob, json, math

ROOT = 'D:/Projects/experiments/evolution-simulator'


def index(points, cells, cell):
    if len(points) < 2:
        return float('nan')
    counts = dict.fromkeys(cells, 0)
    for x, z in points:
        k = (math.floor(x / cell), math.floor(z / cell))
        if k in counts:
            counts[k] += 1
    n = len(counts)
    mean = sum(counts.values()) / n
    if mean == 0:
        return float('nan')
    var = sum((c - mean) ** 2 for c in counts.values()) / (n - 1)
    return var / mean


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('arm')
    ap.add_argument('--cell', type=float, default=5.0)
    ap.add_argument('--every', type=int, default=1000)
    a = ap.parse_args()
    run = sorted(glob.glob(f'{ROOT}/runs/{a.arm}/2026*'))[-1]
    config = json.load(open(run + '/config.json', encoding='utf-8'))

    def find(node, name):
        if isinstance(node, dict):
            for k, v in node.items():
                if k == name:
                    return v
                r = find(v, name)
                if r is not None:
                    return r
        return None

    area = find(config, 'worldAreaSquareMetres') or find(config, 'areaSquareMetres') or find(config, 'area')
    print(f'{a.arm}: cell {a.cell} m, every {a.every} s' + (f', disc of {area} m2' if area else ''))
    print('      t  alive  clumping  jointed  unjointed   centre offset m')
    with open(run + '/positions.jsonl', encoding='utf-8') as f:
        first = True
        for line in f:
            line = line.strip()
            if not line:
                continue
            row = json.loads(line)
            t = int(row['t'])
            if t % a.every:
                continue
            key = next(k for k in row if isinstance(row[k], list))
            bodies = row[key]
            if not bodies:
                continue
            if first:
                # The disc's centre and radius from the config if it names an area, else from the
                # first sample's own extent; positions are in the tank's frame, centred on its axis
                # or on its bounding square, so the centre is read from the data's midrange once.
                xs = [b[1] for b in bodies]; zs = [b[3] for b in bodies]
                radius = math.sqrt(area / math.pi) if area else max(max(xs) - min(xs), max(zs) - min(zs)) / 2
                cx = 0.0 if min(xs) < -radius / 2 else radius
                cz = 0.0 if min(zs) < -radius / 2 else radius
                reach = int(math.ceil(radius / a.cell)) + 1
                cells = [(i + math.floor(cx / a.cell), j + math.floor(cz / a.cell))
                         for i in range(-reach, reach + 1) for j in range(-reach, reach + 1)
                         if math.hypot((i + 0.5 + math.floor(cx / a.cell)) * a.cell - cx,
                                       (j + 0.5 + math.floor(cz / a.cell)) * a.cell - cz) <= radius]
                first = False
            pts = [(b[1], b[3]) for b in bodies]
            # flags: bit 1 is jointed in positions.jsonl's packing (absorptive 1, jointed 2, photosynthetic 4)
            jointed = [(b[1], b[3]) for b in bodies if int(b[4]) & 2]
            rigid = [(b[1], b[3]) for b in bodies if not int(b[4]) & 2]
            mx = sum(p[0] for p in pts) / len(pts) - cx
            mz = sum(p[1] for p in pts) / len(pts) - cz
            print(f'{t:7d} {len(pts):6d} {index(pts, cells, a.cell):9.2f} {index(jointed, cells, a.cell):8.2f} '
                  f'{index(rigid, cells, a.cell):10.2f} {math.hypot(mx, mz):17.1f}')


if __name__ == '__main__':
    main()
