using System;
using System.Globalization;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The story film's lighter look (the owner on round 48's first story film, 2026-09-25:
    /// "everything looks a bit darkish... i wonder if that's something we could take care of"):
    /// lighter water, ambient and fog, a softer vignette, a lamp on the portraits and births, and a
    /// per-shot exposure meter (<see cref="StoryExposure"/>). On only in story mode unless asked
    /// for, so every snapshot, film and census picture in the record keeps the look it was taken
    /// in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the first film was dark.</b> The agent's sampling of it (three frames a scene, 22
    /// scenes) put 18 scenes at a mean luma of 6% to 19% of the way from black to white and the
    /// brightest, a descent from the surface, at 34%. The dark field is the census's look on
    /// purpose (<see cref="TheatreSkin"/>'s remarks): a near black water, a low ambient, a fog that
    /// leaves a crowd 30 m off two thirds fog colour, and a vignette. A documentary frame of lit
    /// water sits nearer a third to a half (the caller's rough guess, not a measurement).
    /// </para>
    /// <para>
    /// <b>Deeper stays darker.</b> Every change keeps the water column's gradient: the deep and
    /// the shallow are both lifted and the shallow more, the lit water reaches further down but
    /// still falls off, the lamp falls off with distance, and the meter's target itself falls with
    /// the camera's depth (<see cref="LumaDepthSlope"/>) and may move only so far within a shot
    /// (<see cref="StoryExposure.TrackRangeEv"/>), so a descent still darkens as it goes down.
    /// </para>
    /// <para>
    /// Dials, all read once: <c>EVOSIM_THEATRE_STORY_LOOK</c> (unset: on in story mode only; 1 on
    /// for any safari; 0 off), <c>_DEEP</c>, <c>_SHALLOW</c>, <c>_AMBIENT</c>, <c>_FOG</c>,
    /// <c>_REACH</c>, <c>_VIGNETTE</c>, <c>_LAMP</c>, <c>_LUMA</c> (0 turns the meter off),
    /// <c>_LUMA_DEPTH</c>, <c>_EV_MIN</c>, <c>_EV_MAX</c>, each after <c>EVOSIM_THEATRE_STORY</c>.
    /// </para>
    /// </remarks>
    public sealed class StoryLook
    {
        /// <summary>The deep water's colour times this: 2.4 takes (0.012, 0.032, 0.048) to (0.029, 0.077, 0.115).</summary>
        public float DeepGain = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_DEEP", 2.4f, 0.25f, 8f);

        /// <summary>The lit water at the waterline, as the skin's <c>ShallowGain</c>: 1.6 in place of 1.</summary>
        public float ShallowGain = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_SHALLOW", 1.6f, 0f, 3f);

        /// <summary>The flat ambient's colour times this.</summary>
        public float AmbientGain = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_AMBIENT", 2.6f, 0.25f, 8f);

        /// <summary>
        /// The fog's density, as URP's exponential squared fog reads it: 0.022 leaves a crowd 30 m
        /// off about two thirds of its own light, where the census's 0.035 leaves a third.
        /// </summary>
        public float FogDensity = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_FOG", 0.022f, 0.002f, 0.08f);

        /// <summary>Metres over which the lit water falls to the deep: 26 in place of the light's 18.</summary>
        public float WaterReachMetres = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_REACH", 26f, 1f, 200f);

        /// <summary>The vignette: 0.12 in place of 0.22.</summary>
        public float Vignette = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_VIGNETTE", 0.12f, 0f, 1f);

        /// <summary>The lamp on a portrait and a birth (<see cref="SnapshotCamera.LampIntensity"/>); 0 none.</summary>
        public float Lamp = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_LAMP", 1.2f, 0f, 8f);

        /// <summary>
        /// The meter's target mean luma at mid-depth, 0 to 1 of full scale; 0 turns the meter off.
        /// 0.38 at first, whose frames of round 48 measured 0.43 and which the owner called too
        /// bright (2026-09-25); the first film's 0.06 to 0.19 was too dark. 0.26 gave frames of
        /// 0.23 to 0.29 and was still too bright; of 0.20 and 0.155 rendered side by side the owner
        /// chose 0.155 ("luma15 is better").
        /// With the starving shade on, which dims most bodies, 0.18 over 0.155 ("this is pretty good").
        /// </summary>
        public float TargetLuma = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_LUMA", 0.18f, 0f, 0.8f);

        /// <summary>
        /// How much the target falls from the surface to the bed, as a fraction of it: 0.4 aims a
        /// frame taken at the surface at 1.2 times the target and one at the bed at 0.8 times it.
        /// </summary>
        public float LumaDepthSlope = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_LUMA_DEPTH", 0.4f, 0f, 1f);

        /// <summary>
        /// How bright a body with no reserve is drawn, as a fraction of a sated one's
        /// (<see cref="TheatrePalette.Starving"/>, the census's 0.45 by default). A story film shades
        /// its bodies by reserve since the window stream carries it (2026-09-25), and at 0.45 a
        /// crowd of starving leaves took the frame's highlights with it: the owner found the
        /// picture "slightly dark again" at the brightness chosen before the shading came back.
        /// </summary>
        public float StarvingBrightness = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_STARVING", 0.45f, 0f, 1f);

        /// <summary>The post-exposure the meter may use, EV, lowest and highest.</summary>
        public float LowestEv = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_EV_MIN", -1f, -6f, 6f);
        public float HighestEv = TheatreSkin.Dial("EVOSIM_THEATRE_STORY_EV_MAX", 3.5f, -6f, 8f);

        // what Apply changed, for Restore
        private TheatrePalette _palette;
        private float _starving;
        private TheatreSkin _skin;
        private TheatreGrade _grade;
        private Color _water, _ambient;
        private float _shallowGain, _fog, _reach, _vignette, _exposure;
        private bool _applied;

        /// <summary>
        /// Whether a trip takes the look: <c>EVOSIM_THEATRE_STORY_LOOK=0</c> never, <c>=1</c>
        /// always, and unset only for a story.
        /// </summary>
        public static bool Wanted(bool story)
        {
            string text = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_STORY_LOOK") ?? "").Trim();
            if (text == "0" || text.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            if (text == "1" || text.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
            return story;
        }

        /// <summary>
        /// Lightens the skin's water, ambient and fog and the grade's vignette, and says what it did
        /// in one line for the log. <see cref="Restore"/> puts everything back.
        /// </summary>
        public string Apply(TheatreSkin skin, TheatreGrade grade)
        {
            if (_applied) return "the story look is already applied";

            _skin = skin;
            _grade = grade;

            if (skin != null)
            {
                _water = skin.Water;
                _ambient = skin.Ambient;
                _shallowGain = skin.ShallowGain;
                _fog = skin.FogDensity;
                _reach = skin.WaterReachMetres;

                skin.Water = Scaled(_water, DeepGain);
                skin.Ambient = Scaled(_ambient, AmbientGain);
                skin.ShallowGain = ShallowGain;
                skin.FogDensity = FogDensity;
                skin.WaterReachMetres = WaterReachMetres;
                skin.Relight();
            }

            if (grade != null)
            {
                _vignette = grade.VignetteNow;
                _exposure = grade.Exposure;
                grade.SetVignette(Vignette);
            }

            _applied = true;

            return string.Format(CultureInfo.InvariantCulture,
                "the story look: deep water x{0:0.##} ({1}), lit water x{2:0.##}, ambient x{3:0.##}, fog {4:0.###} (census {5:0.###}), " +
                "reach {6:0} m, vignette {7:0.##}, lamp {8:0.##} on portraits and births, meter {9}{10}",
                DeepGain, skin != null ? Rgb(skin.Water) : "no skin", ShallowGain, AmbientGain, FogDensity, _fog,
                WaterReachMetres, Vignette, Lamp,
                TargetLuma > 0f
                    ? string.Format(CultureInfo.InvariantCulture, "at {0:0.##} of full scale at mid-depth ({1:0.##} at the surface, {2:0.##} at the bed), {3:0.#} to {4:0.#} EV",
                        TargetLuma, TargetAt(0f), TargetAt(1f), LowestEv, HighestEv)
                    : "off",
                grade == null ? "; NO GRADE, so the meter has nothing to move" : "");
        }

        /// <summary>Puts back what <see cref="Apply"/> changed, the exposure included.</summary>
        /// <summary>Sets the palette's starving brightness to <see cref="StarvingBrightness"/>, restored by <see cref="Restore"/>.</summary>
        public string ApplyTo(TheatrePalette palette)
        {
            if (palette == null || _palette != null) return "the palette is not reached";
            _palette = palette;
            _starving = palette.Starving;
            palette.Starving = StarvingBrightness;
            return string.Format(CultureInfo.InvariantCulture,
                "a starving body at {0:0.##} of a sated one's brightness (census {1:0.##})", StarvingBrightness, _starving);
        }

        public void Restore()
        {
            if (_palette != null)
            {
                _palette.Starving = _starving;
                _palette = null;
            }

            if (!_applied) return;
            _applied = false;

            if (_skin != null)
            {
                _skin.Water = _water;
                _skin.Ambient = _ambient;
                _skin.ShallowGain = _shallowGain;
                _skin.FogDensity = _fog;
                _skin.WaterReachMetres = _reach;
                _skin.Relight();
            }

            if (_grade != null)
            {
                _grade.SetVignette(_vignette);
                _grade.SetExposure(_exposure);
            }
        }

        /// <summary>The meter's target for a camera at a depth, as a fraction of the world's (0 the surface, 1 the bed).</summary>
        public float TargetAt(float depth01) =>
            Mathf.Clamp(TargetLuma * (1f + LumaDepthSlope * (0.5f - Mathf.Clamp01(depth01))), 0.02f, 0.9f);

        private static Color Scaled(Color c, float gain) =>
            new Color(Mathf.Clamp01(c.r * gain), Mathf.Clamp01(c.g * gain), Mathf.Clamp01(c.b * gain), 1f);

        private static string Rgb(Color c) =>
            string.Format(CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}, {2:0.###}", c.r, c.g, c.b);
    }

    /// <summary>
    /// The story's per-shot exposure meter: each take settles on its first pose before its first
    /// frame is kept, then follows the picture slowly within a narrow range, so a shot never
    /// flickers and a descent still darkens as it goes down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The measurement</b> is the camera's own (<see cref="SnapshotCamera.LastMeanLuma"/>): the
    /// mean luma of the frame as rendered, before its label and caption, with a corner chart's
    /// card left out. A frame under a full chart is not tracked at all: the card dims the world on
    /// purpose, and a meter that answered it would brighten the world behind it.
    /// </para>
    /// <para>
    /// <b>The model</b> is a picture whose stored bytes go as the light to the power 1/2.2, so an
    /// error of a ratio r in luma is <c>2.2 log2 r</c> EV. The neutral tonemapper compresses the
    /// top of the range, so the model under-asks there and the settle converges from below; its
    /// gain is under one so a wrong model cannot make it ring.
    /// </para>
    /// </remarks>
    public sealed class StoryExposure
    {
        /// <summary>The most warm-up renders a take spends settling before its first frame.</summary>
        public const int MostSettleFrames = 24;

        /// <summary>A settle is done when the error is under this, EV, on two renders running.</summary>
        public const float SettledWithinEv = 0.12f;

        /// <summary>How far a shot's exposure may follow the picture from where it settled, EV, either way.</summary>
        public const float TrackRangeEv = 0.6f;

        private const float SettleGain = 0.7f;
        private const float MostSettleStepEv = 1.2f;
        private const float TrackGain = 0.05f;
        private const float MostTrackStepEv = 0.02f;

        private readonly StoryLook _look;

        /// <summary>The post-exposure the next render takes, EV.</summary>
        public float Ev { get; private set; }

        private int _calm;
        private float _settledEv;

        // the take's tally, for the log line
        private string _take;
        private float _firstLuma = float.NaN;
        private int _settleRenders;
        private int _frames, _held;
        private double _sum;
        private float _least = float.PositiveInfinity, _most = float.NegativeInfinity;
        private float _lowEv = float.PositiveInfinity, _highEv = float.NegativeInfinity;
        private float _lastTarget = float.NaN;

        public StoryExposure(StoryLook look, float startEv)
        {
            _look = look;
            Ev = Mathf.Clamp(startEv, look.LowestEv, look.HighestEv);
        }

        /// <summary>True once the take's settle has converged.</summary>
        public bool Settled => _calm >= 2;

        /// <summary>A new take: the exposure carries over from the last, and the settle starts again.</summary>
        public void BeginTake(string name)
        {
            _take = name;
            _calm = 0;
            _settleRenders = 0;
            _firstLuma = float.NaN;
            _frames = _held = 0;
            _sum = 0d;
            _least = float.PositiveInfinity;
            _most = float.NegativeInfinity;
            _lowEv = _highEv = Ev;
            _settledEv = Ev;
        }

        /// <summary>One warm-up render's luma: a fast, damped step toward the target.</summary>
        public void Settle(float luma, float depth01)
        {
            if (float.IsNaN(luma)) return;
            if (float.IsNaN(_firstLuma)) _firstLuma = luma;
            _settleRenders++;

            float error = ErrorEv(luma, depth01);
            _calm = Mathf.Abs(error) < SettledWithinEv ? _calm + 1 : 0;
            Ev = Mathf.Clamp(Ev + Mathf.Clamp(SettleGain * error, -MostSettleStepEv, MostSettleStepEv), _look.LowestEv, _look.HighestEv);
            _settledEv = Ev;
            _lowEv = _highEv = Ev;
        }

        /// <summary>One written frame's luma: a slow step, held within <see cref="TrackRangeEv"/> of the settle.</summary>
        public void Track(float luma, float depth01)
        {
            if (float.IsNaN(luma)) return;
            Tally(luma);

            float error = ErrorEv(luma, depth01);
            float low = Mathf.Max(_look.LowestEv, _settledEv - TrackRangeEv);
            float high = Mathf.Min(_look.HighestEv, _settledEv + TrackRangeEv);
            Ev = Mathf.Clamp(Ev + Mathf.Clamp(TrackGain * error, -MostTrackStepEv, MostTrackStepEv), low, high);
            _lowEv = Mathf.Min(_lowEv, Ev);
            _highEv = Mathf.Max(_highEv, Ev);
        }

        /// <summary>A written frame the meter does not answer (under a full chart): counted, the exposure held.</summary>
        public void Hold(float luma)
        {
            _held++;
            if (!float.IsNaN(luma)) Tally(luma);
        }

        /// <summary>The take's line for the log: where it started, how it settled, and what its frames measured.</summary>
        public string TakeLine()
        {
            string frames = _frames > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} frames at mean luma {1:0.###} (least {2:0.###}, most {3:0.###})",
                    _frames, _sum / _frames, _least, _most)
                : "no frames measured";
            return string.Format(CultureInfo.InvariantCulture,
                "exposure for {0}: first render {1}, settled at {2:+0.00;-0.00} EV in {3} render(s){4} toward {5:0.###}; {6}; exposure {7:+0.00;-0.00} to {8:+0.00;-0.00} EV{9}",
                _take ?? "a take",
                float.IsNaN(_firstLuma) ? "not measured" : "luma " + _firstLuma.ToString("0.###", CultureInfo.InvariantCulture),
                _settledEv, _settleRenders, Settled ? "" : " (NOT settled: the render cap)",
                _lastTarget, frames, _lowEv, _highEv,
                _held > 0 ? ", held on " + _held + " frame(s) under a full chart" : "");
        }

        private float ErrorEv(float luma, float depth01)
        {
            float target = _look.TargetAt(depth01);
            _lastTarget = target;
            return 2.2f * Mathf.Log(target / Mathf.Max(luma, 0.004f), 2f);
        }

        private void Tally(float luma)
        {
            _frames++;
            _sum += luma;
            _least = Mathf.Min(_least, luma);
            _most = Mathf.Max(_most, luma);
        }
    }
}
