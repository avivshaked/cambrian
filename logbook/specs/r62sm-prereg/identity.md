# The structural mutation build's card identity sitting

*2026-10-27, the speed agent, on the coordinator's card go, after assay-d6's clear at 11:03 with HOLD in place and the
coordinator's heat watch running. `rounds/identity-struct.ps1` from `scratch/wt-struct` at `e492c5fb` (branch `struct`,
whose source is `37341da0`'s), with that worktree as the working directory and its Release farm built at 11:01. One card
group, `r62sid`, of two worlds founded on seed 6201 for 600 s, on logical 22 to 31 at 10 threads, from 11:04 to about
11:08. Runs under `scratch/struct-build/identity/`.*

## The verdict: it passes

| Check | Result |
|---|---|
| `r62sid-t60-6201` against growth group 1's `r62gr-t60-6201`, by `scripts/reads/gpu-accept.py` | exit 0 (`identity/accept-t60.txt`) |
| stats | identical on all 60 shared samples to 600 s, wall clocks left out |
| digest | identical on all 601 shared rows to step 60,000 |
| lineage | byte-equal on the sitting's 1,427 lines |
| config hash, bindings unset | `6dbb0315e486251f`, growth group 1's |
| `r62sid-s10-6201`'s `config.json` | the seven chances at a tenth of today's: addNode 0.004, addEdge 0.006, removeEdge 0.005, addNeuron 0.005, removeNeuron 0.004, recursiveLimit 0.003, jointType 0.004 |
| `r62sid-s10-6201` steps | to 600 s, 1,324 alive, ended |

The positions files were not compared ("not in both runs"), as in growth group 1's sitting. The stats, the digest and
the lineage cover the world.

## The build the screen carries

Core `361aa924` and dynamics `29144991`, unchanged from the growth build. Farm `ae99d3c2`, which is new: the seven
bindings in `src/Evosim.Farm/EnvBinding.cs`. The screen's manifests must read these three, and the launch is checked
against them.
