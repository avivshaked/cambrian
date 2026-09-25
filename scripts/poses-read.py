"""Read a state stream: a run's poses.bin or a film window's film.poses.bin
(logbook/specs/state-stream-spec.md).

    python scripts/poses-read.py <run-or-arm-directory> --summary
    python scripts/poses-read.py <run-or-arm-directory> --at 105
    python scripts/poses-read.py <run-or-arm-directory> --at 105 --bodies 5
    python scripts/poses-read.py <run-or-arm-directory> --seconds
    python scripts/poses-read.py <film-window-directory> --summary
    python scripts/poses-read.py <any .bin stream> --at 2240.04

A second implementation of the layout, in another language, written from the spec rather than
from the C#. That is most of what it is for: a reader that agrees with the writer only because
it was written beside it checks nothing. It is also the quickest way to ask a live run what it
has recorded without opening the Editor.

It reads all three versions. Version 1 writes every body raw. Version 2 deflates each frame's
bodies (a raw deflate stream, zlib's window bits -15) behind an uncompressed time, count and raw
length, and gives every body a flags byte: 1 absorptive, 2 jointed, 4 photosynthetic. Version 3
(2026-09-25) adds each body's seconds of reserve and its funds over its own breeding gate, two float32s
after the flags byte, which the theatre shades a body by; read_frame() hands them over as
reserveSeconds and breedFraction, None on a version 1 or 2 stream.

A body fraction of NaN means "not recorded". The farm never writes one; a stream converted from a
run's poses.jsonl (scripts/record-convert.py) writes it for every body, because that file never
carried one. read_frame() hands it over as None, the way runrec.py's snapshots say a fraction the
record does not hold, and a picture draws such a body at its adult size.

The index is a shortcut and never the truth, so this scans the stream by default and reads the
index only under --index, where it checks the two against each other.
"""
import argparse
import math
import os
import struct
import sys
import zlib

FILE_MAGIC = b'EVOPOSE\x00'
INDEX_MAGIC = b'EVOPOSX\x00'
FRAME_MAGIC = b'FRAM'
VERSIONS = (1, 2, 3)
HEADER_BYTES = 80
INDEX_HEADER_BYTES = 24
INDEX_ENTRY_BYTES = 16

# Per version: the payload's bytes before its bodies, and a body's bytes before its joints.
PAYLOAD_PREFIX = {1: 12, 2: 16, 3: 16}
BODY_FIXED = {1: 37, 2: 38, 3: 46}

FLAG_BITS = 1 | 2 | 4
STREAM_NAMES = ('poses.bin', 'film.poses.bin')


class Refusal(Exception):
    """A file this reader will not guess at."""


def stream_path(path):
    """The stream a path names: a .bin file, a directory holding one, or an arm directory
    whose newest run holds one."""
    if os.path.isfile(path):
        return path

    for name in STREAM_NAMES:
        candidate = os.path.join(path, name)
        if os.path.isfile(candidate):
            return candidate

    if os.path.isfile(os.path.join(path, 'run.json')):
        raise Refusal('%s recorded no state stream (EVOSIM_POSE_EVERY was 0)' % path)

    runs = [os.path.join(path, d) for d in sorted(os.listdir(path))
            if os.path.isfile(os.path.join(path, d, 'run.json'))]

    if not runs:
        raise Refusal('%s is neither a stream, a run directory, a film window nor an arm '
                      'directory holding a run' % path)

    return stream_path(runs[-1])


