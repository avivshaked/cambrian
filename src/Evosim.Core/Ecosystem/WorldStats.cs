using System;
using System.Collections.Generic;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>
    /// One sample of a world — DESIGN.md §5A.6b, and one row of <c>stats.jsonl</c> (§9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question these exist to answer is not "how is the population doing" but "is this
    /// world running itself, or are we running it".</b> Those look identical in a population
    /// curve: a world topped up by the floor and a world sustaining itself both show a stable
    /// count, steady births and steady deaths. Only <see cref="MinDepth"/> tells them apart.
    /// </para>
    /// <para>
    /// The distribution is reported rather than a mean, because a takeover and a healthy world
    /// have the same mean. A high <see cref="MaxDepth"/> against a <see cref="MedianDepth"/> near
    /// zero is one lucky lineage in a world of floor spawns; min and max close together is a
    /// bottleneck; a wide spread is deep lineages coexisting with young ones.
    /// </para>
    /// </remarks>
    public readonly struct WorldSample
    {
        public double ElapsedSeconds { get; }
        public int Population { get; }

        /// <summary>
        /// Generation depth across the living — §5A.6b. <see cref="MinDepth"/> above zero means
        /// no living creature is a floor spawn, which is the definition of self-sustaining.
        /// </summary>
        public int MinDepth { get; }
        public float MeanDepth { get; }
        public int MedianDepth { get; }
        public int MaxDepth { get; }

        /// <summary>Mean age of the living, seconds.</summary>
        public float MeanAge { get; }

        /// <summary>Mean age at death of everything that died since the last sample, seconds.</summary>
        /// <remarks>
        /// Paired with depth because depth alone can be fooled: a world where creatures reproduce
        /// instantly and die instantly posts healthy depth and is broken. NaN when nothing died.
        /// </remarks>
        public float MeanAgeAtDeath { get; }

        /// <summary>Mean seconds of reserve across the living — what §4.4's energy channel reads.</summary>
        public float MeanSecondsOfReserve { get; }

        public long FloorSpawns { get; }
        public long Births { get; }
        public long Deaths { get; }

        /// <summary>Simulated seconds since the floor last intervened — D021.</summary>
        public double SecondsSinceFloorFired { get; }

        /// <summary>§5A.2's audit: sunlight in, metabolism out.</summary>
        public double EnergyIn { get; }
        public double EnergyOut { get; }

        /// <summary>Mean parts per living creature — is the world growing bodies at all?</summary>
        public float MeanParts { get; }

        public WorldSample(
            double elapsedSeconds, int population,
            int minDepth, float meanDepth, int medianDepth, int maxDepth,
            float meanAge, float meanAgeAtDeath, float meanSecondsOfReserve,
            long floorSpawns, long births, long deaths, double secondsSinceFloorFired,
            double energyIn, double energyOut, float meanParts)
        {
            ElapsedSeconds = elapsedSeconds;
            Population = population;
            MinDepth = minDepth;
            MeanDepth = meanDepth;
            MedianDepth = medianDepth;
            MaxDepth = maxDepth;
            MeanAge = meanAge;
            MeanAgeAtDeath = meanAgeAtDeath;
            MeanSecondsOfReserve = meanSecondsOfReserve;
            FloorSpawns = floorSpawns;
            Births = births;
            Deaths = deaths;
            SecondsSinceFloorFired = secondsSinceFloorFired;
            EnergyIn = energyIn;
            EnergyOut = energyOut;
            MeanParts = meanParts;
        }

        /// <summary>
        /// True when the world has not needed the floor for <paramref name="quietSeconds"/> —
        /// nobody is being handed life by us right now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This used to be <c>MinDepth &gt; 0</c>, which is a stronger statement and an
        /// unreachable one.</b> Nothing dies of age (§5A.6 kills only at zero energy), so a
        /// founder whose income covers its upkeep never dies — and a handful of immortal
        /// generation-zero photosynthesisers pin the minimum at zero forever, in worlds that
        /// stopped needing the floor thousands of seconds earlier and are visibly running
        /// themselves at median depth 78 (logbook/0011). The old test called those worlds
        /// floor-fed. It was measuring immortality, not dependence.
        /// </para>
        /// <para>
        /// A window is required rather than defaulted because there is no natural one: it has to
        /// be long against the generation time of whatever is living there, and that is a property
        /// of the run and not of this struct. <see cref="MinDepth"/> is still reported and is
        /// still the stronger claim when it does rise — it will, once anything can die of
        /// something other than starvation.
        /// </para>
        /// </remarks>
        public bool IsSelfSustaining(double quietSeconds) =>
            Population > 0 && SecondsSinceFloorFired >= quietSeconds;

        /// <summary>One line of <c>stats.jsonl</c>. Compact: one row must be one line (§9).</summary>
        public string ToJson()
        {
            var w = new Json.Writer(indent: false);
            w.BeginObject()
                .Field("t", ElapsedSeconds)
                .Field("pop", Population)
                .Field("depthMin", MinDepth)
                .Field("depthMean", MeanDepth)
                .Field("depthMedian", MedianDepth)
                .Field("depthMax", MaxDepth)
                .Field("age", MeanAge)
                .Field("ageAtDeath", float.IsNaN(MeanAgeAtDeath) ? 0f : MeanAgeAtDeath)
                .Field("reserve", MeanSecondsOfReserve)
                .Field("floorSpawns", FloorSpawns)
                .Field("births", Births)
                .Field("deaths", Deaths)
                .Field("sinceFloor", SecondsSinceFloorFired)
                .Field("energyIn", EnergyIn)
                .Field("energyOut", EnergyOut)
                .Field("parts", MeanParts)
                .EndObject();

            return w.ToString();
        }

        public override string ToString() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "t={0:0} pop={1} depth {2}/{3:0.#}/{4} age={5:0.#}s floor quiet {6:0}s",
                ElapsedSeconds, Population, MinDepth, MeanDepth, MaxDepth, MeanAge,
                SecondsSinceFloorFired);
    }

    /// <summary>Takes samples of a <see cref="World"/> — §5A.6b.</summary>
    public static class WorldStats
    {
        /// <summary>
        /// Samples the world, consuming the dead accumulated since the last call.
        /// </summary>
        /// <remarks>
        /// The dead are consumed rather than read, because age-at-death is only meaningful over
        /// an interval — a running mean over every creature that ever lived would be dominated by
        /// the opening minutes forever, and would stop responding to the world long before a run
        /// ended.
        /// </remarks>
        public static WorldSample Sample(World world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            IReadOnlyList<Organism> living = world.Living;
            int n = living.Count;

            int minDepth = 0, maxDepth = 0, medianDepth = 0;
            float meanDepth = 0f, meanAge = 0f, meanReserve = 0f, meanParts = 0f;

            if (n > 0)
            {
                var depths = new int[n];
                double depthSum = 0, ageSum = 0, reserveSum = 0, partSum = 0;

                for (int i = 0; i < n; i++)
                {
                    Organism creature = living[i];
                    depths[i] = creature.GenerationDepth;

                    depthSum += creature.GenerationDepth;
                    ageSum += creature.Age;
                    partSum += creature.Phenotype.PartCount;

                    // Infinite reserve means zero standing cost, which CellType forbids — but a
                    // sum containing an infinity is an unrecoverable NaN for the rest of the run,
                    // so it is skipped rather than trusted.
                    float reserve = creature.SecondsOfReserve;
                    if (!float.IsInfinity(reserve)) reserveSum += reserve;
                }

                Array.Sort(depths);

                minDepth = depths[0];
                maxDepth = depths[n - 1];
                medianDepth = depths[n / 2];
                meanDepth = (float)(depthSum / n);
                meanAge = (float)(ageSum / n);
                meanReserve = (float)(reserveSum / n);
                meanParts = (float)(partSum / n);
            }

            List<Organism> dead = world.TakeDead();
            float meanAgeAtDeath = float.NaN;

            if (dead.Count > 0)
            {
                double sum = 0;
                for (int i = 0; i < dead.Count; i++) sum += dead[i].Age;
                meanAgeAtDeath = (float)(sum / dead.Count);
            }

            return new WorldSample(
                world.ElapsedSeconds, n,
                minDepth, meanDepth, medianDepth, maxDepth,
                meanAge, meanAgeAtDeath, meanReserve,
                world.FloorSpawns, world.Births, world.Deaths, world.SecondsSinceFloorFired,
                world.EnergyIn, world.EnergyOut, meanParts);
        }
    }
}
