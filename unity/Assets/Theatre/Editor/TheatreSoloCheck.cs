using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Mode A with the rendering taken away: does a real creature, grown from a snapshot row,
    /// actually run under its own brain — and does the brain do anything the test sine does not?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What a headless pass can and cannot say.</b> It can say that the genome grew, that
    /// every sensor channel reported a finite value that was not constant, that the joints moved,
    /// and how far the body travelled under its brain against the same body under the test sine.
    /// It cannot say that the gait <i>looks</i> different, which is what Mode A is for; that
    /// needs the scene and a person.
    /// </para>
    /// <para>
    /// <b>The two runs are separate builds of the same genome from the same start</b>, so the
    /// comparison is the controller and nothing else — the same trap <c>JamSurvey</c> avoids by
    /// running one creature twice rather than two creatures once.
    /// </para>
    /// <code>
    /// $env:EVOSIM_THEATRE_RUN = 'D:\...\runs\th-ref'
    /// $env:EVOSIM_THEATRE_GENOME = 'D:\...\runs\th-ref\&lt;run&gt;\snapshots\000001000.jsonl'
    /// -executeMethod Evosim.Theatre.EditorTools.TheatreSoloCheck.Run
    /// </code>
    /// </remarks>
    public static class TheatreSoloCheck
    {
        [MenuItem("Evosim/Theatre — check one creature headlessly")]
        public static void FromMenu() => Check();

        public static void Run()
        {
            bool ok = Check();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Check()
        {
            string genomePath = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_GENOME");
            if (string.IsNullOrEmpty(genomePath))
            {
                Debug.LogError("[Theatre] EVOSIM_THEATRE_GENOME is not set: no genome to grow.");
                return false;
            }

            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");

            RunConfig config;
            string water;

            if (!string.IsNullOrEmpty(run))
            {
                RunRecord record = RunRecord.Load(run);
                config = record.Config;
                water = record.ArmName + " (" + record.ConfigHash + ")";
                Evosim.Sim.Ecosystem.ConfigurePhysicsStep(record.PhysicsDtSeconds);
            }
            else
            {
                config = new RunConfig();
                water = "RunConfig defaults";
            }

            float seconds = Number("EVOSIM_THEATRE_SECONDS", 30f);

            // A jointed body, because the thing being checked is whether a brain drives joints.
            // Chosen by developing rows until one has a degree of freedom rather than by trusting
            // a row index: which row is jointed is a property of the run, not of the file.
            string[] rows = JsonlWriter.ReadRows(genomePath);
            if (rows.Length == 0)
            {
                Debug.LogError("[Theatre] no rows in " + genomePath);
                return false;
            }

            int chosen = -1;
            Genome genome = null;
            int scanned = 0;

            for (int i = 0; i < rows.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(rows[i])) continue;

                scanned++;
                Genome candidate = GenomeJson.Read(rows[i]);
                Phenotype body = Developer.Develop(candidate, config.Development, null, config.Shapes);

                if (body.TotalDof <= 0) continue;

                chosen = i;
                genome = candidate;
                break;
            }

            if (genome == null)
            {
                Debug.LogWarning(
                    $"[Theatre] none of the {scanned} genomes in " +
                    $"{Path.GetFileName(genomePath)} develops a joint — the population is all " +
                    "rigid bodies, which is a real state of this world and not a failure here. " +
                    "Falling back to row 0 so the rest of the check still runs.");

                chosen = 0;
                genome = GenomeJson.Read(rows[0]);
            }

            long id = GenomeJson.ReadId(rows[chosen]);

            Debug.Log(
                $"[Theatre] solo check: {Path.GetFileName(genomePath)} row {chosen} " +
                $"(creature {id}) of {rows.Length}, water from {water}, {seconds} s each way");

            bool ok = true;
            ok &= Drive(genome, config, id, genomePath, useSine: false, seconds, out double brainDistance);
            ok &= Drive(genome, config, id, genomePath, useSine: true, seconds, out double sineDistance);

            Debug.Log(
                "[Theatre] solo check: brain travelled " + brainDistance.ToString("0.####") +
                " m, test sine travelled " + sineDistance.ToString("0.####") + " m over " +
                seconds + " s. A difference here is the brain's contribution against the null; " +
                "whether the gait looks different is a question for the scene.");

            return ok;
        }

        private static bool Drive(
            Genome genome, RunConfig config, long id, string source,
            bool useSine, float seconds, out double travelled)
        {
            travelled = 0d;

            SoloCreature solo;

            try
            {
                // A little detritus in the water, so the Chemical channel has something to
                // report: a reference world's initial field is empty, and a check that read zero
                // there would be measuring the field rather than the nose.
                solo = SoloCreature.Build(
                    genome, config, 1UL, 12f, id, Path.GetFileName(source), smellDensity: 5f);
            }
            catch (Exception e)
            {
                Debug.LogError("[Theatre] the genome would not grow: " + e.Message);
                return false;
            }

            using (solo)
            {
                solo.UseTestSine = useSine;

                var min = new Dictionary<string, float>();
                var max = new Dictionary<string, float>();
                bool finite = true;

                int steps = Mathf.CeilToInt(seconds / Evosim.Sim.Ecosystem.FixedDt);
                double jointRate = 0d;

                for (int i = 0; i < steps; i++)
                {
                    solo.Step();
                    jointRate += solo.MeanJointRate();

                    // Every implemented channel at every part, read through ISensorField so a
                    // widened pool needs no change here.
                    foreach (SensorChannel channel in SensorChannels.Implemented)
                    {
                        int indices = channel.IndexCount();

                        for (int part = 0; part < solo.Phenotype.PartCount; part++)
                        {
                            for (int index = 0; index < indices; index++)
                            {
                                float v = ((ISensorField)solo.Sensors).Read(part, channel, index);

                                if (float.IsNaN(v) || float.IsInfinity(v)) finite = false;

                                string key = channel.ToString();
                                if (!min.ContainsKey(key)) { min[key] = v; max[key] = v; }
                                else
                                {
                                    if (v < min[key]) min[key] = v;
                                    if (v > max[key]) max[key] = v;
                                }
                            }
                        }
                    }
                }

                travelled = solo.Travelled;

                var report = new System.Text.StringBuilder();
                report.Append("[Theatre] ").Append(useSine ? "test sine" : "its own brain")
                      .Append(": travelled ").Append(travelled.ToString("0.####", CultureInfo.InvariantCulture))
                      .Append(" m, mean joint rate ")
                      .Append((jointRate / Math.Max(1, steps)).ToString("0.###", CultureInfo.InvariantCulture))
                      .Append(" rad/s, depth ").Append(solo.Depth.ToString("0.##", CultureInfo.InvariantCulture))
                      .Append(" m, ").Append(solo.Phenotype.PartCount).Append(" parts, ")
                      .Append(solo.Instance.TotalDof).Append(" DOF");

                foreach (SensorChannel channel in SensorChannels.Implemented)
                {
                    string key = channel.ToString();
                    if (!min.ContainsKey(key)) continue;

                    report.Append("\n  ").Append(key).Append(": ")
                          .Append(min[key].ToString("0.####", CultureInfo.InvariantCulture))
                          .Append(" .. ")
                          .Append(max[key].ToString("0.####", CultureInfo.InvariantCulture))
                          .Append(max[key] == min[key] ? "   (constant)" : "");
                }

                Debug.Log(report.ToString());

                if (!finite)
                {
                    Debug.LogError("[Theatre] a sensor channel reported a non-finite value.");
                    return false;
                }

                return true;
            }
        }

        private static float Number(string variable, float fallback)
        {
            string text = Environment.GetEnvironmentVariable(variable);
            return !string.IsNullOrEmpty(text) &&
                   float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }
    }
}
