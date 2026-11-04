using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// A view dial that marks each eating part's mouth: a small unlit yellow sphere at
    /// <see cref="MouthPoint.Local"/>, parented to the part's object so it follows the pose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and off means nothing happens.</b> <c>EVOSIM_THEATRE_MOUTH_MARK</c> is 0
    /// unless set to 0.5 or more. Every caller tests <see cref="Enabled"/> before it calls anything
    /// here, so with the dial at 0 no object, material or keyword is made and no existing arithmetic
    /// is touched: the first line of each hook is the whole cost.
    /// </para>
    /// <para>
    /// <b>The point is Core's, not the theatre's.</b> The mark calls <see cref="MouthPoint.Local"/> and
    /// <see cref="MouthPoint.IsEatingPart"/>, the two the solver and the world's reach test call, so what
    /// is drawn is the point the reach is measured from. It is asked of the phenotype the body is drawn
    /// at (the view's sized one), never the adult genome's.
    /// </para>
    /// <para>
    /// <b>Frame.</b> A part's object sits at the part's centre and carries the part's own axes, with a
    /// scale of one: <c>PhenotypeBuilder</c>, <c>SnapshotWorld.Assemble</c> and <c>LiveWorldView.Build</c>
    /// all keep size on a "Visual" child and never on the part (the first of them says why: a scaled parent
    /// shears a rotated child). The only scale above a part is <c>LiveWorldView</c>'s root, uniform, for a
    /// corpse's shrink. The mark is a child of the part, so its local position is the point in the part's
    /// frame and its world position is <c>part.TransformPoint(point)</c> by construction, shrink included.
    /// </para>
    /// <para>
    /// <b>The palette must not see it.</b> <see cref="TheatrePalette"/> gathers every <c>MeshRenderer</c>
    /// under a body and squashes, carves and tints each as a part's visual, so a mark under a part would be
    /// dressed as tissue. <see cref="Strip"/> takes marks out of that list.
    /// </para>
    /// </remarks>
    public static class TheatreMouthMarks
    {
        /// <summary>The dial: 0, the default, draws nothing. It also draws nothing while <see cref="TheatreMouth2"/> is on, which replaces the sphere.</summary>
        public static readonly bool Enabled =
            TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH_MARK", 0f, 0f, 1f) >= 0.5f && !TheatreMouth2.Enabled;

        /// <summary>The mark's radius, as a fraction of the part's smallest half-extent.</summary>
        public const float RadiusFraction = 0.25f;

        /// <summary>
        /// The least radius in metres. A grazer's eating part can be a centimetre across, which leaves a quarter of it a
        /// pixel or two at portrait distance; the mark is a marker, not tissue, so it stays readable.
        /// </summary>
        public const float RadiusLeastMetres = 0.012f;

        private static int _made;
        private static int _missingMaterial;

        /// <summary>The mark's object name, by which <see cref="Strip"/> and the lookups find it.</summary>
        public const string MarkName = "Mouth Mark";

        /// <summary>Pure yellow: in none of the theatre's palettes.</summary>
        private static readonly Color Yellow = new Color(1f, 1f, 0f, 1f);

        private static Material _material;

        /// <summary>
        /// Puts the mark on part <paramref name="index"/>'s object, or sizes and places the one already
        /// there, or removes it when the part does not eat. Nothing for an index the phenotype lacks.
        /// </summary>
        /// <param name="part">The part's object: a unit-scale transform at the part's centre, in its own axes.</param>
        /// <param name="phenotype">The body at the size it is drawn at.</param>
        /// <param name="index">The part's index in <paramref name="phenotype"/>.</param>
        public static void Attach(Transform part, Phenotype phenotype, int index)
        {
            if (part == null || phenotype == null || index < 0 || index >= phenotype.PartCount) return;

            Transform mark = part.Find(MarkName);

            if (!MouthPoint.IsEatingPart(phenotype.Parts[index]))
            {
                if (mark != null) Discard(mark);
                return;
            }

            if (mark == null) mark = Make(part);
            if (mark == null) return;

            Float3 h = phenotype.Parts[index].HalfExtents;
            float smallest = Mathf.Min(Mathf.Abs(h.X), Mathf.Min(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));
            float diameter = 2f * Mathf.Max(RadiusFraction * Mathf.Max(smallest, 1e-4f), RadiusLeastMetres);

            Vector3 at = MouthPoint.Local(phenotype, index).ToVector3();
            Vector3 size = Vector3.one * diameter;

            if (mark.localPosition != at) mark.localPosition = at;
            if (mark.localScale != size) mark.localScale = size;
        }

        /// <summary>
        /// <see cref="Attach"/> for every part under a creature's root, found by the name
        /// <c>PhenotypeBuilder</c> gives a part (<c>Part07_n2</c>), at any depth of nesting. For the
        /// bodies the theatre does not build itself: the PhysX replay's and the solo creature's.
        /// </summary>
        public static void Ensure(Transform root, Phenotype phenotype)
        {
            if (root == null || phenotype == null) return;

            Visit(root, phenotype);
        }

        /// <summary>
        /// Takes the marks out of a list of renderers gathered from a body, so the palette neither
        /// reshapes nor paints them.
        /// </summary>
        public static void Strip(List<MeshRenderer> renderers)
        {
            if (renderers == null) return;

            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer != null && renderer.gameObject.name == MarkName) renderers.RemoveAt(i);
            }
        }

        private static void Visit(Transform parent, Phenotype phenotype)
        {
            for (int k = 0; k < parent.childCount; k++)
            {
                Transform child = parent.GetChild(k);
                int index = PartIndexOf(child.name);
                if (index < 0) continue;

                Attach(child, phenotype, index);
                Visit(child, phenotype);
            }
        }

        // "Part07_n2" -> 7; anything else (Visual, Mouth Mark, Creature) -> -1.
        private static int PartIndexOf(string name)
        {
            if (name == null || !name.StartsWith("Part", System.StringComparison.Ordinal)) return -1;

            int end = name.IndexOf('_');
            if (end < 0) end = name.Length;

            return int.TryParse(
                name.Substring(4, end - 4),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int index)
                ? index
                : -1;
        }

        private static Transform Make(Transform part)
        {
            Material material = MarkMaterial();
            if (material == null)
            {
                if (_missingMaterial++ == 0) Debug.LogWarning("[Theatre] mouth mark: no unlit shader found, so no mark is drawn.");
                return null;
            }

            _made++;
            if (_made == 1 || _made == 10 || _made == 100 || _made == 1000)
            {
                Debug.Log("[Theatre] mouth mark: " + _made + " made so far, shader " + material.shader.name);
            }

            var go = new GameObject(MarkName) { layer = PhenotypeBuilder.CreatureLayer };
            go.transform.SetParent(part, false);
            go.AddComponent<MeshFilter>().sharedMesh = TheatreMeshes.BuiltinSphere();

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return go.transform;
        }

        private static void Discard(Transform mark)
        {
            if (Application.isPlaying) Object.Destroy(mark.gameObject);
            else Object.DestroyImmediate(mark.gameObject);
        }

        // Unlit and one colour. URP's Unlit reads _BaseColor; the built-in one reads _Color.
        private static Material MarkMaterial()
        {
            if (_material != null) return _material;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default");

            if (shader == null) return null;

            _material = new Material(shader) { name = "Theatre Mouth Mark", hideFlags = HideFlags.DontSave };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Yellow);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", Yellow);

            return _material;
        }
    }
}
