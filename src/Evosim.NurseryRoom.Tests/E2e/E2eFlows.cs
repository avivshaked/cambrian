using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Evosim.Nursery;
using Xunit;

namespace Evosim.NurseryRoom.Tests.E2e
{
    /// <summary>
    /// The room's screens driven in headless Edge, as the browser walks drove them (logbook/specs/nursery-room-tests-plan.md, step 3; the checks are walk 7's, scratch/nursery-general/room7/walk7-checks.md).
    /// Each flow hosts the room in this process on a free port with the fake launcher, on runs and recipes the test makes (<see cref="E2eWorld"/>), and measures the page. A flow never writes through the room:
    /// a dialog is opened and cancelled, and the flow asserts that nothing was posted, nothing started and no activity row written. Layout is checked by measuring (<see cref="Probes"/>), never against a screenshot.
    /// Where Edge is not installed a flow is skipped, with the reason. <c>--filter "Category!=E2e"</c> leaves them out.
    /// </summary>
    [Trait("Category", "E2e")]
    public class E2eFlows
    {
        /// <summary>The note every dialog that asks for a reason carries under the box (the owner's ruling of 2026-10-05).</summary>
        private const string ReasonKept = "The reason is kept in the room's activity log, which is committed to git and may be published.";

        private static readonly (int W, int H)[] Sizes = { (1920, 1080), (1366, 768) };

        // ------------------------------------------------------------------ helpers

        private static string[] Strings(EdgeBrowser b, string script) => b.Eval(script).EnumerateArray().Select(x => x.GetString()).ToArray();

        private static string Dialog(EdgeBrowser b) => b.EvalString("document.querySelector('form.ask') ? document.querySelector('form.ask').innerText.replace(/\\s+/g, ' ') : null");

        /// <summary>Opens a dialog by its button, and waits until what the dialog reads (git, the machine) has come in. Returns its text.</summary>
        private static string OpenDialog(EdgeBrowser b, string button, string key, bool git = false)
        {
            b.Click(button);
            b.WaitFor("document.querySelector('form.ask[data-ask=\"" + key + "\"]')", "the " + key + " dialog to open");
            b.WaitFor("!document.querySelector('form.ask .ask__machine.dim')", "the dialog's machine line");
            if (git) b.WaitFor("document.querySelector('form.ask .ask__git') && !document.querySelector('form.ask .ask__git.dim')", "the dialog's git line");
            return Dialog(b);
        }

        private static void CancelDialog(EdgeBrowser b)
        {
            b.Click("form.ask [data-cancel]");
            b.WaitFor("!document.querySelector('form.ask')", "the dialog to close");
        }

        /// <summary>The dialog asks for a reason and says, under the box, where the reason goes.</summary>
        private static void AssertReasonNote(EdgeBrowser b, string dialog, string text)
        {
            Assert.True(text.Contains(ReasonKept), $"the {dialog} dialog does not say, under its reason box, that the reason is kept in the activity log which is committed to git and may be published; it reads: {text}");
            Assert.True(b.EvalBool("(() => { const k = document.querySelector('form.ask .ask__keep'), i = document.querySelector('form.ask input[name=reason]'); return !!k && !!i && k.getBoundingClientRect().top >= i.getBoundingClientRect().bottom - 1; })()"),
                $"the {dialog} dialog's note is not under its reason box");
        }

        /// <summary>A flow never writes through the room: nothing posted, nothing started, no activity row.</summary>
        private static void AssertNothingWritten(E2eWorld w, EdgeBrowser b)
        {
            // A read can be a POST (recipe.diff, which the screens ask for): what is held to nothing is every operation that changes something.
            string[] changing = w.Fx.Ops.All.Where(o => o.Mutates).Select(o => "/api/op/" + o.Id).ToArray();
            string[] posted = b.Requests.Where(r => r.StartsWith("POST ") && changing.Any(c => r.EndsWith(c))).ToArray();
            Assert.True(posted.Length == 0, "the page posted an operation that changes something: " + string.Join(", ", posted));
            Assert.Empty(w.Ep.Launcher.Started);
            Assert.Empty(w.Ep.ActivityRows());
        }

        private static void AssertClean(EdgeBrowser b, string where) =>
            Assert.True(b.Problems.Count == 0, where + ": the page had " + b.Problems.Count + " problem(s): " + string.Join(" | ", b.Problems));

        /// <summary>No page wider than the window, nothing shown that must never be, no text running out of its box, no text boxes over each other.</summary>
        private static void AssertAudit(EdgeBrowser b, string where)
        {
            JsonElement a = b.Eval(Probes.Audit);
            int inner = a.GetProperty("innerW").GetInt32();
            Assert.True(a.GetProperty("scrollW").GetInt32() <= inner && a.GetProperty("bodyW").GetInt32() <= inner,
                $"{where}: the page is {a.GetProperty("scrollW").GetInt32()} wide in a window of {inner}");
            Assert.True(a.GetProperty("shown").GetArrayLength() == 0, where + ": shown: " + a.GetProperty("shown").GetRawText());
            Assert.True(a.GetProperty("runsOut").GetArrayLength() == 0, where + ": text runs out of its box: " + a.GetProperty("runsOut").GetRawText());
            Assert.True(a.GetProperty("overlaps").GetArrayLength() == 0, where + ": text boxes overlap: " + a.GetProperty("overlaps").GetRawText());
        }

