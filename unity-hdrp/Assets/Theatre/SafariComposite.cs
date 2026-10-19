using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Evosim.Theatre
{
    /// <summary>
    /// The Unity Recorder, as the runtime sees it: an Editor assembly sets
    /// <see cref="TheatreSafari.Recorder"/> to one of these, because the Recorder is an Editor
    /// package and nothing under <c>Evosim.Theatre</c> may reference an Editor assembly.
    /// </summary>
    public interface ISafariRecorder
    {
        bool Recording { get; }

        /// <summary>Starts a clip of the texture at <paramref name="fps"/>, written to the path with the encoder's extension.</summary>
        bool Start(RenderTexture source, string fileWithoutExtension, float fps, out string note);

        void Stop(out string note);
    }

    /// <summary>
    /// The frame the Recorder records: the world camera drawn into a render texture, and the
    /// interface's panel drawn over it into the same texture (safari-spec.md item 13).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The overlay bug, and the route around it.</b> The Recorder's Game View capture hides a
    /// UI Toolkit panel (HANDOFF's queued item since 2026-09-13). <see cref="TheatreUiCapture"/>
    /// already takes pictures of the interface the other way: the world camera renders into a
    /// texture by hand, the panel's <see cref="PanelSettings.targetTexture"/> is pointed at the
    /// same texture with its clear off, and the panel draws over the world on its own repaint.
    /// This is that route held open for the length of a clip rather than for one picture: the
    /// camera is rendered into the texture once a frame from <see cref="Render"/> (the host calls
    /// it from <c>LateUpdate</c>, after the camera has been posed), and the Recorder's
    /// render-texture input reads the texture at the end of the frame.
    /// </para>
    /// <para>
    /// <b>What it costs the screen.</b> While the panel draws into the texture it draws nothing
    /// on the screen, so the Game View shows the world with no interface for the length of a
    /// clip, and the panel's buttons cannot be clicked; the keys still work (<c>F9</c> stops).
    /// The world camera also renders twice a frame, once for the screen and once here.
    /// </para>
    /// <para>
    /// <b>Unverified until the owner records a clip</b> (the spec's own order: reproduce the bug
    /// on a worker with a graphics device, fix it, show the fix in a clip with the interface on).
    /// The one thing most likely to need a change is the vertical sense of the texture, which
    /// <c>EVOSIM_THEATRE_SAFARI_FLIP=1</c> flips in the Editor's recorder.
    /// </para>
    /// </remarks>
    public sealed class SafariComposite : IDisposable
    {
        private RenderTexture _target;
        private Camera _camera;
        private PanelSettings _panel;
        private RenderTexture _panelTargetWas;
        private bool _panelClearWas;
        private Color _panelClearColourWas;

        public RenderTexture Target => _target;
        public bool Open => _target != null;

        /// <summary>Makes the texture and points the panel at it. False with a note when it cannot.</summary>
        public bool Begin(int width, int height, Camera camera, PanelSettings panel, out string note)
        {
            Dispose();

            if (camera == null) { note = "no world camera"; return false; }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                note = "no graphics device";
                return false;
            }

            int w = Mathf.Clamp(width - width % 2, 64, TheatreUiCapture.MaximumSide);
            int h = Mathf.Clamp(height - height % 2, 64, TheatreUiCapture.MaximumSide);

            _target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "Theatre Safari Composite", antiAliasing = 1 };
            _target.Create();
            _camera = camera;

            if (panel != null)
            {
                _panel = panel;
                _panelTargetWas = panel.targetTexture;
                _panelClearWas = panel.clearColor;
                _panelClearColourWas = panel.colorClearValue;
                panel.clearColor = false;
                panel.targetTexture = _target;
            }

            Render();
            note = w + "x" + h + (panel != null ? ", world under the panel" : ", world only (no panel)");
            return true;
        }

        /// <summary>Draws the world into the texture. The panel draws over it on its own repaint.</summary>
        public void Render()
        {
            if (_target == null || _camera == null) return;

            RenderTexture was = _camera.targetTexture;
            try
            {
                _camera.targetTexture = _target;
                _camera.Render();
            }
            finally
            {
                _camera.targetTexture = was;
            }
        }

        public void Dispose()
        {
            if (_panel != null)
            {
                _panel.targetTexture = _panelTargetWas;
                _panel.clearColor = _panelClearWas;
                _panel.colorClearValue = _panelClearColourWas;
                _panel = null;
            }

            if (_target != null)
            {
                _target.Release();
                if (Application.isPlaying) Object.Destroy(_target);
                else Object.DestroyImmediate(_target);
                _target = null;
            }

            _camera = null;
        }
    }
}
