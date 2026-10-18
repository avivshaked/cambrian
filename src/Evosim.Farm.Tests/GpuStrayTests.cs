using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The card's reach guard and the chemical sense's bounded walk (card-audit-r57.md). A stray
    /// sphere is planted as Evosim.Dynamics.Tests' ContactStrayTests plants one, in both engines,
    /// and the card must lose it as the CPU does: told to the handler with the same sphere, lost,
    /// and the rest of the world the CPU's to the bit. A body far out but in the grid's reach
    /// must not hang the chemical sense's walk.
    /// </summary>
    public class GpuStrayTests
    {
        private readonly ITestOutputHelper _out;

        public GpuStrayTests(ITestOutputHelper output) => _out = output;

        private static (DynamicsWorld world, World core) Pair(bool perPart)
        {
            (World core, SolverConfig solver) = GpuTank.Build(perPart, reefs: false);
            return (new DynamicsWorld(solver) { Threads = 2 }, core);
        }

        /// <summary>Six bodies as GpuCpuAcceleratorTests places them: a cluster, one at the glass, one on the bed.</summary>
        private static void Populate(DynamicsWorld world, World core, ulong seed)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < 6; k++)
            {
                var at = new Vec3(axis.X + 0.7 * (k % 3) - 0.7, -9.0 - 0.6 * (k / 3), axis.Z + 0.5 * (k % 2));
                if (k == 4) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                if (k == 5) at = new Vec3(axis.X - 2.0, -core.Config.WorldDepthMetres + 0.6, axis.Z - 2.0);
                world.AddInIdOrder(GpuTank.Body(1 + k, world.Config, core, rng, at));
            }
        }

        private static List<string> Listen(DynamicsWorld world)
        {
            var told = new List<string>();
            world.StrayHandler = (body, s) => told.Add(
                FormattableString.Invariant($"body {body.Id} at {s.Body} link {s.Link} at ({s.X:R}, {s.Y:R}, {s.Z:R}) r {s.Radius:R}"));
            return told;
        }

        // ---- the plants, ContactStrayTests' own

        private static void LinkAt1e12(Creature body)
        {
            body.Position[3] = 1e12;
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        private static void BodyAt1e12(Creature body) => body.PlaceAt(new Vec3(1e12, -5.0, 0.0), QuatD.Identity);

        private static void LinkAtNaN(Creature body)
        {
            body.Position[3] = double.NaN;
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        private static void RootAtNaN(Creature body)
        {
            body.Position[0] = double.NaN;
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        /// <summary>
        /// Not a stray yet: every link moving at 2e11 m/s, so the step's own integration carries the
        /// body some 4e9 m, past the grid's reach on a cell of 1 to 2 m, and the next step's grid
        /// finds the stray the physics made. Every link alike, so the joints are not torn.
        /// </summary>
        private static void Flung(Creature body)
        {
            for (int l = 0; l < body.Links; l++) body.Velocity[3 * l] = 2e11;
        }

        /// <summary>A told stray's body and link, without its sphere.</summary>
        private static string WhoStrayed(string told) => told.Substring(0, told.IndexOf(" at (", StringComparison.Ordinal));

        private static Action<Creature> Plant(string kind) => kind switch
        {
            "link1e12" => LinkAt1e12,
            "body1e12" => BodyAt1e12,
            "linkNaN" => LinkAtNaN,
            "rootNaN" => RootAtNaN,
            "flung" => Flung,
            _ => throw new ArgumentException(kind),
        };

        /// <summary>
        /// A block of ten steps, then two bodies stray in both engines, then two blocks of
        /// twenty-five. Both engines tell of the same two spheres, lose the same two bodies, and
        /// once the two are taken out, as the farm takes a dead body out at its reconcile, hold
        /// the same world to the bit. The lost bodies' own water is left out of that: the card
        /// samples a stray's water once in the step it strays, which nothing reads. A flung body
        /// samples its bed at angles past the double guard's 1e9 rad while still inside the reach,
        /// where the card's sine is reduced and the CPU's is not (KernelGenerator, AngleGuard), so
        /// its own state parts: for the flung plants the engines tell the same bodies and links,
        /// not the same spheres, and the digest before the two are taken out is not compared. The
        /// device's count of such angles must then be above 0, so the comparison is dropped only
        /// where the guard is known to have acted.
        /// </summary>
        [Theory]
        [InlineData(true, "link1e12")]
        [InlineData(false, "body1e12")]
        [InlineData(true, "linkNaN")]
        [InlineData(false, "rootNaN")]
        [InlineData(true, "flung")]
        [InlineData(false, "flung")]
        public void AStrayIsLostOnTheCpuAcceleratorAsTheCpuLosesIt(bool perPart, string kind)
        {
            (DynamicsWorld cpu, World coreA) = Pair(perPart);
            (DynamicsWorld gpu, World coreB) = Pair(perPart);
            Populate(cpu, coreA, 11UL);
            Populate(gpu, coreB, 11UL);
            List<string> toldCpu = Listen(cpu), toldGpu = Listen(gpu);

            // Resync: the card keeps a body's state between blocks and packs it again only at a
            // birth or a resize, so a plant made on the host between blocks reaches it only so.
            GpuOptions options = GpuTank.CpuDouble();
            options.Resync = true;
            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);

            cpu.StepBlock(10);
            gpu.StepBlock(10);

            Action<Creature> stray = Plant(kind);
            foreach (int k in new[] { 1, 3 })
            {
                stray(cpu.Creatures[k]);
                stray(gpu.Creatures[k]);
            }

            for (int block = 0; block < 2; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
            }

            foreach (string line in toldGpu) _out.WriteLine(line);

            Assert.Equal(2, toldCpu.Count);
            _out.WriteLine($"{kind}: {backend.BedAnglesPastGuard} bed angles past the double guard");
            if (kind == "flung")
            {
                Assert.True(backend.BedAnglesPastGuard > 0, "the flung bodies' spheres are not compared, yet no bed angle passed the guard");
                Assert.Equal(toldCpu.ConvertAll(WhoStrayed), toldGpu.ConvertAll(WhoStrayed));
            }
            else
            {
                Assert.Equal(toldCpu, toldGpu);
            }
            Assert.False(gpu.Creatures[1].Alive);
            Assert.False(gpu.Creatures[3].Alive);
            Assert.Equal(cpu.NonFiniteBodies(), gpu.NonFiniteBodies());
            if (kind != "flung") Assert.Equal(cpu.Digest(), gpu.Digest());

            foreach (long id in new long[] { cpu.Creatures[3].Id, cpu.Creatures[1].Id })
            {
                cpu.Remove(id);
                gpu.Remove(id);
            }

            cpu.StepBlock(10);
            gpu.StepBlock(10);

            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (!a.AsSpan().SequenceEqual(b)) Assert.Fail("The engines part after the strays: " + GpuTank.FirstDifference(cpu, gpu));
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// Bodies outside the glass but well inside the walk's far bound, near the field's
        /// outermost corner (just inside and just outside its radius) and beside the glass along
        /// an axis, where the walk's clamped columns hit first. The walk there is the CPU's, so the
        /// engines agree to the bit, the planted bodies included.
        /// </summary>
        [Fact]
        public void TheWalkNearTheFieldsEdgeIsTheCpusWalk()
        {
            (DynamicsWorld cpu, World coreA) = Pair(perPart: false);
            (DynamicsWorld gpu, World coreB) = Pair(perPart: false);
            Populate(cpu, coreA, 13UL);
            Populate(gpu, coreB, 13UL);

            Vec3 axis = GpuTank.Axis(coreA);
            double corner = coreA.TankRadiusMetres * Math.Sqrt(2.0);
            var places = new[]
            {
                new Vec3(axis.X + (corner - 0.5) / Math.Sqrt(2.0), -8.0, axis.Z + (corner - 0.5) / Math.Sqrt(2.0)),
                new Vec3(axis.X + (corner + 0.5) / Math.Sqrt(2.0), -8.0, axis.Z + (corner + 0.5) / Math.Sqrt(2.0)),
                new Vec3(axis.X - coreA.TankRadiusMetres - 3.0, -8.0, axis.Z + 0.25),
                new Vec3(axis.X + 0.25, -8.0, axis.Z + coreA.TankRadiusMetres + 40.0),
            };

            var rngA = new Rng(17UL);
            var rngB = new Rng(17UL);
            for (int k = 0; k < places.Length; k++)
            {
                cpu.AddInIdOrder(GpuTank.Body(100 + k, cpu.Config, coreA, rngA, places[k]));
                gpu.AddInIdOrder(GpuTank.Body(100 + k, gpu.Config, coreB, rngB, places[k]));
            }

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 2; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);

                byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
                if (!a.AsSpan().SequenceEqual(b)) Assert.Fail("The engines part at the field's edge: " + GpuTank.FirstDifference(cpu, gpu));
            }
        }

        /// <summary>
        /// Bodies 1e6, 1e7 and 1e8 m out, finite and inside the grid's reach (1e9 cells), where the
        /// CPU's walk from the body's distance would take millions of half-cell steps, and in single
        /// would never end past about 1.7e7 m on a 2 m cell. Each block must end; the bodies at
        /// home are the CPU's to the bit once the far ones are taken out.
        /// </summary>
        [Theory]
        [InlineData("double")]
        [InlineData("single")]
        public void AFarBodyDoesNotHangTheWalk(string precision)
        {
            (DynamicsWorld cpu, World coreA) = Pair(perPart: false);
            (DynamicsWorld gpu, World coreB) = Pair(perPart: false);
            Populate(cpu, coreA, 19UL);
            Populate(gpu, coreB, 19UL);

            // Bodies whose brains read the chemical channel, or the walk is never taken.
            var rng = new Rng(23UL);
            double[] far = { 1e6, 1e7, 1e8 };
            int chemical = 1 << (int)SensorChannel.Chemical;
            var pool = new[] { SensorChannel.Chemical };
            for (int k = 0; k < far.Length; k++)
            {
                var at = new Vec3(far[k], -8.0, 3.0);
                Creature body = GpuTank.Body(200 + k, gpu.Config, coreB, rng, at, sensorPool: pool);
                for (int draw = 0; (body.Brain.SensorMask & chemical) == 0; draw++)
                {
                    Assert.True(draw < 500, "no body that smells in 500 draws");
                    body = GpuTank.Body(200 + k, gpu.Config, coreB, rng, at, sensorPool: pool);
                }

                gpu.AddInIdOrder(body);
            }

            GpuOptions options = GpuTank.CpuDouble();
            options.Precision = precision;
            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);

            gpu.StepBlock(10);
            gpu.StepBlock(10);
            _out.WriteLine($"{precision}: {gpu.Steps} steps, {gpu.NonFiniteBodies()} lost of {gpu.Creatures.Count}");

            if (precision != "double") return;

            for (int k = 0; k < far.Length; k++) gpu.Remove(200 + k);
            cpu.StepBlock(10);
            cpu.StepBlock(10);
            Assert.Equal(cpu.Digest(), gpu.Digest());
        }

        /// <summary>
        /// The walk's refusal of a distance that is not finite, called directly: through a step it
        /// cannot be reached, because the reach guard loses a NaN or an infinite sphere before the
        /// senses run. It returns before reading the world, so an empty world and config serve. In
        /// single a finite 3e38 m squares to infinity, the case the branch is for.
        /// </summary>
        /// <remarks>
        /// Found by reflection, so that this project compiles before the kernels are regenerated:
        /// the regeneration is itself a test in this project (GpuKernelSourceTests).
        /// </remarks>
        [Fact]
        public void TheWalkRefusesADistanceThatIsNotFinite()
        {
            var dbl = new Evosim.Farm.Gpu.Dbl.SCfg { CellMetres = 1f, FieldTankR = 10f, Nx = 20, Ny = 20, Nz = 20 };
            var sgl = new Evosim.Farm.Gpu.Sgl.SCfg { CellMetres = 1f, FieldTankR = 10f, Nx = 20, Ny = 20, Nz = 20 };
            Type d = typeof(Evosim.Farm.Gpu.Dbl.WholeStep), f = typeof(Evosim.Farm.Gpu.Sgl.WholeStep);

            foreach (float x in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Equal(-1, TankCellAt(d, x, 3f, dbl));
                Assert.Equal(-1, TankCellAt(f, x, 3f, sgl));
                Assert.Equal(-1, TankCellAt(f, 3f, x, sgl));
            }

            Assert.Equal(-1, TankCellAt(f, 3e38f, 3f, sgl));
        }

        private static int TankCellAt(Type step, float x, float z, object cfg)
        {
            MethodInfo walk = step.GetMethod("TankCellAt", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.True(walk != null, step.FullName + " has no TankCellAt: the kernels are not the templates' output");
            object world = Activator.CreateInstance(walk.GetParameters()[3].ParameterType);
            return (int)walk.Invoke(null, new object[] { x, -8f, z, world, cfg });
        }

        /// <summary>
        /// The same plants on the card, in single and in double. It runs only with EVOSIM_GPU_STRAY_CARD=1, on
        /// the coordinator's go, under the driver watch and alone on the machine: a hole in the
        /// guard ends in a driver reset. The bodies are lost, nothing overflows, the rest step on.
        /// EVOSIM_GPU_STRAY_DEVICE=cpu runs it on ILGPU's CPU accelerator instead, off the card;
        /// EVOSIM_GPU_STRAY_CASE=perPart,kind,precision (as "True,flung,single") runs that case alone
        /// and returns at once from the rest; EVOSIM_GPU_STRAY_TRACE names a file that gets a line
        /// as the case begins and after each block, so a case that never ends says where it stopped.
        /// </summary>
        [Theory]
        [Trait("Category", "Card")]
        [InlineData(true, "link1e12", "single")]
        [InlineData(false, "body1e12", "single")]
        [InlineData(true, "linkNaN", "single")]
        [InlineData(false, "rootNaN", "single")]
        [InlineData(true, "flung", "single")]
        [InlineData(false, "flung", "single")]
        [InlineData(true, "link1e12", "double")]
        [InlineData(false, "body1e12", "double")]
        [InlineData(true, "linkNaN", "double")]
        [InlineData(false, "rootNaN", "double")]
        [InlineData(true, "flung", "double")]
        [InlineData(false, "flung", "double")]
        public void AStrayIsLostOnTheCard(bool perPart, string kind, string precision)
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_STRAY_CARD") != "1")
            {
                _out.WriteLine("not run: the card's stray test needs EVOSIM_GPU_STRAY_CARD=1 and the coordinator's go");
                return;
            }

            string name = FormattableString.Invariant($"{perPart},{kind},{precision}");
            string only = Environment.GetEnvironmentVariable("EVOSIM_GPU_STRAY_CASE");
            if (!string.IsNullOrEmpty(only) && only != name) return;

            string tracePath = Environment.GetEnvironmentVariable("EVOSIM_GPU_STRAY_TRACE");
            void Trace(string what)
            {
                if (string.IsNullOrEmpty(tracePath)) return;
                System.IO.File.AppendAllText(tracePath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
                    " " + name + " " + what + Environment.NewLine);
            }

            bool onCpu = Environment.GetEnvironmentVariable("EVOSIM_GPU_STRAY_DEVICE") == "cpu";
            GpuOptions options = onCpu
                ? new GpuOptions { Device = "cpu", Precision = precision, CpuThreads = 4, Resync = true }
                : new GpuOptions { Device = "cuda", Precision = precision, Resync = true };

            Trace("begins on " + options.Device);
            (DynamicsWorld gpu, World core) = Pair(perPart);
            Populate(gpu, core, 11UL);
            List<string> told = Listen(gpu);

            using var backend = new GpuBackend(gpu.Config, options);

            // The accelerator ILGPU gave, by its own type and name, before anything steps: a case
            // that asked for the CPU and got anything else refuses here.
            object acc = typeof(GpuBackend).GetField("_accelerator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(backend);
            string accType = acc.GetType().GetProperty("AcceleratorType").GetValue(acc).ToString();
            string accName = (string)acc.GetType().GetProperty("Name").GetValue(acc);
            Trace("accelerator " + accType + " (" + acc.GetType().Name + ") " + accName);
            if (onCpu && accType != "CPU")
            {
                throw new InvalidOperationException("asked for the CPU accelerator and was given " + accType + " " + accName);
            }

            gpu.UseBackend(backend);
            gpu.StepBlock(10);
            Trace("first block done at step " + gpu.Steps);

            Action<Creature> stray = Plant(kind);
            stray(gpu.Creatures[1]);
            stray(gpu.Creatures[3]);

            var before = new Dictionary<long, double>();
            foreach (Creature c in gpu.Creatures) before[c.Id] = c.Position[1];

            Trace("planted; second block begins");
            gpu.StepBlock(25);
            Trace("second block done at step " + gpu.Steps + ", " + told.Count + " told");
            gpu.StepBlock(25);
            Trace("third block done at step " + gpu.Steps + ", " + told.Count + " told");

            foreach (string line in told) _out.WriteLine(line);
            _out.WriteLine($"{kind} in {precision}: {told.Count} told as strays, {gpu.NonFiniteBodies()} lost");

            // A flung body may overflow to infinity before its sphere strays, and is then
            // lost by the finiteness check instead: lost either way, and told at most once.
            if (kind == "flung") Assert.True(told.Count <= 2);
            else Assert.Equal(2, told.Count);
            Assert.False(gpu.Creatures[1].Alive);
            Assert.False(gpu.Creatures[3].Alive);
            Assert.Equal(0L, backend.OverflowTotal);

            foreach (Creature c in gpu.Creatures)
            {
                if (c == gpu.Creatures[1] || c == gpu.Creatures[3]) continue;
                Assert.True(c.Alive, $"body {c.Id} was lost beside the strays");
                Assert.NotEqual(before[c.Id], c.Position[1]);
            }
        }
    }
}
