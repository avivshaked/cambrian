#!/usr/bin/env python3
"""Join a run that stopped (a crash, a kill) and its continuation from a checkpoint into one run
directory that the round readers read like any other: `runs/<out arm>/<run>/` and
`runs/<out arm>.md`. Nothing in either source is changed.

A continuation (`run-farm.ps1 -ResumeFrom <arm> -At <T>`) writes the same rows from T on as the
unbroken run would have (the checkpoint spec's acceptance, and compare-det on the shared samples),
so the join takes the stopped run's rows at or before T and the continuation's after it, and drops
a continuation row that is byte-equal to one already taken (queued lineage rows the checkpoint
carried). A row that is not valid JSON (the stopped run's torn last line, or the zeros a crash
leaves at a file's end) is dropped and counted.

Joined: config.json (the stopped run's), run.json (the continuation's, with a `joined` block),
stats.jsonl, lineage.jsonl, absorptive.jsonl, positions.jsonl.gz, genomes.jsonl.gz (by line, both
kept, duplicates dropped), snapshots/ and fields/ (hard links: the stopped run's files at or before
T, then the continuation's), pool/ (the stopped run's), and the report's table rows. Not joined:
poses.bin (binary, per sample, left out, and a reader that needs it says so) and checkpoints/.

Usage: python scripts/stitch-resume.py <stopped arm> <continuation arm> <T seconds> <out arm>
       [--runs-root runs]
       python scripts/stitch-resume.py --fix-gz <joined run dir>   (a join made before the member writer)
"""
import argparse, gzip, json, os, re, shutil, sys

def run_dir(root, arm):
    ds = sorted(d for d in os.listdir(os.path.join(root, arm)) if os.path.isfile(os.path.join(root, arm, d, 'run.json')))
    return os.path.join(root, arm, ds[-1])

def rows_t(path, opener=open):
    bad = 0
    with opener(path, 'rt', encoding='utf-8', errors='replace') as f:
        for line in f:
            s = line.rstrip('\n').rstrip('\r')
            if not s.strip() or s.lstrip('\x00').strip() == '':
                continue
            try:
                r = json.loads(s)
            except ValueError:
                bad += 1
                continue
            yield s, r.get('t') if isinstance(r, dict) else None
    if bad:
        print(f'  {os.path.basename(path)}: {bad} unreadable line(s) dropped')

class MemberWriter:
    """Writes record format 2's gzip (Core's GzipMembers): plain gzip members, each with an extra
    field (subfield EV, 8 bytes) holding the member's whole length, so Core's reader can walk the
    file member by member. Python's gzip writes no such field and Core refuses its files, which is
    what the first stitch (r52-s3j, 2026-09-29) left behind."""

    def __init__(self, path, member_bytes=1 << 20):
        self.f = open(path, 'wb')
        self.buf = []
        self.size = 0
        self.member_bytes = member_bytes

    def write(self, text):
        b = text.encode('utf-8')
        self.buf.append(b)
        self.size += len(b)
        if self.size >= self.member_bytes:
            self.flush()

    def flush(self):
        if not self.buf:
            return
        plain = gzip.compress(b''.join(self.buf), mtime=0)
        assert plain[3] == 0, 'a plain member with header flags'
        extra = (12).to_bytes(2, 'little') + b'EV' + (8).to_bytes(2, 'little')
        length = len(plain) + 2 + 4 + 8
        member = plain[:3] + bytes([plain[3] | 0x04]) + plain[4:10] + extra + length.to_bytes(8, 'little') + plain[10:]
        assert len(member) == length
        self.f.write(member)
        self.buf, self.size = [], 0

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.flush()
        self.f.close()

def join_jsonl(a, c, out, cut, opener=open, keyed_by_t=True):
    seen, n_a, n_c = set(), 0, 0
    writer = MemberWriter(out) if opener is gzip.open else open(out, 'wt', encoding='utf-8', newline='\n')
    with writer as w:
        for s, t in rows_t(a, opener):
            if keyed_by_t and t is not None and t > cut:
                continue
            w.write(s + '\n'); seen.add(s); n_a += 1
        for s, t in rows_t(c, opener):
            if s in seen:
                continue
            if keyed_by_t and t is not None and t <= cut and not s.startswith('{"e"'):
                continue
            w.write(s + '\n'); n_c += 1
    print(f'  {os.path.basename(out)}: {n_a} rows from the stopped run, {n_c} from the continuation')

def link_dir(a, c, out, cut):
    os.makedirs(out, exist_ok=True)
    n = 0
    for src, keep in ((a, lambda s: s <= cut), (c, lambda s: True)):
        if not os.path.isdir(src):
            continue
        for name in sorted(os.listdir(src)):
            m = re.match(r'(\d{9})', name)
            if m and not keep(int(m.group(1))):
                continue
            dst = os.path.join(out, name)
            if os.path.exists(dst):
                os.remove(dst)
            try:
                os.link(os.path.join(src, name), dst)
            except OSError:
                shutil.copy2(os.path.join(src, name), dst)
            n += 1
    print(f'  {os.path.basename(out)}/: {n} files linked')

