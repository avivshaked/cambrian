using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The grown coral of a recorded run, kept on disk so a second film of the same run builds
    /// neither the coral nor the landscape bed it stands on (<see cref="TheatreCoralRock.PlaceFor"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The key is strict and a hit is checked.</b> The file's name is a SHA-256 over the run's
    /// <c>coreHash</c> and <c>configHash</c>, both seeds, every config field the bed and the placer
    /// read, the loaded <c>Evosim.Core</c> assembly's module id (which changes with every recompile of
    /// the code that grows the coral) and <see cref="FormatVersion"/>. A coral read back is rebuilt
    /// through <see cref="CoralGeometry"/>'s public constructor and used only when its digest equals
    /// the one <c>run.json</c> recorded, the same test the grown coral's log line makes. A miss, in
    /// any form, costs only the growth the film pays without a cache.
    /// </para>
    /// <para>
    /// <b>Only a matching coral is written</b>, and through a temporary name that is renamed, so a
    /// killed render leaves no half file. The island records are not kept; the theatre reads none.
    /// </para>
    /// </remarks>
    internal static class TheatreCoralCache
    {
        /// <summary>The file format's version, in the key and in the file.</summary>
        private const int FormatVersion = 1;

        /// <summary>Whether the cache is read and written: <c>EVOSIM_THEATRE_CORAL_CACHE</c>, 1 by default, 0 for neither.</summary>
        public static bool Enabled => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_CACHE", 1f, 0f, 1f) >= 0.5f;

        /// <summary>A cache key: its fields in the order they were hashed, and the hash.</summary>
        internal sealed class Key
        {
            public readonly List<KeyValuePair<string, string>> Fields = new List<KeyValuePair<string, string>>();
            public string Hex;

            public string Short => Hex.Substring(0, 8);
            public string Path => System.IO.Path.Combine(Directory, Hex + ".json");
        }

        private static string Directory =>
            System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(BuildIdentity.RepositoryRoot(), "scratch"), "theatre-cache"), "coral");

        /// <summary>
        /// The key of <paramref name="record"/>'s coral grown from <paramref name="landscapeSeed"/>, or
        /// null (said in the log) when it cannot be made.
        /// </summary>
        public static Key MakeKey(RunRecord record, RunConfig config, ulong landscapeSeed)
        {
            try
            {
                var key = new Key();
                void Add(string name, object value) =>
                    key.Fields.Add(new KeyValuePair<string, string>(name, Text(value)));

                Add("format", FormatVersion);
                Add("coreHash", record.CoreHash ?? "none");
                Add("configHash", record.ConfigHash ?? "none");
                Add("seed", record.Seed);
                Add("landscapeSeed", landscapeSeed);

                // What PlaceFor's two branches read to make the footprint and the bed.
                Add("worldShape", config.WorldShape);
                Add("worldAreaSquareMetres", config.WorldAreaSquareMetres);
                Add("worldDepthMetres", config.WorldDepthMetres);
                Add("horizontalPatches", config.HorizontalPatches);
                Add("patchesAcross", config.PatchesAcross);
                Add("bedReliefMetres", config.BedReliefMetres);
                Add("bedTiltMetres", config.BedTiltMetres);
                Add("bedScaleMetres", config.BedScaleMetres);
                Add("bedShoreDepthMetres", config.BedShoreDepthMetres);
                Add("bedShoreFadeMetres", config.BedShoreFadeMetres);

                // What the placer and its refusal read beside the coral group.
                Add("reefShape", config.ReefShape);
                Add("reefCover", config.ReefCover);
                Add("fieldModel", config.FieldModel);
                Add("fieldMatterCellMetres", config.FieldMatterCellMetres);
                Add("matterIslandCover", config.MatterIslandCover);
                Add("matterIslandGlassMarginMetres", config.MatterIslandGlassMarginMetres);
                Add("matterIslandWavelengthMetres", config.MatterIslandWavelengthMetres);

                // The coral group.
                Add("coralIslandShare", config.CoralIslandShare);
                Add("coralIslandRoof", config.CoralIslandRoof);
                Add("coralMaxColonies", config.CoralMaxColonies);
                Add("coralMaxPiecesPerColony", config.CoralMaxPiecesPerColony);
                Add("coralMaxPieces", config.CoralMaxPieces);
                Add("coralTrunkHeightMinMetres", config.CoralTrunkHeightMinMetres);
                Add("coralTrunkHeightMaxMetres", config.CoralTrunkHeightMaxMetres);
                Add("coralTrunkRadiusMinMetres", config.CoralTrunkRadiusMinMetres);
                Add("coralTrunkRadiusMaxMetres", config.CoralTrunkRadiusMaxMetres);
                Add("coralChildrenMin", config.CoralChildrenMin);
                Add("coralChildrenMax", config.CoralChildrenMax);
                Add("coralTiltMeanDegrees", config.CoralTiltMeanDegrees);
                Add("coralTiltSpreadDegrees", config.CoralTiltSpreadDegrees);
                Add("coralLengthFactor", config.CoralLengthFactor);
                Add("coralRadiusFactor", config.CoralRadiusFactor);
                Add("coralMaxDepth", config.CoralMaxDepth);
                Add("coralMinRadiusMetres", config.CoralMinRadiusMetres);
                Add("coralPlateChance", config.CoralPlateChance);
                Add("coralPlateRadiusMinMetres", config.CoralPlateRadiusMinMetres);
                Add("coralPlateRadiusMaxMetres", config.CoralPlateRadiusMaxMetres);
                Add("coralPlateThicknessMetres", config.CoralPlateThicknessMetres);
                Add("coralFilletMetres", config.CoralFilletMetres);
                Add("coralFadeMetres", config.CoralFadeMetres);
                Add("coralFadeFloor", config.CoralFadeFloor);
                Add("coralTransmission", config.CoralTransmission);
                Add("coralGridCellMetres", config.CoralGridCellMetres);

                // The build: the module id changes with every recompile of the code that grows the coral.
                Add("evosimCoreModule", typeof(CoralGeometry).Assembly.ManifestModule.ModuleVersionId.ToString("N"));

                var canonical = new StringBuilder();
                foreach (KeyValuePair<string, string> f in key.Fields) canonical.Append(f.Key).Append('=').Append(f.Value).Append('\n');

                using (SHA256 sha = SHA256.Create())
                {
                    key.Hex = BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false).GetBytes(canonical.ToString())))
                        .Replace("-", "").ToLowerInvariant();
                }

                return key;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Theatre] coral cache: miss: the key could not be made: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// The cached coral for <paramref name="key"/>, or null when there is none, it is unreadable, its key
        /// fields differ, or its digest is not <paramref name="recordedDigest"/> (said in the log either way).
        /// </summary>
        public static CoralGeometry TryRead(Key key, string recordedDigest)
        {
            string path = key.Path;
            try
            {
                if (recordedDigest == null)
                    return Miss(key, "run.json records no coral digest, so a cached coral cannot be checked against it");
                if (!File.Exists(path)) return Miss(key, "no file");

                JsonNode root = Json.Parse(File.ReadAllText(path));
                if (root["format"].AsInt() != FormatVersion) return Miss(key, "file format " + root["format"].AsInt() + ", wanted " + FormatVersion);

                JsonNode stored = root["keyFields"];
                foreach (KeyValuePair<string, string> f in key.Fields)
                {
                    string had = stored.Has(f.Key) ? stored[f.Key].AsString() : "(absent)";
                    if (had != f.Value) return Miss(key, "key field " + f.Key + " differs: file " + had + ", now " + f.Value);
                }

                float fillet = (float)root["fillet"].AsDouble();
                JsonNode colonyRows = root["colonies"];
                JsonNode pieceRows = root["pieces"];
                var colonies = new CoralGeometry.Colony[colonyRows.Count];
                var pieces = new CoralGeometry.Piece[pieceRows.Count];

                for (int i = 0; i < colonies.Length; i++)
                {
                    JsonNode r = colonyRows[i];
                    colonies[i] = new CoralGeometry.Colony
                    {
                        X = r["x"].AsDouble(), Z = r["z"].AsDouble(), BaseY = r["baseY"].AsDouble(),
                        Island = r["island"].AsInt(), FirstPiece = r["first"].AsInt(), PieceCount = r["count"].AsInt(),
                        CanopySamples = r["canopy"].AsInt(),
                    };
                }

                for (int i = 0; i < pieces.Length; i++)
                {
                    JsonNode r = pieceRows[i];
                    pieces[i] = new CoralGeometry.Piece
                    {
                        Kind = (CoralGeometry.PieceKind)r["kind"].AsInt(), Colony = r["colony"].AsInt(),
                        Parent = r["parent"].AsInt(), Depth = r["depth"].AsInt(),
                        AX = r["ax"].AsDouble(), AY = r["ay"].AsDouble(), AZ = r["az"].AsDouble(),
                        BX = r["bx"].AsDouble(), BY = r["by"].AsDouble(), BZ = r["bz"].AsDouble(),
                        Radius = r["radius"].AsDouble(), HalfThickness = r["halfThickness"].AsDouble(),
                        TiltCos = r["tiltCos"].AsDouble(), TiltSin = r["tiltSin"].AsDouble(),
                        AzimuthCos = r["azimuthCos"].AsDouble(), AzimuthSin = r["azimuthSin"].AsDouble(),
                    };
                }

                var coral = new CoralGeometry(fillet, colonies, pieces);
                string ours = coral.Digest().ToString("x16", CultureInfo.InvariantCulture);
                if (!string.Equals(recordedDigest, ours, StringComparison.OrdinalIgnoreCase))
                    return Miss(key, "the file's coral has digest " + ours + ", run.json's is " + recordedDigest);

                Debug.Log(
                    "[Theatre] coral cache: hit " + key.Short + " (" + path + "): " + coral.Count + " colonies, " +
                    coral.PieceCount + " pieces, digest " + ours + " = run.json's");

                return coral;
            }
            catch (Exception e)
            {
                return Miss(key, "the file is unreadable: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static CoralGeometry Miss(Key key, string reason)
        {
            Debug.Log("[Theatre] coral cache: miss " + key.Short + ": " + reason);
            return null;
        }

        /// <summary>
        /// Writes <paramref name="coral"/> under <paramref name="key"/>, through a temporary name that is
        /// renamed. The caller has checked its digest against <c>run.json</c>'s. A failure is a warning.
        /// </summary>
        public static void Write(Key key, CoralGeometry coral)
        {
            string path = key.Path;
            string temporary = path + ".tmp" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
            try
            {
                System.IO.Directory.CreateDirectory(Directory);

                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(256 * 1024);
                sb.Append("{\n");
                sb.Append("  \"format\": ").Append(FormatVersion.ToString(ci)).Append(",\n");
                sb.Append("  \"key\": \"").Append(key.Hex).Append("\",\n");
                sb.Append("  \"keyFields\": {\n");
                for (int i = 0; i < key.Fields.Count; i++)
                {
                    sb.Append("    \"").Append(Escape(key.Fields[i].Key)).Append("\": \"").Append(Escape(key.Fields[i].Value)).Append('"')
                        .Append(i < key.Fields.Count - 1 ? ",\n" : "\n");
                }

                sb.Append("  },\n");
                sb.Append("  \"digest\": \"").Append(coral.Digest().ToString("x16", ci)).Append("\",\n");
                sb.Append("  \"fillet\": ").Append(coral.FilletMetres.ToString("R", ci)).Append(",\n");

                sb.Append("  \"colonies\": [\n");
                for (int i = 0; i < coral.Count; i++)
                {
                    CoralGeometry.Colony c = coral.ColonyAt(i);
                    sb.Append("    {\"x\": ").Append(D(c.X)).Append(", \"z\": ").Append(D(c.Z)).Append(", \"baseY\": ").Append(D(c.BaseY))
                        .Append(", \"island\": ").Append(c.Island.ToString(ci)).Append(", \"first\": ").Append(c.FirstPiece.ToString(ci))
                        .Append(", \"count\": ").Append(c.PieceCount.ToString(ci)).Append(", \"canopy\": ").Append(c.CanopySamples.ToString(ci))
                        .Append(i < coral.Count - 1 ? "},\n" : "}\n");
                }

                sb.Append("  ],\n");

                sb.Append("  \"pieces\": [\n");
                for (int i = 0; i < coral.PieceCount; i++)
                {
                    CoralGeometry.Piece p = coral.PieceAt(i);
                    sb.Append("    {\"kind\": ").Append(((int)p.Kind).ToString(ci)).Append(", \"colony\": ").Append(p.Colony.ToString(ci))
                        .Append(", \"parent\": ").Append(p.Parent.ToString(ci)).Append(", \"depth\": ").Append(p.Depth.ToString(ci))
                        .Append(", \"ax\": ").Append(D(p.AX)).Append(", \"ay\": ").Append(D(p.AY)).Append(", \"az\": ").Append(D(p.AZ))
                        .Append(", \"bx\": ").Append(D(p.BX)).Append(", \"by\": ").Append(D(p.BY)).Append(", \"bz\": ").Append(D(p.BZ))
                        .Append(", \"radius\": ").Append(D(p.Radius)).Append(", \"halfThickness\": ").Append(D(p.HalfThickness))
                        .Append(", \"tiltCos\": ").Append(D(p.TiltCos)).Append(", \"tiltSin\": ").Append(D(p.TiltSin))
                        .Append(", \"azimuthCos\": ").Append(D(p.AzimuthCos)).Append(", \"azimuthSin\": ").Append(D(p.AzimuthSin))
                        .Append(i < coral.PieceCount - 1 ? "},\n" : "}\n");
                }

                sb.Append("  ]\n}\n");

                File.WriteAllText(temporary, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(temporary);
                else File.Move(temporary, path);

                Debug.Log("[Theatre] coral cache: wrote " + key.Short + " (" + path + ")");
            }
            catch (Exception e)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception) { /* a stray temporary name is harmless */ }

                Debug.LogWarning("[Theatre] coral cache: not written: " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>A double written so that it reads back to the same bits.</summary>
        private static string D(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A key field's value as text, the same on every machine.</summary>
        private static string Text(object value)
        {
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
