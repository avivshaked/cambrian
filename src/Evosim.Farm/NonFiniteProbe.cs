using System;
using System.Globalization;
using System.Text;
using Evosim.Core;

namespace Evosim.Farm
{
    /// <summary>
    /// A film window's <c>--probe</c>: after every metabolic step, looks for the first value that
    /// is not a finite number, or a field cell below zero, and says where it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written for round 48 seed 2, which ended at 13,700 s when the absorptive log's writer
    /// refused a creature's <c>densityHere</c> of minus infinity. Its statistics row at 13,690 s
    /// was ordinary, so whatever did it happened inside twenty metabolic steps, and a film window
    /// from the checkpoint before it steps the same world bit for bit. The probe reads and never
    /// writes, so the window it rides on stays the run's own.
    /// </para>
    /// <para>
    /// What it reads: every living body's density, share, reserve, tissue, place and last ledger;
    /// every corpse's joules and place; and every cell of the two grids, narrowed to float, so a
    /// double past float's range reads as infinite, which is how the writer saw it. The previous
    /// step's grids are kept, so a finding carries the cell's value one step earlier and its six
    /// neighbours' values on both steps.
    /// </para>
    /// </remarks>
    internal sealed class NonFiniteProbe
    {
        private float[] _snow, _snowBefore, _matter, _matterBefore;
        private double _secondsBefore = double.NaN;

        /// <summary>Metabolic steps scanned.</summary>
        public long Scans { get; private set; }

        /// <summary>
        /// Scans the world as it stands after a metabolic step, and returns a report of what is
        /// wrong, or null when nothing is.
        /// </summary>
        public string Scan(World world, double seconds)
        {
            Scans++;
            var found = new StringBuilder();

            GridField snow = world.Nutrients as GridField;
            GridField matter = world.Matter as GridField;
            Copy(snow, ref _snow);
            Copy(matter, ref _matter);

            for (int i = 0; i < world.Living.Count; i++)
            {
                Organism c = world.Living[i];
                EnergyLedger l = c.LastLedger;

                bool bad = !Finite(c.LastDensityHere) || !Finite(c.LastShare) || !Finite(c.Energy)
                    || !Finite(c.TissueJoules) || !Finite(c.X) || !Finite(c.HeightY) || !Finite(c.Z)
                    || !Finite(l.FoodIncome) || !Finite(l.LightIncome) || !Finite(l.PoolDrawn)
                    || !Finite(l.Exuded) || !Finite(l.Upkeep) || !Finite(l.Work) || !Finite(l.Handling)
                    || !Finite(l.Neural) || c.LastDensityHere < 0f;

                if (!bad) continue;

                found.Append(Invariant(
                    $"creature {c.Id} at living index {i}: densityHere {c.LastDensityHere:R}, share {c.LastShare:R}, ")
                    + Invariant($"energy {c.Energy:R}, tissue {c.TissueJoules:R}, x {c.X:R} y {c.HeightY:R} z {c.Z:R}, ")
                    + Invariant($"patch {c.Patch}, age {c.Age:R}, absorptive {c.HasAbsorptiveTissue}, ")
                    + Invariant($"photosynthetic {c.HasPhotosyntheticTissue}, lastStep {c.LastStepSeconds:R}; ledger food {l.FoodIncome:R}, ")
                    + Invariant($"light {l.LightIncome:R}, drawn {l.PoolDrawn:R}, exuded {l.Exuded:R}, upkeep {l.Upkeep:R}, ")
                    + Invariant($"work {l.Work:R}, handling {l.Handling:R}, neural {l.Neural:R}")
                    + Environment.NewLine);

                if (snow != null) found.Append("  its snow cell: " + Cell(snow, _snow, _snowBefore, c.X, c.HeightY, c.Z)).Append(Environment.NewLine);
                if (matter != null) found.Append("  its matter cell: " + Cell(matter, _matter, _matterBefore, c.X, c.HeightY, c.Z)).Append(Environment.NewLine);
            }

            for (int i = 0; i < world.Corpses.Count; i++)
            {
                Corpse k = world.Corpses[i];
                if (Finite(k.Joules) && k.Joules >= 0d && Finite(k.Position.X) && Finite(k.Position.Y) && Finite(k.Position.Z)) continue;

                found.Append(Invariant(
                    $"corpse of {k.CreatureId}: joules {k.Joules:R}, at {k.Position.X:R} {k.Position.Y:R} {k.Position.Z:R}, age {k.AgeSeconds:R}")
                    + Environment.NewLine);
            }

            ScanGrid("snow", snow, _snow, _snowBefore, found);
            ScanGrid("matter", matter, _matter, _matterBefore, found);

            string report = found.Length == 0
                ? null
                : Invariant($"probe: at {seconds:R} s (the step before was {_secondsBefore:R} s), after {Scans} scans:")
                    + Environment.NewLine + found;

            (_snow, _snowBefore) = (_snowBefore, _snow);
            (_matter, _matterBefore) = (_matterBefore, _matter);
            _secondsBefore = seconds;

            return report;
        }

