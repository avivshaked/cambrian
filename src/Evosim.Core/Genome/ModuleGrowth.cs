namespace Evosim.Core
{
    /// <summary>
    /// Whether a node's count is fixed at development or is a bounded rule on the body's
    /// reserve — D106 item 2, <c>logbook/specs/module-gene-spec.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A gene and not a world rule</b>, so that selection decides which lineages are plants.
    /// A world rule that let every rich body add parts would impose plant-hood on everything,
    /// which is what D106 rejected.
    /// </para>
    /// <para>
    /// <b><see cref="Determinate"/> is every recorded body.</b> Development expands the node its
    /// <see cref="MorphNode.RecursiveLimit"/> times, D087's growth scales what is there, and a
    /// lost part is gone. Founders draw it, so a world starts as the record is and the gene
    /// enters by mutation alone.
    /// </para>
    /// <para>
    /// Serialised by name like every other enum here (<see cref="GenomeJson"/>'s remarks): a
    /// stored ordinal silently comes to mean something else the moment a member is inserted.
    /// </para>
    /// </remarks>
    public enum ModuleGrowth
    {
        /// <summary>The count is fixed when development expands the graph. Every founder.</summary>
        Determinate = 0,

        /// <summary>
        /// The count is bounded rather than fixed: the body adds a module of this node while its
        /// reserve stands above <see cref="RunConfig.ModuleAddReserveSeconds"/> of its own
        /// upkeep, up to <see cref="MorphNode.MaxModules"/>, and drops the last one after a long
        /// enough starvation. <c>World.ApplyModuleRule</c> is the whole of it.
        /// </summary>
        Indeterminate = 1,
    }
}
