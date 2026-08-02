using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spike.EditorTools
{
    /// <summary>
    /// Batchmode entry point:
    ///   Unity.exe -projectPath &lt;spike&gt; -batchmode -quit -nographics \
    ///             -executeMethod Spike.EditorTools.SpikeEntry.Run
    /// </summary>
    public static class SpikeEntry
    {
        [MenuItem("Spike/Run All Measurements")]
        public static void RunFromMenu() => Run();

        public static void Run()
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "results");
            try
            {
                SpikeHarness.RunAll(outDir);
                Debug.Log($"[Spike] complete — results in {outDir}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Spike] harness failed: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                throw;
            }
        }
    }
}