        private static void LoadRun(EdgeBrowser b, RoomHost host, string query)
        {
            b.Navigate(host.Base + "/run.html?" + query);
            b.WaitFor("document.querySelector('.exam-t') && document.querySelector('#c-search') && document.querySelector('#c-search').childElementCount > 0" + (query.Contains("vs=") ? " && document.querySelector('.exam-t tbody.vs')" : ""), "the run screen's exam table and search chart");
            b.Settle();
        }

        /// <summary>Each chart is drawn at its box's size (never a fixed size letterboxed in it), and the brains panel holds no more than it shows.</summary>
        private static void AssertRunLayout(EdgeBrowser b, string where)
        {
            List<JsonElement> charts = b.Eval(Probes.Charts).EnumerateArray().ToList();
            Assert.True(charts.Count >= 10, where + ": only " + charts.Count + " charts are in a box");
            string[] bad = charts.Where(c => !c.GetProperty("matches").GetBoolean() || !c.GetProperty("drawn").GetBoolean())
                .Select(c => $"{c.GetProperty("panel").GetString()} / {c.GetProperty("id").GetString()} is drawn at {c.GetProperty("viewBox")[0]}x{c.GetProperty("viewBox")[1]} in a box of {c.GetProperty("box")[0]}x{c.GetProperty("box")[1]}{(c.GetProperty("drawn").GetBoolean() ? "" : " and holds nothing")}").ToArray();
            Assert.True(bad.Length == 0, where + ": charts not drawn at their box's size: " + string.Join("; ", bad));

            JsonElement p = b.Eval(Probes.BrainsPanel);
            Assert.True(p.GetProperty("charts").GetInt32() > 0 && p.GetProperty("fields").GetInt32() > 0, where + ": the brains panel is empty");
            Assert.True(p.GetProperty("bodyScrollH").GetInt32() <= p.GetProperty("bodyClientH").GetInt32() + 1 && p.GetProperty("innerScrollH").GetInt32() <= p.GetProperty("innerClientH").GetInt32() + 1,
                $"{where}: the brains panel scrolls inside itself (scroll height {p.GetProperty("bodyScrollH").GetInt32()}, client height {p.GetProperty("bodyClientH").GetInt32()})");
            Assert.False(p.GetProperty("lastFieldBelowPanel").GetBoolean(), where + ": the brains panel's last field is below the panel");
        }

        private static void AssertBandOneRow(EdgeBrowser b, string where)
        {
            JsonElement band = b.Eval(Probes.Band);
            Assert.True(band.GetProperty("oneRow").GetBoolean(), where + ": the band is not one row: " + band.GetProperty("kids").GetRawText());
            Assert.True(band.GetProperty("pastEdge").GetArrayLength() == 0, where + ": the band's parts run past its edge: " + band.GetProperty("pastEdge").GetRawText());
            Assert.True(band.GetProperty("scrollW").GetInt32() <= band.GetProperty("clientW").GetInt32() + 1, where + ": the band is wider than it shows");
        }

        // ------------------------------------------------------------------ 1. the registry

