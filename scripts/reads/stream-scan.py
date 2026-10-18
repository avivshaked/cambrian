"""Find weird or striking bodies from runs' own state streams (poses.bin): what each body really was.
One frame every 100 simulated seconds. Writes scratch/creature-pick/stream-<arm>.tsv and prints a summary."""
import importlib.util, glob, os, sys, collections
spec = importlib.util.spec_from_file_location("pr", "scripts/poses-read.py")
pr = importlib.util.module_from_spec(spec); spec.loader.exec_module(pr)
GUILD = {1: "stomach", 2: "joints", 4: "leaf"}
def gname(f): return "+".join(v for k, v in GUILD.items() if f & k) or "none"
for arm in sys.argv[1:]:
    d = sorted(glob.glob(f"runs/{arm}/*/"))[-1]
    path = pr.stream_path(d)
    size = os.path.getsize(path)
    rec = {}  # id -> [first, last, maxdof, flags union, frames, maxfrac]
    with open(path, "rb") as f:
        for secs, offset, n in pr.scan(f, size):
            if int(round(secs)) % 100: continue
            t, bodies = pr.read_frame(f, offset, n)
            for b in bodies:
                r = rec.get(b["id"])
                fl = b["flags"] or 0; dof = len(b["q"]); fr = b["bodyFraction"] or 0
                if r is None: rec[b["id"]] = [t, t, dof, fl, 1, fr]
                else:
                    r[1] = t; r[2] = max(r[2], dof); r[3] |= fl; r[4] += 1; r[5] = max(r[5], fr)
    with open(f"scratch/creature-pick/stream-{arm}.tsv", "w", encoding="utf-8") as o:
        o.write("id\tfirst\tlast\tmaxdof\tflags\tframes\tmaxfrac\n")
        for i, r in rec.items(): o.write(f"{i}\t{r[0]:.0f}\t{r[1]:.0f}\t{r[2]}\t{r[3]}\t{r[4]}\t{r[5]:.2f}\n")
    adults = {i: r for i, r in rec.items() if r[4] >= 5}
    combos = collections.Counter(gname(r[3]) for r in adults.values())
    dofs = collections.Counter(min(r[2], 12) for r in adults.values())
    print(f"\n== {arm}: {len(rec)} bodies seen, {len(adults)} lived 500 s or more")
    print("   kinds:", dict(combos.most_common()))
    print("   joint dof (12 = 12+):", dict(sorted(dofs.items())))
    top = sorted(adults.items(), key=lambda kv: (-kv[1][2], -kv[1][4]))[:4]
    for i, r in top: print(f"   most joints: id {i}, dof {r[2]}, {gname(r[3])}, lived {r[0]:.0f}-{r[1]:.0f} s")
    rare = [(i, r) for i, r in adults.items() if combos[gname(r[3])] <= max(3, len(adults) // 1000)]
    for i, r in sorted(rare, key=lambda kv: -kv[1][4])[:4]: print(f"   rare kind: id {i}, {gname(r[3])}, dof {r[2]}, lived {r[0]:.0f}-{r[1]:.0f} s")
