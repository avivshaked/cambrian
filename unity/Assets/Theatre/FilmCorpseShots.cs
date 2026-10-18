using System;
using System.Collections.Generic;
using System.Globalization;
using Evosim.Farm;
using UnityEngine;
using static Evosim.Theatre.FilmPlans;

namespace Evosim.Theatre
{
    /// <summary>
    /// The corpse shots (D127), filmed from a farm film window: <c>sink</c> follows a fresh corpse down
    /// through the water, and <c>vanish</c> holds on a settled one until the run removes it. Both
    /// are made for a time-lapse (<c>EVOSIM_THEATRE_FILM_SPEED</c>, <c>theatre-film.ps1 -Speed</c>):
    /// a corpse sinks a few centimetres a second and leaves the world over hours, so at the run's own
    /// pace neither shot shows anything (the owner, 2026-09-28).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The corpse is named or chosen.</b> <c>EVOSIM_THEATRE_FILM_CORPSE</c> names it by the id of the
    /// body it was. Without it, <c>sink</c> takes the largest corpse founded in the shot's first
    /// quarter, in the water two to fifteen metres over the bed and standing at the shot's end, and
    /// <c>vanish</c> the largest, as drawn, of the corpses settled at the shot's start that leave
    /// between 72 and 97 per cent of the way through it holding no more than their floor's share, so
    /// one the run dissolved and not one a mouth took, and one seen whole before its fade begins.
    /// </para>
    /// <para>
    /// <b>sink</b> follows the corpse down from a few of its lengths away, across its drift and a
    /// little above it, so it stays large in the middle of the frame. The bed in the lower frame,
    /// rising as the corpse nears it, and the living bodies that hold their depth are the fixed
    /// references (the owner's rule that something in the frame must say what moved). The first
    /// cut kept the eye level at the middle of the fall and moved it sideways alone, which fitted
    /// an eight-metre fall in the frame and drew a 0.3 m corpse twenty pixels across in the dark.
    /// </para>
    /// <para>
    /// <b>vanish</b> never moves. Its eye stands a few of the corpse's lengths off, looking down on
    /// it as steeply as the metre it keeps over the bed asks, at the bearing out of sixteen whose
    /// line to the corpse the fewest living bodies cross over the shot, clear of the glass and the
    /// rock. The first cut stood 2.5 m off at 25 degrees and drew a 0.13 m corpse forty pixels across
    /// among the caustics.
    /// </para>
    /// </remarks>
    public static class FilmCorpseShots
    {
        /// <summary>The two shots' names, as <c>EVOSIM_THEATRE_FILM_SHOTS</c> takes them.</summary>
        public static readonly string[] Names = { "sink", "vanish" };

        /// <summary>Whether a shot's name is one of the corpse shots.</summary>
        public static bool Knows(string name) => Array.IndexOf(Names, name) >= 0;

        /// <summary>The lens of both shots, degrees of vertical field.</summary>
        public const float FieldOfView = 40f;

        /// <summary>How far above the corpse <c>sink</c> looks down from, degrees: enough for the bed to show below it.</summary>
        private const float SinkElevation = 15f;

        /// <summary>The least that <c>vanish</c> looks down on the corpse, degrees; more where the bed asks it.</summary>
        private const float VanishElevation = 25f;

        private static readonly List<FilmWindowCorpsePlace> Places = new List<FilmWindowCorpsePlace>();

        /// <summary>
        /// Plans a corpse shot over the window from the frame it shows now, or says why it cannot.
        /// </summary>
        /// <param name="filmSeconds">The shot's length in seconds of film.</param>
        /// <param name="speed">Seconds of the run a second of film: the shot spans this times <paramref name="filmSeconds"/>.</param>
        public static Shot Plan(
            string name, FilmWindowWorld window, WorldBounds world, float filmSeconds, double speed,
            out string refusal)
        {
            refusal = null;

            if (window == null)
            {
                refusal = "a corpse shot is filmed from a farm film window";
                return null;
            }

            if (window.Window.CorpseCount == 0)
            {
                refusal = "the window holds no corpses: a world whose dead leave none, or a window recorded before corpses.jsonl.gz";
                return null;
            }

            double t0 = window.Second;
            double t1 = Math.Min(t0 + filmSeconds * Math.Max(1d, speed), window.LastSecond);

            if (!(t1 > t0))
            {
                refusal = "the window leaves no time to film after " + Seconds(t0) + " s";
                return null;
            }

            long named = NamedCorpse(out string bad);
            if (bad != null)
            {
                refusal = bad;
                return null;
            }

            return name == "sink"
                ? PlanSink(window, world, filmSeconds, t0, t1, named, out refusal)
                : PlanVanish(window, world, filmSeconds, t0, t1, named, out refusal);
        }

