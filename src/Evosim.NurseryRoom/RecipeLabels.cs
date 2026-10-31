using System;
using System.Collections.Generic;
using System.Linq;

namespace Evosim.NurseryRoom
{
    /// <summary>
    /// The plain-word label of every recipe field the screen lists: the one table, in this one file. The recipe resource carries it, so the screen and an agent
    /// read the same words. A key is a field's path as <c>Recipe.cs</c> names it in what it accepts (<c>search.population</c>; a top-level field has no prefix;
    /// an item of an array is <c>[]</c>, so every exam brain shares one label). A key ending <c>.*</c> covers the entries of a map whose keys are data, not
    /// fields (<c>world.overrides</c>, <c>reward.parameters</c>), and <c>{last}</c> in its label stands for the entry's own name.
    /// A test fails when <c>Recipe.cs</c> accepts a path this table does not label (RecipeLabelTests).
    /// </summary>
    public static class RecipeLabels
    {
        // Worded from Recipe.cs, Tank.cs (what each setting does in the search and the episode) and logbook/specs/nursery-general-build.md, sections 2 and 3.
        private static readonly string[][] Rows =
        {
            // the recipe itself
            new[] { "format", "recipe format version" },
            new[] { "name", "recipe name" },
            new[] { "description", "description" },
            new[] { "seed", "random seed" },
            new[] { "base", "recipe it builds on" },

            // the world
            new[] { "world", "world" },
            new[] { "world.config", "world config file" },
            new[] { "world.configHash", "hash of the world config (checked on load)" },
            new[] { "world.overrides", "world settings changed" },
            new[] { "world.overrides.*", "world setting changed: {last}" },

            // the bodies
            new[] { "bodies", "bodies" },
            new[] { "bodies.source", "where the bodies come from" },
            new[] { "bodies.founders", "founder bodies drawn" },
            new[] { "bodies.trunk", "founders' root cell type" },
            new[] { "bodies.pool", "genomes file the bodies come from" },
            new[] { "bodies.keep", "bodies kept, by their place in the pool" },
            new[] { "bodies.brains", "the pool's own brains kept (absent: a fresh brain each)" },

            // the episode's setup
            new[] { "setup", "episode setup" },
            new[] { "setup.seconds", "episode length (s)" },
            new[] { "setup.dt", "time step (s)" },
            new[] { "setup.tankSize", "creatures per tank" },
            new[] { "setup.floorClosed", "tanks never refilled from the population floor" },
            new[] { "setup.birthsHeld", "births held" },
            new[] { "setup.snow", "snow" },
            new[] { "setup.snow.density", "snow density (J/m3)" },
            new[] { "setup.snow.draw", "how the density is drawn" },
            new[] { "setup.snow.layers", "snow layers" },
            new[] { "setup.snow.islands", "snow in islands" },
            new[] { "setup.snow.islands.wavelength", "island wavelength (m)" },
            new[] { "setup.snow.islands.cover", "share of the bed the islands cover" },
            new[] { "setup.carrion", "carrion" },
            new[] { "setup.carrion.count", "corpses laid" },
            new[] { "setup.carrion.joules", "joules per corpse" },
            new[] { "setup.start", "start" },
            new[] { "setup.start.aboveBed", "start height above the bed (m)" },
            new[] { "setup.purse", "starting purse" },
            new[] { "setup.purse.rule", "purse rule" },
            new[] { "setup.purse.seconds", "purse, seconds of standing cost" },

            // the leaves
            new[] { "leaves", "leaves" },
            new[] { "leaves.pool", "leaf pool file" },
            new[] { "leaves.count", "leaves a tank" },
            new[] { "leaves.spentUnitsPerCubicMetre", "spent matter in the water at the start (units/m3)" },
            new[] { "leaves.patches", "leaves in patches" },
            new[] { "leaves.patches.count", "patches" },
            new[] { "leaves.patches.radius", "patch radius (m)" },
            new[] { "leaves.patches.ring", "radius of the ring the patches lie on (m)" },
            new[] { "leaves.patches.switching", "switching patches (two patches take turns being fed)" },
            new[] { "leaves.patches.switching.*", "switching patches: {last} (s, or degrees for minimumTurn)" },

            // the search
            new[] { "search", "search" },
            new[] { "search.population", "genomes per generation" },
            new[] { "search.draws", "tank draws per generation" },
            new[] { "search.generations", "generations after the first" },
            new[] { "search.elites", "elites kept each generation" },
            new[] { "search.tournament", "tournament size" },
            new[] { "search.perBody", "selection within each body" },
            new[] { "search.evolve", "what evolves" },
            new[] { "search.evolveEnvelope", "the joints' drive genes evolve as well" },
            new[] { "search.crossover", "crossover" },
            new[] { "search.crossover.rate", "share of children made by crossover" },
            new[] { "search.crossover.baseShare", "share of crossovers based on the better parent" },
            new[] { "search.crossover.mutationByDistance", "mutation scaled by how far apart the parents are" },
            new[] { "search.stop", "stop before the cap when the search stalls" },
            new[] { "search.stop.measure", "what the stop watches" },
            new[] { "search.stop.window", "generations averaged" },
            new[] { "search.stop.gain", "least gain that keeps the search going (share)" },
            new[] { "search.stop.over", "generations the gain is measured over" },
            new[] { "search.stop.floor", "earliest generation the search may stop after" },

            // the reward
            new[] { "reward", "reward" },
            new[] { "reward.class", "reward class" },
            new[] { "reward.parameters", "reward parameters" },
            new[] { "reward.parameters.*", "reward parameter: {last}" },

            // the exam
            new[] { "exam", "exam" },
            new[] { "exam.episodes", "exam episodes" },
            new[] { "exam.salt", "number mixed into the exam's seeds" },
            new[] { "exam.pool", "genomes examined: every generation's top and the last elites" },
            new[] { "exam.controls", "exam brains" },
            new[] { "exam.controls[]", "exam brain" },
            new[] { "exam.controls[].kind", "brain kind" },
            new[] { "exam.controls[].channels", "sensor channels knocked out" },
            new[] { "exam.controls[].power", "constant power" },
            new[] { "exam.controls[].mode", "kinesis mode" },
            new[] { "exam.controls[].bias", "kinesis bias" },
            new[] { "exam.controls[].gain", "kinesis gain" },
            new[] { "exam.controls[].period", "time between switches (s)" },
            new[] { "exam.controls[].regrowAfter", "delay before the fed patch regrows what dies (s)" },
            new[] { "exam.controls[].witherOver", "time the fed patch takes to wither at a switch (s)" },
            new[] { "exam.controls[].minimumTurn", "smallest turn from the old patch to the new (degrees)" },
        };

