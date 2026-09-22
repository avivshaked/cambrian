using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Evosim.Core;
using Xunit;

using RefVolume = Evosim.Sim.SharedVolume;
using RefFloor = Evosim.Sim.SeaFloor;
using PortVolume = Evosim.Dynamics.Placement.SharedVolume;
using PortFloor = Evosim.Dynamics.Placement.PlacementFloor;
using UVector3 = UnityEngine.Vector3;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The placer's port against the placer — work package H's acceptance, "founder and offspring
    /// places equal the Unity placer's to the bit, 5,000 draws".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a structural proof and not a run.</b> Unity cannot be started from a test, and a
    /// recorded run's <c>positions.jsonl</c> holds where bodies were at a sample rather than where
    /// the placer put them, so there is no recording to replay a draw against. What can be done
    /// instead is to put both placers in one process: the original file, copied byte for byte out
    /// of <c>unity/Assets/Evosim/Sim/SharedVolume.cs</c> and compiled against a shim that carries
    /// Unity's arithmetic and nothing else (<c>UnityReference/</c>), beside the port. Both are
    /// then handed the same seed, the same world, the same phenotypes and the same sequence of
    /// calls, and every answer is compared as bits.
    /// </para>
    /// <para>
    /// <b>What that proof does not cover</b> is the shim itself: if Unity's real <c>Mathf.Sin</c>
    /// were not <c>(float)Math.Sin(f)</c>, both sides here would be wrong together. The shim is
    /// therefore a transcription of Unity's source rather than a convenience, and the one
    /// substitution that could hide behind it — <c>MathF</c> in place of the double routines — is
    /// refused on both sides.
    /// </para>
    /// <para>
    /// <b>The RNG state is compared too.</b> A placer that returned the same positions while
    /// taking a different number of draws would pass a position check and still diverge at the
    /// next birth, so the test reaches into both <see cref="Rng"/> instances and asserts the
    /// recurrence is in the same place.
    /// </para>
    /// </remarks>
    public sealed class PlacementTests
    {
        private const float DepthMetres = 60f;
        private const float AreaSquareMetres = 100f;
        private const int Patches = 4;

        // ------------------------------------------------------------------- the verbatim claim

        /// <summary>
        /// The reference file is the Unity file. Asserted rather than asserted-in-a-comment,
        /// because every other test here is worthless the moment the copy drifts.
        /// </summary>
        [Fact]
        public void TheReferenceCopyIsTheUnitySourceByteForByte()
        {
            string root = RepositoryRoot();

            byte[] original = File.ReadAllBytes(
                Path.Combine(root, "unity", "Assets", "Evosim", "Sim", "SharedVolume.cs"));
            byte[] copy = File.ReadAllBytes(
                Path.Combine(root, "src", "Evosim.Dynamics.Tests", "UnityReference", "SharedVolume.cs"));

            Assert.Equal(original, copy);
        }

        // ------------------------------------------------------------------------- the parities

        /// <summary>
        /// Five thousand reservations in a box and five thousand in a tank, with commits, frees
        /// and released reservations, every answer compared as bits.
        /// </summary>
        /// <remarks>
        /// The four cases are the branches that change a draw: the shape (a founder is two draws
        /// in both, but not the same two), dispersal (0 takes no distance draw at all, which is
        /// the guarantee D088 rests on), and a shaped bed (D092's second clamp, taken inside the
        /// loop). A shaped bed under a box is not a world the campaign runs; it is here because
        /// the branch it exercises is shape-blind and a port should be caught if it is not.
        /// </remarks>
        [Theory]
        [InlineData(WorldShape.Box, 0f, 0f, 5000, 11UL)]
        [InlineData(WorldShape.Box, 1.5f, 0f, 5000, 12UL)]
        [InlineData(WorldShape.Tank, 0f, 0f, 5000, 13UL)]
        [InlineData(WorldShape.Tank, 1.5f, 0f, 5000, 14UL)]
        [InlineData(WorldShape.Tank, 1.5f, 3f, 5000, 15UL)]
        [InlineData(WorldShape.Box, 1.5f, 3f, 2000, 16UL)]
        public void ThePortPlacesWhereTheUnityPlacerPlaces(
            WorldShape shape, float dispersal, float relief, int reservations, ulong seed)
        {
            Parity(shape, dispersal, relief, reservations, seed);
        }

        /// <summary>
        /// The same again with one patch across and two rows, so <see cref="RefVolume.PatchOf"/>'s
        /// two-axis branch is the one being compared.
        /// </summary>
        [Theory]
        [InlineData(1, 2000, 21UL)]
        [InlineData(2, 2000, 22UL)]
        [InlineData(4, 2000, 23UL)]
        public void ThePortIndexesPatchesAsTheUnityPlacerDoes(int across, int reservations, ulong seed)
        {
            Parity(WorldShape.Box, 0.8f, 0f, reservations, seed, across);
        }

        // -------------------------------------------------------------------------- the geometry

        [Fact]
        public void BoundingRadiusIsTheSphereAboutTheRoot()
        {
            // One part at the origin with half-extents (0.3, 0.4, 0.5): centre 0, corner the
            // diagonal. Read against the expression rather than against the reference, so this
            // says what the number is and not only that two files agree.
            Phenotype one = HandBuilt((Float3.Zero, new Float3(0.3f, 0.4f, 0.5f)));

            float corner = (float)Math.Sqrt(0.3f * 0.3f + 0.4f * 0.4f + 0.5f * 0.5f);

            Assert.Equal(corner, PortVolume.BoundingRadius(one), 6);

            // A part 2 m out with a 0.1 m cube on it: 2 + sqrt(3)·0.1, and the root's own 0.5 is
            // smaller, so the far part is what the sphere is measured to.
            Phenotype two = HandBuilt(
                (Float3.Zero, new Float3(0.5f, 0.5f, 0.5f)),
                (new Float3(2f, 0f, 0f), new Float3(0.1f, 0.1f, 0.1f)));

            float expected = 2f + (float)Math.Sqrt(0.1f * 0.1f * 3f);

            Assert.Equal(expected, PortVolume.BoundingRadius(two), 6);

            // Floored at a centimetre, so a body of no size still reserves something.
            Assert.Equal(0.01f, PortVolume.BoundingRadius(HandBuilt((Float3.Zero, Float3.Zero))));
        }

        [Fact]
        public void BoundingRadiusAgreesWithTheUnityPlacerOnEveryDevelopedBody()
        {
            foreach (Phenotype body in Bodies(256, 7UL))
            {
                Assert.Equal(Bits(RefVolume.BoundingRadius(body)), Bits(PortVolume.BoundingRadius(body)));
            }
        }

        [Fact]
        public void APatchIsTheColumnThePositionFallsIn()
        {
            // Four patches of 5 m in a row: x ∈ [0, 20), one patch across.
            var row = new PortVolume(Patches, 5f, DepthMetres, 1UL);

            Assert.Equal(20f, row.LengthMetres);
            Assert.Equal(5f, row.WidthMetres);

            Assert.Equal(0, row.PatchOf(0f, 0f));
            Assert.Equal(0, row.PatchOf(4.99f, 2f));
            Assert.Equal(1, row.PatchOf(5f, 2f));
            Assert.Equal(3, row.PatchOf(19.9f, 2f));

            // Outside is folded in first, so a body past a face still reads a patch that exists.
            Assert.Equal(0, row.PatchOf(20.5f, 2f));
            Assert.Equal(3, row.PatchOf(-0.5f, 2f));

            // Two across: numbered along x first, so the second row starts at 2.
            var grid = new PortVolume(Patches, 5f, DepthMetres, 1UL, 0f, 2);

            Assert.Equal(2, grid.PatchesAlong);
            Assert.Equal(2, grid.PatchesAcross);
            Assert.Equal(0, grid.PatchOf(1f, 1f));
            Assert.Equal(1, grid.PatchOf(6f, 1f));
            Assert.Equal(2, grid.PatchOf(1f, 6f));
            Assert.Equal(3, grid.PatchOf(6f, 6f));
        }

        [Fact]
        public void ARingIsAPatchInATank()
        {
            float radius = TankGeometry.RadiusFor(AreaSquareMetres);
            var tank = new PortVolume(Patches, 5f, DepthMetres, 1UL, 0f, 1, WorldShape.Tank, radius);

            Assert.Equal(2f * radius, tank.LengthMetres);
            Assert.Equal(2f * radius, tank.WidthMetres);

            // The axis is ring 0 and the glass is the last ring.
            Assert.Equal(0, tank.PatchOf(radius, radius));
            Assert.Equal(Patches - 1, tank.PatchOf(2f * radius, radius));

            // Rings of equal area: the boundary of ring i is at R·sqrt(i/K).
            for (int i = 0; i < Patches; i++)
            {
                float mid = TankGeometry.MidRadiusOf(i, radius, Patches);
                Assert.Equal(i, tank.PatchOf(radius + mid, radius));
            }
        }

        [Fact]
        public void TheBoxWrapsAndTheTankDoesNot()
        {
            var box = new PortVolume(Patches, 5f, DepthMetres, 1UL);

            Assert.False(box.TryWrap(new Float3(1f, -3f, 1f), out Float3 inside));
            Assert.Equal(1f, inside.X);
            Assert.Equal(0L, box.Wraps);

            Assert.True(box.TryWrap(new Float3(21f, -3f, -1f), out Float3 wrapped));
            Assert.Equal(1f, wrapped.X, 5);
            Assert.Equal(4f, wrapped.Z, 5);
            Assert.Equal(-3f, wrapped.Y);
            Assert.Equal(1L, box.Wraps);

            // A diverged body is left where it is — the evidence, not a number of our own.
            Assert.False(box.TryWrap(new Float3(float.NaN, -3f, 1f), out _));
            Assert.False(box.TryWrap(new Float3(float.PositiveInfinity, -3f, 1f), out _));
            Assert.Equal(1L, box.Wraps);

            float radius = TankGeometry.RadiusFor(AreaSquareMetres);
            var tank = new PortVolume(Patches, 5f, DepthMetres, 1UL, 0f, 1, WorldShape.Tank, radius);

            Assert.False(tank.TryWrap(new Float3(1000f, -3f, 1000f), out _));
            Assert.Equal(0L, tank.Wraps);
        }

        [Fact]
        public void TheShortestWayRoundIsTheShorterArcInABoxAndThePlainOneInATank()
        {
            var box = new PortVolume(Patches, 5f, DepthMetres, 1UL);

            // 20 m around x: 1 and 19 are two metres apart the short way, not eighteen.
            Assert.Equal(2f, box.ShortestDistance(new Float3(1f, 0f, 2f), new Float3(19f, 0f, 2f)), 5);

            // y is not periodic.
            Assert.Equal(3f, box.ShortestDistance(new Float3(1f, 0f, 2f), new Float3(1f, -3f, 2f)), 5);

            float radius = TankGeometry.RadiusFor(AreaSquareMetres);
            var tank = new PortVolume(Patches, 5f, DepthMetres, 1UL, 0f, 1, WorldShape.Tank, radius);

            Assert.Equal(
                18f, tank.ShortestDistance(new Float3(1f, 0f, 2f), new Float3(19f, 0f, 2f)), 5);
        }

        [Fact]
        public void TheWaterTestHoldsAWholeSphereBackFromTheGlass()
        {
            float radius = TankGeometry.RadiusFor(AreaSquareMetres);
            var tank = new PortVolume(Patches, 5f, DepthMetres, 1UL, 0f, 1, WorldShape.Tank, radius);

            // A centimetre inside the circle is in the water for a point and out of it for a body
            // with half a metre of sphere around it — wall-clearance-spec.md, Astra review F2.
            float justInside = 2f * radius - 0.01f;

            Assert.True(tank.InTheWater(justInside, radius));
            Assert.False(tank.InTheWater(justInside, radius, 0.5f));

            // A box has no outside.
            var box = new PortVolume(Patches, 5f, DepthMetres, 1UL);
            Assert.True(box.InTheWater(-100f, 100f, 5f));
        }

        [Fact]
        public void TheHashCellIsTwiceTheLargestBodyFlooredAtAMetre()
        {
            var small = new PortVolume(Patches, 5f, DepthMetres, 1UL);
            small.Note(1L, new Float3(1f, -1f, 1f), 0.2f);

            // Built lazily, on the first free test — which a founder reservation makes.
            float height = -10f;
            Assert.True(small.TryReserveFounder(Bodies(1, 3UL)[0], ref height, out _));
            Assert.Equal(1f, small.CellMetres);

            var large = new PortVolume(Patches, 5f, DepthMetres, 1UL);
            large.Note(1L, new Float3(1f, -1f, 1f), 3f);

            height = -10f;
            Assert.True(large.TryReserveFounder(Bodies(1, 3UL)[0], ref height, out _));
            Assert.Equal(6f, large.CellMetres);
        }

        /// <summary>
        /// D109's founder rule: with an acceptance set, a founder is planted only where it says,
        /// and the refusals are counted; with none, nothing is asked.
        /// </summary>
        [Fact]
        public void AFounderIsPlantedWhereTheAcceptanceSays()
        {
            var planted = new PortVolume(Patches, 5f, DepthMetres, 9UL)
            {
                FounderAcceptance = (x, z) => x < 4f ? 1f : 0f,
            };

            Phenotype[] bodies = Bodies(12, 9UL);

            for (int i = 0; i < bodies.Length; i++)
            {
                float height = -5f - i;
                Assert.True(planted.TryReserveFounder(bodies[i], ref height, out _));
                planted.Commit(i);

                Assert.True(planted.TryTakePlacement(i, out Float3 at));
                Assert.True(at.X < 4f, $"founder {i} planted at x = {at.X:0.##}, outside the accepted strip");
            }

            Assert.True(planted.DesertRefusals > 0, "a strip a fifth of the box wide refuses most candidates");

            var anywhere = new PortVolume(Patches, 5f, DepthMetres, 9UL);
            float h = -5f;
            Assert.True(anywhere.TryReserveFounder(bodies[0], ref h, out _));
            Assert.Equal(0L, anywhere.DesertRefusals);
        }

        [Fact]
        public void TheBedRaisesAPlacementAndNeverLowersIt()
        {
            var box = new PortVolume(Patches, 5f, DepthMetres, 1UL)
            {
                Floor = new PortFloor(DepthMetres),
            };

            Phenotype body = Bodies(1, 5UL)[0];
            float radius = PortVolume.BoundingRadius(body);

            // Drawn inside the rock: clamped to the rock's top plus the sphere plus the clearance.
            float height = -DepthMetres - 5f;
            Assert.True(box.TryReserveFounder(body, ref height, out _));
            Assert.Equal(-DepthMetres + radius + PortFloor.ClearanceMetres, height, 5);

            // Drawn in open water: untouched.
            var second = new PortVolume(Patches, 5f, DepthMetres, 2UL)
            {
                Floor = new PortFloor(DepthMetres),
            };

            height = -10f;
            Assert.True(second.TryReserveFounder(body, ref height, out _));
            Assert.Equal(-10f, height);
        }

        [Fact]
        public void ACrowdedNeighbourhoodRefusesTheBirthAndCountsIt()
        {
            var box = new PortVolume(Patches, 5f, DepthMetres, 1UL);

            Phenotype body = Bodies(1, 9UL)[0];
            float radius = PortVolume.BoundingRadius(body);

            var parent = Creature(1L, 0);
            box.Note(1L, new Float3(10f, -30f, 2.5f), radius);

            // A shell of occupants at exactly the distance a child is placed at, packed close
            // enough that no direction on the circle is free.
            for (int i = 0; i < 720; i++)
            {
                double a = i * (2d * Math.PI / 720d);
                float d = 2f * radius;

                box.Note(
                    100L + i,
                    new Float3(10f + d * (float)Math.Cos(a), -30f, 2.5f + d * (float)Math.Sin(a)),
                    radius);
            }

            float height = -30f;
            Assert.False(box.TryReserveOffspring(parent, body, ref height, out int patch));

            Assert.Equal(1L, box.Refusals);
            Assert.Equal(RefVolume.AttemptBudget, box.Rejections);

            // The patch it would have been in is the parent's, untouched.
            Assert.Equal(parent.Patch, patch);
        }

        [Fact]
        public void AReleasedReservationLeavesNoRoomTaken()
        {
            var box = new PortVolume(Patches, 5f, DepthMetres, 1UL);
            Phenotype body = Bodies(1, 4UL)[0];

            float height = -20f;
            Assert.True(box.TryReserveFounder(body, ref height, out _));

            box.Release();

            Assert.Equal(0, box.Occupants);
            Assert.False(box.TryTakePlacement(1L, out _));

            height = -20f;
            Assert.True(box.TryReserveFounder(body, ref height, out _));
            box.Commit(7L);

            Assert.Equal(1, box.Occupants);
            Assert.True(box.TryTakePlacement(7L, out Float3 at));

            // Handed over once and then forgotten.
            Assert.False(box.TryTakePlacement(7L, out _));
            Assert.True(at.Y <= 0f);
        }

        // ----------------------------------------------------------------------------- the driver

        /// <summary>
        /// Drives both placers through one world and asserts every answer, counter and RNG state.
        /// </summary>
        private static void Parity(
            WorldShape shape, float dispersal, float relief, int reservations, ulong seed,
            int across = 1)
        {
            float patchWidth = (float)Math.Sqrt(AreaSquareMetres / Patches);
            float tankRadius = shape == WorldShape.Tank
                ? TankGeometry.RadiusFor(AreaSquareMetres)
                : 0f;

            var reference = new RefVolume(
                Patches, patchWidth, DepthMetres, seed, dispersal, across, shape, tankRadius);
            var port = new PortVolume(
                Patches, patchWidth, DepthMetres, seed, dispersal, across, shape, tankRadius);

            if (relief > 0f)
            {
                // A bed is defined about a disc, so its radius is the tank's where there is one
                // and the box's own half-diagonal otherwise — the branch under test is the second
                // clamp inside the draw, not the map.
                float bedRadius = shape == WorldShape.Tank
                    ? tankRadius
                    : 0.5f * (float)Math.Sqrt(
                        reference.LengthMetres * reference.LengthMetres +
                        reference.WidthMetres * reference.WidthMetres);

                var bed = new BedShape(bedRadius, DepthMetres, relief, 2f, 0f, Rng.SeedFor(seed, 99UL));

                reference.Floor = new RefFloor(DepthMetres, bed);
                port.Floor = new PortFloor(DepthMetres, bed);

                Assert.True(port.Floor.HasRelief);
            }
            else
            {
                reference.Floor = new RefFloor(DepthMetres);
                port.Floor = new PortFloor(DepthMetres);

                Assert.False(port.Floor.HasRelief);
            }

            Phenotype[] bodies = Bodies(96, seed);

            // The script: what the harness does next. Its own generator, so the two placers' own
            // streams are the only thing under test.
            var script = new Rng(Rng.SeedFor(seed, 7777UL));

            var live = new List<(long Id, Float3 Position, float Radius)>();
            long nextId = 1L;
            int taken = 0;
            int commits = 0;
            int releases = 0;
            int frees = 0;
            int refusals = 0;

            while (taken < reservations)
            {
                // A free: some of the living died since the last step and are simply not Noted
                // again. The reservations already made for bodies not yet built survive it,
                // which is what Begin promises.
                int deaths = live.Count > 40 ? script.Range(0, live.Count / 4) : 0;

                for (int i = 0; i < deaths; i++)
                {
                    live.RemoveAt(script.Range(live.Count));
                    frees++;
                }

                reference.Begin();
                port.Begin();

                for (int i = 0; i < live.Count; i++)
                {
                    (long id, Float3 position, float radius) = live[i];

                    reference.Note(id, Unity(position), radius);
                    port.Note(id, position, radius);
                }

                Assert.Equal(reference.Occupants, port.Occupants);

                int events = 1 + script.Range(12);

                for (int e = 0; e < events && taken < reservations; e++)
                {
                    Phenotype body = bodies[script.Range(bodies.Length)];

                    float startHeight = script.Range(-DepthMetres - 4f, 1f);
                    float heightReference = startHeight;
                    float heightPort = startHeight;

                    bool founder = live.Count == 0 || script.NextFloat() < 0.25f;

                    bool okReference;
                    bool okPort;
                    int patchReference;
                    int patchPort;

                    if (founder)
                    {
                        okReference = reference.TryReserveFounder(
                            body, ref heightReference, out patchReference);
                        okPort = port.TryReserveFounder(body, ref heightPort, out patchPort);
                    }
                    else
                    {
                        // One parent in twenty is one the placer was never told about, so the
                        // founder fallback inside TryReserveOffspring is driven too.
                        bool known = script.NextFloat() >= 0.05f;

                        long parentId = known
                            ? live[script.Range(live.Count)].Id
                            : -(nextId + 1L);

                        Organism parent = Creature(parentId, script.Range(Patches));

                        okReference = reference.TryReserveOffspring(
                            parent, body, ref heightReference, out patchReference);
                        okPort = port.TryReserveOffspring(
                            parent, body, ref heightPort, out patchPort);
                    }

                    taken++;

                    Assert.Equal(okReference, okPort);
                    Assert.Equal(patchReference, patchPort);
                    Assert.Equal(Bits(heightReference), Bits(heightPort));

                    if (!okReference)
                    {
                        refusals++;

                        reference.Release();
                        port.Release();
                        continue;
                    }

                    // One birth in ten does not happen after all: the reservation is dropped and
                    // the room it held has to come back.
                    if (script.NextFloat() < 0.1f)
                    {
                        releases++;

                        reference.Release();
                        port.Release();
                        continue;
                    }

                    long id = nextId++;

                    reference.Commit(id);
                    port.Commit(id);
                    commits++;

                    Assert.True(reference.TryTakePlacement(id, out UVector3 placedReference));
                    Assert.True(port.TryTakePlacement(id, out Float3 placedPort));

                    Assert.Equal(Bits(placedReference.x), Bits(placedPort.X));
                    Assert.Equal(Bits(placedReference.y), Bits(placedPort.Y));
                    Assert.Equal(Bits(placedReference.z), Bits(placedPort.Z));

                    live.Add((id, placedPort, RefVolume.BoundingRadius(body)));

                    // Every step also asks the two the questions the harness asks them between
                    // births: where a drifted body belongs, how far it swam, and which patch it
                    // is in. TryWrap keeps a counter, so it has to be asked of both.
                    var drifted = new Float3(
                        placedPort.X + script.Range(-30f, 30f),
                        placedPort.Y,
                        placedPort.Z + script.Range(-30f, 30f));

                    bool wrappedReference =
                        reference.TryWrap(Unity(drifted), out UVector3 landedReference);
                    bool wrappedPort = port.TryWrap(drifted, out Float3 landedPort);

                    Assert.Equal(wrappedReference, wrappedPort);
                    Assert.Equal(Bits(landedReference.x), Bits(landedPort.X));
                    Assert.Equal(Bits(landedReference.z), Bits(landedPort.Z));

                    Assert.Equal(
                        Bits(reference.ShortestDistance(Unity(drifted), Unity(placedPort))),
                        Bits(port.ShortestDistance(drifted, placedPort)));

                    Assert.Equal(
                        reference.PatchOf(drifted.X, drifted.Z),
                        port.PatchOf(drifted.X, drifted.Z));
                }
            }

            // The counters, which are the run's own reading of what happened.
            Assert.Equal(reference.Wraps, port.Wraps);
            Assert.Equal(reference.Rejections, port.Rejections);
            Assert.Equal(reference.Refusals, port.Refusals);
            Assert.Equal(reference.Occupants, port.Occupants);
            Assert.Equal(Bits(reference.CellMetres), Bits(port.CellMetres));

            // And the stream itself: same position in the recurrence, not merely the same answers
            // so far. A port that took one draw too few would pass everything above on a world
            // whose rejections happened not to differ, and diverge at the next birth.
            Assert.Equal(StreamState(reference), StreamState(port));

            // The scenario has to have been a world and not a straight line through it.
            Assert.True(commits > reservations / 4, "too few commits: " + commits);
            Assert.True(releases > 0, "no released reservations");
            Assert.True(frees > 0, "no frees");
            Assert.True(reference.Rejections > 0, "no rejections: the world was never crowded");

            // Refusals are allowed to be zero in a roomy world; the crowded case is its own test.
            Assert.True(refusals >= 0);
        }

        // ------------------------------------------------------------------------------ plumbing

        private static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        private static UVector3 Unity(Float3 v) => new UVector3(v.X, v.Y, v.Z);

        /// <summary>The placer's own <see cref="Rng"/>, at the state it has reached.</summary>
        /// <remarks>
        /// Read by reflection rather than through an accessor, because adding one to
        /// <see cref="Rng"/> or to either placer for a test's sake would be a change to the thing
        /// under test.
        /// </remarks>
        private static (ulong Seed, ulong State) StreamState(object placer)
        {
            FieldInfo field = placer.GetType().GetField(
                "_rng", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(field);

            var rng = (Rng)field.GetValue(placer);

            FieldInfo state = typeof(Rng).GetField(
                "_state", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(state);

            return (rng.Seed, (ulong)state.GetValue(rng));
        }

        /// <summary>An organism with an id and a patch — both are <c>internal set</c>.</summary>
        private static Organism Creature(long id, int patch)
        {
            var creature = new Organism();

            typeof(Organism).GetProperty("Id").GetSetMethod(nonPublic: true)
                .Invoke(creature, new object[] { id });
            typeof(Organism).GetProperty("Patch").GetSetMethod(nonPublic: true)
                .Invoke(creature, new object[] { patch });

            return creature;
        }

        /// <summary>A phenotype built by hand, for a geometry test that wants a known radius.</summary>
        private static Phenotype HandBuilt(params (Float3 Position, Float3 HalfExtents)[] parts)
        {
            var phenotype = new Phenotype();

            FieldInfo list = typeof(Phenotype).GetField(
                "_parts", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(list);

            var target = (List<PhenotypePart>)list.GetValue(phenotype);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = new PhenotypePart();

                typeof(PhenotypePart).GetProperty("Index").GetSetMethod(true)
                    .Invoke(part, new object[] { i });
                typeof(PhenotypePart).GetProperty("ParentIndex").GetSetMethod(true)
                    .Invoke(part, new object[] { i == 0 ? -1 : 0 });
                typeof(PhenotypePart).GetProperty("Position").GetSetMethod(true)
                    .Invoke(part, new object[] { parts[i].Position });
                typeof(PhenotypePart).GetProperty("HalfExtents").GetSetMethod(true)
                    .Invoke(part, new object[] { parts[i].HalfExtents });

                target.Add(part);
            }

            return phenotype;
        }

        /// <summary>
        /// Developed bodies, from random genomes — the radii the placer actually sees.
        /// </summary>
        /// <remarks>
        /// One pool, built once and handed to both placers, so a phenotype is never a source of
        /// difference: what is being compared is what the placer does with a radius, not how the
        /// radius was arrived at.
        /// </remarks>
        private static Phenotype[] Bodies(int count, ulong seed)
        {
            var rng = new Rng(Rng.SeedFor(seed, 4242UL));
            var built = new List<Phenotype>(count);

            while (built.Count < count)
            {
                Genome genome = GenomeFactory.Random(rng);

                if (genome.Validate().Count > 0) continue;

                Phenotype body = Developer.Develop(genome);

                if (body.Parts.Count == 0) continue;

                built.Add(body);
            }

            return built.ToArray();
        }

        /// <summary>The repository root, found by walking up from the test assembly.</summary>
        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(
                        directory.FullName, "unity", "Assets", "Evosim", "Sim", "SharedVolume.cs")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "No repository root above " + AppContext.BaseDirectory +
                " carries unity/Assets/Evosim/Sim/SharedVolume.cs.");
        }
    }
}
