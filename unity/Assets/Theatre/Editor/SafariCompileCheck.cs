using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// A compile check for the safari that also reads a guide and prints the trip it builds,
    /// with no Play mode and no graphics device.
    /// </summary>
    /// <remarks>
    /// Reaching this method at all is the compile check: a batch Editor with a compile error in
    /// any assembly reports <c>could not be found</c> rather than running it. With
    /// <c>EVOSIM_THEATRE_RUN</c> set it then reads the run's guide (or
    /// <c>EVOSIM_THEATRE_SAFARI_GUIDE</c>), its lineage and its checkpoints, and prints every
    /// heuristic's trip, the captions each scene carries and the clade map's note; a guide the
    /// reader refuses prints the refusal and exits 1. Launch with <c>-batchmode -quit
    /// -nographics</c>.
    /// </remarks>
    public static class SafariCompileCheck
    {
        public static void Run()
        {
            int code = 0;
            var log = new StringBuilder("[Theatre] safari compile check: the safari's assemblies compiled and loaded (" +
                                        typeof(SafariDirector).Assembly.GetName().Name + ", " + typeof(TheatreSafari).Assembly.GetName().Name +
                                        "); the Recorder binding is " +
                                        (global::Evosim.Theatre.TheatreSafari.Recorder != null ? "loaded" : "NOT loaded") + ".");

            try
            {
                string run = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_RUN");
                if (!string.IsNullOrWhiteSpace(run))
                {
                    string dir = RunRecord.ResolveRunDirectory(run.Trim()) ?? run.Trim();
                    string path = SafariGuide.Locate(dir, out string why);
                    if (path == null)
                    {
                        log.Append("\n  no guide: ").Append(why);
                        code = 1;
                    }
                    else
                    {
                        SafariGuide guide = SafariGuide.Read(path, out string refusal);
                        if (guide == null)
                        {
                            log.Append("\n  guide REFUSED: ").Append(refusal);
                            code = 1;
                        }
                        else
                        {
                            RunRecord record = RunRecord.Load(dir);
                            var checkpoints = SafariDirector.ReadCheckpoints(dir).Select(c => c.seconds).ToList();
                            SafariClades clades = SafariClades.Read(dir);
                            log.Append("\n  guide ").Append(path).Append(": ").Append(guide.Clades.Count).Append(" clades, ")
                               .Append(guide.Picker.Count).Append(" in the picker")
                               .Append(guide.Ignored.Count > 0 ? "; keys not read: " + string.Join(", ", guide.Ignored) : "")
                               .Append("\n  lineage: ").Append(clades.Note)
                               .Append("\n  checkpoints: ").Append(string.Join(", ", checkpoints.Select(s => s.ToString("0", CultureInfo.InvariantCulture))));

                            foreach (SafariHeuristic h in Enum.GetValues(typeof(SafariHeuristic)))
                            {
                                var scenes = SafariTripBuilder.Build(guide, SafariTripBuilder.Choose(guide, h), checkpoints,
                                    record?.RequestedSeconds ?? 0d);
                                RunConfig config = record?.Config;
                                float depth = config?.WorldDepthMetres ?? 0f;
                                float radius = config != null && config.SharedSpace && config.WorldShape == WorldShape.Tank
                                    ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
                                foreach (SafariScene s in scenes)
                                    SafariCaptions.Fill(s, guide, record?.ArmName, t => SafariDirector.RecordedAlive(record, t), depth, radius);

                                log.Append("\n  ").Append(h).Append(" trip, ").Append(scenes.Count).Append(" scenes:");
                                for (int i = 0; i < scenes.Count; i++)
                                {
                                    if (i > 0 && scenes[i].Station == SafariStation.Portrait && scenes[i - 1].Station == SafariStation.Portrait)
                                    {
                                        log.Append("\n    TWO PORTRAITS IN A ROW at ").Append(i + 1);
                                        code = 1;
                                    }
                                    log.Append("\n    ").Append(scenes[i].Line());
                                    foreach (SafariCaption c in scenes[i].Captions)
                                        log.Append("\n        +").Append(c.Offset.ToString("0.#", CultureInfo.InvariantCulture)).Append(" s: ").Append(c.Text);
                                }
                            }

                            // A story, when one is named: its trip for the arm, its clips' names and
                            // every note the reader made, so a shot list can be read before a frame is.
                            string storyPath = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_STORY");
                            if (!string.IsNullOrWhiteSpace(storyPath))
                            {
                                storyPath = storyPath.Trim().Trim('"');
                                if (!Path.IsPathRooted(storyPath)) storyPath = Path.Combine(BuildIdentity.RepositoryRoot(), storyPath);
                                string arm = Environment.GetEnvironmentVariable("EVOSIM_THEATRE_SAFARI_STORY_RUN");
                                if (string.IsNullOrWhiteSpace(arm)) arm = record?.ArmName ?? new DirectoryInfo(dir).Parent?.Name;
                                arm = arm.Trim();

                                SafariStory story = SafariStory.Read(storyPath, out string storyRefusal);
                                if (story == null)
                                {
                                    log.Append("\n  story REFUSED: ").Append(storyRefusal);
                                    code = 1;
                                }
                                else
                                {
                                    RunConfig config = record?.Config;
                                    float depth = config?.WorldDepthMetres ?? 0f;
                                    float radius = config != null && config.SharedSpace && config.WorldShape == WorldShape.Tank
                                        ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
                                    var scenes = story.Trip(arm, guide, () => clades, checkpoints, record?.RequestedSeconds ?? 0d, out var notes);
                                    foreach (SafariScene s in scenes)
                                        SafariCaptions.Fill(s, guide, arm, t => SafariDirector.RecordedAlive(record, t), depth, radius);

                                    log.Append("\n  story ").Append(storyPath).Append(" ('").Append(story.Title ?? "untitled").Append("', ")
                                       .Append(story.Shots.Count).Append(" scenes over ").Append(string.Join(", ", story.Runs))
                                       .Append("): ").Append(scenes.Count).Append(" for ").Append(arm).Append(':');
                                    foreach (SafariScene s in scenes)
                                    {
                                        log.Append("\n    ").Append(s.Line()).Append("\n        clip ").Append(s.Slug).Append(".mp4");
                                        foreach (SafariCaption c in s.Captions)
                                            log.Append("\n        +").Append(c.Offset.ToString("0.#", CultureInfo.InvariantCulture)).Append(" s for ")
                                               .Append(c.Seconds.ToString("0.#", CultureInfo.InvariantCulture)).Append(" s: ").Append(c.Text);
                                    }
                                    foreach (string note in story.Notes.Concat(notes)) log.Append("\n    note: ").Append(note);
                                    if (scenes.Count == 0) code = 1;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Append("\n  THREW: ").Append(e.GetType().Name).Append(": ").Append(e.Message).Append('\n').Append(e.StackTrace);
                code = 1;
            }

            log.Append("\n[Theatre] safari compile check: ").Append(code == 0 ? "OK" : "FAILED");
            Debug.Log(log.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
