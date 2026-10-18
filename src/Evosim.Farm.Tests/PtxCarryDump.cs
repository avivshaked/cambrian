using System;
using System.IO;
using System.Reflection;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime.Cuda;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// r59-pos64's measurement, not a guard: the single-precision class-0 step kernel's PTX, as ILGPU's PTX
    /// backend writes it for the card (SM_89, the engine's default level), with no card, into the checkout's
    /// scratch/r59-tools/step0.ptx, and ContactLinks' into contact-links.ptx.
    /// logbook/specs/r59-build/ptx-kahan.py finds the compensated sums in one, and ptx-cost.py sets two side by side.
    /// </summary>
    public class PtxCarryDump
    {
        [Theory]
        [Trait("Category", "Slow")]
        [InlineData("Step0", "step0.ptx")]
        [InlineData("ContactLinks", "contact-links.ptx")]
        public void KernelPtx(string name, string file)
        {
            using var context = Context.Create(b => b.EnableAlgorithms());
            MethodInfo method = typeof(Evosim.Farm.Gpu.Sgl.WholeStep).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            var backend = new PTXBackend(context, new CudaCapabilityContext(CudaArchitecture.SM_89),
                                         CudaArchitecture.SM_89, CudaInstructionSet.ISA_80, null);
            var kernel = (PTXCompiledKernel)backend.Compile(
                EntryPointDescription.FromImplicitlyGroupedKernel(method), default);
            File.WriteAllText(Path.Combine(DriftDiagnostic.OutDir(), file), kernel.PTXAssembly);
        }
    }
}
