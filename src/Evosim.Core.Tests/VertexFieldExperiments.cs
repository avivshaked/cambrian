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
    }
}
