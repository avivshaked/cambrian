using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Experiments on the vertex field, kept as tests so they run under the same build as the
    /// world and print their readings. They assert only what must hold (conservation); the
    /// numbers are the point, and the logbook entry that cites them is the record.
    /// </summary>
    public sealed class VertexFieldExperiments
    {
        private readonly ITestOutputHelper _output;

        public VertexFieldExperiments(ITestOutputHelper output) => _output = output;

        private const float Area = 100f;   // 4 patches × 5 m, z ring 5 m
        private const int Patches = 4;
        private const float Depth = 20f;

        private static FieldPoint P(float x, float y, float z) =>
            new FieldPoint(new Float3(x, y, z), (int)Math.Floor(x / 5f) % Patches);

        /// <summary>
        /// The owner's hypothesis of 2026-09-08: the field is too coarse for a sitter and a mover to
        /// see materially different water. Two identical blind mouths eat by the world's own rule
        /// (draw = density × clearance × volume × dt; round 30 runs satiation and toe at 0) in
        /// water seeded at round 30's mean detritus density, and the count in reach, the mixing
        /// rate, the mover's speed and the presence of fixed sources beside the mouths are swept.
        /// The reading is the mover's intake over the sitter's, and the sitter's density over the
        /// field's mean.
        /// </summary>
        [Fact]
        public void HowDeepAHoleASitterEatsDependsOnMixingAndSpeedAndNotOnTheCount()
        {
            const float rho = 2f;          // J/m³, round 30's layer means run 0.7–4.6
            const float clearance = 10f;   // round 30's header: clearance 10
            const float volume = 0.05f;    // m³ of absorptive tissue, a small stomach
            const float dt = 0.5f;         // the metabolic step
            const int steps = 400;         // 200 s, several kernel-exchange times at either D

            // Quanta chosen so a 1 m kernel (4.19 m³) holds about 9, 30 and 100 vertices at rho.
            float[] quanta = { 0.95f, 0.28f, 0.084f };
            float[] mixing = { 0.2f, 0.02f };
            float[] speeds = { 0.03f, 0.3f }; // m/s: round 30's drift, and a swimmer
            bool[] sourced = { false, true };

            _output.WriteLine("in-reach  D(m2/s)  v(m/s)  sources  sitter J  mover J  mover/sitter  rho@sitter/mean  rho@mover/mean");

            foreach (float q in quanta)
            foreach (float d in mixing)
            foreach (float v in speeds)
            foreach (bool src in sourced)
            {
                var field = new VertexField(
                    Area, 1f, 0f, Depth, 0f, 0f, Patches,
                    kernelMetres: 1f, mergeMetres: 0.25f, vertexCap: 2_000_000, vertexJoules: q, seed: 11UL);
                field.SeedUniform(rho);
                double inReach = field.Count / (Area * Depth) * 4.18879;

                // Fixed sources at round 30's exudate rate per cubic metre (about 0.005 J/s/m³),
                // 1.5 per m³ like the producers, each depositing its share every step.
                var rng = new Rng(5UL);
                int sources = src ? (int)(1.5f * Area * Depth) : 0;
                var sx = new float[sources]; var sy = new float[sources]; var sz = new float[sources];
                for (int i = 0; i < sources; i++)
                {
                    sx[i] = rng.NextFloat() * 20f; sy[i] = -rng.NextFloat() * Depth; sz[i] = rng.NextFloat() * 5f;
                }
                float perSource = sources > 0 ? 0.005f * Area * Depth * dt / sources : 0f;

                FieldPoint sitter = P(5f, -10f, 2.5f);
                double sitterAte = 0, moverAte = 0;
                double before = field.TotalJoules, deposited = 0;

                for (int step = 0; step < steps; step++)
                {
                    FieldPoint mover = P((15f + v * dt * step) % 20f, -10f, 2.5f);

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

                FieldPoint moverEnd = P((15f + v * dt * steps) % 20f, -10f, 2.5f);
                double mean = field.TotalJoules / (Area * Depth);
                double atSitter = field.EdibleDensityAt(sitter) / mean;
                double atMover = field.EdibleDensityAt(moverEnd) / mean;

                _output.WriteLine(
                    $"{inReach,7:0}  {d,7:0.00}  {v,6:0.00}  {(src ? "on " : "off"),7}  {sitterAte,8:0.0}  {moverAte,7:0.0}  {moverAte / sitterAte,12:0.00}  {atSitter,15:0.00}  {atMover,14:0.00}");

                Assert.Equal(before + deposited - sitterAte - moverAte, field.TotalJoules, 2);
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
                var field = new VertexField(
                    Area, 1f, sink, depth, 0f, 0f, Patches,
                    kernelMetres: 1f, mergeMetres: 0.25f, vertexCap: 400_000, vertexJoules: 0.125f, seed: 3UL);
                var rng = new Rng(9UL);
                float perStep = 0.005f * Area * depth * dt; // round 30's exudate, ~30 J/s per 6,000 m³

                for (int step = 0; step < steps; step++)
                {
                    field.Emit(perStep, new Float3(10f, -8.5f, 2.5f), new Float3(10f, 3.5f, 2.5f));
                    field.Settle(dt);
                    field.Remineralise(dt, r);
                    field.Mix(dt, d, d);
                }

                double[] band = new double[6];
                double floor = 0, weighted = 0, total = 0;
                for (int i = 0; i < field.StoreLength; i++)
                {
                    if (!field.IsAlive(i)) continue;
                    float y = field.PositionOf(i).Y; double m = field.JoulesOf(i);
                    total += m; weighted += m * y;
                    int b = Math.Min(5, (int)(-y / 10f)); band[b] += m;
                    if (y <= -depth + 1e-3f) floor += m;
                }

                _output.WriteLine(
                    $"{d,7:0.00}  {r,7:0.00}  {total,7:0}  {band[0] / total,6:0%}  {band[1] / total,5:0%}  {band[2] / total,5:0%}  {band[3] / total,5:0%}  {band[4] / total,5:0%}  {band[5] / total,5:0%}  {floor / total,8:0%}  {weighted / total,10:0.0}");
            }
        }

        /// <summary>
        /// How long a corpse lasts as a patch. Fifty joules land at one point in water at round
        /// 30's mean density and are stirred at 0.2 or 0.02 m²/s; the reading is the density at
        /// the point over the mean after 10, 30, 100 and 300 s. A patch a mover can find is one that
        /// is still there when the mover arrives.
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
                var field = new VertexField(
                    Area, 1f, 0f, Depth, 0f, 0f, Patches,
                    kernelMetres: 1f, mergeMetres: 0.25f, vertexCap: 2_000_000, vertexJoules: 0.125f, seed: 13UL);
                field.SeedUniform(rho);
                FieldPoint corpse = P(10f, -10f, 2.5f);
                field.Emit(50.0, new Float3(10f, -10f, 2.5f), new Float3(0.3f, 0.3f, 0.3f));
                double mean = field.TotalJoules / (Area * Depth);

                var line = $"{d,7:0.00}";
                int done = 0;
                foreach (int mark in marks)
                {
                    for (; done < mark; done++) field.Mix(dt, d, d);
                    line += $"  {field.EdibleDensityAt(corpse) / mean,7:0.0}";
                }

                _output.WriteLine(line);
            }
        }
    }
}
