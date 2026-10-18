using System.IO;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// A fact that reads a finished run's own files, which a clone that never ran it does not hold (scratch/ is gitignored). When a file it needs is not
    /// found, in this tree or in any folder above it (a worktree's room finds the main checkout's), the fact is reported as skipped with the files named,
    /// in the run's output: it never passes silently.
    /// </summary>
    public sealed class RealRunFactAttribute : FactAttribute
    {
        /// <summary>Each file as a path under <c>scratch/nursery</c>, forward slashes.</summary>
        public RealRunFactAttribute(params string[] filesUnderScratchNursery)
        {
            foreach (string f in filesUnderScratchNursery)
                if (Find(f) == null) { Skip = "skipped: scratch/nursery/" + f + " is not in this clone or above it, so there is no real run to read"; return; }
        }

        /// <summary>The folder (this tree or above it) that holds <c>scratch/nursery/&lt;file&gt;</c>, or null.</summary>
        public static string Find(string fileUnderScratchNursery)
        {
            for (string d = Where.Root; d != null; d = Path.GetDirectoryName(d))
                if (File.Exists(Path.Combine(d, "scratch", "nursery", fileUnderScratchNursery.Replace('/', Path.DirectorySeparatorChar)))) return d;
            return null;
        }
    }
}
