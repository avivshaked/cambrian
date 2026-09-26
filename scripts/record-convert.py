"""Converts a run on disk to record format 2 beside its old files, and checks the conversion row
for row (logbook/specs/record-and-film-spec.md A7). It deletes nothing, ever.

    python scripts/record-convert.py <run-or-arm-directory>
    python scripts/record-convert.py <run-or-arm-directory> --out scratch/convert-test/r48fix-s4
    python scripts/record-convert.py <run-or-arm-directory> [--out DIR] --check-only

What it writes, beside the old files or into --out:

  genomes.jsonl.gz              each body's genome once, from the first snapshot holding it, with
                                the plan fields taken out; one gzip member per snapshot that
                                brought new bodies
  snapshots/NNNNNNNNN.jsonl.gz  each snapshot's rows, slim: the id and the plan fields
                                (`moduleCounts`, `lostPaths`) when the row had them. No body
                                fraction: format 1 never recorded one, so none is invented
  positions.jsonl.gz            positions.jsonl's rows, one member a row, as the farm writes them
  poses.bin, poses.idx          poses.jsonl's rows as a version 2 state stream
                                (logbook/specs/state-stream-spec.md) and its index. Every body's
                                fraction is float32 NaN, "not recorded", and its guild flags are
                                the same sample's positions.jsonl row's, or 0, counted and noted,
                                where that row or the body's entry in it is missing. The numbers
                                are the JSONL's own rounding as float32. poses.jsonl is kept
  record-converted.json         the mark, written last and only when the check passes. Its
                                presence is what makes a reader take the run as format 2

Each file is written to a `.partial` name first and moved to its own only when it is whole, and
an existing target is refused rather than overwritten: a run converted once is not converted
again over itself.

The check reads both records through scripts/reads/runrec.py, the one reader, and compares by
string: every snapshot's rows (format 2's slim rows joined to their genomes are format 1's rows to
the byte, or the conversion is wrong), no slim row without a genome, every genome of an id the same
in every snapshot that held it, and every positions row. The stream is read back through runrec,
which reads it with scripts/poses-read.py's own functions (the stream's independent reader), and
compared with poses.jsonl frame for frame: every body's numbers to the bit as float32, its
fraction not recorded, its flags the positions row's. A failure prints what differed, writes no
mark, and exits 1; the files written stay where they are for a look, and the old ones are untouched.

What is not converted, and why:
  - A run that wrote a state stream of its own (EVOSIM_POSE_EVERY above 0) keeps poses.jsonl
    unconverted: its own stream carries the solver's floats and the body fractions, and a second
    stream from the JSONL would be the worse record under the same name.
  - A body born and dead between two snapshots has no genome in format 1, so it has none in the
    converted genomes.jsonl.gz either. The mark counts them against lineage.jsonl's birth rows.
  - A converted slim row carries no `bf`: format 1 never recorded a body fraction.

Removing the old files after a passed check is the owner's decision, run by run. Standard library
only.
"""
import argparse
import datetime
import hashlib
import itertools
import json
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, 'reads'))

import runrec  # noqa: E402  (path set above)


def fail(message, code=2):
    print('record-convert: ' + message, file=sys.stderr)
    sys.exit(code)


def size_of(path):
    return os.path.getsize(path) if os.path.isfile(path) else 0


def mb(n):
    return '%.1f MB' % (n / 1e6)


# ---------------------------------------------------------------- writing

