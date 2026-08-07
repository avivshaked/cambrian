using System;
using System.Collections.Generic;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// The shapes available to a run — DESIGN.md §4.1, §7.
    /// </summary>
    /// <remarks>
    /// Immutable and ordered, for the same reason <see cref="CellTypeRegistry"/> is: shape
    /// mutation picks by an RNG draw, so the order decides which shape a given draw yields. Two
    /// registries holding the same shapes in a different order are not interchangeable, and the
    /// ordering is therefore part of the config hash.
    /// </remarks>
    public sealed class PartShapeRegistry
    {
        private readonly PartShape[] _shapes;
        private readonly Dictionary<string, PartShape> _byId;

        public PartShapeRegistry(params PartShape[] shapes)
        {
            if (shapes == null || shapes.Length == 0)
            {
                throw new ArgumentException("A run needs at least one shape.", nameof(shapes));
            }

            _shapes = (PartShape[])shapes.Clone();
            _byId = new Dictionary<string, PartShape>(StringComparer.Ordinal);

            foreach (PartShape shape in _shapes)
            {
                if (shape == null) throw new ArgumentException("Null shape.", nameof(shapes));

                if (string.IsNullOrWhiteSpace(shape.Id))
                {
                    throw new ArgumentException("A shape must have an id.", nameof(shapes));
                }

                if (_byId.ContainsKey(shape.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate shape id '{shape.Id}'. Ids are serialized into genomes, so " +
                        "one id must mean exactly one shape.", nameof(shapes));
                }

                _byId.Add(shape.Id, shape);
            }
        }

        public static readonly PartShapeRegistry Standard =
            new PartShapeRegistry(new BoxShape(), new SphereShape(), new CapsuleShape());

        public int Count => _shapes.Length;

        public PartShape this[int index] => _shapes[index];

        public IEnumerable<string> Ids()
        {
            foreach (PartShape shape in _shapes) yield return shape.Id;
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        /// <remarks>
        /// Throws on an unknown id rather than substituting a default. A genome that silently
        /// developed as boxes because its shape id no longer resolved would be measured, scored
        /// and filed under the original — the same failure the cell-type registry exists to
        /// prevent.
        /// </remarks>
        public PartShape Resolve(string id)
        {
            if (id != null && _byId.TryGetValue(id, out PartShape shape)) return shape;

            throw new ArgumentException(
                $"Unknown shape '{id}'. Registered: {string.Join(", ", Ids())}.", nameof(id));
        }

        public string HashContribution()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _shapes.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(_shapes[i].HashContribution());
            }
            return sb.ToString();
        }

        public override string ToString() => string.Join(", ", Ids());
    }
}
