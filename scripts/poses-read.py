"""Read a run's state stream, poses.bin (logbook/specs/state-stream-spec.md).

    python scripts/poses-read.py <run-or-arm-directory> --summary
    python scripts/poses-read.py <run-or-arm-directory> --at 105
    python scripts/poses-read.py <run-or-arm-directory> --at 105 --bodies 5
    python scripts/poses-read.py <run-or-arm-directory> --seconds

A second implementation of the layout, in another language, written from the spec rather than
from the C#. That is most of what it is for: a reader that agrees with the writer only because
it was written beside it checks nothing. It is also the quickest way to ask a live run what it
has recorded without opening the Editor.

The index is a shortcut and never the truth, so this scans poses.bin by default and reads
poses.idx only under --index, where it checks the two against each other.
"""
import argparse
import os
import struct
import sys

FILE_MAGIC = b'EVOPOSE\x00'
INDEX_MAGIC = b'EVOPOSX\x00'
FRAME_MAGIC = b'FRAM'
VERSION = 1
HEADER_BYTES = 80
INDEX_HEADER_BYTES = 24
INDEX_ENTRY_BYTES = 16
BODY_FIXED_BYTES = 37


class Refusal(Exception):
    """A file this reader will not guess at."""


def run_directory(path):
    """A run directory, or the arm directory above one, resolved to the newest run."""
    if os.path.isfile(os.path.join(path, 'run.json')):
        return path

    runs = [os.path.join(path, d) for d in sorted(os.listdir(path))
            if os.path.isfile(os.path.join(path, d, 'run.json'))]

    if not runs:
        raise Refusal('%s is neither a run directory nor an arm directory holding one' % path)

    return runs[-1]


def read_header(f):
    head = f.read(HEADER_BYTES)

    if len(head) < HEADER_BYTES:
        raise Refusal('a stream is at least %d bytes of header and this file is %d'
                      % (HEADER_BYTES, len(head)))

    if head[:8] != FILE_MAGIC:
        raise Refusal('this is not a pose stream: the first eight bytes are %s'
                      % head[:8].hex(' '))

    version, header_bytes = struct.unpack_from('<HH', head, 8)

    if version != VERSION:
        raise Refusal('the stream is version %d and this reader reads version %d'
                      % (version, VERSION))

    if header_bytes != HEADER_BYTES:
        raise Refusal('the header says it is %d bytes and version %d\'s is %d'
                      % (header_bytes, version, HEADER_BYTES))

    cadence = struct.unpack_from('<f', head, 12)[0]
    config_hash = head[16:80].split(b'\x00')[0].decode('ascii')

    return {'version': version, 'cadence': cadence, 'configHash': config_hash}


def scan(f, size):
    """Every complete frame as (seconds, offset, payload bytes), stopping at a torn write."""
    frames = []
    at = HEADER_BYTES

    while at + 12 <= size:
        f.seek(at)
        head = f.read(16)
        if len(head) < 16:
            break

        if head[:4] != FRAME_MAGIC:
            break

        payload = struct.unpack_from('<I', head, 4)[0]
        if payload < 12 or at + 8 + payload + 4 > size:
            break

        seconds = struct.unpack_from('<d', head, 8)[0]

        f.seek(at + 8 + payload)
        trailer = f.read(4)
        if len(trailer) < 4 or struct.unpack('<I', trailer)[0] != payload:
            break

        frames.append((seconds, at, payload))
        at += 8 + payload + 4

    return frames


def read_index(path):
    """poses.idx as (seconds, offset) pairs, or None when it is missing or does not hold."""
    if not os.path.exists(path):
        return None

    with open(path, 'rb') as f:
        data = f.read()

    if len(data) < INDEX_HEADER_BYTES or data[:8] != INDEX_MAGIC:
        return None

    version, header_bytes = struct.unpack_from('<HH', data, 8)
    count = struct.unpack_from('<I', data, 12)[0]

    if version != VERSION or header_bytes != INDEX_HEADER_BYTES:
        return None

    if len(data) != INDEX_HEADER_BYTES + INDEX_ENTRY_BYTES * count:
        return None

    return [struct.unpack_from('<dq', data, INDEX_HEADER_BYTES + INDEX_ENTRY_BYTES * i)
            for i in range(count)]


