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

        /// <summary>
        /// How deep this part is, as a fraction of the world's depth — DESIGN.md §5A.4.
        /// Milestone 4. 0 at the surface, 1 at the bottom.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Depth is the axis the whole environment is structured along.</b> Irradiance falls
        /// off down it, dead matter sinks down it, and §5A.4's argument that heterogeneity is
        /// what stops one strategy winning everywhere is entirely an argument about depth. A
        /// creature that cannot sense it cannot hold a niche.
        /// </para>
        /// <para>
        /// <b>Not redundant with <see cref="Photo"/>, and the reason is the night.</b> Irradiance
        /// is a usable depth proxy only while the sun is up; once §5A.4's diurnal cycle exists,
        /// light at night says nothing about depth at all. This channel is what makes diel
        /// vertical migration expressible — rising in darkness, sinking by day — which §5A.4
        /// names as the most watchable outcome the design could produce.
        /// </para>
        /// <para>
        /// <b>Called depth rather than pressure deliberately.</b> Pressure would be the nicer
        /// framing — a local quantity rather than a world coordinate — but §5.2 disables gravity
        /// outright to get neutral buoyancy, so hydrostatic pressure in this world is uniform and
        /// the name would promise a model that is not there.
        /// </para>
        /// <para>
        /// <b>It is the one channel reporting a world-frame quantity</b>, and that is worth
        /// stating rather than hiding. Every other channel is something physically present at the
        /// part. This is acceptable because depth is not an arbitrary coordinate here — it is a
        /// real gradient the creature is immersed in — and because the gradient rule in §4.4 still
        /// applies: two parts at different depths give a creature <i>which way is up</i> out of
        /// its own morphology, with no light required.
        /// </para>
        /// </remarks>
        Depth = 9,

        /// <summary>
        /// The smell of carrion at this part: the corpses' joules within <c>ScentRangeMetres</c>,
        /// each weighted by a cone from 1 at the corpse to 0 at the range, squashed as
        /// <c>x / (x + k)</c> — propose-foraging.md's third rule.
        /// </summary>
        /// <remarks>
        /// Per part, so two parts a body apart read two values and a brain comparing them has a
        /// direction. <see cref="Chemical"/> smells the dissolved snow, which a still stomach
        /// empties round itself; this smells the corpses, which lie where they fell.
        /// </remarks>
        Scent = 10,

        /// <summary>
        /// The power this joint is delivering now, per degree of freedom, 0 to 1: the brain's power
        /// command after the joint's threshold and envelope, what the fluid's push reads (D135's
        /// <c>EffectorDrive.Stroke</c>). <see cref="NeuronInput.Index"/> is the degree of freedom.
        /// </summary>
        /// <remarks>
        /// The drive sense (the owner, 2026-10-01): the stroke is not simulated as a swing, so no
        /// joint angle shows it, and before this channel a brain could not tell a power command
        /// taken from one held back by the hold or lost under the threshold. Its own output neuron
        /// says what it asked for; this says what the muscle is doing. One step stale, as
        /// <see cref="Flow"/> is: the envelope moves after the brain. Reads 0 where the world does
        /// not run the bend-and-power drive and on a part with no joint.
        /// </remarks>
        Stroke = 11,

        /// <summary>
        /// The share of the joint's hold still to run, per degree of freedom: 1 the step a command
        /// was taken, falling to 0, and 0 while the joint will take the next command. The envelope's
        /// other state the brain cannot otherwise see, since a command sent during the hold is
        /// ignored. <see cref="NeuronInput.Index"/> is the degree of freedom.
        /// </summary>
        Hold = 12,

        /// <summary>
        /// How far the power still has to ramp, per degree of freedom: the command the joint took
        /// less the power it delivers now, -1 to 1, and 0 once it has arrived. Positive while the
        /// stroke builds, negative while it eases. <see cref="NeuronInput.Index"/> is the degree of
        /// freedom.
        /// </summary>
        Ramp = 13,

        /// <summary>
        /// Whether this part's contact sphere was against the bed or a reef on the last physics
        /// step: 1 or 0. The glass is not rock and does not count, and neither does another body
        /// (that is <see cref="Contact"/>). <see cref="NeuronInput.Index"/> is ignored.
        /// </summary>
        /// <remarks>
        /// What the anchoring cell grips is the rock under a part, so a brain that is to decide
        /// when to hold needs to know when a part is on it (D162). One physics step stale: the
        /// brain reads it before that step's contact pass. Read on the part's contact sphere, so a
        /// part reads 1 from the moment its sphere meets the rock, which for a long part can be
        /// before its surface does. Needs per-part contact (<c>RunConfig.ContactPerPart</c>);
        /// <c>RunConfig.SenseBedTouch</c> lets a genome draw it.
        /// </remarks>
        BedTouch = 14,

        /// <summary>
        /// The smell of living leaves at this part (D167): the leaf field's square metres of
        /// photosynthetic area, cone-weighted, squashed as <c>x / (x + k)</c> with
        /// <c>RunConfig.LeafScentHalfScaleSquareMetres</c> as <c>k</c>. Kept apart from
        /// <see cref="Scent"/>, which is carrion. <see cref="NeuronInput.Index"/> is ignored.
        /// </summary>
        LeafScent = 15,

        /// <summary>
        /// The smell of living leaves with its direction (D167): the leaf field's gradient at this
        /// part, in the part's own frame as <see cref="Flow"/> is read, made unit length and scaled
        /// by what <see cref="LeafScent"/> would read. Zero where the gradient is.
        /// <see cref="NeuronInput.Index"/> is the axis, 0 to 2.
        /// </summary>
        LeafScentDirection = 16,

        /// <summary>
        /// Whether this part's grip is holding, 1 or 0 (D268). One step stale, as <see cref="BedTouch"/> is.
        /// <see cref="NeuronInput.Index"/> is ignored. A port of an anchoring part only, in a world whose
        /// anchoring cell lets the brain hold (<c>AnchoringCell.BrainHold</c>); it reads 0 anywhere else.
        /// </summary>
        /// <remarks>Numbers 19 to 21 are another build's (the carried smell), so this build takes 17 and 18.</remarks>
        Gripping = 17,

        /// <summary>
        /// How hard this part's grip is pulled: the pull over the grip's breaking force, 0 to 1, and 0 when not
        /// holding (D268). One step stale. <see cref="NeuronInput.Index"/> is ignored. A port of an anchoring part
        /// only, as <see cref="Gripping"/> is.
        /// </summary>
        GripPull = 18,
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
            Kind == NeuronInputKind.Constant
                ? System.FormattableString.Invariant($"const {Constant}")
          : Kind == NeuronInputKind.Sensor
                ? System.FormattableString.Invariant($"{Channel}[{Index}] * {Weight}")
          : System.FormattableString.Invariant($"{Kind}[{Index}] * {Weight}");
    }
}
