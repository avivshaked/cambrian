using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// A census of reflected parts against their twins in a recorded world
    /// (<c>--mirror-census &lt;run dir&gt; &lt;seconds,comma,separated&gt; &lt;out file&gt;</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question</b> (<c>logbook/specs/mirrored-anchor-finding.md</c>). Developer folds a
    /// reflection into a mirrored part's pose and stores its two joint anchors unreflected; the
    /// solver rebuilds every link from those anchors. If that stacks a mirrored part's joint on its
    /// twin's, then in a recorded world the two joints, the points where each twin meets its parent,
    /// coincide in the solver while development puts them apart.
    /// </para>
    /// <para>
    /// <b>Read-only, and steps nothing.</b> It restores the world from the run's checkpoint at each
    /// second named, as <see cref="ExtentCensus"/> does, and reads the bodies as the solver holds
    /// them. For each mirrored part it finds its twin, the lowest-index other part of the same
    /// parent grown from the same genome node, and measures the distance between the two joints:
    /// in the solver, <c>Position + RotationMatrix · ChildAnchor</c> of each link in the world, and in
    /// development, <c>Position + Rotation · ChildAnchorLocal</c> of each part in the body's frame
    /// at the solver's own scale. Joint angles move a part about its joint, not the joint itself,
    /// so the solver's distance is a property of the anchors alone.
    /// </para>
    /// </remarks>
    public static class MirrorCensus
    {
        public const string Usage =
            "Usage: Evosim.Farm --mirror-census <run dir> <seconds,comma,separated> <out file>";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A pair counts as stacked when its solver joints are this close and development's are not.</summary>
        public const double StackedMetres = 1e-4;

        public static readonly string[] Columns =
        {
            "second", "creature", "parts", "part", "twin", "source_node", "mirrored_twin",
            "solver_joint_gap_m", "developed_joint_gap_m", "solver_centre_gap_m", "developed_centre_gap_m",
        };

        public static int Run(string[] args)
        {
            if (args == null || args.Length != 4 || args[0] != "--mirror-census")
            {
                Console.Error.WriteLine(Usage);
                return 1;
            }

            double[] seconds;

            try
            {
                seconds = CensusRead.ParseSeconds(args[2], Usage);
            }
            catch (FormatException e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }

            try
            {
                Measure(args[1], seconds, args[3], Environment.GetEnvironmentVariable, Console.Out);
                return 0;
            }
            catch (Exception e) when (
                e is ArgumentException || e is InvalidOperationException || e is IOException ||
                e is InvalidDataException)
            {
                Console.Error.WriteLine("mirror-census: " + e.Message);
                return 1;
            }
        }

        /// <summary>The twin of part <paramref name="i"/>: the lowest-index other part with its parent and node, or -1.</summary>
        public static int TwinOf(Phenotype phenotype, int i)
        {
            PhenotypePart part = phenotype.Parts[i];
            if (part.ParentIndex < 0) return -1;

            for (int j = 0; j < phenotype.PartCount; j++)
            {
                if (j == i) continue;
                PhenotypePart other = phenotype.Parts[j];
                if (other.ParentIndex == part.ParentIndex && other.SourceNode == part.SourceNode) return j;
            }

            return -1;
        }

        /// <summary>Link <paramref name="i"/>'s joint in the world: its centre plus its attitude times its child anchor.</summary>
        public static Vec3 SolverJoint(Creature body, int i)
        {
            Vec3 centre = Vec3.Read(body.Position, 3 * i);
            double[] m = body.RotationMatrix;
            int o = 9 * i;
            double ax = body.ChildAnchor[3 * i], ay = body.ChildAnchor[3 * i + 1], az = body.ChildAnchor[3 * i + 2];
            return new Vec3(
                centre.X + m[o] * ax + m[o + 1] * ay + m[o + 2] * az,
                centre.Y + m[o + 3] * ax + m[o + 4] * ay + m[o + 5] * az,
                centre.Z + m[o + 6] * ax + m[o + 7] * ay + m[o + 8] * az);
        }

        /// <summary>
        /// Part <paramref name="i"/>'s joint in the body's frame, as development placed it: its
        /// centre plus the part's full transform (<c>R · F</c>) times the anchor development used.
        /// With <paramref name="anchorsReflected"/> (<see cref="DevelopmentLimits.ReflectMirroredAnchors"/>
        /// of the run's own config) the stored anchor is already <c>F · anchor</c>, so the proper
        /// rotation takes it as it stands. Off, the stored anchor is the unreflected one and a
        /// mirrored part's flip is applied here, so the reference is development's joint under
        /// either setting and the two readings measure the same thing.
        /// </summary>
        public static Float3 DevelopedJoint(Phenotype phenotype, int i, bool anchorsReflected)
        {
            PhenotypePart part = phenotype.Parts[i];
            Float3 anchor = part.ChildAnchorLocal;
            if (!anchorsReflected && part.Mirrored) anchor = new Float3(-anchor.X, anchor.Y, anchor.Z);
            return part.Position + part.Rotation.Rotate(anchor);
        }

        public static void Measure(
            string runPath, IReadOnlyList<double> seconds, string outFile, EnvBinding.Lookup env, TextWriter log)
        {
            CensusRead read = CensusRead.Open("mirror-census", Usage, runPath, outFile, env, log);
            string outPath = Path.GetFullPath(outFile);
            RunConfig config = read.Config;

            // The run's own switch, from its config.json: the bodies below are developed under it,
            // and the reference joint must be read the way the anchors were stored.
            bool reflected = config.Development.ReflectMirroredAnchors;
            log.WriteLine("mirror-census: mirrored anchors " + (reflected ? "reflected" : "unreflected") + " in the run's config");
            TricklePoolFiles.Pool pool = read.Pool;
            string runDirectory = read.RunDir;
            bool migrating = read.Migrating;

            var lines = new List<string> { string.Join("\t", Columns) };

            foreach (double second in seconds)
            {
                CensusRead.Found found = read.Find(second, log);
                CheckpointHeader header = found.Header;

                Parallelism.Threads = 1;

                var world = new World(config, header.Seed, pool?.Genomes, pool?.Weights) { QueueAdmittedGenomes = true };

                using (var sim = new Simulation(world, header.Seed, found.PhysicsDt, found.StepsPerMetabolic, 1, runDirectory: null))
                {
                    var sampler = new Sampler { PoolNamed = config.FoundingTricklePoolCount > 0, Migrating = migrating };
                    Program.ReadCheckpoint(found.Path, world, sim, sampler);

                    int bodies = 0, withMirror = 0, pairs = 0, stacked = 0, apart = 0, unpaired = 0;
                    double worstApart = 0d;
                    IReadOnlyList<Organism> living = world.Living;

                    for (int k = 0; k < living.Count; k++)
                    {
                        Organism organism = living[k];
                        if (!sim.TryPose(organism.Id, out Creature body)) continue;
                        bodies++;

                        Phenotype p = body.Phenotype;
                        bool any = false;

                        for (int i = 0; i < p.PartCount; i++)
                        {
                            if (!p.Parts[i].Mirrored) continue;
                            any = true;

                            int j = TwinOf(p, i);
                            if (j < 0) { unpaired++; continue; }
                            pairs++;

                            double solverJoint = (SolverJoint(body, i) - SolverJoint(body, j)).Magnitude;
                            double developedJoint = (DevelopedJoint(p, i, reflected) - DevelopedJoint(p, j, reflected)).Magnitude;
                            double solverCentre = (Vec3.Read(body.Position, 3 * i) - Vec3.Read(body.Position, 3 * j)).Magnitude;
                            double developedCentre = (p.Parts[i].Position - p.Parts[j].Position).Magnitude;

                            if (solverJoint < StackedMetres && developedJoint > 10 * StackedMetres) stacked++;
                            else
                            {
                                apart++;
                                worstApart = Math.Max(worstApart, Math.Abs(solverJoint - developedJoint));
                            }

                            lines.Add(string.Join("\t",
                                second.ToString("0.###", Inv), organism.Id.ToString(Inv), p.PartCount.ToString(Inv),
                                i.ToString(Inv), j.ToString(Inv), p.Parts[i].SourceNode.ToString(Inv),
                                p.Parts[j].Mirrored ? "1" : "0",
                                solverJoint.ToString("0.000000", Inv), developedJoint.ToString("0.000000", Inv),
                                solverCentre.ToString("0.000000", Inv), developedCentre.ToString("0.000000", Inv)));
                        }

                        if (any) withMirror++;
                    }

                    log.WriteLine(
                        "mirror-census: " + Path.GetFileName(found.Path) + " restored at " +
                        world.ElapsedSeconds.ToString("0.###", Inv) + " s: " + living.Count + " living, " + bodies +
                        " with a solver body, " + withMirror + " with a mirrored part; " + pairs + " mirrored parts with a twin (" +
                        unpaired + " without): joints stacked in the solver and apart in development in " + stacked +
                        ", not stacked in " + apart + " (largest |solver - developed| joint gap among those " +
                        worstApart.ToString("0.000", Inv) + " m)");
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllLines(outPath, lines, new UTF8Encoding(false));
            log.WriteLine("mirror-census: " + (lines.Count - 1) + " pairs written to " + outPath);
        }
    }
}
