# Grazing advice: facts gathered on 2026-10-12 (Fable subagent, read-only)

Files here: `economics.txt` (the mouth arithmetic by hand, from round 58's config and the code's
formulas), `r58s1j-median-leaf.json` (a living leaf body of the modal plan, id 164059),
`r58s1j-mouth.json` (a living round-58 body carrying a consumer node at intake 0), `ledger-leaf-try1.txt`
(the built ledger refusing format 12). The ledger was not rebuilt: the permission layer refused the
build beside round 58.

## Measured (round 58, runs/r58-s*j/2026-10-12-080742-dfc0e881, last stats row at 48,000 s)
- alive 5,391 / 6,941 / 5,327; partsKilled 0, bodiesEaten 0, corpsesFromKills 0 in every seed.
- unitsEaten 223 / 286 / 218 (units of 100 J); corpsesEaten 700 / 841 / 823; intakeShare 0.09 / 0.14 / 0.02 %;
  attackShare 0 / 0 / 0.09 %; protectionShare 0.3 / 0.8 / 0.8 %.
- contact: overlapPairsPerStep 269 / 334 / 144, 97% held; overlapBodies per step about 116 in seed 1 (2% of the living).
- depth: meanHeight -10.8 / -13.5 / -11.5 m, sd 6.5 to 7.3 m; the bed is -25 m; corpses 96% settled, 375 of 422 kJ on the bed (seed 1).
- seed 1 lineage: 179,318 births; atk>0 481, ink>0 444, prt>0 3,703; kill rows 0.
- seed 1 living genomes: every sampled root is photosynthetic; modal plan leaf + 2 bladders; parts median 3;
  126 of 5,391 carry a consumer node, all at intake 0; 0 nodes with attack > 0.
- median leaf body: leaf 0.043 m3 and 7.0 m2 (a 1.3 x 2.7 m sheet 12 mm thick), bladder 0.16 m3; body 0.21 m3, 103 J tissue.

## Read from code
- WorldMouth.ApplyIntake credits `creature.Energy` directly; no ledger, so no handling cost and no `food` measure in the nursery.
- Corpse carries CreatureId, not the killer; the kill lineage row carries `by`; WorldState writes corpses to the checkpoint.
- Reach: corpse radius + 0.5 m from the eater's centre; the corpse is placed at the victim's body point.
- Attack caps: structural 1, link 0.5, consumer 1; leaf, bladder, neural 0. Intake: consumer only. Founders draw attack 0.
- Mutator.MoveAttribute: chance 0.005 per node per birth, step N(0, 0.15 x range) clamped at the floor.
- Nursery recipe: bodies founders|pool, evolve brain|kind, tankSize, carrion count/joules, reward Net|WeightedSum over
  start,end,net,food,light,work,upkeep,handling,exuded,path,fresh,survived,diedAt. No setting lays non-scored bodies.
- Round 45 (logbook/0114): one killer clade, 62 kills, none of the 62 corpses touched; rounds 46, 48, 50: no kills.
