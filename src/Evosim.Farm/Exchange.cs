using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// D159's exchange between the worlds of one <c>--group</c> (round 58): every
    /// <see cref="Every"/> simulated seconds each world sends <see cref="Count"/> copies of living
    /// genomes to the next world in the group's order, the last to the first, and plants the ones
    /// the world before it sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two phases, each closed by a barrier, at a second taken from the clock.</b> At an
    /// exchange each world first picks from its own living and leaves the copies in its slot; when
    /// every world has picked, each plants what the world before it left. No world reads another
    /// while that one is changing, so what crosses depends on the worlds' states at the second and
    /// never on which thread got there first. The second barrier keeps a slot from being written
    /// for the next exchange while it is still being read.
    /// </para>
    /// <para>
    /// <b>The pick is random among the living, from a stream of its own for each exchange</b>:
    /// <c>Rng.SeedFor(Rng.SeedFor(world seed, PickStream), k)</c> for the k-th exchange, with k the
    /// exchange's second over <see cref="Every"/>. Nothing in the checkpoint carries it, and a
    /// world resumed at any second picks what the unbroken world picked. The world's own streams
    /// are not drawn by the pick; planting draws the receiving world's own stream, as a founder does
    /// (<see cref="World.Immigrate"/>).
    /// </para>
    /// <para>
    /// <b>Any ending but the budget ends the whole group.</b> A world that leaves its loop says
    /// why (<see cref="Leave"/>); for any reason but the budget the group is marked ended, and the
    /// others stop at their next metabolic step. A world that leaves also leaves the barrier, so a
    /// world waiting at an exchange is released rather than held for ever.
    /// </para>
    /// </remarks>
    internal sealed class GroupExchange
    {
        /// <summary>"MIGR": the stream the picks are drawn from, beside the world's own.</summary>
        internal const ulong PickStream = 0x4D494752UL;

        /// <summary>One genome on its way: a copy, the id it had at home, and its generation depth.</summary>
        internal readonly struct Migrant
        {
            public Migrant(Genome genome, long id, int generationDepth)
            {
                Genome = genome;
                Id = id;
                GenerationDepth = generationDepth;
            }

            public Genome Genome { get; }
            public long Id { get; }
            public int GenerationDepth { get; }
        }

        private readonly string[] _names;
        private readonly Barrier _barrier;
        private readonly Migrant[][] _slots;
        private readonly double[] _seconds;
        private readonly long[] _index;
        private readonly bool[] _left;
        private readonly object _gate = new object();
        private volatile string _endedBy;

        /// <param name="names">Each world's name, in the group's order (its report's file name).</param>
        /// <param name="every">Simulated seconds between exchanges, above 0.</param>
        /// <param name="count">Genomes each world sends at an exchange, 1 or more.</param>
        public GroupExchange(IReadOnlyList<string> names, double every, int count)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (names.Count < 2) throw new ArgumentException("An exchange needs two worlds or more.", nameof(names));
            if (!(every > 0d) || double.IsInfinity(every)) throw new ArgumentOutOfRangeException(nameof(every), every, "Seconds between exchanges, above 0.");
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Genomes sent at an exchange, 1 or more.");

            _names = new string[names.Count];
            for (int i = 0; i < _names.Length; i++) _names[i] = names[i];

            Every = every;
            Count = count;

            _barrier = new Barrier(_names.Length);
            _slots = new Migrant[_names.Length][];
            _seconds = new double[_names.Length];
            _index = new long[_names.Length];
            _left = new bool[_names.Length];
        }

        public double Every { get; }
        public int Count { get; }
        public int Worlds => _names.Length;

        /// <summary>The name of the world at <paramref name="place"/>.</summary>
        public string Name(int place) => _names[place];

        /// <summary>The place of the world that <paramref name="place"/> sends to.</summary>
        public int To(int place) => (place + 1) % _names.Length;

        /// <summary>The place of the world that <paramref name="place"/> receives from.</summary>
        public int From(int place) => (place + _names.Length - 1) % _names.Length;

        /// <summary>Null while the group runs; once it has ended, which world ended it and why.</summary>
        public string EndedBy => _endedBy;

        /// <summary>
        /// The first exchange second after <paramref name="seconds"/>: the next whole multiple of
        /// <see cref="Every"/>, by the clock, so a resumed world's exchanges land on the seconds the
        /// unbroken world's did. A clock a hair under an exchange second it has already passed
        /// (the loop takes an exchange at <c>t + 1e-9 &gt;= T</c>) counts as at it, so a world
        /// resumed there does not take the same exchange twice.
        /// </summary>
        public double NextAfter(double seconds) => (Math.Floor((seconds + 1e-9) / Every) + 1d) * Every;

        /// <summary>
        /// The exchange due at <paramref name="at"/>, for the world at <paramref name="place"/>:
        /// pick, wait for the group, plant, wait again.
        /// </summary>
        /// <param name="planted">
        /// Called for each genome planted, in order, with the place it came from, the migrant and
        /// the id it was given here (-1 when the world refused it): the world's
        /// <see cref="ImmigrantRecord"/> row. Null records nothing.
        /// </param>
        /// <returns>False when the group ended while this world waited, and the world should stop.</returns>
        /// <exception cref="InvalidOperationException">
        /// The worlds reached the exchange at different seconds or for different exchanges: their
        /// metabolic steps differ, so their clocks do not meet, and the group is refused.
        /// </exception>
        public bool Run(int place, World world, double at, Action<int, Migrant, long> planted = null)
        {
            long k = (long)Math.Round(at / Every);

            _slots[place] = Pick(world, k, Count);
            _seconds[place] = world.ElapsedSeconds;
            _index[place] = k;

            if (!Arrive()) return false;

            for (int i = 0; i < _names.Length; i++)
            {
                if (_index[i] == k && _seconds[i] == world.ElapsedSeconds) continue;

                throw new InvalidOperationException(
                    "D159's exchange " + k.ToString(CultureInfo.InvariantCulture) + " found " + _names[place] +
                    " at " + world.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture) + " s and " + _names[i] +
                    " at exchange " + _index[i].ToString(CultureInfo.InvariantCulture) + ", " +
                    _seconds[i].ToString("R", CultureInfo.InvariantCulture) + " s. The worlds of a migrating group " +
                    "step the same metabolic step from the same second, or their exchanges do not meet.");
            }

            int from = From(place);
            Migrant[] arriving = _slots[from];
            for (int i = 0; i < arriving.Length; i++)
            {
                long id = world.Immigrate(arriving[i].Genome, from, arriving[i].Id, arriving[i].GenerationDepth);
                planted?.Invoke(from, arriving[i], id);
            }

            return Arrive();
        }

        /// <summary>
        /// The world at <paramref name="place"/> has left its loop, for <paramref name="reason"/>
        /// (the manifest's termination code; null for an exception). Any reason but the budget ends
        /// the group. Said once; a second call is ignored.
        /// </summary>
        public void Leave(int place, string reason)
        {
            lock (_gate)
            {
                if (_left[place]) return;
                _left[place] = true;

                if (reason != "budget" && _endedBy == null)
                {
                    _endedBy = _names[place] + " ended (" + (reason ?? "error") + ")";
                }
            }

            // After the mark, so a world released by this sees it.
            _barrier.RemoveParticipant();
        }

        /// <summary>
        /// The <paramref name="count"/> genomes a world sends at exchange <paramref name="k"/>:
        /// distinct living bodies drawn at random from the exchange's own stream (all of them, in
        /// a drawn order, when fewer are alive), each genome copied.
        /// </summary>
        internal static Migrant[] Pick(World world, long k, int count)
        {
            IReadOnlyList<Organism> living = world.Living;
            int n = living.Count;
            int take = Math.Min(count, n);

            var picked = new Migrant[take];
            if (take == 0) return picked;

            var rng = new Rng(Rng.SeedFor(Rng.SeedFor(world.Seed, PickStream), (ulong)k));

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;

            for (int i = 0; i < take; i++)
            {
                int j = i + rng.Range(n - i);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;

                Organism o = living[order[i]];
                picked[i] = new Migrant(o.Genome.Clone(), o.Id, o.GenerationDepth);
            }

            return picked;
        }

        private bool Arrive()
        {
            _barrier.SignalAndWait();
            return _endedBy == null;
        }
    }

    /// <summary>
    /// A migrating world's own record of every genome it was sent (D159), <c>immigrants.jsonl</c>
    /// in its run directory: one row an offer, in the order planted, with the genome as it came, so
    /// that the world can be stepped again without the worlds it traded with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every offer and not only every immigrant.</b> A refused offer writes no lineage row, yet
    /// it draws a seed from the receiving world's own stream as a planted one does
    /// (<see cref="World.Immigrate"/>), so a window stepping the world again has to make every
    /// offer the run made, refusals included, or it parts from the run at the first refusal.
    /// </para>
    /// <para>
    /// A row: <c>t</c>, the world's second at the exchange; <c>from</c>, the sending world's place
    /// in the group; <c>fid</c> and <c>g</c>, the creature's id there and its generation depth;
    /// <c>id</c>, the id it was planted under here, or -1 when it was refused; on a planted row
    /// only, <c>guild</c>, the planted body's flags as <c>positions.jsonl</c> writes a body's
    /// (<see cref="PositionsRow.AbsorptiveBit"/>, <see cref="PositionsRow.JointedBit"/>,
    /// <see cref="PositionsRow.PhotosyntheticBit"/>, by <see cref="GuildOf"/>), taken as it was
    /// planted; on a refused row only, <c>why</c>, the founder's refusal it met (<c>floor</c>,
    /// <c>room</c> or <c>stillborn</c>); and <c>genome</c>, the genome as sent, which
    /// <see cref="GenomeJson"/> carries to the bit, last.
    /// </para>
    /// </remarks>
    internal static class ImmigrantRecord
    {
        public const string FileName = "immigrants.jsonl";

        /// <summary>One row, compact.</summary>
        /// <param name="why">
        /// Why a refused offer was refused (<see cref="World.LastImmigrantRefusal"/>), written as
        /// <c>why</c> on a refused row only, before the genome: <c>floor</c>, <c>room</c> or
        /// <c>stillborn</c>.
        /// </param>
        /// <param name="guild">
        /// The planted body's flags (<see cref="GuildOf"/>), written as <c>guild</c> on a planted row
        /// only, which must carry them; ignored on a refused one, which has no body.
        /// </param>
        public static string Row(
            double t, int from, GroupExchange.Migrant migrant, long id, ImmigrantRefusal why = ImmigrantRefusal.None,
            int guild = -1)
        {
            if (id >= 0 && (guild < 0 || (guild & ~PositionsRow.AllBits) != 0))
            {
                throw new ArgumentOutOfRangeException(nameof(guild), guild,
                    "A planted offer's row carries its body's flags, " + PositionsRow.AllBits.ToString(CultureInfo.InvariantCulture) + " or under.");
            }

            var w = new Json.Writer(indent: false);
            w.BeginObject()
                .Field("t", t)
                .Field("from", from)
                .Field("fid", migrant.Id)
                .Field("g", migrant.GenerationDepth)
                .Field("id", id);
            if (id >= 0) w.Field("guild", guild);
            if (id < 0) w.Field("why", Why(why));
            w.Raw("genome", GenomeJson.Write(migrant.Genome, indent: false))
                .EndObject();
            return w.ToString();
        }

        /// <summary>
        /// The guild of the body planted under <paramref name="id"/>, by the tests the report's
        /// sample makes of every living body for <c>positions.jsonl</c> (Row's pass over the living):
        /// absorptive when any developed part is absorptive tissue, jointed when its parts' joints
        /// have a degree of freedom, photosynthetic when it has photosynthetic tissue. Read just after
        /// the planting, so it is the body as planted, at its birth fraction.
        /// </summary>
        public static int GuildOf(World world, long id)
        {
            // A planted body is admitted last, so the walk from the end finds it at once.
            IReadOnlyList<Organism> living = world.Living;
            for (int i = living.Count - 1; i >= 0; i--)
            {
                Organism body = living[i];
                if (body.Id != id) continue;

                bool absorptive = false;
                int dof = 0;
                foreach (PhenotypePart part in body.Phenotype.Parts)
                {
                    if (part.CellTypeId == CellTypeIds.Absorptive) absorptive = true;
                    dof += part.JointType.DofCount();
                }

                return (absorptive ? PositionsRow.AbsorptiveBit : 0) |
                    (dof > 0 ? PositionsRow.JointedBit : 0) |
                    (body.HasPhotosyntheticTissue ? PositionsRow.PhotosyntheticBit : 0);
            }

            throw new InvalidOperationException(
                "Creature " + id.ToString(CultureInfo.InvariantCulture) + " was planted and is not among the living.");
        }

        /// <summary>The record's word for a refusal.</summary>
        public static string Why(ImmigrantRefusal why)
        {
            switch (why)
            {
                case ImmigrantRefusal.UnderMassFloor: return "floor";
                case ImmigrantRefusal.NoRoom: return "room";
                case ImmigrantRefusal.Stillborn: return "stillborn";
                default:
                    throw new ArgumentException("A refused offer carries its reason; None is a planted one.", nameof(why));
            }
        }

        /// <summary>
        /// The run's offers after <paramref name="from"/> seconds, replayed into a world stepped
        /// again: <see cref="PlantDue"/> after each metabolic step makes every offer whose second
        /// has come, in the run's order, and checks each id against the run's.
        /// </summary>
        public sealed class Replay
        {
            private readonly List<(double T, int From, long Fid, int G, long Id, Genome Genome)> _offers =
                new List<(double, int, long, int, long, Genome)>();

            private int _next;

            public Replay(string runDirectory, double from)
            {
                string path = System.IO.Path.Combine(runDirectory, FileName);

                if (!System.IO.File.Exists(path))
                {
                    throw new System.IO.FileNotFoundException(
                        runDirectory + " is a world of a migrating group and has no " + FileName + ". Its " +
                        "immigrants came from the other worlds, and without the record of them it cannot be " +
                        "stepped again.", path);
                }

                foreach (string line in JsonlWriter.ReadRows(path))
                {
                    JsonNode row = Json.Parse(line);
                    double t = row["t"].AsDouble();
                    if (t <= from + 1e-9) continue;

                    // The genome is read from its own text, the row's last field as Row writes it,
                    // so it reaches GenomeJson exactly as GenomeJson wrote it. Ids and depths are
                    // integers well inside a double's 53 bits.
                    const string Key = "\"genome\":";
                    int at = line.IndexOf(Key, StringComparison.Ordinal);
                    if (at < 0 || line[line.Length - 1] != '}')
                    {
                        throw new System.IO.InvalidDataException(path + ": a row without its genome last: " + line);
                    }

                    string genome = line.Substring(at + Key.Length, line.Length - 1 - (at + Key.Length));

                    _offers.Add((
                        t, row["from"].AsInt(), (long)row["fid"].AsDouble(), row["g"].AsInt(),
                        (long)row["id"].AsDouble(), GenomeJson.Read(genome)));
                }
            }

            /// <summary>Offers still to make.</summary>
            public int Pending => _offers.Count - _next;

            /// <summary>
            /// Makes every offer whose second the world has reached, in order.
            /// </summary>
            /// <exception cref="System.IO.InvalidDataException">
            /// An offer was given another id than the run gave it: the world has parted from the run.
            /// </exception>
            public void PlantDue(World world)
            {
                while (_next < _offers.Count && _offers[_next].T <= world.ElapsedSeconds + 1e-9)
                {
                    var o = _offers[_next++];
                    long id = world.Immigrate(o.Genome, o.From, o.Fid, o.G);

                    if (id == o.Id) continue;

                    throw new System.IO.InvalidDataException(
                        "The offer of creature " + o.Fid.ToString(CultureInfo.InvariantCulture) + " from place " +
                        o.From.ToString(CultureInfo.InvariantCulture) + " at " + o.T.ToString("R", CultureInfo.InvariantCulture) +
                        " s was planted as " + id.ToString(CultureInfo.InvariantCulture) + " here and as " +
                        o.Id.ToString(CultureInfo.InvariantCulture) + " in the run. The world has parted from the run.");
                }
            }
        }
    }
}
