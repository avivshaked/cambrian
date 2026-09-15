using System;
using System.Globalization;
using System.IO;
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
    /// Five parts, cheapest first. <b>Part 1</b> is the restoring boundary's arithmetic —
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
    /// <b>Part 5</b> is the throw trace (<c>logbook/specs/throw-trace-spec.md</c>), and it is not
    /// about the box at all: the ring and the divergence dump belong to <see cref="Ecosystem"/>,
    /// and this is the harness that builds one. One deliberately lopsided two-link body — 72 : 1
    /// across its joint — is inoculated into a tiled world, grown by the real growth path, and
    /// then condemned through <c>Ecosystem.CondemnForTest</c>, and what is asserted is the
    /// contents of the file that lands: three frames oldest to newest, the mass ratio and its
    /// per-link table, the step of the last resize, and the one frame the resize flagged.
    /// Since the spec's second pass it also forces the case the instrument was rebuilt for:
    /// the limb's angular velocity is made non-finite between two steps through
    /// <c>Ecosystem.InjectNonFiniteForTest</c>, and the dump must hold the three finite frames
    /// taken before it, unchanged, with <c>firstNonFiniteStep</c> naming the step after the
    /// injection and the water's three new vectors on every link of every frame.
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
                ok &= TheThrowTrace(report);
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
                worstIntoTheBed = Mathf.Max(worstIntoTheBed, bed.FloorYAt(at.x, at.z) - bottom);

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
                "- bed at y=" + bed.LowestTopY.ToString("0.##", CultureInfo.InvariantCulture) +
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
                                worstBuiltIntoTheBed, eco.Floor.FloorYAt(p.x, p.z) - (p.y - radius));
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

                        Vector3 root = bodies[0].transform.position;
                        float y = root.y;
                        if (float.IsNaN(y) || float.IsInfinity(y)) continue;

                        // The floor under this body rather than a single depth — D092. On the flat
                        // bed part 3 runs in, every place gives −D and the number is what it was.
                        deepestNow = Mathf.Max(deepestNow, eco.Floor.FloorYAt(root.x, root.z) - y);
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
            float worstReservedSphere = 0f;
            int reservedSpheresChecked = 0;
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
                float centreDistance = Mathf.Sqrt(dx * dx + dz * dz);
                furthest = Mathf.Max(furthest, centreDistance);

                // wall-clearance-spec.md item 5: the centre alone is not what Free tests any
                // more, so the smoke should not either — the reserved sphere (centre radius
                // plus the body's own bounding radius) is what has to stay at or below R.
                float bodyRadius = SharedVolume.BoundingRadius(body);
                worstReservedSphere = Mathf.Max(worstReservedSphere, centreDistance + bodyRadius);
                reservedSpheresChecked++;
            }

            report.AppendLine(
                "- placed " + placed + " founders over the disc, " + volume.Rejections +
                " rejected attempts; furthest from the axis " +
                furthest.ToString("0.###", CultureInfo.InvariantCulture) + " m of " +
                radius.ToString("0.###", CultureInfo.InvariantCulture) + "; by ring " +
                string.Join("/", System.Array.ConvertAll(ringCount, c => c.ToString(CultureInfo.InvariantCulture))));
            report.AppendLine(
                "- " + reservedSpheresChecked + " reserved spheres checked against the glass, " +
                "worst centre + bounding radius " +
                worstReservedSphere.ToString("0.####", CultureInfo.InvariantCulture) + " m of " +
                radius.ToString("0.####", CultureInfo.InvariantCulture) + " m");

            ok &= Same(report, "founders placed outside the glass", outside, 0);
            ok &= Same(report, "founders placed in a ring that does not exist", badRing, 0);
            ok &= Within(
                report, "founders' reserved sphere (centre + bounding radius) from the axis",
                worstReservedSphere, radius);

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
            ok &= TheShapedBed(report);

            report.AppendLine();
            return ok;
        }

        /// <summary>The footprint the shaped bed is read at, m² — D092.</summary>
        /// <remarks>
        /// Round 38's own tank rather than part 4's 100 m², and the reason is the tilt: a ramp's
        /// slope is its dial over the diameter, so six metres across an 11.3 m tank is 28° and
        /// <see cref="BedShape.SteepestTiltSlope"/> refuses it, while across a 22.6 m one it is
        /// 15° and is the floor the round will actually run on.
        /// </remarks>
        private const float BedArea = 400f;

        /// <summary>The relief and the tilt the bed case builds, m — the spec's visible floor.</summary>
        private const float BedRelief = 4f;

        private const float BedTilt = 6f;

        /// <summary>Steps the buried body is given to come out or be killed.</summary>
        /// <remarks>
        /// The floor's guard runs at the metabolic cadence, not every step
        /// (<c>Ecosystem.CheckFinite</c>), so this has to be several of those: three hundred steps
        /// at dt 0.01 is six metabolic steps, and the body is put two metres under the rock, which
        /// is far past its own radius and therefore past the guard on the first of them.
        /// </remarks>
        private const int BedSteps = 300;

        /// <summary>The seed the bed case's floor is drawn from.</summary>
        /// <remarks>
        /// <b>Fixed, and chosen from the map it produces.</b> The hollow count is a property of
        /// the mode draw, so at 400 m² with a 4 m dial some seeds give three hollows and some give
        /// none (<see cref="BedShape.Hollows"/>'s rule) — and a smoke that asserted "the map has
        /// hollows" against an arbitrary seed would be asserting the draw rather than the
        /// mechanism. Seed 3 gives three hollows and one ridge, and both halves of the case use
        /// it, so the floor the placer is read against is the floor the world builds.
        /// </remarks>
        private const ulong BedSeed = 3UL;

        /// <summary>
        /// The floor with a shape in it — D092, <c>logbook/specs/bed-spec.md</c> items 10 and 11.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Two halves, as part 4 has.</b> First the placer against a real height map with no
        /// scene: every founder the disc draw sets down has to clear the rock <i>under its own
        /// column</i>, which is the one thing a single global clamp could not do, and the two
        /// founders that landed on the highest and the lowest floor are named so that the reading
        /// is about the extremes rather than about the average. Then a world with rock in it: a
        /// body teleported two metres into a hollow must either come back out or die as the
        /// counted <c>Diverged</c> death the guard makes of it, and the header token has to carry
        /// the floor it ran on.
        /// </para>
        /// <para>
        /// <b>And the flat token, unchanged.</b> Part 4's <c>TheGlass</c> already asserts the whole
        /// string; this asserts the one word that changed, at relief 0, so that a build which
        /// accidentally made the shaped token the general case fails here rather than in a
        /// header-to-header comparison across rounds.
        /// </para>
        /// </remarks>
        private static bool TheShapedBed(StringBuilder report)
        {
            report.AppendLine("**4b. The shaped bed** (`BedShape`, `SeaFloor`, D092)");
            report.AppendLine();

            bool ok = true;

            float radius = TankGeometry.RadiusFor(BedArea);

            var shape = new BedShape(
                radius, 60f, BedRelief, BedTilt, 0f, Rng.SeedFor(BedSeed, World.BedShapeIndex));

            report.AppendLine("- " + shape);
            report.AppendLine(
                "- highest floor " +
                (-60d + shape.HighestMetres).ToString("0.##", CultureInfo.InvariantCulture) +
                " m, lowest " +
                (-60d + shape.LowestMetres).ToString("0.##", CultureInfo.InvariantCulture) +
                " m, total range " +
                shape.TotalRangeMetres.ToString("0.##", CultureInfo.InvariantCulture) + " m");

            // The spec asks the smoke to be able to say it (item 4), and a floor with no hollow in
            // it is a floor the round cannot read pockets off.
            bool hollowed = shape.Hollows > 0;
            report.AppendLine(
                (hollowed ? "- ok   " : "- FAIL ") + "the map has hollows: " + shape.Hollows +
                " hollows and " + shape.Ridges + " ridges");
            ok &= hollowed;

            var volume = new SharedVolume(
                patchCount: TankRings,
                patchWidthMetres: Mathf.Sqrt(BedArea / TankRings),
                depthMetres: 60f,
                seed: 1,
                offspringDispersalMetres: 0f,
                patchesAcross: 1,
                shape: WorldShape.Tank,
                tankRadiusMetres: radius);

            SeaFloor floor = SeaFloor.Build(volume, null, shape);
            volume.Floor = floor;

            try
            {
                ok &= TheShapedPlacer(report, volume, floor, shape);
            }
            finally
            {
                floor.Destroy();
                volume.Floor = null;
            }

            report.AppendLine();
            ok &= TheBuriedBody(report);

            return ok;
        }

        /// <summary>The placer against a shaped floor, with no scene — the first half of 4b.</summary>
        private static bool TheShapedPlacer(
            StringBuilder report, SharedVolume volume, SeaFloor floor, BedShape shape)
        {
            bool ok = true;

            report.AppendLine(
                "- the collider: " +
                (floor.Surface != null ? floor.Surface.sharedMesh.triangles.Length / 3 : 0) +
                " triangles at " + SeaFloor.LatticeMetres +
                " m, convex " + (floor.Surface != null && floor.Surface.convex) +
                ", lowest rock " +
                floor.LowestTopY.ToString("0.##", CultureInfo.InvariantCulture) +
                " m, backstop top " +
                (floor.LowestTopY - SeaFloor.BackstopClearanceMetres)
                    .ToString("0.##", CultureInfo.InvariantCulture) + " m");

            var config = new RunConfig();

            int placed = 0;
            int inTheRock = 0;
            float worstIntoTheRock = 0f;

            // The two extremes of the floor that a founder actually landed on, with the clearance
            // each of them kept. Tracked rather than chosen, because the disc draw is the placer's
            // and a founder put somewhere by hand would be testing this test.
            float highestFloor = float.MinValue, lowestFloor = float.MaxValue;
            float highestClearance = 0f, lowestClearance = 0f;

            for (int i = 0; i < Founders; i++)
            {
                var rng = new Rng(Rng.SeedFor(8484UL, (ulong)i));
                Genome genome = GenomeFactory.Founder(rng, config.Genome, config.SensorPool());
                Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
                if (body.PartCount == 0) continue;

                // The whole 60 m, as the reference world's founder lottery draws it: most of these
                // land inside the rock and every one of them has to come back raised.
                float height = -rng.Range(0f, 60f);

                if (!volume.TryReserveFounder(body, ref height, out int _)) continue;

                volume.Commit(i);
                volume.TryTakePlacement(i, out Vector3 at);

                placed++;

                float bodyRadius = SharedVolume.BoundingRadius(body);
                float floorHere = floor.FloorYAt(at.x, at.z);
                float clearance = at.y - floorHere;

                if (clearance < bodyRadius)
                {
                    inTheRock++;
                    worstIntoTheRock = Mathf.Max(worstIntoTheRock, bodyRadius - clearance);
                }

                if (floorHere > highestFloor)
                {
                    highestFloor = floorHere;
                    highestClearance = clearance - bodyRadius;
                }

                if (floorHere < lowestFloor)
                {
                    lowestFloor = floorHere;
                    lowestClearance = clearance - bodyRadius;
                }
            }

            CultureInfo c = CultureInfo.InvariantCulture;

            report.AppendLine(
                "- placed " + placed + " founders over a shaped disc; the highest floor any of " +
                "them landed on was " + highestFloor.ToString("0.##", c) + " m (of " +
                (-60d + shape.HighestMetres).ToString("0.##", c) + " m) with " +
                highestClearance.ToString("0.###", c) + " m over its own radius, the lowest " +
                lowestFloor.ToString("0.##", c) + " m (of " +
                (-60d + shape.LowestMetres).ToString("0.##", c) + " m) with " +
                lowestClearance.ToString("0.###", c) + " m");

            ok &= Same(report, "founders left with their sphere inside the rock", inTheRock, 0);

            if (inTheRock > 0)
            {
                report.AppendLine(
                    "  worst " + worstIntoTheRock.ToString("0.####", c) + " m into the floor");
            }

            // The draw has to have reached both ends of the floor, or the two clearances above are
            // statements about the middle of a map whose extremes were never visited.
            float span = highestFloor - lowestFloor;
            bool reached = placed > 0 && span > 0.5d * shape.TotalRangeMetres;
            report.AppendLine(
                (reached ? "- ok   " : "- FAIL ") + "the draw reached the floor's range: " +
                span.ToString("0.##", c) + " m of " +
                shape.TotalRangeMetres.ToString("0.##", c) + " m");
            ok &= reached;

            return ok;
        }

        /// <summary>A body put inside a hollow, and the header's token — the second half of 4b.</summary>
        private static bool TheBuriedBody(StringBuilder report)
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
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                HorizontalPatches = TankRings,
                WorldAreaSquareMetres = BedArea,
                WorldDepthMetres = 60f,
                FounderDepthSpread = 60f,
                BedReliefMetres = BedRelief,
                BedTiltMetres = BedTilt,
                MinimumPopulation = TankFounders,
                FloorSpawnsPerStep = TankFounders,
                MaximumPopulation = 4000,
            };

            config.Fluid.TissueExcessDensity = ExcessDensity;
            config.Fluid.SurfaceRestoringFraction = 1f;

            var eco = new Ecosystem(config, seed: BedSeed);
            bool ok = true;

            try
            {
                string token = EvolutionRun.SpaceToken(config, eco);
                bool tokenOk = token.Contains("bed relief") && token.Contains("hollows");

                report.AppendLine(
                    (tokenOk ? "- ok   " : "- FAIL ") + "the header's space token names the " +
                    "floor: `" + token + "`");
                ok &= tokenOk;

                // The seven numbers run.json copies out of the world (D092). Asserted at their
                // source rather than by writing a manifest, which is EvolutionRun's own business
                // and needs a run directory: what a smoke can say is that the world has them and
                // that they are the ones the launcher asked for.
                BedShape bed = eco.World.Bed;
                bool manifestOk =
                    bed != null && bed.HasRelief &&
                    Mathf.Approximately(bed.ReliefMetres, BedRelief) &&
                    Mathf.Approximately(bed.TiltMetres, BedTilt) &&
                    bed.ScaleMetres > 0f && bed.RangeMetres > 0d && bed.Hollows >= 0;

                report.AppendLine(
                    (manifestOk ? "- ok   " : "- FAIL ") + "the manifest's bed fields: relief " +
                    (bed != null ? bed.ReliefMetres : 0f) + ", tilt " +
                    (bed != null ? bed.TiltMetres : 0f) + ", scale " +
                    (bed != null ? bed.ScaleMetres : 0f) + ", hollows " +
                    (bed != null ? bed.Hollows : 0) + ", ridges " +
                    (bed != null ? bed.Ridges : 0));
                ok &= manifestOk;

                // Founding, so there is a body to bury.
                for (int step = 1; step <= Ecosystem.StepsPerMetabolicStep * 2; step++) eco.Step();

                CreatureInstance buried = null;

                foreach (CreatureInstance instance in eco.Instances)
                {
                    ArticulationBody[] bodies = instance.Bodies;
                    if (bodies == null || bodies.Length == 0) continue;

                    Vector3 p = bodies[0].transform.position;

                    // Two metres under the rock where it stands: far past any body's own radius,
                    // which is the threshold the guard uses, and inside the mesh rather than under
                    // the backstop.
                    var target = new Vector3(p.x, eco.Floor.FloorYAt(p.x, p.z) - 2f, p.z);

                    bodies[0].TeleportRoot(target, bodies[0].transform.rotation);
                    bodies[0].linearVelocity = Vector3.zero;

                    buried = instance;
                    break;
                }

                bool haveOne = buried != null;
                report.AppendLine(
                    (haveOne ? "- ok   " : "- FAIL ") + "a body to bury: " +
                    eco.World.Living.Count + " alive after founding");
                ok &= haveOne;

                if (haveOne)
                {
                    long divergedBefore = eco.World.Diverged;
                    bool resolved = false;
                    float deepest = 2f;

                    for (int step = 1; step <= BedSteps && !resolved; step++)
                    {
                        eco.Step();

                        // Killed by the floor's guard is one of the two right answers, and the one
                        // two metres under the rock should produce.
                        if (eco.World.Diverged > divergedBefore) { resolved = true; break; }

                        if (buried.Root == null) { resolved = true; break; }

                        ArticulationBody[] bodies = buried.Bodies;
                        if (bodies == null || bodies.Length == 0) { resolved = true; break; }

                        Vector3 p = bodies[0].transform.position;
                        float sum = p.x + p.y + p.z;
                        if (float.IsNaN(sum) || float.IsInfinity(sum)) { resolved = true; break; }

                        float under = eco.Floor.FloorYAt(p.x, p.z) - p.y;
                        deepest = Mathf.Min(deepest, under);

                        // Or it came back out: at or above the rock under it, which is the other
                        // right answer and the one the depenetration cap gives a shallow burial.
                        if (under <= 0f) { resolved = true; break; }
                    }

                    report.AppendLine(
                        (resolved ? "- ok   " : "- FAIL ") + "a body two metres inside the rock " +
                        "came out or was killed within " + BedSteps + " steps: " +
                        (eco.World.Diverged > divergedBefore
                            ? "killed as a counted Diverged death"
                            : "still under the floor by " +
                              deepest.ToString("0.###", CultureInfo.InvariantCulture) + " m"));
                    ok &= resolved;
                }
            }
            finally
            {
                eco.DestroyAll();
                Physics.simulationMode = previousMode;
                Physics.gravity = previousGravity;
            }

            // And the flat token is the string it always was — the invariant every header in the
            // record is read against.
            ok &= TheFlatToken(report);

            return ok;
        }

        /// <summary>A tank at relief 0 still prints the word <c>bed</c> and nothing else.</summary>
        private static bool TheFlatToken(StringBuilder report)
        {
            SimulationMode previousMode = Physics.simulationMode;

            Physics.simulationMode = SimulationMode.Script;
            Ecosystem.ConfigurePhysicsStep(FixedDt);

            var config = new RunConfig
            {
                Light = new LightModel(200f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                HorizontalPatches = TankRings,
                WorldAreaSquareMetres = TankArea,
                WorldDepthMetres = 60f,
                MinimumPopulation = 0,
                FloorSpawnsPerStep = 0,
            };

            var eco = new Ecosystem(config, seed: 77);

            try
            {
                string token = EvolutionRun.SpaceToken(config, eco);
                const string wanted = "tank r=5.64 m (100 m2), depth 60, wall, bed";

                bool tokenOk = token == wanted;
                report.AppendLine(
                    (tokenOk ? "- ok   " : "- FAIL ") + "a flat tank's token is unchanged: `" +
                    token + "`" + (tokenOk ? "" : " — wanted `" + wanted + "`"));

                return tokenOk;
            }
            finally
            {
                eco.DestroyAll();
                Physics.simulationMode = previousMode;
            }
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

                // wall-clearance-spec.md item 5: the reserved sphere, checked on real bodies
                // rather than on the no-scene arithmetic above — every founder and every
                // offspring born during the walk, the first step it is seen, since that is the
                // step nearest the moment its spot was reserved and before growth (D087) has
                // moved its own radius away from what was reserved.
                var seenForReservation = new System.Collections.Generic.HashSet<EntityId>();
                int reservationsChecked = 0;
                float worstReservedSphere = 0f;

                // Item 5's second half: not the root alone but every part, at every step, each
                // against its own bounding extent — the root-only check above answers for the
                // placer; this one answers for a limb that has swum out past where its root sits.
                int partsChecked = 0;
                float worstPartRadius = 0f;

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

                        if (instance.Root != null && seenForReservation.Add(instance.Root.GetEntityId()))
                        {
                            reservationsChecked++;

                            float bodyRadius = SharedVolume.BoundingRadius(instance.Phenotype);
                            worstReservedSphere = Mathf.Max(worstReservedSphere, r + bodyRadius);
                        }

                        Phenotype phenotype = instance.Phenotype;

                        for (int b = 0; b < bodies.Length; b++)
                        {
                            Vector3 partPosition = bodies[b].transform.position;
                            float partSum = partPosition.x + partPosition.z;
                            if (float.IsNaN(partSum) || float.IsInfinity(partSum)) continue;

                            float partDx = partPosition.x - radius;
                            float partDz = partPosition.z - radius;
                            float partCentreDistance = Mathf.Sqrt(partDx * partDx + partDz * partDz);

                            float partExtent = phenotype != null && b < phenotype.PartCount
                                ? PartExtent(phenotype.Parts[b])
                                : 0f;

                            worstPartRadius = Mathf.Max(worstPartRadius, partCentreDistance + partExtent);
                            partsChecked++;
                        }
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
                report.AppendLine(
                    "- " + reservationsChecked + " reserved spheres checked (founders and " +
                    "offspring born in the walk, first sight), worst centre + bounding radius " +
                    worstReservedSphere.ToString("0.####", CultureInfo.InvariantCulture) + " m of " +
                    radius.ToString("0.####", CultureInfo.InvariantCulture) + " m");
                report.AppendLine(
                    "- " + partsChecked + " part placements checked across the run, worst " +
                    "position + own bounding extent " +
                    worstPartRadius.ToString("0.####", CultureInfo.InvariantCulture) + " m of " +
                    radius.ToString("0.####", CultureInfo.InvariantCulture) + " m (tolerance R + " +
                    WallToleranceMetres + " m)");

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

                // wall-clearance-spec.md item 5: the reserved sphere is the placer's own promise
                // and carries no wall tolerance — it is arithmetic Free already enforced, not a
                // contact with the prism's corners.
                ok &= Within(
                    report, "reserved spheres (centre + bounding radius) from the axis",
                    worstReservedSphere, radius);

                // Every part, not just the root — a limb can reach further than the root it
                // hangs from. This one does carry the wall tolerance, because a part actually
                // touching the glass is a contact with the twelve-millimetre prism, not with the
                // mathematical circle the placer reasons about.
                ok &= Within(
                    report, "every part's own position + bounding extent from the axis",
                    worstPartRadius, radius + WallToleranceMetres);

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

        /// <summary>
        /// One part's own bounding extent, m: its half-diagonal about its own centre.
        /// </summary>
        /// <remarks>
        /// <c>SharedVolume.BoundingRadius</c>'s per-part term, read on its own rather than
        /// maximised over the whole phenotype about the root — wall-clearance-spec.md item 5
        /// asks of every part's own world position, not the root's, so this is the piece of that
        /// method a part-level check needs.
        /// </remarks>
        private static float PartExtent(PhenotypePart part)
        {
            Float3 h = part.HalfExtents;
            return Mathf.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);
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

        // --------------------------------------------------------------- part 5: the throw trace

        /// <summary>
        /// Physics step part 5 runs at, seconds.
        /// </summary>
        /// <remarks>
        /// <b>Coarse on purpose, and it is the only way this part can assert the resize flag.</b>
        /// <c>Ecosystem.CheckFinite</c> runs once per metabolic step, so at the smoke's own 0.01 s
        /// the divergence is caught fifty steps after the resize and the one frame the resize
        /// flagged has long been overwritten in a three-frame ring. At 0.25 s a metabolic step is
        /// two physics steps, so the flagged frame is still held when the dump is written. Nothing
        /// in this part asserts a physical quantity — it asserts what is in a file — so the step
        /// is free to be whatever makes the timing legible. Restored afterwards, because the
        /// setting is process-wide.
        /// </remarks>
        private const float TraceDt = 0.25f;

        /// <summary>
        /// Does the throw trace record and dump what it claims to? —
        /// <c>logbook/specs/throw-trace-spec.md</c>'s smoke.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One body, built deliberately lopsided.</b> A 1 m³ root and a 0.24 m limb put about
        /// 72 : 1 across the one joint, which is seven times the ratio PhysX's joint documentation
        /// says to stay under — so the ratio instrument has something to read and the
        /// over-ten count has something to count. The limb is a link with capacity and no
        /// neurons, so nothing drives it: the trace is under test here, not swimming.
        /// </para>
        /// <para>
        /// <b>Tiled rather than shared, in a smoke about the shared box.</b> The trace is not a
        /// box rule — it reads articulations — and one body on the lattice is the smallest harness
        /// that exercises every line of it. It lives in this file rather than in
        /// <c>Milestone1Smoke</c> because the ring and the dump belong to <c>Ecosystem</c>, and
        /// this is the harness that builds one.
        /// </para>
        /// <para>
        /// <b>The world is arranged so that exactly one thing happens.</b> Reproduction is priced
        /// out of reach with a per-offspring overhead of a gigajoule, the population floor is
        /// switched off, and the inoculant is given enough energy to reach its adult size in a
        /// single growth pass — so there is one body, one resize, and one divergence, at steps the
        /// assertions can name. The overhead and the floor are knobs of the world; nothing here
        /// touches the trace's own behaviour.
        /// </para>
        /// </remarks>
        private static bool TheThrowTrace(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("**5. The throw trace** (`logbook/specs/throw-trace-spec.md`)");
            report.AppendLine();

            SimulationMode previousMode = Physics.simulationMode;
            Vector3 previousGravity = Physics.gravity;

            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);
            Ecosystem.ConfigurePhysicsStep(TraceDt);

            var config = new RunConfig
            {
                Light = new LightModel(200f, 12f),
                SharedSpace = false,
                HorizontalPatches = 1f,
                WorldAreaSquareMetres = 400f,
                WorldDepthMetres = 60f,

                // Nothing but the inoculant: no floor trickle, and a reproduction event priced
                // beyond any reserve this creature can hold.
                MinimumPopulation = 0,
                FloorSpawnsPerStep = 0,
                PerOffspringOverheadJoules = 1e9f,

                // Grown to adult in one pass on the first metabolic step: the growth cadence is
                // one metabolic step, the reserve keeps nothing back, and the purse is far larger
                // than the body costs.
                GrowthStepSeconds = Ecosystem.MetabolicStepSeconds,
                GrowthReserveFloor = 0f,
                FounderEnergyJoules = 100000f,

                // The newborn mass floor would refuse nothing at these sizes, and is switched off
                // so that the fixture's dimensions are the only thing deciding what is built.
                MinNewbornPartKilograms = 0f,

                // A record rather than a rule — nothing in Core reads it — but a config that
                // said 0.01 while the solver ran at 0.25 would mislead anyone who read it back.
                PhysicsStepSeconds = TraceDt,
            };

            var eco = new Ecosystem(config, seed: 91);
            string dumpDirectory = TraceDumpDirectory();
            eco.DivergenceDumpDirectory = dumpDirectory;

            bool ok = true;

            try
            {
                if (Directory.Exists(dumpDirectory)) Directory.Delete(dumpDirectory, recursive: true);

                eco.World.Inoculate(HeavyRootAndLightLimb(), 1, -2f);

                // One step to give the inoculant a body. Reconcile builds it at the top of the
                // step, so the ring exists and holds its first frame by the time this returns.
                eco.Step();

                if (eco.World.Living.Count != 1 || eco.Instances.Count != 1)
                {
                    report.AppendLine(
                        "- FAIL the fixture did not produce exactly one body: " +
                        eco.World.Living.Count + " alive, " + eco.Instances.Count + " built");
                    return false;
                }

                long id = eco.World.Living[0].Id;
                CreatureInstance instance = eco.Instances[0];

                bool built = Same(report, "links built", instance.Bodies.Length, 2);
                ok &= built;
                ok &= Same(report, "actuated DOF", instance.TotalDof, 1);

                // Everything below reads link 1 by index, so a body that developed into something
                // else is reported and abandoned rather than indexed off the end.
                if (!built) return false;

                float ratioBefore = instance.MaxJointMassRatio;
                report.AppendLine(
                    "- note mass ratio at birth: " +
                    ratioBefore.ToString("0.##", CultureInfo.InvariantCulture) + " : 1 across " +
                    instance.LinkMasses[0].ToString("0.###", CultureInfo.InvariantCulture) +
                    " kg and " +
                    instance.LinkMasses[1].ToString("0.###", CultureInfo.InvariantCulture) + " kg");

                bool overTen = ratioBefore > 10f;
                report.AppendLine(
                    (overTen ? "- ok   " : "- FAIL ") +
                    "the fixture is over the ratio PhysX's joints documentation avoids (10)");
                ok &= overTen;

                ok &= Same(report, "bodies over ratio 10", (int)eco.BodiesOverMassRatio10, 1);

                // The growth path, not a hand-made resize: World.Grow moves the joules and scales
                // the phenotype, and Ecosystem.ApplyGrowth calls PhenotypeBuilder.Resize.
                long resizeStep = -1;
                for (int s = 0; s < 200 && resizeStep < 0; s++)
                {
                    eco.Step();
                    if (eco.Resizes > 0) resizeStep = eco.Steps;
                }

                if (resizeStep < 0)
                {
                    report.AppendLine("- FAIL the body never grew, so the resize path never ran");
                    return false;
                }

                // Everything below reads the body and the creature. A world of one animal that
                // has lost it has nothing left to assert, and reading a destroyed articulation
                // would throw rather than fail.
                if (eco.World.Living.Count != 1)
                {
                    report.AppendLine(
                        "- FAIL the body did not survive to be condemned: " +
                        eco.World.Living.Count + " alive");
                    return false;
                }

                report.AppendLine(
                    "- ok   resized by the growth path at step " + resizeStep + " (" +
                    eco.Resizes + " resize(s), body fraction " +
                    eco.World.Living[0].BodyFraction.ToString("0.###", CultureInfo.InvariantCulture) +
                    ")");

                float ratioAfter = instance.MaxJointMassRatio;

                // Growth scales every link by one length, so the ratio is a property of the
                // genome and not of the body's size. Asserting that it survives the resize is
                // what says the resize path re-read the masses rather than leaving the birth
                // reading standing.
                bool ratioHeld = Mathf.Abs(ratioAfter - ratioBefore) <= 1e-2f * ratioBefore;
                report.AppendLine(
                    (ratioHeld ? "- ok   " : "- FAIL ") + "mass ratio after the resize: " +
                    ratioAfter.ToString("0.##", CultureInfo.InvariantCulture) +
                    " : 1, from masses " +
                    instance.LinkMasses[0].ToString("0.###", CultureInfo.InvariantCulture) +
                    " kg and " +
                    instance.LinkMasses[1].ToString("0.###", CultureInfo.InvariantCulture) +
                    " kg — a uniform scale leaves it where it was");
                ok &= ratioHeld;

                // One more step, so the frame that follows the resize is recorded and carries the
                // flag; then the divergence, so that frame is still in the three-frame ring when
                // the dump is written.
                eco.Step();

                // The forced case, the spec's second pass. From here the trace reads the limb's
                // angular velocity as NaN, which is what a body going non-finite between two
                // steps looks like to the ring. The three frames already in it were all finite,
                // so what the dump must show is those three unchanged and an onset naming the
                // first step that was not: the whole point of refusing a frame rather than
                // storing it, after logbook/0097 found 42 traces of 43 holding nothing but NaN.
                if (!eco.InjectNonFiniteForTest(id, 1))
                {
                    report.AppendLine("- FAIL the body was gone before the injection");
                    return false;
                }

                long lastFiniteStep = eco.Steps;

                // One step with the injection in place. The ring must refuse this frame, so the
                // newest frame it holds stays the one taken at lastFiniteStep.
                eco.Step();
                long injectedAt = eco.Steps;

                if (!eco.CondemnForTest(id))
                {
                    report.AppendLine("- FAIL the body was gone before it could be condemned");
                    return false;
                }

                string tracePath = Path.Combine(dumpDirectory, id + "-trace.json");

                for (int s = 0; s < 20 && !File.Exists(tracePath); s++) eco.Step();

                bool landed = File.Exists(tracePath);
                report.AppendLine(
                    (landed ? "- ok   " : "- FAIL ") + "trace written: " + tracePath);
                if (!landed) return false;

                ok &= Same(report, "diverged deaths", (int)eco.World.Diverged, 1);

                bool dumped = File.Exists(Path.Combine(dumpDirectory, id + ".json"));
                report.AppendLine(
                    (dumped ? "- ok   " : "- FAIL ") + "the post-mortem landed beside it");
                ok &= dumped;

                JsonNode trace = Json.Parse(File.ReadAllText(tracePath));

                ok &= Same(report, "frames held", trace["framesHeld"].AsInt(), 3);

                // Checked before anything indexes a frame: JsonNode's indexer throws on a missing
                // member rather than returning null, and a smoke that dies inside its own
                // assertion reports an exception where it should report a count.
                bool three = Same(report, "frames written", trace["frames"].Count, 3);
                ok &= three;
                if (!three) return false;

                ok &= Same(report, "links in the newest frame", trace["frames"][2]["links"].Count, 2);

                ok &= Same(
                    report, "trace mass ratio",
                    trace["maxJointMassRatio"].AsFloat(), ratioAfter);

                ok &= Same(
                    report, "last resize step",
                    (int)trace["lastResizeStep"].AsDouble(), (int)resizeStep);

                // Oldest to newest, so the steps must ascend and end at the step the check fired
                // on. This is the assertion that says the ring was unwound in the right order.
                bool ascending = true;
                var steps = new StringBuilder();

                for (int f = 0; f < trace["frames"].Count; f++)
                {
                    double step = trace["frames"][f]["step"].AsDouble();
                    if (f > 0) steps.Append(", ");
                    steps.Append(step.ToString("0", CultureInfo.InvariantCulture));

                    if (f > 0 && step <= trace["frames"][f - 1]["step"].AsDouble()) ascending = false;
                }

                report.AppendLine(
                    (ascending ? "- ok   " : "- FAIL ") +
                    "frames run oldest to newest: steps " + steps.ToString());
                ok &= ascending;

                // The resize flag names the first frame taken after the new anchors and masses
                // went in, which is the step after the resize itself.
                int flagged = 0;
                double flaggedStep = -1d;

                for (int f = 0; f < trace["frames"].Count; f++)
                {
                    if (!trace["frames"][f]["resizedJustBefore"].AsBool()) continue;

                    flagged++;
                    flaggedStep = trace["frames"][f]["step"].AsDouble();
                }

                ok &= Same(report, "frames flagged as post-resize", flagged, 1);
                ok &= Same(report, "the flagged frame's step", (int)flaggedStep, (int)resizeStep + 1);

                // ---- the forced case: what the ring did with a link that stopped being finite

                ok &= Same(report, "frames finite", trace["framesFinite"].AsInt(), 3);
                ok &= Same(
                    report, "the newest frame's step (nothing after the injection was stored)",
                    (int)trace["frames"][2]["step"].AsDouble(), (int)lastFiniteStep);
                ok &= Same(
                    report, "the first non-finite step",
                    (int)trace["firstNonFiniteStep"].AsDouble(), (int)injectedAt);
                ok &= Same(
                    report, "the first non-finite link", trace["firstNonFiniteLink"].AsInt(), 1);

                // Not asserted against a number: how many steps the body spent non-finite before
                // the check found it is the metabolic cadence's business, and the reading this
                // file has to support is only that it is a count and not a gap.
                double toDump = trace["stepsFromFirstNonFiniteToDump"].AsDouble();
                bool counted = toDump >= 0d;
                report.AppendLine(
                    (counted ? "- ok   " : "- FAIL ") +
                    "steps from the first non-finite to the dump: " +
                    toDump.ToString("0", CultureInfo.InvariantCulture));
                ok &= counted;

                // Every number in every held frame, including the nine the second pass added.
                // The claim the refusal makes is about all of them, so this reads all of them:
                // AsFloat throws on a non-finite value, which arrives as the quoted string
                // "NaN", so a frame that slipped through is a caught exception rather than a
                // silent pass.
                bool allFinite = true;
                bool forcesPresent = true;
                bool waterIdle = true;

                for (int f = 0; f < trace["frames"].Count; f++)
                {
                    JsonNode frameLinks = trace["frames"][f]["links"];

                    for (int b = 0; b < frameLinks.Count; b++)
                    {
                        JsonNode link = frameLinks[b];

                        forcesPresent &= link.Has("drag") && link.Has("accelForce") &&
                                         link.Has("waterAccel");
                        if (!forcesPresent) continue;

                        allFinite &= VectorIsFinite(link["position"]) &&
                                     VectorIsFinite(link["velocity"]) &&
                                     VectorIsFinite(link["angularVelocity"]) &&
                                     VectorIsFinite(link["drag"]) &&
                                     VectorIsFinite(link["accelForce"]) &&
                                     VectorIsFinite(link["waterAccel"]);

                        // This fixture runs at FluidAccelerationCoefficient 0, which is every
                        // config recorded before round 37b. Off must mean zero rather than
                        // whatever the arrays last held, which is the one claim the accessor
                        // makes that no arithmetic here can check.
                        waterIdle &= VectorIsZero(link["accelForce"]) &&
                                     VectorIsZero(link["waterAccel"]);
                    }
                }

                report.AppendLine(
                    (forcesPresent ? "- ok   " : "- FAIL ") +
                    "every frame's links carry drag, accelForce and waterAccel");
                ok &= forcesPresent;

                report.AppendLine(
                    (allFinite ? "- ok   " : "- FAIL ") +
                    "every number in every held frame is finite");
                ok &= allFinite;

                report.AppendLine(
                    (waterIdle ? "- ok   " : "- FAIL ") +
                    "the acceleration force and the water acceleration read 0 with the "
                    + "coefficient off");
                ok &= waterIdle;

                // The per-link masses and ratios, as the dump carries them.
                bool two = Same(report, "links in the trace's mass table", trace["links"].Count, 2);
                ok &= two;
                if (!two) return false;

                ok &= Same(
                    report, "the root's ratio (it has no joint)",
                    trace["links"][0]["jointMassRatio"].AsFloat(), 0f);
                ok &= Same(
                    report, "the limb's ratio against its parent",
                    trace["links"][1]["jointMassRatio"].AsFloat(), ratioAfter);
                ok &= Same(report, "the limb's joint DOF", trace["links"][1]["jointDof"].AsInt(), 1);

                bool aged = trace["ageSeconds"].AsFloat() > 0f;
                report.AppendLine(
                    (aged ? "- ok   " : "- FAIL ") + "the body's age is recorded: " +
                    trace["ageSeconds"].AsFloat().ToString("0.###", CultureInfo.InvariantCulture) +
                    " s");
                ok &= aged;
            }
            finally
            {
                eco.DestroyAll();
                Ecosystem.ConfigurePhysicsStep(FixedDt);
                Physics.simulationMode = previousMode;
                Physics.gravity = previousGravity;
            }

            return ok;
        }

        /// <summary>
        /// True when a trace vector's three members are all finite numbers.
        /// </summary>
        /// <remarks>
        /// A non-finite number arrives as the quoted string <c>"NaN"</c>, which is how
        /// <c>Ecosystem.Number</c> writes one so that a file about a divergence can hold it, and
        /// <c>JsonNode.AsFloat</c> throws on a string rather than returning 0. So the kind is
        /// tested before the value, and a frame that slipped a NaN past the ring's guard reads
        /// false here instead of reading as a legible zero.
        /// </remarks>
        private static bool VectorIsFinite(JsonNode vector) =>
            IsFiniteNumber(vector["x"]) && IsFiniteNumber(vector["y"]) &&
            IsFiniteNumber(vector["z"]);

        /// <summary>True when a trace vector is exactly zero on all three axes.</summary>
        private static bool VectorIsZero(JsonNode vector) =>
            VectorIsFinite(vector) &&
            vector["x"].AsFloat() == 0f && vector["y"].AsFloat() == 0f &&
            vector["z"].AsFloat() == 0f;

        private static bool IsFiniteNumber(JsonNode value)
        {
            if (value.Kind != JsonNode.NodeKind.Number) return false;

            float number = value.AsFloat();
            return !float.IsNaN(number) && !float.IsInfinity(number);
        }

        /// <summary>
        /// Where part 5's dumps go: <c>scratch/throw-trace-smoke/</c> under the repository.
        /// </summary>
        /// <remarks>
        /// <b>Inside the repository and gitignored</b> — the owner's rule (CLAUDE.md's
        /// conventions): nothing of the project's is written outside the tree, TEMP included, and
        /// a transient goes in <c>scratch/</c>. The root is found the way
        /// <c>SharedSpaceSpike.OutputDirectory</c> and <c>BuildIdentity.RepositoryRoot</c> find
        /// it: <c>EVOSIM_REPO_ROOT</c> if it is set, else the parent of this project, which is
        /// where <c>new-worker.ps1</c> puts a worker.
        /// </remarks>
        private static string TraceDumpDirectory()
        {
            string root = Environment.GetEnvironmentVariable("EVOSIM_REPO_ROOT");

            if (string.IsNullOrEmpty(root))
            {
                string project = Path.GetDirectoryName(Application.dataPath);
                root = Path.GetFullPath(Path.Combine(project, ".."));
            }

            return Path.Combine(root, "scratch", "throw-trace-smoke");
        }

        /// <summary>
        /// Part 5's fixture: a 1 m³ root carrying one 0.24 m link on a hinge — about 72 : 1 across
        /// the joint.
        /// </summary>
        /// <remarks>
        /// Hand-built rather than drawn from <c>GenomeFactory.RandomViable</c>, because the whole
        /// point is a known mass ratio: a random genome's ratio is whatever it is, and an
        /// assertion against it would be an assertion about the seed. The root photosynthesises
        /// so the creature is not dead on arrival; the link carries capacity because §5A.1 and
        /// <c>Genome.Validate</c> both refuse a joint without it, and no neuron reads it, so
        /// nothing drives the joint.
        /// </remarks>
        private static Genome HeavyRootAndLightLimb()
        {
            var genome = new Genome();

            var root = new MorphNode
            {
                Dimensions = new Float3(0.5f, 0.5f, 0.5f),
                JointType = JointType.Fixed,
                CellTypeId = CellTypeIds.Photosynthetic,
                Power = 0f,
                RecursiveLimit = 1,
            };
            root.ResampleJointLimits(new Float2(-1f, 1f));

            var limb = new MorphNode
            {
                Dimensions = new Float3(0.12f, 0.12f, 0.12f),
                JointType = JointType.Hinge,
                CellTypeId = CellTypeIds.Link,
                Power = 20f,
                RecursiveLimit = 1,
            };
            limb.ResampleJointLimits(new Float2(-1f, 1f));

            // The child's −X face onto the parent's +X face, the same attachment every hand-built
            // fixture in Evosim.Core.Tests uses.
            root.Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            genome.Nodes.Add(root);
            genome.Nodes.Add(limb);
            genome.RootIndex = 0;

            return genome;
        }
    }
}
