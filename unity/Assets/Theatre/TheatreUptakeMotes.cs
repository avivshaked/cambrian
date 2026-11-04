using System;
using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// Inflow motes (<c>EVOSIM_THEATRE_UPTAKE</c> = 2): small detritus specks that drift in towards an absorptive
    /// part and vanish at its surface, one for every <see cref="TheatreUptake.MilliJoulesPerMote"/> of food energy
    /// the body takes in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stateless.</b> Where a mote is at a second is a function of the body's id, the part's index, a slot
    /// number and that second, and nothing else, so a film frame, a still and a replay of the same second draw the
    /// same motes, a paused world holds them still, and nothing needs to be told when a body is born or dies. Each
    /// absorptive part has slots on a fixed grid (a slot rate that depends only on the part's share of the body, so
    /// that no more than <see cref="MaxPerBody"/> motes of one body are ever alive); a slot's mote is born at its
    /// slot time plus a hashed jitter, and exists if a second hash falls under the body's mote rate at that birth
    /// time over the slot rate (thinning). The rate is read at the birth, not at the frame, so a mote that exists
    /// does not blink out when the rate moves.
    /// </para>
    /// <para>
    /// <b>Path.</b> A mote starts on a shell 1.5 to 2.5 times the part's largest half-extent from its centre, in a
    /// direction drawn from the hash, and moves straight in over <see cref="LifeSeconds"/> to the part's surface
    /// along that direction (the part's ellipsoid, which lies inside a box and so only loses a box part's last
    /// sliver to its own tissue). It is placed in the part's frame and carried by the part's transform, so it
    /// follows the pose. Its size runs up from nothing over the first tenth of its life and down to nothing over
    /// the last fifth, so it neither pops in nor pops out.
    /// </para>
    /// <para>
    /// <b>Not the marine snow</b> (3 cm, grey, falling slowly): these are 4 to 6 mm, warm, and move inward at a few
    /// centimetres a second. They are drawn with the theatre's body material and its lighting and fog, with no rim and
    /// no glow, so the key and the ambient light them and nothing else does: never emissive. Where the skin is not
    /// ready they use an unlit warm grey.
    /// </para>
    /// <para>
    /// <b>Bounded.</b> At most <see cref="MaxPerBody"/> live motes a body, <see cref="MaxTotal"/> in the world, in
    /// bodies within <c>TheatreUptake</c>'s range of the camera, from one pool of objects that grows to the number in
    /// use and is put away (inactive) when the dial goes quiet. The pool is made on first use with the dial at 2.
    /// </para>
    /// </remarks>
    internal static class TheatreUptakeMotes
    {
        public const float LifeSeconds = 1.5f;
        public const float DiameterMinMetres = 0.004f;
        public const float DiameterMaxMetres = 0.006f;
        public const float ShellMin = 1.5f;
        public const float ShellMax = 2.5f;
        public const int MaxPerBody = 16;
        public const int MaxTotal = 1200;

        private const string MoteName = "Uptake Mote";

        // Warm grey, as sRGB numbers: the engine converts a material colour to linear.
        private static readonly Color WarmGrey = new Color(0.62f, 0.55f, 0.46f, 1f);

        private static GameObject _holder;
        private static List<Transform> _pool;
        private static Material _material;
        private static MaterialPropertyBlock _look;
        private static int _used;

        /// <summary>Starts a frame's motes: every one in the pool is free again.</summary>
        public static void Begin()
        {
            _used = 0;
        }

        /// <summary>
        /// Places the live motes of one absorptive part at <paramref name="clock"/>.
        /// </summary>
        /// <param name="id">The body's id.</param>
        /// <param name="partIndex">The part's index in its phenotype.</param>
        /// <param name="part">The part's object: unit scale, at its centre, in its own axes.</param>
        /// <param name="half">The part's half-extents.</param>
        /// <param name="share">The part's share of the body's absorptive volume, which is its share of the body's motes.</param>
        /// <param name="clock">The continuous second the motes run on.</param>
        public static void Part(long id, int partIndex, Transform part, Float3 half, float share, double clock)
        {
            if (part == null || _used >= MaxTotal) return;

            float hx = Mathf.Max(1e-4f, Mathf.Abs(half.X));
            float hy = Mathf.Max(1e-4f, Mathf.Abs(half.Y));
            float hz = Mathf.Max(1e-4f, Mathf.Abs(half.Z));
            float largest = Mathf.Max(hx, Mathf.Max(hy, hz));

            // The slots: enough that the part's share of the body's cap can be alive at once, and one at least.
            int cap = Mathf.Max(1, Mathf.CeilToInt(MaxPerBody * Mathf.Clamp01(share)));
            double slots = cap / (double)LifeSeconds;

            long last = (long)Math.Floor(clock * slots);
            long first = (long)Math.Floor((clock - LifeSeconds) * slots) - 1L;
            if (first < 0L) first = 0L;

            for (long j = first; j <= last; j++)
            {
                ulong h = Mix(id, partIndex, j);

                double birth = (j + Unit(h, 0)) / slots;
                double age = clock - birth;
                if (age < 0d || age >= LifeSeconds) continue;

                // Thinning: the body's mote rate when this one was born, over the slot rate, times the part's share.
                double rate = TheatreUptake.MoteRate(id, birth) * (double)share;
                if (Unit(h, 1) >= rate / slots) continue;

                if (_used >= MaxTotal) return;

                // A direction on the sphere in the part's own frame.
                float z = 1f - 2f * (float)Unit(h, 2);
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float phi = 6.2831853f * (float)Unit(h, 3);
                Vector3 d = new Vector3(ring * Mathf.Cos(phi), z, ring * Mathf.Sin(phi));

                float shell = Mathf.Lerp(ShellMin, ShellMax, (float)Unit(h, 4)) * largest;

                // Where that direction leaves the part's ellipsoid.
                float q = (d.x * d.x) / (hx * hx) + (d.y * d.y) / (hy * hy) + (d.z * d.z) / (hz * hz);
                float surface = q > 1e-12f ? 1f / Mathf.Sqrt(q) : largest;

                float f = (float)(age / LifeSeconds);
                float radius = Mathf.Lerp(shell, surface, f);

                float grow = Mathf.SmoothStep(0f, 1f, f / 0.1f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, (f - 0.8f) / 0.2f);
                float diameter = Mathf.Lerp(DiameterMinMetres, DiameterMaxMetres, (float)Unit(h, 5)) * grow * fade;

                Transform mote = Take();
                if (mote == null) return;

                mote.position = part.TransformPoint(d * radius);
                mote.localScale = new Vector3(diameter, diameter, diameter);
            }
        }

        /// <summary>Ends a frame's motes: the pool objects it did not use are put away.</summary>
        public static void End()
        {
            if (_pool == null) return;

            for (int i = _used; i < _pool.Count; i++)
            {
                Transform mote = _pool[i];
                if (mote != null && mote.gameObject.activeSelf) mote.gameObject.SetActive(false);
            }
        }

        /// <summary>Puts every mote away: a census render, or a run with no uptake to show.</summary>
        public static void Hide()
        {
            _used = 0;
            End();
        }

        // ---------------------------------------------------------------- the pool

        private static Transform Take()
        {
            if (_holder == null)
            {
                _holder = new GameObject("Uptake Motes") { hideFlags = HideFlags.DontSave };
                _pool = new List<Transform>();
            }

            if (_pool == null) _pool = new List<Transform>();

            while (_pool.Count <= _used)
            {
                Transform made = Make();
                if (made == null) return null;

                _pool.Add(made);
            }

            Transform mote = _pool[_used];

            if (mote == null)
            {
                // Destroyed under the pool (a scene change): made again.
                mote = Make();
                if (mote == null) return null;

                _pool[_used] = mote;
            }

            if (!mote.gameObject.activeSelf) mote.gameObject.SetActive(true);

            _used++;
            return mote;
        }

        private static Transform Make()
        {
            Material material = LookMaterial();
            if (material == null) return null;

            var go = new GameObject(MoteName) { layer = PhenotypeBuilder.CreatureLayer, hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_holder.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = TheatreMeshes.BuiltinSphere();

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (_look != null) renderer.SetPropertyBlock(_look);

            go.SetActive(false);
            return go.transform;
        }

        // The theatre's body material with a block that leaves it a plain lit warm-grey solid: no rim, no
        // inner glow, no through-light, no sheen, no carve. Failing that, an unlit warm grey.
        private static Material LookMaterial()
        {
            if (_material != null) return _material;

            TheatreSkin skin = TheatreSkin.Current;

            if (skin != null && skin.Ready)
            {
                _material = skin.BodyMaterial;

                _look = new MaterialPropertyBlock();
                _look.SetColor("_BaseColor", WarmGrey);
                _look.SetColor("_Color", WarmGrey);
                _look.SetColor("_RimColor", Color.black);
                _look.SetColor("_TransTint", Color.black);
                _look.SetFloat("_TransGain", 0f);
                _look.SetFloat("_Reserve", 0f);
                _look.SetFloat("_Sheen", 0f);
                _look.SetFloat("_Individual", 0.5f);
                _look.SetVector("_Carve", Vector4.zero);
                _look.SetVector("_PinchA", Vector4.zero);
                _look.SetVector("_PinchB", Vector4.zero);
                _look.SetVector("_Leaf", Vector4.zero);
                _look.SetVector("_Uptake", Vector4.zero);

                return _material;
            }

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default");

            if (shader == null) return null;

            _material = new Material(shader) { name = "Theatre Uptake Mote", hideFlags = HideFlags.DontSave };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", WarmGrey);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", WarmGrey);

            return _material;
        }

        // ---------------------------------------------------------------- the hash

        // A 64-bit mix of (id, part, slot) that is the same in every process.
        private static ulong Mix(long id, int part, long slot)
        {
            unchecked
            {
                ulong z = (ulong)id * 0x9E3779B97F4A7C15UL;
                z += (ulong)(part + 1) * 0xBF58476D1CE4E5B9UL;
                z += (ulong)slot * 0x94D049BB133111EBUL;
                return Finish(z);
            }
        }

        // A number in [0, 1) for the nth draw from a mixed hash.
        private static double Unit(ulong h, int draw)
        {
            unchecked
            {
                ulong z = Finish(h + (ulong)(draw + 1) * 0x9E3779B97F4A7C15UL);
                return (z >> 11) * (1.0 / 9007199254740992.0);
            }
        }

        // SplitMix64's finaliser.
        private static ulong Finish(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
