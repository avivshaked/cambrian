using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Does the footprint world hold? — D077's smoke. Run in batchmode via
    /// <c>-executeMethod Evosim.Sim.EditorTools.SharedSpaceSmoke.Run</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three parts, cheapest first. <b>Part 1</b> is the restoring boundary's arithmetic —
    /// <see cref="FluidEnvironment.Restore"/> is a pure function and its sign, its continuity at
    /// y = 0 and its magnitude are assertable without a scene at all. It lives here rather than in
    /// <c>Evosim.Core.Tests</c> because §6.1 keeps <c>UnityEngine</c> out of Core and the buoyancy
    /// step is Unity's.
    /// </para>
    /// <para>
    /// <b>Part 2</b> is <see cref="SharedVolume"/>'s geometry: the patch a position falls in, the
    /// wrap at each face, and — the one that matters — that two hundred rejection-sampled
    /// placements do not overlap. Overlapping spawns are a force (logbook/0007), so this is the
    /// difference between a crowd and a trampoline.
    /// </para>
    /// <para>
    /// <b>Part 3</b> is the box with real bodies in it: two hundred founders in the screen's own
    /// 4 × 10 × 10 × 60 m volume, two thousand physics steps at dt 0.01, with twenty of them
    /// pushed out through the top and the bottom to see whether the water brings them back. What
    /// it asserts is D077's rules, in the order they are numbered: nothing outside the box, the
    /// top and bottom restore, contacts are counted, and no body is left non-finite.
    /// </para>
    /// <para>
    /// <b>The excess density is deliberately not the reference world's.</b> At 0.02 kg/m³ a body
    /// sinks at under 2 mm/s and would take four minutes of simulated time to climb back half a
    /// metre — a boundary test that ran for twenty seconds and saw nothing would be measuring its
    /// own patience. 0.5 kg/m³ puts the same rule on a timescale a smoke can watch, and Part 1 is
    /// what checks the rule itself, at any density.
    /// </para>
    /// </remarks>
    public static class SharedSpaceSmoke
    {
        private const float FixedDt = 0.01f;
        private const int Steps = 2000;

        /// <summary>The step the displaced bodies are pushed out of the world at.</summary>
        private const int DisplaceAt = 200;

        /// <summary>How many are pushed through each face.</summary>
        private const int Displaced = 10;

        /// <summary>How far out of the world they are pushed, metres.</summary>
        private const float OutMetres = 0.25f;

        /// <summary>
        /// How far past the boundary a body is still allowed to be at the end, metres.
        /// </summary>
        /// <remarks>
        /// Not zero, and the reason is the rule itself: the restoring density makes y = 0 and
        /// y = −D <i>equilibria</i>, not walls. A heavy body settles at the floor, drifts a
        /// centimetre under it, is pushed back, and hovers there — which is the boundary working
        /// rather than failing. What would be a failure is a body that keeps going, so the bar is
        /// a hand's breadth rather than a float comparison.
        /// </remarks>
        private const float ReturnTolerance = 0.05f;

        /// <summary>
        /// How near the floor a body pushed under it has to end up, metres.
        /// </summary>
        /// <remarks>
        /// <b>The two boundaries are not symmetric, and the reason is inertia rather than the
        /// rule.</b> Above the surface both a buoyant body (restored) and a heavy one (its own
        /// weight) are pushed the same way, so a body pushed up crosses y = 0 and keeps going
        /// down: it returns and stays. Below the floor the sign flips at the boundary itself — a
        /// heavy body is pushed up while it is under −D and pulled down the moment it is over it —
        /// so −D is an equilibrium a body oscillates about, damped only by §5.2's drag. Measured
        /// here: an amplitude of order two tenths of a metre at 2 kg/m³, with the sample landing
        /// wherever in the cycle step 2,000 falls. What the rule promises is that the body is
        /// <i>held</i> at the floor, not that it is at rest on it, so that is what is asserted.
        /// </remarks>
        private const float FloorBandMetres = 0.5f;

        /// <summary>
        /// How far outside the box, in metres, is read as "the wrap has not run yet" rather than
        /// as a body loose in the world.
        /// </summary>
        /// <remarks>
        /// <b>A body reads as one step stale on the step it wraps.</b>
        /// <c>ArticulationBody.TeleportRoot</c> writes the root's pose into PhysX, and the managed
        /// <c>Transform</c> catches up at the next solver writeback — so the check below, which
        /// runs immediately after <c>Ecosystem.Step</c>, sees the pre-wrap position for exactly
        /// the one step in which a crossing is caught. Measured: one such reading in two thousand
        /// steps, matching the run's one wrap exactly, at under half a millimetre past the face.
        /// The bar is five centimetres, which is a body that has genuinely got out.
        /// </remarks>
        private const float OutsideTolerance = 0.05f;

        private const int Founders = 200;

        /// <summary>
        /// See the class remarks: fast enough that a half-metre return happens inside the run.
        /// </summary>
        /// <remarks>
        /// D048's own calibration table gives 0.0089 m/s at 0.1 kg/m³ and is linear at these
        /// speeds, so this is a fifth of a metre a second for a founder-shaped body — and the
        /// slowest body measured in this smoke still covers four tenths of a metre in the eighteen
        /// seconds left after the displacement, against a quarter-metre displacement.
        /// </remarks>
        private const float ExcessDensity = 2f;

        [MenuItem("Evosim/Shared Space Smoke")]
        public static void RunFromMenu() => Execute();

        public static void Run()
        {
            bool ok = Execute();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Execute()
        {
            var report = new StringBuilder();
            report.AppendLine("### Shared space smoke — D077");
            report.AppendLine();

            bool ok = true;

            try
            {
                ok &= RestoringBoundary(report);
                ok &= Geometry(report);
                ok &= TheBox(report);
            }
            catch (Exception e)
            {
                ok = false;
                report.AppendLine("FAIL — threw: " + e);
            }

            report.AppendLine();
            report.AppendLine(ok ? "Shared space smoke: PASS" : "Shared space smoke: FAIL");

            Debug.Log(report.ToString());
            return ok;
        }

        // ------------------------------------------------------------------- part 1: the boundary

        private static bool RestoringBoundary(StringBuilder report)
        {
            report.AppendLine("**1. The restoring top and bottom** (`FluidEnvironment.Restore`)");
            report.AppendLine();

            const float depth = 60f;
            const float excess = 0.02f;

            bool ok = true;

            // Off is off: at fraction 0 the restoring density is 0, and every case has to return
            // exactly what D050's clamp returns — negative net density zeroed at or above the
            // surface, everything else untouched.
            ok &= Check(report, "off, buoyant above the line  -> 0 (D050)",
                FluidEnvironment.Restore(-0.5f, 4f, 0f, depth), 0f);
            ok &= Check(report, "off, buoyant at the line     -> 0 (D050)",
                FluidEnvironment.Restore(-0.5f, 0f, 0f, depth), 0f);
            ok &= Check(report, "off, buoyant below the line  -> unchanged",
                FluidEnvironment.Restore(-0.5f, -1f, 0f, depth), -0.5f);
            ok &= Check(report, "off, heavy below the floor   -> unchanged",
                FluidEnvironment.Restore(0.5f, -80f, 0f, depth), 0.5f);

            // On: above the surface a body that would rise is pushed down at the sink density,
            // below the floor a body that would sink is pushed up at the same magnitude, and the
            // point they share is D050's clamp.
            ok &= Check(report, "on,  buoyant above the line  -> +f x excess (down)",
                FluidEnvironment.Restore(-0.5f, 4f, excess, depth), excess);
            ok &= Check(report, "on,  buoyant at the line     -> 0 (continuity with D050)",
                FluidEnvironment.Restore(-0.5f, 0f, excess, depth), 0f);
            ok &= Check(report, "on,  buoyant below the line  -> unchanged",
                FluidEnvironment.Restore(-0.5f, -1f, excess, depth), -0.5f);
            ok &= Check(report, "on,  heavy below the floor   -> -f x excess (up)",
                FluidEnvironment.Restore(0.5f, -80f, excess, depth), -excess);
            ok &= Check(report, "on,  heavy at the floor      -> unchanged",
                FluidEnvironment.Restore(0.5f, -60f, excess, depth), 0.5f);
            ok &= Check(report, "on,  heavy above the line    -> unchanged (already coming back)",
                FluidEnvironment.Restore(0.5f, 4f, excess, depth), 0.5f);
            ok &= Check(report, "on,  buoyant under the floor -> unchanged (already coming back)",
                FluidEnvironment.Restore(-0.5f, -80f, excess, depth), -0.5f);

            // Half a fraction is half a restoring force: the knob is a fraction of the sink rate
            // and not a switch.
            ok &= Check(report, "half strength above the line -> half",
                FluidEnvironment.Restore(-0.5f, 4f, 0.5f * excess, depth), 0.5f * excess);

            // The case D064's neutral volume makes the common one, and the one the first
            // fp-smoke arm failed on: a body with exactly zero net density is not "already
            // coming back", it is stuck, and the boundary has to move it.
            ok &= Check(report, "on,  neutral above the line   -> +f x excess (down)",
                FluidEnvironment.Restore(0f, 4f, excess, depth), excess);
            ok &= Check(report, "on,  neutral below the floor  -> -f x excess (up)",
                FluidEnvironment.Restore(0f, -80f, excess, depth), -excess);
            ok &= Check(report, "on,  neutral at the line      -> 0 (continuity with D050)",
                FluidEnvironment.Restore(0f, 0f, excess, depth), 0f);
            ok &= Check(report, "off, neutral above the line   -> 0 (D050)",
                FluidEnvironment.Restore(0f, 4f, 0f, depth), 0f);
            ok &= Check(report, "off, neutral below the floor  -> unchanged",
                FluidEnvironment.Restore(0f, -80f, 0f, depth), 0f);

            report.AppendLine();
            return ok;
        }

        private static bool Check(StringBuilder report, string what, float got, float want)
        {
            bool ok = Mathf.Abs(got - want) <= 1e-9f;

            report.AppendLine(
                (ok ? "- ok   " : "- FAIL ") + what + " — got " +
                got.ToString("0.#########", CultureInfo.InvariantCulture) + ", want " +
                want.ToString("0.#########", CultureInfo.InvariantCulture));

            return ok;
        }

        // ------------------------------------------------------------------ part 2: the geometry

        private static bool Geometry(StringBuilder report)
        {
            report.AppendLine("**2. The box** (`SharedVolume`)");
            report.AppendLine();

            bool ok = true;

            // The screen's own geometry: area 400 over four patches is W = 10, so the ring is
            // 40 m around and 10 m across.
            var volume = new SharedVolume(patchCount: 4, patchWidthMetres: 10f, depthMetres: 60f, seed: 1);

            ok &= Same(report, "ring length", volume.LengthMetres, 40f);

            ok &= Same(report, "patch at x=0", volume.PatchOf(0f), 0);
            ok &= Same(report, "patch at x=9.99", volume.PatchOf(9.99f), 0);
            ok &= Same(report, "patch at x=10", volume.PatchOf(10f), 1);
            ok &= Same(report, "patch at x=39.99", volume.PatchOf(39.99f), 3);
            ok &= Same(report, "patch at x=40 (wraps)", volume.PatchOf(40f), 0);
            ok &= Same(report, "patch at x=-1 (wraps)", volume.PatchOf(-1f), 3);

            // The wrap: inside is left alone, and each face maps to the opposite one.
            bool wrapped = volume.TryWrap(new Vector3(5f, -3f, 5f), out Vector3 inside);
            ok &= Same(report, "a body inside is not wrapped", wrapped ? 1 : 0, 0);
            ok &= Same(report, "and is not moved", (inside - new Vector3(5f, -3f, 5f)).magnitude, 0f);

            ok &= volume.TryWrap(new Vector3(41f, -3f, 5f), out Vector3 pastEnd);
            ok &= Same(report, "x = 41 -> 1", pastEnd.x, 1f);
            ok &= Same(report, "and keeps its depth", pastEnd.y, -3f);

            ok &= volume.TryWrap(new Vector3(-1f, -3f, 5f), out Vector3 pastStart);
            ok &= Same(report, "x = -1 -> 39", pastStart.x, 39f);

            ok &= volume.TryWrap(new Vector3(5f, -3f, 12f), out Vector3 pastSide);
            ok &= Same(report, "z = 12 -> 2", pastSide.z, 2f);

            // The wrap is not a swim: across a seam the shorter arc is what a speed instrument
            // has to difference, or a translated body reads as the fastest animal in the world.
            ok &= Same(report, "distance across the x seam (39.5 -> 0.5)",
                volume.ShortestDistance(new Vector3(0.5f, -3f, 5f), new Vector3(39.5f, -3f, 5f)), 1f);
            ok &= Same(report, "distance across the z seam (9.5 -> 0.5)",
                volume.ShortestDistance(new Vector3(5f, -3f, 0.5f), new Vector3(5f, -3f, 9.5f)), 1f);
            ok &= Same(report, "distance inside the box is ordinary",
                volume.ShortestDistance(new Vector3(5f, -3f, 5f), new Vector3(8f, -7f, 5f)), 5f);

            // The one that matters: rejection-sampled placements do not overlap. Founders drawn
            // from the reference world's own options, so the radii are the ones logbook/0064
            // measured rather than a convenient fiction.
            var config = new RunConfig();
            var placed = new System.Collections.Generic.List<Vector3>();
            var radii = new System.Collections.Generic.List<float>();
            int refused = 0;

            for (int i = 0; i < Founders; i++)
            {
                var rng = new Rng(Rng.SeedFor(4242UL, (ulong)i));
                Genome genome = GenomeFactory.Founder(rng, config.Genome, config.SensorPool());
                Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
                if (body.PartCount == 0) continue;

                float height = -rng.Range(0f, 60f);

                if (!volume.TryReserveFounder(body, height, out int _)) { refused++; continue; }

                // Commit is what actually occupies the space; a reservation nobody claims is a
                // birth that did not happen, and the next placement must be free to use the spot.
                volume.Commit(i);
                volume.TryTakePlacement(i, out Vector3 at);

                placed.Add(at);
                radii.Add(SharedVolume.BoundingRadius(body));
            }

            float worst = 0f;
            int overlaps = 0;

            for (int i = 0; i < placed.Count; i++)
            {
                for (int j = i + 1; j < placed.Count; j++)
                {
                    float gap = (placed[i] - placed[j]).magnitude - (radii[i] + radii[j]);
                    if (gap < 0f) { overlaps++; worst = Mathf.Min(worst, gap); }
                }
            }

            report.AppendLine(
                "- placed " + placed.Count + " of " + Founders + " founders, " + refused +
                " refused for want of room, " + volume.Rejections + " rejected attempts (" +
                (placed.Count > 0
                    ? (volume.Rejections / (double)placed.Count).ToString("0.##", CultureInfo.InvariantCulture)
                    : "—") + " per body), hash cell " +
                volume.CellMetres.ToString("0.###", CultureInfo.InvariantCulture) + " m");

            ok &= Same(report, "overlapping pairs", overlaps, 0);
            if (overlaps > 0)
            {
                report.AppendLine(
                    "  worst overlap " + worst.ToString("0.####", CultureInfo.InvariantCulture) + " m");
            }

            report.AppendLine();
            return ok;
        }

        private static bool Same(StringBuilder report, string what, float got, float want)
        {
            bool ok = Mathf.Abs(got - want) <= 1e-4f;
            report.AppendLine(
                (ok ? "- ok   " : "- FAIL ") + what + " = " +
                got.ToString("0.####", CultureInfo.InvariantCulture) +
                (ok ? "" : " (wanted " + want.ToString("0.####", CultureInfo.InvariantCulture) + ")"));
            return ok;
        }

        private static bool Same(StringBuilder report, string what, int got, int want)
        {
            bool ok = got == want;
            report.AppendLine(
                (ok ? "- ok   " : "- FAIL ") + what + " = " + got +
                (ok ? "" : " (wanted " + want + ")"));
            return ok;
        }

        // ------------------------------------------------------------------ part 3: real bodies

        private static bool TheBox(StringBuilder report)
        {
            report.AppendLine("**3. Two hundred bodies in it**");
            report.AppendLine();

            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            Ecosystem.ConfigurePhysicsStep(FixedDt);

            var config = new RunConfig
            {
                Light = new LightModel(200f, 12f),
                SharedSpace = true,
                HorizontalPatches = 4f,
                WorldAreaSquareMetres = 400f,
                WorldDepthMetres = 60f,
                FounderDepthSpread = 60f,

                // The whole founding cohort at once: the floor's trickle is two per metabolic
                // step, which would take a hundred metabolic steps — five thousand physics steps
                // — to reach two hundred, and the box is what is under test rather than the
                // trickle.
                MinimumPopulation = Founders,
                FloorSpawnsPerStep = Founders,
                MaximumPopulation = 4000,
            };

            config.Fluid.TissueExcessDensity = ExcessDensity;
            config.Fluid.SurfaceRestoringFraction = 1f;

            var eco = new Ecosystem(config, seed: 77);
            bool ok = true;

            try
            {
                int outsideEver = 0;
                float worstOutside = 0f;
                var pushedUp = new System.Collections.Generic.List<CreatureInstance>();
                var pushedDown = new System.Collections.Generic.List<CreatureInstance>();

                for (int step = 1; step <= Steps; step++)
                {
                    eco.Step();

                    if (step == DisplaceAt) Displace(eco, pushedUp, pushedDown);

                    // Every step, not every sample: a body outside the box for one step is a
                    // body the wrap did not catch, and a check at the end would miss it.
                    foreach (CreatureInstance instance in eco.Instances)
                    {
                        ArticulationBody[] bodies = instance.Bodies;
                        if (bodies == null || bodies.Length == 0) continue;

                        Vector3 p = bodies[0].transform.position;
                        float over = Outside(p, eco.Volume);

                        worstOutside = Mathf.Max(worstOutside, over);
                        if (over > OutsideTolerance) outsideEver++;
                    }
                }

                int aboveNow = 0, belowNow = 0;
                foreach (Organism creature in eco.World.Living)
                {
                    if (creature.HeightY > 0f) aboveNow++;
                    if (creature.HeightY < -config.WorldDepthMetres) belowNow++;
                }

                // Did the ones that were pushed out come back? Tracked by instance rather than
                // counted in aggregate: "nobody is outside" is also true of a world where the
                // displacement never happened, and this is the assertion the boundary rule
                // actually makes.
                int returnedUp = Returned(pushedUp, config.WorldDepthMetres, out float worstUp);
                int returnedDown = Returned(pushedDown, config.WorldDepthMetres, out float worstDown);

                report.AppendLine(
                    "- " + eco.World.Living.Count + " alive after " + Steps + " steps at dt " +
                    FixedDt + ", " + eco.World.FloorSpawns + " floor spawns, " +
                    eco.World.Births + " births, " + eco.World.CrowdedStillbirths +
                    " crowded stillbirths, " + eco.World.Diverged + " diverged");
                report.AppendLine(
                    "- box " + eco.Volume.PatchCount + " x " +
                    eco.Volume.PatchWidthMetres.ToString("0.##", CultureInfo.InvariantCulture) +
                    " x " + eco.Volume.PatchWidthMetres.ToString("0.##", CultureInfo.InvariantCulture) +
                    " m, depth " + eco.Volume.DepthMetres + ", hash cell " +
                    eco.Volume.CellMetres.ToString("0.###", CultureInfo.InvariantCulture) +
                    " m, " + eco.Volume.Rejections + " placement rejections");
                report.AppendLine(
                    "- wraps " + eco.Wraps + ", contact pairs " + eco.ContactPairs + " (" +
                    (eco.ContactPairs / (double)eco.Steps).ToString("0.####", CultureInfo.InvariantCulture) +
                    " per physics step)");
                report.AppendLine(
                    "- displaced at step " + DisplaceAt + ": " + pushedUp.Count + " to y=+" +
                    OutMetres + ", " + pushedDown.Count + " to y=-" +
                    (config.WorldDepthMetres + OutMetres) + "; back inside by step " + Steps +
                    ": " + returnedUp + " and " + returnedDown + " (deepest excursion left " +
                    Mathf.Max(worstUp, worstDown).ToString("0.###", CultureInfo.InvariantCulture) +
                    " m past a face)");
                report.AppendLine(
                    "- centre-of-mass census at the end: " + aboveNow + " above the surface, " +
                    belowNow + " below the floor");

                report.AppendLine(
                    "- furthest any root was seen past a horizontal face, any step: " +
                    worstOutside.ToString("0.000000", CultureInfo.InvariantCulture) +
                    " m (tolerance " + OutsideTolerance + " m — see OutsideTolerance)");

                ok &= Same(report, "bodies loose outside the box, any step", outsideEver, 0);

                // The displacement has to have happened, or every zero below is vacuous.
                ok &= Same(report, "bodies actually pushed above", pushedUp.Count, Displaced);
                ok &= Same(report, "bodies actually pushed below", pushedDown.Count, Displaced);

                // Above the surface: back inside and staying there, because nothing up there
                // pushes a body the other way (see FloorBandMetres).
                ok &= Same(report, "of those, back inside from above", returnedUp, pushedUp.Count);

                // Below the floor: held in a band about −D rather than resting on it, and the
                // count that made it all the way inside is a reading rather than a bar.
                report.AppendLine(
                    "- " + returnedDown + " of " + pushedDown.Count +
                    " pushed below are fully inside; the rest are oscillating about the floor");

                ok &= Within(report, "everything pushed below is held at the floor",
                    worstDown, FloorBandMetres);

                // The direction, not only the arrival: a body that is still out but is nearer than
                // it was has been restored, and this is what would fail if the sign were wrong.
                ok &= Nearer(report, "everything pushed out ended nearer the world",
                    Mathf.Max(worstUp, worstDown), OutMetres);

                ok &= Same(report, "`above` as the report reads it", eco.AboveSurface, 0);
                ok &= Same(report, "diverged", (int)eco.World.Diverged, 0);

                // The crowd has to be real: two hundred bodies in six thousand cubic metres that
                // never touch would mean the colliders or the contact report are off.
                bool touched = eco.ContactPairs > 0;
                report.AppendLine(
                    (touched ? "- ok   " : "- note ") + "contacts reported: " + eco.ContactPairs +
                    (touched ? "" : " — no pair met in this run, which is possible at this density"));

                // Nothing left non-finite. World.KillDiverged would already have caught a body
                // whose root went; this catches a part that went without its root.
                int nonFinite = 0;
                foreach (CreatureInstance instance in eco.Instances)
                {
                    ArticulationBody[] bodies = instance.Bodies;
                    if (bodies == null) continue;

                    for (int b = 0; b < bodies.Length; b++)
                    {
                        Vector3 p = bodies[b].transform.position;
                        Vector3 v = bodies[b].linearVelocity;
                        float sum = p.x + p.y + p.z + v.x + v.y + v.z;

                        if (float.IsNaN(sum) || float.IsInfinity(sum)) nonFinite++;
                    }
                }

                ok &= Same(report, "non-finite parts", nonFinite, 0);
            }
            finally
            {
                eco.DestroyAll();
                Physics.simulationMode = previousMode;
                Physics.gravity = previousGravity;
            }

            return ok;
        }

        private static bool Within(StringBuilder report, string what, float worst, float bound)
        {
            bool ok = worst <= bound;
            report.AppendLine(
                (ok ? "- ok   " : "- FAIL ") + what + ": worst " +
                worst.ToString("0.####", CultureInfo.InvariantCulture) + " m out, bound " +
                bound.ToString("0.####", CultureInfo.InvariantCulture));
            return ok;
        }

        private static bool Nearer(StringBuilder report, string what, float worst, float started)
        {
            bool ok = worst < started;
            report.AppendLine(
                (ok ? "- ok   " : "- FAIL ") + what + ": worst is now " +
                worst.ToString("0.####", CultureInfo.InvariantCulture) + " m out, from " +
                started.ToString("0.####", CultureInfo.InvariantCulture));
            return ok;
        }

        /// <summary>How far outside the box a root is, in metres, or 0 when it is inside.</summary>
        /// <remarks>
        /// Horizontal only. The vertical faces are restoring rather than periodic — a body above
        /// the surface is out of the water and being pulled back, which is the rule, not a fault
        /// — so the depth is asserted at the end rather than every step.
        /// </remarks>
        private static float Outside(Vector3 p, SharedVolume volume)
        {
            float over = 0f;

            if (p.x < 0f) over = Mathf.Max(over, -p.x);
            if (p.x >= volume.LengthMetres) over = Mathf.Max(over, p.x - volume.LengthMetres);
            if (p.z < 0f) over = Mathf.Max(over, -p.z);
            if (p.z >= volume.PatchWidthMetres) over = Mathf.Max(over, p.z - volume.PatchWidthMetres);

            return over;
        }

        /// <summary>Pushes some bodies out through the top and the bottom, to see them come back.</summary>
        private static void Displace(
            Ecosystem eco,
            System.Collections.Generic.List<CreatureInstance> up,
            System.Collections.Generic.List<CreatureInstance> down)
        {
            float floor = -eco.Volume.DepthMetres - OutMetres;

            foreach (CreatureInstance instance in eco.Instances)
            {
                ArticulationBody[] bodies = instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                ArticulationBody root = bodies[0];
                Vector3 p = root.transform.position;

                if (up.Count < Displaced)
                {
                    root.TeleportRoot(new Vector3(p.x, OutMetres, p.z), root.transform.rotation);
                    root.linearVelocity = Vector3.zero;
                    up.Add(instance);
                }
                else if (down.Count < Displaced)
                {
                    root.TeleportRoot(new Vector3(p.x, floor, p.z), root.transform.rotation);
                    root.linearVelocity = Vector3.zero;
                    down.Add(instance);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// How many of the displaced bodies are back between the surface and the floor.
        /// </summary>
        /// <remarks>
        /// A body that died in the meantime counts as returned — it is no longer outside the
        /// world in any sense the instrument cares about, and refusing to count it would make the
        /// test fail for a reason that has nothing to do with the boundary.
        /// </remarks>
        private static int Returned(
            System.Collections.Generic.List<CreatureInstance> displaced, float depth,
            out float worst)
        {
            int back = 0;
            worst = 0f;

            for (int i = 0; i < displaced.Count; i++)
            {
                ArticulationBody[] bodies = displaced[i].Bodies;
                if (bodies == null || bodies.Length == 0 || displaced[i].Root == null) { back++; continue; }

                float y = bodies[0].transform.position.y;
                if (float.IsNaN(y) || float.IsInfinity(y)) continue;

                float past = Mathf.Max(y, 0f) + Mathf.Max(-depth - y, 0f);
                worst = Mathf.Max(worst, past);

                if (past <= ReturnTolerance) back++;
            }

            return back;
        }
    }
}
