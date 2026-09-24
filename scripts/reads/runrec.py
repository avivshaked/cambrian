"""One way to read a run's record, in either format (logbook/specs/record-and-film-spec.md A6).

    sys.path.insert(0, <repo>/scripts/reads)
    import runrec

    run = runrec.run_directory(path)                 # a run directory, or the arm above one
    fmt, how = runrec.record_format(run)             # 1 or 2, and what decided it
    snap = runrec.snapshot(run, 15000)               # rows with the genome joined, or None
    for row in runrec.positions(run): ...            # positions rows as dicts
    for line in runrec.positions_lines(run): ...     # the same rows as text
    frame = runrec.poses(run, 1000)                  # {'t', 'bodies'} at a second, or None
    genomes = runrec.genomes(run, ids)               # id -> genome row (dict)
    runrec.checkpoints(run)                          # every checkpoint's header, as dicts

Record format 1 is every run before the console farm's round 49 build, and every Unity farm run:
every living body's whole genome in every snapshot (snapshots/NNNNNNNNN.jsonl), positions.jsonl
and poses.jsonl. Format 2 writes each genome once, in genomes.jsonl.gz, when its body is
admitted; snapshots of slim rows (snapshots/NNNNNNNNN.jsonl.gz) carrying the id, the plan fields
(`moduleCounts`, `lostPaths`) when the body had them, and the body fraction `bf`; positions in
positions.jsonl.gz, one gzip member a sample; the state stream (poses.bin) in place of
poses.jsonl; and compressed checkpoints (version 5). Which format a run holds is read from
run.json's `recordFormat`, then from the converter's mark (record-converted.json, written by
scripts/record-convert.py once it has checked its conversion), then from the files present. It
is never guessed from inside a file. Every reader takes `fmt=` to read one record by name, which
is how the converter compares the two.

Every reader says which record it read: the result's `source` names the files, and the first read
of each kind of file in a run prints one line to stderr. A torn last gzip member, which is what a
killed write leaves, is reported on stderr and skipped, and never parsed.

A snapshot row read here is what format 1 wrote for the body, to the byte: the slim row without
its body fraction, joined to the genome row after its id (the C#'s RecordFiles.Join, and the
reason the converter can compare by string). The dict carries `bf` besides when the record has
it, so a reader written against format 1 sees every key it knew with the value it knew. A slim
row whose id genomes.jsonl.gz does not hold is refused and counted (`refused`, `refused_ids`),
never joined to anything else.

The gzip members carry their own length in an extra header field (RFC 1952's FEXTRA, subfield
`EV`, eight bytes, little-endian: the member's length from its first byte to the end of its
trailer), so a torn member is known before a byte of it is inflated. Standard gzip readers skip
the field. A member without it (a file gzipped by hand) is walked with zlib instead.

Poses come from poses.jsonl when the run has one (format 1, or a converted run, which keeps it)
and from the state stream otherwise, read through scripts/poses-read.py's own functions, which
are the stream's independent reader. Standard library only.
"""
import gzip
import hashlib
import importlib.util
import json
import os
import struct
import sys
import zlib

JSONL = 1
COMPACT = 2

GENOMES = 'genomes.jsonl.gz'
POSITIONS = 'positions.jsonl'
POSITIONS_GZ = 'positions.jsonl.gz'
POSES = 'poses.jsonl'
STREAM = 'poses.bin'
SNAPSHOTS = 'snapshots'
CONVERTED = 'record-converted.json'
CHECKPOINTS = 'checkpoints'

MEMBER_HEADER = 24
_FEXTRA = 0x04

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)

VERBOSE = True
_said = set()


class Refusal(Exception):
    """A record this reader will not guess at."""


def say(message):
    print('runrec: ' + message, file=sys.stderr)


def _say_once(run, kind, message):
    key = (os.path.abspath(run), kind)
    if VERBOSE and key not in _said:
        _said.add(key)
        say(message)


# ---------------------------------------------------------------- the run and its record