        // ---------------------------------------------------------------- sink

        private static Shot PlanSink(
            FilmWindowWorld window, WorldBounds world, float filmSeconds, double t0, double t1, long named,
            out string refusal)
        {
            refusal = null;
            FilmWindowReader reader = window.Window;
            FilmWindowCorpse corpse = null;
            float reach = float.NaN;
            string why;

            if (named >= 0)
            {
                if (!reader.TryCorpse(named, out corpse))
                {
                    refusal = "the window holds no corpse of creature " + named;
                    return null;
                }

                reach = window.CorpseReach(named);
                why = "named";
            }
            else
            {
                double latest = t0 + 0.25d * (t1 - t0);
                float best = 0f;

                foreach (FilmWindowCorpse h in reader.Corpses)
                {
                    if (double.IsNaN(h.Born) || h.Born < t0 || h.Born > latest || h.Gone <= t1) continue;
                    if (!PlaceOf(reader, h.Id, h.Born, out FilmWindowCorpsePlace p) || p.Settled) continue;

                    var at = new Vector3(p.X, p.Y, p.Z);
                    float above = p.Y - world.FloorAt(p.X, p.Z);
                    if (above < 2f || above > 15f || world.RoomAround(at) < 3f) continue;

                    float r = window.CorpseReach(h.Id);
                    if (!(r > best)) continue;

                    best = r;
                    corpse = h;
                    reach = r;
                }

                if (corpse == null)
                {
                    refusal = "no corpse is founded in the shot's first quarter two to fifteen metres over the bed, " +
                              "clear of the glass and standing at its end; name one with EVOSIM_THEATRE_FILM_CORPSE";
                    return null;
                }

                why = "the largest founded in the shot's first quarter";
            }

            if (!(reach > 0f)) reach = 0.5f;

            double from = double.IsNaN(corpse.Born) ? t0 : Math.Max(t0, corpse.Born);

            // Its fall, sampled over the shot.
            const int samples = 96;
            float top = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            Vector3 first = default;
            Vector3 last = default;
            bool any = false;
            double settledAt = double.NaN;

            for (int i = 0; i <= samples; i++)
            {
                double t = from + (t1 - from) * i / samples;
                if (!PlaceOf(reader, corpse.Id, t, out FilmWindowCorpsePlace p)) continue;

                var at = new Vector3(p.X, p.Y, p.Z);
                if (!any) first = at;
                last = at;
                any = true;

                top = Mathf.Max(top, at.y);
                bottom = Mathf.Min(bottom, at.y);
                if (p.Settled && double.IsNaN(settledAt)) settledAt = t;
            }

            if (!any)
            {
                refusal = "corpse " + corpse.Id + " stands nowhere in the window between " + Seconds(from) + " and " + Seconds(t1) + " s";
                return null;
            }

            float fall = top - bottom;
            float distance = Mathf.Clamp(7f * reach + 1f, 2.5f, 7f);
            float elevation = SinkElevation * Mathf.Deg2Rad;
            float eyeY = 0.5f * (top + bottom);

            // Across the drift, from the side with more room: the drift then carries the corpse along
            // the lens's line of sight at most a little, and the camera's own slide is sideways.
            var drift = new Vector2(last.x - first.x, last.z - first.z);
            if (drift.sqrMagnitude < 1e-4f) drift = new Vector2(first.x - world.Axis.x, first.z - world.Axis.y);
            if (drift.sqrMagnitude < 1e-8f) drift = Vector2.right;
            drift.Normalize();

            var across = new Vector3(-drift.y, 0f, drift.x);
            Vector3 mid = 0.5f * (first + last);
            mid.y = eyeY;

            Vector3 forward = Room(world, mid - across * distance) >= Room(world, mid + across * distance) ? across : -across;
            Vector3 back = -forward * (distance * Mathf.Cos(elevation)) + Vector3.up * (distance * Mathf.Sin(elevation));

            long id = corpse.Id;
            Vector3 held = first;

            Shot.CameraPath path = (float u, float frameSeconds, out Vector3 eye, out Vector3 look) =>
            {
                double t = Math.Max(window.Second, from);
                if (PlaceOf(reader, id, t, out FilmWindowCorpsePlace p)) held = new Vector3(p.X, p.Y, p.Z);

                look = held;
                eye = held + back;
            };

            string plan = string.Format(
                CultureInfo.InvariantCulture,
                "corpse {0} ({1}), reach {2:0.00} m: falls {3:0.0} m from y={4:0.0} to {5:0.0} m{6}; the eye following " +
                "it {7:0.0} m off across its drift and {8:0} degrees above, a {9:0} degree lens",
                id, why, reach, fall, top, bottom,
                double.IsNaN(settledAt) ? ", not settled inside the shot" : ", settled at " + Seconds(settledAt) + " s",
                distance, SinkElevation, FieldOfView);

            return Shot.Custom("sink", world, filmSeconds, FieldOfView, true, path, plan);
        }

