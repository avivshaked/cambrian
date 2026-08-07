using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Something a neuron can read about the world — DESIGN.md §4.4.
    /// </summary>
    /// <remarks>
    /// Implemented by the simulator, not by this assembly, for the reason §6.1 gives: joint angles
    /// and water velocity live in PhysX and <c>Evosim.Core</c> cannot see it. Same one-way seam as
    /// <see cref="World.Observe"/> — measurements are pushed in, and nothing here knows a solver
    /// exists. A null source reads zero everywhere, which is what a pure central pattern generator
    /// needs, and the MVP operator set of §4.3 is exactly that.
    /// </remarks>
    public interface ISensorField
    {
        /// <param name="partIndex">Part the neuron owning this input lives on.</param>
        /// <param name="channel">What to read.</param>
        /// <param name="index">DOF or axis, where the channel has several.</param>
        /// <returns>Normalised to approximately [-1, 1]. Zero for anything unavailable.</returns>
        float Read(int partIndex, SensorChannel channel, int index);
    }

    /// <summary>
    /// A developed creature's nervous system, evaluated one step at a time — DESIGN.md §4.3, §4.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what was missing, and its absence had consequences.</b> The genome has carried
    /// neurons, oscillator frequencies and input references since draft 1; development places them
    /// on every part; mutation perturbs them. Nothing read them. Every creature was driven by one
    /// shared sine on every degree of freedom, identical regardless of genome — so the controller
    /// was a constant across the population, a uniform flap produced no net thrust, and once
    /// mechanical work was billed the world deleted every joint it had in sixty seconds
    /// (logbook/0015, D029).
    /// </para>
    /// <para>
    /// <b>Updated synchronously, and that is not an implementation detail.</b> Every neuron reads
    /// the <i>previous</i> step's outputs and writes to a separate buffer, which are swapped at the
    /// end. In-place update would make a neuron's value depend on the order parts happen to be
    /// walked in — the class of fault that produces a plausible number rather than an error, and
    /// which this project has already paid for twice (logbook/0007, logbook/0008). It is also what
    /// makes §4.4's claim literally true: a signal crosses exactly one node per step, so a long
    /// body senses direction better and thinks about it slower, and damage propagates outward at a
    /// bounded speed with no relay mechanism needed.
    /// </para>
    /// <para>
    /// <b>Neuron <i>d</i> of a part drives degree of freedom <i>d</i> of its joint.</b> The genome
    /// has no effector-mapping field, so this had to be defined rather than read. It is the only
    /// mapping that survives recursion intact — neurons are copied with the morph node, so the
    /// mapping is copied with them, which is what makes a duplicated segment a duplicated
    /// controller (§4.3). A dedicated output neuron would need a new genome field; summing every
    /// neuron would make gain depend on neuron count, so adding a neuron for some unrelated purpose
    /// would silently change how hard the creature swims.
    /// </para>
    /// <para>
    /// <b>Every operator is implemented, not just the MVP set.</b> §4.3 is explicit that the MVP is
    /// a population constraint rather than a separate system — <c>GenomeFactory</c> draws from
    /// <see cref="NeuronOps.MvpSet"/> and mutation can leave it — so restricting the evaluator
    /// would turn a constraint on the starting population into a permanent ceiling.
    /// </para>
    /// </remarks>
    public sealed class Brain
    {
        /// <summary>Neuron outputs from the last completed step. Index by the layout below.</summary>
        private float[] _previous;
        private float[] _current;

        /// <summary>
        /// One slot per neuron for the operators that remember something across steps.
        /// </summary>
        /// <remarks>
        /// <see cref="NeuronOp.Integrate"/> keeps its accumulator here,
        /// <see cref="NeuronOp.Differentiate"/> and <see cref="NeuronOp.Memory"/> the previous
        /// input, <see cref="NeuronOp.Smooth"/> its running value. Allocated once with the brain
        /// because a creature is evaluated tens of thousands of times and this is the hot loop.
        /// </remarks>
        private readonly float[] _memory;

        private readonly NeuronDef[][] _neurons;      // per part, then the global brain last
        private readonly int[] _offset;               // where each group starts in the buffers
        private readonly int[] _firstChild;           // per part, or -1
        private readonly int[] _parent;               // per part, or -1
        private readonly int[] _dofStart;             // per part, or -1 where the joint is fixed
        private readonly int[] _dofCount;

        /// <summary>Index of the global brain's group in <see cref="_offset"/>.</summary>
        private readonly int _globalGroup;

        /// <summary>Total actuated degrees of freedom — the length <c>drive</c> must be.</summary>
        public int TotalDof { get; }

        /// <summary>Neurons in this creature, body and global brain together.</summary>
        public int NeuronCount => _previous.Length;

        /// <summary>Simulated seconds this brain has been evaluated for.</summary>
        public double ElapsedSeconds { get; private set; }

        private Brain(
            NeuronDef[][] neurons, int[] offset, int[] parent, int[] firstChild,
            int[] dofStart, int[] dofCount, int totalNeurons, int totalDof)
        {
            _neurons = neurons;
            _offset = offset;
            _parent = parent;
            _firstChild = firstChild;
            _dofStart = dofStart;
            _dofCount = dofCount;
            _globalGroup = neurons.Length - 1;

            _previous = new float[totalNeurons];
            _current = new float[totalNeurons];
            _memory = new float[totalNeurons];

            TotalDof = totalDof;
        }

        /// <summary>
        /// Builds the nervous system of a developed body. Call once, at birth.
        /// </summary>
        /// <param name="phenotype">The developed body. Supplies neurons, topology and joints.</param>
        /// <param name="globalBrain">
        /// <see cref="Genome.GlobalBrain"/> — neurons owned by no part, readable from anywhere.
        /// </param>
        public static Brain For(Phenotype phenotype, NeuronDef[] globalBrain = null)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));

            int parts = phenotype.PartCount;
            var neurons = new NeuronDef[parts + 1][];
            var offset = new int[parts + 1];
            var parent = new int[parts];
            var firstChild = new int[parts];
            var dofStart = new int[parts];
            var dofCount = new int[parts];

            for (int i = 0; i < parts; i++) firstChild[i] = -1;

            int cursor = 0;
            int dofCursor = 0;

            for (int i = 0; i < parts; i++)
            {
                PhenotypePart part = phenotype.Parts[i];

                neurons[i] = part.Neurons ?? Array.Empty<NeuronDef>();
                offset[i] = cursor;
                cursor += neurons[i].Length;

                parent[i] = part.ParentIndex;

                // First child by part index, which is depth-first pre-order, so it is the same
                // child on every evaluation and on every replay of the same genome (§7). "A
                // neuron in a child node's copy" (§4.3) does not say which when there are
                // several, and any answer that depended on iteration order would not be one.
                if (part.ParentIndex >= 0 && firstChild[part.ParentIndex] < 0)
                {
                    firstChild[part.ParentIndex] = i;
                }

                // Matches PhenotypeBuilder's assignment exactly: parts in order, the root
                // contributing nothing because Developer forces its joint to Fixed. If the two
                // ever disagreed every creature would drive the wrong joints and nothing would
                // throw — DofOrderingTests holds them together.
                int count = part.JointType.DofCount();
                dofCount[i] = count;
                dofStart[i] = count > 0 ? dofCursor : -1;
                dofCursor += count;
            }

            neurons[parts] = globalBrain ?? Array.Empty<NeuronDef>();
            offset[parts] = cursor;
            cursor += neurons[parts].Length;

            return new Brain(
                neurons, offset, parent, firstChild, dofStart, dofCount, cursor, dofCursor);
        }

        /// <summary>
        /// Evaluates every neuron once and writes one drive value per degree of freedom.
        /// </summary>
        /// <param name="seconds">Step length, for the temporal operators.</param>
        /// <param name="drive">
        /// Filled with values in [-1, 1], indexed as <c>EffectorDriver</c> expects. Must be at
        /// least <see cref="TotalDof"/> long.
        /// </param>
        /// <param name="sensors">Null for a pure pattern generator — every channel reads zero.</param>
        public void Step(float seconds, float[] drive, ISensorField sensors = null)
        {
            if (drive == null) throw new ArgumentNullException(nameof(drive));

            if (drive.Length < TotalDof)
            {
                throw new ArgumentException(
                    $"drive is {drive.Length} long and this creature has {TotalDof} degrees of " +
                    "freedom. A short buffer would leave the last joints on whatever the previous " +
                    "creature was doing.", nameof(drive));
            }

            ElapsedSeconds += seconds;
            float time = (float)ElapsedSeconds;

            for (int group = 0; group < _neurons.Length; group++)
            {
                NeuronDef[] group_ = _neurons[group];
                int at = _offset[group];
                int part = group == _globalGroup ? -1 : group;

                for (int n = 0; n < group_.Length; n++)
                {
                    _current[at + n] = Evaluate(group_[n], at + n, part, time, seconds, sensors);
                }
            }

            // Swap, so the next step reads what this one produced and nothing reads a value
            // written during its own step.
            float[] swap = _previous;
            _previous = _current;
            _current = swap;

            for (int i = 0; i < _dofStart.Length; i++)
            {
                int start = _dofStart[i];
                if (start < 0) continue;

                NeuronDef[] own = _neurons[i];
                int at = _offset[i];

                for (int d = 0; d < _dofCount[i]; d++)
                {
                    // A part with fewer neurons than degrees of freedom leaves the rest still.
                    // That is a real morphology — a joint nothing innervates — and not an error.
                    drive[start + d] = d < own.Length ? Clamp(_previous[at + d]) : 0f;
                }
            }
        }

        /// <summary>
        /// A neuron's output from the last completed step. Zero if it does not exist.
        /// </summary>
        /// <remarks>
        /// For inspection — tests, and the theatre's brain view (§6.1). The step loop reads the
        /// buffers directly.
        /// </remarks>
        public float Output(int partIndex, int neuronIndex)
        {
            int group = partIndex < 0 ? _globalGroup : partIndex;
            if (group < 0 || group >= _neurons.Length) return 0f;

            if (neuronIndex < 0 || neuronIndex >= _neurons[group].Length) return 0f;

            return _previous[_offset[group] + neuronIndex];
        }

        private float Evaluate(
            NeuronDef neuron, int self, int part, float time, float dt, ISensorField sensors)
        {
            float value;

            switch (neuron.Op)
            {
                // Oscillators are driven by their own parameters and by time, not by inputs
                // (§4.3), which is what lets a pure pattern generator need no sensors at all.
                case NeuronOp.OscillateWave:
                    value = (float)Math.Sin(2.0 * Math.PI * neuron.Frequency * time + neuron.Phase);
                    break;

                case NeuronOp.OscillateSaw:
                    value = Saw(neuron.Frequency * time + neuron.Phase / (2f * (float)Math.PI));
                    break;

                default:
                    value = Apply(neuron, self, part, dt, sensors);
                    break;
            }

            value = value * neuron.Amplitude + neuron.Bias;

            // A non-finite drive value reaches PhysX and diverges the solver, and a diverged
            // solver is a creature that has found infinite energy (§11.2). Divide, Integrate and
            // Product are all one mutation away from producing one.
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private float Apply(NeuronDef neuron, int self, int part, float dt, ISensorField sensors)
        {
            NeuronInput[] inputs = neuron.Inputs ?? Array.Empty<NeuronInput>();

            float a = inputs.Length > 0 ? Read(inputs[0], part, sensors) : 0f;
            float b = inputs.Length > 1 ? Read(inputs[1], part, sensors) : 0f;
            float c = inputs.Length > 2 ? Read(inputs[2], part, sensors) : 0f;

            switch (neuron.Op)
            {
                // Variable arity: these mean what they say over however many inputs there are,
                // which is what makes Sum a weighted sum rather than a two-input adder.
                case NeuronOp.Sum: return Fold(inputs, part, sensors, 0f, (x, y) => x + y);
                case NeuronOp.Product: return inputs.Length == 0 ? 0f
                    : Fold(inputs, part, sensors, 1f, (x, y) => x * y);
                case NeuronOp.Min: return inputs.Length == 0 ? 0f
                    : Fold(inputs, part, sensors, float.MaxValue, Math.Min);
                case NeuronOp.Max: return inputs.Length == 0 ? 0f
                    : Fold(inputs, part, sensors, float.MinValue, Math.Max);

                case NeuronOp.Divide: return Math.Abs(b) < 1e-6f ? 0f : a / b;
                case NeuronOp.Abs: return Math.Abs(a);

                case NeuronOp.GreaterThan: return a > b ? 1f : -1f;
                case NeuronOp.SignOf: return a > 0f ? 1f : a < 0f ? -1f : 0f;
                case NeuronOp.If: return a > 0f ? b : c;
                case NeuronOp.Interpolate: return b + (c - b) * Clamp01(a);

                case NeuronOp.Sin: return (float)Math.Sin(a);
                case NeuronOp.Cos: return (float)Math.Cos(a);

                // tanh rather than the logistic curve, and the effector is why. A logistic
                // sigmoid is strictly positive, so a joint driven by one could only ever push
                // one way and could not oscillate — every creature whose gait ran through one
                // would be paralysed in a direction. Both are sigmoids; only one is centred.
                case NeuronOp.Sigmoid: return (float)Math.Tanh(a);

                // The threshold is expressed by giving the neuron a constant input, exactly as a
                // perceptron's bias is. Baking one in would be an unmeasured constant (§5A.10)
                // in a place no run could vary.
                case NeuronOp.SumThreshold:
                    return Fold(inputs, part, sensors, 0f, (x, y) => x + y) > 0f ? 1f : -1f;

                case NeuronOp.Integrate:
                    // Bounded, because an unbounded accumulator reaches infinity and then NaN,
                    // and a NaN torque diverges the solver. The effector clamps to [-1, 1]
                    // anyway, so the range beyond that only matters to downstream neurons.
                    _memory[self] = Clamp(_memory[self] + a * dt, -100f, 100f);
                    return _memory[self];

                case NeuronOp.Differentiate:
                {
                    float rate = dt > 0f ? (a - _memory[self]) / dt : 0f;
                    _memory[self] = a;
                    return rate;
                }

                case NeuronOp.Smooth:
                    _memory[self] += (a - _memory[self]) * Clamp01(b);
                    return _memory[self];

                case NeuronOp.Memory:
                {
                    float held = _memory[self];
                    _memory[self] = a;
                    return held;
                }

                default: return a;
            }
        }

        private float Fold(
            NeuronInput[] inputs, int part, ISensorField sensors, float seed, Func<float, float, float> op)
        {
            float acc = seed;
            for (int i = 0; i < inputs.Length; i++) acc = op(acc, Read(inputs[i], part, sensors));
            return acc;
        }

        /// <summary>One input, resolved against the previous step and scaled by its weight.</summary>
        private float Read(NeuronInput input, int part, ISensorField sensors)
        {
            switch (input.Kind)
            {
                case NeuronInputKind.Constant:
                    return input.Constant * input.Weight;

                case NeuronInputKind.Sensor:
                    return sensors == null
                        ? 0f
                        : sensors.Read(part, input.Channel, input.Index) * input.Weight;

                case NeuronInputKind.SameNode:
                    return part < 0 ? 0f : FromGroup(part, input.Index) * input.Weight;

                case NeuronInputKind.ParentNode:
                {
                    if (part < 0) return 0f;
                    int owner = _parent[part];
                    return owner < 0 ? 0f : FromGroup(owner, input.Index) * input.Weight;
                }

                case NeuronInputKind.ChildNode:
                {
                    if (part < 0) return 0f;
                    int owner = _firstChild[part];
                    return owner < 0 ? 0f : FromGroup(owner, input.Index) * input.Weight;
                }

                case NeuronInputKind.GlobalBrain:
                    return FromGroup(_globalGroup, input.Index) * input.Weight;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// A neuron's last output, by index within its group.
        /// </summary>
        /// <remarks>
        /// <b>Wrapped rather than bounds-checked, though nothing should reach it.</b>
        /// <see cref="Genome.Validate"/> rejects an input naming a neuron that does not exist and
        /// <see cref="Developer"/> refuses to develop a genome that fails validation, so an
        /// out-of-range index cannot arrive here through the normal path — this is a guard, not a
        /// mechanism. It wraps rather than reading zero because if one ever did arrive, a severed
        /// connection is silent and a wrapped one at least keeps the neuron doing something
        /// visible.
        /// </remarks>
        private float FromGroup(int group, int index)
        {
            int count = _neurons[group].Length;
            if (count == 0) return 0f;

            int at = index % count;
            if (at < 0) at += count;

            return _previous[_offset[group] + at];
        }

        private static float Saw(float turns)
        {
            float phase = turns - (float)Math.Floor(turns);
            return 2f * phase - 1f;
        }

        private static float Clamp(float v) => Clamp(v, -1f, 1f);

        private static float Clamp(float v, float low, float high) =>
            v < low ? low : v > high ? high : v;

        private static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
