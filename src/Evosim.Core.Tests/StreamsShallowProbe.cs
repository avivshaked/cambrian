using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A probe, not a guard: what the streams' axis balance costs in a tank that is wide and
    /// shallow, and what the relaxed rule buys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question, and it has since been ruled on.</b> <c>CurrentField.BuildStreams</c>
    /// solves one quadratic for the overturning's amplitude <c>β</c>, the amplitude at which the
    /// vertical RMS equals the per-axis horizontal RMS (D088's rule):
    /// <c>β²(V − H₁/2) − βC − H₀/2 = 0</c>, with <c>a = V − H₁/2</c>, <c>b = −C</c>,
    /// <c>c₀ = −H₀/2</c>. The overturning's radial flow carries a factor <c>qπ/D</c>, so
    /// <c>H₁</c> grows as the tank gets shallower at a fixed radius, and at some depth <c>a</c>
    /// goes to zero. This class measured where, and with what margin, at round 42's footprint;
    /// <c>logbook/specs/streams-shallow-spec.md</c> is what it produced, and the owner ruled
    /// <c>λ</c> = 0.76 on it on 2026-09-21. Since that build a tank flatter than the critical
    /// depth is no longer refused: it is built at <c>λ·k_max</c>, and these tables now read a
    /// field that exists rather than one that was declined. <c>StreamsShallowTests</c> is the
    /// guard; this is still the reading.
    /// </para>
    /// <para>
    /// <b>It reads the production field by reflection and edits nothing.</b> The coefficients are
    /// not stored anywhere — they are locals inside <c>BuildStreams</c>. But everything they are
    /// computed from survives the build: the term arrays are filled at the top of the method and
    /// the streams' own weight is set by the first measurement pass. So the probe re-runs the
    /// second pass itself, through the same private <c>Walk</c> and the same private
    /// <c>StreamsUnit</c>, with the same envelope override — the same lattice, the same phases,
    /// the same accumulation order, and therefore the same doubles, on both branches of the rule.
    /// <see cref="TheProbeReproducesTheProductionBeta"/> is the check on that claim: at every
    /// depth, balanced or relaxed, the <c>β</c> the probe solves for equals the one the field
    /// built, exactly.
    /// </para>
    /// <para>
    /// <b>Marked Slow</b>, with the rest of the scans: it builds the streams fourteen times and
    /// costs 14 s, and it reports a number rather than guarding a rule. So it is left out of the
    /// default run, and the command is <c>core-test.ps1 -All -Filter StreamsShallowProbe</c> —
    /// <c>-Filter</c> without <c>-All</c> appends <c>&amp;Category!=Slow</c> and would run
    /// nothing.
    /// </para>
    /// </remarks>
    [Trait("Category", "Slow")]
    public class StreamsShallowProbe
    {
        private readonly ITestOutputHelper _output;

        public StreamsShallowProbe(ITestOutputHelper output) => _output = output;

        // Round 42's tank: 2,200 m2 of footprint, which is a radius of 26.458 m, four rings,
        // 0.1 m/s, a 6,000 s period. The depths are the sweep the owner asked for; 45 and 30 are
        // runs on disk (r42-s1, r42scrB30-s1), 20 is the shelf the balance has no root for.
        private const float Area = 2200f;
        private const int Rings = 4;
        private const float Speed = 0.1f;
        private const float Period = 6000f;

        // The bed round 42 runs, from its own config.json: relief 1.5 m, tilt 30 m, scale 0.
        private const float Relief = 1.5f;
        private const float Tilt = 30f;

        private static readonly float[] Depths = { 45f, 40f, 35f, 30f, 25f, 20f, 15f };

        // The run seeds of the three arms on the machine.
        private static readonly ulong[] Seeds = { 1UL, 2UL, 3UL };

        // BuildStreams's own EnvelopeRms: the mean square of 0.75 + 0.25*sin, in closed form.
        private static readonly double EnvelopeRms = Math.Sqrt(0.59375d);

        // CurrentField's own ShallowAxisFraction — the owner's ruling of 2026-09-21. Repeated
        // here rather than reached for, because the production constant is private and what this
        // probe is for is to solve the same arithmetic independently and find the same answer.
        private const double Lambda = 0.76d;

        private static float Radius => TankGeometry.RadiusFor(Area);

        // ---------------------------------------------------------------- the tables

        /// <summary>
        /// The balance coefficients, the root and the axes, depth by depth, over a flat bed.
        /// </summary>
        [Fact]
        public void TheCoefficientsAcrossDepth()
        {
            _output.WriteLine(
                Inv($"tank radius {Radius:0.000} m ({Area} m2), knob {Speed} m/s, ") +
                Inv($"period {Period} s, seed {Seeds[0]}, flat bed"));
            _output.WriteLine("");
            _output.WriteLine(
                "  depth  D/R     balance  H1        V         a           b            c0     " +
                "  k_max   beta      vert:horiz   measured x / y / z (m/s)");

            foreach (float depth in Depths)
            {
                Probe p = Measure(depth, Seeds[0], bed: null);

                string axes = p.Built
                    ? FormattableString.Invariant($"{p.RmsX:0.00000} / {p.RmsY:0.00000} / {p.RmsZ:0.00000}")
                    : "not built";

                string beta = FormattableString.Invariant($"{p.Beta:0.0000}");
                string ratio = FormattableString.Invariant($"{p.PredictedRatio:0.000}");

                _output.WriteLine(
                    Inv($"  {depth,5:0.}  {depth / Radius,5:0.000}  {(p.A > 1e-12 ? "yes" : "NO "),-7}  ") +
                    Inv($"{p.H1,8:0.00000}  {p.V,8:0.00000}  {p.A,10:0.0000000;-0.0000000}  ") +
                    Inv($"{p.B,11:0.00e+00;-0.00e+00}  {p.C0,6:0.0000;-0.0000}  ") +
                    Inv($"{p.KMax,6:0.000}  {beta,8}  {ratio,8}     {axes}"));
            }

            _output.WriteLine("");
            _output.WriteLine(
                "  a = V - H1/2, b = -C, c0 = -H0/2 (H0 is 1 by construction, so c0 is -0.5 exactly),");
            _output.WriteLine(
                "  which are the balanced rule's coefficients; where 'balance' reads NO the field");
            _output.WriteLine(
                "  solves the same quadratic at k = lambda*k_max instead and the beta printed is that");
            _output.WriteLine(
                "  root. k_max = sqrt(2V/H1) is the supremum of the vertical:per-axis-horizontal RMS");
            _output.WriteLine(
                "  ratio as beta goes to infinity; the balance needs k_max > 1, which is a > 0.");
            _output.WriteLine(
                "  'vert:horiz' is the predicted ratio from the coefficients; 'measured' is an");
            _output.WriteLine(
                "  independent lattice over the live water through the public VelocityAt.");

            // Not an assertion on the water, an assertion that the probe probed: the deep end
            // balances, the shallow end does not, and since the ruling both ends are water.
            Assert.True(Measure(45f, Seeds[0], null).A > 1e-12);
            Assert.False(Measure(20f, Seeds[0], null).A > 1e-12);
            Assert.True(Measure(20f, Seeds[0], null).Built);
        }

        /// <summary>
        /// The probe's arithmetic is the production arithmetic — the same beta, to the bit.
        /// </summary>
        [Fact]
        public void TheProbeReproducesTheProductionBeta()
        {
            foreach (float depth in Depths)
            {
                Probe p = Measure(depth, Seeds[0], bed: null);

                _output.WriteLine(
                    Inv($"depth {depth,3:0.} m ({(p.A > 1e-12 ? "balanced" : "relaxed")}): probe beta {p.Beta:R}, field beta {p.FieldBeta:R} — ") +
                    Inv($"{(p.Beta == p.FieldBeta ? "identical" : "DIFFERENT")}"));

                Assert.Equal(p.FieldBeta, p.Beta);
            }
        }

        /// <summary>
        /// The bed does not move the balance: it is solved on the flat field, and the
        /// floor-following map is a pass that runs after it.
        /// </summary>
        [Fact]
        public void TheBedDoesNotMoveTheBalance()
        {
            _output.WriteLine(
                Inv($"the same sweep with round 42's bed: relief {Relief} m, tilt {Tilt} m, scale 0"));
            _output.WriteLine("");
            _output.WriteLine(
                "  depth  bed        balance  a          beta      measured x / y / z (m/s)");

            foreach (float depth in Depths)
            {
                BedShape bed = null;
                string bedNote;

                try
                {
                    bed = new BedShape(
                        Radius, depth, Relief, Tilt, 0f, Rng.SeedFor(Seeds[0], World.BedShapeIndex));
                    bedNote = Inv($"range {bed.TotalRangeMetres:0.0} m");
                }
                catch (ArgumentOutOfRangeException)
                {
                    bedNote = "REFUSED";
                }

                if (bed == null)
                {
                    _output.WriteLine(Inv($"  {depth,5:0.}  {bedNote,-9}  (no streams built)"));
                    continue;
                }

                Probe p = Measure(depth, Seeds[0], bed);

                string axes = p.Built
                    ? FormattableString.Invariant($"{p.RmsX:0.00000} / {p.RmsY:0.00000} / {p.RmsZ:0.00000}")
                    : "not built";

                string beta = FormattableString.Invariant($"{p.Beta:0.0000}");

                _output.WriteLine(
                    Inv($"  {depth,5:0.}  {bedNote,-9}  {(p.A > 1e-12 ? "yes" : "NO "),-7}  ") +
                    Inv($"{p.A,9:0.000000;-0.000000}  {beta,8}  {axes}"));
            }

            _output.WriteLine("");
            _output.WriteLine(
                "  BedShape itself refuses relief + tilt/2 >= depth - 1, which is depth <= 17.5 m here.");
            _output.WriteLine(
                "  Where the bed is accepted, 'a' is the flat field's and identical to the table above.");
        }

        /// <summary>
        /// Three seeds, two depths: whether the branch is the geometry's or the draw's.
        /// </summary>
        [Fact]
        public void TheSeedDoesNotDecideTheBranch()
        {
            _output.WriteLine("  depth  seed   a           b            k_max   balance");

            foreach (float depth in new[] { 45f, 25f, 20f })
            {
                foreach (ulong seed in Seeds)
                {
                    Probe p = Measure(depth, seed, bed: null);

                    _output.WriteLine(
                        Inv($"  {depth,5:0.}  {seed,4}   {p.A,10:0.0000000;-0.0000000}  ") +
                        Inv($"{p.B,11:0.00e+00;-0.00e+00}  {p.KMax,6:0.000}  {(p.A > 1e-12 ? "yes" : "NO")}"));
                }
            }

            _output.WriteLine("");
            _output.WriteLine("");
            _output.WriteLine(
                "  The seed moves a in the fourth decimal and k_max in the third, and it never");
            _output.WriteLine(
                "  moves the verdict: depth 20 takes the relaxed branch on every seed. b = -C is the cross term");
            _output.WriteLine(
                "  between the streams and the overturning and is zero analytically — the streams");
            _output.WriteLine(
                "  all carry m >= 1 in theta and the overturning carries m = 0 — so what is printed");
            _output.WriteLine(
                "  is the construction lattice's residue, 1e-8 against a c0 of -0.5.");
        }

        /// <summary>
        /// What each candidate relaxation gives, from the coefficients alone — rule A is the one
        /// ruled on, and rule B is what was not chosen.
        /// </summary>
        [Fact]
        public void TheCandidateRulesAreTabled()
        {
            _output.WriteLine(
                "  Rule A: k = min(1, lambda * k_max), lambda = 0.76, then solve the same quadratic");
            _output.WriteLine(
                "          with a_k = V - k^2*H1/2, b_k = -k^2*C, c_k = -k^2*H0/2. This is the");
            _output.WriteLine(
                "          owner's ruling of 2026-09-21 and what CurrentField now runs.");
            _output.WriteLine(
                "  Rule B: k = min(1, D/R), the shelf's own aspect ratio, same quadratic.");
            _output.WriteLine("");
            _output.WriteLine(
                "  depth  k_max   ruleA k  ruleA beta  ruleA y/x  |  ruleB k  ruleB beta  ruleB y/x  " +
                "|  total RMS held?");

            foreach (float depth in Depths)
            {
                Probe p = Measure(depth, Seeds[0], bed: null);

                double kA = Math.Min(1d, Lambda * p.KMax);
                double kB = Math.Min(1d, depth / Radius);

                (double betaA, double ratioA, double rmsA) = SolveAt(p, kA);
                (double betaB, double ratioB, double rmsB) = SolveAt(p, kB);

                _output.WriteLine(
                    Inv($"  {depth,5:0.}  {p.KMax,6:0.000}  {kA,7:0.0000}  {betaA,10:0.0000}  ") +
                    Inv($"{ratioA,9:0.000}  |  {kB,7:0.0000}  {betaB,10:0.0000}  {ratioB,9:0.000}  ") +
                    Inv($"|  {rmsA,5:0.0000} / {rmsB:0.0000}"));
            }

            _output.WriteLine("");
            _output.WriteLine(
                "  'y/x' is the vertical RMS over the per-axis horizontal RMS after normalisation.");
            _output.WriteLine(
                "  'total RMS held?' is the whole field's RMS at knob 1 under each rule — it is 1 by");
            _output.WriteLine(
                "  construction for any beta, because _streamsScale is measured after beta is chosen.");
            _output.WriteLine("");
            _output.WriteLine(
                "  At every depth where the balance has a root, k_max > 1 so both rules return k = 1");
            _output.WriteLine(
                "  and the quadratic is the balanced one, coefficient for coefficient — which is why");
            _output.WriteLine(
                "  the relaxation reaches no tank that was ever recorded.");
        }

        /// <summary>
        /// The relaxed shallow tank as the field now builds it, measured rather than predicted.
        /// </summary>
        /// <remarks>
        /// <b>The field's own numbers, not the probe's.</b> Until the ruling this read an
        /// amplitude the probe injected by reflection, because the constructor refused a tank this
        /// flat; it now reads what <c>BuildStreams</c> chose, through the public sampler, on a
        /// lattice of the probe's own. Depth 25 is the control at the other end: it balances, so
        /// its target is 1 and nothing about it has changed.
        /// </remarks>
        [Fact]
        public void TheRelaxedShallowTankIsMeasured()
        {
            _output.WriteLine(
                "  depth  target  beta      predicted y/x   measured x / y / z (m/s)      " +
                "measured y/x   total RMS   bound (m/s)  substeps at 1 m / 0.5 s");

            foreach (float depth in new[] { 25f, 20f, 15f })
            {
                Probe p = Measure(depth, Seeds[0], bed: null);

                CurrentField field = Field(depth, Seeds[0], bed: null);
                _ = field.StreamsComponentRms;

                (double x, double y, double z) = PerAxisRms(field, depth, bed: null);

                double horizontal = Math.Sqrt(0.5d * (x * x + z * z));
                double total = Math.Sqrt(x * x + y * y + z * z);

                double bound = field.MaximumTransportSpeed;
                double courant = bound * 0.5d / 1d;
                int substeps = courant <= 0.5d ? 1 : (int)Math.Ceiling(2d * courant);

                _output.WriteLine(
                    Inv($"  {depth,5:0.}  {field.StreamsAxisRatio,6:0.0000}  {p.Beta,8:0.0000}  {p.PredictedRatio,13:0.000}   ") +
                    Inv($"{x:0.00000} / {y:0.00000} / {z:0.00000}   {y / horizontal,12:0.000}   ") +
                    Inv($"{total,9:0.00000}   {bound,11:0.0000}  {substeps,4}"));

                // The knob is 0.1 m/s and this lattice reads a couple of per cent high on any
                // tank, as BedStreamsTests records; what matters here is that the relaxation does
                // not move it, so the tolerance is the lattice's and not the water's.
                Assert.Equal(0.1d, total, 0.01d);

                // And the field aimed where the probe's own arithmetic says it should have.
                Assert.Equal(p.Ratio, field.StreamsAxisRatio, 1e-12);
            }

            _output.WriteLine("");
            _output.WriteLine(
                "  Depth 25 balances: its target is 1 and its water is the one it always was.");
            _output.WriteLine(
                "  The measured y/x runs a few per cent under the predicted because this lattice");
            _output.WriteLine(
                "  is coarser than the construction's (19x13x31 against 22x16x64) and reads the");
            _output.WriteLine(
                "  vertical low on a tank of any depth — 0.967 at depth 45, where the construction");
            _output.WriteLine("  fitted exactly 1.000.");
            _output.WriteLine("");
            _output.WriteLine(
                "  'bound' is the field's own MaximumTransportSpeed, which is what");
            _output.WriteLine(
                "  GridField.CourantSubsteps refuses on; its ceiling is 8 substeps.");
        }

        /// <summary>
        /// The ceiling on the ratio is <c>k_max = c * D / R</c> with one number <c>c</c>, whatever
        /// the tank — which is what makes the relaxation a closed form rather than a fit.
        /// </summary>
        /// <remarks>
        /// Every stream's radial factor is a function of <c>s = r/R</c> and every vertical factor
        /// a function of <c>y/D</c>, and the overturning's radial component alone carries a
        /// dimensional factor, <c>R*q*pi/D</c>. So <c>H1</c> scales as <c>(R/D)²</c> and <c>V</c>
        /// not at all, and <c>k_max = sqrt(2V/H1)</c> is proportional to <c>D/R</c> with a
        /// constant that belongs to the mode set. The construction lattice is itself in
        /// <c>s</c> and <c>y/D</c>, so the scaling is exact rather than approximate.
        /// </remarks>
        [Fact]
        public void TheCeilingIsOneConstantTimesDepthOverRadius()
        {
            _output.WriteLine("  area (m2)  radius    depth   H1        V         k_max    k_max*R/D");

            foreach (float area in new[] { 100f, 400f, 2200f })
            {
                foreach (float depth in new[] { 60f, 30f })
                {
                    float radius = TankGeometry.RadiusFor(area);
                    Probe p = Measure(depth, Seeds[0], bed: null, area: area, measure: false);

                    _output.WriteLine(
                        Inv($"  {area,9:0.}  {radius,7:0.000}  {depth,6:0.}  {p.H1,8:0.00000}  ") +
                        Inv($"{p.V,8:0.00000}  {p.KMax,7:0.000}  {p.KMax * radius / depth,9:0.0000}"));
                }
            }

            _output.WriteLine("");
            _output.WriteLine(
                "  The last column is the constant: one number across a 22x range of area and a 2x");
            _output.WriteLine(
                "  range of depth. The critical depth is therefore D = R / c, below which the");
            _output.WriteLine("  balance has no root and the field is built at lambda * k_max instead.");
        }

        // ---------------------------------------------------------------- the machinery

        private sealed class Probe
        {
            public bool Built;
            public double A, B, C0;
            public double H0, H1, C, V;
            public double KMax;
            public double Beta, FieldBeta;

            /// <summary>The target the rule aimed at: 1 where the balance has a root, λ·k_max otherwise.</summary>
            public double Ratio;

            public double PredictedRatio;
            public double RmsX, RmsY, RmsZ;
        }

        /// <summary>
        /// Builds the streams for one depth, recovers the balance coefficients whether or not the
        /// build succeeded, and measures the field where it exists.
        /// </summary>
        // xUnit makes a new instance per test and each build of the streams costs a second —
        // most of it the Courant pass's 737,000 samples. The results are pure functions of
        // (depth, seed, area, bed), so they are held here across the class's tests.
        private static readonly Dictionary<string, Probe> Cache = new Dictionary<string, Probe>();

        private Probe Measure(float depth, ulong seed, BedShape bed, float area = Area, bool measure = true)
        {
            string key = Inv($"{depth}|{seed}|{area}|{(bed == null ? "flat" : "bed")}|{measure}");

            lock (Cache)
            {
                if (Cache.TryGetValue(key, out Probe cached)) return cached;
            }

            Probe built = Build(depth, seed, bed, area, measure);

            lock (Cache)
            {
                Cache[key] = built;
            }

            return built;
        }

        /// <summary>One tank's water, at this class's knob and period.</summary>
        private static CurrentField Field(float depth, ulong seed, BedShape bed, float area = Area)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = Speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(area / Rings), Rings, depth,
                Rng.SeedFor(seed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank,
                tankRadiusMetres: TankGeometry.RadiusFor(area), bed: bed);

            return field;
        }

        private Probe Build(float depth, ulong seed, BedShape bed, float area, bool measure)
        {
            float radius = TankGeometry.RadiusFor(area);
            CurrentField field = Field(depth, seed, bed, area);

            var p = new Probe();

            try
            {
                // Touching this property is what runs EnsureStreams, and therefore BuildStreams.
                _ = field.StreamsComponentRms;
                p.Built = true;
                p.FieldBeta = (double)Private(field, "_streamsOverturning");
            }
            catch (InvalidOperationException)
            {
                p.Built = false;
            }

            SecondPass(field, p);

            if (p.Built && measure)
            {
                (p.RmsX, p.RmsY, p.RmsZ) = PerAxisRms(field, depth, bed, radius);
            }

            return p;
        }

        /// <summary>
        /// BuildStreams's second measurement pass, re-run through the same private members.
        /// </summary>
        /// <remarks>
        /// Everything this reads is already on the object whether the build threw or not: the
        /// term arrays and the phases are filled before the first pass, and
        /// <c>_streamsEddyWeight</c> is set by it. The envelope override is restored to 0 before
        /// returning, exactly as the production method does on every exit, so a field this probe
        /// touched is the field a caller would have got.
        /// </remarks>
        private static void SecondPass(CurrentField field, Probe p)
        {
            MethodInfo walkInfo = typeof(CurrentField).GetMethod(
                "Walk", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo unitInfo = typeof(CurrentField).GetMethod(
                "StreamsUnit", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(walkInfo);
            Assert.NotNull(unitInfo);

            var walk = (Func<Action<float, float, float, double>, int>)walkInfo.CreateDelegate(
                typeof(Func<Action<float, float, float, double>, int>), field);
            var unit = (Func<double, double, double, double, double, Float3>)unitInfo.CreateDelegate(
                typeof(Func<double, double, double, double, double, Float3>), field);

            SetPrivate(field, "_envelopeOverride", EnvelopeRms);

            double h0 = 0d, h1 = 0d, cross = 0d, vertical = 0d;

            int counted = walk((x, y, z, t) =>
            {
                Float3 rest = unit(x, y, z, t, 0d);
                Float3 whole = unit(x, y, z, t, 1d);

                double overX = whole.X - rest.X;
                double overZ = whole.Z - rest.Z;

                h0 += (double)rest.X * rest.X + (double)rest.Z * rest.Z;
                h1 += overX * overX + overZ * overZ;
                cross += rest.X * overX + rest.Z * overZ;
                vertical += (double)whole.Y * whole.Y;
            });

            SetPrivate(field, "_envelopeOverride", 0d);

            p.H0 = h0 / counted;
            p.H1 = h1 / counted;
            p.C = cross / counted;
            p.V = vertical / counted;

            p.A = (vertical - 0.5d * h1) / counted;
            p.B = -cross / counted;
            p.C0 = -0.5d * h0 / counted;

            p.KMax = Math.Sqrt(2d * p.V / p.H1);

            // The production rule of 2026-09-21, both branches: the balance where it has a root,
            // and λ times the ceiling where it has none. The relaxed branch's coefficients are
            // the balanced ones with k² on three of the four terms, so at k = 1 this is the line
            // the field has always run.
            if (p.A > 1e-12)
            {
                p.Ratio = 1d;
                p.Beta = (-p.B + Math.Sqrt(p.B * p.B - 4d * p.A * p.C0)) / (2d * p.A);
                p.PredictedRatio = RatioAt(p, p.Beta);
            }
            else
            {
                p.Ratio = Lambda * p.KMax;

                double a = p.V - p.Ratio * p.Ratio * p.H1 / 2d;
                double b = -p.Ratio * p.Ratio * p.C;
                double c = -p.Ratio * p.Ratio * p.H0 / 2d;

                p.Beta = (-b + Math.Sqrt(b * b - 4d * a * c)) / (2d * a);
                p.PredictedRatio = RatioAt(p, p.Beta);
            }
        }

        /// <summary>The vertical RMS over the per-axis horizontal RMS at a given amplitude.</summary>
        private static double RatioAt(Probe p, double beta) =>
            Math.Sqrt(2d * beta * beta * p.V / (p.H0 + 2d * beta * p.C + beta * beta * p.H1));

        /// <summary>
        /// The amplitude a target ratio asks for, and what it delivers — the same quadratic with
        /// <c>k</c> in it, which is today's at <c>k = 1</c>.
        /// </summary>
        private static (double Beta, double Ratio, double TotalRms) SolveAt(Probe p, double k)
        {
            double a = p.V - k * k * p.H1 / 2d;
            double b = -k * k * p.C;
            double c = -k * k * p.H0 / 2d;

            if (!(a > 1e-12)) return (double.NaN, double.NaN, double.NaN);

            double beta = (-b + Math.Sqrt(b * b - 4d * a * c)) / (2d * a);

            // The scale the field would then measure, and the RMS it delivers at knob 1 — which is
            // 1 whatever beta is, because the scale is fitted after beta.
            double meanSquare = p.H0 + 2d * beta * p.C + beta * beta * (p.H1 + p.V);
            double scale = 1d / Math.Sqrt(meanSquare);
            double total = scale * Math.Sqrt(meanSquare);

            return (beta, RatioAt(p, beta), total);
        }

        /// <summary>
        /// The per-axis RMS over the live water, on a lattice of this probe's own and through the
        /// public sampler — a second opinion on what the construction fitted.
        /// </summary>
        private static (double X, double Y, double Z) PerAxisRms(
            CurrentField field, float depth, BedShape bed, float radius = 0f)
        {
            if (radius <= 0f) radius = Radius;

            const int Horizontal = 19;
            const int Vertical = 13;
            const int Instants = 31;

            double sx = 0d, sy = 0d, sz = 0d, weight = 0d;

            for (int p = 0; p < Instants; p++)
            {
                double t = p * Period * 1.7320508075688772d / Instants;

                for (int ix = 0; ix < Horizontal; ix++)
                {
                    float x = (float)((ix + 0.5) * 2d * radius / Horizontal);

                    for (int iz = 0; iz < Horizontal; iz++)
                    {
                        float z = (float)((iz + 0.5) * 2d * radius / Horizontal);
                        if (!TankGeometry.Inside(x, z, radius)) continue;

                        double d = bed == null ? depth : -bed.FloorY(x, z);
                        if (!(d > 0d)) continue;

                        double w = d / depth;

                        for (int iy = 0; iy < Vertical; iy++)
                        {
                            float y = -(float)((iy + 0.5) * d / Vertical);

                            Float3 v = field.VelocityAt(x, y, z, t);

                            sx += w * (double)v.X * v.X;
                            sy += w * (double)v.Y * v.Y;
                            sz += w * (double)v.Z * v.Z;
                            weight += w;
                        }
                    }
                }
            }

            return (Math.Sqrt(sx / weight), Math.Sqrt(sy / weight), Math.Sqrt(sz / weight));
        }

        /// <summary>Invariant formatting, one interpolated literal at a time.</summary>
        private static string Inv(FormattableString s) => FormattableString.Invariant(s);

        private static object Private(CurrentField field, string name) =>
            typeof(CurrentField)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(field);

        private static void SetPrivate(CurrentField field, string name, object value) =>
            typeof(CurrentField)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(field, value);
    }
}