def read_header(f):
    head = f.read(HEADER_BYTES)

    if len(head) < HEADER_BYTES:
        raise Refusal('a stream is at least %d bytes of header and this file is %d'
                      % (HEADER_BYTES, len(head)))

    if head[:8] != FILE_MAGIC:
        raise Refusal('this is not a pose stream: the first eight bytes are %s'
                      % head[:8].hex(' '))

    version, header_bytes = struct.unpack_from('<HH', head, 8)

    if version not in VERSIONS:
        raise Refusal('the stream is version %d and this reader reads versions %s'
                      % (version, ', '.join(str(v) for v in VERSIONS)))

    if header_bytes != HEADER_BYTES:
        raise Refusal('the header says it is %d bytes and version %d\'s is %d'
                      % (header_bytes, version, HEADER_BYTES))

    cadence = struct.unpack_from('<f', head, 12)[0]
    config_hash = head[16:80].split(b'\x00')[0].decode('ascii')

    return {'version': version, 'cadence': cadence, 'configHash': config_hash}


def file_version(f, version):
    """The version a caller named, or the file's own when it named none.

    scan() and read_frame() were called without one before version 2 (runrec.py still calls them
    so), and a reader that guessed version 1 would misread every version 2 frame."""
    if version is not None:
        return version

    f.seek(0)
    return read_header(f)['version']


def scan(f, size, version=None):
    """Every complete frame as (seconds, offset, payload bytes), stopping at a torn write.

    The same walk for both versions: the time is the payload's first eight bytes in both."""
    version = file_version(f, version)
    frames = []
    at = HEADER_BYTES
    least = PAYLOAD_PREFIX[version]

    while at + 12 <= size:
        f.seek(at)
        head = f.read(16)
        if len(head) < 16:
            break

        if head[:4] != FRAME_MAGIC:
            break

        payload = struct.unpack_from('<I', head, 4)[0]
        if payload < least or at + 8 + payload + 4 > size:
            break

        seconds = struct.unpack_from('<d', head, 8)[0]

        f.seek(at + 8 + payload)
        trailer = f.read(4)
        if len(trailer) < 4 or struct.unpack('<I', trailer)[0] != payload:
            break

        frames.append((seconds, at, payload))
        at += 8 + payload + 4

    return frames


def read_index(path, version):
    """The index as (seconds, offset) pairs, or None when it is missing or does not hold.

    An index of another version than its stream is not that stream's index."""
    if not os.path.exists(path):
        return None

    with open(path, 'rb') as f:
        data = f.read()

    if len(data) < INDEX_HEADER_BYTES or data[:8] != INDEX_MAGIC:
        return None

    index_version, header_bytes = struct.unpack_from('<HH', data, 8)
    count = struct.unpack_from('<I', data, 12)[0]

    if index_version != version or header_bytes != INDEX_HEADER_BYTES:
        return None

    if len(data) != INDEX_HEADER_BYTES + INDEX_ENTRY_BYTES * count:
        return None

    return [struct.unpack_from('<dq', data, INDEX_HEADER_BYTES + INDEX_ENTRY_BYTES * i)
            for i in range(count)]


def inflate(data, raw, offset):
    """A version 2 frame's bodies: a whole raw deflate stream that inflates to exactly raw bytes."""
    d = zlib.decompressobj(-15)

    try:
        out = d.decompress(data) + d.flush()
    except zlib.error as e:
        raise Refusal('the frame at %d does not inflate: %s' % (offset, e))

    if not d.eof:
        raise Refusal('the frame at %d ends inside its deflate stream' % offset)

    if d.unused_data:
        raise Refusal('the frame at %d holds %d bytes after its deflate stream'
                      % (offset, len(d.unused_data)))

    if len(out) != raw:
        raise Refusal('the frame at %d says its bodies inflate to %d bytes and they inflate to %d'
                      % (offset, raw, len(out)))

    return out


def raw_length(f, offset, version):
    """A frame's bodies before deflating: version 2's stated length, version 1's payload."""
    f.seek(offset + 8)
    prefix = f.read(PAYLOAD_PREFIX[version])

    if version >= 2:
        return struct.unpack_from('<I', prefix, 12)[0]

    return None