def run_directory(path):
    """A run directory, or the arm directory above one resolved to its newest run.

    A run directory is one holding run.json or config.json, or the converter's mark. Newest is the
    last by name, which is by the instant the run started (RunDirectory.Create's naming).
    """
    if _is_run(path):
        return path

    if not os.path.isdir(path):
        raise Refusal('%s is not a directory' % path)

    runs = [os.path.join(path, d) for d in sorted(os.listdir(path)) if _is_run(os.path.join(path, d))]
    if not runs:
        raise Refusal('%s is neither a run directory nor an arm directory holding one' % path)

    if len(runs) > 1 and VERBOSE:
        say('%s holds %d run directories; reading the newest, %s'
            % (path, len(runs), os.path.basename(runs[-1])))

    return runs[-1]


def _is_run(path):
    return os.path.isdir(path) and any(
        os.path.isfile(os.path.join(path, name)) for name in ('run.json', 'config.json', CONVERTED))


def record_format(run, fmt=None):
    """(format, how it was decided). `fmt` forces one, for a reader that names its record."""
    if fmt is not None:
        if fmt not in (JSONL, COMPACT):
            raise Refusal('record format %r: the records are 1 and 2' % (fmt,))
        return fmt, 'asked for by the caller'

    manifest = os.path.join(run, 'run.json')
    have_manifest = os.path.isfile(manifest)

    if have_manifest:
        with open(manifest, encoding='utf-8') as f:
            m = json.load(f)
        if 'recordFormat' in m:
            value = m['recordFormat']
            if value not in (JSONL, COMPACT):
                raise Refusal('run.json says recordFormat %r and this reader reads 1 and 2' % (value,))
            return value, 'run.json recordFormat %d' % value

    if os.path.isfile(os.path.join(run, CONVERTED)):
        return COMPACT, CONVERTED + ' (converted and checked by scripts/record-convert.py)'

    if have_manifest:
        return JSONL, 'run.json names no recordFormat, so the record before format 2'

    if os.path.isfile(os.path.join(run, GENOMES)) or os.path.isfile(os.path.join(run, POSITIONS_GZ)):
        return COMPACT, 'no run.json; %s or %s is present' % (GENOMES, POSITIONS_GZ)

    return JSONL, 'no run.json and no format 2 file'


# ---------------------------------------------------------------- gzip in members

def member_bytes(data, level=6):
    """One member holding `data`, with the EV length field: what the C#'s GzipMembers writes."""
    plain = gzip.compress(data, compresslevel=level, mtime=0)
    if plain[:3] != b'\x1f\x8b\x08' or plain[3] != 0:
        raise Refusal('gzip wrote a header this record does not know: %s' % plain[:4].hex(' '))

    total = len(plain) + 2 + 12
    extra = struct.pack('<H', 12) + b'EV' + struct.pack('<HQ', 8, total)
    return plain[:3] + bytes([_FEXTRA]) + plain[4:10] + extra + plain[10:]


def write_member(f, data, level=6):
    """Appends one member holding `data` to an open binary file."""
    f.write(member_bytes(data, level))


def _length_of(head):
    """A member's whole length from its first 24 bytes, or None when it carries no EV field."""
    if len(head) < MEMBER_HEADER or head[:3] != b'\x1f\x8b\x08':
        return None
    if head[3] != _FEXTRA:
        return None
    xlen, = struct.unpack_from('<H', head, 10)
    if xlen != 12 or head[12:14] != b'EV':
        return None
    sublen, total = struct.unpack_from('<HQ', head, 14)
    if sublen != 8 or total < MEMBER_HEADER + 8:
        return None
    return total


