using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// The checkpoint's own acceptance, in one process and to the field: restore a checkpoint,
    /// step the world on, write it down again, restore that, and compare the stepped world with
    /// its restored twin member by member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a second restore rather than the recording.</b> Comparing a resumed run's rows with
    /// the original's says <i>that</i> a restore parted from the run and at which sample; it
    /// cannot say which number was put back wrong, because the original process is gone. Here
    /// both worlds are in hand: the stepped one is what the run had at that second, the
    /// restored one is what a resume would start from, and every field of every body and every
    /// organism is compared by reflection, so a fault names itself — the field, the body, the
    /// index and the two values. Built on 2026-09-23 when round 45 seed 2's checkpoints at
    /// 2,500 and 5,000 s each restored a handful of jointed bodies of the 1,800 wrongly and the
    /// row comparison could say no more than that.
    /// </para>
    /// <para>
    /// <b>What it steps.</b> <see cref="Simulation.Step"/> alone, which is the world's economy,
    /// growth and the physics; the run loop's other work per metabolic step is the report, the
    /// manifest and the lineage drain, none of which the next step reads. A fault that lives in
    /// that other work would not show here, and the row comparison
    /// (<c>scratch/checkpoint/compare.py</c>) still covers it.
    /// </para>
    /// <para>
    /// <b>What is skipped.</b> The world and the field a body senses through are shared by
    /// every body and compared once, as organisms and fields; a body's back-references to them
    /// are not followed. Wall-clock ticks and the throw trace's ring are left out, the first
    /// being a reading of the machine and the second a diagnostic nothing steps on. Everything
    /// else that is a number, a string or an array of either is compared exactly.
    /// </para>
    /// <para>
    /// <b>The senses and the harness are compared too, from 2026-09-25.</b> Until then every
    /// <c>ISensorField</c> was skipped as a shared service, which took each body's
    /// <c>CreatureSenses</c> out of the comparison, and <c>Organism.PartContact</c> was on the
    /// list of members a step fills before it reads, which it is not: it was sticky until a plan
    /// change then, and since D123 it is the last metabolic step's record, which the physics
    /// steps after a restore read before the next metabolic step rewrites it. So a restore that
    /// put every body back touching nothing and sensing neither contact nor damage passed, and
    /// round 48's resume parted at its first sample. Now a body's
    /// senses are compared member by member, less the per-step arrays <c>Sample</c> fills before
    /// the brain reads them; a wired contact or damage sense must be its own organism's array
    /// in both worlds; and the harness's own members are compared, bodies matched by id, which
    /// is what names a counter the harness does not write (<c>ModuleRebuilds</c> was one).
    /// </para>
    /// <para>
    /// <b>Then the two step side by side.</b> After the member comparison both worlds are
    /// stepped together for two metabolic steps, the solver's digest compared after every
    /// physics step and the world's state digest after every metabolic step, and the first
    /// step that differs is named with the lowest body id at which it does. A member the
    /// comparison skips as filled-before-read that is in fact read first shows here.
    /// </para>
    /// <para>
    /// <b>Its design limit.</b> Both worlds compared have been through a restore when the check
    /// starts from a checkpoint file: the stepped one was restored from the file, and the other
    /// from the stepped one's own write. State that a restore sets identically wrong on both
    /// sides (a member never written, and never refilled by the steps between) agrees with
    /// itself and cannot be seen. The founding mode (a run directory rather than a file) is the
    /// strong form: the stepped world has never been restored, so everything a live process
    /// carries from birth is compared against the restore. A file of the lossy format
    /// (<c>Checkpoint.LossyVersion</c>) is checked the same way and says so, and what its
    /// reader leaves out is exactly what the check cannot see from it.
    /// </para>
    /// </remarks>
    public static class CheckpointFidelity
    {
        /// <summary>
        /// Runs the check. Returns 0 when the stepped world and its restored twin agree in every
        /// compared member, 1 when they do not.
        /// </summary>
        /// <param name="checkpointPath">A checkpoint file of a run directory.</param>
        /// <param name="seconds">How far to step before writing the second checkpoint.</param>
        /// <param name="scratch">A directory this may write into.</param>
        /// <param name="threads">The solver's thread count.</param>
        public static int Run(string checkpointPath, double seconds, string scratch, int threads)
        {
            Directory.CreateDirectory(scratch);
            Parallelism.Threads = threads;

            Console.WriteLine("=== checkpoint fidelity");

            Restored first;
            CheckpointHeader header;
            RunConfig config;
            IReadOnlyList<Genome> pool;

            if (Directory.Exists(checkpointPath))
            {
                // A run directory: found its world from its own config and seed and step it live
                // to the second asked for. This is the one arrangement that can show a piece of
                // state a live process carries from birth and no checkpoint has ever held,
                // because the world compared has never been restored.
                config = RunDirectory.ReadConfig(checkpointPath, out _);
                pool = TricklePoolFiles.Load(checkpointPath, config)?.Genomes;
                ulong seed = SeedOf(checkpointPath);
                header = FoundingHeader(config, seed);

                Console.WriteLine("  founding   " + checkpointPath + " (seed " + seed + ") and stepping live to " + F(seconds) + " s on " + threads + " threads");
                Console.WriteLine("  then writing a checkpoint and restoring it");

                first = Found(config, pool, header, threads,Path.Combine(scratch, "first"));

                while (first.World.ElapsedSeconds < seconds - 1e-9) first.Sim.Step();
            }
            else
            {
                header = CheckpointReader.ReadHeader(checkpointPath);
                string sourceRun = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(checkpointPath)));
                config = RunDirectory.ReadConfig(sourceRun, out _);
                pool = TricklePoolFiles.Load(sourceRun, config)?.Genomes;

                Console.WriteLine("  checkpoint " + checkpointPath + " at " + F(header.Seconds) + " s");
                Console.WriteLine("  stepping   " + F(seconds) + " s on " + threads + " threads, then writing and restoring again");

                if (header.ReadLossily)
                {
                    Console.WriteLine(
                        "  note       version " + header.Version + " file, read lossily: every body " +
                        "restored touching nothing, with its contact and damage senses unwired. Both " +
                        "worlds compared below come after that read, so what it lost is invisible here.");
                }

                first = Restore(checkpointPath, config, pool, header,threads, Path.Combine(scratch, "first"));

                double until = first.World.ElapsedSeconds + seconds;
                while (first.World.ElapsedSeconds < until - 1e-9) first.Sim.Step();
            }

            // A checkpoint cannot be restored while a body's solver is off its organism's plan
            // (Simulation.PlanChangesPending), and the run loop defers one until the count is
            // zero; so does this. From round 49 the harness rebuilds on the metabolic step of
            // the change, so the count is zero here and this steps nothing.
            int extra = StepToARebuiltPlan(first);

            if (extra > 0)
            {
                Console.WriteLine(
                    "  stepped on " + extra + " physics steps to " + F(first.World.ElapsedSeconds) +
                    " s: a body's solver was off its plan, which the harness should never leave");
            }

            string again = Path.Combine(scratch, "again.ckpt");
            WriteAgain(again, first, header, config);

            Restored second = Restore(again, config, pool, CheckpointReader.ReadHeader(again), threads, Path.Combine(scratch, "second"));

            Console.WriteLine("  stepped to " + F(first.World.ElapsedSeconds) + " s: " +
                              first.World.Living.Count + " living, " + first.Sim.Dynamics.Creatures.Count + " bodies");
            Console.WriteLine();

            var differences = new List<string>();
            int bodiesDiffering = 0;

            // The organisms, by id.
            var organisms = new Dictionary<long, Organism>();
            foreach (Organism o in second.World.Living) organisms[o.Id] = o;

            int organismsDiffering = 0;

            foreach (Organism a in first.World.Living)
            {
                if (!organisms.TryGetValue(a.Id, out Organism b))
                {
                    differences.Add("organism " + a.Id + ": missing after the restore");
                    organismsDiffering++;
                    continue;
                }

                int before = differences.Count;
                Compare("organism " + a.Id, a, b, differences, 0, new HashSet<object>(ByReference.Instance));
                if (differences.Count > before) organismsDiffering++;
            }

            // The bodies, by id.
            var bodies = new Dictionary<int, Creature>();
            foreach (Creature c in second.Sim.Dynamics.Creatures) bodies[c.Id] = c;

            foreach (Creature a in first.Sim.Dynamics.Creatures)
            {
                if (!bodies.TryGetValue(a.Id, out Creature b))
                {
                    differences.Add("body " + a.Id + ": missing after the restore");
                    bodiesDiffering++;
                    continue;
                }

                int before = differences.Count;
                Compare("body " + a.Id, a, b, differences, 0, new HashSet<object>(ByReference.Instance));
                if (differences.Count > before) bodiesDiffering++;
            }

            // What each body senses through: a wired contact or damage sense is its own
            // organism's array, in both worlds, and the reserve and the field are the world's.
            CheckWiring("stepped", first, differences);
            CheckWiring("restored", second, differences);

            // The harness's own members, bodies matched by id, and the solver world's.
            CompareHarness(first.Sim, second.Sim, differences);

            // The fields, through the world's own state writer, and the placer through its own.
            string fieldsA = StateDigest(first.World), fieldsB = StateDigest(second.World);
            if (fieldsA != fieldsB) differences.Add("world state digest: " + fieldsA + " against " + fieldsB);

            if (first.Sim.Volume != null)
            {
                string placerA = Digest(first.Sim.Volume.WriteState);
                string placerB = Digest(second.Sim.Volume.WriteState);
                if (placerA != placerB) differences.Add("placer state digest: " + placerA + " against " + placerB);
            }

            // And then the two step side by side, which is what a restore is for.
            TwinStep(first, second, differences);

            // By member first: a scratch array the next step fills before it reads differs on
            // every body and a fault differs on a few, and the count of bodies tells them apart.
            var byMember = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var examples = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string line in differences)
            {
                int colon = line.IndexOf(':');
                string path = colon < 0 ? line : line.Substring(0, colon);
                int space = path.IndexOf(' ');
                int dot = path.IndexOf('.');
                string owner = dot < 0 ? path : path.Substring(0, dot);
                string member = dot < 0 ? "(itself)" : System.Text.RegularExpressions.Regex.Replace(path.Substring(dot + 1), @"\[\d+\]", "[]");
                string kind = space < 0 ? path : path.Substring(0, space);
                string key = kind + " ." + member;

                if (!byMember.TryGetValue(key, out HashSet<string> owners)) byMember[key] = owners = new HashSet<string>(StringComparer.Ordinal);
                owners.Add(owner);
                if (!examples.ContainsKey(key)) examples[key] = line;
            }

            var keys = new List<string>(byMember.Keys);
            keys.Sort((x, y) => byMember[x].Count.CompareTo(byMember[y].Count));

            Console.WriteLine("  member                                           owners differing   first");
            foreach (string key in keys)
            {
                Console.WriteLine("  " + key.PadRight(48) + byMember[key].Count.ToString(CultureInfo.InvariantCulture).PadLeft(16) + "   " + examples[key]);
            }

            Console.WriteLine();

            int shown = 0;
            foreach (string line in differences)
            {
                if (shown++ >= 20) { Console.WriteLine("  ... " + (differences.Count - 20) + " more"); break; }
                Console.WriteLine("  " + line);
            }

            Console.WriteLine();
            Console.WriteLine(
                differences.Count == 0
                    ? "PASS: the restored world is the stepped world in every compared member"
                    : "FAIL: " + differences.Count + " difference(s) in " + bodiesDiffering + " body(ies) and " +
                      organismsDiffering + " organism(s)");

            return differences.Count == 0 ? 0 : 1;
        }

        private sealed class Restored
        {
            public World World;
            public Simulation Sim;
            public Sampler Sampler;
            public int MetabolicSteps;
            public double BestSpeedEver;
            public double BestSpeedAt;
            public bool AssayFired;
        }

        /// <summary>The seed a run was launched with, read off its manifest.</summary>
        private static ulong SeedOf(string runDirectory)
        {
            string text = File.ReadAllText(Path.Combine(runDirectory, "run.json"));
            var match = System.Text.RegularExpressions.Regex.Match(text, "\"seed\"\\s*:\\s*(\\d+)");
            if (!match.Success) throw new InvalidDataException("No seed in " + runDirectory + "/run.json.");
            return ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>A header for a world founded here: the physics step from the config's dt.</summary>
        private static CheckpointHeader FoundingHeader(RunConfig config, ulong seed)
        {
            float dt = EnvBinding.ResolvePhysicsStep(config.PhysicsStepSeconds, out int perMetabolic);

            return new CheckpointHeader
            {
                Version = Checkpoint.Version,
                Seed = seed,
                PhysicsStepSeconds = dt,
                StepsPerMetabolicStep = perMetabolic,
                ConfigHash = config.Hash(),
                EngineVersion = "fidelity",
                SourceArm = "fidelity",
                SourceRun = "fidelity",
                ReportEvery = 20,
            };
        }

        private static Restored Found(
            RunConfig config, IReadOnlyList<Genome> pool, CheckpointHeader header, int threads,
            string runDirectory)
        {
            Directory.CreateDirectory(runDirectory);

            var world = new World(config, header.Seed, pool);
            var sim = new Simulation(
                world, header.Seed, header.PhysicsStepSeconds, header.StepsPerMetabolicStep, threads, runDirectory);

            return new Restored
            {
                World = world, Sim = sim,
                Sampler = new Sampler { PoolNamed = config.FoundingTricklePoolCount > 0 },
            };
        }

        private static Restored Restore(
            string path, RunConfig config, IReadOnlyList<Genome> pool, CheckpointHeader header,
            int threads, string runDirectory)
        {
            Directory.CreateDirectory(runDirectory);

            var world = new World(config, header.Seed, pool);
            var sim = new Simulation(
                world, header.Seed, header.PhysicsStepSeconds, header.StepsPerMetabolicStep, threads, runDirectory);
            var sampler = new Sampler { PoolNamed = config.FoundingTricklePoolCount > 0 };

            var restored = new Restored { World = world, Sim = sim, Sampler = sampler };

            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                BinaryReader r = reader.Reader;

                StateIo.Tag(r, "PAYL");
                world.ReadState(r);
                sim.ReadState(r);
                sampler.ReadState(r);

                StateIo.Tag(r, "LOOP");
                restored.MetabolicSteps = r.ReadInt32();
                restored.BestSpeedEver = r.ReadDouble();
                restored.BestSpeedAt = r.ReadDouble();
                restored.AssayFired = r.ReadBoolean();

                StateIo.Tag(r, "PEND");
            }

            return restored;
        }

        /// <summary>The run loop's checkpoint, written from a restored-and-stepped world.</summary>
        private static void WriteAgain(string path, Restored stepped, CheckpointHeader source, RunConfig config)
        {
            World world = stepped.World;
            Simulation sim = stepped.Sim;

            sim.SettleBeforeCheckpoint();
            world.DrainLineageEvents();

            var header = new CheckpointHeader
            {
                Version = Checkpoint.Version,
                Seconds = world.ElapsedSeconds,
                Seed = source.Seed,
                PhysicsSteps = sim.Steps,
                PhysicsStepSeconds = source.PhysicsStepSeconds,
                StepsPerMetabolicStep = source.StepsPerMetabolicStep,
                ConfigHash = config.Hash(),
                CoreHash = source.CoreHash,
                DynamicsHash = source.DynamicsHash,
                FarmHash = source.FarmHash,
                EngineVersion = source.EngineVersion,
                SourceArm = source.SourceArm,
                SourceRun = source.SourceRun,
                ReportEvery = source.ReportEvery,
                PoseEverySeconds = source.PoseEverySeconds,
                DigestEverySteps = source.DigestEverySteps,
                CheckpointEverySeconds = source.CheckpointEverySeconds,
            };

            CheckpointWriter.Write(
                path,
                header,
                w =>
                {
                    StateIo.Tag(w, "PAYL");
                    world.WriteState(w);
                    sim.WriteState(w);
                    stepped.Sampler.WriteState(w);

                    StateIo.Tag(w, "LOOP");
                    w.Write(stepped.MetabolicSteps);
                    w.Write(stepped.BestSpeedEver);
                    w.Write(stepped.BestSpeedAt);
                    w.Write(stepped.AssayFired);

                    StateIo.Tag(w, "PEND");
                });
        }

        private static string StateDigest(World world) => Digest(world.WriteState);

        private static string Digest(Action<BinaryWriter> write)
        {
            using (var buffer = new MemoryStream())
            using (var w = new BinaryWriter(buffer))
            {
                write(w);
                w.Flush();
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(buffer.ToArray());
                    return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Steps a world on until no body waits for a rebuild on a changed plan. Returns the
        /// physics steps it took, 0 when there was nothing to wait for.
        /// </summary>
        private static int StepToARebuiltPlan(Restored world)
        {
            // Two growth steps' worth of physics steps is a guard on this code, not a tolerance:
            // every metabolic step rebuilds the plans its world step changed and every growth
            // step the module rule's, so the count is zero after the first of either.
            int limit = 2 * (int)Math.Ceiling(
                            Math.Max(world.World.Config.GrowthStepSeconds, Simulation.MetabolicStepSeconds) /
                            world.Sim.PhysicsDt) + 2 * world.Sim.StepsPerMetabolicStep;

            int steps = 0;

            while (world.Sim.PlanChangesPending() > 0)
            {
                if (steps++ >= limit)
                {
                    throw new InvalidOperationException(
                        "A body's plan was still waiting for its rebuild after " + limit +
                        " physics steps, two growth steps' worth. Every metabolic step rebuilds " +
                        "the plans it changed, so this is a body the harness has stopped rebuilding.");
                }

                world.Sim.Step();
            }

            return steps;
        }

        // ------------------------------------------------------------------ the wiring

        /// <summary>
        /// Every body's senses against its organism: a wired contact or damage sense is that
        /// organism's own array, and the reserve and the field are the organism and the world's.
        /// </summary>
        /// <remarks>
        /// Asked of both worlds because a fault on either side is a fault: the stepped world is
        /// the run, and a wiring it holds that is not the organism's array is one the checkpoint
        /// writer refuses; the restored world is the resume, and one it holds is a restore that
        /// wired the wrong thing.
        /// </remarks>
        private static void CheckWiring(string which, Restored world, List<string> into)
        {
            var organisms = new Dictionary<long, Organism>();
            foreach (Organism o in world.World.Living) organisms[o.Id] = o;

            foreach (Creature body in world.Sim.Dynamics.Creatures)
            {
                string path = "body " + body.Id + ".Senses.";

                if (!organisms.TryGetValue(body.Id, out Organism organism))
                {
                    into.Add("body " + body.Id + ".(organism): no living organism in the " + which + " world");
                    continue;
                }

                CreatureSenses senses = body.Senses;

                if (senses.Contact != null && !ReferenceEquals(senses.Contact, organism.PartContact))
                {
                    into.Add(path + "Contact (wiring): not organism " + organism.Id + "'s PartContact in the " + which + " world");
                }

                if (senses.Damage != null && !ReferenceEquals(senses.Damage, organism.PartDamage))
                {
                    into.Add(path + "Damage (wiring): not organism " + organism.Id + "'s PartDamage in the " + which + " world");
                }

                if (!ReferenceEquals(senses.Reserve, organism))
                {
                    into.Add(path + "Reserve (wiring): not organism " + organism.Id + " in the " + which + " world");
                }

                if (!ReferenceEquals(senses.Nutrients, world.World.Nutrients))
                {
                    into.Add(path + "Nutrients (wiring): not the " + which + " world's field");
                }
            }
        }

        // ------------------------------------------------------------------ the harness

        // The harness's members that are not compared as members: the body table and the list in
        // stepping order, compared by id below; the scratch a step fills before it reads (the
        // departed set, the contact list handed to Core, the condemned list, the spread's column
        // flags); the profile's two denominators, which count this process's work as the wall
        // clock does; the clocks themselves; the world and the solver world, compared elsewhere;
        // the solver's config and the bed, which are functions of the config; the placer, which
        // is compared by digest; and the dump writer, which knows its own directory.
        private static readonly HashSet<string> HarnessNotCompared = new HashSet<string>(StringComparer.Ordinal)
        {
            "_bodies", "_order", "_departed", "_contacts", "_condemned",
            "_columnHeld", "_columnHeldAbsorptive", "_bodyStepSum", "_linkStepSum",
            "RunClock", "WritersClock",
            "<World>k__BackingField", "<Dynamics>k__BackingField", "<Solver>k__BackingField",
            "<Volume>k__BackingField", "<Floor>k__BackingField", "<Dumps>k__BackingField",
        };

        // The solver world's: the creature list, compared body by body; the contact grid, its
        // neighbour scratch and the step's overlap list, all rebuilt each step before they are
        // read; the five per-step overlap counts, zeroed at the top of the contact pass; the
        // census's per-slab scratch, sized on the first step after a restore and written before
        // it is read on every step (CloseContactStep); and the digest writer, a recording.
        private static readonly HashSet<string> DynamicsNotCompared = new HashSet<string>(StringComparer.Ordinal)
        {
            "_creatures", "_grid", "_neighbourScratch", "_overlaps",
            "_censusBad", "_censusCounts", "_censusEvents",
            "<OverlapPairsThisStep>k__BackingField", "<OverlapPairsJointedThisStep>k__BackingField",
            "<OverlapPairsHeldThisStep>k__BackingField", "<OverlapBodiesThisStep>k__BackingField",
            "<BedOrGlassBodiesThisStep>k__BackingField",
        };

        /// <summary>
        /// The harness's own members and the solver world's, and every body's bookkeeping matched
        /// by id: what a restore of the harness has to put back beside the bodies themselves.
        /// </summary>
        private static void CompareHarness(Simulation a, Simulation b, List<string> into)
        {
            foreach (FieldInfo field in AllFields(typeof(Simulation)))
            {
                if (field.IsStatic || HarnessNotCompared.Contains(field.Name) || IsSkippedMember(field)) continue;
                if (IsSkipped(field.FieldType)) continue;

                Compare("simulation harness." + field.Name, field.GetValue(a), field.GetValue(b), into, 1,
                        new HashSet<object>(ByReference.Instance));
            }

            foreach (FieldInfo field in AllFields(typeof(DynamicsWorld)))
            {
                if (field.IsStatic || DynamicsNotCompared.Contains(field.Name) || IsSkippedMember(field)) continue;
                if (field.Name.StartsWith("_digest", StringComparison.Ordinal)) continue;
                if (IsSkipped(field.FieldType)) continue;

                Compare("solver world." + field.Name, field.GetValue(a.Dynamics), field.GetValue(b.Dynamics), into, 1,
                        new HashSet<object>(ByReference.Instance));
            }

            // The body table, by id. Its solver and its organism are compared as bodies and as
            // organisms; here they are asked only to be the ones the two worlds hold under the id.
            FieldInfo tableField = typeof(Simulation).GetField("_bodies", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo orderField = typeof(Simulation).GetField("_order", BindingFlags.Instance | BindingFlags.NonPublic);
            var tableA = (IDictionary)tableField.GetValue(a);
            var tableB = (IDictionary)tableField.GetValue(b);

            var ids = new List<long>();
            foreach (object key in tableA.Keys) ids.Add((long)key);
            foreach (object key in tableB.Keys) if (!tableA.Contains(key)) ids.Add((long)key);
            ids.Sort();

            foreach (long id in ids)
            {
                object ba = tableA.Contains(id) ? tableA[id] : null;
                object bb = tableB.Contains(id) ? tableB[id] : null;
                string path = "harness-body " + id;

                if (ba == null || bb == null)
                {
                    into.Add(path + ": " + (ba == null ? "missing in the stepped harness" : "missing after the restore"));
                    continue;
                }

                AskTheBodyIsTheWorlds(path, ba, a, id, into);
                AskTheBodyIsTheWorlds(path, bb, b, id, into);

                foreach (FieldInfo field in AllFields(ba.GetType()))
                {
                    if (field.IsStatic || field.Name == "Solver" || field.Name == "Creature") continue;

                    Compare(path + "." + field.Name, field.GetValue(ba), field.GetValue(bb), into, 1,
                            new HashSet<object>(ByReference.Instance));
                }
            }

            // And the order the harness walks them in, which every accumulator is summed in.
            string orderA = OrderOf((IList)orderField.GetValue(a));
            string orderB = OrderOf((IList)orderField.GetValue(b));
            if (orderA != orderB) into.Add("simulation harness._order: " + orderA + " against " + orderB);
        }

        private static void AskTheBodyIsTheWorlds(string path, object entry, Simulation sim, long id, List<string> into)
        {
            Type type = entry.GetType();
            var solver = (Creature)type.GetField("Solver").GetValue(entry);
            var creature = (Organism)type.GetField("Creature").GetValue(entry);

            if (!ReferenceEquals(solver, sim.Dynamics.ById(id)))
            {
                into.Add(path + ".Solver (wiring): not the solver world's body " + id);
            }

            Organism living = null;
            foreach (Organism o in sim.World.Living) if (o.Id == id) { living = o; break; }

            if (!ReferenceEquals(creature, living))
            {
                into.Add(path + ".Creature (wiring): not the world's living organism " + id);
            }
        }

        private static string OrderOf(IList bodies)
        {
            var text = new System.Text.StringBuilder();

            foreach (object entry in bodies)
            {
                var creature = (Organism)entry.GetType().GetField("Creature").GetValue(entry);
                if (text.Length > 0) text.Append(',');
                text.Append(creature.Id.ToString(CultureInfo.InvariantCulture));
            }

            return bodies.Count + " bodies [" + (text.Length > 120 ? text.ToString(0, 120) + "…" : text.ToString()) + "]";
        }

        // ------------------------------------------------------------------ the twin step

        /// <summary>How far the two worlds are stepped side by side, in metabolic steps.</summary>
        private const int TwinMetabolicSteps = 2;

        /// <summary>
        /// Steps the stepped world and its restored twin together, comparing the solver's digest
        /// after every physics step and the world's state after every metabolic step, and names
        /// the first step that differs with the lowest body id at which it does.
        /// </summary>
        private static void TwinStep(Restored a, Restored b, List<string> into)
        {
            int metabolic = 0;
            int step = 0;

            while (metabolic < TwinMetabolicSteps)
            {
                bool ma = a.Sim.Step();
                bool mb = b.Sim.Step();
                step++;

                string at = "twin-step " + step.ToString(CultureInfo.InvariantCulture);

                if (ma != mb)
                {
                    into.Add(at + ".cadence: the two worlds disagree about whether this was a metabolic step");
                    return;
                }

                if (a.Sim.Dynamics.Digest() != b.Sim.Dynamics.Digest())
                {
                    into.Add(at + ".solver digest: parted; the lowest body that differs is " +
                             LowestDifferingBody(a.Sim.Dynamics, b.Sim.Dynamics));
                    return;
                }

                if (!ma) continue;

                metabolic++;

                if (StateDigest(a.World) != StateDigest(b.World))
                {
                    into.Add(at + ".world digest: parted at metabolic step " + metabolic +
                             "; the lowest organism that differs is " + LowestDifferingOrganism(a.World, b.World));
                    return;
                }
            }

            Console.WriteLine(
                "  twin step: the two worlds agreed after each of " + step + " physics steps and " +
                TwinMetabolicSteps + " metabolic steps");
        }

        private static string LowestDifferingBody(DynamicsWorld a, DynamicsWorld b)
        {
            var hashesA = new SortedDictionary<long, ulong>();
            var hashesB = new SortedDictionary<long, ulong>();

            foreach (Creature body in a.Creatures) hashesA[body.Id] = BodyDigest(body);
            foreach (Creature body in b.Creatures) hashesB[body.Id] = BodyDigest(body);

            var ids = new SortedSet<long>(hashesA.Keys);
            ids.UnionWith(hashesB.Keys);

            foreach (long id in ids)
            {
                bool inA = hashesA.TryGetValue(id, out ulong ha);
                bool inB = hashesB.TryGetValue(id, out ulong hb);

                if (!inA || !inB) return "body " + id + " (" + (inA ? "missing in the restored twin" : "missing in the stepped world") + ")";
                if (ha != hb) return "body " + id;
            }

            return "none: every body agrees and the list order does not";
        }

        /// <summary>One body's share of <see cref="DynamicsWorld.Digest"/>, on its own.</summary>
        private static ulong BodyDigest(Creature body)
        {
            ulong hash = 14695981039346656037UL;

            for (int i = 0; i < body.Links; i++)
            {
                for (int k = 0; k < 3; k++) Mix(ref hash, body.Position[3 * i + k]);
                for (int k = 0; k < 4; k++) Mix(ref hash, body.Rotation[4 * i + k]);
                for (int k = 0; k < 3; k++) Mix(ref hash, body.Spin[3 * i + k]);
                for (int k = 0; k < 3; k++) Mix(ref hash, body.Velocity[3 * i + k]);
            }

            return hash;
        }

        private static void Mix(ref ulong hash, double v)
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);

            for (int b = 0; b < 8; b++)
            {
                hash ^= (bits >> (8 * b)) & 0xFF;
                hash *= 1099511628211UL;
            }
        }

        private static string LowestDifferingOrganism(World a, World b)
        {
            var byId = new Dictionary<long, Organism>();
            foreach (Organism o in b.Living) byId[o.Id] = o;

            var ids = new List<long>();
            foreach (Organism o in a.Living) ids.Add(o.Id);
            ids.Sort();

            foreach (long id in ids)
            {
                Organism oa = null;
                foreach (Organism o in a.Living) if (o.Id == id) { oa = o; break; }

                if (!byId.TryGetValue(id, out Organism ob)) return "organism " + id + " (missing in the restored twin)";

                var found = new List<string>();
                Compare("organism " + id, oa, ob, found, 0, new HashSet<object>(ByReference.Instance));
                if (found.Count > 0) return found[0];
            }

            return "none: every organism agrees, so the difference is in the world's own counters, " +
                   "queues or fields";
        }

        // ------------------------------------------------------------------ the comparison

        private const int MaxDepth = 6;
        private const int MaxPerObject = 6;

        // Back-references and shared services: compared once elsewhere or not state at all.
        private static readonly HashSet<Type> Skipped = new HashSet<Type>
        {
            typeof(World), typeof(SolverConfig), typeof(RunConfig), typeof(PartShapeRegistry),
            typeof(Delegate), typeof(Type),
        };

        // Every ISensorField was skipped here until 2026-09-25, as a shared service, which took each
        // body's CreatureSenses out of the comparison with it; a body's senses are its own and
        // are compared now, less the per-step arrays named in FilledBeforeReadIn.
        private static bool IsSkipped(Type t)
        {
            foreach (Type s in Skipped) if (s.IsAssignableFrom(t)) return true;
            return typeof(IMatterField).IsAssignableFrom(t);
        }

        // What one step fills before it reads, so a stepped body holds the last step's numbers
        // and a restored one holds zeros, and the difference is not a fault. The list is
        // Creature.State's remarks made explicit: the articulated-inertia scratch, the external
        // force and the joint torque cleared at the top of the step, the drag and drive hand-off
        // buffers written for every link, the limit's implicit term written for every degree of
        // freedom, the ledger's pre-step copies, the brain's output, the step's own overlap list
        // (the held one is compared through _heldCount and the ids it counts), the applied
        // torque a probe narrows, Core's contact list the harness hands over each metabolic
        // step, and D110's exposure and up, which the harness reads off the restored rotations
        // before the world prices anything on them. A member added to the solver that the next
        // step reads before it writes is not on this list, and this check will name it.
        private static readonly HashSet<string> FilledBeforeRead = new HashSet<string>(StringComparer.Ordinal)
        {
            "IaA", "IaB", "IaC", "Pa", "Un", "Uf", "Dinv", "Ubar", "Acc", "Fext", "Tau",
            "DragForce", "DragTorque", "DriveTorque", "LimitImplicit",
            "_preVelocity", "_preSpin", "_preRelativeSpin", "_preJointRate", "_passiveTorque",
            "DriveSignal", "_overlapIds", "_overlapCount", "_heldIds", "AppliedTorque",

            // D114's link pair beside each overlap id: the same step's list, filled with it.
            // Organism.PartContact stood here until 2026-09-25 and does not belong: the physics
            // steps read it before the next metabolic step writes it (sticky until a plan change
            // then, the last metabolic step's record since D123), so it is compared.
            "_overlapParts",
            "<PartExposure>k__BackingField", "<UpInBody>k__BackingField",

            // The reserve a gestating body started the step with (2026-09-24), filled at the top
            // of World.Step before Gestate reads it.
            "ReserveAtStepStart",
        };

        // The same, for a member whose name is too common to skip everywhere, qualified by the
        // type that declares it. CreatureSenses' per-step arrays: Sample fills each one a brain
        // can read before Brain.Step reads it, on every physics step (a channel the mask does not
        // read is neither filled nor read). And its reserve, a back-reference to the organism,
        // which is compared as an organism and asked to be the body's own in CheckWiring.
        private static readonly HashSet<string> FilledBeforeReadIn = new HashSet<string>(StringComparer.Ordinal)
        {
            "CreatureSenses._depth", "CreatureSenses._up", "CreatureSenses._angle",
            "CreatureSenses._rate", "CreatureSenses._flow", "CreatureSenses._chemical",
            "CreatureSenses._energy",
            "CreatureSenses.<Reserve>k__BackingField",
        };

        private static bool IsSkippedName(string name) =>
            FilledBeforeRead.Contains(name) ||
            name.IndexOf("Ticks", StringComparison.Ordinal) >= 0 ||
            name.IndexOf("_trace", StringComparison.Ordinal) >= 0 ||
            name == "_config" || name == "_shapes" || name == "_body" || name == "_world";

        private static bool IsSkippedMember(FieldInfo field) =>
            IsSkippedName(field.Name) ||
            FilledBeforeReadIn.Contains(field.DeclaringType.Name + "." + field.Name);

        private static void Compare(
            string path, object a, object b, List<string> into, int depth, HashSet<object> seen)
        {
            if (ReferenceEquals(a, b)) return;

            if (a == null || b == null)
            {
                into.Add(path + ": " + (a == null ? "null" : "value") + " against " + (b == null ? "null" : "value"));
                return;
            }

            Type type = a.GetType();

            if (type != b.GetType())
            {
                into.Add(path + ": type " + type.Name + " against " + b.GetType().Name);
                return;
            }

            if (IsSkipped(type)) return;

            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type.IsEnum)
            {
                if (!a.Equals(b)) into.Add(path + ": " + Show(a) + " against " + Show(b));
                return;
            }

            if (type.IsArray)
            {
                var xa = (Array)a;
                var xb = (Array)b;

                if (xa.Length != xb.Length)
                {
                    into.Add(path + ": length " + xa.Length + " against " + xb.Length);
                    return;
                }

                int reported = 0;

                for (int i = 0; i < xa.Length; i++)
                {
                    object ea = xa.GetValue(i), eb = xb.GetValue(i);
                    Type et = type.GetElementType();

                    if (et.IsPrimitive || et == typeof(string))
                    {
                        if (Equals(ea, eb)) continue;
                        if (reported++ < MaxPerObject) into.Add(path + "[" + i + "]: " + Show(ea) + " against " + Show(eb));
                        continue;
                    }

                    Compare(path + "[" + i + "]", ea, eb, into, depth + 1, seen);
                }

                if (reported > MaxPerObject) into.Add(path + ": ... " + (reported - MaxPerObject) + " more elements differ");
                return;
            }

            if (depth >= MaxDepth) return;

            if (!type.IsValueType)
            {
                if (seen.Contains(a)) return;
                seen.Add(a);
            }

            if (a is IList la && b is IList lb)
            {
                if (la.Count != lb.Count)
                {
                    into.Add(path + ": count " + la.Count + " against " + lb.Count);
                    return;
                }

                for (int i = 0; i < la.Count; i++) Compare(path + "[" + i + "]", la[i], lb[i], into, depth + 1, seen);
                return;
            }

            if (a is IDictionary) return;

            foreach (FieldInfo field in AllFields(type))
            {
                if (field.IsStatic || IsSkippedMember(field)) continue;
                if (IsSkipped(field.FieldType)) continue;

                Compare(path + "." + field.Name, field.GetValue(a), field.GetValue(b), into, depth + 1, seen);
            }
        }

        private static IEnumerable<FieldInfo> AllFields(Type type)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    yield return f;
                }
            }
        }

        private static string Show(object v)
        {
            switch (v)
            {
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case null: return "null";
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
