using System;
using System.Collections.Generic;
using Evosim.Core;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// The scent's fill on the card with the host's bits (C3, card-next-r57.md Part 7): the world
    /// hands over its corpses where it would have filled <see cref="ScentField"/>
    /// (<see cref="World.FillScentElsewhere"/>), this keeps the prefix's positions and joules, bins
    /// them by column, and one thread a cell adds their cones in the list's order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The host's sums, term for term.</b> A cell takes the corpses of the 3 × 3 columns of bins
    /// round its own, each bin in the list's order, and merges the nine by list index, so it meets
    /// them in the order the host's fill does. It skips what the host skips (no joules, outside the
    /// corpse's box, a sum of squares at or past the range squared, a root at or past the range)
    /// and adds the same float term to a float sum that starts from 0, as the cleared grid does.
    /// Every product is an explicitly rounded multiply, which PTX never contracts into a fused
    /// multiply-add (logbook/specs/fma-probe); the double square root and division are
    /// <c>sqrt.rn.f64</c> and <c>div.rn.f64</c> in the double kernels' PTX (card-audit-r57.md). Off
    /// the card a product is the plain one, which .NET never fuses, so the Farm tests run the same
    /// kernel on ILGPU's CPU accelerator against <see cref="ScentField.Fill(IReadOnlyList{Corpse}, int)"/>.
    /// </para>
    /// <para>
    /// <b>Why the bins hold every corpse a cell can meet.</b> A bin is <see cref="ScentField.ReachCells"/>
    /// cells wide, and a corpse is binned by its cell clamped into the grid. A corpse that reaches a
    /// cell is at most the reach from it along each axis, and clamping toward the grid only brings
    /// it closer, so its bin is the cell's own or a neighbour.
    /// </para>
    /// <para>
    /// <b>When the work is done.</b> <see cref="Snapshot"/> runs on the world's thread at the fill's
    /// own point in the step. It copies and bins on the host, then queues the upload and the kernel
    /// at once on a stream of the fill's own, so the card fills while the host finishes the world's
    /// step. Not the engine's stream: the snow's transport, which comes after the snapshot in the
    /// world's step, runs its kernels there and would wait behind the fill (card-next-r57.md Part 7).
    /// The block joins the fill's stream (<see cref="Join"/>) before any kernel reads the grid, and
    /// so does a host reader that asks for the grid (<see cref="BringHome"/>).
    /// </para>
    /// <para>
    /// <b>Everything that touches what the fill uses, and its stream.</b> The order holds by these
    /// joins alone, so the list is the proof; the order probe below only samples it.
    /// <list type="bullet">
    /// <item>The corpses' page-locked host arrays: written by <see cref="Snapshot"/> on the world's
    /// thread, only after it joins the fill's stream; read by their uploads, queued on the fill's
    /// stream. A second snapshot before any block (a restore's, then the step's own) waits there for
    /// the first's uploads.</item>
    /// <item>Their device copies: written by those uploads and read by the fill kernel, both on the
    /// fill's stream, in that order. Resized only inside <see cref="Snapshot"/>, after the join.</item>
    /// <item>The grid on the card: written by the fill kernel on the fill's stream. Read by the
    /// block's kernels on the engine's stream and the classes' streams, all after the runner's section
    /// 5, which joins the fill's stream first; and by <see cref="BringHome"/>'s download on the
    /// engine's stream, after its own join. The runner resizes it in section 4 only when the grid's
    /// shape changes, which it never does once this constructor has sized it, and uploads its host
    /// copy only when no GpuScent fills it. The transport reads and writes the snow's grid, not this
    /// one.</item>
    /// <item>The order probe's generation cell (SCENT_ORDER_PROBE builds only): written by a launch
    /// after the fill kernel on the fill's stream; read by a copy on the engine's stream at
    /// <see cref="Audit"/>, after the join.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>What the runner's Sync waits on with the scent on the card.</b> With a GpuScent wired, Sync
    /// waits stream by stream instead of on the whole device, and every stream the engine's own work
    /// is queued on is one of these:
    /// <list type="bullet">
    /// <item>the engine's stream (GpuBackend's, the device's default when the engine owns it): every
    /// column's copies, the size classes' copies (ClassSet), the memsets, the grid's kernels, the
    /// tail, and the snow's transport (GpuBackend.CreateTransport hands it this stream);</item>
    /// <item>the four size classes' streams, created only under EVOSIM_GPU_CONCURRENT: their water,
    /// fluid, contact and step launches.</item>
    /// </list>
    /// The fill's stream is the one left out. Nothing but this class queues on it, and it is joined in
    /// three places only: section 5 (<see cref="Join"/>), <see cref="BringHome"/> and the next
    /// <see cref="Snapshot"/>. A stream added to the engine later must be added to Sync as well.
    /// </para>
    /// </remarks>
    public sealed class GpuScent : IDisposable
    {
        /// <summary>The grid's shape, the bins, the reach, the range, and the corpses' columns' stride.</summary>
        public struct Params
        {
            public int Nx, Ny, Nz, BinCells, BinsX, BinsZ, Reach, Cap;
            public float Range;
        }

        private readonly Accelerator _acc;
        private readonly AcceleratorStream _stream;
        private readonly Col<float> _grid;
        private readonly Params _p;
        private readonly Action<AcceleratorStream, Index1D, ArrayView<float>, ArrayView<float>, ArrayView<double>,
            ArrayView<int>, ArrayView<int>, Params> _kernel;

        // The binned corpses: x, y, z a corpse; their joules; their cell along x, depth and z and
        // their index in the list; and where each bin starts, with the end after the last.
        private readonly Col<float> _pos;
        private readonly Col<double> _joules;
        private readonly Col<int> _cells;
        private readonly Col<int> _binStart;

        // The host's scratch for the binning, reused.
        private int[] _binOf = new int[16];
        private int[] _cellOf = new int[48];
        private readonly int[] _cursor;

        // The fill's own stream, which the block and BringHome join; and whether a fill is queued
        // there that nothing has joined yet.
        private readonly AcceleratorStream _fill;
        private bool _queued;

        private int _count;

        /// <summary>Threads a group. Every cell is one thread's own, so any size gives the same bits.</summary>
        public const int Group = 64;

        /// <param name="grid">The engine's grid column, which its kernels read; sized here to the field's cells.</param>
        internal GpuScent(Accelerator accelerator, AcceleratorStream stream, ScentField field, Col<float> grid)
        {
            _acc = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
            _stream = stream ?? accelerator.DefaultStream;
            if (field == null) throw new ArgumentNullException(nameof(field));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

            int reach = field.ReachCells;
            _p = new Params
            {
                Nx = field.CellsX, Ny = field.CellsY, Nz = field.CellsZ,
                BinCells = Math.Max(1, reach),
                Reach = reach,
                Range = field.RangeMetres,
            };
            _p.BinsX = (_p.Nx + _p.BinCells - 1) / _p.BinCells;
            _p.BinsZ = (_p.Nz + _p.BinCells - 1) / _p.BinCells;

            int cells = _p.Nx * _p.Ny * _p.Nz;
            if (_grid.Cap != cells) _grid.Resize(cells);

            int bins = _p.BinsX * _p.BinsZ;
            _cursor = new int[bins];
            _fill = _acc.CreateStream();
            _pos = new Col<float>(_acc, _fill, 3, 1);
            _joules = new Col<double>(_acc, _fill, 1, 1);
            _cells = new Col<int>(_acc, _fill, 4, 1);
            _binStart = new Col<int>(_acc, _fill, 1, bins + 1);

            int group = Math.Min(Group, accelerator.MaxNumThreadsPerGroup);
            _kernel = _acc.LoadImplicitlyGroupedKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<double>,
                ArrayView<int>, ArrayView<int>, Params>(FillKernel, group);
#if SCENT_ORDER_PROBE
            _gen = new Col<int>(_acc, _fill, 1, 1);
            _seen = new Col<int>(_acc, _stream, 1, 1);
            _expected = new ScentField(_p.Nx * ScentField.CellMetres, _p.Nz * ScentField.CellMetres,
                _p.Ny * ScentField.CellMetres, _p.Range);
            _want = new float[cells];
            _stamp = _acc.LoadAutoGroupedKernel<Index1D, ArrayView<int>, int>(StampKernel);
            _copy = _acc.LoadAutoGroupedKernel<Index1D, ArrayView<int>, ArrayView<int>>(CopyKernel);
#endif
        }

        /// <summary>How long the last <see cref="Snapshot"/> waited for the fill before it, in ms.</summary>
        public double LastSnapshotWaitMs { get; private set; }

        /// <summary>The corpses the last snapshot kept: those with joules, of the prefix.</summary>
        public int Count => _count;

        /// <summary>
        /// The world's fill, taken on the host: the first <paramref name="filled"/> corpses with joules,
        /// copied and binned by column in the list's order, then uploaded and filled from on the fill's
        /// stream, queued at once.
        /// </summary>
        public void Snapshot(IReadOnlyList<Corpse> corpses, int filled)
        {
            // The last fill's uploads read the arrays written below: wait for them first. Free when
            // a block has joined it since.
            long waited = System.Diagnostics.Stopwatch.GetTimestamp();
            _fill.Synchronize();
            LastSnapshotWaitMs = (System.Diagnostics.Stopwatch.GetTimestamp() - waited) * 1000.0 /
                                 System.Diagnostics.Stopwatch.Frequency;

            if (_binOf.Length < filled) _binOf = new int[Math.Max(filled, 2 * _binOf.Length)];
            if (_cellOf.Length < 3 * filled) _cellOf = new int[Math.Max(3 * filled, 2 * _cellOf.Length)];

            Array.Clear(_cursor, 0, _cursor.Length);
            int kept = 0;
            for (int c = 0; c < filled; c++)
            {
                Corpse corpse = corpses[c];
                if (!(corpse.Joules > 0))
                {
                    _binOf[c] = -1;
                    continue;
                }

                ScentField.CellOf(corpse.Position, out int i, out int j, out int k);
                _cellOf[3 * c] = i;
                _cellOf[3 * c + 1] = j;
                _cellOf[3 * c + 2] = k;
                int b = Clamp(i, _p.Nx) / _p.BinCells * _p.BinsZ + Clamp(k, _p.Nz) / _p.BinCells;
                _binOf[c] = b;
                _cursor[b]++;
                kept++;
            }

            // Where each bin starts, then each corpse at its bin's next place: a counting sort, so a
            // bin keeps the list's order.
            int[] start = _binStart.Host;
            int at = 0;
            for (int b = 0; b < _cursor.Length; b++)
            {
                start[b] = at;
                at += _cursor[b];
                _cursor[b] = start[b];
            }
            start[_cursor.Length] = at;

            int room = Math.Max(1, kept);
            if (_joules.Cap < room)
            {
                int cap = Math.Max(room, 2 * _joules.Cap);
                _pos.Resize(cap);
                _joules.Resize(cap);
                _cells.Resize(cap);
            }

            float[] pos = _pos.Host;
            double[] joules = _joules.Host;
            int[] cell = _cells.Host;
            int capacity = _joules.Cap;
            for (int c = 0; c < filled; c++)
            {
                int b = _binOf[c];
                if (b < 0) continue;
                int to = _cursor[b]++;
                Corpse corpse = corpses[c];
                Float3 p = corpse.Position;
                pos[to] = p.X;
                pos[capacity + to] = p.Y;
                pos[2 * capacity + to] = p.Z;
                joules[to] = corpse.Joules;
                cell[to] = _cellOf[3 * c];
                cell[capacity + to] = _cellOf[3 * c + 1];
                cell[2 * capacity + to] = _cellOf[3 * c + 2];
                cell[3 * capacity + to] = c;
            }

            _count = kept;

#if SCENT_ORDER_PROBE
            _generation++;
            _snapshots++;
            if (OrderOn) _expected.Fill(corpses, filled);
#endif

            // The copies and the launch, on the fill's stream, in that order.
            _pos.UpQueued();
            _joules.UpQueued();
            _cells.UpQueued();
            _binStart.UpQueued();
            Params launch = _p;
            launch.Cap = _joules.Cap;
            _kernel(_fill, launch.Nx * launch.Ny * launch.Nz, _grid.View, _pos.View, _joules.View, _cells.View, _binStart.View, launch);
#if SCENT_ORDER_PROBE
            _stamp(_fill, 1, _gen.View, _generation);
#endif
            _queued = true;
        }

        /// <summary>
        /// Waits for the fill queued at the last snapshot, if nothing has yet: the block calls it
        /// before any kernel reads the grid. True when there was a fill to join.
        /// </summary>
        public bool Join()
        {
            if (!_queued) return false;

            _fill.Synchronize();
            _queued = false;
            return true;
        }

        /// <summary>The bytes the last snapshot sends up: the corpses' columns and the bins.</summary>
        public long Bytes => _pos.Bytes + _joules.Bytes + _cells.Bytes + _binStart.Bytes;

        /// <summary>
        /// The card's grid into <paramref name="field"/> (<see cref="World.BringScentHome"/>), after
        /// the fill's stream is joined. The download is on the engine's stream.
        /// </summary>
        public void BringHome(ScentField field)
        {
            Join();
            _grid.Down();
            field.CopyCellsFrom(_grid.Host);
        }

        /// <summary>
        /// The order probe's check of one block, which the runner calls just after it joins the fill
        /// (SCENT_ORDER_PROBE builds with EVOSIM_GPU_SCENT_ORDER=1; nothing otherwise). On the engine's
        /// stream, where the block's readers run, it copies the generation the last fill stamped, then
        /// brings it home with the grid: the block must read the last snapshot's generation, and its
        /// grid must be the host's fill of that snapshot to the bit.
        /// </summary>
        public void Audit()
        {
#if SCENT_ORDER_PROBE
            if (!OrderOn) return;

            _copy(_stream, 1, _gen.View, _seen.View);
            _seen.Down();
            _grid.Down();
            _expected.CopyCellsTo(_want);

            int differ = 0;
            float[] got = _grid.Host;
            for (int c = 0; c < _want.Length; c++)
            {
                if (BitConverter.SingleToInt32Bits(got[c]) != BitConverter.SingleToInt32Bits(_want[c])) differ++;
            }

            _audits++;
            if (_seen.Host[0] != _generation) _genWrong++;
            if (differ > 0) _gridWrong++;
            if (differ > _mostCells) _mostCells = differ;
            if (_audits % 20 == 0) PrintOrder();
#endif
        }

#if SCENT_ORDER_PROBE
        // The order probe (card-next-r57.md Part 7), compiled only with -p:ScentOrderProbe=1 and on
        // only with EVOSIM_GPU_SCENT_ORDER=1 in the process's environment, which the binding never
        // sees, as with EVOSIM_GPU_PROBE.
        private static readonly bool OrderOn = Environment.GetEnvironmentVariable("EVOSIM_GPU_SCENT_ORDER") == "1";
        private readonly Col<int> _gen, _seen;
        private readonly ScentField _expected;
        private readonly float[] _want;
        private readonly Action<AcceleratorStream, Index1D, ArrayView<int>, int> _stamp;
        private readonly Action<AcceleratorStream, Index1D, ArrayView<int>, ArrayView<int>> _copy;
        private int _generation;
        private long _audits, _snapshots, _genWrong, _gridWrong, _mostCells;

        private void PrintOrder() =>
            GpuConsole.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-scent-order: blocks {0}, snapshots {1}, generation wrong {2}, grid wrong {3}, cells differing at most {4}",
                _audits, _snapshots, _genWrong, _gridWrong, _mostCells));

        private static void StampKernel(Index1D index, ArrayView<int> gen, int generation) => gen[0] = generation;

        private static void CopyKernel(Index1D index, ArrayView<int> from, ArrayView<int> to) => to[0] = from[0];