def members(path, torn=None):
    """Every complete member of a file, inflated, in order. A torn last member is skipped.

    `torn`, a list, is given the torn member's note; the note is also printed. A complete member
    that does not inflate, or bytes that are not gzip at all, raise Refusal: a kill cannot leave
    either. The file's size is read once, so a member a live writer lands during the walk is the
    next reader's.
    """
    size = os.path.getsize(path)
    complete = 0

    with open(path, 'rb') as f:
        at = 0
        while at < size:
            f.seek(at)
            head = f.read(MEMBER_HEADER)

            if len(head) < MEMBER_HEADER and size - at < MEMBER_HEADER:
                if head[:2] in (b'\x1f\x8b', b'\x1f', b''):
                    _tear(path, at, size - at, None, complete, torn)
                    return
                raise Refusal('%s: %d bytes at %d are not a gzip member' % (path, size - at, at))

            total = _length_of(head)

            if total is None:
                if head[:2] != b'\x1f\x8b':
                    raise Refusal('%s: the bytes at %d are not a gzip member' % (path, at))
                # A plain member (no EV field): walk the rest with zlib, which says where each ends.
                for data in _walk_plain(f, path, at, size, complete, torn):
                    yield data
                return

            if at + total > size:
                _tear(path, at, size - at, total, complete, torn)
                return

            f.seek(at)
            member = f.read(total)
            try:
                data = gzip.decompress(member)
            except (OSError, EOFError, zlib.error) as e:
                raise Refusal('%s: the member at byte %d does not inflate: %s' % (path, at, e))

            complete += 1
            at += total
            yield data


def _walk_plain(f, path, at, size, complete, torn):
    f.seek(at)
    rest = f.read(size - at)
    offset = 0

    while offset < len(rest):
        d = zlib.decompressobj(31)
        try:
            data = d.decompress(rest[offset:])
        except zlib.error as e:
            raise Refusal('%s: the member at byte %d does not inflate: %s' % (path, at + offset, e))

        if not d.eof:
            _tear(path, at + offset, len(rest) - offset, None, complete, torn)
            return

        used = len(rest) - offset - len(d.unused_data)
        complete += 1
        offset += used
        yield data


def _tear(path, at, bytes_on_disk, promised, complete, torn):
    note = ('%s: the last member, at byte %d, is torn (%d bytes on disk%s) and was skipped; '
            '%d complete member(s) before it were read'
            % (os.path.basename(path), at, bytes_on_disk,
               ' of the %d its header promises' % promised if promised else ', its header cut',
               complete))
    say(note)
    if torn is not None:
        torn.append(note)


def member_lines(path, torn=None):
    """Every line of every complete member, without its line break. Empty lines are skipped."""
    for data in members(path, torn):
        if not data:
            continue
        if not data.endswith(b'\n'):
            raise Refusal('%s: a complete member does not end at the end of a row' % path)
        for line in data.decode('utf-8').split('\n'):
            line = line.rstrip('\r')
            if line:
                yield line


# ---------------------------------------------------------------- rows

def id_of(line):
    """The organism id a row opens with ('{"id":N'), or None."""
    if not line.startswith('{"id":'):
        return None
    end = 6
    if end < len(line) and line[end] == '-':
        end += 1
    while end < len(line) and line[end].isdigit():
        end += 1
    if end == 6 or end >= len(line) or line[end] not in ',}':
        return None
    return int(line[6:end])


def join_line(slim, genome):
    """A slim snapshot row and its body's genome row, joined into format 1's row."""
    ident = id_of(slim)
    if ident is None or not slim.endswith('}'):
        raise Refusal('a slim row opens with its id and closes its object: %.80s' % slim)

    prefix = '{"id":%d,' % ident
    if not genome.startswith(prefix):
        raise Refusal('the genome row joined to body %d is not its own: %.80s' % (ident, genome))

    at = slim.find(',"bf":')
    if at >= 0:
        if ',' in slim[at + 6:]:
            raise Refusal('body %d\'s slim row carries a field after its body fraction' % ident)
        head = slim[:at]
    else:
        head = slim[:-1]

    return head + ',' + genome[len(prefix):]


def body_fraction_of(slim):
    """A slim row's `bf`, or None."""
    at = slim.find(',"bf":')
    if at < 0:
        return None
    end = slim.find('}', at)
    try:
        return float(slim[at + 6:end])
    except ValueError:
        return None


def genome_line_of(full):
    """Format 1's snapshot row with the plan fields taken out: format 2's genome row."""
    ident = id_of(full)
    at = full.find(',"format":')
    if ident is None or at < 0:
        raise Refusal('a snapshot row opens with its id and carries "format": %.80s' % full)
    return '{"id":%d' % ident + full[at:]


