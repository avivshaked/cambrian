using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Measures how fast an impulse of detritus dropped into the refuge/floor layer
    /// diffuses back into edible water, at four candidate refuge thicknesses -- the
    /// transport timescale a thicker-refuge dose must be chosen from, not guessed
    /// (logbook/0042's dose section; DECISIONS.md's D055/D058-era review finding 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>D055's refuge was sized by area, not by time.</b> <c>FloorRefugeMetres</c> = 1 was
    /// chosen because that is exactly the floor layer, and logbook/0042's dose section
    /// reframed what that number buys: at mixing 0.2 m²/s over 1 m layers, <see cref="Mix"/>
    /// moves 20%/s of the interface difference, so a one-metre refuge is not a slow-release
    /// larder but a boundary condition -- a stripped layer above drains it in tens of
    /// seconds. The round's own pre-registration named the risk it could not answer without
    /// running it: if one metre proves too weak a brake, the next dose is a thicker one, and
    /// nobody had measured what "thicker" buys before this harness existed.
    /// </para>
    /// <para>
    /// <b>A single impulse into an otherwise empty field, not a live world, on purpose.</b>
    /// Watching the refuge inside a full ecology conflates two questions -- does it return
    /// energy fast enough to matter, and does the rest of the food web behave -- and the
    /// D055/D058-era review caught a confound in exactly that area: a consumer sunk below
    /// the seabed reads the same field state as one resting on it, so a bust under a live
    /// refuge could be exile rather than weak damping. Depositing a fixed 10,000 J into the
    /// floor layer and watching only <see cref="NutrientField.Settle"/> and
    /// <see cref="NutrientField.Mix"/> move it removes every other mechanism from the
    /// answer: what comes out is the field's own transport timescale, which existed before
    /// D055 and does not change if the ecology above it changes.
    /// </para>
    /// <para>
    /// <b>Assertion is conservation, not a verdict on dose size.</b> Whether 1, 5 or 10 m is
    /// "enough" is a question about the consumer sitting above it, which this harness does
    /// not model -- so nothing here asserts pass/fail on the recovery numbers, only that
    /// <c>Settle</c> and <c>Mix</c> never create or destroy energy while moving it. The
    /// printed table is the product: whoever picks the next round's
    /// <c>FloorRefugeMetres</c> reads the time-to-25/50/75%-edible off it directly, the same
    /// "measure, don't guess" protocol <see cref="CalibrationSweep"/> and
    /// <see cref="SpeciesCalibration"/> use for their own knobs.
    /// </para>
    /// <para>
    /// <b>R = 0 has no protected layer</b> (<see cref="NutrientField.RefugeLayerCount"/> is
    /// 0, so <see cref="NutrientField.IsRefuge"/> never returns true), so it is not really a
    /// refuge run at all -- it is the baseline this compares every thicker refuge against.
    /// Its "refuge" row is an accounting fiction: the physical floor layer alone, tracked so
    /// the four rows are read on the same axes, even though nothing there is actually
    /// shielded from grazing.
    /// </para>
    /// </remarks>
    public class RefugeImpulse
    {
        private readonly ITestOutputHelper _output;

        public RefugeImpulse(ITestOutputHelper output) => _output = output;

        private const float WorldArea = 400f;
        private const float LayerMetres = 1f;
        private const float SinkMetresPerSecond = 0.02f;
        private const float WorldDepth = 60f;
        private const float MixDiffusivity = 0.2f;
        private const float ImpulseJoules = 10000f;
        private const float DepositHeightY = -59.5f; // Falls in the floor layer regardless of refuge thickness.
        private const float StepSeconds = 0.5f;
        private const float WindowSeconds = 6000f;
        private const float SampleIntervalSeconds = 500f;

        private static readonly float[] RefugeMetresToTest = { 0f, 1f, 5f, 10f };
        private static readonly float[] Milestones = { 0.25f, 0.5f, 0.75f };

        private readonly struct Sample
        {
            public readonly float T;
            public readonly double RefugeJoules;
            public readonly double EdibleJoules;
            public readonly double AboveRefugeDensity;

            public Sample(float t, double refugeJoules, double edibleJoules, double aboveRefugeDensity)
            {
                T = t;
                RefugeJoules = refugeJoules;
                EdibleJoules = edibleJoules;
                AboveRefugeDensity = aboveRefugeDensity;
            }
        }

        [Fact]
        public void ImpulseTransportTimescaleByRefugeThickness()
        {
            _output.WriteLine(
                $"{WorldArea:0} m2 · {LayerMetres:0} m layers · sink {SinkMetresPerSecond} m/s · " +
                $"depth {WorldDepth:0} m · mixing {MixDiffusivity} m2/s · impulse {ImpulseJoules:0} J " +
                $"at y={DepositHeightY} · step {StepSeconds} s · window {WindowSeconds:0} s");

            foreach (float refugeMetres in RefugeMetresToTest)
            {
                var field = new NutrientField(WorldArea, LayerMetres, SinkMetresPerSecond, WorldDepth, refugeMetres);
                field.Deposit(DepositHeightY, ImpulseJoules);

                double initialTotal = field.TotalJoules;

                // R=0 protects nothing (class remarks) -- the floor layer alone stands in as
                // the comparison row so all four thicknesses are read on the same axes.
                int refugeLayerStart = refugeMetres > 0f
                    ? field.LayerCount - field.RefugeLayerCount
                    : field.LayerCount - 1;
                int aboveRefugeLayer = refugeLayerStart - 1;

                var samples = new List<Sample>();
                var milestoneT = new float?[Milestones.Length];

                void RecordSample(float t)
                {
                    double refugeJoules = 0.0;
                    for (int layer = refugeLayerStart; layer < field.LayerCount; layer++)
                    {
                        refugeJoules += field.StockInLayer(layer);
                    }
                    double edibleJoules = field.TotalJoules - refugeJoules;
                    double aboveDensity = aboveRefugeLayer >= 0
                        ? field.StockInLayer(aboveRefugeLayer) / field.LayerVolume
                        : 0.0;

                    samples.Add(new Sample(t, refugeJoules, edibleJoules, aboveDensity));

                    double edibleFraction = field.TotalJoules > 0.0 ? edibleJoules / field.TotalJoules : 0.0;
                    for (int m = 0; m < Milestones.Length; m++)
                    {
                        if (milestoneT[m] is null && edibleFraction >= Milestones[m]) milestoneT[m] = t;
                    }
                }

                RecordSample(0f);

                int totalSteps = (int)Math.Round(WindowSeconds / StepSeconds);
                int stepsPerSample = (int)Math.Round(SampleIntervalSeconds / StepSeconds);

                for (int step = 1; step <= totalSteps; step++)
                {
                    field.Settle(StepSeconds);
                    field.Mix(StepSeconds, MixDiffusivity);

                    float t = step * StepSeconds;
                    if (step % stepsPerSample == 0) RecordSample(t);
                    else
                    {
                        // Milestones are checked every step, not only at the printed 500 s
                        // grid, so a crossing between two sample rows is not missed.
                        double refugeJoules = 0.0;
                        for (int layer = refugeLayerStart; layer < field.LayerCount; layer++)
                        {
                            refugeJoules += field.StockInLayer(layer);
                        }
                        double edibleFraction = field.TotalJoules > 0.0
                            ? (field.TotalJoules - refugeJoules) / field.TotalJoules
                            : 0.0;
                        for (int m = 0; m < Milestones.Length; m++)
                        {
                            if (milestoneT[m] is null && edibleFraction >= Milestones[m]) milestoneT[m] = t;
                        }
                    }
                }

                double finalTotal = field.TotalJoules;

                _output.WriteLine("");
                _output.WriteLine(
                    $"Refuge = {refugeMetres:0} m " +
                    (refugeMetres > 0f
                        ? $"({field.RefugeLayerCount} layer(s), index >= {refugeLayerStart})"
                        : "(no protected layer -- floor layer index tracked for comparison)"));
                _output.WriteLine("| t (s) | refuge J | edible J | edible % | density above refuge J/m3 |");
                _output.WriteLine("|---|---|---|---|---|");
                foreach (Sample s in samples)
                {
                    double ediblePct = initialTotal > 0.0 ? 100.0 * s.EdibleJoules / initialTotal : 0.0;
                    _output.WriteLine(
                        $"| {s.T:0} | {s.RefugeJoules:0.0000} | {s.EdibleJoules:0.0000} | " +
                        $"{ediblePct:0.00}% | {s.AboveRefugeDensity:0.0000} |");
                }

                for (int m = 0; m < Milestones.Length; m++)
                {
                    string pct = $"{Milestones[m] * 100:0}%";
                    string when = milestoneT[m] is float t ? $"t={t:0}" : "never (within 6000 s)";
                    _output.WriteLine($"Time to {pct} edible: {when}");
                }

                // The only thing this harness asserts: Settle and Mix move energy between
                // layers, they never create or destroy it. The recovery numbers above are
                // the product for a human to read a dose off, not a pass/fail here.
                double relativeDrift = initialTotal > 0.0
                    ? Math.Abs(finalTotal - initialTotal) / initialTotal
                    : Math.Abs(finalTotal - initialTotal);
                Assert.True(
                    relativeDrift <= 1e-6,
                    $"refuge {refugeMetres} m: TotalJoules drifted from {initialTotal} to {finalTotal} " +
                    $"({relativeDrift:0.###e+00} relative)");
            }
        }
    }
}
