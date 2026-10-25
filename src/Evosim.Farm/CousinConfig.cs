using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// D201's cousin resume: a checkpoint carried on under an edited copy of its source run's
    /// config, where only the dials a screen turns may differ. <c>EVOSIM_RESUME_CONFIG</c> names
    /// the edited file and <c>EVOSIM_RESUME_TRICKLE_OFF</c> turns the founding trickle off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Refused by default.</b> A field may differ from the source's only when its group is one
    /// of <see cref="AllowedGroups"/> or it is the cell-type registry; the trickle's rate and its
    /// pool share may fall to 0 under the trickle switch. Anything else (the world's size, its seeds and landscape, its
    /// layout, development, physics) builds a world the checkpoint was not taken in, so every
    /// such field is named and the resume refused. The set widens only when a screen needs a dial
    /// (the coordinator's ruling of 2026-10-18).
    /// </para>
    /// <para>
    /// <b>What the run is.</b> A cousin of its source, never its continuation: the manifest keeps
    /// the source's config hash beside the cousin's own and lists every field that changed, and the
    /// run directory's config.json is the cousin's.
    /// </para>
    /// </remarks>
    public static class CousinConfig
    {
        /// <summary>The groups whose dials a cousin may change.</summary>
        public static readonly IReadOnlyList<string> AllowedGroups = new[] { "economy", "mouth" };

        /// <summary>
        /// The config a cousin runs under: the edited file at <paramref name="editedPath"/> (the
        /// source's own config when it is null), with the trickle off under
        /// <paramref name="trickleOff"/>, checked field by field against <paramref name="source"/>.
        /// </summary>
        /// <param name="changes">Every field that differs, as <c>path: source -> cousin</c>.</param>
        public static RunConfig Build(
            RunConfig source, string editedPath, bool trickleOff, out IReadOnlyList<string> changes)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            RunConfig cousin;
            if (string.IsNullOrEmpty(editedPath))
            {
                cousin = RunConfigJson.Read(RunConfigJson.Write(source), out _);
            }
            else
            {
                if (!File.Exists(editedPath))
                {
                    throw new FileNotFoundException("EVOSIM_RESUME_CONFIG names no file.", editedPath);
                }

                // Its stored hash is stale by construction, since the file was edited; the hash
                // this run records is the one its settings give. Said, never silently passed over.
                cousin = RunConfigJson.Read(File.ReadAllText(editedPath), out string stale);
                if (stale != null) FarmConsole.Error.WriteLine("note: " + editedPath + " " + stale);
            }

            // The trickle's pool share goes with it: the world refuses a share of a trickle that
            // never runs (D117), and the pool itself stays pinned and loaded, as the source's.
            if (trickleOff)
            {
                cousin.FoundingTricklePerSecond = 0f;
                cousin.FoundingTricklePoolShare = 0f;
            }

            var changed = new List<string>();
            var refused = new List<string>();

            IReadOnlyList<TunableEntry> a = ConfigSchema.Of(source);
            IReadOnlyList<TunableEntry> b = ConfigSchema.Of(cousin);
            if (a.Count != b.Count) throw new InvalidOperationException("The two configs' schemas differ.");

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Path != b[i].Path) throw new InvalidOperationException("The two configs' schemas differ at " + a[i].Path + ".");

                string was = a[i].Format(), now = b[i].Format();
                if (was == now) continue;

                string line = a[i].Group + "." + a[i].Key + ": " + was + " -> " + now;
                changed.Add(line);

                bool allowed = Contains(AllowedGroups, a[i].Group) ||
                               (trickleOff && a[i].Path == nameof(RunConfig.FoundingTricklePerSecond) &&
                                cousin.FoundingTricklePerSecond == 0f) ||
                               (trickleOff && a[i].Path == nameof(RunConfig.FoundingTricklePoolShare) &&
                                cousin.FoundingTricklePoolShare == 0f);
                if (!allowed) refused.Add(line);
            }

            if (source.CellTypes.HashContribution() != cousin.CellTypes.HashContribution())
            {
                changed.AddRange(CellTypeChanges(source.CellTypes, cousin.CellTypes));
            }

            if (source.Shapes.HashContribution() != cousin.Shapes.HashContribution())
            {
                const string line = "shapes: the registry differs";
                changed.Add(line);
                refused.Add(line);
            }

            if (refused.Count > 0)
            {
                throw new InvalidOperationException(
                    "A cousin resume may change only the " + string.Join(", ", AllowedGroups) +
                    " dials, the cell types and, under EVOSIM_RESUME_TRICKLE_OFF, the trickle's rate; " +
                    "this config also changes " + string.Join("; ", refused) + ".");
            }

            changes = changed;
            return cousin;
        }

        /// <summary>
        /// Each field of each cell type that differs, as <c>cellTypes.id.field: source -> cousin</c>,
        /// read from the two registries as the config file writes them. A registry whose ids differ
        /// in number or order is one line naming both lists.
        /// </summary>
        private static List<string> CellTypeChanges(CellTypeRegistry source, CellTypeRegistry cousin)
        {
            JsonNode a = Registry(source), b = Registry(cousin);
            var lines = new List<string>();
            var ia = new List<JsonNode>(a.Items());
            var ib = new List<JsonNode>(b.Items());

            var idsA = new List<string>(source.Ids());
            var idsB = new List<string>(cousin.Ids());
            if (string.Join(",", idsA) != string.Join(",", idsB))
            {
                lines.Add("cellTypes: " + string.Join(",", idsA) + " -> " + string.Join(",", idsB));
                return lines;
            }

            for (int i = 0; i < ia.Count; i++)
            {
                var keys = new SortedSet<string>(ia[i].Keys(), StringComparer.Ordinal);
                keys.UnionWith(ib[i].Keys());
                foreach (string key in keys)
                {
                    string was = ia[i].Has(key) ? Render(ia[i][key]) : "(none)";
                    string now = ib[i].Has(key) ? Render(ib[i][key]) : "(none)";
                    if (was != now) lines.Add("cellTypes." + idsA[i] + "." + key + ": " + was + " -> " + now);
                }
            }

            if (lines.Count == 0) lines.Add("cellTypes: the registry differs");
            return lines;
        }

        private static JsonNode Registry(CellTypeRegistry registry)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            CellTypeJson.WriteRegistry(w, "t", registry);
            w.EndObject();
            return Json.Parse(w.ToString())["t"];
        }

        private static string Render(JsonNode node)
        {
            switch (node.Kind)
            {
                case JsonNode.NodeKind.Number: return node.AsDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case JsonNode.NodeKind.String: return node.AsString();
                case JsonNode.NodeKind.Bool: return node.AsBool() ? "true" : "false";
                case JsonNode.NodeKind.Array:
                    var items = new List<string>();
                    foreach (JsonNode item in node.Items()) items.Add(Render(item));
                    return "[" + string.Join(",", items) + "]";
                case JsonNode.NodeKind.Object:
                    var fields = new List<string>();
                    foreach (string key in new SortedSet<string>(node.Keys(), StringComparer.Ordinal)) fields.Add(key + ":" + Render(node[key]));
                    return "{" + string.Join(",", fields) + "}";
                default: return "null";
            }
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
