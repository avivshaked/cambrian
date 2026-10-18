using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// C2 (card-next-r57.md): on a grid, World.DepositCorpses finds every corpse's cell and instalment across
    /// Core's threads and then makes the additions in the list's order. It must be the serial loop to the bit:
    /// the same stocks, the same field total, the same detritus total, the same corpses left in the same order
    /// with the same joules, and a refusal at the same corpse.
    /// </summary>
    // Parallelism.Threads is a static: every test class that sets it shares this collection, so xUnit runs them one at a time.
    [Collection("Parallelism.Threads")]
    public sealed class CorpseDepositTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly MethodInfo Batched = typeof(World).GetMethod("DepositCorpses", Hidden);
        private static readonly MethodInfo Serial = typeof(World).GetMethod("DepositCorpsesSerially", Hidden);
        private static readonly MethodInfo WriteState = typeof(GridField).GetMethod(
            "WriteState", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo Settled = typeof(Corpse).GetProperty(nameof(Corpse.Settled));
        private static readonly PropertyInfo Joules = typeof(Corpse).GetProperty(nameof(Corpse.Joules));
        private static readonly PropertyInfo Position = typeof(Corpse).GetProperty(nameof(Corpse.Position));

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheBatchedDepositsAreTheSerialLoopsToTheBit(bool tank)
        {
            int was = Parallelism.Threads;
            try
            {
                foreach (int threads in new[] { 1, 3, 16 })
                {
                    (GridField fieldA, List<Corpse> corpsesA) = Build(tank);
                    (GridField fieldB, List<Corpse> corpsesB) = Build(tank);
                    Parallelism.Threads = threads;
                    double totalA = 123.456, totalB = 123.456;
                    int[] cell = null;
                    double[] amount = null;
                    bool[] last = null;
                    int left = 0;

                    for (int step = 0; step < 60; step++)
                    {
                        // Small instalments, a settled rate of its own, D127's floor from step 20, and one
                        // step that asks for everything (step 50, where every corpse leaves).
                        double sinking = step == 50 ? 1.0 : 0.002 + 0.0005 * (step % 7);
                        double settled = 0.0015;
                        double floor = step >= 20 ? 0.3 : 0.0;

                        int before = corpsesA.Count;
                        totalA = RunBatched(corpsesA, fieldA, sinking, settled, floor, totalA, ref cell, ref amount, ref last);
                        totalB = RunSerial(corpsesB, fieldB, sinking, settled, floor, totalB);
                        left += before - corpsesA.Count;

                        string at = $"{(tank ? "tank" : "box")}, {threads} threads, step {step}";
                        Assert.True(BitConverter.DoubleToInt64Bits(totalA) == BitConverter.DoubleToInt64Bits(totalB),
                            $"{at}: total {totalA:R} against {totalB:R}");
                        Assert.Equal(corpsesB.Count, corpsesA.Count);
                        for (int i = 0; i < corpsesA.Count; i++)
                        {
                            Assert.Equal(corpsesB[i].CreatureId, corpsesA[i].CreatureId);
                            if (BitConverter.DoubleToInt64Bits(corpsesA[i].Joules) != BitConverter.DoubleToInt64Bits(corpsesB[i].Joules))
                                Assert.Fail($"{at}: corpse {corpsesA[i].CreatureId} holds {corpsesA[i].Joules:R}, the serial loop's {corpsesB[i].Joules:R}");
                        }

                        Assert.True(State(fieldA).AsSpan().SequenceEqual(State(fieldB)), $"{at}: the grid's stocks or total differ");
                    }

                    Assert.True(left > 0, "no corpse left the list");
                    Assert.True(fieldA.TotalJoules > 0, "nothing was deposited");
                }
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        [Fact]
        public void TheBatchedDepositsRefuseAPointWhereTheSerialLoopDoes()
        {
            int was = Parallelism.Threads;
            try
            {
                Parallelism.Threads = 3;
                (GridField fieldA, List<Corpse> corpsesA) = Build(false);
                (GridField fieldB, List<Corpse> corpsesB) = Build(false);

                // A corpse whose position has stopped being finite, a third of the way down the list.
                int k = corpsesA.Count / 3;
                Position.SetValue(corpsesA[k], new Float3(float.NaN, -3f, 2f));
                Position.SetValue(corpsesB[k], new Float3(float.NaN, -3f, 2f));

                int[] cell = null;
                double[] amount = null;
                bool[] last = null;
                Exception a = Record.Exception(() => RunBatched(corpsesA, fieldA, 0.01, 0.01, 0.0, 0.0, ref cell, ref amount, ref last));
                Exception b = Record.Exception(() => RunSerial(corpsesB, fieldB, 0.01, 0.01, 0.0, 0.0));

                Assert.NotNull(b);
                Assert.NotNull(a);
                Assert.Equal(b.GetType(), a.GetType());
                Assert.Equal(b.Message, a.Message);

                // Every deposit before the refused corpse was made, and none after it.
                Assert.True(State(fieldA).AsSpan().SequenceEqual(State(fieldB)), "the grid differs at the refusal");
                for (int i = 0; i < corpsesA.Count; i++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(corpsesB[i].Joules), BitConverter.DoubleToInt64Bits(corpsesA[i].Joules));
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        private static double RunBatched(List<Corpse> corpses, GridField field, double sinking, double settled, double floor,
            double total, ref int[] cell, ref double[] amount, ref bool[] last)
        {
            object[] args = { corpses, field, sinking, settled, floor, total, cell, amount, last };
            Invoke(Batched, args);
            cell = (int[])args[6];
            amount = (double[])args[7];
            last = (bool[])args[8];
            return (double)args[5];
        }

        private static double RunSerial(List<Corpse> corpses, GridField field, double sinking, double settled, double floor, double total)
        {
            object[] args = { corpses, field, sinking, settled, floor, total };
            Invoke(Serial, args);
            return (double)args[5];
        }

        private static void Invoke(MethodInfo method, object[] args)
        {
            try
            {
                method.Invoke(null, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        private static byte[] State(GridField field)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream))
            {
                WriteState.Invoke(field, new object[] { w });
            }
            return stream.ToArray();
        }

        /// <summary>
        /// A grid and its corpses, the same each call: many to a cell, some on the floor and past the walls,
        /// some settled, some about to leave, one empty, one negative and one NaN.
        /// </summary>
        private static (GridField, List<Corpse>) Build(bool tank)
        {
            const float area = 400f, depth = 12f;
            float radius = TankGeometry.RadiusFor(area);
            GridField field = tank
                ? new GridField(area, 0f, depth, 0f, 0f, 4, 1f, patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: radius)
                : new GridField(area, 0f, depth, 0f, 0f, 4, 1f);
            float length = tank ? 2f * radius : field.LengthMetres;
            float width = tank ? 2f * radius : field.WidthMetres;

            var rng = new Random(59);
            var corpses = new List<Corpse>();
            for (int c = 0; c < 4_000; c++)
            {
                // Two corpses in three on a coarse lattice, so many share a cell; the rest anywhere, a
                // margin past every wall and below the floor.
                float x, y, z;
                if (c % 3 != 2)
                {
                    x = 0.5f + 4f * rng.Next(0, (int)(length / 4f));
                    y = -0.5f - 3f * rng.Next(0, (int)(depth / 3f));
                    z = 0.5f + 4f * rng.Next(0, (int)(width / 4f));
                }
                else
                {
                    x = (float)(-2.0 + (length + 4.0) * rng.NextDouble());
                    y = (float)(1.0 - (depth + 3.0) * rng.NextDouble());
                    z = (float)(-2.0 + (width + 4.0) * rng.NextDouble());
                }

                double joules = c % 50 == 7 ? 5e-7 : 0.5 + 999.5 * rng.NextDouble();
                double first = c % 4 == 0 ? joules * 1.5 : joules;
                int patch = tank ? 0 : Math.Min(3, Math.Max(0, (int)Math.Floor(x / (length / 4f))));
                var corpse = (Corpse)Activator.CreateInstance(typeof(Corpse), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { (long)c, new Float3(x, y, z), patch, joules, first }, null);
                if (rng.Next(3) == 0) Settled.SetValue(corpse, true);
                corpses.Add(corpse);
            }

            Joules.SetValue(corpses[11], 0d);
            Joules.SetValue(corpses[12], -4d);
            Joules.SetValue(corpses[13], double.NaN);
            return (field, corpses);
        }
    }
}
