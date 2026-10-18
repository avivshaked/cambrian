using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evosim.NurseryRoom
{
    /// <summary>Small helpers for reading optional fields from a <see cref="JsonElement"/> and for writing the room's JSON.</summary>
    public static class Js
    {
        public static readonly JsonSerializerOptions Out = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false,
        };

        /// <summary>The member named <paramref name="key"/> of an object, or null when absent, null, or <paramref name="e"/> is no object.</summary>
        public static JsonElement? Get(JsonElement? e, string key)
        {
            if (!e.HasValue || e.Value.ValueKind != JsonValueKind.Object) return null;
            if (!e.Value.TryGetProperty(key, out JsonElement v) || v.ValueKind == JsonValueKind.Null || v.ValueKind == JsonValueKind.Undefined) return null;
            return v;
        }

        /// <summary>Follows a path of keys.</summary>
        public static JsonElement? At(JsonElement? e, params string[] path)
        {
            foreach (string k in path) e = Get(e, k);
            return e;
        }

        public static double? Num(JsonElement? e, params string[] path)
        {
            JsonElement? v = At(e, path);
            if (v.HasValue && v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetDouble(out double d) && double.IsFinite(d)) return d;
            return null;
        }

        public static string Str(JsonElement? e, params string[] path)
        {
            JsonElement? v = At(e, path);
            return v.HasValue && v.Value.ValueKind == JsonValueKind.String ? v.Value.GetString() : null;
        }

        public static bool? Bool(JsonElement? e, params string[] path)
        {
            JsonElement? v = At(e, path);
            if (!v.HasValue) return null;
            if (v.Value.ValueKind == JsonValueKind.True) return true;
            if (v.Value.ValueKind == JsonValueKind.False) return false;
            return null;
        }

        /// <summary>An array's elements, or nothing.</summary>
        public static IEnumerable<JsonElement> Items(JsonElement? e)
        {
            if (!e.HasValue || e.Value.ValueKind != JsonValueKind.Array) yield break;
            foreach (JsonElement x in e.Value.EnumerateArray()) yield return x;
        }

        public static double? Finite(double d) => double.IsFinite(d) ? d : (double?)null;

        public static string Serialize(object value) => JsonSerializer.Serialize(value, Out);

        public static JsonElement Parse(string json)
        {
            using (JsonDocument d = JsonDocument.Parse(json)) return d.RootElement.Clone();
        }
    }
}
