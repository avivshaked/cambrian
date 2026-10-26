using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm;

namespace Evosim.Nursery
{
    /// <summary>
    /// Something that looks at one tank episode while it is swum (logbook/specs/nursery-room-tank-plan.md, "The stepper"), and changes
    /// nothing in it. <c>TankEpisode</c> takes one as an optional parameter that <c>SwimTanks</c> leaves null; with it null the episode
    /// executes the statements it always did, and a watcher is handed an <see cref="EpisodeProbe"/>, never the world, the solver or the
    /// episode's <see cref="Rng"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The four calls, and what the episode has computed when each is made.</b>
    /// <see cref="Begin"/>: the snow is laid, the leaf tank's water and leaves are laid (LeafTank.cs), the bodies are inoculated and given their purses and the carrion is placed, and no step has
    /// been taken, so no body has a solver yet (its poses cannot be read until the first physics step). <see cref="Physics"/>: one
    /// physics step has been taken that did not run the economy; the poses, the senses and the brain are those of that step, and the
    /// ledger and the energy are the last economy step's. <see cref="Metabolic"/>: a physics step that ran the economy, after the
    /// episode has added the step's ledger to each body's sums. <see cref="End"/>: the last step has been taken and every
    /// <c>Resident</c> is filled in, before the solver is disposed.
    /// </para>
    /// <para>
    /// <b>A watcher that throws ends the episode</b>, since nothing catches it: the restep then writes no <c>result.json</c>.
    /// </para>
    /// </remarks>
    public interface ITankWatcher
    {
        void Begin(EpisodeProbe probe);
        void Physics(EpisodeProbe probe);
        void Metabolic(EpisodeProbe probe);
        void End(EpisodeProbe probe);
    }

    /// <summary>One link's pose in world space: the origin and the orientation quaternion, as the solver holds them.</summary>
    public readonly struct LinkPose
    {
        public readonly double X, Y, Z, Qx, Qy, Qz, Qw;

        public LinkPose(double x, double y, double z, double qx, double qy, double qz, double qw)
        {
            X = x; Y = y; Z = z; Qx = qx; Qy = qy; Qz = qz; Qw = qw;
        }
    }

    /// <summary>One carrion body of the episode as it now lies: its place and the joules it still holds. A copy: nothing here reaches the corpse.</summary>
    public readonly struct CorpseView
    {
        public readonly long CreatureId;
        public readonly double X, Y, Z, Joules;

        public CorpseView(long creatureId, double x, double y, double z, double joules)
        {
            CreatureId = creatureId; X = x; Y = y; Z = z; Joules = joules;
        }
    }

    /// <summary>One thing a body's senses offer its brain: a channel read on a part, and the axis or degree of freedom where the channel has several.</summary>
    public readonly struct SenseSpec
    {
        public readonly SensorChannel Channel;
        public readonly int Part, Index;

        public SenseSpec(SensorChannel channel, int part, int index)
        {
            Channel = channel; Part = part; Index = index;
        }
    }

    /// <summary>
    /// The one place a watcher reads an episode through: link poses, senses, brain units, outputs, energy and the ledger, the carrion, and the
    /// snow's density at a point. Every member returns a number, a flag or a copy; none returns the world, the solver, an organism, a corpse or a
    /// random stream, and none writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The calls this class must never make</b>, because each of them writes something an episode later reads, or draws, or builds a cache
    /// the solver's next step would then find: <c>Sample</c> (the senses' own refresh, which the solver calls once a step),
    /// <c>Refresh*</c> and <c>Commit*</c> (the contact sphere the other bodies read), <c>Step</c> (the solver's, the world's and the brain's), and
    /// <c>Creature.LinkReach</c> (a lazily built cache: it is built on first read, so a read here would be the first and would change when the
    /// solver builds it). <c>Brain.WriteState</c> is called, and is a read: it copies the brain's buffers into a stream. A test reads this
    /// file's source and fails if any of those names, or <c>Rng</c>, appears in code.
    /// </para>
    /// <para>
    /// <b>The one derived read that is not a field.</b> A brain's units have no public accessor in Core, whose edits are not this build's
    /// to make, so <see cref="TryBrainUnits"/> reads them out of the checkpoint stream <c>Brain.WriteState</c> writes: the tag, the clock, then
    /// the length and the floats of the buffer of the last completed step. A change to that layout makes the read refuse, not guess.
    /// </para>
    /// </remarks>
    public sealed class EpisodeProbe
    {
        private readonly World _world;
        private readonly Simulation _sim;
        private readonly Organism[] _focal;
        private readonly Corpse[] _placed;

