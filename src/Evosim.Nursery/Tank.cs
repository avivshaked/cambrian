using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Evosim.Core;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// The tank: fable-propose-world-nursery.md's R1 to R10, as logbook/specs/nursery-plan-joint-drive.md
    /// re-reads them under D135. Nothing here is ruled; it is built so that the pilot is ready when
    /// the owner rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An episode</b> is several genomes inoculated into one small world (the recipe's tank
    /// size of them), each given the grown body's purse (the growth paid in full, and the recipe's
    /// purse seconds of the adult's standing watts on top), with snow laid on the bed (R6, the
    /// recipe's density, drawn half to double per episode unless fixed) and its carrion laid on the
    /// bed at random. Each body's score is read by the recipe's reward (Reward.cs) from its episode
    /// record, the net energy over the episode (R1) among it: reserve, tissue and gestation at the
    /// end less the same at the start, and a body that died ends at 0. Births are held. A recipe with
    /// leaves (LeafTank.cs) lays the water's spent matter and its living leaves first; the leaves are
    /// not members, and nothing here reads them but the world.
    /// </para>
    /// <para>
    /// <b>The loop</b> (R4, R5): a population of the recipe's population size of genomes, shuffled
    /// into tanks of the tank size, its draws times a generation, every tank of a draw on the
    /// draw's seed; a genome's score is the reward over every episode it has swum; the top elites
    /// carry over and swim again among fresh mates; the rest of the next population are children of
    /// parents chosen by a tournament on that score. No crossover unless the recipe's crossover
    /// rate is above 0 (propose-nursery-crossover.md, <see cref="CrossChild"/> and Crossover.cs).
    /// </para>
    /// <para>
    /// <b>What evolves</b> (R3, the recipe's evolve): <c>kind</c> mutates the whole genome and
    /// refuses a child whose set of cell types differs from its parent's; <c>brain</c> keeps the
    /// parent's body and takes the child's neurons, or mutates the one brain on it; with the
    /// recipe's evolveEnvelope it takes the child's joint drive genes too (Envelope.cs), and each
    /// generation row ends with the population's mean envelope over every joint.
    /// </para>
    /// <para>
    /// <b>The bench</b> (a recipe with no search, and the exam at a search's end) swims every
    /// genome under each of the recipe's exam controls, every tank holding one brain: its own,
    /// frozen (power -1), full power with the bend centred, a knockout of some senses, brainless
    /// (every sense reading 0), a constant power, and a hand-wired kinesis. It is the plan's
    /// control: if the hand-wired kinesis does not out-earn full power once the work cost is on, no
    /// evolved brain will.
    /// </para>
    /// </remarks>
    internal static partial class Program
    {
        /// <summary>One body's reading from one tank episode.</summary>
        private sealed class Resident
        {
            public bool Landed, Survived;
            public double Net, Start, End, Food, Eaten, Kills, Killed, Meals, Light, Work, Path, Turn, Fresh, Upkeep, Handling, Exuded, DiedAt = double.NaN;
        }

        private sealed class Member
        {
            public Genome Genome;
            public int Body, Born, Episodes;

            /// <summary>The reward's value over every episode this genome has swum (<see cref="Records"/>); selection ranks by it.</summary>
            public double Score = double.NaN;

            /// <summary>Every episode it has swum, in the order swum: an elite keeps its own across generations.</summary>
            public readonly List<EpisodeRecord> Records = new List<EpisodeRecord>();

            /// <summary>The reward's last verdict, its value and its named parts; null before the first.</summary>
            public Score Scored;

            /// <summary>
            /// D178: this generation's mean of the reward taken over each of its episodes alone, the stop's reading under
            /// <c>search.stop.measure</c> reward; NaN otherwise.
            /// </summary>
            public double GenerationReward = double.NaN;

            /// <summary>This generation's means, for the report.</summary>
            public double Net = double.NaN, Work, Path, Turn, Fresh, Eaten, Kills, Meals, Alive;

            /// <summary>
            /// The member's id in the generation it is swimming in (<see cref="TankLog.MemberId"/>), set at the top of each generation from its place in
            /// the population; the search's tournament and the exam read it, nothing draws from it.
            /// </summary>
            public string Id;

            /// <summary>
            /// The ids, in the generation before, of the members this one was made from: none in generation 0, one for a mutated copy or an elite,
            /// two for a crossover that passed a route (the base parent first). Kept in memory as the children are made, never by drawing.
            /// </summary>
            public string[] Parents = Array.Empty<string>();

            /// <summary>True for an elite carried unchanged into this generation (the same genome and its episodes, under a new place's id).</summary>
            public bool Elite;
        }

        private static string Inv3(FormattableString a, FormattableString b, FormattableString c) =>
            FormattableString.Invariant(a) + FormattableString.Invariant(b) + FormattableString.Invariant(c);

        /// <summary>EVOSIM_TANK_TRACE: every body's start, keep and ending, episode by episode.</summary>
        private static readonly bool _tankTrace = Environment.GetEnvironmentVariable("EVOSIM_TANK_TRACE") == "1";

        private static bool Better(double a, double b) => !double.IsNaN(a) && (double.IsNaN(b) || a > b);

        /// <summary>The run's reward (the recipe's class; <see cref="Net"/> until the recipe is read).</summary>
        private static IReward _reward = new Net();

        /// <summary>The reward's name, for the scores' rows.</summary>
        private static string RewardName => _recipe.RewardClass;

        /// <summary>One body's reading from one episode as the reward reads it.</summary>
        private static EpisodeRecord RecordOf(Resident x, int generation, int draw, ulong seed, string arena, int body, double seconds) =>
            new EpisodeRecord
            {
                Generation = generation, Draw = draw, Seed = seed, Arena = arena, Body = body, Seconds = seconds,
                Start = x.Start, End = x.End, Net = x.Net, Food = x.Food, Eaten = x.Eaten, Kills = x.Kills, Killed = x.Killed, Meals = x.Meals, Light = x.Light, Work = x.Work,
                Upkeep = x.Upkeep, Handling = x.Handling, Exuded = x.Exuded, Path = x.Path, Turn = x.Turn, Fresh = x.Fresh,
                Survived = x.Survived, DiedAt = x.DiedAt,
            };

        /// <summary>One generation's verdicts, member by member in population order: body, birth, episodes, value and parts.</summary>
        private static string ScoresRow(int gen, List<Member> population)
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("gen", gen);
            w.Field("reward", RewardName);
            w.BeginArray("members");
            foreach (Member p in population)
            {
                w.BeginObject();
                w.Field("body", p.Body);
                w.Field("born", p.Born);
                w.Field("episodes", p.Episodes);
                if (p.Scored == null || !double.IsFinite(p.Scored.Value)) w.Raw("value", "null"); else w.Field("value", p.Scored.Value);
                w.BeginObject("parts");
                if (p.Scored != null)
                    foreach (KeyValuePair<string, double> part in p.Scored.Parts)
                        if (!double.IsFinite(part.Value)) w.Raw(part.Key, "null"); else w.Field(part.Key, part.Value);
                w.EndObject();
                w.EndObject();
            }
            w.EndArray();
            w.EndObject();
            return w.ToString();
        }

        // ------------------------------------------------------------------ one tank

        /// <param name="watcher">
        /// Null for every swim the nursery makes (<see cref="SwimTanks"/> passes none). A watcher (<see cref="ITankWatcher"/>, <c>--restep</c>) only
        /// reads: it is handed an <see cref="EpisodeProbe"/>, never the <see cref="Rng"/>, and with it null the statements below run as they always did.
        /// </param>
        private static Resident[] TankEpisode(Genome[] genomes, ulong seed, Arena arena, ITankWatcher watcher = null)
        {
            // The leaf tank smoke's read of the leaf smell, only when EVOSIM_LEAF_SENSE names its file (LeafSenseProbe.cs). It reads, as every watcher does.
            if (watcher == null && LeafSenseProbe.Path != null) watcher = new LeafSenseProbe(seed);

            // The tank job's profile, set on this thread by SwimTanks only when EVOSIM_NURSERY_PROFILE=1.
            TankProfile.Job profile = TankProfile.Current;
            long profileT0 = profile != null ? Stopwatch.GetTimestamp() : 0L;
            int n = genomes.Length;
            var result = new Resident[n];
            for (int i = 0; i < n; i++) result[i] = new Resident();

            var world = new World(EpisodeConfig(arena.Text), seed);
            world.BirthsHeld = true;
            int stepsPerMetabolic = (int)Math.Round(Simulation.MetabolicStepSeconds / _o.Dt);
            var sim = new Simulation(world, seed, _o.Dt, stepsPerMetabolic, 1, null);
            try
            {
                var rng = new Rng(Rng.SeedFor(seed, 0x74616E6BUL));
                float bed = -arena.Config.WorldDepthMetres;

                // The snow first, so that the placer finds the tank as the bodies will.
                // Fault 2 of nursery-general-build.md: fixed snow takes its draw too and discards it,
                // so that a fixed and a drawn run differ in the density alone and the heights and the
                // carrion below draw the same numbers in both.
                if (_o.Snow > 0d)
                {
                    double density = _o.Snow;
                    float spread = rng.NextFloat();
                    if (!_o.SnowFixed) density *= Math.Pow(2d, 2d * spread - 1d);
                    if (_o.SnowIslandWavelength > 0f)
                        world.LaySnowIslands((float)density, Rng.SeedFor(seed, 0x736E6F77UL), _o.SnowIslandWavelength, _o.SnowIslandCover);
                    else
                        world.LaySnow((float)density, _o.SnowLayers);
                }

                // The leaf tank (LeafTank.cs): the water's spent units and the leaves, before the bred bodies so that the placer
                // finds the leaves where they stand. Drawn on a stream of their own, so a recipe without leaves draws and places
                // exactly what it did. The leaves are not members: nothing below reads them but the world.
                if (_leafPool != null) LayLeaves(world, sim, seed, arena.Config);

                var focal = new Organism[n];
                for (int i = 0; i < n; i++)
                {
                    if (genomes[i] == null) continue;
                    int before = world.Living.Count;

                    // Jakobi's draw on the start: each body's height over the bed, half to one and
                    // a half times setup.start.aboveBed.
                    float height = bed + (float)(_o.BodyAboveBed * (0.5d + rng.NextFloat()));
                    try { world.Inoculate(genomes[i], 1, height); }
                    catch (ArgumentException) { continue; }
                    if (world.Living.Count == before) continue;
                    focal[i] = world.Living[world.Living.Count - 1];

                    // The guard a run makes before anything swims (RefuseFeedersBesideLeaves), made again of the body itself.
                    string feeder = _leafPool == null ? null : FeederBesideLeaves(focal[i].HasAbsorptiveTissue, focal[i].HasPhotosyntheticTissue);
                    if (feeder != null) throw new InvalidDataException($"recipe: the bred body in slot {i} {feeder}; the recipe's leaves refuse it");
                }

                var startReserve = new double[n];
                var standing = new double[n];
                var startY = new double[n];
                var startMouth = new double[n];
                var lastMouth = new double[n];
                for (int i = 0; i < n; i++)
                {
                    Organism o = focal[i];
                    if (o == null) continue;
                    // The purse ("grown"): the growth paid in full, and the recipe's purse seconds
                    // (the config's endowment seconds by default) of the adult's standing watts on
                    // top, uncapped, so that a body that eats nothing lives that long once grown.
                    // A founder pays its growth out of the reserve on its first step, and an
                    // endowment alone starves a growing body inside a minute.
                    double adultTissue = Metabolism.TissueJoules(o.AdultPhenotype, arena.Config);
                    double seconds = !double.IsNaN(_o.PurseSeconds) ? _o.PurseSeconds : arena.Config.FounderEndowmentSeconds;
                    double target = Math.Max(0d, adultTissue - o.TissueJoules) + seconds * Metabolism.StandingWatts(o.AdultPhenotype, arena.Config);
                    double give = target - o.Energy;
                    if (give > 0d) world.GiveReserve(o, give);
                    result[i].Landed = true;
                    result[i].Start = o.Energy + o.TissueJoules + o.GestationJoules;
                    startReserve[i] = o.Energy;
                    startMouth[i] = o.MouthJoules;
                    lastMouth[i] = o.MouthJoules;
                    standing[i] = Metabolism.StandingWatts(o.Phenotype, arena.Config);
                    startY[i] = o.Point.Position.Y;
                    if (_tankTrace)
                        Say($"    trace body {i}: parts {string.Join(",", o.Phenotype.Parts.Select(pt => pt.CellTypeId + (pt.Intake > 0f ? "(mouth)" : "")))}");
                }

                // Carrion on the bed at random places inside the glass (the sitter's guard: it
                // does not rain on a body that waits).
                float limit = Math.Max(1f, world.TankRadiusMetres - 1.5f);
                for (int c = 0; c < arena.Corpses; c++)
                {
                    float x, z;
                    if (world.TankRadiusMetres > 0f)
                    {
                        double r = limit * Math.Sqrt(rng.NextFloat()), a = 2d * Math.PI * rng.NextFloat();
                        x = world.TankRadiusMetres + (float)(r * Math.Cos(a));
                        z = world.TankRadiusMetres + (float)(r * Math.Sin(a));
                    }
                    else
                    {
                        x = rng.NextFloat() * world.Nutrients.LengthMetres;
                        z = rng.NextFloat() * world.Nutrients.WidthMetres;
                    }
                    world.PlaceCorpse(new Float3(x, bed + 0.3f, z), arena.CorpseJoules);
                }

                var last = new Float3[n];
                var steps = new int[n];
                var turnSteps = new int[n];
                for (int i = 0; i < n; i++) if (focal[i] != null) last[i] = focal[i].Point.Position;

                // The world writes Organism.LastLedger for bodies with absorptive tissue only, and every other body reads the zero it was born
                // with. A body without it (a leaf trunk and a link limb, say) has its step read from its Lifetime ledger's change instead, which
                // the world adds to for every body. A body with absorptive tissue reads LastLedger as before, so its results are the bytes they were.
                var lifetime = new EnergyLedger[n];
                for (int i = 0; i < n; i++) if (focal[i] != null) lifetime[i] = focal[i].Lifetime;

                // The trace's corpse census (EVOSIM_TANK_TRACE only): each corpse the episode makes, first seen after an economy step, by the
                // body it came from (a leaf, that is a body living at the start that is not a member; a member; or neither) and its joules at its
                // making (Corpse.FirstJoules). Reads only.
                HashSet<long> censusLeaves = null, censusMembers = null;
                HashSet<Corpse> censusSeen = null;
                double[] censusJoules = null;
                int[] censusCount = null;
                if (_tankTrace)
                {
                    censusMembers = new HashSet<long>(focal.Where(f => f != null).Select(f => f.Id));
                    censusLeaves = new HashSet<long>(world.Living.Where(b => !censusMembers.Contains(b.Id)).Select(b => b.Id));
                    censusSeen = new HashSet<Corpse>(world.Corpses);
                    censusJoules = new double[3];
                    censusCount = new int[3];
                }

                // The watcher's view of the episode, and its first call: the carrion is placed and no step has been taken. Reads only.
                EpisodeProbe probe = null;
                if (watcher != null)
                {
                    probe = new EpisodeProbe(world, sim, focal);
                    watcher.Begin(probe);
                }

                // A tank still swimming after a minute of wall time says where it is, once a minute:
                // one slow episode holds up the whole generation at the join. Reads only.
                var wall = Stopwatch.StartNew();
                long reportAt = 60_000;
                if (profile != null) profile.Setup = Stopwatch.GetTimestamp() - profileT0;
                while (world.ElapsedSeconds < arena.Seconds)
                {
                    if (!sim.Step())
                    {
                        if (watcher != null) watcher.Physics(probe);
                        continue;
                    }
                    if (wall.ElapsedMilliseconds > reportAt)
                    {
                        reportAt += 60_000;
                        Say(FormattableString.Invariant(
                            $"  slow tank, seed {seed % 100000}: {wall.Elapsed.TotalSeconds:0} s wall at {world.ElapsedSeconds:0.0} s simulated, {world.Living.Count} living, {sim.Dynamics.TotalLinks()} links, diverged {world.Diverged}; bodies {string.Join("; ", focal.Select(o => o == null ? "-" : $"{o.Phenotype.PartCount}p {(world.Living.Contains(o) ? "alive" : "dead")} y {o.Point.Position.Y:0.0} e {o.Energy:0}"))}"));
                    }
                    // The kills each member's bites made this step: the world's kill rows, which name the attacker. Draining the rows changes
                    // nothing the world steps on (World.DrainLineageEvents), and nothing else in the nursery reads them.
                    foreach (LineageEvent e in world.DrainLineageEvents())
                    {
                        if (e.Kind != LineageEventKind.Kill || e.AttackerId < 0) continue;
                        for (int i = 0; i < n; i++)
                        {
                            if (focal[i] == null || focal[i].Id != e.AttackerId) continue;
                            result[i].Kills++;
                            result[i].Killed += e.TissueJoulesLost + e.ReserveJoulesLost;
                        }
                    }
                    for (int i = 0; i < n; i++)
                    {
                        Organism o = focal[i];
                        if (o == null || !double.IsNaN(result[i].DiedAt)) continue;
                        if (!world.Living.Contains(o)) { result[i].DiedAt = world.ElapsedSeconds; continue; }
                        if (o.MouthJoules > lastMouth[i]) result[i].Meals++;
                        lastMouth[i] = o.MouthJoules;
                        if (o.HasAbsorptiveTissue)
                        {
                            EnergyLedger l = o.LastLedger;
                            result[i].Food += l.FoodIncome;
                            result[i].Light += l.LightIncome;
                            result[i].Work += l.Work;
                            result[i].Upkeep += l.Upkeep;
                            result[i].Handling += l.Handling;
                            result[i].Exuded += l.Exuded;
                        }
                        else
                        {
                            EnergyLedger now = o.Lifetime, was = lifetime[i];
                            result[i].Food += (double)now.FoodIncome - was.FoodIncome;
                            result[i].Light += (double)now.LightIncome - was.LightIncome;
                            result[i].Work += (double)now.Work - was.Work;
                            result[i].Upkeep += (double)now.Upkeep - was.Upkeep;
                            result[i].Handling += (double)now.Handling - was.Handling;
                            result[i].Exuded += (double)now.Exuded - was.Exuded;
                            lifetime[i] = now;
                        }

                        // The path on the economy's half second: a lower bound on the distance
                        // swum, which misses the wag within a step and keeps the travel.
                        Float3 p = o.Point.Position;
                        double dx = p.X - last[i].X, dy = p.Y - last[i].Y, dz = p.Z - last[i].Z;
                        result[i].Path += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        last[i] = p;
                        result[i].Fresh += world.Nutrients.EdibleDensityAt(o.Point);
                        steps[i]++;

                        // The rocking on the same half second: the root's angular speed, read off the solver's own state (the root
                        // link's world spin, which the integrator holds and the step left). A read of an array: nothing is built.
                        if (sim.TryPose(o.Id, out Evosim.Dynamics.Creature solver))
                        {
                            double wx = solver.Spin[0], wy = solver.Spin[1], wz = solver.Spin[2];
                            result[i].Turn += Math.Sqrt(wx * wx + wy * wy + wz * wz);
                            turnSteps[i]++;
                        }
                    }
                    probe?.AfterEconomy();   // built only for a watcher: the ledger of a body the world leaves at zero, before the watcher reads it
                    if (watcher != null) watcher.Metabolic(probe);
                    if (_tankTrace)
                        foreach (Corpse c in world.Corpses)
                        {
                            if (!censusSeen.Add(c)) continue;
                            int k = censusLeaves.Contains(c.CreatureId) ? 0 : censusMembers.Contains(c.CreatureId) ? 1 : 2;
                            censusCount[k]++;
                            censusJoules[k] += c.FirstJoules;
                        }
                }
                if (_tankTrace)
                    Say(FormattableString.Invariant(
                        $"    trace corpses seed {seed % 100000}: from leaves {censusCount[0]} ({censusJoules[0]:0.0} J), from members {censusCount[1]} ({censusJoules[1]:0.0} J), other {censusCount[2]} ({censusJoules[2]:0.0} J); bodies eaten {world.BodiesEaten}, corpses from kills {world.CorpsesFromKills}, corpses eaten {world.CorpsesEaten}, module drops {world.ModuleDrops}, carrion placed {world.CorpsesPlaced}, members' mouths kept {Enumerable.Range(0, n).Where(i => focal[i] != null).Sum(i => focal[i].MouthJoules - startMouth[i]):0.0} J"));

                for (int i = 0; i < n; i++)
                {
                    Organism o = focal[i];
                    if (o == null) continue;
                    bool alive = world.Living.Contains(o);
                    double end = alive ? o.Energy + o.TissueJoules + o.GestationJoules : 0d;
                    result[i].Survived = alive;
                    result[i].End = end;
                    result[i].Net = end - result[i].Start;
                    result[i].Fresh = steps[i] > 0 ? result[i].Fresh / steps[i] : double.NaN;
                    result[i].Turn = turnSteps[i] > 0 ? result[i].Turn / turnSteps[i] : double.NaN;
                    // What the mouth kept. The count is added to only while the body lives, so a body that died holds what it had eaten.
                    result[i].Eaten = o.MouthJoules - startMouth[i];
                    if (_tankTrace && i == 0)
                        Say(FormattableString.Invariant($"    trace world: deaths {world.Deaths}, by age {world.DeathsByAge}, parts killed by mouths {world.PartsKilled}, diverged {world.Diverged}"));
                    if (_tankTrace)
                        Say(FormattableString.Invariant(
                            $"    trace seed {seed % 100000}: body {i} start {result[i].Start:0.0} J (reserve {startReserve[i]:0.0}, tissue {result[i].Start - startReserve[i]:0.0}), standing {standing[i]:0.000} W, end {end:0.0} J, died {result[i].DiedAt:0}, food {result[i].Food:0.0}, eaten {result[i].Eaten:0.0}, work {result[i].Work:0.0}, upkeep {result[i].Upkeep:0.0}, handling {result[i].Handling:0.0}, exuded {result[i].Exuded:0.0}, path {result[i].Path:0.0} m, turn {result[i].Turn:0.000} rad/s, y0 {startY[i]:0.00}"));
                }
                if (watcher != null) watcher.End(probe);

                if (profile != null)
                {
                    // When the tank's last body died, for the reader's estimate of what ending a
                    // tank with no one alive in it would save. Read after the episode; draws nothing.
                    double lastDeath = 0d;
                    int landed = 0;
                    bool anyAlive = false;
                    for (int i = 0; i < n; i++)
                    {
                        if (!result[i].Landed) continue;
                        landed++;
                        if (result[i].Survived || double.IsNaN(result[i].DiedAt)) anyAlive = true;
                        else lastDeath = Math.Max(lastDeath, result[i].DiedAt);
                    }
                    profile.Bodies = landed;
                    if (landed > 0 && !anyAlive) profile.AllDeadAt = lastDeath;
                    profile.Take(sim);
                }
                return result;
            }
            finally
            {
                sim.Dispose();
            }
        }

        /// <summary>
        /// Every genome swum search.draws times, shuffled into tanks of setup.tankSize each time, all tanks
        /// of a draw on the draw's seed. [genome][draw], null where the genome could not land.
        /// </summary>
        /// <remarks>
        /// The tanks are dealt by <see cref="TankLog.PlanTanks"/>, the one shuffle, and <paramref name="onPlan"/> (when given) is told each
        /// draw's seed and tanks before anything swims, so that the files of who swam with whom are written from the very plan that is swum.
        /// It reads the plan and draws nothing.
        /// </remarks>
        private static Resident[][] SwimTanks(Genome[] genomes, int generation, int draws, ulong salt, Arena arena, Action<int, ulong, int[][]> onPlan = null)
        {
            int n = genomes.Length, m = _o.TankSize;
            ulong[] seeds = SeedsFor(generation, draws, salt);
            int[][][] plan = TankLog.PlanTanks(n, m, seeds);
            var jobs = new List<(int draw, int[] members)>();
            for (int d = 0; d < draws; d++)
            {
                onPlan?.Invoke(d, seeds[d], plan[d]);
                foreach (int[] tank in plan[d]) jobs.Add((d, tank));
            }

            var readings = new Resident[n][];
            for (int i = 0; i < n; i++) readings[i] = new Resident[draws];
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = _o.Threads };
            TankProfile.Pool profile = TankProfile.On ? new TankProfile.Pool(jobs.Count, generation, draws, salt, _o.Threads) : null;
            // One job at a time to whichever thread is free. Parallel.For hands its range out in chunks
            // that grow as it goes, and a 14-thread generation of 192 tanks ended with its last 10 run one
            // after another on one thread: 103 s of a 307 s generation (logbook/specs/nursery-speed.md,
            // A1). Each job writes only its own members' slots for its own draw, so which thread runs it,
            // and when, changes no result.
            var order = Partitioner.Create(Enumerable.Range(0, jobs.Count), EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(order, parallel, j =>
            {
                (int d, int[] members) = jobs[j];
                TankProfile.Job job = profile?.Begin(j, d);
                TankProfile.Current = job;
                Resident[] r = TankEpisode(members.Select(i => genomes[i]).ToArray(), seeds[d], arena);
                TankProfile.Current = null;
                for (int k = 0; k < members.Length; k++) readings[members[k]][d] = r[k].Landed ? r[k] : null;
                if (job != null) profile.End(job);
            });
            profile?.Write(_o.Out);
            return readings;
        }

        // ------------------------------------------------------------------ the loop

        private static int EvolveTank(List<Body> bodies)
        {
            var clock = Stopwatch.StartNew();
            int size = _o.Population;
            var rng = new Rng(Rng.SeedFor(_o.Seed, 0x74616E6B32UL));
            var population = new List<Member>();
            for (int k = 0; k < size; k++)
            {
                Body b = bodies[k % bodies.Count];
                // D146: under the one brain every member draws its own brain on its body, so the
                // first generation is a spread of brains rather than one brain copied.
                Genome start = b.Ancestor;
                // D178: a recipe that keeps a pool's brains starts every member as the pool wrote it.
                if (start.Brain != null && !_o.KeepBrains) start = FreshBrain(b.Ancestor, Rng.SeedFor(Rng.SeedFor(_o.Seed, 0x42524E31UL), (ulong)k));
                population.Add(new Member { Genome = start, Body = b.Index, Born = 0 });
            }

            Say(FormattableString.Invariant(
                $"tank search: {size} genomes from {bodies.Count} bodies, tanks of {_o.TankSize}, {_o.Draws} draws a generation, {_o.Elites} elites, tournament of {_o.Tournament}, evolve {_o.Evolve}{(_o.PerBody ? " per body" : "")}, {_search.Seconds:0} s episodes, snow {_o.Snow} J/m3{(_o.SnowFixed ? "" : " (half to double)")} on {_o.SnowLayers} layer(s){(_o.SnowIslandWavelength > 0f ? $" in islands of {_o.SnowIslandWavelength} m at cover {_o.SnowIslandCover}" : "")}, purse {_o.PurseRule}{(double.IsNaN(_o.PurseSeconds) ? "" : ", " + _o.PurseSeconds + " s of standing watts")}"));

            // propose-nursery-crossover.md. Off (the default), nothing below draws or writes anything new.
            bool crossing = _o.Crossover > 0d;
            if (crossing)
                Say(FormattableString.Invariant(
                    $"  crossover {_o.Crossover} of children, base the better parent {_o.BaseShare} of the time, mutation {(double.IsNaN(_o.MutationByDistance) ? "always" : $"by distance, d_ref {_o.MutationByDistance}")}"));
            using StreamWriter crossLog = crossing ? new StreamWriter(Path.Combine(_o.Out, "tank-crossovers.jsonl")) : null;
            var made = new CrossStats();

            using var generations = new StreamWriter(Path.Combine(_o.Out, "tank-generations.jsonl"));
            using var best = new StreamWriter(Path.Combine(_o.Out, "tank-best.jsonl"));
            // Wall time has its own file (nursery-general-build.md, section 1), so that two runs of
            // one experiment write the same result rows.
            using var timing = new StreamWriter(Path.Combine(_o.Out, "timing.jsonl"));
            // The reward's verdict on every member each generation, its value and its named parts
            // (nursery-general-build.md, section 4), beside the rows rather than in them.
            using var scores = new StreamWriter(Path.Combine(_o.Out, "tank-scores.jsonl"));
            // Who swam with whom, and where each member came from (TankLog.cs): new files, written from what the loop already holds.
            // Nothing here draws a number, so every file above is written as it was before these existed.
            using var drawsLog = new StreamWriter(Path.Combine(_o.Out, TankLog.DrawsFile));
            using var lineage = new StreamWriter(Path.Combine(_o.Out, TankLog.LineageFile));
            // D172: the measure the stop reads, a generation at a time, and each generation's top as tank-best.jsonl has it, for the exam.
            var curve = new List<double>();
            var tops = new List<(string text, string id, int body)>();

            for (int gen = 0; ; gen++)
            {
                if (StopNow($"before generation {gen}")) return 0;
                // The ids of this generation: each member's place in the population as the search holds it now. An elite carried over
                // took its parent's id (its own, a generation ago) in Parents when the next population was made, and is renamed here.
                for (int i = 0; i < population.Count; i++) population[i].Id = TankLog.MemberId(gen, i);
                foreach (Member p in population)
                    TankLog.Append(lineage, TankLog.LineageRow(gen, p.Id, p.Body, p.Born, p.Parents, p.Elite));
                string[] ids = population.Select(p => p.Id).ToArray();
                int thisGen = gen;
                Resident[][] r = SwimTanks(population.Select(p => p.Genome).ToArray(), gen, _o.Draws, GenerationSalt, _search,
                    (draw, seed, tanks) => TankLog.Append(drawsLog, TankLog.DrawRow(thisGen, draw, seed, tanks, ids)));
                ulong[] seeds = SeedsFor(gen, _o.Draws, GenerationSalt);
                for (int i = 0; i < population.Count; i++)
                {
                    Member p = population[i];
                    Resident[] mine = r[i].Where(x => x != null).ToArray();
                    if (mine.Length == 0) { p.Net = double.NaN; continue; }
                    double sum = mine.Sum(x => x.Net);
                    int before = p.Records.Count;
                    for (int d = 0; d < r[i].Length; d++)
                        if (r[i][d] != null) p.Records.Add(RecordOf(r[i][d], gen, d, seeds[d], "search", p.Body, _search.Seconds));
                    // D178: each of this generation's episodes scored alone, for a stop that reads the reward. Reads only.
                    if (_o.StopMeasure == "reward")
                        p.GenerationReward = p.Records.Skip(before).Average(e => _reward.Score(new[] { e }).Value);
                    // The score through the reward. Net's running mean, kept a generation at a time,
                    // is the mean this loop kept before rewards existed, to the bit (RewardMath).
                    p.Scored = _reward.Score(p.Records);
                    p.Score = p.Scored.Value;
                    p.Episodes += mine.Length;
                    p.Net = sum / mine.Length;
                    p.Work = mine.Average(x => x.Work) / _search.Seconds;
                    p.Path = mine.Average(x => x.Path);
                    p.Turn = TurnOf(mine);
                    p.Fresh = mine.Where(x => !double.IsNaN(x.Fresh)).Select(x => x.Fresh).DefaultIfEmpty(double.NaN).Average();
                    p.Eaten = mine.Average(x => x.Eaten);
                    p.Kills = mine.Average(x => x.Kills);
                    p.Meals = mine.Average(x => x.Meals);
                    p.Alive = mine.Count(x => x.Survived) / (double)mine.Length;
                }

                Member[] ranked = population.OrderByDescending(p => double.IsNaN(p.Score) ? double.NegativeInfinity : p.Score).ToArray();
                Member[] landed = population.Where(p => !double.IsNaN(p.Net)).ToArray();
                Member top = ranked[0];
                DriveGenes genes = top.Genome.Nodes.FirstOrDefault(nd => nd.JointType != JointType.Fixed)?.Drive ?? DriveGenes.Default;
                double Mean(Func<Member, double> f) => landed.Length == 0 ? double.NaN : landed.Average(f);
                // Over the members that have the measure: a body with no step measured has no turn.
                double MeanOf(Func<Member, double> f) => landed.Select(f).Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN).Average();
                string row = Inv3(
                    $"{{\"gen\":{gen},\"landed\":{landed.Length},\"netMean\":{Num(Mean(p => p.Net))},\"netBest\":{Num(landed.Length == 0 ? double.NaN : landed.Max(p => p.Net))},",
                    $"\"alive\":{Num(Mean(p => p.Alive))},\"workWatts\":{Num(Mean(p => p.Work))},\"path\":{Num(Mean(p => p.Path))},\"turn\":{Num(MeanOf(p => p.Turn))},\"fresh\":{Num(Mean(p => p.Fresh))},\"eaten\":{Num(Mean(p => p.Eaten))},\"kills\":{Num(Mean(p => p.Kills))},\"meals\":{Num(Mean(p => p.Meals))},",
                    $"\"top\":{{\"score\":{Num(top.Score)},\"episodes\":{top.Episodes},\"body\":{top.Body},\"born\":{top.Born},\"nodes\":{top.Genome.Nodes.Count},\"threshold\":{genes.Threshold:R},\"reaction\":{genes.Reaction:R},\"hold\":{genes.Hold:R},\"release\":{genes.Release:R}}}}}");
                // With the crossover on, how this generation's children were made (by the step
                // that ended the generation before), appended last so that existing readers still
                // read every field they read before.
                if (crossing) row = row.Substring(0, row.Length - 1) + ",\"made\":" + made.Json(population.Select(p => p.Genome)) + "}";
                // D146: how big the one brains are, the test's "grown" read: hidden units and links,
                // the mean and the largest, and the top brain's.
                if (population[0].Genome.Brain != null)
                {
                    CentralBrain[] brains = population.Select(p => p.Genome.Brain).ToArray();
                    row = row.Substring(0, row.Length - 1) + Inv3(
                        $",\"brain\":{{\"hidden\":{Num(brains.Average(b => b.Hidden.Count))},\"hiddenMax\":{brains.Max(b => b.Hidden.Count)},",
                        $"\"links\":{Num(brains.Average(b => b.Links.Count))},\"linksMax\":{brains.Max(b => b.Links.Count)},",
                        $"\"topHidden\":{top.Genome.Brain.Hidden.Count},\"topLinks\":{top.Genome.Brain.Links.Count}}}}}");
                }
                if (_o.EvolveEnvelope)
                {
                    var env = Envelope.Mean(population.Select(p => p.Genome));
                    row = row.Substring(0, row.Length - 1) + Inv3(
                        $",\"envelope\":{{\"joints\":{env.joints},\"threshold\":{Num(env.threshold)},\"reaction\":{Num(env.reaction)},",
                        $"\"hold\":{Num(env.hold)},\"release\":{Num(env.release)}}}",
                        $"}}");
                }
                // D172: the stop's reading of this generation, in its row so that the stop can be read from the files. Off, the row is
                // as it was.
                curve.Add(_o.StopMeasure == "reward" ? MeanOf(p => p.GenerationReward) : Mean(p => p.Eaten));
                bool stalled = false;
                if (_o.StopMeasure != null)
                {
                    SearchEnd.Verdict v = SearchEnd.Judge(curve, _o.StopWindow, _o.StopGain, _o.StopOver, _o.StopFloor);
                    stalled = v.Stop;
                    row = row.Substring(0, row.Length - 1) + FormattableString.Invariant(
                        $",\"stop\":{{\"smoothed\":{Num(v.Smoothed)},\"gain\":{Num(v.Gain)},\"stop\":{(v.Stop ? "true" : "false")}}}}}");
                }
                generations.WriteLine(row);
                generations.Flush();
                timing.WriteLine(FormattableString.Invariant($"{{\"gen\":{gen},\"seconds\":{clock.Elapsed.TotalSeconds:0.0}}}"));
                timing.Flush();
                scores.WriteLine(ScoresRow(gen, population));
                scores.Flush();
                string topText = GenomeJson.Write(top.Genome);
                best.WriteLine(topText);
                best.Flush();
                tops.Add((topText, top.Id, top.Body));
                Say(FormattableString.Invariant(
                    $"  gen {gen}: net mean {Mean(p => p.Net):0.0} J, best running mean {top.Score:0.0} J over {top.Episodes} (body {top.Body}, born {top.Born}), alive {Mean(p => p.Alive):P0}, work {Mean(p => p.Work):0.000} W, path {Mean(p => p.Path):0.0} m, turn {MeanOf(p => p.Turn):0.000} rad/s, eaten {Mean(p => p.Eaten):0.0} J, kills {Mean(p => p.Kills):0.00}, meals {Mean(p => p.Meals):0.0}, {clock.Elapsed.TotalMinutes:0.0} min"));
                if (top.Genome.Brain != null)
                    Say(FormattableString.Invariant(
                        $"    brains: hidden {population.Average(p => p.Genome.Brain.Hidden.Count):0.00} (max {population.Max(p => p.Genome.Brain.Hidden.Count)}), links {population.Average(p => p.Genome.Brain.Links.Count):0.0}; top {top.Genome.Brain.Hidden.Count} hidden, {top.Genome.Brain.Links.Count} links"));
                if (crossing && gen > 0) Say("    made: " + made.Line(population.Select(p => p.Genome)));

                if (stalled)
                    Say(FormattableString.Invariant($"  the search stops after generation {gen}: {(_o.StopMeasure == "reward" ? "the reward" : "eaten")}, averaged over {_o.StopWindow} generations, gained no more than {_o.StopGain:P0} in {_o.StopOver} (D172)"));
                if (gen >= _o.Generations || stalled)
                {
                    // The owner's ruling, 2026-10-04: every nursery run keeps its last generation's whole
                    // population, so that a later round can take the best ten creatures per body. Written
                    // here once and read by nothing in the run, so no other result moves.
                    TankPopulation.Write(_o.Out, gen, population.Select(p => new PopulationMember
                    {
                        Genome = p.Genome, Body = p.Body, Born = p.Born, Score = p.Score, Episodes = p.Episodes, Net = p.Net,
                    }));
                    if (_o.ExamPool == "tops") return ExamOfTops(tops, population, size);
                    // The exam's genomes, named by the ids they have in this last generation of the search.
                    List<Member> finalists = (_o.PerBody
                        ? population.GroupBy(p => p.Body).Select(g => g.OrderByDescending(p => double.IsNaN(p.Score) ? double.NegativeInfinity : p.Score).First())
                        : ranked.Take(_o.Elites)).ToList();
                    return TankBench(finalists.Select(p => (p.Genome, p.Body)).ToList(), "exam", finalists.Select(p => p.Id).ToArray());
                }

                // search.perBody: every body keeps the share of the population it started with, and a brain
                // meets only brains on its own body in the tournament, so one body's blind cruise cannot
                // take the population and every body's brain is asked whether it senses (the exam then
                // takes each body's best). Without it, selection runs across bodies as before.
                var groups = _o.PerBody
                    ? population.GroupBy(p => p.Body).Select(g => g.ToArray()).ToList()
                    : new List<Member[]> { population.ToArray() };
                var next = new List<Member>();
                int tries = 0;
                made = new CrossStats();
                foreach (Member[] group in groups)
                {
                    int quota = group.Length, start = next.Count;
                    int elites = _o.PerBody ? Math.Max(1, _o.Elites * quota / size) : _o.Elites;
                    // An elite is the same member carried over (its genome, its episodes): its one parent is itself a generation ago.
                    List<Member> carried = group.OrderByDescending(p => double.IsNaN(p.Score) ? double.NegativeInfinity : p.Score).Take(elites).ToList();
                    foreach (Member e in carried)
                    {
                        e.Parents = TankLog.CopyParents(e.Id);
                        e.Elite = true;
                    }
                    next.AddRange(carried);
                    while (next.Count - start < quota)
                    {
                        Member parent = null;
                        for (int t = 0; t < _o.Tournament; t++)
                        {
                            Member c = group[rng.Range(group.Length)];
                            if (parent == null || Better(c.Score, parent.Score)) parent = c;
                        }
                        if (crossing)
                        {
                            // Every draw of the crossover is inside this branch, so with search.crossover
                            // at 0 the stream is drawn exactly as it was before the rule existed.
                            CrossStep step = CrossChild(group, parent, rng);
                            if (step.Child == null && ++tries < 10 * size) continue;
                            made.Add(step, gen + 1, crossLog);
                            next.Add(new Member
                            {
                                Genome = step.Child ?? step.Unmutated, Body = step.Base.Body, Born = gen + 1,
                                Parents = TankLog.CrossParents(step.Base.Id, step.Donor.Id, step.Record),
                            });
                            continue;
                        }
                        Genome child = ChildOfKind(parent.Genome, rng);
                        if (child == null && ++tries < 10 * size) continue;
                        next.Add(new Member { Genome = child ?? parent.Genome.Clone(), Body = parent.Body, Born = gen + 1, Parents = TankLog.CopyParents(parent.Id) });
                    }
                }
                population = next;
            }
        }

        /// <summary>
        /// A child under search.evolve: the brain mutated and grafted onto the body (brain), or the whole genome mutated with the
        /// set of cell types locked to the parent's (R3). Null when twenty draws all changed the kind.
        /// </summary>
        private static Genome ChildOfKind(Genome parent, Rng rng)
        {
            if (_o.Evolve == "brain") return Child(parent, rng);

            var kinds = new HashSet<string>(parent.Nodes.Select(nd => nd.CellTypeId));
            SensorChannel[] pool = _config.SensorPool();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Genome mutant = Mutator.Mutate(
                    parent, new Rng(rng.NextUInt() | ((ulong)rng.NextUInt() << 32)), _config.Mutation,
                    _config.CellTypes, _config.Genome, pool,
                    buoyancyOffsetPriced: _config.BuoyancyOffsetWattsPerCubicMetre > 0f);
                if (!kinds.SetEquals(mutant.Nodes.Select(nd => nd.CellTypeId))) continue;
                // D146: a body that changed takes its brain with it, as a birth in the world does
                // (World.Conceive): edges numbered, the brain wired to the new body, then mutated.
                if (mutant.Brain != null)
                {
                    mutant.NumberEdges();
                    Phenotype body = BodyOf(mutant);
                    var brainRng = new Rng(rng.NextUInt() | ((ulong)rng.NextUInt() << 32));
                    mutant.Brain = CentralWiring.Effective(mutant.Brain, body, brainRng, pool, _config.Brain, founder: mutant.Brain.Outputs.Count == 0);
                    CentralMutation.Mutate(mutant.Brain, body, pool, brainRng, _config.Brain);
                }
                return mutant;
            }
            return null;
        }

        // ------------------------------------------------------------------ two parents

        /// <summary>One child made under search.crossover, before it is counted.</summary>
        private sealed class CrossStep
        {
            public Member Base, Donor;
            public bool BaseBetter, Mutated;

            /// <summary>Null when the child is a copy of the base (no crossover drawn).</summary>
            public CrossoverRecord Record;

            /// <summary>The brain distance between the crossed (or copied) child and its base.</summary>
            public double D;

            /// <summary>The crossed or copied child before mutation, and the child; Child is null when the mutation failed.</summary>
            public Genome Unmutated, Child;
        }

        /// <summary>
        /// propose-nursery-crossover.md, for one child: a second parent by the same tournament
        /// from the same group, the better of the two the base with probability search.crossover.baseShare,
        /// the crossover with probability search.crossover.rate (else a copy of the base), and then the
        /// mutation, always or with probability max(0, 1 - d / d_ref) under search.crossover.mutationByDistance.
        /// </summary>
        private static CrossStep CrossChild(Member[] group, Member first, Rng rng)
        {
            Member second = null;
            for (int t = 0; t < _o.Tournament; t++)
            {
                Member c = group[rng.Range(group.Length)];
                if (second == null || Better(c.Score, second.Score)) second = c;
            }
            Member better = Better(second.Score, first.Score) ? second : first;
            Member worse = ReferenceEquals(better, first) ? second : first;

            var step = new CrossStep();
            step.BaseBetter = rng.Chance((float)_o.BaseShare);
            step.Base = step.BaseBetter ? better : worse;
            step.Donor = step.BaseBetter ? worse : better;

            Genome x = rng.Chance((float)_o.Crossover)
                ? Crossover.Cross(step.Base.Genome, step.Donor.Genome, rng, step.Base.Genome.Brain != null ? _config.Brain.MaxHiddenUnits : 0, out step.Record)   // the old brain: no neuron limit, missing route neurons are added; the one brain: its hidden cap
                : step.Base.Genome.Clone();
            step.D = Crossover.BrainDistance(x, step.Base.Genome);
            step.Mutated = double.IsNaN(_o.MutationByDistance)
                || rng.NextFloat() < Math.Max(0d, 1d - step.D / _o.MutationByDistance);
            step.Unmutated = x;
            step.Child = step.Mutated ? ChildOfKind(x, rng) : x;
            return step;
        }

        /// <summary>What one generation's reproduction did under search.crossover, for the log.</summary>
        private sealed class CrossStats
        {
            private int _children, _tried, _transferred, _added, _mutated, _baseBetter, _routeNeurons, _unread;
            private double _d;
            private readonly int[] _ends = new int[5];
            private readonly SortedDictionary<string, int> _refused = new SortedDictionary<string, int>(StringComparer.Ordinal);

            public void Add(CrossStep s, int born, StreamWriter log)
            {
                bool mutated = s.Mutated && s.Child != null;
                _children++;
                if (mutated) _mutated++;
                if (s.BaseBetter) _baseBetter++;
                _d += s.D;
                CrossoverRecord r = s.Record;
                if (r == null) return;

                _tried++;
                if (r.Transferred)
                {
                    _transferred++;
                    _added += r.Added;
                    _routeNeurons += r.Route.Count;
                    _ends[(int)r.End]++;
                    if (!r.OutputInputRead) _unread++;
                }
                else
                {
                    _refused.TryGetValue(r.Refused, out int n);
                    _refused[r.Refused] = n + 1;
                }

                log.WriteLine(FormattableString.Invariant(
                    $"{{\"born\":{born},\"body\":{s.Base.Body},\"baseBetter\":{(s.BaseBetter ? "true" : "false")},\"baseScore\":{Num(s.Base.Score)},\"donorScore\":{Num(s.Donor.Score)},\"output\":[{r.OutputNode},{r.OutputIndex}],\"route\":[{string.Join(",", r.Route.Select(x => FormattableString.Invariant($"[{x.Node},{x.Index}]")))}],\"end\":\"{EndName(r.End)}\",\"transferred\":{(r.Transferred ? "true" : "false")},\"refused\":{(r.Refused == null ? "null" : "\"" + r.Refused + "\"")},\"added\":{r.Added},\"read\":{(r.OutputInputRead ? "true" : "false")},\"d\":{Num(s.D)},\"mutated\":{(mutated ? "true" : "false")}}}"));
                log.Flush();
            }

            private static string EndName(RouteEnd e) =>
                e == RouteEnd.Sense ? "sense" : e == RouteEnd.NoInput ? "oscillator" : e == RouteEnd.Loop ? "loop" : e == RouteEnd.Unresolved ? "unresolved" : "none";

            private static (double mean, int max) Sizes(IEnumerable<Genome> genomes)
            {
                // Under D146 the brain's size is its hidden units; under the old model its neurons.
                int[] sizes = genomes.Select(g => g.Brain != null ? g.Brain.Hidden.Count : g.Nodes.Sum(nd => nd.Neurons.Length)).ToArray();
                return sizes.Length == 0 ? (double.NaN, 0) : (sizes.Average(), sizes.Max());
            }

            private double Share(int n) => _children == 0 ? double.NaN : n / (double)_children;

            public string Json(IEnumerable<Genome> population)
            {
                (double mean, int max) = Sizes(population);
                string refused = string.Join(",", _refused.Select(kv => $"\"{kv.Key}\":{kv.Value.ToString(Inv)}"));
                return FormattableString.Invariant(
                    $"{{\"children\":{_children},\"crossovers\":{_tried},\"transferred\":{_transferred},\"added\":{_added},\"refused\":{{{refused}}},\"ends\":{{\"sense\":{_ends[(int)RouteEnd.Sense]},\"oscillator\":{_ends[(int)RouteEnd.NoInput]},\"loop\":{_ends[(int)RouteEnd.Loop]},\"unresolved\":{_ends[(int)RouteEnd.Unresolved]}}},\"routeLength\":{Num(_transferred == 0 ? double.NaN : _routeNeurons / (double)_transferred)},\"unread\":{_unread},\"baseBetter\":{Num(Share(_baseBetter))},\"dMean\":{Num(_children == 0 ? double.NaN : _d / _children)},\"mutated\":{Num(Share(_mutated))},\"neuronsMean\":{Num(mean)},\"neuronsMax\":{max}}}");
            }

            public string Line(IEnumerable<Genome> population)
            {
                (double mean, int max) = Sizes(population);
                string refused = _refused.Count == 0 ? "none" : string.Join(", ", _refused.Select(kv => $"{kv.Key} {kv.Value.ToString(Inv)}"));
                return FormattableString.Invariant(
                    $"{_children} children, {_tried} crossovers, {_transferred} passed a route ({_added} neurons added, mean length {(_transferred == 0 ? double.NaN : _routeNeurons / (double)_transferred):0.00}, ends sense {_ends[(int)RouteEnd.Sense]} / oscillator {_ends[(int)RouteEnd.NoInput]} / loop {_ends[(int)RouteEnd.Loop]} / unresolved {_ends[(int)RouteEnd.Unresolved]}, {_unread} set an unread input), refused {refused}; mean d {(_children == 0 ? double.NaN : _d / _children):0.000}, mutated {Share(_mutated):P0}; brains {mean:0.00} neurons (max {max})");
            }
        }

        /// <summary>
        /// D172's exam: every generation's top, as tank-best.jsonl holds it, beside the last generation's elites (the members the
        /// loop would carry over: each body's share with perBody, the best <c>Elites</c> without), each genome once and named by the
        /// first id it had. tank-exam-pool.jsonl says where each came from, one row a genome in the exam's order.
        /// </summary>
        private static int ExamOfTops(List<(string text, string id, int body)> tops, List<Member> population, int size)
        {
            double Rank(Member p) => double.IsNaN(p.Score) ? double.NegativeInfinity : p.Score;
            IEnumerable<Member> elites = _o.PerBody
                ? population.GroupBy(p => p.Body).SelectMany(g => g.OrderByDescending(Rank).Take(Math.Max(1, _o.Elites * g.Count() / size)))
                : population.OrderByDescending(Rank).Take(_o.Elites);
            List<SearchEnd.Entry> pool = SearchEnd.ExamPool(tops, elites.Select(p => (GenomeJson.Write(p.Genome), p.Id, p.Body)).ToList());

            using (var rows = new StreamWriter(Path.Combine(_o.Out, "tank-exam-pool.jsonl")))
                for (int i = 0; i < pool.Count; i++)
                {
                    var w = new Json.Writer(indent: false);
                    w.BeginObject();
                    w.Field("genome", i);
                    w.Field("id", pool[i].Id);
                    w.Field("body", pool[i].Body);
                    w.BeginArray("tops");
                    foreach (int g in pool[i].Tops) w.Value(g);
                    w.EndArray();
                    w.Field("elite", pool[i].Elite);
                    w.BeginArray("ids");
                    foreach (string id in pool[i].Ids) w.Value(id);
                    w.EndArray();
                    w.EndObject();
                    rows.WriteLine(w.ToString());
                }
            Say($"  the exam's pool (D172): {tops.Count} tops and the last generation's elites, {pool.Count} genomes once each");
            return TankBench(pool.Select(e => (GenomeJson.Read(e.Text), e.Body)).ToList(), "exam", pool.Select(e => e.Id).ToArray());
        }

        // ------------------------------------------------------------------ the bench

        private static int TankBench(List<Body> bodies) =>
            TankBench(bodies.Take(_o.Population).Select(b => (b.Ancestor, b.Index)).ToList(), "bench");

        /// <summary>
        /// A genome's mean food density at the body over its episodes, J/m3, NaN when none has one:
        /// beside a knockout's cost it says whether the cost came from steering or from where the
        /// body stayed.
        /// </summary>
        private static double FreshOf(Resident[] mine) =>
            mine.Where(x => !double.IsNaN(x.Fresh)).Select(x => x.Fresh).DefaultIfEmpty(double.NaN).Average();

        /// <summary>A genome's mean rocking over its episodes, rad/s, NaN when none has one (step 6 of logbook/specs/nursery-leaf-tank-plan.md).</summary>
        private static double TurnOf(Resident[] mine) =>
            mine.Where(x => !double.IsNaN(x.Turn)).Select(x => x.Turn).DefaultIfEmpty(double.NaN).Average();

        /// <summary>
        /// Every genome under each brain, every tank one brain, on held-out seeds: mean net, work,
        /// path and survival per brain, and each brain's net paired genome by genome against its own.
        /// </summary>
        /// <param name="ids">
        /// The id of each genome of the pool (<see cref="TankLog.MemberId"/>): the one it had in the search the exam follows. Null for an exam
        /// with no search, whose genomes are named by their place in the pool (<see cref="TankLog.PoolId"/>).
        /// </param>
        private static int TankBench(List<(Genome genome, int body)> pool, string name, string[] ids = null)
        {
            var clock = Stopwatch.StartNew();
            List<(string label, Func<Genome, Genome> make)> brains = _recipe.Controls.Select(c => (c.Label, ExamBrain(c))).ToList();
            ids = ids ?? Enumerable.Range(0, pool.Count).Select(TankLog.PoolId).ToArray();

            Say(FormattableString.Invariant(
                $"{name}: {pool.Count} genomes under {brains.Count} brains, tanks of {_o.TankSize}, {_o.FinalEpisodes} draws on held-out seeds (salt {_o.ExamSalt}), {_search.Seconds:0} s, snow {_o.Snow} J/m3, work cost {_config.WorkCostMultiplier}, purse {_o.PurseRule}"));

            // The genomes themselves, so an exam's winners can be benched again or inoculated.
            using (var kept = new StreamWriter(Path.Combine(_o.Out, $"tank-{name}-genomes.tsv")))
                foreach ((Genome genome, int body) in pool) kept.WriteLine(body.ToString(Inv) + "\t" + GenomeJson.Write(genome));

            using var rows = new StreamWriter(Path.Combine(_o.Out, $"tank-{name}.jsonl"));
            // Who swam with whom in each of the exam's draws, per brain (TankLog.cs). Written from the plan that is swum; draws nothing.
            using var examDraws = new StreamWriter(Path.Combine(_o.Out, TankLog.BenchOrExamDrawsFile(name)));

            // The exam's plan into run.json before the first episode: its brains in order, its genomes with their ids and bodies, its episodes
            // and its salt. A screen reads progress from tank-exam.jsonl's rows, as before. A failed write warns and the exam goes on.
            ulong[] examSeeds = SeedsFor(0, _o.FinalEpisodes, _o.ExamSalt);
            _record.Exam = new ExamPlan
            {
                Brains = _recipe.Controls.Select(c => new ExamPlan.BrainEntry { Label = c.Label, Kind = c.Kind }).ToList(),
                Genomes = Enumerable.Range(0, pool.Count).Select(i => new ExamPlan.GenomeEntry { Id = ids[i], Body = pool[i].body }).ToList(),
                Name = name, Episodes = _o.FinalEpisodes, Salt = _o.ExamSalt, Seeds = examSeeds,
            };
            WriteExamPlan();

            var own = new double[pool.Count];
            for (int b = 0; b < brains.Count; b++)
            {
                // The stop file is checked between brains, not between genomes: each brain swims every genome of the pool in one
                // go (SwimTanks), so a STOP found here waits for the brain being swum to finish, which can take a long while.
                // The search's check, above, is likewise between generations.
                if (StopNow($"before the exam's brain {b + 1} of {brains.Count}")) return 0;
                (string label, Func<Genome, Genome> make) = brains[b];
                Genome[] genomes = pool.Select(p => make(p.genome)).ToArray();
                Resident[][] r = SwimTanks(genomes, 0, _o.FinalEpisodes, _o.ExamSalt, _search,
                    (draw, seed, tanks) => TankLog.Append(examDraws, TankLog.ExamDrawRow(label, b, draw, seed, tanks, ids)));
                ulong[] seeds = SeedsFor(0, _o.FinalEpisodes, _o.ExamSalt);
                var net = new double[pool.Count];
                // The reward's value per genome over its exam episodes: what the exam pairs against
                // the genome's own brain, since the reward is what the search selected for.
                var score = new double[pool.Count];
                double work = 0, path = 0, alive = 0, fresh = 0, eaten = 0, turn = 0;
                int count = 0, freshN = 0, turnN = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    Resident[] mine = r[i].Where(x => x != null).ToArray();
                    net[i] = mine.Length == 0 ? double.NaN : mine.Average(x => x.Net);
                    var records = new List<EpisodeRecord>();
                    for (int d = 0; d < r[i].Length; d++)
                        if (r[i][d] != null) records.Add(RecordOf(r[i][d], 0, d, seeds[d], "exam", pool[i].body, _search.Seconds));
                    score[i] = _reward.Score(records).Value;
                    foreach (Resident x in mine)
                    {
                        work += x.Work / _search.Seconds; path += x.Path; alive += x.Survived ? 1 : 0; eaten += x.Eaten; count++;
                        if (!double.IsNaN(x.Fresh)) { fresh += x.Fresh; freshN++; }
                        if (!double.IsNaN(x.Turn)) { turn += x.Turn; turnN++; }
                    }
                    rows.WriteLine(FormattableString.Invariant(
                        $"{{\"brain\":\"{label}\",\"genome\":{i},\"body\":{pool[i].body},\"net\":{Num(net[i])},\"per\":[{string.Join(",", r[i].Select(x => x == null ? "null" : x.Net.ToString("R", Inv)))}],\"work\":{Num(mine.Length == 0 ? double.NaN : mine.Average(x => x.Work) / _search.Seconds)},\"path\":{Num(mine.Length == 0 ? double.NaN : mine.Average(x => x.Path))},\"turn\":{Num(TurnOf(mine))},\"fresh\":{Num(FreshOf(mine))},\"eaten\":{Num(mine.Length == 0 ? double.NaN : mine.Average(x => x.Eaten))},\"kills\":{Num(mine.Length == 0 ? double.NaN : mine.Average(x => x.Kills))},\"killed\":{Num(mine.Length == 0 ? double.NaN : mine.Average(x => x.Killed))},\"meals\":{Num(mine.Length == 0 ? double.NaN : mine.Average(x => x.Meals))},\"score\":{Num(score[i])}}}"));
                }
                rows.Flush();
                if (b == 0) Array.Copy(score, own, score.Length);

                // Paired against the genome's own brain, genome by genome, on the reward's value.
                var diff = Enumerable.Range(0, pool.Count).Where(i => !double.IsNaN(score[i]) && !double.IsNaN(own[i])).Select(i => score[i] - own[i]).ToArray();
                double dm = diff.Length == 0 ? double.NaN : diff.Average();
                double se = diff.Length < 2 ? double.NaN : Math.Sqrt(diff.Sum(x => (x - dm) * (x - dm)) / (diff.Length - 1) / diff.Length);
                double[] landed = net.Where(x => !double.IsNaN(x)).ToArray();
                Say(FormattableString.Invariant(
                    $"  {label,-24} net {(landed.Length == 0 ? double.NaN : landed.Average()):0.00} J, against own (score) {dm:+0.00;-0.00} (se {se:0.00}), alive {(count == 0 ? double.NaN : alive / count):P0}, work {(count == 0 ? double.NaN : work / count):0.000} W, path {(count == 0 ? double.NaN : path / count):0.0} m, turn {(turnN == 0 ? double.NaN : turn / turnN):0.000} rad/s, eaten {(count == 0 ? double.NaN : eaten / count):0.0} J, snow at the body {(freshN == 0 ? double.NaN : fresh / freshN):0.00} J/m3, {clock.Elapsed.TotalMinutes:0.0} min"));
            }
            return 0;
        }

        /// <summary>One exam control of a recipe as the brain it grafts on a genome.</summary>
        private static Func<Genome, Genome> ExamBrain(Recipe.ExamControl c)
        {
            switch (c.Kind)
            {
                case "own": return g => g;
                case "frozen": return g => GraftConstant(g, -1f, 0f);
                case "full": return g => GraftConstant(g, 1f, 0f);
                case "brainless": return Brainless;
                case "knockout": return g => KnockoutOf(g, c.Channels, out _);
                case "constant": return g => GraftConstant(g, c.Power, 0f);
                case "kinesis": return g => GraftDriveKinesis(g, c.Mode, c.Bias, c.Gain);
                default: throw new ArgumentException("no exam control " + c.Kind);
            }
        }

        /// <summary>The genome with every sensor input reading the constant 0: a brain that senses nothing.</summary>
        private static Genome Brainless(Genome genome)
        {
            if (genome.Brain != null) return KnockoutCentral(genome, null, out _);
            Genome g = genome.Clone();
            foreach (MorphNode node in g.Nodes)
                foreach (NeuronDef neuron in node.Neurons)
                {
                    NeuronInput[] inputs = neuron.Inputs;
                    for (int k = 0; k < inputs.Length; k++)
                        if (inputs[k].Kind == NeuronInputKind.Sensor) inputs[k] = NeuronInput.FromConstant(0f, inputs[k].Weight);
                }
            return g;
        }

        /// <summary>
        /// The kinesis under D135 (nursery-plan-joint-drive.md, "Controls"): every degree of
        /// freedom's power is <c>bias - gain x</c> and its bend centred, where <c>x</c> is the
        /// snow the part smells (<c>level</c>: <see cref="SensorChannel.Chemical"/>) or how fast
        /// that reading changes (<c>rate</c>: a <see cref="NeuronOp.Differentiate"/> neuron on it).
        /// A level kinesis pushes hardest in drained water and eases in rich; a rate kinesis
        /// pushes while the reading falls. The body's drive genes are its own.
        /// </summary>
        private static Genome GraftDriveKinesis(Genome body, string mode, float bias, float gain)
        {
            if (mode != "level" && mode != "rate") throw new ArgumentException("a kinesis control's mode is level or rate, not " + mode);
            Genome g = body.Clone();
            g.Brain = null;   // D146: a hand-wired brain replaces the one brain whole
            foreach (MorphNode node in g.Nodes)
            {
                int dofs = node.JointType.DofCount();
                if (dofs == 0) continue;
                bool rate = mode == "rate";
                var neurons = new NeuronDef[2 * dofs + (rate ? 1 : 0)];
                NeuronInput reading = rate
                    ? NeuronInput.FromNeuron(NeuronInputKind.SameNode, 2 * dofs, -gain)
                    : NeuronInput.FromSensor(SensorChannel.Chemical, 0, -gain);
                for (int d = 0; d < dofs; d++)
                {
                    neurons[d] = new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = bias, Inputs = new[] { reading } };
                    neurons[dofs + d] = new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() };
                }
                if (rate)
                {
                    neurons[2 * dofs] = new NeuronDef
                    {
                        Op = NeuronOp.Differentiate, Frequency = 1f, Amplitude = 1f,
                        Inputs = new[] { NeuronInput.FromSensor(SensorChannel.Chemical, 0, 1f) },
                    };
                }
                node.Neurons = neurons;
            }
            return g;
        }
    }
}