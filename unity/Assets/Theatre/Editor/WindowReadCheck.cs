using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Whether this theatre reads a farm film window, without drawing a frame: opens each window named in
    /// <c>EVOSIM_THEATRE_WINDOWS</c> (separated by <c>;</c>), shows its first, middle and last frames as bodies in edit
    /// mode, and logs what it read, so a check can run with <c>-nographics</c>, off the graphics card.
    /// </summary>
    /// <remarks>
    /// Each line names the window's verdict word, its frames and seconds, the bodies shown, and the counts the window
    /// world keeps of what it could not read (bodies without a genome, refused poses, unreadable rows). With
    /// <c>EVOSIM_THEATRE_WINDOW_IDS</c> set (comma separated) it also says whether each of those bodies is in the middle
    /// frame. Exits 0 when every window opened and showed its three frames, 1 otherwise.
    /// <code>
    /// $env:EVOSIM_THEATRE_WINDOWS = "&lt;window dir&gt;;&lt;window dir&gt;"
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @('-projectPath', "$PWD/unity-w8", '-batchmode',
    ///   '-nographics', '-quit', '-executeMethod', 'Evosim.Theatre.EditorTools.WindowReadCheck.Run', '-logFile', $log)
    /// </code>
    /// </remarks>
    public static class WindowReadCheck
    {
        private const string Tag = "[WindowReadCheck] ";

        public static void Run()
        {
            int code = 0;

            try
            {
                string list = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WINDOWS") ?? "";
                string[] ids = (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WINDOW_IDS") ?? "")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string raw in list.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Check(raw.Trim(), ids)) code = 1;
                }

                if (list.Trim().Length == 0)
                {
                    Debug.LogError(Tag + "EVOSIM_THEATRE_WINDOWS names no window.");
                    code = 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "failed: " + e);
                code = 1;
            }

            Debug.Log(Tag + (code == 0 ? "PASS" : "FAIL"));
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static bool Check(string directory, string[] ids)
        {
            FilmWindowWorld world = FilmWindowWorld.Open(directory, null, out string refusal);

            if (world == null)
            {
                Debug.LogError(Tag + directory + ": refused: " + refusal);
                return false;
            }

            using (world)
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                Debug.Log(Tag + directory + ": " + world.ProvenanceWord + ", " + world.FrameCount + " frames, " +
                          world.FirstSecond.ToString("0.###", c) + " to " + world.LastSecond.ToString("0.###", c) + " s" +
                          (world.SourceNote != null ? "; " + world.SourceNote : ""));

                int last = world.FrameCount - 1;
                bool ok = true;

                foreach (int f in new[] { 0, last / 2, last })
                {
                    double second = world.Window.SecondOf(f);
                    bool shown = world.ShowAt(second);
                    Debug.Log(Tag + "  frame " + f + " at " + second.ToString("0.###", c) + " s: " + (shown ? "shown" : "NOT shown") +
                              ", " + world.BodyCount + " bodies; without a genome " + world.WithoutAGenome + ", pose refusals " +
                              world.PoseRefusals + ", unreadable " + world.Unreadable);
                    if (!shown) ok = false;

                    if (f == last / 2 && ids.Length > 0)
                    {
                        foreach (string id in ids)
                        {
                            long want = long.Parse(id.Trim(), c);
                            bool found = false;
                            for (int i = 0; i < world.BodyCount && !found; i++) found = world.IdAt(i) == want;
                            Debug.Log(Tag + "    body " + want + (found ? " is in the frame" : " is NOT in the frame"));
                        }
                    }
                }

                return ok;
            }
        }
    }
}
