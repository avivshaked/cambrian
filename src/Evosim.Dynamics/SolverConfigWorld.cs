using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// What the solver reads about the <i>shape</i> of the world the farm runs in: the bed's
    /// height map, where the glass's axis stands, and whether the contact instrument is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every default here reproduces the spike exactly.</b> A null bed is the flat slab at
    /// <c>-WorldDepthMetres</c> and takes the same arithmetic path it always did; an axis of
    /// <c>(0, 0)</c> is the spike's origin-centred glass; the instrument is off. So a
    /// <see cref="SolverConfig"/> built by <see cref="SolverConfig.From"/> and stepped is the
    /// same trajectory to the bit as before this file existed.
    /// </para>
    /// <para>
    /// <b>The farm's tank is not centred on the origin, and the spike's was.</b>
    /// <c>TankWall.Build</c> and <c>SharedVolume.Axis</c> both put the disc's axis at
    /// <c>(R, R)</c>, and <c>BedShape.Height</c> subtracts <c>R</c> from both coordinates before
    /// it evaluates anything. So a world ported from a run has its glass at <c>(R, R)</c> and a
    /// world built by hand in a test has it wherever the test says. That is what
    /// <see cref="SolverConfig.FromWorld"/> is for, and calling <see cref="SolverConfig.From"/>
    /// and setting <see cref="SolverConfig.TankRadiusMetres"/> by hand — which is what the bench
    /// does — leaves the axis at the origin and is a different world.
    /// </para>
    /// </remarks>
    public sealed partial class SolverConfig
    {
        /// <summary>
        /// The sea bed's height map, or null for the flat slab at <c>-WorldDepthMetres</c>.
        /// </summary>
        /// <remarks>
        /// Null and <c>HasRelief == false</c> mean the same thing and are folded together by
        /// <see cref="FromWorld"/>, exactly as <c>SeaFloor.Build</c> and
        /// <see cref="Placement.PlacementFloor"/> fold them: a bed with no relief takes the flat
        /// path rather than a general path with a zero in it, so the flat world's arithmetic is
        /// not merely equal to what it was but identical.
        /// </remarks>
        public BedShape Bed;

        /// <summary>
        /// The reefs' rock, or null — every recorded world. <c>logbook/specs/reef-spec.md</c> §2:
        /// a static shape in the contacts (<see cref="ContactReef"/>), counted with the bed and the
        /// glass, and a guard in <see cref="Divergence"/>. Carried by <see cref="FromWorld"/> from
        /// <c>World.Reefs</c>, the one object every reader of the rock shares.
        /// </summary>
        public ReefGeometry Reefs;

        /// <summary>
        /// The glass's axis, m. <b>Core's frame, and not a dial:</b> the tank's water is
        /// <c>[0, 2R)</c> on both horizontal axes and its axis stands at <c>(R, R)</c>.
        /// </summary>
        /// <remarks>
        /// <b>A property rather than a field, because a field can be forgotten.</b> The spike
        /// measured its radius from the origin and scattered bodies around it, which is a
        /// different world from the farm's by 26 m: a body correctly inside round 42's water sits
        /// about 41 m from the origin, outside a 26 m glass, and the wall spring throws it on the
        /// first step. <c>TankGeometry</c>, <c>CurrentField</c>, <c>GridField</c>,
        /// <c>SharedVolume</c> and <see cref="BedShape.Height"/> are all in Core's frame, so this
        /// is too, and there is no way to set it to anything else.
        /// </remarks>
        public double TankAxisX => TankRadiusMetres;

        /// <summary>The glass's axis, m — see <see cref="TankAxisX"/>.</summary>
        public double TankAxisZ => TankRadiusMetres;

        /// <summary>
        /// The most separating speed one pair may be given in one step, m/s, over what it
        /// already had. 0 or less takes the cap off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The role <c>Physics.defaultMaxDepenetrationVelocity</c> plays in the farm</b>, and
        /// the reason the farm sets it at all: a solver handed a deep overlap answers with
        /// whatever force it takes to fix it in one step, and that force is a throw. Here the
        /// spring's own answer to a half-metre overlap between a 19 kg body and a 260 kg one is
        /// about seven kilonewtons; the cap turns that into a push that separates them at a
        /// metre a second and takes as many steps as it takes.
        /// </para>
        /// <para>
        /// One metre a second, which is over the fastest thing in these worlds — the water's RMS
        /// is 0.3 m/s and a body swims at centimetres — and far under anything that could be
        /// called a throw. It binds only on an overlap no placer would ever admit.
        /// </para>
        /// </remarks>
        public double MaxSeparationSpeed = 1.0;

        /// <summary>
        /// The most a body's whole contact set may change its speed by in one step, m/s. 0 or
        /// less takes the cap off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Per pair is not enough in a pile.</b> Each pair's spring is stable on its own, and
        /// twenty of them are not: the effective stiffness a body feels is the sum of its
        /// neighbours' and the effective damping likewise, so an explicit step that is
        /// comfortable at one neighbour is past its bound at twenty
        /// (<c>c_total * dt / m &lt; 2</c>). A crowd is the normal condition in these worlds —
        /// round 42 holds about nine hundred bodies in the top ten metres of a 26 m tank and puts
        /// every newborn within five metres of its parent — so the sum has to be bounded and not
        /// merely each term.
        /// </para>
        /// <para>
        /// <b>It is the one place the pair stops being exactly equal and opposite.</b> Each body
        /// caps its own total, and two bodies in different crowds cap differently, so momentum
        /// is conserved exactly everywhere the cap is slack and approximately where it binds —
        /// which is a pile a placer would not have made. The alternative, sharing a budget
        /// between neighbours, makes a pair's stiffness depend on bodies that have nothing to do
        /// with it.
        /// </para>
        /// </remarks>
        public double MaxContactSpeedChange = 1.0;

        /// <summary>
        /// Whether the contact instrument counts overlaps. Off costs one branch a body a step.
        /// </summary>
        public bool ContactInstrument;

        /// <summary>
        /// Whether the step's overlapping pairs are kept as a list a consumer can walk —
        /// <see cref="DynamicsWorld.Overlaps"/>. Requires <see cref="ContactInstrument"/>.
        /// </summary>
        public bool ContactEvents;

        /// <summary>
        /// <see cref="From"/> plus the world's shape: the glass, its axis and the bed.
        /// </summary>
        /// <param name="bed">
        /// The world's height map — <c>World.Bed</c> — or null for the flat floor every
        /// recorded world before D092 has.
        /// </param>
        /// <param name="world">
        /// The world the run is actually in, or null. <b>Package G reconciled the two factories
        /// through this parameter.</b> They had diverged in exactly one field and it was the one
        /// that decides whether the water moves: <see cref="From"/> carries
        /// <see cref="SolverConfig.Current"/> only when it is handed a world, and
        /// <see cref="FromWorld"/> called it without one — so a farm that built its solver config
        /// through <c>FromWorld</c>, as the API note said it should, got the tank, the axis and
        /// the bed right and ran the whole round in still water. With a world the two are now one
        /// method: <c>FromWorld(config, dt, world.Bed, world)</c> carries everything <c>From</c>
        /// carries (the current, the patch count, D100's hold) and everything the shape adds (the
        /// glass, its axis, the bed). Without one the behaviour is unchanged to the bit, which is
        /// what keeps the bench and the solver tests where they were.
        /// </param>
        /// <remarks>
        /// The radius comes through <see cref="TankGeometry.RadiusFor"/> rather than from a
        /// square root taken here, so the solver's glass stands exactly where the field's mask,
        /// the placer's disc and the gyre's normalised radius put theirs — and off
        /// <c>World.TankRadiusMetres</c> when there is a world, which is the same number by the
        /// same function and is the one the fields and the placer were built with.
        /// </remarks>
        public static SolverConfig FromWorld(
            RunConfig config, double stepSeconds, BedShape bed = null, World world = null)
        {
            SolverConfig solver = From(config, stepSeconds, world);

            double radius = config.WorldShape == WorldShape.Tank
                ? world != null
                    ? world.TankRadiusMetres
                    : TankGeometry.RadiusFor(config.WorldAreaSquareMetres)
                : 0;

            solver.TankRadiusMetres = radius;
            solver.Bed = bed != null && bed.HasRelief ? bed : null;

            // Round 47's reefs, from the world that placed them; null without a world, which is
            // the bench and the solver tests, and null in every world without reefs.
            solver.Reefs = world?.Reefs;


            return solver;
        }
    }
}
