"""film-guard.ps1 with real processes: a test trip stops the whole tree of a pass, a farm worker whose parent has gone, and
nothing else. Every path is under tmp_path; -Workers and -FarmExe point there, so no real process can match.

    python -B scripts/tests/minirun.py scripts/tests/story-video-check/test_film_guard.py
"""
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

GUARD = Path(__file__).resolve().parents[3] / ".claude" / "skills" / "create-story-video" / "film-guard.ps1"
NOTHING = r"no-process-has-this-in-its-command-line-\d{9}"


def procs_with(env_var, value):
    """Process ids whose command line contains value, read by a pwsh that takes the value from the environment (so its
    own command line does not carry it)."""
    env = dict(os.environ, **{env_var: value})
    cmd = ("Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -and "
           f"$_.CommandLine.Contains($env:{env_var}) }} | ForEach-Object {{ $_.ProcessId }}")
    out = subprocess.run(["pwsh", "-NoProfile", "-Command", cmd], env=env, capture_output=True, text=True, timeout=60).stdout
    return [int(x) for x in out.split()]


def alive(pid):
    out = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/NH"], capture_output=True, text=True).stdout
    return re.search(rf"\b{pid}\b", out) is not None


def kill(pid):
    subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True)


def run_guard(tmp, *extra, workers=NOTHING):
    log, stop, done = tmp / "guard.log", tmp / "STOP", tmp / "done"
    args = ["pwsh", "-NoProfile", "-File", str(GUARD), "-Done", str(done), "-Log", str(log), "-StopFile", str(stop),
            "-Workers", workers, "-Minutes", "2", *extra]
    r = subprocess.run(args, capture_output=True, text=True, timeout=90)
    assert r.returncode == 0, r.stdout + r.stderr
    return log, stop, done


def wait_gone(pids, seconds=10):
    end = time.time() + seconds
    while time.time() < end:
        if not any(alive(p) for p in pids):
            return True
        time.sleep(0.5)
    return False


def test_trip_kills_three_level_tree_and_writes_stop(tmp_path):
    marker = f"TREEMARK{os.getpid()}X{int(time.time())}"
    inner = f"Start-Sleep 120 # {marker}"
    middle = f"pwsh -NoProfile -Command \"{inner}\" # {marker}"
    code = (f"import subprocess,time; subprocess.Popen(['pwsh','-NoProfile','-Command',{middle!r}]); "
            f"time.sleep(120) # {marker} film.py run")
    root = subprocess.Popen([sys.executable, "-c", code])
    pids = []
    try:
        end = time.time() + 30
        while time.time() < end:
            pids = procs_with("FG_MARK", marker)
            if len(pids) >= 3:
                break
            time.sleep(0.5)
        assert len(pids) >= 3, f"tree did not start: {pids}"
        log, stop, _ = run_guard(tmp_path, "-TestTrip", "-KillTree", str(root.pid))
        assert "test trip" in stop.read_text()
        assert wait_gone(pids), [p for p in pids if alive(p)]
        text = log.read_text()
        assert "STOPPED: test trip" in text and "TEST TRIP" in text.splitlines()[0]
        assert alive(os.getpid())
    finally:
        kill(root.pid)
        for p in procs_with("FG_MARK", marker):
            kill(p)


def test_guard_inside_the_tree_survives_its_trip(tmp_path):
    marker = f"INSIDEMARK{os.getpid()}X{int(time.time())}"
    log, stop, done = tmp_path / "guard.log", tmp_path / "STOP", tmp_path / "done"
    guard = ["pwsh", "-NoProfile", "-File", str(GUARD), "-Done", str(done), "-Log", str(log), "-StopFile", str(stop),
             "-Workers", NOTHING, "-Minutes", "2", "-TestTrip", "-KillTree"]
    # The root is the job (film.py run); its children are a sleeper and the guard itself, which trips after the sleeper is up.
    code = ("import subprocess,time,os; "
            "s=subprocess.Popen(['pwsh','-NoProfile','-Command','Start-Sleep 120 # " + marker + "']); time.sleep(8); "
            f"g=subprocess.Popen({guard!r}+[str(os.getpid())]); g.wait(); open(r'{tmp_path / 'guard-exit.txt'}','w').write(str(g.returncode)); "
            f"time.sleep(120) # film.py run")
    root = subprocess.Popen([sys.executable, "-c", code])
    try:
        end = time.time() + 30
        sleepers = []
        while time.time() < end and not sleepers:
            sleepers = procs_with("FG_MARK", marker)
            time.sleep(0.5)
        assert sleepers, "sleeper did not start"
        end = time.time() + 60
        while time.time() < end and not (tmp_path / "guard-exit.txt").exists():
            time.sleep(0.5)
        assert (tmp_path / "guard-exit.txt").read_text() == "0", "guard did not finish its trip"
        assert "STOPPED: test trip" in log.read_text()
        assert wait_gone([x for x in sleepers if x != root.pid]), "tree process still running"
        assert "spared" in log.read_text() or "stopped tree process" in log.read_text()
    finally:
        kill(root.pid)
        for p in procs_with("FG_MARK", marker):
            kill(p)


def test_reused_pid_that_is_not_film_run_is_not_killed(tmp_path):
    other = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"])
    try:
        log, stop, _ = run_guard(tmp_path, "-TestTrip", "-KillTree", str(other.pid))
        assert stop.exists()
        assert other.poll() is None and alive(other.pid)
        assert "tree kill skipped" in log.read_text()
    finally:
        kill(other.pid)


