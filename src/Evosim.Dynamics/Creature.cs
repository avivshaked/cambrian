using System;
using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// One creature as the solver holds it: flat arrays of doubles, sized once at birth and
    /// never reallocated, plus the <see cref="Brain"/> and the effector conditioning that drive
    /// it. This is the unit <see cref="DynamicsWorld"/> steps in parallel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Layout, not objects.</b> Every per-link and per-degree-of-freedom quantity is an entry
    /// in a flat array indexed by link or DOF, and every scratch buffer the step needs is
    /// allocated here. A Burst or GPU port is then a translation of the loops rather than a
    /// redesign: nothing in <see cref="Aba"/> walks a reference graph, and no step allocates.
    /// </para>
    /// <para>
    /// <b>Link frames.</b> Link <i>i</i>'s frame is the developed part's own frame — origin at
    /// the part's centre, axes as <see cref="PhenotypePart.Rotation"/> has them. All three
    /// shapes are centred and axis-aligned in that frame, so the centre of mass is the origin
    /// and the inertia tensor is diagonal, which is what lets the solver carry three numbers
    /// per link instead of six.
    /// </para>
    /// <para>
    /// <b>Joint frames, from <c>PhenotypeBuilder.ConfigureJoint</c>.</b> One physical joint
    /// frame is expressed in both bodies: <see cref="JointFrame"/> is the child's
    /// (<c>anchorRotation</c>) and <see cref="RestFrame"/> the parent's
    /// (<c>parentAnchorRotation = relative * frame</c>). A hinge's frame is
    /// <c>Euler(0, 90, 0)</c> and everything else's is identity, exactly as the builder has it.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        public const int MaxDofPerJoint = 3;

        /// <summary>Stable identity, and the order every cross-creature sum is taken in.</summary>
        public readonly int Id;

        public readonly int Links;
        public readonly int Dof;

        /// <summary>
        /// The body as it now is — the scaled phenotype, not the adult. Replaced by
        /// <see cref="Resize"/> at a growth step; see <c>Creature.Growth.cs</c>.
        /// </summary>
        public Phenotype Phenotype { get; private set; }

        // ---------------------------------------------------------------- topology

        /// <summary>Parent link index, or -1 for the root. Always less than the child's index.</summary>
        public readonly int[] Parent;

        public readonly int[] DofCount;

        /// <summary>First DOF of this link's joint, or -1 where the joint is fixed.</summary>
        public readonly int[] DofStart;

        // ---------------------------------------------------------------- inertial properties

        /// <summary>Link mass, kg, with added mass folded in — <c>FluidModel.EffectiveMass</c>.</summary>
        public readonly double[] Mass;

        /// <summary>The mass before added mass, kg. Kept because buoyancy is priced on volume.</summary>
        public readonly double[] PlainMass;

        /// <summary>Diagonal inertia in the link's own axes, kg·m2. 3 per link.</summary>
        public readonly double[] InertiaLocal;

        /// <summary>The smallest principal inertia, for the two limiters. 1 per link.</summary>
        public readonly double[] SmallestInertia;

        public readonly double[] Volume;
        public readonly double[] Lift;

        /// <summary>
        /// D111's offset per link, the fraction of the thin half-extent the buoyancy centre sits
        /// at — <see cref="PhenotypePart.BuoyancyOffset"/>.
        /// </summary>
        public readonly double[] BuoyancyOffset;

        /// <summary>
        /// Each link's thinnest axis in its own frame, 0 to 2, or -1 for a sphere —
        /// <see cref="PhenotypePart.ThinAxis"/>.
        /// </summary>
        public readonly int[] ThinAxis;

        /// <summary>
        /// The buoyancy centre's distance from the origin along <see cref="ThinAxis"/>, metres:
        /// the offset times the thin half-extent the link has now, signed, and 0 on a sphere.
        /// </summary>
        /// <remarks>
        /// Metres and not the fraction, because the fraction is what the genome says and the arm
        /// is what the torque needs, and a growth resize changes the second without the first; it
        /// is rewritten with the rest of the sized properties.
        /// </remarks>
        public readonly double[] BuoyancyArm;

        /// <summary>Total body volume, m3 — what D064's buoyancy factor is a function of.</summary>
        public double TotalVolume { get; private set; }

        // ---------------------------------------------------------------- joints

        /// <summary>Joint anchor in the child's own frame, metres. 3 per link.</summary>
        public readonly double[] ChildAnchor;

        /// <summary>Joint anchor in the parent's frame, metres. 3 per link.</summary>
        public readonly double[] ParentAnchor;

        /// <summary>The joint frame in the child's axes — <c>anchorRotation</c>. 4 per link.</summary>
        public readonly double[] JointFrame;

        /// <summary>The joint frame in the parent's axes — <c>parentAnchorRotation</c>. 4 per link.</summary>
        public readonly double[] RestFrame;

        public readonly double[] LimitLo;
        public readonly double[] LimitHi;
        public readonly double[] LimitStiffness;
        public readonly double[] LimitDamping;

        /// <summary>
        /// The limit spring's implicit term for this step, <c>(c + k·dt)·dt</c>, per degree of
        /// freedom — zero for every degree of freedom inside its stops.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the spring is not simply stiffer.</b> A penalty limit integrated explicitly is
        /// stable only while <c>omega·dt &lt; 2</c>, and the overshoot it allows against a
        /// constant drive is <c>m / k</c> with <c>k = I·omega²</c>. Those two together cap how
        /// closely a penalty can imitate a hard stop: at <c>omega = 0.5/dt</c> the first build
        /// overshot round 42's genome 1 by 0.0907 rad, and buying that back by raising
        /// <c>omega</c> alone runs into the stability bound long before the overshoot is small.
        /// </para>
        /// <para>
        /// <b>What this is instead.</b> The spring's own resistance to the acceleration it is
        /// about to see, <c>-(c + k·dt)·dt·q̈</c>, added to the joint's articulated inertia
        /// <c>D</c> in the inward pass — the standard first-order implicit treatment of a
        /// stiff joint in an articulated-body algorithm. It is unconditionally stable in the
        /// spring constant, so the stiffness can be chosen for the overshoot it allows rather
        /// than for what the step will survive.
        /// </para>
        /// <para>
        /// <b>Zero inside the stops, deliberately.</b> A degree of freedom not touching its limit
        /// is solved by the plain algorithm, to the bit — which is what keeps the two
        /// constraint-solve oracle tests meaningful: they check an unmodified ABA against an
        /// independent maximal-coordinate solve, and a term that was always on would be checked
        /// by neither.
        /// </para>
        /// </remarks>
        public readonly double[] LimitImplicit;

        /// <summary>Evolved peak torque per link, N·m — <see cref="PhenotypePart.Power"/>.</summary>
        public readonly double[] Power;

        /// <summary>Each link's drive genes (threshold and envelope), from its part.</summary>
        public readonly DriveGenes[] JointGenes;

        /// <summary>
        /// D234, the mirrored twin's drive: for each link, a bit mask of the degrees of freedom whose signed
        /// quantities (angle, rate, target, torque) are read negated. Zero for every link of a body with no
        /// reflected part, and so for every recorded world. See <c>Creature.Twin.cs</c>.
        /// </summary>
        public readonly int[] MirrorMask;

        /// <summary>
        /// D234, the mirrored twin's senses: for each link, the axis of its own frame that the part's reflection
        /// is about (0 to 2), or -1 where the part reflects nothing. A vector the senses read in the part's own
        /// frame has this component negated. See <c>Creature.Twin.cs</c>.
        /// </summary>
        public readonly int[] FrameNormal;

        // ---------------------------------------------------------------- drag panels

        /// <summary>Where link <i>i</i>'s panels start. Length <c>Links + 1</c>.</summary>
        public readonly int[] PanelStart;

        // Not readonly: a resize rewrites their contents in place, and reallocates them in the
        // one case a resize can change the count — see Creature.Growth.cs's WritePanels.
        public double[] PanelCentre;
        public double[] PanelNormal;
        public double[] PanelArea;

        /// <summary>
        /// D161's spin form: for each link and each of its own three axes <i>k</i>, the sum over its
        /// panels of the area times the cube of the panel centre's distance from axis <i>k</i>
        /// through the link's origin, m^5. 3 per link, in the link's frame.
        /// </summary>
        /// <remarks>
        /// Written beside the panels by <c>WritePanels</c>, so a resize keeps it current. Read
        /// only under <see cref="TurningDragForm.Spin"/>.
        /// </remarks>
        public readonly double[] SpinMoment;

        // ---------------------------------------------------------------- state

        /// <summary>Root link origin in world space, metres.</summary>
        public Vec3 BasePosition;

        /// <summary>Root link orientation in world space.</summary>
        public QuatD BaseRotation = QuatD.Identity;

        public readonly double[] Q;
        public readonly double[] Qd;

        /// <summary>
        /// A three-degree joint's relative rotation, as a quaternion in the joint frame. 4 per
        /// link, identity on every joint that is not a ball.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why a ball joint is not three angles.</b> The one-, two- and three-degree joints
        /// were all written first as compositions <c>Rx(q0) Ry(q1) Rz(q2)</c>, truncated to the
        /// degree count. For one and two degrees that has no singularity. For three it is a set
        /// of Euler angles, and at <c>q1 = +-pi/2</c> the first and third axes coincide: the
        /// motion subspace loses rank and the <c>k x k</c> the solve inverts becomes singular.
        /// Round 42 draws joint limits at up to 1.4 rad, which is 0.17 rad short of that, and a
        /// penalty limit can be overshot — five of its 747 genomes wound a spherical joint into
        /// the singularity and were thrown, every one of them a three-part body with two
        /// spherical joints (the trace is in <c>scratch/solver-spike/logs/</c>).
        /// </para>
        /// <para>
        /// So a ball joint's velocity coordinates are the relative angular velocity in the
        /// child's own joint frame, its motion subspace is the identity there — constant, full
        /// rank, everywhere — and its configuration is this quaternion. <see cref="Q"/> still
        /// carries three numbers for it, read back as the rotation vector, and they are what the
        /// limit springs and <see cref="SensorChannel.JointAngle"/> see.
        /// </para>
        /// </remarks>
        public readonly double[] BallRotation;

        /// <summary>World position of each link's origin. Refreshed by the kinematics pass.</summary>
        public readonly double[] Position;

        /// <summary>World orientation of each link, as a quaternion.</summary>
        public readonly double[] Rotation;

        /// <summary>World orientation of each link, as a matrix. The fluid and the sensors read it.</summary>
        public readonly double[] RotationMatrix;

        /// <summary>World angular velocity per link, rad/s.</summary>
        public readonly double[] Spin;

        /// <summary>World velocity of each link's origin, m/s.</summary>
        public readonly double[] Velocity;

        // ---------------------------------------------------------------- per-step scratch

        public readonly double[] IaA, IaB, IaC;   // articulated inertia blocks, 9 per link
        public readonly double[] Pa;              // articulated bias force, 6 per link
        public readonly double[] Sang, Slin;      // motion subspace, 3 columns of 3, world axes
        public readonly double[] Cbias;           // velocity-product acceleration, 6 per link
        public readonly double[] Un, Uf;          // I^A * S, 3 columns of 3, torque and force
        public readonly double[] Dinv;            // 3x3 per link
        public readonly double[] Ubar;            // 3 per link
        public readonly double[] Acc;             // [angular; linear] acceleration, 6 per link
        public readonly double[] Fext;            // external [torque; force] at the link origin

        /// <summary>
        /// Generalised joint torque, one per degree of freedom, on the way in — and the joint
        /// acceleration the solve produced, on the way out. See <see cref="Aba.Integrate"/>.
        /// </summary>
        public readonly double[] Tau;

        public readonly double[] Water;           // water velocity at each link, world
        public readonly double[] RelativeVelocity;// body minus water, world — the lateral line

        // ---------------------------------------------------------------- control

        public readonly Brain Brain;
        public readonly EffectorDrive Drive;
        // Public so the parity probe can read what the brain was handed and what it emitted.
        public readonly float[] DriveSignal;

        /// <summary>The brain's second output per degree of freedom, the bend (propose-joint-drive.md).</summary>
        public readonly float[] BendSignal;
        public readonly CreatureSenses Senses;

        // ---------------------------------------------------------------- contact state

        // Contact reads a frozen snapshot and writes a pending one, and the world commits the
        // second into the first between steps. Without that, a body stepped later in the same
        // parallel phase would read a neighbour that had already moved — which is the one rule
        // the spec puts above everything else, and it showed up immediately as a digest that
        // differed at four threads from at one.

        /// <summary>Bounding sphere centre as of the last commit — what other bodies read.</summary>
        public Vec3 ContactCentre;

        /// <summary>Bounding sphere radius, metres, as of the last commit.</summary>
        public double ContactRadius;

        /// <summary>Whole-body velocity as of the last commit, for contact damping.</summary>
        public Vec3 ContactVelocity;

        /// <summary>Whether this body was still being stepped as of the last commit.</summary>
        public bool ContactActive = true;

        private Vec3 _pendingCentre;
        private double _pendingRadius;
        private Vec3 _pendingVelocity;
        private bool _pendingActive = true;

        // Whether the pending sphere is a step's own and still waits to be committed. The step
        // sets it on its way out (DynamicsWorld.StepOne) and the next grid build commits it
        // (ContactGrid.Build, BuildLinks), so the commit costs no pass of its own; anything that
        // reads the committed sphere between the two calls EnsureContactCommitted first. A write
        // to the pending sphere between steps commits the step's own first, which keeps the
        // committed sphere what the commit at the foot of the step used to leave.
        private bool _commitDue;

        // Per-part contact, D114 (logbook/specs/per-part-contact-spec.md): a sphere on every link,
        // committed and pending exactly as the body's is and for the same reason. Null unless the
        // world asked for it, so a recorded world's body carries nothing new and its path adds no
        // float. The body's own sphere stays either way: the placer, Volume.Note and the
        // divergence check read it.

        /// <summary>Each link's sphere centre as of the last commit, 3 per link; null unless per-part.</summary>
        public double[] LinkContactCentre;

        /// <summary>Each link's sphere radius, metres, as of the last commit; 0 on a lost body.</summary>
        public double[] LinkContactRadius;

        /// <summary>Each link's velocity as of the last commit, 3 per link, for the damper.</summary>
        public double[] LinkContactVelocity;

        private double[] _pendingLinkCentre;
        private double[] _pendingLinkRadius;
        private double[] _pendingLinkVelocity;

        // D228, the true shape (RunConfig.ContactShape): each link's turn travels with its sphere, committed
        // and pending exactly as the centre is, so that contact reads only the frozen snapshot and never a
        // body's live pose. The core and margin are the link's shape and size, which change only at a growth
        // resize between steps. All null unless the world asked for the true shape: with it off nothing is
        // allocated and nothing is copied.

        /// <summary>
        /// Each link's world orientation as of the last commit, a row-major 3x3 matrix, 9 per link; null
        /// unless <see cref="SolverConfig.ContactShape"/> is the true shape.
        /// </summary>
        public double[] LinkContactRotation;

        /// <summary>
        /// Each link's convex core as half-extents in its own frame, 3 per link (a sphere's is the origin, a
        /// capsule's runs along local Y, a box's is the box less its smallest half-extent); null unless the
        /// true shape. <see cref="ConvexCore.Of"/>.
        /// </summary>
        public double[] LinkCoreHalf;

        /// <summary>Each link's margin, metres: the radius of the ball swept round its core. Null unless the true shape.</summary>
        public double[] LinkMargin;

        /// <summary>
        /// Each link's hull core, D233: for a link with a family part, the 98 points of its contact core at
        /// the part's present size, three doubles a point in the link's own frame (<see cref="ConvexCore.OfFamily"/>),
        /// with <see cref="LinkMargin"/> its margin and <see cref="LinkCoreHalf"/> zero; null for a link of a
        /// named shape, which has a box, segment or point core. The whole array is null unless the true shape.
        /// Rewritten in place at a growth resize.
        /// </summary>
        public double[][] LinkCoreHull;

        private double[] _pendingLinkRotation;

        /// <summary>
        /// Each link's angular velocity in the world as of the last commit (<see cref="Spin"/>, the one the solver
        /// integrates), 3 per link; null unless <see cref="SolverConfig.ContactShape"/> is
        /// <see cref="ContactShapeForm.TrueShapeAtPoint"/>. The velocity of a point of the link is
        /// <c>v + spin x r</c>.
        /// </summary>
        public double[] LinkContactSpin;

        private double[] _pendingLinkSpin;

        // The touch of the rock, per link: written by the link's own contact pass and read by the
        // next step's senses (and, from step 4, by the grip). Null unless SolverConfig.RockTouch.

        /// <summary>
        /// Whether each link's sphere was against the bed or a reef in the last contact pass; null
        /// unless <see cref="SolverConfig.RockTouch"/>.
        /// </summary>
        public bool[] RockTouched;

        /// <summary>
        /// Where each touching link met the rock, 3 per link: the point of its sphere deepest along
        /// the rock's normal. Meaningful only where <see cref="RockTouched"/> is true.
        /// </summary>
        public double[] RockPoint;

        /// <summary>The rock's outward normal at that point, 3 per link, unit length.</summary>
        public double[] RockNormal;

        /// <summary>Whether this body carries a contact sphere per link — the world's D114 switch.</summary>
        public bool LinkContactActive => LinkContactRadius != null;

        /// <summary>Total mass of the body, kg — the contact spring is scaled by it.</summary>
        public double TotalMass { get; private set; }

        /// <summary>Times this creature's drive torque was capped. Summed in id order, never shared.</summary>
        public long DriveImpulsesLimited;

        /// <summary>Times this creature's drag impulse was capped.</summary>
        public long DragImpulsesLimited;


        /// <summary>False once any link stops being finite. The body is then left alone.</summary>
        public bool Alive = true;

        public Creature(
            int id, Phenotype phenotype, SolverConfig config, PartShapeRegistry shapes = null)
        {
            if (phenotype == null) throw new ArgumentNullException(nameof(phenotype));
            if (phenotype.PartCount == 0)
            {
                throw new ArgumentException("A body with no parts cannot be stepped.", nameof(phenotype));
            }

            shapes = shapes ?? PartShapeRegistry.Catalogue;

            // Kept so that a growth resize cannot be handed a different world or a different
            // shape registry from the one the body was built with — the class of fault that
            // makes a parameter stop reaching the thing it configures (logbook/0007, 0008).
            _config = config;
            _shapes = shapes;

            // D231's two refusals come before anything is built: a body the switch cannot push is never made.
            RefuseStrokeAssembly(config);
            RefuseFamilyStroke(config, phenotype);

            Id = id;
            Phenotype = phenotype;
            Links = phenotype.PartCount;

            Parent = new int[Links];
            DofCount = new int[Links];
            DofStart = new int[Links];

            Mass = new double[Links];
            PlainMass = new double[Links];
            InertiaLocal = new double[3 * Links];
            SmallestInertia = new double[Links];
            SpinMoment = new double[3 * Links];
            Volume = new double[Links];
            Lift = new double[Links];
            BuoyancyOffset = new double[Links];
            ThinAxis = new int[Links];
            BuoyancyArm = new double[Links];
            Power = new double[Links];
            JointGenes = new DriveGenes[Links];
            MirrorMask = new int[Links];
            FrameNormal = new int[Links];

            ChildAnchor = new double[3 * Links];
            ParentAnchor = new double[3 * Links];
            JointFrame = new double[4 * Links];
            RestFrame = new double[4 * Links];

            int dof = 0;
            for (int i = 0; i < Links; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                Parent[i] = part.ParentIndex;
                int n = part.IsRoot ? 0 : part.JointType.DofCount();
                DofCount[i] = n;
                DofStart[i] = n > 0 ? dof : -1;
                dof += n;
            }

            Dof = dof;
            Q = new double[Dof];
            Qd = new double[Dof];
            BallRotation = new double[4 * Links];
            for (int i = 0; i < Links; i++) QuatD.Write(BallRotation, 4 * i, QuatD.Identity);
            Tau = new double[Dof];
            LimitLo = new double[Dof];
            LimitHi = new double[Dof];
            LimitStiffness = new double[Dof];
            LimitDamping = new double[Dof];
            LimitImplicit = new double[Dof];

            // ---- inertial properties, the joint frames, the limit springs and the panels
            //
            // Written by the three methods a growth resize uses, and deliberately not by a copy
            // of them here: a grown body and a body built at that size must be the same body to
            // the bit, and two transcriptions of one derivation is how that stops being true
            // without anything reporting it. Creature.Growth.cs holds them.

            var panelSets = new DragPanelSet[Links];
            int panels = WriteSizedProperties(phenotype, config, shapes, panelSets, PanelScratch);
            WriteJointLimits(config);

            PanelStart = new int[Links + 1];
            WritePanels(panelSets, panels);

            // D231's assembly constants, only when the switch is on: with it off nothing is allocated.
            if (config.StrokePushesAssembly)
            {
                AllocateStrokeAssembly();
                WriteStrokeAssembly();
            }

            // D255's family limb constants, only when the fin law is on and a family part has a joint.
            if (config.ShapedAddedMass && HasFamilyJoint(phenotype))
            {
                AllocateFamilyStroke();
                WriteFamilyStroke();
            }

            // ---- state and scratch

            Position = new double[3 * Links];
            Rotation = new double[4 * Links];
            RotationMatrix = new double[9 * Links];
            Spin = new double[3 * Links];
            Velocity = new double[3 * Links];

            IaA = new double[9 * Links];
            IaB = new double[9 * Links];
            IaC = new double[9 * Links];
            Pa = new double[6 * Links];
            Sang = new double[9 * Links];
            Slin = new double[9 * Links];
            Cbias = new double[6 * Links];
            Un = new double[9 * Links];
            Uf = new double[9 * Links];
            Dinv = new double[9 * Links];
            Ubar = new double[3 * Links];
            Acc = new double[6 * Links];
            Fext = new double[6 * Links];
            Water = new double[3 * Links];
            RelativeVelocity = new double[3 * Links];

            InitLedger();   // package B — Creature.Ledger.cs
            InitWater();    // package C — Creature.Water.cs

            Brain = Brain.For(phenotype);
            DriveSignal = new float[System.Math.Max(1, Brain.TotalDof)];
            BendSignal = new float[System.Math.Max(1, Brain.TotalDof)];
            Drive = new EffectorDrive(this, config);
            Senses = new CreatureSenses(this, config);

            BasePosition = ToVec(phenotype.Parts[0].Position);
            BaseRotation = QuatD.From(phenotype.Parts[0].Rotation);

            if (config.ContactPerPart)
            {
                LinkContactCentre = new double[3 * Links];
                LinkContactRadius = new double[Links];
                LinkContactVelocity = new double[3 * Links];
                _pendingLinkCentre = new double[3 * Links];
                _pendingLinkRadius = new double[Links];
                _pendingLinkVelocity = new double[3 * Links];

                if (config.ContactShape.IsTrueShape())
                {
                    LinkContactRotation = new double[9 * Links];
                    _pendingLinkRotation = new double[9 * Links];
                    LinkCoreHalf = new double[3 * Links];
                    LinkMargin = new double[Links];
                    LinkCoreHull = new double[Links][];
                    WriteLinkCores();

                    // The force at the contact point reads the other side's velocity there, so the spin travels
                    // with the sphere. Only at that level: the true shape at the centre allocates none.
                    if (config.ContactShape.AtPoint())
                    {
                        LinkContactSpin = new double[3 * Links];
                        _pendingLinkSpin = new double[3 * Links];
                    }
                }
            }
            else if (config.ContactShape.IsTrueShape())
            {
                throw new System.ArgumentException(
                    "SolverConfig.ContactShape is TrueShape and ContactPerPart is off: the true shape is the " +
                    "narrowphase of per-part contact (D228).");
            }

            if (config.RockTouch)
            {
                if (!config.ContactPerPart)
                {
                    throw new System.ArgumentException(
                        "SolverConfig.RockTouch needs ContactPerPart: the touch is read on each link's own sphere.");
                }

                RockTouched = new bool[Links];
                RockPoint = new double[3 * Links];
                RockNormal = new double[3 * Links];
            }

            InitGrip(phenotype, config);   // D162 — Creature.Grip.cs

            Kinematics.Refresh(this);
            RefreshContactSphere();
            CommitContactSphere();
        }

        /// <summary>
        /// Puts the body down at <paramref name="origin"/> in the attitude development gave it,
        /// at rest — which is what <c>PhenotypeBuilder.Build</c> does with its <c>start</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Not <c>PlaceAt(origin, QuatD.Identity)</c>, and the difference is not cosmetic.</b>
        /// The farm parents every part under one creature object placed at <c>start</c> with no
        /// rotation, and gives the root part <c>localPosition = Parts[0].Position</c> and
        /// <c>localRotation = Parts[0].Rotation</c>. So the root link lands at
        /// <c>start + Parts[0].Position</c> wearing <c>Parts[0].Rotation</c>, and every other
        /// part lands at <c>start + Parts[i].Position</c>. Standing the root upright at
        /// <c>start</c> instead moves and turns the whole body, which changes what
        /// <see cref="SensorChannel.OrientationUp"/> and <see cref="SensorChannel.Depth"/> read
        /// on the very first step — and those feed the brain, so the two engines' creatures
        /// start emitting different drive signals before a single step has been taken.
        /// </para>
        /// </remarks>
        public void PlaceAsDeveloped(Vec3 origin, Phenotype phenotype) =>
            PlaceAt(
                origin + ToVec(phenotype.Parts[0].Position),
                QuatD.From(phenotype.Parts[0].Rotation));

        /// <summary>Puts the body's root link at a place and an attitude, at rest.</summary>
        public void PlaceAt(Vec3 position, QuatD rotation)
        {
            BasePosition = position;
            BaseRotation = rotation;

            Array.Clear(Q, 0, Q.Length);
            Array.Clear(Qd, 0, Qd.Length);
            for (int i = 0; i < Links; i++) QuatD.Write(BallRotation, 4 * i, QuatD.Identity);
            Array.Clear(Spin, 0, Spin.Length);
            Array.Clear(Velocity, 0, Velocity.Length);

            Kinematics.Refresh(this);
            RefreshContactSphere();
            CommitContactSphere();
        }

        /// <summary>
        /// The bounding sphere contact reads, taken from the poses as they now stand.
        /// </summary>
        /// <remarks>
        /// Centred on the root link rather than on the centre of mass: the sphere is refreshed
        /// at the end of every step and read at the start of the next, so what matters is that
        /// it is cheap and that it encloses the body. A body's own centre of mass moves as its
        /// joints move and would make the radius wobble for no gain.
        /// </remarks>
        public void RefreshContactSphere()
        {
            EnsureContactCommitted();

            Vec3 centre = Vec3.Read(Position, 0);
            double radius = 0;
            Vec3 momentum = Vec3.Zero;

            for (int i = 0; i < Links; i++)
            {
                Vec3 at = Vec3.Read(Position, 3 * i);
                double reach = (at - centre).Magnitude + LinkReach[i];
                if (reach > radius) radius = reach;

                momentum += Vec3.Read(Velocity, 3 * i) * Mass[i];
            }

            _pendingCentre = centre;
            _pendingRadius = radius;
            _pendingVelocity = TotalMass > 0 ? momentum * (1.0 / TotalMass) : Vec3.Zero;
            _pendingActive = Alive;

            if (_pendingLinkRadius != null) RefreshLinkSpheres();
        }

        /// <summary>
        /// Each link's sphere, from the poses as they now stand: centred on the link's own origin,
        /// which is its centre of mass, with <see cref="LinkReach"/> for a radius and the link's
        /// own velocity for the damper. D114.
        /// </summary>
        /// <remarks>
        /// <b>A one-link body's link sphere is its body sphere, copied rather than recomputed.</b>
        /// The two are the same sphere, and the body's velocity is its momentum over its mass,
        /// <c>(v m)(1/m)</c>, which can sit an ulp from <c>v</c>. Copying is what makes a crowd of
        /// one-part bodies the recorded crowd to the bit under either model, which is the spec's
        /// first acceptance (section 1).
        /// </remarks>
        private void RefreshLinkSpheres()
        {
            // D228: the turn of every link, one-link body included (its link is link 0).
            if (_pendingLinkRotation != null) Array.Copy(RotationMatrix, _pendingLinkRotation, 9 * Links);
            if (_pendingLinkSpin != null) Array.Copy(Spin, _pendingLinkSpin, 3 * Links);

            if (Links == 1)
            {
                Vec3.Write(_pendingLinkCentre, 0, _pendingCentre);
                _pendingLinkRadius[0] = _pendingRadius;
                Vec3.Write(_pendingLinkVelocity, 0, _pendingVelocity);
                return;
            }

            double[] reach = LinkReach;

            for (int i = 0; i < Links; i++)
            {
                _pendingLinkCentre[3 * i] = Position[3 * i];
                _pendingLinkCentre[3 * i + 1] = Position[3 * i + 1];
                _pendingLinkCentre[3 * i + 2] = Position[3 * i + 2];
                _pendingLinkRadius[i] = reach[i];
                _pendingLinkVelocity[3 * i] = Velocity[3 * i];
                _pendingLinkVelocity[3 * i + 1] = Velocity[3 * i + 1];
                _pendingLinkVelocity[3 * i + 2] = Velocity[3 * i + 2];
            }
        }

        /// <summary>Takes a body the solver has lost out of everyone else's contact set.</summary>
        public void MarkLost()
        {
            EnsureContactCommitted();

            _pendingActive = false;
            _pendingRadius = 0;

            if (_pendingLinkRadius != null) Array.Clear(_pendingLinkRadius, 0, Links);
        }

        /// <summary>Makes this step's sphere the one other bodies will read. Between steps.</summary>
        public void CommitContactSphere()
        {
            ContactCentre = _pendingCentre;
            ContactRadius = _pendingRadius;
            ContactVelocity = _pendingVelocity;
            ContactActive = _pendingActive;

            if (_pendingLinkRadius != null)
            {
                Array.Copy(_pendingLinkCentre, LinkContactCentre, 3 * Links);
                Array.Copy(_pendingLinkRadius, LinkContactRadius, Links);
                Array.Copy(_pendingLinkVelocity, LinkContactVelocity, 3 * Links);
                if (_pendingLinkRotation != null) Array.Copy(_pendingLinkRotation, LinkContactRotation, 9 * Links);
                if (_pendingLinkSpin != null) Array.Copy(_pendingLinkSpin, LinkContactSpin, 3 * Links);
            }

            _commitDue = false;
        }

        /// <summary>
        /// Commits the last step's sphere if it has not been committed yet: what a reader of
        /// <see cref="ContactCentre"/>, <see cref="ContactRadius"/>, <see cref="ContactVelocity"/>,
        /// <see cref="ContactActive"/> or the link spheres calls between steps.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The commit is folded into the next step's grid build</b> (2026-10-03,
        /// <c>logbook/specs/farm-physics-speed-read.md</c> P1). Until then it was a pass of its
        /// own at the foot of every step, one fork-join over every body for a few hundred bytes
        /// each, 8 to 12% of round 49's physics. So between the end of a step and the next grid
        /// build the committed sphere is the one the step <i>started</i> from, and a reader there
        /// (the checkpoint writer, the divergence guard's default radius, a step backend, a test)
        /// commits first. The numbers it then reads are the ones the old commit left.
        /// </para>
        /// <para>
        /// <b>Never inside the parallel phase.</b> Other bodies read this body's committed sphere
        /// while it is being stepped; a commit there is the thread-count dependence the pending
        /// twin exists to prevent. Nothing in a step calls this with the flag set: the grid build
        /// before it has cleared every flag, and the step sets it only on the way out.
        /// </para>
        /// </remarks>
        public void EnsureContactCommitted()
        {
            if (_commitDue) CommitContactSphere();
        }

        /// <summary>
        /// Marks the sphere this step left as waiting for the next grid build's commit. The
        /// step's own, on its way out.
        /// </summary>
        internal void MarkContactCommitDue() => _commitDue = true;

        /// <summary>
        /// Rebuilds the per-link spheres after a checkpoint restore, pending and committed.
        /// </summary>
        /// <remarks>
        /// <b>Derived rather than written, so the checkpoint's layout does not move.</b> Between
        /// steps the committed link spheres are exactly what <see cref="RefreshLinkSpheres"/> makes
        /// of the poses, the velocities and the body's own pending sphere, all of which the
        /// checkpoint holds; a lost body's are zero-radius. Writing them down would bump
        /// <c>StateVersion</c> and orphan every checkpoint on disk for no information.
        /// </remarks>
        internal void RestoreLinkSpheres()
        {
            if (_pendingLinkRadius == null) return;

            RefreshLinkSpheres();
            if (!_pendingActive) Array.Clear(_pendingLinkRadius, 0, Links);

            Array.Copy(_pendingLinkCentre, LinkContactCentre, 3 * Links);
            Array.Copy(_pendingLinkRadius, LinkContactRadius, Links);
            Array.Copy(_pendingLinkVelocity, LinkContactVelocity, 3 * Links);
            if (_pendingLinkRotation != null) Array.Copy(_pendingLinkRotation, LinkContactRotation, 9 * Links);
            if (_pendingLinkSpin != null) Array.Copy(_pendingLinkSpin, LinkContactSpin, 3 * Links);
        }

        /// <summary>
        /// Each link's convex core and margin, from its shape and half-extents as they now are (D228).
        /// Written at a build and rewritten at a growth resize, beside <see cref="LinkReach"/>.
        /// </summary>
        private void WriteLinkCores()
        {
            for (int i = 0; i < Links; i++)
            {
                PhenotypePart part = Phenotype.Parts[i];

                // D233: a family part's core is the hull of its contact core's points, scaled to its size. The
                // array is made once and written in place at every resize.
                if (part.Family != null)
                {
                    double[] hull = LinkCoreHull[i] ?? (LinkCoreHull[i] = new double[3 * Superellipsoid.ContactCorePoints]);
                    ConvexCore.OfFamily(part, hull, out Vec3 hullHalf, out double hullMargin);
                    Vec3.Write(LinkCoreHalf, 3 * i, hullHalf);
                    LinkMargin[i] = hullMargin;
                    continue;
                }

                ConvexCore.Of(_shapes.Resolve(part.ShapeId), part.HalfExtents, out Vec3 half, out double margin);
                Vec3.Write(LinkCoreHalf, 3 * i, half);
                LinkMargin[i] = margin;
            }
        }

        /// <summary>Half the diagonal of each link's own extent, metres — the sphere's padding.</summary>
        public double[] LinkReach => _linkReach ?? (_linkReach = BuildReach());

        private double[] _linkReach;

        private double[] BuildReach()
        {
            var reach = new double[Links];
            for (int i = 0; i < Links; i++)
            {
                if (Phenotype.Parts[i].Family != null)
                {
                    reach[i] = FamilyReach(Phenotype.Parts[i]);
                    continue;
                }

                Float3 h = Phenotype.Parts[i].HalfExtents;
                reach[i] = System.Math.Sqrt(
                    (double)h.X * h.X + (double)h.Y * h.Y + (double)h.Z * h.Z);
            }
            return reach;
        }

        /// <summary>
        /// Whether link <paramref name="i"/> has a hull core: the world is on the true shape and the link's
        /// part is a family part. Only then is its core the hull of the contact core's points. Its broadphase
        /// sphere is the family's reach in either contact shape (<see cref="FamilyReach"/>), never the half-diagonal.
        /// </summary>
        private bool HasHullCore(int i) => LinkCoreHull != null && Phenotype.Parts[i].Family != null;

        /// <summary>
        /// A family part's broadphase radius at its present size, for both contact shapes: the geometry's reach
        /// at the reference size times the part's <see cref="PhenotypePart.FamilyScale"/>. With a hull core it is
        /// every core point plus the margin and every point of the solid; with sphere contact there is no core, and
        /// it is the solid's reach alone (D255), so that no contact core is built. The half-diagonal does not hold
        /// a tapered part: a taper of 4 reaches 1.6 half-extents at its wide end, and its pole stands farther than
        /// b from the origin.
        /// </summary>
        private double FamilyReach(PhenotypePart part) =>
            (LinkCoreHull != null ? part.Family.ReferenceReach : part.Family.ReferenceSolidReach) * (double)part.FamilyScale;

        /// <summary>
        /// The rest axis of one degree of freedom, in the joint frame — X for the first,
        /// Y for the second, Z for the third, which is <c>EffectorDriver.DriveAxis</c>.
        /// </summary>
        /// <summary>
        /// One link's inertia about an axis through a point of its own frame: the tensor's
        /// component along the axis plus the parallel-axis term for the centre of mass standing
        /// off the line.
        /// </summary>
        private static double AxisInertia(Vec3 axis, Vec3 anchor, Vec3 inertia, double mass)
        {
            double along = axis.X * axis.X * inertia.X +
                           axis.Y * axis.Y * inertia.Y +
                           axis.Z * axis.Z * inertia.Z;

            Vec3 perpendicular = anchor - axis * Vec3.Dot(anchor, axis);
            return along + mass * perpendicular.SqrMagnitude;
        }

        internal static Vec3 AxisOf(int d) =>
            d == 0 ? Vec3.UnitX : d == 1 ? Vec3.UnitY : Vec3.UnitZ;

        /// <summary>
        /// <c>PhenotypeBuilder.JointFrameRotation</c>: a hinge bends across the attachment axis
        /// and everything else twists about it.
        /// </summary>
        internal static QuatD FrameOf(JointType type) =>
            FrameIsTurned(type)
                ? QuatD.FromEuler(0, System.Math.PI * 0.5, 0)
                : QuatD.Identity;

        /// <summary>
        /// Whether <see cref="FrameOf"/> turns the joint frame off the part's own axes: a hinge's first axis
        /// lies across the attachment axis, and every other joint's lies along it. D234 reads it, since a
        /// reflection reverses the one and not the other (<c>Creature.Twin.cs</c>).
        /// </summary>
        internal static bool FrameIsTurned(JointType type) => type == JointType.Hinge;

        /// <summary>
        /// The diagonal inertia tensor of one part about its own centre, in its own axes.
        /// </summary>
        /// <remarks>
        /// <b>Computed here and read from the collider in the farm.</b> Unity derives an
        /// <c>ArticulationBody</c>'s tensor from its collider and its mass, and this is the
        /// analytic answer for the same three solids: the box from its half-extents, the sphere
        /// from <see cref="SphereShape.Radius"/>, the capsule as a cylinder plus two
        /// hemispheres about <see cref="CapsuleShape.HalfSpan"/>. A shape that is none of the three
        /// and is not the family's falls back to the box's tensor over its half-extents, which is
        /// the loosest honest answer and is counted nowhere — no run in the record carries one.
        /// The family's shape (D233) never falls back: its tensor needs the part's form, so this
        /// overload refuses it and <see cref="InertiaOf(PartShape, PhenotypePart, double)"/> is the
        /// one to call.
        /// </remarks>
        public static Vec3 InertiaOf(PartShape shape, Float3 halfExtents, double mass)
        {
            switch (shape)
            {
                case SuperellipsoidShape _:
                    throw new ArgumentException(
                        "A superellipsoid's inertia needs the part's form; call the overload that " +
                        "takes the part.", nameof(shape));

                case SphereShape _:
                {
                    double r = SphereShape.Radius(halfExtents);
                    double i = 0.4 * mass * r * r;
                    return new Vec3(i, i, i);
                }

                case CapsuleShape _:
                {
                    double r = CapsuleShape.Radius(halfExtents);
                    double s = CapsuleShape.HalfSpan(halfExtents);

                    double cylinderVolume = System.Math.PI * r * r * 2.0 * s;
                    double capVolume = (4.0 / 3.0) * System.Math.PI * r * r * r;
                    double total = cylinderVolume + capVolume;

                    if (!(total > 0)) return new Vec3(mass, mass, mass);

                    double cylinderMass = mass * (cylinderVolume / total);
                    double capMass = mass * (capVolume / total) * 0.5;   // one hemisphere

                    double along = 0.5 * cylinderMass * r * r + 2.0 * capMass * 0.4 * r * r;

                    double across =
                        cylinderMass * (r * r / 4.0 + s * s / 3.0) +
                        2.0 * capMass * (0.4 * r * r + s * s + 0.75 * r * s);

                    return new Vec3(across, along, across);   // Y is the long axis
                }

                default:
                {
                    double hx = System.Math.Abs(halfExtents.X);
                    double hy = System.Math.Abs(halfExtents.Y);
                    double hz = System.Math.Abs(halfExtents.Z);
                    double k = mass / 3.0;
                    return new Vec3(
                        k * (hy * hy + hz * hz),
                        k * (hx * hx + hz * hz),
                        k * (hx * hx + hy * hy));
                }
            }
        }

        /// <summary>
        /// The diagonal inertia tensor of one developed part about its own centre, in its own axes —
        /// <see cref="InertiaOf(PartShape, Float3, double)"/> for a part of a named shape, and the
        /// superellipsoid's own for a family part (D233).
        /// </summary>
        /// <remarks>
        /// A family part's frame has its centre of mass at the origin (<see cref="Superellipsoid"/>), so
        /// the tensor about the origin is the one about the centre of mass. It is the geometry's
        /// per-kilogram tensor at the part's present half-extents times the mass, an arithmetic of
        /// factors and powers that costs no panel and no beta function at a growth step.
        /// </remarks>
        public static Vec3 InertiaOf(PartShape shape, PhenotypePart part, double mass)
        {
            if (part.Family == null) return InertiaOf(shape, part.HalfExtents, mass);

            Double3 perMass = part.Family.Geometry.InertiaPerMass(part.HalfExtents);
            return new Vec3(perMass.X, perMass.Y, perMass.Z) * mass;
        }

        internal static Vec3 ToVec(Float3 v) => new Vec3(v.X, v.Y, v.Z);

        /// <summary>Whether every link's pose and motion is still a number.</summary>
        public bool IsFinite()
        {
            for (int i = 0; i < Links; i++)
            {
                double sum =
                    Position[3 * i] + Position[3 * i + 1] + Position[3 * i + 2] +
                    Spin[3 * i] + Spin[3 * i + 1] + Spin[3 * i + 2] +
                    Velocity[3 * i] + Velocity[3 * i + 1] + Velocity[3 * i + 2];

                if (double.IsNaN(sum) || double.IsInfinity(sum)) return false;
            }
            return true;
        }

        /// <summary>The fastest link origin in the body, m/s.</summary>
        public double MaxLinkSpeed()
        {
            double worst = 0;
            for (int i = 0; i < Links; i++)
            {
                double speed = Vec3.Read(Velocity, 3 * i).Magnitude;
                if (speed > worst) worst = speed;
            }
            return worst;
        }

        /// <summary>Centre of mass of the whole body, world space.</summary>
        public Vec3 CentreOfMass()
        {
            Vec3 sum = Vec3.Zero;
            for (int i = 0; i < Links; i++) sum += Vec3.Read(Position, 3 * i) * Mass[i];
            return TotalMass > 0 ? sum * (1.0 / TotalMass) : Vec3.Zero;
        }

        /// <summary>Linear momentum of the whole body, kg·m/s.</summary>
        public Vec3 LinearMomentum()
        {
            Vec3 sum = Vec3.Zero;
            for (int i = 0; i < Links; i++) sum += Vec3.Read(Velocity, 3 * i) * Mass[i];
            return sum;
        }

        /// <summary>Angular momentum about <paramref name="about"/>, world axes.</summary>
        public Vec3 AngularMomentum(Vec3 about)
        {
            Vec3 sum = Vec3.Zero;
            for (int i = 0; i < Links; i++)
            {
                Mat3 r = Mat3.Read(RotationMatrix, 9 * i);
                Mat3 inertia = Mat3.RotateDiagonal(r, Vec3.Read(InertiaLocal, 3 * i));
                Vec3 w = Vec3.Read(Spin, 3 * i);
                Vec3 offset = Vec3.Read(Position, 3 * i) - about;

                sum += inertia * w + Vec3.Cross(offset, Vec3.Read(Velocity, 3 * i) * Mass[i]);
            }
            return sum;
        }

        /// <summary>Total kinetic energy, joules.</summary>
        public double KineticEnergy()
        {
            double total = 0;
            for (int i = 0; i < Links; i++)
            {
                Mat3 r = Mat3.Read(RotationMatrix, 9 * i);
                Mat3 inertia = Mat3.RotateDiagonal(r, Vec3.Read(InertiaLocal, 3 * i));
                Vec3 w = Vec3.Read(Spin, 3 * i);
                Vec3 v = Vec3.Read(Velocity, 3 * i);

                total += 0.5 * Mass[i] * v.SqrMagnitude + 0.5 * Vec3.Dot(w, inertia * w);
            }
            return total;
        }
    }
}