def slim_line_of(full):
    """Format 1's snapshot row without its genome: format 2's slim row, with no body fraction."""
    at = full.find(',"format":')
    if id_of(full) is None or at < 0:
        raise Refusal('a snapshot row opens with its id and carries "format": %.80s' % full)
    return full[:at] + '}'


def _jsonl_lines(path):
    """The complete lines of a JSONL file; a last line that does not close its object is dropped
    (a live writer's half row)."""
    with open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line and line.endswith('}'):
                yield line


class Rows:
    """An iterable of rows that says where they came from: `source`, `fmt`, `how`, and `notes`
    (a torn member's, filled while iterating)."""

    def __init__(self, source, fmt, how, make):
        self.source = source
        self.fmt = fmt
        self.how = how
        self.notes = []
        self._make = make

    def __iter__(self):
        return self._make(self.notes)


# ---------------------------------------------------------------- snapshots and genomes

def snapshot_name(second, fmt):
    return '%09d%s' % (int(second), '.jsonl.gz' if fmt == COMPACT else '.jsonl')


def snapshot_seconds(run, fmt=None):
    """Every second the record holds a snapshot at, ascending, as ints."""
    fmt, _ = record_format(run, fmt)
    d = os.path.join(run, SNAPSHOTS)
    if not os.path.isdir(d):
        return []
    suffix = '.jsonl.gz' if fmt == COMPACT else '.jsonl'
    out = []
    for name in os.listdir(d):
        if name.endswith(suffix) and name[:-len(suffix)].isdigit():
            out.append(int(name[:-len(suffix)]))
    return sorted(out)


def has_snapshot(run, second, fmt=None):
    fmt, _ = record_format(run, fmt)
    return os.path.isfile(os.path.join(run, SNAPSHOTS, snapshot_name(second, fmt)))


class Snapshot(list):
    """A snapshot's rows as dicts, with `lines` (the rows as text, format 1's to the byte), `ids`,
    `fractions` (None where the record does not say), `refused`, `refused_ids`, `notes`, `source`,
    `fmt` and `second`."""


def genome_index(run, ids=None):
    """id -> genome row as text, from genomes.jsonl.gz: every id, or those asked for.

    For a reader that joins many snapshots of one run: build this once and pass it to snapshot()
    as `genomes`, rather than walk the file per snapshot. Memory is the inflated file.
    """
    path = os.path.join(run, GENOMES)
    found = {}
    if not os.path.isfile(path):
        return found

    wanted = set(ids) if ids is not None else None
    if wanted is not None and not wanted:
        return found
    torn = []
    for line in member_lines(path, torn):
        ident = id_of(line)
        if ident is None or ident in found:
            continue
        if wanted is not None and ident not in wanted:
            continue
        found[ident] = line
        if wanted is not None and len(found) == len(wanted):
            break

    _say_once(run, 'genomes', '%s: %s (record format 2), %d genome(s) indexed'
              % (os.path.basename(run), GENOMES, len(found)))
    return found