#endif

        public void Dispose()
        {
            _fill.Synchronize();
#if SCENT_ORDER_PROBE
            if (OrderOn) PrintOrder();
            _gen.Dispose();
            _seen.Dispose();
#endif
            _pos.Dispose();
            _joules.Dispose();
            _cells.Dispose();
            _binStart.Dispose();
            _fill.Dispose();
        }

        private static int Clamp(int i, int n) => i < 0 ? 0 : i >= n ? n - 1 : i;

        private static void FillKernel(Index1D index, ArrayView<float> grid, ArrayView<float> pos,
            ArrayView<double> joules, ArrayView<int> cells, ArrayView<int> binStart, Params p)
        {
            int cell = index;
            int k = cell % p.Nz;
            int row = cell / p.Nz;
            int j = row % p.Ny;
            int i = row / p.Ny;

            int bx = i / p.BinCells, bz = k / p.BinCells;
            int x0 = Math.Max(0, bx - 1), x1 = Math.Min(p.BinsX - 1, bx + 1);
            int z0 = Math.Max(0, bz - 1), z1 = Math.Min(p.BinsZ - 1, bz + 1);

            ArrayView<int> cursor = LocalMemory.Allocate<int>(9);
            ArrayView<int> end = LocalMemory.Allocate<int>(9);
            int bins = 0;
            for (int ix = x0; ix <= x1; ix++)
            {
                for (int iz = z0; iz <= z1; iz++)
                {
                    int b = ix * p.BinsZ + iz;
                    cursor[bins] = binStart[b];
                    end[bins] = binStart[b + 1];
                    bins++;
                }
            }

            // ScentField.AddRows' constants for this cell: the range squared, exact in a double
            // since the range is a float, and the cell's centre, (i + 0.5) times the cell's 2 m,
            // exact too.
            double range = p.Range;
            double r2 = M(range, range);
            double cx = M(i + 0.5, ScentField.CellMetres);
            double cy = M(-(j + 0.5), ScentField.CellMetres);
            double cz = M(k + 0.5, ScentField.CellMetres);
            int cap = p.Cap;

            float v = 0f;
            while (true)
            {
                // The next corpse in the list's order among the nine bins' heads.
                int best = -1, first = int.MaxValue;
                for (int q = 0; q < bins; q++)
                {
                    if (cursor[q] >= end[q]) continue;
                    int listed = cells[3 * cap + cursor[q]];
                    if (listed < first)
                    {
                        first = listed;
                        best = q;
                    }
                }

                if (best < 0) break;
                int c = cursor[best];
                cursor[best] = c + 1;

                // Inside the corpse's box: the host's loops run each axis over its cell plus and
                // minus the reach, clamped to the grid, which this cell is in. Long, so a cell far
                // outside the int range cannot wrap.
                long ci = cells[c], cj = cells[cap + c], ck = cells[2 * cap + c];
                if (i < ci - p.Reach || i > ci + p.Reach) continue;
                if (j < cj - p.Reach || j > cj + p.Reach) continue;
                if (k < ck - p.Reach || k > ck + p.Reach) continue;

                double dx = cx - pos[c];
                double xx = M(dx, dx);
                if (xx >= r2) continue;
                double dy = cy - pos[cap + c];
                double xy = xx + M(dy, dy);
                if (xy >= r2) continue;
                double dz = cz - pos[2 * cap + c];
                double s = xy + M(dz, dz);
                if (s >= r2) continue;
                double d = Math.Sqrt(s);
                if (d >= range) continue;
                v += (float)M(joules[c], 1.0 - d / range);
            }

            grid[cell] = v;
        }

        // An explicitly rounded product: never contracted into a fused multiply-add on the card, and
        // the plain product elsewhere, which .NET never fuses (GpuTransport.M).
        private static double M(double p, double q)
        {
            if (CudaAsm.IsSupported)
            {
                CudaAsm.Emit("mul.rn.f64 %0, %1, %2;", out double r, p, q);
                return r;
            }

            return p * q;
        }
    }
}
