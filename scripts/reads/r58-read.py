"""The reader for round 58's clauses V, S, R, W and Y (the draft, logbook/specs/r58-prereg-draft.md): three
worlds joined in a ring that passes genomes along it (D159), against three kept apart.

Joined and isolated worlds are paired in the order given: joined[i]'s twin is isolated[i]. With --unpaired,
"than its twin" becomes "than every isolated world". A world is a run directory (the folder holding
run.json) or an arm folder, of which the latest dated run directory is read.

The year is --year seconds (6,000 s by default; D151): summer from -1/24 to 11/24 of each year and winter
from 11/24 to 23/24, which at 6,000 s are round 57's windows (-250, 2,750) and (2,750, 5,750). Year 1 is 0
to one year. "After the first year" means a time strictly later than one year. Samples are taken from
lineage.jsonl at every --every seconds (a sample at T counts every row with t <= T), up to --end or the
run's last stats row, whichever is earlier.

Lines and guilds, from lineage.jsonl, by immigrants.py's conventions. A birth with no parent roots a line
(a root lineage) of its own: the floor's, the trickle's and the pool's founders (kind "f", src floor,
trickle or pool) and every planted arrival (kind "m", with "from", the sending world's place in the group,
and "fid", its id there). Every later birth belongs to its parent's root. An arrival-descended body is one
whose root is an arrival. Nothing is left out of the lines: unlike round 57's reader, which leaves the
founders out of its margin statistics, a founder here roots a line like any other. A body's guild is read
from its birth row's flags, as round 57's reader reads them: jointed when jnt is 1, absorptive when abs is
1, a leaf when pho is 1 and jnt and abs are both 0. Jointed and absorptive can overlap; a leaf is neither.

V:  at the end, the root lineages holding at least V_MIN_SHARE of the living, counted per world. An
    arrival's line is one root lineage, and so is each founder's. Holds if each joined world's count
    exceeds its twin's (or every isolated world's), in 3 of 3.
S:  per joined world, guild and sample after the first year: the living of the guild, and the living of
    the guild descended from an arrival. A world lacks a guild at a sample when none of its living has it.
    Readable only if at some sample after the first year a joined world lacks a guild that its sender
    (the world whose place is its run.json migrateFrom) holds at the same sample; otherwise unread, with
    the reason. Holds if, in at least one joined world, for some guild it lacks at a sample after the
    first year, a later unbroken run of samples, each with at least S_MIN_ARRIVAL_BODIES living
    arrival-descended bodies of that guild, spans at least HOLD_SPAN_YEARS years (its last sample minus
    its first). The lack that opens the window need not be one the sender held at that sample.
R:  a guild falls to no living bodies in a joined world: the guild's living count, followed row by row
    through lineage.jsonl rather than at the samples, goes from one or more to zero at a time after the
    first year. Holds if after some such fall an unbroken run of samples, each with at least
    R_MIN_ARRIVAL_BODIES living arrival-descended bodies of the guild, spans at least HOLD_SPAN_YEARS
    years. Unread if no guild falls in any joined world. The largest arrival-descended count of the guild
    reached after each fall is printed.
W:  at the end, each joined world's living are grouped by the line they trace to across worlds: an
    arrival's root is followed through its "from" and "fid" to the creature it copies in the sending
    world, that creature's root there, and on through that world's arrivals, until a founder is reached.
    Holds if a single such founder's line holds more than W_MIN_SHARE of the living in every joined world.
    The largest root lineage within each world, counted as V counts, is printed beside it. An arrival
    whose "from" is not the place of a joined world given, or whose "fid" was never born in that world by
    the end read, cannot be traced: its line's living are printed as untraced, belong to no line, and still
    count among the living. The exchange picks in every world before it plants in any (Exchange.cs), so a
    copy is never of a body planted at the same exchange and the trace cannot loop.
Y:  per world, round 57's winter depth for each year from FIRST_YEAR_READ to the last full year (one
    minus the fewest living in the winter over the most living in the summer before it, from stats.jsonl's
    alive). Pearson correlation over the years both worlds share, for each pair within the joined group
    and within the isolated group; holds if the joined pairs' mean exceeds the isolated pairs' mean.
Readings: per world and year, the most living in summer, the fewest in winter, the depth, and at the year's
end the largest root lineage's share. Per joined world at every --every seconds: the living, the
arrival-descended living and their share, and each guild's living and arrival-descended living.
B1 is read by round 54's reader.

Usage: python r58-read.py --joined DIR DIR DIR --isolated DIR DIR DIR [--end 48000] [--year 6000]
                          [--every 1500] [--unpaired]
"""
import argparse
import glob
import gzip
import json
import math
import os
import sys
from collections import Counter

