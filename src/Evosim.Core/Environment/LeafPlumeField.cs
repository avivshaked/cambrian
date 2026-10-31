using System;

namespace Evosim.Core
{
    /// <summary>
    /// D270's carried leaf smell: a coarse grid of the lit leaf area living leaves have deposited, which the
    /// current carries, the water stirs and a lifetime slowly takes back, with a table of the density's slope in
    /// every cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It owns a <see cref="GridField"/> and borrows all of its geometry.</b> The cells are cubes of
    /// <see cref="RunConfig.LeafPlumeCellMetres"/>; the mask that kills the cells under the bed and in coral, the
    /// seams that join x and z, the conservative <see cref="GridField.Mix"/> and the advection from the current's
    /// potential are the grid's own. The plume never settles: it has no sink and no refuge, so nothing falls and
    /// nothing is eaten.
    /// </para>
    /// <para>
    /// <b>The stock is in square-metre seconds</b>: a leaf of lit area A deposits A times the step, so the density
    /// is m2 s per m3, and <see cref="RunConfig.LeafPlumeHalfScale"/> is in those units.
    /// </para>
    /// <para>
    /// <b>The gradient table.</b> Three doubles a cell hold the density's slope per metre on the world's x, y and
    /// z. With both neighbours on an axis live it is the central difference; with one live, the one-sided
    /// difference against the cell; with neither, 0. x and z wrap at the seams. A cell's <c>iy</c> grows downward,
    /// so the slope on y is minus the difference along <c>iy</c>. A dead cell holds 0.
    /// </para>
    /// <para>
    /// <b>Reads.</b> <see cref="DensityOfCell"/> and <see cref="GradientOfCell"/> give a cell's own numbers;
    /// <see cref="Sample"/> gives them interpolated trilinearly between cell centres, so that a body crossing a
    /// cell boundary meets no step in either (the coordinator's ruling within D270). A corner cell with no water
    /// stands in as the cell the point is in.
    /// </para>
    /// </remarks>
    public sealed class LeafPlumeField
    {
        private readonly GridField _grid;
        private readonly int _nx;
        private readonly int _ny;
        private readonly int _nz;
        private readonly double _cell;
        private readonly double _length;
        private readonly double _width;
        private readonly double _volume;

        private readonly double[] _gx;
        private readonly double[] _gy;
        private readonly double[] _gz;

        /// <summary>Builds the plume over a grid the world made for it, with no sink and no refuge.</summary>
        public LeafPlumeField(GridField grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (grid.Shape == WorldShape.Tank)
                throw new ArgumentException("D270's plume is built for the box and is refused in a tank.", nameof(grid));

            _nx = grid.CellsX;
            _ny = grid.CellsY;
            _nz = grid.CellsZ;
            _cell = grid.CellMetres;
            _length = grid.LengthMetres;
            _width = grid.WidthMetres;
            _volume = grid.CellVolume;

            _gx = new double[grid.CellCount];
            _gy = new double[grid.CellCount];
            _gz = new double[grid.CellCount];
        }

        /// <summary>The grid underneath, for the stock sums and the dump.</summary>
        public GridField Grid => _grid;

        /// <summary>The plume's cell, metres.</summary>
        public float CellMetres => _grid.CellMetres;

        /// <summary>Cells along x, y and z.</summary>
        public int CellsX => _nx;

        /// <inheritdoc cref="CellsX"/>
        public int CellsY => _ny;

        /// <inheritdoc cref="CellsX"/>
        public int CellsZ => _nz;

        /// <summary>Every cell of the array, dead ones included.</summary>
        public int CellCount => _grid.CellCount;

        /// <summary>What the plume holds now, m2 s: the running total, which <see cref="Recount"/> re-sums.</summary>
        public double TotalStock => _grid.TotalJoules;

        /// <summary>Re-sums the cells and returns the sum.</summary>
        public double Recount() => _grid.Recount();

        /// <summary>The cell a position falls in, by flat index. A point in rock is given the nearest water.</summary>
        public int CellIndexAt(Float3 position) => _grid.CellIndexAt(position);

        /// <summary>The flat index of the cell at three indices: <c>(iy * nx + ix) * nz + iz</c>.</summary>
        public int CellIndex(int ix, int iy, int iz) => _grid.CellIndexOf(ix, iy, iz);

        /// <summary>The stock of the cell at a flat index.</summary>
        public double StockOfCell(int cell) => _grid.StockOfCell(cell);

        /// <summary>The density of the cell at a flat index: its stock over the cell's volume.</summary>
        public double DensityOfCell(int cell) => _grid.StockOfCell(cell) / _volume;

