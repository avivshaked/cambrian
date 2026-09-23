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

            if (Directory.Exists(checkpointPath))
            {
                // A run directory: found its world from its own config and seed and step it live
                // to the second asked for. This is the one arrangement that can show a piece of
                // state a live process carries from birth and no checkpoint has ever held,
                // because the world compared has never been restored.
                config = RunDirectory.ReadConfig(checkpointPath, out _);
                ulong seed = SeedOf(checkpointPath);
                header = FoundingHeader(config, seed);

                Console.WriteLine("  founding   " + checkpointPath + " (seed " + seed + ") and stepping live to " + F(seconds) + " s on " + threads + " threads");
                Console.WriteLine("  then writing a checkpoint and restoring it");

                first = Found(config, header, threads, Path.Combine(scratch, "first"));

                while (first.World.ElapsedSeconds < seconds - 1e-9) first.Sim.Step();
            }
            else
            {
                header = CheckpointReader.ReadHeader(checkpointPath);
                string sourceRun = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(checkpointPath)));
                config = RunDirectory.ReadConfig(sourceRun, out _);

                Console.WriteLine("  checkpoint " + checkpointPath + " at " + F(header.Seconds) + " s");
                Console.WriteLine("  stepping   " + F(seconds) + " s on " + threads + " threads, then writing and restoring again");

                first = Restore(checkpointPath, config, header, threads, Path.Combine(scratch, "first"));

                double until = first.World.ElapsedSeconds + seconds;
                while (first.World.ElapsedSeconds < until - 1e-9) first.Sim.Step();
            }

            string again = Path.Combine(scratch, "again.ckpt");
            WriteAgain(again, first, header, config);

            Restored second = Restore(again, config, CheckpointReader.ReadHeader(again), threads, Path.Combine(scratch, "second"));

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

            // The fields and the placer, through the world's own state writer: one digest each.
            string fieldsA = StateDigest(first.World), fieldsB = StateDigest(second.World);
            if (fieldsA != fieldsB) differences.Add("world state digest: " + fieldsA + " against " + fieldsB);

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

        private static Restored Found(RunConfig config, CheckpointHeader header, int threads, string runDirectory)
        {
            Directory.CreateDirectory(runDirectory);

            var world = new World(config, header.Seed);
            var sim = new Simulation(
                world, header.Seed, header.PhysicsStepSeconds, header.StepsPerMetabolicStep, threads, runDirectory);

            return new Restored { World = world, Sim = sim, Sampler = new Sampler() };
        }

        private static Restored Restore(
            string path, RunConfig config, CheckpointHeader header, int threads, string runDirectory)
        {
            Directory.CreateDirectory(runDirectory);

            var world = new World(config, header.Seed);
            var sim = new Simulation(
                world, header.Seed, header.PhysicsStepSeconds, header.StepsPerMetabolicStep, threads, runDirectory);
            var sampler = new Sampler();

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

        private static string StateDigest(World world)
        {
            using (var buffer = new MemoryStream())
            using (var w = new BinaryWriter(buffer))
            {
                world.WriteState(w);
                w.Flush();
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(buffer.ToArray());
                    return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
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

        private static bool IsSkipped(Type t)
        {
            foreach (Type s in Skipped) if (s.IsAssignableFrom(t)) return true;
            return typeof(IMatterField).IsAssignableFrom(t) || typeof(ISensorField).IsAssignableFrom(t);
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
            "_overlapParts",
            "<PartContact>k__BackingField",
            "<PartExposure>k__BackingField", "<UpInBody>k__BackingField",
        };

        private static bool IsSkippedName(string name) =>
            FilledBeforeRead.Contains(name) ||
            name.IndexOf("Ticks", StringComparison.Ordinal) >= 0 ||
            name.IndexOf("_trace", StringComparison.Ordinal) >= 0 ||
            name == "_config" || name == "_shapes" || name == "_body" || name == "_world";

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
                if (field.IsStatic || IsSkippedName(field.Name)) continue;
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
