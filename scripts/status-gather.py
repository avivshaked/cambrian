"""Gathers, in one call, the facts the coordinator needs to write the owner a status message, as plain text in
eleven sections (window, runs, pace, heat, hardware events, processes and memory, disk and orphans, git, new files,
logs, queue). A section whose source fails prints one line saying what failed, and the rest goes on.

Usage: python -I scripts/status-gather.py [--since "YYYY-MM-DD HH:MM"] [--mark]   (from the repository root)

--since is local time. Without it the window starts at the time in scratch/status/last-status.txt (one line,
YYYY-MM-DD HH:MM:SS), or 12 hours ago when that file is absent. --mark writes the current local time to that file after
printing; it is the only write this script makes.

Reads: runs/*/*/run.json (and the tail of a running world's stats.jsonl), the one-second hardware log through
scripts/reads/temp-curve.py's own main(), the System event log (wevtutil), process and memory counters (one pwsh call),
git, logbook/, proposals/, scratch/owner/, scratch/logs/ and scratch/wt-*/scratch/logs/, scratch/machine/queue.md.
Runs scripts/pace-check.py and scripts/sweep-orphans.ps1 (list mode) as subprocesses, each with a timeout.
Runs at below-normal priority, with no threads. Standard library only.
"""
import argparse, contextlib, datetime as dt, glob, importlib.util, io, json, os, re, subprocess, sys, time

T0 = time.time()
ROOT = os.getcwd()
MARK = os.path.join("scratch", "status", "last-status.txt")


def rel(p):
    try:
        r = os.path.relpath(p, ROOT)
        return r if not r.startswith("..") else p
    except ValueError:
        return p


def cut(s, n):
    s = " ".join(str(s).split())
    return s if len(s) <= n else s[:n - 3] + "..."


def head(name):
    print(f"\n== {name}")


def run(cmd, timeout=60):
    """(output, None) or (None, reason)."""
    try:
        p = subprocess.run(cmd, capture_output=True, timeout=timeout, cwd=ROOT)
    except subprocess.TimeoutExpired:
        return None, f"timed out after {timeout} s"
    except OSError as e:
        return None, f"could not start: {e}"
    out = (p.stdout.decode("utf-8", "replace") + p.stderr.decode("utf-8", "replace")).strip()
    return out, None


def section(fn, *args):
    try:
        fn(*args)
    except Exception as e:  # a section never takes the others down
        print(f"(this section failed: {type(e).__name__}: {cut(e, 150)})")


def parse_iso(s):
    """ISO time with optional offset or Z, as a local naive datetime."""
    s = s.strip().replace("Z", "+00:00")
    m = re.match(r"^(.*?\.\d{6})\d*(.*)$", s)  # fractional seconds beyond microseconds
    if m:
        s = m.group(1) + m.group(2)
    d = dt.datetime.fromisoformat(s)
    return d.astimezone().replace(tzinfo=None) if d.tzinfo else d


def tail_lines(path, nbytes=65536):
    size = os.path.getsize(path)
    with open(path, "rb") as f:
        f.seek(max(0, size - nbytes))
        data = f.read()
    return [l for l in data.decode("utf-8", "replace").splitlines() if l.strip()]


# 1
def s_window(start, now):
    head("window")
    print(f"since {start:%Y-%m-%d %H:%M:%S}  now {now:%Y-%m-%d %H:%M:%S}  length {(now - start).total_seconds() / 3600:.1f} h")


