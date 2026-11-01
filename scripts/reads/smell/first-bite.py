"""Newborns' time to first bite: for bred grazers born in a span, the median seconds from birth to the first pose frame in
which one of their mouths has the ate bit set, and the share that die before any (the smell screen's mark 3, beside it).

Usage: python -I scripts/reads/smell/first-bite.py RUN --from 21000 --to 24000 [--window 3000] [--json]

RUN is a run directory or an arm folder (whose latest dated run is read). Reads lineage.jsonl and poses.bin only.

DEFINITIONS.
  grazer    a body born with attack and intake both above 0 (lineage `atk` and `ink`; they are 0 or 1), as the draft and
            newborn-extract.py have it.
  bred      the birth row has a parent (`p` >= 0). Founders, the floor, the trickle and the pool are left out.
  born in   --from <= birth t < --to (the last window takes its end). With --window W the span is cut into windows of
            W seconds and one line is printed for each; without it, one line for the span.
  ate       the pose stream's version 5 mask (logbook/specs/state-stream-spec.md, "Version 5"): bit k is set when the
            body's k-th eating part took matter at any step since the previous frame. A grazer "bit" in the first frame,
            at or after its birth, in which its mask is not 0. Time to first bite = that frame's second less the birth t.
            The stream's cadence (10 s in the farm's rounds) is this read's resolution, and a bite is seen up to one
            cadence after it happened.
  died first  the body's death row (lineage `e` = d) falls before any frame that shows a bite, i.e. it was never seen
            to bite. A body that bit and died within one cadence, before a frame showed the bite, counts here.
  unbitten  alive at the stream's last frame (or the span's end of reading) with no bite seen: the read ends there, so
            these are neither bites nor deaths. They are counted, and the median is over the bodies seen to bite only,
            so it is biased low by them; the share dying first is a share of ALL the span's bred grazers.

A stream older than version 5 has no ate mask: every mask reads 0, which would read as "no bites". This read REFUSES to
report on one (exit 3, with the version named) rather than report that. Exits 2 on a missing input. A run that resumed
another is read as its own lineage and stream only (the births before the resume are not in its lineage).
"""
import argparse
import importlib.util
import json
import os
import statistics
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))


def fail(msg, code=2):
    print(msg, file=sys.stderr)
    sys.exit(code)


def load_poses():
    path = os.path.join(REPO, 'scripts', 'poses-read.py')
    if not os.path.isfile(path):
        fail(path + ': missing')
    spec = importlib.util.spec_from_file_location('poses_read', path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def resolve_run(path):
    path = os.path.normpath(path)
    if not os.path.isdir(path):
        fail(path + ': not a directory')
    if os.path.isfile(os.path.join(path, 'lineage.jsonl')):
        return path
    subs = sorted(d for d in (os.path.join(path, x) for x in os.listdir(path))
                  if os.path.isfile(os.path.join(d, 'lineage.jsonl')))
    if not subs:
        fail(path + ': no run with a lineage.jsonl')
    return subs[-1]


def main():
    ap = argparse.ArgumentParser(description='Newborns\' time to first bite, from the pose stream\'s ate mask.')
    ap.add_argument('run')
    ap.add_argument('--from', dest='t_from', type=float, required=True)
    ap.add_argument('--to', dest='t_to', type=float, required=True)
    ap.add_argument('--window', type=float, default=None)
    ap.add_argument('--json', action='store_true')
    a = ap.parse_args()

    run = resolve_run(a.run)
    poses = load_poses()
    try:
        spath = poses.stream_path(run)
    except poses.Refusal as e:
        fail(str(e))
    births, deaths = {}, {}
    with open(os.path.join(run, 'lineage.jsonl'), encoding='utf-8') as f:
        for line in f:
            if not line.strip():
                continue
            r = json.loads(line)
            if r.get('e') == 'b':
                if (r.get('atk') or 0) > 0 and (r.get('ink') or 0) > 0 and r.get('p', -1) >= 0:
                    births[r['id']] = r['t']
            elif r.get('e') == 'd':
                deaths[r['id']] = r['t']
    size = os.path.getsize(spath)
    with open(spath, 'rb') as f:
        head = poses.read_header(f)
        version = head['version']
        if version < 5:
            fail('REFUSED: %s is a version %d pose stream, which carries no ate mask (every mask would read 0 and look like '
                 '"no bites"). Needs version 5.' % (spath, version), 3)
        frames = poses.scan(f, size, version)
        if not frames:
            fail(spath + ': no complete frame')
        last_t = frames[-1][0]
        lo = min((t for t in births.values() if a.t_from <= t <= a.t_to), default=None)
        first_bite = {}
        want = {i for i, t in births.items() if a.t_from <= t <= a.t_to}
        if want:
            for seconds, offset, payload in frames:
                if seconds < lo - 1e-6:
                    continue
                f.seek(offset + 8)
                buf = f.read(payload)
                count = struct.unpack_from('<I', buf, 8)[0]
                raw = struct.unpack_from('<I', buf, 12)[0]
                rec = poses.inflate(buf[16:], raw, offset)
                at = 0
                for _ in range(count):
                    ident = struct.unpack_from('<i', rec, at)[0]
                    if ident in want and ident not in first_bite and seconds >= births[ident] - 1e-6:
                        if struct.unpack_from('<I', rec, at + 49)[0]:
                            first_bite[ident] = seconds
                    at += poses.BODY_FIXED[5] + 4 * rec[at + 53]

    if a.window:
        wins, t0 = [], a.t_from
        while t0 < a.t_to - 1e-9:
            wins.append((t0, min(t0 + a.window, a.t_to)))
            t0 += a.window
    else:
        wins = [(a.t_from, a.t_to)]
    rows = []
    for n, (t0, t1) in enumerate(wins):
        last = n == len(wins) - 1
        ids = [i for i, t in births.items() if t0 <= t < t1 or (last and abs(t - t1) < 1e-9)]
        bit = [i for i in ids if i in first_bite]
        times = [first_bite[i] - births[i] for i in bit]
        # seen to bite or not, a body that died before its first bite frame (or never bit) and is dead by the stream's end
        died_first = [i for i in ids if i not in first_bite and i in deaths and deaths[i] <= last_t + 1e-9]
        unbitten = [i for i in ids if i not in first_bite and i not in died_first]
        n_ids = len(ids)
        rows.append(dict(run=os.path.basename(os.path.dirname(run)), from_s=t0, to_s=t1, bred_grazers=n_ids,
                         bit=len(bit), median_s=statistics.median(times) if times else float('nan'),
                         died_first=len(died_first), died_first_share=len(died_first) / n_ids if n_ids else float('nan'),
                         unbitten_at_end=len(unbitten), stream_version=version, stream_last_s=last_t,
                         under_30=n_ids < 30))
    for r in rows:
        if a.json:
            print(json.dumps(r))
        else:
            print('%s  %g-%g s  %d bred grazers: %d seen to bite, median %s s to first bite; %d died first (%.3f of all); '
                  '%d alive and unbitten at the stream\'s end (%g s)%s'
                  % (r['run'], r['from_s'], r['to_s'], r['bred_grazers'], r['bit'],
                     '%.1f' % r['median_s'] if r['bit'] else 'nan', r['died_first'], r['died_first_share'],
                     r['unbitten_at_end'], r['stream_last_s'], '  (under 30 grazers: not judged, D244)' if r['under_30'] else ''))
    return 0


if __name__ == '__main__':
    sys.exit(main())
