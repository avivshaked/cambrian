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
    /// from a list instead — <see cref="DefaultPool"/>, or the run's own
    /// <see cref="RunConfig.SensorPool"/> where a round has opened one of the later channels.
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
        /// Channels a simulator is expected to answer today — every one of them measured by the
        /// Milestone 1 smoke test rather than promised here.
        /// </summary>
        /// <remarks>
        /// <b>This is no longer the same list as the one mutation draws from.</b> It was, for as
        /// long as the two questions had the same answer: a channel the simulator answers is a
        /// channel worth drawing. Perception for movement separates them — <c>Chemical</c>,
        /// <c>Energy</c> and <c>Flow</c> are answered here from the day they were wired, and
        /// whether a *population* may acquire them is a property of the run rather than of the
        /// code, because turning them on changes which channel a given RNG draw yields and every
        /// run in the historical record would stop replaying. What may be drawn is
        /// <see cref="RunConfig.SensorPool"/>; what is answered is this.
        /// </remarks>
        public static readonly SensorChannel[] Implemented =
        {
            SensorChannel.JointAngle,
            SensorChannel.JointAngularVelocity,
            SensorChannel.OrientationUp,
            SensorChannel.Depth,
            SensorChannel.Chemical,
            SensorChannel.Energy,
            SensorChannel.Flow,
        };

        /// <summary>
        /// The pool a run draws from with every sense knob off — the four channels every run
        /// before <c>EVOSIM_SENSE_*</c> existed drew from, in the order it drew them in.
        /// </summary>
        /// <remarks>
        /// <b>The order is the identity requirement, not a style.</b> <see cref="Rng.Pick{T}"/>
        /// takes one draw and indexes the array with it, so a pool of the same length in the same
        /// order consumes the same draw and returns the same channel — which is what makes a
        /// default-configuration run bit-identical to the record across this change. Anything
        /// enabled is appended after <see cref="SensorChannel.Depth"/> for the same reason.
        /// </remarks>
        public static readonly SensorChannel[] DefaultPool =
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

        /// <summary>Draws a channel and a valid index for it, from the run's own pool.</summary>
        /// <param name="rng">The stream the draw comes from.</param>
        /// <param name="weight">The connection strength the new input carries.</param>
        /// <param name="pool">
        /// What this run lets a genome reach for — <see cref="RunConfig.SensorPool"/>. Null means
        /// <see cref="DefaultPool"/>, which is what every caller outside a world gets and what
        /// every run before the sense knobs existed had.
        /// </param>
        public static NeuronInput RandomSensor(Rng rng, float weight, SensorChannel[] pool = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            SensorChannel channel = rng.Pick(pool ?? DefaultPool);
            return NeuronInput.FromSensor(channel, rng.Range(channel.IndexCount()), weight);
        }
    }
}
