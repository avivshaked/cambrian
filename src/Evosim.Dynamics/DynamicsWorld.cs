using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Steps a population of creatures, each one alone, spread over as many threads as asked
    /// for — and to the same bits at any thread count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What makes the thread count invisible.</b> Every buffer a creature's step touches
    /// belongs to that creature. The one thing read across bodies is the contact grid, which is
    /// built serially before the parallel phase from state that is then frozen, and every sum
    /// taken over other bodies is taken in ascending creature order. Nothing is accumulated into
    /// a shared total during the step: the limiter counts live on the creature and are added up
    /// afterwards, in id order.
    /// </para>
    /// <para>
    /// <b>Still water.</b> The spike's water does not move
    /// (<c>Creature.Water</c> stays zero), so the current's velocity and acceleration are both
    /// zero and D090's Morison term is identically zero. The hooks are in
    /// <see cref="Fluid.Apply"/> for a <c>CurrentField</c> to be dropped into; nothing here
    /// samples one.
    /// </para>
    /// </remarks>
    public sealed partial class DynamicsWorld
    {
        private readonly List<Creature> _creatures = new List<Creature>();
        private readonly ContactGrid _grid = new ContactGrid();
        private int[][] _neighbourScratch = Array.Empty<int[]>();

        public SolverConfig Config { get; }

        /// <summary>Simulated seconds stepped.</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>Steps taken.</summary>
        public long Steps { get; private set; }

        /// <summary>How many threads the parallel phase may use. 1 is a plain serial loop.</summary>
        public int Threads { get; set; } = 1;

        public IReadOnlyList<Creature> Creatures => _creatures;

        /// <summary>
        /// The bench's probe: a contact-grid cell size in place of the rule, metres; 0 is the
        /// rule. No farm sets it.
        /// </summary>
        public double ContactCellOverrideMetres { get; set; }

        /// <summary>The contact grid's cell after the last step, metres.</summary>
        public double ContactCellMetres => _grid.CellSize;

        /// <summary>The largest active bounding radius the last step saw, metres.</summary>
        public double LargestContactRadius => _grid.LargestRadius;

        /// <summary>
        /// The names of the five phases of <see cref="Step"/>, in order: the contact grid's build,
        /// the water sampled for every body, the parallel body phase, the contact commit and the
        /// after-step (the contact census and the digest row).
        /// </summary>
        /// <remarks>
        /// <b>Why the step is timed by phase.</b> Only the third phase runs on more than one
        /// thread. The farm's rows read the whole step as `wallPhysicsMs` and could not say how
        /// much of it was serial, which is what decides whether more threads buy pace: on the
        /// night of 2026-09-22 a full crowd cost 1.6 to 2.7 µs a body-step at 8 and at 12
        /// threads alike, five times the bench's 24-thread number, and the bench's bodies sample
        /// no water. Five timestamps a step, no trajectory touched.
        /// </remarks>
        public static readonly string[] PhaseNames = { "grid", "water", "bodies", "commit", "after" };

        /// <summary>Stopwatch ticks spent in each phase of <see cref="PhaseNames"/> since construction.</summary>
        public readonly long[] PhaseTicks = new long[5];

        /// <summary>Milliseconds spent in phase <paramref name="phase"/> of <see cref="PhaseNames"/>.</summary>
        public long PhaseMs(int phase) => PhaseTicks[phase] * 1000L / Stopwatch.Frequency;

        public DynamicsWorld(SolverConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Creature Add(Creature body)
        {
            _creatures.Add(body);
            if (_neighbourScratch.Length < _creatures.Count)
            {
                Array.Resize(ref _neighbourScratch, System.Math.Max(8, _creatures.Count * 2));
            }
            int at = _creatures.Count - 1;
            _neighbourScratch[at] = _neighbourScratch[at] ?? new int[32];
            return body;
        }

        public void Step()
        {
            double dt = Config.StepSeconds;

            long t0 = Stopwatch.GetTimestamp();

            _grid.Build(_creatures, ContactCellOverrideMetres);

            long t1 = Stopwatch.GetTimestamp();

            // Package C. Serial and before the parallel phase, over the poses the step starts
            // from — the farm's gather phase, and the one place a CurrentField may be touched.
            SampleWater();

            long t2 = Stopwatch.GetTimestamp();

            int count = _creatures.Count;

            // One code path at every thread count, including one. A serial `for` and a
            // `Parallel.For` delegate are different code to the JIT, and .NET's tiered
            // compilation gives quick-JITted and optimised loops different floating-point bits
            // where the arithmetic sits on a rounding knife-edge — so the serial path could part
            // from the parallel one at one thread and the digest would say the thread count
            // changed the trajectory when what changed was which compiler tier ran. The test and
            // bench projects also set `TieredCompilation` false; a farm that reports a digest
            // must do the same.
            var options = new ParallelOptions { MaxDegreeOfParallelism = Threads < 1 ? 1 : Threads };
            Parallel.For(0, count, options, i => StepOne(i, dt));

            long t3 = Stopwatch.GetTimestamp();

            // Serial, between steps: what every body will read of every other on the next one.
            // Committing inside the parallel phase is what made the digest depend on the thread
            // count — see Creature's contact fields.
            for (int i = 0; i < count; i++) _creatures[i].CommitContactSphere();

            long t4 = Stopwatch.GetTimestamp();

            ElapsedSeconds += dt;
            Steps++;

            AfterStep();

            long t5 = Stopwatch.GetTimestamp();

            PhaseTicks[0] += t1 - t0;
            PhaseTicks[1] += t2 - t1;
            PhaseTicks[2] += t3 - t2;
            PhaseTicks[3] += t4 - t3;
            PhaseTicks[4] += t5 - t4;
        }

        private void StepOne(int index, double dt)
        {
            Creature body = _creatures[index];
            if (!body.Alive) return;

            body.Senses.Sample();
            body.Brain.Step((float)dt, body.DriveSignal, body.Senses);

            Array.Clear(body.Fext, 0, body.Fext.Length);
            Array.Clear(body.Tau, 0, body.Tau.Length);

            body.Drive.Drive(body.DriveSignal);
            Fluid.Apply(body, Config);
            Contacts.Apply(body, index, _grid, Config, ref _neighbourScratch[index]);
            JointTorques(body);

            Aba.Solve(body);
            Aba.Integrate(body, dt);

            Kinematics.Poses(body);
            Kinematics.Velocities(body);

            // Package B. Where the farm calls EffectorDriver.Settle and FluidEnvironment.Settle:
            // immediately past the solve, against the velocities it produced. Per body and
            // inside the parallel phase, because every accumulator it touches is this body's.
            body.Settle(dt);

            body.RecordTraceFrame(Steps + 1, (Steps + 1) * dt);

            if (!body.IsFinite())
            {
                body.Alive = false;
                body.MarkLost();
                return;
            }

            body.RefreshContactSphere();
        }

        /// <summary>
        /// The two generalised torques: the drive's own damping, which
        /// <c>PhenotypeBuilder.MakeDrive</c> sets at 1 N·m·s/rad so that undriven joints settle
        /// rather than ring, and the limit springs.
        /// </summary>
        /// <remarks>
        /// <b>A limit is a spring here and a hard constraint in PhysX.</b> An
        /// <c>ArticulationDofLock.LimitedMotion</c> is solved as an inequality constraint and
        /// cannot be overshot; this is a penalty, so a joint driven hard into its stop passes it
        /// by whatever the spring allows. The stiffness is <c>I·omega^2</c> per degree of
        /// freedom against that joint's own inertia about its axis, with
        /// <c>omega = 0.5/dt</c> — see <see cref="SolverConfig.JointLimitOmegaTimesStep"/> for
        /// why that number and not a stiffer one.
        /// </remarks>
        private void JointTorques(Creature body)
        {
            double damping = Config.JointDriveDamping;
            double dt = Config.StepSeconds;

            for (int i = 1; i < body.Links; i++)
            {
                int n = body.DofCount[i];
                if (n == 0) continue;

                int at = body.DofStart[i];
                for (int d = 0; d < n; d++)
                {
                    int j = at + d;
                    double q = body.Q[j];
                    double rate = body.Qd[j];
                    double torque = -damping * rate;

                    // The drive damper carried implicitly, beside the limit's term and for the
                    // same reason. A viscous damper integrated explicitly grows rather than
                    // decays once `c dt / I > 2`, and the farm's 1 N·m·s/rad at dt 0.01 makes
                    // that bound `I < 0.005 kg·m2` — which every newborn is under: a 0.4 kg
                    // two-link body scaled to a third of adult size carries about 5e-4 and sits
                    // twenty times past the bound. Nothing in still water shows it, because a
                    // body holding a pose has no joint rate to amplify; one touch is enough.
                    // `-damping * (rate + qdd * dt)` is the same torque written against the
                    // acceleration the solve is about to produce, so the explicit part above is
                    // unchanged and `damping * dt` goes on the diagonal.
                    double resist = damping * dt;

                    double past = q < body.LimitLo[j] ? q - body.LimitLo[j]
                        : q > body.LimitHi[j] ? q - body.LimitHi[j]
                        : 0;

                    if (past != 0)
                    {
                        double k = body.LimitStiffness[j];
                        double c = body.LimitDamping[j];

                        torque += -k * past - c * rate;
                        resist += (c + k * dt) * dt;
                    }

                    body.LimitImplicit[j] = resist;

                    // Package B's third accumulator: the damper's and the limit spring's own
                    // work, which PhysX does inside its solver where nothing can see it.
                    body.NotePassiveTorque(j, torque, rate);

                    body.Tau[j] += torque;
                }
            }
        }

        /// <summary>
        /// A hash over every link's place, attitude and motion, in creature order then link
        /// order — the whole of the state a trajectory is, to the bit.
        /// </summary>
        /// <remarks>
        /// FNV-1a over the raw bits of every double, so two runs agree here only if every
        /// number in them is identical. A tolerance would defeat the purpose: the question the
        /// digest answers is whether the thread count changed the arithmetic, and any change at
        /// all is the answer.
        /// </remarks>
        public ulong Digest()
        {
            ulong hash = 14695981039346656037UL;

            for (int c = 0; c < _creatures.Count; c++)
            {
                Creature body = _creatures[c];
                Mix(ref hash, (ulong)body.Id);

                for (int i = 0; i < body.Links; i++)
                {
                    for (int k = 0; k < 3; k++) Mix(ref hash, Bits(body.Position[3 * i + k]));
                    for (int k = 0; k < 4; k++) Mix(ref hash, Bits(body.Rotation[4 * i + k]));
                    for (int k = 0; k < 3; k++) Mix(ref hash, Bits(body.Spin[3 * i + k]));
                    for (int k = 0; k < 3; k++) Mix(ref hash, Bits(body.Velocity[3 * i + k]));
                }
            }

            return hash;
        }

        private static ulong Bits(double v) => (ulong)BitConverter.DoubleToInt64Bits(v);

        private static void Mix(ref ulong hash, ulong value)
        {
            for (int b = 0; b < 8; b++)
            {
                hash ^= (value >> (8 * b)) & 0xFF;
                hash *= 1099511628211UL;
            }
        }

        /// <summary>Bodies the solver has lost — any link that stopped being a number.</summary>
        public int NonFiniteBodies()
        {
            int n = 0;
            for (int i = 0; i < _creatures.Count; i++) if (!_creatures[i].Alive) n++;
            return n;
        }

        /// <summary>The fastest link in the world, m/s.</summary>
        public double MaxLinkSpeed()
        {
            double worst = 0;
            for (int i = 0; i < _creatures.Count; i++)
            {
                if (!_creatures[i].Alive) continue;
                double speed = _creatures[i].MaxLinkSpeed();
                if (speed > worst) worst = speed;
            }
            return worst;
        }

        public long DriveImpulsesLimited()
        {
            long n = 0;
            for (int i = 0; i < _creatures.Count; i++) n += _creatures[i].DriveImpulsesLimited;
            return n;
        }

        public long DragImpulsesLimited()
        {
            long n = 0;
            for (int i = 0; i < _creatures.Count; i++) n += _creatures[i].DragImpulsesLimited;
            return n;
        }

        public int TotalLinks()
        {
            int n = 0;
            for (int i = 0; i < _creatures.Count; i++) n += _creatures[i].Links;
            return n;
        }
    }
}
