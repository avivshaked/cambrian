# O3's follow-up in 0121: do the jointed bodies' brains read the contact or the damage channel
# more than the rigid ones'? The living at named seconds, jointed from the positions flags, the
# senses from the snapshot's genomes as r49-read.py's C1 reads them. Run from the main tree.
# usage: jointsense.py <arm> [s1,s2,...]
import glob, json, re, sys
sys.path.insert(0, 'scripts/reads')
import runrec
arm = sys.argv[1]
seconds = [int(s) for s in (sys.argv[2] if len(sys.argv) > 2 else '5000,16000,30000').split(',')]
run = sorted(glob.glob(f'runs/{arm}/2026*'))[-1]
SENSE = re.compile('"kind":"Sensor","index":-?[0-9]+,"channel":"(Contact|Damage)"')
ID = re.compile(r'^\{"id":(\d+)')
JOINTED = 2
want = {'{"t":%d,' % s: s for s in seconds}
flags = {}
for line in runrec.positions_lines(run):
    head = line[:line.index(',') + 1]
    if head in want:
        flags[want[head]] = {b[0]: b[4] for b in json.loads(line)['b']}
        if len(flags) == len(want): break
for s in seconds:
    snap = runrec.snapshot(run, s)
    if snap is None or s not in flags:
        print(f'{arm} {s}: no snapshot or no positions row'); continue
    tally = {True: [0, 0], False: [0, 0]}
    missing = 0
    for line in snap.lines:
        m = ID.match(line)
        if not m or int(m.group(1)) not in flags[s]:
            missing += 1; continue
        j = bool(flags[s][int(m.group(1))] & JOINTED)
        tally[j][0] += 1
        tally[j][1] += bool(SENSE.search(line))
    js, rs = tally[True], tally[False]
    share = lambda t: '%d of %d (%.1f%%)' % (t[1], t[0], 100.0 * t[1] / t[0]) if t[0] else 'none'
    print(f'{arm} {s} s: jointed reading contact or damage {share(js)}; rigid {share(rs)}; unjoined rows {missing}')
