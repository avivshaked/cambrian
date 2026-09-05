using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The water, drawn: the surface at y=0 and the floor at the run's depth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without this the world has no visible extent at all.</b> Creatures are tiled 100 m
    /// apart (§6.3) in otherwise empty space, so a camera flying between them has nothing to
    /// judge depth or scale against — and depth is the whole ecology (light falls off downward,
    /// detritus sinks). Two grids give a viewer the one axis that means something.
    /// </para>
    /// <para>
    /// <b>Patch boundaries are not drawn, because they are not boundaries.</b> D061's horizontal
    /// patches are an index carried on each creature and on each field cell — <c>Organism.Patch</c>
    /// — not a region of space: a creature's patch and its lattice tile are unrelated, and two
    /// creatures side by side on screen may be in different patches. Drawing lines between them
    /// would be inventing geometry the simulation does not have. The patch a selected creature is
    /// in is in the overlay instead, where it is a fact rather than a picture.
    /// </para>
    /// <para>
    /// <b>Likewise the horizontal extent is the lattice, not the world.</b>
    /// <c>RunConfig.WorldAreaSquareMetres</c> is the aperture the sun shines through and the
    /// denominator of shading (D053) — 100 m² in the reference world, which is 10 m across, while
    /// the same world's bodies are spread over kilometres of lattice. The grid therefore spans
    /// where the creatures are, and says nothing about the column's area.
    /// </para>
    /// </remarks>
    public sealed class WaterBounds : MonoBehaviour
    {
        public Color SurfaceColour = new Color(0.45f, 0.75f, 0.95f, 0.5f);
        public Color FloorColour = new Color(0.55f, 0.45f, 0.30f, 0.5f);

        private float _depth;
        private float _extent;
        private float _spacing;
        private Material _material;
        private bool _ready;

        /// <summary>
        /// Sets the water up from a loaded run.
        /// </summary>
        /// <param name="depthMetres"><c>RunConfig.WorldDepthMetres</c>.</param>
        /// <param name="extentMetres">How far the grid reaches from the origin.</param>
        /// <param name="spacingMetres">Grid pitch. The tile spacing makes the lattice legible.</param>
        public void Show(float depthMetres, float extentMetres, float spacingMetres)
        {
            _depth = Mathf.Max(0.1f, depthMetres);
            _extent = Mathf.Max(spacingMetres, extentMetres);
            _spacing = Mathf.Max(1f, spacingMetres);
            _ready = true;
        }

        public void Hide() => _ready = false;

        private void OnRenderObject()
        {
            if (!_ready) return;

            if (_material == null)
            {
                // The engine's own line shader, with two fallbacks: nothing here is worth a
                // shader asset, and a missing shader must not take the viewer down with it.
                Shader shader =
                    Shader.Find("Hidden/Internal-Colored") ??
                    Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Sprites/Default");

                if (shader == null) { _ready = false; return; }

                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetInt("_ZWrite", 0);
            }

            _material.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            Grid(0f, SurfaceColour);
            Grid(-_depth, FloorColour);

            // The verticals, so the two planes read as one volume rather than two floors.
            GL.Color(new Color(SurfaceColour.r, SurfaceColour.g, SurfaceColour.b, 0.25f));
            for (float x = -_extent; x <= _extent + 0.001f; x += _spacing * 4f)
            {
                for (float z = -_extent; z <= _extent + 0.001f; z += _spacing * 4f)
                {
                    GL.Vertex3(x, 0f, z);
                    GL.Vertex3(x, -_depth, z);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        private void Grid(float y, Color colour)
        {
            GL.Color(colour);

            for (float a = -_extent; a <= _extent + 0.001f; a += _spacing)
            {
                GL.Vertex3(a, y, -_extent);
                GL.Vertex3(a, y, _extent);
                GL.Vertex3(-_extent, y, a);
                GL.Vertex3(_extent, y, a);
            }
        }

        private void OnDestroy()
        {
            if (_material == null) return;

            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }
    }
}
