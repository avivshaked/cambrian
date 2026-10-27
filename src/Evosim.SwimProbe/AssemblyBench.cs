using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.SwimProbe
{
    /// <summary>
    /// D231's bench (<c>--assembly-bench</c>, a sibling of <c>--fin-bench</c>, which is untouched): three bodies
    /// of equal limb volume, each a root with one hinged limb, stroked alike by hand in still water at four
    /// powers under three laws. CPU only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bodies.</b> <i>blade</i>: a small box link, half-extents 0.06 x 0.03 x 0.03 m, with a flat box blade
    /// welded at its tip, thin in the stroke's direction (Y) and wide across it (Z), 0.01 and 0.2 m half-extents
    /// like the fin bench's paddle; its length is solved so that the two parts together have the paddle's volume.
    /// <i>flat</i>: the fin bench's paddle as the jointed link itself, 0.2 x 0.01 x 0.2 m. <i>stick</i>: the fin
    /// bench's stick, one box link of the same volume, as thick as it is wide. The dimensions and the three
    /// volumes are printed.
    /// </para>
    /// <para>
    /// <b>The laws.</b> <i>off</i>: no fin law, the push depends on the limb's volume alone (D135). <i>d229</i>:
    /// the fin law, which pushes the jointed link's own part only. <i>d231</i>: the fin law and the stroke that
    /// pushes its welded assembly. <b>The powers</b> are the stroke's peak, 0.8, 0.4, 0.295 and 0.2.
    /// </para>
    /// <para>
    /// Each row reports the distance the centre of mass travelled (m), the mechanical work the body was billed
    /// (J) and the work per metre (J/m).
    /// </para>
    /// </remarks>
    internal static class AssemblyBench
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly MethodInfo JointTorques =
            typeof(DynamicsWorld).GetMethod("JointTorques", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly double[] Powers = { 0.8, 0.4, 0.295, 0.2 };

        private static readonly Float3 BladeLink = new Float3(0.06f, 0.03f, 0.03f);

        /// <summary>The blade's thickness and chord, half-extents, as the paddle's.</summary>
        private const float BladeThick = 0.01f;
        private const float BladeChord = 0.2f;

        private static double Volume(Float3 half) => 8.0 * Math.Abs(half.X) * Math.Abs(half.Y) * Math.Abs(half.Z);

        public static int Run(double seconds, double push, double omega, double hertz, string outDir)
        {
            Float3 flat = FinBench.Paddle;
            Float3 stick = FinBench.Stick;
            double total = Volume(flat);

            // The blade takes what the small link leaves of the paddle's volume.
            double bladeLength = (total - Volume(BladeLink)) / (8.0 * BladeThick * BladeChord);
            var blade = new Float3((float)bladeLength, BladeThick, BladeChord);

            Console.WriteLine(FormattableString.Invariant(
                $"=== assembly bench: {seconds} s, limb push {push}, stroke rate {omega} rad/s, {hertz} Hz"));
            Console.WriteLine(FormattableString.Invariant(
                $"  blade: link half-extents {BladeLink.X} x {BladeLink.Y} x {BladeLink.Z} m + blade half-extents {blade.X:0.######} x {blade.Y} x {blade.Z} m welded at its tip"));
            Console.WriteLine(FormattableString.Invariant(
                $"  flat : link half-extents {flat.X} x {flat.Y} x {flat.Z} m"));
            Console.WriteLine(FormattableString.Invariant(
                $"  stick: link half-extents {stick.X} x {stick.Y:0.######} x {stick.Z:0.######} m"));
            Console.WriteLine(FormattableString.Invariant(
                $"  limb volumes (m3): blade {Volume(BladeLink) + Volume(blade):0.########}, flat {Volume(flat):0.########}, stick {Volume(stick):0.########}"));

            var rows = new List<string> { "body,law,power,limbVolume,distance,workJoules,workPerMetre" };
            foreach (string law in new[] { "off", "d229", "d231" })
            {
                foreach (double power in Powers)
                {
                    foreach (string name in new[] { "blade", "flat", "stick" })
                    {
                        Genome genome = name == "blade" ? Bladed(BladeLink, blade) : Single(name == "flat" ? flat : stick);
                        (double volume, double distance, double work) = Stroke(genome, law, seconds, push, omega, power, hertz);
                        double perMetre = distance > 0 ? work / distance : double.NaN;
                        string line = string.Join(",",
                            name, law, power.ToString("0.###", Inv),
                            volume.ToString("0.######", Inv), distance.ToString("0.#####", Inv),
                            work.ToString("0.#####", Inv), double.IsNaN(perMetre) ? "nan" : perMetre.ToString("0.#####", Inv));
                        rows.Add(line);
                        Console.WriteLine("  " + line);
                    }
                }
            }

            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "assembly-bench.csv"), string.Join("\n", rows) + "\n", new UTF8Encoding(false));
            }

            return 0;
        }

        private static SolverConfig Water(string law, double push, double omega, double hertz) => new SolverConfig
        {
            StepSeconds = 0.01,
            Density = 1000,
            DragCoefficient = 1.5,
            PanelsPerAxis = 2,
            AddedMassCoefficient = 0.5,
            WorldDepthMetres = 1e6,
            TankRadiusMetres = 0,
            CreatureContact = false,
            JointDriveDamping = 1,
            LimbPushCoefficient = push,
            PowerPushRadiansPerSecond = omega,
            PoseHertz = hertz,
            ShapedAddedMass = law != "off",
            StrokePushesAssembly = law == "d231",
        };

        private static MorphNode Root() => new MorphNode
        {
            Dimensions = new Float3(0.2f, 0.2f, 0.2f),
            CellTypeId = CellTypeIds.Structural,
            ShapeId = ShapeIds.Box,
            JointType = JointType.Fixed,
            JointLimits = Array.Empty<Float2>(),
            RecursiveLimit = 1,
        };

        private static MorphNode Hinged(Float3 half) => new MorphNode
        {
            Dimensions = half,
            CellTypeId = CellTypeIds.Link,
            ShapeId = ShapeIds.Box,
            JointType = JointType.Hinge,
            JointLimits = new[] { new Float2(-1.2f, 1.2f) },
            Power = 10f,
            RecursiveLimit = 1,
            Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
            },
        };

        private static MorphEdge Tip(int child) => new MorphEdge
        {
            Child = child,
            ParentAnchor = new Float3(1f, 0f, 0f),
            ChildAnchor = new Float3(-1f, 0f, 0f),
            Orientation = Quat.Identity,
            Scale = Float3.One,
        };

        private static Genome Single(Float3 limb)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };
            genome.Nodes.Add(Root());
            genome.Nodes.Add(Hinged(limb));
            genome.Nodes[0].Edges.Add(Tip(1));
            return genome;
        }

        /// <summary>The small hinged link, with a flat blade welded at its tip, in line with it.</summary>
        private static Genome Bladed(Float3 link, Float3 blade)
        {
            Genome genome = Single(link);
            genome.Nodes.Add(new MorphNode
            {
                Dimensions = blade,
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });
            genome.Nodes[1].Edges.Add(Tip(2));
            return genome;
        }

        /// <summary>
        /// One run, as the fin bench's: the power rises and falls as |sin| at <paramref name="hertz"/> Hz and peaks
        /// at <paramref name="stroke"/>, set straight into the drive as the envelope would write it.
        /// </summary>
        private static (double volume, double distance, double work) Stroke(
            Genome genome, string law, double seconds, double push, double omega, double stroke, double hertz)
        {
            SolverConfig solver = Water(law, push, omega, hertz);
            Phenotype plan = Developer.Develop(genome, DevelopmentLimits.Default);
            var body = new Creature(0, plan, solver);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);

            var world = new DynamicsWorld(solver);
            world.Add(body);
            var jointArgs = new object[] { body };

            double dt = solver.StepSeconds;
            int steps = (int)Math.Round(seconds / dt);
            Vec3 start = body.CentreOfMass();

            for (int step = 0; step < steps; step++)
            {
                double t = step * dt;
                body.Drive.Stroke[0] = stroke * Math.Abs(Math.Sin(2 * Math.PI * hertz * t));

                Array.Clear(body.Fext, 0, body.Fext.Length);
                Array.Clear(body.Tau, 0, body.Tau.Length);
                Fluid.Apply(body, solver);
                JointTorques.Invoke(world, jointArgs);
                Aba.Solve(body);
                Aba.Integrate(body, dt);
                Kinematics.Poses(body);
                Kinematics.Velocities(body);
                body.Settle(dt);
                if (!body.IsFinite()) throw new InvalidOperationException("the body went non-finite at step " + step);
            }

            double limb = 0;
            for (int i = 1; i < body.Links; i++) limb += body.Volume[i];

            double distance = (body.CentreOfMass() - start).Magnitude;
            return (limb, distance, body.MechanicalWorkJoules);
        }
    }
}
