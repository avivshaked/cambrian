"""D236's ten blade founders: each of round 59's ten direction tops (inocula/r59-direction-tops/) with its limb's capsule
replaced by a box of the same volume, flat across the stroke. Cut into inocula/r59-direction-blades/.

    python -I scripts/reads/r59-blades.py            # writes the folder
    python -I scripts/reads/r59-blades.py --check    # writes nothing; runs every check and prints what it would write

A parent is read as its committed bytes (LF; a Windows checkout may end the line with CRLF, and origins.tsv of the tops
hashed the LF form). It is parsed with every number kept as its own text, and written back through a serialiser that
mirrors Core's Json.Writer (compact, same key order, no spaces). Before anything is changed, the script proves that
serialiser by writing every parent back unchanged and comparing it byte for byte. Only then does it touch node 1.

The limb (node 1: the link on the hinge) is the only thing changed: shape "capsule" becomes "box" and "dimensions"
becomes the blade's half-extents. Everything else (the joint, its limits, the drive genes, the brain, the mouth, the
trunk, the edge and its anchors) is carried over as text. After writing, the script diffs each blade against its parent
and refuses unless only those two fields of that one node differ.

The numbers, and where each comes from:

* The capsule's volume is Core's, transcribed from CapsuleShape.Volume (src/Evosim.Core/Shapes/StandardShapes.cs,
  lines 191-199). With h the half-extents: r = (|h.X| + |h.Z|) / 2 (Radius, line 186), span = max(0, |h.Y| - r)
  (HalfSpan, line 189), volume = pi r^2 (2 span) + 4/3 pi r^3. Core computes it in single precision. The script
  evaluates it in double on the same float32 inputs (the reference the box is matched to, to 1e-9 relative: a float32
  volume cannot be matched finer than its own ulp, 6e-8) and also emulates Core's float32 arithmetic to say what
  PhenotypePart.Volume will read.
* The box's volume is Core's, BoxShape.Volume (same file, line 27): 8 |h.X| |h.Y| |h.Z|.
* Development builds a part at the node's dimensions times the accumulated scale (here 1: adultScale 1 and the edge's
  scale 1) and raises a half-extent below DevelopmentLimits.MinPartHalfExtent (0.01 m) to it
  (src/Evosim.Core/Development/Developer.cs, Expand and Thicken). So a half-extent the genome gives is the part's.
* Axes. The limb's long axis is its local y (an edge attaches the child's anchor (0,-1,0), which is the box's face
  centre (0,-h.Y,0) and the capsule's pole (0,-0.26,0): the same point). The hinge axis is the joint frame's x, the
  frame being Euler(0, 90 deg, 0) (src/Evosim.Dynamics/Creature.cs line 814, FrameOf), which turns x into -z of the limb's own
  frame; so the hinge axis lies along the limb's local z. The edge's orientation turns the limb's local z onto the
  trunk's -y, which stands vertical. The fin law's width w is the part's caliper along axis x motion, and motion is
  dofAxis x axis, so w is the caliper along the hinge axis (src/Evosim.Dynamics/Fluid.cs, AddedMassOf at line 441
  and its callers at lines 260 and 312). Width is therefore dimensions.z; the thickness, along the swing (hinge axis x limb axis, the limb's local
  x), is dimensions.x; the long axis keeps dimensions.y.
* The blade's proportions. The 0.26 m link limit of round 58 (GenomeFactory.MaxLinkHalfExtent, 0.26) bounds every half-extent
  of a link, by the coordinator's ruling for D236, so the length and the width are both at most 0.26 m. The flattest box
  of the capsule's volume inside that bound has both at 0.26 m and the thickness set by the volume:
  T = V / (8 L W), about 0.01735 m, so width over thickness is about 15 to 1. The tip stays as far from the joint as the
  capsule's did (L = 0.26). The width is searched downward from 0.26 by up to SEARCH_BITS float32 steps (never above it).
  Other bounds: Genome.Validate asks only positive and finite half-extents (src/Evosim.Core/Genome/Genome.cs, lines
  276-283); Development clamps a half-extent up to 0.01 m and prunes a part under 1e-4 m^3 or over 1e6 m^3
  (DevelopmentLimits.cs); a non-root node whose mean half-extent falls under 0.02 m is pruned by mutation
  (MutationRates.NodeExtinctionHalfExtent). Nothing is raised or pruned here. (The fin bench's paddle, 0.2 x 0.01 x 0.2,
  is 20 to 1; at equal volume and this bound 20 to 1 would need a width of 0.30 m, which the ruling excludes.)
* Float32. A half-extent is a float32, so the product of three cannot hit the capsule's volume to 1e-9 by rounding
  each to the nearest float. The script searches the float32 steps below 0.26 for the width and, for each, the nearest
  floats of the thickness, for the triple whose exact product is closest to the reference volume.
"""
import argparse
import functools
import hashlib
import json
import math
import os
import re
import struct
import sys
from fractions import Fraction

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOPS = os.path.join(ROOT, 'inocula', 'r59-direction-tops')
OUT = os.path.join(ROOT, 'inocula', 'r59-direction-blades')

