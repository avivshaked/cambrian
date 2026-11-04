using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using Evosim.Core;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre
{
    /// <summary>
    /// A view dial that lets absorptive tissue show how much it is taking in: <b>fullness</b> (dial 1) and,
    /// at dial 2, <b>inflow motes</b> as well (<see cref="TheatreUptakeMotes"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and off means nothing happens.</b> <c>EVOSIM_THEATRE_UPTAKE</c> is 0 unless set;
    /// 1 or more is fullness, 2 is fullness and motes. Every caller tests <see cref="Enabled"/> before it calls
    /// anything here, so with the dial at 0 no object, material, keyword or float operation is added to any
    /// rendering path. The shader's side is compiled only into the <c>_EVO_UPTAKE</c> variant, a
    /// <c>multi_compile</c> keyword this class turns on once and nothing turns off, so with the dial at 0 the
    /// keyword is never touched and the shader is the one it was.
    /// </para>
    /// <para>
    /// <b>The source is the run's own <c>absorptive.jsonl</c></b>: one row per absorptive body every 10 s, with
    /// <c>foodW</c>, the food income of the last half-second step. A body's uptake at a second is the mean of its
    /// <c>foodW</c> over the rows within <see cref="SmoothHalfWidthSeconds"/> of that second, in watts for the whole
    /// body. The look scales it by the 95th percentile of <c>foodW</c> over every row read:
    /// <c>u = min(1, foodW / P95)</c>. It is the body's intake and not a rate per cubic metre of its tissue: that
    /// ratio is the local nutrient density times the clearance, so it would single out a mixotroph with a speck of
    /// absorptive tissue in a rich patch over a one-part feeder taking fifty times the power. Every absorptive part
    /// of a body shows the body's u; it is not divided among the parts by their volume shares. A body with no row in
    /// the window is drawn as it always was. It is a sampled and noisy source (one body reads 0.5 W, then 2.8 W ten
    /// seconds later); the recording of an exact per-frame income is a farm change that has not been made.
    /// </para>
    /// <para>
    /// <b>The mean is a tent, not a box.</b> The rows are ten seconds apart, so a plain mean over ±15 s
    /// changes by a third of the range every ten seconds as a row enters and another leaves: a swell that steps
    /// in a film. Each row is weighted by <c>1 - |dt| / 15</c> instead, so a row enters at nothing and the value
    /// moves continuously. A body whose rows weigh under <see cref="PresenceWeight"/> in all fades its look out
    /// by that share, so a body that appears or ends between rows does not pop. Set <see cref="TentWeights"/>
    /// false for the plain mean.
    /// </para>
    /// <para>
    /// <b>The file is read in a window.</b> Only the rows within 1,500 s of the span being drawn are kept (a
    /// film window declares its span through <see cref="Declare"/>; any other view reads 1,500 s either side of
    /// the second it first shows), found by bisection on the file's bytes since the rows are in time order, and
    /// read with a shared read so a live run's file can be opened. The scale is fixed when the first window is
    /// read and kept when a later window replaces it, so the look does not change scale as a viewer moves on.
    /// </para>
    /// <para>
    /// <b>How a body is told.</b> <see cref="TheatrePalette.Paint"/> calls <see cref="Seen"/> as it paints, which
    /// finds the body's absorptive visuals and remembers them. <see cref="Frame"/>, called from every render
    /// (<see cref="TheatreSkin.PushFrame"/>) and from the runner's late update, sets the second, takes each
    /// remembered body's u and writes it into the visuals' property blocks as <c>_Uptake</c> = (u, swell metres).
    /// The palette reads a block before it writes it, so its repaints keep the value, and the shader does the
    /// rest (<c>TheatreBody.shader</c>, <c>TheatreUptake.hlsl</c>).
    /// </para>
    /// <para>
    /// <b>The census views keep the old look.</b> <see cref="Suspend"/> zeroes every body's look for a census
    /// render, as the underwater look holds itself off for them, so <c>logbook/images/</c> stays comparable.
    /// </para>
    /// </remarks>
    public static class TheatreUptake
    {
        /// <summary>The dial: 0, the default, draws nothing; 1 is fullness; 2 is fullness and motes.</summary>
        public static readonly float Level = TheatreSkin.Dial("EVOSIM_THEATRE_UPTAKE", 0f, 0f, 2f);

        public static readonly bool Enabled = Level >= 0.5f;

        public static readonly bool MotesEnabled = Level >= 1.5f;

        /// <summary>The swell at u = 1, as a fraction of the part's smallest half-extent.</summary>
        public const float SwellFraction = 0.06f;

        /// <summary>Rows within this many seconds of the frame's second are averaged.</summary>
        public const float SmoothHalfWidthSeconds = 15f;

        /// <summary>Weigh the rows by a tent over the window (see the class remarks); false is the plain mean.</summary>
        public const bool TentWeights = true;

        /// <summary>The summed tent weight at which a body's look is whole; below it the look fades out.</summary>
        public const float PresenceWeight = 0.5f;

        /// <summary>Rows are read this far either side of the span drawn, seconds.</summary>
        public const double SpanMarginSeconds = 1500d;

        /// <summary>
        /// One mote stands for this much food energy taken in, millijoules (inflow motes, dial 2). In the
        /// t3 run the median absorptive body takes in 2.1 mW over 20,000 to 24,000 s (1.5 to 3.0 mW over the
        /// run's other spans), so 1.5 mJ a mote makes that body about 1.4 motes a second (1.0 to 2.0).
        /// </summary>
        public const float MilliJoulesPerMote = 1.5f;

        // ---------------------------------------------------------------- the series

        private struct Row
        {
            public double T;
            public float Food;
        }

        private sealed class Series
        {
            public Row[] Rows;
        }

        // Everything below is made on first use with the dial on; with it off none of it exists.
        private static Dictionary<long, Series> _series;
        private static string _path;
        private static string _unavailable;
        private static double _lo = double.NaN;
        private static double _hi = double.NaN;
        private static float _p95 = float.NaN;
        private static string _declaredPath;
        private static double _declaredFirst = double.NaN;
        private static double _declaredLast = double.NaN;

        /// <summary>The second last given to <see cref="Frame"/>, and the continuous clock the motes run on.</summary>
        private static double _second = double.NaN;
        private static double _clock = double.NaN;
        private static double _lastSecond = double.NaN;
        private static float _lastWall;

        private static HashSet<string> _said;

        /// <summary>
        /// The shader keyword the uptake code in <c>TheatreBody.shader</c> is compiled under. Turned on once, the
        /// first time anything here runs with the dial on, and never turned off; with the dial at 0 it is never
        /// touched, so the shader every body is drawn with is the one it was.
        /// </summary>
        public const string Keyword = "_EVO_UPTAKE";

        private static bool _keywordOn;

        private static void EnsureKeyword()
        {
            if (_keywordOn) return;

            _keywordOn = true;
            Shader.EnableKeyword(Keyword);
        }

        // ---------------------------------------------------------------- the bodies

        private sealed class Entry
        {
            public Transform Root;
            public Phenotype Phenotype;

            /// <summary>The absorptive visuals, and per visual the swell in metres at u = 1.</summary>
            public MeshRenderer[] Renderers;
            public float[] Swell;

            /// <summary>Per absorptive part: its object, index, half-extents and share of the body's absorptive volume.</summary>
            public Transform[] PartRoots;
            public int[] PartIndex;
            public Float3[] Half;
            public float[] Share;

            public float U = -1f;
            public float Scale = -1f;
        }

        private static Dictionary<long, Entry> _entries;
        private static List<long> _gone;
        private static MaterialPropertyBlock _block;
        private static int _uptakeId;

        private static int UptakeId => _uptakeId != 0 ? _uptakeId : (_uptakeId = Shader.PropertyToID("_Uptake"));

        /// <summary>The 95th percentile of <c>foodW</c> over the rows read, W for the whole body; NaN before any.</summary>
        public static float P95 => _p95;

        // ---------------------------------------------------------------- the hooks

        /// <summary>
        /// Tells the dial which seconds a film window spans, so its first load reads the span and 1,500 s either
        /// side of it (<c>FilmWindowWorld.Open</c>).
        /// </summary>
        public static void Declare(string runPath, double first, double last)
        {
            if (!Enabled || string.IsNullOrEmpty(runPath)) return;
            if (double.IsNaN(first) || double.IsNaN(last) || last < first) return;

            _declaredPath = runPath;
            _declaredFirst = first;
            _declaredLast = last;
        }

        /// <summary>
        /// Said by a view that has no run to read (the solo creature): nothing is drawn, and the log says why once.
        /// </summary>
        public static void NoRun(string why)
        {
            if (!Enabled) return;

            Say("norun:" + why, "[Theatre] uptake: nothing drawn: " + why + ".");
        }

        /// <summary>
        /// Called by the palette as it paints a body: registers the body's absorptive visuals, or forgets the
        /// body when it is not to be drawn with the look (a corpse, the plain look, the raw shapes).
        /// </summary>
        public static void Seen(long id, Transform root, Phenotype phenotype, bool hidden)
        {
            if (root == null || phenotype == null) return;

            EnsureKeyword();

            if (_entries == null) _entries = new Dictionary<long, Entry>();

            _entries.TryGetValue(id, out Entry entry);

            if (hidden)
            {
                if (entry != null)
                {
                    if (entry.Root != null && entry.U != 0f) Write(entry, 0f, 1f);
                    _entries.Remove(id);
                }

                return;
            }

            if (entry != null && entry.Root == root && ReferenceEquals(entry.Phenotype, phenotype)) return;

            Entry built = Build(root, phenotype);

            if (built == null)
            {
                if (entry != null) _entries.Remove(id);
                return;
            }

            _entries[id] = built;
        }

        /// <summary>
        /// Called with every rendered frame: sets the world second the frame shows, gives each remembered body
        /// its u, and moves the motes. Cheap when nothing has changed.
        /// </summary>
        /// <param name="frame">The world on screen: its record names the run, and it knows its second.</param>
        /// <param name="eye">Where the camera is, for the motes (only bodies near it get any).</param>
        /// <param name="filmSecond">
        /// The film's own continuous second, when the caller has one; the motes run on it (a world's second moves
        /// in half-second steps).
        /// </param>
        public static void Frame(ITheatreFrame frame, Vector3 eye, double filmSecond = double.NaN)
        {
            if (!Enabled || frame == null) return;

            EnsureKeyword();

            string path = frame.Record != null ? frame.Record.Path : null;

            if (string.IsNullOrEmpty(path))
            {
                NoRun("this view names no run directory");
                return;
            }

            if (!TheatreDaylight.TrySecond(frame, out double second))
            {
                NoRun("this view has no world second");
                return;
            }

            if (frame is TheatreDynamicsReplay live && !live.Faithful)
            {
                Say("cousin:" + path,
                    "[Theatre] uptake: this live world is a cousin of the recording; the recorded uptake is keyed " +
                    "by creature id and may describe a different body than the one drawn.");
            }

            bool ready = Prepare(path, second);

            _second = second;
            _clock = ContinuousClock(second, filmSecond);

            Refresh(ready);

            if (MotesEnabled)
            {
                if (ready) StepMotes(eye);
                else TheatreUptakeMotes.Hide();
            }
        }

        /// <summary>
        /// For a census render: every body goes back to the old look, and the motes are put away. The next
        /// <see cref="Frame"/> brings them back.
        /// </summary>
        public static void Suspend()
        {
            if (!Enabled) return;

            if (_entries != null)
            {
                foreach (KeyValuePair<long, Entry> pair in _entries)
                {
                    Entry entry = pair.Value;
                    if (entry.Root != null && entry.U != 0f) Write(entry, 0f, 1f);
                }
            }

            TheatreUptakeMotes.Hide();
        }

        // ---------------------------------------------------------------- the rate

        /// <summary>A body's smoothed uptake at a second; false when the recording has no row for it in the window.</summary>
        private static bool Sample(long id, double second, out float foodW, out float presence)
        {
            foodW = 0f;
            presence = 0f;

            if (_series == null || !_series.TryGetValue(id, out Series series)) return false;

            Row[] rows = series.Rows;
            double from = second - SmoothHalfWidthSeconds;
            double to = second + SmoothHalfWidthSeconds;

            int i = LowerBound(rows, from);
            double weights = 0d, food = 0d;

            for (; i < rows.Length && rows[i].T <= to; i++)
            {
                double w = TentWeights ? 1d - Math.Abs(rows[i].T - second) / SmoothHalfWidthSeconds : 1d;
                if (w <= 0d) continue;

                weights += w;
                food += w * rows[i].Food;
            }

            if (weights <= 0d) return false;

            foodW = (float)(food / weights);
            presence = TentWeights ? (float)Math.Min(1d, weights / PresenceWeight) : 1f;
            return true;
        }

        private static int LowerBound(Row[] rows, double t)
        {
            int lo = 0, hi = rows.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (rows[mid].T < t) lo = mid + 1;
                else hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// Motes a second this body makes at a second: its smoothed food income in milliwatts over
        /// <see cref="MilliJoulesPerMote"/>, faded by the presence of its rows. Zero for a body with no row.
        /// </summary>
        internal static float MoteRate(long id, double second)
        {
            if (!Sample(id, second, out float foodW, out float presence)) return 0f;

            return foodW * 1000f / MilliJoulesPerMote * presence;
        }

        // ---------------------------------------------------------------- writing the look

        private static void Refresh(bool ready)
        {
            if (_entries == null || _entries.Count == 0) return;

            if (_block == null) _block = new MaterialPropertyBlock();
            if (_gone == null) _gone = new List<long>();

            _gone.Clear();

            foreach (KeyValuePair<long, Entry> pair in _entries)
            {
                Entry entry = pair.Value;

                if (entry.Root == null)
                {
                    _gone.Add(pair.Key);
                    continue;
                }

                float u = 0f;

                if (ready && _p95 > 0f && Sample(pair.Key, _second, out float foodW, out float presence))
                {
                    u = Mathf.Clamp01(foodW / _p95) * presence;
                }

                // Steps of 1/256 of the range: a value that is drifting is not rewritten every frame, and a
                // step this small is under a hundredth of the swell and of the colour move.
                u = Mathf.Round(u * 256f) / 256f;

                float scale = entry.Root.lossyScale.x;

                if (u == entry.U && Mathf.Abs(scale - entry.Scale) < 1f / 512f) continue;

                Write(entry, u, scale);
            }

            for (int i = 0; i < _gone.Count; i++) _entries.Remove(_gone[i]);
        }

        private static void Write(Entry entry, float u, float scale)
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                MeshRenderer renderer = entry.Renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetVector(UptakeId, new Vector4(u, entry.Swell[i] * u * scale, 0f, 0f));
                renderer.SetPropertyBlock(_block);
            }

            entry.U = u;
            entry.Scale = scale;
        }

        // ---------------------------------------------------------------- finding a body's absorptive visuals

        private static Entry Build(Transform root, Phenotype phenotype)
        {
            int parts = phenotype.PartCount;
            float volume = 0f;
            int absorptive = 0;

            for (int p = 0; p < parts; p++)
            {
                if (phenotype.Parts[p].CellTypeId != CellTypeIds.Absorptive) continue;

                absorptive++;
                volume += phenotype.Parts[p].Volume;
            }

            if (absorptive == 0) return null;

            var entry = new Entry
            {
                Root = root,
                Phenotype = phenotype,
                PartRoots = new Transform[absorptive],
                PartIndex = new int[absorptive],
                Half = new Float3[absorptive],
                Share = new float[absorptive],
            };

            int slot = 0;

            for (int p = 0; p < parts; p++)
            {
                PhenotypePart part = phenotype.Parts[p];
                if (part.CellTypeId != CellTypeIds.Absorptive) continue;

                entry.PartIndex[slot] = p;
                entry.Half[slot] = part.HalfExtents;
                entry.Share[slot] = volume > 0f ? part.Volume / volume : 1f / absorptive;
                slot++;
            }

            var found = new List<MeshRenderer>();
            var swell = new List<float>();
            root.GetComponentsInChildren(true, found);

            var kept = new List<MeshRenderer>(found.Count);

            for (int i = 0; i < found.Count; i++)
            {
                MeshRenderer renderer = found[i];
                if (renderer == null || IsFurniture(renderer.gameObject.name)) continue;

                Transform host = renderer.transform.parent;
                int index = host != null ? PartIndexOf(host.name) : -1;
                if (index < 0 || index >= parts) continue;

                int k = Array.IndexOf(entry.PartIndex, index);
                if (k < 0) continue;

                // A part's visuals all take the swell of the part's own smallest half-extent, so a capsule's
                // shaft and its two caps swell alike and no seam opens between them.
                Float3 h = entry.Half[k];
                float smallest = Mathf.Min(Mathf.Abs(h.X), Mathf.Min(Mathf.Abs(h.Y), Mathf.Abs(h.Z)));

                kept.Add(renderer);
                swell.Add(SwellFraction * smallest);
                entry.PartRoots[k] = host;
            }

            if (kept.Count == 0) return null;

            entry.Renderers = kept.ToArray();
            entry.Swell = swell.ToArray();
            return entry;
        }

        // The mouth mark, the palette's necks: renderers that hang off a part and are not its tissue.
        private static bool IsFurniture(string name) =>
            name == TheatreMouthMarks.MarkName ||
            name.StartsWith("Knuckle", StringComparison.Ordinal) ||
            name.StartsWith("Bridge", StringComparison.Ordinal);

        // "Part07_n2" -> 7; anything else -> -1.
        private static int PartIndexOf(string name)
        {
            if (name == null || !name.StartsWith("Part", StringComparison.Ordinal)) return -1;

            int end = name.IndexOf('_');
            if (end < 0) end = name.Length;

            return int.TryParse(
                name.Substring(4, end - 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                ? index
                : -1;
        }

        // ---------------------------------------------------------------- the motes

        private const float MoteRangeMetres = 12f;

        private static void StepMotes(Vector3 eye)
        {
            TheatreUptakeMotes.Begin();

            if (_entries != null)
            {
                float range2 = MoteRangeMetres * MoteRangeMetres;

                foreach (KeyValuePair<long, Entry> pair in _entries)
                {
                    Entry entry = pair.Value;
                    if (entry.Root == null || !entry.Root.gameObject.activeInHierarchy) continue;
                    if ((entry.Root.position - eye).sqrMagnitude > range2) continue;

                    for (int k = 0; k < entry.PartIndex.Length; k++)
                    {
                        if (entry.PartRoots[k] == null) continue;

                        TheatreUptakeMotes.Part(
                            pair.Key, entry.PartIndex[k], entry.PartRoots[k], entry.Half[k], entry.Share[k], _clock);
                    }
                }
            }

            TheatreUptakeMotes.End();
        }

        // The motes' clock: the film's own second when it gives one; otherwise the world's second, carried on
        // by the wall clock between its half-second steps (a live world's second moves in steps, and five
        // frames on one instant would hold the motes still and then jump them).
        private static double ContinuousClock(double second, double filmSecond)
        {
            if (!double.IsNaN(filmSecond) && !double.IsInfinity(filmSecond)) return filmSecond;

            float wall = Time.unscaledTime;

            if (second != _lastSecond)
            {
                _lastSecond = second;
                _lastWall = wall;
                return second;
            }

            return second + Math.Min(0.5d, wall - _lastWall);
        }

        // ---------------------------------------------------------------- reading the file

        /// <summary>
        /// Makes the series cover <paramref name="second"/> for the run at <paramref name="path"/>. False when
        /// the run has no usable file, in which case nothing is drawn and the log has said why.
        /// </summary>
        private static bool Prepare(string path, double second)
        {
            if (!string.Equals(path, _path, StringComparison.Ordinal))
            {
                _path = path;
                _series = null;
                _unavailable = null;
                _lo = double.NaN;
                _hi = double.NaN;
                _p95 = float.NaN;
            }

            if (_unavailable != null) return false;

            if (_series == null || second < _lo || second > _hi)
            {
                double lo = second - SpanMarginSeconds;
                double hi = second + SpanMarginSeconds;

                if (string.Equals(path, _declaredPath, StringComparison.Ordinal) &&
                    second >= _declaredFirst && second <= _declaredLast)
                {
                    lo = _declaredFirst - SpanMarginSeconds;
                    hi = _declaredLast + SpanMarginSeconds;
                }

                Load(path, lo, hi);
            }

            return _unavailable == null && _series != null && _series.Count > 0 && _p95 > 0f;
        }

        private static void Load(string path, double lo, double hi)
        {
            string plain = Path.Combine(path, "absorptive.jsonl");
            string zipped = plain + ".gz";
            bool gz = !File.Exists(plain) && File.Exists(zipped);
            string file = gz ? zipped : plain;

            if (!File.Exists(file))
            {
                _unavailable = "no absorptive.jsonl (or .gz) in " + path;
                Say("nofile:" + path, "[Theatre] uptake: nothing drawn: " + _unavailable + ".");
                return;
            }

            var clock = Stopwatch.StartNew();
            var perBody = new Dictionary<long, List<Row>>();
            var incomes = new List<float>();
            int rows = 0;

            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16))
                {
                    Stream source = stream;
                    bool seeked = false;

                    if (gz) source = new GZipStream(stream, CompressionMode.Decompress);
                    else seeked = SeekNear(stream, lo - 300d);

                    using (var reader = new StreamReader(source, Encoding.UTF8, false, 1 << 16))
                    {
                        // A seek lands inside a row, so the first line read is a fragment.
                        if (seeked) reader.ReadLine();

                        string line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!TryNumber(line, "\"t\":", out double t)) continue;
                            if (t < lo) continue;
                            if (t > hi + 300d) break;
                            if (t > hi) continue;

                            // The rows that count the bodies a capped sample left out carry no id.
                            if (!TryNumber(line, "\"id\":", out double idValue)) continue;
                            if (!TryNumber(line, "\"foodW\":", out double food)) continue;
                            if (line.IndexOf("\"dead\":true", StringComparison.Ordinal) >= 0) continue;
                            if (!(food >= 0d) || double.IsInfinity(food)) continue;

                            long id = (long)idValue;

                            if (!perBody.TryGetValue(id, out List<Row> list))
                            {
                                list = new List<Row>();
                                perBody[id] = list;
                            }

                            list.Add(new Row { T = t, Food = (float)food });
                            incomes.Add((float)food);
                            rows++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _unavailable = "absorptive.jsonl in " + path + " cannot be read: " + e.GetType().Name + ": " + e.Message;
                Say("readfail:" + path, "[Theatre] uptake: nothing drawn: " + _unavailable + ".");
                return;
            }

            var series = new Dictionary<long, Series>(perBody.Count);

            foreach (KeyValuePair<long, List<Row>> pair in perBody)
            {
                List<Row> list = pair.Value;

                // Rows are in time order; a resumed run's overlap is the one thing that can break it.
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].T >= list[i - 1].T) continue;

                    list.Sort((a, b) => a.T.CompareTo(b.T));
                    break;
                }

                series[pair.Key] = new Series { Rows = list.ToArray() };
            }

            _series = series;
            _lo = lo;
            _hi = hi;

            if (rows == 0)
            {
                Say("norows:" + path + ":" + (long)lo,
                    string.Format(CultureInfo.InvariantCulture,
                        "[Theatre] uptake: no absorptive rows between {0:0} s and {1:0} s of {2}; bodies there are drawn as always.",
                        lo, hi, path));
                return;
            }

            incomes.Sort();
            float windowP95 = incomes[(int)(0.95 * (incomes.Count - 1))];

            if (float.IsNaN(_p95))
            {
                _p95 = windowP95;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] uptake: P95 {0:0.####} W of food income per body over {1:N0} rows of {2:N0} bodies, " +
                    "t {3:0} to {4:0} s of {5} ({6:0.0} s to read); u = min(1, foodW / P95), foodW smoothed over +-{7:0} s{8}.",
                    _p95, rows, series.Count, lo, hi, path, clock.Elapsed.TotalSeconds, SmoothHalfWidthSeconds,
                    TentWeights ? " (tent-weighted)" : ""));
            }
            else
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] uptake: read {0:N0} rows of {1:N0} bodies, t {2:0} to {3:0} s ({4:0.0} s); the scale stays " +
                    "the first window's P95 {5:0.####} W (this window's is {6:0.####}).",
                    rows, series.Count, lo, hi, clock.Elapsed.TotalSeconds, _p95, windowP95));
            }
        }

        /// <summary>
        /// Positions the stream inside the row-ordered file near the first row at or after
        /// <paramref name="second"/>, by bisection on the bytes. True when it stopped inside a row.
        /// </summary>
        private static bool SeekNear(FileStream stream, double second)
        {
            long low = 0;
            long high = stream.Length;

            while (high - low > (1L << 20))
            {
                long mid = low + (high - low) / 2;
                double t = TimeAfter(stream, mid);

                if (double.IsNaN(t) || t >= second) high = mid;
                else low = mid;
            }

            stream.Position = low;
            return low > 0;
        }

        // The t of the first whole row that starts after a byte position, or NaN.
        private static double TimeAfter(FileStream stream, long position)
        {
            stream.Position = position;

            int b;
            while ((b = stream.ReadByte()) >= 0 && b != '\n') { }
            if (b < 0) return double.NaN;

            var head = new byte[96];
            int n = stream.Read(head, 0, head.Length);
            if (n <= 0) return double.NaN;

            return TryNumber(Encoding.ASCII.GetString(head, 0, n), "\"t\":", out double t) ? t : double.NaN;
        }

        // The number after a key, up to the comma or brace that ends it. A value cut short by the end of the
        // text is no value.
        private static bool TryNumber(string row, string key, out double value)
        {
            value = double.NaN;

            int start = row.IndexOf(key, StringComparison.Ordinal);
            if (start < 0) return false;
            start += key.Length;

            int end = start;
            while (end < row.Length && row[end] != ',' && row[end] != '}') end++;

            if (end >= row.Length || end == start) return false;

            return double.TryParse(
                row.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static void Say(string key, string message)
        {
            if (_said == null) _said = new HashSet<string>();
            if (_said.Add(key)) Debug.Log(message);
        }
    }
}
