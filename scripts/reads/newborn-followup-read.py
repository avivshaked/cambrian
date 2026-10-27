"""Three follow-up reads on the slow bred-grazer newborns of logbook/specs/r62p-read/slow-newborn/README.md.

Population: bred grazers with a first speed and an unchanged body plan (structure diff count 0), as in
slow-newborn-read.py, split by their first speed into three groups:
  standstill   first speed under 0.01 m/s
  slow         0.01 to under 0.03 m/s
  moving       0.03 m/s and above
Every definition is loaded, not copied, from scripts/reads/genome-diff-read.py by the method slow-newborn-read.py uses:
this file executes that script's source up to its "groups" marker with the same RUN, FRAMES, --end and --window, so
grazer, bred, ate, the first speed (first pair of consecutive frames, at most 10 s apart, whose first frame comes within
--window s of birth; straight-line horizontal distance in x and z over the time between them), the exclusions (bred
grazers whose own or whose parent's genome is not in genomes.jsonl.gz; a living grazer born in the last 60 s before
--end) and the diff are that script's, unchanged. If the marker is missing the whole script runs with its output
discarded.

Every number is read from recorded data. Every split below is an ASSOCIATION; none separates a cause from what travels
with it. Frames are 10 s apart on the world clock, not on the creature's age.

1. joints   Stroke or stuck. For each child, the first two pose frames after birth (the pair that defines its first
            speed), from the state stream through scripts/reads/runrec.py. A pose row holds, per body: id, root
            position p (x y z, y up), root quaternion r (x y z w), and q, the joint coordinates in the solver's own
            order, one float per degree of freedom (a ball joint is three numbers of a rotation vector, not three
            angles). It holds NO part orientations, so a joint's angle is read straight from q and a part's angle to
            its parent is not computed. Per group: the share with every joint coordinate unchanged within TOL, the
            median of each child's largest change, the root's height above the bed (y minus fields/bed.f32 at the
            root's x and z, when the run wrote it), and the root's turn between the frames. Two frames 10 s apart
            cannot see a stroke inside the interval, and equal coordinates at both frames are evidence of silence,
            not proof. A change does not prove a stroke either: a joint settling to a held bend moves once.
2. brain    Which brain change. The child-against-parent "brain" diff split by the parameter kinds genome-diff-read.py's
            diff walks (diff_neurons, diff_central): frequency, phase, amplitude, bias, op, input kind, channel, input
            constant, weight, index, and a central brain's unit bias and link weight. The kinds' sums are checked
            against the diff's own brain sum. Where each neuron sits (a power neuron, a bend neuron, other, the unused
            global brain) and whether a change is live or inert by Brain.cs are reported beside. Then the share whose
            joint-driving oscillators are all near zero in amplitude in the child but not in the parent.
3. early    The bred grazers with no first speed: cause and age at death from lineage.jsonl's death row (field "c":
            starved, diverged, eaten, aged, withered), the age at death against the first frame's time, and the
            horizontal distance to a leaf (dleafh) at the first frame against the moving group's.

C# the joint reads rest on (src/Evosim.Core unless noted): Brain.cs:213-284 (a brain with no central brain takes each
part's own neuron list), Brain.cs:481-501 (the drive for a degree of freedom d of a part is neuron d of that part, the
bend is neuron dofCount+d, clamped to [-1, 1]), Brain.cs:531-544 (an oscillator's value is sin or a saw of frequency
and phase and reads no inputs; every neuron's output is value*amplitude + bias), Brain.cs:216 (a central brain, when the
genome has one, replaces the node neurons), Brain.cs:419-423 (central: drive is unit 2k, bend is unit 2k+1),
src/Evosim.Dynamics/EffectorDrive.cs:274-287 (power is the positive part of the drive above the joint's threshold,
rescaled; the bend is (w+1)/2 across the joint's range) and :313-317 (a muscle holds the joint at the bend's angle).

Usage: python -I scripts/reads/newborn-followup-read.py RUN FRAMES [--end 24000] [--window 10]
                                                       [--only joints,brain,early] [--tol 0.01] [--eps 0.05]
"""
import argparse
import array
import bisect
import contextlib
import io
import json
import math
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
GDR = os.path.join(HERE, "genome-diff-read.py")
GROUP_MARK = "groups"
STAND = 0.01
NAN = float("nan")
JDOF = {"Fixed": 0, "Hinge": 1, "Twist": 1, "HingeTwist": 2, "TwistHinge": 2, "Universal": 2, "Spherical": 3}
GROUPS = ["standstill", "slow", "moving"]

ap = argparse.ArgumentParser()
ap.add_argument("run")
ap.add_argument("frames")
ap.add_argument("--end", type=float, default=24000.0)
ap.add_argument("--window", type=float, default=10.0)
ap.add_argument("--only", default="joints,brain,early")
ap.add_argument("--tol", type=float, default=0.01, help="a joint coordinate counts as unchanged within this many radians")
ap.add_argument("--eps", type=float, default=0.05, help="an oscillator amplitude at or under this counts as near zero")
A = ap.parse_args()
ONLY = {s.strip() for s in A.only.split(",") if s.strip()}
if not ONLY <= {"joints", "brain", "early"}:
    sys.exit("--only takes joints, brain, early (comma separated); got %s" % A.only)

