using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Evosim.Farm
{
    /// <summary>
    /// Reference identity as an equality comparer, for a visited set over objects whose own
    /// <c>Equals</c> is not the question.
    /// </summary>
    /// <remarks>
    /// <c>System.Collections.Generic.ReferenceEqualityComparer</c> is .NET 5 and later, and the
    /// farm is also a Unity local package compiled against netstandard2.1 in the Editor, where the
    /// name does not resolve (the Editor's compile of <see cref="CheckpointFidelity"/> failed on it
    /// on 2026-09-23 and took every theatre check with it). This is the same comparer, written out.
    /// </remarks>
    internal sealed class ByReference : IEqualityComparer<object>
    {
        public static readonly ByReference Instance = new ByReference();

        private ByReference() { }

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
    }
}