def fix_gz(run):
    """Rewrites a joined run's two gzip files in record format 2's member layout, line for line;
    the old file is kept beside it as <name>.plain until the new one reads back equal."""
    for name in ('positions.jsonl.gz', 'genomes.jsonl.gz'):
        path = os.path.join(run, name)
        if not os.path.exists(path):
            continue
        with open(path, 'rb') as f:
            head = f.read(4)
        if head[3] & 0x04:
            print(f'  {name}: already in members')
            continue
        plain = path + '.plain'
        os.replace(path, plain)
        n = 0
        with gzip.open(plain, 'rt', encoding='utf-8', newline='\n') as r, MemberWriter(path) as w:
            for line in r:
                w.write(line); n += 1
        with gzip.open(plain, 'rb') as x, gzip.open(path, 'rb') as y:
            assert x.read() == y.read(), f'{name}: the rewrite does not read back equal'
        os.remove(plain)
        print(f'  {name}: {n} lines rewritten in members')

def main():
    ap = argparse.ArgumentParser()
    if len(sys.argv) == 3 and sys.argv[1] == '--fix-gz':
        fix_gz(sys.argv[2])
        return
    ap.add_argument('stopped'); ap.add_argument('cont'); ap.add_argument('cut', type=float); ap.add_argument('out')
    ap.add_argument('--runs-root', default=os.path.join(os.path.dirname(__file__), '..', 'runs'))
    ap.add_argument('--header-from', default=None, help='a report whose header to take when the stopped run report is unreadable (a crash can zero it); regenerate it with a one-second launch of the same build, launcher and seed')
    a_ = ap.parse_args()
    root = os.path.abspath(a_.runs_root)
    a, c = run_dir(root, a_.stopped), run_dir(root, a_.cont)
    out = os.path.join(root, a_.out, os.path.basename(a) + '-joined')
    if os.path.exists(out):
        sys.exit(f'{out} exists; remove it first')
    os.makedirs(out)
    print(f'joining {a} (to {a_.cut:g} s) and {c} into {out}')
    shutil.copy2(os.path.join(a, 'config.json'), out)
    for name in ('stats.jsonl', 'lineage.jsonl', 'absorptive.jsonl'):
        if os.path.exists(os.path.join(a, name)):
            join_jsonl(os.path.join(a, name), os.path.join(c, name), os.path.join(out, name), a_.cut)
    join_jsonl(os.path.join(a, 'positions.jsonl.gz'), os.path.join(c, 'positions.jsonl.gz'),
               os.path.join(out, 'positions.jsonl.gz'), a_.cut, opener=gzip.open)
    join_jsonl(os.path.join(a, 'genomes.jsonl.gz'), os.path.join(c, 'genomes.jsonl.gz'),
               os.path.join(out, 'genomes.jsonl.gz'), a_.cut, opener=gzip.open, keyed_by_t=False)
    for d in ('snapshots', 'fields'):
        link_dir(os.path.join(a, d), os.path.join(c, d), os.path.join(out, d), a_.cut)
    if os.path.isdir(os.path.join(a, 'pool')):
        shutil.copytree(os.path.join(a, 'pool'), os.path.join(out, 'pool'))
    man = json.load(open(os.path.join(c, 'run.json'), encoding='utf-8'))
    man['arm'] = a_.out
    man['joined'] = {'stopped': os.path.relpath(a, root), 'continuation': os.path.relpath(c, root), 'at': a_.cut,
                     'note': 'rows at or before the cut from the stopped run, after it from the continuation; poses.bin and checkpoints not joined'}
    json.dump(man, open(os.path.join(out, 'run.json'), 'w', encoding='utf-8'), indent=2)
    # The report: the stopped run's header and table rows to the cut, the continuation's rows and footer after it.
    ra = open(os.path.join(root, a_.stopped + '.md'), encoding='utf-8', errors='replace').read().split('\n')
    rc = open(os.path.join(root, a_.cont + '.md'), encoding='utf-8', errors='replace').read().split('\n')
    def t_of(line):
        m = re.match(r'\|\s*([0-9.]+)\s*\|', line)
        return float(m.group(1)) if m else None
    head = [l for l in ra if t_of(l) is None and '\x00' not in l][:6]
    if len(head) < 3 or not any(l.startswith('engine=') for l in head):
        if not a_.header_from:
            sys.exit('the stopped run report has no readable header; pass --header-from')
        head = [l for l in open(a_.header_from, encoding='utf-8').read().split(chr(10)) if t_of(l) is None][:6]
        print(f'  header taken from {a_.header_from}: the stopped run report is unreadable, and its table rows to the cut are lost (stats.jsonl holds them)')
    body = [l for l in ra if t_of(l) is not None and t_of(l) <= a_.cut] + [l for l in rc if t_of(l) is not None and t_of(l) > a_.cut]
    tail = [l for l in rc if t_of(l) is None][6:]
    with open(os.path.join(root, a_.out + '.md'), 'w', encoding='utf-8', newline='\n') as w:
        w.write('\n'.join(head + body + tail))
    print(f'  {a_.out}.md: {len(body)} table rows')

if __name__ == '__main__':
    main()
