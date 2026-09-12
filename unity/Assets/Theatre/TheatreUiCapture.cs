using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Evosim.Theatre
{
    /// <summary>
    /// A picture of the theatre with its interface on it, taken off screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>ScreenCapture</c> writes nothing under <c>-batchmode</c>.</b> The batch Editor has a
    /// graphics device but no presented backbuffer, so <c>ScreenCapture.CaptureScreenshot</c>
    /// queues a capture that never lands: the interface check logged a shot for every state it
    /// walked and its directory stayed empty, and the snapshot entry's <c>-Chrome</c> frame
    /// logged "queued" and produced no file (2026-09-13). Nothing in the log said so, which is
    /// the worst shape a failure can take for an agent whose only eyes are the files.
    /// </para>
    /// <para>
    /// <b>So this goes the way <see cref="SnapshotCamera"/> already goes</b> — a
    /// <see cref="RenderTexture"/>, <c>ReadPixels</c>, <c>EncodeToPNG</c>, which is the one route
    /// known to work in this project under batch. The difference is that a screen-space UI panel
    /// does not draw into a camera's target: it draws into its own. UI Toolkit gives a runtime
    /// panel a render target through <see cref="PanelSettings.targetTexture"/>, so the two are
    /// pointed at one texture and the layers are put down in order — the world first, rendered
    /// here by hand, then the chrome over it by the panel's own repaint.
    /// </para>
    /// <para>
    /// <b>It therefore takes two ticks, and the API says so.</b> <see cref="Arm"/> makes the
    /// texture, renders the world into it and hands it to the panel; the panel draws on its next
    /// repaint, which is a frame away; <see cref="Shoot"/> reads the result back on a later tick
    /// and puts both targets back. Calling <c>Shoot</c> in the same tick as <c>Arm</c> would
    /// photograph the world with no interface on it.
    /// </para>
    /// <para>
    /// <b>Setting a panel's target texture resizes the panel to it.</b> Under Constant Pixel Size
    /// at scale 1 — which is what <see cref="TheatreUi"/> sets — the layout is the texture's
    /// pixels, so a 3840 by 2160 capture is the only way this project can exercise the design's
    /// 3400-pixel density step without a monitor that wide. That is a property of the route, not
    /// a trick on top of it, and it is why the check photographs at two sizes rather than at one
    /// size twice.
    /// </para>
    /// </remarks>
    public static class TheatreUiCapture
    {
        /// <summary>The largest side this will photograph. A 4K frame is 33 MB of readback.</summary>
        public const int MaximumSide = 4096;

        /// <summary>The smallest, so a mistyped size cannot ask for a one-pixel picture.</summary>
        public const int MinimumSide = 64;

        /// <summary>
        /// The dark field the theatre is lit against, so a picture taken with no camera stands on
        /// the same ground as one taken with. <see cref="SnapshotCamera.Water"/>'s value, kept in
        /// step by eye for the reason that class keeps it: this must work on a scene with no skin.
        /// </summary>
        private static readonly Color Ground = new Color(0.012f, 0.032f, 0.048f, 1f);

        private static RenderTexture _target;
        private static Texture2D _readback;

        private static Camera _camera;
        private static PanelSettings _panel;

        private static RenderTexture _cameraTargetWas;
        private static RenderTexture _panelTargetWas;
        private static bool _panelClearedColour;
        private static Color _panelClearColourWas;

        private static string _armedNote;

        /// <summary>True between <see cref="Arm"/> and <see cref="Shoot"/> or <see cref="Disarm"/>.</summary>
        public static bool Armed => _target != null;

        /// <summary>What the armed capture is, for a log line while it waits.</summary>
        public static string ArmedNote => _armedNote;

        /// <summary>
        /// Points the world camera and the interface's panel at a fresh texture and renders the
        /// world into it. The panel draws over that on its own next repaint.
        /// </summary>
        /// <param name="width">Wanted pixels across, clamped to 64..4096.</param>
        /// <param name="height">Wanted pixels down, clamped the same way.</param>
        /// <param name="camera">
        /// The world camera, or null for a picture of the interface over a flat ground.
        /// </param>
        /// <param name="panel">The interface's panel. Without it there is nothing to photograph.</param>
        /// <param name="note">What was armed, or why nothing was.</param>
        /// <returns>True when a capture is armed and <see cref="Shoot"/> should follow.</returns>
        public static bool Arm(int width, int height, Camera camera, PanelSettings panel, out string note)
        {
            if (Armed)
            {
                note = "a capture is already armed (" + _armedNote + "); shoot or disarm it first";
                return false;
            }

            if (panel == null)
            {
                note = "no PanelSettings: the interface did not load, so there is no chrome to take";
                return false;
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                note = "this Editor has no graphics device: nothing draws and nothing reads back";
                return false;
            }

            int w = Mathf.Clamp(width, MinimumSide, MaximumSide);
            int h = Mathf.Clamp(height, MinimumSide, MaximumSide);

            try
            {
                _target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Theatre UI Capture",
                    antiAliasing = 1,
                };

                // RGB24 rather than RGBA32: a camera's alpha in this pipeline is not a coverage
                // mask, and a PNG that carries it reads as a half-transparent picture in every
                // viewer. The picture wants to be opaque, so the alpha is never read back at all.
                _readback = new Texture2D(w, h, TextureFormat.RGB24, false);

                _panel = panel;
                _camera = camera;

                _panelTargetWas = panel.targetTexture;
                _panelClearedColour = panel.clearColor;
                _panelClearColourWas = panel.colorClearValue;

                // The texture starts as whatever memory it was handed. Clear it before anything
                // draws, so a camera that renders nothing leaves a flat ground rather than noise.
                RenderTexture wasActive = RenderTexture.active;
                RenderTexture.active = _target;
                try { GL.Clear(true, true, Ground); }
                finally { RenderTexture.active = wasActive; }

                // The world, by hand and now, so that the order of the two layers is not a guess
                // about where in the player loop a panel repaint sits. The camera goes back to
                // the screen immediately: its own per-frame render must not land in this texture
                // and wipe what the panel is about to draw over.
                bool rendered = false;

                if (camera != null)
                {
                    _cameraTargetWas = camera.targetTexture;

                    try
                    {
                        camera.targetTexture = _target;
                        camera.Render();
                        rendered = true;
                    }
                    finally
                    {
                        camera.targetTexture = _cameraTargetWas;
                    }
                }

                // The panel must not clear what the world just put down. With a camera it draws
                // over; without one it clears to the ground colour itself, so the picture is flat
                // rather than undefined.
                panel.clearColor = !rendered;
                panel.colorClearValue = Ground;
                panel.targetTexture = _target;

                _armedNote =
                    w + "x" + h + (rendered ? ", world under chrome" : ", chrome on a flat ground");

                note = _armedNote;
                return true;
            }
            catch (Exception e)
            {
                Disarm();
                note = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Reads the armed texture back, writes it as a PNG and restores both targets.
        /// </summary>
        /// <returns>The file's size in bytes, or 0 when nothing was written.</returns>
        public static int Shoot(string path, out string note)
        {
            if (!Armed)
            {
                note = "nothing is armed";
                return 0;
            }

            try
            {
                int w = _target.width;
                int h = _target.height;

                RenderTexture wasActive = RenderTexture.active;
                RenderTexture.active = _target;

                try
                {
                    _readback.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
                    _readback.Apply(false);
                }
                finally
                {
                    RenderTexture.active = wasActive;
                }

                byte[] png = _readback.EncodeToPNG();

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, png);

                note = w + "x" + h + ", " + (png.Length / 1024) + " KB";
                return png.Length;
            }
            catch (Exception e)
            {
                note = e.GetType().Name + ": " + e.Message;
                return 0;
            }
            finally
            {
                Disarm();
            }
        }

        /// <summary>
        /// Puts the camera and the panel back and throws the texture away. Safe at any time, and
        /// the one thing a failure path has to call.
        /// </summary>
        public static void Disarm()
        {
            if (_panel != null)
            {
                _panel.targetTexture = _panelTargetWas;
                _panel.clearColor = _panelClearedColour;
                _panel.colorClearValue = _panelClearColourWas;
            }

            if (_camera != null) _camera.targetTexture = _cameraTargetWas;

            if (_target != null)
            {
                _target.Release();
                Kill(_target);
                _target = null;
            }

            if (_readback != null)
            {
                Kill(_readback);
                _readback = null;
            }

            _panel = null;
            _camera = null;
            _panelTargetWas = null;
            _cameraTargetWas = null;
            _armedNote = null;
        }

        private static void Kill(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
