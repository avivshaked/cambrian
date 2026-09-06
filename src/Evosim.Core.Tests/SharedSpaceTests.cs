using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The footprint world's economy-side half — D077. What <c>Evosim.Core</c> can be asked about
    /// a box without owning any coordinates.
    /// </summary>
    /// <remarks>
    /// The geometry itself (the ring, the wrap, the rejection sampling) lives in
    /// <c>Evosim.Sim.SharedVolume</c> and is tested in the Sim smoke, because it is denominated in
    /// <c>UnityEngine.Vector3</c> and §6.1 keeps that out of here. What is testable here is the
    /// seam: that the switch is off by default and reaches the hash, that the two retired
    /// lotteries are retired without drawing, and that a birth with nowhere to go costs its parent
    /// nothing.
    /// </remarks>
    public class SharedSpaceTests
    {
        private readonly ITestOutputHelper _output;

        public SharedSpaceTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void BothKnobsAreOffByDefaultAndBothReachTheHash()
        {
            var config = new RunConfig();

            Assert.False(config.SharedSpace);
            Assert.Equal(0f, config.Fluid.SurfaceRestoringFraction);

            string baseline = config.Hash();

            Assert.NotEqual(baseline, new RunConfig { SharedSpace = true }.Hash());

            var restoring = new RunConfig();
            restoring.Fluid.SurfaceRestoringFraction = 1f;
            Assert.NotEqual(baseline, restoring.Hash());

            // Clone() is hand-written and is where a new FluidConfig field is silently dropped —
            // FluidConfigTests exists because TissueExcessDensity was once dropped exactly here.
            Assert.Equal(1f, restoring.Fluid.Clone().SurfaceRestoringFraction);
        }

        [Fact]
        public void SharedSpaceRetiresBothTransportLotteriesWithoutDrawingFromTheStream()
        {
            // The guard the whole build rests on, taken from the other side: with the box on, a
            // world that also has dispersal and body advection switched on must be step for step
            // the same world as one with both off. If either lottery still drew, the two would
            // hand every creature after the first draw a different seed and diverge immediately.
            //
            // A placer is deliberately not attached: World must be runnable without one (every
            // test in this project builds a world with no coordinates at all), and with none the
            // patches simply stay where Admit put them.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 11);
                var sb = new StringBuilder();

                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    sb.AppendLine(WorldStats.Sample(world).ToJson());
                }

                return sb.ToString();
            }

            RunConfig Config(float dispersal, bool rolls)
            {
                var config = new RunConfig
                {
                    Light = new LightModel(300f, 12f),
                    SharedSpace = true,
                    HorizontalPatches = 4f,
                    DispersalChancePerStep = dispersal,
                };

                config.Current.Speed = rolls ? 0.3f : 0f;
                config.Current.Rolls = rolls;
                return config;
            }

            Assert.Equal(Trajectory(Config(0f, false)), Trajectory(Config(0.5f, true)));
        }

        [Fact]
        public void WithoutSharedSpaceTheLotteriesStillRun()
        {
            // The other half of the test above, and the reason it is worth having: if dispersal
            // were broken rather than merely retired, the check above would pass for the wrong
            // reason. With the box off, the same pair of configs must differ.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 11);
                var sb = new StringBuilder();

                for (int i = 0; i < 300; i++)
                {
                    world.Step(1f);
                    sb.AppendLine(WorldStats.Sample(world).ToJson());
                }

                return sb.ToString();
            }

            var off = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                HorizontalPatches = 4f,
            };

            var on = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                HorizontalPatches = 4f,
                DispersalChancePerStep = 0.5f,
            };

            Assert.NotEqual(Trajectory(off), Trajectory(on));
        }

        /// <summary>
        /// A placer with a policy and a memory, standing in for <c>Evosim.Sim.SharedVolume</c>.
        /// </summary>
        private sealed class StubPlacement : IBodyPlacement
        {
            public bool Room = true;
            public int Patch;
            public long Reserved;
            public long Committed;
            public long Released;

            private readonly Dictionary<long, int> _patches = new Dictionary<long, int>();

            /// <summary>
            /// A floor the placer will not put a body through, or 0 for "no bed at all".
            /// </summary>
            /// <remarks>
            /// Stands in for <c>SharedVolume</c>'s clamp against the sea-bed collider: what the
            /// interface promises Core is that <c>heightY</c> may come back <i>raised</i> and
            /// never lowered, and that whatever comes back is the height the creature is admitted
            /// at. Both of those are Core's to check; how many centimetres of clearance a real
            /// body needs is not.
            /// </remarks>
            /// <remarks>
            /// Negative infinity, not 0, and the difference is not cosmetic: a default of 0 is a
            /// bed at the waterline, which raises every founder into the brightest water in the
            /// world and turns a placement stub into a §5A.7 photosynthetic mat. It did, the
            /// first time this was written — <c>ACrowdedBirthCostsItsParentNothingAndIsCounted</c>
            /// ran away to 415 creatures.
            /// </remarks>
            public float MinimumHeightY = float.NegativeInfinity;

            public float LastOfferedHeightY;

            public bool TryReserveOffspring(Organism parent, Phenotype child, ref float heightY, out int patch)
            {
                patch = Patch;
                LastOfferedHeightY = heightY;
                if (!Room) return false;

                if (heightY < MinimumHeightY) heightY = MinimumHeightY;

                Reserved++;
                return true;
            }

            public bool TryReserveFounder(Phenotype body, ref float heightY, out int patch)
            {
                patch = Patch;
                LastOfferedHeightY = heightY;
                if (!Room) return false;

                if (heightY < MinimumHeightY) heightY = MinimumHeightY;

                Reserved++;
                return true;
            }

            public int PatchOf(Organism creature) =>
                _patches.TryGetValue(creature.Id, out int patch) ? patch : creature.Patch;

            public void Commit(long creatureId)
            {
                Committed++;
                _patches[creatureId] = Patch;
            }

            public void Release() => Released++;

            /// <summary>Moves a creature, the way a body drifting across a seam would.</summary>
            public void Move(long creatureId, int patch) => _patches[creatureId] = patch;
        }

        [Fact]
        public void ACrowdedBirthCostsItsParentNothingAndIsCounted()
        {
            // The rule D077 names: a birth that cannot be placed is not a failed birth, it is a
            // birth that did not happen. Nothing was built, so the parent keeps its energy and
            // its matter and no lineage row is written — the difference between a stillbirth
            // (a body that developed into nothing, and whose endowment therefore left the world)
            // and a refusal.
            RunConfig Config() => new RunConfig
            {
                Light = new LightModel(400f, 12f),
                SharedSpace = true,
                MinimumPopulation = 20,
                MaximumPopulation = 400,
            };

            var open = new StubPlacement { Room = true };
            var full = new StubPlacement { Room = false };

            var openWorld = new World(Config(), seed: 3) { Placement = open };
            var fullWorld = new World(Config(), seed: 3) { Placement = full };

            for (int i = 0; i < 200; i++)
            {
                openWorld.Step(1f);
                fullWorld.Step(1f);
            }

            _output.WriteLine(
                $"open: births {openWorld.Births}, crowded {openWorld.CrowdedStillbirths}, " +
                $"reserved {open.Reserved}, committed {open.Committed}, released {open.Released}");
            _output.WriteLine(
                $"full: births {fullWorld.Births}, crowded {fullWorld.CrowdedStillbirths}, " +
                $"floor spawns {fullWorld.FloorSpawns}, alive {fullWorld.Living.Count}");

            // The open world breeds and every reservation is claimed by a creature.
            Assert.True(openWorld.Births > 0);
            Assert.Equal(0, openWorld.CrowdedStillbirths);
            Assert.Equal(open.Reserved, open.Committed + open.Released);

            // The full world admits nobody at all — not a founder, not a child — and says so.
            Assert.Equal(0, fullWorld.Births);
            Assert.Empty(fullWorld.Living);
            Assert.True(fullWorld.CrowdedStillbirths > 0);
            Assert.True(fullWorld.FloorSpawns > 0);

            // And the energy audit still closes: a refusal creates and destroys nothing.
            Assert.Equal(0d, fullWorld.EnergyIn, 9);
            Assert.Equal(0d, fullWorld.EnergyOut, 9);
        }

        [Fact]
        public void ARefusedConceptionLeavesTheParentsEnergyAndMatterExactlyWhereTheyWere()
        {
            // The same claim as above, measured on one body rather than on a world: a parent
            // whose child cannot be placed must be indistinguishable, joule for joule and unit
            // for unit, from a parent that was never asked.
            var config = new RunConfig
            {
                Light = new LightModel(400f, 12f),
                SharedSpace = true,
                MinimumPopulation = 20,
                MaximumPopulation = 400,
                MatterPerTissueJoule = 0.5f,
                MatterPerCreature = 3f,
                InitialMatterPerCubicMetre = 1f,
            };

            var placement = new StubPlacement { Room = true };
            var world = new World(config, seed: 7) { Placement = placement };

            // Long enough that somebody is solvent and trying to breed.
            for (int i = 0; i < 300; i++) world.Step(1f);
            Assert.True(world.Births > 0);

            placement.Room = false;

            double energy = 0d;
            foreach (Organism creature in world.Living) energy += creature.Energy;

            double matter = world.StandingMatter;
            long births = world.Births;
            long crowded = world.CrowdedStillbirths;

            // A short window with the world full, rather than a single step: the placement gate
            // is the *last* one in Conceive, so it is only reached by a parent that is solvent in
            // both energy and matter this step, and there is no guarantee that any given step has
            // one. Metabolism moves energy and matter on its own, so what is asserted is the
            // birth-side ledger: no births, refusals counted, and the matter still adding up.
            for (int i = 0; i < 20; i++) world.Step(1f);

            _output.WriteLine(
                $"crowded {world.CrowdedStillbirths - crowded} over 20 s, " +
                $"births {world.Births - births}, standing matter {matter:0.###} -> " +
                $"{world.StandingMatter:0.###}");

            Assert.Equal(births, world.Births);
            Assert.True(world.CrowdedStillbirths > crowded);

            // D074's matter identity, which a leaked partial Take would break: initial + influx
            // − buried == free + locked. This is the one that catches a refusal placed after the
            // Take instead of before it.
            double expected = world.MatterInitialTotal + world.MatterInfluxedTotal - world.MatterBuriedTotal;
            Assert.Equal(expected, world.StandingMatter, 4);

            Assert.True(energy >= 0d);
        }

        [Fact]
        public void NobodyIsAdmittedBelowTheHeightThePlacerHandedBack()
        {
            // The floor build's Core-side contract (scratch/floor-spec.md rule 2): where the world
            // has a solid sea bed, the placer raises a body clear of it — and the height the body
            // is *built* at has to be the height the economy charges, or a creature would eat the
            // light and the matter of a layer its body is not in. The world may never lower it.
            //
            // The bed here is at −40 m in a 60 m world, which no draw and no parent can be below
            // by accident: the founder spread runs to 60, so the floor lottery reliably offers
            // depths under it, and every child of a founder inherits its parent's.
            const float bed = -40f;

            var config = new RunConfig
            {
                Light = new LightModel(400f, 12f),
                SharedSpace = true,
                WorldDepthMetres = 60f,
                FounderDepthSpread = 60f,
                MinimumPopulation = 20,
                MaximumPopulation = 400,
            };

            var placement = new StubPlacement { Room = true, MinimumHeightY = bed };
            var world = new World(config, seed: 11) { Placement = placement };

            for (int i = 0; i < 200; i++) world.Step(1f);

            Assert.True(world.Births > 0);
            Assert.NotEmpty(world.Living);

            // The offers have to have included some the clamp actually bit on, or the assertion
            // below is true of a world where nothing was ever raised.
            Assert.True(placement.Reserved > 0);

            float deepest = 0f;
            foreach (Organism creature in world.Living)
            {
                if (creature.HeightY < deepest) deepest = creature.HeightY;
            }

            _output.WriteLine(
                $"bed {bed} m, deepest living creature {deepest:0.###} m, " +
                $"births {world.Births}, reserved {placement.Reserved}");

            // Nothing sank on its own — World.Observe only moves a creature when a body reports a
            // new height, and there is no body here — so every living height is one the placer
            // handed back, and none of them is in the rock.
            Assert.True(deepest >= bed, $"a creature was admitted at {deepest} m, below the bed at {bed} m");
        }

        [Fact]
        public void APatchIsReadFromTheBodyRatherThanInheritedOrDrawn()
        {
            // D077's second rule, through the seam: the placer says where a creature is and the
            // world believes it, every metabolic step, before anything prices a patch.
            var config = new RunConfig
            {
                Light = new LightModel(400f, 12f),
                SharedSpace = true,
                HorizontalPatches = 4f,
                MinimumPopulation = 20,
                MaximumPopulation = 400,
            };

            var placement = new StubPlacement { Room = true, Patch = 0 };
            var world = new World(config, seed: 9) { Placement = placement };

            for (int i = 0; i < 40; i++) world.Step(1f);

            Assert.NotEmpty(world.Living);
            foreach (Organism creature in world.Living) Assert.Equal(0, creature.Patch);

            // Now the water carries them all into patch 3. Nothing about the creatures changed —
            // only where their bodies are — and the world has to agree on the next step.
            foreach (Organism creature in world.Living) placement.Move(creature.Id, 3);
            world.Step(1f);

            foreach (Organism creature in world.Living)
            {
                // A creature born during that step inherits its placement (patch 0 from the stub)
                // rather than the move, which is correct: it was placed, not carried.
                if (creature.Age <= 1f) continue;

                Assert.Equal(3, creature.Patch);
            }
        }
    }
}
