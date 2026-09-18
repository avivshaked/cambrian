using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// A world <see cref="SnapshotCamera"/> can photograph: the record it belongs to, the floor
    /// under it, and where the bodies in it stand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the camera stopped taking a replay.</b> Two things can put a world in front of it
    /// now. <see cref="TheatreReplay"/> simulates one, which is the faithful picture and the only
    /// one that can show motion. <see cref="SnapshotWorld"/> draws one from the run's own
    /// <c>snapshots/</c> and <c>positions.jsonl</c> without stepping anything, which is a
    /// reconstruction and says so on every frame. The framing, the fog, the box and the label are
    /// the same work in both cases, and a second copy of that work would drift from this one
    /// silently.
    /// </para>
    /// <para>
    /// <b>It is a reader and nothing else.</b> Nothing behind this interface is stepped, painted
    /// or moved through it: the camera asks where a body is and what it developed into, and that
    /// is the whole of what a still picture needs.
    /// </para>
    /// </remarks>
    public interface ITheatreFrame
    {
        /// <summary>The run this world belongs to: its config, its arm, its hashes.</summary>
        RunRecord Record { get; }

        /// <summary>The shaped floor, or null on a flat bed and on a recording made before D092.</summary>
        BedShape Bed { get; }

        /// <summary>How many bodies are standing in the world.</summary>
        int BodyCount { get; }

        /// <summary>Where a body's centre is, in metres.</summary>
        Vector3 PositionOf(int index);

        /// <summary>What a body developed into, for its reach and its parts' shapes.</summary>
        Phenotype PhenotypeOf(int index);

        /// <summary>Whether a body carries absorptive tissue — the marker's first question.</summary>
        bool AbsorptiveAt(int index);

        /// <summary>Whether a body carries photosynthetic tissue.</summary>
        bool PhotosyntheticAt(int index);

        /// <summary>
        /// The line, or lines, burnt into the picture: which world, when, how many, which way,
        /// and how far what is drawn is from what the run did.
        /// </summary>
        /// <param name="view">The view's name, as the file name carries it.</param>
        /// <param name="look">The grade's token, so a picture says which look made it.</param>
        string LabelFor(string view, string look);
    }
}
