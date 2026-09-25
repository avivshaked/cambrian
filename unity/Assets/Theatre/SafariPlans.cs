using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The camera plans of the seven stations (safari-spec.md items 6 and 9), each one move, each
    /// a <see cref="FilmPlans.Shot.Custom"/> so that the film's walls, pushes, smoothing and tally
    /// keep every frame, and each checked against the bed and the bodies before it plays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The grammar, as the plans hold it.</b> One move per take; a strong move eased on the
    /// film's trapezoid (a fifth at each end, <see cref="FilmPlans.Ease"/>), a small drift linear.
    /// The camera's own speed along its path is held under <see cref="FilmPlans.OrbitSpeedCeiling"/>
    /// at its peak, and a following orbit counts the subject's drift against it. Following is an
    /// orbit round a smoothed subject, never a tail; the subject sits a third off centre on the
    /// side it came from, so it swims into the frame; the arc's family (horizontal, vertical or
    /// both) and its sense come from the scene's index and the clade's hash, so consecutive
    /// scenes differ; a sitter gets a tighter, slower arc than a swimmer; the lens comes from the
    /// arc's radius. No pan: a look that moves does so because the eye moved.
    /// </para>
    /// <para>
    /// <b>The check before the take.</b> Every plan's path is sampled and each sample asked
    /// whether it stands over the bed by the film's clearance, inside the glass, under the
    /// surface, a metre and a half outside every reef's rock and outside every other body's reach
    /// at the scene's second. A portrait or a colony that fails is re-planned, not clamped: its
    /// height (down to under the subject, looking up), its bearing and its distance are searched
    /// nearest first, and the plan's line says what moved. The film's per-frame walls stay on
    /// behind it for what moves after the check, and their corrections are held to the ceiling.
    /// </para>
    /// </remarks>
    public static class SafariPlans
    {
        /// <summary>The margin past a body's reach a planned eye keeps, m (the film's push uses a quarter metre).</summary>
        public const float BodyMargin = 0.35f;

        /// <summary>A subject faster than this through the water is filmed as a swimmer, m/s.</summary>
        public const float SwimmerSpeed = 0.12f;

        /// <summary>Everything a plan needs to know about the world at the scene's second.</summary>
        public sealed class Stage
        {
            public TheatreDynamicsReplay Live;
            public LiveWorldView View;
            public FilmPlans.WorldBounds World;
            public float Aspect = 16f / 9f;
            public int SceneIndex;
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<float> Reaches = new List<float>();
            public readonly List<long> Ids = new List<long>();

            public static Stage Of(TheatreDynamicsReplay live, LiveWorldView view, float aspect, int sceneIndex)
            {
                var s = new Stage { Live = live, View = view, World = new FilmPlans.WorldBounds(live), Aspect = aspect, SceneIndex = sceneIndex };
                FilmPlans.Shot.Crowd(live, view, s.Positions, s.Reaches, s.Ids);
                return s;
            }

            public int IndexOf(long id) => Ids.IndexOf(id);

            public Vector3 Centroid(IList<int> members)
            {
                if (members == null || members.Count == 0)
                {
                    if (Positions.Count == 0) return World.Box.center;
                    Vector3 all = Vector3.zero;
                    foreach (Vector3 p in Positions) all += p;
                    return all / Positions.Count;
                }

                Vector3 sum = Vector3.zero;
                foreach (int i in members) sum += Positions[i];
                return sum / members.Count;
            }

            /// <summary>The tank's axis at a height, or the box's centre.</summary>
            public Vector3 Axis(float y) =>
                World.Tank ? new Vector3(World.Axis.x, y, World.Axis.y) : new Vector3(World.Box.center.x, y, World.Box.center.z);

            /// <summary>How far from the axis a camera may stand horizontally.</summary>
            public float Room => World.Tank ? World.Radius - FilmPlans.GlassClearance
                : 0.5f * Mathf.Min(World.Box.size.x, World.Box.size.z) - FilmPlans.GlassClearance;
        }

        /// <summary>A take ready to play: the shot, its length, and the plan's line.</summary>
        public sealed class Take
        {
            public FilmPlans.Shot Shot;
            public float Seconds;
            public string Plan;
            /// <summary>The body the take is about, or -1.</summary>
            public long Subject = -1;
            /// <summary>
            /// The eye at a fraction of the take, 0 to 1, for a plan whose path is known in closed
            /// form (the descent's dolly), so the director can time a caption to a depth; null for
            /// the rest.
            /// </summary>
            public Func<float, Vector3> EyeAt;
        }

        // ---------------------------------------------------------------- the check

        /// <summary>
        /// The extra room under the surface a moving plan keeps at the scene's second, m, over the
        /// film's metre: a followed subject rises and falls a little during its take, and a plan
        /// that grazes the metre at the second it was made sits on the surface's clamp after.
        /// </summary>
        public const float SurfaceMargin = 0.5f;

        /// <summary>Why a planned eye would clip, or null when it is clear.</summary>
        /// <param name="topMargin">Extra room under the surface's clearance, m.</param>
        public static string Clash(Stage stage, Vector3 eye, long ignore = -1, float topMargin = 0f)
        {
            FilmPlans.WorldBounds w = stage.World;

            if (w.Outside(eye)) return "outside the glass";
            if (w.Tank && new Vector2(eye.x - w.Axis.x, eye.z - w.Axis.y).magnitude > w.Radius - FilmPlans.GlassClearance) return "at the glass";
            if (eye.y > -FilmPlans.Clearance - topMargin) return "at the surface";
            if (eye.y < w.FloorAt(eye.x, eye.z) + FilmPlans.Clearance) return "in the bed";
            if (w.ReefDistance(eye) < FilmPlans.ReefClearance) return "in the reef";

            for (int i = 0; i < stage.Positions.Count; i++)
            {
                if (stage.Ids[i] == ignore) continue;
                if ((eye - stage.Positions[i]).magnitude < stage.Reaches[i] + BodyMargin) return "in body " + stage.Ids[i];
            }

            return null;
        }

        /// <summary>
        /// Whether the rock stands between an eye and what it looks at, sampled at eight points of
        /// the line short of both ends (a sitter on a table touches the rock it sits on).
        /// </summary>
        /// <param name="halfTan">
        /// With a lens: the tangent of the frame's half-width, and the rock must also stand clear
        /// of seven tenths of the frame's cone round the line, tapered to nothing at the subject
        /// (a table under a sitter is its background). Round 47's second portrait of
        /// Pinnifrons febofila kept its eye out of the rock and still filled half its frames
        /// with a stem a few metres off the lens.
        /// </param>
        public static string Hidden(Stage stage, Vector3 eye, Vector3 look, float halfTan = 0f)
        {
            if (stage.World.Reefs == null) return null;
            float length = (look - eye).magnitude;
            for (int k = 1; k <= 8; k++)
            {
                float f = 0.05f + 0.85f * k / 8f;
                Vector3 p = Vector3.Lerp(eye, look, f);
                float room = 0.7f * halfTan * Mathf.Min(f, 1f - f) * length;
                float d = stage.World.ReefDistance(p);
                if (d < 0f) return "the reef hides the subject";
                if (d < room) return "the reef fills the frame";
            }
            return null;
        }

        /// <summary>The first clash along a path sampled at 25 points, or null.</summary>
        private static string PathClash(Stage stage, Func<float, Vector3> eyeAt, long ignore)
        {
            PathClashes(stage, eyeAt, ignore, null, 0f, out string first);
            return first;
        }

        /// <summary>
        /// How many of a path's 25 samples clash (with the rock between the eye and the look
        /// counted when a look is given), and the first of them, or null.
        /// </summary>
        private static int PathClashes(Stage stage, Func<float, Vector3> eyeAt, long ignore,
            Func<float, Vector3> lookAt, float topMargin, out string first, float halfTan = 0f)
        {
            first = null;
            int n = 0;
            for (int k = 0; k <= 24; k++)
            {
                float u = k / 24f;
                Vector3 eye = eyeAt(u);
                string why = Clash(stage, eye, ignore, topMargin);
                if (why == null && lookAt != null) why = Hidden(stage, eye, lookAt(u), halfTan);
                if (why == null) continue;
                n++;
                if (first == null) first = why + " at " + (k * 100 / 24) + "% of the path";
            }
            return n;
        }

        // ---------------------------------------------------------------- arrival

        /// <summary>
        /// Outside the glass just over the waterline, at the bearing whose frame holds the most
        /// bodies and reef tables, looking down at that crowd; a ten-second hold with a push of a
        /// metre and a half, linear, since it is a drift.
        /// </summary>
        public static Take Arrival(Stage stage, float seconds, int hash)
        {
            // The first cut stood at a bearing from the hash, looked at the axis 88 m away through
            // the fog and framed the glass's seams and a dim haze (round 47 seed 1, 2026-09-24).
            // Now the bearing is the one whose frame holds the most bodies and reef tables within
            // the distance the water shows, the look is at those bodies, and the eye stands over
            // the waterline, outside the glass, so the tables' tops face it rather than their rims.
            const float fov = 50f;
            float outside = stage.Room + FilmPlans.GlassClearance + 2.5f;
            float push = 1.5f;
            float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad), tanH = tanV * stage.Aspect;
            ReefGeometry reefs = stage.World.Reefs;

            float bestBearing = (hash % 360) * Mathf.Deg2Rad;
            Vector3 bestLook = stage.Axis(-3f);
            int bestBodies = -1, bestTables = 0;
            float bestScore = -1f;

            for (int k = 0; k < 72; k++)
            {
                float b = (hash % 5 + k * 5f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                Vector3 eye = stage.Axis(ArrivalEyeHeight) + outward * outside;

                // The crowd on this side: bodies within sight, inside a wide cone inward.
                Vector3 sum = Vector3.zero;
                int near = 0;
                for (int i = 0; i < stage.Positions.Count; i++)
                {
                    Vector3 d = stage.Positions[i] - eye;
                    float dist = d.magnitude;
                    if (dist > ArrivalSightMetres || dist < 1f) continue;
                    if (Vector3.Dot(new Vector3(d.x, 0f, d.z).normalized, -outward) < 0.7f) continue;
                    sum += stage.Positions[i];
                    near++;
                }

                Vector3 look = near > 0 ? sum / near : eye - outward * (0.5f * ArrivalSightMetres);
                look.y = Mathf.Min(look.y, -2.5f);
                Quaternion inverse = Quaternion.Inverse(Quaternion.LookRotation(look - eye, Vector3.up));

                bool InFrame(Vector3 p)
                {
                    Vector3 c = inverse * (p - eye);
                    return c.z > 1f && c.z < ArrivalSightMetres && Mathf.Abs(c.x) < c.z * tanH && Mathf.Abs(c.y) < c.z * tanV;
                }

                int bodies = 0;
                foreach (Vector3 p in stage.Positions) if (InFrame(p)) bodies++;

                int tables = 0;
                if (reefs != null)
                {
                    for (int r = 0; r < reefs.Count; r++)
                    {
                        if (InFrame(new Vector3((float)reefs.CentreX(r), (float)reefs.CapTopY(r), (float)reefs.CentreZ(r)))) tables++;
                    }
                }

                float score = bodies + 25f * tables;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBearing = b;
                    bestLook = look;
                    bestBodies = bodies;
                    bestTables = tables;
                }
            }

            var outwardBest = new Vector3(Mathf.Cos(bestBearing), 0f, Mathf.Sin(bestBearing));
            Vector3 start = stage.Axis(ArrivalEyeHeight) + outwardBest * outside;
            Vector3 end = start - outwardBest * push;
            Vector3 lookAt = bestLook;

            FilmPlans.Shot shot = FilmPlans.Shot.Custom("arrival", stage.World, seconds, fov, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    eye = Vector3.Lerp(start, end, Mathf.Clamp01(u));
                    at = lookAt + (eye - start);
                },
                string.Format(CultureInfo.InvariantCulture,
                    "arrival from outside the glass at bearing {0:0} deg, {1:0.#} m from the axis at {2:0.#} m {3} the waterline, " +
                    "looking {4:0} deg down at ({5:0.#}, {6:0.#}, {7:0.#}) where the frame holds {8} bodies and {9} reef tables within {10:0} m, " +
                    "a {11:0.#} m linear push over {12:0} s ({13:0.###} m/s)",
                    bestBearing * Mathf.Rad2Deg, outside, Mathf.Abs(ArrivalEyeHeight), ArrivalEyeHeight >= 0f ? "over" : "under",
                    Mathf.Atan2(start.y - lookAt.y, new Vector2(lookAt.x - start.x, lookAt.z - start.z).magnitude) * Mathf.Rad2Deg,
                    lookAt.x, lookAt.y, lookAt.z, bestBodies, bestTables, ArrivalSightMetres, push, seconds, push / seconds),
                mayLeaveTheGlass: true);

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_ };
        }

        /// <summary>The arrival's eye over the waterline, m (negative is under it).</summary>
        public const float ArrivalEyeHeight = 1.5f;

        /// <summary>How far the arrival counts bodies and tables as seen, m: past it the water is haze.</summary>
        public const float ArrivalSightMetres = 55f;

        // ---------------------------------------------------------------- descent

        /// <summary>
        /// A dolly down through the lit band on the shallow side, the fog thickening and the sand
        /// rising ahead, ending a metre above the clearance over the floor. The look keeps its
        /// bearing (outward and down), so nothing pans. The length comes from the ceiling.
        /// </summary>
        /// <param name="exactSeconds">
        /// A length the take must have, s (a story's shot list sets it): the dolly starts lower on
        /// the same line when the whole of it would pass the ceiling in that time, and runs slower
        /// when it would not. 0 lets the ceiling decide, the template's rule.
        /// </param>
        public static Take Descent(Stage stage, int hash, float mostSeconds = 120f, float exactSeconds = 0f)
        {
            if (exactSeconds > 0f) mostSeconds = exactSeconds;

            FilmPlans.WorldBounds w = stage.World;
            float room = stage.Room;

            // The shallow arc: the bearing whose floor at seven tenths of the room is highest.
            float bestFloor = float.NegativeInfinity, bearing = 0f;
            for (int k = 0; k < 36; k++)
            {
                float b = k * 10f * Mathf.Deg2Rad;
                Vector3 p = stage.Axis(0f) + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * (0.7f * room);
                float f = w.FloorAt(p.x, p.z);
                if (f > bestFloor + 0.01f) { bestFloor = f; bearing = b; }
            }

            var outward = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
            Vector3 top = stage.Axis(-FilmPlans.Clearance - 0.5f) + outward * (0.2f * room);
            Vector3 bottom = stage.Axis(0f) + outward * (0.5f * room);
            bottom.y = w.FloorAt(bottom.x, bottom.z) + FilmPlans.Clearance + 1f;

            // A path longer than the ceiling allows in the time given starts lower on the same line.
            float mean = 0.8f * FilmPlans.OrbitSpeedCeiling;
            float length = (bottom - top).magnitude;
            string shortened = "";
            if (length / mean > mostSeconds)
            {
                float keep = mean * mostSeconds;
                top = bottom + (top - bottom).normalized * keep;
                shortened = string.Format(CultureInfo.InvariantCulture, ", started {0:0.#} m down so the dolly fits {1:0} s", -top.y, mostSeconds);
                length = keep;
            }

            float seconds = exactSeconds > 0f ? exactSeconds : Mathf.Max(20f, length / mean);

            // Lifted clear of the bed wherever the straight line would cut it.
            string lifted = "";
            float lift = 0f;
            Vector3 a = top, b2 = bottom;
            for (int tries = 0; tries < 20; tries++)
            {
                Vector3 aa = a, bb = b2 + Vector3.up * lift;
                bool clear = true;
                for (int k = 0; k <= 24 && clear; k++)
                {
                    Vector3 p = Vector3.Lerp(aa, bb, k / 24f);
                    if (p.y < w.FloorAt(p.x, p.z) + FilmPlans.Clearance + 0.2f) clear = false;
                }
                if (clear) break;
                lift += 0.5f;
            }
            if (lift > 0f) { bottom += Vector3.up * lift; lifted = string.Format(CultureInfo.InvariantCulture, ", the end lifted {0:0.#} m off the bed", lift); }

            Vector3 gaze = (outward * 8f + Vector3.down * 3f);
            Vector3 from = top, to = bottom;

            FilmPlans.Shot shot = FilmPlans.Shot.Custom("descent", w, seconds, 55f, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    eye = Vector3.Lerp(from, to, FilmPlans.Ease(u));
                    at = eye + gaze;
                },
                string.Format(CultureInfo.InvariantCulture,
                    "descent on the shallow side (bearing {0:0} deg) from {1:0.#} m to {2:0.#} m, {3:0.#} m of dolly in {4:0} s, " +
                    "{5:0.###} m/s at the peak{6}{7}",
                    bearing * Mathf.Rad2Deg, -from.y, -to.y, (to - from).magnitude, seconds,
                    (to - from).magnitude / seconds / (1f - FilmPlans.EaseShare), shortened, lifted));

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, EyeAt = u => Vector3.Lerp(from, to, FilmPlans.Ease(Mathf.Clamp01(u))) };
        }

        // ---------------------------------------------------------------- portrait

        /// <summary>
        /// A named body about two fifths of the frame's height at 1.5 to 3 of its own lengths
        /// through the portrait's lens (the films review's third item, 2026-09-24), orbited a
        /// quarter turn (a sitter) or two fifths (a swimmer), the subject a third off centre with
        /// room ahead of it, lit from one side by the portrait's back light and fill, and focused
        /// on at every frame.
        /// </summary>
        /// <remarks>
        /// Until then the radius was ten reaches, at least 2.5 m, and the lens came from the radius,
        /// which stood round 47's portraits 5 to 31 m off through the fog, small in the frame. The
        /// orbit now follows the body's drawn centre rather than its root, which can sit at one
        /// end of it, and among the bearings that clear everything it prefers one with no body
        /// across the lens.
        /// </remarks>
        public static Take Portrait(Stage stage, int subjectIndex, float seconds, bool swimmer, int cladeHash)
        {
            long id = stage.Ids[subjectIndex];
            float reach = Mathf.Max(0.05f, stage.Reaches[subjectIndex]);
            Vector3 root0 = stage.Positions[subjectIndex];
            FilmPlans.Subject(stage.View, id, root0, reach, out Vector3 centre0, out float length);
            Vector3 rootToCentre = centre0 - root0;

            // The lens is the portrait's, and the distance fills the frame's height with the body's
            // longest side, a fifth less for a swimmer, which needs room to swim into; never inside
            // the body's reach from its root.
            float fov = FilmPlans.PortraitLens;
            float fill = swimmer ? 0.8f * FilmPlans.PortraitFill : FilmPlans.PortraitFill;
            float inside = rootToCentre.magnitude + reach + BodyMargin + 0.1f;
            float nearest = Mathf.Max(inside, FilmPlans.PortraitNearest * length);
            float farthest = Mathf.Max(inside, FilmPlans.PortraitFarthest * length);
            float radius = Mathf.Clamp(FilmPlans.PortraitDistance(length, fov, fill), nearest, farthest);

            float turns = swimmer ? 0.4f : 0.25f;
            int family = (cladeHash % 3 + stage.SceneIndex % 3) % 3;      // 0 horizontal, 1 vertical, 2 both
            float sense = ((cladeHash % 2 + (stage.SceneIndex / 3) % 2) % 2 == 0) ? 1f : -1f;
            float azimuth0 = ((cladeHash >> 3) % 360) * Mathf.Deg2Rad;
            float elevation0 = family == 1 ? -10f * Mathf.Deg2Rad : 12f * Mathf.Deg2Rad;
            float elevationSweep = family == 0 ? 0f : family == 1 ? 40f * Mathf.Deg2Rad : 20f * Mathf.Deg2Rad;
            float azimuthSweep = family == 1 ? 0f : sense * turns * 2f * Mathf.PI;

            // The subject's drift through the tank at the scene's second, from the solver.
            Vector3 drift = VelocityOf(stage.Live, id);
            float driftSpeed = drift.magnitude;

            // The ceiling: arc speed at its peak plus the subject's drift, under half a metre a second.
            string notes = "";
            float Arc(float r) => (Mathf.Abs(azimuthSweep) * r * Mathf.Cos(elevation0) + elevationSweep * r) / seconds / (1f - FilmPlans.EaseShare);
            float allowed = Mathf.Max(0.12f, 0.95f * (FilmPlans.OrbitSpeedCeiling - driftSpeed));   // under the per-frame limiter (0.97 of the ceiling)
            if (Arc(radius) > allowed)
            {
                float cut = allowed / Arc(radius);
                azimuthSweep *= cut;
                elevationSweep *= cut;
                notes += string.Format(CultureInfo.InvariantCulture, ", the arc cut to {0:0.##} of itself for the ceiling with {1:0.###} m/s of drift", cut, driftSpeed);
            }

            // The check re-plans rather than clamps: the arc's height (down to under the subject,
            // looking up), its bearing, the sense of a vertical sweep and its distance are searched
            // nearest first until every sample of the ring is clear of the glass, the bed, the
            // reef's rock and the bodies, a metre and a half under the surface, with no rock
            // between the eye and the subject, at the scene's second. Round 47's first safari
            // lifted surface portraits into the surface's clamp and orbited two into the rock.
            // The subject where its drift at the scene's second carries it, for the check: a
            // swimmer crosses metres in a take, and the eye goes with it.
            Vector3 SubjectAt(float u) => centre0 + drift * (u * seconds);
            Vector3 EyeAt(float u, float r, float az0, float el0, float elSweep) =>
                SubjectAt(u) + r * Direction(az0 + azimuthSweep * FilmPlans.Ease(u), el0 + elSweep * FilmPlans.Ease(u));

            // Bodies across the lens at the start, the middle and the end of the arc.
            int Across(float r, float az0, float el0, float elSweep)
            {
                int n = 0;
                for (int k = 0; k <= 2; k++)
                {
                    float u = 0.5f * k;
                    n += FilmPlans.Occluders(stage.Positions, stage.Reaches, stage.Ids, id, EyeAt(u, r, az0, el0, elSweep), SubjectAt(u));
                }
                return n;
            }

            float baseRadius = radius, baseElevation = elevation0, baseAzimuth = azimuth0, baseSweep = elevationSweep;
            float[] radiusScales = { 1f, 1.15f, 0.87f, 1.3f, 0.77f };
            float[] elevationSteps = { 0f, -5f, 5f, -10f, 10f, -15f, 15f, -20f, 20f, -30f, 30f, -40f, 40f };
            float[] azimuthSteps = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
            float[] senses = baseSweep != 0f ? new[] { 1f, -1f } : new[] { 1f };
            float steepest = 65f * Mathf.Deg2Rad;
            float lensTan = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * stage.Aspect;

            // The first bearing clear of everything with no body across the lens is taken; failing
            // that, the clear one with the fewest across it among the first two dozen clear ones;
            // failing that, the one with the fewest clashing samples.
            int fewest = int.MaxValue, fewestAcross = int.MaxValue, clearSeen = 0;
            string clash = null;
            bool Search()
            {
                foreach (float scale in radiusScales)
                foreach (float sense in senses)
                foreach (float de in elevationSteps)
                foreach (float da in azimuthSteps)
                {
                    float r = Mathf.Clamp(baseRadius * scale, nearest, farthest), e0 = baseElevation + de * Mathf.Deg2Rad;
                    float a0 = baseAzimuth + da * Mathf.Deg2Rad, es = sense * baseSweep;
                    if (Mathf.Abs(e0) > steepest || Mathf.Abs(e0 + es) > steepest) continue;

                    int n = PathClashes(stage, u => EyeAt(u, r, a0, e0, es), id, SubjectAt, SurfaceMargin, out string why, lensTan);
                    if (n > fewest) continue;

                    int across = n == 0 ? Across(r, a0, e0, es) : int.MaxValue;
                    if (n == 0) clearSeen++;

                    if (n < fewest || across < fewestAcross)
                    {
                        fewest = n;
                        fewestAcross = across;
                        clash = why;
                        radius = r; elevation0 = e0; azimuth0 = a0; elevationSweep = es;
                    }

                    if (fewest == 0 && (fewestAcross == 0 || clearSeen >= 24)) return true;
                }
                return false;
            }

            Search();

            bool replanned = radius != baseRadius || elevation0 != baseElevation || azimuth0 != baseAzimuth || elevationSweep != baseSweep;
            if (replanned)
            {
                notes += string.Format(CultureInfo.InvariantCulture,
                    ", re-planned clear: {0:0} deg of elevation (from {1:0}), turned {2:0} deg, at {3:0.##} m (from {4:0.##}){5}",
                    elevation0 * Mathf.Rad2Deg, baseElevation * Mathf.Rad2Deg, (azimuth0 - baseAzimuth) * Mathf.Rad2Deg,
                    radius, baseRadius, elevationSweep != baseSweep ? ", sweeping down" : "");
            }

            // A longer radius makes the arc faster: cut it again (a shorter arc is a part of the
            // one checked, so it stays clear).
            if (Arc(radius) > allowed)
            {
                float cut = allowed / Arc(radius);
                azimuthSweep *= cut;
                elevationSweep *= cut;
                notes += string.Format(CultureInfo.InvariantCulture, ", cut again to {0:0.##} for the ceiling at the new radius", cut);
            }

            if (fewest > 0) notes += ", STILL CLASHES (" + clash + ", " + fewest + " of 25 samples): the film's per-frame walls take it from here";
            else if (fewestAcross > 0) notes += string.Format(CultureInfo.InvariantCulture, ", {0} body sightings across the lens at the arc's start, middle and end", fewestAcross);

            float tanH = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * stage.Aspect;

            // A third off centre, on the side the subject came from, so it swims into the frame.
            Vector3 firstEye = EyeAt(0f, radius, azimuth0, elevation0, elevationSweep);
            Vector3 right = Vector3.Cross(Vector3.up, (centre0 - firstEye).normalized).normalized;
            float lead = Vector3.Dot(drift, right) >= 0f ? 1f : -1f;
            float offset = lead * radius * tanH / 3f;

            Vector3 followed = centre0;
            bool hasFollowed = false;
            LiveWorldView view = stage.View;
            float r0 = radius, el = elevation0;

            FilmPlans.Shot shot = FilmPlans.Shot.Custom("portrait", stage.World, seconds, fov, true,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    // The drawn centre, or the root and the planned offset when it is not drawn.
                    Vector3 now = followed;
                    if (FilmPlans.DrawnBounds(view, id, out Bounds drawn)) now = drawn.center;
                    else
                    {
                        Transform root = view?.RootOf(id);
                        if (root != null && FilmPlans.Shot.Finite(root.position)) now = root.position + rootToCentre;
                    }

                    float k = 1f - Mathf.Exp(-Mathf.Max(1e-3f, dt) / 1.5f);
                    followed = hasFollowed ? Vector3.Lerp(followed, now, k) : now;
                    hasFollowed = true;

                    float e = FilmPlans.Ease(u);
                    Vector3 dir = Direction(azimuth0 + azimuthSweep * e, el + elevationSweep * e);
                    eye = followed + r0 * dir;
                    Vector3 across = Vector3.Cross(Vector3.up, -dir).normalized;
                    at = followed + across * offset;
                },
                string.Format(CultureInfo.InvariantCulture,
                    "portrait of body {0} ({1:0.##} m long, reach {2:0.###} m, {3}), a {4} arc of {5:0.###} turn and {6:0} deg of elevation " +
                    "at {7:0.##} m ({8:0.#} body lengths, {9:0}% of the frame's height), lens {10:0} deg, subject a third off centre " +
                    "on the {11} with room ahead, {12:0.###} m/s along the arc at the peak{13}; {14}",
                    id, length, reach, swimmer ? "a swimmer" : "a sitter",
                    family == 0 ? "horizontal" : family == 1 ? "vertical" : "diagonal",
                    Mathf.Abs(azimuthSweep) / (2f * Mathf.PI), elevationSweep * Mathf.Rad2Deg, radius, radius / length,
                    100f * length / (2f * radius * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad)), fov,
                    lead > 0f ? "left" : "right", Arc(radius), notes, DepthOfField(radius, fov)),
                focusOn: id);

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, Subject = id };
        }

        /// <summary>The portrait's depth of field in words for a plan line.</summary>
        private static string DepthOfField(float distance, float fov) =>
            TheatreGrade.Current != null ? TheatreGrade.Current.DescribePortrait(distance, fov) : "no grade, so no depth of field";

        // ---------------------------------------------------------------- the floor

        /// <summary>
        /// Low over the sand along the slope, across the deepest hollow the plan can find inside
        /// the room, looking forward and down at the bed's angle; a truck of eight metres.
        /// </summary>
        public static Take Floor(Stage stage, float seconds, int hash)
        {
            FilmPlans.WorldBounds w = stage.World;
            float room = stage.Room;

            // The hollow: the lowest floor on a two-metre lattice inside four fifths of the room.
            Vector3 hollow = stage.Axis(0f);
            float lowest = float.PositiveInfinity;
            for (float dx = -0.8f * room; dx <= 0.8f * room; dx += 2f)
            for (float dz = -0.8f * room; dz <= 0.8f * room; dz += 2f)
            {
                if (dx * dx + dz * dz > 0.64f * room * room) continue;
                Vector3 p = stage.Axis(0f) + new Vector3(dx, 0f, dz);
                float f = w.FloorAt(p.x, p.z);
                if (f < lowest) { lowest = f; hollow = p; }
            }
            hollow.y = lowest;

            // Along the slope: the direction the floor rises fastest from the hollow.
            float bearing = (hash % 360) * Mathf.Deg2Rad, steepest = float.NegativeInfinity;
            for (int k = 0; k < 24; k++)
            {
                float b = k * 15f * Mathf.Deg2Rad;
                Vector3 p = hollow + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * 4f;
                float rise = w.FloorAt(p.x, p.z) - lowest;
                if (rise > steepest) { steepest = rise; bearing = b; }
            }

            var along = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
            float pass = 0.8f * FilmPlans.OrbitSpeedCeiling * seconds;
            Vector3 start = hollow - along * (0.5f * pass);
            Vector3 end = hollow + along * (0.5f * pass);
            start.y = w.FloorAt(start.x, start.z) + FilmPlans.Clearance + 0.6f;
            end.y = w.FloorAt(end.x, end.z) + FilmPlans.Clearance + 0.6f;

            float lift = 0f;
            for (int tries = 0; tries < 20; tries++)
            {
                Vector3 a = start + Vector3.up * lift, b = end + Vector3.up * lift;
                string why = PathClash(stage, u => Vector3.Lerp(a, b, u), -1);
                if (why == null) break;
                lift += 0.4f;
            }
            start += Vector3.up * lift;
            end += Vector3.up * lift;

            Vector3 gaze = along * 6f + Vector3.down * 1.6f;
            FilmPlans.Shot shot = FilmPlans.Shot.Custom("floor", w, seconds, 50f, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    eye = Vector3.Lerp(start, end, FilmPlans.Ease(u));
                    at = eye + gaze;
                },
                string.Format(CultureInfo.InvariantCulture,
                    "the floor: a {0:0.#} m truck up the slope (bearing {1:0} deg) across the hollow at {2:0.#} m, " +
                    "{3:0.#} m over the sand, {4:0.###} m/s at the peak{5}",
                    pass, bearing * Mathf.Rad2Deg, -lowest, FilmPlans.Clearance + 0.6f + lift,
                    pass / seconds / (1f - FilmPlans.EaseShare),
                    lift > 0f ? string.Format(CultureInfo.InvariantCulture, ", lifted {0:0.#} m clear of bodies and sand", lift) : ""));

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_ };
        }

        // ---------------------------------------------------------------- a still

        /// <summary>
        /// A held frame on a birth's parent at a portrait's lens and distance, with the child's
        /// landing spot in the frame when the rehearsal saw where it landed. The camera does not
        /// move; the parent does what it does, and the lens stays focused on its drawn centre.
        /// </summary>
        /// <remarks>
        /// Until the films review (2026-09-24) the frame was a 35 degree lens at the distance that
        /// put a dispersal's disc round the parent, which at round 47's five metres stood the eye
        /// about 17 m off, the parent a speck in the fog. Now the look is the parent's drawn
        /// centre where its drift carries it at the birth, or the middle of it and the child's
        /// spot; the bearing is square to the line between the two, so both stand at one depth
        /// and in one focus; and the distance is the portrait's, pushed out only as far as the
        /// child's spot and the parent's drift need to stay in the frame.
        /// </remarks>
        /// <param name="leadSeconds">How far into the take the birth comes, s.</param>
        /// <param name="childOffset">Where the rehearsal's child landed from its parent's root, or NaN in any component when unknown.</param>
        public static Take Hold(Stage stage, int subjectIndex, float seconds, float leadSeconds, Vector3 childOffset, int hash, string name)
        {
            long id = stage.Ids[subjectIndex];
            float reach = Mathf.Max(0.05f, stage.Reaches[subjectIndex]);
            Vector3 root0 = stage.Positions[subjectIndex];
            FilmPlans.Subject(stage.View, id, root0, reach, out Vector3 centre0, out float length);
            Vector3 rootToCentre = centre0 - root0;

            Vector3 drift = VelocityOf(stage.Live, id);
            leadSeconds = Mathf.Clamp(leadSeconds, 0f, seconds);
            Vector3 centreAtBirth = centre0 + drift * leadSeconds;
            bool child = FilmPlans.Shot.Finite(childOffset);
            Vector3 childSpot = child ? root0 + drift * leadSeconds + childOffset : centreAtBirth;
            Vector3 look = child ? 0.5f * (centreAtBirth + childSpot) : centreAtBirth;

            float fov = FilmPlans.PortraitLens;
            float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad), tanH = tanV * stage.Aspect;
            float inside = rootToCentre.magnitude + reach + BodyMargin + 0.1f;

            // What must stay in the frame round the look: the parent's half length, the child's
            // spot, and the parent's drift either side of the birth; a tenth of the frame kept
            // clear at each edge.
            float travel = Mathf.Max(leadSeconds, seconds - leadSeconds);
            Vector3 offset = child ? childSpot - centreAtBirth : Vector3.zero;
            var offsetFlat = new Vector2(offset.x, offset.z);
            float halfAcross = 0.5f * offsetFlat.magnitude + 0.5f * length + 0.3f + new Vector2(drift.x, drift.z).magnitude * travel;
            float halfUp = 0.5f * Mathf.Abs(offset.y) + 0.5f * length + 0.3f + Mathf.Abs(drift.y) * travel;
            float fits = Mathf.Max(halfAcross / (0.8f * tanH), halfUp / (0.8f * tanV));
            float portrait = FilmPlans.PortraitDistance(length, fov, FilmPlans.PortraitFill);
            float baseDistance = Mathf.Max(inside, Mathf.Max(portrait, fits));

            // Square to the child's line when there is one, else a bearing from the hash.
            float baseAzimuth = child && offsetFlat.magnitude > 0.05f
                ? Mathf.Atan2(offsetFlat.y, offsetFlat.x) + ((hash >> 5) % 2 == 0 ? 0.5f : -0.5f) * Mathf.PI
                : ((hash >> 5) % 360) * Mathf.Deg2Rad;

            // The parent where its drift carries it, for the check: at the take's start, the
            // birth and the end.
            bool NearParent(Vector3 eye)
            {
                foreach (float t in new[] { 0f, leadSeconds, seconds })
                {
                    if ((eye - (root0 + drift * t)).magnitude < reach + BodyMargin) return true;
                }
                return false;
            }

            float[] distanceScales = { 1f, 1.15f, 1.3f };
            float[] elevations = { 15f, 5f, 25f, -5f, 35f, -15f };
            float[] azimuthSteps = child ? new[] { 0f, 180f, 20f, -20f, 160f, -160f, 40f, -40f } : new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };

            float distance = baseDistance, azimuth = baseAzimuth, elevation = 15f * Mathf.Deg2Rad;
            int fewestAcross = int.MaxValue, clearSeen = 0;
            string clash = null;
            bool clear = false;

            // The first bearing clear of everything with no body across the lens is taken; failing
            // that, the clear one with the fewest across it among the first two dozen clear ones.
            bool Search()
            {
                foreach (float scale in distanceScales)
                foreach (float el in elevations)
                foreach (float da in azimuthSteps)
                {
                    float d = baseDistance * scale, a = baseAzimuth + da * Mathf.Deg2Rad, e = el * Mathf.Deg2Rad;
                    Vector3 eye = look + d * Direction(a, e);

                    string why = Clash(stage, eye, id);
                    if (why == null && NearParent(eye)) why = "in the parent";
                    if (why == null && child && (eye - childSpot).magnitude < BodyMargin + 0.5f) why = "on the child's spot";
                    if (why == null) why = Hidden(stage, eye, look, tanH);
                    if (why != null)
                    {
                        if (clash == null) clash = why;
                        continue;
                    }

                    int across = FilmPlans.Occluders(stage.Positions, stage.Reaches, stage.Ids, id, eye, look);
                    clearSeen++;
                    if (!clear || across < fewestAcross)
                    {
                        clear = true;
                        fewestAcross = across;
                        distance = d; azimuth = a; elevation = e;
                    }

                    if (fewestAcross == 0 || clearSeen >= 24) return true;
                }
                return false;
            }

            Search();

            Vector3 fixedEye = look + distance * Direction(azimuth, elevation);
            Vector3 fixedLook = look;

            string notes = "";
            if (distance != baseDistance || azimuth != baseAzimuth || elevation != 15f * Mathf.Deg2Rad)
            {
                notes += string.Format(CultureInfo.InvariantCulture, ", moved to clear: turned {0:0} deg, {1:0} deg of elevation, at {2:0.##} m (from {3:0.##})",
                    (azimuth - baseAzimuth) * Mathf.Rad2Deg, elevation * Mathf.Rad2Deg, distance, baseDistance);
            }
            if (!clear) notes += ", STILL CLASHES (" + clash + "): the film's per-frame walls take it from here";
            else if (fewestAcross > 0) notes += string.Format(CultureInfo.InvariantCulture, ", {0} bod{1} across the lens", fewestAcross, fewestAcross == 1 ? "y" : "ies");

            FilmPlans.Shot shot = FilmPlans.Shot.Custom(name, stage.World, seconds, fov, true,
                (float u, float dt, out Vector3 eye, out Vector3 at) => { eye = fixedEye; at = fixedLook; },
                string.Format(CultureInfo.InvariantCulture,
                    "a held frame on body {0} ({1:0.##} m long) at {2:0.##} m from the look ({3:0.#} body lengths), lens {4:0} deg, " +
                    "{5:0} deg {6}, {7}; the parent drifting {8:0.###} m/s{9}; {10}",
                    id, length, distance, distance / length, fov,
                    Mathf.Abs(elevation * Mathf.Rad2Deg), elevation >= 0f ? "down" : "up",
                    child ? string.Format(CultureInfo.InvariantCulture, "the child's spot {0:0.##} m off, square across the frame", offset.magnitude)
                          : "no child's spot known, the parent alone",
                    drift.magnitude, notes, DepthOfField(distance, fov)),
                focusOn: id);

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, Subject = id };
        }

        // ---------------------------------------------------------------- the canopy

        /// <summary>How long the descent's canopy take runs when the canopy stands in for the dolly, s.</summary>
        public const float CanopyDescentSeconds = 30f;

        /// <summary>
        /// The film's canopy shot as a take (the films review's second item, 2026-09-24): the eye
        /// 8 to 15 m under the densest column of bodies near the surface, looking up through them
        /// at Snell's window with the sun's bearing behind the leaves, one slow move. The director
        /// plays it for the arrival (a rise) and the descent (a sink) when
        /// <see cref="SafariOptions.Canopy"/> is on.
        /// </summary>
        public static Take Canopy(Stage stage, float seconds, FilmPlans.CanopyMove move, string name)
        {
            FilmPlans.Shot shot = FilmPlans.Shot.Canopy(name, stage.World, stage.Positions, stage.Reaches, stage.Ids,
                seconds, stage.Aspect, move, out Func<float, Vector3> eyeAt);
            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, EyeAt = eyeAt };
        }

        // ---------------------------------------------------------------- the colony

        /// <summary>
        /// A pull-back from a portrait's distance along the view axis until the clade's members
        /// fill the frame, or as far as the ceiling allows in the take's time, whichever is less.
        /// </summary>
        public static Take PullBack(Stage stage, List<int> members, int anchorIndex, float seconds, int hash)
        {
            Vector3 centre = stage.Centroid(members);
            float reach = Mathf.Max(0.05f, stage.Reaches[anchorIndex]);
            Vector3 anchor = stage.Positions[anchorIndex];

            var distances = new List<float>(members.Count);
            foreach (int i in members) distances.Add((stage.Positions[i] - centre).magnitude);
            distances.Sort();
            float spread = distances.Count > 0 ? distances[Mathf.Min(distances.Count - 1, (int)(0.85f * distances.Count))] : 2f;
            spread = Mathf.Max(1.5f, spread + reach);

            float fov = 40f;
            float tanV = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
            // Ten reaches, at least 2.5 m: the portrait's distance until the films review
            // (2026-09-24) brought portraits in to 1.5 to 3 body lengths; kept, so the colony's
            // pull-back is the one round 47's first safari planned.
            float r0 = Mathf.Max(2.5f, 10f * reach);
            float wanted = 1.1f * spread / Mathf.Min(tanV, tanV * stage.Aspect) + spread;

            // The eye's whole travel under the ceiling: the eased peak is 1/(1 - share) of the mean,
            // and the travel is the look's move plus the pull-back's, both at once. The first
            // safari counted the pull-back alone and let the look cross a colony tens of metres
            // wide in twenty seconds, which moved the camera at up to 5.3 m/s (2026-09-24).
            float peakShare = 1f / (1f - FilmPlans.EaseShare);
            float travel = 0.95f * FilmPlans.OrbitSpeedCeiling * seconds / peakShare;
            float r1Full = Mathf.Clamp(wanted, r0 + 1f, r0 + travel);
            Vector3 toCentre = centre - anchor;

            float baseAzimuth = ((hash >> 7) % 360) * Mathf.Deg2Rad;
            const float baseElevation = 20f;
            long anchorId = stage.Ids[anchorIndex];

            // The share of the way from the anchor to the members' centre the look may travel with
            // a given pull-back, the most that keeps the eye's whole move under the travel.
            float LookShare(Vector3 pull)
            {
                if (pull.magnitude >= travel) return 0f;
                if ((toCentre + pull).magnitude <= travel) return 1f;
                float lo = 0f, hi = 1f;
                for (int i = 0; i < 24; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    if ((mid * toCentre + pull).magnitude <= travel) lo = mid; else hi = mid;
                }
                return lo;
            }

            // The rock is kept out of the frame's cone as far as its half-height (a milder test
            // than the portrait's half-width: a colony round a table has the table beside it).
            // Re-planned rather than clamped: the pull-back's height (down to under the colony,
            // looking up at it and the surface's underside), its bearing and its length are
            // searched nearest first until the whole path is clear of the glass, the bed, the
            // reef's rock and the bodies, a metre and a half under the surface, with no rock
            // between the eye and the look.
            float[] pullScales = { 1f, 0.7f, 0.45f, 0.2f };
            float[] elevations = { 20f, 10f, 30f, 0f, -10f, 40f, -20f, -30f, -40f, -50f };
            float[] azimuthSteps = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

            int fewest = int.MaxValue;
            string clash = null;
            float r1 = r1Full, elevation = baseElevation * Mathf.Deg2Rad, azimuth = baseAzimuth, share = 0f;
            Vector3 dir = Direction(azimuth, elevation);

            bool Search()
            {
                foreach (float scale in pullScales)
                foreach (float el in elevations)
                foreach (float da in azimuthSteps)
                {
                    float r = r0 + (r1Full - r0) * scale;
                    float e = el * Mathf.Deg2Rad, a = baseAzimuth + da * Mathf.Deg2Rad;
                    Vector3 d = Direction(a, e);
                    float f = LookShare((r - r0) * d);
                    Vector3 look1 = anchor + f * toCentre;

                    int n = PathClashes(stage, u => Vector3.Lerp(anchor, look1, u) + Mathf.Lerp(r0, r, u) * d, anchorId,
                        u => Vector3.Lerp(anchor, look1, u), SurfaceMargin, out string why, tanV);
                    if (n >= fewest) continue;

                    fewest = n;
                    clash = why;
                    r1 = r; elevation = e; azimuth = a; dir = d; share = f;
                    if (n == 0) return true;
                }
                return false;
            }

            Search();

            string notes = string.Format(CultureInfo.InvariantCulture, ", from {0:0} deg of elevation at a bearing turned {1:0} deg",
                elevation * Mathf.Rad2Deg, (azimuth - baseAzimuth) * Mathf.Rad2Deg);
            if (elevation != baseElevation * Mathf.Deg2Rad || azimuth != baseAzimuth || r1 != r1Full)
                notes += string.Format(CultureInfo.InvariantCulture, " (re-planned clear from {0:0} deg, {1:0.##} m)", baseElevation, r1Full);
            if (fewest > 0) notes += ", STILL CLASHES (" + clash + ", " + fewest + " of 25 samples)";

            Vector3 fixedDir = dir;
            Vector3 lookEnd = anchor + share * toCentre;
            float peak = (share * toCentre + (r1 - r0) * dir).magnitude / seconds * peakShare;
            FilmPlans.Shot shot = FilmPlans.Shot.Custom("colony", stage.World, seconds, fov, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    float e = FilmPlans.Ease(u);
                    at = Vector3.Lerp(anchor, lookEnd, e);
                    eye = at + Mathf.Lerp(r0, r1, e) * fixedDir;
                },
                string.Format(CultureInfo.InvariantCulture,
                    "colony of {0} members: a pull-back from {1:0.##} m to {2:0.##} m (their spread wants {3:0.##} m), " +
                    "the look carried {4:0}% of the {5:0.#} m from body {6} to the members' centre, {7:0.###} m/s at the peak{8}",
                    members.Count, r0, r1, wanted, 100f * share, toCentre.magnitude, anchorId, peak, notes));

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, Subject = stage.Ids[anchorIndex] };
        }

        /// <summary>
        /// The chapter card: the disc from above, held. The eye stands a metre and a half under the
        /// surface over the axis and looks straight down, with a lens wide enough for the room.
        /// </summary>
        public static Take FromAbove(Stage stage, float seconds)
        {
            Vector3 eye0 = stage.Axis(-FilmPlans.Clearance - 0.5f);
            float floor = stage.World.FloorAt(eye0.x, eye0.z);
            float depth = Mathf.Max(5f, eye0.y - floor);
            float fov = Mathf.Clamp(2f * Mathf.Atan(stage.Room / depth) * Mathf.Rad2Deg, 40f, 100f);
            Vector3 look = eye0 + Vector3.down * 10f + Vector3.forward * 0.01f;

            FilmPlans.Shot shot = FilmPlans.Shot.Custom("chapter", stage.World, seconds, fov, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) => { eye = eye0; at = look; },
                string.Format(CultureInfo.InvariantCulture,
                    "the chapter card: the disc from above, held, {0:0.#} m of water under the eye, lens {1:0} deg", depth, fov));
            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_ };
        }

        /// <summary>
        /// The time station's shot: fixed by the tank's shape alone, never by the crowd, so the two
        /// takes thousands of seconds apart are one shot. Held.
        /// </summary>
        public static Take Fixed(Stage stage, float seconds, int hash, string name)
        {
            float bearing = ((hash >> 11) % 360) * Mathf.Deg2Rad;
            float depth = stage.World.Depth;
            Vector3 eye0 = stage.Axis(-0.35f * depth) + new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing)) * (0.85f * stage.Room);
            float floorThere = stage.World.FloorAt(eye0.x, eye0.z) + FilmPlans.Clearance + 1f;
            if (eye0.y < floorThere) eye0.y = floorThere;
            Vector3 look = stage.Axis(-0.4f * depth);

            FilmPlans.Shot shot = FilmPlans.Shot.Custom(name, stage.World, seconds, 60f, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) => { eye = eye0; at = look; },
                string.Format(CultureInfo.InvariantCulture,
                    "a fixed wide shot from bearing {0:0} deg at {1:0.#} m down, looking at the axis; the same eye at both seconds",
                    bearing * Mathf.Rad2Deg, -eye0.y));
            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_ };
        }

        // ---------------------------------------------------------------- helpers

        public static Vector3 Direction(float azimuth, float elevation) =>
            new Vector3(Mathf.Cos(elevation) * Mathf.Cos(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(azimuth));

        /// <summary>A body's root velocity from the solver, m/s, or zero.</summary>
        public static Vector3 VelocityOf(TheatreDynamicsReplay live, long id)
        {
            if (live?.Sim == null || !live.Sim.TryPose(id, out Evosim.Dynamics.Creature body) || body == null ||
                body.Velocity == null || body.Velocity.Length < 3)
            {
                return Vector3.zero;
            }

            var v = new Vector3((float)body.Velocity[0], (float)body.Velocity[1], (float)body.Velocity[2]);
            return FilmPlans.Shot.Finite(v) ? v : Vector3.zero;
        }
    }
}
