using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// Milestone 1's visible payoff: grow a random genome into a creature and drive it, so
    /// there is something to watch flop.
    /// </summary>
    /// <remarks>
    /// This is a scaffold, not the evaluator. DESIGN.md §6.1 keeps evaluation headless and
    /// separate from presentation, and there is no fitness, no fluid and no brain here — the
    /// signal is a test sine (see <see cref="EffectorDriver.DriveTestSine"/>). Its job is to
    /// make development and the articulation mapping visible, because a limb in the wrong
    /// place is obvious on screen and invisible in a unit test.
    /// </remarks>
    public sealed class CreatureSpawner : MonoBehaviour
    {
        [Header("Genome")]
        [Tooltip("Seed for the genome. The same seed always grows the same creature (DESIGN.md §7).")]
        public ulong Seed = 1;

        [Tooltip("Reject genomes developing into fewer parts than this, then take what we get.")]
        public int MinParts = 3;

        [Header("Development limits (DESIGN.md §4.2)")]
        public int MaxParts = 16;
        public int MaxDepth = 8;
        public float MinPartVolume = 1e-4f;

        [Header("Test actuation — placeholder until the brain graph (Milestone 3)")]
        public bool Actuate = true;
        public float TestSineHz = 0.8f;
        public float TorqueScale = 300f;

        [Header("Respawn")]
        [Tooltip("Seconds before the creature is torn down and the next seed grown. Zero disables.")]
        public float CycleSeconds = 8f;

        [Header("Water (DESIGN.md §5.2)")]
        [Tooltip("Fluid density, kg/m3. Water is 1000.")]
        public float Density = 1000f;

        [Tooltip("Quadratic drag coefficient. [C18 §2.2, p.5] uses 1.5.")]
        public float DragCoefficient = 1.5f;

        [Tooltip("Added mass, as a multiple of displaced water. 0 = drag only.")]
        public float AddedMassCoefficient = 1f;

        [Tooltip("Camera to point at whatever is currently spawned.")]
        public FollowCamera Camera;

        private CreatureInstance _creature;
        private EffectorDriver _driver;
        private FluidEnvironment _fluid;
        private float[] _scratch;
        private float _age;
        private Vector3 _startCentre;

        private void Start()
        {
            FluidEnvironment.ConfigureScene();
            Spawn();
        }

        private void Spawn()
        {
            _creature?.Destroy();

            var limits = new DevelopmentLimits
            {
                MaxParts = MaxParts,
                MaxDepth = MaxDepth,
                MinPartVolume = MinPartVolume,
            };

            var rng = new Rng(Seed);
            Genome genome = GenomeFactory.RandomViable(rng, RandomGenomeOptions.Default, limits, MinParts);
            Phenotype phenotype = Developer.Develop(genome, limits);

            _creature = PhenotypeBuilder.Build(phenotype, transform.position, transform);
            _creature.Root.name = $"Creature (seed {Seed})";

            _fluid = new FluidEnvironment(new FluidConfig
            {
                Density = Density,
                DragCoefficient = DragCoefficient,
                AddedMassCoefficient = AddedMassCoefficient,
            });
            _fluid.ApplyAddedMass(_creature);

            if (Camera != null) Camera.Target = _creature.Root.transform;

            _driver = new EffectorDriver(_creature) { TorqueScale = TorqueScale };
            _scratch = new float[Mathf.Max(1, _creature.TotalDof)];
            _age = 0f;
            _startCentre = FluidEnvironment.CentreOfMass(_creature);

            Debug.Log(
                $"[Evosim] seed {Seed}: {phenotype.PartCount} parts, depth {phenotype.MaxDepthReached}, " +
                $"{phenotype.TotalDof} DOF, {phenotype.TotalVolume:0.###} m³" +
                (phenotype.WasTruncated ? " (truncated)" : ""));
        }

        private void FixedUpdate()
        {
            if (_creature == null) return;

            _age += Time.fixedDeltaTime;

            if (Actuate && _creature.TotalDof > 0)
            {
                _driver.DriveTestSine(_age, TestSineHz, _scratch);
            }

            _fluid.Apply(_creature);

            if (CycleSeconds > 0f && _age >= CycleSeconds)
            {
                float travelled = Vector3.Distance(
                    FluidEnvironment.CentreOfMass(_creature), _startCentre);
                Debug.Log($"[Evosim] seed {Seed}: travelled {travelled:0.##} m in {_age:0.#} s");

                Seed++;
                Spawn();
            }
        }

        private void OnDestroy() => _creature?.Destroy();
    }
}
