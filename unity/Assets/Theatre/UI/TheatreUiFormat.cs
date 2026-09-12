using System.Globalization;
using System.Text;

namespace Evosim.Theatre
{
    /// <summary>
    /// Every number on the theatre's interface, written the one way the design settled on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Grouping is semantic, not typographic</b> (design/canvas/theatre.uss, NUMBERS).
    /// Quantities and times group every three digits with a thin space from 1 000 up — alive
    /// 1 842, births 41 207, t 24 300 s, lived 3 120 s. Identifiers never group — creature 4821,
    /// parent 4390, seed 1, sample 251, config hash 96d4bce667. A thin space says "this is a
    /// magnitude you may compare"; its absence says "this is a name". So the two live in
    /// different methods here and a caller has to choose, which is the point.
    /// </para>
    /// <para>
    /// <b>The thin space is built from its code point and never typed.</b> U+2009 appears in no
    /// design file as a literal — both documents write it as a code point or a plain space — it
    /// is invisible in every editor, and every grouped number on screen depends on it. A literal
    /// would be one careless copy away from becoming an ordinary space or nothing at all. The
    /// eleven visible glyphs below are literals, and they are listed here because they are also
    /// the set the SDF atlases have to bake: a glyph missing from an atlas draws as a blank box,
    /// and a blank box in the middle of an identity line does not look like a font problem.
    /// </para>
    /// <para>
    /// <b>Depth is the only true minus.</b> U+2212 MINUS SIGN, not the hyphen a format string
    /// produces, and only on a depth — a residual or a rate keeps the hyphen it was printed with,
    /// because the minus is a typographic promise about a column of measured heights rather than
    /// a rule about negative numbers.
    /// </para>
    /// <para>
    /// Everything is formatted in the invariant culture. A machine whose decimal separator is a
    /// comma would otherwise put one where the design's thin space goes, and the two would be
    /// impossible to tell apart at 13 px.
    /// </para>
    /// </remarks>
    public static class TheatreUiFormat
    {
        /// <summary>U+2009 THIN SPACE, the thousands separator of every grouped number.</summary>
        public static readonly string ThinSpace = char.ConvertFromUtf32(0x2009);

        /// <summary>U+2212 MINUS SIGN, a true minus. Depth only.</summary>
        public const string Minus = "−";

        /// <summary>U+00B7 MIDDLE DOT, the separator that carries the identity line.</summary>
        public const string Dot = "·";

        /// <summary>U+2014 EM DASH.</summary>
        public const string EmDash = "—";

        /// <summary>U+2013 EN DASH, ranges.</summary>
        public const string EnDash = "–";

        /// <summary>U+2192 RIGHTWARDS ARROW, the seek target.</summary>
        public const string RightArrow = "→";

        /// <summary>U+2190 LEFTWARDS ARROW, the ancestry chain.</summary>
        public const string LeftArrow = "←";

        /// <summary>U+2026 HORIZONTAL ELLIPSIS, elided ancestry.</summary>
        public const string Ellipsis = "…";

        /// <summary>U+2260 NOT EQUAL TO, a cousin badge and nothing else.</summary>
        public const string NotEqual = "≠";

        /// <summary>U+00D7 MULTIPLICATION SIGN: the pace, and the popover's mismatch mark.</summary>
        public const string Times = "×";

        /// <summary>U+03A3 GREEK CAPITAL LETTER SIGMA, the cumulative section's title.</summary>
        /// <remarks>
        /// <b>Sans only.</b> IBM Plex Mono has no sigma — checked with fontTools against both mono
        /// faces on 2026-09-12, where U+03A3 is the one glyph of the design's set that is missing
        /// — and the design uses it only in <c>.section__title</c>, which is Sans. A mono rule
        /// carrying it would draw a blank box.
        /// </remarks>
        public const string Sigma = "Σ";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>A quantity: grouped with a thin space from 1 000 up.</summary>
        public static string Quantity(long value) => Group(value.ToString(Invariant));

