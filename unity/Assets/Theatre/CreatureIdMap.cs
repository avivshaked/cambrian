using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// Which creature is which body on screen — the join selection needs and the world does not
    /// have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing in the simulation carries this.</b> <c>Ecosystem</c> holds a private
    /// id-to-body dictionary and exposes none of it; a built body's root <c>GameObject</c> is
    /// called "Creature" and knows nothing about the organism that owns it. The theatre must not
    /// reach into <c>Ecosystem</c> — replay identity is measured in that file — so the map is
    /// inferred from outside instead, and an inference is exactly the thing that has to be
    /// stated plainly and checked.
    /// </para>
    /// <para>
    /// <b>How.</b> <c>Ecosystem.Reconcile</c> runs at the top of a step and builds a body for
    /// each living creature that has none, walking <c>World.Living</c> in order; each new root is
    /// appended to the scene as it is built. So the ids alive but unmapped, sampled
    /// <i>before</i> a step, are exactly the bodies that step will build, in that order — and the
    /// roots that appeared, sampled <i>after</i> it, are the same sequence. Pairing by position
    /// is then exact.
    /// </para>
    /// <para>
    /// <b>And how it is checked.</b> A root transform is never moved after it is built (physics
    /// moves the articulation root, which is that root's child), so whatever was true of it at
    /// build time is still true when the pairing is made. Three things are:
    /// </para>
    /// <list type="bullet">
    /// <item><b>The part count.</b> <c>PhenotypeBuilder</c> gives every part of the phenotype one
    /// GameObject with one <c>ArticulationBody</c> on it and puts none on the root, so the bodies
    /// under a root are exactly the creature's parts. This holds in both worlds and is the check
    /// that actually discriminates: part counts run from one to sixteen.</item>
    /// <item><b>The height, in the tiled world.</b> A tiled body is built at
    /// <c>(tileX, creature.HeightY, tileZ)</c>, so the root's y is the creature's height exactly.
    /// </item>
    /// <item><b>The patch, in the shared box.</b> A shared body is built at the spot the placer
    /// reserved for it, which is not the creature's height: <c>World.Conceive</c> admits a child
    /// at its parent's centre of mass and <c>SharedVolume</c> reserves a spot at the parent's
    /// root, and the two differ by however far a parent's root sits from its own centre. What is
    /// exact there is that the reserved x is the x the patch was read from, so the patch the
    /// creature was admitted with is the patch its root stands in; and that the placer may only
    /// ever raise the height it was offered, so a root can never stand above its creature's
    /// recorded height.</item>
    /// </list>
    /// <para>
    /// The first draft of this file checked the height in both worlds, which is a tiled-world
    /// fact: in a shared box it fails at the first birth from a parent whose root hangs below its
    /// centre of mass, and selection would have gone dark for the rest of the session with a
    /// message about a height nobody had done anything wrong to (found by reading, 2026-09-10).
    /// A failure does not guess: the map goes unreliable and the HUD says selection is
    /// unavailable, because a viewer confidently naming the wrong creature is worse than one that
    /// admits it cannot.
    /// </para>
    /// <para>
    /// <b>Cost.</b> One pass over the scene's roots per birth-or-death batch, and nothing at all
    /// on steps where the population did not change — the same revision number
    /// <c>Reconcile</c> itself early-outs on.
    /// </para>
    /// </remarks>
    public sealed class CreatureIdMap
    {
        private readonly Dictionary<long, Transform> _rootById = new Dictionary<long, Transform>();
        private readonly Dictionary<Transform, long> _idByRoot = new Dictionary<Transform, long>();

        /// <summary>
        /// Roots whose creature has died, held until Unity actually destroys them.
        /// </summary>
        /// <remarks>
        /// A belt rather than a brace since 2026-09-10: <c>PhenotypeInstance.Destroy</c> is
        /// immediate in both modes now, so a dead root leaves the hierarchy on the line that kills
        /// it and this set empties on the next pass. It used to be load-bearing, because a
        /// deferred destroy left a dead body in the scene until the end of the frame and it would
        /// have read as a new arrival. The deferral is exactly what parted the replay from its
        /// recording (logbook/0083), and the guard stays because a set of nulls costs nothing.
        /// </remarks>
        private readonly HashSet<Transform> _retired = new HashSet<Transform>();

        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<long> _pendingIds = new List<long>();
        private readonly List<float> _pendingHeights = new List<float>();
        private readonly List<int> _pendingParts = new List<int>();
        private readonly List<int> _pendingPatches = new List<int>();
        private readonly HashSet<long> _alive = new HashSet<long>();
        private readonly List<long> _gone = new List<long>();

        /// <summary>
        /// The box, when the world is one, and null when it is the tiled lattice. It is what says
        /// which of the two checks in the remarks a pairing gets, and it answers the patch
        /// question with the placer's own function rather than a second derivation of it.
        /// </summary>
        private SharedVolume _volume;

        private long _revision = -1;

        /// <summary>False once a pairing failed its check; selection is off from then on.</summary>
        public bool Reliable { get; private set; } = true;

        /// <summary>Why the map stopped being reliable, or null.</summary>
        public string Note { get; private set; }

        /// <summary>Creatures currently mapped to a body.</summary>
        public int Count => _rootById.Count;

        /// <summary>
        /// Notes which creatures are about to be given a body. Call immediately before
        /// <c>Ecosystem.Step</c>, and <see cref="AfterStep"/> immediately after it.
        /// </summary>
        /// <param name="world">The world about to be stepped.</param>
        /// <param name="volume">
        /// The world's box, or null for the tiled lattice. Pass <c>Ecosystem.Volume</c>: reading
        /// it is not reaching into the simulation, it is asking the placer where it put things,
        /// with the same function the placer used.
        /// </param>
        public void BeforeStep(World world, SharedVolume volume = null)
        {
            if (!Reliable) return;

            _volume = volume;

            long revision = world.Births + world.Deaths + world.FloorSpawns;
            if (revision == _revision) return;
            _revision = revision;

            IReadOnlyList<Organism> living = world.Living;

            _alive.Clear();
            for (int i = 0; i < living.Count; i++) _alive.Add(living[i].Id);

            // Departures first, so a dead creature's root is never mistaken for a new arrival.
            _gone.Clear();
            foreach (KeyValuePair<long, Transform> entry in _rootById)
            {
                if (!_alive.Contains(entry.Key)) _gone.Add(entry.Key);
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                Transform root = _rootById[_gone[i]];
                _rootById.Remove(_gone[i]);
                _idByRoot.Remove(root);
                if (root != null) _retired.Add(root);
            }

            _retired.RemoveWhere(t => t == null);

            _pendingIds.Clear();
            _pendingHeights.Clear();
            _pendingParts.Clear();
            _pendingPatches.Clear();

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (_rootById.ContainsKey(creature.Id)) continue;

                _pendingIds.Add(creature.Id);
                _pendingHeights.Add(creature.HeightY);
                _pendingParts.Add(creature.Phenotype.PartCount);
                _pendingPatches.Add(creature.Patch);
            }
        }

        /// <summary>Pairs the bodies the step just built with the creatures that wanted them.</summary>
        public void AfterStep()
        {
            if (!Reliable || _pendingIds.Count == 0) return;

            _roots.Clear();
            SceneManager.GetActiveScene().GetRootGameObjects(_roots);

            var fresh = new List<Transform>(_pendingIds.Count);

            for (int i = 0; i < _roots.Count; i++)
            {
                GameObject go = _roots[i];
                if (go == null || go.name != "Creature") continue;

                Transform t = go.transform;
                if (_idByRoot.ContainsKey(t) || _retired.Contains(t)) continue;

                fresh.Add(t);
            }

            if (fresh.Count != _pendingIds.Count)
            {
                Unreliable(
                    $"{_pendingIds.Count} creature(s) were waiting for a body and " +
                    $"{fresh.Count} unclaimed root(s) appeared");
                return;
            }

            for (int i = 0; i < fresh.Count; i++)
            {
                string wrong = WhyThisIsNotIt(fresh[i], i);

                if (wrong != null)
                {
                    Unreliable(wrong);
                    return;
                }

                _rootById[_pendingIds[i]] = fresh[i];
                _idByRoot[fresh[i]] = _pendingIds[i];
            }

            _pendingIds.Clear();
            _pendingHeights.Clear();
            _pendingParts.Clear();
            _pendingPatches.Clear();
        }

        /// <summary>
        /// The checks described in the remarks, against one pairing: null when the body could be
        /// the creature's, and what is wrong with it when it could not.
        /// </summary>
        private string WhyThisIsNotIt(Transform root, int i)
        {
            long id = _pendingIds[i];

            // One ArticulationBody per part, none on the root. Both worlds, and the check that
            // does the work: a body of four parts cannot be a creature of seven whatever the
            // placement rule is.
            int parts = root.GetComponentsInChildren<ArticulationBody>(true).Length;

            if (parts != _pendingParts[i])
            {
                return $"creature {id} has {_pendingParts[i]} part(s) and the root paired with " +
                       $"it has {parts}";
            }

            if (_volume == null)
            {
                // The tiled lattice: built at the creature's own height, exactly.
                return root.position.y == _pendingHeights[i]
                    ? null
                    : $"creature {id} stood at y={_pendingHeights[i]} and the root paired with " +
                      $"it stands at y={root.position.y}";
            }

            // The box. The patch is read from the x and z the placer reserved, which is where this
            // root stands, so the two must agree.
            int patch = _volume.PatchOf(root.position.x, root.position.z);

            if (patch != _pendingPatches[i])
            {
                return $"creature {id} was admitted into patch {_pendingPatches[i]} and the root " +
                       $"paired with it stands at x={root.position.x}, z={root.position.z}, " +
                       $"which is patch {patch}";
            }

            // And the placer only ever raises the height it is offered, so a body cannot have
            // been built above the height its creature is charged at. Equality is the ordinary
            // case; a bred child sits below it by however far its parent's root hangs under its
            // parent's centre of mass.
            if (root.position.y > _pendingHeights[i])
            {
                return $"creature {id} was admitted at y={_pendingHeights[i]} and the root paired " +
                       $"with it stands above that, at y={root.position.y}";
            }

            return null;
        }

        private void Unreliable(string why)
        {
            if (!Reliable) return;

            Reliable = false;
            Note = why;
            _rootById.Clear();
            _idByRoot.Clear();
            _pendingIds.Clear();
            _pendingHeights.Clear();
            _pendingParts.Clear();
            _pendingPatches.Clear();
        }

        /// <summary>The creature a transform belongs to, or -1. Accepts any part of the body.</summary>
        public long IdOf(Transform anyPartOfTheBody)
        {
            if (!Reliable) return -1;

            for (Transform t = anyPartOfTheBody; t != null; t = t.parent)
            {
                if (_idByRoot.TryGetValue(t, out long id)) return id;
            }

            return -1;
        }

        /// <summary>The body of a creature, or null.</summary>
        public Transform RootOf(long id) =>
            Reliable && _rootById.TryGetValue(id, out Transform root) ? root : null;

        /// <summary>The living organism with an id, or null. A scan: for one creature, not all.</summary>
        public static Organism Find(World world, long id)
        {
            IReadOnlyList<Organism> living = world.Living;
            for (int i = 0; i < living.Count; i++)
            {
                if (living[i].Id == id) return living[i];
            }

            return null;
        }

        public void Clear()
        {
            _rootById.Clear();
            _idByRoot.Clear();
            _retired.Clear();
            _pendingIds.Clear();
            _pendingHeights.Clear();
            _pendingParts.Clear();
            _pendingPatches.Clear();
            _volume = null;
            _revision = -1;
            Reliable = true;
            Note = null;
        }
    }
}
