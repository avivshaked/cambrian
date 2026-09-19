# Inocula

Genomes and configuration files the logbook points at: bodies this project's own worlds
produced and later took back in as experiment inputs, plus the configs needed to read them.
They are tracked rather than left in `scratch/`, because an entry that cites a genome and
cannot show it proves nothing. [`LICENSE-DOCS`](../LICENSE-DOCS) puts this directory under
the code licence (D060).

Every genome here was copied byte for byte out of a run's own `snapshots/`. None was written
by hand, and none is edited: a genome file wearing a body's identity has to be that body.
A stored genome is refused by any build whose `GenomeJson.FormatVersion` has moved past it
(CLAUDE.md's gotcha on formats 3, 4 and 5). Several of these load only under the build they
were taken from.

## The files

| file | what it is | who cites it |
|---|---|---|
| `d056-s5-absorptive.json` | the largest-volume absorptive genome in round 6 seed 5's final snapshot at t=22,721; three nodes, developing to one absorptive sphere of about 0.023 m³, brood 2. SHA-256 `e6f8e4da1edb…` | D060; logbook/0043, the first transplant |
| `inoculum-r13a-s2-t17000.json` | `r13a-s2`'s stomach at t=17,000: a single-node pure stomach, brood 1, endowment 103 J. SHA-256 `342f472a7dda…` | logbook/0051, the invasion assay; `rounds/launch-r15.ps1` points `EVOSIM_INOCULATE` at it; `scripts/ledger.ps1`'s usage block |
| `s4-stomach-1.json` | `r14c10-s4` snapshot 17,000, row 1747: a one-node absorptive box | no entry names the file; logbook/0050 and 0053 are the readings it was pulled for (the dissection that found stomachs which should have bred and did not) |
| `s4-stomach-2.json` | `r14c10-s4` snapshot 29,000, row 1788 | as above |
| `s4-stomach-3.json` | `r14c10-s4` snapshot 19,000, row 1808 | as above |
| `s4-mixo-2node.json` | `r14c10-s4` snapshot 20,000, row 1775: a two-node mixotroph, absorptive root with one edge | the concrete case behind CLAUDE.md's gotcha that a genome can carry an absorptive node it never expresses, because development prunes the subtree for volume |
| `s4-config-patched.json` | `r14c10-s4`'s own `config.json` with `feeding.exudationFraction` added at 0, so a build that had the tunable would read it; refused by every build since the growth group and kept as history | CLAUDE.md's gotcha that adding a tunable makes every older config unreadable; kept as the worked example of bringing one forward |
| `r28s4-stomach.json` | `r28-s4` snapshot 26,000, row 1778 | no entry names the file; it is the earlier of the two bodies the D082 ledger pass looked at |
| `r28s4-stomach2.json` | `r28-s4` snapshot 30,000, row 1736: absorptive root, one neuron, one input, developing to one absorptive part of 0.0048 m³ | `logbook/specs/price-ledger.md`, the ledger pass that priced a neuron for D082 |
| `growth-ledger-config.json` | round 41 seed 1's own `config.json` (`configHash a2aa45a689c188e6`, the one-substance economy of D098 at 3,000 units and a 100 J overhead, dt 0.01); regenerated 2026-09-18 from the base round, since every config written before D098's build is refused by it | the config half of the ledger pass (D087, logbook/0081; D098, `logbook/specs/economy-spec.md` §7); `scripts/ledger.ps1 -Config` |
| `growth-ledger-genome.json` | a format-6 genome from `r41o100b3k-s1`'s 2,000 s snapshot (id 525): photosynthetic root, two nodes, adult scale 1.28, brood 3, investment 0.31, margin 59 s; the nearest in that snapshot to the format-5 file it replaces (adult scale 1.25, investment 0.5), regenerated 2026-09-18 because format 6 refuses every stored genome | the genome half of the same pass |
| `knot-16-r41c-s3-1341.json` | round 41c seed 3's 3,000 s snapshot, creature 1341: two nodes and a terminal-only self-edge on the link, developing under the run's own limits to sixteen rigid parts folded into a ball, 103 self-overlapping pairs, lit area 1.13 m² over a silhouette of 0.31 m². Under D099's development rule it develops to **three** parts, the root and the two links its two ordinary edges reach. Format 6. SHA-256 `1a99d7f0fae6…` | logbook/0107, the round 41c read; `logbook/specs/silhouette-spec.md` §3; `DevelopmentTests` |
| `knot-9-r41c-s3-937.json` | the same seed's 2,000 s snapshot, creature 937: the nine-part rigid knot off the same terminal-only self-edge, lit area 0.96 m² over a silhouette of 0.48 m². **Two** parts under D099, the root and one link. Format 6. SHA-256 `08a6decdf3b7…` | as above |
| `knot-16-jointed-r41b-s1-1632.json` | round 41b seed 1's 5,000 s snapshot, creature 1632: the articulated knot, a twist joint on its link and a reflection on the root's edge, sixteen parts and a lit area of 2.21 m² over a silhouette of 0.46 m². **Three** parts under D099, the root and the reflection's bilateral pair. Format 6. SHA-256 `de7704563e9f…` | as above; the evidence that the knot was not the joint's doing |
| `spike-reference-config.json` | round 24's `config.json`, byte-identical to `r24-s1` through `r24-s4`'s; refused by every build since the `sense` group and kept as history | the reference world the shared-space spike had to restate by hand, because `EVOSIM_SPIKE_CONFIG` refuses a round-24 config that predates the `sense` group (`logbook/specs/shared-space-spike-report.md`, item 3; logbook/0064) |

Several rows say "no entry names the file". That is their state as found: they were inputs
to a reading whose conclusions were cited while the file itself never was. They are kept
so that the reading can be repeated.
