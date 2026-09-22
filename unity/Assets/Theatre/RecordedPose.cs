using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// One body's attitude as the farm recorded it: the root link's place and orientation, and
    /// the joint coordinates in the solver's own degree-of-freedom order.
    /// </summary>
    /// <remarks>
    /// The row is <c>poses.jsonl</c>'s, written beside <c>positions.jsonl</c> at the same cadence
    /// by <c>Evosim.Farm</c>'s <c>Row.WritePoses</c> and joined on the organism id, exactly as the
    /// snapshot's genomes are. The numbers are rounded there — centimetres on the metres, four
    /// places on everything dimensionless — which is a hundredth of a degree on an angle and
    /// under anything a world view can show.
    /// </remarks>
    public sealed class RecordedPose
    {
        /// <summary>World place of the root link's own origin, metres.</summary>
        public Vector3 Root;

        /// <summary>World orientation of the root link.</summary>
        public Quaternion Attitude;

        /// <summary>Joint coordinates, one per degree of freedom, in the solver's order.</summary>
        public float[] Joint;
    }

    /// <summary>
    /// The reader of <c>poses.jsonl</c> and the forward kinematics that turns one of its rows
    /// back into a body — the second half of what a still frame needs, the snapshot's genomes
    /// being the first (<c>logbook/specs/snapshot-render-spec.md</c> §2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the chain is walked here rather than posed by the engine.</b> A reconstructed body
    /// has no <c>ArticulationBody</c> at all — <see cref="SnapshotWorld"/> builds transforms and
    /// renderers and nothing else — and <c>jointPosition</c> is a property of an articulation
    /// that PhysX reads at the next simulation step, which this mode never takes. So the pose is
    /// applied the way the solver itself computes it: <c>Evosim.Dynamics.Kinematics.Poses</c>,
    /// transcribed into Unity's types. Every term below has a line in that method, and the joint
    /// frames come from <c>PhenotypeBuilder.ConfigureJoint</c>, which is what both engines build
    /// their joints from.
    /// </para>
    /// <para>
    /// <b>The transcription is the risk, and it is a small one.</b> <c>QuatD</c> is Core's
    /// convention widened — the same Hamilton product and the same <c>q v q*</c> — and
    /// <c>UnityConvert</c> carries a <c>Quat</c> into a <c>Quaternion</c> component for
    /// component, so there is no axis or handedness change anywhere between the farm's numbers
    /// and these. What a reader can check, if a body ever looks wrong: a hinge is a rotation
    /// about the joint frame's X axis, and the joint frame of a hinge is a quarter turn about Y
    /// (<see cref="FrameOf"/>), so a hinge bends <i>across</i> the attachment axis rather than
    /// spinning about it.
    /// </para>
    /// <para>
    /// <b>A ball joint's three coordinates are a rotation vector, not three angles.</b> The
    /// solver carries a spherical joint's configuration as a quaternion and reports
    /// <c>Q</c> as its rotation vector — the axis times the angle, in (-pi, pi] — so the
    /// quaternion comes back exactly, by the inverse of that map. A one- or two-degree joint's
    /// coordinates are angles and compose as <c>Rx(q0) Ry(q1)</c>, which is the ordering PhysX's
    /// <c>xDrive</c>/<c>yDrive</c> are addressed in and the one
    /// <c>Kinematics.JointRotation</c> uses.
    /// </para>
    /// </remarks>
    public static class RecordedPoses
    {
        /// <summary>Whether the run recorded poses at all. Every run before 2026-09-21 did not.</summary>
        public static bool Has(string runDirectory) =>
            File.Exists(Path.Combine(runDirectory, "poses.jsonl"));

        /// <summary>
        /// Every body's pose at a second, by organism id, or null when the run wrote no poses or
        /// wrote none at that second.
        /// </summary>
        /// <remarks>
        /// Streamed and dropped on the first complete row at the second, rather than taken
        /// through <c>JsonlWriter.ReadRows</c>: a full run's poses are a multiple of its
        /// positions, and one row of them is ever wanted. The share mode is that method's, so a
        /// live run is readable under its writer, and a half-written last row is skipped by the
        /// same closing-brace test the positions reader uses.
        /// </remarks>
        public static Dictionary<long, RecordedPose> At(string runDirectory, double second)
        {
            string path = Path.Combine(runDirectory, "poses.jsonl");
            if (!File.Exists(path)) return null;

            string found = null;

            using (var stream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false)))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line.Length == 0 ||
                        !line.EndsWith("}", StringComparison.Ordinal)) continue;

                    double t = TimeOf(line);
                    if (double.IsNaN(t) || Math.Abs(t - second) > 1e-3) continue;

                    found = line;
                    break;
                }
            }

            if (found == null) return null;

            var poses = new Dictionary<long, RecordedPose>();
            JsonNode row = Json.Parse(found);
            JsonNode bodies = row["bodies"];

            for (int i = 0; i < bodies.Count; i++)
            {
                JsonNode body = bodies[i];

                JsonNode place = body["p"];
                JsonNode turn = body["r"];
                JsonNode joints = body["q"];

                var coordinates = new float[joints.Count];
                for (int d = 0; d < joints.Count; d++) coordinates[d] = (float)joints[d].AsDouble();

                var pose = new RecordedPose
                {
                    Root = new Vector3(
                        (float)place[0].AsDouble(),
                        (float)place[1].AsDouble(),
                        (float)place[2].AsDouble()),

                    // The writer's order is (x, y, z, w) and Unity's constructor's is the same.
                    Attitude = new Quaternion(
                        (float)turn[0].AsDouble(),
                        (float)turn[1].AsDouble(),
                        (float)turn[2].AsDouble(),
                        (float)turn[3].AsDouble()).normalized,

                    Joint = coordinates,
                };

                poses[(long)body["id"].AsDouble()] = pose;
            }

            return poses;
        }

        /// <summary>
        /// Every part's place and orientation under a recorded pose, in the body's own frame with
        /// the root link at the origin. False when the pose does not fit the developed body.
        /// </summary>
        /// <remarks>
        /// The root at the origin rather than at its recorded place, so that the caller hangs the
        /// assembled body from one transform — and so that a build which is going to be refused
        /// for a coordinate count is refused before anything is placed.
        /// </remarks>
        public static bool Apply(
            Phenotype phenotype, RecordedPose pose,
            Vector3[] positions, Quaternion[] rotations, out string refusal)
        {
            refusal = null;

            int parts = phenotype.PartCount;
            int dof = 0;
            var start = new int[parts];

            for (int i = 0; i < parts; i++)
            {
                int n = phenotype.Parts[i].IsRoot ? 0 : phenotype.Parts[i].JointType.DofCount();
                start[i] = n > 0 ? dof : -1;
                dof += n;
            }

            // The genome develops into a different number of degrees of freedom than the body
            // that was recorded had — a build difference, or a row joined to the wrong creature.
            // Either way the coordinates cannot be laid on this body, and drawing it upright and
            // saying so on the label is the honest answer.
            if (pose.Joint.Length != dof)
            {
                refusal =
                    "the row carries " + pose.Joint.Length + " joint coordinate(s) and this " +
                    "build develops the genome into " + dof;
                return false;
            }

            positions[0] = Vector3.zero;
            rotations[0] = pose.Attitude;

            for (int i = 1; i < parts; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                int p = part.ParentIndex;

                if (p < 0 || p >= i)
                {
                    refusal = "part " + i + "'s parent is " + p + ", which is not before it";
                    return false;
                }

                PhenotypePart parent = phenotype.Parts[p];

                // One physical joint frame in both bodies' axes, as ConfigureJoint writes it:
                // the child's is `frame` and the parent's is `relative * frame`.
                Quaternion frame = FrameOf(part.JointType);

                Quaternion rest =
                    Quaternion.Inverse(parent.Rotation.ToQuaternion()) *
                    part.Rotation.ToQuaternion() * frame;

                Quaternion joint = JointRotation(part.JointType, pose.Joint, start[i]);

                Quaternion rotation =
                    (rotations[p] * rest * joint * Quaternion.Inverse(frame)).normalized;

                rotations[i] = rotation;

                // The two anchors are one point, so the child hangs where the parent's anchor is
                // less where its own anchor sits in it — Kinematics.Poses' last line.
                positions[i] =
                    positions[p] +
                    rotations[p] * part.ParentAnchorLocal.ToVector3() -
                    rotation * part.ChildAnchorLocal.ToVector3();
            }

            return true;
        }

        /// <summary>
        /// <c>PhenotypeBuilder.JointFrameRotation</c>, which is private to <c>Evosim.Sim</c>.
        /// </summary>
        /// <remarks>
        /// Duplicated rather than reached for, under the rule <see cref="SoloCreature"/> and
        /// <see cref="SnapshotWorld"/>'s visual plan are written under: widening a seam in the
        /// simulation's source for the theatre's convenience is the thing not to do. The
        /// dynamics' own <c>Creature.FrameOf</c> is a third copy of the same two lines, and all
        /// three say the same thing — a hinge bends across the attachment axis, everything else
        /// twists about it.
        /// </remarks>
        private static Quaternion FrameOf(JointType type) =>
            type == JointType.Hinge ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

        /// <summary>
        /// The joint's relative rotation in its own frame, from its coordinates.
        /// </summary>
        private static Quaternion JointRotation(JointType type, float[] q, int at)
        {
            int n = type.DofCount();
            if (n == 0 || at < 0) return Quaternion.identity;

            if (n == 3)
            {
                // A rotation vector, which is how the solver reports a ball joint: the axis
                // times the angle. The inverse of QuatD.RotationVector, exactly.
                var v = new Vector3(q[at], q[at + 1], q[at + 2]);
                float angle = v.magnitude;

                return angle < 1e-9f ? Quaternion.identity : AxisAngle(v / angle, angle);
            }

            Quaternion r = AxisAngle(Axis(0), q[at]);
            for (int d = 1; d < n; d++) r = r * AxisAngle(Axis(d), q[at + d]);

            return r;
        }

        /// <summary>
        /// A rotation about a unit axis, built from the half angle rather than through
        /// <c>Quaternion.AngleAxis</c>, whose argument is degrees.
        /// </summary>
        private static Quaternion AxisAngle(Vector3 axis, float radians)
        {
            float half = 0.5f * radians;
            float s = Mathf.Sin(half);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(half));
        }

        private static Vector3 Axis(int d) =>
            d == 0 ? Vector3.right : d == 1 ? Vector3.up : Vector3.forward;

        /// <summary>A row's <c>t</c> without parsing the rest of it, or NaN.</summary>
        private static double TimeOf(string line)
        {
            const string opening = "{\"t\":";

            if (!line.StartsWith(opening, StringComparison.Ordinal)) return double.NaN;

            int end = line.IndexOf(',', opening.Length);
            if (end < 0) return double.NaN;

            return double.TryParse(
                line.Substring(opening.Length, end - opening.Length),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
                ? t
                : double.NaN;
        }
    }
}
