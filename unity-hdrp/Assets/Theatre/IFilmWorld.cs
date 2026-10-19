using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// A world a film is made from: a still's world (<see cref="ITheatreFrame"/>) and the few
    /// things a camera plan asks beyond a still, which is which body is which, where one is
    /// going, and what it holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two worlds answer it.</b> The live world (<see cref="TheatreDynamicsReplay"/>) steps a
    /// farm world in the Editor, a cousin of the run from its restore on. A farm film window
    /// (<see cref="FilmWindowWorld"/>) steps nothing: the farm stepped the world and wrote every
    /// frame, and the window plays them back under its own verdict
    /// (<c>logbook/specs/record-and-film-spec.md</c>, B3). The plans (<see cref="FilmPlans"/>,
    /// <see cref="SafariPlans"/>) and the story's chart ask this interface and never which of the
    /// two they were given.
    /// </para>
    /// <para>
    /// <b>Looking ahead is the one place they differ.</b> The live world does not know its future,
    /// so a body's path is its velocity at the plan's second carried forward in a straight line,
    /// as the plans always took it. A window knows the whole of its span, so a body's path is
    /// where its frames put it (<see cref="DisplacementOf"/>).
    /// </para>
    /// </remarks>
    public interface IFilmWorld : ITheatreFrame
    {
        /// <summary>The world's second on screen.</summary>
        double Second { get; }

        /// <summary>A body's id, by its index in <see cref="ITheatreFrame.PositionOf"/>'s order.</summary>
        long IdAt(int index);

        /// <summary>A body's root velocity at <see cref="Second"/>, m/s, or zero when it is not known.</summary>
        Vector3 VelocityOf(long id);

        /// <summary>
        /// How far a body's root moves from <see cref="Second"/> to that second and
        /// <paramref name="seconds"/> more, m: the velocity carried forward in the live world, the
        /// recorded path in a window, zero when neither knows the body.
        /// </summary>
        Vector3 DisplacementOf(long id, float seconds);

        /// <summary>
        /// A living body's reserve, J, and its children, for a story's account chart. False when the
        /// body is not in the world. The reserve is NaN where it is not recorded, as in a window,
        /// whose children are its birth rows in the window.
        /// </summary>
        bool TryAccount(long id, out double reserve, out int children);
    }
}