YEAR = 6000.0
END = 48000.0
EVERY = 1500.0
SUMMER_FRAC = (-250.0 / 6000.0, 2750.0 / 6000.0)
WINTER_FRAC = (2750.0 / 6000.0, 5750.0 / 6000.0)
V_MIN_SHARE = 0.01
S_MIN_ARRIVAL_BODIES = 20
R_MIN_ARRIVAL_BODIES = 1
HOLD_SPAN_YEARS = 1.0
W_MIN_SHARE = 0.5
FIRST_YEAR_READ = 2
GUILDS = ("jointed", "absorptive", "leaf")
MAX_FALLS_PRINTED = 6


def rows(path):
    """A JSONL file's complete rows (plain or .gz); a live run's last line may be half written and is skipped."""
    if not os.path.exists(path) and os.path.exists(path + ".gz"):
        path += ".gz"
    opener = gzip.open if path.endswith(".gz") else open
    with opener(path, "rt", encoding="utf-8") as f:
        for line in f:
            if line.endswith("\n") and line.strip():
                yield json.loads(line)


def resolve(d):
    if os.path.exists(os.path.join(d, "run.json")):
        return d
    dirs = sorted(x for x in glob.glob(os.path.join(d, "*")) if os.path.exists(os.path.join(x, "run.json")))
    if not dirs:
        sys.exit(f"no run.json in {d} or its subfolders")
    return dirs[-1]


def guild_bits(r):
    """(jointed, absorptive, leaf) from a birth row's flags, round 57's definitions."""
    j, a, p = r.get("jnt") == 1, r.get("abs") == 1, r.get("pho") == 1
    return (j, a, p and not j and not a)


