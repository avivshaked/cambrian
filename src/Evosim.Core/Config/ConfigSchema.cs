using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Evosim.Core
{
    /// <summary>One configurable value, found by walking a <see cref="RunConfig"/>.</summary>
    public sealed class TunableEntry
    {
        /// <summary>Dotted path from the config root, e.g. <c>Development.MaxPartVolume</c>.</summary>
        /// <remarks>
        /// <b>The hash is taken over these, sorted.</b> Full paths rather than bare names because
        /// two sub-configs may reasonably use one name, and sorted because
        /// <see cref="Type.GetProperties()"/> makes no guarantee about order — see
        /// <see cref="ConfigSchema.Of"/>.
        /// </remarks>
        public string Path { get; }

        /// <summary>Section for the file and for any editor. Never empty.</summary>
        public string Group { get; }

        /// <summary>Key this is written under within its group. Camel case of the property name.</summary>
        public string Key { get; }

        /// <summary>One line on what this decides, or empty when the doc comment is the answer.</summary>
        public string Description { get; }

        /// <summary>Unit for display, or null.</summary>
        public string Unit { get; }

        /// <summary>
        /// <see cref="float"/>, <see cref="int"/>, <see cref="bool"/>, <c>string[]</c>, an enum,
        /// or an array of some enum — see <see cref="ConfigSchema.EnumElementOf"/>.
        /// </summary>
        public Type ValueType { get; }

        private readonly object _owner;
        private readonly PropertyInfo _property;

        internal TunableEntry(
            string path, string group, string key, string description, string unit,
            object owner, PropertyInfo property)
        {
            Path = path;
            Group = group;
            Key = key;
            Description = description;
            Unit = unit;
            ValueType = property.PropertyType;
            _owner = owner;
            _property = property;
        }

        public object Get() => _property.GetValue(_owner);
        public void Set(object value) => _property.SetValue(_owner, value);

        /// <summary>The value as it appears in the hash and the file. Round-trippable.</summary>
        /// <remarks>
        /// <c>"R"</c> on floats, which is the only format that survives a save and reload
        /// unchanged. A shorter one would make two configs that differ hash the same after a trip
        /// through a file, which is §7's failure in the least visible direction.
        /// </remarks>
        public string Format()
        {
            object value = Get();
            var c = CultureInfo.InvariantCulture;

            switch (value)
            {
                case null: return "";
                case float f: return f.ToString("R", c);
                case int i: return i.ToString(c);
                case bool b: return b ? "true" : "false";
                case string[] a: return string.Join(";", a);

                // A scalar enum, by name for the reason the array case below records. It would
                // fall through to Convert.ToString and produce the same string today; written out
                // so that the rule is stated where a reader looks for it rather than inherited
                // from a conversion that is free to change its mind.
                case Enum single: return single.ToString();

                // By name, never by ordinal — the same rule §9 applies to the file applies here.
                // An ordinal hash would be unchanged by inserting a member into an enum, which
                // renames every value after it and changes what the run actually did.
                case Array e when ConfigSchema.EnumElementOf(value.GetType()) != null:
                {
                    var names = new string[e.Length];
                    for (int i = 0; i < e.Length; i++) names[i] = e.GetValue(i).ToString();
                    return string.Join(";", names);
                }

                default: return Convert.ToString(value, c);
            }
        }

        public override string ToString() =>
            $"{Path} = {Format()}{(string.IsNullOrEmpty(Unit) ? "" : " " + Unit)}";
    }

    /// <summary>
    /// Every configurable value on a <see cref="RunConfig"/>, discovered once — DESIGN.md §5A.10, §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One walk, four consumers.</b> The config hash, the written file, the file reader and any
    /// editing UI are all derived from this rather than each maintaining its own list of a hundred
    /// values. That is the whole point: a knob declared once cannot be present in three of them and
    /// missing from the fourth, which is what happened twice (logbook/0011, logbook/0013).
    /// </para>
    /// <para>
    /// <b>Reflection is used for discovery and never for ordering.</b> That distinction is
    /// load-bearing. <see cref="Type.GetProperties()"/> is explicitly documented not to guarantee
    /// order, so a hash taken in reflection order would be stable on one runtime and quietly
    /// different on the next — turning §7's promise that <c>(genome, seed, configHash)</c>
    /// identifies a run into a promise that holds until someone upgrades .NET. Entries are sorted
    /// by <see cref="TunableEntry.Path"/> with an ordinal comparison, so the order is a property of
    /// the names and nothing else.
    /// </para>
    /// </remarks>
    public static class ConfigSchema
    {
        /// <summary>Every tunable on this config, in a stable order.</summary>
        public static IReadOnlyList<TunableEntry> Of(RunConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var entries = new List<TunableEntry>();
            Walk(config, prefix: "", entries);

            entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return entries;
        }

        /// <summary>Group names in the order they should be written, for a readable file.</summary>
        /// <remarks>
        /// Sorted, like everything else here, so that adding a knob never reshuffles the file and
        /// makes a diff unreadable — which is the one thing §9 asks of the format a person edits.
        /// </remarks>
        public static IReadOnlyList<string> Groups(IReadOnlyList<TunableEntry> entries)
        {
            var seen = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (!seen.Contains(entries[i].Group)) seen.Add(entries[i].Group);
            }

            seen.Sort(StringComparer.Ordinal);
            return seen;
        }

        private static void Walk(object owner, string prefix, List<TunableEntry> into)
        {
            foreach (PropertyInfo p in owner.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                var tunable = p.GetCustomAttribute<TunableAttribute>();
                if (tunable != null)
                {
                    if (!p.CanRead || !p.CanWrite)
                    {
                        throw new InvalidOperationException(
                            $"{prefix}{p.Name} is marked [Tunable] but is not both readable and " +
                            "writable. A value a run cannot set is not a tunable.");
                    }

                    into.Add(new TunableEntry(
                        prefix + p.Name, tunable.Group, CamelCase(p.Name),
                        tunable.Description, tunable.Unit, owner, p));

                    continue;
                }

                if (p.GetCustomAttribute<TunableGroupAttribute>() == null) continue;

                object child = p.GetValue(owner);
                if (child == null)
                {
                    throw new InvalidOperationException(
                        $"{prefix}{p.Name} is a [TunableGroup] and is null, so the run has no " +
                        "value for anything inside it.");
                }

                Walk(child, prefix + p.Name + ".", into);
            }
        }

        /// <summary>
        /// The element type if this is an array of some enum, otherwise null.
        /// </summary>
        /// <remarks>
        /// <c>JointType[]</c> is a tunable — it decides which joints a random genome may draw, and
        /// two runs differing only in it are two different experiments. It is not a scalar and it is
        /// not <c>string[]</c>, so the first version of this walk simply did not see it, and it fell
        /// out of both the hash and the file (logbook/0013). Handled by shape rather than by name so
        /// that the next enum array is carried without anyone remembering to add it.
        /// </remarks>
        public static Type EnumElementOf(Type type)
        {
            if (type == null || !type.IsArray) return null;

            Type element = type.GetElementType();
            return element != null && element.IsEnum ? element : null;
        }

        private static string CamelCase(string name) =>
            name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
