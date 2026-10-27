using System;
using System.Collections.Generic;
using System.Linq;

namespace Evosim.Nursery
{
    /// <summary>
    /// One body's travel through a switching leaf tank (D236, piece 4), read from samples of where it is, as <see cref="PatchTrack"/> reads a
    /// passage through fixed patches. Here a patch is an <i>opening</i> of the tank's schedule (<see cref="LeafSchedule"/>): a fresh complement
    /// laid where a withered disc stood is a new patch. A pure class: it reads only what it is given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Time from a switch to arrival.</b> For each switch the watcher announces (<see cref="Switched"/>), the seconds from the switch to the
    /// body's first sample in the smell of a leaf of the newly opened opening. NaN if the body never arrives, and NaN if it was dead at the
    /// switch; <see cref="AliveAtSwitches"/> counts the switches it was alive at. A switch the body was alive at also keeps its follow-up: the
    /// seconds from the switch to the body's last alive sample, or to its arrival, so that a reader can treat the ones that never arrived as censored.
    /// </para>
    /// <para>
    /// <b>Eating by opening.</b> Each sample's gain in the cumulative eaten the tank reads is credited to the opening whose smell the body is in at
    /// that sample, or to none. A sample at which the body is dead credits its gain (nil in practice) to the opening of its last alive sample.
    /// A gain below zero is not credited. The first sample's gain counts from the baseline given at the start.
    /// </para>
    /// </remarks>
    public sealed class TravelTrack
    {
        /// <summary>One switch as this body met it.</summary>
        public sealed class Switch
        {
            /// <summary>1 for the first switch.</summary>
            public int Index { get; internal set; }

            public double SwitchSeconds { get; internal set; }

            /// <summary>The newly opened opening's id.</summary>
            public int Opening { get; internal set; }

            public bool AliveAtSwitch { get; internal set; }

            /// <summary>Seconds from the switch to the first sample in the new opening's smell; NaN if it never came, or the body was dead at the switch.</summary>
            public double ArrivalSeconds { get; internal set; } = double.NaN;

            /// <summary>Seconds from the switch to the arrival, else to the last alive sample; NaN if the body was dead at the switch.</summary>
            public double FollowSeconds { get; internal set; } = double.NaN;

            public bool Arrived => !double.IsNaN(ArrivalSeconds);
        }

        private readonly List<Switch> _switches = new List<Switch>();
        private readonly List<Switch> _waiting = new List<Switch>();
        private double _lastEaten;
        private bool _haveBaseline;
        private int _lastPatch = -1;

        /// <summary>Every switch announced, in order.</summary>
        public IReadOnlyList<Switch> Switches => _switches;

        /// <summary>The switches the body was alive at.</summary>
        public int AliveAtSwitches => _switches.Count(s => s.AliveAtSwitch);

        /// <summary>The joules credited to each opening (by id); an opening nothing was credited to is absent.</summary>
        public readonly Dictionary<int, double> EatenByOpening = new Dictionary<int, double>();

        /// <summary>The joules credited to no opening: eaten at a sample at which the body was in the smell of no leaf.</summary>
        public double EatenNone;

        /// <summary>The opening whose smell the body was first in, or -1 if it never was.</summary>
        public int FirstPatch { get; private set; } = -1;

        /// <summary>Everything credited, to an opening or to none.</summary>
        public double EatenTotal => EatenNone + EatenByOpening.Values.Sum();

        /// <param name="baselineEaten">The cumulative eaten as the first sample's gain counts from; NaN to take the first sample's own value as the baseline.</param>
        public TravelTrack(double baselineEaten = double.NaN)
        {
            if (!double.IsNaN(baselineEaten)) { _lastEaten = baselineEaten; _haveBaseline = true; }
        }

        /// <summary>
        /// A switch has come: the new opening's id and the time it opened, and whether the body is alive at the sample that first sees it. Called
        /// before that sample's <see cref="Observe"/>.
        /// </summary>
        public void Switched(int index, double switchSeconds, int opening, bool alive)
        {
            var s = new Switch { Index = index, SwitchSeconds = switchSeconds, Opening = opening, AliveAtSwitch = alive };
            if (alive) { s.FollowSeconds = 0d; _waiting.Add(s); }
            _switches.Add(s);
        }

        /// <summary>
        /// One sample: the time, s, whether the body is alive, the opening whose smell it is in (-1 for none), and the cumulative eaten the tank
        /// reads (<see cref="EpisodeProbe.EatenJoules"/>), ignored where NaN.
        /// </summary>
        public void Observe(double t, bool alive, int opening, double eatenTotal)
        {
            if (alive)
            {
                for (int i = _waiting.Count - 1; i >= 0; i--)
                {
                    Switch s = _waiting[i];
                    double since = Math.Max(0d, t - s.SwitchSeconds);
                    s.FollowSeconds = since;
                    if (opening == s.Opening)
                    {
                        s.ArrivalSeconds = since;
                        _waiting.RemoveAt(i);
                    }
                }
                if (opening >= 0 && FirstPatch < 0) FirstPatch = opening;
            }

            if (!double.IsNaN(eatenTotal))
            {
                if (!_haveBaseline) { _lastEaten = eatenTotal; _haveBaseline = true; }
                double gain = eatenTotal - _lastEaten;
                _lastEaten = eatenTotal;
                if (gain > 0d)
                {
                    int to = alive ? opening : _lastPatch;
                    if (to >= 0) EatenByOpening[to] = (EatenByOpening.TryGetValue(to, out double had) ? had : 0d) + gain;
                    else EatenNone += gain;
                }
            }
            if (alive) _lastPatch = opening;
        }

        /// <summary>
        /// The share of the body's eating (all of it, the eating in no smell included) taken in openings that opened after the body's first patch.
        /// <paramref name="openedAt"/> is each opening's opening time by id. NaN where the body never was in a smell or ate nothing.
        /// </summary>
        public double LaterShare(IReadOnlyList<double> openedAt)
        {
            double total = EatenTotal;
            if (FirstPatch < 0 || !(total > 0d)) return double.NaN;
            if (FirstPatch >= openedAt.Count) throw new ArgumentException("the first patch is not among the openings given", nameof(openedAt));
            double later = 0d;
            foreach (KeyValuePair<int, double> e in EatenByOpening)
            {
                if (e.Key >= openedAt.Count) throw new ArgumentException("an opening credited is not among the openings given", nameof(openedAt));
                if (openedAt[e.Key] > openedAt[FirstPatch]) later += e.Value;
            }
            return later / total;
        }
    }
}