        /// <summary>The density's slope per metre in the cell at a flat index, on the world's x, y and z.</summary>
        public void GradientOfCell(int cell, out double gx, out double gy, out double gz)
        {
            gx = _gx[cell];
            gy = _gy[cell];
            gz = _gz[cell];
        }

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// Deposits <paramref name="amount"/> at a point and returns it (0 for an amount that is not above 0).
        /// </summary>
        public double Deposit(Float3 position, double amount)
        {
            if (!(amount > 0d)) return 0d;
            _grid.DepositExact(new FieldPoint(position, _grid.PatchOf(position.X, position.Z)), amount);
            return amount;
        }

        /// <summary>
        /// Every leaf source deposits its lit area times the step, and the sum comes back. The sources are the
        /// world's, four floats each: x, y, z and area.
        /// </summary>
        public double DepositSources(float[] sources, int count, float seconds)
        {
            double emitted = 0d;
            for (int i = 0; i < count; i++)
            {
                var p = new Float3(sources[4 * i], sources[4 * i + 1], sources[4 * i + 2]);
                emitted += Deposit(p, (double)sources[4 * i + 3] * seconds);
            }

            return emitted;
        }

        /// <summary>Stirs on every axis at one rate (the cells are cubes), conservatively.</summary>
        public void Mix(float seconds, float diffusivity) => _grid.Mix(seconds, diffusivity, diffusivity);

        /// <summary>Carries the plume with the current's potential, conservatively. Never offered a device.</summary>
        public void Advect(CurrentField current, double elapsedSeconds, float seconds) =>
            _grid.Advect(current, elapsedSeconds, seconds, _grid.PatchWidthMetres);

        /// <summary>
        /// Loses a share of every cell: the stock times <c>exp(-seconds / lifetime)</c>, one multiplication a
        /// cell. Returns the stock that went.
        /// </summary>
        public double Decay(double seconds, double lifetimeSeconds) =>
            _grid.Decay(Math.Exp(-seconds / lifetimeSeconds));

        /// <summary>
        /// One step of the plume, in the world's order: the leaves deposit, the water stirs and carries it, a
        /// lifetime takes a share, and the gradient table is rebuilt. Returns what was emitted and what decayed.
        /// </summary>
        public (double Emitted, double Decayed) Step(
            float[] sources, int count, float seconds, float diffusivity, CurrentField current,
            double elapsedSeconds, float lifetimeSeconds)
        {
            double emitted = DepositSources(sources, count, seconds);
            Mix(seconds, diffusivity);
            Advect(current, elapsedSeconds, seconds);
            double decayed = Decay(seconds, lifetimeSeconds);
            RebuildGradient();
            return (emitted, decayed);
        }

        // ------------------------------------------------------------------ the gradient

        private bool Live(int ix, int iy, int iz) => _grid.IsLive(ix, iy, iz);

        private double Density(int ix, int iy, int iz) => _grid.StockOfCell(_grid.CellIndexOf(ix, iy, iz)) / _volume;

        /// <summary>
        /// Rebuilds the table of the density's slope from the stock as it stands. Run at the end of every step and
        /// after a restore; it reads only the stock and the mask, so a restored world has the table the unbroken
        /// one has.
        /// </summary>
        public void RebuildGradient()
        {
            for (int iy = 0; iy < _ny; iy++)
            for (int ix = 0; ix < _nx; ix++)
            for (int iz = 0; iz < _nz; iz++)
            {
                int cell = _grid.CellIndexOf(ix, iy, iz);
                if (!Live(ix, iy, iz))
                {
                    _gx[cell] = 0d;
                    _gy[cell] = 0d;
                    _gz[cell] = 0d;
                    continue;
                }

                double here = Density(ix, iy, iz);

                int xm = ix == 0 ? _nx - 1 : ix - 1;
                int xp = ix == _nx - 1 ? 0 : ix + 1;
                _gx[cell] = Slope(
                    Live(xm, iy, iz), Density(xm, iy, iz), Live(xp, iy, iz), Density(xp, iy, iz), here);

                int zm = iz == 0 ? _nz - 1 : iz - 1;
                int zp = iz == _nz - 1 ? 0 : iz + 1;
                _gz[cell] = Slope(
                    Live(ix, iy, zm), Density(ix, iy, zm), Live(ix, iy, zp), Density(ix, iy, zp), here);

                // iy grows downward and the world's y grows up, so the slope on y has the opposite sign to the
                // difference along iy: the layer above is iy - 1 and is the "plus" neighbour. No wrap on y.
                bool upLive = iy > 0 && Live(ix, iy - 1, iz);
                bool downLive = iy < _ny - 1 && Live(ix, iy + 1, iz);
                _gy[cell] = Slope(
                    downLive, downLive ? Density(ix, iy + 1, iz) : 0d,
                    upLive, upLive ? Density(ix, iy - 1, iz) : 0d, here);
            }
        }

