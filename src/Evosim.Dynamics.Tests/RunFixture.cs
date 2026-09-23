using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics.Placement;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// A recorded crowd on this build, read once for the whole test run: round 44's world
    /// (<c>rounds/env-r44.ps1</c>, the module gene live) recorded as <c>r46fix-s4</c> on the
    /// light-by-exposure build (D110, 2026-09-23), seed 4, 20,000 s, with the mouth's tunables at
    /// their defaults, the reach bound, the islands and light by exposure off, so that it is round
    /// 44's world. It replaced <c>r45fixc-s4</c> for D110's tunable, which had replaced
    /// <c>r45fixb-s4</c> for D109's islands, which had replaced <c>r45fix-s4</c> the night
    /// the reach bound (<c>DevelopmentLimits.MaxBodyReachMetres</c>) made that recording
    /// unreadable, which had replaced <c>r44fix-s4</c> the same night for the mouth's tunables
    /// and caps, which had replaced round 42 seed 4 that afternoon for the module gene's; every
    /// tunable and every format bump re-records this.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The run directory is read and never written.</b> Two files are opened, both read-only:
    /// the recorded <c>config.json</c>, which is where the tank, the depth and the bed's dials
    /// come from, and one snapshot, which is where the bodies come from. Nothing in this
    /// assembly writes anything under <c>runs/</c>.
    /// </para>
    /// <para>
    /// <b>A real population rather than a constructed one</b>, because the questions packages E
    /// and F answer are about a crowd: how many bodies a bounding sphere says are touching, what
    /// a thousand of them do to a slope, and whether the answer moves when the work is split
    /// differently. A hand-built chain answers none of those, and
    /// <see cref="TestBodies"/> is still what the joint tests use.
    /// </para>
    /// <para>
    /// <b>Cached in a static</b>, so the thousand-genome develop runs once for every test in the
    /// assembly rather than once per test. xunit builds a fixture per test class and there are
    /// three of them here.
    /// </para>
    /// </remarks>
    internal static class RunFixture
    {
        public const string RunDirectory =
            @"D:\Projects\experiments\evolution-simulator\runs\r46fix-s4\2026-09-23-112430-444ac038";

        /// <summary>The recording's name, for the messages that say why it cannot serve.</summary>
        private const string Recording = "r46fix-s4 (round 44's world on the light-by-exposure build with the new tunables off, seed 4)";

        public const string Snapshot = "000020000.jsonl";

        /// <summary>The bed's seed. Any value gives a bed; this one is the tests' bed.</summary>
        public const ulong BedSeed = 20260921;

        private static readonly object Gate = new object();
        private static RunConfig _config;
        private static List<Phenotype> _bodies;

        /// <summary>The recorded world: a tank of 2,200 m2, 45 m deep, relief 1.5 m, tilt 30 m.</summary>
        public static RunConfig Config
        {
            get { Load(); return _config; }
        }

        /// <summary>The snapshot's genomes, developed. About a thousand of them.</summary>
        public static List<Phenotype> Bodies
        {
            get { Load(); return _bodies; }
        }

        /// <summary>
        /// Whether this build can use the recording. Every test here needs it.
        /// </summary>
        /// <remarks>
        /// <b>On disk is not the same as readable.</b> A build that adds a tunable makes every
        /// earlier <c>config.json</c> unreadable and a build that bumps the genome format makes
        /// every earlier snapshot unreadable — §9's refuse-rather-than-default rule, working. So
        /// this asks the two questions separately and <see cref="Why"/> says which one failed,
        /// because "the run is not on this machine" and "this build cannot read the run that is"
        /// need opposite responses: fetch the recording, or record a new fixture.
        /// </remarks>
        public static bool Present => Why == null;

        /// <summary>Why the recording cannot serve as a fixture, or null when it can.</summary>
        public static string Why
        {
            get
            {
                if (!File.Exists(Path.Combine(RunDirectory, "config.json")) ||
                    !File.Exists(Path.Combine(RunDirectory, "snapshots", Snapshot)))
                {
                    return Recording + " is not on this machine (run directories are " +
                           "gitignored and belong to the main working tree)";
                }

                try
                {
                    Load();
                }
                catch (Exception e)
                {
                    return "this build cannot read the config of " + Recording + ": " + e.Message +
                           " — the recording predates a tunable, and a fixture has to be " +
                           "recorded on the build that reads it";
                }

                if (_bodies.Count == 0)
                {
                    return "this build refused every genome in the snapshot of " + Recording + " — the " +
                           "recording predates a genome format bump, and a fixture has to be " +
                           "recorded on the build that reads it";
                }

                return null;
            }
        }

        private static void Load()
        {
            lock (Gate)
            {
                if (_config != null) return;

                _config = RunConfigJson.Read(
                    File.ReadAllText(Path.Combine(RunDirectory, "config.json")), out _);

                var developed = new List<Phenotype>();

                foreach (string line in File.ReadLines(
                             Path.Combine(RunDirectory, "snapshots", Snapshot)))
                {
                    if (line.Length == 0) continue;
                    if (developed.Count >= 1000) break;

                    try
                    {
                        Genome genome = GenomeJson.Read(line);
                        developed.Add(
                            Developer.Develop(genome, _config.Development, null, _config.Shapes));
                    }
                    catch (Exception)
                    {
                        // A genome this build refuses, or one that develops to nothing. The
                        // population is what the snapshot holds that this build can step; a
                        // refusal is not the thing under test here.
                    }
                }

                _bodies = developed;
            }
        }

        /// <summary>The tank's radius, m — <c>sqrt(area / pi)</c>, through Core's own function.</summary>
        public static double TankRadius => TankGeometry.RadiusFor(Config.WorldAreaSquareMetres);

        /// <summary>The run's bed, shaped by its recorded dials.</summary>
        public static BedShape Bed() =>
            new BedShape(
                (float)TankRadius, Config.WorldDepthMetres, Config.BedReliefMetres,
                Config.BedTiltMetres, Config.BedScaleMetres, BedSeed);

        /// <summary>
        /// The run's world for the solver: the glass at <c>(R, R)</c>, the bed shaped, the
        /// instrument on.
        /// </summary>
        public static SolverConfig Solver(
            double dt = 0.01, bool shapedBed = true, bool instrument = true, bool events = true)
        {
            SolverConfig solver = SolverConfig.FromWorld(
                Config, dt, shapedBed ? Bed() : null);

            solver.ContactInstrument = instrument;
            solver.ContactEvents = events;

            return solver;
        }

        /// <summary>
        /// <paramref name="count"/> bodies scattered over the disc and dropped a little way over
        /// the rock.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Placed the way the farm's placer places</b>: a column drawn uniformly over the disc
        /// — <c>r = R sqrt(u)</c>, which is what makes it uniform in area rather than in radius —
        /// and a height taken from <see cref="PlacementFloor.MinimumPlacementY"/>, which is the
        /// floor under that column plus the body's own bounding radius and the placer's five
        /// centimetres. The band on top of that is what makes this a test of the bed rather than
        /// a test of a body already resting on it: the lowest bodies start touching the rock and
        /// the rest come down to it.
        /// </para>
        /// <para>
        /// <b>A band rather than a surface, because the surface is not a place a thousand bodies
        /// fit.</b> A disc of 24 m holds 1,800 m2 and a thousand bodies of about 0.4 m radius
        /// stand 1.3 m apart on it, so a uniform draw puts many of them deeply inside one
        /// another before a step is taken — and the soft push answers a deep overlap with
        /// kilonewtons. The farm's placer refuses a crowded spot and never admits such a body;
        /// this stands in for that refusal by drawing from a volume instead of a surface.
        /// </para>
        /// <para>
        /// <b>A spot already taken is drawn again</b>, which is <c>SharedVolume</c>'s own rule:
        /// the farm's placer refuses a crowded spot and counts the refusal, and no body is ever
        /// admitted with its sphere inside another's. A test that admitted one would be asking
        /// the soft push a question the world never asks it, and the answer — a deep overlap is
        /// kilonewtons — is already known. <paramref name="crowd"/> turns the rule off, for the
        /// instrument's own test, which needs overlaps to count.
        /// </para>
        /// </remarks>
        public static DynamicsWorld Scatter(
            SolverConfig solver, int count, int threads, ulong seed = 4242424242,
            double drop = 10.0, bool crowd = false)
        {
            var world = new DynamicsWorld(solver) { Threads = threads };
            var floor = new PlacementFloor(Config.WorldDepthMetres, solver.Bed);
            var rng = new Rng(seed);

            double axis = TankRadius;

            // Inside the glass with room: the placer keeps a body's whole sphere in the water,
            // and a body started outside it would be a test of the wall's push and not of the
            // bed's.
            double reach = TankRadius - 2.0;

            for (int i = 0; i < count; i++)
            {
                Phenotype adult = Bodies[i % Bodies.Count];
                var body = new Creature(i, adult, solver, Config.Shapes);

                for (int draw = 0; ; draw++)
                {
                    double radius = reach * System.Math.Sqrt(rng.NextFloat());
                    double theta = 2.0 * System.Math.PI * rng.NextFloat();

                    double x = axis + radius * System.Math.Cos(theta);
                    double z = axis + radius * System.Math.Sin(theta);

                    var turn = QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5)
                            .Normalized,
                        rng.NextFloat() * 2.0 * System.Math.PI);

                    // Placed once to learn its own bounding sphere, then put where that sphere
                    // clears the rock. The radius is a property of the pose, so it has to be
                    // asked of a posed body.
                    body.PlaceAt(new Vec3(x, 0, z), turn);

                    double y = floor.MinimumPlacementY((float)x, (float)z, (float)body.ContactRadius) +
                               drop * rng.NextFloat();

                    body.PlaceAt(new Vec3(x, y, z), turn);

                    if (crowd || draw == 40 || Clear(world, body)) break;
                }

                world.AddInIdOrder(body);
            }

            return world;
        }

        /// <summary>
        /// <paramref name="count"/> bodies grown to <paramref name="fraction"/> of adult size
        /// and packed into a cube <paramref name="side"/> metres on a side, in open water.
        /// </summary>
        /// <remarks>
        /// <b>Round 42's crowd, without the bed or the glass to hide behind.</b> A newborn is
        /// born at about a third of its adult body (D087) and dropped inside its parent's
        /// dispersal disc, so a cluster of small bodies at close quarters is what a breeding
        /// world looks like at every moment — the condition the soft push has to survive, not an
        /// edge case. <c>Phenotype.Scaled</c> is Core's own growth, so these are the bodies the
        /// world would have made.
        /// </remarks>
        public static DynamicsWorld Pack(
            SolverConfig solver, int count, double side, float fraction, int threads,
            ulong seed = 909090909)
        {
            var world = new DynamicsWorld(solver) { Threads = threads };
            var rng = new Rng(seed);

            double axis = solver.TankRadiusMetres > 0 ? solver.TankRadiusMetres : 0;

            for (int i = 0; i < count; i++)
            {
                Phenotype adult = Bodies[i % Bodies.Count].Scaled(fraction, Config.Shapes);
                var body = new Creature(i, adult, solver, Config.Shapes);

                body.PlaceAt(
                    new Vec3(
                        axis + (rng.NextFloat() - 0.5) * side,
                        -0.5 * Config.WorldDepthMetres + (rng.NextFloat() - 0.5) * side,
                        axis + (rng.NextFloat() - 0.5) * side),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5)
                            .Normalized,
                        rng.NextFloat() * 2.0 * System.Math.PI));

                world.AddInIdOrder(body);
            }

            return world;
        }

        /// <summary>Whether a posed body's sphere is clear of every body already placed.</summary>
        private static bool Clear(DynamicsWorld world, Creature body)
        {
            for (int i = 0; i < world.Creatures.Count; i++)
            {
                Creature other = world.Creatures[i];
                double gap = (body.ContactCentre - other.ContactCentre).Magnitude;

                if (gap < body.ContactRadius + other.ContactRadius) return false;
            }

            return true;
        }
    }
}
