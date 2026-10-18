using System;

namespace Evosim.Core
{
    public sealed partial class World
    {
        /// <summary>
        /// Corpses placed by an experimenter's hand (<see cref="PlaceCorpse"/>), which a world
        /// that only runs its own rules never does. 0 on every run on file.
        /// </summary>
        public long CorpsesPlaced { get; private set; }

        /// <summary>
        /// An assay's hold on breeding: while true no parent clears its gate, so a body keeps its
        /// reserve and the world holds the line it was given. The nursery's episodes set it, since
        /// a child with a mutated brain would score in its parent's name. Not a tunable: no config
        /// carries it, and false, the default, is every world on file.
        /// </summary>
        public bool BirthsHeld { get; set; }

        /// <summary>
        /// Lays a corpse of <paramref name="joules"/> at <paramref name="position"/>, as an assay
        /// does: the nursery's episodes (propose-nursery.md) put food where a brain has to find it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Booked as <see cref="Inoculate"/> books an inoculant</b>, because it is the same
        /// thing: joules created by the experimenter's hand. They enter <see cref="EnergyIn"/>, and
        /// the same over ρ enters <see cref="MatterInfluxedTotal"/> (D098's leg 9), so both books
        /// close over a world that is fed this way. From there the corpse is any other corpse: it
        /// sinks, settles, decays into the snow at the world's own rates, scents the water and is
        /// eaten by the world's own mouths.
        /// </para>
        /// <para>
        /// <b>Its creature id is negative</b> (-1, -2, ... in the order placed), so no join from a
        /// corpse to <c>lineage.jsonl</c> can take it for a body that lived. It carries no body
        /// plan, which nothing in the economy reads.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Corpses are not objects in this world (<see cref="RunConfig.CorpseDecayPerSecond"/> 0):
        /// a death deposits at once, and a placed corpse would be a rule the world does not have.
        /// </exception>
        public Corpse PlaceCorpse(Float3 position, double joules)
        {
            if (!(Config.CorpseDecayPerSecond > 0f))
            {
                throw new InvalidOperationException(
                    "PlaceCorpse needs corpses to be objects (CorpseDecayPerSecond above 0); this " +
                    "world deposits a death at once.");
            }

            if (!(joules > 0d) || double.IsInfinity(joules))
            {
                throw new ArgumentOutOfRangeException(nameof(joules), joules,
                    "a corpse holds a positive, finite number of joules");
            }

            if (float.IsNaN(position.X) || float.IsNaN(position.Z) ||
                float.IsInfinity(position.X) || float.IsInfinity(position.Z) ||
                !HeightIsInTheWorld(position.Y, Config.WorldDepthMetres))
            {
                throw new ArgumentOutOfRangeException(nameof(position), "the corpse's place is not in the world");
            }

            // In a tank the axis is at (R, R): a place past the glass is not in the water.
            if (TankRadiusMetres > 0f)
            {
                double dx = position.X - TankRadiusMetres, dz = position.Z - TankRadiusMetres;
                if (Math.Sqrt(dx * dx + dz * dz) > TankRadiusMetres)
                    throw new ArgumentOutOfRangeException(nameof(position), "the corpse's place is outside the tank's glass");
            }

            CorpsesChanged();
            CorpsesPlaced++;
            var corpse = new Corpse(-CorpsesPlaced, position, PatchOfXZ(position.X, position.Z), joules);
            _corpses.Add(corpse);

            EnergyIn += joules;
            MatterInfluxedTotal += joules / Config.JoulesPerUnit;
            return corpse;
        }
        /// <summary>
        /// Lays snow (the detritus a stomach absorbs) at <paramref name="densityJoulesPerCubicMetre"/>
        /// in the lowest <paramref name="layers"/> cells of every column, as an assay does: the
        /// nursery lays a round's bed snow in a tank whose own seed is matter and holds no snow
        /// until something dies. Returns the joules laid.
        /// </summary>
        /// <remarks>
        /// Booked as <see cref="PlaceCorpse"/> books a corpse: into <see cref="EnergyIn"/>, and the
        /// same over ρ into <see cref="MatterInfluxedTotal"/>. From there it is any other snow: it
        /// sinks, mixes, drifts and is drawn at the world's own rates.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The snow is not on a grid.</exception>
        public double LaySnow(float densityJoulesPerCubicMetre, int layers)
        {
            if (!(Nutrients is GridField grid))
                throw new InvalidOperationException("LaySnow needs the snow on a grid (FieldModel Grid).");
            if (!(densityJoulesPerCubicMetre >= 0f) || float.IsInfinity(densityJoulesPerCubicMetre))
                throw new ArgumentOutOfRangeException(nameof(densityJoulesPerCubicMetre), densityJoulesPerCubicMetre, "a density is finite and not negative");

            double joules = grid.SeedBed(densityJoulesPerCubicMetre, layers);
            EnergyIn += joules;
            MatterInfluxedTotal += joules / Config.JoulesPerUnit;
            return joules;
        }
        /// <summary>
        /// Lays snow through the whole water column in islands, as the world lays matter (D109): the
        /// same total as <paramref name="densityJoulesPerCubicMetre"/> over the live water, placed in
        /// the <paramref name="cover"/> of the columns where a gradient-noise map of
        /// <paramref name="wavelengthMetres"/> stands highest, with a short ramp at each shore.
        /// Returns the joules laid. The nursery's patchy larder: an even one rewards a blind cruise,
        /// since a body that keeps moving always meets fresh snow.
        /// </summary>
        /// <remarks>Booked as <see cref="LaySnow"/> books its snow.</remarks>
        /// <exception cref="InvalidOperationException">The snow is not on a grid.</exception>
        public double LaySnowIslands(float densityJoulesPerCubicMetre, ulong mapSeed, float wavelengthMetres, float cover)
        {
            if (!(Nutrients is GridField grid))
                throw new InvalidOperationException("LaySnowIslands needs the snow on a grid (FieldModel Grid).");
            if (!(densityJoulesPerCubicMetre >= 0f) || float.IsInfinity(densityJoulesPerCubicMetre))
                throw new ArgumentOutOfRangeException(nameof(densityJoulesPerCubicMetre), densityJoulesPerCubicMetre, "a density is finite and not negative");
            if (!(wavelengthMetres > 0f))
                throw new ArgumentOutOfRangeException(nameof(wavelengthMetres), wavelengthMetres, "a wavelength is positive");
            if (!(densityJoulesPerCubicMetre > 0f)) return 0d;

            double joules = densityJoulesPerCubicMetre * grid.LiveVolumeCubicMetres;
            var map = new GradientNoise(mapSeed, wavelengthMetres);
            double before = grid.TotalJoules;
            grid.SeedIslands(joules, map.Fbm, cover);
            joules = grid.TotalJoules - before;
            EnergyIn += joules;
            MatterInfluxedTotal += joules / Config.JoulesPerUnit;
            return joules;
        }
        /// <summary>
        /// Adds <paramref name="joules"/> to a living body's reserve, as an assay does: the
        /// nursery's episodes start every body with the same purse whatever its birth fraction,
        /// where <see cref="Inoculate"/> scales the founder's purse by it.
        /// </summary>
        /// <remarks>
        /// Booked as <see cref="PlaceCorpse"/> books a corpse: into <see cref="EnergyIn"/>, and the
        /// same over ρ into <see cref="MatterInfluxedTotal"/>, since a reserve is charged matter.
        /// </remarks>
        public void GiveReserve(Organism body, double joules)
        {
            if (body == null || !_living.Contains(body))
                throw new ArgumentException("the body is not alive in this world", nameof(body));
            if (!(joules >= 0d) || double.IsInfinity(joules))
                throw new ArgumentOutOfRangeException(nameof(joules), joules, "a reserve is given a finite, non-negative number of joules");
            body.Energy += joules;
            EnergyIn += joules;
            MatterInfluxedTotal += joules / Config.JoulesPerUnit;
        }
        /// <summary>
        /// Lays spent matter (the units a leaf fixes light with) at <paramref name="unitsPerCubicMetre"/>
        /// through every live cell of the water, as an assay does: the nursery's leaf tank starts
        /// its water with the spent units the round it copies held. Added to what the water holds.
        /// Returns the units laid.
        /// </summary>
        /// <remarks>
        /// Booked as the world's own influx of matter is booked: into
        /// <see cref="MatterInfluxedTotal"/>, unit for unit. Spent matter is not energy, so
        /// <see cref="EnergyIn"/> does not move, and <see cref="MatterResidual"/> closes over it as
        /// over the influx. From there it is any other spent matter: it mixes, drifts and is fixed
        /// at the world's own rates.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The matter is not on a grid.</exception>
        public double LaySpentMatter(float unitsPerCubicMetre)
        {
            if (!(Matter is GridField grid))
                throw new InvalidOperationException("LaySpentMatter needs the matter on a grid (FieldModel Grid).");
            if (!(unitsPerCubicMetre >= 0f) || float.IsInfinity(unitsPerCubicMetre))
                throw new ArgumentOutOfRangeException(nameof(unitsPerCubicMetre), unitsPerCubicMetre, "a density is finite and not negative");

            double before = grid.TotalJoules;
            grid.SeedUniform(unitsPerCubicMetre);
            double units = grid.TotalJoules - before;
            MatterInfluxedTotal += units;
            return units;
        }
        /// <summary>
        /// Admits one copy of <paramref name="genome"/> with its root at <paramref name="point"/>, as
        /// an assay does: the nursery's leaf tank sets each leaf where its own draw put it, where
        /// <see cref="Inoculate"/> leaves x and z to the placer. Returns the body.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>An inoculant in every other respect</b>: the same refusals asked first (D111 and the
        /// newborn mass floor), born at its own genome's birth fraction with the founder's purse
        /// scaled the same way, and booked as <see cref="Inoculate"/> books it, into both books.
        /// Its birth seed is the next of the world's own stream, as an inoculant's is.
        /// </para>
        /// <para>
        /// <b>Never moved.</b> The placer (<see cref="IBodyPlacement.TryReserveAt"/>) is asked for
        /// that point alone; a point in the rock, past the glass, on a reef or on another body is
        /// refused with the placer's reason. Every refusal is made before anything is admitted, so a
        /// refused call leaves the world as it was. With no placer (a world stepped without a
        /// solver) the body is admitted at the point, which nothing else then reads but the fields.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">The world is not a shared space, so has no x and z.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The point is not in the water.</exception>
        /// <exception cref="ArgumentException">The world will not admit the body there, with the reason.</exception>
        public Organism InoculateAt(Genome genome, Float3 point)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (!Config.SharedSpace)
                throw new InvalidOperationException("InoculateAt needs a shared space (SharedSpace true): a tiled world has no x and z to place a body at.");

