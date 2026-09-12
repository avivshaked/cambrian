using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The box, pinned to the bit against the tank — <c>logbook/specs/tank-spec.md</c>,
    /// <c>fable-propose-aquarium.md</c> ruling 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The tank's whole contract is that a box is unchanged by it.</b> Every branch the build
    /// added is gated on <see cref="WorldShape.Tank"/>, and a run at the default shape has to be
    /// the world it always was — not nearly, but to the last bit, because PhysX replays bit for
    /// bit on this machine and every per-step change is a butterfly (CLAUDE.md). A gate that
    /// leaks by one ulp does not read as a bug: it reads as a different realisation of the same
    /// seed, twenty per cent of a population and four metres of depth away, and the digest is the
    /// only instrument that would ever catch it.
    /// </para>
    /// <para>
    /// <b>So the numbers below are recorded rather than derived.</b> Each was measured on the
    /// build immediately before the tank landed (commit <c>9be3b80</c>, the box branch's Core)
    /// and is asserted exactly. Nothing here asks whether the arithmetic is right — the
    /// field's own tests do that; it asks whether it is the same arithmetic. A change that moves
    /// one of these is either a Box-path regression or a deliberate new realisation of every
    /// recorded world, and the second kind is a decision, not an edit.
    /// </para>
    /// <para>
    /// <b>Two of the four were re-recorded on 2026-09-12, and that is the second kind.</b>
    /// <c>logbook/specs/transport-conserves-spec.md</c> replaced the grid's centre-sampled
    /// transport with face fluxes assembled from the current's vector potential, because the old
    /// one turned a uniform concentration into 0.36 to 2.45 of itself in 600 s. The water itself
    /// is untouched — <see cref="TheTransportFieldIsUnchanged"/> still holds its six samples and
    /// its bound to the bit, and so does <see cref="ThePlacementStreamIsUnchanged"/> — but what
    /// the grid does with that water is different arithmetic on purpose, so
    /// <see cref="TheBoxGridIsUnchanged"/> and
    /// <see cref="ABoxWorldRunsTheSameFourHundredSteps"/> carry new numbers and every world that
    /// ran on <see cref="CurrentMode.Transport"/> — rounds 34 to 37 — is a new realisation of its
    /// seed. Worlds on <see cref="CurrentMode.Rolls"/>, which is every round through 33, replay:
    /// the rolls have no potential and keep the scheme they always had.
    /// </para>
    /// <para>
    /// <b>Why these four.</b> They are the four places the tank build reached into and the four
    /// the box path runs through: the periodic transport field's construction and its bound,
    /// which must keep their 28x24x5 lattice and closed-form RMS while the tank's streams get two
    /// lattices of their own; the grid's cells, seeding, stirring and advection, which gained a mask that must
    /// stay null in a box; the placement RNG stream, which is what the Sim-side placer draws a
    /// founder's spot from and must advance by the same draws in the same order; and a whole
    /// world stepped four hundred times, which is every one of them at once plus the economy.
    /// </para>
    /// <para>
    /// <b>What this cannot reach.</b> The placer itself is <c>Evosim.Sim</c> and needs
    /// <c>UnityEngine</c>, so Core can pin the stream it consumes but not the positions it makes
    /// of them; the same goes for <c>EffectorDriver</c>'s limiter gate and <c>Ecosystem</c>'s
    /// contact split. Those stay a reading of a run's digest against
    /// <c>runs/boxdig-old</c> — and a digest comparison is only evidence when both runs carry the
    /// same config, which is what the header, not the launch command, says (CLAUDE.md).
    /// </para>
    /// </remarks>
    public class BoxPathTests
    {
        /// <summary>The campaign's water: D088's transport field at the round's own settings.</summary>
        private static CurrentField Water()
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = 0.1f,
                PeriodSeconds = 6000f,
                AdvectFields = true,
                CellMetres = 30f,
                Rolls = true,
                RollBlinkSeconds = 3000f,
                VentDepthMetres = 60f,
            };

            field.SetBox(5f, 4, 60f, 987654321UL);
            return field;
        }

        /// <summary>
        /// The transport field's bound and six sampled velocities, unchanged to the bit.
        /// </summary>
        /// <remarks>
        /// <see cref="CurrentField.MaximumTransportSpeed"/> is what
        /// <see cref="GridField.Advect"/> substeps against, so it decides how many times a step
        /// stirs the water as well as how fast the fastest parcel goes; the six samples are the
        /// field itself, at six places and six times chosen only to be unremarkable. The tank
        /// added a second branch to both — the streams' measured ceiling and
        /// <c>StreamsAt</c> — and this is the assertion that the first branch still answers.
        /// </remarks>
        [Fact]
        public void TheTransportFieldIsUnchanged()
        {
            CurrentField field = Water();

            Assert.Equal(WorldShape.Box, field.Shape);
            Assert.Equal(0f, field.TankRadiusMetres);
            Assert.Equal(20f, field.LengthMetres);
            Assert.Equal(5f, field.WidthMetres);

            Assert.Equal(0.46419993f, field.MaximumTransportSpeed);

            var expected = new[]
            {
                new[] { 0.080657534f, 0.017939966f, -0.007291254f },
                new[] { -0.025986906f, -0.03132243f, -0.021075508f },
                new[] { 0.0022616403f, -0.07954323f, -0.05666309f },
                new[] { -0.021305852f, 0.07013317f, 0.012379267f },
                new[] { 0.040932808f, 0.016995996f, 0.09968913f },
                new[] { -0.013064415f, 0.0513805f, 0.08257497f },
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Float3 v = field.VelocityAt(
                    1.3f + 3f * i, -2.5f - 7f * i, 0.7f + 0.9f * i, 37.5 * i);

                Assert.Equal(expected[i][0], v.X);
                Assert.Equal(expected[i][1], v.Y);
                Assert.Equal(expected[i][2], v.Z);
            }
        }

        /// <summary>
        /// A box grid has no mask, and two hundred steps of settling, stirring and advection
        /// leave it where they always did.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The mask is the tank's one change to this class and it is a <c>null</c> array in a
        /// box, so what is asserted first is that it is absent — every cell live, the live volume
        /// the box's own volume — and then that the stock two hundred steps later is the same
        /// number to the last digit, in total and in two named cells. Two cells rather than one
        /// because a total can hide a pair of equal and opposite errors, and a seam cell
        /// (<c>ix = 19</c>, the last column, whose east face is the wrap) because the wrap is what
        /// the tank replaced with glass.
        /// </para>
        /// <para>
        /// <b>The seam cell now reads exactly 2 and no longer pins the wrap</b>, and saying so is
        /// better than leaving a number that looks like it is still doing the job. A uniform seed
        /// plus sinking plus stirring is uniform across every layer, and the repaired transport
        /// keeps a uniform field uniform, so the horizontal passes have nothing to carry and the
        /// cell holds what it was seeded with. What took the job over is
        /// <c>ConservativeTransportTests.TheNewRouteIsStillLocalStillUpwindAndStillWrapsAtTheSeam</c>,
        /// which puts a lump in that same last column and watches it cross.
        /// </para>
        /// </remarks>
        [Fact]
        public void TheBoxGridIsUnchanged()
        {
            CurrentField current = Water();

            var grid = new GridField(100f, 0.002f, 60f, 0f, 0f, 4, 1f);

            Assert.Equal(20, grid.CellsX);
            Assert.Equal(60, grid.CellsY);
            Assert.Equal(5, grid.CellsZ);

            // No mask: the whole array is water, which is what every recorded run had.
            Assert.Equal(20 * 60 * 5, grid.LiveCellCount);
            Assert.Equal(6000d, grid.LiveVolumeCubicMetres);
            Assert.True(grid.IsLive(0, 0, 0));
            Assert.True(grid.IsLive(19, 59, 4));

            grid.SeedUniform(2f);
            Assert.Equal(12000d, grid.TotalJoules);

            for (int s = 0; s < 200; s++)
            {
                grid.Settle(0.5f);
                grid.Mix(0.5f, 0.02f, 0.02f);
                grid.Advect(current, 0.5 * s, 0.5f, grid.PatchWidthMetres);
            }

            Assert.Equal(12000d, grid.TotalJoules);
            Assert.Equal(1.9059117348309726d, grid.JoulesAt(0, 0, 0));
            Assert.Equal(2d, grid.JoulesAt(19, 30, 4));
            Assert.Equal(46.26154862916448d, grid.StockInLayer(0, 0));
        }

        /// <summary>
        /// The stream a founder's spot is drawn from, unchanged in order and in value.
        /// </summary>
        /// <remarks>
        /// <c>SharedVolume.TryReserveFounder</c> draws two floats per attempt — x over the box's
        /// length, then z over its width — and the tank's branch draws two as well, so that the
        /// stream advances by the same amount in either shape. The placer is Sim-side and Core
        /// cannot reach it, but the stream is <see cref="Rng"/> at
        /// <see cref="World.PlacementIndex"/> and the draws are <see cref="Rng.Range"/>, so what
        /// the placer will get out of it is pinnable here: these eight pairs are the first eight
        /// founder positions seed 3 offers in a 20 x 5 m box, whatever happens to them afterwards.
        /// </remarks>
        [Fact]
        public void ThePlacementStreamIsUnchanged()
        {
            var rng = new Rng(Rng.SeedFor(3UL, World.PlacementIndex));

            var expected = new[]
            {
                new[] { 17.733433f, 0.2552101f },
                new[] { 14.135342f, 1.6386709f },
                new[] { 14.334644f, 3.5421004f },
                new[] { 19.652266f, 0.84442466f },
                new[] { 16.39057f, 3.4118476f },
                new[] { 8.504767f, 0.8783108f },
                new[] { 1.4819717f, 1.1005745f },
                new[] { 6.265215f, 4.734241f },
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i][0], rng.Range(0f, 20f));
                Assert.Equal(expected[i][1], rng.Range(0f, 5f));
            }
        }

        /// <summary>
        /// Four hundred steps of the campaign's own world, to the bit: its books, its bodies and
        /// where they are.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The strongest of the four, because it is all of them at once with the economy on top:
        /// the grid seeded from a density, the transport field advecting it, founders admitted at
        /// depths the world draws, births, deaths and the population floor. A leak anywhere in
        /// Core's box path moves one of these numbers, and the counts move visibly rather than in
        /// the last digit — which is the point, since that is how the divergence would present in
        /// a run.
        /// </para>
        /// <para>
        /// <b>Two hundred simulated seconds, not six hundred</b>, because this runs in a second
        /// and the suite is a feedback loop of seconds by design (CLAUDE.md). A butterfly is
        /// visible long before then: the world has founded, bred, killed and refilled from the
        /// floor a hundred and ten times by the last step.
        /// </para>
        /// <para>
        /// <b>No placer</b>, so every body sits at its patch's centre — this is a Core world, and
        /// a Core world's field shape is not the farm's (CLAUDE.md's vertex-field gotcha makes
        /// the same point). That does not weaken the pin: what is asserted is sameness, and the
        /// sameness of a world with no placer is exactly as sensitive to an arithmetic change as
        /// the sameness of one with it.
        /// </para>
        /// </remarks>
        [Fact]
        public void ABoxWorldRunsTheSameFourHundredSteps()
        {
            var config = new RunConfig
            {
                Light = new LightModel(200f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 100f,
                HorizontalPatches = 4f,
                WorldDepthMetres = 60f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 5f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 2f,
                CorpseDecayPerSecond = 0.005f,
                SenescenceDoublingSeconds = 3000f,
                FounderDepthSpread = 60f,
                NutrientSinkMetresPerSecond = 0.002f,
                MatterSinkMetresPerSecond = 0.002f,
                ExcretionPerJoule = 0.01f,
                ExudationFraction = 0.15f,
                Current = Water(),
            };

            // The three knobs the tank build added, at the values that are every run on file.
            Assert.Equal(WorldShape.Box, config.WorldShape);
            Assert.Equal(0f, config.MatterBudgetUnits);
            Assert.False(config.DriveLimitAtEveryStep);

            var world = new World(config, seed: 3);

            Assert.Equal(6000d, world.MatterInitialTotal);

            for (int s = 0; s < 400; s++) world.Step(0.5f);

            double sumY = 0d;
            for (int i = 0; i < world.Living.Count; i++) sumY += world.Living[i].HeightY;

            Assert.Equal(77, world.Living.Count);
            Assert.Equal(67L, world.Births);
            Assert.Equal(97L, world.Deaths);
            Assert.Equal(107L, world.FloorSpawns);

            Assert.Equal(-5.282669079768193d, sumY / world.Living.Count);
            Assert.Equal(6000d, world.StandingMatter);
            Assert.Equal(4297.9588841974455d, world.Nutrients.TotalJoules);
            Assert.Equal(6000d, world.Matter.TotalJoules);
            Assert.Equal(2.07525026780786E-05d, world.AuditResidual);
        }
    }
}
