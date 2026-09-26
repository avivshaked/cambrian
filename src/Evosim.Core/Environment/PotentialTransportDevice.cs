using System;

namespace Evosim.Core
{
    /// <summary>
    /// Somewhere other than the CPU that carries a <see cref="GridField"/>'s stock through a
    /// tank's potential-driven transport with the CPU's bits: the console farm's card
    /// (logbook/specs/transport-probe). Core cannot name the card, so the farm hands one to the
    /// grid (<see cref="GridField.TransportDevice"/>), and the grid keeps every decision the
    /// transport makes: the Courant refusal, the substep count and its refusal, the clock of each
    /// substep, and the instant's coefficients. The device is asked for the arithmetic alone.
    /// </summary>
    /// <remarks>
    /// A device that cannot give the CPU's bits for every value it is asked for declines the
    /// plan, because a run's trajectory is a function of its config and seed and never of where
    /// a pass ran. The farm's card writes every product as an explicitly rounded multiply, which
    /// PTX never contracts into a fused multiply-add (logbook/specs/fma-probe), and matched the
    /// CPU in every edge, face and cell of round 50's tank (logbook/specs/transport-probe).
    /// </remarks>
    public interface IPotentialTransportDevice
    {
        /// <summary>Takes the grid's fixed tables. False declines, and the CPU carries every step.</summary>
        bool Accept(PotentialTransportPlan plan);

        /// <summary>The stock, before a step's first sample.</summary>
        void Upload(double[] stock);

        /// <summary>
        /// The edge potentials at one instant and the faces assembled from them. The instant is
        /// <see cref="PotentialTransportPlan.InstantLength"/> coefficients: the streams' 24
        /// cosines, 24 sines and 24 eddy amplitudes and the 3 cells' amplitudes, pinned at the
        /// instant's clock.
        /// </summary>
        void Sample(double[] instant);

        /// <summary>
        /// The largest fraction of any live cell's stock the faces from the last
        /// <see cref="Sample"/> would move in <paramref name="seconds"/>, summed over its six faces
        /// and halved, as <c>GridField.LargestOutflowFraction</c> reads it.
        /// </summary>
        double LargestOutflowFraction(float seconds);

        /// <summary>One substep of <paramref name="seconds"/>: the upwind fluxes from the stock, then the three passes.</summary>
        void Apply(float seconds);

        /// <summary>The stock, after a step's last substep.</summary>
        void Download(double[] stock);
    }

    /// <summary>
    /// A tank grid's fixed transport tables, flattened for a device. Every array is in the
    /// indexing of the CPU's own loops, and the masks are the grid's own arrays, read and never
    /// written.
    /// </summary>
    public sealed class PotentialTransportPlan
    {
        /// <summary>A streams column's terms: Dx, Dz, Wall, Cos1 to Cos4, Sin1 to Sin4, then F11, F12, F21, F22, F31, F32, F41, F42.</summary>
        public const int ColumnTerms = 19;

        /// <summary>A depth's terms: AtFace as 1 or 0, SinPhi, CosPhi.</summary>
        public const int DepthTerms = 3;

        /// <summary>A bed column's terms: FloorY, Stretch, DepthSquared, SlopeX, SlopeZ, and the shore's fade there (1 with the fade off).</summary>
        public const int BedTerms = 6;

        /// <summary>The instant's coefficients (<see cref="IPotentialTransportDevice.Sample"/>).</summary>
        public const int InstantLength = 75;

        internal PotentialTransportPlan() { }

        public int Nx { get; internal set; }

        public int Ny { get; internal set; }

        public int Nz { get; internal set; }

        /// <summary>The cell's edge, widened from the grid's float as the CPU's loops widen it.</summary>
        public double CellMetres { get; internal set; }

        public float CellVolume { get; internal set; }

        public bool SplitColumns { get; internal set; }

        public int LayerStride { get; internal set; }

        public bool[] Live { get; internal set; }

        public int[] LowestLive { get; internal set; }

        /// <summary>Whether the edges read a shaped floor's columns (<see cref="PotentialTransportAxis.Bed"/>).</summary>
        public bool HasBed { get; internal set; }

        /// <summary>Whether a shaped floor fades the water toward the shore.</summary>
        public bool ShoreFade { get; internal set; }

        /// <summary>Whether the reefs fade the edges (<see cref="PotentialTransportAxis.ReefFade"/>).</summary>
        public bool HasReefs { get; internal set; }

        /// <summary>Whether the streams carry an overturning term.</summary>
        public bool Overturns { get; internal set; }

        /// <summary>The water's depth, widened from the current's float.</summary>
        public double DepthMetres { get; internal set; }

        /// <summary>The float every hoisted potential is multiplied by.</summary>
        public float Scale { get; internal set; }

        /// <summary>0 when every column shares a plane's depth terms, 1 when each has its own.</summary>
        public int DepthStep { get; internal set; }

        public PotentialTransportAxis X { get; internal set; }

        public PotentialTransportAxis Y { get; internal set; }

        public PotentialTransportAxis Z { get; internal set; }
    }

    /// <summary>
    /// One edge lattice's tables. The terms for column <c>c</c> at depth <c>d</c> sit at
    /// <c>d · <see cref="Stride"/> + c · <see cref="PotentialTransportPlan.DepthStep"/></c>.
    /// </summary>
    public sealed class PotentialTransportAxis
    {
        internal PotentialTransportAxis() { }

        /// <summary>The edges on this lattice.</summary>
        public int Length { get; internal set; }

        public int Stride { get; internal set; }

        public double[] Columns { get; internal set; }

        public double[] Depths { get; internal set; }

        /// <summary>Null without a shaped floor.</summary>
        public double[] Bed { get; internal set; }

        public bool[] Open { get; internal set; }

        /// <summary>Null without reefs.</summary>
        public float[] ReefFade { get; internal set; }
    }
}