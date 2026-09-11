using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre, photographed: a framed view of the world on screen, written out as a PNG.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> On 2026-09-10 the owner opened round 33 in the theatre and saw in
    /// a minute what thirty-three rounds of tables could not show: the whole world was two
    /// vertical ribbons about a metre wide in a box twenty metres long (logbook/0083). The agent
    /// that read those rounds has no eyes. It read a report carrying a mean depth and per-patch
    /// bins and nothing at all about x or z, and it believed a wider world than the one that ran.
    /// A picture was the missing instrument, and a PNG file is a picture an agent can read.
    /// </para>
    /// <para>
    /// <b>The frame comes from the run's own config, never from a constant.</b> The box is
    /// <c>SharedSpace</c>'s literal water: K patches of <c>sqrt(area / K)</c> metres along x, one
    /// patch across z, <c>WorldDepthMetres</c> down. A hard-coded 20 by 5 by 60 would have been
    /// right for the campaign and wrong for the first run that changed a patch count, and it
    /// would have been wrong silently, which is the failure mode this project spends most of its
    /// gotchas on.
    /// </para>
    /// <para>
    /// <b>Three of the four census views are orthographic and the fourth is not.</b> Side, end
    /// and top are the literal projections the names promise, and a perspective camera would
    /// foreshorten the far half of the box and make a column look like a cone. The iso view is
    /// perspective, because its whole job is to give the eye the depth cue the three flat views
    /// deliberately throw away. The two views that are not of the census, close and sky, are both
    /// perspective and neither is framed on the box: one is a portrait of a body and the other is
    /// taken from inside the water looking up at the surface.
    /// </para>
    /// <para>
    /// <b>The label and the markers are composited into the pixels, not rendered.</b>
    /// <c>GUI.Label</c> in <c>OnGUI</c> draws to the screen and never into a
    /// <see cref="RenderTexture"/>, and a <c>TextMesh</c> would put the text in the world, where
    /// it would be framed, lit and occluded like everything else in it. So the text is stamped
    /// into the <see cref="Color32"/> array after <c>ReadPixels</c>, from the five by seven
    /// bitmap font at the bottom of this file: no font asset, no shader, no dependence on which
    /// render pipeline resolved. The box's own wireframe is stamped the same way for the same
    /// reason, over whatever the scene's <see cref="WaterBounds"/> drew, so that the frame reads
    /// even if a camera this class made never gets that component's immediate-mode geometry.
    /// </para>
    /// <para>
    /// <b>A body gets a marker when its mesh is too small to see.</b> The box is 60 m deep and 20
    /// m long, so a side view fitted to the depth puts about 15 pixels in a metre and a 0.3 m
    /// creature covers four of them; at that size a body is an artefact rather than a shape. Any
    /// body whose projected diameter is under <see cref="MinimumBodyPixels"/> therefore gets a
    /// screen-space marker of <see cref="MarkerPixels"/> at its centre of mass, coloured by guild
    /// the way <see cref="TheatrePalette"/> colours a part. The markers are stamped over the
    /// rendered image and so ignore occlusion: two bodies a metre apart along the view axis draw
    /// the same size, and the nearer one does not hide the further. That is the right trade for a
    /// picture whose question is where the bodies are, and it is a wrong one for any question
    /// about what a body looks like, which is Mode A's.
    /// </para>
    /// <para>
    /// <b>It writes only the file it is asked for.</b> Nothing goes into the run directory: Mode B
    /// never writes there, and a viewer that can modify the record is a viewer whose recording
    /// cannot be trusted afterwards.
    /// </para>
    /// </remarks>
    public sealed class SnapshotCamera : IDisposable
    {
        /// <summary>Which way the camera looks. The first four frame the whole box.</summary>
        public enum View
        {
            /// <summary>Along z: the box's length by its depth, the view the ribbons showed up in.</summary>
            Side = 0,

            /// <summary>Along x: the box's width by its depth.</summary>
            End = 1,

            /// <summary>Straight down: length by width, the footprint.</summary>
            Top = 2,

            /// <summary>Three quarters, from above one corner. The perspective census view.</summary>
            Iso = 3,

            /// <summary>
            /// The six largest bodies, from a three quarter angle, close enough to see one.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <b>Why a fifth view.</b> The other four frame the whole box, which is what a
            /// question about where the bodies are needs. The campaign's water is 20 m long in a
            /// 1600 px frame, so a metre is about eighty pixels and a 0.3 m creature is
            /// twenty-four of them: enough to say it is there and its guild, and nowhere near
            /// enough to say what its surface does. A skin cannot be judged from a picture that
            /// cannot resolve it, so this one throws away the census and frames a handful of
            /// bodies instead.
            /// </para>
            /// <para>
            /// <b>Largest, because the question is the silhouette.</b> The bodies with the most
            /// pixels on them are the ones a carve can be read off, and picking them by size
            /// makes the choice repeatable rather than a matter of where the camera happened to
            /// point. It is not a sample of the world and must not be read as one: nothing about
            /// the population, the guild mix or the spread is visible in it.
            /// </para>
            /// <para>
            /// <b>The six largest in the world are not six neighbours, and the first cut of this
            /// view learned it the expensive way.</b> Asked for the six largest bodies in r35-s1
            /// at t = 5,000 s, it framed 19.35 by 13.28 m, which is the whole box: the clades sit
            /// in columns metres apart (logbook/0083), so the largest body in one column and the
            /// largest in another are as far apart as anything in the world. The picture came out
            /// at about twenty pixels a body, which is the world view again. So the anchor is the
            /// largest body and the other five are the largest of its neighbours; see
            /// <see cref="CloseOn"/>.
            /// </para>
            /// <para>
            /// <b>It carries no box and no markers.</b> A frame a few metres across cuts the
            /// water's edges at odd angles, and a marker is a five pixel square standing in for
            /// a body too small to draw, which is the opposite of this view's purpose.
            /// </para>
            /// </remarks>
            Close = 4,

            /// <summary>
            /// Up at the surface from three metres under, for the sun and the window.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <b>Why a sixth view.</b> The fourth day put a sea on the world's ceiling: a
            /// rippling surface, the sky refracted through it into Snell's window with the sun in
            /// it, and shafts of light coming down (logbook/specs/skin-spec-4.md). Not one of the
            /// other five can see any of it. Four of them photograph the census from outside the
            /// box and have the sea turned off for them
            /// (<see cref="HideWhatOnlyTheWaterSees"/>), and the close view looks along the water
            /// at a body. So this one stands inside the water and looks up, which is the only
            /// place the day's work exists.
            /// </para>
            /// <para>
            /// <b>Three metres down, at the box's centre, sixty degrees up, wide.</b> Three
            /// metres is close enough that the window fills a good part of the frame and far
            /// enough that it is a window rather than a ceiling tile; sixty degrees puts the
            /// vertical near the top of the frame and leaves the bottom third for the water and
            /// the bodies in it; and the lens is wide because the whole point is one frame that
            /// holds the window, the sun, the shafts and what is swimming under them.
            /// </para>
            /// <para>
            /// <b>It looks along the sun's bearing.</b> The disc is wherever the scene's
            /// directional light is, so a camera pointed at a fixed compass bearing would find it
            /// in some runs and not others. The bearing is read from the same global the shaders
            /// draw the sun from (<c>TheatreWater.hlsl</c>, <c>_EvoSun</c>), so the picture and
            /// the ceiling cannot disagree about where the sun is.
            /// </para>
            /// <para>
            /// <b>No box and no markers, as the close view has none.</b> The box's wireframe
            /// would be stamped across the window, and a five pixel marker is for a body too
            /// small to draw, which from inside the water at this range is not the case.
            /// </para>
            /// </remarks>
            Sky = 5,
        }

        /// <summary>How many bodies <see cref="View.Close"/> frames.</summary>
        public const int CloseBodies = 6;

        /// <summary>How far under the surface <see cref="View.Sky"/> stands, in metres.</summary>
        public const float SkyDepthMetres = 3f;

        /// <summary>How far above the horizontal <see cref="View.Sky"/> looks, in degrees.</summary>
        public const float SkyRiseDegrees = 60f;

        /// <summary>The wide lens <see cref="View.Sky"/> uses, in degrees across the frame's height.</summary>
        public const float SkyFieldOfView = 76f;

        /// <summary>A projected body narrower than this gets a marker instead of being trusted.</summary>
        public const float MinimumBodyPixels = 4f;

        /// <summary>
        /// How many chords a tank's circle is stamped from — <c>logbook/specs/tank-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// Forty-eight, which is <c>Evosim.Sim.TankWall.Segments</c>: the glass a body actually
        /// hits is a forty-eight-sided prism, so the outline is the wall rather than a smoothed
        /// idea of it. At 1600 px across a 11.3 m tank a chord is about thirty pixels, which reads
        /// as a circle; the patch rings use the same number so that one picture has one arc
        /// resolution in it.
        /// </remarks>
        public const int CircleSegments = 48;

        /// <summary>The marker's coloured square, in pixels, inside a one-pixel dark surround.</summary>
        public const int MarkerPixels = 5;

        /// <summary>Pixels per cell of the label's font. Three is legible at 1600 across.</summary>
        public const int LabelScale = 3;

        /// <summary>
        /// The water: the frame's background and the colour the fog carries everything towards.
        /// </summary>
        /// <remarks>
        /// Near black rather than the sandbox's blue since the skin landed. A dark field is the
        /// arrangement plankton is photographed under and the one the theatre's shaders are written
        /// for (research/theatre-look/README.md, "Lighting, before any material"); against a lit
        /// blue the rim that carries the guild is the thing that disappears. The value is
        /// <see cref="TheatreSkin.Water"/>'s, kept in step by eye rather than by reference, since
        /// this class must work on a scene that never built a skin.
        /// </remarks>
        public Color Water = new Color(0.012f, 0.032f, 0.048f);

        // The palette's three guilds. Duplicated as plain fields rather than shared with
        // TheatrePalette, which paints renderers through a MaterialPropertyBlock and has no
        // opinion about a pixel; the values are the same and the runner's inspector owns them.
        public Color Photosynthetic = new Color(0.34f, 0.72f, 0.36f);
        public Color Absorptive = new Color(0.90f, 0.55f, 0.22f);
        public Color Structural = new Color(0.72f, 0.74f, 0.78f);

        private readonly int _width;
        private readonly int _height;
        private readonly RenderTexture _target;
        private readonly Texture2D _readback;
        private readonly GameObject _holder;
        private readonly Camera _camera;

        private Color32[] _pixels;

        /// <summary>The largest picture this will make. A 4K frame is 33 MB of readback.</summary>
        public const int MaximumSide = 4096;

        public SnapshotCamera(int width, int height)
        {
            _width = Mathf.Clamp(width, 64, MaximumSide);
            _height = Mathf.Clamp(height, 64, MaximumSide);

            _target = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Theatre Snapshot",
                antiAliasing = 1,
            };

            _readback = new Texture2D(_width, _height, TextureFormat.RGBA32, false);

            // Hidden and not saved: this camera belongs to one call and must never be caught by
            // a scene save or turn up in the hierarchy the owner is flying around in.
            _holder = new GameObject("Theatre Snapshot Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            _camera = _holder.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Water;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 8000f;
            _camera.targetTexture = _target;
            _camera.cullingMask = ~0;

            // Disabled, because this camera renders when it is asked to and never once a frame.
            // An enabled second camera would render the whole world every frame of a run that
            // spends its frames stepping physics.
            _camera.enabled = false;
        }

        /// <summary>
        /// Reads a comma separated view list, or every view when it is empty.
        /// </summary>
        /// <param name="text">Something like <c>side,top</c>. Case and spaces do not matter.</param>
        /// <param name="refusal">Which word was not a view, or null.</param>
        public static View[] ParseViews(string text, out string refusal)
        {
            refusal = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                // Close and sky are not in the default set. Each answers a different question
                // from the other four and takes a picture nobody asked for whenever a caller
                // wants a census, so each is named or it is not taken.
                return new[] { View.Side, View.End, View.Top, View.Iso };
            }

            string[] words = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var views = new List<View>(words.Length);

            foreach (string word in words)
            {
                string trimmed = word.Trim();
                if (trimmed.Length == 0) continue;

                if (!Enum.TryParse(trimmed, true, out View view))
                {
                    refusal =
                        "'" + trimmed +
                        "' is not a view; the views are side, end, top, iso, close, sky.";
                    return null;
                }

                if (!views.Contains(view)) views.Add(view);
            }

            if (views.Count == 0)
            {
                refusal = "no view was named.";
                return null;
            }

            return views.ToArray();
        }

        /// <summary>The name a view goes into a file name under.</summary>
        public static string NameOf(View view) => view.ToString().ToLowerInvariant();

        /// <summary>
        /// Frames the world, renders it, stamps the markers and the label, and writes the PNG.
        /// </summary>
        /// <param name="replay">The world on screen. Read, never stepped.</param>
        /// <param name="view">Which way to look.</param>
        /// <param name="path">The file to write. Its directory is created if it is missing.</param>
        /// <param name="remark">What the picture cost and what it could not hold, for the log.</param>
        /// <returns>The bytes written.</returns>
        public int Capture(TheatreReplay replay, View view, string path, out string remark)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));

            Bounds box = BoxOf(replay, out string boxNote);

            // What the camera is fitted to, which is the whole water for every view but the close
            // one. The sky view is fitted to nothing at all and stands where it stands, but it is
            // handed the box too, because the fog below is spanned across whatever is passed here.
            Bounds framed = view == View.Close ? CloseOn(replay, box, ref boxNote) : box;

            Frame(view, framed);

            _camera.backgroundColor = Water;

            List<WaterBounds> silenced = SilenceTheWater();
            List<Renderer> hidden = HideWhatOnlyTheWaterSees(view);

            // The fog is framed on what is being looked at rather than on the box. In the close
            // view that is the point: a few metres of water put the rest of the world into the
            // background where it belongs, which is the dark field arrangement done with depth.
            Fog saved = FrameTheFog(framed);

            try
            {
                _camera.Render();
            }
            finally
            {
                saved.Restore();
                for (int i = 0; i < silenced.Count; i++) silenced[i].enabled = true;
                for (int i = 0; i < hidden.Count; i++) hidden[i].enabled = true;
            }

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _target;

            try
            {
                _readback.ReadPixels(new Rect(0f, 0f, _width, _height), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = active;
            }

            _pixels = _readback.GetPixels32();

            // Neither the close view nor the sky view carries the box. The close view's frame cuts
            // the water's edges at odd angles, and the sky view stands inside the box looking up,
            // where the wireframe would be stamped straight across the window.
            if (view != View.Close && view != View.Sky) DrawBox(box, replay);

            DrawBodies(replay, view, out int marked, out int outside, out int bodies);
            DrawLabel(Label(replay, view));

            _readback.SetPixels32(_pixels);
            _readback.Apply(false);

            byte[] png = _readback.EncodeToPNG();

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, png);

            remark =
                bodies + " bodies, " + marked + " too small to see and marked at " +
                MarkerPixels + " px, " + outside + " outside the frame" +
                (boxNote != null ? "; " + boxNote : "");

            return png.Length;
        }

        /// <summary>What the render settings' fog was, so one render can borrow them.</summary>
        private struct Fog
        {
            public bool On;
            public FogMode Mode;
            public Color Colour;
            public float Density;
            public float Start;
            public float End;

            public static Fog Save() => new Fog
            {
                On = RenderSettings.fog,
                Mode = RenderSettings.fogMode,
                Colour = RenderSettings.fogColor,
                Density = RenderSettings.fogDensity,
                Start = RenderSettings.fogStartDistance,
                End = RenderSettings.fogEndDistance,
            };

            public void Restore()
            {
                RenderSettings.fog = On;
                RenderSettings.fogMode = Mode;
                RenderSettings.fogColor = Colour;
                RenderSettings.fogDensity = Density;
                RenderSettings.fogStartDistance = Start;
                RenderSettings.fogEndDistance = End;
            }
        }

        /// <summary>
        /// Rescales the fog to the span this view is looking through, for the length of one
        /// render, and hands back what it changed.
        /// </summary>
        /// <remarks>
        /// <b>Why the picture does not simply use the Play mode fog.</b>
        /// <see cref="TheatreSkin"/> sets an exponential squared fog tuned for a person flying
        /// inside the water, where twenty metres should be dim. Three of these four views are
        /// orthographic, and an orthographic camera stands off the whole box before it starts:
        /// the top view of the campaign's sixty metre column looks through a hundred and twenty
        /// metres of it, at which that density is black. The first constraint on these pictures is
        /// that they can be read, so the fog here is linear and spanned across the box: it starts
        /// at the near face and reaches a little past the far one, which leaves about a third of a
        /// body's light at the back wall whatever the box's size. The look is the same look; only
        /// the depth it is measured over follows the frame.
        /// </remarks>
        private Fog FrameTheFog(Bounds box)
        {
            Fog saved = Fog.Save();

            if (!RenderSettings.fog) return saved;

            Vector3 eye = _camera.transform.position;
            Vector3 forward = _camera.transform.forward;
            Vector3 half = 0.5f * box.size;

            float near = float.MaxValue;
            float far = float.MinValue;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 corner = box.center +
                            new Vector3(sx * half.x, sy * half.y, sz * half.z);

                        float along = Vector3.Dot(corner - eye, forward);

                        near = Mathf.Min(near, along);
                        far = Mathf.Max(far, along);
                    }
                }
            }

            near = Mathf.Max(0f, near);
            float span = Mathf.Max(0.5f, far - near);

            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = near;

            // 1.54 spans is where a linear fog leaves 35% of the light at the far wall, which is
            // "dim but present" without turning the back of the box into background.
            RenderSettings.fogEndDistance = near + 1.54f * span;

            return saved;
        }

        /// <summary>
        /// Turns the scene's own water off for the length of one render, and hands back what it
        /// turned off.
        /// </summary>
        /// <remarks>
        /// <b>The incident.</b> The first eight pictures this class ever made each carried a
        /// second, smaller box floating inside the real one, with its own grid and its own patch
        /// seams, about a fifteenth of the size and off to one side.
        /// <see cref="WaterBounds"/> draws the box in immediate mode from
        /// <c>OnRenderObject</c>, which fires once per camera, and <c>GL</c> in that callback
        /// takes its matrices from whatever the render state holds rather than from the camera
        /// the frame is being drawn for; under a scriptable pipeline that was the runner's fly
        /// camera, sitting a few metres off one corner. So the water drew a perspective view of
        /// the box into an orthographic picture of the same box. It is off during the render for
        /// that reason, and it costs nothing: this class composites the box, the floor and the
        /// seams itself, from the config, in <see cref="DrawBox"/>.
        /// </remarks>
        private static List<WaterBounds> SilenceTheWater()
        {
            var silenced = new List<WaterBounds>(2);

            foreach (WaterBounds water in
                     UnityEngine.Object.FindObjectsByType<WaterBounds>(FindObjectsSortMode.None))
            {
                if (water == null || !water.enabled) continue;

                water.enabled = false;
                silenced.Add(water);
            }

            return silenced;
        }

        /// <summary>
        /// Turns off the furniture that only exists to be seen from inside the water, for the
        /// length of one render, and hands back what it turned off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What and why.</b> The fourth day put a rippling surface on the world's ceiling and a
        /// handful of light shafts under it (logbook/specs/skin-spec-4.md), and said in as many
        /// words that the views taken from outside the box must be unchanged by it: the top view
        /// still sees the world, and nothing new stands in front of the box in the side, the end
        /// or the iso. The shaders refuse any eye above the waterline by themselves, which covers
        /// the top and the iso, but the side and the end cameras stand under the water looking in,
        /// where a shaft is a bright slab straight across the census.
        /// </para>
        /// <para>
        /// <b>Done by camera rather than by geometry, deliberately.</b> The obvious rule, draw
        /// them only for an eye inside the box, would also have taken the sea away from the Play
        /// mode viewer, whose camera starts twenty six metres outside it
        /// (<c>TheatreSceneBuilder</c>). What separates a census picture from a viewer is which
        /// camera is rendering, and that is known here and nowhere else.
        /// </para>
        /// <para>
        /// The close and the sky views keep it. Sky is a picture of the surface and would be an
        /// empty frame without it, and close is a portrait taken inside the water where the light
        /// falling on a body is the subject.
        /// </para>
        /// </remarks>
        private static List<Renderer> HideWhatOnlyTheWaterSees(View view)
        {
            var hidden = new List<Renderer>(2);

            if (view == View.Close || view == View.Sky) return hidden;

            foreach (TheatreInsideOnly mark in
                     UnityEngine.Object.FindObjectsByType<TheatreInsideOnly>(FindObjectsSortMode.None))
            {
                if (mark == null) continue;

                var renderer = mark.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled) continue;

                renderer.enabled = false;
                hidden.Add(renderer);
            }

            return hidden;
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// The water the run was simulated in, as a box in world coordinates.
        /// </summary>
        /// <remarks>
        /// D077's shared volume is a literal box and the theatre already draws it that way
        /// (<see cref="WaterBounds.ShowBox"/>), from the same three numbers: K patches of
        /// <c>sqrt(area / K)</c>, laid out <c>K/A</c> along x by <c>A</c> across z
        /// (fable-propose-box.md). A tiled recording has no box at all, only a lattice a hundred
        /// metres apart in otherwise empty space, so there the frame is four tiles of it and the
        /// remark says the picture is of a lattice.
        ///
        /// <para>
        /// <b>A tank is framed by its bounding square</b> — <c>[0, 2R)²</c> about an axis at
        /// <c>(R, R)</c>, <c>TankGeometry</c>, <c>logbook/specs/tank-spec.md</c>. A camera fitted
        /// to the circle's own bounding box is fitted to the circle, since the two touch on all
        /// four sides; the corners cost a few percent of the frame and buy the whole of the rest
        /// of the theatre keeping one rectangle to reason about — the skin's bed and surface, the
        /// snow's volume and the close view's neighbourhood all read these bounds.
        /// </para>
        /// </remarks>
        public static Bounds BoxOf(TheatreReplay replay, out string note)
        {
            note = null;
            RunConfig config = replay.Record.Config;
            float depth = Mathf.Max(0.1f, config.WorldDepthMetres);

            if (config.SharedSpace && config.WorldShape == WorldShape.Tank)
            {
                float side = 2f * TankGeometry.RadiusFor(config.WorldAreaSquareMetres);

                return new Bounds(
                    new Vector3(0.5f * side, -0.5f * depth, 0.5f * side),
                    new Vector3(side, depth, side));
            }

            if (config.SharedSpace)
            {
                int patches = Mathf.Max(1, (int)config.HorizontalPatches);
                int across = Mathf.Clamp((int)config.PatchesAcross, 1, patches);
                float patchMetres = Mathf.Max(0.1f, Mathf.Sqrt(config.WorldAreaSquareMetres / patches));
                float length = patchMetres * Mathf.Max(1, patches / across);
                float width = patchMetres * across;

                return new Bounds(
                    new Vector3(0.5f * length, -0.5f * depth, 0.5f * width),
                    new Vector3(length, depth, width));
            }

            float reach = 2f * Ecosystem.TileSpacing;
            note = "a tiled recording has no box, so the frame is four tile spacings of lattice";

            return new Bounds(new Vector3(0f, -0.5f * depth, 0f), new Vector3(reach, depth, reach));
        }

        /// <summary>
        /// The water that holds the six largest bodies, as the box <see cref="View.Close"/> is
        /// fitted to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sized by <see cref="Radius"/>, the same reach the marker rule measures a body by, so
        /// that "largest" means the same thing in both places. Each body contributes its own
        /// reach on every axis, so a long creature lying across the frame is inside it and not
        /// cut in half by its own selection.
        /// </para>
        /// <para>
        /// <b>The largest body, and then its largest neighbours.</b> Asking for the six largest
        /// in the world frames the whole world whenever two of them are in different columns,
        /// which is what r35-s1 did (see <see cref="View.Close"/>). The neighbourhood is set from
        /// the anchor's own reach rather than in metres, so the frame holds the same fraction of
        /// a body whether the world's creatures are centimetres or metres across.
        /// </para>
        /// <para>
        /// <b>What is given up, and it should be said.</b> The five neighbours are the largest
        /// near the largest, so a body of a different guild or a different size class in the next
        /// column over is not in the picture and cannot be read out of it. This view is a
        /// portrait of one crowd and never a census.
        /// </para>
        /// </remarks>
        private static Bounds CloseOn(TheatreReplay replay, Bounds box, ref string note)
        {
            IReadOnlyList<Organism> living = replay.Eco.World.Living;

            Organism anchor = null;
            float anchorReach = 0f;

            for (int i = 0; i < living.Count; i++)
            {
                var at = new Vector3(living[i].X, living[i].HeightY, living[i].Z);
                if (!IsFinite(at)) continue;

                float r = Radius(living[i].Phenotype);
                if (anchor != null && r <= anchorReach) continue;

                anchor = living[i];
                anchorReach = r;
            }

            if (anchor == null)
            {
                note = Join(note, "nothing was alive to frame, so the close view is of the box");
                return box;
            }

            // How far out the neighbours may be. Six reaches of the anchor is a frame that holds
            // the anchor across about a sixth of its width, which is a body of a couple of
            // hundred pixels at 1600 across; the metre floor keeps a frame of that shape when
            // every body in the world is small.
            float around = Mathf.Max(1.2f, 6f * anchorReach);
            var centre = new Vector3(anchor.X, anchor.HeightY, anchor.Z);

            var chosen = new List<Organism>(CloseBodies) { anchor };
            var reach = new List<float>(CloseBodies) { anchorReach };

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (creature == anchor) continue;

                var at = new Vector3(creature.X, creature.HeightY, creature.Z);
                if (!IsFinite(at)) continue;
                if ((at - centre).sqrMagnitude > around * around) continue;

                float r = Radius(creature.Phenotype);

                int slot = chosen.Count;
                while (slot > 1 && reach[slot - 1] < r) slot--;

                if (slot >= CloseBodies) continue;

                chosen.Insert(slot, creature);
                reach.Insert(slot, r);

                if (chosen.Count > CloseBodies)
                {
                    chosen.RemoveAt(CloseBodies);
                    reach.RemoveAt(CloseBodies);
                }
            }

            Bounds framed = default;

            for (int i = 0; i < chosen.Count; i++)
            {
                var at = new Vector3(chosen[i].X, chosen[i].HeightY, chosen[i].Z);
                var size = 2.4f * reach[i] * Vector3.one;

                if (i == 0) framed = new Bounds(at, size);
                else framed.Encapsulate(new Bounds(at, size));
            }

            note = Join(
                note,
                "close on " + chosen.Count + " bodies within " +
                around.ToString("0.##", CultureInfo.InvariantCulture) + " m of the largest, of reach " +
                Least(reach).ToString("0.###", CultureInfo.InvariantCulture) + " to " +
                anchorReach.ToString("0.###", CultureInfo.InvariantCulture) + " m, framed over " +
                Metres(framed.size) + "; " + Ellipsoids(replay));

            return framed;
        }

        private static float Least(List<float> values)
        {
            float worst = float.MaxValue;
            for (int i = 0; i < values.Count; i++) worst = Mathf.Min(worst, values[i]);

            return values.Count == 0 ? 0f : worst;
        }

        private static string Join(string first, string second) =>
            string.IsNullOrEmpty(first) ? second : first + "; " + second;

        private static string Metres(Vector3 size) => string.Format(
            CultureInfo.InvariantCulture, "{0:0.##} by {1:0.##} by {2:0.##} m",
            size.x, size.y, size.z);

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        /// <summary>
        /// How far from round the world's round parts are, as the theatre draws them.
        /// </summary>
        /// <remarks>
        /// The theatre draws a sphere part as an ellipsoid of the genome's three half-extents
        /// (<c>TheatrePalette.Aspect</c>), because the simulation reduced them to their mean and
        /// collides as a ball. Whether that shows in a picture depends entirely on how far from
        /// equal a genome's three numbers are, and that is a fact about the run rather than about
        /// the skin, so it is measured here and printed with the picture instead of being assumed
        /// either way. The ratio is the smallest half-extent over the largest: one is a ball.
        /// </remarks>
        private static string Ellipsoids(TheatreReplay replay)
        {
            IReadOnlyList<Organism> living = replay.Eco.World.Living;

            int round = 0;
            double total = 0.0;
            float flattest = 1f;

            for (int i = 0; i < living.Count; i++)
            {
                Phenotype phenotype = living[i].Phenotype;
                if (phenotype == null) continue;

                foreach (PhenotypePart part in phenotype.Parts)
                {
                    bool sphere = part.ShapeId == ShapeIds.Sphere;
                    bool capsule = part.ShapeId == ShapeIds.Capsule;

                    if (!sphere && !capsule) continue;

                    Float3 h = part.HalfExtents;

                    float hx = Mathf.Abs(h.X), hy = Mathf.Abs(h.Y), hz = Mathf.Abs(h.Z);

                    // A capsule's length is a dimension the shape really has; only its cross
                    // section was averaged away, so only X and Z are asked about.
                    float low = sphere ? Mathf.Min(hx, Mathf.Min(hy, hz)) : Mathf.Min(hx, hz);
                    float high = sphere ? Mathf.Max(hx, Mathf.Max(hy, hz)) : Mathf.Max(hx, hz);

                    if (high <= 0f) continue;

                    float ratio = low / high;

                    round++;
                    total += ratio;
                    flattest = Mathf.Min(flattest, ratio);
                }
            }

            if (round == 0) return "no sphere or capsule part is alive, so nothing is drawn as an ellipsoid";

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} round parts alive, short over long axis {1:0.###} on average and {2:0.###} at the flattest",
                round, total / round, flattest);
        }

        /// <summary>Points the camera and sizes it so the whole box is inside the picture.</summary>
        /// <remarks>
        /// Fitted from the box's eight corners rather than from its diagonal: the diagonal is a
        /// bound and not the frame, and on a box 20 by 5 by 60 it would waste a third of the
        /// picture. The margin is the only slack, and it is small enough that the box's edges are
        /// visible as edges.
        /// </remarks>
        private void Frame(View view, Bounds box)
        {
            const float margin = 1.06f;

            // The sky view is not fitted to anything: it stands in the water rather than outside
            // it, so the corner arithmetic below, which asks how far back a camera has to stand to
            // hold the whole box, has no answer for it. Placed and returned here instead.
            if (view == View.Sky)
            {
                FrameTheSky(box);
                return;
            }

            Quaternion rotation;
            bool perspective = false;

            switch (view)
            {
                case View.Side:
                    // Along +z from the near face: +x runs to the right, so the box's length is
                    // the picture's width and depth is its height.
                    rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    break;

                case View.End:
                    rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                    break;

                case View.Top:
                    // Up is +z, so the picture reads like a map: length across, width up.
                    rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    break;

                case View.Close:
                    // A shallower three quarters than the iso view's, and a longer lens. Looking
                    // steeply down on a body a metre away puts most of it in its own shadow and
                    // spreads the near end of it across the frame; from nearer the horizon the
                    // key rakes across the carve, which is what a carve has to be seen by.
                    rotation = Quaternion.LookRotation(
                        new Vector3(-0.78f, -0.34f, 1f).normalized, Vector3.up);
                    perspective = true;
                    break;

                default:
                    rotation = Quaternion.LookRotation(
                        new Vector3(-0.85f, -0.55f, 1f).normalized, Vector3.up);
                    perspective = true;
                    break;
            }

            Vector3 centre = box.center;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 half = 0.5f * box.size;

            float aspect = (float)_width / _height;
            float reachX = 0f, reachY = 0f, reachZ = 0f;

            var corners = new Vector3[8];
            int n = 0;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 corner = new Vector3(sx * half.x, sy * half.y, sz * half.z);
                        corners[n++] = inverse * corner;

                        reachX = Mathf.Max(reachX, Mathf.Abs(corners[n - 1].x));
                        reachY = Mathf.Max(reachY, Mathf.Abs(corners[n - 1].y));
                        reachZ = Mathf.Max(reachZ, Mathf.Abs(corners[n - 1].z));
                    }
                }
            }

            _camera.orthographic = !perspective;
            _camera.aspect = aspect;

            // The label is burnt into the top of the picture, and the surface of the water is the
            // top of the box: fitted edge to edge, the first thing the label hid was the one line
            // that says which end is up. So the vertical fit is squeezed into the band left
            // between a label's height at the top and the same at the bottom, which keeps the box
            // centred and its surface line clear of the text.
            float clear = 1f - 2f * (BarHeight + LabelScale) / (float)_height;
            clear = Mathf.Clamp(clear, 0.25f, 1f);

            float standoff;

            if (!perspective)
            {
                _camera.orthographicSize = margin * Mathf.Max(reachY / clear, reachX / aspect);
                standoff = reachZ + Mathf.Max(10f, reachZ);
            }
            else
            {
                _camera.fieldOfView = view == View.Close ? 28f : 34f;

                float tanV = Mathf.Tan(0.5f * _camera.fieldOfView * Mathf.Deg2Rad);
                float tanH = tanV * aspect;
                float need = 0f;

                foreach (Vector3 c in corners)
                {
                    need = Mathf.Max(need, Mathf.Abs(c.y) / (tanV * clear) - c.z);
                    need = Mathf.Max(need, Mathf.Abs(c.x) / tanH - c.z);
                }

                standoff = margin * Mathf.Max(need, reachZ + 1f);
            }

            Vector3 forward = rotation * Vector3.forward;
            _camera.transform.SetPositionAndRotation(centre - forward * standoff, rotation);
            _camera.nearClipPlane = Mathf.Max(0.05f, 0.001f * standoff);
            _camera.farClipPlane = standoff + 2f * reachZ + 200f;
        }

        /// <summary>Stands the camera in the water under the surface and points it up at the sun.</summary>
        /// <remarks>
        /// <para>
        /// <b>The bearing comes from the sun the shaders draw.</b>
        /// <see cref="TheatreSkin.PushWater"/> sets <c>_EvoSun</c> from the scene's directional
        /// light, and the surface's window and the shafts are both built on it, so reading the
        /// same global here is the only way to be sure the camera is looking where the sun
        /// actually is. A scene that never dressed itself leaves the global at zero, and the
        /// picture then looks along the box's length, which is at least a view of the water.
        /// </para>
        /// <para>
        /// <b>The depth is bounded by the box.</b> Three metres under is the intent, but a run in
        /// a shallower world would put the camera under its own floor, so it is at most half the
        /// water's depth.
        /// </para>
        /// </remarks>
        private void FrameTheSky(Bounds box)
        {
            float depth = Mathf.Min(SkyDepthMetres, 0.5f * box.size.y);

            var eye = new Vector3(box.center.x, box.max.y - depth, box.center.z);

            Vector4 sun = Shader.GetGlobalVector("_EvoSun");
            var bearing = new Vector3(sun.x, 0f, sun.z);

            if (bearing.sqrMagnitude < 1e-6f) bearing = Vector3.right;
            bearing = bearing.normalized;

            Vector3 forward =
                (bearing + Vector3.up * Mathf.Tan(SkyRiseDegrees * Mathf.Deg2Rad)).normalized;

            _camera.orthographic = false;
            _camera.aspect = (float)_width / _height;
            _camera.fieldOfView = SkyFieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 4f * box.size.magnitude + 200f;

            _camera.transform.SetPositionAndRotation(
                eye, Quaternion.LookRotation(forward, Vector3.up));
        }

        // ---------------------------------------------------------------- what is stamped

        /// <summary>
        /// The box's twelve edges and its patch seams, drawn into the pixels.
        /// </summary>
        /// <remarks>
        /// Composited rather than rendered so that the frame reads whatever happened in the
        /// scene: <see cref="WaterBounds"/> draws the same box in immediate mode from
        /// <c>OnRenderObject</c>, and if that callback ever stops reaching a camera made here the
        /// picture would be bodies in a void with no scale, no floor and no seams, which is
        /// exactly the picture that cannot be read. Drawn under the markers and over the render,
        /// so an edge crosses in front of a body it passes; at one pixel wide that costs nothing
        /// and it keeps the box's corner visible where a body sits in it.
        /// </remarks>
        private void DrawBox(Bounds box, TheatreReplay replay)
        {
            Vector3 lo = box.min;
            Vector3 hi = box.max;

            Color surface = new Color(0.45f, 0.75f, 0.95f, 1f);
            Color floor = new Color(0.62f, 0.50f, 0.33f, 1f);
            Color vertical = new Color(0.30f, 0.48f, 0.62f, 1f);
            Color seam = new Color(0.95f, 0.85f, 0.45f, 1f);

            RunConfig water = replay.Record.Config;

            // fable-propose-aquarium.md ruling 1. The frame is the bounding square either way
            // (BoxOf), but what is stamped into the pixels is the water: a tank's outline is the
            // circle and its patches are rings, and drawing the square would put a wall through
            // four corners of open frame and invite a reader to measure a body against it.
            if (water.SharedSpace && water.WorldShape == WorldShape.Tank)
            {
                DrawTank(box, water, surface, floor, vertical, seam);
                return;
            }

            // The surface rectangle at y = 0 and the floor rectangle at y = -depth, in their own
            // colours: which is which is the one thing a still picture of a water column must
            // never leave ambiguous.
            Rectangle(hi.y, lo, hi, surface);
            Rectangle(lo.y, lo, hi, floor);

            Line(new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, hi.y, lo.z), vertical);
            Line(new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, hi.y, lo.z), vertical);
            Line(new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, hi.z), vertical);
            Line(new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), vertical);

            RunConfig config = replay.Record.Config;
            if (!config.SharedSpace) return;

            // D077's seams: under a shared volume a patch is a region a body is in and crosses,
            // so the lines between them are geometry the run has, not an index drawn as if it
            // were a place. Both axes, since fable-propose-box.md; at A = 1 the second loop runs
            // no times and this is the picture every recording before the layout was drawn as.
            int patches = Mathf.Max(1, (int)config.HorizontalPatches);
            int across = Mathf.Clamp((int)config.PatchesAcross, 1, patches);
            int along = Mathf.Max(1, patches / across);
            float width = (hi.x - lo.x) / along;
            float depthPerPatch = (hi.z - lo.z) / across;

            for (int k = 1; k < along; k++)
            {
                float x = lo.x + k * width;

                Line(new Vector3(x, lo.y, lo.z), new Vector3(x, hi.y, lo.z), seam);
                Line(new Vector3(x, lo.y, hi.z), new Vector3(x, hi.y, hi.z), seam);
                Line(new Vector3(x, hi.y, lo.z), new Vector3(x, hi.y, hi.z), seam);
                Line(new Vector3(x, lo.y, lo.z), new Vector3(x, lo.y, hi.z), seam);
            }

            for (int k = 1; k < across; k++)
            {
                float z = lo.z + k * depthPerPatch;

                Line(new Vector3(lo.x, lo.y, z), new Vector3(lo.x, hi.y, z), seam);
                Line(new Vector3(hi.x, lo.y, z), new Vector3(hi.x, hi.y, z), seam);
                Line(new Vector3(lo.x, hi.y, z), new Vector3(hi.x, hi.y, z), seam);
                Line(new Vector3(lo.x, lo.y, z), new Vector3(hi.x, lo.y, z), seam);
            }
        }

        /// <summary>
        /// The tank's two circles, its eight verticals and its patch rings, drawn into the pixels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The same picture <see cref="WaterBounds.ShowTank"/> draws in the scene</b>, for the
        /// reason <see cref="DrawBox"/> composites the box: the immediate-mode draw is one
        /// callback away from not reaching a camera made here, and a census view with no water in
        /// it cannot be read. The radius is the config's own <c>sqrt(area/π)</c>
        /// (<c>TankGeometry</c>) rather than half the frame, so a frame later widened for any
        /// reason would still draw the glass where the glass is.
        /// </para>
        /// <para>
        /// <b>Rings, not seams.</b> A tank's patches are annuli of equal area
        /// (<c>logbook/specs/tank-spec.md</c>), so the boundaries are circles at
        /// <c>R·sqrt(k/K)</c> and the outermost of them is the glass, already drawn.
        /// </para>
        /// </remarks>
        private void DrawTank(
            Bounds box, RunConfig water, Color surface, Color floor, Color vertical, Color seam)
        {
            float radius = TankGeometry.RadiusFor(water.WorldAreaSquareMetres);
            var axis = new Vector3(radius, 0f, radius);

            float top = box.max.y;
            float bottom = box.min.y;

            Circle(top, axis, radius, surface);
            Circle(bottom, axis, radius, floor);

            for (int i = 0; i < 8; i++)
            {
                float angle = 2f * Mathf.PI * i / 8f;
                float x = axis.x + radius * Mathf.Cos(angle);
                float z = axis.z + radius * Mathf.Sin(angle);

                Line(new Vector3(x, bottom, z), new Vector3(x, top, z), vertical);
            }

            int rings = Mathf.Max(1, (int)water.HorizontalPatches);

            for (int k = 1; k < rings; k++)
            {
                float r = radius * Mathf.Sqrt(k / (float)rings);

                Circle(top, axis, r, seam);
                Circle(bottom, axis, r, seam);
            }
        }

        /// <summary>One horizontal circle, as a closed polyline of <see cref="CircleSegments"/> chords.</summary>
        private void Circle(float y, Vector3 axis, float radius, Color colour)
        {
            if (!(radius > 0f)) return;

            var previous = new Vector3(axis.x + radius, y, axis.z);

            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / CircleSegments;
                var next = new Vector3(
                    axis.x + radius * Mathf.Cos(angle), y, axis.z + radius * Mathf.Sin(angle));

                Line(previous, next, colour);
                previous = next;
            }
        }

        private void Rectangle(float y, Vector3 lo, Vector3 hi, Color colour)
        {
            var a = new Vector3(lo.x, y, lo.z);
            var b = new Vector3(hi.x, y, lo.z);
            var c = new Vector3(hi.x, y, hi.z);
            var d = new Vector3(lo.x, y, hi.z);

            Line(a, b, colour);
            Line(b, c, colour);
            Line(c, d, colour);
            Line(d, a, colour);
        }

        /// <summary>One marker per living body, where the mesh is too small to be one.</summary>
        /// <remarks>
        /// The position is the creature's own <c>X</c>, <c>HeightY</c> and <c>Z</c>, which the
        /// harness sets from its centre of mass at every metabolic step (D083's
        /// <c>World.Observe</c>). That is the world's own reading of where a body is, so the
        /// picture and the report cannot disagree about it; going through the scene instead would
        /// need the id map, which can go unreliable, and would have put a second answer to the
        /// same question into the picture.
        /// </remarks>
        private void DrawBodies(
            TheatreReplay replay, View view, out int marked, out int outside, out int bodies)
        {
            marked = 0;
            outside = 0;
            bodies = 0;

            // A marker stands in for a body too small to draw. The close view exists to show
            // what a body looks like, so a square painted over one would answer its own question,
            // and the sky view stands inside the water at a few metres, where a body is tens of
            // pixels across and a marker would only hide the light falling on it.
            bool marking = view != View.Close && view != View.Sky;

            IReadOnlyList<Organism> living = replay.Eco.World.Living;
            float tanV = Mathf.Tan(0.5f * _camera.fieldOfView * Mathf.Deg2Rad);

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                bodies++;

                var world = new Vector3(creature.X, creature.HeightY, creature.Z);

                if (float.IsNaN(world.x) || float.IsNaN(world.y) || float.IsNaN(world.z) ||
                    float.IsInfinity(world.x) || float.IsInfinity(world.y) ||
                    float.IsInfinity(world.z))
                {
                    outside++;
                    continue;
                }

                Vector3 screen = _camera.WorldToScreenPoint(world);

                if (screen.z <= 0f ||
                    screen.x < 0f || screen.x >= _width ||
                    screen.y < 0f || screen.y >= _height)
                {
                    outside++;
                    continue;
                }

                float pixelsPerMetre = _camera.orthographic
                    ? _height / (2f * _camera.orthographicSize)
                    : _height / (2f * Mathf.Max(0.01f, screen.z) * tanV);

                if (2f * Radius(creature.Phenotype) * pixelsPerMetre >= MinimumBodyPixels) continue;
                if (!marking) continue;

                marked++;
                Marker((int)screen.x, (int)screen.y, ColourOf(creature));
            }
        }

        /// <summary>
        /// Which guild a body is marked as.
        /// </summary>
        /// <remarks>
        /// One marker is one body and a body can be both, so a mixotroph reads as a stomach: what
        /// the picture is asked for is who eats whom, and an absorptive part is the whole of that
        /// question. The same three colours <see cref="TheatrePalette"/> paints parts in, so a
        /// marked body and a rendered one do not disagree.
        /// </remarks>
        private Color ColourOf(Organism creature) =>
            creature.HasAbsorptiveTissue ? Absorptive :
            creature.HasPhotosyntheticTissue ? Photosynthetic : Structural;

        /// <summary>The furthest a part reaches from the body's origin. TheatreRunner's rule.</summary>
        private static float Radius(Phenotype phenotype)
        {
            float worst = 0.05f;
            if (phenotype == null) return worst;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                Float3 h = part.HalfExtents;
                float reach = Mathf.Sqrt(Float3.Dot(part.Position, part.Position)) +
                              Mathf.Max(Mathf.Abs(h.X), Mathf.Max(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));

                worst = Mathf.Max(worst, reach);
            }

            return worst;
        }

        /// <summary>The one line the picture carries: which world, when, how many, which way.</summary>
        /// <remarks>
        /// The faithfulness of the replay is on the same line rather than left to the log,
        /// because a picture travels without its log. A run this build did not record is a cousin
        /// of that run and not that run (D078, logbook/0052), and a still frame of a cousin
        /// labelled with the arm's name would be the most quietly misleading artefact this
        /// project could make.
        /// </remarks>
        private static string Label(TheatreReplay replay, View view)
        {
            string arm = replay.Record.ArmName ?? "run";
            string faithful = replay.Faithful && replay.ThreadCaveat == null
                ? ""
                : "  NOT A FAITHFUL REPLAY";

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}  t={1:0.#}s  alive {2}  {3}{4}",
                arm, replay.Census.T, replay.Census.Alive, NameOf(view), faithful);
        }

        // ---------------------------------------------------------------- pixels

        private void Marker(int x, int y, Color colour)
        {
            int half = MarkerPixels / 2;

            // The dark surround first, so a marker on pale water and a marker on a lit body both
            // read as a marker rather than as a bright patch of whatever is behind it.
            Fill(x - half - 1, y - half - 1, MarkerPixels + 2, MarkerPixels + 2,
                new Color32(8, 10, 12, 255));

            Fill(x - half, y - half, MarkerPixels, MarkerPixels, colour);
        }

        private void Fill(int x, int y, int width, int height, Color32 colour)
        {
            int x1 = Mathf.Min(_width - 1, x + width - 1);
            int y1 = Mathf.Min(_height - 1, y + height - 1);

            for (int py = Mathf.Max(0, y); py <= y1; py++)
            {
                int row = py * _width;
                for (int px = Mathf.Max(0, x); px <= x1; px++) _pixels[row + px] = colour;
            }
        }

        /// <summary>A world-space segment, projected and drawn one pixel wide.</summary>
        private void Line(Vector3 from, Vector3 to, Color32 colour)
        {
            Vector3 a = _camera.WorldToScreenPoint(from);
            Vector3 b = _camera.WorldToScreenPoint(to);

            // Behind the camera the projection folds the point through the origin, which draws a
            // line across the whole picture that is not in the world. Dropped rather than
            // clipped: every view here stands outside the box looking at it, so this is the case
            // that should not arise and must not be drawn if it does.
            if (a.z <= 0f || b.z <= 0f) return;

            int x0 = Mathf.RoundToInt(a.x), y0 = Mathf.RoundToInt(a.y);
            int x1 = Mathf.RoundToInt(b.x), y1 = Mathf.RoundToInt(b.y);

            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            // Bresenham, with a step cap: a projected segment can be enormous, and a picture is
            // never worth an unbounded loop.
            for (int guard = 0; guard < 4 * (_width + _height); guard++)
            {
                if (x0 >= 0 && x0 < _width && y0 >= 0 && y0 < _height)
                {
                    _pixels[y0 * _width + x0] = colour;
                }

                if (x0 == x1 && y0 == y1) return;

                int twice = 2 * error;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        /// <summary>How tall the label's bar is. Read by the frame, which keeps clear of it.</summary>
        private static int BarHeight => 7 * LabelScale + 4 * LabelScale;

        private void DrawLabel(string text)
        {
            string upper = text.ToUpperInvariant();

            int cell = LabelScale;
            int pad = 2 * cell;
            int textWidth = upper.Length * 6 * cell - cell;

            int barWidth = Mathf.Min(_width, textWidth + 2 * pad);
            int barHeight = BarHeight;
            int barX = 0;
            int barY = _height - barHeight;

            Fill(barX, barY, barWidth, barHeight, new Color32(0, 0, 0, 255));

            int x = barX + pad;
            int top = _height - pad - 1;

            foreach (char c in upper)
            {
                Glyph(c, x, top, cell);
                x += 6 * cell;
                if (x > _width) break;
            }
        }

        private void Glyph(char c, int x, int top, int cell)
        {
            string rows = Font(c);
            var ink = new Color32(255, 255, 255, 255);

            for (int r = 0; r < 7; r++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if (rows[r * 5 + column] != '#') continue;

                    Fill(x + column * cell, top - (r + 1) * cell + 1, cell, cell, ink);
                }
            }
        }

        // ---------------------------------------------------------------- the font

        /// <summary>
        /// A five by seven bitmap font, as thirty-five characters per glyph.
        /// </summary>
        /// <remarks>
        /// Written out here rather than taken from a font asset because a picture's label must
        /// not depend on an asset importing, a shader resolving or a render pipeline being the
        /// one somebody expected. Seven rows of five, top row first, <c>#</c> for ink. Anything
        /// this table does not have prints as a hollow box, which is visible in the picture, and
        /// silence would not be.
        /// </remarks>
        private static readonly Dictionary<char, string> Glyphs = Build();

        private static string Font(char c) =>
            Glyphs.TryGetValue(c, out string rows) ? rows : Glyphs['\0'];

        private static Dictionary<char, string> Build()
        {
            var font = new Dictionary<char, string>(64);

            void Add(char c, string a, string b, string d, string e, string f, string g, string h)
                => font[c] = a + b + d + e + f + g + h;

            Add('\0', "#####", "#...#", "#...#", "#...#", "#...#", "#...#", "#####");
            Add(' ', ".....", ".....", ".....", ".....", ".....", ".....", ".....");

            Add('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
            Add('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
            Add('C', ".####", "#....", "#....", "#....", "#....", "#....", ".####");
            Add('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
            Add('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
            Add('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
            Add('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.");
            Add('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
            Add('I', ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###.");
            Add('J', "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##..");
            Add('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
            Add('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
            Add('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
            Add('N', "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#");
            Add('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
            Add('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
            Add('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
            Add('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
            Add('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
            Add('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
            Add('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
            Add('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
            Add('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
            Add('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
            Add('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
            Add('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");

            Add('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
            Add('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
            Add('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
            Add('3', "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###.");
            Add('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
            Add('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
            Add('6', "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###.");
            Add('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
            Add('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
            Add('9', ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##..");

            Add('.', ".....", ".....", ".....", ".....", ".....", ".##..", ".##..");
            Add(',', ".....", ".....", ".....", ".....", ".##..", ".##..", ".#...");
            Add(':', ".....", ".##..", ".##..", ".....", ".##..", ".##..", ".....");
            Add(';', ".....", ".##..", ".##..", ".....", ".##..", ".##..", ".#...");
            Add('-', ".....", ".....", ".....", "#####", ".....", ".....", ".....");
            Add('+', ".....", "..#..", "..#..", "#####", "..#..", "..#..", ".....");
            Add('=', ".....", ".....", "#####", ".....", "#####", ".....", ".....");
            Add('_', ".....", ".....", ".....", ".....", ".....", ".....", "#####");
            Add('/', "....#", "....#", "...#.", "..#..", ".#...", "#....", "#....");
            Add('(', "..##.", ".#...", "#....", "#....", "#....", ".#...", "..##.");
            Add(')', ".##..", "...#.", "....#", "....#", "....#", "...#.", ".##..");
            Add('[', ".###.", ".#...", ".#...", ".#...", ".#...", ".#...", ".###.");
            Add(']', ".###.", "...#.", "...#.", "...#.", "...#.", "...#.", ".###.");
            Add('%', "##..#", "##.#.", "..#..", ".#...", "#..##", "...##", ".....");
            Add('!', "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#..");
            Add('?', ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..");
            Add('\'', "..#..", "..#..", ".....", ".....", ".....", ".....", ".....");
            Add('*', ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", ".....");
            Add('#', ".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#.");

            return font;
        }

        public void Dispose()
        {
            if (_camera != null) _camera.targetTexture = null;

            Discard(_holder);
            Discard(_target);
            Discard(_readback);
        }

        /// <summary>Immediate outside Play mode, deferred inside it, which is what each allows.</summary>
        private static void Discard(UnityEngine.Object thing)
        {
            if (thing == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(thing);
            else UnityEngine.Object.DestroyImmediate(thing);
        }
    }
}
