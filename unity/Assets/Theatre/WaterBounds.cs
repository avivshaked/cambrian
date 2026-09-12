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
    /// honest picture of it. Since <c>logbook/specs/floor-spec.md</c> the bed is a static collider a body
    /// rests on (<c>Evosim.Sim.SeaFloor</c>), so it is filled rather than drawn as lines, and the
    /// grid is kept over the fill as a scale reference rather than as the floor itself. What is
    /// filled is the box's own footprint, x in [0, W·K/A), z in [0, W·A): the collider overhangs it
    /// by <c>SeaFloor.SeamMarginMetres</c> so a body caught mid-wrap has rock under it, and drawing
    /// the overhang would put sea bed where there is no water.
    /// </para>
    /// <para>
    /// <b>Both of those stop being true under <c>RunConfig.SharedSpace</c></b> (D077), and
    /// <see cref="ShowBox"/> is what the theatre draws then: the box is literal — K patches of
    /// <c>sqrt(area / K)</c> metres, laid out K/A along x by A across z (fable-propose-box.md),
    /// x in [0, W·K/A), z in [0, W·A) — and the lines between them are boundaries a creature
    /// actually crosses. The far seam on each axis is the same line as 0 and is drawn as the
    /// box's own end face; a body that leaves there reappears at the other, which no still
    /// picture can show.
    /// </para>
    /// <para>
    /// <b>Or the water is a tank</b> — <c>fable-propose-aquarium.md</c> ruling 1,
    /// <c>logbook/specs/tank-spec.md</c>, and <see cref="ShowTank"/>. Then there is no seam to
    /// leave unshown and the picture can be honest all the way round: a circle of
    /// <c>R = sqrt(area/π)</c> at the surface and another at the floor, eight verticals between
    /// them, the patch rings as circles at their own boundary radii, and the bed as a disc rather
    /// than a square. Which shape is drawn is the config's <c>WorldShape</c> and never an
    /// inference from the numbers: a tank and a square box of the same area differ by a corner,
    /// and a viewer who cannot tell them apart cannot tell whether a body is against the glass.
    /// </para>
    /// </remarks>
    public sealed class WaterBounds : MonoBehaviour
    {
        public Color SurfaceColour = new Color(0.45f, 0.75f, 0.95f, 0.5f);
        public Color FloorColour = new Color(0.55f, 0.45f, 0.30f, 0.5f);

        /// <summary>
        /// The sea bed's fill, drawn under the box's floor grid — <c>logbook/specs/floor-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// Darker than <see cref="FloorColour"/> so the grid still reads on top of it. Only the
        /// box has one: in a tiled world the floor is a rule about a number and not a place, and
        /// filling a plane there would draw a sea bed the run does not have.
        /// </remarks>
        public Color BedColour = new Color(0.28f, 0.23f, 0.16f, 1f);

        /// <summary>
        /// Draw the filled floor quad. Turned off while <see cref="TheatreSkin"/> owns the bed.
        /// </summary>
        /// <remarks>
        /// The skin puts a real renderer on the sea floor with a sand material on it, and two beds
        /// at the same height would z fight along the whole floor of the picture. This one is the
        /// one that goes: it is a flat colour drawn in immediate mode, and it is already turned off
        /// entirely during a snapshot render (see <c>SnapshotCamera.SilenceTheWater</c>), so the
        /// grid lines above it are what this component is actually for.
        /// </remarks>
        public bool DrawBed = true;

        /// <summary>Where a patch seam is drawn: x = k·W, for k in 1..K−1 — D077.</summary>
        public Color SeamColour = new Color(0.95f, 0.85f, 0.45f, 0.55f);

        private float _depth;
        private float _extent;
        private float _spacing;
        private Material _material;
        private bool _ready;

        /// <summary>
        /// How many straight pieces one circle is drawn from — <c>logbook/specs/tank-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// Forty-eight, which is <c>Evosim.Sim.TankWall.Segments</c>: the glass a body actually
        /// hits is a forty-eight-sided prism, so a forty-eight-sided outline is the wall rather
        /// than a smoothed idea of it. The patch rings are drawn at the same resolution for one
        /// arc resolution in the picture; nothing here measures anything, so the number is a
        /// matter of how the frame reads and not of what the world is.
        /// </remarks>
        private const int CircleSegments = 48;

        /// <summary>The box, when the run has one — D077. Zero width means "no box, draw a grid".</summary>
        private float _boxLength;
        private float _boxWidth;
        private float _patchMetres;
        private int _patches;
        private int _patchesAcross = 1;

        /// <summary>
        /// The tank's radius, m — <c>fable-propose-aquarium.md</c> ruling 1. Zero means "not a
        /// tank", the way a zero <see cref="_boxWidth"/> means "no box".
        /// </summary>
        private float _tankRadius;

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
            _patchMetres = 0f;
            _patches = 1;
            _patchesAcross = 1;
            _tankRadius = 0f;
            _ready = true;
        }

        /// <summary>
        /// Draws D077's actual box — the water the run was simulated in — and its patch seams.
        /// </summary>
        /// <param name="depthMetres"><c>RunConfig.WorldDepthMetres</c>. The box runs from y = 0 to −D.</param>
        /// <param name="patchWidthMetres">W = sqrt(area / K), the side of one patch.</param>
        /// <param name="patchesAlong">K/A, the patches along x. The last seam is the box's own end face.</param>
        /// <param name="patchesAcross">
        /// A — <c>RunConfig.PatchesAcross</c>, the patches across z (fable-propose-box.md). 1 draws
        /// the row of patches every recording before the layout was made of.
        /// </param>
        public void ShowBox(
            float depthMetres, float patchWidthMetres, int patchesAlong, int patchesAcross = 1)
        {
            _depth = Mathf.Max(0.1f, depthMetres);
            _patches = Mathf.Max(1, patchesAlong);
            _patchesAcross = Mathf.Max(1, patchesAcross);
            _patchMetres = Mathf.Max(0.1f, patchWidthMetres);
            _boxWidth = _patchMetres * _patchesAcross;
            _boxLength = _patchMetres * _patches;

            // A grid pitch that gives a legible number of lines whatever the box is: a 10 m patch
            // wants metres, a 100 m one does not. Read off the patch rather than off the box, so
            // a square layout is drawn at the pitch its row-shaped twin would have been.
            _spacing = Mathf.Max(1f, Mathf.Round(_patchMetres / 5f));
            _extent = Mathf.Max(_boxLength, _boxWidth);
            _tankRadius = 0f;
            _ready = true;
        }

        /// <summary>
        /// Draws the tank — the cylinder of water the run was simulated in, and its patch rings.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The radius is the world's, not a shape chosen for the picture.</b> It comes from
        /// <c>Evosim.Core.TankGeometry.RadiusFor(area)</c> at the call site
        /// (<c>TheatreRunner.OpenWorld</c>), which is the same square root the fields, the placer
        /// and the glass were built with, so a viewer measuring a body against the wall in this
        /// picture is measuring it against the wall the body hit.
        /// </para>
        /// <para>
        /// <b>The bounding square is <c>[0, 2R)²</c> and the axis is at <c>(R, R)</c></b>
        /// (<c>TankGeometry</c>), so everything drawn here is in the same world coordinates the
        /// box path uses and a camera fitted to the square frames the tank.
        /// </para>
        /// </remarks>
        /// <param name="depthMetres"><c>RunConfig.WorldDepthMetres</c>. The water runs y = 0 to −D.</param>
        /// <param name="radiusMetres">R = sqrt(area/π) — <c>World.TankRadiusMetres</c>.</param>
        /// <param name="rings">
        /// K — <c>RunConfig.HorizontalPatches</c>. In a tank a patch is a ring of equal area, so
        /// K−1 circles are drawn inside the glass and the glass itself is the last boundary.
        /// </param>
        public void ShowTank(float depthMetres, float radiusMetres, int rings)
        {
            _depth = Mathf.Max(0.1f, depthMetres);
            _tankRadius = Mathf.Max(0.1f, radiusMetres);
            _patches = Mathf.Max(1, rings);
            _patchesAcross = 1;

            // The bounding square stands in for the box everywhere the extent is read: the
            // storage, the camera and the grid pitch all want a rectangle, and the circle is
            // inscribed in this one (TankGeometry's remarks).
            _boxLength = 2f * _tankRadius;
            _boxWidth = _boxLength;

            // No patch has a side in a tank — a ring is an annulus — and the field is cleared
            // rather than left carrying the last box's width, so nothing can draw a seam with it.
            _patchMetres = 0f;

            // Read off the whole footprint rather than off a patch, because a tank's patches are
            // annuli of very different widths and the innermost one would set a pitch that drew
            // hundreds of lines across the outermost.
            _spacing = Mathf.Max(1f, Mathf.Round(_boxLength / 10f));
            _extent = _boxLength;
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
            // A tank's bed is a disc and needs triangles where the box's needs one quad, which is
            // the only reason the two are not one call.
            if (_tankRadius > 0f && DrawBed)
            {
                GL.Begin(GL.TRIANGLES);
                BedDisc();
                GL.End();
            }
            else if (_boxWidth > 0f && DrawBed)
            {
                GL.Begin(GL.QUADS);
                Bed();
                GL.End();
            }

            GL.Begin(GL.LINES);

            if (_tankRadius > 0f) Tank();
            else if (_boxWidth > 0f) Box();
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

            // The patch seams, full height and in their own colour: under SharedSpace these are
            // regions a creature is in and crosses, not indices. The last seam on each axis is the
            // box's own end face, already drawn — and it is the same line as 0, because the ring
            // wraps there. At A = 1 the second loop runs no times and this is the picture every
            // recording before the layout was drawn as.
            GL.Color(SeamColour);
            for (int k = 1; k < _patches; k++)
            {
                float x = k * _patchMetres;

                GL.Vertex3(x, 0f, 0f);
                GL.Vertex3(x, 0f, _boxWidth);
                GL.Vertex3(x, -_depth, 0f);
                GL.Vertex3(x, -_depth, _boxWidth);
                Vertical(x, 0f);
                Vertical(x, _boxWidth);
            }

            for (int k = 1; k < _patchesAcross; k++)
            {
                float z = k * _patchMetres;

                GL.Vertex3(0f, 0f, z);
                GL.Vertex3(_boxLength, 0f, z);
                GL.Vertex3(0f, -_depth, z);
                GL.Vertex3(_boxLength, -_depth, z);
                Vertical(0f, z);
                Vertical(_boxLength, z);
            }
        }

        /// <summary>
        /// The tank: the circle at the surface and at the floor, eight verticals, the patch rings.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>No grid and no seams.</b> The box's two grids exist to give a viewer a scale on a
        /// flat plane; here the patch rings do that job and are boundaries as well, so a square
        /// grid over them would be two rulers disagreeing about what a line means. The rings are
        /// drawn in <see cref="SeamColour"/> for exactly the reason the box's seams are: under a
        /// shared volume a patch is a region a body is in and crosses, not an index.
        /// </para>
        /// <para>
        /// <b>Eight verticals, at 45°.</b> Enough that the two circles read as one cylinder from
        /// any angle, few enough that they do not become a cage in front of the bodies. They are
        /// at the glass, where the box's are at its corners, and they carry no information beyond
        /// "these two circles are the ends of the same water".
        /// </para>
        /// </remarks>
        private void Tank()
        {
            Circle(0f, _tankRadius, SurfaceColour);
            Circle(-_depth, _tankRadius, FloorColour);

            GL.Color(new Color(SurfaceColour.r, SurfaceColour.g, SurfaceColour.b, 0.35f));

            for (int i = 0; i < 8; i++)
            {
                float angle = 2f * Mathf.PI * i / 8f;

                Vertical(
                    _tankRadius + _tankRadius * Mathf.Cos(angle),
                    _tankRadius + _tankRadius * Mathf.Sin(angle));
            }

            // The patch rings, at the surface and at the floor so that the pair reads as a
            // cylinder of its own. The glass is ring K−1's outer boundary and is already drawn, so
            // the loop stops short of it.
            for (int k = 1; k < _patches; k++)
            {
                float r = RingBoundary(k);

                Circle(0f, r, SeamColour);
                Circle(-_depth, r, SeamColour);
            }
        }

        /// <summary>
        /// The radius where ring <paramref name="ring"/> begins, m: <c>R·sqrt(ring/K)</c>.
        /// </summary>
        /// <remarks>
        /// <b>The inverse of <c>TankGeometry.RingOf</c>'s <c>floor(K·(r/R)²)</c></b>, which is the
        /// one place the world decides which ring a body is in
        /// (<c>logbook/specs/tank-spec.md</c>). The arithmetic is repeated here rather than added
        /// to <c>TankGeometry</c>, which is deliberate and is the rule that keeps the theatre out
        /// of <c>Assets/Evosim</c>: a drawing must never be the reason a simulation source file
        /// changes. Nothing here is measured against anything — a circle a pixel off is not a
        /// world event, where a field and a placer disagreeing about a boundary would be.
        /// </remarks>
        private float RingBoundary(int ring) =>
            _tankRadius * Mathf.Sqrt(Mathf.Clamp01(ring / (float)Mathf.Max(1, _patches)));

        /// <summary>One horizontal circle about the axis at <c>(R, R)</c>, as a closed polyline.</summary>
        private void Circle(float y, float radius, Color colour)
        {
            if (!(radius > 0f)) return;

            GL.Color(colour);

            float previousX = _tankRadius + radius;
            float previousZ = _tankRadius;

            for (int i = 1; i <= CircleSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / CircleSegments;
                float x = _tankRadius + radius * Mathf.Cos(angle);
                float z = _tankRadius + radius * Mathf.Sin(angle);

                GL.Vertex3(previousX, y, previousZ);
                GL.Vertex3(x, y, z);

                previousX = x;
                previousZ = z;
            }
        }

        /// <summary>The sea bed, filled — a disc under the tank at y = −D, as a triangle fan.</summary>
        /// <remarks>
        /// A fan from the axis rather than a square: the water is the circle, and filling the
        /// bounding square would draw sea bed in four corners the run has no water in — the same
        /// fault the box's fill avoids by stopping short of <c>SeaFloor</c>'s overhang.
        /// </remarks>
        private void BedDisc()
        {
            GL.Color(BedColour);

            for (int i = 0; i < CircleSegments; i++)
            {
                float a = 2f * Mathf.PI * i / CircleSegments;
                float b = 2f * Mathf.PI * (i + 1) / CircleSegments;

                GL.Vertex3(_tankRadius, -_depth, _tankRadius);
                GL.Vertex3(
                    _tankRadius + _tankRadius * Mathf.Cos(a), -_depth,
                    _tankRadius + _tankRadius * Mathf.Sin(a));
                GL.Vertex3(
                    _tankRadius + _tankRadius * Mathf.Cos(b), -_depth,
                    _tankRadius + _tankRadius * Mathf.Sin(b));
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
