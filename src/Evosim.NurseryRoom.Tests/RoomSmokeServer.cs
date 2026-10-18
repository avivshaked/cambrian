using System;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// The smoke server for operating the screens on the real registry and the real runs (read only): the room on port 5180, never the room's own 5179, with a copy of
    /// room.json that is written beside the stop file (not under rounds/), an activity log beside it, and a launcher that is a fake, so that no operation can start a
    /// process. Skipped unless <c>NURSERY_ROOM_SMOKE_STOP</c> names the file whose appearing stops it.
    /// </summary>
    public class RoomSmokeServer
    {
        [SmokeFact]
        public void ServesTheRealRegistryOnPort5180WithAFakeLauncherUntilTheStopFileAppears()
        {
            string stop = Environment.GetEnvironmentVariable(SmokeFactAttribute.StopVariable);
            string dir = Path.GetDirectoryName(stop);
            string real = File.ReadAllText(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            string copy = real.Replace("\"port\": 5179", "\"port\": 5180");
            Assert.NotEqual(real, copy);
            string settingsFile = Path.Combine(dir, "room-smoke.json");
            File.WriteAllText(settingsFile, copy);
            RoomSettings settings = RoomSettings.Load(settingsFile);
            Assert.Equal(5180, settings.Port);

            var launcher = new FakeLauncher();
            // A registry of its own where one is named (a file beside the stop file, never the main checkout's): entries made up to show a rerunnable one, a long reason and a rerun.
            string registry = Environment.GetEnvironmentVariable("NURSERY_ROOM_SMOKE_REGISTRY");
            var room = new RoomContext(Where.Root, settings, registryFile: string.IsNullOrEmpty(registry) ? null : registry, activityFile: Path.Combine(dir, "activity-smoke.jsonl"), launcher: launcher);
            WebApplication app = Program.Create(room, Where.WwwRoot);
            app.Start();
            using var stopped = new ManualResetEventSlim(File.Exists(stop));
            using var watcher = new FileSystemWatcher(dir, Path.GetFileName(stop)) { EnableRaisingEvents = true };
            watcher.Created += (s, e) => stopped.Set();
            File.WriteAllText(stop + ".ready", "5180");
            stopped.Wait(TimeSpan.FromMinutes(60));
            app.StopAsync().GetAwaiter().GetResult();
        }
    }
}
