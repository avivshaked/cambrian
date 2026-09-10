using System;
using System.Globalization;
using System.Text;

namespace Evosim.Core
{
    /// <summary>
    /// One line of <c>positions.jsonl</c>: where every living body stood at one sample.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing in a run said where a body was.</b> The report carried a mean depth and
    /// per-patch bins, a lineage row carried a patch index, a snapshot carried a genome. On
    /// 2026-09-10 the owner opened round 33 seed 3 in the theatre and saw the whole world as two
    /// vertical ribbons about a metre wide in a box twenty metres long (logbook/0083). Thirty
    /// three rounds had been read on numbers that cannot show a ribbon. This file is the first of
    /// the instruments that answer the question from the maths side rather than by eye.
    /// </para>
    /// <para>
    /// <b>A recording, not a world rule.</b> It reads positions the harness has already taken and
    /// influences nothing, so it is not a tunable: it must not move the config hash and must not
    /// refuse an older config. It does move <c>simHash</c> and <c>coreHash</c>, which is why it
    /// lands after the owner's theatre pass on the growth base rather than during it.
    /// </para>
    /// <para>
    /// <b>Short, because it is per body per sample.</b> One entry is
    /// <c>[id,x,y,z,f]</c> and measures about 30 bytes at the campaign's ids and box, so a world
    /// of 1,500 bodies writes about 45 KB a sample and about 13 MB over 300 samples, which is the
    /// cadence of <c>stats.jsonl</c> over a full run. Named fields per body would have trebled
    /// that for nothing a reader cannot get from the shape.
    /// </para>
    /// <para>
    /// <b>Built here rather than in the harness</b> so that the format has a test that runs in a
    /// second outside the Editor (§6.1). Sim fills the arrays; this turns them into the row.
    /// </para>
    /// </remarks>
    public static class PositionsRow
    {
        /// <summary>Bit 0: any part of the developed body is <see cref="CellTypeIds.Absorptive"/>.</summary>
        public const int AbsorptiveBit = 1;

        /// <summary>Bit 1: the developed body has at least one actuated joint.</summary>
        public const int JointedBit = 2;

        /// <summary>Bit 2: any part of the developed body is <see cref="CellTypeIds.Photosynthetic"/>.</summary>
        public const int PhotosyntheticBit = 4;

        /// <summary>Every bit this format defines. Anything else on a flag is refused.</summary>
        /// <remarks>
        /// Refused rather than masked off, because a reader that meets an unknown bit has no way
        /// to tell a new guild from a corrupted row, and the same rule the loaders keep (refuse
        /// rather than default, §9) is the only one that keeps a stored guild honest.
        /// </remarks>
        public const int AllBits = AbsorptiveBit | JointedBit | PhotosyntheticBit;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>
        /// One row: the sample time, how many bodies it carries, and one entry per body.
        /// </summary>
        /// <param name="elapsedSeconds">
        /// The sample's simulated time, written exactly as <c>stats.jsonl</c> writes its own
        /// <c>t</c>, so that a reader can join the two files by string rather than by tolerance.
        /// </param>
        /// <param name="count">How many entries of the arrays to read. The arrays may be longer.</param>
        /// <param name="ids">The organism id, which joins to <c>lineage.jsonl</c>.</param>
        /// <param name="x">Metres along the box.</param>
        /// <param name="y">Metres, negative below the surface.</param>
        /// <param name="z">Metres across the box.</param>
        /// <param name="flags">
        /// <see cref="AbsorptiveBit"/>, <see cref="JointedBit"/> and
        /// <see cref="PhotosyntheticBit"/>, decided by the caller from the same tests that produce
        /// <c>absorpt</c>, <c>jointed</c> and <c>photo</c> in the report, so that the two files
        /// cannot disagree about what a stomach is.
        /// </param>
        /// <remarks>
        /// <b>Two decimals, and never a negative zero.</b> A centimetre is two orders of magnitude
        /// under the smallest body and four under the metre column the spread instrument counts,
        /// so nothing a reader asks of this file can see the truncation, and full precision would
        /// have doubled the file for digits no instrument reads. <c>-0</c> is legal JSON and
        /// parses as a float, but it reads as a sign in a column of positions and there is no
        /// sense in which a body is at minus zero metres.
        /// </remarks>
        public static string Write(
            double elapsedSeconds, int count,
            long[] ids, float[] x, float[] y, float[] z, int[] flags)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (y == null) throw new ArgumentNullException(nameof(y));
            if (z == null) throw new ArgumentNullException(nameof(z));
            if (flags == null) throw new ArgumentNullException(nameof(flags));

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Negative body count.");
            }

            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentException(
                    $"Sample time is {elapsedSeconds}, which JSON cannot represent.",
                    nameof(elapsedSeconds));
            }

            // Checked against every array rather than against the first, because the five are
            // filled by five separate loops in the caller and a short one would otherwise be read
            // as a body at the origin: a position nothing distinguishes from a real one.
            Require(ids.Length, count, nameof(ids));
            Require(x.Length, count, nameof(x));
            Require(y.Length, count, nameof(y));
            Require(z.Length, count, nameof(z));
            Require(flags.Length, count, nameof(flags));

            var sb = new StringBuilder(32 * count + 32);

            sb.Append("{\"t\":").Append(elapsedSeconds.ToString("R", Invariant));
            sb.Append(",\"n\":").Append(count.ToString(Invariant));
            sb.Append(",\"b\":[");

            for (int i = 0; i < count; i++)
            {
                if ((flags[i] & ~AllBits) != 0 || flags[i] < 0)
                {
                    throw new ArgumentException(
                        $"Body {i} (id {ids[i]}) carries flags {flags[i]}, which sets a bit this " +
                        $"format does not define (the defined bits are {AllBits}).",
                        nameof(flags));
                }

                if (i > 0) sb.Append(',');

                sb.Append('[').Append(ids[i].ToString(Invariant)).Append(',');
                Append(sb, x[i], i, ids[i], "x");
                sb.Append(',');
                Append(sb, y[i], i, ids[i], "y");
                sb.Append(',');
                Append(sb, z[i], i, ids[i], "z");
                sb.Append(',').Append(flags[i].ToString(Invariant)).Append(']');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void Require(int length, int count, string name)
        {
            if (length < count)
            {
                throw new ArgumentException(
                    $"'{name}' holds {length} entries and the row was asked for {count}.", name);
            }
        }

        /// <summary>
        /// One coordinate, rounded to the centimetre.
        /// </summary>
        /// <remarks>
        /// A non-finite coordinate is refused rather than written as <c>null</c>: it is a diverged
        /// body the harness should have skipped (<c>Ecosystem.TryRootPosition</c> drops one, as
        /// <c>MeasureHorizontalSpread</c> does), and a row that quietly carried one would put a
        /// creature nowhere and let a reader plot it anyway.
        /// </remarks>
        private static void Append(StringBuilder sb, float value, int index, long id, string axis)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException(
                    $"Body {index} (id {id}) has {axis} = {value}, which JSON cannot represent. " +
                    "A non-finite root here is a diverged body that was not skipped.");
            }

            // Away from zero, so that a coordinate is rounded the way a reader would round it by
            // hand rather than to the nearest even hundredth.
            double rounded = Math.Round((double)value, 2, MidpointRounding.AwayFromZero);

            // Both zeroes take this branch, which is the point: a body 4 mm above the surface
            // would otherwise be written "-0".
            if (rounded == 0d) { sb.Append('0'); return; }

            sb.Append(rounded.ToString("0.##", Invariant));
        }
    }
}