        /// <summary>A quantity that arrived as a double, rounded. Grouped.</summary>
        public static string Quantity(double value) =>
            Group(System.Math.Round(value).ToString("0", Invariant));

        /// <summary>An identifier — a creature, a parent, a seed, a sample. Never grouped.</summary>
        public static string Identifier(long value) => value.ToString(Invariant);

        /// <summary>
        /// A simulated time in seconds, grouped, with one decimal — the clock's form.
        /// </summary>
        public static string Clock(double seconds)
        {
            string text = seconds.ToString("0.0", Invariant);
            int point = text.IndexOf('.');

            string whole = point < 0 ? text : text.Substring(0, point);
            string rest = point < 0 ? "" : text.Substring(point);

            return Group(whole) + rest;
        }

        /// <summary>A simulated time in seconds, grouped, whole — t=24 300, lived 3 120 s.</summary>
        public static string Seconds(double seconds) => Quantity(seconds);

        /// <summary>A depth in metres, one decimal, with a true minus below the surface.</summary>
        /// <param name="heightY">The height the world holds: zero at the surface, negative down.</param>
        public static string Depth(double heightY)
        {
            string text = System.Math.Abs(heightY).ToString("0.0", Invariant);
            return heightY < 0d ? Minus + text : text;
        }

        /// <summary>A fixed-point reading that is not a depth and is not grouped.</summary>
        public static string Fixed(double value, int decimals) =>
            value.ToString("F" + decimals.ToString(Invariant), Invariant);

        /// <summary>The pace, as the strip says it: 1.00x requested, 0.70x actual.</summary>
        public static string Pace(double requested, double measured) =>
            requested.ToString("0.00", Invariant) + Times + " requested " + Dot + " " +
            measured.ToString("0.00", Invariant) + Times + " actual";

        /// <summary>A wall-clock estimate, in the words the seek plate uses.</summary>
        public static string Remaining(double seconds)
        {
            if (seconds <= 0d) return "arriving";
            if (seconds > 90d) return "about " + (seconds / 60d).ToString("0", Invariant) + " min to go";
            return "about " + seconds.ToString("0", Invariant) + " s to go";
        }

        /// <summary>
        /// The physics step, as the strip prints it: 0.01, not 0.010 and never 0,01.
        /// </summary>
        /// <remarks>
        /// Invariant like everything else here, and for a reason with teeth: the step is the
        /// number that decides whether a reading about swimming may be read at all (CLAUDE.md's
        /// 0.02-screens, 0.01-confirms rule), and a viewer who cannot tell 0.01 from 0,01 at a
        /// glance is a viewer who can quote the wrong world.
        /// </remarks>
        public static string Step(double seconds) => seconds.ToString("0.####", Invariant);

        /// <summary>A hash, cut to its first ten characters, as the strip and the popover show it.</summary>
        public static string Hash(string hash) =>
            string.IsNullOrEmpty(hash) ? "(none)" :
            hash.Length <= 10 ? hash : hash.Substring(0, 10);

        /// <summary>
        /// Groups the digits of an already-formatted integer with a thin space, from 1 000 up.
        /// </summary>
        /// <remarks>
        /// Written out rather than taken from <c>NumberFormatInfo</c>, because a culture's group
        /// separator is a culture's business and this separator is the design's: a number grouped
        /// with whatever the operator's machine prefers would not be the same number on two
        /// machines, and both of them end up in the record as pictures.
        /// </remarks>
        private static string Group(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return digits;

            bool negative = digits[0] == '-';
            string body = negative ? digits.Substring(1) : digits;

            if (body.Length < 4) return digits;

            var text = new StringBuilder(body.Length + body.Length / 3);
            int lead = body.Length % 3;

            if (lead > 0) text.Append(body, 0, lead);

            for (int i = lead; i < body.Length; i += 3)
            {
                if (text.Length > 0) text.Append(ThinSpace);
                text.Append(body, i, 3);
            }

            return negative ? "-" + text.ToString() : text.ToString();
        }
    }
}