# ------------------------------------------------------------------ load genome-diff-read.py's definitions
src = open(GDR, encoding="utf-8").read()
cut = None
pos = 0
for line in src.splitlines(keepends=True):
    s = line.strip()
    if s.startswith("# ----") and s.endswith(GROUP_MARK):
        cut = pos
        break
    pos += len(line)
head = src if cut is None else src[:cut]
ns = {"__name__": "genome_diff_read", "__file__": GDR}
argv0 = sys.argv
sys.argv = [GDR, A.run, A.frames, "--end", repr(A.end), "--window", repr(A.window)]
try:
    if cut is None:
        with contextlib.redirect_stdout(io.StringIO()):
            exec(compile(src, GDR, "exec"), ns)
    else:
        exec(compile(head, GDR, "exec"), ns)
finally:
    sys.argv = argv0

births, deaths = ns["births"], ns["deaths"]
grazer, ate, diff_genome, speed_fn = ns["grazer"], ns["ate"], ns["diff_genome"], ns["speed"]
genomes, bred, SLOW = ns["genomes"], ns["bred"], ns["SLOW"]
END = A.end
bred0 = [i for i, b in births.items() if grazer(b) and b.get("p", -1) >= 0]


def parent_of(i):
    b = births.get(i)
    return b.get("p", -1) if b else -1


# ------------------------------------------------------------------ the frame table, for the bred grazers
def fnum(s):
    try:
        return float(s)
    except ValueError:
        return NAN


T, X, Y, Z, BF, J, DH, D3 = range(8)
MY = {}
want = set(bred)
with open(A.frames, encoding="utf-8") as f:
    hd = f.readline().rstrip("\n").split("\t")
    col = {n: k for k, n in enumerate(hd)}
    need_cols = ("id", "t", "x", "y", "z")
    if any(n not in col for n in need_cols):
        sys.exit("frame table lacks one of %s; has %s" % (need_cols, hd))
    ci, ct, cx, cy, cz = (col[n] for n in need_cols)
    cbf, cj, cdh, cd3 = (col.get(n) for n in ("bf", "J", "dleafh", "dleaf3"))
    for line in f:
        p = line.rstrip("\n").split("\t")
        if len(p) < len(hd):
            continue
        try:
            i = int(p[ci])
        except ValueError:
            continue
        if i not in want:
            continue
        t = fnum(p[ct])
        if math.isnan(t):
            continue
        MY.setdefault(i, []).append((t, fnum(p[cx]), fnum(p[cy]), fnum(p[cz]),
                                     fnum(p[cbf]) if cbf is not None else NAN, fnum(p[cj]) if cj is not None else NAN,
                                     fnum(p[cdh]) if cdh is not None else NAN, fnum(p[cd3]) if cd3 is not None else NAN))
for v in MY.values():
    v.sort()

_spd = {}


def spd(i):
    if i not in _spd:
        _spd[i] = speed_fn(i) if i in births else None
    return _spd[i]


def first_pair(i):
    """The consecutive frame pair that defines the first speed (the same rule as genome-diff-read.py's speed()), or None."""
    t0 = births[i]["t"]
    fl = MY.get(i, ())
    for a, b in zip(fl, fl[1:]):
        if a[T] < t0 - 1e-6:
            continue
        if a[T] - t0 >= A.window:
            return None
        dt = b[T] - a[T]
        if 0 < dt <= 10.01:
            return a, b
        return None
    return None


def first_frame(i):
    t0 = births[i]["t"]
    for fr in MY.get(i, ()):
        if fr[T] >= t0 - 1e-6:
            return fr
    return None


def clean(xs):
    return [x for x in xs if x is not None and not (isinstance(x, float) and math.isnan(x))]


def quart(xs):
    xs = sorted(clean(xs))
    if not xs:
        return NAN, NAN, NAN

    def q(p):
        at = p * (len(xs) - 1)
        lo = int(at)
        hi = min(lo + 1, len(xs) - 1)
        return xs[lo] + (xs[hi] - xs[lo]) * (at - lo)
    return q(0.25), q(0.5), q(0.75)


def med(xs):
    xs = clean(xs)
    return statistics.median(xs) if xs else NAN


def fm(x, spec="%.4g"):
    return "-" if x is None or (isinstance(x, float) and math.isnan(x)) else spec % x


def share(a, b):
    return a / b if b else NAN


# ------------------------------------------------------------------ the population
young = {i for i in bred if i not in deaths and births[i]["t"] > END - 60}
withspeed, nospeed = [], []
for i in bred:
    if i in young:
        continue
    (withspeed if spd(i) is not None else nospeed).append(i)
diffs = {i: diff_genome(genomes[i], genomes[parent_of(i)]) for i in withspeed}
pop = [i for i in withspeed if diffs[i].n["structure"] == 0]


def group_of(v):
    return "standstill" if v < STAND else ("slow" if v < SLOW else "moving")


GM = {g: [] for g in GROUPS}
for i in pop:
    GM[group_of(spd(i))].append(i)

