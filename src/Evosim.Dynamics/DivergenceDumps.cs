using System;
using System.Globalization;
using System.IO;
using System.Text;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// What the caller knows about one body it is about to kill, and this assembly does not.
    /// </summary>
    /// <remarks>
    /// Age, generation and the genome are the organism's, which lives in Core's world; the step
    /// and the second are the farm loop's clock. The solver knows the body. Handing them over as
    /// one object rather than as nine arguments keeps a call site readable and makes an omission
    /// visible at the call site rather than at the eleventh comma.
    /// </remarks>
    public sealed class DivergedBody
    {
        /// <summary>The body, at the state the guards condemned it in.</summary>
        public Creature Body;

        /// <summary>The world it was in.</summary>
        public SolverConfig Config;

        /// <summary>The physics step it died on — the farm's <c>Steps + 1</c>.</summary>
        public long PhysicsStep;

        /// <summary>The simulated second the world stood at.</summary>
        public double ElapsedSeconds;

        /// <summary>Which guard condemned it, or null.</summary>
        public string Reason;

        /// <summary>The creature's genome, or null when the caller has none.</summary>
        public Genome Genome;

        /// <summary>The organism's age, s.</summary>
        public double AgeSeconds;

        /// <summary>The organism's generation depth.</summary>
        public int GenerationDepth;

        /// <summary>The last height the economy observed for it, m.</summary>
        public double LastObservedHeightY;

        /// <summary>
        /// The last root position known to be finite, or null to take the newest held trace
        /// frame's root — and the body's own root when it holds no frame.
        /// </summary>
        public Vec3? LastRootPosition;
    }

    /// <summary>
    /// A diverged body's post-mortem and its throw trace, in the farm's two file shapes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Best-effort, and never allowed to take the run down.</b> The run continues after a
    /// divergence — that is the whole point — and losing the record of one body is a smaller
    /// loss than losing the rest of the arm to an IO error while writing it. The farm's rule,
    /// and its two nested try/catches: the trace's failure cannot lose the post-mortem that is
    /// already on disk.
    /// </para>
    /// <para>
    /// <b>Fifty dumps and no more</b> — <c>Ecosystem.MaxDumps</c>. Past that the count is still
    /// counted and the anatomy is not kept, which is a censoring a reader has to be told about:
    /// <see cref="Written"/> against the caller's own <c>diverged</c> column is what says so.
    /// </para>
    /// <para>
    /// <b>Every number goes out through a writer that admits a non-finite one.</b>
    /// <see cref="Json.Writer"/> refuses NaN and infinity, correctly, everywhere else in this
    /// project. These two files are the exception — they are written <i>because</i> something
    /// stopped being finite — so such a value goes in as its own name, in quotes, and the reader
    /// sees which slot it was rather than a missing key.
    /// </para>
    /// </remarks>
    public sealed class DivergenceDumps
    {
        /// <summary>The farm's cap, unchanged.</summary>
        public const int MaxDumps = 50;

        private readonly int _cap;

        /// <param name="directory">Where the files go — the run's <c>diverged/</c>.</param>
        /// <param name="cap">The dump cap; the default is the farm's fifty.</param>
        public DivergenceDumps(string directory, int cap = MaxDumps)
        {
            Directory = directory;
            _cap = cap;
        }

        /// <summary>Where the files go. Created on the first dump and not before.</summary>
        public string Directory { get; }

        /// <summary>Post-mortems written so far, at most <see cref="MaxDumps"/>.</summary>
        public int Written { get; private set; }

        /// <summary>
        /// Writes <c>&lt;id&gt;.json</c> and, for a jointed body, <c>&lt;id&gt;-trace.json</c>.
        /// </summary>
        /// <returns>False when there is nowhere to write or the cap is reached.</returns>
        public bool Write(DivergedBody what)
        {
            if (what == null) throw new ArgumentNullException(nameof(what));
            if (string.IsNullOrEmpty(Directory) || Written >= _cap) return false;

            Creature body = what.Body;

            try
            {
                var w = new Json.Writer(indent: true);
                w.BeginObject();

                w.Field("creatureId", (long)body.Id);
                w.Field("t", what.ElapsedSeconds);
                w.Field("physicsStep", what.PhysicsStep);
                w.Field("physicsDtSeconds", what.Config.StepSeconds);
                w.Field("divergenceReason", what.Reason);
                w.Field("lastObservedHeightY", what.LastObservedHeightY);
                w.Field("generationDepth", what.GenerationDepth);
                w.Field("ageSeconds", what.AgeSeconds);
                w.Field("parts", body.Links);
                w.Field("totalDof", body.Dof);
                w.Field("jointed", body.Jointed);

                w.Field(
                    "traceOmitted",
                    body.HasTrace
                        ? null
                        : "no trace: unjointed body (no movable joint, so no ring is kept)");

                Vector(w, "lastRootPosition", what.LastRootPosition ?? LastFiniteRoot(body));

                w.BeginArray("partStates");

                for (int b = 0; b < body.Links; b++)
                {
                    PhenotypePart shape = Part(body, b);

                    w.BeginObject();
                    w.Field("index", b);
                    w.Field("name", (string)null);
                    w.Field("shapeId", shape != null ? shape.ShapeId : null);
                    w.Field("parentIndex", shape != null ? shape.ParentIndex : -1);
                    w.Field("jointType", shape != null ? shape.JointType.ToString() : "unknown");
                    w.Field("cellTypeId", shape != null ? shape.CellTypeId : null);
                    Number(w, "power", shape != null ? shape.Power : 0d);
                    Number(w, "volumeM3", shape != null ? shape.Volume : 0d);
                    Number(w, "massKg", body.Mass[b]);

                    Vector(w, "driveTorque", Vec3.Read(body.Drive.AppliedTorque, 3 * b));
                    Vector(w, "externalForce", Vec3.Read(body.Fext, 6 * b + 3));
                    Vector(w, "externalTorque", Vec3.Read(body.Fext, 6 * b));
                    Vector(w, "waterVelocity", Vec3.Read(body.Water, 3 * b));

                    Vector(w, "position", Vec3.Read(body.Position, 3 * b));
                    Vector(w, "velocity", Vec3.Read(body.Velocity, 3 * b));
                    Vector(w, "angularVelocity", Vec3.Read(body.Spin, 3 * b));
                    w.EndObject();
                }

                w.EndArray();

                // The genome last, because it is the long part and a reader opening this file
                // wants the numbers above it first. Written by GenomeJson, compact, as one line:
                // there is exactly one genome serialiser and this is not a second one.
                w.Raw("genome", what.Genome != null ? GenomeJson.Write(what.Genome) : null);

                w.EndObject();

                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(
                    Path.Combine(Directory, body.Id + ".json"), w.ToString(), new UTF8Encoding(false));

                Written++;

                WriteTrace(what);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The three steps before the divergence, per link — <c>&lt;id&gt;-trace.json</c>.
        /// </summary>
        /// <remarks>
        /// Its own try/catch inside the dump's, and after it, so a body that has one file always
        /// has the more important one. Nothing is written for a body with no ring: the readers on
        /// file count their shares over the dumps that carry a trace, and a file holding no
        /// frames would enter those denominators as a traced body.
        /// </remarks>
        private void WriteTrace(DivergedBody what)
        {
            Creature body = what.Body;
            if (!body.HasTrace) return;

            try
            {
                int links = body.Links;

                var w = new Json.Writer(indent: true);
                w.BeginObject();

                w.Field("creatureId", (long)body.Id);
                w.Field("t", what.ElapsedSeconds);
                w.Field("physicsStep", what.PhysicsStep);
                w.Field("physicsDtSeconds", what.Config.StepSeconds);
                w.Field("ageSeconds", what.AgeSeconds);
                w.Field("parts", links);
                w.Field("totalDof", body.Dof);

                double worst = 0;
                for (int b = 0; b < links; b++)
                {
                    double ratio = JointMassRatio(body, b);
                    if (ratio > worst) worst = ratio;
                }

                Number(w, "maxJointMassRatio", worst);

                w.Field("lastResizeStep", body.ResizedAtStep);
                w.Field(
                    "stepsSinceLastResize",
                    body.ResizedAtStep < 0 ? -1L : what.PhysicsStep - body.ResizedAtStep);

                w.BeginArray("links");

                for (int b = 0; b < links; b++)
                {
                    PhenotypePart shape = Part(body, b);

                    w.BeginObject();
                    w.Field("index", b);
                    w.Field("name", (string)null);
                    w.Field("parentIndex", shape != null ? shape.ParentIndex : -1);
                    w.Field("jointType", shape != null ? shape.JointType.ToString() : "unknown");
                    w.Field("jointDof", body.DofCount[b]);
                    Number(w, "massKg", body.Mass[b]);
                    Number(w, "jointMassRatio", JointMassRatio(body, b));
                    w.EndObject();
                }

                w.EndArray();

                w.Field("firstNonFiniteStep", body.FirstNonFiniteStep);
                w.Field("firstNonFiniteLink", body.FirstNonFiniteLink);
                w.Field(
                    "stepsFromFirstNonFiniteToDump",
                    body.FirstNonFiniteStep < 0 ? -1L : what.PhysicsStep - body.FirstNonFiniteStep);

                int held = body.TraceHeld;
                w.Field("framesHeld", held);
                w.Field("framesFinite", CountFiniteFrames(body));

                w.BeginArray("frames");

                // Oldest to newest. The cursor points at the slot the next frame would overwrite,
                // which is the oldest one it holds once it is full.
                int first = held == Creature.TraceFrames ? body.TraceCursor : 0;

                for (int f = 0; f < held; f++)
                {
                    int slot = (first + f) % Creature.TraceFrames;

                    w.BeginObject();
                    w.Field("step", body.TraceStepAt(slot));
                    w.Field("t", body.TraceTimeAt(slot));
                    w.Field("resizedJustBefore", body.TraceResizedAt(slot));
                    w.BeginArray("links");

                    for (int b = 0; b < links; b++)
                    {
                        w.BeginObject();
                        w.Field("index", b);
                        TraceVector(w, "position", body, slot, b, 0);
                        TraceVector(w, "velocity", body, slot, b, 3);
                        TraceVector(w, "angularVelocity", body, slot, b, 6);

                        // The reduced-space joint velocity, named by slot rather than by axis:
                        // these are degrees of freedom in the order the drive takes them, not a
                        // direction in the water.
                        w.BeginObject("jointVelocity");
                        Number(w, "v0", body.TraceValue(slot, b, 9));
                        Number(w, "v1", body.TraceValue(slot, b, 10));
                        Number(w, "v2", body.TraceValue(slot, b, 11));
                        w.EndObject();

                        TraceVector(w, "externalForce", body, slot, b, 12);
                        TraceVector(w, "externalTorque", body, slot, b, 15);
                        TraceVector(w, "waterVelocity", body, slot, b, 18);

                        w.EndObject();
                    }

                    w.EndArray();
                    w.EndObject();
                }

                w.EndArray();
                w.EndObject();

                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(
                    Path.Combine(Directory, body.Id + "-trace.json"),
                    w.ToString(),
                    new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // The post-mortem is on disk; the trace is the smaller loss.
            }
        }

        /// <summary>The newest held frame's root, or the body's own root when it holds none.</summary>
        public static Vec3 LastFiniteRoot(Creature body)
        {
            if (!body.HasTrace || body.TraceHeld == 0) return Vec3.Read(body.Position, 0);

            int newest = (body.TraceCursor + Creature.TraceFrames - 1) % Creature.TraceFrames;

            return new Vec3(
                body.TraceValue(newest, 0, 0),
                body.TraceValue(newest, 0, 1),
                body.TraceValue(newest, 0, 2));
        }

        /// <summary>
        /// The heavier of a link's mass and its parent's over the lighter, or 0 where that is not
        /// a number — <c>PhenotypeBuilder.MassRatio</c>, term for term, and 0 for a root.
        /// </summary>
        private static double JointMassRatio(Creature body, int link)
        {
            int parent = body.Parent[link];
            if (link == 0 || parent < 0 || parent >= body.Links) return 0;

            double a = body.Mass[parent], b = body.Mass[link];
            double heavier = a > b ? a : b;
            double lighter = a > b ? b : a;

            if (!(lighter > 0) || double.IsNaN(heavier) || double.IsInfinity(heavier)) return 0;

            double ratio = heavier / lighter;
            return double.IsNaN(ratio) || double.IsInfinity(ratio) ? 0 : ratio;
        }

        /// <summary>
        /// How many of the frames a body holds carry nothing but finite numbers.
        /// </summary>
        /// <remarks>
        /// Walked rather than trusted. Every frame in the ring is finite by construction, so this
        /// must equal <c>framesHeld</c>; writing it as a count means a reader of the file can see
        /// that it does rather than take the claim on trust.
        /// </remarks>
        private static int CountFiniteFrames(Creature body)
        {
            int held = body.TraceHeld;
            int first = held == Creature.TraceFrames ? body.TraceCursor : 0;
            int finite = 0;

            for (int f = 0; f < held; f++)
            {
                int slot = (first + f) % Creature.TraceFrames;
                bool all = true;

                for (int b = 0; b < body.Links && all; b++)
                {
                    for (int k = 0; k < Creature.TraceValuesPerLink; k++)
                    {
                        double v = body.TraceValue(slot, b, k);
                        if (double.IsNaN(v) || double.IsInfinity(v)) { all = false; break; }
                    }
                }

                if (all) finite++;
            }

            return finite;
        }

        private static PhenotypePart Part(Creature body, int index) =>
            body.Phenotype != null && index < body.Phenotype.Parts.Count
                ? body.Phenotype.Parts[index]
                : null;

        private static void TraceVector(
            Json.Writer w, string name, Creature body, int slot, int link, int at)
        {
            w.BeginObject(name);
            Number(w, "x", body.TraceValue(slot, link, at));
            Number(w, "y", body.TraceValue(slot, link, at + 1));
            Number(w, "z", body.TraceValue(slot, link, at + 2));
            w.EndObject();
        }

        private static void Vector(Json.Writer w, string name, Vec3 v)
        {
            w.BeginObject(name);
            Number(w, "x", v.X);
            Number(w, "y", v.Y);
            Number(w, "z", v.Z);
            w.EndObject();
        }

        /// <summary>One number, non-finite included — as its own name, in quotes.</summary>
        internal static void Number(Json.Writer w, string name, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
            {
                w.Field(name, v.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                w.Field(name, v);
            }
        }
    }
}
