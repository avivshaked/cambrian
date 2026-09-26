namespace Evosim.Core
{
    /// <summary>
    /// One body's genome at the moment it was admitted: what <c>genomes.jsonl.gz</c> writes, once
    /// per body, in record format 2 (<c>logbook/specs/record-and-film-spec.md</c> A1).
    /// </summary>
    /// <remarks>
    /// Beside <see cref="LineageEvent"/> rather than inside it. A lineage row is a few numbers and
    /// is written by both farms; the genome is the five kilobytes the compact record exists to
    /// write once instead of every hundred seconds, and only the console farm asks for it, through
    /// <see cref="World.QueueAdmittedGenomes"/>. A reference to the genome the body carries, not a
    /// copy: a genome does not change in a body's life.
    /// </remarks>
    public readonly struct AdmittedGenome
    {
        public AdmittedGenome(long id, Genome genome)
        {
            Id = id;
            Genome = genome;
        }

        /// <summary>The organism id, the one <c>lineage.jsonl</c> carries.</summary>
        public long Id { get; }

        /// <summary>The body's genome.</summary>
        public Genome Genome { get; }
    }
}
