using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// Oxygen pearling (<c>EVOSIM_THEATRE_PEARLING</c>): a leaf in strong light releases fine bubbles of oxygen that form on the blade,
    /// grow attached, then let go and rise, dissolving as they go. Each photosynthetic part releases them at a rate driven by its
    /// light income, so the picture follows the world: a bright leaf streams, a leaf in the dark does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> <see cref="FilmWindowWorld.Show"/> calls <see cref="Frame"/> only with the dial
    /// on. With it off no layer, mesh, material, pass, hook, list, light field or log line is made, no frame is read for a leaf, and
    /// every existing frame is pixel-identical. <see cref="MaxPearls"/>' dial is not even read.
    /// </para>
    /// <para>
    /// <b>The income, the way Core takes it, from what the window holds.</b> A leaf part's income is
    /// <c>irradiance x lit area x efficiency</c> (<c>Fixation.Fix</c>, <c>StandardCellTypes.cs</c>, per second). The efficiency is the
    /// run's photosynthetic cell type's own. The lit area is the part's <see cref="PhenotypePart.LitArea"/> times its exposure in the
    /// pose when the run bills by exposure (D110, taken from the drawn part's axes), times the body's silhouette cap factor when the
    /// run caps it (D099), all by Core's own <see cref="Phenotype"/> functions. The irradiance is Core's own <see cref="LightField"/>,
    /// built from the run's config and fed what the window shows: every drawn body's effective lit area contributed at its height, then
    /// <c>Solve()</c>, then <c>IrradianceAt(height)</c> at the body's drawn centre, with the day factor at the window's second. That
    /// is the pooled canopy the world uses (the light the leaves above take from those below, the depth by Beer-Lambert, the day and
    /// season cycle) run on the bodies the film draws.
    /// </para>
    /// <para>
    /// <b>What is approximated.</b> Not in the window, and so left out: the shade map (D109) and the reefs' and coral's caps over a
    /// column, per-patch shading (the pooled canopy is used whatever the run asks), the dead remains' shadow and a grazed part's
    /// health (a bitten leaf is taken whole), and the uptake ceiling (the dissolved matter bounds income only in water too thin to
    /// supply it; the light capacity is taken). The body's height is its drawn volume-weighted centre, where Core's is its recorded
    /// height. A run whose canopy is mostly shade-map or reef shadow will read brighter than it was; one without them reads as it
    /// was, to the extent the window draws every living body.
    /// </para>
    /// <para>
    /// <b>The rate.</b> A part releases <see cref="PearlsPerJoule"/> pearls per joule of income, held between <see cref="RateLeast"/>
    /// (below it a part releases none) and <see cref="RateMost"/> a second. At the run's top-of-the-water light a 10 cm leaf is
    /// held at the most; a 3 cm leaf streams a couple a second.
    /// </para>
    /// <para>
    /// <b>A pearl is a function of (body id, part, slot, cycle).</b> A part has as many slots as its rate gives pearls in one
    /// <see cref="Cycle"/> (a fractional slot is kept on a hashed draw each cycle). Each slot has a hashed phase, so its cycles do not
    /// line up with its neighbours', and in each cycle it holds at most one pearl: formed at a hashed spot on the part's surface
    /// (a box's faces by area, any other shape as an ellipsoid), growing attached for a hashed <see cref="AttachLow"/> to
    /// <see cref="AttachHigh"/> seconds, then released and rising and dissolving by the birth bubbles' own laws
    /// (<see cref="TheatreBirthBubbles.Place"/>, held and released through its cover bubbles' hold). The diameter is
    /// <see cref="DiameterLow"/> to <see cref="DiameterHigh"/> on the birth bubbles' power law. Every random number is hashed with
    /// <see cref="TheatreBirthCloud.Hash"/> at salts 401 to 416, and time is the run's, so a time-lapse compresses a pearl's life as it
    /// does a birth's. The part's pose is the frame's: an attached pearl rides it, and a released one is carried back along the body's
    /// recorded velocity (<see cref="FilmWindowWorld.VelocityOf"/>) and on with the water's current, so a body that drifts does not
    /// tow its pearls.
    /// </para>
    /// <para>
    /// <b>Cost.</b> A wide shot holds thousands of leaves. The pearls drawn in a frame are held to <see cref="MaxPearls"/>, the parts
    /// nearest the camera first (a part outside the frustum is skipped before it is ranked), and the log says once when the cap binds.
    /// Drawn in a layer of its own (<see cref="TheatreBubbleLayer"/>) with the birth bubbles' shader.
    /// </para>
    /// </remarks>
    public static class TheatrePearling
    {
        // ---------------------------------------------------------------- the dials

        /// <summary>The dial, <c>EVOSIM_THEATRE_PEARLING</c> (0 or 1, 0 by default), read once.</summary>
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_PEARLING", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The most pearls drawn in a frame (<c>EVOSIM_THEATRE_PEARLING_MAX</c>, 6000 by default, held to 100 to 100000), the parts
        /// nearest the camera first. Read only with <see cref="On"/>.
        /// </summary>
        public static readonly int MaxPearls = On ? Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_PEARLING_MAX", 6000f, 100f, 100000f)) : 6000;

        // ---------------------------------------------------------------- the constants (D262: started strong, to be dialled back)

        /// <summary>Pearls a second per joule a second of light income (watts), before the part's most.</summary>
        private const float PearlsPerJoule = 300f;

        /// <summary>The most pearls a second one part releases, and the least rate under which it releases none.</summary>
        private const float RateMost = 10f;

        private const float RateLeast = 0.02f;

        /// <summary>One slot's cycle, run seconds: a pearl's whole life (attached and risen) is under it, so a slot holds one pearl a cycle.</summary>
        private const float Cycle = 5f;

        /// <summary>How long a pearl grows attached, run seconds, hashed between these.</summary>
        private const float AttachLow = 0.5f;

        private const float AttachHigh = 3f;

        /// <summary>A pearl's final diameter, metres, on the birth bubbles' power law between these: 5 to 20 mm (at 0.3 to 1.5 mm a pearl was under a pixel at the close framing, and at 2 to 8 mm still a dot).</summary>
        private const float DiameterLow = 0.005f;

        private const float DiameterHigh = 0.02f;

        /// <summary>Slots are numbered part * stride + slot in the hash; the most a part has is RateMost x Cycle + 1, under the stride.</summary>
        private const int SlotStride = 64;

        /// <summary>A leaf outside the frustum by more than its bound plus this, metres, is skipped: the reach of a pearl's rise and drift.</summary>
        private const float Reach = 0.5f;

        /// <summary>A pearl outside the frustum by more than this, metres, is not built.</summary>
        private const float Margin = 0.05f;

        // ---------------------------------------------------------------- the state (made at the first frame, with the dial on)

        /// <summary>One leaf part of the frame, fixed when the frame is shown.</summary>
        private struct Leaf
        {
            public long Id;
            public int Part;
            public int Body;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Half;
            public bool Box;

            /// <summary>Pearls a second.</summary>
            public float Rate;

            /// <summary>A sphere about the part that holds it, metres.</summary>
            public float Bound;
        }

        /// <summary>A body that has at least one leaf in the frame: where it is, how it moves, and the water's current at it.</summary>
        private struct Body
        {
            public Vector3 Centre;
            public Vector3 Velocity;
            public bool CurrentKnown;
            public Vector3 Current;
        }

        private struct Ranked
        {
            public float Distance;
            public int Index;
        }

        private static TheatreBubbleLayer _layer;
        private static List<Leaf> _leaves;
        private static List<Body> _bodies;
        private static List<Ranked> _ranked;
        private static Comparison<Ranked> _rankedOrder;
        private static Dictionary<int, float[]> _exposure;

        private static FilmWindowWorld _fieldWindow;
        private static LightField _field;
        private static float _efficiency;

        private static int _snapBodies;
        private static float _snapPeak;
        private static float _snapIrradiance;

        private static bool _told;
        private static bool _toldCap;
        private static bool _toldFailure;

        // The snapshot's count, told once at the CountFrames-th frame: the photosynthetic bodies met and where each was dropped.
        private const int CountFrames = 90;
        private static int _countFrames, _countGreen, _countNoBody, _countDark, _countFaint, _countLeaves;
        private static float _countBestRate;
        private static bool _countTold;

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Called by <see cref="FilmWindowWorld.Show"/> after every body of a frame is placed, with the dial on: reads the frame's leaf
        /// parts and their income once, for the next camera render to draw their pearls.
        /// </summary>
        public static void Frame(FilmWindowWorld window)
        {
            if (!On || window == null || window.FrameIndex < 0 || double.IsNaN(window.Second)) return;

            if (_layer == null)
            {
                _layer = new TheatreBubbleLayer("pearls", Fill);
                _leaves = new List<Leaf>();
                _bodies = new List<Body>();
                _ranked = new List<Ranked>();
                _exposure = new Dictionary<int, float[]>();
                _rankedOrder = (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Index.CompareTo(b.Index);
            }

            if (!_layer.Ensure()) return;

            try
            {
                Snapshot(window);
            }
            catch (Exception e)
            {
                // A cosmetic layer never takes the film down; the pearls are simply absent for this frame.
                _leaves.Clear();
                _bodies.Clear();

                if (!_toldFailure)
                {
                    _toldFailure = true;
                    Debug.LogWarning("[Theatre] pearling: the frame's leaves could not be read: " + e.GetType().Name + ": " + e.Message);
                }
            }

            _layer.Second = window.Second;
            _layer.Version++;

            if (++_countFrames == CountFrames && !_countTold)
            {
                _countTold = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] pearling: snapshot count after {0} frames: {1} photosynthetic bodies met, {2} not drawn, {3} in no light, " +
                    "{4} leaf parts under {5:0.###} pearls a second (the best {6:0.#####}), {7} leaf parts kept; field {8}, efficiency {9:0.###}",
                    _countFrames, _countGreen, _countNoBody, _countDark, _countFaint, RateLeast, _countBestRate, _countLeaves,
                    _field != null ? "on" : "off", _efficiency));
            }
        }

        // The frame's leaf parts and their pearl rates: the light field fed the drawn bodies, then each photosynthetic body read at its
        // own height. See the class remarks for what is Core's and what is approximated.
        private static void Snapshot(FilmWindowWorld window)
        {
            RunConfig config = window.Record.Config;
            LightModel model = config.Light;
            double second = window.Second;
            bool cap = config.LightSilhouetteCap;
            bool byExposure = config.LightByExposure;
            LiveWorldView view = window.View;

            if (!ReferenceEquals(window, _fieldWindow))
            {
                _fieldWindow = window;
                _field = null;
                _efficiency = EfficiencyOf(config);

                try
                {
                    _field = new LightField(model, config.WorldAreaSquareMetres, config.LightLayerMetres);
                }
                catch (Exception)
                {
                    // A config the field refuses: the light model by depth alone, with no shading.
                    _field = null;
                }
            }

            _leaves.Clear();
            _bodies.Clear();
            _snapBodies = 0;
            _snapPeak = 0f;
            _snapIrradiance = 0f;

            int count = window.BodyCount;

            // The canopy: every drawn body casts its effective lit area at its height, as Core's step contributes its living.
            if (_field != null)
            {
                _field.Clear();
                _field.Advance(second);

                for (int i = 0; i < count; i++)
                {
                    if (!view.TryBody(window.IdAt(i), out Phenotype sized, out Transform[] parts)) continue;
                    _field.Contribute(window.PositionOf(i).y, EffectiveArea(sized, parts, cap, byExposure));
                }

                _field.Solve();
            }

            // The leaves: each part of a photosynthetic body earns on its lit area at the body's irradiance.
            for (int i = 0; i < count; i++)
            {
                if (!window.PhotosyntheticAt(i)) continue;
                _countGreen++;

                long id = window.IdAt(i);
                if (!view.TryBody(id, out Phenotype sized, out Transform[] parts))
                {
                    _countNoBody++;
                    continue;
                }

                Vector3 centre = window.PositionOf(i);
                float irradiance = _field != null ? _field.IrradianceAt(centre.y) : model.IrradianceAt(centre.y) * model.DayFactorAt(second);
                if (!(irradiance > 0f))
                {
                    _countDark++;
                    continue;
                }

                float[] exposure = null;
                float litFactor;

                if (byExposure)
                {
                    exposure = ExposureOf(sized, parts, out Float3 upInBody);
                    litFactor = sized.LitAreaFactor(exposure, upInBody, cap);
                }
                else
                {
                    litFactor = sized.LitAreaFactor(cap);
                }

                int bodyIndex = -1;

                for (int q = 0; q < sized.PartCount; q++)
                {
                    PhenotypePart part = sized.Parts[q];
                    if (part.CellTypeId != CellTypeIds.Photosynthetic) continue;

                    Transform at = q < parts.Length ? parts[q] : null;
                    if (at == null) continue;

                    // Core's expression: the irradiance times the part's lit area in its pose (times the cap's factor) times the efficiency.
                    float lit = part.LitArea * (exposure != null ? exposure[q] : 1f) * litFactor;
                    float income = irradiance * lit * _efficiency;
                    float rate = Mathf.Min(RateMost, income * PearlsPerJoule);
                    if (rate > _countBestRate) _countBestRate = rate;

                    if (!(rate >= RateLeast))
                    {
                        _countFaint++;
                        continue;
                    }

                    _countLeaves++;

                    if (bodyIndex < 0)
                    {
                        bodyIndex = _bodies.Count;
                        _bodies.Add(new Body { Centre = centre, Velocity = window.VelocityOf(id) });
                    }

                    Float3 h = part.HalfExtents;
                    var half = new Vector3(Mathf.Abs(h.X), Mathf.Abs(h.Y), Mathf.Abs(h.Z));

                    _leaves.Add(new Leaf
                    {
                        Id = id,
                        Part = q,
                        Body = bodyIndex,
                        Position = at.position,
                        Rotation = at.rotation,
                        Half = half,
                        Box = part.ShapeId == ShapeIds.Box,
                        Rate = rate,
                        Bound = half.magnitude,
                    });

                    if (rate > _snapPeak)
                    {
                        _snapPeak = rate;
                        _snapIrradiance = irradiance;
                    }
                }
            }

            _snapBodies = _bodies.Count;
        }

        /// <summary>The efficiency of the run's photosynthetic cell type, or Core's default where the registry cannot say.</summary>
        private static float EfficiencyOf(RunConfig config)
        {
            try
            {
                if (config.CellTypes.Resolve(CellTypeIds.Photosynthetic) is PhotosyntheticCell cell) return cell.Efficiency;
            }
            catch (Exception)
            {
                // Fall through to the default.
            }

            return PhotosyntheticCell.DefaultEfficiency;
        }

        /// <summary>A body's effective lit area, the quantity Core contributes to the canopy: in its pose when the run bills by exposure, else orientation-averaged.</summary>
        private static float EffectiveArea(Phenotype sized, Transform[] parts, bool cap, bool byExposure)
        {
            if (!byExposure) return sized.EffectiveLitArea(cap);

            float[] exposure = ExposureOf(sized, parts, out Float3 upInBody);
            return sized.EffectiveLitArea(exposure, upInBody, cap);
        }

        /// <summary>
        /// Each part's exposure in the drawn pose (D110: <see cref="PhenotypePart.ExposureFactor(double, double, double)"/> on the
        /// world's up along the part's own axes), and the world's up in the root part's frame. The array is shared and valid until the next call.
        /// </summary>
        private static float[] ExposureOf(Phenotype sized, Transform[] parts, out Float3 upInBody)
        {
            int n = sized.PartCount;
            if (!_exposure.TryGetValue(n, out float[] exposure))
            {
                exposure = new float[n];
                _exposure[n] = exposure;
            }

            int root = -1;

            for (int q = 0; q < n; q++)
            {
                Transform at = q < parts.Length ? parts[q] : null;
                PhenotypePart part = sized.Parts[q];
                exposure[q] = at != null ? part.ExposureFactor(at.right.y, at.up.y, at.forward.y) : 1f;
                if (part.IsRoot) root = q;
            }

            upInBody = new Float3(0f, 1f, 0f);

            if (root >= 0 && root < parts.Length && parts[root] != null)
            {
                Vector3 up = parts[root].InverseTransformDirection(Vector3.up);
                upInBody = new Float3(up.x, up.y, up.z);
            }

            return exposure;
        }

        // ---------------------------------------------------------------- the drawing

        /// <summary>Writes the frame's pearls for one camera, the parts nearest it first, up to <see cref="MaxPearls"/>. Returns the pearls written.</summary>
        private static int Fill(TheatreBubbleLayer layer, Camera camera, TheatreSkin skin)
        {
            if (_leaves.Count == 0) return 0;

            Vector3 eye = camera.transform.position;
            _ranked.Clear();

            for (int i = 0; i < _leaves.Count; i++)
            {
                Leaf leaf = _leaves[i];
                if (!layer.InFrustum(leaf.Position, leaf.Bound + Reach)) continue;
                _ranked.Add(new Ranked { Distance = (leaf.Position - eye).sqrMagnitude, Index = i });
            }

            _ranked.Sort(_rankedOrder);

            bool open = skin.RunWaterOpen;
            double second = layer.Second;
            int drawn = 0;
            int leaves = 0;

            for (int r = 0; r < _ranked.Count; r++)
            {
                if (drawn >= MaxPearls)
                {
                    if (!_toldCap) TellCap(leaves, _ranked.Count);
                    break;
                }

                Leaf leaf = _leaves[_ranked[r].Index];
                Body body = _bodies[leaf.Body];

                // The current at the body at the frame's second, read once.
                if (!body.CurrentKnown)
                {
                    body.Current = open ? skin.WaterAt(body.Centre, second) : Vector3.zero;
                    body.CurrentKnown = true;
                    _bodies[leaf.Body] = body;
                }

                drawn += Emit(layer, leaf, body, second);
                leaves++;
            }

            if (drawn > 0 && !_told) Tell(leaves);
            return drawn;
        }

        /// <summary>One leaf's pearls at a second: its slots, each holding at most one pearl in its cycle. Returns the pearls written.</summary>
        private static int Emit(TheatreBubbleLayer layer, in Leaf leaf, in Body body, double second)
        {
            float expected = leaf.Rate * Cycle;
            int whole = (int)expected;
            float fraction = expected - whole;
            int slots = whole + (fraction > 0f ? 1 : 0);
            int drawn = 0;

            for (int j = 0; j < slots; j++)
            {
                int index = leaf.Part * SlotStride + j;

                // The slot's cycle: its own phase, so the neighbours' pearls are not in step. The pearl is a function of the cycle's number.
                double shifted = (second - Cycle * TheatreBirthCloud.Hash(leaf.Id, index, 401)) / Cycle;
                double number = Math.Floor(shifted);
                float into = (float)((shifted - number) * Cycle);
                long key = leaf.Id + (((long)number) << 32);

                // A fractional last slot fires on a hashed draw each cycle.
                if (j == whole && !(TheatreBirthCloud.Hash(key, index, 402) < fraction)) continue;

                // When the pearl is: grown attached, then risen and dissolved; fitted into the cycle at a hashed start.
                float attach = Mathf.Lerp(AttachLow, AttachHigh, TheatreBirthCloud.Hash(key, index, 403));
                float rise = TheatreBirthBubbles.DissolveEnd(TheatreBirthCloud.Hash(key, index, 405));
                float age = into - Mathf.Max(0f, Cycle - attach - rise) * TheatreBirthCloud.Hash(key, index, 404);
                if (!(age > 0f) || age >= attach + rise) continue;

                // Where: a hashed spot on the part's surface, standing off it by the pearl's own radius.
                float diameter = TheatreBirthBubbles.PowerDiameter(TheatreBirthCloud.Hash(key, index, 412), DiameterLow, DiameterHigh);
                Vector3 spot = SpotOf(leaf, TheatreBirthCloud.Hash(key, index, 413), TheatreBirthCloud.Hash(key, index, 414),
                    TheatreBirthCloud.Hash(key, index, 415), TheatreBirthCloud.Hash(key, index, 416), out Vector3 normal);

                Vector3 anchor = leaf.Position + leaf.Rotation * spot;

                // Held and growing attached (hold = grow-in = attach), released at the attach time into the birth bubbles' rise.
                TheatreBirthBubbles.Bubble pearl = TheatreBirthBubbles.Single(
                    diameter, leaf.Rotation * (normal * (0.5f * diameter)), attach, attach, attach,
                    TheatreBirthCloud.Hash(key, index, 405), TheatreBirthCloud.Hash(key, index, 406), TheatreBirthCloud.Hash(key, index, 407),
                    TheatreBirthCloud.Hash(key, index, 408), TheatreBirthCloud.Hash(key, index, 409), TheatreBirthCloud.Hash(key, index, 410),
                    TheatreBirthCloud.Hash(key, index, 411));

                // Once released it is in the water and not on the leaf: carried back along the body's motion, and on with the current.
                float risen = age - attach;
                Vector3 drift = risen > 0f ? anchor + (body.Current - body.Velocity) * risen : anchor;

                if (!TheatreBirthBubbles.Place(in pearl, age, drift, out TheatreBirthBubbles.Placed placed)) continue;
                if (!layer.InFrustum(placed.Place, Margin + placed.Radius)) continue;

                // uv1: the radius, the fade, the seed, 0 (a sphere).
                layer.AddQuad(placed.Place, new Vector4(placed.Radius, placed.Fade, pearl.Seed, 0f));
                drawn++;
            }

            return drawn;
        }

        /// <summary>
        /// A hashed point on a part's surface in its own frame, and the surface's outward normal there. A box: a face by area, a side,
        /// and a place on it. Any other shape: a point on its half-extents' ellipsoid.
        /// </summary>
        private static Vector3 SpotOf(in Leaf leaf, float hFace, float hSide, float hU, float hV, out Vector3 normal)
        {
            Vector3 half = leaf.Half;
            float u = 2f * hU - 1f;
            float v = 2f * hV - 1f;

            if (leaf.Box)
            {
                float areaX = half.y * half.z;
                float areaY = half.x * half.z;
                float areaZ = half.x * half.y;
                float pick = hFace * (areaX + areaY + areaZ);
                float side = hSide < 0.5f ? -1f : 1f;

                if (pick < areaX)
                {
                    normal = new Vector3(side, 0f, 0f);
                    return new Vector3(side * half.x, u * half.y, v * half.z);
                }

                if (pick < areaX + areaY)
                {
                    normal = new Vector3(0f, side, 0f);
                    return new Vector3(u * half.x, side * half.y, v * half.z);
                }

                normal = new Vector3(0f, 0f, side);
                return new Vector3(u * half.x, v * half.y, side * half.z);
            }

            Vector3 way = TheatreBirthCloud.Unit(hU, hV);
            normal = new Vector3(way.x / Mathf.Max(half.x, 1e-6f), way.y / Mathf.Max(half.y, 1e-6f), way.z / Mathf.Max(half.z, 1e-6f));
            normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : way;
            return Vector3.Scale(way, half);
        }

        // ---------------------------------------------------------------- the log

        /// <summary>The one log line, at the first frame a pearl is drawn: the dial took effect.</summary>
        private static void Tell(int leaves)
        {
            _told = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] pearling (EVOSIM_THEATRE_PEARLING): oxygen pearls on photosynthetic parts, {0} pearls a second per watt of light income " +
                "(none under {1:0.##}, at most {2:0.#} a part), {3:0.##}-{4:0.##} mm, grown attached {5:0.##}-{6:0.##} s then released to rise and dissolve by the " +
                "birth bubbles' laws, cap {7} pearls a frame; first: {8} leaf parts on {9} bodies lit in the frame, the brightest {10:0.##} pearls/s " +
                "at {11:0.#} W/m2, {12} leaf parts drawn; light by Core's LightField on the drawn bodies (pooled canopy, depth and day factor; no shade map, " +
                "reef caps, patches or part health), efficiency {13:0.###}, {14}.",
                PearlsPerJoule, RateLeast, RateMost, 1000f * DiameterLow, 1000f * DiameterHigh, AttachLow, AttachHigh, MaxPearls,
                _leaves.Count, _snapBodies, _snapPeak, _snapIrradiance, leaves, _efficiency,
                _field != null ? "canopy on" : "canopy off (the light model by depth)"));
        }

        /// <summary>The one line when the cap binds: how many of the parts in view were drawn before it was reached.</summary>
        private static void TellCap(int drawnLeaves, int inView)
        {
            _toldCap = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] pearling: the cap of {0} pearls a frame binds (EVOSIM_THEATRE_PEARLING_MAX): {1} of {2} leaf parts in view drawn, nearest the camera first; " +
                "the rest release none this frame.",
                MaxPearls, drawnLeaves, inView));
        }
    }
}
