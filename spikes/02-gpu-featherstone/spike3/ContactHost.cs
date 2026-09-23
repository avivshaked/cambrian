using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;

namespace Gpu.Spike3
{
    internal interface IContactRunner : IDisposable
    {
        double CompileMs { get; }
        string Precision { get; }
        int Buckets { get; }
        void UploadSpheres();
        void UploadSpheresTimed();
        void SetRecord(bool record);
        void SetLocal(bool local);
        void Build(bool serialMean);
        void Query();
        void Step(bool serialMean);
        void Memset();
        void Mean(bool serial);
        void RangesOnly();
        void ScanOnly();
        void ScatterOnly();
        void EmptyLaunch();
        void Spin(int iterations);
        void Synchronize();
        void ClearFlags();
        ContactResult Read();
        ulong Digest();
    }

    /// <summary>What the contact kernel wrote, widened to double, strided by body as on the card.</summary>
    internal sealed class ContactResult
    {
        public int N, CandCap, OverCap, MaxLinks;
        public int[] Found, Unique, Cand, NOver, Over, BedGlass;
        public double[] Push, PairSum, Force, Fext;
        public double Cell;
        public int Entries, EntryOverflow;
    }

    /// <summary>
    /// The host side of the contact kernel's inputs: every body's committed sphere, its masses
    /// and the world's constants, struct-of-arrays and strided by body.
    /// </summary>
    internal sealed class ContactHost
    {
        public int N, MaxLinks, CandCap, OverCap, EntryCap;
        public bool CreatureContact, HasBed;
        public double Omega, Zeta, MaxSep, MaxDv, Dt, WorldDepth, TankRadius, AxisX, AxisZ;
        public double BedRadius, BedDepth, TiltX, TiltZ, Offset;
        public double[] BedAmp = Array.Empty<double>(), BedKx = Array.Empty<double>(),
            BedKz = Array.Empty<double>(), BedPhase = Array.Empty<double>();

        public double[] Cx, Cy, Cz, R, Vx, Vy, Vz, M, LinkMass;
        public int[] Active, Links;
        public long[] Ids;

        public ContactHost(IReadOnlyList<Creature> bodies, SolverConfig s, int candCap, int overCap, int entryCap)
        {
            N = bodies.Count;
            int most = 1;
            foreach (Creature c in bodies) if (c.Links > most) most = c.Links;
            MaxLinks = most;
            CandCap = candCap;
            OverCap = overCap;
            EntryCap = entryCap;

            CreatureContact = s.CreatureContact;
            Omega = s.ContactOmega;
            Zeta = s.ContactDampingRatio;
            MaxSep = s.MaxSeparationSpeed;
            MaxDv = s.MaxContactSpeedChange;
            Dt = s.StepSeconds;
            WorldDepth = s.WorldDepthMetres;
            TankRadius = s.TankRadiusMetres;
            AxisX = s.TankAxisX;
            AxisZ = s.TankAxisZ;

            HasBed = s.Bed != null;
            if (HasBed)
            {
                // BedShape keeps its modes private; read them, never write them.
                BedShape bed = s.Bed;
                BedKx = (double[])Field(bed, "_kx");
                BedKz = (double[])Field(bed, "_kz");
                BedAmp = (double[])Field(bed, "_amplitude");
                BedPhase = (double[])Field(bed, "_phase");
                TiltX = (double)Field(bed, "_tiltX");
                TiltZ = (double)Field(bed, "_tiltZ");
                Offset = (double)Field(bed, "_offset");
                BedRadius = (double)Field(bed, "_radius");
                BedDepth = (double)bed.DepthMetres;
            }

            Cx = new double[N]; Cy = new double[N]; Cz = new double[N]; R = new double[N];
            Vx = new double[N]; Vy = new double[N]; Vz = new double[N]; M = new double[N];
            Active = new int[N];
            Links = new int[N];
            Ids = new long[N];
            LinkMass = new double[MaxLinks * N];

            for (int i = 0; i < N; i++)
            {
                Creature c = bodies[i];
                Links[i] = c.Links;
                Ids[i] = c.Id;
                for (int l = 0; l < c.Links; l++) LinkMass[l * N + i] = c.Mass[l];
            }

            Fill(bodies);
        }

        /// <summary>The committed spheres as they stand, what the next step's contact pass reads.</summary>
        public void Fill(IReadOnlyList<Creature> bodies)
        {
            for (int i = 0; i < N; i++)
            {
                Creature c = bodies[i];
                Cx[i] = c.ContactCentre.X; Cy[i] = c.ContactCentre.Y; Cz[i] = c.ContactCentre.Z;
                R[i] = c.ContactRadius;
                Vx[i] = c.ContactVelocity.X; Vy[i] = c.ContactVelocity.Y; Vz[i] = c.ContactVelocity.Z;
                M[i] = c.TotalMass;
                Active[i] = c.ContactActive ? 1 : 0;
            }
        }

        private static object Field(object o, string name) =>
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o)
            ?? throw new MissingFieldException(o.GetType().Name, name);
    }
}
