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
}
