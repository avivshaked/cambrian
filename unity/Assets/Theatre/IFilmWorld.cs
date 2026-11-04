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

        /// <summary>
        /// A dead body's remains as the world draws them at <see cref="Second"/> (D220), for a close
        /// shot whose named subject is no longer alive: the death shot's time-lapse opens after the
        /// death, and its subject is then the remains. False when the world draws no remains under
        /// that id at this second, which the live world and the nursery never do.
        /// </summary>
        bool TryRemains(long id, out FilmRemains remains);

        /// <summary>
        /// Where a body not yet born at <see cref="Second"/> will first stand (D279's birth film), for a
        /// close shot whose named subject is born after the shot opens: the birth film opens before
        /// the birth, so the birth cloud forms in frame and the newborn appears inside it. False when
        /// the world holds no birth of that id after this second, which the live world and the nursery
        /// never do.
        /// </summary>
        bool TryUnborn(long id, out FilmUnborn unborn);
    }

    /// <summary>
    /// A body not yet born at the second on screen, as <see cref="IFilmWorld.TryUnborn"/> gives it: the
    /// place its first recorded pose puts it, enough for a close shot to frame that place as it frames a
    /// living body, to hold on it until the body is drawn, and for its log line.
    /// </summary>
    public struct FilmUnborn
    {
        /// <summary>The root at the first frame that holds the body, m: where the view will first draw it.</summary>
        public Vector3 Root;

        /// <summary>
        /// The box the parts of that first pose fill at their recorded size, m, before the newborn's grow-in: the
        /// stand-in a close shot reads in place of the drawn box until the body is drawn.
        /// </summary>
        public Bounds Box;

        /// <summary>How far the parts reach from the root at their recorded size, m, as the crowd measures a living body.</summary>
        public float Reach;

        /// <summary>The second of the body's birth row.</summary>
        public double Born;

        /// <summary>The second of the first frame that holds the body, at or just after <see cref="Born"/>.</summary>
        public double FirstFrame;
    }

    /// <summary>
    /// One body's remains at the second on screen, as <see cref="IFilmWorld.TryRemains"/> gives them:
    /// enough for a close shot to frame them as it frames a living body, and for its log line.
    /// </summary>
    public struct FilmRemains
    {
        /// <summary>The drawn root, m.</summary>
        public Vector3 Root;

        /// <summary>How far the parts still held reach from the root, m.</summary>
        public float Reach;

        /// <summary>The parts still held.</summary>
        public int Parts;

        /// <summary>The second the remains began, the death for a body that is itself the remains.</summary>
        public double Since;

        /// <summary>The second the run removes them: <see cref="Since"/> and the run's remains window.</summary>
        public double Removed;

        /// <summary>The view that draws them, which a portrait reads the drawn box and the root from.</summary>
        public LiveWorldView View;
    }
}
