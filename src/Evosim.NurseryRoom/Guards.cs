using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// What stands between the room and a web page on this machine that is not the room's own. The
    /// server listens on loopback, but any page the owner has open can ask a loopback address for
    /// things, and a hostile name can resolve to one. So the room answers only to its own names (the
    /// Host header), refuses a request that names another origin (the Origin header a browser adds to
    /// a cross-site request, whatever the path, <c>/mcp</c> included), and takes an operation's
    /// arguments only as <c>application/json</c>, which a cross-site form cannot send.
    /// </summary>
    public static class RoomGuards
    {
        /// <summary>The only host names the room answers to.</summary>
        public static readonly string[] AllowedHosts = { "127.0.0.1", "localhost", "[::1]" };

        public static void ConfigureHosts(HostFilteringOptions o)
        {
            o.AllowedHosts = new List<string>(AllowedHosts);
            o.AllowEmptyHosts = false;
            o.IncludeFailureMessage = true;
        }

        /// <summary>The origins the room's own screens call from: its two loopback names, on its port.</summary>
        public static string[] AllowedOrigins(int port) => new[] { $"http://127.0.0.1:{port}", $"http://localhost:{port}" };

        /// <summary>Whether an Origin header value is one of the room's own.</summary>
        public static bool OriginAllowed(StringValues origin, int port) =>
            origin.Count == 1 && AllowedOrigins(port).Contains(origin[0], StringComparer.OrdinalIgnoreCase);

        public static bool IsJson(string contentType) =>
            !string.IsNullOrEmpty(contentType) && MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue t)
            && string.Equals(t.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase);

        /// <summary>The guards as middleware: host filtering first, then the origin and the content type.</summary>
        public static void Use(IApplicationBuilder app, int port)
        {
            app.UseHostFiltering();
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Headers.TryGetValue(HeaderNames.Origin, out StringValues origin) && !OriginAllowed(origin, port))
                {
                    await Refuse(ctx, 403, $"the request comes from the origin \"{origin}\"; the room answers only to its own pages");
                    return;
                }
                if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.StartsWithSegments("/api/op", StringComparison.OrdinalIgnoreCase)
                    && !IsJson(ctx.Request.ContentType))
                {
                    await Refuse(ctx, 415, "an operation takes its arguments as Content-Type: application/json");
                    return;
                }
                await next();
            });
        }

        private static async System.Threading.Tasks.Task Refuse(HttpContext ctx, int status, string error)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(Js.Serialize(new { ok = false, error }));
        }

        /// <summary>True when an address the server reports is a loopback one.</summary>
        public static bool IsLoopbackAddress(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri u)) return false;
            string host = u.Host.Trim('[', ']');
            return IPAddress.TryParse(host, out IPAddress ip) && IPAddress.IsLoopback(ip);
        }
    }
}