        /// <summary>
        /// The slope along an axis from the density at its minus and plus neighbours: the central difference when
        /// both are live, the one-sided difference against the cell when one is, and 0 when neither.
        /// </summary>
        private double Slope(bool minusLive, double minus, bool plusLive, double plus, double here)
        {
            if (minusLive && plusLive) return (plus - minus) / (2d * _cell);
            if (plusLive) return (plus - here) / _cell;
            if (minusLive) return (here - minus) / _cell;
            return 0d;
        }

        // ------------------------------------------------------------------ the reads

        /// <summary>
        /// The density and its slope at a point, interpolated trilinearly between the eight cell centres around
        /// it: x and z wrap at the seams, y is clamped at the top and the floor of the array, and a corner cell
        /// with no water stands in as the cell the point is in. A point that is not finite reads zeros.
        /// </summary>
        public void Sample(Float3 position, out double density, out double gx, out double gy, out double gz)
        {
            density = 0d;
            gx = gy = gz = 0d;
            if (!position.IsFinite) return;

            int home = _grid.CellIndexAt(position);

            double ux = Fold(position.X, _length) / _cell - 0.5d;
            double uz = Fold(position.Z, _width) / _cell - 0.5d;
            double uy = (position.Y >= 0f ? 0d : -(double)position.Y) / _cell - 0.5d;

            Axis(ux, _nx, true, out int x0, out int x1, out double fx);
            Axis(uz, _nz, true, out int z0, out int z1, out double fz);
            Axis(uy, _ny, false, out int y0, out int y1, out double fy);

            for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
            for (int c = 0; c < 2; c++)
            {
                double w = (a == 0 ? 1d - fx : fx) * (b == 0 ? 1d - fy : fy) * (c == 0 ? 1d - fz : fz);
                if (w == 0d) continue;

                int ix = a == 0 ? x0 : x1;
                int iy = b == 0 ? y0 : y1;
                int iz = c == 0 ? z0 : z1;
                int cell = Live(ix, iy, iz) ? _grid.CellIndexOf(ix, iy, iz) : home;

                density += w * _grid.StockOfCell(cell) / _volume;
                gx += w * _gx[cell];
                gy += w * _gy[cell];
                gz += w * _gz[cell];
            }
        }

        /// <summary>The density at a point, <see cref="Sample"/>'s first number.</summary>
        public double DensityAt(Float3 position)
        {
            Sample(position, out double density, out _, out _, out _);
            return density;
        }

        private static double Fold(double v, double extent)
        {
            double folded = v - extent * Math.Floor(v / extent);
            return folded >= extent || folded < 0d ? 0d : folded;
        }

        /// <summary>
        /// The two cells along an axis a coordinate (in cells, measured from the first centre) lies between, and
        /// the share of the second. Wrapped when the axis is a ring, clamped to the first and last cell when not.
        /// </summary>
        private static void Axis(double u, int n, bool ring, out int i0, out int i1, out double f)
        {
            if (ring)
            {
                double floor = Math.Floor(u);
                f = u - floor;
                int a = (int)floor;
                i0 = ((a % n) + n) % n;
                i1 = (i0 + 1) % n;
                return;
            }

            if (u <= 0d)
            {
                i0 = i1 = 0;
                f = 0d;
                return;
            }

            if (u >= n - 1)
            {
                i0 = i1 = n - 1;
                f = 0d;
                return;
            }

            double fl = Math.Floor(u);
            i0 = (int)fl;
            i1 = i0 + 1;
            f = u - fl;
        }

        // ------------------------------------------------------------------ checks

        /// <summary>How many cells hold a stock that is not finite or is below 0. 0 in a sound plume.</summary>
        public int CountUnsoundCells()
        {
            int bad = 0;
            for (int i = 0; i < _grid.CellCount; i++)
            {
                double s = _grid.StockOfCell(i);
                if (double.IsNaN(s) || double.IsInfinity(s) || s < 0d) bad++;
            }

            return bad;
        }

        /// <summary>The cells' stocks as floats, in flat order, for the dump.</summary>
        public float[] CopyStock()
        {
            var into = new float[_grid.CellCount];
            _grid.CopyStockTo(into);
            return into;
        }

        // ------------------------------------------------------------------ the checkpoint

        internal void WriteState(System.IO.BinaryWriter w) => _grid.WriteState(w);

        internal void ReadState(System.IO.BinaryReader r)
        {
            _grid.ReadState(r);
            RebuildGradient();
        }
    }
}
