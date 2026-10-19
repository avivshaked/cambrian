using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The run's own light at one second, for the underwater look's item 3 and item 6
    /// (<c>logbook/specs/underwater-look/brief.md</c>): how fast light falls with depth and how
    /// bright the day is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The recorded factor first, the build's function second.</b> Every round since the
    /// seasons writes its <c>dayFactor</c> into <c>stats.jsonl</c> at every sample, and that number
    /// is what the run's leaves were paid by, whatever build reads it now. This build's
    /// <see cref="LightModel.DayFactorAt"/> knows only the smooth season and no weather, and a
    /// run recorded on a build with a two-season year or a weather draw (round 57) would be read
    /// wrongly by it: at 27,000 s and 36,000 s of round 57 it answers 1.0 where the run recorded
    /// 0.34 and 2.82. So the row at or before the second is read, and the function is the
    /// fallback for a run that wrote no such column; <see cref="Reading.Source"/> says which.
    /// </para>
    /// <para>
    /// <b>The weather is not separate here.</b> The recorded factor already carries it, and this
    /// build has no <c>WeatherFactorAt</c> to take it apart, so the overcast is read from the day
    /// factor alone.
    /// </para>
    /// <para>
    /// <b>A picture-only reader.</b> Nothing here reaches a simulation; a run whose stats cannot be
    /// read gets the fallback and the log says so.
    /// </para>
    /// </remarks>
    public static class TheatreDaylight
    {
        /// <summary>The light at one second of one run.</summary>
        public struct Reading
        {
            /// <summary>The run's attenuation depth, metres: the light falls to 1/e over this.</summary>
            public float AttenuationDepth;

            /// <summary>The surface light's multiplier at the second: 1 is the mean day.</summary>
            public float DayFactor;

            /// <summary>The second read, or NaN when the frame does not say.</summary>
            public double Second;

            /// <summary>Where <see cref="DayFactor"/> came from, for the log.</summary>
            public string Source;

            /// <summary>
            /// How overcast the sky is, 0 (clear) to 1 (dull): 0 at a day factor of 1.2 and above,
            /// rising to 1 at 0.3. Read from the day factor alone, since this build cannot part the
            /// weather from it.
            /// </summary>
            public float Overcast => Clamp01((1.2f - DayFactor) / 0.9f);
        }

        /// <summary>The mean day at the theatre's own light reach: what a scene with no run gets.</summary>
        public static Reading Neutral(float attenuationDepth) => new Reading
        {
            AttenuationDepth = attenuationDepth > 0.01f ? attenuationDepth : 18f,
            DayFactor = 1f,
            Second = double.NaN,
            Source = "no run second (mean day)",
        };

        // One run's recorded factors, kept between frames: a film asks every frame.
        private static string _cachedPath;
        private static DateTime _cachedWrite;
        private static double[] _cachedSeconds;
        private static float[] _cachedFactors;

        /// <summary>The light at the second <paramref name="frame"/> is showing.</summary>
        public static Reading At(ITheatreFrame frame)
        {
            RunConfig config = frame?.Record?.Config;
            float depth = config?.Light != null ? config.Light.AttenuationDepth : 0f;

            if (frame == null || !TrySecond(frame, out double second)) return Neutral(depth);

            var reading = new Reading
            {
                AttenuationDepth = depth > 0.01f ? depth : 18f,
                DayFactor = 1f,
                Second = second,
                Source = "mean day",
            };

            string directory = frame.Record?.Path;

            if (!string.IsNullOrEmpty(directory) && TryRecorded(directory, second, out float recorded, out double at))
            {
                reading.DayFactor = recorded;
                reading.Source = string.Format(CultureInfo.InvariantCulture, "stats.jsonl dayFactor at {0:0.#} s", at);
                return reading;
            }

            if (config?.Light != null)
            {
                try
                {
                    reading.DayFactor = config.Light.DayFactorAt(second);
                    reading.Source = "this build's DayFactorAt (no recorded dayFactor; smooth season, no weather)";
                }
                catch (Exception e)
                {
                    reading.Source = "mean day (DayFactorAt refused: " + e.GetType().Name + ")";
                }
            }

            return reading;
        }

        /// <summary>The world's second on screen, where the frame can say.</summary>
        public static bool TrySecond(ITheatreFrame frame, out double second)
        {
            switch (frame)
            {
                case IFilmWorld film:
                    second = film.Second;
                    break;
                case SnapshotWorld snapshot:
                    second = snapshot.Second;
                    break;
                case TheatreReplay replay:
                    second = replay.ElapsedSeconds;
                    break;
                default:
                    second = double.NaN;
                    break;
            }

            return !double.IsNaN(second) && !double.IsInfinity(second);
        }

        /// <summary>
        /// The run's own <c>dayFactor</c> at the last sample at or before the second (the first
        /// sample, before it), from <c>stats.jsonl</c>.
        /// </summary>
        private static bool TryRecorded(string directory, double second, out float factor, out double at)
        {
            factor = 1f;
            at = double.NaN;

            string path = Path.Combine(directory, "stats.jsonl");
            if (!File.Exists(path)) return false;

            try
            {
                DateTime written = File.GetLastWriteTimeUtc(path);

                if (_cachedPath != path || _cachedWrite != written || _cachedSeconds == null)
                {
                    Load(path, out _cachedSeconds, out _cachedFactors);
                    _cachedPath = path;
                    _cachedWrite = written;
                }
            }
            catch (Exception)
            {
                _cachedPath = null;
                return false;
            }

            double[] seconds = _cachedSeconds;
            if (seconds == null || seconds.Length == 0) return false;

            int index = Array.BinarySearch(seconds, second);
            if (index < 0) index = Math.Max(0, ~index - 1);

            factor = _cachedFactors[index];
            at = seconds[index];
            return true;
        }

        /// <summary>
        /// Every row's <c>t</c> and <c>dayFactor</c>, by a scan for the two keys rather than a parse
        /// of the whole row: a long run's stats are tens of megabytes of columns nothing here reads.
        /// </summary>
        private static void Load(string path, out double[] seconds, out float[] factors)
        {
            var t = new List<double>();
            var f = new List<float>();

            foreach (string row in JsonlWriter.ReadRows(path))
            {
                if (!TryNumber(row, "\"t\":", out double second)) continue;
                if (!TryNumber(row, "\"dayFactor\":", out double factor)) continue;
                if (!(factor > 0.0) || double.IsInfinity(factor)) continue;

                // Rows are in time order; a stray earlier one (a resumed run's overlap) is skipped
                // so the binary search stays sound.
                if (t.Count > 0 && second <= t[t.Count - 1]) continue;

                t.Add(second);
                f.Add((float)factor);
            }

            seconds = t.ToArray();
            factors = f.ToArray();
        }

        private static bool TryNumber(string row, string key, out double value)
        {
            value = double.NaN;

            int start = row.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return false;
            start += key.Length;

            int end = start;
            while (end < row.Length && row[end] != ',' && row[end] != '}') end++;

            return double.TryParse(
                row.Substring(start, end - start).Trim(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
