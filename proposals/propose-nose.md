# Proposal: smell moves into a nose, a cell whose size and placement decide how well a body smells

*A draft for the owner, written at the coordinator's request on 2026-10-25. The owner proposed that smell become its own cell type, and later the same day asked what in the world should give off smell. Cell types and senses are world rules, so nothing here is built before the owner rules. Every claim about the code names its file. Anything I guessed is marked as inference.*

## The proposal in one paragraph

Today every part of every body smells. Each part reads the leaf smell and the carrion smell at its own centre, as an exact number, and a big part reads the same number as a small one. I propose a nose, a new cell type. When a world's table of types lists it, only nose parts smell, and the other parts read nothing on the smell channels. For size to matter, the reading cannot be exact. A nose counts what it catches, so it reads the field plus noise, and the noise falls with the square root of the nose's surface area. A big nose detects a fainter smell and gives a steadier reading. The nose reads at its tip, so where it sits on the body matters. Two mirrored noses read two values, and their difference points toward the source. A nose pays upkeep by its receptor area, so a large one costs energy. The noise comes from a hash of the world's seed, the body, the part and the step, so replays hold on the CPU and on the card. Nothing changes in a world whose table does not list the nose, so every recorded world replays. I recommend testing it in the nursery's patch tank against today's whole-body smell before any world carries it.

## What the code does today

These are the facts the rules below build on. The right-hand column says where each one is.

| What happens today | Where |
|---|---|
| Each part reads the leaf smell at its own centre, as the field's value squashed by x/(x+k). The direction form gives the field's exact gradient in the part's own frame, made unit length and scaled by the strength. | `src/Evosim.Dynamics/CreatureSenses.cs`, `SampleLeaf` |
| The carrion smell is read the same way, from a field of corpse joules. | `CreatureSenses.cs`; `src/Evosim.Core/Environment/ScentField.cs` |
| Both fields are sums of cones on a 2 m grid, read by trilinear interpolation, so across one cell the field is linear and its gradient is constant. Neither is carried by the current. | `ScentField.cs`; D167 rejected a plume carried by the current |
| A leaf emits 1 per square metre of photosynthetic lit area, from the body's own point and not from each part. | `src/Evosim.Core/Ecosystem/World.cs`, `GatherLeafSources`; `src/Evosim.Core/RunConfig.cs`, `LeafScentHalfScaleSquareMetres` |
| The chemical sense reads the edible density of `World.Nutrients` at the part, which is the marine snow that absorbers eat. It is squashed with k = 10 J/m³. | `CreatureSenses.cs`; `World.cs` ("Dead matter in the water, and what feeds on it"); `RunConfig.cs` |
| The islands' dose goes into `World.Matter`, the spent units that leaves fix. No sense reads that field. | `World.cs` (`PlanIslands` on `Matter`); `RunConfig.cs`, `MatterInflux.Islands` |
| The carrion sense is refused in a world where corpses are not objects. | `World.cs`, the check on `SenseScent` |
| The senses are sampled every physics step, inside the parallel region, from state that nothing writes during the step. | `CreatureSenses.cs`, class remarks |
| In the one brain, every part carries one input port per sense the run offers, and a link to a port it never uses has weight 0. The founder's wiring scale falls with the square root of the port count. | `src/Evosim.Core/Brain/CentralWiring.cs`; `src/Evosim.Core/Brain/CentralBrain.cs` |
| Thinking is free under the one brain. A neural price set above 0 is refused. | `World.cs`, the check on `BrainModel.Central` (D146) |
| Every type pays upkeep per cubic metre and embodies 500 J/m³ of tissue. Every type also has a bud weight that sets how often mutation draws it. | `src/Evosim.Core/Cells/CellType.cs` |
| A mouth sits at the end of a part away from where it hangs. | `src/Evosim.Core/Development/MouthPoint.cs` |
| In the solver, a mirrored part lands on top of its twin unless D228's mirrored-joint switch is on. | `logbook/specs/mirrored-anchor-finding.md`; `src/Evosim.Core/Development/DevelopmentLimits.cs`, `ReflectMirroredAnchors` |
| The card samples both smell grids in its own kernels, per part and per physics step, and the grids are uploaded every block. The carrion fill can run on the card. The leaf fill runs on the host. | `src/Evosim.Farm.Gpu/Kernels.F.g.cs`, `GpuWorld.cs`, `GpuScent.cs` |
| `Rng.SeedFor` is a pure 64-bit mixer with no state. | `src/Evosim.Core/Rng.cs` |

