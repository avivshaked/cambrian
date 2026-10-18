using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// D176's instrument for D167's leaf smell: does a body move up the smell of living leaves? At
    /// every <see cref="CadenceSeconds"/>, for each living body in the water, the cosine between its
    /// root's velocity through the water and the leaf smell's gradient at its root. Written to
    /// <c>leafscent.jsonl</c>, one line a sample, beside <see cref="SteeringRecorder"/>'s
    /// <c>steering.jsonl</c>, whose shape it copies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sample is the sense's own.</b> The strength and the gradient are
    /// <see cref="ScentField.At"/> and <see cref="ScentField.GradientAt"/> on <c>World.LeafScent</c>,
    /// the calls <c>CreatureSenses.SampleLeaf</c> makes for each part. The direction form hands the
    /// brain that gradient turned into the part's own frame and scaled to the strength, so its
    /// direction is the world-frame gradient's, and the cosine against the world-frame velocity is
    /// the cosine against the direction the brain is given. The recorder reads the root, as the
    /// steering recorder does.
    /// </para>
    /// <para>
    /// <b>The control is built in, twice.</b> The bodies are split by whether their brain has any
    /// input on <see cref="SensorChannel.LeafScent"/> or <see cref="SensorChannel.LeafScentDirection"/>,
    /// and the grazers (attack and intake and no photosynthetic tissue, the lineage row's
    /// <c>atk</c>, <c>ink</c> and <c>pho</c>, read from the living body, whose flags follow a plan
    /// change) are split again the same way. A body that cannot smell the leaves moves up their
    /// gradient only by chance or by something that correlates with it, so the readers' mean
    /// cosine is read against the others' at the same sample, never against zero alone.
    /// </para>
    /// <para>
    /// <b>A recording and not state.</b> It reads the last physics step's bodies and the leaf field
    /// the world filled at its last metabolic step, and writes nothing any step reads, so it moves no
    /// hash and no trajectory. The leaf field is filled on the host on either engine and uploaded to
    /// the card each block, and the card's block unpacks every body's positions and velocities to the
    /// host before the metabolic step, so nothing is brought home first. A resumed run appends to
    /// the file it finds, so the rows after the checkpoint's second are written twice when a run is
    /// continued; a reader keeps the last row of each second.
    /// </para>
    /// <para>
    /// Each row: <c>t</c>; <c>read</c> and <c>ctl</c>, each <c>[n, mean cos, mean speed m/s]</c>
    /// over every body counted, by whether it reads the leaf smell; <c>gread</c> and <c>gctl</c>,
    /// the same over the grazers alone; <c>skip</c>, <c>[no motion, no gradient, out of the water
    /// or not finite]</c>, the bodies not counted; and <c>b</c>, one
    /// <c>[id, round(1000 cos), reads, grazer, speed mm/s, sense ‰, field, gradient]</c> per body
    /// counted, where sense is the strength the leaf sense reads, x / (x + k), in thousandths, field
    /// the field's own value at the root (lit m², cone-weighted) and gradient its magnitude in lit m²
    /// a metre, each to four significant figures.
    /// </para>
    /// </remarks>
    public sealed class LeafScentRecorder : IDisposable
    {
        public const string FileName = "leafscent.jsonl";

        /// <summary>
        /// Slower than this through the water, a body's heading is not read, m/s:
        /// <see cref="SteeringRecorder.MinimumSpeed"/>, so the two instruments count alike.
        /// </summary>
        public const double MinimumSpeed = SteeringRecorder.MinimumSpeed;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private JsonlWriter _writer;
        private readonly double _cadence;
        private double _due;

        public LeafScentRecorder(string runDirectory, double cadenceSeconds)
        {
            _cadence = cadenceSeconds;
            _writer = new JsonlWriter(Path.Combine(runDirectory, FileName), flushEachRow: true);
        }

        public double CadenceSeconds => _cadence;

        public int Rows { get; private set; }

        public void Sample(Simulation sim)
        {
            if (_writer == null || sim?.World.LeafScent == null) return;

            double t = sim.World.ElapsedSeconds;
            if (t + 1e-9 < _due) return;

            WriteRow(sim, t);
            _due = Math.Floor(t / _cadence + 1e-9) * _cadence + _cadence;
        }

        public void WriteRow(Simulation sim, double seconds)
        {
            ScentField leaf = sim.World.LeafScent;
            if (_writer == null || leaf == null) return;

            double half = Math.Max(1e-6, sim.World.Config.LeafScentHalfScaleSquareMetres);
            IReadOnlyList<Organism> living = sim.World.Living;

            var read = new Tally();
            var ctl = new Tally();
            var grazeRead = new Tally();
            var grazeCtl = new Tally();
            int still = 0, flat = 0, outside = 0;
            var bodies = new StringBuilder();

            for (int i = 0; i < living.Count; i++)
            {
                if (!sim.TryPose(living[i].Id, out Creature body)) continue;

                double px = body.Position[0], py = body.Position[1], pz = body.Position[2];
                double vx = body.RelativeVelocity[0], vy = body.RelativeVelocity[1], vz = body.RelativeVelocity[2];
                double speed = Math.Sqrt(vx * vx + vy * vy + vz * vz);

                if (py > 0 || double.IsNaN(px + py + pz + speed) || double.IsInfinity(px + py + pz + speed))
                {
                    outside++;
                    continue;
                }

                float x = (float)px, y = (float)py, z = (float)pz;
                Float3 g = leaf.GradientAt(x, y, z);
                double gx = g.X, gy = g.Y, gz = g.Z;
                double grad = Math.Sqrt(gx * gx + gy * gy + gz * gz);

                if (!(grad > 0) || double.IsInfinity(grad))
                {
                    flat++;
                    continue;
                }

                if (!(speed > MinimumSpeed))
                {
                    still++;
                    continue;
                }

                double cos = (vx * gx + vy * gy + vz * gz) / (speed * grad);
                int mask = body.Brain.SensorMask;
                bool reads = Brain.MaskReads(mask, SensorChannel.LeafScent) ||
                             Brain.MaskReads(mask, SensorChannel.LeafScentDirection);
                Organism o = living[i];
                bool grazer = o.HasAttack && o.HasIntake && !o.HasPhotosyntheticTissue;

                float s = leaf.At(x, y, z);
                double sense = s > 0f ? s / (s + half) : 0.0;

                (reads ? read : ctl).Add(cos, speed);
                if (grazer) (reads ? grazeRead : grazeCtl).Add(cos, speed);

                if (bodies.Length > 0) bodies.Append(',');
                bodies.Append('[').Append(o.Id.ToString(Inv)).Append(',')
                      .Append(((int)Math.Round(1000 * cos)).ToString(Inv)).Append(',')
                      .Append(reads ? '1' : '0').Append(',')
                      .Append(grazer ? '1' : '0').Append(',')
                      .Append(((int)Math.Round(1000 * speed)).ToString(Inv)).Append(',')
                      .Append(((int)Math.Round(1000 * sense)).ToString(Inv)).Append(',')
                      .Append(Sig4(s)).Append(',')
                      .Append(Sig4(grad)).Append(']');
            }

            var row = new StringBuilder();
            row.Append("{\"t\":").Append(seconds.ToString("R", Inv));
            row.Append(",\"read\":").Append(read);
            row.Append(",\"ctl\":").Append(ctl);
            row.Append(",\"gread\":").Append(grazeRead);
            row.Append(",\"gctl\":").Append(grazeCtl);
            row.Append(",\"skip\":[").Append(still.ToString(Inv)).Append(',')
               .Append(flat.ToString(Inv)).Append(',').Append(outside.ToString(Inv)).Append(']');
            row.Append(",\"b\":[").Append(bodies).Append("]}");
            _writer.Write(row.ToString());
            Rows++;
        }

        private static string Sig4(double v) => v.ToString("G4", Inv);

        // n, the sum of the cosines and the sum of the speeds; printed as SteeringRecorder's triple.
        private sealed class Tally
        {
            private int _n;
            private double _cos, _speed;

            public void Add(double cos, double speed)
            {
                _n++;
                _cos += cos;
                _speed += speed;
            }

            public override string ToString() =>
                _n == 0
                    ? "[0,null,null]"
                    : "[" + _n.ToString(Inv) + "," + (_cos / _n).ToString("0.#####", Inv) + "," +
                      (_speed / _n).ToString("0.######", Inv) + "]";
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
