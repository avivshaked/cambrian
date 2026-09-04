using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Run settings to and from JSON — DESIGN.md §5A.10 and §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written indented, because this is the file a person opens to set up an experiment. It is
    /// the one artefact in a run directory meant to be edited by hand.
    /// </para>
    /// <para>
    /// <b>Both directions are derived from <see cref="ConfigSchema"/>.</b> A knob used to be
    /// written out four times — the property, <see cref="RunConfig.Hash"/>, the writer here and
    /// the reader here — and with around a hundred knobs that is four hundred sites nobody can
    /// hold in their head. Both faults §7 exists to catch came from exactly that:
    /// <c>DevelopmentLimits.MaxPartVolume</c> was in two of the four, and
    /// <see cref="RunConfig.Light"/> was in none (logbook/0011, logbook/0013). Now a knob that
    /// exists is written, and a knob that is written is required on load.
    /// </para>
    /// <para>
    /// <b>Loading refuses rather than defaults</b> (§9). A missing field throws and says what was
    /// present, because a config that loads with one value silently defaulted describes a run that
    /// never happened, and every result filed under its hash is mislabelled.
    /// </para>
    /// <para>
    /// <b>The hash is written alongside the settings, and checked on load.</b> If they disagree,
    /// the file was hand-edited — which is expected and allowed, since editing it is the point —
    /// but the stored hash is then stale. The load reports the discrepancy rather than silently
    /// recomputing, and the caller decides.
    /// </para>
    /// </remarks>
    public static class RunConfigJson
    {
        /// <summary>Bumped when the layout changes, not when a knob is added.</summary>
        /// <remarks>
        /// 2 since <see cref="ConfigSchema"/> took over: sections and keys are derived from
        /// <see cref="TunableAttribute.Group"/> and the property name rather than hand-placed, and
        /// the light model gained a section of its own. Adding a knob does not bump this — the
        /// reader refuses a file missing one, which is a clearer failure than a version number.
        /// </remarks>
        public const int FormatVersion = 2;

        public static string Write(RunConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            IReadOnlyList<TunableEntry> schema = ConfigSchema.Of(config);

            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("format", FormatVersion);

            // Written for the reader's benefit and checked on load. Not authoritative — the
            // settings below are.
            w.Field("configHash", config.Hash());

            foreach (string group in ConfigSchema.Groups(schema))
            {
                w.BeginObject(group);

                foreach (TunableEntry entry in schema)
                {
                    if (entry.Group == group) WriteValue(w, entry);
                }

                w.EndObject();
            }

            CellTypeJson.WriteRegistry(w, "cellTypes", config.CellTypes);

            w.EndObject();
            return w.ToString();
        }

        private static void WriteValue(Json.Writer w, TunableEntry entry)
        {
            switch (entry.Get())
            {
                case float f: w.Field(entry.Key, f); break;
                case int i: w.Field(entry.Key, i); break;
                case bool b: w.Field(entry.Key, b); break;

                case string[] a:
                    w.BeginArray(entry.Key);
                    foreach (string s in a) w.Value(s);
                    w.EndArray();
                    break;

                // A scalar enum, by name and as a string, for the same reason the array case
                // below gives. Written before the Array case because an enum is not an array and
                // would otherwise reach the throw.
                case Enum single: w.Field(entry.Key, single.ToString()); break;

                // Enums by name, per §9. An ordinal would silently re-point at a different member
                // the first time anyone inserts one, and the file would still load.
                case Array e when ConfigSchema.EnumElementOf(e.GetType()) != null:
                    w.BeginArray(entry.Key);
                    foreach (object v in e) w.Value(v.ToString());
                    w.EndArray();
                    break;

                default:
                    throw new InvalidOperationException(
                        $"{entry.Path} is a {entry.ValueType.Name}, which the config format cannot " +
                        "write. Add a case here and a matching one in ReadValue, or the knob is " +
                        "unsavable and the run cannot be reproduced from its own directory.");
            }
        }

        /// <param name="text">Contents of a <c>config.json</c>.</param>
        /// <param name="hashMismatch">
        /// Set when the stored hash does not match the settings that were read — see the class
        /// remarks. Not an error, but never something to ignore silently.
        /// </param>
        public static RunConfig Read(string text, out string hashMismatch)
        {
            JsonNode root = Json.Parse(text);

            int format = root["format"].AsInt();
            if (format != FormatVersion)
            {
                throw new FormatException(
                    $"Run settings are format {format}, this build reads {FormatVersion}.");
            }

            // Cell types are constructed rather than assigned into, so they cannot come from the
            // schema walk — and the walk needs a config to write into, so they are read first.
            var config = new RunConfig
            {
                CellTypes = CellTypeJson.ReadRegistry(root["cellTypes"]),
            };

            foreach (TunableEntry entry in ConfigSchema.Of(config))
            {
                entry.Set(ReadValue(entry, root[entry.Group][entry.Key]));
            }

            string stored = root["configHash"].AsString();
            string actual = config.Hash();

            hashMismatch = stored == actual
                ? null
                : $"Stored hash {stored} does not match the settings in this file ({actual}). " +
                  "The file was edited after it was written, which is fine — but results filed " +
                  "under the stored hash were produced by different settings.";

            return config;
        }

        private static object ReadValue(TunableEntry entry, JsonNode node)
        {
            if (entry.ValueType == typeof(float)) return node.AsFloat();
            if (entry.ValueType == typeof(int)) return node.AsInt();
            if (entry.ValueType == typeof(bool)) return node.AsBool();

            if (entry.ValueType == typeof(string[]))
            {
                var values = new List<string>();
                foreach (JsonNode item in node.Items()) values.Add(item.AsString());
                return values.ToArray();
            }

            // A scalar enum reads through the same refusal as an array's members: an unknown name
            // stops the load rather than defaulting, which for RunConfig.ConceptionOrder is the
            // difference between a run whose walk order is what the file says and one that
            // silently ran the other order under the file's name.
            if (entry.ValueType.IsEnum) return ParseEnum(entry, entry.ValueType, node);

            Type element = ConfigSchema.EnumElementOf(entry.ValueType);
            if (element != null)
            {
                var parsed = new List<object>();
                foreach (JsonNode item in node.Items()) parsed.Add(ParseEnum(entry, element, item));

                // A typed array, not object[] — the property setter would reject anything else.
                Array array = Array.CreateInstance(element, parsed.Count);
                for (int i = 0; i < parsed.Count; i++) array.SetValue(parsed[i], i);
                return array;
            }

            throw new InvalidOperationException(
                $"{entry.Path} is a {entry.ValueType.Name}, which the config format cannot read.");
        }

        /// <remarks>
        /// <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> alone is not enough: it accepts any
        /// number, so <c>"7"</c> would load as a <c>JointType</c> that does not exist and the run
        /// would fail somewhere far away from the file that caused it. §9's rule is that loading
        /// refuses rather than defaults, and this is the shape that takes for a name.
        /// </remarks>
        private static object ParseEnum(TunableEntry entry, Type element, JsonNode node)
        {
            string name = node.AsString();

            try
            {
                object value = Enum.Parse(element, name, ignoreCase: false);
                if (Enum.IsDefined(element, value)) return value;
            }
            catch (ArgumentException)
            {
                // Falls through to the same message as an out-of-range number.
            }

            throw new FormatException(
                $"'{name}' is not a {element.Name} (at {entry.Group}.{entry.Key}). Known: " +
                string.Join(", ", Enum.GetNames(element)) + ".");
        }

        public static RunConfig Read(string text) => Read(text, out _);
    }
}
