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
    /// D229's fin-law bench (<c>--fin-bench</c>): two bodies of equal total volume, each a root with
    /// one powered limb, one a stick and one a paddle flat across its stroke, stroked alike by hand
    /// in still water with the shaped added mass off and on. Four runs; each reports the distance
    /// the centre of mass travelled, the mechanical work the body was billed, and the work per metre.
    /// With the law off the push depends on the limb's volume alone, so the two travel alike (D135);
    /// with it on the paddle must travel further. CPU only.
    /// </summary>
    internal static class FinBench
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly MethodInfo JointTorques =
            typeof(DynamicsWorld).GetMethod("JointTorques", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>The limb's half-extents: the same length and chord, and the stick as thick as it is wide.</summary>
        internal static readonly Float3 Paddle = new Float3(0.2f, 0.01f, 0.2f);
        internal static readonly Float3 Stick =new Float3(0.2f, (float)Math.Sqrt(0.002), (float)Math.Sqrt(0.002));

        public static int Run(double seconds, double push, double omega, double stroke, double hertz, string outDir)
        {
            Console.WriteLine(FormattableString.Invariant(
                $"=== fin bench: {seconds} s, limb push {push}, stroke rate {omega} rad/s, power {stroke} at {hertz} Hz"));

            var rows = new List<string> { "body,shapedAddedMass,volume,distance,workJoules,workPerMetre" };
            foreach (bool shaped in new[] { false, true })
            {
                foreach ((string name, Float3 half) in new[] { ("stick", Stick), ("paddle", Paddle) })
                {
                    (double volume, double distance, double work) = Stroke(half, shaped, seconds, push, omega, stroke, hertz);
                    double perMetre = distance > 0 ? work / distance : double.NaN;
                    string line = string.Join(",",
                        name, shaped ? "on" : "off",
                        volume.ToString("0.######", Inv), distance.ToString("0.#####", Inv),
                        work.ToString("0.#####", Inv), double.IsNaN(perMetre) ? "nan" : perMetre.ToString("0.#####", Inv));
                    rows.Add(line);
                    Console.WriteLine("  " + line);
                }
            }

            if (outDir != null)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "fin-bench.csv"), string.Join("\n", rows) + "\n", new UTF8Encoding(false));
            }

            return 0;
        }

        private static SolverConfig Water(bool shaped, double push, double omega, double hertz) => new SolverConfig
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
            ShapedAddedMass = shaped,
        };

        private static Genome Body(Float3 limb)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = limb,
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
            });

            genome.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            return genome;
        }

        /// <summary>
        /// One run. The hand-made stroke is the same for every body: the power rises and falls as
        /// |sin| at <paramref name="hertz"/> Hz and peaks at <paramref name="stroke"/>, set straight
        /// into the drive as the envelope would write it. The push is never negative (it always drives
        /// the limb toward its joint), so the body is pushed in pulses.
        /// </summary>
        private static (double volume, double distance, double work) Stroke(
            Float3 limb, bool shaped, double seconds, double push, double omega, double stroke, double hertz)
        {
            SolverConfig solver = Water(shaped, push, omega, hertz);
            Phenotype plan = Developer.Develop(Body(limb), DevelopmentLimits.Default);
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

            double distance = (body.CentreOfMass() - start).Magnitude;
            return (body.TotalVolume, distance, body.MechanicalWorkJoules);
        }
    }
}
