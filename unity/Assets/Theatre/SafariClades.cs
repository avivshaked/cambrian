using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// Which clade every body belongs to: the recording's from <c>lineage.jsonl</c>, and a
    /// cousin's own births worked out as they arrive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk is the guide's</b> (safari-spec.md item 1): a founder with no parent founds a
    /// clade, a birth whose expressed flags (absorptive, jointed, photosynthetic) differ from its
    /// parent's founds one, and a child with its parent's flags belongs to its parent's clade. A
    /// clade is named by its founder's body id, which is how the guide and this map meet. The
    /// director needs it because a restored world is a cousin: a body alive at the checkpoint
    /// keeps its recorded id (item 8), so its clade is the recording's, but a body born after
    /// the restore is the cousin's own, and may even carry an id the recording gave to someone
    /// else.
    /// </para>
    /// <para>
    /// <b>So a lookup is asked with the second of the restore.</b> An id the recording saw born at
    /// or before that second is the recording's body; any other living id is the cousin's, and
    /// its clade is its parent's clade, or its own when its flags differ from its parent's,
    /// worked out from the live organism and cached until the next restore.
    /// </para>
    /// <para>
    /// The file is streamed line by line with a sharing reader (the rule for a live run's
    /// JSONL), and only four fields of a birth row are read: <c>id</c>, <c>p</c>, <c>t</c> and the
    /// three flags. A run of a million births costs a dictionary of a million small entries.
    /// </para>
    /// </remarks>
    public sealed class SafariClades
    {
        private struct Born
        {
            public long Parent;
            public double At;
            public byte Flags;
            public long Clade;
        }

        private readonly Dictionary<long, Born> _recorded = new Dictionary<long, Born>();
        private readonly Dictionary<long, (long clade, byte flags)> _cousin = new Dictionary<long, (long, byte)>();
        private double _restoredAt = double.PositiveInfinity;

        public int Births => _recorded.Count;
        public int Malformed { get; private set; }
        public string Note { get; private set; }

        /// <summary>The flag triple as a byte: 1 absorptive, 2 jointed, 4 photosynthetic.</summary>
        public static byte FlagsOf(bool absorptive, bool jointed, bool photosynthetic) =>
            (byte)((absorptive ? 1 : 0) | (jointed ? 2 : 0) | (photosynthetic ? 4 : 0));

        public static byte FlagsOf(Organism o) =>
            FlagsOf(o.HasAbsorptiveTissue, o.Phenotype != null && o.Phenotype.TotalDof > 0, o.HasPhotosyntheticTissue);

        public static byte FlagsOf(SafariClade c) => FlagsOf(c.Absorptive, c.Jointed, c.Photosynthetic);

        /// <summary>Reads a run's lineage, or returns a map with a note saying why it is empty.</summary>
        public static SafariClades Read(string runDirectory)
        {
            var map = new SafariClades();
            string path = Path.Combine(runDirectory, "lineage.jsonl");

            if (!File.Exists(path))
            {
                map.Note = "no lineage.jsonl in '" + runDirectory + "': no body can be put in a clade";
                return map;
            }

            // Rows arrive in birth order, so a parent's clade is always known before its child's.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length < 8 || line.IndexOf("\"e\":\"b\"", StringComparison.Ordinal) < 0) continue;

                    if (!TryLong(line, "\"id\":", out long id) || !TryLong(line, "\"p\":", out long parent) ||
                        !TryDouble(line, "\"t\":", out double t))
                    {
                        map.Malformed++;
                        continue;
                    }

                    byte flags = FlagsOf(Bit(line, "\"abs\":"), Bit(line, "\"jnt\":"), Bit(line, "\"pho\":"));

                    long clade = id;
                    if (parent >= 0 && map._recorded.TryGetValue(parent, out Born p) && p.Flags == flags) clade = p.Clade;

                    map._recorded[id] = new Born { Parent = parent, At = t, Flags = flags, Clade = clade };
                }
            }

            map.Note = map._recorded.Count.ToString(CultureInfo.InvariantCulture) + " births read" +
                       (map.Malformed > 0 ? ", " + map.Malformed + " malformed rows skipped" : "");
            return map;
        }

        /// <summary>Forgets the cousin's own births: a new restore is a new cousin.</summary>
        public void Restored(double seconds)
        {
            _restoredAt = seconds;
            _cousin.Clear();
        }

        /// <summary>The recorded birth second of an id, or NaN.</summary>
        public double RecordedBirth(long id) => _recorded.TryGetValue(id, out Born b) ? b.At : double.NaN;

        /// <summary>
        /// True when a living id is the recording's own body: born at or before the restore, so
        /// the cousin carried it over from the checkpoint. Anything else is the cousin's, and a
        /// fact about it comes from the live organism, never from the lineage.
        /// </summary>
        public bool IsRecorded(long id) => _recorded.TryGetValue(id, out Born b) && b.At <= _restoredAt + 1e-6;

        /// <summary>The founder's parent as the recording has it, or -1.</summary>
        public long RecordedParent(long id) => _recorded.TryGetValue(id, out Born b) ? b.Parent : -1;

        /// <summary>
        /// A body's birth row as the recording has it, whatever the restore: its parent, second,
        /// flags (<see cref="FlagsOf(bool, bool, bool)"/>) and clade. False when the lineage never
        /// saw the id. A story names a clade by its root's id, and a clade the guide has no card
        /// for is built from this (<see cref="SafariStory"/>).
        /// </summary>
        public bool TryRecorded(long id, out long parent, out double at, out byte flags, out long clade)
        {
            if (_recorded.TryGetValue(id, out Born b))
            {
                parent = b.Parent;
                at = b.At;
                flags = b.Flags;
                clade = b.Clade;
                return true;
            }

            parent = clade = -1;
            at = double.NaN;
            flags = 0;
            return false;
        }

        /// <summary>
        /// A living body's clade: the recording's for a body the recording had by the restore,
        /// the cousin's own otherwise. -1 when neither can say.
        /// </summary>
        public long CladeOf(Organism o, IReadOnlyDictionary<long, Organism> living)
        {
            if (o == null) return -1;
            return CladeOf(o.Id, o, living, 0);
        }

        private long CladeOf(long id, Organism o, IReadOnlyDictionary<long, Organism> living, int depth)
        {
            if (_recorded.TryGetValue(id, out Born b) && b.At <= _restoredAt + 1e-6) return b.Clade;
            if (_cousin.TryGetValue(id, out var known)) return known.clade;
            if (o == null || depth > 64) return -1;

            byte flags = FlagsOf(o);
            long parent = o.ParentId;
            long clade = id;

            if (parent >= 0)
            {
                byte parentFlags;
                long parentClade;

                if (_recorded.TryGetValue(parent, out Born pb) && pb.At <= _restoredAt + 1e-6)
                {
                    parentFlags = pb.Flags;
                    parentClade = pb.Clade;
                }
                else if (_cousin.TryGetValue(parent, out var pc))
                {
                    parentFlags = pc.flags;
                    parentClade = pc.clade;
                }
                else if (living != null && living.TryGetValue(parent, out Organism po))
                {
                    parentClade = CladeOf(parent, po, living, depth + 1);
                    parentFlags = FlagsOf(po);
                }
                else
                {
                    // A cousin parent that has died since without being seen: the child cannot be
                    // placed, and is its own clade rather than a guess at someone else's.
                    parentClade = -1;
                    parentFlags = 255;
                }

                if (parentClade >= 0 && parentFlags == flags) clade = parentClade;
            }

            _cousin[id] = (clade, flags);
            return clade;
        }

        /// <summary>Notes a cousin body seen alive, so its children can be placed after it dies.</summary>
        public void Saw(Organism o, IReadOnlyDictionary<long, Organism> living) => CladeOf(o, living);

        // ---------------------------------------------------------------- a row's fields

        private static bool TryLong(string line, string key, out long value)
        {
            value = 0;
            int i = line.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i += key.Length;
            int j = i;
            if (j < line.Length && line[j] == '-') j++;
            while (j < line.Length && char.IsDigit(line[j])) j++;
            return long.TryParse(line.Substring(i, j - i), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryDouble(string line, string key, out double value)
        {
            value = 0;
            int i = line.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i += key.Length;
            int j = i;
            while (j < line.Length && (char.IsDigit(line[j]) || line[j] == '.' || line[j] == '-' || line[j] == 'E' || line[j] == 'e' || line[j] == '+')) j++;
            return double.TryParse(line.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool Bit(string line, string key) => TryLong(line, key, out long v) && v != 0;
    }
}
