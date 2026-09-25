using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// The GPU engine (logbook/specs/gpu-port-spec.md) lives in Evosim.Farm.Gpu, a .NET 8 project
// that Unity never compiles, and it drives the mirror through the internal members below. The
// attribute names an assembly Unity does not have, which costs nothing there.
[assembly: InternalsVisibleTo("Evosim.Farm.Gpu")]

namespace Evosim.Dynamics
{
    /// <summary>
    /// Another way to take a block of physics steps over the same world: the GPU engine's seam.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The host's creatures stay canonical.</b> A backend reads every <see cref="Creature"/>
    /// it is handed, steps them somewhere else, and writes back every number the CPU step would
    /// have left in them, so that everything that reads a body between blocks (the farm's
    /// metabolic step, the digest, a checkpoint) reads exactly what it reads on the CPU. The
    /// world's clock, its step count, the contact census and the event list are advanced through
    /// the internal members of <see cref="DynamicsWorld"/> that exist for this and nothing else.
    /// </para>
    /// <para>
    /// <b>Nothing reads the arrays inside a block.</b> That is the farm's contract (the spec's
    /// section 1), and it is why a block can run whole on another device. A digest step is the
    /// exception: <see cref="DynamicsWorld.DigestDue"/> tells a backend which steps must be
    /// brought back to write their row.
    /// </para>
    /// </remarks>
    public interface IStepBackend : IDisposable
    {
        /// <summary>The engine's name for the manifest: "gpu".</summary>
        string Name { get; }

        /// <summary>
        /// How many physics steps the backend would take in one call. The farm asks for no more
        /// than the steps left to its next metabolic step.
        /// </summary>
        int PreferredBlockSteps { get; }

        /// <summary>Takes <paramref name="steps"/> physics steps and leaves the mirror as the CPU would.</summary>
        void StepBlock(DynamicsWorld world, int steps);

        /// <summary>Brings back anything the backend holds lazily. A no-op for a backend that holds nothing.</summary>
        void SyncMirror(DynamicsWorld world);
    }

    public sealed partial class DynamicsWorld
    {
        /// <summary>The backend that takes this world's steps, or null for the CPU solver.</summary>
        public IStepBackend Backend { get; private set; }

        /// <summary>
        /// Hands this world's steps to another engine. Null puts them back on the CPU, which is
        /// safe at any block boundary because the mirror is the state.
        /// </summary>
        public void UseBackend(IStepBackend backend)
        {
            Backend = backend;
        }

        /// <summary>
        /// Physics steps one <see cref="StepBlock"/> call would take, before the caller's own
        /// bound. 1 on the CPU, where the farm keeps stepping one step a call exactly as it
        /// always has.
        /// </summary>
        public int BlockSteps => Backend == null ? 1 : Math.Max(1, Backend.PreferredBlockSteps);

        /// <summary>
        /// Takes <paramref name="steps"/> physics steps: on the CPU the ordinary step that many
        /// times, and on a backend one block. The farm's one call site (the spec's section 1).
        /// </summary>
        public void StepBlock(int steps)
        {
            if (steps < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(steps), steps, "A block is at least one physics step.");
            }

            if (Backend == null)
            {
                for (int s = 0; s < steps; s++) StepOnCpu();
                return;
            }

            Backend.StepBlock(this, steps);
        }

        /// <summary>
        /// Everything a backend holds lazily, back into the creatures. Called before a
        /// checkpoint is written; a no-op on the CPU.
        /// </summary>
        public void SyncMirror() => Backend?.SyncMirror(this);

        // ------------------------------------------------------------ what a backend drives

        /// <summary>The clock and the step count a CPU step advances, advanced the same way.</summary>
        /// <remarks>
        /// <c>ElapsedSeconds += dt</c> once a step and never <c>n * dt</c> at once, because the
        /// water is sampled at the accumulated clock and the two differ in the last bit.
        /// </remarks>
        internal void BackendAdvanceClock()
        {
            ElapsedSeconds += Config.StepSeconds;
            Steps++;
        }

        /// <summary>Whether the digest will write a row at <paramref name="step"/>.</summary>
        internal bool DigestDue(long step)
        {
            if (_digest == null) return false;
            if (step == 1 || step % _digestEvery == 0) return true;
            return _digestDumpSteps != null && _digestDumpSteps.Contains(step);
        }

        /// <summary>Whether any digest is being written at all.</summary>
        internal bool DigestOn => _digest != null;

        /// <summary>The digest row for the step just taken, from the mirror.</summary>
        internal void BackendWriteDigestRow() => WriteDigestRow();

        /// <summary>
        /// One step's census, counted by the backend: the same five numbers
        /// <see cref="CloseContactStep"/> takes, added to the running totals and kept as the
        /// step's own.
        /// </summary>
        internal void BackendCensus(long pairs, long jointed, long held, long bodies, long bedOrGlass)
        {
            OverlapPairsThisStep = pairs;
            OverlapPairsJointedThisStep = jointed;
            OverlapPairsHeldThisStep = held;
            OverlapBodiesThisStep = bodies;
            BedOrGlassBodiesThisStep = bedOrGlass;

            OverlapPairs += pairs;
            OverlapPairsJointed += jointed;
            OverlapPairsHeld += held;
            OverlapBodies += bodies;
            BedOrGlassBodies += bedOrGlass;
        }

        /// <summary>The event list <see cref="Overlaps"/> returns, for a backend to refill.</summary>
        internal List<OverlapPair> BackendOverlapList => _overlaps;

        /// <summary>Adds wall time to one of the five <see cref="PhaseNames"/>.</summary>
        internal void BackendPhaseTicks(int phase, long ticks) => PhaseTicks[phase] += ticks;
    }
}