        // ---------------------------------------------------------------- vanish

        private static Shot PlanVanish(
            FilmWindowWorld window, WorldBounds world, float filmSeconds, double t0, double t1, long named,
            out string refusal)
        {
            refusal = null;
            FilmWindowReader reader = window.Window;
            float floor = window.Record.Config.CorpseLeavesBelowFraction;
            FilmWindowCorpse corpse = null;
            FilmWindowCorpsePlace start = default;
            float reach = float.NaN;
            string why;

            if (named >= 0)
            {
                if (!reader.TryCorpse(named, out corpse))
                {
                    refusal = "the window holds no corpse of creature " + named;
                    return null;
                }

                if (!PlaceOf(reader, named, t0, out start))
                {
                    refusal = "corpse " + named + " does not stand at the shot's start, " + Seconds(t0) + " s";
                    return null;
                }

                reach = window.CorpseReach(named) * Shrink(start.Fraction);
                why = "named";
            }
            else
            {
                double earliest = t0 + 0.72d * (t1 - t0);
                double latest = t0 + 0.97d * (t1 - t0);
                float best = 0f;

                foreach (FilmWindowCorpse h in reader.Corpses)
                {
                    if (double.IsInfinity(h.Gone) || h.Gone < earliest || h.Gone > latest) continue;
                    if (!PlaceOf(reader, h.Id, t0, out FilmWindowCorpsePlace p) || !p.Settled) continue;

                    // Its last sample holds about its floor's share: dissolved, not eaten.
                    if (!PlaceOf(reader, h.Id, h.Gone - 0.01d, out FilmWindowCorpsePlace end) || end.Fraction > 1.15f * floor) continue;
                    if (world.RoomAround(new Vector3(p.X, p.Y, p.Z)) < 3f) continue;

                    float r = window.CorpseReach(h.Id) * Shrink(p.Fraction);
                    if (!(r > best)) continue;

                    best = r;
                    corpse = h;
                    start = p;
                    reach = r;
                }

                if (corpse == null)
                {
                    refusal = "no corpse settled at the shot's start leaves at its floor between 72 and 97 per cent of " +
                              "the way through the shot, clear of the glass; name one with EVOSIM_THEATRE_FILM_CORPSE";
                    return null;
                }

                why = "the largest settled one that leaves at its floor late in the shot";
            }

            if (!(reach > 0f)) reach = 0.3f;

            var at = new Vector3(start.X, start.Y, start.Z);
            float distance = Mathf.Clamp(6f * reach + 0.6f, 1.4f, 9f);

            // Who passes near the lens: every living body's root at about sixty instants of the shot.
            var crowd = new List<Vector3>();
            int f0 = Math.Max(0, reader.NearestFrame(t0));
            int f1 = Math.Max(f0, reader.NearestFrame(t1));
            int stride = Math.Max(1, (f1 - f0) / 60);

            for (int f = f0; f <= f1; f += stride)
            {
                PoseFrame frame = reader.ReadFrame(f);
                foreach (PoseBody body in frame.Bodies) crowd.Add(new Vector3(body.X, body.Y, body.Z));
            }

            Vector3 axis = new Vector3(world.Axis.x, at.y, world.Axis.y);
            float bestScore = float.PositiveInfinity;
            Vector3 chosen = at + distance * Vector3.up;
            float chosenElevation = 90f;
            int crossed = 0;

            for (int k = 0; k < 16; k++)
            {
                float azimuth = k * (2f * Mathf.PI / 16f);
                Vector3 eye = at;
                float degrees = VanishElevation;

                // The lowest look down that keeps the eye its metre over the bed.
                for (; degrees <= 75f; degrees += 5f)
                {
                    float e = degrees * Mathf.Deg2Rad;
                    eye = at + distance * new Vector3(Mathf.Cos(e) * Mathf.Cos(azimuth), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(azimuth));
                    if (eye.y >= world.FloorAt(eye.x, eye.z) + Clearance + 0.05f) break;
                }

                float score = 0f;
                if (world.Outside(eye) || world.RoomAround(eye) < 0.75f) score += 1000f;
                if (eye.y < world.FloorAt(eye.x, eye.z) + Clearance || eye.y > -Clearance) score += 1000f;
                if (world.ReefDistance(eye) < ReefClearance) score += 1000f;

                // The line of sight, short of the corpse itself.
                for (int s = 1; s <= 8; s++)
                {
                    Vector3 q = Vector3.Lerp(eye, at, s / 10f);
                    if (world.ReefDistance(q) < 0.1f || q.y < world.FloorAt(q.x, q.z) + 0.05f) score += 500f;
                }

                int near = 0;
                foreach (Vector3 c in crowd)
                {
                    if (DistanceToSegment(c, eye, at) < 1.2f) near++;
                }

                score += near;

                // Looking in from nearer the glass puts the open tank behind the corpse.
                var fromAxis = new Vector2(eye.x - axis.x, eye.z - axis.z);
                var corpseFromAxis = new Vector2(at.x - axis.x, at.z - axis.z);
                if (world.Tank && fromAxis.magnitude < corpseFromAxis.magnitude) score += 0.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    chosen = eye;
                    chosenElevation = degrees;
                    crossed = near;
                }
            }

