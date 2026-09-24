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
  record-converted.json         the mark, written last and only when the check passes. Its
                                presence is what makes a reader take the run as format 2

Each file is written to a `.partial` name first and moved to its own only when it is whole, and
an existing target is refused rather than overwritten: a run converted once is not converted
again over itself.

The check reads both records through scripts/reads/runrec.py, the one reader, and compares by
string: every snapshot's rows (format 2's slim rows joined to their genomes are format 1's rows to
the byte, or the conversion is wrong), no slim row without a genome, every genome of an id the same
in every snapshot that held it, and every positions row. A failure prints what differed, writes no
mark, and exits 1; the files written stay where they are for a look, and the old ones are untouched.

What is not converted, and why:
  - poses.jsonl is kept. The spec's target is a version 2 state stream without body fractions,
    and the version 2 layout is being built beside this (the pose stream is another build's), so
    this converter writes no stream. runrec.poses() reads poses.jsonl wherever it is.
  - A body born and dead between two snapshots has no genome in format 1, so it has none in the
    converted genomes.jsonl.gz either. The mark counts them against lineage.jsonl's birth rows.

Removing the old files after a passed check is the owner's decision, run by run. Standard library
only.
"""
import argparse
import datetime
import hashlib
import itertools
import json
import os
import sys

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

    if not args.check_only:
        targets = [os.path.join(out, runrec.GENOMES), os.path.join(out, runrec.POSITIONS_GZ), mark]
        targets += [os.path.join(out, runrec.SNAPSHOTS, runrec.snapshot_name(s, runrec.COMPACT)) for s in seconds]
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

    problems, checked = check(run, out, seconds)

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

    print('record-convert: snapshots %s -> %s + genomes %s; positions %s -> %s; poses.jsonl %s kept'
          % (mb(old_snapshots), mb(new_snapshots), mb(sizes['new'][runrec.GENOMES]),
             mb(sizes['old'][runrec.POSITIONS]), mb(sizes['new'][runrec.POSITIONS_GZ]),
             mb(sizes['old'][runrec.POSES])))
    print('record-convert: checked %d snapshot row(s) and %d positions row(s) against %d genome(s)'
          % (checked['checkedSnapshotRows'], checked['checkedPositionsRows'], checked['indexedGenomes']))

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
        'bodyFractions': 'absent: record format 1 never recorded one',
        'poses': 'poses.jsonl kept and not converted: the version 2 stream is another build\'s',
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