class World:
    def __init__(self, d, end, year, every):
        self.dir = resolve(d)
        self.manifest = json.load(open(os.path.join(self.dir, "run.json"), encoding="utf-8"))
        m = self.manifest
        self.name = m.get("arm") or os.path.basename(os.path.dirname(os.path.normpath(self.dir)))
        self.migrating = "migrateEverySeconds" in m
        self.place = m.get("groupPlace")
        names = [s.strip() for s in m.get("group", "").split(",") if s.strip()]
        self.sender_place = names.index(m["migrateFrom"]) if self.migrating and m.get("migrateFrom") in names else None
        self.year, self.every = year, every
        self.notes = []

        self.stats = [(r["t"], r["alive"], r.get("jointed"), r.get("absorptive"), r.get("photosynthetic"))
                      for r in rows(os.path.join(self.dir, "stats.jsonl"))]
        self.reach = self.stats[-1][0] if self.stats else 0.0
        self.end = min(end, self.reach)
        if self.reach < end:
            self.notes.append(f"the run reaches {self.reach:g} s, short of --end {end:g} s; read to {self.end:g} s")
        self._read_lineage()
        self._read_immigrants()
        self._years()

    # -- lineage: one pass, with snapshots at the marks and the guilds' falls followed row by row
    def _read_lineage(self):
        year, every, end = self.year, self.every, self.end
        marks = set()
        k = 1
        while k * every <= end + 1e-9:
            marks.add(round(k * every, 6))
            k += 1
        y = 1
        while y * year <= end + 1e-9:
            marks.add(round(y * year, 6))
            y += 1
        marks.add(round(end, 6))
        marks = sorted(marks)

        self.root = {}        # id -> its root's id
        self.rootinfo = {}    # root id -> (kind, src, from, fid, t)
        alive = {}            # id -> guild bits
        alive_in = Counter()  # root -> members alive
        g_n = [0, 0, 0]       # living per guild
        g_arr = [0, 0, 0]     # living arrival-descended per guild
        g_ever = [False, False, False]
        arr_n = 0
        self.snap = {}        # mark -> dict
        self.falls = []       # (t, guild index)
        self.births_m = 0
        prev_t = -1.0
        mi = 0

        def take(at):
            top = max(alive_in.values(), default=0)
            self.snap[at] = {"alive": len(alive), "arr": arr_n, "g": tuple(g_n), "g_arr": tuple(g_arr),
                             "top": top, "top_share": top / len(alive) if alive else None}

        for r in rows(os.path.join(self.dir, "lineage.jsonl")):
            e = r.get("e")
            if e not in ("b", "d"):
                continue
            t = r["t"]
            if t < prev_t:
                self.notes.append(f"lineage.jsonl out of time order at t={t}")
            prev_t = t
            while mi < len(marks) and t > marks[mi]:
                take(marks[mi])
                mi += 1
            if t > end:
                break
            i = r["id"]
            if e == "b":
                p = r.get("p", -1)
                if p >= 0 and p in self.root:
                    self.root[i] = self.root[p]
                else:
                    if p >= 0:
                        self.notes.append(f"birth {i} names parent {p}, never born here; rooted at itself")
                    self.root[i] = i
                    self.rootinfo[i] = (r.get("k"), r.get("src"), r.get("from"), r.get("fid"), t)
                    if r.get("k") == "m":
                        self.births_m += 1
                bits = guild_bits(r)
                alive[i] = bits
                ri = self.root[i]
                alive_in[ri] += 1
                is_arr = self.rootinfo[ri][0] == "m"
                arr_n += is_arr
                for gi in range(3):
                    if bits[gi]:
                        g_n[gi] += 1
                        g_ever[gi] = True
                        g_arr[gi] += is_arr
            elif i in alive:
                bits = alive.pop(i)
                ri = self.root[i]
                alive_in[ri] -= 1
                if alive_in[ri] == 0:
                    del alive_in[ri]
                is_arr = self.rootinfo[ri][0] == "m"
                arr_n -= is_arr
                for gi in range(3):
                    if bits[gi]:
                        g_n[gi] -= 1
                        g_arr[gi] -= is_arr
                        if g_n[gi] == 0 and t > year:
                            self.falls.append((t, gi))
        while mi < len(marks):
            take(marks[mi])
            mi += 1
        self.alive_end = dict(alive_in)
        self.n_end = len(alive)
        self.samples = [t for t in marks if abs(t / every - round(t / every)) < 1e-9]
        # lineage's living against stats' at every sample, and the birth flags against stats' guild counts at the end
        st = {s[0]: s for s in self.stats}
        worst = max(((abs(self.snap[t]["alive"] - st[t][1]), t) for t in self.samples if t in st), default=None)
        if worst and worst[0] > 0:
            self.notes.append(f"lineage's living differs from stats' alive by up to {worst[0]} (at {worst[1]:g} s)")
        last = st.get(self.end)
        if last:
            g = self.snap[self.end]["g"]
            self.flag_check = (f"at {self.end:g} s, birth flags: jointed {g[0]}, absorptive {g[1]}, leaf {g[2]}; "
                               f"stats: jointed {last[2]}, absorptive {last[3]}, photosynthetic {last[4]}")
        else:
            self.flag_check = None

    def _read_immigrants(self):
        path = os.path.join(self.dir, "immigrants.jsonl")
        self.planted = self.refused = 0
        if os.path.exists(path):
            for r in rows(path):
                if r["t"] > self.end:
                    continue
                if r["id"] >= 0:
                    self.planted += 1
                else:
                    self.refused += 1
        if self.births_m != self.planted:
            self.notes.append(f"lineage holds {self.births_m} arrival births and immigrants.jsonl {self.planted} planted")

    def _years(self):
        self.years = {}
        y = 1
        while True:
            base = (y - 1) * self.year
            s0, s1 = base + SUMMER_FRAC[0] * self.year, base + SUMMER_FRAC[1] * self.year
            w0, w1 = base + WINTER_FRAC[0] * self.year, base + WINTER_FRAC[1] * self.year
            if w1 > self.end:
                break
            summer = [a for t, a, *_ in self.stats if max(s0, 0) <= t < s1]
            winter = [a for t, a, *_ in self.stats if w0 <= t < w1]
            most = max(summer) if summer else None
            fewest = min(winter) if winter else None
            depth = 1 - fewest / most if most and fewest is not None else None
            ye = round(y * self.year, 6)
            top = self.snap.get(ye, {}).get("top_share")
            self.years[y] = {"most": most, "fewest": fewest, "depth": depth, "top": top}
            y += 1

    def series(self, key, gi=None, after=None):
        out = []
        for t in self.samples:
            if after is not None and t <= after:
                continue
            v = self.snap[t][key]
            out.append((t, v if gi is None else v[gi]))
        return out


