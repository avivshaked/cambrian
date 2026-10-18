using System;
using System.Collections.Generic;
namespace Evosim.Core
{
    /// <summary>
    /// The smell of carrion, <see cref="SensorChannel.Scent"/> (propose-foraging.md's third rule):
    /// at a point, the sum over corpses of the corpse's joules times a cone that falls from 1 at the
    /// corpse to 0 at <see cref="RangeMetres"/>. Filled once a metabolic step from
    /// <see cref="World.Corpses"/> on a grid of <see cref="CellMetres"/> cells and read by trilinear
    /// interpolation between cell centres, so a body a metre long reads a gradient across itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived, not state.</b> Nothing reads it but the senses, and it is refilled from the
    /// corpses at every metabolic step and on every restore, so a checkpoint carries the corpses and
    /// not this. The fill splits the grid's rows over <see cref="Parallelism.Threads"/>, and every
    /// row takes the corpses in their own order, so the sums are the same sums at any thread count.
    /// </para>
    /// <para>
    /// <b>Why a grid and not an exact sum.</b> Round 52 held about 15,000 corpses by 30,000 s, a
    /// few hundred within 10 m of any body on the bed; summing them for every part at every
    /// physics step would cost more than the physics. The grid costs the corpses times the cells a
    /// cone covers (about 520 at 10 m on 2 m cells) twice a second.
    /// </para>
    /// </remarks>
    /// <summary>D167's leaf smell: which form a world carries (<c>RunConfig.LeafScent</c>).</summary>
    public enum LeafScentForm
    {
        /// <summary>No leaf smell: every recorded world.</summary>
        None,

        /// <summary>One number per part, <see cref="SensorChannel.LeafScent"/>.</summary>
        Strength,

        /// <summary>Three per part, <see cref="SensorChannel.LeafScentDirection"/>.</summary>
        Direction,
    }

    public sealed class ScentField
    {
        /// <summary>
        /// The carrion field's cell, metres, and the default for any field built without one.
        /// <see cref="CellOf"/> and the card's carrion kernel read this constant.
        /// </summary>
        public const float CellMetres = 2f;

        private readonly int _nx, _ny, _nz;
        private readonly float _range;
        private readonly float _cell;
        private readonly float[] _v;

        public float RangeMetres => _range;

        /// <summary>This field's cell, metres: <see cref="CellMetres"/> unless built with another.</summary>
        public float CellSizeMetres => _cell;

        /// <param name="length">The world along x, metres (x from 0).</param>
        /// <param name="width">The world along z, metres (z from 0).</param>
        /// <param name="depth">The water's depth, metres (y from 0 down to −depth).</param>
        /// <param name="range">Where the cone reaches 0, metres.</param>
        public ScentField(float length, float width, float depth, float range)
            : this(length, width, depth, range, CellMetres)
        {
        }

        /// <param name="cell">
        /// The grid's cell, metres. The carrion field's is <see cref="CellMetres"/>; D167's leaf
        /// field takes its own (<c>RunConfig.LeafScentCellMetres</c>).
        /// </param>
        public ScentField(float length, float width, float depth, float range, float cell)
        {
            if (!(range > 0) || float.IsInfinity(range))
                throw new ArgumentOutOfRangeException(nameof(range), range, "the scent's range must be a positive number of metres");
            if (!(cell > 0) || float.IsInfinity(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "the scent's cell must be a positive number of metres");
            _range = range;
            _cell = cell;
            _nx = Math.Max(1, (int)Math.Ceiling(length / cell));
            _ny = Math.Max(1, (int)Math.Ceiling(depth / cell));
            _nz = Math.Max(1, (int)Math.Ceiling(width / cell));
            _v = new float[_nx * _ny * _nz];
        }

        private int Index(int i, int j, int k) => (i * _ny + j) * _nz + k;

        /// <summary>Clears the grid and adds every corpse's cone to it, in the list's order.</summary>
        public void Fill(IReadOnlyList<Corpse> corpses) => Fill(corpses, corpses.Count);

        /// <summary>
        /// Clears the grid and adds the first <paramref name="count"/> corpses' cones to it, in the
        /// list's order.
        /// </summary>
        /// <remarks>
        /// The prefix is what a restore needs: a corpse appended after the step's fill (a module's
        /// remains, from the farm's growth step) is not in the live field until the next fill, so
        /// the restore leaves it out too (<c>logbook/specs/r57-build/scent-restore.md</c>).
        /// </remarks>
        public void Fill(IReadOnlyList<Corpse> corpses, int count)
        {
            if (count < 0 || count > corpses.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count), count,
                    "The scent fills from a prefix of the corpses, at most all " + corpses.Count + " of them.");
            }

            Clear();

            // Split by rows of cells, a row being one (i, j) pair and its run of k, so each range
            // writes only its own cells. Every range walks every corpse in the list's order, so
            // each cell takes the same additions in the same order at any thread count; at one
            // thread ForRanges is one call over every row, the serial loop it always was.
            Parallelism.ForRanges(_nx * _ny, (from, to) =>
            {
                for (int c = 0; c < count; c++) AddRows(corpses[c].Position, corpses[c].Joules, from, to);
            });
        }

