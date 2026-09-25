using System.Runtime.CompilerServices;
using Evosim.Core;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// The brain's private state, reached without a change to Core: .NET 8's unsafe accessors,
    /// which bind by name at the first call and throw <c>MissingFieldException</c> or
    /// <c>MissingMethodException</c> naming what a rename in Core took away.
    /// </summary>
    /// <remarks>
    /// <b>Why not a public accessor on <see cref="Brain"/>.</b> Core compiles into Unity as well,
    /// and every byte of it is in <c>coreHash</c>; a member added for this engine alone would move
    /// the hash of every recorded run for a reader the recorded runs never had. The brain's layout
    /// (one group per part, the two output buffers swapped every step, the memory beside them) is
    /// Brain.cs's and is read here exactly as it stands.
    /// </remarks>
    internal static class BrainAccess
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_previous")]
        public static extern ref float[] Previous(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_current")]
        public static extern ref float[] Current(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_memory")]
        public static extern ref float[] Memory(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_neurons")]
        public static extern ref NeuronDef[][] Neurons(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_offset")]
        public static extern ref int[] Offset(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_firstChild")]
        public static extern ref int[] FirstChild(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_parent")]
        public static extern ref int[] Parent(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dofStart")]
        public static extern ref int[] DofStart(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_dofCount")]
        public static extern ref int[] DofCount(Brain brain);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_ElapsedSeconds")]
        public static extern void SetElapsedSeconds(Brain brain, double value);
    }
}
