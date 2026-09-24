using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// The run report — <c>runs/&lt;arm&gt;.md</c>: the title, the settings header, the table and
    /// the footer, as <c>EvolutionRun</c> writes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The format is a contract with the scripts.</b> <c>analyse-arm.ps1</c> builds its
    /// name-to-index map from the table's own header row; <c>parse-arm.ps1</c> splits the settings
    /// line on <c>" · "</c> and matches the footer's pace line by a regular expression;
    /// <c>pace-survey.py</c> matches the same line; <c>watch-round.py</c> reads the first two cells
    /// of any row starting <c>| &lt;digit&gt;</c> and looks for <c>**Ended</c>. So every token, every
    /// separator and every rounding below is transcribed rather than improved, and the pace line
    /// is byte-for-byte what every footer on file already says.
    /// </para>
    /// <para>
    /// <b>What the port changes.</b> Two tokens on the settings line, both in the slot the Unity
    /// build's stood in so the line's append-only order is untouched: <c>Unity 6000.5.6f1</c>
    /// becomes <c>engine=dynamics &lt;version&gt;</c>, and <c>physics jobs N</c> becomes
    /// <c>threads N</c> — the first because there is no editor, the second because a thread count
    /// here is a pace setting and not, as the job-worker count was, the difference between a
    /// recording that replays and one that cannot.
    /// </para>
    /// </remarks>
    public sealed class Report
    {
        private readonly string _path;
        private readonly StringBuilder _text = new StringBuilder();

        /// <summary>The table's columns: the base set plus one per patch.</summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// D117's column, the window's pool founders (a share of <c>trickle</c>'s), bold as its
        /// neighbours are; present only when the config names a pool.
        /// </summary>
        public const string PoolColumn = "**pool**";

        /// <summary>
        /// Fixes the table's shape for this run — <c>ConfigureColumns</c>.
        /// </summary>
        /// <remarks>
        /// The per-patch columns are appended for every run, K = 1 included, so the shape depends
        /// on exactly one setting and a reader never has to work out whether a missing <c>p0</c>
        /// means one patch or a report written before D077.
        /// </remarks>
        public Report(string path, RunConfig config)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            if (config == null) throw new ArgumentNullException(nameof(config));

            int patches = Math.Max(1, (int)config.HorizontalPatches);

            // D117's `pool`, after the last base column and before the patches, and only in a run whose
            // config names a pool: every other run's table is the trickle build's to the byte.
            int pool = config.FoundingTricklePoolCount > 0 ? 1 : 0;
            var columns = new string[BaseColumns.Length + pool + patches];

            Array.Copy(BaseColumns, columns, BaseColumns.Length);
            if (pool > 0) columns[BaseColumns.Length] = PoolColumn;

            for (int p = 0; p < patches; p++)
            {
                columns[BaseColumns.Length + pool + p] = "p" + p.ToString(CultureInfo.InvariantCulture);
            }

            Columns = columns;
        }

        /// <summary>Everything written so far, which is what <see cref="Flush"/> puts on disk.</summary>
        public string Text => _text.ToString();

        /// <summary>The title, the settings header and the table's header row.</summary>
        public void Begin(EnvSettings s, RunConfig config, SpaceFacts space, string engineVersion)
        {
            _text.AppendLine("# Evolution run — " + s.Irradiance.ToString("0", Inv) + " W/m2");
            _text.AppendLine();
            _text.AppendLine(HeaderLine(s, config, space, engineVersion));
            _text.AppendLine();
            _text.AppendLine(TableHeader());
        }

        /// <summary>One markdown row, already built — the loop's own <c>Row</c> (work package G).</summary>
        public void AppendRow(string row) => _text.AppendLine(row);

        /// <summary>One markdown row from its cells, in <see cref="Columns"/>' order.</summary>
        public void AppendRow(IReadOnlyList<string> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Count != Columns.Count)
            {
                throw new ArgumentException(
                    "The row has " + cells.Count + " cells and the table has " + Columns.Count +
                    " columns. A row that does not match its header is a positional misread " +
                    "waiting to happen (logbook/0044).", nameof(cells));
            }

            var row = new StringBuilder("| ");
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) row.Append(" | ");
                row.Append(cells[i]);
            }

            _text.AppendLine(row.Append(" |").ToString());
        }

        /// <summary>Appends a free line — the footer's paragraphs, or a blank.</summary>
        public void AppendLine(string line = "") => _text.AppendLine(line);

        /// <summary>
        /// Rewrites the whole report.
        /// </summary>
        /// <remarks>
        /// A locked output file must not take the run down with it — the run is the expensive part
        /// and the numbers are still in the log.
        /// </remarks>
        public void Flush()
        {
            try
            {
                File.WriteAllText(_path, _text.ToString());
            }
            catch (IOException)
            {
            }
        }

        /// <summary>The table's header row and its separator.</summary>
        public string TableHeader()
        {
            var names = new string[Columns.Count];
            for (int i = 0; i < Columns.Count; i++) names[i] = Columns[i];

            return
                "| " + string.Join(" | ", names) + " |" + Environment.NewLine +
                "|" + string.Concat(System.Linq.Enumerable.Repeat("---|", names.Length)) + "|";
        }

        // ------------------------------------------------------------------ the settings line

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string F(float v) => v.ToString(Inv);
        private static string F(double v) => v.ToString(Inv);
        private static string F(int v) => v.ToString(Inv);
        private static string F(long v) => v.ToString(Inv);

        /// <summary>
        /// The settings line: every knob this run was launched with, in one string.
        /// </summary>
        /// <remarks>
        /// Every token is rendered unconditionally for D065's reason — a reader must never have to
        /// work out whether a missing token means "off" or "written before the knob existed" — and
        /// nothing is ever inserted in the middle: a new knob goes on the end. The comments in
        /// <c>EvolutionRun</c> say why each token exists and are not repeated here; what is
        /// repeated is the text, character for character.
        /// </remarks>
        public static string HeaderLine(
            EnvSettings s, RunConfig config, SpaceFacts space, string engineVersion)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (config == null) throw new ArgumentNullException(nameof(config));

            float physicsDt = config.PhysicsStepSeconds;
            int stepsPerMetabolic = (int)Math.Round(EnvBinding.MetabolicStepSeconds / physicsDt);
            float metabolicStep = stepsPerMetabolic * physicsDt;

            return
                "engine=" + RunManifest.EngineName + " " + (engineVersion ?? "unknown") +
                " · dt=" + F(physicsDt) +
                " · metabolic step " + F(metabolicStep) +
                " s · seed " + s.Seed.ToString(Inv) + " · idle " + F(s.Idle) + " W/N·m · power " +
                F(s.MinPower) + "-" + F(s.MaxPower) +
                " · light reach " + F(s.LightReach) + " m" +
                " · silhouette " + (s.SilhouetteCap ? "on" : "off") +
                // D110, beside the cap it shares the shadow with, as the spec places it.
                " · light " + (s.LightByExposure ? "by exposure" : "averaged") +
                // D111, beside the light it turns the leaves towards.
                " · buoyancy offset " + (s.BuoyancyOffsetCost > 0f
                    ? s.BuoyancyOffsetCost.ToString("0.####", Inv) + " W/m3"
                    : "off") +
                " · selfOverlap " + (s.SelfOverlap > 0f ? s.SelfOverlap.ToString("0.###", Inv) : "off") +
                " · reach " + (s.MaxReach > 0f ? s.MaxReach.ToString("0.##", Inv) + " m" : "off") +
                // The owner's ruling of 2026-09-24: what the volume and newborn floors weigh.
                " · floors " + (s.RigidFloors ? "rigid groups" : "per part") +

                // D109's three tokens, all reading the recorded world at their defaults.
                " · matter " + (s.MatterIslands > 0f
                    ? "islands " + s.MatterIslands.ToString("0.#", Inv) + " m cover " + s.MatterIslandCover.ToString("0.###", Inv) +
                      (s.MatterIslandDepth > 0f ? " to " + s.MatterIslandDepth.ToString("0.#", Inv) + " m" : " to bed")
                    : "uniform") +
                // D116 takes the token's own slot: one founder rule or the other, never both.
                " · founders " + (s.FoundersFollowMatter
                    ? "in matter"
                    : s.FoundersFollowFood
                        ? s.FoundersFollowFoodDepth ? "in their food at its depth" : "in their food"
                        : "anywhere") +
                // The round 48 founding ruling's endowment, beside the rule that places the
                // founders it is given to; rendered off as well as on, as the trickle is.
                " · endowment " + (s.FounderEndowment > 0f ? F(s.FounderEndowment) + " s" : "off") +
                " · shade " + (s.LightShade > 0f
                    ? s.LightShade.ToString("0.##", Inv) + " drift " + s.LightShadeDrift.ToString("0.#", Inv) + " m/h"
                    : "off") +
                " · day ±" + F(s.DayAmplitude) + " over " + F(s.DayLength) + " s" +
                " · current " + F(s.CurrentSpeed) + " m/s " +
                s.CurrentMode.ToString().ToLowerInvariant() +
                " over " + F(s.CurrentPeriod) + " s" +
                (s.CurrentMode == CurrentMode.Transport
                    ? " (cell " + F(s.CurrentCell) + " m unread)"
                    : " in " + F(s.CurrentCell) + " m cells") +

                // D102, in the slot EvolutionRun prints it in: the vertical-to-horizontal ratio
                // the tank's streams were built to, beside the current because it says what kind
                // of water the speed is the RMS of. Derived from the depth and the radius, so
                // there is no tunable behind it and no recorded config is refused, and read off
                // the field the world was built with for SpaceToken's reason — a header must not
                // name water the simulation does not have. 1.00 in a box and in every tank whose
                // axes balance.
                " · axes v:h " + config.Current.StreamsAxisRatio.ToString("0.00", Inv) +
                " · rolls " + (s.CurrentMode == CurrentMode.Transport
                    ? "unread in transport"
                    : s.CurrentRolls
                        ? s.CurrentBlink > 0f ? "blink " + F(s.CurrentBlink) + " s" : "steady"
                        : "off") +
                " · advect " + (s.CurrentAdvect ? "on" : "off") +
                " · vent " + (s.Vent > 0f
                    ? F(s.Vent) + " m/s in patch " + F((int)s.VentPatch) + " from " + F(s.VentDepth) +
                      " m, legs " + F(s.VentLeg) + " m"
                    : "off") +
                " · mixing " + F(s.Mixing) + " m2/s" +
                " · sink " + F(s.NutrientSink) + " m/s, matter " + F(s.MatterSink) + " m/s" +
                " · economy rho " + F(s.Rho) + " J/unit, uptake " + F(s.UptakeRate) +
                " /m2/s at K " + F(s.UptakeHalf) + " /m3, remin " + F(s.Remin) + " /s, handling " +
                F(s.Handling) + ", reserveCap " + (s.ReserveCap > 0f ? F(s.ReserveCap) + " s" : "off") +
                ", margin " + F(config.Genome.MinReserveMargin) + "-" +
                F(config.Genome.MaxReserveMargin) + " s at " + F(s.MarginChance) +
                " · refuge " + F(s.FloorRefuge) + " m" +
                (s.RefugeFraction > 0f ? " at " + F(s.RefugeFraction) + " edible" : "") +
                (s.Satiation > 0f ? " · satiation " + F(s.Satiation) + " W/m3" : "") +
                (s.ClearanceToe > 0f ? " · toe " + F(s.ClearanceToe) + " J/m3" : "") +
                (s.Exudation > 0f ? " · exudation " + F(s.Exudation) : "") +
                " · conception " + s.ConceptionOrder.ToString().ToLowerInvariant() +
                " · matter in " + F(s.MatterInflux) + "/s at " +
                s.MatterInfluxAt.ToString().ToLowerInvariant() +
                ", burial " + F(s.MatterBurial) + "/s" +
                " · speciesTheta " + F(s.SpeciesTheta) +
                (s.Patches > 1f
                    ? " · patches " + F((int)s.Patches) + ", h-mix " + F(s.HorizontalMixing) + " m2/s, " +
                      "disperse " + F(s.DispersalChance) + ", patchShade " + F(s.PatchShading)
                    : "") +
                (s.PatchesAcross > 1f ? " · across " + F((int)s.PatchesAcross) : "") +
                " · matter-mix " + F(s.MatterMixing) + " m2/s" +
                " · area " + F(s.Area) + " m2" +
                " · space " + SpaceToken(config, space) +
                " dispersal=" + F(s.OffspringDispersal) + " m" +
                " · surface restore " + F(s.SurfaceRestore) +
                (s.FloorCloses > 0f ? " · floor closes " + F(s.FloorCloses) + " s" : " · floor open") +

                // D115, beside the floor it follows, and rendered off as well as on so a reader
                // never has to ask whether a missing token means off or an older build.
                " · trickle " + TrickleToken(s.Trickle) +

                // D117, after the trickle it is a share of, and only when a pool is named (the
                // count is the config's, set from the files), so every earlier header is unchanged:
                // `pool 0.1 of 3`, or `pool 0 of 3` for a pool named and unused.
                PoolToken(config) +
                " · ceiling " + F(s.MaxPopulation) +
                " maxTissue=" + s.MaxTissue.ToString("0.#", Inv) +
                // The round 48 ruling says which sides the wear takes: D038's two, or upkeep alone.
                " · senescence " + (s.Senescence > 0f
                    ? F(s.Senescence) + " s on " + (s.SenescenceWearsIntake ? "upkeep and intake" : "upkeep")
                    : "off") +
                " · cellType mut " + F(s.CellTypeMutation) +
                " · clearance " + F(s.Clearance) +
                " · linkPhoto " + F(s.LinkPhoto) +
                " · excessDensity " + F(s.ExcessDensity) + " kg/m3" +
                " · neutralV " + F(s.NeutralVolume) + " m3" +
                " · founderDepth " + F(s.FounderDepth) + " m" +
                " · matter from " + F(s.InitialMatter) + "/m3" +
                " · tissue " + config.CellTypes.Resolve(CellTypeIds.Photosynthetic)
                    .TissueEnergyPerCubicMetre.ToString("0.###", Inv) + " J/m3" +
                " · overhead " + config.PerOffspringOverheadJoules.ToString("0.###", Inv) + " J" +
                " · founders " + config.Genome.MinHalfExtent.ToString("0.###", Inv) + "-" +
                config.Genome.MaxHalfExtent.ToString("0.###", Inv) + " m" +
                " · float " + F(s.FloatChance) + " at " + F(s.LiftCost) + " W/lift" +
                " · senses " + SensesToken(config) +
                " · sense scale chem " + F(s.ChemicalHalfScale) + " J/m3, energy " + F(s.EnergyFullScale) +
                " s, flow " + F(s.FlowFullScale) + " m/s" +
                (space.InoculumHashShort != null
                    ? " · inoculate " + F(s.InoculateCount) + " @ " + F(s.InoculateAt) + " s, " +
                      F(s.InoculateDepth) + " m, genome " + space.InoculumHashShort
                    : "") +

                // Where `physics jobs` stood. A pace setting, in the slot a pace setting had.
                " · threads " + F(space.Threads) +
                " · driveLimit " + (s.DriveLimitAlways ? "always" : ">0.01") +
                " · matterBudget " + F(s.MatterBudget) +
                " · addedMass " + F(s.AddedMass) +
                " · fluidAccel " + F(s.FluidAccel) +
                " · " + (s.WaterHold > 0f ? "water held " + F(s.WaterHold) + " s" : "water per link") +
                " · neuron " + F(s.NeuronCost) + " W + " + F(s.ConnectionCost) + " W/input, work x" + F(s.WorkCost) +
                " · field " + s.FieldModel.ToString().ToLowerInvariant() +
                " h=" + F(s.FieldKernel) + " mh=" + F(s.FieldMatterKernel) + " merge=" + F(s.FieldMerge) +
                " cap=" + F(s.FieldCap) + " q=" + F(s.FieldQuantum) +
                " cell=" + F(s.FieldCell) + " mcell=" + F(s.FieldMatterCell) +
                " corpse=" + F(s.CorpseDecay) + "/s" +
                " · growth reserve=" + F(s.NewbornReserve) + " floor=" + F(s.GrowthFloor) +
                " minkg=" + F(s.MinNewbornKg) + " step=" + F(s.GrowthStep) +
                " invest=" + F(config.Genome.MinBirthInvestment) + "-" + F(config.Genome.MaxBirthInvestment) +
                " scale/invest chance=" + F(s.AdultScaleChance) + "/" + F(s.InvestChance) +

                // D106 item 2, at the end, which is where a new knob goes: nothing already
                // written ever moves. Every token is rendered whatever the values are, so a
                // reader is never left working out whether a missing one means "off" or "written
                // before the gene existed".
                " · modules add=" + F(s.ModuleAdd) + " drop=" + F(s.ModuleDrop) +
                " after=" + F(s.ModuleDropAfter) + " mut=" + F(s.ModuleMutation) +

                // D106 items 1, 3 and 4, after the module gene and before the hash, which is
                // where the end of the header is. Every token is rendered whatever the values
                // are, for the module token's reason: a reader must never have to work out
                // whether a missing one means "off" or "written before the mouth existed".
                " · mouth hp=" + F(s.Health) + " heal=" + F(s.Healing) + "/s@" + F(s.HealingCost) +
                "J reach=" + F(s.IntakeReach) + " waste=" + F(s.IntakeWaste) +
                " prices atk=" + F(s.PriceAttack) + " ink=" + F(s.PriceIntake) +
                " prt=" + F(s.PriceProtection) + " tgh=" + F(s.PriceToughness) +
                " mut=" + F(s.AttributeMutation) +

                // D113, beside the four attribute prices it is billed with and at the end of the
                // line, which is where a new knob goes. Rendered at 0 too, as `off`.
                " · support " + (s.Support > 0f
                    ? s.Support.ToString("0.####", Inv) + " W/m2/m2"
                    : "off") +
                // D114, after the mouth it hands its parts to and before the hash, the end of the
                // header being where a new knob goes. Rendered either way: the overlap columns
                // count a different thing under each model, and analyse-arm.ps1 and
                // contact_aliases.py read which from this token.
                " · contact " + (config.ContactPerPart ? "per part" : "per body") +

                // The ruling of 2026-09-24, at the end before the hash and rendered either way.
                // The floor is the `overhead` token above, which is unchanged; this says whether
                // the overhead scales with the child (`x0` is the flat fee of every recorded
                // world) and whether any lineage can pay as it goes.
                " · overhead scale x" + F(config.PerOffspringOverheadPerTissueJoule) +
                " floor " + config.PerOffspringOverheadJoules.ToString("0.###", Inv) + " J" +
                " · gestation " + (config.Mutation.GestationModeChance > 0f
                    ? "mut=" + F(config.Mutation.GestationModeChance) +
                      " share=" + F(config.Genome.MinGestationShare) + "-" +
                      F(config.Genome.MaxGestationShare) +
                      " at " + F(config.Mutation.GestationShareChance)
                    : "off") +
                " · configHash `" + config.Hash() + "`";
        }

        /// <summary>
        /// The <c>space</c> token: which container this run was in, in one string.
        /// </summary>
        /// <remarks>
        /// Built from the world the run constructed rather than from the environment it was
        /// launched with — the patch width is the fields' own <c>sqrt(area / K)</c> and the radius
        /// is the one the world derived — so the header cannot describe a box the simulation does
        /// not have. The glass and the bed are read from what the engine built, for the same
        /// reason: a header that inferred them from the shape would still say "wall" on the day
        /// something stops building one.
        /// </remarks>
        public static string SpaceToken(RunConfig config, SpaceFacts space)
        {
            if (!config.SharedSpace) return "tiled " + F(space.TileSpacingMetres) + " m";

            if (config.WorldShape == WorldShape.Tank)
            {
                return
                    "tank r=" + space.TankRadiusMetres.ToString("0.##", Inv) +
                    " m (" + config.WorldAreaSquareMetres.ToString("0.###", Inv) +
                    " m2), depth " + F(config.WorldDepthMetres) + ", " +
                    (space.HasWall ? "wall" : "no wall") + ", " +
                    (space.HasFloor ? BedToken(space.Bed) : "no bed") +
                    // The reefs after the bed and its shore (logbook/specs/reef-spec.md §1): the
                    // dials as the config holds them, or "no reef".
                    ", " + ReefGeometry.HeaderToken(config, space.Reefs);
            }


            string width = space.PatchWidthMetres.ToString("0.###", Inv);
            int patchesAcross = space.PatchesAcross;
            int patchesAlong = space.PatchCount / Math.Max(1, patchesAcross);

            // One row of patches prints the form every recording carries ("4x5x5 m"); only a
            // second row prints the count on each axis (tank-spec.md's invariant).
            string layout = patchesAcross > 1
                ? patchesAlong + "x" + patchesAcross + "x" + width
                : patchesAlong + "x" + width + "x" + width;

            return
                "shared " + layout + " m, depth " + F(config.WorldDepthMetres) + ", wrap, " +
                (space.HasFloor ? "bed" : "no bed");
        }

        /// <summary>
        /// D117's header token with its separator — <c> · pool 0.1 of 3</c> — or nothing when the
        /// config names no pool.
        /// </summary>
        public static string PoolToken(RunConfig config)
        {
            if (config.FoundingTricklePoolCount <= 0) return string.Empty;

            return " · pool " + F(config.FoundingTricklePoolShare) + " of " +
                config.FoundingTricklePoolCount.ToString(Inv);
        }

        /// <summary>
        /// D115's rate as the header prints it: <c>off</c> at 0, <c>1/N s</c> when the rate is
        /// one every whole N seconds, and a number per second otherwise (<c>0.05/s</c>).
        /// </summary>
        /// <remarks>
        /// The reciprocal is recognised to a part in ten thousand, because <c>1/30</c> reaches the
        /// config as the float nearest a third of a tenth and its reciprocal is 29.999998, not 30.
        /// </remarks>
        public static string TrickleToken(float rate)
        {
            if (!(rate > 0f)) return "off";

            double every = 1d / rate;
            double whole = Math.Round(every);

            if (whole >= 1d && Math.Abs(every - whole) <= 1e-4d * whole)
            {
                return "1/" + whole.ToString("0", Inv) + " s";
            }

            return rate.ToString("0.######", Inv) + "/s";
        }

        /// <summary>
        /// The bed's half of the space token: <c>bed</c> on a flat floor and the whole shape on a
        /// shaped one.
        /// </summary>
        /// <remarks>
        /// The flat word is unchanged to the character, which is the invariant every header in the
        /// record is compared against.
        /// </remarks>
        public static string BedToken(BedShape bed)
        {
            if (bed == null || !bed.HasRelief) return "bed";

            return
                "bed relief " + bed.ReliefMetres.ToString("0.##", Inv) +
                " m tilt " + bed.TiltMetres.ToString("0.##", Inv) +
                // The beach's two, after the tilt and only when on, so that every recorded
                // header's bed token reads as it did (logbook/specs/beach-spec.md §2).
                (bed.HasShore
                    ? " m shore " + bed.ShoreDepthMetres.ToString("0.##", Inv) +
                      " m fade " + bed.ShoreFadeMetres.ToString("0.##", Inv)
                    : "") +
                " m scale " + bed.ScaleMetres.ToString("0.##", Inv) +
                " m (hollows " + bed.Hollows.ToString(Inv) +
                ", ridges " + bed.Ridges.ToString(Inv) +
                ", range " + bed.RangeMetres.ToString("0.00", Inv) +
                " m, steepest " + (bed.SteepestTotalSlopeRadians * 180d / Math.PI).ToString("0", Inv) +
                "° bands " + (bed.SteepestSlopeRadians * 180d / Math.PI).ToString("0", Inv) +
                "°, bound " + (bed.SlopeBoundBinds ? "binds" : "clear") +
                // The shoal and the shelf by band, read from the map on the half-metre columns
                // rather than from the spec's arithmetic of the plane; only with the shore on.
                (bed.HasShore
                    ? ", shoal " + (100d * bed.ShoalAreaFraction).ToString("0.00", Inv) +
                      "%, shelf 6/12/24 m " +
                      (100d * bed.ShelfWithin6Fraction).ToString("0.0", Inv) + "/" +
                      (100d * bed.ShelfWithin12Fraction).ToString("0.0", Inv) + "/" +
                      (100d * bed.ShelfWithin24Fraction).ToString("0.0", Inv) + "%"
                    : "") +
                ")";
        }

        /// <summary>
        /// The run's sensor pool as the header prints it, in the order a draw walks them.
        /// </summary>
        /// <remarks>
        /// Named per channel rather than by <c>ToString()</c> so the header's vocabulary is a
        /// decision rather than a consequence of an enum member's spelling. Read from the pool
        /// itself: the token and the draw cannot disagree, because there is only one list.
        /// </remarks>
        public static string SensesToken(RunConfig config)
        {
            SensorChannel[] pool = config.SensorPool();
            var names = new string[pool.Length];

            for (int i = 0; i < pool.Length; i++)
            {
                switch (pool[i])
                {
                    case SensorChannel.JointAngle: names[i] = "jointangle"; break;
                    case SensorChannel.JointAngularVelocity: names[i] = "jointrate"; break;
                    case SensorChannel.OrientationUp: names[i] = "up"; break;
                    case SensorChannel.Depth: names[i] = "depth"; break;
                    case SensorChannel.Chemical: names[i] = "chemical"; break;
                    case SensorChannel.Energy: names[i] = "energy"; break;
                    case SensorChannel.Flow: names[i] = "flow"; break;

                    // D106 item 5's two, named here rather than left to the default case so the
                    // token reads in the same lower-case shorthand as the rest of it.
                    case SensorChannel.Contact: names[i] = "contact"; break;
                    case SensorChannel.Damage: names[i] = "damage"; break;
                    default: names[i] = pool[i].ToString().ToLowerInvariant(); break;
                }
            }

            return string.Join(",", names);
        }

        // ------------------------------------------------------------------ the footer

        /// <summary>
        /// One bucket of the timing split as a whole percentage of the run's own clock.
        /// </summary>
        /// <remarks>
        /// Whole percentages, because the footer's job is to say where a second went and a reader
        /// who wants the millisecond has it in <c>run.json</c> and in every <c>stats.jsonl</c> row.
        /// The denominator is floored at one so a run that ends in its first millisecond prints
        /// zeros rather than dividing by nothing.
        /// </remarks>
        public static string WallShare(long ms, long totalMs) =>
            (100d * ms / Math.Max(1L, totalMs)).ToString("0", Inv) + "%";

        /// <summary>
        /// Everything after the last row: how it ended, the two stabilisers, the space line, the
        /// pace line and the timing split, and the fastest creature.
        /// </summary>
        /// <remarks>
        /// <b>The space line says three things differently from the recorded footers, because it
        /// is reporting three different measurements.</b> The pair count is this engine's overlap
        /// census and not PhysX's contact census, so it is named for what it counts — the same
        /// rule that renamed four of the table's columns. The bed is a heightfield the solver
        /// tests against analytically and not a mesh collider, and the lowest rock is the lowest
        /// point on the disc rather than the lowest vertex of a mesh that carries on past the rim,
        /// which is why the same world reads about eight metres shallower here. And the last
        /// figure counts bodies touching the bed or the glass, not pairs of colliders. No script
        /// on file parses this line; three parse the pace line below, which is byte-for-byte what
        /// every footer on file says.
        /// </remarks>
        /// <param name="ending">The prose — "budget reached", "wall clock reached", a RUNAWAY.</param>
        /// <param name="wallClockMs">The run's own clock, which every share is taken against.</param>
        /// <param name="writersMs">What this file's writers cost — the loop's own stopwatch.</param>
        public void Footer(
            RunConfig config, IHarnessReadings h, string ending,
            long wallClockMs, long writersMs, double bestSpeed, double bestSpeedAtSeconds)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (h == null) throw new ArgumentNullException(nameof(h));

            double wallMinutes = wallClockMs / 60000d;
            double wallSeconds = wallClockMs / 1000d;

            _text.AppendLine();
            _text.AppendLine("**Ended:** " + ending + ".");
            _text.AppendLine();
            _text.AppendLine(
                "Drag impulses limited: " + F(h.DragImpulsesLimited) +
                " (the coarse-step stabiliser; 0 means every step's drag was applied as computed)");
            _text.AppendLine();
            _text.AppendLine(
                "Drive impulses limited: " + F(h.DriveImpulsesLimited) +
                " (the joint-torque cap; 0 means every drive torque was applied as computed)");
            _text.AppendLine();
            _text.AppendLine(
                "Shared space: " + (config.SharedSpace ? "on" : "off") +
                " · wraps " + F(h.Wraps) +
                " · crowded stillbirths " + F(h.Crowded) +
                " · overlap pairs per physics step " +
                (h.HasSharedVolume && h.PhysicsSteps > 0
                    ? (h.ContactPairs / (double)h.PhysicsSteps).ToString("0.####", Inv)
                    : "—") +
                " · sea bed " + (h.HasFloor
                    ? h.FloorHasRelief
                        ? "relief, lowest rock on the disc " +
                          h.FloorLowestTopY.ToString("0.##", Inv) + " m"
                        : "plane at -" + F(config.WorldDepthMetres) + " m"
                    : "none") +
                " · bed or glass bodies per physics step " +
                (h.HasFloor && h.PhysicsSteps > 0
                    ? (h.FloorContactPairs / (double)h.PhysicsSteps).ToString("0.####", Inv)
                    : "—"));
            _text.AppendLine();

            // The pace line. Byte-for-byte what every footer on file says: three scripts match it
            // by regular expression and one of them, parse-arm.ps1, reads the simulated seconds as
            // digits and commas only.
            _text.AppendLine(
                F(h.PhysicsSteps) + " physics steps · " +
                h.ElapsedSeconds.ToString("0.#", Inv) + " simulated seconds · " +
                F(h.Births) + " births · " +
                wallMinutes.ToString("0.#", Inv) + " min wall clock (" +
                (h.ElapsedSeconds / Math.Max(1e-9, wallSeconds)).ToString("0.#", Inv) +
                "x real time).");

            // The timing split, on its own line inside the same paragraph so that the pace line
            // above stays what it was. `other` is the remainder against the run's own clock: the
            // world's construction and the founding draw before the loop, and everything written
            // after the clock stopped.
            long wallOtherMs = Math.Max(0L,
                wallClockMs - h.WallPhysicsMs - h.WallWorldMs - h.WallHarnessMs - writersMs);

            _text.AppendLine(
                "wall split: physics " + WallShare(h.WallPhysicsMs, wallClockMs) +
                ", world " + WallShare(h.WallWorldMs, wallClockMs) +
                ", harness " + WallShare(h.WallHarnessMs, wallClockMs) +
                ", writers " + WallShare(writersMs, wallClockMs) +
                ", other " + WallShare(wallOtherMs, wallClockMs));

            IReadOnlyList<string> phases = h.HarnessPhases;
            IReadOnlyList<long> phaseMs = h.HarnessPhaseMs;
            var harnessSplit = new StringBuilder("harness split: ");

            for (int p = 0; p < phaseMs.Count && p < phases.Count; p++)
            {
                if (p > 0) harnessSplit.Append(", ");
                harnessSplit.Append(phases[p]).Append(' ')
                    .Append(WallShare(phaseMs[p], h.WallHarnessMs));
            }

            _text.AppendLine(harnessSplit.ToString());

            // Round 46's K10, on a line of its own so the split above keeps its sum and its
            // bytes. Each is a share of the bucket it sits inside. Printed only when either
            // timer read something, so a footer from a build without them (round 42's, which
            // the tests hold this one to byte for byte) is still that footer.
            if (h.WallExposureMs > 0L || h.WallLedgerMs > 0L)
            {
                long metaboliseMs = 0L;
                for (int p = 0; p < phaseMs.Count && p < phases.Count; p++)
                {
                    if (phases[p] == "metabolise") metaboliseMs = phaseMs[p];
                }

                _text.AppendLine(
                    "inner timers: exposure " + h.WallExposureMs.ToString(Inv) + " ms (" +
                    WallShare(h.WallExposureMs, metaboliseMs) + " of metabolise), ledger " +
                    h.WallLedgerMs.ToString(Inv) + " ms (" +
                    WallShare(h.WallLedgerMs, h.WallWorldMs) +
                    " of world; the bill whose part walk holds the support term)");
            }

            long fluidMs = h.WallFluidGatherMs + h.WallFluidWaterMs +
                           h.WallFluidComputeMs + h.WallFluidApplyMs;

            _text.AppendLine(
                "fluid split: gather " + WallShare(h.WallFluidGatherMs, fluidMs) +
                ", water " + WallShare(h.WallFluidWaterMs, fluidMs) +
                ", compute " + WallShare(h.WallFluidComputeMs, fluidMs) +
                ", apply " + WallShare(h.WallFluidApplyMs, fluidMs));

            _text.AppendLine(
                "fluid per link-step: " +
                h.FluidMicrosecondsPerLinkStep.ToString("0.#", Inv) +
                " µs (" + h.FluidLinkSteps.ToString("N0", Inv) + " link-steps).");

            _text.AppendLine(
                "harness per body-step: " +
                h.HarnessMicrosecondsPerBodyStep.ToString("0.#", Inv) +
                " µs (" + h.HarnessBodySteps.ToString("N0", Inv) + " body-steps).");

            _text.AppendLine();
            _text.AppendLine(
                "**Fastest creature seen at any point: " + bestSpeed.ToString("0.####", Inv) +
                " m/s, at t=" + bestSpeedAtSeconds.ToString("0.#", Inv) + " s.**");
        }

        /// <summary>The last line of a report that had a run directory to write creatures into.</summary>
        public void GenomesLine(string runDirectoryPath)
        {
            _text.AppendLine();
            _text.AppendLine("Genomes: `" + runDirectoryPath + "`");
        }

        // ------------------------------------------------------------------ the columns

        /// <summary>
        /// Column headers. The single source of the table's shape.
        /// </summary>
        /// <remarks>
        /// Transcribed from <c>EvolutionRun.BaseColumns</c> in order, and appended to in the same
        /// way: everything is added at the end and nothing already written ever moves, so a reader
        /// that indexes by position keeps working — though <c>analyse-arm.ps1</c> reads by name,
        /// because the count depends on the config (CLAUDE.md). The comments that say why each
        /// group exists are in <c>EvolutionRun</c> and in the specs; what matters here is that the
        /// order is identical.
        /// </remarks>
        public static readonly string[] BaseColumns =
        {
            "t (s)", "alive", "births", "deaths", "**jointed**", "jointed %", "**jnt inh**", "mean dof",
            "mean m/s", "max m/s", "work J/s", "work share", "**food %**", "**absorpt**", "**inherit**",
            "**detritus J**", "**J/m3 here**", "**% on floor**", "**det deep**", "depth m", "**depth sd**",
            "**rise m**", "age s", "sun", "**shade %**",
            "**float**", "**flt inh**", "lift", "**flt m**",
            "mat top", "mat deep", "**upt lim**", "**floor**", "gen min", "gen max", "audit",
            "species",
            "det here ed", "below world", "abs below", "mat locked", "refuge J", "remin",
            "det patch sd", "patch max share",
            "det in", "det out",
            "det exuded",
            "abs logged",
            "**photo**", "**photo inh**",
            "diverged",
            "mat in", "mat buried",
            "**spd jnt**", "**spd rig**", "**food jnt**", "**food rig**",
            // The four that changed their name because they changed their census
            // (fable-propose-own-solver.md's change 4, and <see cref="Sampler"/>'s remarks).
            // `contacts`, `pairs/body`, `pairs jnt %` and `stuck %` counted PhysX's contact
            // manifolds between colliders; these count overlapping bounding spheres between
            // creatures, one per pair, and a body cannot overlap itself. The places are the same
            // four so nothing after them moves, and the names are different so that a reader
            // comparing a row of this engine's against a row of the other one's is stopped rather
            // than handed a different measurement under the old name.
            "above", "wraps", "crowded", "overlaps",
            "ovl/body", "ovl jnt %", "ovl held %",

            // The bed and the glass, apart from the crowd's, in `floor con`'s own place — bodies
            // resting against the world rather than manifolds, for the reason above.
            "floor con",
            "stillb",
            "**sense**", "dep jnt", "dep rig", "mat here",
            "vtx",
            "**mat resid**", "burnt",
            "corpses",
            "adult scale", "invest", "brood",
            "margin s",
            "body frac",
            "cols", "cols abs", "x sd",
            "det cv", "mat cv",
            "floor low %", "floor J",
            "self stillb",

            // D106 item 2, rule 8, appended at the end in the order the spec states them:
            // the standing count, the window's three events, and the share of the living
            // carrying the gene at all. `modules` is a state and the three between are windows
            // — scripts/reads/r44-read.py reads them from stats.jsonl that way.
            "modules", "mod add", "mod drop", "mod refused", "indet %",

            // D106 items 1, 3 and 4, rule 8, appended after them and in the spec's own order:
            // three shares of the living carrying an attribute, then the window's kills, the
            // window's bodies eaten and the window's corpses emptied, then the window's repair
            // bill. The three shares are states and the four after them are windows —
            // stats.jsonl carries the cumulative totals a reader differences.
            "attack %", "intake %", "prot %",
            "killed", "eaten", "corpse eat", "heal J",

            // The module rule's refusals by reason, per window, appended after round 44's read
            // (logbook/0113): the shape test (no larger, cut by a limit, or folded under D101)
            // and the reserve short of the tissue. They sum to `mod refused`.
            "ref shape", "ref reserve",

            // D110, light by exposure: the leaves' area-weighted exposure factor, 1 for a crowd of
            // random poses and 2 for every leaf flat; a dash with the tunable off.
            "expo",

            // D111, the buoyancy offset: the leaves' area-weighted |offset|, 0 at founding and 1
            // at the range's end; a dash with the price at 0.
            "float off",

            // D113, the support cost: the living bodies' summed support watts, a dash with the
            // price at 0, and the area-weighted mean distance of a part from its root, the reach
            // the price bounds, read at any price.
            "support W", "reach m",

            // D115, the founding trickle: the window's trickle founders, every attempt counted as
            // `floor` counts the floor's. The spec places it beside `floor`; it is appended here
            // instead, because nothing already written ever moves and `analyse-arm.ps1` reads by
            // name, so a reader asking for both gets them side by side all the same.
            "**trickle**",

            // Round 46's K6b: the largest distance of any living part from its root, unweighted,
            // the bound `reach m`'s mean cannot give. It belongs beside `reach m` and is appended
            // here for the trickle's reason.
            "max reach",
        };
    }

    /// <summary>
    /// What the header needs from the world the run built, and from the launch.
    /// </summary>
    /// <remarks>
    /// A struct of facts rather than a reference to the world, because two of them — the glass and
    /// the bed — are things the engine either built or did not, and only the engine knows which.
    /// <see cref="Of"/> reads the rest off Core's <c>World</c>, which is where the patch width and
    /// the tank's radius are derived once and for everyone.
    /// </remarks>
    public struct SpaceFacts
    {
        public float TankRadiusMetres;
        public float PatchWidthMetres;
        public int PatchesAcross;
        public int PatchCount;
        public bool HasWall;
        public bool HasFloor;
        public BedShape Bed;

        /// <summary>The world's reefs, for the header's count and cover got; null with none.</summary>
        public ReefGeometry Reefs;

        public int Threads;
        public string InoculumHashShort;

        /// <summary>The tile spacing of the tiled world — <c>Ecosystem.TileSpacing</c>.</summary>
        public float TileSpacingMetres;

        public static SpaceFacts Of(
            World world, bool hasWall, bool hasFloor, int threads, string inoculumHashShort) =>
            new SpaceFacts
            {
                TankRadiusMetres = world.TankRadiusMetres,
                PatchWidthMetres = world.Nutrients.PatchWidthMetres,
                PatchesAcross = world.Nutrients.PatchesAcross,
                PatchCount = world.Nutrients.PatchCount,
                HasWall = hasWall,
                HasFloor = hasFloor,
                Bed = world.Bed,
                Reefs = world.Reefs,
                Threads = threads,
                InoculumHashShort = inoculumHashShort,
                TileSpacingMetres = 100f,
            };
    }

    /// <summary>
    /// What the report and the manifest need from the loop and the solver — work package G
    /// implements it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here is invented.</b> Every member is a number the Unity harness already keeps
    /// and the footer or the ending block already prints: the two impulse limiters, D077's
    /// shared-space counters, the contact instrument's three totals, the throw trace's two, the
    /// timing split and the harness profile. A value the new engine does not have is not a value
    /// to make up — it is a column or a token that has to be redefined out loud
    /// (fable-propose-own-solver.md's change 4 does exactly that for the contact instrument).
    /// </para>
    /// <para>
    /// The two per-step rates are properties rather than a division done here, because the
    /// harness times in stopwatch ticks and only it can divide before the milliseconds round.
    /// </para>
    /// </remarks>
    public interface IHarnessReadings
    {
        /// <summary>Physics steps taken. Simulated seconds is this times the step.</summary>
        long PhysicsSteps { get; }

        double ElapsedSeconds { get; }
        long Births { get; }
        int Alive { get; }

        /// <summary>Bodies the solver blew up — <c>World.Diverged</c>. 0 is healthy.</summary>
        long Diverged { get; }

        /// <summary>The fastest living body this metabolic step, m/s — the footer's best-ever.</summary>
        double MaxSpeed { get; }

        /// <summary>The coarse-step drag stabiliser's binds.</summary>
        long DragImpulsesLimited { get; }

        /// <summary>The joint-torque cap's binds.</summary>
        long DriveImpulsesLimited { get; }

        /// <summary>D074's two totals, for the ending block.</summary>
        double MatterInfluxedTotal { get; }
        double MatterBuriedTotal { get; }

        /// <summary>Whether there is a shared volume at all: the contact mean is a dash without one.</summary>
        bool HasSharedVolume { get; }

        long Wraps { get; }
        long Crowded { get; }

        /// <summary>Creature-creature pairs, cumulative. A window is two samples differenced.</summary>
        long ContactPairs { get; }
        long ContactPairsJointed { get; }
        long ContactPairsPersistent { get; }
        long ContactBodies { get; }

        /// <summary>Pairs against the bed, cumulative — never summed into the above.</summary>
        long FloorContactPairs { get; }

        bool HasFloor { get; }
        bool FloorHasRelief { get; }

        /// <summary>The deepest rock, m — the footer's "lowest rock" on a shaped bed.</summary>
        double FloorLowestTopY { get; }

        /// <summary>The throw trace's two run-level readings.</summary>
        double MaxJointMassRatio { get; }
        long BodiesOverMassRatio10 { get; }

        /// <summary>The timing split, ms: the solver, the world's step, the rest of the harness.</summary>
        long WallPhysicsMs { get; }
        long WallWorldMs { get; }
        long WallHarnessMs { get; }

        /// <summary>The harness profile: the phase names and their milliseconds, `other` last.</summary>
        IReadOnlyList<string> HarnessPhases { get; }
        IReadOnlyList<long> HarnessPhaseMs { get; }
        long HarnessBodySteps { get; }
        double HarnessMicrosecondsPerBodyStep { get; }

        /// <summary>
        /// Round 46's K10: the exposure pass (inside the harness's <c>metabolise</c>) and the
        /// metabolic bill (inside <c>world</c>, holding D113's support term), ms. Parts of their
        /// buckets, never added to a split.
        /// </summary>
        long WallExposureMs { get; }
        long WallLedgerMs { get; }

        /// <summary>The fluid phase's own four, summing to the harness's `fluid`.</summary>
        long WallFluidGatherMs { get; }
        long WallFluidWaterMs { get; }
        long WallFluidComputeMs { get; }
        long WallFluidApplyMs { get; }
        long FluidLinkSteps { get; }
        double FluidMicrosecondsPerLinkStep { get; }
    }
}
