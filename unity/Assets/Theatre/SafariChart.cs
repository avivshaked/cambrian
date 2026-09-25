using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>What a story's chart draws.</summary>
    public enum SafariChartKind
    {
        /// <summary>Series of points, x ascending: the highlighted one in the subject's clade colour, the rest muted.</summary>
        Line,

        /// <summary>Labelled bars with their values.</summary>
        Bars,

        /// <summary>The followed body's own reserve in joules, read from the live world every frame, with its births marked.</summary>
        Account,
    }

    /// <summary>Where a story's chart stands in the frame.</summary>
    public enum SafariChartPlace
    {
        /// <summary>A card at the right of the frame, above the captions' band.</summary>
        Corner,

        /// <summary>A large card over the whole picture dimmed.</summary>
        Full,
    }

    /// <summary>
    /// A chart a story's scene asks for (the owner, 2026-09-25: "it would be great to be able to
    /// show some stats when introducing a creature, or introducing a concept"): read from the
    /// scene's <c>chart</c> object by <see cref="Read"/>, drawn by <see cref="SafariChartLayer"/>
    /// into the frame the safari writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The form, agreed with the story's writer:</b>
    /// <c>{"kind": "line" | "bars" | "account", "title", "place": "corner" | "full", "at", "until",
    /// "x": {"label"}, "y": {"label"}, "series": [{"name", "points": [[x, y], ...], "highlight"}],
    /// "bars": [{"label", "value"}], "marks": [{"x", "label"}]}</c>. <c>at</c> and <c>until</c> are
    /// seconds into the scene, as a caption's <c>at</c> is; a scene that opens a chapter moves
    /// both by the chapter card's length, as it moves its captions.
    /// </para>
    /// <para>
    /// <b>Read the way the story is read</b>: a field it does not know is noted and passed over, a
    /// value it cannot use is noted and dropped, and nothing here throws. A chart that cannot be
    /// drawn at all (no kind, a line with no points) is noted and the scene plays without it.
    /// </para>
    /// </remarks>
    public sealed class SafariChart
    {
        public sealed class Series
        {
            public string Name;
            public bool Highlight;
            public readonly List<(double x, double y)> Points = new List<(double, double)>();
        }

        public sealed class Bar
        {
            public string Label;
            public double Value;
            /// <summary>Drawn in the clade's colour; when no bar says so, every bar is.</summary>
            public bool Highlight;
        }

        public sealed class Mark
        {
            public double X;
            public string Label;
        }

        public SafariChartKind Kind;
        public SafariChartPlace Place = SafariChartPlace.Corner;
        public string Title;
        public string XLabel;
        public string YLabel;

        /// <summary>Seconds into the scene the chart fades in at.</summary>
        public double At;

        /// <summary>
        /// Seconds into the scene the chart fades out at; NaN until the story's reader resolves it
        /// to the scene's end.
        /// </summary>
        public double Until = double.NaN;

        public readonly List<Series> Lines = new List<Series>();
        public readonly List<Bar> Bars = new List<Bar>();
        public readonly List<Mark> Marks = new List<Mark>();

        /// <summary>One line for the log and the scene list.</summary>
        public string Line() => string.Format(CultureInfo.InvariantCulture,
            "{0} {1} '{2}' from {3:0.#} to {4} s{5}{6}{7}",
            Kind.ToString().ToLowerInvariant(), Place.ToString().ToLowerInvariant(), Title ?? "",
            At, double.IsNaN(Until) ? "the scene's end" : Until.ToString("0.#", CultureInfo.InvariantCulture),
            Lines.Count > 0 ? ", " + Lines.Count + " series (" + Lines.Sum(s => s.Points.Count) + " points" +
                              (Lines.Any(s => s.Highlight) ? ", '" + Lines.First(s => s.Highlight).Name + "' highlighted" : "") + ")" : "",
            Bars.Count > 0 ? ", " + Bars.Count + " bars" : "",
            Marks.Count > 0 ? ", " + Marks.Count + " mark(s)" : "");

        /// <summary>A copy with <see cref="At"/> and <see cref="Until"/> moved later by some seconds.</summary>
        public SafariChart Shifted(double by)
        {
            var c = (SafariChart)MemberwiseClone();
            c.At = At + by;
            c.Until = Until + by;
            return c;
        }

        // ---------------------------------------------------------------- reading

        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            "kind", "type", "title", "place", "placement", "at", "from", "start", "until", "to", "end",
            "x", "y", "x_label", "y_label", "xLabel", "yLabel", "series", "lines", "bars", "marks", "note", "notes",
            "why", "sources", "source",
        };

        /// <summary>
        /// A scene's chart, or null when it has none or none can be drawn from what it gives; every
        /// guess and every refusal is one line of <paramref name="notes"/>. Never throws.
        /// </summary>
        public static SafariChart Read(JsonNode node, int scene, List<string> notes)
        {
            if (node == null || node.Kind == JsonNode.NodeKind.Null) return null;
            string who = "scene " + scene.ToString(CultureInfo.InvariantCulture) + ": its chart";

            try
            {
                if (node.Kind != JsonNode.NodeKind.Object)
                {
                    notes.Add(who + " is not an object, and is not drawn");
                    return null;
                }

                var chart = new SafariChart();

                string kind = (Text(First(node, "kind", "type")) ?? "").Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "line": case "lines": case "series": case "timeline": chart.Kind = SafariChartKind.Line; break;
                    case "bars": case "bar": case "barchart": case "bar chart": chart.Kind = SafariChartKind.Bars; break;
                    case "account": case "reserve": case "energy": case "ledger": chart.Kind = SafariChartKind.Account; break;
                    default:
                        notes.Add(who + ": the kind '" + kind + "' is none of line, bars or account, and the chart is not drawn");
                        return null;
                }

                string place = (Text(First(node, "place", "placement")) ?? "corner").Trim().ToLowerInvariant();
                switch (place)
                {
                    case "corner": case "lower-right": case "lower right": case "overlay": chart.Place = SafariChartPlace.Corner; break;
                    case "full": case "card": case "fullscreen": case "full screen": case "full-screen": chart.Place = SafariChartPlace.Full; break;
                    default:
                        notes.Add(who + ": the place '" + place + "' is neither corner nor full: drawn in the corner");
                        chart.Place = SafariChartPlace.Corner;
                        break;
                }

                chart.Title = Text(First(node, "title"));
                chart.XLabel = AxisLabel(node, "x", "x_label", "xLabel");
                chart.YLabel = AxisLabel(node, "y", "y_label", "yLabel");

                if (Number(First(node, "at", "from", "start"), out double at)) chart.At = Math.Max(0d, at);
                if (Number(First(node, "until", "to", "end"), out double until)) chart.Until = until;
                if (!double.IsNaN(chart.Until) && chart.Until <= chart.At)
                {
                    notes.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0}: 'until' {1:0.#} s is not after 'at' {2:0.#} s: it is shown to the scene's end", who, chart.Until, chart.At));
                    chart.Until = double.NaN;
                }

                JsonNode series = First(node, "series", "lines");
                if (series != null && series.Kind == JsonNode.NodeKind.Array)
                {
                    int i = 0;
                    foreach (JsonNode s in series.Items())
                    {
                        i++;
                        Series read = ReadSeries(s, who + ", series " + i.ToString(CultureInfo.InvariantCulture), notes);
                        if (read != null) chart.Lines.Add(read);
                    }
                }
                else if (series != null)
                {
                    notes.Add(who + ": 'series' is not a list, and is not read");
                }

                JsonNode bars = First(node, "bars");
                if (bars != null && bars.Kind == JsonNode.NodeKind.Array)
                {
                    int i = 0;
                    foreach (JsonNode b in bars.Items())
                    {
                        i++;
                        if (b.Kind != JsonNode.NodeKind.Object || !Number(First(b, "value", "v", "y"), out double v))
                        {
                            notes.Add(who + ", bar " + i.ToString(CultureInfo.InvariantCulture) + " has no numeric 'value', and is not drawn");
                            continue;
                        }
                        if (v < 0d)
                        {
                            notes.Add(string.Format(CultureInfo.InvariantCulture, "{0}, bar {1} is negative ({2}): drawn at zero with its value written",
                                who, i, v));
                        }
                        chart.Bars.Add(new Bar
                        {
                            Label = Text(First(b, "label", "name", "text")) ?? "",
                            Value = v,
                            Highlight = First(b, "highlight") is JsonNode h && h.Kind == JsonNode.NodeKind.Bool && h.AsBool(),
                        });
                    }
                }
                else if (bars != null)
                {
                    notes.Add(who + ": 'bars' is not a list, and is not read");
                }

                JsonNode marks = First(node, "marks");
                if (marks != null && marks.Kind == JsonNode.NodeKind.Array)
                {
                    int i = 0;
                    foreach (JsonNode m in marks.Items())
                    {
                        i++;
                        if (m.Kind == JsonNode.NodeKind.Object && Number(First(m, "x", "at", "t"), out double x))
                            chart.Marks.Add(new Mark { X = x, Label = Text(First(m, "label", "text", "name")) ?? "" });
                        else if (m.Kind == JsonNode.NodeKind.Number)
                            chart.Marks.Add(new Mark { X = m.AsDouble(), Label = "" });
                        else
                            notes.Add(who + ", mark " + i.ToString(CultureInfo.InvariantCulture) + " has no numeric 'x', and is not drawn");
                    }
                }

                string[] unread = node.Keys().Where(k => !Known.Contains(k)).ToArray();
                if (unread.Length > 0) notes.Add(who + ": keys not read: " + string.Join(", ", unread));

                switch (chart.Kind)
                {
                    case SafariChartKind.Line:
                        if (chart.Lines.Count == 0)
                        {
                            notes.Add(who + " is a line with no series of two or more points, and is not drawn");
                            return null;
                        }
                        if (chart.Lines.Count(s => s.Highlight) > 1)
                            notes.Add(who + ": more than one series is highlighted; each is drawn in the clade's colour");
                        if (chart.Bars.Count > 0) notes.Add(who + " is a line: its bars are not drawn");
                        break;

                    case SafariChartKind.Bars:
                        if (chart.Bars.Count == 0)
                        {
                            notes.Add(who + " is bars with no bar that has a value, and is not drawn");
                            return null;
                        }
                        if (chart.Lines.Count > 0) notes.Add(who + " is bars: its series are not drawn");
                        if (chart.Marks.Count > 0) notes.Add(who + " is bars: its marks are not drawn");
                        break;

                    case SafariChartKind.Account:
                        if (chart.Lines.Count > 0 || chart.Bars.Count > 0)
                            notes.Add(who + " is an account, read from the live world: its series and bars are not drawn");
                        if (chart.Marks.Count > 0)
                            notes.Add(who + " is an account: its marks are not drawn (the body's own births are marked where they happen)");
                        break;
                }

                return chart;
            }
            catch (Exception e)
            {
                notes.Add(who + " could not be read and is not drawn: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        private static Series ReadSeries(JsonNode node, string who, List<string> notes)
        {
            if (node.Kind != JsonNode.NodeKind.Object)
            {
                notes.Add(who + " is not an object, and is not drawn");
                return null;
            }

            var series = new Series
            {
                Name = Text(First(node, "name", "label")) ?? "",
                Highlight = First(node, "highlight", "highlighted") is JsonNode h && h.Kind == JsonNode.NodeKind.Bool && h.AsBool(),
            };

            JsonNode points = First(node, "points", "data", "values");
            if (points == null || points.Kind != JsonNode.NodeKind.Array)
            {
                notes.Add(who + " ('" + series.Name + "') has no 'points' list, and is not drawn");
                return null;
            }

            int dropped = 0;
            foreach (JsonNode p in points.Items())
            {
                double x = double.NaN, y = double.NaN;
                if (p.Kind == JsonNode.NodeKind.Array && p.Count >= 2)
                {
                    Number(p[0], out x);
                    Number(p[1], out y);
                }
                else if (p.Kind == JsonNode.NodeKind.Object)
                {
                    Number(First(p, "x", "t"), out x);
                    Number(First(p, "y", "v", "value"), out y);
                }

                if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y)) { dropped++; continue; }
                series.Points.Add((x, y));
            }

            if (dropped > 0) notes.Add(who + " ('" + series.Name + "'): " + dropped + " point(s) not a pair of numbers, dropped");

            bool ascending = true;
            for (int i = 1; i < series.Points.Count; i++) if (series.Points[i].x < series.Points[i - 1].x) { ascending = false; break; }
            if (!ascending)
            {
                notes.Add(who + " ('" + series.Name + "'): x is not ascending, so the points are sorted by x");
                series.Points.Sort((a, b) => a.x.CompareTo(b.x));
            }

            if (series.Points.Count < 2)
            {
                notes.Add(who + " ('" + series.Name + "') has fewer than two points, and is not drawn");
                return null;
            }

            return series;
        }

        /// <summary>An axis's label from <c>{"x": {"label": ...}}</c>, <c>{"x": "..."}</c> or <c>{"x_label": ...}</c>.</summary>
        private static string AxisLabel(JsonNode node, string axis, params string[] flat)
        {
            JsonNode a = First(node, axis);
            if (a != null && a.Kind == JsonNode.NodeKind.Object) return Text(First(a, "label", "name", "title"));
            if (a != null && a.Kind == JsonNode.NodeKind.String) return Text(a);
            return Text(First(node, flat));
        }

        private static bool Number(JsonNode n, out double value)
        {
            value = double.NaN;
            if (n == null) return false;
            if (n.Kind == JsonNode.NodeKind.Number) { value = n.AsDouble(); return !double.IsNaN(value); }
            if (n.Kind == JsonNode.NodeKind.String &&
                double.TryParse(n.AsString().Replace(",", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            value = double.NaN;
            return false;
        }

        private static string Text(JsonNode n)
        {
            if (n == null) return null;
            if (n.Kind == JsonNode.NodeKind.String) return string.IsNullOrWhiteSpace(n.AsString()) ? null : n.AsString().Trim();
            if (n.Kind == JsonNode.NodeKind.Number) return n.AsDouble().ToString("R", CultureInfo.InvariantCulture);
            return null;
        }

        private static JsonNode First(JsonNode host, params string[] keys)
        {
            if (host == null || host.Kind != JsonNode.NodeKind.Object) return null;
            foreach (string k in keys)
                if (host.Has(k) && host[k].Kind != JsonNode.NodeKind.Null) return host[k];
            return null;
        }
    }
}
