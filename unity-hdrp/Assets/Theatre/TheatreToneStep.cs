using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The URP theatre's tone step, a render pass after the grade, held inert in the HDRP spike
    /// (D202). URP's render pass API has no counterpart here; HDRP's would be a custom post-process
    /// volume component, and the spike compares the pictures before deciding whether the step is
    /// wanted at all. The fields stay so the skin and the checks that set and read them compile.
    /// </summary>
    public static class TheatreToneStep
    {
        /// <summary>Whether the step would run. Nothing reads it in the spike.</summary>
        public static bool Active;

        /// <summary>The step's curve, as the URP theatre sets it.</summary>
        public static Vector4 Curve = new Vector4(0.01f, 1.2f, 0.2f, 1.1f);

        /// <summary>Does nothing in the spike.</summary>
        public static void Ensure() { }
    }
}
