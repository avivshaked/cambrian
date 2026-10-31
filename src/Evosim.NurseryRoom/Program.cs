using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Evosim.Nursery;
using ModelContextProtocol.Protocol;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// The nursery room: one process, three doors (the screens, a JSON API, an MCP endpoint at /mcp),
    /// listening on 127.0.0.1 only. Options: <c>--root &lt;repo&gt;</c> (default: the repository above the
    /// working directory or the program), <c>--activity-file &lt;path&gt;</c> (default: room.json's activityFile under the main checkout), <c>--settings &lt;file&gt;</c> (default
    /// rounds/nursery/room.json under the root), <c>--web-root &lt;folder&gt;</c> (default: the project's wwwroot).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                string root = Opt(args, "--root") ?? RoomContext.FindRoot(Directory.GetCurrentDirectory()) ?? RoomContext.FindRoot(AppContext.BaseDirectory);
                if (root == null) { Console.Error.WriteLine("nursery room: no repository root (a folder holding src/Evosim.Core) above the working directory or the program; pass --root"); return 2; }
                string settingsPath = Opt(args, "--settings") ?? Path.Combine(root, "rounds", "nursery", "room.json");
                RoomSettings settings = RoomSettings.Load(settingsPath);
                string webRoot = Opt(args, "--web-root") ?? WebRootFor(root);
                // --activity-file puts the activity log elsewhere than room.json says (a smoke test run from a worktree, whose main checkout is another folder).
                var room = new RoomContext(root, settings, activityFile: Opt(args, "--activity-file"));
                CheckActivityFileIsListed(room);
                WebApplication app = Create(room, webRoot);
                app.Start();
                // Loopback by construction: Listen() names the one address, and what the server reports it
                // bound is checked all the same, so no setting outside this program can leave the machine.
                IServerAddressesFeature bound = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
                string[] addresses = bound == null ? new string[0] : bound.Addresses.ToArray();
                string stray = addresses.FirstOrDefault(a => !RoomGuards.IsLoopbackAddress(a));
                if (stray != null || addresses.Length == 0)
                {
                    app.StopAsync().GetAwaiter().GetResult();
                    Console.Error.WriteLine(stray != null
                        ? $"nursery room: the server bound {stray}, which is not a loopback address; refusing to run"
                        : "nursery room: the server reports no bound address, so loopback cannot be confirmed; refusing to run");
                    return 2;
                }
                Console.WriteLine($"nursery room: {string.Join(" ", addresses)}  (root {root}, web root {webRoot})");
                app.WaitForShutdown();
                return 0;
            }
            catch (InvalidDataException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
        }

        /// <summary>
        /// The activity log is appended to by every operation, so a tree where it counts as an edit would read dirty after
        /// the first one: it must be on the list of appended files (rounds/nursery/appended.txt), which the nursery's dirty
        /// check reads. Refused by naming both when it is not, or when the list cannot be read.
        /// </summary>
        public static void CheckActivityFileIsListed(RoomContext room)
        {
            IReadOnlyList<string> listed = RunRecord.AppendedPaths(room.Root);
            string mine = room.Settings.ActivityFile;
            if (!listed.Any(p => string.Equals(p, mine, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"the activity file {mine} (room.json) is not on the list of appended files, {RunRecord.AppendedFileRelativePath}; a run started after the first operation would read as a run from a dirty tree. Add it there, or change room.json.");
        }

        private static string Opt(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static string WebRootFor(string root)
        {
            string inRepo = Path.Combine(root, "src", "Evosim.NurseryRoom", "wwwroot");
            return Directory.Exists(inRepo) ? inRepo : Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        /// <summary>
        /// What <c>/api/room</c> answers: the room's numbers, its slot as the machine's queue gives it now (the free cores, and the lines that claim cores
        /// inside the slot), and the heat verdict. A slot that is not known is shown with its reason, never as cores the screen would draw while a launch
        /// is refused for it.
        /// </summary>
        public static object RoomStatus(RoomContext room, DateTime nowUtc)
        {
            // A poll: the timing-read file is read, and a re-step that lives when one is declared is stopped.
            (object timingRead, object resteps) = EpisodeSteps.Status(room);
            int granted = room.GrantedThreads(nowUtc);
            Slot slot = room.ReadSlot();
            HeatReading heat = room.Heat.Read();
            bool known = slot.Problem == null;
            return new
            {
                timingRead,
                resteps,
                // What would refuse an episode step now (the tank screen's STEP button says it before it is pressed): null when nothing does.
                stepRefusal = EpisodeSteps.StepRefusal(room, slot, heat, granted),
                pollSeconds = room.Settings.PollSeconds,
                affinity = CoreMask.Format(room.Settings.Affinity),
                logical = room.Logical,
                root = room.Root,
                mainCheckout = room.MainCheckout,
                nowUtc,
                machine = new
                {
                    queue = room.Settings.QueueFile,
                    problem = slot.Problem,
                    slot = known ? CoreMask.List(slot.Room) : null,
                    slotCount = known ? CoreMask.Count(slot.Usable) : (int?)null,
                    free = known ? CoreMask.List(slot.Free) : null,
                    freeCount = known ? CoreMask.Count(slot.Free) : (int?)null,
                    // The threads this room granted that still count against the free cores (RoomContext.GrantedThreads).
                    granted,
                    explain = known ? slot.Explain() : null,
                    claims = slot.Claims.Select(c => new { line = c.Job.Text, cores = CoreMask.List(c.Inside), card = c.Job.Card, note = c.Job.CoresProblem }).ToList(),
                    // The jobs that use the graphics card, by their text: the room never does, and only shows the tag.
                    card = (slot.Queue?.Running ?? new List<QueueJob>()).Where(j => j.Card).Select(j => j.Text).ToList(),
                    readUtc = slot.Queue?.AtUtc,
                },
                heat = new { verdict = heat.Verdict, line = heat.Line, readUtc = heat.AtUtc },
            };
        }

        /// <summary>What <c>/api/resource</c> answers (with HTTP 200) when the read is refused: the status it stands for (404 nothing there, 400 a bad address) and why.</summary>
        public static object Refusal(int status, string error) => new { ok = false, refused = true, status, error };

        /// <param name="gitForRecipes">How the recipe resource asks git whether a file is committed; null runs git. The end-to-end tests give a fake for the states real git cannot make.</param>
        public static WebApplication Create(RoomContext room, string webRoot, GitRunner gitForRecipes = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot, ContentRootPath = room.Root });
            // Nothing outside this program may move the listener: no appsettings.json, no ASPNETCORE_URLS,
            // no Kestrel__ variable (the sources are dropped), and Listen() names the one address.
            builder.Configuration.Sources.Clear();
            IPAddress bindAddress = IPAddress.Parse(room.Settings.Bind);
            builder.WebHost.ConfigureKestrel(k => k.Listen(bindAddress, room.Settings.Port));
            builder.Services.Configure<HostFilteringOptions>(RoomGuards.ConfigureHosts);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            var ops = new OperationList(room);
            var resources = new ResourceResolver(room) { GitForRecipes = gitForRecipes };

            // The MCP endpoint: the official SDK, its tools and resources generated from the list and the resolver.
            builder.Services.AddMcpServer(o => { o.ServerInfo = new Implementation { Name = "nursery-room", Version = "1.0" }; })
                .WithHttpTransport(o => o.Stateless = true)
                .WithListToolsHandler((ctx, ct) => ValueTask.FromResult(new ListToolsResult { Tools = McpSurface.Tools(ops) }))
                .WithCallToolHandler((ctx, ct) => ValueTask.FromResult(McpSurface.Call(ops, ctx.Params.Name, ctx.Params.Arguments)))
                .WithListResourcesHandler((ctx, ct) => ValueTask.FromResult(new ListResourcesResult { Resources = McpSurface.Resources(resources) }))
                .WithListResourceTemplatesHandler((ctx, ct) => ValueTask.FromResult(new ListResourceTemplatesResult { ResourceTemplates = McpSurface.Templates(resources) }))
                .WithReadResourceHandler((ctx, ct) => ValueTask.FromResult(McpSurface.Read(resources, ctx.Params.Uri)));

            WebApplication app = builder.Build();

            RoomGuards.Use(app, room.Settings.Port);

            var files = new PhysicalFileProvider(webRoot);
            var defaults = new DefaultFilesOptions { FileProvider = files };
            defaults.DefaultFileNames.Clear();
            defaults.DefaultFileNames.Add("registry.html");
            app.UseDefaultFiles(defaults);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = files,
                OnPrepareResponse = c => c.Context.Response.Headers["Cache-Control"] = "no-cache",
            });

            app.MapGet("/api/room", async () =>
            {
                // Read off the request's thread: the heat check runs a script, which takes seconds when its reading is not kept.
                object status = await Task.Run(() => RoomStatus(room, DateTime.UtcNow));
                return Results.Text(Js.Serialize(status), "application/json");
            });

            app.MapGet("/api/resources", () => Results.Text(Js.Serialize(new { resources = resources.List(), templates = resources.Templates }), "application/json"));

            app.MapGet("/api/resource", (HttpContext http) =>
            {
                string uri = http.Request.Query["uri"].FirstOrDefault();
                try { return Results.Text(Js.Serialize(resources.Read(uri)), "application/json"); }
                // A refused read is an answer, not a failed request: the screens ask about runs, genomes and recipes that may not be there (a run with no exam plan is an
                // ordinary state), and a 4xx would be logged in the browser's console as an error whatever the page does with it. So it is HTTP 200 with
                // { ok: false, refused: true, status: 404 | 400, error }, and the page reads `refused`. (The two file downloads below stay 4xx: they are links.)
                catch (ResourceNotFound e) { return Results.Text(Js.Serialize(Refusal(404, e.Message)), "application/json"); }
                catch (ResourceRefused e) { return Results.Text(Js.Serialize(Refusal(400, e.Message)), "application/json"); }
            });

            // A genome's text as the run stores it, as a download. A read like /api/resource (a plain link, never an operation): nothing is written, and it is
            // the one place a screen hands out a file.
            app.MapGet("/api/genome-file", (HttpContext http) =>
            {
                string run = http.Request.Query["run"].FirstOrDefault(), id = http.Request.Query["id"].FirstOrDefault();
                try
                {
                    if (string.IsNullOrEmpty(run) || string.IsNullOrEmpty(id)) throw new ResourceRefused("/api/genome-file needs a run and an id");
                    (string fileName, string text) = resources.GenomeText(run, id);
                    http.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                    return Results.Text(text, "application/json; charset=utf-8");
                }
                catch (ResourceNotFound e) { return Results.Text(Js.Serialize(new { ok = false, error = e.Message }), "application/json", statusCode: 404); }
                catch (ResourceRefused e) { return Results.Text(Js.Serialize(new { ok = false, error = e.Message }), "application/json", statusCode: 400); }
            });

            // One file of a finished episode (its header, its events, its frames), for the tank screen to play. A read like the one above: a plain GET,
            // never an operation, and only the three files the nursery writes for a client, and only once result.json is there.
            app.MapGet("/api/episode-file", (HttpContext http) =>
            {
                string run = http.Request.Query["run"].FirstOrDefault(), file = http.Request.Query["file"].FirstOrDefault();
                try
                {
                    if (string.IsNullOrEmpty(run) || string.IsNullOrEmpty(file)) throw new ResourceRefused("/api/episode-file needs a run, a brain, a draw, a tank and a file");
                    int[] n = new int[3];
                    string[] keys = { "b", "d", "t" };
                    for (int i = 0; i < 3; i++)
                        if (!int.TryParse(http.Request.Query[keys[i]].FirstOrDefault(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n[i]))
                            throw new ResourceRefused($"/api/episode-file's {keys[i]} is a whole number");
                    (string path, string type) = EpisodeSteps.FileOf(room, resources, run, n[0], n[1], n[2], file);
                    http.Response.Headers["Cache-Control"] = "no-cache";
                    return Results.File(path, type);
                }
                catch (ResourceNotFound e) { return Results.Text(Js.Serialize(new { ok = false, error = e.Message }), "application/json", statusCode: 404); }
                catch (ResourceRefused e) { return Results.Text(Js.Serialize(new { ok = false, error = e.Message }), "application/json", statusCode: 400); }
            });

            app.MapPost("/api/op/{id}", async (string id, HttpContext http) =>
            {
                JsonElement? body = null;
                if (http.Request.ContentLength != 0)
                {
                    try
                    {
                        using (JsonDocument d = await JsonDocument.ParseAsync(http.Request.Body))
                            body = d.RootElement.Clone();
                    }
                    catch (JsonException e)
                    {
                        return Results.Text(Js.Serialize(new { operation = id, ok = false, error = "the body is not JSON: " + e.Message }), "application/json", statusCode: 400);
                    }
                }
                OperationResult r = ops.Invoke(id, body, OperationList.Screen);
                return Results.Text(Js.Serialize(r.Envelope()), "application/json", statusCode: r.Status);
            });

            app.MapMcp("/mcp");
            return app;
        }
    }
}
