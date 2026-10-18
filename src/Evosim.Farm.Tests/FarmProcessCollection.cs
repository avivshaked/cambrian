using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The tests that swap the process's <c>Console.Out</c> or run the farm's <c>Program.Main</c>, one at a
    /// time and apart from every other test.
    /// </summary>
    /// <remarks>
    /// Both are the process's own: a test that swaps the console while another runs the farm in the same
    /// process changes where that farm writes, and xUnit runs test classes in parallel. In one collection with
    /// parallelization off, no swap overlaps a <c>Main</c>, and a farm that exits 1 in the full suite is not
    /// that. Three resume tests did so once in a full Farm run of round 59's step 5, and passed ten times in a
    /// row alone. A class that does either goes here.
    /// </remarks>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class FarmProcessCollection
    {
        public const string Name = "the farm's process";
    }
}
