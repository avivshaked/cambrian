using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Evosim.Theatre.Recording
{
    /// <summary>
    /// The safari's record button, wired to the Unity Recorder (safari-spec.md item 12, route b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An assembly of its own, and it only compiles where the package is.</b> The Recorder is an
    /// Editor package (<c>com.unity.recorder</c> 5.1.6 in the manifest), so nothing under the
    /// runtime <c>Evosim.Theatre</c> may reference it. This assembly references it and sets
    /// <see cref="global::Evosim.Theatre.TheatreSafari.Recorder"/> when the Editor loads; the
    /// define constraint drops the whole assembly on a project without the package, and the
    /// safari then says that record needs the Recorder rather than failing to build.
    /// </para>
    /// <para>
    /// <b>The input is the composite render texture, never the Game View</b>
    /// (<see cref="SafariComposite"/>): the world camera drawn into it and the interface's panel
    /// drawn over it, which is the route <see cref="TheatreUiCapture"/> takes for pictures and
    /// item 13's fix for the Recorder hiding the interface. H.264 in MP4 at high quality, constant
    /// frame rate with the cap on, so a clip's second is a simulated second: the safari steps the
    /// world one frame interval per rendered frame while a clip is open.
    /// </para>
    /// <para>
    /// <b>Untested until the owner records a clip in the Editor.</b> Two things are the first to
    /// check: that the picture is the right way up (<c>EVOSIM_THEATRE_SAFARI_FLIP=1</c> flips it),
    /// and that the interface is in the clip when the panel's <c>ui</c> button says <c>ui on</c>.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class SafariRecorderBinding
    {
        static SafariRecorderBinding()
        {
            global::Evosim.Theatre.TheatreSafari.Recorder = new UnityRecorder();
        }
    }

    internal sealed class UnityRecorder : ISafariRecorder
    {
        private RecorderController _controller;
        private RecorderControllerSettings _settings;
        private MovieRecorderSettings _movie;
        private string _file;

        public bool Recording => _controller != null && _controller.IsRecording();

        public bool Start(RenderTexture source, string fileWithoutExtension, float fps, out string note)
        {
            if (!EditorApplication.isPlaying) { note = "the Recorder records in Play mode only"; return false; }
            if (source == null) { note = "no render texture to record"; return false; }
            if (Recording) Stop(out _);

            try
            {
                _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                _movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                _movie.name = "Theatre Safari";
                _movie.Enabled = true;
                _movie.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                };
                _movie.ImageInputSettings = new RenderTextureInputSettings
                {
                    RenderTexture = source,
                    FlipFinalOutput = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_FLIP") == "1",
                };
                _movie.OutputFile = fileWithoutExtension;
                _file = fileWithoutExtension;

                _settings.AddRecorderSettings(_movie);
                _settings.SetRecordModeToManual();
                _settings.FrameRatePlayback = FrameRatePlayback.Constant;
                _settings.FrameRate = fps;
                _settings.CapFrameRate = true;

                _controller = new RecorderController(_settings);
                _controller.PrepareRecording();

                if (!_controller.StartRecording())
                {
                    note = "RecorderController.StartRecording returned false (the Console says why)";
                    Discard();
                    return false;
                }

                note = string.Format(CultureInfo.InvariantCulture, "H.264 MP4, {0}x{1}, {2:0.##} fps constant, from the composite render texture",
                    source.width, source.height, fps);
                return true;
            }
            catch (Exception e)
            {
                note = e.GetType().Name + ": " + e.Message;
                Discard();
                return false;
            }
        }

        public void Stop(out string note)
        {
            if (_controller == null) { note = "nothing was recording"; return; }

            try
            {
                if (_controller.IsRecording()) _controller.StopRecording();
                note = "stopped " + _file + ".mp4";
            }
            catch (Exception e)
            {
                note = e.GetType().Name + ": " + e.Message;
            }

            Debug.Log("[Theatre] safari: " + note);
            Discard();
        }

        private void Discard()
        {
            _controller = null;
            if (_movie != null) Object.DestroyImmediate(_movie);
            if (_settings != null) Object.DestroyImmediate(_settings);
            _movie = null;
            _settings = null;
        }
    }
}
