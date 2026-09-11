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
    /// pushed out through the top and into the bed to see whether the world brings them back. What
    /// it asserts is D077's rules, in the order they are numbered: nothing outside the box, the
    /// top restores, the bed holds, contacts are counted, and no body is left non-finite.
    /// </para>
    /// <para>
    /// <b>Part 4</b> is the other container — the tank of <c>fable-propose-aquarium.md</c> ruling 1
    /// (<c>logbook/specs/tank-spec.md</c>), added on 2026-09-11 and in the same two halves. First
    /// <see cref="SharedVolume"/>'s tank arithmetic with no scene: the bounding square, the rings,
    /// the wrap that never happens, the separation that is never folded, founders that are all
    /// inside the glass. Then the world with rock and glass in it, where five bodies are walked
    /// into the wall at three times the fastest speed anything here has ever swum, to see them
    /// stopped. The three box parts above are untouched: a Box world must stay bit-identical to
    /// every recording, which is the tank build's one invariant.
    /// </para>
    /// <para>
    /// <b>The bed is a collider now</b> (<c>logbook/specs/floor-spec.md</c>, <see cref="SeaFloor"/>), so
    /// what Part 3 asks of the bottom changed with it. It used to ask whether a spring threw a
    /// body back into the world; it now asks the two things a bed has to be true of: <b>nothing is
    /// ever placed inside it</b> — every founder's bounding sphere is clear of the rock the step
    /// its body is built — and <b>a body put inside it by hand comes out and stays out</b>. The
    /// second is slower than it looks, and deliberately: <c>Physics.defaultMaxDepenetrationVelocity</c>
    /// is 0.02 m/s in this project (<see cref="FluidEnvironment.MaxDepenetrationVelocity"/>,
    /// because depenetration is a free-energy source a creature can learn to farm), so an
    /// interpenetration takes fifty seconds a metre to resolve. <see cref="UnderMetres"/> is sized
    /// against that cap rather than against the bed.
    /// </para>
    /// <para>
    /// <b>The excess density is deliberately not the reference world's.</b> At 0.02 kg/m³ a body
    /// sinks at under 2 mm/s and drifts nowhere in twenty seconds, so a box test at that density
    /// would be measuring its own patience rather than the world's rules. A higher one puts
    /// ordinary sinking on a timescale a smoke can watch. The restoring boundary itself no longer
    /// depends on it at all — since the 2026-09-05 ruling it pushes at the body's whole weight —
    /// and Part 1 checks that rule on its own, at any density.
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

        /// <summary>How far above the surface they are pushed, metres.</summary>
        private const float OutMetres = 0.25f;

        /// <summary>How far into the sea bed the other ten are pushed, metres.</summary>
        /// <remarks>
        /// <b>Ten centimetres, and the number is about the depenetration cap rather than about the
        /// bed.</b> A body teleported inside a static collider is an initial condition no solver
        /// can fix instantly, and this project caps the fix at 0.02 m/s on purpose
        /// (<see cref="FluidEnvironment.MaxDepenetrationVelocity"/>): a separating velocity the
        /// solver invents is free energy, and logbook/0007 measured a creature farming it. So the
        /// rock pushes a buried body out at five centimetres a second and no faster. Ten
        /// centimetres is ten times the default contact offset — unambiguously inside — and clears
        /// in about five seconds, which is inside the run. Twenty-five centimetres, the figure the
        /// surface uses, would take twelve and a half seconds against a body that is also being
        /// pulled down at <see cref="ExcessDensity"/>, and a smoke that failed on that would be
        /// reporting the cap.
        /// </remarks>
        private const float UnderMetres = 0.1f;

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
        /// How far below −D a body is still allowed to be at the end, metres.
        /// </summary>
        /// <remarks>
        /// <b>The floor is a bed now, not a bouncer</b> (<see cref="SeaFloor"/>), so this is a
        /// contact tolerance rather than a band a body hovers in. A root at rest on the bed sits
        /// <i>above</i> −D by whatever the lowest part of its body reaches down to, and the only
        /// way to be under it at all is to be mid-way out of an interpenetration we created. Ten
        /// centimetres — the push itself — is therefore the bound: a body that has not moved is a
        /// failure and a body that has come most of the way is not.
        /// </remarks>
        private const float BedBandMetres = 0.1f;

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
        /// See the class remarks: fast enough that a body <i>settles</i> inside the run.
        /// </summary>
        /// <remarks>
        /// D048's own calibration table gives 0.0089 m/s at 0.1 kg/m³ and is linear at these
        /// speeds, so this is a fifth of a metre a second for a founder-shaped body. It no longer
        /// sets how fast a displaced body comes home — since the 2026-09-05 ruling the boundary
        /// pushes at the body's whole weight, which is three orders of magnitude larger and
        /// returns a body in well under a second — but it is still what decides where a body
        /// drifts once it is back in the water, and a world in which nothing sinks at all would
        /// make the box's other assertions vacuous.
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
                ok &= TheTank(report);
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

            // The restoring density is now the water's own density times the fraction — a body
            // out of the water feels mass x g, so the net density that expresses it is the one
            // the mass was assigned with. 1,000 kg/m3 is a fraction of 1; the pure function does
            // not care what the number means, but the cases below are only honest at the scale
            // the call site actually passes.
            const float weight = 1000f;

            bool ok = true;

            // Off is off: at fraction 0 the restoring density is 0, and every case has to return
            // exactly what D050's clamp returns — negative net density zeroed at or above the
            // surface, everything else untouched.
            ok &= Check(report, "off, buoyant above the line   -> 0 (D050)",
                FluidEnvironment.Restore(-0.5f, 4f, 0f, depth), 0f);
            ok &= Check(report, "off, buoyant at the line      -> 0 (D050)",
                FluidEnvironment.Restore(-0.5f, 0f, 0f, depth), 0f);
            ok &= Check(report, "off, buoyant below the line   -> unchanged",
                FluidEnvironment.Restore(-0.5f, -1f, 0f, depth), -0.5f);
            ok &= Check(report, "off, heavy below the floor    -> unchanged",
                FluidEnvironment.Restore(0.5f, -80f, 0f, depth), 0.5f);

            // On: above the surface a body feels its whole weight downward, below the floor the
            // mirror upward, and the one point the two rules share is D050's clamp.
            ok &= Check(report, "on,  buoyant above the line   -> +f x weight (down)",
                FluidEnvironment.Restore(-0.5f, 4f, weight, depth), weight);
            ok &= Check(report, "on,  buoyant at the line      -> 0 (continuity with D050)",
                FluidEnvironment.Restore(-0.5f, 0f, weight, depth), 0f);
            ok &= Check(report, "on,  buoyant below the line   -> unchanged",
                FluidEnvironment.Restore(-0.5f, -1f, weight, depth), -0.5f);
            ok &= Check(report, "on,  heavy below the floor    -> -f x weight (up)",
                FluidEnvironment.Restore(0.5f, -80f, weight, depth), -weight);
            ok &= Check(report, "on,  heavy at the floor       -> unchanged",
                FluidEnvironment.Restore(0.5f, -60f, weight, depth), 0.5f);

            // Monotone, which is the whole reason the rule is a max and not a return. A body
            // already sinking above the line is out of the water too, and it does not get to
            // fall five hundred times slower than the neutral one beside it because its own
            // excess density happened to be positive. It falls at its weight, or at more than
            // its weight if it somehow had more.
            ok &= Check(report, "on,  heavy above the line     -> +f x weight, not its excess",
                FluidEnvironment.Restore(0.5f, 4f, weight, depth), weight);
            ok &= Check(report, "on,  heavier than its weight  -> unchanged (never reduced)",
                FluidEnvironment.Restore(2f * weight, 4f, weight, depth), 2f * weight);
            ok &= Check(report, "on,  buoyant under the floor  -> -f x weight, not its lift",
                FluidEnvironment.Restore(-0.5f, -80f, weight, depth), -weight);
            ok &= Check(report, "on,  lighter than its weight  -> unchanged (never reduced)",
                FluidEnvironment.Restore(-2f * weight, -80f, weight, depth), -2f * weight);

            // Half a fraction is half the weight: the knob is a fraction and not a switch.
            ok &= Check(report, "half strength above the line  -> half",
                FluidEnvironment.Restore(-0.5f, 4f, 0.5f * weight, depth), 0.5f * weight);

            // The case D064's neutral volume makes the common one, and the one the first
            // fp-smoke arm failed on: a body with exactly zero net density is not "already
            // coming back", it is stuck, and the boundary has to move it.
            ok &= Check(report, "on,  neutral above the line   -> +f x weight (down)",
                FluidEnvironment.Restore(0f, 4f, weight, depth), weight);
            ok &= Check(report, "on,  neutral below the floor  -> -f x weight (up)",
                FluidEnvironment.Restore(0f, -80f, weight, depth), -weight);
            ok &= Check(report, "on,  neutral at the line      -> 0 (continuity with D050)",
                FluidEnvironment.Restore(0f, 0f, weight, depth), 0f);
            ok &= Check(report, "off, neutral above the line   -> 0 (D050)",
                FluidEnvironment.Restore(0f, 4f, 0f, depth), 0f);
            ok &= Check(report, "off, neutral below the floor  -> unchanged",
                FluidEnvironment.Restore(0f, -80f, 0f, depth), 0f);

            // With a real sea bed the bottom half of the rule is retired and the fraction governs
            // the surface alone (logbook/specs/floor-spec.md rule 1). These are the cases that would
            // make a trampoline if both acted: rock holding a body down while the water threw it
            // back up. The surface is untouched by the change, which is the other half of the
            // claim and is why it is asserted here rather than assumed.
            ok &= Check(report, "bed, heavy below the floor    -> unchanged (the rock holds it)",
                FluidEnvironment.Restore(0.5f, -80f, weight, depth, floorRestores: false), 0.5f);
            ok &= Check(report, "bed, buoyant below the floor  -> unchanged",
                FluidEnvironment.Restore(-0.5f, -80f, weight, depth, floorRestores: false), -0.5f);
            ok &= Check(report, "bed, neutral below the floor  -> unchanged",
                FluidEnvironment.Restore(0f, -80f, weight, depth, floorRestores: false), 0f);
            ok &= Check(report, "bed, above the line           -> +f x weight, as before",
                FluidEnvironment.Restore(-0.5f, 4f, weight, depth, floorRestores: false), weight);
            ok &= Check(report, "bed, at the line              -> 0 (D050), as before",
                FluidEnvironment.Restore(-0.5f, 0f, weight, depth, floorRestores: false), 0f);

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

            ok &= Same(report, "patch at x=0", volume.PatchOf(0f, 0f), 0);
            ok &= Same(report, "patch at x=9.99", volume.PatchOf(9.99f, 0f), 0);
            ok &= Same(report, "patch at x=10", volume.PatchOf(10f, 0f), 1);
            ok &= Same(report, "patch at x=39.99", volume.PatchOf(39.99f, 0f), 3);
            ok &= Same(report, "patch at x=40 (wraps)", volume.PatchOf(40f, 0f), 0);
            ok &= Same(report, "patch at x=-1 (wraps)", volume.PatchOf(-1f, 0f), 3);

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
            //
            // With a bed under the box (logbook/specs/floor-spec.md rule 2) the depth is no longer free
            // either: the draw here runs the full 60 m of a 60 m world, exactly as the reference
            // world's EVOSIM_FOUNDER_DEPTH does, so the deep end of it lands inside the rock and
            // every one of those has to come back raised.
            SeaFloor bed = SeaFloor.Build(volume);
            volume.Floor = bed;

            var config = new RunConfig();
            var placed = new System.Collections.Generic.List<Vector3>();
            var radii = new System.Collections.Generic.List<float>();
            int refused = 0;
            int raised = 0;
            float deepestSphere = 0f;
            float worstIntoTheBed = 0f;
            int misreported = 0;

            for (int i = 0; i < Founders; i++)
            {
                var rng = new Rng(Rng.SeedFor(4242UL, (ulong)i));
                Genome genome = GenomeFactory.Founder(rng, config.Genome, config.SensorPool());
                Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
                if (body.PartCount == 0) continue;

                float drawn = -rng.Range(0f, 60f);
                float height = drawn;

                if (!volume.TryReserveFounder(body, ref height, out int _)) { refused++; continue; }

                if (height > drawn) raised++;

                // Commit is what actually occupies the space; a reservation nobody claims is a
                // birth that did not happen, and the next placement must be free to use the spot.
                volume.Commit(i);
                volume.TryTakePlacement(i, out Vector3 at);

                float radius = SharedVolume.BoundingRadius(body);

                // The depth handed back to the world has to be the depth the body was actually put
                // at, or the economy charges one layer for a creature living in another.
                if (Mathf.Abs(at.y - height) > 1e-4f) misreported++;

                float bottom = at.y - radius;
                deepestSphere = Mathf.Min(deepestSphere, bottom);
                worstIntoTheBed = Mathf.Max(worstIntoTheBed, bed.TopY - bottom);

                placed.Add(at);
                radii.Add(radius);
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

            report.AppendLine(
                "- bed at y=" + bed.TopY.ToString("0.##", CultureInfo.InvariantCulture) +
                " m, BoxCollider " + bed.Collider.size.x.ToString("0.##", CultureInfo.InvariantCulture) +
                " x " + bed.Collider.size.y.ToString("0.##", CultureInfo.InvariantCulture) +
                " x " + bed.Collider.size.z.ToString("0.##", CultureInfo.InvariantCulture) +
                " m, layer " + bed.Root.layer + ", material " +
                (bed.Collider.sharedMaterial == null ? "project default" : bed.Collider.sharedMaterial.name) +
                ", providesContacts " + bed.Collider.providesContacts);

            report.AppendLine(
                "- " + raised + " of " + placed.Count + " founders were drawn inside the rock and " +
                "raised clear; deepest bounding sphere now reaches " +
                deepestSphere.ToString("0.####", CultureInfo.InvariantCulture) + " m");

            ok &= Same(report, "overlapping pairs", overlaps, 0);
            if (overlaps > 0)
            {
                report.AppendLine(
                    "  worst overlap " + worst.ToString("0.####", CultureInfo.InvariantCulture) + " m");
            }

            // Rule 2: nothing is placed in the floor. Not "few" and not "on average" — the whole
            // bounding sphere of every body, clear of the rock.
            ok &= Same(report, "founders whose sphere reaches into the bed",
                Mathf.Max(0f, worstIntoTheBed), 0f);

            ok &= Same(report, "founders placed at a depth other than the one reported", misreported, 0);

            // And the raising has to have happened, or the line above is true of a world in which
            // the bed was never consulted.
            bool anyRaised = raised > 0;
            report.AppendLine(
                (anyRaised ? "- ok   " : "- FAIL ") + "the clamp was exercised: " + raised + " raised");
            ok &= anyRaised;

            bed.Destroy();
            volume.Floor = null;

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

                // Rule 2, measured on real articulations rather than on reserved spheres: every
                // body is checked against the bed the first step it exists, which is the step
                // after Ecosystem.Build placed it and before any physics has moved it.
                var seen = new System.Collections.Generic.HashSet<EntityId>();
                int placedChecked = 0;
                float worstBuiltIntoTheBed = 0f;

                // How far under the bed anything got, at any step, after the displacement — the
                // bound the spec names is −D − radius, and this is the number it is read against.
                float deepestPartEver = 0f;
                int clearedAtStep = -1;

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

                        // The first sight of a body is the one that says whether it was *placed*
                        // in the rock. Everything after that is the physics, which is what the
                        // rest of this part is about.
                        if (instance.Root != null && seen.Add(instance.Root.GetEntityId()))
                        {
                            placedChecked++;

                            float radius = SharedVolume.BoundingRadius(instance.Phenotype);
                            worstBuiltIntoTheBed = Mathf.Max(
                                worstBuiltIntoTheBed, eco.Floor.TopY - (p.y - radius));
                        }
                    }

                    if (step <= DisplaceAt) continue;

                    // The displaced ten, on their way out of the rock. Roots only: what "under the
                    // floor" means for a body is where its root is, which is what every other
                    // depth reading in this project is denominated in.
                    float deepestNow = 0f;

                    for (int i = 0; i < pushedDown.Count; i++)
                    {
                        // A displaced body can starve and be destroyed mid-run — Returned() has
                        // the same guard, and for the same reason: a dead body is not under the
                        // bed in any sense the instrument cares about.
                        if (pushedDown[i].Root == null) continue;

                        ArticulationBody[] bodies = pushedDown[i].Bodies;
                        if (bodies == null || bodies.Length == 0) continue;

                        float y = bodies[0].transform.position.y;
                        if (float.IsNaN(y) || float.IsInfinity(y)) continue;

                        deepestNow = Mathf.Max(deepestNow, eco.Floor.TopY - y);
                    }

                    deepestPartEver = Mathf.Max(deepestPartEver, deepestNow);
                    if (clearedAtStep < 0 && deepestNow <= 0f) clearedAtStep = step;
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
                    "- box " + eco.Volume.PatchesAlong + " x " + eco.Volume.PatchesAcross +
                    " x " +
                    eco.Volume.PatchWidthMetres.ToString("0.##", CultureInfo.InvariantCulture) +
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
                    (config.WorldDepthMetres + UnderMetres) + " (inside the bed); back inside by " +
                    "step " + Steps + ": " + returnedUp + " and " + returnedDown +
                    " (deepest excursion left " +
                    Mathf.Max(worstUp, worstDown).ToString("0.###", CultureInfo.InvariantCulture) +
                    " m past a face)");
                report.AppendLine(
                    "- the buried ten: deepest any root was under the bed after the push " +
                    deepestPartEver.ToString("0.####", CultureInfo.InvariantCulture) +
                    " m, all clear of it by step " +
                    (clearedAtStep >= 0 ? clearedAtStep.ToString(CultureInfo.InvariantCulture) : "never") +
                    " (depenetration is capped at " + FluidEnvironment.MaxDepenetrationVelocity +
                    " m/s — see UnderMetres)");
                report.AppendLine(
                    "- the bed: " + eco.FloorContactPairs + " floor contact pairs (" +
                    (eco.FloorContactPairs / (double)eco.Steps).ToString("0.####", CultureInfo.InvariantCulture) +
                    " per physics step), counted apart from the " + eco.ContactPairs +
                    " creature-creature pairs");
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
                // pushes a body the other way.
                ok &= Same(report, "of those, back inside from above", returnedUp, pushedUp.Count);

                // Rule 2 on the real bodies: nothing was *built* inside the rock. This is the one
                // the three r25q-s2 divergences were newborns of.
                ok &= Same(report, "bodies built with their sphere in the bed",
                    Mathf.Max(0f, worstBuiltIntoTheBed), 0f);
                report.AppendLine("- " + placedChecked + " bodies were checked as they were built");

                // Out of the bed and staying out. Both halves: everything is back inside at the
                // end, and nothing ever got further in than the push itself — a body that sank
                // *through* rock would show here as a deeper excursion, not as a slow return.
                ok &= Same(report, "of those, back out of the bed", returnedDown, pushedDown.Count);
                ok &= Within(report, "deepest any buried root ever got under the bed",
                    deepestPartEver, UnderMetres + 1e-3f);
                ok &= Within(report, "excursion left under the bed at the end",
                    worstDown, BedBandMetres);

                // The direction, not only the arrival: a body that is still out but is nearer than
                // it was has been restored, and this is what would fail if the sign were wrong.
                ok &= Nearer(report, "everything pushed out ended nearer the world",
                    Mathf.Max(worstUp, worstDown), OutMetres);

                ok &= Same(report, "`above` as the report reads it", eco.AboveSurface, 0);
                ok &= Same(report, "below the floor, centre of mass", belowNow, 0);
                ok &= Same(report, "diverged", (int)eco.World.Diverged, 0);

                // The bed has to be reporting: ten bodies resting on it that produce no pairs
                // would mean the collider is on a layer nothing hits, or providesContacts is off,
                // or the split is filing floor pairs as creature pairs.
                bool bedTouched = eco.FloorContactPairs > 0;
                report.AppendLine(
                    (bedTouched ? "- ok   " : "- FAIL ") + "the bed reports contacts: " +
                    eco.FloorContactPairs + " pairs");
                ok &= bedTouched;

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
            if (p.z >= volume.WidthMetres) over = Mathf.Max(over, p.z - volume.WidthMetres);

            return over;
        }

        // ----------------------------------------------------------------------- part 4: the tank

        /// <summary>The tank's footprint, m². 100 m² is R = 5.6419 m — round 37's water.</summary>
        private const float TankArea = 100f;

        /// <summary>K, the rings of equal area a tank's patches are.</summary>
        private const int TankRings = 4;

        /// <summary>
        /// How far past the glass a root is allowed to be seen, metres.
        /// </summary>
        /// <remarks>
        /// <b>A quarter of a metre, and it is a contact tolerance rather than a band.</b> The
        /// placer never puts a body outside the circle and the slabs' inner faces are tangent to
        /// it, so a root pressed against the glass sits at R less whatever its own body reaches
        /// out to; the only way past R at all is the millimetre or two of the prism's corners
        /// (<see cref="TankWall"/>) plus the solver's contact offset. A quarter of a metre is far
        /// more than either and far less than the metre at which
        /// <c>Ecosystem.CheckFinite</c> calls a root a <c>Diverged</c> death, so a body this test
        /// passes is a body inside the water and not one the divergence guard would have caught.
        /// </remarks>
        private const float WallToleranceMetres = 0.25f;

        /// <summary>How hard a walked body is pushed at the glass, m/s.</summary>
        /// <remarks>
        /// The fastest animal on record here manages 0.5 m/s, so this is three times anything the
        /// wall will ever be asked to stop by a creature — and at dt 0.01 it is 1.5 cm a step
        /// against half a metre of glass, which is thirty steps of contact before anything could
        /// tunnel. Re-imposed every step rather than applied once, because the fluid takes it
        /// straight back off: what is under test is a body that keeps trying to leave.
        /// </remarks>
        private const float WalkMetresPerSecond = 1.5f;

        /// <summary>How many bodies are walked into the glass.</summary>
        private const int Walkers = 5;

        /// <summary>Physics steps the tank is run for — six seconds at <see cref="FixedDt"/>.</summary>
        /// <remarks>
        /// Enough for a walked body to cross the whole tank at <see cref="WalkMetresPerSecond"/>
        /// (9 m against a diameter of 11.3, from wherever in the disc it was founded) and then to
        /// spend seconds pressed against the glass, which is the part that matters. The box's two
        /// thousand steps are not needed: nothing here is waiting on a depenetration at 0.02 m/s.
        /// </remarks>
        private const int TankSteps = 600;

        /// <summary>Founders the walled world is run with.</summary>
        private const int TankFounders = 40;

        /// <summary>
        /// Does the tank hold? — <c>fable-propose-aquarium.md</c> ruling 1,
        /// <c>logbook/specs/tank-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Two halves, cheapest first, exactly as the box's three are.</b> The first is
        /// <see cref="SharedVolume"/>'s tank arithmetic with no scene at all: the bounding square,
        /// the ring a position falls in, the wrap that must never happen, the distance that must
        /// not be folded, and founders that must all be inside the glass. The second builds the
        /// world with rock and glass in it and walks bodies at the wall to see them stopped.
        /// </para>
        /// <para>
        /// <b>Why the wall is worth a smoke of its own.</b> Every other rule that keeps a body in
        /// the tank is arithmetic and is tested as arithmetic — the placer refuses a candidate
        /// outside the disc, the gyre has no radial flow at the glass, the divergence guard kills
        /// a root a metre past it. The wall is the only one that is a collider, which means it is
        /// the only one that can be built on the wrong layer, at the wrong radius, or facing the
        /// wrong way and still look right in every number the world prints. A body walked into it
        /// is the one question those four cannot answer between them.
        /// </para>
        /// <para>
        /// <b>The header token is asked of the code that writes it</b>
        /// (<see cref="EvolutionRun.SpaceToken"/>) rather than assembled again here. A smoke
        /// carrying its own copy of the format would agree with itself for ever, and the token is
        /// the only proof a launcher has that the container it asked for arrived: a build that
        /// does not know <c>EVOSIM_SHAPE</c> ignores it and runs the old box.
        /// </para>
        /// </remarks>
        private static bool TheTank(StringBuilder report)
        {
            report.AppendLine("**4. The tank** (`TankGeometry`, `TankWall`)");
            report.AppendLine();

            bool ok = true;

            float radius = TankGeometry.RadiusFor(TankArea);

            // The patch width is the box's number and means nothing in a tank — a ring is an
            // annulus — but it is what the constructor takes, and handing it the world's own
            // sqrt(area / K) keeps this volume identical to the one Ecosystem would build.
            var volume = new SharedVolume(
                patchCount: TankRings,
                patchWidthMetres: Mathf.Sqrt(TankArea / TankRings),
                depthMetres: 60f,
                seed: 1,
                offspringDispersalMetres: 0f,
                patchesAcross: 1,
                shape: WorldShape.Tank,
                tankRadiusMetres: radius);

            report.AppendLine(
                "- tank of " + TankArea + " m2: R = " +
                radius.ToString("0.####", CultureInfo.InvariantCulture) +
                " m, bounding square " +
                volume.LengthMetres.ToString("0.####", CultureInfo.InvariantCulture) +
                " m on a side, axis at (" +
                radius.ToString("0.##", CultureInfo.InvariantCulture) + ", " +
                radius.ToString("0.##", CultureInfo.InvariantCulture) + ")");

            ok &= Same(report, "the bounding square's length", volume.LengthMetres, 2f * radius);
            ok &= Same(report, "the bounding square's width", volume.WidthMetres, 2f * radius);

            // The rings, at known radii. Never on a boundary: ring i begins at R.sqrt(i/K) and a
            // point placed exactly there is a float comparison rather than a fact about the world
            // — the fractions below are either side of one, which is what a reader of a per-patch
            // bin actually needs to be true.
            ok &= Same(report, "ring at the axis", volume.PatchOf(radius, radius), 0);
            ok &= Same(report, "ring at r = 0.49 R (inside the first boundary)",
                volume.PatchOf(radius + 0.49f * radius, radius), 0);
            ok &= Same(report, "ring at r = 0.51 R (outside it)",
                volume.PatchOf(radius + 0.51f * radius, radius), 1);
            ok &= Same(report, "ring at r = 0.51 R the other way (a ring has no direction)",
                volume.PatchOf(radius - 0.51f * radius, radius), 1);
            ok &= Same(report, "ring at r = 0.75 R, on z",
                volume.PatchOf(radius, radius + 0.75f * radius), 2);
            ok &= Same(report, "ring at r = 0.99 R (against the glass)",
                volume.PatchOf(radius + 0.99f * radius, radius), 3);

            // Outside reads the last ring rather than throwing: float error at the wall is not a
            // world event, and the callers that care about being outside ask InTheWater.
            ok &= Same(report, "ring past the glass (clamped, not thrown)",
                volume.PatchOf(radius + 1.5f * radius, radius), 3);

            ok &= Same(report, "the axis is in the water", volume.InTheWater(radius, radius) ? 1 : 0, 1);
            ok &= Same(report, "a corner of the bounding square is not",
                volume.InTheWater(0.1f, 0.1f) ? 1 : 0, 0);

            // The wrap that must never happen. Every one of these leaves the box on some axis, and
            // in D077's water each would have been translated to the far face; here the boundary
            // is a collider and the body is stopped during the step rather than moved after it.
            var loose = new[]
            {
                new Vector3(5f, -3f, 5f),
                new Vector3(-1f, -3f, 5f),
                new Vector3(2f * radius + 1f, -3f, 5f),
                new Vector3(5f, -3f, -2f),
                new Vector3(5f, -3f, 2f * radius + 2f),
                new Vector3(4f * radius, -3f, 4f * radius),
            };

            int wrapped = 0;
            int moved = 0;

            for (int i = 0; i < loose.Length; i++)
            {
                if (volume.TryWrap(loose[i], out Vector3 after)) wrapped++;
                if ((after - loose[i]).sqrMagnitude > 0f) moved++;
            }

            ok &= Same(report, "positions wrapped, including four outside the square", wrapped, 0);
            ok &= Same(report, "positions moved by the wrap", moved, 0);
            ok &= Same(report, "the wrap counter", (float)volume.Wraps, 0f);

            // And the distance is the plain one. Two bodies at opposite ends of the bounding
            // square are as far apart as the world goes; a periodic box of the same extent would
            // call them a metre apart, which is the reading the minimum image exists to give and
            // the one a tank must not.
            ok &= Same(report, "distance across the tank (0.5 -> 2R - 0.5)",
                volume.ShortestDistance(
                    new Vector3(0.5f, -3f, radius), new Vector3(2f * radius - 0.5f, -3f, radius)),
                2f * radius - 1f);

            // Founders: uniform over the disc, and every one of them inside the glass. The bed is
            // under them for part 2's reason — the founder lottery draws depths inside the rock,
            // and a placer that did not consult the floor would bury a few.
            SeaFloor bed = SeaFloor.Build(volume);
            volume.Floor = bed;

            var config = new RunConfig();
            int placed = 0;
            int outside = 0;
            int badRing = 0;
            float furthest = 0f;
            var ringCount = new int[TankRings];

            for (int i = 0; i < Founders; i++)
            {
                var rng = new Rng(Rng.SeedFor(909UL, (ulong)i));
                Genome genome = GenomeFactory.Founder(rng, config.Genome, config.SensorPool());
                Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
                if (body.PartCount == 0) continue;

                float height = -rng.Range(0f, 60f);

                if (!volume.TryReserveFounder(body, ref height, out int patch)) continue;

                volume.Commit(i);
                volume.TryTakePlacement(i, out Vector3 at);

                placed++;
                if (!volume.InTheWater(at.x, at.z)) outside++;
                if (patch < 0 || patch >= TankRings) badRing++;
                else ringCount[patch]++;

                float dx = at.x - radius;
                float dz = at.z - radius;
                furthest = Mathf.Max(furthest, Mathf.Sqrt(dx * dx + dz * dz));
            }

            report.AppendLine(
                "- placed " + placed + " founders over the disc, " + volume.Rejections +
                " rejected attempts; furthest from the axis " +
                furthest.ToString("0.###", CultureInfo.InvariantCulture) + " m of " +
                radius.ToString("0.###", CultureInfo.InvariantCulture) + "; by ring " +
                string.Join("/", System.Array.ConvertAll(ringCount, c => c.ToString(CultureInfo.InvariantCulture))));

            ok &= Same(report, "founders placed outside the glass", outside, 0);
            ok &= Same(report, "founders placed in a ring that does not exist", badRing, 0);

            // And the draw has to have happened, or both zeroes above are true of a world in which
            // no founder was ever set down — part 2's `anyRaised` rule.
            bool anyPlaced = placed > 0;
            report.AppendLine(
                (anyPlaced ? "- ok   " : "- FAIL ") + "the disc was drawn on: " + placed +
                " of " + Founders + " founders placed");
            ok &= anyPlaced;

            bed.Destroy();
            volume.Floor = null;

            report.AppendLine();
            ok &= TheGlass(report, radius);

            report.AppendLine();
            return ok;
        }

        /// <summary>
        /// The walled world with bodies in it: the header's token, and bodies walked at the glass.
        /// </summary>
        private static bool TheGlass(StringBuilder report, float radius)
        {
            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);

            Ecosystem.ConfigurePhysicsStep(FixedDt);

            var config = new RunConfig
            {
                Light = new LightModel(200f, 12f),
                SharedSpace = true,

                // The tank, and the one thing it insists on: a grid. World refuses a tank on the
                // cell field (a row of patches is not an annulus) and on the vertex field (a set
                // of positions in a periodic box with no mask), so this is not a choice.
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,

                HorizontalPatches = TankRings,
                WorldAreaSquareMetres = TankArea,
                WorldDepthMetres = 60f,
                FounderDepthSpread = 60f,

                MinimumPopulation = TankFounders,
                FloorSpawnsPerStep = TankFounders,
                MaximumPopulation = 4000,
            };

            config.Fluid.TissueExcessDensity = ExcessDensity;
            config.Fluid.SurfaceRestoringFraction = 1f;

            var eco = new Ecosystem(config, seed: 77);
            bool ok = true;

            try
            {
                // The token the run's header will carry, from the code that writes it. This is the
                // only proof a launcher has that the shape arrived: a build that does not know
                // EVOSIM_SHAPE ignores it silently and runs the old box.
                string token = EvolutionRun.SpaceToken(config, eco);
                const string wanted = "tank r=5.64 m (100 m2), depth 60, wall, bed";

                bool tokenOk = token == wanted;
                report.AppendLine(
                    (tokenOk ? "- ok   " : "- FAIL ") + "the header's space token: `" + token + "`" +
                    (tokenOk ? "" : " — wanted `" + wanted + "`"));
                ok &= tokenOk;

                ok &= Same(report, "slabs of glass", eco.Wall != null ? eco.Wall.Colliders.Length : 0,
                    TankWall.Segments);

                var walkers = new System.Collections.Generic.List<CreatureInstance>(Walkers);

                float worstRadius = 0f;
                float furthestWalker = 0f;
                int outsideEver = 0;

                for (int step = 1; step <= TankSteps; step++)
                {
                    eco.Step();

                    // Picked once the founding cohort has bodies, and kept: what is under test is
                    // one body's whole journey to the glass and what happens when it arrives. The
                    // economy runs once every Ecosystem.StepsPerMetabolicStep physics steps and
                    // the floor's spawns are its business, so there is nothing to pick before the
                    // first of those; asked again each step until it has its five rather than on
                    // one chosen step, which would be a number about the cadence.
                    if (walkers.Count < Walkers && step > Ecosystem.StepsPerMetabolicStep)
                    {
                        Walk(eco, walkers);
                    }

                    // Re-imposed every step. The fluid takes the push straight back off, and a
                    // body that stopped trying to leave would make every number below vacuous.
                    Push(walkers, radius);

                    foreach (CreatureInstance instance in eco.Instances)
                    {
                        ArticulationBody[] bodies = instance.Bodies;
                        if (bodies == null || bodies.Length == 0) continue;

                        Vector3 p = bodies[0].transform.position;
                        float sum = p.x + p.z;
                        if (float.IsNaN(sum) || float.IsInfinity(sum)) continue;

                        float dx = p.x - radius;
                        float dz = p.z - radius;
                        float r = Mathf.Sqrt(dx * dx + dz * dz);

                        worstRadius = Mathf.Max(worstRadius, r);
                        if (r > radius + WallToleranceMetres) outsideEver++;
                    }

                    for (int i = 0; i < walkers.Count; i++)
                    {
                        if (walkers[i].Root == null) continue;

                        ArticulationBody[] bodies = walkers[i].Bodies;
                        if (bodies == null || bodies.Length == 0) continue;

                        Vector3 p = bodies[0].transform.position;
                        float sum = p.x + p.z;
                        if (float.IsNaN(sum) || float.IsInfinity(sum)) continue;

                        float dx = p.x - radius;
                        float dz = p.z - radius;

                        furthestWalker = Mathf.Max(furthestWalker, Mathf.Sqrt(dx * dx + dz * dz));
                    }
                }

                report.AppendLine(
                    "- " + eco.World.Living.Count + " alive after " + TankSteps + " steps at dt " +
                    FixedDt + ", " + eco.World.FloorSpawns + " floor spawns, " + eco.World.Births +
                    " births, " + eco.World.Diverged + " diverged");
                report.AppendLine(
                    "- " + walkers.Count + " bodies walked at the glass at " +
                    WalkMetresPerSecond + " m/s: the furthest reached " +
                    furthestWalker.ToString("0.####", CultureInfo.InvariantCulture) +
                    " m from the axis, in a tank of " +
                    radius.ToString("0.####", CultureInfo.InvariantCulture) + " m");
                report.AppendLine(
                    "- furthest any root was seen from the axis, any step and any body: " +
                    worstRadius.ToString("0.####", CultureInfo.InvariantCulture) +
                    " m (tolerance R + " + WallToleranceMetres + " m — see WallToleranceMetres)");
                report.AppendLine(
                    "- the glass and the bed: " + eco.FloorContactPairs +
                    " contact pairs, counted apart from the " + eco.ContactPairs +
                    " creature-creature pairs");

                // The walk has to have happened, or every zero below is a statement about a world
                // in which nothing ever went near the wall.
                bool reached = walkers.Count > 0 && furthestWalker > radius - 1f;
                report.AppendLine(
                    (reached ? "- ok   " : "- FAIL ") + "the glass was actually reached: " +
                    furthestWalker.ToString("0.###", CultureInfo.InvariantCulture) + " m of " +
                    radius.ToString("0.###", CultureInfo.InvariantCulture) + " m");
                ok &= reached;

                ok &= Same(report, "roots seen past R + tolerance, any step", outsideEver, 0);
                ok &= Within(report, "furthest any root got from the axis",
                    worstRadius, radius + WallToleranceMetres);

                // 0 by construction, and the column is kept in the report precisely so that a
                // reader can see that it is (SharedVolume.TryWrap).
                ok &= Same(report, "wraps in a tank", (float)eco.Wraps, 0f);

                // A body thrown through the glass dies here as a counted death rather than riding
                // a gyre sampled at a radius the field was never built over. It should never fire.
                ok &= Same(report, "diverged", (int)eco.World.Diverged, 0);

                // The glass has to be reporting: five bodies pressed against it that produce no
                // pairs would mean the slabs are on a layer nothing hits, or providesContacts is
                // off, or they are facing the wrong way and the bodies are inside the prism.
                bool touched = eco.FloorContactPairs > 0;
                report.AppendLine(
                    (touched ? "- ok   " : "- FAIL ") + "the glass and the bed report contacts: " +
                    eco.FloorContactPairs + " pairs");
                ok &= touched;
            }
            finally
            {
                eco.DestroyAll();
                Physics.simulationMode = previousMode;
                Physics.gravity = previousGravity;
            }

            return ok;
        }

        /// <summary>Picks the bodies that will be walked into the glass.</summary>
        private static void Walk(
            Ecosystem eco, System.Collections.Generic.List<CreatureInstance> walkers)
        {
            foreach (CreatureInstance instance in eco.Instances)
            {
                if (walkers.Count >= Walkers) return;

                ArticulationBody[] bodies = instance.Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                // By reference, because this is asked again on every step until it has its five:
                // a world whose founding cohort arrived in two batches would otherwise carry the
                // first batch twice and count one body's journey as two.
                if (walkers.Contains(instance)) continue;

                walkers.Add(instance);
            }
        }

        /// <summary>
        /// Pushes each walked body straight at the nearest glass, one step's worth.
        /// </summary>
        /// <remarks>
        /// The root's velocity is set rather than a force added, so that what arrives at the wall
        /// is a known speed rather than whatever the drag model left of an impulse — the wall is
        /// what is under test and not the water. Outward from the axis, so a body walks the
        /// shortest way to the glass from wherever it was founded; a body exactly on the axis has
        /// no outward direction and is sent along +x.
        /// </remarks>
        private static void Push(
            System.Collections.Generic.List<CreatureInstance> walkers, float radius)
        {
            for (int i = 0; i < walkers.Count; i++)
            {
                if (walkers[i].Root == null) continue;

                ArticulationBody[] bodies = walkers[i].Bodies;
                if (bodies == null || bodies.Length == 0) continue;

                ArticulationBody root = bodies[0];
                Vector3 p = root.transform.position;

                float dx = p.x - radius;
                float dz = p.z - radius;
                float r = Mathf.Sqrt(dx * dx + dz * dz);

                Vector3 outward = r > 1e-3f
                    ? new Vector3(dx / r, 0f, dz / r)
                    : Vector3.right;

                root.linearVelocity = outward * WalkMetresPerSecond;
            }
        }

        /// <summary>
        /// Pushes ten bodies above the surface and ten into the sea bed, to see them come back.
        /// </summary>
        private static void Displace(
            Ecosystem eco,
            System.Collections.Generic.List<CreatureInstance> up,
            System.Collections.Generic.List<CreatureInstance> down)
        {
            float floor = -eco.Volume.DepthMetres - UnderMetres;

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