        [EdgeFact]
        public void RegistryListsTheRunsAndKeysMoveTheSelectionAndTheFilterWorksAndARunOpens()
        {
            using var w = new E2eWorld();
            using RoomHost host = w.Host();
            using var b = new EdgeBrowser(w.Fx.Dir, 1920, 1080);

            b.Navigate(host.Base + "/");
            b.WaitFor("document.querySelectorAll('#list tr[data-key]').length === 6", "the six fixture runs listed");
            Assert.Equal(new[] { "1|entry", "5|entry", "4|entry", "3|entry", "2|entry", "live|run" }, Strings(b, "[...document.querySelectorAll('#list tr[data-key]')].map(r => r.dataset.key)"));
            Assert.Equal(new[] { "brain2", "e2e-delta", "e2e-gamma", "e2e-beta", "e2e-alpha", "live" }, Strings(b, "[...document.querySelectorAll('#list tr[data-key] .nm')].map(n => n.textContent)"));
            Assert.Equal(new[] { "all 6", "entries 5", "runs 1", "completed 4", "failed 1", "search 1" }, Strings(b, "[...document.querySelectorAll('#filters button')].map(x => x.textContent)"));
            Assert.Equal(new[] { "recipe.html", "run.html", "tank.html", "genome.html" }, Strings(b, "[...document.querySelectorAll('#strip .tabs a:not(#tab-registry)')].map(a => a.getAttribute('href').split('?')[0])"));

            // C21: down, down, up move the selection, and the panel beside follows it
            string Selected() => b.EvalString("(document.querySelector('#list tr.sel') || {}).dataset ? document.querySelector('#list tr.sel').dataset.key : null");
            b.Press("ArrowDown");
            b.WaitFor("document.querySelector('#list tr.sel') && document.querySelector('#list tr.sel').dataset.key === '5|entry'", "the selection on 5 after the first down");
            b.Press("ArrowDown");
            b.WaitFor("document.querySelector('#list tr.sel').dataset.key === '4|entry'", "the selection on 4 after the second down");
            Assert.Contains("e2e-gamma", b.EvalString("document.querySelector('#sel h2').innerText"));
            b.Press("ArrowUp");
            b.WaitFor("document.querySelector('#list tr.sel').dataset.key === '5|entry'", "the selection back on 5 after up");
            Assert.Equal("5|entry", Selected());

            // the filter: a chip, then the box ("/" focuses it), then Escape clears it
            b.Click("#filters button[data-f='failed']");
            b.WaitFor("document.querySelectorAll('#list tr[data-key]').length === 1", "one row under the failed chip");
            Assert.Equal(new[] { "5|entry" }, Strings(b, "[...document.querySelectorAll('#list tr[data-key]')].map(r => r.dataset.key)"));
            b.Click("#filters button[data-f='all']");
            b.WaitFor("document.querySelectorAll('#list tr[data-key]').length === 6", "six rows under the all chip");
            b.Press("/");
            Assert.True(b.EvalBool("document.activeElement && document.activeElement.id === 'q'"), "the slash key did not focus the filter box");
            b.Type("gamma");
            b.WaitFor("document.querySelectorAll('#list tr[data-key]').length === 1", "one row for the text gamma");
            Assert.Equal(new[] { "4|entry" }, Strings(b, "[...document.querySelectorAll('#list tr[data-key]')].map(r => r.dataset.key)"));
            b.Press("Escape");
            b.WaitFor("document.querySelectorAll('#list tr[data-key]').length === 6", "six rows after Escape");
            Assert.Equal("", b.EvalString("document.getElementById('q').value"));

            // the running run is stoppable: its STOP opens a dialog that says where the reason goes (and is cancelled)
            b.Click("#list tr[data-key='live|run']");
            b.WaitFor("document.querySelector('#list tr.sel').dataset.key === 'live|run'", "the live run selected");
            string stop = OpenDialog(b, "#sel [data-op='run.stop']", "stop");
            Assert.Contains("Writes STOP into the run's folder", stop);
            AssertReasonNote(b, "stop", stop);
            CancelDialog(b);

            // the registry's RERUN says how many threads fit too, and the same note
            b.Click("#list tr[data-key='2|entry']");
            b.WaitFor("document.querySelector('#list tr.sel').dataset.key === '2|entry'", "entry 2 selected");
            string rerun = OpenDialog(b, "#sel [data-op='run.rerun']", "rerun");
            Assert.True(Regex.IsMatch(rerun, @"so at most 16 threads fit\b"), "the registry's RERUN dialog does not say how many threads fit: " + rerun);   // the default queue: the slot's 16 cores, none claimed
            AssertReasonNote(b, "rerun", rerun);
            CancelDialog(b);

            // opening a run reaches its run screen: Enter on a selected row, then R for its recipe
            b.Click("#list tr[data-key='4|entry']");
            b.WaitFor("document.querySelector('#list tr.sel').dataset.key === '4|entry'", "row 4 selected");
            b.Press("Enter");
            b.WaitFor("location.pathname.endsWith('run.html') && location.search === '?id=4' && document.title.includes('e2e-gamma')", "the run screen of entry 4");
            b.WaitFor("document.querySelector('.exam-t') && document.getElementById('name').innerText.includes('e2e-gamma')", "the run screen drawn");
            b.Navigate(host.Base + "/");
            b.WaitFor("document.querySelector('#list tr.sel') && document.querySelector('#list tr.sel').dataset.key === '4|entry'", "the run last looked at is the one selected");
            b.Press("r");
            b.WaitFor("location.pathname.endsWith('recipe.html') && location.search.includes('e2e-gamma.json')", "the recipe of entry 4");

            AssertClean(b, "the registry flow");
            AssertNothingWritten(w, b);
        }

        // ------------------------------------------------------------------ 2. the run screen, with and without a control