HALF_LENGTH = 0.26          # the long axis's half-extent, the width's ceiling, and the link limit of round 58 (GenomeFactory.MaxLinkHalfExtent)
SEARCH_BITS = 4000          # float32 steps below the link limit tried for the width (at most 0.05% off 0.26 m)
RELATIVE_TOLERANCE = 1e-9
LIMB = 1                    # the limb's node index in every top


# --------------------------------------------------------------------------------------------- float32

def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def f32_bits(x):
    return struct.unpack('<i', struct.pack('<f', x))[0]


def f32_from_bits(b):
    return struct.unpack('<f', struct.pack('<i', b))[0]


def shortest(x):
    """The shortest decimal text that reads back as the float32 x: what .NET's float.ToString("R") writes (Json.cs, line 190)."""
    for p in range(1, 10):
        s = '%.*g' % (p, x)
        if f32(float(s)) == x:
            if 'e' in s or 'E' in s:
                raise SystemExit('%r writes in exponent form; this script handles plain decimals only' % s)
            return s
    raise SystemExit('no float32 text for %r' % x)


# --------------------------------------------------------------------------------------------- Core's volumes

def capsule_volume_double(hx, hy, hz):
    """CapsuleShape.Volume (StandardShapes.cs lines 191-199), in double on the float32 half-extents."""
    r = (abs(hx) + abs(hz)) / 2.0                 # Radius, line 186
    span = max(0.0, abs(hy) - r)                  # HalfSpan, line 189
    return math.pi * r * r * 2.0 * span + (4.0 / 3.0) * math.pi * r * r * r


def capsule_volume_float(hx, hy, hz):
    """The same, rounded to float32 at every operation in C#'s order, which is what Core's PhenotypePart.Volume holds."""
    pi = f32(math.pi)
    r = f32(f32(abs(hx) + abs(hz)) / 2.0)
    span = max(0.0, f32(abs(hy) - r))
    cyl = f32(f32(f32(f32(pi * r) * r) * 2.0) * span)
    caps = f32(f32(f32(f32(f32(4.0 / 3.0) * pi) * r) * r) * r)
    return f32(cyl + caps)


def box_volume_exact(hx, hy, hz):
    """BoxShape.Volume (StandardShapes.cs line 27), 8 |x| |y| |z|, exactly, as a Fraction of the float32 values."""
    return 8 * Fraction(abs(hx)) * Fraction(abs(hy)) * Fraction(abs(hz))


def box_volume_float(hx, hy, hz):
    return f32(f32(f32(8.0 * abs(hx)) * abs(hy)) * abs(hz))


# --------------------------------------------------------------------------------------------- the blade

def blade_for(hx, hy, hz):
    """The blade for a capsule of half-extents (hx, hy, hz): (thickness x, length y, width z), floats, and the error."""
    target = capsule_volume_double(hx, hy, hz)
    length = f32(HALF_LENGTH)
    if abs(hy - length) > 0:
        raise SystemExit('the capsule\'s half-length is %r, not %r' % (hy, length))
    limit = f32(HALF_LENGTH)
    top = f32_bits(limit)
    best = None
    for k in range(0, SEARCH_BITS + 1):
        w = f32_from_bits(top - k)                  # never above the link limit
        t0 = target / (8.0 * length * w)
        tb = f32_bits(f32(t0))
        for j in range(-2, 3):
            t = f32_from_bits(tb + j)
            err = abs(float(box_volume_exact(t, length, w) / Fraction(target) - 1))
            if best is None or err < best[0]:
                best = (err, t, w)
    err, t, w = best
    if err > RELATIVE_TOLERANCE:
        raise SystemExit('the closest float32 triple is off by %.3g relative' % err)
    return t, length, w, err


# Cached without decorator syntax: the pre-commit hook reads an at sign before a dotted name as an email address.
blade_for = functools.lru_cache(maxsize=None)(blade_for)


