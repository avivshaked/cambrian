using System;
using System.Globalization;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The two shots of a nursery tank's film that follow one swimmer: <c>follow</c> keeps the swimmer in a portrait's
    /// frame for as long as the film runs, and <c>nose</c> is a short pass on its nose part, fixed in the swimmer's own
    /// frame. Filmed only from a nursery tank (<c>EVOSIM_THEATRE_NURSERY</c>); <c>theatre-film.ps1 -Nursery ... -Shots
    /// follow,nose -Member 8</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The swimmer is named by its pool member</b> (<c>EVOSIM_THEATRE_FILM_MEMBER</c>, <c>-Member</c>): the row of the
    /// request's pool the swimmer was made from, which is <c>members[].pool</c> in <c>episode.json</c>.
    /// <see cref="NurseryEpisodeWorld.TryMemberBody"/> turns it into the body id, which is the swimmer's slot. The film's
    /// window in tank seconds is the film's own: it starts at <c>-At</c> and runs <c>-Seconds</c>, or to <c>-To</c>.
    /// </para>
    /// <para>
    /// <b>follow.</b> The camera stands at the side of the swimmer's heading (the horizontal part of the line from its
    /// root to its nose tip, smoothed over three seconds, or its velocity when it has no nose), ten degrees above the
    /// horizontal, on the side with more room from the glass, at the distance at which the swimmer's drawn box, its
    /// longest side smoothed over two seconds, fills <c>-Fill</c> of the frame's height (default 0.7, 0.3 to 0.8) through
    /// the portrait's lens. The subject sits a third of the half width off centre on the side it came from, with room
    /// ahead, and the lens is focused on it at every frame, as the portrait is. The film's walls keep the eye inside the
    /// glass, over the bed, under the surface and a quarter metre outside every body's reach, the swimmer's own included;
    /// where that reach is the limit the distance is held there and the fill falls short, and the log says how often.
    /// Nothing about the leaves is steered: the camera moves through the patch with the swimmer and the film's push moves
    /// it off any leaf it would touch.
    /// </para>
    /// <para>
    /// <b>nose.</b> The nose is the swimmer's part whose cell type is <see cref="Evosim.Core.CellTypeIds.Nose"/>; the point looked at is
    /// its sensing tip, <c>MouthPoint.Local</c> mapped through the part's recorded pose, which is the point the pit is
    /// drawn at and the point the world reads the smell from. The eye is fixed in the nose part's own frame: forty degrees
    /// off the nose's axis, on the side that is highest at the shot's first frame, at the distance at which the nose's
    /// length fills a third of the frame's height through a 40 degree lens, or further where the body's reach would
    /// push the eye out (the distance is solved so that the eye stands outside that reach from the start). Because the
    /// grafted nose is fixed to the root, the eye turns with the swimmer and the nose does not move in the frame. The
    /// lens is focused on the tip. It starts at <c>-NoseAt</c> (tank seconds, default the film's start) and runs
    /// <c>-NoseSeconds</c> (default 3).
    /// </para>
    /// </remarks>
    public static class FilmNurseryShots
    {
        /// <summary>The shots' names, as <c>EVOSIM_THEATRE_FILM_SHOTS</c> takes them.</summary>
        public static readonly string[] Names = { "follow", "nose" };

        /// <summary>Whether a shot's name is one of the nursery follow shots.</summary>
        public static bool Knows(string name) => Array.IndexOf(Names, name) >= 0;

        /// <summary>The pool member to film, a row of the request's pool: <c>theatre-film.ps1 -Member</c>.</summary>
        public const string MemberVariable = "EVOSIM_THEATRE_FILM_MEMBER";

        /// <summary>How much of the frame's height the follow shot's swimmer fills, 0.3 to 0.8: <c>-Fill</c>.</summary>
        public const string FillVariable = "EVOSIM_THEATRE_FILM_FILL";

        /// <summary>How long the nose pass runs, s: <c>-NoseSeconds</c>.</summary>
        public const string NoseSecondsVariable = "EVOSIM_THEATRE_FILM_NOSE_SECONDS";

        /// <summary>The tank second the nose pass starts at: <c>-NoseAt</c>. The film's start when unset.</summary>
        public const string NoseAtVariable = "EVOSIM_THEATRE_FILM_NOSE_AT";

        /// <summary>The follow shot's fill when none is given, and the range a fill may take.</summary>
        public const float DefaultFill = 0.7f, LeastFill = 0.3f, MostFill = 0.8f;

        /// <summary>The follow shot's height above the horizontal, degrees.</summary>
        public const float FollowElevation = 10f;

        /// <summary>Time constants of the follow shot's smoothing: the subject's centre, its drawn length, its heading, s.</summary>
        public const float CentreSeconds = 1.5f, LengthSeconds = 2f, HeadingSeconds = 3f;

        /// <summary>The nose pass's lens, degrees of vertical field.</summary>
        public const float NoseLens = 40f;

        /// <summary>The share of the frame's height the nose's length fills in the nose pass.</summary>
        public const float NoseFill = 0.33f;

        /// <summary>How far off the nose's axis the nose pass's eye stands, degrees.</summary>
        public const float NoseCone = 40f;

        /// <summary>How far outside a body's reach the film keeps the eye (<c>FilmPlans.Shot.OffTheBodies</c>), plus a margin, m.</summary>
        private const float EyeMargin = 0.25f + 0.1f;

        /// <summary>
        /// Plans a shot over the tank at the second it shows now, or says why it cannot.
        /// </summary>
        /// <param name="filmStart">The tank second the film starts at.</param>
        /// <param name="filmSeconds">The film's length in tank seconds.</param>
        /// <param name="aspect">The frame's width over its height.</param>
        /// <param name="startSecond">The tank second the shot's first frame is at.</param>
        /// <param name="lengthSeconds">The shot's length in tank seconds.</param>
        public static FilmPlans.Shot Plan(
            string name, NurseryEpisodeWorld tank, FilmPlans.WorldBounds world, double filmStart, double filmSeconds,
            float aspect, out double startSecond, out double lengthSeconds, out string refusal)
        {
            refusal = null;
            startSecond = filmStart;
            lengthSeconds = filmSeconds;

            if (tank == null)
            {
                refusal = "the follow shots are filmed from a nursery tank";
                return null;
            }

            string text = Environment.GetEnvironmentVariable(MemberVariable);
            if (string.IsNullOrWhiteSpace(text) ||
                !int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int member) || member < 0)
            {
                refusal = MemberVariable + " (-Member) names the pool member to film, a row of the tank's pool: '" + text + "' is not one";
                return null;
            }

            if (!tank.TryMemberBody(member, out long id))
            {
                refusal = "pool member " + member + " is not in the tank; the tank holds " + tank.MembersLine();
                return null;
            }

            if (!tank.TryIndexOf(id, out int index))
            {
                refusal = "pool member " + member + " (body " + id + ") is not drawn at " +
                          filmStart.ToString("0.###", CultureInfo.InvariantCulture) + " s: dead, or not recorded there";
                return null;
            }

            if (name == "follow")
            {
                float fill = DefaultFill;
                text = Environment.GetEnvironmentVariable(FillVariable);
                if (!string.IsNullOrWhiteSpace(text) &&
                    (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out fill) ||
                     fill < LeastFill || fill > MostFill))
                {
                    refusal = FillVariable + " (-Fill): '" + text + "' is not a share of the frame's height from " + LeastFill + " to " + MostFill;
                    return null;
                }

                return PlanFollow(tank, world, id, member, index, fill, aspect, (float)filmSeconds);
            }

            double seconds = 3d;
            text = Environment.GetEnvironmentVariable(NoseSecondsVariable);
            if (!string.IsNullOrWhiteSpace(text) &&
                (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || !(seconds > 0d) || seconds > 60d))
            {
                refusal = NoseSecondsVariable + " (-NoseSeconds): '" + text + "' is not a length from 0 to 60 s";
                return null;
            }

            double at = filmStart;
            text = Environment.GetEnvironmentVariable(NoseAtVariable);
            if (!string.IsNullOrWhiteSpace(text) &&
                !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out at))
            {
                refusal = NoseAtVariable + " (-NoseAt): '" + text + "' is not a second";
                return null;
            }

            if (at < filmStart - 1e-6d || at >= filmStart + filmSeconds)
            {
                refusal = "the nose pass starts at " + at.ToString("0.###", CultureInfo.InvariantCulture) + " s, outside the film's " +
                          filmStart.ToString("0.###", CultureInfo.InvariantCulture) + " s to " +
                          (filmStart + filmSeconds).ToString("0.###", CultureInfo.InvariantCulture) + " s";
                return null;
            }

            startSecond = at;
            lengthSeconds = Math.Min(seconds, filmStart + filmSeconds - at);
            return PlanNose(tank, world, id, member, index, (float)lengthSeconds, out refusal);
        }

        // ---------------------------------------------------------------- follow

        private static FilmPlans.Shot PlanFollow(
            NurseryEpisodeWorld tank, FilmPlans.WorldBounds world, long id, int member, int index, float fill, float aspect, float seconds)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            LiveWorldView view = tank.View;
            float fov = FilmPlans.PortraitLens;
            float tanHalf = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
            float elevation = FollowElevation * Mathf.Deg2Rad;
            float reach = Mathf.Max(0.05f, SnapshotCamera.ReachOf(tank.PhenotypeOf(index)));

            Vector3 root0 = FilmPlans.Shot.Where(tank, index, view);
            FilmPlans.Subject(view, id, root0, reach, out Vector3 centre0, out float length0);
            Vector3 rootToCentre = centre0 - root0;
            float inside = rootToCentre.magnitude + reach + EyeMargin;
            float distance0 = Mathf.Max(inside, length0 / (fill * 2f * tanHalf));

            // How many leaves share the patch at the shot's start, for the plan's line.
            int leaves = 0;
            for (int i = 0; i < tank.BodyCount; i++)
            {
                if (tank.IdAt(i) >= NurseryEpisodeWorld.LeafIdOffset && (tank.PositionOf(i) - centre0).magnitude < 3f) leaves++;
            }

            Vector3 followed = centre0;
            bool hasFollowed = false;
            Vector3 heading = Vector3.right;
            bool hasHeading = false;
            float lengthSmooth = length0;
            float sideSign = 1f;
            bool sideChosen = false;
            int frames = 0, clamped = 0, decile = -1;

            FilmPlans.Shot.CameraPath path = (float u, float dt, out Vector3 eye, out Vector3 look) =>
            {
                dt = Mathf.Max(1e-3f, dt);

                // The drawn centre and the drawn length, or the root and the planned offset when it is not drawn.
                Vector3 now = followed;
                float lengthNow = lengthSmooth;
                if (FilmPlans.DrawnBounds(view, id, out Bounds drawn))
                {
                    now = drawn.center;
                    lengthNow = Mathf.Max(drawn.size.x, Mathf.Max(drawn.size.y, drawn.size.z));
                }
                else
                {
                    Transform rootTransform = view?.RootOf(id);
                    if (rootTransform != null && FilmPlans.Shot.Finite(rootTransform.position)) now = rootTransform.position + rootToCentre;
                }

                followed = hasFollowed ? Vector3.Lerp(followed, now, 1f - Mathf.Exp(-dt / CentreSeconds)) : now;
                hasFollowed = true;
                lengthSmooth = Mathf.Clamp(
                    Mathf.Lerp(lengthSmooth, lengthNow, 1f - Mathf.Exp(-dt / LengthSeconds)), 0.75f * length0, 1.35f * length0);

                // The heading: the horizontal part of the root-to-tip line, else the velocity.
                Vector3 axis = tank.TryNose(id, out Vector3 tip, out Quaternion noseTurn, out Vector3 noseLocal, out Vector3 rootNow)
                    ? tip - rootNow
                    : tank.VelocityOf(id);
                axis.y = 0f;
                if (axis.sqrMagnitude > 1e-6f)
                {
                    axis.Normalize();
                    heading = hasHeading ? Vector3.Slerp(heading, axis, 1f - Mathf.Exp(-dt / HeadingSeconds)) : axis;
                    hasHeading = true;
                }

                Vector3 side = Vector3.Cross(Vector3.up, heading).normalized;
                if (!sideChosen)
                {
                    sideSign = world.RoomAround(followed + side * distance0) >= world.RoomAround(followed - side * distance0) ? 1f : -1f;
                    sideChosen = true;
                }

                Vector3 dir = side * (sideSign * Mathf.Cos(elevation)) + Vector3.up * Mathf.Sin(elevation);
                float distance = lengthSmooth / (fill * 2f * tanHalf);
                if (distance < inside)
                {
                    distance = inside;
                    clamped++;
                }

                eye = followed + dir * distance;

                // A third of the half width off centre, on the side the subject is heading to, so there is room ahead of it.
                Vector3 right = Vector3.Cross(Vector3.up, -dir).normalized;
                float lead = Vector3.Dot(heading, right) >= 0f ? 1f : -1f;
                look = followed + right * (lead * distance * tanHalf * aspect / 3f);

                frames++;
                int tenth = Mathf.Clamp((int)(u * 10f), 0, 10);
                if (tenth != decile)
                {
                    decile = tenth;
                    Debug.Log(string.Format(inv,
                        "[Theatre] nursery follow: {0:0}% through the shot, the swimmer fills {1:0.00} of the frame's height at {2:0.00} m; " +
                        "{3} of {4} frame(s) so far held off the body's reach",
                        100f * u, lengthSmooth / (2f * distance * tanHalf), distance, clamped, frames));
                }
            };

            string plan = string.Format(inv,
                "follow of pool member {0} (body {1}), {2:0.##} m long as drawn, reach {3:0.###} m: side-on to its heading, {4:0} degrees up, " +
                "{5:0.##} m off at the start so that it fills {6:0.##} of the frame's height (held no nearer than {7:0.##} m for its reach), " +
                "lens {8:0} degrees, the subject a third off centre with room ahead, focused on at every frame; {9} leaf(ves) within 3 m at the start",
                member, id, length0, reach, FollowElevation, distance0, fill, inside, fov, leaves);

            return FilmPlans.Shot.Custom("follow", world, seconds, fov, true, path, plan, false, id);
        }

        // ---------------------------------------------------------------- nose

        private static FilmPlans.Shot PlanNose(
            NurseryEpisodeWorld tank, FilmPlans.WorldBounds world, long id, int member, int index, float seconds, out string refusal)
        {
            refusal = null;
            CultureInfo inv = CultureInfo.InvariantCulture;

            if (!tank.TryNose(id, out Vector3 tip, out Quaternion turn, out Vector3 localTip, out Vector3 root))
            {
                refusal = "pool member " + member + " (body " + id + ") has no nose part, or is not drawn";
                return null;
            }

            float noseLength = 2f * localTip.magnitude;
            if (!(noseLength > 1e-4f))
            {
                refusal = "pool member " + member + "'s nose has no length (its sensing tip is at the part's centre)";
                return null;
            }

            float fov = NoseLens;
            float tanHalf = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
            float wanted = noseLength / (NoseFill * 2f * tanHalf);
            float keep = SnapshotCamera.ReachOf(tank.PhenotypeOf(index)) + EyeMargin;

            // The eye's direction in the nose part's frame: NoseCone off the tip's axis, turned about it to the
            // side that is highest in the world at the shot's start (eight sides tried).
            Vector3 axisLocal = localTip.normalized;
            Vector3 helper = Mathf.Abs(axisLocal.x) < 0.9f ? Vector3.right : Vector3.up;
            Vector3 sideLocal = Vector3.Cross(axisLocal, helper).normalized;
            Vector3 upLocal = Vector3.Cross(axisLocal, sideLocal);
            float cone = NoseCone * Mathf.Deg2Rad;
            Vector3 direction = axisLocal;
            float highest = float.NegativeInfinity;

            for (int k = 0; k < 8; k++)
            {
                float phi = k * (Mathf.PI / 4f);
                Vector3 candidate = Mathf.Cos(cone) * axisLocal +
                                    Mathf.Sin(cone) * (Mathf.Cos(phi) * sideLocal + Mathf.Sin(phi) * upLocal);
                float up = (turn * candidate).y;
                if (up > highest)
                {
                    highest = up;
                    direction = candidate;
                }
            }

            // The nearest the eye can stand and still be outside the swimmer's own reach sphere (the film pushes it out
            // otherwise): the positive root of |tipFromRoot + d u|^2 = keep^2.
            Vector3 tipFromRoot = tip - root;
            Vector3 world3 = turn * direction;
            float along = Vector3.Dot(tipFromRoot, world3);
            float excess = tipFromRoot.sqrMagnitude - keep * keep;
            float minimum = excess < 0f ? -along + Mathf.Sqrt(Mathf.Max(0f, along * along - excess)) : 0f;
            float distance = Mathf.Clamp(Mathf.Max(wanted, minimum), 0.1f, 3f);

            Vector3 lastEye = tip + turn * (direction * distance);
            Vector3 lastLook = tip;

            FilmPlans.Shot.CameraPath path = (float u, float dt, out Vector3 eye, out Vector3 look) =>
            {
                if (tank.TryNose(id, out Vector3 tipNow, out Quaternion turnNow, out Vector3 localNow, out Vector3 rootNow))
                {
                    lastEye = tipNow + turnNow * (direction * distance);
                    lastLook = tipNow;
                }

                eye = lastEye;
                look = lastLook;
            };

            string plan = string.Format(inv,
                "nose of pool member {0} (body {1}), the nose {2:0.###} m long: the eye fixed in the nose part's own frame {3:0} degrees off its axis " +
                "at {4:0.##} m ({5:0.##} m wanted for the nose to fill {6:0.##} of the frame's height, {7:0.##} m least to stand outside the body's " +
                "reach of {8:0.##} m), lens {9:0} degrees, looking at the sensing tip and focused on it",
                member, id, noseLength, NoseCone, distance, wanted, NoseFill, minimum, keep - EyeMargin, fov);

            return FilmPlans.Shot.Custom("nose", world, seconds, fov, true, path, plan, false, -1L);
        }
    }
}