        [EdgeFact]
        public void RunScreenDrawsTheExamGreysWhatTheGenomesLeaveUnchangedAndKeepsTheControlsSideAndTheEmptyCellsUngreyed()
        {
            using var w = new E2eWorld();
            using RoomHost host = w.Host();
            using var b = new EdgeBrowser(w.Fx.Dir, 1920, 1080);

            // alpha alone: both genomes read only Chemical, so the Flow knockout leaves them unchanged and its whole column is greyed, with its reason in every title (C4)
            LoadRun(b, host, "id=2");
            JsonElement ex = b.Eval(Probes.Exam);
            Assert.Equal(7, ex.GetProperty("headings").GetArrayLength());          // genome, own, and five brains
            Assert.Equal(2, ex.GetProperty("body").GetArrayLength());
            Assert.Equal(1, ex.GetProperty("offHeadings").GetInt32());
            Assert.True(ex.GetProperty("flowCol").GetInt32() >= 2, "no column is headed Flow");
            JsonElement off = ex.GetProperty("offCells");
            Assert.True(off.GetArrayLength() == 3, "the greyed cells of alpha: two genomes and the pooled row, not " + off.GetArrayLength() + ": " + off.GetRawText());
            foreach (JsonElement c in off.EnumerateArray())
                Assert.True(c.GetProperty("title").GetString().Contains("zero by construction"), "a greyed cell has no reason in its title: " + c.GetRawText());
            Assert.All(ex.GetProperty("flowBody").EnumerateArray(), c => Assert.True(c.GetProperty("off").GetBoolean() && c.GetProperty("title").GetString().EndsWith("by construction"), "a Flow cell of a genome that reads no Flow port is not greyed with its reason: " + c.GetRawText()));
            Assert.True(ex.GetProperty("flowPooled")[0].GetProperty("off").GetBoolean(), "the pooled Flow cell is not greyed");
            Assert.Contains("is greyed:", string.Join(" ", ex.GetProperty("note").EnumerateArray().Select(n => n.GetString())));
            // the heading's own title carries the reason too
            Assert.Contains("zero by construction", ex.GetProperty("headings").EnumerateArray().Single(h => h.GetProperty("off").GetBoolean()).GetProperty("title").GetString());

            // STOP is disabled with its reason (a completed run), RERUN opens its dialog, carries the machine line with the threads that fit, and cancels (C17)
            JsonElement stop = b.Eval("(() => { const s = document.querySelector('[data-op=\"run.stop\"]'); return { disabled: s.disabled, title: s.title, why: document.getElementById('stop-why').textContent }; })()");
            Assert.True(stop.GetProperty("disabled").GetBoolean(), "STOP is not disabled on a completed run");
            Assert.Contains("so there is nothing to stop", stop.GetProperty("title").GetString());
            Assert.Equal("not running: completed", stop.GetProperty("why").GetString());
            Assert.False(b.EvalBool("document.querySelector('[data-op=\"run.rerun\"]').disabled"), "RERUN is disabled for a registry entry that can be rerun");
            w.Fx.Queue.Text = FixedQueue.With("speed (a test) · cores: 16-19");   // twelve of the slot's sixteen cores are free
            string rerun = OpenDialog(b, "[data-op='run.rerun']", "rerun");
            Assert.Contains("rerun entry #2", rerun, StringComparison.OrdinalIgnoreCase);
            Assert.True(Regex.IsMatch(rerun, @"so at most 12 threads fit\b"), "the RERUN dialog's machine line does not say how many threads fit: " + rerun);
            AssertReasonNote(b, "rerun", rerun);
            CancelDialog(b);
            w.Fx.Queue.Text = FixedQueue.With("speed (a test) · cores: 16-30");   // one core of the slot is free, and is said in the singular
            string one = OpenDialog(b, "[data-op='run.rerun']", "rerun");
            Assert.True(one.Contains("so at most 1 thread fits") && !one.Contains("1 threads"), "the machine line does not say one thread in the singular: " + one);
            CancelDialog(b);
            w.Fx.Queue.Text = FixedQueue.With();

            // layout at both sizes, on alpha against its control gamma: the greyed column is only this run's side, the by-body plot is drawn (C5, C7)
            LoadRun(b, host, "id=2&vs=4");
            ex = b.Eval(Probes.Exam);
            Assert.Equal(new[] { "this run, pooled", "control, pooled", "differs from the control" }, ex.GetProperty("vs").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToArray());
            JsonElement flowVs = ex.GetProperty("flowVs");
            Assert.True(flowVs[0].GetProperty("off").GetBoolean(), "this run's pooled Flow cell is not greyed: " + flowVs[0].GetRawText());
            Assert.True(!flowVs[1].GetProperty("off").GetBoolean() && !flowVs[1].GetProperty("cls").GetString().Contains("off"), "the control's pooled Flow cell is greyed, and the grey says nothing of the control: " + flowVs[1].GetRawText());
            Assert.True(flowVs[2].GetProperty("off").GetBoolean() && (flowVs[2].GetProperty("text").GetString() == "differs" || flowVs[2].GetProperty("text").GetString() == "no"),
                "the differs cell of a brain both exams swam, one of them greyed, is not greyed with its value: " + flowVs[2].GetRawText());
            Assert.True(b.EvalNumber("document.querySelectorAll('#c-bodynet circle').length") >= 4, "the by-body plot is not drawn");
            Assert.Contains("only", b.EvalString("document.querySelector('#b-exam .bn-note') ? document.querySelector('#b-exam .bn-note').innerText : ''"));   // the runs' bodies differ
            foreach ((int width, int height) in Sizes)
            {
                b.SetSize(width, height);
                AssertRunLayout(b, $"alpha vs gamma at {width}x{height}");
                AssertAudit(b, $"alpha vs gamma at {width}x{height}");
                if (width == 1920) AssertBandOneRow(b, "alpha vs gamma at 1920");
            }
            b.SetSize(1920, 1080);

            // the control did not swim a brain (beta has no Flow knockout): that brain's "—" cell holds no value, so it is not greyed, and keeps its title (fix 4)
            LoadRun(b, host, "id=2&vs=3");
            ex = b.Eval(Probes.Exam);
            flowVs = ex.GetProperty("flowVs");
            Assert.True(flowVs[0].GetProperty("off").GetBoolean(), "this run's pooled Flow cell is not greyed against beta: " + flowVs[0].GetRawText());
            Assert.True(flowVs[1].GetProperty("text").GetString() == "—" && !flowVs[1].GetProperty("off").GetBoolean(), "the control's pooled cell for a brain it did not swim is not a plain dash: " + flowVs[1].GetRawText());
            JsonElement differs = flowVs[2];
            Assert.True(differs.GetProperty("text").GetString() == "—" && !differs.GetProperty("off").GetBoolean() && !differs.GetProperty("cls").GetString().Contains("off"),
                "the 'differs from the control' cell of a brain the control did not swim is a dash that is greyed: " + differs.GetRawText());
            Assert.Contains("did not swim this brain", differs.GetProperty("title").GetString());

            // with this run as the control, nothing is greyed (C5): gamma greys nothing, and alpha's greyed column leaves no trace in its view
            LoadRun(b, host, "id=4&vs=2");
            ex = b.Eval(Probes.Exam);
            Assert.True(ex.GetProperty("offCells").GetArrayLength() == 0 && ex.GetProperty("offHeadings").GetInt32() == 0,
                "a run that is the control is greyed in the view of one that greys nothing: " + ex.GetProperty("offCells").GetRawText());

            // delta: one genome of two reads Flow, so the column is greyed on one genome only, and says so (C7)
            LoadRun(b, host, "id=5");
            ex = b.Eval(Probes.Exam);
            Assert.Equal(0, ex.GetProperty("offHeadings").GetInt32());
            Assert.True(ex.GetProperty("flowBody")[0].GetProperty("off").GetBoolean() && !ex.GetProperty("flowBody")[1].GetProperty("off").GetBoolean(), "the Flow column is not greyed on exactly the genome that reads no Flow port: " + ex.GetProperty("flowBody").GetRawText());
            Assert.Contains("in 1 of its 2 genomes", string.Join(" ", ex.GetProperty("note").EnumerateArray().Select(n => n.GetString())));
            Assert.False(ex.GetProperty("flowPooled")[0].GetProperty("off").GetBoolean(), "the pooled Flow cell is greyed though one genome reads Flow");

            // a run that is not an entry cannot be rerun (C18), and a running one can be stopped, with the note on its dialog
            LoadRun(b, host, "id=2");
            b.Navigate(host.Base + "/run.html?id=live");
            b.WaitFor("document.getElementById('name') && document.getElementById('name').innerText.includes('live')", "the live run");
            JsonElement rerunOff = b.Eval("(() => { const r = document.querySelector('[data-op=\"run.rerun\"]'); return { disabled: r.disabled, title: r.title }; })()");
            Assert.True(rerunOff.GetProperty("disabled").GetBoolean() && rerunOff.GetProperty("title").GetString().StartsWith("only a registry entry can be rerun"), "RERUN of a run with no entry: " + rerunOff.GetRawText());
            string stopDialog = OpenDialog(b, "[data-op='run.stop']", "stop");
            AssertReasonNote(b, "stop", stopDialog);
            CancelDialog(b);

            AssertClean(b, "the run flow");
            AssertNothingWritten(w, b);
        }