def test_guard_file_has_no_lone_cr_or_tab():
    data = GUARD.read_bytes()
    assert data.startswith(b"\xef\xbb\xbf")
    assert b"\t" not in data
    crlf = data.count(b"\r\n")
    assert data.count(b"\r") == crlf and data.count(b"\n") == crlf


def test_farm_exe_with_dead_parent_is_stopped(tmp_path):
    farm = tmp_path / "bin" / "Evosim.Farm.exe"
    farm.parent.mkdir()
    shutil.copy(Path(os.environ.get("SystemRoot", r"C:\Windows")) / "System32" / "ping.exe", farm)
    other = tmp_path / "other"
    other.mkdir()
    bystander_exe = other / "Evosim.Farm.exe"
    shutil.copy(farm, bystander_exe)
    pid_file = tmp_path / "pids.txt"
    code = ("import subprocess,sys; a=subprocess.Popen([sys.argv[1],'-n','100','127.0.0.1'],stdout=subprocess.DEVNULL); "
            "b=subprocess.Popen([sys.argv[2],'-n','100','127.0.0.1'],stdout=subprocess.DEVNULL); "
            "open(sys.argv[3],'w').write(f'{a.pid} {b.pid}')")
    subprocess.run([sys.executable, "-c", code, str(farm), str(bystander_exe), str(pid_file)], timeout=30)  # the parent exits
    target, bystander = [int(x) for x in pid_file.read_text().split()]
    try:
        assert alive(target) and alive(bystander)
        pattern = re.escape(str(farm.parent)).replace("\\\\", "\\\\")
        log, stop, _ = run_guard(tmp_path, "-TestTrip", "-FarmExe", pattern)
        assert wait_gone([target]), "farm worker still running"
        assert alive(bystander), "a farm exe outside the pattern was stopped"
        assert f"stopped Evosim.Farm.exe {target}" in log.read_text()
        assert alive(os.getpid())
    finally:
        kill(target)
        kill(bystander)


def test_done_marker_ends_without_a_trip(tmp_path):
    sleeper = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"])
    try:
        (tmp_path / "done").write_text("x")
        log, stop, _ = run_guard(tmp_path, "-TestTrip", "-KillTree", str(sleeper.pid))
        assert not stop.exists()
        assert sleeper.poll() is None and alive(sleeper.pid)
        text = log.read_text()
        assert "STOPPED" not in text and "guard end" in text
    finally:
        kill(sleeper.pid)


def test_header_names_card_terms_only_when_given(tmp_path):
    (tmp_path / "done").write_text("x")
    log, _, _ = run_guard(tmp_path, "-CardTerms")
    assert "card terms" in log.read_text().splitlines()[0]
    log.unlink()
    log, _, _ = run_guard(tmp_path)
    assert "card terms" not in log.read_text().splitlines()[0]


def readings(tmp_path, series, *extra):
    """Run the guard on a list of CPU readings; returns the log and the stop file's path."""
    return run_guard(tmp_path, "-TestReadings", series, *extra)


def test_film_terms_trip_on_two_readings_in_a_row_at_90(tmp_path):
    log, stop, _ = readings(tmp_path, "80,90,90", "-FilmTerms")
    assert stop.exists() and "(film terms)" in stop.read_text() and "(film terms)" in log.read_text()
    first = log.read_text().splitlines()[0]
    assert "film terms" in first and "TEST READINGS" in first


def test_film_terms_do_not_trip_when_the_run_is_broken(tmp_path):
    log, stop, _ = readings(tmp_path, "80,90,85,90", "-FilmTerms")
    assert not stop.exists() and "guard end" in log.read_text()


def test_film_terms_trip_once_at_95(tmp_path):
    log, stop, _ = readings(tmp_path, "80,95", "-FilmTerms")
    assert stop.exists() and "CPU 95 C (film terms)" in stop.read_text()
    assert log.read_text().count("STOPPED") == 1


def test_an_unreadable_reading_does_not_reset_the_film_run(tmp_path):
    log, stop, _ = readings(tmp_path, "90,-1,90", "-FilmTerms")
    assert stop.exists() and "(film terms)" in stop.read_text()


def test_85_three_times_trips_the_card_terms_and_not_the_film_terms(tmp_path):
    log, stop, _ = readings(tmp_path, "85,85,85", "-FilmTerms")
    assert not stop.exists()
    (tmp_path / "card").mkdir()
    log, stop, _ = readings(tmp_path / "card", "85,85,85", "-CardTerms")
    assert stop.exists() and "(card terms)" in stop.read_text()


def test_both_switches_together_are_refused(tmp_path):
    log, stop, done = tmp_path / "guard.log", tmp_path / "STOP", tmp_path / "done"
    r = subprocess.run(["pwsh", "-NoProfile", "-File", str(GUARD), "-Done", str(done), "-Log", str(log), "-StopFile", str(stop),
                        "-Workers", NOTHING, "-Minutes", "2", "-TestReadings", "99,99", "-CardTerms", "-FilmTerms"],
                       capture_output=True, text=True, timeout=90)
    assert r.returncode == 2 and not stop.exists()
    assert "refused" in log.read_text() and "STOPPED" not in log.read_text()