        // The world writes Organism.LastLedger for bodies with absorptive tissue only. For every other body the last economy step's ledger is
        // its Lifetime ledger's change, taken once an economy step by AfterEconomy, which the episode calls before the watcher's Metabolic.
        private readonly EnergyLedger[] _lifetimeSeen;
        private readonly EnergyLedger[] _lastLedger;

        /// <summary>Built by the episode, after its carrion is placed: the corpses then in the world are the carrion placed.</summary>
        internal EpisodeProbe(World world, Simulation sim, Organism[] focal)
        {
            _world = world;
            _sim = sim;
            _focal = focal;
            _placed = world.Corpses.ToArray();
            _lifetimeSeen = new EnergyLedger[focal.Length];
            _lastLedger = new EnergyLedger[focal.Length];
            for (int i = 0; i < focal.Length; i++) if (focal[i] != null) _lifetimeSeen[i] = focal[i].Lifetime;
        }

        /// <summary>
        /// Takes each body's ledger for the economy step just ended, for the bodies whose LastLedger the world leaves at zero: the change in
        /// its Lifetime ledger. A read of fields the world has written; called by the episode once an economy step, and by nothing else.
        /// </summary>
        internal void AfterEconomy()
        {
            for (int i = 0; i < _focal.Length; i++)
            {
                Organism o = _focal[i];
                if (o == null || o.HasAbsorptiveTissue || !_world.Living.Contains(o)) continue;
                EnergyLedger now = o.Lifetime, was = _lifetimeSeen[i];
                _lastLedger[i] = new EnergyLedger(
                    new CellIntake(now.LightIncome - was.LightIncome, now.FoodIncome - was.FoodIncome, now.PoolDrawn - was.PoolDrawn,
                        now.LightCapacity - was.LightCapacity),
                    now.Upkeep - was.Upkeep, now.Neural - was.Neural, now.Work - was.Work, now.Exuded - was.Exuded, now.Handling - was.Handling);
                _lifetimeSeen[i] = now;
            }
        }

        // ------------------------------------------------------------------ the clock and the world

        /// <summary>The slots of the tank: one per genome handed in, whether or not its body landed.</summary>
        public int MemberCount => _focal.Length;

        /// <summary>Physics steps taken. The physics clock is this times <see cref="PhysicsDt"/>; the tank's time is <see cref="ClockSeconds"/>.</summary>
        public long Steps => _sim.Steps;

        public float PhysicsDt => _sim.PhysicsDt;

        public int StepsPerMetabolicStep => _sim.StepsPerMetabolicStep;

        /// <summary>Physics steps times the step: the physics clock, which is world time only at a pace factor of 1 (D239); see <see cref="ClockSeconds"/>.</summary>
        public double PhysicsSeconds => _sim.Steps * (double)_sim.PhysicsDt;

        /// <summary>
        /// World time between economy steps, s (D240): the clock the poses, the economy and the switching schedule's openings share.
        /// <b>At a pace factor of 1 it is <see cref="PhysicsSeconds"/> itself, the same expression</b>, so its bits are the ones that clock always had;
        /// above 1 it is physics steps times the step over the factor, in double. The watchers take their times from it.
        /// </summary>
        public double ClockSeconds => _sim.PaceFactor == 1f ? _sim.Steps * (double)_sim.PhysicsDt : _sim.Steps * (double)_sim.PhysicsDt / _sim.PaceFactor;

        /// <summary>The economy's clock, which moves once a metabolic step.</summary>
        public double EconomySeconds => _world.ElapsedSeconds;

        public double WorldDepthMetres => _world.Config.WorldDepthMetres;

        public double TankRadiusMetres => _world.TankRadiusMetres;

        /// <summary>The config's horizontal patches: above 1 a point's patch is not 0 everywhere, and <see cref="SnowAt"/> reads patch 0.</summary>
        public int HorizontalPatches => (int)_world.Config.HorizontalPatches;

        public double JoulesPerUnit => _world.Config.JoulesPerUnit;

        /// <summary>The charged units the world's mouths have taken from corpses, ever. Cumulative.</summary>
        public double UnitsEaten => _world.UnitsEaten;

        /// <summary>
        /// D236's schedule of the switching leaf tank: every opening so far and each leaf's opening; null in a tank whose patches do not switch
        /// (or that has none). Set by the episode before the first call; the watcher reads it and nothing here writes it.
        /// </summary>
        public LeafSchedule Schedule => _schedule;

