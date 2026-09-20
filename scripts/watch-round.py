"""One look at a round, then exit. Never a loop.

    python scripts/watch-round.py r41e [--read scripts/reads/r41d-read.py] [--marks 3000,5000,10000,15000,30000]

Prints only what is new since the last look: queue lines, error signatures and the footer in each
arm's Unity log, a manifest that has left `running`, a living count past the stop rule's warning,
a stall suspicion (the report's byte size flat for thirty minutes on a running arm; a suspicion,
confirm with CPU before acting, CLAUDE.md's wedge gotcha), and the round's read the first time a
seed's report passes each mark. State lives in scratch/logs/<round>-watch.json.

Why one look and not a loop (2026-09-20): a background shell loop outlives the session that armed
it, every re-arm adds a copy, and on this machine an orphaned Git Bash loop opens a Windows
Terminal tab for every child it forks. Dozens of copies took the desktop down and cost round 41e
its arms. The schedule belongs to the session (its cron or wake-up), which cannot orphan; this
script is one process, forks only the read, and ends.
"""
import argparse, glob, json, os, re, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
QUEUE = re.compile(r'launching|launched|FAILED|refus|error|every seed|exited|stopped', re.I)
LOG = re.compile(r'error CS|Exception|could not be found|\*\*Ended')
STALL_SECONDS = 1800
COUNT_WARNING = 3500


def last_row(report):
    t = alive = None
    with open(report, encoding='utf-8', errors='replace') as f:
        for line in f:
            if re.match(r'^\| *\d', line):
                cells = [c.strip() for c in line.split('|')]
                try:
                    t, alive = int(float(cells[1])), int(cells[2])
                except (ValueError, IndexError):
                    pass
    return t, alive


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('round')
    ap.add_argument('--read', default=None, help='a read script taking <second> <arm>')
    ap.add_argument('--marks', default='3000,5000,10000,15000,30000')
    ap.add_argument('--seeds', default='1,2,3,4,5')
    a = ap.parse_args()
    marks = [int(m) for m in a.marks.split(',')]
    os.makedirs(f'{ROOT}/scratch/logs', exist_ok=True)
    state_path = f'{ROOT}/scratch/logs/{a.round}-watch.json'
    state = json.load(open(state_path, encoding='utf-8')) if os.path.exists(state_path) else {}
    seen = set(state.get('seen', []))
    sizes = state.get('sizes', {})
    now = time.time()
    out = []

    def emit(line):
        if line not in seen:
            seen.add(line)
            out.append(line)

    queue = f'{ROOT}/scratch/logs/{a.round}-queue.out'
    if os.path.exists(queue):
        for line in open(queue, encoding='utf-8', errors='replace'):
            if QUEUE.search(line):
                emit('queue: ' + line.strip())

    for s in a.seeds.split(','):
        arm = f'{a.round}-s{s}'
        log = f'{ROOT}/scratch/logs/evosim-{arm}.log'
        if os.path.exists(log):
            hits = [l.strip() for l in open(log, encoding='utf-8', errors='replace') if LOG.search(l)]
            if hits:
                emit(f'{arm} log: {hits[-1][:300]}')
        runs = sorted(glob.glob(f'{ROOT}/runs/{arm}/2026*'))
        report = f'{ROOT}/runs/{arm}.md'
        if not runs or not os.path.exists(report):
            continue
        try:
            manifest = json.load(open(runs[-1] + '/run.json', encoding='utf-8'))
        except (OSError, ValueError):
            manifest = {}
        status = manifest.get('status', '?')
        if status != 'running':
            emit(f'{arm} manifest: {status} {manifest.get("reason") or ""}'.strip())
        t, alive = last_row(report)
        if alive is not None and alive > COUNT_WARNING:
            emit(f'{arm} count: alive {alive} at {t} s (stop rule at 4,000)')
        if t is not None and a.read:
            for m in marks:
                key = f'{arm} reached {m}'
                if t >= m and key not in seen:
                    seen.add(key)
                    r = subprocess.run([sys.executable, f'{ROOT}/{a.read}', str(m), arm],
                                       capture_output=True, text=True, timeout=300)
                    out.append(f'{key} s:\n{(r.stdout or r.stderr).rstrip()}')
        size = os.path.getsize(report)
        rec = sizes.get(arm)
        if rec is None or rec['size'] != size:
            sizes[arm] = {'size': size, 'since': now}
        elif status == 'running' and now - rec['since'] >= STALL_SECONDS:
            out.append(f'{arm} stall suspicion: report flat at {size} bytes for {(now - rec["since"]) / 60:.0f} min '
                       f'(last row {t} s; confirm with CPU before acting)')
            sizes[arm]['since'] = now
        if t is not None:
            out_status = f'{arm}: {t} s, alive {alive}, {status}'
            state.setdefault('last', {})[arm] = out_status

    state['seen'] = sorted(seen)
    state['sizes'] = sizes
    json.dump(state, open(state_path, 'w', encoding='utf-8'), indent=1)
    print('\n'.join(out) if out else 'nothing new')
    print('now: ' + '; '.join(state.get('last', {}).values()))


if __name__ == '__main__':
    main()