print("newborn-followup-read.py  run %s  frames %s  reads %s" % (A.run, A.frames, ",".join(sorted(ONLY))))
print("to %.0f s, window %g s; everything below is ASSOCIATION in recorded data, not cause" % (END, A.window))
print("DEFINITIONS")
print("  grazer, bred, ate, first speed, the exclusions and the diff: genome-diff-read.py's, loaded from its source "
      "(see this file's docstring)")
print("  standstill: first speed < %g m/s;  slow: %g to < %g;  moving: >= %g;  all among bred grazers with a first speed "
      "and structure diff count 0" % (STAND, STAND, SLOW, SLOW))
print("  joint coordinate: an entry of a pose row's q (radians for a hinge or twist; three components of a rotation vector "
      "for a spherical joint)")
print("  unchanged: every joint coordinate differs by at most TOL = %g rad between the child's first two pose frames. TOL is "
      "far above float32 resolution (about 1e-7) and the solver's settling jitter, and far below a stroke "
      "(a joint range is tens of degrees, 0.01 rad is 0.57 degrees); the share is also printed at 0.001, 0.05 and 0.1" % A.tol)
print("  near zero (amplitude): |amplitude| <= EPS = %g; amplitude is a neuron's multiplier on a value in [-1, 1] "
      "(Brain.cs:544)" % A.eps)
print("bred grazers (p >= 0) %d; with both genomes %d; young and alive, left out %d; with a first speed %d; without %d" % (
    len(bred0), len(bred), len(young), len(withspeed), len(nospeed)))
print("POPULATION n %d: %s" % (len(pop), "; ".join("%s %d" % (g, len(GM[g])) for g in GROUPS)))
print("  slow in the earlier reads' sense (< %g): %d (README: 997 and 193)" % (SLOW, len(GM["standstill"]) + len(GM["slow"])))
bad = 0
for i in pop:
    pr = first_pair(i)
    if pr is None or abs(math.hypot(pr[1][X] - pr[0][X], pr[1][Z] - pr[0][Z]) / (pr[1][T] - pr[0][T]) - spd(i)) > 1e-9:
        bad += 1
print("  check: children whose first pair here gives a different speed from genome-diff-read.py's: %d (should be 0)" % bad)
if not pop:
    print("no population; nothing to split. (Is the frame table empty?)")
    sys.exit(0)