Two things follow from the table. A part's size changes nothing it smells, apart from making the body longer. And the direction form hands the brain a solved bearing, which D020 rejected and D167 built as one of two arms. The nose is a way to keep direction while bringing back D020's rule that the body does the geometry.

## The rules

1. **A new cell type, the nose.** It earns nothing. It pays upkeep by its receptor area (see the section on cost below). Its caps are attack 0, intake 0, protection 0 and toughness 1, so it is a soft organ. It carries no joint of its own, but it may hang from a link, which moves it.
2. **Smell is the nose's alone, when the table lists the nose.** In such a world, the leaf smell, the carrion smell and the chemical sense read zero on every part that is not a nose. The one brain gives smell ports to nose parts only. Under the old per-part brain, a neuron on any other part reads zero on those channels.
3. **The switch is the table.** The nose keeps every number it needs on its own type, as the anchoring cell does. A world that does not list it never sees it, and older configs stay readable. No new world tunable is needed for the nose itself.
4. **A nose reads at its tip.** It samples at the end away from where it hangs, the point `MouthPoint` already computes for a mouth.
5. **A nose reads the field plus noise that falls with its area.** The model and its constants are in the next section. The noise is drawn once per metabolic step (0.5 s, one "sniff") for each nose part and each smell, and held through that step's physics steps.
6. **The noise is a hash, not a draw from the world's stream.** It is a function of the world's seed, the body's id, the part's path id, the smell and the metabolic step's index. It is built from integers and exactly rounded arithmetic, so the double kernels on the card match the CPU bit for bit. It needs no checkpoint state.
7. **One set of ports per nose part.** Two noses are two sets of readings, which is what lets a pair give a bearing.

## Why a bigger nose must smell better, and the model that makes it so

If a reading is exact, a bigger nose gains nothing. One receptor reports what a thousand would. So size can only pay if sensing is imperfect, and the imperfection shrinks as receptors are added. I modelled a nose as a counter of captured molecules. The captures in one sniff come from a Poisson process whose rate is proportional to the smell plus a background, times the nose's area. The square-root law below is the standard result for counting. I have not fetched a source for it this session, so treat its biological reading as inference.

Let `c` be the field's raw value at the nose's tip (lit square metres for leaves, joules for carrion, both cone-weighted, as today). Let `A` be the nose's surface area in square metres, as its shape gives it. Let `A_ref` = 0.1 m² be a fixed reference area, about the surface of a 13 cm cube. The nose reads

```
c_hat  = max(0, c + sigma(c, A) * z)
sigma(c, A) = sigma_b * sqrt( (1 + c / c_b) * A_ref / A )
reading = c_hat / (c_hat + k)          # today's squash, unchanged
```

Here `z` has mean 0 and variance 1. Each smell has two constants, both kept on the nose type:

- `c_b`, the background. Below it the noise is fixed. Above it the noise grows with the signal, so a strong smell reads with a steady relative error.
- `sigma_b`, the noise of a reference nose in clean water.

A detection threshold falls out of the model with no separate rule. A smell is told apart from nothing at about two standard deviations, so the faintest smell a nose detects is about `2 sigma_b sqrt(A_ref / A)`. Four times the area halves it.

The table shows what that means for one leaf whose cone reaches 6 m, the reach round 59 carries (D174). I calibrated it so that a reference nose detects a median leaf at half the reach. Read the third column. Every fourfold increase in area halves the distance still left to the edge of the cone.

| Nose area | Faintest smell detected, as a share of the leaf's peak | Detection distance from one leaf at a 6 m reach |
|---|---|---|
| 0.025 m² (¼ of reference) | 1.0 | 0 m: it smells the leaf only when touching it |
| 0.1 m² (reference) | 0.5 | 3.0 m |
| 0.4 m² | 0.25 | 4.5 m |
| 1.6 m² | 0.125 | 5.25 m |

In a patch, ten leaves' cones overlap, so every nose does better than this.

**Calibration takes three steps, all in the nursery, before any arm.**

