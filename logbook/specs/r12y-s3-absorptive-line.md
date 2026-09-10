# r12y-s3: the absorptive lineage

Source: `runs/r12y-s3/2026-09-03-002357-a913ec1b/lineage.jsonl`, copied to
`scratch/r12y-s3-lineage-copy.jsonl` before analysis (live process; file grew from 5,959 to
6,026 valid rows between two copies — second copy used throughout). Config hash
`a913ec1b7ca7c6f2` (matches `runs/r12y-s3.md` header). Sample interval in the run report: 100 s.
Data covers births/deaths from t=0 to t≈22,925 (last birth in the copy).

## Schema found (from `LineageEvent.cs`, confirmed against the file)

Birth row: `e:"b", t, id, p (parent id, -1 = no parent), k ("f" floor-spawn / "r" reproduction /
"i" inoculation), g (generation depth), s (species id), abs (0/1 — any developed part is
Absorptive), jnt (0/1 — any actuated joint), pt (horizontal patch index)`.
Death row: `e:"d", t, id, c (cause)`. **Only one `DeathCause` is implemented (`Starved`)** —
every death in the file reads `"starved"`; there is no predation cause yet, so cause-of-death
cannot distinguish predation from starvation in this run.
**No depth, volume, or energy field exists per-creature in lineage.jsonl.** Those only exist as
population-wide aggregates in `stats.jsonl`/`r12y-s3.md` (`meanHeight`, etc.), or inside
`snapshots/*.jsonl`, which store each living creature's **genome graph** (confirmed via
`GenomeJson.Write`/`EvolutionRun.cs:773`), not its developed phenotype, and carry no `id` field.

## Q1 — absorptive creatures: mutant vs inherited

29 of 3,821 births (0.76%) ever carried `abs=1`. **26 mutant** (parent not absorptive, or no
parent) / **3 inherited** (parent also `abs=1`).

- 22 of the 26 mutants are `k:"f"` floor-spawn founders, all born t≤823, all dead within
  ≤428 s, **zero children each** — a founding-population draw, not a lineage.
- 4 mutants arose later via reproduction (`k:"r"`) from non-absorptive parents — i.e. the
  absorptive cell type re-appearing by mutation: id 971 (t=6,961.5), 2860 (t=15,221), 2905
  (t=15,529.5, still alive, childless), 3333 (t=18,018, still alive, childless).
- Only **971** produced a surviving absorptive line. 2860/2905/3333 are dead ends.

## Q2 — the inherited line's family tree

Founding mutant **id 971**, born t=6,961.5 (g=17), to parent **id 682** (non-absorptive,
non-jointed, `k:"r"`, g=16, born t=5,567.5, patch 3). The line is a **strict single-child
chain, no branching** — each parent's only recorded offspring (of any type) was the next
absorptive individual:

```
682 (non-abs, g16, born 5567.5, died 11571.5 starved)
 └─ 971  (abs, jnt=0, g17, patch3) born 6961.5,  died 14684.5 starved, age 7723.0,  1 child
     └─ 1403 (abs, jnt=0, g18, patch3) born 8839.5,  died 17089.5 starved, age 8250.0, 1 child
         └─ 1741 (abs, jnt=0, g19, patch3) born 10100.0, died 18344.0 starved, age 8244.0, 1 child
             └─ 2470 (abs, jnt=0, g20, patch3) born 13155.0, died 22650.5 starved, age 9495.5, 0 children
```

All four stayed on horizontal patch 3, all unjointed, all died starved. Ages at death: 7,723 /
8,250 / 8,244 / 9,495.5 s — the line got longer-lived each generation, not shorter, right up to
the end.

## Q3 — why the line died out (last death t=22,650.5)

Every death in the file is coded `"starved"`, so cause-of-death carries no discriminating
information — this compares age at death, brood size, and *when* deaths land relative to
`starved`-only coding.

