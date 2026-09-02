# Proposal: round 10 — stop the drowning

*2026-09-02. Agent proposal for the owner's ruling; absorbed into DECISIONS.md on decision,
then deleted. Round 9's last two arms are still running, but its verdict is formally
determined (≤2 of 5) and nothing they do changes the diagnosis below.*

## The diagnosis this proposal rests on

Rounds 8 and 9 scored zero passes between them, and the failure analysis
([0044](logbook/0044-three-medicines.md) results,
[0045](logbook/0045-the-dose-and-the-dice.md)) converged on a mechanism that none of the
treatments touched:

1. **Every creature is denser than water (`ExcessDensity` 0.02 kg/m³) and sinks its whole
   life.** The population holds the photic band only because births keep replenishing the
   top — birth is the only upward flux selection maintains.
2. **A matter drought pauses births; the standing crowd sinks out of the light in
   ~1,500 s; the world is then unrecoverable** — creatures starve in the dark while free
   matter accumulates above them. Three worlds died exactly this way with full larders
   (r9-s1, r9-s2, control d056-s3; depth means sliding −20→−49, −20→−37, −65→−98 m).
3. **The buoyancy escape hatch exists and selection throws it away.** Float tissue costs
   upkeep plus `LiftCost` 0.05 W per unit lift, permanently; a floatless producer breeds
   cheaper before it sinks out. Founders carry float at 50%; evolved populations hold it
   at ~1%. In r9-s2 the literal last survivor was a floater holding at −14.5 m — the
   insurance works, but one insured survivor is not a population.
4. Downstream of this, **chain arrival is drought-gated, not mutation-gated**: 5× mutation
   supplied absorptive singletons all round and none could breed through a drought.

The treatments of rounds 8–9 metered the larder. The patient was drowning.

## The levers (all world rules — the owner's call)

| # | lever | change | mechanism | cost |
|---|---|---|---|---|
| L1 | **Lighter tissue** | `EVOSIM_EXCESS_DENSITY` 0.02 → 0.005 | sinking 4× slower; a 1,500 s drought costs ~3 m of depth instead of ~12 — survivable, recoverable | one knob, zero code |
| L2 | **Cheap float** | `EVOSIM_LIFT_COST` 0.05 → 0.01 | insurance becomes affordable; selection can *keep* float instead of pricing it out between crises — the world stays buoyancy-Darwinian rather than buoyancy-free | one knob, zero code |
| L3 | **Mixed-layer turbulence** | new mechanism: vertical stirring of passive bodies in the top N m | physically the honest one (real mixed layers resuspend plankton) and preserves depth as a live axis | new code + tests mid-campaign |

Real plankton use all three: density tricks (L1), gas vacuoles (L2), and turbulence (L3).

## Recommendation

**Two arms, one round: L1 alone (`r10a`) and L2 alone (`r10b`), five seeds each, scored
under the unchanged D063 rule; mutation stays at 0.005 (the ratified discovery regime) so
arrival gets its chances once droughts stop being fatal. No refuge — the pantry meter goes
back on the shelf until a chain exists to need it.** L3 is the better physics but the
wrong week for new code; if L1 or L2 passes, L3 can later replace it as the honest
version of the same fix.

Why not both knobs in one arm: rounds 8–9 taught (expensively) that a two-mechanism arm
that fails teaches nothing. One lever per arm, and the failure signatures stay readable.

Named risks, two-sided: (a) a world that no longer drowns may instead **run away** — the
8,000 ceiling censored four arms in round 8, and weakening the conveyor strengthens
producer booms; if both arms censor by ceiling, the next conversation is about the light
economy, not buoyancy. (b) L1 dulls the future movement prize (swimming up matters less
when nothing sinks fast); L2 keeps it sharp — a swimmer pays for depth control only when
needed, which is precisely the argument that movement should eventually pay. If the arms
tie, that tiebreak favours L2.

## What I need from you

1. A ruling on the round-10 design (L1+L2 as proposed / different doses / L3 instead /
   something else).
2. Confirmation that dropping the refuge for round 10 is acceptable — it changes the
   answer's shape from "the refuge dose was right" to "the world stops drowning", which I
   read as squarely inside your "get the goal met and move on" priority, but it is a
   direction change and therefore yours.