        private LeafSchedule _schedule;

        /// <summary>The episode hands the probe its schedule here, once, before the watcher's first call. Internal, so the probe has no setter.</summary>
        internal void AttachSchedule(LeafSchedule schedule) => _schedule = schedule;

        /// <summary>The bodies now lying as remains (contact eating, D220). A count.</summary>
        public int RemainsCount => _world.Remains.Count;

        /// <summary>The corpses a mouth has emptied, ever. Cumulative.</summary>
        public long CorpsesEmptied => _world.CorpsesEaten;

        /// <summary>The world's deaths, ever.</summary>
        public long Deaths => _world.Deaths;

        /// <summary>The state hash of the solver: FNV-1a over every link's place, attitude and motion. A read; the same function the solver's identity tests use.</summary>
        public ulong SolverDigest() => _sim.Dynamics.Digest();

        // ------------------------------------------------------------------ the members

        /// <summary>Whether the slot's genome landed a body in the tank.</summary>
        public bool Landed(int member) => _focal[member] != null;

        /// <summary>Whether the body is alive in the world now.</summary>
        public bool Alive(int member) => _focal[member] != null && _world.Living.Contains(_focal[member]);

        /// <summary>The body's reserve, tissue and gestation joules; NaN where the slot landed nothing.</summary>
        public double Reserve(int member) => _focal[member]?.Energy ?? double.NaN;

        public double Tissue(int member) => _focal[member]?.TissueJoules ?? double.NaN;

        public double Gestation(int member) => _focal[member]?.GestationJoules ?? double.NaN;

        /// <summary>The joules the body's mouths have kept over its life, net of the waste (<c>Organism.MouthJoules</c>); NaN where the slot landed nothing.</summary>
        public double MouthJoules(int member) => _focal[member]?.MouthJoules ?? double.NaN;

        /// <summary>
        /// The cumulative count a member's "eaten" is the change of, as the tank reads it: the joules kept from food that came from leaves under
        /// contact eating, else <see cref="MouthJoules"/>. A number; NaN where the slot landed nothing.
        /// </summary>
        public double EatenJoules(int member)
        {
            Organism o = _focal[member];
            if (o == null) return double.NaN;
            return _world.Config.ContactEating ? o.FoodFromLeavesJoules : o.MouthJoules;
        }

        /// <summary>
        /// The last economy step's ledger of the body: income, the four spends and what it exuded. Zero where the slot landed nothing. A body
        /// with absorptive tissue reads the world's LastLedger; any other body, which the world leaves at zero there, its Lifetime's change.
        /// </summary>
        public EnergyLedger Ledger(int member)
        {
            Organism o = _focal[member];
            if (o == null) return default(EnergyLedger);
            return o.HasAbsorptiveTissue ? o.LastLedger : _lastLedger[member];
        }

        /// <summary>The funds the breeding gate is asked of (the reserve, or the account of a gestating body), and the gate. NaN where nothing landed.</summary>
        public double BreedingFunds(int member)
        {
            Organism o = _focal[member];
            return o == null ? double.NaN : o.Gestates ? o.GestationJoules : o.Energy;
        }

        public double BreedingGate(int member) => _focal[member]?.BreedingGate(_world.Config) ?? double.NaN;

        /// <summary>The edible snow density at the body's place on the economy's last step, J/m3; NaN where nothing landed or the place is not finite.</summary>
        public double SnowAtBody(int member)
        {
            Organism o = _focal[member];
            if (o == null) return double.NaN;
            FieldPoint at = o.Point;
            return at.Position.IsFinite ? _world.Nutrients.EdibleDensityAt(at) : double.NaN;
        }

        /// <summary>The edible snow density at a point, J/m3, in a patch (0 unless the world has several); NaN for a point that is not finite.</summary>
        public double SnowAt(double x, double y, double z, int patch = 0)
        {
            if (!(double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z))) return double.NaN;
            return _world.Nutrients.EdibleDensityAt(new FieldPoint(new Float3((float)x, (float)y, (float)z), patch));
        }

        // ------------------------------------------------------------------ the solver's body

        private bool Body(int member, out Creature body)
        {
            body = null;
            Organism o = _focal[member];
            return o != null && _sim.TryPose(o.Id, out body);
        }

        /// <summary>The links the solver holds for the body now; 0 before the first step and after the body has left the world.</summary>
        public int LinkCount(int member) => Body(member, out Creature c) ? c.Links : 0;

        /// <summary>The parts of the body's adult plan: the most links it can have.</summary>
        public int AdultPartCount(int member) => _focal[member]?.AdultPhenotype.PartCount ?? 0;

