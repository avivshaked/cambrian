using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// Hand-built genomes. <see cref="Phenotype"/>'s parts are only assembled by
    /// <see cref="Developer"/>, so a test body is a genome developed rather than a part list
    /// written out — which is also the honest thing, because it is the developer's anchors and
    /// orientations the solver has to consume.
    /// </summary>
    public static class TestBodies
    {
        /// <summary>A world with nothing in it but the solver: no drag, no weight, no contact.</summary>
        public static SolverConfig Vacuum(double dt = 0.001) => new SolverConfig
        {
            StepSeconds = dt,
            Density = 1000,
            DragCoefficient = 0,
            PanelsPerAxis = 2,
            AddedMassCoefficient = 0,
            FluidAccelerationCoefficient = 0,
            TissueExcessDensity = 0,
            NeutralBodyVolume = 0,
            SurfaceRestoringFraction = 0,
            WorldDepthMetres = 1e6,
            TankRadiusMetres = 0,
            CreatureContact = false,

            // PhenotypeBuilder.MakeDrive's own value, kept rather than zeroed: it is what every
            // joint in the farm carries, and a vacuum with no dissipation anywhere at all lets a
            // driven chain ring against its own limit springs until the integrator gives up.
            JointDriveDamping = 1,
        };

        /// <summary>Water with drag and nothing else — no weight, no contact.</summary>
        public static SolverConfig Water(double dt = 0.01)
        {
            SolverConfig config = Vacuum(dt);
            config.DragCoefficient = 1.5;
            return config;
        }

        /// <summary>One box, no joints.</summary>
        public static Genome Box(Float3 halfExtents)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = halfExtents,
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = System.Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            return genome;
        }

        /// <summary>
        /// A chain of <paramref name="links"/> parts: a structural root and then link cells,
        /// each attached to the +X face of the one before it and each carrying one oscillator.
        /// </summary>
        public static Genome Chain(
            int links,
            JointType joint = JointType.Hinge,
            float power = 10f,
            float hz = 1f,
            Float3? rootHalf = null,
            Float3? linkHalf = null,
            float limit = 1.2f,
            bool neurons = true)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            int dof = joint.DofCount();
            var limits = new Float2[dof];
            for (int d = 0; d < dof; d++) limits[d] = new Float2(-limit, limit);

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = rootHalf ?? new Float3(0.2f, 0.2f, 0.2f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = System.Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            for (int i = 1; i < links; i++)
            {
                var node = new MorphNode
                {
                    Dimensions = linkHalf ?? new Float3(0.2f, 0.05f, 0.2f),
                    CellTypeId = CellTypeIds.Link,
                    ShapeId = ShapeIds.Box,
                    JointType = joint,
                    JointLimits = (Float2[])limits.Clone(),
                    Power = power,
                    RecursiveLimit = 1,
                    Neurons = neurons
                        ? OneOscillatorPerDof(dof, hz, i * 0.6f)
                        : System.Array.Empty<NeuronDef>(),
                };

                genome.Nodes.Add(node);

                genome.Nodes[i - 1].Edges.Add(new MorphEdge
                {
                    Child = i,
                    ParentAnchor = new Float3(1f, 0f, 0f),
                    ChildAnchor = new Float3(-1f, 0f, 0f),
                    Orientation = Quat.Identity,
                    Scale = Float3.One,
                });
            }

            return genome;
        }

        private static NeuronDef[] OneOscillatorPerDof(int dof, float hz, float phase)
        {
            var set = new NeuronDef[dof < 1 ? 1 : dof];
            for (int d = 0; d < set.Length; d++)
            {
                set[d] = new NeuronDef
                {
                    Op = NeuronOp.OscillateWave,
                    Frequency = hz,
                    Phase = phase + d * 1.1f,
                    Amplitude = 1f,
                    Bias = 0f,
                    Inputs = System.Array.Empty<NeuronInput>(),
                };
            }
            return set;
        }

        public static Creature Build(Genome genome, SolverConfig config, int id = 0)
        {
            Phenotype body = Developer.Develop(genome, DevelopmentLimits.Default);
            return new Creature(id, body, config);
        }
    }
}
