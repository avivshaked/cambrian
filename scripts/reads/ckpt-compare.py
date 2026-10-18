"""Two checkpoints of one world at one second: whether they hold the same state (round 60, 2026-10-15).

    python -I scripts/reads/ckpt-compare.py <a.ckpt> <b.ckpt>

A checkpoint's header names the arm and the run that wrote it (src/Evosim.Farm/Checkpoint.cs, CheckpointWriter.Store),
so two runs that reach the same state write different files. What must agree is everything else: the second, the
seed, the physics steps, the four hashes, the cadences, and the payload, the world's own bytes, by its length, by the
FNV-1a digest the header carries over it, and byte for byte after gunzip. Prints each field that differs, then a
verdict; exits 0 when the state is the same, 1 when it is not, 2 when a file cannot be read.
"""
import gzip
import struct
import sys

MAGIC_BYTES = 12


class Reader:
    def __init__(self, data):
        self.d, self.i = data, 0

    def take(self, fmt):
        v = struct.unpack_from('<' + fmt, self.d, self.i)
        self.i += struct.calcsize('<' + fmt)
        return v[0]

    def string(self):
        n, shift = 0, 0
        while True:
            b = self.d[self.i]
            self.i += 1
            n |= (b & 0x7F) << shift
            if b < 0x80:
                break
            shift += 7
        s = self.d[self.i:self.i + n].decode('utf-8')
        self.i += n
        return s


def read(path):
    data = open(path, 'rb').read()
    r = Reader(data)
    if data[:8] != b'EVOCKPT\x00':
        print(f'{path}: no checkpoint magic')
        sys.exit(2)
    r.i = 8  # the magic; its version and its length (12, all three) follow
    h = {'version': r.take('H')}
    if r.take('H') != MAGIC_BYTES:
        sys.exit(f'{path}: not a checkpoint this reader knows')
    if h['version'] != 6:
        print(f'{path}: version {h["version"]}; this reader knows the layout of 6')
        sys.exit(2)
    h['seconds'] = r.take('d')
    h['seed'] = r.take('Q')
    h['physicsSteps'] = r.take('q')
    h['physicsStepSeconds'] = r.take('f')
    h['stepsPerMetabolicStep'] = r.take('i')
    for k in ('configHash', 'coreHash', 'dynamicsHash', 'farmHash', 'engineVersion', 'sourceArm', 'sourceRun'):
        h[k] = r.string()
    h['reportEvery'] = r.take('i')
    h['poseEverySeconds'] = r.take('d')
    h['digestEverySteps'] = r.take('q')
    h['checkpointEverySeconds'] = r.take('d')
    h['payloadBytes'] = r.take('q')
    h['payloadDigest'] = r.take('Q')
    h['storedBytes'] = r.take('q')
    stored = data[r.i:r.i + h['storedBytes']]
    if len(stored) != h['storedBytes']:
        print(f'{path}: {len(stored)} stored bytes of {h["storedBytes"]}')
        sys.exit(2)
    payload = gzip.decompress(stored)
    return h, payload


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    (ha, pa), (hb, pb) = read(sys.argv[1]), read(sys.argv[2])
    print(f'a: {ha["sourceArm"]}/{ha["sourceRun"]} at {ha["seconds"]:g} s; b: {hb["sourceArm"]}/{hb["sourceRun"]} at {hb["seconds"]:g} s')
    differ = [k for k in ha if k not in ('sourceArm', 'sourceRun', 'storedBytes') and ha[k] != hb[k]]
    for k in differ:
        print(f'header {k}: {ha[k]} against {hb[k]}')
    print(f'payload: {len(pa):,} and {len(pb):,} bytes; digests {ha["payloadDigest"]:016x} and {hb["payloadDigest"]:016x}')
    if pa != pb:
        n = min(len(pa), len(pb))
        first = next((i for i in range(n) if pa[i] != pb[i]), n)
        print(f'payload bytes differ, first at offset {first:,}')
    same = not differ and pa == pb
    print('VERDICT: the same state' if same else 'VERDICT: different states')
    sys.exit(0 if same else 1)


if __name__ == '__main__':
    main()
