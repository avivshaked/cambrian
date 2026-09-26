using System;
using System.IO;
using Evosim.Core;

// Reads each converted inoculum with this build's reader, asks that every offset is 0 and that
// it develops, and writes it back with this build's writer (id kept) into args[1] for
// convert_inocula.py compare.
internal static class Program
{
    private static int Main(string[] args)
    {
        string inocula = args[0];
        string outDir = args[1];
        Directory.CreateDirectory(outDir);

        string[] files =
        {
            "bush-16-r41d-s2-15000.json",
            "growth-ledger-genome.json",
            "knot-16-jointed-r41b-s1-1632.json",
            "knot-16-r41c-s3-1341.json",
            "knot-9-r41c-s3-937.json",
            "r45s1-stomach-100.json",
        };

        int bad = 0;
        foreach (string name in files)
        {
            string text = File.ReadAllText(Path.Combine(inocula, name));
            Genome genome = GenomeJson.Read(text);
            long id = GenomeJson.ReadId(text);

            foreach (MorphNode node in genome.Nodes)
            {
                if (node.BuoyancyOffset != 0f) { bad++; Console.WriteLine($"{name}: offset {node.BuoyancyOffset}"); }
            }

            var issues = genome.Validate(null, buoyancyOffsetPriced: false);
            if (issues.Count > 0) { bad++; Console.WriteLine($"{name}: {string.Join("; ", issues)}"); }

            Phenotype body = Developer.Develop(genome);
            string written = GenomeJson.Write(genome, id: id);
            File.WriteAllText(Path.Combine(outDir, name), written);

            Console.WriteLine(
                $"{name}: id {id}, {genome.Nodes.Count} nodes, {body.PartCount} parts, " +
                $"text {(written == text ? "identical" : "reordered")}");
        }

        return bad == 0 ? 0 : 1;
    }
}
