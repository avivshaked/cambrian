using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evosim.Core;

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
    /// <b>And how it is checked.</b> A body is built at <c>(tileX, creature.HeightY, tileZ)</c>
    /// and its root transform is never moved again — physics moves the articulation root, which
    /// is that root's child. So a new root's y coordinate is the height its creature stood at
    /// when the body was built, and every pairing is verified against it. A failure does not
    /// guess: the map goes unreliable and the HUD says selection is unavailable, because a
    /// viewer confidently naming the wrong creature is worse than one that admits it cannot.
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
        /// <c>Object.Destroy</c> is deferred to the end of the frame in Play mode, so a dead root
        /// is still in the hierarchy for a while and would otherwise read as a new arrival.
        /// </summary>
        private readonly HashSet<Transform> _retired = new HashSet<Transform>();

        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<long> _pendingIds = new List<long>();
        private readonly List<float> _pendingHeights = new List<float>();
        private readonly HashSet<long> _alive = new HashSet<long>();
        private readonly List<long> _gone = new List<long>();

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
        public void BeforeStep(World world)
        {
            if (!Reliable) return;

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

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];
                if (_rootById.ContainsKey(creature.Id)) continue;

                _pendingIds.Add(creature.Id);
                _pendingHeights.Add(creature.HeightY);
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
                // The check described in the remarks: a root stands exactly where its creature
                // stood when the body was built.
                if (fresh[i].position.y != _pendingHeights[i])
                {
                    Unreliable(
                        $"creature {_pendingIds[i]} stood at y={_pendingHeights[i]} and the root " +
                        $"paired with it stands at y={fresh[i].position.y}");
                    return;
                }

                _rootById[_pendingIds[i]] = fresh[i];
                _idByRoot[fresh[i]] = _pendingIds[i];
            }

            _pendingIds.Clear();
            _pendingHeights.Clear();
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
            _revision = -1;
            Reliable = true;
            Note = null;
        }
    }
}
