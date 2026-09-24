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

            public WorldBounds(TheatreDynamicsReplay live)
            {
                Reefs = live.Reefs != null && live.Reefs.Count > 0 ? live.Reefs : null;
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

            /// <summary>The signed distance from a place to the nearest reef's rock, m, or infinity with no reef.</summary>
            public float ReefDistance(Vector3 p) =>
                Reefs == null ? float.PositiveInfinity : (float)Reefs.SignedDistance(p.x, p.y, p.z);

            /// <summary>
            /// The nearest place at least <see cref="ReefClearance"/> outside every reef's rock,
            /// moved along the rock's outward normal, or across it when the normal would carry the
            /// eye into the surface's clearance (over a shallow table); the count says the rock
            /// moved it.
            /// </summary>
            public Vector3 OffTheReef(Vector3 eye, ref int reef)
            {
                if (Reefs == null) return eye;
                bool moved = false;

                for (int pass = 0; pass < 6; pass++)
                {
                    double s = Reefs.SignedDistance(eye.x, eye.y, eye.z, ReefClearance + 0.25d, out int nearest, out ReefGeometry.Distance at);
                    if (nearest < 0 || s >= ReefClearance) break;

                    float need = ReefClearance - (float)s + 0.02f;
                    var g = new Vector3((float)at.Gx, (float)at.Gy, (float)at.Gz);
                    if (!Shot.Finite(g) || g.sqrMagnitude < 1e-10f) g = Vector3.up;
                    Vector3 next = eye + g.normalized * need;

                    if (next.y > -Clearance)
                    {
                        var across = new Vector3(g.x, 0f, g.z);
                        if (across.sqrMagnitude < 1e-8f)
                            across = new Vector3(eye.x - (float)Reefs.CentreX(nearest), 0f, eye.z - (float)Reefs.CentreZ(nearest));
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

            /// <summary>The body the close shot follows, or -1 for any other shot.</summary>
            public long Subject => _subject;
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

            // a path handed in by the safari's director (Custom), null for the film's own shots
            private CameraPath _path;
            private bool _mayLeaveTheGlass;

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
            public static Shot Custom(
                string name, WorldBounds world, float seconds, float fieldOfView, bool portrait,
                CameraPath path, string plan, bool mayLeaveTheGlass = false)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));
                if (name == "close" || name == "drift") throw new ArgumentException("'" + name + "' names a film shot", nameof(name));

                return new Shot
                {
                    Name = name, _world = world, _seconds = seconds, FieldOfView = fieldOfView,
                    Portrait = portrait, _path = path, _mayLeaveTheGlass = mayLeaveTheGlass, Plan_ = plan,
                };
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
                string name, TheatreDynamicsReplay live, LiveWorldView view, WorldBounds world,
                float seconds, float aspect, float turns, bool still = false)
            {
                var shot = new Shot { Name = name, _world = world, _seconds = seconds, _still = still };

                var positions = new List<Vector3>();
                var reaches = new List<float>();
                var ids = new List<long>();
                Crowd(live, view, positions, reaches, ids);

                switch (name)
                {
                    case "close": shot.PlanClose(positions, reaches, ids, aspect, CloseSubject(live)); break;
                    case "drift": shot.PlanCrowd(positions, reaches, aspect, 0f, true); break;
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
            private static Func<long, bool> CloseSubject(TheatreDynamicsReplay live)
            {
                string pick = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SUBJECT");
                if (string.IsNullOrWhiteSpace(pick)) return null;
                pick = pick.Trim();

                if (long.TryParse(pick, NumberStyles.Integer, CultureInfo.InvariantCulture, out long wanted))
                {
                    return id => id == wanted;
                }

                if (pick != "leaf") throw new ArgumentException("EVOSIM_THEATRE_FILM_SUBJECT is a body id or 'leaf', not '" + pick + "'");

                var leafy = new HashSet<long>();
                foreach (Organism o in live.Sim.World.Living)
                {
                    Phenotype body = o.Phenotype;
                    if (body == null) continue;
                    for (int k = 0; k < body.PartCount; k++)
                    {
                        if (body.Parts[k].ShapeId == ShapeIds.Box && body.Parts[k].CellTypeId == CellTypeIds.Photosynthetic)
                        {
                            leafy.Add(o.Id);
                            break;
                        }
                    }
                }

                return leafy.Contains;
            }

            /// <summary>Every living body's root, its reach and its id, from the drawn scene where it can.</summary>
            public static void Crowd(
                TheatreDynamicsReplay live, LiveWorldView view,
                List<Vector3> positions, List<float> reaches, List<long> ids)
            {
                IReadOnlyList<Organism> living = live.Sim.World.Living;

                for (int i = 0; i < living.Count; i++)
                {
                    Organism o = living[i];
                    Vector3 at = Where(o, view);
                    if (!Finite(at)) continue;

                    positions.Add(at);
                    reaches.Add(SnapshotCamera.ReachOf(o.Phenotype));
                    ids.Add(o.Id);
                }
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
            /// The close view's framing: the largest body and the largest of its neighbours, from
            /// a three-quarter angle, the frame fitted to the pair and a slow dolly in.
            /// </summary>
            private void PlanClose(
                List<Vector3> positions, List<float> reaches, List<long> ids, float aspect,
                Func<long, bool> eligible = null)
            {
                FieldOfView = 28f;
                Portrait = true;
                _forward = new Vector3(-0.78f, -0.34f, 1f).normalized;

                // A study of one body (2026-09-24, the leaves): EVOSIM_THEATRE_FILM_CLOSE_LOOK=up
                // looks up at the subject from below, where a blade's glow from the surface shows,
                // and =down looks steeply down on it, at the blade's upper face.
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

                float around = Mathf.Max(1.2f, 6f * reaches[anchor]);
                int neighbour = -1;
                for (int i = 0; i < positions.Count && eligible == null; i++)
                {
                    if (i == anchor) continue;
                    if ((positions[i] - positions[anchor]).sqrMagnitude > around * around) continue;
                    if (neighbour < 0 || reaches[i] > reaches[neighbour]) neighbour = i;
                }

                var framed = new Bounds(positions[anchor], 2.4f * reaches[anchor] * Vector3.one);
                if (neighbour >= 0) framed.Encapsulate(new Bounds(positions[neighbour], 2.4f * reaches[neighbour] * Vector3.one));

                // The snapshot's perspective fit (SnapshotCamera.Frame), with its 6% margin.
                Quaternion rotation = Quaternion.LookRotation(_forward, Vector3.up);
                Quaternion inverse = Quaternion.Inverse(rotation);
                float tanV = Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad);
                float tanH = tanV * aspect;
                Vector3 half = 0.5f * framed.size;
                float need = 0f, reachZ = 0f;

                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 c = inverse * new Vector3(sx * half.x, sy * half.y, sz * half.z);
                    need = Mathf.Max(need, Mathf.Abs(c.y) / tanV - c.z);
                    need = Mathf.Max(need, Mathf.Abs(c.x) / tanH - c.z);
                    reachZ = Mathf.Max(reachZ, Mathf.Abs(c.z));
                }

                _standoff = 1.06f * Mathf.Max(need, reachZ + 0.5f);

                // EVOSIM_THEATRE_FILM_CLOSE_NEAR brings the eye in to that fraction of the fitted
                // distance (0.2 to 1, 1 the fit), never nearer than the subject's reach and a
                // third of a metre, so it stays outside the body.
                string nearText = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_CLOSE_NEAR");
                if (!string.IsNullOrWhiteSpace(nearText) &&
                    float.TryParse(nearText, NumberStyles.Float, CultureInfo.InvariantCulture, out float near))
                {
                    _standoff = Mathf.Max(Mathf.Clamp(near, 0.2f, 1f) * _standoff, reaches[anchor] + 0.33f);
                }

                // A few metres in at most, never past two fifths of the standoff (the subject is
                // there), and never faster on average than a quarter of the ceiling.
                _dolly = Mathf.Min(3f, Mathf.Min(0.4f * _standoff, 0.25f * CloseSpeedCeiling * _seconds));

                _subject = ids[anchor];
                _offset = framed.center - positions[anchor];
                _followed = positions[anchor];
                _hasFollowed = true;

                Plan_ = string.Format(CultureInfo.InvariantCulture,
                    "body {0} (reach {1:0.###} m){2}, framed over {3:0.##} m, standoff {4:0.##} m, dolly in {5:0.##} m " +
                    "({6:0.###} m/s at its peak), {7}",
                    _subject, reaches[anchor],
                    neighbour >= 0
                        ? string.Format(CultureInfo.InvariantCulture, " and body {0} (reach {1:0.###} m)", ids[neighbour], reaches[neighbour])
                        : ", no neighbour within reach",
                    framed.size.magnitude, _standoff, _dolly, _dolly / _seconds / (1f - EaseShare),
                    _still ? "held still a quarter further back, no dolly, no follow" : "following the subject");
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
                TheatreDynamicsReplay live, LiveWorldView view, float u, float frameSeconds,
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
                    // The subject's root, smoothed over a second and a half so the frame follows a
                    // swimmer without copying every stroke.
                    Vector3 now = _followed;
                    if (_subject >= 0)
                    {
                        Transform root = view?.RootOf(_subject);
                        if (root != null && Finite(root.position)) now = root.position;
                    }

                    float k = 1f - Mathf.Exp(-frameSeconds / 1.5f);
                    _followed = _hasFollowed ? Vector3.Lerp(_followed, now, k) : now;
                    _hasFollowed = true;

                    subject = _followed + _offset;
                    float distance = _standoff - _dolly * Ease(u);

                    if (_still)
                    {
                        // Framed once, a quarter further back than the following shot so the
                        // subject's drift has room, and never moved: every motion in the clip
                        // is a creature's or the water's.
                        if (!_hasStill)
                        {
                            _stillEye = subject - _forward * (1.25f * _standoff);
                            _stillRotation = Quaternion.LookRotation(_forward, Vector3.up);
                            _hasStill = true;
                        }

                        eye = _stillEye;
                        rotation = _stillRotation;
                        focus = 1.25f * _standoff;
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
            private Vector3 OffTheBodies(TheatreDynamicsReplay live, LiveWorldView view, Vector3 eye)
            {
                IReadOnlyList<Organism> living = live.Sim.World.Living;
                bool moved = false;

                for (int pass = 0; pass < 3; pass++)
                {
                    bool again = false;

                    for (int i = 0; i < living.Count; i++)
                    {
                        Vector3 at = Where(living[i], view);
                        if (!Finite(at)) continue;

                        float keep = SnapshotCamera.ReachOf(living[i].Phenotype) + 0.25f;
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

            public static bool InsideABody(TheatreDynamicsReplay live, LiveWorldView view, Vector3 eye)
            {
                IReadOnlyList<Organism> living = live.Sim.World.Living;
                for (int i = 0; i < living.Count; i++)
                {
                    Vector3 at = Where(living[i], view);
                    if (!Finite(at)) continue;
                    if ((eye - at).magnitude < SnapshotCamera.ReachOf(living[i].Phenotype)) return true;
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