            if (!point.IsFinite || point.Y > 0f || point.Y < -Config.WorldDepthMetres)
            {
                throw new ArgumentOutOfRangeException(nameof(point), point,
                    FormattableString.Invariant($"the point is not in the water, which runs from the surface at 0 down to {-Config.WorldDepthMetres} m"));
            }

            // In a tank the axis is at (R, R): a place past the glass is not in the water. In a box,
            // a place past a face is not in the world.
            if (TankRadiusMetres > 0f)
            {
                double dx = point.X - TankRadiusMetres, dz = point.Z - TankRadiusMetres;
                if (Math.Sqrt(dx * dx + dz * dz) > TankRadiusMetres)
                    throw new ArgumentOutOfRangeException(nameof(point), point, "the point is outside the tank's glass");
            }
            else if (point.X < 0f || point.X > Matter.LengthMetres || point.Z < 0f || point.Z > Matter.WidthMetres)
            {
                throw new ArgumentOutOfRangeException(nameof(point), point, "the point is outside the box");
            }

            genome = InoculantReady(genome, admitting: true);

            Phenotype adult = Developer.Develop(
                genome, Config.Development, null, Config.Shapes, cellTypes: Config.CellTypes);
            NewbornFrom(genome, adult, out Phenotype body, out double tissue, out float birthFraction);

