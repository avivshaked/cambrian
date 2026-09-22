using System;
using System.Collections.Generic;
using System.Globalization;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm
{
    /// <summary>
    /// One sample: the markdown row, <c>stats.jsonl</c>, <c>positions.jsonl</c>,
    /// <c>poses.jsonl</c>, <c>absorptive.jsonl</c> and the window's lineage rows — work package J,
    /// the port of <c>EvolutionRun.Row</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>All of it is built here, from the same locals, deliberately.</b> A stats file written
    /// from a second pass over the population would let the two drift, and a statistics file that
    /// disagrees with the report is worse than none.
    /// </para>
    /// <para>
    /// <b>The one group of columns whose name changed, and why it had to.</b> The farm's
    /// <c>contacts</c>, <c>pairs/body</c>, <c>pairs jnt %</c> and <c>stuck %</c> count what PhysX
    /// reported: contact <i>manifolds between colliders</i>, so a four-part animal lying across
    /// another reports a dozen and a body folded on itself reports twenty of its own. This engine
    /// counts overlapping <i>bounding spheres between creatures</i>, one per pair, and a body
    /// cannot overlap itself at all. The two are a different census, so they are
    /// <c>overlaps</c>, <c>ovl/body</c>, <c>ovl jnt %</c> and <c>ovl held %</c> here and
    /// <c>overlapPairs</c>, <c>overlapPairsJointed</c>, <c>overlapPairsHeld</c> and
    /// <c>overlapBodies</c> in the statistics file. Every other column and every other field
    /// keeps its name, its place and its formatting to the character — <c>analyse-arm.ps1</c>
    /// reads by name and will print <c>?</c> for the four that moved, which is what it is for.
    /// </para>
    /// <para>
    /// <b><c>poses.jsonl</c> is new</b>, and it is a recording rather than a world rule: one row
    /// per sample at <c>positions.jsonl</c>'s own cadence, carrying each body's root position,
    /// root rotation and joint coordinates, so that a picture of a recorded second can pose the
    /// bodies instead of standing every one of them upright in the developer's frame. It moves no
    /// hash and refuses no config.
    /// </para>
    /// </remarks>
    public sealed class Sampler
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The ids-ever-seen sets: what separates a lineage from a standing crop. Instance fields
        // rather than statics — EvolutionRun's were static and had to be reset by hand between
        // runs in one process, which is a fault waiting for a second run in one process.
        private readonly HashSet<long> _everAbsorptive = new HashSet<long>();
        private readonly HashSet<long> _everJointed = new HashSet<long>();
        private readonly HashSet<long> _everPhotosynthetic = new HashSet<long>();
        private readonly HashSet<long> _everBuoyant = new HashSet<long>();

        // The window's baselines, one per running total the table prints per window.
        private long _lastFloorSpawns;
        private long _lastUptakeLimited;
        private long _lastPhotosyntheticSteps;
        private double _lastBurntTotal;
        private double _lastRemineralisedTotal;
        private double _lastDetritusDeposited;
        private double _lastDetritusTaken;
        private double _lastDetritusExuded;
        private double _lastDetritusReturned;
        private double _lastMatterInfluxed;
        private double _lastMatterBuried;
        private long _lastWraps;
        private long _lastCrowded;
        private long _lastOverlapPairs;
        private long _lastOverlapPairsJointed;
        private long _lastOverlapPairsHeld;
        private long _lastBedOrGlassBodies;
        private long _lastContactSteps;

        // D106 item 2's three windows. Baselines like the rest: the table prints what happened
        // since the last sample and stats.jsonl carries the running total beside it.
        private long _lastModuleAdds;
        private long _lastModuleDrops;
        private long _lastModuleAddsRefused;
        private long _lastModuleAddsRefusedForShape;
        private long _lastModuleAddsRefusedForReserve;

        // D106 items 1, 3 and 4's four windows, on the same rule.
        private long _lastPartsKilled;
        private long _lastBodiesEaten;
        private long _lastCorpsesEaten;
        private double _lastHealingJoules;

        private readonly List<AbsorptiveSample> _absorptiveRows = new List<AbsorptiveSample>();

        private long[] _positionIds = Array.Empty<long>();
        private float[] _positionX = Array.Empty<float>();
        private float[] _positionY = Array.Empty<float>();
        private float[] _positionZ = Array.Empty<float>();
        private int[] _positionFlags = Array.Empty<int>();

        private double _lastSnapshotSeconds = double.NaN;

        /// <summary><c>poses.jsonl</c>, opened beside the run's other writers on the first sample.</summary>
        private JsonlWriter _poses;

        /// <summary>The table's columns, base set plus one per patch — <see cref="Report"/>'s.</summary>
        public static IReadOnlyList<string> Columns(RunConfig config) => new Report("x", config).Columns;

        // ------------------------------------------------------------------ the whole state
        //
        // Four sets and nineteen baselines, and every one of them is the difference between a
        // lineage and a standing crop or between a window and a running total. A resumed run
        // whose sampler started empty would report its first window as the whole run and would
        // count every living creature as the first of its kind, which is the same class of
        // mistake CLAUDE.md's floor rule warns about: a column that reads plausibly and means
        // something else. The position buffers and the absorptive row list are a sample's own
        // scratch, and poses.jsonl is a writer rather than a state.

        /// <summary>Writes the ever-seen sets, the window baselines and the snapshot mark.</summary>
        public void WriteState(System.IO.BinaryWriter w)
        {
            StateIo.Tag(w, "SMPL");

            WriteIds(w, _everAbsorptive);
            WriteIds(w, _everJointed);
            WriteIds(w, _everPhotosynthetic);
            WriteIds(w, _everBuoyant);

            w.Write(_lastFloorSpawns);
            w.Write(_lastUptakeLimited);
            w.Write(_lastPhotosyntheticSteps);
            w.Write(_lastBurntTotal);
            w.Write(_lastRemineralisedTotal);
            w.Write(_lastDetritusDeposited);
            w.Write(_lastDetritusTaken);
            w.Write(_lastDetritusExuded);
            w.Write(_lastDetritusReturned);
            w.Write(_lastMatterInfluxed);
            w.Write(_lastMatterBuried);
            w.Write(_lastWraps);
            w.Write(_lastCrowded);
            w.Write(_lastOverlapPairs);
            w.Write(_lastOverlapPairsJointed);
            w.Write(_lastOverlapPairsHeld);
            w.Write(_lastBedOrGlassBodies);
            w.Write(_lastContactSteps);
            w.Write(_lastSnapshotSeconds);

            w.Write(_lastModuleAdds);
            w.Write(_lastModuleDrops);
            w.Write(_lastModuleAddsRefused);
            w.Write(_lastModuleAddsRefusedForShape);
            w.Write(_lastModuleAddsRefusedForReserve);

            w.Write(_lastPartsKilled);
            w.Write(_lastBodiesEaten);
            w.Write(_lastCorpsesEaten);
            w.Write(_lastHealingJoules);
        }

        /// <summary>Puts the sampler back.</summary>
        public void ReadState(System.IO.BinaryReader r)
        {
            StateIo.Tag(r, "SMPL");

            ReadIds(r, _everAbsorptive);
            ReadIds(r, _everJointed);
            ReadIds(r, _everPhotosynthetic);
            ReadIds(r, _everBuoyant);

            _lastFloorSpawns = r.ReadInt64();
            _lastUptakeLimited = r.ReadInt64();
            _lastPhotosyntheticSteps = r.ReadInt64();
            _lastBurntTotal = r.ReadDouble();
            _lastRemineralisedTotal = r.ReadDouble();
            _lastDetritusDeposited = r.ReadDouble();
            _lastDetritusTaken = r.ReadDouble();
            _lastDetritusExuded = r.ReadDouble();
            _lastDetritusReturned = r.ReadDouble();
            _lastMatterInfluxed = r.ReadDouble();
            _lastMatterBuried = r.ReadDouble();
            _lastWraps = r.ReadInt64();
            _lastCrowded = r.ReadInt64();
            _lastOverlapPairs = r.ReadInt64();
            _lastOverlapPairsJointed = r.ReadInt64();
            _lastOverlapPairsHeld = r.ReadInt64();
            _lastBedOrGlassBodies = r.ReadInt64();
            _lastContactSteps = r.ReadInt64();
            _lastSnapshotSeconds = r.ReadDouble();

            _lastModuleAdds = r.ReadInt64();
            _lastModuleDrops = r.ReadInt64();
            _lastModuleAddsRefused = r.ReadInt64();
            _lastModuleAddsRefusedForShape = r.ReadInt64();
            _lastModuleAddsRefusedForReserve = r.ReadInt64();

            _lastPartsKilled = r.ReadInt64();
            _lastBodiesEaten = r.ReadInt64();
            _lastCorpsesEaten = r.ReadInt64();
            _lastHealingJoules = r.ReadDouble();
        }

        /// <summary>
        /// A set of ids, ascending.
        /// </summary>
        /// <remarks>
        /// Sorted on the way out so that two checkpoints of the same world are the same bytes,
        /// which is what lets a test compare files rather than parse them. A hash set's own
        /// enumeration order is not a promise.
        /// </remarks>
        private static void WriteIds(System.IO.BinaryWriter w, HashSet<long> ids)
        {
            var sorted = new long[ids.Count];
            ids.CopyTo(sorted);
            Array.Sort(sorted);

            w.Write(sorted.Length);
            for (int i = 0; i < sorted.Length; i++) w.Write(sorted[i]);
        }

        private static void ReadIds(System.IO.BinaryReader r, HashSet<long> into)
        {
            int count = r.ReadInt32();
            into.Clear();
            for (int i = 0; i < count; i++) into.Add(r.ReadInt64());
        }

        /// <summary>
        /// Writes the sample and returns the markdown row.
        /// </summary>
        public string Write(Simulation sim, RunDirectory dir, IReadOnlyList<string> columns)
        {
            World world = sim.World;

            double spend = 0d, workSpend = 0d, depth = 0d, light = 0d, food = 0d;
            double travelled = 0d, age = 0d;
            int jointed = 0, jointedInherited = 0, dof = 0, absorptive = 0, inherited = 0;

            double foodJointed = 0d, foodRigid = 0d;
            int foodJointedCount = 0, foodRigidCount = 0;

            double depthJointed = 0d, depthRigid = 0d;
            int sensing = 0;
            int buoyant = 0, buoyantInherited = 0;
            int photosyntheticInherited = 0;
            double liftHeld = 0d, buoyantDepth = 0d;
            int genMin = int.MaxValue, genMax = 0;
            int belowWorld = 0, absorptiveBelowWorld = 0;

            double matterLocked = world.StandingJoulesInBodies / world.Config.JoulesPerUnit;

            var speciesSeen = new HashSet<uint>();
            var absorptiveNow = new HashSet<long>();

            bool recordPositions = dir?.Positions != null;
            int positionCount = 0;

            if (recordPositions) EnsurePositionCapacity(world.Living.Count);

            var alivePerPatch = new int[Math.Max(1, (int)world.Config.HorizontalPatches)];

            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism creature = world.Living[i];
                speciesSeen.Add(creature.SpeciesId);

                if (creature.Patch >= 0 && creature.Patch < alivePerPatch.Length)
                {
                    alivePerPatch[creature.Patch]++;
                }

                spend += creature.Lifetime.Expenditure;
                workSpend += creature.Lifetime.Work;
                depth += creature.HeightY;
                light += creature.Lifetime.LightIncome;
                food += creature.Lifetime.FoodIncome;
                travelled += Math.Abs(creature.HeightY - creature.BirthHeightY);
                age += creature.Age;

                bool creatureAbsorptive = false;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    if (part.CellTypeId != CellTypeIds.Absorptive) continue;

                    absorptive++;
                    creatureAbsorptive = true;
                    absorptiveNow.Add(creature.Id);
                    _everAbsorptive.Add(creature.Id);

                    if (_everAbsorptive.Contains(creature.ParentId)) inherited++;
                    break;
                }

                if (creature.HasPhotosyntheticTissue)
                {
                    _everPhotosynthetic.Add(creature.Id);
                    if (_everPhotosynthetic.Contains(creature.ParentId)) photosyntheticInherited++;
                }

                int creatureDof = 0;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    creatureDof += part.JointType.DofCount();
                }

                if (creatureDof > 0)
                {
                    jointed++;
                    _everJointed.Add(creature.Id);
                    if (_everJointed.Contains(creature.ParentId)) jointedInherited++;
                }
                dof += creatureDof;

                if (recordPositions && sim.TryRootPosition(creature.Id, out Float3 positionRoot))
                {
                    _positionIds[positionCount] = creature.Id;
                    _positionX[positionCount] = positionRoot.X;
                    _positionY[positionCount] = positionRoot.Y;
                    _positionZ[positionCount] = positionRoot.Z;
                    _positionFlags[positionCount] =
                        (creatureAbsorptive ? PositionsRow.AbsorptiveBit : 0) |
                        (creatureDof > 0 ? PositionsRow.JointedBit : 0) |
                        (creature.HasPhotosyntheticTissue ? PositionsRow.PhotosyntheticBit : 0);

                    positionCount++;
                }

                double foodHere = world.Nutrients.EdibleDensityAt(creature.Point);

                if (creatureDof > 0)
                {
                    foodJointed += foodHere; foodJointedCount++; depthJointed += creature.HeightY;
                }
                else
                {
                    foodRigid += foodHere; foodRigidCount++; depthRigid += creature.HeightY;
                }

                if (ReadsPerception(creature.Genome)) sensing++;

                float creatureLift = 0f;
                foreach (PhenotypePart part in creature.Phenotype.Parts)
                {
                    if (part.CellTypeId == CellTypeIds.Buoyancy) creatureLift += part.Lift;
                }

                if (creatureLift > 0f)
                {
                    buoyant++;
                    liftHeld += creatureLift;
                    buoyantDepth += creature.HeightY;

                    _everBuoyant.Add(creature.Id);
                    if (_everBuoyant.Contains(creature.ParentId)) buoyantInherited++;
                }

                if (creature.GenerationDepth < genMin) genMin = creature.GenerationDepth;
                if (creature.GenerationDepth > genMax) genMax = creature.GenerationDepth;

                if (creature.HeightY < -world.Config.WorldDepthMetres)
                {
                    belowWorld++;
                    if (creatureAbsorptive) absorptiveBelowWorld++;
                }
            }

            int alive = world.Living.Count;
            if (alive == 0) genMin = 0;

            int photosynthetic = world.CountPhotosynthetic();

            HorizontalSpread spread = sim.MeasureHorizontalSpread(absorptiveNow);

            double meanDepth = alive > 0 ? depth / alive : 0d;
            double variance = 0d;

            for (int i = 0; i < world.Living.Count; i++)
            {
                double d = world.Living[i].HeightY - meanDepth;
                variance += d * d;
            }

            double depthSd = alive > 1 ? Math.Sqrt(variance / (alive - 1)) : 0d;

            double workShare = spend > 0d ? workSpend / spend : 0d;
            double residual = world.EnergyIn > 0d ? 100d * world.AuditResidual / world.EnergyIn : 0d;
            double seconds = sim.StepsPerMetabolicStep * sim.PhysicsDt;

            double edibleHere = world.Nutrients.EdibleDensityAt((float)meanDepth, 0);
            double refugeStock = FloorStock(world.Nutrients);

            double burntWindow = world.BurntTotal - _lastBurntTotal;
            double remineralisedWindow = world.RemineralisedTotal - _lastRemineralisedTotal;

            long photoStepsWindow = world.PhotosyntheticSteps - _lastPhotosyntheticSteps;
            long uptakeLimitedWindow = world.UptakeLimitedSteps - _lastUptakeLimited;
            double uptakeLimitedShare =
                photoStepsWindow > 0 ? uptakeLimitedWindow / (double)photoStepsWindow : 0d;

            double detritusInWindow = world.DetritusDepositedTotal - _lastDetritusDeposited;
            double detritusOutWindow = world.DetritusTakenTotal - _lastDetritusTaken;
            double detritusExudedWindow = world.DetritusExudedTotal - _lastDetritusExuded;
            double detritusReturnedWindow = world.DetritusReturnedTotal - _lastDetritusReturned;

            double matterInfluxWindow = world.MatterInfluxedTotal - _lastMatterInfluxed;
            double matterBuriedWindow = world.MatterBuriedTotal - _lastMatterBuried;

            long wrapsWindow = sim.Wraps - _lastWraps;
            long crowdedWindow = sim.Crowded - _lastCrowded;

            long moduleAddsWindow = world.ModuleAdds - _lastModuleAdds;
            long moduleDropsWindow = world.ModuleDrops - _lastModuleDrops;
            long moduleRefusedWindow = world.ModuleAddsRefused - _lastModuleAddsRefused;
            long moduleRefusedShapeWindow = world.ModuleAddsRefusedForShape - _lastModuleAddsRefusedForShape;
            long moduleRefusedReserveWindow = world.ModuleAddsRefusedForReserve - _lastModuleAddsRefusedForReserve;
            long modulesStanding = world.ModulesStanding;
            double indeterminateShare = world.IndeterminateShare;

            // D106 items 1, 3 and 4's windows and shares. The three shares are read off the
            // cached per-body flags, so each is a walk over the living and not over their parts.
            long partsKilledWindow = world.PartsKilled - _lastPartsKilled;
            long bodiesEatenWindow = world.BodiesEaten - _lastBodiesEaten;
            long corpsesEatenWindow = world.CorpsesEaten - _lastCorpsesEaten;
            double healingWindow = world.HealingJoules - _lastHealingJoules;

            double attackShare = world.AttackShare;
            double intakeShare = world.IntakeShare;
            double protectionShare = world.ProtectionShare;

            double matterResidual = world.MatterResidual;

            // The overlap instrument's window, against the physics steps the overlaps arrived
            // over: an overlap is counted every step, not every sample.
            long overlapWindow = sim.Dynamics.OverlapPairs - _lastOverlapPairs;
            long overlapJointedWindow = sim.Dynamics.OverlapPairsJointed - _lastOverlapPairsJointed;
            long overlapHeldWindow = sim.Dynamics.OverlapPairsHeld - _lastOverlapPairsHeld;
            long bedOrGlassWindow = sim.Dynamics.BedOrGlassBodies - _lastBedOrGlassBodies;
            long contactSteps = sim.Volume != null ? sim.Steps - _lastContactSteps : 0L;
            long floorSteps = sim.Floor != null ? contactSteps : 0L;

            int patchesForReport = Math.Max(1, (int)world.Config.HorizontalPatches);
            double detritusPatchSd = 0d;
            double patchMaxShare = 0d;

            if (patchesForReport > 1)
            {
                float deepHeight = -(float)world.Config.WorldDepthMetres * 0.9f;
                var patchDensities = new double[patchesForReport];
                double meanPatchDensity = 0d;

                for (int p = 0; p < patchesForReport; p++)
                {
                    patchDensities[p] = world.Nutrients.DensityAt(deepHeight, p);
                    meanPatchDensity += patchDensities[p];
                }
                meanPatchDensity /= patchesForReport;

                double sumSquares = 0d;
                for (int p = 0; p < patchesForReport; p++)
                {
                    double d = patchDensities[p] - meanPatchDensity;
                    sumSquares += d * d;
                }
                detritusPatchSd = Math.Sqrt(sumSquares / patchesForReport);

                var patchCounts = new int[patchesForReport];
                for (int i = 0; i < world.Living.Count; i++)
                {
                    int p = world.Living[i].Patch;
                    if (p >= 0 && p < patchesForReport) patchCounts[p]++;
                }

                int maxCount = 0;
                for (int p = 0; p < patchesForReport; p++)
                {
                    if (patchCounts[p] > maxCount) maxCount = patchCounts[p];
                }

                patchMaxShare = alive > 0 ? (double)maxCount / alive : 0d;
            }

            _absorptiveRows.Clear();
            int absorptiveTruncated = world.CollectAbsorptiveLog(_absorptiveRows);
            int absorptiveLogged = 0;

            if (dir != null)
            {
                for (int i = 0; i < _absorptiveRows.Count; i++)
                {
                    dir.Absorptive.Write(_absorptiveRows[i].ToJson());
                    absorptiveLogged++;
                }

                if (absorptiveTruncated > 0)
                {
                    dir.Absorptive.Write(
                        AbsorptiveSample.TruncatedRowJson(world.ElapsedSeconds, absorptiveTruncated));
                    absorptiveLogged++;
                }
            }

            sim.DrainMotility(
                out double speedJointedSum, out long speedJointedSamples,
                out double speedRigidSum, out long speedRigidSamples);

            double detritusCv = world.Nutrients is GridField detritusGrid
                ? detritusGrid.DensityCoefficientOfVariation()
                : 0d;
            double matterCv = world.Matter is GridField matterGrid
                ? matterGrid.DensityCoefficientOfVariation()
                : 0d;

            bool shapedBed = world.Bed != null && world.Bed.HasRelief && world.Nutrients is GridField;
            double floorStockJoules = 0d;
            double floorLowQuarterShare = 0d;
            var floorDecileShares = new float[0];

            if (shapedBed)
            {
                (float[] floors, double[] stocks) =
                    ((GridField)world.Nutrients).ColumnFloorAndFloorStock();

                float lowest = float.MaxValue;
                float highest = float.MinValue;

                for (int i = 0; i < floors.Length; i++)
                {
                    if (floors[i] < lowest) lowest = floors[i];
                    if (floors[i] > highest) highest = floors[i];
                }

                double threshold = lowest + 0.25d * (highest - lowest);
                double low = 0d;

                for (int i = 0; i < stocks.Length; i++)
                {
                    floorStockJoules += stocks[i];
                    if (floors[i] <= threshold) low += stocks[i];
                }

                floorLowQuarterShare = floorStockJoules > 0d ? low / floorStockJoules : 0d;

                if (floors.Length >= 10 && floorStockJoules > 0d)
                {
                    var order = new int[floors.Length];
                    for (int i = 0; i < order.Length; i++) order[i] = i;
                    Array.Sort(order, (a, b) => floors[a].CompareTo(floors[b]));

                    floorDecileShares = new float[10];

                    for (int rank = 0; rank < order.Length; rank++)
                    {
                        int bin = Math.Min(9, rank * 10 / order.Length);
                        floorDecileShares[bin] += (float)(stocks[order[rank]] / floorStockJoules);
                    }
                }
            }

            // ---------------------------------------------------------------- stats.jsonl

            dir?.Stats.WriteRow(w =>
            {
                w
                .Field("t", world.ElapsedSeconds)
                .Field("alive", alive)
                .Field("births", world.Births)
                .Field("deaths", world.Deaths)
                .Field("jointed", jointed)
                .Field("jointedInherited", jointedInherited)
                .Field("dof", dof)
                .Field("meanSpeed", sim.MeanSpeed)
                .Field("maxSpeed", sim.MaxSpeed)
                .Field("workJoulesPerSecond", sim.WorkThisStep / seconds)
                .Field("spendJoules", spend)
                .Field("workJoules", workSpend)
                .Field("lightJoules", light)
                .Field("foodJoules", food)
                .Field("absorptive", absorptive)
                .Field("absorptiveInherited", inherited)
                .Field("detritusJoules", world.Nutrients.TotalJoules)
                .Field("detritusHere", world.Nutrients.DensityAt((float)meanDepth, 0))
                .Field("detritusOnFloor", refugeStock)
                .Field("detritusDeep",
                    world.Nutrients.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0))
                .Field("meanHeight", meanDepth)
                .Field("heightSd", depthSd)
                .Field("meanRise", alive > 0 ? travelled / alive : 0d)
                .Field("meanAge", alive > 0 ? age / alive : 0d)
                .Field("dayFactor", world.Field.DayFactor)
                .Field("shading", 1d - world.Field.ShadingAt((float)meanDepth))
                .Field("buoyant", buoyant)
                .Field("buoyantInherited", buoyantInherited)
                .Field("liftHeld", liftHeld)
                .Field("buoyantDepth", buoyant > 0 ? buoyantDepth / buoyant : 0d)
                .Field("matterHere", world.Matter.DensityAt((float)meanDepth, 0))
                .Field("matterSurface", world.Matter.DensityAt(0f, 0))
                .Field("matterDeep",
                    world.Matter.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0))
                .Field("matterStanding", world.StandingMatterUnits)
                .Field("uptakeLimitedShare", uptakeLimitedShare)
                .Field("floorSpawns", world.FloorSpawns)
                .Field("floorSpawnsWindow", world.FloorSpawns - _lastFloorSpawns)
                .Field("secondsSinceFloorFired", world.SecondsSinceFloorFired)
                .Field("generationMin", genMin)
                .Field("generationMax", genMax)
                .Field("auditResidual", world.AuditResidual)
                .Field("species", speciesSeen.Count)
                .Field("edibleDetritusHere", edibleHere)
                .Field("belowWorld", belowWorld)
                .Field("absorptiveBelowWorld", absorptiveBelowWorld)
                .Field("matterLocked", matterLocked)
                .Field("refugeJoules", refugeStock)
                .Field("burntTotal", world.BurntTotal)
                .Field("burntWindow", burntWindow)
                .Field("detritusPatchSd", detritusPatchSd)
                .Field("patchMaxShare", patchMaxShare)
                .Field("detritusDepositedTotal", world.DetritusDepositedTotal)
                .Field("detritusDepositedWindow", detritusInWindow)
                .Field("detritusTakenTotal", world.DetritusTakenTotal)
                .Field("detritusTakenWindow", detritusOutWindow)
                .Field("detritusExudedTotal", world.DetritusExudedTotal)
                .Field("detritusExudedWindow", detritusExudedWindow)
                .Field("absorptiveLogged", absorptiveLogged)
                .Field("photosynthetic", photosynthetic)
                .Field("photosyntheticInherited", photosyntheticInherited)
                .Field("diverged", world.Diverged)
                .Field("matterInfluxWindow", matterInfluxWindow)
                .Field("matterBuriedWindow", matterBuriedWindow)
                .Field("matterInfluxedTotal", world.MatterInfluxedTotal)
                .Field("matterBuriedTotal", world.MatterBuriedTotal)
                .Field("speedJointed",
                    speedJointedSamples > 0 ? speedJointedSum / speedJointedSamples : 0d)
                .Field("speedJointedSamples", speedJointedSamples)
                .Field("speedRigid", speedRigidSamples > 0 ? speedRigidSum / speedRigidSamples : 0d)
                .Field("speedRigidSamples", speedRigidSamples)
                .Field("foodJointed", foodJointedCount > 0 ? foodJointed / foodJointedCount : 0d)
                .Field("foodJointedCount", foodJointedCount)
                .Field("foodRigid", foodRigidCount > 0 ? foodRigid / foodRigidCount : 0d)
                .Field("foodRigidCount", foodRigidCount)
                .Field("sharedSpace", world.Config.SharedSpace)
                .Field("aboveSurface", sim.AboveSurface)
                .Field("wraps", sim.Wraps)
                .Field("wrapsWindow", wrapsWindow)
                .Field("crowded", sim.Crowded)
                .Field("crowdedWindow", crowdedWindow)

                // The four that changed their name because they changed their census — see the
                // class remarks. Their places in the row are the four the contact instrument's
                // held, so a positional reader of an old file is not silently handed a different
                // measurement under the old name.
                .Field("overlapPairs", sim.Dynamics.OverlapPairs)
                .Field("overlapPairsPerStep",
                    contactSteps > 0 ? overlapWindow / (double)contactSteps : 0d)
                .Field("overlapPairsJointed", sim.Dynamics.OverlapPairsJointed)
                .Field("overlapPairsHeld", sim.Dynamics.OverlapPairsHeld)
                .Field("overlapBodies", sim.Dynamics.OverlapBodies)

                // The bed and the glass, counted apart from the crowd's as the farm counts them,
                // and as bodies rather than as manifolds for the same reason the four above are.
                .Field("bedOrGlassBodies", sim.Dynamics.BedOrGlassBodies)
                .Field("bedOrGlassBodiesPerStep",
                    floorSteps > 0 ? bedOrGlassWindow / (double)floorSteps : 0d)

                .Field("stillbirths", world.Stillbirths)
                .Field("selfOverlapStillbirths", world.SelfOverlapStillbirths)
                .Field("detritusReturnedWindow", detritusReturnedWindow)
                .Field("sensing", sensing)
                .Field("depthJointed", foodJointedCount > 0 ? depthJointed / foodJointedCount : 0d)
                .Field("depthRigid", foodRigidCount > 0 ? depthRigid / foodRigidCount : 0d)
                .Field("detritusVertices", world.Nutrients is VertexField dv ? dv.Count : 0)
                .Field("matterVertices", world.Matter is VertexField mv ? mv.Count : 0)
                .Field("verticesMerged",
                    (world.Nutrients is VertexField dm ? dm.Merged : 0L) +
                    (world.Matter is VertexField mm ? mm.Merged : 0L))
                .Field("matterResidual", matterResidual)
                .Field("remineralisedTotal", world.RemineralisedTotal)
                .Field("remineralisedWindow", remineralisedWindow)
                .Field("corpses", world.Corpses.Count)
                .Field("corpseJoules", world.CorpseJoules)
                .Field("detritusReturnedTotal", world.DetritusReturnedTotal)
                .Field("meanAdultScale", alive > 0 ? world.MeanAdultScale : 0f)
                .Field("meanBirthInvestment", alive > 0 ? world.MeanBirthInvestment : 0f)
                .Field("meanBroodSize", alive > 0 ? world.MeanBroodSize : 0f)
                .Field("meanBodyFraction", alive > 0 ? world.MeanBodyFraction : 0f)
                .Field("meanReserveMargin", alive > 0 ? world.MeanReserveMargin : 0f)
                .Field("conceptionsUnderMargin", world.ConceptionsUnderMargin)
                .Field("conceptionsUnderMassFloor", world.ConceptionsUnderMassFloor)
                .Field("resizes", sim.Resizes)
                .Field("resizeJumpMetres", sim.MaxResizeJumpMetres)
                .Field("resizeStepMetres", sim.MaxResizeStepMetres)
                .Field("occupiedColumns", spread.OccupiedColumns)
                .Field("totalColumns", spread.TotalColumns)
                .Field("occupiedColumnsAbsorptive", spread.OccupiedColumnsAbsorptive)
                .Field("xSpreadMetres", spread.XSpreadMetres)
                .Field("zSpreadMetres", spread.ZSpreadMetres)
                .Field("maxJointMassRatio", sim.MaxJointMassRatio)
                .Field("bodiesOverMassRatio10", sim.BodiesOverMassRatio10)
                .Field("detritusCv", detritusCv)
                .Field("matterCv", matterCv)
                .Field("floorStockJoules", floorStockJoules)
                .Field("floorStockLowQuarterShare", floorLowQuarterShare)
                .Field("wallPhysicsMs", sim.WallPhysicsMs)
                .Field("wallWorldMs", sim.WallWorldMs)
                .Field("wallHarnessMs", sim.WallHarnessMs)
                .Field("wallWritersMs", sim.WallWritersMs)
                .Field("wallTotalMs", sim.WallTotalMs)

                // D106 item 2, rule 8. `modulesStanding` and `indeterminateShare` are states and
                // the other three are running totals a reader differences into a window, which is
                // what scripts/reads/r44-read.py does with them. `moduleRebuilds` is the
                // harness's own count of bodies rebuilt on a new plan, beside `resizes`.
                .Field("modulesStanding", modulesStanding)
                .Field("moduleAdds", world.ModuleAdds)
                .Field("moduleDrops", world.ModuleDrops)
                .Field("moduleAddsRefused", world.ModuleAddsRefused)
                .Field("moduleAddsRefusedForShape", world.ModuleAddsRefusedForShape)
                .Field("moduleAddsRefusedForReserve", world.ModuleAddsRefusedForReserve)
                .Field("indeterminateShare", indeterminateShare)
                .Field("moduleRebuilds", sim.ModuleRebuilds)

                // D106 items 1, 3 and 4, rule 8. The six counters are running totals a reader
                // differences into a window, as the module gene's three are; the three shares are
                // states. `corpsesFromKills` is beside `bodiesEaten` because the two part in a
                // world at CorpseDecayPerSecond 0, where a kill deposits at once and founds no
                // corpse at all.
                .Field("partsKilled", world.PartsKilled)
                .Field("bodiesEaten", world.BodiesEaten)
                .Field("corpsesFromKills", world.CorpsesFromKills)
                .Field("unitsEaten", world.UnitsEaten)
                .Field("corpsesEaten", world.CorpsesEaten)
                .Field("healingJoules", world.HealingJoules)
                .Field("attackShare", attackShare)
                .Field("intakeShare", intakeShare)
                .Field("protectionShare", protectionShare);

                long[] harnessPhaseMs = sim.HarnessPhaseMs();

                for (int p = 0; p < harnessPhaseMs.Length; p++)
                {
                    w.Field(Simulation.HarnessPhaseFields[p], harnessPhaseMs[p]);
                }

                w.Field("harnessBodySteps", sim.HarnessBodySteps);

                // The fluid phase's own four. This engine has no separate drag pass to time — a
                // body's drag runs on the body's own thread inside the solver's step — so all
                // four read 0 and `fluidLinkSteps` is the denominator the solver's own
                // microseconds-per-link-step is taken over. Written rather than dropped, for
                // D065's reason: a reader must not have to work out whether a missing field means
                // "free" or "written before the instrument".
                w.Field("wallFluidGatherMs", 0L);
                w.Field("wallFluidWaterMs", 0L);
                w.Field("wallFluidComputeMs", 0L);
                w.Field("wallFluidApplyMs", 0L);
                w.Field("fluidLinkSteps", sim.FluidLinkSteps);

                w.BeginArray("alivePerPatch");
                for (int p = 0; p < alivePerPatch.Length; p++) w.Value(alivePerPatch[p]);
                w.EndArray();

                w.BeginArray("floorStockByFloorDecile");
                for (int d = 0; d < floorDecileShares.Length; d++) w.Value(floorDecileShares[d]);
                w.EndArray();
            });

            // ---------------------------------------------------- positions.jsonl and poses.jsonl

            if (recordPositions)
            {
                dir.Positions.Write(PositionsRow.Write(
                    world.ElapsedSeconds, positionCount,
                    _positionIds, _positionX, _positionY, _positionZ, _positionFlags));

                WritePoses(sim, dir);
            }

            IReadOnlyList<LineageEvent> lineageEvents = world.DrainLineageEvents();
            if (dir != null)
            {
                for (int i = 0; i < lineageEvents.Count; i++)
                {
                    dir.Lineage.Write(lineageEvents[i].ToJson());
                }
            }

            // ---------------------------------------------------------------- the markdown row

            var c = Inv;

            var row = new List<string>
            {
                world.ElapsedSeconds.ToString("0", c),
                alive.ToString(c),
                world.Births.ToString(c),
                world.Deaths.ToString(c),
                "**" + jointed.ToString(c) + "**",
                (alive > 0 ? 100d * jointed / alive : 0d).ToString("0.#", c) + "%",
                "**" + jointedInherited.ToString(c) + "**",
                (alive > 0 ? (double)dof / alive : 0d).ToString("0.##", c),
                sim.MeanSpeed.ToString("0.####", c),
                sim.MaxSpeed.ToString("0.####", c),
                (sim.WorkThisStep / seconds).ToString("0.##", c),
                (100d * workShare).ToString("0.#", c) + "%",
                "**" + (light + food > 0d ? 100d * food / (light + food) : 0d).ToString("0.##", c) + "%**",
                "**" + absorptive.ToString(c) + "**",
                "**" + inherited.ToString(c) + "**",
                "**" + world.Nutrients.TotalJoules.ToString("0.#", c) + "**",
                "**" + world.Nutrients.DensityAt((float)meanDepth, 0).ToString("0.####", c) + "**",
                "**" + (world.Nutrients.TotalJoules > 0d
                    ? 100d * refugeStock / world.Nutrients.TotalJoules
                    : 0d).ToString("0.#", c) + "%**",
                "**" + world.Nutrients.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0)
                    .ToString("0.####", c) + "**",
                meanDepth.ToString("0.#", c),
                "**" + depthSd.ToString("0.##", c) + "**",
                "**" + (alive > 0 ? travelled / alive : 0d).ToString("0.####", c) + "**",
                (alive > 0 ? age / alive : 0d).ToString("0.#", c),
                world.Field.DayFactor.ToString("0.##", c),
                (100d * (1d - world.Field.ShadingAt((float)meanDepth))).ToString("0.#", c) + "%",
                "**" + buoyant.ToString(c) + "**",
                "**" + buoyantInherited.ToString(c) + "**",
                buoyant > 0 ? (liftHeld / buoyant).ToString("0.##", c) : "—",
                buoyant > 0 ? (buoyantDepth / buoyant).ToString("0.#", c) : "—",
                world.Matter.DensityAt(0f, 0).ToString("0.###", c),
                world.Matter.DensityAt(-(float)world.Config.WorldDepthMetres * 0.9f, 0)
                    .ToString("0.###", c),
                photoStepsWindow > 0
                    ? "**" + (100d * uptakeLimitedShare).ToString("0.#", c) + "%**"
                    : "—",
                "**" + (world.FloorSpawns - _lastFloorSpawns).ToString(c) + "**",
                genMin.ToString(c),
                genMax.ToString(c),
                residual.ToString("0.0000", c) + "%",
                speciesSeen.Count.ToString(c),
                edibleHere.ToString("0.####", c),
                belowWorld.ToString(c),
                absorptiveBelowWorld.ToString(c),
                matterLocked.ToString("0.###", c),
                refugeStock.ToString("0.#", c),
                (remineralisedWindow / world.Config.JoulesPerUnit).ToString("0.######", c),
                detritusPatchSd.ToString("0.####", c),
                patchMaxShare.ToString("0.###", c),
                detritusInWindow.ToString("0.###", c),
                detritusOutWindow.ToString("0.###", c),
                detritusExudedWindow.ToString("0.###", c),
                "**" + absorptiveLogged.ToString(c) + "**",
                "**" + photosynthetic.ToString(c) + "**",
                "**" + photosyntheticInherited.ToString(c) + "**",
                world.Diverged.ToString(c),
                matterInfluxWindow.ToString("0.###", c),
                matterBuriedWindow.ToString("0.###", c),
                speedJointedSamples > 0
                    ? (speedJointedSum / speedJointedSamples).ToString("0.#####", c) : "—",
                speedRigidSamples > 0
                    ? (speedRigidSum / speedRigidSamples).ToString("0.#####", c) : "—",
                foodJointedCount > 0 ? (foodJointed / foodJointedCount).ToString("0.####", c) : "—",
                foodRigidCount > 0 ? (foodRigid / foodRigidCount).ToString("0.####", c) : "—",
                "**" + sim.AboveSurface.ToString(c) + "**",
                wrapsWindow.ToString(c),
                "**" + crowdedWindow.ToString(c) + "**",

                // `overlaps`, `ovl/body`, `ovl jnt %`, `ovl held %` — the four that changed name
                // because they changed census. The arithmetic is the farm's, term for term.
                contactSteps > 0 ? (overlapWindow / (double)contactSteps).ToString("0.###", c) : "—",
                contactSteps > 0 && alive > 0
                    ? (overlapWindow / (double)contactSteps / alive).ToString("0.####", c)
                    : "—",
                overlapWindow > 0
                    ? (100d * overlapJointedWindow / overlapWindow).ToString("0.#", c) + "%"
                    : "—",
                overlapWindow > 0
                    ? (100d * overlapHeldWindow / overlapWindow).ToString("0.#", c) + "%"
                    : "—",

                floorSteps > 0 ? (bedOrGlassWindow / (double)floorSteps).ToString("0.###", c) : "—",
                world.Stillbirths.ToString(c),
                "**" + sensing.ToString(c) + "**",
                foodJointedCount > 0 ? (depthJointed / foodJointedCount).ToString("0.##", c) : "—",
                foodRigidCount > 0 ? (depthRigid / foodRigidCount).ToString("0.##", c) : "—",
                world.Matter.DensityAt((float)meanDepth, 0).ToString("0.###", c),
                world.Nutrients is VertexField rowDetritus && world.Matter is VertexField rowMatter
                    ? rowDetritus.Count.ToString(c) + "/" + rowMatter.Count.ToString(c)
                    : "—",
                "**" + matterResidual.ToString("0.###", c) + "**",
                burntWindow.ToString("0.######", c),
                world.Corpses.Count.ToString(c),
                alive > 0 ? world.MeanAdultScale.ToString("0.###", c) : "—",
                alive > 0 ? world.MeanBirthInvestment.ToString("0.###", c) : "—",
                alive > 0 ? world.MeanBroodSize.ToString("0.###", c) : "—",
                alive > 0 ? world.MeanReserveMargin.ToString("0.#", c) : "—",
                alive > 0 ? world.MeanBodyFraction.ToString("0.###", c) : "—",
                spread.TotalColumns > 0
                    ? spread.OccupiedColumns.ToString(c) + "/" + spread.TotalColumns.ToString(c)
                    : "—",
                spread.TotalColumns > 0
                    ? spread.OccupiedColumnsAbsorptive.ToString(c) + "/" +
                      spread.TotalColumns.ToString(c)
                    : "—",
                spread.TotalColumns > 0 ? spread.XSpreadMetres.ToString("0.00", c) : "—",
                world.Nutrients is GridField ? detritusCv.ToString("0.###", c) : "—",
                world.Matter is GridField ? matterCv.ToString("0.###", c) : "—",
                shapedBed ? (100d * floorLowQuarterShare).ToString("0.#", c) + "%" : "—",
                shapedBed ? floorStockJoules.ToString("0.#", c) : "—",
                world.SelfOverlapStillbirths.ToString(c),

                // D106 item 2, rule 8, in BaseColumns' order.
                modulesStanding.ToString(c),
                moduleAddsWindow.ToString(c),
                moduleDropsWindow.ToString(c),
                moduleRefusedWindow.ToString(c),
                (100d * indeterminateShare).ToString("0.#", c) + "%",

                // D106 items 1, 3 and 4, rule 8, in BaseColumns' order.
                (100d * attackShare).ToString("0.#", c) + "%",
                (100d * intakeShare).ToString("0.#", c) + "%",
                (100d * protectionShare).ToString("0.#", c) + "%",
                partsKilledWindow.ToString(c),
                bodiesEatenWindow.ToString(c),
                corpsesEatenWindow.ToString(c),
                healingWindow.ToString("0.###", c),

                // The refusal split, appended after the mouth's seven (logbook/0113's read).
                moduleRefusedShapeWindow.ToString(c),
                moduleRefusedReserveWindow.ToString(c),
            };

            for (int p = 0; p < alivePerPatch.Length; p++) row.Add(alivePerPatch[p].ToString(c));

            _lastFloorSpawns = world.FloorSpawns;
            _lastUptakeLimited = world.UptakeLimitedSteps;
            _lastPhotosyntheticSteps = world.PhotosyntheticSteps;
            _lastBurntTotal = world.BurntTotal;
            _lastRemineralisedTotal = world.RemineralisedTotal;
            _lastDetritusDeposited = world.DetritusDepositedTotal;
            _lastDetritusTaken = world.DetritusTakenTotal;
            _lastDetritusExuded = world.DetritusExudedTotal;
            _lastDetritusReturned = world.DetritusReturnedTotal;
            _lastMatterInfluxed = world.MatterInfluxedTotal;
            _lastMatterBuried = world.MatterBuriedTotal;
            _lastWraps = sim.Wraps;
            _lastCrowded = sim.Crowded;
            _lastOverlapPairs = sim.Dynamics.OverlapPairs;
            _lastOverlapPairsJointed = sim.Dynamics.OverlapPairsJointed;
            _lastOverlapPairsHeld = sim.Dynamics.OverlapPairsHeld;
            _lastBedOrGlassBodies = sim.Dynamics.BedOrGlassBodies;
            _lastContactSteps = sim.Steps;
            _lastModuleAdds = world.ModuleAdds;
            _lastModuleDrops = world.ModuleDrops;
            _lastModuleAddsRefused = world.ModuleAddsRefused;
            _lastModuleAddsRefusedForShape = world.ModuleAddsRefusedForShape;
            _lastModuleAddsRefusedForReserve = world.ModuleAddsRefusedForReserve;
            _lastPartsKilled = world.PartsKilled;
            _lastBodiesEaten = world.BodiesEaten;
            _lastCorpsesEaten = world.CorpsesEaten;
            _lastHealingJoules = world.HealingJoules;

            if (row.Count != columns.Count)
            {
                throw new InvalidOperationException(
                    row.Count + " values against " + columns.Count + " headers. A column was " +
                    "added at one end and not the other, and every row after it would be " +
                    "mislabelled.");
            }

            return "| " + string.Join(" | ", row) + " |";
        }

        /// <summary>
        /// Every living creature's genome, one per line —
        /// <c>snapshots/&lt;t&gt;.jsonl</c>.
        /// </summary>
        /// <remarks>
        /// Once per instant, compared against the elapsed time rather than against the file's
        /// existence: the loop takes a snapshot every tenth report row and the shutdown takes one
        /// more, so a run whose budget lands exactly on a snapshot boundary would otherwise write
        /// the same population twice into the same file.
        /// </remarks>
        public void Snapshot(RunDirectory dir, World world)
        {
            if (dir == null) return;
            if (world.ElapsedSeconds == _lastSnapshotSeconds) return;
            _lastSnapshotSeconds = world.ElapsedSeconds;

            string path = System.IO.Path.ChangeExtension(
                dir.SnapshotPath(world.ElapsedSeconds), ".jsonl");

            using (var writer = new JsonlWriter(path, flushEachRow: false))
            {
                foreach (Organism creature in world.Living)
                {
                    // With the body's own plan beside the genome (2026-09-22 night): the module
                    // counts and the parts a bite took, so a picture drawn from the row is the
                    // body the run was stepping and not the genome's minimum. Round 44 seed 1's
                    // fourteen-metre leaf was invisible in every picture for want of them
                    // (logbook/0113's read).
                    writer.Write(GenomeJson.Write(
                        creature.Genome, indent: false, id: creature.Id,
                        moduleCounts: creature.ModuleCounts, lostPartPaths: creature.LostPartPaths));
                }
            }
        }

        /// <summary>Closes <c>poses.jsonl</c>.</summary>
        public void Close()
        {
            _poses?.Dispose();
            _poses = null;
        }

        /// <summary>
        /// One row of <c>poses.jsonl</c>: every living body's root place, root attitude and joint
        /// coordinates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it is for.</b> <c>positions.jsonl</c> says where a body is and nothing about
        /// how it is held, so a picture drawn from a snapshot stands every creature upright in the
        /// developer's own frame and says so on the frame
        /// (<c>logbook/specs/snapshot-render-spec.md</c>). With the attitude and the joint
        /// coordinates beside the place, a reader that has the genome has the whole pose.
        /// </para>
        /// <para>
        /// <b>Not part of positions.jsonl.</b> That file's schema is read by the theatre and is
        /// left untouched to the character; this is a second file at the same cadence, joined on
        /// the organism id, exactly as <c>absorptive.jsonl</c> is a second file rather than more
        /// columns on a stats row.
        /// </para>
        /// <para>
        /// <b>Four decimals on the quaternion and the joint angles, two on the metres.</b> The
        /// positions match <c>PositionsRow</c>'s centimetre, which is two orders under the
        /// smallest body; an attitude and an angle are dimensionless and a ten-thousandth of a
        /// radian is a hundredth of a degree, under anything a picture can show.
        /// </para>
        /// </remarks>
        private void WritePoses(Simulation sim, RunDirectory dir)
        {
            if (_poses == null)
            {
                _poses = new JsonlWriter(
                    System.IO.Path.Combine(dir.Path, "poses.jsonl"), flushEachRow: false);
            }

            var sb = new System.Text.StringBuilder(4096);
            sb.Append("{\"t\":").Append(sim.World.ElapsedSeconds.ToString("R", Inv));
            sb.Append(",\"bodies\":[");

            bool first = true;

            IReadOnlyList<Organism> living = sim.World.Living;

            for (int i = 0; i < living.Count; i++)
            {
                if (!sim.TryPose(living[i].Id, out Creature body)) continue;

                if (!first) sb.Append(',');
                first = false;

                sb.Append("{\"id\":").Append(living[i].Id.ToString(Inv));
                sb.Append(",\"p\":[")
                    .Append(Metre(body.BasePosition.X)).Append(',')
                    .Append(Metre(body.BasePosition.Y)).Append(',')
                    .Append(Metre(body.BasePosition.Z)).Append(']');

                sb.Append(",\"r\":[")
                    .Append(Unit(body.BaseRotation.X)).Append(',')
                    .Append(Unit(body.BaseRotation.Y)).Append(',')
                    .Append(Unit(body.BaseRotation.Z)).Append(',')
                    .Append(Unit(body.BaseRotation.W)).Append(']');

                sb.Append(",\"q\":[");
                for (int d = 0; d < body.Dof; d++)
                {
                    if (d > 0) sb.Append(',');
                    sb.Append(Unit(body.Q[d]));
                }
                sb.Append("]}");
            }

            sb.Append("]}");
            _poses.Write(sb.ToString());
        }

        /// <summary>A metre, to the centimetre, never a negative zero — <c>PositionsRow</c>'s rule.</summary>
        private static string Metre(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            double r = Math.Round(v, 2);
            if (r == 0d) r = 0d;
            return r.ToString("0.##", Inv);
        }

        /// <summary>A dimensionless number, to four places, never a negative zero.</summary>
        private static string Unit(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            double r = Math.Round(v, 4);
            if (r == 0d) r = 0d;
            return r.ToString("0.####", Inv);
        }

        private void EnsurePositionCapacity(int needed)
        {
            if (_positionIds.Length >= needed) return;

            int size = Math.Max(64, needed * 2);
            Array.Resize(ref _positionIds, size);
            Array.Resize(ref _positionX, size);
            Array.Resize(ref _positionY, size);
            Array.Resize(ref _positionZ, size);
            Array.Resize(ref _positionFlags, size);
        }

        /// <summary>
        /// What the refuge holds, J — patch 0, as the three legacy columns have always been.
        /// </summary>
        private static double FloorStock(IMatterField field) =>
            field is GridField grid
                ? grid.RefugeStock(0)
                : field.StockInLayer(field.LayerCount - 1, 0);

        /// <summary>
        /// True when any neuron in the genome reads one of D075's three perception channels.
        /// </summary>
        /// <remarks>
        /// On the genome, the thing mutation writes, rather than on the developed brain's mask: a
        /// genome that carries the input in a node development pruned still carries it.
        /// </remarks>
        private static bool ReadsPerception(Genome genome)
        {
            for (int n = 0; n < genome.Nodes.Count; n++)
            {
                NeuronDef[] neurons = genome.Nodes[n].Neurons;
                for (int k = 0; k < neurons.Length; k++)
                {
                    NeuronInput[] inputs = neurons[k].Inputs;
                    if (inputs == null) continue;

                    for (int i = 0; i < inputs.Length; i++)
                    {
                        if (inputs[i].Kind != NeuronInputKind.Sensor) continue;

                        SensorChannel ch = inputs[i].Channel;
                        if (ch == SensorChannel.Chemical || ch == SensorChannel.Energy ||
                            ch == SensorChannel.Flow)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
