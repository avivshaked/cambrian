using System;
using System.Collections.Generic;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>One parsed JSON value — DESIGN.md §9.</summary>
    /// <remarks>
    /// <para>
    /// Accessors throw rather than returning a default when a field is absent or the wrong
    /// type. That is deliberate and is the point of the class: a genome that loads with a
    /// missing field silently defaulted is a <i>different creature</i> wearing the original's
    /// identity, and every number measured from it afterwards would be filed under the wrong
    /// genome. Loudly refusing to load is recoverable; quietly loading the wrong thing is not.
    /// </para>
    /// <para>
    /// <see cref="OptionalFloat"/> and friends exist for the one legitimate case — a field
    /// added after some files were already written — and they make that decision visible at the
    /// call site instead of applying it to everything.
    /// </para>
    /// </remarks>
    public sealed class JsonNode
    {
        public enum NodeKind { Null, Bool, Number, String, Array, Object }

        public NodeKind Kind { get; private set; }

        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonNode> _array;
        private Dictionary<string, JsonNode> _object;

        public static readonly JsonNode Null = new JsonNode { Kind = NodeKind.Null };

        public static JsonNode FromBool(bool v) => new JsonNode { Kind = NodeKind.Bool, _bool = v };
        public static JsonNode FromNumber(double v) => new JsonNode { Kind = NodeKind.Number, _number = v };
        public static JsonNode FromString(string v) => new JsonNode { Kind = NodeKind.String, _string = v };

        public static JsonNode FromArray(List<JsonNode> v) =>
            new JsonNode { Kind = NodeKind.Array, _array = v };

        public static JsonNode FromObject(Dictionary<string, JsonNode> v) =>
            new JsonNode { Kind = NodeKind.Object, _object = v };

        public int Count =>
            Kind == NodeKind.Array ? _array.Count
          : Kind == NodeKind.Object ? _object.Count
          : throw Wrong("a collection");

        public JsonNode this[int index] =>
            Kind == NodeKind.Array ? _array[index] : throw Wrong("an array");

        /// <summary>A required member. Throws if it is missing.</summary>
        public JsonNode this[string name]
        {
            get
            {
                if (Kind != NodeKind.Object) throw Wrong("an object");
                if (!_object.TryGetValue(name, out JsonNode node))
                {
                    throw new FormatException(
                        $"Missing required field '{name}'. Present: " +
                        (_object.Count == 0 ? "<none>" : string.Join(", ", Keys())) + ".");
                }
                return node;
            }
        }

        public bool Has(string name) => Kind == NodeKind.Object && _object.ContainsKey(name);

        public IEnumerable<string> Keys() =>
            Kind == NodeKind.Object ? _object.Keys : throw Wrong("an object");

        public IEnumerable<JsonNode> Items() =>
            Kind == NodeKind.Array ? _array : throw Wrong("an array");

        public float AsFloat()
        {
            if (Kind != NodeKind.Number) throw Wrong("a number");
            return (float)_number;
        }

        public double AsDouble() =>
            Kind == NodeKind.Number ? _number : throw Wrong("a number");

        public int AsInt()
        {
            if (Kind != NodeKind.Number) throw Wrong("a number");
            return checked((int)Math.Round(_number));
        }

        public ulong AsULong()
        {
            if (Kind != NodeKind.Number) throw Wrong("a number");
            if (_number < 0d) throw new FormatException($"Expected a non-negative number, got {_number}.");
            return (ulong)_number;
        }

        public bool AsBool() => Kind == NodeKind.Bool ? _bool : throw Wrong("a boolean");

        public string AsString() =>
            Kind == NodeKind.String ? _string
          : Kind == NodeKind.Null ? null
          : throw Wrong("a string");

        /// <summary>
        /// A member that may legitimately be absent, because it was added to the format after
        /// some files were written. Use sparingly — see the class remarks.
        /// </summary>
        public float OptionalFloat(string name, float fallback) =>
            Has(name) ? this[name].AsFloat() : fallback;

        public int OptionalInt(string name, int fallback) =>
            Has(name) ? this[name].AsInt() : fallback;

        public string OptionalString(string name, string fallback) =>
            Has(name) ? this[name].AsString() : fallback;

        private FormatException Wrong(string expected) =>
            new FormatException(
                $"Expected {expected}, found {Kind}" +
                (Kind == NodeKind.String ? $" (\"{_string}\")" : "") + ".");

        public override string ToString() =>
            Kind == NodeKind.Number ? _number.ToString("R", CultureInfo.InvariantCulture)
          : Kind == NodeKind.String ? $"\"{_string}\""
          : Kind == NodeKind.Bool ? (_bool ? "true" : "false")
          : Kind == NodeKind.Null ? "null"
          : Kind == NodeKind.Array ? $"[{_array.Count} items]"
          : $"{{{_object.Count} fields}}";
    }
}
