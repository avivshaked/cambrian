// The current-mode probe (open-ocean-current.md): the card's cost per point of today's streams
// (StreamsUnitWithGradient, a copy of gpu-kernel.template's in single precision) against the open
// square's torus field at 24 to 81 modes, both with velocity, clock derivative and Jacobian, which
// is what the card's acceleration reads. The bed map that wraps either field is the same cost both
// ways and is left out. Run: ModeProbe.exe [points] [repeats].
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

internal static class Program
{
    private const float L = 300f, Depth = 30f, TankR = 150f;
    private const int Terms = 24, Cells = 3;

    // Today's instant table, at the acceleration's instant (B): per term an envelope, its rate, cos, sin;
    // per cell an envelope, its rate, a turn, its rate.
    private const int BEnv = 0, BEnvRate = Terms, BCos = 2 * Terms, BSin = 3 * Terms;
    private const int BCellEnv = 4 * Terms, BCellEnvRate = 4 * Terms + Cells, BCell = 4 * Terms + 2 * Cells,
        BCellRate = 4 * Terms + 3 * Cells;

    // WR: per term an amplitude and a rate; per cell an amplitude.
    private const int StreamAmpAt = 0, StreamRateAt = Terms, CellAmpAt = 2 * Terms;

    private static void Main(string[] args)
    {
        int points = args.Length > 0 ? int.Parse(args[0]) : 1 << 20;
        int repeats = args.Length > 1 ? int.Parse(args[1]) : 40;

        using var context = Context.Create(b => b.Cuda().EnableAlgorithms().Optimize(OptimizationLevel.O2));
        using var accelerator = context.CreateCudaAccelerator(0);
        Console.WriteLine($"device {accelerator.Name}, {points} points, {repeats} repeats, single precision");

        var rng = new Random(62);
        var pos = new float[3 * points];
        for (int i = 0; i < points; i++)
        {
            pos[i] = (float)(rng.NextDouble() * L);
            pos[points + i] = (float)(-rng.NextDouble() * Depth);
            pos[2 * points + i] = (float)(rng.NextDouble() * L);
        }

        using var dPos = accelerator.Allocate1D(pos);
        using var dOut = accelerator.Allocate1D<float>(points);

        // ---- today's streams
        var inst = Enumerable.Range(0, 4 * Terms + 4 * Cells).Select(_ => (float)(rng.NextDouble() - 0.5)).ToArray();
        var wr = Enumerable.Range(0, 2 * Terms + Cells).Select(_ => (float)(rng.NextDouble() - 0.5)).ToArray();
        using var dInst = accelerator.Allocate1D(inst);
        using var dWr = accelerator.Allocate1D(wr);

        var old = accelerator.LoadAutoGroupedStreamKernel<Index1D, int, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>>(OldKernel);
        double oldMs = Time(accelerator, repeats, () => old(points, points, dPos.View, dInst.View, dWr.View, dOut.View));
        double oldNs = oldMs * 1e6 / points;
        Console.WriteLine($"today's streams (24 terms + 3 cells): {oldMs:F3} ms, {oldNs:F3} ns a point");

        var trig = accelerator.LoadAutoGroupedStreamKernel<Index1D, int, int, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<float>>(TorusTrigKernel);
        var powers = accelerator.LoadAutoGroupedStreamKernel<Index1D, int, int, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<float>>(TorusPowersKernel);

        Console.WriteLine("vectors  modes   trig ms  ns/pt  x today   powers ms  ns/pt  x today   max |trig-powers| / rms");
        foreach (int vectors in new[] { 8, 20, 25, 27 })
        {
            (float[] table, int[] mn) = Torus(vectors, rng);
            using var dTable = accelerator.Allocate1D(table);
            using var dMn = accelerator.Allocate1D(mn);
            using var dOut2 = accelerator.Allocate1D<float>(points);

            double trigMs = Time(accelerator, repeats, () => trig(points, points, vectors, dPos.View, dTable.View, dMn.View, dOut.View));
            float[] a = dOut.GetAsArray1D();
            double powMs = Time(accelerator, repeats, () => powers(points, points, vectors, dPos.View, dTable.View, dMn.View, dOut2.View));
            float[] b = dOut2.GetAsArray1D();

            double rms = Math.Sqrt(a.Average(v => (double)v * v));
            double diff = a.Zip(b, (p, q) => Math.Abs((double)p - q)).Max();

            Console.WriteLine(
                $"{vectors,7}  {3 * vectors,5}   {trigMs,7:F3}  {trigMs * 1e6 / points,5:F3}  {trigMs / oldMs,7:F2}   " +
                $"{powMs,9:F3}  {powMs * 1e6 / points,5:F3}  {powMs / oldMs,7:F2}   {diff / rms:E2}");
        }
    }