# ================================================================== 1. joints
if "joints" in ONLY:
    print("\n" + "=" * 100)
    print("1. JOINTS: stroke or stuck  (association; two frames 10 s apart)")
    print("=" * 100)
    bed = None
    try:
        lay = json.load(open(os.path.join(A.run, "fields", "layout.json"), encoding="utf-8"))["bed"]
        arr = array.array("f")
        with open(os.path.join(A.run, "fields", "bed.f32"), "rb") as f:
            arr.frombytes(f.read())
        if sys.byteorder == "big":
            arr.byteswap()
        if len(arr) == lay["cellsX"] * lay["cellsZ"]:
            bed = (arr, lay["cellsX"], lay["cellsZ"], float(lay["cellMetres"]))
    except Exception as e:      # noqa: BLE001 - a missing bed is reported, not fatal
        print("  bed: not read (%s)" % e)
    print("  bed floor y: %s" % ("fields/bed.f32, %dx%d cells of %g m, index (int(x/cell))*nz + int(z/cell), as scripts/guide.py "
                                 "reads it" % (bed[1], bed[2], bed[3]) if bed else "unavailable; the root's raw y is given instead"))

    # which frames are wanted
    need = {}
    times = set()
    for i in pop:
        a, b = first_pair(i)
        need.setdefault(a[T], {}).setdefault(i, []).append(0)
        need.setdefault(b[T], {}).setdefault(i, []).append(1)
        times.add(a[T])
        times.add(b[T])
    wanted = sorted(times)

    def nearest(t):
        k = bisect.bisect_left(wanted, t)
        best = None
        for j in (k - 1, k):
            if 0 <= j < len(wanted) and abs(wanted[j] - t) <= 0.01 and (best is None or abs(wanted[j] - t) < abs(best - t)):
                best = wanted[j]
        return best

    POSE = {}
    frames_read = 0
    pose_note = None
    try:
        sys.path.insert(0, HERE)
        import runrec
        run = runrec.run_directory(A.run)
        src_name = runrec.poses_source(run)
        if src_name is None:
            raise RuntimeError("the run holds neither poses.jsonl nor poses.bin")
        rows = runrec.poses(run, keep=lambda k, t: nearest(t) is not None)
        for row in rows:
            wt = nearest(row["t"])
            if wt is None:
                continue
            frames_read += 1
            ids = need[wt]
            for b in row["bodies"]:
                w = ids.get(b["id"])
                if w is not None:
                    for which in w:
                        POSE[(b["id"], which)] = (list(b["q"]) if b.get("q") is not None else [], tuple(b["p"]), tuple(b["r"]))
        pose_note = "read %d of the %d wanted frame times from %s; %d body-frames kept" % (
            frames_read, len(wanted), src_name, len(POSE))
    except Exception as e:      # noqa: BLE001
        pose_note = "pose stream NOT read (%s: %s); only the frame table's x y z are used below" % (type(e).__name__, e)
    print("  " + pose_note)

    recs = {g: [] for g in GROUPS}
    miss = {g: 0 for g in GROUPS}
    shape_bad = 0
    for g in GROUPS:
        for i in GM[g]:
            a, b = first_pair(i)
            r = {"age0": a[T] - births[i]["t"], "ytab": a[Y], "dy": b[Y] - a[Y], "maxdq": None, "ndof": None, "h": None,
                 "turn": None, "maxq": None}
            p0, p1 = POSE.get((i, 0)), POSE.get((i, 1))
            if p0 is None or p1 is None:
                miss[g] += 1
                recs[g].append(r)
                continue
            q0, q1 = p0[0], p1[0]
            if len(q0) != len(q1):
                shape_bad += 1
                miss[g] += 1
                recs[g].append(r)
                continue
            r["ndof"] = len(q0)
            if q0:
                r["maxdq"] = max(abs(u - v) for u, v in zip(q0, q1))
                r["maxq"] = max(abs(v) for v in q1)
            y = p0[1][1]
            if bed is not None:
                ix = min(max(int(p0[1][0] / bed[3]), 0), bed[1] - 1)
                iz = min(max(int(p0[1][2] / bed[3]), 0), bed[2] - 1)
                r["h"] = y - bed[0][ix * bed[2] + iz]
            else:
                r["h"] = None
            r["yraw"] = y
            dot = abs(sum(u * v for u, v in zip(p0[2], p1[2])))
            r["turn"] = 2.0 * math.acos(min(1.0, dot))
            recs[g].append(r)
    if shape_bad:
        print("  children whose two frames hold different numbers of joint coordinates, left out: %d" % shape_bad)

    print("\n  Per group. 'rigid' bodies have no joint coordinate (q empty) and are left out of the joint columns.")
    hdr = "  %-11s %5s %6s %6s %7s %9s %9s %9s %9s %9s" % ("group", "n", "pose", "rigid", "jointed", "unch@0.001", "unch@TOL",
                                                          "unch@0.05", "unch@0.1", "")
    print(hdr)
    for g in GROUPS:
        rs = recs[g]
        got = [r for r in rs if r["ndof"] is not None]
        rigid = [r for r in got if r["ndof"] == 0]
        jt = [r for r in got if r["ndof"]]

        def un(tol):
            return share(sum(1 for r in jt if r["maxdq"] <= tol), len(jt))
        print("  %-11s %5d %6d %6d %7d %9s %9s %9s %9s" % (g, len(rs), len(got), len(rigid), len(jt),
                                                         fm(un(0.001), "%.3f"), fm(un(A.tol), "%.3f"), fm(un(0.05), "%.3f"),
                                                         fm(un(0.1), "%.3f")))
    print("\n  Joint coordinate change between the two frames, per jointed child (radians): largest over its coordinates")
    print("  %-11s %7s %9s %9s %9s %12s" % ("group", "jointed", "Q1", "median", "Q3", "med max|q|"))
    for g in GROUPS:
        jt = [r for r in recs[g] if r["ndof"]]
        q1, m, q3 = quart([r["maxdq"] for r in jt])
        print("  %-11s %7d %9s %9s %9s %12s" % (g, len(jt), fm(q1), fm(m), fm(q3), fm(med([r["maxq"] for r in jt]))))
    print("  (max|q| is the largest joint coordinate magnitude at the second frame; a value near a joint's limit would "
          "hint at a joint pinned against it, but the limits are not read here)")

    print("\n  The root, from the pose rows")
    print("  %-11s %5s %12s %12s %14s %14s %10s" % ("group", "n", "med age f0", "med root y", "med height/bed", "Q1..Q3 h/bed", "med turn"))
    for g in GROUPS:
        rs = [r for r in recs[g] if r["ndof"] is not None]
        hs = [r["h"] for r in rs]
        q1, m, q3 = quart(hs)
        print("  %-11s %5d %12s %12s %14s %14s %10s" % (g, len(rs), fm(med([r["age0"] for r in recs[g]]), "%.1f s"),
                                                       fm(med([r.get("yraw") for r in rs]), "%.2f m"), fm(m, "%.2f m"),
                                                       "%s..%s" % (fm(q1, "%.2f"), fm(q3, "%.2f")),
                                                       fm(med([r["turn"] for r in rs]), "%.3f rad")))
    print("  height/bed is y at the first frame minus the bed's floor y at the root's x and z (y is up); %s. turn is the "
          "root quaternion's angle between the two frames." % (
              "the bed is from fields/bed.f32" if bed else "NO BED, so 'med height/bed' is blank and root y is raw"))

    print("\n  Root height from the frame table (needs no pose stream; raw y, y up) and its change between the same two frames")
    print("  %-11s %5s %12s %14s %14s" % ("group", "n", "med y f0", "med |dy| (m)", "Q3 |dy| (m)"))
    for g in GROUPS:
        ys = [r["ytab"] for r in recs[g]]
        dys = [abs(r["dy"]) for r in recs[g]]
        print("  %-11s %5d %12s %14s %14s" % (g, len(recs[g]), fm(med(ys), "%.2f"), fm(med(dys), "%.3f"), fm(quart(dys)[2], "%.3f")))

    print("\n  Joint change against whether the body moved (association): jointed children only")
    print("  %-11s %9s %9s %9s %9s" % ("group", "n jointed", "unch", "changed", "share unch"))
    for g in GROUPS:
        jt = [r for r in recs[g] if r["ndof"]]
        u = sum(1 for r in jt if r["maxdq"] <= A.tol)
        print("  %-11s %9d %9d %9d %9s" % (g, len(jt), u, len(jt) - u, fm(share(u, len(jt)), "%.3f")))
    print("""
  READING (inference, to be checked by eye): a standstill child whose joints are unchanged is silent at both frames; one
  whose joints changed but whose root did not move may be stroking against something, or only settling to a held bend.
  Two frames 10 s apart cannot see a stroke inside the interval: equal coordinates at both frames are evidence of
  silence, not proof, because a full stroke cycle shorter than 10 s can return to the same angle (a joint driven by an
  oscillator of period dividing 10 s would read as unchanged). A change is not proof of a stroke either. Pose rows hold
  no part orientations, so this is the joint coordinate q, not a computed part-to-parent rotation.""")

