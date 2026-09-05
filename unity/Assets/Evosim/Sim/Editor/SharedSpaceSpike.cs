using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// What does one shared volume cost against today's tiling? — D076's measurement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The owner has ruled (D076) that creatures will share one volume: a world without contact
    /// cannot have predation, crowding or anything else that needs two animals in the same place.
    /// Today they are tiled 100 m apart on a lattice (<c>Ecosystem.TileSpacing</c>, §6.3) and can
    /// never touch. Moving them into one volume adds broadphase pairs that are actually near each
    /// other and contacts that actually resolve, and DESIGN.md §5A.9 assumes that cost is "rare
    /// and local". <b>This replaces the assumption with a number</b>, at the populations the
    /// ecosystem really runs and at three candidate footprints.
    /// </para>
    /// <para>
    /// <b>Real creatures, the real fluid, the real drive.</b> Founders are drawn from
    /// <see cref="GenomeFactory.Founder"/> with the reference world's
    /// <see cref="RandomGenomeOptions"/>, developed, and built through
    /// <see cref="PhenotypeBuilder"/>; <see cref="FluidEnvironment"/> applies drag and buoyancy
    /// exactly as <c>Ecosystem.Step</c> does, because the fluid is most of the per-body cost and a
    /// spike without it would measure the wrong loop. Joints are driven by the test sine at full
    /// amplitude so nothing sleeps — Spike 01's first run reported numbers two orders of magnitude
    /// too good because PhysX slept an unactuated scene, and every cell here reports awake fraction
    /// and mean body speed for that reason.
    /// </para>
    /// <para>
    /// <b>What it does not simulate:</b> brains and sensors (the drive is the test sine, as in
    /// <see cref="ThroughputSurvey"/>), and the economy. Both are per-creature managed work that is
    /// identical in the two arms, so leaving them out sharpens the contrast the spike is for; it
    /// also means <b>ms/step here is not the ecosystem's ms/step</b>, and the answer to "what
    /// population holds real time" is an upper bound on population, not a promise.
    /// </para>
    /// <para>
    /// <b>Placement is rejection-sampled on bounding spheres and never overlaps.</b> Two
    /// articulations born inside each other are pushed apart by PhysX's depenetration pass, which
    /// is momentum from nowhere (logbook/0007) — it would both corrupt the contact count and give
    /// every shared cell a launch transient that no tiled cell has. The one exception is the
    /// <c>contact-check</c> cell, which exists only to prove the contact instruments report and is
    /// excluded from every answer.
    /// </para>
    /// <para>
    /// <b>The tiled arm is what the ecosystem runs today, which is not what the spec for this
    /// spike described.</b> The spec said tiled creatures were kept apart by "mutually ignoring
    /// layers"; that was Spike 01. <c>PhenotypeBuilder</c> puts every part on one layer and
    /// <c>FluidEnvironment.ConfigureScene(selfCollision: true)</c> leaves that layer colliding
    /// with itself, so today's tiling separates creatures by 100 m of distance alone, with
    /// creature-to-creature collision enabled and simply never reached. The tiled arm here is
    /// that. The layer-ignoring variant is available as <c>tiled-nc</c> for anyone who wants the
    /// broadphase difference isolated.
    /// </para>
    /// <para>
    /// Parameterised by environment, like every other harness here, and every setting is written
    /// into the header of its own output so a result is never separated from what produced it.
    /// </para>
    /// </remarks>
    public static class SharedSpaceSpike
    {
        [MenuItem("Evosim/Spike — shared space cost")]
        public static void RunFromMenu() => Run();

        // ---------------------------------------------------------------- cells and results

        private sealed class Cell
        {
            /// <summary>
            /// tiled (today's 100 m lattice) | tiled-nc (the same with layer collisions off) |
            /// shared (one volume) | contact-check (the instrument test, not a measurement)
            /// </summary>
            public string Space;

            public int Population;

            /// <summary>Side of the shared volume in metres. The tile spacing, for a tiled cell.</summary>
            public float FootprintMetres;

            public float DepthMetres;

            /// <summary>1-based; &gt; 1 only for the repeated cells that show the noise.</summary>
            public int Repeat;

            /// <summary>False for the control cells that measure what the instrument costs.</summary>
            public bool PartCounters;

            public bool EngineContacts;

            public bool IsVolume => Space == "shared" || Space == "contact-check";

            public string Label =>
                IsVolume
                    ? $"{Space} {FootprintMetres:0}x{FootprintMetres:0}x{DepthMetres:0} N={Population}"
                    : $"{Space} N={Population}";
        }

        private sealed class Result
        {
            public Cell Cell;
            public string Status = "ok";
            public string Note = "";

            public int Parts;
            public int Dof;
            public float MeanRadius;
            public float MaxRadius;
            public string Placement = "rejection";
            public long PlacementRejects;
            public double BuildMs;

            public double MsPerStep;
            public double MsP50;
            public double MsP95;
            public double FluidMs;
            public double PhysicsMs;
            public double SettleMs;

            public double CallbacksPerStep;
            public double EntersPerStep;

            /// <summary>Pairs the MonoBehaviour callbacks saw — zero in edit mode, see below.</summary>
            public double PairsPerStep;

            public double PairsPerBodyPerStep;
            public double EnginePairsPerStep;
            public double EnginePointsPerStep;
            public double ContactPointsPerStep;

            /// <summary>
            /// The contact count this cell reports, from whichever instrument was actually
            /// answering.
            /// </summary>
            /// <remarks>
            /// <b>MonoBehaviour collision messages are not dispatched in a non-playing editor</b>,
            /// with or without <c>ExecuteAlways</c>, and the spike runs from <c>-executeMethod</c>
            /// with the editor not in play mode. Measured, not assumed: a cell at 41.9 bodies per
            /// cubic metre took 1.57 ms of physics against 0.19 ms for the same population spread
            /// out — six to eight times the solver work, which is contacts by definition — while
            /// <see cref="SpikeContactCounter"/> reported zero and <c>Physics.ContactEvent</c>
            /// reported 767 pairs per step. So the engine event is the instrument, the callbacks
            /// are kept as the cross-check the spec asked for, and both are in the CSV.
            /// </remarks>
            public double ReportedPairsPerStep =>
                Cell.EngineContacts ? EnginePairsPerStep : PairsPerStep;

            public double AwakeFraction;
            public double MeanSpeed;
            public double MaxSpeed;
            public int NonFinite;
            public int LeftVolume;

            public double VolumeM3 =>
                Cell.IsVolume
                    ? Cell.FootprintMetres * Cell.FootprintMetres * Cell.DepthMetres
                    : double.NaN;

            public double BodiesPerM3 => Cell.Population / VolumeM3;

            public double RealTimeFactor => MsPerStep > 0 ? EnvDtForReport * 1000.0 / MsPerStep : 0;

            public float EnvDtForReport;
        }

        // ------------------------------------------------------------------------ the entry

        public static void Run()
        {
            DateTime startedAt = DateTime.UtcNow;

            float dt = Env("EVOSIM_SPIKE_DT", 0.01f);
            int warmup = (int)Env("EVOSIM_SPIKE_WARMUP", 200f);
            int steps = (int)Env("EVOSIM_SPIKE_STEPS", 1000f);
            float hz = Env("EVOSIM_SPIKE_HZ", 1.2f);
            ulong seed = EnvULong("EVOSIM_SPIKE_SEED", 1UL);
            int sampleEvery = Math.Max(1, (int)Env("EVOSIM_SPIKE_SAMPLE_EVERY", 100f));
            int repeats = Math.Max(1, (int)Env("EVOSIM_SPIKE_REPEATS", 3f));
            bool control = EnvBool("EVOSIM_SPIKE_CONTROL", true);
            bool partCounters = EnvBool("EVOSIM_SPIKE_CONTACT_COUNTERS", true);
            bool engineContacts = EnvBool("EVOSIM_SPIKE_ENGINE_CONTACTS", true);
            string bodyDraw = EnvString("EVOSIM_SPIKE_BODIES", "founder").ToLowerInvariant();

            RunConfig config = LoadConfig(out string configSource);

            float defaultDepth = Env("EVOSIM_SPIKE_DEPTH", config.WorldDepthMetres);
            int[] populations = EnvInts("EVOSIM_SPIKE_N", new[] { 250, 500, 1000, 2000 });
            string[] spaces = EnvStrings("EVOSIM_SPIKE_SPACES", "tiled,shared");
            (float Side, float Depth)[] footprints =
                EnvFootprints("EVOSIM_SPIKE_FOOTPRINTS", "10,20,50", defaultDepth);

            List<Cell> cells = BuildMatrix(
                spaces, populations, footprints, defaultDepth, repeats, control,
                partCounters, engineContacts);

            string outDir = OutputDirectory();
            Directory.CreateDirectory(outDir);
            string csvPath = Path.Combine(outDir, "results.csv");

            // The world the spike actually ran, written where the results are. A cost measured in
            // a world nobody can reconstruct is a number without a subject.
            File.WriteAllText(Path.Combine(outDir, "config.json"), RunConfigJson.Write(config));

            var header = new StringBuilder();
            header.AppendLine("=== Shared-space spike (D076) ===");
            header.AppendLine(
                $"Unity {Application.unityVersion}   dt={dt}   {warmup} warmup + {steps} measured steps   " +
                $"sine {hz} Hz   seed {seed}");
            header.AppendLine(
                $"solverIterations={Physics.defaultSolverIterations} " +
                $"velocityIterations={Physics.defaultSolverVelocityIterations} " +
                $"maxDepenetrationVelocity={FluidEnvironment.MaxDepenetrationVelocity} " +
                $"reuseCollisionCallbacks=true");
            header.AppendLine($"bodies={bodyDraw}   configHash={config.Hash()}   config={configSource}");
            header.AppendLine(
                $"fluid: density={config.Fluid.Density} drag={config.Fluid.DragCoefficient} " +
                $"addedMass={config.Fluid.AddedMassCoefficient} panels={config.Fluid.PanelsPerAxis} " +
                $"tissueExcessDensity={config.Fluid.TissueExcessDensity} " +
                $"neutralBodyVolume={config.Fluid.NeutralBodyVolume}");
            header.AppendLine(
                $"current: speed={config.Current.Speed} rolls={config.Current.Rolls} " +
                $"cell={config.Current.CellMetres} patches={config.HorizontalPatches}");
            header.AppendLine($"Unity processes at start: {UnityProcessCount()}");
            header.AppendLine($"output: {outDir}");

            Debug.Log(header.ToString());

            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;
            bool previousReuse = Physics.reuseCollisionCallbacks;
            bool previousInvoke = Physics.invokeCollisionCallbacks;

            Physics.simulationMode = SimulationMode.Script;
            Physics.reuseCollisionCallbacks = true;
            Physics.invokeCollisionCallbacks = true;

            var results = new List<Result>();

            try
            {
                File.WriteAllText(csvPath, CsvHeader() + Environment.NewLine);

                foreach (Cell cell in cells)
                {
                    Result result = Measure(
                        cell, config, dt, warmup, steps, hz, seed, sampleEvery, bodyDraw);

                    results.Add(result);

                    // Appended as it is produced: a killed spike keeps every cell it finished,
                    // for the same reason stats.jsonl is append-only (§9).
                    File.AppendAllText(csvPath, CsvRow(result) + Environment.NewLine);

                    Debug.Log(
                        $"{cell.Label}  {result.MsPerStep:0.###} ms/step  " +
                        $"{result.ReportedPairsPerStep:0.##} pairs/step  " +
                        $"awake {result.AwakeFraction:P0}  " +
                        $"speed {result.MeanSpeed:0.###} m/s  {result.Status}");
                }
            }
            finally
            {
                Physics.simulationMode = previousMode;
                Physics.gravity = previousGravity;
                Physics.reuseCollisionCallbacks = previousReuse;
                Physics.invokeCollisionCallbacks = previousInvoke;
            }

            string summary = Summarise(
                results, header.ToString(), startedAt, dt, warmup, steps, csvPath);

            File.WriteAllText(Path.Combine(outDir, "summary.md"), summary);
            Debug.Log(summary);
            Debug.Log($"Shared-space spike written to {outDir}");
        }

        // ------------------------------------------------------------------------ the matrix

        private static List<Cell> BuildMatrix(
            string[] spaces, int[] populations, (float Side, float Depth)[] footprints,
            float defaultDepth, int repeats, bool control, bool partCounters, bool engineContacts)
        {
            var cells = new List<Cell>();

            Array.Sort(populations);

            foreach (int n in populations)
            {
                foreach (string space in spaces)
                {
                    if (space == "shared")
                    {
                        foreach ((float side, float depth) in footprints)
                        {
                            cells.Add(new Cell
                            {
                                Space = space,
                                Population = n,
                                FootprintMetres = side,
                                DepthMetres = depth,
                                Repeat = 1,
                                PartCounters = partCounters,
                                EngineContacts = engineContacts,
                            });
                        }
                    }
                    else if (space == "contact-check")
                    {
                        // One per population, and no footprint: the instrument test sizes its own
                        // box to the smallest cube that holds the population at contact spacing,
                        // because a tight lattice scattered through a 10 x 10 x 60 m volume would
                        // be as contact-free as the measurement cells and would prove nothing.
                        cells.Add(new Cell
                        {
                            Space = space,
                            Population = n,
                            Repeat = 1,
                            PartCounters = partCounters,
                            EngineContacts = engineContacts,
                        });
                    }
                    else
                    {
                        cells.Add(new Cell
                        {
                            Space = space,
                            Population = n,
                            FootprintMetres = Ecosystem.TileSpacing,
                            DepthMetres = defaultDepth,
                            Repeat = 1,
                            PartCounters = partCounters,
                            EngineContacts = engineContacts,
                        });
                    }
                }
            }

            // The noise repeats: the two smallest shared cells, in the smallest footprint, run
            // three times in total. Cheap, and the only thing that says whether a shared/tiled
            // ratio of 1.2 is a cost or a wobble.
            bool hasShared = Array.IndexOf(spaces, "shared") >= 0;

            if (hasShared && repeats > 1 && footprints.Length > 0)
            {
                (float side, float depth) = footprints[0];
                int howMany = Math.Min(2, populations.Length);

                for (int i = 0; i < howMany; i++)
                {
                    for (int r = 2; r <= repeats; r++)
                    {
                        cells.Add(new Cell
                        {
                            Space = "shared",
                            Population = populations[i],
                            FootprintMetres = side,
                            DepthMetres = depth,
                            Repeat = r,
                            PartCounters = partCounters,
                            EngineContacts = engineContacts,
                        });
                    }
                }
            }

            // Controls: the largest shared cell of each footprint, run with the contact instrument
            // switched off. A counter that reports contacts is itself a cost — PhysX has to build
            // the contact report and the managed callback has to run — and a spike that never
            // measured its own instrument would attribute that cost to shared space.
            if (hasShared && control && populations.Length > 0 && (partCounters || engineContacts))
            {
                int biggest = populations[populations.Length - 1];

                foreach ((float side, float depth) in footprints)
                {
                    cells.Add(new Cell
                    {
                        Space = "shared",
                        Population = biggest,
                        FootprintMetres = side,
                        DepthMetres = depth,
                        Repeat = 1,
                        PartCounters = false,
                        EngineContacts = false,
                    });
                }
            }

            return cells;
        }

        // ------------------------------------------------------------------------- one cell

        private static Result Measure(
            Cell cell, RunConfig config, float dt, int warmup, int steps, float hz,
            ulong seed, int sampleEvery, string bodyDraw)
        {
            var result = new Result { Cell = cell, EnvDtForReport = dt };

            // tiled and tiled-nc differ in exactly this expression, and nothing else in the
            // harness knows which arm it is in. "tiled" is what the ecosystem runs today:
            // one layer, layer-vs-itself collisions ENABLED, creatures kept apart by 100 m of
            // distance rather than by a collision matrix.
            bool selfCollision = cell.Space != "tiled-nc";
            FluidEnvironment.ConfigureScene(selfCollision: selfCollision);

            var fluid = new FluidEnvironment(config.Fluid, config.Shapes, config.Current)
            {
                PatchCount = Mathf.Max(1, (int)config.HorizontalPatches),
            };

            var parent = new GameObject($"SharedSpaceSpike ({cell.Label})");

            var creatures = new CreatureInstance[cell.Population];
            var drivers = new EffectorDriver[cell.Population];
            var scratch = new float[cell.Population][];
            var origins = new Vector3[cell.Population];

            var buildWatch = Stopwatch.StartNew();

            try
            {
                // ---- develop first, place second, build third. Placement needs every bounding
                // sphere before it can place the first body, and building needs the placement.
                var phenotypes = new Phenotype[cell.Population];
                var radii = new float[cell.Population];

                var genomeRng = new Rng(seed);
                SensorChannel[] sensorPool = config.SensorPool();

                for (int i = 0; i < cell.Population; i++)
                {
                    Genome genome = bodyDraw == "viable"
                        ? GenomeFactory.RandomViable(
                            genomeRng, config.Genome, config.Development, minParts: 3)
                        : GenomeFactory.Founder(genomeRng, config.Genome, sensorPool);

                    phenotypes[i] = Developer.Develop(genome, config.Development);
                    radii[i] = BoundingRadius(phenotypes[i]);

                    result.Parts += phenotypes[i].PartCount;
                    result.MeanRadius += radii[i];
                    result.MaxRadius = Mathf.Max(result.MaxRadius, radii[i]);
                }

                result.MeanRadius /= Mathf.Max(1, cell.Population);

                Place(cell, radii, new Rng(seed + 977UL), origins, result);

                if (result.Status != "ok") return result;

                for (int i = 0; i < cell.Population; i++)
                {
                    creatures[i] = PhenotypeBuilder.Build(
                        phenotypes[i], origins[i], parent.transform, config.Shapes);

                    fluid.ApplyAddedMass(creatures[i]);

                    if (cell.PartCounters)
                    {
                        for (int b = 0; b < creatures[i].Bodies.Length; b++)
                        {
                            GameObject part = creatures[i].Bodies[b].gameObject;
                            part.AddComponent<SpikeContactCounter>();

                            // Contact reporting is opt-in per collider (it defaults to off), and
                            // without it PhysX resolves the contact and tells nobody: the first
                            // contact-check cell showed physics time rise six-fold while both
                            // counters read zero. Turned on only where the instrument is on, so
                            // the control cells pay nothing for it.
                            Collider collider = part.GetComponent<Collider>();
                            if (collider != null) collider.providesContacts = true;
                        }
                    }

                    drivers[i] = new EffectorDriver(creatures[i], dt);
                    scratch[i] = new float[Mathf.Max(1, creatures[i].TotalDof)];
                    result.Dof += creatures[i].TotalDof;
                }

                buildWatch.Stop();
                result.BuildMs = buildWatch.Elapsed.TotalMilliseconds;

                // A collection landing inside the measured window would be read as a slow step.
                GC.Collect();
                GC.WaitForPendingFinalizers();

                float t = 0f;

                for (int s = 0; s < warmup; s++)
                {
                    Step(creatures, drivers, scratch, fluid, dt, hz, t);
                    t += dt;
                }

                // Both reset unconditionally, including in the control cells that switch the
                // instruments off: a counter left holding the previous cell's total would report
                // contacts for a cell that was not counting any.
                SpikeContactCounter.Reset();
                _engineContactPairs = 0;
                _engineContactPoints = 0;

                if (cell.EngineContacts) SubscribeEngineContacts();

                var fluidWatch = new Stopwatch();
                var physicsWatch = new Stopwatch();
                var settleWatch = new Stopwatch();
                var perStepMs = new double[steps];

                double awakeSum = 0, speedSum = 0;
                int samples = 0;
                int completed = 0;

                for (int s = 0; s < steps; s++)
                {
                    long before = Stopwatch.GetTimestamp();

                    fluidWatch.Start();
                    for (int i = 0; i < creatures.Length; i++)
                    {
                        drivers[i].DriveTestSine(t, hz, scratch[i]);
                    }

                    fluid.Apply(creatures, dt);
                    fluidWatch.Stop();

                    physicsWatch.Start();
                    Physics.Simulate(dt);
                    physicsWatch.Stop();

                    settleWatch.Start();
                    fluid.Settle(creatures);
                    for (int i = 0; i < creatures.Length; i++) drivers[i].Settle();
                    settleWatch.Stop();

                    perStepMs[s] =
                        (Stopwatch.GetTimestamp() - before) * 1000.0 / Stopwatch.Frequency;

                    completed++;
                    t += dt;

                    // Sampled outside the timed sections: the awake check must not appear in the
                    // cost it is validating.
                    if (s % sampleEvery == 0 || s == steps - 1)
                    {
                        Sample(creatures, out double awake, out double meanSpeed,
                            out double maxSpeed, out int nonFinite);

                        awakeSum += awake;
                        speedSum += meanSpeed;
                        samples++;

                        result.MaxSpeed = Math.Max(result.MaxSpeed, maxSpeed);
                        result.NonFinite += nonFinite;

                        if (nonFinite > 0)
                        {
                            result.Status = "diverged";
                            result.Note = $"{nonFinite} non-finite bodies at step {s}";
                            break;
                        }
                    }
                }

                if (cell.EngineContacts) UnsubscribeEngineContacts();

                // Steps that actually ran, not steps that were asked for: a cell cut short by
                // divergence must not divide its totals by a thousand.
                int measured = Math.Max(1, completed);

                result.FluidMs = fluidWatch.Elapsed.TotalMilliseconds / measured;
                result.PhysicsMs = physicsWatch.Elapsed.TotalMilliseconds / measured;
                result.SettleMs = settleWatch.Elapsed.TotalMilliseconds / measured;
                result.MsPerStep = result.FluidMs + result.PhysicsMs + result.SettleMs;

                var sorted = new double[measured];
                Array.Copy(perStepMs, sorted, measured);
                Array.Sort(sorted);
                result.MsP50 = sorted[measured / 2];
                result.MsP95 = sorted[Mathf.Clamp((int)(measured * 0.95f), 0, measured - 1)];

                result.CallbacksPerStep = SpikeContactCounter.Callbacks / (double)measured;
                result.EntersPerStep = SpikeContactCounter.Enters / (double)measured;
                result.ContactPointsPerStep = SpikeContactCounter.ContactPoints / (double)measured;

                // Two colliders report every pair, so callbacks are twice the pairs. Every collider
                // in the scene carries a counter, so there is no pair with only one reporter.
                result.PairsPerStep = result.CallbacksPerStep / 2.0;
                result.EnginePairsPerStep = _engineContactPairs / (double)measured;
                result.EnginePointsPerStep = _engineContactPoints / (double)measured;
                result.PairsPerBodyPerStep = result.ReportedPairsPerStep / cell.Population;

                result.AwakeFraction = samples > 0 ? awakeSum / samples : 0;
                result.MeanSpeed = samples > 0 ? speedSum / samples : 0;
                result.LeftVolume = CountOutside(cell, creatures, origins);
            }
            finally
            {
                UnsubscribeEngineContacts();

                for (int i = 0; i < creatures.Length; i++) creatures[i]?.Destroy();

                UnityEngine.Object.DestroyImmediate(parent);

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return result;
        }

        private static void Step(
            CreatureInstance[] creatures, EffectorDriver[] drivers, float[][] scratch,
            FluidEnvironment fluid, float dt, float hz, float t)
        {
            for (int i = 0; i < creatures.Length; i++) drivers[i].DriveTestSine(t, hz, scratch[i]);

            fluid.Apply(creatures, dt);
            Physics.Simulate(dt);
            fluid.Settle(creatures);

            for (int i = 0; i < creatures.Length; i++) drivers[i].Settle();
        }

        private static void Sample(
            CreatureInstance[] creatures, out double awakeFraction, out double meanSpeed,
            out double maxSpeed, out int nonFinite)
        {
            long awake = 0, bodies = 0, finiteBodies = 0;
            double speed = 0;
            maxSpeed = 0;
            nonFinite = 0;

            for (int i = 0; i < creatures.Length; i++)
            {
                CreatureInstance creature = creatures[i];
                if (creature?.Bodies == null) continue;

                for (int b = 0; b < creature.Bodies.Length; b++)
                {
                    ArticulationBody body = creature.Bodies[b];
                    bodies++;

                    Vector3 v = body.linearVelocity;
                    Vector3 p = body.transform.position;

                    if (!IsFinite(v) || !IsFinite(p))
                    {
                        nonFinite++;
                        continue;
                    }

                    finiteBodies++;
                    if (!body.IsSleeping()) awake++;

                    float magnitude = v.magnitude;
                    speed += magnitude;
                    if (magnitude > maxSpeed) maxSpeed = magnitude;
                }
            }

            awakeFraction = bodies > 0 ? awake / (double)bodies : 0;
            meanSpeed = finiteBodies > 0 ? speed / finiteBodies : 0;
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        /// <summary>
        /// Bodies that have left the space they were given, counted at the end of a cell.
        /// </summary>
        /// <remarks>
        /// In a shared cell that is the box itself. In a tiled cell there is no box, so the test
        /// is displacement past half the tile spacing — the point at which a creature would have
        /// entered a neighbour's tile, which is the tiled arrangement's own failure mode.
        /// </remarks>
        private static int CountOutside(Cell cell, CreatureInstance[] creatures, Vector3[] origins)
        {
            int outside = 0;

            for (int i = 0; i < creatures.Length; i++)
            {
                if (creatures[i]?.Root == null) continue;

                Vector3 centre = FluidEnvironment.CentreOfMass(creatures[i]);
                if (!IsFinite(centre)) { outside++; continue; }

                if (cell.Space == "shared")
                {
                    if (centre.x < 0f || centre.x > cell.FootprintMetres ||
                        centre.z < 0f || centre.z > cell.FootprintMetres ||
                        centre.y > 0f || centre.y < -cell.DepthMetres)
                    {
                        outside++;
                    }
                }
                else if ((centre - origins[i]).magnitude > Ecosystem.TileSpacing * 0.5f)
                {
                    outside++;
                }
            }

            return outside;
        }

        // ------------------------------------------------------------------------ placement

        /// <summary>
        /// Positions that do not overlap: rejection sampling on bounding spheres, with a jittered
        /// lattice as the fallback when the volume is too full for rejection to converge.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Overlap at spawn is not a starting condition, it is a force.</b> PhysX resolves an
        /// overlap by assigning separating velocity, which does not conserve momentum — capped at
        /// <see cref="FluidEnvironment.MaxDepenetrationVelocity"/> here, but a whole population
        /// unfolding at once would still be a transient that no tiled cell has, and every contact
        /// it produced would be counted as a contact of shared space.
        /// </para>
        /// <para>
        /// Rejection sampling is O(1) per attempt through a uniform grid whose cell is one sphere
        /// diameter, so only 27 cells are ever tested. It converges freely at low fill and
        /// collapses near the 3D random-sequential-adsorption jamming fraction of about 0.38; at
        /// 2,000 founders in 10 x 10 x 60 m the fill is a third of that, which is comfortable but
        /// not guaranteed. When any body exhausts its attempts the whole placement restarts on a
        /// jittered lattice, which cannot overlap by construction, and the CSV records which
        /// method produced the cell. A lattice is a different spatial statistic from a Poisson
        /// draw — more uniform, so slightly fewer close pairs — and a cell that used one should be
        /// read with that in mind.
        /// </para>
        /// </remarks>
        private static void Place(
            Cell cell, float[] radii, Rng rng, Vector3[] origins, Result result)
        {
            // The instrument test, and the one placement here that is allowed to overlap. A
            // population sparse enough to be a measurement is too sparse to prove that the
            // contact counters count — fifty founders in 10 x 10 x 60 m never touch, and a
            // counter reading zero there is indistinguishable from a counter that is broken.
            // So one cell packs bodies closer than their own bounding spheres and asks only
            // whether the contacts are reported. Its timings are not a measurement of anything
            // and the four answers exclude it.
            if (cell.Space == "contact-check")
            {
                // A fraction of the bounding diameter, and a small one. A bounding sphere is a
                // long way outside a founder's actual solid — one cell of half-extent 0.15-0.40 m
                // inside a sphere of radius 0.88 m — so lattice spacings that look tight in
                // bounding-sphere terms still leave the colliders metres apart, and the first two
                // attempts at this test reported zero contacts for exactly that reason.
                float factor = Env("EVOSIM_SPIKE_CONTACT_CHECK_SPACING", 0.15f);
                float spacing = Mathf.Max(0.02f, 2f * result.MaxRadius * factor);

                // The smallest cube of lattice sites that holds the population. The cell's own
                // footprint is written here rather than read, so the label and the volume in the
                // CSV describe the box the bodies were actually put in.
                int perAxis = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(origins.Length, 1f / 3f)));
                // A shade over perAxis * spacing so that floor(box / spacing) is perAxis and not
                // perAxis - 1 on a rounding that goes the wrong way.
                cell.FootprintMetres = perAxis * spacing * 1.001f;
                cell.DepthMetres = perAxis * spacing * 1.001f;

                result.Placement = "tight-lattice";

                if (!TightLattice(cell, rng, origins, spacing, 0f))
                {
                    result.Status = "placement-failed";
                    result.Note =
                        $"{origins.Length} sites at {spacing:0.###} m do not fit in " +
                        $"{cell.FootprintMetres:0} x {cell.FootprintMetres:0} x " +
                        $"{cell.DepthMetres:0} m";
                }

                return;
            }

            if (cell.Space != "shared")
            {
                // Today's arrangement, copied from Ecosystem.Build: a 64-wide lattice at
                // TileSpacing. Depth is drawn from the same distribution the shared cells use, so
                // the two arms see the same light, the same current and the same buoyancy.
                const int side = 64;

                for (int i = 0; i < origins.Length; i++)
                {
                    float y = -rng.Range(radii[i], Mathf.Max(radii[i], cell.DepthMetres - radii[i]));

                    origins[i] = new Vector3(
                        (i % side) * Ecosystem.TileSpacing, y, (i / side) * Ecosystem.TileSpacing);
                }

                return;
            }

            const int MaxAttemptsPerBody = 4000;

            float box = cell.FootprintMetres;
            float depth = cell.DepthMetres;
            float cellSize = Mathf.Max(0.05f, 2f * result.MaxRadius);

            int nx = Mathf.Max(1, Mathf.CeilToInt(box / cellSize));
            int ny = Mathf.Max(1, Mathf.CeilToInt(depth / cellSize));
            var grid = new Dictionary<int, List<int>>();

            long rejects = 0;
            bool ok = true;

            for (int i = 0; i < origins.Length && ok; i++)
            {
                float r = radii[i];
                bool placed = false;

                for (int attempt = 0; attempt < MaxAttemptsPerBody; attempt++)
                {
                    var p = new Vector3(
                        rng.Range(r, Mathf.Max(r, box - r)),
                        -rng.Range(r, Mathf.Max(r, depth - r)),
                        rng.Range(r, Mathf.Max(r, box - r)));

                    if (Free(p, r, origins, radii, grid, cellSize, nx, ny))
                    {
                        origins[i] = p;
                        Add(i, p, grid, cellSize, nx, ny);
                        placed = true;
                        break;
                    }

                    rejects++;
                }

                if (!placed) ok = false;
            }

            result.PlacementRejects = rejects;

            if (ok)
            {
                result.Placement = "rejection";
                return;
            }

            result.Placement = "lattice";

            if (!Lattice(cell, rng, origins, result.MaxRadius))
            {
                result.Status = "placement-failed";
                result.Note =
                    $"{origins.Length} bodies of radius up to {result.MaxRadius:0.###} m do not fit " +
                    $"in {cell.FootprintMetres:0} x {cell.FootprintMetres:0} x {cell.DepthMetres:0} m " +
                    "without overlap";
            }
        }

        private static bool Lattice(
            Cell cell, Rng rng, Vector3[] origins, float maxRadius)
        {
            float box = cell.FootprintMetres;
            float depth = cell.DepthMetres;

            foreach (float slack in new[] { 1.5f, 1.25f, 1.1f, 1.02f })
            {
                float s = 2f * maxRadius * slack;

                if ((long)Mathf.FloorToInt(box / s) *
                    Mathf.FloorToInt(depth / s) *
                    Mathf.FloorToInt(box / s) >= origins.Length)
                {
                    // Jitter that cannot make two neighbours overlap: half of whatever the
                    // spacing has over one full diameter, applied per axis.
                    return TightLattice(
                        cell, rng, origins, s, Mathf.Max(0f, (s - 2f * maxRadius) * 0.5f));
                }
            }

            return false;
        }

        private static bool TightLattice(
            Cell cell, Rng rng, Vector3[] origins, float spacing, float jitter)
        {
            float box = cell.FootprintMetres;
            float depth = cell.DepthMetres;

            int nx = Mathf.FloorToInt(box / spacing);
            int ny = Mathf.FloorToInt(depth / spacing);
            int nz = Mathf.FloorToInt(box / spacing);

            if ((long)nx * ny * nz < origins.Length || nx < 1 || ny < 1 || nz < 1) return false;

            int sites = nx * ny * nz;
            var order = new int[sites];
            for (int i = 0; i < sites; i++) order[i] = i;

            // Fisher-Yates, so the occupied sites are a random subset rather than the top slab of
            // the lattice — a population packed into the shallow third would answer a different
            // question about light and current than the one the spike asks.
            for (int i = sites - 1; i > 0; i--)
            {
                int j = rng.Range(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            float originX = (box - nx * spacing) * 0.5f + spacing * 0.5f;
            float originZ = (box - nz * spacing) * 0.5f + spacing * 0.5f;
            float originY = -((depth - ny * spacing) * 0.5f + spacing * 0.5f);

            for (int i = 0; i < origins.Length; i++)
            {
                int site = order[i];
                int ix = site % nx;
                int iy = (site / nx) % ny;
                int iz = site / (nx * ny);

                origins[i] = new Vector3(
                    originX + ix * spacing + rng.Range(-jitter, jitter),
                    originY - iy * spacing + rng.Range(-jitter, jitter),
                    originZ + iz * spacing + rng.Range(-jitter, jitter));
            }

            return true;
        }

        private static int Key(Vector3 p, float cellSize, int nx, int ny)
        {
            int ix = Mathf.FloorToInt(p.x / cellSize);
            int iy = Mathf.FloorToInt(-p.y / cellSize);
            int iz = Mathf.FloorToInt(p.z / cellSize);

            return Key(ix, iy, iz, nx, ny);
        }

        private static int Key(int ix, int iy, int iz, int nx, int ny) =>
            ((iz * (ny + 2) + iy) * (nx + 2)) + ix;

        private static void Add(
            int index, Vector3 p, Dictionary<int, List<int>> grid, float cellSize, int nx, int ny)
        {
            int key = Key(p, cellSize, nx, ny);
            if (!grid.TryGetValue(key, out List<int> bucket))
            {
                bucket = new List<int>();
                grid[key] = bucket;
            }

            bucket.Add(index);
        }

        private static bool Free(
            Vector3 p, float r, Vector3[] origins, float[] radii,
            Dictionary<int, List<int>> grid, float cellSize, int nx, int ny)
        {
            int ix = Mathf.FloorToInt(p.x / cellSize);
            int iy = Mathf.FloorToInt(-p.y / cellSize);
            int iz = Mathf.FloorToInt(p.z / cellSize);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (!grid.TryGetValue(
                                Key(ix + dx, iy + dy, iz + dz, nx, ny), out List<int> bucket))
                        {
                            continue;
                        }

                        for (int k = 0; k < bucket.Count; k++)
                        {
                            int j = bucket[k];
                            float reach = r + radii[j];

                            if ((origins[j] - p).sqrMagnitude < reach * reach) return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Radius of the sphere that contains the whole developed body, about its root origin.
        /// </summary>
        private static float BoundingRadius(Phenotype phenotype)
        {
            float radius = 0f;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                Float3 p = part.Position;
                Float3 h = part.HalfExtents;

                float centre = Mathf.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                float corner = Mathf.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);

                radius = Mathf.Max(radius, centre + corner);
            }

            return Mathf.Max(0.01f, radius);
        }

        // ------------------------------------------------------------- the second contact count

        private static long _engineContactPairs;
        private static long _engineContactPoints;
        private static bool _subscribed;

        /// <summary>
        /// A contact count that does not go through MonoBehaviour messaging.
        /// </summary>
        /// <remarks>
        /// <see cref="SpikeContactCounter"/> is the count the spec asks for, and it depends on the
        /// editor dispatching physics messages to a non-playing scene. If that ever stopped, the
        /// counter would read zero and the spike would report that shared space has no contacts.
        /// <c>Physics.ContactEvent</c> is raised by the engine itself, so the two agreeing is
        /// evidence that the instrument works, and the two disagreeing is a finding rather than a
        /// silent wrong answer.
        /// </remarks>
        private static void SubscribeEngineContacts()
        {
            if (_subscribed) return;

            _engineContactPairs = 0;
            _engineContactPoints = 0;
            Physics.ContactEvent += OnContactEvent;
            _subscribed = true;
        }

        private static void UnsubscribeEngineContacts()
        {
            if (!_subscribed) return;

            Physics.ContactEvent -= OnContactEvent;
            _subscribed = false;
        }

        private static void OnContactEvent(
            PhysicsScene scene, NativeArray<ContactPairHeader>.ReadOnly headers)
        {
            long pairs = 0;
            long points = 0;

            for (int i = 0; i < headers.Length; i++)
            {
                ContactPairHeader header = headers[i];
                pairs += header.pairCount;

                for (int j = 0; j < header.pairCount; j++) points += header.GetContactPair(j).contactCount;
            }

            System.Threading.Interlocked.Add(ref _engineContactPairs, pairs);
            System.Threading.Interlocked.Add(ref _engineContactPoints, points);
        }

        // ---------------------------------------------------------------------------- output

        private static string CsvHeader() =>
            "cell,space,n,footprintM,depthM,volumeM3,bodiesPerM3,repeat,counters,engineContacts," +
            "parts,dof,meanRadiusM,maxRadiusM,placement,placementRejects,buildMs," +
            "msPerStep,msP50,msP95,fluidMs,physicsMs,settleMs,realTimeFactor," +
            "contactCallbacksPerStep,contactEntersPerStep,contactPairsPerStep," +
            "pairsPerBodyPerStep,enginePairsPerStep,enginePointsPerStep,contactPointsPerStep," +
            "awakeFraction,meanSpeed,maxSpeed,nonFinite,leftVolume,status,note";

        private static string CsvRow(Result r)
        {
            var c = r.Cell;

            return string.Join(",", new[]
            {
                Quote(c.Label),
                c.Space,
                N(c.Population),
                N(c.FootprintMetres),
                N(c.DepthMetres),
                N(r.VolumeM3),
                N(r.BodiesPerM3),
                N(c.Repeat),
                c.PartCounters ? "1" : "0",
                c.EngineContacts ? "1" : "0",
                N(r.Parts),
                N(r.Dof),
                N(r.MeanRadius),
                N(r.MaxRadius),
                r.Placement,
                N(r.PlacementRejects),
                N(r.BuildMs),
                N(r.MsPerStep),
                N(r.MsP50),
                N(r.MsP95),
                N(r.FluidMs),
                N(r.PhysicsMs),
                N(r.SettleMs),
                N(r.RealTimeFactor),
                N(r.CallbacksPerStep),
                N(r.EntersPerStep),
                N(r.PairsPerStep),
                N(r.PairsPerBodyPerStep),
                N(r.EnginePairsPerStep),
                N(r.EnginePointsPerStep),
                N(r.ContactPointsPerStep),
                N(r.AwakeFraction),
                N(r.MeanSpeed),
                N(r.MaxSpeed),
                N(r.NonFinite),
                N(r.LeftVolume),
                r.Status,
                Quote(r.Note),
            });
        }

        private static string N(double v) =>
            double.IsNaN(v) ? "" : v.ToString("0.######", CultureInfo.InvariantCulture);

        private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static string N(long v) => v.ToString(CultureInfo.InvariantCulture);

        private static string Quote(string s) =>
            string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

        private static string Summarise(
            List<Result> results, string header, DateTime startedAt, float dt,
            int warmup, int steps, string csvPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Shared-space spike — D076's measurement");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.Append(header);
            sb.AppendLine($"Unity processes at end: {UnityProcessCount()}");
            sb.AppendLine(
                $"started {startedAt:yyyy-MM-dd HH:mm:ss}Z, " +
                $"took {(DateTime.UtcNow - startedAt).TotalMinutes:0.00} min");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine($"Rows: `{csvPath}`");
            sb.AppendLine();

            sb.AppendLine("## Every cell");
            sb.AppendLine();
            sb.AppendLine(
                "| cell | rep | inst | bodies/m3 | ms/step | p95 | fluid | physics | settle | " +
                "x real time | pairs/step | pairs/body/step | points/step | callback pairs | " +
                "awake | mean speed | left | status |");
            sb.AppendLine(
                "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            foreach (Result r in results)
            {
                sb.AppendLine(
                    $"| {r.Cell.Label} | {r.Cell.Repeat} | " +
                    $"{(r.Cell.PartCounters ? "on" : "off")} | " +
                    $"{(double.IsNaN(r.BodiesPerM3) ? "—" : r.BodiesPerM3.ToString("0.####"))} | " +
                    $"{r.MsPerStep:0.###} | {r.MsP95:0.###} | {r.FluidMs:0.###} | " +
                    $"{r.PhysicsMs:0.###} | {r.SettleMs:0.###} | {r.RealTimeFactor:0.##}x | " +
                    $"{r.ReportedPairsPerStep:0.##} | {r.PairsPerBodyPerStep:0.#####} | " +
                    $"{r.EnginePointsPerStep:0.##} | {r.PairsPerStep:0.##} | " +
                    $"{r.AwakeFraction:P0} | {r.MeanSpeed:0.####} | " +
                    $"{r.LeftVolume} | {r.Status} |");
            }

            sb.AppendLine();
            sb.AppendLine(
                "`inst` is the contact instrument: the control cells run with it off, so the " +
                "difference against the same cell with it on is what the counter costs rather " +
                "than what shared space costs. `left` is bodies outside the volume at the end " +
                "(for a tiled cell, bodies more than half a tile from their spawn).");
            sb.AppendLine();
            sb.AppendLine(
                "**`pairs/step` comes from `Physics.ContactEvent`, not from `OnCollisionStay`.** " +
                "The per-part `SpikeContactCounter` the spec asked for is attached and reports " +
                "into the `callback pairs` column, and in a non-playing editor that column is " +
                "zero however many contacts there are: Unity does not dispatch MonoBehaviour " +
                "collision messages outside play mode, `ExecuteAlways` included. That was " +
                "measured rather than assumed — the `contact-check` cell packs bodies until the " +
                "solver is visibly doing contact work, and the two instruments are read against " +
                "each other there. The engine event needs `Collider.providesContacts`, which the " +
                "harness sets on every part it counts.");

            AnswerCostRatio(sb, results);
            AnswerRealTime(sb, results, dt);
            AnswerDensity(sb, results);
            AnswerStability(sb, results);

            sb.AppendLine();
            sb.AppendLine("## What this does not measure");
            sb.AppendLine();
            sb.AppendLine(
                "- **No brains, no sensors, no economy.** The drive is the test sine and nothing " +
                "metabolises. Those costs are per creature and identical in both arms, so the " +
                "shared/tiled ratio is unaffected — but ms/step here is lower than the " +
                "ecosystem's, so the real-time population below is a ceiling, not a forecast.");
            sb.AppendLine(
                "- **Founders, not an evolved population.** Generation zero is one cell and " +
                "sometimes a tail. An evolved body is larger and presents more surface, so " +
                "contacts per body per step here is a floor for a world that has been running.");
            sb.AppendLine(
                "- **No births or deaths.** The population is fixed for the cell, so nothing " +
                "pays for reconciliation, rebuilding or the tile pool.");
            sb.AppendLine(
                $"- Warm-up {warmup} steps discarded, {steps} measured, dt {dt}. " +
                "Timings are the sum of the fluid pass, `Physics.Simulate` and the settle pass; " +
                "sampling of awake fraction and speed happens between steps and is not timed.");

            return sb.ToString();
        }

        private static void AnswerCostRatio(StringBuilder sb, List<Result> results)
        {
            sb.AppendLine();
            sb.AppendLine("## 1. The cost ratio, shared / tiled");
            sb.AppendLine();
            sb.AppendLine("| N | footprint | tiled ms/step | shared ms/step | ratio |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (Result shared in results)
            {
                if (shared.Cell.Space != "shared" || shared.Cell.Repeat != 1) continue;
                if (!shared.Cell.PartCounters) continue;

                Result tiled = results.Find(
                    r => r.Cell.Space == "tiled" &&
                         r.Cell.Population == shared.Cell.Population &&
                         r.Cell.Repeat == 1 && r.Cell.PartCounters);

                string ratio = tiled != null && tiled.MsPerStep > 0
                    ? (shared.MsPerStep / tiled.MsPerStep).ToString("0.00") + "x"
                    : "no tiled cell";

                sb.AppendLine(
                    $"| {shared.Cell.Population} | {shared.Cell.FootprintMetres:0} m | " +
                    $"{(tiled != null ? tiled.MsPerStep.ToString("0.###") : "—")} | " +
                    $"{shared.MsPerStep:0.###} | {ratio} |");
            }

            sb.AppendLine();
            sb.AppendLine(
                "DESIGN.md §5A.9 expects contact cost to be \"rare and local\". A ratio near 1 " +
                "is that expectation holding; a ratio that grows with N or falls with footprint " +
                "is density, and §3 below says where it starts.");
        }

        private static void AnswerRealTime(StringBuilder sb, List<Result> results, float dt)
        {
            double budget = dt * 1000.0;

            sb.AppendLine();
            sb.AppendLine($"## 2. The population that holds real time (≤ {budget:0.#} ms/step at dt {dt})");
            sb.AppendLine();
            sb.AppendLine("| space | footprint | largest N within budget | ms/step there |");
            sb.AppendLine("|---|---|---|---|");

            var keys = new List<string>();

            foreach (Result r in results)
            {
                if (r.Cell.Repeat != 1 || !r.Cell.PartCounters) continue;

                // The instrument test is not a measurement of anything and does not belong in an
                // answer about how many creatures fit.
                if (r.Cell.Space == "contact-check") continue;

                string key = r.Cell.Space == "shared"
                    ? $"shared|{r.Cell.FootprintMetres:0}"
                    : r.Cell.Space + "|—";

                if (!keys.Contains(key)) keys.Add(key);
            }

            foreach (string key in keys)
            {
                string[] parts = key.Split('|');
                int best = 0;
                double bestMs = 0;

                foreach (Result r in results)
                {
                    if (r.Cell.Repeat != 1 || !r.Cell.PartCounters || r.Status != "ok") continue;

                    string rowKey = r.Cell.Space == "shared"
                        ? $"shared|{r.Cell.FootprintMetres:0}"
                        : r.Cell.Space + "|—";

                    if (rowKey != key) continue;
                    if (r.MsPerStep > budget) continue;

                    if (r.Cell.Population > best)
                    {
                        best = r.Cell.Population;
                        bestMs = r.MsPerStep;
                    }
                }

                sb.AppendLine(
                    $"| {parts[0]} | {parts[1]} | " +
                    $"{(best > 0 ? best.ToString() : "none measured")} | " +
                    $"{(best > 0 ? bestMs.ToString("0.###") : "—")} |");
            }

            sb.AppendLine();
            sb.AppendLine(
                "\"None measured\" means every N in the matrix was already over budget, not that " +
                "no population fits. The machine state is in the header: a number taken while " +
                "other arms were running is not this answer.");
        }

        private static void AnswerDensity(StringBuilder sb, List<Result> results)
        {
            sb.AppendLine();
            sb.AppendLine("## 3. Density — contacts per body per step against bodies per m3");
            sb.AppendLine();
            sb.AppendLine(
                "| footprint | N | bodies/m3 | pairs/body/step | pairs/step | points/step | " +
                "ms/step |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            foreach (Result r in results)
            {
                if (r.Cell.Space != "shared" || !r.Cell.PartCounters) continue;

                sb.AppendLine(
                    $"| {r.Cell.FootprintMetres:0} m | {r.Cell.Population}" +
                    $"{(r.Cell.Repeat > 1 ? " (rep " + r.Cell.Repeat + ")" : "")} | " +
                    $"{r.BodiesPerM3:0.####} | {r.PairsPerBodyPerStep:0.#####} | " +
                    $"{r.ReportedPairsPerStep:0.##} | {r.EnginePointsPerStep:0.##} | " +
                    $"{r.MsPerStep:0.###} |");
            }
        }

        private static void AnswerStability(StringBuilder sb, List<Result> results)
        {
            sb.AppendLine();
            sb.AppendLine("## 4. Stability");
            sb.AppendLine();

            int bad = 0;

            foreach (Result r in results)
            {
                if (r.Status == "ok" && r.NonFinite == 0 && r.LeftVolume == 0) continue;

                bad++;
                sb.AppendLine(
                    $"- **{r.Cell.Label}** (rep {r.Cell.Repeat}): status `{r.Status}`, " +
                    $"{r.NonFinite} non-finite bodies, {r.LeftVolume} left the volume, " +
                    $"max body speed {r.MaxSpeed:0.###} m/s. {r.Note}");
            }

            if (bad == 0)
            {
                sb.AppendLine(
                    "No divergence, no non-finite body, and nothing left its volume in any cell.");
            }

            double maxSpeed = 0;
            foreach (Result r in results) maxSpeed = Math.Max(maxSpeed, r.MaxSpeed);

            sb.AppendLine();
            sb.AppendLine($"Fastest body seen in any cell: {maxSpeed:0.###} m/s.");
        }

        // ------------------------------------------------------------------ configuration in

        /// <summary>
        /// The world the spike puts creatures in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Prefer <c>EVOSIM_SPIKE_CONFIG</c>, a path to a run's <c>config.json</c>: it carries the
        /// fluid, the current, the shapes and the genome options of a world that actually ran, and
        /// it carries its own hash, so the spike's world is a world someone can point at rather
        /// than a set of defaults that drifted.
        /// </para>
        /// <para>
        /// <b>It must have been written by this build.</b> <c>RunConfigJson</c> refuses a file
        /// with a missing field rather than defaulting it (CLAUDE.md), so a config.json from
        /// before a tunable was added will not load — which is correct, and is why the failure is
        /// re-thrown here with that sentence attached rather than as a bare "missing required
        /// field 'sense'".
        /// </para>
        /// <para>
        /// Without a file, the fallback is <see cref="RunConfig"/>'s defaults with the fluid and
        /// current knobs the round-24 arms run at, each overridable by an
        /// <c>EVOSIM_SPIKE_*</c> variable. Those defaults are stated here rather than inherited,
        /// because <see cref="RunConfig"/>'s own defaults are still water with neutral tissue and
        /// the ecosystem has not run in that world for many rounds. Whichever path was taken, the
        /// config the spike actually used is written next to the results with its own hash, so
        /// the world is recorded rather than remembered.
        /// </para>
        /// </remarks>
        private static RunConfig LoadConfig(out string source)
        {
            string path = EnvString("EVOSIM_SPIKE_CONFIG", null);

            if (!string.IsNullOrEmpty(path))
            {
                string text = File.ReadAllText(path);

                RunConfig loaded;
                string mismatch;

                try
                {
                    loaded = RunConfigJson.Read(text, out mismatch);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(
                        $"EVOSIM_SPIKE_CONFIG='{path}' could not be read by this build: " +
                        e.Message + "  A config.json written before a tunable was added does not " +
                        "load, by design — settings are refused rather than defaulted. Use a " +
                        "config.json from a run of this build, or unset EVOSIM_SPIKE_CONFIG and " +
                        "let the spike state its own world.", e);
                }

                source = Path.GetFullPath(path) +
                    (string.IsNullOrEmpty(mismatch) ? "" : "  [HASH MISMATCH: " + mismatch + "]");

                return loaded;
            }

            var config = new RunConfig
            {
                Fluid = new FluidConfig
                {
                    TissueExcessDensity = Env("EVOSIM_SPIKE_EXCESS_DENSITY", 0.02f),
                    NeutralBodyVolume = Env("EVOSIM_SPIKE_NEUTRAL_VOLUME", 0.25f),
                },
                HorizontalPatches = Env("EVOSIM_SPIKE_PATCHES", 4f),
            };

            config.Current.Speed = Env("EVOSIM_SPIKE_CURRENT_SPEED", 0.3f);
            config.Current.CellMetres = Env("EVOSIM_SPIKE_CURRENT_CELL", 30f);
            config.Current.PeriodSeconds = Env("EVOSIM_SPIKE_CURRENT_PERIOD", 6000f);
            config.Current.RollBlinkSeconds = Env("EVOSIM_SPIKE_CURRENT_BLINK", 3000f);
            config.Current.Rolls = EnvBool("EVOSIM_SPIKE_CURRENT_ROLLS", true);
            config.Current.AdvectFields = EnvBool("EVOSIM_SPIKE_CURRENT_ADVECT", true);
            config.Current.VentSpeed = Env("EVOSIM_SPIKE_VENT", 0.05f);

            source = "round-24 settings stated in SharedSpaceSpike.LoadConfig " +
                "(no EVOSIM_SPIKE_CONFIG given)";

            return config;
        }

        private static string OutputDirectory()
        {
            string given = EnvString("EVOSIM_SPIKE_OUT", null);
            if (!string.IsNullOrEmpty(given)) return Path.GetFullPath(given);

            string repoRoot = EnvString("EVOSIM_REPO_ROOT", null);

            if (string.IsNullOrEmpty(repoRoot))
            {
                // Application.dataPath is <project>/Assets, so its parent is the worker project
                // and the worker's parent is the repository — where new-worker.ps1 puts workers.
                string worker = Path.GetDirectoryName(Application.dataPath);
                repoRoot = Path.GetFullPath(Path.Combine(worker, ".."));
            }

            string tag = EnvString("EVOSIM_SPIKE_TAG", null);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);

            return Path.Combine(
                repoRoot, "runs", "spike-shared-space",
                string.IsNullOrEmpty(tag) ? stamp : stamp + "-" + tag);
        }

        private static int UnityProcessCount()
        {
            try
            {
                return Process.GetProcessesByName("Unity").Length;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        // ----------------------------------------------------------------------------- env

        private static float Env(string name, float fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrEmpty(raw) &&
                   float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }

        private static ulong EnvULong(string name, ulong fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);

            return !string.IsNullOrEmpty(raw) && ulong.TryParse(raw, out ulong v) ? v : fallback;
        }

        private static bool EnvBool(string name, bool fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(raw)) return fallback;

            raw = raw.Trim().ToLowerInvariant();

            return raw == "1" || raw == "true" || raw == "yes" || raw == "on";
        }

        private static string EnvString(string name, string fallback)
        {
            string raw = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(raw) ? fallback : raw.Trim();
        }

        private static string[] EnvStrings(string name, string fallback)
        {
            string raw = EnvString(name, fallback);
            string[] parts = raw.Split(',');

            var list = new List<string>();
            foreach (string p in parts)
            {
                string trimmed = p.Trim().ToLowerInvariant();
                if (trimmed.Length > 0) list.Add(trimmed);
            }

            return list.ToArray();
        }

        private static int[] EnvInts(string name, int[] fallback)
        {
            string raw = EnvString(name, null);
            if (string.IsNullOrEmpty(raw)) return fallback;

            var list = new List<int>();

            foreach (string p in raw.Split(','))
            {
                if (int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int v) && v > 0)
                {
                    list.Add(v);
                }
            }

            return list.Count > 0 ? list.ToArray() : fallback;
        }

        /// <summary>
        /// Footprints as <c>side</c> or <c>side:depth</c>, e.g. <c>10,20,50</c> or <c>3:3</c>.
        /// </summary>
        /// <remarks>
        /// The optional depth exists so the smoke can force a dense cell without a second launch:
        /// fifty founders in 10 x 10 x 60 m never touch, so a smoke run at the real footprint
        /// would prove the harness runs and prove nothing at all about whether the contact
        /// counters count.
        /// </remarks>
        private static (float Side, float Depth)[] EnvFootprints(
            string name, string fallback, float defaultDepth)
        {
            string raw = EnvString(name, fallback);
            var list = new List<(float, float)>();

            foreach (string p in raw.Split(','))
            {
                string entry = p.Trim();
                if (entry.Length == 0) continue;

                string[] halves = entry.Split(':');

                if (!float.TryParse(halves[0], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float side) || side <= 0f)
                {
                    continue;
                }

                float depth = defaultDepth;

                if (halves.Length > 1)
                {
                    float.TryParse(halves[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out depth);
                }

                if (depth <= 0f) depth = defaultDepth;

                list.Add((side, depth));
            }

            return list.ToArray();
        }
    }
}
