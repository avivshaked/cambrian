using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Evosim.Farm;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The state stream's end-to-end, in edit mode and without a graphics device: a run's
    /// <c>poses.bin</c> joined to the nearest snapshot at or before a second, developed, posed and
    /// counted (<c>logbook/specs/state-stream-spec.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is not a picture.</b> A picture needs a graphics device and
    /// <c>theatre-snap.ps1</c> therefore runs the Editor without <c>-nographics</c>. This entry
    /// builds the same world the snapshot tool photographs, through the same
    /// <see cref="SnapshotWorld"/> calls, and prints the label the picture would have been
    /// stamped with. It is what a headless machine can check, and it is the whole load path:
    /// the stream's header, its index, the frame at the second, the join to a snapshot the
    /// second does not sit on, the development, the growth scaling and the pose.
    /// </para>
    /// <para>
    /// <b>It writes nothing into the run.</b> Same rule the reconstruction is built under.
    /// </para>
    /// <para>
    /// <c>EVOSIM_THEATRE_RUN</c> names the run or its arm directory, and
    /// <c>EVOSIM_THEATRE_SNAP_TIMES</c> the second to draw. Both as the snapshot tool takes them,
    /// so one launcher can be pointed at either.
    /// </para>
    /// </remarks>
    public static class TheatreStreamCheck
    {
        public static void Run()
        {
            int code = 1;

            try
            {
                code = Check();
            }
            catch (Exception e)
            {
                Debug.LogError("[StreamCheck] " + e);
            }

            EditorApplication.Exit(code);
        }

        private static int Check()
        {
            string asked = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");

            if (string.IsNullOrWhiteSpace(asked))
            {
                Debug.LogError("[StreamCheck] EVOSIM_THEATRE_RUN names no run.");
                return 1;
            }

            string times = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SNAP_TIMES");

            if (!double.TryParse(
                    times, NumberStyles.Float, CultureInfo.InvariantCulture, out double second))
            {
                Debug.LogError(
                    "[StreamCheck] EVOSIM_THEATRE_SNAP_TIMES is '" + times + "', which is not " +
                    "one second.");

                return 1;
            }

            string directory = SnapshotWorld.Resolve(asked);

            Debug.Log("[StreamCheck] " + directory + " at t=" + second + " s");

            if (!SnapshotWorld.HasStream(directory))
            {
                Debug.LogError(
                    "[StreamCheck] this run recorded no poses.bin, so there is no stream to read.");

                return 1;
            }

            double[] seconds = SnapshotWorld.StreamSeconds(directory);

            Debug.Log(
                "[StreamCheck] the stream holds " + seconds.Length + " frame(s), t=" +
                (seconds.Length > 0 ? seconds[0] + " to " + seconds[seconds.Length - 1] : "none"));

            string snapshot = SnapshotWorld.SnapshotFileAtOrBefore(directory, second);

            Debug.Log(
                "[StreamCheck] the nearest snapshot at or before it is " +
                (snapshot ?? "none") + "; a snapshot at the second itself: " +
                (SnapshotWorld.SnapshotFileAt(directory, second) ?? "none"));

            using (SnapshotWorld world = SnapshotWorld.Open(directory, out string refusal))
            {
                if (world == null)
                {
                    Debug.LogError("[StreamCheck] refused: " + refusal);
                    return 1;
                }

                world.Begin(second);

                // Across calls, as the snapshot tool drives it, so this exercises the same loop.
                int turns = 0;
                while (!world.BuildSome(0.25d) && turns++ < 10000)
                {
                }

                Debug.Log(
                    "[StreamCheck] fromStream=" + world.FromStream +
                    " recordedSize=" + world.RecordedSize +
                    " second=" + world.Second +
                    " snapshotSecond=" + world.SnapshotSecond +
                    " joined=" + world.JoinedCount +
                    " built=" + world.BodyCount +
                    " posed=" + world.PosedCount +
                    " poseRefusals=" + world.PoseRefusals +
                    " withoutAGenome=" + world.WithoutAGenome +
                    " withoutAPosition=" + world.WithoutAPosition +
                    " unreadable=" + world.Unreadable);

                Debug.Log("[StreamCheck] label:\n" + world.LabelFor("side", "dark"));

                if (!world.FromStream)
                {
                    Debug.LogError(
                        "[StreamCheck] the picture was not drawn from the stream, which is the " +
                        "one thing this check exists to establish.");

                    return 1;
                }

                if (world.BodyCount == 0)
                {
                    Debug.LogError("[StreamCheck] no body was built.");
                    return 1;
                }

                if (world.PosedCount != world.BodyCount || world.PoseRefusals != 0)
                {
                    Debug.LogError(
                        "[StreamCheck] " + world.PosedCount + " of " + world.BodyCount +
                        " bodies wear their recorded pose and " + world.PoseRefusals +
                        " pose(s) were refused. Every body in a stream frame has a pose in it.");

                    return 1;
                }

                Debug.Log("[StreamCheck] PASS");
                return 0;
            }
        }
    }
}