            long id = corpse.Id;
            Vector3 held = at;
            Vector3 still = chosen;

            Shot.CameraPath path = (float u, float frameSeconds, out Vector3 eye, out Vector3 look) =>
            {
                if (PlaceOf(reader, id, window.Second, out FilmWindowCorpsePlace p)) held = new Vector3(p.X, p.Y, p.Z);
                look = held;
                eye = still;
            };

            string plan = string.Format(
                CultureInfo.InvariantCulture,
                "corpse {0} ({1}), reach {2:0.00} m at the start, holding {3:0.000} of its first joules and leaving at " +
                "{4} s; the eye still, {5:0.0} m off and {6:0} degrees above it, a {7:0} degree lens; {8} sighting(s) " +
                "of a living body within 1.2 m of the line of sight over {9} frames sampled",
                id, why, reach, start.Fraction, Seconds(corpse.Gone), distance, chosenElevation, FieldOfView, crossed,
                (f1 - f0) / stride + 1);

            return Shot.Custom("vanish", world, filmSeconds, FieldOfView, true, path, plan);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The corpse named by <c>EVOSIM_THEATRE_FILM_CORPSE</c>, or -1 when none is.</summary>
        private static long NamedCorpse(out string refusal)
        {
            refusal = null;
            string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CORPSE");
            if (string.IsNullOrWhiteSpace(text)) return -1;

            if (long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) && id >= 0) return id;

            refusal = "EVOSIM_THEATRE_FILM_CORPSE: '" + text + "' is not the id of the body a corpse was";
            return -1;
        }

        /// <summary>A corpse's place at a second, or false when it does not stand then.</summary>
        private static bool PlaceOf(FilmWindowReader reader, long id, double seconds, out FilmWindowCorpsePlace place)
        {
            reader.CorpsesAt(seconds, Places);

            for (int i = 0; i < Places.Count; i++)
            {
                if (Places[i].Corpse.Id != id) continue;
                place = Places[i];
                return true;
            }

            place = default;
            return false;
        }

        /// <summary>A corpse's drawn size against its first, from the share of its joules it holds.</summary>
        private static float Shrink(float fraction) => Mathf.Pow(Mathf.Clamp(fraction, 1e-3f, 1f), 1f / 3f);

        /// <summary>How much room a place leaves to the glass and the rock, m.</summary>
        private static float Room(WorldBounds world, Vector3 eye) =>
            world.Outside(eye) ? -1f : Mathf.Min(world.RoomAround(eye), world.ReefDistance(eye));

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + t * ab)).magnitude;
        }

        private static string Seconds(double s) => s.ToString("0.#", CultureInfo.InvariantCulture);
    }
}