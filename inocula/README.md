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
| `s4-config-patched.json` | `r14c10-s4`'s own `config.json` with `feeding.exudationFraction` added at 0, so a build that has the tunable will read it | CLAUDE.md's gotcha that adding a tunable makes every older config unreadable; kept as the worked example of bringing one forward |
| `r28s4-stomach.json` | `r28-s4` snapshot 26,000, row 1778 | no entry names the file; it is the earlier of the two bodies the D082 ledger pass looked at |
| `r28s4-stomach2.json` | `r28-s4` snapshot 30,000, row 1736: absorptive root, one neuron, one input, developing to one absorptive part of 0.0048 m³ | `logbook/specs/price-ledger.md`, the ledger pass that priced a neuron for D082 |
| `growth-ledger-config.json` | a format-2 config carrying the `growth` group, `configHash 8395eb323c29be98`; no run on disk carries that hash, so it is a bench config assembled for the calculator rather than a world that ran | no entry names it; it is the config half of the growth build's ledger pass (D087, logbook/0081) |
| `growth-ledger-genome.json` | a format-5 genome, adult scale 1.25, brood 3, investment 0.5, photosynthetic root; it matches no snapshot row now on disk | the genome half of the same pass |
| `spike-reference-config.json` | round 24's `config.json`, byte-identical to `r24-s1` through `r24-s4`'s | the reference world the shared-space spike had to restate by hand, because `EVOSIM_SPIKE_CONFIG` refuses a round-24 config that predates the `sense` group (`logbook/specs/shared-space-spike-report.md`, item 3; logbook/0064) |

Several rows say "no entry names the file". That is their state as found: they were inputs
to a reading whose conclusions were cited while the file itself never was. They are kept
so that the reading can be repeated.
