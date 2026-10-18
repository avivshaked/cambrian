using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Evosim.NurseryRoom.Tests.E2e
{
    /// <summary>
    /// A fact that drives headless Edge: skipped, with the reason, where Edge is not installed (as <see cref="RealRunFactAttribute"/> skips where the real runs are
    /// not). It carries the <c>E2e</c> trait, so that <c>--filter "Category!=E2e"</c> leaves every flow out.
    /// </summary>
    public sealed class EdgeFactAttribute : FactAttribute
    {
        public EdgeFactAttribute()
        {
            if (EdgeBrowser.Find() == null) Skip = "skipped: Microsoft Edge is not at " + string.Join(" or ", EdgeBrowser.Candidates) + ", so there is no browser to drive";
        }
    }

    /// <summary>
    /// A small driver for headless Edge over the DevTools protocol, with <see cref="ClientWebSocket"/> and nothing added to the project: navigate, evaluate script, click, press a key,
    /// set the window's size, and collect what went wrong in the page (a console error, an exception, a failed request, a 4xx or 5xx). Edge keeps its own profile folder
    /// (<c>--user-data-dir</c>) under the test program's folder, and it is stopped and the folder deleted when this is disposed, also when the flow failed.
    /// </summary>
    internal sealed class EdgeBrowser : IDisposable
    {
        public static readonly string[] Candidates =
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        };

        public static string Find() => Candidates.FirstOrDefault(File.Exists);

        private readonly string _profile;
        private readonly Process _process;
        private readonly ClientWebSocket _ws = new ClientWebSocket();
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new ConcurrentDictionary<int, TaskCompletionSource<JsonElement>>();
        private readonly object _gate = new object();
        private readonly List<string> _problems = new List<string>();
        private readonly List<string> _requests = new List<string>();
        private readonly Dictionary<string, string> _urlOf = new Dictionary<string, string>();
        private readonly HashSet<string> _inflight = new HashSet<string>();
        private readonly Task _reader;
        private int _id;
        private int _loads;
        private long _quietSince = Environment.TickCount64;

        /// <summary>Starts Edge headless on a profile folder of its own (under <paramref name="profileParent"/>, which the caller owns), the window <paramref name="width"/> by <paramref name="height"/>.</summary>
        public EdgeBrowser(string profileParent, int width, int height)
        {
            string exe = Find() ?? throw new InvalidOperationException("Edge is not installed");
            _profile = Path.Combine(profileParent, "edge-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_profile);
            var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string a in new[]
            {
                "--headless=new", "--remote-debugging-port=0", "--user-data-dir=" + _profile, $"--window-size={width},{height}", "--no-first-run", "--no-default-browser-check",
                "--disable-gpu", "--disable-extensions", "--disable-background-networking", "--disable-sync", "--disable-features=msEdgeSignin", "about:blank",
            }) info.ArgumentList.Add(a);
            try
            {
                _process = Process.Start(info);
                string portFile = Path.Combine(_profile, "DevToolsActivePort");
                string port = null;
                for (int i = 0; i < 300 && port == null; i++)
                {
                    if (_process.HasExited) throw new InvalidOperationException("Edge ended before it opened its debugging port");
                    try { if (File.Exists(portFile)) port = File.ReadAllLines(portFile).FirstOrDefault(); } catch (IOException) { }
                    if (port == null) Thread.Sleep(100);
                }
                if (port == null) throw new TimeoutException("Edge did not open its debugging port in 30 s");
                string wsUrl = null;
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                    for (int i = 0; i < 100 && wsUrl == null; i++)
                    {
                        try
                        {
                            using JsonDocument targets = JsonDocument.Parse(http.GetStringAsync($"http://127.0.0.1:{port}/json").GetAwaiter().GetResult());
                            foreach (JsonElement t in targets.RootElement.EnumerateArray())
                                if (t.GetProperty("type").GetString() == "page") { wsUrl = t.GetProperty("webSocketDebuggerUrl").GetString(); break; }
                        }
                        catch (HttpRequestException) { }
                        if (wsUrl == null) Thread.Sleep(100);
                    }
                if (wsUrl == null) throw new TimeoutException("Edge listed no page to drive");
                _ws.ConnectAsync(new Uri(wsUrl), _stop.Token).GetAwaiter().GetResult();
                _reader = Task.Run(ReadLoop);
                foreach (string domain in new[] { "Page", "Runtime", "Network", "Log" }) Send(domain + ".enable");
                SetSize(width, height);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // ------------------------------------------------------------------ what went wrong in the page, and what it asked for

        /// <summary>
        /// Every console error, exception, failed request and 4xx or 5xx the page has had since <see cref="ClearProblems"/>. A request the browser cancelled itself (a page left
        /// while it loaded) is not one.
        /// </summary>
        public IReadOnlyList<string> Problems { get { lock (_gate) return _problems.ToArray(); } }

        public void ClearProblems() { lock (_gate) _problems.Clear(); }

        /// <summary>Every request the page made, as <c>METHOD url</c>, since <see cref="ClearRequests"/>.</summary>
        public IReadOnlyList<string> Requests { get { lock (_gate) return _requests.ToArray(); } }

        public void ClearRequests() { lock (_gate) _requests.Clear(); }

        private void Problem(string text) { lock (_gate) _problems.Add(text); }

        private void OnEvent(string method, JsonElement p)
        {
            switch (method)
            {
                case "Page.loadEventFired": Interlocked.Increment(ref _loads); break;
                case "Runtime.consoleAPICalled":
                {
                    string type = p.GetProperty("type").GetString();
                    if (type == "error" || type == "assert")
                        Problem("console." + type + ": " + string.Join(" ", p.GetProperty("args").EnumerateArray().Select(a =>
                            a.TryGetProperty("value", out JsonElement v) ? v.ToString() : a.TryGetProperty("description", out JsonElement d) ? d.GetString() : a.GetProperty("type").GetString())));
                    break;
                }
                case "Runtime.exceptionThrown":
                {
                    JsonElement d = p.GetProperty("exceptionDetails");
                    string what = d.TryGetProperty("exception", out JsonElement ex) && ex.TryGetProperty("description", out JsonElement desc) ? desc.GetString() : d.GetProperty("text").GetString();
                    Problem("exception: " + what);
                    break;
                }
                case "Log.entryAdded":
                {
                    JsonElement e = p.GetProperty("entry");
                    // A failed request is reported by the Network events below; the log says it again.
                    if (e.GetProperty("level").GetString() == "error" && !(e.TryGetProperty("source", out JsonElement src) && src.GetString() == "network"))
                        Problem("log.error: " + e.GetProperty("text").GetString() + (e.TryGetProperty("url", out JsonElement u) ? " " + u.GetString() : ""));
                    break;
                }
                case "Network.requestWillBeSent":
                {
                    string id = p.GetProperty("requestId").GetString(), url = p.GetProperty("request").GetProperty("url").GetString();
                    lock (_gate)
                    {
                        _requests.Add(p.GetProperty("request").GetProperty("method").GetString() + " " + url);
                        _urlOf[id] = url;
                        _inflight.Add(id);
                    }
                    break;
                }
                case "Network.responseReceived":
                {
                    JsonElement r = p.GetProperty("response");
                    int status = r.GetProperty("status").GetInt32();
                    if (status >= 400) Problem("HTTP " + status + " " + r.GetProperty("url").GetString());
                    break;
                }
                case "Network.loadingFinished":
                    lock (_gate) { _inflight.Remove(p.GetProperty("requestId").GetString()); _quietSince = Environment.TickCount64; }
                    break;
                case "Network.loadingFailed":
                {
                    string id = p.GetProperty("requestId").GetString();
                    bool cancelled = p.TryGetProperty("canceled", out JsonElement c) && c.GetBoolean();
                    lock (_gate)
                    {
                        if (!cancelled) _problems.Add("failed request " + (_urlOf.TryGetValue(id, out string u) ? u : id) + ": " + p.GetProperty("errorText").GetString());
                        _inflight.Remove(id);
                        _quietSince = Environment.TickCount64;
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ the protocol

        private async Task ReadLoop()
        {
            var buffer = new byte[1 << 20];
            try
            {
                while (!_stop.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _stop.Token).ConfigureAwait(false);
                        if (r.MessageType == WebSocketMessageType.Close) return;
                        message.Write(buffer, 0, r.Count);
                    } while (!r.EndOfMessage);
                    using JsonDocument doc = JsonDocument.Parse(message.ToArray());
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("id", out JsonElement id))
                    {
                        if (_pending.TryRemove(id.GetInt32(), out var tcs))
                        {
                            if (root.TryGetProperty("error", out JsonElement err)) tcs.TrySetException(new InvalidOperationException("DevTools: " + err.GetProperty("message").GetString()));
                            else tcs.TrySetResult(root.TryGetProperty("result", out JsonElement res) ? res.Clone() : default);
                        }
                    }
                    else if (root.TryGetProperty("method", out JsonElement m) && root.TryGetProperty("params", out JsonElement p))
                    {
                        try { OnEvent(m.GetString(), p); }
                        catch (Exception e) when (e is KeyNotFoundException || e is InvalidOperationException || e is FormatException) { Problem("the driver could not read the event " + m.GetString() + ": " + e.Message); }
                    }
                }
            }
            catch (Exception) when (_stop.IsCancellationRequested || _ws.State != WebSocketState.Open) { }
            finally { foreach (var t in _pending.Values) t.TrySetException(new InvalidOperationException("the browser's connection ended")); }
        }

        /// <summary>One protocol call; its result.</summary>
        public JsonElement Send(string method, object parameters = null, int timeoutMs = 60000)
        {
            int id = Interlocked.Increment(ref _id);
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;
            string text = JsonSerializer.Serialize(new Dictionary<string, object> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new Dictionary<string, object>() });
            lock (_ws) _ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, _stop.Token).GetAwaiter().GetResult();
            if (!tcs.Task.Wait(timeoutMs)) throw new TimeoutException(method + " got no answer in " + timeoutMs / 1000 + " s");
            return tcs.Task.Result;
        }

        // ------------------------------------------------------------------ what a flow does

        public void SetSize(int width, int height)
        {
            Send("Emulation.setDeviceMetricsOverride", new Dictionary<string, object> { ["width"] = width, ["height"] = height, ["deviceScaleFactor"] = 1, ["mobile"] = false });
            Settle();
            Thread.Sleep(350);   // the screens redraw their charts 120 ms after a resize
        }

        /// <summary>Opens the address and waits for the page's load and for its requests to be over.</summary>
        public void Navigate(string url)
        {
            int before = Volatile.Read(ref _loads);
            Send("Page.navigate", new Dictionary<string, object> { ["url"] = url });
            long until = Environment.TickCount64 + 30000;
            while (Volatile.Read(ref _loads) == before)
            {
                if (Environment.TickCount64 > until) throw new TimeoutException("the page did not load in 30 s: " + url);
                Thread.Sleep(25);
            }
            Settle();
        }

        /// <summary>Waits until no request has been in flight for 300 ms.</summary>
        public void Settle(int timeoutMs = 30000)
        {
            long until = Environment.TickCount64 + timeoutMs;
            while (true)
            {
                bool quiet;
                lock (_gate) quiet = _inflight.Count == 0 && Environment.TickCount64 - _quietSince >= 300;
                if (quiet) return;
                if (Environment.TickCount64 > until) throw new TimeoutException("the page's requests did not end in " + timeoutMs / 1000 + " s");
                Thread.Sleep(25);
            }
        }

        /// <summary>The value of a script (it may return a promise), as JSON; an exception in the script is thrown here with its text.</summary>
        public JsonElement Eval(string script)
        {
            JsonElement r = Send("Runtime.evaluate", new Dictionary<string, object> { ["expression"] = script, ["awaitPromise"] = true, ["returnByValue"] = true, ["timeout"] = 30000 });
            if (r.TryGetProperty("exceptionDetails", out JsonElement d))
                throw new InvalidOperationException("the script threw: " + (d.TryGetProperty("exception", out JsonElement ex) && ex.TryGetProperty("description", out JsonElement desc) ? desc.GetString() : d.GetProperty("text").GetString()));
            return r.GetProperty("result").TryGetProperty("value", out JsonElement v) ? v.Clone() : default;
        }

        public string EvalString(string script) { JsonElement v = Eval(script); return v.ValueKind == JsonValueKind.Undefined || v.ValueKind == JsonValueKind.Null ? null : v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText(); }

        public bool EvalBool(string script) => Eval(script).ValueKind == JsonValueKind.True;

        public double EvalNumber(string script) => Eval(script).GetDouble();

        /// <summary>A script that returns an object, parsed.</summary>
        public JsonElement EvalObject(string script) => Eval(script);

        /// <summary>Polls a script until it is true (a truthy value), or fails with <paramref name="what"/> and what the page had gone wrong with by then.</summary>
        public void WaitFor(string script, string what, int timeoutMs = 20000)
        {
            long until = Environment.TickCount64 + timeoutMs;
            while (true)
            {
                JsonElement v = default;
                try { v = Eval("!!(" + script + ")"); }
                catch (InvalidOperationException) { /* the page is changing (a navigation): asked again */ }
                if (v.ValueKind == JsonValueKind.True) return;
                if (Environment.TickCount64 > until) throw new TimeoutException("waited " + timeoutMs / 1000 + " s for " + what + " (" + script + "); the page's problems: " + string.Join(" | ", Problems));
                Thread.Sleep(80);
            }
        }

        /// <summary>A real mouse click at the middle of the first element that matches, after it is scrolled into view.</summary>
        public void Click(string selector)
        {
            JsonElement pos = Eval("(() => { const e = document.querySelector(" + JsonSerializer.Serialize(selector) + "); if (!e) return null; e.scrollIntoView({ block: 'center' }); const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()");
            if (pos.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("nothing to click: " + selector);
            double x = pos.GetProperty("x").GetDouble(), y = pos.GetProperty("y").GetDouble();
            Send("Input.dispatchMouseEvent", new Dictionary<string, object> { ["type"] = "mouseMoved", ["x"] = x, ["y"] = y });
            Send("Input.dispatchMouseEvent", new Dictionary<string, object> { ["type"] = "mousePressed", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 });
            Send("Input.dispatchMouseEvent", new Dictionary<string, object> { ["type"] = "mouseReleased", ["x"] = x, ["y"] = y, ["button"] = "left", ["clickCount"] = 1 });
        }

        private static readonly Dictionary<string, (int code, string name)> Keys = new Dictionary<string, (int, string)>
        {
            ["ArrowDown"] = (40, "ArrowDown"), ["ArrowUp"] = (38, "ArrowUp"), ["Escape"] = (27, "Escape"), ["Enter"] = (13, "Enter"),
        };

        /// <summary>A key press (ArrowDown, ArrowUp, Escape, Enter, or one character) on whatever has the focus.</summary>
        public void Press(string key)
        {
            if (Keys.TryGetValue(key, out var k))
            {
                var d = new Dictionary<string, object> { ["type"] = "rawKeyDown", ["key"] = k.name, ["code"] = k.name, ["windowsVirtualKeyCode"] = k.code };
                if (key == "Enter") { d["type"] = "keyDown"; d["text"] = "\r"; }
                Send("Input.dispatchKeyEvent", d);
                Send("Input.dispatchKeyEvent", new Dictionary<string, object> { ["type"] = "keyUp", ["key"] = k.name, ["code"] = k.name, ["windowsVirtualKeyCode"] = k.code });
            }
            else
            {
                Send("Input.dispatchKeyEvent", new Dictionary<string, object> { ["type"] = "keyDown", ["key"] = key, ["text"] = key, ["unmodifiedText"] = key });
                Send("Input.dispatchKeyEvent", new Dictionary<string, object> { ["type"] = "keyUp", ["key"] = key });
            }
        }

        /// <summary>Types text into the element that has the focus.</summary>
        public void Type(string text) => Send("Input.insertText", new Dictionary<string, object> { ["text"] = text });

        public void Dispose()
        {
            try { _stop.Cancel(); } catch (ObjectDisposedException) { }
            try { _ws.Abort(); } catch (Exception) { }
            try { _reader?.Wait(2000); } catch (Exception) { }
            if (_process != null)
            {
                try { if (!_process.HasExited) _process.Kill(true); } catch (Exception) { }
                try { _process.WaitForExit(5000); } catch (Exception) { }
                _process.Dispose();
            }
            // Edge's own children may hold the profile for a moment after the kill.
            for (int attempt = 0; attempt < 40 && Directory.Exists(_profile); attempt++)
            {
                try { Directory.Delete(_profile, true); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Thread.Sleep(250); }
            }
            _ws.Dispose();
            _stop.Dispose();
        }
    }
}
