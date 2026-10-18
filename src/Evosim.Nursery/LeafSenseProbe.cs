using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Evosim.Nursery
{
    /// <summary>
    /// The leaf tank smoke's read of D167's leaf smell (<c>logbook/specs/nursery-leaf-tank-plan.md</c>, step 7): every
    /// <see cref="EveryEconomySteps"/> economy steps, for each living member, the smell's strength at its root as a part there
    /// would read it, and the angle between the field's gradient and the way to the nearest leaf. One JSON line a member
    /// and sample, into the file <c>EVOSIM_LEAF_SENSE</c> names. Set only for a smoke; with it unset no episode builds one.
    /// </summary>
    /// <remarks>
    /// It is a watcher, so it only reads (<see cref="ITankWatcher"/>), and every episode of a run writes to the one file,
    /// each line with its episode's seed, under a lock. It reads the smell from the world's field directly, whatever the
    /// member's brain reads, so the spread does not depend on the brains drawn.
    /// </remarks>
    public sealed class LeafSenseProbe : ITankWatcher
    {
        public const int EveryEconomySteps = 10;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object Lock = new object();
        private static StreamWriter _writer;

        /// <summary>The file <c>EVOSIM_LEAF_SENSE</c> names, or null.</summary>
        public static readonly string Path = Environment.GetEnvironmentVariable("EVOSIM_LEAF_SENSE");

        private readonly ulong _seed;
        private int _steps;

        public LeafSenseProbe(ulong seed) { _seed = seed; }

        public void Begin(EpisodeProbe probe) { }

        public void Physics(EpisodeProbe probe) { }

        public void Metabolic(EpisodeProbe probe)
        {
            if (++_steps % EveryEconomySteps != 0) return;

            var leaves = new List<BodyView>();
            foreach (BodyView b in probe.Others()) if (b.Photosynthetic) leaves.Add(b);

            var lines = new List<string>();
            for (int m = 0; m < probe.MemberCount; m++)
            {
                if (!probe.Alive(m) || !probe.TryLinkPose(m, 0, out LinkPose root)) continue;
                if (!probe.TryLeafScent(root.X, root.Y, root.Z, out double strength, out double gx, out double gy, out double gz)) return;

                double best = double.PositiveInfinity, dx = 0, dy = 0, dz = 0;
                foreach (BodyView l in leaves)
                {
                    double ex = l.X - root.X, ey = l.Y - root.Y, ez = l.Z - root.Z, d = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                    if (d < best) { best = d; dx = ex; dy = ey; dz = ez; }
                }
                double g = Math.Sqrt(gx * gx + gy * gy + gz * gz);
                double angle = g > 0 && best > 0 && !double.IsInfinity(best)
                    ? Math.Acos(Math.Max(-1, Math.Min(1, (gx * dx + gy * dy + gz * dz) / (g * best)))) * 180 / Math.PI
                    : double.NaN;
                lines.Add(FormattableString.Invariant(
                    $"{{\"seed\":\"{_seed}\",\"t\":{probe.EconomySeconds:R},\"member\":{m},\"x\":{root.X:R},\"y\":{root.Y:R},\"z\":{root.Z:R},") +
                    FormattableString.Invariant(
                    $"\"strength\":{Num(strength)},\"nearest\":{Num(best)},\"angle\":{Num(angle)},\"leaves\":{leaves.Count}}}"));
            }

            lock (Lock)
            {
                if (_writer == null) _writer = new StreamWriter(Path, append: true) { AutoFlush = true };
                foreach (string line in lines) _writer.WriteLine(line);
            }
        }

        public void End(EpisodeProbe probe) { }

        private static string Num(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "null" : v.ToString("R", Inv);
    }
}