        public bool TryLinkPose(int member, int link, out LinkPose pose)
        {
            pose = default(LinkPose);
            if (!Body(member, out Creature c) || link < 0 || link >= c.Links) return false;
            pose = new LinkPose(
                c.Position[3 * link], c.Position[3 * link + 1], c.Position[3 * link + 2],
                c.Rotation[4 * link], c.Rotation[4 * link + 1], c.Rotation[4 * link + 2], c.Rotation[4 * link + 3]);
            return true;
        }

        /// <summary>
        /// The root link's world angular velocity in the solver's units, rad a physics second, as the last step left it: what the episode's <c>turn</c>
        /// reads, which reports it in rad a world second (k times this at a pace factor k, D240). False where the solver holds no body.
        /// </summary>
        public bool TryRootSpin(int member, out double wx, out double wy, out double wz)
        {
            wx = wy = wz = double.NaN;
            if (!Body(member, out Creature c)) return false;
            wx = c.Spin[0];
            wy = c.Spin[1];
            wz = c.Spin[2];
            return true;
        }

        /// <summary>The body's actuated degrees of freedom: the length of its drive and its bend. 0 where the solver holds no body.</summary>
        public int OutputCount(int member) => Body(member, out Creature c) ? c.Brain.TotalDof : 0;

        /// <summary>The brain's power for one degree of freedom as the last step left it, in [-1, 1]. NaN where unavailable.</summary>
        public float Drive(int member, int dof) =>
            Body(member, out Creature c) && dof >= 0 && dof < c.Brain.TotalDof ? c.DriveSignal[dof] : float.NaN;

        /// <summary>The brain's bend for one degree of freedom as the last step left it, in [-1, 1]. NaN where unavailable.</summary>
        public float Bend(int member, int dof) =>
            Body(member, out Creature c) && dof >= 0 && dof < c.Brain.TotalDof ? c.BendSignal[dof] : float.NaN;

