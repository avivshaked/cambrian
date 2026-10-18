using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Evosim.NurseryRoom.Tests.Golden
{
    /// <summary>What a <see cref="Mask"/> does to its field's value.</summary>
    internal enum MaskKind
    {
        /// <summary>The value is replaced by <c>"&lt;masked:Token&gt;"</c>: it changes from run to run and is not compared at all.</summary>
        Hide,
        /// <summary>The fixture root inside the string becomes <c>&lt;root&gt;</c> and the slashes after it forward ones; the rest of the string is still compared.</summary>
        Root,
        /// <summary>The string's line endings become a bare line feed: they follow the checkout's git settings, the rest of the string is still compared.</summary>
        Lf,
    }

    /// <summary>
    /// One field whose value changes from run to run (or from machine to machine), named by its path in the answer: object members joined by dots, an array element written <c>[]</c>
    /// (<c>items[].startedUtc</c>). A null value is left as it is, so that a field turning null or turning into a value is still a difference.
    /// </summary>
    internal sealed class Mask
    {
        public readonly string Path, Token, Why;
        public readonly MaskKind Kind;
        public Mask(string path, string token, string why, MaskKind kind = MaskKind.Hide) { Path = path; Token = token; Why = why; Kind = kind; }

        public JsonNode Apply(string text, string fixtureRoot)
        {
            switch (Kind)
            {
                case MaskKind.Root: return JsonValue.Create(GoldenAnswer.Rooted(text, fixtureRoot));
                case MaskKind.Lf: return JsonValue.Create(text.Replace("\r\n", "\n"));
                default: return JsonValue.Create("<masked:" + Token + ">");
            }
        }
    }

    /// <summary>
    /// A saved answer: the room's answer to one resource read on fixture data, masked, written as indented JSON beside the tests. A test fails when the answer changes (its first differing
    /// path, the saved value, the new value, then the whole new answer). An answer is written only when <c>ROOM_GOLDEN_UPDATE=1</c> is set; without it a missing file fails the test.
    /// </summary>
    internal static class GoldenAnswer
    {
        public const string UpdateVariable = "ROOM_GOLDEN_UPDATE";
        public const string RootToken = "<root>";

        /// <summary>The same options write the saved file and the failure's copy of the new answer, so that what is shown is what would be saved. Not escaping &lt; and &gt; keeps the tokens readable.</summary>
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static string AnswersDir => System.IO.Path.Combine(Where.Root, "src", "Evosim.NurseryRoom.Tests", "Golden", "answers");

        public static bool Updating => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

        /// <summary>The answer as the room writes it (<see cref="Js.Serialize"/>, what the MCP and /api/resource both send), with every masked field's value replaced. Each mask that found something has its path added to <paramref name="hit"/>.</summary>
        public static JsonNode Masked(object answer, IReadOnlyList<Mask> masks, string fixtureRoot, ISet<string> hit = null)
        {
            JsonNode tree = JsonNode.Parse(Js.Serialize(answer));
            Walk(tree, "", masks, fixtureRoot, hit);
            return tree;
        }

        private static void Walk(JsonNode node, string path, IReadOnlyList<Mask> masks, string root, ISet<string> hit)
        {
            if (node is JsonObject o)
            {
                foreach (string key in o.Select(kv => kv.Key).ToList())
                {
                    string at = path.Length == 0 ? key : path + "." + key;
                    JsonNode child = o[key];
                    Mask m = masks.FirstOrDefault(x => x.Path == at);
                    if (m != null && child is JsonValue v0 && v0.GetValueKind() != JsonValueKind.Null)
                    {
                        hit?.Add(m.Path);
                        if (m.Kind == MaskKind.Hide) { o[key] = m.Apply(null, root); continue; }
                        if (v0.GetValueKind() == JsonValueKind.String) { o[key] = m.Apply(v0.GetValue<string>(), root); continue; }
                    }
                    else if (m != null && child != null && m.Kind == MaskKind.Hide && !(child is JsonValue))
                    {
                        hit?.Add(m.Path);
                        o[key] = m.Apply(null, root);
                        continue;
                    }
                    Walk(child, at, masks, root, hit);
                }
            }
            else if (node is JsonArray a)
            {
                for (int i = 0; i < a.Count; i++)
                {
                    // An array element is named by its parent's path: a mask on an array of strings masks every element.
                    Mask m = masks.FirstOrDefault(x => x.Path == path + "[]");
                    if (m != null && a[i] is JsonValue ev && ev.GetValueKind() == JsonValueKind.String)
                    {
                        hit?.Add(m.Path);
                        a[i] = m.Apply(ev.GetValue<string>(), root);
                    }
                    else Walk(a[i], path + "[]", masks, root, hit);
                }
            }
        }

        /// <summary>The fixture root, written with either slash, becomes <c>&lt;root&gt;</c>; the slashes after it are made forward ones, so the answer is the same on every machine.</summary>
        internal static string Rooted(string text, string root)
        {
            string full = System.IO.Path.GetFullPath(root).TrimEnd('\\', '/');
            foreach (string form in new[] { full, full.Replace('\\', '/'), full.Replace("\\", "\\\\") })
            {
                int i = text.IndexOf(form, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                string tail = text.Substring(i + form.Length).Replace('\\', '/');
                return text.Substring(0, i) + RootToken + tail;
            }
            return text;
        }

        public static string Pretty(JsonNode answer) => answer.ToJsonString(Indented).Replace("\r\n", "\n") + "\n";

        /// <summary>
        /// Compares the new answer with the saved one, or records it. Fails (with the first differing path, both values and the whole new answer) when they differ, and when no
        /// answer is saved and recording is not asked for.
        /// </summary>
        public static void Check(string name, JsonNode answer)
        {
            string file = System.IO.Path.Combine(AnswersDir, name + ".json");
            string fresh = Pretty(answer);
            if (Updating)
            {
                Directory.CreateDirectory(AnswersDir);
                File.WriteAllText(file, fresh, new System.Text.UTF8Encoding(false));
                return;
            }
            Assert.True(File.Exists(file), $"no saved answer for \"{name}\" ({file}). Record it on purpose: set {UpdateVariable}=1 and run this test again, then read the file and commit it with a message that says why it is there.");
            JsonNode saved = JsonNode.Parse(File.ReadAllText(file));
            string diff = FirstDifference(saved, answer, "$");
            Assert.True(diff == null,
                $"the room's answer for \"{name}\" is not the saved one ({file}).\nFirst difference: {diff}\n" +
                $"If the change is meant, record the answer again with {UpdateVariable}=1 and say why in the commit.\n--- the whole new answer ---\n{fresh}");
        }

        /// <summary>The first place two answers differ, as a path with both values; null when they are equal. Members are compared in order: a renamed or moved member is a difference.</summary>
        public static string FirstDifference(JsonNode saved, JsonNode fresh, string path)
        {
            string Show(JsonNode n) => n == null ? "null" : n.ToJsonString(Indented).Replace("\r\n", "\n");
            if (saved is JsonObject so && fresh is JsonObject fo)
            {
                List<string> sk = so.Select(kv => kv.Key).ToList(), fk = fo.Select(kv => kv.Key).ToList();
                for (int i = 0; i < Math.Max(sk.Count, fk.Count); i++)
                {
                    string s = i < sk.Count ? sk[i] : null, f = i < fk.Count ? fk[i] : null;
                    if (s != f)
                        return $"{path}: member #{i} is named {(s == null ? "(none: the saved object has {0} members)".Replace("{0}", sk.Count.ToString()) : "\"" + s + "\"")} in the saved answer and {(f == null ? "(none: the new object has " + fk.Count + " members)" : "\"" + f + "\"")} in the new one (saved members: {string.Join(", ", sk)}; new members: {string.Join(", ", fk)})";
                }
                foreach (string k in sk)
                {
                    string d = FirstDifference(so[k], fo[k], path + "." + k);
                    if (d != null) return d;
                }
                return null;
            }
            if (saved is JsonArray sa && fresh is JsonArray fa)
            {
                for (int i = 0; i < Math.Min(sa.Count, fa.Count); i++)
                {
                    string d = FirstDifference(sa[i], fa[i], path + "[" + i + "]");
                    if (d != null) return d;
                }
                if (sa.Count != fa.Count) return $"{path}: the saved array holds {sa.Count} element(s), the new one {fa.Count}";
                return null;
            }
            string a = Show(saved), b = Show(fresh);
            if (a == b) return null;
            return $"{path}: saved {Trim(a)}, new {Trim(b)}";
        }

        private static string Trim(string s) => s.Length <= 600 ? s : s.Substring(0, 600) + "... (" + s.Length + " characters)";
    }
}