        // ------------------------------------------------------------------ 3. the recipe screen

        [EdgeFact]
        public void RecipeScreenPlateMatchesGitLabelsHoldTheirPathsAndLaunchDialogsSayTheRightThingAndNothingIsPosted()
        {
            using var w = new E2eWorld();
            using RoomHost host = w.Host();
            using var b = new EdgeBrowser(w.Fx.Dir, 1920, 1080);

            void Open(string path)
            {
                b.Navigate(host.Base + "/recipe.html?path=" + Uri.EscapeDataString(path).Replace("%2F", "/"));
                b.WaitFor("document.getElementById('git-plate') && document.querySelectorAll('.row').length > 20", "the recipe " + path + " drawn");
            }
            string Plate() => b.EvalString("document.getElementById('git-plate').dataset.state");

            // C9: every label's title is the label, a dot, and the field's path
            Open(E2eWorld.Committed);
            JsonElement titles = b.Eval(@"(() => { let rows = 0, exact = 0; const wrong = [];
              for (const r of document.querySelectorAll('.row:not(.row--dropped)')) { const l = r.querySelector('label'); if (!l) continue; rows++;
                const code = (l.querySelector('code') || {}).textContent || ''; const own = [...l.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent).join('');
                if (l.title === own + ' · ' + code) exact++; else wrong.push({ own, code, title: l.title }); }
              return { rows, exact, wrong: wrong.slice(0, 5) }; })()");
            Assert.True(titles.GetProperty("rows").GetInt32() > 30 && titles.GetProperty("exact").GetInt32() == titles.GetProperty("rows").GetInt32(), "labels whose title is not 'label · path': " + titles.GetRawText());

            // C10: committed, by real git in the room's repository: the plate says so and the dialog adds no sentence about what the launch will be
            Assert.Equal("committed", Plate());
            Assert.Equal("COMMITTED", b.EvalString("document.getElementById('git-plate').innerText.trim()"));
            string launch = OpenDialog(b, "[data-op='run.launch']", "launch", git: true);
            Assert.Contains("Committed and unchanged", launch);
            Assert.DoesNotContain("This launch goes ahead", launch);
            Assert.Equal("LAUNCH", b.EvalString("document.querySelector('form.ask button[type=submit]').textContent"));
            Assert.Contains("launch brain2-w11", launch, StringComparison.OrdinalIgnoreCase);
            Assert.Matches(@"so at most 16 threads fit\b", launch);   // the default queue: the slot's 16 cores, none claimed
            AssertReasonNote(b, "launch", launch);
            CancelDialog(b);

