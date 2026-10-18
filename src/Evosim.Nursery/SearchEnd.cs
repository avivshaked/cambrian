using System;
using System.Collections.Generic;
using System.Linq;

namespace Evosim.Nursery
{
    /// <summary>
    /// D172's two pure parts of a tank search's end: the stop on a stalled curve, and the exam's pool of every generation's top
    /// beside the last generation's elites, each genome once. The loop (Tank.cs) calls these; nothing here swims or draws.
    /// </summary>
    public static class SearchEnd
    {
        /// <summary>One generation's reading of the stop rule.</summary>
        public readonly struct Verdict
        {
            /// <summary>The measure averaged over the last window of generations; NaN before a whole window has swum.</summary>
            public readonly double Smoothed;
            /// <summary>The smoothed measure's gain over <c>over</c> generations, as a share of the size of where it stood; NaN when that is unknown or 0.</summary>
            public readonly double Gain;
            public readonly bool Stop;
            public Verdict(double smoothed, double gain, bool stop) { Smoothed = smoothed; Gain = gain; Stop = stop; }
        }

        /// <summary>The measure averaged over generations <paramref name="g"/> - window + 1 to g; NaN before generation window - 1, or where one is NaN.</summary>
        public static double Smoothed(IReadOnlyList<double> curve, int g, int window)
        {
            if (g < window - 1 || g >= curve.Count) return double.NaN;
            double sum = 0;
            for (int k = g - window + 1; k <= g; k++) sum += curve[k];
            return sum / window;
        }

        /// <summary>
        /// The rule after the last generation of <paramref name="curve"/> (generation curve.Count - 1): stop when it is at least
        /// <paramref name="floor"/> and the smoothed measure has gained no more than <paramref name="gain"/> of itself in
        /// <paramref name="over"/> generations. The share is of the size of where it stood, so a measure that is below 0, as a reward net
        /// of costs can be (D178), gains when it rises towards 0; on eaten, never below 0, that is the share D172 ruled. A smoothed
        /// measure of 0 back then gives no share, so the rule does not stop on it. The cap is the loop's, not the rule's.
        /// </summary>
        public static Verdict Judge(IReadOnlyList<double> curve, int window, double gain, int over, int floor)
        {
            int g = curve.Count - 1;
            double now = Smoothed(curve, g, window);
            double then = Smoothed(curve, g - over, window);
            double share = double.IsNaN(now) || double.IsNaN(then) || then == 0 ? double.NaN : (now - then) / Math.Abs(then);
            return new Verdict(now, share, g >= floor && !double.IsNaN(share) && share <= gain);
        }

        /// <summary>One genome of the exam's pool: its text, the id it is named by (its first), and every place it came from.</summary>
        public sealed class Entry
        {
            public string Text, Id;
            public int Body;
            /// <summary>The generations it was the top of, in order.</summary>
            public List<int> Tops = new List<int>();
            public bool Elite;
            /// <summary>Every id it had where it came in: a top's id in the generation it topped, an elite's in the last generation.</summary>
            public List<string> Ids = new List<string>();
        }

        /// <summary>
        /// The tops in generation order and then the elites in rank order, each genome (by its text) once, in the order it first came.
        /// </summary>
        public static List<Entry> ExamPool(IReadOnlyList<(string text, string id, int body)> tops, IReadOnlyList<(string text, string id, int body)> elites)
        {
            var pool = new List<Entry>();
            var byText = new Dictionary<string, Entry>(StringComparer.Ordinal);
            Entry At(string text, string id, int body)
            {
                if (!byText.TryGetValue(text, out Entry e))
                {
                    e = new Entry { Text = text, Id = id, Body = body };
                    byText.Add(text, e);
                    pool.Add(e);
                }
                if (!e.Ids.Contains(id)) e.Ids.Add(id);
                return e;
            }
            for (int g = 0; g < tops.Count; g++) At(tops[g].text, tops[g].id, tops[g].body).Tops.Add(g);
            foreach (var x in elites) At(x.text, x.id, x.body).Elite = true;
            return pool;
        }
    }
}