Window t=9,000–22,000 (the line's active span), comparing the **inherited absorptive line**
(all 4 members, none confined to the window since only 1741/2470 were born inside it) against
**producers** (all non-absorptive births in the window, n=2,318) and **all absorptive births**
in the window (mutant+inherited, n=5):

| group | n | dead | alive | mean age at death (s) | mean children | cause dist. |
|---|---|---|---|---|---|---|
| inherited line (971,1403,1741,2470) | 4 | 4 | 0 | 8,428.1 | 0.75 | 100% starved |
| all absorptive born in window | 5 | 3 | 2 | 7,802.8 | 0.20 | 100% starved |
| producers born in window | 2,318 | 851 | 1,467 | 6,310.4 | 0.93 | 100% starved |

The inherited line actually **outlived** producers by ~2,100 s on average and starved at the
same rate as everyone else — it did not die from being weak. It died from **brood failure**:
three generations in a row each had exactly one recorded child, and the fourth (2470) had
none before starving at age 9,495.5. One missed birth anywhere in that chain ends the line,
and eventually one did. This is a thin-lineage/single-line-of-descent extinction, not an
elevated death rate.

## Q4 — depth

**Not measurable per creature** — lineage.jsonl has no depth field and snapshots have no `id`
to join against. Only population-wide `meanHeight` ("depth m" in the report) exists, not split
by guild. Context only: across the line's lifespan, the *whole population's* mean height rose
from −8.3 m (t=7,000) to −5.7 m (t=22,700) — shallowing overall. No per-guild claim follows.

## Q5 — size / mixed vs. pure absorber (inferred, see caveat)

I found that at every 1,000 s snapshot, the row order matches the ascending-`id` order of
creatures alive at that timestamp exactly (row counts matched at t=7000/9000/13000/17000/20000;
structural match confirmed by cross-checking known abs/jnt flags against node lists). Using
that alignment: **971/1741/2470's genome graph is identical across generations** —
`photosynthetic, buoyancy, photosynthetic, absorptive`, no joints — i.e. two photosynthetic
nodes alongside the absorptive one. **Caveat, confirmed by reading source:** snapshot "nodes"
is the compact **genome graph** (`GenomeJson.Write`, `EvolutionRun.cs:773`), not the developed
**phenotype** (`World.cs:1112`'s `HasAbsorptive` walks `Phenotype.Parts`, a different list) —
a genome node can exist without ever being expressed. So "genome carries photosynthetic nodes"
is not proof the grown body actually had working photosynthetic tissue; it is suggestive, not
confirmed. Body-volume proxy (bounding-box product summed over genome nodes) for creatures
carrying an absorptive node at t=13,000: mean 0.154 m³ (n=8) vs. 0.168 m³ for non-absorptive
bodies (n=1,353) — essentially the same size, no meaningful difference.

## Q6 — surprising, and what's inferred vs. measured

- **Measured:** absorptive creatures are jointed far more often than the rest of the
  population — 8 of 29 ever (27.6%) vs. 31 of 3,792 non-absorptive (0.8%), a ~34× rate
  difference. Small n (29), but large enough to be worth a flag rather than a shrug.
- **Measured:** the whole inherited line lived on horizontal patch 3 for four generations
  straight, vs. ~44% of producer births on that patch (1,829/3,792) — patch-3 favourable, or
  just a line that never dispersed.
- **Measured:** `abs=1` genuinely means "expressed," not "genetically present" — id 682 (the
  founding mutant's own parent, `abs=0` in lineage) carries an absorptive node in its genome
  graph that was evidently never instantiated in its phenotype. Anyone reading snapshots
  expecting them to equal "what abs/jnt flags represent" will be misled; they are two
  different representations of the same creature (§"Q5 caveat" above), a real trap in this
  run's data, not a bug.
- **Inferred, not measured:** brood-size-1 chains are not unusual in this population overall
  (2,403 of 3,821 parents-with-children had exactly one, the modal outcome) — so the line's
  extinction is an ordinary tail event compounded four times in a row, not a special weakness
  of absorptive creatures specifically.
- The three later independent absorptive mutants (2860/2905/3333) had 0 children each despite
  two still being alive at copy time — the trait keeps re-arising by mutation but almost never
  re-establishes a line; 971's chain is the only one that ever got past generation zero.