            // the edit dialog says where its reason goes too
            string edit = OpenDialog(b, "[data-op='recipe.write']", "write");
            AssertReasonNote(b, "edit by hand", edit);
            CancelDialog(b);

            // C11: changed since its commit, and never committed: "its record will show the recipe was not committed" (fix 1 keeps it for these two)
            w.MakeChanged(E2eWorld.ChangedRecipe);
            Open(E2eWorld.ChangedRecipe);
            Assert.Equal("changed", Plate());
            launch = OpenDialog(b, "[data-op='run.launch']", "launch", git: true);
            Assert.Contains("its record will show the recipe was not committed, so it does not count as a pre-registered run", launch);
            Assert.DoesNotContain("could not read whether", launch);
            CancelDialog(b);
            w.MakeUntracked(E2eWorld.UntrackedRecipe);
            Open(E2eWorld.UntrackedRecipe);
            Assert.Equal("untracked", Plate());
            launch = OpenDialog(b, "[data-op='run.launch']", "launch", git: true);
            Assert.Contains("its record will show the recipe was not committed, so it does not count as a pre-registered run", launch);
            CancelDialog(b);

            // a state the room does not know (outside any repository, unreadable): the note says the room could not read it and that the nursery reads the tree itself, never that the recipe was not committed (fix 1)
            foreach (string mode in new[] { "outside", "unreadable" })
            {
                w.GitMode = mode;
                Open(E2eWorld.Committed);
                Assert.Equal(mode, Plate());
                launch = OpenDialog(b, "[data-op='run.launch']", "launch", git: true);
                Assert.True(launch.Contains("The room could not read whether the recipe is committed") && launch.Contains("counts as a pre-registered run only if the tree is clean then") && !launch.Contains("the recipe was not committed"),
                    $"for the git state {mode} the launch note does not say the room could not read the state: {launch}");
                CancelDialog(b);
            }
            w.GitMode = "real";

            // C13: a trial (an untracked recipe under the trials folder): LAUNCH is disabled, LAUNCH AS TRIAL says a trial is never a pre-registration, for each state, and never mentions "was not committed"
            w.MakeChanged(E2eWorld.TrialRecipe);
            Open(E2eWorld.TrialRecipe);
            Assert.True(b.EvalBool("(() => { const l = [...document.querySelectorAll('#launch button')].find(x => x.textContent.trim() === 'LAUNCH'); return !!l && l.disabled; })()"), "LAUNCH is not disabled for a trial recipe");
            foreach (string mode in new[] { "real", "outside", "unreadable" })
            {
                w.GitMode = mode;
                Open(E2eWorld.TrialRecipe);
                string trial = OpenDialog(b, "[data-op='run.launch'][data-trial]", "launch", git: true);
                Assert.Contains("A trial is never a pre-registration, so it launches whatever the state.", trial);
                Assert.DoesNotContain("This launch goes ahead", trial);
                Assert.Equal("LAUNCH AS TRIAL", b.EvalString("document.querySelector('form.ask button[type=submit]').textContent"));
                if (mode == "real") AssertReasonNote(b, "launch as trial", trial);
                CancelDialog(b);
            }
            w.GitMode = "real";

            // a recipe whose world this build cannot load: the plate, and a launch that is refused (both buttons disabled, with the reason)
            Open(E2eWorld.Unloadable);
            Assert.Equal("WORLD NOT LOADED", b.EvalString("document.getElementById('world-plate').innerText.trim()"));
            JsonElement refused = b.Eval("(() => { const q = t => [...document.querySelectorAll('#launch button')].find(x => x.textContent.trim() === t); return { launch: q('LAUNCH').disabled, trial: q('LAUNCH AS TRIAL').disabled, title: q('LAUNCH').title, edit: document.querySelector('.author .btn').disabled, said: document.body.innerText.includes('NOT LAUNCHED') }; })()");
            Assert.True(refused.GetProperty("launch").GetBoolean() && refused.GetProperty("trial").GetBoolean() && refused.GetProperty("edit").GetBoolean() && refused.GetProperty("said").GetBoolean() && refused.GetProperty("title").GetString().Contains("WORLD NOT LOADED"), "a recipe whose world does not load is not refused: " + refused.GetRawText());
            b.Eval("[...document.querySelectorAll('#launch button')].find(x => x.textContent.trim() === 'LAUNCH').click()");
            Thread.Sleep(300);
            Assert.False(b.EvalBool("!!document.querySelector('form.ask')"), "a refused LAUNCH opened a dialog");