def write_files(run, out, seconds, level):
    """Writes the three kinds of file under .partial names and moves each to its own. Returns
    counts for the mark."""
    snap_dir = os.path.join(out, runrec.SNAPSHOTS)
    os.makedirs(snap_dir, exist_ok=True)

    genomes_final = os.path.join(out, runrec.GENOMES)
    genomes_partial = genomes_final + '.partial'

    seen = {}          # id -> digest of its genome row
    changed = []       # (second, id) where a later snapshot's genome differs
    rows = 0
    genomes = 0
    snapshot_bytes = 0

    with open(genomes_partial, 'wb') as g:
        for s in seconds:
            source = os.path.join(run, runrec.SNAPSHOTS, runrec.snapshot_name(s, runrec.JSONL))
            slim = []
            fresh = []

            for line in runrec._jsonl_lines(source):
                ident = runrec.id_of(line)
                if ident is None:
                    fail('snapshot %d holds a row with no organism id (a format 3 genome or older); '
                         'it cannot be joined, so the run is not converted' % s)

                genome = runrec.genome_line_of(line)
                digest = hashlib.blake2b(genome.encode('utf-8'), digest_size=16).digest()

                if ident in seen:
                    if seen[ident] != digest:
                        changed.append((s, ident))
                else:
                    seen[ident] = digest
                    fresh.append(genome)

                slim.append(runrec.slim_line_of(line))
                rows += 1

            if fresh:
                runrec.write_member(g, ('\n'.join(fresh) + '\n').encode('utf-8'), level)
                genomes += len(fresh)

            final = os.path.join(snap_dir, runrec.snapshot_name(s, runrec.COMPACT))
            partial = final + '.partial'
            with open(partial, 'wb') as f:
                if slim:
                    runrec.write_member(f, ('\n'.join(slim) + '\n').encode('utf-8'), level)
            os.replace(partial, final)
            snapshot_bytes += size_of(final)

    os.replace(genomes_partial, genomes_final)

    positions_rows = 0
    old_positions = os.path.join(run, runrec.POSITIONS)

    if os.path.isfile(old_positions):
        final = os.path.join(out, runrec.POSITIONS_GZ)
        partial = final + '.partial'
        with open(partial, 'wb') as p:
            for line in runrec._jsonl_lines(old_positions):
                runrec.write_member(p, (line + '\n').encode('utf-8'), level)
                positions_rows += 1
        os.replace(partial, final)

    return {'snapshotRows': rows, 'genomes': genomes, 'genomesChanged': changed,
            'positionsRows': positions_rows, 'snapshotBytes': snapshot_bytes}


# ---------------------------------------------------------------- the stream
#
# Written from logbook/specs/state-stream-spec.md, version 2, and read back through
# scripts/poses-read.py, which was written from the same page and not from this code.

STREAM_VERSION = 2
STREAM_HEADER = 80
STREAM_INDEX_HEADER = 24
STREAM_NAN = struct.pack('<f', float('nan'))     # 00 00 c0 7f: "not recorded"


def _time_of(line):
    """A poses.jsonl row's second, from its opening '{"t":N,' without parsing the row."""
    end = line.find(',', 5)
    return float(line[5:end])


def stream_cadence(times):
    """The header's nominal cadence: the commonest gap between the first rows, which is the report
    interval the farm wrote poses.jsonl at. A run of one row takes its own second."""
    gaps = [round(b - a, 6) for a, b in zip(times[:41], times[1:41]) if b > a]
    if gaps:
        return max(set(gaps), key=gaps.count)
    return times[0] if times and times[0] > 0 else 1.0


def config_hash_of(run):
    """The run's configHash, from run.json and then config.json, or '' when neither says."""
    for name in ('run.json', 'config.json'):
        path = os.path.join(run, name)
        if os.path.isfile(path):
            with open(path, encoding='utf-8') as f:
                value = json.load(f).get('configHash')
            if value:
                return value
    return ''


def position_flags(run):
    """(second, {id: flags}) for every positions row, in order, lazily: the poses and positions
    rows are written at the same report steps, so the two files are walked in step."""
    if not os.path.isfile(os.path.join(run, runrec.POSITIONS)):
        return
    for line in runrec._jsonl_lines(os.path.join(run, runrec.POSITIONS)):
        row = json.loads(line)
        yield row['t'], dict((b[0], b[4]) for b in row['b'])


def _flags_at(walk, state, t):
    """The positions row at second t, advancing the walk; None when positions holds no row there."""
    while state['row'] is not None and state['row'][0] < t - 1e-9:
        state['row'] = next(walk, None)
    if state['row'] is not None and abs(state['row'][0] - t) <= 1e-9:
        return state['row'][1]
    return None


