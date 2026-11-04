using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// The swimming stroke D135 leaves to the theatre, drawn from the power a film window logged
    /// and in the owner's curve (<see cref="SwimStrokeCurve"/>), over poses that hold the limbs
    /// still. Off by default: <c>EVOSIM_THEATRE_SWIM_STROKE</c> above 0 turns it on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is drawn.</b> Under D135 the solver never swings a powered limb: a joint's power
    /// pushes the body and the limb stays at its bend. So a recorded pose shows a swimmer that
    /// glides without a stroke. The farm writes <c>film.stroke.jsonl</c> beside
    /// <c>film.poses.bin</c>, one line a pose frame, in the same order and at the same seconds,
    /// listing each degree of freedom that had power: its organism, link and index, its power from
    /// 0 to 1, its hinge pivot and its rotation axis in the world. This class reads that and
    /// swings each listed link about its pivot by <see cref="SwimStrokeCurve.Amplitude"/> times
    /// <see cref="SwimStrokeCurve.Ease"/> of a phase it integrates from <see cref="SwimStrokeCurve.PhaseRate"/>,
    /// as <c>scripts/render-swim-film.py</c> draws it, a child carried by its parent's swing.
    /// </para>
    /// <para>
    /// <b>A twist draws nothing.</b> The push ignores twist, so a degree of freedom whose axis is
    /// within <see cref="TwistDegrees"/> of the link's long axis is skipped.
    /// </para>
    /// <para>
    /// <b>The chain wave is the theatre's reading of ruling 9 and not a ruling.</b> A run of
    /// powered links, each the child of the one before, is a chain. A short deep chain moves in
    /// step; a long thin one carries up to half a wave along it, each link's phase lagging its
    /// parent's by the share of the chain's length between their joints. A link alone is a chain
    /// of one with no lag. The owner has not ruled on this; it is shown for ruling.
    /// </para>
    /// <para>
    /// <b>Picture only.</b> It changes the part positions and rotations the view is handed and
    /// nothing in any simulation. With the dial off no file is read and nothing is called.
    /// </para>
    /// </remarks>
    public sealed class TheatreSwimStroke
    {
        /// <summary>The file the farm writes beside the pose stream.</summary>
        public const string FileName = "film.stroke.jsonl";

        /// <summary>Whether the stroke is drawn (<c>EVOSIM_THEATRE_SWIM_STROKE</c>, off at 0).</summary>
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_SWIM_STROKE", 0f, 0f, 1f) > 0f;

        /// <summary>A degree of freedom whose axis lies this close to the link's long axis is a twist.</summary>
        public const float TwistDegrees = 25f;

        // The chain wave, the theatre's reading of ruling 9 and not a ruling. A chain's slenderness
        // is its length over its mean thickness; below WaveSlenderFloor it moves in step, and the
        // wave builds over WaveSlenderSpan more until it carries WaveHalfWave (half a wave, pi of
        // phase) from the chain's first joint to its last.
        private const double WaveSlenderFloor = 2.0;
        private const double WaveSlenderSpan = 6.0;
        private const double WaveHalfWave = Math.PI;

        /// <summary>The farm's seconds against the stroke file's, beyond which a frame is not drawn.</summary>
        private const double SecondsTolerance = 0.002;

        /// <summary>One powered degree of freedom as the file lists it.</summary>
        private struct Dof
        {
            public int Link;
            public int Index;
            public float Power;
            public Vector3 Pivot;
            public Vector3 Axis;
        }

        /// <summary>One frame's powered degrees of freedom by organism.</summary>
        private sealed class StrokeFrame
        {
            public double Seconds;
            public Dictionary<long, List<Dof>> Bodies = new Dictionary<long, List<Dof>>();
        }

        private readonly List<StrokeFrame> _frames = new List<StrokeFrame>();
        private readonly Dictionary<(long, int, int), double> _phase = new Dictionary<(long, int, int), double>();
        private readonly double _omega;

        private StrokeFrame _frame;
        private double _dt;
        private double _previousSecond = double.NaN;
        private bool _toldOfMismatch;

        private TheatreSwimStroke(double omega) { _omega = omega; }

        /// <summary>Powered degrees of freedom drawn in the frame shown, a count for the status line.</summary>
        public int DrawnLastFrame { get; private set; }

        /// <summary>
        /// Reads a window's stroke file, or returns null and says once why there is no stroke: no
        /// file, or no omega to draw it at.
        /// </summary>
        /// <param name="window">The open window, whose directory holds the file and whose frames it follows.</param>
        /// <param name="omega"><c>Fluid.PowerPushRadiansPerSecond</c> of the run the window was filmed from.</param>
        public static TheatreSwimStroke Open(FilmWindowReader window, double omega)
        {
            string path = Path.Combine(window.Directory, FileName);

            if (!File.Exists(path))
            {
                Debug.Log("[Theatre] swim stroke: off, this window has no " + FileName);
                return null;
            }

            if (!(omega > 0d))
            {
                Debug.Log(
                    "[Theatre] swim stroke: off, the run's config carries no fluid.powerPushRadiansPerSecond " +
                    "above 0 to draw the stroke at");
                return null;
            }

            var stroke = new TheatreSwimStroke(omega);
            int bad = 0;

            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    stroke._frames.Add(Parse(line));
                }
                catch (Exception)
                {
                    // A line that does not parse is a frame with no stroke, so the file's order holds.
                    stroke._frames.Add(new StrokeFrame { Seconds = double.NaN });
                    bad++;
                }
            }

            int first = 0;

            if (stroke._frames.Count > 0)
            {
                foreach (List<Dof> body in stroke._frames[0].Bodies.Values) first += body.Count;
            }

            Debug.Log(
                "[Theatre] swim stroke: " + first + " powered DOF(s) in frame 1, omega " +
                omega.ToString("0.###", CultureInfo.InvariantCulture) + ", chain wave on");

            if (stroke._frames.Count != window.FrameCount)
            {
                Debug.LogWarning(
                    "[Theatre] swim stroke: the file holds " + stroke._frames.Count + " frame(s) and the window " +
                    window.FrameCount + "; a frame is drawn only where its second agrees with the pose frame's");
            }

            if (bad > 0) Debug.LogWarning("[Theatre] swim stroke: " + bad + " line(s) could not be read and draw no stroke");

            return stroke;
        }

        private static StrokeFrame Parse(string line)
        {
            JsonNode root = Json.Parse(line);
            var frame = new StrokeFrame { Seconds = root["t"].AsDouble() };

            foreach (JsonNode row in root["b"].Items())
            {
                long id = (long)row[0].AsDouble();

                if (!frame.Bodies.TryGetValue(id, out List<Dof> list))
                {
                    list = new List<Dof>(4);
                    frame.Bodies[id] = list;
                }

                list.Add(new Dof
                {
                    Link = row[1].AsInt(),
                    Index = row[2].AsInt(),
                    Power = row[3].AsFloat(),
                    Pivot = new Vector3(row[4].AsFloat(), row[5].AsFloat(), row[6].AsFloat()),
                    Axis = new Vector3(row[7].AsFloat(), row[8].AsFloat(), row[9].AsFloat()),
                });
            }

            return frame;
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>
        /// Starts a frame: picks its stroke line, and moves the clock the phases run on. A second
        /// that goes back resets every phase to 0; the first frame shown, and a frame shown again,
        /// advance none.
        /// </summary>
        public void BeginFrame(int frame, double second)
        {
            if (double.IsNaN(_previousSecond)) _dt = 0d;
            else if (second < _previousSecond - 1e-9)
            {
                _phase.Clear();
                _dt = 0d;
            }
            else _dt = second - _previousSecond;

            _previousSecond = second;
            DrawnLastFrame = 0;
            _frame = null;

            if (frame < 0 || frame >= _frames.Count) return;

            StrokeFrame candidate = _frames[frame];

            if (double.IsNaN(candidate.Seconds) || Math.Abs(candidate.Seconds - second) > SecondsTolerance)
            {
                if (!_toldOfMismatch && !double.IsNaN(candidate.Seconds))
                {
                    _toldOfMismatch = true;
                    Debug.LogWarning(
                        "[Theatre] swim stroke: frame " + frame + " is at " + candidate.Seconds.ToString("0.#####", CultureInfo.InvariantCulture) +
                        " s in the stroke file and " + second.ToString("0.#####", CultureInfo.InvariantCulture) +
                        " s in the pose stream; frames that disagree draw no stroke");
                }

                return;
            }

            _frame = candidate;
        }

        /// <summary>
        /// Swings a body's powered links about their pivots, after <see cref="RecordedPoses.Apply"/>
        /// has laid the pose into <paramref name="positions"/> and <paramref name="rotations"/> and
        /// before the view places them. Parents come before children (a part's parent is always
        /// below it), and a child's pivot and axis are carried by every swing already applied.
        /// </summary>
        /// <param name="id">The organism.</param>
        /// <param name="phenotype">The body the pose was laid on, at the size it was drawn.</param>
        /// <param name="root">The body's root place in the world (<see cref="RecordedPose.Root"/>).</param>
        /// <param name="positions">Each part's place relative to the root, changed in place.</param>
        /// <param name="rotations">Each part's attitude in the world, changed in place.</param>
        public void Apply(long id, Phenotype phenotype, Vector3 root, Vector3[] positions, Quaternion[] rotations)
        {
            if (_frame == null || !_frame.Bodies.TryGetValue(id, out List<Dof> logged)) return;

            int parts = phenotype.PartCount;
            var drawn = new List<Dof>(logged.Count);

            // Which degrees of freedom draw, read before anything moves: the link must carry the
            // coordinate, and a twist (an axis along the link) draws nothing.
            for (int i = 0; i < logged.Count; i++)
            {
                Dof d = logged[i];

                if (d.Link <= 0 || d.Link >= parts || !(d.Power > 0f)) continue;
                if (d.Index < 0 || d.Index >= phenotype.Parts[d.Link].JointType.DofCount()) continue;
                if (!IsFinite(d.Pivot) || !IsFinite(d.Axis) || d.Axis.sqrMagnitude < 1e-12f) continue;

                d.Pivot -= root;
                d.Axis.Normalize();

                if (IsTwist(phenotype.Parts[d.Link], rotations[d.Link], d.Axis)) continue;

                drawn.Add(d);
            }

            if (drawn.Count == 0) return;

            drawn.Sort((a, b) => a.Link != b.Link ? a.Link.CompareTo(b.Link) : a.Index.CompareTo(b.Index));

            double[] lag = ChainLags(phenotype, drawn);

            // Parent before child: ascending link, so every ancestor's swing is already in the
            // positions, rotations and carried pivots by the time a link's own is applied.
            var pivots = new Vector3[drawn.Count];
            var axes = new Vector3[drawn.Count];

            for (int e = 0; e < drawn.Count; e++)
            {
                pivots[e] = drawn[e].Pivot;
                axes[e] = drawn[e].Axis;
            }

            var under = new bool[parts];

            for (int e = 0; e < drawn.Count; e++)
            {
                Dof d = drawn[e];

                var key = (id, d.Link, d.Index);
                _phase.TryGetValue(key, out double phase);
                phase += SwimStrokeCurve.PhaseRate(d.Power, _omega) * _dt;
                _phase[key] = phase;

                double angle = SwimStrokeCurve.Amplitude(d.Power) * SwimStrokeCurve.Ease(phase - lag[e]);

                if (angle == 0d) continue;

                Vector3 pivot = pivots[e];
                Quaternion swing = Quaternion.AngleAxis((float)(angle * (180d / Math.PI)), axes[e].normalized);

                // This link and everything below it.
                for (int j = 0; j < parts; j++)
                {
                    under[j] = j == d.Link || (j > d.Link && under[phenotype.Parts[j].ParentIndex]);
                }

                for (int j = d.Link; j < parts; j++)
                {
                    if (!under[j]) continue;

                    positions[j] = pivot + swing * (positions[j] - pivot);
                    rotations[j] = swing * rotations[j];
                }

                // The later degrees of freedom on this subtree ride on the swing just made.
                for (int f = e + 1; f < drawn.Count; f++)
                {
                    if (!under[drawn[f].Link]) continue;

                    pivots[f] = pivot + swing * (pivots[f] - pivot);
                    axes[f] = swing * axes[f];
                }

                DrawnLastFrame++;
            }
        }

        // ---------------------------------------------------------------- the pieces

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary>Whether an axis lies within <see cref="TwistDegrees"/> of the link's long axis, either way along it.</summary>
        private static bool IsTwist(PhenotypePart part, Quaternion rotation, Vector3 axis)
        {
            Float3 h = part.HalfExtents;
            Vector3 local = h.X >= h.Y && h.X >= h.Z ? Vector3.right : h.Y >= h.Z ? Vector3.up : Vector3.forward;
            Vector3 along = rotation * local;

            return Mathf.Abs(Vector3.Dot(axis, along)) > Mathf.Cos(TwistDegrees * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The phase lag of each drawn degree of freedom, radians: the ruling 9 reading. A chain
        /// is a run of powered links each the child of the one before; where a powered link has
        /// several powered children the one with the longest powered run below it continues the
        /// chain and the others begin chains of their own.
        /// </summary>
        private static double[] ChainLags(Phenotype phenotype, List<Dof> drawn)
        {
            int parts = phenotype.PartCount;
            var lags = new double[drawn.Count];

            var firstDof = new int[parts];
            for (int j = 0; j < parts; j++) firstDof[j] = -1;

            for (int e = 0; e < drawn.Count; e++)
            {
                if (firstDof[drawn[e].Link] < 0) firstDof[drawn[e].Link] = e;
            }

            // The longest powered run at or below each link, found children first.
            var run = new int[parts];
            var main = new int[parts];
            for (int j = 0; j < parts; j++) main[j] = -1;

            for (int j = parts - 1; j >= 1; j--)
            {
                if (firstDof[j] < 0) continue;

                run[j] += 1;
                int parent = phenotype.Parts[j].ParentIndex;

                if (parent >= 1 && firstDof[parent] >= 0 &&
                    (main[parent] < 0 || run[j] > run[main[parent]] || (run[j] == run[main[parent]] && j < main[parent])))
                {
                    main[parent] = j;
                }

                if (parent >= 1 && firstDof[parent] >= 0) run[parent] = Math.Max(run[parent], run[j]);
            }

            // Each powered link's chain head, parents first.
            var head = new int[parts];
            var chains = new Dictionary<int, List<int>>();

            for (int j = 1; j < parts; j++)
            {
                if (firstDof[j] < 0) continue;

                int parent = phenotype.Parts[j].ParentIndex;
                head[j] = parent >= 1 && firstDof[parent] >= 0 && main[parent] == j ? head[parent] : j;

                if (!chains.TryGetValue(head[j], out List<int> members))
                {
                    members = new List<int>();
                    chains[head[j]] = members;
                }

                members.Add(j);
            }

            var lagOfLink = new double[parts];

            foreach (List<int> members in chains.Values)
            {
                double length = 0d;
                double thickness = 0d;

                for (int m = 0; m < members.Count; m++)
                {
                    Float3 h = phenotype.Parts[members[m]].HalfExtents;
                    length += 2d * Math.Max(h.X, Math.Max(h.Y, h.Z));
                    thickness += 2d * Math.Min(h.X, Math.Min(h.Y, h.Z));
                }

                thickness /= members.Count;

                double slender = thickness > 1e-9 ? length / thickness : WaveSlenderFloor;
                double w = Math.Min(1d, Math.Max(0d, (slender - WaveSlenderFloor) / WaveSlenderSpan));

                if (w <= 0d || length <= 1e-9) continue;

                // Distance along the chain from its first joint, joint to joint.
                double along = 0d;
                Vector3 previous = drawn[firstDof[members[0]]].Pivot;

                for (int m = 0; m < members.Count; m++)
                {
                    Vector3 here = drawn[firstDof[members[m]]].Pivot;
                    along += Vector3.Distance(previous, here);
                    previous = here;

                    lagOfLink[members[m]] = WaveHalfWave * w * Math.Min(1d, along / length);
                }
            }

            for (int e = 0; e < drawn.Count; e++) lags[e] = lagOfLink[drawn[e].Link];

            return lags;
        }
    }
}