            // C14 / fix 5: the RECIPE tab follows the selected row, also when the browser holds a recipe from an earlier visit
            b.Navigate(host.Base + "/");
            b.Eval("localStorage.setItem('nursery-room.selection', JSON.stringify({ run: '3', recipe: '" + E2eWorld.Unloadable + "' }))");
            b.Navigate(host.Base + "/");
            b.WaitFor("document.querySelector('#list tr.sel') && document.querySelector('#list tr.sel').dataset.key === '3|entry'", "entry 3 selected");
            Assert.Equal("recipe.html?path=rounds%2Fnursery%2Fe2e-beta.json", b.EvalString("document.getElementById('tab-recipe').getAttribute('href')"));
            b.Press("ArrowDown");
            b.WaitFor("document.querySelector('#list tr.sel').dataset.key === '2|entry'", "entry 2 selected");
            Assert.Equal("recipe.html?path=rounds%2Fnursery%2Fbrain2-w11.json", b.EvalString("document.getElementById('tab-recipe').getAttribute('href')"));
            b.Click("#tab-recipe");
            b.WaitFor("location.pathname.endsWith('recipe.html') && document.querySelector('#author h1') && document.querySelector('#author h1').innerText === 'brain2-w11'", "the selected entry's recipe opened by the tab");
            // off the registry the tab keeps the last recipe viewed, and with none the picker
            b.Navigate(host.Base + "/run.html?id=3");
            b.WaitFor("document.getElementById('name') && document.getElementById('name').innerText.includes('e2e-beta')", "the run screen");
            Assert.Equal("recipe.html?path=rounds%2Fnursery%2Fbrain2-w11.json", b.EvalString("document.getElementById('tab-recipe').getAttribute('href')"));
            b.Eval("localStorage.removeItem('nursery-room.selection')");
            b.Navigate(host.Base + "/run.html?id=3");
            b.WaitFor("document.getElementById('name') && document.getElementById('name').innerText.includes('e2e-beta')", "the run screen again");
            b.Eval("localStorage.removeItem('nursery-room.selection')");
            b.Navigate(host.Base + "/recipe.html");
            b.WaitFor("document.querySelector('.pick')", "the recipe picker");
            Assert.Contains("Which recipe?", b.EvalString("document.querySelector('.pick').innerText"));

            AssertClean(b, "the recipe flow");
            AssertNothingWritten(w, b);
        }

        // ------------------------------------------------------------------ 4. the tank screen