def write_stream(run, out, level):
    """poses.jsonl as a version 2 state stream, poses.bin and poses.idx, under .partial names.

    Every body's fraction is float32 NaN, "not recorded": poses.jsonl never carried one, and a
    number here would be invented. Its flags are the same sample's positions.jsonl row's, or 0,
    counted, when positions holds no row at that second or no entry for the body. The numbers
    are poses.jsonl's own, rounded to the centimetre and to four places, as float32: what the
    theatre drew from the JSONL, and not the solver's floats a farm-written stream carries."""
    source = os.path.join(run, runrec.POSES)
    times = [_time_of(line) for line in runrec._jsonl_lines(source)]
    cadence = stream_cadence(times)

    config_hash = config_hash_of(run).encode('ascii')[:64]
    header = (b'EVOPOSE\x00' + struct.pack('<HHf', STREAM_VERSION, STREAM_HEADER, cadence)
              + config_hash.ljust(64, b'\x00'))

    final = os.path.join(out, runrec.STREAM)
    index_final = os.path.splitext(final)[0] + '.idx'
    partial = final + '.partial'
    index_partial = index_final + '.partial'

    walk = position_flags(run)
    state = {'row': next(walk, None)}
    entries = []
    counts = {'frames': 0, 'bodies': 0, 'rawBytes': 0, 'deflatedBytes': 0, 'cadence': cadence,
              'framesWithoutPositions': 0, 'bodiesWithoutFlags': 0}

    with open(partial, 'wb') as f:
        f.write(header)
        at = STREAM_HEADER

        for line in runrec._jsonl_lines(source):
            row = json.loads(line)
            t = float(row['t'])
            flags = _flags_at(walk, state, t)
            if flags is None:
                counts['framesWithoutPositions'] += 1

            records = bytearray()
            for body in row['bodies']:
                ident = body['id']
                q = body['q']
                if not -2 ** 31 <= ident < 2 ** 31:
                    fail('body %d at %g s does not fit the stream\'s int32 id' % (ident, t))
                if len(q) > 255:
                    fail('body %d at %g s has %d joint coordinates; the stream holds 255'
                         % (ident, t, len(q)))

                bits = flags.get(ident) if flags is not None else None
                if bits is None:
                    bits = 0
                    counts['bodiesWithoutFlags'] += 1

                records += struct.pack('<i3f4f', ident, *(body['p'] + body['r']))
                records += STREAM_NAN
                records += bytes((bits, len(q)))
                if q:
                    records += struct.pack('<%df' % len(q), *q)

            packer = zlib.compressobj(level, zlib.DEFLATED, -15)
            packed = packer.compress(bytes(records)) + packer.flush()
            payload = struct.pack('<dII', t, len(row['bodies']), len(records)) + packed

            f.write(b'FRAM' + struct.pack('<I', len(payload)) + payload + struct.pack('<I', len(payload)))
            entries.append((t, at))
            at += 8 + len(payload) + 4

            counts['frames'] += 1
            counts['bodies'] += len(row['bodies'])
            counts['rawBytes'] += len(records)
            counts['deflatedBytes'] += len(packed)

    with open(index_partial, 'wb') as f:
        f.write(b'EVOPOSX\x00' + struct.pack('<HHI', STREAM_VERSION, STREAM_INDEX_HEADER, len(entries))
                + b'\x00' * 8)
        for t, offset in entries:
            f.write(struct.pack('<dq', t, offset))

    os.replace(partial, final)
    os.replace(index_partial, index_final)
    return counts


