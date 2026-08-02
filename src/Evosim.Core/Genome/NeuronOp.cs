using System;

namespace Evosim.Core
{
    /// <summary>
    /// Neuron operators — Sims' set, lightly trimmed. DESIGN.md §4.3.
    /// </summary>
    public enum NeuronOp
    {
        // Arithmetic
        Sum = 0,
        Product = 1,
        Divide = 2,
        Abs = 3,
        Min = 4,
        Max = 5,

        // Comparison
        GreaterThan = 10,
        SignOf = 11,
        If = 12,
        Interpolate = 13,

        // Waveform
        Sin = 20,
        Cos = 21,
        OscillateWave = 22,
        OscillateSaw = 23,

        // Transfer
        Sigmoid = 30,
        SumThreshold = 31,

        // Temporal
        Integrate = 40,
        Differentiate = 41,
        Smooth = 42,
        Memory = 43,
    }

    public static class NeuronOps
    {
        /// <summary>
        /// The MVP operator set (DESIGN.md §4.3): a pure central pattern generator.
        /// </summary>
        /// <remarks>
        /// This is a <b>population constraint, not a separate system</b> — the initial
        /// population is restricted to these operators, and no code is discarded when the
        /// restriction lifts. <c>OscillateSaw</c> is here deliberately: [C18 §4, p.30]
        /// warns that purely harmonic actuation is a real limitation in unsteady aquatic
        /// locomotion, and a sawtooth has the asymmetric duty cycle a sine cannot express.
        /// </remarks>
        public static readonly NeuronOp[] MvpSet =
        {
            NeuronOp.OscillateWave,
            NeuronOp.OscillateSaw,
            NeuronOp.Sin,
            NeuronOp.Sum,
            NeuronOp.Sigmoid,
        };

        public static readonly NeuronOp[] All = (NeuronOp[])Enum.GetValues(typeof(NeuronOp));

        /// <summary>Number of inputs an operator consumes.</summary>
        public static int Arity(this NeuronOp op)
        {
            switch (op)
            {
                case NeuronOp.Abs:
                case NeuronOp.SignOf:
                case NeuronOp.Sin:
                case NeuronOp.Cos:
                case NeuronOp.Sigmoid:
                case NeuronOp.Integrate:
                case NeuronOp.Differentiate:
                case NeuronOp.Memory:
                    return 1;

                case NeuronOp.Sum:
                case NeuronOp.Product:
                case NeuronOp.Divide:
                case NeuronOp.Min:
                case NeuronOp.Max:
                case NeuronOp.GreaterThan:
                case NeuronOp.SumThreshold:
                case NeuronOp.Smooth:
                    return 2;

                case NeuronOp.If:
                case NeuronOp.Interpolate:
                    return 3;

                // Oscillators are driven by their parameters and simulation time, not by inputs.
                case NeuronOp.OscillateWave:
                case NeuronOp.OscillateSaw:
                    return 0;

                default:
                    return 0;
            }
        }

        public static bool IsInMvpSet(this NeuronOp op)
        {
            for (int i = 0; i < MvpSet.Length; i++)
            {
                if (MvpSet[i] == op) return true;
            }
            return false;
        }
    }
}