1. Read the median leaf's lit area from the patch tank's leaf pool (`rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz`). Set `sigma_b` so that a reference nose detects that leaf at 2σ at half the reach.
2. Set `c_b` to the median raw field a feeding body meets inside a patch. Read it from the leaf-scent recorder's field column (`src/Evosim.Farm/LeafScentRecorder.cs`), run on a re-step of the patch exam.
3. Re-step the patch arm's examined tops with a reference nose grafted on, at `sigma_b` ×0.5, ×1 and ×2. Keep the level at which the smell knockout's cost falls by about a third against the exact sense. A reference nose is then useful but imperfect, which leaves room for a bigger one to pay. The "third" is my judgement, not a measurement.

The carrion smell gets the same two constants, calibrated the same way against a corpse of median joules.

**Why the noise is held for a sniff.** If the noise were redrawn at every 0.01 s physics step, a brain that averages over time would cancel it, and the noise would then depend on the physics step's length. Holding it for 0.5 s makes the noise a property of the nose. A brain can still average over sniffs, which is the temporal comparison real animals use. The project's literature pass records lobster receptors integrating over 200 ms and sniffing at 4 Hz (`logbook/specs/leaf-scent-lit/pass2.md`, C33).

**How the noise replays.** One 64-bit output of the existing mixer, keyed on the seed, the body, the part, the smell and the step, gives four 16-bit uniforms. Then `z = (u1 + u2 + u3 + u4 - 2) * sqrt(3)`, which has variance 1 and is close to a bell curve. The obvious alternative is the Box–Muller transform, which needs a logarithm and a cosine. I believe the card's double logarithm and cosine are not rounded as .NET's are, which would break the double kernels' bit match. That is inference, and the sum of uniforms avoids the question. The world's own random stream cannot be used, because the senses run in the parallel region and a shared draw there would tie the result to thread order. The body's id has to be handed to the senses, as the reserve already is. That is the one change in plumbing, and how large it is I infer rather than know.

## Direction comes from where noses sit and how they face

**Placement.** A nose reads at its tip, so a nose on a stalk ahead of the body samples ahead of the body. Across one 2 m cell the field is linear, so moving the sampling point by 20 cm shifts the reading by the gradient times 20 cm. The effect is small but real (inference).

**A single nose in the direction form.** If the worlds keep the direction form, the gradient has to be noisy too, or a tiny nose would still hand over a perfect bearing. I derived a rule from treating the nose's two halves as two sensors half a nose-length apart, each with half the area. That gives a gradient noise of `4 sigma(c, A) / L` on each axis, where `L` is the nose's length along its outward axis. The reported direction is the noisy gradient made unit length and scaled by the strength reading. A long nose points well, and a stubby one points badly. The derivation is mine (inference).

**Paired noses.** A genome that mirrors a nose grows two of them. Each has its own ports and its own independent noise. A brain that weighs one with +w and the other with −w reads their difference. The difference's signal is the gradient times the span `s` between the tips, and its noise is √2 σ. So the signal-to-noise ratio is `|grad c| * s / (sqrt(2) * sigma)`, and both span and area pay. Sharks are the owner's example, with nostrils set wide apart. I have not verified the shark biology this session, so it stands as inference. The project's literature pass does record that comparison across the body needs a span wide compared with the plume's structure, and that larger animals gain by spreading their receptors wider (`pass2.md`, C28 and C40). Our field is a set of smooth cones and not a turbulent plume, so in it a wider span always helps (inference).

**Pairs need D228's fix.** In today's solver a mirrored twin lands on its original (`logbook/specs/mirrored-anchor-finding.md`). Two noses in one place read one value with two noises, and they give no bearing at all. Every nose test with pairs therefore runs with D228's mirrored-joint switch on.

**Facing.** The owner asked about a cone of acceptance and about sampling upstream. Neither smell field is carried by the current, so "upstream" leads nowhere in this world. A body that swims against the flow on a cue finds nothing, even though real animals most often find a source that way (`pass2.md`, Q3). Facing can still mean something through the nose's intake. A nose facing into the water it moves through samples more water per sniff. I would model that as an effective area `A_eff = A * (1 + max(0, -v . n) / v_s)`, where `v` is the water's velocity relative to the part (the flow sense already computes it), `n` is the nose's outward axis and `v_s` = 0.3 m/s (the flow sense's full scale). A forward-facing nose on a swimming body then reads with less noise. I recommend building it switched off on the nose type and testing it as a second step. Sniffing that moves water over the receptors is real in lobsters (`pass2.md`, C33). How far it carries over is inference.

## What in the world gives off smell