    private static double Time(Accelerator accelerator, int repeats, Action launch)
    {
        for (int i = 0; i < 3; i++) launch();
        accelerator.Synchronize();

        var times = new List<double>();
        var watch = new Stopwatch();
        for (int i = 0; i < repeats; i++)
        {
            watch.Restart();
            launch();
            accelerator.Synchronize();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        return times[times.Count / 2];
    }

    /// <summary>
    /// The torus's vectors: whole (m, n) with 2 ≤ |(m, n)| ≤ 10, one of each ± pair, spread by
    /// length; per (vector, q) the amplitude's cos and sin and their clock rates.
    /// </summary>
    private static (float[] table, int[] mn) Torus(int vectors, Random rng)
    {
        var lattice = new List<(int m, int n)>();
        for (int m = 0; m <= 10; m++)
        {
            for (int n = -10; n <= 10; n++)
            {
                if (m == 0 && n <= 0) continue;
                int k2 = m * m + n * n;
                if (k2 < 4 || k2 > 100) continue;
                lattice.Add((m, n));
            }
        }

        lattice.Sort((p, q) => (p.m * p.m + p.n * p.n).CompareTo(q.m * q.m + q.n * q.n));
        var chosen = Enumerable.Range(0, vectors).Select(i => lattice[i * (lattice.Count - 1) / Math.Max(1, vectors - 1)]).ToList();

        var table = new float[vectors * 14];
        var mn = new int[2 * vectors];
        for (int v = 0; v < vectors; v++)
        {
            mn[2 * v] = chosen[v].m;
            mn[2 * v + 1] = chosen[v].n;
            table[14 * v] = (float)(2 * Math.PI * chosen[v].m / L);
            table[14 * v + 1] = (float)(2 * Math.PI * chosen[v].n / L);
            for (int k = 2; k < 14; k++) table[14 * v + k] = (float)(rng.NextDouble() - 0.5);
        }

        return (table, mn);
    }

    // ================================================================ today's streams, copied

    private static void OldKernel(Index1D i, int n, ArrayView<float> pos, ArrayView<float> inst, ArrayView<float> wr, ArrayView<float> output)
    {
        float x = pos[i], y = pos[n + i], z = pos[2 * n + i];
        const float eddyWeight = 1f, overturning = 0.3f;
        float depth = Depth, radius = TankR;

        if (y > 0) y = 0;
        else if (y < -depth) y = -depth;

        bool atFace = y >= 0 || y <= -depth;

        float dx = x - radius;
        float dz = z - radius;
        float r = XMath.Sqrt(dx * dx + dz * dz);

        float cosTheta = r > 0 ? dx / r : 1;
        float sinTheta = r > 0 ? dz / r : 0;

        if (r > radius)
        {
            dx = radius * cosTheta;
            dz = radius * sinTheta;
            r = radius;
        }

        float s = r / radius;
        float u = s * s;

        float phi = (float)Math.PI * y / depth;
        float cosPhi = XMath.Cos(phi);
        float sinPhi = XMath.Sin(phi);

        float sin1 = atFace ? 0 : sinPhi;
        float sin2 = atFace ? 0 : 2f * sinPhi * cosPhi;
        float sin3 = atFace ? 0 : sinPhi * (4f * cosPhi * cosPhi - 1f);

        float cos1 = cosPhi;
        float cos2 = cosPhi * cosPhi - sinPhi * sinPhi;
        float cos3 = cosPhi * (4f * cosPhi * cosPhi - 3f);

        float invR = 1f / radius;
        float gx = 2f * dx * invR * invR;
        float gz = 2f * dz * invR * invR;

        float eVx = 0, eVz = 0;
        float eTx = 0, eTz = 0;
        float eXx = 0, eXy = 0, eXz = 0;
        float eZx = 0, eZy = 0, eZz = 0;

        float ca = 0, sa = 0;
        float cb = 1, sb = 0;
        float cc = s * cosTheta, sc = s * sinTheta;
        float cd = s * (cc * cosTheta - sc * sinTheta);
        float sd = s * (sc * cosTheta + cc * sinTheta);

        int k = 0;

        for (int m = 1; m <= 4; m++)
        {
            float mp = (m + 1) * invR;
            float mm = (m - 1) * invR;

            for (int j = 1; j <= 2; j++)
            {
                float alpha, alphaU, beta, betaU;

                if (j == 1)
                {
                    alpha = 1;
                    alphaU = 0;
                    beta = m - (m + 1) * u;
                    betaU = -(m + 1);
                }
                else
                {
                    alpha = 3f - 4f * u;
                    alphaU = -4;
                    beta = m - 3f * (m + 1) * u + (2 * m + 4) * u * u;
                    betaU = -3f * (m + 1) + 2f * (2 * m + 4) * u;
                }

                float bc = 0, bs = 0;
                float yc = 0, ys = 0;
                float tc = 0, ts = 0;
                float wc = 0, ws = 0;

                for (int qq = 1; qq <= 3; qq++, k++)
                {
                    float profile = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;
                    float ky = qq * (float)Math.PI / depth;
                    float slope = ky * (qq == 1 ? cos1 : qq == 2 ? cos2 : cos3);

                    float raw = eddyWeight * wr[StreamAmpAt + k];
                    float envelope = inst[BEnv + k];

                    float b = raw * envelope * profile;
                    float by = raw * envelope * slope;
                    float bt = raw * inst[BEnvRate + k] * profile;
                    float bw = b * wr[StreamRateAt + k];

                    float cosPsi = inst[BCos + k];
                    float sinPsi = inst[BSin + k];

                    bc += b * cosPsi;
                    bs += b * sinPsi;
                    yc += by * cosPsi;
                    ys += by * sinPsi;
                    tc += bt * cosPsi;
                    ts += bt * sinPsi;
                    wc += bw * cosPsi;
                    ws += bw * sinPsi;
                }

                float psD = sd * bc + cd * bs, pcD = cd * bc - sd * bs;
                float psC = sc * bc + cc * bs, pcC = cc * bc - sc * bs;
                float psB = sb * bc + cb * bs, pcB = cb * bc - sb * bs;
                float psA = sa * bc + ca * bs, pcA = ca * bc - sa * bs;

                float ysD = sd * yc + cd * ys, ycD = cd * yc - sd * ys;
                float ysB = sb * yc + cb * ys, ycB = cb * yc - sb * ys;

                float tsD = sd * tc + cd * ts, tcD = cd * tc - sd * ts;
                float tsB = sb * tc + cb * ts, tcB = cb * tc - sb * ts;

                float wsD = sd * wc + cd * ws, wcD = cd * wc - sd * ws;
                float wsB = sb * wc + cb * ws, wcB = cb * wc - sb * ws;

                eVx += alpha * psD + beta * psB;
                eVz += -alpha * pcD + beta * pcB;

                eXx += alphaU * gx * psD + alpha * mp * psC + betaU * gx * psB + beta * mm * psA;
                eXz += alphaU * gz * psD + alpha * mp * pcC + betaU * gz * psB + beta * mm * pcA;
                eZx += -alphaU * gx * pcD - alpha * mp * pcC + betaU * gx * pcB + beta * mm * pcA;
                eZz += -alphaU * gz * pcD + alpha * mp * psC + betaU * gz * pcB - beta * mm * psA;

                eXy += alpha * ysD + beta * ysB;
                eZy += -alpha * ycD + beta * ycB;

                eTx += alpha * tsD + beta * tsB + alpha * wcD + beta * wcB;
                eTz += -alpha * tcD + beta * tcB + alpha * wsD - beta * wsB;
            }

            ca = cb; sa = sb;
            cb = cc; sb = sc;
            cc = cd; sc = sd;

            float nextC = s * (cc * cosTheta - sc * sinTheta);
            float nextS = s * (sc * cosTheta + cc * sinTheta);
            cd = nextC; sd = nextS;
        }

        float Vx = -eVx * invR, Vz = -eVz * invR, Vy = 0;
        float Tx = -eTx * invR, Tz = -eTz * invR, Ty = 0;
        float Xx = -eXx * invR, Xy = -eXy * invR, Xz = -eXz * invR;
        float Zx = -eZx * invR, Zy = -eZy * invR, Zz = -eZz * invR;
        float Yx = 0, Yy = 0, Yz = 0;

        float wall = 1f - s;
        float wallSquared = wall * wall;
        float wallSlope = -2f * wall;

        float vertical = 2f * wall * (1f - 2f * s);
        float verticalSlope = 2f * (4f * s - 3f);

        float sx = r > 0 ? dx / (r * radius) : 0;
        float sz = r > 0 ? dz / (r * radius) : 0;

        for (int c = 0; c < Cells; c++)
        {
            int qq = c + 1;
            float ky = qq * (float)Math.PI / depth;

            float raw = overturning * wr[CellAmpAt + c];
            float envelope = inst[BCellEnv + c];
            float turn = inst[BCell + c];

            float amplitude = raw * envelope * turn;
            float rate = raw * (inst[BCellEnvRate + c] * turn + envelope * inst[BCellRate + c]);

            float cosKy = qq == 1 ? cos1 : qq == 2 ? cos2 : cos3;
            float sinKy = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;

            float horizontal = amplitude * ky * cosKy;
            float horizontalRate = rate * ky * cosKy;

            Vx -= horizontal * wallSquared * dx;
            Vz -= horizontal * wallSquared * dz;
            Vy += amplitude * vertical * sinKy;

            Xx -= horizontal * (wallSquared + wallSlope * sx * dx);
            Xz -= horizontal * wallSlope * sz * dx;
            Zx -= horizontal * wallSlope * sx * dz;
            Zz -= horizontal * (wallSquared + wallSlope * sz * dz);

            Xy += amplitude * ky * ky * sinKy * wallSquared * dx;
            Zy += amplitude * ky * ky * sinKy * wallSquared * dz;

            Yx += amplitude * verticalSlope * sx * sinKy;
            Yz += amplitude * verticalSlope * sz * sinKy;
            Yy += amplitude * vertical * ky * cosKy;

            Tx -= horizontalRate * wallSquared * dx;
            Tz -= horizontalRate * wallSquared * dz;
            Ty += rate * vertical * sinKy;
        }

        output[i] = Vx + Vy + Vz + Tx + Ty + Tz + Xx + Xy + Xz + Yx + Yy + Yz + Zx + Zy + Zz;
    }

    // ================================================================ the torus field

    // ψ = Σ_v Σ_q sin(qπη)·(C cos θ + S sin θ), θ = k_v·x, the potential's y component; the velocity
    // is curl(ψ ŷ) = (−∂ψ/∂z, 0, ∂ψ/∂x). Per (v, q) the table holds C, S, dC/dt, dS/dt; per v also
    // kx and kz (14 floats a vector).

    private static void TorusTrigKernel(Index1D i, int n, int vectors, ArrayView<float> pos, ArrayView<float> table, ArrayView<int> mn, ArrayView<float> output)
    {
        float x = pos[i], y = pos[n + i], z = pos[2 * n + i];
        Vertical(y, out float s1, out float s2, out float s3, out float d1, out float d2, out float d3);

        var acc = default(Acc);
        for (int v = 0; v < vectors; v++)
        {
            int at = 14 * v;
            float kx = table[at], kz = table[at + 1];
            float theta = kx * x + kz * z;
            float sn = XMath.Sin(theta), cs = XMath.Cos(theta);
            Mode(ref acc, table, at, kx, kz, cs, sn, s1, s2, s3, d1, d2, d3);
        }

        output[i] = acc.Sum();
    }

    private static void TorusPowersKernel(Index1D i, int n, int vectors, ArrayView<float> pos, ArrayView<float> table, ArrayView<int> mn, ArrayView<float> output)
    {
        float x = pos[i], y = pos[n + i], z = pos[2 * n + i];
        Vertical(y, out float s1, out float s2, out float s3, out float d1, out float d2, out float d3);

        // The base phasors and their whole powers 0..10, by repeated multiplication.
        float ax = 2f * (float)Math.PI * x / L, az = 2f * (float)Math.PI * z / L;
        float bxc = XMath.Cos(ax), bxs = XMath.Sin(ax), bzc = XMath.Cos(az), bzs = XMath.Sin(az);

        var exc = LocalMemory.Allocate<float>(11);
        var exs = LocalMemory.Allocate<float>(11);
        var ezc = LocalMemory.Allocate<float>(11);
        var ezs = LocalMemory.Allocate<float>(11);
        exc[0] = 1; exs[0] = 0; ezc[0] = 1; ezs[0] = 0;
        for (int p = 1; p <= 10; p++)
        {
            exc[p] = exc[p - 1] * bxc - exs[p - 1] * bxs;
            exs[p] = exs[p - 1] * bxc + exc[p - 1] * bxs;
            ezc[p] = ezc[p - 1] * bzc - ezs[p - 1] * bzs;
            ezs[p] = ezs[p - 1] * bzc + ezc[p - 1] * bzs;
        }

        var acc = default(Acc);
        for (int v = 0; v < vectors; v++)
        {
            int at = 14 * v;
            int m = mn[2 * v], nn = mn[2 * v + 1];
            float xc = exc[m], xs = exs[m];
            float zc = ezc[nn < 0 ? -nn : nn], zs = nn < 0 ? -ezs[-nn] : ezs[nn];
            float cs = xc * zc - xs * zs, sn = xs * zc + xc * zs;
            Mode(ref acc, table, at, table[at], table[at + 1], cs, sn, s1, s2, s3, d1, d2, d3);
        }

        output[i] = acc.Sum();
    }

    private struct Acc
    {
        public float Vx, Vz, Tx, Tz, Xx, Xy, Xz, Zx, Zy, Zz;

        public float Sum() => Vx + Vz + Tx + Tz + Xx + Xy + Xz + Zx + Zy + Zz;
    }

    private static void Vertical(float y, out float s1, out float s2, out float s3, out float d1, out float d2, out float d3)
    {
        float phi = (float)Math.PI * -y / Depth;
        float c = XMath.Cos(phi), s = XMath.Sin(phi);
        float k = (float)Math.PI / Depth;
        s1 = s; s2 = 2f * s * c; s3 = s * (4f * c * c - 1f);
        d1 = k * c; d2 = 2f * k * (c * c - s * s); d3 = 3f * k * c * (4f * c * c - 3f);
    }

    private static void Mode(ref Acc a, ArrayView<float> t, int at, float kx, float kz, float cs, float sn,
        float s1, float s2, float s3, float d1, float d2, float d3)
    {
        // Per q: P = C cos + S sin, P' = −C sin + S cos, and the clock's dP'.
        float p = 0, pd = 0, py = 0, pt = 0;
        for (int q = 0; q < 3; q++)
        {
            int o = at + 2 + 4 * q;
            float f = q == 0 ? s1 : q == 1 ? s2 : s3;
            float fy = q == 0 ? d1 : q == 1 ? d2 : d3;
            float c = t[o], s = t[o + 1], dc = t[o + 2], ds = t[o + 3];
            float P = c * cs + s * sn;
            float P1 = s * cs - c * sn;
            p += f * P;
            pd += f * P1;
            py += fy * P1;
            pt += f * (ds * cs - dc * sn);
        }

        // u_x = −Σ f kz P', u_z = Σ f kx P'; P'' = −P.
        a.Vx -= kz * pd;
        a.Vz += kx * pd;
        a.Tx -= kz * pt;
        a.Tz += kx * pt;
        a.Xx += kz * kx * p;
        a.Xz += kz * kz * p;
        a.Xy -= kz * py;
        a.Zx -= kx * kx * p;
        a.Zz -= kx * kz * p;
        a.Zy += kx * py;
    }
}
