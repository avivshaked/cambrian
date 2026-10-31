using System;
using System.Reflection;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The guards against ILGPU 1.5.3's double maths on the card (card-audit-r57.md, the card in double
    /// hangs on a far body), and the farm's refusal of the card in double. Nothing here opens the card.
    /// </summary>
    public class GpuGuardTests
    {
        /// <summary>
        /// The double flavour's sine, cosine and tanh are System.Math's to the bit inside their
        /// bounds, and outside them are the same narrowed float for tanh and a finite number in
        /// [-1, 1] for the angles, so the accelerator's proofs keep their bits. Found by reflection,
        /// so that this project compiles before the kernels are regenerated.
        /// </summary>
        [Fact]
        public void TheDoubleGuardsAreSystemMathInsideTheirBounds()
        {
            Type step = typeof(Evosim.Farm.Gpu.Dbl.WholeStep);
            MethodInfo sin = Helper(step, "SinR"), cos = Helper(step, "CosR"), tanh = Helper(step, "TanhF");

            foreach (double x in new[] { 0.0, 0.3, -2.5, 10.0, 471.2, -1e5, 3.7e8, 9.99e8, -1e9 })
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Sin(x)), BitConverter.DoubleToInt64Bits((double)sin.Invoke(null, new object[] { x })));
                Assert.Equal(BitConverter.DoubleToInt64Bits(Math.Cos(x)), BitConverter.DoubleToInt64Bits((double)cos.Invoke(null, new object[] { x })));
            }

            // Past the guard the angle reaches ILGPU's loop small, so the loop turns a few times at most.
            MethodInfo reduced = Helper(step, "Reduced");
            foreach (double x in new[] { 1.0000001e9, -3e9, 1e12, -1e12, 1e17, -1e17, 1e300, -1.7e308 })
            {
                double r = (double)reduced.Invoke(null, new object[] { x });
                Assert.True(r >= -64 && r <= 64, "reduced " + x.ToString("R") + " to " + r.ToString("R"));

                double s = (double)sin.Invoke(null, new object[] { x }), c = (double)cos.Invoke(null, new object[] { x });
                Assert.True(s >= -1 && s <= 1, "sin " + x.ToString("R") + " gave " + s.ToString("R"));
                Assert.True(c >= -1 && c <= 1, "cos " + x.ToString("R") + " gave " + c.ToString("R"));
            }

            foreach (double x in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.True(double.IsNaN((double)sin.Invoke(null, new object[] { x })));
                Assert.True(double.IsNaN((double)cos.Invoke(null, new object[] { x })));
            }

            foreach (float a in new[] { 0f, 0.5f, -3f, 8.25f, 18.9f, 19.99f, 20f, 20.01f, -25f, 1e30f, -1e30f,
                                        float.PositiveInfinity, float.NegativeInfinity, float.NaN })
            {
                float cpu = (float)Math.Tanh(a);
                float card = (float)tanh.Invoke(null, new object[] { a });
                Assert.True(BitConverter.SingleToInt32Bits(cpu) == BitConverter.SingleToInt32Bits(card) ||
                            (float.IsNaN(cpu) && float.IsNaN(card)),
                            "tanh " + a.ToString("R") + ": " + cpu.ToString("R") + " against " + card.ToString("R"));
            }
        }

        /// <summary>
        /// The farm refuses the card in double before it opens a device, and leaves the CPU
        /// accelerator's double alone.
        /// </summary>
        [Fact]
        public void TheFarmRefusesTheCardInDouble()
        {
            var block = GpuTank.Launcher(perPart: true, reefs: false);
            block["EVOSIM_ENGINE"] = "gpu";
            block["EVOSIM_GPU_DEVICE"] = "cuda";
            block["EVOSIM_GPU_PRECISION"] = "double";
            EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));
            (_, SolverConfig solver) = GpuTank.Build(perPart: true, reefs: false);

            MethodInfo open = typeof(EnvBinding).Assembly.GetType("Evosim.Farm.FarmGpu", throwOnError: true)
                .GetMethod("Open", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            // The third argument is the world's shapes; the fourth is the device a pair shares, and null opens one of
            // the engine's own.
            var thrown = Assert.Throws<TargetInvocationException>(() => open.Invoke(null, new object[] { s, solver, Evosim.Core.PartShapeRegistry.Standard, null }));
            Assert.IsType<InvalidOperationException>(thrown.InnerException);
            Assert.Contains("single only", thrown.InnerException.Message);
        }

        /// <summary>
        /// D228's contact at the point is on the card (the kernels carry the spin, the vertices and the torque), so the
        /// farm no longer refuses it by name. Opened on the card in double, which the farm refuses for its own reason, it
        /// gets past the contact shape to that refusal, as the true shape at the centre and the spheres do. Nothing
        /// here opens a device.
        /// </summary>
        [Fact]
        public void TheFarmDoesNotRefuseContactAtThePointOnTheCard()
        {
            foreach (Evosim.Core.ContactShapeForm form in new[]
            {
                Evosim.Core.ContactShapeForm.Spheres,
                Evosim.Core.ContactShapeForm.TrueShape,
                Evosim.Core.ContactShapeForm.TrueShapeAtPoint,
            })
            {
                var block = GpuTank.Launcher(perPart: true, reefs: false);
                block["EVOSIM_ENGINE"] = "gpu";
                block["EVOSIM_GPU_DEVICE"] = "cuda";
                block["EVOSIM_GPU_PRECISION"] = "double";
                EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));
                (_, SolverConfig solver) = GpuTank.Build(perPart: true, reefs: false);
                solver.ContactShape = form;

                MethodInfo open = typeof(EnvBinding).Assembly.GetType("Evosim.Farm.FarmGpu", throwOnError: true)
                    .GetMethod("Open", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                var thrown = Assert.Throws<TargetInvocationException>(() => open.Invoke(null, new object[] { s, solver, Evosim.Core.PartShapeRegistry.Standard, null }));
                Assert.IsType<InvalidOperationException>(thrown.InnerException);
                Assert.Contains("single only", thrown.InnerException.Message);
                Assert.DoesNotContain("trueshapeatpoint", thrown.InnerException.Message);
            }
        }

        /// <summary>
        /// D233's family touches on the host through a hull of core points under a true-shape contact (D228). The card
        /// carries that hull where the force acts at the link's centre (<c>trueshape</c>): it packs each family link's
        /// points and reads them in its GJK and at its surfaces. At the level of the force at the contact point
        /// (<c>trueshapeatpoint</c>) the host touches the hull at its eight corner points, which the card does not
        /// carry, so the farm refuses a world whose shapes hold one that takes a form at that level, before it opens a
        /// device, naming the shape and D233. Under <c>trueshape</c> and under sphere contact the farm opens the world:
        /// asked for the card in double, which it refuses for its own reason, it gets past the family to that refusal.
        /// Nothing here opens a device. The world is otherwise one the card takes, so the family is what is refused.
        /// </summary>
        [Fact]
        public void TheFarmRefusesAFamilyShapeOnTheCardOnlyUnderTheTrueShapeAtThePointAndOpensItUnderTheTrueShapeAndSpheres()
        {
            var block = GpuTank.Launcher(perPart: true, reefs: false);
            block["EVOSIM_ENGINE"] = "gpu";
            block["EVOSIM_GPU_DEVICE"] = "cuda";
            block["EVOSIM_GPU_PRECISION"] = "single";
            EnvSettings s = EnvBinding.Read(EnvBinding.Of(block));
            var shapes = new Evosim.Core.PartShapeRegistry(
                new Evosim.Core.BoxShape(), new Evosim.Core.SuperellipsoidShape());

            MethodInfo open = typeof(EnvBinding).Assembly.GetType("Evosim.Farm.FarmGpu", throwOnError: true)
                .GetMethod("Open", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            // The force at the contact point touches a hull at its corner points, which the card does not carry.
            (_, SolverConfig atPoint) = GpuTank.Build(perPart: true, reefs: false);
            atPoint.ContactShape = Evosim.Core.ContactShapeForm.TrueShapeAtPoint;

            var thrown = Assert.Throws<TargetInvocationException>(() => open.Invoke(null, new object[] { s, atPoint, shapes, null }));
            Assert.IsType<InvalidOperationException>(thrown.InnerException);
            Assert.Contains(Evosim.Core.ShapeIds.Superellipsoid, thrown.InnerException.Message);
            Assert.Contains("D233", thrown.InnerException.Message);

            // Under the true shape at the centre and under sphere contact the family is not refused. The same call in
            // double on the card meets the refusal of the card in double instead (D157), which comes after the family's,
            // so the family was passed.
            var inDouble = GpuTank.Launcher(perPart: true, reefs: false);
            inDouble["EVOSIM_ENGINE"] = "gpu";
            inDouble["EVOSIM_GPU_DEVICE"] = "cuda";
            inDouble["EVOSIM_GPU_PRECISION"] = "double";
            EnvSettings doubled = EnvBinding.Read(EnvBinding.Of(inDouble));

            foreach (Evosim.Core.ContactShapeForm form in new[]
            {
                Evosim.Core.ContactShapeForm.Spheres,
                Evosim.Core.ContactShapeForm.TrueShape,
            })
            {
                (_, SolverConfig opened) = GpuTank.Build(perPart: true, reefs: false);
                opened.ContactShape = form;

                var passed = Assert.Throws<TargetInvocationException>(() => open.Invoke(null, new object[] { doubled, opened, shapes, null }));
                Assert.IsType<InvalidOperationException>(passed.InnerException);
                Assert.Contains("single only", passed.InnerException.Message);
                Assert.DoesNotContain("D233", passed.InnerException.Message);
            }
        }

        private static MethodInfo Helper(Type step, string name)
        {
            MethodInfo m = step.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.True(m != null, step.FullName + " has no " + name + ": the kernels are not the templates' output");
            return m;
        }
    }
}
