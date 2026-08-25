using System;

namespace Evosim.Core
{
    /// <summary>
    /// Which of <see cref="SensorChannel"/>'s channels a simulator actually answers, and how
    /// many indices each one has — DESIGN.md §4.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every channel is legal in a genome; only some of them read anything.</b> The enum is
    /// deliberately complete so the serialization format does not change when a later milestone
    /// lands, and <see cref="Brain"/> answers an unimplemented channel with zero rather than
    /// throwing — a genome carrying a <see cref="SensorChannel.Photo"/> input is not invalid, it
    /// is early.
    /// </para>
    /// <para>
    /// <b>What this list is for is mutation and founder generation.</b> Drawing a channel
    /// uniformly from the enum would spend most of evolution's sensory mutations on channels
    /// that are wired to a constant zero, and — worse — it would do so invisibly: a neuron
    /// reading an unimplemented channel is indistinguishable from a neuron with a dead input,
    /// and both look exactly like a slightly worse creature. So new sensor references are drawn
    /// from here instead, and adding a channel to the simulator means adding one line to this
    /// array.
    /// </para>
    /// <para>
    /// <b>This is a promise made in <c>Evosim.Core</c> about code that lives in
    /// <c>Evosim.Sim</c></b>, which §6.1's no-<c>UnityEngine</c> rule makes impossible to check
    /// by reference. It is checked by measurement instead: the Milestone 1 smoke test drives a
    /// creature and asserts every channel named here reports a finite value that is not constant
    /// across the run. A channel listed here and unhandled there fails that check rather than
    /// quietly reading zero forever — which is the same failure that kept the whole brain
    /// unread until logbook/0016.
    /// </para>
    /// </remarks>
    public static class SensorChannels
    {
        /// <summary>
        /// Channels a simulator is expected to answer today. Founder generation and mutation
        /// draw sensor references only from here.
        /// </summary>
        public static readonly SensorChannel[] Implemented =
        {
            SensorChannel.JointAngle,
            SensorChannel.JointAngularVelocity,
            SensorChannel.OrientationUp,
            SensorChannel.Depth,
        };

        public static bool IsImplemented(this SensorChannel channel)
        {
            for (int i = 0; i < Implemented.Length; i++)
            {
                if (Implemented[i] == channel) return true;
            }

            return false;
        }

        /// <summary>
        /// How many distinct indices <see cref="NeuronInput.Index"/> may take for this channel.
        /// </summary>
        /// <remarks>
        /// An upper bound rather than an exact count for the joint channels, because the real
        /// number is the owning part's degree-of-freedom count and that is a property of the
        /// developed phenotype, not of the genome — the same node can be grown onto joints of
        /// different types. A reference past the end reads zero, which is what a rigid part's
        /// joint angle should read anyway.
        /// </remarks>
        public static int IndexCount(this SensorChannel channel)
        {
            switch (channel)
            {
                case SensorChannel.JointAngle:
                case SensorChannel.JointAngularVelocity:
                case SensorChannel.Flow:
                case SensorChannel.Photo:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>Draws a channel and a valid index for it.</summary>
        public static NeuronInput RandomSensor(Rng rng, float weight)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            SensorChannel channel = rng.Pick(Implemented);
            return NeuronInput.FromSensor(channel, rng.Range(channel.IndexCount()), weight);
        }
    }
}
