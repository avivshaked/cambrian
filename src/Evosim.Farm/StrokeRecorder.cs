using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// A film window's stroke log: for each frame it writes to <c>film.poses.bin</c>, one line of
    /// <c>film.stroke.jsonl</c> naming every powered degree of freedom, the power it has, and where
    /// its joint is and which way it turns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the theatre needs it.</b> The solver never swings a powered limb (D135): a joint's
    /// power pushes the body through the fluid, and the stroke itself is drawn by the theatre from
    /// the power. The pose stream carries the joint angles and not the power, and the farm's other
    /// records do not log it, so a window that steps the world itself writes it from its own
    /// stepping.
    /// </para>
    /// <para>
    /// <b>One line per frame, in the same second.</b> A line is <c>{"t":24000.04167,"b":[[id, link,
    /// dof, s, px, py, pz, ax, ay, az], ...]}</c>, with an entry for each degree of freedom whose
    /// power is above zero and none for the rest, over the same bodies
    /// <see cref="PoseRecorder.WriteFrame"/> writes (a living organism with a solver body). A
    /// frame in which nothing is powered is still a line, with an empty list, so a reader can lay
    /// the two files side by side frame for frame.
    /// </para>
    /// <para>
    /// <b>It moves no trajectory.</b> It reads <see cref="Creature.Drive"/>'s stroke and the
    /// solver's last poses and writes to a file of its own. Nothing here touches the world, the
    /// solver, the drive or any RNG.
    /// </para>
    /// </remarks>
    public sealed class StrokeRecorder : IDisposable
    {
        private readonly JsonlWriter _writer;
        private readonly StringBuilder _row = new StringBuilder(1 << 12);

        /// <summary>Opens the log at a path of the caller's choosing.</summary>
        public StrokeRecorder(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            _writer = new JsonlWriter(path, flushEachRow: false);
        }

        /// <summary>Lines written, one for each frame.</summary>
        public long FrameCount => _writer.RowCount;

        /// <summary>
        /// Writes the line for one frame, labelled with the second the caller names, which is the
        /// second the caller gave <see cref="PoseRecorder.WriteFrame"/> for the same frame.
        /// </summary>
        /// <returns>The degrees of freedom the line holds.</returns>
        public int WriteFrame(Simulation sim, double seconds)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));

            _row.Clear();
            _row.Append("{\"t\":").Append(seconds.ToString("R", CultureInfo.InvariantCulture)).Append(",\"b\":[");

            IReadOnlyList<Organism> living = sim.World.Living;
            int entries = 0;

            for (int i = 0; i < living.Count; i++)
            {
                // The same rule as the pose stream: a living organism whose solver body has gone is
                // not drawn rather than drawn somewhere.
                if (!sim.TryPose(living[i].Id, out Creature body)) continue;

                entries += AppendBody(_row, living[i].Id, body);
            }

            _row.Append("]}");
            _writer.Write(_row.ToString());
            return entries;
        }

        /// <summary>
        /// Appends an entry to <paramref name="row"/> for each powered degree of freedom of one
        /// body, each after a comma where the row already holds one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The power is read where the fluid reads it.</b> A link's degrees of freedom start at
        /// <see cref="Creature.DofStart"/> in <see cref="EffectorDrive.Stroke"/>, one slot each in
        /// axis order, which is the index <c>Fluid.Apply</c> and <c>CreatureSenses</c> use. The
        /// link is the solver's, and the solver's link order is the phenotype's part order.
        /// </para>
        /// <para>
        /// <b>The pivot and the axis are the solver's own.</b> The pivot is the joint's anchor on the
        /// parent, taken to world space through the parent's current pose, which is the line
        /// <c>Kinematics.Poses</c> places the child from (the parent's origin plus the rotated
        /// <see cref="Creature.ParentAnchor"/>). The axis is the column of the motion subspace
        /// <c>Kinematics.Velocities</c> leaves in <see cref="Creature.Sang"/>, in world axes, which
        /// is the axis the joint's rotation is about, so a twist degree of freedom is written with
        /// its axis like the others and the theatre decides what to draw. Both are refreshed at the
        /// end of every physics step (and mirrored back from the graphics card), so they stand where
        /// the pose stream's root and angles put the body.
        /// </para>
        /// <para>
        /// An entry with a number that is not finite is left out rather than written: a line that
        /// carried <c>NaN</c> would not parse as JSON, and a body in that state is about to be
        /// killed as diverged.
        /// </para>
        /// </remarks>
        /// <returns>The entries appended.</returns>
        public static int AppendBody(StringBuilder row, long id, Creature body)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (body.Drive == null) return 0;

            double[] stroke = body.Drive.Stroke;
            int appended = 0;

            for (int link = 1; link < body.Links; link++)
            {
                int count = body.DofCount[link];
                int start = body.DofStart[link];
                int parent = body.Parent[link];
                if (count == 0 || start < 0 || parent < 0) continue;

                bool havePivot = false;
                Vec3 pivot = Vec3.Zero;

                for (int d = 0; d < count; d++)
                {
                    int at = start + d;
                    if (at >= stroke.Length) break;

                    double s = stroke[at];
                    if (!(s > 0d)) continue;

                    if (!havePivot)
                    {
                        pivot = Vec3.Read(body.Position, 3 * parent) +
                                Mat3.Read(body.RotationMatrix, 9 * parent) * Vec3.Read(body.ParentAnchor, 3 * link);
                        havePivot = true;
                    }

                    Vec3 axis = Vec3.Read(body.Sang, 9 * link + 3 * d);
                    double length = axis.Magnitude;
                    if (!(length > 0d)) continue;
                    axis = axis * (1.0 / length);

                    if (!IsFinite(s) || !IsFinite(pivot) || !IsFinite(axis)) continue;

                    if (row[row.Length - 1] != '[') row.Append(',');

                    row.Append('[').Append(id.ToString(CultureInfo.InvariantCulture))
                       .Append(',').Append(link.ToString(CultureInfo.InvariantCulture))
                       .Append(',').Append(d.ToString(CultureInfo.InvariantCulture));
                    Number(row, s);
                    Number(row, pivot.X);
                    Number(row, pivot.Y);
                    Number(row, pivot.Z);
                    Number(row, axis.X);
                    Number(row, axis.Y);
                    Number(row, axis.Z);
                    row.Append(']');

                    appended++;
                }
            }

            return appended;
        }

        /// <summary>Pushes the buffered lines to the file.</summary>
        public void Flush() => _writer.Flush();

        public void Dispose() => _writer.Dispose();

        /// <summary>A float's shortest round-trip form, as the pose stream carries its numbers.</summary>
        private static void Number(StringBuilder row, double value)
        {
            row.Append(',').Append(((float)value).ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(Vec3 v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
    }
}