def read_frame(f, offset, payload_bytes):
    f.seek(offset + 8)
    payload = f.read(payload_bytes)

    seconds = struct.unpack_from('<d', payload, 0)[0]
    count = struct.unpack_from('<I', payload, 8)[0]

    bodies = []
    at = 12

    for _ in range(count):
        ident = struct.unpack_from('<i', payload, at)[0]
        x, y, z = struct.unpack_from('<3f', payload, at + 4)
        qx, qy, qz, qw = struct.unpack_from('<4f', payload, at + 16)
        fraction = struct.unpack_from('<f', payload, at + 32)[0]
        dof = payload[at + 36]
        at += BODY_FIXED_BYTES

        joints = struct.unpack_from('<%df' % dof, payload, at) if dof else ()
        at += 4 * dof

        bodies.append({'id': ident, 'p': (x, y, z), 'r': (qx, qy, qz, qw),
                       'bodyFraction': fraction, 'q': list(joints)})

    if at != len(payload):
        raise Refusal('the frame at %d says %d bodies and they end %d bytes short of its payload'
                      % (offset, count, len(payload) - at))

    return seconds, bodies


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('run', help='a run directory or the arm directory above it')
    parser.add_argument('--summary', action='store_true', help='header, frames, crowd, size')
    parser.add_argument('--seconds', action='store_true', help='every frame second, one per line')
    parser.add_argument('--at', type=float, help='print the frame at this second')
    parser.add_argument('--bodies', type=int, default=3, help='bodies to print from a frame')
    parser.add_argument('--index', action='store_true', help='check poses.idx against the scan')
    args = parser.parse_args()

    try:
        directory = run_directory(os.path.abspath(args.run))
    except Refusal as e:
        print('refused: %s' % e)
        return 2

    path = os.path.join(directory, 'poses.bin')

    if not os.path.exists(path):
        print('refused: %s recorded no state stream (EVOSIM_POSE_EVERY was 0)' % directory)
        return 2

    size = os.path.getsize(path)

    with open(path, 'rb') as f:
        try:
            header = read_header(f)
        except Refusal as e:
            print('refused: %s' % e)
            return 2

        frames = scan(f, size)

        if args.index:
            index = read_index(os.path.join(directory, 'poses.idx'))

            if index is None:
                print('poses.idx: missing or malformed, so a reader would scan')
            else:
                same = (len(index) == len(frames) and
                        all(abs(index[i][0] - frames[i][0]) < 1e-9 and index[i][1] == frames[i][1]
                            for i in range(len(frames))))

                print('poses.idx: %d entries, scan found %d, agree: %s'
                      % (len(index), len(frames), same))

                if not same:
                    return 1

        if args.summary or not (args.seconds or args.at is not None or args.index):
            crowd = [len(read_frame(f, o, n)[1]) for _, o, n in frames[:1] + frames[-1:]]

            print('%s' % path)
            print('  version %d, cadence %g s, configHash %s'
                  % (header['version'], header['cadence'], header['configHash']))
            print('  %d complete frame(s), %.3f MB, %.1f bytes a frame'
                  % (len(frames), size / 1048576.0,
                     (size - HEADER_BYTES) / max(1, len(frames))))

            if frames:
                print('  t from %g to %g s' % (frames[0][0], frames[-1][0]))
                print('  bodies in the first frame %d, in the last %d' % (crowd[0], crowd[-1]))

            tail = HEADER_BYTES + sum(8 + n + 4 for _, _, n in frames)
            if tail != size:
                print('  %d byte(s) after the last complete frame: a killed run\'s torn write'
                      % (size - tail))

        if args.seconds:
            for seconds, _, _ in frames:
                print('%g' % seconds)

        if args.at is not None:
            found = [fr for fr in frames if abs(fr[0] - args.at) <= 1e-3]

            if not found:
                near = min(frames, key=lambda fr: abs(fr[0] - args.at)) if frames else None
                print('no frame at %g s%s'
                      % (args.at, '' if near is None else '; nearest is %g s' % near[0]))
                return 2

            seconds, bodies = read_frame(f, found[0][1], found[0][2])
            print('t=%g s, %d bodies' % (seconds, len(bodies)))

            for body in bodies[:args.bodies]:
                print('  id %-8d p %8.3f %8.3f %8.3f  r %6.3f %6.3f %6.3f %6.3f  frac %.4f  q %s'
                      % (body['id'], body['p'][0], body['p'][1], body['p'][2],
                         body['r'][0], body['r'][1], body['r'][2], body['r'][3],
                         body['bodyFraction'],
                         ' '.join('%.4f' % v for v in body['q']) if body['q'] else '(rigid)'))

    return 0


if __name__ == '__main__':
    sys.exit(main())