def read_frame(f, offset, payload_bytes, version=None):
    version = file_version(f, version)
    f.seek(offset + 8)
    payload = f.read(payload_bytes)

    seconds = struct.unpack_from('<d', payload, 0)[0]
    count = struct.unpack_from('<I', payload, 8)[0]

    if version >= 2:
        raw = struct.unpack_from('<I', payload, 12)[0]
        records = inflate(payload[PAYLOAD_PREFIX[2]:], raw, offset)
        at = 0
    else:
        records = payload
        at = PAYLOAD_PREFIX[1]

    fixed = BODY_FIXED[version]
    bodies = []

    for _ in range(count):
        if at + fixed > len(records):
            raise Refusal('the frame at %d says %d bodies and its records end inside one'
                          % (offset, count))

        ident = struct.unpack_from('<i', records, at)[0]
        x, y, z = struct.unpack_from('<3f', records, at + 4)
        qx, qy, qz, qw = struct.unpack_from('<4f', records, at + 16)
        fraction = struct.unpack_from('<f', records, at + 32)[0]
        if math.isnan(fraction):
            fraction = None   # not recorded: a stream converted from poses.jsonl

        if version >= 2:
            flags = records[at + 36]
            if flags & ~FLAG_BITS:
                raise Refusal('a body at frame %d carries flags %d, outside the three guild bits'
                              % (offset, flags))
            if version >= 3:
                # Version 3 (2026-09-25): the seconds of reserve and the funds over the breeding gate.
                reserve, breed = struct.unpack_from('<2f', records, at + 37)
                reserve = None if math.isnan(reserve) else reserve
                breed = None if math.isnan(breed) else breed
                dof = records[at + 45]
            else:
                reserve = breed = None
                dof = records[at + 37]
        else:
            flags = None
            reserve = breed = None
            dof = records[at + 36]

        at += fixed

        if at + 4 * dof > len(records):
            raise Refusal('the frame at %d says %d bodies and its records end inside one'
                          % (offset, count))

        joints = struct.unpack_from('<%df' % dof, records, at) if dof else ()
        at += 4 * dof

        bodies.append({'id': ident, 'p': (x, y, z), 'r': (qx, qy, qz, qw),
                       'bodyFraction': fraction, 'flags': flags, 'reserveSeconds': reserve, 'breedFraction': breed,
                       'q': list(joints)})

    if at != len(records):
        raise Refusal('the frame at %d says %d bodies and they end %d bytes short of its records'
                      % (offset, count, len(records) - at))

    return seconds, bodies


def frac(fraction):
    """A body fraction for print: four places, or n/r when the stream did not record one."""
    return 'n/r' if fraction is None else '%.4f' % fraction


