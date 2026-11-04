using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A film window's stroke log (<see cref="StrokeRecorder"/>): the shape of a line, that the power
    /// in it is the drive's own, and that the pivot and the axis are where the solver has them.
    /// </summary>
    /// <remarks>
    /// Narrowed to <see cref="StrokeRecorder.AppendBody"/> on a hand-built chain: a stepped world
    /// would need a founded jointed body with power by chance, and the window itself needs a recorded
    /// run to replay. The line's frame around the entries is built here the way
    /// <see cref="StrokeRecorder.WriteFrame"/> builds it.
    /// </remarks>
    public class FilmStrokeTests
    {
        private static SolverConfig PoweredWater()
        {
            return new SolverConfig
            {
                StepSeconds = 0.01,
                Density = 1000,
                DragCoefficient = 1.5,
                PanelsPerAxis = 2,
                AddedMassCoefficient = 0.5,
                FluidAccelerationCoefficient = 0,
                TissueExcessDensity = 0,
                NeutralBodyVolume = 0,
                SurfaceRestoringFraction = 0,
                WorldDepthMetres = 1e6,
                TankRadiusMetres = 0,
                CreatureContact = false,
                JointDriveDamping = 1,
                LimbPushCoefficient = 0.3,
                PowerPushRadiansPerSecond = 6.0,
                PoseHertz = 2.0,
            };
        }

        /// <summary>A box root and <paramref name="links"/> - 1 hinged boxes in a line, with no neurons.</summary>
        private static Creature Chain(int links)
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

            for (int i = 1; i < links; i++)
            {
                genome.Nodes.Add(new MorphNode
                {
                    Dimensions = new Float3(0.2f, 0.05f, 0.2f),
                    CellTypeId = CellTypeIds.Link,
                    ShapeId = ShapeIds.Box,
                    JointType = JointType.Hinge,
                    JointLimits = new[] { new Float2(-1.2f, 1.2f) },
                    Power = 10f,
                    RecursiveLimit = 1,
                    Neurons = Array.Empty<NeuronDef>(),
                    Drive = new DriveGenes(0f, 0.02f, 0.02f, 0.02f),
                });

                genome.Nodes[i - 1].Edges.Add(new MorphEdge
                {
                    Child = i,
                    ParentAnchor = new Float3(1f, 0f, 0f),
                    ChildAnchor = new Float3(-1f, 0f, 0f),
                    Orientation = Quat.Identity,
                    Scale = Float3.One,
                });
            }

            Phenotype phenotype = Developer.Develop(genome, DevelopmentLimits.Default);
            return new Creature(7, phenotype, PoweredWater());
        }

        /// <summary>The frame's line as the recorder builds it, parsed.</summary>
        private static JsonElement Line(Creature body, long id)
        {
            var row = new StringBuilder();
            row.Append("{\"t\":").Append((24000.04167).ToString("R", CultureInfo.InvariantCulture)).Append(",\"b\":[");
            StrokeRecorder.AppendBody(row, id, body);
            row.Append("]}");

            string text = row.ToString();
            Assert.DoesNotContain('\n', text);
            Assert.DoesNotContain('\r', text);

            return JsonDocument.Parse(text).RootElement.Clone();
        }

        /// <summary>A tolerance and not a rounding: the line carries floats, and a rounded pair can straddle a digit.</summary>
        private static void Near(double expected, double actual, double tolerance) =>
            Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected}, got {actual}");

        [Fact]
        public void APoweredHingeIsOneEntryWithTheDrivesPowerItsPivotAndItsAxis()
        {
            Creature body = Chain(2);
            Assert.True(body.Drive.BendAndPower);

            // The envelope's own path: a half-power command held until the ramp has run.
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { 0.5f }, new[] { 0f });
            Assert.Equal(0.5, body.Drive.Stroke[0], 6);

            // A turned root and a bent joint, so a pivot or an axis taken in the wrong frame shows.
            body.PlaceAt(new Vec3(1.0, -20.0, 3.0), QuatD.FromAxisAngle(Vec3.UnitY, 0.7));
            body.Q[0] = 0.4;
            Kinematics.Refresh(body);

            JsonElement line = Line(body, 7);
            Assert.Equal(24000.04167, line.GetProperty("t").GetDouble());

            JsonElement entries = line.GetProperty("b");
            Assert.Equal(1, entries.GetArrayLength());

            JsonElement e = entries[0];
            Assert.Equal(10, e.GetArrayLength());
            Assert.Equal(7, e[0].GetInt64());
            Assert.Equal(1, e[1].GetInt32());
            Assert.Equal(0, e[2].GetInt32());
            Near(body.Drive.Stroke[0], e[3].GetDouble(), 1e-6);

            // The pivot is the child's own anchor from its end of the joint as well as the parent's
            // from its: the two places the solver puts the same point.
            Vec3 viaChild = Vec3.Read(body.Position, 3) +
                            Mat3.Read(body.RotationMatrix, 9) * Vec3.Read(body.ChildAnchor, 3);
            Near(viaChild.X, e[4].GetDouble(), 1e-4);
            Near(viaChild.Y, e[5].GetDouble(), 1e-4);
            Near(viaChild.Z, e[6].GetDouble(), 1e-4);

            // The axis is the solver's, in world axes, of unit length: link 1's first column.
            Vec3 sang = Vec3.Read(body.Sang, 9);
            Near(sang.X, e[7].GetDouble(), 1e-6);
            Near(sang.Y, e[8].GetDouble(), 1e-6);
            Near(sang.Z, e[9].GetDouble(), 1e-6);
            double length = Math.Sqrt(
                e[7].GetDouble() * e[7].GetDouble() + e[8].GetDouble() * e[8].GetDouble() + e[9].GetDouble() * e[9].GetDouble());
            Near(1.0, length, 1e-6);
        }

        [Fact]
        public void AnUnpoweredBodyIsAnEmptyListAndOnlyThePoweredLinkIsNamed()
        {
            Creature body = Chain(3);
            Kinematics.Refresh(body);

            // Nothing powered: a line with an empty list, so the two files keep step.
            Assert.Equal(0, Line(body, 7).GetProperty("b").GetArrayLength());

            // The second hinge alone, as the drive's array has it: link 2's one degree of freedom is slot 1.
            body.Drive.Stroke[1] = 0.25;

            JsonElement entries = Line(body, 7).GetProperty("b");
            Assert.Equal(1, entries.GetArrayLength());
            Assert.Equal(2, entries[0][1].GetInt32());
            Assert.Equal(0, entries[0][2].GetInt32());
            Near(0.25, entries[0][3].GetDouble(), 1e-6);

            // Both, in link order, comma separated.
            body.Drive.Stroke[0] = 1.0;
            entries = Line(body, 7).GetProperty("b");
            Assert.Equal(2, entries.GetArrayLength());
            Assert.Equal(1, entries[0][1].GetInt32());
            Assert.Equal(2, entries[1][1].GetInt32());
        }
    }
}
