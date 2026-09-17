"""Pace of every round's seeds from the run reports: simulated seconds, wall hours, x real time,
mean and peak alive, and round 38's D8 metric, wall seconds per 1,000 simulated seconds per
1,000 bodies (logbook/0101). Reads runs/<arm>.md footers and tables; an arm whose report has
no footer yet (running, or overwritten by a rerun) is skipped. The metric folds in whatever
else the machine was doing: read it beside the launch times (2026-09-17, the pace question).

    python scripts/pace-survey.py            # rounds 32 to 39
    python scripts/pace-survey.py r38 r39
"""
import re, sys, glob, statistics
from pathlib import Path

root = Path(__file__).resolve().parent.parent
rows = []
for md in sorted(glob.glob(str(root / 'runs' / 'r*.md'))):
    name = Path(md).stem
    m = re.match(r'^(r\d+[a-z]*)-s(\d)$', name)
    if not m:
        continue
    text = Path(md).read_text(encoding='utf-8', errors='replace')
    foot = re.search(r'(\d+) physics steps · ([\d.]+) simulated seconds · (\d+) births · ([\d.]+) min wall clock \(([\d.]+)x real time\)', text)
    if not foot:
        continue
    steps, sim, births, wall, x = foot.groups()
    header = text[:3000]
    hm = re.search(r'alive[^|]*', header)
    # table: alive is a named column; find header row and index
    lines = [l for l in text.splitlines() if l.startswith('|')]
    alive = []
    jointed = []
    if lines:
        hdr = [c.strip() for c in lines[0].strip('|').split('|')]
        try:
            ai = hdr.index('alive')
        except ValueError:
            ai = None
        ji = hdr.index('jnt inh') if 'jnt inh' in hdr else None
        for l in lines[2:]:
            cells = [c.strip().strip('*') for c in l.strip('|').split('|')]
            if ai is not None and len(cells) > ai:
                try: alive.append(float(cells[ai]))
                except ValueError: pass
            if ji is not None and len(cells) > ji:
                try: jointed.append(float(cells[ji]))
                except ValueError: pass
    dt = re.search(r'dt[ =]+([\d.]+)', header)
    area = re.search(r'\((\d+) m2\)', header) or re.search(r'shared (\d+)x', header)
    matter = re.search(r'matter[^,;]*?(\d{4,5})', header)
    rows.append(dict(arm=name, round=m.group(1), sim=float(sim), wall=float(wall), x=float(x),
                     steps=int(steps), alive_mean=statistics.mean(alive) if alive else 0,
                     alive_max=max(alive) if alive else 0, jnt=statistics.mean(jointed) if jointed else -1,
                     dt=dt.group(1) if dt else '?', area=area.group(1) if area else '?'))

want = sys.argv[1:] or ['r32', 'r33', 'r35', 'r36', 'r37', 'r37b', 'r38', 'r39']
print(f"{'arm':10} {'sim s':>7} {'wall h':>7} {'x real':>7} {'alive mean':>10} {'peak':>6} {'jnt inh':>8} {'dt':>5} {'area':>5} {'wall s /1000 s /1000 bodies':>28}")
for r in rows:
    if r['round'] not in want:
        continue
    per = (r['wall'] * 60) / (r['sim'] / 1000) / max(r['alive_mean'], 1) * 1000
    print(f"{r['arm']:10} {r['sim']:7.0f} {r['wall']/60:7.1f} {r['x']:7.2f} {r['alive_mean']:10.0f} {r['alive_max']:6.0f} {r['jnt']:8.0f} {r['dt']:>5} {r['area']:>5} {per:28.0f}")
