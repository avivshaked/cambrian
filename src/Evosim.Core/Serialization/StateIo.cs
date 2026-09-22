using System.Collections.Generic;
using System.IO;

namespace Evosim.Core
{
    /// <summary>
    /// The primitives every <c>WriteState</c> and <c>ReadState</c> in the project is built from:
    /// raw bits, a length before every run of them, and a tag between the sections so a reader
    /// that has drifted a field finds out at once rather than a megabyte later.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bits, never text.</b> <see cref="BinaryWriter"/> writes a float and a double
    /// little-endian in IEEE-754 bit order, which is what <see cref="System.BitConverter"/> gives
    /// on this machine, so a value written and read back is the same value and not the nearest
    /// one to a decimal rendering of it. The genome is the one exception and it says why where it
    /// is written (<c>logbook/specs/checkpoint-spec.md</c>).
    /// </para>
    /// <para>
    /// <b>A tag is not a checksum.</b> It catches a writer and a reader that disagree about the
    /// shape of a section, which is the fault that actually happens when a field is added to one
    /// and not the other; it says nothing about whether the bytes are intact. The file's own
    /// length trailer is what says that.
    /// </para>
    /// </remarks>
    public static class StateIo
    {
        /// <summary>Writes a section marker. <paramref name="tag"/> is four ASCII characters.</summary>
        public static void Tag(BinaryWriter w, string tag)
        {
            w.Write((byte)tag[0]);
            w.Write((byte)tag[1]);
            w.Write((byte)tag[2]);
            w.Write((byte)tag[3]);
        }

        /// <summary>Reads a section marker and refuses one that is not the expected four.</summary>
        public static void Tag(BinaryReader r, string tag)
        {
            var got = new char[4];
            for (int i = 0; i < 4; i++) got[i] = (char)r.ReadByte();

            string read = new string(got);
            if (read == tag) return;

            throw new InvalidDataException(
                "The checkpoint's section marker reads '" + read + "' where '" + tag +
                "' was expected. A writer and a reader that disagree about the shape of a " +
                "section produce a world nobody simulated, so this is refused here rather than " +
                "carried forward.");
        }

        public static void WriteDoubles(BinaryWriter w, double[] values)
        {
            w.Write(values.Length);
            for (int i = 0; i < values.Length; i++) w.Write(values[i]);
        }

        /// <summary>Reads into an array the caller has already sized. Refuses a length mismatch.</summary>
        public static void ReadDoubles(BinaryReader r, double[] into, string what)
        {
            int length = r.ReadInt32();
            if (length != into.Length)
            {
                throw new InvalidDataException(
                    "The checkpoint holds " + length + " values for " + what + " and this body " +
                    "has " + into.Length + ". A body's arrays are sized once at birth, so a " +
                    "mismatch is a different creature wearing this one's id.");
            }

            for (int i = 0; i < length; i++) into[i] = r.ReadDouble();
        }

        /// <summary>Reads a run of doubles into a fresh array, for a buffer the reader must size.</summary>
        public static double[] ReadDoubles(BinaryReader r)
        {
            var values = new double[r.ReadInt32()];
            for (int i = 0; i < values.Length; i++) values[i] = r.ReadDouble();
            return values;
        }

        public static void WriteFloats(BinaryWriter w, float[] values)
        {
            w.Write(values.Length);
            for (int i = 0; i < values.Length; i++) w.Write(values[i]);
        }

        public static float[] ReadFloats(BinaryReader r)
        {
            var values = new float[r.ReadInt32()];
            for (int i = 0; i < values.Length; i++) values[i] = r.ReadSingle();
            return values;
        }

        public static void ReadFloats(BinaryReader r, float[] into, string what)
        {
            int length = r.ReadInt32();
            if (length != into.Length)
            {
                throw new InvalidDataException(
                    "The checkpoint holds " + length + " values for " + what + " and this " +
                    "buffer is " + into.Length + " long.");
            }

            for (int i = 0; i < length; i++) into[i] = r.ReadSingle();
        }

        public static void WriteLongs(BinaryWriter w, long[] values, int count)
        {
            w.Write(count);
            for (int i = 0; i < count; i++) w.Write(values[i]);
        }

        public static long[] ReadLongs(BinaryReader r)
        {
            var values = new long[r.ReadInt32()];
            for (int i = 0; i < values.Length; i++) values[i] = r.ReadInt64();
            return values;
        }

        public static void WriteDoubleList(BinaryWriter w, List<double> values)
        {
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++) w.Write(values[i]);
        }

        public static void ReadDoubleList(BinaryReader r, List<double> into)
        {
            int count = r.ReadInt32();
            into.Clear();
            for (int i = 0; i < count; i++) into.Add(r.ReadDouble());
        }

        public static void WriteFloatList(BinaryWriter w, List<float> values)
        {
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++) w.Write(values[i]);
        }

        public static void ReadFloatList(BinaryReader r, List<float> into)
        {
            int count = r.ReadInt32();
            into.Clear();
            for (int i = 0; i < count; i++) into.Add(r.ReadSingle());
        }

        public static void WriteBoolList(BinaryWriter w, List<bool> values)
        {
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++) w.Write(values[i]);
        }

        public static void ReadBoolList(BinaryReader r, List<bool> into)
        {
            int count = r.ReadInt32();
            into.Clear();
            for (int i = 0; i < count; i++) into.Add(r.ReadBoolean());
        }

        public static void WriteFloat3(BinaryWriter w, Float3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        public static Float3 ReadFloat3(BinaryReader r) =>
            new Float3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