# 2
def s_runs(start, now):
    head("runs")
    running, ended = [], []
    n = 0
    for m in glob.glob(os.path.join("runs", "*", "*", "run.json")):
        n += 1
        try:
            mt = dt.datetime.fromtimestamp(os.path.getmtime(m))
            with open(m, encoding="utf-8") as f:
                text = f.read()
            # an old file is parsed only when it says running; reading 350 small files costs milliseconds
            if mt < start and '"status": "running"' not in text and '"status":"running"' not in text:
                continue
            j = json.loads(text)
        except (OSError, ValueError):
            continue
        d = os.path.dirname(m)
        if j.get("status") == "running":
            sim = j.get("simulatedSeconds")
            if sim is None:
                try:
                    for l in reversed(tail_lines(os.path.join(d, "stats.jsonl"))):
                        try:
                            r = json.loads(l)
                        except ValueError:
                            continue
                        if "t" in r:
                            sim = r["t"]
                            break
                except OSError:
                    pass
            running.append((j.get("arm", os.path.basename(os.path.dirname(d))), sim, rel(d)))
        elif j.get("endedAt"):
            try:
                e = parse_iso(j["endedAt"])
            except ValueError:
                continue
            if start <= e <= now:
                ended.append((e, j, d))
    print(f"running worlds: {len(running)}")
    for arm, sim, d in sorted(running):
        print(f"  {arm}  simulated {sim if sim is not None else '?'} s  ({d})")
    print(f"runs ended in the window: {len(ended)}  (of {n} run dirs)")
    for e, j, d in sorted(ended, key=lambda x: x[0]):
        tr = j.get("timesRealTime")
        print(f"  {e:%m-%d %H:%M}  {j.get('arm')}  {os.path.basename(d)}  status {j.get('status')}  reason {j.get('reason')}  "
              f"sim {j.get('simulatedSeconds')} s  alive {j.get('aliveAtEnd')}  "
              f"{'%.2fx' % tr if isinstance(tr, (int, float)) else '?x'}  | {cut(j.get('ending', ''), 160)}")


# 3
def s_pace():
    head("pace")
    out, err = run([sys.executable, "-I", os.path.join("scripts", "pace-check.py")])
    print(err and f"pace-check failed: {err}" or out or "(no output)")


# 4
def s_heat(start, now):
    head("heat")
    p = os.path.join(ROOT, "scripts", "reads", "temp-curve.py")
    spec = importlib.util.spec_from_file_location("temp_curve", p)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        rc = mod.main(["temp-curve", f"{start:%Y-%m-%d %H:%M:%S}", f"{now:%Y-%m-%d %H:%M:%S}"])
    lines = buf.getvalue().splitlines()
    if rc != 0:
        print(cut(lines[0] if lines else "temp-curve returned an error", 200))
        return
    first = re.match(r"(\d+) rows from (.+?) to (.+)$", lines[0])
    print(lines[0])
    if first:
        t0 = dt.datetime.fromisoformat(first.group(2))
        if (t0 - start).total_seconds() > 120:
            print(f"(the log reads only from {t0:%Y-%m-%d %H:%M:%S}, after the window's start)")
    for l in lines[1:2]:
        print(l)


# 5
def s_events(start, now):
    head("hardware events")
    u = start.astimezone().astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.000Z")
    q = (f"*[System[(Provider[@Name='nvlddmkm'] or (Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41) "
         f"or (Provider[@Name='EventLog'] and EventID=6008) or Provider[@Name='Microsoft-Windows-WindowsUpdateClient']) "
         f"and TimeCreated[@SystemTime>='{u}']]]")
    out, err = run(["wevtutil", "qe", "System", f"/q:{q}", "/f:text", "/c:30", "/rd:true"])
    if err:
        print(f"wevtutil failed: {err}")
    elif not out:
        print("no events in the window")
    elif out.startswith("Failed") or "Access is denied" in out[:200]:
        print(f"wevtutil said: {cut(out, 200)}")
    else:
        n = 0
        for blk in re.split(r"^Event\[\d+\]:?\s*$", out, flags=re.M)[1:]:
            g = lambda k: (re.search(rf"^\s*{k}:\s*(.*)$", blk, re.M) or [None, ""])[1].strip()
            desc = blk.split("Description:", 1)[1] if "Description:" in blk else ""
            d = g("Date")
            try:  # the text format gives UTC with a Z; show local time
                d = f"{dt.datetime.fromisoformat(d[:19] + '+00:00').astimezone():%Y-%m-%d %H:%M:%S}"
            except ValueError:
                d = d[:19]
            print(f"  {d}  {g('Source')}  id {g('Event ID')}  {cut(desc, 120)}")
            n += 1
        if n >= 30:
            print("  (cut at 30 events, newest first)")
        if n == 0:
            print(cut(out, 200))
    out, err = run(["pwsh", "-NoProfile", "-Command", "(Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToString('yyyy-MM-dd HH:mm:ss')"])
    print(f"last boot: {err or out}")


