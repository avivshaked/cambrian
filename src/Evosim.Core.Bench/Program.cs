using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Core.Bench
{
    /// <summary>
    /// Times <see cref="World.Step(float)"/> and the passes it makes, on a world built from a
    /// recorded run's own <c>config.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Core alone.</b> No Dynamics, no physics, no run directory. Bodies are placed by this
    /// program at a deterministic scatter inside the world's own footprint and stay where they
    /// are put; every metabolic step hands each of them back to
    /// <see cref="World.Observe(Organism, Float3, float)"/> at that place with no work done. So
    /// what the clock reads is the economy and the fields, which is the 87% the farm's wall split
    /// attributes to <c>world</c>.
    /// </para>
    /// <para>
    /// <b>Two measurements, not one.</b> The first is the whole step, which is the number the
    /// farm's footer prints. The second walks the field passes one at a time — they are all
    /// public on <see cref="IMatterField"/> — so each has its own clock. The two are taken on the
    /// same world one after the other, so the pass timings are of a world the first measurement
    /// has already aged; the cost of a grid pass is set by the cell count and not by what the
    /// cells hold, so that does not move them.
    /// </para>
    /// </remarks>
    public static class Program
    {
        private const float MetabolicStep = 0.5f;

        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine(
                    "usage: Evosim.Core.Bench <config.json> [--steps N] [--warm N] [--bodies N] " +
                    "[--seed N] [--threads N]");
                return 2;
            }

            string configPath = args[0];
            int steps = 200, warm = 60, bodies = 300, threadList = -1;
            ulong seed = 1UL;

            for (int i = 1; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "--steps": steps = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                    case "--warm": warm = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                    case "--bodies": bodies = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                    case "--seed": seed = ulong.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                    case "--threads": threadList = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                }
            }

            bool hashOnly = Array.IndexOf(args, "--hash") >= 0;

            // Through reflection so that this one program compiles against a tree that has no
            // Parallelism — which is how the pre-change state hash was taken.
            if (threadList > 0)
            {
                Type knob = typeof(World).Assembly.GetType("Evosim.Core.Parallelism");
                if (knob != null) knob.GetProperty("Threads").SetValue(null, threadList);
                else if (threadList > 1) Console.Error.WriteLine("this Core has no thread knob");
            }

            RunConfig config = RunConfigJson.Read(File.ReadAllText(configPath), out string mismatch);
            if (mismatch != null) Console.Error.WriteLine("config hash: " + mismatch);

            var world = new World(config, seed);

            Console.WriteLine("world:   " + config.WorldShape + " " +
                config.WorldAreaSquareMetres.ToString("0", CultureInfo.InvariantCulture) + " m2 x " +
                config.WorldDepthMetres.ToString("0", CultureInfo.InvariantCulture) + " m, field " +
                config.FieldModel + ", detritus cell " +
                config.FieldCellMetres.ToString(CultureInfo.InvariantCulture) + " m, matter cell " +
                config.FieldMatterCellMetres.ToString(CultureInfo.InvariantCulture) + " m");

            Describe("detritus", world.Nutrients);
            Describe("matter  ", world.Matter);

            Console.WriteLine("threads: " + threadList.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine();

            var crowd = new Crowd(world, seed ^ 0x5eedUL);

            crowd.Seed(bodies);

            if (hashOnly)
            {
                for (int i = 0; i < steps; i++) crowd.Step(MetabolicStep);

                Console.WriteLine(
                    "bodies " + world.Living.Count.ToString(CultureInfo.InvariantCulture) +
                    ", t " + world.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                Console.WriteLine("state hash: " + StateHash.Of(world));
                return 0;
            }

            for (int i = 0; i < warm; i++) crowd.Step(MetabolicStep);

            Console.WriteLine(
                "after warm-up: " + world.Living.Count.ToString(CultureInfo.InvariantCulture) +
                " bodies, t = " + world.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            Console.WriteLine();

            // ---- the whole step
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < steps; i++) crowd.Step(MetabolicStep);
            clock.Stop();

            double wholeMs = clock.Elapsed.TotalMilliseconds / steps;

            // ---- the passes, one at a time, on the world the run above left
            var passes = new List<(string Name, double Ms)>();
            IMatterField nutrients = world.Nutrients, matter = world.Matter;
            CurrentField current = config.Current;
            double t = world.ElapsedSeconds;

            passes.Add(("light  Advance+Solve", Time(steps, () =>
            {
                world.Field.Advance(t);
                world.Field.Solve();
            })));

            passes.Add(("det    Settle", Time(steps, () => nutrients.Settle(MetabolicStep))));
            passes.Add(("mat    Settle", Time(steps, () => matter.Settle(MetabolicStep))));

            passes.Add(("det    Remineralise", Time(steps, () =>
                nutrients.Remineralise(matter, MetabolicStep, config.RemineralisationPerSecond,
                    config.JoulesPerUnit))));

            passes.Add(("det    Mix", Time(steps, () =>
                nutrients.Mix(MetabolicStep, config.NutrientMixingDiffusivity,
                    config.HorizontalMixingDiffusivity))));

            passes.Add(("mat    Mix", Time(steps, () =>
                matter.Mix(MetabolicStep, config.MatterMixingDiffusivity,
                    config.MatterMixingDiffusivity))));

            passes.Add(("det    Advect", Time(steps, () =>
                nutrients.Advect(current, t, MetabolicStep, nutrients.PatchWidthMetres))));

            passes.Add(("mat    Advect", Time(steps, () =>
                matter.Advect(current, t, MetabolicStep, matter.PatchWidthMetres))));

            // The three legs inside the conservative transport, which is where most of Advect is.
            if (nutrients is GridField detGrid)
            {
                var legs = detGrid.ProfileTransport(current, t, MetabolicStep, steps);
                passes.Add(("  det  .SampleEdges", legs.SampleMs));
                passes.Add(("  det  .AssembleFaces", legs.AssembleMs));
                passes.Add(("  det  .LargestOutflow", legs.OutflowMs));
                passes.Add(("  det  .ApplyFaces", legs.ApplyMs));

                // D105's hoist, on and off, on the one grid: the same arithmetic either way, so
                // the difference is the whole of what it bought. See
                // GridField.PrecomputeStreamsTerms.
                if (detGrid.PrecomputeStreamsTerms)
                {
                    detGrid.PrecomputeStreamsTerms = false;
                    var unhoisted = detGrid.ProfileTransport(current, t, MetabolicStep, steps);
                    detGrid.PrecomputeStreamsTerms = true;
                    passes.Add(("  det  .SampleEdges unhoisted", unhoisted.SampleMs));
                }
            }

            double fieldTotal = 0d;
            foreach ((string name, double ms) in passes)
            {
                if (name.StartsWith("  ", StringComparison.Ordinal)) continue;
                fieldTotal += ms;
            }

            Console.WriteLine("World.Step(0.5) ............ " + Fmt(wholeMs) + " ms");
            Console.WriteLine();
            foreach ((string name, double ms) in passes)
            {
                Console.WriteLine(
                    "  " + name.PadRight(24, '.') + " " + Fmt(ms) + " ms   " +
                    (100d * ms / wholeMs).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            }
            Console.WriteLine();
            Console.WriteLine("  field passes ............. " + Fmt(fieldTotal) + " ms   " +
                (100d * fieldTotal / wholeMs).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            Console.WriteLine("  everything else .......... " + Fmt(wholeMs - fieldTotal) + " ms   " +
                (100d * (wholeMs - fieldTotal) / wholeMs).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            Console.WriteLine();
            Console.WriteLine("bodies at the end: " +
                world.Living.Count.ToString(CultureInfo.InvariantCulture));

            return 0;
        }

        private static void Describe(string what, IMatterField field)
        {
            if (field is GridField grid)
            {
                Console.WriteLine(
                    "  " + what + ": " + grid.CellsX + " x " + grid.CellsY + " x " + grid.CellsZ +
                    " = " + grid.CellCount.ToString(CultureInfo.InvariantCulture) + " cells, " +
                    grid.LiveCellCount.ToString(CultureInfo.InvariantCulture) + " live");
            }
        }

        private static double Time(int iterations, Action action)
        {
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            clock.Stop();
            return clock.Elapsed.TotalMilliseconds / iterations;
        }

        private static string Fmt(double ms) =>
            ms.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(8);

        /// <summary>
        /// Founders, scattered, and kept where this program put them.
        /// </summary>
        /// <remarks>
        /// The harness is what tells Core where a body is, and there is no harness here, so this
        /// stands in for one: every living body is observed once per metabolic step at a place
        /// drawn once, deterministically, from its own id. Nothing moves, which is the point —
        /// the clock is reading the fields and the economy and not a solver.
        /// </remarks>
        private sealed class Crowd
        {
            private readonly World _world;
            private readonly Dictionary<long, Float3> _places = new Dictionary<long, Float3>();
            private readonly ulong _seed;
            private ulong _draw;

            public Crowd(World world, ulong seed)
            {
                _world = world;
                _seed = seed;
            }

            public void Seed(int wanted)
            {
                RunConfig config = _world.Config;
                var rng = new Rng(Rng.SeedFor(_seed, 7UL));

                int made = 0, tries = 0;

                while (made < wanted && tries < wanted * 20)
                {
                    tries++;

                    Genome genome = GenomeFactory.Founder(rng, config.Genome, config.SensorPool());

                    try
                    {
                        _world.Inoculate(genome, 1, -rng.Range(1f, config.WorldDepthMetres - 1f));
                        made++;
                    }
                    catch (ArgumentException)
                    {
                        // Under the newborn mass floor — draw another. The floor does the same.
                    }
                }

                Place();
            }

            public void Step(float seconds)
            {
                Place();
                _world.Step(seconds);
            }

            private void Place()
            {
                IReadOnlyList<Organism> living = _world.Living;

                for (int i = 0; i < living.Count; i++)
                {
                    Organism creature = living[i];

                    if (!_places.TryGetValue(creature.Id, out Float3 at))
                    {
                        at = Draw();
                        _places[creature.Id] = at;
                    }

                    _world.Observe(creature, at, 0f);
                }
            }

            private Float3 Draw()
            {
                var rng = new Rng(Rng.SeedFor(_seed, _draw++));

                RunConfig config = _world.Config;
                float depth = config.WorldDepthMetres;

                if (config.WorldShape == WorldShape.Tank)
                {
                    float radius = _world.TankRadiusMetres;
                    // Uniform over the disc, and a metre in from the glass so nothing sits on it.
                    float r = (float)(Math.Sqrt(rng.Range(0f, 1f)) * Math.Max(0f, radius - 1f));
                    float theta = rng.Range(0f, (float)(2.0 * Math.PI));
                    return new Float3(
                        (float)(radius + r * Math.Cos(theta)),
                        -rng.Range(1f, depth - 1f),
                        (float)(radius + r * Math.Sin(theta)));
                }

                return new Float3(
                    rng.Range(0f, _world.Nutrients.LengthMetres),
                    -rng.Range(1f, depth - 1f),
                    rng.Range(0f, _world.Nutrients.WidthMetres));
            }
        }
    }
}
