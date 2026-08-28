using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Variation operators — DESIGN.md §4.5.</summary>
    public class MutationTests
    {
        private readonly ITestOutputHelper _output;

        public MutationTests(ITestOutputHelper output) => _output = output;

        private static Genome Parent(ulong seed) => GenomeFactory.RandomViable(
            new Rng(seed), RandomGenomeOptions.Default, DevelopmentLimits.Default, minParts: 3);

        [Fact]
        public void EveryMutantIsValidAndDevelops()
        {
            // Mutation is where invariants go to die: a joint type changed without its limit
            // array, a link turned into a stomach while keeping its hinge, an edge left pointing
            // at a removed node. Each operator has to repair what it disturbs.
            var rates = new MutationRates();

            for (ulong seed = 1; seed <= 400; seed++)
            {
                Genome child = Mutator.Mutate(Parent(seed % 40 + 1), new Rng(seed), rates);

                Assert.Empty(child.Validate());
                Developer.Develop(child, DevelopmentLimits.Default);
            }
        }

        [Fact]
        public void HeavyMutationStillProducesValidGenomes()
        {
            // Every operator firing almost every time. Not a realistic setting — it is how you
            // reach the interactions between operators that a normal rate would take a million
            // births to find once.
            var violent = new MutationRates
            {
                ScalarChance = 0.9f, ScalarStdDev = 0.8f,
                AddNodeChance = 0.9f,
                AddEdgeChance = 0.9f, RemoveEdgeChance = 0.9f,
                AddNeuronChance = 0.9f, RemoveNeuronChance = 0.9f,
                RewireInputChance = 0.9f, NeuronOpChance = 0.9f,
                JointTypeChance = 0.9f, FlagChance = 0.9f,
                RecursiveLimitChance = 0.9f, CellTypeChance = 0.9f,
                BroodSizeChance = 0.9f, EndowmentChance = 0.9f,
            };

            Genome g = Parent(1);
            for (ulong step = 1; step <= 500; step++)
            {
                g = Mutator.Mutate(g, new Rng(step), violent);
                Assert.Empty(g.Validate());
            }

            _output.WriteLine($"after 500 violent mutations: {g.Nodes.Count} nodes");
        }

        [Fact]
        public void AnOffspringIsFullyDeterminedByItsParentAndSeed()
        {
            // The property §9's storage rests on. If this holds, a birth records as a parent
            // reference plus a seed — a couple of dozen bytes — instead of a ~5 KB genome. At
            // 40,000 births an hour that is the difference between 200 MB and a few MB.
            for (ulong seed = 1; seed <= 100; seed++)
            {
                Genome parent = Parent(seed % 20 + 1);

                string a = GenomeJson.Write(Mutator.Mutate(parent, new Rng(seed)));
                string b = GenomeJson.Write(Mutator.Mutate(parent, new Rng(seed)));

                Assert.Equal(a, b);
            }
        }

        [Fact]
        public void DifferentSeedsGiveDifferentOffspring()
        {
            Genome parent = Parent(1);
            var seen = new HashSet<string>();

            for (ulong seed = 1; seed <= 50; seed++)
            {
                seen.Add(GenomeJson.Write(Mutator.Mutate(parent, new Rng(seed))));
            }

            _output.WriteLine($"{seen.Count} distinct offspring from 50 seeds");
            Assert.True(seen.Count > 40, "mutation is barely varying anything");
        }

        [Fact]
        public void TheParentIsNeverModified()
        {
            // Mutate returns a copy. A parent quietly mutated in place would change while it is
            // still alive and still reproducing, and every earlier record of it would be wrong.
            Genome parent = Parent(3);
            string before = GenomeJson.Write(parent);

            for (ulong seed = 1; seed <= 50; seed++) Mutator.Mutate(parent, new Rng(seed));

            Assert.Equal(before, GenomeJson.Write(parent));
        }

        [Fact]
        public void OnlyLinksEverCarryAJointAfterMutation()
        {
            // The §5A.1 invariant, checked after the operator most able to break it: cell-type
            // mutation turning a link into something that feeds.
            var rates = new MutationRates { CellTypeChance = 0.9f, JointTypeChance = 0.9f };

            for (ulong seed = 1; seed <= 300; seed++)
            {
                Genome child = Mutator.Mutate(Parent(seed % 30 + 1), new Rng(seed), rates);

                foreach (MorphNode node in child.Nodes)
                {
                    if (node.JointType.DofCount() == 0) continue;
                    Assert.Equal(CellTypeIds.Link, node.CellTypeId);
                    Assert.True(node.Power > 0f, "a joint with no capacity cannot actuate");
                }
            }
        }

        [Fact]
        public void CellTypeMutationIsRareAtTheDefaultRate()
        {
            // "Very scarce" is the requirement (§5A.3). A type that flips often is not a trait,
            // it is noise, and no lineage can specialise around a body whose parts keep changing
            // what they do. Measured rather than asserted from the constant, because what
            // matters is the rate per birth, not per node.
            int changed = 0;
            const int births = 1000;

            for (ulong seed = 1; seed <= births; seed++)
            {
                Genome parent = Parent(seed % 25 + 1);
                Genome child = Mutator.Mutate(parent, new Rng(seed));

                for (int i = 0; i < Math.Min(parent.Nodes.Count, child.Nodes.Count); i++)
                {
                    if (parent.Nodes[i].CellTypeId != child.Nodes[i].CellTypeId) { changed++; break; }
                }
            }

            _output.WriteLine($"{changed}/{births} births changed a cell type " +
                              $"({changed / (float)births:P1})");

            Assert.True(changed > 0, "cell type never changes — the predator valley has no bridge");
            Assert.True(changed < births / 5, "cell type changes too often to be a trait");
        }

        [Fact]
        public void ANodeShrinkingToNothingRepointsEveryEdge()
        {
            // Extinction renumbers everything after the vanished node. Getting this wrong
            // produces a genome that still *validates* — the indices stay in range — while
            // pointing at the wrong parts, so validity alone cannot catch it.
            //
            // Forced by setting the extinction threshold above every node's size, so the whole
            // genome is at the brink and the repair path runs on every birth.
            var rates = new MutationRates
            {
                NodeExtinctionHalfExtent = 10f,
                AddNodeChance = 0f, ScalarChance = 0f,
                AddEdgeChance = 0f, RemoveEdgeChance = 0f, CellTypeChance = 0f,
                JointTypeChance = 0f, FlagChance = 0f,
            };

            for (ulong seed = 1; seed <= 100; seed++)
            {
                Genome parent = Parent(seed % 20 + 1);
                if (parent.Nodes.Count < 3) continue;

                Genome child = Mutator.Mutate(parent, new Rng(seed), rates);

                // Everything but the root is below the threshold, so only the root survives.
                Assert.Single(child.Nodes);

                foreach (MorphNode node in child.Nodes)
                {
                    foreach (MorphEdge edge in node.Edges)
                    {
                        Assert.InRange(edge.Child, 0, child.Nodes.Count - 1);
                    }
                }

                Assert.InRange(child.RootIndex, 0, child.Nodes.Count - 1);
            }
        }

        [Fact]
        public void ReproductionTraitsStayLegal()
        {
            var rates = new MutationRates
            {
                BroodSizeChance = 1f, EndowmentChance = 1f, ScalarStdDev = 2f, MaxBroodSize = 8,
            };

            Genome g = Parent(1);
            for (ulong step = 1; step <= 500; step++)
            {
                g = Mutator.Mutate(g, new Rng(step), rates);

                Assert.InRange(g.Reproduction.BroodSize, 1, 8);
                Assert.True(g.Reproduction.OffspringEndowment > 0f);
            }
        }

        [Fact]
        public void BroodSizeDriftsBothWays()
        {
            // A random walk that only ever went up would make brood size a ratchet rather than
            // a strategy, and the r/K axis (§5A.6) would collapse to one end of itself.
            var rates = new MutationRates { BroodSizeChance = 1f, MaxBroodSize = 64 };

            int up = 0, down = 0;
            for (ulong seed = 1; seed <= 400; seed++)
            {
                Genome parent = Parent(seed % 20 + 1);
                parent.Reproduction = new ReproductionTraits { BroodSize = 8, OffspringEndowment = 100f };

                int after = Mutator.Mutate(parent, new Rng(seed), rates).Reproduction.BroodSize;
                if (after > 8) up++; else if (after < 8) down++;
            }

            _output.WriteLine($"up {up}, down {down}");
            Assert.True(up > 100 && down > 100, "brood size is not drifting symmetrically");
        }

        [Fact]
        public void EveryMutationRateReachesTheConfigHash()
        {
            // Same reflection guard as the other config tests: a rate added without being folded
            // into the hash makes two materially different runs indistinguishable afterwards.
            string baseline = new RunConfig().Hash();
            var missed = new List<string>();

            foreach (PropertyInfo p in typeof(MutationRates).GetProperties())
            {
                if (!p.CanWrite) continue;

                var config = new RunConfig { Mutation = new MutationRates() };
                if (p.PropertyType == typeof(float))
                    p.SetValue(config.Mutation, (float)p.GetValue(config.Mutation) + 0.37f);
                else if (p.PropertyType == typeof(int))
                    p.SetValue(config.Mutation, (int)p.GetValue(config.Mutation) + 3);
                else continue;

                if (config.Hash() == baseline) missed.Add(p.Name);
            }

            _output.WriteLine(missed.Count == 0
                ? "every mutation rate reaches the hash"
                : "missing from Hash(): " + string.Join(", ", missed));

            Assert.Empty(missed);
        }

        [Fact]
        public void EveryMutationRateSurvivesTheRoundTrip()
        {
            foreach (PropertyInfo p in typeof(MutationRates).GetProperties())
            {
                if (!p.CanWrite) continue;

                var config = new RunConfig { Mutation = new MutationRates() };
                if (p.PropertyType == typeof(float))
                    p.SetValue(config.Mutation, (float)p.GetValue(config.Mutation) + 0.37f);
                else if (p.PropertyType == typeof(int))
                    p.SetValue(config.Mutation, (int)p.GetValue(config.Mutation) + 3);
                else continue;

                RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config));
                Assert.True(config.Hash() == back.Hash(), $"{p.Name} did not survive the round trip");
            }
        }

        [Fact]
        public void RewiringActuallyRepointsInputs()
        {
            // RewireInputChance was declared, reached the config hash, survived a JSON round
            // trip, and was set to 0.9 by the violent-mutation test above — and nothing read it
            // (logbook/0019). Neuron topology inside a node was frozen at whatever the founder
            // drew, for every generation of every lineage that has ever run here.
            //
            // The two reflection tests could not catch it. They prove a tunable reaches the hash
            // and the file, which is a statement about serialization, not about whether any code
            // consults the value. This one asserts the effect instead: turn the knob off and the
            // wiring must be identical; turn it up and it must not be.
            Genome parent = Parent(7);

            var off = new MutationRates { RewireInputChance = 0f };
            var on = new MutationRates { RewireInputChance = 0.9f };

            Assert.Equal(Wiring(parent), Wiring(Mutator.Mutate(parent, new Rng(11), off)));
            Assert.NotEqual(Wiring(parent), Wiring(Mutator.Mutate(parent, new Rng(11), on)));
        }

        [Fact]
        public void RewiringReachesEveryInputKindAndOnlyImplementedChannels()
        {
            // Two properties at once, because they fail in opposite directions. A rewire that
            // cannot reach ParentNode or GlobalBrain leaves whole classes of connection
            // unevolvable and looks exactly like a search that is merely slow. A rewire that
            // draws channels uniformly from the enum spends most of its sensory mutations on
            // channels wired to a constant zero, which looks exactly like a sensor that does
            // not help.
            var rates = new MutationRates { RewireInputChance = 0.9f };
            var kinds = new HashSet<NeuronInputKind>();
            var channels = new HashSet<SensorChannel>();

            Genome g = Parent(3);
            for (ulong step = 1; step <= 300; step++)
            {
                g = Mutator.Mutate(g, new Rng(step), rates);

                foreach (MorphNode node in g.Nodes)
                {
                    foreach (NeuronDef neuron in node.Neurons)
                    {
                        foreach (NeuronInput input in neuron.Inputs)
                        {
                            kinds.Add(input.Kind);
                            if (input.Kind == NeuronInputKind.Sensor) channels.Add(input.Channel);
                        }
                    }
                }
            }

            foreach (NeuronInputKind kind in Enum.GetValues(typeof(NeuronInputKind)))
            {
                Assert.Contains(kind, kinds);
            }

            Assert.NotEmpty(channels);
            Assert.All(channels, c => Assert.True(
                c.IsImplemented(),
                $"mutation introduced {c}, which nothing reads — it would evaluate to zero " +
                "forever and be indistinguishable from a dead input"));

            _output.WriteLine($"kinds reached: {kinds.Count}; channels used: {string.Join(", ", channels)}");
        }

        [Fact]
        public void SensorIndicesStayInRangeForTheirChannel()
        {
            // Depth has one value; a reference to Depth[6] can never become meaningful however
            // the body changes. It reads zero rather than faulting, which is precisely why
            // nothing would report it.
            var rates = new MutationRates { RewireInputChance = 0.9f };

            Genome g = Parent(5);
            for (ulong step = 1; step <= 200; step++)
            {
                g = Mutator.Mutate(g, new Rng(step), rates);

                foreach (MorphNode node in g.Nodes)
                {
                    foreach (NeuronDef neuron in node.Neurons)
                    {
                        foreach (NeuronInput input in neuron.Inputs)
                        {
                            if (input.Kind != NeuronInputKind.Sensor) continue;

                            Assert.InRange(input.Index, 0, input.Channel.IndexCount() - 1);
                        }
                    }
                }
            }
        }

        /// <summary>Every input reference in the genome, ignoring weights and constants.</summary>
        private static string Wiring(Genome g)
        {
            var parts = new List<string>();

            foreach (MorphNode node in g.Nodes)
            {
                foreach (NeuronDef neuron in node.Neurons)
                {
                    foreach (NeuronInput input in neuron.Inputs)
                    {
                        parts.Add($"{input.Kind}:{input.Index}:{input.Channel}");
                    }
                }
            }

            return string.Join("|", parts);
        }

        [Fact]
        public void GraftingIsAbsentRatherThanDisabled()
        {
            // §4.5 lists grafting as the design's only recombination and §5A.6 makes
            // reproduction asexual, so there is no second parent to graft from. Recorded as a
            // test because a disabled operator reads as a decision that was made, and this one
            // is an open question waiting on review round 3.
            Assert.DoesNotContain(
                typeof(MutationRates).GetProperties(),
                p => p.Name.IndexOf("graft", StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.DoesNotContain(
                typeof(Mutator).GetMethods(),
                m => m.Name.IndexOf("graft", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// A parent whose nodes may all take a joint and none of which has one, so every joint
        /// seen in a mutant was created by <c>ChangeJointType</c> rather than inherited.
        /// </summary>
        private static Genome Jointless(ulong seed)
        {
            Genome g = Parent(seed);

            foreach (MorphNode node in g.Nodes)
            {
                node.CellTypeId = CellTypeIds.Link;
                node.JointType = JointType.Fixed;
                node.JointLimits = Array.Empty<Float2>();
                node.Power = 0f;
            }

            return g;
        }

        [Fact]
        public void AMutationBornJointIsNoStrongerThanAFounderIsAllowedToBe()
        {
            // The branch that gives a jointless node a joint is the only path by which an
            // established lineage can invent a muscle, and it used to draw rng.Range(5f, 120f) —
            // the ceiling RandomGenomeOptions retired in logbook/0017 — while founders drew
            // 5..20. A mutant muscle therefore arrived a mean six times stronger than a
            // founder's, and LinkCell bills for capacity whether or not it moves.
            //
            // Asserted against the options rather than the literals, because two hardcoded
            // bounds that must agree are exactly how this drifted apart in the first place.
            // ScalarChance is zeroed so the draw is observed rather than the draw plus a
            // perturbation — Perturb is relative (sigma = 0.15 x value), so one step carries a
            // 20 N·m joint past 24 and would make a bound assertion meaningless.
            var options = new RandomGenomeOptions { MinLinkPower = 5f, MaxLinkPower = 20f };
            var rates = new MutationRates
            {
                JointTypeChance = 1f, CellTypeChance = 0f, ScalarChance = 0f,
            };

            var powers = new List<float>();

            for (ulong seed = 0; seed < 400; seed++)
            {
                Genome child = Mutator.Mutate(
                    Jointless(seed), new Rng(seed + 90000), rates,
                    CellTypeRegistry.Standard, options);

                foreach (MorphNode node in child.Nodes)
                {
                    if (node.JointType.DofCount() == 0) continue;
                    powers.Add(node.Power);
                    Assert.InRange(node.Power, options.MinLinkPower, options.MaxLinkPower);
                }
            }

            Assert.NotEmpty(powers);
            _output.WriteLine(
                $"{powers.Count} freshly drawn joints over 400 mutants, " +
                $"max {Max(powers):0.##} N·m against a ceiling of {options.MaxLinkPower:0.##}");
        }

        [Fact]
        public void TheRunsConfiguredPowerCeilingReachesTheMutator()
        {
            // "Identical numbers across a configuration change mean the change was not applied."
            // A ceiling World never forwards is a ceiling that does nothing, which is the same
            // class of fault as the hardcode it replaced — so this checks the value travels, not
            // merely that some bound is honoured.
            var tight = new RandomGenomeOptions { MinLinkPower = 1f, MaxLinkPower = 2f };
            var loose = new RandomGenomeOptions { MinLinkPower = 90f, MaxLinkPower = 100f };
            var rates = new MutationRates
            {
                JointTypeChance = 1f, CellTypeChance = 0f, ScalarChance = 0f,
            };

            float tightMax = 0f, looseMin = float.MaxValue;

            for (ulong seed = 0; seed < 200; seed++)
            {
                foreach (MorphNode n in Mutator.Mutate(
                    Jointless(seed), new Rng(seed + 70000), rates,
                    CellTypeRegistry.Standard, tight).Nodes)
                {
                    if (n.JointType.DofCount() > 0) tightMax = Math.Max(tightMax, n.Power);
                }

                foreach (MorphNode n in Mutator.Mutate(
                    Jointless(seed), new Rng(seed + 70000), rates,
                    CellTypeRegistry.Standard, loose).Nodes)
                {
                    if (n.JointType.DofCount() > 0) looseMin = Math.Min(looseMin, n.Power);
                }
            }

            _output.WriteLine(
                $"ceiling 2 produced max {tightMax:0.##}; floor 90 produced min {looseMin:0.##}");
            Assert.True(tightMax <= 2f, $"a ceiling of 2 N·m produced {tightMax} — it did not reach the mutator");
            Assert.True(looseMin >= 90f, $"a floor of 90 N·m produced {looseMin} — it did not reach the mutator");
        }

        private static float Max(List<float> xs)
        {
            float m = 0f;
            for (int i = 0; i < xs.Count; i++) m = Math.Max(m, xs[i]);
            return m;
        }
    }
}
