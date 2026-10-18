using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// The episode seeds a run swims: a draw's seed is the run's seed with a salt mixed in, then the generation times 1,000
    /// plus the draw. The search's salt is <see cref="GenerationSalt"/>; the exam's, and a bench's, is the recipe's
    /// <c>exam.salt</c>, at generation 0. The one place the formula lives, so that the run, <c>--restep</c> and the recipe's
    /// check of held-out seeds cannot drift apart.
    /// </summary>
    public static class EpisodeSeeds
    {
        /// <summary>The salt of the search's episode seeds.</summary>
        public const ulong GenerationSalt = 0x67656EUL;

        /// <summary>The seeds of <paramref name="count"/> draws of generation <paramref name="generation"/> under <paramref name="salt"/>.</summary>
        public static ulong[] For(ulong runSeed, int generation, int count, ulong salt)
        {
            var seeds = new ulong[count];
            for (int e = 0; e < count; e++) seeds[e] = Rng.SeedFor(runSeed ^ salt, (ulong)(generation * 1000 + e));
            return seeds;
        }

        /// <summary>
        /// The first of the exam's seeds that some search generation, 0 to <paramref name="generations"/> inclusive (the cap,
        /// which the search swims last), also swims: (exam draw, generation, search draw). Null when the exam's episodes are
        /// held out from every one the search could swim.
        /// </summary>
        public static (int examDraw, int generation, int draw)? ExamOverlap(
            ulong runSeed, int generations, int draws, ulong examSalt, int examEpisodes)
        {
            var exam = new Dictionary<ulong, int>();
            ulong[] e = For(runSeed, 0, examEpisodes, examSalt);
            for (int k = 0; k < e.Length; k++) if (!exam.ContainsKey(e[k])) exam[e[k]] = k;
            for (int g = 0; g <= generations; g++)
            {
                ulong[] s = For(runSeed, g, draws, GenerationSalt);
                for (int d = 0; d < s.Length; d++)
                    if (exam.TryGetValue(s[d], out int k)) return (k, g, d);
            }
            return null;
        }
    }
}
