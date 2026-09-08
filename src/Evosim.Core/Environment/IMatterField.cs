using System;

namespace Evosim.Core
{
    /// <summary>
    /// Where a body feeds, deposits or is priced — a position in the box and the patch it is in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two descriptions of one place, and each field reads the one it understands.</b> The
    /// cell field (<see cref="NutrientField"/>) has addressed water by depth and patch index
    /// since D061 and reads <see cref="Position"/>'s Y and <see cref="Patch"/> here, so a world
    /// running on cells is the arithmetic every recorded run was measured with. The vertex field
    /// (<see cref="VertexField"/>, D083) reads the whole position and ignores the index, because
    /// its water has no cells to index.
    /// </para>
    /// <para>
    /// A point made with <see cref="At(float, int)"/> carries no horizontal position at all —
    /// NaN in X and Z — and is only an honest description of a place to a field that does not
    /// need one. <see cref="VertexField"/> refuses it rather than putting the body at a
    /// coordinate nobody chose.
    /// </para>
    /// </remarks>
    public readonly struct FieldPoint
    {
        public readonly Float3 Position;
        public readonly int Patch;

        public FieldPoint(Float3 position, int patch)
        {
            Position = position;
            Patch = patch;
        }

        /// <summary>A depth and a patch and nothing else — the pre-D083 address.</summary>
        public static FieldPoint At(float heightY, int patch) =>
            new FieldPoint(new Float3(float.NaN, heightY, float.NaN), patch);

        public float HeightY => Position.Y;

        /// <summary>Whether the point says where in the horizontal plane it is.</summary>
        public bool HasHorizontal => !float.IsNaN(Position.X) && !float.IsNaN(Position.Z);
    }

    /// <summary>
    /// A stock of something dissolved or suspended in the water — detritus, or free matter —
    /// that bodies draw from and give back to, and that the water moves: the seam between
    /// <see cref="World"/> and the two representations of its water, D083.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Everything the world asks of its water, and nothing about how the water is stored.</b>
    /// <see cref="NutrientField"/> keeps one number per (layer, patch) cell; <see cref="VertexField"/>
    /// keeps a set of vertices each holding joules at a position. The world prices, demands, shares,
    /// takes and deposits through this interface and never learns which it has, so the economy's
    /// arithmetic — the frozen-availability allocation in particular (the Astra review's R1) — is
    /// written once and holds for both.
    /// </para>
    /// <para>
    /// <b>The per-cell reads stay, as averages.</b> <see cref="StockInLayer"/> and the
    /// depth-and-patch <see cref="DensityAt(float, int)"/> are what the run report and the
    /// clade scorer's inputs have always read; a vertex field answers them by binning its vertices
    /// into the same layers and patches, so a column of the report means the same thing across the
    /// change of representation.
    /// </para>
    /// </remarks>
    public interface IMatterField
    {
        float WorldArea { get; }
        float LayerMetres { get; }
        float SinkMetresPerSecond { get; }
        int LayerCount { get; }
        int PatchCount { get; }
        float PatchWidthMetres { get; }
        float LayerVolume { get; }

        /// <summary>Everything the field holds, J. Part of §5A.2's audit.</summary>
        double TotalJoules { get; }

        int LayerOf(float heightY);

        // ---- what a body does to the water

        void Deposit(FieldPoint at, float joules);

        /// <summary>
        /// A deposit by depth and patch — the address every test and tool has used since D061.
        /// A vertex field puts it at the patch's centre, and a grid field the patch's centre
        /// column. The world calls this in one place, D074's surface influx into a cell field,
        /// where each patch receives its own share of one deposit.
        /// </summary>
        void Deposit(float heightY, float joules, int patch);

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        void Deposit(float heightY, float joules);
        float DensityAt(FieldPoint at);
        float EdibleDensityAt(FieldPoint at);

        /// <summary>
        /// Removes up to <paramref name="wanted"/> J from one layer of one patch and returns what
        /// was taken. What D074's burial asks of a field.
        /// </summary>
        /// <remarks>
        /// <b>Added so that <c>World.BuryMatter</c> stops casting to a concrete field.</b> It read
        /// the floor through <see cref="NutrientField"/>'s own <c>Take(heightY, joules, patch)</c>
        /// and special-cased the vertex field beside it, which meant a third representation could
        /// not be added without a third branch there. Each field answers in its own units: the
        /// cell field takes from the one cell, the grid spreads the draw over the patch's columns
        /// in proportion to what each holds, and the vertex field over the vertices in that layer
        /// and patch. The world still buries vertices whole (<see cref="VertexField.BuryFloor"/>),
        /// so rounds 30 and 31 replay unchanged.
        /// </remarks>
        double TakeFromLayer(int layer, int patch, double wanted);

        void ClearDemand();
        void Demand(FieldPoint at, float joules);
        void FreezeAvailability();
        float FrozenEdibleDensityAt(FieldPoint at);
        float ShareAt(FieldPoint at);
        float Take(FieldPoint at, float joules);

        /// <summary>
        /// What a <see cref="Take(FieldPoint, float)"/> at this point could reach at most, J — the
        /// cell's own stock for a cell field, the edible mass inside the kernel for a vertex
        /// field. What conception's matter gate compares a price against.
        /// </summary>
        double ReachableStock(FieldPoint at);

        // ---- what the water does on its own

        void Settle(float seconds);
        void Remineralise(double seconds, float ratePerSecond);
        void Mix(float seconds, float diffusivity, float horizontalDiffusivity = 0f);
        void Advect(CurrentField current, double seconds, float dt, float patchWidthMetres);

        /// <summary>
        /// Housekeeping after the transport passes: merges what has drifted together and keeps the
        /// representation within its budget. A no-op for a field with nothing to merge.
        /// </summary>
        void Cull();

        // ---- reads by layer and patch, for reports and for the record's continuity

        double StockInLayer(int layer, int patch);

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        double StockInLayer(int layer);
        float DensityAt(float heightY, int patch);
        float EdibleDensityAt(float heightY, int patch);
    }
}
