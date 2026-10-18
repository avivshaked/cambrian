using System;
using System.IO;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The world writes Organism.LastLedger for bodies with absorptive tissue only, so a body without it (a leaf trunk and a link limb, say)
    /// summed from LastLedger read 0 J of food, light, work, upkeep, handling and exuded whatever it did (found by the light bench's trial of
    /// 2026-10-07). Such a body is read from its Lifetime ledger's change, in the episode's columns and in the room's step view. Nothing here
    /// swims an episode: the tests read source, as the other loop tests do.
    /// </summary>
    public class LedgerReadTests
    {
        private static string Source(string name)
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string file = Path.Combine(dir.FullName, "src", "Evosim.Nursery", name);
                if (File.Exists(file)) return File.ReadAllText(file).Replace("\r\n", "\n");
            }
            throw new FileNotFoundException("src/Evosim.Nursery/" + name + " was not found above the test program");
        }

        [Fact]
        public void TheEpisodeReadsABodyWithoutAbsorptiveTissueFromItsLifetimeLedger()
        {
            string tank = Source("Tank.cs");
            Assert.Contains("if (o.HasAbsorptiveTissue)\n", tank);
            Assert.Contains("EnergyLedger l = o.LastLedger;", tank);   // a body with absorptive tissue reads as before
            Assert.Contains("EnergyLedger now = o.Lifetime, was = lifetime[i];", tank);
            Assert.Contains("result[i].Light += (double)now.LightIncome - was.LightIncome;", tank);
            Assert.Contains("result[i].Work += (double)now.Work - was.Work;", tank);
            Assert.Contains("lifetime[i] = now;", tank);
        }

        [Fact]
        public void TheStepViewReadsSuchABodyFromItsLifetimeOnceAnEconomyStepBeforeTheWatcher()
        {
            string tank = Source("Tank.cs");
            Assert.Contains("probe?.AfterEconomy();", tank);
            Assert.True(tank.IndexOf("probe?.AfterEconomy();", StringComparison.Ordinal) < tank.IndexOf("watcher.Metabolic(probe);", StringComparison.Ordinal));
            string watcher = Source("TankWatcher.cs");
            Assert.Contains("return o.HasAbsorptiveTissue ? o.LastLedger : _lastLedger[member];", watcher);
        }
    }
}