# 6
PS = r"""
$o = Get-CimInstance Win32_OperatingSystem
$lim = $o.TotalVirtualMemorySize / 1MB
$used = ($o.TotalVirtualMemorySize - $o.FreeVirtualMemory) / 1MB
$all = Get-Process
$names = 'dotnet','Evosim.Farm','Unity','python','pwsh','bash','node','claude'
$counts = @{}
foreach ($n in $names) { $counts[$n] = @($all | Where-Object { $_.ProcessName -eq $n }).Count }
$top = $all | Sort-Object PrivateMemorySize64 -Descending | Select-Object -First 5 | ForEach-Object {
  @{ name = $_.ProcessName; id = $_.Id; gb = [math]::Round($_.PrivateMemorySize64 / 1GB, 2) } }
@{ used = [math]::Round($used, 1); limit = [math]::Round($lim, 1); counts = $counts; top = @($top) } | ConvertTo-Json -Compress -Depth 4
"""


def s_procs():
    head("processes and memory")
    out, err = run(["pwsh", "-NoProfile", "-Command", PS])
    if err:
        print(f"process query failed: {err}")
        return
    j = json.loads(out[out.index("{"):])
    print(f"committed {j['used']} GB of limit {j['limit']} GB")
    print("counts: " + ", ".join(f"{k} {v}" for k, v in j["counts"].items()))
    print("largest private memory: " + "; ".join(f"{t['name']} (pid {t['id']}) {t['gb']} GB" for t in j["top"]))


# 7
def s_orphans():
    head("disk and orphans")
    out, err = run(["pwsh", "-NoProfile", "-File", os.path.join("scripts", "sweep-orphans.ps1")], 120)
    print(err and f"sweep-orphans failed: {err}" or out or "(no output)")


# 8
def s_git(start, now):
    head("git")
    g = lambda *a: run(["git", *a])
    out, err = g("reflog", "show", "--date=iso", "-1", "refs/remotes/origin/main")
    when, label = None, ""
    if out and not err:
        m = re.search(r"\{(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d [+-]\d{4})\}", out)
        if m:
            when, label = dt.datetime.strptime(m.group(1), "%Y-%m-%d %H:%M:%S %z"), "origin/main last updated (reflog)"
    if when is None:
        out, err = g("log", "-1", "--format=%cI", "origin/main")
        if out and not err:
            when, label = dt.datetime.fromisoformat(out.strip()), "origin/main committer date (no reflog)"
    if when:
        days = (dt.datetime.now(dt.timezone.utc) - when).total_seconds() / 86400
        print(f"{label}: {when:%Y-%m-%d %H:%M} ({days:.1f} days ago)")
    else:
        print("origin/main time unknown")
    out, err = g("rev-list", "--count", "origin/main..main")
    print(f"unpushed commits (origin/main..main): {err or out}")
    out, err = g("log", "--all", f"--since={start:%Y-%m-%d %H:%M:%S}", "--format=%h %ad %D %s", "--date=format:%H:%M")
    if err:
        print(f"git log failed: {err}")
        return
    lines = out.splitlines()
    print(f"commits on all branches in the window: {len(lines)}")
    for l in lines[:40]:
        print("  " + cut(l, 220))
    if len(lines) > 40:
        print(f"  ({len(lines) - 40} more cut)")


