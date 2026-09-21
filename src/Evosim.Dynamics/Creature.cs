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

        public readonly Phenotype Phenotype;

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

        /// <summary>Total body volume, m3 — what D064's buoyancy factor is a function of.</summary>
        public readonly double TotalVolume;

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

        // ---------------------------------------------------------------- drag panels

        /// <summary>Where link <i>i</i>'s panels start. Length <c>Links + 1</c>.</summary>
        public readonly int[] PanelStart;

        public readonly double[] PanelCentre;
        public readonly double[] PanelNormal;
        public readonly double[] PanelArea;

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

        /// <summary>Total mass of the body, kg — the contact spring is scaled by it.</summary>
        public readonly double TotalMass;

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

            shapes = shapes ?? PartShapeRegistry.Standard;

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
            Volume = new double[Links];
            Lift = new double[Links];
            Power = new double[Links];

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

            // ---- inertial properties and the joint frames

            double totalVolume = 0, totalMass = 0;
            var panelSets = new DragPanelSet[Links];
            var scratch = new List<DragPanel>(64);
            int panels = 0;

            for (int i = 0; i < Links; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                PartShape shape = shapes.Resolve(part.ShapeId);

                double volume = part.Volume;
                Volume[i] = volume;
                Lift[i] = part.Lift;
                Power[i] = part.Power;
                totalVolume += volume;

                // The builder's own two lines: the plain mass with its floor, then the effective
                // mass once the water a part drags along is folded in.
                double plain = System.Math.Max(config.MinimumLinkMass, volume * config.TissueDensity);
                double effective = plain + config.AddedMassCoefficient * config.Density * volume;
                PlainMass[i] = plain;
                Mass[i] = effective;
                totalMass += effective;

                // PhysX recomputes a link's tensor from its collider whenever the mass is written,
                // so an inflated mass inflates the tensor in proportion. The same is done here,
                // from an analytic tensor rather than from the engine's — see the report.
                Vec3 diag = InertiaOf(shape, part.HalfExtents, plain) * (effective / plain);
                Vec3.Write(InertiaLocal, 3 * i, diag);
                SmallestInertia[i] = System.Math.Min(diag.X, System.Math.Min(diag.Y, diag.Z));

                Vec3.Write(ChildAnchor, 3 * i, ToVec(part.ChildAnchorLocal));
                Vec3.Write(ParentAnchor, 3 * i, ToVec(part.ParentAnchorLocal));

                QuatD frame = FrameOf(part.JointType);
                QuatD rest = QuatD.Identity;

                if (!part.IsRoot)
                {
                    QuatD parentRotation = QuatD.From(phenotype.Parts[part.ParentIndex].Rotation);
                    QuatD own = QuatD.From(part.Rotation);
                    QuatD relative = parentRotation.Conjugate * own;
                    rest = relative * frame;
                }

                QuatD.Write(JointFrame, 4 * i, frame);
                QuatD.Write(RestFrame, 4 * i, rest);

                panelSets[i] = DragPanelSet.For(shape, part.HalfExtents, config.PanelsPerAxis, scratch);
                panels += panelSets[i].Count;
            }

            TotalVolume = totalVolume;
            TotalMass = totalMass;

            // ---- joint limits and their springs

            for (int i = 0; i < Links; i++)
            {
                int n = DofCount[i];
                if (n == 0) continue;

                PhenotypePart part = phenotype.Parts[i];
                int parent = Parent[i];
                int at = DofStart[i];
                QuatD frame = QuatD.Read(JointFrame, 4 * i);
                QuatD rest = QuatD.Read(RestFrame, 4 * i);
                Vec3 childAnchor = Vec3.Read(ChildAnchor, 3 * i);
                Vec3 parentAnchor = Vec3.Read(ParentAnchor, 3 * i);

                double omega = config.JointLimitOmegaTimesStep / config.StepSeconds;

                for (int d = 0; d < n; d++)
                {
                    // PhenotypeBuilder.Limit: a missing entry is [-1, 1] radians.
                    Float2 limit = d < part.JointLimits.Length
                        ? part.JointLimits[d]
                        : new Float2(-1f, 1f);

                    LimitLo[at + d] = limit.X;
                    LimitHi[at + d] = limit.Y;

                    // The two-body reduced inertia about this axis through the anchor, taken at
                    // the rest configuration. It is a LOWER bound on the inertia the relative
                    // coordinate actually carries — anything hung off either side raises it —
                    // and a penalty spring scaled by a lower bound can only be softer than the
                    // step allows, never stiffer. Scaling by the child's own inertia instead,
                    // which is the obvious thing and what this did first, makes the spring too
                    // stiff whenever the parent is light: the damping term's explicit stability
                    // condition is violated and a driven chain rings itself to infinity within
                    // two seconds.
                    double child = AxisInertia(
                        frame.Rotate(AxisOf(d)), childAnchor,
                        Vec3.Read(InertiaLocal, 3 * i), Mass[i]);

                    double above = AxisInertia(
                        rest.Rotate(AxisOf(d)), parentAnchor,
                        Vec3.Read(InertiaLocal, 3 * parent), Mass[parent]);

                    double reduced = child + above > 0 ? child * above / (child + above) : child;

                    LimitStiffness[at + d] = reduced * omega * omega;
                    LimitDamping[at + d] = 2.0 * config.JointLimitDampingRatio * reduced * omega;
                }
            }

            // ---- panels, flattened

            PanelStart = new int[Links + 1];
            PanelCentre = new double[3 * panels];
            PanelNormal = new double[3 * panels];
            PanelArea = new double[panels];

            int cursor = 0;
            for (int i = 0; i < Links; i++)
            {
                PanelStart[i] = cursor;
                DragPanelSet set = panelSets[i];
                for (int p = 0; p < set.Count; p++)
                {
                    Vec3.Write(PanelCentre, 3 * cursor, ToVec(set.Centres[p]));
                    Vec3.Write(PanelNormal, 3 * cursor, ToVec(set.Normals[p]));
                    PanelArea[cursor] = set.Areas[p];
                    cursor++;
                }
            }
            PanelStart[Links] = cursor;

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

            Brain = Brain.For(phenotype);
            DriveSignal = new float[System.Math.Max(1, Brain.TotalDof)];
            Drive = new EffectorDrive(this, config);
            Senses = new CreatureSenses(this, config);

            BasePosition = ToVec(phenotype.Parts[0].Position);
            BaseRotation = QuatD.From(phenotype.Parts[0].Rotation);

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
        }

        /// <summary>Takes a body the solver has lost out of everyone else's contact set.</summary>
        public void MarkLost()
        {
            _pendingActive = false;
            _pendingRadius = 0;
        }

        /// <summary>Makes this step's sphere the one other bodies will read. Serial, between steps.</summary>
        public void CommitContactSphere()
        {
            ContactCentre = _pendingCentre;
            ContactRadius = _pendingRadius;
            ContactVelocity = _pendingVelocity;
            ContactActive = _pendingActive;
        }

        /// <summary>Half the diagonal of each link's own extent, metres — the sphere's padding.</summary>
        public double[] LinkReach => _linkReach ?? (_linkReach = BuildReach());

        private double[] _linkReach;

        private double[] BuildReach()
        {
            var reach = new double[Links];
            for (int i = 0; i < Links; i++)
            {
                Float3 h = Phenotype.Parts[i].HalfExtents;
                reach[i] = System.Math.Sqrt(
                    (double)h.X * h.X + (double)h.Y * h.Y + (double)h.Z * h.Z);
            }
            return reach;
        }

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
            type == JointType.Hinge
                ? QuatD.FromEuler(0, System.Math.PI * 0.5, 0)
                : QuatD.Identity;

        /// <summary>
        /// The diagonal inertia tensor of one part about its own centre, in its own axes.
        /// </summary>
        /// <remarks>
        /// <b>Computed here and read from the collider in the farm.</b> Unity derives an
        /// <c>ArticulationBody</c>'s tensor from its collider and its mass, and this is the
        /// analytic answer for the same three solids: the box from its half-extents, the sphere
        /// from <see cref="SphereShape.Radius"/>, the capsule as a cylinder plus two
        /// hemispheres about <see cref="CapsuleShape.HalfSpan"/>. A shape the registry resolves
        /// to none of the three falls back to the box's tensor over its half-extents, which is
        /// the loosest honest answer and is counted nowhere — no run in the record carries one.
        /// </remarks>
        public static Vec3 InertiaOf(PartShape shape, Float3 halfExtents, double mass)
        {
            switch (shape)
            {
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
