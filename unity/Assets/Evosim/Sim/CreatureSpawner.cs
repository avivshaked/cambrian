using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// Milestone 1's visible payoff: grow a random genome into a creature and drive it, so
    /// there is something to watch flop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a scaffold, not the evaluator. DESIGN.md §6.1 keeps evaluation headless and
    /// separate from presentation, and there is no fitness and no brain here — the signal is a
    /// test sine (see <see cref="EffectorDriver.DriveTestSine"/>). Its job is to make
    /// development and the articulation mapping visible, because a limb in the wrong place is
    /// obvious on screen and invisible in a unit test.
    /// </para>
    /// <para>
    /// <b>It is driveable from the keyboard and reports on screen, and that is not polish.</b>
    /// The first version could only be steered by typing into Unity's Inspector, and the seed
    /// field there did not do what it appeared to: changing it took effect one creature later,
    /// and setting the cycle time to zero disabled the only code path that ever spawned
    /// anything, so nothing could take effect at all. Nobody could have reached a chosen
    /// creature. Meanwhile which creature was on screen, how many parts it had and whether its
    /// joints were moving were all knowable only by reading log lines in a separate panel.
    ///
    /// The result was a session where the questions worth asking — is <i>this</i> the jammed
    /// one, are those two parts really intersecting — could not be answered by the person
    /// looking at it. A simulator you cannot aim is not observable, and an unobservable
    /// simulator produces opinions rather than findings.
    /// </para>
    /// </remarks>
    public sealed class CreatureSpawner : MonoBehaviour
    {
        /// <summary>Which population to draw from — DESIGN.md §5A.0b.</summary>
        public enum PopulationKind
        {
            /// <summary>One earning cell, half the time with a tail. What a world starts with.</summary>
            Founder = 0,

            /// <summary>Three to sixteen parts with branching and recursion. The Milestone 1 harness population.</summary>
            Elaborate = 1,
        }

        [Header("Genome")]
        [Tooltip("Founder is what generation zero actually looks like (§5A.0b). Elaborate is the " +
                 "pre-§5A population, kept because the Milestone 1 harnesses need joints to actuate.")]
        public PopulationKind Population = PopulationKind.Founder;

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

        [Tooltip("Multiplier on each link's evolved Power (§5A.1). A viewing knob — leave at 1 " +
                 "for anything you intend to believe.")]
        public float PowerScale = 1f;

        [Header("Respawn")]
        [Tooltip("Seconds before the creature is torn down and the next seed grown. Zero holds " +
                 "the current creature; N and P still work.")]
        public float CycleSeconds = 8f;

        [Header("Water (DESIGN.md §5.2)")]
        [Tooltip("Fluid density, kg/m3. Water is 1000.")]
        public float Density = 1000f;

        [Tooltip("Quadratic drag coefficient. [C18 §2.2, p.5] uses 1.5.")]
        public float DragCoefficient = 1.5f;

        [Tooltip("Added mass, as a multiple of displaced water. 0 = drag only.")]
        public float AddedMassCoefficient = 1f;

        [Header("Display")]
        [Tooltip("On-screen readout. Turn off for a clean picture.")]
        public bool ShowOverlay = true;

        [Tooltip("False-colour each part by what it is made of (§5A.1). An instrument, not the " +
                 "creature's appearance — §5A.5 reserves real colour as an evolvable trait.")]
        public bool ColourByCellType = true;

        [Tooltip("Camera to point at whatever is currently spawned.")]
        public FollowCamera Camera;

        private CreatureInstance _creature;
        private EffectorDriver _driver;
        private FluidEnvironment _fluid;
        private float[] _scratch;
        private float _age;
        private Vector3 _startCentre;

        /// <summary>The seed the creature on screen was actually grown from.</summary>
        /// <remarks>
        /// Kept separate from <see cref="Seed"/> so that editing the field respawns immediately
        /// rather than at the next cycle. The two differing is the signal to rebuild; without
        /// it, "set the seed and look at that creature" is not an operation this component
        /// supports, which was true for longer than it should have been.
        /// </remarks>
        private ulong _spawnedSeed;
        private PopulationKind _spawnedPopulation;

        private string _summary = "";
        private string _composition = "";
        private float _travelled;
        private float _jointRate;

        private void Start()
        {
            FluidEnvironment.ConfigureScene();
            Spawn();
        }

        private void Update()
        {
            ReadKeys();

            // Covers both the Inspector field being edited and the keys above, so there is one
            // path from "the seed changed" to "a new creature exists".
            if (Seed != _spawnedSeed || Population != _spawnedPopulation) Spawn();
        }

        private void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightArrow)) Seed++;

            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (Seed > 1) Seed--;
            }

            if (Input.GetKeyDown(KeyCode.R)) Spawn();
            if (Input.GetKeyDown(KeyCode.A)) Actuate = !Actuate;
            if (Input.GetKeyDown(KeyCode.H)) ShowOverlay = !ShowOverlay;

            if (Input.GetKeyDown(KeyCode.C))
            {
                ColourByCellType = !ColourByCellType;
                PhenotypeBuilder.ApplyCellTypeColours(_creature, ColourByCellType);
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                Population = Population == PopulationKind.Founder
                    ? PopulationKind.Elaborate
                    : PopulationKind.Founder;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Holding at zero is the useful state for looking at one creature, so the
                // toggle remembers nothing and simply restores the default.
                CycleSeconds = CycleSeconds > 0f ? 0f : 8f;
            }
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

            // No viability filter on founders, deliberately. RandomViable rejects genomes with no
            // degrees of freedom, which every one-part founder has — and under §5A a blob that
            // pays its bills sitting in the light is a plant, not a reject.
            Genome genome = Population == PopulationKind.Founder
                ? GenomeFactory.Founder(rng)
                : GenomeFactory.RandomViable(rng, RandomGenomeOptions.Default, limits, MinParts);

            Phenotype phenotype = Developer.Develop(genome, limits);

            _creature = PhenotypeBuilder.Build(phenotype, transform.position, transform);
            _creature.Root.name = $"Creature (seed {Seed})";
            _spawnedSeed = Seed;
            _spawnedPopulation = Population;

            PhenotypeBuilder.ApplyCellTypeColours(_creature, ColourByCellType);

            _fluid = new FluidEnvironment(new FluidConfig
            {
                Density = Density,
                DragCoefficient = DragCoefficient,
                AddedMassCoefficient = AddedMassCoefficient,
            });
            _fluid.ApplyAddedMass(_creature);

            if (Camera != null) Camera.SnapTo(_creature.Root.transform, BodyRadius(phenotype));

            _driver = new EffectorDriver(_creature, Time.fixedDeltaTime) { PowerScale = PowerScale };
            _scratch = new float[Mathf.Max(1, _creature.TotalDof)];
            _age = 0f;
            _travelled = 0f;
            _jointRate = 0f;
            _startCentre = FluidEnvironment.CentreOfMass(_creature);
            _summary = Describe(phenotype);
            _composition = DescribeCells(phenotype);

            Debug.Log(
                $"[Evosim] seed {Seed}: {phenotype.PartCount} parts, depth {phenotype.MaxDepthReached}, " +
                $"{phenotype.TotalDof} DOF, {phenotype.TotalVolume:0.###} m³" +
                (phenotype.WasTruncated ? " (truncated)" : ""));
        }

        /// <summary>How far the body reaches from its own centre, in metres.</summary>
        /// <remarks>
        /// The camera frames on this rather than on a constant offset. Part half-extents span
        /// 0.1–0.4 m and part counts 2–16, so a distance that suits a large creature loses a
        /// small one entirely — and under §5A the early world is nothing but small creatures.
        /// </remarks>
        private static float BodyRadius(Phenotype phenotype)
        {
            float worst = 0.3f;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                Float3 h = part.HalfExtents;
                float reach = Mathf.Sqrt(Float3.Dot(part.Position, part.Position)) +
                              Mathf.Max(Mathf.Abs(h.X), Mathf.Max(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));

                worst = Mathf.Max(worst, reach);
            }

            return worst;
        }

        private static string Describe(Phenotype phenotype)
        {
            int box = 0, sphere = 0, capsule = 0;

            foreach (PhenotypePart part in phenotype.Parts)
            {
                if (part.ShapeId == ShapeIds.Sphere) sphere++;
                else if (part.ShapeId == ShapeIds.Capsule) capsule++;
                else box++;
            }

            var sb = new StringBuilder();
            sb.Append(phenotype.PartCount).Append(" parts  ");
            sb.Append(box).Append(" box / ").Append(sphere).Append(" sphere / ")
              .Append(capsule).Append(" capsule");

            return sb.ToString();
        }

        /// <summary>What the creature is made of, as coloured counts matching the parts on screen.</summary>
        /// <remarks>
        /// Reads the registry rather than a hardcoded list, so a sixth cell type appears here
        /// without this being touched — the same property that makes the registry worth having.
        /// </remarks>
        private static string DescribeCells(Phenotype phenotype)
        {
            var counts = new Dictionary<string, int>();
            foreach (PhenotypePart part in phenotype.Parts)
            {
                counts.TryGetValue(part.CellTypeId, out int n);
                counts[part.CellTypeId] = n + 1;
            }

            var sb = new StringBuilder();
            foreach (string id in CellTypeRegistry.Standard.Ids())
            {
                if (!counts.TryGetValue(id, out int n) || n == 0) continue;

                Float3 c = CellTypeRegistry.Standard.Resolve(id).InspectionColour;
                if (sb.Length > 0) sb.Append("  ");

                sb.Append("<color=#")
                  .Append(Mathf.RoundToInt(c.X * 255f).ToString("x2"))
                  .Append(Mathf.RoundToInt(c.Y * 255f).ToString("x2"))
                  .Append(Mathf.RoundToInt(c.Z * 255f).ToString("x2"))
                  .Append('>').Append(n).Append(' ').Append(id).Append("</color>");
            }

            return sb.ToString();
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

            _travelled = Vector3.Distance(FluidEnvironment.CentreOfMass(_creature), _startCentre);
            _jointRate = MeanJointRate();

            if (CycleSeconds > 0f && _age >= CycleSeconds)
            {
                Debug.Log($"[Evosim] seed {Seed}: travelled {_travelled:0.##} m in {_age:0.#} s");
                Seed++;
                Spawn();
            }
        }

        /// <summary>Mean absolute joint speed across every actuated DOF, rad/s.</summary>
        /// <remarks>
        /// The closest thing to a live jamming indicator. It is <i>not</i> a jamming measure —
        /// that needs the same creature run twice, with self-collision off and on, which
        /// <c>JamSurvey</c> does and a live view cannot. A creature reading near zero here is
        /// either jammed, weakly driven, or barely jointed, and the overlay says only what it
        /// measured rather than which of those it is.
        /// </remarks>
        private float MeanJointRate()
        {
            float sum = 0f;
            int n = 0;

            for (int b = 0; b < _creature.Bodies.Length; b++)
            {
                if (_creature.DofOffset[b] < 0) continue;

                ArticulationReducedSpace v = _creature.Bodies[b].jointVelocity;
                for (int d = 0; d < v.dofCount; d++)
                {
                    sum += Mathf.Abs(v[d]);
                    n++;
                }
            }

            return n > 0 ? sum / n : 0f;
        }

        private GUIStyle _panel;
        private GUIStyle _text;

        private void OnGUI()
        {
            if (!ShowOverlay || _creature == null) return;

            if (_panel == null)
            {
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                background.Apply();

                _panel = new GUIStyle { normal = { background = background } };
                _text = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white },
                    richText = true,
                };
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 380f, 250f), _panel);
            GUILayout.Space(8f);

            GUILayout.Label(
                $"<b>seed {Seed}</b>   " +
                (Population == PopulationKind.Founder ? "founder — generation zero" : "elaborate"),
                _text);

            GUILayout.Label(_summary, _text);
            GUILayout.Label(_composition, _text);
            GUILayout.Space(6f);
            GUILayout.Label($"{_creature.TotalDof} DOF   {_age:0.0} s", _text);
            GUILayout.Label($"travelled  <b>{_travelled:0.00} m</b>", _text);
            GUILayout.Label($"joint rate  {_jointRate:0.00} rad/s{StillnessNote()}", _text);
            GUILayout.Space(6f);
            GUILayout.Label(
                Actuate ? "driven by test sine — no brain yet" : "<b>not actuated</b>", _text);

            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(12f, Screen.height - 92f, 620f, 80f), _panel);
            GUILayout.Space(6f);
            GUILayout.Label(
                "<b>N</b>/<b>P</b> next & previous creature    <b>R</b> regrow    " +
                "<b>Space</b> hold/cycle", _text);
            GUILayout.Label(
                "<b>F</b> founder/elaborate    <b>C</b> cell-type colour    <b>A</b> actuation    " +
                "<b>H</b> hide this", _text);
            GUILayout.Label("drag to orbit, wheel to zoom", _text);
            GUILayout.EndArea();
        }

        /// <remarks>
        /// Deliberately describes rather than diagnoses. "Barely moving" is an observation; the
        /// reason could be jamming, a weak drive, or a body with almost no joints, and the
        /// overlay cannot tell them apart. Labelling it "jammed" would put a conclusion on
        /// screen that the measurement does not support — and under §5A stillness is not a
        /// failure anyway: a creature that cannot swim is a creature living differently, not a
        /// broken one.
        /// </remarks>
        private string StillnessNote()
        {
            if (_creature.TotalDof == 0) return "   (no joints)";
            if (_age < 1f) return "";

            return _jointRate < 0.05f ? "   <b>— barely moving</b>" : "";
        }

        private void OnDestroy() => _creature?.Destroy();
    }
}