        /// <summary>Every label by path, in the order above. The recipe resource carries it.</summary>
        public static readonly IReadOnlyDictionary<string, string> Table = Rows.ToDictionary(r => r[0], r => r[1], StringComparer.Ordinal);

        /// <summary>A path as the table keys it: array positions as <c>[]</c>, and the heading section's <c>recipe.</c> prefix taken off.</summary>
        public static string Normalize(string path)
        {
            string p = System.Text.RegularExpressions.Regex.Replace(path ?? "", @"\[\d+\]", "[]");
            return p.StartsWith("recipe.", StringComparison.Ordinal) ? p.Substring("recipe.".Length) : p;
        }

        /// <summary>The label for a path, or null when the table has none for it.</summary>
        public static string Of(string path)
        {
            string p = Normalize(path);
            if (Table.TryGetValue(p, out string label)) return label;
            foreach (KeyValuePair<string, string> kv in Table)
            {
                if (!kv.Key.EndsWith(".*", StringComparison.Ordinal)) continue;
                string prefix = kv.Key.Substring(0, kv.Key.Length - 1);
                if (p.StartsWith(prefix, StringComparison.Ordinal) && p.Length > prefix.Length) return kv.Value.Replace("{last}", p.Substring(prefix.Length));
            }
            return null;
        }
    }
}
