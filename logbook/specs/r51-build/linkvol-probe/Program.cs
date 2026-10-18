using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;


// Round 51's free first link: the adult volume of each body's first link (the lowest-index part
// carrying a joint) and of every link, read from round 50's snapshots.
class Program
{
    static int Main(string[] args)
    {
        foreach (string arm in args[0].Split(','))
        {
            string run = Directory.GetDirectories(Path.Combine("runs", arm)).OrderBy(d => d).Last();
            RunConfig config = RunDirectory.ReadConfig(run, out _);
            var first = new List<double>();
            var all = new List<double>();
            var firstShare = new List<double>();
            int bodies = 0, jointed = 0;
            foreach (double second in new[] { 10000.0, 20000.0, 30000.0 })
            {
                SnapshotRead read = RecordFiles.ReadSnapshot(run, second, 2);
                if (read == null) { Console.WriteLine(arm + " no snapshot at " + second); continue; }
                foreach (string row in read.Rows)
                {
                    Genome g = GenomeJson.Read(row);
                    int[] counts = GenomeJson.ReadModuleCounts(row);
                    Phenotype p = Developer.Develop(g, config.Development, null, config.Shapes, counts);
                    bodies++;
                    bool found = false;
                    double total = 0;
                    foreach (PhenotypePart part in p.Parts) total += part.Volume;
                    foreach (PhenotypePart part in p.Parts)
                    {
                        if (part.ParentIndex < 0 || part.JointType.DofCount() == 0) continue;
                        all.Add(part.Volume);
                        if (!found) { first.Add(part.Volume); firstShare.Add(part.Volume / total); found = true; }
                    }
                    if (found) jointed++;
                }
            }
            Console.WriteLine($"{arm}: bodies {bodies}, jointed {jointed}");
            Print("  first link m3", first);
            Print("  every link m3", all);
            Print("  first link share of body volume", firstShare);
        }
        return 0;
    }

    static void Print(string name, List<double> v)
    {
        if (v.Count == 0) { Console.WriteLine(name + ": none"); return; }
        v.Sort();
        double Q(double q) => v[(int)Math.Min(v.Count - 1, Math.Floor(q * v.Count))];
        Console.WriteLine(FormattableString.Invariant(
            $"{name}: n {v.Count}  p10 {Q(0.1):G3}  p25 {Q(0.25):G3}  median {Q(0.5):G3}  p75 {Q(0.75):G3}  p90 {Q(0.9):G3}  mean {v.Average():G3}"));
    }
}
