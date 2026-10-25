// The open ocean's tracer screen, check 5 of logbook/specs/open-ocean-current.md: passive tracers released over
// one island, carried by a world's water for 6,000 s, and counted over the other islands, in two worlds side by
// side. Each world is named by a launcher block resolved to KEY=VALUE lines (a PowerShell dot-source of the block,
// written out), read by EnvBinding exactly as the farm reads it. The tool rebuilds only what the water and the map
// need, as World's constructor builds them from the landscape's seed: the floor, the reefs, the current and the
// islands' columns. No grid, no life.
//
// Usage: OceanTracers <settings.txt> [<settings.txt> ...] [--tracers N] [--seconds S] [--step S] [--threads T]
//                     [--starts a,b,c]
//        OceanTracers <settings.txt> --maps FIRST-LAST
//        OceanTracers <settings.txt> --coral [--seed S]
// --maps reads no water: for each landscape seed in the span it lists the islands as Core seeds them
// (CoralIslandColumns), by round 61's pick rule (scripts/reads/r61-maps.py): how many have 10 columns or more, the
// island columns' share of the water, the largest's share of them, the nearest two centres (the shortest way round
// the seams in a box), and each counted island's columns and nearest column centre to the edge or the glass.
// For each world, every island of 10 columns or more is a source in turn, at each start time; the tracers start
// uniform over the source's columns and uniform in the water above them. Printed per world: the islands' share of
// the square, then at 1,500 s, 3,000 s and the last second the share of tracers over the source, over another
// island and over the desert, the number of other islands holding 1% or more, and the RMS horizontal distance
// travelled. --coral builds the world itself (Core's World, life seed S, 6201 by default; the landscape is the
// block's) with EVOSIM_REEF_SHAPE coral unless the block names a shape, and reports D194's coral as the world grew
// it: colonies and pieces, island and desert colonies and canopy against their targets, the hulls' fade reach,
// the water within a fade, the grid's cells in rock, the fade's cost a call, and the run header's coral token.
// It sets the trickle pool's share to 0, since the pool is a file only the farm reads.
// Build with the Unity SDK's dotnet: dotnet build -c Release src/Evosim.OceanTracers; the exe lands
// under artifacts/Evosim.OceanTracers/.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Farm;

