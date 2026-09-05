using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// Mode A: one creature, alone in the reference water, under its own evolved brain —
    /// D075 item 3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this had to be written rather than reused.</b> The brain evaluator, the sensors and
    /// the effector driver are wired together in exactly one place — <c>Ecosystem.Build</c> — and
    /// that place also builds a whole world's economy around them. <c>CreatureSpawner</c>, the
    /// only other thing that drives a body, drives it with a fixed test sine and no brain at all
    /// (its own doc comment: "there is no fitness and no brain here"). So the fifty lines below
    /// are a deliberate duplicate of <c>Ecosystem.Build</c>'s wiring, not a refactor of it: the
    /// perception build is in that file and replay identity is measured there, and a viewer is
    /// not a good enough reason to move it.
    /// </para>
    /// <para>
    /// <b>No economy.</b> Nothing here feeds, breeds, bills or kills. Energy is held constant
    /// unless <see cref="Starving"/> is set, in which case the reserve runs down at the
    /// creature's own standing rate so a viewer can watch what hunger does to a gait. That makes
    /// the creature's behaviour watchable indefinitely, which is the whole point of the mode —
    /// and it means nothing seen here is evidence about whether the animal could survive.
    /// </para>
    /// <para>
    /// <b>The water is real; the food in it is not the run's.</b> The
    /// <see cref="FluidEnvironment"/> is built from the run's own config, so the drag, the added
    /// mass and the current field are the ones the creature evolved in. The nutrient field is
    /// not: it comes from a <see cref="World"/> constructed and never stepped, which means it is
    /// the world's <i>initial</i> field, and a run's detritus at some later moment is not
    /// recoverable from anything a run stores — <c>stats.jsonl</c> holds aggregates and
    /// snapshots hold genomes. In a reference world that initial field is empty, so a nose here
    /// reads zero unless <paramref name="smellDensity"/> puts something in front of it by hand.
    /// Said plainly rather than papered over: this is the one place Mode A shows a creature
    /// water it never swam in.
    /// </para>
    /// </remarks>
    public sealed class SoloCreature : IDisposable
    {
        /// <summary>Standing in for an account, since there is no economy here.</summary>
        private sealed class HeldReserve : IReserveSource
        {
            public float Seconds = 1200f;
            public float SecondsOfReserve => Seconds;
        }

        public Genome Genome { get; private set; }
        public Phenotype Phenotype { get; private set; }
        public CreatureInstance Instance { get; private set; }
        public Brain Brain { get; private set; }
        public CreatureSensors Sensors { get; private set; }
        public EffectorDriver Driver { get; private set; }
        public FluidEnvironment Fluid { get; private set; }

        /// <summary>The id the genome's snapshot row carried, or -1.</summary>
        public long SourceId { get; private set; }

        /// <summary>Where the genome came from, for the HUD.</summary>
        public string Source { get; private set; }

        /// <summary>Drive the body with the test sine instead of its brain — the null to compare against.</summary>
        public bool UseTestSine;

        public float TestSineHz = 0.8f;

        /// <summary>Let the reserve run down at the creature's standing rate.</summary>
        public bool Starving;

        public double ElapsedSeconds { get; private set; }
        public Vector3 StartCentre { get; private set; }

        private readonly HeldReserve _reserve = new HeldReserve();
        private float[] _drive;
        private RunConfig _config;
        private World _water;
        private SimulationMode _previousMode;
        private Vector3 _previousGravity;
        private bool _sceneConfigured;

        /// <summary>Reserve seconds a fed creature holds. Constant unless <see cref="Starving"/>.</summary>
        public float ReserveSeconds => _reserve.Seconds;

        /// <summary>The world whose water this creature is swimming in.</summary>
        public RunConfig Config => _config;

        /// <summary>Detritus laid into the field by hand, J/m³, or 0 for the world's own.</summary>
        public float SmellDensity { get; private set; }

        /// <summary>
        /// Grows a genome and puts it in the water.
        /// </summary>
        /// <param name="genome">Already read; see <see cref="ReadGenome"/>.</param>
        /// <param name="config">The world whose water, shapes and development limits to use.</param>
        /// <param name="seed">Only reaches the nutrient field the nose smells.</param>
        /// <param name="depth">Metres below the surface, positive down.</param>
        /// <param name="smellDensity">
        /// Detritus to lay into every layer, J/m³, so <see cref="SensorChannel.Chemical"/> has
        /// something to read. 0 leaves the world's initial field alone. A number a viewer chose
        /// is not the run's water, and the overlay says so whenever it is not 0.
        /// </param>
        public static SoloCreature Build(
            Genome genome, RunConfig config, ulong seed, float depth,
            long sourceId, string source, float smellDensity = 0f)
        {
            if (genome == null) throw new ArgumentNullException(nameof(genome));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var solo = new SoloCreature
            {
                Genome = genome,
                _config = config,
                SourceId = sourceId,
                Source = source,
            };

            IReadOnlyList<string> issues = genome.Validate();
            if (issues.Count > 0)
            {
                throw new InvalidOperationException("The genome is not valid: " + issues[0]);
            }

            solo._previousMode = Physics.simulationMode;
            solo._previousGravity = Physics.gravity;
            Physics.simulationMode = SimulationMode.Script;
            FluidEnvironment.ConfigureScene(selfCollision: true);
            solo._sceneConfigured = true;

            // Developed exactly as World develops one: same limits, same shape registry. A
            // different registry would give a body whose colliders and drag panels disagreed with
            // the one the run measured.
            solo.Phenotype = Developer.Develop(genome, config.Development, null, config.Shapes);

            if (solo.Phenotype.PartCount == 0)
            {
                throw new InvalidOperationException(
                    "The genome develops into no parts at all — every part was pruned. It is a " +
                    "real genome and there is nothing to show.");
            }

            // Constructed and never stepped: this is where the nose's field comes from, and
            // nothing else about the world is wanted.
            solo._water = new World(config, seed);
            solo.SmellDensity = smellDensity;

            if (smellDensity > 0f)
            {
                NutrientField field = solo._water.Nutrients;
                float volume = field.LayerVolume;

                for (int layer = 0; layer < field.LayerCount; layer++)
                {
                    float height = -(layer + 0.5f) * field.LayerMetres;
                    for (int patch = 0; patch < field.PatchCount; patch++)
                    {
                        field.Deposit(height, smellDensity * volume, patch);
                    }
                }
            }

            solo.Fluid = new FluidEnvironment(config.Fluid, config.Shapes, config.Current)
            {
                PatchCount = Mathf.Max(1, (int)config.HorizontalPatches),
            };

            solo.Instance = PhenotypeBuilder.Build(
                solo.Phenotype, new Vector3(0f, -Mathf.Abs(depth), 0f), null, config.Shapes);
            solo.Instance.Root.name = "Creature";

            // Patch 0's water, and only patch 0's. CreatureInstance.Patch is `internal set` to
            // Evosim.Sim — Ecosystem owns it and refreshes it every metabolic step as dispersal
            // and advection move a creature — and widening that seam so a viewer could pick a
            // patch would be changing the simulation for the theatre's convenience. In a run with
            // one patch this is the whole world; in a patchy one it is the current and the field
            // of patch 0, which the overlay says.

            solo.Fluid.ApplyAddedMass(solo.Instance);

            solo.Brain = Brain.For(solo.Phenotype, genome.GlobalBrain);

            // Every channel, not the brain's own mask: a viewer wants to see what the creature
            // could read as well as what it does read, and the HUD lists all seven. The mask is a
            // cost optimisation inside a world of thousands, and there is one creature here.
            solo.Sensors = new CreatureSensors(
                solo.Instance, config.WorldDepthMetres, solo._water.Nutrients, solo._reserve,
                Brain.AllSensorChannels, config);

            solo.Driver = new EffectorDriver(solo.Instance, Ecosystem.FixedDt);
            solo._drive = new float[Mathf.Max(1, solo.Brain.TotalDof)];

            if (solo.Brain.TotalDof != solo.Instance.TotalDof)
            {
                throw new InvalidOperationException(
                    $"The brain produces {solo.Brain.TotalDof} drive values and the articulation " +
                    $"has {solo.Instance.TotalDof} degrees of freedom.");
            }

            solo.StartCentre = FluidEnvironment.CentreOfMass(solo.Instance);
            return solo;
        }

        /// <summary>One physics step, in the order <c>Ecosystem.Step</c> takes them.</summary>
        /// <remarks>
        /// The order is not arbitrary: sensors are sampled before the brain so every neuron
        /// perceives the same instant (§4.3's synchronous update), and the fluid's own clock is
        /// advanced from the physics step rather than the metabolic one so a current is a flow
        /// rather than a staircase.
        /// </remarks>
        public void Step()
        {
            if (Instance == null) return;

            float dt = Ecosystem.FixedDt;

            Sensors.Sample();

            if (UseTestSine && Instance.TotalDof > 0)
            {
                Driver.DriveTestSine((float)ElapsedSeconds, TestSineHz, _drive);
            }
            else
            {
                Brain.Step(dt, _drive, Sensors);
                Driver.Drive(_drive);
            }

            Fluid.ElapsedSeconds = ElapsedSeconds;
            Fluid.Apply(Instance, dt);
            Physics.Simulate(dt);
            Fluid.Settle(Instance);
            Driver.Settle();

            ElapsedSeconds += dt;

            if (Starving)
            {
                _reserve.Seconds = Mathf.Max(0f, _reserve.Seconds - dt);
            }
        }

        /// <summary>Root speed, m/s — the same quantity the movement instrument accumulates.</summary>
        public float Speed =>
            Instance == null || Instance.Bodies.Length == 0
                ? 0f
                : Instance.Bodies[0].linearVelocity.magnitude;

        /// <summary>Depth of the root, metres below the surface.</summary>
        public float Depth =>
            Instance == null || Instance.Bodies.Length == 0
                ? 0f
                : -Instance.Bodies[0].transform.position.y;

        /// <summary>How far the centre of mass has moved since it was built.</summary>
        public float Travelled =>
            Instance == null ? 0f : Vector3.Distance(FluidEnvironment.CentreOfMass(Instance), StartCentre);

        /// <summary>Mean absolute joint speed across every actuated DOF, rad/s.</summary>
        public float MeanJointRate()
        {
            if (Instance == null) return 0f;

            float sum = 0f;
            int n = 0;

            for (int b = 0; b < Instance.Bodies.Length; b++)
            {
                if (Instance.DofOffset[b] < 0) continue;

                ArticulationReducedSpace v = Instance.Bodies[b].jointVelocity;
                for (int d = 0; d < v.dofCount; d++)
                {
                    sum += Mathf.Abs(v[d]);
                    n++;
                }
            }

            return n > 0 ? sum / n : 0f;
        }

        /// <summary>
        /// What every implemented channel reads at the root part right now — read through
        /// <see cref="ISensorField.Read"/>, so a widened channel pool needs no change here.
        /// </summary>
        public void ReadSensors(List<string> into)
        {
            into.Clear();
            if (Sensors == null) return;

            ISensorField field = Sensors;

            foreach (SensorChannel channel in SensorChannels.Implemented)
            {
                int indices = channel.IndexCount();
                string value;

                if (indices == 1)
                {
                    value = field.Read(0, channel, 0).ToString("0.000");
                }
                else
                {
                    var parts = new string[indices];
                    for (int i = 0; i < indices; i++)
                    {
                        parts[i] = field.Read(0, channel, i).ToString("0.00");
                    }

                    value = string.Join("/", parts);
                }

                into.Add(channel + " " + value);
            }
        }

        /// <summary>
        /// A genome from a snapshot row, a <c>.jsonl</c> line, or a file holding one genome.
        /// </summary>
        /// <param name="path">A <c>snapshots/*.jsonl</c> file, or any file with a genome in it.</param>
        /// <param name="row">Row index, 0-based, when no id is given.</param>
        /// <param name="id">The creature to find, or -1 to take the row.</param>
        /// <param name="foundId">The id on the row that was taken, or -1.</param>
        /// <param name="description">Where it came from, for the HUD.</param>
        public static Genome ReadGenome(
            string path, int row, long id, out long foundId, out string description)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"No genome file at '{path}'.", path);
            }

            // ReadRows, not ReadAllLines: a snapshot from a live run has a writer on it.
            string[] rows = JsonlWriter.ReadRows(path);

            if (rows.Length == 0)
            {
                // A single-genome file written indented has no line structure at all.
                string whole = File.ReadAllText(path);
                foundId = GenomeJson.ReadId(whole);
                description = Path.GetFileName(path);
                return GenomeJson.Read(whole);
            }

            int index = -1;

            if (id >= 0)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    if (GenomeJson.ReadId(rows[i]) != id) continue;
                    index = i;
                    break;
                }

                if (index < 0)
                {
                    throw new InvalidOperationException(
                        $"No creature {id} in {Path.GetFileName(path)} ({rows.Length} rows). " +
                        "Snapshots are the living population at one moment, so a creature that " +
                        "died before it or was born after it is not in this one.");
                }
            }
            else
            {
                if (row < 0 || row >= rows.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(row), row, $"{Path.GetFileName(path)} has {rows.Length} rows.");
                }

                index = row;
            }

            foundId = GenomeJson.ReadId(rows[index]);
            description = Path.GetFileName(path) + " row " + index +
                          (foundId >= 0 ? " (creature " + foundId + ")" : "");

            return GenomeJson.Read(rows[index]);
        }

        /// <summary>How far the body reaches from its own centre, in metres — for framing.</summary>
        public float BodyRadius()
        {
            float worst = 0.3f;
            if (Phenotype == null) return worst;

            foreach (PhenotypePart part in Phenotype.Parts)
            {
                Float3 h = part.HalfExtents;
                float reach = Mathf.Sqrt(Float3.Dot(part.Position, part.Position)) +
                              Mathf.Max(Mathf.Abs(h.X), Mathf.Max(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));

                worst = Mathf.Max(worst, reach);
            }

            return worst;
        }

        public void Dispose()
        {
            Instance?.Destroy();
            Instance = null;
            _water = null;

            if (!_sceneConfigured) return;

            Physics.simulationMode = _previousMode;
            Physics.gravity = _previousGravity;
            _sceneConfigured = false;
        }
    }
}
