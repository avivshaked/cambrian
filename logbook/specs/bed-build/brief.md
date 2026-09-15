# Build brief: the bed with shape, the Core half (round 39; D092; `logbook/specs/bed-spec.md`)

Read `CLAUDE.md` (all of it; the Commands section says how the Core tests run, and the
gotchas on the grid, the tank and the transporter are the ground you build on), then
`logbook/specs/bed-spec.md` in full (the requirements; this brief builds items 1 to 9 and
12, the Core half, and nothing of 10, 11 or 13, which are Unity's), then
`logbook/specs/tank-spec.md`, `logbook/specs/streams-analytic-spec.md` and
`logbook/specs/transport-conserves-spec.md` for how the tank, the streams' closed form and
the potential-based transporter were built and validated, and DESIGN.md §5.2 and §5A for
the water and the fields.

## Rules

You work in the worktree `D:\Projects\experiments\evolution-simulator\scratch\wt-bed`
(branch `bed`, from main at `790b17a`). Every edit goes there and nowhere else: never the
main checkout at `D:\Projects\experiments\evolution-simulator\src` or `unity`, never the
session scratchpad, never TEMP. Temporary files, if any, go under
`D:\Projects\experiments\evolution-simulator\scratch\bed-build\`. No commit (the caller
commits). No Unity. Do not touch `runs/`, workers (`unity-w*`) or any running process.
Never sleep, poll or wait. Use absolute paths; the working directory resets between tool
calls. Run the Core tests from the worktree with
`pwsh -File D:\Projects\experiments\evolution-simulator\scratch\wt-bed\scripts\core-test.ps1 -Filter <Class>`
(fast, filtered; the whole default suite once at the end, which takes about a minute; never
`-All`). If the worktree's `core-test.ps1` resolves paths against the main tree, say so in
your report and run the filtered tests with Unity's dotnet directly
(`C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\DotNetSdk\dotnet.exe test`
on `scratch\wt-bed\src\Evosim.Core.Tests\Evosim.Core.Tests.csproj`).

House rules that bind the code: `Evosim.Core` has no `UnityEngine` and no dependencies;
every new tunable on `RunConfig` must reach `RunConfig.Hash()` and survive the JSON round
trip (`RunConfigTests` and `RunConfigJsonTests` are reflection-driven and will tell you);
loading refuses rather than defaults, so a new config group is a new refusal for every
older `config.json`, which is expected and must be said in the report; the deterministic
RNG is `Rng` (PCG), seeded from the run's seed; every per-step term must be bitwise
reproducible; XML doc comments in the existing voice, saying why and citing the spec.

## What to build

**1. The height map: `BedShape` (new, `src/Evosim.Core/Environment/BedShape.cs`).**
Constructed from the tank radius, the depth, the seed and three tunables (below). It gives
`Height(x, z)` (metres, the floor's offset from the flat bed at `-depth`, positive up),
`Gradient(x, z)` (∂h/∂x, ∂h/∂z), `Hessian(x, z)` (the three second derivatives), all
closed-form, and `FloorY(x, z) = -depth + Height(x, z)`. The shape is a sum of cosines in
three scale bands (spec item 1: about a third of the tank, a few metres, under a metre)
with random wavevectors and phases drawn from the seed, amplitudes falling with
wavenumber so the spectrum is red (spec: more relief at large scales than small; a power
law of about -2 on amplitude against wavenumber is a reasonable default, say why you pick
yours), plus the tilt (spec item 5a): a linear term `tilt · (x cos θ + z sin θ) / (2R)`
along a diameter whose direction θ is drawn from the seed. Then, once at construction:
subtract the disc mean numerically so the map is mean-zero over the disc (spec item 5;
the cosines are not mean-zero over a disc, the tilt is), and scale the relief amplitudes so
that the total range over the disc is `BedReliefMetres` and the steepest slope is at most
about 30 degrees (spec item 3; if the slope bound binds, the range comes out below the
dial and the report says so and by how much). Count the hollows (local minima deeper
than a quarter of the relief below their surroundings, say) so the smoke can print it
(spec item 4). Relief 0 and tilt 0 must give `Height ≡ 0` exactly, gradients and Hessian
exactly zero, so every recorded world replays to the bit (spec item 12).

Tunables on `RunConfig`, in a new group beside the tank's, each with the doc comment that
says what it is and cites D092 and the spec: `BedReliefMetres` (default 0: the flat
world; the round's default is picked from pictures later and is not your concern),
`BedTiltMetres` (default 0), `BedScaleMetres` (the largest band's wavelength, default a
third of the tank's diameter, computed from the area if 0). Refuse a negative or
non-finite value, a relief or tilt in a `Box` world (the bed is the tank's; say so in the
refusal), and a relief plus half the tilt that reaches the surface (the floor must stay
below `-1 m` everywhere; the beach is a later round). The environment variables
(`EVOSIM_BED_RELIEF`, `EVOSIM_BED_TILT`, `EVOSIM_BED_SCALE`) are parsed on the Unity side
by `EvolutionRun` and are not yours; name them in the doc comments.

**2. The grid masked below the floor (`GridField`).** The tank's mask (`_live`, built once
at construction from the circle) gains a second condition: a cell is live only when its
centre is above `FloorY(x, z)`. Everything that runs over live cells (seeding, totals,
refuge sums, per-patch sums, `LiveCellCount`, `LiveVolumeCubicMetres`, the transporter's
faces, mixing, settling) follows from the mask as it does for the glass, so read the
existing code before adding a branch anywhere. Settling keeps today's rule: matter that
reaches the lowest live cell of its column stays there (spec item 9). Add a
`FloorYAtColumn(ix, iz)` reading and a way for the report to sum the detritus held per
column against the floor's height (a method returning, over live columns, the detritus in
the lowest live cell and the floor height, so the read can say the hollows hold the
most). A `Box` world and a tank at relief 0 and tilt 0 must produce the identical mask
and identical arithmetic to today's (the digest test on the Unity side will check the
trajectory; your tests check the mask and the totals).

**3. The water follows the floor (`CurrentField`, the tank's streams only).** The flat
tank's streams are a vector potential `A(x, y, z, t)` (`PotentialAt`, `StreamsPotentialUnit`)
whose curl is the velocity, with no flux through the flat bed at `y = -depth`, the glass
and the surface. Build the sloped field as the **pullback of the potential through the
floor-following map** `Φ: (x, y, z) → (x, ỹ, z)` with `ỹ = y · depth / (depth + h(x, z))`
... precisely: choose the map so that `y = FloorY(x, z)` goes to `ỹ = -depth` and `y = 0`
stays `0`, linear in between (`ỹ = -depth · (y - FloorY) / (0 - FloorY)`; note
`0 - FloorY = depth - h`). The pulled-back potential is `A'_i(p) = Σ_j (∂Φ_j/∂x_i)(p) · A_j(Φ(p))`
(a 1-form transforms by the Jacobian's transpose), and the velocity is `curl A'` in real
coordinates, which by the pullback's naturality equals the pullback of the flat velocity
as a 2-form: `u'(p) = (1 / det J) · J · u(Φ(p))` with `J = ∂Φ/∂x`. Implement the velocity
by that closed form (it needs `h`, its gradient and the flat field's velocity at the mapped
point) and the potential by the 1-form rule (the transporter needs `PotentialAt`, and the
face fluxes it takes from the potential are then exact for the sloped field too). Both
properties the spec asks for follow by construction and you check them numerically: the
divergence of `u'` is zero (finite differences at random points inside the disc, of the
order of 1e-4 of the RMS per metre as D089's check 2 read on the flat field), the flux
through the sloped floor is zero (the normal velocity at `FloorY` of the order of 1e-7
of the RMS, as the flat bed and the glass read), the glass and the surface stay
flux-free, and at relief 0 and tilt 0 the field is bit-identical to today's (`Φ` is the
identity; make that path return the flat field's own arithmetic rather than the general
formula with a unit Jacobian, so the replay is exact and not merely close). Renormalise
the RMS as the flat field does (it is measured on a lattice at construction); the sloped
field's RMS must read the knob within the same 1%.

The **closed-form acceleration** (`StreamsAccelerationAt`, the material derivative
`∂u/∂t + (u·∇)u` used by the fluid acceleration force) extends through the map: `∂u'/∂t`
is the pullback of `∂u/∂t`; `(u'·∇)u'` needs the Jacobian of `u'`, which needs `J`, its
spatial derivative (the Hessian of `h`) and the flat field's velocity Jacobian at the
mapped point, which the analytic spec already provides. Write it out, and test it against
a central finite difference of `u'` along the path at random points to the tolerance the
streams-analytic spec used (0.15% of the RMS or better). Cost: count velocity samples per
call as the analytic spec did and report the ratio to the flat field's (the spec's ceiling
is two to three times).

**4. Tests (new classes beside `StreamsTests`, `TankTests`, `GridFieldTests`):**
`BedShapeTests` (mean zero over the disc; range equals the dial or the slope bound binds
and is reported; steepest slope under the bound; relief 0 and tilt 0 give exact zero;
determinism from the seed, two constructions identical; a different seed differs; hollows
counted; the tilt's shallow arc is where θ says; the refusals). `BedGridTests` (mask below
the floor: the cell count against the floor's volume within a cell layer; a column's
lowest live cell is above the floor; settling stops at it; totals conserve under transport
and mixing on the sloped grid; the constant-field check: 1 unit/m³ on the sloped grid with
the sloped streams at the campaign's mixing and current stays uniform to the transporter's
rounding over 600 s, as `transport-conserves-spec.md`'s test does on the flat one; relief 0
identical to the flat grid). `BedStreamsTests` (divergence, floor flux, glass flux,
surface flux, RMS, identity at relief 0, the acceleration against finite differences,
the sample count). Keep every test under a few seconds; a long one goes under the `Slow`
trait as the existing experiments do.

**5. What you do not build:** the Unity side (the mesh collider, the placer above the
floor, the `Diverged` guard, the header tokens, the env parsing, the report columns, the
theatre's drape); the smoke; the digest; any change to the box's fields or to the rolls.

## Report (your final message; no `.md` files anywhere)

What you built, file by file, with the design choices you made where the brief left them
to you (the spectrum's slope, the hollow rule, the slope-bound scaling, the map's exact
form) and why; the test counts (new and the whole default suite, green or not, with any
failure quoted); the measured numbers (divergence, floor flux, glass flux, RMS error,
acceleration error, samples per call flat against sloped, the hollow count and range on a
few seeds at a relief of 4 m and a tilt of 10 m on the 400 m² tank); every place the
brief's mathematics did not survive contact with the code and what you did instead;
and every refusal a user of an old config will now meet. Under 1,500 words.
