using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A contact-eating world on the true shape, stepped with hunters and claws pressed against forked victims before
    /// every census, and the digest of everything it writes (D275's switch-off identity, <c>BiteNeedsMouthFarmTests</c>).
    /// </summary>
    /// <remarks>
    /// <b>Written against what the tree held before D275</b> (no mention of the switch), so that this file and a
    /// two-line test that prints <see cref="Digest"/> can be built and run on that tree as it stood, exported with
    /// <c>git archive</c>, as <c>DialOffDigestTests</c>'s pin was. The digest has the shape of that test's: every lineage
    /// row, every positions row, every stats row with its wall-clock columns taken out, the pose stream, the checkpoint
    /// payload at two metabolic steps and the solver's own state digest at the end.
    /// </remarks>
    internal static class TrueShapeBiteScenario
    {
        private static readonly Regex WallClock = new Regex("\"wall[A-Za-z]*\":[^,}]*,?", RegexOptions.Compiled);

        /// <summary>The run directory of the last <see cref="Digest"/>, for a test that reads its rows.</summary>
        internal static string LastRunPath;

        internal static RunConfig Config(Action<RunConfig> adjust)
        {
            RunConfig config = RemainsFixtures.Config(contactEating: true);
            config.ContactPerPart = true;
            config.ContactShape = ContactShapeForm.TrueShape;
            adjust?.Invoke(config);
            return config;
        }

        private static byte[] CheckpointOf(World world, Simulation sim)
        {
            sim.SettleBeforeCheckpoint();

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    sim.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static void Mix(HashAlgorithm sha, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        private static void Mix(HashAlgorithm sha, byte[] bytes) => sha.TransformBlock(bytes, 0, bytes.Length, null, 0);

        /// <summary>Presses each hunter or claw against a victim of two or more parts, in id order.</summary>
        private static void PressAll(World world, Simulation sim)
        {
            var biters = new List<long>();
            var victims = new List<long>();

            foreach (Organism o in world.Living)
            {
                if (o.HasAttack) biters.Add(o.Id);
                else if (o.Phenotype.PartCount >= 2) victims.Add(o.Id);
            }

            for (int i = 0; i < biters.Count && i < victims.Count; i++)
            {
                RemainsFixtures.Press(sim, biters[i], victims[i], 1 + i % 2);
            }
        }

        /// <summary>
        /// The digest of 150 metabolic steps of four forks, two hunters and two claws, with the biters pressed against
        /// the forks before every census from the third step.
        /// </summary>
        internal static string Digest(string name, Action<RunConfig> adjust, out World world, out string summary)
        {
            string scratch = SimulationTests.Scratch(name);
            if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
            Directory.CreateDirectory(scratch);

            RunConfig config = Config(adjust);
            world = new World(config, 1UL);
            var sha = SHA256.Create();
            string poseFile = Path.Combine(scratch, "digest-poses.bin");
            string runPath;

            using (RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow, RunRecordFormat.Compact))
            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
            using (PoseRecorder poses = PoseRecorder.AtPath(poseFile, 0.5f, config.Hash()))
            {
                runPath = dir.Path;
                LastRunPath = runPath;
                world.QueueAdmittedGenomes = dir.Genomes != null;

                world.Inoculate(RemainsFixtures.Fork(), count: 4, heightY: -2.5f);
                world.Inoculate(RemainsFixtures.Hunter(), count: 2, heightY: -2.5f);
                world.Inoculate(RemainsFixtures.Claw(), count: 2, heightY: -2.5f);

                var sampler = new Sampler();
                IReadOnlyList<string> columns = Sampler.Columns(sim.Config);
                int metabolic = 0;

                while (metabolic < 150)
                {
                    if ((sim.Steps + 1) % sim.StepsPerMetabolicStep == 0 && metabolic >= 2) PressAll(world, sim);
                    if (!sim.Step()) continue;

                    metabolic++;

                    if (metabolic % 5 == 0) sampler.Write(sim, dir, columns);
                    if (metabolic % 10 == 0) poses.WriteFrame(sim, world.ElapsedSeconds);

                    if (metabolic == 60 || metabolic == 150)
                    {
                        Mix(sha, "checkpoint " + metabolic);
                        Mix(sha, CheckpointOf(world, sim));
                    }
                }

                Mix(sha, "solver " + sim.Dynamics.Digest());
                sampler.Close();
            }

            int lineage = 0, positions = 0, stats = 0, frames = 0;

            foreach (string row in JsonlWriter.ReadRows(Path.Combine(runPath, "lineage.jsonl")))
            {
                Mix(sha, row);
                lineage++;
            }

            foreach (string row in RecordFiles.PositionLines(runPath, RunRecordFormat.Compact))
            {
                Mix(sha, row);
                positions++;
            }

            string statsFile = Path.Combine(runPath, "stats.jsonl");
            if (File.Exists(statsFile))
            {
                foreach (string row in JsonlWriter.ReadRows(statsFile))
                {
                    Mix(sha, WallClock.Replace(row, string.Empty));
                    stats++;
                }
            }

            using (PoseStreamReader reader = PoseStreamReader.Open(poseFile))
            {
                for (int f = 0; f < reader.Frames.Length; f++)
                {
                    PoseFrame frame = reader.Read(f);
                    Mix(sha, BitConverter.GetBytes(frame.Seconds));
                    frames++;

                    foreach (PoseBody b in frame.Bodies)
                    {
                        Mix(sha, BitConverter.GetBytes(b.Id));
                        Mix(sha, BitConverter.GetBytes(b.Flags));
                        Mix(sha, BitConverter.GetBytes(b.AteMask));
                        foreach (float v in new[] { b.X, b.Y, b.Z, b.Qx, b.Qy, b.Qz, b.Qw, b.BodyFraction, b.ReserveSeconds, b.BreedFraction, b.ReserveJoules })
                        {
                            Mix(sha, BitConverter.GetBytes(v));
                        }

                        foreach (float v in b.Joints) Mix(sha, BitConverter.GetBytes(v));
                    }
                }
            }

            sha.TransformFinalBlock(new byte[0], 0, 0);

            summary = $"{lineage} lineage rows, {positions} positions rows, {stats} stats rows, {frames} pose frames; " +
                      $"parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}, deaths {world.Deaths}, " +
                      $"mouth blows {world.MouthBlows}, kept {world.BiteKeptFromOthersJoules:0.###} + {world.BiteKeptFromLeavesJoules:0.###} J";

            return Convert.ToBase64String(sha.Hash);
        }
    }
}
