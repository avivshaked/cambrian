using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Evosim.Core;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The run's own snow and the water that carries it, for the underwater look's second batch
    /// (<c>logbook/specs/underwater-look/brief-2.md</c>, items 4 to 6, and the owner's addition that
    /// the snow drift with the water).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The fields.</b> The farm writes the snow every 100 s under <c>&lt;run&gt;/fields/</c>:
    /// <c>NNNNNNNNN.snow-columns.f32</c>, each column's joules, and <c>NNNNNNNNN.snow-floor.f32</c>,
    /// the stock of the column's lowest cell, both in <c>layout.json</c>'s order
    /// (<c>ix * cellsZ + iz</c>, cell centres at <c>(ix + 0.5) * cellMetres</c> from zero, the
    /// world's own frame), and <c>bed.f32</c>, the floor's height per column, once. A column's
    /// density is its joules over its water (its depth times the cell's area). Both are interpolated
    /// in time between the two dumps either side of the second, and handed to the shaders as one
    /// texture: red the density, green the floor stock. Nothing is rescaled per frame.
    /// </para>
    /// <para>
    /// <b>The water.</b> The current is Core's <see cref="CurrentField"/>, built here from a fresh
    /// read of the run's config the way <c>World</c> builds its own (the same box, the same seed's
    /// stream, the same floor and reefs), so the live world's field is never sampled by the theatre.
    /// It is a pure function of a place and a second.
    /// </para>
    /// </remarks>
    public sealed class TheatreRunSnow
    {
        /// <summary>Columns along x and z, and their size in metres.</summary>
        public int CellsX { get; private set; }
        public int CellsZ { get; private set; }
        public float CellMetres { get; private set; }

        /// <summary>True when the run has snow fields this reader could open.</summary>
        public bool HasFields { get; private set; }

        /// <summary>Why there are no fields, or where they came from, for the log.</summary>
        public string FieldSource { get; private set; }

        /// <summary>True when the run's current could be built.</summary>
        public bool HasCurrent { get; private set; }

        /// <summary>What the current is, or why there is none, for the log and the label.</summary>
        public string CurrentSource { get; private set; }

        /// <summary>The run's sink rate for the snow, metres a second (zero when unread).</summary>
        public float SinkMetresPerSecond { get; private set; }

        private string _fields;
        private double[] _seconds;
        private float[] _volume;
        private CurrentField _current;
        private float _depth;
        private float _radius;
        private bool _tank;

        private double _loadedA = double.NaN, _loadedB = double.NaN;
        private float[] _colA, _colB, _floorA, _floorB;

        /// <summary>Opens the snow and the water of the run <paramref name="frame"/> belongs to.</summary>
        public static TheatreRunSnow Open(ITheatreFrame frame)
        {
            var snow = new TheatreRunSnow();
            RunRecord record = frame?.Record;

            if (record == null || string.IsNullOrEmpty(record.Path))
            {
                snow.FieldSource = "no run directory";
                snow.CurrentSource = "no run directory";
                return snow;
            }

            snow.OpenFields(record);
            snow.OpenCurrent(record, frame);
            return snow;
        }

        // ---------------------------------------------------------------- the fields

        private void OpenFields(RunRecord record)
        {
            string fields = Path.Combine(record.Path, "fields");
            string layout = Path.Combine(fields, "layout.json");

            if (!File.Exists(layout))
            {
                FieldSource = "the run has no fields/layout.json";
                return;
            }

            try
            {
                string text = File.ReadAllText(layout);
                Match block = Regex.Match(text, "\"snowColumns\"\\s*:\\s*\\{([^}]*)\\}");

                if (!block.Success)
                {
                    FieldSource = "layout.json names no snowColumns";
                    return;
                }

                CellsX = (int)Number(block.Groups[1].Value, "cellsX");
                CellsZ = (int)Number(block.Groups[1].Value, "cellsZ");
                CellMetres = (float)Number(block.Groups[1].Value, "cellMetres");

                if (CellsX <= 0 || CellsZ <= 0 || !(CellMetres > 0f))
                {
                    FieldSource = "layout.json's snowColumns has no usable shape";
                    return;
                }

                var seconds = new List<double>();

                foreach (string path in Directory.GetFiles(fields, "*.snow-columns.f32"))
                {
                    string stem = Path.GetFileName(path);
                    stem = stem.Substring(0, stem.IndexOf('.'));

                    if (!double.TryParse(stem, NumberStyles.Integer, CultureInfo.InvariantCulture, out double s)) continue;
                    if (!File.Exists(Path.Combine(fields, stem + ".snow-floor.f32"))) continue;

                    seconds.Add(s);
                }

                if (seconds.Count == 0)
                {
                    FieldSource = "fields/ holds no snow-columns with a snow-floor beside it";
                    return;
                }

                seconds.Sort();
                _seconds = seconds.ToArray();
                _fields = fields;

                // Each column's water in cubic metres: its depth under the waterline at zero times
                // the cell's area, from bed.f32 where the floor is shaped and the world's depth
                // where it is flat.
                int columns = CellsX * CellsZ;
                _volume = new float[columns];

                string bedPath = Path.Combine(fields, "bed.f32");
                float[] bed = File.Exists(bedPath) ? ReadFloats(bedPath, columns) : null;
                float flat = record.Config != null ? record.Config.WorldDepthMetres : 0f;
                float area = CellMetres * CellMetres;

                for (int i = 0; i < columns; i++)
                {
                    float depth = bed != null ? -bed[i] : flat;
                    _volume[i] = depth > 0.05f ? depth * area : 0f;
                }

                HasFields = true;
                FieldSource = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} snow dumps from {1:0} to {2:0} s, {3}x{4} columns of {5:0.##} m, floor {6}",
                    _seconds.Length, _seconds[0], _seconds[_seconds.Length - 1], CellsX, CellsZ, CellMetres,
                    bed != null ? "from bed.f32" : "flat at the world's depth");
            }
            catch (Exception e)
            {
                HasFields = false;
                FieldSource = "the fields could not be read: " + e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>
        /// The density (J per cubic metre) and the floor stock (J) of every column at a second,
        /// interpolated between the dumps either side of it and held at the ends; false without
        /// fields. <paramref name="at"/> says which dumps and how far between.
        /// </summary>
        public bool TryAt(double second, float[] density, float[] floor, out string at)
        {
            at = null;
            if (!HasFields || density == null || floor == null) return false;

            int columns = CellsX * CellsZ;
            if (density.Length < columns || floor.Length < columns) return false;

            int index = Array.BinarySearch(_seconds, second);
            double a, b;

            if (index >= 0) { a = b = _seconds[index]; }
            else
            {
                int next = ~index;
                a = _seconds[Math.Max(0, next - 1)];
                b = _seconds[Math.Min(_seconds.Length - 1, next)];
            }

            try
            {
                Load(a, b);
            }
            catch (Exception e)
            {
                at = "unreadable: " + e.Message;
                return false;
            }

            float w = b > a ? (float)Math.Max(0.0, Math.Min(1.0, (second - a) / (b - a))) : 0f;

            for (int i = 0; i < columns; i++)
            {
                float joules = _colA[i] + w * (_colB[i] - _colA[i]);
                density[i] = _volume[i] > 0f ? Math.Max(0f, joules) / _volume[i] : 0f;
                floor[i] = Math.Max(0f, _floorA[i] + w * (_floorB[i] - _floorA[i]));
            }

            at = a == b
                ? string.Format(CultureInfo.InvariantCulture, "the dump at {0:0} s", a)
                : string.Format(CultureInfo.InvariantCulture, "{0:0} s and {1:0} s at {2:0.##}", a, b, w);
            return true;
        }

        private void Load(double a, double b)
        {
            int columns = CellsX * CellsZ;

            if (a != _loadedA)
            {
                if (a == _loadedB) { _colA = _colB; _floorA = _floorB; }
                else
                {
                    _colA = ReadFloats(DumpPath(a, "snow-columns"), columns);
                    _floorA = ReadFloats(DumpPath(a, "snow-floor"), columns);
                }
                _loadedA = a;
            }

            if (b != _loadedB)
            {
                if (b == _loadedA) { _colB = _colA; _floorB = _floorA; }
                else
                {
                    _colB = ReadFloats(DumpPath(b, "snow-columns"), columns);
                    _floorB = ReadFloats(DumpPath(b, "snow-floor"), columns);
                }
                _loadedB = b;
            }
        }

        private string DumpPath(double second, string kind) =>
            Path.Combine(_fields, ((long)Math.Round(second)).ToString("D9", CultureInfo.InvariantCulture) + "." + kind + ".f32");

        private static float[] ReadFloats(string path, int count)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < count * 4)
            {
                throw new InvalidDataException(path + " holds " + bytes.Length / 4 + " floats, not " + count);
            }

            var values = new float[count];
            Buffer.BlockCopy(bytes, 0, values, 0, count * 4);

            if (!BitConverter.IsLittleEndian)
            {
                throw new InvalidDataException("the field dumps are little-endian and this machine is not");
            }

            return values;
        }

        private static double Number(string block, string key)
        {
            Match m = Regex.Match(block, "\"" + key + "\"\\s*:\\s*([-0-9.eE+]+)");
            return m.Success ? double.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture) : double.NaN;
        }

        // ---------------------------------------------------------------- the water

        private void OpenCurrent(RunRecord record, ITheatreFrame frame)
        {
            RunConfig config;

            try
            {
                config = RunDirectory.ReadConfig(record.Path, out _);
            }
            catch (Exception)
            {
                try { config = PictureConfig.Read(record.Path, out _); }
                catch (Exception e)
                {
                    CurrentSource = "the run's config could not be read again (" + e.GetType().Name + ")";
                    return;
                }
            }

            SinkMetresPerSecond = Mathf.Max(0f, config.NutrientSinkMetresPerSecond);
            CurrentField current = config.Current;

            if (current == null || !(current.Speed > 0f))
            {
                CurrentSource = "the run's config has no current, so the snow does not drift";
                return;
            }

            try
            {
                int patches = Math.Max(1, (int)config.HorizontalPatches);
                int across = Math.Max(1, (int)config.PatchesAcross);
                float patchWidth = (float)Math.Sqrt(config.WorldAreaSquareMetres / patches);

                _tank = config.WorldShape == WorldShape.Tank;
                _radius = _tank ? TankGeometry.RadiusFor(config.WorldAreaSquareMetres) : 0f;
                _depth = config.WorldDepthMetres;

                current.SetBox(
                    patchWidth, patches, config.WorldDepthMetres,
                    Rng.SeedFor(record.Seed, World.CurrentFieldIndex), across,
                    config.WorldShape, _radius, frame.Bed);
                current.SetReefs(frame.Reefs);

                // One sample, to find out now rather than mid-film whether this field answers.
                current.VelocityAt(_tank ? _radius : 0.5f * patchWidth, -1f, _tank ? _radius : 0.5f * patchWidth, 0.0);

                _current = current;
                HasCurrent = true;
                CurrentSource = string.Format(
                    CultureInfo.InvariantCulture,
                    "the run's current ({0}, {1:0.###} m/s), sinking at {2:0.####} m/s",
                    _tank ? "tank streams" : current.Mode.ToString(), current.Speed, SinkMetresPerSecond);
            }
            catch (Exception e)
            {
                _current = null;
                HasCurrent = false;
                CurrentSource = "the run's current could not be built (" + e.GetType().Name + ": " + e.Message + "), so the snow does not drift sideways";
            }
        }

        /// <summary>
        /// The water's velocity at a place and second, m/s, in the world's frame, with the place
        /// held inside the water (a camera outside the glass or above the surface reads the water
        /// nearest it). Zero without a current or when the field refuses.
        /// </summary>
        public Vector3 VelocityAt(Vector3 place, double second)
        {
            if (_current == null) return Vector3.zero;

            float y = Mathf.Clamp(place.y, -Mathf.Max(0.5f, _depth - 0.5f), -0.1f);
            float x = place.x, z = place.z;

            if (_tank)
            {
                float dx = x - _radius, dz = z - _radius;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                float most = Mathf.Max(0.5f, _radius - 1f);

                if (r > most)
                {
                    x = _radius + dx * most / r;
                    z = _radius + dz * most / r;
                }
            }

            try
            {
                Float3 v = _current.VelocityAt(x, y, z, second);
                var velocity = new Vector3(v.X, v.Y, v.Z);
                return float.IsNaN(velocity.x + velocity.y + velocity.z) ? Vector3.zero : velocity;
            }
            catch (Exception)
            {
                return Vector3.zero;
            }
        }
    }
}
