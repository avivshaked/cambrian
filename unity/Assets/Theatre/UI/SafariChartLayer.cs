using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Evosim.Core;
using Object = UnityEngine.Object;

namespace Evosim.Theatre
{
    /// <summary>
    /// A story's charts, drawn with UI Toolkit into a texture of their own and composited into the
    /// frame the safari writes (the owner, 2026-09-25: "can we introduce graphs in unity? it would
    /// be great to be able to show some stats when introducing a creature, or introducing a
    /// concept").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How a chart reaches the frame.</b> The safari's frames are rendered off screen by a
    /// <see cref="SnapshotCamera"/> into a supersampled target, box-filtered on the card and read
    /// back; <c>ScreenCapture</c> writes nothing under <c>-batchmode</c>
    /// (<see cref="TheatreUiCapture"/>'s remarks). So the chart is a runtime panel of its own whose
    /// <see cref="PanelSettings.targetTexture"/> is a texture the size of that target, cleared to
    /// transparent, and the camera hands its target to <see cref="Composite"/> after the render and
    /// before the filter (<see cref="SnapshotCamera.OverRender"/>), which blits the panel's texture
    /// over it through <c>TheatreChartOver.shader</c>. The label and the caption are stamped after
    /// the read-back, so they stay on top; the chart is filtered with the world, so its edges are
    /// smoothed the same way.
    /// </para>
    /// <para>
    /// <b>A frame late, by construction.</b> A panel draws into its texture on the player loop's
    /// own repaint, a tick after its elements change (the reason <see cref="TheatreUiCapture"/> arms
    /// on one tick and shoots on the next). So the texture a frame composites holds the chart as the
    /// previous frame set it: the static kinds are built when their scene's take starts, ticks
    /// before the first frame, and an account's line is one frame (a thirtieth of a second) behind
    /// the body. The fade is the composite's own (<c>_Fade</c>) and has no lag at all.
    /// </para>
    /// <para>
    /// <b>The plate and the dim are the shader's, not the panel's.</b> What UI Toolkit writes into
    /// the alpha channel of a transparent texture depends on its blend state, which this project has
    /// never measured; its colour channels over a transparent black clear are premultiplied either
    /// way. So the panel draws only ink (lines, bars, text), the shader lays the card's plate and a
    /// full chart's darkening under it in closed form, and the ink goes over both premultiplied.
    /// </para>
    /// <para>
    /// <b>The first visible frame of each chart is checked</b>: the card's region of the panel's
    /// texture is read back once and the log says how many of its pixels are inked. An empty card
    /// means the offscreen panel was never repainted in this Editor; the layer then asks UI Toolkit's
    /// runtime to update and repaint its offscreen panels before each composite (by reflection, an
    /// internal method, logged), and says whether that inked it.
    /// </para>
    /// </remarks>
    public sealed class SafariChartLayer : IDisposable
    {
        /// <summary>The layout's own frame, in reference pixels: every size here is at 1920 by 1080 and scales with the picture.</summary>
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>How long a chart takes to fade in or out, s.</summary>
        public const float FadeSeconds = 0.6f;

        public const string StylesResource = "SafariChartStyles";
        private const string ThemeResource = "TheatreTheme";
        private const string CompositeShaderName = "Hidden/Evosim/Theatre Chart Over";

        /// <summary>
        /// The corner card, reference pixels from the top left: at the right, its foot 400 px above
        /// the frame's, clear of the captions' band (a three-line caption's plate reaches 359 px up
        /// the 1080 frame, <see cref="SnapshotCamera"/>'s <c>DrawCaption</c>; the join's two-line
        /// subtitle about 210 px, <c>scripts/story-assemble.py</c>) and of the label at the top left.
        /// </summary>
        public static readonly Rect CornerCard = new Rect(ReferenceWidth - 44f - 640f, 280f, 640f, 400f);

        /// <summary>The full card, reference pixels from the top left, its foot at the same height as the corner's.</summary>
        public static readonly Rect FullCard = new Rect(260f, 120f, 1400f, 560f);

        /// <summary>How much of the world under a card its plate takes away, in linear light.</summary>
        public const float CornerPlateAlpha = 0.78f;
        public const float FullPlateAlpha = 0.6f;

        /// <summary>
        /// How much of the whole picture a full chart takes away before its plate, in linear light:
        /// a third, so the world stays moving and visible round and through the card on any station
        /// (the owner, 2026-09-25: "why can't we do this while showing cool world videos?"). It was
        /// 0.55 when a full chart sat only on a card whose own dim of 0.6 was stamped on top.
        /// </summary>
        public const float FullWorldDim = 0.33f;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int WorldDimId = Shader.PropertyToID("_WorldDim");
        private static readonly int PlateId = Shader.PropertyToID("_Plate");
        private static readonly int PlateAlphaId = Shader.PropertyToID("_PlateAlpha");
        private static readonly int PlateRadiusId = Shader.PropertyToID("_PlateRadius");
        private static readonly int TargetSizeId = Shader.PropertyToID("_TargetSize");

        private GameObject _host;
        private PanelSettings _settings;
        private UIDocument _document;
        private RenderTexture _texture;
        private Material _material;
        private SafariChartCard _card;

        private SafariScene _scene;
        private SafariChart _chart;
        private float _fade;
        private bool _shown;
        private int _visibleFrames;
        private bool _inkChecked;
        private bool _forceRepaint;
        private bool _repaintSaid;

        // the account
        private long _subject = -1;
        private int _children = -1;
        private double _domainStart = double.NaN;
        private bool _gone;
        private bool _saidNoBody;
        private bool _saidNoReserve;
        private readonly List<(double t, double j)> _account = new List<(double, double)>();
        private readonly List<double> _births = new List<double>();

