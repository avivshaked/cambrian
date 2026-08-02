using System;

namespace Evosim.Core
{
    /// <summary>
    /// One neuron in a morph node's local brain — DESIGN.md §4.3.
    /// </summary>
    /// <remarks>
    /// [K12 §2.2, p.3]: <i>"Each body part contains a local neuro-controller (an artificial
    /// neural network), as well as a local sensor and effector."</i> Because these live
    /// inside the morph node, recursion copies them with the segment.
    /// </remarks>
    public sealed class NeuronDef
    {
        public NeuronOp Op { get; set; } = NeuronOp.Sum;

        public NeuronInput[] Inputs { get; set; } = Array.Empty<NeuronInput>();

        /// <summary>Oscillator frequency in Hz. Used by the waveform operators.</summary>
        public float Frequency { get; set; } = 1f;

        /// <summary>Oscillator phase in radians.</summary>
        public float Phase { get; set; }

        /// <summary>Output scale, applied after the operator.</summary>
        public float Amplitude { get; set; } = 1f;

        /// <summary>Output offset, applied after <see cref="Amplitude"/>.</summary>
        public float Bias { get; set; }

        public NeuronDef Clone() => new NeuronDef
        {
            Op = Op,
            Inputs = (NeuronInput[])Inputs.Clone(),
            Frequency = Frequency,
            Phase = Phase,
            Amplitude = Amplitude,
            Bias = Bias,
        };

        public override string ToString() => $"{Op}({Inputs.Length} in)";
    }
}