        /// <summary>
        /// The brain's units as of the last completed step, in a copy: for D146's one brain the outputs first (two a degree of freedom, power then
        /// bend, so <paramref name="outputUnits"/> of them) and then the hidden units; for the old per-part neurons every neuron, with
        /// <paramref name="outputUnits"/> 0. False where the solver holds no body, or where the checkpoint stream is not the layout this reads.
        /// </summary>
        public bool TryBrainUnits(int member, out float[] units, out bool central, out int outputUnits)
        {
            units = null;
            central = false;
            outputUnits = 0;
            if (!Body(member, out Creature c)) return false;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                c.Brain.WriteState(writer);
                writer.Flush();
                byte[] bytes = stream.ToArray();

                // The tag (4 bytes), the clock (a double), then the length and the floats of the buffer of the last completed step.
                const int at = 4 + 8;
                if (bytes.Length < at + 4 || bytes[0] != (byte)'B' || bytes[1] != (byte)'R' || bytes[2] != (byte)'A' || bytes[3] != (byte)'N') return false;
                int count = BitConverter.ToInt32(bytes, at);
                if (count < 0 || bytes.Length < at + 4 + 4 * count) return false;
                units = new float[count];
                for (int k = 0; k < count; k++) units[k] = BitConverter.ToSingle(bytes, at + 4 + 4 * k);
            }
            central = c.Brain.IsCentral;
            outputUnits = central ? 2 * c.Brain.TotalDof : 0;
            return outputUnits <= units.Length;
        }

        /// <summary>
        /// What the body's senses offer on the channels its brain reads (the brain's own mask): every part for a channel a part reads, every degree
        /// of freedom of a joint for the joint's channels, three axes for the flow, and the whole-body energy once. Empty where the solver holds no body.
        /// </summary>
        public IReadOnlyList<SenseSpec> SenseSpecs(int member)
        {
            var specs = new List<SenseSpec>();
            if (!Body(member, out Creature c)) return specs;
            int mask = c.Brain.SensorMask;
            foreach (SensorChannel channel in Enum.GetValues(typeof(SensorChannel)))
            {
                if (!Brain.MaskReads(mask, channel)) continue;
                switch (channel)
                {
                    case SensorChannel.Energy:
                        specs.Add(new SenseSpec(channel, 0, 0));
                        break;
                    case SensorChannel.JointAngle:
                    case SensorChannel.JointAngularVelocity:
                    case SensorChannel.Stroke:
                    case SensorChannel.Hold:
                    case SensorChannel.Ramp:
                        for (int part = 0; part < c.Links; part++)
                            for (int d = 0; d < c.DofCount[part]; d++) specs.Add(new SenseSpec(channel, part, d));
                        break;
                    case SensorChannel.Flow:
                    case SensorChannel.LeafScentDirection:
                        for (int part = 0; part < c.Links; part++)
                            for (int axis = 0; axis < 3; axis++) specs.Add(new SenseSpec(channel, part, axis));
                        break;
                    case SensorChannel.Depth:
                    case SensorChannel.OrientationUp:
                    case SensorChannel.Chemical:
                    case SensorChannel.Scent:
                    case SensorChannel.Contact:
                    case SensorChannel.Damage:
                    case SensorChannel.BedTouch:
                    case SensorChannel.LeafScent:
                        for (int part = 0; part < c.Links; part++) specs.Add(new SenseSpec(channel, part, 0));
                        break;
                    // Photo and any channel the sampler does not implement read 0 everywhere: nothing to record.
                }
            }
            return specs;
        }

        /// <summary>One sense as the solver's last step sampled it (the cached value; the read does not sample). NaN where unavailable.</summary>
        public float Sense(int member, SenseSpec spec) =>
            Body(member, out Creature c) ? c.Senses.Read(spec.Part, spec.Channel, spec.Index) : float.NaN;

        // ------------------------------------------------------------------ the carrion

        /// <summary>The carrion the episode placed: the corpses the world held when the watcher began.</summary>
        public int PlacedCorpseCount => _placed.Length;

        public CorpseView PlacedCorpse(int index)
        {
            Corpse c = _placed[index];
            return new CorpseView(c.CreatureId, c.Position.X, c.Position.Y, c.Position.Z, c.Joules);
        }

        /// <summary>Every corpse in the world now, the dead bodies' included.</summary>
        public int CorpseCount => _world.Corpses.Count;

        // ------------------------------------------------------------------ the rest of the water

        /// <summary>
        /// D167's leaf smell at a point, as a part there would read it: the strength <c>s / (s + half-scale)</c>, and the field's
        /// gradient in world axes. False in a world with no leaf smell. Reads the field the world last filled.
        /// </summary>
        public bool TryLeafScent(double x, double y, double z, out double strength, out double gx, out double gy, out double gz)
        {
            strength = gx = gy = gz = double.NaN;
            if (_world.LeafScent == null) return false;
            float s = _world.LeafScent.At((float)x, (float)y, (float)z);
            float half = (float)Math.Max(1e-6, _world.Config.LeafScentHalfScaleSquareMetres);
            strength = s > 0f ? s / (s + half) : 0f;
            var g = _world.LeafScent.GradientAt((float)x, (float)y, (float)z);
            gx = g.X; gy = g.Y; gz = g.Z;
            return true;
        }

        /// <summary>Every body alive in the world now, the members and the leaf tank's leaves together.</summary>
        public int LivingCount => _world.Living.Count;

        /// <summary>The snow the water holds, J.</summary>
        public double SnowJoules => _world.Nutrients.TotalJoules;

        /// <summary>The spent matter the water holds, units.</summary>
        public double SpentUnits => _world.Matter.TotalJoules;

        /// <summary>Every living body that is not a member (the leaf tank's leaves), in the world's order: a copy of each.</summary>
        public IReadOnlyList<BodyView> Others()
        {
            var members = new HashSet<long>();
            foreach (Organism o in _focal) if (o != null) members.Add(o.Id);
            var others = new List<BodyView>();
            foreach (Organism o in _world.Living)
            {
                if (members.Contains(o.Id)) continue;
                others.Add(new BodyView(o.Id, o.X, o.HeightY, o.Z, o.HasAbsorptiveTissue, o.HasPhotosyntheticTissue, o.Energy, o.TissueJoules,
                    o.AdultTissueJoules));
            }
            return others;
        }
    }

    /// <summary>One living body that is not a member, as it now stands: its id, its place as the world last knew it, its tissue flags and its joules. A copy.</summary>
    public readonly struct BodyView
    {
        public readonly long Id;
        public readonly double X, Y, Z, Reserve, Tissue, AdultTissue;
        public readonly bool Absorptive, Photosynthetic;

        public BodyView(long id, double x, double y, double z, bool absorptive, bool photosynthetic, double reserve, double tissue, double adultTissue)
        {
            Id = id; X = x; Y = y; Z = z; Absorptive = absorptive; Photosynthetic = photosynthetic; Reserve = reserve; Tissue = tissue; AdultTissue = adultTissue;
        }
    }
}
