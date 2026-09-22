using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// The run's end of the state stream: a frame of <c>poses.bin</c> whenever the cadence falls
    /// due (<c>logbook/specs/state-stream-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It hangs off the metabolic loop and not off the sample.</b> A report row is written
    /// every <c>EVOSIM_REPORT_EVERY</c> metabolic steps, which is a hundred simulated seconds in a
    /// round, and the stream exists because ten seconds is already too coarse. So the loop asks
    /// this at every metabolic step and it writes when it is due.
    /// </para>
    /// <para>
    /// <b>It moves no trajectory.</b> Nothing here writes to the world, the solver or the RNG; it
    /// reads the same two objects <c>Row.WritePoses</c> reads, one of which it gets through
    /// <c>Simulation.TryPose</c>. A run with the stream on and a run with it off produce the same
    /// <c>lineage.jsonl</c> to the byte, which is the check the build was accepted on.
    /// </para>
    /// </remarks>
    public sealed class PoseRecorder : IDisposable
    {
        private PoseStreamWriter _writer;
        private readonly double _cadence;
        private double _due;

        /// <summary>Opens the stream inside a run directory.</summary>
        public PoseRecorder(string runDirectory, float cadenceSeconds, string configHash)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentNullException(nameof(runDirectory));
            }

            _cadence = cadenceSeconds;
            _due = 0d;

            _writer = new PoseStreamWriter(
                Path.Combine(runDirectory, PoseStream.FileName), cadenceSeconds, configHash);
        }

        /// <summary>Complete frames written.</summary>
        public int FrameCount => _writer?.FrameCount ?? 0;

        /// <summary>The cadence the header carries.</summary>
        public double CadenceSeconds => _cadence;

        /// <summary>Writes a frame if one is due at the world's current second.</summary>
        public void Sample(Simulation sim)
        {
            if (_writer == null || sim == null) return;

            double t = sim.World.ElapsedSeconds;
            if (t + 1e-9 < _due) return;

            Write(sim, t);

            // The next multiple of the cadence strictly after this instant, so a run whose steps
            // land on the cadence writes one frame per instant and no more.
            _due = Math.Floor(t / _cadence + 1e-9) * _cadence + _cadence;
        }

        private void Write(Simulation sim, double t)
        {
            _writer.BeginFrame(t);

            IReadOnlyList<Organism> living = sim.World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];

                // A living organism whose solver body has gone is not drawn rather than drawn
                // somewhere, which is the rule positions.jsonl and poses.jsonl already follow.
                if (!sim.TryPose(creature.Id, out Creature body)) continue;

                _writer.Body(
                    creature.Id,
                    (float)body.BasePosition.X,
                    (float)body.BasePosition.Y,
                    (float)body.BasePosition.Z,
                    (float)body.BaseRotation.X,
                    (float)body.BaseRotation.Y,
                    (float)body.BaseRotation.Z,
                    (float)body.BaseRotation.W,
                    creature.BodyFraction,
                    body.Dof,
                    body.Q);
            }

            _writer.EndFrame();
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