        /// <summary>A copy of every cell's value, for a check that two fields are the same field.</summary>
        public float[] CopyCells() => (float[])_v.Clone();

        /// <summary>The grid's cells along x, depth and z, indexed (i·ny + j)·nz + k.</summary>
        public int CellsX => _nx;
        public int CellsY => _ny;
        public int CellsZ => _nz;

        /// <summary>
        /// Every cell's value into <paramref name="into"/>, which holds at least
        /// CellsX·CellsY·CellsZ: the gpu engine's upload, once a block, with nothing allocated.
        /// </summary>
        public void CopyCellsTo(float[] into) => Array.Copy(_v, into, _v.Length);

        /// <summary>
        /// Every cell's value from <paramref name="from"/>, which holds at least
        /// CellsX·CellsY·CellsZ: an engine that fills the grid itself bringing it home
        /// (<see cref="World.BringScentHome"/>).
        /// </summary>
        public void CopyCellsFrom(float[] from) => Array.Copy(from, _v, _v.Length);

        /// <summary>The cells a corpse's cone can reach from its own cell along each axis.</summary>
        public int ReachCells => (int)Math.Ceiling(_range / _cell);

        /// <summary>
        /// The cell a point falls in along x, depth and z, unclamped, as <see cref="Fill"/> takes a
        /// corpse's: the same expressions, so an engine that bins corpses by cell bins them as the
        /// fill reaches them.
        /// </summary>
        public static void CellOf(Float3 p, out int i, out int j, out int k)
        {
            i = (int)Math.Floor(p.X / CellMetres);
            j = (int)Math.Floor(-p.Y / CellMetres);
            k = (int)Math.Floor(p.Z / CellMetres);
        }

        /// <summary>Empties the grid.</summary>
        public void Clear() => Array.Clear(_v, 0, _v.Length);

        /// <summary>
        /// Clears the grid and adds <paramref name="count"/> sources' cones from
        /// <paramref name="sources"/>, four floats each (x, y, z and the cone's height), in order:
        /// D167's leaves, by rows across the threads as <see cref="Fill(IReadOnlyList{Corpse}, int)"/>
        /// splits them, so the sums are the same at any thread count.
        /// </summary>
        public void FillSources(float[] sources, int count)
        {
            if (count < 0 || 4 * count > sources.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "more sources than the array holds");
            }