def check_stream(run, out):
    """The stream read back through runrec, which reads it with scripts/poses-read.py's own
    functions, against poses.jsonl row for row: the same seconds and bodies, every number the
    JSONL's as float32 to the bit, every fraction not recorded, every flag the positions row's."""
    problems = []
    old = runrec.poses(run, source=runrec.POSES)
    new = runrec.poses(out, source=runrec.STREAM)

    walk = position_flags(run)
    state = {'row': next(walk, None)}
    frames = 0
    bodies = 0

    for i, (a, b) in enumerate(itertools.zip_longest(old, new)):
        if a is None or b is None:
            problems.append('poses row %d: %s' % (i, 'the stream holds more frames' if a is None
                                                   else 'the stream ends before poses.jsonl'))
            break

        t = float(a['t'])
        if b['t'] != t:
            problems.append('poses row %d: poses.jsonl at %r s and the stream at %r s' % (i, t, b['t']))
            break

        if len(a['bodies']) != len(b['bodies']):
            problems.append('poses at %g s: %d bodies in poses.jsonl and %d in the stream'
                            % (t, len(a['bodies']), len(b['bodies'])))
            break

        flags = _flags_at(walk, state, t)

        for x, y in zip(a['bodies'], b['bodies']):
            want = struct.pack('<3f4f', *(x['p'] + x['r'])) + struct.pack('<%df' % len(x['q']), *x['q'])
            got = struct.pack('<3f4f', *(tuple(y['p']) + tuple(y['r']))) + struct.pack('<%df' % len(y['q']), *y['q'])
            bits = flags.get(x['id'], 0) if flags is not None else 0

            if x['id'] != y['id'] or want != got:
                problems.append('poses at %g s: body %d does not read back as poses.jsonl wrote it'
                                % (t, x['id']))
                break
            if y['bodyFraction'] is not None:
                problems.append('poses at %g s: body %d reads a body fraction of %r where none was '
                                'recorded' % (t, x['id'], y['bodyFraction']))
                break
            if y['flags'] != bits:
                problems.append('poses at %g s: body %d reads flags %r and positions.jsonl says %d'
                                % (t, x['id'], y['flags'], bits))
                break
            bodies += 1

        if problems:
            break
        frames += 1

    return problems, {'checkedPoseFrames': frames, 'checkedPoseBodies': bodies}


# ---------------------------------------------------------------- the check

def check(run, out, seconds):
    """Reads both records through runrec and compares them. Returns (problems, counts)."""
    problems = []
    index = runrec.genome_index(out)
    rows = 0

    for s in seconds:
        a = runrec.snapshot(run, s, fmt=runrec.JSONL)
        b = runrec.snapshot(out, s, fmt=runrec.COMPACT, genomes=index)

        if b is None:
            problems.append('snapshot %d: no format 2 file' % s)
            continue

        if b.refused:
            problems.append('snapshot %d: %d slim row(s) with no genome (ids %s)'
                            % (s, b.refused, b.refused_ids[:5]))

        if b.notes:
            problems.extend('snapshot %d: %s' % (s, n) for n in b.notes)

        if len(a.lines) != len(b.lines):
            problems.append('snapshot %d: %d rows in format 1 and %d in format 2' % (s, len(a.lines), len(b.lines)))

        for i, (x, y) in enumerate(zip(a.lines, b.lines)):
            if x != y:
                problems.append('snapshot %d row %d (id %s): the joined row differs from the old one'
                                % (s, i, runrec.id_of(x)))
                break

        # The dicts an old reader sees are the old ones, `bf` aside (a converted row has none).
        if [dict((k, v) for k, v in r.items() if k != 'bf') for r in b] != list(a):
            problems.append('snapshot %d: the rows read as dicts differ' % s)

        rows += len(b.lines)

    positions_rows = 0

    if runrec.has_positions(run, runrec.JSONL):
        old = runrec.positions_lines(run, runrec.JSONL)
        new = runrec.positions_lines(out, runrec.COMPACT)

        for i, (x, y) in enumerate(itertools.zip_longest(old, new)):
            if x != y:
                problems.append('positions row %d differs (%s against %s)'
                                % (i, 'absent' if x is None else 't=' + x[5:x.find(',')],
                                   'absent' if y is None else 't=' + y[5:y.find(',')]))
                break
            positions_rows += 1

        if new.notes:
            problems.extend('positions: ' + n for n in new.notes)

    return problems, {'checkedSnapshotRows': rows, 'checkedPositionsRows': positions_rows,
                      'indexedGenomes': len(index)}


def lineage_births(run):
    path = os.path.join(run, 'lineage.jsonl')
    if not os.path.isfile(path):
        return None
    births = 0
    for line in runrec._jsonl_lines(path):
        if '"e":"b"' in line:
            births += 1
    return births


# ---------------------------------------------------------------- main

