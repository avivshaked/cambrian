namespace Evosim.Core
{
    /// <summary>
    /// A joint's four drive genes (propose-joint-drive.md, the owner's ruling of 2026-09-30): the
    /// threshold below which its power output is off, and the envelope every command passes
    /// through. Every node carries them and only a joint reads them: a node that is not a joint
    /// holds <see cref="Default"/>, so a joint gained by mutation starts from a drawn set and a
    /// hand-built genome is a valid one without naming them.
    /// </summary>
    /// <remarks>
    /// A command is a change of a degree of freedom's power or bend. <see cref="Reaction"/> is
    /// the ramp, in seconds, from rest (power 0) to a first command; <see cref="Hold"/> is how long
    /// an accepted command stands before the joint takes another; <see cref="Release"/> is the ramp
    /// to every later command. Read only when the world runs the bend-and-power drive
    /// (<c>FluidConfig.PowerPushRadiansPerSecond</c> above 0); carried and mutated either way.
    /// </remarks>
    public struct DriveGenes
    {
        public const float MaxThreshold = 0.5f;
        public const float MinSeconds = 0.02f;
        public const float MaxSeconds = 1f;

        /// <summary>The power output below which the joint gets no drive, 0 to 0.5.</summary>
        public float Threshold;

        /// <summary>Seconds from rest to a first command, 0.02 to 1.</summary>
        public float Reaction;

        /// <summary>Seconds an accepted command stands, 0.02 to 1.</summary>
        public float Hold;

        /// <summary>Seconds to move to a later command, 0.02 to 1.</summary>
        public float Release;

        public DriveGenes(float threshold, float reaction, float hold, float release)
        {
            Threshold = threshold;
            Reaction = reaction;
            Hold = hold;
            Release = release;
        }

        /// <summary>No threshold, and a tenth of a second for each time.</summary>
        public static DriveGenes Default => new DriveGenes(0f, 0.1f, 0.1f, 0.1f);

        /// <summary>A founder's genes, drawn evenly over each range.</summary>
        public static DriveGenes Draw(Rng rng) => new DriveGenes(
            rng.Range(0f, MaxThreshold),
            rng.Range(MinSeconds, MaxSeconds),
            rng.Range(MinSeconds, MaxSeconds),
            rng.Range(MinSeconds, MaxSeconds));

        /// <summary>Why these genes are not a joint's, or null when they are.</summary>
        public string Problem()
        {
            if (!(Threshold >= 0f && Threshold <= MaxThreshold)) return $"threshold {Threshold} outside 0 to {MaxThreshold}";
            if (!InRange(Reaction)) return $"reaction {Reaction} s outside {MinSeconds} to {MaxSeconds}";
            if (!InRange(Hold)) return $"hold {Hold} s outside {MinSeconds} to {MaxSeconds}";
            if (!InRange(Release)) return $"release {Release} s outside {MinSeconds} to {MaxSeconds}";
            return null;
        }

        private static bool InRange(float s) => s >= MinSeconds && s <= MaxSeconds;
    }
}