            Clear();
            Parallelism.ForRanges(_nx * _ny, (from, to) =>
            {
                for (int c = 0; c < count; c++)
                {
                    int at = 4 * c;
                    AddRows(new Float3(sources[at], sources[at + 1], sources[at + 2]), sources[at + 3], from, to);
                }
            });
        }

        /// <summary>Adds one source's cone: <paramref name="joules"/> at its centre, 0 at the range.</summary>
        public void Add(Float3 p, double joules) => AddRows(p, joules, 0, _nx * _ny);

        /// <summary>
        /// <see cref="Add"/> restricted to the rows <c>i * ny + j</c> in [<paramref name="from"/>,
        /// <paramref name="to"/>): the same arithmetic, in the same order, on those cells only.
        /// </summary>
        private void AddRows(Float3 p, double joules, int from, int to)
        {
            if (!(joules > 0)) return;
            int reach = (int)Math.Ceiling(_range / _cell);
            int ci = (int)Math.Floor(p.X / _cell);
            int cj = (int)Math.Floor(-p.Y / _cell);
            int ck = (int)Math.Floor(p.Z / _cell);

            // The rows this range owns span i from from / ny to (to - 1) / ny; a cone outside them
            // costs two compares.
            int iFirst = Math.Max(Math.Max(0, ci - reach), from / _ny);
            int iLast = Math.Min(Math.Min(_nx - 1, ci + reach), (to - 1) / _ny);

            // A cell whose sum of squares is at least the range squared is skipped before its root.
            // The range is a float, so its square is exact in a double, and a correctly rounded root
            // is monotone, so such a cell's distance is at least the range and the test below would
            // skip it anyway. Adding a square never rounds a sum below what it was added to, so a
            // row or a slab whose partial sum is already there is skipped whole. The squares and
            // the partial sums are the same doubles the full expression makes, left to right, since
            // .NET fuses no product into an add; every cell under the bound keeps the exact test.
            double r2 = (double)_range * _range;
            for (int i = iFirst; i <= iLast; i++)
            {
                double dx = (i + 0.5) * _cell - p.X;
                double xx = dx * dx;
                if (xx >= r2) continue;
                for (int j = Math.Max(0, cj - reach); j <= Math.Min(_ny - 1, cj + reach); j++)
                {
                    int row = i * _ny + j;
                    if (row < from || row >= to) continue;
                    double dy = -(j + 0.5) * _cell - p.Y;
                    double xy = xx + dy * dy;
                    if (xy >= r2) continue;
                    int at = row * _nz;
                    for (int k = Math.Max(0, ck - reach); k <= Math.Min(_nz - 1, ck + reach); k++)
                    {
                        double dz = (k + 0.5) * _cell - p.Z;
                        double s = xy + dz * dz;
                        if (s >= r2) continue;
                        double d = Math.Sqrt(s);
                        if (d >= _range) continue;
                        _v[at + k] += (float)(joules * (1.0 - d / _range));
                    }
                }
            }
        }

        /// <summary>
        /// The scent at a point, joules-weighted, by trilinear interpolation between the centres of
        /// the eight cells round it; a point outside the grid reads its nearest edge.
        /// </summary>
        public float At(float x, float y, float z)
        {
            double u = x / _cell - 0.5, w = -y / _cell - 0.5, s = z / _cell - 0.5;
            if (double.IsNaN(u + w + s) || double.IsInfinity(u + w + s)) return 0f;
            int i0 = Clamp((int)Math.Floor(u), _nx), j0 = Clamp((int)Math.Floor(w), _ny), k0 = Clamp((int)Math.Floor(s), _nz);
            int i1 = Clamp(i0 + 1, _nx), j1 = Clamp(j0 + 1, _ny), k1 = Clamp(k0 + 1, _nz);
            double fu = Frac(u, i0, _nx), fw = Frac(w, j0, _ny), fs = Frac(s, k0, _nz);
            double c00 = _v[Index(i0, j0, k0)] * (1 - fs) + _v[Index(i0, j0, k1)] * fs;
            double c01 = _v[Index(i0, j1, k0)] * (1 - fs) + _v[Index(i0, j1, k1)] * fs;
            double c10 = _v[Index(i1, j0, k0)] * (1 - fs) + _v[Index(i1, j0, k1)] * fs;
            double c11 = _v[Index(i1, j1, k0)] * (1 - fs) + _v[Index(i1, j1, k1)] * fs;
            double c0 = c00 * (1 - fw) + c01 * fw, c1 = c10 * (1 - fw) + c11 * fw;
            return (float)(c0 * (1 - fu) + c1 * fu);
        }

        /// <summary>
        /// The field's gradient at a point, per metre, along x, y and z: the derivative of the
        /// interpolation <see cref="At"/> reads. Zero along an axis where the point lies outside
        /// the cell centres, where <see cref="At"/> holds the edge's value.
        /// </summary>
        public Float3 GradientAt(float x, float y, float z)
        {
            double u = x / _cell - 0.5, w = -y / _cell - 0.5, s = z / _cell - 0.5;
            if (double.IsNaN(u + w + s) || double.IsInfinity(u + w + s)) return new Float3(0f, 0f, 0f);
            int i0 = Clamp((int)Math.Floor(u), _nx), j0 = Clamp((int)Math.Floor(w), _ny), k0 = Clamp((int)Math.Floor(s), _nz);
            int i1 = Clamp(i0 + 1, _nx), j1 = Clamp(j0 + 1, _ny), k1 = Clamp(k0 + 1, _nz);
            double fu = Frac(u, i0, _nx), fw = Frac(w, j0, _ny), fs = Frac(s, k0, _nz);
            bool inU = Inside(u, _nx), inW = Inside(w, _ny), inS = Inside(s, _nz);

            double v000 = _v[Index(i0, j0, k0)], v001 = _v[Index(i0, j0, k1)];
            double v010 = _v[Index(i0, j1, k0)], v011 = _v[Index(i0, j1, k1)];
            double v100 = _v[Index(i1, j0, k0)], v101 = _v[Index(i1, j0, k1)];
            double v110 = _v[Index(i1, j1, k0)], v111 = _v[Index(i1, j1, k1)];

            double c00 = v000 * (1 - fs) + v001 * fs, c01 = v010 * (1 - fs) + v011 * fs;
            double c10 = v100 * (1 - fs) + v101 * fs, c11 = v110 * (1 - fs) + v111 * fs;
            double c0 = c00 * (1 - fw) + c01 * fw, c1 = c10 * (1 - fw) + c11 * fw;

            double du = inU ? c1 - c0 : 0;
            double dw = inW ? (c01 - c00) * (1 - fu) + (c11 - c10) * fu : 0;
            double ds = inS
                ? ((v001 - v000) * (1 - fw) + (v011 - v010) * fw) * (1 - fu) +
                  ((v101 - v100) * (1 - fw) + (v111 - v110) * fw) * fu
                : 0;

            // y runs down the grid's j, so its derivative changes sign.
            return new Float3((float)(du / _cell), (float)(-dw / _cell), (float)(ds / _cell));
        }

        private static bool Inside(double u, int n) => n > 1 && u > 0 && u < n - 1;

        private static int Clamp(int i, int n) => i < 0 ? 0 : i >= n ? n - 1 : i;

        private static double Frac(double u, int i0, int n)
        {
            if (u <= 0 || n == 1) return 0;
            if (u >= n - 1) return 0;
            return u - i0;
        }
    }
}