def main():
    parser = argparse.ArgumentParser(
        description='Convert a run to record format 2 beside its old files, and check it row for row. '
                    'Deletes nothing.')
    parser.add_argument('run', help='a run directory, or the arm directory above one')
    parser.add_argument('--out', help='write the new files here instead of beside the old ones')
    parser.add_argument('--level', type=int, default=6, help='gzip level (default 6)')
    parser.add_argument('--check-only', action='store_true',
                        help='write nothing: compare an existing conversion with its source')
    args = parser.parse_args()

    try:
        run = runrec.run_directory(args.run)
    except runrec.Refusal as e:
        fail(str(e))

    fmt, how = runrec.record_format(run)
    if fmt != runrec.JSONL and not args.check_only:
        fail('%s is already record format 2 (%s); there is nothing to convert' % (run, how))

    out = os.path.abspath(args.out) if args.out else run
    seconds = runrec.snapshot_seconds(run, runrec.JSONL)
    mark = os.path.join(out, runrec.CONVERTED)

    print('record-convert: %s, record format %d (%s), %d snapshot(s)' % (run, fmt, how, len(seconds)))
    print('record-convert: writing %s' % ('nothing (--check-only)' if args.check_only else 'into ' + out))

    counts = {}
    stream = {}

    # The poses become a stream unless the run already wrote one of its own, which carries the
    # solver's floats and the body fractions and is the better record of the two.
    has_jsonl_poses = os.path.isfile(os.path.join(run, runrec.POSES))
    has_own_stream = os.path.isfile(os.path.join(run, runrec.STREAM))

    if args.check_only:
        previous = {}
        if os.path.isfile(mark):
            with open(mark, encoding='utf-8') as f:
                previous = json.load(f)
        convert_poses = isinstance(previous.get('poses'), dict) and previous['poses'].get('converted')
    else:
        convert_poses = has_jsonl_poses and not has_own_stream

    if has_jsonl_poses and has_own_stream and not args.check_only:
        print('record-convert: %s has a state stream of its own, so poses.jsonl is kept and not '
              'converted' % run)

    if not args.check_only:
        targets = [os.path.join(out, runrec.GENOMES), os.path.join(out, runrec.POSITIONS_GZ), mark]
        targets += [os.path.join(out, runrec.SNAPSHOTS, runrec.snapshot_name(s, runrec.COMPACT)) for s in seconds]
        if convert_poses:
            targets += [os.path.join(out, runrec.STREAM), os.path.join(out, 'poses.idx')]
        present = [t for t in targets if os.path.exists(t)]
        if present:
            fail('%d target file(s) already exist, the first %s; a conversion is not written over '
                 'itself. Nothing was written.' % (len(present), present[0]))

        os.makedirs(out, exist_ok=True)
        counts = write_files(run, out, seconds, args.level)

        if counts['genomesChanged']:
            s, ident = counts['genomesChanged'][0]
            print('record-convert: %d id(s) carry a different genome in a later snapshot, the first '
                  'id %d at %d s; the check will fail on them' % (len(counts['genomesChanged']), ident, s))

        if convert_poses:
            stream = write_stream(run, out, args.level)
            print('record-convert: poses.jsonl written as a version 2 stream: %d frame(s), %d bodies, '
                  'cadence %g s, bodies deflated %.2fx; body fractions not recorded (NaN)'
                  % (stream['frames'], stream['bodies'], stream['cadence'],
                     stream['rawBytes'] / max(1, stream['deflatedBytes'])))
            if stream['framesWithoutPositions'] or stream['bodiesWithoutFlags']:
                print('record-convert: note: %d frame(s) had no positions row at their second and %d '
                      'bodies no flags there; their flags are written as 0'
                      % (stream['framesWithoutPositions'], stream['bodiesWithoutFlags']))

    problems, checked = check(run, out, seconds)

    if convert_poses:
        stream_problems, stream_checked = check_stream(run, out)
        problems += stream_problems
        checked.update(stream_checked)

    old_snapshots = sum(size_of(os.path.join(run, runrec.SNAPSHOTS, runrec.snapshot_name(s, runrec.JSONL)))
                        for s in seconds)
    new_snapshots = sum(size_of(os.path.join(out, runrec.SNAPSHOTS, runrec.snapshot_name(s, runrec.COMPACT)))
                        for s in seconds)
    sizes = {
        'old': {'snapshots/*.jsonl': old_snapshots,
                runrec.POSITIONS: size_of(os.path.join(run, runrec.POSITIONS)),
                runrec.POSES: size_of(os.path.join(run, runrec.POSES))},
        'new': {'snapshots/*.jsonl.gz': new_snapshots,
                runrec.GENOMES: size_of(os.path.join(out, runrec.GENOMES)),
                runrec.POSITIONS_GZ: size_of(os.path.join(out, runrec.POSITIONS_GZ))},
    }

    if convert_poses:
        sizes['new'][runrec.STREAM] = size_of(os.path.join(out, runrec.STREAM))
        sizes['new']['poses.idx'] = size_of(os.path.join(out, 'poses.idx'))

    print('record-convert: snapshots %s -> %s + genomes %s; positions %s -> %s; poses.jsonl %s %s'
          % (mb(old_snapshots), mb(new_snapshots), mb(sizes['new'][runrec.GENOMES]),
             mb(sizes['old'][runrec.POSITIONS]), mb(sizes['new'][runrec.POSITIONS_GZ]),
             mb(sizes['old'][runrec.POSES]),
             ('-> poses.bin %s + poses.idx %s, and kept'
              % (mb(sizes['new'][runrec.STREAM]), mb(sizes['new']['poses.idx']))) if convert_poses else 'kept'))
    print('record-convert: checked %d snapshot row(s) and %d positions row(s) against %d genome(s)'
          % (checked['checkedSnapshotRows'], checked['checkedPositionsRows'], checked['indexedGenomes']))
    if convert_poses:
        print('record-convert: checked %d pose frame(s) and %d bodies read back through '
              'scripts/poses-read.py against poses.jsonl'
              % (checked.get('checkedPoseFrames', 0), checked.get('checkedPoseBodies', 0)))

    if problems:
        for p in problems[:20]:
            print('record-convert: FAILED ' + p)
        if len(problems) > 20:
            print('record-convert: ... and %d more' % (len(problems) - 20))
        print('record-convert: no mark written; the files written are left for a look and the old '
              'ones are untouched')
        sys.exit(1)

    print('record-convert: the check passed')

    if args.check_only:
        return

    births = lineage_births(run)
    record = {
        'converter': 'scripts/record-convert.py',
        'convertedAt': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
        'source': os.path.abspath(run),
        'sourceFormat': fmt,
        'recordFormat': runrec.COMPACT,
        'check': 'passed',
        'snapshots': len(seconds),
        'snapshotRows': counts['snapshotRows'],
        'genomes': counts['genomes'],
        'lineageBirths': births,
        'birthsWithNoGenome': (births - counts['genomes']) if births is not None else None,
        'positionsRows': counts['positionsRows'],
        'bodyFractions': 'absent: record format 1 never recorded one; the slim rows carry no bf and '
                         'the stream carries NaN, "not recorded"',
        'poses': ({'converted': True, 'from': runrec.POSES, 'to': [runrec.STREAM, 'poses.idx'],
                   'version': STREAM_VERSION, 'frames': stream['frames'], 'bodies': stream['bodies'],
                   'cadenceSeconds': stream['cadence'], 'bodyFraction': 'NaN, not recorded',
                   'flags': 'the same sample\'s positions.jsonl row',
                   'framesWithoutPositions': stream['framesWithoutPositions'],
                   'bodiesWithoutFlags': stream['bodiesWithoutFlags'],
                   'kept': runrec.POSES}
                  if convert_poses else
                  ('poses.jsonl kept and not converted: the run has a state stream of its own'
                   if has_jsonl_poses and has_own_stream else
                   'the run wrote no poses.jsonl')),
        'bytes': sizes,
    }

    partial = mark + '.partial'
    with open(partial, 'w', encoding='utf-8') as f:
        json.dump(record, f, indent=2)
        f.write('\n')
    os.replace(partial, mark)

    print('record-convert: wrote %s' % mark)
    if births is not None:
        print('record-convert: %d of %d birth rows have a genome; the rest lived and died between '
              'two snapshots and format 1 never recorded theirs' % (counts['genomes'], births))


if __name__ == '__main__':
    main()
