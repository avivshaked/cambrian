namespace Evosim.Core
{
    /// <summary>
    /// The guard rails from DESIGN.md §4.2. Part of the config hash (§7).
    /// </summary>
    public sealed class DevelopmentLimits
    {
        /// <summary>Hard cap on parts in the developed phenotype. DESIGN.md §4.2 proposes 16.</summary>
        [Tunable("development")]
        public int MaxParts { get; set; } = 16;

        /// <summary>Hard cap on tree depth, root at depth 0. DESIGN.md §4.2 proposes 8.</summary>
        [Tunable("development")]
        public int MaxDepth { get; set; } = 8;

        /// <summary>
        /// Minimum box volume for a part, in m³. [K12 §2.3, p.7]: <i>"the volume of each body
        /// part must be larger than the specified threshold as extremely small body parts
        /// cause instability in the physical engine."</i>
        /// </summary>
        /// <remarks>
        /// Cumulative scale is multiplicative down a subtree, so a scale of 0.5 on a
        /// recursive edge shrinks parts geometrically. Without this floor, a long chain
        /// reaches sub-millimetre boxes and the solver misbehaves in ways that look like
        /// evolved behaviour.
        /// </remarks>
        [Tunable("development", Unit = "m3")]
        public float MinPartVolume { get; set; } = 1e-4f;

        /// <summary>
        /// Maximum part volume, m³. A part above it is pruned exactly as one below
        /// <see cref="MinPartVolume"/> is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Size is a multiplicative random walk, and only one of its tails was absorbed.</b>
        /// Mutation perturbs a half-extent by a Gaussian scaled to the half-extent itself, which
        /// is geometric Brownian motion on size: log-size diffuses without bound and has no
        /// stationary distribution. §4.5 relies on the lower tail hitting
        /// <see cref="MinPartVolume"/> — extinction by shrinking is what removes nodes at all, and
        /// what makes genome size settle. Nothing absorbed the upper tail, so bodies grew until
        /// half-extents reached 10<sup>18</sup> m and every derived quantity overflowed
        /// (logbook/0011).
        /// </para>
        /// <para>
        /// <b>The economics already forbid giants; this only keeps the arithmetic alive long
        /// enough to say so.</b> Income scales with surface area and upkeep with volume, so
        /// income/upkeep falls as 1/size and there is a largest body that can pay for itself
        /// (§5A.2b) — a creature this size starves within one step. What it cannot survive is
        /// <c>float</c>: a 10<sup>18</sup> m part has a volume of 10<sup>54</sup> m³, past
        /// <c>float.MaxValue</c>, so upkeep becomes infinite, energy becomes −∞, and §5A.2's audit
        /// is permanently NaN. So this is a bound on what the accounting can represent, not a
        /// judgement about what evolution may build, and it is set far outside anything the
        /// energy economy would tolerate: 10⁶ m³ is a 100 m cube in a world tens of metres wide.
        /// </para>
        /// <para>
        /// The consequence is <b>extinction by growing</b>, mirroring §4.5's extinction by
        /// shrinking. An oversized part is dropped with its subtree; a lineage whose root walks up
        /// into giantism develops into nothing and is stillborn (§5A.6). Both tails now end the
        /// same way, through one mechanism, with selection deciding which nodes get held away from
        /// either edge.
        /// </para>
        /// </remarks>
        [Tunable("development", Unit = "m3")]
        public float MaxPartVolume { get; set; } = 1e6f;

        /// <summary>
        /// Smallest half-extent on any one axis, m. A thinner part is built at this thickness
        /// rather than pruned.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Volume does not bound surface area, and the energy economy pays for area.</b> Income
        /// scales with lit area and upkeep with volume (§5A.2), so the cheapest way to earn is to
        /// be thin — and a box of half-extents (10⁻²⁵, 10⁻⁵, 10³⁰) has a volume of 8 m³ and a
        /// surface of 10²⁶ m². <see cref="MaxPartVolume"/> admits it. Selection found exactly
        /// this: within a few thousand births, shadow areas reached 10³⁷ m² in a world 400 m²
        /// wide (logbook/0011). It is §11.2's physics exploitation moved into the economy — a free
        /// lunch discovered by evolution rather than designed in, and the design's own arithmetic
        /// handed it over.
        /// </para>
        /// <para>
        /// <b>It is also, at the right scale, correct biology</b> — a leaf and a kelp blade are
        /// thin sheets for precisely this reason. What real tissue has and this model lacks is a
        /// cost that scales with area rather than volume, so thinness saturates instead of running
        /// away. That is a change to §5A.2's ledger and is treated as one. This limit does the
        /// separate, narrower job: keeping the geometry something a solver can integrate.
        /// </para>
        /// <para>
        /// <b>Clamped, not pruned, and the difference is load-bearing.</b> Flatness is a real and
        /// valuable trait — a flat box is the strongest paddle in the shape registry, 12× more
        /// directional than a sphere (§4.1) — so dropping thin parts would delete the best
        /// swimmers in the world. A part thinner than this is built at this thickness instead, and
        /// remains as flat as anything useful needs to be. Pruning stays the mechanism for volume
        /// alone, where §4.5 depends on it to remove nodes at all.
        /// </para>
        /// </remarks>
        [Tunable("development", Unit = "m")]
        public float MinPartHalfExtent { get; set; } = 0.01f;

        public static DevelopmentLimits Default => new DevelopmentLimits();

        public DevelopmentLimits Clone() => new DevelopmentLimits
        {
            MaxParts = MaxParts,
            MaxDepth = MaxDepth,
            MinPartVolume = MinPartVolume,
            MaxPartVolume = MaxPartVolume,
            MinPartHalfExtent = MinPartHalfExtent,
        };

        public override string ToString() =>
            System.FormattableString.Invariant($"maxParts={MaxParts} maxDepth={MaxDepth} ") +
            System.FormattableString.Invariant(
                $"volume={MinPartVolume:0.######}..{MaxPartVolume:0.} ") +
            System.FormattableString.Invariant($"thickness>={MinPartHalfExtent:0.###}");
    }
}
