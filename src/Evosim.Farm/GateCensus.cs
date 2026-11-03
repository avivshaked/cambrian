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
    /// A census of every living body's reserve against its breeding gate in a recorded world
    /// (<c>--gate-census &lt;run dir&gt; &lt;seconds,comma,separated&gt; &lt;out file&gt;</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question</b> (<c>logbook/specs/breeding-urge/spec.md</c>, measure 1). A body breeds on
    /// the first metabolic step at which its funds clear <see cref="Organism.BreedingGate(RunConfig)"/>,
    /// so by the code a lump breeder never stays above its gate for more than one step. The read
    /// shows how much of a guild sits at the line and how much below it.
    /// </para>
    /// <para>
    /// <b>Read-only, and steps nothing.</b> It restores the world from the run's checkpoint at each
    /// second named, as <see cref="MirrorCensus"/> does, and writes one JSONL row per living body
    /// that is not remains. The gate is Core's own expression on the restored body; the funds it is
    /// asked of are the reserve for a lump breeder and the gestation account for a gestating one,
    /// as <c>World.IsSolvent</c> reads them, and the row carries both numbers.
    /// </para>
    /// <para>
    /// <b>What <c>adult</c> is.</b> <c>World.IsSolvent</c> asks two things before the gate: that
    /// births are not held (<see cref="World.BirthsHeld"/>, an assay's switch) and, with
    /// <see cref="RunConfig.GrowthAdultSeconds"/> above 0 (D241), that the body's
    /// <see cref="Organism.BodyFraction"/> has reached 1. Each is its own field
    /// (<c>births_held</c>, <c>grown</c>), and <c>adult</c> is true when neither refuses. No age or
    /// cooldown condition exists in that path, so the reserve is the only thing left for the
    /// reader to judge.
    /// </para>
    /// </remarks>
    public static class GateCensus
    {
        public const string Usage =
            "Usage: Evosim.Farm --gate-census <run dir> <seconds,comma,separated> <out file>";

        /// <summary>The fields of each row, in the order they are written.</summary>
        public static readonly string[] Fields =
        {
            "t", "id", "plant", "mode", "adult", "grown", "births_held", "reserve", "account", "gate",
            "standingWatts", "age", "tissue",
        };

        public static int Run(string[] args)
        {
            if (args == null || args.Length != 4 || args[0] != "--gate-census")
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
                Console.Error.WriteLine("gate-census: " + e.Message);
                return 1;
            }
        }

        /// <summary>One body's row: <see cref="Fields"/>, as one line of compact JSON.</summary>
        public static string Row(World world, Organism organism, double second, RunConfig config)
        {
            bool gestating = organism.Gestates;
            bool grown = !(config.GrowthAdultSeconds > 0f) || organism.BodyFraction >= 1f;
            bool held = world.BirthsHeld;

            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("t", second);
            w.Field("id", organism.Id);
            w.Field("plant", Metabolism.IsPlant(organism.Phenotype));
            w.Field("mode", gestating ? "gestating" : "lump");
            w.Field("adult", grown && !held);
            w.Field("grown", grown);
            w.Field("births_held", held);
            w.Field("reserve", organism.Energy);
            w.Field("account", gestating ? organism.GestationJoules : 0d);
            w.Field("gate", organism.BreedingGate(config));
            w.Field("standingWatts", organism.StandingWatts);
            w.Field("age", organism.Age);
            w.Field("tissue", organism.TissueJoules);
            w.EndObject();
            return w.ToString();
        }

        public static void Measure(
            string runPath, IReadOnlyList<double> seconds, string outFile, EnvBinding.Lookup env, TextWriter log)
        {
            CensusRead read = CensusRead.Open("gate-census", Usage, runPath, outFile, env, log);
            string outPath = Path.GetFullPath(outFile);
            RunConfig config = read.Config;
            TricklePoolFiles.Pool pool = read.Pool;
            string runDirectory = read.RunDir;
            bool migrating = read.Migrating;

            var lines = new List<string>();

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

                    int rows = 0, plants = 0, gestating = 0, atOrAbove = 0;
                    IReadOnlyList<Organism> living = world.Living;

                    for (int k = 0; k < living.Count; k++)
                    {
                        Organism organism = living[k];
                        if (organism.IsRemains) continue;

                        lines.Add(Row(world, organism, second, config));
                        rows++;
                        if (Metabolism.IsPlant(organism.Phenotype)) plants++;
                        if (organism.Gestates) gestating++;

                        double funds = organism.Gestates ? organism.GestationJoules : organism.Energy;
                        if (funds >= organism.BreedingGate(config)) atOrAbove++;
                    }

                    log.WriteLine(
                        "gate-census: " + Path.GetFileName(found.Path) + " restored at " +
                        world.ElapsedSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s: " + rows +
                        " bodies written (" + plants + " plants, " + gestating + " gestating), " + atOrAbove +
                        " with funds at or above the gate");
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllLines(outPath, lines, new UTF8Encoding(false));
            log.WriteLine("gate-census: " + lines.Count + " rows written to " + outPath);
        }
    }
}
