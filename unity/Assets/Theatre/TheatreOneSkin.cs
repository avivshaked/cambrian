using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using Evosim.Core;
using Evosim.Sim;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Evosim.Theatre
{
    /// <summary>
    /// One smooth surface over a body's tissues (<c>EVOSIM_THEATRE_ONE_SKIN</c>, the spike of
    /// <c>scratch/theatre-realism/skin-plan.md</c>'s ONE SKIN): the parts of the body's cell types are
    /// drawn as one skinned mesh, and the specialised parts as they are today.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The field.</b> Each merged part is the signed distance of the shape it is drawn as, in its own
    /// frame. A box is the rounded cube the palette swaps in, under ShapeBox's taper and bend and, with
    /// <c>EVOSIM_THEATRE_ORGANIC</c> on, the organic silhouette and the soft tissue's margin: the shader's
    /// map is inverted in closed form and the rounded cube's field read at the inverse (<see cref="DrawnBox"/>).
    /// A capsule is its collider squashed across by the palette's aspect, a short one the one ellipsoid the
    /// palette draws it as, and a sphere the ellipsoid it is drawn as. A box drawn as a leaf, and every part
    /// of a body with no palette, keep the collider's rounded box. Every primitive is inset by k/4 and joined
    /// by the quadratic polynomial smooth minimum, which is never more than k/4 below the plain minimum per
    /// fold; the fold is clamped at the plain minimum less k/4, so the bound holds for any number of parts
    /// and the skin lies inside the union of the drawn parts, and so of the colliders.
    /// k = min(0.5 x the median of the parts' smallest half extents, 3 cm).
    /// </para>
    /// <para>
    /// <b>The joints.</b> A child is developed with its anchor on its parent's anchor, so two
    /// colliders meet at a point or a face and the inset union meets there with no thickness at all.
    /// At level 2 a ball is added at the anchor of every joint between two merged parts, of radius
    /// min(k, half the thinner part's smallest half extent) after the inset, so the skin is carried
    /// across the joint. That ball is the one place the skin may leave the colliders, by at most its
    /// radius plus k/4 from the anchor. Level 1 is the specification as written, with no balls.
    /// </para>
    /// <para>
    /// <b>The no-box join (<c>EVOSIM_THEATRE_NO_BOX_JOIN</c>).</b> At NO_BOX level 2 every box is an ellipsoid of its half extents,
    /// and two ellipsoids whose boxes met across a whole face meet only near their tips, which the global k (3 cm at most)
    /// cannot bridge. At every joint between two merged parts of which at least one is such an ellipsoid, the two parts' fields
    /// are therefore joined by a smooth minimum of their own, <see cref="TheatreNoBox.JoinShare"/> of the thinner part's smallest
    /// half extent in radius (the pair blend of level 4, with its own radius and no change to the weights), and a ball of
    /// <see cref="TheatreNoBox.JoinBallShare"/> of that radius at the anchor is united with it at the global k. It is local to
    /// the joint: the blend lowers the field only where a point is within a quarter of its radius of the nearer part and within
    /// its radius of the other, which is the neighbourhood of the two surfaces' meeting, and the ball is at the anchor. The bound
    /// is given up there: the skin may leave the union of the drawn parts by at most a quarter of the blend's radius less k/4,
    /// within the blend's reach of the joint, and the ball is inside both colliders while the anchor stands at a face's middle
    /// (a ball of at most the thinner part's smallest half extent) and may leave them by at most its radius where it does not.
    /// The grid's bounds and the cull margin grow to hold both. At 0 no join is made and nothing else changes.
    /// </para>
    /// <para>
    /// <b>The mesh.</b> Sampled once in the adult's rest frame (the phenotype's own positions and
    /// rotations, divided by its <c>ScaledBy</c>), surface nets, two passes of Taubin smoothing, every
    /// vertex left outside pulled back to the surface along the gradient, normals from the gradient.
    /// Skinned to one bone per merged part, a <c>SkinBone</c> under the part's transform, so a growth
    /// step is a scale on the bones and never a rebuild. Each vertex also carries, for the body shader:
    /// its rest position (UV5) and rest normal (UV6), so the markings ride on the tissue; its own half
    /// thickness in rest metres (UV3's y), marched along the inward normal, for the light through it; and
    /// its tissue colour (the vertex colour), the merged parts' painted colours blended by its weights.
    /// </para>
    /// <para>
    /// <b>The bounds.</b> The renderer's bounds are authored, not recomputed (<c>updateWhenOffscreen</c> off):
    /// <see cref="Posed"/> carries each bone's bind-space box of the vertices it dominates through the bone's pose
    /// into the skin object's space, because the parts carry the body's attitude and the skin object never turns.
    /// Skinning weights under 1e-3 are dropped, so no bone carries the whole mesh.
    /// </para>
    /// <para>
    /// <b>The carve (level 3).</b> The shader cuts one field for the whole body at the vertex's rest position over
    /// one body-wide length, inward along the skinned normal by a share of the vertex's own half thickness; each
    /// tissue's character (<see cref="BlendCarve"/>) is blended per vertex by the weights into UV7.
    /// </para>
    /// <para>
    /// <b>The rig (level 4).</b> Level 3 with three changes, so that the body reads as one skinned creature. The joint
    /// between two merged parts is a ball and a pair blend whose radius is <see cref="RigJointShare"/> of the thinner
    /// part's smallest half extent, with no cap (the global k stays, so away from joints the skin is as before). The
    /// weights across a joint are the two bones' alone over a width of about the ball, so a bend flexes the tissue. And
    /// every part joins the mesh unless its type draws itself in a way the skin cannot (<see cref="RigMerges"/>).
    /// </para>
    /// <para>
    /// <b>Off, nothing.</b> With the dial at 0 <see cref="Build"/> returns null before it reads
    /// anything, and <see cref="LiveWorldView"/> calls nothing else.
    /// </para>
    /// </remarks>
    public sealed class TheatreOneSkin
    {
        /// <summary>
        /// The dial: 0 off, 1 the skin as specified, 2 the skin with a ball at every merged joint, 3 level 2 with
        /// the carve at rest (<see cref="Carved"/>).
        /// </summary>
        public static readonly float Level = TheatreSkin.Dial("EVOSIM_THEATRE_ONE_SKIN", 0f, 0f, 6f);

        public static readonly bool Enabled = Level >= 0.5f;

        /// <summary>Whether a ball carries the skin across each joint between two merged parts (level 2).</summary>
        public static readonly bool JointBalls = Level >= 1.5f;

        /// <summary>
        /// Whether the skin is carved at rest (level 3): one carve field for the whole body, read at the vertex's rest
        /// position over one body-wide length, each tissue's character blended per vertex by the skinning weights, the
        /// depth a share of the vertex's own half thickness. The shader is told by <c>_SkinCarve</c>, set at this level
        /// alone, and every term of it sits behind that.
        /// </summary>
        public static readonly bool Carved = Level >= 2.5f;
        // RIG4 begin
        /// <summary>
        /// Whether the body is a rig (level 4): the joint ball and the pair blend scale with the thinner part, the weights
        /// across a joint are the two bones' over a width of about the ball, and every part whose type can be drawn by the
        /// skin is in the mesh. Every statement of it sits behind this.
        /// </summary>
        public static readonly bool Rigged = Level >= 3.5f;
        // RIG4 end
        // RIG5 begin
        /// <summary>
        /// Whether the rig fills to the collider at each joint (level 5): level 4 with two changes there. Within a reach of each
        /// joint's face every merged part's field eases by a smooth ramp from the shape it is drawn as to its collider (a box's
        /// edges along the face normal keep level 1's rounding), so its taper, organic cut and end rounding fall to nothing at the
        /// face and two faces that meet in the world meet in the drawing; the skin's carve eases out over the same ramp. The joint
        /// ball goes, and the two parts are joined by a fillet: a smooth minimum at <see cref="Rig5FilletShare"/> of the thicker
        /// part's smallest half extent, faded out with the ramp and held inside the union of the two colliders and the wedge
        /// between their faces. The weights are level 4's. Every statement of it sits behind this, or behind
        /// <see cref="SkinFill"/> above 0, which is 0 below level 5.
        /// </summary>
        public static readonly bool Filleted = Level >= 4.5f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_SKIN_FILL</c> (0 to 1, default 0), read at level 5 alone: the skin's drawn box cuts its taper, organic
        /// silhouette and soft-tissue margin, and the skin its carve, by 1 - fill of their depth, so that at 1 a box is the
        /// rounded cube the palette swaps in, inside its collider but for its rounded edges and corners. 0 below level 5.
        /// </summary>
        public static readonly float SkinFill = Filleted ? TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_FILL", 0f, 0f, 1f) : 0f;
        // RIG5 end
        // RIG6 begin
        /// <summary>
        /// Whether the rig follows a bent hinge (level 6): level 5 with two changes. (1) The sleeve: at a joint whose larger part, as
        /// drawn, leaves out the smaller part's section just under its face (a taper or an organic end narrowing toward the joint),
        /// the smaller part's section is carried straight into the larger part along its face normal, inside the larger part's
        /// collider, so the skin narrows no further than the smaller part; and the larger part's ramp eases to its collider only
        /// inside its drawn shape and that sleeve, so no bead stands at the joint. (2) The bend: per frame, near each hinge bent past
        /// <see cref="Rig6BendLow"/>, the skin's vertices move onto the field evaluated with each part in its posed frame (implicit
        /// skinning), the move carried back into rest space so that Unity's skinning puts them there. Every statement of it sits
        /// behind this.
        /// </summary>
        public static readonly bool BentHinge = Level >= 5.5f;
        // BEYOND begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_RIG6_BEYOND</c> (0 to 0.5, default 0), read at level 6 alone, a test the owner rules on with D242: at a
        /// hinge whose contact is a round end on a face, the fillet may reach outside the posed colliders and the wedge by up to this
        /// many of the round end's radius r, and its radius grows to at least 4 times that, so the bend can read as a finger. Every
        /// picture then carries <see cref="BeyondLabel"/>. 0 below level 6.
        /// </summary>
        public static readonly float Beyond6 = BentHinge ? TheatreSkin.Dial("EVOSIM_THEATRE_RIG6_BEYOND", 0f, 0f, 0.5f) : 0f;

        /// <summary>The label every picture carries while the BEYOND test is on (SnapshotCamera.LabelLines); null when it is off.</summary>
        public static string BeyondLabel => Beyond6 > 0f ? "joint tissue beyond the colliders (test)" : null;
        // BEYOND end
        // RIG6 end
        // SEAT begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_SKIN_SEAT</c> (0 to 4, default 0, off), read at every level: where another part stands on a drawn
        /// box's collider, at a joint anchor, the box's taper, bend, organic silhouette and soft-tissue margin ease away over
        /// a reach of this many of the other part's smallest half extent (full inside it, gone by twice it), so the drawn face
        /// meets the collider's there and the joined part stands on the body. Useful from 1 to 4. 0 builds no seat and
        /// evaluates nothing.
        /// </summary>
        public static readonly float SeatDial = TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_SEAT", 0f, 0f, 4f);

        /// <summary>
        /// <c>EVOSIM_THEATRE_SKIN_SEAT_PROBE</c> (1 on, default off): logs, per body, each seat's drop (how far under the
        /// collider's face the drawn surface lies on the face normal at the anchor) with the seat off and on. Logging only,
        /// whatever <see cref="SeatDial"/> is.
        /// </summary>
        public static readonly bool SeatProbe = System.Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SKIN_SEAT_PROBE") == "1";
        // SEAT end
        // THICKCAP begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_SKIN_THICK_CAP</c> (0, 1 or 2, default 2 since the owner ruled cap 2 for the film; 0 is the old uncapped look),
        /// read once, at every skin level: what bounds a vertex's half thickness (UV3.y), which the level-3 carve and the mouth cut read;
        /// SKIN2's marks read the thickness from before the cap (D281). 0 leaves the normal chord alone. 1 caps
        /// it at the smallest plain half extent of the vertex's dominant bone. 2 takes the smallest of the normal chord, the chord
        /// through the field along that bone's thin axis (the axis of its smallest half extent, in the rest frame, both ways from the
        /// vertex), and the plain-extent cap, so a tapered tip, far thinner than the part's plain extent, is bounded by its own sheet.
        /// </summary>
        public static readonly int ThickCap = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_THICK_CAP", 2f, 0f, 2f));
        // THICKCAP end
        // ORGANSEAT begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_SEAT_MARKER</c> (0 or 1, default 0), read once, at every level of the one skin: a diagnostic. At 1, at
        /// each joint where a part stands apart from the skin and its parent is in it, the palette's knuckles and bridges are hidden
        /// and the parent's drawn box takes no <see cref="SeatDial"/> seat for that part. Off, no statement of it runs.
        /// </summary>
        public static readonly bool SeatMarkerOff = TheatreSkin.Dial("EVOSIM_THEATRE_SEAT_MARKER", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_ORGAN_SEAT</c> (0 to 2, default 0; 1 from 0.5, and from 1.5 the cone's spread at 0.5), read once, at every level of the one skin: a part standing apart on a
        /// Fixed joint whose parent is a drawn box in the skin is seated on the drawn skin. In place of <see cref="SeatDial"/>'s ball
        /// the parent's box takes a cone seat there (<see cref="SeatSet.ConeWeight"/>), so its face is pulled out to the collider's
        /// across the part's footprint and runs back into the body down 45-degree flanks; the palette's bridges at that joint are
        /// hidden and its knuckles kept. Off, no statement of it runs.
        /// </summary>
        public static readonly bool OrganSeat = TheatreSkin.Dial("EVOSIM_THEATRE_ORGAN_SEAT", 0f, 0f, 2f) >= 0.5f;

        /// <summary>
        /// The cone's radius at the face, as a share of the part's largest half reach across the face: one, so the plateau is the
        /// part's back face and the snout's skirt and the knuckles cover its rim.
        /// </summary>
        private const float OrganSeatFootprint = 1f;

        /// <summary>
        /// How fast the cone widens below the face, in metres across for each metre in: one, a 45-degree flank, which reads as the
        /// body running out into the part; a steeper flank reads as the stalk this replaces. At an ORGAN_SEAT dial of 1.5 or more,
        /// 0.5: a narrower cone that un-tapers less of the parent's front where its drawn face recedes far behind the collider's.
        /// </summary>
        private static readonly float OrganSeatSpread = TheatreSkin.Dial("EVOSIM_THEATRE_ORGAN_SEAT", 0f, 0f, 2f) >= 1.5f ? 0.5f : 1f;

        /// <summary>
        /// The width of the smoothstep fade beyond the cone's side and beyond its depth, as a share of its radius at the face: one,
        /// as SKIN_SEAT's ball fades from r to 2r.
        /// </summary>
        private const float OrganSeatBand = 1f;

        /// <summary>
        /// The prefix the palette names a bridge with (<c>TheatrePalette.BuildNecks</c>: "Bridge (parent)", "Bridge (child)"), the one
        /// mark of a neck's kind the skin can read from the palette's list.
        /// </summary>
        private const string BridgeNamePrefix = "Bridge";
        // ORGANSEAT end
        // WHOLE begin

        /// <summary>
        /// <c>EVOSIM_THEATRE_SKIN_WHOLE</c> (0 or 1, default 0), read once, at the rig levels alone (<see cref="Rigged"/>) and not while
        /// <c>EVOSIM_THEATRE_SKIN_BODY</c> is set (that list rules). On an animal, a body with at least one link part, the skin wraps
        /// every part but its organs, the nose and the holdfast: a producer joins the mesh whether or not it is drawn as a blade, and a
        /// thin part is kept by refining the grid to hold it, to at most <see cref="WholeMaxCells"/> cells an axis. A body with no link
        /// part keeps the rule above. Off, no statement behind it runs.
        /// </summary>
        public static readonly bool WholeOn =
            Rigged && TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_WHOLE", 0f, 0f, 1f) >= 0.5f &&
            string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SKIN_BODY"));

        /// <summary>The most cells the refined grid has on an axis, its padding (<see cref="PadCells"/> a side) and the one past the last included.</summary>
        private const int WholeMaxCells = 128;

        /// <summary>The refined cell is never under the longest extent over this: the cap less the padding and a cell of rounding.</summary>
        private const int WholeCapDivisor = WholeMaxCells - 2 * PadCells - 2;

        /// <summary>The estimate of the longest extent, before the cap is judged, is taken this much over: the grid's rim adds to the oriented boxes.</summary>
        private const float WholeBoundsMargin = 1.05f;

        /// <summary>A part is left out at the cap when its smallest half extent is under this share of ThinPartCells cells, so that one cell exactly 1.5 of them is kept.</summary>
        private const float WholeSlack = 0.999f;
        // WHOLE end

        // JOIN begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_SKIN_JOIN</c> (0 or 1, default 0), read at the rig levels (4 to 6) alone: the skin's pattern keeps one
        /// cover and one pigment over the whole body, so a limb's pattern does not change at its joint, and SKIN2's crease at a
        /// joint is a soft fold. Shader terms only, under the local keyword <see cref="JoinKeyword"/>, which
        /// <see cref="KeywordedOf"/> turns on in the skin's material copy while this is on; nothing moves. False below level 4.
        /// </summary>
        public static readonly bool SkinJoin = Rigged && TheatreSkin.Dial("EVOSIM_THEATRE_SKIN_JOIN", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The body shader's keyword for <see cref="SkinJoin"/>, local to the skin's material copy.</summary>
        private const string JoinKeyword = "_EVO_SKIN_JOIN";
        // JOIN end
        // JOINDEBUG begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_JOIN_DEBUG</c> (0 to 4, default 0), read once: a diagnostic, never a look, that finds what makes the skin
        /// plain in a joint's blend. Above 0 the skin's final colour is replaced by an unlit false colour, one view per value (1 the
        /// triplanar plane weights, 2 the maze cover, 3 the band, the thin backlight and the crease mark as red, green and blue, 4 the
        /// maze's stretch against the drawn surface), under the local keyword <see cref="JoinDebugKeyword"/>, which
        /// <see cref="KeywordedOf"/> turns on in the skin's material copy with the global <c>_EvoJoinDebug</c> at the value. Shader
        /// terms only: nothing moves. Views 1 and 2 need <c>EVOSIM_THEATRE_SKIN_JOIN</c>.
        /// </summary>
        public static readonly int JoinDebug = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_JOIN_DEBUG", 0f, 0f, 4f));

        /// <summary>The body shader's keyword for <see cref="JoinDebug"/>, local to the skin's material copy.</summary>
        private const string JoinDebugKeyword = "_EVO_JOIN_DEBUG";

        private static readonly int JoinDebugId = Shader.PropertyToID("_EvoJoinDebug");
        // JOINDEBUG end
        // PADDLE begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_PADDLE_LIMB</c> (0 or 1, default 0), read once, at the rig levels (4 to 6) alone: a link part is drawn
        /// as a paddle (<see cref="PaddleShape"/>), its section the shape drawn today at the root, morphing along its length into a
        /// flattened ellipse at its free end, inside its collider (D242). Only the field the skin is marched from changes, so the
        /// skin stays one surface; the collider and the world are untouched. False below level 4, and off no statement of it runs.
        /// </summary>
        public static readonly bool PaddleOn = Rigged && TheatreSkin.Dial("EVOSIM_THEATRE_PADDLE_LIMB", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// A link is reshaped when its half length is at least this many of its radius (a capsule: half span plus radius, over the
        /// radius; a box: its longest half extent over its middle one), so its shaft is at least 1.5 diameters: room for a round
        /// root, the blend and the blade's rounded end. A capsule the palette draws as one ellipsoid (half span under its radius)
        /// is far below it and is left alone.
        /// </summary>
        private const float PaddleLengthShare = 2.5f;

        /// <summary>
        /// The root's joint anchor lies at least this share of the half length from the part's centre along its long axis: a link
        /// joined at an end. One joined at its side has no free end, and is left round.
        /// </summary>
        private const float PaddleRootShare = 0.5f;

        /// <summary>
        /// The section is today's over this share of the way from the root end to the start of the blade's rounded end, and is the
        /// blade's from that start on; between, the two fields are blended by a smootherstep (6u^5 - 15u^4 + 10u^3, continuous in
        /// slope and curvature), so the profile has no kink. A quarter keeps the root, where the sleeve, the fillet and the weights'
        /// joint zone act, the shape they meet today.
        /// </summary>
        private const float PaddleStartShare = 0.25f;

        /// <summary>The blade's visible half thickness at its tip as a share of its visible half width (both after the k/4 inset): a third.</summary>
        private const float PaddleMinorShare = 1f / 3f;

        /// <summary>
        /// A capsule drawn with its narrow semi-axis at least this share of its wide one counts as round, and its blade lies in the
        /// plane of its long axis and its hinge axis, so the stroke meets it broadside; a
        /// flatter capsule keeps its wider extent as the blade's width.
        /// </summary>
        private const float PaddleRoundShare = 0.8f;
        // PADDLE end
        // WIDTH begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_PADDLE_WIDTH</c> (1 to 3, default 1), read once, with <see cref="PaddleOn"/> alone: an owner preview that
        /// draws outside the collider (D242), on purpose and only while it is above 1. A round capsule's blade, whose half width is
        /// the collider's radius at the root, grows along the blend (<see cref="PaddleShape.At"/>) to this many times the radius at the
        /// free end, so the blade seen face-on is wider than the rod. The blade's thickness is the dial-off one. A box and a flat
        /// capsule are unchanged. 1 leaves every pixel as it is, and 3 is the dial's top: three radii a side already
        /// stands two radii past the collider, enough to see the question the preview asks. 1 when the paddle is off.
        /// </summary>
        public static readonly float PaddleWidth = PaddleOn ? TheatreSkin.Dial("EVOSIM_THEATRE_PADDLE_WIDTH", 1f, 1f, 3f) : 1f;
        // WIDTH end
        // BLADE begin
        /// <summary>
        /// One paddle as the skin drew it, for the safari portrait <c>EVOSIM_THEATRE_PORTRAIT_FROM</c> <c>blade</c>
        /// (<c>SafariPlans.BladeFromPoint</c>). It is read off the <see cref="PaddleShape"/> itself, so the camera cannot disagree with
        /// what is drawn. <c>Normal</c> is the blade's thin axis (<c>PaddleShape.E2</c>: its face normal, square to the long axis and to
        /// the blade's width, which for a round capsule is the hinge axis), <c>Far</c> the long axis from the root toward the free end;
        /// both are unit vectors in the part's own frame. <c>Span</c> is the blade's straight half span and <c>Radius</c> its half
        /// width, in the skin's rest metres, so the free end's rounded cap runs from <c>Span</c> to <c>Span + Radius</c> along
        /// <c>Far</c> from the part's centre. <c>RestHalf</c> is the part's half extents in the same metres: the camera divides the
        /// drawn part's by its length for the scale the body is drawn at.
        /// </summary>
        public struct BladeFrame
        {
            public int Part;
            public Vector3 Normal, Far, RestHalf;
            public float Span, Radius;

            /// <summary>The blade's half length from the part's centre to its tip, rest metres.</summary>
            public float Half => Span + Radius;
        }

        /// <summary>The paddles each body's last skin drew, by body id. Written only while <see cref="BladeRecording"/>.</summary>
        private static readonly Dictionary<long, List<BladeFrame>> Blades = new Dictionary<long, List<BladeFrame>>();

        /// <summary>The paddles are recorded only when the portrait is placed by a blade, so no other run keeps a list.</summary>
        private static bool BladeRecording => PaddleOn && FilmPlans.PortraitFrom == "blade";

        /// <summary>One paddle made, noted with the part's half extents in rest metres.</summary>
        private static void BladeNote(long id, PaddleShape paddle, Vector3 restHalf)
        {
            if (!Blades.TryGetValue(id, out List<BladeFrame> list))
            {
                list = new List<BladeFrame>();
                Blades[id] = list;
            }

            list.Add(new BladeFrame
            {
                Part = paddle.Part, Normal = paddle.E2, Far = paddle.Far, RestHalf = restHalf,
                Span = paddle.Span, Radius = paddle.Radius,
            });
        }

        /// <summary>
        /// The longest paddle the body's last skin drew (the greatest <see cref="BladeFrame.Half"/>; the lower part on a tie), or
        /// false when the dial is off, the body was not skinned with one, or no link was reshaped.
        /// </summary>
        public static bool TryBlade(long id, out BladeFrame blade)
        {
            blade = default;
            if (!BladeRecording || !Blades.TryGetValue(id, out List<BladeFrame> list) || list.Count == 0) return false;

            blade = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Half > blade.Half) blade = list[i];
            }

            return true;
        }

        /// <summary>
        /// <c>EVOSIM_THEATRE_PADDLE_HINGE</c> (0 or 1, default 0), read once, with <see cref="PaddleOn"/> alone: a flat capsule's blade
        /// and a box's are laid square to the hinge they stroke about (<see cref="PaddleRelay"/>), as a round capsule's is. False when
        /// the paddle is off, and off no statement of it runs.
        /// </summary>
        public static readonly bool PaddleHinge = PaddleOn && TheatreSkin.Dial("EVOSIM_THEATRE_PADDLE_HINGE", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// Lays a made paddle on its hinge, in place, after <see cref="PaddleOf"/>; true when it changed it. The hinge is
        /// <see cref="FinHinge"/>'s axis standing most square to the long axis, under the same <see cref="FinWebTwistDegrees"/> gate a round
        /// capsule's blade uses, and h-perp is its component square to the long axis. A flat capsule (the palette squashes it across)
        /// is laid as a round capsule on its hinge: E1 = h-perp, E2 = Along x E1, the width the collider's radius r, the thickness
        /// wanted a third of the visible half width (<see cref="PaddleMinorShare"/> of r less the k/4 inset), never past the radius r,
        /// so the section is an ellipse inside the collider's round section and the end a squashed half disc, as for a round capsule
        /// (D242); it draws wider than the palette's squash on that axis. A box takes as E1 whichever of its two short axes lies nearer
        /// h-perp and the other as E2, the width and the thickness bound swapped with them (the width is that axis' half extent, the
        /// bound the other's), the straight span lengthened to keep the tip at the box's end, so the ellipse and its end stay inside the
        /// box; it is left as it is when E1 is already the nearer, when no hinge is found or it is within the gate of the long axis, or
        /// when the swap leaves no thickness. Nothing else of the paddle is touched.
        /// </summary>
        private static bool PaddleRelay(
            Phenotype adult, int index, bool[] merged, float unscale, Vector3 h, bool dressed, float k, PaddleShape paddle)
        {
            if (!FinHinge(adult, index, merged, unscale, out _, out Vector3[] axes, out _)) return false;

            Vector3 hinge = axes[0];
            float best = 2f;
            for (int a = 0; a < axes.Length; a++)
            {
                float dot = Mathf.Abs(Vector3.Dot(axes[a], paddle.Along));
                if (dot < best) { best = dot; hinge = axes[a]; }
            }

            if (!(best <= Mathf.Cos(FinWebTwistDegrees * Mathf.Deg2Rad))) return false;

            Vector3 square = hinge - Vector3.Dot(hinge, paddle.Along) * paddle.Along;
            if (!(square.sqrMagnitude > 1e-8f)) return false;

            square.Normalize();
            float quarter = 0.25f * k;

            if (paddle.Capsule)
            {
                float across = Mathf.Max(h.x, h.z);
                float sx = dressed && across > 0f ? h.x / across : 1f;
                float sz = dressed && across > 0f ? h.z / across : 1f;
                if (Mathf.Min(sx, sz) >= PaddleRoundShare) return false;

                paddle.E1 = square;
                paddle.E2 = Vector3.Cross(paddle.Along, paddle.E1).normalized;
                paddle.Wanted = PaddleMinorShare * (paddle.Radius - quarter);
                paddle.Limit = paddle.Radius;
            }
            else
            {
                if (Mathf.Abs(Vector3.Dot(square, paddle.E2)) <= Mathf.Abs(Vector3.Dot(square, paddle.E1))) return false;

                float half = paddle.Span + paddle.Radius;
                float width = paddle.Limit, bound = paddle.Radius;
                float wanted = Mathf.Min(PaddleMinorShare * (width - quarter), bound - quarter);
                if (!(wanted > 0f)) return false;

                Vector3 short1 = paddle.E1;
                paddle.E1 = paddle.E2;
                paddle.E2 = short1;
                paddle.Radius = width;
                paddle.Limit = bound;
                paddle.Span = half - width;
                paddle.Wanted = wanted;
                paddle.T1 = paddle.Span;
                paddle.T0 = -half + PaddleStartShare * (paddle.T1 + half);
            }

            paddle.Floor(0f, quarter);
            return true;
        }

        /// <summary>A blade whose thin axis stands at least this many degrees from the hinge is broadside to its stroke.</summary>
        private const float BladeBroadsideDegrees = 60f;

        /// <summary>A blade whose thin axis stands at most this many degrees from the hinge slices through its stroke edge-on to it.</summary>
        private const float BladeSlicingDegrees = 30f;

        /// <summary>
        /// The paddle probe (every paddle made while <see cref="PaddleOn"/>): the angle between the blade's thin axis E2 and the hinge
        /// it strokes about, as a line in the log. The hinge is the one <see cref="FinHinge"/> gives, the axis of it that stands most
        /// square to the long axis (the smallest dot with it, as <see cref="PaddleOf"/> picks for a round capsule), and the case names
        /// the branch of <see cref="PaddleOf"/> that laid the blade. E2 square to the hinge is broadside to the stroke (E2 along
        /// h x a); E2 along the hinge slices. Nothing is changed.
        /// </summary>
        private static void PaddleOrientationLog(
            long id, Phenotype adult, int index, bool[] merged, float unscale, Vector3 h, bool dressed, PaddleShape paddle, bool relaid)
        {
            bool found = FinHinge(adult, index, merged, unscale, out _, out Vector3[] axes, out _);
            Vector3 hinge = Vector3.zero;
            float best = 2f;

            if (found)
            {
                for (int a = 0; a < axes.Length; a++)
                {
                    float dot = Mathf.Abs(Vector3.Dot(axes[a], paddle.Along));
                    if (dot < best) { best = dot; hinge = axes[a]; }
                }
            }

            string kind;
            if (!paddle.Capsule) kind = "box";
            else
            {
                float across = Mathf.Max(h.x, h.z);
                float sx = dressed && across > 0f ? h.x / across : 1f;
                float sz = dressed && across > 0f ? h.z / across : 1f;
                var flat = new Vector3(hinge.x, 0f, hinge.z);

                if (Mathf.Min(sx, sz) < PaddleRoundShare) kind = "flat capsule";
                else if (!found) kind = "round capsule, no hinge";
                else if (best <= Mathf.Cos(FinWebTwistDegrees * Mathf.Deg2Rad) && flat.sqrMagnitude > 1e-8f) kind = "round capsule on its hinge";
                else kind = "round capsule, hinge tilted";
            }

            if (relaid) kind += ", on its hinge";

            if (!found)
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] paddle orientation: body {0}, part {1} ({2}): E2 to the hinge: no hinge", id, paddle.Part, kind));
                return;
            }

            float degrees = Mathf.Acos(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(paddle.E2, hinge.normalized)))) * Mathf.Rad2Deg;
            string verdict = degrees >= BladeBroadsideDegrees ? "broadside" : degrees <= BladeSlicingDegrees ? "slicing" : "oblique";
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] paddle orientation: body {0}, part {1} ({2}): E2 to the hinge {3:0} deg ({4})", id, paddle.Part, kind, degrees, verdict));
        }
        // BLADE end
        /// <summary>The default body: every type but the leaf and the holdfast.</summary>
        private static readonly string[] DefaultBody =
        {
            CellTypeIds.Structural, CellTypeIds.Absorptive, CellTypeIds.Buoyancy,
            CellTypeIds.Neural, CellTypeIds.Link, CellTypeIds.Consumer,
        };

        private static HashSet<string> _bodyTypes;

        /// <summary>The cell types merged into the skin, from <c>EVOSIM_THEATRE_SKIN_BODY</c> (comma separated).</summary>
        public static HashSet<string> BodyTypes
        {
            get
            {
                if (_bodyTypes != null) return _bodyTypes;

                var types = new HashSet<string>(StringComparer.Ordinal);
                string text = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SKIN_BODY");

                if (!string.IsNullOrWhiteSpace(text))
                {
                    foreach (string item in text.Split(','))
                    {
                        string id = item.Trim();
                        if (id.Length > 0) types.Add(id);
                    }
                }

                if (types.Count == 0)
                {
                    foreach (string id in DefaultBody) types.Add(id);
                }

                _bodyTypes = types;
                return _bodyTypes;
            }
        }

        // ---------------------------------------------------------------- the numbers

        /// <summary>k is this share of the median of the merged parts' smallest half extents...</summary>
        private const float KShare = 0.5f;

        /// <summary>...and never more than this, in metres.</summary>
        private const float KMaximum = 0.03f;

        /// <summary>The grid's cell is at least this share of the smallest half extent...</summary>
        private const float CellOfSmallest = 0.25f;

        /// <summary>...and at least the body's largest extent over this.</summary>
        private const float CellsAcrossBody = 64f;

        /// <summary>
        /// A merged part whose smallest half extent is under this many cells is left out of the skin and drawn as its
        /// own part: the marching grid cannot keep a sheet thinner than that, and the smooth union loses it.
        /// </summary>
        private const float ThinPartCells = 1.5f;

        /// <summary>Cells of padding round the body's bounds, so the grid's rim is outside.</summary>
        private const int PadCells = 2;

        private const float TaubinLambda = 0.5f;
        private const float TaubinMu = -0.53f;
        private const int TaubinPasses = 2;

        /// <summary>The newton steps that pull a vertex left outside back to the surface.</summary>
        private const int PullSteps = 8;

        /// <summary>Bones a vertex is weighted to.</summary>
        private const int MaxInfluences = 4;

        /// <summary>The shape channel's flag for a skin (TheatreBody.shader's ShapeBody: x above 2.5).</summary>
        private const float SkinFlag = 3f;

        /// <summary>The thickness march's step, as a share of the grid's cell.</summary>
        private const float ThicknessStep = 0.25f;

        /// <summary>
        /// A part's painted colour has to move by more than this share of its largest channel (plus a floor)
        /// before the vertex colours are blended again, so a reserve's slow drift does not upload the colour
        /// stream on every repaint. Linear, so 0.5 % of a colour is under a tenth of a step of 255 on screen.
        /// </summary>
        private const float ColourTolerance = 0.005f;

        private const float ColourFloor = 1e-5f;

        /// <summary>
        /// A skinning weight under this is dropped and the rest renormalised, so that no bone carries the whole mesh
        /// through a sliver of weight (the renderer's own bounds are a union over the bones that carry any), and
        /// the skin moves by under a part in a thousand.
        /// </summary>
        private const float MinWeight = 1e-3f;

        /// <summary>
        /// Level 3's carve length is this many of the median of the parts' smallest half extents at rest (rest metres);
        /// the carve field is read at the rest position over it. A part reads its carve in its own object units, one of
        /// which is its full extent, twice its half extent, so two here gives the skin's lobes a part's size.
        /// </summary>
        private const float CarveLengthShare = 2f;
        // RIG4 begin
        /// <summary>Level 4: the joint's ball radius and its pair blend radius, as this share of the thinner part's smallest half extent.</summary>
        private const float RigJointShare = 0.7f;

        /// <summary>
        /// Level 4: the weights' blend scale across a joint, as this share of the joint's radius. The two bones' weights
        /// follow exp(-d/scale), so the bend runs over about four times this (the weights go from 0.12 to 0.88 over it).
        /// </summary>
        private const float RigWeightShare = 0.5f;

        /// <summary>Level 4: a vertex is fully in a joint's blend within this many joint radii of its anchor...</summary>
        private const float RigZoneInner = 1.5f;

        /// <summary>...and not at all beyond this many (a smoothstep between), so the old weights hold away from joints.</summary>
        private const float RigZoneOuter = 3f;
        // RIG4 end
        // RIG5 begin
        /// <summary>
        /// Level 5: a part is its collider on the smaller of a joint's two faces (a box's face rectangle; a round part's point
        /// or line of contact), and eases back by a smoothstep to the shape it is drawn as over this many of the thicker part's
        /// smallest half extent from that face. The fillet fades out over the same ramp.
        /// </summary>
        private const float Rig5RampShare = 1f;

        /// <summary>Level 5: the fillet's smooth-minimum radius, as this share of the thicker part's smallest half extent...</summary>
        private const float Rig5FilletShare = 0.4f;

        /// <summary>...and at least this many k, so that it closes the two insets (k/4 each) between faces that meet.</summary>
        private const float Rig5FilletFloor = 2f;

        /// <summary>
        /// Level 5: the skin's carve cuts at most this many grid cells deep (rest metres), so that a cut taken as a share of the
        /// vertex's own half thickness cannot fold the mesh where that thickness is large or jumps between neighbours.
        /// </summary>
        private const float Rig5CarveCells = 0.5f;
        // RIG5 end
        // RIG6 begin
        /// <summary>
        /// Level 6: the sleeve's radius, as this share of the smaller part's section at the joint (the smaller of a box face's two
        /// half sizes, or a round part's smallest half extent)...
        /// </summary>
        private const float Rig6SleeveShare = 1f;

        /// <summary>...and at most this share of the larger part's smallest half extent, so the sleeve stays inside its drawn middle.</summary>
        private const float Rig6SleeveCap = 0.5f;

        /// <summary>The sleeve runs this share of the larger part's half extent along the face normal into it (1: to its centre plane).</summary>
        private const float Rig6SleeveDepth = 1f;

        /// <summary>The sleeve joins the larger part by a smooth minimum of this share of its radius, held inside the larger collider.</summary>
        private const float Rig6SleeveBlend = 1f;

        /// <summary>
        /// A joint is waisted when the larger part as drawn leaves out any of eight points a sleeve radius under its face, at this
        /// share of the sleeve radius from the sleeve's axis.
        /// </summary>
        private const float Rig6WaistProbe = 0.9f;

        /// <summary>The bend from rest, in degrees, where the per-frame correction starts...</summary>
        private const float Rig6BendLow = 10f;

        /// <summary>...and where it is whole (a smoothstep between), so a straight or gently bent hinge is level 5's.</summary>
        private const float Rig6BendHigh = 30f;

        /// <summary>
        /// A vertex is in a hinge's zone within this share of the larger of the joint's weight zone (<see cref="RigZoneOuter"/> joint
        /// radii) and the ramp's reach, from the anchor.
        /// </summary>
        private const float Rig6ZoneShare = 1f;

        /// <summary>Newton steps per vertex per frame.</summary>
        private const int Rig6Steps = 3;

        /// <summary>A vertex within this many grid cells of the posed surface (rest metres) is left where the skinning puts it.</summary>
        private const float Rig6Tolerance = 0.05f;

        /// <summary>The posed field's gradient is taken by central differences of this many grid cells.</summary>
        private const float Rig6GradientCells = 0.25f;

        /// <summary>A vertex stops where two steps' gradients turn by more than this many degrees: it has reached a crease.</summary>
        private const float Rig6StopDegrees = 55f;

        /// <summary>A vertex moves at most this many of its hinge's fillet radius from its rest place (rest metres); the bounds grow by the most.</summary>
        private const float Rig6MaxPush = 1.5f;

        /// <summary>The bend log prints a hinge again each time its bend passes another step of this many degrees.</summary>
        private const float Rig6LogStep = 15f;
        // RIG6 end

        // ---------------------------------------------------------------- one skin

        private readonly Transform _root;
        private readonly SkinnedMeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly Transform[] _bones;
        private readonly int[] _mergedParts;
        private readonly bool[] _merged;
        private readonly int[] _parentOf;
        private readonly Renderer[] _hidden;
        private readonly Transform[] _sourceVisuals;
        private readonly BoneWeight[] _weights;
        private readonly Color[] _colours;
        private readonly Vector4[] _partColours;
        private readonly Vector4[] _nextColours;
        private bool _coloured;
        private float _scaleWritten = -1f;

        // Posed (the renderer's bounds): for each bone, the bind-space box of the vertices it dominates.
        private Vector3[] _boxLow;
        private Vector3[] _boxHigh;
        private bool[] _boxed;

        // Level 3 (Carved): the carve length in rest metres, which merged parts are a soft-tissue box or drawn as a
        // leaf, each part's last blended character (lobe gain, wrinkle gain, depth share, soft leaf), and the
        // per-vertex stream the shader reads (UV7: wrinkle share, depth share, tissue weight, soft-leaf weight), and each
        // vertex's concavity in [0, 1] (Concavities), which eases the depth share toward none in the creases.
        private float _carveLength = 1f;
        private bool[] _tissueBox;
        private bool[] _leafShaped;
        private Vector4[] _character;
        private Vector4[] _nextCharacter;
        private Vector4[] _carveUVs;
        private float[] _concavity;
        private bool _carved;
        // RIG5 begin
        // Level 5: per vertex, the share of the carve's depth kept (the fill and the joint ramp) and the most depth share the cap
        // allows (Rig5CarveCells), applied to UV7's depth share at each blend (BlendCarve). Null below level 5.
        private Vector2[] _carve5;
        // RIG5 end
        // RIG6 begin
        // Level 6, per frame (Bend6): the field, the rest positions and normals and the working copies written to the mesh, the
        // bindposes and the rest rotations, each bone's skinning matrix now, last time and inverted, the zone's vertices and each
        // one's hinge, each hinge's two bones, push cap, eased share, bend and last logged step, and whether the mesh holds a
        // correction. Null below level 6, and for a body with no hinge.
        private Field _field6;
        private Vector3[] _rest6, _restNormals6, _work6, _workNormals6;
        private Matrix4x4[] _bind6, _skin6, _last6, _toRest6;
        private Quaternion[] _restRot6;
        private int[] _zone6, _zoneHinge6, _hingeParent6, _hingeChild6;
        private float[] _hingePush6, _hingeShare6, _hingeBend6, _hingeLogged6;
        private float _cell6;
        private long _id6;
        private bool _bent6, _have6;
        // BEYOND begin
        // The BEYOND test: the widest the tissue has gone beyond the bound (rest metres), the widest last logged, and the largest
        // round end's radius at a beyond hinge, for the log.
        private float _beyondMost6, _beyondLogged6, _beyondR6;
        // BEYOND end
        // RIG6 end

        // mouth2:begin
        // MOUTH2: each eating merged part's mouth, up to four, in rest metres (xyz) and its radius there (w); built with the mesh.
        private Vector4[] _mouthRest;
        // MOUTH2_DISC: the same mouths' disc axes (xyz) and stripe phases (w); null with the dial off.
        private Vector4[] _mouthAxes;
        // MOUTH2_BITE: the part each of the four slots belongs to (-1 where none), the skin's four bite timers as TheatreMouthBite last set them,
        // and whether it has; all unused with the dial off.
        private int[] _mouthParts;
        private Vector4 _mouthBite;
        private bool _mouthBiteSet;
        // MOUTH2_GAPE: the skin's four gape shares as TheatreMouthBite last set them, and whether it has; unused with the dial off.
        private Vector4 _mouthGape;
        private bool _mouthGapeSet;
        // DEBUGTINT begin
        /// <summary>
        /// EVOSIM_THEATRE_DEBUG_TINT: a skin vertex whose largest bone weight is under this is drawn as a joint's blend. Three quarters,
        /// so a vertex a quarter or more on another bone, the rig's blend across a joint, shows.
        /// </summary>
        private const float DebugBlendShare = 0.75f;

        /// <summary>A skin vertex within this many mouth radii of a skin mouth is drawn as the mouth's zone: the shader's MouthReach.</summary>
        private const float DebugMouthReach = 1.35f;

        /// <summary>The skin's debug vertex colours, made at the first dress; null with the dial off.</summary>
        private Color[] _debugColours;
        // DEBUGTINT end
        // mouth2:end

        // noseskin:begin
        // NOSE_PIT 2: the merged nose's pit, the sensing tip in rest metres (xyz) and its radius there (w), and its depth in rest metres;
        // zero where the skin has no nose. Built with the mesh.
        private Vector4 _noseRest;
        private float _noseDepth;
        // noseskin:end

        // ORGANSEAT begin
        // ORGAN_SEAT: by part index, the parts given a cone seat on their parent's drawn box, or null.
        private bool[] _organSeated;
        // ORGANSEAT end
        // SEATPROBE begin
        // The nose seat probe: a row per part standing apart from the skin with its parent in it, measured at the build and
        // logged once, at the first dress.
        private List<SeatRow> _seatRows;
        private bool _seatRowsSaid;
        // SEATPROBE end
        private readonly List<Transform> _necks = new List<Transform>();
        private readonly List<int> _neckJoints = new List<int>();
        private MaterialPropertyBlock _block;

        private static readonly int PinchAId = Shader.PropertyToID("_PinchA");
        private static readonly int FamilyBoxId = Shader.PropertyToID("_FamilyBox");
        private static readonly int FamilyExtentId = Shader.PropertyToID("_FamilyExtent");
        private static readonly int FamilySpotId = Shader.PropertyToID("_FamilySpot");
        private static readonly int PinchBId = Shader.PropertyToID("_PinchB");
        private static readonly int LeafId = Shader.PropertyToID("_Leaf");
        private static readonly int RampAxisId = Shader.PropertyToID("_RampAxis");
        private static readonly int VelvetId = Shader.PropertyToID("_Velvet");
        private static readonly int VelvetOsculumId = Shader.PropertyToID("_VelvetOsculum");
        private static readonly int LinkAxisId = Shader.PropertyToID("_LinkAxis");
        private static readonly int LinkShapeId = Shader.PropertyToID("_LinkShape");
        private static readonly int SwayPartId = Shader.PropertyToID("_SwayPart");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SkinScaleId = Shader.PropertyToID("_SkinScale");
        private static readonly int SkinCarveId = Shader.PropertyToID("_SkinCarve");
        private static readonly int SkinCarveLengthId = Shader.PropertyToID("_SkinCarveLength");
        private static readonly int CarveId = Shader.PropertyToID("_Carve");
        private static readonly int LeafLookId = Shader.PropertyToID("_LeafLook");
        private static readonly int UptakeId = Shader.PropertyToID("_Uptake"); // uptake-skin

        /// <summary>The keyword the body shader compiles the skin's branches under; no part's material ever has it.</summary>
        private const string SkinKeyword = "_ONE_SKIN";

        // PATTERNAA begin
        /// <summary>
        /// <c>EVOSIM_THEATRE_PATTERN_AA</c> (0, 1 or 2, default 0), read once: the skin's pattern (<c>PatternSkin2</c>) fades and
        /// picks its mip level by another measure of the pixel's footprint than the long axis it reads today, so a limb's shaft
        /// seen foreshortened along its length draws its maze. 1 is the area measure, the geometric mean of the footprint's two
        /// axes; 2 is the minor axis held no smaller than the major over 8. Shader terms only, under the local keyword
        /// <see cref="PatternAaKeyword"/>, which <see cref="KeywordedOf"/> turns on in the skin's material copy while this is above
        /// 0, with the dial's value in the global <c>_EvoPatternAa</c>; nothing moves, and a part is as it was.
        /// </summary>
        public static readonly int PatternAa = Mathf.RoundToInt(TheatreSkin.Dial("EVOSIM_THEATRE_PATTERN_AA", 0f, 0f, 2f));

        /// <summary>The body shader's keyword for <see cref="PatternAa"/>, local to the skin's material copy.</summary>
        private const string PatternAaKeyword = "_EVO_PATTERN_AA";

        private static readonly int PatternAaId = Shader.PropertyToID("_EvoPatternAa");

        // PATTERNAA end
        /// <summary>
        /// One keyworded copy per source material, shared by every skin. The body shader compiles the skin's own terms
        /// (the sheen's tighter lobe, level 3's carve) only under <c>_ONE_SKIN</c>, so that a part's code is the code from
        /// before the skin; a skin draws with a copy of the parts' material that has it on, and the parts keep theirs.
        /// Shared, not one per skin, because the palette makes a fading pair of materials once per material it is handed,
        /// and a copy per skin would leave two more behind for every body that ever faded.
        /// </summary>
        private static readonly Dictionary<Material, Material> KeywordedCopies = new Dictionary<Material, Material>();

        /// <summary>
        /// The skin's material for a part's: a copy with <c>_ONE_SKIN</c> on, made once and brought up to the source's
        /// properties on every call (the theatre sets some on the shared material in place, such as the raw-shapes
        /// toggle's three outline fractions). The source itself is never touched.
        /// </summary>
        private static Material KeywordedOf(Material source)
        {
            if (source == null) return null;

            if (!KeywordedCopies.TryGetValue(source, out Material copy) || copy == null)
            {
                copy = new Material(source) { name = source.name + " (one skin)", hideFlags = HideFlags.DontSave };
                KeywordedCopies[source] = copy;
            }
            else
            {
                copy.CopyPropertiesFromMaterial(source);
            }

            copy.EnableKeyword(SkinKeyword);
            // PATTERNAA begin
            if (PatternAa > 0)
            {
                copy.EnableKeyword(PatternAaKeyword);
                Shader.SetGlobalFloat(PatternAaId, PatternAa);
            }
            // PATTERNAA end
            // JOIN begin
            if (SkinJoin) copy.EnableKeyword(JoinKeyword);
            // JOIN end
            // JOINDEBUG begin
            if (JoinDebug > 0)
            {
                copy.EnableKeyword(JoinDebugKeyword);
                Shader.SetGlobalFloat(JoinDebugId, JoinDebug);
            }
            // JOINDEBUG end
            return copy;
        }

        /// <summary>Parts merged into the skin.</summary>
        public int MergedCount => _mergedParts.Length;

        /// <summary>
        /// MOUTH2_BITE: which of <c>_MouthSkin0</c> to <c>_MouthSkin3</c> holds part <paramref name="part"/>'s mouth, or -1 where the
        /// part has none on this skin. Only a skin built with the dial on knows.
        /// </summary>
        public int MouthSlotOfPart(int part)
        {
            if (_mouthParts == null) return -1;

            for (int k = 0; k < _mouthParts.Length; k++)
            {
                if (_mouthParts[k] == part) return k;
            }

            return -1;
        }

        /// <summary>
        /// MOUTH2_BITE: the skin's four bite timers (<c>_MouthBite</c>, in slot order), kept for every dress and written to the skin's
        /// renderer now. Called by <c>TheatreMouthBite</c> only with the dial on.
        /// </summary>
        public void SetMouthBite(Vector4 timers)
        {
            _mouthBite = timers;
            _mouthBiteSet = true;

            if (_renderer == null) return;

            if (_block == null) _block = new MaterialPropertyBlock();

            _renderer.GetPropertyBlock(_block);
            _block.SetVector(TheatreMouth2.BiteId, timers);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// MOUTH2_GAPE: the skin's four gape shares (<c>_MouthBiteGape</c>, in slot order), kept for every dress and written to the skin's
        /// renderer now. Called by <c>TheatreMouthBite</c> only with the gape dial on.
        /// </summary>
        public void SetMouthGape(Vector4 shares)
        {
            _mouthGape = shares;
            _mouthGapeSet = true;

            if (_renderer == null) return;

            if (_block == null) _block = new MaterialPropertyBlock();

            _renderer.GetPropertyBlock(_block);
            _block.SetVector(TheatreMouth2.GapeId, shares);
            _renderer.SetPropertyBlock(_block);
        }

        private TheatreOneSkin(
            Transform root, SkinnedMeshRenderer renderer, Mesh mesh, Transform[] bones,
            int[] mergedParts, bool[] merged, int[] parentOf, Renderer[] hidden, Transform[] sourceVisuals,
            BoneWeight[] weights, Color[] colours)
        {
            _root = root;
            _renderer = renderer;
            _mesh = mesh;
            _bones = bones;
            _mergedParts = mergedParts;
            _merged = merged;
            _parentOf = parentOf;
            _hidden = hidden;
            _sourceVisuals = sourceVisuals;
            _weights = weights;
            _colours = colours;
            _partColours = new Vector4[mergedParts.Length];
            _nextColours = new Vector4[mergedParts.Length];
        }

        /// <summary>
        /// The two global keywords <see cref="Build"/> switches on before it does anything else, switched on by a body whose skin is
        /// left pending (EVOSIM_THEATRE_DRESS_NEAR) at the moment its <see cref="Build"/> would have: both are the same whichever
        /// body asks, and both are written once. Nothing else a build does is global.
        /// </summary>
        public static void EnsureBuildKeywords()
        {
            if (TheatreSkin.Skin2On) TheatreSkin.EnsureSkin2Keyword(); // skin2
            if (TheatreSkin.SheenCapBaseOn) TheatreSkin.EnsureSheenCapBaseKeyword();
        }

        /// <summary>
        /// The skin for one body, or null: the dial off, fewer than two merged parts, or no surface.
        /// </summary>
        /// <param name="parts">The body's part transforms, unscaled, in part order (<c>LiveWorldView.Build</c>).</param>
        /// <param name="visuals">The visuals hanging off each part.</param>
        /// <param name="current">The phenotype the body is drawn at now: the adult, or one <c>Scaled</c> copy of it.</param>
        /// <param name="source">The view's key for the body; an adult phenotype when the view is fed.</param>
        /// <param name="material">The material a part is born with, until the palette's first paint.</param>
        /// <param name="palette">
        /// The palette that will paint the body, for the shapes it draws the parts as (its skin's taper and
        /// bend, its raw shapes, the organic dial and the parts' seeds). Null: every part keeps its collider.
        /// </param>
        public static TheatreOneSkin Build(
            long id, Transform root, Transform[] parts, Transform[][] visuals, Phenotype current, object source,
            PartShapeRegistry shapes, Material material, TheatrePalette palette = null)
        {
            if (!Enabled || root == null || parts == null || current == null) return null;
            if (parts.Length != current.PartCount) return null;

            Stopwatch clock = Stopwatch.StartNew();
            if (TheatreSkin.Skin2On) TheatreSkin.EnsureSkin2Keyword(); // skin2
            // SHEEN2 begin
            if (TheatreSkin.SheenCapBaseOn) TheatreSkin.EnsureSheenCapBaseKeyword();
            // SHEEN2 end

            // The adult: the fed view's key when it is one, else the body now, divided by the one
            // scale it was made at (Phenotype.ScaledBy: the adult or exactly one Scaled copy).
            Phenotype adult = current;
            float unscale = current.ScaledBy > 0f ? 1f / current.ScaledBy : 1f;

            if (source is Phenotype keyed && keyed.PartCount == current.PartCount && keyed.ScaledBy == 1f)
            {
                adult = keyed;
                unscale = 1f;
            }

            HashSet<string> body = BodyTypes;
            int count = adult.PartCount;
            var merged = new bool[count];
            var mergedParts = new List<int>();
            // NOBOX begin
            // EVOSIM_THEATRE_NO_BOX: a body of one merged part is a body. The rule that a skin joins at least two parts is the
            // only thing this changes; every other refusal stands. Off, two, as it was.
            int minMerged = TheatreNoBox.Enabled ? 1 : 2;
            // BOXLOG begin
            // EVOSIM_THEATRE_BOX_LOG: why a part or the body was left out, written only while the log is on.
            TheatreBoxLog.SkinNote note = TheatreBoxLog.On ? TheatreBoxLog.Begin(id, count) : null;
            // BOXLOG end
            // NOBOX end
            // WHOLE begin

            // An animal is a body with a link part; a body with none (a plant) keeps the rule it has without the dial. The records are
            // per part: the producers the dial brought in (wholeBrought), the thin parts it brought back (wholeRestored), and the count
            // of parts the cap on the grid left out.
            bool wholeAnimal = false;
            bool[] wholeBrought = null, wholeRestored = null;
            int wholeCapped = 0;

            if (WholeOn)
            {
                for (int wholeA = 0; wholeA < count; wholeA++)
                {
                    if (adult.Parts[wholeA].CellTypeId == CellTypeIds.Link) { wholeAnimal = true; break; }
                }

                if (wholeAnimal)
                {
                    wholeBrought = new bool[count];
                    wholeRestored = new bool[count];
                }
            }
            // WHOLE end

            for (int i = 0; i < count; i++)
            {
                if (parts[i] == null) continue;
                // RIG4 begin
                if (Rigged)
                {
                    // WHOLE begin
                    if (wholeAnimal)
                    {
                        Vector3 wholeHalf = Abs(adult.Parts[i].HalfExtents.ToVector3()) * unscale;
                        if (!RigMerges(adult.Parts[i], wholeHalf, body, true)) continue;

                        merged[i] = true;
                        mergedParts.Add(i);
                        wholeBrought[i] = !RigMerges(adult.Parts[i], wholeHalf, body);
                        continue;
                    }
                    // WHOLE end
                    if (!RigMerges(adult.Parts[i], Abs(adult.Parts[i].HalfExtents.ToVector3()) * unscale, body)) continue;

                    merged[i] = true;
                    mergedParts.Add(i);
                    continue;
                }
                // RIG4 end
                if (!body.Contains(adult.Parts[i].CellTypeId)) continue;

                merged[i] = true;
                mergedParts.Add(i);
            }
            // BOXLOG begin
            if (note != null)
            {
                for (int noteA = 0; noteA < count; noteA++)
                {
                    if (!merged[noteA]) note.Part[noteA] = NotMergedReason(adult.Parts[noteA], parts[noteA] == null, body);
                }
            }
            // BOXLOG end

            if (mergedParts.Count < minMerged)
            {
                // BOXLOG begin
                if (note != null) note.Body = NotEnoughReason(mergedParts);
                // BOXLOG end
                return null;
            }

            // Thin parts, left out: the grid's cell is the larger of a quarter of the smallest half extent and the body's
            // extent over 64, so a part whose smallest half extent is under ThinPartCells of them is lost by the mesher.
            // The cell is estimated here from the candidates' oriented boxes, as the grid will take it from the field's
            // bounds, and the estimate is taken again after each part is left out (a part is only ever left out, never
            // let back in). A left-out part keeps its renderer, its neck and its own material, as a specialised part does.
            int thinLeftOut = 0;
            // WHOLE begin

            int[] wholeCandidates = wholeAnimal ? mergedParts.ToArray() : null;
            // WHOLE end

            for (int pass = 0; pass < 4 && mergedParts.Count >= minMerged; pass++)
            {
                Vector3 estLow = Vector3.one * float.PositiveInfinity;
                Vector3 estHigh = Vector3.one * float.NegativeInfinity;
                float estSmallest = float.PositiveInfinity;
                var estSmall = new float[mergedParts.Count];

                for (int j = 0; j < mergedParts.Count; j++)
                {
                    PhenotypePart part = adult.Parts[mergedParts[j]];
                    Vector3 h = Abs(part.HalfExtents.ToVector3()) * unscale;
                    Vector3 c = part.Position.ToVector3() * unscale;
                    Quaternion q = Normalised(part.Rotation.ToQuaternion());
                    estSmall[j] = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
                    estSmallest = Mathf.Min(estSmallest, estSmall[j]);

                    for (int corner = 0; corner < 8; corner++)
                    {
                        var local = new Vector3(
                            (corner & 1) == 0 ? -h.x : h.x, (corner & 2) == 0 ? -h.y : h.y, (corner & 4) == 0 ? -h.z : h.z);
                        Vector3 at = c + q * local;
                        estLow = Vector3.Min(estLow, at);
                        estHigh = Vector3.Max(estHigh, at);
                    }
                }

                Vector3 estSize = estHigh - estLow;
                float estLargest = Mathf.Max(estSize.x, Mathf.Max(estSize.y, estSize.z));
                float estCell = Mathf.Max(CellOfSmallest * estSmallest, estLargest / CellsAcrossBody);
                bool left = false;

                for (int j = mergedParts.Count - 1; j >= 0; j--)
                {
                    if (!(estSmall[j] < ThinPartCells * estCell)) continue;

                    // BOXLOG begin
                    if (note != null) note.Part[mergedParts[j]] = ThinReason(estSmall[j], estCell, false);
                    // BOXLOG end
                    merged[mergedParts[j]] = false;
                    mergedParts.RemoveAt(j);
                    thinLeftOut++;
                    left = true;
                }

                if (!left) break;
            }
            // WHOLE begin

            // On an animal a thin part is kept, not left out: every part the pass above dropped comes back but the organs (a nose under
            // 1.5 cells stands apart as it does without the dial; the holdfast is never in the mesh). The grid is refined below to hold
            // them, to at most WholeMaxCells cells an axis. A part that cap would still lose is left out here, with the cell estimated
            // as the pass above estimates it, from the candidates' oriented boxes, the longest extent a margin over, so that the estimate
            // errs toward leaving a part out, and again after each part is left out.
            if (wholeAnimal)
            {
                bool wholeBack = false;

                for (int wholeW = 0; wholeW < wholeCandidates.Length; wholeW++)
                {
                    int wholeIndex = wholeCandidates[wholeW];
                    string wholeType = adult.Parts[wholeIndex].CellTypeId;
                    if (merged[wholeIndex] || wholeType == CellTypeIds.Nose || wholeType == CellTypeIds.Anchoring) continue;

                    merged[wholeIndex] = true;
                    mergedParts.Add(wholeIndex);
                    // BOXLOG begin
                    if (note != null) note.Part[wholeIndex] = null;
                    // BOXLOG end
                    wholeRestored[wholeIndex] = true;
                    thinLeftOut--;
                    wholeBack = true;
                }

                if (wholeBack) mergedParts.Sort();

                for (int wholePass = 0; wholePass < 4 && mergedParts.Count >= minMerged; wholePass++)
                {
                    Vector3 wholeLow = Vector3.one * float.PositiveInfinity;
                    Vector3 wholeHigh = Vector3.one * float.NegativeInfinity;
                    float wholeSmallest = float.PositiveInfinity;
                    var wholeSmall = new float[mergedParts.Count];

                    for (int j = 0; j < mergedParts.Count; j++)
                    {
                        PhenotypePart part = adult.Parts[mergedParts[j]];
                        Vector3 h = Abs(part.HalfExtents.ToVector3()) * unscale;
                        Vector3 c = part.Position.ToVector3() * unscale;
                        Quaternion q = Normalised(part.Rotation.ToQuaternion());
                        wholeSmall[j] = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
                        wholeSmallest = Mathf.Min(wholeSmallest, wholeSmall[j]);

                        for (int corner = 0; corner < 8; corner++)
                        {
                            var local = new Vector3(
                                (corner & 1) == 0 ? -h.x : h.x, (corner & 2) == 0 ? -h.y : h.y, (corner & 4) == 0 ? -h.z : h.z);
                            Vector3 at = c + q * local;
                            wholeLow = Vector3.Min(wholeLow, at);
                            wholeHigh = Vector3.Max(wholeHigh, at);
                        }
                    }

                    Vector3 wholeSize = wholeHigh - wholeLow;
                    float wholeLargest = Mathf.Max(wholeSize.x, Mathf.Max(wholeSize.y, wholeSize.z));
                    float wholeCell = WholeCell(wholeSmallest, wholeLargest * WholeBoundsMargin);
                    bool wholeLeft = false;

                    for (int j = mergedParts.Count - 1; j >= 0; j--)
                    {
                        if (!(wholeSmall[j] < ThinPartCells * wholeCell * WholeSlack)) continue;

                        int wholeOut = mergedParts[j];
                        // BOXLOG begin
                        if (note != null) note.Part[wholeOut] = ThinReason(wholeSmall[j], wholeCell, true);
                        // BOXLOG end
                        merged[wholeOut] = false;
                        wholeRestored[wholeOut] = false;
                        mergedParts.RemoveAt(j);
                        thinLeftOut++;
                        wholeCapped++;
                        wholeLeft = true;
                    }

                    if (!wholeLeft) break;
                }
            }
            // WHOLE end

            if (mergedParts.Count < minMerged)
            {
                // BOXLOG begin
                if (note != null) note.Body = NotEnoughReason(mergedParts);
                // BOXLOG end
                return null;
            }

            shapes = shapes ?? PartShapeRegistry.Standard;

            // What the palette will draw the parts as. Its skin dresses a body only when it is ready, and
            // swaps the rounded cube in, squashes a capsule and a sphere and shapes the box only then; the
            // raw shapes draw the colliders with no taper, bend or organic silhouette (TheatreSkin.ShowRawShapes).
            bool dressed = palette != null && palette.Skin != null && palette.Skin.Ready;
            bool shaped = dressed && !palette.RawShapes;

            var look = new DrawnLook
            {
                Taper = shaped ? Mathf.Clamp(palette.Skin.TaperFraction, 0f, 0.8f) : 0f,
                Bend = shaped ? Mathf.Clamp(palette.Skin.BendFraction, 0f, 0.4f) : 0f,
                Organic = shaped && TheatrePalette.OrganicSilhouetteOn,
                Tissue = shaped && TheatrePalette.OrganicTissueOn,
            };
            // RIG5 begin
            if (SkinFill > 0f) look.Fill5 = SkinFill;
            // RIG5 end

            // ---- the primitives, in the adult's rest frame

            int n = mergedParts.Count;
            var smallest = new float[n];
            var centres = new Vector3[n];
            var rotations = new Quaternion[n];
            var tissueBox = new bool[n];
            var leafShaped = new bool[n];

            for (int j = 0; j < n; j++)
            {
                PhenotypePart part = adult.Parts[mergedParts[j]];
                Vector3 h = Abs(part.HalfExtents.ToVector3()) * unscale;
                smallest[j] = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
                centres[j] = part.Position.ToVector3() * unscale;
                rotations[j] = Normalised(part.Rotation.ToQuaternion());
            }

            float median = Median(smallest);
            float k = Mathf.Min(KShare * median, KMaximum);
            if (!(k > 1e-5f))
            {
                // BOXLOG begin
                if (note != null) note.Body = "no surface: the smoothing length k is " + k.ToString("0.000000", CultureInfo.InvariantCulture) + " m";
                // BOXLOG end
                return null;
            }

            var field = new Field(k);
            float minSmallest = float.PositiveInfinity;
            int drawnBoxes = 0;
            // SEAT begin
            List<string> seatProbe = SeatProbe ? new List<string>() : null;
            // SEAT end
            // ORGANSEAT begin
            bool[] organSeated = OrganSeat ? new bool[count] : null;
            // ORGANSEAT end
            // SEATPROBE begin
            var seatBoxes = new DrawnBox[n];
            // SEATPROBE end
            // PADDLE begin
            PaddleShape[] paddles = PaddleOn ? new PaddleShape[n] : null;
            List<string> paddleNotes = PaddleOn ? new List<string>() : null;
            float paddleCellToday = 0f;
            // PADDLE end
            // BLADE begin
            if (BladeRecording) Blades.Remove(id);
            // BLADE end
            // NOBOXJOIN begin
            bool[] noBoxDrawn = TheatreNoBox.JoinOn ? new bool[n] : null;
            // NOBOXJOIN end

            for (int j = 0; j < n; j++)
            {
                int index = mergedParts[j];
                PhenotypePart part = adult.Parts[index];
                Vector3 h = Abs(part.HalfExtents.ToVector3()) * unscale;
                minSmallest = Mathf.Min(minSmallest, smallest[j]);

                // The seeds the palette paints this part with (TheatrePalette.Paint, from the phenotype it
                // is first painted with: the body now), for the taper's end, the bend and the silhouette.
                look.Seed = TheatrePalette.CarveSeedOf(current, id, index);
                look.Individual = TheatrePalette.IndividualOf(id, index);

                Primitive primitive = PrimitiveOf(
                    shapes.Resolve(part.ShapeId), part, h, centres[j], rotations[j], dressed, shaped, look,
                    out bool drawnBox,
                    TheatreNoBox.Enabled && (TheatreNoBox.EllipsoidAll || mergedParts.Count == 1));   // NOBOX
                // NOBOXJOIN begin
                // A box the no-box rule drew as its ellipsoid: the ellipsoid kinds of a part that is neither a sphere nor a capsule
                // (those two keep their own shapes, and a family part is its own kind).
                if (noBoxDrawn != null)
                {
                    PartShape joinShape = shapes.Resolve(part.ShapeId);

                    noBoxDrawn[j] = (primitive.Kind == KindEllipsoid || primitive.Kind == KindSuperEllipsoid) &&
                                    !(joinShape is SphereShape) && !(joinShape is CapsuleShape);
                }
                // NOBOXJOIN end
                // SEAT begin
                if (SeatDial > 0f && drawnBox) primitive.Drawn.SetSeats(SeatsOf(adult, index, unscale, SeatDial));
                if (SeatProbe && drawnBox) SeatProbeBox(seatProbe, id, adult, index, unscale, primitive.Drawn);
                // SEAT end
                // ORGANSEAT begin
                if ((SeatMarkerOff || OrganSeat) && drawnBox)
                    primitive.Drawn.SetSeats(OrganSeatsOf(adult, index, unscale, merged, primitive.Drawn, organSeated));
                // ORGANSEAT end
                // SEATPROBE begin
                if (drawnBox) seatBoxes[j] = primitive.Drawn;
                // SEATPROBE end
                // RIG5 begin
                if (Filleted) ColliderOf5(ref primitive, shapes.Resolve(part.ShapeId), h, centres[j]);
                // RIG5 end
                // PADDLE begin
                if (PaddleOn && part.CellTypeId == CellTypeIds.Link)
                {
                    paddles[j] = PaddleOf(adult, index, merged, unscale, shapes.Resolve(part.ShapeId), h, dressed, k, ref primitive, out string paddleSaid);
                    if (paddleSaid != null) paddleNotes.Add(paddleSaid);
                    // BLADE begin
                    bool hingeRelaid = paddles[j] != null && PaddleHinge && PaddleRelay(adult, index, merged, unscale, h, dressed, k, paddles[j]);
                    if (paddles[j] != null) PaddleOrientationLog(id, adult, index, merged, unscale, h, dressed, paddles[j], hingeRelaid);
                    if (paddles[j] != null && BladeRecording) BladeNote(id, paddles[j], h);
                    // BLADE end
                }
                // PADDLE end

                if (drawnBox) drawnBoxes++;
                field.AddPart(primitive);

                // Level 3: a box under the soft tissue takes TissueCarve; a box drawn as a leaf takes LeafCarve under
                // the leaf dial (the shader's lamina), as the palette and the shader choose for the part.
                if (Carved)
                {
                    PartShape drawnShape = shapes.Resolve(part.ShapeId);

                    tissueBox[j] = drawnBox && look.Tissue;
                    leafShaped[j] = shaped && !drawnBox && !(drawnShape is SphereShape) && !(drawnShape is CapsuleShape) &&
                                    TheatrePalette.DrawnAsLeaf(part, 2f * h);
                    // FAMILYSKIN begin
                    // A family part is never a leaf as a box is (DrawnAsLeaf reads boxes alone, and this keeps it so).
                    if (TheatreFamilySkin.Enabled && primitive.Kind == KindFamily) leafShaped[j] = false;
                    // FAMILYSKIN end
                }
            }

            // The balls across the merged joints, level 2 alone. The anchor is where the two parts'
            // anchors meet, taken from both sides (RecordedPoses.Apply's last line) and averaged.
            int balls = 0;
            if (JointBalls)
            {
                var slotOf = new Dictionary<int, int>();
                for (int j = 0; j < n; j++) slotOf[mergedParts[j]] = j;

                for (int j = 0; j < n; j++)
                {
                    int child = mergedParts[j];
                    PhenotypePart part = adult.Parts[child];
                    int parent = part.ParentIndex;
                    if (parent < 0 || parent >= count || !merged[parent]) continue;

                    int pj = slotOf[parent];

                    Vector3 fromParent = centres[pj] + rotations[pj] * (part.ParentAnchorLocal.ToVector3() * unscale);
                    Vector3 fromChild = centres[j] + rotations[j] * (part.ChildAnchorLocal.ToVector3() * unscale);
                    Vector3 anchor = 0.5f * (fromParent + fromChild);
                    // RIG5 begin
                    if (Filleted)
                    {
                        // Level 5: no ball. The joint keeps level 4's radius for the weights alone (Field.RigWeights), and the field takes
                        // the fillet and the ramp, whose faces are the two colliders' at the anchor (FilletOf).
                        float rig5Radius = RigJointShare * Mathf.Min(smallest[j], smallest[pj]);
                        if (!(rig5Radius > 1e-5f)) continue;

                        Vector3 child5Half = Abs(part.HalfExtents.ToVector3()) * unscale;
                        Vector3 parent5Half = Abs(adult.Parts[parent].HalfExtents.ToVector3()) * unscale;

                        field.AddJoint(pj, j, anchor, rig5Radius);
                        field.AddFillet(FilletOf(
                            pj, j, anchor, k,
                            shapes.Resolve(adult.Parts[parent].ShapeId), parent5Half, centres[pj], rotations[pj],
                            part.ParentAnchorLocal.ToVector3() * unscale,
                            shapes.Resolve(part.ShapeId), child5Half, centres[j], rotations[j],
                            part.ChildAnchorLocal.ToVector3() * unscale
                            // FAMILYSKIN begin
                            , TheatreFamilySkin.Enabled ? TheatreFamilySkin.SolidOf(adult.Parts[parent], parent5Half) : null,
                            TheatreFamilySkin.Enabled ? TheatreFamilySkin.SolidOf(part, child5Half) : null
                            // FAMILYSKIN end
                            ));
                        // RIG6 begin
                        if (BentHinge) field.AddHinge6(shapes.Resolve(adult.Parts[parent].ShapeId), parent5Half, shapes.Resolve(part.ShapeId), child5Half);
                        // RIG6 end
                        continue;
                    }
                    // RIG5 end

                    // RIG4 begin
                    if (Rigged)
                    {
                        float joint = RigJointShare * Mathf.Min(smallest[j], smallest[pj]);
                        if (!(joint > 1e-5f)) continue;

                        // Inset with everything else, so its radius after the inset is the joint's; the pair blend has the
                        // same radius, and the weights across it follow it (Field.RigWeights).
                        field.AddBall(anchor, joint + 0.25f * k);
                        field.AddJoint(pj, j, anchor, joint);
                        balls++;
                        continue;
                    }
                    // RIG4 end
                    float radius = Mathf.Min(k, 0.5f * Mathf.Min(smallest[j], smallest[pj]));
                    if (!(radius > 1e-5f)) continue;

                    // Inset with everything else, so its radius after the inset is the one above.
                    field.AddBall(anchor, radius + 0.25f * k);
                    balls++;
                }
            }
            // NOBOXJOIN begin

            // EVOSIM_THEATRE_NO_BOX_JOIN: at every joint between two merged parts, one at least a box the no-box rule drew as its
            // ellipsoid, a pair blend of JoinShare of the thinner part's smallest half extent and a ball of JoinBallShare of that
            // radius (Field.AddNbJoin). The anchor is taken as the balls above take it.
            int nbJoins = 0;
            float nbRadiusLeast = float.PositiveInfinity, nbRadiusMost = 0f, nbBallMost = 0f;

            if (noBoxDrawn != null)
            {
                var nbSlotOf = new Dictionary<int, int>();
                for (int j = 0; j < n; j++) nbSlotOf[mergedParts[j]] = j;

                for (int j = 0; j < n; j++)
                {
                    int child = mergedParts[j];
                    PhenotypePart part = adult.Parts[child];
                    int parent = part.ParentIndex;
                    if (parent < 0 || parent >= count || !merged[parent]) continue;

                    int pj = nbSlotOf[parent];
                    if (!noBoxDrawn[j] && !noBoxDrawn[pj]) continue;

                    float thinner = Mathf.Min(smallest[j], smallest[pj]);
                    float joinRadius = TheatreNoBox.JoinShare * thinner;
                    if (!(joinRadius > 1e-5f)) continue;

                    Vector3 fromParent = centres[pj] + rotations[pj] * (part.ParentAnchorLocal.ToVector3() * unscale);
                    Vector3 fromChild = centres[j] + rotations[j] * (part.ChildAnchorLocal.ToVector3() * unscale);
                    Vector3 anchor = 0.5f * (fromParent + fromChild);
                    float joinBall = TheatreNoBox.JoinBallShare * joinRadius;

                    field.AddNbJoin(pj, j, anchor, joinRadius, joinBall);
                    nbJoins++;
                    nbRadiusLeast = Mathf.Min(nbRadiusLeast, joinRadius);
                    nbRadiusMost = Mathf.Max(nbRadiusMost, joinRadius);
                    nbBallMost = Mathf.Max(nbBallMost, joinBall);
                }

                if (nbJoins > 0 && !_nbJoinLogged)
                {
                    _nbJoinLogged = true;

                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] EVOSIM_THEATRE_NO_BOX_JOIN={0:0.00}: body {1}, {2} joints of the skin's {3} parts joined by a pair blend of " +
                        "{4:0.0} to {5:0.0} mm ({0:0.00} x the thinner part's smallest half extent) and a ball of up to {6:0.0} mm " +
                        "({7:0.00} of the blend); the global k is {8:0.0} mm, and the skin may leave the drawn parts by up to a quarter " +
                        "of the blend, {9:0.0} mm here",
                        TheatreNoBox.JoinShare, id, nbJoins, n, 1000f * nbRadiusLeast, 1000f * nbRadiusMost, 1000f * nbBallMost,
                        TheatreNoBox.JoinBallShare, 1000f * k, 250f * nbRadiusMost));
                }
            }
            // NOBOXJOIN end

            // ---- the grid

            field.RawBounds(out Vector3 low, out Vector3 high);
            Vector3 size = high - low;
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float cell = Mathf.Max(CellOfSmallest * minSmallest, largest / CellsAcrossBody);
            // WHOLE begin

            // On an animal the cell is refined until the thinnest merged part spans ThinPartCells of them, to the cap of WholeMaxCells an axis
            // (the parts the cap would lose were left out above). The usual cell is kept for the log.
            float wholeCellToday = cell;

            if (wholeAnimal)
            {
                float wholeWanted = minSmallest / ThinPartCells;
                if (cell > wholeWanted) cell = Mathf.Max(wholeWanted, largest / WholeCapDivisor);
            }
            // WHOLE end
            if (!(cell > 1e-6f))
            {
                // BOXLOG begin
                if (note != null) note.Body = "no surface: the grid cell is " + cell.ToString("0.000000", CultureInfo.InvariantCulture) + " m";
                // BOXLOG end
                return null;
            }
            // PADDLE begin
            // The paddles' tips: the cell is refined until each blade's wanted half thickness at its tip spans ThinPartCells cells, never under
            // the longest extent over WholeCapDivisor (WholeMaxCells cells an axis, as SKIN_WHOLE's cap), and each tip is floored at that many
            // cells of the grid it is marched on, so the mesher keeps the blade's edge.
            if (paddles != null)
            {
                paddleCellToday = cell;
                float paddleWanted = float.PositiveInfinity;

                foreach (PaddleShape paddle in paddles)
                {
                    if (paddle != null) paddleWanted = Mathf.Min(paddleWanted, paddle.Wanted / ThinPartCells);
                }

                if (cell > paddleWanted) cell = Mathf.Min(cell, Mathf.Max(paddleWanted, largest / WholeCapDivisor));

                foreach (PaddleShape paddle in paddles)
                {
                    if (paddle != null) paddle.Floor(cell, 0.25f * k);
                }
            }
            // PADDLE end

            // A primitive further than this from a sample is left out of it (see Field.Cull).
            field.Cull(Mathf.Max(4f * k, 3f * cell));

            Vector3 origin = low - Vector3.one * (PadCells * cell);
            int nx = Mathf.CeilToInt(size.x / cell) + 2 * PadCells + 1;
            int ny = Mathf.CeilToInt(size.y / cell) + 2 * PadCells + 1;
            int nz = Mathf.CeilToInt(size.z / cell) + 2 * PadCells + 1;

            float[] samples = field.SampleGrid(origin, cell, nx, ny, nz);

            // ---- surface nets

            var vertices = new List<Vector3>();
            var quads = new List<int>();
            SurfaceNets(samples, origin, cell, nx, ny, nz, vertices, quads);

            if (vertices.Count == 0 || quads.Count == 0)
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] one skin: body {0}, {1} parts merged and no surface, left as parts", id, n));
                // BOXLOG begin
                if (note != null) note.Body = "no surface: the field has no zero set on the grid";
                // BOXLOG end
                return null;
            }

            Vector3[] positions = vertices.ToArray();

            // ---- Taubin, then back inside, then the normals

            Taubin(positions, quads);
            int pulled = field.PullInside(positions, 0.1f * cell, 1e-4f * cell);

            var normals = new Vector3[positions.Length];
            for (int v = 0; v < positions.Length; v++)
            {
                Vector3 g = field.Gradient(positions[v], 0.1f * cell);
                float length = g.magnitude;
                normals[v] = length > 1e-12f ? g / length : Vector3.up;
            }

            // ---- the weights: softmax of -d/k over the merged parts, the uninset fields

            var weights = new BoneWeight[positions.Length];
            var distance = new float[n];
            var order = new int[n];
            var w = new float[MaxInfluences];

            for (int v = 0; v < positions.Length; v++)
            {
                for (int j = 0; j < n; j++)
                {
                    distance[j] = field.PartDistance(j, positions[v]);
                    order[j] = j;
                }

                // Nearest first, so the weights come out largest first, as Unity wants them.
                Array.Sort(distance, order);
                float nearest = distance[0];

                int take = Mathf.Min(MaxInfluences, n);
                Array.Clear(w, 0, w.Length);
                float sum = 0f;

                for (int t = 0; t < take; t++)
                {
                    w[t] = Mathf.Exp(-(distance[t] - nearest) / k);
                    sum += w[t];
                }

                if (!(sum > 0f)) { w[0] = 1f; sum = 1f; }

                // The small weights dropped and the rest renormalised (MinWeight). Largest first, so the dropped ones
                // are the last, and the first is a quarter or more of the sum and always kept.
                float kept = 0f;
                for (int d = 0; d < take; d++)
                {
                    if (w[d] < MinWeight * sum) w[d] = 0f;
                    kept += w[d];
                }

                sum = kept;

                weights[v] = new BoneWeight
                {
                    boneIndex0 = order[0],
                    weight0 = w[0] / sum,
                    boneIndex1 = take > 1 ? order[1] : 0,
                    weight1 = take > 1 ? w[1] / sum : 0f,
                    boneIndex2 = take > 2 ? order[2] : 0,
                    weight2 = take > 2 ? w[2] / sum : 0f,
                    boneIndex3 = take > 3 ? order[3] : 0,
                    weight3 = take > 3 ? w[3] / sum : 0f,
                };
                // RIG4 begin
                if (Rigged) weights[v] = field.RigWeights(weights[v], positions[v]);
                // RIG4 end
            }

            // PADDLE begin
            if (paddles != null)
            {
                PaddleLog(id, adult, mergedParts, shapes, unscale, centres, rotations, paddles, paddleNotes, positions, weights,
                    cell, paddleCellToday, k);
            }
            // PADDLE end
            // ---- the thickness: half the chord along the inward normal, in rest metres

            float[] halfThick = HalfThicknesses(positions, normals, samples, origin, cell, nx, ny, nz, largest);

            // SKIN2's marks read the half thickness from before the cap: the cap is ruled for the shape, and a tip it calls thin
            // would otherwise take SKIN2's thin backlight and margin band, a colour the close look never had. The same array at
            // cap 0, so nothing changes there.
            float[] skin2HalfThick = ThickCap >= 1 ? (float[])halfThick.Clone() : halfThick;

            // The cap, at every skin level and with NO_BOX on or off; the carve and the mouth cut read it, SKIN2's marks do not.
            // On a flat body's rim the normal lies in the body's plane and the march runs across its width, so the half
            // thickness is capped at the smallest plain half extent of the vertex's dominant bone (weights[v].boneIndex0, as
            // Posed reads it); the march itself is unchanged. The plain extent, not the taper's or the organic silhouette's, so
            // a bulge thicker than the part is clipped.
            // THICKCAP begin
            // EVOSIM_THEATRE_SKIN_THICK_CAP: 0 no bound; 1 the plain-extent cap above; 2 the smallest of the normal chord, the chord
            // along the dominant bone's thin axis (the axis of its smallest half extent, in the rest frame as skin2Axes is) and the
            // plain-extent cap, since at a tapered tip the sheet is far thinner than the part's plain half extent.
            int cappedVertices = 0;
            float cappedMost = 0f;
            int thinAxisDecided = 0;
            int extentDecided = 0;
            if (ThickCap >= 1)
            {
                var boneCap = new float[n];
                var boneThin = new Vector3[n];
                for (int j = 0; j < n; j++)
                {
                    Vector3 capHalf = Abs(adult.Parts[mergedParts[j]].HalfExtents.ToVector3()) * unscale;
                    boneCap[j] = Mathf.Max(0.5f * ThicknessStep * cell, Mathf.Min(capHalf.x, Mathf.Min(capHalf.y, capHalf.z)));
                    int thinAxis = capHalf.x <= capHalf.y && capHalf.x <= capHalf.z ? 0 : (capHalf.y <= capHalf.z ? 1 : 2);
                    boneThin[j] = rotations[j] * (thinAxis == 0 ? Vector3.right : (thinAxis == 1 ? Vector3.up : Vector3.forward));
                }

                for (int v = 0; v < halfThick.Length; v++)
                {
                    int dominant = Mathf.Clamp(weights[v].boneIndex0, 0, n - 1);
                    float boneLimit = boneCap[dominant];
                    float normalHalf = halfThick[v];
                    float bound = boneLimit;
                    bool byThinAxis = false;

                    if (ThickCap >= 2)
                    {
                        float thinHalf = AxisHalfThickness(samples, origin, cell, nx, ny, nz, positions[v], boneThin[dominant], largest);
                        if (thinHalf <= boneLimit) { bound = thinHalf; byThinAxis = true; }
                    }

                    if (!(normalHalf > bound)) continue;

                    cappedVertices++;
                    cappedMost = Mathf.Max(cappedMost, normalHalf);
                    if (byThinAxis) thinAxisDecided++; else extentDecided++;
                    halfThick[v] = bound;
                }
            }
            // THICKCAP end

            // ---- the mesh

            int triangleCount = quads.Count / 2;
            var triangles = new int[triangleCount * 3];
            for (int q = 0, t = 0; q < quads.Count; q += 4)
            {
                triangles[t++] = quads[q];
                triangles[t++] = quads[q + 1];
                triangles[t++] = quads[q + 2];
                triangles[t++] = quads[q];
                triangles[t++] = quads[q + 2];
                triangles[t++] = quads[q + 3];
            }

            var mesh = new Mesh
            {
                name = string.Format(CultureInfo.InvariantCulture, "Theatre One Skin {0}", id),
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };

            // The shape channel (TheatreMeshes.Finish's TEXCOORD3): the skin's flag, and in y the
            // vertex's own half thickness in rest metres, which the shader scales by the body's
            // world scale (_SkinScale) for the light through it. The leaf channel is zero: no leaf.
            // UV5 and UV6 are the rest position and the rest normal, the markings' frame. The colour
            // is white until the first paint blends the tissues into it (Dress).
            var shape = new List<Vector2>(positions.Length);
            var leaf = new List<Vector4>(positions.Length);
            var colours = new Color[positions.Length];
            float thinnest = float.PositiveInfinity, thickest = 0f;

            for (int v = 0; v < positions.Length; v++)
            {
                shape.Add(new Vector2(SkinFlag, halfThick[v]));
                leaf.Add(Vector4.zero);
                colours[v] = Color.white;
                thinnest = Mathf.Min(thinnest, halfThick[v]);
                thickest = Mathf.Max(thickest, halfThick[v]);
            }

            // skin2:begin
            // SKIN2's four marks per vertex, in the leaf channel (UV4), which a skin leaves zero: how thin the tissue is, how
            // near the rest normal lies to its part's rim, and the joint's crease and its depth (Skin2Marks).
            if (TheatreSkin.Skin2On)
            {
                var skin2Axes = new Vector3[n];
                var skin2Flat = new float[n];

                for (int j = 0; j < n; j++)
                {
                    Vector3 skin2Half = Abs(adult.Parts[mergedParts[j]].HalfExtents.ToVector3()) * unscale;
                    int flatAxis = skin2Half.x <= skin2Half.y && skin2Half.x <= skin2Half.z ? 0 : (skin2Half.y <= skin2Half.z ? 1 : 2);
                    float flatSize = flatAxis == 0 ? skin2Half.x : (flatAxis == 1 ? skin2Half.y : skin2Half.z);
                    float middleSize = skin2Half.x + skin2Half.y + skin2Half.z - flatSize -
                                       Mathf.Max(skin2Half.x, Mathf.Max(skin2Half.y, skin2Half.z));

                    skin2Axes[j] = rotations[j] * (flatAxis == 0 ? Vector3.right : (flatAxis == 1 ? Vector3.up : Vector3.forward));
                    skin2Flat[j] = Skin2Step(Skin2FlatLow, Skin2FlatHigh, 1f - flatSize / Mathf.Max(middleSize, 1e-6f));
                }

                Vector4[] skin2Marks = Skin2Marks(normals, weights, skin2HalfThick, quads, skin2Axes, skin2Flat);
                // skin2b:begin
                if (TheatreSkin.Skin2BOn) Skin2BOutline(skin2Marks, weights);
                // skin2b:end
                for (int v = 0; v < positions.Length; v++) leaf[v] = skin2Marks[v];
            }

            // skin2:end
            // BAND4 begin
            // SKIN2_BAND at 4: each vertex's place along its dominant part's longest axis, the part-end mark for UV0 (Skin2BEnds).
            Vector2[] band4End = null;

            if (TheatreSkin.Skin2BandFour)
            {
                var band4Axes = new Vector3[n];
                var band4Half = new float[n];
                var band4Elongation = new float[n];

                for (int band4Bone = 0; band4Bone < n; band4Bone++)
                {
                    Vector3 band4Size = Abs(adult.Parts[mergedParts[band4Bone]].HalfExtents.ToVector3()) * unscale;
                    int band4Long = band4Size.x >= band4Size.y && band4Size.x >= band4Size.z ? 0 : (band4Size.y >= band4Size.z ? 1 : 2);
                    float band4Length = band4Long == 0 ? band4Size.x : (band4Long == 1 ? band4Size.y : band4Size.z);
                    float band4Middle = band4Size.x + band4Size.y + band4Size.z - band4Length -
                                        Mathf.Min(band4Size.x, Mathf.Min(band4Size.y, band4Size.z));

                    band4Axes[band4Bone] = rotations[band4Bone] * (band4Long == 0 ? Vector3.right : (band4Long == 1 ? Vector3.up : Vector3.forward));
                    band4Half[band4Bone] = band4Length;
                    band4Elongation[band4Bone] = Skin2Step(Skin2BEndElongLow, Skin2BEndElongHigh, 1f - band4Middle / Mathf.Max(band4Length, 1e-6f));
                }

                band4End = Skin2BEnds(positions, weights, centres, band4Axes, band4Half, band4Elongation);
            }
            // BAND4 end
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(3, shape);
            mesh.SetUVs(4, leaf);
            // BAND4 begin
            if (band4End != null) mesh.SetUVs(0, band4End);
            // BAND4 end
            mesh.SetUVs(5, positions);
            mesh.SetUVs(6, normals);

            // mouth2:begin
            // MOUTH2: the mouths of the merged eating parts (up to four, in part order), in the rest frame as the parts are
            // (the part's centre plus its rotation times Core's point, over the one scale the adult was made at), and
            // each vertex's skinning weight toward each mouth's part in UV2, which the shader's cut is weighted by. A skin
            // built under the dial always carries the channel (zero where it has no mouth), so no vertex reads a default.
            Vector4[] mouthRest = null;
            Vector4[] mouthAxes = null;
            int[] mouthParts = null;

            if (TheatreMouth2.Enabled)
            {
                if (TheatreMouth2.Bite > 0f)
                {
                    mouthParts = new int[TheatreMouth2.SkinSlots];
                    for (int slot = 0; slot < mouthParts.Length; slot++) mouthParts[slot] = -1;
                }

                var mouthSlots = new List<int>();
                // mouth2b:begin
                var mouthLine = new System.Text.StringBuilder();
                // mouth2b:end
                mouthRest = new Vector4[TheatreMouth2.SkinSlots];
                if (TheatreMouth2.Disc) mouthAxes = new Vector4[TheatreMouth2.SkinSlots];

                for (int mouthIndex = 0; mouthIndex < n && mouthSlots.Count < TheatreMouth2.SkinSlots; mouthIndex++)
                {
                    TheatreMouth2.Measured mouthMeasured = TheatreMouth2.Measure(adult, mergedParts[mouthIndex]);
                    if (!mouthMeasured.Valid) continue;

                    Vector3 mouthAt = centres[mouthIndex] + rotations[mouthIndex] * (mouthMeasured.Point * unscale);
                    mouthRest[mouthSlots.Count] = new Vector4(mouthAt.x, mouthAt.y, mouthAt.z, mouthMeasured.RadiusMetres * unscale);
                    // mouth2b:begin
                    TheatreMouth2.SkinMouth mouthPlaced = TheatreMouth2.PlaceOnSkin(
                        positions, weights, mouthIndex, centres[mouthIndex], rotations[mouthIndex],
                        mouthMeasured.Point * unscale, mouthRest[mouthSlots.Count]);
                    mouthRest[mouthSlots.Count] = mouthPlaced.Rest;
                    if (mouthAxes != null) mouthAxes[mouthSlots.Count] = mouthPlaced.AxisPhase;
                    TheatreMouth2.AppendSkinMouth(mouthLine, mouthIndex, mergedParts[mouthIndex], mouthPlaced);
                    // mouth2b:end
                    if (mouthParts != null) mouthParts[mouthSlots.Count] = mergedParts[mouthIndex];
                    mouthSlots.Add(mouthIndex);
                }

                // mouth2b:begin
                TheatreMouth2.LogSkin(id, n, mouthSlots.Count, mouthLine);
                // mouth2b:end
                var mouthWeights = new Vector4[positions.Length];

                if (mouthSlots.Count > 0)
                {
                    TheatreMouth2.EnsureKeyword();

                    for (int mouthVertex = 0; mouthVertex < positions.Length; mouthVertex++)
                    {
                        var mouthShare = Vector4.zero;
                        for (int mouthSlot = 0; mouthSlot < mouthSlots.Count; mouthSlot++)
                        {
                            mouthShare[mouthSlot] = TheatreMouth2.BoneShare(weights[mouthVertex], mouthSlots[mouthSlot]);
                        }

                        mouthWeights[mouthVertex] = mouthShare;
                    }
                }

                mesh.SetUVs(2, mouthWeights);
            }

            // mouth2:end

            // noseskin:begin
            // NOSE_PIT 2 (TheatreNosePit.OnSkin): the first merged nose's pit, in the rest frame as the mouths are (the part's
            // centre plus its rotation times the pit's tip, over the one scale the adult was made at), and each vertex's
            // skinning weight toward the nose's bone in UV1, which the shader's cut is weighted by. Only a skin with a nose in
            // it carries the channel; a second nose gets no pit. One log line for a body that has a nose at all.
            Vector4 noseRest = Vector4.zero;
            float noseDepth = 0f;

            if (TheatreNosePit.OnSkin)
            {
                int noseBone = -1;
                bool noseAny = false;

                for (int noseIndex = 0; noseIndex < count; noseIndex++)
                {
                    if (adult.Parts[noseIndex].CellTypeId == CellTypeIds.Nose) noseAny = true;
                }

                for (int noseSlot = 0; noseSlot < n && noseBone < 0; noseSlot++)
                {
                    if (adult.Parts[mergedParts[noseSlot]].CellTypeId != CellTypeIds.Nose) continue;

                    TheatreNosePit.Measured noseMeasured = TheatreNosePit.Measure(adult, mergedParts[noseSlot]);
                    if (!noseMeasured.Valid) continue;

                    Vector3 noseAt = centres[noseSlot] + rotations[noseSlot] * (noseMeasured.Tip * unscale);
                    noseRest = new Vector4(noseAt.x, noseAt.y, noseAt.z, noseMeasured.RadiusMetres * unscale);
                    noseDepth = noseMeasured.DepthMetres * unscale;
                    noseBone = noseSlot;
                }

                if (noseBone >= 0)
                {
                    TheatreNosePit.EnsureSkinKeyword();

                    var noseWeights = new Vector2[positions.Length];

                    for (int noseVertex = 0; noseVertex < positions.Length; noseVertex++)
                    {
                        noseWeights[noseVertex] = new Vector2(TheatreMouth2.BoneShare(weights[noseVertex], noseBone), 0f);
                    }

                    mesh.SetUVs(1, noseWeights);
                }

                if (noseAny) Debug.Log(NoseLog(id, adult, merged, noseBone >= 0, noseRest.w, noseDepth));
            }
            // noseskin:end

            // Level 3's carve character, per vertex (UV7), zero until the first paint blends the tissues into it (Dress).
            if (Carved) mesh.SetUVs(7, new Vector4[positions.Length]);
            mesh.colors = colours;
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights;

            var bindposes = new Matrix4x4[n];
            for (int j = 0; j < n; j++)
            {
                bindposes[j] = Matrix4x4.TRS(centres[j], rotations[j], Vector3.one).inverse;
            }

            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();

            // For each bone, the box in its bind space of the vertices whose dominant bone it is (Posed).
            var boxLow = new Vector3[n];
            var boxHigh = new Vector3[n];
            var boxed = new bool[n];

            for (int v = 0; v < positions.Length; v++)
            {
                int dominant = weights[v].boneIndex0;
                Vector3 inBind = bindposes[dominant].MultiplyPoint3x4(positions[v]);

                if (!boxed[dominant])
                {
                    boxLow[dominant] = inBind;
                    boxHigh[dominant] = inBind;
                    boxed[dominant] = true;
                }
                else
                {
                    boxLow[dominant] = Vector3.Min(boxLow[dominant], inBind);
                    boxHigh[dominant] = Vector3.Max(boxHigh[dominant], inBind);
                }
            }

            // ---- the bones, the renderer and the parts it stands for

            float grown = current.ScaledBy > 0f ? current.ScaledBy : 1f;
            var bones = new Transform[n];

            for (int j = 0; j < n; j++)
            {
                Transform host = parts[mergedParts[j]];
                var bone = new GameObject("SkinBone") { layer = host.gameObject.layer };
                bone.transform.SetParent(host, false);
                bone.transform.localPosition = Vector3.zero;
                bone.transform.localRotation = Quaternion.identity;
                bone.transform.localScale = Vector3.one * grown;
                bones[j] = bone.transform;
            }

            var skinObject = new GameObject("OneSkin") { layer = root.gameObject.layer };
            skinObject.transform.SetParent(root, false);
            skinObject.transform.localPosition = Vector3.zero;
            skinObject.transform.localRotation = Quaternion.identity;
            skinObject.transform.localScale = Vector3.one;

            SkinnedMeshRenderer renderer = skinObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = skinObject.transform;
            renderer.quality = SkinQuality.Bone4;

            // The bounds are authored, from the bones' own poses (Posed): left to the renderer they are a union over
            // every bone that carries any weight, and every bone carried a sliver.
            renderer.updateWhenOffscreen = false;
            renderer.sharedMaterial = KeywordedOf(material);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // The merged parts' own renderers, switched off; their objects stay.
            var hidden = new List<Renderer>();
            for (int j = 0; j < n; j++)
            {
                Transform[] mine = visuals != null && mergedParts[j] < visuals.Length ? visuals[mergedParts[j]] : null;
                if (mine == null) continue;

                foreach (Transform visual in mine)
                {
                    if (visual == null) continue;
                    var r = visual.GetComponent<MeshRenderer>();
                    if (r == null) continue;
                    r.enabled = false;
                    hidden.Add(r);
                }
            }

            // The material and every property but the colour are the largest merged part's.
            int biggest = mergedParts[0];
            float most = float.NegativeInfinity;
            for (int j = 0; j < n; j++)
            {
                float volume = adult.Parts[mergedParts[j]].Volume;
                if (volume > most) { most = volume; biggest = mergedParts[j]; }
            }
            // WHOLE begin

            // The skin takes its material and block from the largest merged part. A blade brought in by the dial carries the lamina's
            // (its sway, its leaf look), which the skin has no use for, so another part is chosen when there is one.
            if (wholeAnimal)
            {
                float wholeMost = float.NegativeInfinity;
                int wholeBiggest = -1;

                for (int j = 0; j < n; j++)
                {
                    if (wholeBrought[mergedParts[j]]) continue;

                    float wholeVolume = adult.Parts[mergedParts[j]].Volume;
                    if (wholeVolume > wholeMost) { wholeMost = wholeVolume; wholeBiggest = mergedParts[j]; }
                }

                if (wholeBiggest >= 0) biggest = wholeBiggest;
            }
            // WHOLE end

            var parentOf = new int[count];
            for (int i = 0; i < count; i++) parentOf[i] = adult.Parts[i].ParentIndex;

            clock.Stop();
            DressTimers.OneSkinMs += clock.Elapsed.TotalMilliseconds; // the film's timing: dress line
            DressTimers.OneSkins++;

            // The brief's line, and after it in brackets what a reader of the spike needs to judge it.
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] one skin: body {0}, {1} parts, {2} vertices, {3} triangles, {4:0.0} ms " +
                "(k {5:0.0000} m, cell {6:0.0000} m, grid {7}x{8}x{9}, {10} joint balls, {11} vertices pulled inside, " +
                "{12} boxes drawn shaped{13}, half thickness {14:0.0000} to {15:0.0000} m{17}, {16} thin parts left out)",
                id, n, positions.Length, triangleCount, clock.Elapsed.TotalMilliseconds,
                k, cell, nx, ny, nz, balls, pulled, drawnBoxes,
                look.Organic ? (look.Tissue ? " with the organic tissue" : " with the organic silhouette") : "",
                thinnest, thickest, thinLeftOut,
                cappedVertices > 0 ? (ThickCap >= 2 ? string.Format(CultureInfo.InvariantCulture,
                    " (half thickness bounded on {0} v (thin axis {1}, extent {2}), max before {3:0.0000} m)",
                    cappedVertices, thinAxisDecided, extentDecided, cappedMost) : string.Format(CultureInfo.InvariantCulture,
                    " (half thickness capped on {0} v, max before {1:0.0000} m)", cappedVertices, cappedMost)) : ""));
            // RIG4 begin
            // WHOLE begin
            // The rig line with the dial's tail: what it brought in (the producers, the thin parts), what the cap on the grid left out,
            // the cell with the dial and the cell the usual rule gives, the grid, and how many cells the thinnest part spans.
            if (WholeOn)
            {
                string wholeTail = "; whole: not an animal";

                if (wholeAnimal)
                {
                    int wholeLeaves = 0, wholeThin = 0;

                    for (int wholeI = 0; wholeI < count; wholeI++)
                    {
                        if (!merged[wholeI]) continue;
                        if (wholeBrought[wholeI]) wholeLeaves++;
                        if (wholeRestored[wholeI]) wholeThin++;
                    }

                    wholeTail = string.Format(CultureInfo.InvariantCulture,
                        "; whole: photosynthetic brought in {0}, thin brought in {1}, thin left out at the cap {2}, " +
                        "cell {3:0.0000} m (usual {4:0.0000} m), grid {5}x{6}x{7} (cap {8} cells an axis), thinnest part spans {9:0.00} cells",
                        wholeLeaves, wholeThin, wholeCapped, cell, wholeCellToday, nx, ny, nz, WholeMaxCells,
                        cell > 1e-9f ? minSmallest / cell : 0f);
                }

                Debug.Log(RigLog(id, adult, merged, field) + wholeTail);
            }
            else
            // WHOLE end
            if (Rigged) Debug.Log(RigLog(id, adult, merged, field));
            // RIG4 end
            // RIG5 begin
            if (Filleted) Debug.Log(Rig5Log(id, field));
            // RIG5 end
            // SEAT begin
            if (seatProbe != null) for (int sp = 0; sp < seatProbe.Count; sp++) Debug.Log(seatProbe[sp]);
            // SEAT end

            var skin = new TheatreOneSkin(
                root, renderer, mesh, bones, mergedParts.ToArray(), merged, parentOf, hidden.ToArray(),
                visuals != null && biggest < visuals.Length ? visuals[biggest] : null, weights, colours);

            skin._boxLow = boxLow;
            skin._boxHigh = boxHigh;
            skin._boxed = boxed;
            // BOXLOG begin
            if (note != null) note.Built = true;
            // BOXLOG end

            // The family spot (EVOSIM_THEATRE_SKIN_MAP_FAMILY_SPOT): the skin's largest part, as the material is taken from, its rest centre and
            // 0.35 of twice its smallest half extent as the eyespot's radius (the shader's rule on the whole box), for FamilySpotOfRest.
            if (TheatreSkinMap.FamilySpotOn)
            {
                for (int j = 0; j < n; j++)
                {
                    if (mergedParts[j] != biggest) continue;

                    Vector3 spotHalf = Abs(adult.Parts[biggest].HalfExtents.ToVector3()) * unscale;

                    skin._spotCentre = centres[j];
                    skin._spotRadius = 0.7f * Mathf.Min(spotHalf.x, Mathf.Min(spotHalf.y, spotHalf.z));
                    break;
                }
            }

            // mouth2:begin
            skin._mouthRest = mouthRest;
            skin._mouthAxes = mouthAxes;
            skin._mouthParts = mouthParts;
            // mouth2:end
            // noseskin:begin
            skin._noseRest = noseRest;
            skin._noseDepth = noseDepth;
            // noseskin:end
            // ORGANSEAT begin
            skin._organSeated = organSeated;
            // ORGANSEAT end
            // SEATPROBE begin
            skin._seatRows = SeatRowsOf(adult, unscale, merged, mergedParts, centres, rotations, seatBoxes, positions, triangles);
            // SEATPROBE end

            if (Carved)
            {
                skin._carveLength = Mathf.Max(1e-5f, median * CarveLengthShare);
                skin._tissueBox = tissueBox;
                skin._leafShaped = leafShaped;
                skin._character = new Vector4[n];
                skin._nextCharacter = new Vector4[n];
                skin._carveUVs = new Vector4[positions.Length];
                skin._concavity = Concavities(positions, normals, quads, Mathf.Max(k, 2f * cell));
                // RIG5 begin
                if (Filleted) skin._carve5 = Carve5(positions, halfThick, cell, field);
                // RIG5 end
            }
            // skin2:begin
            // The one body-wide length SKIN2's asymmetry and relief are read over, at every level (level 3 sets it above).
            if (TheatreSkin.Skin2On) skin._carveLength = Mathf.Max(1e-5f, median * CarveLengthShare);

            // skin2:end

            // RIG6 begin
            if (BentHinge) skin.SetUpHinges6(field, positions, normals, weights, bindposes, rotations, cell, id);
            // RIG6 end
            skin.Posed();
            return skin;
        }

        /// <summary>A body has grown: every bone takes the grown phenotype's scale against the adult. No rebuild.</summary>
        public void Resize(Phenotype grown)
        {
            if (grown == null) return;

            float s = grown.ScaledBy > 0f ? grown.ScaledBy : 1f;
            var scale = new Vector3(s, s, s);

            for (int j = 0; j < _bones.Length; j++)
            {
                if (_bones[j] != null && _bones[j].localScale != scale) _bones[j].localScale = scale;
            }

            Posed();

            Rescale();
        }

        /// <summary>
        /// The renderer's bounds from the bones' poses now: for each bone, the box of the vertices it dominates, in its
        /// bind space, carried by the bone's matrix into the skin object's space (the root bone, which never turns as
        /// the body does: the parts carry the attitude, so the rest mesh's box points the wrong way), eight corners
        /// each, and the union. A bone with no dominant vertices is skipped. The bones' scale carries the growth, so
        /// a growth step needs nothing else. Called after the parts are placed (LiveWorldView.Place, Pose), for a
        /// portrait that measures its subject by its renderers' boxes before the first render
        /// (FilmPlans.DrawnBounds). Never called with the dial off.
        /// </summary>
        public void Posed()
        {
            if (_renderer == null || _boxLow == null || _bones == null) return;
            // RIG6 begin
            if (BentHinge) Bend6();
            // RIG6 end

            Matrix4x4 toSkin = _renderer.transform.worldToLocalMatrix;
            Vector3 low = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 high = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            bool any = false;

            for (int j = 0; j < _bones.Length; j++)
            {
                if (!_boxed[j] || _bones[j] == null) continue;

                Matrix4x4 m = toSkin * _bones[j].localToWorldMatrix;
                Vector3 a = _boxLow[j];
                Vector3 b = _boxHigh[j];

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? a.x : b.x,
                        (corner & 2) == 0 ? a.y : b.y,
                        (corner & 4) == 0 ? a.z : b.z));

                    low = Vector3.Min(low, p);
                    high = Vector3.Max(high, p);
                }

                any = true;
            }

            if (!any) return;

            var bounds = new Bounds();
            bounds.SetMinMax(low, high);
            _renderer.localBounds = bounds;
        }

        // RIG6 begin
        /// <summary>
        /// Level 6, once per body after the mesh is built: keeps the field, the rest positions and normals, the bindposes and the rest
        /// rotations, and picks each hinge's zone: the vertices within <see cref="Rig6ZoneShare"/> of the larger of the joint's weight
        /// zone and its ramp's reach from the anchor that carry weight on the joint's two bones, each in its nearest hinge's zone.
        /// Grows each bone's box by the most a vertex may move, so the authored bounds hold it, marks the mesh dynamic, and logs one
        /// line with each hinge's two faces' sizes and its sleeve.
        /// </summary>
        private void SetUpHinges6(
            Field field, Vector3[] positions, Vector3[] normals, BoneWeight[] weights, Matrix4x4[] bindposes, Quaternion[] rotations,
            float cell, long id)
        {
            int hinges = field.Hinge6Count;
            if (hinges == 0 || _mesh == null) return;

            int n = bindposes.Length;
            _field6 = field;
            _cell6 = cell;
            _id6 = id;
            _rest6 = (Vector3[])positions.Clone();
            _restNormals6 = (Vector3[])normals.Clone();
            _work6 = (Vector3[])positions.Clone();
            _workNormals6 = (Vector3[])normals.Clone();
            _bind6 = (Matrix4x4[])bindposes.Clone();
            _restRot6 = (Quaternion[])rotations.Clone();
            _skin6 = new Matrix4x4[n];
            _last6 = new Matrix4x4[n];
            _toRest6 = new Matrix4x4[n];

            _hingeParent6 = new int[hinges];
            _hingeChild6 = new int[hinges];
            _hingePush6 = new float[hinges];
            _hingeShare6 = new float[hinges];
            _hingeBend6 = new float[hinges];
            _hingeLogged6 = new float[hinges];

            var anchors = new Vector3[hinges];
            var reach = new float[hinges];
            float pushMost = 0f, sleeveLeast = float.PositiveInfinity, sleeveMost = 0f;
            int waisted = 0;
            var faces = new System.Text.StringBuilder();

            for (int f = 0; f < hinges; f++)
            {
                Fillet5 fillet = field.FilletOf6(f);
                Hinge6 hinge = field.HingeOf6(f);

                _hingeParent6[f] = fillet.Parent;
                _hingeChild6[f] = fillet.Child;
                _hingePush6[f] = Rig6MaxPush * fillet.Radius;
                anchors[f] = fillet.Anchor;
                reach[f] = Rig6ZoneShare * Mathf.Max(RigZoneOuter * field.JointRadiusOf6(f), fillet.Reach);
                pushMost = Mathf.Max(pushMost, _hingePush6[f]);

                if (hinge.Waisted)
                {
                    waisted++;
                    sleeveLeast = Mathf.Min(sleeveLeast, hinge.Radius);
                    sleeveMost = Mathf.Max(sleeveMost, hinge.Radius);
                }

                faces.Append(string.Format(CultureInfo.InvariantCulture, " part {0} to {1}: faces {2:0.0000} and {3:0.0000} m{4};",
                    _mergedParts[fillet.Parent], _mergedParts[fillet.Child], hinge.ParentSize, hinge.ChildSize,
                    hinge.Waisted
                        ? string.Format(CultureInfo.InvariantCulture, ", waisted, sleeve {0:0.0000} m deep {1:0.0000} m", hinge.Radius, hinge.Depth)
                        : ""));
            }

            var zone = new List<int>();
            var zoneHinge = new List<int>();

            for (int v = 0; v < positions.Length; v++)
            {
                int best = -1;
                float bestAway = float.PositiveInfinity;

                for (int f = 0; f < hinges; f++)
                {
                    float away = (positions[v] - anchors[f]).magnitude / Mathf.Max(reach[f], 1e-9f);
                    if (away > 1f || away >= bestAway) continue;

                    float on = Share6(weights[v], _hingeParent6[f]) + Share6(weights[v], _hingeChild6[f]);
                    if (!(on > 0f)) continue;

                    best = f;
                    bestAway = away;
                }

                if (best < 0) continue;

                zone.Add(v);
                zoneHinge.Add(best);
            }

            _zone6 = zone.ToArray();
            _zoneHinge6 = zoneHinge.ToArray();

            // The authored bounds (Posed) are each bone's box of its dominant vertices, which a correction moves by at most pushMost.
            for (int j = 0; j < _boxed.Length; j++)
            {
                if (!_boxed[j]) continue;

                _boxLow[j] -= Vector3.one * pushMost;
                _boxHigh[j] += Vector3.one * pushMost;
            }

            // BEYOND begin
            if (Beyond6 > 0f)
            {
                var identity6 = new Matrix4x4[n];
                for (int j = 0; j < n; j++) identity6[j] = Matrix4x4.identity;

                for (int f = 0; f < hinges; f++) _beyondR6 = Mathf.Max(_beyondR6, field.HingeOf6(f).BeyondRadius);
                for (int v = 0; v < positions.Length; v++) _beyondMost6 = Mathf.Max(_beyondMost6, field.PosedBound6(positions[v], identity6));

                _beyondLogged6 = _beyondMost6;
                Debug.Log(BeyondLog6("at rest"));
            }
            // BEYOND end
            _mesh.MarkDynamic();

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] one skin rig6: body {0}, {1} hinges, {2} waisted (sleeve radius {3:0.0000} to {4:0.0000} m), {5} zone vertices " +
                "of {6}, push cap {7:0.0000} m, correction from {8:0} to {9:0} degrees;{10}",
                id, hinges, waisted, waisted > 0 ? sleeveLeast : 0f, sleeveMost, _zone6.Length, positions.Length, pushMost,
                Rig6BendLow, Rig6BendHigh, faces));
        }

        /// <summary>
        /// Level 6, per frame (from <see cref="Posed"/>): each hinge's bend from rest, the angle between its two bones' turns from
        /// their rest rotations; for a hinge bent past <see cref="Rig6BendLow"/>, each of its zone's vertices moves onto the field
        /// with every part in its posed frame (<c>Field.PosedAt6</c>). From where linear-blend skinning puts it, Newton steps
        /// along the posed field's gradient, stopped at a crease (two steps' gradients more than <see cref="Rig6StopDegrees"/>
        /// apart); the move carried back into rest space by the inverse of the vertex's blended matrix, capped at the hinge's push,
        /// and eased in by a smoothstep of the bend up to <see cref="Rig6BendHigh"/>. The normal is the posed gradient carried back
        /// the same way. The mesh's positions and normals are written when a hinge is bent, and set back to the rest once none is.
        /// Runs on a skin the camera saw last frame alone (<c>Renderer.isVisible</c>), and not at all when no bone moved.
        /// </summary>
        private void Bend6()
        {
            if (_zone6 == null || _field6 == null || _mesh == null || _renderer == null) return;
            if (!_renderer.isVisible) return;

            int n = _bones.Length;
            if (_skin6.Length != n) return;

            Matrix4x4 toSkin = _renderer.transform.worldToLocalMatrix;
            Quaternion skinTurn = Quaternion.Inverse(_renderer.transform.rotation);
            bool same = _have6;

            for (int j = 0; j < n; j++)
            {
                if (_bones[j] == null) return;

                _skin6[j] = toSkin * _bones[j].localToWorldMatrix * _bind6[j];
                if (same && _skin6[j] != _last6[j]) same = false;
            }

            if (same) return;

            for (int j = 0; j < n; j++)
            {
                _last6[j] = _skin6[j];
                _toRest6[j] = _skin6[j].inverse;
            }

            _have6 = true;

            float scale = new Vector3(_skin6[0].m00, _skin6[0].m10, _skin6[0].m20).magnitude;
            if (!(scale > 1e-9f)) return;

            bool anyBent = false;

            for (int f = 0; f < _hingeParent6.Length; f++)
            {
                int p = _hingeParent6[f], c = _hingeChild6[f];
                Quaternion turnP = skinTurn * _bones[p].rotation * Quaternion.Inverse(_restRot6[p]);
                Quaternion turnC = skinTurn * _bones[c].rotation * Quaternion.Inverse(_restRot6[c]);
                float bend = Quaternion.Angle(turnP, turnC);
                float t = Mathf.Clamp01((bend - Rig6BendLow) / (Rig6BendHigh - Rig6BendLow));

                _hingeBend6[f] = bend;
                _hingeShare6[f] = t * t * (3f - 2f * t);
                if (_hingeShare6[f] > 0f) anyBent = true;
            }

            if (!anyBent)
            {
                if (!_bent6) return;

                Array.Copy(_rest6, _work6, _rest6.Length);
                Array.Copy(_restNormals6, _workNormals6, _restNormals6.Length);
                _mesh.SetVertices(_work6);
                _mesh.SetNormals(_workNormals6);
                _bent6 = false;
                return;
            }

            Stopwatch clock = Stopwatch.StartNew();
            float tolerance = Rig6Tolerance * _cell6;
            float step = Rig6GradientCells * _cell6 * scale;
            float stopCos = Mathf.Cos(Rig6StopDegrees * Mathf.Deg2Rad);
            int moved = 0;
            float most = 0f;

            for (int z = 0; z < _zone6.Length; z++)
            {
                int v = _zone6[z];
                int f = _zoneHinge6[z];
                float share = _hingeShare6[f];
                Vector3 rest = _rest6[v];

                _work6[v] = rest;
                _workNormals6[v] = _restNormals6[v];
                if (!(share > 0f)) continue;

                Matrix4x4 blend = Blend6(_weights[v]);
                Vector3 skinned = blend.MultiplyPoint3x4(rest);
                float value = _field6.PosedAt6(skinned, _toRest6);
                if (Mathf.Abs(value) <= tolerance) continue;

                Vector3 x = skinned;
                Vector3 gradient = Gradient6(x, step);

                for (int s = 0; s < Rig6Steps; s++)
                {
                    float g2 = Vector3.Dot(gradient, gradient);
                    if (!(g2 > 1e-12f)) break;

                    Vector3 next = x - gradient * (value / g2);
                    Vector3 nextGradient = Gradient6(next, step);
                    if (Vector3.Dot(nextGradient.normalized, gradient.normalized) < stopCos) break;

                    x = next;
                    gradient = nextGradient;
                    value = _field6.PosedAt6(x, _toRest6);
                    if (Mathf.Abs(value) <= tolerance) break;
                }

                Vector3 delta = Inverse6(blend, x - skinned);
                float length = delta.magnitude;
                if (length > _hingePush6[f]) delta *= _hingePush6[f] / length;
                delta *= share;

                _work6[v] = rest + delta;
                // BEYOND begin
                if (Beyond6 > 0f) _beyondMost6 = Mathf.Max(_beyondMost6, _field6.PosedBound6(blend.MultiplyPoint3x4(_work6[v]), _toRest6));
                // BEYOND end

                Vector3 normal = Inverse6(blend, gradient);
                if (normal.sqrMagnitude > 1e-20f)
                {
                    Vector3 eased = _restNormals6[v] * (1f - share) + normal.normalized * share;
                    if (eased.sqrMagnitude > 1e-20f) _workNormals6[v] = eased.normalized;
                }

                moved++;
                most = Mathf.Max(most, delta.magnitude);
            }

            _mesh.SetVertices(_work6);
            _mesh.SetNormals(_workNormals6);
            _bent6 = true;
            // BEYOND begin
            if (Beyond6 > 0f && _beyondMost6 > 1e-6f && _beyondMost6 > 1.25f * _beyondLogged6)
            {
                _beyondLogged6 = _beyondMost6;
                Debug.Log(BeyondLog6("bent"));
            }
            // BEYOND end
            clock.Stop();

            for (int f = 0; f < _hingeParent6.Length; f++)
            {
                float stepNow = Mathf.Floor(_hingeBend6[f] / Rig6LogStep);
                if (!(_hingeShare6[f] > 0f) || !(stepNow > _hingeLogged6[f])) continue;

                _hingeLogged6[f] = stepNow;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] one skin rig6 bend: body {0}, part {1} to {2} bent {3:0.0} degrees (share {4:0.00}); this frame {5} of {6} " +
                    "zone vertices moved, the most {7:0.0000} m (rest), {8:0.0} ms",
                    _id6, _mergedParts[_hingeParent6[f]], _mergedParts[_hingeChild6[f]], _hingeBend6[f], _hingeShare6[f], moved,
                    _zone6.Length, most, clock.Elapsed.TotalMilliseconds));
            }
        }

        // BEYOND begin
        /// <summary>The BEYOND test's line: the widest the body's tissue has gone beyond the bound so far, in metres and in r.</summary>
        private string BeyondLog6(string when)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "[Theatre] one skin rig6 beyond: body {0}, widest beyond the colliders and the wedge so far {1:0.0000} m (rest), " +
                "{2:0.00} r (r {3:0.0000} m, the largest round end at a beyond hinge), {4}, dial {5:0.00}",
                _id6, Mathf.Max(0f, _beyondMost6), _beyondR6 > 0f ? Mathf.Max(0f, _beyondMost6) / _beyondR6 : 0f, _beyondR6, when, Beyond6);
        }
        // BEYOND end
        /// <summary>Level 6: the posed field's gradient at a point in the skin object's space, by central differences.</summary>
        private Vector3 Gradient6(Vector3 p, float step)
        {
            float inv = 0.5f / step;
            return new Vector3(
                (_field6.PosedAt6(new Vector3(p.x + step, p.y, p.z), _toRest6) - _field6.PosedAt6(new Vector3(p.x - step, p.y, p.z), _toRest6)) * inv,
                (_field6.PosedAt6(new Vector3(p.x, p.y + step, p.z), _toRest6) - _field6.PosedAt6(new Vector3(p.x, p.y - step, p.z), _toRest6)) * inv,
                (_field6.PosedAt6(new Vector3(p.x, p.y, p.z + step), _toRest6) - _field6.PosedAt6(new Vector3(p.x, p.y, p.z - step), _toRest6)) * inv);
        }

        /// <summary>Level 6: a vertex's blended skinning matrix now, the weighted sum of its bones' (Unity's linear blend).</summary>
        private Matrix4x4 Blend6(BoneWeight bw)
        {
            var m = new Matrix4x4();
            Add6(ref m, _skin6[bw.boneIndex0], bw.weight0);
            Add6(ref m, _skin6[bw.boneIndex1], bw.weight1);
            Add6(ref m, _skin6[bw.boneIndex2], bw.weight2);
            Add6(ref m, _skin6[bw.boneIndex3], bw.weight3);
            m.m33 = 1f;
            return m;
        }

        private static void Add6(ref Matrix4x4 m, Matrix4x4 a, float w)
        {
            if (!(w > 0f)) return;

            m.m00 += w * a.m00; m.m01 += w * a.m01; m.m02 += w * a.m02; m.m03 += w * a.m03;
            m.m10 += w * a.m10; m.m11 += w * a.m11; m.m12 += w * a.m12; m.m13 += w * a.m13;
            m.m20 += w * a.m20; m.m21 += w * a.m21; m.m22 += w * a.m22; m.m23 += w * a.m23;
        }

        /// <summary>Level 6: the inverse of a matrix's 3x3 linear part applied to a vector (zero for a singular one).</summary>
        private static Vector3 Inverse6(Matrix4x4 a, Vector3 r)
        {
            float c00 = a.m11 * a.m22 - a.m12 * a.m21;
            float c01 = a.m12 * a.m20 - a.m10 * a.m22;
            float c02 = a.m10 * a.m21 - a.m11 * a.m20;
            float det = a.m00 * c00 + a.m01 * c01 + a.m02 * c02;
            if (!(Mathf.Abs(det) > 1e-12f)) return Vector3.zero;

            float inv = 1f / det;
            return new Vector3(
                (c00 * r.x + (a.m02 * a.m21 - a.m01 * a.m22) * r.y + (a.m01 * a.m12 - a.m02 * a.m11) * r.z) * inv,
                (c01 * r.x + (a.m00 * a.m22 - a.m02 * a.m20) * r.y + (a.m02 * a.m10 - a.m00 * a.m12) * r.z) * inv,
                (c02 * r.x + (a.m01 * a.m20 - a.m00 * a.m21) * r.y + (a.m00 * a.m11 - a.m01 * a.m10) * r.z) * inv);
        }

        /// <summary>Level 6: a vertex's weight on one bone.</summary>
        private static float Share6(BoneWeight bw, int bone) =>
            (bw.boneIndex0 == bone ? bw.weight0 : 0f) + (bw.boneIndex1 == bone ? bw.weight1 : 0f) +
            (bw.boneIndex2 == bone ? bw.weight2 : 0f) + (bw.boneIndex3 == bone ? bw.weight3 : 0f);
        // RIG6 end
        // The skin map's family dial: the skin's rest box, made once from the rest positions (UV5, the very positions the shader reads as restOS).
        private bool _familyBoxDone;
        private Vector4 _familyBox, _familyExtent;

        /// <summary>
        /// The axis-aligned bounds of the skin's rest positions (the mesh's UV5 channel, which is what the shader's <c>restOS</c> reads, in
        /// rest metres), made once: <c>_familyBox</c> the centre with w one, <c>_familyExtent</c> the half extents. The mesh's own bounds are
        /// not used, since Bend6 rewrites its vertices. Called only while the family dial is on. With no positions, a unit box at the origin
        /// is left with w zero, which the shader reads as no box.
        /// </summary>
        private void FamilyBoxOfRest()
        {
            _familyBoxDone = true;
            _familyBox = Vector4.zero;
            _familyExtent = Vector4.zero;

            if (_mesh == null) return;

            var rest = new List<Vector3>();
            _mesh.GetUVs(5, rest);
            if (rest.Count == 0) return;

            Vector3 lo = rest[0], hi = rest[0];

            for (int i = 1; i < rest.Count; i++)
            {
                lo = Vector3.Min(lo, rest[i]);
                hi = Vector3.Max(hi, rest[i]);
            }

            Vector3 centre = 0.5f * (lo + hi), half = 0.5f * (hi - lo);

            _familyBox = new Vector4(centre.x, centre.y, centre.z, 1f);
            _familyExtent = new Vector4(half.x, half.y, half.z, 0f);
        }

        /// <summary>The largest merged part's rest centre and its eyespot radius (rest metres), found at the build; zero radius for none. Under the family spot dial only.</summary>
        private Vector3 _spotCentre;
        private float _spotRadius;

        /// <summary>
        /// The shader's <c>_FamilySpot</c>: xyz the largest part's rest centre in the family box's -0.5 to 0.5 units (over twice the box's half
        /// extents, as the shader maps a position into the box), w the radius in rest metres. Zero (as today) with no part, no radius or no box.
        /// Called only while the family spot dial is on, after <see cref="FamilyBoxOfRest"/>.
        /// </summary>
        private Vector4 FamilySpotOfRest()
        {
            if (!(_spotRadius > 0f) || _familyBox.w < 0.5f) return Vector4.zero;

            return new Vector4(
                (_spotCentre.x - _familyBox.x) / Mathf.Max(2f * _familyExtent.x, 1e-4f),
                (_spotCentre.y - _familyBox.y) / Mathf.Max(2f * _familyExtent.y, 1e-4f),
                (_spotCentre.z - _familyBox.z) / Mathf.Max(2f * _familyExtent.z, 1e-4f),
                Mathf.Max(_spotRadius, 1e-5f));
        }

        /// <summary>
        /// The body's world scale has changed (a growth step, or the root's own scale, a corpse's shrink):
        /// the shader's <c>_SkinScale</c>, the metres in the world of one rest metre, written again once the
        /// skin has been dressed. Before that, <see cref="Dress"/> writes it.
        /// </summary>
        public void Rescale()
        {
            if (_renderer == null || _block == null) return;

            float scale = WorldScale();
            if (scale == _scaleWritten) return;

            // The renderer's block as it stands, so a fade the palette wrote since the last paint is kept.
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(SkinScaleId, scale);
            _scaleWritten = scale;
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// After every paint of the body: the merged parts' renderers off again (the palette's dressing
        /// switches every renderer on), each neck between two merged parts off, and the skin given the
        /// material and block of the largest merged part, less the properties that are in that part's
        /// own object units, with its world scale, and its vertex colours blended again from the parts'
        /// painted colours when one has moved.
        /// </summary>
        public void Dress(TheatrePalette palette, long id)
        {
            for (int i = 0; i < _hidden.Length; i++)
            {
                if (_hidden[i] != null && _hidden[i].enabled) _hidden[i].enabled = false;
            }

            if (palette != null && palette.NecksOf(id, _root, _necks, _neckJoints))
            {
                for (int i = 0; i < _necks.Count; i++)
                {
                    int joint = _neckJoints[i];
                    if (joint < 0 || joint >= _merged.Length) continue;

                    int parent = _parentOf[joint];
                    if (parent < 0 || parent >= _merged.Length) continue;
                    if (!_merged[joint] || !_merged[parent]) continue;

                    Transform neck = _necks[i];
                    var r = neck != null ? neck.GetComponent<MeshRenderer>() : null;
                    if (r != null && r.enabled) r.enabled = false;
                }
            }

            // ORGANSEAT begin
            // SEAT_MARKER hides every knuckle and bridge at a joint where a part stands apart and its parent is in the skin; ORGAN_SEAT
            // hides the bridges at a joint it seated, where the skin now reaches the part, and keeps the knuckles.
            if ((SeatMarkerOff || _organSeated != null) && palette != null && palette.NecksOf(id, _root, _necks, _neckJoints))
            {
                for (int i = 0; i < _necks.Count; i++)
                {
                    int joint = _neckJoints[i];
                    if (joint < 0 || joint >= _merged.Length || _merged[joint]) continue;

                    int parent = _parentOf[joint];
                    if (parent < 0 || parent >= _merged.Length || !_merged[parent]) continue;

                    Transform neck = _necks[i];
                    if (neck == null) continue;

                    bool seated = _organSeated != null && joint < _organSeated.Length && _organSeated[joint];
                    if (!SeatMarkerOff && !(seated && neck.name.StartsWith(BridgeNamePrefix, StringComparison.Ordinal))) continue;

                    var r = neck.GetComponent<MeshRenderer>();
                    if (r != null && r.enabled) r.enabled = false;
                }
            }
            // ORGANSEAT end
            // SEATPROBE begin
            if (_seatRows != null && !_seatRowsSaid) LogSeatRows(palette, id);
            // SEATPROBE end
            if (_renderer == null || _sourceVisuals == null) return;

            MeshRenderer source = null;
            foreach (Transform visual in _sourceVisuals)
            {
                if (visual == null) continue;
                source = visual.GetComponent<MeshRenderer>();
                if (source != null) break;
            }

            if (source == null) return;

            // The parts' material, as a copy with the skin's keyword on. A fading pair (a newborn, a corpse) is left as the
            // palette's fade last wrote it onto this renderer, which it does right after a paint (ApplyFades), from the
            // copy it finds here; a pair copied from the parts would not map back to its opaque material when the fade ends.
            Material[] sourceMaterials = source.sharedMaterials;
            if (sourceMaterials.Length == 1) _renderer.sharedMaterial = KeywordedOf(sourceMaterials[0]);

            if (_block == null) _block = new MaterialPropertyBlock();
            source.GetPropertyBlock(_block);

            // The tissues' colours into the vertices, before the block is cut down: the fallback for a
            // part the palette has no colour for is the largest part's own.
            Blend(palette, id, _block.GetVector(BaseColorId));

            // Level 3: each tissue's carve character into the vertices, from the same block (the largest part's is
            // the fallback for a part the palette has no block for).
            if (Carved) BlendCarve(palette, id, _block.GetVector(CarveId), _block.GetFloat(VelvetId), _block.GetFloat(LeafLookId));

            // In the source part's own object units, so meaningless on the skin, whose object space
            // is the body's: none, as a neck has none.
            _block.SetVector(PinchAId, Vector4.zero);
            _block.SetVector(PinchBId, Vector4.zero);
            _block.SetVector(LeafId, Vector4.zero);
            _block.SetVector(RampAxisId, Vector4.zero);
            _block.SetFloat(VelvetId, 0f);
            _block.SetVector(VelvetOsculumId, Vector4.zero);
            _block.SetVector(LinkAxisId, Vector4.zero);
            _block.SetVector(LinkShapeId, Vector4.zero);
            _block.SetVector(SwayPartId, Vector4.zero);
            // mouth2:begin
            // The largest part's block carries its own _Mouth, in that part's object units: the skin's replace them.
            if (TheatreMouth2.Enabled)
            {
                TheatreMouth2.DressSkin(_block, _mouthRest, _mouthAxes, _mouthBiteSet ? _mouthBite : (Vector4?)null,
                    _mouthGapeSet ? _mouthGape : (Vector4?)null);
                if (TheatreMouth2.Bite > 0f) TheatreMouthBite.SkinSeen(id, this);
            }

            // mouth2:end
            // noseskin:begin
            // The largest part's block carries no pit of the skin's: a nose part's own _Pit is in that part's object units. The
            // skin's pit goes on the block here, at every dress, as the mouths' do.
            if (_noseRest.w > 0f) TheatreNosePit.DressSkin(_block, _noseRest, _noseDepth);
            // noseskin:end
            // uptake-skin:begin
            // The largest part's block carries its own _Uptake, which would swell and deepen the whole skin by that part's
            // uptake (a part's own units, like the others zeroed above). Zeroed only while the uptake dial is on.
            if (TheatreUptake.Enabled) _block.SetVector(UptakeId, Vector4.zero);

            // uptake-skin:end

            // The metres in the world of one rest metre, for the per-vertex half thickness.
            _scaleWritten = WorldScale();
            _block.SetFloat(SkinScaleId, _scaleWritten);

            // Level 3 alone: the carve on, and its length in rest metres (the shader reads the block's own _Carve for the seed).
            if (Carved)
            {
                _block.SetFloat(SkinCarveId, 1f);
                _block.SetFloat(SkinCarveLengthId, _carveLength);
            }
            // skin2:begin
            if (TheatreSkin.Skin2On && !Carved) _block.SetFloat(SkinCarveLengthId, _carveLength);
            // skin2:end

            // The skin map's family dial (EVOSIM_THEATRE_SKIN_MAP_FAMILY), only while it is on: the skin's rest box, so the shader lays the bars
            // and the eyespot along the body and not along the skin object's unit frame. Nothing is written with the dial off.
            if (TheatreSkinMap.FamilyOn)
            {
                if (!_familyBoxDone) FamilyBoxOfRest();

                _block.SetVector(FamilyBoxId, _familyBox);
                _block.SetVector(FamilyExtentId, _familyExtent);
                if (TheatreSkinMap.FamilySpotOn) _block.SetVector(FamilySpotId, FamilySpotOfRest());
            }

            _renderer.SetPropertyBlock(_block);
            // DEBUGTINT begin
            if (TheatreDebugTint.On)
            {
                if (_debugColours == null) _debugColours = DebugColours(id);
                if (_mesh != null) _mesh.colors = _debugColours;
                TheatreDebugTint.Apply(id, _root);
            }
            // DEBUGTINT end
        }

        // DEBUGTINT begin
        /// <summary>
        /// EVOSIM_THEATRE_DEBUG_TINT: each vertex's colour by what dominates it, in this order: within <see cref="DebugMouthReach"/>
        /// radii of a skin mouth (<c>_mouthRest</c>), black; a largest bone weight under <see cref="DebugBlendShare"/>, white; else its
        /// largest bone's (TheatreDebugTint.SkinBone, by slot). Read at the mesh's own positions (the rest frame). Logs the counts and
        /// each slot's part.
        /// </summary>
        private Color[] DebugColours(long id)
        {
            Vector3[] at = _mesh != null ? _mesh.vertices : new Vector3[0];
            var colours = new Color[at.Length];
            int mouth = 0, blend = 0;
            var bones = new int[_bones.Length];

            for (int v = 0; v < at.Length; v++)
            {
                bool inMouth = false;

                if (_mouthRest != null)
                {
                    foreach (Vector4 m in _mouthRest)
                    {
                        if (m.w > 0f && (at[v] - new Vector3(m.x, m.y, m.z)).magnitude < DebugMouthReach * m.w) inMouth = true;
                    }
                }

                BoneWeight bw = v < _weights.Length ? _weights[v] : new BoneWeight { boneIndex0 = 0, weight0 = 1f };

                if (inMouth)
                {
                    colours[v] = TheatreDebugTint.SkinMouth;
                    mouth++;
                }
                else if (bw.weight0 < DebugBlendShare)
                {
                    colours[v] = TheatreDebugTint.SkinBlend;
                    blend++;
                }
                else
                {
                    int slot = Mathf.Clamp(bw.boneIndex0, 0, bones.Length - 1);
                    colours[v] = TheatreDebugTint.SkinBone[slot % TheatreDebugTint.SkinBone.Length];
                    bones[slot]++;
                }
            }

            var line = new System.Text.StringBuilder();
            line.AppendFormat(CultureInfo.InvariantCulture,
                "[Theatre] debug tint skin: body {0}, {1} vertices: {2} within a mouth's reach (black), {3} in a joint's blend (white);",
                id, at.Length, mouth, blend);

            for (int s = 0; s < bones.Length; s++)
            {
                line.AppendFormat(CultureInfo.InvariantCulture, " slot {0} (part {1}) {2};",
                    s, s < _mergedParts.Length ? _mergedParts[s] : -1, bones[s]);
            }

            Debug.Log(line.ToString());
            return colours;
        }
        // DEBUGTINT end
        /// <summary>Drops the mesh; the objects go with the body's root.</summary>
        public void Release()
        {
            if (_mesh == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(_mesh);
            else UnityEngine.Object.DestroyImmediate(_mesh);
        }

        /// <summary>
        /// The world scale of the skin's bones: the growth they carry times the root's own scale, as one
        /// number (the root is scaled uniformly and the parts not at all).
        /// </summary>
        private float WorldScale()
        {
            for (int j = 0; j < _bones.Length; j++)
            {
                if (_bones[j] != null) return Mathf.Abs(_bones[j].lossyScale.x);
            }

            return _root != null ? Mathf.Abs(_root.lossyScale.x) : 1f;
        }

        /// <summary>
        /// The vertex colours: each merged part's colour as the palette last painted it
        /// (<see cref="TheatrePalette.PaintedColourOf"/>), blended per vertex by its skinning weights,
        /// the softmax of -d/k. The same weights as the motion, so a colour changes where the skin bends
        /// between two parts, and at that falloff a thin link keeps most of its own colour a radius out
        /// from the body it joins; a softer one washed a link's colour into the body's. Blended again only
        /// when some part's colour has moved by more than <see cref="ColourTolerance"/> since the last blend,
        /// so a dim, a corpse or a focus change is redrawn at its next paint and a reserve's drift costs
        /// nothing until it shows.
        /// </summary>
        private void Blend(TheatrePalette palette, long id, Vector4 fallback)
        {
            if (_mesh == null || _weights == null || _colours == null) return;

            bool moved = !_coloured;

            for (int j = 0; j < _mergedParts.Length; j++)
            {
                Vector4 c = fallback;
                if (palette != null && palette.PaintedColourOf(id, _root, _mergedParts[j], out Vector4 painted)) c = painted;

                _nextColours[j] = c;
                if (!moved && Differs(c, _partColours[j])) moved = true;
            }

            if (!moved) return;

            Array.Copy(_nextColours, _partColours, _partColours.Length);

            for (int v = 0; v < _colours.Length; v++)
            {
                BoneWeight bw = _weights[v];
                Vector4 c =
                    bw.weight0 * _partColours[bw.boneIndex0] +
                    bw.weight1 * _partColours[bw.boneIndex1] +
                    bw.weight2 * _partColours[bw.boneIndex2] +
                    bw.weight3 * _partColours[bw.boneIndex3];

                _colours[v] = new Color(c.x, c.y, c.z, 1f);
            }

            _mesh.colors = _colours;
            _coloured = true;
        }

        /// <summary>
        /// Level 3: each merged part's carve character as the palette last painted it (<c>_Carve</c> y, z and w, the
        /// lobe gain, the wrinkle gain and the depth share; the soft-leaf choice from <c>_Velvet</c>, or <c>_LeafLook</c>
        /// on a box drawn as a leaf), blended per vertex by the skinning weights as the colour is, with the soft-tissue
        /// box choice made at meshing. The stream (UV7) is the wrinkles' share of the two gains, the blended depth
        /// share, the tissue weight and the soft-leaf weight; the shader needs the gains as a ratio alone. Blended again
        /// only when a part's character has moved.
        /// </summary>
        private void BlendCarve(TheatrePalette palette, long id, Vector4 fallback, float fallbackVelvet, float fallbackLeaf)
        {
            if (_mesh == null || _weights == null || _character == null || _carveUVs == null) return;

            bool moved = !_carved;

            for (int j = 0; j < _mergedParts.Length; j++)
            {
                Vector4 carve = fallback;
                float velvet = fallbackVelvet;
                float leafLook = fallbackLeaf;

                if (palette != null && palette.PaintedCharacterOf(id, _root, _mergedParts[j], out Vector4 c, out float v, out float l))
                {
                    carve = c;
                    velvet = v;
                    leafLook = l;
                }

                bool soft = velvet > 0.5f || (_leafShaped[j] && leafLook > 0.5f);
                var next = new Vector4(carve.y, carve.z, carve.w, soft ? 1f : 0f);

                _nextCharacter[j] = next;
                if (!moved && next != _character[j]) moved = true;
            }

            if (!moved) return;

            Array.Copy(_nextCharacter, _character, _character.Length);

            for (int v = 0; v < _carveUVs.Length; v++)
            {
                BoneWeight bw = _weights[v];
                Vector4 a = _character[bw.boneIndex0];
                Vector4 b = _character[bw.boneIndex1];
                Vector4 c2 = _character[bw.boneIndex2];
                Vector4 d = _character[bw.boneIndex3];

                float lobe = bw.weight0 * a.x + bw.weight1 * b.x + bw.weight2 * c2.x + bw.weight3 * d.x;
                float wrinkle = bw.weight0 * a.y + bw.weight1 * b.y + bw.weight2 * c2.y + bw.weight3 * d.y;
                float share = bw.weight0 * a.z + bw.weight1 * b.z + bw.weight2 * c2.z + bw.weight3 * d.z;
                float soft = bw.weight0 * a.w + bw.weight1 * b.w + bw.weight2 * c2.w + bw.weight3 * d.w;

                float tissue =
                    bw.weight0 * (_tissueBox[bw.boneIndex0] ? 1f : 0f) +
                    bw.weight1 * (_tissueBox[bw.boneIndex1] ? 1f : 0f) +
                    bw.weight2 * (_tissueBox[bw.boneIndex2] ? 1f : 0f) +
                    bw.weight3 * (_tissueBox[bw.boneIndex3] ? 1f : 0f);

                float gains = lobe + wrinkle;
                float ease = _concavity != null && v < _concavity.Length ? 1f - _concavity[v] : 1f;
                _carveUVs[v] = new Vector4(gains > 1e-6f ? wrinkle / gains : 0f, share * ease, tissue, soft);
                // RIG5 begin
                if (_carve5 != null) _carveUVs[v].y = Mathf.Min(_carveUVs[v].y * _carve5[v].x, _carve5[v].y);
                // RIG5 end
            }

            _mesh.SetUVs(7, _carveUVs);
            _carved = true;
        }

        // skin2:begin
        /// <summary>The flatness of a part, from its smallest half extent over its middle one: none below the first, whole above the second.</summary>
        private const float Skin2FlatLow = 0.1f, Skin2FlatHigh = 0.6f;

        /// <summary>A crease's depth, as this share of the vertex's own half thickness.</summary>
        private const float Skin2CreaseDepthShare = 0.15f;

        /// <summary>
        /// The share of a part's vertices, by half thickness, that its usual thickness is read at: the 80th percentile, so that
        /// a flat part's faces (the most of its vertices) set it and a few thick tips do not.
        /// </summary>
        private const float Skin2ReferencePercentile = 0.8f;

        /// <summary>What a round part keeps of its thinness: a tapering tail is not a margin, so only a flat part takes the whole.</summary>
        private const float Skin2RoundThin = 0.35f;

        /// <summary>Passes of averaging with the neighbours over the mesh, on the thickness and the crease, so a measure is not blotchy.</summary>
        private const int Skin2SmoothPasses = 2;

        private static float Skin2Step(float low, float high, float x)
        {
            float t = Mathf.Clamp01((x - low) / Mathf.Max(high - low, 1e-6f));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// SKIN2's four marks per vertex, for the shader's <c>Varyings.skin2</c>. x: how thin the tissue is against its
        /// part's usual thickness, one minus the vertex's half thickness over the 80th percentile of the half thicknesses of
        /// the vertices that part dominates (zero on a round section, near one at a flat part's margin), times
        /// <see cref="Skin2RoundThin"/> to one by the part's flatness, smoothed over the mesh. y: how near the vertex's rest normal lies to its part's rim, the flat part's flatness times one minus the
        /// normal's cosine with the part's thin axis. z: a joint's crease, one where the two heaviest bones share the vertex
        /// evenly and zero from a quarter-three-quarters split out, smoothed. w: the crease's depth in rest metres, z times
        /// <see cref="Skin2CreaseDepthShare"/> of the vertex's half thickness. Everything is a property of the rest mesh, built
        /// once, so a body that bends or grows keeps its marks on the same tissue.
        /// </summary>
        private static Vector4[] Skin2Marks(
            Vector3[] normals, BoneWeight[] weights, float[] halfThick, List<int> quads, Vector3[] flatAxes, float[] flatness)
        {
            int count = normals.Length;
            int bones = flatAxes.Length;
            var marks = new Vector4[count];
            if (count == 0 || bones == 0) return marks;

            var dominant = new int[count];
            var share = new float[count];
            var lists = new List<float>[bones];
            float overall = 0f;

            for (int v = 0; v < count; v++)
            {
                BoneWeight bw = weights[v];
                float top = bw.weight0, second = 0f;
                int topBone = bw.boneIndex0;

                Skin2Heaviest(bw.weight1, bw.boneIndex1, ref top, ref topBone, ref second);
                Skin2Heaviest(bw.weight2, bw.boneIndex2, ref top, ref topBone, ref second);
                Skin2Heaviest(bw.weight3, bw.boneIndex3, ref top, ref topBone, ref second);

                dominant[v] = Mathf.Clamp(topBone, 0, bones - 1);
                share[v] = top + second > 1e-6f ? second / (top + second) : 0f;
                overall = Mathf.Max(overall, halfThick[v]);

                if (lists[dominant[v]] == null) lists[dominant[v]] = new List<float>();
                lists[dominant[v]].Add(halfThick[v]);
            }

            var reference = new float[bones];

            for (int b = 0; b < bones; b++)
            {
                List<float> mine = lists[b];

                if (mine == null || mine.Count == 0)
                {
                    reference[b] = overall;
                    continue;
                }

                mine.Sort();
                reference[b] = mine[Mathf.Clamp((int)(Skin2ReferencePercentile * (mine.Count - 1)), 0, mine.Count - 1)];
            }

            var thin = new float[count];
            var crease = new float[count];

            for (int v = 0; v < count; v++)
            {
                thin[v] = Mathf.Clamp01(1f - halfThick[v] / Mathf.Max(reference[dominant[v]], 1e-6f)) *
                          Mathf.Lerp(Skin2RoundThin, 1f, flatness[dominant[v]]);
                crease[v] = Mathf.Clamp01((2f * share[v] - 0.5f) / 0.5f);
            }

            // Averaging with the neighbours over the quads' edges (compressed rows, as Concavities builds them).
            var degree = new int[count + 1];
            for (int q = 0; q + 3 < quads.Count; q += 4)
            {
                for (int e = 0; e < 4; e++)
                {
                    degree[quads[q + e] + 1]++;
                    degree[quads[q + (e + 1) % 4] + 1]++;
                }
            }

            for (int v = 0; v < count; v++) degree[v + 1] += degree[v];

            var fill = new int[count];
            var next = new int[degree[count]];
            for (int q = 0; q + 3 < quads.Count; q += 4)
            {
                for (int e = 0; e < 4; e++)
                {
                    int a = quads[q + e];
                    int c = quads[q + (e + 1) % 4];
                    next[degree[a] + fill[a]++] = c;
                    next[degree[c] + fill[c]++] = a;
                }
            }

            var thinNext = new float[count];
            var creaseNext = new float[count];

            for (int pass = 0; pass < Skin2SmoothPasses; pass++)
            {
                for (int v = 0; v < count; v++)
                {
                    float thinSum = 0f, creaseSum = 0f;
                    int neighbours = degree[v + 1] - degree[v];

                    for (int e = degree[v]; e < degree[v + 1]; e++)
                    {
                        thinSum += thin[next[e]];
                        creaseSum += crease[next[e]];
                    }

                    thinNext[v] = neighbours > 0 ? 0.5f * thin[v] + 0.5f * thinSum / neighbours : thin[v];
                    creaseNext[v] = neighbours > 0 ? 0.5f * crease[v] + 0.5f * creaseSum / neighbours : crease[v];
                }

                Array.Copy(thinNext, thin, count);
                Array.Copy(creaseNext, crease, count);
            }

            for (int v = 0; v < count; v++)
            {
                int b = dominant[v];
                float rim = flatness[b] * (1f - Mathf.Abs(Vector3.Dot(normals[v], flatAxes[b])));

                marks[v] = new Vector4(thin[v], Mathf.Clamp01(rim), crease[v], crease[v] * Skin2CreaseDepthShare * halfThick[v]);
            }

            return marks;
        }

        /// <summary>Keeps the heaviest and second heaviest of a vertex's weights as they come.</summary>
        private static void Skin2Heaviest(float weight, int bone, ref float top, ref int topBone, ref float second)
        {
            if (weight > top)
            {
                second = top;
                top = weight;
                topBone = bone;
            }
            else if (weight > second)
            {
                second = weight;
            }
        }

        // skin2:end
        // skin2b:begin
        /// <summary>
        /// SKIN2 level 2's hinge fade, applied to the marks <see cref="Skin2Marks"/> built: the y mark (how far the rest normal
        /// lies from the part's thinnest axis, the band's rim gate) is multiplied by one minus a smoothstep of the vertex's
        /// second heaviest skinning weight over the four weights, from <see cref="Skin2BHingeLow"/> to
        /// <see cref="Skin2BHingeHigh"/>. A vertex that two parts share, a joint's neck, ends with no rim, so the band does not
        /// paint a hinge. The weights are the mesh's own <c>BoneWeight</c>; no channel is added.
        /// </summary>
        private const float Skin2BHingeLow = 0.08f, Skin2BHingeHigh = 0.20f;

        private static void Skin2BOutline(Vector4[] marks, BoneWeight[] weights)
        {
            for (int v = 0; v < marks.Length; v++)
            {
                BoneWeight bw = weights[v];
                float top = bw.weight0, second = 0f;
                int topBone = bw.boneIndex0;

                Skin2Heaviest(bw.weight1, bw.boneIndex1, ref top, ref topBone, ref second);
                Skin2Heaviest(bw.weight2, bw.boneIndex2, ref top, ref topBone, ref second);
                Skin2Heaviest(bw.weight3, bw.boneIndex3, ref top, ref topBone, ref second);

                float total = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
                float share = total > 1e-6f ? second / total : 0f;

                marks[v].y *= 1f - Skin2Step(Skin2BHingeLow, Skin2BHingeHigh, share);
            }
        }
        // skin2b:end
        // BAND4 begin
        /// <summary>The last share of a part's half length, along its longest axis, that the part-end mark rises over.</summary>
        private const float Skin2BEndFraction = 0.15f;

        /// <summary>
        /// A part with no longest axis has no ends: the end mark is multiplied by a smoothstep, from this to the second number,
        /// of one minus the middle half extent over the longest, so a sphere has none and a capsule or a long box has them whole.
        /// </summary>
        private const float Skin2BEndElongLow = 0.1f, Skin2BEndElongHigh = 0.4f;

        /// <summary>
        /// SKIN2_BAND at 4's part-end mark per vertex, for UV0 (x; y is zero): the vertex's distance along its dominant part's
        /// longest axis (the part's rest centre and rotation) over that axis's half length, zero up to
        /// <c>1 - Skin2BEndFraction</c> and rising linearly to one at the end, times the part's elongation, times the same hinge
        /// fade <see cref="Skin2BOutline"/> uses (<see cref="Skin2BHingeLow"/> to <see cref="Skin2BHingeHigh"/> of the
        /// second heaviest skinning weight), so a part's end that is a joint face, buried in a neighbour, takes none. The
        /// dominant part is the heaviest of the four weights, as <see cref="Skin2Marks"/> reads it.
        /// </summary>
        private static Vector2[] Skin2BEnds(
            Vector3[] positions, BoneWeight[] weights, Vector3[] centres, Vector3[] axes, float[] halfLength, float[] elongation)
        {
            var ends = new Vector2[positions.Length];
            int bones = axes.Length;
            if (bones == 0) return ends;

            for (int v = 0; v < positions.Length; v++)
            {
                BoneWeight bw = weights[v];
                float top = bw.weight0, second = 0f;
                int topBone = bw.boneIndex0;

                Skin2Heaviest(bw.weight1, bw.boneIndex1, ref top, ref topBone, ref second);
                Skin2Heaviest(bw.weight2, bw.boneIndex2, ref top, ref topBone, ref second);
                Skin2Heaviest(bw.weight3, bw.boneIndex3, ref top, ref topBone, ref second);

                int b = Mathf.Clamp(topBone, 0, bones - 1);
                float total = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
                float share = total > 1e-6f ? second / total : 0f;
                float along = Mathf.Abs(Vector3.Dot(positions[v] - centres[b], axes[b])) / Mathf.Max(halfLength[b], 1e-6f);
                float cap = Mathf.Clamp01((along - (1f - Skin2BEndFraction)) / Skin2BEndFraction);

                ends[v] = new Vector2(cap * elongation[b] * (1f - Skin2Step(Skin2BHingeLow, Skin2BHingeHigh, share)), 0f);
            }

            return ends;
        }
        // BAND4 end
        /// <summary>
        /// Each vertex's concavity in [0, 1], for easing the carve in the creases between merged lobes. The depth is a
        /// share of the local half thickness, and a crease between two lobes is thick, so it would take the deepest
        /// carve and fill the waist. The signal is geometric, from the mesh alone: for every vertex reached over the
        /// surface within <paramref name="radius"/> (the blend width, k) and further than a quarter of it, the normal
        /// curvature of the chord, 2 (d . n) / |d|^2 with n the outward normal (positive where the surface bends away
        /// from the normal, as in a crease, negative on a convex face); the largest of them, times the radius, is 1 for a
        /// crease whose radius of curvature is the blend width. A smoothstep from 0.15 to 0.8 of that is the concavity.
        /// The largest, not the mean, so a saddle (a waist, convex round and concave along) still reads as a crease.
        /// </summary>
        private static float[] Concavities(Vector3[] positions, Vector3[] normals, List<int> quads, float radius)
        {
            int count = positions.Length;
            var result = new float[count];
            if (count == 0 || !(radius > 1e-6f)) return result;

            // Adjacency (compressed rows) from the quads' four edges.
            var degree = new int[count + 1];
            for (int q = 0; q + 3 < quads.Count; q += 4)
            {
                for (int e = 0; e < 4; e++)
                {
                    degree[quads[q + e] + 1]++;
                    degree[quads[q + (e + 1) % 4] + 1]++;
                }
            }

            for (int v = 0; v < count; v++) degree[v + 1] += degree[v];

            var fill = new int[count];
            var next = new int[degree[count]];
            for (int q = 0; q + 3 < quads.Count; q += 4)
            {
                for (int e = 0; e < 4; e++)
                {
                    int a = quads[q + e];
                    int b = quads[q + (e + 1) % 4];
                    next[degree[a] + fill[a]++] = b;
                    next[degree[b] + fill[b]++] = a;
                }
            }

            var stamp = new int[count];
            var frontier = new List<int>();
            float radiusSquared = radius * radius;
            float nearSquared = 0.0625f * radiusSquared;

            for (int v = 0; v < count; v++)
            {
                int mark = v + 1;
                frontier.Clear();
                frontier.Add(v);
                stamp[v] = mark;
                Vector3 at = positions[v];
                Vector3 normal = normals[v];
                float largest = float.NegativeInfinity;

                for (int f = 0; f < frontier.Count; f++)
                {
                    int u = frontier[f];

                    for (int e = degree[u]; e < degree[u + 1]; e++)
                    {
                        int w = next[e];
                        if (stamp[w] == mark) continue;
                        stamp[w] = mark;

                        Vector3 chord = positions[w] - at;
                        float lengthSquared = chord.sqrMagnitude;
                        if (lengthSquared > radiusSquared) continue;

                        frontier.Add(w);
                        if (lengthSquared < nearSquared) continue;

                        largest = Mathf.Max(largest, 2f * Vector3.Dot(chord, normal) / lengthSquared);
                    }
                }

                if (largest <= float.NegativeInfinity) continue;

                float t = Mathf.Clamp01((largest * radius - 0.15f) / (0.8f - 0.15f));
                result[v] = t * t * (3f - 2f * t);
            }

            return result;
        }

        private static bool Differs(Vector4 a, Vector4 b)
        {
            float scale = Mathf.Max(Mathf.Abs(b.x), Mathf.Max(Mathf.Abs(b.y), Mathf.Abs(b.z)));
            float tolerance = ColourTolerance * scale + ColourFloor;

            return Mathf.Abs(a.x - b.x) > tolerance || Mathf.Abs(a.y - b.y) > tolerance || Mathf.Abs(a.z - b.z) > tolerance;
        }

        // ---------------------------------------------------------------- the thickness

        /// <summary>
        /// Each vertex's half thickness in rest metres: from the vertex along the inward normal (the
        /// negative of the field's gradient), in steps of a quarter cell, to where the field turns positive
        /// again, the crossing placed between the last two steps by their values, or to the body's largest
        /// extent; half that distance. The caller then caps it at the smallest plain half extent of the vertex's
        /// dominant bone, since on a flat body's rim the chord runs across the width, not through the sheet. The field is the sampled grid's, read trilinearly, which is the field
        /// the surface was cut from and a few hundred times cheaper than the field itself. Never under half a
        /// step, the finest the march resolves.
        /// </summary>
        private static float[] HalfThicknesses(
            Vector3[] positions, Vector3[] normals, float[] samples, Vector3 origin, float cell,
            int nx, int ny, int nz, float cap)
        {
            var result = new float[positions.Length];
            float step = ThicknessStep * cell;
            float floor = 0.5f * step;

            for (int v = 0; v < positions.Length; v++)
            {
                float chord = MarchChord(samples, origin, cell, nx, ny, nz, positions[v], -normals[v], step, cap);

                result[v] = Mathf.Max(floor, 0.5f * chord);
            }

            return result;
        }

        // THICKCAP begin
        /// <summary>
        /// One march of <see cref="HalfThicknesses"/>: from <paramref name="p"/> along <paramref name="dir"/>, in steps of
        /// <paramref name="step"/>, to where the field turns positive, the crossing placed between the last two steps by their
        /// values, or to <paramref name="cap"/>. The distance, unfloored.
        /// </summary>
        private static float MarchChord(
            float[] samples, Vector3 origin, float cell, int nx, int ny, int nz, Vector3 p, Vector3 dir, float step, float cap)
        {
            float lastT = 0f;
            float lastF = Mathf.Min(0f, GridAt(samples, origin, cell, nx, ny, nz, p));
            float chord = cap;

            for (float t = step; t <= cap; t += step)
            {
                float f = GridAt(samples, origin, cell, nx, ny, nz, p + dir * t);

                if (f >= 0f)
                {
                    float share = lastF < 0f ? lastF / (lastF - f) : 0f;
                    chord = lastT + (t - lastT) * share;
                    break;
                }

                lastT = t;
                lastF = f;
            }

            return chord;
        }

        /// <summary>
        /// Half the chord through the field along an axis, both ways from the vertex (the sum of the two marches), floored at half
        /// a step as the normal chord is. From a vertex on the surface the march outward ends at once, so this is the sheet's
        /// own thickness along the axis; where the axis grazes the surface it comes out short, and the carve fades there.
        /// </summary>
        private static float AxisHalfThickness(
            float[] samples, Vector3 origin, float cell, int nx, int ny, int nz, Vector3 p, Vector3 axis, float cap)
        {
            float step = ThicknessStep * cell;
            float floor = 0.5f * step;
            float chord = MarchChord(samples, origin, cell, nx, ny, nz, p, axis, step, cap) +
                          MarchChord(samples, origin, cell, nx, ny, nz, p, -axis, step, cap);

            return Mathf.Max(floor, 0.5f * chord);
        }
        // THICKCAP end

        /// <summary>The sampled field at a point, trilinear; outside the grid, outside the body (a cell).</summary>
        private static float GridAt(float[] f, Vector3 origin, float cell, int nx, int ny, int nz, Vector3 p)
        {
            float gx = (p.x - origin.x) / cell;
            float gy = (p.y - origin.y) / cell;
            float gz = (p.z - origin.z) / cell;

            if (!(gx >= 0f && gy >= 0f && gz >= 0f && gx <= nx - 1 && gy <= ny - 1 && gz <= nz - 1)) return cell;

            int x0 = Mathf.Min((int)gx, nx - 2);
            int y0 = Mathf.Min((int)gy, ny - 2);
            int z0 = Mathf.Min((int)gz, nz - 2);
            float tx = gx - x0, ty = gy - y0, tz = gz - z0;

            int At(int x, int y, int z) => x + nx * (y + ny * z);

            float c00 = Mathf.Lerp(f[At(x0, y0, z0)], f[At(x0 + 1, y0, z0)], tx);
            float c10 = Mathf.Lerp(f[At(x0, y0 + 1, z0)], f[At(x0 + 1, y0 + 1, z0)], tx);
            float c01 = Mathf.Lerp(f[At(x0, y0, z0 + 1)], f[At(x0 + 1, y0, z0 + 1)], tx);
            float c11 = Mathf.Lerp(f[At(x0, y0 + 1, z0 + 1)], f[At(x0 + 1, y0 + 1, z0 + 1)], tx);

            return Mathf.Lerp(Mathf.Lerp(c00, c10, ty), Mathf.Lerp(c01, c11, ty), tz);
        }

        // ---------------------------------------------------------------- the field

        private const int KindBox = 0;
        private const int KindCapsule = 1;
        private const int KindEllipsoid = 2;
        private const int KindBall = 3;
        private const int KindDrawnBox = 4;
        // FAMILYSKIN begin
        private const int KindFamily = 5;
        // FAMILYSKIN end
        // NOBOX begin
        private const int KindSuperEllipsoid = 6;
        // NOBOX end

        private struct Primitive
        {
            public int Kind;

            public float Cx, Cy, Cz;

            // The part's own axes in the rest frame: local x, y and z.
            public float Xx, Xy, Xz, Yx, Yy, Yz, Zx, Zy, Zz;

            // Box: the half extents less the rounding. Capsule: radius and half span. Ellipsoid: the
            // semi-axes. Ball: radius.
            public float A, B, C;

            public float Round;
            // NOBOX begin
            // EVOSIM_THEATRE_NO_BOX_SKIN_EXPONENT: the exponent of a KindSuperEllipsoid, whose A, B, C are its semi-axes; zero otherwise.
            public float Exp;
            // NOBOX end

            // Capsule: the palette's squash across its axis, as the factors that undo it on x and z, and
            // the smaller squash, which keeps the squashed field a bound on the distance. One and one when
            // it is drawn round.
            public float UnsquashX, UnsquashZ, Squash;

            // The drawn box's constants (KindDrawnBox), or null.
            public DrawnBox Drawn;
            // FAMILYSKIN begin
            // EVOSIM_THEATRE_FAMILY_SKIN: a family part's solid (KindFamily, and ColKind 3 at levels 5 and 6); null otherwise.
            public TheatreFamilySkin.Solid Fam;
            // FAMILYSKIN end
            // PADDLE begin
            // EVOSIM_THEATRE_PADDLE_LIMB: this link's paddle, whose field Distance reads in place of the kinds below; null otherwise.
            public PaddleShape Paddle;
            // PADDLE end
            // RIG5 begin
            // Level 5: the collider, the shape the world simulates, in the part's own frame (ColliderOf5): ColKind 0 a box of half
            // extents ColA, ColB, ColC, whose edges along a joint's face normal round by ColRound where it eases to it; 1 a capsule
            // of radius ColA and half span ColB on local y; 2 a sphere of radius ColA; 3 (EVOSIM_THEATRE_FAMILY_SKIN) a family
            // part's solid, Fam's field, with ColA, ColB, ColC its half extents for the footprint of a joint's face.
            public int ColKind;
            public float ColA, ColB, ColC, ColRound;
            // RIG5 end

            // The rest frame box round the primitive, and the same grown by the cull margin.
            public Vector3 Low, High;
            public float CullLowX, CullLowY, CullLowZ, CullHighX, CullHighY, CullHighZ;
        }

        /// <summary>The palette's settings and one part's seeds, for <see cref="DrawnBox"/>.</summary>
        private struct DrawnLook
        {
            public float Taper, Bend;
            public bool Organic, Tissue;
            public float Seed, Individual;
            // RIG5 begin
            // Level 5: EVOSIM_THEATRE_SKIN_FILL (0 below level 5).
            public float Fill5;
            // RIG5 end
        }

        /// <summary>
        /// The shape one part is drawn as. A sphere part is the ellipsoid it is drawn as. A capsule is its
        /// collider squashed across by the palette's aspect (TheatrePalette.Aspect) once its skin dresses
        /// the body, and one capsule shorter than its radius the single ellipsoid the palette draws it as
        /// (NearlyASphere, not under the raw shapes). A box the palette shapes is the rounded cube under
        /// ShapeBox (<see cref="DrawnBox"/>); a box drawn as a leaf, under the raw shapes or with no palette,
        /// the collider's rounded box as before.
        /// </summary>
        private static Primitive PrimitiveOf(
            PartShape shape, PhenotypePart part, Vector3 h, Vector3 centre, Quaternion rotation,
            bool dressed, bool shaped, DrawnLook look, out bool drawnBox, bool ellipsoidBox = false)
        {
            drawnBox = false;

            var p = new Primitive
            {
                Cx = centre.x, Cy = centre.y, Cz = centre.z,
                UnsquashX = 1f, UnsquashZ = 1f, Squash = 1f,
            };

            Vector3 ax = rotation * Vector3.right;
            Vector3 ay = rotation * Vector3.up;
            Vector3 az = rotation * Vector3.forward;

            p.Xx = ax.x; p.Xy = ax.y; p.Xz = ax.z;
            p.Yx = ay.x; p.Yy = ay.y; p.Yz = ay.z;
            p.Zx = az.x; p.Zy = az.y; p.Zz = az.z;

            // FAMILYSKIN begin
            // EVOSIM_THEATRE_FAMILY_SKIN: a family part is its own kind, at its true half extents (the collider's, not the palette's
            // aspect), with no drawn-box taper or bend. A reference half extent of zero keeps the box.
            if (TheatreFamilySkin.Enabled && shape is SuperellipsoidShape)
            {
                TheatreFamilySkin.Solid solid = TheatreFamilySkin.SolidOf(part, h);

                if (solid != null)
                {
                    p.Kind = KindFamily;
                    p.Fam = solid;
                    p.A = (float)solid.A;
                    p.B = (float)solid.B;
                    p.C = (float)solid.C;

                    // The rest frame box of the box that holds the solid, taper and shift included, turned by the rotation.
                    Vector3 solidExtent = solid.Extent;
                    var solidReach = new Vector3(
                        Mathf.Abs(ax.x) * solidExtent.x + Mathf.Abs(ay.x) * solidExtent.y + Mathf.Abs(az.x) * solidExtent.z,
                        Mathf.Abs(ax.y) * solidExtent.x + Mathf.Abs(ay.y) * solidExtent.y + Mathf.Abs(az.y) * solidExtent.z,
                        Mathf.Abs(ax.z) * solidExtent.x + Mathf.Abs(ay.z) * solidExtent.y + Mathf.Abs(az.z) * solidExtent.z);

                    p.Low = centre - solidReach;
                    p.High = centre + solidReach;

                    TheatreFamilySkin.LogFirst(solid, part, new Float3(h.x, h.y, h.z));
                    return p;
                }
            }
            // FAMILYSKIN end

            Vector3 extent;
            Float3 hf = new Float3(h.x, h.y, h.z);

            switch (shape)
            {
                case SphereShape _:
                {
                    // The genome's three half extents, the longest at the collider's radius
                    // (TheatrePalette.Aspect, on the sphere of diameter 2r that Plan lays).
                    float r = SphereShape.Radius(hf);
                    float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                    float scale = longest > 0f ? r / longest : 1f;

                    p.Kind = KindEllipsoid;
                    p.A = Mathf.Max(1e-6f, h.x * scale);
                    p.B = Mathf.Max(1e-6f, h.y * scale);
                    p.C = Mathf.Max(1e-6f, h.z * scale);
                    extent = new Vector3(p.A, p.B, p.C);
                    break;
                }

                case CapsuleShape _:
                {
                    float r = CapsuleShape.Radius(hf);
                    float span = CapsuleShape.HalfSpan(hf);

                    // The palette's squash across the axis (TheatrePalette.Aspect): x and z over the larger
                    // of the two, on the shaft and on both caps, y left alone.
                    float across = Mathf.Max(h.x, h.z);
                    float sx = dressed && across > 0f ? h.x / across : 1f;
                    float sz = dressed && across > 0f ? h.z / across : 1f;

                    if (shaped && span < r)
                    {
                        // NearlyASphere: one cap at the part's centre draws the whole part, stretched along
                        // the axis to the genome's own half extent there.
                        p.Kind = KindEllipsoid;
                        p.A = Mathf.Max(1e-6f, r * sx);
                        p.B = Mathf.Max(1e-6f, Mathf.Max(h.y, r));
                        p.C = Mathf.Max(1e-6f, r * sz);
                        extent = new Vector3(p.A, p.B, p.C);
                        break;
                    }

                    p.Kind = KindCapsule;
                    p.A = r;
                    p.B = span;
                    p.UnsquashX = sx > 1e-6f ? 1f / sx : 1f;
                    p.UnsquashZ = sz > 1e-6f ? 1f / sz : 1f;
                    p.Squash = Mathf.Min(sx, sz);
                    extent = new Vector3(r * sx, span + r, r * sz);
                    break;
                }

                default:
                {
                    float small = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
                    float large = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                    float cubicness = large > 1e-6f ? small / large : 1f;

                    // A box the palette shapes and does not draw as a leaf: the rounded cube it swaps in
                    // (RoundedFor's bucket of the pillow for the part's cubicness) under ShapeBox. Under the
                    // raw shapes it swaps the engine's cube back in, and the collider's rounded box below stands.
                    // NOBOX begin
                    // EVOSIM_THEATRE_NO_BOX: a box that is the whole skin (level 1) or any box in a skin (level 2) is the ellipsoid
                    // inside it, so the body reads as one smooth animal, not a pillow. It takes no organ seat and no tissue carve.
                    if (ellipsoidBox && shaped && !TheatrePalette.DrawnAsLeaf(part, 2f * h))
                    {
                        extent = NoBoxSolid(ref p, h);
                        break;
                    }

                    // Level 2: a flat box that DrawnAsLeaf names is the lens of its half extents as well, not a tile with flat faces.
                    // A photosynthetic one is the leaf lamina, which is exempt, and stays the rounded box below.
                    if (ellipsoidBox && TheatreNoBox.EllipsoidAll && shaped && part.CellTypeId != CellTypeIds.Photosynthetic)
                    {
                        extent = NoBoxSolid(ref p, h);
                        break;
                    }
                    // NOBOX end

                    if (shaped && !TheatrePalette.DrawnAsLeaf(part, 2f * h))
                    {
                        float pillow = Mathf.Clamp(Bucket(TheatreMeshes.PillowFor(cubicness)), 0f, 0.5f);

                        p.Kind = KindDrawnBox;
                        p.Drawn = new DrawnBox(h, pillow, look);
                        extent = h;
                        drawnBox = true;
                        break;
                    }

                    // Rounded as the drawn cube is: TheatreMeshes' pillow for the part's cubicness, as a
                    // share of the smallest half extent, so the rounded box is inside the box.
                    float round = Mathf.Clamp(TheatreMeshes.PillowFor(cubicness), 0f, 0.5f) * small;

                    p.Kind = KindBox;
                    p.Round = round;
                    p.A = Mathf.Max(0f, h.x - round);
                    p.B = Mathf.Max(0f, h.y - round);
                    p.C = Mathf.Max(0f, h.z - round);
                    extent = h;
                    break;
                }
            }

            // The rest frame box of the local box `extent` turned by the rotation.
            var reach = new Vector3(
                Mathf.Abs(ax.x) * extent.x + Mathf.Abs(ay.x) * extent.y + Mathf.Abs(az.x) * extent.z,
                Mathf.Abs(ax.y) * extent.x + Mathf.Abs(ay.y) * extent.y + Mathf.Abs(az.y) * extent.z,
                Mathf.Abs(ax.z) * extent.x + Mathf.Abs(ay.z) * extent.y + Mathf.Abs(az.z) * extent.z);

            p.Low = centre - reach;
            p.High = centre + reach;
            return p;
        }

        // NOBOX begin
        /// <summary>
        /// The solid a box is inside a skin under EVOSIM_THEATRE_NO_BOX, set into <paramref name="p"/>: the ellipsoid of the box's
        /// half extents, or with EVOSIM_THEATRE_NO_BOX_SKIN_EXPONENT over 2 the superellipsoid of that exponent on the same semi-axes.
        /// Returns the half extents of the box that holds it, which are the semi-axes.
        /// </summary>
        private static Vector3 NoBoxSolid(ref Primitive p, Vector3 h)
        {
            p.Kind = KindEllipsoid;
            p.A = Mathf.Max(1e-6f, Mathf.Abs(h.x));
            p.B = Mathf.Max(1e-6f, Mathf.Abs(h.y));
            p.C = Mathf.Max(1e-6f, Mathf.Abs(h.z));

            if (TheatreNoBox.SkinSuperellipsoid)
            {
                p.Kind = KindSuperEllipsoid;
                p.Exp = TheatreNoBox.SkinExponent;
            }

            return new Vector3(p.A, p.B, p.C);
        }
        // NOBOX end

        /// <summary>TheatreMeshes.Bucket: the pillow in whole steps of 4 %, the cube the palette swaps in.</summary>
        private static float Bucket(float pillow) => Mathf.Clamp(Mathf.RoundToInt(pillow * 100f / 4f) * 4, 0, 50) / 100f;

        // SEAT begin
        /// <summary>
        /// The seats of one drawn box, in the part's own frame and metres: a point where another part stands on the box (a
        /// joint anchor) and a radius, a share of that part's smallest half extent.
        /// </summary>
        private sealed class SeatSet
        {
            public readonly List<Vector4> Seat = new List<Vector4>();
            public readonly List<int> Other = new List<int>();

            public int Count => Seat.Count;
            // ORGANSEAT begin
            // ORGAN_SEAT's cones, null when there is none: per cone the anchor and its radius at the face (xyz, w) in Cone, and in
            // ConeAxis the face axis (x, 0 to 2), the inward sign along it (y), the depth of full weight (z) and the fade's width (w).
            public List<Vector4> Cone;
            public List<Vector4> ConeAxis;

            public void AddCone(Vector3 anchor, float radius, int axis, float inward, float depth, float band)
            {
                if (Cone == null)
                {
                    Cone = new List<Vector4>();
                    ConeAxis = new List<Vector4>();
                }

                Cone.Add(new Vector4(anchor.x, anchor.y, anchor.z, radius));
                ConeAxis.Add(new Vector4(axis, inward, depth, band));
            }

            /// <summary>
            /// One inside a cone: within its radius of the face axis through the anchor, a radius that widens by
            /// <see cref="OrganSeatSpread"/> for each metre in, and no deeper than its depth; falling by a smoothstep to 0 over the
            /// fade's width beyond its side or its depth. The largest over the cones.
            /// </summary>
            public float ConeWeight(float x, float y, float z)
            {
                float w = 0f;

                for (int i = 0; i < Cone.Count; i++)
                {
                    Vector4 s = Cone[i];
                    Vector4 a = ConeAxis[i];
                    float dx = x - s.x, dy = y - s.y, dz = z - s.z;
                    int axis = (int)a.x;
                    float off = axis == 0 ? dx : axis == 1 ? dy : dz;
                    float along = a.y * off;
                    if (along >= a.z + a.w) continue;

                    float across = Mathf.Sqrt(Mathf.Max(0f, dx * dx + dy * dy + dz * dz - off * off));
                    float reach = s.w + OrganSeatSpread * Mathf.Max(0f, along);
                    float cone = ConeFade(across - reach, a.w) * ConeFade(along - a.z, a.w);
                    if (cone >= 1f) return 1f;

                    w = Mathf.Max(w, cone);
                }

                return w;
            }

            /// <summary>One at or under 0, falling by a smoothstep to 0 at <paramref name="band"/>.</summary>
            private static float ConeFade(float over, float band)
            {
                if (over <= 0f) return 1f;
                if (over >= band) return 0f;

                float t = over / band;
                return 1f - t * t * (3f - 2f * t);
            }
            // ORGANSEAT end

            /// <summary>One at a seat and inside its radius r, falling by a smoothstep to 0 at 2r; the largest over the seats.</summary>
            public float Weight(float x, float y, float z)
            {
                float w = 0f;
                // ORGANSEAT begin
                if (Cone != null)
                {
                    w = ConeWeight(x, y, z);
                    if (w >= 1f) return 1f;
                }
                // ORGANSEAT end

                for (int i = 0; i < Seat.Count; i++)
                {
                    Vector4 s = Seat[i];
                    float dx = x - s.x, dy = y - s.y, dz = z - s.z, r = s.w;
                    float d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 >= 4f * r * r) continue;

                    float d = Mathf.Sqrt(d2);
                    if (d <= r) return 1f;

                    float t = (d - r) / r;
                    w = Mathf.Max(w, 1f - t * t * (3f - 2f * t));
                }

                return w;
            }
        }

        /// <summary>
        /// The seats of part <paramref name="index"/>: every joint anchor on it (each child's ParentAnchorLocal) and its own
        /// ChildAnchorLocal unless it is the root, each at <paramref name="seat"/> times the smallest half extent of the part on
        /// the joint's far side. The anchors are in the part's own frame and metres as the part list holds them, at the adult's
        /// size times <paramref name="unscale"/> as the rest of Build reads them. Null when there is none.
        /// </summary>
        private static SeatSet SeatsOf(Phenotype adult, int index, float unscale, float seat)
        {
            var set = new SeatSet();
            int count = adult.PartCount;

            for (int i = 0; i < count; i++)
            {
                if (adult.Parts[i].ParentIndex != index) continue;
                AddSeat(set, adult, i, adult.Parts[i].ParentAnchorLocal.ToVector3() * unscale, unscale, seat);
            }

            PhenotypePart part = adult.Parts[index];
            if (part.ParentIndex >= 0 && part.ParentIndex < count)
                AddSeat(set, adult, part.ParentIndex, part.ChildAnchorLocal.ToVector3() * unscale, unscale, seat);

            return set.Count > 0 ? set : null;
        }

        private static void AddSeat(SeatSet set, Phenotype adult, int other, Vector3 anchor, float unscale, float seat)
        {
            Vector3 h = Abs(adult.Parts[other].HalfExtents.ToVector3()) * unscale;
            float r = seat * Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            if (!(r > 1e-6f)) return;

            set.Seat.Add(new Vector4(anchor.x, anchor.y, anchor.z, r));
            set.Other.Add(other);
        }

        /// <summary>
        /// The probe for one drawn box: a line per seat (at most 8 for the body) with the drop, the distance from the anchor to
        /// the drawn surface along the inward face normal, with the seat off and with it on at the dial's value (2 when the
        /// dial is off). Changes nothing drawn: the box's seats are put back as they were.
        /// </summary>
        private static void SeatProbeBox(List<string> lines, long id, Phenotype adult, int index, float unscale, DrawnBox box)
        {
            if (lines.Count >= 8) return;

            float on = SeatDial > 0f ? SeatDial : 2f;
            SeatSet set = SeatsOf(adult, index, unscale, on);
            if (set == null) return;

            SeatSet held = box.SwapSeats(null);
            var off = new float[set.Count];
            for (int i = 0; i < set.Count; i++) off[i] = box.Drop(set.Seat[i].x, set.Seat[i].y, set.Seat[i].z);

            box.SwapSeats(set);
            for (int i = 0; i < set.Count && lines.Count < 8; i++)
            {
                Vector4 a = set.Seat[i];
                float drop = box.Drop(a.x, a.y, a.z);
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] skin seat probe: body {0} part {1} to {2}, anchor ({3:0.0000} {4:0.0000} {5:0.0000}) m, " +
                    "drop off {6:0.0000} m, on {7:0.0000} m (seat {8:0.###})",
                    id, index, set.Other[i], a.x, a.y, a.z, off[i], drop, on));
            }

            box.SwapSeats(held);
        }

        // ORGANSEAT begin
        /// <summary>
        /// The seats of part <paramref name="index"/>'s drawn box under SEAT_MARKER or ORGAN_SEAT: SKIN_SEAT's balls as
        /// <see cref="SeatsOf"/> makes them while its dial is on, less any at a joint to a part standing apart from the skin; and,
        /// under ORGAN_SEAT, a cone at each child standing apart on a Fixed joint, its radius at the face the child's largest half
        /// reach across the face (<see cref="OrganSeatFootprint"/>) and its depth the box's own drop at the anchor with no seat.
        /// Marks each child given a cone in <paramref name="seated"/>. Null when there is no seat.
        /// </summary>
        private static SeatSet OrganSeatsOf(Phenotype adult, int index, float unscale, bool[] merged, DrawnBox box, bool[] seated)
        {
            var set = new SeatSet();
            int count = adult.PartCount;

            if (SeatDial > 0f)
            {
                for (int i = 0; i < count; i++)
                {
                    if (adult.Parts[i].ParentIndex != index || !merged[i]) continue;
                    AddSeat(set, adult, i, adult.Parts[i].ParentAnchorLocal.ToVector3() * unscale, unscale, SeatDial);
                }

                PhenotypePart own = adult.Parts[index];
                if (own.ParentIndex >= 0 && own.ParentIndex < count && merged[own.ParentIndex])
                    AddSeat(set, adult, own.ParentIndex, own.ChildAnchorLocal.ToVector3() * unscale, unscale, SeatDial);
            }

            if (OrganSeat && seated != null)
            {
                Vector3 h = Abs(adult.Parts[index].HalfExtents.ToVector3()) * unscale;
                Quaternion toParent = Quaternion.Inverse(Normalised(adult.Parts[index].Rotation.ToQuaternion()));
                SeatSet held = box.SwapSeats(null);

                for (int i = 0; i < count; i++)
                {
                    PhenotypePart child = adult.Parts[i];
                    if (child.ParentIndex != index || merged[i] || child.JointType != JointType.Fixed) continue;

                    // The face, as DrawnBox.Drop picks it, and the box's own drop there with no seat: the cone's depth.
                    Vector3 anchor = child.ParentAnchorLocal.ToVector3() * unscale;
                    float rx = Mathf.Abs(anchor.x) / Mathf.Max(1e-9f, h.x);
                    float ry = Mathf.Abs(anchor.y) / Mathf.Max(1e-9f, h.y);
                    float rz = Mathf.Abs(anchor.z) / Mathf.Max(1e-9f, h.z);
                    int axis = rx >= ry && rx >= rz ? 0 : ry >= rz ? 1 : 2;
                    float drop = box.Drop(anchor.x, anchor.y, anchor.z);
                    if (!(drop > 0f)) continue;

                    // The child's half reach across the face: its box turned into the parent's frame.
                    Vector3 ch = Abs(child.HalfExtents.ToVector3()) * unscale;
                    Quaternion turn = toParent * Normalised(child.Rotation.ToQuaternion());
                    Vector3 ex = turn * Vector3.right, ey = turn * Vector3.up, ez = turn * Vector3.forward;
                    float footprint = 0f;

                    for (int c = 0; c < 3; c++)
                    {
                        if (c == axis) continue;
                        footprint = Mathf.Max(footprint, Mathf.Abs(ex[c]) * ch.x + Mathf.Abs(ey[c]) * ch.y + Mathf.Abs(ez[c]) * ch.z);
                    }

                    float radius = OrganSeatFootprint * footprint;
                    if (!(radius > 1e-6f)) continue;

                    set.AddCone(anchor, radius, axis, anchor[axis] >= 0f ? -1f : 1f, drop, OrganSeatBand * radius);
                    seated[i] = true;
                }

                box.SwapSeats(held);
            }

            return set.Count > 0 || set.Cone != null ? set : null;
        }
        // ORGANSEAT end
        // SEATPROBE begin
        /// <summary>
        /// The nose seat probe's row for a part standing apart from the skin whose parent is in it: the line through the joint
        /// anchor along the inward normal of the parent's box face it sits on (the axis of the largest anchor over half extent, as
        /// <see cref="DrawnBox.Drop"/> picks it), in the parent's rest frame and metres, and what lies on it.
        /// </summary>
        private sealed class SeatRow
        {
            public int Part, Parent, Slot;
            public Vector3 Anchor, Inward;
            public float Smallest, Depth;
            public bool Drawn;
            public float DropBare = -1f, DropBuilt = -1f;
            public float BackIn = float.NaN;
            public readonly List<float> Crossings = new List<float>();
            public string Type, Shape, Joint;
        }

        /// <summary>Two crossings nearer than this, in metres, are one: a tenth of a millimetre, far under any cell the skin is meshed on.</summary>
        private const float SeatProbeTie = 1e-4f;

        /// <summary>
        /// The probe's rows, measured on the built mesh at rest: null when no part stands apart beside the skin. Reads only: a
        /// drawn box's seats are put back as they were.
        /// </summary>
        private static List<SeatRow> SeatRowsOf(
            Phenotype adult, float unscale, bool[] merged, List<int> mergedParts, Vector3[] centres, Quaternion[] rotations,
            DrawnBox[] boxes, Vector3[] positions, int[] triangles)
        {
            List<SeatRow> rows = null;
            int count = adult.PartCount;

            for (int i = 0; i < count; i++)
            {
                if (merged[i]) continue;

                PhenotypePart part = adult.Parts[i];
                int parent = part.ParentIndex;
                if (parent < 0 || parent >= count || !merged[parent]) continue;

                int slot = mergedParts.IndexOf(parent);
                if (slot < 0) continue;

                PhenotypePart host = adult.Parts[parent];
                Vector3 h = Abs(host.HalfExtents.ToVector3()) * unscale;
                Vector3 anchor = part.ParentAnchorLocal.ToVector3() * unscale;
                float rx = Mathf.Abs(anchor.x) / Mathf.Max(1e-9f, h.x);
                float ry = Mathf.Abs(anchor.y) / Mathf.Max(1e-9f, h.y);
                float rz = Mathf.Abs(anchor.z) / Mathf.Max(1e-9f, h.z);
                int axis = rx >= ry && rx >= rz ? 0 : ry >= rz ? 1 : 2;
                Vector3 inward = Vector3.zero;
                inward[axis] = anchor[axis] >= 0f ? -1f : 1f;
                Vector3 ph = Abs(part.HalfExtents.ToVector3()) * unscale;

                var row = new SeatRow
                {
                    Part = i,
                    Parent = parent,
                    Slot = slot,
                    Anchor = anchor,
                    Inward = inward,
                    Smallest = Mathf.Min(ph.x, Mathf.Min(ph.y, ph.z)),
                    Depth = 2f * h[axis],
                    Type = part.CellTypeId,
                    Shape = host.ShapeId,
                    Joint = part.JointType.ToString(),
                };

                DrawnBox box = boxes != null && slot < boxes.Length ? boxes[slot] : null;
                if (box != null)
                {
                    row.Drawn = true;
                    row.DropBuilt = box.Drop(anchor.x, anchor.y, anchor.z);
                    SeatSet held = box.SwapSeats(null);
                    row.DropBare = box.Drop(anchor.x, anchor.y, anchor.z);
                    box.SwapSeats(held);
                }

                // The line in the body's rest frame, t metres in from the face; it starts the part's largest extent out.
                Vector3 origin = centres[slot] + rotations[slot] * anchor;
                Vector3 dir = rotations[slot] * inward;
                row.BackIn = BoxLeave(origin, dir, part.Position.ToVector3() * unscale, Normalised(part.Rotation.ToQuaternion()), ph);
                LineCrossings(positions, triangles, origin, dir, -2f * Mathf.Max(ph.x, Mathf.Max(ph.y, ph.z)), row.Depth, row.Crossings);

                if (rows == null) rows = new List<SeatRow>();
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>Where a line leaves a box going along it (the far end of the slab interval), or NaN when it misses the box.</summary>
        private static float BoxLeave(Vector3 origin, Vector3 dir, Vector3 centre, Quaternion turn, Vector3 half)
        {
            Quaternion back = Quaternion.Inverse(turn);
            Vector3 o = back * (origin - centre);
            Vector3 d = back * dir;
            float enter = float.NegativeInfinity, leave = float.PositiveInfinity;

            for (int c = 0; c < 3; c++)
            {
                if (Mathf.Abs(d[c]) < 1e-9f)
                {
                    if (Mathf.Abs(o[c]) > half[c]) return float.NaN;
                    continue;
                }

                float t1 = (-half[c] - o[c]) / d[c];
                float t2 = (half[c] - o[c]) / d[c];
                enter = Mathf.Max(enter, Mathf.Min(t1, t2));
                leave = Mathf.Min(leave, Mathf.Max(t1, t2));
            }

            return enter <= leave ? leave : float.NaN;
        }

        /// <summary>
        /// Every t from <paramref name="low"/> to <paramref name="high"/> at which the line origin + t dir crosses a triangle of the
        /// mesh, in order, crossings nearer than <see cref="SeatProbeTie"/> taken as one (a line through a shared edge meets both).
        /// </summary>
        private static void LineCrossings(
            Vector3[] positions, int[] triangles, Vector3 origin, Vector3 dir, float low, float high, List<float> into)
        {
            var found = new List<float>();

            for (int q = 0; q + 2 < triangles.Length; q += 3)
            {
                Vector3 a = positions[triangles[q]];
                Vector3 e1 = positions[triangles[q + 1]] - a, e2 = positions[triangles[q + 2]] - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-14f) continue;

                float inv = 1f / det;
                Vector3 o = origin - a;
                float u = Vector3.Dot(o, p) * inv;
                if (u < 0f || u > 1f) continue;

                Vector3 w = Vector3.Cross(o, e1);
                float v = Vector3.Dot(dir, w) * inv;
                if (v < 0f || u + v > 1f) continue;

                float t = Vector3.Dot(e2, w) * inv;
                if (t >= low && t <= high) found.Add(t);
            }

            found.Sort();
            for (int i = 0; i < found.Count; i++)
            {
                if (into.Count == 0 || found[i] - into[into.Count - 1] > SeatProbeTie) into.Add(found[i]);
            }
        }

        /// <summary>
        /// The nose seat probe, once per skin, at its first dress: a <c>nose seat</c> line per row (<see cref="SeatRowsOf"/>), with
        /// the depths against the face (negative behind it), the gap (the stretches of the line outside the skin from the part's
        /// back face to the body, the line taken to start outside), the seat, and every knuckle and bridge the palette has at the
        /// joint with its span along the line in rest metres.
        /// </summary>
        private void LogSeatRows(TheatrePalette palette, long id)
        {
            _seatRowsSaid = true;
            bool found = palette != null && palette.NecksOf(id, _root, _necks, _neckJoints);
            CultureInfo ci = CultureInfo.InvariantCulture;

            string Against(float metresIn) => (-metresIn).ToString("+0.0000;-0.0000;0.0000", ci) + " m";

            foreach (SeatRow row in _seatRows)
            {
                float back = float.IsNaN(row.BackIn) ? 0f : row.BackIn;
                var gaps = new List<Vector2>();
                float lastOut = float.NegativeInfinity;
                bool inside = false;

                foreach (float t in row.Crossings)
                {
                    if (!inside && t > back) gaps.Add(new Vector2(Mathf.Max(lastOut, back), t));
                    if (inside) lastOut = t;
                    inside = !inside;
                }

                if (!inside) gaps.Add(new Vector2(Mathf.Max(lastOut, back), row.Depth));

                float gap = 0f;
                var gapText = new List<string>();
                foreach (Vector2 g in gaps)
                {
                    gap += g.y - g.x;
                    gapText.Add(string.Format(ci, "{0:0.0000} to {1:0.0000}", g.x, g.y));
                }

                var crossText = new List<string>();
                foreach (float t in row.Crossings) crossText.Add(t.ToString("0.0000", ci));

                var drawn = new List<string>();
                Transform bone = row.Slot < _bones.Length ? _bones[row.Slot] : null;

                for (int i = 0; found && i < _necks.Count; i++)
                {
                    if (_neckJoints[i] != row.Part || _necks[i] == null) continue;

                    Transform neck = _necks[i];
                    var r = neck.GetComponent<MeshRenderer>();
                    bool on = r != null && r.enabled && neck.gameObject.activeInHierarchy;
                    string name = neck.name.ToLowerInvariant();

                    if (bone == null)
                    {
                        drawn.Add(name + (on ? ", drawn" : ", hidden"));
                        continue;
                    }

                    // The unit sphere's three radii, carried into the parent's rest frame and onto the line.
                    float mid = Vector3.Dot(bone.InverseTransformPoint(neck.position) - row.Anchor, row.Inward);
                    float reach2 = 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 radius = Vector3.zero;
                        radius[k] = 0.5f;
                        float along = Vector3.Dot(bone.InverseTransformVector(neck.TransformVector(radius)), row.Inward);
                        reach2 += along * along;
                    }

                    float reach = Mathf.Sqrt(reach2);
                    bool inGap = false;
                    foreach (Vector2 g in gaps) inGap |= mid + reach > g.x && mid - reach < g.y;

                    drawn.Add(string.Format(ci, "{0} {1:0.0000} to {2:0.0000} m in, {3}{4}",
                        name, mid - reach, mid + reach, on ? "drawn" : "hidden", on && inGap ? ", in the gap" : ""));
                }

                if (TheatreNosePit.SnoutOn && row.Type == CellTypeIds.Nose)
                    drawn.Add("the snout's skirt where the nose is drawn as a snout (its depth is on the nose snout line)");

                string seat;
                if (OrganSeat && _organSeated != null && row.Part < _organSeated.Length && _organSeated[row.Part]) seat = "ORGAN_SEAT's cone";
                else if (!row.Drawn) seat = "none, the parent is not a drawn box";
                else if (SeatMarkerOff) seat = "none for this part (SEAT_MARKER)";
                else if (SeatDial > 0f) seat = string.Format(ci, "SKIN_SEAT's ball, full to {0:0.0000} m and gone by twice it", SeatDial * row.Smallest);
                else seat = "none";

                string Drop(float d) => !row.Drawn ? "not a drawn box" : d < 0f ? "no surface" : Against(d);

                Debug.Log(string.Format(ci,
                    "[Theatre] nose seat: body {0}, part {1} ({2}, {3} joint) on part {4} ({5}): the skin at the anchor {6} against the " +
                    "face (negative behind it; the drawn box alone {7}, with its seats {8}); the part's back face {9}; gap {10:0.0000} m " +
                    "outside the skin along the face normal ({11} m in); skin crossings {12} m in; seat: {13}; at the joint: {14}",
                    id, row.Part, row.Type, row.Joint, row.Parent, row.Shape,
                    row.Crossings.Count > 0 ? Against(row.Crossings[0]) : "none (no skin on the line)",
                    Drop(row.DropBare), Drop(row.DropBuilt),
                    float.IsNaN(row.BackIn) ? "off the line" : Against(row.BackIn),
                    gap, gapText.Count > 0 ? string.Join(", ", gapText) : "none",
                    crossText.Count > 0 ? string.Join(", ", crossText) : "none",
                    seat, drawn.Count > 0 ? string.Join("; ", drawn) : "no knuckle or bridge"));
            }
        }
        // SEATPROBE end
        // SEAT end
        // ---------------------------------------------------------------- the drawn box

        /// <summary>
        /// A box part as TheatreBody.shader draws it, as a field in the part's own metres: the rounded cube
        /// the palette swaps in (TheatreMeshes.BuildRoundedCube, inset, its corners rounded by the pillow's
        /// share of the object half, so ellipsoidal in metres), carried by ShapeBox's map, the soft tissue's
        /// margin (TissueMargin, ORGANIC=2) first, then the organic silhouette (OrganicFactor, ORGANIC over 0),
        /// then the taper and the bend, and clamped into the mesh's own box.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Exact, by inverting the map.</b> Every one of ShapeBox's maps leaves the long axis alone and
        /// moves a point only across it, so the field at a point is the rounded cube's at the point the map
        /// sends there. The taper and the bend are an affine map of the two coordinates across, per station
        /// along, and undo by a division. The organic silhouette scales the section toward the long axis by a
        /// factor that depends on the direction across (the rounding), the station (the ends and the bulge)
        /// and, through the lean, linearly on the point across, so along each ray from the axis the drawn
        /// radius is a quadratic in the mesh's radius with one root in range, or linear where the lean is
        /// clamped. The margin moves the thin coordinate alone, by a scale and an offset that depend on the
        /// two long coordinates, which it leaves alone, and undoes by a division. So the zero set is the drawn
        /// surface exactly, up to the shader's floors (a scale of at least 1e-3), which no part reaches.
        /// </para>
        /// <para>
        /// <b>What it leaves out.</b> The carve, the pinch at a joint, the float's pulse and the sway: noise
        /// and motion on the drawn surface, all inward, none of them the silhouette. The shader reads the
        /// part's world half extents, which carry the growth and the root's scale; every term ShapeBox draws
        /// from them is a ratio or a share, so the adult's half extents give the same shape.
        /// </para>
        /// <para>
        /// <b>A distance, near enough.</b> The rounded cube's field is read in the mesh's metres and so
        /// measures the distance before the map shrank the section. It is multiplied by the shrink across
        /// (at most one), which keeps it under the true distance and its zero set where it is, and the clamp
        /// box's distance is the floor under it outside. The constants are TheatreBody.shader's; the two must
        /// move together.
        /// </para>
        /// </remarks>
        private sealed class DrawnBox
        {
            // TheatreBody.shader's constants (AxisTieTolerance, the Organic* and the Tissue* of the margin).
            private const float TieTolerance = 1e-3f;
            private const float SectionPower = 2.5f;
            private const float CornerPower = 8f;
            private const float EndStart = 0.5f;
            private const float EndDepth = 0.45f;
            private const float EndRoundness = 0.9f;
            private const float BulgeDepth = 0.12f;
            private const float BulgeShift = 0.3f;
            private const float Lean = 0.14f;
            private const float FlatRatio = 0.55f;
            private const float MarginStart = 0.6f;
            private const float MarginThin = 0.6f;
            private const float RuffleShare = 0.5f;
            private const float RuffleWavesLow = 3f;
            private const float RuffleWavesSpan = 4f;
            private const float RuffleHarmonic = 0.3f;

            private const float TwoPi = 6.2831853f;

            // The mesh's half in object units (TheatreMeshes.Finish's shape channel).
            private const float MeshHalf = 0.5f * TheatreMeshes.Inset;

            private readonly float[] _h = new float[3];
            private readonly float[] _inner = new float[3];
            private readonly float[] _rho = new float[3];
            private readonly float[] _clamp = new float[3];
            private readonly bool _round;

            private readonly int _a, _b, _c;
            private readonly float _end;
            private readonly float _taper;
            private readonly float _ampB, _ampC, _roomB, _roomC;

            private readonly bool _organic;
            private readonly float _peak, _leanX, _leanY, _leanEnd;

            private readonly bool _margin;
            private readonly int _t, _v;
            private readonly float _waves, _phase;
            // RIG5 begin
            // Level 5: the share of the taper, the organic silhouette's terms and the margin kept, 1 - fill (1 with no fill).
            private readonly float _cut5 = 1f;
            // RIG5 end

            public DrawnBox(Vector3 h, float pillow, DrawnLook look)
            {
                _h[0] = h.x; _h[1] = h.y; _h[2] = h.z;

                for (int i = 0; i < 3; i++)
                {
                    // The cube of side one, its half 0.5 Inset and its corners rounded by pillow of that half,
                    // scaled by the visual's 2h: a flat half of Inset h (1 - pillow) and a corner semi-axis of
                    // Inset h pillow on each axis.
                    _rho[i] = TheatreMeshes.Inset * pillow * _h[i];
                    _inner[i] = TheatreMeshes.Inset * _h[i] - _rho[i];
                    _clamp[i] = TheatreMeshes.Inset * _h[i];
                }

                _round = pillow > 1e-4f;

                // ShapeBox's long axis, with its tie rule and fixed order, and the two across in cyclic order.
                if (AtLeast(h.x, h.y) && AtLeast(h.x, h.z)) _a = 0;
                else if (AtLeast(h.y, h.z)) _a = 1;
                else _a = 2;

                _b = (_a + 1) % 3;
                _c = (_a + 2) % 3;

                float cx = look.Seed;
                float smallest = Mathf.Min(h.x, Mathf.Min(h.y, h.z));

                // The taper's end and the bend's direction, from the part's seed as ShapeBox draws them.
                _end = Frac(cx * 13.37f + 0.271f) < 0.5f ? -1f : 1f;
                float angle = TwoPi * Frac(cx * 7.919f + 0.613f);

                _taper = look.Taper;
                // RIG5 begin
                if (look.Fill5 > 0f)
                {
                    _cut5 = 1f - look.Fill5;
                    _taper *= _cut5;
                }
                // RIG5 end
                _ampB = Mathf.Cos(angle) / Mathf.Max(1e-6f, 2f * _h[_b]) * look.Bend * smallest;
                _ampC = Mathf.Sin(angle) / Mathf.Max(1e-6f, 2f * _h[_c]) * look.Bend * smallest;
                _roomB = 1f - Mathf.Clamp01(Mathf.Abs(_ampB) / MeshHalf);
                _roomC = 1f - Mathf.Clamp01(Mathf.Abs(_ampC) / MeshHalf);

                // The organic silhouette's bulge and lean (OrganicFactor).
                _organic = look.Organic;
                _peak = BulgeShift * (2f * Frac(cx * 5.381f + 0.447f) - 1f);
                float leanAngle = TwoPi * Frac(cx * 3.917f + 0.829f);
                _leanEnd = Frac(cx * 9.173f + 0.358f) < 0.5f ? -1f : 1f;
                _leanX = Mathf.Cos(leanAngle) * 0.70710678f;
                _leanY = Mathf.Sin(leanAngle) * 0.70710678f;

                // The soft tissue's margin (TissueMargin): on a flattened part alone, with its thinnest axis by
                // the leaf's tie rule, its middle axis the one left, and the ruffle's count and phase.
                _margin = false;
                if (look.Tissue)
                {
                    if (AtMost(h.x, h.y) && AtMost(h.x, h.z)) _t = 0;
                    else if (AtMost(h.y, h.z)) _t = 1;
                    else _t = 2;

                    float middle = float.PositiveInfinity;
                    for (int i = 0; i < 3; i++) if (i != _t) middle = Mathf.Min(middle, _h[i]);

                    _v = 3 - _a - _t;
                    _margin = _a != _t && AtMost(_h[_t], FlatRatio * middle);

                    _waves = RuffleWavesLow + Mathf.Floor(RuffleWavesSpan * Frac(cx * 5.719f + 0.233f));
                    _phase = TwoPi * Frac(cx * 8.317f + look.Individual * 3.71f + 0.519f);
                }
            }

            // SEAT begin
            // The seats of this box (SeatDial), or null: no seat, and every test of it false.
            private SeatSet _seats;

            public void SetSeats(SeatSet seats)
            {
                _seats = seats;
            }

            /// <summary>Puts <paramref name="seats"/> in and returns the ones that were there, for the probe.</summary>
            public SeatSet SwapSeats(SeatSet seats)
            {
                SeatSet held = _seats;
                _seats = seats;
                return held;
            }

            /// <summary>
            /// How far the drawn surface lies from the anchor, going in along the normal of the box face the anchor sits on (the
            /// largest of anchor over half extent), found by marching the field in steps of at most 1 mm and bisecting. 0 when the
            /// anchor is already inside, -1 when the march crosses nothing.
            /// </summary>
            public float Drop(float ax, float ay, float az)
            {
                float rx = Mathf.Abs(ax) / Mathf.Max(1e-9f, _h[0]);
                float ry = Mathf.Abs(ay) / Mathf.Max(1e-9f, _h[1]);
                float rz = Mathf.Abs(az) / Mathf.Max(1e-9f, _h[2]);
                int axis = rx >= ry && rx >= rz ? 0 : ry >= rz ? 1 : 2;
                float dir = Pick(ax, ay, az, axis) >= 0f ? -1f : 1f;
                float reach = 2f * _h[axis];

                if (At(ax, ay, az) <= 0f) return 0f;

                float step = Mathf.Min(0.001f, 0.05f * reach);
                if (!(step > 1e-7f)) return -1f;

                int steps = Mathf.CeilToInt(reach / step) + 1;
                float lo = 0f;

                for (int s = 1; s <= steps; s++)
                {
                    float t = s * step;
                    if (AtInward(ax, ay, az, axis, dir, t) <= 0f)
                    {
                        float hi = t;
                        for (int i = 0; i < 24; i++)
                        {
                            float mid = 0.5f * (lo + hi);
                            if (AtInward(ax, ay, az, axis, dir, mid) <= 0f) hi = mid; else lo = mid;
                        }

                        return 0.5f * (lo + hi);
                    }

                    lo = t;
                }

                return -1f;
            }

            private float AtInward(float ax, float ay, float az, int axis, float dir, float t) =>
                At(axis == 0 ? ax + dir * t : ax, axis == 1 ? ay + dir * t : ay, axis == 2 ? az + dir * t : az);

            // SEAT end
            /// <summary>The field at a point in the part's own metres.</summary>
            public float At(float lx, float ly, float lz)
            {
                // The clamp box: the mesh's own box, ShapeBox's last word, as a floor under the field.
                float clamp = BoxDistance(lx, ly, lz, _clamp[0], _clamp[1], _clamp[2]);

                // Into the visual's object units, where ShapeBox works: an object unit is the part's full size.
                float o0 = lx / Mathf.Max(1e-9f, 2f * _h[0]);
                float o1 = ly / Mathf.Max(1e-9f, 2f * _h[1]);
                float o2 = lz / Mathf.Max(1e-9f, 2f * _h[2]);

                float along = Pick(o0, o1, o2, _a);
                float vB = Pick(o0, o1, o2, _b);
                float vC = Pick(o0, o1, o2, _c);

                // ---- the taper and the bend, undone

                float u = Mathf.Clamp01(0.5f + 0.5f * _end * along / MeshHalf);
                float ease = u * u * (3f - 2f * u);
                float taper = 1f - _taper * ease;

                float t = Mathf.Clamp01(0.5f + 0.5f * along / MeshHalf);
                float wave = Mathf.Sin(3.14159265f * t);

                float scaleB = Mathf.Max(taper * _roomB, 1e-3f);
                float scaleC = Mathf.Max(taper * _roomC, 1e-3f);
                // SEAT begin
                // At a seat the taper's scales go to 1 and the bend's offset to 0 (the organic factor and the margin below read seatW).
                float seatW = 0f;
                if (_seats != null)
                {
                    seatW = _seats.Weight(lx, ly, lz);
                    if (seatW > 0f)
                    {
                        scaleB = Mathf.Lerp(scaleB, 1f, seatW);
                        scaleC = Mathf.Lerp(scaleC, 1f, seatW);
                        wave *= 1f - seatW;
                    }
                }
                // SEAT end

                vB = (vB - _ampB * wave) / scaleB;
                vC = (vC - _ampC * wave) / scaleC;

                float shrink = Mathf.Min(scaleB, scaleC);

                // ---- the organic silhouette, undone along the ray from the axis

                if (_organic)
                {
                    float drawn = Mathf.Sqrt(vB * vB + vC * vC);
                    float factor = OrganicInverse(along, vB, vC, drawn);
                    // SEAT begin
                    if (_seats != null && seatW > 0f) factor = Mathf.Lerp(factor, 1f, seatW);
                    // SEAT end

                    vB /= factor;
                    vC /= factor;
                    shrink *= Mathf.Min(1f, factor);
                }

                float p0 = Put(along, vB, vC, 0);
                float p1 = Put(along, vB, vC, 1);
                float p2 = Put(along, vB, vC, 2);

                // ---- the margin, undone on the thin coordinate

                if (_margin)
                {
                    float a = Pick(p0, p1, p2, _a) / MeshHalf;
                    float b = Pick(p0, p1, p2, _v) / MeshHalf;
                    MarginAt(a, b, out float thick, out float ruffle);
                    // SEAT begin
                    if (_seats != null && seatW > 0f)
                    {
                        thick = Mathf.Lerp(thick, 1f, seatW);
                        ruffle *= 1f - seatW;
                    }
                    // SEAT end

                    float c = (Pick(p0, p1, p2, _t) - ruffle) / thick;
                    if (_t == 0) p0 = c; else if (_t == 1) p1 = c; else p2 = c;
                    shrink *= thick;
                }

                // ---- the rounded cube, in its metres

                float d = RoundedCube(p0 * 2f * _h[0], p1 * 2f * _h[1], p2 * 2f * _h[2]);

                return Mathf.Max(d * Mathf.Min(1f, shrink), clamp);
            }

            /// <summary>
            /// The organic factor at the mesh point that the map sends to the drawn point (along, vB, vC):
            /// OrganicFactor's o, found from the drawn radius by solving r' = o(r) r along the ray, since every
            /// term but the lean depends on the direction and the station alone.
            /// </summary>
            private float OrganicInverse(float along, float vB, float vC, float drawn)
            {
                float x = along / MeshHalf;

                // 2. The ends.
                float span = 1f - EndStart;
                float w = Mathf.Clamp01((Mathf.Abs(x) - EndStart) / span);
                float arc = Mathf.Sqrt(1f - EndRoundness * w * w);
                float arcFull = 1f - Mathf.Sqrt(1f - EndRoundness);
                float ends = 1f - EndDepth * (1f - arc) / arcFull;
                // RIG5 begin
                if (_cut5 < 1f) ends = 1f - (1f - ends) * _cut5;
                // RIG5 end

                // 3 and 4. The bulge, its peak placed by the part's number.
                float reach = x >= _peak ? 1f - _peak : 1f + _peak;
                float dd = (x - _peak) / reach;
                float bulge = 1f - BulgeDepth * dd * dd;
                // RIG5 begin
                if (_cut5 < 1f) bulge = 1f - (1f - bulge) * _cut5;
                // RIG5 end

                float toward = Mathf.Clamp01(0.5f + 0.5f * _leanEnd * x);
                float lt = Lean * toward;
                // RIG5 begin
                if (_cut5 < 1f) lt *= _cut5;
                // RIG5 end

                float profile = ends * bulge;

                if (!(drawn > 1e-9f)) return Mathf.Max(1e-3f, profile * (1f - 0.5f * lt));

                float ex = vB / drawn, ey = vC / drawn;

                // 1. The rounder section, which depends on the direction alone.
                float rounded = OrganicRound(ex, ey);
                // RIG5 begin
                if (_cut5 < 1f) rounded = 1f - (1f - rounded) * _cut5;
                // RIG5 end

                float k = rounded * profile;
                float c = (_leanX * ex + _leanY * ey) / MeshHalf;
                float a0 = 1f - 0.5f * lt;
                float a1 = 0.5f * lt;

                float r;
                if (Mathf.Abs(c) < 1e-9f || lt <= 0f) r = drawn / (k * a0);
                else if (c > 0f && drawn >= k / c) r = drawn / k;
                else if (c < 0f && drawn >= k * (1f - lt) / -c) r = drawn / (k * (1f - lt));
                else r = 2f * drawn / (k * a0 + Mathf.Sqrt(Mathf.Max(0f, k * k * a0 * a0 + 4f * k * a1 * c * drawn)));

                return Mathf.Max(1e-3f, r > 1e-12f ? drawn / r : k * a0);
            }

            /// <summary>OrganicRound: N8 over N2.5 of the direction across, at most one.</summary>
            private static float OrganicRound(float x, float y)
            {
                float mx = Mathf.Abs(x), my = Mathf.Abs(y);
                float big = Mathf.Max(mx, my);
                if (big < 1e-5f) return 1f;

                float rx = Mathf.Max(mx / big, 1e-6f), ry = Mathf.Max(my / big, 1e-6f);
                float sumN = Mathf.Pow(rx, SectionPower) + Mathf.Pow(ry, SectionPower);
                float sumM = Mathf.Pow(rx, CornerPower) + Mathf.Pow(ry, CornerPower);

                float s = Mathf.Pow(sumM, 1f / CornerPower) / Mathf.Pow(sumN, 1f / SectionPower);
                return Mathf.Min(s, 1f);
            }

            /// <summary>TissueMargin's thickness share and ruffle at a point of the section (a, b over the mesh half).</summary>
            private void MarginAt(float a, float b, out float thick, out float ruffle)
            {
                float big = Mathf.Max(Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)), 1e-5f);
                float rx = Mathf.Abs(a) / big, ry = Mathf.Abs(b) / big;
                float rx4 = rx * rx * rx * rx, ry4 = ry * ry * ry * ry;
                float s = Mathf.Max(Mathf.Pow(rx4 * rx4 + ry4 * ry4, 0.125f), 1f);
                float e = big * s;

                float t = Mathf.Clamp01((e - MarginStart) / (1f - MarginStart));
                float w = t * t * (3f - 2f * t);

                thick = 1f - MarginThin * w;
                // RIG5 begin
                if (_cut5 < 1f) thick = 1f - (1f - thick) * _cut5;
                // RIG5 end

                float theta = Mathf.Atan2(b, a + 1e-7f);
                float k1 = _waves * theta + _phase;
                float k2 = (2f * _waves + 1f) * theta + 2.3f * _phase;
                float wave = (1f - RuffleHarmonic) * Mathf.Sin(k1) + RuffleHarmonic * Mathf.Sin(k2);

                ruffle = RuffleShare * MarginThin * MeshHalf * w * wave;
                // RIG5 begin
                if (_cut5 < 1f) ruffle *= _cut5;
                // RIG5 end
            }

            /// <summary>
            /// The rounded cube in metres: its flat half and its ellipsoidal corner, the corner's field the
            /// bounded ellipsoid approximation on the part of the point past the flat half.
            /// </summary>
            private float RoundedCube(float x, float y, float z)
            {
                if (!_round) return BoxDistance(x, y, z, _clamp[0], _clamp[1], _clamp[2]);

                float qx = Mathf.Abs(x) - _inner[0];
                float qy = Mathf.Abs(y) - _inner[1];
                float qz = Mathf.Abs(z) - _inner[2];

                if (qx > 0f || qy > 0f || qz > 0f)
                {
                    float ex = Mathf.Max(qx, 0f) / _rho[0];
                    float ey = Mathf.Max(qy, 0f) / _rho[1];
                    float ez = Mathf.Max(qz, 0f) / _rho[2];
                    float k0 = Mathf.Sqrt(ex * ex + ey * ey + ez * ez);
                    float fx = ex / _rho[0], fy = ey / _rho[1], fz = ez / _rho[2];
                    float k1 = Mathf.Sqrt(fx * fx + fy * fy + fz * fz);
                    if (!(k1 > 1e-20f)) return -Mathf.Min(_rho[0], Mathf.Min(_rho[1], _rho[2]));
                    return k0 * (k0 - 1f) / k1;
                }

                return Mathf.Max(qx - _rho[0], Mathf.Max(qy - _rho[1], qz - _rho[2]));
            }

            private static float BoxDistance(float x, float y, float z, float hx, float hy, float hz)
            {
                float qx = Mathf.Abs(x) - hx, qy = Mathf.Abs(y) - hy, qz = Mathf.Abs(z) - hz;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f), oz = Mathf.Max(qz, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy + oz * oz) + Mathf.Min(Mathf.Max(qx, Mathf.Max(qy, qz)), 0f);
            }

            private static float Pick(float x, float y, float z, int axis) => axis == 0 ? x : axis == 1 ? y : z;

            /// <summary>Component <paramref name="axis"/> of the point with along on the long axis and vB, vC across.</summary>
            private float Put(float along, float vB, float vC, int axis) =>
                axis == _a ? along : axis == _b ? vB : vC;

            private static bool AtLeast(float a, float b) => a >= b * (1f - TieTolerance);

            private static bool AtMost(float a, float b) => a <= b * (1f + TieTolerance);

            private static float Frac(float x) => x - Mathf.Floor(x);
        }

        /// <summary>The merged primitives and the balls, and the smooth union over them.</summary>
        private sealed class Field
        {
            private readonly float _k;
            private readonly float _quarter;
            private readonly List<Primitive> _parts = new List<Primitive>();
            private readonly List<Primitive> _all = new List<Primitive>();
            private Primitive[] _prims;
            private float _cull = float.PositiveInfinity;

            public Field(float k)
            {
                _k = k;
                _quarter = 0.25f * k;
            }

            // RIG4 begin
            // Level 4: the joints (null at any lower level, and for a body with none), the largest pair blend radius, and
            // the scratch the rig's evaluation and weights share.
            private List<RigJoint> _joints;
            private float _jointKMax;
            private float[] _slots;
            private float[] _acc;
            private int[] _pickIndex;
            private float[] _pickWeight;

            /// <summary>A joint between merged parts <paramref name="parent"/> and <paramref name="child"/> (slots in AddPart's order).</summary>
            public void AddJoint(int parent, int child, Vector3 anchor, float radius)
            {
                if (_joints == null) _joints = new List<RigJoint>();

                _joints.Add(new RigJoint
                {
                    Parent = parent, Child = child, Anchor = anchor, Radius = radius, WeightK = RigWeightShare * radius,
                });

                _jointKMax = Mathf.Max(_jointKMax, radius);
            }

            public int JointCount => _joints != null ? _joints.Count : 0;

            public float JointRadiusLeast()
            {
                float least = float.PositiveInfinity;
                if (_joints != null) foreach (RigJoint joint in _joints) least = Mathf.Min(least, joint.Radius);
                return least;
            }

            public float JointRadiusMost() => _jointKMax;

            // RIG4 end
            public void AddPart(Primitive p)
            {
                _parts.Add(p);
                _all.Add(p);
                _prims = null;
            }

            public void AddBall(Vector3 centre, float radius)
            {
                var p = new Primitive
                {
                    Kind = KindBall,
                    Cx = centre.x, Cy = centre.y, Cz = centre.z,
                    A = radius,
                    Low = centre - Vector3.one * radius,
                    High = centre + Vector3.one * radius,
                };

                _all.Add(p);
                _prims = null;
            }

            // NOBOXJOIN begin
            // EVOSIM_THEATRE_NO_BOX_JOIN: the joins (null at 0, and for a body with none) and the largest blend radius.
            private List<NbJoin> _nbJoins;
            private float _nbMax;

            /// <summary>
            /// A join between merged parts <paramref name="parent"/> and <paramref name="child"/> (slots in AddPart's order): a pair blend
            /// of <paramref name="radius"/> and a ball of <paramref name="ball"/> (0 for none) at <paramref name="anchor"/>.
            /// </summary>
            public void AddNbJoin(int parent, int child, Vector3 anchor, float radius, float ball)
            {
                if (_nbJoins == null) _nbJoins = new List<NbJoin>();

                _nbJoins.Add(new NbJoin { Parent = parent, Child = child, Anchor = anchor, Radius = radius, Ball = ball });
                _nbMax = Mathf.Max(_nbMax, radius);
            }

            /// <summary>
            /// The field with the no-box joins: for each join the smooth minimum of its two parts' inset fields at the join's own radius,
            /// united at the global k with the join's ball (a plain distance to the anchor), the lowest of them and
            /// <paramref name="result"/>. A join is skipped where either part is culled. The pair blend is never more than a quarter of
            /// its radius below the nearer part, and it differs from the plain minimum only where the two are within its radius of each
            /// other, so away from the meeting of the two surfaces it is the field it joins. <paramref name="at"/> is one rest point per
            /// part (level 6, where the parts may be posed), or null for the one point <paramref name="x"/>, <paramref name="y"/>,
            /// <paramref name="z"/>.
            /// </summary>
            private float NbJoined(float[] d, Vector3[] at, float x, float y, float z, float result)
            {
                for (int j = 0; j < _nbJoins.Count; j++)
                {
                    NbJoin join = _nbJoins[j];
                    float a = d[join.Parent];
                    float b = d[join.Child];
                    if (float.IsPositiveInfinity(a) || float.IsPositiveInfinity(b)) continue;

                    float joined = SmoothMin(a, b, join.Radius);

                    if (join.Ball > 0f)
                    {
                        Vector3 onParent = at != null ? at[join.Parent] : new Vector3(x, y, z);
                        Vector3 onChild = at != null ? at[join.Child] : onParent;
                        float away = Mathf.Min((onParent - join.Anchor).magnitude, (onChild - join.Anchor).magnitude);

                        joined = SmoothMin(joined, away - join.Ball, _k);
                    }

                    if (joined < result) result = joined;
                }

                return result;
            }
            // NOBOXJOIN end

            public void RawBounds(out Vector3 low, out Vector3 high)
            {
                low = Vector3.one * float.PositiveInfinity;
                high = Vector3.one * float.NegativeInfinity;

                foreach (Primitive p in _all)
                {
                    low = Vector3.Min(low, p.Low);
                    high = Vector3.Max(high, p.High);
                }
                // RIG4 begin
                // A pair blend lets the skin leave the parts by up to a quarter of its radius, so the grid's rim is that
                // much further out.
                if (_joints != null)
                {
                    low -= Vector3.one * (0.25f * _jointKMax);
                    high += Vector3.one * (0.25f * _jointKMax);
                }
                // RIG4 end
                // RIG5 begin
                // A fillet is below its parts only where both are within a quarter of its radius, so the grid's rim is that much further
                // out; it adds nothing outside the two colliders and the wedge between their faces.
                if (_fillets != null)
                {
                    low -= Vector3.one * (0.25f * _filletMax);
                    high += Vector3.one * (0.25f * _filletMax);
                }
                // RIG5 end
                // NOBOXJOIN begin
                // A join's blend lets the skin leave its parts by up to a quarter of its radius, and its ball stands at the anchor, which a
                // part's box need not hold, so the grid's rim is the balls' reach and that quarter further out.
                if (_nbJoins != null)
                {
                    foreach (NbJoin join in _nbJoins)
                    {
                        low = Vector3.Min(low, join.Anchor - Vector3.one * join.Ball);
                        high = Vector3.Max(high, join.Anchor + Vector3.one * join.Ball);
                    }

                    low -= Vector3.one * (0.25f * _nbMax);
                    high += Vector3.one * (0.25f * _nbMax);
                }
                // NOBOXJOIN end
            }

            /// <summary>
            /// Leaves a primitive out of every sample further than <paramref name="margin"/> from its
            /// box. Exact for the box, the capsule and the ball, whose fields are distances and so at
            /// least the margin there, which is over k beyond any surface the smooth minimum could
            /// still move, and for the drawn box, whose field is floored by its clamp box's distance.
            /// The ellipsoid's bound and the squashed capsule's can read less than the distance; at four
            /// k from the box the difference is assumed not to reach the blend.
            /// </summary>
            public void Cull(float margin)
            {
                // RIG4 begin
                if (_joints != null) margin = Mathf.Max(margin, 4f * _jointKMax);
                // RIG4 end
                // RIG5 begin
                if (_fillets != null) margin = Mathf.Max(margin, 4f * _filletMax);
                // RIG5 end
                // NOBOXJOIN begin
                // The blend moves the surface where a part is within its radius of the other, so a part twice that far is left out.
                if (_nbJoins != null) margin = Mathf.Max(margin, 2f * _nbMax);
                // NOBOXJOIN end
                _cull = margin;
                _prims = new Primitive[_all.Count];

                for (int i = 0; i < _all.Count; i++)
                {
                    Primitive p = _all[i];
                    p.CullLowX = p.Low.x - margin; p.CullLowY = p.Low.y - margin; p.CullLowZ = p.Low.z - margin;
                    p.CullHighX = p.High.x + margin; p.CullHighY = p.High.y + margin; p.CullHighZ = p.High.z + margin;
                    // FAMILYSKIN begin
                    // The family field can read well under the distance on a long thin solid, so its box is grown by the margin times
                    // the solid's aspect (Solid.Pad) before a sample is left out of it.
                    if (TheatreFamilySkin.Enabled && p.Kind == KindFamily && !float.IsInfinity(margin))
                    {
                        float extra = margin * p.Fam.Pad;
                        p.CullLowX -= extra; p.CullLowY -= extra; p.CullLowZ -= extra;
                        p.CullHighX += extra; p.CullHighY += extra; p.CullHighZ += extra;
                    }
                    // FAMILYSKIN end
                    _prims[i] = p;
                }
            }

            /// <summary>The skin's field at a point: negative inside, zero on the skin.</summary>
            public float At(float x, float y, float z)
            {
                // RIG6 begin
                if (_hinges6 != null) return Rig6At(x, y, z);
                // RIG6 end
                // RIG5 begin
                if (_fillets != null) return Rig5At(x, y, z);
                // RIG5 end
                // RIG4 begin
                if (_joints != null) return RigAt(x, y, z);
                // RIG4 end
                // NOBOXJOIN begin
                if (_nbJoins != null) return RigAt(x, y, z);
                // NOBOXJOIN end
                if (_prims == null) Cull(float.PositiveInfinity);

                bool any = false;
                float fold = 0f;
                float least = float.PositiveInfinity;

                for (int i = 0; i < _prims.Length; i++)
                {
                    if (x < _prims[i].CullLowX || x > _prims[i].CullHighX ||
                        y < _prims[i].CullLowY || y > _prims[i].CullHighY ||
                        z < _prims[i].CullLowZ || z > _prims[i].CullHighZ)
                    {
                        continue;
                    }

                    float d = Distance(ref _prims[i], x, y, z) + _quarter;

                    if (!any) { fold = d; any = true; }
                    else fold = SmoothMin(fold, d, _k);

                    if (d < least) least = d;
                }

                if (!any) return _cull;

                // Each fold is within k/4 of its own two inputs' minimum and the folds add, so the
                // whole is held at the plain minimum less k/4: the margin, for any number of parts.
                return Mathf.Max(fold, least - _quarter);
            }

            public float At(Vector3 p) => At(p.x, p.y, p.z);

            /// <summary>The uninset field of merged part <paramref name="j"/>, for the weights.</summary>
            public float PartDistance(int j, Vector3 at)
            {
                Primitive p = _parts[j];
                return Distance(ref p, at.x, at.y, at.z);
            }

            public Vector3 Gradient(Vector3 p, float step)
            {
                float inv = 0.5f / step;
                return new Vector3(
                    (At(p.x + step, p.y, p.z) - At(p.x - step, p.y, p.z)) * inv,
                    (At(p.x, p.y + step, p.z) - At(p.x, p.y - step, p.z)) * inv,
                    (At(p.x, p.y, p.z + step) - At(p.x, p.y, p.z - step)) * inv);
            }

            /// <summary>
            /// Samples the field on the grid. Each row of samples is tested against the primitives
            /// whose culled boxes cross it, which gives the same numbers as <see cref="At(float,float,float)"/>.
            /// </summary>
            public float[] SampleGrid(Vector3 origin, float cell, int nx, int ny, int nz)
            {
                // RIG6 begin
                if (_hinges6 != null) return Rig6SampleGrid(origin, cell, nx, ny, nz);
                // RIG6 end
                // RIG5 begin
                if (_fillets != null) return Rig5SampleGrid(origin, cell, nx, ny, nz);
                // RIG5 end
                // RIG4 begin
                if (_joints != null) return RigSampleGrid(origin, cell, nx, ny, nz);
                // RIG4 end
                // NOBOXJOIN begin
                if (_nbJoins != null) return RigSampleGrid(origin, cell, nx, ny, nz);
                // NOBOXJOIN end
                if (_prims == null) Cull(float.PositiveInfinity);

                var samples = new float[nx * ny * nz];
                var row = new List<int>(_prims.Length);

                for (int z = 0; z < nz; z++)
                {
                    float pz = origin.z + z * cell;

                    for (int y = 0; y < ny; y++)
                    {
                        float py = origin.y + y * cell;

                        row.Clear();
                        for (int i = 0; i < _prims.Length; i++)
                        {
                            if (py < _prims[i].CullLowY || py > _prims[i].CullHighY ||
                                pz < _prims[i].CullLowZ || pz > _prims[i].CullHighZ)
                            {
                                continue;
                            }

                            row.Add(i);
                        }

                        int at = nx * (y + ny * z);

                        for (int x = 0; x < nx; x++)
                        {
                            float px = origin.x + x * cell;

                            bool any = false;
                            float fold = 0f;
                            float least = float.PositiveInfinity;

                            for (int r = 0; r < row.Count; r++)
                            {
                                int i = row[r];
                                if (px < _prims[i].CullLowX || px > _prims[i].CullHighX) continue;

                                float d = Distance(ref _prims[i], px, py, pz) + _quarter;

                                if (!any) { fold = d; any = true; }
                                else fold = SmoothMin(fold, d, _k);

                                if (d < least) least = d;
                            }

                            samples[at + x] = any ? Mathf.Max(fold, least - _quarter) : _cull;
                        }
                    }
                }

                return samples;
            }

            // RIG4 begin
            // ---- level 4: the rig

            /// <summary>The scratch of the primitives' distances at one sample: infinity for a culled primitive.</summary>
            private float[] Slots()
            {
                if (_slots == null || _slots.Length != _prims.Length) _slots = new float[_prims.Length];
                return _slots;
            }

            /// <summary>
            /// The field with the joints (level 4): the smooth union of every primitive at the global k, as before, then
            /// for each joint the smooth minimum of its two parts at the joint's own radius, the lowest of them all. A pair
            /// blend is never above the plain minimum of its two parts and never more than a quarter of its radius below
            /// it, so it joins to the union where its parts are further apart than its radius (it equals their plain
            /// minimum there, which the global fold is never above) and stays within that quarter of the parts.
            /// </summary>
            private float RigValue(float[] d, float x, float y, float z)
            {
                bool any = false;
                float fold = 0f;
                float least = float.PositiveInfinity;

                for (int i = 0; i < d.Length; i++)
                {
                    float value = d[i];
                    if (float.IsPositiveInfinity(value)) continue;

                    if (!any) { fold = value; any = true; }
                    else fold = SmoothMin(fold, value, _k);

                    if (value < least) least = value;
                }

                if (!any) return _cull;

                float result = Mathf.Max(fold, least - _quarter);

                // NOBOXJOIN begin
                // The joints are null when only the no-box joins routed the field here (levels 1 to 3).
                if (_joints != null)
                // NOBOXJOIN end
                for (int j = 0; j < _joints.Count; j++)
                {
                    RigJoint joint = _joints[j];
                    float a = d[joint.Parent];
                    float b = d[joint.Child];
                    if (float.IsPositiveInfinity(a) || float.IsPositiveInfinity(b)) continue;

                    float joined = SmoothMin(a, b, joint.Radius);
                    if (joined < result) result = joined;
                }
                // NOBOXJOIN begin
                if (_nbJoins != null) result = NbJoined(d, null, x, y, z, result);
                // NOBOXJOIN end

                return result;
            }

            private float RigAt(float x, float y, float z)
            {
                if (_prims == null) Cull(float.PositiveInfinity);

                float[] d = Slots();

                for (int i = 0; i < _prims.Length; i++)
                {
                    bool near = !(x < _prims[i].CullLowX || x > _prims[i].CullHighX ||
                                  y < _prims[i].CullLowY || y > _prims[i].CullHighY ||
                                  z < _prims[i].CullLowZ || z > _prims[i].CullHighZ);

                    d[i] = near ? Distance(ref _prims[i], x, y, z) + _quarter : float.PositiveInfinity;
                }

                return RigValue(d, x, y, z);
            }

            private float[] RigSampleGrid(Vector3 origin, float cell, int nx, int ny, int nz)
            {
                if (_prims == null) Cull(float.PositiveInfinity);

                var samples = new float[nx * ny * nz];
                var row = new List<int>(_prims.Length);
                float[] d = Slots();

                for (int z = 0; z < nz; z++)
                {
                    float pz = origin.z + z * cell;

                    for (int y = 0; y < ny; y++)
                    {
                        float py = origin.y + y * cell;

                        row.Clear();
                        for (int i = 0; i < _prims.Length; i++)
                        {
                            if (py < _prims[i].CullLowY || py > _prims[i].CullHighY ||
                                pz < _prims[i].CullLowZ || pz > _prims[i].CullHighZ)
                            {
                                continue;
                            }

                            row.Add(i);
                        }

                        int at = nx * (y + ny * z);

                        for (int x = 0; x < nx; x++)
                        {
                            float px = origin.x + x * cell;

                            for (int i = 0; i < d.Length; i++) d[i] = float.PositiveInfinity;

                            for (int r = 0; r < row.Count; r++)
                            {
                                int i = row[r];
                                if (px < _prims[i].CullLowX || px > _prims[i].CullHighX) continue;

                                d[i] = Distance(ref _prims[i], px, py, pz) + _quarter;
                            }

                            samples[at + x] = RigValue(d, px, py, pz);
                        }
                    }
                }

                return samples;
            }

            /// <summary>
            /// A vertex's weights across the joints (level 4). <paramref name="bw"/> is the vertex's weights from the global
            /// softmax. For each joint, in order, a share is taken: the weight the vertex already puts on the joint's two
            /// parts (so a vertex of a third part is not pulled in), times a smoothstep that is one within
            /// <see cref="RigZoneInner"/> joint radii of the anchor and zero beyond <see cref="RigZoneOuter"/>. The weights
            /// then move that share of the way to the softmax of the two parts' distances alone at the joint's scale
            /// (<see cref="RigJoint.WeightK"/>), so across the joint the bend is the two bones' blend over a width of about
            /// the ball. Each step is continuous in its share and the shares are continuous in the position, so the weights
            /// are. Away from every joint nothing changes. The four largest are kept, those under <see cref="MinWeight"/>
            /// dropped and the rest renormalised, largest first.
            /// </summary>
            public BoneWeight RigWeights(BoneWeight bw, Vector3 p)
            {
                if (_joints == null) return bw;

                int bones = _parts.Count;
                if (_acc == null || _acc.Length != bones) _acc = new float[bones];
                if (_pickIndex == null) { _pickIndex = new int[MaxInfluences]; _pickWeight = new float[MaxInfluences]; }

                bool moved = false;

                for (int j = 0; j < _joints.Count; j++)
                {
                    RigJoint joint = _joints[j];

                    float near = BoneShare(bw, joint.Parent) + BoneShare(bw, joint.Child);
                    if (!(near > 0f)) continue;

                    float away = (p - joint.Anchor).magnitude / joint.Radius;
                    float t = Mathf.Clamp01((away - RigZoneInner) / (RigZoneOuter - RigZoneInner));
                    float share = near * (1f - t * t * (3f - 2f * t));
                    if (!(share > 1e-4f)) continue;

                    if (!moved)
                    {
                        Array.Clear(_acc, 0, _acc.Length);
                        _acc[bw.boneIndex0] += bw.weight0;
                        _acc[bw.boneIndex1] += bw.weight1;
                        _acc[bw.boneIndex2] += bw.weight2;
                        _acc[bw.boneIndex3] += bw.weight3;
                        moved = true;
                    }

                    float da = PartDistance(joint.Parent, p);
                    float db = PartDistance(joint.Child, p);
                    float nearest = Mathf.Min(da, db);
                    float wa = Mathf.Exp(-(da - nearest) / joint.WeightK);
                    float wb = Mathf.Exp(-(db - nearest) / joint.WeightK);
                    float both = wa + wb;

                    for (int i = 0; i < _acc.Length; i++) _acc[i] *= 1f - share;

                    _acc[joint.Parent] += share * wa / both;
                    _acc[joint.Child] += share * wb / both;
                }

                if (!moved) return bw;

                // The four largest, largest first.
                for (int t = 0; t < MaxInfluences; t++)
                {
                    int at = 0;
                    float most = -1f;

                    for (int i = 0; i < _acc.Length; i++)
                    {
                        if (_acc[i] > most) { most = _acc[i]; at = i; }
                    }

                    _pickIndex[t] = at;
                    _pickWeight[t] = most > 0f ? most : 0f;
                    if (most > 0f) _acc[at] = -1f;
                }

                float kept = 0f;
                for (int t = 0; t < MaxInfluences; t++)
                {
                    if (_pickWeight[t] < MinWeight) _pickWeight[t] = 0f;
                    kept += _pickWeight[t];
                }

                if (!(kept > 0f)) return bw;

                return new BoneWeight
                {
                    boneIndex0 = _pickIndex[0], weight0 = _pickWeight[0] / kept,
                    boneIndex1 = _pickIndex[1], weight1 = _pickWeight[1] / kept,
                    boneIndex2 = _pickIndex[2], weight2 = _pickWeight[2] / kept,
                    boneIndex3 = _pickIndex[3], weight3 = _pickWeight[3] / kept,
                };
            }

            private static float BoneShare(BoneWeight bw, int bone) =>
                (bw.boneIndex0 == bone ? bw.weight0 : 0f) + (bw.boneIndex1 == bone ? bw.weight1 : 0f) +
                (bw.boneIndex2 == bone ? bw.weight2 : 0f) + (bw.boneIndex3 == bone ? bw.weight3 : 0f);

            // RIG4 end
            // RIG5 begin
            // ---- level 5: the fillets and the joint ramp

            // The fillets (null below level 5, and for a body with no joint) and the largest fillet radius.
            private List<Fillet5> _fillets;
            private float _filletMax;

            /// <summary>A joint's fillet and ramp (level 5); its parts are slots in AddPart's order.</summary>
            public void AddFillet(Fillet5 fillet)
            {
                if (_fillets == null) _fillets = new List<Fillet5>();

                _fillets.Add(fillet);
                _filletMax = Mathf.Max(_filletMax, fillet.Radius);
            }

            public int FilletCount => _fillets != null ? _fillets.Count : 0;

            /// <summary>The least and the most of the fillets' radii and of their ramps' reach, for the log.</summary>
            public void FilletRanges(out float radiusLeast, out float radiusMost, out float reachLeast, out float reachMost)
            {
                radiusLeast = 0f;
                radiusMost = 0f;
                reachLeast = 0f;
                reachMost = 0f;
                if (_fillets == null || _fillets.Count == 0) return;

                radiusLeast = float.PositiveInfinity;
                reachLeast = float.PositiveInfinity;

                foreach (Fillet5 fillet in _fillets)
                {
                    radiusLeast = Mathf.Min(radiusLeast, fillet.Radius);
                    radiusMost = Mathf.Max(radiusMost, fillet.Radius);
                    reachLeast = Mathf.Min(reachLeast, fillet.Reach);
                    reachMost = Mathf.Max(reachMost, fillet.Reach);
                }
            }

            /// <summary>A rest point in a primitive's own frame.</summary>
            private static void Local5(ref Primitive p, float x, float y, float z, out float lx, out float ly, out float lz)
            {
                float dx = x - p.Cx, dy = y - p.Cy, dz = z - p.Cz;
                lx = dx * p.Xx + dy * p.Xy + dz * p.Xz;
                ly = dx * p.Yx + dy * p.Yy + dz * p.Yz;
                lz = dx * p.Zx + dy * p.Zy + dz * p.Zz;
            }

            /// <summary>The collider's distance at a point in the part's own frame.</summary>
            private static float ColliderAt5(ref Primitive p, float lx, float ly, float lz)
            {
                // FAMILYSKIN begin
                if (TheatreFamilySkin.Enabled && p.ColKind == 3) return p.Fam.At(lx, ly, lz);
                // FAMILYSKIN end

                if (p.ColKind == 2) return Mathf.Sqrt(lx * lx + ly * ly + lz * lz) - p.ColA;

                if (p.ColKind == 1)
                {
                    float ry = ly - Mathf.Clamp(ly, -p.ColB, p.ColB);
                    return Mathf.Sqrt(lx * lx + ry * ry + lz * lz) - p.ColA;
                }

                float qx = Mathf.Abs(lx) - p.ColA, qy = Mathf.Abs(ly) - p.ColB, qz = Mathf.Abs(lz) - p.ColC;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f), oz = Mathf.Max(qz, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy + oz * oz) + Mathf.Min(Mathf.Max(qx, Mathf.Max(qy, qz)), 0f);
            }

            /// <summary>
            /// What a part eases to at a joint: its collider, and for a box the collider with its four edges along the face normal
            /// (local axis <paramref name="face"/>) rounded by ColRound, so that its section at the face is the face with rounded
            /// corners and two such faces that meet join with no groove. Inside the collider.
            /// </summary>
            private static float TargetAt5(ref Primitive p, float lx, float ly, float lz, int face)
            {
                if (p.ColKind != 0 || face < 0) return ColliderAt5(ref p, lx, ly, lz);

                float along = face == 0 ? lx : face == 1 ? ly : lz;
                float u = face == 0 ? ly : lx;
                float v = face == 2 ? ly : lz;
                float ha = face == 0 ? p.ColA : face == 1 ? p.ColB : p.ColC;
                float hu = face == 0 ? p.ColB : p.ColA;
                float hv = face == 2 ? p.ColB : p.ColC;

                float rho = Mathf.Min(p.ColRound, 0.5f * Mathf.Min(hu, hv));
                float qu = Mathf.Abs(u) - (hu - rho), qv = Mathf.Abs(v) - (hv - rho);
                float ou = Mathf.Max(qu, 0f), ov = Mathf.Max(qv, 0f);
                float across = Mathf.Sqrt(ou * ou + ov * ov) + Mathf.Min(Mathf.Max(qu, qv), 0f) - rho;
                float qa = Mathf.Abs(along) - ha;
                float oa = Mathf.Max(across, 0f), ob = Mathf.Max(qa, 0f);
                return Mathf.Sqrt(oa * oa + ob * ob) + Mathf.Min(Mathf.Max(across, qa), 0f);
            }

            /// <summary>A box's signed distance outside its face rectangle (local axis <paramref name="face"/>), across the face's normal.</summary>
            private static float BoxFaceOut5(ref Primitive p, float x, float y, float z, int face)
            {
                Local5(ref p, x, y, z, out float lx, out float ly, out float lz);

                float u = face == 0 ? ly : lx;
                float v = face == 2 ? ly : lz;
                float hu = face == 0 ? p.ColB : p.ColA;
                float hv = face == 2 ? p.ColB : p.ColC;
                float qu = Mathf.Abs(u) - hu, qv = Mathf.Abs(v) - hv;
                float ou = Mathf.Max(qu, 0f), ov = Mathf.Max(qv, 0f);
                return Mathf.Sqrt(ou * ou + ov * ov) + Mathf.Min(Mathf.Max(qu, qv), 0f);
            }

            /// <summary>
            /// How far a point lies outside a part's footprint at a joint, seen along the face's normal: a box's face rectangle; for
            /// a round part the patch round its contact (a point, or the shaft of a capsule touching side on) within which the
            /// collider is at least k/4 deep under its tangent plane, of radius sqrt(2 r k/4 - (k/4)^2). Within the footprint a
            /// point no more than k/4 inside the face's plane is inside the collider.
            /// </summary>
            private float FootprintAt5(ref Primitive p, float x, float y, float z, int face, Vector3 normal, float low, float high)
            {
                if (face >= 0) return BoxFaceOut5(ref p, x, y, z, face);

                var q = new Vector3(x - p.Cx, y - p.Cy, z - p.Cz);
                q -= normal * Vector3.Dot(q, normal);

                var axis = new Vector3(p.Yx, p.Yy, p.Yz);
                axis -= normal * Vector3.Dot(axis, normal);

                float a2 = Vector3.Dot(axis, axis);
                float t = a2 > 1e-12f ? Mathf.Clamp(Vector3.Dot(q, axis) / a2, low, high) : low;

                float patch = Mathf.Sqrt(Mathf.Max(0f, 2f * p.ColA * _quarter - _quarter * _quarter));
                return (q - axis * t).magnitude - patch;
            }

            /// <summary>
            /// The distance from a point to the smaller of a joint's two faces (the ramp's face, Fillet5.RampOnParent): a box's face
            /// rectangle, or a round part's point or line of contact on its tangent plane.
            /// </summary>
            private float PatchDistance5(Fillet5 fillet, float x, float y, float z)
            {
                bool onParent = fillet.RampOnParent;
                int slot = onParent ? fillet.Parent : fillet.Child;
                int face = onParent ? fillet.ParentFace : fillet.ChildFace;
                Vector3 normal = onParent ? fillet.ParentNormal : fillet.ChildNormal;
                ref Primitive p = ref _prims[slot];

                if (face >= 0)
                {
                    float plane = normal.x * x + normal.y * y + normal.z * z - (onParent ? fillet.ParentOffset : fillet.ChildOffset);
                    float lateral = Mathf.Max(BoxFaceOut5(ref p, x, y, z, face), 0f);
                    return Mathf.Sqrt(plane * plane + lateral * lateral);
                }

                float low = onParent ? fillet.ParentSegLow : fillet.ChildSegLow;
                float high = onParent ? fillet.ParentSegHigh : fillet.ChildSegHigh;

                var q = new Vector3(x - p.Cx - normal.x * p.ColA, y - p.Cy - normal.y * p.ColA, z - p.Cz - normal.z * p.ColA);
                var axis = new Vector3(p.Yx, p.Yy, p.Yz);
                float t = Mathf.Clamp(Vector3.Dot(q, axis), low, high);
                return (q - axis * t).magnitude;
            }

            /// <summary>The joint ramp at a point: one on the ramp's face, a smoothstep to zero at Fillet5.Reach from it.</summary>
            private float Ramp5(Fillet5 fillet, float x, float y, float z)
            {
                float away = PatchDistance5(fillet, x, y, z);
                if (!(away < fillet.Reach)) return 0f;

                float t = away / fillet.Reach;
                return 1f - t * t * (3f - 2f * t);
            }

            /// <summary>The largest joint ramp at a rest point, for the carve's ease (Carve5); 0 with no fillet.</summary>
            public float Ramp5At(Vector3 point)
            {
                float most = 0f;
                if (_fillets == null) return most;
                if (_prims == null) Cull(float.PositiveInfinity);

                foreach (Fillet5 fillet in _fillets) most = Mathf.Max(most, Ramp5(fillet, point.x, point.y, point.z));
                return most;
            }

            /// <summary>
            /// The wedge between a joint's two faces: beyond both faces' planes, to within k/4, and inside both footprints. Where
            /// the faces meet flat on it is a slab k/4 either side of the face they share, inside both colliders, which closes the
            /// two insets; where the joint stands open at rest, it is the opening between the faces.
            /// </summary>
            private float WedgeAt5(Fillet5 fillet, float x, float y, float z)
            {
                var at = new Vector3(x, y, z);
                float beyond = Mathf.Max(
                    fillet.ParentOffset - Vector3.Dot(fillet.ParentNormal, at),
                    fillet.ChildOffset - Vector3.Dot(fillet.ChildNormal, at)) - _quarter;

                float parentOut = FootprintAt5(ref _prims[fillet.Parent], x, y, z, fillet.ParentFace, fillet.ParentNormal,
                    fillet.ParentSegLow, fillet.ParentSegHigh);
                float childOut = FootprintAt5(ref _prims[fillet.Child], x, y, z, fillet.ChildFace, fillet.ChildNormal,
                    fillet.ChildSegLow, fillet.ChildSegHigh);

                return Mathf.Max(beyond, Mathf.Max(parentOut, childOut));
            }

            /// <summary>
            /// Part slot <paramref name="i"/>'s inset field eased toward its collider by every joint ramp it is in: a blend of two
            /// fields that are both positive outside the collider, so the eased shape lies between the drawn one and the collider.
            /// </summary>
            private float RampedAt5(int i, float x, float y, float z, float d)
            {
                for (int f = 0; f < _fillets.Count; f++)
                {
                    Fillet5 fillet = _fillets[f];
                    bool parent = fillet.Parent == i;
                    if (!parent && fillet.Child != i) continue;

                    float ramp = Ramp5(fillet, x, y, z);
                    if (!(ramp > 0f)) continue;

                    Local5(ref _prims[i], x, y, z, out float lx, out float ly, out float lz);
                    float target = TargetAt5(ref _prims[i], lx, ly, lz, parent ? fillet.ParentFace : fillet.ChildFace) + _quarter;
                    d += (target - d) * ramp;
                }

                return d;
            }

            /// <summary>
            /// The field at level 5: each part eased by its joint ramps, the smooth union at the global k with its clamp as before,
            /// then for each joint the fillet, the smooth minimum of its two parts at its radius times the ramp, held above the field
            /// of the two colliders and the wedge between their faces, so that it adds nothing outside them; the lowest of them all.
            /// The array is the primitives' inset distances (infinity where culled) and is eased in place.
            /// </summary>
            private float Rig5Value(float[] d, float x, float y, float z)
            {
                bool any = false;
                float fold = 0f;
                float least = float.PositiveInfinity;

                for (int i = 0; i < d.Length; i++)
                {
                    float value = d[i];
                    if (float.IsPositiveInfinity(value)) continue;

                    value = RampedAt5(i, x, y, z, value);
                    d[i] = value;

                    if (!any) { fold = value; any = true; }
                    else fold = SmoothMin(fold, value, _k);

                    if (value < least) least = value;
                }

                if (!any) return _cull;

                float result = Mathf.Max(fold, least - _quarter);

                for (int j = 0; j < _fillets.Count; j++)
                {
                    Fillet5 fillet = _fillets[j];
                    float a = d[fillet.Parent];
                    float b = d[fillet.Child];
                    if (float.IsPositiveInfinity(a) || float.IsPositiveInfinity(b)) continue;

                    float radius = fillet.Radius * Ramp5(fillet, x, y, z);
                    if (!(radius > 1e-6f)) continue;

                    float joined = SmoothMin(a, b, radius);
                    if (!(joined < result)) continue;

                    Local5(ref _prims[fillet.Parent], x, y, z, out float px, out float py, out float pz);
                    Local5(ref _prims[fillet.Child], x, y, z, out float cx, out float cy, out float cz);

                    float allowed = Mathf.Min(
                        Mathf.Min(ColliderAt5(ref _prims[fillet.Parent], px, py, pz), ColliderAt5(ref _prims[fillet.Child], cx, cy, cz)),
                        WedgeAt5(fillet, x, y, z));

                    joined = Mathf.Max(joined, allowed);
                    if (joined < result) result = joined;
                }
                // NOBOXJOIN begin
                if (_nbJoins != null) result = NbJoined(d, null, x, y, z, result);
                // NOBOXJOIN end

                return result;
            }

            private float Rig5At(float x, float y, float z)
            {
                if (_prims == null) Cull(float.PositiveInfinity);

                float[] d = Slots();

                for (int i = 0; i < _prims.Length; i++)
                {
                    bool near = !(x < _prims[i].CullLowX || x > _prims[i].CullHighX ||
                                  y < _prims[i].CullLowY || y > _prims[i].CullHighY ||
                                  z < _prims[i].CullLowZ || z > _prims[i].CullHighZ);

                    d[i] = near ? Distance(ref _prims[i], x, y, z) + _quarter : float.PositiveInfinity;
                }

                return Rig5Value(d, x, y, z);
            }

            private float[] Rig5SampleGrid(Vector3 origin, float cell, int nx, int ny, int nz)
            {
                if (_prims == null) Cull(float.PositiveInfinity);

                var samples = new float[nx * ny * nz];
                var row = new List<int>(_prims.Length);
                float[] d = Slots();

                for (int z = 0; z < nz; z++)
                {
                    float pz = origin.z + z * cell;

                    for (int y = 0; y < ny; y++)
                    {
                        float py = origin.y + y * cell;

                        row.Clear();
                        for (int i = 0; i < _prims.Length; i++)
                        {
                            if (py < _prims[i].CullLowY || py > _prims[i].CullHighY ||
                                pz < _prims[i].CullLowZ || pz > _prims[i].CullHighZ)
                            {
                                continue;
                            }

                            row.Add(i);
                        }

                        int at = nx * (y + ny * z);

                        for (int x = 0; x < nx; x++)
                        {
                            float px = origin.x + x * cell;

                            for (int i = 0; i < d.Length; i++) d[i] = float.PositiveInfinity;

                            for (int r = 0; r < row.Count; r++)
                            {
                                int i = row[r];
                                if (px < _prims[i].CullLowX || px > _prims[i].CullHighX) continue;

                                d[i] = Distance(ref _prims[i], px, py, pz) + _quarter;
                            }

                            samples[at + x] = Rig5Value(d, px, py, pz);
                        }
                    }
                }

                return samples;
            }
            // RIG5 end
            // RIG6 begin
            // ---- level 6: the sleeve at a waisted joint, and the field with each part in its own posed frame

            // One hinge per fillet, in AddFillet's order (null below level 6), and the scratch of each primitive's rest point.
            private List<Hinge6> _hinges6;
            private Vector3[] _at6;

            public int Hinge6Count => _hinges6 != null ? _hinges6.Count : 0;

            public Hinge6 HingeOf6(int f) => _hinges6[f];

            public Fillet5 FilletOf6(int f) => _fillets[f];

            /// <summary>The joint radius (level 4's, the weights' scale) of fillet <paramref name="f"/>: AddJoint and AddFillet go in pairs.</summary>
            public float JointRadiusOf6(int f) => _joints != null && f < _joints.Count ? _joints[f].Radius : 0f;

            /// <summary>
            /// Level 6: the hinge of the fillet just added (AddFillet). The larger part is the one whose face is not the ramp's. A sleeve
            /// is made only when the larger part is a drawn box (the shape a taper or an organic end narrows) whose face at the joint is
            /// a box face: a capsule of the smaller part's section (<see cref="Rig6SleeveShare"/>, at most <see cref="Rig6SleeveCap"/> of
            /// the larger part's smallest half extent) from the anchor's foot on the face, <see cref="Rig6SleeveDepth"/> of the larger
            /// part's half extent along the inward normal. It is kept only when the joint is waisted: one of eight points a sleeve radius
            /// under the face, <see cref="Rig6WaistProbe"/> of its radius from its axis, lies outside the larger part as drawn.
            /// </summary>
            public void AddHinge6(PartShape parentShape, Vector3 parentHalf, PartShape childShape, Vector3 childHalf)
            {
                if (_fillets == null || _fillets.Count == 0) return;
                if (_hinges6 == null) _hinges6 = new List<Hinge6>();

                Fillet5 fillet = _fillets[_fillets.Count - 1];
                bool largerIsParent = !fillet.RampOnParent;
                int larger = largerIsParent ? fillet.Parent : fillet.Child;
                int face = largerIsParent ? fillet.ParentFace : fillet.ChildFace;
                int smallFace = largerIsParent ? fillet.ChildFace : fillet.ParentFace;
                Vector3 normal = largerIsParent ? fillet.ParentNormal : fillet.ChildNormal;
                float offset = largerIsParent ? fillet.ParentOffset : fillet.ChildOffset;
                Vector3 largeHalf = largerIsParent ? parentHalf : childHalf;
                Vector3 smallHalf = largerIsParent ? childHalf : parentHalf;

                var hinge = new Hinge6
                {
                    Larger = larger,
                    ParentSize = FaceSize6(parentHalf, fillet.ParentFace),
                    ChildSize = FaceSize6(childHalf, fillet.ChildFace),
                };

                float section = smallFace >= 0
                    ? Mathf.Min(smallFace == 0 ? smallHalf.y : smallHalf.x, smallFace == 2 ? smallHalf.y : smallHalf.z)
                    : Mathf.Min(smallHalf.x, Mathf.Min(smallHalf.y, smallHalf.z));
                float largeSmallest = Mathf.Min(largeHalf.x, Mathf.Min(largeHalf.y, largeHalf.z));
                float radius = Mathf.Min(Rig6SleeveShare * section, Rig6SleeveCap * largeSmallest);

                Primitive p = _parts[larger];

                if (face >= 0 && p.Kind == KindDrawnBox && radius > 1e-6f)
                {
                    Vector3 inward = -normal;
                    Vector3 foot = fillet.Anchor - normal * (Vector3.Dot(normal, fillet.Anchor) - offset);

                    hinge.Base = foot;
                    hinge.Axis = inward;
                    hinge.Radius = radius;
                    hinge.Depth = Rig6SleeveDepth * (face == 0 ? p.ColA : face == 1 ? p.ColB : p.ColC);
                    hinge.Blend = Rig6SleeveBlend * radius;

                    Vector3 across = Vector3.Cross(inward, Mathf.Abs(inward.x) < 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 other = Vector3.Cross(inward, across);
                    Vector3 centre = foot + inward * radius;

                    for (int i = 0; i < 8 && !hinge.Waisted; i++)
                    {
                        float angle = 0.25f * Mathf.PI * i;
                        Vector3 probe = centre + (across * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * (Rig6WaistProbe * radius);
                        if (Distance(ref p, probe.x, probe.y, probe.z) + _quarter > 0f) hinge.Waisted = true;
                    }
                }

                // BEYOND begin
                if (Beyond6 > 0f)
                {
                    bool roundIsParent = fillet.RampOnParent;
                    int roundFace = roundIsParent ? fillet.ParentFace : fillet.ChildFace;
                    int flatFace = roundIsParent ? fillet.ChildFace : fillet.ParentFace;
                    float roundLow = roundIsParent ? fillet.ParentSegLow : fillet.ChildSegLow;
                    float roundHigh = roundIsParent ? fillet.ParentSegHigh : fillet.ChildSegHigh;
                    Primitive round = _parts[roundIsParent ? fillet.Parent : fillet.Child];

                    if (roundFace < 0 && flatFace >= 0 && roundLow == roundHigh && round.ColKind != 0) hinge.BeyondRadius = round.ColA;
                }
                // BEYOND end
                _hinges6.Add(hinge);
            }

            /// <summary>A box face's half diagonal (0 for a round part's point or line of contact), for the log.</summary>
            private static float FaceSize6(Vector3 h, int face)
            {
                if (face < 0) return 0f;

                float hu = face == 0 ? h.y : h.x;
                float hv = face == 2 ? h.y : h.z;
                return Mathf.Sqrt(hu * hu + hv * hv);
            }

            private Vector3[] At6()
            {
                if (_at6 == null || _at6.Length != _prims.Length) _at6 = new Vector3[_prims.Length];
                return _at6;
            }

            /// <summary>The sleeve's inset field at a rest point.</summary>
            private float SleeveAt6(Hinge6 hinge, Vector3 p)
            {
                Vector3 q = p - hinge.Base;
                float t = Mathf.Clamp(Vector3.Dot(q, hinge.Axis), 0f, hinge.Depth);
                return (q - hinge.Axis * t).magnitude - hinge.Radius + _quarter;
            }

            /// <summary>
            /// Part slot <paramref name="i"/>'s inset field eased by every joint ramp it is in, as level 5's RampedAt5, with two
            /// differences: each ramp is read at the point as its face's own part sees it (<paramref name="at"/>, one rest point per
            /// part; all the same at rest), and at a waisted joint the larger part eases only toward its collider within its drawn shape
            /// and the sleeve, so no bead stands round the contact.
            /// </summary>
            private float RampedAt6(int i, Vector3[] at, float d)
            {
                Vector3 p = at[i];

                for (int f = 0; f < _fillets.Count; f++)
                {
                    Fillet5 fillet = _fillets[f];
                    bool parent = fillet.Parent == i;
                    if (!parent && fillet.Child != i) continue;

                    Vector3 o = at[fillet.RampOnParent ? fillet.Parent : fillet.Child];
                    float ramp = Ramp5(fillet, o.x, o.y, o.z);
                    if (!(ramp > 0f)) continue;

                    Local5(ref _prims[i], p.x, p.y, p.z, out float lx, out float ly, out float lz);
                    float target = TargetAt5(ref _prims[i], lx, ly, lz, parent ? fillet.ParentFace : fillet.ChildFace) + _quarter;

                    Hinge6 hinge = _hinges6[f];
                    if (hinge.Waisted && hinge.Larger == i) target = Mathf.Max(target, Mathf.Min(d, SleeveAt6(hinge, p)));

                    d += (target - d) * ramp;
                }

                return d;
            }

            /// <summary>
            /// Part slot <paramref name="i"/> joined to the sleeve of every waisted joint whose larger part it is: the smooth minimum of the
            /// part and the sleeve cut to the part's collider, held above the part and its collider, so the sleeve adds nothing outside
            /// the collider.
            /// </summary>
            private float Sleeved6(int i, Vector3 p, float value)
            {
                for (int f = 0; f < _hinges6.Count; f++)
                {
                    Hinge6 hinge = _hinges6[f];
                    if (!hinge.Waisted || hinge.Larger != i) continue;

                    Local5(ref _prims[i], p.x, p.y, p.z, out float lx, out float ly, out float lz);
                    float collider = ColliderAt5(ref _prims[i], lx, ly, lz) + _quarter;
                    float sleeve = Mathf.Max(SleeveAt6(hinge, p), collider);
                    float joined = SmoothMin(value, sleeve, hinge.Blend);
                    value = Mathf.Max(joined, Mathf.Min(value, collider));
                }

                return value;
            }

            /// <summary>Level 5's wedge with each face read at the point as its own part sees it (the posed wedge; WedgeAt5 at rest).</summary>
            private float Wedge6(Fillet5 fillet, Vector3 atParent, Vector3 atChild)
            {
                float beyond = Mathf.Max(
                    fillet.ParentOffset - Vector3.Dot(fillet.ParentNormal, atParent),
                    fillet.ChildOffset - Vector3.Dot(fillet.ChildNormal, atChild)) - _quarter;

                float parentOut = FootprintAt5(ref _prims[fillet.Parent], atParent.x, atParent.y, atParent.z, fillet.ParentFace,
                    fillet.ParentNormal, fillet.ParentSegLow, fillet.ParentSegHigh);
                float childOut = FootprintAt5(ref _prims[fillet.Child], atChild.x, atChild.y, atChild.z, fillet.ChildFace,
                    fillet.ChildNormal, fillet.ChildSegLow, fillet.ChildSegHigh);

                return Mathf.Max(beyond, Mathf.Max(parentOut, childOut));
            }

            /// <summary>
            /// The field at level 6: level 5's (Rig5Value) with each part read at its own rest point (<paramref name="at"/>): each part
            /// eased by its ramps (RampedAt6) and joined to its sleeves (Sleeved6), the smooth union at the global k with its clamp, then
            /// each joint's fillet held above the two colliders and the wedge between their faces, each read in its own part's frame.
            /// At rest every point is the same and this is level 5's field plus the sleeves; posed, it is the union of the parts where
            /// they stand, so the outside of a bend rounds over within the opened wedge and the inside creases.
            /// </summary>
            private float Rig6Value(float[] d, Vector3[] at)
            {
                bool any = false;
                float fold = 0f;
                float least = float.PositiveInfinity;

                for (int i = 0; i < d.Length; i++)
                {
                    float value = d[i];
                    if (float.IsPositiveInfinity(value)) continue;

                    value = RampedAt6(i, at, value);
                    value = Sleeved6(i, at[i], value);
                    d[i] = value;

                    if (!any) { fold = value; any = true; }
                    else fold = SmoothMin(fold, value, _k);

                    if (value < least) least = value;
                }

                if (!any) return _cull;

                float result = Mathf.Max(fold, least - _quarter);

                for (int j = 0; j < _fillets.Count; j++)
                {
                    Fillet5 fillet = _fillets[j];
                    float a = d[fillet.Parent];
                    float b = d[fillet.Child];
                    if (float.IsPositiveInfinity(a) || float.IsPositiveInfinity(b)) continue;

                    Vector3 o = at[fillet.RampOnParent ? fillet.Parent : fillet.Child];
                    float radius = fillet.Radius * Ramp5(fillet, o.x, o.y, o.z);
                    // BEYOND begin
                    if (Beyond6 > 0f && _hinges6[j].BeyondRadius > 0f)
                    {
                        radius = Mathf.Max(fillet.Radius, 4f * Beyond6 * _hinges6[j].BeyondRadius) * Ramp5(fillet, o.x, o.y, o.z);
                    }
                    // BEYOND end
                    if (!(radius > 1e-6f)) continue;

                    float joined = SmoothMin(a, b, radius);
                    if (!(joined < result)) continue;

                    Vector3 ap = at[fillet.Parent];
                    Vector3 ac = at[fillet.Child];
                    Local5(ref _prims[fillet.Parent], ap.x, ap.y, ap.z, out float px, out float py, out float pz);
                    Local5(ref _prims[fillet.Child], ac.x, ac.y, ac.z, out float cx, out float cy, out float cz);

                    float allowed = Mathf.Min(
                        Mathf.Min(ColliderAt5(ref _prims[fillet.Parent], px, py, pz), ColliderAt5(ref _prims[fillet.Child], cx, cy, cz)),
                        Wedge6(fillet, ap, ac));
                    // BEYOND begin
                    if (Beyond6 > 0f && _hinges6[j].BeyondRadius > 0f) allowed -= Beyond6 * _hinges6[j].BeyondRadius;
                    // BEYOND end

                    joined = Mathf.Max(joined, allowed);
                    if (joined < result) result = joined;
                }
                // NOBOXJOIN begin
                if (_nbJoins != null) result = NbJoined(d, at, 0f, 0f, 0f, result);
                // NOBOXJOIN end

                return result;
            }

            private float Rig6At(float x, float y, float z)
            {
                if (_prims == null) Cull(float.PositiveInfinity);

                float[] d = Slots();
                Vector3[] at = At6();
                var point = new Vector3(x, y, z);

                for (int i = 0; i < _prims.Length; i++)
                {
                    bool near = !(x < _prims[i].CullLowX || x > _prims[i].CullHighX ||
                                  y < _prims[i].CullLowY || y > _prims[i].CullHighY ||
                                  z < _prims[i].CullLowZ || z > _prims[i].CullHighZ);

                    at[i] = point;
                    d[i] = near ? Distance(ref _prims[i], x, y, z) + _quarter : float.PositiveInfinity;
                }

                return Rig6Value(d, at);
            }

            private float[] Rig6SampleGrid(Vector3 origin, float cell, int nx, int ny, int nz)
            {
                if (_prims == null) Cull(float.PositiveInfinity);

                var samples = new float[nx * ny * nz];
                var row = new List<int>(_prims.Length);
                float[] d = Slots();
                Vector3[] at = At6();

                for (int z = 0; z < nz; z++)
                {
                    float pz = origin.z + z * cell;

                    for (int y = 0; y < ny; y++)
                    {
                        float py = origin.y + y * cell;

                        row.Clear();
                        for (int i = 0; i < _prims.Length; i++)
                        {
                            if (py < _prims[i].CullLowY || py > _prims[i].CullHighY ||
                                pz < _prims[i].CullLowZ || pz > _prims[i].CullHighZ)
                            {
                                continue;
                            }

                            row.Add(i);
                        }

                        int at0 = nx * (y + ny * z);

                        for (int x = 0; x < nx; x++)
                        {
                            float px = origin.x + x * cell;
                            var point = new Vector3(px, py, pz);

                            for (int i = 0; i < d.Length; i++)
                            {
                                d[i] = float.PositiveInfinity;
                                at[i] = point;
                            }

                            for (int r = 0; r < row.Count; r++)
                            {
                                int i = row[r];
                                if (px < _prims[i].CullLowX || px > _prims[i].CullHighX) continue;

                                d[i] = Distance(ref _prims[i], px, py, pz) + _quarter;
                            }

                            samples[at0 + x] = Rig6Value(d, at);
                        }
                    }
                }

                return samples;
            }

            /// <summary>
            /// Level 6: the field at a point of the skin object's space with every part where it stands now. <paramref name="toRest"/>
            /// carries the point into the rest frame as each part's bone sees it (the inverse of the bone's skinning matrix), so each
            /// part reads its own rest field there; the value is in rest metres. A primitive is a part at level 5 and above (no balls).
            /// </summary>
            public float PosedAt6(Vector3 x, Matrix4x4[] toRest)
            {
                if (_prims == null) Cull(float.PositiveInfinity);
                if (toRest == null || toRest.Length != _prims.Length) return _cull;

                float[] d = Slots();
                Vector3[] at = At6();

                for (int i = 0; i < _prims.Length; i++)
                {
                    Vector3 p = toRest[i].MultiplyPoint3x4(x);
                    bool near = !(p.x < _prims[i].CullLowX || p.x > _prims[i].CullHighX ||
                                  p.y < _prims[i].CullLowY || p.y > _prims[i].CullHighY ||
                                  p.z < _prims[i].CullLowZ || p.z > _prims[i].CullHighZ);

                    at[i] = p;
                    d[i] = near ? Distance(ref _prims[i], p.x, p.y, p.z) + _quarter : float.PositiveInfinity;
                }

                return Rig6Value(d, at);
            }
            // BEYOND begin
            /// <summary>
            /// The BEYOND test: how far a point of the skin object's space lies outside D242's bound, every part's collider and every
            /// joint's wedge, each read in its own part's frame through <paramref name="toRest"/> (rest metres, negative inside).
            /// </summary>
            public float PosedBound6(Vector3 x, Matrix4x4[] toRest)
            {
                if (_prims == null) Cull(float.PositiveInfinity);
                if (toRest == null || toRest.Length != _prims.Length) return float.NegativeInfinity;

                Vector3[] at = At6();
                float least = float.PositiveInfinity;

                for (int i = 0; i < _prims.Length; i++)
                {
                    at[i] = toRest[i].MultiplyPoint3x4(x);
                    Local5(ref _prims[i], at[i].x, at[i].y, at[i].z, out float lx, out float ly, out float lz);
                    least = Mathf.Min(least, ColliderAt5(ref _prims[i], lx, ly, lz));
                }

                if (_fillets != null)
                {
                    foreach (Fillet5 fillet in _fillets) least = Mathf.Min(least, Wedge6(fillet, at[fillet.Parent], at[fillet.Child]));
                }

                return least;
            }
            // BEYOND end
            // RIG6 end
            /// <summary>
            /// Pulls every vertex the field puts outside back onto the surface along the gradient,
            /// newton steps toward a value just below zero. Returns how many it moved.
            /// </summary>
            public int PullInside(Vector3[] positions, float step, float below)
            {
                int moved = 0;

                for (int v = 0; v < positions.Length; v++)
                {
                    Vector3 p = positions[v];
                    float f = At(p);
                    if (f <= 0f) continue;

                    for (int s = 0; s < PullSteps && f > 0f; s++)
                    {
                        Vector3 g = Gradient(p, step);
                        float g2 = Vector3.Dot(g, g);
                        if (!(g2 > 1e-12f)) break;

                        p -= g * ((f + below) / g2);
                        f = At(p);
                    }

                    positions[v] = p;
                    moved++;
                }

                return moved;
            }
        }

        /// <summary>The quadratic polynomial smooth minimum: never more than k/4 below min(a, b).</summary>
        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        private static float Distance(ref Primitive p, float x, float y, float z)
        {
            // PADDLE begin
            if (p.Paddle != null) return p.Paddle.At(x, y, z);
            // PADDLE end
            float dx = x - p.Cx, dy = y - p.Cy, dz = z - p.Cz;

            if (p.Kind == KindBall) return Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - p.A;

            float lx = dx * p.Xx + dy * p.Xy + dz * p.Xz;
            float ly = dx * p.Yx + dy * p.Yy + dz * p.Yz;
            float lz = dx * p.Zx + dy * p.Zy + dz * p.Zz;

            switch (p.Kind)
            {
                case KindDrawnBox:
                    return p.Drawn.At(lx, ly, lz);

                // FAMILYSKIN begin
                case KindFamily:
                    return p.Fam.At(lx, ly, lz);
                // FAMILYSKIN end

                case KindCapsule:
                {
                    // Squashed across by the palette's aspect: the round capsule's distance with x and z
                    // stretched back, times the smaller squash, which keeps it under the distance.
                    float sx = lx * p.UnsquashX, sz = lz * p.UnsquashZ;
                    float c = Mathf.Clamp(ly, -p.B, p.B);
                    float ry = ly - c;
                    return (Mathf.Sqrt(sx * sx + ry * ry + sz * sz) - p.A) * p.Squash;
                }

                case KindEllipsoid:
                {
                    // The usual bounded approximation (Quilez): k0 (k0 - 1) / k1, zero exactly on
                    // the surface and of the right sign everywhere, so the zero set is the ellipsoid.
                    float ex = lx / p.A, ey = ly / p.B, ez = lz / p.C;
                    float k0 = Mathf.Sqrt(ex * ex + ey * ey + ez * ez);
                    float fx = ex / p.A, fy = ey / p.B, fz = ez / p.C;
                    float k1 = Mathf.Sqrt(fx * fx + fy * fy + fz * fz);
                    if (!(k1 > 1e-20f)) return -Mathf.Min(p.A, Mathf.Min(p.B, p.C));
                    return k0 * (k0 - 1f) / k1;
                }

                // NOBOX begin
                case KindSuperEllipsoid:
                {
                    // |x/A|^p + |y/B|^p + |z/C|^p = 1, by the same bound as the ellipsoid's: (f - 1) / |grad f|, f the gauge
                    // (sum of |e|^p)^(1/p), whose gradient in metres is |e|^(p-1) f / (sum A) on each axis. At p = 2 that is
                    // k0 (k0 - 1) / k1 exactly; it is zero on the surface and of the right sign everywhere.
                    float ex = Mathf.Abs(lx / p.A), ey = Mathf.Abs(ly / p.B), ez = Mathf.Abs(lz / p.C);
                    float gx = Mathf.Pow(ex, p.Exp - 1f), gy = Mathf.Pow(ey, p.Exp - 1f), gz = Mathf.Pow(ez, p.Exp - 1f);
                    float sum = gx * ex + gy * ey + gz * ez;
                    if (!(sum > 1e-20f)) return -Mathf.Min(p.A, Mathf.Min(p.B, p.C));

                    float f = Mathf.Pow(sum, 1f / p.Exp);
                    float scale = f / sum;
                    float hx = gx * scale / p.A, hy = gy * scale / p.B, hz = gz * scale / p.C;
                    float slope = Mathf.Sqrt(hx * hx + hy * hy + hz * hz);
                    if (!(slope > 1e-20f)) return -Mathf.Min(p.A, Mathf.Min(p.B, p.C));
                    return (f - 1f) / slope;
                }
                // NOBOX end

                default:
                {
                    float qx = Mathf.Abs(lx) - p.A;
                    float qy = Mathf.Abs(ly) - p.B;
                    float qz = Mathf.Abs(lz) - p.C;
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f), oz = Mathf.Max(qz, 0f);
                    float outside = Mathf.Sqrt(ox * ox + oy * oy + oz * oz);
                    float inside = Mathf.Min(Mathf.Max(qx, Mathf.Max(qy, qz)), 0f);
                    return outside + inside - p.Round;
                }
            }
        }

        // PADDLE begin
        /// <summary>
        /// One link's paddle (<see cref="PaddleOn"/>), in the part's own frame and rest metres. Its field is the part's field as drawn
        /// today (<see cref="Today"/>) blended along the long axis into the blade's: the collider squashed across toward
        /// <see cref="E2"/>, so that the blade's section is an ellipse of semi-axes <see cref="Radius"/> on <see cref="E1"/> and
        /// <see cref="Minor"/> on E2, and its end a squashed half disc. The blade is inside the collider (a capsule is round about its
        /// axis and convex, and squashing it toward its axis keeps it inside; the box's ellipse is inside its section and its end
        /// inside its length), and today's shape is, so both fields are positive outside the collider and so is any blend of them:
        /// the drawn outline stays inside the collider.
        /// </summary>
        private sealed class PaddleShape
        {
            /// <summary>The part as it is drawn without the dial (its own Paddle null).</summary>
            public Primitive Today;

            public int Part;
            public bool Capsule;

            /// <summary>Unit vectors in the part's frame: the long axis, the blade's width and its thickness, and the long axis from the root toward the free end.</summary>
            public Vector3 Along, E1, E2, Far;

            /// <summary>The blade's half width on E1 (a capsule's radius, a box's middle half extent) and its straight half span along the long axis.</summary>
            public float Radius, Span;

            /// <summary>The blade's half thickness on E2 at the tip in the field (the visible one plus the k/4 inset), and Minor over Radius.</summary>
            public float Minor, Squash;

            /// <summary>The visible half thickness wanted at the tip, and the most the collider allows on E2 (a capsule's radius, a box's smallest half extent).</summary>
            public float Wanted, Limit;

            /// <summary>Along Far, from the part's centre: where the blend starts, and where it is whole (the blade's rounded end begins).</summary>
            public float T0, T1;

            /// <summary>Whether the grid's floor raised the tip's half thickness over the wanted one.</summary>
            public bool Floored;
            // WIDTH begin
            /// <summary>
            /// The blade's width at its free end over its width at the root: <see cref="PaddleWidth"/> on a round capsule, else 1
            /// (a box, a flat capsule, or the dial at 1), set by <see cref="PaddleOf"/>.
            /// </summary>
            public float WidthScale = 1f;
            // WIDTH end

            /// <summary>
            /// The tip's half thickness on the grid of this cell: the wanted one, at least ThinPartCells cells so the mesher keeps the
            /// sheet, and never past the collider.
            /// </summary>
            public void Floor(float cell, float quarter)
            {
                float visible = Mathf.Max(Wanted, ThinPartCells * cell);
                Floored = visible > Wanted;
                Minor = Mathf.Min(visible + quarter, Limit);
                Squash = Mathf.Max(Minor / Radius, 1e-3f);
            }

            /// <summary>The paddle's field at a rest point: today's up to T0, the blade's past T1, a smootherstep blend between.</summary>
            public float At(float x, float y, float z)
            {
                // WIDTH begin
                if (WidthScale > 1f) WidenPoint(ref x, ref y, ref z);
                // WIDTH end
                float d0 = Distance(ref Today, x, y, z);

                float dx = x - Today.Cx, dy = y - Today.Cy, dz = z - Today.Cz;
                float lx = dx * Today.Xx + dy * Today.Xy + dz * Today.Xz;
                float ly = dx * Today.Yx + dy * Today.Yy + dz * Today.Yz;
                float lz = dx * Today.Zx + dy * Today.Zy + dz * Today.Zz;

                float t = lx * Far.x + ly * Far.y + lz * Far.z;
                if (t <= T0) return d0;

                float u = Mathf.Clamp01((t - T0) / (T1 - T0));
                float s = u * u * u * (u * (u * 6f - 15f) + 10f);

                float a = lx * E1.x + ly * E1.y + lz * E1.z;
                float b = (lx * E2.x + ly * E2.y + lz * E2.z) / Squash;
                float w = lx * Along.x + ly * Along.y + lz * Along.z;
                float ru = w - Mathf.Clamp(w, -Span, Span);
                float d1 = (Mathf.Sqrt(a * a + ru * ru + b * b) - Radius) * Squash;

                return d0 + s * (d1 - d0);
            }
            // WIDTH begin

            /// <summary>
            /// The owner preview <see cref="PaddleWidth"/>: moves a rest point toward the axis along E1 so that the field read there is
            /// the dial-off blade's, stretched along E1 by 1 + (WidthScale - 1) s at the point's station along the long axis, s the
            /// same smootherstep as <see cref="At"/>'s blend. The long axis is square to E1, so the station is the same before and
            /// after, and the section at every station is the dial-off section stretched on E1 alone: the root (s = 0) is
            /// untouched and round, the half width reaches WidthScale times the dial-off width at the free end, and the half
            /// thickness on E2 is the dial-off one. The field is then not a distance, only its sign and zero set matter, as with
            /// <see cref="Squash"/>.
            /// </summary>
            private void WidenPoint(ref float x, ref float y, ref float z)
            {
                float dx = x - Today.Cx, dy = y - Today.Cy, dz = z - Today.Cz;
                float lx = dx * Today.Xx + dy * Today.Xy + dz * Today.Xz;
                float ly = dx * Today.Yx + dy * Today.Yy + dz * Today.Yz;
                float lz = dx * Today.Zx + dy * Today.Zy + dz * Today.Zz;

                float t = lx * Far.x + ly * Far.y + lz * Far.z;
                if (t <= T0) return;

                float u = Mathf.Clamp01((t - T0) / (T1 - T0));
                float s = u * u * u * (u * (u * 6f - 15f) + 10f);
                float stretch = 1f + (WidthScale - 1f) * s;

                float a = lx * E1.x + ly * E1.y + lz * E1.z;
                float shift = a - a / stretch;

                // E1 is in the part's frame; its rest-frame components are E1.x of the part's X axis, E1.y of its Y axis, E1.z of its Z.
                x -= shift * (E1.x * Today.Xx + E1.y * Today.Yx + E1.z * Today.Zx);
                y -= shift * (E1.x * Today.Xy + E1.y * Today.Yy + E1.z * Today.Zy);
                z -= shift * (E1.x * Today.Xz + E1.y * Today.Yz + E1.z * Today.Zz);
            }
            // WIDTH end
        }

        private static Vector3 PaddleAxis(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        /// <summary>
        /// One link's paddle, or null with the reason in <paramref name="said"/> (null when it is made). When it is made the
        /// primitive's rest box is grown to the collider's, the primitive as it stands is kept as <see cref="PaddleShape.Today"/>,
        /// and the primitive carries the paddle. The root is the end of the long axis at the link's joint to its merged parent, else
        /// at its first merged child's; the free end is the other. A link on whose blade another part stands (a child's anchor, or a
        /// parent's when the root is a child's, past T0) is left round, since its far end is not free.
        /// </summary>
        private static PaddleShape PaddleOf(
            Phenotype adult, int index, bool[] merged, float unscale, PartShape shape, Vector3 h, bool dressed, float k,
            ref Primitive primitive, out string said)
        {
            PhenotypePart part = adult.Parts[index];
            // FAMILYSKIN begin
            // A family limb takes no paddle: its shape is already the solid the world simulates, and a second taper would draw it
            // unlike its collider. This holds for the hinge too, which reshapes a made paddle alone.
            if (TheatreFamilySkin.Enabled && primitive.Kind == KindFamily)
            {
                said = string.Format(CultureInfo.InvariantCulture, "part {0} (a family limb, drawn as its own solid)", index);
                return null;
            }
            // FAMILYSKIN end
            var hf = new Float3(h.x, h.y, h.z);
            var paddle = new PaddleShape { Part = index };
            float quarter = 0.25f * k;
            float half, limit, wanted;
            Vector3 extent;

            if (shape is CapsuleShape)
            {
                float r = CapsuleShape.Radius(hf);
                float span = CapsuleShape.HalfSpan(hf);

                if (primitive.Kind != KindCapsule)
                {
                    said = string.Format(CultureInfo.InvariantCulture, "part {0} (a capsule drawn as one ellipsoid)", index);
                    return null;
                }

                if (!(r > 1e-5f) || span + r < PaddleLengthShare * r)
                {
                    said = string.Format(CultureInfo.InvariantCulture, "part {0} (half length {1:0.00} radii, under {2:0.0})",
                        index, r > 1e-5f ? (span + r) / r : 0f, PaddleLengthShare);
                    return null;
                }

                paddle.Capsule = true;
                paddle.Along = Vector3.up;
                paddle.Radius = r;
                paddle.Span = span;
                half = span + r;
                limit = r;
                extent = new Vector3(r, span + r, r);
                wanted = PaddleMinorShare * (r - quarter);

                // The palette's squash across the axis (PrimitiveOf's): a flat capsule keeps its wider extent as the blade's width
                // and never grows thicker across than it is drawn; a round one lays its blade in the plane of its hinge axis.
                float across = Mathf.Max(h.x, h.z);
                float sx = dressed && across > 0f ? h.x / across : 1f;
                float sz = dressed && across > 0f ? h.z / across : 1f;
                float narrow = Mathf.Min(sx, sz);

                if (narrow < PaddleRoundShare)
                {
                    paddle.E1 = sx >= sz ? Vector3.right : Vector3.forward;
                    wanted = Mathf.Min(wanted, narrow * r - quarter);
                }
                else
                {
                    paddle.E1 = Vector3.right;

                    if (FinHinge(adult, index, merged, unscale, out _, out Vector3[] axes, out _))
                    {
                        Vector3 hinge = axes[0];
                        float best = 2f;

                        for (int a = 0; a < axes.Length; a++)
                        {
                            float dot = Mathf.Abs(axes[a].y);
                            if (dot < best) { best = dot; hinge = axes[a]; }
                        }

                        var flat = new Vector3(hinge.x, 0f, hinge.z);
                        if (best <= Mathf.Cos(FinWebTwistDegrees * Mathf.Deg2Rad) && flat.sqrMagnitude > 1e-8f) paddle.E1 = flat.normalized;
                    }
                }

                paddle.E2 = Vector3.Cross(paddle.Along, paddle.E1).normalized;
                // WIDTH begin
                // A round capsule (the same test as above) takes the preview's width; a flat one keeps today's.
                if (PaddleWidth > 1f && !(narrow < PaddleRoundShare)) paddle.WidthScale = PaddleWidth;
                // WIDTH end
            }
            else if (shape is BoxShape)
            {
                if (primitive.Kind != KindDrawnBox && primitive.Kind != KindBox)
                {
                    said = string.Format(CultureInfo.InvariantCulture, "part {0} (a box not drawn as a box)", index);
                    return null;
                }

                var sizes = new[] { h.x, h.y, h.z };
                var order = new[] { 0, 1, 2 };
                Array.Sort(sizes, order);

                if (!(sizes[1] > 1e-5f) || sizes[2] < PaddleLengthShare * sizes[1])
                {
                    said = string.Format(CultureInfo.InvariantCulture, "part {0} (a box of half length {1:0.00} half widths, under {2:0.0})",
                        index, sizes[1] > 1e-5f ? sizes[2] / sizes[1] : 0f, PaddleLengthShare);
                    return null;
                }

                // The widest face is the long and the middle axes': the blade's width lies along the middle one, its thickness the smallest.
                paddle.Capsule = false;
                paddle.Along = PaddleAxis(order[2]);
                paddle.E1 = PaddleAxis(order[1]);
                paddle.E2 = PaddleAxis(order[0]);
                paddle.Radius = sizes[1];
                paddle.Span = sizes[2] - sizes[1];
                half = sizes[2];
                limit = sizes[0];
                extent = h;
                wanted = Mathf.Min(PaddleMinorShare * (sizes[1] - quarter), sizes[0] - quarter);
            }
            else
            {
                said = string.Format(CultureInfo.InvariantCulture, "part {0} (a {1}, with no long axis)", index, shape != null ? shape.Id : "shape unknown");
                return null;
            }

            if (!(wanted > 0f))
            {
                said = string.Format(CultureInfo.InvariantCulture, "part {0} (too thin for the skin's inset)", index);
                return null;
            }

            // The root: the joint to the merged parent, else to the first merged child.
            Vector3 anchor = Vector3.zero;
            int rootChild = -1;
            bool fromParent = part.ParentIndex >= 0 && part.ParentIndex < merged.Length && merged[part.ParentIndex];

            if (fromParent)
            {
                anchor = part.ChildAnchorLocal.ToVector3() * unscale;
            }
            else
            {
                for (int c = 0; c < adult.PartCount && rootChild < 0; c++)
                {
                    if (adult.Parts[c].ParentIndex != index || c >= merged.Length || !merged[c]) continue;

                    rootChild = c;
                    anchor = adult.Parts[c].ParentAnchorLocal.ToVector3() * unscale;
                }

                if (rootChild < 0)
                {
                    said = string.Format(CultureInfo.InvariantCulture, "part {0} (no joint to a merged part)", index);
                    return null;
                }
            }

            float along = Vector3.Dot(anchor, paddle.Along);

            if (Mathf.Abs(along) < PaddleRootShare * half)
            {
                said = string.Format(CultureInfo.InvariantCulture, "part {0} (joined at its side, {1:0.00} of its half length from its centre)",
                    index, half > 0f ? Mathf.Abs(along) / half : 0f);
                return null;
            }

            paddle.Far = along > 0f ? -paddle.Along : paddle.Along;
            paddle.T1 = paddle.Span;
            paddle.T0 = -half + PaddleStartShare * (paddle.T1 + half);

            for (int c = 0; c < adult.PartCount; c++)
            {
                if (c == rootChild || adult.Parts[c].ParentIndex != index) continue;
                if (!(Vector3.Dot(adult.Parts[c].ParentAnchorLocal.ToVector3() * unscale, paddle.Far) > paddle.T0)) continue;

                said = string.Format(CultureInfo.InvariantCulture, "part {0} (part {1} stands on its blade)", index, c);
                return null;
            }

            if (!fromParent && part.ParentIndex >= 0 && Vector3.Dot(part.ChildAnchorLocal.ToVector3() * unscale, paddle.Far) > paddle.T0)
            {
                said = string.Format(CultureInfo.InvariantCulture, "part {0} (its parent, part {1}, stands on its blade)", index, part.ParentIndex);
                return null;
            }

            paddle.Wanted = wanted;
            paddle.Limit = limit;
            paddle.Floor(0f, quarter);

            // The blade may reach the collider where today's shape does not (a round capsule's blade across its squash), so the rest
            // box is grown to the collider's, as level 5's ColliderOf5 grows it.
            // WIDTH begin
            // The widened blade reaches WidthScale times the radius a side on E1, which lies in the part's x-z plane, so the rest box is
            // grown on x and z by it (the E1 and E2 reach of a diagonal E1 is within that on each of the two axes).
            if (paddle.WidthScale > 1f) extent = new Vector3(extent.x * paddle.WidthScale, extent.y, extent.z * paddle.WidthScale);
            // WIDTH end
            var reach = new Vector3(
                Mathf.Abs(primitive.Xx) * extent.x + Mathf.Abs(primitive.Yx) * extent.y + Mathf.Abs(primitive.Zx) * extent.z,
                Mathf.Abs(primitive.Xy) * extent.x + Mathf.Abs(primitive.Yy) * extent.y + Mathf.Abs(primitive.Zy) * extent.z,
                Mathf.Abs(primitive.Xz) * extent.x + Mathf.Abs(primitive.Yz) * extent.y + Mathf.Abs(primitive.Zz) * extent.z);
            var centre = new Vector3(primitive.Cx, primitive.Cy, primitive.Cz);
            primitive.Low = Vector3.Min(primitive.Low, centre - reach);
            primitive.High = Vector3.Max(primitive.High, centre + reach);

            paddle.Today = primitive;
            primitive.Paddle = paddle;
            said = null;
            return paddle;
        }

        /// <summary>How far a point of a part's own frame (rest metres) stands outside its collider, negative inside.</summary>
        private static float PaddleColliderOutside(
            PartShape shape, Vector3 h, Vector3 l,
            // FAMILYSKIN begin
            TheatreFamilySkin.Solid family = null
            // FAMILYSKIN end
            )
        {
            // FAMILYSKIN begin
            // A family part's collider is its solid, not the box of its half extents.
            if (TheatreFamilySkin.Enabled && family != null) return family.At(l.x, l.y, l.z);
            // FAMILYSKIN end

            var hf = new Float3(h.x, h.y, h.z);

            if (shape is SphereShape) return l.magnitude - SphereShape.Radius(hf);

            if (shape is CapsuleShape)
            {
                float r = CapsuleShape.Radius(hf), span = CapsuleShape.HalfSpan(hf);
                float ry = l.y - Mathf.Clamp(l.y, -span, span);
                return Mathf.Sqrt(l.x * l.x + ry * ry + l.z * l.z) - r;
            }

            var e = new Vector3(Mathf.Abs(l.x) - h.x, Mathf.Abs(l.y) - h.y, Mathf.Abs(l.z) - h.z);
            return Vector3.Max(e, Vector3.zero).magnitude + Mathf.Min(Mathf.Max(e.x, Mathf.Max(e.y, e.z)), 0f);
        }

        /// <summary>
        /// The body's one paddle line (D242's check): the links reshaped, each tip's visible half width and half thickness (after the
        /// k/4 inset), the cell, why each other link stays round, and over the vertices whose largest weight is a reshaped link's:
        /// on the blade (past T0) the most any stands outside its own collider and outside every merged part's, and at the root,
        /// which keeps today's shape and the hinge's bridge, the most outside its own. Nothing for a body with no link in the skin.
        /// </summary>
        private static void PaddleLog(
            long id, Phenotype adult, List<int> mergedParts, PartShapeRegistry shapes, float unscale, Vector3[] centres,
            Quaternion[] rotations, PaddleShape[] paddles, List<string> notes, Vector3[] positions, BoneWeight[] weights,
            float cell, float cellToday, float k)
        {
            int n = paddles.Length;
            var kinds = new PartShape[n];
            var halves = new Vector3[n];
            var inverse = new Quaternion[n];
            var tips = new System.Text.StringBuilder();
            int reshaped = 0;
            float quarter = 0.25f * k;
            // FAMILYSKIN begin
            TheatreFamilySkin.Solid[] families = TheatreFamilySkin.Enabled ? new TheatreFamilySkin.Solid[n] : null;
            // FAMILYSKIN end

            for (int j = 0; j < n; j++)
            {
                PhenotypePart part = adult.Parts[mergedParts[j]];
                kinds[j] = shapes.Resolve(part.ShapeId);
                halves[j] = Abs(part.HalfExtents.ToVector3()) * unscale;
                inverse[j] = Quaternion.Inverse(rotations[j]);
                // FAMILYSKIN begin
                if (families != null) families[j] = TheatreFamilySkin.SolidOf(part, halves[j]);
                // FAMILYSKIN end

                PaddleShape paddle = paddles[j];
                if (paddle == null) continue;

                reshaped++;
                float major = paddle.Radius - quarter, minor = paddle.Minor - quarter;
                // WIDTH begin
                if (paddle.WidthScale > 1f) major *= paddle.WidthScale;
                // WIDTH end
                tips.AppendFormat(CultureInfo.InvariantCulture, "; part {0} {1}: tip half width {2:0.0000} m, half thickness {3:0.0000} m ({4:0.00}){5}",
                    paddle.Part, paddle.Capsule ? "capsule" : "box", major, minor, major > 0f ? minor / major : 0f,
                    paddle.Floored ? string.Format(CultureInfo.InvariantCulture, ", floored at {0:0.0} cells", ThinPartCells) : "");
            }

            // WIDTH begin
            if (PaddleWidth > 1f)
            {
                tips.AppendFormat(CultureInfo.InvariantCulture,
                    "; owner preview EVOSIM_THEATRE_PADDLE_WIDTH {0:0.00}: a round capsule's blade is {0:0.00} times its collider's radius at the free end", PaddleWidth);
            }
            // WIDTH end
            if (reshaped + notes.Count == 0) return;

            float blade = 0f, every = 0f, root = 0f;
            int bladeCount = 0;

            for (int v = 0; v < positions.Length; v++)
            {
                int j = weights[v].boneIndex0;
                if (j < 0 || j >= n || paddles[j] == null) continue;

                Vector3 local = inverse[j] * (positions[v] - centres[j]);
                float own = PaddleColliderOutside(kinds[j], halves[j], local, families?[j]);

                if (Vector3.Dot(local, paddles[j].Far) <= paddles[j].T0)
                {
                    root = Mathf.Max(root, own);
                    continue;
                }

                bladeCount++;
                blade = Mathf.Max(blade, own);

                float least = own;
                for (int o = 0; o < n && least > 0f; o++)
                {
                    if (o == j) continue;
                    least = Mathf.Min(least, PaddleColliderOutside(kinds[o], halves[o], inverse[o] * (positions[v] - centres[o]), families?[o]));
                }

                every = Mathf.Max(every, least);
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] paddle limb: body {0}, {1} of {2} links reshaped, cell {3:0.0000} m (usual {4:0.0000} m){5}; blade: {6} vertices, " +
                "most outside its own collider {7:0.00000} m, outside every collider {8:0.00000} m; root (today's shape and the hinge's bridge): " +
                "most outside its own collider {9:0.00000} m{10}",
                id, reshaped, reshaped + notes.Count, cell, cellToday, tips, bladeCount, blade, every, root,
                notes.Count > 0 ? "; left round: " + string.Join(", ", notes) : ""));
        }
        // PADDLE end
        // ---------------------------------------------------------------- the mesh

        /// <summary>
        /// Surface nets: a vertex in every cell whose corners change sign, at the mean of its edges'
        /// crossings, and a quad across every grid edge that changes sign, wound so its normal
        /// points out of the body (toward the positive field).
        /// </summary>
        private static void SurfaceNets(
            float[] f, Vector3 origin, float cell, int nx, int ny, int nz, List<Vector3> vertices, List<int> quads)
        {
            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            if (cx < 1 || cy < 1 || cz < 1) return;

            var vertexOf = new int[cx * cy * cz];
            for (int i = 0; i < vertexOf.Length; i++) vertexOf[i] = -1;

            int Sample(int x, int y, int z) => x + nx * (y + ny * z);
            int Cell(int x, int y, int z) => x + cx * (y + cy * z);

            var corner = new float[8];

            for (int z = 0; z < cz; z++)
            for (int y = 0; y < cy; y++)
            for (int x = 0; x < cx; x++)
            {
                int mask = 0;
                for (int c = 0; c < 8; c++)
                {
                    float value = f[Sample(x + (c & 1), y + ((c >> 1) & 1), z + ((c >> 2) & 1))];
                    corner[c] = value;
                    if (value < 0f) mask |= 1 << c;
                }

                if (mask == 0 || mask == 255) continue;

                Vector3 sum = Vector3.zero;
                int crossings = 0;

                for (int a = 0; a < 8; a++)
                {
                    for (int bit = 1; bit < 8; bit <<= 1)
                    {
                        if ((a & bit) != 0) continue;

                        int b = a | bit;
                        bool insideA = corner[a] < 0f;
                        bool insideB = corner[b] < 0f;
                        if (insideA == insideB) continue;

                        float t = corner[a] / (corner[a] - corner[b]);
                        var pa = new Vector3(a & 1, (a >> 1) & 1, (a >> 2) & 1);
                        var pb = new Vector3(b & 1, (b >> 1) & 1, (b >> 2) & 1);
                        sum += pa + (pb - pa) * t;
                        crossings++;
                    }
                }

                Vector3 local = sum / crossings;
                vertexOf[Cell(x, y, z)] = vertices.Count;
                vertices.Add(origin + new Vector3(x + local.x, y + local.y, z + local.z) * cell);
            }

            // Every grid edge, by its axis a and the other two (b, c) in cyclic order, so that the cells
            // q0 (-b -c), q1 (-c), q2 (0), q3 (-b) go round the edge with e_b x e_c = e_a.
            for (int axis = 0; axis < 3; axis++)
            {
                int ax = axis == 0 ? 1 : 0, ay = axis == 1 ? 1 : 0, az = axis == 2 ? 1 : 0;
                int b = (axis + 1) % 3;
                int c = (axis + 2) % 3;
                int bx = b == 0 ? 1 : 0, by = b == 1 ? 1 : 0, bz = b == 2 ? 1 : 0;
                int qx = c == 0 ? 1 : 0, qy = c == 1 ? 1 : 0, qz = c == 2 ? 1 : 0;

                for (int z = 0; z + az < nz; z++)
                for (int y = 0; y + ay < ny; y++)
                for (int x = 0; x + ax < nx; x++)
                {
                    bool inside0 = f[Sample(x, y, z)] < 0f;
                    bool inside1 = f[Sample(x + ax, y + ay, z + az)] < 0f;
                    if (inside0 == inside1) continue;

                    // The four cells round the edge must all exist.
                    int x0 = x - bx - qx, y0 = y - by - qy, z0 = z - bz - qz;
                    if (x0 < 0 || y0 < 0 || z0 < 0) continue;
                    if (x >= cx || y >= cy || z >= cz) continue;

                    int v0 = vertexOf[Cell(x - bx - qx, y - by - qy, z - bz - qz)];
                    int v1 = vertexOf[Cell(x - qx, y - qy, z - qz)];
                    int v2 = vertexOf[Cell(x, y, z)];
                    int v3 = vertexOf[Cell(x - bx, y - by, z - bz)];
                    if (v0 < 0 || v1 < 0 || v2 < 0 || v3 < 0) continue;

                    if (inside0)
                    {
                        // Inside at the start: out along +a.
                        quads.Add(v0); quads.Add(v1); quads.Add(v2); quads.Add(v3);
                    }
                    else
                    {
                        quads.Add(v0); quads.Add(v3); quads.Add(v2); quads.Add(v1);
                    }
                }
            }
        }

        /// <summary>Taubin's smoothing, a shrinking step and an inflating one, <see cref="TaubinPasses"/> times.</summary>
        private static void Taubin(Vector3[] positions, List<int> quads)
        {
            int count = positions.Length;
            var edges = new HashSet<long>();

            void Edge(int a, int b)
            {
                if (a == b) return;
                if (a > b) { int s = a; a = b; b = s; }
                edges.Add(((long)a << 32) | (uint)b);
            }

            for (int q = 0; q < quads.Count; q += 4)
            {
                Edge(quads[q], quads[q + 1]);
                Edge(quads[q + 1], quads[q + 2]);
                Edge(quads[q + 2], quads[q + 3]);
                Edge(quads[q + 3], quads[q]);
            }

            var degree = new int[count];
            foreach (long e in edges)
            {
                degree[(int)(e >> 32)]++;
                degree[(int)(e & 0xFFFFFFFF)]++;
            }

            var start = new int[count + 1];
            for (int i = 0; i < count; i++) start[i + 1] = start[i] + degree[i];

            var fill = new int[count];
            var neighbours = new int[start[count]];
            foreach (long e in edges)
            {
                int a = (int)(e >> 32), b = (int)(e & 0xFFFFFFFF);
                neighbours[start[a] + fill[a]++] = b;
                neighbours[start[b] + fill[b]++] = a;
            }

            var next = new Vector3[count];

            void Step(float factor)
            {
                for (int i = 0; i < count; i++)
                {
                    int n = start[i + 1] - start[i];
                    if (n == 0) { next[i] = positions[i]; continue; }

                    Vector3 mean = Vector3.zero;
                    for (int j = start[i]; j < start[i + 1]; j++) mean += positions[neighbours[j]];
                    mean /= n;

                    next[i] = positions[i] + factor * (mean - positions[i]);
                }

                Array.Copy(next, positions, count);
            }

            for (int pass = 0; pass < TaubinPasses; pass++)
            {
                Step(TaubinLambda);
                Step(TaubinMu);
            }
        }

        // NOBOXJOIN begin
        /// <summary>One no-box join: the two parts' slots, the anchor in the rest frame, the pair blend's radius and the ball's (0 for none).</summary>
        private struct NbJoin
        {
            public int Parent, Child;
            public Vector3 Anchor;
            public float Radius, Ball;
        }

        /// <summary>Whether the first no-box join has been logged, so that the line comes once.</summary>
        private static bool _nbJoinLogged;
        // NOBOXJOIN end

        // RIG4 begin
        // ---------------------------------------------------------------- the rig (level 4)

        /// <summary>One joint between two merged parts: their slots, the anchor in the rest frame, the joint's radius, and the weights' scale.</summary>
        private struct RigJoint
        {
            public int Parent, Child;
            public Vector3 Anchor;
            public float Radius, WeightK;
        }

        /// <summary>
        /// Whether a part joins the mesh at level 4. With <c>EVOSIM_THEATRE_SKIN_BODY</c> set, that list rules, as at the
        /// lower levels. Otherwise every part does but three kinds, each drawn by a path the skin has no field for. A
        /// holdfast (anchoring) is a rough tissue of its own look, seated in the bed, and its stalk meets it in a neck. A
        /// nose is drawn at its true size with an olfactory pit cupped into its face (<c>TheatreNosePit</c>), on the part's
        /// own renderer. A blade (a producer drawn as a leaf, <c>TheatrePalette.DrawnAsLeaf</c>) is the lamina path's
        /// sheet, with its veins, curl and sway. A producer drawn as anything else (a round or capsule one) merges. A flat
        /// box of another type that is drawn as a leaf keeps its place in the mesh as a rounded box, as it does at every
        /// level.
        /// </summary>
        private static bool RigMerges(PhenotypePart part, Vector3 h, HashSet<string> body)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SKIN_BODY")))
            {
                return body.Contains(part.CellTypeId);
            }

            string type = part.CellTypeId;
            // noseskin:begin
            // NOSE_PIT 2 on the rig: the nose joins the skin, at its true size, by its collider as any merged part (its bone
            // carries it, with the rig's weights), and the pit is carved in the skin (TheatreNosePit). A nose under 1.5 cells
            // is still left out below and stands apart with level 1's pit.
            if (type == CellTypeIds.Nose && TheatreNosePit.OnSkin) return true;
            // noseskin:end
            if (type == CellTypeIds.Anchoring || type == CellTypeIds.Nose) return false;
            if (type == CellTypeIds.Photosynthetic && TheatrePalette.DrawnAsLeaf(part, 2f * h)) return false;

            return true;
        }

        // WHOLE begin
        /// <summary>
        /// RigMerges for an animal under <see cref="WholeOn"/>: every part the rig merges, and a producer too, whether or not it is
        /// drawn as a blade (a blade is its collider in the skin, as any merged part is). Called only with the dial on, which is
        /// off while <c>EVOSIM_THEATRE_SKIN_BODY</c> is set (that list rules).
        /// </summary>
        private static bool RigMerges(PhenotypePart part, Vector3 h, HashSet<string> body, bool whole)
        {
            if (RigMerges(part, h, body)) return true;

            return whole && part.CellTypeId == CellTypeIds.Photosynthetic;
        }

        /// <summary>
        /// The grid's cell for an animal under <see cref="WholeOn"/>, from the thinnest merged part's half extent and the body's
        /// longest extent: the usual cell, or when that leaves the thinnest part under <see cref="ThinPartCells"/> cells, the cell that
        /// gives it that many, and never under the longest extent over <see cref="WholeCapDivisor"/> (so that the grid keeps to
        /// <see cref="WholeMaxCells"/> cells an axis, its padding included).
        /// </summary>
        private static float WholeCell(float smallest, float largest)
        {
            float usual = Mathf.Max(CellOfSmallest * smallest, largest / CellsAcrossBody);
            float wanted = smallest / ThinPartCells;
            float floor = largest / WholeCapDivisor;

            return Mathf.Max(Mathf.Min(usual, wanted), floor);
        }

        // WHOLE end
        /// <summary>The level 4 line of the log: the joints, their radii, and which types are in the mesh and which stand apart.</summary>
        private static string RigLog(long id, Phenotype adult, bool[] merged, Field field)
        {
            var inside = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var apart = new SortedDictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < adult.PartCount; i++)
            {
                var into = merged[i] ? inside : apart;
                string type = adult.Parts[i].CellTypeId;
                into[type] = into.TryGetValue(type, out int have) ? have + 1 : 1;
            }

            string Tally(SortedDictionary<string, int> map)
            {
                if (map.Count == 0) return "none";

                var parts = new List<string>();
                foreach (KeyValuePair<string, int> pair in map) parts.Add(pair.Key + " " + pair.Value);
                return string.Join(", ", parts);
            }

            return string.Format(CultureInfo.InvariantCulture,
                "[Theatre] one skin rig: body {0}, {1} joints, joint radius {2:0.0000} to {3:0.0000} m, " +
                "in the mesh: {4}; standing apart: {5}",
                id, field.JointCount, field.JointCount > 0 ? field.JointRadiusLeast() : 0f, field.JointRadiusMost(),
                Tally(inside), Tally(apart));
        }

        // RIG4 end
        // RIG5 begin
        // ---------------------------------------------------------------- the fillet (level 5)

        /// <summary>
        /// One joint's fillet and ramp (level 5): its parts' slots, the anchor, the fillet's radius, the ramp's reach and which
        /// part's face it is measured from (the smaller face), and each part's face at the joint: its plane (an outward unit
        /// normal and its offset, n . p = offset, in the rest frame), a box's local face axis (-1 for a round part) and a round
        /// part's contact segment on its local y.
        /// </summary>
        private struct Fillet5
        {
            public int Parent, Child;
            public Vector3 Anchor;
            public float Radius, Reach;
            public bool RampOnParent;
            public Vector3 ParentNormal, ChildNormal;
            public float ParentOffset, ChildOffset;
            public int ParentFace, ChildFace;
            public float ParentSegLow, ParentSegHigh, ChildSegLow, ChildSegHigh;
        }

        /// <summary>Level 5: the part's collider on its primitive (Primitive.ColKind), and its rest box grown to hold it.</summary>
        private static void ColliderOf5(ref Primitive p, PartShape shape, Vector3 h, Vector3 centre)
        {
            // FAMILYSKIN begin
            // A family part's collider is its solid, the field the skin draws it by (D252: the skin clamps inside the simulated
            // solid). The rest box PrimitiveOf made already holds the solid, taper and shift included.
            if (TheatreFamilySkin.Enabled && p.Kind == KindFamily)
            {
                p.ColKind = 3;
                p.ColA = h.x;
                p.ColB = h.y;
                p.ColC = h.z;
                return;
            }
            // FAMILYSKIN end

            var hf = new Float3(h.x, h.y, h.z);
            Vector3 extent;

            switch (shape)
            {
                case SphereShape _:
                {
                    float r = SphereShape.Radius(hf);
                    p.ColKind = 2;
                    p.ColA = r;
                    extent = new Vector3(r, r, r);
                    break;
                }

                case CapsuleShape _:
                {
                    float r = CapsuleShape.Radius(hf);
                    float span = CapsuleShape.HalfSpan(hf);
                    p.ColKind = 1;
                    p.ColA = r;
                    p.ColB = span;
                    extent = new Vector3(r, span + r, r);
                    break;
                }

                default:
                {
                    float small = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
                    float large = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                    float cubicness = large > 1e-6f ? small / large : 1f;

                    p.ColKind = 0;
                    p.ColA = h.x;
                    p.ColB = h.y;
                    p.ColC = h.z;
                    p.ColRound = Mathf.Clamp(TheatreMeshes.PillowFor(cubicness), 0f, 0.5f) * small;
                    extent = h;
                    break;
                }
            }

            var reach = new Vector3(
                Mathf.Abs(p.Xx) * extent.x + Mathf.Abs(p.Yx) * extent.y + Mathf.Abs(p.Zx) * extent.z,
                Mathf.Abs(p.Xy) * extent.x + Mathf.Abs(p.Yy) * extent.y + Mathf.Abs(p.Zy) * extent.z,
                Mathf.Abs(p.Xz) * extent.x + Mathf.Abs(p.Yz) * extent.y + Mathf.Abs(p.Zz) * extent.z);

            p.Low = Vector3.Min(p.Low, centre - reach);
            p.High = Vector3.Max(p.High, centre + reach);
        }

        /// <summary>
        /// A part's face at a joint, in its own frame. For a box: the face whose axis the anchor lies furthest along (over the
        /// half extent), its plane at that half extent, its size the face's half diagonal. For a capsule or a sphere: the tangent
        /// plane at the surface point nearest the anchor, size 0, and its contact segment on local y (the shaft, when the anchor
        /// lies on it, else the one point). An anchor near the part's middle has no face, and the direction toward the other
        /// part's centre stands in for it.
        /// </summary>
        private static void Face5(
            PartShape shape, Vector3 h, Vector3 anchorLocal, Vector3 towardLocal,
            out int face, out Vector3 normal, out float offset, out float size, out float segLow, out float segHigh,
            // FAMILYSKIN begin
            TheatreFamilySkin.Solid family = null
            // FAMILYSKIN end
            )
        {
            var hf = new Float3(h.x, h.y, h.z);
            face = -1;
            segLow = 0f;
            segHigh = 0f;

            // FAMILYSKIN begin
            if (TheatreFamilySkin.Enabled && family != null)
            {
                // A family part's face is the box's rule (the default case below) on the solid's own reach: the widest slice across
                // (SideReach times the half extent) and the two poles along Y, which the taper and the shift put at different
                // distances. The plane touches the solid there, so a joint's ramp and wedge stand on its surface and not on a box
                // the solid does not fill. The part's collider (ColKind 3) is the solid, so the ramp eases toward that.
                Vector3 w = anchorLocal;
                float reachX = h.x * family.SideReach, reachZ = h.z * family.SideReach;
                float sideX = Mathf.Abs(w.x) / Mathf.Max(reachX, 1e-9f);
                float sideY = Mathf.Abs(w.y) / Mathf.Max(w.y >= 0f ? family.PoleHigh : family.PoleLow, 1e-9f);
                float sideZ = Mathf.Abs(w.z) / Mathf.Max(reachZ, 1e-9f);

                if (Mathf.Max(sideX, Mathf.Max(sideY, sideZ)) < 0.5f)
                {
                    w = towardLocal;
                    sideX = Mathf.Abs(w.x) / Mathf.Max(reachX, 1e-9f);
                    sideY = Mathf.Abs(w.y) / Mathf.Max(w.y >= 0f ? family.PoleHigh : family.PoleLow, 1e-9f);
                    sideZ = Mathf.Abs(w.z) / Mathf.Max(reachZ, 1e-9f);
                }

                face = sideX >= sideY && sideX >= sideZ ? 0 : sideY >= sideZ ? 1 : 2;
                float pole = (face == 0 ? w.x : face == 1 ? w.y : w.z) < 0f ? -1f : 1f;
                normal = face == 0 ? new Vector3(pole, 0f, 0f) : face == 1 ? new Vector3(0f, pole, 0f) : new Vector3(0f, 0f, pole);
                offset = face == 0 ? reachX : face == 1 ? (pole > 0f ? family.PoleHigh : family.PoleLow) : reachZ;

                float across = face == 0 ? h.y : h.x;
                float other = face == 2 ? h.y : h.z;
                size = Mathf.Sqrt(across * across + other * other);
                return;
            }
            // FAMILYSKIN end

            switch (shape)
            {
                case SphereShape _:
                case CapsuleShape _:
                {
                    bool capsule = shape is CapsuleShape;
                    float r = capsule ? CapsuleShape.Radius(hf) : SphereShape.Radius(hf);
                    float span = capsule ? CapsuleShape.HalfSpan(hf) : 0f;

                    Vector3 v = anchorLocal;
                    float c = Mathf.Clamp(v.y, -span, span);
                    Vector3 outward = v - new Vector3(0f, c, 0f);

                    if (outward.magnitude < 0.25f * r)
                    {
                        v = towardLocal;
                        c = Mathf.Clamp(v.y, -span, span);
                        outward = v - new Vector3(0f, c, 0f);
                    }

                    normal = outward.sqrMagnitude > 1e-12f ? outward.normalized : Vector3.up;
                    offset = normal.y * c + r;
                    size = 0f;

                    bool side = capsule && Mathf.Abs(normal.y) < 1e-4f;
                    segLow = side ? -span : c;
                    segHigh = side ? span : c;
                    return;
                }

                default:
                {
                    Vector3 v = anchorLocal;
                    float sx = Mathf.Abs(v.x) / Mathf.Max(h.x, 1e-9f);
                    float sy = Mathf.Abs(v.y) / Mathf.Max(h.y, 1e-9f);
                    float sz = Mathf.Abs(v.z) / Mathf.Max(h.z, 1e-9f);

                    if (Mathf.Max(sx, Mathf.Max(sy, sz)) < 0.5f)
                    {
                        v = towardLocal;
                        sx = Mathf.Abs(v.x) / Mathf.Max(h.x, 1e-9f);
                        sy = Mathf.Abs(v.y) / Mathf.Max(h.y, 1e-9f);
                        sz = Mathf.Abs(v.z) / Mathf.Max(h.z, 1e-9f);
                    }

                    face = sx >= sy && sx >= sz ? 0 : sy >= sz ? 1 : 2;
                    float sign = (face == 0 ? v.x : face == 1 ? v.y : v.z) < 0f ? -1f : 1f;
                    normal = face == 0 ? new Vector3(sign, 0f, 0f) : face == 1 ? new Vector3(0f, sign, 0f) : new Vector3(0f, 0f, sign);
                    offset = face == 0 ? h.x : face == 1 ? h.y : h.z;

                    float hu = face == 0 ? h.y : h.x;
                    float hv = face == 2 ? h.y : h.z;
                    size = Mathf.Sqrt(hu * hu + hv * hv);
                    return;
                }
            }
        }

        /// <summary>
        /// The fillet and the ramp of one joint between merged parts (level 5), slots and rest frame. The fillet's radius is
        /// <see cref="Rig5FilletShare"/> of the thicker part's smallest half extent, at least <see cref="Rig5FilletFloor"/> k;
        /// the ramp's reach <see cref="Rig5RampShare"/> of it, measured from the smaller face (a round part's contact first).
        /// </summary>
        private static Fillet5 FilletOf(
            int parentSlot, int childSlot, Vector3 anchor, float k,
            PartShape parentShape, Vector3 parentHalf, Vector3 parentCentre, Quaternion parentRotation, Vector3 parentAnchorLocal,
            PartShape childShape, Vector3 childHalf, Vector3 childCentre, Quaternion childRotation, Vector3 childAnchorLocal,
            // FAMILYSKIN begin
            TheatreFamilySkin.Solid parentFamily = null, TheatreFamilySkin.Solid childFamily = null
            // FAMILYSKIN end
            )
        {
            float parentSmallest = Mathf.Min(parentHalf.x, Mathf.Min(parentHalf.y, parentHalf.z));
            float childSmallest = Mathf.Min(childHalf.x, Mathf.Min(childHalf.y, childHalf.z));
            float thicker = Mathf.Max(parentSmallest, childSmallest);

            Face5(parentShape, parentHalf, parentAnchorLocal, Quaternion.Inverse(parentRotation) * (childCentre - parentCentre),
                out int parentFace, out Vector3 parentNormal, out float parentOffset, out float parentSize,
                out float parentLow, out float parentHigh, parentFamily);
            Face5(childShape, childHalf, childAnchorLocal, Quaternion.Inverse(childRotation) * (parentCentre - childCentre),
                out int childFace, out Vector3 childNormal, out float childOffset, out float childSize,
                out float childLow, out float childHigh, childFamily);

            Vector3 parentWorld = parentRotation * parentNormal;
            Vector3 childWorld = childRotation * childNormal;

            return new Fillet5
            {
                Parent = parentSlot, Child = childSlot, Anchor = anchor,
                Radius = Mathf.Max(Rig5FilletShare * thicker, Rig5FilletFloor * k),
                Reach = Rig5RampShare * thicker,
                RampOnParent = parentSize <= childSize,
                ParentNormal = parentWorld, ChildNormal = childWorld,
                ParentOffset = Vector3.Dot(parentWorld, parentCentre) + parentOffset,
                ChildOffset = Vector3.Dot(childWorld, childCentre) + childOffset,
                ParentFace = parentFace, ChildFace = childFace,
                ParentSegLow = parentLow, ParentSegHigh = parentHigh, ChildSegLow = childLow, ChildSegHigh = childHigh,
            };
        }

        /// <summary>
        /// Level 5: per vertex, the share of the skin's carve depth kept, (1 - fill) times (1 - the joint ramp), and the most
        /// depth share that keeps the cut within <see cref="Rig5CarveCells"/> cells: the shader cuts at most _CarveFraction (at
        /// most 0.5) times the share times the vertex's half thickness, times a field value of at most one.
        /// </summary>
        private static Vector2[] Carve5(Vector3[] positions, float[] halfThick, float cell, Field field)
        {
            var result = new Vector2[positions.Length];
            float cap = Rig5CarveCells * cell;

            for (int v = 0; v < positions.Length; v++)
            {
                float keep = (1f - SkinFill) * (1f - field.Ramp5At(positions[v]));
                result[v] = new Vector2(keep, 2f * cap / Mathf.Max(halfThick[v], 1e-6f));
            }

            return result;
        }

        /// <summary>The level 5 line of the log: the fillets' radii, the ramps' reach, the fill and the carve's cap.</summary>
        private static string Rig5Log(long id, Field field)
        {
            field.FilletRanges(out float radiusLeast, out float radiusMost, out float reachLeast, out float reachMost);

            return string.Format(CultureInfo.InvariantCulture,
                "[Theatre] one skin rig5: body {0}, {1} fillets, fillet radius {2:0.0000} to {3:0.0000} m, " +
                "ramp reach {4:0.0000} to {5:0.0000} m, fill {6:0.00}, carve cap {7:0.00} cells",
                id, field.FilletCount, radiusLeast, radiusMost, reachLeast, reachMost, SkinFill, Rig5CarveCells);
        }
        // RIG5 end
        // RIG6 begin
        // ---------------------------------------------------------------- the bent hinge (level 6)

        /// <summary>
        /// One joint's hinge record (level 6), one per fillet: the larger part's slot, whether the joint is waisted and so carries a
        /// sleeve, the sleeve (its base on the larger part's face, its inward axis, depth, radius and blend radius, rest frame), and
        /// the two faces' sizes for the log (a box face's half diagonal, 0 for a round part).
        /// </summary>
        private struct Hinge6
        {
            public int Larger;
            public bool Waisted;
            public Vector3 Base, Axis;
            public float Depth, Radius, Blend;
            public float ParentSize, ChildSize;
            // BEYOND begin
            // The BEYOND test: the round end's collider radius r at a hinge whose contact is a round end on a box face, else 0.
            public float BeyondRadius;
            // BEYOND end
        }
        // RIG6 end
        // noseskin:begin
        /// <summary>
        /// The NOSE_PIT 2 line of the log, for a body that has a nose: whether the nose joined the skin, its pit's radius and
        /// depth in rest metres, and which parts stand apart (by type and smallest half extent at the adult's size).
        /// </summary>
        private static string NoseLog(long id, Phenotype adult, bool[] merged, bool noseIn, float radius, float depth)
        {
            var apart = new List<string>();

            for (int i = 0; i < adult.PartCount; i++)
            {
                if (merged[i]) continue;

                Vector3 half = Abs(adult.Parts[i].HalfExtents.ToVector3());
                float smallest = Mathf.Min(half.x, Mathf.Min(half.y, half.z));
                apart.Add(adult.Parts[i].CellTypeId + " " + smallest.ToString("0.0000", CultureInfo.InvariantCulture) + " m");
            }

            return string.Format(CultureInfo.InvariantCulture,
                "[Theatre] one skin nose: body {0}, {1}; standing apart (type, smallest half extent): {2}",
                id,
                noseIn
                    ? string.Format(CultureInfo.InvariantCulture, "nose merged, pit radius {0:0.0000} m, depth {1:0.0000} m (rest)", radius, depth)
                    : "nose not in the skin",
                apart.Count > 0 ? string.Join(", ", apart) : "none");
        }
        // noseskin:end

        /// <summary>A capsule whose every swing axis lies within this many degrees of its long axis turns about it: no stroke plane.</summary>
        private const float FinWebTwistDegrees = 20f;

        /// <summary>
        /// The hinge a link's fin reads its stroke plane and its lag from: the link's own joint to a merged parent, else its first
        /// merged child's. The anchor is in the link's frame in rest metres. The joint frame is PhenotypeBuilder.JointFrameRotation's
        /// (RecordedPose.FrameOf is the theatre's copy): a hinge turns about the frame's x turned by Euler(0, 90, 0), the child's z;
        /// a joint of more freedoms turns about the child's own x, y and z, as many as it has, x the twist. A child's axes are
        /// carried into the link's frame by the two parts' rest rotations.
        /// </summary>
        private static bool FinHinge(
            Phenotype adult, int index, bool[] merged, float unscale, out Vector3 anchor, out Vector3[] axes, out string said)
        {
            PhenotypePart part = adult.Parts[index];
            anchor = Vector3.zero;
            axes = null;
            int joint = -1;
            int other = -1;
            Quaternion toLink = Quaternion.identity;

            if (part.ParentIndex >= 0 && part.ParentIndex < merged.Length && merged[part.ParentIndex])
            {
                joint = index;
                other = part.ParentIndex;
                anchor = part.ChildAnchorLocal.ToVector3() * unscale;
            }
            else
            {
                for (int c = 0; c < adult.PartCount && joint < 0; c++)
                {
                    PhenotypePart child = adult.Parts[c];
                    if (child.ParentIndex != index || c >= merged.Length || !merged[c]) continue;

                    joint = c;
                    other = c;
                    anchor = child.ParentAnchorLocal.ToVector3() * unscale;
                    toLink = Quaternion.Inverse(Normalised(part.Rotation.ToQuaternion())) * Normalised(child.Rotation.ToQuaternion());
                }
            }

            if (joint < 0)
            {
                said = "no joint to a merged part";
                return false;
            }

            JointType type = adult.Parts[joint].JointType;
            int freedoms = type.DofCount();

            if (freedoms == 0)
            {
                said = string.Format(CultureInfo.InvariantCulture, "its joint to part {0} is fixed, so it has no stroke", other);
                return false;
            }

            Quaternion frame = toLink * (type == JointType.Hinge ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            axes = new Vector3[freedoms];
            for (int d = 0; d < freedoms; d++) axes[d] = (frame * (d == 0 ? Vector3.right : d == 1 ? Vector3.up : Vector3.forward)).normalized;

            said = string.Format(CultureInfo.InvariantCulture, "{0} joint to part {1}", type, other);
            return true;
        }

        // ---------------------------------------------------------------- small things

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static Quaternion Normalised(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 1e-9f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
        }

        private static float Median(float[] values)
        {
            var sorted = (float[])values.Clone();
            Array.Sort(sorted);
            int n = sorted.Length;
            if (n == 0) return 0f;
            return (n & 1) == 1 ? sorted[n / 2] : 0.5f * (sorted[n / 2 - 1] + sorted[n / 2]);
        }

        // BOXLOG begin
        /// <summary>
        /// The skinned renderer, for the box counter's frustum test (<c>EVOSIM_THEATRE_BOX_LOG</c>). Read only; nothing else asks.
        /// </summary>
        internal Renderer DrawnRenderer => _renderer;

        /// <summary>
        /// Why the first pass of <see cref="Build"/> did not merge a part, for the box counter's side record. It re-asks
        /// the rules that pass applied, in the same order, and changes nothing.
        /// </summary>
        private static string NotMergedReason(PhenotypePart part, bool noTransform, HashSet<string> body)
        {
            if (noTransform) return "no part transform";

            string type = part.CellTypeId;
            string level = " (level " + Level.ToString("0.#", CultureInfo.InvariantCulture) + ")";

            if (type == CellTypeIds.Anchoring) return "holdfast, cell type " + type + ": never in the skin" + level;

            if (!Rigged || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SKIN_BODY")))
            {
                return "cell type " + type + " is not in the skin's body set" + level;
            }

            if (type == CellTypeIds.Nose) return "cell type " + type + ": the rig leaves a nose standing apart" + level;
            if (type == CellTypeIds.Photosynthetic) return "cell type " + type + ": drawn as a leaf blade, which the skin has no field for" + level;

            return "cell type " + type + ": the rig's rule" + level;
        }

        /// <summary>Why <see cref="Build"/> refused a body for having too few merged parts.</summary>
        private static string NotEnoughReason(List<int> mergedParts)
        {
            if (mergedParts.Count == 0) return "no merged part: no part of the body is in the skin's set";

            return "one part: only part " + mergedParts[0].ToString(CultureInfo.InvariantCulture) +
                   " is merged, and a skin joins two or more unless EVOSIM_THEATRE_NO_BOX=1";
        }

        /// <summary>Why a part was left out as too thin for the grid.</summary>
        private static string ThinReason(float smallestHalf, float cell, bool atCap)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "thin: smallest half extent {0:0.0000} m is under {1:0.##} cells of {2:0.0000} m{3}",
                smallestHalf, ThinPartCells, cell, atCap ? " (the whole-animal grid cap)" : "");
        }
        // BOXLOG end
    }
}
