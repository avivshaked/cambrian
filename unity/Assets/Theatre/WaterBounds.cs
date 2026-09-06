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
    /// <b>In a tiled world, patch boundaries are not drawn, because they are not boundaries.</b>
    /// D061's horizontal patches are an index carried on each creature and on each field cell —
    /// <c>Organism.Patch</c> — not a region of space: a creature's patch and its lattice tile are
    /// unrelated, and two creatures side by side on screen may be in different patches. Drawing
    /// lines between them would be inventing geometry the simulation does not have. The patch a
    /// selected creature is in is in the overlay instead, where it is a fact rather than a
    /// picture.
    /// </para>
    /// <para>
    /// <b>Likewise the horizontal extent is the lattice, not the world.</b>
    /// <c>RunConfig.WorldAreaSquareMetres</c> is the aperture the sun shines through and the
    /// denominator of shading (D053) — 100 m² in the reference world, which is 10 m across, while
    /// the same world's bodies are spread over kilometres of lattice. The grid therefore spans
    /// where the creatures are, and says nothing about the column's area.
    /// </para>
    /// <para>
    /// <b>Under <c>RunConfig.SharedSpace</c> the floor is a solid thing and is drawn as one.</b>
    /// D077's bottom was a restoring force — a rule, with nothing there — and a wire grid was an
    /// honest picture of it. Since <c>scratch/floor-spec.md</c> the bed is a static collider a body
    /// rests on (<c>Evosim.Sim.SeaFloor</c>), so it is filled rather than drawn as lines, and the
    /// grid is kept over the fill as a scale reference rather than as the floor itself. What is
    /// filled is the box's own footprint, x in [0, K·W), z in [0, W): the collider overhangs that
    /// by <c>SeaFloor.SeamMarginMetres</c> so a body caught mid-wrap has rock under it, and drawing
    /// the overhang would put sea bed where there is no water.
    /// </para>
    /// <para>
    /// <b>Both of those stop being true under <c>RunConfig.SharedSpace</c></b> (D077), and
    /// <see cref="ShowBox"/> is what the theatre draws then: the box is literal — K patches of
    /// <c>sqrt(area / K)</c> metres side by side on a ring, x in [0, K·W), z in [0, W) — and the
    /// K−1 lines between them are boundaries a creature actually crosses. The far seam (x = K·W
    /// back to x = 0) is the same line as x = 0 and is drawn as the box's own end face; a body
    /// that leaves there reappears at the other, which no still picture can show.
    /// </para>
    /// </remarks>
    public sealed class WaterBounds : MonoBehaviour
    {
        public Color SurfaceColour = new Color(0.45f, 0.75f, 0.95f, 0.5f);
        public Color FloorColour = new Color(0.55f, 0.45f, 0.30f, 0.5f);

        /// <summary>
        /// The sea bed's fill, drawn under the box's floor grid — <c>scratch/floor-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// Darker than <see cref="FloorColour"/> so the grid still reads on top of it. Only the
        /// box has one: in a tiled world the floor is a rule about a number and not a place, and
        /// filling a plane there would draw a sea bed the run does not have.
        /// </remarks>
        public Color BedColour = new Color(0.28f, 0.23f, 0.16f, 1f);

        /// <summary>Where a patch seam is drawn: x = k·W, for k in 1..K−1 — D077.</summary>
        public Color SeamColour = new Color(0.95f, 0.85f, 0.45f, 0.55f);

        private float _depth;
        private float _extent;
        private float _spacing;
        private Material _material;
        private bool _ready;

        /// <summary>The box, when the run has one — D077. Zero width means "no box, draw a grid".</summary>
        private float _boxLength;
        private float _boxWidth;
        private int _patches;

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
            _boxWidth = 0f;
            _boxLength = 0f;
            _patches = 1;
            _ready = true;
        }

        /// <summary>
        /// Draws D077's actual box — the water the run was simulated in — and its patch seams.
        /// </summary>
        /// <param name="depthMetres"><c>RunConfig.WorldDepthMetres</c>. The box runs from y = 0 to −D.</param>
        /// <param name="patchWidthMetres">W = sqrt(area / K), the side of one patch.</param>
        /// <param name="patches">K. K−1 seams are drawn; the K-th is the box's own end face.</param>
        public void ShowBox(float depthMetres, float patchWidthMetres, int patches)
        {
            _depth = Mathf.Max(0.1f, depthMetres);
            _patches = Mathf.Max(1, patches);
            _boxWidth = Mathf.Max(0.1f, patchWidthMetres);
            _boxLength = _boxWidth * _patches;

            // A grid pitch that gives a legible number of lines whatever the box is: a 10 m patch
            // wants metres, a 100 m one does not.
            _spacing = Mathf.Max(1f, Mathf.Round(_boxWidth / 5f));
            _extent = Mathf.Max(_boxLength, _boxWidth);
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

                // Two-sided, for the bed: the free-fly camera can go under the world, and a
                // back-face-culled quad would leave a hole to fall through with nothing in it.
                // Harmless for the lines, which have no facing.
                _material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }

            _material.SetPass(0);

            GL.PushMatrix();

            // The bed first and the lines after, so the grid and the seams read on top of it.
            if (_boxWidth > 0f)
            {
                GL.Begin(GL.QUADS);
                Bed();
                GL.End();
            }

            GL.Begin(GL.LINES);

            if (_boxWidth > 0f) Box();
            else Lattice();

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>Two grids spanning where the tiled creatures are — the pre-D077 picture.</summary>
        private void Lattice()
        {
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
        }

        /// <summary>D077's box: the surface, the floor, the four vertical edges, the seams.</summary>
        private void Box()
        {
            BoxGrid(0f, SurfaceColour);
            BoxGrid(-_depth, FloorColour);

            // The four corners, so the box reads as a volume.
            GL.Color(new Color(SurfaceColour.r, SurfaceColour.g, SurfaceColour.b, 0.35f));
            Vertical(0f, 0f);
            Vertical(_boxLength, 0f);
            Vertical(0f, _boxWidth);
            Vertical(_boxLength, _boxWidth);

            // The K−1 patch seams, full height and in their own colour: under SharedSpace these
            // are regions a creature is in and crosses, not indices. The K-th seam is the box's
            // own end face, already drawn — and it is the same line as x = 0, because the ring
            // wraps there.
            GL.Color(SeamColour);
            for (int k = 1; k < _patches; k++)
            {
                float x = k * _boxWidth;

                GL.Vertex3(x, 0f, 0f);
                GL.Vertex3(x, 0f, _boxWidth);
                GL.Vertex3(x, -_depth, 0f);
                GL.Vertex3(x, -_depth, _boxWidth);
                Vertical(x, 0f);
                Vertical(x, _boxWidth);
            }
        }

        /// <summary>The sea bed, filled — one quad across the box's footprint at y = −D.</summary>
        private void Bed()
        {
            GL.Color(BedColour);

            GL.Vertex3(0f, -_depth, 0f);
            GL.Vertex3(_boxLength, -_depth, 0f);
            GL.Vertex3(_boxLength, -_depth, _boxWidth);
            GL.Vertex3(0f, -_depth, _boxWidth);
        }

        private void Vertical(float x, float z)
        {
            GL.Vertex3(x, 0f, z);
            GL.Vertex3(x, -_depth, z);
        }

        private void BoxGrid(float y, Color colour)
        {
            GL.Color(colour);

            for (float x = 0f; x <= _boxLength + 0.001f; x += _spacing)
            {
                GL.Vertex3(x, y, 0f);
                GL.Vertex3(x, y, _boxWidth);
            }

            for (float z = 0f; z <= _boxWidth + 0.001f; z += _spacing)
            {
                GL.Vertex3(0f, y, z);
                GL.Vertex3(_boxLength, y, z);
            }
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