internal static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static int Main(string[] args)
    {
        // Every number printed is the invariant culture's, whatever the machine's.
        CultureInfo.DefaultThreadCurrentCulture = Inv;
        CultureInfo.CurrentCulture = Inv;

        var files = new List<string>();
        int tracers = 2000, threads = 4;
        double seconds = 6000d, step = 2d;
        double[] starts = { 0d, 20000d, 40000d };
        string maps = null;
        bool coral = false;
        ulong lifeSeed = 6201UL;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tracers": tracers = int.Parse(args[++i], Inv); break;
                case "--seconds": seconds = double.Parse(args[++i], Inv); break;
                case "--step": step = double.Parse(args[++i], Inv); break;
                case "--threads": threads = int.Parse(args[++i], Inv); break;
                case "--starts": starts = args[++i].Split(',').Select(s => double.Parse(s, Inv)).ToArray(); break;
                case "--maps": maps = args[++i]; break;
                case "--coral": coral = true; break;
                case "--seed": lifeSeed = ulong.Parse(args[++i], Inv); break;
                default: files.Add(args[i]); break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("usage: OceanTracers <settings.txt> ... [--tracers N] [--seconds S] [--step S] [--threads T] [--starts a,b]");
            return 2;
        }

        if (coral)
        {
            foreach (string file in files) Coral(file, lifeSeed);
            return 0;
        }

        if (maps != null)
        {
            string[] span = maps.Split('-');
            ulong first = ulong.Parse(span[0], Inv), last = ulong.Parse(span[span.Length - 1], Inv);
            foreach (string file in files)
            for (ulong seed = first; seed <= last; seed++)
            {
                Maps(file, seed);
            }

            return 0;
        }

        Console.WriteLine((
            $"tracers {tracers} a source, {seconds} s at a {step} s midpoint step, starts {string.Join(", ", starts)} s, {threads} threads"));

        foreach (string file in files)
        {
            Screen(file, tracers, seconds, step, threads, starts);
        }

        return 0;
    }

    private sealed class Water
    {
        public string Name;
        public RunConfig Config;
        public CurrentField Current;
        public BedShape Bed;
        public CoralIslandColumns Islands;
        public bool Tank;
        public float Radius;
        public float Length, Width;
        public float Depth;

        public double FloorY(double x, double z) => Bed == null ? -Depth : Bed.FloorY(x, z);

        /// <summary>The island under a place, folding a box's place into the square; −1 for none.</summary>
        public int IslandAt(double x, double z)
        {
            if (!Tank)
            {
                x -= Length * Math.Floor(x / Length);
                z -= Width * Math.Floor(z / Width);
            }

            int label = Islands.IslandAt(x, z);
            return label >= 0 ? label : -1;
        }
    }

    private static Dictionary<string, string> Settings(string file)
    {
        var values = new Dictionary<string, string>();
        foreach (string line in File.ReadAllLines(file))
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            values[line.Substring(0, eq).Trim().TrimStart('﻿')] = line.Substring(eq + 1).Trim();
        }

        return values;
    }

    private static Water Build(string file) => Build(file, Settings(file));

    private static Water Build(string file, Dictionary<string, string> values)
    {
        RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(values)));
        ulong landscape = config.TrySeedOverride("landscape", out ulong named) ? named : 0UL;

        var w = new Water { Name = Path.GetFileNameWithoutExtension(file), Config = config };
        w.Tank = config.WorldShape == WorldShape.Tank;
        w.Depth = config.WorldDepthMetres;

        int count = Math.Max(1, (int)config.HorizontalPatches);
        int across = w.Tank ? 1 : Math.Min(Math.Max(1, (int)config.PatchesAcross), count);
        float patch = (float)Math.Sqrt(config.WorldAreaSquareMetres / count);

        // As World's constructor: the radius, the box's sides, the floor, the reefs, the current.
        w.Radius = w.Tank ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
        w.Length = w.Tank ? 2f * w.Radius : patch * Math.Max(1, count / across);
        w.Width = w.Tank ? 2f * w.Radius : patch * across;

        w.Bed = w.Tank && (config.BedReliefMetres > 0f || config.BedTiltMetres > 0f)
            ? new BedShape(
                w.Radius, config.WorldDepthMetres, config.BedReliefMetres, config.BedTiltMetres,
                config.BedScaleMetres, Rng.SeedFor(landscape, World.BedShapeIndex),
                config.BedShoreDepthMetres, config.BedShoreFadeMetres)
            : !w.Tank && config.BedReliefMetres > 0f
                ? BedShape.ForBox(
                    w.Length, w.Width, config.WorldDepthMetres, config.BedReliefMetres, config.BedScaleMetres,
                    Rng.SeedFor(landscape, World.BedShapeIndex), config.BedShoreDepthMetres, config.BedShoreFadeMetres)
                : null;

        ReefGeometry reefs = config.ReefCover > 0f
            ? ReefGeometry.Place(config, w.Radius, w.Bed, Rng.SeedFor(landscape, World.ReefPlacementIndex))
            : null;

        w.Current = config.Current;
        w.Current.SetBox(
            patch, count, config.WorldDepthMetres, Rng.SeedFor(landscape, World.CurrentFieldIndex), across,
            config.WorldShape, w.Radius, w.Bed);
        w.Current.SetReefs(reefs);

        var map = new GradientNoise(Rng.SeedFor(landscape, World.IslandMapIndex), config.MatterIslandWavelengthMetres);
        CoralFootprint footprint = w.Tank ? CoralFootprint.Tank(w.Radius) : CoralFootprint.Box(w.Length, w.Width);

        w.Islands = CoralIslandColumns.Build(
            footprint, config.FieldMatterCellMetres, config.WorldDepthMetres, w.Bed, map.Fbm,
            config.MatterIslandCover, config.MatterIslandGlassMarginMetres);

        return w;
    }

    private static void Screen(string file, int tracers, double seconds, double step, int threads, double[] starts)
    {
        Water w = Build(file);
        CoralIslandColumns isl = w.Islands;
        double cell = isl.CellMetres;

        int columns = 0;
        for (int k = 0; k < isl.IslandCount; k++) columns += isl.IslandColumns(k).Count;
        double area = w.Tank ? Math.PI * w.Radius * w.Radius : (double)w.Length * w.Width;

        var sources = Enumerable.Range(0, isl.IslandCount).Where(k => isl.IslandColumns(k).Count >= 10).ToArray();

        Console.WriteLine();
        Console.WriteLine((
            $"== {w.Name}: {(w.Tank ? $"tank r {w.Radius:0.0} m" : $"square {w.Length:0} by {w.Width:0} m")}, depth {w.Depth} m, " +
            $"{w.Current}, {isl.IslandCount} islands ({sources.Length} of 10+ columns), islands {columns * cell * cell / area:0.0%} of the area"));

        double[] marks = { 1500d, 3000d, seconds };
        var sums = new double[marks.Length, 5];
        int runs = 0;

        foreach (double start in starts)
        foreach (int source in sources)
        {
            var rng = new Rng(Rng.SeedFor((ulong)source + 1UL, (ulong)(start + 1d)));
            IReadOnlyList<int> cols = isl.IslandColumns(source);

            var x = new double[tracers];
            var y = new double[tracers];
            var z = new double[tracers];
            var x0 = new double[tracers];
            var z0 = new double[tracers];

            for (int i = 0; i < tracers; i++)
            {
                int c = cols[(int)(rng.NextFloat() * cols.Count) % cols.Count];
                int ix = c / isl.CellsAcross, iz = c % isl.CellsAcross;
                x[i] = x0[i] = (ix + rng.NextFloat()) * cell;
                z[i] = z0[i] = (iz + rng.NextFloat()) * cell;
                double floor = w.FloorY(x[i], z[i]);
                y[i] = floor + rng.NextFloat() * -floor;
            }

            double t = start;
            int mark = 0;
            var options = new ParallelOptions { MaxDegreeOfParallelism = threads };

            while (mark < marks.Length)
            {
                double half = t + 0.5d * step;
                var kx = new double[tracers];
                var ky = new double[tracers];
                var kz = new double[tracers];

                // The midpoint rule on a pinned field: every sample of a pass is at one clock.
                w.Current.PinInstant(t);
                try
                {
                    Parallel.For(0, tracers, options, i =>
                    {
                        Float3 u = w.Current.VelocityAt((float)x[i], (float)y[i], (float)z[i], t);
                        kx[i] = x[i] + 0.5d * step * u.X;
                        ky[i] = y[i] + 0.5d * step * u.Y;
                        kz[i] = z[i] + 0.5d * step * u.Z;
                    });
                }
                finally { w.Current.UnpinInstant(); }

                w.Current.PinInstant(half);
                try
                {
                    Parallel.For(0, tracers, options, i =>
                    {
                        Float3 u = w.Current.VelocityAt((float)kx[i], (float)ky[i], (float)kz[i], half);
                        x[i] += step * u.X;
                        y[i] += step * u.Y;
                        z[i] += step * u.Z;
                        Keep(w, ref x[i], ref y[i], ref z[i]);
                    });
                }
                finally { w.Current.UnpinInstant(); }

                t += step;

                if (t - start >= marks[mark] - 1e-9)
                {
                    var held = new Dictionary<int, int>();
                    int atSource = 0, onOther = 0, desert = 0;
                    double travel = 0d;

                    for (int i = 0; i < tracers; i++)
                    {
                        int at = w.IslandAt(x[i], z[i]);
                        if (at == source) atSource++;
                        else if (at >= 0) { onOther++; held[at] = held.TryGetValue(at, out int n) ? n + 1 : 1; }
                        else desert++;

                        double dx = x[i] - x0[i], dz = z[i] - z0[i];
                        travel += dx * dx + dz * dz;
                    }

                    sums[mark, 0] += atSource / (double)tracers;
                    sums[mark, 1] += onOther / (double)tracers;
                    sums[mark, 2] += desert / (double)tracers;
                    sums[mark, 3] += held.Values.Count(n => n >= 0.01 * tracers);
                    sums[mark, 4] += Math.Sqrt(travel / tracers);
                    mark++;
                }
            }

            runs++;
        }

        for (int m = 0; m < marks.Length; m++)
        {
            Console.WriteLine((
                $"  at {marks[m],6:0} s: over the source {sums[m, 0] / runs:0.0%}, over another island {sums[m, 1] / runs:0.0%}, " +
                $"over the desert {sums[m, 2] / runs:0.0%}, other islands holding 1%+ {sums[m, 3] / runs:0.0}, " +
                $"RMS travel {sums[m, 4] / runs:0.0} m ({runs} releases)"));
        }
    }

    /// <summary>D194's coral as the world grows it from a block; see the usage at the top.</summary>
    private static void Coral(string file, ulong lifeSeed)
    {
        Dictionary<string, string> values = Settings(file);
        if (!values.ContainsKey("EVOSIM_REEF_SHAPE")) values["EVOSIM_REEF_SHAPE"] = "coral";
        RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(values)));
        string name = Path.GetFileNameWithoutExtension(file);

        // The trickle's pool is a file the farm reads and pins (D117); it touches nothing the coral reads.
        config.FoundingTricklePoolShare = 0f;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        World world;
        try
        {
            world = new World(config, lifeSeed);
        }
        catch (ArgumentException e)
        {
            Console.WriteLine($"{name}: the world refused the coral: {e.Message}");
            return;
        }

        double built = watch.Elapsed.TotalSeconds;
        CoralRock rock = world.Coral;
        if (rock == null)
        {
            Console.WriteLine($"{name}: no coral (ReefShape {config.ReefShape})");
            return;
        }

        CoralGeometry g = rock.Coral;
        bool tank = config.WorldShape == WorldShape.Tank;
        double length = tank ? 2d * world.TankRadiusMetres : world.BoxLengthMetres;
        double width = tank ? 2d * world.TankRadiusMetres : world.BoxWidthMetres;

        double target = 0d, got = 0d;
        int columns = 0;
        for (int i = 0; i < g.IslandCount; i++)
        {
            target += g.IslandTargetSamples(i);
            got += g.IslandGotSamples(i);
            columns += g.IslandColumnCount(i);
        }

        int island = Enumerable.Range(0, g.Count).Count(k => g.ColonyAt(k).OnIsland);
        double desert = g.DesertGotSamples / (got + g.DesertGotSamples);

        Console.WriteLine(
            $"{name}: built in {built:0.0} s; {g.Count} colonies ({island} on islands, {g.Count - island} in the desert) " +
            $"of {g.PieceCount} pieces over {g.IslandCount} islands of {columns} 1 m columns");
        Console.WriteLine(
            $"  island canopy {got / Math.Max(1d, target):0.00} of its target; the desert holds {desert:0.000} of the canopy " +
            $"(the islands {1d - desert:0.000} against a share of {config.CoralIslandShare} and a roof of {config.CoralIslandRoof})");
        for (int i = 0; i < g.IslandCount; i++)
        {
            int on = Enumerable.Range(0, g.Count).Count(k => g.ColonyAt(k).Island == i);
            Console.WriteLine(
                $"    island {i}: {g.IslandColumnCount(i)} columns, {on} colonies, canopy " +
                $"{g.IslandGotSamples(i) / Math.Max(1d, g.IslandTargetSamples(i)):0.00} of its target");
        }

        // The hulls as CoralRock builds them, and the fade's reach along 2,000 rays from each centre.
        var rng = new Rng(3UL);
        double meanReach = 0d, longestReach = 0d, pastHull = 0d, meanHeight = 0d;
        for (int c = 0; c < g.Count; c++)
        {
            g.Bounds(c, out double x0, out double y0, out double z0, out double x1, out double y1, out double z1);
            double ax = Math.Max(0.25d, 0.5d * (x1 - x0)), ay = Math.Max(0.25d, 0.5d * (y1 - y0)), az = Math.Max(0.25d, 0.5d * (z1 - z0));
            double least = Math.Max(ax, Math.Max(ay, az)) / CoralRock.HullAspect;
            ax = Math.Max(ax, least);
            ay = Math.Max(ay, least);
            az = Math.Max(az, least);
            meanHeight += (y1 - y0) / g.Count;

            double longest = 0d;
            for (int k = 0; k < 2000; k++)
            {
                double nx = 2d * rng.NextFloat() - 1d, ny = 2d * rng.NextFloat() - 1d, nz = 2d * rng.NextFloat() - 1d;
                double norm = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (!(norm > 0.1d && norm <= 1d)) continue;
                nx /= norm;
                ny /= norm;
                nz /= norm;
                double ux = nx / ax, uy = ny / ay, uz = nz / az;
                double c0 = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                double c1 = Math.Sqrt(ux * ux / (ax * ax) + uy * uy / (ay * ay) + uz * uz / (az * az));
                double reach = (1d + rock.FadeMetres * c1 / c0) / c0;
                longest = Math.Max(longest, reach);
                pastHull = Math.Max(pastHull, reach - 1d / c0);
            }

            meanReach += longest / g.Count;
            longestReach = Math.Max(longestReach, longest);
        }

        // The water within a fade and under a half, over points uniform in the water, and the readers' cost a call.
        int points = 0, inFade = 0, underHalf = 0;
        var sample = new List<(double X, double Y, double Z)>();
        while (points < 200000)
        {
            double x = rng.NextFloat() * length, z = rng.NextFloat() * width;
            double dx = x - length / 2, dz = z - width / 2;
            if (tank && Math.Sqrt(dx * dx + dz * dz) > length / 2) continue;
            double floorY = world.Bed != null ? world.Bed.FloorY(x, z) : -config.WorldDepthMetres;
            double y = floorY - rng.NextFloat() * floorY;
            points++;
            double f = rock.FadeAt(x, y, z);
            if (f < 1d) inFade++;
            if (f < 0.5d) underHalf++;
            if (sample.Count < 20000) sample.Add((x, y, z));
        }

        double sink = 0d;
        watch.Restart();
        foreach ((double X, double Y, double Z) q in sample)
        {
            rock.Fade(q.X, q.Y, q.Z, out ReefGeometry.Distance d);
            sink += d.S;
        }

        double perFade = watch.Elapsed.TotalMilliseconds * 1000d / sample.Count;
        watch.Restart();
        foreach ((double X, double Y, double Z) q in sample) sink += rock.SignedDistance(q.X, q.Y, q.Z, 1d, out _, out _);
        double perDistance = watch.Elapsed.TotalMilliseconds * 1000d / sample.Count;

        // What a link pays for its water at the same points, the fade included: the velocity and the acceleration
        // the farm asks at every link on every step, at a time well into the run.
        CurrentField current = config.Current;
        double perWater = double.NaN;
        if (current != null)
        {
            current.VelocityAt((float)sample[0].X, (float)sample[0].Y, (float)sample[0].Z, 12345d);
            watch.Restart();
            foreach ((double X, double Y, double Z) q in sample)
            {
                sink += current.VelocityAt((float)q.X, (float)q.Y, (float)q.Z, 12345d).X;
                sink += current.AccelerationAt((float)q.X, (float)q.Y, (float)q.Z, 12345d).X;
            }

            perWater = watch.Elapsed.TotalMilliseconds * 1000d / sample.Count;
        }

        int dead = 0;
        if (world.Nutrients is GridField grid)
        {
            double cell = grid.CellMetres;
            int nx = (int)Math.Round(length / cell), nz = (int)Math.Round(width / cell);
            int ny = (int)Math.Round(config.WorldDepthMetres / cell);
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                double cx = (ix + 0.5d) * cell, cz = (iz + 0.5d) * cell;
                if (!rock.FadeReachesColumn(cx, cz, 0d)) continue;
                for (int iy = 0; iy < ny; iy++)
                {
                    if (rock.Inside(cx, -((iy + 0.5d) * cell), cz) && !grid.IsLive(ix, iy, iz)) dead++;
                }
            }
        }

        Console.WriteLine(
            $"  hulls: colonies {meanHeight:0.0} m tall on the mean; the fade reaches {meanReach:0.0} m from a centre on " +
            $"the mean, {longestReach:0.0} m at most, and at most {pastHull:0.0} m past a hull along a ray");
        Console.WriteLine(
            $"  water: {inFade / (double)points:0.0000} of it within a fade, {underHalf / (double)points:0.0000} faded under " +
            $"a half; {dead} nutrient cells of {CellOf(world)} m in the rock, dead");
        Console.WriteLine(
            $"  cost on one thread: the fade with its derivatives {perFade:0.00} us a call, the distance at a 1 m limit " +
            $"{perDistance:0.00} us; a link's water, velocity and acceleration with the fade, {perWater:0.00} us " +
            $"(checksum {sink:0.###})");
        Console.WriteLine("  header" + Report.CoralToken(config, rock));
    }

    private static string CellOf(World world) =>
        world.Nutrients is GridField grid ? grid.CellMetres.ToString("0.##", Inv) : "?";

    /// <summary>One landscape seed's islands by round 61's pick rule; see the usage at the top.</summary>
    private static void Maps(string file, ulong seed)
    {
        Dictionary<string, string> values = Settings(file);
        values["EVOSIM_SEED_OVERRIDES"] = "landscape=" + seed.ToString(Inv);
        Water w = Build(file, values);
        CoralIslandColumns isl = w.Islands;
        double cell = isl.CellMetres;

        int water = 0;
        for (int ix = 0; ix < isl.CellsAlong; ix++)
        for (int iz = 0; iz < isl.CellsAcross; iz++)
        {
            double cx = (ix + 0.5d) * cell, cz = (iz + 0.5d) * cell;
            if (!w.Tank || Math.Sqrt((cx - w.Radius) * (cx - w.Radius) + (cz - w.Radius) * (cz - w.Radius)) <= w.Radius) water++;
        }

        int total = 0;
        var counted = new List<(int Columns, double X, double Z, double Edge)>();
        for (int k = 0; k < isl.IslandCount; k++)
        {
            IReadOnlyList<int> cols = isl.IslandColumns(k);
            total += cols.Count;
            if (cols.Count < 10) continue;

            // A box's island keeps clear of the seams by its margin, so its plain mean is its centre.
            double sx = 0d, sz = 0d, edge = double.MaxValue;
            foreach (int c in cols)
            {
                double cx = (c / isl.CellsAcross + 0.5d) * cell, cz = (c % isl.CellsAcross + 0.5d) * cell;
                sx += cx;
                sz += cz;
                double e = w.Tank
                    ? w.Radius - Math.Sqrt((cx - w.Radius) * (cx - w.Radius) + (cz - w.Radius) * (cz - w.Radius))
                    : Math.Min(Math.Min(cx, w.Length - cx), Math.Min(cz, w.Width - cz));
                edge = Math.Min(edge, e);
            }

            counted.Add((cols.Count, sx / cols.Count, sz / cols.Count, edge));
        }

        counted.Sort((a, b) => b.Columns.CompareTo(a.Columns));

        double gap = double.NaN;
        for (int i = 0; i < counted.Count; i++)
        for (int j = i + 1; j < counted.Count; j++)
        {
            double dx = Math.Abs(counted[i].X - counted[j].X), dz = Math.Abs(counted[i].Z - counted[j].Z);
            if (!w.Tank)
            {
                dx = Math.Min(dx, w.Length - dx);
                dz = Math.Min(dz, w.Width - dz);
            }

            double d = Math.Sqrt(dx * dx + dz * dz);
            if (double.IsNaN(gap) || d < gap) gap = d;
        }

        Console.WriteLine(
            $"landscape {seed}: {counted.Count} islands of 10+ columns, of {isl.IslandCount}; {total} island columns " +
            $"({total / (double)water:0.00} of the water); largest {(counted.Count > 0 ? counted[0].Columns / (double)total : 0d):0.00} " +
            $"of them; nearest centres {gap:0} m apart; " +
            string.Join(", ", counted.Select(c => $"{c.Columns} cols {c.Edge:0} m off the {(w.Tank ? "glass" : "edge")}")));
    }

    /// <summary>
    /// Keeps a tracer in the water: the surface and the floor by the field's own clamps, which read the nearest
    /// face; the glass by sliding back onto the rim, as a drifting corpse is. A box needs nothing: its place folds
    /// when it is read.
    /// </summary>
    private static void Keep(Water w, ref double x, ref double y, ref double z)
    {
        if (w.Tank)
        {
            double dx = x - w.Radius, dz = z - w.Radius;
            double r = Math.Sqrt(dx * dx + dz * dz);
            double rim = w.Radius - 0.01d;
            if (r > rim)
            {
                x = w.Radius + dx * rim / r;
                z = w.Radius + dz * rim / r;
            }
        }

        double floor = w.FloorY(x, z);
        if (y > 0d) y = 0d;
        else if (y < floor) y = floor;
    }
}