# ================================================================== 2. brain
if "brain" in ONLY:
    print("\n" + "=" * 100)
    print("2. BRAIN: which brain change  (association)")
    print("=" * 100)
    KINDS = ["frequency", "phase", "amplitude", "bias", "op", "input_kind", "channel", "input_const", "weight", "index",
             "central_unit_bias", "central_link_weight"]
    LOCS = ["power", "bend", "other", "global", "central"]

    def is_osc(op):
        return isinstance(op, str) and op.startswith("Oscillate")

    def rel(x, y):
        """genome-diff-read.py's Diff.num / Diff.name: a number's relative change, a name's 1.0."""
        if isinstance(x, bool) or isinstance(y, bool) or not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
            return 0.0 if x == y else 1.0
        return abs(x - y) / max(abs(x), abs(y), 1e-9) if x != y else 0.0

    class BD:
        def __init__(self):
            self.k = {n: [0, 0.0] for n in KINDS}
            self.loc = {n: [0, 0.0] for n in LOCS}
            self.live = [0, 0.0]
            self.total = 0.0

        def add(self, kind, r, loc, live):
            if r <= 0:
                return
            self.k[kind][0] += 1
            self.k[kind][1] += r
            self.loc[loc][0] += 1
            self.loc[loc][1] += r
            self.total += r
            if live:
                self.live[0] += 1
                self.live[1] += r

    def walk_neurons(bd, ch, pa, dof, inert_node, is_global):
        if not isinstance(ch, list) or not isinstance(pa, list):
            return
        for k in range(min(len(ch), len(pa))):
            c, p = ch[k], pa[k]
            loc = "global" if is_global else ("power" if k < dof else ("bend" if k < 2 * dof else "other"))
            osc = is_osc(c.get("op"))
            live_any = not inert_node
            for key in ("frequency", "phase"):
                # an oscillator reads them (Brain.cs:531-537); any other op ignores them
                bd.add(key, rel(c.get(key), p.get(key)), loc, live_any and osc)
            for key in ("amplitude", "bias"):
                bd.add(key, rel(c.get(key), p.get(key)), loc, live_any)      # Brain.cs:544, every op
            bd.add("op", rel(c.get("op"), p.get("op")), loc, live_any)
            ci, pi = c.get("inputs", []), p.get("inputs", [])
            for m in range(min(len(ci), len(pi))):
                # an oscillator reads no inputs (Brain.cs:529-537)
                lv = live_any and not osc
                for key, kind in (("index", "index"), ("constant", "input_const"), ("weight", "weight")):
                    bd.add(kind, rel(ci[m].get(key), pi[m].get(key)), loc, lv)
                bd.add("input_kind", rel(ci[m].get("kind"), pi[m].get("kind")), loc, lv)
                bd.add("channel", rel(ci[m].get("channel"), pi[m].get("channel")), loc, lv)

    def walk_central(bd, ch, pa):
        if not ch or not pa:
            return
        for part in ("outputs", "hidden"):
            cm = {u["id"]: u for u in ch.get(part, [])}
            pm = {u["id"]: u for u in pa.get(part, [])}
            for k in cm.keys() & pm.keys():
                bd.add("central_unit_bias", rel(cm[k].get("bias"), pm[k].get("bias")), "central", True)
        cm = {(l["from"], l["to"]): l for l in ch.get("links", [])}
        pm = {(l["from"], l["to"]): l for l in pa.get("links", [])}
        for k in cm.keys() & pm.keys():
            bd.add("central_link_weight", rel(cm[k].get("w"), pm[k].get("w")), "central", True)

    def brain_diff(c, p):
        bd = BD()
        central = bool(c.get("brain"))      # Brain.cs:216: a genome with a central brain is driven by it alone
        cn, pn = c.get("nodes", []), p.get("nodes", [])
        for k in range(min(len(cn), len(pn))):
            dof = JDOF.get(cn[k].get("joint"), 0)
            walk_neurons(bd, cn[k].get("neurons", []), pn[k].get("neurons", []), dof, central, False)
        walk_neurons(bd, c.get("globalBrain", []), p.get("globalBrain", []), 0, True, True)   # unused since D081, Brain.cs:207-211
        walk_central(bd, c.get("brain"), p.get("brain"))
        return bd

    BDS = {i: brain_diff(genomes[i], genomes[parent_of(i)]) for i in pop}
    mism = [i for i in pop if abs(BDS[i].total - diffs[i].s["brain"]) > 1e-6 * max(1.0, diffs[i].s["brain"])]
    print("  check: children whose kinds' sum differs from the diff's own brain sum: %d of %d (should be 0)" % (len(mism), len(pop)))
    for i in mism[:3]:
        print("    id %d  kinds %.9g  diff %.9g" % (i, BDS[i].total, diffs[i].s["brain"]))
    nc = sum(1 for i in pop if genomes[i].get("brain"))
    print("  children whose genome has a central brain (the node neurons are then unused): %d of %d" % (nc, len(pop)))
    print("  (a child's neuron location: power = neuron d < dof of the node's joint, which drives the joint; bend = the next "
          "dof neurons; other = the rest; global = the unused global brain; central = a central brain's units and links)")

    print("\n  2a. Share of children with any change of each kind / median summed relative change over all members / median over "
          "those that changed")
    print("      (a changed name counts 1.0, as in genome-diff-read.py)")
    print("  %-20s" % "kind" + "".join(" | %-26s" % ("%s (n %d)" % (g, len(GM[g]))) for g in GROUPS))
    for kind in KINDS:
        line = "  %-20s" % kind
        for g in GROUPS:
            ms = GM[g]
            ch = [BDS[i].k[kind] for i in ms]
            n = sum(1 for c in ch if c[0])
            line += " | %5s  %8s  %8s " % (fm(share(n, len(ms)), "%.3f"), fm(med([c[1] for c in ch]), "%.3g"),
                                          fm(med([c[1] for c in ch if c[0]]), "%.3g"))
        print(line)
    line = "  %-20s" % "ANY kind"
    for g in GROUPS:
        ms = GM[g]
        n = sum(1 for i in ms if BDS[i].total > 0)
        line += " | %5s  %8s  %8s " % (fm(share(n, len(ms)), "%.3f"), fm(med([BDS[i].total for i in ms]), "%.3g"),
                                      fm(med([BDS[i].total for i in ms if BDS[i].total > 0]), "%.3g"))
    print(line)

    print("\n  2b. Where the change sits, and whether it can act (Brain.cs): share with any change at each location, and with "
          "any LIVE change")
    print("      live = not on a global-brain neuron, not on node neurons when a central brain exists, and not a frequency, "
          "phase (non-oscillator child op) or input change on an oscillator neuron")
    print("  %-26s" % "location" + "".join(" | %-12s" % g for g in GROUPS))
    for loc in LOCS:
        print("  %-26s" % loc + "".join(" | %12s" % fm(share(sum(1 for i in GM[g] if BDS[i].loc[loc][0]), len(GM[g])), "%.3f")
                                        for g in GROUPS))
    print("  %-26s" % "ANY LIVE change" + "".join(
        " | %12s" % fm(share(sum(1 for i in GM[g] if BDS[i].live[0]), len(GM[g])), "%.3f") for g in GROUPS))
    print("  %-26s" % "no live change at all" + "".join(
        " | %12s" % fm(share(sum(1 for i in GM[g] if not BDS[i].live[0]), len(GM[g])), "%.3f") for g in GROUPS))
    print("  %-26s" % "median live summed change" + "".join(
        " | %12s" % fm(med([BDS[i].live[1] for i in GM[g]]), "%.3g") for g in GROUPS))

    # ---- the joint-driving neurons
    def power_neurons(g):
        out = []
        for nd in g.get("nodes", []):
            dof = JDOF.get(nd.get("joint"), 0)
            thr = (nd.get("drive") or {}).get("threshold", 0.0)
            for n in (nd.get("neurons") or [])[:dof]:
                out.append((n, thr if isinstance(thr, (int, float)) else 0.0))
        return out

    def drive_status(g):
        if g.get("brain"):
            return None
        pn = power_neurons(g)
        osc_amp = [abs(n.get("amplitude", 0.0)) for n, _ in pn if is_osc(n.get("op"))]
        n_non = sum(1 for n, _ in pn if not is_osc(n.get("op")))

        def ub(n, thr):     # the largest value this neuron's output can reach, against the joint's threshold (EffectorDrive.cs:284-285)
            return (abs(n.get("amplitude", 0.0)) + n.get("bias", 0.0)) if is_osc(n.get("op")) else None
        dead = bool(pn) and all(ub(n, t) is not None and ub(n, t) <= t for n, t in pn)
        return {"n_power": len(pn), "n_osc": len(osc_amp), "n_non": n_non, "maxamp": max(osc_amp) if osc_amp else None,
                "osc_zero": bool(osc_amp) and max(osc_amp) <= A.eps, "dead": dead}

    print("\n  2c. Joint-driving (power) neurons: the first dof neurons of each node whose joint has dof > 0 (Brain.cs:481-493)")
    print("      osc_zero: the genome has power neurons that are oscillators, and every one has |amplitude| <= EPS = %g" % A.eps)
    print("      dead: every power neuron is an oscillator whose |amplitude| + bias can never exceed its joint's threshold, so "
          "its power is never above 0 (EffectorDrive.cs:282-285); a non-oscillator power neuron is unknown and keeps a genome "
          "from being 'dead'. A static bound only.")
    DS = {}
    for i in pop:
        DS[i] = (drive_status(genomes[i]), drive_status(genomes[parent_of(i)]))
    print("  %-34s" % "" + "".join(" | %-12s" % g for g in GROUPS))

    def row(label, fn, denom=lambda i: True):
        line = "  %-34s" % label
        for g in GROUPS:
            ids = [i for i in GM[g] if DS[i][0] is not None and DS[i][1] is not None and denom(i)]
            line += " | %12s" % ("%s (%d)" % (fm(share(sum(1 for i in ids if fn(i)), len(ids)), "%.3f"), len(ids)))
        print(line)

    row("child: has any power neuron", lambda i: DS[i][0]["n_power"] > 0)
    row("child: has an oscillator power neuron", lambda i: DS[i][0]["n_osc"] > 0)
    row("child: osc_zero", lambda i: DS[i][0]["osc_zero"])
    row("parent: osc_zero", lambda i: DS[i][1]["osc_zero"])
    row("child osc_zero, parent not", lambda i: DS[i][0]["osc_zero"] and not DS[i][1]["osc_zero"])
    row("child: dead", lambda i: DS[i][0]["dead"])
    row("parent: dead", lambda i: DS[i][1]["dead"])
    row("child dead, parent not", lambda i: DS[i][0]["dead"] and not DS[i][1]["dead"])
    row("child: no power neuron, parent has", lambda i: DS[i][0]["n_power"] == 0 and DS[i][1]["n_power"] > 0)
    row("child: all power neurons non-osc", lambda i: DS[i][0]["n_power"] > 0 and DS[i][0]["n_osc"] == 0)
    both = lambda i: DS[i][0]["maxamp"] is not None and DS[i][1]["maxamp"] is not None
    print("  largest oscillator power amplitude (both have an oscillator power neuron):")
    line1, line2, line3 = ("  %-34s" % "  child, median", "  %-34s" % "  parent, median", "  %-34s" % "  share child < half of parent")
    for g in GROUPS:
        ids = [i for i in GM[g] if DS[i][0] is not None and DS[i][1] is not None and both(i)]
        line1 += " | %12s" % fm(med([DS[i][0]["maxamp"] for i in ids]), "%.3f")
        line2 += " | %12s" % fm(med([DS[i][1]["maxamp"] for i in ids]), "%.3f")
        line3 += " | %12s" % fm(share(sum(1 for i in ids if DS[i][0]["maxamp"] < 0.5 * DS[i][1]["maxamp"]), len(ids)), "%.3f")
    print(line1)
    print(line2)
    print(line3)
    print("""
  READING (inference): 2a says which parameter kinds co-occur with standstill, 2b whether the change could act at all,
  2c whether the child's drive neurons are statically silent where the parent's were not. An oscillator at amplitude
  near zero holds its joint at its bias, so its joint does not stroke; but a non-oscillator drive neuron (a Sum of
  joint-angle inputs, as the founder genomes have) can be silent at rest and start only from a disturbance, and this
  read cannot tell that from outside. The bend neurons (second dof neurons) shape the held angle, not the power.""")

