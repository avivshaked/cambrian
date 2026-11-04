using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// The death caption (<c>EVOSIM_THEATRE_DEATH_CAPTION</c>): a film whose close-shot subject dies inside the window names the death
    /// in its label, as D279's birth cloud names a birth (<see cref="TheatreBirthCloud.TryCaption"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and then nothing is read.</b> The film asks for the caption only with the dial on. With it off no file is
    /// opened, no row is looked up, no log line is written, and the label is as it was.
    /// </para>
    /// <para>
    /// <b>Which film.</b> One whose <c>EVOSIM_THEATRE_FILM_SUBJECT</c> names one body by its id, and whose window holds that body's death
    /// row (<see cref="FilmWindowReader.TryDeathOf"/>). The cause is the row's own <c>c</c>: <c>aged</c>, <c>starved</c> or
    /// <c>eaten</c>; any other cause (or none) says nothing.
    /// </para>
    /// <para>
    /// <b>The words.</b> From the first frame at or after the death row's second the label gains <c>"  &lt;id&gt; dies of old age at
    /// &lt;a&gt;"</c>, <c>"  &lt;id&gt; dies of hunger at &lt;a&gt;"</c> or <c>"  &lt;id&gt; is eaten"</c>. A film that opens after the death
    /// row's second (the 50x film of the remains) has the past tense from its first frame, <c>"  &lt;id&gt; died of old age at &lt;a&gt;; its
    /// remains"</c>, and so on; the label's own time-lapse words already give the speed. Before the death row's second nothing is added.
    /// </para>
    /// <para>
    /// <b>The age</b> is the death second less the birth second, in whole seconds, as <c>&lt;m&gt; min &lt;s&gt; s</c> (under a minute,
    /// <c>&lt;s&gt; s</c>). The birth row is in the run's own <c>lineage.jsonl</c>, not in the window when the subject was born before
    /// it opened; the file is one row per creature ever born and large, so it is streamed once, line by line, and the read stops at the
    /// subject's row. A birth that cannot be found leaves the age out, and the one log line says so.
    /// </para>
    /// </remarks>
    public static class TheatreDeathCaption
    {
        /// <summary>The dial, <c>EVOSIM_THEATRE_DEATH_CAPTION</c> (0 or 1, 0 by default), read once.</summary>
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_DEATH_CAPTION", 0f, 0f, 1f) > 0f;

        private static FilmWindowWorld _window;
        private static bool _read;
        private static bool _has;

        // What the caption says, fixed once the subject's death row and birth row are read.
        private static long _subject;
        private static double _deathSecond;
        private static string _cause;
        private static string _age;

        // The second of the first frame the caption was asked for: the film's opening, which the window's own first second is not
        // (a window recorded from 22,500 s can open a film at 22,511 s).
        private static double _opened = double.NaN;

        /// <summary>
        /// The caption for the frame on screen, to be appended to the label. False with the dial off, with
        /// <c>EVOSIM_THEATRE_FILM_SUBJECT</c> naming no single body, for a subject with no death row of a known cause in the window, and
        /// before the death row's second; the label is then left as it was.
        /// </summary>
        public static bool TryCaption(FilmWindowWorld window, out string caption)
        {
            caption = null;
            if (!On || window == null || window.Window == null) return false;

            if (!_read || !ReferenceEquals(window, _window))
            {
                _read = true;
                _window = window;
                _has = Read(window);
                _opened = window.Second;
            }

            if (!_has) return false;

            double second = window.Second;
            if (double.IsNaN(second) || second < _deathSecond) return false;

            var inv = CultureInfo.InvariantCulture;
            string id = _subject.ToString(inv);

            // A film that opens after the death row's second speaks of it in the past from its first frame.
            bool past = !double.IsNaN(_opened) && _opened > _deathSecond;
            string at = _age != null ? " at " + _age : "";

            switch (_cause)
            {
                case "aged":
                    caption = past ? "  " + id + " died of old age" + at + "; its remains" : "  " + id + " dies of old age" + at;
                    break;
                case "starved":
                    caption = past ? "  " + id + " died of hunger" + at + "; its remains" : "  " + id + " dies of hunger" + at;
                    break;
                default:
                    caption = past ? "  " + id + " was eaten; its remains" : "  " + id + " is eaten";
                    break;
            }

            return true;
        }

        /// <summary>Finds the subject's death row and its birth second, writes the one log line; false where there is nothing to say.</summary>
        private static bool Read(FilmWindowWorld window)
        {
            string pick = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_FILM_SUBJECT") ?? "").Trim();
            if (!long.TryParse(pick, NumberStyles.Integer, CultureInfo.InvariantCulture, out _subject)) return false;
            if (!window.Window.TryDeathOf(_subject, out FilmWindowEvent death)) return false;

            string cause = death.Cause ?? "";
            if (cause != "aged" && cause != "starved" && cause != "eaten") return false;

            _cause = cause;
            _deathSecond = death.Seconds;
            _age = null;

            var inv = CultureInfo.InvariantCulture;
            string run = window.Record != null ? window.Record.Path : null;
            double born = FindBirth(run, _subject);

            if (!double.IsNaN(born) && death.Seconds >= born)
            {
                // Aged and starved name the age; eaten names none.
                if (cause != "eaten") _age = Age(death.Seconds - born);
                Debug.Log(string.Format(inv, "[Theatre] death caption: {0} {1} at {2:0.##} s, born {3:0.##} s (from lineage.jsonl)",
                    _subject, cause, death.Seconds, born));
            }
            else
            {
                Debug.Log(string.Format(inv, "[Theatre] death caption: {0} {1} at {2:0.##} s, born unknown | birth not found",
                    _subject, cause, death.Seconds));
            }

            return true;
        }

        /// <summary>
        /// The subject's birth second from the run's <c>lineage.jsonl</c>, or NaN where the file, the row or the run is missing. Streamed
        /// line by line with <see cref="FileShare.ReadWrite"/>, so a live run's writer does not refuse it, and stopped at the subject's
        /// row. A line is parsed only where it holds the subject's id, so the millions of others cost a string search each.
        /// </summary>
        private static double FindBirth(string run, long id)
        {
            if (string.IsNullOrEmpty(run)) return double.NaN;

            try
            {
                string path = Path.Combine(run, "lineage.jsonl");
                if (!File.Exists(path)) return double.NaN;

                string needle = "\"id\":" + id.ToString(CultureInfo.InvariantCulture);

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16))
                using (var reader = new StreamReader(stream))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf(needle, StringComparison.Ordinal) < 0) continue;

                        JsonNode row;

                        try
                        {
                            row = Json.Parse(line);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        if (row == null || !row.Has("e") || !row.Has("id") || !row.Has("t")) continue;
                        if (row["e"].Kind != JsonNode.NodeKind.String || row["e"].AsString() != "b") continue;
                        if ((long)row["id"].AsDouble() != id) continue;

                        return row["t"].AsDouble();
                    }
                }
            }
            catch (Exception e)
            {
                // A cosmetic layer never takes the film down; the age is simply left out.
                Debug.LogWarning("[Theatre] death caption: lineage.jsonl could not be read: " + e.GetType().Name + ": " + e.Message);
            }

            return double.NaN;
        }

        /// <summary>An age in whole seconds as <c>"&lt;m&gt; min &lt;s&gt; s"</c>, or <c>"&lt;s&gt; s"</c> under a minute.</summary>
        private static string Age(double seconds)
        {
            var inv = CultureInfo.InvariantCulture;
            long whole = (long)Math.Floor(seconds);
            if (whole < 0L) whole = 0L;

            return whole < 60L
                ? whole.ToString(inv) + " s"
                : string.Format(inv, "{0} min {1} s", whole / 60L, whole % 60L);
        }
    }
}
