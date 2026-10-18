// The anchoring cube's gap to the rock in a checkpoint: for every body that can grip, each
// anchoring link's contact sphere, its reef signed distance less its radius (the gap the contact
// pass would have to close to touch), and its touch flag. Read only; nothing is stepped.
// Usage: Evosim.CubeGap [--bodies] <checkpoint> [<checkpoint> ...]
// --bodies prints one line a body instead: its nearest link to the rock, and its grip counters.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm;

bool summary = args.Contains("--bodies");
foreach (string path in args.Where(a => a != "--bodies"))
{
    CheckpointHeader header = CheckpointReader.ReadHeader(path);
    string run = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(path)));
    RunConfig config = RunDirectory.ReadConfig(run, out _);
    var pool = TricklePoolFiles.Load(run, config);
    string scratch = Path.Combine(Path.GetDirectoryName(typeof(Simulation).Assembly.Location) ?? ".", "..", "cube-gap-run");
    Directory.CreateDirectory(scratch);

    var world = new World(config, header.Seed, pool?.Genomes, pool?.Weights);
    using var sim = new Simulation(world, header.Seed, header.PhysicsStepSeconds, header.StepsPerMetabolicStep, 1, scratch);
    using (CheckpointReader reader = CheckpointReader.Open(path))
    {
        BinaryReader r = reader.Reader;
        StateIo.Tag(r, "PAYL");
        world.ReadState(r);
        sim.ReadState(r);
    }

    sim.Dynamics.CommitContactSpheres();
    ReefGeometry reefs = sim.Dynamics.Config.Reefs;
    Console.WriteLine(Path.GetFileName(path) + " at " + world.ElapsedSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s");
    if (summary)
    {
        // One line a body that can grip: its nearest link's gap to the reefs, how many links
        // touch the rock at this step, and its grips held now, formed and broken since birth.
        Console.WriteLine("  body   links   nearest gap   nearest y   touching   held   formed   broken");
        foreach (Creature body in sim.Dynamics.Creatures)
        {
            if (!body.CanGrip) continue;
            double nearest = double.PositiveInfinity, nearestY = double.NaN;
            int touching = 0;
            for (int i = 0; i < body.Links; i++)
            {
                double x = body.LinkContactCentre[3 * i], y = body.LinkContactCentre[3 * i + 1], z = body.LinkContactCentre[3 * i + 2];
                double gap = reefs == null ? double.NaN : reefs.SignedDistance(x, y, z) - body.LinkContactRadius[i];
                if (gap < nearest) { nearest = gap; nearestY = y; }
                if (body.RockTouched != null && body.RockTouched[i]) touching++;
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,5} {1,6} {2,13:F3} {3,11:F3} {4,10} {5,6} {6,8} {7,8}",
                body.Id, body.Links, nearest, nearestY, touching, body.GripsHeld, body.GripsFormed, body.GripsBroken));
        }
        continue;
    }

    Console.WriteLine("  body   link   centre y   radius   reef gap   touched   held");
    foreach (Creature body in sim.Dynamics.Creatures)
    {
        if (!body.CanGrip) continue;
        for (int i = 0; i < body.Links; i++)
        {
            if (!body.Anchors[i]) continue;
            double x = body.LinkContactCentre[3 * i], y = body.LinkContactCentre[3 * i + 1], z = body.LinkContactCentre[3 * i + 2];
            double radius = body.LinkContactRadius[i];
            double gap = reefs == null ? double.NaN : reefs.SignedDistance(x, y, z) - radius;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,5} {1,5} {2,10:F3} {3,8:F3} {4,10:F3} {5,9} {6,6}",
                body.Id, i, y, radius, gap, body.RockTouched != null && body.RockTouched[i], body.GripHeld != null && body.GripHeld[i]));
        }
    }
}
