using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Mutation of D146's one brain, on a developed body: weights and biases step, links and
    /// hidden units are added and removed. Outputs are never removed, and ports are the body's.
    /// </summary>
    /// <remarks>
    /// Run after development, because what a link may come from is the body's ports. Removal is
    /// rarer than addition (<see cref="CentralBrainOptions"/>), so that a brain grows where growth
    /// pays and drift does not shrink it, which is the fault the old brain's rates had: adding a
    /// neuron at 0.05 a birth against removing each at 0.04 held it near one neuron a part.
    /// </remarks>
    public static class CentralMutation
    {
        /// <summary>What one call did, for the generation rows.</summary>
        public struct Report
        {
            public int WeightSteps;
            public int BiasSteps;
            public int LinksAdded;
            public int UnitsAdded;
            public int LinksRemoved;
            public int UnitsRemoved;
        }

        public static Report Mutate(
            CentralBrain brain, Phenotype body, SensorChannel[] pool, Rng rng, CentralBrainOptions o)
        {
            if (brain == null) throw new ArgumentNullException(nameof(brain));
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (o == null) throw new ArgumentNullException(nameof(o));

            var report = new Report();

            foreach (BrainLink l in brain.Links)
            {
                if (!rng.Chance(o.WeightChance)) continue;
                l.Weight += rng.Gaussian(0f, o.WeightStdDev);
                report.WeightSteps++;
            }

            foreach (BrainUnit u in brain.Outputs) StepBias(u);
            foreach (BrainUnit u in brain.Hidden) StepBias(u);

            void StepBias(BrainUnit u)
            {
                if (!rng.Chance(o.BiasChance)) return;
                u.Bias += rng.Gaussian(0f, o.BiasStdDev);
                report.BiasSteps++;
            }

            if (rng.Chance(o.AddLinkChance) && AddLink(brain, body, pool, rng, o)) report.LinksAdded++;

            if (rng.Chance(o.AddUnitChance) && brain.Hidden.Count < o.MaxHiddenUnits &&
                brain.Links.Count > 0)
            {
                Split(brain, brain.Links[rng.Range(brain.Links.Count)]);
                report.UnitsAdded++;
            }

            if (rng.Chance(o.RemoveLinkChance) && brain.Links.Count > 0)
            {
                brain.Links.RemoveAt(rng.Range(brain.Links.Count));
                report.LinksRemoved++;
            }

            if (rng.Chance(o.RemoveUnitChance) && brain.Hidden.Count > 0)
            {
                ulong gone = brain.Hidden[rng.Range(brain.Hidden.Count)].Id;
                brain.Hidden.RemoveAll(u => u.Id == gone);
                brain.Links.RemoveAll(l => l.From == gone || l.To == gone);
                report.UnitsRemoved++;
            }

            return report;
        }

        /// <summary>
        /// One new link: the source drawn from every port of the body and every unit, the target
        /// from every unit. A pair already linked is left alone and the draw is spent.
        /// </summary>
        public static bool AddLink(
            CentralBrain brain, Phenotype body, SensorChannel[] pool, Rng rng, CentralBrainOptions o)
        {
            List<BrainPort> ports = CentralWiring.Ports(body, pool);
            int units = brain.Outputs.Count + brain.Hidden.Count;
            if (units == 0) return false;

            int s = rng.Range(ports.Count + units);
            ulong from = s < ports.Count ? ports[s].Id : UnitAt(brain, s - ports.Count);
            ulong to = UnitAt(brain, rng.Range(units));
            float weight = rng.Gaussian(0f, o.NewLinkStdDev);

            foreach (BrainLink l in brain.Links)
            {
                if (l.From == from && l.To == to) return false;
            }

            brain.Links.Add(new BrainLink { From = from, To = to, Weight = weight });
            return true;
        }

        /// <summary>
        /// Splits <paramref name="link"/> with a new hidden unit, NEAT's add-node: weight 1 in and
        /// the old weight out, so the brain behaves nearly as before until the unit is tuned
        /// (nearly, because the unit's tanh is not the identity).
        /// </summary>
        public static ulong Split(CentralBrain brain, BrainLink link)
        {
            var existing = new HashSet<ulong>();
            foreach (BrainUnit u in brain.Hidden) existing.Add(u.Id);
            foreach (BrainUnit u in brain.Outputs) existing.Add(u.Id);

            int k = 0;
            ulong id = BrainIds.Hidden(link.From, link.To, k);
            while (existing.Contains(id)) id = BrainIds.Hidden(link.From, link.To, ++k);

            brain.Links.Remove(link);
            brain.Hidden.Add(new BrainUnit { Id = id, Bias = 0f });
            brain.Links.Add(new BrainLink { From = link.From, To = id, Weight = 1f });
            brain.Links.Add(new BrainLink { From = id, To = link.To, Weight = link.Weight });
            return id;
        }

        private static ulong UnitAt(CentralBrain brain, int i) =>
            i < brain.Outputs.Count ? brain.Outputs[i].Id : brain.Hidden[i - brain.Outputs.Count].Id;
    }
}
