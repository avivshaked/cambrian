namespace Evosim.Core
{
    /// <summary>
    /// What a neuron input may reference. The restriction is load-bearing — DESIGN.md §4.3.
    /// </summary>
    /// <remarks>
    /// Neurons live inside morph nodes, so recursion duplicates a segment's neurons along
    /// with the segment, producing a chain of identical local controllers — structurally a
    /// central pattern generator. That only holds if every input reference is expressible
    /// relative to the copy. An input naming an absolute part index would break under
    /// duplication, which is why no such kind exists here.
    /// </remarks>
    public enum NeuronInputKind
    {
        /// <summary>A fixed value. Uses <see cref="NeuronInput.Constant"/>.</summary>
        Constant = 0,

        /// <summary>A sensor on the part owning this neuron (DESIGN.md §4.4).</summary>
        Sensor = 1,

        /// <summary>Another neuron in the same morph node.</summary>
        SameNode = 2,

        /// <summary>A neuron in the parent node's copy. Reads zero at the root.</summary>
        ParentNode = 3,

        /// <summary>A neuron in a child node's copy. Reads zero at a leaf.</summary>
        ChildNode = 4,

        /// <summary>A neuron in the global brain — not owned by any part.</summary>
        GlobalBrain = 5,
    }

    /// <summary>
    /// Sensor channels, normalised to approximately [-1, 1] — DESIGN.md §4.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [K12 §2.2, p.4] used joint-angle sensors alone. The rest are staged:
    /// <see cref="Damage"/> arrives with predation (Milestone 3) and <see cref="Photo"/> with
    /// the full brain graph (Milestone 6). They are declared now so the genome format does
    /// not change when they land.
    /// </para>
    /// <para>
    /// <b>Which channels a part may read is not restricted by cell type.</b> The joint channels
    /// read zero on anything rigid, which is the same thing a restriction would achieve without
    /// making the legality of a genome depend on a mutation elsewhere in it. What limits
    /// perception is not permission but cost: a sensor is only useful through a neuron, and
    /// neurons are billed per step (§5A.2). A creature that senses everything everywhere starves.
    /// </para>
    /// </remarks>
    public enum SensorChannel
    {
        /// <summary>Joint angle for one DOF. Uses <see cref="NeuronInput.Index"/> as the DOF.</summary>
        JointAngle = 0,

        /// <summary>Joint angular velocity for one DOF.</summary>
        JointAngularVelocity = 1,

        /// <summary>
        /// Something solid is touching this part — terrain, or another creature.
        /// </summary>
        /// <remarks>
        /// Originally scoped to terrain and Milestone 5. §5A made it aquatic too: contact is how
        /// a consumer cell finds tissue to bite (§5A.3), so the channel that reports it is
        /// needed as soon as predation is, which is Milestone 3.
        /// </remarks>
        Contact = 2,

        /// <summary>Part orientation against world up.</summary>
        OrientationUp = 3,

        /// <summary>Photoreceptor triple. Milestone 6.</summary>
        Photo = 4,

        /// <summary>
        /// Energy taken out of this part by something else, over the last step — DESIGN.md §4.4.
        /// Normalised against the part's own stored energy, so it reads as a fraction lost
        /// rather than in joules. <see cref="NeuronInput.Index"/> is ignored.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Available on <i>every</i> part, not only links. Once creatures can eat each other,
        /// being bitten is the most consequential thing that happens to a body cell, and a
        /// cell that cannot report it leaves the creature with no way to distinguish a
        /// successful photosynthetic pose from being slowly eaten in one.
        /// </para>
        /// <para>
        /// <b>No signal relay is needed for it to reach the rest of the creature.</b> A neuron
        /// on the bitten part is read by <see cref="NeuronInputKind.ParentNode"/> and
        /// <see cref="NeuronInputKind.ChildNode"/> inputs on its neighbours, so damage
        /// propagates one node per step through machinery that already exists. That latency is
        /// a feature: it is what a conduction delay looks like, it is bounded by body length,
        /// and it does not require an input kind that names an absolute part — which would
        /// break under recursion for the reasons in <see cref="NeuronInputKind"/>.
        /// </para>
        /// </remarks>
        Damage = 5,

        /// <summary>
        /// Nutrient concentration in the water at this part — smell. Milestone 4.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Without this, §5A.1's absorptive cell is a lottery ticket.</b> That section says the
        /// type "rewards being <i>where</i> food is" — but a creature that cannot smell has no
        /// way to get where food is except by drifting into it, so no amount of control or
        /// intelligence improves its intake. The strategy the design describes was not reachable.
        /// </para>
        /// <para>
        /// Chemotaxis predates vision by billions of years and bacteria manage it, which is a
        /// reasonable indication of how basic a capability this is and how odd its absence was.
        /// </para>
        /// </remarks>
        Chemical = 6,

        /// <summary>
        /// The creature's own energy reserve, as seconds of life remaining at its current burn
        /// rate — DESIGN.md §5A.2. Milestone 4.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The level, not a hunger flag.</b> A flag needs a threshold, and picking one is us
        /// deciding when a creature ought to feel hungry — an unmeasured constant (§5A.10)
        /// baked somewhere no run could vary it. A neuron builds whatever thresholds it wants
        /// from a weight and a bias, and can hold several at once for different behaviours.
        /// Hunger is derivable from the level; the level is not recoverable from hunger.
        /// </para>
        /// <para>
        /// <b>Normalised against the creature's own burn rate</b>, so it reads as a duration
        /// rather than a quantity. Raw joules would be meaningless across bodies of different
        /// sizes, and normalising against the reproduction threshold instead would make a
        /// brood-size mutation silently rescale how the creature perceives itself. Time to
        /// starvation is the quantity every decision actually turns on, and it needs no
        /// reference number that anyone has to guess.
        /// </para>
        /// <para>
        /// Whole-creature rather than per-part, since §5A.6 kills the creature at zero and not
        /// the part. <see cref="NeuronInput.Index"/> is ignored.
        /// </para>
        /// </remarks>
        Energy = 7,

        /// <summary>
        /// Water velocity relative to this part, along the part's own axes — a lateral line.
        /// Milestone 4. <see cref="NeuronInput.Index"/> selects the axis.
        /// </summary>
        /// <remarks>
        /// Currents exist in §5A.4, so holding station against one is a real task, and something
        /// large moving nearby disturbs the water before it arrives. That makes this the only
        /// channel offering warning of a thing that has not touched you yet and that you cannot
        /// see — which is what it is for in every animal that has one.
        /// </remarks>
        Flow = 8,
    }

    /// <summary>
    /// One input to a neuron: what it reads, and how strongly. DESIGN.md §4.3.
    /// </summary>
    public readonly struct NeuronInput
    {
        public readonly NeuronInputKind Kind;

        /// <summary>
        /// Neuron index within the referenced node or the global brain. For
        /// <see cref="NeuronInputKind.Sensor"/> this is the DOF or axis index instead.
        /// </summary>
        public readonly int Index;

        /// <summary>Only meaningful when <see cref="Kind"/> is <see cref="NeuronInputKind.Sensor"/>.</summary>
        public readonly SensorChannel Channel;

        /// <summary>Only meaningful when <see cref="Kind"/> is <see cref="NeuronInputKind.Constant"/>.</summary>
        public readonly float Constant;

        /// <summary>Connection weight. A perturbable scalar under DESIGN.md §4.5.</summary>
        public readonly float Weight;

        public NeuronInput(NeuronInputKind kind, int index, SensorChannel channel, float constant, float weight)
        {
            Kind = kind;
            Index = index;
            Channel = channel;
            Constant = constant;
            Weight = weight;
        }

        public static NeuronInput FromConstant(float value, float weight = 1f) =>
            new NeuronInput(NeuronInputKind.Constant, 0, SensorChannel.JointAngle, value, weight);

        public static NeuronInput FromSensor(SensorChannel channel, int index, float weight = 1f) =>
            new NeuronInput(NeuronInputKind.Sensor, index, channel, 0f, weight);

        public static NeuronInput FromNeuron(NeuronInputKind kind, int neuronIndex, float weight = 1f) =>
            new NeuronInput(kind, neuronIndex, SensorChannel.JointAngle, 0f, weight);

        public override string ToString() =>
            Kind == NeuronInputKind.Constant ? $"const {Constant}"
          : Kind == NeuronInputKind.Sensor ? $"{Channel}[{Index}] * {Weight}"
          : $"{Kind}[{Index}] * {Weight}";
    }
}
