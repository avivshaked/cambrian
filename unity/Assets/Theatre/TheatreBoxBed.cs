using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The sea bed of a box world, as the farm stepped it (D278), built once and kept on disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A box world's bed has about 1.5 m of relief, and <see cref="BedShape.ForBox"/> builds it in
    /// minutes in the Editor. A film that draws the floor flat at the box's depth hides a body that
    /// rests low and lets one that rests high seem to hover. With <c>EVOSIM_THEATRE_BOX_BED</c> at 1
    /// the film takes its bed from here, and the shaped-bed drawing that already exists
    /// (<c>TheatreSkin.Dress</c>) draws it. The dial is 0 by default and then nothing here runs.
    /// </para>
    /// <para>
    /// <b>The lines are World's own.</b> The landscape's seed is the config's override or the run's
    /// seed, the box's sides are the patch layout's, and the bed is built from them exactly as
    /// <c>TheatreCoralRock.PlaceFor</c> builds it to place the coral.
    /// </para>
    /// <para>
    /// <b>The cache is checked before it is kept.</b> A bed that was just built is written to a
    /// temporary name, read back, and compared with the built one at every point of a 1 m grid over
    /// the box, bit for bit; only then is the file given its final name. A file that fails the check
    /// stays under its temporary name and the built bed is used uncached. Nothing is deleted.
    /// </para>
    /// </remarks>
    internal static class TheatreBoxBed
    {
        private const int FormatVersion = 1;

        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>Whether the film draws the box's true bed: <c>EVOSIM_THEATRE_BOX_BED</c>, 0 by default, 1 for on.</summary>
        public static bool Enabled => TheatreSkin.Dial("EVOSIM_THEATRE_BOX_BED", 0f, 0f, 1f) >= 0.5f;

        // The same run opened again (a film's every scene opens its window) is the same bed.
        private static string _heldKey;
        private static BedShape _held;

        private static string Directory =>
            System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "theatre-cache"), "bed");

        /// <summary>
        /// The box bed of <paramref name="record"/>, or null when the dial is off, the world is a
        /// tank or the config asks for no relief. A failure to build the bed throws, as
        /// <see cref="BedShape.ForBox"/> does; a failure of the cache only means the bed is built.
        /// </summary>
        public static BedShape For(RunRecord record)
        {
            if (record == null || !Enabled) return null;

            RunConfig config = record.Config;
            if (config == null || config.WorldShape == WorldShape.Tank || !(config.BedReliefMetres > 0f)) return null;

            string heldKey = record.Path + "|" + record.Seed.ToString(CultureInfo.InvariantCulture);
            if (_heldKey == heldKey && _held != null) return _held;

            // World's own lines (D192): the landscape's seed is the config's override or the
            // world's seed, and the box's sides are the patch layout's.
            ulong landscape = config.TrySeedOverride("landscape", out ulong named) ? named : record.Seed;
            int count = Math.Max(1, (int)config.HorizontalPatches);
            int across = Math.Min(Math.Max(1, (int)config.PatchesAcross), count);
            float patch = (float)Math.Sqrt(config.WorldAreaSquareMetres / count);
            float length = patch * Math.Max(1, count / across);
            float width = patch * across;
            ulong bedSeed = Rng.SeedFor(landscape, World.BedShapeIndex);

            var ci = CultureInfo.InvariantCulture;
            string head =
                "[Theatre] box bed (D278): " + length.ToString("0.###", ci) + " x " + width.ToString("0.###", ci) +
                " m, relief " + config.BedReliefMetres.ToString("0.###", ci) + " m, seed " + bedSeed.ToString(ci) + "; ";

            string hex = null;
            string path = null;
            string why = null;
            try
            {
                hex = MakeKey(record, config, length, width, bedSeed);
                path = System.IO.Path.Combine(Directory, hex + ".bed");
            }
            catch (Exception e)
            {
                why = "the key could not be made: " + e.GetType().Name + ": " + e.Message;
            }

            if (path != null && File.Exists(path))
            {
                BedShape cached = TryRead(path, hex, out string readWhy);
                if (cached != null)
                {
                    Debug.Log(head + "from the cache " + hex.Substring(0, 8));
                    _heldKey = heldKey;
                    _held = cached;
                    return cached;
                }

                why = "the cached file was not used (" + readWhy + ")";
            }

            var clock = System.Diagnostics.Stopwatch.StartNew();
            BedShape bed = BedShape.ForBox(
                length, width, config.WorldDepthMetres, config.BedReliefMetres,
                config.BedScaleMetres, bedSeed,
                config.BedShoreDepthMetres, config.BedShoreFadeMetres);
            clock.Stop();
            string built = "built in " + (clock.ElapsedMilliseconds / 1000f).ToString("0.0", ci) + " s, ";

            // A final file that was there but unusable stays: it is never overwritten or deleted.
            if (path != null && why == null) why = TryWrite(path, hex, bed);

            if (why == null) Debug.Log(head + built + "cached " + hex.Substring(0, 8));
            else
            {
                Debug.Log(head + built + "not cached (" + why + ")");
                if (why.StartsWith("self-check", StringComparison.Ordinal)) Debug.LogWarning("[Theatre] box bed cache: " + why);
            }

            _heldKey = heldKey;
            _held = bed;
            return bed;
        }

        /// <summary>The key: a SHA-256 over everything <see cref="BedShape.ForBox"/> reads, the run's hashes and the loaded Core build.</summary>
        private static string MakeKey(RunRecord record, RunConfig config, float length, float width, ulong bedSeed)
        {
            var fields = new List<KeyValuePair<string, string>>();
            void Add(string name, object value) => fields.Add(new KeyValuePair<string, string>(name, Text(value)));

            Add("format", FormatVersion);
            Add("coreHash", record.CoreHash ?? "none");
            Add("configHash", record.ConfigHash ?? "none");
            Add("lengthMetres", length);
            Add("widthMetres", width);
            Add("depthMetres", config.WorldDepthMetres);
            Add("reliefMetres", config.BedReliefMetres);
            Add("scaleMetres", config.BedScaleMetres);
            Add("bedSeed", bedSeed);
            Add("shoreDepthMetres", config.BedShoreDepthMetres);
            Add("shoreFadeMetres", config.BedShoreFadeMetres);

            // The build: the digest of the Core sources this theatre compiled, which builds the bed. A module id would change
            // with every recompile of the same sources and never hit.
            Add("theatreCoreHash", BuildIdentity.CoreHash() ?? "none");

            var canonical = new StringBuilder();
            foreach (KeyValuePair<string, string> f in fields) canonical.Append(f.Key).Append('=').Append(f.Value).Append('\n');

            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false).GetBytes(canonical.ToString())))
                    .Replace("-", "").ToLowerInvariant();
            }
        }

        private static string Text(object value)
        {
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- the file

        /// <summary>
        /// Writes <paramref name="bed"/> to a temporary name, reads it back, compares the floor at
        /// every point of a 1 m grid, and renames it to <paramref name="path"/> if every point matches.
        /// Returns null on success, or the reason the bed is not cached.
        /// </summary>
        private static string TryWrite(string path, string hex, BedShape bed)
        {
            string temporary = path + ".tmp" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(temporary, Serialise(bed, hex), new UTF8Encoding(false));

                BedShape back = TryRead(temporary, hex, out string readWhy);
                if (back == null) return "self-check: the file could not be read back: " + readWhy;

                string differs = FirstDifference(bed, back);
                if (differs != null) return "self-check: " + differs;

                if (File.Exists(path)) return "a file of that name already exists";
                File.Move(temporary, path);
                return null;
            }
            catch (Exception e)
            {
                return "not written: " + e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>
        /// The first place on a 1 m grid over the box (and its far edges) where the two beds'
        /// <see cref="BedShape.FloorY"/> differ in any bit, as text, or null when none does.
        /// </summary>
        /// <remarks>
        /// <c>FloorY</c> takes world x and z in metres, and the box occupies x in 0..length and z in
        /// 0..width (the same frame <c>TheatreSkin.GroundUnder</c> asks in), so the grid is that
        /// rectangle, with its last row and column at the far edge.
        /// </remarks>
        private static string FirstDifference(BedShape a, BedShape b)
        {
            double length = a.LengthMetres;
            double width = a.WidthMetres;
            int nx = (int)Math.Ceiling(length);
            int nz = (int)Math.Ceiling(width);

            for (int ix = 0; ix <= nx; ix++)
            {
                double x = Math.Min((double)ix, length);
                for (int iz = 0; iz <= nz; iz++)
                {
                    double z = Math.Min((double)iz, width);
                    if (BitConverter.DoubleToInt64Bits(a.FloorY(x, z)) != BitConverter.DoubleToInt64Bits(b.FloorY(x, z)))
                    {
                        return "FloorY differs at (" + x.ToString("R", CultureInfo.InvariantCulture) + ", " +
                               z.ToString("R", CultureInfo.InvariantCulture) + ")";
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Every instance field by name, type and exact value: doubles and floats as their bits in
        /// hex, ints and bools as text, double arrays as comma-separated bits. Throws on any other type.
        /// </summary>
        private static string Serialise(BedShape bed, string hex)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(4096);
            sb.Append("format\t").Append(FormatVersion.ToString(ci)).Append('\n');
            sb.Append("key\t").Append(hex).Append('\n');

            FieldInfo[] fields = typeof(BedShape).GetFields(InstanceFields);
            Array.Sort(fields, (p, q) => string.CompareOrdinal(p.Name, q.Name));

            foreach (FieldInfo field in fields)
            {
                Type type = field.FieldType;
                object value = field.GetValue(bed);
                string text;

                if (type == typeof(double)) text = DoubleBits((double)value);
                else if (type == typeof(float)) text = FloatBits((float)value);
                else if (type == typeof(int)) text = ((int)value).ToString(ci);
                else if (type == typeof(bool)) text = (bool)value ? "1" : "0";
                else if (type == typeof(double[]))
                {
                    var array = (double[])value;
                    if (array == null) text = "null";
                    else
                    {
                        var parts = new string[array.Length];
                        for (int i = 0; i < parts.Length; i++) parts[i] = DoubleBits(array[i]);
                        text = "[" + string.Join(",", parts) + "]";
                    }
                }
                else throw new InvalidOperationException("BedShape field " + field.Name + " has type " + type.Name + ", which the cache cannot write");

                sb.Append(field.Name).Append('\t').Append(type.Name).Append('\t').Append(text).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// The bed in <paramref name="path"/>, or null with the reason. A field missing from the file,
        /// or a field in the file the type lacks, or one of another type, is a miss.
        /// </summary>
        private static BedShape TryRead(string path, string hex, out string why)
        {
            try
            {
                string[] lines = File.ReadAllText(path, Encoding.UTF8).Split('\n');
                var rows = new Dictionary<string, string[]>();
                bool formatOk = false, keyOk = false;

                foreach (string raw in lines)
                {
                    if (raw.Length == 0) continue;
                    string[] cells = raw.Split('\t');
                    if (cells.Length == 2 && cells[0] == "format")
                    {
                        if (cells[1] != FormatVersion.ToString(CultureInfo.InvariantCulture)) { why = "file format " + cells[1]; return null; }
                        formatOk = true;
                    }
                    else if (cells.Length == 2 && cells[0] == "key")
                    {
                        if (cells[1] != hex) { why = "the file's key differs"; return null; }
                        keyOk = true;
                    }
                    else if (cells.Length == 3)
                    {
                        rows[cells[0]] = cells;
                    }
                    else
                    {
                        why = "a malformed line";
                        return null;
                    }
                }

                if (!formatOk || !keyOk) { why = "no format or key line"; return null; }

                FieldInfo[] fields = typeof(BedShape).GetFields(InstanceFields);
                if (rows.Count != fields.Length)
                {
                    why = "the file has " + rows.Count + " fields, the type " + fields.Length;
                    return null;
                }

                var bed = (BedShape)FormatterServices.GetUninitializedObject(typeof(BedShape));
                foreach (FieldInfo field in fields)
                {
                    if (!rows.TryGetValue(field.Name, out string[] cells)) { why = "field " + field.Name + " is missing from the file"; return null; }
                    if (cells[1] != field.FieldType.Name) { why = "field " + field.Name + " is " + cells[1] + " in the file, " + field.FieldType.Name + " in the type"; return null; }

                    object value;
                    Type type = field.FieldType;
                    string text = cells[2];

                    if (type == typeof(double)) value = BitsDouble(text);
                    else if (type == typeof(float)) value = BitsFloat(text);
                    else if (type == typeof(int)) value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    else if (type == typeof(bool))
                    {
                        if (text != "0" && text != "1") { why = "field " + field.Name + " is not 0 or 1"; return null; }
                        value = text == "1";
                    }
                    else if (type == typeof(double[]))
                    {
                        if (text == "null") value = null;
                        else
                        {
                            if (text.Length < 2 || text[0] != '[' || text[text.Length - 1] != ']') { why = "field " + field.Name + " is not an array"; return null; }
                            string inner = text.Substring(1, text.Length - 2);
                            string[] parts = inner.Length == 0 ? new string[0] : inner.Split(',');
                            var array = new double[parts.Length];
                            for (int i = 0; i < parts.Length; i++) array[i] = BitsDouble(parts[i]);
                            value = array;
                        }
                    }
                    else { why = "field " + field.Name + " has type " + type.Name; return null; }

                    field.SetValue(bed, value);
                }

                why = null;
                return bed;
            }
            catch (Exception e)
            {
                why = "unreadable: " + e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        private static string DoubleBits(double value) =>
            BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);

        private static double BitsDouble(string text) =>
            BitConverter.Int64BitsToDouble(long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        private static string FloatBits(float value) =>
            BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("x8", CultureInfo.InvariantCulture);

        private static float BitsFloat(string text) =>
            BitConverter.ToSingle(BitConverter.GetBytes(int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture)), 0);
    }
}
