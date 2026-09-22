using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Package D of the farm port: growth, resized in place. The acceptance the proposal names is
    /// "no jump at a resize, to the bit", and it is read here as two separate claims — that a body
    /// grown to a size is the body built at that size, quantity for quantity, and that nothing the
    /// creature was in the middle of doing is thrown away by the resize.
    /// </summary>
    public sealed class GrowthTests
    {
        private readonly ITestOutputHelper _out;

        public GrowthTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------------------ (1) and (2)

        /// <summary>
        /// The central acceptance. A body built at one fraction and resized to another carries
        /// exactly the numbers a body built at the second fraction carries: masses, inertias,
        /// anchors, joint frames, limit springs, drag panels, the contact radius — and, given the
        /// same joint coordinates and the same root pose, exactly the same forward kinematics.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Bit-equality, not a tolerance.</b> The two paths run the same arithmetic on the same
        /// inputs, so they must agree to the last bit; anything else means a second transcription
        /// of the derivation has appeared somewhere, which is the fault this method exists to
        /// catch. A tolerance would hide exactly that.
        /// </para>
        /// <para>
        /// <b>And the root does not move.</b> The resize never writes
        /// <see cref="Creature.BasePosition"/>, so the jump the farm measures
        /// (<c>Ecosystem.MaxResizeJumpMetres</c>) is zero by construction rather than by
        /// measurement; every other link moves, because the body is a different size, and each one
        /// lands where the fresh build's kinematics put it.
        /// </para>
        /// </remarks>
        [Fact]
        public void AResizedBodyIsTheBodyBuiltAtThatSize()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.TissueExcessDensity = 0.02;
            config.NeutralBodyVolume = 0.25;

            var bodies = new List<(string, Phenotype, PartShapeRegistry)>
            {
                ("hinge chain 4",
                    Developer.Develop(TestBodies.Chain(4, JointType.Hinge)),
                    PartShapeRegistry.Standard),
                ("universal chain 3",
                    Developer.Develop(TestBodies.Chain(3, JointType.Universal)),
                    PartShapeRegistry.Standard),
                ("spherical chain 3",
                    Developer.Develop(TestBodies.Chain(3, JointType.Spherical)),
                    PartShapeRegistry.Standard),
                ("box, no joints",
                    Developer.Develop(TestBodies.Box(new Float3(0.3f, 0.2f, 0.15f))),
                    PartShapeRegistry.Standard),
            };

            // The evolved bodies too, for the shapes a hand-written chain has not got: r42's
            // genomes carry spheres and capsules, whose panel counts and inertia tensors are
            // where a scaling is most likely to be got wrong.
            Round42 round42 = Round42.Read(6, jointedOnly: false);
            if (round42 != null)
            {
                for (int i = 0; i < round42.Bodies.Count; i++)
                {
                    bodies.Add(($"r42 genome {i}", round42.Bodies[i], round42.Config.Shapes));
                }
            }

            foreach ((string name, Phenotype adult, PartShapeRegistry shapes) in bodies)
            {
                foreach ((float from, float to) in new[] { (0.3f, 1f), (0.45f, 0.9f), (0.08f, 0.62f) })
                {
                    Phenotype small = At(adult, from, shapes);
                    Phenotype large = At(adult, to, shapes);

                    var grown = new Creature(7, small, config, shapes);
                    Pose(grown);
                    grown.Resize(large);

                    var fresh = new Creature(7, large, config, shapes);
                    Pose(fresh);

                    string at = $"{name}, {from} -> {to}";

                    Same(at + " Volume", fresh.Volume, grown.Volume);
                    Same(at + " Lift", fresh.Lift, grown.Lift);
                    Same(at + " Power", fresh.Power, grown.Power);
                    Same(at + " PlainMass", fresh.PlainMass, grown.PlainMass);
                    Same(at + " Mass", fresh.Mass, grown.Mass);
                    Same(at + " InertiaLocal", fresh.InertiaLocal, grown.InertiaLocal);
                    Same(at + " SmallestInertia", fresh.SmallestInertia, grown.SmallestInertia);

                    Same(at + " ChildAnchor", fresh.ChildAnchor, grown.ChildAnchor);
                    Same(at + " ParentAnchor", fresh.ParentAnchor, grown.ParentAnchor);
                    Same(at + " JointFrame", fresh.JointFrame, grown.JointFrame);
                    Same(at + " RestFrame", fresh.RestFrame, grown.RestFrame);

                    Same(at + " LimitLo", fresh.LimitLo, grown.LimitLo);
                    Same(at + " LimitHi", fresh.LimitHi, grown.LimitHi);
                    Same(at + " LimitStiffness", fresh.LimitStiffness, grown.LimitStiffness);
                    Same(at + " LimitDamping", fresh.LimitDamping, grown.LimitDamping);

                    Assert.Equal(fresh.PanelStart, grown.PanelStart);
                    Same(at + " PanelCentre", fresh.PanelCentre, grown.PanelCentre);
                    Same(at + " PanelNormal", fresh.PanelNormal, grown.PanelNormal);
                    Same(at + " PanelArea", fresh.PanelArea, grown.PanelArea);

                    Same(at + " LinkReach", fresh.LinkReach, grown.LinkReach);
                    Same(at + " TotalMass", new[] { fresh.TotalMass }, new[] { grown.TotalMass });
                    Same(at + " TotalVolume", new[] { fresh.TotalVolume }, new[] { grown.TotalVolume });
                    Same(at + " ContactRadius",
                        new[] { fresh.ContactRadius }, new[] { grown.ContactRadius });
                    Same(at + " ContactCentre",
                        new[] { fresh.ContactCentre.X, fresh.ContactCentre.Y, fresh.ContactCentre.Z },
                        new[] { grown.ContactCentre.X, grown.ContactCentre.Y, grown.ContactCentre.Z });

                    // (2): the root is exactly where it was, and every other link is where the
                    // fresh build's forward kinematics put it.
                    Assert.Equal(0.0, (grown.BasePosition - fresh.BasePosition).Magnitude);
                    Assert.Equal(
                        0.0,
                        (Vec3.Read(grown.Position, 0) - Vec3.Read(fresh.Position, 0)).Magnitude);

                    Same(at + " Position", fresh.Position, grown.Position);
                    Same(at + " Rotation", fresh.Rotation, grown.Rotation);
                    Same(at + " RotationMatrix", fresh.RotationMatrix, grown.RotationMatrix);
                    Same(at + " Velocity", fresh.Velocity, grown.Velocity);
                    Same(at + " Spin", fresh.Spin, grown.Spin);

                    // And the state a resize must NOT touch.
                    Same(at + " Q", fresh.Q, grown.Q);
                    Same(at + " Qd", fresh.Qd, grown.Qd);
                    Same(at + " BallRotation", fresh.BallRotation, grown.BallRotation);
                }
            }

            _out.WriteLine($"{bodies.Count} bodies x 3 growth steps, every array bit-equal");
        }

        /// <summary>
        /// The root moves not at all and the body's own centre of mass does — which is the
        /// distinction the farm's two instruments draw, and the reason this is written out rather
        /// than left implicit in the test above.
        /// </summary>
        [Fact]
        public void AResizeMovesTheBodyAroundItsRootAndNotTheRoot()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Phenotype adult = Developer.Develop(TestBodies.Chain(4, JointType.Hinge));

            var body = new Creature(1, At(adult, 0.3f, null), config, null);
            Pose(body);

            Vec3 rootBefore = Vec3.Read(body.Position, 0);
            Vec3 centreBefore = body.CentreOfMass();
            double radiusBefore = body.ContactRadius;

            // The farm records the fraction it built at, as Ecosystem.Reconcile does.
            body.AppliedBodyFraction = 0.3f;

            body.Resize(At(adult, 1f, null), 1f);

            Assert.Equal(0.0, (Vec3.Read(body.Position, 0) - rootBefore).Magnitude);
            Assert.Equal(0.0, (body.BasePosition - rootBefore).Magnitude);

            double centreMoved = (body.CentreOfMass() - centreBefore).Magnitude;

            _out.WriteLine($"root moved 0 m exactly; centre of mass moved {centreMoved:0.####} m; " +
                           $"contact radius {radiusBefore:0.####} -> {body.ContactRadius:0.####} m");

            Assert.True(centreMoved > 0, "a body that grew around a fixed root moved its centre");
            Assert.True(body.ContactRadius > radiusBefore, "a grown body needs more room");
            Assert.Equal(1L, body.Resizes);
            Assert.Equal(1f, body.AppliedBodyFraction);
        }

        // ------------------------------------------------------------------ (3)

        /// <summary>
        /// A moving, driven body grown from a third of its adult volume to the whole of it, ten
        /// seconds at a time over five minutes of still water: it stays a number, and not one of
        /// its joint angles moves across a resize.
        /// </summary>
        /// <remarks>
        /// The angle continuity is the part that matters. A body's joint coordinates are its
        /// state, and a growth model that rebuilt the articulation — or that wrote the link
        /// transforms rather than the anchors — would reset or perturb them every ten seconds,
        /// which is a creature that stops mid-stroke thirty times a lifetime. Here the coordinates
        /// are not written at all, so the snap is exactly zero and the threshold in the brief
        /// (1e-9 rad) has nothing to absorb.
        /// </remarks>
        [Fact]
        public void AGrowingDrivenBodyNeverSnapsAJoint()
        {
            Round42 round42 = Round42.Read(40, jointedOnly: true);
            if (round42 == null) { Skipped(); return; }

            Phenotype adult = round42.Bodies[0];
            SolverConfig config = round42.Solver(0.01);
            config.CreatureContact = false;

            var world = new DynamicsWorld(config) { Threads = 1 };
            var body = new Creature(0, At(adult, 0.3f, round42.Config.Shapes), config, round42.Config.Shapes);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);

            // Moving, and not only driven. Round 42's jointed bodies hold a pose — the parity
            // swims found that none of them strokes in either engine — so a body left at rest
            // here sits at rest for five minutes and the test asserts nothing. Thrown through the
            // water instead, its drag swings every joint against its springs while its brain adds
            // whatever it asks for, which is a body with something to lose at a resize.
            Vec3.Write(body.Velocity, 0, new Vec3(1.2, 0.0, 0.4));
            Vec3.Write(body.Spin, 0, new Vec3(0.0, 0.9, 0.0));
            Kinematics.Refresh(body);
            body.RefreshContactSphere();
            body.CommitContactSphere();

            world.Add(body);

            const int steps = 30000;          // 300 s at dt 0.01
            const int every = 1000;           // a growth step every 10 s
            double worstSnap = 0;
            int resizes = 0;

            var before = new double[body.Dof];
            var lowest = new double[body.Dof];
            var highest = new double[body.Dof];
            for (int i = 0; i < body.Dof; i++) { lowest[i] = double.MaxValue; highest[i] = double.MinValue; }

            for (int step = 1; step <= steps; step++)
            {
                world.Step();

                for (int i = 0; i < body.Dof; i++)
                {
                    if (body.Q[i] < lowest[i]) lowest[i] = body.Q[i];
                    if (body.Q[i] > highest[i]) highest[i] = body.Q[i];
                }

                if (step % every != 0) continue;

                Array.Copy(body.Q, before, body.Dof);

                float fraction = 0.3f + 0.7f * (step / every) / (float)(steps / every);
                body.Resize(At(adult, fraction, round42.Config.Shapes));
                resizes++;

                for (int i = 0; i < body.Dof; i++)
                {
                    double snap = Math.Abs(body.Q[i] - before[i]);
                    if (snap > worstSnap) worstSnap = snap;
                }

                Assert.True(body.IsFinite(), $"body stopped being a number at step {step}");
            }

            double widest = 0;
            for (int i = 0; i < body.Dof; i++)
            {
                double range = highest[i] - lowest[i];
                if (range > widest) widest = range;
            }

            _out.WriteLine($"{body.Links} links, {body.Dof} dof, {resizes} resizes over 300 s");
            _out.WriteLine($"worst joint-angle snap across a resize {worstSnap:0.###e+00} rad");
            _out.WriteLine($"widest joint travel during the run {widest:0.####} rad");
            _out.WriteLine($"fastest link at the end {body.MaxLinkSpeed():0.###} m/s, " +
                           $"depth {body.BasePosition.Y:0.##} m, {body.Resizes} resizes recorded");

            Assert.Equal(30, resizes);
            Assert.True(body.Alive, "the solver lost the body");
            Assert.True(body.IsFinite(), "the body is not finite at the end");

            // Without this the continuity claim is vacuous: a joint that never moved cannot snap.
            Assert.True(widest > 1e-3, $"the joints barely moved ({widest} rad) — nothing was tested");

            Assert.True(worstSnap <= 1e-9, $"a joint snapped by {worstSnap} rad across a resize");
        }

        // ------------------------------------------------------------------ (4)

        /// <summary>
        /// The thread count still does not change the trajectory when bodies are growing: 200 of
        /// round 42's jointed bodies, packed close enough that contact fires, every one of them
        /// resized every thousand steps, run at 1, 4 and 16 threads.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A resize commits the contact sphere, which is the one piece of a creature every other
        /// creature reads — so it has to happen between steps, serially, in one fixed order. This
        /// is the test that says so: move the resize loop inside the parallel phase and the
        /// digests part.
        /// </para>
        /// <para>
        /// <b>Packed to twelve metres rather than to four, and the reason is not growth.</b> At
        /// four metres 78 of these 200 bodies are lost to divergence inside three thousand steps,
        /// and a world with divergent bodies in it does not replay across thread counts on this
        /// build — with or without a single resize in it. The cause is the JIT and not a race:
        /// one thread takes <c>DynamicsWorld.Step</c>'s serial branch and four take the delegate,
        /// the two are promoted out of quick-JIT differently, and quick-JITted code gives
        /// different floating-point bits from optimised code on arithmetic that is already at the
        /// edge. <c>DOTNET_TieredCompilation=0</c> and
        /// <c>DOTNET_TC_QuickJitForLoops=0</c> each make all three thread counts agree, on the
        /// same digest. That is a hazard for the port's replay guarantee and it belongs to the
        /// package that owns the digest; this test is about growth, so it runs a world that holds
        /// together and says plainly why.
        /// </para>
        /// </remarks>
        [Fact]
        public void TheThreadCountDoesNotChangeAGrowingWorld()
        {
            Round42 round42 = Round42.Read(400, jointedOnly: true);
            if (round42 == null) { Skipped(); return; }

            ulong one = GrowingWorld(round42, 1, contact: true);
            ulong four = GrowingWorld(round42, 4, contact: true);
            ulong sixteen = GrowingWorld(round42, 16, contact: true);

            _out.WriteLine($"digest  1 thread  {one:x16}");
            _out.WriteLine($"digest  4 threads {four:x16}");
            _out.WriteLine($"digest 16 threads {sixteen:x16}");

            Assert.Equal(one, four);
            Assert.Equal(one, sixteen);

            // A packing loose enough to keep every body means the test could quietly become one
            // in which no two bodies ever touch, and then it would say nothing about the shared
            // read a resize makes. So: the same world without creature contact must differ.
            ulong alone = GrowingWorld(round42, 4, contact: false);
            _out.WriteLine($"digest with no creature contact {alone:x16}");

            Assert.NotEqual(one, alone);
        }

        private ulong GrowingWorld(Round42 round42, int threads, bool contact)
        {
            SolverConfig config = round42.Solver(0.01);
            config.CreatureContact = contact;

            var world = new DynamicsWorld(config) { Threads = threads };
            var adults = new List<Phenotype>();
            var rng = new Rng(20260921);

            const int bodies = 200;
            const double Spread = 12.0;

            for (int i = 0; i < bodies; i++)
            {
                Phenotype adult = round42.Bodies[i % round42.Bodies.Count];
                adults.Add(adult);

                var body = new Creature(
                    i, At(adult, 0.3f, round42.Config.Shapes), config, round42.Config.Shapes);

                body.PlaceAt(
                    new Vec3(
                        (rng.NextFloat() - 0.5) * Spread,
                        -3.0 - rng.NextFloat() * 2.0,
                        (rng.NextFloat() - 0.5) * Spread),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));

                world.Add(body);
            }

            for (int step = 1; step <= 3000; step++)
            {
                world.Step();

                if (step % 1000 != 0) continue;

                // Between steps and in creature order, which is what keeps the resize out of the
                // thread count. The fraction is the same for every body, so every one of them is
                // resized on every growth step rather than a few.
                float fraction = 0.3f + 0.7f * (step / 1000) / 3f;

                for (int i = 0; i < world.Creatures.Count; i++)
                {
                    Creature body = world.Creatures[i];
                    if (!body.Alive) continue;
                    body.Resize(At(adults[i], fraction, round42.Config.Shapes));
                }
            }

            _out.WriteLine($"  {threads} thread(s): {world.NonFiniteBodies()} of {bodies} lost, " +
                           $"{world.TotalLinks()} links");

            return world.Digest();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The body at a volume fraction of its adult, exactly as <c>World.Grow</c> makes it: the
        /// cube root, because the fraction is a volume and the parts are scaled by a length.
        /// </summary>
        private static Phenotype At(Phenotype adult, float fraction, PartShapeRegistry shapes) =>
            fraction >= 1f
                ? adult
                : adult.Scaled((float)Math.Pow(fraction, 1d / 3d), shapes);

        /// <summary>
        /// A non-trivial pose and motion: a root off the origin and turned, every joint bent and
        /// turning, and the root moving and spinning. A resize that only worked from rest, or only
        /// at the identity pose, would pass a weaker test than this.
        /// </summary>
        private static void Pose(Creature body)
        {
            body.PlaceAt(
                new Vec3(1.5, -7.25, -2.0),
                QuatD.FromAxisAngle(new Vec3(0.3, 0.5, -0.2).Normalized, 0.7));

            for (int link = 1; link < body.Links; link++)
            {
                int n = body.DofCount[link];
                int at = body.DofStart[link];
                if (n == 0 || at < 0) continue;

                if (n == 3)
                {
                    // A ball joint's configuration is the quaternion; Q is the rotation vector it
                    // reads back as, so the two are set together or the pose is not a pose.
                    var v = new Vec3(0.11 * (link + 1), -0.07 * link, 0.05 * (link + 2));
                    double angle = v.Magnitude;

                    QuatD.Write(
                        body.BallRotation, 4 * link,
                        angle > 1e-12
                            ? QuatD.FromAxisAngle(v * (1.0 / angle), angle)
                            : QuatD.Identity);

                    body.Q[at] = v.X;
                    body.Q[at + 1] = v.Y;
                    body.Q[at + 2] = v.Z;
                }
                else
                {
                    for (int d = 0; d < n; d++) body.Q[at + d] = 0.13 * (link + d + 1);
                }

                for (int d = 0; d < n; d++) body.Qd[at + d] = 0.21 * (d + 1) - 0.05 * link;
            }

            Vec3.Write(body.Velocity, 0, new Vec3(0.4, -0.15, 0.25));
            Vec3.Write(body.Spin, 0, new Vec3(-0.2, 0.35, 0.1));

            // The three the end of a step does, and in that order — a contact sphere taken at the
            // straight pose the constructor left behind would make the two bodies differ in the
            // one quantity a resize is most obviously responsible for.
            Kinematics.Refresh(body);
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        private static void Same(string what, double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) ==
                    BitConverter.DoubleToInt64Bits(actual[i]))
                {
                    continue;
                }

                Assert.Fail($"{what}[{i}]: built {expected[i]:R} but grown {actual[i]:R}");
            }
        }

        private void Skipped() => _out.WriteLine(
            "runs/r42-s4/2026-09-21-051718-ff557bce is not on this machine, or is and this build " +
            "refuses it — run directories are gitignored and belong to the main working tree, and " +
            "a recording written before a tunable or a genome format bump is refused by §9.");

        /// <summary>
        /// Round 42 seed 4's genomes at t = 20,000 s, developed under that run's own config.
        /// Read-only, always: a test that wrote into a run directory would be editing the record.
        /// </summary>
        private sealed class Round42
        {
            public RunConfig Config;
            public List<Phenotype> Bodies;

            /// <summary>The run's world, with the water at rest and no glass.</summary>
            public SolverConfig Solver(double dt)
            {
                SolverConfig solver = SolverConfig.From(Config, dt);
                solver.TankRadiusMetres = 0;
                return solver;
            }

            public static Round42 Read(int want, bool jointedOnly)
            {
                string run = Locate();
                if (run == null) return null;

                // A recording this build cannot read is the same answer as a recording that is not
                // here: there are no bodies to add. §9 refuses a config written before a tunable
                // and a genome written before a format bump, so both arrive as an exception and
                // both mean "record a fixture on the build that reads it".
                RunConfig config;
                try
                {
                    config = RunConfigJson.Read(
                        File.ReadAllText(Path.Combine(run, "config.json")), out _);
                }
                catch (Exception)
                {
                    return null;
                }

                var bodies = new List<Phenotype>();
                string snapshot = Path.Combine(run, "snapshots", "000020000.jsonl");

                foreach (string line in File.ReadLines(snapshot))
                {
                    if (bodies.Count >= want) break;
                    if (line.Length == 0) continue;

                    Phenotype developed;
                    try
                    {
                        developed = Developer.Develop(
                            GenomeJson.Read(line), config.Development, null, config.Shapes);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (developed.PartCount == 0) continue;
                    if (jointedOnly && developed.TotalDof == 0) continue;

                    bodies.Add(developed);
                }

                return bodies.Count > 0 ? new Round42 { Config = config, Bodies = bodies } : null;
            }

            /// <summary>
            /// Found by walking up from the test binary: this worktree has no <c>runs/</c> of its
            /// own, and the main tree is two directories above <c>scratch/wt-farm-growth</c>.
            /// </summary>
            private static string Locate()
            {
                const string rel = "runs/r42-s4/2026-09-21-051718-ff557bce";
                var directory = new DirectoryInfo(AppContext.BaseDirectory);

                while (directory != null)
                {
                    string candidate = Path.Combine(
                        directory.FullName, rel.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(Path.Combine(candidate, "config.json")) &&
                        File.Exists(Path.Combine(candidate, "snapshots", "000020000.jsonl")))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }

                return null;
            }
        }
    }
}
