using System.Collections.Generic;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// <see cref="IHarnessReadings"/> over the running simulation — what the footer and the
    /// manifest's ending block take off the loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A view and not a copy.</b> Every member here forwards a live reading, so the footer, the
    /// error manifest and the ending block cannot be looking at three different instants of the
    /// same run.
    /// </para>
    /// <para>
    /// <b>Four members carry a different census under the interface's old name</b>, because the
    /// interface was written in package I against the farm's vocabulary and the numbers moved in
    /// package E. <see cref="ContactPairs"/>, <see cref="ContactPairsJointed"/>,
    /// <see cref="ContactPairsPersistent"/> and <see cref="ContactBodies"/> are the overlap
    /// instrument's four, and <see cref="FloorContactPairs"/> is the bodies against the bed or the
    /// glass. The table renames them; the footer's own sentence still says "contact pairs per
    /// physics step" and is left alone, because three scripts match that line and the whole point
    /// of the port is that a report still parses. It is said here and in the report rather than
    /// quietly changed under the reader.
    /// </para>
    /// </remarks>
    public sealed class Readings : IHarnessReadings
    {
        private readonly Simulation _sim;

        public Readings(Simulation sim) => _sim = sim;

        public long PhysicsSteps => _sim.Steps;
        public double ElapsedSeconds => _sim.World.ElapsedSeconds;
        public long Births => _sim.World.Births;
        public int Alive => _sim.World.Living.Count;
        public long Diverged => _sim.World.Diverged;
        public double MaxSpeed => _sim.MaxSpeed;

        public long DragImpulsesLimited => _sim.DragImpulsesLimited;
        public long DriveImpulsesLimited => _sim.DriveImpulsesLimited;

        public double MatterInfluxedTotal => _sim.World.MatterInfluxedTotal;
        public double MatterBuriedTotal => _sim.World.MatterBuriedTotal;

        public bool HasSharedVolume => _sim.Volume != null;
        public long Wraps => _sim.Wraps;
        public long Crowded => _sim.Crowded;

        public long ContactPairs => _sim.Dynamics.OverlapPairs;
        public long ContactPairsJointed => _sim.Dynamics.OverlapPairsJointed;
        public long ContactPairsPersistent => _sim.Dynamics.OverlapPairsHeld;
        public long ContactBodies => _sim.Dynamics.OverlapBodies;

        public long FloorContactPairs => _sim.Dynamics.BedOrGlassBodies;

        public bool HasFloor => _sim.Floor != null;
        public bool FloorHasRelief => _sim.Floor != null && _sim.Floor.HasRelief;

        /// <summary>
        /// The lowest rock in the world, m.
        /// </summary>
        /// <remarks>
        /// <b>The map's own extreme over the disc, not a mesh's deepest vertex.</b> The farm
        /// builds a <c>MeshCollider</c> over a half-metre lattice that runs a seam margin past the
        /// rim, and reports that mesh's lowest vertex — a little deeper than the disc, because a
        /// tilt keeps falling outside it. This engine has no mesh: <c>ContactBed</c> evaluates
        /// <c>BedShape</c> analytically at the body's own column, so the deepest rock a body can
        /// reach is the deepest point of the disc. The two differ by however far the tilt falls
        /// over the margin, and the footer's number is therefore a little shallower than the same
        /// world's under the other engine.
        /// </remarks>
        public double FloorLowestTopY =>
            _sim.World.Bed != null && _sim.World.Bed.HasRelief
                ? -(double)_sim.Config.WorldDepthMetres + _sim.World.Bed.LowestMetres
                : -(double)_sim.Config.WorldDepthMetres;

        public double MaxJointMassRatio => _sim.MaxJointMassRatio;
        public long BodiesOverMassRatio10 => _sim.BodiesOverMassRatio10;

        public long WallPhysicsMs => _sim.WallPhysicsMs;
        public long WallWorldMs => _sim.WallWorldMs;
        public long WallHarnessMs => _sim.WallHarnessMs;

        public IReadOnlyList<string> HarnessPhases => Simulation.HarnessPhases;
        public IReadOnlyList<long> HarnessPhaseMs => _sim.HarnessPhaseMs();
        public long HarnessBodySteps => _sim.HarnessBodySteps;
        public double HarnessMicrosecondsPerBodyStep => _sim.HarnessMicrosecondsPerBodyStep;

        /// <summary>
        /// The fluid phase's four, which this engine does not have separately — see
        /// <see cref="Simulation.FluidMicrosecondsPerLinkStep"/>. All four read 0 and the split
        /// line therefore prints four zeros, which is a fact about the engine rather than about
        /// the drag.
        /// </summary>
        public long WallFluidGatherMs => 0L;

        /// <inheritdoc cref="WallFluidGatherMs"/>
        public long WallFluidWaterMs => 0L;

        /// <inheritdoc cref="WallFluidGatherMs"/>
        public long WallFluidComputeMs => 0L;

        /// <inheritdoc cref="WallFluidGatherMs"/>
        public long WallFluidApplyMs => 0L;

        public long FluidLinkSteps => _sim.FluidLinkSteps;
        public double FluidMicrosecondsPerLinkStep => _sim.FluidMicrosecondsPerLinkStep;
    }
}