# 9
def s_new(start, now):
    head("new files")
    st = start.timestamp()

    def walk(top):
        for dp, dn, fn in os.walk(top):
            for f in fn:
                p = os.path.join(dp, f)
                try:
                    mt = os.path.getmtime(p)
                except OSError:
                    continue
                if mt >= st:
                    yield mt, p

    for top in ("logbook", "proposals"):
        fs = sorted(walk(top), reverse=True)
        print(f"{top}/: {len(fs)} modified")
        for mt, p in fs[:3 if top == "proposals" else 6]:
            print(f"  {dt.datetime.fromtimestamp(mt):%m-%d %H:%M}  {rel(p)}")
        if top == "logbook" and len(fs) > 6:
            print(f"  ({len(fs) - 6} more)")
    groups = {}
    for mt, p in walk(os.path.join("scratch", "owner")):
        parts = os.path.relpath(p, os.path.join("scratch", "owner")).split(os.sep)
        groups.setdefault(parts[0] if len(parts) > 1 else "(loose files)", []).append((mt, p))
    print(f"scratch/owner/: {sum(len(v) for v in groups.values())} modified in {len(groups)} folders")
    for k, v in sorted(groups.items(), key=lambda kv: -max(x[0] for x in kv[1])):
        v.sort(reverse=True)
        print(f"  {k}: {len(v)} files; newest: " + ", ".join(os.path.basename(p) for _, p in v[:3]))


# 10
def s_logs(start, now):
    head("logs")
    st = start.timestamp()
    fs = []
    for pat in (os.path.join("scratch", "logs", "*.log"), os.path.join("scratch", "wt-*", "scratch", "logs", "*.log")):
        for p in glob.glob(pat):
            try:
                mt = os.path.getmtime(p)
            except OSError:
                continue
            if mt >= st:
                fs.append((mt, p))
    fs.sort(reverse=True)
    print(f"{len(fs)} log files modified in the window" + (" (newest 15 shown)" if len(fs) > 15 else ""))
    for mt, p in fs[:15]:
        print(f"  {rel(p)}  {dt.datetime.fromtimestamp(mt):%m-%d %H:%M}")
        try:
            for l in tail_lines(p, 8192)[-2:]:
                print("      " + cut(l, 200))
        except OSError as e:
            print(f"      (unreadable: {e})")


# 11
def s_queue():
    head("queue")
    p = os.path.join("scratch", "machine", "queue.md")
    with open(p, encoding="utf-8") as f:
        lines = f.read().splitlines()
    out, on = [], False
    for l in lines:
        if l.startswith("## Running"):
            on = True
        elif on and l.startswith("## "):
            break
        if on:
            out.append(l)
    print("\n".join(out) if out else "(no '## Running' heading in scratch/machine/queue.md)")


def main():
    if sys.platform == "win32":
        try:
            import ctypes
            ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)
        except Exception:
            pass
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    a = argparse.ArgumentParser()
    a.add_argument("--since")
    a.add_argument("--mark", action="store_true")
    o = a.parse_args()
    now = dt.datetime.now().replace(microsecond=0)
    if o.since:
        start = dt.datetime.strptime(o.since, "%Y-%m-%d %H:%M")
    else:
        try:
            with open(MARK, encoding="utf-8") as f:
                start = dt.datetime.strptime(f.read().strip(), "%Y-%m-%d %H:%M:%S")
        except (OSError, ValueError):
            start = now - dt.timedelta(hours=12)
    section(s_window, start, now)
    section(s_runs, start, now)
    section(s_pace)
    section(s_heat, start, now)
    section(s_events, start, now)
    section(s_procs)
    section(s_orphans)
    section(s_git, start, now)
    section(s_new, start, now)
    section(s_logs, start, now)
    section(s_queue)
    if o.mark:
        os.makedirs(os.path.dirname(MARK), exist_ok=True)
        with open(MARK, "w", encoding="utf-8") as f:
            f.write(dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S") + "\n")
        print(f"\nmarked {MARK}")
    print(f"\nrun time {time.time() - T0:.1f} s")


if __name__ == "__main__":
    main()
