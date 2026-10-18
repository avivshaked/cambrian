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
    /// propose-foraging.md's first brain instrument: does a body move up the scent? At every
    /// <see cref="CadenceSeconds"/>, for each living body in the water where the scent has a
    /// gradient, the cosine between its root's velocity through the water and the gradient at its
    /// root. Written to <c>steering.jsonl</c>, one line a sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The control is built in.</b> The bodies are split by whether their brain has any input on
    /// <see cref="SensorChannel.Scent"/>. A body that cannot smell moves up the gradient only by
    /// chance, or by something that correlates with it (corpses lie where bodies died, and bodies
    /// die where they lived), so the readers' mean cosine is read against the others' at the same
    /// sample, never against zero alone.
    /// </para>
    /// <para>
    /// <b>A recording and not state.</b> It reads the solver's last physics step and the world's
    /// scent field and writes nothing any step reads, so it moves no hash and no trajectory. A
    /// resumed run appends to the file it finds, so the rows after the checkpoint's second are
    /// written twice when a run is continued; a reader keeps the last row of each second.
    /// </para>
    /// <para>
    /// Each row: <c>t</c>; <c>read</c> and <c>ctl</c>, each <c>[n, mean cos, mean speed m/s]</c>
    /// over the bodies counted; <c>skip</c>, the bodies with no gradient or no motion; and
    /// <c>b</c>, one <c>[id, round(1000 cos), reads]</c> per body counted.
    /// </para>
    /// </remarks>
    public sealed class SteeringRecorder : IDisposable
    {
        public const string FileName = "steering.jsonl";

        /// <summary>The central difference's half-step, metres: half the scent's cell.</summary>
        public const float GradientStepMetres = 1f;

        /// <summary>Slower than this through the water, a body's heading is not read, m/s.</summary>
        public const double MinimumSpeed = 1e-3;

        /// <summary>A gradient smaller than this is no gradient, joules a metre.</summary>
        public const double MinimumGradient = 1e-4;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private StreamWriter _writer;
        private readonly double _cadence;
        private double _due;

        public SteeringRecorder(string runDirectory, double cadenceSeconds)
        {
            _cadence = cadenceSeconds;
            _writer = new StreamWriter(
                new FileStream(Path.Combine(runDirectory, FileName), FileMode.Append, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false)) { NewLine = "\n" };
        }

        public double CadenceSeconds => _cadence;

        public int Rows { get; private set; }

        public void Sample(Simulation sim)
        {
            if (_writer == null || sim?.World.Scent == null) return;

            double t = sim.World.ElapsedSeconds;
            if (t + 1e-9 < _due) return;

            WriteRow(sim, t);
            _due = Math.Floor(t / _cadence + 1e-9) * _cadence + _cadence;
        }

        public void WriteRow(Simulation sim, double seconds)
        {
            // The grid the bodies smelt, from the card when the card fills it (World.BringScentHome).
            sim.World.BringScentHome();
            ScentField scent = sim.World.Scent;
            IReadOnlyList<Organism> living = sim.World.Living;

            int nRead = 0, nCtl = 0, skipped = 0;
            double cosRead = 0, cosCtl = 0, speedRead = 0, speedCtl = 0;
            var bodies = new StringBuilder();
            float h = GradientStepMetres;

            for (int i = 0; i < living.Count; i++)
            {
                if (!sim.TryPose(living[i].Id, out Creature body)) continue;

                double px = body.Position[0], py = body.Position[1], pz = body.Position[2];
                double vx = body.RelativeVelocity[0], vy = body.RelativeVelocity[1], vz = body.RelativeVelocity[2];
                double speed = Math.Sqrt(vx * vx + vy * vy + vz * vz);

                float x = (float)px, y = (float)py, z = (float)pz;
                double gx = (scent.At(x + h, y, z) - scent.At(x - h, y, z)) / (2 * h);
                double gy = (scent.At(x, y + h, z) - scent.At(x, y - h, z)) / (2 * h);
                double gz = (scent.At(x, y, z + h) - scent.At(x, y, z - h)) / (2 * h);
                double grad = Math.Sqrt(gx * gx + gy * gy + gz * gz);

                if (py > 0 || !(speed > MinimumSpeed) || !(grad > MinimumGradient) ||
                    double.IsNaN(speed + grad))
                {
                    skipped++;
                    continue;
                }

                double cos = (vx * gx + vy * gy + vz * gz) / (speed * grad);
                bool reads = Brain.MaskReads(body.Brain.SensorMask, SensorChannel.Scent);

                if (reads) { nRead++; cosRead += cos; speedRead += speed; }
                else { nCtl++; cosCtl += cos; speedCtl += speed; }

                if (bodies.Length > 0) bodies.Append(',');
                bodies.Append('[').Append(living[i].Id.ToString(Inv)).Append(',')
                      .Append(((int)Math.Round(1000 * cos)).ToString(Inv)).Append(',')
                      .Append(reads ? '1' : '0').Append(']');
            }

            var row = new StringBuilder();
            row.Append("{\"t\":").Append(seconds.ToString("R", Inv));
            row.Append(",\"read\":").Append(Triple(nRead, cosRead, speedRead));
            row.Append(",\"ctl\":").Append(Triple(nCtl, cosCtl, speedCtl));
            row.Append(",\"skip\":").Append(skipped.ToString(Inv));
            row.Append(",\"b\":[").Append(bodies).Append("]}");
            _writer.WriteLine(row.ToString());
            _writer.Flush();
            Rows++;
        }

        private static string Triple(int n, double cos, double speed) =>
            n == 0
                ? "[0,null,null]"
                : "[" + n.ToString(Inv) + "," + (cos / n).ToString("0.#####", Inv) + "," +
                  (speed / n).ToString("0.######", Inv) + "]";

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}