The owner asked on 2026-10-25 what should give off smell. Today two things do, living leaves (D167) and carrion.

**Carrion stays, and the nose reads it.** Under D232's switch a kill swallows the whole body and leaves no remains. A body that dies any other way still leaves remains: age, starvation, falling under the mass floor and divergence all do, and so do a module's remains (D232; D170's reminder; `HANDOFF.md` on the scent restore). So the carrion field still has sources. It may have fewer and shorter-lived ones under D219–D221, where remains are edible for 60 s. Whether a remains still smells after its window depends on whether it stays in the world's corpse list. I have not checked that, and the build has to read it. Under the nose rule, carrion becomes one of the nose's receptors.

**Animals could give off a smell of their own.** A third field, built like the leaf field, would emit from each living body by its tissue that is not photosynthetic: biter, link, absorptive, neural, nose and structural. A hunter would find prey by it, and prey would sense hunters. One field cannot tell a hunter from a grazer. A body reads only how much animal is near, which tracks size. A fourth field emitting by biting area would tell them apart, at twice the cost. I recommend one field first. Four costs come with it.

- *The fill.* The leaf field's cone at 10 m on 2 m cells covers about 520 cells (`ScentField.cs`, remarks). A few thousand animals is a few million cell visits a step. The carrion fill, at 42,394 corpses and 56 million visits, was the largest phase of the world step in W0 (`HANDOFF.md`). So the animal fill is about a tenth of that, or less (inference from the counts).
- *Self-smell.* A body sits at the peak of its own cone. The reading has to subtract the body's own gridded contribution at the nose, or every animal smells mostly itself. The same holds today for a swimmer with leaf tissue reading the leaf field (inference from `GatherLeafSources`, which takes every photosynthetic body).
- *State.* The leaf field's sources are carried in the checkpoint (`World.cs`, `LeafScent` remarks). An animal field needs the same, which bumps the checkpoint format.
- *Inputs.* One port per nose in the strength form, three in the direction form.

This is the sensing half of the lateral line in `proposals/propose-new-cell-types.md`, which D228 held until grazers live on living leaves. I recommend that animal smell replace the lateral-line candidate and wait for the same condition.

**Dissolved matter is already smellable as snow, and that is enough for now.** The chemical sense (D020) reads the edible density of marine snow at the part (`CreatureSenses.cs`, `World.cs`). So absorbers can already find rich water wherever a run opens the sense, and the nursery's base config opens it. It is a taste at the point, with no cone. It reads only the water the part is in, at the snow grid's resolution. The islands' dose is spent matter, the leaves' food, and nothing smells it. A smell of spent matter would serve only a swimming body that carries leaf tissue and steers toward fertile water. Leaves do not steer. I recommend not building it now. Under the nose rule the chemical sense moves onto the nose with the other smells, so that one organ carries every distal chemical sense. Leaving it on every part as a "taste" is the alternative, and it is decision 7.

**Separate receptor channels or one pooled smell.** There are three ways to do it.

- *(a) One pooled smell*, the sum of every field. It costs one port per nose. But a body could not tell a leaf from a corpse from a hunter, which is why D167 kept the leaf smell apart from carrion. Animals tell living food from dead by smell (`pass2.md`, item 2).
- *(b) Separate channels, and every nose carries every channel the world opens.* That is one port per smell per nose in the strength form. One upkeep covers them all, and the genome format does not change.
- *(c) Specialised noses.* A leaf nose and a carrion nose could be separate cell types, or a node trait could choose the receptors. A body would then pay for each receptor it carries. Separate types add entries to the table and colours to the theatre. A trait bumps the genome format.

I recommend (b) now and (c) only if a world shows a reason for specialists.

## What a nose costs

**Upkeep and build.** In the nursery's config (`rounds/nursery/configs/e6bdb9c74cb48742.json`), the upkeeps in W/m³ are structural 1, link 2.5, buoyancy 2.5, photosynthetic 3, absorptive 4, neural 5 and consumer 2 (round 62's table has the consumer at 6). Every type embodies 500 J/m³. A nose priced only by volume would cost almost nothing. A 10 × 10 × 20 cm nose is 0.002 m³, about 0.008 W at 4 W/m³. A swimmer in the tank burns on the order of 1 W standing, which I infer from D170's net of about −1,600 J per 1,800 s episode. So size would be nearly free, and the noise model's reward would have no price beside it. I propose that the nose override its upkeep, as the link cell does, so that it pays mostly by receptor area:

