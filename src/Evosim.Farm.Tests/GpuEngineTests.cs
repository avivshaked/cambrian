using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A small tank with everything the gpu engine carries switched on: the streams with their
    /// acceleration, the bed with its relief and its beach, the reefs, D111's torque, per-part
    /// contact on request, and the senses the farm wires. Built through the farm's own binding
    /// and Core's own world, as a launch builds it, and stepped at the solver level.
    /// </summary>
    internal static class GpuTank
    {
        public static Dictionary<string, string> Launcher(bool perPart, bool reefs)
        {
            Dictionary<string, string> block = Common(perPart);
            if (!reefs) return block;

            // With no reef every reef dial stands at its default (ReefGeometry.Refuse).
            block["EVOSIM_REEF_COVER"] = "0.1";
            block["EVOSIM_REEF_MAX_COUNT"] = "6";
            block["EVOSIM_REEF_CAP_RADIUS_MIN"] = "2";
            block["EVOSIM_REEF_CAP_RADIUS_MAX"] = "3";
            block["EVOSIM_REEF_CAP_DEPTH"] = "8";
            block["EVOSIM_REEF_CAP_DEPTH_JITTER"] = "0";
            block["EVOSIM_REEF_CAP_THICKNESS"] = "1";
            block["EVOSIM_REEF_FADE"] = "2";
            return block;
        }

        private static Dictionary<string, string> Common(bool perPart) => new Dictionary<string, string>
        {
            { "EVOSIM_SHAPE", "tank" }, { "EVOSIM_AREA", "400" }, { "EVOSIM_DEPTH", "20" },
            { "EVOSIM_PATCHES", "4" }, { "EVOSIM_SHARED_SPACE", "1" }, { "EVOSIM_FOUNDER_DEPTH", "12" },
            { "EVOSIM_FIELD", "grid" }, { "EVOSIM_FIELD_CELL", "1" }, { "EVOSIM_FIELD_MATTER_CELL", "5" },
            { "EVOSIM_MATTER_BUDGET", "300" }, { "EVOSIM_IRRADIANCE", "200" },
            { "EVOSIM_MIXING", "0.02" }, { "EVOSIM_H_MIXING", "0.02" },
            { "EVOSIM_CURRENT", "0.1" }, { "EVOSIM_CURRENT_MODE", "Transport" },
            { "EVOSIM_CURRENT_PERIOD", "6000" }, { "EVOSIM_CURRENT_CELL", "30" },
            { "EVOSIM_CURRENT_ROLLS", "1" }, { "EVOSIM_CURRENT_BLINK", "3000" }, { "EVOSIM_CURRENT_ADVECT", "1" },
            { "EVOSIM_FLUID_ACCEL", "1" }, { "EVOSIM_ADDED_MASS", "0.5" }, { "EVOSIM_SURFACE_RESTORE", "1" },
            { "EVOSIM_EXCESS_DENSITY", "0.02" }, { "EVOSIM_NEUTRAL_VOLUME", "0.25" },
            { "EVOSIM_BED_RELIEF", "1.5" }, { "EVOSIM_BED_TILT", "10" },
            { "EVOSIM_BED_SHORE", "1" }, { "EVOSIM_BED_SHORE_FADE", "4" },
            { "EVOSIM_BUOYANCY_OFFSET_COST", "0.02" },
            { "EVOSIM_CONTACT_PER_PART", perPart ? "1" : "0" },
            { "EVOSIM_DT", "0.02" }, { "EVOSIM_WATER_HOLD", "0" },
            { "EVOSIM_SENSE_CHEMICAL", "1" }, { "EVOSIM_SENSE_ENERGY", "1" }, { "EVOSIM_SENSE_FLOW", "1" },
            { "EVOSIM_SENSE_CONTACT", "1" }, { "EVOSIM_SENSE_DAMAGE", "1" },
        };

        public static (World world, SolverConfig solver) Build(bool perPart = true, bool reefs = true, ulong seed = 7UL,
                                                                IReadOnlyDictionary<string, string> extra = null)
        {
            Dictionary<string, string> block = Launcher(perPart, reefs);
            if (extra != null)
            {
                foreach (KeyValuePair<string, string> setting in extra) block[setting.Key] = setting.Value;
            }

            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block)));
            var world = new World(config, seed);

            SolverConfig solver = SolverConfig.FromWorld(config, config.PhysicsStepSeconds, world.Bed, world);
            solver.ContactInstrument = true;
            solver.ContactEvents = true;
            return (world, solver);
        }

        /// <summary>A fixed reserve, so the energy sense reads a number and not the stand-in.</summary>
        private sealed class Reserve : IReserveSource
        {
            public float SecondsOfReserve { get; set; }
        }

        /// <summary>
        /// A random viable body the gpu engine holds (sixteen links at most, three inputs a
        /// neuron), placed as the farm places one and wired as the farm wires one.
        /// </summary>
        public static Creature Body(int id, SolverConfig solver, World world, Rng rng, Vec3 at, int maxNeurons = 256,
                                   SensorChannel[] sensorPool = null, bool central = false, int bigLinks = 0,
                                   string bodyCell = null)
        {
            for (int attempt = 0; ; attempt++)
            {
                if (attempt > 2000) throw new InvalidOperationException("no viable body in 2,000 draws");
                Genome genome = GenomeFactory.RandomViable(rng, minParts: 2, sensorPool: sensorPool);
                if (central && !WireCentral(genome, rng, sensorPool ?? world.Config.SensorPool(), world.Config.Brain, bigLinks)) continue;

                // Every part but the links recast as one cell type, developed against the world's table.
                if (bodyCell != null)
                {
                    foreach (MorphNode node in genome.Nodes)
                    {
                        if (node.CellTypeId == CellTypeIds.Link) continue;
                        node.CellTypeId = bodyCell;
                        node.Intake = 0f;
                        node.Lift = 0f;
                    }
                }

                Phenotype plan = bodyCell == null
                    ? Developer.Develop(genome, DevelopmentLimits.Default)
                    : Developer.Develop(genome, DevelopmentLimits.Default, cellTypes: world.Config.CellTypes);
                if (plan.PartCount > 16) continue;

                var body = new Creature(id, plan, solver);
                if (GpuSlots.OverInputs(body) || Math.Max(GpuSlots.NeuronsOf(body), body.Brain.NeuronCount) > maxNeurons)
                {
                    if (attempt < 200) continue;
                }

                return Equip(body, plan, world, id, at);
            }
        }

        /// <summary>
        /// A body from a hand-built genome, placed and wired as <see cref="Body"/> places and wires a drawn one.
        /// </summary>
        public static Creature Hand(int id, SolverConfig solver, World world, Genome genome, Vec3 at)
        {
            Phenotype plan = Developer.Develop(genome, DevelopmentLimits.Default);
            return Equip(new Creature(id, plan, solver), plan, world, id, at);
        }

        private static Creature Equip(Creature body, Phenotype plan, World world, int id, Vec3 at)
        {
            body.PlaceAsDeveloped(at, plan);
            body.Senses.Nutrients = world.Nutrients;
            body.Senses.Scent = world.Scent;
            body.Senses.LeafScent = world.LeafScent;
            body.Senses.Reserve = new Reserve { SecondsOfReserve = 40f + id };

            var contact = new bool[body.Links];
            var damage = new float[body.Links];
            for (int l = 0; l < body.Links; l++)
            {
                contact[l] = (l + id) % 3 == 0;
                damage[l] = 0.1f * ((l + id) % 4);
            }

            body.Senses.Contact = contact;
            body.Senses.Damage = damage;

            if (body.Jointed) body.EnableTrace();
            return body;
        }

        /// <summary>
        /// D146's one brain for a drawn genome, as World.GrowBrain makes a founder's and then mutates
        /// it a few times so it grows links, ports and hidden units. With <paramref name="bigLinks"/>
        /// above 0 it then splits a dozen links into hidden units and adds links until it holds that
        /// many, as seed 1's largest brains do (1,022 links at 47,000 s). False when the result
        /// breaks a rule of the brain's or falls a quarter short, and the caller draws again.
        /// </summary>
        private static bool WireCentral(Genome genome, Rng rng, SensorChannel[] pool, CentralBrainOptions options,
                                        int bigLinks = 0)
        {
            CentralWiring.Convert(genome);
            Phenotype grown = Developer.Develop(genome, DevelopmentLimits.Default);
            genome.Brain = CentralWiring.Effective(genome.Brain, grown, rng, pool, options, founder: true);
            for (int m = 0; m < 6; m++) CentralMutation.Mutate(genome.Brain, grown, pool, rng, options);

            if (bigLinks > 0)
            {
                List<BrainLink> links = genome.Brain.Links;
                for (int h = 0; h < 12 && links.Count > 0; h++) CentralMutation.Split(genome.Brain, links[rng.Range(links.Count)]);
                for (int a = 0; a < 40 * bigLinks && links.Count < bigLinks; a++)
                {
                    CentralMutation.AddLink(genome.Brain, grown, pool, rng, options);
                }

                if (links.Count < bigLinks * 3 / 4) return false;
            }

            return genome.Brain.Validate().Count == 0;
        }

        // ---- D231's bodies: hinged and universal links, flat blades welded beyond them, a lone flat link.

        private static readonly Float3 StrokeLinkHalf = new Float3(0.1f, 0.05f, 0.04f);
        private static readonly Float3 StrokeBladeHalf = new Float3(0.15f, 0.01f, 0.12f);

        /// <summary>How many designs <see cref="StrokeGenome"/> draws; a design is the body's index modulo this.</summary>
        public const int StrokeDesigns = 5;

        /// <summary>The design that a test grows between blocks.</summary>
        public const int StrokeGrowerDesign = 3;

        private static NeuronDef[] StrokeNeurons(int dof, float hz, float phase)
        {
            // The bend-and-power drive reads neuron d of a part as the power of degree d and neuron dof + d as its bend.
            var set = new NeuronDef[2 * dof];
            for (int d = 0; d < dof; d++)
            {
                set[d] = new NeuronDef
                {
                    Op = NeuronOp.OscillateWave, Frequency = hz, Phase = phase + 1.1f * d, Amplitude = 1f, Bias = 0.5f,
                    Inputs = Array.Empty<NeuronInput>(),
                };
                set[dof + d] = new NeuronDef
                {
                    Op = NeuronOp.OscillateWave, Frequency = 0.5f * hz, Phase = phase + 0.7f * d, Amplitude = 0.5f, Bias = 0f,
                    Inputs = Array.Empty<NeuronInput>(),
                };
            }

            return set;
        }

        private static int StrokeAdd(Genome g, int parent, MorphNode node, Float3 parentAnchor, Float3 childAnchor)
        {
            int index = g.Nodes.Count;
            node.RecursiveLimit = 1;
            g.Nodes.Add(node);
            g.Nodes[parent].Edges.Add(new MorphEdge
            {
                Child = index, ParentAnchor = parentAnchor, ChildAnchor = childAnchor,
                Orientation = Quat.Identity, Scale = Float3.One,
            });
            return index;
        }

        private static MorphNode StrokeLink(Float3 half, JointType joint, float power, float hz, float phase)
        {
            int dof = joint.DofCount();
            var limits = new Float2[dof];
            for (int d = 0; d < dof; d++) limits[d] = new Float2(-1.2f, 1.2f);
            return new MorphNode
            {
                Dimensions = half, CellTypeId = CellTypeIds.Link, ShapeId = ShapeIds.Box, JointType = joint,
                JointLimits = limits, Power = power, Neurons = StrokeNeurons(dof, hz, phase),
            };
        }

        private static MorphNode StrokeWeld(Float3 half) => new MorphNode
        {
            Dimensions = half, CellTypeId = CellTypeIds.Structural, ShapeId = ShapeIds.Box,
            JointType = JointType.Fixed, JointLimits = Array.Empty<Float2>(),
        };

        /// <summary>
        /// A hand-built body for D231, by design. 0: a hinged link with a flat blade welded at its tip, in line.
        /// 1: a universal link (two degrees of freedom) with two blades side by side across its stroke. 2: a lone
        /// flat hinged link, nothing welded beyond it. 3: as 0 with other numbers; the one a test grows. 4: two hinged
        /// links in a chain with a blade welded beyond the second, so that the first link's assembly is itself alone.
        /// </summary>
        public static Genome StrokeGenome(int design, int id)
        {
            var g = new Genome { RootIndex = 0, AdultScale = 1f };
            g.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };
            g.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.2f, 0.2f), CellTypeId = CellTypeIds.Structural, ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed, JointLimits = Array.Empty<Float2>(), RecursiveLimit = 1,
            });

            float power = 8f + id % 5;
            float hz = 0.5f + 0.1f * (id % 7);
            var inLineAt = new Float3(1f, 0f, 0f);
            var inLineBy = new Float3(-1f, 0f, 0f);

            switch (design % StrokeDesigns)
            {
                case 0:
                {
                    int link = StrokeAdd(g, 0, StrokeLink(StrokeLinkHalf, JointType.Hinge, power, hz, 0.6f), inLineAt, inLineBy);
                    StrokeAdd(g, link, StrokeWeld(StrokeBladeHalf), inLineAt, inLineBy);
                    break;
                }
                case 1:
                {
                    var link = StrokeAdd(g, 0, StrokeLink(new Float3(0.1f, 0.05f, 0.03f), JointType.Universal, power, hz, 0.3f),
                                         inLineAt, inLineBy);
                    var blade = new Float3(0.2f, 0.01f, 0.05f);
                    StrokeAdd(g, link, StrokeWeld(blade), new Float3(1f, 0f, 1f), new Float3(-1f, 0f, -1f));
                    StrokeAdd(g, link, StrokeWeld(blade), new Float3(1f, 0f, -1f), new Float3(-1f, 0f, 1f));
                    break;
                }
                case 2:
                {
                    StrokeAdd(g, 0, StrokeLink(new Float3(0.2f, 0.01f, 0.12f), JointType.Hinge, power, hz, 0.9f), inLineAt, inLineBy);
                    break;
                }
                case 3:
                {
                    int link = StrokeAdd(g, 0, StrokeLink(new Float3(0.12f, 0.05f, 0.05f), JointType.Hinge, power, hz, 1.2f), inLineAt, inLineBy);
                    StrokeAdd(g, link, StrokeWeld(new Float3(0.2f, 0.012f, 0.1f)), inLineAt, inLineBy);
                    break;
                }
                default:
                {
                    int first = StrokeAdd(g, 0, StrokeLink(StrokeLinkHalf, JointType.Hinge, power, hz, 0.4f), inLineAt, inLineBy);
                    int second = StrokeAdd(g, first, StrokeLink(StrokeLinkHalf, JointType.Hinge, power, hz, 1.0f), inLineAt, inLineBy);
                    StrokeAdd(g, second, StrokeWeld(StrokeBladeHalf), inLineAt, inLineBy);
                    break;
                }
            }

            return g;
        }

        /// <summary>The tank's axis, where Core's tank puts it.</summary>
        public static Vec3 Axis(World world) => new Vec3(world.TankRadiusMetres, 0, world.TankRadiusMetres);

        /// <summary>
        /// Everything a checkpoint holds of the world and every body, and the per-step outputs a
        /// checkpoint derives: the committed link spheres, the external forces, the drive signal,
        /// the overlap lists with their link pairs, the bed-or-glass flag and the event list.
        /// </summary>
        public static byte[] Snapshot(DynamicsWorld world)
        {
            using var stream = new MemoryStream();
            using (var w = new CanonicalWriter(stream))
            {
                world.WriteState(w);
                w.Write(world.OverlapPairsThisStep);
                w.Write(world.OverlapPairsJointedThisStep);
                w.Write(world.OverlapPairsHeldThisStep);
                w.Write(world.OverlapBodiesThisStep);
                w.Write(world.BedOrGlassBodiesThisStep);

                foreach (OverlapPair pair in world.Overlaps)
                {
                    w.Write(pair.A); w.Write(pair.B); w.Write(pair.Jointed); w.Write(pair.Held);
                    w.Write(pair.PartA); w.Write(pair.PartB);
                }

                foreach (Creature c in world.Creatures)
                {
                    // Without the card's pairs (round 60), which only the card engine holds: the rest of
                    // each body's state is the comparison, and the pairs are the card's own record of it.
                    c.WriteState(w, cardBlock: false);
                    Doubles(w, c.Fext);
                    Doubles(w, c.Tau);
                    foreach (float v in c.DriveSignal) w.Write(v);

                    if (c.LinkContactActive)
                    {
                        Doubles(w, c.LinkContactCentre);
                        Doubles(w, c.LinkContactRadius);
                        Doubles(w, c.LinkContactVelocity);
                    }

                    w.Write(c.OverlapCount);
                    for (int k = 0; k < c.OverlapCount; k++)
                    {
                        w.Write(c.OverlapId(k));
                        w.Write(c.OverlapPart(k));
                        w.Write(c.OverlapOtherPart(k));
                    }

                    w.Write(c.TouchedBedOrGlass);
                }
            }

            return stream.ToArray();
        }

        /// <summary>
        /// A writer that writes every NaN as one pattern, the digest's rule (gpu-port-spec.md
        /// section 4): a lost body's state holds NaNs whose sign says which compilation ran, so a
        /// raw-bit comparison of two engines' checkpoints would part on the loss and nothing else.
        /// </summary>
        private sealed class CanonicalWriter : BinaryWriter
        {
            public CanonicalWriter(Stream stream) : base(stream, Encoding.UTF8, leaveOpen: true) { }

            public override void Write(double value) => base.Write(double.IsNaN(value) ? double.NaN : value);

            public override void Write(float value) => base.Write(float.IsNaN(value) ? float.NaN : value);
        }

        private static bool Same(double a, double b) =>
            BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b) || (double.IsNaN(a) && double.IsNaN(b));

        private static void Doubles(BinaryWriter w, double[] values)
        {
            // NaN written as one pattern: two compilations may keep different NaN payloads in a
            // lost body, and the digest reads them as one (logbook/0115).
            foreach (double v in values) w.Write(double.IsNaN(v) ? double.NaN : v);
        }

        /// <summary>Where two snapshots first part, for the failure message.</summary>
        public static string FirstDifference(DynamicsWorld cpu, DynamicsWorld gpu)
        {
            for (int i = 0; i < cpu.Creatures.Count; i++)
            {
                Creature a = cpu.Creatures[i], b = gpu.Creatures[i];
                string where = "body " + a.Id + " (" + a.Links + " links, " + a.Dof + " dof)";

                string d = Compare(a.Position, b.Position, "Position") ?? Compare(a.Velocity, b.Velocity, "Velocity") ??
                           Compare(a.Spin, b.Spin, "Spin") ?? Compare(a.Q, b.Q, "Q") ?? Compare(a.Qd, b.Qd, "Qd") ??
                           Compare(a.Water, b.Water, "Water") ?? Compare(a.WaterAcceleration, b.WaterAcceleration, "WaterAcceleration") ??
                           Compare(a.RelativeVelocity, b.RelativeVelocity, "RelativeVelocity") ??
                           Compare(a.Fext, b.Fext, "Fext") ?? Compare(a.Tau, b.Tau, "Tau") ??
                           (a.Alive != b.Alive ? "Alive " + a.Alive + " against " + b.Alive : null) ??
                           (a.OverlapCount != b.OverlapCount ? "OverlapCount " + a.OverlapCount + " against " + b.OverlapCount : null) ??
                           (!Same(a.MechanicalWorkJoules, b.MechanicalWorkJoules) ? "work" : null) ??
                           (!Same(a.DissipatedJoules, b.DissipatedJoules) ? "dissipated" : null) ??
                           (a.DriveImpulsesLimited != b.DriveImpulsesLimited ? "DriveImpulsesLimited " + a.DriveImpulsesLimited + " against " + b.DriveImpulsesLimited : null) ??
                           (a.DragImpulsesLimited != b.DragImpulsesLimited ? "DragImpulsesLimited " + a.DragImpulsesLimited + " against " + b.DragImpulsesLimited : null) ??
                           (!Same(a.ContactRadius, b.ContactRadius) ? "ContactRadius" : null);

                if (d != null) return where + ": " + d;

                for (int n = 0; n < a.DriveSignal.Length; n++)
                {
                    if (BitConverter.SingleToInt32Bits(a.DriveSignal[n]) != BitConverter.SingleToInt32Bits(b.DriveSignal[n]) &&
                        !(float.IsNaN(a.DriveSignal[n]) && float.IsNaN(b.DriveSignal[n])))
                    {
                        return where + ": DriveSignal[" + n + "] " + a.DriveSignal[n].ToString("R") + " against " +
                               b.DriveSignal[n].ToString("R");
                    }
                }
            }

            return "the world's counters or a checkpointed field outside the list above " +
                   "(census " + cpu.OverlapPairs + "/" + gpu.OverlapPairs + ", clock " +
                   cpu.ElapsedSeconds.ToString("R") + "/" + gpu.ElapsedSeconds.ToString("R") + ")";
        }

        private static string Compare(double[] a, double[] b, string name)
        {
            for (int k = 0; k < a.Length; k++)
            {
                bool same = BitConverter.DoubleToInt64Bits(a[k]) == BitConverter.DoubleToInt64Bits(b[k]) ||
                            (double.IsNaN(a[k]) && double.IsNaN(b[k]));
                if (!same)
                {
                    return name + "[" + k + "] " + a[k].ToString("R") + " against " + b[k].ToString("R") +
                           " (" + (a[k] - b[k]).ToString("R") + ")";
                }
            }

            return null;
        }

        /// <summary>The engine on ILGPU's CPU accelerator: the transcription's test, at four threads.</summary>
        public static GpuOptions CpuDouble() => new GpuOptions { Device = "cpu", Precision = "double", CpuThreads = 4 };
    }

    /// <summary>The kernels are the template's output (logbook/specs/gpu-port-spec.md section 8, test 1).</summary>
    public class GpuKernelSourceTests
    {
        [Fact]
        public void TheGeneratedKernelsAreTheTemplatesOutput()
        {
            string dir = KernelGenerator.FindSourceDirectory(AppContext.BaseDirectory);

            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_REGENERATE") == "1") KernelGenerator.WriteAll(dir);

            foreach ((string name, string text) in KernelGenerator.GenerateAll(dir))
            {
                string committed = KernelGenerator.Lf(File.ReadAllText(Path.Combine(dir, name)));
                Assert.True(
                    committed == text,
                    name + " is not what gpu-kernel.template and gpu-runner.template generate: edit the " +
                    "templates, not the output, and run this test with EVOSIM_GPU_REGENERATE=1.");
            }
        }

        [Fact]
        public void EveryClassIsWrittenOncePerPrecision()
        {
            string dir = KernelGenerator.FindSourceDirectory(AppContext.BaseDirectory);
            foreach ((string name, string text) in KernelGenerator.GenerateAll(dir))
            {
                for (int c = 0; c < KernelGenerator.ClassCount; c++)
                {
                    Assert.Contains("public static void Step" + c + "(", text);
                    Assert.Contains("const int MaxLinks = " + KernelGenerator.ClassLinks[c] + ";", text);
                }

                Assert.DoesNotContain("__", text.Replace("k__BackingField", ""));
            }
        }
    }

    /// <summary>What the engine refuses, each naming its setting (section 3, test 6).</summary>
    public class GpuRefusalTests
    {
        private static void Refused(Action build, string setting)
        {
            var e = Assert.ThrowsAny<Exception>(build);
            Assert.Contains(setting, e.Message);
        }

        [Fact]
        public void ABoxIsRefusedWithoutItsSeams()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.TankRadiusMetres = 0;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_READ_ACROSS_SEAMS");
        }

        [Fact]
        public void ABackendBuiltForOneContactShapeRefusesToStepTheOther()
        {
            // The same world twice, as one config and its copy, so that only the shape can differ.
            (_, SolverConfig built) = GpuTank.Build(reefs: false);
            var copy = (SolverConfig)typeof(object)
                .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(built, null);

            GpuBackend.Check(built, copy);

            copy.ContactShape = ContactShapeForm.TrueShape;
            Refused(() => GpuBackend.Check(built, copy), "contact shape");

            built.ContactShape = ContactShapeForm.TrueShape;
            GpuBackend.Check(built, copy);

            copy.ContactShape = ContactShapeForm.Spheres;
            Refused(() => GpuBackend.Check(built, copy), "contact shape");
        }

        [Fact]
        public void ABackendBuiltForOneStrokeLawRefusesToStepTheOther()
        {
            // The same world twice, as one config and its copy, so that only D231's switch can differ.
            (_, SolverConfig built) = GpuTank.Build(reefs: false);
            var copy = (SolverConfig)typeof(object)
                .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(built, null);

            GpuBackend.Check(built, copy);

            copy.StrokePushesAssembly = true;
            Refused(() => GpuBackend.Check(built, copy), "stroke assembly");

            built.StrokePushesAssembly = true;
            GpuBackend.Check(built, copy);

            copy.StrokePushesAssembly = false;
            Refused(() => GpuBackend.Check(built, copy), "stroke assembly");
        }

        [Fact]
        public void AWaterHoldIsRefusedNamingIt()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.WaterHoldSeconds = 0.5;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_WATER_HOLD");
        }

        [Fact]
        public void AnotherStepIsRefusedNamingIt()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.StepSeconds = 0.005;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_DT");
        }

        [Fact]
        public void TheRollsAreRefusedNamingTheMode()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.Current.Mode = CurrentMode.Rolls;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_CURRENT_MODE");
        }

        [Fact]
        public void AVentIsRefusedNamingIt()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            s.Current.VentSpeed = 0.1f;
            Refused(() => GpuWorld.Refuse(s), "EVOSIM_VENT");
        }

        [Fact]
        public void TheEnginesOwnWordsAreRefusedNamingTheirSettings()
        {
            Refused(() => new GpuOptions { Device = "opencl" }.Validate(), "EVOSIM_GPU_DEVICE");
            Refused(() => new GpuOptions { Precision = "half" }.Validate(), "EVOSIM_GPU_PRECISION");
            Refused(() => new GpuOptions { Mean = "tree" }.Validate(), "EVOSIM_GPU_MEAN");
            Refused(() => new GpuOptions { GroupSize = 4096 }.Validate(), "EVOSIM_GPU_GROUP");
        }

        [Fact]
        public void TheEngineSettingsAreReadAndAreNotTunables()
        {
            var block = GpuTank.Launcher(perPart: true, reefs: false);
            string plain = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(block))).Hash();

            block["EVOSIM_ENGINE"] = "GPU";
            block["EVOSIM_GPU_DEVICE"] = "cpu";
            block["EVOSIM_GPU_PRECISION"] = "double";
            block["EVOSIM_GPU_GROUP"] = "64";
            block["EVOSIM_GPU_CONCURRENT"] = "1";
            EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));

            Assert.True(s.EngineIsGpu);
            Assert.Equal("cpu", s.GpuDevice);
            Assert.Equal("double", s.GpuPrecision);
            Assert.Equal(64, s.GpuGroup);
            Assert.True(s.GpuConcurrent);
            Assert.Equal(plain, EnvBinding.BuildConfig(s).Hash());
            Assert.Empty(EnvBinding.UnknownNames(block.Keys));
        }
    }

    /// <summary>The current's instants, transcribed, against the field's own pinned slots.</summary>
    public class GpuInstantTests
    {
        [Fact]
        public void TheInstantsAreTheFieldsOwnToTheBit()
        {
            (_, SolverConfig s) = GpuTank.Build(reefs: false);
            var world = new GpuWorld(s);

            double seconds = 0;
            for (int k = 0; k < 300; k++)
            {
                if (k % 60 == 0 || k > 290)
                {
                    (int values, int mismatches) = world.VerifyInstant(seconds);
                    Assert.True(values > 0, "no slot of the field was pinned at " + seconds.ToString("R") + " s");
                    Assert.Equal(0, mismatches);
                }

                seconds += 0.02;
            }

            foreach (double at in new[] { 1234.5678, 5999.99, 12000.01, 29999.5 })
            {
                (int values, int mismatches) = world.VerifyInstant(at);
                Assert.True(values > 0);
                Assert.Equal(0, mismatches);
            }
        }
    }

    /// <summary>
    /// The transcription on ILGPU's CPU accelerator in double, against the solver, at the
    /// scale the owner's load rule allows a test (a handful of bodies, a hundred-odd steps, four
    /// threads). The farm-scale acceptance is the caller's (gpu-port-spec.md section 4).
    /// </summary>
    public class GpuCpuAcceleratorTests
    {
        private readonly ITestOutputHelper _out;

        public GpuCpuAcceleratorTests(ITestOutputHelper output) => _out = output;

        private static (DynamicsWorld world, World core) Pair(bool perPart, bool reefs,
                                                              IReadOnlyDictionary<string, string> extra = null)
        {
            (World core, SolverConfig solver) = GpuTank.Build(perPart, reefs, extra: extra);
            return (new DynamicsWorld(solver) { Threads = 2 }, core);
        }

        private static void Populate(DynamicsWorld world, World core, int count, ulong seed, int firstId,
                                     SensorChannel[] sensorPool = null, bool central = false)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                // A cluster a couple of metres across at mid-depth, so bodies touch each other;
                // the fifth at the glass and the sixth on the bed, so the world's contacts run.
                var at = new Vec3(axis.X + 0.7 * (k % 3) - 0.7, -9.0 - 0.6 * (k / 3), axis.Z + 0.5 * (k % 2));
                if (k == 4) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                if (k == 5) at = new Vec3(axis.X - 2.0, -core.Config.WorldDepthMetres + 0.6, axis.Z - 2.0);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at, sensorPool: sensorPool, central: central));
            }
        }

        /// <summary>
        /// D231's crowd: <see cref="Populate"/>'s places, with <see cref="GpuTank.StrokeGenome"/>'s bodies in them, the
        /// design by the body's place in the crowd. The fifth stands at the glass and the sixth on the bed.
        /// </summary>
        private static void PopulateStroke(DynamicsWorld world, World core, int count, int firstId)
        {
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                var at = new Vec3(axis.X + 0.7 * (k % 3) - 0.7, -9.0 - 0.6 * (k / 3), axis.Z + 0.5 * (k % 2));
                if (k == 4) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                if (k == 5) at = new Vec3(axis.X - 2.0, -core.Config.WorldDepthMetres + 0.6, axis.Z - 2.0);
                int id = firstId + k;
                world.AddInIdOrder(GpuTank.Hand(id, world.Config, core, GpuTank.StrokeGenome(k, id), at));
            }
        }

        /// <summary>
        /// D228's crowd: bodies close enough to touch each other, the plates and the capsules among them, with the
        /// bed under the lowest and the last at the glass.
        /// </summary>
        private static void PopulateCrowd(DynamicsWorld world, World core, int count, ulong seed, int firstId)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                double x = axis.X + 0.45 * (k % 4) - 0.7;
                double z = axis.Z + 0.45 * ((k / 4) % 3) - 0.45;
                double y = core.Bed.FloorY(x, z) + 0.5 + 0.45 * (k / 12);
                var at = new Vec3(x, y, z);
                if (k == count - 1) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at));
            }
        }

        private void AssertSame(DynamicsWorld cpu, DynamicsWorld gpu, string when)
        {
            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (a.Length == b.Length && a.AsSpan().SequenceEqual(b)) return;

            Assert.Fail("The engines part " + when + ": " + GpuTank.FirstDifference(cpu, gpu));
        }

        /// <summary>
        /// Section 8's tests 2 and 3 at test scale: a block at a time, with a death and a birth
        /// between blocks so a slot is freed and taken again, every body and every counter the
        /// same to the bit after every block.
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void DoubleOnTheCpuAcceleratorIsTheSolver(bool perPart, bool reefs)
        {
            (DynamicsWorld cpu, World coreA) = Pair(perPart, reefs);
            (DynamicsWorld gpu, World coreB) = Pair(perPart, reefs);

            Populate(cpu, coreA, 6, 11UL, 1);
            Populate(gpu, coreB, 6, 11UL, 1);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);
            _out.WriteLine("kernels compiled in " + backend.CompileMs.ToString("0") + " ms on " + backend.DeviceName);

            AssertSame(cpu, gpu, "before the first block");

            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "after block " + block);

                // A death and a birth: the lowest-id body leaves, a new one arrives at the end.
                if (block == 1)
                {
                    cpu.Remove(cpu.Creatures[0].Id);
                    gpu.Remove(gpu.Creatures[0].Id);
                    Populate(cpu, coreA, 1, 99UL, 50);
                    Populate(gpu, coreB, 1, 99UL, 50);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
            _out.WriteLine("pairs " + gpu.OverlapPairs + ", bed or glass " + gpu.BedOrGlassBodies);
        }

        /// <summary>
        /// The bend-and-power drive and the limb push (card-audit-r57.md): the drive's envelope and
        /// muscle alone, then with the power's push as round 57 runs them, and the swing's push under
        /// the torque drive, each the solver's to the bit after every block, a death and a birth
        /// included. Each world is first shown to part on the CPU from the world without its last
        /// setting, so a setting that never reached the solver or a push that never fired cannot pass
        /// as a match.
        /// </summary>
        [Theory]
        [InlineData("drive")]
        [InlineData("power")]
        [InlineData("swing")]
        [InlineData("shapedpower")]
        [InlineData("strokeassembly")]
        [InlineData("shapedswing")]
        [InlineData("trueshape")]
        [InlineData("trueshape-crowd")]
        public void TheDriveAndThePushesOnTheCpuAcceleratorAreTheSolver(string world)
        {
            var drive = new Dictionary<string, string> { { "EVOSIM_POWER_PUSH", "2" }, { "EVOSIM_POSE_HZ", "2" } };
            var power = new Dictionary<string, string>(drive) { { "EVOSIM_LIMB_PUSH", "0.3" } };
            var swing = new Dictionary<string, string> { { "EVOSIM_LIMB_PUSH", "0.3" } };

            // D229's fin law: the power's push and the swing's with the shaped added mass on, each shown
            // to part on the CPU from the same push with the volume formula.
            var shapedPower = new Dictionary<string, string>(power) { { "EVOSIM_SHAPED_ADDED_MASS", "1" } };
            var shapedSwing = new Dictionary<string, string>(swing) { { "EVOSIM_SHAPED_ADDED_MASS", "1" } };

            // D231: the power's push moves the assembly welded beyond a jointed link, shown to part on the CPU from
            // the fin law's push alone. Its crowd is hand-built: a blade beyond a hinge, a universal link with two
            // blades, a lone flat link, a chain of two joints with a blade beyond the second and a body that grows.
            var strokeAssembly = new Dictionary<string, string>(shapedPower) { { "EVOSIM_STROKE_ASSEMBLY", "1" } };
            bool strokes = world == "strokeassembly";

            // D228's contact on the true shape: the bodies' parts touch as their convex cores with margins, the
            // bed, the reefs and the glass on the core's support point. The second puts plates and capsules close
            // enough to touch each other and the bed. Each is shown to part on the CPU from the spheres' world.
            var trueShape = new Dictionary<string, string> { { "EVOSIM_CONTACT_SHAPE", "trueshape" } };
            bool crowd = world == "trueshape-crowd";
            bool reefs = world.StartsWith("trueshape");

            Dictionary<string, string> settings =
                world == "drive" ? drive : world == "power" ? power : world == "swing" ? swing
                : world == "shapedpower" ? shapedPower : strokes ? strokeAssembly : world == "shapedswing" ? shapedSwing : trueShape;
            Dictionary<string, string> without =
                world == "power" ? drive : world == "shapedpower" ? power : strokes ? shapedPower
                : world == "shapedswing" ? swing : null;

            (DynamicsWorld plain, World core0) = Pair(true, reefs, without);
            (DynamicsWorld cpu, World coreA) = Pair(true, reefs, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, reefs, settings);

            if (crowd)
            {
                PopulateCrowd(plain, core0, 14, 11UL, 1);
                PopulateCrowd(cpu, coreA, 14, 11UL, 1);
                PopulateCrowd(gpu, coreB, 14, 11UL, 1);
            }
            else if (strokes)
            {
                PopulateStroke(plain, core0, 6, 1);
                PopulateStroke(cpu, coreA, 6, 1);
                PopulateStroke(gpu, coreB, 6, 1);
            }
            else
            {
                Populate(plain, core0, 6, 11UL, 1);
                Populate(cpu, coreA, 6, 11UL, 1);
                Populate(gpu, coreB, 6, 11UL, 1);
            }
            Assert.Equal(world != "swing" && world != "shapedswing" && !world.StartsWith("trueshape"),
                         cpu.Creatures[0].Drive.BendAndPower);
            Assert.Equal(world.StartsWith("shaped") || strokes, cpu.Config.ShapedAddedMass);
            Assert.Equal(strokes, cpu.Config.StrokePushesAssembly);
            Assert.False(plain.Config.StrokePushesAssembly);
            Assert.Equal(world.StartsWith("trueshape"), cpu.Config.ContactShape == ContactShapeForm.TrueShape);

            if (strokes)
            {
                // The crowd holds what the case is about: an assembly of two parts and of three, a jointed link
                // alone, and a body that will grow. A table that was never filled would pass as a match.
                int blades = 0, triples = 0, lone = 0;
                foreach (Creature c in cpu.Creatures)
                {
                    for (int l = 0; l < c.Links; l++)
                    {
                        if (c.StrokeAssemblyParts[l] == 2) blades++;
                        if (c.StrokeAssemblyParts[l] == 3) triples++;
                        if (c.StrokeAssemblyParts[l] == 1) lone++;
                    }
                }

                Assert.True(blades >= 3 && triples >= 1 && lone >= 2, "the crowd lacks an assembly: " + blades + " pairs, " + triples + " triples, " + lone + " lone");
            }

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 4; block++)
            {
                plain.StepBlock(25);
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, world + ", after block " + block);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (plain, core0), (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        if (strokes) PopulateStroke(w, core, 1, 50);
                        else Populate(w, core, 1, 99UL, 50);
                    }
                }

                // A body that grows between blocks: it shrinks after the first block and grows after the second,
                // in all three worlds alike, so that the card re-packs it with the constants of its new sizes.
                if (strokes && block <= 1)
                {
                    foreach (DynamicsWorld w in new[] { plain, cpu, gpu })
                    {
                        Creature grower = null;
                        foreach (Creature c in w.Creatures) if (c.Id == 1 + GpuTank.StrokeGrowerDesign) grower = c;
                        Assert.NotNull(grower);
                        long resizes = grower.Resizes;
                        grower.Resize(grower.Phenotype.Scaled(block == 0 ? 0.6f : 1.5f));
                        Assert.Equal(resizes + 1, grower.Resizes);
                    }
                }
            }

            bool parted = false;
            for (int i = 0; i < cpu.Creatures.Count && !parted; i++)
            {
                double[] a = plain.Creatures[i].Position, b = cpu.Creatures[i].Position;
                for (int k = 0; k < a.Length && !parted; k++)
                {
                    parted = BitConverter.DoubleToInt64Bits(a[k]) != BitConverter.DoubleToInt64Bits(b[k]);
                }
            }

            Assert.True(parted, world + ": the world stepped as the one without its setting");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// D199/D200's limiter (<c>EVOSIM_DRAG_LIMIT_WELDED</c>): the force capped link by link and
        /// the torque by each welded body, at the 0.02 s step where the limiter engages and at 0.01 s
        /// with its gate opened (<c>EVOSIM_DRAG_LIMIT_ALWAYS</c>). Each is the solver's to the bit
        /// after every block, a death and a birth included, and each is first shown to part on the CPU
        /// from the per-link limiter under the same gate, so a switch that never reached the kernel
        /// cannot pass as a match.
        /// </summary>
        [Theory]
        [InlineData("coarse")]
        [InlineData("always")]
        public void TheWeldedLimiterOnTheCpuAcceleratorIsTheSolver(string gate)
        {
            var link = gate == "coarse"
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { { "EVOSIM_DT", "0.01" }, { "EVOSIM_DRAG_LIMIT_ALWAYS", "1" } };
            var welded = new Dictionary<string, string>(link) { { "EVOSIM_DRAG_LIMIT_WELDED", "1" } };

            (DynamicsWorld plain, World core0) = Pair(true, false, link);
            (DynamicsWorld cpu, World coreA) = Pair(true, false, welded);
            (DynamicsWorld gpu, World coreB) = Pair(true, false, welded);
            Assert.True(cpu.Config.DragLimiterEngages && cpu.Config.DragLimitByWeldedBody);
            Assert.False(plain.Config.DragLimitByWeldedBody);

            Populate(plain, core0, 6, 11UL, 1);
            Populate(cpu, coreA, 6, 11UL, 1);
            Populate(gpu, coreB, 6, 11UL, 1);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 4; block++)
            {
                plain.StepBlock(25);
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, gate + ", after block " + block);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (plain, core0), (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        Populate(w, core, 1, 99UL, 50);
                    }
                }
            }

            bool parted = false;
            for (int i = 0; i < cpu.Creatures.Count && !parted; i++)
            {
                double[] a = plain.Creatures[i].Spin, b = cpu.Creatures[i].Spin;
                for (int k = 0; k < a.Length && !parted; k++)
                {
                    parted = BitConverter.DoubleToInt64Bits(a[k]) != BitConverter.DoubleToInt64Bits(b[k]);
                }
            }

            long limited = 0;
            foreach (Creature c in cpu.Creatures) limited += c.DragImpulsesLimited;
            _out.WriteLine(gate + ": " + limited + " drag impulses limited on the CPU");

            Assert.True(parted, gate + ": the welded world stepped as the per-link one");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// D161's drag on turning (r59 step 7): the skin form, a shear on every panel, and the spin
        /// form, a torque against each axis's spin, each the solver's to the bit after every block, a
        /// death and a birth included. Each world is first shown to part on the CPU from the world
        /// without the drag, so a setting that never reached the kernel cannot pass as a match. The
        /// CPU accelerator ignores the group size, so the two group sizes are the card's check.
        /// </summary>
        [Theory]
        [InlineData("skin")]
        [InlineData("spin")]
        public void TheDragOnTurningOnTheCpuAcceleratorIsTheSolver(string form)
        {
            var settings = form == "skin"
                ? new Dictionary<string, string> { { "EVOSIM_TURNING_DRAG", "skin" }, { "EVOSIM_SKIN_FRICTION", "0.05" } }
                : new Dictionary<string, string> { { "EVOSIM_TURNING_DRAG", "spin" }, { "EVOSIM_SPIN_DRAG", "0.5" } };

            (DynamicsWorld plain, World core0) = Pair(true, true);
            (DynamicsWorld cpu, World coreA) = Pair(true, true, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, true, settings);

            Populate(plain, core0, 6, 11UL, 1);
            Populate(cpu, coreA, 6, 11UL, 1);
            Populate(gpu, coreB, 6, 11UL, 1);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 4; block++)
            {
                plain.StepBlock(25);
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, form + ", after block " + block);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (plain, core0), (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        Populate(w, core, 1, 99UL, 50);
                    }
                }
            }

            bool parted = false;
            for (int i = 0; i < cpu.Creatures.Count && !parted; i++)
            {
                double[] a = plain.Creatures[i].Position, b = cpu.Creatures[i].Position;
                for (int k = 0; k < a.Length && !parted; k++)
                {
                    parted = BitConverter.DoubleToInt64Bits(a[k]) != BitConverter.DoubleToInt64Bits(b[k]);
                }
            }

            Assert.True(parted, form + ": the world stepped as the one without the drag");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// D162's touch of the rock and its sense (r59 step 7): bodies whose brains draw the touch
        /// among their senses, on the shaped bed and among the reefs, the solver's to the bit after
        /// every block, the touch record, its point and its normal included (the body's checkpointed
        /// state), a death and a birth included. The CPU is first shown to have brains that read the
        /// touch and links that touch the rock, so a record that never reached the card cannot pass.
        /// </summary>
        [Fact]
        public void TheTouchOfTheRockAndItsSenseOnTheCpuAcceleratorAreTheSolver()
        {
            // Tissue that sinks against the tank's current: with the tank's own excess and neutral
            // volume, and even at 50 kg/m3, the rolls lifted bodies put down on the bed about half a
            // metre a block, and no link touched the rock at any block's end.
            var settings = new Dictionary<string, string>
            {
                { "EVOSIM_SENSE_BED_TOUCH", "1" }, { "EVOSIM_EXCESS_DENSITY", "500" }, { "EVOSIM_NEUTRAL_VOLUME", "0" },
            };
            // Drawn from the touch alone, so most brains read it, as the scent test draws its senses.
            SensorChannel[] pool = { SensorChannel.BedTouch };

            (DynamicsWorld cpu, World coreA) = Pair(true, true, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, true, settings);

            OnTheBed(cpu, coreA, 6, 11UL, 1, pool);
            OnTheBed(gpu, coreB, 6, 11UL, 1, pool);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            int touching = 0;
            bool sensed = false;
            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "the touch, after block " + block);
                sensed |= Reads(cpu, SensorChannel.BedTouch);

                foreach (Creature body in cpu.Creatures)
                {
                    for (int l = 0; l < body.Links; l++) if (body.RockTouched[l]) touching++;
                }

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        OnTheBed(w, core, 1, 99UL, 50, pool);
                    }
                }
            }

            _out.WriteLine("links touching the rock at the blocks' ends: " + touching);
            Assert.True(touching > 0, "no link touched the rock");
            Assert.True(sensed, "no brain read a touch");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// D167's leaf senses (r59 step 7), in each form: bodies whose brains draw the form's channel
        /// alone, the solver's to the bit after every block over four blocks of 25 steps, a death and
        /// a birth included. The leaf field takes its own cell, 1.5 m against the carrion's 2, and is
        /// refilled between blocks from sources that move, as the world refills it at a metabolic
        /// step. The CPU is first shown to have a brain read the sense, so a field or a sense that
        /// never reached the card cannot pass.
        /// </summary>
        [Theory]
        [InlineData("strength")]
        [InlineData("direction")]
        public void TheLeafSensesOnTheCpuAcceleratorAreTheSolver(string form)
        {
            var settings = new Dictionary<string, string>
            {
                { "EVOSIM_LEAF_SCENT", form }, { "EVOSIM_LEAF_SCENT_RANGE", "6" }, { "EVOSIM_LEAF_SCENT_CELL", "1.5" },
                { "EVOSIM_LEAF_SCENT_HALF", "0.5" },
            };
            SensorChannel channel = form == "strength" ? SensorChannel.LeafScent : SensorChannel.LeafScentDirection;
            SensorChannel[] pool = { channel };

            (DynamicsWorld cpu, World coreA) = Pair(true, true, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, true, settings);
            Assert.NotNull(coreA.LeafScent);
            Assert.Equal(1.5f, coreA.LeafScent.CellSizeMetres);

            Populate(cpu, coreA, 6, 11UL, 1, pool);
            Populate(gpu, coreB, 6, 11UL, 1, pool);

            Vec3 axis = GpuTank.Axis(coreA);
            Leaves(coreA.LeafScent, axis, 0);
            Leaves(coreB.LeafScent, axis, 0);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            bool sensed = false;
            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, form + ", after block " + block);
                sensed |= Reads(cpu, channel);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        Populate(w, core, 1, 99UL, 50, pool);
                    }
                }

                Leaves(coreA.LeafScent, axis, block + 1);
                Leaves(coreB.LeafScent, axis, block + 1);
            }

            Assert.True(sensed, form + ": no brain read a leaf smell that was not 0");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// Three leaves round the bodies' cluster (or round <paramref name="axis"/> itself), x, y, z and
        /// lit area as World.GatherLeafSources writes them, moved a little each round so the grid
        /// changes between blocks.
        /// </summary>
        private static void Leaves(ScentField field, Vec3 axis, int round, bool atCentre = false)
        {
            float x = (float)axis.X, z = (float)axis.Z, d = 0.4f * round;
            float y = atCentre ? (float)axis.Y : -9f;
            float[] sources =
            {
                x + 2.0f + d, y + 0.5f, z - 1.5f, 0.3f,
                x - 3.0f, y - 1.0f + d, z + 2.0f, 0.8f,
                x + 0.5f, y + 2.0f, z + 3.5f - d, 0.15f,
            };
            field.FillSources(sources, round == 3 ? 2 : 3);
        }

        /// <summary>
        /// D162's grip (r59 step 7): bodies whose body cells are anchoring, put down on the shaped bed
        /// among the reefs, the solver's to the bit after every block over six blocks of 25 steps,
        /// the grips, their strength, the reserve clock and the price owed included. Between blocks
        /// the price is drained from both, as the farm drains it, and the reserves are moved through
        /// the rule's cases: below the threshold past the lag, empty, and above the threshold. A death
        /// and a birth come between two blocks. The CPU is first shown to form, hold, break and pay
        /// for grips, so a grip that never reached the card cannot pass.
        /// </summary>
        [Fact]
        public void TheGripOnTheCpuAcceleratorIsTheSolver()
        {
            // The touch test's sinking tissue; a grip weak and quick to fade, so grips break and form
            // again within a test; the reserve rule's threshold above the stand-in reserves (40 s
            // and the body's id) and its lag a few steps.
            var settings = new Dictionary<string, string>
            {
                { "EVOSIM_ANCHORING", "1" }, { "EVOSIM_EXCESS_DENSITY", "500" }, { "EVOSIM_NEUTRAL_VOLUME", "0" },
                { "EVOSIM_ANCHOR_DECAY", "1" },
                { "EVOSIM_ANCHOR_RESERVE_THRESHOLD", "50" }, { "EVOSIM_ANCHOR_RESERVE_LAG", "0.2" },
            };

            (DynamicsWorld cpu, World coreA) = Pair(true, true, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, true, settings);
            Assert.NotNull(cpu.Config.Anchoring);

            OnTheBed(cpu, coreA, 6, 11UL, 1, null, CellTypeIds.Anchoring);
            OnTheBed(gpu, coreB, 6, 11UL, 1, null, CellTypeIds.Anchoring);
            Assert.Contains(cpu.Creatures, c => c.CanGrip);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            int heldAtEnds = 0;
            double paid = 0;
            for (int block = 0; block < 6; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "the grip, after block " + block);

                foreach (Creature body in cpu.Creatures) heldAtEnds += body.GripsHeld;

                double a = 0, b = 0;
                foreach (Creature body in cpu.Creatures) a += body.DrainGripOwed();
                foreach (Creature body in gpu.Creatures) b += body.DrainGripOwed();
                Assert.Equal(a, b);
                paid += a;

                foreach (DynamicsWorld w in new[] { cpu, gpu })
                {
                    for (int k = 0; k < w.Creatures.Count; k++)
                    {
                        // Block 2: one body's reserve empty and one's above the threshold; block 4: back.
                        float seconds = 40f + w.Creatures[k].Id;
                        if (block == 2 && k == 1) seconds = 0f;
                        if (block == 2 && k == 2) seconds = 80f;
                        w.Creatures[k].Senses.Reserve = new GripReserve { SecondsOfReserve = seconds };
                    }
                }

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        OnTheBed(w, core, 1, 99UL, 50, null, CellTypeIds.Anchoring);
                    }
                }
            }

            long formed = 0, broken = 0;
            double heldSeconds = 0;
            foreach (Creature body in cpu.Creatures)
            {
                formed += body.GripsFormed;
                broken += body.GripsBroken;
                heldSeconds += body.GripHeldSeconds;
            }

            _out.WriteLine($"grips formed {formed}, broken {broken}, held at the blocks' ends {heldAtEnds}, " +
                           $"link-seconds held {heldSeconds:0.###}, paid {paid:0.######} J");
            Assert.True(formed > 0, "no grip formed");
            Assert.True(broken > 0, "no grip broke");
            Assert.True(heldAtEnds > 0, "no grip held at a block's end");
            Assert.True(paid > 0, "no grip was paid for");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        private sealed class GripReserve : IReserveSource
        {
            public float SecondsOfReserve { get; set; }
        }

        /// <summary>
        /// Round 59's world on the card with every piece on at once (r59-anchor-build-plan.md, step 7's
        /// tests): the drag on turning in its spin form, the touch and its sense, the grip, and the
        /// leaf smell in its direction form. Bodies whose body cells are anchoring and whose brains
        /// draw the touch and the leaf's direction are put down on the bed among the reefs, under
        /// leaves that move between blocks, with the grip's price drained and a death and a birth
        /// between blocks. The solver's to the bit after every block, and on the CPU grips form and
        /// brains read both senses.
        /// </summary>
        [Fact]
        public void EveryPieceOfRound59OnTheCpuAcceleratorIsTheSolver()
        {
            var settings = new Dictionary<string, string>
            {
                { "EVOSIM_TURNING_DRAG", "spin" }, { "EVOSIM_SPIN_DRAG", "0.5" },
                { "EVOSIM_SENSE_BED_TOUCH", "1" }, { "EVOSIM_ANCHORING", "1" }, { "EVOSIM_ANCHOR_DECAY", "1" },
                { "EVOSIM_LEAF_SCENT", "direction" }, { "EVOSIM_LEAF_SCENT_RANGE", "6" },
                { "EVOSIM_LEAF_SCENT_CELL", "1.5" }, { "EVOSIM_LEAF_SCENT_HALF", "0.5" },
                { "EVOSIM_EXCESS_DENSITY", "500" }, { "EVOSIM_NEUTRAL_VOLUME", "0" },
            };
            SensorChannel[] pool = { SensorChannel.BedTouch, SensorChannel.LeafScentDirection };

            (DynamicsWorld cpu, World coreA) = Pair(true, true, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, true, settings);

            OnTheBed(cpu, coreA, 6, 11UL, 1, pool, CellTypeIds.Anchoring);
            OnTheBed(gpu, coreB, 6, 11UL, 1, pool, CellTypeIds.Anchoring);

            // The leaves near the bed, where the bodies lie.
            Vec3 axis = GpuTank.Axis(coreA);
            var low = new Vec3(axis.X, coreA.Bed.FloorY(axis.X, axis.Z) + 1.5, axis.Z);
            Leaves(coreA.LeafScent, low, 0, true);
            Leaves(coreB.LeafScent, low, 0, true);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            bool touched = false, smelt = false;
            double paid = 0;
            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "every piece, after block " + block);
                touched |= Reads(cpu, SensorChannel.BedTouch);
                smelt |= Reads(cpu, SensorChannel.LeafScentDirection);

                double a = 0, b = 0;
                foreach (Creature body in cpu.Creatures) a += body.DrainGripOwed();
                foreach (Creature body in gpu.Creatures) b += body.DrainGripOwed();
                Assert.Equal(a, b);
                paid += a;

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        OnTheBed(w, core, 1, 99UL, 50, pool, CellTypeIds.Anchoring);
                    }
                }

                Leaves(coreA.LeafScent, low, block + 1, true);
                Leaves(coreB.LeafScent, low, block + 1, true);
            }

            long formed = 0;
            foreach (Creature body in cpu.Creatures) formed += body.GripsFormed;
            _out.WriteLine($"grips formed {formed}, paid {paid:0.######} J");
            Assert.True(formed > 0, "no grip formed");
            Assert.True(touched, "no brain read a touch");
            Assert.True(smelt, "no brain read a leaf's direction");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// The scent and the drive senses (card-audit-r57.md): bodies whose genomes draw their senses
        /// from those under test, the solver's to the bit after every block. The scent's grid is
        /// refilled between blocks, as the farm refills it at a metabolic step, and once with a
        /// corpse left out of the fill, as a death after the step's fill is left out of the live
        /// field: the card must read the grid the host holds, stale or not, when the CPU reads it.
        /// Under the bend-and-power drive the drive senses read its envelope; the torque drive's
        /// world has the scent alone, since World refuses the drive senses without the drive. The
        /// CPU is first shown to read a scent and, under the drive, a drive sense that is not 0, so
        /// a sense that never reached a brain cannot pass.
        /// </summary>
        [Theory]
        [InlineData("power")]
        [InlineData("torque")]
        public void TheScentAndTheDriveSensesOnTheCpuAcceleratorAreTheSolver(string drive)
        {
            // Corpses as objects, round 55's half-hour half-life: World refuses a scent without them.
            var settings = new Dictionary<string, string> { { "EVOSIM_SENSE_SCENT", "1" }, { "EVOSIM_CORPSE_DECAY", "0.000385" } };
            if (drive == "power")
            {
                settings["EVOSIM_SENSE_DRIVE"] = "1";
                settings["EVOSIM_POWER_PUSH"] = "2";
                settings["EVOSIM_POSE_HZ"] = "2";
                settings["EVOSIM_LIMB_PUSH"] = "0.3";
            }

            (DynamicsWorld cpu, World coreA) = Pair(true, false, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, false, settings);
            Assert.NotNull(coreA.Scent);

            // Drawn from the senses under test alone, so most brains read them: from the world's whole
            // pool six brains drew no scent at all (2026-10-08).
            SensorChannel[] pool = drive == "power"
                ? new[] { SensorChannel.Scent, SensorChannel.Stroke, SensorChannel.Hold, SensorChannel.Ramp }
                : new[] { SensorChannel.Scent };
            Populate(cpu, coreA, 6, 11UL, 1, pool);
            Populate(gpu, coreB, 6, 11UL, 1, pool);

            Vec3 axis = GpuTank.Axis(coreA);
            Smell(coreA.Scent, axis, 0);
            Smell(coreB.Scent, axis, 0);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            bool scented = false, sensedDrive = false;
            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, drive + ", after block " + block);
                scented |= Reads(cpu, SensorChannel.Scent);
                sensedDrive |= Reads(cpu, SensorChannel.Stroke) || Reads(cpu, SensorChannel.Hold) ||
                               Reads(cpu, SensorChannel.Ramp);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        Populate(w, core, 1, 99UL, 50, pool);
                    }
                }

                // The farm's refill at a metabolic step, between blocks; at block 2 a corpse is
                // left out of the fill.
                Smell(coreA.Scent, axis, block + 1);
                Smell(coreB.Scent, axis, block + 1);
            }

            Assert.True(scented, drive + ": no body read a scent that was not 0");
            Assert.Equal(drive == "power", sensedDrive);
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// D146's one brain (card-audit-r57.md, the central brain), in round 57's own setting: the
        /// bend-and-power drive with its push, the scent and the drive senses, bodies grown with one
        /// brains wired from the world's own pool, the solver's to the bit after every block. Body 1
        /// has a brain of about 600 links and dies after block 1, a small body is born, and after
        /// block 2 one of about 1,000 links is born, near seed 1's largest, so the link pool must
        /// grow mid-run. At the default slack the runs are placed and kept; at a slack far below 0
        /// the pools are laid again from nothing every block. The world is first shown to hold a
        /// body whose brain has links and ports, and the test asserts the pool grew and, at the low
        /// slack, that it was laid again, so a pool the card never used cannot pass.
        /// </summary>
        [Theory]
        [InlineData(4096)]
        [InlineData(-1000000)]
        public void TheOneBrainOnTheCpuAcceleratorIsTheSolver(int slack)
        {
            var settings = new Dictionary<string, string>
            {
                { "EVOSIM_BRAIN_MODEL", "central" }, { "EVOSIM_NEURON_COST", "0" }, { "EVOSIM_CONNECTION_COST", "0" },
                { "EVOSIM_POWER_PUSH", "2" }, { "EVOSIM_POSE_HZ", "2" }, { "EVOSIM_LIMB_PUSH", "0.3" },
                { "EVOSIM_SENSE_SCENT", "1" }, { "EVOSIM_SENSE_DRIVE", "1" }, { "EVOSIM_CORPSE_DECAY", "0.000385" },
            };

            (DynamicsWorld cpu, World coreA) = Pair(true, false, settings);
            (DynamicsWorld gpu, World coreB) = Pair(true, false, settings);

            SensorChannel[] pool = coreA.Config.SensorPool();
            Big(cpu, coreA, 1, 600, 21UL, pool);
            Big(gpu, coreB, 1, 600, 21UL, pool);
            Populate(cpu, coreA, 5, 11UL, 2, pool, central: true);
            Populate(gpu, coreB, 5, 11UL, 2, pool, central: true);

            Assert.All(cpu.Creatures, c => Assert.True(c.Brain.IsCentral));
            Assert.Contains(cpu.Creatures, c => c.Brain.LinkCount > 0 && c.Brain.PortCount > 0);
            Assert.True(cpu.Creatures[0].Brain.LinkCount >= 400, "body 1's brain has " + cpu.Creatures[0].Brain.LinkCount + " links");
            Describe(cpu);

            Vec3 axis = GpuTank.Axis(coreA);
            Smell(coreA.Scent, axis, 0);
            Smell(coreB.Scent, axis, 0);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            backend.PoolSlack = slack;
            gpu.UseBackend(backend);

            int roomAfterFirst = 0;
            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "the one brain at slack " + slack + ", after block " + block);
                if (block == 0) roomAfterFirst = backend.LinkPoolCapacity;

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        Populate(w, core, 1, 99UL, 50, pool, central: true);
                    }
                }

                if (block == 2)
                {
                    Creature born = Big(cpu, coreA, 51, 1000, 23UL, pool);
                    Big(gpu, coreB, 51, 1000, 23UL, pool);
                    Assert.True(born.Brain.LinkCount >= 700, "body 51's brain has " + born.Brain.LinkCount + " links");
                }

                Smell(coreA.Scent, axis, block + 1);
                Smell(coreB.Scent, axis, block + 1);
            }

            Describe(cpu);
            _out.WriteLine("link pool room " + roomAfterFirst + " after block 0, " + backend.LinkPoolCapacity +
                           " at the end; laid again " + backend.PoolRelays + " times");
            Assert.True(backend.LinkPoolCapacity > roomAfterFirst, "the link pool never grew after block 0");
            if (slack < 0) Assert.True(backend.PoolRelays >= 4, "the pools were laid again " + backend.PoolRelays + " times");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        private static Creature Big(DynamicsWorld world, World core, int id, int links, ulong seed, SensorChannel[] pool)
        {
            Vec3 axis = GpuTank.Axis(core);
            Creature c = GpuTank.Body(id, world.Config, core, new Rng(seed), new Vec3(axis.X + 1.5, -10.5, axis.Z - 1.0),
                                      sensorPool: pool, central: true, bigLinks: links);
            world.AddInIdOrder(c);
            return c;
        }

        private void Describe(DynamicsWorld world)
        {
            foreach (Creature c in world.Creatures)
            {
                _out.WriteLine("body " + c.Id + ": " + c.Brain.NeuronCount + " units, " + c.Brain.LinkCount + " links, " +
                               c.Brain.PortCount + " ports, " + c.Brain.TotalDof + " dof");
            }
        }

        /// <summary>
        /// A refill of the scent from three corpses at the cluster, moving and growing with
        /// <paramref name="fill"/>; at fill 3 the last is left out, a prefix fill.
        /// </summary>
        private static void Smell(ScentField scent, Vec3 axis, int fill)
        {
            scent.Clear();
            int corpses = fill == 3 ? 2 : 3;
            for (int c = 0; c < corpses; c++)
            {
                var at = new Float3((float)(axis.X + 0.8 * c - 0.8 + 0.3 * fill), -9.5f + 0.4f * c, (float)(axis.Z - 0.5 * fill));
                scent.Add(at, 40.0 * (c + 1) + 15.0 * fill);
            }
        }

        /// <summary>Whether a body of the CPU's world whose brain draws the channel reads it as not 0 now.</summary>
        /// <summary>
        /// Bodies a hand's breadth above the shaped bed's own surface, in a row across the tank's
        /// axis, so their links meet the rock from the first steps.
        /// </summary>
        private static void OnTheBed(DynamicsWorld world, World core, int count, ulong seed, int firstId, SensorChannel[] pool,
                                     string bodyCell = null)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                double x = axis.X + 1.5 * (k % 3) - 1.5;
                double z = axis.Z + 1.5 * (k / 3) - 0.75;
                var at = new Vec3(x, core.Bed.FloorY(x, z) + 0.1, z);
                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at, sensorPool: pool, bodyCell: bodyCell));
            }
        }

        private static bool Reads(DynamicsWorld world, SensorChannel channel)
        {
            foreach (Creature c in world.Creatures)
            {
                if (!c.Alive || !Brain.MaskReads(c.Brain.SensorMask, channel)) continue;
                for (int part = 0; part < c.Links; part++)
                {
                    for (int index = 0; index < channel.IndexCount(); index++)
                    {
                        if (c.Senses.Read(part, channel, index) != 0f) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// EVOSIM_GPU_CONCURRENT: with the size classes on a stream each, the engine still gives
        /// the solver's numbers to the bit after every block, the death and the birth included.
        /// The card's own check is the digest at two group sizes (13af2c3's message).
        /// </summary>
        [Fact]
        public void TheClassesOnConcurrentStreamsAreTheSolver()
        {
            (DynamicsWorld cpu, World coreA) = Pair(true, true);
            (DynamicsWorld gpu, World coreB) = Pair(true, true);

            Populate(cpu, coreA, 9, 11UL, 1);
            Populate(gpu, coreB, 9, 11UL, 1);

            GpuOptions options = GpuTank.CpuDouble();
            options.Concurrent = true;
            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);
            Assert.EndsWith(" concurrent", backend.HeaderToken());

            for (int block = 0; block < 4; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "after block " + block);

                if (block == 1)
                {
                    cpu.Remove(cpu.Creatures[0].Id);
                    gpu.Remove(gpu.Creatures[0].Id);
                    Populate(cpu, coreA, 1, 99UL, 50);
                    Populate(gpu, coreB, 1, 99UL, 50);
                }
            }

            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>Section 8's test 5: a lost body hashes the same on both engines from its loss on.</summary>
        [Fact]
        public void ALostBodyDigestsTheSameOnBothEngines()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "gpu-tests", "digest");
            string dirA = Path.Combine(root, "cpu"), dirB = Path.Combine(root, "gpu");
            foreach (string d in new[] { dirA, dirB })
            {
                if (Directory.Exists(d)) Directory.Delete(d, recursive: true);
                Directory.CreateDirectory(d);
            }

            (DynamicsWorld cpu, World coreA) = Pair(perPart: false, reefs: false);
            (DynamicsWorld gpu, World coreB) = Pair(perPart: false, reefs: false);
            Populate(cpu, coreA, 3, 5UL, 1);
            Populate(gpu, coreB, 3, 5UL, 1);

            // The poison: a non-finite velocity on one link before the first step, which the
            // integration spreads through the body and the finiteness check then loses.
            cpu.Creatures[1].Velocity[0] = double.NaN;
            gpu.Creatures[1].Velocity[0] = double.NaN;
            cpu.Creatures[2].InjectNonFiniteForTest(0);
            gpu.Creatures[2].InjectNonFiniteForTest(0);

            cpu.EnableDigest(dirA, 5, null);
            gpu.EnableDigest(dirB, 5, null);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 2; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
            }

            cpu.CloseDigest();
            gpu.CloseDigest();

            Assert.False(cpu.Creatures[1].Alive);
            Assert.False(gpu.Creatures[1].Alive);
            Assert.Equal(cpu.Creatures[2].FirstNonFiniteStep, gpu.Creatures[2].FirstNonFiniteStep);

            string a = File.ReadAllText(Path.Combine(dirA, "digest.jsonl"));
            string b = File.ReadAllText(Path.Combine(dirB, "digest.jsonl"));
            Assert.False(string.IsNullOrEmpty(a));
            Assert.Equal(a, b);
            AssertSame(cpu, gpu, "after the loss");
        }

        /// <summary>
        /// Section 8's test 4, as it now stands: a body over the top class stops the block before
        /// anything steps, with an error that names it and its step, and no body is lost or changed.
        /// The card does not shrink what a body grew to fit it (card-audit-r57.md, the central brain).
        /// </summary>
        [Fact]
        public void ABodyOverTheTopClassStopsTheRunAndNothingSteps()
        {
            (DynamicsWorld gpu, World core) = Pair(perPart: false, reefs: false);
            Populate(gpu, core, 5, 3UL, 1);

            int least = int.MaxValue;
            foreach (Creature c in gpu.Creatures) least = Math.Min(least, Math.Max(GpuSlots.NeuronsOf(c), c.Brain.NeuronCount));

            Creature over = null;
            foreach (Creature c in gpu.Creatures)
            {
                if (over == null && Math.Max(GpuSlots.NeuronsOf(c), c.Brain.NeuronCount) > least) over = c;
            }

            Assert.True(over != null, "the draw gave every body the same neuron count");

            GpuOptions options = GpuTank.CpuDouble();
            int ceiling = Math.Max(1, least);
            options.ClassNeurons = new[] { ceiling, ceiling, ceiling, ceiling };

            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);

            AssertStopsAndNothingSteps(gpu, over.Id);
        }

        /// <summary>
        /// The same with the default classes and a brain of 257 neurons, one over the top class's 256:
        /// the body is planted whole, and the run refuses it rather than losing it.
        /// </summary>
        [Fact]
        public void ABrainOverTheTopClassStopsTheRun()
        {
            (DynamicsWorld gpu, World core) = Pair(perPart: false, reefs: false);
            Populate(gpu, core, 3, 3UL, 1);

            var rng = new Rng(17UL);
            Genome genome = GenomeFactory.RandomViable(rng, minParts: 2);
            var neurons = new NeuronDef[257];
            for (int n = 0; n < neurons.Length; n++) neurons[n] = new NeuronDef { Bias = 0.1f };
            genome.Nodes[0].Neurons = neurons;

            Phenotype plan = Developer.Develop(genome, DevelopmentLimits.Default);
            var big = new Creature(90, plan, gpu.Config);
            Vec3 axis = GpuTank.Axis(core);
            big.PlaceAsDeveloped(new Vec3(axis.X + 2.0, -8.0, axis.Z + 2.0), plan);
            big.Senses.Nutrients = core.Nutrients;
            gpu.AddInIdOrder(big);
            Assert.True(Math.Max(GpuSlots.NeuronsOf(big), big.Brain.NeuronCount) >= 257);
            Assert.True(big.Links <= 16, "the planted body is over the top class by its links, not its brain");

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            AssertStopsAndNothingSteps(gpu, big.Id);
        }

        private static void AssertStopsAndNothingSteps(DynamicsWorld gpu, int id)
        {
            var before = new Dictionary<int, double>();
            foreach (Creature c in gpu.Creatures) before[c.Id] = c.Position[1];

            var e = Assert.Throws<NotSupportedException>(() => gpu.StepBlock(25));
            Assert.Contains("body " + id + " at step 0", e.Message);
            Assert.Contains("cpu engine", e.Message);

            Assert.Equal(0L, gpu.Steps);
            foreach (Creature c in gpu.Creatures)
            {
                Assert.True(c.Alive);
                Assert.Equal(before[c.Id], c.Position[1]);
            }
        }
    }
}
