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
    /// <para>
    /// <b>A film window writes through it too</b> (<see cref="FilmWindow"/>), at its own path and
    /// on physics steps rather than on metabolic ones, through <see cref="WriteFrame"/>: one
    /// definition of what a frame holds, whoever asks for it.
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

        private PoseRecorder(PoseStreamWriter writer, float cadenceSeconds)
        {
            _writer = writer;
            _cadence = cadenceSeconds;
            _due = 0d;
        }

        /// <summary>
        /// Opens a stream at a path of the caller's choosing, for a writer that decides for itself
        /// when a frame is due and calls <see cref="WriteFrame"/>.
        /// </summary>
        /// <param name="path">The stream's file; its index is written beside it as <c>.idx</c>.</param>
        /// <param name="cadenceSeconds">The nominal interval the header records.</param>
        /// <param name="configHash">The recorded run's <c>configHash</c>.</param>
        public static PoseRecorder AtPath(string path, float cadenceSeconds, string configHash) =>
            new PoseRecorder(new PoseStreamWriter(path, cadenceSeconds, configHash), cadenceSeconds);

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

            WriteFrame(sim, t);

            // The next multiple of the cadence strictly after this instant, so a run whose steps
            // land on the cadence writes one frame per instant and no more.
            _due = Math.Floor(t / _cadence + 1e-9) * _cadence + _cadence;
        }

        /// <summary>
        /// Writes one frame of every living body that has a solver body, labelled with the second
        /// the caller names.
        /// </summary>
        /// <returns>The bodies the frame holds.</returns>
        public int WriteFrame(Simulation sim, double seconds)
        {
            if (_writer == null) throw new ObjectDisposedException(nameof(PoseRecorder));
            if (sim == null) throw new ArgumentNullException(nameof(sim));

            _writer.BeginFrame(seconds);

            IReadOnlyList<Organism> living = sim.World.Living;
            int written = 0;

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
                    GuildFlags(creature),
                    creature.SecondsOfReserve,
                    BreedFraction(creature, sim.World.Config),
                    (float)creature.Energy,
                    body.Dof,
                    body.Q);

                written++;
            }

            _writer.EndFrame();
            return written;
        }

        /// <summary>
        /// How near a body is to its next child, as World.IsSolvent asks it: the account over the
        /// gestation gate for a gestating body, the reserve over the reproduction gate for a lump
        /// breeder. NaN where the gate is not positive.
        /// </summary>
        private static float BreedFraction(Organism creature, RunConfig config)
        {
            double gate = creature.Gestates ? creature.GestationThreshold(config) : creature.ReproductionThreshold(config);
            double funds = creature.Gestates ? creature.GestationJoules : creature.Energy;
            return gate > 0d ? (float)(funds / gate) : float.NaN;
        }

        /// <summary>
        /// A body's guild flags, decided exactly as the sampler decides them for
        /// <c>positions.jsonl</c> (<c>Sampler.Write</c>): absorptive when any developed part is
        /// absorptive, jointed when the parts' joints have any degree of freedom between them, and
        /// photosynthetic by the organism's own cached flag.
        /// </summary>
        /// <remarks>
        /// Written out again here rather than shared because the sampler computes the three tests
        /// inline in a loop that feeds a dozen other columns, and the file that loop lives in is
        /// not this change's to restructure. <c>PoseStreamTests</c> holds the stream's flags equal
        /// to <c>positions.jsonl</c>'s on a stepped world, which is what keeps the two one rule.
        /// </remarks>
        public static int GuildFlags(Organism creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            bool absorptive = false;
            int dof = 0;

            foreach (PhenotypePart part in creature.Phenotype.Parts)
            {
                if (part.CellTypeId == CellTypeIds.Absorptive) absorptive = true;
                dof += part.JointType.DofCount();
            }

            return (absorptive ? PositionsRow.AbsorptiveBit : 0) |
                   (dof > 0 ? PositionsRow.JointedBit : 0) |
                   (creature.HasPhotosyntheticTissue ? PositionsRow.PhotosyntheticBit : 0);
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
