"""Each logical processor's mean usage, minute by minute, over a time window, from MSI Afterburner's one-second log.

Usage: python scripts/reads/core-load.py "2026-11-07 14:40:00" "2026-11-07 14:59:00"

Reads the log as temp-curve.py does, opened "rb" so Afterburner goes on writing. Afterburner numbers its CPU usage
columns from 1, so its "CPU13 usage" is logical processor 12. Prints, for each minute, the CPU's hottest reading and
the mean usage of the processors in four bands: 0-7 (the owner's), 8-11, 12-19 and 20-31, then each processor's mean
over the whole window. The question it answers is which band was loaded when the CPU ran hot.
"""
import datetime as dt
import math
import re
import sys

LOG = r"D:\Monitoring\HardwareMonitoring.hml"
HEADER = r"D:\Monitoring\archive\header.hml"
BANDS = (("0-7", range(0, 8)), ("8-11", range(8, 12)), ("12-19", range(12, 20)), ("20-31", range(20, 32)))


def columns():
    with open(HEADER, "rb") as f:
        for line in f.read().decode("cp1252").splitlines():
            if line.startswith("02,"):
                return [x.strip() for x in line.split(",")[2:]]
    raise SystemExit("no 02 line in header.hml")


def num(x):
    try:
        return float(x)
    except ValueError:
        return float("nan")


def mean(v):
    v = [x for x in v if not math.isnan(x)]
    return sum(v) / len(v) if v else float("nan")


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2
    start = dt.datetime.strptime(argv[1], "%Y-%m-%d %H:%M:%S")
    end = dt.datetime.strptime(argv[2], "%Y-%m-%d %H:%M:%S")
    names = columns()
    temp = [i for i, n in enumerate(names) if n.startswith("CPU") and n.endswith("temperature")]
    usage = {}
    for i, n in enumerate(names):
        m = re.fullmatch(r"CPU(\d+) usage", n)
        if m:
            usage[int(m.group(1)) - 1] = i
    if not usage:
        raise SystemExit("no per-processor usage columns in header.hml")
    with open(LOG, "rb") as f:
        text = f.read().decode("cp1252")
    minutes = {}
    whole = {k: [] for k in usage}
    for line in text.splitlines():
        if not line.startswith("80,"):
            continue
        v = line.split(",")
        if len(v) != len(names) + 2:
            continue
        try:
            t = dt.datetime.strptime(v[1].strip(), "%d-%m-%Y %H:%M:%S")
        except ValueError:
            continue
        if not (start <= t <= end):
            continue
        r = [x.strip() for x in v[2:]]
        key = t.strftime("%H:%M")
        mm = minutes.setdefault(key, {"t": [], "u": {k: [] for k in usage}})
        mm["t"].append(max(num(r[i]) for i in temp))
        for k, i in usage.items():
            mm["u"][k].append(num(r[i]))
            whole[k].append(num(r[i]))
    if not minutes:
        print(f"no rows between {start} and {end}; is Afterburner logging?")
        return 1
    print("minute | CPU max C | " + " | ".join(f"{b} mean %" for b, _ in BANDS))
    for key in sorted(minutes):
        mm = minutes[key]
        bands = [mean([mean(mm["u"][k]) for k in ks if k in mm["u"]]) for _, ks in BANDS]
        print(f"{key} | {max(mm['t']):.0f} | " + " | ".join(f"{b:.0f}" for b in bands))
    print("each processor's mean %: " + ", ".join(f"{k}:{mean(whole[k]):.0f}" for k in sorted(whole)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