        /// <summary>Where the layer's lines go; the console by default.</summary>
        public Action<string> Say = line => Debug.Log("[Theatre] safari chart: " + line);

        private SafariChartLayer() { }

        /// <summary>The composite, for <see cref="SnapshotCamera.OverRender"/>.</summary>
        public Action<RenderTexture> Composite => CompositeOnto;

        /// <summary>True when this frame's chart is on screen at all.</summary>
        public bool Visible => _chart != null && _fade > 0.001f;

        /// <summary>True when this frame's chart is a full card on screen: the meter holds.</summary>
        public bool FullVisible => Visible && _chart.Place == SafariChartPlace.Full;

        /// <summary>The chart's opacity this frame, 0 to 1.</summary>
        public float Fade => _fade;

        // ---------------------------------------------------------------- making it

        /// <summary>
        /// The layer for a camera's target of this size (<see cref="SnapshotCamera.TargetWidth"/>),
        /// or null with the reason when the shader or the panel cannot be made.
        /// </summary>
        public static SafariChartLayer Create(int width, int height, out string note)
        {
            note = null;

            Shader shader = Shader.Find(CompositeShaderName);
            if (shader == null || !shader.isSupported)
            {
                note = "'" + CompositeShaderName + "' is " + (shader == null ? "not in the project" : "not supported by this device") + ": no charts are drawn";
                return null;
            }

            var layer = new SafariChartLayer();

            try
            {
                width = Mathf.Clamp(width, 64, 8192);
                height = Mathf.Clamp(height, 64, 8192);

                // The camera's own format: an sRGB target in this linear project, so the panel's
                // writes are encoded and the composite reads them back as light.
                layer._texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Theatre Chart Layer",
                    antiAliasing = 1,
                };
                layer._texture.Create();

                layer._material = new Material(shader) { name = "Theatre Chart Over", hideFlags = HideFlags.HideAndDontSave };

                layer._settings = ScriptableObject.CreateInstance<PanelSettings>();
                layer._settings.name = "Theatre Chart Panel";
                layer._settings.scaleMode = PanelScaleMode.ConstantPixelSize;

                // Laid out at 1920 by 1080 whatever the texture's size: the texture is the
                // supersampled target, so the type is drawn at that density and filtered down.
                layer._settings.scale = width / ReferenceWidth;

                var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
                if (theme != null) layer._settings.themeStyleSheet = theme;

                layer._settings.targetTexture = layer._texture;
                layer._settings.clearColor = true;
                layer._settings.colorClearValue = new Color(0f, 0f, 0f, 0f);
                layer._settings.clearDepthStencil = true;

                layer._host = new GameObject("Theatre Chart Layer");
                layer._document = layer._host.AddComponent<UIDocument>();
                layer._document.panelSettings = layer._settings;

                VisualElement root = layer._document.rootVisualElement;
                if (root == null)
                {
                    layer.Dispose();
                    note = "the chart panel's document has no root: no charts are drawn";
                    return null;
                }

                var styles = Resources.Load<StyleSheet>(StylesResource);
                if (styles != null) root.styleSheets.Add(styles);
                else note = "Resources/" + StylesResource + ".uss is missing: the charts are drawn in the theme's default font";

                root.pickingMode = PickingMode.Ignore;

                layer._card = new SafariChartCard();
                root.Add(layer._card);

                string made = string.Format(CultureInfo.InvariantCulture,
                    "a panel of its own at {0}x{1} ({2:0.##}x the 1920 layout), composited before the box filter", width, height, width / ReferenceWidth);
                note = note == null ? made : made + "; " + note;
                return layer;
            }
            catch (Exception e)
            {
                layer.Dispose();
                note = "the chart layer could not be made: " + e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        /// <summary>
        /// The colour a clade's line is drawn in: its guild's, turned by its lineage as its bodies
        /// are painted (<see cref="TheatrePalette.TurnFor"/>), lifted to a lightness that reads on
        /// the dark plate. The interface's high ink for a scene with no clade.
        /// </summary>
        public static Color InkFor(SafariClade clade, TheatrePalette palette)
        {
            if (clade == null) return SafariChartCard.InkHi;

            Color guild = clade.Absorptive
                ? palette != null ? palette.Absorptive : new Color(0.90f, 0.55f, 0.22f)
                : clade.Photosynthetic
                    ? palette != null ? palette.Photosynthetic : new Color(0.34f, 0.72f, 0.36f)
                    : palette != null ? palette.Structural : new Color(0.72f, 0.74f, 0.78f);

            float hue = palette != null ? palette.LineageHue : 0.06f;
            Color turned = TheatrePalette.Turned(guild, TheatrePalette.TurnFor(clade.Founder, hue));
            Color ink = TheatrePalette.Muted(turned, 0.8f, 0.62f);
            ink.a = 1f;
            return ink;
        }

        // ---------------------------------------------------------------- a scene

        /// <summary>
        /// A take is starting: the scene's chart is built now, so the panel has drawn it by the
        /// take's first frame. A scene's next take builds it again, so an account starts afresh
        /// with the world restored rather than carrying the last take's samples.
        /// </summary>
        public void Begin(SafariScene scene, Color ink)
        {
            SafariChart chart = scene?.Chart;

            SayDone();

            _scene = scene;
            _chart = chart;
            _fade = 0f;
            _shown = false;
            _visibleFrames = 0;
            _inkChecked = false;
            ResetAccount();

            if (chart == null)
            {
                _card?.Empty();
                return;
            }

            _card?.Build(chart, ink, chart.Place == SafariChartPlace.Full ? FullCard : CornerCard, chart.Place == SafariChartPlace.Full);
            Say((scene.FromStory ? "story " + scene.StoryNumber.ToString(CultureInfo.InvariantCulture) : scene.Slug) + ": " + chart.Line());
        }

        /// <summary>
        /// One frame: the chart's fade at the frame's second in its scene, and an account's sample
        /// of the followed body. Call it before the capture; true when the chart is on screen.
        /// </summary>
        public bool Frame(SafariPose pose, IFilmWorld live, float interval)
        {
            if (!ReferenceEquals(pose.Scene, _scene)) Begin(pose.Scene, InkFor(pose.Scene?.Clade, null));
            if (_chart == null) { _fade = 0f; return false; }

            double offset = pose.SceneOffset;
            _fade = FadeAt(_chart, offset);

            if (_chart.Kind == SafariChartKind.Account) Account(pose, live, offset, interval);

            if (_fade > 0.001f)
            {
                _shown = true;
                _visibleFrames++;
                if (!_inkChecked) CheckInk();
            }

            return _fade > 0.001f;
        }

        /// <summary>
        /// The pixels a corner chart covers, for the exposure meter to leave out
        /// (<see cref="SnapshotCamera.MeterExclude"/>), in output pixels from the bottom left; empty
        /// when no corner chart shows.
        /// </summary>
        public RectInt MeterExclude(int width, int height)
        {
            if (!Visible || _chart.Place != SafariChartPlace.Corner) return new RectInt(0, 0, 0, 0);

            Rect b = CornerCard;
            float sx = width / ReferenceWidth, sy = height / ReferenceHeight;
            int x0 = Mathf.FloorToInt(b.xMin * sx), x1 = Mathf.CeilToInt(b.xMax * sx);
            int y0 = Mathf.FloorToInt((ReferenceHeight - b.yMax) * sy), y1 = Mathf.CeilToInt((ReferenceHeight - b.yMin) * sy);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>A chart's opacity at a second into its scene: a smooth step in over <see cref="FadeSeconds"/> from its start, and out to its end.</summary>
        public static float FadeAt(SafariChart chart, double offset)
        {
            if (chart == null) return 0f;
            double rise = (offset - chart.At) / FadeSeconds;
            double fall = double.IsNaN(chart.Until) ? 1d : (chart.Until - offset) / FadeSeconds;
            double u = Math.Max(0d, Math.Min(1d, Math.Min(rise, fall)));
            return (float)(u * u * (3d - 2d * u));
        }

        // ---------------------------------------------------------------- the account

        private void ResetAccount()
        {
            _subject = -1;
            _children = -1;
            _domainStart = double.NaN;
            _gone = false;
            _saidNoBody = false;
            _saidNoReserve = false;
            _account.Clear();
            _births.Clear();
        }

        private void Account(SafariPose pose, IFilmWorld live, double offset, float interval)
        {
            if (pose.Subject != _subject)
            {
                if (_subject >= 0 && _account.Count > 0)
                    Say(string.Format(CultureInfo.InvariantCulture, "the account's body changed from {0} to {1} at {2:0.#} s: its line starts again",
                        _subject, pose.Subject, pose.Second));
                ResetAccount();
                _subject = pose.Subject;
            }

            if (_subject < 0)
            {
                if (!_saidNoBody) Say("an account chart on a take that follows no body: it draws its axes and no line");
                _saidNoBody = true;
                return;
            }

            // The run's second the chart's left edge stands at: the scene's offset and the world's
            // second advance together inside a take, so this is fixed until the chart shows.
            double span = double.IsNaN(_chart.Until) ? 20d : Math.Max(1d, _chart.Until - _chart.At);
            if (!_shown || double.IsNaN(_domainStart)) _domainStart = pose.Second - (offset - _chart.At);

            // Nothing is sampled until two frames before the chart opens.
            if (offset < _chart.At - 2d * interval) { _card?.SetAccount(_account, _births, _domainStart, _domainStart + span, _gone); return; }

            // A film window records no reserve (IFilmWorld.TryAccount), so there the account marks
            // the body's births in the window and draws no line, and says so once.
            if (live == null || !live.TryAccount(_subject, out double reserve, out int children))
            {
                if (!_gone && _account.Count > 0)
                    Say(string.Format(CultureInfo.InvariantCulture, "body {0} is gone at {1:0.#} s: the account's line stops there", _subject, pose.Second));
                _gone = _account.Count > 0;
            }
            else
            {
                if (_children >= 0 && children > _children)
                {
                    _births.Add(pose.Second);
                    Say(string.Format(CultureInfo.InvariantCulture, "body {0} gave birth at {1:0.#} s ({2} children now): marked on its account",
                        _subject, pose.Second, children));
                }
                _children = children;

                if (!double.IsNaN(reserve)) _account.Add((pose.Second, reserve));
                else if (!_saidNoReserve)
                {
                    _saidNoReserve = true;
                    Say(string.Format(CultureInfo.InvariantCulture,
                        "body {0}'s reserve is not recorded in a film window: the account marks its births and draws no line", _subject));
                }
            }

            _card?.SetAccount(_account, _births, _domainStart, _domainStart + span, _gone);
        }

        // ---------------------------------------------------------------- the composite

        private void CompositeOnto(RenderTexture target)
        {
            if (_chart == null || _fade <= 0.001f || _material == null || _texture == null || target == null) return;
            if (_forceRepaint) ForceRepaint();

            Rect box = _chart.Place == SafariChartPlace.Full ? FullCard : CornerCard;
            bool full = _chart.Place == SafariChartPlace.Full;

            _material.SetFloat(FadeId, _fade);
            _material.SetFloat(WorldDimId, full ? FullWorldDim : 0f);
            _material.SetVector(PlateId, new Vector4(
                box.xMin / ReferenceWidth, 1f - box.yMax / ReferenceHeight,
                box.xMax / ReferenceWidth, 1f - box.yMin / ReferenceHeight));
            _material.SetFloat(PlateAlphaId, full ? FullPlateAlpha : CornerPlateAlpha);
            _material.SetFloat(PlateRadiusId, 14f * target.width / ReferenceWidth);
            _material.SetVector(TargetSizeId, new Vector4(target.width, target.height, 0f, 0f));

            Graphics.Blit(_texture, target, _material);
        }

        /// <summary>The card's region of the panel's texture read back once, on the chart's first visible frame.</summary>
        private void CheckInk()
        {
            _inkChecked = true;
            if (_texture == null || _chart == null) return;

            Rect box = _chart.Place == SafariChartPlace.Full ? FullCard : CornerCard;
            float sx = _texture.width / ReferenceWidth, sy = _texture.height / ReferenceHeight;
            int x = Mathf.Clamp(Mathf.FloorToInt(box.xMin * sx), 0, _texture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((ReferenceHeight - box.yMax) * sy), 0, _texture.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(box.width * sx), 1, _texture.width - x);
            int h = Mathf.Clamp(Mathf.CeilToInt(box.height * sy), 1, _texture.height - y);

            Texture2D probe = null;
            RenderTexture was = RenderTexture.active;
            try
            {
                probe = new Texture2D(w, h, TextureFormat.RGBA32, false);
                RenderTexture.active = _texture;
                probe.ReadPixels(new Rect(x, y, w, h), 0, 0, false);
                RenderTexture.active = was;

                Color32[] pixels = probe.GetPixels32();
                int inked = 0;
                for (int i = 0; i < pixels.Length; i++) if (pixels[i].a > 16) inked++;

                if (inked > 0)
                {
                    Say(string.Format(CultureInfo.InvariantCulture, "the chart panel drew into its texture: {0} of {1} pixels of the card inked ({2:0.#}%){3}",
                        inked, pixels.Length, 100d * inked / pixels.Length, _forceRepaint ? ", with the forced repaint" : ""));
                }
                else if (!_forceRepaint)
                {
                    _forceRepaint = true;
                    Say("WARNING: the chart panel's texture is empty over the card on its first visible frame: the offscreen panel was not " +
                        "repainted by the player loop here. Every composite from now asks UI Toolkit's runtime to update and repaint its " +
                        "offscreen panels first (reflection on an internal method); the next chart says whether that inked it.");
                }
                else
                {
                    Say("WARNING: the chart panel's texture is still empty with the forced repaint: this chart draws its plate and no ink");
                }
            }
            catch (Exception e)
            {
                Say("the chart's ink check could not read the panel's texture: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                RenderTexture.active = was;
                if (probe != null) Kill(probe);
            }
        }

        private static MethodInfo _update, _repaint;
        private static bool _looked;

        /// <summary>UI Toolkit's own offscreen update and repaint, called by hand: the fallback when the player loop does not draw the panel.</summary>
        private void ForceRepaint()
        {
            try
            {
                if (!_looked)
                {
                    _looked = true;
                    Type utility = typeof(PanelSettings).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
                    const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    _update = utility?.GetMethod("UpdateRuntimePanels", any, null, Type.EmptyTypes, null);
                    _repaint = utility?.GetMethod("RepaintOffscreenPanels", any, null, Type.EmptyTypes, null);
                    Say("the forced repaint: UpdateRuntimePanels " + (_update != null ? "found" : "NOT found") +
                        ", RepaintOffscreenPanels " + (_repaint != null ? "found" : "NOT found"));
                }

                _update?.Invoke(null, null);
                _repaint?.Invoke(null, null);
            }
            catch (Exception e)
            {
                if (!_repaintSaid) Say("the forced repaint threw: " + e.GetType().Name + ": " + (e.InnerException?.Message ?? e.Message));
                _repaintSaid = true;
            }
        }

        private void SayDone()
        {
            if (_chart == null || _scene == null) return;
            Say(string.Format(CultureInfo.InvariantCulture, "{0}: the chart was on {1} frame(s){2}",
                _scene.FromStory ? "story " + _scene.StoryNumber.ToString(CultureInfo.InvariantCulture) : _scene.Slug, _visibleFrames,
                _chart.Kind == SafariChartKind.Account
                    ? string.Format(CultureInfo.InvariantCulture, "; the account holds {0} sample(s) of body {1} and {2} birth(s)", _account.Count, _subject, _births.Count)
                    : ""));
        }

        public void Dispose()
        {
            SayDone();
            _chart = null;
            _scene = null;

            if (_settings != null) _settings.targetTexture = null;
            if (_host != null) Kill(_host);
            if (_settings != null) Kill(_settings);
            if (_texture != null)
            {
                _texture.Release();
                Kill(_texture);
            }
            if (_material != null) Kill(_material);

            _host = null;
            _document = null;
            _settings = null;
            _texture = null;
            _material = null;
            _card = null;
        }

        private static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }

    /// <summary>
    /// One chart, laid out in reference pixels and drawn with the panel's painter: the ink only,
    /// on no background (the plate is the composite's).
    /// </summary>
    /// <remarks>
    /// The interface's rules (design/SPEC.md, TheatreUiStyles.uss): IBM Plex Sans for words and
    /// Plex Mono for numbers, the interface's inks, no hue but the clade's, no gridlines. A line
    /// chart has one baseline, short ticks and a few rounded numbers; its highlighted series is the
    /// clade's colour and the rest a muted grey; a mark is a thin upright line with its label at the
    /// top. Every label is placed by hand at a size set here, so the layout never waits on a
    /// measurement the panel has not made yet.
    /// </remarks>
    internal sealed class SafariChartCard : VisualElement
    {
        public static readonly Color InkHi = new Color32(237, 243, 245, 255);
        public static readonly Color Ink = new Color32(176, 190, 196, 255);
        public static readonly Color MutedLine = new Color32(128, 142, 150, 255);
        public static readonly Color Rule = new Color(237f / 255f, 243f / 255f, 245f / 255f, 0.45f);
        public static readonly Color MarkLine = new Color(237f / 255f, 243f / 255f, 245f / 255f, 0.55f);

        private SafariChart _chart;
        private Color _accent = InkHi;
        private bool _full;
        private Rect _box;

        // sizes, reference pixels
        private float _pad, _title, _axis, _tick, _value, _thin, _thick;

        // the plot, in the card's own coordinates
        private Rect _plot;
        private bool _hasDomain;
        private double _x0, _x1, _y0, _y1;
        private readonly List<double> _xTicks = new List<double>();
        private readonly List<double> _yTicks = new List<double>();

        // the account's live state
        private readonly List<(double t, double j)> _points = new List<(double, double)>();
        private readonly List<double> _births = new List<double>();
        private Label _valueLabel;
        private int _laidBirths = -1;
        private bool _gone;

        public SafariChartCard()
        {
            name = "safari-chart";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        /// <summary>No chart: hidden, and its labels gone.</summary>
        public void Empty()
        {
            _chart = null;
            _hasDomain = false;
            _points.Clear();
            _births.Clear();
            Clear();
            style.display = DisplayStyle.None;
            MarkDirtyRepaint();
        }

        /// <summary>Lays a chart out in its card.</summary>
        public void Build(SafariChart chart, Color accent, Rect box, bool full)
        {
            _chart = chart;
            _accent = accent;
            _full = full;
            _box = box;
            _points.Clear();
            _births.Clear();
            _laidBirths = -1;
            _gone = false;
            _hasDomain = false;
            _xTicks.Clear();
            _yTicks.Clear();
            _x0 = _y0 = 0d;
            _x1 = _y1 = 1d;
            _barRects.Clear();

            _pad = full ? 34f : 22f;
            _title = full ? 44f : 32f;
            _axis = full ? 28f : 23f;
            _tick = full ? 26f : 22f;
            _value = full ? 32f : 27f;
            _thin = full ? 4f : 3f;
            _thick = full ? 6f : 5f;

            style.left = box.x;
            style.top = box.y;
            style.width = box.width;
            style.height = box.height;
            style.display = DisplayStyle.Flex;

            if (chart.Kind == SafariChartKind.Line) Domain(chart);
            LayOut();
        }

        /// <summary>An account's samples, its births and its window, one frame at a time.</summary>
        public void SetAccount(List<(double t, double j)> samples, List<double> births, double from, double to, bool gone)
        {
            if (_chart == null || _chart.Kind != SafariChartKind.Account) return;

            _points.Clear();
            _points.AddRange(samples);
            _births.Clear();
            _births.AddRange(births);
            _gone = gone;

            bool relay = false;
            if (!double.IsNaN(from) && (!_hasDomain || Math.Abs(from - _x0) > 1e-6 || Math.Abs(to - _x1) > 1e-6))
            {
                _x0 = from;
                _x1 = Math.Max(from + 1d, to);
                _hasDomain = true;
                relay = true;
            }

            // The reserve's axis grows and never shrinks, so the line never jumps as it rises:
            // half as much again as the first sample, and a nice step up whenever it is passed.
            double top = 0d;
            foreach (var p in _points) top = Math.Max(top, p.j);
            if (_points.Count > 0 && (_yTicks.Count == 0 || top > 0.97d * _y1))
            {
                double want = Math.Max(1d, top * (_yTicks.Count == 0 ? 1.5d : 1.25d));
                _y0 = 0d;
                _y1 = NiceTop(want, 4, out double step);
                _yTicks.Clear();
                _yTicks.AddRange(Ticks(_y0, _y1, step));
                relay = true;
            }

            if (_births.Count != _laidBirths) relay = true;

            if (relay) LayOut();
            PlaceTheValue();
            MarkDirtyRepaint();
        }

        // ---------------------------------------------------------------- layout

        private void Domain(SafariChart chart)
        {
            double xmin = double.PositiveInfinity, xmax = double.NegativeInfinity, ymin = 0d, ymax = double.NegativeInfinity;
            foreach (SafariChart.Series s in chart.Lines)
            {
                foreach (var p in s.Points)
                {
                    xmin = Math.Min(xmin, p.x);
                    xmax = Math.Max(xmax, p.x);
                    ymin = Math.Min(ymin, p.y);
                    ymax = Math.Max(ymax, p.y);
                }
            }
            foreach (SafariChart.Mark m in chart.Marks)
            {
                xmin = Math.Min(xmin, m.X);
                xmax = Math.Max(xmax, m.X);
            }

            if (double.IsInfinity(xmin) || double.IsInfinity(xmax)) return;
            if (!(xmax > xmin)) xmax = xmin + 1d;

            // A series over the run that starts a sample or two in reads from zero.
            if (xmin > 0d && xmin <= 0.1d * (xmax - xmin)) xmin = 0d;
            if (!(ymax > ymin)) ymax = ymin + 1d;

            _x0 = xmin;
            _x1 = xmax;
            _xTicks.Clear();
            _xTicks.AddRange(Ticks(_x0, _x1, TickStep(_x0, _x1, _full ? 6 : 4)));

            // From zero for a count or a share, from a rounded floor under a negative.
            double ystep = TickStep(Math.Min(0d, ymin), ymax, 4);
            _y0 = ymin < 0d ? Math.Floor(ymin / ystep) * ystep : 0d;
            _y1 = Math.Ceiling(ymax / ystep - 1e-9) * ystep;
            if (_y1 <= _y0) _y1 = _y0 + ystep;
            _yTicks.Clear();
            _yTicks.AddRange(Ticks(_y0, _y1, ystep));
            _hasDomain = true;
        }

        private void LayOut()
        {
            Clear();
            _valueLabel = null;
            if (_chart == null) return;

            float w = _box.width, h = _box.height;
            float y = _pad;
            SafariChartKind kind = _chart.Kind;

            string title = _chart.Title ?? (kind == SafariChartKind.Account ? "Its reserve" : null);
            if (!string.IsNullOrEmpty(title))
            {
                float th = _title * 1.3f;
                Place(Text(title, "chart-sans", _title, InkHi, TextAnchor.MiddleLeft), _pad, y, w - 2f * _pad, th);
                y += th + 4f;
            }

            // The row under the title: the value axis's name on the left, the legend on the right.
            string yName = _chart.YLabel ?? (kind == SafariChartKind.Account ? "joules in reserve" : null);
            bool legend = kind == SafariChartKind.Line && _chart.Lines.Count >= 2;
            float rowH = _axis * 1.35f;
            if (kind != SafariChartKind.Bars && (yName != null || legend))
            {
                if (yName != null) Place(Text(yName, "chart-sans", _axis, Ink, TextAnchor.MiddleLeft), _pad, y, legend ? 0.42f * w : w - 2f * _pad, rowH);
                if (legend) Legend(y, rowH, yName != null ? 0.44f * w : _pad);
                y += rowH + 6f;
            }

            float bottom = h - _pad;
            string xName = _chart.XLabel ?? (kind == SafariChartKind.Account ? "time in the run (s)" : null);
            float axisH = _axis * 1.3f;
            if (xName != null) bottom -= axisH + 2f;

            if (kind == SafariChartKind.Bars)
            {
                _plot = new Rect(_pad, y + 4f, w - 2f * _pad, Mathf.Max(20f, bottom - y - 8f));
                if (xName != null) Place(Text(xName, "chart-sans", _axis, Ink, TextAnchor.MiddleRight), _pad, h - _pad - axisH, w - 2f * _pad, axisH);
                LayBars();
                return;
            }

            float tickH = _tick * 1.3f;
            bottom -= tickH + 8f;
            float top = y + 0.55f * _tick;

            // The value axis's numbers decide how far in the plot starts.
            int longest = 1;
            foreach (double v in _yTicks) longest = Math.Max(longest, Number(v, _yTicks).Length);
            float yTickW = longest * _tick * 0.62f + 6f;
            float left = _pad + yTickW + 10f;
            float right = w - _pad - (kind == SafariChartKind.Account ? 8f : 6f);

            _plot = new Rect(left, top, Mathf.Max(20f, right - left), Mathf.Max(20f, bottom - top));

            if (xName != null) Place(Text(xName, "chart-sans", _axis, Ink, TextAnchor.MiddleCenter), _plot.xMin, h - _pad - axisH, _plot.width, axisH);

            if (!_hasDomain) return;

            foreach (double v in _yTicks)
                Place(Text(Number(v, _yTicks), "chart-mono", _tick, Ink, TextAnchor.MiddleRight), _pad, Y(v) - 0.5f * tickH, yTickW, tickH);

            // An account's window is set by its first sample, so its time ticks are worked out here.
            if (kind == SafariChartKind.Account)
            {
                _xTicks.Clear();
                _xTicks.AddRange(Ticks(_x0, _x1, TickStep(_x0, _x1, _full ? 6 : 4)));
            }

            const float tickW = 150f;
            foreach (double v in _xTicks)
            {
                float cx = X(v);
                float lx = Mathf.Clamp(cx - 0.5f * tickW, 2f, w - tickW - 2f);
                Place(Text(Number(v, _xTicks), "chart-mono", _tick, Ink, TextAnchor.UpperCenter), lx, _plot.yMax + 8f, tickW, tickH);
            }

            // The marks' labels at the top of the plot, beside their lines, stepped down a row when
            // two would overlap.
            var labels = new List<(double x, string text)>();
            if (kind == SafariChartKind.Line)
                foreach (SafariChart.Mark m in _chart.Marks) if (!string.IsNullOrEmpty(m.Label)) labels.Add((m.X, m.Label));
            if (kind == SafariChartKind.Account)
                foreach (double b in _births) labels.Add((b, "birth"));
            _laidBirths = _births.Count;

            float lastRight = float.NegativeInfinity;
            int row = 0;
            const float markW = 280f;
            foreach (var (mx, text) in labels.OrderBy(l => l.x))
            {
                if (mx < _x0 || mx > _x1) continue;
                float cx = X(mx);
                bool leftward = cx + 8f + 0.6f * markW > w - _pad;
                float lx = leftward ? cx - 8f - markW : cx + 8f;
                row = lx < lastRight + 12f ? row + 1 : 0;
                float ly = _plot.yMin + row * (tickH + 2f);
                Place(Text(text, "chart-sans", _tick, InkHi, leftward ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft), lx, ly, markW, tickH);
                lastRight = leftward ? cx : lx + Mathf.Min(markW, text.Length * _tick * 0.55f);
            }

            if (kind == SafariChartKind.Account)
            {
                _valueLabel = Text("", "chart-strong", _value, _accent, TextAnchor.MiddleLeft);
                Place(_valueLabel, _plot.xMin, _plot.yMin, 220f, _value * 1.3f);
            }

            MarkDirtyRepaint();
        }

        private void Legend(float y, float rowH, float left)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute;
            row.style.left = left;
            row.style.right = _pad;
            row.style.top = y;
            row.style.height = rowH;
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.alignItems = Align.Center;
            row.style.overflow = Overflow.Hidden;
            Add(row);

            foreach (SafariChart.Series s in _chart.Lines.OrderBy(k => k.Highlight ? 0 : 1))
            {
                var swatch = new VisualElement { pickingMode = PickingMode.Ignore };
                swatch.style.width = 22f;
                swatch.style.height = s.Highlight ? _thick : _thin;
                swatch.style.backgroundColor = s.Highlight ? _accent : MutedLine;
                swatch.style.marginRight = 8f;
                swatch.style.flexShrink = 0f;
                row.Add(swatch);

                Label name = Text(string.IsNullOrEmpty(s.Name) ? "(unnamed)" : s.Name, "chart-sans", _tick, s.Highlight ? InkHi : Ink, TextAnchor.MiddleLeft, absolute: false);
                name.style.marginRight = 18f;
                name.style.height = rowH;
                name.style.flexShrink = 1f;
                row.Add(name);
            }
        }

        private void LayBars()
        {
            List<SafariChart.Bar> bars = _chart.Bars;
            int n = bars.Count;
            float rowH = Mathf.Min(_plot.height / Mathf.Max(1, n), (_full ? 2.8f : 2.6f) * _tick);
            float labelW = 0.42f * _plot.width;
            float valueW = _value * 4.4f;
            float barX = _plot.xMin + labelW + 14f;
            float barMost = Mathf.Max(10f, _plot.xMax - barX - valueW - 10f);
            double most = 0d;
            foreach (SafariChart.Bar b in bars) most = Math.Max(most, b.Value);
            if (!(most > 0d)) most = 1d;

            _barRects.Clear();
            bool any = bars.Any(b => b.Highlight);

            for (int i = 0; i < n; i++)
            {
                SafariChart.Bar b = bars[i];
                float cy = _plot.yMin + rowH * (i + 0.5f);
                float lh = _axis * 1.3f;
                Place(Text(b.Label ?? "", "chart-sans", _axis, InkHi, TextAnchor.MiddleRight), _plot.xMin, cy - 0.5f * lh, labelW, lh);

                float length = (float)(Math.Max(0d, b.Value) / most) * barMost;
                float thick = 0.55f * rowH;
                _barRects.Add((new Rect(barX, cy - 0.5f * thick, Mathf.Max(length, 2f), thick), !any || b.Highlight));

                float vh = _value * 1.3f;
                Place(Text(Value(b.Value), "chart-mono", _value, InkHi, TextAnchor.MiddleLeft), barX + length + 10f, cy - 0.5f * vh, valueW, vh);
            }

            _barBase = barX;
            MarkDirtyRepaint();
        }

        private readonly List<(Rect rect, bool accent)> _barRects = new List<(Rect, bool)>();
        private float _barBase;

        /// <summary>The account's number at the end of its line, clear of the card's edge.</summary>
        private void PlaceTheValue()
        {
            if (_valueLabel == null) return;

            if (_points.Count == 0 || !_hasDomain)
            {
                _valueLabel.text = "";
                return;
            }

            var last = _points[_points.Count - 1];
            string text = Value(last.j) + " J" + (_gone ? " (gone)" : "");
            if (_valueLabel.text != text) _valueLabel.text = text;

            float lh = _value * 1.3f;
            float x = X(Math.Min(Math.Max(last.t, _x0), _x1));
            float y = Y(Math.Min(last.j, _y1));
            const float width = 220f;
            bool leftward = x + 12f + width > _box.width - 4f;
            _valueLabel.style.unityTextAlign = leftward ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            _valueLabel.style.left = leftward ? x - 12f - width : x + 12f;
            _valueLabel.style.top = Mathf.Clamp(y - lh - 4f, 2f, _box.height - lh - 2f);
            _valueLabel.style.width = width;
            _valueLabel.style.height = lh;
        }

        private Label Text(string text, string face, float size, Color colour, TextAnchor align, bool absolute = true)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("chart-text");
            label.AddToClassList(face);
            if (absolute) label.style.position = Position.Absolute;
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.unityTextAlign = align;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.marginTop = 0f;
            label.style.marginBottom = 0f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
            label.style.paddingTop = 0f;
            label.style.paddingBottom = 0f;
            if (absolute) Add(label);
            return label;
        }

        private static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.left = x;
            e.style.top = y;
            e.style.width = Mathf.Max(1f, w);
            e.style.height = Mathf.Max(1f, h);
        }

        private float X(double x) => _plot.xMin + (float)((x - _x0) / (_x1 - _x0)) * _plot.width;
        private float Y(double y) => _plot.yMax - (float)((y - _y0) / (_y1 - _y0)) * _plot.height;

        // ---------------------------------------------------------------- drawing

        private void Draw(MeshGenerationContext context)
        {
            if (_chart == null) return;

            Painter2D p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            if (_chart.Kind == SafariChartKind.Bars)
            {
                foreach (var (rect, accent) in _barRects) FillRect(p, rect, accent ? _accent : Ink);
                if (_barRects.Count > 0)
                    Stroke(p, Rule, 2f, new Vector2(_barBase, _barRects[0].rect.yMin - 6f), new Vector2(_barBase, _barRects[_barRects.Count - 1].rect.yMax + 6f));
                return;
            }

            if (!_hasDomain) return;

            // One baseline, at zero or the floor, and short ticks: no grid.
            Stroke(p, Rule, 2f, new Vector2(_plot.xMin, Y(_y0)), new Vector2(_plot.xMax, Y(_y0)));
            foreach (double v in _xTicks) Stroke(p, Rule, 2f, new Vector2(X(v), _plot.yMax), new Vector2(X(v), _plot.yMax + 7f));
            foreach (double v in _yTicks)
                if (Math.Abs(v - _y0) > 1e-9) Stroke(p, Rule, 2f, new Vector2(_plot.xMin - 7f, Y(v)), new Vector2(_plot.xMin, Y(v)));

            if (_chart.Kind == SafariChartKind.Line)
            {
                foreach (SafariChart.Mark m in _chart.Marks)
                    if (m.X >= _x0 && m.X <= _x1) Stroke(p, MarkLine, 2f, new Vector2(X(m.X), _plot.yMin - 2f), new Vector2(X(m.X), _plot.yMax));

                bool any = _chart.Lines.Any(s => s.Highlight);
                foreach (SafariChart.Series s in _chart.Lines.Where(s => !s.Highlight))
                    Polyline(p, s.Points, any ? MutedLine : _accent, any ? _thin : _thick);
                foreach (SafariChart.Series s in _chart.Lines.Where(s => s.Highlight))
                {
                    Polyline(p, s.Points, _accent, _thick);
                    var end = s.Points[s.Points.Count - 1];
                    Dot(p, new Vector2(X(end.x), Y(end.y)), _thick * 1.2f, _accent);
                }
                return;
            }

            // The account: its births, its line, and a dot at its end.
            foreach (double b in _births)
                if (b >= _x0 && b <= _x1) Stroke(p, MarkLine, 2f, new Vector2(X(b), _plot.yMin - 2f), new Vector2(X(b), _plot.yMax));

            var inside = new List<(double x, double y)>(_points.Count);
            foreach (var q in _points) if (q.t >= _x0 - 1e-6 && q.t <= _x1 + 1e-6) inside.Add((q.t, Math.Min(q.j, _y1)));
            if (inside.Count >= 2) Polyline(p, inside, _accent, _thick);
            if (inside.Count >= 1)
            {
                var end = inside[inside.Count - 1];
                Dot(p, new Vector2(X(end.x), Y(end.y)), _thick * 1.3f, _gone ? Ink : _accent);
            }

            foreach (double b in _births)
            {
                if (b < _x0 || b > _x1) continue;
                double at = double.NaN;
                foreach (var q in inside) if (q.x >= b - 1e-6) { at = q.y; break; }
                if (!double.IsNaN(at)) Dot(p, new Vector2(X(b), Y(at)), _thick * 1.1f, InkHi);
            }
        }

        private void Polyline(Painter2D p, List<(double x, double y)> points, Color colour, float width)
        {
            if (points.Count < 2) return;
            p.strokeColor = colour;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(new Vector2(X(points[0].x), Y(points[0].y)));
            for (int i = 1; i < points.Count; i++) p.LineTo(new Vector2(X(points[i].x), Y(points[i].y)));
            p.Stroke();
        }

        private static void Stroke(Painter2D p, Color colour, float width, Vector2 a, Vector2 b)
        {
            p.strokeColor = colour;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        private static void FillRect(Painter2D p, Rect r, Color colour)
        {
            p.fillColor = colour;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>A filled dot as a sixteen-sided polygon, so no angle type is needed.</summary>
        private static void Dot(Painter2D p, Vector2 centre, float radius, Color colour)
        {
            p.fillColor = colour;
            p.BeginPath();
            for (int i = 0; i < 16; i++)
            {
                float a = i * (Mathf.PI * 2f / 16f);
                var v = new Vector2(centre.x + radius * Mathf.Cos(a), centre.y + radius * Mathf.Sin(a));
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
            p.Fill();
        }

        // ---------------------------------------------------------------- numbers

        /// <summary>
        /// A tick step of 1, 2, 2.5 or 5 times a power of ten, the one nearest the step that would
        /// put this many ticks across a range (nearest and not next above, which gave three ticks
        /// where six were asked for).
        /// </summary>
        internal static double TickStep(double lo, double hi, int wanted)
        {
            double range = hi - lo;
            if (!(range > 0d) || double.IsInfinity(range)) return 1d;
            double raw = range / Math.Max(1, wanted - 1);
            double magnitude = Math.Pow(10d, Math.Floor(Math.Log10(raw)));
            double r = raw / magnitude;
            double nice = r < 1.5d ? 1d : r < 2.25d ? 2d : r < 3.5d ? 2.5d : r < 7.5d ? 5d : 10d;
            return nice * magnitude;
        }

        /// <summary>A range's top rounded up to a tick, and the step it is a multiple of.</summary>
        internal static double NiceTop(double top, int wanted, out double step)
        {
            step = TickStep(0d, top, wanted);
            return Math.Max(step, Math.Ceiling(top / step - 1e-9) * step);
        }

        /// <summary>The multiples of a step inside a range, taken as whole multiples so they do not drift.</summary>
        internal static List<double> Ticks(double lo, double hi, double step)
        {
            var ticks = new List<double>();
            if (!(step > 0d) || !(hi >= lo)) return ticks;
            long first = (long)Math.Ceiling(lo / step - 1e-9);
            long last = (long)Math.Floor(hi / step + 1e-9);
            for (long k = first; k <= last && ticks.Count < 40; k++) ticks.Add(k * step);
            return ticks;
        }

        /// <summary>A tick's number, with the thousands comma and as many decimals as the step needs.</summary>
        internal static string Number(double v, List<double> ticks)
        {
            double step = ticks.Count >= 2 ? Math.Abs(ticks[1] - ticks[0]) : 1d;
            int decimals = 0;
            while (decimals < 4 && Math.Abs(step * Math.Pow(10d, decimals) - Math.Round(step * Math.Pow(10d, decimals))) > 1e-6 * Math.Max(1d, step * Math.Pow(10d, decimals)))
                decimals++;
            if (Math.Abs(v) < 1e-9 * Math.Max(1d, step)) return "0";
            return v.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        /// <summary>A value to three significant figures or so, with the thousands comma.</summary>
        internal static string Value(double v)
        {
            double a = Math.Abs(v);
            if (a >= 100d) return v.ToString("N0", CultureInfo.InvariantCulture);
            if (a >= 10d) return v.ToString("0.#", CultureInfo.InvariantCulture);
            if (a >= 1d) return v.ToString("0.##", CultureInfo.InvariantCulture);
            if (a == 0d) return "0";
            int decimals = Math.Min(8, (int)Math.Ceiling(-Math.Log10(a)) + 2);
            return v.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
        }
    }
}
