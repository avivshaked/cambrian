using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Compiles every pass of every theatre shader and prints what the compiler said: the check
    /// the underwater look's build needs (<c>logbook/specs/underwater-look/brief.md</c>), because
    /// an import compiles nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it exists.</b> A batch Editor imports a <c>.shader</c> in about a hundredth of a
    /// second, which is a parse: an HLSL error in a function the parse never reaches is not in the
    /// log, and the first anyone hears of it is a pink or an empty frame. So each shader is given a
    /// material and each of its passes is compiled synchronously
    /// (<see cref="ShaderUtil.CompilePass"/>), and the shader's own messages are printed.
    /// </para>
    /// <para>
    /// <b>The instrument is checked too.</b> A two-line shader with a deliberate error is made in
    /// memory and compiled the same way first; if the check does not report that one, it reports
    /// nothing it can be trusted for, and says so.
    /// </para>
    /// <para>
    /// Launch with <c>-batchmode -quit</c> and
    /// <c>-executeMethod Evosim.Theatre.EditorTools.LookShaderCheck.Run</c>. Exits 1 on any error.
    /// </para>
    /// </remarks>
    public static class LookShaderCheck
    {
        private static readonly string[] Names =
        {
            "Evosim/Theatre Body", "Evosim/Theatre Bed", "Evosim/Theatre Rock", "Evosim/Theatre Earth",
            "Evosim/Theatre Surface", "Evosim/Theatre Backdrop", "Evosim/Theatre Glass",
            "Evosim/Theatre Shafts", "Evosim/Theatre Snow", "Evosim/Theatre Shaft March",
            "Evosim/Theatre Snow Shells", "Evosim/Theatre Tone Step",
        };

        private const string Broken =
            "Shader \"Hidden/Evosim/Look Check Broken\" { SubShader { Pass { HLSLPROGRAM\n" +
            "#pragma vertex V\n#pragma fragment F\n" +
            "float4 V(float4 p : POSITION) : SV_POSITION { return p; }\n" +
            "float4 F() : SV_Target { return NotAThing(1.0); }\n" +
            "ENDHLSL } } }";

        public static void Run()
        {
            var log = new StringBuilder("[Theatre] look shader check on " + SystemInfo.graphicsDeviceType + "\n");
            int errors = 0;

            Shader broken = ShaderUtil.CreateShaderAsset(Broken, true);
            int caught = broken != null ? Report(broken, log, true) : -1;
            bool trusted = caught > 0;
            log.Append(trusted
                ? "  the instrument: the deliberately broken shader was reported, so the check reads compile errors\n"
                : "  THE INSTRUMENT FAILED: the deliberately broken shader was not reported; nothing below proves a compile\n");
            if (broken != null) Object.DestroyImmediate(broken);

            foreach (string name in Names)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    log.Append("  MISSING " + name + "\n");
                    errors++;
                    continue;
                }

                errors += Report(shader, log, false);
            }

            log.Append(errors == 0 ? "  no shader errors\n" : "  " + errors + " shader error(s)\n");

            errors += DressSmoke(log);

            Debug.Log(log.ToString());

            EditorApplication.Exit(errors == 0 && trusted ? 0 : 1);
        }

        /// <summary>
        /// Builds a skin from the environment's switches, dresses a tank with it, and holds and
        /// lets go of the look as a census render would, printing what is built and drawn at each
        /// stage and the shaders' flags: the runtime half of the check, with no render. Returns
        /// the number of exceptions or broken promises.
        /// </summary>
        private static int DressSmoke(StringBuilder log)
        {
            int failures = 0;
            GameObject holder = null;
            TheatreSkin skin = null;

            try
            {
                holder = new GameObject("Look Check Camera") { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = holder.AddComponent<Camera>();
                camera.enabled = false;

                skin = new TheatreSkin();
                log.Append("  smoke: " + skin.Look.Describe() + "\n");

                skin.Apply(camera);
                skin.Dress(new Bounds(new Vector3(10f, -15f, 10f), new Vector3(20f, 30f, 20f)), null, 10f);

                log.Append("  smoke dressed: " + skin.FurnitureState() + "; _EvoLook " + Shader.GetGlobalVector("_EvoLook") +
                           "; _EvoLook2 " + Shader.GetGlobalVector("_EvoLook2") + "; _EvoFade " + Shader.GetGlobalVector("_EvoFade") +
                           "; tone step " + (TheatreToneStep.Active ? "on" : "off") + "\n");

                var summer = new TheatreDaylight.Reading { AttenuationDepth = 6f, DayFactor = 2.82f, Second = 36000, Source = "smoke" };
                var winter = new TheatreDaylight.Reading { AttenuationDepth = 6f, DayFactor = 0.34f, Second = 27000, Source = "smoke" };

                // For each kind of view: the meter in each season, and the brightness of what the
                // day lights after it (picture day times 2^EV), which must stay summer over winter.
                foreach (TheatreSkin.MeterView kind in new[] { TheatreSkin.MeterView.Subject, TheatreSkin.MeterView.Wide, TheatreSkin.MeterView.Sky })
                {
                    skin.PushDaylight(summer);
                    float summerEv = skin.MeterEv(-3f, kind);
                    float summerLit = skin.Look.PictureDay(summer.DayFactor) * Mathf.Pow(2f, summerEv);
                    skin.PushDaylight(winter);
                    float winterEv = skin.MeterEv(-3f, kind);
                    float winterLit = skin.Look.PictureDay(winter.DayFactor) * Mathf.Pow(2f, winterEv);

                    log.Append("  smoke meter, " + kind + " view at 3 m: summer " + summerEv.ToString("+0.00;-0.00") + " EV (lit x" + summerLit.ToString("0.00") +
                               "), winter " + winterEv.ToString("+0.00;-0.00") + " EV (lit x" + winterLit.ToString("0.00") + ")\n");

                    if (skin.Look.DepthLight && !(summerLit > winterLit))
                    {
                        log.Append("  SMOKE FAILED: after the meter the winter frame is not darker than the summer one\n");
                        failures++;
                    }
                }

                log.Append("  smoke label: " + skin.LabelToken("look 2", false) + " (census: " + skin.LabelToken("look 2", true) + "); _EvoDaylight " +
                           Shader.GetGlobalVector("_EvoDaylight") + "\n");

                Vector4 lookBefore = Shader.GetGlobalVector("_EvoLook");
                Vector4 sunBefore = Shader.GetGlobalVector("_EvoSun");

                using (skin.HoldTodaysLook())
                {
                    Vector4 held = Shader.GetGlobalVector("_EvoLook");
                    log.Append("  smoke held: " + skin.FurnitureState() + "; _EvoLook " + held + "; meter " + skin.MeterEv(-3f, TheatreSkin.MeterView.Subject).ToString("0.00") + "\n");

                    if (held.x != 0f || held.y != 0f || held.z != 0f)
                    {
                        log.Append("  SMOKE FAILED: the census hold left a shader flag on\n");
                        failures++;
                    }

                    // The second batch: every flag and the tone step off under the hold.
                    Vector4 held2 = Shader.GetGlobalVector("_EvoLook2");
                    log.Append("  smoke held, second batch: _EvoLook2 " + held2 + ", tone step " + (TheatreToneStep.Active ? "on" : "off") + "\n");

                    if (held2 != Vector4.zero || TheatreToneStep.Active)
                    {
                        log.Append("  SMOKE FAILED: the census hold left a second-batch flag on\n");
                        failures++;
                    }
                }

                Vector4 lookAfter = Shader.GetGlobalVector("_EvoLook");
                Vector4 sunAfter = Shader.GetGlobalVector("_EvoSun");
                log.Append("  smoke released: " + skin.FurnitureState() + "; _EvoLook " + lookAfter + "\n");

                if (lookAfter != lookBefore || sunAfter != sunBefore)
                {
                    log.Append("  SMOKE FAILED: letting go of the hold did not put the look back\n");
                    failures++;
                }

                // Item 1's check, as far as it can be made without a picture: the sun the water
                // draws does not move when the key turns behind another camera.
                skin.Aim(Quaternion.Euler(0f, 0f, 0f));
                skin.PushWater();
                Vector4 one = Shader.GetGlobalVector("_EvoSun");
                skin.Aim(Quaternion.Euler(10f, 140f, 0f));
                skin.PushWater();
                Vector4 two = Shader.GetGlobalVector("_EvoSun");
                log.Append("  smoke sun at two bearings: " + one + " and " + two +
                           (skin.Look.OneSun ? (one == two ? " (one sun: the same, as it must be)" : " (ONE SUN FAILED: they differ)") : " (one sun off: the key's, so they may differ)") + "\n");
                if (skin.Look.OneSun && one != two) failures++;
            }
            catch (System.Exception e)
            {
                log.Append("  SMOKE FAILED: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace + "\n");
                failures++;
            }
            finally
            {
                skin?.Dispose();
                if (holder != null) Object.DestroyImmediate(holder);
            }

            return failures;
        }

        /// <summary>Compiles every pass and appends the shader's messages; returns its error count.</summary>
        private static int Report(Shader shader, StringBuilder log, bool quiet)
        {
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                for (int pass = 0; pass < material.passCount; pass++)
                {
                    ShaderUtil.CompilePass(material, pass, true);
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
            }

            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            int errors = 0;
            var seen = new HashSet<string>();

            foreach (ShaderMessage message in messages)
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errors++;

                string line = "    " + message.severity + " " + message.message + " (" + message.file + ":" + message.line + ", " + message.platform + ")";
                if (!quiet && seen.Add(line)) log.Append(line + "\n");
            }

            if (ShaderUtil.ShaderHasError(shader) && errors == 0) errors = 1;
            if (!quiet) log.Append("  " + shader.name + ": " + messages.Length + " message(s), " + (errors == 0 ? "compiles" : errors + " error(s)") + "\n");

            return errors;
        }
    }
}
