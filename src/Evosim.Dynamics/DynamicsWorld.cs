using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Evosim.Core;

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

        /// <summary>
        /// The contact query's extremes over the stats row's window (<see cref="ContactWatch"/>): the CPU
        /// solver notes into it as it builds and queries, a backend after each block. The row closes it.
        /// </summary>
        public ContactWatch Watch => _grid.Watch;
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

        /// <summary>
        /// The largest active bounding radius the last step saw, metres: a body's, or under
        /// per-part contact (D114) a link's.
        /// </summary>
        public double LargestContactRadius => _grid.LargestRadius;

        /// <summary>
        /// The names of the five phases of <see cref="Step"/>, in order: the contact grid's build,
        /// the water sampled for every body, the parallel body phase, the contact commit and the
        /// after-step (the contact census and the digest row).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Two of the five are empty from 2026-10-03</b>
        /// (<c>logbook/specs/farm-physics-speed-read.md</c> P1 and P2). The commit is made inside
        /// the next step's grid build and its time is the grid's; the water is sampled inside
        /// each body's own step and its time is the bodies'. The water phase is left holding the
        /// pin alone and the commit phase nothing, so the rows' <c>wallStepWaterMs</c> and
        /// <c>wallStepCommitMs</c> read 0 and keep their places in the row. A reading of the five
        /// across that date compares grid with grid plus commit, and bodies with bodies plus
        /// water.
        /// </para>
        /// <para>
        /// <b>Why the step is timed by phase.</b> Only the third phase runs on more than one
        /// thread. The farm's rows read the whole step as `wallPhysicsMs` and could not say how
        /// much of it was serial, which is what decides whether more threads buy pace: on the
        /// night of 2026-09-22 a full crowd cost 1.6 to 2.7 µs a body-step at 8 and at 12
        /// threads alike, five times the bench's 24-thread number, and the bench's bodies sample
        /// no water. Five timestamps a step, no trajectory touched.
        /// </para>
        /// </remarks>
        public static readonly string[] PhaseNames = { "grid", "water", "bodies", "commit", "after" };

        /// <summary>Stopwatch ticks spent in each phase of <see cref="PhaseNames"/> since construction.</summary>
        public readonly long[] PhaseTicks = new long[5];

        /// <summary>Milliseconds spent in phase <paramref name="phase"/> of <see cref="PhaseNames"/>.</summary>
        public long PhaseMs(int phase) => PhaseTicks[phase] * 1000L / Stopwatch.Frequency;

        /// <summary>
        /// Off by default. When set, every body's step is timed in four parts into
        /// <see cref="BodyTicks"/>: the water at its links, its senses, its brain, and the rest of its step.
        /// </summary>
        /// <remarks>
        /// Set once by a measuring program (the nursery's <c>EVOSIM_NURSERY_PROFILE</c>) before any
        /// world steps. Nothing the step computes reads it or the ticks, so a run with it on is the
        /// same realisation as one with it off; the nursery's digest check is what says so. Five
        /// timestamps a body-step, so it costs a few percent of a small body's step while on.
        /// </remarks>
        public static bool ProfileBodySteps;

        /// <summary>
        /// With <see cref="ProfileBodySteps"/>: Stopwatch ticks in a body's senses [0], its brain [1]
        /// and the rest of its step [2], the body-steps counted [3], and the water sampled at its links
        /// [4], summed over every body and step. Added with <c>Interlocked</c>, because a farm world's
        /// bodies share it across threads.
        /// </summary>
        public readonly long[] BodyTicks = new long[5];

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

        /// <summary>
        /// One physics step: the CPU solver's, or one step of the <see cref="Backend"/> when a
        /// world has one.
        /// </summary>
        public void Step()
        {
            if (Backend == null) StepOnCpu();
            else
            {
                // A backend reads every body's committed sphere as its block's first step's.
                CommitContactSpheres();
                Backend.StepBlock(this, 1);
            }
        }

        /// <summary>
        /// Commits every body's sphere that the last step left pending: what a reader of the
        /// committed spheres calls between steps, before the next step's grid build would have.
        /// </summary>
        /// <remarks>
        /// The last CPU step leaves each body's new sphere pending and the next grid build commits
        /// it (<see cref="Creature.EnsureContactCommitted"/>). A body is committed in its own
        /// slots alone, so the pass splits across the world's threads and the numbers are the
        /// same at any count; a body already committed is left as it is.
        /// </remarks>
        public void CommitContactSpheres()
        {
            int count = _creatures.Count;
            Slabs.Run(count, Slabs.CountFor(count, Threads), (s, from, to) =>
            {
                for (int i = from; i < to; i++) _creatures[i].EnsureContactCommitted();
            });
        }

        private void StepOnCpu()
        {
            double dt = Config.StepSeconds;

            long t0 = Stopwatch.GetTimestamp();

            // D114: the links' spheres rather than the bodies' when the world asks for it.
            _grid.Threads = Threads;
            BuildGrid();

            // A sphere out of the grid's reach is a body diverged since the last check: it is
            // handed to the caller to kill, taken out of the step, and the grid built again
            // without it, all before anything reads the grid. Nothing happens here in a step in
            // which no sphere strays, so such a step is the step it was.
            if (_grid.Strays.Count > 0) LoseStrays();

            long t1 = Stopwatch.GetTimestamp();

            // At one thread the bodies are stepped by a plain loop, as every nursery tank is. The
            // loop and Parallel.For call the same method, StepOne, so its arithmetic is the same
            // compiled code either way. The rule that kept one code path at every thread count
            // was against .NET's tiered compilation, which gives quick-JITted and optimised code
            // different floating-point bits where the arithmetic sits on a rounding knife-edge;
            // every program and test project that reports a digest sets `TieredCompilation`
            // false, and that setting is what the rule rests on now. The digest at one and at N
            // threads is the check (logbook/specs/speed-plan.md, A3).
            var options = new ParallelOptions { MaxDegreeOfParallelism = Threads < 1 ? 1 : Threads };

            // Package C. The field pinned at the step's clock for the whole parallel phase, so
            // that every body samples it from its own thread as the first thing it does
            // (StepOne). A no-op for still water and for water with nothing to pin.
            CurrentField current = Config.Current;
            current?.PinInstant(ElapsedSeconds);

            long t2 = Stopwatch.GetTimestamp();
            long t3;

            int count = _creatures.Count;

            try
            {
                double seconds = ElapsedSeconds;
                if (Threads <= 1)
                {
                    for (int i = 0; i < count; i++) StepOne(i, dt, seconds);
                }
                else
                {
                    Parallel.For(0, count, options, i => StepOne(i, dt, seconds));
                }
            }
            finally
            {
                current?.UnpinInstant();
                t3 = Stopwatch.GetTimestamp();
            }

            // No commit here. What every body will read of every other on the next step is
            // committed by the next grid build, body by body as it gathers them
            // (Creature.EnsureContactCommitted), or sooner by a reader between steps that needs
            // it. Committing inside the parallel phase is what made the digest depend on the
            // thread count (see Creature's contact fields); the build runs after the phase has
            // ended, so the copy is the same copy, one pass later and without a pass of its own
            // (logbook/specs/farm-physics-speed-read.md P1).
            long t4 = t3;

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

        private void BuildGrid()
        {
            // D114: the links' spheres rather than the bodies' when the world asks for it.
            if (Config.ContactPerPart) _grid.BuildLinks(_creatures, ContactCellOverrideMetres);
            else _grid.Build(_creatures, ContactCellOverrideMetres);
        }

        /// <summary>
        /// Who is told of a body whose contact sphere strayed out of the grid's reach, before the
        /// body is taken out of the step: the farm's divergence path, which dumps it and kills it
        /// as a counted death. Null in a world with no caller to tell, where the body is only lost.
        /// </summary>
        /// <remarks>
        /// Called serially, inside <see cref="Step"/>, after the grid is built and before the water
        /// is pinned or any body is stepped, once a body, in body order, with the body's first
        /// stray sphere. The body is still in the list and still as the last step left it. The
        /// handler must not add or remove bodies; the farm removes the dead at its next reconcile.
        /// </remarks>
        public Action<Creature, ContactStray> StrayHandler { get; set; }

        /// <summary>
        /// Takes every body with a stray sphere out of this step: told to <see cref="StrayHandler"/>,
        /// marked lost as the solver marks a non-finite body, and left out of the grid, which is
        /// built again. Again until no sphere strays, because the grid's cell is made of the mean
        /// radius and a lost body's leaves it.
        /// </summary>
        private void LoseStrays()
        {
            while (_grid.Strays.Count > 0)
            {
                var strays = new List<ContactStray>(_grid.Strays);
                int last = -1;

                for (int i = 0; i < strays.Count; i++)
                {
                    ContactStray stray = strays[i];
                    if (stray.Body == last) continue;
                    last = stray.Body;

                    Creature body = _creatures[stray.Body];
                    StrayHandler?.Invoke(body, stray);

                    body.Alive = false;
                    body.MarkLost();
                    body.CommitContactSphere();
                }

                BuildGrid();
            }
        }

        private void StepOne(int index, double dt, double seconds)
        {
            Creature body = _creatures[index];
            if (!body.Alive) return;

            bool profile = ProfileBodySteps;
            long w0 = profile ? Stopwatch.GetTimestamp() : 0L;

            // Package C, first: the water at every link, over the poses the step starts from, with
            // the field pinned by the caller. It reads this body and the field alone, and the drag
            // pass (Fluid.Apply) is its only reader, so it may run anywhere before that; here the
            // body's arrays are hot for the rest of its step. Until 2026-10-03 it was a parallel
            // pass of its own between the grid build and this phase
            // (logbook/specs/farm-physics-speed-read.md P2).
            Water.Sample(body, Config, seconds);

            long s0 = profile ? Stopwatch.GetTimestamp() : 0L;

            body.Senses.Sample();

            long s1 = profile ? Stopwatch.GetTimestamp() : 0L;

            body.Brain.Step((float)dt, body.DriveSignal, body.Senses, body.Drive.BendAndPower ? body.BendSignal : null);

            if (profile)
            {
                long s2 = Stopwatch.GetTimestamp();
                Interlocked.Add(ref BodyTicks[0], s1 - s0);
                Interlocked.Add(ref BodyTicks[1], s2 - s1);
                Interlocked.Increment(ref BodyTicks[3]);
                Interlocked.Add(ref BodyTicks[4], s0 - w0);
                s0 = s2;
            }

            Array.Clear(body.Fext, 0, body.Fext.Length);
            Array.Clear(body.Tau, 0, body.Tau.Length);

            body.Drive.Drive(body.DriveSignal, body.BendSignal);
            Fluid.Apply(body, Config);
            Contacts.Apply(body, index, _grid, Config, ref _neighbourScratch[index]);
            if (Config.ContactInstrument) _grid.Watch.NoteOverlaps(body.OverlapCount, index, body.Id, body.Links);
            Anchor.Apply(body, Config, dt);   // D162, on the touch the contact pass just recorded
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
                body.MarkContactCommitDue();
                if (profile) Interlocked.Add(ref BodyTicks[2], Stopwatch.GetTimestamp() - s0);
                return;
            }

            body.RefreshContactSphere();
            body.MarkContactCommitDue();

            if (profile) Interlocked.Add(ref BodyTicks[2], Stopwatch.GetTimestamp() - s0);
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