def guild(flags):
    """The flags as three letters: absorptive, jointed, photosynthetic, or ??? when unrecorded."""
    if flags is None:
        return '???'

    return ''.join(letter if flags & bit else '-'
                   for bit, letter in ((1, 'a'), (2, 'j'), (4, 'p')))


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('run', help='a stream, a run or film directory, or an arm directory')
    parser.add_argument('--summary', action='store_true', help='header, frames, crowd, size')
    parser.add_argument('--seconds', action='store_true', help='every frame second, one per line')
    parser.add_argument('--at', type=float, help='print the frame at this second')
    parser.add_argument('--bodies', type=int, default=3, help='bodies to print from a frame')
    parser.add_argument('--index', action='store_true', help='check the index against the scan')
    parser.add_argument('--check', action='store_true',
                        help='read every frame whole, which inflates every version 2 frame')
    args = parser.parse_args()

    try:
        path = stream_path(os.path.abspath(args.run))
    except Refusal as e:
        print('refused: %s' % e)
        return 2

    size = os.path.getsize(path)

    with open(path, 'rb') as f:
        try:
            header = read_header(f)
        except Refusal as e:
            print('refused: %s' % e)
            return 2

        version = header['version']
        frames = scan(f, size, version)

        if args.index:
            index = read_index(os.path.splitext(path)[0] + '.idx', version)

            if index is None:
                print('%s: missing or malformed, so a reader would scan'
                      % os.path.basename(os.path.splitext(path)[0] + '.idx'))
            else:
                same = (len(index) == len(frames) and
                        all(abs(index[i][0] - frames[i][0]) < 1e-9 and index[i][1] == frames[i][1]
                            for i in range(len(frames))))

                print('index: %d entries, scan found %d, agree: %s'
                      % (len(index), len(frames), same))

                if not same:
                    return 1

        if args.check:
            bodies = 0
            unrecorded = 0

            try:
                for _, o, n in frames:
                    read = read_frame(f, o, n, version)[1]
                    bodies += len(read)
                    unrecorded += sum(1 for body in read if body['bodyFraction'] is None)
            except Refusal as e:
                print('refused: %s' % e)
                return 2

            print('read %d frame(s) whole, %d bodies in all%s'
                  % (len(frames), bodies,
                     ', %d with no recorded body fraction (NaN)' % unrecorded if unrecorded else ''))

        if args.summary or not (args.seconds or args.at is not None or args.index or args.check):
            try:
                crowd = [len(read_frame(f, o, n, version)[1])
                         for _, o, n in frames[:1] + frames[-1:]]
            except Refusal as e:
                print('refused: %s' % e)
                return 2

            print('%s' % path)
            print('  version %d, cadence %g s (%g a second), configHash %s'
                  % (version, header['cadence'],
                     1.0 / header['cadence'] if header['cadence'] > 0 else 0,
                     header['configHash']))
            print('  %d complete frame(s), %.3f MB, %.1f bytes a frame'
                  % (len(frames), size / 1048576.0,
                     (size - HEADER_BYTES) / max(1, len(frames))))

            if version >= 2 and frames:
                raw = sum(raw_length(f, o, version) for _, o, _ in frames)
                packed = sum(n - PAYLOAD_PREFIX[2] for _, _, n in frames)
                print('  bodies deflated from %.3f MB to %.3f MB (%.2fx)'
                      % (raw / 1048576.0, packed / 1048576.0, raw / max(1, packed)))

            if frames:
                print('  t from %g to %g s' % (frames[0][0], frames[-1][0]))
                print('  bodies in the first frame %d, in the last %d' % (crowd[0], crowd[-1]))

                first = read_frame(f, frames[0][1], frames[0][2], version)[1]
                if first and all(b['bodyFraction'] is None for b in first):
                    print('  body fractions not recorded (NaN): a stream converted from poses.jsonl')

            tail = HEADER_BYTES + sum(8 + n + 4 for _, _, n in frames)
            if tail != size:
                print('  %d byte(s) after the last complete frame: a killed run\'s torn write'
                      % (size - tail))

        if args.seconds:
            for seconds, _, _ in frames:
                print('%.6f' % seconds)

        if args.at is not None:
            found = [fr for fr in frames if abs(fr[0] - args.at) <= 1e-3]

            if not found:
                near = min(frames, key=lambda fr: abs(fr[0] - args.at)) if frames else None
                print('no frame at %g s%s'
                      % (args.at, '' if near is None else '; nearest is %.6f s' % near[0]))
                return 2

            try:
                seconds, bodies = read_frame(f, found[0][1], found[0][2], version)
            except Refusal as e:
                print('refused: %s' % e)
                return 2

            print('t=%.6f s, %d bodies' % (seconds, len(bodies)))

            for body in bodies[:args.bodies]:
                print('  id %-8d p %8.3f %8.3f %8.3f  r %6.3f %6.3f %6.3f %6.3f  frac %s  %s  q %s'
                      % (body['id'], body['p'][0], body['p'][1], body['p'][2],
                         body['r'][0], body['r'][1], body['r'][2], body['r'][3],
                         frac(body['bodyFraction']), guild(body['flags']),
                         ' '.join('%.4f' % v for v in body['q']) if body['q'] else '(rigid)'))

    return 0


if __name__ == '__main__':
    sys.exit(main())
