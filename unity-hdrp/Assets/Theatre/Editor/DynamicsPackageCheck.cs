using System;
using System.Globalization;
using Evosim.Core;
using Evosim.Dynamics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The compile-proof for <c>com.evosim.dynamics</c>: does the Editor see the solver package,
    /// and does its code compile under Unity's own Roslyn?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It answers a build question, not a physics one.</b> The digest it prints is whatever a
    /// hundred steps of one random body in still water come to on this machine; nothing compares
    /// it to anything, and no trajectory in the record depends on it. What the entry is for is
    /// that <c>Evosim.Dynamics</c> is resolved as a local package beside <c>Evosim.Core</c>, that
    /// the theatre's two assemblies may reference it, and that everything in it — the spans and
    /// stack allocations in the articulated-body algorithm included — survives a compiler set to
    /// C# 9 and netstandard2.1 rather than the SDK's defaults.
    /// </para>
    /// <para>
    /// <b>It lives beside the theatre and not under <c>Assets/Evosim</c>.</b> <c>simHash</c> is a
    /// digest of every <c>.cs</c> under that root, so a build check placed there would refuse
    /// every recording made before it, which is the fault the theatre itself was moved out for.
    /// </para>
    /// <code>
    /// Start-Process -FilePath $unity -Wait -NoNewWindow -ArgumentList @(
    ///   '-projectPath', "$PWD/unity-w6", '-batchmode', '-quit', '-nographics',
    ///   '-executeMethod', 'Evosim.Theatre.EditorTools.DynamicsPackageCheck.Run',
    ///   '-logFile', "$PWD/scratch/logs/package-a.log")
    /// </code>
    /// </remarks>
    public static class DynamicsPackageCheck
    {
        /// <summary>Steps one body for this long, at the solver's own default step.</summary>
        private const int Steps = 100;

        [MenuItem("Evosim/Theatre/Dynamics Package Check")]
        public static void Run()
        {
            int code = 1;

            try
            {
                // A fixed seed, so the line in the log is the same line on every run of the same
                // build. It is a build check and not a measurement, so the number is a witness
                // that the loop ran and not a result.
                var rng = new Rng(20260922UL);
                Genome genome = GenomeFactory.RandomViable(rng);
                Phenotype adult = Developer.Develop(genome);

                // Still water, no neighbours, no glass: the fewest terms that still exercise the
                // articulated-body pass, the drag panels and the buoyancy.
                var config = new SolverConfig
                {
                    StepSeconds = 0.01,
                    Current = null,
                    CreatureContact = false,
                    TankRadiusMetres = 0,
                };

                var world = new DynamicsWorld(config);
                var body = new Creature(0, adult, config);
                body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);
                world.Add(body);

                for (int i = 0; i < Steps; i++) world.Step();

                if (!body.Alive) throw new InvalidOperationException("the body stopped being finite");

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[DynamicsPackageCheck] ok, digest {0:x16} ({1} parts, {2} dof, {3} steps, {4} links)",
                    world.Digest(), adult.PartCount, body.Dof, world.Steps, world.TotalLinks()));

                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[DynamicsPackageCheck] failed: " + e);
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
