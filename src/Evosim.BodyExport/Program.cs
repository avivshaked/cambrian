using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Evosim.Core;

internal static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static IEnumerable<string> Lines(string path)
    {
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Stream s = path.EndsWith(".gz", StringComparison.Ordinal) ? new GZipStream(fs, CompressionMode.Decompress) : (Stream)fs;
        using var r = new StreamReader(s);
        string line;
        while ((line = r.ReadLine()) != null) if (line.Length > 0) yield return line;
    }

    // SnapshotWorld.DevelopWithPlan's rule, repeated.
    private static Phenotype DevelopWithPlan(Genome genome, RunConfig config, int[] counts, List<int[]> lost)
    {
        if (counts == null && lost == null) return Developer.Develop(genome, config.Development, null, config.Shapes);
        var paths = new List<int[]>();
        Phenotype phenotype = Developer.Develop(genome, config.Development, null, config.Shapes, counts, paths);
        if (lost == null || lost.Count == 0) return phenotype;
        var drop = new bool[phenotype.PartCount];
        bool any = false;
        for (int i = 0; i < phenotype.PartCount; i++)
            foreach (int[] path in lost)
                if (paths[i] != null && paths[i].SequenceEqual(path)) { drop[i] = true; any = true; break; }
        return any ? phenotype.WithoutSubtrees(drop, out _) : phenotype;
    }

    private static Float3 Rotate(Quat q, Float3 v)
    {
        double ux = q.X, uy = q.Y, uz = q.Z, w = q.W;
        double cx = uy * v.Z - uz * v.Y + w * v.X, cy = uz * v.X - ux * v.Z + w * v.Y, cz = ux * v.Y - uy * v.X + w * v.Z;
        double dx = uy * cz - uz * cy, dy = uz * cx - ux * cz, dz = ux * cy - uy * cx;
        return new Float3((float)(v.X + 2 * dx), (float)(v.Y + 2 * dy), (float)(v.Z + 2 * dz));
    }

    private static string F(double v) => v.ToString("0.#####", Inv);

    private static int Main(string[] args)
    {
        string outDir = null, label = null;
        int minParts = 6;
        var runs = new List<(string label, string dir)>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": outDir = args[++i]; break;
                case "--min-parts": minParts = int.Parse(args[++i], Inv); break;
                case "--label": label = args[++i]; break;
                case "--run": runs.Add((label, args[++i])); break;
                default: Console.Error.WriteLine("unknown option " + args[i]); return 2;
            }
        }
        Directory.CreateDirectory(outDir);
        using var metrics = new StreamWriter(Path.Combine(outDir, "metrics.tsv"));
        using var partsOut = new StreamWriter(Path.Combine(outDir, "parts.jsonl"));
        metrics.WriteLine(string.Join('\t', "run", "id", "snap", "parts", "joints", "jointKinds", "mirrored", "cellTypes", "cells", "shapes", "depth", "reach", "bx", "by", "bz", "volume", "pruned"));

        foreach (var (lab, dir) in runs)
        {
            RunConfig config = RunConfigJson.Read(File.ReadAllText(Path.Combine(dir, "config.json")), out string mismatch);
            var last = new Dictionary<long, (int snap, string row)>();
            foreach (string file in Directory.GetFiles(Path.Combine(dir, "snapshots")).OrderBy(f => f, StringComparer.Ordinal))
            {
                int snap = int.Parse(Path.GetFileName(file).Substring(0, 9), Inv);
                foreach (string line in Lines(file)) last[GenomeJson.ReadId(line)] = (snap, line);
            }
            var pool = new Dictionary<long, string>();
            foreach (string line in Lines(Path.Combine(dir, "genomes.jsonl.gz")))
            {
                long id = GenomeJson.ReadId(line);
                if (last.ContainsKey(id)) pool[id] = line;
            }
            int missing = 0, failed = 0, written = 0;
            string firstFail = null;
            foreach (var kv in last.OrderBy(k => k.Key))
            {
                if (!pool.TryGetValue(kv.Key, out string gtext)) { missing++; continue; }
                Phenotype p;
                try
                {
                    Genome g = GenomeJson.Read(gtext);
                    p = DevelopWithPlan(g, config, GenomeJson.ReadModuleCounts(kv.Value.row), GenomeJson.ReadLostPartPaths(kv.Value.row));
                }
                catch (Exception e) { failed++; firstFail ??= kv.Key + ": " + e.Message; continue; }
                if (p.PartCount == 0) continue;

                int joints = 0, mirrored = 0, depth = 0;
                double reach = 0, volume = 0;
                var kinds = new SortedSet<string>(); var cellCount = new SortedDictionary<string, int>(); var shapeCount = new SortedDictionary<string, int>();
                double minX = 1e9, minY = 1e9, minZ = 1e9, maxX = -1e9, maxY = -1e9, maxZ = -1e9;
                foreach (PhenotypePart part in p.Parts)
                {
                    if (part.JointType != JointType.Fixed && part.ParentIndex >= 0) { joints++; kinds.Add(part.JointType.ToString()); }
                    if (part.Mirrored) mirrored++;
                    depth = Math.Max(depth, part.Depth);
                    volume += part.Volume;
                    cellCount[part.CellTypeId] = cellCount.TryGetValue(part.CellTypeId, out int c) ? c + 1 : 1;
                    shapeCount[part.ShapeId] = shapeCount.TryGetValue(part.ShapeId, out int s) ? s + 1 : 1;
                    Float3 h = part.HalfExtents;
                    for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Float3 r = Rotate(part.Rotation, new Float3(sx * h.X, sy * h.Y, sz * h.Z));
                        double x = part.Position.X + r.X, y = part.Position.Y + r.Y, z = part.Position.Z + r.Z;
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
                        reach = Math.Max(reach, Math.Sqrt(x * x + y * y + z * z));
                    }
                }
                int pruned = p.PrunedForVolume + p.PrunedForDepth + p.PrunedForParts + p.PrunedForReach;
                metrics.WriteLine(string.Join('\t', lab, kv.Key.ToString(Inv), kv.Value.snap.ToString(Inv), p.PartCount.ToString(Inv), joints.ToString(Inv),
                    string.Join("+", kinds), mirrored.ToString(Inv), cellCount.Count.ToString(Inv),
                    string.Join(",", cellCount.Select(x => x.Key + ":" + x.Value)), string.Join(",", shapeCount.Select(x => x.Key + ":" + x.Value)),
                    depth.ToString(Inv), F(reach), F(maxX - minX), F(maxY - minY), F(maxZ - minZ), F(volume), pruned.ToString(Inv)));

                if (p.PartCount < minParts) continue;
                var sb = new StringBuilder();
                sb.Append("{\"run\":\"").Append(lab).Append("\",\"id\":").Append(kv.Key.ToString(Inv)).Append(",\"snap\":").Append(kv.Value.snap.ToString(Inv)).Append(",\"parts\":[");
                for (int i = 0; i < p.PartCount; i++)
                {
                    PhenotypePart part = p.Parts[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"i\":").Append(i.ToString(Inv)).Append(",\"parent\":").Append(part.ParentIndex.ToString(Inv))
                      .Append(",\"node\":").Append(part.SourceNode.ToString(Inv)).Append(",\"depth\":").Append(part.Depth.ToString(Inv))
                      .Append(",\"cell\":\"").Append(part.CellTypeId).Append("\",\"shape\":\"").Append(part.ShapeId)
                      .Append("\",\"joint\":\"").Append(part.JointType.ToString()).Append("\",\"mirrored\":").Append(part.Mirrored ? "true" : "false")
                      .Append(",\"pos\":[").Append(F(part.Position.X)).Append(',').Append(F(part.Position.Y)).Append(',').Append(F(part.Position.Z))
                      .Append("],\"half\":[").Append(F(part.HalfExtents.X)).Append(',').Append(F(part.HalfExtents.Y)).Append(',').Append(F(part.HalfExtents.Z))
                      .Append("],\"rot\":[").Append(F(part.Rotation.X)).Append(',').Append(F(part.Rotation.Y)).Append(',').Append(F(part.Rotation.Z)).Append(',').Append(F(part.Rotation.W))
                      .Append("],\"canchor\":[").Append(F(part.ChildAnchorLocal.X)).Append(',').Append(F(part.ChildAnchorLocal.Y)).Append(',').Append(F(part.ChildAnchorLocal.Z))
                      .Append("],\"panchor\":[").Append(F(part.ParentAnchorLocal.X)).Append(',').Append(F(part.ParentAnchorLocal.Y)).Append(',').Append(F(part.ParentAnchorLocal.Z))
                      .Append("],\"anchor\":[");
                    Float3 an = Rotate(part.Rotation, part.ChildAnchorLocal);
                    sb.Append(F(part.Position.X + an.X)).Append(',').Append(F(part.Position.Y + an.Y)).Append(',').Append(F(part.Position.Z + an.Z)).Append("]}");
                }
                sb.Append("]}");
                partsOut.WriteLine(sb.ToString());
                written++;
            }
            Console.WriteLine($"{lab}: bodies {last.Count}, missing genome {missing}, failed {failed}, parts written {written}" + (mismatch != null ? ", config hash mismatch " + mismatch : "") + (firstFail != null ? ", first failure " + firstFail : ""));
        }
        return 0;
    }
}

