using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// The rock round 62's branching coral is drawn with (D194, <c>logbook/specs/coral-build.md</c>):
    /// each colony's capsules and plates welded into one mesh in the colony's own frame, under the
    /// same rock shader the mushroom reefs wear (<c>TheatreRock.shader</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One mesh a colony, not a game object a piece.</b> A coral of 30 colonies holds about
    /// 1,100 pieces; a holder and a renderer for each would be a draw call each. A colony's pieces
    /// are built into one vertex list in the frame whose origin is the trunk's foot
    /// (<see cref="CoralGeometry.Colony.X"/>, <see cref="CoralGeometry.Colony.BaseY"/>,
    /// <see cref="CoralGeometry.Colony.Z"/>), so the holder sits at the foot and every vertex is a
    /// small number. The index format goes to 32 bits past 65,000 vertices, as
    /// <see cref="TheatreReefRock"/> does.
    /// </para>
    /// <para>
    /// <b>Inside the physics' rock.</b> The physics reads the coral as the smooth union of its
    /// pieces (<see cref="CoralRock.OfColony"/>): a capsule's distance is a segment's less its
    /// radius, a plate's a disc's less its half thickness. Each piece here starts on that surface
    /// and is cut only inward, by a depth that is never negative and at most a quarter of a
    /// capsule's radius or three tenths of a plate's half thickness, so the bodies the physics
    /// pushes off the rock never show sunk into it. The union's fillet lies outside the drawn
    /// pieces and is left undrawn, which is the same direction of error.
    /// </para>
    /// <para>
    /// <b>The vertex colours are the reef cap's three readings</b> (<see cref="TheatreReefRock"/>):
    /// red is how deep the vertex was cut, green the analytic surface's upward component mapped
    /// to [0, 1], blue 1 everywhere, so the shader's crust lies on what faces up and its tone
    /// follows the colony's height.
    /// </para>
    /// <para>
    /// <b>The trunk's foot runs <see cref="FootBelowMetres"/> under the bed.</b> The coral stands
    /// on the landscape's floor, which a box with relief shapes by <see cref="BedShape.ForBox"/>,
    /// and the theatre's sand is flat wherever <see cref="SnapshotWorld.BuildFloor"/> builds no
    /// bed (every box). A trunk that began at the floor's height would hover over the flat sand
    /// where the floor stands high. The extension is buried where the floor stands low.
    /// </para>
    /// </remarks>
    public static class TheatreCoralRock
    {
        /// <summary>
        /// How far under its foot the trunk is drawn, m: more than a box's relief (round 62's is
        /// 1.5 m) so a trunk on a high floor still meets flat sand.
        /// </summary>
        public const float FootBelowMetres = 2f;

        /// <summary>The farthest the camera's rock distance looks for coral, m. Past it the answer is a bound.</summary>
        public const float LookMetres = 64f;

        /// <summary>The deepest a capsule is cut, as a share of its radius.</summary>
        private const float CapsuleCut = 0.25f;

        /// <summary>The deepest a plate is cut, as a share of its half thickness.</summary>
        private const float PlateCut = 0.30f;

        /// <summary>A tip's radius at its top end under <see cref="ShapeFurther"/>, as a share of its base's.</summary>
        private const float TipTaper = 0.55f;

        /// <summary>How much wider than the tapered tip the axial polyp swells, as a share of the tip's radius.</summary>
        private const float AxialSwell = 0.15f;

        /// <summary>How far back along the shaft the axial polyp's swelling begins, in the polyp's radii.</summary>
        private const float AxialSwellReach = 1.2f;

        /// <summary>How far each prong's axial cup leans off the branch's axis, radians.</summary>
        private const float ProngTilt = 40f * Mathf.Deg2Rad;

        /// <summary>An axial cup's angular radius about its own axis, radians.</summary>
        private const float AxialCupAngle = 22f * Mathf.Deg2Rad;

        /// <summary>An axial cup's depth at its middle, as a share of the polyp's radius.</summary>
        private const float AxialCupDepth = 0.22f;

        /// <summary>How much of the stone's cut the swollen polyp loses, so it reads smooth and rounded.</summary>
        private const float AxialSmooth = 0.6f;

        /// <summary>
        /// The deepest any vertex is cut under <see cref="ShapeFurther"/>, as a share of the branch's
        /// radius where it lies: the deepest the shape at 1 reaches, so no point passes the axis.
        /// </summary>
        private const float MaxCutShare = 0.85f;

        /// <summary>A plate's half thickness at its rim under <see cref="ShapeFurther"/>, as a share of the physics' half thickness.</summary>
        private const float RimThin = 0.45f;

        /// <summary>The share of a plate's radius from which it starts thinning toward the rim.</summary>
        private const float RimThinFrom = 0.35f;

        // The bush (ShapeBold). Lengths given "in radii" are in the physics capsule's radius R; on a plate,
        // in its half thickness.

        /// <summary>A branch's own core under the bush, as a share of its radius: the room outside it is the sprigs'.</summary>
        private const float BushCore = 0.4f;

        /// <summary>
        /// How far inside the physics piece every sprig and prong stays, as a share of the capsule's radius or
        /// the plate's half thickness, so no drawn tip ever touches the surface the bodies are pushed off.
        /// </summary>
        private const float BushMargin = 0.06f;

        /// <summary>How far back from a tip's end, in radii, the sprigs stop and leave the end to the prongs.</summary>
        private const float BushTipClear = 0.8f;

        /// <summary>
        /// Sprigs per radius of a branch's length: about one every 0.8 radii over the bush's outer surface. The
        /// budget's figure; at it the coral has a little over three times the further shape's vertices.
        /// </summary>
        private const float SprigsPerRadius = 9f;

        /// <summary>The most sprigs one piece carries, so a long thin piece stays in the budget.</summary>
        private const int SprigCap = 220;

        /// <summary>A sprig's radius at its root, in radii: buried in the core, whose cut surface lies at least 0.165 radii out where any sprig springs.</summary>
        private const float SprigRoot = 0.16f;

        /// <summary>A sprig's radius at its end, in radii.</summary>
        private const float SprigEnd = 0.12f;

        /// <summary>How far a sprig leans along its branch toward the branch's end, as a share of its outward direction.</summary>
        private const float SprigLean = 0.25f;

        /// <summary>How far a sprig turns up toward the light, as a share of its outward direction.</summary>
        private const float SprigRise = 0.35f;

        /// <summary>The spread of a sprig's direction, each component a draw in this much either way.</summary>
        private const float SprigJitter = 0.35f;

        /// <summary>The lowest a sprig may point (its direction's up component); one pointing lower is not grown, so a branch's underside is barer.</summary>
        private const float SprigDownmost = -0.3f;

        /// <summary>The shortest a sprig grows, as a share of the room it has to the capsule's surface.</summary>
        private const float SprigShortest = 0.7f;

        /// <summary>A sprig shorter than this, in radii, is not grown: it would barely clear the core.</summary>
        private const float SprigLeast = 0.5f;

        /// <summary>The farthest the search for a sprig's room looks, in radii.</summary>
        private const float SprigReachMost = 1.1f;

        /// <summary>How far along a branch a sprig's place may wander from its even spacing: a share of the spacing, half of it either way.</summary>
        private const float SprigAlongJitter = 0.8f;

        /// <summary>How far round a branch a sprig's bearing may wander from the golden-angle spiral, radians either way.</summary>
        private const float SprigTurnJitter = 0.3f;

        /// <summary>A sprig's sides: low-poly, as the shader's bumps and the depth of field carry the fine form.</summary>
        private const int SprigSides = 6;

        /// <summary>A sprig's tip reading at its end, for the bright tissue's pale tips: paler than the tissue, below a prong's.</summary>
        private const float SprigTipMark = 0.55f;

        /// <summary>The cavity reading (vertex red) at a sprig's root, so the bush darkens toward its core.</summary>
        private const float SprigRootShade = 0.6f;

        /// <summary>The fewest prongs crowning a tip.</summary>
        private const int ProngsLeast = 3;

        /// <summary>How many more prongs a tip may carry than <see cref="ProngsLeast"/>, plus one.</summary>
        private const int ProngsSpread = 3;

        /// <summary>The least a prong splays off its branch's axis, radians.</summary>
        private const float ProngSplayLow = 20f * Mathf.Deg2Rad;

        /// <summary>The most a prong splays off its branch's axis, radians.</summary>
        private const float ProngSplayHigh = 42f * Mathf.Deg2Rad;

        /// <summary>How far a prong's bearing may wander from the even spacing round the tip: a share of the spacing, half of it either way.</summary>
        private const float ProngBearingJitter = 0.35f;

        /// <summary>How far back from the tip's end the prongs spring from the axis, in radii.</summary>
        private const float ProngRootBack = 0.6f;

        /// <summary>A prong's radius at its root, in radii: no wider than the core's narrowest cut surface where it springs.</summary>
        private const float ProngRoot = 0.16f;

        /// <summary>A prong's radius at its end, in radii: the axial polyp's.</summary>
        private const float ProngEnd = 0.13f;

        /// <summary>How much of its room to the capsule's surface a prong fills.</summary>
        private const float ProngFill = 0.95f;

        /// <summary>The farthest the search for a prong's room looks, in radii.</summary>
        private const float ProngReachMost = 2.5f;

        /// <summary>A prong's sides: more than a sprig's, for the round axial cup that ends it.</summary>
        private const int ProngSides = 8;

        /// <summary>How far past a prong's end its cup's lip stands, and its radius there, in the end's radii.</summary>
        private const float CupLipAlong = 0.55f, CupLipRound = 0.8f;

        /// <summary>Where the cup's wall turns into its floor, along and across, in the end's radii.</summary>
        private const float CupWallAlong = 0.35f, CupWallRound = 0.45f;

        /// <summary>How far past the prong's end the cup's floor lies, in the end's radii: the cup is that deep below the lip.</summary>
        private const float CupFloorAlong = 0.12f;

        /// <summary>The ring round a sprig's rounded end, as a cosine and sine of its angle off the end's equator.</summary>
        private const float SprigCapCos = 0.70710678f, SprigCapSin = 0.70710678f;

        /// <summary>
        /// A plate's body under the bush, as a share of its half thickness: thinned and lowered to the bottom of
        /// the slab, so the mat above it stands inside the slab.
        /// </summary>
        private const float MatBody = 0.35f;

        /// <summary>Metres between neighbouring sprigs of a plate's mat.</summary>
        private const float MatSpacing = 0.11f;

        /// <summary>The most sprigs one plate's mat carries.</summary>
        private const int MatCap = 900;

        /// <summary>How far toward its outline a plate's mat reaches, as a share of the outline at each bearing.</summary>
        private const float MatReach = 0.88f;

        /// <summary>A mat sprig's radius at its root and at its end, in half thicknesses.</summary>
        private const float MatRoot = 0.22f, MatEnd = 0.16f;

        /// <summary>How far a mat sprig leans outward at the plate's rim, as a share of up; none at the middle.</summary>
        private const float MatSplay = 0.25f;

        /// <summary>The spread of a mat sprig's direction, each component a draw in this much either way.</summary>
        private const float MatJitter = 0.25f;

        /// <summary>How far a mat sprig's bearing may wander from the golden-angle spiral, radians either way.</summary>
        private const float MatTurnJitter = 0.25f;

        /// <summary>The shortest a mat sprig grows, as a share of its room to the slab's top.</summary>
        private const float MatShortest = 0.75f;

        /// <summary>A mat sprig shorter than this, in half thicknesses, is not grown.</summary>
        private const float MatLeast = 0.3f;

        /// <summary>The farthest the search for a mat sprig's room looks, in half thicknesses.</summary>
        private const float MatReachMost = 2.5f;

        /// <summary>A mat sprig's sides.</summary>
        private const int MatSides = 6;

        /// <summary>How far past its end a blunt sprig's pole stands, in its end radii.</summary>
        private const float BluntAlong = 0.5f;

        /// <summary>A mat sprig's tip reading at its end: low, so a plate's mat reads as its tissue with pale points, not as one sheet of tip colour.</summary>
        private const float MatTipMark = 0.25f;

        /// <summary>How many halvings the search for a sprig's room takes: a ten-thousandth of its range.</summary>
        private const int ReachSteps = 14;

        /// <summary>The golden angle, radians: sprigs round a branch and over a plate in its spiral, so no two rows line up.</summary>
        private const float GoldenAngle = 2.39996323f;

        /// <summary>The sprigs' own stream, salted apart from every other draw the coral makes.</summary>
        private const uint SprigStream = 0x5a17c0deu;

        /// <summary>The prongs' own stream.</summary>
        private const uint ProngStream = 0x3c6ef372u;

        /// <summary>The mat's own stream.</summary>
        private const uint MatStream = 0x1b873593u;

        /// <summary>
        /// Whether the coral is drawn (and, in a film window or a reconstruction, placed at all):
        /// <c>EVOSIM_THEATRE_CORAL</c>, 1 by default, 0 for off.
        /// </summary>
        public static bool Enabled => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The level a coral dial reads as its further look at: <c>EVOSIM_THEATRE_CORAL_LIVE</c> and
        /// <c>EVOSIM_THEATRE_CORAL_SHAPE</c> at 2. Up to 1 each dial is a share, as it always was;
        /// above 1 it rounds to a level, so a value that clamped to 1 before still draws as 1.
        /// </summary>
        public const float FurtherLevel = 2f;

        /// <summary>
        /// How far the coral reads as living coral rather than bare stone: <c>EVOSIM_THEATRE_CORAL_LIVE</c>,
        /// 0 (the stone, unchanged) by default, 1 for full tissue. A corallite in every cell, a tint
        /// per colony from <see cref="LivingTints"/>, paler growing tips toward the crown. At
        /// <see cref="FurtherLevel"/> this stays 1 and <see cref="LivingFurther"/> adds the rest.
        /// </summary>
        public static float Living => Mathf.Min(1f, LivingDial);

        /// <summary>
        /// <c>EVOSIM_THEATRE_CORAL_LIVE</c> at <see cref="FurtherLevel"/>: full tissue, plus a palette per
        /// colony from the reference board (<see cref="PaletteTint"/>) and bare skeleton on the lower,
        /// older parts of each colony (<see cref="BareTint"/>, <see cref="BareEdge"/>).
        /// </summary>
        public static bool LivingFurther => LivingDial >= FurtherLevel - 0.5f;

        /// <summary>
        /// The level a coral dial reads as its bold look at: <c>EVOSIM_THEATRE_CORAL_SHAPE</c> at 3 grows the
        /// bush (<see cref="ShapeBold"/>), <c>EVOSIM_THEATRE_CORAL_LIVE</c> at 3 the bright tissue
        /// (<see cref="LivingBold"/>). Each is laid over the further look, which stays on at 3.
        /// </summary>
        public const float BoldLevel = 3f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_CORAL_LIVE</c> at <see cref="BoldLevel"/>: the further look's palette pick and bare
        /// foot, with tissue about six times brighter than level 2's (<see cref="VividTint"/>) and pale tips that
        /// the bush's mesh marks (<see cref="ShapeBold"/>), so a colony reads lighter than the water behind it.
        /// </summary>
        public static bool LivingBold => LivingDial >= BoldLevel - 0.5f;

        /// <summary>The living dial as set, 0 to <see cref="BoldLevel"/>.</summary>
        private static float LivingDial => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_LIVE", 0f, 0f, BoldLevel);

        /// <summary>The tissue's brightness as a multiple of the palette's: <c>EVOSIM_THEATRE_CORAL_LIVE_GAIN</c>, 1 by default.</summary>
        public static float LivingGain => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_LIVE_GAIN", 1f, 0.1f, 4f);

        /// <summary>
        /// Metres per corallite: <c>EVOSIM_THEATRE_CORAL_POLYP</c>, 0.14 by default. A real one is a few
        /// millimetres to a centimetre; these colonies stand twenty metres tall, and 7 cm was lost
        /// under a portrait's depth of field.
        /// </summary>
        public static float PolypMetres => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_POLYP", 0.14f, 0.01f, 0.5f);

        /// <summary>
        /// The polyps out, as living coral has them by day: <c>EVOSIM_THEATRE_CORAL_POLYP_TUFTS</c>, 0 (off,
        /// the coral as before) by default, above 0 for on. A tuft of pale translucent tentacles in each
        /// corallite's cup and a faint soft fringe at the silhouette, all in the shader. Needs
        /// <see cref="Living"/> above 0, and draws nothing on the bare skeleton.
        /// </summary>
        public static bool PolypTufts => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_POLYP_TUFTS", 0f, 0f, 1f) > 0f;

        /// <summary>
        /// Living tissue's colours, linear, each with its growing tips: the browns and tans of a reef
        /// coral's symbionts, a dusty rose, a mauve, an olive. A colony takes one by its seed.
        /// </summary>
        private static readonly Vector4[] LivingTints =
        {
            new Vector4(0.050f, 0.032f, 0.016f, 1f), new Vector4(0.085f, 0.070f, 0.048f, 1f), // tan, cream tips
            new Vector4(0.062f, 0.022f, 0.030f, 1f), new Vector4(0.095f, 0.055f, 0.066f, 1f), // rose, pink tips
            new Vector4(0.040f, 0.020f, 0.060f, 1f), new Vector4(0.070f, 0.055f, 0.095f, 1f), // mauve, lilac tips
            new Vector4(0.030f, 0.046f, 0.016f, 1f), new Vector4(0.060f, 0.080f, 0.042f, 1f), // olive, lime tips
            new Vector4(0.062f, 0.034f, 0.010f, 1f), new Vector4(0.100f, 0.072f, 0.030f, 1f), // ochre, gold tips
        };

        /// <summary>
        /// How far the coral's shape departs from the physics' smooth pieces, always inward:
        /// <c>EVOSIM_THEATRE_CORAL_SHAPE</c>, 0 (the mesh as before, vertex for vertex) by default, 1 for
        /// full. Plates take a ragged, lobed edge; a branch with no branch above it takes a forked,
        /// knobbly tip; every capsule is ringed along its length so its lumps show along it. At
        /// <see cref="FurtherLevel"/> this stays 1 and <see cref="ShapeFurther"/> adds the rest.
        /// </summary>
        public static float Shape => Mathf.Min(1f, ShapeDial);

        /// <summary>
        /// <c>EVOSIM_THEATRE_CORAL_SHAPE</c> at <see cref="FurtherLevel"/>: the shape at 1, plus branches
        /// that taper toward their tips, a swollen axial polyp with a cup on each prong of a tip,
        /// plates thinning toward the rim, and in the shader corallite bumps along the branches and
        /// growth rings and radial ridges on the plates. Still inward only.
        /// </summary>
        public static bool ShapeFurther => ShapeDial >= FurtherLevel - 0.5f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_CORAL_SHAPE</c> at <see cref="BoldLevel"/>: a dense bush grown inside each piece. A
        /// branch's own core thins to <see cref="BushCore"/> of its radius and short sprigs grow from it out to
        /// the capsule's surface; a few splayed prongs, each with an axial cup, crown every tip; a plate thins
        /// and lowers, and a mat of short upright sprigs stands on its upper face. Every vertex stays inside the
        /// piece the physics holds.
        /// </summary>
        public static bool ShapeBold => ShapeDial >= BoldLevel - 0.5f;

        /// <summary>The shape dial as set, 0 to <see cref="BoldLevel"/>.</summary>
        private static float ShapeDial => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_SHAPE", 0f, 0f, BoldLevel);

        /// <summary>
        /// Metres per corallite bump along a branch under <see cref="ShapeFurther"/>, for the shader:
        /// a few per 10 cm, as on an Acropora branch, and far finer than the mesh's facets.
        /// </summary>
        public const float BumpMetres = 0.035f;

        /// <summary>
        /// Warmer crowns: <c>EVOSIM_THEATRE_CORAL_WARM</c>, 0 by default. Each colony's tissue turns
        /// toward a warm colour of its own (<see cref="WarmTints"/>) in the top few metres, where the
        /// water still carries red. Needs <see cref="Living"/> above 0.
        /// </summary>
        public static float Warm => TheatreSkin.Dial("EVOSIM_THEATRE_CORAL_WARM", 0f, 0f, 1f);

        /// <summary>The crowns' warm colours, linear: rose, purple, ochre, coral orange, magenta.</summary>
        private static readonly Vector4[] WarmTints =
        {
            new Vector4(0.110f, 0.030f, 0.045f, 1f),
            new Vector4(0.070f, 0.025f, 0.100f, 1f),
            new Vector4(0.105f, 0.060f, 0.012f, 1f),
            new Vector4(0.120f, 0.045f, 0.022f, 1f),
            new Vector4(0.100f, 0.020f, 0.075f, 1f),
        };

        /// <summary>A colony's warm crown colour, linear, scaled by <see cref="LivingGain"/>.</summary>
        public static Vector4 WarmTint(int colony)
        {
            int pick = Mathf.Min(WarmTints.Length - 1, (int)(SeedUnit(colony + 307) * WarmTints.Length));
            Vector4 warm = WarmTints[pick] * LivingGain * (0.85f + 0.3f * SeedUnit(colony + 401));
            warm.w = 1f;
            return warm;
        }

        /// <summary>A colony's tissue and tip colours, linear, scaled by <see cref="LivingGain"/>.</summary>
        public static void LivingTint(int colony, out Vector4 tissue, out Vector4 tip)
        {
            float seed = SeedUnit(colony + 101);
            int pick = Mathf.Min(LivingTints.Length / 2 - 1, (int)(seed * (LivingTints.Length / 2)));
            float gain = LivingGain * (0.85f + 0.3f * SeedUnit(colony + 211));
            tissue = LivingTints[2 * pick] * gain;
            tip = LivingTints[2 * pick + 1] * gain;
            tissue.w = 1f;
            tip.w = 1f;
        }

        /// <summary>
        /// The palette under <see cref="LivingFurther"/>, linear, each tissue with its growing tips,
        /// taken from the reference board (<c>scratch/owner/realism-refs/coral-*</c>): each hue from the
        /// photographs, each value and saturation held to <see cref="LivingTints"/>' range, tissue at a
        /// luminance near 0.03 to 0.04 and tips near 0.06 to 0.07, so nothing outshines today's coral
        /// under the grade.
        /// </summary>
        private static readonly Vector4[] PaletteTints =
        {
            new Vector4(0.052f, 0.028f, 0.017f, 1f), new Vector4(0.076f, 0.063f, 0.052f, 1f), // staghorn brown, pale tips
            new Vector4(0.043f, 0.035f, 0.023f, 1f), new Vector4(0.082f, 0.067f, 0.037f, 1f), // table tan, cream tips
            new Vector4(0.032f, 0.042f, 0.013f, 1f), new Vector4(0.060f, 0.070f, 0.039f, 1f), // olive, pale lime tips
            new Vector4(0.055f, 0.033f, 0.007f, 1f), new Vector4(0.083f, 0.067f, 0.037f, 1f), // golden brown, cream tips
            new Vector4(0.024f, 0.047f, 0.014f, 1f), new Vector4(0.055f, 0.074f, 0.048f, 1f), // green, pale green tips
            new Vector4(0.042f, 0.026f, 0.040f, 1f), new Vector4(0.070f, 0.058f, 0.066f, 1f), // dusky mauve, lilac tips: a saturated purple read blue-violet under the water
            new Vector4(0.049f, 0.027f, 0.031f, 1f), new Vector4(0.080f, 0.062f, 0.064f, 1f), // pink, pale pink tips
            new Vector4(0.044f, 0.033f, 0.018f, 1f), new Vector4(0.047f, 0.064f, 0.086f, 1f), // tan, blue tips
        };

        /// <summary>
        /// How often a colony takes each pair of <see cref="PaletteTints"/>: most in the browns and
        /// olives of living reef, a minority green, a few pink and a few with blue tips. The mauve
        /// pair is kept but drawn by none: under the water's blue-green light a colony in it read
        /// blue-violet, saturated or muted.
        /// </summary>
        private static readonly float[] PaletteShares = { 0.28f, 0.16f, 0.16f, 0.15f, 0.12f, 0.00f, 0.05f, 0.08f };

        /// <summary>
        /// The bare skeleton's colour, linear: a dull cream, below the brightest of today's growing tips.
        /// A neutral off-white at the tips' brightness took the water's blue and read violet beside the
        /// living tissue, so the blue channel is held lowest.
        /// </summary>
        private static readonly Vector4 BareSkeleton = new Vector4(0.062f, 0.054f, 0.040f, 1f);

        /// <summary>
        /// The barest colony's border, as an age (1 at the trunk's foot, 0 at the youngest tip): it is
        /// bare from its foot up to here, about the trunk's upper part.
        /// </summary>
        private const float BareEdgeLow = 0.75f;

        /// <summary>How far toward the foot a colony's border may sit above <see cref="BareEdgeLow"/>; at the most, only the trunk is bare.</summary>
        private const float BareEdgeSpread = 0.2f;

        /// <summary>
        /// The palette's own stream: a hash of the colony and a draw number salted apart from
        /// <see cref="SeedUnit"/>, so today's draws (<see cref="LivingTint"/>, <see cref="WarmTint"/>,
        /// the mesh's seeds) are not touched by any draw made here.
        /// </summary>
        private const uint PaletteStream = 0x2f6b1c3du;

        /// <summary>One draw of a colony's palette stream, in [0, 1).</summary>
        private static float PaletteUnit(int colony, int draw)
        {
            unchecked
            {
                uint h = (uint)colony * 0x85ebca6bu ^ (uint)draw * 0xc2b2ae35u ^ PaletteStream;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>
        /// A colony's tissue and tip colours under <see cref="LivingFurther"/>, linear, scaled by
        /// <see cref="LivingGain"/>: a pair of <see cref="PaletteTints"/> drawn by <see cref="PaletteShares"/>
        /// from the palette's own stream.
        /// </summary>
        public static void PaletteTint(int colony, out Vector4 tissue, out Vector4 tip)
        {
            int pick = PalettePick(colony);
            float gain = LivingGain * (0.85f + 0.3f * PaletteUnit(colony, 2));
            tissue = PaletteTints[2 * pick] * gain;
            tip = PaletteTints[2 * pick + 1] * gain;
            tissue.w = 1f;
            tip.w = 1f;
        }

        /// <summary>The bare skeleton's colour under <see cref="LivingFurther"/>, linear, scaled by <see cref="LivingGain"/>.</summary>
        public static Vector4 BareTint
        {
            get
            {
                Vector4 bare = BareSkeleton * LivingGain;
                bare.w = 1f;
                return bare;
            }
        }

        /// <summary>
        /// Where a colony's bare skeleton gives way to living tissue, as an age (1 at the trunk's foot,
        /// 0 at the youngest tip), from the palette's stream: some colonies bare only at the foot,
        /// some up into their first branches.
        /// </summary>
        public static float BareEdge(int colony) => BareEdgeLow + BareEdgeSpread * PaletteUnit(colony, 3);

        /// <summary>
        /// Which pair of <see cref="PaletteTints"/> a colony takes, by <see cref="PaletteShares"/> from the first
        /// draw of its palette stream: the further look's pick, which the bright look keeps.
        /// </summary>
        private static int PalettePick(int colony)
        {
            float draw = PaletteUnit(colony, 1);
            int pick = PaletteShares.Length - 1;
            float sum = 0f;
            for (int k = 0; k < PaletteShares.Length; k++)
            {
                sum += PaletteShares[k];
                if (draw < sum)
                {
                    pick = k;
                    break;
                }
            }

            return pick;
        }

        /// <summary>
        /// The tissue under <see cref="LivingBold"/>, linear, one for each pair of <see cref="PaletteTints"/>
        /// and taken by the same pick, so a colony keeps its hue from level 2: each about six times brighter
        /// (a luminance near 0.2, where level 2's sits near 0.035) and a little more saturated. Under the
        /// water's blue-green light the green channel carries most of what reaches the eye, so even the
        /// browns hold a green near 0.17 to 0.2.
        /// </summary>
        private static readonly Vector4[] VividTissues =
        {
            new Vector4(0.36f, 0.17f, 0.075f, 1f), // staghorn brown
            new Vector4(0.28f, 0.20f, 0.11f, 1f),  // table tan
            new Vector4(0.18f, 0.24f, 0.06f, 1f),  // olive
            new Vector4(0.34f, 0.19f, 0.035f, 1f), // golden brown
            new Vector4(0.10f, 0.25f, 0.06f, 1f),  // green
            new Vector4(0.26f, 0.15f, 0.24f, 1f),  // dusky mauve, drawn by none, as at level 2
            new Vector4(0.34f, 0.16f, 0.19f, 1f),  // pink
            new Vector4(0.28f, 0.20f, 0.10f, 1f),  // tan, under blue tips
        };

        /// <summary>
        /// The pale tips under <see cref="LivingBold"/>, linear, the brightest part of a colony: cream, lime and
        /// blue-white, at a luminance near 0.43 to 0.5: pale, and short of the white and the neon a brighter tip read as.
        /// </summary>
        private static readonly Vector4[] VividTips =
        {
            new Vector4(0.52f, 0.46f, 0.34f, 1f), // cream
            new Vector4(0.36f, 0.47f, 0.25f, 1f), // lime, short of neon
            new Vector4(0.42f, 0.52f, 0.62f, 1f), // blue-white
        };

        /// <summary>
        /// Which of <see cref="VividTips"/> each pair of <see cref="PaletteTints"/> takes: cream on the browns,
        /// the tans and the pink, lime on the olive and the green, blue-white on the tan that had blue tips.
        /// </summary>
        private static readonly int[] VividTipOf = { 0, 0, 1, 0, 1, 0, 0, 2 };

        /// <summary>The share of colonies whose tips take the next of <see cref="VividTips"/> instead, so not every brown colony is cream-tipped.</summary>
        private const float VividTipSwap = 0.2f;

        /// <summary>The least of a colony's own brightness under <see cref="LivingBold"/>, as a multiple of its tints.</summary>
        private const float VividGainLow = 0.9f;

        /// <summary>How far above <see cref="VividGainLow"/> a colony's brightness may sit.</summary>
        private const float VividGainSpread = 0.2f;

        /// <summary>
        /// The bright look's own stream, salted apart from <see cref="PaletteStream"/> and <see cref="SeedUnit"/>,
        /// so no draw made here moves a draw the further look or the stone makes.
        /// </summary>
        private const uint VividStream = 0x68e31da4u;

        /// <summary>One draw of a colony's bright stream, in [0, 1).</summary>
        private static float VividUnit(int colony, int draw)
        {
            unchecked
            {
                uint h = (uint)colony * 0x85ebca6bu ^ (uint)draw * 0xc2b2ae35u ^ VividStream;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>
        /// A colony's tissue and tip colours under <see cref="LivingBold"/>, linear, scaled by
        /// <see cref="LivingGain"/>: the further look's pick (<see cref="PalettePick"/>) in
        /// <see cref="VividTissues"/>, its tip from <see cref="VividTips"/>, and its own brightness, the last
        /// two from the bright stream.
        /// </summary>
        public static void VividTint(int colony, out Vector4 tissue, out Vector4 tip)
        {
            int pick = PalettePick(colony);
            int tipPick = VividTipOf[pick];
            if (VividUnit(colony, 1) < VividTipSwap) tipPick = (tipPick + 1) % VividTips.Length;

            float gain = LivingGain * (VividGainLow + VividGainSpread * VividUnit(colony, 2));
            tissue = VividTissues[pick] * gain;
            tip = VividTips[tipPick] * gain;
            tissue.w = 1f;
            tip.w = 1f;
        }

        // ------------------------------------------------------------------ placing

        // The last run placed, so a second open of it grows nothing. A CoralRock is read-only.
        private static string _placedKey;
        private static CoralRock _placed;
        private static BedShape _placedBed;

        /// <summary>
        /// The coral of a recorded run, grown as <c>World</c> grows it and without a <c>World</c>:
        /// from the landscape's seed (the run's own, or the config's <c>landscape</c> override),
        /// over the floor and the footprint the config describes. Null at the mushrooms, with the
        /// dial off, and when this build cannot place it (said in the log).
        /// </summary>
        /// <remarks>
        /// <para>
        /// For the routes that do not step a <c>World</c>: the film windows
        /// (<see cref="FilmWindowWorld"/>) and the reconstructions (<see cref="SnapshotWorld"/>).
        /// The live routes read <c>World.Coral</c> itself.
        /// </para>
        /// <para>
        /// <b>A cousin of the farm's coral, checked.</b> The editor runs Mono, which can round a
        /// cosine otherwise than .NET 8; the placer draws its colonies by comparing floats, so a
        /// differing bit could in principle move a colony. The log line compares the grown
        /// coral's digest with the one <c>run.json</c> recorded.
        /// </para>
        /// <para>
        /// The picture-only config reader reads the coral group and the fields the placer reads
        /// beside it when the file names a coral (<c>PictureConfig.ReadTheCoral</c>), so a coral
        /// run the strict reader refuses still grows its coral; the digest line says whether it
        /// is the farm's.
        /// </para>
        /// </remarks>
        /// <param name="record">The run.</param>
        /// <param name="landscapeBed">
        /// The floor the coral stands on, as <c>World</c> builds it, or null for a flat one. For a
        /// box with relief this is a floor <see cref="SnapshotWorld.BuildFloor"/> does not build.
        /// <b>Null on a cache hit</b> (<see cref="TheatreCoralCache"/>, <c>EVOSIM_THEATRE_CORAL_CACHE</c>),
        /// where no bed is built at all. Every caller discards it today (<c>out _</c>); a caller that
        /// needs the bed must not take it from a call that may hit the cache.
        /// </param>
        public static CoralRock PlaceFor(RunRecord record, out BedShape landscapeBed)
        {
            landscapeBed = null;
            if (record == null || !Enabled) return null;

            RunConfig config = record.Config;

            if (config.ReefShape != ReefShape.Coral)
            {
                WarnIfDropped(record);
                return null;
            }

            // The same run opened again (a film's every scene opens its window) is the same coral.
            string key = record.Path + "|" + record.Seed.ToString(CultureInfo.InvariantCulture);
            if (_placedKey == key)
            {
                landscapeBed = _placedBed;
                return _placed;
            }

            try
            {
                // World's own lines (D192): the landscape's seed is the config's override or the
                // world's seed, and the box's sides are the patch layout's. The footprint is
                // cheap; the bed is not, and a cached coral needs neither the bed nor its seconds.
                ulong landscape = config.TrySeedOverride("landscape", out ulong named) ? named : record.Seed;
                CoralFootprint footprint;
                float radius = 0f, length = 0f, width = 0f;
                bool tank = config.WorldShape == WorldShape.Tank;

                if (tank)
                {
                    radius = TankGeometry.RadiusFor(config.WorldAreaSquareMetres);
                    footprint = CoralFootprint.Tank(radius);
                }
                else
                {
                    int count = Math.Max(1, (int)config.HorizontalPatches);
                    int across = Math.Min(Math.Max(1, (int)config.PatchesAcross), count);
                    float patch = (float)Math.Sqrt(config.WorldAreaSquareMetres / count);
                    length = patch * Math.Max(1, count / across);
                    width = patch * across;
                    footprint = CoralFootprint.Box(length, width);
                }

                // The cache is looked at before the bed is built. A hit is a coral whose digest is
                // run.json's, so it is the farm's coral and needs no growing.
                string recordedDigest = RecordedDigest(record);
                TheatreCoralCache.Key cacheKey = TheatreCoralCache.Enabled ? TheatreCoralCache.MakeKey(record, config, landscape) : null;
                if (cacheKey != null)
                {
                    CoralGeometry cached = TheatreCoralCache.TryRead(cacheKey, recordedDigest);
                    if (cached != null)
                    {
                        _placedKey = key;
                        _placed = new CoralRock(cached, footprint, config);
                        _placedBed = null;
                        return _placed;
                    }
                }

                var clock = System.Diagnostics.Stopwatch.StartNew();
                Debug.Log(
                    "[Theatre] growing the coral of " + record.Path + "; the landscape's bed is built first and a box's " +
                    "takes minutes in the Editor, where the farm takes seconds");

                var bedClock = System.Diagnostics.Stopwatch.StartNew();

                if (tank)
                {
                    landscapeBed = config.BedReliefMetres > 0f || config.BedTiltMetres > 0f
                        ? new BedShape(
                            radius, config.WorldDepthMetres, config.BedReliefMetres,
                            config.BedTiltMetres, config.BedScaleMetres, Rng.SeedFor(landscape, World.BedShapeIndex),
                            config.BedShoreDepthMetres, config.BedShoreFadeMetres)
                        : null;
                }
                else if (TheatreBoxBed.Enabled)
                {
                    // D278: the film draws this bed too, so it is built (and cached) once, there.
                    landscapeBed = TheatreBoxBed.For(record);
                }
                else
                {
                    landscapeBed = config.BedReliefMetres > 0f
                        ? BedShape.ForBox(
                            length, width, config.WorldDepthMetres, config.BedReliefMetres,
                            config.BedScaleMetres, Rng.SeedFor(landscape, World.BedShapeIndex),
                            config.BedShoreDepthMetres, config.BedShoreFadeMetres)
                        : null;
                }

                bedClock.Stop();
                var placeClock = System.Diagnostics.Stopwatch.StartNew();
                CoralGeometry coral = CoralGeometry.PlaceFromLandscape(config, footprint, landscapeBed, landscape);
                placeClock.Stop();
                if (coral == null || coral.Count == 0) return null;

                Debug.Log(
                    "[Theatre] coral placed from landscape seed " + landscape.ToString(CultureInfo.InvariantCulture) + " in " +
                    (clock.ElapsedMilliseconds / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " s (bed " +
                    (bedClock.ElapsedMilliseconds / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " s, placement " +
                    (placeClock.ElapsedMilliseconds / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " s): " +
                    coral.Count + " colonies, " + coral.PieceCount + " pieces, digest " +
                    coral.Digest().ToString("x16", CultureInfo.InvariantCulture) + " " + DigestVerdict(record, coral));

                // Only the farm's own coral is kept: one whose digest is run.json's.
                if (cacheKey != null && recordedDigest != null &&
                    string.Equals(recordedDigest, coral.Digest().ToString("x16", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
                {
                    TheatreCoralCache.Write(cacheKey, coral);
                }

                _placedKey = key;
                _placed = new CoralRock(coral, footprint, config);
                _placedBed = landscapeBed;
                return _placed;
            }
            catch (Exception e)
            {
                landscapeBed = null;
                Debug.LogWarning("[Theatre] coral not placed: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        /// <summary>The coral digest <c>run.json</c> recorded, or null when there is no file, no digest or the file is unreadable.</summary>
        private static string RecordedDigest(RunRecord record)
        {
            try
            {
                string path = System.IO.Path.Combine(record.Path, "run.json");
                if (!System.IO.File.Exists(path)) return null;

                JsonNode root = Json.Parse(System.IO.File.ReadAllText(path));
                return root.Has("coralDigest") ? root["coralDigest"].AsString() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the coral grown here is the one the run recorded, from <c>run.json</c>'s digest and counts.</summary>
        private static string DigestVerdict(RunRecord record, CoralGeometry coral)
        {
            try
            {
                string path = System.IO.Path.Combine(record.Path, "run.json");
                if (!System.IO.File.Exists(path)) return "(no run.json to compare with)";

                JsonNode root = Json.Parse(System.IO.File.ReadAllText(path));
                if (!root.Has("coralDigest")) return "(run.json records no coral digest)";

                string recorded = root["coralDigest"].AsString();
                string ours = coral.Digest().ToString("x16", CultureInfo.InvariantCulture);

                return string.Equals(recorded, ours, StringComparison.OrdinalIgnoreCase)
                    ? "= run.json's: the farm's coral"
                    : "DIFFERS from run.json's " + recorded + ": a cousin of the farm's coral (Mono's maths, or another build)";
            }
            catch (Exception e)
            {
                return "(run.json unreadable: " + e.GetType().Name + ")";
            }
        }

        /// <summary>Says so when a coral run was read by the picture-only reader, which drops the coral group.</summary>
        private static void WarnIfDropped(RunRecord record)
        {
            try
            {
                string path = System.IO.Path.Combine(record.Path, "config.json");
                if (!System.IO.File.Exists(path)) return;

                JsonNode root = Json.Parse(System.IO.File.ReadAllText(path));
                if (!root.Has("coral")) return;
                JsonNode group = root["coral"];
                if (group.Has("reefShape") && string.Equals(group["reefShape"].AsString(), "Coral", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        "[Theatre] this run's config names a coral, and the picture-only reader (OLD-RUN READ) does not read " +
                        "the coral group, so no coral is drawn.");
                }
            }
            catch (Exception)
            {
                // A note about a note: nothing to say.
            }
        }

        // ------------------------------------------------------------------ the log

        /// <summary>
        /// The one log line of a built coral: its counts, its box, and the five tallest colonies
        /// by their trunk foot and their top.
        /// </summary>
        public static string Summary(CoralGeometry coral, int vertices)
        {
            var ci = CultureInfo.InvariantCulture;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            var heights = new List<KeyValuePair<double, int>>();

            for (int c = 0; c < coral.Count; c++)
            {
                coral.Bounds(c, out double x0, out double y0, out double z0, out double x1, out double y1, out double z1);
                minX = Math.Min(minX, x0); minY = Math.Min(minY, y0); minZ = Math.Min(minZ, z0);
                maxX = Math.Max(maxX, x1); maxY = Math.Max(maxY, y1); maxZ = Math.Max(maxZ, z1);
                heights.Add(new KeyValuePair<double, int>(y1 - coral.ColonyAt(c).BaseY, c));
            }

            heights.Sort((a, b) => b.Key != a.Key ? b.Key.CompareTo(a.Key) : a.Value.CompareTo(b.Value));

            var line = new System.Text.StringBuilder();
            line.Append("[Theatre] coral: ")
                .Append(coral.Count.ToString(ci)).Append(" colonies, ")
                .Append(coral.PieceCount.ToString(ci)).Append(" pieces, ")
                .Append(vertices.ToString(ci)).Append(" vertices, box x ")
                .Append(minX.ToString("0.0", ci)).Append("..").Append(maxX.ToString("0.0", ci)).Append(" y ")
                .Append(minY.ToString("0.0", ci)).Append("..").Append(maxY.ToString("0.0", ci)).Append(" z ")
                .Append(minZ.ToString("0.0", ci)).Append("..").Append(maxZ.ToString("0.0", ci))
                .Append(" m; tallest (foot x,y,z, top y, height, pieces):");

            for (int k = 0; k < Math.Min(5, heights.Count); k++)
            {
                int c = heights[k].Value;
                CoralGeometry.Colony colony = coral.ColonyAt(c);
                coral.Bounds(c, out _, out _, out _, out _, out double top, out _);
                line.Append(" #").Append(c.ToString(ci)).Append(" (")
                    .Append(colony.X.ToString("0.0", ci)).Append(',')
                    .Append(colony.BaseY.ToString("0.0", ci)).Append(',')
                    .Append(colony.Z.ToString("0.0", ci)).Append(") top ")
                    .Append(top.ToString("0.0", ci)).Append(" h ")
                    .Append(heights[k].Key.ToString("0.0", ci)).Append(" n ")
                    .Append(colony.PieceCount.ToString(ci)).Append(k < 4 ? ";" : "");
            }

            return line.ToString();
        }

        /// <summary>A colony's seed for the rock shader, in [0, 1).</summary>
        public static float SeedUnit(int colony)
        {
            unchecked
            {
                uint h = (uint)colony * 2654435761u + 0x9e3779b9u;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
                return (h % 1000003u) / 1000003f;
            }
        }

        // ------------------------------------------------------------------ the mesh

        /// <summary>
        /// One colony's mesh in its own frame (origin at the trunk's foot on the bed), every piece
        /// welded into the one vertex list.
        /// </summary>
        /// <param name="coral">The placed coral.</param>
        /// <param name="colonyIndex">Which colony.</param>
        /// <param name="topLocal">The colony's highest surface above its foot, m.</param>
        /// <param name="reach">The farthest a surface lies from the trunk's axis, horizontally, m.</param>
        /// <param name="shape"><see cref="Shape"/>: 0 builds the mesh exactly as before.</param>
        /// <param name="further">
        /// <see cref="ShapeFurther"/>, which takes effect only with <paramref name="shape"/> above 0: tapered
        /// branches, axial polyps, thin plate rims, and the piece readings the shader's relief needs.
        /// </param>
        /// <param name="ages">
        /// Whether to write each vertex's age for the bare skeleton (<see cref="LivingFurther"/>). It
        /// adds two texture coordinates and moves no vertex.
        /// </param>
        /// <param name="bush">
        /// <see cref="ShapeBold"/>, which takes effect only with <paramref name="further"/> and
        /// <paramref name="shape"/> above 0: each capsule built as a thin core with sprigs and, on a tip, prongs
        /// (<see cref="AddBushCapsule"/>), each plate as a thin lowered body under a mat (<see cref="AddBushPlate"/>).
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>The readings in the texture coordinates</b>, written only when <paramref name="further"/>
        /// or <paramref name="ages"/> asks, after each piece is built, so no builder below changes for
        /// them. The first (four numbers) is the piece: a plate's centre (x, z in the colony's frame),
        /// its radius and 1; a capsule's 0, 0, its radius and 0. The second (two) is the age: 1 at the
        /// trunk's foot, falling along each capsule and down the generations to 0 at the youngest
        /// tip, from the piece's depth (<see cref="CoralGeometry.Piece.Depth"/>) and how far along its
        /// axis the vertex lies; and under the bush, the tip reading (<see cref="Mesher.Tips"/>), 0 at a
        /// sprig's root rising to its end, which the bright tissue paints pale. Without the bush it is 0.
        /// </para>
        /// <para>
        /// <b>Separate streams.</b> The pieces' seeds and the offset are drawn as before, in the same
        /// order; nothing here draws from a stream the mesh at 0 or 1 reads. The bush draws only from its
        /// own (<see cref="BushUnit"/>).
        /// </para>
        /// </remarks>
        public static Mesh Colony(
            CoralGeometry coral, int colonyIndex, out float topLocal, out float reach, float shape = 0f,
            bool further = false, bool ages = false, bool bush = false)
        {
            CoralGeometry.Colony colony = coral.ColonyAt(colonyIndex);
            coral.Bounds(colonyIndex, out double minX, out _, out double minZ, out double maxX, out double maxY, out double maxZ);
            topLocal = Mathf.Max(0.1f, (float)(maxY - colony.BaseY));
            reach = Mathf.Max(
                0.5f,
                (float)Math.Max(
                    Math.Max(colony.X - minX, maxX - colony.X),
                    Math.Max(colony.Z - minZ, maxZ - colony.Z)));

            var m = new Mesher { Offset = SeedOffset(colonyIndex * 7919 + 13), Shape = shape };

            // A piece is a tip when nothing grows from it (a parent index is within the colony).
            var hasChild = new bool[colony.PieceCount];
            for (int k = 0; k < colony.PieceCount; k++)
            {
                int parent = coral.PieceAt(colony.FirstPiece + k).Parent;
                if (parent >= 0 && parent < colony.PieceCount) hasChild[parent] = true;
            }

            // Under the further shape, each capsule tapers to its widest capsule child's radius, so a
            // branch narrows continuously across its joints, and a capsule with none to TipTaper.
            float[] childRadius = null;
            int deepest = 0;
            bool readings = further || ages;
            if (readings)
            {
                m.Further = further;
                childRadius = new float[colony.PieceCount];
                for (int k = 0; k < colony.PieceCount; k++)
                {
                    CoralGeometry.Piece p = coral.PieceAt(colony.FirstPiece + k);
                    deepest = Math.Max(deepest, p.Depth);
                    if (p.Kind != CoralGeometry.PieceKind.Capsule) continue;
                    if (p.Parent >= 0 && p.Parent < colony.PieceCount)
                        childRadius[p.Parent] = Mathf.Max(childRadius[p.Parent], (float)p.Radius);
                }
            }

            // The bush needs the further shape's taper and readings, so it is only ever laid over them.
            bool bushy = bush && further && shape > 0f;

            for (int k = 0; k < colony.PieceCount; k++)
            {
                CoralGeometry.Piece p = coral.PieceAt(colony.FirstPiece + k);
                m.Seed = colonyIndex * 1009 + k;
                m.MatFrom = -1;
                int firstVertex = m.Vertices.Count;
                if (childRadius != null)
                {
                    m.Taper = childRadius[k] > 0f
                        ? Mathf.Clamp(childRadius[k] / Mathf.Max(1e-5f, (float)p.Radius), TipTaper, 1f)
                        : TipTaper;
                }

                if (bushy)
                {
                    if (p.Kind == CoralGeometry.PieceKind.Plate) AddBushPlate(m, p, colony);
                    else AddBushCapsule(m, p, colony, foot: k == 0 && p.Parent < 0, tip: !hasChild[k]);
                }
                else if (p.Kind == CoralGeometry.PieceKind.Plate) AddPlate(m, p, colony);
                else AddCapsule(m, p, colony, foot: k == 0 && p.Parent < 0, tip: !hasChild[k]);

                if (readings) AddReadings(m, p, colony, firstVertex, deepest);
            }

            var mesh = new Mesh { name = "Theatre Coral Colony " + colonyIndex, hideFlags = HideFlags.DontSave };
            mesh.indexFormat = m.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(m.Vertices);
            mesh.SetNormals(m.Normals);
            mesh.SetColors(m.Colours);
            if (readings)
            {
                mesh.SetUVs(0, m.Pieces);
                mesh.SetUVs(1, m.Ages);
            }
            mesh.SetTriangles(m.Triangles, 0);

            // A shaped surface is shaded by its own facets, so the lobes and knobs catch the light;
            // a lathe's pole is a ring of coincident vertices, and any normal the recalculation
            // leaves degenerate there goes back to the analytic one.
            if (shape > 0f)
            {
                mesh.RecalculateNormals();
                Vector3[] normals = mesh.normals;
                for (int i = 0; i < normals.Length; i++)
                {
                    Vector3 n = normals[i];
                    if (!(n.sqrMagnitude > 0.25f) || float.IsNaN(n.x)) normals[i] = m.Normals[i];
                }
                mesh.normals = normals;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A growing vertex list, and the noise's offset for the colony being built.</summary>
        private sealed class Mesher
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Color32> Colours = new List<Color32>();
            public readonly List<int> Triangles = new List<int>();
            public Vector3 Offset;

            /// <summary><see cref="Shape"/> for this build; 0 is the mesh as before.</summary>
            public float Shape;

            /// <summary>The piece being built, for its own lobes and fork.</summary>
            public int Seed;

            /// <summary><see cref="ShapeFurther"/> for this build; false is the shape at <see cref="Shape"/> alone.</summary>
            public bool Further;

            /// <summary>The capsule being built's radius at its top end, as a share of its own, under <see cref="Further"/>.</summary>
            public float Taper = 1f;

            /// <summary>The piece reading per vertex (the first texture coordinate), when asked for.</summary>
            public readonly List<Vector4> Pieces = new List<Vector4>();

            /// <summary>The age per vertex (the second texture coordinate), when asked for.</summary>
            public readonly List<Vector2> Ages = new List<Vector2>();

            /// <summary>
            /// The tip reading per vertex under the bush (the second texture coordinate's second number), kept
            /// level with <see cref="Vertices"/> by the bush's builders; empty without the bush, which reads as 0.
            /// </summary>
            public readonly List<float> Tips = new List<float>();

            /// <summary>Where a bush plate's mat begins in the vertex list, so its sprigs read as a capsule's; -1 for none.</summary>
            public int MatFrom = -1;
        }

        /// <summary>
        /// The texture coordinates of the vertices one piece just added (from <paramref name="firstVertex"/>):
        /// the piece reading and the age, as <see cref="Colony"/> describes them. Read off the built
        /// vertices, so it changes nothing a builder does.
        /// </summary>
        private static void AddReadings(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony, int firstVertex, int deepest)
        {
            Vector3 a = Local(colony, p.AX, p.AY, p.AZ);
            float generations = deepest + 1f;

            if (p.Kind == CoralGeometry.PieceKind.Plate)
            {
                var plate = new Vector4(a.x, a.z, (float)p.Radius, 1f);
                float age = 1f - Mathf.Clamp01((p.Depth + 1f) / generations);

                // A bush plate's mat reads as small capsules, so the shader gives its sprigs bumps, not rings.
                int bodyEnd = m.MatFrom >= firstVertex ? m.MatFrom : m.Vertices.Count;
                var sprig = new Vector4(0f, 0f, (float)p.HalfThickness, 0f);
                for (int i = firstVertex; i < m.Vertices.Count; i++)
                {
                    m.Pieces.Add(i < bodyEnd ? plate : sprig);
                    m.Ages.Add(new Vector2(age, TipAt(m, i)));
                }

                return;
            }

            Vector3 b = Local(colony, p.BX, p.BY, p.BZ);
            Vector3 axis = b - a;
            float length = axis.magnitude;
            axis = length > 1e-5f ? axis / length : Vector3.up;
            var capsule = new Vector4(0f, 0f, (float)p.Radius, 0f);

            for (int i = firstVertex; i < m.Vertices.Count; i++)
            {
                float along = Mathf.Clamp01(Vector3.Dot(m.Vertices[i] - a, axis) / Mathf.Max(1e-4f, length));
                float age = 1f - Mathf.Clamp01((p.Depth + along) / generations);
                m.Pieces.Add(capsule);
                m.Ages.Add(new Vector2(age, TipAt(m, i)));
            }
        }

        /// <summary>A vertex's tip reading: the bush's, or 0 where the bush wrote none.</summary>
        private static float TipAt(Mesher m, int i) => i < m.Tips.Count ? m.Tips[i] : 0f;

        private static Vector3 Local(CoralGeometry.Colony colony, double x, double y, double z) =>
            new Vector3((float)(x - colony.X), (float)(y - colony.BaseY), (float)(z - colony.Z));

        /// <summary>
        /// A capsule: rings from the A pole round its hemisphere, along the shaft (two rings, one
        /// at each equator) and round the B hemisphere to the B pole, cut inward by noise.
        /// </summary>
        private static void AddCapsule(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony, bool foot, bool tip)
        {
            if (m.Shape > 0f)
            {
                if (m.Further) AddFurtherCapsule(m, p, colony, foot, tip);
                else AddShapedCapsule(m, p, colony, foot, tip);
                return;
            }

            Vector3 a = Local(colony, p.AX, p.AY, p.AZ);
            Vector3 b = Local(colony, p.BX, p.BY, p.BZ);
            Vector3 axis = b - a;
            float length = axis.magnitude;
            axis = length > 1e-5f ? axis / length : Vector3.up;

            // The trunk runs on below the bed, along its own axis.
            if (foot)
            {
                a -= axis * FootBelowMetres;
                length += FootBelowMetres;
            }

            float r = (float)p.Radius;
            Vector3 helper = Mathf.Abs(axis.x) < 0.9f ? Vector3.right : Vector3.forward;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            // A modest tessellation: the shader's grain does the roughness, the mesh the roundness.
            int segments = r >= 0.3f ? 16 : (r >= 0.15f ? 12 : 10);
            int capRings = r >= 0.3f ? 5 : 4;
            int rings = 2 * (capRings + 1);
            int first = m.Vertices.Count;

            for (int ring = 0; ring < rings; ring++)
            {
                // The A hemisphere's rings run from its pole (angle π/2) to its equator (0), the B
                // hemisphere's from its equator to its pole.
                bool far = ring > capRings;
                int step = far ? ring - capRings - 1 : ring;
                float angle = far
                    ? 0.5f * Mathf.PI * step / capRings
                    : 0.5f * Mathf.PI * (1f - (float)step / capRings);
                float along = far ? length + r * Mathf.Sin(angle) : -r * Mathf.Sin(angle);
                float round = r * Mathf.Cos(angle);
                float lift = (far ? 1f : -1f) * Mathf.Sin(angle);

                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    Vector3 radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * v;
                    Vector3 onSurface = a + axis * along + radial * round;
                    Vector3 normal = (radial * Mathf.Cos(angle) + axis * lift).normalized;
                    AddVertex(m, onSurface, normal, CapsuleCut * r);
                }
            }

            Bridge(m, first, rings, segments);
        }

        /// <summary>
        /// A plate: a horizontal disc of rounded rim, as a lathe about its centre. Flat from the
        /// axis to the radius, a half circle of its half thickness round the rim, flat back.
        /// </summary>
        /// <summary>
        /// A capsule under <see cref="Shape"/>: the same lathe, ringed along its shaft, and on a tip
        /// (a capsule nothing grows from) a fork and knobs. Every cut is inward along the analytic
        /// normal, and the deepest (the stone's own cut, a knob's edge and the groove together at
        /// the pole) is 0.85 of the radius, so the drawn branch stays inside the one the physics
        /// pushes bodies off and never turns inside out.
        /// </summary>
        /// <remarks>
        /// The fork is a groove across the tip in a plane through the axis, deepest at the pole and
        /// fading back along the last radius and a half of the shaft, which leaves two prongs. The
        /// knobs are a cellular field about a radius across over the outer half of a tip: a cell's
        /// middle is cut least, its edges most, so each cell stands as a rounded knob.
        /// </remarks>
        private static void AddShapedCapsule(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony, bool foot, bool tip)
        {
            Vector3 a = Local(colony, p.AX, p.AY, p.AZ);
            Vector3 b = Local(colony, p.BX, p.BY, p.BZ);
            Vector3 axis = b - a;
            float length = axis.magnitude;
            axis = length > 1e-5f ? axis / length : Vector3.up;

            if (foot)
            {
                a -= axis * FootBelowMetres;
                length += FootBelowMetres;
            }

            float r = (float)p.Radius;
            Vector3 helper = Mathf.Abs(axis.x) < 0.9f ? Vector3.right : Vector3.forward;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            int segments = (r >= 0.3f ? 16 : (r >= 0.15f ? 12 : 10)) + (tip ? 8 : 4);
            int capRings = (r >= 0.3f ? 5 : 4) + (tip ? 3 : 0);
            int shaft = Mathf.Clamp(Mathf.CeilToInt(length / (0.6f * r)), 1, 24);
            int rings = 2 * (capRings + 1) + (shaft - 1);
            int first = m.Vertices.Count;

            // The fork's plane, turned about the axis by the piece's own seed.
            float turn = 2f * Mathf.PI * SeedUnit(m.Seed + 17);
            Vector3 across = Mathf.Cos(turn) * u + Mathf.Sin(turn) * v;
            float shape = m.Shape;

            for (int ring = 0; ring < rings; ring++)
            {
                float along, round, lift, angle;

                if (ring <= capRings)
                {
                    // The A hemisphere, pole to equator.
                    angle = 0.5f * Mathf.PI * (1f - (float)ring / capRings);
                    along = -r * Mathf.Sin(angle);
                    round = r * Mathf.Cos(angle);
                    lift = -Mathf.Sin(angle);
                }
                else if (ring < capRings + shaft)
                {
                    // The shaft's inner rings, evenly along it.
                    angle = 0f;
                    along = length * (ring - capRings) / shaft;
                    round = r;
                    lift = 0f;
                }
                else
                {
                    // The B hemisphere, equator to pole.
                    int step = ring - (capRings + shaft);
                    angle = 0.5f * Mathf.PI * step / capRings;
                    along = length + r * Mathf.Sin(angle);
                    round = r * Mathf.Cos(angle);
                    lift = Mathf.Sin(angle);
                }

                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    Vector3 radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * v;
                    Vector3 onSurface = a + axis * along + radial * round;
                    Vector3 normal = (radial * Mathf.Cos(angle) + axis * lift).normalized;

                    float extra = 0f;
                    if (tip)
                    {
                        // How far toward the tip, 0 at mid shaft, 1 from the B equator on.
                        float toward = Mathf.Clamp01((along - 0.5f * length) / Mathf.Max(1e-4f, 0.5f * length));
                        toward = toward * toward * (3f - 2f * toward);

                        float cell = Worley((onSurface + m.Offset) / Mathf.Max(0.05f, 0.9f * r));
                        extra += 0.22f * r * toward * Mathf.SmoothStep(0.15f, 0.55f, cell);

                        float lateral = Mathf.Abs(Vector3.Dot(onSurface - (a + axis * along), across)) / r;
                        float groove = 1f - Mathf.SmoothStep(0f, 0.4f, lateral);
                        float reach = Mathf.Clamp01((along - (length - 1.5f * r)) / (2.5f * r));
                        extra += 0.38f * r * groove * reach;
                    }

                    AddVertex(m, onSurface, normal, CapsuleCut * r, shape * extra);
                }
            }

            Bridge(m, first, rings, segments);
        }

        /// <summary>
        /// A capsule under <see cref="ShapeFurther"/>: the shaped lathe, tapering from its own radius at
        /// its base to <see cref="Mesher.Taper"/> of it at its top, and on a tip the shape at 1's fork
        /// and knobs, scaled to the narrower tip, with a swollen axial polyp and a cup on each prong.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Inside the physics' capsule.</b> Before any cut the surface is a round cone: a hemisphere
        /// of the full radius at the base, rings whose radius falls linearly up the shaft, and a
        /// hemisphere of the tapered radius at the top, swollen by <see cref="AxialSwell"/> on a tip.
        /// Every radius is at most the capsule's and every centre lies on its segment, so all of it
        /// lies inside the capsule. Every cut then goes inward along the analytic normal, scaled by
        /// the radius where it lies, and their sum is held to <see cref="MaxCutShare"/> of that
        /// radius, so no point crosses the axis.
        /// </para>
        /// <para>
        /// <b>The axial polyp.</b> The fork's groove takes the pole, so each prong carries a polyp of
        /// its own: a shallow dish about a direction leaning <see cref="ProngTilt"/> off the axis
        /// toward the prong, with the knobs and most of the stone's cut smoothed away round it.
        /// </para>
        /// </remarks>
        private static void AddFurtherCapsule(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony, bool foot, bool tip)
        {
            Vector3 a = Local(colony, p.AX, p.AY, p.AZ);
            Vector3 b = Local(colony, p.BX, p.BY, p.BZ);
            Vector3 axis = b - a;
            float length = axis.magnitude;
            axis = length > 1e-5f ? axis / length : Vector3.up;
            float grown = Mathf.Max(1e-4f, length);
            float below = 0f;

            if (foot)
            {
                a -= axis * FootBelowMetres;
                length += FootBelowMetres;
                below = FootBelowMetres;
            }

            float r = (float)p.Radius;
            float top = r * Mathf.Clamp(m.Taper, TipTaper, 1f);
            float end = tip ? Mathf.Min(r, top * (1f + AxialSwell)) : top;
            float swellFrom = length - AxialSwellReach * end;

            Vector3 helper = Mathf.Abs(axis.x) < 0.9f ? Vector3.right : Vector3.forward;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            int segments = (r >= 0.3f ? 16 : (r >= 0.15f ? 12 : 10)) + (tip ? 20 : 4);
            int capRings = (r >= 0.3f ? 5 : 4) + (tip ? 8 : 0);
            int shaft = Mathf.Clamp(Mathf.CeilToInt(length / (0.3f * (r + top))), 1, 32);
            int rings = 2 * (capRings + 1) + (shaft - 1);
            int first = m.Vertices.Count;

            // The fork's plane, turned about the axis by the piece's own seed, as at 1.
            float turn = 2f * Mathf.PI * SeedUnit(m.Seed + 17);
            Vector3 across = Mathf.Cos(turn) * u + Mathf.Sin(turn) * v;
            float shape = m.Shape;

            // Each prong's cup, as a direction from the top hemisphere's centre.
            Vector3 crown = a + axis * length;
            Vector3 cupOne = (Mathf.Cos(ProngTilt) * axis + Mathf.Sin(ProngTilt) * across).normalized;
            Vector3 cupTwo = (Mathf.Cos(ProngTilt) * axis - Mathf.Sin(ProngTilt) * across).normalized;

            for (int ring = 0; ring < rings; ring++)
            {
                float along, round, lift, angle, local;
                bool cap = false;

                if (ring <= capRings)
                {
                    // The base hemisphere, pole to equator, at the full radius.
                    angle = 0.5f * Mathf.PI * (1f - (float)ring / capRings);
                    along = -r * Mathf.Sin(angle);
                    round = r * Mathf.Cos(angle);
                    lift = -Mathf.Sin(angle);
                    local = r;
                }
                else if (ring < capRings + shaft)
                {
                    // The shaft, narrowing up the piece's own length (the buried foot keeps the full
                    // radius), and on a tip swelling into the polyp over its last few radii.
                    angle = 0f;
                    along = length * (ring - capRings) / shaft;
                    local = Mathf.Lerp(r, top, Mathf.Clamp01((along - below) / grown));
                    if (tip) local = Mathf.Lerp(local, end, Smooth01((along - swellFrom) / (length - swellFrom)));
                    round = local;
                    lift = 0f;
                }
                else
                {
                    // The top hemisphere, equator to pole, at the tapered (and on a tip, swollen) radius.
                    int step = ring - (capRings + shaft);
                    angle = 0.5f * Mathf.PI * step / capRings;
                    along = length + end * Mathf.Sin(angle);
                    round = end * Mathf.Cos(angle);
                    lift = Mathf.Sin(angle);
                    local = end;
                    cap = true;
                }

                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    Vector3 radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * v;
                    Vector3 onSurface = a + axis * along + radial * round;
                    Vector3 normal = (radial * Mathf.Cos(angle) + axis * lift).normalized;

                    float extra = 0f;
                    float stone = 1f;
                    if (tip)
                    {
                        // The shape at 1's terms, each scaled by the radius where it lies.
                        float toward = Mathf.Clamp01((along - 0.5f * length) / Mathf.Max(1e-4f, 0.5f * length));
                        toward = toward * toward * (3f - 2f * toward);

                        // The axial cups, on the top hemisphere: a dish about each prong's direction.
                        float nearest = 9f;
                        if (cap)
                        {
                            Vector3 from = (onSurface - crown) / Mathf.Max(1e-5f, end);
                            float one = Mathf.Acos(Mathf.Clamp(Vector3.Dot(from, cupOne), -1f, 1f));
                            float two = Mathf.Acos(Mathf.Clamp(Vector3.Dot(from, cupTwo), -1f, 1f));
                            nearest = Mathf.Min(one, two) / AxialCupAngle;
                            extra += AxialCupDepth * end * (1f - Smooth01(nearest));
                        }

                        float cell = Worley((onSurface + m.Offset) / Mathf.Max(0.05f, 0.9f * end));
                        float knob = 0.22f * local * toward * Mathf.SmoothStep(0.15f, 0.55f, cell);
                        extra += knob * Smooth01((nearest - 1f) / 0.8f);

                        float lateral = Mathf.Abs(Vector3.Dot(onSurface - (a + axis * along), across)) / local;
                        float groove = 1f - Mathf.SmoothStep(0f, 0.4f, lateral);
                        float reach = Mathf.Clamp01((along - (length - 1.5f * end)) / (2.5f * end));
                        extra += 0.38f * local * groove * reach;

                        // The polyp is smooth tissue over the skeleton: most of the stone's cut goes.
                        float polyp = cap ? 1f : Smooth01((along - swellFrom) / (length - swellFrom));
                        stone = 1f - AxialSmooth * polyp;
                    }

                    AddVertexWithin(m, onSurface, normal, CapsuleCut * local, stone, shape * extra, MaxCutShare * local);
                }
            }

            Bridge(m, first, rings, segments);
        }

        /// <summary>A smooth Hermite ramp from 0 at <paramref name="t"/> 0 to 1 at <paramref name="t"/> 1, clamped.</summary>
        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static void AddPlate(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony)
        {
            if (m.Shape > 0f)
            {
                if (m.Further) AddFurtherPlate(m, p, colony);
                else AddShapedPlate(m, p, colony);
                return;
            }

            Vector3 centre = Local(colony, p.AX, p.AY, p.AZ);
            float radius = (float)p.Radius;
            float half = (float)p.HalfThickness;

            int segments = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * (radius + half) / 0.3f), 20, 48);
            int flat = Mathf.Clamp(Mathf.CeilToInt(radius / 0.45f), 2, 6);
            const int arc = 6;
            int rings = (flat + 1) + (arc - 1) + (flat + 1);
            int first = m.Vertices.Count;

            for (int ring = 0; ring < rings; ring++)
            {
                float rho, y, nr, ny;

                if (ring <= flat)
                {
                    rho = radius * ring / flat; y = half; nr = 0f; ny = 1f;
                }
                else if (ring < flat + arc)
                {
                    float angle = 0.5f * Mathf.PI - Mathf.PI * (ring - flat) / arc;
                    nr = Mathf.Cos(angle); ny = Mathf.Sin(angle);
                    rho = radius + half * nr; y = half * ny;
                }
                else
                {
                    int j = flat - (ring - (flat + arc));
                    rho = radius * j / flat; y = -half; nr = 0f; ny = -1f;
                }

                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    float c = Mathf.Cos(phi), si = Mathf.Sin(phi);
                    var onSurface = centre + new Vector3(rho * c, y, rho * si);
                    var normal = new Vector3(nr * c, ny, nr * si);
                    AddVertex(m, onSurface, normal, PlateCut * half);
                }
            }

            Bridge(m, first, rings, segments);
        }

        /// <summary>
        /// A plate under <see cref="Shape"/>: its outline pulled inward by lobes of its own, five to
        /// nine round the rim with a finer ragged term over them, never past three tenths of the
        /// radius, so the drawn plate stays inside the disc the physics holds. More segments round
        /// the rim, so the lobes are drawn rather than sampled.
        /// </summary>
        private static void AddShapedPlate(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony)
        {
            Vector3 centre = Local(colony, p.AX, p.AY, p.AZ);
            float radius = (float)p.Radius;
            float half = (float)p.HalfThickness;

            int segments = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * (radius + half) / 0.12f), 40, 120);
            int flat = Mathf.Clamp(Mathf.CeilToInt(radius / 0.35f), 3, 8);
            const int arc = 6;
            int rings = (flat + 1) + (arc - 1) + (flat + 1);
            int first = m.Vertices.Count;

            int lobes = 5 + (int)(SeedUnit(m.Seed + 5) * 5f);
            float phase1 = 2f * Mathf.PI * SeedUnit(m.Seed + 7);
            float phase2 = 2f * Mathf.PI * SeedUnit(m.Seed + 11);
            float shape = m.Shape;

            for (int ring = 0; ring < rings; ring++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    float c = Mathf.Cos(phi), si = Mathf.Sin(phi);

                    // The outline's share of the radius at this bearing, in [0.7, 1].
                    float lobe = 0.5f + 0.5f * Mathf.Sin(lobes * phi + phase1);
                    float ragged = Noise(new Vector3(c * 3.1f, si * 3.1f, m.Seed * 0.37f) + m.Offset);
                    float twin = 0.5f + 0.5f * Mathf.Sin((2 * lobes + 1) * phi + phase2);
                    float edge = 1f - shape * (0.05f + 0.13f * lobe * lobe + 0.06f * twin + 0.06f * ragged);
                    float outline = radius * edge;

                    float rho, y, nr, ny;

                    if (ring <= flat)
                    {
                        rho = outline * ring / flat; y = half; nr = 0f; ny = 1f;
                    }
                    else if (ring < flat + arc)
                    {
                        float angle = 0.5f * Mathf.PI - Mathf.PI * (ring - flat) / arc;
                        nr = Mathf.Cos(angle); ny = Mathf.Sin(angle);
                        rho = outline + half * nr; y = half * ny;
                    }
                    else
                    {
                        int j = flat - (ring - (flat + arc));
                        rho = outline * j / flat; y = -half; nr = 0f; ny = -1f;
                    }

                    var onSurface = centre + new Vector3(rho * c, y, rho * si);
                    var normal = new Vector3(nr * c, ny, nr * si);
                    AddVertex(m, onSurface, normal, PlateCut * half);
                }
            }

            Bridge(m, first, rings, segments);
        }

        /// <summary>
        /// A plate under <see cref="ShapeFurther"/>: the shaped plate's lobed outline, and its faces
        /// drawn together toward the rim, from the full half thickness out to
        /// <see cref="RimThinFrom"/> of the outline down to <see cref="RimThin"/> of it at the rim,
        /// whose round edge takes that thinner half thickness. A few more rings across the face carry
        /// the slope. The growth rings and radial ridges are the shader's.
        /// </summary>
        /// <remarks>
        /// Inside the physics' disc: a face point lies over the disc within the full half thickness,
        /// and a rim point lies within the thinner half thickness of a point on the outline, which is
        /// itself on the disc, so every point is within the half thickness of the disc. The stone's
        /// cut is scaled by the half thickness where it lies, so it never reaches the middle plane.
        /// </remarks>
        private static void AddFurtherPlate(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony)
        {
            Vector3 centre = Local(colony, p.AX, p.AY, p.AZ);
            float radius = (float)p.Radius;
            float half = (float)p.HalfThickness;

            int segments = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * (radius + half) / 0.12f), 40, 120);
            int flat = Mathf.Clamp(Mathf.CeilToInt(radius / 0.35f), 3, 8) + 3;
            const int arc = 6;
            int rings = (flat + 1) + (arc - 1) + (flat + 1);
            int first = m.Vertices.Count;

            // The shaped plate's lobes, from the same seeds.
            int lobes = 5 + (int)(SeedUnit(m.Seed + 5) * 5f);
            float phase1 = 2f * Mathf.PI * SeedUnit(m.Seed + 7);
            float phase2 = 2f * Mathf.PI * SeedUnit(m.Seed + 11);
            float shape = m.Shape;
            float rim = half * RimThin;

            for (int ring = 0; ring < rings; ring++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float phi = 2f * Mathf.PI * s / segments;
                    float c = Mathf.Cos(phi), si = Mathf.Sin(phi);

                    float lobe = 0.5f + 0.5f * Mathf.Sin(lobes * phi + phase1);
                    float ragged = Noise(new Vector3(c * 3.1f, si * 3.1f, m.Seed * 0.37f) + m.Offset);
                    float twin = 0.5f + 0.5f * Mathf.Sin((2 * lobes + 1) * phi + phase2);
                    float edge = 1f - shape * (0.05f + 0.13f * lobe * lobe + 0.06f * twin + 0.06f * ragged);
                    float outline = radius * edge;

                    float rho, y, nr, ny, thick;

                    if (ring <= flat)
                    {
                        float share = (float)ring / flat;
                        thick = PlateHalfAt(half, share);
                        rho = outline * share; y = thick; nr = 0f; ny = 1f;
                    }
                    else if (ring < flat + arc)
                    {
                        float angle = 0.5f * Mathf.PI - Mathf.PI * (ring - flat) / arc;
                        nr = Mathf.Cos(angle); ny = Mathf.Sin(angle);
                        thick = rim;
                        rho = outline + rim * nr; y = rim * ny;
                    }
                    else
                    {
                        int j = flat - (ring - (flat + arc));
                        float share = (float)j / flat;
                        thick = PlateHalfAt(half, share);
                        rho = outline * share; y = -thick; nr = 0f; ny = -1f;
                    }

                    var onSurface = centre + new Vector3(rho * c, y, rho * si);
                    var normal = new Vector3(nr * c, ny, nr * si);
                    AddVertex(m, onSurface, normal, PlateCut * thick);
                }
            }

            Bridge(m, first, rings, segments);
        }

        /// <summary>A plate's half thickness under <see cref="ShapeFurther"/> at a share of the way from its centre to its outline.</summary>
        private static float PlateHalfAt(float half, float share) =>
            half * Mathf.Lerp(1f, RimThin, Smooth01((share - RimThinFrom) / (1f - RimThinFrom)));

        // ------------------------------------------------------------------ the bush

        /// <summary>
        /// A capsule under <see cref="ShapeBold"/>: the further capsule thinned to <see cref="BushCore"/> of its
        /// radius as the branch's own core, a golden-angle spiral of short sprigs from the core out toward the
        /// capsule's surface, leaning up and along the branch, and on a tip a crown of splayed prongs, each
        /// ending in an axial cup.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Inside the physics' capsule.</b> The core is the further capsule of a smaller radius on the same
        /// segment, so its own proof puts it inside a capsule that lies inside this one. A sprig or a prong is a
        /// tapered capsule: every vertex lies in the convex hull of two spheres, one about its root on the
        /// branch's axis and one about its end, and the end is placed by <see cref="ReachInCapsule"/> so its
        /// sphere lies within the capsule's radius less <see cref="BushMargin"/>. The capsule is convex, so it
        /// holds the hull and every vertex in it. No sprig is cut by noise.
        /// </para>
        /// <para>
        /// The core is built with no tip of its own (no fork, no swollen polyp): the prongs take the tip. A
        /// trunk's sprigs start a radius above its foot, and its buried run carries none.
        /// </para>
        /// </remarks>
        private static void AddBushCapsule(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony, bool foot, bool tip)
        {
            CoralGeometry.Piece core = p;
            core.Radius = p.Radius * BushCore;
            AddFurtherCapsule(m, core, colony, foot, tip: false);
            PadTips(m);

            Vector3 a = Local(colony, p.AX, p.AY, p.AZ);
            Vector3 b = Local(colony, p.BX, p.BY, p.BZ);
            Vector3 axis = b - a;
            float length = axis.magnitude;
            axis = length > 1e-5f ? axis / length : Vector3.up;

            float r = (float)p.Radius;
            float limit = r * (1f - BushMargin);
            Vector3 helper = Mathf.Abs(axis.x) < 0.9f ? Vector3.right : Vector3.forward;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);

            // The sprigs, up a golden-angle spiral, each rooted on the axis inside the core.
            float from = foot ? r : 0f;
            float to = tip ? length - BushTipClear * r : length;
            int count = to > from ? Mathf.Clamp(Mathf.RoundToInt((to - from) / r * SprigsPerRadius), 0, SprigCap) : 0;
            float sprigEnd = SprigEnd * r;

            for (int i = 0; i < count; i++)
            {
                int d = 6 * i;
                float along = from + (to - from) * (i + 0.5f + SprigAlongJitter * (BushUnit(m.Seed, d, SprigStream) - 0.5f)) / count;
                float turn = i * GoldenAngle + 2f * SprigTurnJitter * (BushUnit(m.Seed, d + 1, SprigStream) - 0.5f);
                Vector3 radial = Mathf.Cos(turn) * u + Mathf.Sin(turn) * v;
                var jitter = new Vector3(
                    BushUnit(m.Seed, d + 2, SprigStream) - 0.5f,
                    BushUnit(m.Seed, d + 3, SprigStream) - 0.5f,
                    BushUnit(m.Seed, d + 4, SprigStream) - 0.5f);
                Vector3 direction = (radial + SprigLean * axis + SprigRise * Vector3.up + 2f * SprigJitter * jitter).normalized;
                if (direction.sqrMagnitude < 1e-6f || direction.y < SprigDownmost) continue;

                Vector3 root = a + axis * along;
                float room = ReachInCapsule(root, direction, a, b, limit - sprigEnd, SprigReachMost * r);
                float grown = room * Mathf.Lerp(SprigShortest, 1f, BushUnit(m.Seed, d + 5, SprigStream));
                if (grown < SprigLeast * r) continue;

                AddSprig(m, root, direction, grown, SprigRoot * r, sprigEnd, SprigSides, SprigTipMark, SprigTop.Round);
            }

            if (!tip) return;

            // The prongs, splayed evenly round the axis from a point inside the tip.
            int prongs = ProngsLeast + Mathf.Min(ProngsSpread - 1, (int)(BushUnit(m.Seed, 0, ProngStream) * ProngsSpread));
            float turn0 = 2f * Mathf.PI * BushUnit(m.Seed, 1, ProngStream);
            Vector3 spring = a + axis * Mathf.Max(0f, length - ProngRootBack * r);
            float prongEnd = ProngEnd * r;

            for (int j = 0; j < prongs; j++)
            {
                float splay = Mathf.Lerp(ProngSplayLow, ProngSplayHigh, BushUnit(m.Seed, 2 + 2 * j, ProngStream));
                float bearing = turn0 + 2f * Mathf.PI * (j + ProngBearingJitter * (BushUnit(m.Seed, 3 + 2 * j, ProngStream) - 0.5f)) / prongs;
                Vector3 radial = Mathf.Cos(bearing) * u + Mathf.Sin(bearing) * v;
                Vector3 direction = Mathf.Cos(splay) * axis + Mathf.Sin(splay) * radial;

                float room = ReachInCapsule(spring, direction, a, b, limit - prongEnd, ProngReachMost * r);
                AddSprig(m, spring, direction, room * ProngFill, ProngRoot * r, prongEnd, ProngSides, 1f, SprigTop.Cup);
            }
        }

        /// <summary>
        /// A plate under <see cref="ShapeBold"/>: the further plate thinned to <see cref="MatBody"/> of its half
        /// thickness and lowered to the bottom of the slab, and on its upper face a mat of short upright sprigs in
        /// a sunflower spiral, leaning outward toward the rim, as a table Acropora's surface stands.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Inside the physics' slab.</b> The body is a further plate of half thickness h' about a centre
        /// lowered by h - h', so every point of it lies within h' of the lowered disc and so within h of the
        /// physics' disc: its underside lies on the slab's lower face before its cut, as every level's plate
        /// does. A mat sprig is a tapered capsule whose root sphere lies inside the body and whose end sphere is
        /// placed by <see cref="ReachInSlab"/> within the half thickness less <see cref="BushMargin"/>, and the
        /// slab is convex.
        /// </para>
        /// <para>
        /// The mat keeps to <see cref="MatReach"/> of the plate's lobed outline (<see cref="BushOutline"/>), and
        /// each root sits halfway up the body where it stands, so every sprig's open root is hidden in the body.
        /// </para>
        /// </remarks>
        private static void AddBushPlate(Mesher m, in CoralGeometry.Piece p, CoralGeometry.Colony colony)
        {
            float half = (float)p.HalfThickness;
            float body = half * MatBody;
            CoralGeometry.Piece slab = p;
            slab.HalfThickness = body;
            slab.AY = p.AY - (p.HalfThickness - body);
            AddFurtherPlate(m, slab, colony);
            PadTips(m);
            m.MatFrom = m.Vertices.Count;

            Vector3 centre = Local(colony, p.AX, p.AY, p.AZ);
            float radius = (float)p.Radius;
            float middle = centre.y - (half - body);
            float matEnd = MatEnd * half;
            float room = half * (1f - BushMargin) - matEnd;

            // The shaped plate's lobes, from the same seeds.
            int lobes = 5 + (int)(SeedUnit(m.Seed + 5) * 5f);
            float phase1 = 2f * Mathf.PI * SeedUnit(m.Seed + 7);
            float phase2 = 2f * Mathf.PI * SeedUnit(m.Seed + 11);

            int count = Mathf.Min(MatCap, Mathf.CeilToInt(Mathf.PI * radius * radius / (MatSpacing * MatSpacing)));
            for (int i = 0; i < count; i++)
            {
                int d = 6 * i;
                float share = Mathf.Sqrt((i + 0.5f) / count) * MatReach;
                float phi = i * GoldenAngle + 2f * MatTurnJitter * (BushUnit(m.Seed, d, MatStream) - 0.5f);
                float c = Mathf.Cos(phi), si = Mathf.Sin(phi);
                float rho = share * BushOutline(m, radius, phi, lobes, phase1, phase2);
                float thick = PlateHalfAt(body, share);

                var root = new Vector3(centre.x + rho * c, middle + 0.5f * thick, centre.z + rho * si);
                var outward = new Vector3(c, 0f, si);
                var jitter = new Vector3(
                    BushUnit(m.Seed, d + 1, MatStream) - 0.5f,
                    BushUnit(m.Seed, d + 2, MatStream) - 0.5f,
                    BushUnit(m.Seed, d + 3, MatStream) - 0.5f);
                Vector3 direction = (Vector3.up + MatSplay * (rho / radius) * outward + 2f * MatJitter * jitter).normalized;

                float reach = ReachInSlab(root, direction, centre, radius, room, MatReachMost * half);
                float grown = reach * Mathf.Lerp(MatShortest, 1f, BushUnit(m.Seed, d + 4, MatStream));
                if (grown < MatLeast * half) continue;

                AddSprig(m, root, direction, grown, MatRoot * half, matEnd, MatSides, MatTipMark, SprigTop.Blunt);
            }
        }

        /// <summary>
        /// A shaped plate's outline at a bearing, m: the lobes, the twin and the ragged term of
        /// <see cref="AddShapedPlate"/> and <see cref="AddFurtherPlate"/>, from the same seeds, so the mat follows
        /// the lobed rim the body draws. Written apart rather than shared, so neither of those changes.
        /// </summary>
        private static float BushOutline(Mesher m, float radius, float phi, int lobes, float phase1, float phase2)
        {
            float c = Mathf.Cos(phi), si = Mathf.Sin(phi);
            float lobe = 0.5f + 0.5f * Mathf.Sin(lobes * phi + phase1);
            float ragged = Noise(new Vector3(c * 3.1f, si * 3.1f, m.Seed * 0.37f) + m.Offset);
            float twin = 0.5f + 0.5f * Mathf.Sin((2 * lobes + 1) * phi + phase2);
            return radius * (1f - m.Shape * (0.05f + 0.13f * lobe * lobe + 0.06f * twin + 0.06f * ragged));
        }

        /// <summary>How a sprig ends (<see cref="AddSprig"/>). Every end lies inside the sphere of the sprig's end radius.</summary>
        private enum SprigTop
        {
            /// <summary>A rounded end, as a branch's sprig has: four rings.</summary>
            Round,

            /// <summary>An axial cup, a lip round a shallow dish, as a tip's prong has: five rings.</summary>
            Cup,

            /// <summary>A low cone, the cheapest end, as a plate's mat sprig has: three rings.</summary>
            Blunt,
        }

        /// <summary>
        /// One sprig: a tapered capsule from a ring of <paramref name="rootRadius"/> at its root to one of
        /// <paramref name="endRadius"/> at its end, closed as <paramref name="top"/> says. The root is left open,
        /// as it lies buried in the core or the plate's body. Its cavity reading falls from
        /// <see cref="SprigRootShade"/> at the root to none at the end, and its tip reading rises from 0 to
        /// <paramref name="tipMark"/>.
        /// </summary>
        private static void AddSprig(
            Mesher m, Vector3 root, Vector3 direction, float length, float rootRadius, float endRadius, int sides,
            float tipMark, SprigTop top)
        {
            Vector3 helper = Mathf.Abs(direction.x) < 0.9f ? Vector3.right : Vector3.forward;
            Vector3 u = Vector3.Cross(direction, helper).normalized;
            Vector3 v = Vector3.Cross(direction, u);
            Vector3 end = root + direction * length;
            float slope = (rootRadius - endRadius) / Mathf.Max(1e-4f, length);
            bool cup = top == SprigTop.Cup;
            int rings = cup ? 5 : top == SprigTop.Blunt ? 3 : 4;
            int first = m.Vertices.Count;

            for (int ring = 0; ring < rings; ring++)
            {
                // Each ring's centre and radius, and its analytic normal's parts across and along the sprig.
                Vector3 centre;
                float round, across, ahead;
                float cavity = 0f, mark = tipMark;

                if (ring == 0)
                {
                    centre = root; round = rootRadius; across = 1f; ahead = slope;
                    cavity = SprigRootShade; mark = 0f;
                }
                else if (ring == 1)
                {
                    centre = end; round = endRadius; across = 1f; ahead = slope;
                }
                else if (ring == 2 && cup)
                {
                    centre = end + direction * (CupLipAlong * endRadius); round = CupLipRound * endRadius;
                    across = CupLipRound; ahead = CupLipAlong;
                }
                else if (ring == 2 && top == SprigTop.Blunt)
                {
                    // A blunt end's pole, a low cone over the end.
                    centre = end + direction * (BluntAlong * endRadius); round = 0f;
                    across = 0f; ahead = 1f;
                }
                else if (ring == 2)
                {
                    centre = end + direction * (SprigCapSin * endRadius); round = SprigCapCos * endRadius;
                    across = SprigCapCos; ahead = SprigCapSin;
                }
                else if (ring == 3 && cup)
                {
                    // The cup's wall faces in and up, toward the cup's middle.
                    centre = end + direction * (CupWallAlong * endRadius); round = CupWallRound * endRadius;
                    across = -1f; ahead = 1f;
                }
                else
                {
                    // The pole: a rounded end's, or the cup's floor.
                    centre = end + direction * ((cup ? CupFloorAlong : 1f) * endRadius); round = 0f;
                    across = 0f; ahead = 1f;
                }

                for (int s = 0; s < sides; s++)
                {
                    float phi = 2f * Mathf.PI * s / sides;
                    Vector3 radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * v;
                    Vector3 normal = (radial * across + direction * ahead).normalized;
                    AddBushVertex(m, centre + radial * round, normal, cavity, mark);
                }
            }

            Bridge(m, first, rings, sides);
        }

        /// <summary>One vertex of the bush, uncut: its cavity reading as given, and its tip reading kept level with the vertex list.</summary>
        private static void AddBushVertex(Mesher m, Vector3 position, Vector3 normal, float cavity, float tipMark)
        {
            m.Vertices.Add(position);
            m.Normals.Add(normal);
            m.Colours.Add(new Color32(
                (byte)Mathf.RoundToInt(255f * cavity),
                (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f + 0.5f * normal.y)),
                255,
                255));
            m.Tips.Add(tipMark);
        }

        /// <summary>Brings the tip readings level with the vertex list, 0 for every vertex the stone's builders added.</summary>
        private static void PadTips(Mesher m)
        {
            while (m.Tips.Count < m.Vertices.Count) m.Tips.Add(0f);
        }

        /// <summary>
        /// How far from <paramref name="from"/> along <paramref name="direction"/> a point stays within
        /// <paramref name="room"/> of the segment from <paramref name="a"/> to <paramref name="b"/>, up to
        /// <paramref name="most"/>. The distance to a segment is convex along a line, so from a start within
        /// reach the points within it are one interval, and halving finds its far end from inside.
        /// </summary>
        private static float ReachInCapsule(Vector3 from, Vector3 direction, Vector3 a, Vector3 b, float room, float most)
        {
            if (room <= 0f) return 0f;
            if (SegmentDistance(from + direction * most, a, b) <= room) return most;

            float lo = 0f, hi = most;
            for (int k = 0; k < ReachSteps; k++)
            {
                float mid = 0.5f * (lo + hi);
                if (SegmentDistance(from + direction * mid, a, b) <= room) lo = mid;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// <see cref="ReachInCapsule"/> for a plate: within <paramref name="room"/> of the horizontal disc of
        /// <paramref name="radius"/> about <paramref name="centre"/>, which is convex as well.
        /// </summary>
        private static float ReachInSlab(Vector3 from, Vector3 direction, Vector3 centre, float radius, float room, float most)
        {
            if (room <= 0f) return 0f;
            if (DiscDistance(from + direction * most, centre, radius) <= room) return most;

            float lo = 0f, hi = most;
            for (int k = 0; k < ReachSteps; k++)
            {
                float mid = 0.5f * (lo + hi);
                if (DiscDistance(from + direction * mid, centre, radius) <= room) lo = mid;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>A point's distance to the segment from <paramref name="a"/> to <paramref name="b"/>: a capsule's, before its radius.</summary>
        private static float SegmentDistance(Vector3 q, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-10f, ab.sqrMagnitude));
            return (q - (a + ab * t)).magnitude;
        }

        /// <summary>A point's distance to a horizontal disc: a plate's, before its half thickness.</summary>
        private static float DiscDistance(Vector3 q, Vector3 centre, float radius)
        {
            float dx = q.x - centre.x, dz = q.z - centre.z;
            float beyond = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - radius);
            float dy = q.y - centre.y;
            return Mathf.Sqrt(beyond * beyond + dy * dy);
        }

        /// <summary>
        /// One draw of the bush's streams, in [0, 1): a hash of the piece (<see cref="Mesher.Seed"/>, the colony's
        /// index and the piece's), the draw and a stream's salt, so the same colony grows the same bush every
        /// time and no draw the stone or the palettes make is moved.
        /// </summary>
        private static float BushUnit(int piece, int draw, uint stream)
        {
            unchecked
            {
                uint h = (uint)piece * 0x85ebca6bu ^ (uint)draw * 0xc2b2ae35u ^ stream;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>
        /// One vertex, cut inward along its normal by <paramref name="depthMax"/> times a noise
        /// in [0, 1] sampled at its place in the colony's frame, so two pieces that cross cut the
        /// same depth where they meet, and by <paramref name="extra"/> beyond it (the shape's).
        /// </summary>
        private static void AddVertex(Mesher m, Vector3 onSurface, Vector3 normal, float depthMax, float extra = 0f)
        {
            float cut = Cut(onSurface + m.Offset);

            m.Vertices.Add(onSurface - normal * (depthMax * cut + extra));
            m.Normals.Add(normal);
            m.Colours.Add(new Color32(
                (byte)Mathf.RoundToInt(255f * cut),
                (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f + 0.5f * normal.y)),
                255,
                255));
        }

        /// <summary>
        /// One vertex under <see cref="ShapeFurther"/>: cut inward as <see cref="AddVertex"/> cuts, the
        /// stone's share of it scaled by <paramref name="stone"/> (and its cavity reading with it), the
        /// whole depth held to <paramref name="limit"/>.
        /// </summary>
        private static void AddVertexWithin(Mesher m, Vector3 onSurface, Vector3 normal, float depthMax, float stone, float extra, float limit)
        {
            float cut = Cut(onSurface + m.Offset) * stone;

            m.Vertices.Add(onSurface - normal * Mathf.Min(depthMax * cut + extra, limit));
            m.Normals.Add(normal);
            m.Colours.Add(new Color32(
                (byte)Mathf.RoundToInt(255f * cut),
                (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f + 0.5f * normal.y)),
                255,
                255));
        }

        /// <summary>Joins consecutive rings of a lathe into triangles, wrapping the last segment to the first.</summary>
        private static void Bridge(Mesher m, int first, int rings, int segments)
        {
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = first + r * segments + s;
                    int b = first + r * segments + (s + 1) % segments;
                    int d = a + segments;
                    int e = b + segments;

                    m.Triangles.Add(a); m.Triangles.Add(b); m.Triangles.Add(d);
                    m.Triangles.Add(b); m.Triangles.Add(e); m.Triangles.Add(d);
                }
            }
        }

        // ------------------------------------------------------------------ the cut's noise

        /// <summary>The cut's share in [0, 1]: lumps about a metre across, knobs a third of that and a fine crag.</summary>
        private static float Cut(Vector3 q)
        {
            float lumps = Noise(q / 1.1f);
            float knobs = Noise(q / 0.35f);
            float crag = Mathf.Abs(2f * Noise(q / 0.18f) - 1f);
            return Mathf.Clamp01(0.45f * lumps + 0.35f * knobs + 0.2f * crag);
        }

        private static Vector3 SeedOffset(int seed)
        {
            uint h = (uint)seed;
            if (h == 0u) h = 0x9e3779b9u;
            float Next()
            {
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                return (h & 0xffffff) / 16777216f;
            }
            return new Vector3(Next() * 500f, Next() * 500f, Next() * 500f);
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>
        /// The distance to the nearest of one jittered point per unit cell, in [0, about 1]: 0 at a
        /// knob's middle, highest at the walls between knobs.
        /// </summary>
        private static float Worley(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float best = 9f;

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = ix + dx, cy = iy + dy, cz = iz + dz;
                var point = new Vector3(
                    cx + Hash(cx, cy, cz),
                    cy + Hash(cx + 31, cy + 17, cz + 7),
                    cz + Hash(cx + 13, cy + 47, cz + 29));
                best = Mathf.Min(best, (p - point).sqrMagnitude);
            }

            return Mathf.Min(1f, Mathf.Sqrt(best));
        }

        /// <summary>Value noise in three dimensions, in [0, 1], with the quintic fade the rock shader's has.</summary>
        private static float Noise(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float uz = fz * fz * fz * (fz * (fz * 6f - 15f) + 10f);

            float a = Hash(ix, iy, iz), b = Hash(ix + 1, iy, iz);
            float c = Hash(ix, iy + 1, iz), d = Hash(ix + 1, iy + 1, iz);
            float e = Hash(ix, iy, iz + 1), f = Hash(ix + 1, iy, iz + 1);
            float g = Hash(ix, iy + 1, iz + 1), h = Hash(ix + 1, iy + 1, iz + 1);

            float x0 = Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
            float x1 = Mathf.Lerp(Mathf.Lerp(e, f, ux), Mathf.Lerp(g, h, ux), uy);
            return Mathf.Lerp(x0, x1, uz);
        }
    }
}
