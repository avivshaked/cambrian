using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Evosim.Theatre
{
    /// <summary>
    /// The safari's panel (safari-spec.md item 11): the heuristic, the scene list with the current
    /// scene marked, next and previous, the picker, auto and record; and, outside the panel, the
    /// caption line and the provenance word for a frame with the interface hidden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Under the interface's rules</b> (design/SPEC.md, theatre-ui-spec.md). Built in C# into
    /// the interface's own document, with its values in <c>Resources/SafariStyles.uss</c> read
    /// against the interface's tokens: no hue anywhere (the record button inverts to the plate
    /// when it is on, it never turns red), the type and the furniture take the 2240 and 3400
    /// density steps through the same <c>.is-wide</c> and <c>.is-wider</c> classes on the root,
    /// and at <c>.is-wider</c> every font size is written literally, the rule the stylesheet
    /// learnt on 2026-09-13. The panel sits bottom right, above the bar, clear of the centre.
    /// </para>
    /// <para>
    /// <b>It lives under the interface's root, so <c>H</c> hides it with the rest.</b> The caption
    /// and the provenance word do not: they are part of the picture, not of the interface, and
    /// sit on the document's own root, shown only while a scene plays.
    /// </para>
    /// </remarks>
    public sealed class SafariPanel
    {
        public const string StylesResource = "SafariStyles";

        private readonly VisualElement _panel;
        private readonly Label _title;
        private readonly Label _status;
        private readonly ScrollView _list;
        private readonly Button _heuristic, _previous, _next, _auto, _record, _interfaceInRecord;
        private readonly DropdownField _picker;
        private readonly VisualElement _overlay;
        private readonly Label _caption;
        private readonly Label _provenance;
        private readonly SafariSparkline _sparkline;
        private readonly List<Label> _rows = new List<Label>();
        private int _marked = -1;

        public event Action HeuristicClicked, PreviousClicked, NextClicked, AutoClicked, RecordClicked, InterfaceInRecordClicked;
        public event Action<int> SceneClicked;
        public event Action<string> Picked;

        private SafariPanel(VisualElement host, VisualElement documentRoot, StyleSheet styles)
        {
            if (styles != null && !documentRoot.styleSheets.Contains(styles)) documentRoot.styleSheets.Add(styles);

            _panel = new VisualElement { name = "safari" };
            _panel.AddToClassList("panel");
            _panel.AddToClassList("panel--safari");
            _panel.pickingMode = PickingMode.Position;

            _title = new Label("SAFARI");
            _title.AddToClassList("section__title");
            _panel.Add(_title);

            VisualElement tripRow = Row();
            _heuristic = MakeButton("top 10", () => HeuristicClicked?.Invoke(), tripRow, true);
            _picker = new DropdownField { name = "safari-picker" };
            _picker.AddToClassList("safari-picker");
            _picker.RegisterValueChangedCallback(e => { if (!string.IsNullOrEmpty(e.newValue)) Picked?.Invoke(e.newValue); });
            tripRow.Add(_picker);
            _panel.Add(tripRow);

            _list = new ScrollView(ScrollViewMode.Vertical) { name = "safari-scenes" };
            _list.AddToClassList("safari-scenes");
            _panel.Add(_list);

            VisualElement controls = Row();
            _previous = MakeButton("prev ,", () => PreviousClicked?.Invoke(), controls, true);
            _next = MakeButton("next .", () => NextClicked?.Invoke(), controls, false);
            _auto = MakeButton("auto", () => AutoClicked?.Invoke(), controls, false);
            _record = MakeButton("record F9", () => RecordClicked?.Invoke(), controls, false);
            _interfaceInRecord = MakeButton("ui off", () => InterfaceInRecordClicked?.Invoke(), controls, false);
            _panel.Add(controls);

            _status = new Label("");
            _status.AddToClassList("safari-status");
            _panel.Add(_status);

            host.Add(_panel);

            // The caption and the corner word sit in a layer of their own on the document's root,
            // so H does not take them; the layer carries the interface's density classes, copied
            // from the interface's root by SyncWidth, so the two steps reach them too.
            _overlay = new VisualElement { name = "safari-overlay", pickingMode = PickingMode.Ignore };
            _overlay.AddToClassList("safari-overlay");
            documentRoot.Add(_overlay);

            _caption = new Label("") { name = "safari-caption", pickingMode = PickingMode.Ignore };
            _caption.AddToClassList("safari-caption");
            _caption.AddToClassList("is-gone");
            _overlay.Add(_caption);

            _provenance = new Label("COUSIN") { name = "safari-provenance", pickingMode = PickingMode.Ignore };
            _provenance.AddToClassList("safari-provenance");
            _provenance.AddToClassList("is-gone");
            _overlay.Add(_provenance);

            _sparkline = new SafariSparkline();
            _overlay.Add(_sparkline);
        }

        /// <summary>Builds the panel into the interface, or returns null when there is no interface to build it in.</summary>
        public static SafariPanel Create(TheatreUi ui)
        {
            if (ui?.Root == null || ui.Document == null) return null;
            var styles = Resources.Load<StyleSheet>(StylesResource);
            if (styles == null) Debug.LogWarning("[Theatre] safari: Resources/" + StylesResource + ".uss is missing; the panel draws unstyled.");
            return new SafariPanel(ui.Root, ui.Document.rootVisualElement, styles);
        }

        private static VisualElement Row()
        {
            var row = new VisualElement { pickingMode = PickingMode.Position };
            row.AddToClassList("safari-row");
            return row;
        }

        private static Button MakeButton(string text, Action click, VisualElement into, bool lead)
        {
            var b = new Button(click) { text = text };
            b.AddToClassList("safari-button");
            if (lead) b.AddToClassList("lead");
            into.Add(b);
            return b;
        }

        // ---------------------------------------------------------------- state

        public void SetHeuristic(string word) => _heuristic.text = word;

        public void SetPicker(IList<string> names)
        {
            _picker.choices = new List<string>(names);
            _picker.SetValueWithoutNotify("");
        }

        public void SetScenes(IReadOnlyList<SafariScene> scenes)
        {
            _list.Clear();
            _rows.Clear();
            _marked = -1;

            for (int i = 0; i < scenes.Count; i++)
            {
                int index = i;
                var row = new Label("  " + scenes[i].Line().Trim());
                row.AddToClassList("safari-scene");
                row.RegisterCallback<ClickEvent>(_ => SceneClicked?.Invoke(index));
                _list.Add(row);
                _rows.Add(row);
            }
        }

        /// <summary>Marks the current scene with an arrow and the high ink; the rest stay low.</summary>
        public void Mark(int index, IReadOnlyList<SafariScene> scenes)
        {
            if (index == _marked) return;

            if (_marked >= 0 && _marked < _rows.Count)
            {
                _rows[_marked].RemoveFromClassList("safari-scene--current");
                _rows[_marked].text = "  " + scenes[_marked].Line().Trim();
            }

            _marked = index;
            if (index >= 0 && index < _rows.Count)
            {
                _rows[index].AddToClassList("safari-scene--current");
                _rows[index].text = "→ " + scenes[index].Line().Trim();
                _list.ScrollTo(_rows[index]);
            }
        }

        public void SetStatus(string text)
        {
            if (_status.text != text) _status.text = text;
        }

        public void SetAuto(bool on) => _auto.EnableInClassList("safari-button--on", on);

        public void SetRecording(bool on)
        {
            _record.EnableInClassList("safari-button--on", on);
            _record.text = on ? "stop F9" : "record F9";
        }

        public void SetInterfaceInRecord(bool shown) => _interfaceInRecord.text = shown ? "ui on" : "ui off";

        /// <summary>The caption line, or nothing.</summary>
        public void SetCaption(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            _caption.EnableInClassList("is-gone", !show);
            if (show && _caption.text != text) _caption.text = text;
        }

        /// <summary>The corner word, shown when the interface is hidden and a scene plays.</summary>
        public void SetProvenance(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            _provenance.EnableInClassList("is-gone", !show);
            if (show && _provenance.text != text) _provenance.text = text;
        }

        /// <summary>The call-outs' sparkline for a clade at a second, or nothing for null.</summary>
        public void SetSparkline(SafariClade clade, double second) => _sparkline?.Set(clade, second);

        /// <summary>Copies the interface root's density step onto the caption layer.</summary>
        public void SyncWidth(VisualElement interfaceRoot)
        {
            if (interfaceRoot == null) return;
            _overlay.EnableInClassList("is-wide", interfaceRoot.ClassListContains("is-wide"));
            _overlay.EnableInClassList("is-wider", interfaceRoot.ClassListContains("is-wider"));
        }

        public void Dispose()
        {
            _panel?.RemoveFromHierarchy();
            _overlay?.RemoveFromHierarchy();
        }
    }

    /// <summary>
    /// The safari's sparkline call-out (<c>EVOSIM_THEATRE_SAFARI_CALLOUTS=1</c>): a clade's count
    /// of living members over its life, the filmed second marked by an upright line, and its
    /// extinction as a cut, the line stopping at a bar down to the baseline.
    /// </summary>
    /// <remarks>
    /// Drawn with the panel's own painter, top right, in the interface's ink and no hue: it is a
    /// reading, not a decoration. It sits on the document's root, beside the caption layer, so it
    /// is composited with the rest of the interface's layer (<see cref="SafariComposite"/> in the
    /// Editor, <see cref="TheatreUiCapture.ArmOver"/> headless) and hidden by nothing but itself.
    /// </remarks>
    public sealed class SafariSparkline : VisualElement
    {
        private readonly List<(double t, int n)> _series = new List<(double, int)>();
        private double _at = double.NaN;
        private double _end = double.NaN;
        private SafariClade _clade;

        public SafariSparkline()
        {
            name = "safari-sparkline";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.right = 40;
            style.top = 40;
            style.width = 420;
            style.height = 110;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        /// <summary>Shows a clade's series with a second marked, or hides the line for null.</summary>
        public void Set(SafariClade clade, double second)
        {
            bool show = clade != null && clade.Series.Count > 1;
            style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) { _clade = null; return; }

            if (!ReferenceEquals(clade, _clade))
            {
                _clade = clade;
                _series.Clear();
                _series.AddRange(clade.Series);
                _end = clade.ExtinctAt;
            }

            if (Math.Abs(second - _at) > 1e-3 || double.IsNaN(_at))
            {
                _at = second;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_series.Count < 2) return;
            Rect r = contentRect;
            if (r.width < 8f || r.height < 8f) return;

            float pad = 10f;
            double t0 = _series[0].t;
            double t1 = _series[_series.Count - 1].t;
            if (!double.IsNaN(_end)) t1 = Math.Max(t1, _end);
            if (!double.IsNaN(_at)) t1 = Math.Max(t1, _at);
            if (t1 <= t0) t1 = t0 + 1d;
            int most = 1;
            foreach (var p in _series) most = Math.Max(most, p.n);

            float X(double t) => r.xMin + pad + (float)((t - t0) / (t1 - t0)) * (r.width - 2f * pad);
            float Y(int n) => r.yMax - pad - (n / (float)most) * (r.height - 2f * pad);

            Painter2D painter = context.painter2D;

            // The baseline, faint.
            painter.strokeColor = new Color(1f, 1f, 1f, 0.25f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(X(t0), Y(0)));
            painter.LineTo(new Vector2(X(t1), Y(0)));
            painter.Stroke();

            // The count, stopping at the extinction.
            painter.strokeColor = new Color(1f, 1f, 1f, 0.85f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            bool started = false;
            foreach (var p in _series)
            {
                if (!double.IsNaN(_end) && p.t > _end + 1e-6) break;
                var v = new Vector2(X(p.t), Y(p.n));
                if (!started) { painter.MoveTo(v); started = true; }
                else painter.LineTo(v);
            }
            painter.Stroke();

            // The extinction as a cut: a short bar across the baseline where the line stops.
            if (!double.IsNaN(_end))
            {
                painter.lineWidth = 3f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(X(_end), Y(0) + 6f));
                painter.LineTo(new Vector2(X(_end), Y(0) - 14f));
                painter.Stroke();
            }

            // The filmed second.
            if (!double.IsNaN(_at))
            {
                painter.strokeColor = new Color(1f, 1f, 1f, 1f);
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(X(_at), r.yMin + pad * 0.5f));
                painter.LineTo(new Vector2(X(_at), r.yMax - pad * 0.5f));
                painter.Stroke();
            }
        }
    }
}
