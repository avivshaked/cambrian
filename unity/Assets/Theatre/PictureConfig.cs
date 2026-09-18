using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// A run's <c>config.json</c> read for a picture and for nothing else — §11 of
    /// <c>logbook/specs/snapshot-render-spec.md</c> (the owner's ruling, 2026-09-18).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a second reader exists at all.</b> <see cref="RunConfigJson"/> refuses a config
    /// missing a tunable, which is right for everything that simulates: a run measured under a
    /// value nobody set is a result filed under the wrong name. The first use of the snapshot
    /// render hit that rule on round 39 seed 1, whose config predates D098's economy group, and
    /// the owner ruled that a picture may read an old run tolerantly. Nothing here reaches a
    /// simulation. What a still frame needs is the shape of the water, its size, its floor and
    /// how many patches it holds, and every recorded run carries all of them.
    /// </para>
    /// <para>
    /// <b>It is tried second and never instead.</b> <see cref="RunRecord.LoadForPicture"/> asks
    /// the strict reader first, so a run this build recorded is read exactly as it was before
    /// this class existed, and the tolerant path leaves its mark on the log and on the label
    /// (<c>OLD-RUN READ</c>).
    /// </para>
    /// <para>
    /// <b>What it will not do.</b> It refuses a config with no world group, or one missing the
    /// shape, the area or the depth, naming the field: those decide where the camera stands and
    /// what the box is, and a frame drawn from a guess about them would be a picture of another
    /// world. Everything it does default is stated: an absent bed group is the flat bed that
    /// every config written before D092 meant, an absent patch count is one patch, and an absent
    /// development group is <see cref="DevelopmentLimits.Default"/>, which is what
    /// <see cref="RunConfig"/> itself would hold. The caller is handed the list.
    /// </para>
    /// <para>
    /// <b>The object it returns is not a run's config.</b> Every field the picture does not read
    /// is left at whatever <see cref="RunConfig"/>'s own initialiser holds, and its
    /// <see cref="RunConfig.Hash"/> is therefore meaningless. It exists to be read by
    /// <see cref="SnapshotCamera"/>, <c>TheatreRunner</c>'s furniture and
    /// <see cref="Developer"/>, and it must never be handed to anything that steps.
    /// </para>
    /// </remarks>
    public static class PictureConfig
    {
        /// <summary>
        /// Reads the handful of settings a still frame needs.
        /// </summary>
        /// <param name="runDirectory">A resolved run directory.</param>
        /// <param name="absent">
        /// What the file did not carry and what was assumed in its place, or null when it carried
        /// everything. Logged by the caller, never swallowed.
        /// </param>
        public static RunConfig Read(string runDirectory, out string absent)
        {
            string path = Path.Combine(runDirectory, "config.json");

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "No config.json in '" + runDirectory + "'. The shape of the water lives " +
                    "there and nowhere else a picture can read.", path);
            }

            JsonNode root = Json.Parse(File.ReadAllText(path));
            var missing = new List<string>();

            if (!root.Has("world"))
            {
                throw new FormatException(
                    "config.json has no 'world' group, so nothing in it says what shape or size " +
                    "the water was. Present: " + Keys(root) + ".");
            }

            JsonNode world = root["world"];

            var config = new RunConfig
            {
                SharedSpace = Bool(world, "sharedSpace", "world"),
                WorldAreaSquareMetres = Number(world, "worldAreaSquareMetres", "world"),
                WorldDepthMetres = Number(world, "worldDepthMetres", "world"),
                WorldShape = Shape(world),
            };

            // D092's floor. Absent means the flat bed, which is exactly what a config written
            // before the dials existed meant by saying nothing: World builds no BedShape unless
            // a relief or a tilt is above zero.
            if (root.Has("bed"))
            {
                JsonNode bed = root["bed"];

                config.BedReliefMetres = Optional(bed, "bedReliefMetres", 0f, missing, "bed.bedReliefMetres");
                config.BedTiltMetres = Optional(bed, "bedTiltMetres", 0f, missing, "bed.bedTiltMetres");
                config.BedScaleMetres = Optional(bed, "bedScaleMetres", 0f, missing, "bed.bedScaleMetres");
            }
            else
            {
                missing.Add("bed (the flat floor every world before D092 had)");
            }

            // The seams the box view stamps, and the rings the tank's does. One patch is what a
            // config written before the layout existed described.
            if (root.Has("patches"))
            {
                JsonNode patches = root["patches"];

                config.HorizontalPatches = Optional(patches, "horizontalPatches", 1f, missing, "patches.horizontalPatches");
                config.PatchesAcross = Optional(patches, "patchesAcross", 1f, missing, "patches.patchesAcross");
            }
            else
            {
                missing.Add("patches (one patch assumed)");
            }

            // Not in §11's list, and read anyway, because it decides the phenotype: the pruning
            // limits are what turn a genome into the body that is drawn. Defaulting them silently
            // would draw parts the run had pruned, which is the one kind of wrongness this mode
            // is not allowed to hide.
            if (root.Has("development"))
            {
                JsonNode limits = root["development"];

                config.Development = new DevelopmentLimits
                {
                    MaxParts = (int)Optional(limits, "maxParts", DevelopmentLimits.Default.MaxParts, missing, "development.maxParts"),
                    MaxDepth = (int)Optional(limits, "maxDepth", DevelopmentLimits.Default.MaxDepth, missing, "development.maxDepth"),
                    MinPartVolume = Optional(limits, "minPartVolume", DevelopmentLimits.Default.MinPartVolume, missing, "development.minPartVolume"),
                    MaxPartVolume = Optional(limits, "maxPartVolume", DevelopmentLimits.Default.MaxPartVolume, missing, "development.maxPartVolume"),
                    MinPartHalfExtent = Optional(limits, "minPartHalfExtent", DevelopmentLimits.Default.MinPartHalfExtent, missing, "development.minPartHalfExtent"),
                };
            }
            else
            {
                missing.Add("development (the default limits)");
            }

            // The guilds a body can carry. Taken from the file where it can be, because a run
            // whose cell types are not this build's is a run whose colours would be a guess; the
            // names are checked against the standard registry either way, since that is the one
            // Genome.Validate asks and the one TheatrePalette paints from.
            ReadTheCellTypes(root, config, missing);

            absent = missing.Count == 0 ? null : string.Join(", ", missing.ToArray());
            return config;
        }

        private static void ReadTheCellTypes(JsonNode root, RunConfig config, List<string> missing)
        {
            if (!root.Has("cellTypes"))
            {
                missing.Add("cellTypes (the standard registry's names)");
                return;
            }

            var unknown = new List<string>();

            foreach (JsonNode cell in root["cellTypes"].Items())
            {
                if (!cell.Has("id")) continue;

                string id = cell["id"].AsString();
                if (!CellTypeRegistry.Standard.Contains(id)) unknown.Add(id);
            }

            if (unknown.Count > 0)
            {
                // Named rather than worked around: a genome carrying such a cell is refused by
                // Genome.Validate, which reads the standard registry, so the reader is owed the
                // reason its bodies did not draw.
                missing.Add(
                    "cellTypes the standard registry does not carry (" +
                    string.Join(", ", unknown.ToArray()) + "), whose bodies cannot be drawn");
            }

            try
            {
                config.CellTypes = CellTypeJson.ReadRegistry(root["cellTypes"]);
            }
            catch (Exception e)
            {
                missing.Add("cellTypes unreadable (" + e.GetType().Name + "), standard names used");
            }
        }

        private static WorldShape Shape(JsonNode world)
        {
            string name = Text(world, "worldShape", "world");

            if (!Enum.TryParse(name, true, out WorldShape shape))
            {
                throw new FormatException(
                    "world.worldShape is '" + name + "', which this build does not know. The " +
                    "shapes are " + string.Join(", ", Enum.GetNames(typeof(WorldShape))) + ".");
            }

            return shape;
        }

        private static float Number(JsonNode group, string key, string name)
        {
            if (!group.Has(key)) throw Refuse(group, key, name);

            return group[key].AsFloat();
        }

        private static bool Bool(JsonNode group, string key, string name)
        {
            if (!group.Has(key)) throw Refuse(group, key, name);

            return group[key].AsBool();
        }

        private static string Text(JsonNode group, string key, string name)
        {
            if (!group.Has(key)) throw Refuse(group, key, name);

            return group[key].AsString();
        }

        /// <summary>A number the picture can do without, with what was used in its place recorded.</summary>
        private static float Optional(
            JsonNode group, string key, float fallback, List<string> missing, string name)
        {
            if (group.Has(key)) return group[key].AsFloat();

            missing.Add(
                name + " (" + fallback.ToString("0.####", CultureInfo.InvariantCulture) + " used)");

            return fallback;
        }

        private static FormatException Refuse(JsonNode group, string key, string name) =>
            new FormatException(
                "config.json's '" + name + "' group has no '" + key + "', and a picture cannot " +
                "be framed without it. Present: " + Keys(group) + ".");

        private static string Keys(JsonNode node)
        {
            var keys = new List<string>();
            foreach (string key in node.Keys()) keys.Add(key);

            keys.Sort(StringComparer.Ordinal);
            return keys.Count == 0 ? "nothing" : string.Join(", ", keys.ToArray());
        }
    }
}