# --------------------------------------------------------------------------------------------- JSON, numbers as text

class Raw(str):
    """A number as the file wrote it."""


def parse(text):
    return json.loads(text, parse_float=Raw, parse_int=Raw)


_SAFE = re.compile(r'^[A-Za-z0-9_]*$')


def ser(v):
    """Core's Json.Writer in compact mode: no spaces, keys in order, numbers as their own text, true/false lowercase."""
    if isinstance(v, Raw):
        return str(v)
    if isinstance(v, bool):
        return 'true' if v else 'false'
    if isinstance(v, dict):
        parts = []
        for k, x in v.items():
            if not _SAFE.match(k):
                raise SystemExit('key %r needs escaping; this serialiser does not' % k)
            parts.append('"%s":%s' % (k, ser(x)))
        return '{' + ','.join(parts) + '}'
    if isinstance(v, list):
        return '[' + ','.join(ser(x) for x in v) + ']'
    if isinstance(v, str):
        if not _SAFE.match(v):
            raise SystemExit('string %r needs escaping; this serialiser does not' % v)
        return '"%s"' % v
    raise SystemExit('a %s in a genome: %r' % (type(v).__name__, v))


def committed_bytes(path):
    with open(path, 'rb') as f:
        data = f.read()
    if data.endswith(b'\r\n'):
        data = data[:-2] + b'\n'
    if data.count(b'\n') != 1 or not data.endswith(b'\n') or b'\r' in data:
        raise SystemExit('%s is not one line and a newline' % path)
    return data


def sha(data):
    return hashlib.sha256(data).hexdigest()


# --------------------------------------------------------------------------------------------- tsv helpers

def read_tsv(path):
    with open(path, encoding='utf-8', newline='') as f:
        lines = [l.rstrip('\r\n') for l in f if l.strip()]
    head = lines[0].split('\t')
    return head, [dict(zip(head, l.split('\t'))) for l in lines[1:]]


