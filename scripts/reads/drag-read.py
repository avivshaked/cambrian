"""The leaf tank's drag check, read from restep folders (logbook/specs/nursery-leaf-tank-plan.md, step 6).

    python scripts/reads/drag-read.py <restep folder> [<restep folder> ...]

Each folder is one `Evosim.Nursery --restep` of a drag-check bench: episode.json names the channels, frames.bin holds
them as little-endian float32, one frame a sample. Per member it prints:

- its pool body (bodies 0 and 1 are the swimmer; 2 to 7 single stomachs, by the drag pool's record);
- speed: the root link's path through the water from the second frame on, over that time, m/s, so the first economy
  step's jump from the patch's centre (which the nursery's `path` counts) is left out. Also the level part of it alone;
- the straight-line distance from the second frame to the last, m;
- spin: the root's mean angular speed, rad/s, from the rotation between consecutive frames (2·acos|q1·q2| over the
  interval), which is the nursery's `turn` sampled at the frame rate rather than on economy steps;
- the work the ledger booked, J, and the reserve and tissue lost, J.

Standard library only.
"""
import json
import math
import os
import struct
import sys

SWIMMERS = (0, 1)


def read(folder):
    ep = json.load(open(os.path.join(folder, 'episode.json'), encoding='utf-8'))
    names = [c['name'] for c in ep['channels']]
    width = len(names)
    raw = open(os.path.join(folder, 'frames.bin'), 'rb').read()
    count = len(raw) // (4 * width)
    frames = [struct.unpack_from('<%df' % width, raw, k * 4 * width) for k in range(count)]
    col = dict((n, i) for i, n in enumerate(names))
    return ep, frames, col


def member(ep, frames, col, m):
    def series(name):
        return [f[col[name]] for f in frames]

    t = series('t')
    x, y, z = series('m%d.link0.x' % m), series('m%d.link0.y' % m), series('m%d.link0.z' % m)
    q = list(zip(series('m%d.link0.qx' % m), series('m%d.link0.qy' % m), series('m%d.link0.qz' % m), series('m%d.link0.qw' % m)))
    alive = series('m%d.alive' % m)
    last = max(k for k in range(len(t)) if alive[k] == 1 and not math.isnan(x[k]))
    path = level = 0.0
    turn = 0.0
    for k in range(2, last + 1):
        dx, dy, dz = x[k] - x[k - 1], y[k] - y[k - 1], z[k] - z[k - 1]
        path += math.sqrt(dx * dx + dy * dy + dz * dz)
        level += math.sqrt(dx * dx + dz * dz)
        dot = abs(sum(a * b for a, b in zip(q[k], q[k - 1])))
        turn += 2 * math.acos(min(1.0, dot))
    span = t[last] - t[1]
    work = sum(v for v in series('m%d.ledger.work' % m) if not math.isnan(v))
    start = frames[0][col['m%d.reserve' % m]] + frames[0][col['m%d.tissue' % m]]
    end = frames[last][col['m%d.reserve' % m]] + frames[last][col['m%d.tissue' % m]]
    return {
        'speed': path / span, 'levelSpeed': level / span,
        'distance': math.sqrt((x[last] - x[1]) ** 2 + (y[last] - y[1]) ** 2 + (z[last] - z[1]) ** 2),
        'spin': turn / span, 'work': work, 'lost': start - end, 'y': (min(y[1:last + 1]), max(y[1:last + 1])),
        'span': span,
    }


def main():
    for folder in sys.argv[1:]:
        ep, frames, col = read(folder)
        print('%s: %s, %d frames at %g Hz' % (os.path.basename(folder.rstrip('/\\')), ep['control'], len(frames), ep['sampleRate']))
        pools = [m['pool'] for m in ep['members']]
        print('  slot body kind      speed  level  dist   spin    work   lost    y range')
        for slot, body in sorted(enumerate(pools), key=lambda p: p[1]):
            r = member(ep, frames, col, slot)
            print('  %4d %4d %-8s %6.3f %6.3f %5.1f %6.3f %7.2f %6.1f  %.2f to %.2f'
                  % (slot, body, 'swimmer' if body in SWIMMERS else 'stomach', r['speed'], r['levelSpeed'], r['distance'],
                     r['spin'], r['work'], r['lost'], r['y'][0], r['y'][1]))


if __name__ == '__main__':
    main()
