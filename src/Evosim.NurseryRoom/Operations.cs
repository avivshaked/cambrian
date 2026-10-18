using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using Evosim.Nursery;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// One thing the room can do. The screens' buttons and the MCP's tools are both this list and
    /// nothing else: a button calls an operation by id, a tool is a family of them.
    /// </summary>
    public sealed class Operation
    {
        /// <summary><c>family.action</c>, or just the family for a family of one that takes no action (<c>describe</c>).</summary>
        public string Id => Action == null ? Family : Family + "." + Action;
        public string Family, Action, Summary;

        private string _schema;

        /// <summary>
        /// JSON schema of the arguments, as hand-written JSON text. Parsed once, when it is set, so a
        /// schema that does not parse refuses at construction and never at a call.
        /// </summary>
        public string Schema
        {
            get => _schema;
            set { SchemaElement = Js.Parse(value); _schema = value; }
        }

        /// <summary>The schema parsed.</summary>
        public JsonElement SchemaElement { get; private set; }

        /// <summary>True when it changes anything: then a <c>reason</c> is required and goes to the activity feed.</summary>
        public bool Mutates;

        public Func<JsonElement, object> Handler;

        /// <summary>Arguments that hold repository paths: a row records them relative, or marks one that lies outside.</summary>
        public string[] PathArgs = new string[0];

        /// <summary>Arguments too large for a row (a recipe's text): a row records their SHA-256 instead.</summary>
        public string[] HashArgs = new string[0];

        /// <summary>What a row records of a successful result (a small object with repository-relative paths), or null for nothing.</summary>
        public Func<object, object> Record;
    }

    public sealed class OperationResult
    {
        public bool Ok;
        /// <summary>HTTP-shaped: 200, 400 for a refused call, 404 for an unknown operation, 500 for a fault.</summary>
        public int Status = 200;
        public string Operation;
        public object Result;
        public string Error;

        /// <summary>Set when the operation did what it was asked but its activity row could not be written.</summary>
        public string Warning;

        public object Envelope() => Ok
            ? (Warning == null ? (object)new { operation = Operation, ok = true, result = Result } : new { operation = Operation, ok = true, result = Result, warning = Warning })
            : new { operation = Operation, ok = false, error = Error };
    }

    /// <summary>The one registry of operations.</summary>
    public sealed partial class OperationList
    {
        private readonly List<Operation> _all = new List<Operation>();
        private readonly RoomContext _room;
        private readonly ResourceResolver _resources;

        /// <summary>Mutating operations run one at a time: a hash checked and a file written, or a cap checked and a run started, are each one step.</summary>
        private readonly object _mutating = new object();

        public IReadOnlyList<Operation> All => _all;

        public OperationList(RoomContext room)
        {
            _room = room;
            _resources = new ResourceResolver(room);

            Add(new Operation
            {
                Family = "recipe", Action = "check", Mutates = false,
                Summary = "Read a recipe file, resolve its base and check every field and its world config, as the nursery would before a run. Has no side effect.",
                Schema = @"{""type"":""object"",""properties"":{""path"":{""type"":""string"",""description"":""The recipe file, relative to the repository root (rounds/nursery/brain2.json).""}},""required"":[""path""],""additionalProperties"":false}",
                Handler = a => RecipeCheck(Js.Str(a, "path")),
            });

            AddRecipeWrites();
            AddRunOperations();
            AddEpisodeOperations();
            AddCompare();

            Add(new Operation
            {
                Family = "describe", Action = null, Mutates = false,
                Summary = "Every operation with its arguments' schema, whole or for one family.",
                Schema = @"{""type"":""object"",""properties"":{""family"":{""type"":""string"",""description"":""Only this family's operations.""}},""additionalProperties"":false}",
                Handler = a => Describe(Js.Str(a, "family")),
            });
        }

        private void Add(Operation op)
        {
            if (_all.Any(o => o.Id == op.Id)) throw new InvalidOperationException("two operations are called " + op.Id);
            _all.Add(op);
        }

        public Operation Find(string id) => _all.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal));

        public IReadOnlyList<string> Families => _all.Select(o => o.Family).Distinct().ToList();

        // ------------------------------------------------------------------ invoking

        public const string Screen = "screen", Mcp = "mcp";

        /// <summary>
        /// Checks the arguments against the operation's schema, then runs it. An operation that changes something
        /// runs alone, and however it ends (done, refused or failed) one row goes to the activity log, saying
        /// <paramref name="who"/> asked: <c>screen</c> for the JSON API, <c>mcp</c> for the MCP.
        /// </summary>
        public OperationResult Invoke(string id, JsonElement? args, string who = Screen)
        {
            Operation op = Find(id);
            if (op == null)
                return new OperationResult { Operation = id, Status = 404, Error = $"no operation \"{id}\"; the operations are {string.Join(", ", _all.Select(o => o.Id))}" };
            JsonElement a = args ?? Js.Parse("{}");
            if (a.ValueKind == JsonValueKind.Null) a = Js.Parse("{}");
            if (!op.Mutates) return Run(op, a);
            lock (_mutating)
            {
                OperationResult r = Run(op, a);
                Log(op, a, who, r);
                return r;
            }
        }

        private OperationResult Run(Operation op, JsonElement a)
        {
            string problem = Validate(op, a);
            if (problem != null) return new OperationResult { Operation = op.Id, Status = 400, Error = problem };
            try { return new OperationResult { Operation = op.Id, Ok = true, Result = op.Handler(a) }; }
            catch (ArgumentException e) { return new OperationResult { Operation = op.Id, Status = 400, Error = e.Message }; }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return new OperationResult { Operation = op.Id, Status = 500, Error = e.GetType().Name + ": " + e.Message };
            }
        }

        /// <summary>Appends the row; a log that cannot be written is said in the result, since the operation has already been done.</summary>
        private void Log(Operation op, JsonElement a, string who, OperationResult r)
        {
            try
            {
                string outcome = r.Ok ? "ok" : r.Status >= 500 ? "failed" : "refused";
                object recorded = null;
                if (r.Ok && op.Record != null)
                {
                    try { recorded = op.Record(r.Result); } catch (Exception e) when (!(e is OutOfMemoryException)) { recorded = null; }
                }
                _room.Activity.Append(new
                {
                    timeUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    operation = op.Id,
                    args = ActivityLog.ArgsOf(op, a, _room.RelativeOrOutside),
                    reason = ActivityLog.Clean(Js.Str(a, "reason"), ActivityLog.MaxReason),
                    who = who == Mcp ? Mcp : Screen,
                    // The tree the room ran in, relative to the main checkout ("." for the main checkout): a folder in a row is relative to the main checkout.
                    tree = _room.TreeRelative,
                    outcome,
                    detail = r.Ok ? null : ActivityLog.Clean(_room.Scrub(r.Error), 1000),
                    result = recorded,
                });
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                r.Warning = "the operation ran, but its activity row could not be written: " + e.Message;
            }
        }

        /// <summary>The arguments checked against the schema's <c>required</c>, <c>properties</c> and <c>additionalProperties</c>, and a mutating operation's reason.</summary>
        public static string Validate(Operation op, JsonElement args)
        {
            if (args.ValueKind != JsonValueKind.Object) return "the arguments are a JSON object";
            JsonElement schema = op.SchemaElement;
            JsonElement props = schema.TryGetProperty("properties", out var p) ? p : default;
            bool closed = schema.TryGetProperty("additionalProperties", out var ap) && ap.ValueKind == JsonValueKind.False;
            if (schema.TryGetProperty("required", out var req))
                foreach (JsonElement r in req.EnumerateArray())
                    if (!args.TryGetProperty(r.GetString(), out var v) || v.ValueKind == JsonValueKind.Null)
                        return $"{op.Id}: \"{r.GetString()}\" is required";
            foreach (JsonProperty m in args.EnumerateObject())
            {
                if (props.ValueKind != JsonValueKind.Object || !props.TryGetProperty(m.Name, out var def))
                {
                    // A mutating operation takes a reason whether or not its schema lists one.
                    if (op.Mutates && m.Name == "reason")
                    {
                        if (m.Value.ValueKind != JsonValueKind.String && m.Value.ValueKind != JsonValueKind.Null) return $"{op.Id}: \"reason\" is a string";
                        continue;
                    }
                    if (closed) return $"{op.Id}: unknown argument \"{m.Name}\"";
                    continue;
                }
                if (def.TryGetProperty("type", out var t))
                {
                    string type = t.GetString();
                    bool ok = type == "string" ? m.Value.ValueKind == JsonValueKind.String
                        : type == "integer" ? m.Value.ValueKind == JsonValueKind.Number && m.Value.TryGetInt64(out _)
                        : type == "number" ? m.Value.ValueKind == JsonValueKind.Number
                        : type == "boolean" ? m.Value.ValueKind == JsonValueKind.True || m.Value.ValueKind == JsonValueKind.False
                        : type == "object" ? m.Value.ValueKind == JsonValueKind.Object
                        : type == "array" ? m.Value.ValueKind == JsonValueKind.Array
                        : true;
                    if (!ok && m.Value.ValueKind != JsonValueKind.Null) return $"{op.Id}: \"{m.Name}\" is a {type}";
                }
                if (def.TryGetProperty("enum", out var en) && !en.EnumerateArray().Any(x => x.ValueKind == m.Value.ValueKind && x.ToString() == m.Value.ToString()))
                    return $"{op.Id}: \"{m.Name}\" is one of {string.Join(", ", en.EnumerateArray().Select(x => x.ToString()))}";
            }
            if (op.Mutates)
            {
                string reason = Js.Str(args, "reason");
                if (string.IsNullOrWhiteSpace(reason)) return $"{op.Id} changes something, so a reason is required";
                if (reason.Length > ActivityLog.MaxReason) return $"{op.Id}: the reason is {reason.Length} characters; at most {ActivityLog.MaxReason}";
                if (reason.Any(char.IsControl)) return $"{op.Id}: the reason holds a line break or another control character; it is one line of plain text";
            }
            return null;
        }

        // ------------------------------------------------------------------ the operations

        private static readonly Regex Refusal = new Regex(@"^recipe: (?<field>\S+) (?<what>.*)$", RegexOptions.Singleline);

        public object RecipeCheck(string path)
        {
            string full = _room.ResolveInRepo(path, out string problem);
            // A path the room refuses is a refused call (400), as the recipe resource refuses it; a recipe
            // the nursery refuses is a successful check whose answer is no.
            if (full == null) throw new ArgumentException(problem);
            if (!File.Exists(full)) return new { ok = false, path, field = (string)null, message = $"{path} does not exist" };
            try
            {
                Recipe r = Recipe.Load(full);
                return new { ok = true, path, name = r.Name, hash = r.Hash(), canonical = r.Canonical() };
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return RefusalOf(path, e.Message);
            }
        }

        /// <summary>A refusal's message, with the field's path parsed out of <c>recipe: &lt;path&gt; &lt;message&gt;</c> when it has that form.</summary>
        public static object RefusalOf(string path, string message)
        {
            Match m = Refusal.Match(message ?? "");
            return m.Success
                ? new { ok = false, path, field = m.Groups["field"].Value, message = m.Groups["what"].Value.TrimEnd() }
                : new { ok = false, path, field = (string)null, message = message };
        }

        public object Describe(string family)
        {
            IEnumerable<Operation> ops = _all;
            if (family != null)
            {
                ops = _all.Where(o => string.Equals(o.Family, family, StringComparison.Ordinal)).ToList();
                if (!ops.Any()) throw new ArgumentException($"no family \"{family}\"; the families are {string.Join(", ", Families)}");
            }
            return new
            {
                families = Families,
                operations = ops.Select(o => new { id = o.Id, family = o.Family, action = o.Action, summary = o.Summary, mutates = o.Mutates, schema = o.SchemaElement }).ToList(),
            };
        }
    }
}
