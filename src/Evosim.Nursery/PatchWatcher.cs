using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// One body's passage through a patchy leaf tank (D182), read from samples of where it is: in the smell of patch k (k ≥ 0), or in water
    /// that smells of no leaf (-1). It counts what the coordinator asked of the patchy arms on 2026-10-15: the share of the body's alive time
    /// spent in smell, the exits from a patch's smell and what followed each, the transits from one patch to another, and, for a body that
    /// starts in water that smells of nothing, the time to its first smell. A pure class: it reads only what it is given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Time.</b> The span between two samples is the body's state at the earlier one, counted only while the body was alive then. The
    /// first alive sample opens the count, so the time before it is no one's.
    /// </para>
    /// <para>
    /// <b>An exit</b> is a sample in bare water after one in patch k. What ends it is the next sample in a patch: patch k again within
    /// <see cref="WindowSeconds"/> of the exit is a <i>turn-back</i>, patch k later is a <i>late return</i>, another patch is a
    /// <i>transit</i>. An exit that nothing ends (the episode ends, or the body dies, in bare water) is <i>unended</i>. A sample in patch j
    /// straight after one in patch k, with no bare water between, is an exit and a transit at once.
    /// </para>
    /// </remarks>
    public sealed class PatchTrack
    {
        public readonly double WindowSeconds;

        public double AliveSeconds, ScentSeconds;
        /// <summary>Seconds in each patch's smell, by patch.</summary>
        public readonly Dictionary<int, double> PatchSeconds = new Dictionary<int, double>();
        /// <summary>Entries into each patch's smell, by patch: the first sample in it after one outside it, or the first alive sample.</summary>
        public readonly Dictionary<int, int> Entries = new Dictionary<int, int>();
        public int Exits, TurnBacks, LateReturns, Transits;
        /// <summary>When each transit arrived, s, and the patches it went from and to.</summary>
        public readonly List<(double t, int from, int to)> TransitLog = new List<(double t, int from, int to)>();
        /// <summary>Null before the first alive sample; else whether that sample was in a patch's smell.</summary>
        public bool? StartedInScent;
        /// <summary>For a body that started in bare water, the seconds from its first alive sample to its first smell; NaN if it never smelled one.</summary>
        public double FirstScentSeconds = double.NaN;
        public bool Died;

        private bool _open, _wasAlive;
        private double _lastT, _firstT;
        private int _last = -1;
        private int _exitPatch = -1;
        private double _exitT;

        public PatchTrack(double windowSeconds = 30d)
        {
            if (!(windowSeconds > 0d) || double.IsInfinity(windowSeconds)) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            WindowSeconds = windowSeconds;
        }

        /// <summary>Exits that nothing ended: the body was in bare water, after a patch, when the episode ended or it died.</summary>
        public int Unended => Exits - TurnBacks - LateReturns - Transits;

        /// <summary>One sample: the time, s, whether the body is alive, and the patch whose smell it is in (-1 for none; ignored when dead).</summary>
        public void Observe(double t, bool alive, int patch)
        {
            if (_open && t < _lastT) throw new ArgumentException("samples go forward in time", nameof(t));
            if (_open && _wasAlive)
            {
                double dt = t - _lastT;
                AliveSeconds += dt;
                if (_last >= 0)
                {
                    ScentSeconds += dt;
                    PatchSeconds[_last] = (PatchSeconds.TryGetValue(_last, out double s) ? s : 0d) + dt;
                }
            }
            if (!alive)
            {
                if (_open && _wasAlive) Died = true;
                _lastT = t;
                _wasAlive = false;
                return;
            }
            if (_open && !_wasAlive) throw new ArgumentException("a body that has died does not come back", nameof(alive));

            if (!_open)
            {
                _open = true;
                _firstT = t;
                StartedInScent = patch >= 0;
                if (patch >= 0) Enter(patch);
            }
            else if (patch != _last)
            {
                if (_last >= 0) { Exits++; _exitPatch = _last; _exitT = t; }
                if (patch >= 0)
                {
                    if (StartedInScent == false && double.IsNaN(FirstScentSeconds)) FirstScentSeconds = t - _firstT;
                    if (_exitPatch >= 0)
                    {
                        if (patch == _exitPatch)
                        {
                            if (t - _exitT <= WindowSeconds) TurnBacks++;
                            else LateReturns++;
                        }
                        else
                        {
                            Transits++;
                            TransitLog.Add((t, _exitPatch, patch));
                        }
                        _exitPatch = -1;
                    }
                    Enter(patch);
                }
            }
            _last = patch;
            _lastT = t;
            _wasAlive = true;
        }

        private void Enter(int patch) => Entries[patch] = (Entries.TryGetValue(patch, out int n) ? n : 0) + 1;
    }

    /// <summary>D182's patches, found again from the leaves where they stand: the groups that leaves closer than a link apply join.</summary>
    public static class PatchGroups
    {
        /// <summary>
        /// Each leaf's group, by single linkage across the surface: two leaves closer than <paramref name="link"/> m are in one group. Groups
        /// are numbered in the order of the leaves given, by the first leaf of each.
        /// </summary>
        public static Dictionary<long, int> Of(IReadOnlyList<(long id, double x, double z)> leaves, double link)
        {
            int n = leaves.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double dx = leaves[i].x - leaves[j].x, dz = leaves[i].z - leaves[j].z;
                    if (dx * dx + dz * dz < link * link) parent[Root(j)] = Root(i);
                }
            var number = new Dictionary<int, int>();
            var groups = new Dictionary<long, int>();
            for (int i = 0; i < n; i++)
            {
                int r = Root(i);
                if (!number.TryGetValue(r, out int g)) number[r] = g = number.Count;
                groups[leaves[i].id] = g;
            }
            return groups;
        }
    }

    /// <summary>
    /// The watcher <c>--restep --patches</c> swims an exam episode with (D182; the coordinator's question of 2026-10-15): on each economy step
    /// it reads every member's root, the leaf smell there (<see cref="EpisodeProbe.TryLeafScent"/>, filled from the living lit leaves out to
    /// the reach), and, where the smell is above 0, the patch of the nearest living leaf. The patches are the leaves' groups at the start
    /// (<see cref="PatchGroups"/>, a link of <see cref="LinkMetres"/>). It only reads, holds no random stream, and writes nothing itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In a switching tank</b> (D236, <see cref="EpisodeProbe.Schedule"/> not null) a leaf's patch is its opening (<c>OpeningOf</c>), not its
    /// 12 m group, so a fresh complement laid where a withered disc stood is a new patch, and <see cref="PatchCount"/> is the openings so far.
    /// The watcher then also keeps a <see cref="TravelTrack"/> a member (<see cref="Travel"/>): the time from each switch to arrival, and the eating by
    /// opening. A leaf is looked for as the nearest only once the smell has had a step to know it: the world fills the smell inside its step, and the
    /// switcher lays its leaves after, so a leaf laid since the last sample is not yet in the field it is read from. Without a schedule none of this
    /// runs, and every row is the row it always was.
    /// </para>
    /// </remarks>
    public sealed class PatchWatcher : ITankWatcher
    {
        /// <summary>The link that groups the leaves: half D184's nearest gap between patches (24.5 m), and more than a 6 m patch's width.</summary>
        public const double LinkMetres = 12d;

        public PatchTrack[] Tracks { get; private set; }
        public int PatchCount { get; private set; }
        public int LeafCount { get; private set; }
        public bool HasScent { get; private set; }

        /// <summary>A member's travel through the openings of a switching tank; null in a tank whose patches do not switch.</summary>
        public TravelTrack[] Travel { get; private set; }

        /// <summary>When each opening opened, by id, as far as the watcher has seen them; empty in a tank whose patches do not switch.</summary>
        public IReadOnlyList<double> OpenedAt => _openedAt;

        private readonly double _window;
        private Dictionary<long, int> _group;
        private LeafSchedule _schedule;
        private readonly List<double> _openedAt = new List<double>();
        private int _announced;
        private HashSet<long> _smelt;

        public PatchWatcher(double windowSeconds = 30d) { _window = windowSeconds; }

        public void Begin(EpisodeProbe probe)
        {
            IReadOnlyList<BodyView> leaves = probe.Others();
            LeafCount = leaves.Count;
            _schedule = probe.Schedule;
            if (_schedule == null)
            {
                _group = PatchGroups.Of(leaves.Select(l => (l.Id, l.X, l.Z)).ToList(), LinkMetres);
                PatchCount = _group.Values.Distinct().Count();
            }
            else
            {
                _openedAt.AddRange(_schedule.Openings.Select(o => o.OpenedAt));
                _announced = _openedAt.Count;
                PatchCount = _openedAt.Count;
                _smelt = new HashSet<long>(leaves.Select(l => l.Id));
            }
            HasScent = probe.TryLeafScent(0d, 0d, 0d, out _, out _, out _, out _);
            Tracks = Enumerable.Range(0, probe.MemberCount).Select(_ => new PatchTrack(_window)).ToArray();
            if (_schedule != null) Travel = Enumerable.Range(0, probe.MemberCount).Select(m => new TravelTrack(probe.EatenJoules(m))).ToArray();
        }

        public void Physics(EpisodeProbe probe) { }

        public void Metabolic(EpisodeProbe probe)
        {
            if (_schedule != null) { MetabolicSwitching(probe); return; }
            double t = probe.ClockSeconds;   // world seconds (D240): the switching schedule's own clock, and the same bits as the physics clock at a pace factor of 1
            IReadOnlyList<BodyView> leaves = null;
            for (int m = 0; m < Tracks.Length; m++)
            {
                if (!probe.Landed(m)) continue;
                if (!probe.Alive(m)) { Tracks[m].Observe(t, false, -1); continue; }
                if (!probe.TryLinkPose(m, 0, out LinkPose root)) continue;
                int patch = -1;
                if (probe.TryLeafScent(root.X, root.Y, root.Z, out double s, out _, out _, out _) && s > 0d)
                {
                    leaves = leaves ?? probe.Others();
                    patch = NearestGroup(leaves, root.X, root.Y, root.Z);
                }
                Tracks[m].Observe(t, true, patch);
            }
        }

        // A switching tank's last sample is the last economy step's, which Metabolic has taken: a second look at the same state would see
        // the leaves laid in that step as smelt, and could name another opening for the same instant.
        public void End(EpisodeProbe probe)
        {
            if (_schedule == null) Metabolic(probe);
        }

        /// <summary>
        /// The switching tank's sample: the openings that opened since the last one are announced to every member (before it is read), and each
        /// member's place is its nearest eligible leaf's opening. The leaves eligible are those the last sample saw, which the smell has had a step to know.
        /// </summary>
        private void MetabolicSwitching(EpisodeProbe probe)
        {
            double t = probe.ClockSeconds;
            IReadOnlyList<BodyView> leaves = probe.Others();
            IReadOnlyList<LeafOpening> openings = _schedule.Openings;
            var fresh = new List<LeafOpening>();
            while (_announced < openings.Count)
            {
                fresh.Add(openings[_announced]);
                _openedAt.Add(openings[_announced].OpenedAt);
                _announced++;
            }
            PatchCount = openings.Count;

            for (int m = 0; m < Tracks.Length; m++)
            {
                if (!probe.Landed(m)) continue;
                bool alive = probe.Alive(m);
                foreach (LeafOpening o in fresh) Travel[m].Switched(o.Id - 1, o.OpenedAt, o.Id, alive);
                if (!alive)
                {
                    Tracks[m].Observe(t, false, -1);
                    Travel[m].Observe(t, false, -1, probe.EatenJoules(m));
                    continue;
                }
                if (!probe.TryLinkPose(m, 0, out LinkPose root)) continue;
                int patch = -1;
                if (probe.TryLeafScent(root.X, root.Y, root.Z, out double s, out _, out _, out _) && s > 0d)
                    patch = NearestOpening(leaves, root.X, root.Y, root.Z);
                Tracks[m].Observe(t, true, patch);
                Travel[m].Observe(t, true, patch, probe.EatenJoules(m));
            }
            _smelt = new HashSet<long>(leaves.Select(l => l.Id));
        }

        private int NearestOpening(IReadOnlyList<BodyView> leaves, double x, double y, double z)
        {
            int best = -1;
            double near = double.PositiveInfinity;
            foreach (BodyView l in leaves)
            {
                if (!_smelt.Contains(l.Id)) continue;
                int opening = _schedule.OpeningOf(l.Id);
                if (opening < 0) continue;
                double d = (l.X - x) * (l.X - x) + (l.Y - y) * (l.Y - y) + (l.Z - z) * (l.Z - z);
                if (d < near) { near = d; best = opening; }
            }
            return best;
        }

        private int NearestGroup(IReadOnlyList<BodyView> leaves, double x, double y, double z)
        {
            int best = -1;
            double near = double.PositiveInfinity;
            foreach (BodyView l in leaves)
            {
                if (!_group.TryGetValue(l.Id, out int g)) continue;
                double d = (l.X - x) * (l.X - x) + (l.Y - y) * (l.Y - y) + (l.Z - z) * (l.Z - z);
                if (d < near) { near = d; best = g; }
            }
            return best;
        }

        /// <summary>One row of <c>patches.jsonl</c>: a member's track, named by the request's numbers and its slot, pool line and body.</summary>
        /// <remarks>
        /// With a <paramref name="travel"/> (a switching tank) the row ends with the travel columns: <c>switches</c> (announced so far),
        /// <c>aliveAtSwitch</c>, <c>arrivals</c> and <c>follow</c> (a value a switch, null where NaN), <c>firstPatch</c> (the opening first smelt),
        /// <c>laterShare</c> (the share of the eating taken in openings opened after it; <paramref name="openedAt"/> is each opening's time by id),
        /// <c>eaten</c>, <c>eatenNone</c> and <c>eatenByOpening</c> (joules by opening id, 0 to <paramref name="patches"/> - 1). Without one the
        /// row has none of them and is the row it always was.
        /// </remarks>
        public static string Row(RestepRequest q, RestepMember m, PatchTrack k, int patches, TravelTrack travel = null, IReadOnlyList<double> openedAt = null)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            string Num(double v) => double.IsNaN(v) ? "null" : v.ToString("R", inv);
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("episode", q.Episode);
            w.Field("brain", q.Brain);
            w.Field("tank", q.Tank);
            w.Field("slot", m.Slot);
            w.Field("pool", m.PoolIndex);
            w.Field("body", m.Body);
            w.Field("patches", patches);
            w.Field("window", k.WindowSeconds);
            if (k.StartedInScent.HasValue) w.Field("startedInScent", k.StartedInScent.Value); else w.Raw("startedInScent", "null");
            w.Field("died", k.Died);
            w.Raw("alive", Num(k.AliveSeconds));
            w.Raw("scent", Num(k.ScentSeconds));
            w.Raw("scentShare", Num(k.AliveSeconds > 0d ? k.ScentSeconds / k.AliveSeconds : double.NaN));
            w.Raw("firstScent", Num(k.FirstScentSeconds));
            w.Field("exits", k.Exits);
            w.Field("turnBacks", k.TurnBacks);
            w.Field("lateReturns", k.LateReturns);
            w.Field("transits", k.Transits);
            w.Field("unended", k.Unended);
            w.Raw("perPatch", "[" + string.Join(",", Enumerable.Range(0, patches).Select(p =>
                "{\"patch\":" + p.ToString(inv) + ",\"seconds\":" + Num(k.PatchSeconds.TryGetValue(p, out double s) ? s : 0d) +
                ",\"entries\":" + (k.Entries.TryGetValue(p, out int e) ? e : 0).ToString(inv) + "}")) + "]");
            w.Raw("transitLog", "[" + string.Join(",", k.TransitLog.Select(x =>
                "{\"t\":" + Num(x.t) + ",\"from\":" + x.from.ToString(inv) + ",\"to\":" + x.to.ToString(inv) + "}")) + "]");
            if (travel != null)
            {
                w.Field("switches", travel.Switches.Count);
                w.Field("aliveAtSwitch", travel.AliveAtSwitches);
                w.Raw("arrivals", "[" + string.Join(",", travel.Switches.Select(x => Num(x.ArrivalSeconds))) + "]");
                w.Raw("follow", "[" + string.Join(",", travel.Switches.Select(x => Num(x.FollowSeconds))) + "]");
                if (travel.FirstPatch >= 0) w.Field("firstPatch", travel.FirstPatch); else w.Raw("firstPatch", "null");
                w.Raw("laterShare", Num(travel.LaterShare(openedAt ?? new double[0])));
                w.Raw("eaten", Num(travel.EatenTotal));
                w.Raw("eatenNone", Num(travel.EatenNone));
                w.Raw("eatenByOpening", "[" + string.Join(",", Enumerable.Range(0, patches).Select(p =>
                    Num(travel.EatenByOpening.TryGetValue(p, out double joules) ? joules : 0d))) + "]");
            }
            w.EndObject();
            return w.ToString();
        }
    }
}
