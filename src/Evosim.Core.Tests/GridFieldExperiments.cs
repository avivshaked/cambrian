using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The three experiments of <see cref="VertexFieldExperiments"/>, run on the grid instead, so
    /// the two representations can be read side by side. They assert only what must hold
    /// (conservation); the numbers are the point, and the logbook entry that cites them is the
    /// record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One column had to change.</b> The vertex table sweeps the number of vertices a kernel
    /// holds, because that is what buys a vertex field its resolution. A grid buys resolution with
    /// the cell size and holds exactly one number per cell, so the first column is the cell and the
    /// volume a mouth reaches. Everything else is the same sweep at the same settings, and the box
    /// is 24 by 6 by 24 m rather than 20 by 5 by 20 so that cells of 1, 2 and 3 m all divide it.
    /// </para>
    /// <para>
    /// Marked <c>Slow</c> and left out of the default run, alongside <see cref="VertexFieldExperiments"/>:
    /// this is a scan of the field's own behaviour, not a guard on it. Run it with
    /// <c>core-test.ps1 -All</c>.
    /// </para>
    /// </remarks>
    [Trait("Category", "Slow")]
    public sealed class GridFieldExperiments
    {
        private readonly ITestOutputHelper _output;

        public GridFieldExperiments(ITestOutputHelper output) => _output = output;

        private const float Area = 144f;   // 4 patches × 6 m, z ring 6 m
        private const int Patches = 4;
        private const float Depth = 24f;
        private const float Ring = 24f;
        private const float Width = 6f;

        private static FieldPoint P(float x, float y, float z) =>
            new FieldPoint(new Float3(x, y, z), (int)Math.Floor(x / Width) % Patches);

        /// <summary>
        /// The owner's hypothesis of 2026-09-08, asked of the grid: is the field too coarse for a
        /// sitter and a mover to see materially different water? Two identical blind mouths eat by
        /// the world's own rule (draw = density × clearance × volume × dt; round 30 runs satiation
        /// and toe at 0) in water seeded at round 30's mean detritus density, and the cell size,
        /// the mixing rate, the mover's speed and the presence of fixed sources beside the mouths
        /// are swept. The reading is the mover's intake over the sitter's, and each mouth's density
        /// over the field's mean.
        /// </summary>
        [Fact]
        public void HowDeepAHoleASitterEatsDependsOnMixingAndSpeedAndNotOnTheCell()
        {
            const float rho = 2f;          // J/m³, round 30's layer means run 0.7 to 4.6
            const float clearance = 10f;   // round 30's header: clearance 10
            const float volume = 0.05f;    // m³ of absorptive tissue, a small stomach
            const float dt = 0.5f;         // the metabolic step
            const int steps = 400;         // 200 s, several mixing times at either D

            // Cells that divide the box. 1 m is the vertex field's kernel support; below about
            // 0.78 m the 0.2 m²/s arm would be over the grid's stability limit at this step.
            float[] cells = { 1f, 2f, 3f };
            float[] mixing = { 0.2f, 0.02f };
            float[] speeds = { 0.03f, 0.3f }; // m/s: round 30's drift, and a swimmer
            bool[] sourced = { false, true };

            _output.WriteLine("cell(m)  reach m3  D(m2/s)  v(m/s)  sources  sitter J  mover J  mover/sitter  rho@sitter/mean  rho@mover/mean");

            foreach (float cell in cells)
            foreach (float d in mixing)
            foreach (float v in speeds)
            foreach (bool src in sourced)
            {
                var field = new GridField(Area, 0f, Depth, 0f, 0f, Patches, cell);
                field.SeedUniform(rho);
                double reach = field.CellVolume;

                // Fixed sources at round 30's exudate rate per cubic metre (about 0.005 J/s/m³),
                // 1.5 per m³ like the producers, each depositing its share every step.
                var rng = new Rng(5UL);
                int sources = src ? (int)(1.5f * Area * Depth) : 0;
                var sx = new float[sources]; var sy = new float[sources]; var sz = new float[sources];
                for (int i = 0; i < sources; i++)
                {
                    sx[i] = rng.NextFloat() * Ring; sy[i] = -rng.NextFloat() * Depth; sz[i] = rng.NextFloat() * Width;
                }
                float perSource = sources > 0 ? 0.005f * Area * Depth * dt / sources : 0f;

                FieldPoint sitter = P(5f, -10f, 2.5f);
                double sitterAte = 0, moverAte = 0;
                double before = field.TotalJoules, deposited = 0;

                for (int step = 0; step < steps; step++)
                {
                    FieldPoint mover = P((15f + v * dt * step) % Ring, -10f, 2.5f);

                    field.ClearDemand();
                    float wantS = field.EdibleDensityAt(sitter) * clearance * volume * dt;
                    float wantM = field.EdibleDensityAt(mover) * clearance * volume * dt;
                    field.Demand(sitter, wantS);
                    field.Demand(mover, wantM);
                    field.FreezeAvailability();
                    sitterAte += field.Take(sitter, wantS * field.ShareAt(sitter));
                    moverAte += field.Take(mover, wantM * field.ShareAt(mover));

                    field.Mix(dt, d, d);
                    for (int i = 0; i < sources; i++)
                    {
                        field.Deposit(P(sx[i], sy[i], sz[i]), perSource);
                        deposited += perSource;
                    }
                }

                FieldPoint moverEnd = P((15f + v * dt * steps) % Ring, -10f, 2.5f);
                double mean = field.TotalJoules / (Area * Depth);
                double atSitter = field.EdibleDensityAt(sitter) / mean;
                double atMover = field.EdibleDensityAt(moverEnd) / mean;

                _output.WriteLine(
                    $"{cell,7:0.0}  {reach,8:0.0}  {d,7:0.00}  {v,6:0.00}  {(src ? "on " : "off"),7}  {sitterAte,8:0.0}  {moverAte,7:0.0}  {moverAte / sitterAte,12:0.00}  {atSitter,15:0.00}  {atMover,14:0.00}");

                Assert.Equal(before + deposited - sitterAte - moverAte, field.Recount(), 2);
            }
        }

        /// <summary>
        /// Check 6 of <c>fable-propose-aquarium.md</c>: the same sitter-against-mover question,
        /// asked of the world round 38 actually runs rather than the loose 144 m² box above —
        /// <see cref="WorldShape.Tank"/> at the quadrupled footprint (400 m², radius
        /// <see cref="TankGeometry.RadiusFor"/> of it) with the matter held at
        /// <see cref="RunConfig.MatterBudgetUnits"/> 6,000. The detritus cell is the campaign's
        /// 1 m (<see cref="RunConfig.FieldCellMetres"/>) and the matter cell its 5 m
        /// (<see cref="RunConfig.FieldMatterCellMetres"/>); the depth is the world's own 60 m,
        /// because 24 m — the box variant's toy depth — does not divide by 5. A tank has a wall
        /// where the box had a seam, so the mover cannot wrap: it crosses the full diameter
        /// through the axis and bounces off the glass, and the sitter sits on the axis itself.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>No current.</b> This reruns the box variant's own scripted mover — a straight track
        /// at a fixed speed — rather than driving either body with the tank's own streams;
        /// the check asks what the mask and the mixing do to a mouth's reach, the same question
        /// the box asked, not what the current adds on top of it. That is a deliberate narrowing
        /// to keep this cheap, not a claim the current does nothing — a current-driven version is a
        /// different, larger check than the proposal's check 6 asks for.
        /// </para>
        /// <para>
        /// <b>Detritus is not the matter budget.</b> Ruling 2 dilutes
        /// <see cref="RunConfig.MatterBudgetUnits"/>, which is <c>World.Matter</c>'s own field —
        /// the reproduction currency — and not the food a mouth grazes (<c>World.Nutrients</c>):
        /// light still makes detritus at the same rate per square metre wherever the glass sits,
        /// so the first table keeps the food density at round 30's mean (rho = 2) exactly as the
        /// box variant does and changes only the shape. The second table is what ruling 2 is
        /// actually about: it reuses the identical demand/take mechanic on a field built like
        /// <c>World.Matter</c>, seeded at the budget divided by the tank's own live volume — the
        /// same division <see cref="World"/> itself seeds with — so it reads whether a mover
        /// reaches materially more matter than a sitter once the budget is spread over four times
        /// the footprint at the same total. There is no continuous matter grazing in the real
        /// world (D055: a body draws matter in one lump at conception), so this is a probe built
        /// from the field's own API and not a model of conception; it answers "is there more
        /// matter near the mover's track than the sitter's spot", which is the quantity a founder
        /// actually needs, in the units <see cref="RunConfig.MatterBudgetUnits"/> is priced in
        /// rather than joules.
        /// </para>
        /// </remarks>
        [Fact]
        public void HowDeepAHoleASitterEatsInTheDiluteTank()
        {
            const float area = 400f;          // ruling 2's round 38 footprint, 4x the 100 m2 campaign
            const float depth = 60f;          // the world's own depth; 24 (the box toy) does not divide by 5
            const float matterBudget = 6000f; // ruling 2's held total
            const int rings = 2;              // centre and rim — ruling 1's "the agent's pick is two rings"
            const float detritusCell = 1f;    // RunConfig.FieldCellMetres
            const float matterCell = 5f;      // RunConfig.FieldMatterCellMetres, D086's ruled value
            const float rho = 2f;             // unchanged: detritus is not what ruling 2 dilutes
            const float clearance = 10f;
            const float volume = 0.05f;
            const float dt = 0.5f;
            const int steps = 400;            // 200 s, the box variant's own duration

            float radius = TankGeometry.RadiusFor(area);
            float diameter = 2f * radius;

            // A tank has no seam to wrap at, so the mover bounces along the diameter through the
            // axis instead of circling an annulus — the longest chord, and the one line guaranteed
            // to stay inside the glass at every point between its ends.
            float MoverXAt(double travelledMetres)
            {
                double period = 2d * diameter;
                double onLine = travelledMetres % period;
                if (onLine < 0) onLine += period;
                return (float)(onLine <= diameter ? onLine : period - onLine);
            }

            FieldPoint TankPoint(float x, float y, float z) =>
                new FieldPoint(new Float3(x, y, z), TankGeometry.RingOf(x, z, radius, rings));

            _output.WriteLine(
                $"tank: area {area:0} m2, radius {radius:0.000} m, depth {depth:0} m, matter budget {matterBudget:0} units, rings {rings}");

            float[] mixing = { 0.2f, 0.02f };
            float[] speeds = { 0.03f, 0.3f };
            bool[] sourced = { false, true };

            _output.WriteLine("-- detritus: rho fixed at round 30's mean, unchanged by ruling 2 --");
            _output.WriteLine("cell(m)  reach m3  D(m2/s)  v(m/s)  sources  sitter J  mover J  mover/sitter  rho@sitter/mean  rho@mover/mean");

            foreach (float d in mixing)
            foreach (float v in speeds)
            foreach (bool src in sourced)
            {
                var field = new GridField(area, 0f, depth, 0f, 0f, rings, detritusCell, 1, WorldShape.Tank, radius);
                field.SeedUniform(rho);
                double reach = field.CellVolume;

                // Fixed sources at round 30's exudate rate per cubic metre, rejection-sampled into
                // the disc rather than the bounding square: a corner deposit would be walked to the
                // nearest live cell by GridField itself, but that would pile every corner's share
                // onto the rim rather than spreading it the way real producers would.
                var rng = new Rng(5UL);
                int sources = src ? (int)(1.5f * area * depth) : 0;
                var sx = new float[sources]; var sy = new float[sources]; var sz = new float[sources];
                for (int i = 0; i < sources; i++)
                {
                    float x, z;
                    do
                    {
                        x = rng.NextFloat() * diameter;
                        z = rng.NextFloat() * diameter;
                    } while (!TankGeometry.Inside(x, z, radius));

                    sx[i] = x; sy[i] = -rng.NextFloat() * depth; sz[i] = z;
                }
                float perSource = sources > 0 ? 0.005f * area * depth * dt / sources : 0f;

                FieldPoint sitter = TankPoint(radius, -10f, radius);
                double sitterAte = 0, moverAte = 0;
                double before = field.TotalJoules, deposited = 0;

                for (int step = 0; step < steps; step++)
                {
                    FieldPoint mover = TankPoint(MoverXAt(v * dt * step), -10f, radius);

                    field.ClearDemand();
                    float wantS = field.EdibleDensityAt(sitter) * clearance * volume * dt;
                    float wantM = field.EdibleDensityAt(mover) * clearance * volume * dt;
                    field.Demand(sitter, wantS);
                    field.Demand(mover, wantM);
                    field.FreezeAvailability();
                    sitterAte += field.Take(sitter, wantS * field.ShareAt(sitter));
                    moverAte += field.Take(mover, wantM * field.ShareAt(mover));

                    field.Mix(dt, d, d);
                    for (int i = 0; i < sources; i++)
                    {
                        field.Deposit(TankPoint(sx[i], sy[i], sz[i]), perSource);
                        deposited += perSource;
                    }
                }

                FieldPoint moverEnd = TankPoint(MoverXAt(v * dt * steps), -10f, radius);
                double mean = field.TotalJoules / field.LiveVolumeCubicMetres;
                double atSitter = field.EdibleDensityAt(sitter) / mean;
                double atMover = field.EdibleDensityAt(moverEnd) / mean;

                _output.WriteLine(
                    $"{detritusCell,7:0.0}  {reach,8:0.0}  {d,7:0.00}  {v,6:0.00}  {(src ? "on " : "off"),7}  {sitterAte,8:0.0}  {moverAte,7:0.0}  {moverAte / sitterAte,12:0.00}  {atSitter,15:0.00}  {atMover,14:0.00}");

                Assert.Equal(before + deposited - sitterAte - moverAte, field.Recount(), 2);
            }

            // The matter field itself: cheap because GridField's Demand/Take/Mix API knows
            // nothing about which substance it holds, so the same mechanic runs on a field built
            // and seeded exactly the way World.cs builds and seeds World.Matter — see LiveVolumeOf
            // and the seedDensity computation in World's constructor.
            _output.WriteLine("-- matter: World.Matter's own field, seeded at the budget over the tank's live volume --");

            var probe = new GridField(area, 0f, depth, 0f, 0f, rings, matterCell, 1, WorldShape.Tank, radius);
            double liveVolume = probe.LiveVolumeCubicMetres;
            float matterDensity = (float)(matterBudget / liveVolume);
            _output.WriteLine(
                $"matter cell live volume {liveVolume:0} m3 of a {diameter:0.00}x{diameter:0.00}x{depth:0} m bounding array -> seed density {matterDensity:0.000} units/m3");
            _output.WriteLine("cell(m)  reach m3  D(m2/s)  v(m/s)  sitter units  mover units  mover/sitter  rho@sitter/mean  rho@mover/mean");

            foreach (float d in mixing)
            foreach (float v in speeds)
            {
                var field = new GridField(area, 0f, depth, 0f, 0f, rings, matterCell, 1, WorldShape.Tank, radius);
                field.SeedUniform(matterDensity);
                double reach = field.CellVolume;

                FieldPoint sitter = TankPoint(radius, -10f, radius);
                double sitterAte = 0, moverAte = 0;
                double before = field.TotalJoules;

                for (int step = 0; step < steps; step++)
                {
                    FieldPoint mover = TankPoint(MoverXAt(v * dt * step), -10f, radius);

                    field.ClearDemand();
                    float wantS = field.EdibleDensityAt(sitter) * clearance * volume * dt;
                    float wantM = field.EdibleDensityAt(mover) * clearance * volume * dt;
                    field.Demand(sitter, wantS);
                    field.Demand(mover, wantM);
                    field.FreezeAvailability();
                    sitterAte += field.Take(sitter, wantS * field.ShareAt(sitter));
                    moverAte += field.Take(mover, wantM * field.ShareAt(mover));

                    field.Mix(dt, d, d);
                }

                FieldPoint moverEnd = TankPoint(MoverXAt(v * dt * steps), -10f, radius);
                double mean = field.TotalJoules / field.LiveVolumeCubicMetres;
                double atSitter = field.EdibleDensityAt(sitter) / mean;
                double atMover = field.EdibleDensityAt(moverEnd) / mean;

                _output.WriteLine(
                    $"{matterCell,7:0.0}  {reach,8:0.0}  {d,7:0.00}  {v,6:0.00}  {sitterAte,12:0.000}  {moverAte,11:0.000}  {moverAte / sitterAte,12:0.00}  {atSitter,15:0.00}  {atMover,14:0.00}");

                Assert.Equal(before - sitterAte - moverAte, field.Recount(), 2);
            }
        }

        /// <summary>
        /// Where the food sits when the mixing comes down. Exudate at round 30's rate enters a band
        /// where its producers stand (−5 to −12 m) in a 60 m column, sinks at the world's rate, and
        /// is stirred at 0.2 or 0.02 m²/s with remineralisation off or on. No mouths: the profile
        /// is the supply a stomach would have to reach. A scale height of D/w says 100 m at 0.2 and
        /// 10 m at 0.02, so the reading is whether the larder moves to the deep.
        /// </summary>
        [Fact]
        public void WhereTheFoodSitsWhenTheMixingComesDown()
        {
            const float depth = 60f, dt = 0.5f, sink = 0.002f;
            const int steps = 6000; // 3,000 s, one lifetime
            float[] mixing = { 0.2f, 0.02f };
            float[] remin = { 0f, 0.01f };

            _output.WriteLine("D(m2/s)  remin/s  total J   0-10 m  10-20  20-30  30-40  40-50  50-60  on floor  mean depth");

            foreach (float d in mixing)
            foreach (float r in remin)
            {
                var field = new GridField(Area, sink, depth, 0f, 0f, Patches, 1f);
                float perStep = 0.005f * Area * depth * dt; // round 30's exudate, ~43 J/s per 8,640 m³

                for (int step = 0; step < steps; step++)
                {
                    field.DepositBox(perStep, new Float3(0.5f * Ring, -8.5f, 0.5f * Width), new Float3(0.5f * Ring, 3.5f, 0.5f * Width));
                    field.Settle(dt);
                    field.Remineralise(dt, r);
                    field.Mix(dt, d, d);
                }

                double[] band = new double[6];
                double floor = 0, weighted = 0, total = 0;
                for (int iy = 0; iy < field.CellsY; iy++)
                {
                    double layer = 0;
                    for (int ix = 0; ix < field.CellsX; ix++)
                    for (int iz = 0; iz < field.CellsZ; iz++) layer += field.JoulesAt(ix, iy, iz);

                    float y = -(iy + 0.5f);
                    total += layer; weighted += layer * y;
                    band[Math.Min(5, (int)(-y / 10f))] += layer;
                    if (iy == field.CellsY - 1) floor += layer;
                }

                _output.WriteLine(
                    $"{d,7:0.00}  {r,7:0.00}  {total,7:0}  {band[0] / total,6:0%}  {band[1] / total,5:0%}  {band[2] / total,5:0%}  {band[3] / total,5:0%}  {band[4] / total,5:0%}  {band[5] / total,5:0%}  {floor / total,8:0%}  {weighted / total,10:0.0}");

                // Against what was emitted, not against the same sum read twice: the bands are
                // built from the cells and so is Recount, so comparing them asserts arithmetic
                // rather than conservation.
                Assert.Equal((double)steps * perStep, field.Recount(), 3);
                Assert.Equal(field.Recount(), total, 3);
            }
        }

        /// <summary>
        /// How long a corpse lasts as a patch. Fifty joules land at one point in water at round
        /// 30's mean density and are stirred at 0.2 or 0.02 m²/s; the reading is the density at the
        /// point over the mean after 10, 30, 100 and 300 s. A patch a mover can find is one that is
        /// still there when the mover arrives.
        /// </summary>
        [Fact]
        public void HowLongACorpseLastsAsAPatch()
        {
            const float rho = 2f, dt = 0.5f;
            float[] mixing = { 0.2f, 0.02f };
            int[] marks = { 20, 60, 200, 600 };

            _output.WriteLine("D(m2/s)  at 10 s  at 30 s  at 100 s  at 300 s   (density at the corpse over the mean)");

            foreach (float d in mixing)
            {
                var field = new GridField(Area, 0f, Depth, 0f, 0f, Patches, 1f);
                field.SeedUniform(rho);
                FieldPoint corpse = P(10.5f, -10.5f, 2.5f);
                field.DepositBox(50.0, new Float3(10.5f, -10.5f, 2.5f), new Float3(0.3f, 0.3f, 0.3f));
                double mean = field.TotalJoules / (Area * Depth);

                var line = $"{d,7:0.00}";
                int done = 0;
                foreach (int mark in marks)
                {
                    for (; done < mark; done++) field.Mix(dt, d, d);
                    line += $"  {field.EdibleDensityAt(corpse) / mean,7:0.0}";
                }

                _output.WriteLine(line);

                // The corpse spreads, and none of it leaves: the seed plus the deposit, still
                // there after six hundred stirs.
                Assert.Equal(rho * Area * Depth + 50.0, field.Recount(), 3);
            }
        }
    }
}
