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
    /// [K12 §2.2, p.4] used joint-angle sensors alone. The rest are staged:
    /// <see cref="Contact"/> arrives with land (Milestone 5) and <see cref="Photo"/> with
    /// the full brain graph (Milestone 6). They are declared now so the genome format does
    /// not change when they land.
    /// </remarks>
    public enum SensorChannel
    {
        /// <summary>Joint angle for one DOF. Uses <see cref="NeuronInput.Index"/> as the DOF.</summary>
        JointAngle = 0,

        /// <summary>Joint angular velocity for one DOF.</summary>
        JointAngularVelocity = 1,

        /// <summary>Contact with terrain. Land only.</summary>
        Contact = 2,

        /// <summary>Part orientation against world up.</summary>
        OrientationUp = 3,

        /// <summary>Photoreceptor triple. Milestone 6.</summary>
        Photo = 4,
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
