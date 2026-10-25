using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D234: under D228's mirrored-joint switch a mirrored twin's joint drive is reflected with its geometry, so that
    /// one brain signal bends and beats a pair as mirror images rather than in a seesaw.
    /// </summary>
    /// <remarks>
    /// The mirror plane is the root's: the root stands at the origin of its own frame, so the plane through its
    /// centre with the world's x for a normal is the plane the pair is reflected in. A twin's pose is the reflection of
    /// its original's when its centre is reflected and its rotation is <c>F R F</c> with <c>F = diag(-1, 1, 1)</c>.
    /// </remarks>
    public sealed class MirroredDriveTests
    {
        private readonly ITestOutputHelper _out;

        public MirroredDriveTests(ITestOutputHelper output) => _out = output;

        private static readonly DevelopmentLimits Reflecting = new DevelopmentLimits { MaxParts = 64, ReflectMirroredAnchors = true };
        private static readonly DevelopmentLimits Plain = new DevelopmentLimits { MaxParts = 64 };

        private static readonly int[][] OnePair = { new[] { 1, 2 } };

        // root, a, b, a', b': a and b under the original, their twins under the mirrored copy.
        private static readonly int[][] TwoPairs = { new[] { 1, 3 }, new[] { 2, 4 } };

        // ------------------------------------------------------------------ the worlds and the bodies

        private static SolverConfig Torque() => TestBodies.Water(0.01);

        private static SolverConfig Power()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = 0.3;
            config.PowerPushRadiansPerSecond = 6.0;
            config.PoseHertz = 2.0;
            return config;
        }

        private static NeuronDef Wave(float hz, float phase) => new NeuronDef
        {
            Op = NeuronOp.OscillateWave, Frequency = hz, Phase = phase, Amplitude = 1f, Bias = 0f,
            Inputs = Array.Empty<NeuronInput>(),
        };

        // A sum of nothing plus a bias: a constant, which an oscillator is not.
        private static NeuronDef Constant(float value) => new NeuronDef
        {
            Op = NeuronOp.Sum, Frequency = 0f, Phase = 0f, Amplitude = 1f, Bias = value,
            Inputs = Array.Empty<NeuronInput>(),
        };

        // The neurons of a limb: one oscillator a degree of freedom for the drive, and under the bend-and-power
        // drive a second set for the bend. Stops that are not symmetric, so that swapping them is seen.
        private static void Fit(MorphNode node, JointType joint, int depth, bool bendAndPower)
        {
            int dof = joint.DofCount();
            var neurons = new List<NeuronDef>();

            for (int d = 0; d < dof; d++)
            {
                node.JointLimits[d] = new Float2(-0.5f - 0.1f * d, 1.0f - 0.1f * d);
                neurons.Add(Wave(0.7f + 0.2f * d, 0.4f * depth + 1.1f * d));
            }

            if (bendAndPower)
            {
                for (int d = 0; d < dof; d++) neurons.Add(Wave(0.5f + 0.15f * d, 2.0f * depth + 0.8f * d));
            }

            node.Neurons = neurons.ToArray();
        }

        // A chain of <links> parts along +x whose first edge reflects in x: the root, the original limb and the twin.
        private static Genome PairGenome(JointType joint, int links, bool bendAndPower)
        {
            Genome genome = TestBodies.Chain(links, joint, power: 8f, hz: 0.7f);
            for (int n = 1; n < links; n++) Fit(genome.Nodes[n], joint, n, bendAndPower);
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);
            return genome;
        }

        // The same, with every anchor off every axis and every edge turned, so the mirror is not along a face.
        private static Genome OffAxisGenome(JointType joint, bool bendAndPower)
        {
            Genome genome = TestBodies.Chain(3, joint, power: 8f, hz: 0.7f, linkHalf: new Float3(0.2f, 0.15f, 0.2f));
            for (int n = 1; n < 3; n++) Fit(genome.Nodes[n], joint, n, bendAndPower);

            genome.Nodes[0].Edges[0].ParentAnchor = new Float3(0.8f, 0.5f, 0.4f);
            genome.Nodes[0].Edges[0].ChildAnchor = new Float3(-0.7f, 0.3f, -0.2f);
            genome.Nodes[0].Edges[0].Orientation = Quat.FromEuler(0.3f, 0.5f, -0.4f);
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);

            genome.Nodes[1].Edges[0].ParentAnchor = new Float3(-0.6f, 0.4f, 0.5f);
            genome.Nodes[1].Edges[0].ChildAnchor = new Float3(0.5f, -0.3f, 0.2f);
            genome.Nodes[1].Edges[0].Orientation = Quat.FromEuler(-0.2f, 0.1f, 0.6f);

            return genome;
        }

        private static (Creature body, DynamicsWorld world) Make(
            Genome genome, DevelopmentLimits limits, SolverConfig config, bool dropReflectedDrive = false)
        {
            Phenotype adult = Developer.Develop(genome, limits);

            if (dropReflectedDrive)
            {
                // The geometry of D228 with the drive of before D234: what the build found, a seesaw.
                MethodInfo set = typeof(PhenotypePart).GetProperty("ReflectedDrive").GetSetMethod(true);
                foreach (PhenotypePart part in adult.Parts) set.Invoke(part, new object[] { false });
            }

            var body = new Creature(0, adult, config);
            body.PlaceAsDeveloped(new Vec3(0, -10, 0), adult);

            var world = new DynamicsWorld(config);
            world.Add(body);
            return (body, world);
        }

        // ------------------------------------------------------------------ the measures

        // How far the twin's pose stands from the reflection of its original's in the plane through the root's
        // centre: the centre reflected in x, and the rotation matrix against F R F, where an entry changes sign
        // exactly when one of its two indices is x.
        private static double MirrorError(Creature body, int original, int twin)
        {
            double plane = body.Position[0];
            double worst = Math.Abs(body.Position[3 * twin] - (2 * plane - body.Position[3 * original]));
            worst = Math.Max(worst, Math.Abs(body.Position[3 * twin + 1] - body.Position[3 * original + 1]));
            worst = Math.Max(worst, Math.Abs(body.Position[3 * twin + 2] - body.Position[3 * original + 2]));

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    double sign = (r == 0) != (c == 0) ? -1.0 : 1.0;
                    double want = sign * body.RotationMatrix[9 * original + 3 * r + c];
                    worst = Math.Max(worst, Math.Abs(body.RotationMatrix[9 * twin + 3 * r + c] - want));
                }
            }

            return worst;
        }

        // The far end of a limb, opposite its joint: its centre less its turned child anchor.
        private static Vec3 Tip(Creature body, int link)
        {
            double ax = body.ChildAnchor[3 * link], ay = body.ChildAnchor[3 * link + 1], az = body.ChildAnchor[3 * link + 2];
            int o = 9 * link;
            double[] m = body.RotationMatrix;
            return new Vec3(
                body.Position[3 * link] - (m[o] * ax + m[o + 1] * ay + m[o + 2] * az),
                body.Position[3 * link + 1] - (m[o + 3] * ax + m[o + 4] * ay + m[o + 5] * az),
                body.Position[3 * link + 2] - (m[o + 6] * ax + m[o + 7] * ay + m[o + 8] * az));
        }

        private static string Show(Vec3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.####}, {1:0.####}, {2:0.####})", v.X, v.Y, v.Z);

        private struct Followed
        {
            public double Worst;      // the largest mirror error over the run
            public double Travel;     // the most the original's pose moved from where it started
            public double AtStart;    // the mirror error before the first step
        }

        // Steps the world <steps> times and reads every pair after every step. <log> prints the first pair's tips
        // every <every> steps.
        private Followed Follow(Creature body, DynamicsWorld world, int[][] pairs, int steps, string label, int every = 0)
        {
            var followed = new Followed();

            double[] start = new double[12];
            int first = pairs[0][0];
            for (int k = 0; k < 3; k++) start[k] = body.Position[3 * first + k];
            for (int k = 0; k < 9; k++) start[3 + k] = body.RotationMatrix[9 * first + k];

            foreach (int[] pair in pairs) followed.AtStart = Math.Max(followed.AtStart, MirrorError(body, pair[0], pair[1]));

            for (int step = 1; step <= steps; step++)
            {
                world.Step();
                Assert.True(body.Alive, label + ": the body was lost at step " + step);

                foreach (int[] pair in pairs) followed.Worst = Math.Max(followed.Worst, MirrorError(body, pair[0], pair[1]));

                double moved = 0;
                for (int k = 0; k < 3; k++) moved = Math.Max(moved, Math.Abs(body.Position[3 * first + k] - start[k]));
                for (int k = 0; k < 9; k++) moved = Math.Max(moved, Math.Abs(body.RotationMatrix[9 * first + k] - start[3 + k]));
                followed.Travel = Math.Max(followed.Travel, moved);

                if (every > 0 && step % every == 0)
                {
                    _out.WriteLine(string.Format(
                        CultureInfo.InvariantCulture, "{0} t={1:0.00}s  original tip {2}  twin tip {3}  mirror error {4:0.#####}",
                        label, step * 0.01, Show(Tip(body, pairs[0][0])), Show(Tip(body, pairs[0][1])),
                        MirrorError(body, pairs[0][0], pairs[0][1])));
                }
            }

            return followed;
        }

        // ------------------------------------------------------------------ the tests

        public static IEnumerable<object[]> EveryJointAndDrive()
        {
            foreach (JointType joint in new[]
            {
                JointType.Hinge, JointType.Twist, JointType.HingeTwist, JointType.TwistHinge,
                JointType.Universal, JointType.Spherical,
            })
            {
                yield return new object[] { joint, false };
                yield return new object[] { joint, true };
            }
        }

        /// <summary>
        /// A root on the mirror plane and one mirrored pair, driven by one signal for ten seconds with the switch on:
        /// at every step the twin's centre is the reflection of its original's and its rotation is F R F, within 1e-9.
        /// Each joint type has its own degrees of freedom and so its own signs, and each is tried under the torque drive
        /// and under the bend-and-power drive, whose limb push is quadratic in the stroke and needs none.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryJointAndDrive))]
        public void ATwinMovesAsTheMirrorImageOfItsOriginalUnderOneSignal(JointType joint, bool bendAndPower)
        {
            SolverConfig config = bendAndPower ? Power() : Torque();
            (Creature body, DynamicsWorld world) = Make(PairGenome(joint, 2, bendAndPower), Reflecting, config);

            Assert.Equal(3, body.Links);
            Assert.True(body.Phenotype.Parts[2].Mirrored);
            Assert.Equal(bendAndPower, body.Drive.BendAndPower);

            Followed followed = Follow(body, world, OnePair, 1000, joint + (bendAndPower ? " bend-and-power" : " torque"), 200);
            _out.WriteLine($"{joint}, bend-and-power {bendAndPower}: mirror error {followed.AtStart:0.#e+0} at the start and {followed.Worst:0.#e+0} at worst; the original moved {followed.Travel:0.###}");

            Assert.True(followed.Travel > 0.02, $"the original moved only {followed.Travel:0.######}, so nothing was tested");
            Assert.True(followed.AtStart < 1e-9, $"the pair does not start as mirror images: {followed.AtStart:0.#e+0}");
            Assert.True(followed.Worst < 1e-9, $"the twin left the mirror image of its original by {followed.Worst:0.#e+0}");

            // The degrees of freedom themselves: the twin's stored angle is the original's with the mask's signs.
            int original = body.DofStart[1], twin = body.DofStart[2];
            for (int d = 0; d < joint.DofCount(); d++)
            {
                double want = body.DofSign(2, d) * body.Q[original + d];
                Assert.True(
                    Math.Abs(body.Q[twin + d] - want) < 1e-9,
                    $"degree of freedom {d} of the twin stands at {body.Q[twin + d]:0.######} where {want:0.######} mirrors its original");
            }
        }

        /// <summary>
        /// Two pairs, the second hanging from the first, the second pair's parent a mirrored part: the twins of the
        /// second pair are mirror images of theirs too, since the mirrored branch is a reflection of the whole branch.
        /// </summary>
        [Theory]
        [InlineData(JointType.Hinge, false)]
        [InlineData(JointType.Universal, true)]
        [InlineData(JointType.Spherical, false)]
        public void ATwinUnderAMirroredParentMovesAsAMirrorImageToo(JointType joint, bool bendAndPower)
        {
            SolverConfig config = bendAndPower ? Power() : Torque();
            (Creature body, DynamicsWorld world) = Make(PairGenome(joint, 3, bendAndPower), Reflecting, config);

            Assert.Equal(5, body.Links);
            Assert.True(body.Phenotype.Parts[3].Mirrored && body.Phenotype.Parts[4].Mirrored);

            Followed followed = Follow(body, world, TwoPairs, 1000, joint + " two pairs");
            _out.WriteLine($"{joint}, bend-and-power {bendAndPower}: mirror error {followed.Worst:0.#e+0} at worst; the original moved {followed.Travel:0.###}");

            Assert.True(followed.Travel > 0.02, $"the original moved only {followed.Travel:0.######}, so nothing was tested");
            Assert.True(followed.Worst < 1e-9, $"a twin left the mirror image of its original by {followed.Worst:0.#e+0}");
        }

        /// <summary>
        /// The same with the anchors off every axis and the edges turned. The attitudes are float quaternions that
        /// development rounds, so the pose at the start already stands 1e-7 from a mirror image, and the bound is 1e-5.
        /// </summary>
        [Theory]
        [InlineData(JointType.Hinge, false)]
        [InlineData(JointType.Hinge, true)]
        [InlineData(JointType.Universal, false)]
        [InlineData(JointType.Spherical, true)]
        public void ATwinOfAnOffAxisBodyMovesAsAMirrorImageToo(JointType joint, bool bendAndPower)
        {
            SolverConfig config = bendAndPower ? Power() : Torque();
            (Creature body, DynamicsWorld world) = Make(OffAxisGenome(joint, bendAndPower), Reflecting, config);

            Followed followed = Follow(body, world, TwoPairs, 1000, joint + " off axis");
            _out.WriteLine($"{joint}, bend-and-power {bendAndPower}: mirror error {followed.AtStart:0.#e+0} at the start and {followed.Worst:0.#e+0} at worst; the original moved {followed.Travel:0.###}");

            Assert.True(followed.Travel > 0.02, $"the original moved only {followed.Travel:0.######}, so nothing was tested");
            Assert.True(followed.Worst < 1e-5, $"a twin left the mirror image of its original by {followed.Worst:0.#e+0}");
        }

        /// <summary>
        /// What the same body does without the rule. With D228's anchors alone the twin starts as the mirror image and
        /// then seesaws: the same signal turns it the same way about the mirror image of its axis, one tip going up
        /// while the other goes down. With the switch off altogether the twin stands on its original, so it is not a
        /// mirror image from the first step. Both tips' paths are written to the output.
        /// </summary>
        [Fact]
        public void WithoutTheDriveRuleTheSameBodySeesawsAndWithoutTheSwitchItIsStacked()
        {
            Genome genome = PairGenome(JointType.Hinge, 2, false);

            (Creature reflected, DynamicsWorld reflectedWorld) = Make(genome, Reflecting, Torque());
            Followed mirrored = Follow(reflected, reflectedWorld, OnePair, 1000, "drive reflected", 200);

            (Creature seesaw, DynamicsWorld seesawWorld) = Make(genome, Reflecting, Torque(), dropReflectedDrive: true);
            Assert.Equal(0, seesaw.MirrorMask[2]);
            Followed seesawed = Follow(seesaw, seesawWorld, OnePair, 1000, "anchors only (seesaw)", 200);

            (Creature unswitched, DynamicsWorld unswitchedWorld) = Make(genome, Plain, Torque());
            Followed stacked = Follow(unswitched, unswitchedWorld, OnePair, 1000, "switch off (stacked)", 200);

            _out.WriteLine($"mirror error at the start / at worst: drive reflected {mirrored.AtStart:0.#e+0} / {mirrored.Worst:0.#e+0}; anchors only {seesawed.AtStart:0.#e+0} / {seesawed.Worst:0.######}; switch off {stacked.AtStart:0.######} / {stacked.Worst:0.######}");

            Assert.True(mirrored.Worst < 1e-9);

            // The anchors alone: a mirror image at the start, a seesaw once the signal arrives.
            Assert.True(seesawed.AtStart < 1e-9, $"the pair does not start as mirror images: {seesawed.AtStart:0.#e+0}");
            Assert.True(seesawed.Worst > 0.05, $"the twin stayed within {seesawed.Worst:0.######} of the mirror image, so it did not seesaw");

            // The switch off: the twin is on its original, 0.8 m from where its mirror image stands.
            Assert.True(stacked.AtStart > 0.5, $"the unreflected twin stands only {stacked.AtStart:0.######} from the mirror image of its original");
        }

        /// <summary>A mirrored hinge stops at the negated stops: [-hi, -lo] where its original stops at [lo, hi].</summary>
        [Theory]
        [InlineData(false, 1f)]
        [InlineData(false, -1f)]
        [InlineData(true, 1f)]
        [InlineData(true, -1f)]
        public void AMirroredHingeStopsAtTheNegatedStops(bool bendAndPower, float push)
        {
            SolverConfig config = bendAndPower ? Power() : Torque();
            Genome genome = TestBodies.Chain(2, JointType.Hinge, power: 8f, limit: 1f);
            genome.Nodes[1].JointLimits[0] = new Float2(-0.5f, 1.0f);

            // Held on one stop for ever: a constant torque, or a constant bend of all the way up or all the way down.
            genome.Nodes[1].Neurons = bendAndPower ? new[] { Constant(0f), Constant(push) } : new[] { Constant(push) };
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);

            (Creature body, DynamicsWorld world) = Make(genome, Reflecting, config);
            Assert.Equal(3, body.Links);

            int original = body.DofStart[1], twin = body.DofStart[2];

            // As stored: the original's stops and their mirror image.
            Assert.Equal(-0.5, body.LimitLo[original]);
            Assert.Equal(1.0, body.LimitHi[original]);
            Assert.Equal(-1.0, body.LimitLo[twin]);
            Assert.Equal(0.5, body.LimitHi[twin]);

            for (int step = 0; step < 800; step++) world.Step();
            Assert.True(body.Alive);

            double stop = push > 0 ? 1.0 : -0.5;
            _out.WriteLine($"bend-and-power {bendAndPower}, push {push}: the original rests at {body.Q[original]:0.#####} and the twin at {body.Q[twin]:0.#####}; the stop is {stop}");

            // A penalty stop is passed by a little, and the muscle settles a little short of its target
            // (BendAndPowerTests.TheMuscleHoldsTheJointAtItsBend allows it 15%), so the bound is wider there.
            double tolerance = bendAndPower ? 0.15 * Math.Abs(stop) : 0.03;
            Assert.True(Math.Abs(body.Q[original] - stop) < tolerance, $"the original rests at {body.Q[original]:0.#####}, not at its stop {stop}");
            Assert.True(Math.Abs(body.Q[twin] + stop) < tolerance, $"the twin rests at {body.Q[twin]:0.#####}, not at the negated stop {-stop}");

            // The mirror image to the last digits, whichever way the drive settles.
            Assert.True(Math.Abs(body.Q[twin] + body.Q[original]) < 1e-6, $"the twin stands at {body.Q[twin]:0.#######} where the mirror image of {body.Q[original]:0.#######} stands");
        }

        /// <summary>
        /// Twins that move as mirror images report the same angle and rate to the brain. A neuron that reads every
        /// degree of freedom's angle and rate asks the senses for them.
        /// </summary>
        [Theory]
        [InlineData(JointType.Hinge)]
        [InlineData(JointType.Universal)]
        [InlineData(JointType.Spherical)]
        public void TwinsMovingAsMirrorImagesReportTheSameAngleAndRateToTheBrain(JointType joint)
        {
            SolverConfig config = Torque();
            Genome genome = PairGenome(joint, 2, false);
            var neurons = new List<NeuronDef>(genome.Nodes[1].Neurons);
            for (int d = 0; d < joint.DofCount(); d++)
            {
                foreach (SensorChannel channel in new[] { SensorChannel.JointAngle, SensorChannel.JointAngularVelocity })
                {
                    neurons.Add(new NeuronDef
                    {
                        Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
                        Inputs = new[] { NeuronInput.FromSensor(channel, d) },
                    });
                }
            }

            genome.Nodes[1].Neurons = neurons.ToArray();

            (Creature body, DynamicsWorld world) = Make(genome, Reflecting, config);

            double biggestAngle = 0, biggestRate = 0, worst = 0, physicallyApart = 0;

            for (int step = 0; step < 1000; step++)
            {
                world.Step();

                if (step % 20 != 0) continue;

                body.Senses.Sample();

                for (int d = 0; d < joint.DofCount(); d++)
                {
                    float angleOriginal = body.Senses.Read(1, SensorChannel.JointAngle, d);
                    float angleTwin = body.Senses.Read(2, SensorChannel.JointAngle, d);
                    float rateOriginal = body.Senses.Read(1, SensorChannel.JointAngularVelocity, d);
                    float rateTwin = body.Senses.Read(2, SensorChannel.JointAngularVelocity, d);

                    worst = Math.Max(worst, Math.Max(Math.Abs(angleOriginal - angleTwin), Math.Abs(rateOriginal - rateTwin)));
                    biggestAngle = Math.Max(biggestAngle, Math.Abs(angleOriginal));
                    biggestRate = Math.Max(biggestRate, Math.Abs(rateOriginal));

                    // What the senses would have read without the sign: the stored angle of a negated degree of
                    // freedom stands opposite, so the test is shown to see a difference.
                    if (body.DofSign(2, d) < 0)
                    {
                        physicallyApart = Math.Max(physicallyApart, Math.Abs(body.Q[body.DofStart[1] + d] - body.Q[body.DofStart[2] + d]));
                    }
                }
            }

            _out.WriteLine($"{joint}: the senses differ by {worst:0.#e+0} at worst; the original's angle reached {biggestAngle:0.###} and its rate {biggestRate:0.###}; the stored angles stood {physicallyApart:0.###} apart");

            Assert.True(biggestAngle > 0.05 && biggestRate > 0.01, "the original barely moved, so nothing was tested");
            Assert.True(physicallyApart > 0.05, "the stored angles never differed, so the sign was not tested");
            Assert.True(worst < 1e-5, $"the twins report angles and rates {worst:0.#e+0} apart");
        }

        /// <summary>
        /// The mask names the degrees of freedom the reflection reverses: all of a hinge's, none of a twist's, and the
        /// second and third of any other joint's; the stops are swapped and negated on those only; and with the switch
        /// off every mask is zero and every stop as the genome gave it.
        /// </summary>
        [Theory]
        [InlineData(JointType.Hinge, 1)]
        [InlineData(JointType.Twist, 0)]
        [InlineData(JointType.HingeTwist, 2)]
        [InlineData(JointType.TwistHinge, 2)]
        [InlineData(JointType.Universal, 2)]
        [InlineData(JointType.Spherical, 6)]
        public void TheMaskNamesTheDegreesOfFreedomTheReflectionReverses(JointType joint, int expected)
        {
            Creature reflected = Make(PairGenome(joint, 2, false), Reflecting, Torque()).body;
            Creature plain = Make(PairGenome(joint, 2, false), Plain, Torque()).body;

            Assert.Equal(0, reflected.MirrorMask[0]);
            Assert.Equal(0, reflected.MirrorMask[1]);
            Assert.Equal(expected, reflected.MirrorMask[2]);

            for (int link = 0; link < plain.Links; link++) Assert.Equal(0, plain.MirrorMask[link]);

            int original = reflected.DofStart[1], twin = reflected.DofStart[2];
            for (int d = 0; d < joint.DofCount(); d++)
            {
                bool negated = ((expected >> d) & 1) != 0;
                Assert.Equal(negated ? -1.0 : 1.0, reflected.DofSign(2, d));
                Assert.Equal(1.0, reflected.DofSign(1, d));

                Assert.Equal(negated ? -reflected.LimitHi[original + d] : reflected.LimitLo[original + d], reflected.LimitLo[twin + d]);
                Assert.Equal(negated ? -reflected.LimitLo[original + d] : reflected.LimitHi[original + d], reflected.LimitHi[twin + d]);
                Assert.Equal(reflected.LimitLo[original + d], plain.LimitLo[plain.DofStart[1] + d]);
                Assert.Equal(reflected.LimitHi[original + d], plain.LimitHi[plain.DofStart[1] + d]);
                Assert.Equal(reflected.LimitLo[original + d], plain.LimitLo[plain.DofStart[2] + d]);
                Assert.Equal(reflected.LimitHi[original + d], plain.LimitHi[plain.DofStart[2] + d]);
            }
        }

        /// <summary>
        /// A body with no mirrored part steps the same bits with the switch on and off, under both drives: the
        /// signs are exactly 1 and nothing reads a mask.
        /// </summary>
        [Theory]
        [InlineData(JointType.Hinge, false)]
        [InlineData(JointType.Spherical, false)]
        [InlineData(JointType.Hinge, true)]
        [InlineData(JointType.Universal, true)]
        public void ABodyWithNoMirroredPartStepsTheSameBitsWithTheSwitchOnAndOff(JointType joint, bool bendAndPower)
        {
            Genome genome = PairGenome(joint, 3, bendAndPower);
            genome.Nodes[0].Edges[0].Reflect = Bool3.None;

            (Creature plain, DynamicsWorld plainWorld) = Make(genome, Plain, bendAndPower ? Power() : Torque());
            (Creature reflected, DynamicsWorld reflectedWorld) = Make(genome, Reflecting, bendAndPower ? Power() : Torque());

            Assert.Equal(3, reflected.Links);
            for (int link = 0; link < reflected.Links; link++) Assert.Equal(0, reflected.MirrorMask[link]);

            for (int step = 1; step <= 500; step++)
            {
                plainWorld.Step();
                reflectedWorld.Step();
                if (step % 25 == 0) Assert.Equal(plainWorld.Digest(), reflectedWorld.Digest());
            }

            for (int d = 0; d < plain.Dof; d++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(plain.Q[d]), BitConverter.DoubleToInt64Bits(reflected.Q[d]));
                Assert.Equal(BitConverter.DoubleToInt64Bits(plain.Qd[d]), BitConverter.DoubleToInt64Bits(reflected.Qd[d]));
            }

            Assert.True(Math.Abs(plain.Q[0]) + Math.Abs(plain.Q[1]) > 1e-3, "the body never moved, so nothing was compared");
        }

        // A limb whose drive neurons add up the flow its part feels (every degree of freedom reads two of the three
        // components, weighted) and the oscillator the plain pair has, so that what the senses report moves the pose.
        private static Genome FlowGenome(JointType joint)
        {
            Genome genome = PairGenome(joint, 2, false);
            MorphNode node = genome.Nodes[1];
            int dof = joint.DofCount();

            var neurons = new List<NeuronDef>();
            for (int d = 0; d < dof; d++)
            {
                neurons.Add(new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
                    Inputs = new[]
                    {
                        NeuronInput.FromNeuron(NeuronInputKind.SameNode, dof + d, 1f),
                        NeuronInput.FromSensor(SensorChannel.Flow, d % 3, 3f),
                        NeuronInput.FromSensor(SensorChannel.Flow, (d + 1) % 3, 3f),
                    },
                });
            }

            neurons.AddRange(node.Neurons);
            node.Neurons = neurons.ToArray();
            return genome;
        }

        // The uniform current is along z, which lies in the mirror plane (its normal is x), so it is its own mirror
        // image. It is written into the body's water each step: this world has no current field.
        private const double SideFlow = 0.2;

        private void RunInTheSideCurrent(
            Creature body, DynamicsWorld world, out double worstPose, out double worstSense, out double biggestFlowX)
        {
            worstPose = 0;
            worstSense = 0;
            biggestFlowX = 0;

            for (int step = 1; step <= 1000; step++)
            {
                for (int link = 0; link < body.Links; link++) body.Water[3 * link + 2] = SideFlow;

                world.Step();
                Assert.True(body.Alive, "the body was lost at step " + step);

                worstPose = Math.Max(worstPose, MirrorError(body, 1, 2));

                if (step % 10 != 0) continue;

                body.Senses.Sample();
                for (int k = 0; k < 3; k++)
                {
                    float original = body.Senses.Read(1, SensorChannel.Flow, k);
                    float twin = body.Senses.Read(2, SensorChannel.Flow, k);
                    worstSense = Math.Max(worstSense, Math.Abs(original - twin));
                    if (k == 0) biggestFlowX = Math.Max(biggestFlowX, Math.Abs(original));
                }
            }
        }

        /// <summary>
        /// D234's senses: twins in a uniform current across the mirror plane (along z, in the plane, so the current is its
        /// own mirror image), driven by brains that read the flow, report the same flow and stay mirror images. The
        /// body's flow along its own x is not zero, so the reflected component is exercised. With the reflection of the
        /// senses off, the twin reads that component with the wrong sign and the pair parts.
        /// </summary>
        [Theory]
        [InlineData(JointType.Hinge)]
        [InlineData(JointType.Universal)]
        [InlineData(JointType.Spherical)]
        public void TwinsInASideCurrentReadTheSameFlowAndStayMirrorImages(JointType joint)
        {
            (Creature body, DynamicsWorld world) = Make(FlowGenome(joint), Reflecting, Torque());
            Assert.Equal(-1, body.FrameNormal[0]);
            Assert.Equal(-1, body.FrameNormal[1]);
            Assert.Equal(0, body.FrameNormal[2]);

            RunInTheSideCurrent(body, world, out double pose, out double sense, out double flowX);

            (Creature unreflected, DynamicsWorld unreflectedWorld) = Make(FlowGenome(joint), Reflecting, Torque());
            for (int link = 0; link < unreflected.Links; link++) unreflected.FrameNormal[link] = -1;

            RunInTheSideCurrent(unreflected, unreflectedWorld, out double partedPose, out double partedSense, out _);

            _out.WriteLine($"{joint}: reflected senses differ by {sense:0.#e+0} and the pose by {pose:0.#e+0} (the original's flow along its x reached {flowX:0.###}); unreflected senses differ by {partedSense:0.###} and the pose by {partedPose:0.######}");

            Assert.True(flowX > 0.005, $"the original's flow along its x reached only {flowX:0.######}, so the reflected component was not tested");
            Assert.True(sense < 1e-5, $"the twins read flows {sense:0.#e+0} apart");
            Assert.True(pose < 1e-9, $"the twin left the mirror image of its original by {pose:0.#e+0}");

            Assert.True(partedSense > 0.005, $"without the reflection the twins still read flows only {partedSense:0.######} apart");
            Assert.True(partedPose > 0.01, $"without the reflection the pair stayed within {partedPose:0.######} of a mirror image");
        }

        /// <summary>The frame normal is the part's own x for every reflected part, and absent elsewhere and with the switch off.</summary>
        [Fact]
        public void TheFrameNormalIsTheFirstAxisOfEveryReflectedPartAndAbsentElsewhere()
        {
            foreach (Bool3 plane in new[] { new Bool3(true, false, false), new Bool3(false, true, false), new Bool3(false, false, true) })
            {
                Genome genome = OffAxisGenome(JointType.Hinge, false);
                genome.Nodes[0].Edges[0].Reflect = plane;

                Creature reflected = Make(genome, Reflecting, Torque()).body;
                Creature plain = Make(genome, Plain, Torque()).body;

                for (int link = 0; link < reflected.Links; link++)
                {
                    PhenotypePart part = reflected.Phenotype.Parts[link];
                    Assert.Equal(part.Mirrored ? 0 : -1, reflected.FrameNormal[link]);
                    Assert.Equal(-1, plain.FrameNormal[link]);
                }
            }
        }

        /// <summary>A growth resize rewrites the stops and the mask as a build at that size would.</summary>
        [Fact]
        public void AGrowthResizeKeepsTheMaskAndTheSwappedStops()
        {
            Genome genome = PairGenome(JointType.Universal, 2, false);
            Phenotype adult = Developer.Develop(genome, Reflecting);
            var body = new Creature(0, adult.Scaled(0.5f), Torque());

            int[] mask = (int[])body.MirrorMask.Clone();
            double[] lo = (double[])body.LimitLo.Clone();
            double[] hi = (double[])body.LimitHi.Clone();
            Assert.NotEqual(0, mask[2]);

            body.Resize(adult.Scaled(0.8f));

            Assert.Equal(mask, body.MirrorMask);
            Assert.Equal(lo, body.LimitLo);
            Assert.Equal(hi, body.LimitHi);
        }
    }
}
