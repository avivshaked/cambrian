using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// D236's switching patches (<c>leaves.patches.switching</c>): one opening of the leaf tank, a disc of leaves at an angle on the ring.
    /// Openings 0 and 1 are the two discs the tank is laid with at t = 0; each switch adds one. A time that has not come (or never will) is NaN.
    /// Read-only to everything but the switcher that keeps it.
    /// </summary>
    public sealed class LeafOpening
    {
        /// <summary>0 and 1 for the t = 0 discs, then one more at each switch, in order.</summary>
        public int Id { get; }

        /// <summary>Which of the two discs it is: 0 or 1. A switch opens the disc that is about to be fed.</summary>
        public int Disc { get; }

        /// <summary>The ring's angle at its centre, radians in [0, 2 pi): the centre is the tank's axis plus the ring's radius at this angle.</summary>
        public double Angle { get; }

        public double CentreX { get; }
        public double CentreZ { get; }

        /// <summary>When it opened: 0 for the t = 0 discs, the switch's time for the others.</summary>
        public double OpenedAt { get; }

        /// <summary>When it began to be fed (0 for the disc fed first, the switch's time for a later opening). NaN for the t = 0 disc that is never fed.</summary>
        public double FedFrom { get; internal set; } = double.NaN;

        /// <summary>When it stopped being fed, which is when it began to wither; NaN while it is still fed, and for the disc never fed.</summary>
        public double FedUntil { get; internal set; } = double.NaN;

        /// <summary>When its leaves began to wither (the switch that ended it, or the switch after the first for the disc never fed); NaN before.</summary>
        public double WitheringFrom { get; internal set; } = double.NaN;

        internal LeafOpening(int id, int disc, double angle, double centreX, double centreZ, double openedAt)
        {
            Id = id; Disc = disc; Angle = angle; CentreX = centreX; CentreZ = centreZ; OpenedAt = openedAt;
        }
    }

    /// <summary>One switch of the plan: when, which disc is fed from then on, and where its fresh complement stands.</summary>
    public sealed class LeafSwitch
    {
        /// <summary>1 for the first switch, at one period.</summary>
        public int Index { get; }

        public double Time { get; }

        /// <summary>The disc (0 or 1) fed from this switch on: the one that was not fed.</summary>
        public int Disc { get; }

        /// <summary>The new opening's ring angle, radians in [0, 2 pi).</summary>
        public double Angle { get; }

        /// <summary>The angle turned from the outgoing disc's, degrees, from 0 to 180 (the shorter way round).</summary>
        public double TurnDegrees { get; }

        public double CentreX { get; }
        public double CentreZ { get; }

        /// <summary>The distance, metres, between the new opening's centre and the outgoing disc's.</summary>
        public double CentreDistance { get; }

        internal LeafSwitch(int index, double time, int disc, double angle, double turnDegrees, double centreX, double centreZ, double centreDistance)
        {
            Index = index; Time = time; Disc = disc; Angle = angle; TurnDegrees = turnDegrees; CentreX = centreX; CentreZ = centreZ; CentreDistance = centreDistance;
        }
    }

    /// <summary>
    /// The whole switch plan of one episode, drawn up front from one stream of its own (the first fed disc, then each switch's new angle in
    /// order), so that it follows the episode seed and the recipe alone. With the field absent nothing calls it and nothing is drawn.
    /// </summary>
    public sealed class LeafSwitchPlan
    {
        /// <summary>The salt of the plan's stream, apart from the leaf stream (0x6C656166) and the tank stream (0x74616E6B).</summary>
        public const ulong Salt = 0x73776974UL;

        /// <summary>The disc (0 or 1) fed first.</summary>
        public int FirstFedDisc { get; private set; }

        public IReadOnlyList<LeafSwitch> Switches { get; private set; }

        /// <summary>
        /// Draws the plan. <paramref name="turn"/> is the t = 0 ring's turn (disc 0 stands at it, disc 1 half a circle on), <paramref name="ring"/>
        /// the ring's radius and <paramref name="tank"/> the tank's, whose axis is at (tank, tank). Switches fall at one period, two periods and
        /// so on, while strictly before <paramref name="episodeSeconds"/>. A new angle is drawn uniformly from the arc at least the minimum turn
        /// from the outgoing disc's angle, on either side.
        /// </summary>
        public static LeafSwitchPlan Draw(ulong episodeSeed, Recipe.SwitchingSetting sw, double ring, double tank, double turn, double episodeSeconds)
        {
            var rng = new Rng(Rng.SeedFor(episodeSeed, Salt));
            int first = rng.Range(2);
            var list = new List<LeafSwitch>();
            int fedDisc = first;
            double fedAngle = turn + Math.PI * first;
            for (int k = 1; k * sw.PeriodSeconds < episodeSeconds; k++)
            {
                double u = rng.NextFloat();
                double delta = sw.MinimumTurnDegrees + (360d - 2d * sw.MinimumTurnDegrees) * u;
                double angle = Wrap(fedAngle + delta * Math.PI / 180d);
                double turned = delta <= 180d ? delta : 360d - delta;
                double cx = tank + ring * Math.Cos(angle), cz = tank + ring * Math.Sin(angle);
                double ox = tank + ring * Math.Cos(fedAngle), oz = tank + ring * Math.Sin(fedAngle);
                list.Add(new LeafSwitch(k, k * sw.PeriodSeconds, 1 - fedDisc, angle, turned, cx, cz, Math.Sqrt((cx - ox) * (cx - ox) + (cz - oz) * (cz - oz))));
                fedAngle = angle;
                fedDisc = 1 - fedDisc;
            }
            return new LeafSwitchPlan { FirstFedDisc = first, Switches = list };
        }

        private static double Wrap(double angle)
        {
            double full = 2d * Math.PI;
            angle -= full * Math.Floor(angle / full);
            return angle;
        }
    }

    /// <summary>
    /// The schedule of one switching episode, for the watcher (exposed read-only through <c>EpisodeProbe.Schedule</c>): every opening so far, and
    /// every leaf's opening. The openings are appended as the switches happen, so the schedule is the episode as far as it has got; the whole plan
    /// is <see cref="Plan"/>. Nothing here is written by anyone but the episode's switcher.
    /// </summary>
    public sealed class LeafSchedule
    {
        private readonly List<LeafOpening> _openings = new List<LeafOpening>();
        private readonly Dictionary<long, int> _opening = new Dictionary<long, int>();

        public LeafSwitchPlan Plan { get; }

        /// <summary>Every opening so far, by id: the two t = 0 discs, then one a switch.</summary>
        public IReadOnlyList<LeafOpening> Openings => _openings;

        /// <summary>Replacement leaves laid by the fed disc's regrowth, so far.</summary>
        public int Regrown { get; internal set; }

        /// <summary>Leaves laid in fresh complements, so far.</summary>
        public int ComplementLeavesLaid { get; internal set; }

        /// <summary>The times a leaf due to be laid found no place and was tried again at the next economy step.</summary>
        public int PlacementDelays { get; internal set; }

        internal LeafSchedule(LeafSwitchPlan plan) => Plan = plan;

        /// <summary>The id of the opening a leaf belongs to, or -1 for a body that is not one of the tank's leaves.</summary>
        public int OpeningOf(long leafId) => _opening.TryGetValue(leafId, out int o) ? o : -1;

        /// <summary>The opening fed at <paramref name="seconds"/> as the schedule stands (the latest opening fed from then or before), or null before any.</summary>
        public LeafOpening FedAt(double seconds)
        {
            LeafOpening fed = null;
            foreach (LeafOpening o in _openings)
                if (!double.IsNaN(o.FedFrom) && o.FedFrom <= seconds && (double.IsNaN(o.FedUntil) || seconds < o.FedUntil)) fed = o;
            return fed;
        }

        internal LeafOpening Open(int disc, double angle, double cx, double cz, double openedAt)
        {
            var o = new LeafOpening(_openings.Count, disc, angle, cx, cz, openedAt);
            _openings.Add(o);
            return o;
        }

        internal void Assign(long leafId, int opening) => _opening[leafId] = opening;
    }

    /// <summary>
    /// D236's rules for the switching patches, kept for one episode: the fed disc regrows, the other holds still, and at each switch both old
    /// openings wither and a fresh complement is laid for the disc now fed. Called by the episode once an economy step (<see cref="AfterEconomy"/>),
    /// between steps, where the world takes births and deaths from outside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Streams.</b> Three, each salted apart from the leaf and tank streams: the plan (<see cref="LeafSwitchPlan"/>), each withering leaf's
    /// second (a stream seeded by the episode seed and the leaf's id, so it does not depend on order), and the regrowth stream, which draws a
    /// replacement's row and then its places, and a fresh complement's rows and then its places, in order of need.
    /// </para>
    /// <para>
    /// <b>Rows.</b> A replacement's row is drawn uniformly from the whole pool, a fresh complement's from the pool without repeating a row within
    /// it, both redrawn (by the same stream) while the leaf's bounding radius is more than the discs'. A complement is laid largest first, as
    /// the t = 0 deal is.
    /// </para>
    /// <para>
    /// <b>Places.</b> As LayLeaves has them: uniform over the opening's disc less the leaf's radius, at the leaf's recorded height kept in the
    /// water, paid the growth it owes. A leaf that finds no place in 64 draws is tried again at the next economy step, and the schedule counts
    /// the delay.
    /// </para>
    /// <para>
    /// <b>Time.</b> Everything is read at economy steps (every half second), the world's clock after the step. A leaf that left the living is
    /// seen at the step it left in, and replaced a regrowth time after that; a withering leaf dies at the first step at or after its second.
    /// </para>
    /// </remarks>
    public sealed class LeafSwitcher
    {
        private const ulong WitherSalt = 0x77697468UL;
        private const ulong RegrowSalt = 0x72656772UL;
        private const int LeafAttempts = 64;
        private const int RowAttempts = 1000;

        private sealed class Pending
        {
            public double Due;
            public int Opening;
            public int Row = -1;
        }

        private readonly Recipe.PatchSetting _patches;
        private readonly Recipe.SwitchingSetting _sw;
        private readonly IReadOnlyList<LeafPool.Leaf> _pool;
        private readonly RunConfig _config;
        private readonly ulong _seed;
        private readonly PlacementFloor _floor;
        private readonly Rng _regrow;
        private readonly int[] _dealt;
        private readonly List<List<Organism>> _members = new List<List<Organism>>();
        private readonly int[] _discOpening = new int[2];
        private readonly List<(double at, Organism leaf)> _withers = new List<(double at, Organism leaf)>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly HashSet<long> _alive = new HashSet<long>();
        private readonly HashSet<long> _gone = new HashSet<long>();
        private readonly Dictionary<int, (Phenotype adult, float radius)> _shape = new Dictionary<int, (Phenotype adult, float radius)>();
        private int _fed;
        private int _next;

        /// <summary>The episode's schedule, handed to the watcher.</summary>
        public LeafSchedule Schedule { get; }

        /// <param name="dealt">The leaves each disc was dealt at t = 0 (the count a fresh complement for it holds).</param>
        /// <param name="turn">The t = 0 ring's turn, radians: disc 0 stands at it.</param>
        /// <param name="floor">The solver's placement floor (<c>Simulation.Floor</c>) or null: where the water is too shallow for a leaf.</param>
        public LeafSwitcher(Recipe.PatchSetting patches, LeafPool pool, RunConfig config, ulong seed, float tankRadius, double episodeSeconds,
            double turn, int[] dealt, PlacementFloor floor)
        {
            if (patches == null || patches.Switching == null) throw new ArgumentException("the patches are not switching", nameof(patches));
            if (patches.Count != 2) throw new ArgumentException("switching patches are two", nameof(patches));
            _patches = patches;
            _sw = patches.Switching;
            _pool = pool.Leaves;
            _config = config;
            _seed = seed;
            _floor = floor;
            _dealt = dealt;
            _regrow = new Rng(Rng.SeedFor(seed, RegrowSalt));
            Schedule = new LeafSchedule(LeafSwitchPlan.Draw(seed, _sw, patches.RingMetres, tankRadius, turn, episodeSeconds));
            TankRadius = tankRadius;

            for (int d = 0; d < 2; d++)
            {
                (double cx, double cz) = LeafPatches.Centre(tankRadius, patches.RingMetres, 2, turn, d);
                Schedule.Open(d, Wrap(turn + Math.PI * d), cx, cz, 0d);
                _members.Add(new List<Organism>());
                _discOpening[d] = d;
            }
            _fed = Schedule.Plan.FirstFedDisc;
            Schedule.Openings[_fed].FedFrom = 0d;
        }

        public float TankRadius { get; }

        private static double Wrap(double angle)
        {
            double full = 2d * Math.PI;
            return angle - full * Math.Floor(angle / full);
        }

        /// <summary>
        /// Registers the leaves LayLeaves laid at t = 0 (in the order it laid them) with their discs: <paramref name="placed"/> carries each
        /// leaf's disc as <c>patch</c>.
        /// </summary>
        public void Adopt(IReadOnlyList<Organism> leaves, IReadOnlyList<(float x, float z, float radius, int patch)> placed)
        {
            for (int i = 0; i < leaves.Count; i++)
            {
                int disc = placed[i].patch;
                _members[disc].Add(leaves[i]);
                Schedule.Assign(leaves[i].Id, disc);
            }
        }

        /// <summary>The leaf each disc holds at t = 0 is counted from the laid layout: the count a fresh complement holds.</summary>
        public static int[] DealtOf(IReadOnlyList<(float x, float z, float radius, int patch)> placed)
        {
            var dealt = new int[2];
            foreach (var l in placed) dealt[l.patch]++;
            return dealt;
        }

        // ------------------------------------------------------------------ once an economy step

        /// <summary>
        /// The switching tank's whole turn at one economy step, after the world has taken it: the switches that have come, the withers that have
        /// come, the fed disc's leaves that left the living (a replacement queued for each, a regrowth time on), and the leaves due to be laid.
        /// </summary>
        public void AfterEconomy(World world)
        {
            double t = world.ElapsedSeconds;
            _alive.Clear();
            foreach (Organism o in world.Living) _alive.Add(o.Id);

            IReadOnlyList<LeafSwitch> switches = Schedule.Plan.Switches;
            while (_next < switches.Count && t >= switches[_next].Time) Switch(world, switches[_next++]);

            int w = 0;
            while (w < _withers.Count && _withers[w].at <= t)
            {
                Organism leaf = _withers[w].leaf;
                w++;
                if (!_alive.Remove(leaf.Id)) continue;
                world.WitherForTank(leaf);
            }
            _withers.RemoveRange(0, w);

            foreach (Organism m in _members[_fed])
                if (!_alive.Contains(m.Id) && _gone.Add(m.Id))
                    _pending.Add(new Pending { Due = t + _sw.RegrowAfterSeconds, Opening = _fed });

            for (int i = 0; i < _pending.Count;)
            {
                Pending p = _pending[i];
                if (p.Due > t) { i++; continue; }
                if (p.Row < 0) p.Row = DrawRow(null);
                if (!LayOne(world, p.Opening, p.Row, regrowth: !_complement.Contains(p)))
                {
                    Schedule.PlacementDelays++;
                    i++;
                    continue;
                }
                _complement.Remove(p);
                _pending.RemoveAt(i);
            }
        }

        // A pending entry laid by a switch is a complement leaf, counted apart from a regrowth.
        private readonly HashSet<Pending> _complement = new HashSet<Pending>();

        private void Switch(World world, LeafSwitch sw)
        {
            int outgoing = _fed;
            int disc = sw.Disc;
            int other = _discOpening[disc];

            // A pending regrowth (or complement leaf not yet laid) of the outgoing disc is cancelled.
            _pending.Clear();
            _complement.Clear();

            LeafOpening og = Schedule.Openings[outgoing];
            og.FedUntil = sw.Time;
            if (double.IsNaN(og.WitheringFrom)) og.WitheringFrom = sw.Time;
            LeafOpening ot = Schedule.Openings[other];
            if (double.IsNaN(ot.WitheringFrom)) ot.WitheringFrom = sw.Time;

            ScheduleWithers(outgoing, sw.Time);
            ScheduleWithers(other, sw.Time);

            LeafOpening nw = Schedule.Open(disc, sw.Angle, sw.CentreX, sw.CentreZ, sw.Time);
            nw.FedFrom = sw.Time;
            _members.Add(new List<Organism>());
            _discOpening[disc] = nw.Id;
            _fed = nw.Id;

            foreach (int row in ComplementRows(_dealt[disc]))
            {
                var p = new Pending { Due = sw.Time, Opening = nw.Id, Row = row };
                _pending.Add(p);
                _complement.Add(p);
            }
        }

        private void ScheduleWithers(int opening, double switchTime)
        {
            ulong stream = Rng.SeedFor(_seed, WitherSalt);
            int before = _withers.Count;
            foreach (Organism m in _members[opening])
            {
                if (!_alive.Contains(m.Id)) continue;
                float u = new Rng(Rng.SeedFor(stream, (ulong)m.Id)).NextFloat();
                _withers.Add((switchTime + u * _sw.WitherOverSeconds, m));
            }
            if (_withers.Count > before)
                _withers.Sort((a, b) => a.at != b.at ? a.at.CompareTo(b.at) : a.leaf.Id.CompareTo(b.leaf.Id));
        }

        // ------------------------------------------------------------------ rows and places

        private (Phenotype adult, float radius) ShapeOf(int row)
        {
            if (_shape.TryGetValue(row, out var s)) return s;
            Phenotype adult = Developer.Develop(_pool[row].Genome, _config.Development, null, _config.Shapes, cellTypes: _config.CellTypes);
            s = (adult, SharedVolume.BoundingRadius(adult));
            _shape[row] = s;
            return s;
        }

        /// <summary>A pool row from the regrowth stream, not in <paramref name="not"/>, whose leaf fits the discs.</summary>
        private int DrawRow(HashSet<int> not)
        {
            for (int a = 0; a < RowAttempts; a++)
            {
                int row = _regrow.Range(_pool.Count);
                if (not != null && not.Contains(row)) continue;
                if (ShapeOf(row).radius > _patches.RadiusMetres) continue;
                return row;
            }
            throw new InvalidDataException(FormattableString.Invariant(
                $"the switching tank drew {RowAttempts} rows of {_pool.Count} and none fits a disc of {_patches.RadiusMetres} m radius"));
        }

        /// <summary>A fresh complement's rows: <paramref name="count"/> distinct, drawn in order and then laid largest first.</summary>
        private List<int> ComplementRows(int count)
        {
            var seen = new HashSet<int>();
            var drawn = new List<(int row, float radius, int k)>();
            for (int k = 0; k < count; k++)
            {
                int row = DrawRow(seen);
                seen.Add(row);
                drawn.Add((row, ShapeOf(row).radius, k));
            }
            drawn.Sort((a, b) => a.radius != b.radius ? b.radius.CompareTo(a.radius) : a.k.CompareTo(b.k));
            return drawn.Select(d => d.row).ToList();
        }

        private bool LayOne(World world, int opening, int row, bool regrowth)
        {
            LeafOpening at = Schedule.Openings[opening];
            Organism o = Place(world, at.CentreX, at.CentreZ, row);
            if (o == null) return false;
            _members[opening].Add(o);
            Schedule.Assign(o.Id, opening);
            _alive.Add(o.Id);
            if (regrowth) Schedule.Regrown++; else Schedule.ComplementLeavesLaid++;
            return true;
        }

        /// <summary>
        /// One leaf of pool row <paramref name="row"/> laid in the disc about (<paramref name="cx"/>, <paramref name="cz"/>) by LayLeaves' rules
        /// (the same draws of the same kind, from the regrowth stream): up to 64 places, the first the world admits; null when none is. Paid the
        /// growth it owes, so that it starts grown.
        /// </summary>
        private Organism Place(World world, double cx, double cz, int row)
        {
            LeafPool.Leaf leaf = _pool[row];
            float radius = ShapeOf(row).radius;
            float depth = _config.WorldDepthMetres;
            Organism o = null;
            for (int attempt = 0; attempt < LeafAttempts && o == null; attempt++)
            {
                double r = (_patches.RadiusMetres - radius) * Math.Sqrt(_regrow.NextFloat()), a = 2d * Math.PI * _regrow.NextFloat();
                float x = (float)(cx + r * Math.Cos(a));
                float z = (float)(cz + r * Math.Sin(a));

                float lowest = _floor != null ? _floor.MinimumPlacementY(x, z, radius) : -depth + radius;
                if (lowest > -radius)
                    throw new InvalidDataException(FormattableString.Invariant(
                        $"leaf {leaf.Id} has a radius of {radius} m, and the water at ({x}, {z}) is too shallow to hold it"));
                float y = Math.Min(-radius, Math.Max(lowest, leaf.HeightY));

                try { o = world.InoculateAt(leaf.Genome, new Float3(x, y, z)); }
                catch (ArgumentException e) when (e.ParamName == "point") { }
                catch (ArgumentException e)
                {
                    throw new InvalidDataException($"leaf {leaf.Id} is refused by the world: {e.Message}", e);
                }
            }
            if (o == null) return null;

            // Under the growth climb (D241) the owed tissue as reserve would buy a climb, not an adult, so the leaf is made adult outright and owes nothing.
            if (_config.GrowthAdultSeconds > 0f) world.MakeAdult(o);
            double owed = Math.Max(0d, Metabolism.TissueJoules(o.AdultPhenotype, _config) - o.TissueJoules);
            if (owed > 0d) world.GiveReserve(o, owed);
            return o;
        }

        // ------------------------------------------------------------------ --check

        /// <summary>
        /// <c>--check</c>: lays switch <paramref name="sw"/>'s whole fresh complement into <paramref name="world"/>, a world built for the check
        /// (an empty tank), by the rules above, and returns how many of its leaves found no place in 64 draws. Registers nothing.
        /// </summary>
        public int LayComplementForCheck(World world, LeafSwitch sw)
        {
            int refused = 0;
            foreach (int row in ComplementRows(_dealt[sw.Disc]))
                if (Place(world, sw.CentreX, sw.CentreZ, row) == null) refused++;
            return refused;
        }

        /// <summary>The plan as <c>--check</c> prints it for one seed.</summary>
        public static string Describe(string name, LeafSwitchPlan plan)
        {
            var parts = plan.Switches.Select(s => FormattableString.Invariant(
                $"{s.Time:0} s: angle {s.Angle * 180d / Math.PI:0.0} deg, centre ({s.CentreX:0.0}, {s.CentreZ:0.0}), turned {s.TurnDegrees:0.0} deg, centres {s.CentreDistance:0.0} m apart"));
            return FormattableString.Invariant(
                $"  {name}: first fed disc {plan.FirstFedDisc}; {plan.Switches.Count} switches{(plan.Switches.Count == 0 ? "" : ": " + string.Join("; ", parts))}");
        }
    }

    internal static partial class Program
    {
        /// <summary>The recipe's switching patches (D236), or null when its patches are fixed (or it has none); the whole feature is behind this.</summary>
        private static Recipe.SwitchingSetting LeafSwitchingSet => _leafPool == null ? null : _recipe?.Leaves?.Patches?.Switching;

        /// <summary>The episode's switcher, with the leaves LayLeaves has just laid adopted.</summary>
        private static LeafSwitcher NewSwitcher(World world, Simulation sim, ulong seed, Arena arena, List<Organism> leaves, LeafPatches.Layout laid)
        {
            var switcher = new LeafSwitcher(
                _recipe.Leaves.Patches, _leafPool, arena.Config, seed, world.TankRadiusMetres, arena.Seconds, laid.Turn,
                LeafSwitcher.DealtOf(laid.Leaves), sim.Floor);
            switcher.Adopt(leaves, laid.Leaves);
            return switcher;
        }
    }
}
