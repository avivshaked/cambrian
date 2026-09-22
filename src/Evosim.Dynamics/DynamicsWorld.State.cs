using System.IO;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The solver world's own state — the clock, the step count and the instrument's running
    /// totals. Every body's state is the body's (<c>Creature.State.cs</c>).
    /// </summary>
    /// <remarks>
    /// The creature list is not written here. A restore rebuilds it from Core's population
    /// through the harness's ordinary birth path, in ascending id order, which is the order that
    /// makes a trajectory a function of the population rather than of its history
    /// (<see cref="AddInIdOrder"/>); writing the list here as well would be a second answer to
    /// the same question. The contact grid and the neighbour scratch hold nothing between steps.
    /// </remarks>
    public sealed partial class DynamicsWorld
    {
        public void WriteState(BinaryWriter w)
        {
            StateIo.Tag(w, "DYNW");
            w.Write(ElapsedSeconds);
            w.Write(Steps);

            w.Write(OverlapPairs);
            w.Write(OverlapPairsJointed);
            w.Write(OverlapPairsHeld);
            w.Write(OverlapBodies);
            w.Write(BedOrGlassBodies);
        }

        public void ReadState(BinaryReader r)
        {
            StateIo.Tag(r, "DYNW");
            ElapsedSeconds = r.ReadDouble();
            Steps = r.ReadInt64();

            OverlapPairs = r.ReadInt64();
            OverlapPairsJointed = r.ReadInt64();
            OverlapPairsHeld = r.ReadInt64();
            OverlapBodies = r.ReadInt64();
            BedOrGlassBodies = r.ReadInt64();
        }
    }
}
