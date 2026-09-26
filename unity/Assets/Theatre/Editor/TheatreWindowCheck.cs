using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Evosim.Farm;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// The film window player's end-to-end, in edit mode and without a graphics device: a farm
    /// film window opened, every frame shown, and every body the frame holds checked against the
    /// window's own files (<c>logbook/specs/record-and-film-spec.md</c>, B2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is not a picture.</b> A picture needs a graphics device; this entry builds the
    /// same world <c>theatre-snap.ps1 -From window</c> photographs, through the same
    /// <see cref="FilmWindowWorld"/> calls, and prints the label a frame would carry. It is the
    /// whole load path: the stream, the verdict, the genomes, the plans, the development, the
    /// growth scaling, the pose and the view's build, sweep and resize.
    /// </para>
    /// <para>
    /// <b>What it asserts.</b> The label's first line is FARM FILM WINDOW and the verdict's own
    /// word. Every framed body has a genome. A body drawn in a frame stands at the root the stream
    /// recorded for it. A body born inside the window is never drawn before its birth row, and a
    /// dead one never after its death row. The view builds one body per id and removes one per
    /// departure. Pose refusals (a frame whose joints fit no plan the window holds, which the
    /// player expects only on the frame a plan moved) are counted and must stay under one body-frame
    /// in a hundred.
    /// </para>
    /// <para>
    /// <c>EVOSIM_THEATRE_WINDOW</c> names the window and <c>EVOSIM_THEATRE_RUN</c>, optionally, the
    /// run it was filmed from. It writes nothing anywhere.
    /// </para>
    /// </remarks>
    public static class TheatreWindowCheck
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
                Debug.LogError("[WindowCheck] " + e);
            }

            EditorApplication.Exit(code);
        }

        private static int Check()
        {
            string directory = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_WINDOW");

            if (string.IsNullOrWhiteSpace(directory))
            {
                Debug.LogError("[WindowCheck] EVOSIM_THEATRE_WINDOW names no window.");
                return 1;
            }

            string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");
            int failures = 0;

            using (FilmWindowWorld world = FilmWindowWorld.Open(directory, run, out string refusal))
            {
                if (world == null)
                {
                    Debug.LogError("[WindowCheck] refused: " + refusal);
                    return 1;
                }

                FilmWindowReader window = world.Window;
                Debug.Log("[WindowCheck] " + window.Describe());

                if (world.FrameCount == 0)
                {
                    Debug.LogError("[WindowCheck] the window holds no frame.");
                    return 1;
                }

                string word = FilmWindowReader.WordOf(window.Verdict?.Verdict);

                if (world.ProvenanceWord != word)
                {
                    Debug.LogError("[WindowCheck] the word is " + world.ProvenanceWord + " and the verdict's is " + word);
                    failures++;
                }

                if (world.ProvenanceWord == "FAITHFUL" && !(window.Verdict != null && window.Verdict.Verdict == FilmWindow.Faithful))
                {
                    Debug.LogError("[WindowCheck] FAITHFUL is claimed without a faithful verdict.");
                    failures++;
                }

                var ever = new HashSet<long>();
                var previous = new HashSet<long>();
                var current = new HashSet<long>();
                long bodyFrames = 0;
                long refusals = 0;
                long fallbacks = 0;
                long withoutAGenome = 0;
                long departures = 0;
                int changes = 0;
                int misplaced = 0;
                int beforeBirth = 0;
                int afterDeath = 0;
                string firstMisplaced = null;

                var clock = Stopwatch.StartNew();

                for (int f = 0; f < world.FrameCount; f++)
                {
                    world.Show(f);

                    PoseFrame frame = window.ReadFrame(f);
                    var roots = new Dictionary<long, Vector3>(frame.Bodies.Length);
                    foreach (PoseBody body in frame.Bodies) roots[body.Id] = new Vector3(body.X, body.Y, body.Z);

                    refusals += world.PoseRefusals;
                    fallbacks += world.PlanFallbacks;
                    withoutAGenome += world.WithoutAGenome;
                    bodyFrames += frame.Bodies.Length;

                    current.Clear();

                    for (int i = 0; i < world.BodyCount; i++)
                    {
                        long id = world.IdAt(i);
                        current.Add(id);
                        ever.Add(id);

                        Transform root = world.RootOf(id);

                        if (root == null || !roots.TryGetValue(id, out Vector3 recorded) ||
                            (root.position - recorded).sqrMagnitude > 1e-8f)
                        {
                            misplaced++;
                            if (firstMisplaced == null)
                            {
                                firstMisplaced = "creature " + id + " at frame " + f + ": " +
                                                 (root == null ? "no root" : root.position.ToString("F4"));
                            }
                        }

                        if (window.TryBirthOf(id, out FilmWindowEvent birth) && world.Second < birth.Seconds - 1e-6)
                        {
                            beforeBirth++;
                        }

                        if (window.TryDeathOf(id, out FilmWindowEvent death) && world.Second > death.Seconds + 1e-6)
                        {
                            afterDeath++;
                        }
                    }

                    if (f > 0 && !current.SetEquals(previous)) changes++;

                    foreach (long id in previous)
                    {
                        if (!current.Contains(id) && !world.View.Holds(id)) departures++;
                    }

                    previous.Clear();
                    previous.UnionWith(current);

                    if (f == 0 || f == world.FrameCount - 1)
                    {
                        Debug.Log("[WindowCheck] " + world.Describe());
                        Debug.Log("[WindowCheck] label:\n" + world.LabelFor("iso", "look 1"));
                    }
                }

                clock.Stop();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "[WindowCheck] {0} frames in {1:0.00} s ({2:0.#} frames a second shown); {3} " +
                    "body-frames, {4} ids drawn, the set changed on {5} frames, built {6}, removed " +
                    "{7}, resized {8}, rebuilt {9}; {10} refused, {11} on an older plan, {12} " +
                    "without a genome, {13} unreadable",
                    world.FrameCount, clock.Elapsed.TotalSeconds,
                    world.FrameCount / Math.Max(1e-6, clock.Elapsed.TotalSeconds),
                    bodyFrames, ever.Count, changes, world.View.Built, world.View.Removed,
                    world.View.Resized, world.View.Rebuilt, refusals, fallbacks, withoutAGenome,
                    world.Unreadable));

                string label = world.LabelFor("iso", "look 1");

                if (!label.StartsWith("FARM FILM WINDOW · " + word, StringComparison.Ordinal))
                {
                    Debug.LogError("[WindowCheck] the label does not open with the window and its word: " + label);
                    failures++;
                }

                if (withoutAGenome > 0)
                {
                    Debug.LogError("[WindowCheck] " + withoutAGenome + " body-frame(s) with no genome in the window.");
                    failures++;
                }

                if (world.Unreadable > 0)
                {
                    Debug.LogError("[WindowCheck] " + world.Unreadable + " genome(s) this build cannot read or develop.");
                    failures++;
                }

                if (misplaced > 0)
                {
                    Debug.LogError("[WindowCheck] " + misplaced + " drawn body-frame(s) not at their recorded root; first " + firstMisplaced);
                    failures++;
                }

                if (beforeBirth > 0 || afterDeath > 0)
                {
                    Debug.LogError("[WindowCheck] " + beforeBirth + " drawn before their birth and " + afterDeath + " after their death.");
                    failures++;
                }

                // One build per id drawn, and one removal per departure: a body that left the stream
                // for a frame and came back is built twice, which is said and not failed.
                if (world.View.Built != ever.Count || world.View.Removed != departures)
                {
                    Debug.LogWarning(
                        "[WindowCheck] the view built " + world.View.Built + " bodies for " + ever.Count +
                        " ids drawn and removed " + world.View.Removed + " for " + departures + " departures.");
                }

                if (refusals * 100 > bodyFrames)
                {
                    Debug.LogError("[WindowCheck] " + refusals + " of " + bodyFrames + " body-frames were refused, over one in a hundred.");
                    failures++;
                }

                if (window.Verdict == null)
                {
                    Debug.LogWarning("[WindowCheck] the window has no verdict, so it reads UNVERIFIED.");
                }

                Debug.Log(failures == 0 ? "[WindowCheck] PASS" : "[WindowCheck] FAIL (" + failures + ")");
                return failures == 0 ? 0 : 1;
            }
        }
    }
}