def number(x):
    return '%.12g' % x


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true', help='write nothing')
    a = ap.parse_args()

    _, pool_rows = read_tsv(os.path.join(TOPS, 'pool.tsv'))
    _, origin_rows = read_tsv(os.path.join(TOPS, 'origins.tsv'))
    parent_sha = {r['file']: r['sha256'] for r in origin_rows}
    if len(pool_rows) != 10:
        raise SystemExit('the tops\' pool.tsv lists %d genomes, not ten' % len(pool_rows))

    # 1. The serialiser reproduces every parent, byte for byte, with nothing changed.
    parents = []
    for r in pool_rows:
        name = r['path']
        data = committed_bytes(os.path.join(TOPS, name))
        if sha(data) != parent_sha[name]:
            raise SystemExit('%s does not match its SHA-256 in origins.tsv' % name)
        text = data.decode('utf-8')
        again = ser(parse(text.rstrip('\n'))) + '\n'
        if again != text:
            raise SystemExit('the serialiser does not reproduce %s byte for byte' % name)
        parents.append((r, name, data, text))
    print('serialiser: %d parents rewritten unchanged, byte for byte' % len(parents))

    # 2. The blades.
    blades = []
    for r, name, data, text in parents:
        g = parse(text.rstrip('\n'))
        limb = g['nodes'][LIMB]
        if not (limb['shape'] == 'capsule' and limb['cell'] == 'link' and limb['joint'] == 'Hinge'):
            raise SystemExit('%s: node %d is not a hinged link capsule' % (name, LIMB))
        if len(g['nodes']) != 2 or str(g['nodes'][0]['edges'][0]['child']) != str(LIMB):
            raise SystemExit('%s: not the swimmer\'s two-node body' % name)
        d = limb['dimensions']
        hx, hy, hz = (f32(float(d[k])) for k in 'xyz')
        if hx != hz:
            raise SystemExit('%s: the capsule is not round (x %r, z %r)' % (name, hx, hz))

        t, length, w, err = blade_for(hx, hy, hz)
        limb['shape'] = 'box'
        limb['dimensions'] = {'x': Raw(shortest(t)), 'y': Raw(shortest(length)), 'z': Raw(shortest(w))}
        out = (ser(g) + '\n').encode('utf-8')
        out_name = name[:-len('.json')] + '-blade.json'

        # Read back what was written: the text must give the float32s chosen.
        back = parse(out.decode('utf-8').rstrip('\n'))['nodes'][LIMB]['dimensions']
        if (f32(float(back['x'])), f32(float(back['y'])), f32(float(back['z']))) != (t, length, w):
            raise SystemExit('%s: the dimensions do not read back' % out_name)

        # Only the limb's shape and dimensions differ from the parent.
        before, after = parse(text.rstrip('\n')), parse(out.decode('utf-8').rstrip('\n'))
        diffs = []

        def walk(p, b, c):
            if type(b) != type(c):
                diffs.append(p)
            elif isinstance(b, dict):
                if list(b) != list(c):
                    diffs.append(p + '(keys)')
                for k in b:
                    if k in c:
                        walk(p + '.' + k, b[k], c[k])
            elif isinstance(b, list):
                if len(b) != len(c):
                    diffs.append(p + '(length)')
                for i, (x, y) in enumerate(zip(b, c)):
                    walk('%s[%d]' % (p, i), x, y)
            elif b != c:
                diffs.append(p)

        walk('', before, after)
        # The long axis keeps the parent's own text (0.26), so y does not differ.
        if sorted(diffs) != ['.nodes[1].dimensions.x', '.nodes[1].dimensions.z', '.nodes[1].shape']:
            raise SystemExit('%s differs from its parent in %s' % (out_name, diffs))

        blades.append({
            'row': r, 'parent': name, 'parent_sha': sha(data), 'file': out_name, 'bytes': out, 'sha': sha(out),
            'cap': (hx, hy, hz), 'box': (t, length, w), 'err': err,
            'cap_v': capsule_volume_double(hx, hy, hz), 'cap_vf': capsule_volume_float(hx, hy, hz),
            'box_v': float(box_volume_exact(t, length, w)), 'box_vf': box_volume_float(t, length, w),
            'diffs': diffs,
        })
        print('%s: capsule %s x %s x %s (V %.10g, Core float %.9g)  box %s x %s x %s (V %.10g, Core float %.9g)  '
              'relative error %.2e  width:thickness %.4f  differs in %s' % (
                  out_name, hx, hy, hz, blades[-1]['cap_v'], blades[-1]['cap_vf'],
                  t, length, w, blades[-1]['box_v'], blades[-1]['box_vf'], err, w / t, ', '.join(diffs)))

    if a.check:
        print('--check: nothing written')
        return

    # 3. Write.
    os.makedirs(OUT, exist_ok=True)
    for b in blades:
        with open(os.path.join(OUT, b['file']), 'wb') as f:
            f.write(b['bytes'])

    head = ['file', 'sha256', 'parent', 'parent sha256', 'run', 'id', 'capsule x', 'capsule y', 'capsule z', 'capsule volume m3',
            'capsule volume as Core computes it (float32)', 'box x thickness', 'box y length', 'box z width', 'box volume m3',
            'box volume as Core computes it (float32)', 'volume relative difference', 'width over thickness']
    origins = ['\t'.join(head)]
    for b in blades:
        r = b['row']
        origins.append('\t'.join([
            b['file'], b['sha'], b['parent'], b['parent_sha'], r['run'], r['id'],
            *(shortest(x) for x in b['cap']), number(b['cap_v']), number(b['cap_vf']),
            *(shortest(x) for x in b['box']), number(b['box_v']), number(b['box_vf']),
            '%.3g' % b['err'], '%.4f' % (b['box'][2] / b['box'][0])]))
    write_text(os.path.join(OUT, 'origins.tsv'), origins)

    pool_head = '\t'.join(['path', 'weight', 'run', 'id', 'body', 'ownEaten'])
    # The blades are unmeasured: ownEaten is left empty, where the parent's number would say something about a body that has not swum.
    own = [pool_head] + ['\t'.join([b['file'], '0.1', b['row']['run'], b['row']['id'] + '-blade', b['row']['body'], '']) for b in blades]
    write_text(os.path.join(OUT, 'pool.tsv'), own)

    mixed = [pool_head]
    for r in pool_rows:
        mixed.append('\t'.join(['../r59-direction-tops/' + r['path'], '0.05', r['run'], r['id'], r['body'], r['ownEaten']]))
    for b in blades:
        mixed.append('\t'.join([b['file'], '0.05', b['row']['run'], b['row']['id'] + '-blade', b['row']['body'], '']))
    write_text(os.path.join(OUT, 'pool-mixed.tsv'), mixed)

    print('wrote %d genomes, origins.tsv, pool.tsv and pool-mixed.tsv to %s' % (len(blades), os.path.relpath(OUT, ROOT)))


def write_text(path, lines):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