def snapshot(run, second, fmt=None, genomes=None, ids=None):
    """The snapshot at `second` as a Snapshot, or None when the record holds none there.

    Format 1 is the file's rows. Format 2 reads the slim rows and joins each to its genome from
    `genomes` (a genome_index) or, without one, from one walk of genomes.jsonl.gz for the ids it
    names. A slim row with no genome is refused and counted, and its id listed.

    `ids` (a set) keeps only those bodies' rows and parses no other: a reader after a handful of
    bodies in a crowd of thousands pays for the handful. The refusal count is then over the ids
    kept.
    """
    fmt, how = record_format(run, fmt)
    path = os.path.join(run, SNAPSHOTS, snapshot_name(second, fmt))
    if not os.path.isfile(path):
        return None

    snap = Snapshot()
    snap.fmt = fmt
    snap.second = int(second)
    snap.notes = []
    snap.refused = 0
    snap.refused_ids = []
    snap.lines = []
    snap.ids = []
    snap.fractions = []

    if fmt == JSONL:
        for line in _jsonl_lines(path):
            ident = id_of(line)
            if ids is not None and ident not in ids:
                continue
            snap.lines.append(line)
            snap.ids.append(ident)
            snap.fractions.append(None)
            snap.append(json.loads(line))
        snap.source = '%s/%s (record format 1: %s)' % (SNAPSHOTS, os.path.basename(path), how)
        _say_once(run, 'snapshots', '%s: snapshots from %s/*.jsonl, record format 1 (%s)'
                  % (os.path.basename(run), SNAPSHOTS, how))
        return snap

    slim = []
    slim_ids = []
    for s in member_lines(path, snap.notes):
        ident = id_of(s)
        if ids is not None and ident not in ids:
            continue
        slim.append(s)
        slim_ids.append(ident)

    if genomes is None:
        genomes = genome_index(run, [i for i in slim_ids if i is not None])

    for s, ident in zip(slim, slim_ids):
        g = genomes.get(ident) if ident is not None else None
        if g is None:
            snap.refused += 1
            snap.refused_ids.append(ident)
            continue

        line = join_line(s, g)
        row = json.loads(line)
        bf = body_fraction_of(s)
        if bf is not None:
            row['bf'] = bf

        snap.lines.append(line)
        snap.ids.append(ident)
        snap.fractions.append(bf)
        snap.append(row)

    if snap.refused:
        say('%s: snapshot %d: %d slim row(s) with no genome in %s, refused and counted (first ids %s)'
            % (os.path.basename(run), int(second), snap.refused, GENOMES, snap.refused_ids[:5]))

    snap.source = '%s/%s joined to %s (record format 2: %s)' % (
        SNAPSHOTS, os.path.basename(path), GENOMES, how)
    _say_once(run, 'snapshots', '%s: snapshots from %s/*.jsonl.gz joined to %s, record format 2 (%s)'
              % (os.path.basename(run), SNAPSHOTS, GENOMES, how))
    return snap


def genomes(run, ids=None, fmt=None):
    """id -> genome row (dict, the id included), with `source`.

    Format 2 reads genomes.jsonl.gz, which holds every body admitted. Format 1 has no such file:
    the answer is the union of the snapshots (the first snapshot holding a body gives its genome,
    without the plan fields), which misses every body born and dead between two snapshots, and
    `source` says so.
    """
    fmt, how = record_format(run, fmt)
    out = _Genomes()
    out.fmt = fmt

    if fmt == COMPACT:
        for ident, line in genome_index(run, ids).items():
            out[ident] = json.loads(line)
        out.source = '%s (record format 2: %s)' % (GENOMES, how)
        return out

    wanted = set(ids) if ids is not None else None
    for s in snapshot_seconds(run, JSONL):
        path = os.path.join(run, SNAPSHOTS, snapshot_name(s, JSONL))
        for line in _jsonl_lines(path):
            ident = id_of(line)
            if ident is None or ident in out or (wanted is not None and ident not in wanted):
                continue
            out[ident] = json.loads(genome_line_of(line))
        if wanted is not None and len(out) == len(wanted):
            break

    out.source = ('the union of the snapshots (record format 1: %s; a body born and dead between '
                  'two snapshots has no genome)' % how)
    _say_once(run, 'genomes', '%s: genomes from %s' % (os.path.basename(run), out.source))
    return out


class _Genomes(dict):
    pass


# ---------------------------------------------------------------- positions

def positions_lines(run, fmt=None):
    """Every complete positions row as text, in order. Rows carries `source` and `notes`."""
    fmt, how = record_format(run, fmt)
    name = POSITIONS_GZ if fmt == COMPACT else POSITIONS
    path = os.path.join(run, name)

    def make(notes):
        if not os.path.isfile(path):
            return iter(())
        _say_once(run, 'positions', '%s: positions from %s, record format %d (%s)'
                  % (os.path.basename(run), name, fmt, how))
        return member_lines(path, notes) if fmt == COMPACT else _jsonl_lines(path)

    return Rows(name + ' (record format %d: %s)' % (fmt, how), fmt, how, make)


def positions(run, fmt=None):
    """Every complete positions row as a dict: {"t", "n", "b": [[id, x, y, z, flags], ...]}."""
    lines = positions_lines(run, fmt)

    def make(notes):
        for line in lines._make(notes):
            yield json.loads(line)

    return Rows(lines.source, lines.fmt, lines.how, make)


