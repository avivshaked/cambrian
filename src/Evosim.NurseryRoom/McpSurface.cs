using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// The MCP's tools and resources, generated from the operation list and the resolver and listed
    /// nowhere else: one tool per family, each taking an <c>action</c> and that action's arguments;
    /// every resource and template the resolver offers. A new operation joins its family and adds no
    /// tool; a new resource kind appears here by being in the resolver.
    /// </summary>
    public static class McpSurface
    {
        public static List<Tool> Tools(OperationList ops)
        {
            var tools = new List<Tool>();
            foreach (string family in ops.Families)
            {
                List<Operation> members = ops.All.Where(o => o.Family == family).ToList();
                tools.Add(new Tool
                {
                    Name = family,
                    Description = Describe(members),
                    InputSchema = Schema(members),
                });
            }
            return tools;
        }

        private static string Describe(List<Operation> members)
        {
            if (members.Count == 1 && members[0].Action == null) return members[0].Summary;
            return "Actions: " + string.Join(" ", members.Select(o => $"{o.Action}: {o.Summary}" + (o.Mutates ? " (changes something: a reason is required)" : "")))
                + " Call describe for each action's argument schema.";
        }

        /// <summary>
        /// A family's input schema: a family of one with no action is that operation's own schema; otherwise
        /// an object with <c>action</c> (an enum of the family's actions) and the union of the actions'
        /// arguments, each described by which action takes it.
        /// </summary>
        public static JsonElement Schema(List<Operation> members)
        {
            if (members.Count == 1 && members[0].Action == null) return members[0].SchemaElement;
            var props = new JsonObject();
            props["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(members.Select(o => (JsonNode)o.Action).ToArray()) };
            // Each property is described once, from the first action that lists it, and says which actions take it.
            var takenBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var order = new List<(string name, JsonElement def)>();
            foreach (Operation o in members)
            {
                JsonElement schema = o.SchemaElement;
                if (!schema.TryGetProperty("properties", out JsonElement p)) continue;
                foreach (JsonProperty prop in p.EnumerateObject())
                {
                    if (!takenBy.TryGetValue(prop.Name, out List<string> who)) { takenBy[prop.Name] = who = new List<string>(); order.Add((prop.Name, prop.Value)); }
                    who.Add(o.Action);
                }
            }
            foreach ((string name, JsonElement def) in order)
            {
                JsonNode node = JsonNode.Parse(def.GetRawText());
                if (node is JsonObject d)
                {
                    string said = d["description"]?.GetValue<string>();
                    string who = takenBy[name].Count == members.Count ? "every action" : "action " + string.Join(", ", takenBy[name]);
                    d["description"] = (string.IsNullOrEmpty(said) ? "" : said + " ") + $"(taken by {who})";
                }
                props[name] = node;
            }
            if (members.Any(o => o.Mutates) && !props.ContainsKey("reason"))
                props["reason"] = new JsonObject { ["type"] = "string", ["description"] = "Why; required by every action that changes something, and shown in the activity feed." };
            var schemaNode = new JsonObject { ["type"] = "object", ["properties"] = props, ["required"] = new JsonArray { (JsonNode)"action" } };
            return Js.Parse(schemaNode.ToJsonString());
        }

        /// <summary>Runs a tool call: the family's action, or the single operation, with the arguments that remain.</summary>
        public static CallToolResult Call(OperationList ops, string tool, IDictionary<string, JsonElement> arguments)
        {
            IEnumerable<Operation> members = ops.All.Where(o => o.Family == tool).ToList();
            if (!members.Any()) throw new McpProtocolException($"no tool \"{tool}\"; the tools are {string.Join(", ", ops.Families)}", McpErrorCode.InvalidParams);
            var rest = new JsonObject();
            string id;
            if (members.Count() == 1 && members.First().Action == null) id = members.First().Id;
            else
            {
                string action = arguments != null && arguments.TryGetValue("action", out JsonElement a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
                if (action == null) return Error($"{tool}: \"action\" is required; its actions are {string.Join(", ", members.Select(o => o.Action))}");
                id = tool + "." + action;
            }
            if (arguments != null)
                foreach (var kv in arguments)
                    if (kv.Key != "action" || ops.Find(tool) != null) rest[kv.Key] = JsonNode.Parse(kv.Value.GetRawText());
            JsonElement args = Js.Parse(rest.ToJsonString());
            OperationResult r = ops.Invoke(id, args, OperationList.Mcp);
            string text = Js.Serialize(r.Envelope());
            return new CallToolResult { Content = new List<ContentBlock> { new TextContentBlock { Text = text } }, IsError = !r.Ok };
        }

        private static CallToolResult Error(string message) =>
            new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = message } } };

        public static List<Resource> Resources(ResourceResolver resolver) =>
            resolver.List().Select(r => new Resource { Uri = r.Uri, Name = r.Name, Description = r.Description, MimeType = "application/json" }).ToList();

        public static List<ResourceTemplate> Templates(ResourceResolver resolver) =>
            resolver.Templates.Select(t => new ResourceTemplate { UriTemplate = t.UriTemplate, Name = t.Name, Description = t.Description, MimeType = "application/json" }).ToList();

        public static ReadResourceResult Read(ResourceResolver resolver, string uri)
        {
            string text;
            try { text = Js.Serialize(resolver.Read(uri)); }
            catch (ResourceNotFound e) { throw new McpProtocolException(e.Message, McpErrorCode.InvalidParams); }
            catch (ResourceRefused e) { throw new McpProtocolException(e.Message, McpErrorCode.InvalidParams); }
            return new ReadResourceResult
            {
                Contents = new List<ResourceContents> { new TextResourceContents { Uri = uri, MimeType = "application/json", Text = text } },
            };
        }
    }
}