```
upkeep (W) = u_V * volume + u_A * surface area
u_V = 3 W/m3   (the base class refuses 0)
u_A = 0.5 W/m2 (a reference nose of 0.1 m2 then costs about 0.05 W, some 5% of a swimmer's standing cost)
build = 500 J/m3, as every type
```

`u_A` is a placeholder. It is calibrated in the nursery so that the bred noses settle inside the size limits and not against a bound. A nose also has costs the economy does not bill: a part slot under the part limit, drag and mass, and a soft target for a bite.

**Its place in the brain.** In the patch tank's pool, a rigid part carries 12 ports: up and depth, the chemical sense, flow ×3, contact, damage, carrion, and the leaf direction ×3. Five of the 12 are smell. A six-part body therefore carries 30 smell ports today. With one nose it carries 5, and with a mirrored pair 10. In the strength form with four smells (leaf, carrion, animal, snow), each nose carries 4. Ports cost no energy, since thinking is free under the one brain (D146). They change the search. The founder's wiring scale depends on the port count, and a link mutation draws from the ports, so a body with no nose cannot grow a smell link at all (inference from `CentralWiring.cs`). Pooling all noses into one set of ports would make two noses one bigger nose and throw away the bearing, so I recommend one set per nose (rule 7).

## What it does to worlds, the nursery and the machine

**Recorded worlds replay.** A world whose table lacks the nose builds the same ports, reads the same senses and draws no noise. Adding the nose to a table makes a new realisation even before any body grows one, because mutation draws a part's type from the table (`propose-new-cell-types.md`, item 4). Its bud weight can be set to 0 for a control.

**The nursery's smell-bred swimmers smell nothing under the rule.** The 400 m² tank's tops (`inocula/r59-direction-tops/`) and the patch arm's genomes have no nose. To carry them over, a converter would graft a nose node onto the part whose smell ports carry the most weight, and rewrite those links to the nose's ports. A port's id is a hash of its part's path, so the links have to be rewritten and not just copied. I have not read which part carries their smell links. I would guess the root, but that is a guess. Round 59's worlds keep their whole-body smell unless the owner rules otherwise.

**Config, format and fixtures.** The nose alone needs no new tunable, no genome bump and no checkpoint bump, since the noise is stateless. An animal field needs new tunables, which refuse every older config, and a checkpoint bump, and the three recorded fixtures then need re-recording (`src/Evosim.Core/CLAUDE.md`). A smell of spent matter would need a new tunable and a new sensor channel appended at the enum's end. Enums serialise by name, so a genome is not bumped by that (inference from the conventions in the root `CLAUDE.md`).

**The card.** Smell is sampled on the card, in the generated kernels, for each part at each physics step (`Kernels.F.g.cs`). So the nose needs a kernel change: a nose flag and area for each part, its tip offset, the noise hash and the gate. The gate removes reads from every part that is not a nose, and the hash costs a few integer multiplies per nose per sniff. My guess is that the net cost is close to zero or slightly negative. The kernel change reruns the card's identity checks. It could share a round of them with D228's and D234's solver changes, if the timing allows. An animal field adds one grid upload per block and a host fill like the leaf field's. The nursery runs on the CPU (`HANDOFF.md`, the nursery on the card), so the nursery test needs no card at all.

The table gives agent time for each piece. It does not include machine time or the owner's review.

| Piece | Agent time |
|---|---|
| Nose type, the rule, gated ports, tip sampling, noise, tests (Core and Dynamics) | about 1 day |
| Card kernels and identity checks | about 1 day, plus a short card sitting |
| Nursery recipe, graft converter, exam controls | half a day to a day |
| Theatre colour and drawing | half a day |
| Animal smell field, if ruled | 1 to 1.5 days |
| Spent-matter smell, if ruled | half a day |

The nose alone is about 2.5 to 3.5 days of agent time, and the card part can wait until the nursery has answered. Each nursery arm takes machine time on the scale of the patch arm, whose episodes cost 3.05 times one in the 400 m² tank (`HANDOFF.md`). I did not look up the patch arm's wall time.

## A nursery test: nose against whole-body smell in the patch tank

