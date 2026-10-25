# The square's snow at speed: the ocean's phases tabled, and its transport on the card

Built on 2026-10-19 by the speed agent, on the coordinator's order. The owner had stopped round 62's five collapse
screens (`r62dx`) at 0.32 times real time and set about twice real time a world as the aim. Option A is on `r62-hoist`
(`d7981dd6`, from `r62-card` at `6590b026`). Option B is on `r62-gpubox` (`ebef49b1`, from `r62-hoist`). Round 62's
build is `r62-build`: `r62-gpubox` with `r62-d207` merged in. Neither option moves a number. Each is proven against the
path it replaces, bit for bit, below.

## Where the time went

A phase probe resumed `r62dx-ctl` alone on the card from its 2,710 s checkpoint for 100 s on `r62-card`. At about
1,370 bodies it spent 1,109 ms of wall a simulated second. The snow's advection took 843 of them (76%), physics
195, and every other world phase under 17 each.

The snow is a 1 m grid over the 300 m square, 25 m deep: 2.25 million cells and about 6.6 million cell edges. Each
transport pass samples the current's potential at every edge. In a tank the streams' terms were already hoisted
per column and per depth (D105). In the box the ocean took the direct path. Each edge evaluated the ocean's 25
vectors with a fresh `Math.Cos` and `Math.Sin` of `χ = k_x·x + k_z·z`, and neither the height nor the clock enters
`χ`.

## A: the phases tabled per edge column

`GridField` builds, once per current and per build of the ocean's vectors, `cos χ` and `sin χ` for every vector
at every column of the three edge lattices. Over the square that is about 110 MB a world. The box's edge loops read
the table through `CurrentField.PotentialAt(x, y, z, seconds, column, phases, at)`. That entry applies the bed's
map, the shore's fade, the scale and the coral's fade as the direct path does. It sums the same products in the same
order.

- The switch `GridField.PrecomputeOceanPhases` is on by default, and `OceanPhasePasses` counts the passes that read
  the table.
- `OceanPhaseHoistTests` holds the tabled edges to the direct path's bits three ways: on a shaped floor, on a floor
  with a shore, and in the coral square. It checks every face flux at three clocks and every cell after four
  advection steps.

## B: the transport on the card

The card's transport device (`GpuTransport`, `EVOSIM_GPU_TRANSPORT`, `-Transport`) carried only a tank's grid
under the streams. `GridField.PlanOceanTransport` now plans the box's ocean for it. The plan needs three things:
the ocean on a shaped floor reading its phase tables, a live mask, and no shore. The world refuses a shore in a box,
and no device test has carried one. The plan carries:

- The phase table from A.
- `sin θ` at each edge's mapped height (`CurrentField.OceanMappedSine`), the one transcendental the three layers'
  shapes need. The card rebuilds the shapes from it by `OceanLayersAt`'s own recurrence and never calls its own sine.
- The bed's columns, and the coral's fade at each edge. The coral is immutable, so the CPU's live fade and the
  table agree.
- A 150-term instant of the ocean's cosine and sine coefficients (`CopyOceanInstant`).

`OceanEdgeKernel` transcribes the box's edge loops with every product an explicit `mul.rn`. An edge on a seam is
computed at the edge it copies, which is that edge's own arithmetic. Assembly, the outflow bound and the fluxes wrap
at the seams (`GridParams.Wrap`); the fused apply already did.

- `GpuTransportBoxTests` holds the device on ILGPU's CPU accelerator to the grid's bits over six steps of a 60 m
  square, with and without coral, with snow placed across the seams.
- The Farm's GPU tests pass 92 of 92 off the card.

## The proof on the card, and the pace

All runs are `WORLD_PHASE_PROBE` builds on the card at group 32, 14 threads on logical 16-29, with nothing else
running beside them. Resumes across builds are cousins by hash, so the claim is the rows. Each comparison is
`scripts/reads/gpu-accept.py`.

| Run | Snow advection, ms per simulated second | Whole step, ms | Times real time |
|---|---|---|---|
| `r62dx-probe`, `r62-card`, alone | 843 | 1,109 | 0.90 |
| `r62dx-hprobe`, A, alone | 237 | 486 | 2.1 |
| `r62dx-gprobe`, B with `-Transport`, alone | 25 | 211 | 4.7 |

- **Alone.** `r62dx-hprobe` and `r62dx-gprobe` each match `r62dx-probe`: every stats sample, all 100 digest rows,
  and the lineage byte for byte.
- **Five-up, A.** The five `r62dx` worlds were resumed together from their last checkpoints for 200 s
  (`r62dx-h5-*`). Each ran 0.99 to 1.16 times real time, and the near-empty `still` world 2.70. Over their last
  200 s on `r62-card` the same worlds had run 0.33 to 0.35.
- **Five-up, B with `-Transport`.** `ctl` ran 2.53, `slow` 2.42 at 2,315 bodies, `dose2` 2.40, `d206off` 3.09,
  and `still` 3.77 (`r62dx-g5-*`). Each world matches A's five-up of the same checkpoint on all 20 samples, all
  200 digest rows, and its lineage.

With the snow on the card, physics is the larger half of a step. Five-up it takes 196 to 213 ms a simulated second
per world, and the rest of the world 166 to 188. My reading is that a world growing well past 2,300 bodies will
slow from there.

## Running it

- A needs nothing; it is on by default in every box ocean on a shaped floor.
- B needs `-Transport` on `card-run.ps1` or `card-group.ps1`, and the run's `run.json` records `transport: card`.
