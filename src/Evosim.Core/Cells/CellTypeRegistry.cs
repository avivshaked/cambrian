using System;
using System.Collections.Generic;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// The set of cell types a run may use, in a fixed order — DESIGN.md §5A.1, §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Order is load-bearing, not cosmetic.</b> Cell-type mutation picks a type from an RNG
    /// draw (§4.5), so which type a given draw yields depends on the registry's ordering. A
    /// genome replayed against a registry with a type added, removed, or merely registered in a
    /// different order develops into a different creature — silently, and long after the run
    /// that produced it. The order is therefore fixed at construction and folded into
    /// <see cref="HashContribution"/>, which §7's <c>configHash</c> covers.
    /// </para>
    /// <para>
    /// This is why the registry is immutable and why there is no ambient mutable default.
    /// Registration at static-initialisation time from several files is convenient and produces
    /// an order that depends on type-load order, which is a reproducibility bug waiting for a
    /// refactor to trigger it.
    /// </para>
    /// </remarks>
    public sealed class CellTypeRegistry
    {
        private readonly CellType[] _types;
        private readonly Dictionary<string, int> _byId;

        /// <summary>The four built-in types of §5A.1, in a fixed order.</summary>
        /// <remarks>
        /// Appending to this list is safe for future genomes and changes what past ones develop
        /// into only if they used mutation; inserting into the middle, or reordering, breaks
        /// replay of everything. Append only.
        /// </remarks>
        public static readonly CellTypeRegistry Standard = new CellTypeRegistry(
            new StructuralCell(),
            new LinkCell(),
            new NeuralCell(),
            new PhotosyntheticCell(),
            new AbsorptiveCell(),
            new ConsumerCell(),
            new BuoyancyCell());

        public CellTypeRegistry(params CellType[] types)
        {
            if (types == null || types.Length == 0)
            {
                throw new ArgumentException("A registry needs at least one cell type.", nameof(types));
            }

            _types = (CellType[])types.Clone();
            _byId = new Dictionary<string, int>(_types.Length, StringComparer.Ordinal);

            for (int i = 0; i < _types.Length; i++)
            {
                CellType type = _types[i] ?? throw new ArgumentException(
                    $"Cell type at index {i} is null.", nameof(types));

                if (string.IsNullOrWhiteSpace(type.Id))
                {
                    throw new ArgumentException($"Cell type at index {i} has no id.", nameof(types));
                }

                if (type.UpkeepWattsPerCubicMetre <= 0f)
                {
                    throw new ArgumentException(
                        $"Cell type '{type.Id}' has upkeep {type.UpkeepWattsPerCubicMetre}. " +
                        "Every type must cost something (DESIGN.md §5A.1) — a free part is a " +
                        "free lever, and bodies grow without limit against it.",
                        nameof(types));
                }

                if (_byId.ContainsKey(type.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate cell type id '{type.Id}'. Ids are serialized into genomes " +
                        "and must identify exactly one type.", nameof(types));
                }

                _byId.Add(type.Id, i);
            }
        }

        public int Count => _types.Length;

        /// <summary>The type at an index. Indices are meaningful only within one registry.</summary>
        public CellType At(int index) => _types[index];

        /// <summary>Position of a type in the fixed order, or -1.</summary>
        public int IndexOf(string id) => _byId.TryGetValue(id, out int i) ? i : -1;

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        /// <summary>
        /// The type with this id. Throws when unknown — never falls back to a default.
        /// </summary>
        /// <remarks>
        /// A genome naming a type this run does not have is not a genome this run can evaluate.
        /// Substituting a default would produce a creature that is not the one stored, score it,
        /// and file the result under the original genome — which is worse than a crash, because
        /// nothing downstream could tell.
        /// </remarks>
        public CellType Resolve(string id)
        {
            if (id != null && _byId.TryGetValue(id, out int i)) return _types[i];

            throw new ArgumentException(
                $"Unknown cell type '{id}'. This registry has: {string.Join(", ", Ids())}. " +
                "A genome referring to a type that is not registered cannot be developed; " +
                "register the type or do not load the genome.", nameof(id));
        }

        public IEnumerable<string> Ids()
        {
            foreach (CellType type in _types) yield return type.Id;
        }

        /// <summary>
        /// The registry's contribution to §7's <c>configHash</c>: every type, in order, with its
        /// parameters.
        /// </summary>
        public string HashContribution()
        {
            var sb = new StringBuilder("cells[");
            for (int i = 0; i < _types.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(_types[i].FullHashContribution());
            }
            return sb.Append(']').ToString();
        }
    }
}
