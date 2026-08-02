using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Spike
{
    /// <summary>
    /// Runs measurements M1-M6 from spikes/01-articulation-body/README.md and
    /// writes CSV to results/. Pure C#; no Editor dependency, so it can also be
    /// driven from Play mode later.
    /// </summary>
    public static class SpikeHarness
    {
        // Thresholds from README §2 — derived from DESIGN.md §6.4.
        public const float BuildDestroyBudgetMs = 15.0f;
        public const float StepPerCreatureBudgetMsLo = 0.15f;
        public const float StepPerCreatureBudgetMsHi = 0.30f;

        const int DefaultParts = 10;
        const float TileSpacing = 100f;
        const float FixedDt = 0.01f;      // DESIGN.md §5.5 — 1/100 s
        const int EvalSteps = 2000;

        static string _outDir;
        static readonly StringBuilder Summary = new();

        public static void RunAll(string outDir)
        {
            _outDir = outDir;
            Directory.CreateDirectory(_outDir);
            Summary.Clear();

            ConfigurePhysics();

            Line("# Spike 01 — ArticulationBody at scale");
            Line($"Unity {Application.unityVersion}   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Line($"fixedDeltaTime={FixedDt}  solverIterations={Physics.defaultSolverIterations}" +
                 $"  solverVelocityIterations={Physics.defaultSolverVelocityIterations}");
            Line("");

            try { M1_M2_BuildTeardown(); } catch (Exception e) { Fail("M1/M2", e); }
            try { M3_StepScaling(); }      catch (Exception e) { Fail("M3", e); }
            try { M4_TorqueStability(); }  catch (Exception e) { Fail("M4", e); }
            try { M5_Determinism(); }      catch (Exception e) { Fail("M5", e); }
            try { M6_DepthLimit(); }       catch (Exception e) { Fail("M6", e); }

            File.WriteAllText(Path.Combine(_outDir, "FINDINGS.md"), Summary.ToString());
            Debug.Log(Summary.ToString());
        }

        static void ConfigurePhysics()
        {
            Physics.simulationMode = SimulationMode.Script;   // DESIGN.md §6.2
            Time.fixedDeltaTime = FixedDt;
            Physics.IgnoreLayerCollision(ArticulationBuilder.CreatureLayer,
                                         ArticulationBuilder.CreatureLayer, true);
            Physics.gravity = Vector3.zero;                   // neutral buoyancy proxy (§5.2)
        }

        // ── M1 / M2 ──────────────────────────────────────────────────────────
        static void M1_M2_BuildTeardown()
        {
            const int reps = 100;
            var build = new List<double>(reps);
            var tear = new List<double>(reps);
            var sw = new Stopwatch();

            for (int i = 0; i < reps; i++)
            {
                var spec = CreatureSpec.Random(1000 + i, DefaultParts);

                sw.Restart();
                var c = ArticulationBuilder.Build(spec, Vector3.zero);
                sw.Stop();
                build.Add(sw.Elapsed.TotalMilliseconds);

                sw.Restart();
                ArticulationBuilder.Destroy(c);
                sw.Stop();
                tear.Add(sw.Elapsed.TotalMilliseconds);
            }

            WriteCsv("m1-m2-build-teardown.csv", "rep,build_ms,teardown_ms",
                i => $"{i},{build[i].ToString("F4", CultureInfo.InvariantCulture)}," +
                     $"{tear[i].ToString("F4", CultureInfo.InvariantCulture)}", reps);

            double bMed = Median(build), bP95 = Percentile(build, 0.95);
            double tMed = Median(tear), tP95 = Percentile(tear, 0.95);
            double combined = bMed + tMed;

            Line("## M1/M2 — build + teardown");
            Line($"- build   median {bMed:F3} ms   p95 {bP95:F3} ms");
            Line($"- teardown median {tMed:F3} ms   p95 {tP95:F3} ms");
            Line($"- combined median **{combined:F3} ms** (budget {BuildDestroyBudgetMs} ms)");
            Line(combined <= BuildDestroyBudgetMs
                ? "- **PASS** — rebuild-per-evaluation is affordable; pooling not required"
                : "- **FAIL** — pooling required (README §6); check what can be reconfigured without teardown");
            Line("");
        }

        // ── M3 ───────────────────────────────────────────────────────────────
        static void M3_StepScaling()
        {
            int[] counts = { 1, 8, 32, 64, 128 };
            var rows = new List<string>();
            var perCreature = new List<double>();

            Line("## M3 — step cost scaling (THE ARCHITECTURE TEST)");
            Line("All creatures actuated every step. `mean speed` is the awake-check —");
            Line("if it collapses toward zero the bodies are asleep and timings are void.");
            Line("| creatures | ms/step | ms/step/creature | vs linear | mean speed m/s |");
            Line("|---|---|---|---|---|");

            double baseline = 0;

            foreach (int n in counts)
            {
                var built = new List<BuiltCreature>(n);
                var drivers = new List<EffectorDriver>(n);
                var scratches = new List<float[]>(n);
                for (int i = 0; i < n; i++)
                {
                    var spec = CreatureSpec.Random(2000 + i, DefaultParts);
                    int gx = i % 16, gz = i / 16;
                    var c = ArticulationBuilder.Build(spec,
                        new Vector3(gx * TileSpacing, 0, gz * TileSpacing));
                    built.Add(c);
                    drivers.Add(new EffectorDriver(c, spec));
                    scratches.Add(new float[Mathf.Max(1, c.totalDof)]);
                }

                // CRITICAL: creatures must be ACTUATED during measurement.
                // Undriven bodies in zero gravity settle and PhysX puts them to
                // sleep, which makes the whole measurement meaningless — an
                // earlier revision of this harness did exactly that and reported
                // 64 creatures costing less than one driven chain.
                for (int w = 0; w < 20; w++)
                {
                    for (int i = 0; i < n; i++) drivers[i].DriveSine(w * FixedDt, 2f, scratches[i]);
                    Physics.Simulate(FixedDt);
                }

                const int steps = 300;
                var sw = Stopwatch.StartNew();
                for (int s = 0; s < steps; s++)
                {
                    for (int i = 0; i < n; i++)
                        drivers[i].DriveSine((20 + s) * FixedDt, 2f, scratches[i]);
                    Physics.Simulate(FixedDt);
                }
                sw.Stop();

                // Activity proof: if mean body speed is ~0 the bodies are asleep
                // and the timing above is measuring nothing. Report it, don't assume it.
                double speedSum = 0; int bodyCount = 0;
                foreach (var c in built)
                    foreach (var ab in c.bodies) { speedSum += ab.linearVelocity.magnitude; bodyCount++; }
                double meanSpeed = bodyCount > 0 ? speedSum / bodyCount : 0;

                double msPerStep = sw.Elapsed.TotalMilliseconds / steps;
                double msPer = msPerStep / n;
                perCreature.Add(msPer);
                if (n == 1) baseline = msPerStep;

                double linearExpectation = baseline * n;
                double ratio = linearExpectation > 0 ? msPerStep / linearExpectation : 1.0;

                rows.Add($"{n},{msPerStep.ToString("F4", CultureInfo.InvariantCulture)}," +
                         $"{msPer.ToString("F4", CultureInfo.InvariantCulture)}," +
                         $"{ratio.ToString("F3", CultureInfo.InvariantCulture)}," +
                         $"{meanSpeed.ToString("F4", CultureInfo.InvariantCulture)}");
                Line($"| {n} | {msPerStep:F3} | {msPer:F4} | {ratio:F2}× | {meanSpeed:F3} |");

                foreach (var c in built) ArticulationBuilder.Destroy(c);
            }

            WriteCsvRaw("m3-step-scaling.csv",
                "creatures,ms_per_step,ms_per_step_per_creature,vs_linear,mean_speed", rows);

            double at64 = perCreature[3];
            Line("");
            Line($"- at 64 tiled: **{at64:F4} ms/creature/step** " +
                 $"(budget {StepPerCreatureBudgetMsLo}–{StepPerCreatureBudgetMsHi})");
            Line(at64 <= StepPerCreatureBudgetMsHi
                ? "- **PASS** on absolute cost"
                : "- **FAIL** on absolute cost — throughput target in DESIGN.md §6.4 unreachable as specified");

            double scalingRatio = perCreature[3] / perCreature[0];
            Line($"- per-creature cost at 64 vs at 1: **{scalingRatio:F2}×**");
            Line(scalingRatio <= 0.85
                ? "- **PASS** — sub-linear: PhysX is parallelising across solver islands, tiling (§6.3) works"
                : "- **FAIL** — cost is linear or worse: no island parallelism. **Escalate to DOTS, revise §6.**");
            Line("");
        }

        // ── M4 ───────────────────────────────────────────────────────────────
        static void M4_TorqueStability()
        {
            var spec = CreatureSpec.Random(4242, DefaultParts);
            var c = ArticulationBuilder.Build(spec, Vector3.zero);
            var driver = new EffectorDriver(c, spec);
            var scratch = new float[Mathf.Max(1, c.totalDof)];

            bool nan = false, sep = false, vel = false;
            float maxVel = 0f, maxAngVel = 0f, maxSep = 0f;

            for (int s = 0; s < EvalSteps; s++)
            {
                driver.DriveSine(s * FixedDt, 2.0f, scratch);
                Physics.Simulate(FixedDt);

                for (int b = 0; b < c.bodies.Length; b++)
                {
                    var ab = c.bodies[b];
                    var p = ab.transform.position;
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                        float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    { nan = true; break; }

                    float v = ab.linearVelocity.magnitude, av = ab.angularVelocity.magnitude;
                    if (v > maxVel) maxVel = v;
                    if (av > maxAngVel) maxAngVel = av;
                    if (v > 100f || av > 100f) vel = true;

                    int par = spec.parts[b].parentIndex;
                    if (par >= 0)
                    {
                        float d = Vector3.Distance(p, c.bodies[par].transform.position);
                        float expect = spec.parts[b].halfExtents.z * 2f + spec.parts[par].halfExtents.magnitude + 1f;
                        if (d > maxSep) maxSep = d;
                        if (d > expect * 3f) sep = true;
                    }
                }
                if (nan) break;
            }

            ArticulationBuilder.Destroy(c);

            Line("## M4 — torque stability (§4.4 effector conditioning)");
            Line($"- {EvalSteps} steps, 2 Hz full-amplitude sine on all {c.totalDof} DOF");
            Line($"- max linear velocity {maxVel:F2} m/s, max angular {maxAngVel:F2} rad/s, max part separation {maxSep:F2} m");
            Line($"- NaN/Inf: {(nan ? "**YES**" : "no")}   joint separation: {(sep ? "**YES**" : "no")}   velocity blow-up: {(vel ? "**YES**" : "no")}");
            Line(!nan && !sep && !vel
                ? "- **PASS** — articulations hold under the §4.4 scheme"
                : "- **FAIL** — re-open the §4.4 effector decision");
            Line("");
        }

        // ── M5 ───────────────────────────────────────────────────────────────
        static void M5_Determinism()
        {
            const int runs = 10;
            var finals = new List<Vector3>(runs);

            for (int r = 0; r < runs; r++)
            {
                var spec = CreatureSpec.Random(777, DefaultParts);
                var c = ArticulationBuilder.Build(spec, Vector3.zero);
                var driver = new EffectorDriver(c, spec);
                var scratch = new float[Mathf.Max(1, c.totalDof)];

                for (int s = 0; s < 500; s++)
                {
                    driver.DriveSine(s * FixedDt, 1.5f, scratch);
                    Physics.Simulate(FixedDt);
                }

                Vector3 com = Vector3.zero; float m = 0f;
                foreach (var ab in c.bodies) { com += ab.transform.position * ab.mass; m += ab.mass; }
                finals.Add(com / Mathf.Max(m, 1e-6f));
                ArticulationBuilder.Destroy(c);
            }

            float maxDrift = 0f;
            for (int i = 1; i < finals.Count; i++)
                maxDrift = Mathf.Max(maxDrift, Vector3.Distance(finals[0], finals[i]));

            var rows = new List<string>();
            for (int i = 0; i < finals.Count; i++)
                rows.Add($"{i},{finals[i].x:F8},{finals[i].y:F8},{finals[i].z:F8}");
            WriteCsvRaw("m5-determinism.csv", "run,com_x,com_y,com_z", rows);

            Line("## M5 — determinism (same seed, same process)");
            Line($"- {runs} runs × 500 steps; max COM drift **{maxDrift:E3} m**");
            Line(maxDrift < 1e-4f
                ? "- **PASS** — reproducibility claim in §7 holds within a process"
                : "- **FAIL** — §7 must be weakened; `configHash` cannot promise replay fidelity");
            Line("");
        }

        // ── M6 ───────────────────────────────────────────────────────────────
        static void M6_DepthLimit()
        {
            int[] depths = { 2, 4, 8, 16 };
            var rows = new List<string>();

            Line("## M6 — chain depth");
            Line("| depth | build ms | ms/step | max |joint pos| | stable |");
            Line("|---|---|---|---|---|");

            foreach (int d in depths)
            {
                var spec = CreatureSpec.Chain(d);
                var sw = Stopwatch.StartNew();
                var c = ArticulationBuilder.Build(spec, Vector3.zero);
                sw.Stop();
                double buildMs = sw.Elapsed.TotalMilliseconds;

                var driver = new EffectorDriver(c, spec);
                var scratch = new float[Mathf.Max(1, c.totalDof)];

                for (int w = 0; w < 20; w++) Physics.Simulate(FixedDt);

                sw.Restart();
                bool stable = true; float maxJoint = 0f;
                for (int s = 0; s < 500; s++)
                {
                    driver.DriveSine(s * FixedDt, 2f, scratch);
                    Physics.Simulate(FixedDt);
                    foreach (var ab in c.bodies)
                    {
                        if (ab.jointPosition.dofCount > 0)
                            maxJoint = Mathf.Max(maxJoint, Mathf.Abs(ab.jointPosition[0]));
                        var p = ab.transform.position;
                        if (float.IsNaN(p.x) || float.IsInfinity(p.x)) stable = false;
                    }
                }
                sw.Stop();
                double msStep = sw.Elapsed.TotalMilliseconds / 500;

                rows.Add($"{d},{buildMs.ToString("F4", CultureInfo.InvariantCulture)}," +
                         $"{msStep.ToString("F4", CultureInfo.InvariantCulture)}," +
                         $"{maxJoint.ToString("F4", CultureInfo.InvariantCulture)},{stable}");
                Line($"| {d} | {buildMs:F3} | {msStep:F4} | {maxJoint:F3} | {(stable ? "yes" : "**NO**")} |");

                ArticulationBuilder.Destroy(c);
            }

            WriteCsvRaw("m6-depth.csv", "depth,build_ms,ms_per_step,max_joint_pos,stable", rows);
            Line("");
            Line("- DESIGN.md §4.2 caps depth at 8; this shows where solver quality actually degrades.");
            Line("");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        static void Fail(string stage, Exception e)
        {
            Line($"## {stage} — **ERRORED**");
            Line("```");
            Line(e.ToString());
            Line("```");
            Line("");
            Debug.LogError($"[Spike] {stage} threw: {e}");
        }

        static void Line(string s) => Summary.AppendLine(s);

        static double Median(List<double> xs)
        {
            var c = new List<double>(xs); c.Sort();
            return c.Count == 0 ? 0 : c[c.Count / 2];
        }

        static double Percentile(List<double> xs, double p)
        {
            var c = new List<double>(xs); c.Sort();
            if (c.Count == 0) return 0;
            return c[Mathf.Clamp((int)(c.Count * p), 0, c.Count - 1)];
        }

        static void WriteCsv(string name, string header, Func<int, string> row, int count)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            for (int i = 0; i < count; i++) sb.AppendLine(row(i));
            File.WriteAllText(Path.Combine(_outDir, name), sb.ToString());
        }

        static void WriteCsvRaw(string name, string header, List<string> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var r in rows) sb.AppendLine(r);
            File.WriteAllText(Path.Combine(_outDir, name), sb.ToString());
        }
    }
}