def longest_run(series, ok, year):
    """The longest unbroken run of samples meeting ok: (span in seconds, first t, last t, largest value)."""
    best = (None, None, None, None)
    start = None
    peak = 0
    for t, v in series:
        if ok(v):
            if start is None:
                start, peak = t, v
            peak = max(peak, v)
            span = t - start
            if best[0] is None or span > best[0]:
                best = (span, start, t, peak)
        else:
            start = None
    return best


def pearson(xs, ys):
    n = len(xs)
    if n < 2:
        return None
    mx, my = sum(xs) / n, sum(ys) / n
    sx = math.sqrt(sum((x - mx) ** 2 for x in xs))
    sy = math.sqrt(sum((y - my) ** 2 for y in ys))
    if sx == 0 or sy == 0:
        return None
    return sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / (sx * sy)


def fmt(x, f="{:.3f}"):
    return "-" if x is None else f.format(x)


def origin(worlds_by_place, w, i, depth=0):
    """The founder a body's line traces to across the joined worlds: (place, root id, kind/src) or (None, reason)."""
    if depth > 1000:
        return (None, "trace looped")
    if i not in w.root:
        return (None, f"id {i} never born in {w.name} by {w.end:g} s")
    r = w.root[i]
    k, src, frm, fid, t = w.rootinfo[r]
    if k != "m":
        return (w.place, r, src or k)
    sender = worlds_by_place.get(frm)
    if sender is None:
        return (None, f"sender place {frm} not among the joined worlds")
    return origin(worlds_by_place, sender, fid, depth + 1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--joined", nargs="+", required=True)
    ap.add_argument("--isolated", nargs="+", required=True)
    ap.add_argument("--end", type=float, default=END)
    ap.add_argument("--year", type=float, default=YEAR)
    ap.add_argument("--every", type=float, default=EVERY)
    ap.add_argument("--unpaired", action="store_true")
    a = ap.parse_args()
    if not a.unpaired and len(a.joined) != len(a.isolated):
        sys.exit("paired reading needs as many isolated worlds as joined; pass --unpaired otherwise")
    year, every = a.year, a.every
    span_needed = HOLD_SPAN_YEARS * year

    joined = [World(d, a.end, year, every) for d in a.joined]
    isolated = [World(d, a.end, year, every) for d in a.isolated]
    by_place = {w.place: w for w in joined}
    if len(by_place) != len(joined):
        print("WARNING: two joined worlds share a group place; the trace across worlds is ambiguous")

    print(f"== round 58's reader: year {year:g} s, samples every {every:g} s, end {a.end:g} s"
          f"{', unpaired' if a.unpaired else ''}")
    for label, group in (("joined", joined), ("isolated", isolated)):
        for w in group:
            sender = by_place.get(w.sender_place)
            print(f"{label} {w.name}: {w.dir}")
            print(f"   seed {w.manifest.get('seed')}, place {w.place}, "
                  + (f"hears from place {w.sender_place} ({sender.name if sender else 'not given'}), "
                     f"every {w.manifest['migrateEverySeconds']:g} s, {w.manifest['migrateCount']} a time; "
                     if w.migrating else "not migrating; ")
                  + f"arrivals planted {w.planted}, refused {w.refused}; read to {w.end:g} s")
            if label == "isolated" and w.migrating:
                print("   WARNING: an isolated world whose run.json migrates")
            if label == "joined" and not w.migrating:
                print("   WARNING: a joined world whose run.json does not migrate")
            if w.flag_check:
                print(f"   {w.flag_check}")
            for n in w.notes:
                print(f"   NOTE: {n}")

    # -- readings
    print("\n== readings: per world and year")
    print(f"   {'world':<12} {'year':>4} {'summer most':>11} {'winter fewest':>13} {'depth':>6} {'top root':>8}")
    for w in joined + isolated:
        for y, rec in w.years.items():
            print(f"   {w.name:<12} {y:>4} {fmt(rec['most'], '{}'):>11} {fmt(rec['fewest'], '{}'):>13} "
                  f"{fmt(rec['depth']):>6} {fmt(rec['top']):>8}")
        if not w.years:
            print(f"   {w.name:<12} no full year by {w.end:g} s")
    print("\n== readings: per joined world at every sample (living / arrival-descended living per guild)")
    for w in joined:
        print(f"   {w.name}")
        print(f"   {'t':>8} {'alive':>6} {'arr.':>6} {'share':>6}   "
              + "   ".join(f"{g[:4]:>4} {'arr':>4}" for g in GUILDS))
        for t in w.samples:
            s = w.snap[t]
            share = s["arr"] / s["alive"] if s["alive"] else 0.0
            print(f"   {t:>8g} {s['alive']:>6} {s['arr']:>6} {share:>6.1%}   "
                  + "   ".join(f"{s['g'][gi]:>4} {s['g_arr'][gi]:>4}" for gi in range(3)))

    verdicts = []

    # -- V
    print(f"\n== V: root lineages holding at least {V_MIN_SHARE:.0%} of the living at the end")
    def v_count(w):
        lines = [(n, r) for r, n in w.alive_end.items() if w.n_end and n / w.n_end >= V_MIN_SHARE]
        kinds = Counter(("arrival" if w.rootinfo[r][0] == "m" else (w.rootinfo[r][1] or w.rootinfo[r][0]))
                        for _, r in lines)
        return len(lines), kinds
    vc = {}
    for w in joined + isolated:
        n, kinds = v_count(w)
        vc[id(w)] = n
        print(f"   {w.name:<12} {n:>3} lines of {len(w.alive_end)} alive, {w.n_end} living ("
              + ", ".join(f"{k} {c}" for k, c in sorted(kinds.items())) + ")")
    held = 0
    for i, w in enumerate(joined):
        bar = [isolated[i]] if not a.unpaired else isolated
        ok = all(vc[id(w)] > vc[id(b)] for b in bar)
        held += ok
        print(f"   {w.name} {vc[id(w)]} against " + ", ".join(f"{b.name} {vc[id(b)]}" for b in bar)
              + f" -> {'exceeds' if ok else 'does not exceed'}")
    verdicts.append(("V", "held" if held == len(joined) else "failed", f"{held} of {len(joined)} joined worlds exceed"))

    # -- S
    print(f"\n== S: a guild a joined world lacks after {year:g} s, later held by >= {S_MIN_ARRIVAL_BODIES} "
          f"arrival-descended bodies for {HOLD_SPAN_YEARS:g} year(s)")
    readable = []
    s_hold = []
    for w in joined:
        snd = by_place.get(w.sender_place)
        for gi, g in enumerate(GUILDS):
            ser = w.series("g", gi, after=year)
            lacks = [t for t, v in ser if v == 0]
            if not lacks:
                continue
            sender_holds = [t for t in lacks if snd and t in snd.snap and snd.snap[t]["g"][gi] > 0]
            if sender_holds:
                readable.append((w.name, g, sender_holds[0]))
            t0 = lacks[0]
            span, t1, t2, peak = longest_run(w.series("g_arr", gi, after=t0), lambda v: v >= S_MIN_ARRIVAL_BODIES, year)
            peak_all = max((v for _, v in w.series("g_arr", gi, after=t0)), default=0)
            ok = span is not None and span >= span_needed
            if ok:
                s_hold.append((w.name, g))
            print(f"   {w.name} {g}: lacked at {len(lacks)} samples from {t0:g} s"
                  + (f"; its sender {snd.name} held it at {len(sender_holds)} of them, first {sender_holds[0]:g} s"
                     if sender_holds else "; its sender never held it at those samples")
                  + f"; arrival-descended {g} after: most {peak_all}"
                  + (f", longest run >= {S_MIN_ARRIVAL_BODIES} spans {span:g} s ({t1:g} to {t2:g} s)" if span is not None
                     else f", never >= {S_MIN_ARRIVAL_BODIES}")
                  + (" -> holds" if ok else ""))
    if not readable:
        lacked_any = any(v == 0 for w in joined for gi in range(3) for _, v in w.series("g", gi, after=year))
        reason = ("no joined world lacks a guild at a sample after the first year" if not lacked_any else
                  "a joined world lacks a guild after the first year, but never while its sender holds it")
        print(f"   unread: {reason}")
        verdicts.append(("S", "unread", reason))
    else:
        print("   readable: " + "; ".join(f"{n} lacks {g} at {t:g} s while its sender holds it" for n, g, t in readable))
        verdicts.append(("S", "held" if s_hold else "failed",
                         ("in " + ", ".join(f"{n} ({g})" for n, g in s_hold)) if s_hold else "no lacked guild taken up"))

    # -- R
    print(f"\n== R: a guild falls to no living after {year:g} s; arrival-descended bodies of it "
          f"(>= {R_MIN_ARRIVAL_BODIES}) alive at every sample for {HOLD_SPAN_YEARS:g} year(s)")
    r_hold, any_fall, r_best = [], False, 0
    for w in joined:
        sample_zero = {g: any(v == 0 for _, v in w.series("g", gi, after=year)) for gi, g in enumerate(GUILDS)}
        for gi, g in enumerate(GUILDS):
            falls = [t for t, x in w.falls if x == gi]
            if not falls:
                if sample_zero[g]:
                    print(f"   {w.name} {g}: zero at a sample after the first year but no fall followed there "
                          f"(never held after it fell before the year, or never held)")
                continue
            any_fall = True
            outs = []
            for t in falls:
                ser = w.series("g_arr", gi, after=t)
                span, t1, t2, _ = longest_run(ser, lambda v: v >= R_MIN_ARRIVAL_BODIES, year)
                peak = max((v for _, v in ser), default=0)
                r_best = max(r_best, peak)
                ok = span is not None and span >= span_needed
                outs.append((t, span, t1, t2, peak, ok))
                if ok:
                    r_hold.append((w.name, g, t))
            print(f"   {w.name} {g}: {len(falls)} falls to zero, first {falls[0]:g} s"
                  + ("" if sample_zero[g] else " (none seen at a sample)"))
            for t, span, t1, t2, peak, ok in outs[:MAX_FALLS_PRINTED]:
                print(f"      fell {t:g} s: arrival-descended {g} after it most {peak}"
                      + (f", longest run spans {span:g} s ({t1:g} to {t2:g} s)" if span is not None else ", none at any sample")
                      + (" -> holds" if ok else ""))
            if len(outs) > MAX_FALLS_PRINTED:
                print(f"      ... and {len(outs) - MAX_FALLS_PRINTED} more falls"
                      + (f", {sum(o[5] for o in outs[MAX_FALLS_PRINTED:])} holding" if any(o[5] for o in outs) else ""))
    if not any_fall:
        print("   unread: no guild falls to zero in a joined world after the first year")
        verdicts.append(("R", "unread", "no guild falls to zero after the first year"))
    else:
        verdicts.append(("R", "held" if r_hold else "failed",
                         (", ".join(f"{n} {g} after {t:g} s" for n, g, t in r_hold[:3]) if r_hold else "no return held")
                         + f"; largest arrival-descended count after a fall {r_best}"))

    # -- W
    print(f"\n== W: one line over {W_MIN_SHARE:.0%} of the living in every joined world at the end")
    by_origin = {}
    for w in joined:
        groups, untraced = Counter(), Counter()
        for r, n in w.alive_end.items():
            o = origin(by_place, w, r)
            if o[0] is None:
                untraced[o[1]] += n
            else:
                groups[o] += n
        by_origin[id(w)] = groups
        top_local = max(((n, r) for r, n in w.alive_end.items()), default=None)
        top_o = groups.most_common(1)[0] if groups else None
        print(f"   {w.name}: largest root lineage here "
              + (f"{top_local[1]} ({w.rootinfo[top_local[1]][0]}/{w.rootinfo[top_local[1]][1] or '-'}) "
                 f"{top_local[0] / w.n_end:.3f}" if top_local else "none")
              + "; largest line traced across worlds "
              + (f"place {top_o[0][0]} root {top_o[0][1]} ({top_o[0][2]}) {top_o[1] / w.n_end:.3f}" if top_o else "none")
              + (f"; untraced {sum(untraced.values())} living: " + "; ".join(f"{k} ({c})" for k, c in untraced.items())
                 if untraced else ""))
    origins = set().union(*(set(by_origin[id(w)]) for w in joined)) if joined else set()
    best = None
    for o in origins:
        shares = [by_origin[id(w)].get(o, 0) / w.n_end if w.n_end else 0 for w in joined]
        if best is None or min(shares) > min(best[1]):
            best = (o, shares)
    if best:
        o, shares = best
        ok = all(s > W_MIN_SHARE for s in shares)
        print(f"   the line with the greatest least share: place {o[0]} root {o[1]} ({o[2]}), shares "
              + ", ".join(f"{w.name} {s:.3f}" for w, s in zip(joined, shares)))
        verdicts.append(("W", "held" if ok else "failed", f"least share {min(shares):.3f}"))
    else:
        verdicts.append(("W", "unread", "no living in the joined worlds"))

    # -- Y
    print(f"\n== Y: winter depths, years {FIRST_YEAR_READ} to the last full year, correlated within each group")
    means = {}
    for label, group in (("joined", joined), ("isolated", isolated)):
        rs = []
        for i in range(len(group)):
            for j in range(i + 1, len(group)):
                u, v = group[i], group[j]
                ys = [y for y in u.years if y >= FIRST_YEAR_READ and y in v.years
                      and u.years[y]["depth"] is not None and v.years[y]["depth"] is not None]
                r = pearson([u.years[y]["depth"] for y in ys], [v.years[y]["depth"] for y in ys])
                note = " (two years: +-1 by construction)" if len(ys) == 2 else ""
                print(f"   {label} {u.name} x {v.name}: years {ys[0] if ys else '-'} to {ys[-1] if ys else '-'} "
                      f"({len(ys)}), r = {fmt(r)}{note}")
                if r is not None:
                    rs.append(r)
        means[label] = sum(rs) / len(rs) if rs else None
        print(f"   {label} mean r = {fmt(means[label])} over {len(rs)} pairs")
    if means["joined"] is None or means["isolated"] is None:
        verdicts.append(("Y", "unread", "a group has no defined correlation"))
    else:
        verdicts.append(("Y", "held" if means["joined"] > means["isolated"] else "failed",
                         f"joined mean {means['joined']:.3f} against isolated {means['isolated']:.3f}"))

    print("\n== clauses")
    for k, v, note in verdicts:
        print(f"{k}: {v} ({note})")


if __name__ == "__main__":
    main()
