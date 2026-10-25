using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// With contact eating off the farm steps every world bit for bit as before the remains reached the solver
    /// (D220, D221): it writes the same rows, the same pose stream and the same checkpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How the digest was obtained.</b> This file and <c>RemainsFixtures.cs</c> were built and run, unchanged, on a
    /// copy of the tree at commit <c>22a1a36c</c> (the remains in Core, before this build), exported to a folder of
    /// its own with <c>git archive</c>; the digest printed there is <see cref="PinnedDigest"/>. The same two files then
    /// ran on this tree and printed the same digest.
    /// </para>
    /// <para>
    /// <b>What the digest covers.</b> A dial-off world of forked, jointed victims and armed claws, stepped through 150
    /// metabolic steps with a claw pressed against a victim before every census, so that parts are bitten off and
    /// bodies die and are rebuilt. The digest is a hash of: every row of <c>lineage.jsonl</c> and of the positions
    /// record; every stats row with its wall-clock columns taken out (they measure the machine); the pose stream's
    /// frames, body by body; the checkpoint payload (world, then harness) at metabolic steps 60 and 150; and the
    /// solver's own state digest at the end. It is a function of the world and nothing about the machine.
    /// </para>
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class DialOffDigestTests
    {
        private readonly ITestOutputHelper _out;

        public DialOffDigestTests(ITestOutputHelper output) => _out = output;

        /// <summary>The digest taken on the tree before this build (see the remarks).</summary>
        private const string PinnedDigest = "wGvQhSUBzyNyWXwcse328i9SdnA2cR1y0UX9uIEir9Q=";

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

        private static void Mix(HashAlgorithm sha, float value) => Mix(sha, BitConverter.GetBytes(value));

        private static readonly Regex WallClock = new Regex("\"wall[A-Za-z]*\":[^,}]*,?", RegexOptions.Compiled);

        /// <summary>Presses each claw against a victim of two or more parts, in id order.</summary>
        private static void PressAll(World world, Simulation sim)
        {
            var claws = new List<long>();
            var victims = new List<long>();

            foreach (Organism o in world.Living)
            {
                if (o.HasAttack) claws.Add(o.Id);
                else if (o.Phenotype.PartCount >= 2) victims.Add(o.Id);
            }

            for (int i = 0; i < claws.Count && i < victims.Count; i++)
            {
                RemainsFixtures.Press(sim, claws[i], victims[i], 1);
            }
        }

        private static string Digest(string name, out World world, out string summary)
        {
            string scratch = SimulationTests.Scratch(name);
            if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
            Directory.CreateDirectory(scratch);

            RunConfig config = RemainsFixtures.Config(contactEating: false);
            world = new World(config, 1UL);
            var sha = SHA256.Create();
            string poseFile = Path.Combine(scratch, "digest-poses.bin");
            string runPath;
            int rows = 0;

            using (RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow, RunRecordFormat.Compact))
            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
            using (PoseRecorder poses = PoseRecorder.AtPath(poseFile, 0.5f, config.Hash()))
            {
                runPath = dir.Path;
                world.QueueAdmittedGenomes = dir.Genomes != null;

                world.Inoculate(RemainsFixtures.Fork(), count: 4, heightY: -2.5f);
                world.Inoculate(RemainsFixtures.Claw(), count: 4, heightY: -2.5f);

                var sampler = new Sampler();
                IReadOnlyList<string> columns = Sampler.Columns(sim.Config);
                int metabolic = 0;

                while (metabolic < 150)
                {
                    if ((sim.Steps + 1) % sim.StepsPerMetabolicStep == 0 && metabolic >= 2) PressAll(world, sim);
                    if (!sim.Step()) continue;

                    metabolic++;

                    if (metabolic % 5 == 0)
                    {
                        sampler.Write(sim, dir, columns);
                        rows++;
                    }

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

            int lineage = 0, positions = 0, stats = 0, frames = 0, bodies = 0;

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
                        foreach (float v in new[] { b.X, b.Y, b.Z, b.Qx, b.Qy, b.Qz, b.Qw, b.BodyFraction, b.ReserveSeconds, b.BreedFraction, b.ReserveJoules })
                        {
                            Mix(sha, v);
                        }

                        foreach (float v in b.Joints) Mix(sha, v);
                        bodies++;
                    }
                }
            }

            sha.TransformFinalBlock(new byte[0], 0, 0);

            summary = $"{lineage} lineage rows, {positions} positions rows, {stats} stats rows, {frames} pose frames of {bodies} bodies, " +
                      $"{rows} samples; parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}, deaths {world.Deaths}";

            return Convert.ToBase64String(sha.Hash);
        }

        [Fact]
        public void TheDialOffWritesTheRowsThePoseStreamAndTheCheckpointOfATreeWithoutTheRemainsInTheSolver()
        {
            string digest = Digest("dial-off-digest", out World world, out string summary);
            _out.WriteLine("dial-off digest " + digest);
            _out.WriteLine(summary);

            // The scenario has to bite, kill and rebuild, or equal bytes say nothing.
            Assert.True(world.PartsKilled > 0L, "no part was bitten off: the scenario tests nothing");

            Assert.Equal(PinnedDigest, digest);
        }
    }
}
