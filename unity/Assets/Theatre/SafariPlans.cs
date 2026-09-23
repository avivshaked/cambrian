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
    /// surface and outside every other body's reach at the scene's second. A plan that fails is
    /// lifted, then pulled back, in small steps, and the plan's line says how far. The film's
    /// per-frame walls stay on behind it for what moves after the check.
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
        }

        // ---------------------------------------------------------------- the check

        /// <summary>Why a planned eye would clip, or null when it is clear.</summary>
        public static string Clash(Stage stage, Vector3 eye, long ignore = -1)
        {
            FilmPlans.WorldBounds w = stage.World;

            if (w.Outside(eye)) return "outside the glass";
            if (w.Tank && new Vector2(eye.x - w.Axis.x, eye.z - w.Axis.y).magnitude > w.Radius - FilmPlans.GlassClearance) return "at the glass";
            if (eye.y > -FilmPlans.Clearance) return "at the surface";
            if (eye.y < w.FloorAt(eye.x, eye.z) + FilmPlans.Clearance) return "in the bed";

            for (int i = 0; i < stage.Positions.Count; i++)
            {
                if (stage.Ids[i] == ignore) continue;
                if ((eye - stage.Positions[i]).magnitude < stage.Reaches[i] + BodyMargin) return "in body " + stage.Ids[i];
            }

            return null;
        }

        /// <summary>The first clash along a path sampled at 25 points, or null.</summary>
        private static string PathClash(Stage stage, Func<float, Vector3> eyeAt, long ignore)
        {
            for (int k = 0; k <= 24; k++)
            {
                string why = Clash(stage, eyeAt(k / 24f), ignore);
                if (why != null) return why + " at " + (k * 100 / 24) + "% of the path";
            }
            return null;
        }

        // ---------------------------------------------------------------- arrival

        /// <summary>
        /// Outside the glass at the surface, the rim a line and the crowd a haze below it; a
        /// ten-second hold with a push of a metre and a half, linear, since it is a drift.
        /// </summary>
        public static Take Arrival(Stage stage, float seconds, int hash)
        {
            float bearing = (hash % 360) * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
            float outside = stage.Room + FilmPlans.GlassClearance + 4f;
            float push = 1.5f;

            Vector3 crowd = stage.Centroid(null);
            Vector3 start = stage.Axis(-0.6f) + outward * outside;
            Vector3 end = start - outward * push;
            Vector3 look = stage.Axis(Mathf.Min(-2f, 0.5f * crowd.y));

            FilmPlans.Shot shot = FilmPlans.Shot.Custom("arrival", stage.World, seconds, 50f, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    eye = Vector3.Lerp(start, end, Mathf.Clamp01(u));
                    at = look + (eye - start);
                },
                string.Format(CultureInfo.InvariantCulture,
                    "arrival from outside the glass at bearing {0:0} deg, {1:0.#} m from the axis at 0.6 m under the waterline, " +
                    "a {2:0.#} m linear push over {3:0} s ({4:0.###} m/s)",
                    bearing * Mathf.Rad2Deg, outside, push, seconds, push / seconds),
                mayLeaveTheGlass: true);

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_ };
        }

        // ---------------------------------------------------------------- descent

        /// <summary>
        /// A dolly down through the lit band on the shallow side, the fog thickening and the sand
        /// rising ahead, ending a metre above the clearance over the floor. The look keeps its
        /// bearing (outward and down), so nothing pans. The length comes from the ceiling.
        /// </summary>
        public static Take Descent(Stage stage, int hash, float mostSeconds = 120f)
        {
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

            float seconds = Mathf.Max(20f, length / mean);

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

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_ };
        }

        // ---------------------------------------------------------------- portrait

        /// <summary>
        /// A named body a third of the frame, orbited a quarter turn (a sitter) or two fifths (a
        /// swimmer), the subject a third off centre with room ahead of it, the lens from the
        /// radius, lit from one side by the portrait's back light and fill.
        /// </summary>
        public static Take Portrait(Stage stage, int subjectIndex, float seconds, bool swimmer, int cladeHash)
        {
            long id = stage.Ids[subjectIndex];
            float reach = Mathf.Max(0.05f, stage.Reaches[subjectIndex]);
            Vector3 centre0 = stage.Positions[subjectIndex];

            float radius = Mathf.Max(2.5f, 10f * reach) * (swimmer ? 1.3f : 1f);
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
            float allowed = Mathf.Max(0.12f, FilmPlans.OrbitSpeedCeiling - driftSpeed);
            if (Arc(radius) > allowed)
            {
                float cut = allowed / Arc(radius);
                azimuthSweep *= cut;
                elevationSweep *= cut;
                notes += string.Format(CultureInfo.InvariantCulture, ", the arc cut to {0:0.##} of itself for the ceiling with {1:0.###} m/s of drift", cut, driftSpeed);
            }

            // The check: lift in five-degree steps, then pull back by fifteen percent, until every
            // sample of the ring is clear at the scene's second.
            Vector3 EyeAt(float u, float r, float el0) =>
                centre0 + r * Direction(azimuth0 + azimuthSweep * FilmPlans.Ease(u), el0 + elevationSweep * FilmPlans.Ease(u));

            int lifts = 0, pulls = 0;
            string clash = PathClash(stage, u => EyeAt(u, radius, elevation0), id);
            while (clash != null && lifts < 6) { lifts++; elevation0 += 5f * Mathf.Deg2Rad; clash = PathClash(stage, u => EyeAt(u, radius, elevation0), id); }
            while (clash != null && pulls < 6) { pulls++; radius *= 1.15f; clash = PathClash(stage, u => EyeAt(u, radius, elevation0), id); }
            if (lifts > 0) notes += string.Format(CultureInfo.InvariantCulture, ", lifted {0} deg", 5 * lifts);
            if (pulls > 0) notes += string.Format(CultureInfo.InvariantCulture, ", pulled back to {0:0.##} m", radius);
            if (clash != null) notes += ", STILL CLASHES (" + clash + "): the film's per-frame walls take it from here";

            // The lens from the radius: the body's diameter a third of the frame's height.
            float fov = Mathf.Clamp(2f * Mathf.Atan(3f * reach / radius) * Mathf.Rad2Deg, 12f, 50f);
            float tanH = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * stage.Aspect;

            // A third off centre, on the side the subject came from, so it swims into the frame.
            Vector3 firstEye = EyeAt(0f, radius, elevation0);
            Vector3 right = Vector3.Cross(Vector3.up, (centre0 - firstEye).normalized).normalized;
            float lead = Vector3.Dot(drift, right) >= 0f ? 1f : -1f;
            float offset = lead * radius * tanH / 3f;

            Vector3 followed = centre0;
            bool hasFollowed = false;
            LiveWorldView view = stage.View;
            TheatreDynamicsReplay live = stage.Live;
            float r0 = radius, el = elevation0;

            FilmPlans.Shot shot = FilmPlans.Shot.Custom("portrait", stage.World, seconds, fov, true,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    Vector3 now = followed;
                    Transform root = view?.RootOf(id);
                    if (root != null && FilmPlans.Shot.Finite(root.position)) now = root.position;
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
                    "portrait of body {0} (reach {1:0.###} m, {2}), a {3} arc of {4:0.###} turn and {5:0} deg of elevation at {6:0.##} m, " +
                    "lens {7:0} deg, subject a third off centre on the {8} with room ahead, {9:0.###} m/s along the arc at the peak{10}",
                    id, reach, swimmer ? "a swimmer" : "a sitter",
                    family == 0 ? "horizontal" : family == 1 ? "vertical" : "diagonal",
                    Mathf.Abs(azimuthSweep) / (2f * Mathf.PI), elevationSweep * Mathf.Rad2Deg, radius, fov,
                    lead > 0f ? "left" : "right", Arc(radius), notes));

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, Subject = id };
        }

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
        /// A held frame on a body: the birth's parent, with room for the child's disc. The camera
        /// does not move; the parent does what it does.
        /// </summary>
        public static Take Hold(Stage stage, int subjectIndex, float seconds, float roomAround, int hash, string name)
        {
            long id = stage.Ids[subjectIndex];
            Vector3 at0 = stage.Positions[subjectIndex];
            float reach = Mathf.Max(0.05f, stage.Reaches[subjectIndex]);
            float half = Mathf.Max(1.2f, roomAround + reach);
            float fov = 35f;
            float distance = half / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
            float azimuth = ((hash >> 5) % 360) * Mathf.Deg2Rad;
            float elevation = 15f * Mathf.Deg2Rad;

            int lifts = 0, turns = 0;
            Vector3 eye0 = at0 + distance * Direction(azimuth, elevation);
            string clash = Clash(stage, eye0, id);
            while (clash != null && lifts < 8) { lifts++; elevation += 6f * Mathf.Deg2Rad; eye0 = at0 + distance * Direction(azimuth, elevation); clash = Clash(stage, eye0, id); }
            while (clash != null && turns < 12) { turns++; azimuth += 30f * Mathf.Deg2Rad; eye0 = at0 + distance * Direction(azimuth, elevation); clash = Clash(stage, eye0, id); }

            Vector3 fixedEye = eye0;
            FilmPlans.Shot shot = FilmPlans.Shot.Custom(name, stage.World, seconds, fov, true,
                (float u, float dt, out Vector3 eye, out Vector3 at) => { eye = fixedEye; at = at0; },
                string.Format(CultureInfo.InvariantCulture,
                    "a held frame on body {0} at {1:0.##} m, {2:0} deg down, {3:0.#} m of room round it, lens {4:0} deg{5}{6}",
                    id, distance, elevation * Mathf.Rad2Deg, half, fov,
                    lifts + turns > 0 ? string.Format(CultureInfo.InvariantCulture, ", moved {0} step(s) to clear", lifts + turns) : "",
                    clash != null ? ", STILL CLASHES (" + clash + ")" : ""));

            return new Take { Shot = shot, Seconds = seconds, Plan = shot.Plan_, Subject = id };
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
            // The portrait's own distance for this body, so the pull-back starts where a portrait sat.
            float r0 = Mathf.Max(2.5f, 10f * reach);
            float wanted = 1.1f * spread / Mathf.Min(tanV, tanV * stage.Aspect) + spread;
            float most = r0 + 0.8f * FilmPlans.OrbitSpeedCeiling * seconds;
            float r1 = Mathf.Clamp(wanted, r0 + 1f, most);

            float azimuth = ((hash >> 7) % 360) * Mathf.Deg2Rad;
            float elevation = 20f * Mathf.Deg2Rad;
            Vector3 dir = Direction(azimuth, elevation);

            // The look travels from the anchor to the members' centre as the eye pulls back, so the
            // colony gathers round the body the portrait showed.
            string notes = "";
            int lifts = 0;
            string clash = PathClash(stage, u => Vector3.Lerp(anchor, centre, u) + Mathf.Lerp(r0, r1, u) * dir, stage.Ids[anchorIndex]);
            while (clash != null && lifts < 8)
            {
                lifts++;
                elevation += 5f * Mathf.Deg2Rad;
                dir = Direction(azimuth, elevation);
                Vector3 d = dir;
                clash = PathClash(stage, u => Vector3.Lerp(anchor, centre, u) + Mathf.Lerp(r0, r1, u) * d, stage.Ids[anchorIndex]);
            }
            if (lifts > 0) notes += string.Format(CultureInfo.InvariantCulture, ", lifted {0} deg", 5 * lifts);
            if (clash != null) notes += ", STILL CLASHES (" + clash + ")";

            Vector3 fixedDir = dir;
            FilmPlans.Shot shot = FilmPlans.Shot.Custom("colony", stage.World, seconds, fov, false,
                (float u, float dt, out Vector3 eye, out Vector3 at) =>
                {
                    float e = FilmPlans.Ease(u);
                    at = Vector3.Lerp(anchor, centre, e);
                    eye = at + Mathf.Lerp(r0, r1, e) * fixedDir;
                },
                string.Format(CultureInfo.InvariantCulture,
                    "colony of {0} members: a pull-back from {1:0.##} m to {2:0.##} m (their spread wants {3:0.##} m), " +
                    "{4:0.###} m/s at the peak{5}",
                    members.Count, r0, r1, wanted, (r1 - r0) / seconds / (1f - FilmPlans.EaseShare), notes));

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
