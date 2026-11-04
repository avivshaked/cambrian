using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// The bite bubbles (<c>EVOSIM_THEATRE_BITE_BUBBLES</c>, <see cref="BiteBubbles"/>): at each landed bite a small burst of air
    /// bubbles at the mouth, which rise and dissolve by the birth bubbles' own laws. A bitten leaf holds gas in its tissue and
    /// releases a good deal of it; an animal's flesh releases a little.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing exists.</b> The dial is read only with the bite pulse on
    /// (<c>EVOSIM_THEATRE_MOUTH2_BITE</c>), whose onsets it reads, and is independent of the crumbs' dial and the plume's: it works with
    /// the crumbs off and the crumbs draw what they drew with it on. <see cref="Frame"/> calls <see cref="BubbleFrame"/> only with
    /// the dial on, beside the crumbs' and the plume's calls; with it off no layer, mesh, material, pass, hook, list or log line is
    /// made, and no existing frame changes.
    /// </para>
    /// <para>
    /// <b>Which bites.</b> The crumbs' own onsets, classification and catch rule, by the crumbs' own functions
    /// (<see cref="CrumbKillOf"/>, <see cref="CrumbVictimTint"/>, <see cref="CrumbNearestPrey"/>): a kill row of the same eater and part
    /// within a pose frame of the onset makes the bite a kill (the whole body taken) or a cut (a part), and none makes it a wound.
    /// The prey is a leaf (a photosynthetic part) or, for every other prey the crumbs name (an animal, a corpse, remains, none known),
    /// an animal, which is the smaller release. An onset whose own frame the film skips is captured at the first shown frame within
    /// <see cref="CrumbCatchFilmSeconds"/> of film after it, or not at all.
    /// </para>
    /// <para>
    /// <b>A bubble is a function of (eater, part, onset, index).</b> Its count by kind and prey, its diameter, place in the ball
    /// about the mouth, moment of release and every number of its rise and wobble are hashed from the eater's id, the part, the onset's
    /// second to the millisecond and the index, with salts 301 to 313, which neither the crumbs (1 to 52) nor the plume (101 to 236)
    /// use, so a film draws the same burst every time. A bubble is released over <see cref="ReleaseSeconds"/> of the run, from a ball
    /// of <see cref="BallRadii"/> mouth radii a little ahead of the mouth, and its rise, wobble, dissolving and fade are
    /// <see cref="TheatreBirthBubbles.Place"/>: the very lines the birth bubbles draw with. Time is the run's, so a time-lapse
    /// compresses the burst as it compresses the birth's.
    /// </para>
    /// <para>
    /// <b>Drawn in a layer of its own</b> (<see cref="TheatreBubbleLayer"/>) with the birth bubbles' shader, in a mesh and a pass
    /// apart from the crumbs'. The crumbs draw with the flake shader, so sharing their mesh would mean a second material and a second
    /// submesh, and it would tie the bubbles to the crumbs' dial; a layer of their own keeps the two dials independent and leaves the
    /// crumbs' code as it was. The current at the burst's centre at the onset carries the bubbles, read once as the crumbs read theirs.
    /// </para>
    /// </remarks>
    public static partial class TheatreMouthBite
    {
        // ---------------------------------------------------------------- the dial

        /// <summary>
        /// The bite bubbles (<c>EVOSIM_THEATRE_BITE_BUBBLES</c>, 0 or 1, 0 off by default), read once and only with
        /// <see cref="TheatreMouth2.Bite"/> on, whose onsets it reads. Independent of <see cref="TheatreMouth2.BiteCrumbs"/> and
        /// <see cref="WoundPlume"/>.
        /// </summary>
        public static readonly bool BiteBubbles =
            TheatreMouth2.Bite > 0f && TheatreSkin.Dial("EVOSIM_THEATRE_BITE_BUBBLES", 0f, 0f, 1f) >= 0.5f;

        // ---------------------------------------------------------------- the constants (D262: started strong, to be dialled back)

        /// <summary>The run's seconds over which a burst's bubbles are released.</summary>
        private const float BubbleReleaseSeconds = 0.3f;

        /// <summary>
        /// The bubbles of a bite, by kind and prey, hashed between these (both ends reached): a leaf holds gas in its tissue, so a
        /// bitten one releases more; a kill releases most. A cut and a wound are alike.
        /// </summary>
        private const int BubbleLeafWoundLow = 45, BubbleLeafWoundHigh = 75;

        private const int BubbleLeafKillLow = 120, BubbleLeafKillHigh = 180;
        private const int BubbleAnimalWoundLow = 12, BubbleAnimalWoundHigh = 24;
        private const int BubbleAnimalKillLow = 45, BubbleAnimalKillHigh = 75;

        /// <summary>
        /// The diameters, metres, on the birth bubbles' power law toward the small: a leaf's 2 to 20 mm, the birth bubbles' own range,
        /// an animal's 2 to 12 mm. At 1 to 5 mm a burst drew single pixels at the close framing.
        /// </summary>
        private const float BubbleDiameterLow = 0.002f;

        private const float BubbleLeafDiameterHigh = 0.02f;
        private const float BubbleAnimalDiameterHigh = 0.012f;

        /// <summary>The ball the burst starts in: its radius in mouth radii (held to <see cref="BubbleBallLeast"/>, metres), and how far ahead of the mouth along its normal its centre stands, in mouth radii.</summary>
        private const float BubbleBallRadii = 0.8f;

        private const float BubbleBallLeast = 0.004f;
        private const float BubbleMouthAhead = 0.3f;

        /// <summary>A bubble outside the frustum by more than this, metres, is not built.</summary>
        private const float BubbleMargin = 0.05f;

        /// <summary>A burst's last bubble is gone this many run seconds after the onset: the release, and the longest life a bubble has.</summary>
        private const float BubbleLifeSeconds = BubbleReleaseSeconds + TheatreBirthBubbles.EndMostSeconds;

        // ---------------------------------------------------------------- one onset's burst

        /// <summary>One onset's burst: the bite, its kind and prey, the ball's centre, the current there, and the bubbles.</summary>
        private sealed class BubbleBite
        {
            public long Eater;
            public int Part;
            public double Onset;
            public int Match;
            public int Prey;
            public Vector3 Centre;

            /// <summary>The current at the centre at the onset, read once the run's water is open (or known to stay shut).</summary>
            public bool CurrentKnown;

            public Vector3 Current;
            public TheatreBirthBubbles.Bubble[] Bubbles;
        }

        // ---------------------------------------------------------------- the state (made at the first frame, with the dial on)

        private static TheatreBubbleLayer _bubbleLayer;
        private static List<BubbleBite> _bubbleBites;
        private static HashSet<(long, int, double)> _bubbleSeen;
        private static List<BiteOnset> _bubbleOnsets;

        private static FilmWindowWorld _bubbleWindow;
        private static int _bubbleFrame = -1;
        private static bool _bubbleTold;

        // The capture's count, told once at the CountFrames-th frame: the onsets met and what became of them.
        private const int CountFrames = 90;
        private static int _countFrames, _countOnsets, _countLate, _countCaught, _countNoMouth;
        private static bool _countTold;

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Called by <see cref="Frame"/> with every rendered frame of a film window, with the dial on, the crumbs' on or off: captures
        /// the burst of each onset of the last <see cref="BubbleLifeSeconds"/> of the run not yet seen in this pass, at its own frame
        /// (or the first shown just after it), and forgets the bursts that are gone.
        /// </summary>
        private static void BubbleFrame(FilmWindowWorld window, BiteOnsets onsets, double speed)
        {
            if (window == null || onsets == null || window.FrameIndex < 0 || double.IsNaN(window.Second)) return;

            if (_bubbleLayer == null)
            {
                _bubbleLayer = new TheatreBubbleLayer("bite bubbles", BuildBubbles);
                _bubbleBites = new List<BubbleBite>();
                _bubbleSeen = new HashSet<(long, int, double)>();
                _bubbleOnsets = new List<BiteOnset>();
            }

            if (!_bubbleLayer.Ensure()) return;

            bool replaced = !ReferenceEquals(window, _bubbleWindow);
            bool reopened = replaced || window.FrameIndex < _bubbleFrame;

            if (!reopened && window.FrameIndex == _bubbleFrame) return;

            // A scene that plays the window again from earlier forgets the bursts, so each pass is drawn from its own frames.
            if (reopened)
            {
                _bubbleBites.Clear();
                _bubbleSeen.Clear();
            }

            _bubbleWindow = window;
            _bubbleFrame = window.FrameIndex;

            double second = window.Second;
            FilmWindowReader reader = window.Window;

            onsets.Between(second - BubbleLifeSeconds, second, speed, _bubbleOnsets);

            for (int i = 0; i < _bubbleOnsets.Count; i++)
            {
                BiteOnset onset = _bubbleOnsets[i];
                var key = (onset.Id, onset.Part, onset.Second);
                if (_bubbleSeen.Contains(key)) continue;

                int at = reader.NearestFrame(onset.Second);
                if (at > window.FrameIndex) continue;

                _bubbleSeen.Add(key);
                _countOnsets++;

                // Its own frame skipped and none shown near enough after it: no burst, as no crumbs (the crumbs' catch rule).
                if (at != window.FrameIndex && (second - onset.Second) / speed > CrumbCatchFilmSeconds)
                {
                    _countLate++;
                    continue;
                }

                BubbleBite bite = CaptureBubbles(window, onset);
                if (bite != null)
                {
                    _bubbleBites.Add(bite);
                    _countCaught++;
                }
                else
                {
                    _countNoMouth++;
                }
            }

            if (++_countFrames == CountFrames && !_countTold)
            {
                _countTold = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] bite bubbles: capture count after {0} frames: {1} onsets met, {2} too late for their frame, {3} caught, " +
                    "{4} with no mouth drawn; {5} bursts held at t {6:0.###}",
                    _countFrames, _countOnsets, _countLate, _countCaught, _countNoMouth, _bubbleBites.Count, second));
            }

            // The bursts that are gone, forgotten; kept in their order, so the quads are written in the same order every time.
            _bubbleBites.RemoveAll(b => b.Onset < second - BubbleLifeSeconds);

            _bubbleLayer.Second = second;
            _bubbleLayer.Version++;
        }

        // ---------------------------------------------------------------- one onset

        /// <summary>One onset's burst, from the frame on screen; null where the eater's mouth is not drawn.</summary>
        private static BubbleBite CaptureBubbles(FilmWindowWorld window, BiteOnset onset)
        {
            if (!TryBody(window, onset.Id, out Phenotype eater, out Transform[] eaterParts) ||
                onset.Part < 0 || onset.Part >= eater.PartCount || onset.Part >= eaterParts.Length || eaterParts[onset.Part] == null ||
                !TryMouth(eater, eaterParts, onset.Part, out Vector3 mouth, out float radius) || !(radius > 0f))
            {
                return null;
            }

            TheatreMouth2.Measured measured = TheatreMouth2.Measure(eater, onset.Part);
            Vector3 normal = MouthNormal(eaterParts[onset.Part], measured.Point);
            if (!(normal.sqrMagnitude > 0.5f)) normal = Vector3.up;

            // The crumbs' classification, by their own functions: the kill row of this eater and part, else the nearest drawn prey.
            KillRow kill = CrumbKillOf(window, onset);
            int match;
            int prey;

            if (kill != null)
            {
                match = kill.Root == 1 ? CrumbKill : CrumbCut;
                CrumbVictimTint(window, kill.Victim, kill.VictimPart, onset.Second, out prey, out Color _);
            }
            else
            {
                match = CrumbWound;
                CrumbNearestPrey(window, onset.Id, mouth, radius, out prey, out Color _, out long _, out int _);
            }

            // A leaf holds gas; every other prey (an animal, a corpse, remains, none known) is the smaller release.
            bool leaf = prey == PreyLeaf;
            bool kills = match == CrumbKill;

            int low = leaf ? (kills ? BubbleLeafKillLow : BubbleLeafWoundLow) : (kills ? BubbleAnimalKillLow : BubbleAnimalWoundLow);
            int high = leaf ? (kills ? BubbleLeafKillHigh : BubbleLeafWoundHigh) : (kills ? BubbleAnimalKillHigh : BubbleAnimalWoundHigh);
            float diameterHigh = leaf ? BubbleLeafDiameterHigh : BubbleAnimalDiameterHigh;

            long id = onset.Id;
            int part = onset.Part;
            double at = onset.Second;

            int count = Math.Min(high, low + (int)(CrumbHash(id, part, at, 0, 301) * (high - low + 1)));

            var bite = new BubbleBite
            {
                Eater = id,
                Part = part,
                Onset = at,
                Match = match,
                Prey = prey,
                Centre = mouth + normal * (BubbleMouthAhead * radius),
                Bubbles = new TheatreBirthBubbles.Bubble[count],
            };

            float ball = Mathf.Max(BubbleBallRadii * radius, BubbleBallLeast);

            for (int i = 0; i < count; i++)
            {
                float diameter = TheatreBirthBubbles.PowerDiameter(CrumbHash(id, part, at, i, 302), BubbleDiameterLow, diameterHigh);

                // Uniform through the ball, each bubble at a hashed place in it and a hashed moment of the release.
                Vector3 way = CrumbUnit(CrumbHash(id, part, at, i, 303), CrumbHash(id, part, at, i, 304));
                Vector3 offset = way * (ball * Mathf.Pow(CrumbHash(id, part, at, i, 305), 1f / 3f));
                float release = BubbleReleaseSeconds * CrumbHash(id, part, at, i, 306);

                bite.Bubbles[i] = TheatreBirthBubbles.Single(
                    diameter, offset, release, 0f, 0f,
                    CrumbHash(id, part, at, i, 307), CrumbHash(id, part, at, i, 308), CrumbHash(id, part, at, i, 309),
                    CrumbHash(id, part, at, i, 310), CrumbHash(id, part, at, i, 311), CrumbHash(id, part, at, i, 312),
                    CrumbHash(id, part, at, i, 313));
            }

            return bite;
        }

        // ---------------------------------------------------------------- the drawing

        /// <summary>Writes every shown burst for one camera into the layer. Returns the bubbles written.</summary>
        private static int BuildBubbles(TheatreBubbleLayer layer, Camera camera, TheatreSkin skin)
        {
            bool open = skin.RunWaterOpen;
            int drawn = 0;
            BubbleBite first = null;

            for (int b = 0; b < _bubbleBites.Count; b++)
            {
                BubbleBite bite = _bubbleBites[b];
                float age = (float)(layer.Second - bite.Onset);
                if (age < 0f) continue;

                // The current at the burst's centre at the onset: a function of the place and the second, read once.
                if (!bite.CurrentKnown)
                {
                    bite.Current = open ? skin.WaterAt(bite.Centre, bite.Onset) : Vector3.zero;
                    bite.CurrentKnown = true;
                }

                Vector3 drift = bite.Centre + bite.Current * age;

                for (int i = 0; i < bite.Bubbles.Length; i++)
                {
                    TheatreBirthBubbles.Bubble bubble = bite.Bubbles[i];
                    if (!TheatreBirthBubbles.Place(in bubble, age, drift, out TheatreBirthBubbles.Placed placed)) continue;
                    if (!layer.InFrustum(placed.Place, BubbleMargin + placed.Radius)) continue;

                    // uv1: the radius, the fade, the seed, 0 (a sphere).
                    layer.AddQuad(placed.Place, new Vector4(placed.Radius, placed.Fade, bubble.Seed, 0f));
                    drawn++;
                    if (first == null) first = bite;
                }
            }

            if (first != null && !_bubbleTold) TellBubbles(first);
            return drawn;
        }

        /// <summary>The one log line, at the first frame a bite bubble is drawn: the dial took effect.</summary>
        private static void TellBubbles(BubbleBite first)
        {
            _bubbleTold = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] bite bubbles (EVOSIM_THEATRE_BITE_BUBBLES): a burst of air at the mouth of each landed bite, with the crumbs' onsets and kinds; " +
                "leaf wound/cut {0}-{1}, leaf kill {2}-{3}, animal wound/cut {4}-{5}, animal kill {6}-{7} bubbles of {8:0}-{9:0} mm (an animal's to {10:0}), " +
                "released over {11:0.##} s of the run, gone by {12:0.##} s; first: t {13:0.###} eater {14} part {15}, {16} on a {17}, {18} bubbles.",
                BubbleLeafWoundLow, BubbleLeafWoundHigh, BubbleLeafKillLow, BubbleLeafKillHigh, BubbleAnimalWoundLow, BubbleAnimalWoundHigh,
                BubbleAnimalKillLow, BubbleAnimalKillHigh, 1000f * BubbleDiameterLow, 1000f * BubbleLeafDiameterHigh,
                1000f * BubbleAnimalDiameterHigh, BubbleReleaseSeconds, BubbleLifeSeconds, first.Onset, first.Eater, first.Part,
                CrumbMatchWords[first.Match], first.Prey == PreyLeaf ? "leaf" : "animal", first.Bubbles.Length));
        }
    }
}
