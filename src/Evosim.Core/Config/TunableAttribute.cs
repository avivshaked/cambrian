using System;

namespace Evosim.Core
{
    /// <summary>
    /// Marks a property as a value a run may be configured with — DESIGN.md §5A.10, §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A knob should be declared once.</b> Before this, every tunable had to be written in four
    /// places — the property, <see cref="RunConfig.Hash"/>, the JSON writer and the JSON reader —
    /// and there are around a hundred of them. Four hundred hand-maintained sites is not a style
    /// problem; it is the mechanism behind both faults §7 exists to catch.
    /// <c>DevelopmentLimits.MaxPartVolume</c> reached the property and nothing else, and
    /// <see cref="LightModel"/> — carrying the single most consequential number in the design —
    /// reached none of them, so every run of the §5A.2b calibration sweep shared one
    /// <c>configHash</c> (logbook/0013).
    /// </para>
    /// <para>
    /// Marked properties are discovered by <see cref="ConfigSchema"/>, and the hash, the file
    /// format and eventually the editing UI are all derived from that one walk. Adding a knob is
    /// one property and one attribute; forgetting the attribute fails a test rather than going
    /// quiet.
    /// </para>
    /// <para>
    /// <b>What does not belong here.</b> Evolved genome traits — brood size, offspring endowment —
    /// are not configuration; a creature that could choose its own would choose whatever is free.
    /// Nor are mathematical identities: lit area is a quarter of surface area by Cauchy's formula,
    /// and a knob there would be a licence to break geometry rather than a parameter.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TunableAttribute : Attribute
    {
        /// <summary>
        /// Section this belongs to, and the object it is written under in <c>config.json</c>.
        /// </summary>
        /// <remarks>
        /// Grouping is for the reader — the file is meant to be opened and edited by hand (§9), and
        /// a hundred flat keys is not something a person can navigate. It has no effect on the
        /// hash, which is taken over full paths in sorted order.
        /// </remarks>
        public string Group { get; }

        /// <summary>One line on what this decides. Shown wherever a run is configured.</summary>
        /// <remarks>
        /// Deliberately short and deliberately optional. The reasoning lives in the property's own
        /// documentation, where it can be paragraphs; repeating it here would be a second copy to
        /// drift. Empty means "the doc comment is the explanation".
        /// </remarks>
        public string Description { get; }

        /// <summary>Unit, for display — <c>"W/m²"</c>, <c>"J"</c>, <c>"m"</c>. Optional.</summary>
        public string Unit { get; set; }

        public TunableAttribute(string group, string description = "")
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                throw new ArgumentException(
                    "Every tunable needs a group, because the file it lands in is meant to be " +
                    "read by a person and a hundred ungrouped keys is not readable.", nameof(group));
            }

            Group = group;
            Description = description ?? "";
        }
    }

    /// <summary>
    /// Marks a property as a sub-object whose own <see cref="TunableAttribute"/> properties count.
    /// </summary>
    /// <remarks>
    /// <see cref="ConfigSchema"/> does not guess which properties to descend into. It was tried
    /// the other way — a walk that discovered sub-configs by reflection — and the failure mode is
    /// that it silently covers whatever it happens to reach: <see cref="RunConfig.CellTypes"/> and
    /// <see cref="RunConfig.Shapes"/> are registries with no settable scalars, so an automatic
    /// walk finds nothing in them and reports success. Descent is declared, and a sub-config that
    /// is not marked fails the coverage test rather than being quietly skipped.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TunableGroupAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a property that is configuration but is not a scalar the schema can walk.
    /// </summary>
    /// <remarks>
    /// The registries. <see cref="CellTypeRegistry"/> and <see cref="PartShapeRegistry"/> hold
    /// objects with constructor-only parameters and a variable membership, so they carry their own
    /// <c>HashContribution</c> and their own serializer. Marking them keeps them visible to the
    /// coverage test — the point is that nothing on <see cref="RunConfig"/> is unaccounted for,
    /// not that everything is walkable the same way.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TunableRegistryAttribute : Attribute
    {
    }
}
