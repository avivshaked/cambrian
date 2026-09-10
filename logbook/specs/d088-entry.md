
### D088
**The third dimension — a newborn dispersed, a current that carries, a destroy that does not wait** · 2026-09-10

**Status:** ruled by the owner in conversation on 2026-09-10 after the theatre showed round
33 as two ribbons (logbook/0083): "lets start the fix please, because what you have now is
not a world"; "the world i want: creatures have full degrees of freedom and can be anywhere
within the confines of the world. currents move them up and down, left and right, in all
directions really. diffusion happens in all directions." Built in three parts the same
afternoon and smoked (logbook/0085). The values that are first cuts and not rulings are
listed at the end. Supersedes D077's clause 5 (a newborn beside its parent) and D037's and
D066's current as the default; both currents stay in the code as `CurrentMode Rolls` so that
every recorded run replays under its own build.

**Context.** A child was placed touching its parent (D077), the current returned every body
to where it found it (D037, written when horizontal position was a tile index; D066's rolls
kept the property), and no body ever swam, so every clade was a metre-wide column around its
founder's spot, and since the grid (D086) a column drained its own cells. The record carried
a mean depth and per-patch bins and no x or z, so thirty-three rounds were read without
seeing it. The theatre saw it in a minute.

**Ruled.**

1. **Dispersal at birth.** A newborn's horizontal position is drawn uniformly over a disc of
   `OffspringDispersalMetres` around its parent, uniform in area, never closer than the
   parent's radius plus the child's, wrapped at the seams, at the parent's depth, under the
   same free-spot test and attempt budget. 0 is D077's rule unchanged and the default, so
   every config in the record describes its world. `EVOSIM_OFFSPRING_DISPERSAL`; the header's
   `dispersal=`. Founders were already placed across the whole box.

2. **The instrument.** At every sample: occupied 1 m columns of the footprint
   (`occupiedColumns` of `totalColumns`), the same for bodies with a stomach
   (`occupiedColumnsAbsorptive`), and the circular spread of x and z (`xSpreadMetres`,
   `zSpreadMetres`); in the table `cols`, `cols abs`, `x sd`. A tiled world prints a dash.

3. **A current that carries.** `CurrentMode Transport`: a three-dimensional velocity field
   over the box, the curl of a vector potential of five Fourier modes with wavevectors
   (1,0), (0,1), (1,1), (2,1), (2,0) in units of 2π over the length and the width, each with
   a half-sine profile in depth whose mode number is derived from the horizontal wavenumbers
   by `k_y = sqrt(k_x² + k_z²)`, the one condition under which the three axes carry equal
   mean squares (in the campaign's box: 6, 24, 25, 27 and 12 half-waves, wavelengths 20 m
   down to 4.4 m; the eddies are squat because the box is 5 m wide and 60 m deep and the
   owner asked for all directions). Divergence-free by derivation; periodic in x and z; the
   vertical component exactly zero at the surface and the bed, so D050's rule and the floor
   are never fought; phases seeded from the run seed; each mode's phase drifting at
   ±(1 + j·φ) times the period `CurrentPeriodSeconds`, pairwise irrational, so that the
   pattern never repeats and a parcel is carried rather than returned. The knob
   `CurrentSpeedMetresPerSecond` is the field's root-mean-square speed over the box, in
   closed form; the fastest water is 2.45 times it and an analytic ceiling of 4.64 times it
   bounds the Courant check. The vent (D067) is unchanged in either mode.
   `EVOSIM_CURRENT_MODE`; the header reads `current 0.3 m/s transport over 6000 s`.

4. **Everything rides it.** A body's drag is against the water at its root, in three
   dimensions; a corpse drifts by the same sampler; the grid advects each cell by the water at
   its centre and its lower face, upwind on all three axes, wrapped in x and z, closed at the
   surface and the bed, conserving to the last joule. Where the fastest water would cross more
   than half a cell in a metabolic step the advection runs the step in `ceil(2·Courant)`
   substeps, each sampling the field at its own clock, and refuses above eight. At 0.3 m/s on
   1 m cells that is two substeps, 24,000 field samples per half second, about a quarter of
   the throughput in the smoke. The cell and vertex fields' patch-level movers still sample at
   the patch's centre in transport mode, a coarse reading kept because a grid does not use
   them.

5. **Destroy immediately in either mode.** The farm destroyed a dead body inside the step
   that killed it and the theatre deferred it to the end of a frame of tens of steps, which is
   why the replay parted at 200 s (0083). The body, the sea floor, the mesh cache's
   temporaries and the world's teardown destroy immediately now; the one caller that destroys
   from inside its own destruction keeps the deferred form and is named as the exception. A
   Play-mode identity check (`TheatreIdentityCheck.RunInPlayMode`, launched without `-quit`)
   drives the runner's own frame and read identical on all six samples of the smoke; the
   edit-mode check names itself as such in its verdict.

6. **The placer reserves the adult.** A child's spot is reserved at the radius of the body it
   will grow into, not the one it is born with (0082's suspect for seed 2's contact
   divergence), and the dispersal floor is the parent's radius plus the child's adult radius.
   In a crowded neighbourhood that refuses more births, counted as crowded stillbirths. The
   theatre's id pairing, which compared a root's height to a centre of mass's and failed on
   the first bred child, compares part counts and patches.

**First cuts, not rulings**, each a world rule for the owner (the list is logbook/0084's):
the dispersal radius (5 m; the disc spans the whole width); the current's speed (0.3 m/s kept
from the rolls, now an RMS rather than an amplitude) and period (6,000 s); the box's shape;
founders still reserving their birth radius; and `mean m/s`, which reads the water now and
no longer reads locomotion, with no relative-speed column yet in its place.

**Rejected.** Refusing the advection above a Courant number of a half (the first build's
rule): sound against a clamp, which runs a slower transport than the config says and says
nothing, and wrong against a substep, which runs what the config says and costs time; the
refusal was biting the campaign's own 0.3 m/s at 1 m cells. Balancing the axes by scaling a
component by hand: it would give up divergence-free, which is the whole reason the field
is a curl. A measured RMS scale on a lattice: twelve samples in depth cannot see
twenty-seven half-sines, and the closed form needs no lattice.