The tank is D184's patch tank, because there the smell carries nearly all the eating: 83.4 J an episode against 9.4 J with it knocked out (`HANDOFF.md`; logbook/0146). The founders are the patch arm's pool. In the nose arms each founder carries a grafted nose of reference size at the front tip, and half the founders carry a mirrored pair. Bodies evolve their kinds, so a lineage can resize a nose, add one or lose it. D228's mirrored-joint switch is on in every arm.

1. **N0, today's smell**, as the patch recipe on the new build. Its generation 0 must first replay the record byte for byte.
2. **N1, a nose with the direction form**, noise on.
3. **N2, a nose with the strength form**, noise on. It is the faithful arm, since any bearing must come from pairs, from placement or from time.
4. **N3, optional: a nose with the direction form and the noise off.** It tests whether size pays without noise. I expect it not to.

The exam adds three controls to own, brainless and knockout. The first removes the nose part. The second knocks out one nose of a pair. The third re-steps each top with its nose scaled ×0.5 and ×2, which measures directly whether size pays.

**What would justify the rule in worlds.** Three results, together.

- A nose arm eats within two standard errors of N0 or better, after the nose's upkeep, on the energy return.
- In the size swap, the ×2 nose eats more than the ×0.5 nose by at least two standard errors. Across generations the nose area moves away from the founder's in the same direction.
- The knockout still costs at least two standard errors, the bar of D172's exam.

If the size swap shows nothing, the noise is too weak. If the nose arms eat far less and the noses shrink away, the noise is too strong or the upkeep too dear. Either way the constants are recalibrated once, and a second failure goes to the owner. A pair that beats a single nose of the same total area in N2 would show that bearings come from the body's own geometry, as D020 intended.

## How it looks in the theatre

A nose could be drawn like a sea slug's rhinophore: a tapered rod with rings of folds near its tip, in a colour of its own in the theatre's palette (`unity/Assets/Theatre/TheatrePalette.cs`). D233's tapered shape would give the rod its taper. The theatre sits outside the simulation's hash, so the drawing changes no recording. One caution belongs here. Rhinophores are often described as smell organs, but I have not checked that this session (inference). The project's literature pass records that in *Aplysia juliana* the rhinophores sensed water currents, while the oral tentacles sensed the food cue (`pass2.md`, C2, Frings & Frings 1965). So the look is a picture of a sensory organ, and a film should not caption it as how sea slugs smell.

## The decisions for the owner

1. **The nose as a cell type**, whose presence in a world's table makes smell the nose's alone, with no new world tunable. I recommend yes. The alternative is a smell trait any part may carry, which avoids changing bodies but loses the organ and its placement.
2. **The sensing model.** The reading is the field plus noise that falls with the square root of the nose's area. It has two constants per smell, kept on the nose type, and is drawn once per 0.5 s sniff from a hash so that replays hold. I recommend yes, calibrated in the nursery as described.
3. **The price.** The nose pays upkeep mostly by receptor area (about 0.5 W/m² to start, 3 W/m³ by volume) and builds at 500 J/m³ like every type. I recommend yes, because a nose priced by volume alone would make size free.
4. **Placement and facing.** The nose reads at its tip. Intake from water flowing into its face is built switched off and tested second. I recommend both.
5. **The direction form under the nose.** Keep both forms, with the direction form's gradient made noisier for a shorter nose, and let the nursery's N1 and N2 decide which the worlds carry. Pair tests need D228's mirrored fix. I recommend running N2 even if the worlds keep the direction form, since it is the faithful sense.
6. **Receptor channels.** Separate channels, and every nose carries every smell the world opens, with one upkeep. I recommend this over a pooled smell (which loses the leaf from the corpse) and over specialised noses (more types, or a genome bump, with no evidence yet that they pay).
7. **The chemical sense (snow) moves onto the nose** under the rule, so that one organ carries every distal chemical sense. I recommend yes. A smell of the spent matter the islands are fed with is not built for now.
8. **Animal smell.** One field emitting by each body's non-photosynthetic tissue, with each body's own smell subtracted. It replaces the held lateral line and is built only once grazers live on living leaves. I recommend designing it now and holding the build.
9. **Carrion smell stays** as a nose receptor, given that deaths other than kills still leave remains under D232. I recommend yes.
10. **The nursery test as designed**, with its three conditions for carrying the nose into worlds, and the nose built on the CPU first with the card's kernels after the nursery answers. I recommend yes.
11. **The theatre draws the nose as a rhinophore**, with no caption claiming that sea slugs smell with them. I recommend yes.
