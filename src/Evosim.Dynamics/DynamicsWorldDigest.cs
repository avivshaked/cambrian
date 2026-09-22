using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The state digest on disk, in the farm's three files, so <c>scripts/digest-diff.py</c> and
    /// <c>scripts/compare-det.py</c> read a run of this engine the way they read a run of the
    /// other one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The narrowing rule: there is none.</b> The farm's digest hashes the raw bits of
    /// thirteen <c>float</c>s a link; this solver's state is <c>double</c> throughout, and
    /// rounding it to float to match would hash a number the solver never held — two
    /// trajectories that differ in the last bits of a double would collide, which is the one
    /// thing a digest exists not to do. So it hashes the raw bits of thirteen <c>double</c>s a
    /// link, and <c>digest-settings.json</c> says <c>"precision": "double"</c> so that a reader
    /// comparing two files knows before it starts that a Unity digest and one of these can never
    /// be equal and were never meant to be. Within one engine the question the digest answers —
    /// did the thread count change the arithmetic — is unchanged.
    /// </para>
    /// <para>
    /// <b>The field order is the farm's</b>: position, rotation, linear velocity, angular
    /// velocity. <see cref="DynamicsWorld.Digest"/>, the in-memory one the spike's identity tests
    /// read, takes them in its own order (position, rotation, spin, velocity) and hashes four
    /// quantities per link rather than thirteen numbers. The two are different hashes of the same
    /// state and neither is derivable from the other; the file's is the one a script compares
    /// across runs.
    /// </para>
    /// <para>
    /// <b>Flushed each row</b>, for the farm's reason: this file exists to be diffed against
    /// another run's, and a run stopped or wedged part-way through is exactly the case it is read
    /// in.
    /// </para>
    /// </remarks>
    public sealed partial class DynamicsWorld
    {
        /// <summary>Numbers hashed per link — the farm's thirteen, as doubles.</summary>
        private const int DigestValuesPerPart = 13;

        private string _digestDirectory;
        private long _digestEvery;
        private HashSet<long> _digestDumpSteps;
        private JsonlWriter _digest;
        private JsonlWriter _digestBodies;
        private readonly double[] _digestPart = new double[DigestValuesPerPart];
        private readonly byte[] _digestBytes = new byte[DigestValuesPerPart * 8];
        private readonly long[] _digestId = new long[1];
        private readonly byte[] _digestIdBytes = new byte[8];

        /// <summary>
        /// Turns the per-step digest on, writing into an existing run directory.
        /// </summary>
        /// <param name="runDirectory">The run's own directory; the three files go in it.</param>
        /// <param name="everySteps">Physics steps between digests. 0 or less leaves it off.</param>
        /// <param name="dumpSteps">Steps at which to also write a row per living body, or null.</param>
        public void EnableDigest(string runDirectory, long everySteps, IEnumerable<long> dumpSteps)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentException("A run directory is required.", nameof(runDirectory));
            }

            if (everySteps <= 0) return;

            _digestDirectory = runDirectory;
            _digestEvery = everySteps;

            _digestDumpSteps = null;
            var named = new List<long>();

            if (dumpSteps != null)
            {
                foreach (long step in dumpSteps) if (!named.Contains(step)) named.Add(step);
                if (named.Count > 0) _digestDumpSteps = new HashSet<long>(named);
            }

            Directory.CreateDirectory(runDirectory);
            _digest = new JsonlWriter(Path.Combine(runDirectory, "digest.jsonl"), flushEachRow: true);

            // The digest's own settings beside it, so a comparison knows what it can observe
            // without the run log. Not in run.json, whose shape several scripts read, and not in
            // config.json or its hash, which the digest must not touch. `precision` is this
            // engine's addition, and the one field a reader of both engines' files must read.
            File.WriteAllText(
                Path.Combine(runDirectory, "digest-settings.json"),
                "{\"digestEvery\": " + everySteps.ToString(CultureInfo.InvariantCulture) +
                ", \"dumpSteps\": [" +
                string.Join(",", named.ConvertAll(x => x.ToString(CultureInfo.InvariantCulture))) +
                "], \"precision\": \"double\", \"engine\": \"Evosim.Dynamics\"}\n",
                new UTF8Encoding(false));
        }

        /// <summary>Closes the digest files. A killed process loses nothing; this is for a test.</summary>
        public void CloseDigest()
        {
            _digest?.Dispose();
            _digestBodies?.Dispose();
            _digest = null;
            _digestBodies = null;
            _digestDirectory = null;
            _digestEvery = 0;
        }

        /// <summary>One digest row, and the per-body dump on the steps that asked for one.</summary>
        private void WriteDigestRow()
        {
            if (_digest == null) return;

            bool due = Steps == 1 || Steps % _digestEvery == 0;
            bool dump = _digestDumpSteps != null && _digestDumpSteps.Contains(Steps);
            if (!due && !dump) return;

            // FNV-1a 64, over the raw bits of every living body's state in list order — which is
            // ascending id order, the farm's World.Living order.
            ulong hash = 14695981039346656037UL;
            long first = -1;
            int counted = 0;

            for (int i = 0; i < _creatures.Count; i++)
            {
                Creature body = _creatures[i];
                if (body.Links == 0) continue;

                if (first < 0) first = body.Id;
                counted++;

                _digestId[0] = body.Id;
                Buffer.BlockCopy(_digestId, 0, _digestIdBytes, 0, 8);
                hash = Fnv1a(hash, _digestIdBytes, 8);

                for (int b = 0; b < body.Links; b++)
                {
                    ReadPartState(body, b, _digestPart);
                    Buffer.BlockCopy(_digestPart, 0, _digestBytes, 0, DigestValuesPerPart * 8);
                    hash = Fnv1a(hash, _digestBytes, DigestValuesPerPart * 8);
                }
            }

            string hex = hash.ToString("x16", CultureInfo.InvariantCulture);
            long step = Steps;
            double t = step * Config.StepSeconds;
            int bodyCount = counted;
            long firstId = first;

            _digest.WriteRow(w => w
                .Field("step", step)
                .Field("t", t)
                .Field("bodies", bodyCount)
                .Field("hash", hex)
                .Field("first", firstId));

            if (dump) DumpBodies();
        }

        /// <summary>One row per living creature: every number the digest hashed, in the clear.</summary>
        private void DumpBodies()
        {
            if (_digestBodies == null)
            {
                _digestBodies = new JsonlWriter(
                    Path.Combine(_digestDirectory, "digest-bodies.jsonl"), flushEachRow: true);
            }

            for (int i = 0; i < _creatures.Count; i++)
            {
                Creature body = _creatures[i];
                if (body.Links == 0) continue;

                var links = new StringBuilder();
                links.Append('[');
                for (int b = 1; b < body.Links; b++)
                {
                    if (b > 1) links.Append(',');
                    links.Append(PartStateJson(body, b));
                }
                links.Append(']');

                var w = new Json.Writer(indent: false);
                w.BeginObject();
                w.Field("step", Steps);
                w.Field("id", (long)body.Id);
                w.Raw("root", PartStateJson(body, 0));
                w.Raw("links", links.ToString());

                // Nothing sleeps here: this solver steps every living body every step, which is
                // the whole reason PhysX's sleep had to be fought in the farm. False, always, and
                // kept so that a reader of both engines' files does not have to branch.
                w.Field("sleeping", false);

                // The farm writes 0 here, because PhysX reports contacts to a scene-wide callback
                // and there is no cheap per-body count to read. This engine has one: the bodies
                // this body's bounding sphere overlapped on the step just taken, which is 0 when
                // the contact instrument is off.
                w.Field("contacts", body.OverlapCount);
                w.EndObject();

                _digestBodies.Write(w.ToString());
            }
        }

        /// <summary>One link's thirteen numbers, in the order the hash takes them.</summary>
        private static void ReadPartState(Creature body, int link, double[] into)
        {
            into[0] = body.Position[3 * link];
            into[1] = body.Position[3 * link + 1];
            into[2] = body.Position[3 * link + 2];
            into[3] = body.Rotation[4 * link];
            into[4] = body.Rotation[4 * link + 1];
            into[5] = body.Rotation[4 * link + 2];
            into[6] = body.Rotation[4 * link + 3];
            into[7] = body.Velocity[3 * link];
            into[8] = body.Velocity[3 * link + 1];
            into[9] = body.Velocity[3 * link + 2];
            into[10] = body.Spin[3 * link];
            into[11] = body.Spin[3 * link + 1];
            into[12] = body.Spin[3 * link + 2];
        }

        /// <summary>The same thirteen numbers as a JSON array, round-trip formatted.</summary>
        private string PartStateJson(Creature body, int link)
        {
            ReadPartState(body, link, _digestPart);

            var sb = new StringBuilder(320);
            sb.Append('[');

            for (int i = 0; i < DigestValuesPerPart; i++)
            {
                if (i > 0) sb.Append(',');
                double v = _digestPart[i];

                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    sb.Append('"').Append(v.ToString(CultureInfo.InvariantCulture)).Append('"');
                }
                else
                {
                    sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static ulong Fnv1a(ulong hash, byte[] bytes, int count)
        {
            for (int i = 0; i < count; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }
}
