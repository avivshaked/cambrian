namespace Evosim.Dynamics
{
    /// <summary>
    /// The serial tail of a step: everything that reads across bodies, in one place and in one
    /// order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One call in <see cref="DynamicsWorld.Step"/>, and nothing above it in the parallel
    /// region.</b> The contact instrument's sums, the held-overlap swap and the digest row all
    /// walk the whole population, so all three belong after the last thread has finished and all
    /// three take the list in the same order. Putting them behind one method means a reader of
    /// <c>Step</c> sees one line and a reader of this file sees the whole of what happens between
    /// steps.
    /// </para>
    /// <para>
    /// The throw trace is the exception and is not here: a frame is the body's own state, written
    /// by the body's own thread inside the parallel region, so that a body the solver is about to
    /// lose has its last finite frame recorded before anything looks at it.
    /// </para>
    /// </remarks>
    public sealed partial class DynamicsWorld
    {
        private void AfterStep()
        {
            CloseContactStep();
            WriteDigestRow();
        }
    }
}
