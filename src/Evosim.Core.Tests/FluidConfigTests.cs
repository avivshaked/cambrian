using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="FluidConfig.Clone"/> — DESIGN.md §5.2, part of the config hash (§7).
    /// </summary>
    public class FluidConfigTests
    {
        private readonly ITestOutputHelper _output;

        public FluidConfigTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void CloneCopiesEveryTunable()
        {
            // Clone() is a hand-written object initializer, and TissueExcessDensity (added at
            // D044) was left out of it while every property survived Hash() and JSON round-trip —
            // the two guards RunConfigTests and RunConfigJsonTests already run for this type.
            // Driven by reflection for the same reason as those: adding a property to FluidConfig
            // without adding it to Clone() must fail here, not silently years later when a cloned
            // config quietly reverts one knob to its default.
            var missed = new List<string>();
            int checkedCount = 0;

            foreach (PropertyInfo p in typeof(FluidConfig).GetProperties())
            {
                if (!p.CanWrite || !p.CanRead) continue;
                if (p.PropertyType != typeof(float) &&
                    p.PropertyType != typeof(int) &&
                    p.PropertyType != typeof(bool)) continue;

                var original = new FluidConfig();
                if (!Nudge(original, p)) continue;
                checkedCount++;

                var cloned = original.Clone();
                object expected = p.GetValue(original);
                object actual = p.GetValue(cloned);

                if (!Equals(expected, actual)) missed.Add(p.Name);
            }

            _output.WriteLine(missed.Count == 0
                ? $"{checkedCount} tunables checked, all survive Clone()"
                : "missing from Clone(): " + string.Join(", ", missed));

            // A property this walk never managed to nudge would pass by never being tested —
            // the same silent hole RunConfigTests guards against with its own checkedCount assert.
            Assert.True(checkedCount > 0, "FluidConfig exposed no nudgeable tunable");
            Assert.Empty(missed);
        }

        /// <summary>Changes a property to something different, or returns false if it cannot.</summary>
        private static bool Nudge(object target, PropertyInfo p)
        {
            if (p.PropertyType == typeof(float))
            {
                return NudgeFloat(target, p);
            }
            else if (p.PropertyType == typeof(int))
            {
                p.SetValue(target, (int)p.GetValue(target) + 3);
            }
            else if (p.PropertyType == typeof(bool))
            {
                p.SetValue(target, !(bool)p.GetValue(target));
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>Moves a float tunable to a different legal value, or reports that it cannot.</summary>
        private static bool NudgeFloat(object target, PropertyInfo p)
        {
            var original = (float)p.GetValue(target);

            for (float delta = 7.5f; delta > 1e-4f; delta *= 0.5f)
            {
                foreach (float candidate in new[] { original + delta, original - delta })
                {
                    try
                    {
                        p.SetValue(target, candidate);
                    }
                    catch (Exception e) when (
                        e is ArgumentOutOfRangeException ||
                        e.InnerException is ArgumentOutOfRangeException)
                    {
                        continue;
                    }

                    // A setter is free to clamp rather than throw, and one that clamped back to
                    // the original would leave this reporting a value that never had a chance to
                    // change.
                    if ((float)p.GetValue(target) != original) return true;
                }
            }

            return false;
        }
    }
}
