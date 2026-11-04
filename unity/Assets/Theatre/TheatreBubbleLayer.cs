using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// One layer of air bubbles drawn by <c>Evosim/Theatre Bubbles</c> in a mesh and a pass of its own: the bite bubbles
    /// (<c>EVOSIM_THEATRE_BITE_BUBBLES</c>) and the oxygen pearls (<c>EVOSIM_THEATRE_PEARLING</c>) each hold one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Made only by an effect whose dial is on.</b> A layer is created lazily by its owner's first frame; with the owner's dial off
    /// nothing here is constructed, so no object, mesh, material, hook or list exists. The birth cloud keeps its own drawer
    /// (<see cref="TheatreBirthCloud"/>), untouched: this class is the same mesh of camera-facing quads, rebuilt when a game camera is
    /// about to render and drawn after the lens while the lens is armed, with the bubbles' shader in a material of its own (no
    /// second-take keyword, so it is the first take's compiled variant).
    /// </para>
    /// <para>
    /// <b>The owner fills it.</b> At each build the layer checks the water and the snow shells' box, copies the box into its
    /// material, takes the camera's frustum, and hands the camera to the owner's <see cref="Filler"/>, which writes its bubbles with
    /// <see cref="AddQuad"/>. The owner sets <see cref="Second"/> and bumps <see cref="Version"/> at each frame it changes the
    /// bubbles it holds, which is what lets the layer skip a rebuild for a camera that has not moved.
    /// </para>
    /// </remarks>
    internal sealed class TheatreBubbleLayer
    {
        /// <summary>Writes the owner's bubbles for one camera with <see cref="AddQuad"/>; returns the bubbles written.</summary>
        public delegate int Filler(TheatreBubbleLayer layer, Camera camera, TheatreSkin skin);

        /// <summary>The run's second the owner's bubbles are drawn at; NaN before its first frame.</summary>
        public double Second = double.NaN;

        /// <summary>Bumped by the owner whenever the bubbles it holds change at the same second.</summary>
        public int Version;

        private readonly string _label;
        private readonly Filler _fill;

        private GameObject _object;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Mesh _drawMesh;
        private Material _material;
        private LayerPass _pass;
        private bool _hooked;
        private bool _noShader;
        private bool _noShellsTold;

        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector2> _corners = new List<Vector2>();
        private readonly List<Vector4> _data = new List<Vector4>();
        private readonly List<int> _triangles = new List<int>();
        private readonly Plane[] _planes = new Plane[6];

        private double _builtSecond = double.NaN;
        private int _builtVersion = -1;
        private Camera _builtCamera;
        private Vector3 _builtEye;
        private Quaternion _builtRotation;
        private float _builtFov;
        private float _builtAspect;

        // The layer's count, told once after CountCalls game-camera calls: which of its ways out each call took.
        private const int CountCalls = 120;
        private int _calls, _off, _same, _noWater, _noSecond, _noShells, _fills, _filled;
        private bool _countTold;

        /// <param name="label">What the layer's log lines and objects are called, such as "bite bubbles".</param>
        /// <param name="fill">The owner's writer of bubbles.</param>
        public TheatreBubbleLayer(string label, Filler fill)
        {
            _label = label;
            _fill = fill;
        }

        /// <summary>
        /// The material, mesh, object, pass and hook, made once. False when the bubbles' shader cannot be found, said once; the
        /// owner then draws nothing.
        /// </summary>
        public bool Ensure()
        {
            if (_noShader) return false;

            if (_material == null)
            {
                Shader shader = Shader.Find(TheatreBirthBubbles.ShaderName);

                if (shader == null)
                {
                    _noShader = true;
                    Debug.LogWarning("[Theatre] " + _label + ": shader '" + TheatreBirthBubbles.ShaderName + "' not found, so no bubble is drawn");
                    return false;
                }

                _material = new Material(shader) { name = "Theatre " + _label, hideFlags = HideFlags.HideAndDontSave };
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Theatre " + _label, hideFlags = HideFlags.HideAndDontSave };
                _mesh.MarkDynamic();
            }

            // The object belongs to the scene the film plays in, and is made again if a scene change took it.
            if (_object == null)
            {
                _object = new GameObject("Theatre " + _label);
                _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

                _renderer = _object.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = ShadowCastingMode.Off;
                _renderer.receiveShadows = false;

                // Seen from inside the water only, and turned off for the census views, as the birth cloud's and the crumbs' are.
                _object.AddComponent<TheatreInsideOnly>();
            }

            if (_pass == null) _pass = new LayerPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1 };

            if (!_hooked)
            {
                RenderPipelineManager.beginCameraRendering += OnCamera;
                _hooked = true;
            }

            return true;
        }

        /// <summary>One camera-facing quad: the bubble's centre, and uv1 (radius, fade, seed, 0 for a sphere). Called only from a <see cref="Filler"/>.</summary>
        public void AddQuad(Vector3 place, Vector4 data)
        {
            int first = _positions.Count;

            for (int corner = 0; corner < 4; corner++)
            {
                _positions.Add(place);
                _corners.Add(new Vector2((corner == 0 || corner == 1) ? -1f : 1f, (corner == 0 || corner == 3) ? -1f : 1f));
                _data.Add(data);
            }

            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
        }

        /// <summary>Whether a sphere of this radius about this point reaches the camera's frustum; valid inside a <see cref="Filler"/>.</summary>
        public bool InFrustum(Vector3 point, float radius)
        {
            for (int i = 0; i < _planes.Length; i++)
            {
                if (_planes[i].GetDistanceToPoint(point) < -radius) return false;
            }

            return true;
        }

        private void OnCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_renderer == null || camera == null || camera.cameraType != CameraType.Game) return;

            _calls++;
            if (_calls == CountCalls && !_countTold) TellCount();

            // Turned off by the census views (TheatreInsideOnly): nothing to draw and nothing to build.
            if (!_renderer.enabled)
            {
                _off++;
                return;
            }

            bool lens = TheatreLens.Armed;

            try
            {
                Build(camera);
            }
            catch (Exception e)
            {
                // A cosmetic layer never takes the film down; its bubbles are simply absent for this render.
                _drawMesh = null;
                if (_mesh != null) _mesh.Clear();
                Debug.LogWarning("[Theatre] " + _label + ": the bubbles could not be built for this render: " + e.GetType().Name + ": " + e.Message);
            }

            // With the lens armed the shells are drawn after it, sharp; the bubbles follow them, as the birth cloud's do.
            _renderer.forceRenderingOff = lens;

            if (lens && _drawMesh != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.scriptableRenderer != null) data.scriptableRenderer.EnqueuePass(_pass);
            }
        }

        private void Build(Camera camera)
        {
            Transform view = camera.transform;
            TheatreSkin skin = TheatreSkin.Current;
            Material shells = skin != null ? skin.SnowShellsMaterial : null;

            // The run's water is opened by the first push of a frame; until then the current at a bubble would read zero.
            bool water = skin != null && (skin.RunWaterOpen || !skin.RunWaterWanted);

            bool same = ReferenceEquals(_builtCamera, camera) && Second == _builtSecond && Version == _builtVersion &&
                        view.position == _builtEye && view.rotation == _builtRotation && camera.fieldOfView == _builtFov &&
                        camera.aspect == _builtAspect && water;
            if (same)
            {
                _same++;
                return;
            }

            _builtCamera = camera;
            _builtSecond = Second;
            _builtVersion = Version;
            _builtEye = view.position;
            _builtRotation = view.rotation;
            _builtFov = camera.fieldOfView;
            _builtAspect = camera.aspect;

            _positions.Clear();
            _corners.Clear();
            _data.Clear();
            _triangles.Clear();
            _drawMesh = null;

            if (!water || double.IsNaN(Second) || shells == null || _material == null)
            {
                if (!water) _noWater++;
                else if (double.IsNaN(Second)) _noSecond++;
                else _noShells++;

                if (shells == null && !_noShellsTold && !double.IsNaN(Second) && skin != null)
                {
                    _noShellsTold = true;
                    Debug.Log("[Theatre] " + _label + ": the look has no snow shells (EVOSIM_THEATRE_SNOW_SHELLS), so the bubbles cannot be drawn in the water's box");
                }

                _mesh.Clear();
                return;
            }

            // The water the shells sit in.
            _material.SetVector("_BoxMin", shells.GetVector("_BoxMin"));
            _material.SetVector("_BoxMax", shells.GetVector("_BoxMax"));
            _material.SetVector("_Tank", shells.GetVector("_Tank"));
            _material.SetFloat("_Waterline", shells.GetFloat("_Waterline"));

            GeometryUtility.CalculateFrustumPlanes(camera, _planes);

            int drawn = _fill(this, camera, skin);
            _fills++;
            _filled += drawn;

            if (drawn > 0)
            {
                _mesh.Clear();
                _mesh.indexFormat = IndexFormat.UInt32;
                _mesh.SetVertices(_positions);
                _mesh.SetUVs(0, _corners);
                _mesh.SetUVs(1, _data);
                _mesh.SetTriangles(_triangles, 0, false);
                _mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
                _drawMesh = _mesh;
            }
            else
            {
                _mesh.Clear();
            }
        }

        /// <summary>The one count line: where the layer's first game-camera calls went, so a layer that draws nothing says why.</summary>
        private void TellCount()
        {
            _countTold = true;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] {0}: layer count after {1} camera calls: renderer off {2}, unchanged {3}, water not open {4}, no second {5}, " +
                "no shells or material {6}, filled {7} times with {8} bubbles in all",
                _label, _calls, _off, _same, _noWater, _noSecond, _noShells, _fills, _filled));
        }

        /// <summary>Draws the layer after the theatre's lens, as the birth cloud's pass does: each bubble carries its own blur.</summary>
        private sealed class LayerPass : ScriptableRenderPass
        {
            private readonly TheatreBubbleLayer _layer;

            public LayerPass(TheatreBubbleLayer layer)
            {
                _layer = layer;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            private sealed class PassData
            {
                public Mesh Mesh;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Mesh mesh = _layer._drawMesh;
                Material material = _layer._material;
                if (mesh == null || material == null) return;

                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle colour = resources.activeColorTexture;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!colour.IsValid() || !depth.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Theatre " + _layer._label, out PassData data))
                {
                    data.Mesh = mesh;
                    data.Material = material;

                    builder.UseTexture(depth);
                    builder.SetRenderAttachment(colour, 0, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                    {
                        context.cmd.DrawMesh(d.Mesh, Matrix4x4.identity, d.Material, 0, 0);
                    });
                }
            }
        }
    }
}
