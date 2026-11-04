using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Farm;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// A count of the bodies a still close shot could ever show: the first step of dressing only what the camera can see
    /// (<c>scratch/render-speed/dress-near/design.md</c>, 2026-11-01). It reads, it logs one line and it changes nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off unless asked.</b> <c>EVOSIM_THEATRE_DRESS_SET_COUNT=1</c> turns it on; at any other value nothing here runs, so no
    /// sweep, no line and no allocation. With it on the film's frames and its other log lines are as they were: the sweep
    /// advances no paint cursor and dresses, builds, resizes, seeks and steps nothing, touches no RNG, keyword or material,
    /// and takes the later frames' poses with <see cref="FilmWindowReader.ReadFrame"/>, which seeks to the frame it is asked for
    /// and keeps no cursor and no cache (<c>PoseStreamReader.Read</c>), so the next <c>Show</c> reads what it would have read.
    /// </para>
    /// <para>
    /// <b>What it counts.</b> The close shot's camera as <c>SnapshotCamera.CapturePlaced</c> sets it (the planned eye and
    /// rotation, the lens, the capture's aspect, near 0.05 and far twice the box's diagonal and 200 m), a frustum built once,
    /// and then for every film frame of the shot, the recorded frame nearest its second, and every body in it: a sphere at the
    /// body's recorded root of the body's reach and a margin, against the frustum and against a sphere round the eye. A body
    /// any frame puts in either is in the set.
    /// </para>
    /// <para>
    /// <b>The reach</b> is the one <c>FilmPlans.Shot.Crowd</c> hands the plan: <c>SnapshotCamera.ReachOf</c> of the drawn body,
    /// which is the adult's scaled by the cube root of its body fraction. A body on screen at the plan's frame has its adult
    /// reach worked back from that and scaled by the fraction of each frame it is swept at. A body absent from the on-screen
    /// frame (born or arriving later) has no body to measure without developing its genome, which would fill a cache, so it
    /// takes the largest adult reach on screen, unscaled: an over-estimate, never an under-one, for the count.
    /// </para>
    /// </remarks>
    public static class TheatreDressSet
    {
        // SnapshotCamera.CapturePlaced's planes (SnapshotCamera.cs:770-771): the near plane is a constant, the far plane 2 * the box's diagonal + 200.
        private const float NearPlane = 0.05f;

        /// <summary>True when <c>EVOSIM_THEATRE_DRESS_SET_COUNT</c> is 1.</summary>
        public static bool Enabled => Environment.GetEnvironmentVariable("EVOSIM_THEATRE_DRESS_SET_COUNT") == "1";

        /// <summary>Metres added to a body's reach for the frustum test: <c>EVOSIM_THEATRE_DRESS_SET_MARGIN</c>, default 1.</summary>
        public static float Margin => TheatreSkin.Dial("EVOSIM_THEATRE_DRESS_SET_MARGIN", 1f, 0f, 1000f);

        /// <summary>Metres round the eye inside which a body counts: <c>EVOSIM_THEATRE_DRESS_SET_EYE</c>, default 3.</summary>
        public static float EyeMargin => TheatreSkin.Dial("EVOSIM_THEATRE_DRESS_SET_EYE", 3f, 0f, 1000f);

        /// <summary>True when <c>EVOSIM_THEATRE_DRESS_NEAR</c> is 1: a still close film of a farm window dresses only the bodies the sweep finds.</summary>
        public static bool NearOn => TheatreSkin.Dial("EVOSIM_THEATRE_DRESS_NEAR", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Set by the film before Play mode starts, when <see cref="NearOn"/> and the film is one the dial can serve; read once by
        /// <see cref="FilmWindowWorld.Open"/> when it makes its view, so the very first frame shown already defers the skins.
        /// False in every session that is not such a film.
        /// </summary>
        public static bool Wanted;

        /// <summary>
        /// Logs the dress set of one close shot, after the shots are planned and before the warm-up. Call it only when
        /// <see cref="Enabled"/>. Nothing it reads is written back.
        /// </summary>
        /// <param name="window">The farm film window on screen, or null for any other world (which is not swept).</param>
        /// <param name="t0">The film's first second.</param>
        /// <param name="speed">World seconds a film second.</param>
        /// <param name="fps">Film frames a second.</param>
        /// <param name="freeze">True when the film holds the window's frame on screen for every film frame.</param>
        /// <param name="aspect">The capture's width over its height.</param>
        public static void Log(FilmWindowWorld window, FilmPlans.Shot shot, double t0, double speed, int fps, bool freeze, float aspect)
        {
            if (shot == null || shot.Name != "close") return;

            if (window == null)
            {
                Debug.Log("[Theatre] dress set: not swept (shot close, no farm film window: only a window holds the later frames)");
                return;
            }

            if (!shot.IsStill)
            {
                Debug.Log("[Theatre] dress set: not swept (shot close, the eye follows)");
                return;
            }

            if (!shot.HasStillEye)
            {
                Debug.Log("[Theatre] dress set: not swept (shot close, no still eye was planned: nothing alive to frame)");
                return;
            }

            try
            {
                Sweep(window, shot, t0, speed, fps, freeze, aspect, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Theatre] dress set: the sweep failed and the film goes on without it: " + e.Message);
            }
        }

        /// <summary>
        /// The ids of the bodies one still close shot could ever show (EVOSIM_THEATRE_DRESS_NEAR), swept as <see cref="Log"/> sweeps,
        /// or null with the reason in <paramref name="refusal"/> where it cannot be swept. Reads, and changes nothing. With
        /// <paramref name="logCount"/> it logs the count line <see cref="Log"/> logs.
        /// </summary>
        public static HashSet<long> Set(
            FilmWindowWorld window, FilmPlans.Shot shot, double t0, double speed, int fps, bool freeze, float aspect,
            bool logCount, out string refusal)
        {
            refusal = null;

            if (shot == null || shot.Name != "close") { refusal = "the shot is not a close shot"; return null; }
            if (window == null) { refusal = "no farm film window: only a window holds the later frames"; return null; }
            if (!shot.IsStill) { refusal = "the eye follows"; return null; }
            if (!shot.HasStillEye) { refusal = "no still eye was planned: nothing alive to frame"; return null; }

            try
            {
                return Sweep(window, shot, t0, speed, fps, freeze, aspect, logCount);
            }
            catch (Exception e)
            {
                refusal = "the sweep failed: " + e.Message;
                return null;
            }
        }

        private static HashSet<long> Sweep(
            FilmWindowWorld window, FilmPlans.Shot shot, double t0, double speed, int fps, bool freeze, float aspect, bool logCount)
        {
            float margin = Margin, eyeMargin = EyeMargin;
            FilmWindowReader reader = window.Window;

            Vector3 eye = shot.StillEye;
            Quaternion rotation = shot.StillRotation;
            float far = 2f * SnapshotCamera.BoxOf(window, out _).size.magnitude + 200f;
            FrustumPlanes(eye, rotation, Mathf.Clamp(shot.FieldOfView, 5f, 120f), aspect, NearPlane, far, out Vector3[] normals, out float[] offsets);

            // The reach the plan was given, per body on the frame on screen, worked back to the adult's.
            var adultReach = new Dictionary<long, float>();
            float biggest = 0.05f;
            PoseBody[] onScreen = reader.ReadFrame(window.FrameIndex).Bodies;
            var scaleNow = new Dictionary<long, float>(onScreen.Length);
            for (int i = 0; i < onScreen.Length; i++) scaleNow[onScreen[i].Id] = LinearScale(onScreen[i]);

            for (int i = 0; i < window.BodyCount; i++)
            {
                long id = window.IdAt(i);
                float reach = SnapshotCamera.ReachOf(window.PhenotypeOf(i));
                float adult = reach / (scaleNow.TryGetValue(id, out float s) && s > 1e-3f ? s : 1f);
                adultReach[id] = adult;
                if (adult > biggest) biggest = adult;
            }

            // The recorded frames the film shows for this shot, in order, each once.
            var frames = new List<int>();
            if (freeze) frames.Add(window.FrameIndex);
            else
            {
                for (int k = 0; k < shot.Frames; k++)
                {
                    int f = reader.NearestFrame(t0 + (double)k * speed / fps);
                    if (f >= 0 && (frames.Count == 0 || frames[frames.Count - 1] != f)) frames.Add(f);
                }
            }

            var every = new HashSet<int>();
            var inFrustum = new HashSet<int>();
            var nearEye = new HashSet<int>();

            foreach (int f in frames)
            {
                PoseBody[] bodies = reader.ReadFrame(f).Bodies;

                for (int i = 0; i < bodies.Length; i++)
                {
                    PoseBody body = bodies[i];
                    var at = new Vector3(body.X, body.Y, body.Z);
                    if (!FilmPlans.Shot.Finite(at)) continue;

                    every.Add(body.Id);

                    float reach = adultReach.TryGetValue(body.Id, out float adult) ? adult * LinearScale(body) : biggest;
                    float radius = reach + margin;

                    bool inside = true;
                    for (int p = 0; p < normals.Length && inside; p++)
                    {
                        if (Vector3.Dot(normals[p], at) + offsets[p] < -radius) inside = false;
                    }

                    if (inside) inFrustum.Add(body.Id);
                    if ((at - eye).magnitude <= radius + eyeMargin) nearEye.Add(body.Id);
                }
            }

            int onlyNearEye = 0;
            foreach (int id in nearEye) if (!inFrustum.Contains(id)) onlyNearEye++;

            if (logCount)
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] dress set: {0} of {1} bodies (shot close, still eye, {2} frames, margin {3:0.###} m, eye {4:0.###} m; " +
                    "in the frustum {5}, near the eye {6} only)",
                    inFrustum.Count + onlyNearEye, every.Count, shot.Frames, margin, eyeMargin, inFrustum.Count, onlyNearEye));
            }

            var set = new HashSet<long>();
            foreach (int id in inFrustum) set.Add(id);
            foreach (int id in nearEye) set.Add(id);
            return set;
        }

        /// <summary>
        /// The linear scale <c>FilmWindowWorld.SizedFor</c> draws a body at: the cube root of its body fraction below 1, and 1
        /// for a fraction not recorded, not positive, or at or past 1.
        /// </summary>
        private static float LinearScale(PoseBody body)
        {
            bool sized = body.FractionRecorded && body.BodyFraction > 0f;
            return sized && body.BodyFraction < 1f ? Mathf.Pow(body.BodyFraction, 1f / 3f) : 1f;
        }

        /// <summary>
        /// The six planes of a perspective camera's frustum, each as a unit normal pointing in and an offset, so a point is inside
        /// a plane when <c>dot(normal, point) + offset >= 0</c>. Built by hand from the lens (a vertical field of view, as
        /// Unity's is), so no matrix convention is relied on.
        /// </summary>
        private static void FrustumPlanes(
            Vector3 eye, Quaternion rotation, float verticalFov, float aspect, float near, float far,
            out Vector3[] normals, out float[] offsets)
        {
            Vector3 f = rotation * Vector3.forward, r = rotation * Vector3.right, u = rotation * Vector3.up;
            float tanV = Mathf.Tan(0.5f * verticalFov * Mathf.Deg2Rad);
            float tanH = tanV * aspect;

            normals = new[]
            {
                (r + f * tanH).normalized,   // left
                (-r + f * tanH).normalized,  // right
                (u + f * tanV).normalized,   // bottom
                (-u + f * tanV).normalized,  // top
                f,                           // near
                -f,                          // far
            };

            offsets = new float[6];
            for (int i = 0; i < 4; i++) offsets[i] = -Vector3.Dot(normals[i], eye);
            offsets[4] = -Vector3.Dot(f, eye) - near;
            offsets[5] = Vector3.Dot(f, eye) + far;
        }
    }
}