        [EdgeFact]
        public void TankScreenRefusesAnAddressItHasNotInItsOwnWordsAndStepIsRefusedByTheCapOrOpensAndCancels()
        {
            using var w = new E2eWorld();
            using RoomHost host = w.Host();
            using var b = new EdgeBrowser(w.Fx.Dir, 1920, 1080);

            void Open(string query)
            {
                b.Navigate(host.Base + "/tank.html?" + query);
                b.WaitFor("document.querySelectorAll('#s-brain option').length === 18 && document.getElementById('step')", "the tank screen's pickers");
            }
            string Note() => b.EvalString("(() => { const n = document.getElementById('addr-note'); return n && !n.hidden ? n.innerText.replace(/\\s+/g, ' ') : ''; })()");

            // C20: an address the run does not have is refused in the tank page's words, and what is shown instead is said
            Open("run=1&brain=0&draw=0&tank=0");
            Assert.Equal("", Note());
            Open("run=1&brain=99&draw=0&tank=0");
            b.WaitFor("document.getElementById('addr-note') && !document.getElementById('addr-note').hidden", "the address note");
            Assert.Contains("the address asked for brain 99, but this run has 18 brains (0 to 17), so brain 0 is shown", Note());
            Open("run=1&brain=0&draw=40&tank=0");
            b.WaitFor("document.getElementById('addr-note') && !document.getElementById('addr-note').hidden", "the address note for the draw");
            Assert.Contains("the address asked for draw 40, but this run has 32 draws (0 to 31), so draw 0 is shown", Note());
            Open("run=1&brain=0&draw=0&tank=3");
            b.WaitFor("document.getElementById('addr-note') && !document.getElementById('addr-note').hidden", "the address note for the tank");
            Assert.Contains("the address asked for tank 3, but this run has 1 tank (0 to 0), so tank 0 is shown", Note());

            // C20: with the room's slot free and the machine cool, STEP is enabled, and its dialog opens, carries the reason box's note, and cancels
            w.Fx.Heat.Value = new FixedHeat().Value;
            Open("run=1&brain=0&draw=0&tank=0");
            b.WaitFor("!document.getElementById('step').disabled", "STEP enabled");
            Assert.Equal("", b.EvalString("document.getElementById('stepwhy').textContent"));
            string step = OpenDialog(b, "#step", "step");
            Assert.Contains("step: run 1, brain 0, draw 0, tank 0", step, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Re-steps this exam episode", step);
            AssertReasonNote(b, "step", step);
            CancelDialog(b);
            Assert.False(b.EvalBool("document.getElementById('step').disabled"), "STEP is disabled after its dialog is cancelled");

            // C19: with the heat check reading HOT STEP is refused, with the room's own reason in its title and beside it
            w.Fx.Heat.Value = HeatReading.Of(HeatReading.Hot, "2026-10-11 12:00:00  HOT  gpu 86C [GPU 86 C]");
            Open("run=1&brain=0&draw=0&tank=0");
            b.WaitFor("document.getElementById('step').disabled", "STEP disabled by the cap");
            string reason = b.EvalString("fetch('/api/room').then(r => r.json()).then(j => j.stepRefusal && j.stepRefusal.reason)");
            Assert.False(string.IsNullOrEmpty(reason), "the room gives no reason for refusing a step on a hot machine");
            Assert.Equal(reason, b.EvalString("document.getElementById('step').title"));
            Assert.Equal("not now: " + reason, b.EvalString("document.getElementById('stepwhy').textContent"));
            Assert.Equal("true", b.EvalString("document.getElementById('step').getAttribute('aria-disabled')"));
            w.Fx.Heat.Value = new FixedHeat().Value;

            AssertClean(b, "the tank flow");
            AssertNothingWritten(w, b);
        }

        // ------------------------------------------------------------------ 5. the genome screen

        [EdgeFact]
        public void GenomeScreenLoadsABodyAndABrainAndThePickerChangesTheGenomeAndTheUrl()
        {
            using var w = new E2eWorld();
            using RoomHost host = w.Host();
            using var b = new EdgeBrowser(w.Fx.Dir, 1920, 1080);

            b.Navigate(host.Base + "/genome.html?run=2&id=best.g0");
            b.WaitFor("document.getElementById('graph') && document.getElementById('graph').childElementCount > 0 && document.getElementById('v-top').childElementCount > 0", "the genome's body and brain drawn");
            Assert.Equal("Nursery room · genome best.g0 of 2", b.EvalString("document.title"));
            // the body: three parts, drawn from above and from the side; the brain: its graph, one link
            string text = b.EvalString("document.querySelector('main').innerText.replace(/\\s+/g, ' ')");
            Assert.Matches(@"PARTS 3 JOINTS 2", text);
            Assert.Matches(@"HIDDEN UNITS 0 LINKS 1", text);
            Assert.True(b.EvalNumber("document.getElementById('v-top').childElementCount") > 0 && b.EvalNumber("document.getElementById('v-side').childElementCount") > 0, "the body is not drawn");
            Assert.True(b.EvalNumber("document.querySelectorAll('#graph *').length") > 5, "the brain graph is not drawn");
            Assert.Equal(14, (int)b.EvalNumber("document.querySelectorAll('#pick option').length"));

            // C22: the picker changes the genome, the address and the title
            b.Eval("(() => { const s = document.getElementById('pick'); s.value = 'exam.1'; s.dispatchEvent(new Event('change', { bubbles: true })); })()");
            b.WaitFor("location.search.includes('id=exam.1') && document.title === 'Nursery room · genome exam.1 of 2'", "the genome changed by the picker");
            b.WaitFor("document.getElementById('graph').childElementCount > 0", "the second genome drawn");
            Assert.Equal("exam.1", b.EvalString("document.getElementById('pick').value"));
            Assert.Contains("?run=2&id=exam.1", b.EvalString("location.search ? location.pathname + location.search : ''"));
            // the tabs carry it
            Assert.Equal("genome.html?run=2&id=exam.1", b.EvalString("document.getElementById('tab-genome').getAttribute('href')"));

            AssertAudit(b, "the genome screen");
            AssertClean(b, "the genome flow");
            AssertNothingWritten(w, b);
        }

        // ------------------------------------------------------------------ 6. every screen at both sizes

        [EdgeFact]
        public void EveryScreenAtBothSizesHasNoConsoleErrorNoExceptionNoFailedRequestNoPageWiderThanTheWindowNoTextOutOfItsBoxAndNoNaN()
        {
            using var w = new E2eWorld();
            using RoomHost host = w.Host();
            using var b = new EdgeBrowser(w.Fx.Dir, 1920, 1080);

            var screens = new (string Name, string Url, string Ready)[]
            {
                ("registry", "/", "document.querySelectorAll('#list tr[data-key]').length === 6 && document.querySelector('#sel h2')"),
                ("recipe", "/recipe.html?path=rounds/nursery/brain2-w11.json", "document.getElementById('git-plate') && document.querySelectorAll('.row').length > 20"),
                ("run", "/run.html?id=2&vs=4", "document.querySelector('.exam-t tbody.vs') && document.querySelector('#c-search').childElementCount > 0 && document.querySelector('#c-bodynet')"),
                ("tank", "/tank.html?run=1&brain=0&draw=0&tank=0", "document.querySelectorAll('#s-brain option').length === 18"),
                ("genome", "/genome.html?run=2&id=best.g0", "document.getElementById('graph') && document.getElementById('graph').childElementCount > 0"),
            };
            foreach ((int width, int height) in Sizes)
            {
                b.SetSize(width, height);
                foreach ((string name, string url, string ready) in screens)
                {
                    string where = $"the {name} screen at {width}x{height}";
                    b.ClearProblems();
                    b.Navigate(host.Base + url);
                    b.WaitFor(ready, where + " drawn");
                    b.Settle();
                    Thread.Sleep(400);
                    AssertClean(b, where);
                    AssertAudit(b, where);
                    // the tank screen scrolls at 1366 x 768, which the owner accepted: only its width is held to the window
                    if (name == "run") AssertRunLayout(b, where);
                }
            }
            AssertNothingWritten(w, b);
        }
    }
}