# ================================================================== 3. early
if "early" in ONLY:
    print("\n" + "=" * 100)
    print("3. EARLY: bred grazers with no first speed  (association)")
    print("=" * 100)
    print("  n %d (README: 554). Cause is the death row's field c (LineageEvent.cs:860: starved, diverged, eaten, aged, "
          "withered); a body with no death row by --end is 'alive at end'." % len(nospeed))
    print("  age at death = death row t minus birth row t, seconds; the first frame is the first table frame at or after birth.")

    def cause_of(i):
        d = deaths.get(i)
        return "alive at end" if d is None else str(d.get("c", "?"))

    def age_of(i):
        d = deaths.get(i)
        return None if d is None else d["t"] - births[i]["t"]

    def cause_table(label, ids):
        cs = {}
        for i in ids:
            cs.setdefault(cause_of(i), []).append(i)
        print("\n  %s (n %d)" % (label, len(ids)))
        print("    %-14s %6s %7s %9s %9s %9s %10s" % ("cause", "n", "share", "age Q1", "age med", "age Q3", "ate share"))
        for c, m in sorted(cs.items(), key=lambda kv: -len(kv[1])):
            q1, md, q3 = quart([age_of(i) for i in m])
            print("    %-14s %6d %7s %9s %9s %9s %10s" % (c, len(m), fm(share(len(m), len(ids)), "%.3f"), fm(q1, "%.1f"),
                                                          fm(md, "%.1f"), fm(q3, "%.1f"), fm(share(sum(1 for i in m if ate(i)), len(m)), "%.3f")))
        q1, md, q3 = quart([age_of(i) for i in ids])
        print("    %-14s %6d %7s %9s %9s %9s %10s" % ("all", len(ids), "", fm(q1, "%.1f"), fm(md, "%.1f"), fm(q3, "%.1f"),
                                                      fm(share(sum(1 for i in ids if ate(i)), len(ids)), "%.3f")))

    cause_table("no first speed", nospeed)
    for g in GROUPS:
        cause_table("for comparison: %s" % g, GM[g])

    print("\n  Age at death against the first frame's time (the frame table has frames 10 s apart on the world clock)")
    print("  one frame and no pair means the body died within 10 s of its first frame")
    cls = {}
    for i in nospeed:
        t0 = births[i]["t"]
        fl = [fr for fr in MY.get(i, ()) if fr[T] >= t0 - 1e-6]
        d = deaths.get(i)
        if not fl:
            k = "no frame at all"
        elif len(fl) == 1:
            k = "one frame, no pair"
        elif fl[0][T] - t0 >= A.window:
            k = "first frame at age >= window"
        elif fl[1][T] - fl[0][T] > 10.01 or fl[1][T] - fl[0][T] <= 0:
            k = "first two frames more than 10 s apart"
        else:
            k = "a usable pair exists (unexpected)"
        cls.setdefault(k, []).append(i)
    print("  %-40s %6s %9s %9s %9s %14s" % ("class", "n", "age Q1", "age med", "age Q3", "med death-f0"))
    for k, m in sorted(cls.items(), key=lambda kv: -len(kv[1])):
        q1, md, q3 = quart([age_of(i) for i in m])
        gaps = []
        for i in m:
            fl = [fr for fr in MY.get(i, ()) if fr[T] >= births[i]["t"] - 1e-6]
            if fl and i in deaths:
                gaps.append(deaths[i]["t"] - fl[0][T])
        print("  %-40s %6d %9s %9s %9s %14s" % (k, len(m), fm(q1, "%.1f"), fm(md, "%.1f"), fm(q3, "%.1f"), fm(med(gaps), "%.1f")))
    ff_age = [first_frame(i)[T] - births[i]["t"] for i in nospeed if first_frame(i)]
    print("  age at the first frame, where there is one: n %d, Q1/median/Q3 %s" % (
        len(ff_age), " / ".join(fm(x, "%.1f") for x in quart(ff_age))))
    died_before = sum(1 for i in nospeed if i in deaths and not any(fr[T] >= births[i]["t"] - 1e-6 for fr in MY.get(i, ())))
    print("  of the no-speed, dead with no frame at all (died before the first 10 s tick after birth): %d" % died_before)

    print("\n  Where it was at its first frame: horizontal distance to the nearest living leaf, dleafh (m), and the 3D "
          "distance dleaf3")
    print("  %-22s %6s %8s %8s %8s %9s %9s %10s" % ("set", "n", "Q1", "median", "Q3", "<0.3 m", ">1.4 m", "med d3"))

    def dl_row(label, ids):
        ffs = [first_frame(i) for i in ids]
        dh = [f[DH] for f in ffs if f is not None]
        d3 = [f[D3] for f in ffs if f is not None]
        dc = clean(dh)
        q1, md, q3 = quart(dh)
        print("  %-22s %6d %8s %8s %8s %9s %9s %10s" % (label, len(dc), fm(q1, "%.2f"), fm(md, "%.2f"), fm(q3, "%.2f"),
                                                       fm(share(sum(1 for x in dc if x < 0.3), len(dc)), "%.3f"),
                                                       fm(share(sum(1 for x in dc if x > 1.4), len(dc)), "%.3f"), fm(med(d3), "%.2f")))
    dl_row("no first speed", nospeed)
    by = {}
    for i in nospeed:
        by.setdefault(cause_of(i), []).append(i)
    for c, m in sorted(by.items(), key=lambda kv: -len(kv[1])):
        dl_row("  no speed, " + c, m)
    for g in reversed(GROUPS):
        dl_row(g, GM[g])
    ffn = [first_frame(i) for i in nospeed]
    ffm = [first_frame(i) for i in GM["moving"]]
    print("  first-frame body fraction bf, median: no speed %s, moving %s;  reserve J median: no speed %s, moving %s" % (
        fm(med([f[BF] for f in ffn if f]), "%.3f"), fm(med([f[BF] for f in ffm if f]), "%.3f"),
        fm(med([f[J] for f in ffn if f]), "%.1f"), fm(med([f[J] for f in ffm if f]), "%.1f")))
    print("""
  READING (inference): the classes say how many of the no-speed bodies died before they could be measured at all and how
  many had one frame. dleafh against the moving group asks whether they were born further from food; it is a split, not
  a cause. The comparison sets differ in survival by construction (a body must live to a second frame to have a speed), so
  any difference in the first-frame place can come from who survives long enough to be counted.""")

print("\nAll of the above is association in recorded data. Nothing here separates a cause from what travels with it.")
