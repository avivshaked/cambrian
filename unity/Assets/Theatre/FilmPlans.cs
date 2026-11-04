using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The film's camera plans and the walls they keep to, shared by <c>TheatreFilm</c> (the
    /// Editor's film entry) and the safari's director (<see cref="SafariDirector"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Moved here from <c>TheatreFilm</c> on 2026-09-23, not rewritten.</b> The safari's brief
    /// is to reuse the film's shot machinery rather than build a second camera, and the director
    /// runs in Play mode, where an Editor-only class cannot be reached. The two classes below are
    /// the film's <c>WorldBounds</c> and <c>Shot</c> unchanged in every branch the film takes. What
    /// was added is a third kind of shot, <see cref="Shot.Custom"/>, a path the director hands
    /// in, and it is the only new branch in <see cref="Shot.Pose"/>. A film therefore poses its
    /// cameras exactly as it did, which the caller checks by rendering one.
    /// </para>
    /// <para>
    /// <b>Two changes of 2026-09-24</b>, the films review's second and third items
    /// (<c>logbook/specs/theatre-review-2026-09-24.md</c>). The close shot frames one body at 1.5
    /// to 3 of its lengths through a 45 to 60 degree lens and every portrait pulls its focus to its
    /// subject at every frame (<see cref="Shot.PlanClose"/>). And a fourth film shot, the canopy,
    /// looks up through the leaves at Snell's window along a planned path, the same kind of path a
    /// safari take hands in (<see cref="Shot.PlanCanopy"/>); the director may use it for its
    /// arrival and descent. The orbit and the drift are unchanged.
    /// </para>
    /// <para>
    /// The owner's rules stand as the film wrote them (safari-spec.md §9): one move per shot,
    /// eased at both ends over a fifth; nothing faster than a body swims; the camera kept inside
    /// the glass, over the bed, under the surface and pushed off any body it would stand in; and
    /// a moving camera's eye smoothed over half a second, so a push is a glide.
    /// </para>
    /// </remarks>
    public static class FilmPlans
    {
        /// <summary>The camera's ceiling against its subject in a close shot, m/s (the owner's rule).</summary>
        public const float CloseSpeedCeiling = 0.3f;

        /// <summary>The orbit's peak speed along its arc, m/s: about what a body swims.</summary>
        public const float OrbitSpeedCeiling = 0.5f;

        /// <summary>How far above the bed and below the surface the camera keeps, m.</summary>
        public const float Clearance = 1f;

        /// <summary>How far inside the glass the camera keeps, m.</summary>
        public const float GlassClearance = 1f;

        /// <summary>
        /// How far outside a reef's rock the camera keeps, m: the signed distance of the eye to the
        /// rock (<c>ReefGeometry.SignedDistance</c>). Round 47's safari put two clips' eyes inside
        /// a stem or a cap, and half their frames were black (2026-09-24).
        /// </summary>
        public const float ReefClearance = 1.5f;

        /// <summary>The share of a strong move spent easing in, and again easing out (safari-spec §9).</summary>
        public const float EaseShare = 0.2f;

        // ------------------------------------------------------------------ the portrait's lens

        /// <summary>
        /// A portrait's lens, degrees across the frame's height: <c>EVOSIM_THEATRE_PORTRAIT_LENS</c>,
        /// 45 to 60, default 50 (the films review's third item, 2026-09-24). Until then the close
        /// shot used 28 degrees framed on a pair, and the safari's portrait took its lens from a
        /// radius of ten reaches, which stood the camera 5 to 31 m off through the fog.
        /// </summary>
        public static float PortraitLens => TheatreSkin.Dial("EVOSIM_THEATRE_PORTRAIT_LENS", 50f, 45f, 60f);

        /// <summary>
        /// How much of the frame's height a portrait's subject fills, by the longest side of its
        /// drawn box: <c>EVOSIM_THEATRE_PORTRAIT_FILL</c>, 0.3 to 0.8, default 0.4. Above about 0.65 a swimmer's
        /// portrait reaches the nearest distance, 1.5 body lengths, and goes no closer.
        /// </summary>
        public static float PortraitFill => TheatreSkin.Dial("EVOSIM_THEATRE_PORTRAIT_FILL", 0.4f, 0.3f, 0.8f);

        // portraitfrom:begin
        /// <summary>
        /// A safari portrait placed by a body point: <c>EVOSIM_THEATRE_PORTRAIT_FROM</c>, <c>mouth</c> (the first eating part's
        /// <c>MouthPoint.Local</c>) or <c>nose</c> (the first nose part's sensing tip). The eye stands on the outward axis of that
        /// point and looks at it (<c>SafariPlans.PortraitFromPoint</c>). Null, read once, when the variable is unset or says anything
        /// else, and then nothing here runs.
        /// </summary>
        public static readonly string PortraitFrom = ReadPortraitFrom();

        private static string ReadPortraitFrom()
        {
            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_PORTRAIT_FROM");
            if (string.IsNullOrWhiteSpace(text)) return null;

            text = text.Trim().ToLowerInvariant();
            // BLADE begin
            if (text == "blade") return text;
            // BLADE end
            if (text == "mouth" || text == "nose") return text;

            Debug.LogWarning("[Theatre] EVOSIM_THEATRE_PORTRAIT_FROM is '" + text + "', not mouth or nose; the portrait is the usual orbit.");
            return null;
        }

        /// <summary>
        /// How far off the point's outward axis a portrait from a body point stands, degrees: <c>EVOSIM_THEATRE_PORTRAIT_FROM_ANGLE</c>,
        /// 5 to 80, default 30, so an opening reads as a cup and not a ring.
        /// </summary>
        public static float PortraitFromAngle => TheatreSkin.Dial("EVOSIM_THEATRE_PORTRAIT_FROM_ANGLE", 30f, 5f, 80f);
        // portraitfrom:end

        /// <summary>The nearest and farthest a portrait stands from its subject's centre, in body lengths.</summary>
        public const float PortraitNearest = 1.5f, PortraitFarthest = 3f;

        /// <summary>
        /// The distance from a subject's centre at which a body <paramref name="length"/> long fills
        /// <paramref name="fill"/> of a lens's height, held between 1.5 and 3 body lengths.
        /// </summary>
        public static float PortraitDistance(float length, float fieldOfView, float fill)
        {
            length = Mathf.Max(0.05f, length);
            float fit = length / (Mathf.Max(0.05f, fill) * 2f * Mathf.Tan(0.5f * fieldOfView * Mathf.Deg2Rad));
            return Mathf.Clamp(fit, PortraitNearest * length, PortraitFarthest * length);
        }

        private static readonly List<Renderer> _renderers = new List<Renderer>();

        /// <summary>
        /// The drawn body's box in the world, from its renderers, or false when it is not drawn.
        /// A portrait looks at and focuses on this box's centre rather than on the root, which
        /// can sit at one end of its body.
        /// </summary>
        public static bool DrawnBounds(LiveWorldView view, long id, out Bounds bounds)
        {
            bounds = default;
            Transform root = view?.RootOf(id);
            if (root == null) return false;

            bool any = false;
            root.GetComponentsInChildren(false, _renderers);
            foreach (Renderer r in _renderers)
            {
                if (r == null || !r.enabled) continue;
                Bounds b = r.bounds;
                if (!Shot.Finite(b.center) || !Shot.Finite(b.size)) continue;
                if (any) bounds.Encapsulate(b);
                else { bounds = b; any = true; }
            }

            _renderers.Clear();
            return any;
        }

        /// <summary>
        /// A portrait's subject: the centre and the longest side of its drawn box, or its root and
        /// a reach's diameter when it is not drawn.
        /// </summary>
        public static void Subject(LiveWorldView view, long id, Vector3 root, float reach, out Vector3 centre, out float length)
        {
            if (DrawnBounds(view, id, out Bounds b))
            {
                centre = b.center;
                length = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            }
            else
            {
                centre = root;
                length = 2f * reach;
            }

            length = Mathf.Max(0.05f, length);
        }

        /// <summary>
        /// How many bodies other than the subject stand across the line of sight: a body whose
        /// reach, at four fifths, crosses the line between a twentieth and nine tenths of the way
        /// from the eye to what it looks at. A neighbour across the lens is the risk the films
        /// review named for close portraits, so a portrait prefers a bearing with none.
        /// </summary>
        public static int Occluders(
            List<Vector3> positions, List<float> reaches, List<long> ids, long subject, Vector3 eye, Vector3 target)
        {
            Vector3 line = target - eye;
            float length = line.magnitude;
            if (length < 1e-4f) return 0;

            Vector3 along = line / length;
            int n = 0;

            for (int i = 0; i < positions.Count; i++)
            {
                if (ids[i] == subject) continue;
                Vector3 d = positions[i] - eye;
                float t = Vector3.Dot(d, along);
                if (t < 0.05f * length || t > 0.9f * length) continue;
                float r = 0.8f * reaches[i];
                if ((d - along * t).sqrMagnitude < r * r) n++;
            }

            return n;
        }

        /// <summary>A direction from an azimuth about the vertical and an elevation above the horizontal, radians.</summary>
        public static Vector3 Direction(float azimuth, float elevation) =>
            new Vector3(Mathf.Cos(elevation) * Mathf.Cos(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(azimuth));

        // ------------------------------------------------------------------ the canopy's lens

        /// <summary>How the canopy shot moves, one move a shot: up toward the leaves, down away from them, or round under them.</summary>
        public enum CanopyMove { Rise, Sink, Turn }

        /// <summary>The canopy shot's lens, degrees across the frame's height: <c>EVOSIM_THEATRE_CANOPY_LENS</c>, 60 to 70, default 65.</summary>
        public static float CanopyLens => TheatreSkin.Dial("EVOSIM_THEATRE_CANOPY_LENS", 65f, 60f, 70f);

        /// <summary>How far above the horizontal the canopy shot looks: <c>EVOSIM_THEATRE_CANOPY_TILT</c>, 60 to 75 degrees, default 68.</summary>
        public static float CanopyTilt => TheatreSkin.Dial("EVOSIM_THEATRE_CANOPY_TILT", 68f, 60f, 75f);

        /// <summary>The nearest and farthest the canopy shot's eye stands under the canopy's layer, m (the review's 8 to 15).</summary>
        public const float CanopyNearest = 8f, CanopyFarthest = 15f;

        /// <summary>How deep below the surface a body still counts as the canopy, m.</summary>
        public const float CanopyBand = 10f;

        /// <summary>The film's canopy move, from <c>EVOSIM_THEATRE_CANOPY_MOVE</c>: <c>rise</c> (the default), <c>sink</c> or <c>turn</c>.</summary>
        public static CanopyMove FilmCanopyMove()
        {
            string text = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_CANOPY_MOVE") ?? "").Trim().ToLowerInvariant();
            switch (text)
            {
                case "": case "rise": return CanopyMove.Rise;
                case "sink": return CanopyMove.Sink;
                case "turn": return CanopyMove.Turn;
                default: throw new ArgumentException("EVOSIM_THEATRE_CANOPY_MOVE is rise, sink or turn, not '" + text + "'");
            }
        }

        // ------------------------------------------------------------------ the water's walls

        /// <summary>Where a camera may stand: inside the glass, over the bed, under the surface.</summary>
        public sealed class WorldBounds
        {
            public readonly bool Tank;
            public readonly float Radius;
            public readonly Vector2 Axis;
            public readonly Bounds Box;
            public readonly BedShape Bed;
            public readonly float Depth;
            /// <summary>The world's reefs, or null: the rock is one more thing the camera keeps off.</summary>
            public readonly ReefGeometry Reefs;
            /// <summary>The world's branching coral (round 62), or null: more rock for the camera to keep off.</summary>
            public readonly CoralRock Coral;

            public WorldBounds(ITheatreFrame live)
            {
                Reefs = live.Reefs != null && live.Reefs.Count > 0 ? live.Reefs : null;
                Coral = live.Coral != null && live.Coral.Count > 0 ? live.Coral : null;
                RunConfig config = live.Record.Config;
                Box = SnapshotCamera.BoxOf(live, out _);
                Bed = live.Bed;
                Depth = config.WorldDepthMetres;
                Tank = config.SharedSpace && config.WorldShape == WorldShape.Tank;
                Radius = Tank ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
                Axis = new Vector2(Radius, Radius);
            }

            /// <summary>The floor's height under a place, m.</summary>
            public float FloorAt(float x, float z) =>
                Bed != null ? (float)Bed.FloorY(x, z) : -Depth;

            /// <summary>How far a camera centred here may stand horizontally, m.</summary>
            public float RoomAround(Vector3 centre)
            {
                if (!Tank)
                {
                    float rx = Mathf.Min(centre.x - Box.min.x, Box.max.x - centre.x);
                    float rz = Mathf.Min(centre.z - Box.min.z, Box.max.z - centre.z);
                    return Mathf.Max(0.5f, Mathf.Min(rx, rz) - GlassClearance);
                }

                float off = new Vector2(centre.x - Axis.x, centre.z - Axis.y).magnitude;
                return Mathf.Max(0.5f, Radius - GlassClearance - off);
            }

            /// <summary>The nearest place a camera may stand, and which rule moved it, if any.</summary>
            public Vector3 Keep(Vector3 eye, ref int glass, ref int bed, ref int surface)
            {
                if (Tank)
                {
                    var h = new Vector2(eye.x - Axis.x, eye.z - Axis.y);
                    float most = Radius - GlassClearance;
                    if (h.magnitude > most)
                    {
                        h = h.normalized * most;
                        eye.x = Axis.x + h.x;
                        eye.z = Axis.y + h.y;
                        glass++;
                    }
                }
                else
                {
                    float x = Mathf.Clamp(eye.x, Box.min.x + GlassClearance, Box.max.x - GlassClearance);
                    float z = Mathf.Clamp(eye.z, Box.min.z + GlassClearance, Box.max.z - GlassClearance);
                    if (x != eye.x || z != eye.z) glass++;
                    eye.x = x;
                    eye.z = z;
                }

                float top = -Clearance;
                if (eye.y > top) { eye.y = top; surface++; }

                float floor = FloorAt(eye.x, eye.z) + Clearance;
                if (eye.y < floor) { eye.y = floor; bed++; }

                return eye;
            }

            /// <summary>Whether the world has any rock for the camera to keep off: a mushroom reef or a coral.</summary>
            public bool HasRock => Reefs != null || Coral != null;

            /// <summary>
            /// The signed distance from a place to the nearest reef's or coral's rock, m, or
            /// infinity with neither. Past the coral's look (<see cref="TheatreCoralRock.LookMetres"/>)
            /// the coral's answer is a bound at or above it, which no clearance asks about.
            /// </summary>
            public float ReefDistance(Vector3 p)
            {
                float d = Reefs == null ? float.PositiveInfinity : (float)Reefs.SignedDistance(p.x, p.y, p.z);
                if (Coral == null) return d;

                return Mathf.Min(d, (float)Coral.SignedDistance(
                    p.x, p.y, p.z, TheatreCoralRock.LookMetres, out _, out ReefGeometry.Distance _));
            }

            /// <summary>
            /// The nearest place at least <see cref="ReefClearance"/> outside every reef's and
            /// coral's rock, moved along the rock's outward normal, or across it when the normal
            /// would carry the eye into the surface's clearance (over a shallow table); the count
            /// says the rock moved it.
            /// </summary>
            public Vector3 OffTheReef(Vector3 eye, ref int reef)
            {
                if (!HasRock) return eye;
                bool moved = false;

                for (int pass = 0; pass < 6; pass++)
                {
                    double s = double.PositiveInfinity;
                    int nearest = -1;
                    var at = default(ReefGeometry.Distance);
                    bool coral = false;

                    if (Reefs != null)
                        s = Reefs.SignedDistance(eye.x, eye.y, eye.z, ReefClearance + 0.25d, out nearest, out at);

                    if (Coral != null)
                    {
                        double sc = Coral.SignedDistance(eye.x, eye.y, eye.z, ReefClearance + 0.25d, out int nc, out ReefGeometry.Distance ac);
                        if (nc >= 0 && (nearest < 0 || sc < s)) { s = sc; nearest = nc; at = ac; coral = true; }
                    }

                    if (nearest < 0 || s >= ReefClearance) break;

                    float need = ReefClearance - (float)s + 0.02f;
                    var g = new Vector3((float)at.Gx, (float)at.Gy, (float)at.Gz);
                    if (!Shot.Finite(g) || g.sqrMagnitude < 1e-10f) g = Vector3.up;
                    Vector3 next = eye + g.normalized * need;

                    if (next.y > -Clearance)
                    {
                        var across = new Vector3(g.x, 0f, g.z);
                        if (across.sqrMagnitude < 1e-8f)
                        {
                            // Away from the nearest reef's axis, or the nearest colony's trunk.
                            double cx, cz;
                            if (coral)
                            {
                                CoralGeometry.Colony colony = Coral.Coral.ColonyAt(nearest);
                                cx = colony.X;
                                cz = colony.Z;
                            }
                            else
                            {
                                cx = Reefs.CentreX(nearest);
                                cz = Reefs.CentreZ(nearest);
                            }

                            across = new Vector3(eye.x - (float)cx, 0f, eye.z - (float)cz);
                        }
                        if (across.sqrMagnitude < 1e-8f) across = Vector3.right;
                        next = eye + across.normalized * need;
                        next.y = Mathf.Min(next.y, -Clearance);
                    }

                    eye = next;
                    moved = true;
                }

                if (moved) reef++;
                return eye;
            }

            /// <summary>True when a place is beyond the glass (or outside the box's sides).</summary>
            public bool Outside(Vector3 eye)
            {
                if (Tank) return new Vector2(eye.x - Axis.x, eye.z - Axis.y).magnitude > Radius;
                return eye.x < Box.min.x || eye.x > Box.max.x || eye.z < Box.min.z || eye.z > Box.max.z;
            }

            public string Describe() =>
                Tank
                    ? string.Format(CultureInfo.InvariantCulture, "tank r={0:0.##} m, depth {1:0.#} m{2}",
                        Radius, Depth, Bed != null && Bed.HasRelief ? ", shaped bed" : ", flat bed")
                    : string.Format(CultureInfo.InvariantCulture, "box {0:0.#} by {1:0.#} by {2:0.#} m",
                        Box.size.x, Box.size.y, Box.size.z);
        }

        // ------------------------------------------------------------------ the plans

        /// <summary>A trapezoid of speed: eased in over the first fifth, level, eased out over the last.</summary>
        /// <remarks>
        /// Position against time for a speed that rises linearly over <see cref="EaseShare"/>, holds,
        /// and falls again; the peak is 1/(1 − share) of the mean, 1.25 at a fifth.
        /// </remarks>
        public static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            float a = EaseShare;
            float peak = 1f / (1f - a);

            if (u < a) return 0.5f * peak * u * u / a;
            if (u > 1f - a) { float w = 1f - u; return 1f - 0.5f * peak * w * w / a; }

            return 0.5f * peak * a + peak * (u - a);
        }

        /// <summary>One shot: a plan fixed at the first frame, and its camera posed at each.</summary>
        public sealed class Shot
        {
            public string Name;
            public string Directory;
            public string Plan_;
            public float FieldOfView;
            public bool Portrait;
            /// <summary>Frames this shot captures; the close shot's are fewer than the film's.</summary>
            public int Frames;
            public SnapshotCamera Camera;
            public readonly List<string> Spreads = new List<string>();

            /// <summary>Each written frame's render and read-back, milliseconds.</summary>
            public readonly List<double> RenderMs = new List<double>();

            private WorldBounds _world;
            private float _seconds;

            // orbit and drift
            private Vector3 _centre;
            private float _distance;
            private float _elevation;
            private float _azimuth;
            private float _turns;
            private Vector3 _across;
            private float _pass;

            // close
            private long _subject = -1;

            // The view that draws the subject when it is not the view the shot is given: the remains view, for a named
            // subject found dead (the death shot's time-lapse). Null for every other shot, which reads the view it is given.
            private LiveWorldView _subjectView;

            // D279's birth film: a named subject not yet born at the shot's second, whose stand-in is the box of its first
            // recorded pose (CloseOnUnborn). The plan frames the stand-in, and every pose reads it in place of the drawn box
            // until the view first draws the body, which then takes over for good. -1 for every other shot.
            private long _unbornId = -1;
            private Bounds _unbornBox;

            /// <summary>The body the close shot follows, or -1 for any other shot.</summary>
            public long Subject => _subject;

            /// <summary>True for a close shot planned to hold its eye still (<c>EVOSIM_THEATRE_FILM_CLOSE_FOLLOW</c> unset); false for a following one.</summary>
            public bool IsStill => _still;

            /// <summary>True once a still close shot has its eye and rotation planned (not when nothing was alive to frame).</summary>
            public bool HasStillEye => _hasStill;

            /// <summary>The still close shot's planned eye, before the per-frame walls and pushes. Read by the dress-set count, which changes nothing.</summary>
            public Vector3 StillEye => _stillEye;

            /// <summary>The still close shot's planned rotation. Read by the dress-set count, which changes nothing.</summary>
            public Quaternion StillRotation => _stillRotation;
            private Vector3 _offset;
            private Vector3 _forward;
            private float _standoff;
            private float _dolly;
            private Vector3 _followed;
            private bool _hasFollowed;
            private bool _still;
            private Vector3 _stillEye;
            private Quaternion _stillRotation;
            private bool _hasStill;

            // the orbit's aim below the crowd, so the bed and the far glass share the frame
            private float _aimDown;

            // a path handed in by the safari's director (Custom) or planned for the canopy, null
            // for the film's other shots
            private CameraPath _path;
            private bool _mayLeaveTheGlass;

            // the body a portrait focuses on, when it is not the close shot's subject; -1 for none
            private long _focusId = -1;

            // the tally
            private int _frames, _glass, _bed, _surface, _pushed, _inside, _reef, _limited, _over;
            private float _fastest, _fastestRelative;
            private Vector3 _lastEye, _lastSubject;
            private bool _hasLast;

            /// <summary>
            /// Where the eye stands and the point it looks at, at a point <paramref name="u"/> of
            /// the shot from 0 to 1. The safari's director writes these; the film never does.
            /// </summary>
            public delegate void CameraPath(float u, float frameSeconds, out Vector3 eye, out Vector3 look);

            /// <summary>
            /// A shot along a path the caller has planned, kept to the same walls and the same
            /// smoothing as the film's own: inside the glass (unless the path may leave it, and
            /// only while it is outside), over the bed, under the surface and off every body.
            /// </summary>
            /// <param name="name">Anything but <c>close</c> and <c>drift</c>, which name the film's shots.</param>
            /// <param name="plan">The plan's one line for the log.</param>
            /// <param name="mayLeaveTheGlass">
            /// True for the arrival, which stands outside the tank looking in: while the eye is
            /// beyond the glass neither the walls nor the bodies are asked of it.
            /// </param>
            /// <param name="focusOn">
            /// For a portrait, the body whose drawn centre the lens is focused on at every frame
            /// (a focus pull, the camera does not move for it); -1 focuses on the look point.
            /// </param>
            public static Shot Custom(
                string name, WorldBounds world, float seconds, float fieldOfView, bool portrait,
                CameraPath path, string plan, bool mayLeaveTheGlass = false, long focusOn = -1)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));
                if (name == "close" || name == "drift") throw new ArgumentException("'" + name + "' names a film shot", nameof(name));

                return new Shot
                {
                    Name = name, _world = world, _seconds = seconds, FieldOfView = fieldOfView,
                    Portrait = portrait, _path = path, _mayLeaveTheGlass = mayLeaveTheGlass, Plan_ = plan,
                    _focusId = focusOn,
                };
            }

            /// <summary>
            /// The canopy shot for a caller with its own crowd (the safari's director): the eye 8 to
            /// 15 m under the densest column of bodies near the surface, looking up through the
            /// leaves at Snell's window, one slow move (<see cref="PlanCanopy"/>).
            /// </summary>
            /// <param name="eyeAt">The eye at a fraction of the shot, 0 to 1, before the walls and the smoothing.</param>
            public static Shot Canopy(
                string name, WorldBounds world, List<Vector3> positions, List<float> reaches, List<long> ids,
                float seconds, float aspect, CanopyMove move, out Func<float, Vector3> eyeAt)
            {
                var shot = new Shot { Name = name, _world = world, _seconds = seconds };
                shot.PlanCanopy(positions, reaches, ids, aspect, move, out eyeAt);
                return shot;
            }

            /// <summary>Where the eye stood at the last pose.</summary>
            public Vector3 LastEye => _lastEye;

            /// <summary>Poses taken since the tally was reset.</summary>
            public int PosedFrames => _frames;

            /// <summary>The fastest the eye has moved between two poses, m/s.</summary>
            public float Fastest => _fastest;

            /// <summary>Poses at which the smoothed eye still stood inside a body.</summary>
            public int InsideCount => _inside;

            public static Shot Plan(
                string name, IFilmWorld live, LiveWorldView view, WorldBounds world,
                float seconds, float aspect, float turns, bool still = false)
            {
                var shot = new Shot { Name = name, _world = world, _seconds = seconds, _still = still };

                var positions = new List<Vector3>();
                var reaches = new List<float>();
                var ids = new List<long>();
                Crowd(live, view, positions, reaches, ids);

                switch (name)
                {
                    case "close":
                    {
                        Func<long, bool> eligible = CloseSubject(live, out long named);
                        bool asRemains = shot.CloseOnRemains(live, named, positions, reaches, ids, out FilmRemains remains);
                        FilmUnborn unborn = default;
                        bool asUnborn = !asRemains && shot.CloseOnUnborn(live, named, positions, reaches, ids, out unborn);
                        shot.PlanClose(live, view, positions, reaches, ids, aspect, eligible);

                        if (asRemains && shot._subject == named)
                        {
                            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                "[Theatre] close subject {0}: framed as remains (died {1:0.###} s, removed {2:0.###} s), {3} parts",
                                named, remains.Since, remains.Removed, remains.Parts));
                        }
                        else if (asUnborn && shot._subject == named)
                        {
                            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                "[Theatre] close subject {0}: framed at its birth place (born {1:0.###} s, shot opens {2:0.###} s)",
                                named, unborn.Born, live.Second));
                        }
                        else if (named >= 0 && shot._subject < 0)
                        {
                            shot.Plan_ += string.Format(CultureInfo.InvariantCulture,
                                " (subject {0} is neither alive nor among the remains at this second)", named);
                        }

                        break;
                    }
                    case "drift": shot.PlanCrowd(positions, reaches, aspect, 0f, true); break;
                    case "canopy": shot.PlanCanopy(positions, reaches, ids, aspect, FilmCanopyMove(), out _); break;
                    default: shot.PlanCrowd(positions, reaches, aspect, turns, false); break;
                }

                return shot;
            }

            /// <summary>
            /// The close shot's subject by dial, <c>EVOSIM_THEATRE_FILM_SUBJECT</c>: a body's id,
            /// or <c>leaf</c> for any body carrying a photosynthetic box, of which the largest is
            /// framed. Null when unset: the largest body, as the shot always chose. A subject
            /// named by the dial is framed alone, with no neighbour, as a portrait.
            /// </summary>
            /// <param name="named">The id the dial names, or -1 when it names none or names <c>leaf</c>.</param>
            private static Func<long, bool> CloseSubject(IFilmWorld live, out long named)
            {
                named = -1;
                string pick = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SUBJECT");
                if (string.IsNullOrWhiteSpace(pick)) return null;
                pick = pick.Trim();

                if (long.TryParse(pick, NumberStyles.Integer, CultureInfo.InvariantCulture, out long wanted))
                {
                    named = wanted;
                    return id => id == wanted;
                }

                if (pick != "leaf") throw new ArgumentException("EVOSIM_THEATRE_FILM_SUBJECT is a body id or 'leaf', not '" + pick + "'");

                var leafy = new HashSet<long>();
                for (int i = 0; i < live.BodyCount; i++)
                {
                    Phenotype body = live.PhenotypeOf(i);
                    if (body == null) continue;
                    for (int k = 0; k < body.PartCount; k++)
                    {
                        if (body.Parts[k].ShapeId == ShapeIds.Box && body.Parts[k].CellTypeId == CellTypeIds.Photosynthetic)
                        {
                            leafy.Add(live.IdAt(i));
                            break;
                        }
                    }
                }

                return leafy.Contains;
            }

            /// <summary>
            /// The death shot's second film (design note option A): a subject named by id that is not among the living at
            /// the shot's second but whose remains the world draws then is put among the crowd as the living are, its drawn
            /// root, its reach and its id, so <see cref="PlanClose"/> frames the remains by the same bearing search, lens and
            /// distance as it would the body alive. The view that draws them is kept for the plan and every pose to read the
            /// subject's drawn box from. False, and nothing touched, for any other shot: no id named, the subject alive, or
            /// no remains under that id.
            /// </summary>
            private bool CloseOnRemains(
                IFilmWorld live, long named, List<Vector3> positions, List<float> reaches, List<long> ids, out FilmRemains remains)
            {
                remains = default;
                if (named < 0 || ids.Contains(named)) return false;
                if (!live.TryRemains(named, out remains)) return false;

                positions.Add(remains.Root);
                reaches.Add(remains.Reach);
                ids.Add(named);
                _subjectView = remains.View;
                return true;
            }

            /// <summary>
            /// D279's birth film: a subject named by id that is neither alive nor among the remains at the shot's second, but
            /// whose birth the window holds after it, is put among the crowd as the living are, the root and the reach of its
            /// first recorded pose and its id, so <see cref="PlanClose"/> frames the place it will be born by the same bearing
            /// search, lens and distance as it would the body alive. The box that pose fills is kept as its stand-in, which the
            /// plan frames and every pose reads until the view first draws the body. False, and nothing touched, for any other
            /// shot: no id named, the subject alive or among the remains, or no birth of it after this second.
            /// </summary>
            private bool CloseOnUnborn(
                IFilmWorld live, long named, List<Vector3> positions, List<float> reaches, List<long> ids, out FilmUnborn unborn)
            {
                unborn = default;
                if (named < 0 || ids.Contains(named)) return false;
                if (!live.TryUnborn(named, out unborn)) return false;

                positions.Add(unborn.Root);
                reaches.Add(unborn.Reach);
                ids.Add(named);
                _unbornId = named;
                _unbornBox = unborn.Box;
                return true;
            }

            /// <summary>Every living body's root, its reach and its id, from the drawn scene where it can.</summary>
            public static void Crowd(
                IFilmWorld live, LiveWorldView view,
                List<Vector3> positions, List<float> reaches, List<long> ids)
            {
                for (int i = 0; i < live.BodyCount; i++)
                {
                    Vector3 at = Where(live, i, view);
                    if (!Finite(at)) continue;

                    positions.Add(at);
                    reaches.Add(SnapshotCamera.ReachOf(live.PhenotypeOf(i)));
                    ids.Add(live.IdAt(i));
                }
            }

            /// <summary>
            /// A body's place by its index in the world: its drawn root, which is posed every
            /// frame, or the world's own place for it (the live world's centre of mass, a window's
            /// posed centre) when it is not drawn.
            /// </summary>
            public static Vector3 Where(IFilmWorld world, int index, LiveWorldView view)
            {
                Transform root = view?.RootOf(world.IdAt(index));
                return root != null ? root.position : world.PositionOf(index);
            }

            /// <summary>
            /// A body's place: its drawn root, which is posed every frame, or the world's centre of
            /// mass, which moves twice a simulated second, when it is not drawn.
            /// </summary>
            public static Vector3 Where(Organism o, LiveWorldView view)
            {
                Transform root = view?.RootOf(o.Id);
                return root != null ? root.position : new Vector3(o.X, o.HeightY, o.Z);
            }

            public static bool Finite(Vector3 v) =>
                !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                  float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

            /// <summary>
            /// The orbit and the drift: the crowd's centroid, and a distance that holds the
            /// crowd's spread in the frame, pulled in where the glass or the surface leave no room.
            /// </summary>
            private void PlanCrowd(List<Vector3> positions, List<float> reaches, float aspect, float turns, bool drift)
            {
                FieldOfView = drift ? 45f : 50f;
                Portrait = false;

                _centre = positions.Count > 0 ? Vector3.zero : _world.Box.center;
                foreach (Vector3 p in positions) _centre += p;
                if (positions.Count > 0) _centre /= positions.Count;

                // The crowd's spread as the 85th percentile of distance from the centroid: the
                // farthest stray would frame the whole tank round one body that wandered.
                var distances = new List<float>(positions.Count);
                float reach = 0.2f;
                for (int i = 0; i < positions.Count; i++)
                {
                    distances.Add((positions[i] - _centre).magnitude);
                    reach = Mathf.Max(reach, reaches[i]);
                }

                distances.Sort();
                float spread = distances.Count > 0 ? distances[Mathf.Min(distances.Count - 1, (int)(0.85f * distances.Count))] : 2f;
                spread = Mathf.Max(1.5f, spread + reach);

                float tanV = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);
                float tanH = tanV * aspect;
                float wanted = 1.1f * spread / Mathf.Min(tanV, tanH) + spread;

                _elevation = (drift ? 4f : 12f) * Mathf.Deg2Rad;

                // Room: horizontally to the glass, and upward to the surface for the orbit's lift.
                float room = _world.RoomAround(_centre) / Mathf.Max(0.2f, Mathf.Cos(_elevation));
                float headroom = (-Clearance - _centre.y) / Mathf.Max(0.02f, Mathf.Sin(_elevation));
                float most = Mathf.Min(room, headroom);
                string tilt = drift ? "drift at 4 deg down" : "12 deg down as planned";

                // A crowd near the surface leaves a tilted-down orbit no room: r46-s1 at 5,000 s,
                // centroid at -3.4 m, got 11 m against 166 wanted, and the camera orbited inside the
                // crowd, pushed off a body 98 times. So when the surface is the cap and it caps
                // below half of what is wanted, the orbit goes level at the crowd's depth, capped
                // by the glass alone; and if the glass caps that below half too, it looks slightly
                // up at the crowd from under it, where a 45 m tank has water to spare.
                // A crowd near the surface leaves a tilted-down orbit no room, and the two
                // fallbacks this had (level at the crowd's depth, then 15 deg up from under it)
                // framed water and bodies and nothing fixed, so a viewer could not tell the
                // camera's motion from the creatures' (the owner, 2026-09-23 evening). So when
                // the surface caps the lift below half of what is wanted, the orbit stays level
                // at the crowd's depth, as far out as the glass allows, and aims five degrees
                // below the centroid: the crowd sits in the upper third of the frame, the bed
                // and the far glass fill the rest, and the surface's underside crosses the top.
                if (!drift && headroom < room && most < 0.5f * wanted)
                {
                    float surfaceCap = most;
                    _elevation = 0f;
                    _aimDown = 5f * Mathf.Deg2Rad;
                    most = _world.RoomAround(_centre);
                    tilt = string.Format(CultureInfo.InvariantCulture,
                        "level at the crowd's depth aiming 5 deg down at the bed: the surface capped 12 deg down at {0:0.##} m",
                        surfaceCap);
                }

                _distance = Mathf.Max(2f, Mathf.Min(wanted, most));

                _azimuth = -0.5f * Mathf.PI;
                _turns = turns;

                string ring = "";
                if (!drift)
                {
                    // The ring must clear the bed all the way round: a tank with a beach has a
                    // shoal that rises to the surface, and a level orbit at the glass's room
                    // crossed it (r46-s2 at 5,000 s: the bed clamp lifted the camera 396 times
                    // in 360 frames and the last frames skimmed the sand). Sampled every five
                    // degrees; the ring shrinks until every sample's floor is two metres and the
                    // clearance below the eye, and never under twenty metres.
                    float asked = _distance;
                    float eyeY = _centre.y + _distance * Mathf.Sin(_elevation);
                    while (_distance > 20f && !RingClearsTheBed(eyeY)) _distance -= 2f;
                    if (_distance < asked)
                    {
                        ring = string.Format(CultureInfo.InvariantCulture,
                            ", ring shrunk from {0:0.##} to {1:0.##} m to clear the bed", asked, _distance);
                    }

                    // The owner's rule: nothing moves faster than a body swims. The arc's peak
                    // speed is capped at half a metre a second, which at the tank's radius is a
                    // few degrees of turn a minute; the film says what it turned.
                    float peakPerTurn = 2f * Mathf.PI * _distance * Mathf.Cos(_elevation) / _seconds / (1f - EaseShare);
                    float allowed = peakPerTurn > 1e-6f ? OrbitSpeedCeiling / peakPerTurn : turns;
                    if (_turns > allowed)
                    {
                        ring += string.Format(CultureInfo.InvariantCulture,
                            ", turn cut from {0:0.###} to {1:0.###} for the {2:0.##} m/s ceiling", _turns, allowed, OrbitSpeedCeiling);
                        _turns = allowed;
                    }
                }

                if (drift)
                {
                    // A truck, not a pan: the camera keeps its bearing and slides across it at a
                    // swimmer's pace, a quarter of a metre a second at the most.
                    Vector3 look = new Vector3(-Mathf.Cos(_azimuth), 0f, -Mathf.Sin(_azimuth));
                    _across = Vector3.Cross(Vector3.up, look).normalized;
                    _pass = Mathf.Min(0.25f * _seconds, Mathf.Max(2f, spread));
                }

                Plan_ = string.Format(CultureInfo.InvariantCulture,
                    "{0} of {1} bodies about ({2:0.##}, {3:0.##}, {4:0.##}), spread {5:0.##} m, distance {6:0.##} m " +
                    "(wanted {7:0.##}, room {8:0.##}), lens {9:0} deg, {10}",
                    drift ? "drift" : "orbit", positions.Count, _centre.x, _centre.y, _centre.z, spread,
                    _distance, wanted, most, FieldOfView,
                    drift
                        ? string.Format(CultureInfo.InvariantCulture, "a {0:0.##} m pass, {1:0.###} m/s at its peak",
                            _pass, _pass / _seconds / (1f - EaseShare))
                        : string.Format(CultureInfo.InvariantCulture,
                            "{0:0.##} turn(s), {3}, {2:0.###} m/s along the arc at its peak{4}",
                            _turns, _elevation * Mathf.Rad2Deg,
                            _turns * 2f * Mathf.PI * _distance * Mathf.Cos(_elevation) / _seconds / (1f - EaseShare),
                            tilt, ring));
            }

            /// <summary>
            /// The close shot (the films review's third item, 2026-09-24): one body at 1.5 to 3 of
            /// its own lengths through a 45 to 60 degree lens, filling about two fifths of the
            /// frame's height, from the bearing nearest the three-quarter view that keeps the eye
            /// off the walls, the rock and every body with nothing across the lens, and focused on
            /// the body at every frame.
            /// </summary>
            /// <remarks>
            /// Until then it framed the largest body and the largest of its neighbours through a 28
            /// degree lens from as far off as the pair needed, which put the fog between the lens
            /// and the subject. The neighbour is no longer framed: a body in a crowd has neighbours
            /// in its frame anyway, and the lens's blur now sets them behind or in front of it. The
            /// still shot (the owner's default since 2026-09-23 evening) is framed where the
            /// subject's drift will carry it at the clip's middle, so the subject crosses the frame
            /// rather than leaving it, and the focus is pulled to it at every frame.
            /// </remarks>
            private void PlanClose(
                IFilmWorld live, LiveWorldView view,
                List<Vector3> positions, List<float> reaches, List<long> ids, float aspect,
                Func<long, bool> eligible = null)
            {
                FieldOfView = PortraitLens;
                Portrait = true;
                _forward = new Vector3(-0.78f, -0.34f, 1f).normalized;

                // A study of one body (2026-09-24, the leaves): EVOSIM_THEATRE_FILM_CLOSE_LOOK=up
                // looks up at the subject from below, where a blade's glow from the surface shows,
                // and =down looks steeply down on it, at the blade's upper face. Either is where the
                // bearing search below starts.
                string look = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CLOSE_LOOK");
                if (look == "up") _forward = new Vector3(-0.55f, 0.62f, 0.78f).normalized;
                else if (look == "down") _forward = new Vector3(-0.45f, -0.8f, 0.55f).normalized;

                int anchor = -1;
                for (int i = 0; i < positions.Count; i++)
                {
                    if (eligible != null && !eligible(ids[i])) continue;
                    if (anchor < 0 || reaches[i] > reaches[anchor]) anchor = i;
                }

                if (anchor < 0)
                {
                    _subject = -1;
                    _followed = _world.Box.center;
                    _hasFollowed = true;
                    _offset = Vector3.zero;
                    _standoff = 5f;
                    _dolly = 0f;
                    Plan_ = "nothing alive to frame: a still of the box's centre";
                    return;
                }

                long id = ids[anchor];
                float reach = reaches[anchor];

                // The view that draws the subject: the remains view for a subject found dead (CloseOnRemains), else the one given.
                LiveWorldView subjectView = _subjectView ?? view;

                // EVOSIM_THEATRE_DRESS_NEAR: the subject's one skin, left unbuilt at its first frame, is built now and dressed as the
                // palette left it, before its drawn bounds are read. Does nothing unless the view is deferring skins.
                subjectView?.BuildSkinOf(id);

                Vector3 centre;
                float length;

                // D279's birth film: a subject not yet born is not drawn, so its stand-in's box is read as a drawn box is.
                if (id == _unbornId)
                {
                    centre = _unbornBox.center;
                    length = Mathf.Max(0.05f, Mathf.Max(_unbornBox.size.x, Mathf.Max(_unbornBox.size.y, _unbornBox.size.z)));
                }
                else
                {
                    Subject(subjectView, id, positions[anchor], reach, out centre, out length);
                }

                // EVOSIM_THEATRE_FILM_CLOSE_LOOK=mouth: the search below starts from the reverse of the
                // outward normal of the subject's first eating part's mouth, as drawn now, so the camera
                // looks into the mouth. The forward's height is held to +-0.8 so the camera's up stays usable.
                string mouthNote = "";
                if (look == "mouth")
                {
                    if (TheatreMouthBite.TryFirstMouthNormal(subjectView, id, out int mouthPart, out Vector3 mouthNormal))
                    {
                        Vector3 into = -mouthNormal;
                        into.y = Mathf.Clamp(into.y, -0.8f, 0.8f);
                        if (into.sqrMagnitude > 1e-6f)
                        {
                            _forward = into.normalized;
                            mouthNote = string.Format(CultureInfo.InvariantCulture,
                                ", looking into mouth {0} (normal {1:0.###} {2:0.###} {3:0.###})",
                                mouthPart, mouthNormal.x, mouthNormal.y, mouthNormal.z);
                        }
                    }

                    if (mouthNote.Length == 0)
                    {
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[Theatre] film: close look mouth asked, no mouth on body {0}; the three-quarter view kept", id));
                    }
                }

                // The distance: the body's longest side two fifths of the frame's height, held
                // between 1.5 and 3 of its lengths, and never inside the body's own reach from its
                // root, where the per-frame push would throw the eye out again.
                float inside = (centre - positions[anchor]).magnitude + reach + 0.4f;
                float fitted = PortraitDistance(length, FieldOfView, PortraitFill);

                // EVOSIM_THEATRE_FILM_CLOSE_NEAR brings the eye in to that fraction of the fitted
                // distance (0.2 to 1, 1 the fit), never inside the body.
                string nearText = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CLOSE_NEAR");
                if (!string.IsNullOrWhiteSpace(nearText) &&
                    float.TryParse(nearText, NumberStyles.Float, CultureInfo.InvariantCulture, out float near))
                {
                    fitted *= Mathf.Clamp(near, 0.2f, 1f);
                }

                float standoff = Mathf.Max(fitted, inside);

                // Where the still shot looks: the subject's centre where its drift at this second
                // carries it at the clip's middle (in a film window, where its recorded path does).
                // A following shot looks at the subject itself.
                Vector3 drift = _still ? live.VelocityOf(id) : Vector3.zero;
                Vector3 target = centre + (_still ? live.DisplacementOf(id, 0.5f * _seconds) : Vector3.zero);

                // The bodies that could stand in the eye's way or across the lens.
                float around = 1.3f * Mathf.Max(standoff, PortraitFarthest * length) + drift.magnitude * _seconds + 2f;
                var nearPositions = new List<Vector3>();
                var nearReaches = new List<float>();
                var nearIds = new List<long>();
                for (int i = 0; i < positions.Count; i++)
                {
                    if ((positions[i] - target).magnitude > around + reaches[i]) continue;
                    nearPositions.Add(positions[i]);
                    nearReaches.Add(reaches[i]);
                    nearIds.Add(ids[i]);
                }

                // The bearing, nearest the three-quarter view first: the distance as fitted, then a
                // little nearer and farther; the elevation in steps of fifteen degrees; the azimuth
                // in steps of thirty. The first bearing with no clash and nothing across the lens is
                // taken, else the one with the fewest of both.
                float baseAzimuth = Mathf.Atan2(_forward.z, _forward.x);
                float baseElevation = Mathf.Asin(Mathf.Clamp(_forward.y, -1f, 1f));
                float[] scales = { 1f, 0.85f, 1.2f };
                float[] elevationSteps = { 0f, 15f, -15f, 30f, -30f };
                float[] azimuthSteps = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
                float steepest = 60f * Mathf.Deg2Rad;

                int bestScore = int.MaxValue, bestClashes = 0, bestAcross = 0;
                string bestWhy = null;
                Vector3 bestForward = _forward;
                float bestDistance = standoff, bestTurn = 0f, bestTilt = 0f;

                bool Search()
                {
                    foreach (float scale in scales)
                    foreach (float de in elevationSteps)
                    foreach (float da in azimuthSteps)
                    {
                        float elevation = Mathf.Clamp(baseElevation + de * Mathf.Deg2Rad, -steepest, steepest);
                        Vector3 forward = Direction(baseAzimuth + da * Mathf.Deg2Rad, elevation);
                        float distance = Mathf.Max(inside,
                            Mathf.Clamp(standoff * scale, PortraitNearest * length, PortraitFarthest * length));

                        int clashes = CloseClashes(target - forward * distance, target, nearPositions, nearReaches, nearIds, id, out string why);
                        if (!_still)
                        {
                            // The follow's dolly ends two fifths of the standoff further in.
                            clashes += CloseClashes(target - forward * Mathf.Max(inside, 0.6f * distance), target, nearPositions, nearReaches, nearIds, id, out string whyEnd);
                            if (why == null) why = whyEnd;
                        }

                        int across = Occluders(nearPositions, nearReaches, nearIds, id, target - forward * distance, target);
                        int score = 1000 * clashes + across;
                        if (score >= bestScore) continue;

                        bestScore = score;
                        bestClashes = clashes;
                        bestAcross = across;
                        bestWhy = why;
                        bestForward = forward;
                        bestDistance = distance;
                        bestTurn = da;
                        bestTilt = (elevation - baseElevation) * Mathf.Rad2Deg;
                        if (score == 0) return true;
                    }

                    return false;
                }

                // A subject just under the surface at a near portrait (the close look's fill of 0.8) leaves
                // every bearing above the surface's clearance: the eye then stands further off at the same
                // tilts, up to the farthest portrait, and the fill gives way only there. Looking up more
                // steeply instead put Snell's window, the sky through the surface, across the top of the
                // frame as a smooth banded disc, and left a still eye too near to hold a drifting body
                // (63868 at 22501 s, 2026-11-04). Only when every bearing clashes, so a shot that found a
                // clear bearing keeps it; and the further bearing replaces the first search's best only if
                // it breaks fewer rules.
                if (!Search() && bestClashes > 0)
                {
                    int firstScore = bestScore, firstClashes = bestClashes, firstAcross = bestAcross;
                    string firstWhy = bestWhy;
                    Vector3 firstForward = bestForward;
                    float firstDistance = bestDistance, firstTurn = bestTurn, firstTilt = bestTilt;

                    scales = new[] { 1.4f, 1.7f, 2f, 2.5f };
                    Search();

                    if (bestClashes >= firstClashes)
                    {
                        bestScore = firstScore;
                        bestClashes = firstClashes;
                        bestAcross = firstAcross;
                        bestWhy = firstWhy;
                        bestForward = firstForward;
                        bestDistance = firstDistance;
                        bestTurn = firstTurn;
                        bestTilt = firstTilt;
                    }
                }

                _forward = bestForward;
                _standoff = bestDistance;

                // A few metres in at most, never past two fifths of the standoff (the subject is
                // there) or into the body's reach, and never faster on average than a quarter of
                // the ceiling.
                _dolly = Mathf.Max(0f, Mathf.Min(Mathf.Min(3f, _standoff - inside),
                    Mathf.Min(0.4f * _standoff, 0.25f * CloseSpeedCeiling * _seconds)));

                _subject = id;
                _offset = centre - positions[anchor];
                _followed = centre;
                _hasFollowed = true;

                if (_still)
                {
                    _stillEye = target - _forward * _standoff;
                    _stillRotation = Quaternion.LookRotation(_forward, Vector3.up);
                    _hasStill = true;
                }

                // closeat:begin
                // EVOSIM_THEATRE_FILM_CLOSE_AT, "x,y,z,bearing,elevation,distance": the still close shot's eye stands
                // distance metres from the world point (x, y, z), on the bearing (degrees in the x-z plane, from +x
                // towards +z) and the elevation (degrees, positive looking up), and looks at the point; a diagnostic
                // for a spot rather than a body, such as where a cloud forms. Unset, nothing changes. The per-frame
                // walls and pushes still apply.
                string closeAtNote = "";
                if (_still && CloseAt(out Vector3 atPoint, out Vector3 atForward, out float atDistance))
                {
                    _forward = atForward;
                    _standoff = atDistance;
                    _stillEye = atPoint - atForward * atDistance;
                    _stillRotation = Quaternion.LookRotation(atForward, Vector3.up);
                    closeAtNote = string.Format(CultureInfo.InvariantCulture,
                        "; EVOSIM_THEATRE_FILM_CLOSE_AT: the eye at ({0:0.##}, {1:0.##}, {2:0.##}) looks at ({3:0.##}, {4:0.##}, {5:0.##}) " +
                        "from {6:0.##} m, the subject's planned centre ({7:0.##}, {8:0.##}, {9:0.##}) {10:0.##} m from that point",
                        _stillEye.x, _stillEye.y, _stillEye.z, atPoint.x, atPoint.y, atPoint.z, atDistance,
                        target.x, target.y, target.z, (target - atPoint).magnitude);
                }
                // closeat:end

                float fill = length / (2f * _standoff * Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad));
                TheatreGrade grade = TheatreGrade.Current;

                Plan_ = string.Format(CultureInfo.InvariantCulture,
                    "body {0} ({1:0.##} m long, reach {2:0.###} m) at {3:0.##} m, {4:0.#} body lengths, {5:0}% of the frame's height " +
                    "through a {6:0} deg lens; the bearing turned {7:0} deg and tilted {8:0} deg from the three-quarter view, " +
                    "{9} {10} across the lens{11}; {12}; {13}{14}",
                    id, length, reach, _standoff, _standoff / length, 100f * fill, FieldOfView, bestTurn, bestTilt,
                    bestAcross, bestAcross == 1 ? "body" : "bodies",
                    bestClashes > 0 ? ", STILL CLASHES (" + bestWhy + "): the per-frame walls and push take it from here" : "",
                    _still
                        ? string.Format(CultureInfo.InvariantCulture,
                            "held still on where its drift ({0:0.###} m/s) carries it at the clip's middle, the focus pulled to it every frame",
                            drift.magnitude)
                        : string.Format(CultureInfo.InvariantCulture,
                            "following the subject, dolly in {0:0.##} m ({1:0.###} m/s at its peak)",
                            _dolly, _dolly / _seconds / (1f - EaseShare)),
                    grade != null ? grade.DescribePortrait(_standoff, FieldOfView) : "no grade, so no depth of field",
                    mouthNote) + closeAtNote;
            }

            /// <summary>
            /// <c>EVOSIM_THEATRE_FILM_CLOSE_AT</c> read: the point, the eye's look direction and its distance. False when
            /// the variable is unset or not six numbers with a distance above zero.
            /// </summary>
            private static bool CloseAt(out Vector3 point, out Vector3 forward, out float distance)
            {
                point = forward = Vector3.zero;
                distance = 0f;
                string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CLOSE_AT");
                if (string.IsNullOrWhiteSpace(text)) return false;
                string[] parts = text.Split(',');
                if (parts.Length != 6) return false;
                var v = new float[6];
                for (int i = 0; i < 6; i++)
                    if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
                if (!(v[5] > 0f)) return false;
                float a = v[3] * Mathf.Deg2Rad, e = v[4] * Mathf.Deg2Rad;
                point = new Vector3(v[0], v[1], v[2]);
                forward = new Vector3(Mathf.Cos(e) * Mathf.Cos(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(a));
                distance = v[5];
                return true;
            }

            /// <summary>
            /// How many of a close shot's rules an eye breaks, and the first: the glass, the surface
            /// and the bed by the film's clearance, the rock by its own and between the eye and the
            /// subject, and every other body's reach and a third of a metre.
            /// </summary>
            private int CloseClashes(
                Vector3 eye, Vector3 target, List<Vector3> positions, List<float> reaches, List<long> ids, long subject,
                out string why)
            {
                why = null;
                int n = 0;

                int glass = 0, bed = 0, surface = 0;
                _world.Keep(eye, ref glass, ref bed, ref surface);
                if (glass > 0) { n++; why = why ?? "at the glass"; }
                if (surface > 0) { n++; why = why ?? "at the surface"; }
                if (bed > 0) { n++; why = why ?? "in the bed"; }
                if (_world.ReefDistance(eye) < ReefClearance) { n++; why = why ?? "in the reef"; }

                if (_world.HasRock)
                {
                    for (int k = 1; k <= 6; k++)
                    {
                        if (_world.ReefDistance(Vector3.Lerp(eye, target, k / 7f)) < 0f)
                        {
                            n++;
                            why = why ?? "the reef hides the subject";
                            break;
                        }
                    }
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    if (ids[i] == subject) continue;
                    if ((eye - positions[i]).magnitude < reaches[i] + 0.35f)
                    {
                        n++;
                        why = why ?? "in body " + ids[i];
                        break;
                    }
                }

                return n;
            }

            /// <summary>
            /// The canopy shot (the films review's second item, 2026-09-24): the eye 8 to 15 m under
            /// the densest column of bodies near the surface, looking up 60 to 75 degrees through a
            /// 60 to 70 degree lens so the leaves stand against Snell's window, with one slow move.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <b>Why.</b> At 30,000 s of round 47, 97% of the living sit in the top 4 m, most of
            /// them leaves. A leaf is a line from the side and a whole shape from below, and a blade
            /// looked up at glows with the surface's light (<c>TheatreBody.shader</c>,
            /// <c>_LeafSkyGlow</c>), which is what keeps it from going black against the window.
            /// </para>
            /// <para>
            /// <b>The column.</b> The bodies within <see cref="CanopyBand"/> of the surface (the
            /// highest quarter of the crowd when fewer than eight are), binned on three-metre
            /// squares; a square weighs its own and its eight neighbours' count, and the heaviest
            /// squares are tried first. The canopy's layer is the median height of the bodies in
            /// the nine squares, and the look passes through their centre.
            /// </para>
            /// <para>
            /// <b>The move</b> (<see cref="CanopyMove"/>). A rise is a crane straight up from 12 to 9
            /// m under the layer with the lens held; a sink goes the other way, from 9 to 14 m; a
            /// turn goes a quarter of the way round the column's axis at 11 m under, looking in at
            /// it. Each is eased at both ends and held under nine tenths of the ceiling at its peak,
            /// which shortens a short clip's rise. The bearing is the sun's first (the global the
            /// snapshot's sky view reads), so the sun's disc sits in the window behind the leaves,
            /// then turned in steps of 45 degrees until the whole path stands clear of the glass,
            /// the bed, the surface, the rock and every body, with no rock across the view.
            /// </para>
            /// </remarks>
            private void PlanCanopy(
                List<Vector3> positions, List<float> reaches, List<long> ids, float aspect,
                CanopyMove move, out Func<float, Vector3> eyeAt)
            {
                FieldOfView = CanopyLens;
                Portrait = false;

                float tilt = CanopyTilt * Mathf.Deg2Rad;
                float tanTilt = Mathf.Tan(tilt);
                float halfTanV = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);

                // The canopy's bodies.
                var canopy = new List<int>();
                for (int i = 0; i < positions.Count; i++) if (positions[i].y >= -CanopyBand) canopy.Add(i);
                string band = string.Format(CultureInfo.InvariantCulture, "{0} bodies in the top {1:0} m", canopy.Count, CanopyBand);

                if (canopy.Count < 8 && positions.Count > 0)
                {
                    var byHeight = new List<int>(positions.Count);
                    for (int i = 0; i < positions.Count; i++) byHeight.Add(i);
                    byHeight.Sort((a, b) => positions[b].y.CompareTo(positions[a].y));
                    canopy = byHeight.GetRange(0, Mathf.Min(byHeight.Count, Mathf.Max(8, byHeight.Count / 4)));
                    band = string.Format(CultureInfo.InvariantCulture,
                        "too few in the top {0:0} m, so the highest {1} bodies", CanopyBand, canopy.Count);
                }

                // The squares, and their weights with their neighbours.
                const float cell = 3f;
                var squares = new Dictionary<(int, int), List<int>>();
                foreach (int i in canopy)
                {
                    var key = (Mathf.FloorToInt(positions[i].x / cell), Mathf.FloorToInt(positions[i].z / cell));
                    if (!squares.TryGetValue(key, out List<int> list)) squares[key] = list = new List<int>();
                    list.Add(i);
                }

                var weights = new List<((int x, int z) key, int weight)>();
                foreach (KeyValuePair<(int, int), List<int>> square in squares)
                {
                    int weight = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (squares.TryGetValue((square.Key.Item1 + dx, square.Key.Item2 + dz), out List<int> near)) weight += near.Count;
                    }
                    weights.Add((square.Key, weight));
                }

                weights.Sort((a, b) =>
                {
                    int byWeight = b.weight.CompareTo(a.weight);
                    if (byWeight != 0) return byWeight;
                    int byX = a.key.x.CompareTo(b.key.x);
                    return byX != 0 ? byX : a.key.z.CompareTo(b.key.z);
                });

                // The sun's bearing, as the snapshot's sky view reads it.
                Vector4 sun = Shader.GetGlobalVector("_EvoSun");
                float sunBearing = sun.x * sun.x + sun.z * sun.z > 1e-8f ? Mathf.Atan2(sun.z, sun.x) : 0f;

                // The move's depths under the layer, and the most the ceiling lets the eye travel.
                float travel = 0.9f * OrbitSpeedCeiling * _seconds * (1f - EaseShare);
                float h0, h1;
                switch (move)
                {
                    case CanopyMove.Rise: h0 = 12f; h1 = Mathf.Max(9f, h0 - travel); break;
                    case CanopyMove.Sink: h0 = 9f; h1 = Mathf.Min(14f, h0 + travel); break;
                    default: h0 = h1 = 11f; break;
                }

                float turnRadius = h0 / tanTilt;
                float turn = move == CanopyMove.Turn ? Mathf.Min(0.5f * Mathf.PI, travel / Mathf.Max(0.5f, turnRadius)) : 0f;

                // The eye at a point of the move, and which way it looks.
                Vector3 EyeOf(Vector3 column, float bearing, float u, out Vector3 forward)
                {
                    float e = Ease(u);

                    if (move == CanopyMove.Turn)
                    {
                        float b = bearing + turn * e;
                        forward = Direction(b, tilt);
                        return column - new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * (h0 / tanTilt) - Vector3.up * h0;
                    }

                    // A crane: the lens held and the eye straight up or down, the look passing
                    // through the column's centre at the move's middle.
                    forward = Direction(bearing, tilt);
                    float middleH = 0.5f * (h0 + h1);
                    Vector3 middle = column - new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing)) * (middleH / tanTilt) - Vector3.up * middleH;
                    return middle + Vector3.up * (middleH - Mathf.Lerp(h0, h1, e));
                }

                // How many of the path's five samples break a rule, and the first rule broken.
                int Clashes(Vector3 column, float bearing, out string why)
                {
                    why = null;
                    int n = 0;

                    for (int s = 0; s <= 4; s++)
                    {
                        Vector3 eye = EyeOf(column, bearing, s / 4f, out Vector3 forward);
                        string broke = null;

                        if (_world.Tank)
                        {
                            if (new Vector2(eye.x - _world.Axis.x, eye.z - _world.Axis.y).magnitude > _world.Radius - GlassClearance - 0.5f) broke = "at the glass";
                        }
                        else if (eye.x < _world.Box.min.x + GlassClearance || eye.x > _world.Box.max.x - GlassClearance ||
                                 eye.z < _world.Box.min.z + GlassClearance || eye.z > _world.Box.max.z - GlassClearance)
                        {
                            broke = "at the glass";
                        }

                        if (broke == null && eye.y > -Clearance - 0.5f) broke = "at the surface";
                        if (broke == null && eye.y < _world.FloorAt(eye.x, eye.z) + Clearance + 0.5f) broke = "in the bed";
                        if (broke == null && _world.ReefDistance(eye) < ReefClearance + 0.5f) broke = "in the reef";

                        if (broke == null)
                        {
                            for (int i = 0; i < positions.Count; i++)
                            {
                                if ((eye - positions[i]).magnitude < reaches[i] + 0.35f) { broke = "in body " + ids[i]; break; }
                            }
                        }

                        if (broke == null && _world.HasRock)
                        {
                            // The rock across the view, up to the canopy's layer: in the line, or
                            // filling seven tenths of the frame's cone round it.
                            float h = Mathf.Max(1f, column.y - eye.y);
                            float ray = h / Mathf.Max(0.2f, Mathf.Sin(tilt));
                            for (int k = 1; k <= 8 && broke == null; k++)
                            {
                                float f = k / 9f;
                                float d = _world.ReefDistance(eye + forward * (f * ray));
                                if (d < 0f) broke = "the reef hides the canopy";
                                else if (d < 0.7f * halfTanV * Mathf.Min(f, 1f - f) * ray) broke = "the reef fills the frame";
                            }
                        }

                        if (broke == null) continue;
                        n++;
                        if (why == null) why = broke + " at " + (s * 25) + "% of the move";
                    }

                    return n;
                }

                Vector3 bestColumn = new Vector3(_world.Box.center.x, -Clearance - 2f, _world.Box.center.z);
                if (_world.Tank) bestColumn = new Vector3(_world.Axis.x, -Clearance - 2f, _world.Axis.y);
                float bestBearing = sunBearing;
                int best = int.MaxValue, bestWeight = 0, bestRank = -1, bestStep = 0;
                string bestWhy = null;
                float[] steps = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

                for (int c = 0; c < Mathf.Min(16, weights.Count) && best > 0; c++)
                {
                    var members = new List<int>();
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (squares.TryGetValue((weights[c].key.x + dx, weights[c].key.z + dz), out List<int> near)) members.AddRange(near);
                    }

                    float sx = 0f, sz = 0f;
                    var heights = new List<float>(members.Count);
                    foreach (int i in members) { sx += positions[i].x; sz += positions[i].z; heights.Add(positions[i].y); }
                    heights.Sort();
                    var column = new Vector3(sx / members.Count, heights[heights.Count / 2], sz / members.Count);

                    for (int k = 0; k < steps.Length && best > 0; k++)
                    {
                        float bearing = sunBearing + steps[k] * Mathf.Deg2Rad;
                        int n = Clashes(column, bearing, out string why);
                        if (n >= best) continue;

                        best = n;
                        bestWhy = why;
                        bestColumn = column;
                        bestBearing = bearing;
                        bestWeight = weights[c].weight;
                        bestRank = c;
                        bestStep = (int)steps[k];
                    }
                }

                if (bestRank < 0) best = Clashes(bestColumn, bestBearing, out bestWhy);

                Vector3 chosen = bestColumn;
                float chosenBearing = bestBearing;
                _path = (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    eye = EyeOf(chosen, chosenBearing, u, out Vector3 forward);
                    at = eye + forward * 10f;
                };
                eyeAt = u => EyeOf(chosen, chosenBearing, Mathf.Clamp01(u), out _);

                string moved;
                switch (move)
                {
                    case CanopyMove.Rise:
                    case CanopyMove.Sink:
                        moved = string.Format(CultureInfo.InvariantCulture,
                            "a crane {0} from {1:0.#} to {2:0.#} m under the leaves with the lens held, {3:0.###} m/s at its peak",
                            move == CanopyMove.Rise ? "up" : "down", h0, h1, Mathf.Abs(h1 - h0) / _seconds / (1f - EaseShare));
                        break;
                    default:
                        moved = string.Format(CultureInfo.InvariantCulture,
                            "{0:0.###} of a turn round the column's axis at {1:0.#} m out and {2:0.#} m under the leaves, {3:0.###} m/s at its peak",
                            turn / (2f * Mathf.PI), turnRadius, h0, turn * turnRadius / _seconds / (1f - EaseShare));
                        break;
                }

                Plan_ = string.Format(CultureInfo.InvariantCulture,
                    "canopy: {0}; the column {1} ({2} bodies in its nine squares) about ({3:0.#}, {4:0.#}) with its leaves at {5:0.#} m; " +
                    "looking {6:0} deg up at bearing {7:0} deg ({8}), lens {9:0} deg, straight up {10:0.#} deg inside the frame's top edge " +
                    "so Snell's window is in frame; {11}{12}",
                    band, bestRank >= 0 ? "ranked " + (bestRank + 1) : "(none: the tank's axis)", bestWeight,
                    chosen.x, chosen.z, chosen.y, CanopyTilt, chosenBearing * Mathf.Rad2Deg,
                    bestStep == 0 ? "the sun's" : string.Format(CultureInfo.InvariantCulture, "turned {0} deg from the sun's", bestStep),
                    FieldOfView, CanopyTilt + 0.5f * FieldOfView - 90f, moved,
                    best > 0 ? ", STILL CLASHES (" + bestWhy + ", " + best + " of 5 samples): the per-frame walls take it from here" : "");
            }

            /// <summary>True when every point of the orbit's ring stands over a floor two metres and the clearance below the eye.</summary>
            private bool RingClearsTheBed(float eyeY)
            {
                float horizontal = _distance * Mathf.Cos(_elevation);
                for (int i = 0; i < 72; i++)
                {
                    float theta = i * (2f * Mathf.PI / 72f);
                    float x = _centre.x + horizontal * Mathf.Cos(theta);
                    float z = _centre.z + horizontal * Mathf.Sin(theta);
                    if (_world.FloorAt(x, z) + Clearance + 2f > eyeY) return false;
                }
                return true;
            }

            /// <summary>The camera at a point of the clip, kept inside the water and off every body.</summary>
            public void Pose(
                IFilmWorld live, LiveWorldView view, float u, float frameSeconds,
                out Vector3 eye, out Quaternion rotation, out float focus)
            {
                Vector3 subject;

                if (_path != null)
                {
                    _path(u, frameSeconds, out eye, out subject);
                    rotation = Quaternion.LookRotation(subject - eye, Vector3.up);
                    focus = Portrait ? (subject - eye).magnitude : 0f;
                }
                else if (Name == "close")
                {
                    // The subject's drawn centre (its root and the planned offset when it is not
                    // drawn), smoothed over a second and a half so the frame follows a swimmer
                    // without copying every stroke.
                    Vector3 now = _followed;
                    if (_subject >= 0)
                    {
                        LiveWorldView subjectView = _subjectView ?? view;
                        if (DrawnBounds(subjectView, _subject, out Bounds drawn)) now = drawn.center;
                        else
                        {
                            Transform root = subjectView?.RootOf(_subject);
                            if (root != null && Finite(root.position)) now = root.position + _offset;
                            else if (_subject == _unbornId) now = _unbornBox.center;
                        }

                        // D279's birth film: once the view draws the newborn, the live body is the subject for the rest of
                        // the shot, and the stand-in is never read again (a body that dies in the shot is then held where
                        // it was last seen, as any subject is).
                        if (_subject == _unbornId && subjectView?.RootOf(_subject) != null) _unbornId = -1;
                    }

                    float k = 1f - Mathf.Exp(-frameSeconds / 1.5f);
                    _followed = _hasFollowed ? Vector3.Lerp(_followed, now, k) : now;
                    _hasFollowed = true;

                    subject = _followed;
                    float distance = _standoff - _dolly * Ease(u);

                    if (_still)
                    {
                        // Framed once, on where the subject's drift carries it at the clip's
                        // middle (PlanClose), and never moved: every motion in the clip is a
                        // creature's or the water's. The focus follows the subject (below).
                        if (!_hasStill)
                        {
                            _stillEye = subject - _forward * _standoff;
                            _stillRotation = Quaternion.LookRotation(_forward, Vector3.up);
                            _hasStill = true;
                        }

                        eye = _stillEye;
                        rotation = _stillRotation;
                        focus = _standoff;
                    }
                    else
                    {
                        eye = subject - _forward * distance;
                        rotation = Quaternion.LookRotation(_forward, Vector3.up);
                        focus = distance;
                    }
                }
                else if (Name == "drift")
                {
                    Vector3 look = new Vector3(-Mathf.Cos(_azimuth), 0f, -Mathf.Sin(_azimuth));
                    Vector3 back = -look * Mathf.Cos(_elevation) + Vector3.up * Mathf.Sin(_elevation);
                    Vector3 slide = _across * (_pass * (Ease(u) - 0.5f));

                    // The look slides with the camera, so the bearing holds and nothing pans.
                    subject = _centre + slide;
                    eye = _centre + back * _distance + slide;
                    rotation = Quaternion.LookRotation(subject - eye, Vector3.up);
                    focus = 0f;
                }
                else
                {
                    subject = _centre;
                    float theta = _azimuth + _turns * 2f * Mathf.PI * Ease(u);
                    eye = _centre + _distance * new Vector3(
                        Mathf.Cos(_elevation) * Mathf.Cos(theta), Mathf.Sin(_elevation),
                        Mathf.Cos(_elevation) * Mathf.Sin(theta));
                    subject = _centre - Vector3.up * (_distance * Mathf.Tan(_aimDown));
                    rotation = Quaternion.LookRotation(subject - eye, Vector3.up);
                    focus = 0f;
                }

                if (!(_mayLeaveTheGlass && _world.Outside(eye)))
                {
                    eye = _world.Keep(eye, ref _glass, ref _bed, ref _surface);
                    eye = OffTheBodies(live, view, eye);

                    // The reef's rock, a metre and a half off (nothing in a world with no reef).
                    eye = _world.OffTheReef(eye, ref _reef);

                    // A push can cross the glass or the bed again; the walls win, and the tally says so.
                    eye = _world.Keep(eye, ref _glass, ref _bed, ref _surface);
                }

                // A push off a body is a quarter-metre jump in one frame (r46-s1 at 5,000 s: 60
                // pushes on a 33 m ring inside the crowd, 2.7 m/s at the jump). The moving shots'
                // eye is smoothed over half a second, so a push becomes a glide at about the arc's
                // own pace; the smoothed eye may sit inside a body for a few frames, and the
                // tally counts those. The still close shot never moves and is left alone.
                if (Name != "close" && _hasLast)
                {
                    eye = Vector3.Lerp(_lastEye, eye, 1f - Mathf.Exp(-frameSeconds / 0.5f));
                }

                // A safari path's corrections are speed-limited too: a wall's clamp or a push that
                // would carry the eye faster than a body swims moves it at the ceiling instead, and
                // the eye catches up over the frames that follow, which is a slower dolly. Round
                // 47's colonies at the surface moved at 1.2 to 5.3 m/s under the surface's clamp
                // (2026-09-24). The film's own shots are left as they were.
                if (_path != null && _hasLast)
                {
                    float most = OrbitSpeedCeiling * 0.97f * Mathf.Max(1e-3f, frameSeconds);
                    Vector3 step = eye - _lastEye;
                    float length = step.magnitude;
                    if (length > most)
                    {
                        eye = _lastEye + step * (most / length);
                        _limited++;
                    }
                }

                if (InsideABody(live, view, eye)) _inside++;

                if (Name != "close") rotation = Quaternion.LookRotation(subject - eye, Vector3.up);

                // A portrait's focus is pulled to its subject at every frame: the depth along the
                // lens of the drawn centre of the body it is about, from where the eye stands after
                // the walls, the pushes and the smoothing. URP focuses on a plane, so a depth and
                // not a distance; a still camera stays still and only the focus moves.
                if (Portrait)
                {
                    long about = _focusId >= 0 ? _focusId : _subject;
                    Vector3 point = subject;
                    LiveWorldView aboutView = _subjectView != null && about == _subject ? _subjectView : view;
                    if (about >= 0 && DrawnBounds(aboutView, about, out Bounds body)) point = body.center;
                    else if (about >= 0 && about == _unbornId) point = _unbornBox.center;
                    float depth = Vector3.Dot(point - eye, rotation * Vector3.forward);
                    focus = depth > 0.1f ? depth : Mathf.Max(0.1f, (subject - eye).magnitude);
                }

                if (_hasLast)
                {
                    float speed = (eye - _lastEye).magnitude / frameSeconds;
                    if (speed > OrbitSpeedCeiling * 1.05f) _over++;
                    _fastest = Mathf.Max(_fastest, speed);
                    _fastestRelative = Mathf.Max(_fastestRelative,
                        ((eye - subject) - (_lastEye - _lastSubject)).magnitude / frameSeconds);
                }

                _lastEye = eye;
                _lastSubject = subject;
                _hasLast = true;
                _frames++;
            }

            /// <summary>Moves the eye out of any body's reach, plus a quarter metre.</summary>
            private Vector3 OffTheBodies(IFilmWorld live, LiveWorldView view, Vector3 eye)
            {
                int count = live.BodyCount;
                bool moved = false;

                for (int pass = 0; pass < 3; pass++)
                {
                    bool again = false;

                    for (int i = 0; i < count; i++)
                    {
                        Vector3 at = Where(live, i, view);
                        if (!Finite(at)) continue;

                        float keep = SnapshotCamera.ReachOf(live.PhenotypeOf(i)) + 0.25f;
                        Vector3 away = eye - at;
                        float d = away.magnitude;
                        if (d >= keep) continue;

                        eye = at + (d > 1e-4f ? away / d : Vector3.up) * keep;
                        again = true;
                        moved = true;
                    }

                    if (!again) break;
                }

                if (moved) _pushed++;
                return eye;
            }

            public static bool InsideABody(IFilmWorld live, LiveWorldView view, Vector3 eye)
            {
                for (int i = 0; i < live.BodyCount; i++)
                {
                    Vector3 at = Where(live, i, view);
                    if (!Finite(at)) continue;
                    if ((eye - at).magnitude < SnapshotCamera.ReachOf(live.PhenotypeOf(i))) return true;
                }

                return false;
            }

            /// <summary>Forgets the warm-up's poses, so the tally counts the film's frames alone.</summary>
            public void ResetTally()
            {
                _frames = _glass = _bed = _surface = _pushed = _inside = _reef = _limited = _over = 0;
                _fastest = _fastestRelative = 0f;
                _hasLast = false;
            }

            public string Tally() => string.Format(CultureInfo.InvariantCulture,
                "{0} frames; the glass bound {1}, the bed {2}, the surface {3}, the reef {9}; pushed off a body {4}, " +
                "still inside one {5}; fastest camera {6:0.###} m/s, fastest against the subject {7:0.###} m/s; " +
                "speed-limited {10}, over the ceiling {11}{8}",
                _frames, _glass, _bed, _surface, _pushed, _inside, _fastest, _fastestRelative,
                Name == "close" && _fastestRelative > CloseSpeedCeiling ? " (OVER the 0.3 m/s ceiling)" : "",
                _reef, _limited, _over);
        }
    }
}
