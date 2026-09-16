using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Evosim.Theatre
{
    /// <summary>
    /// The theatre's image chain: a global volume built in code (tonemapping, exposure, a mild
    /// bloom, a vignette, and depth of field held ready for a portrait), and the camera
    /// settings that let URP's post stack run at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Until 2026-09-16 the theatre rendered with Unity's image-making
    /// switched off: the project in gamma colour space, the renderer with no post-process data,
    /// no volume profile, no anti-aliasing, an 8-bit target, and the dark-field look's values
    /// (a water of 0.012, 0.032, 0.048; a sand of 0.012 to 0.066; a starving body's face near
    /// 0.04) sitting in the bottom tenth of the range with nothing to redistribute them. The
    /// owner's "can barely see anything" (2026-09-15) was the display chain, not the art
    /// (<c>logbook/specs/striking-theatre-menu.md</c>). The renderer now carries URP's
    /// post-process data, the project is linear, and this class supplies the grade.
    /// </para>
    /// <para>
    /// <b>Built in code, like the rest of the skin</b>: no committed profile asset, so nothing
    /// under <c>Assets/Theatre</c> is a binary and every dial is a number a reader can see.
    /// Neutral tonemapping rather than ACES, because ACES shifts hue at the top of the range
    /// and hue carries meaning here (the guild on a body's rim); the neutral curve keeps a
    /// green a green as it rolls off.
    /// </para>
    /// <para>
    /// <b>The look has a version</b>, <see cref="LookVersion"/>, stamped into every snapshot's
    /// label from this build on: a picture that carries no look token was taken through the
    /// old chain (look 1) and is not directly comparable to one that carries <c>look 2</c>,
    /// though its framing is the same (the owner's ruling of 2026-09-16: grade everything from
    /// here on, version the look, leave the record's earlier pictures as they are).
    /// </para>
    /// <para>
    /// Dials from the environment, so a script can set them without a scene edit:
    /// <c>EVOSIM_THEATRE_GRADE</c> (1 on, 0 off; off restores look 1 for a comparison pair),
    /// <c>EVOSIM_THEATRE_EXPOSURE</c> (post-exposure in EV, default 0.4; 0.8 until the lit
    /// water carried its own light), <c>EVOSIM_THEATRE_CONTRAST</c> (default 8), <c>EVOSIM_THEATRE_BLOOM</c> (intensity,
    /// default 0.35), <c>EVOSIM_THEATRE_VIGNETTE</c> (default 0.22).
    /// </para>
    /// </remarks>
    public sealed class TheatreGrade
    {
        /// <summary>The look's version, stamped into every picture's label.</summary>
        public const int LookVersion = 2;

        /// <summary>The one grade in the scene, for a camera created after it (the snapshot's).</summary>
        public static TheatreGrade Current { get; private set; }

        public bool Enabled = TheatreSkin.Dial("EVOSIM_THEATRE_GRADE", 1f, 0f, 1f) >= 0.5f;
        public float PostExposure = TheatreSkin.Dial("EVOSIM_THEATRE_EXPOSURE", 0.4f, -4f, 4f);
        public float Contrast = TheatreSkin.Dial("EVOSIM_THEATRE_CONTRAST", 8f, -50f, 50f);
        public float BloomIntensity = TheatreSkin.Dial("EVOSIM_THEATRE_BLOOM", 0.35f, 0f, 3f);
        public float BloomThreshold = 0.9f;
        public float BloomScatter = 0.6f;
        public float VignetteIntensity = TheatreSkin.Dial("EVOSIM_THEATRE_VIGNETTE", 0.22f, 0f, 1f);

        private GameObject _holder;
        private Volume _volume;
        private VolumeProfile _profile;
        private DepthOfField _depthOfField;

        /// <summary>Builds the global volume and its profile, once, and remembers itself as current.</summary>
        public void Apply()
        {
            Current = this;
            if (_holder != null) return;

            _holder = new GameObject("Theatre Grade") { hideFlags = HideFlags.DontSave };
            _volume = _holder.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.weight = Enabled ? 1f : 0f;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Theatre Grade (built in code)";
            _profile.hideFlags = HideFlags.DontSave;

            var tone = _profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var adjust = _profile.Add<ColorAdjustments>(true);
            adjust.postExposure.Override(PostExposure);
            adjust.contrast.Override(Contrast);

            var bloom = _profile.Add<Bloom>(true);
            bloom.intensity.Override(BloomIntensity);
            bloom.threshold.Override(BloomThreshold);
            bloom.scatter.Override(BloomScatter);
            bloom.highQualityFiltering.Override(true);

            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(0.5f);

            // Held ready and off: a portrait turns it on with a focus distance; a census view
            // never does, since a diagram in focus everywhere is the point of a diagram.
            _depthOfField = _profile.Add<DepthOfField>(true);
            _depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            _depthOfField.active = false;

            _volume.sharedProfile = _profile;
        }

        /// <summary>
        /// Lets a camera render through the stack: post-processing on, SMAA for the edges
        /// (TAA cannot be combined with MSAA and is unverified into a render texture under
        /// batch mode; SMAA works in both).
        /// </summary>
        public static void Attach(Camera camera)
        {
            if (camera == null) return;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data == null) return;

            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
        }

        /// <summary>
        /// Focus at a distance, for a portrait: the near and far fall into the fog's own blur
        /// twice over. Off again with <see cref="Unfocus"/>.
        /// </summary>
        public void Focus(float distanceMetres, float aperture = 4f)
        {
            if (_depthOfField == null) return;

            _depthOfField.focusDistance.Override(Mathf.Max(0.1f, distanceMetres));
            _depthOfField.aperture.Override(Mathf.Clamp(aperture, 1f, 32f));
            _depthOfField.focalLength.Override(50f);
            _depthOfField.active = true;
        }

        public void Unfocus()
        {
            if (_depthOfField != null) _depthOfField.active = false;
        }

        /// <summary>The look's token for a picture's label: <c>look 2</c>, or <c>look 1</c> with the grade off.</summary>
        public string LabelToken => string.Format(CultureInfo.InvariantCulture, "look {0}", Enabled ? LookVersion : 1);

        public void Dispose()
        {
            if (Current == this) Current = null;
            if (_profile != null) UnityEngine.Object.DestroyImmediate(_profile);
            if (_holder != null) UnityEngine.Object.DestroyImmediate(_holder);
            _profile = null; _holder = null; _volume = null; _depthOfField = null;
        }
    }
}
