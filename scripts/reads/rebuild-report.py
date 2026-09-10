"""Rebuild runs/<arm>.md's sample table from the run's own stats.jsonl, for a report file
that was overwritten (r18x-s1, 2026-09-06, by a misnamed launch). Column mapping calibrated
against r18x-s2, where both files exist: python3 scripts/reads/rebuild-report.py --check r18x-s2.
The header line is copied from a sibling report with the seed and configHash swapped, and
the file says at the top that it was rebuilt. Formatting is best effort; join by t (s)."""
import json, glob, os, sys

def fmt(x, nd):
    return f"{x:.{nd}f}".rstrip('0').rstrip('.') if nd else f"{x:.0f}"
def pct(x, nd=1):
    s = f"{x*100:.{nd}f}".rstrip('0').rstrip('.')
    return s + '%'
def cell(r, prev):
    alive = max(r['alive'], 1)
    blk = r['conceptionsBlockedByMatter'] - (prev['conceptionsBlockedByMatter'] if prev else 0)
    food = r['foodJoules']; light = r['lightJoules']
    buoy = r['buoyant']
    return [
        fmt(r['t'], 0), str(r['alive']), str(r['births']), str(r['deaths']),
        f"**{r['jointed']}**", pct(r['jointed']/alive), f"**{r['jointedInherited']}**",
        fmt(r['dof']/alive, 2), fmt(r['meanSpeed'], 4), fmt(r['maxSpeed'], 4),
        fmt(r['workJoulesPerSecond'], 2), pct(r['workJoules']/max(r['spendJoules'], 1e-9)),
        f"**{pct(food/max(food+light, 1e-9), 2)}**", f"**{r['absorptive']}**", f"**{r['absorptiveInherited']}**",
        f"**{fmt(r['detritusJoules'], 1)}**", f"**{fmt(r['detritusHere'], 4)}**",
        f"**{pct(r['detritusOnFloor']/max(r['detritusJoules'], 1e-9))}**", f"**{fmt(r['detritusDeep'], 4)}**",
        fmt(r['meanHeight'], 1), f"**{fmt(r['heightSd'], 2)}**", f"**{fmt(r['meanRise'], 4)}**",
        fmt(r['meanAge'], 1), fmt(r['dayFactor'], 2), pct(r['shading']),
        f"**{buoy}**", f"**{r['buoyantInherited']}**",
        fmt(r['liftHeld']/buoy, 2) if buoy else '—', fmt(r['buoyantDepth'], 1) if buoy else '—',
        fmt(r['matterSurface'], 3), fmt(r['matterDeep'], 3),
        f"**{blk}**", f"**{r['floorSpawnsWindow']}**",
        str(r['generationMin']), str(r['generationMax']),
        f"{abs(r['auditResidual'])/max(r['spendJoules'],1e-9)*100:.4f}%", str(r['species']),
        fmt(r['edibleDetritusHere'], 4), str(r['belowWorld']), str(r['absorptiveBelowWorld']),
        fmt(r['matterLocked'], 3), fmt(r['refugeJoules'], 1), fmt(r['excretedWindow'], 6),
        fmt(r['detritusPatchSd'], 4), fmt(r['patchMaxShare'], 3),
        fmt(r['detritusDepositedWindow'], 3), fmt(r['detritusTakenWindow'], 3), fmt(r['detritusExudedWindow'], 3),
        f"**{r['absorptiveLogged']}**",
    ]

HEADER_FROM = 'runs/r18x-s2.md'
def table(arm):
    run = sorted(glob.glob(f'runs/{arm}/*'))[-1]
    rows = [json.loads(l) for l in open(os.path.join(run, 'stats.jsonl'), encoding='utf-8') if l.strip()]
    out = []; prev = None
    for r in rows:
        out.append('| ' + ' | '.join(cell(r, prev)) + ' |'); prev = r
    return run, out

if sys.argv[1] == '--check':
    arm = sys.argv[2]
    run, mine = table(arm)
    real = [l.rstrip('\n') for l in open(f'runs/{arm}.md', encoding='utf-8') if l.startswith('| ') and not l.startswith('| t ') and l[2].isdigit()]
    diff = 0; cells = 0; bad = {}
    for a, b in zip(mine, real):
        ca, cb = a.split(' | '), b.split(' | ')
        for i, (x, y) in enumerate(zip(ca, cb)):
            cells += 1
            if x != y:
                diff += 1; bad[i] = bad.get(i, 0) + 1
    print(f'{arm}: {len(mine)} rows mine, {len(real)} real; {diff} of {cells} cells differ; by column {bad}')
    sys.exit()

arm = sys.argv[1]
run, lines = table(arm)
m = json.load(open(os.path.join(run, 'run.json'), encoding='utf-8'))
cfg = json.load(open(os.path.join(run, 'config.json'), encoding='utf-8'))
src = open(HEADER_FROM, encoding='utf-8').read().split('\n')
title, header_line, cols, sep = src[0], src[2], src[4], src[5]
header_line = header_line.replace('seed 2', f"seed {m['seed']}")
import re
header_line = re.sub(r'configHash `[0-9a-f]+`', f"configHash `{cfg['configHash']}`", header_line)
out = [title, '',
       f"*Rebuilt 2026-09-06 from this run's stats.jsonl by scripts/reads/rebuild-report.py after the original report file was overwritten by a misnamed launch (the run directory was untouched). Header copied from {HEADER_FROM} with seed and configHash swapped; cell formatting is best effort and verified against {HEADER_FROM}.*",
       '', header_line, '', cols, sep] + lines + ['',
       f"**Ended:** {m['terminationReason']} reached.", '',
       f"{int(m['requestedSeconds']/m['physicsDtSeconds'])} physics steps · {m['requestedSeconds']} simulated seconds · (wall clock not recorded in the rebuilt file).", '',
       f"Genomes: `{os.path.abspath(run)}`", '']
open(f'runs/{arm}.md', 'w', encoding='utf-8').write('\n'.join(out))
print('wrote', f'runs/{arm}.md', len(lines), 'rows')
