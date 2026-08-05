using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// A minimal JSON reader and writer — DESIGN.md §9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why hand-written rather than a library.</b> <c>Evosim.Core</c> has no dependencies at
    /// all, which is what lets its tests run in a second outside the Editor instead of thirty
    /// inside it (§6.1). Taking one on for a format this small trades that away, and it would
    /// have to be taken on twice — Unity supplies Newtonsoft, the standalone test project does
    /// not, and the two would have to be kept agreeing about a file format. The schemas here are
    /// numbers, strings and arrays; that is not enough JSON to be worth a dependency.
    /// </para>
    /// <para>
    /// <b>Round-trip fidelity is the whole requirement.</b> Floats are written with the "R"
    /// format specifier, which is the shortest string that reads back as the identical value.
    /// Anything less and a genome saved and reloaded is a <i>different</i> genome that looks the
    /// same — it would develop into a slightly different body, be evaluated as though it were
    /// the original, and nothing downstream could tell. That is the same class of fault as a
    /// configuration change that never reaches what it configures, which this project has now
    /// hit twice.
    /// </para>
    /// <para>
    /// Deliberately not a general-purpose parser. It rejects what it does not understand rather
    /// than skipping it, because a field silently dropped on load is exactly the failure this
    /// exists to prevent.
    /// </para>
    /// </remarks>
    public static class Json
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- writing

        /// <summary>Builds JSON text. Indented, because these files are meant to be read.</summary>
        public sealed class Writer
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private readonly Stack<bool> _firstInScope = new Stack<bool>();
            private readonly bool _indent;
            private int _depth;

            /// <param name="indent">
            /// <c>true</c> for files a person opens — run settings, a genome pulled out to look
            /// at. <c>false</c> for a row in an append-only file, where the record must occupy
            /// exactly one line: <c>lineage.jsonl</c> and <c>stats.jsonl</c> are line-oriented
            /// precisely so that a killed run leaves every completed row intact and readable,
            /// and an embedded newline would destroy that property.
            /// </param>
            public Writer(bool indent = true)
            {
                _indent = indent;
                _firstInScope.Push(true);
            }

            private void Separate()
            {
                if (_firstInScope.Count > 0 && !_firstInScope.Peek()) _sb.Append(',');
                if (_firstInScope.Count > 0)
                {
                    _firstInScope.Pop();
                    _firstInScope.Push(false);
                }

                if (_indent && _sb.Length > 0) _sb.Append('\n').Append(' ', _depth * 2);
            }

            private void Break()
            {
                if (_indent) _sb.Append('\n').Append(' ', _depth * 2);
            }

            private void Key(string name)
            {
                Separate();
                WriteString(name);
                _sb.Append(_indent ? ": " : ":");
            }

            public Writer BeginObject()
            {
                Separate();
                _sb.Append('{');
                _depth++;
                _firstInScope.Push(true);
                return this;
            }

            public Writer BeginObject(string name)
            {
                Key(name);
                _sb.Append('{');
                _depth++;
                _firstInScope.Push(true);
                return this;
            }

            public Writer EndObject()
            {
                _depth--;
                _firstInScope.Pop();
                Break();
                _sb.Append('}');
                return this;
            }

            public Writer BeginArray(string name)
            {
                Key(name);
                _sb.Append('[');
                _depth++;
                _firstInScope.Push(true);
                return this;
            }

            public Writer EndArray()
            {
                _depth--;
                _firstInScope.Pop();
                Break();
                _sb.Append(']');
                return this;
            }

            public Writer Field(string name, string value)
            {
                Key(name);
                if (value == null) _sb.Append("null"); else WriteString(value);
                return this;
            }

            public Writer Field(string name, bool value)
            {
                Key(name);
                _sb.Append(value ? "true" : "false");
                return this;
            }

            public Writer Field(string name, int value)
            {
                Key(name);
                _sb.Append(value.ToString(Invariant));
                return this;
            }

            public Writer Field(string name, long value)
            {
                Key(name);
                _sb.Append(value.ToString(Invariant));
                return this;
            }

            public Writer Field(string name, ulong value)
            {
                Key(name);
                _sb.Append(value.ToString(Invariant));
                return this;
            }

            /// <remarks>
            /// "R" round-trips: the shortest decimal string that parses back to the same bits.
            /// A fixed number of decimal places would quietly alter every genome it saved.
            /// </remarks>
            public Writer Field(string name, float value)
            {
                Key(name);
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new ArgumentException(
                        $"Field '{name}' is {value}, which JSON cannot represent. A non-finite " +
                        "number here means something upstream produced one and nothing noticed.",
                        nameof(value));
                }
                _sb.Append(value.ToString("R", Invariant));
                return this;
            }

            public Writer Value(float value)
            {
                Separate();
                _sb.Append(value.ToString("R", Invariant));
                return this;
            }

            public Writer Value(int value)
            {
                Separate();
                _sb.Append(value.ToString(Invariant));
                return this;
            }

            public Writer Value(string value)
            {
                Separate();
                if (value == null) _sb.Append("null"); else WriteString(value);
                return this;
            }

            private void WriteString(string s)
            {
                _sb.Append('"');
                foreach (char ch in s)
                {
                    switch (ch)
                    {
                        case '"': _sb.Append("\\\""); break;
                        case '\\': _sb.Append("\\\\"); break;
                        case '\n': _sb.Append("\\n"); break;
                        case '\r': _sb.Append("\\r"); break;
                        case '\t': _sb.Append("\\t"); break;
                        default:
                            if (ch < ' ') _sb.Append("\\u").Append(((int)ch).ToString("x4", Invariant));
                            else _sb.Append(ch);
                            break;
                    }
                }
                _sb.Append('"');
            }

            public override string ToString() => _sb.ToString();
        }

        // ---------------------------------------------------------------- reading

        /// <summary>Parses JSON text into <see cref="JsonNode"/>s.</summary>
        public static JsonNode Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            int i = 0;
            JsonNode node = ParseValue(text, ref i);
            SkipWhitespace(text, ref i);

            if (i < text.Length)
            {
                throw new FormatException(
                    $"Trailing content at offset {i}: '{Excerpt(text, i)}'. The document ended " +
                    "and then continued, which usually means two files were concatenated.");
            }

            return node;
        }

        private static JsonNode ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of document.");

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return JsonNode.FromString(ParseString(s, ref i));
                case 't': Expect(s, ref i, "true"); return JsonNode.FromBool(true);
                case 'f': Expect(s, ref i, "false"); return JsonNode.FromBool(false);
                case 'n': Expect(s, ref i, "null"); return JsonNode.Null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static JsonNode ParseObject(string s, ref int i)
        {
            var members = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
            i++;   // '{'

            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return JsonNode.FromObject(members); }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"')
                {
                    throw new FormatException($"Expected a field name at offset {i}: '{Excerpt(s, i)}'.");
                }

                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);

                if (i >= s.Length || s[i] != ':')
                {
                    throw new FormatException($"Expected ':' after '{key}' at offset {i}.");
                }
                i++;

                members[key] = ParseValue(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated object.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return JsonNode.FromObject(members); }

                throw new FormatException($"Expected ',' or '}}' at offset {i}: '{Excerpt(s, i)}'.");
            }
        }

        private static JsonNode ParseArray(string s, ref int i)
        {
            var items = new List<JsonNode>();
            i++;   // '['

            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return JsonNode.FromArray(items); }

            while (true)
            {
                items.Add(ParseValue(s, ref i));

                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated array.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return JsonNode.FromArray(items); }

                throw new FormatException($"Expected ',' or ']' at offset {i}: '{Excerpt(s, i)}'.");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++;   // opening quote
            var sb = new StringBuilder();

            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Truncated \\u escape.");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, Invariant));
                        i += 4;
                        break;
                    default:
                        throw new FormatException($"Unknown escape '\\{esc}' at offset {i - 1}.");
                }
            }

            throw new FormatException("Unterminated string.");
        }

        private static JsonNode ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;

            while (i < s.Length &&
                   (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                    ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
            {
                i++;
            }

            string text = s.Substring(start, i - start);
            if (text.Length == 0 ||
                !double.TryParse(text, NumberStyles.Float, Invariant, out double value))
            {
                throw new FormatException($"Not a number at offset {start}: '{Excerpt(s, start)}'.");
            }

            return JsonNode.FromNumber(value);
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length ||
                string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
            {
                throw new FormatException($"Expected '{literal}' at offset {i}: '{Excerpt(s, i)}'.");
            }
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static string Excerpt(string s, int i) =>
            i >= s.Length ? "<end of document>" : s.Substring(i, Math.Min(24, s.Length - i));
    }
}