def has_positions(run, fmt=None):
    fmt, _ = record_format(run, fmt)
    return os.path.isfile(os.path.join(run, POSITIONS_GZ if fmt == COMPACT else POSITIONS))


# ---------------------------------------------------------------- poses

_poses_read = None


def _stream_reader():
    """scripts/poses-read.py, loaded as a module: the stream's own reader, independent of the C#."""
    global _poses_read
    if _poses_read is None:
        path = os.path.join(SCRIPTS, 'poses-read.py')
        spec = importlib.util.spec_from_file_location('poses_read', path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        for name in ('read_header', 'scan', 'read_frame'):
            if not hasattr(module, name):
                raise Refusal('scripts/poses-read.py has no %s(), which runrec reads the stream through' % name)
        _poses_read = module
    return _poses_read


def _stream_frames(run):
    """[(seconds, offset, payload bytes)] of poses.bin, scanned, and the open reader module."""
    reader = _stream_reader()
    path = os.path.join(run, STREAM)
    with open(path, 'rb') as f:
        reader.read_header(f)
        return reader.scan(f, os.path.getsize(path))


def _stream_frame(run, offset, payload):
    reader = _stream_reader()
    with open(os.path.join(run, STREAM), 'rb') as f:
        reader.read_header(f)
        seconds, bodies = reader.read_frame(f, offset, payload)
    return {'t': seconds, 'bodies': bodies}


def poses_source(run):
    """Which file poses() reads: poses.jsonl, poses.bin, or None."""
    if os.path.isfile(os.path.join(run, POSES)):
        return POSES
    if os.path.isfile(os.path.join(run, STREAM)):
        return STREAM
    return None


def poses(run, second=None, tolerance=1e-3, keep=None):
    """Poses rows {'t', 'bodies': [{'id', 'p', 'r', 'q'} ...]}: every row as a Rows when `second`
    is None, else the row at that second or None.

    From poses.jsonl when the run has one, else from the state stream, whose bodies also carry
    'bodyFraction' and whose numbers are the solver's floats rather than the JSONL's rounding.
    `keep(index, t)`, when given, decides which rows are parsed at all: a row it refuses is
    skipped on its time alone, which is what a reader taking every tenth row wants.
    """
    source = poses_source(run)

    if source == POSES:
        path = os.path.join(run, POSES)
        _say_once(run, 'poses', '%s: poses from %s' % (os.path.basename(run), POSES))

        if second is None:
            def make_jsonl(notes):
                for k, line in enumerate(_jsonl_lines(path)):
                    if keep is not None:
                        end = line.find(',', 5)
                        try:
                            t = float(line[5:end])
                        except ValueError:
                            continue
                        if not keep(k, t):
                            continue
                    yield json.loads(line)
            return Rows(POSES, JSONL, 'poses.jsonl present', make_jsonl)

        for line in _jsonl_lines(path):
            end = line.find(',', 5)
            try:
                t = float(line[5:end])
            except ValueError:
                continue
            if abs(t - second) <= tolerance:
                return json.loads(line)
        return None

    if source == STREAM:
        _say_once(run, 'poses', '%s: poses from %s (the state stream, through scripts/poses-read.py)'
                  % (os.path.basename(run), STREAM))
        frames = _stream_frames(run)

        if second is None:
            def make(notes):
                reader = _stream_reader()
                with open(os.path.join(run, STREAM), 'rb') as f:
                    reader.read_header(f)
                    for k, (seconds, offset, payload) in enumerate(frames):
                        if keep is not None and not keep(k, seconds):
                            continue
                        t, bodies = reader.read_frame(f, offset, payload)
                        yield {'t': t, 'bodies': bodies}
            return Rows(STREAM, COMPACT, 'poses.bin present, no poses.jsonl', make)

        for seconds, offset, payload in frames:
            if abs(seconds - second) <= tolerance:
                return _stream_frame(run, offset, payload)
        return None

    if second is None:
        return Rows('no poses', None, 'neither poses.jsonl nor poses.bin', lambda notes: iter(()))
    return None


# ---------------------------------------------------------------- checkpoints

def _read_string(data, at):
    """A BinaryWriter string: a 7-bit-encoded length, then UTF-8."""
    length = 0
    shift = 0
    while True:
        b = data[at]
        at += 1
        length |= (b & 0x7f) << shift
        if not b & 0x80:
            break
        shift += 7
    return data[at:at + length].decode('utf-8'), at + length


def checkpoint_header(path):
    """A checkpoint's header as a dict, and whether the file is whole. Reads the header and the
    trailer; the payload is not inflated or digested (the farm's reader does that)."""
    size = os.path.getsize(path)
    with open(path, 'rb') as f:
        head = f.read(min(size, 4096))

    if head[:8] != b'EVOCKPT\x00':
        raise Refusal('%s is not a checkpoint' % path)

    version, magic_bytes = struct.unpack_from('<HH', head, 8)
    if version not in (4, 5):
        raise Refusal('%s is checkpoint version %d and this reader reads 4 and 5' % (path, version))

    seconds, seed, steps, dt, per = struct.unpack_from('<dQqfi', head, 12)
    at = 12 + 32
    names = ['configHash', 'coreHash', 'dynamicsHash', 'farmHash', 'engineVersion', 'sourceArm', 'sourceRun']
    out = {'path': path, 'version': version, 'seconds': seconds, 'seed': seed, 'physicsSteps': steps,
           'physicsStepSeconds': dt, 'stepsPerMetabolicStep': per}
    for name in names:
        out[name], at = _read_string(head, at)

    report_every, pose_every, digest_every, ckpt_every = struct.unpack_from('<idqd', head, at)
    at += 28
    payload, digest = struct.unpack_from('<qQ', head, at)
    at += 16
    stored = payload
    if version == 5:
        stored, = struct.unpack_from('<q', head, at)
        at += 8

    out.update({'reportEvery': report_every, 'poseEverySeconds': pose_every,
                'digestEverySteps': digest_every, 'checkpointEverySeconds': ckpt_every,
                'payloadBytes': payload, 'payloadDigest': digest, 'storedBytes': stored,
                'compressed': version == 5, 'fileBytes': size})

    whole = at + stored + 16 == size
    if whole:
        with open(path, 'rb') as f:
            f.seek(at + stored)
            trailer = f.read(16)
        whole = struct.unpack_from('<q', trailer, 0)[0] == stored and trailer[8:] == b'EVOCKPT\x00'
    out['whole'] = whole
    return out


def checkpoints(run):
    """Every checkpoint in a run, ascending by second, as header dicts (checkpoint_header)."""
    d = os.path.join(run, CHECKPOINTS)
    if not os.path.isdir(d):
        return []
    out = []
    for name in sorted(os.listdir(d)):
        if name.endswith('.ckpt') and name[:-5].isdigit():
            try:
                out.append(checkpoint_header(os.path.join(d, name)))
            except (Refusal, struct.error, IndexError, UnicodeDecodeError) as e:
                out.append({'path': os.path.join(d, name), 'seconds': float(name[:-5]),
                            'error': str(e), 'whole': False})
    return out


# ---------------------------------------------------------------- a quick look

def describe(run):
    """A few lines on what a run's record holds."""
    fmt, how = record_format(run)
    lines = ['%s: record format %d (%s)' % (run, fmt, how)]
    seconds = snapshot_seconds(run)
    lines.append('snapshots: %d%s' % (len(seconds), (' (%d to %d s)' % (seconds[0], seconds[-1])) if seconds else ''))
    lines.append('positions: %s' % ('yes' if has_positions(run) else 'none'))
    lines.append('poses: %s' % (poses_source(run) or 'none'))
    lines.append('checkpoints: %d' % len(checkpoints(run)))
    return '\n'.join(lines)


if __name__ == '__main__':
    if len(sys.argv) != 2:
        print('python scripts/reads/runrec.py <run-or-arm-directory>', file=sys.stderr)
        sys.exit(2)
    print(describe(run_directory(sys.argv[1])))
