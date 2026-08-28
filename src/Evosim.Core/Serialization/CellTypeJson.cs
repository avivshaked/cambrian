using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Cell types to and from JSON, without the serializer knowing the full list — §9, §5A.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type writes its own parameters through
    /// <see cref="CellType.WriteParameters"/> and registers a reader here. That pairing is what
    /// keeps the type system genuinely extensible: adding a sixth cell type means writing the
    /// type and one <see cref="Register"/> call, and touching neither this file nor development,
    /// mutation or the genome format.
    /// </para>
    /// <para>
    /// The id and the upkeep are handled here rather than by each type, because every type has
    /// them and duplicating that is how one of them ends up omitted.
    /// </para>
    /// </remarks>
    public static class CellTypeJson
    {
        private static readonly Dictionary<string, Func<JsonNode, float, CellType>> Readers =
            new Dictionary<string, Func<JsonNode, float, CellType>>(StringComparer.Ordinal)
            {
                [CellTypeIds.Structural] = (n, upkeep) => new StructuralCell(upkeep),

                [CellTypeIds.Link] = (n, upkeep) =>
                    new LinkCell(
                        n["idleWattsPerNewtonMetre"].AsFloat(), upkeep,
                        n["photosyntheticEfficiency"].AsFloat()),

                [CellTypeIds.Neural] = (n, upkeep) => new NeuralCell(
                    n["neuronsSupportedPerCubicMetre"].AsFloat(),
                    n["discountedCostFraction"].AsFloat(),
                    upkeep),

                [CellTypeIds.Photosynthetic] = (n, upkeep) =>
                    new PhotosyntheticCell(n["efficiency"].AsFloat(), upkeep),

                [CellTypeIds.Buoyancy] = (n, upkeep) =>
                    new BuoyancyCell(n["wattsPerLiftUnit"].AsFloat(), upkeep),

                [CellTypeIds.Absorptive] = (n, upkeep) =>
                    new AbsorptiveCell(
                        n["clearanceRate"].AsFloat(), upkeep, n["yield"].AsFloat()),

                [CellTypeIds.Consumer] = (n, upkeep) => new ConsumerCell(
                    n["biteRate"].AsFloat(),
                    upkeep,
                    n["carrionYield"].AsFloat(),
                    n["grazingYield"].AsFloat(),
                    n["predationYield"].AsFloat(),
                    n["scavengeRate"].AsFloat()),
            };

        /// <summary>
        /// Teaches the loader about a cell type. The reader receives the type's JSON object and
        /// the upkeep already parsed out of it.
        /// </summary>
        public static void Register(string id, Func<JsonNode, float, CellType> reader)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            Readers[id] = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public static void Write(Json.Writer writer, CellType type)
        {
            writer.BeginObject();
            writer.Field("id", type.Id);
            writer.Field("upkeepWattsPerCubicMetre", type.UpkeepWattsPerCubicMetre);
            writer.Field("tissueEnergyPerCubicMetre", type.TissueEnergyPerCubicMetre);
            type.WriteParameters(writer);
            writer.EndObject();
        }

        public static CellType Read(JsonNode node)
        {
            string id = node["id"].AsString();

            if (!Readers.TryGetValue(id, out Func<JsonNode, float, CellType> reader))
            {
                throw new FormatException(
                    $"No reader registered for cell type '{id}'. Known: " +
                    string.Join(", ", Readers.Keys) + ". A type that can be written but not " +
                    "read makes its runs unloadable, so register one alongside the type itself.");
            }

            CellType type = reader(node, node["upkeepWattsPerCubicMetre"].AsFloat());

            // Fields every type has are applied here rather than threaded through the reader
            // delegate. A type registered from outside this assembly then picks them up without
            // its constructor knowing they exist, which is what Register promises.
            type.TissueEnergyPerCubicMetre = node["tissueEnergyPerCubicMetre"].AsFloat();

            return type;
        }

        public static void WriteRegistry(Json.Writer writer, string name, CellTypeRegistry registry)
        {
            writer.BeginArray(name);
            foreach (string id in registry.Ids()) Write(writer, registry.Resolve(id));
            writer.EndArray();
        }

        /// <remarks>
        /// Order is preserved, and that is not cosmetic: cell-type mutation picks by an RNG draw,
        /// so the order decides which type a given draw yields. A registry rebuilt in a different
        /// order is a different world, which is why it is part of the config hash (§7).
        /// </remarks>
        public static CellTypeRegistry ReadRegistry(JsonNode array)
        {
            var types = new List<CellType>();
            foreach (JsonNode n in array.Items()) types.Add(Read(n));
            return new CellTypeRegistry(types.ToArray());
        }
    }
}
