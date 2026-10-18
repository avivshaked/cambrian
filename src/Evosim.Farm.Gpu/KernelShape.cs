using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// What the card's time probe (EVOSIM_GPU_PROBE=1) prints once about the kernels themselves
    /// (logbook/specs/card-next-r57.md, part 3): how many of a kernel's groups an SM can hold,
    /// against the groups a step launches, the registers and local bytes the driver gave each
    /// thread, and what one launch costs. A diagnostic, never on in a scored run, and it changes no
    /// number: the empty kernel it launches touches no memory.
    /// </summary>
    internal static class KernelShape
    {
        // CUfunction_attribute (cuda.h): the driver's own count for a compiled kernel.
        private const int LocalBytes = 3, Registers = 4;

        [DllImport("nvcuda", EntryPoint = "cuFuncGetAttribute")]
        private static extern int CuFuncGetAttribute(out int value, int attribute, IntPtr function);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// One kernel's line: its group, the groups one launch of <paramref name="threads"/>
        /// threads makes, how many an SM holds by the occupancy calculator, and so what share of
        /// the card's room one launch fills. A launch under the card's room leaves SMs idle unless
        /// other work runs beside it.
        /// </summary>
        public static string Describe(Accelerator acc, string name, Delegate launcher, int group, long threads)
        {
            Kernel kernel;
            if (!launcher.TryGetKernel(out kernel)) return name + ": no kernel object";

            int g = group;
            if (g <= 0)
            {
                try { g = acc.EstimateGroupSize(kernel); }
                catch (Exception e) { return name + ": group not estimated (" + e.GetType().Name + ")"; }
            }

            long groups = threads <= 0 ? 0 : (threads + g - 1) / g;
            string held;
            long room = 0;
            try
            {
                int perSm = acc.EstimateMaxActiveGroupsPerMultiprocessor(kernel, g);
                room = (long)perSm * acc.NumMultiprocessors;
                held = string.Format(Inv, "{0} a SM, {1} on the card", perSm, room);
            }
            catch (Exception e)
            {
                held = "occupancy not estimated (" + e.GetType().Name + ")";
            }

            string fill = room > 0 ? string.Format(Inv, ", one launch fills {0:0.0}% of it", 100.0 * groups / room) : "";
            return string.Format(Inv, "{0}: group {1}, {2} threads in {3} groups; {4}{5}; {6}",
                name, g, threads, groups, held, fill, Driver(kernel));
        }

        private static string Driver(Kernel kernel)
        {
            if (!(kernel is CudaKernel cuda)) return "registers not read off the card";
            try
            {
                int e1 = CuFuncGetAttribute(out int regs, Registers, cuda.FunctionPtr);
                int e2 = CuFuncGetAttribute(out int local, LocalBytes, cuda.FunctionPtr);
                if (e1 != 0 || e2 != 0) return string.Format(Inv, "registers not read (CUDA error {0}, {1})", e1, e2);
                return string.Format(Inv, "{0} registers and {1} local bytes a thread", regs, local);
            }
            catch (Exception e)
            {
                return "registers not read (" + e.GetType().Name + ")";
            }
        }

        // ILGPU loads no kernel with its index alone, so the empty kernel takes a buffer it never reads.
        private static void Empty(Index1D i, ArrayView<int> unused)
        {
        }

        /// <summary>
        /// What a launch costs, from an empty kernel of one group: the mean of 200 launches each
        /// joined at once, which is what a probed step pays at each join, and the mean of 200
        /// launches queued and joined once, which is what an unprobed step pays a launch.
        /// </summary>
        public static string LaunchCost(Accelerator acc, int group, double launchesAStep)
        {
            Action<Index1D, ArrayView<int>> load = acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>>(Empty);
            using MemoryBuffer1D<int, Stride1D.Dense> buffer = acc.Allocate1D<int>(1);
            ArrayView<int> view = buffer.View;
            Action<Index1D> empty = n => load(n, view);
            int threads = Math.Max(1, group);
            for (int k = 0; k < 20; k++) empty(threads);
            acc.Synchronize();

            const int N = 200;
            long t0 = Stopwatch.GetTimestamp();
            for (int k = 0; k < N; k++)
            {
                empty(threads);
                acc.Synchronize();
            }
            long t1 = Stopwatch.GetTimestamp();
            for (int k = 0; k < N; k++) empty(threads);
            acc.Synchronize();
            long t2 = Stopwatch.GetTimestamp();

            double us = 1e6 / Stopwatch.Frequency / N;
            double joined = (t1 - t0) * us, queued = (t2 - t1) * us;
            return string.Format(Inv,
                "gpu-probe launches: an empty launch joined {0:0.0} us, queued {1:0.0} us; {2:0.0} launches a step, " +
                "so {3:0.000} ms a step queued and {4:0.000} ms joined",
                joined, queued, launchesAStep, launchesAStep * queued / 1000, launchesAStep * joined / 1000);
        }
    }
}