        private static void Copy(GridField grid, ref float[] into)
        {
            if (grid == null) return;
            if (into == null || into.Length < grid.CellCount) into = new float[grid.CellCount];
            grid.CopyStockTo(into);
        }

        private static void ScanGrid(string name, GridField grid, float[] now, float[] before, StringBuilder found)
        {
            if (grid == null) return;

            int bad = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                float v = now[i];
                if (Finite(v) && v >= -1e-9f) continue;

                if (bad < 12)
                {
                    Decompose(grid, i, out int ix, out int iy, out int iz);
                    found.Append(Invariant($"{name} cell {i} (ix {ix}, iy {iy}, iz {iz}, live {grid.IsLive(ix, iy, iz)}): ")
                        + Around(grid, now, before, ix, iy, iz)).Append(Environment.NewLine);
                }

                bad++;
            }

            if (bad > 12) found.Append(Invariant($"{name}: {bad} cells in all")).Append(Environment.NewLine);
        }

        private static string Cell(GridField grid, float[] now, float[] before, float x, float y, float z)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z)) return "no cell for a point that is not finite";

            int ix = (int)Math.Floor(x / grid.CellMetres);
            int iy = (int)Math.Floor(-y / grid.CellMetres);
            int iz = (int)Math.Floor(z / grid.CellMetres);

            if (ix < 0 || ix >= grid.CellsX || iy < 0 || iy >= grid.CellsY || iz < 0 || iz >= grid.CellsZ)
            {
                return Invariant($"(ix {ix}, iy {iy}, iz {iz}) is outside the grid, which clamps it");
            }

            return Invariant($"(ix {ix}, iy {iy}, iz {iz}, live {grid.IsLive(ix, iy, iz)}) ") + Around(grid, now, before, ix, iy, iz);
        }

        /// <summary>The cell and its six neighbours, now and one step before.</summary>
        private static string Around(GridField grid, float[] now, float[] before, int ix, int iy, int iz)
        {
            var s = new StringBuilder();
            int[,] offsets = { { 0, 0, 0 }, { -1, 0, 0 }, { 1, 0, 0 }, { 0, -1, 0 }, { 0, 1, 0 }, { 0, 0, -1 }, { 0, 0, 1 } };
            string[] names = { "self", "west", "east", "up", "down", "south", "north" };

            for (int k = 0; k < names.Length; k++)
            {
                int jx = ix + offsets[k, 0], jy = iy + offsets[k, 1], jz = iz + offsets[k, 2];
                if (jx < 0 || jx >= grid.CellsX || jy < 0 || jy >= grid.CellsY || jz < 0 || jz >= grid.CellsZ) continue;

                int j = (jy * grid.CellsX + jx) * grid.CellsZ + jz;
                string was = before == null ? "?" : before[j].ToString("R", CultureInfo.InvariantCulture);
                string dead = grid.IsLive(jx, jy, jz) ? "" : ", dead";
                s.Append(Invariant($"{names[k]} {now[j]:R} (was {was}{dead}); "));
            }

            return s.ToString();
        }

        private static void Decompose(GridField grid, int cell, out int ix, out int iy, out int iz)
        {
            iz = cell % grid.CellsZ;
            int rest = cell / grid.CellsZ;
            ix = rest % grid.CellsX;
            iy = rest / grid.CellsX;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
    }
}
