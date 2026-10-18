"""The CPU's temperature curve over a time window, from MSI Afterburner's one-second log.

Usage: python scripts/reads/temp-curve.py "2026-10-05 10:40:00" "2026-10-05 11:10:00"

Reads D:\\Monitoring\\HardwareMonitoring.hml opened "rb", which shares read, write and delete, so
Afterburner can go on writing (it stops logging for good the first time it cannot). CPU is the
hottest of Afterburner's CPU temperature columns, as scripts/temp-check.ps1 and the heat watchdog
take it. Prints the CPU maximum, the seconds at or above 85, 90 and 95 C, the GPU maximum, and
the CPU's maximum minute by minute.
"""
import datetime as dt
import sys

LOG = r"D:\Monitoring\HardwareMonitoring.hml"
HEADER = r"D:\Monitoring\archive\header.hml"


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


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2
    start = dt.datetime.strptime(argv[1], "%Y-%m-%d %H:%M:%S")
    end = dt.datetime.strptime(argv[2], "%Y-%m-%d %H:%M:%S")
    names = columns()
    cpu = [i for i, n in enumerate(names) if n.startswith("CPU") and n.endswith("temperature")]
    gpu = names.index("GPU temperature")
    with open(LOG, "rb") as f:
        text = f.read().decode("cp1252")
    rows = []
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
        if start <= t <= end:
            r = [x.strip() for x in v[2:]]
            rows.append((t, max(num(r[i]) for i in cpu), num(r[gpu])))
    if not rows:
        print(f"no rows between {start} and {end}; is Afterburner logging?")
        return 1
    c = [x[1] for x in rows]
    g = [x[2] for x in rows]
    print(f"{len(rows)} rows from {rows[0][0]} to {rows[-1][0]}")
    print(f"CPU max {max(c):.0f} C; seconds >= 85 C: {sum(x >= 85 for x in c)}, >= 90 C: {sum(x >= 90 for x in c)}, "
          f">= 95 C: {sum(x >= 95 for x in c)}; GPU max {max(g):.0f} C")
    minutes = {}
    for t, a, _ in rows:
        k = t.replace(second=0)
        minutes[k] = max(minutes.get(k, float("-inf")), a)
    print("CPU max by minute: " + ", ".join(f"{k:%H:%M} {v:.0f}" for k, v in sorted(minutes.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