            // Admit's two stillbirths, asked here so that a refusal spends nothing.
            if (body.PartCount == 0)
                throw new ArgumentException("the inoculant develops into no parts, so it would be stillborn", nameof(genome));
            if (Config.SelfOverlapDepthFraction > 0f && body.SelfOverlappingPairs(Config.SelfOverlapDepthFraction) > 0)
                throw new ArgumentException("the inoculant grows into itself (SelfOverlapDepthFraction), so it would be stillborn", nameof(genome));

            int patch;
            if (Placement != null)
            {
                if (!Placement.TryReserveAt(adult, point, out patch, out string refusal))
                    throw new ArgumentException("the world will not admit the inoculant at that point: " + refusal, nameof(point));
            }
            else
            {
                patch = PatchOfXZ(point.X, point.Z);
            }

            ulong seed = Rng.SeedFor(Seed, _nextIndex++);
            Organism creature = Admit(
                genome, body, BirthKind.Inoculation, seed, parentId: -1, generationDepth: 0,
                energy: Config.FounderEnergyJoules * birthFraction,
                tissue: tissue, heightY: point.Y, parent: null,
                patch: patch, adultPhenotype: adult,
                adultTissue: Metabolism.TissueJoules(adult, Config));

            // Where it stands until the solver first reports it.
            creature.X = point.X;
            creature.Z = point.Z;

            Placement?.Commit(creature.Id);
            Inoculated++;
            _living.Add(creature);
            return creature;
        }
    }
}
