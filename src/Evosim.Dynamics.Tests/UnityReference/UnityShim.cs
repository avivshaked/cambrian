using System;
using Evosim.Core;

// ---------------------------------------------------------------------------------------------
// The shim the Unity placer is compiled against, so that the *original* file — copied verbatim
// into UnityReference/SharedVolume.cs and never edited — can stand beside the port in one test
// process and be driven through the same draws.
//
// Only the members Evosim.Sim.SharedVolume actually touches are here, and each is Unity's own
// expression rather than the framework's nearest equivalent. That is the whole point: a shim
// written with MathF would make the parity test compare the port against a third thing, and the
// two places where Unity and MathF genuinely part (Sin/Cos through the double routine, and Max's
// ternary with its NaN answer) are exactly the places a placer notices.
//
// Nothing here simulates an engine. No collider, no transform, no physics step.
// ---------------------------------------------------------------------------------------------

namespace UnityEngine
{
    /// <summary>
    /// <c>UnityEngine.Vector3</c>: three public float fields, so the original's
    /// <c>candidate.y = placedY</c> compiles as the in-place write it is.
    /// </summary>
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>Unity: <c>x * x + y * y + z * z</c>.</summary>
        public float sqrMagnitude => x * x + y * y + z * z;

        public static Vector3 operator -(Vector3 a, Vector3 b) =>
            new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vector3 operator +(Vector3 a, Vector3 b) =>
            new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

        /// <summary>
        /// Unity: the three differences and their squares in <c>float</c>, then one
        /// <c>Math.Sqrt</c> over the widened sum.
        /// </summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            float diffX = a.x - b.x;
            float diffY = a.y - b.y;
            float diffZ = a.z - b.z;

            return (float)Math.Sqrt(diffX * diffX + diffY * diffY + diffZ * diffZ);
        }

        public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
    }

    /// <summary>
    /// <c>UnityEngine.Mathf</c>, transcribed from Unity's own source. See the file header.
    /// </summary>
    public static class Mathf
    {
        public const float PI = 3.14159274F;

        public static float Max(float a, float b) => a > b ? a : b;

        public static int Max(int a, int b) => a > b ? a : b;

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;
            return value;
        }

        public static float Sqrt(float f) => (float)Math.Sqrt(f);

        public static float Sin(float f) => (float)Math.Sin(f);

        public static float Cos(float f) => (float)Math.Cos(f);

        public static float Floor(float f) => (float)Math.Floor(f);

        public static float Round(float f) => (float)Math.Round(f);

        public static int FloorToInt(float f) => (int)Math.Floor(f);

        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
    }
}

namespace Evosim.Sim
{
    /// <summary>
    /// <c>Evosim.Sim.SeaFloor</c>'s three arithmetic members, transcribed from
    /// <c>unity/Assets/Evosim/Sim/SeaFloor.cs</c>, with the colliders and the mesh left out
    /// because <c>SharedVolume</c> never reads them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The constructor reproduces <c>SeaFloor.Build</c>'s two decisions and nothing else:
    /// <c>topY</c> is <c>-volume.DepthMetres</c>, and a <see cref="BedShape"/> with no relief
    /// goes down the flat path and is not stored, so <c>HasRelief</c> reads false for it.
    /// </para>
    /// <para>
    /// This is deliberately an independent transcription of the same two expressions rather than
    /// a call into <c>Evosim.Dynamics.Placement.PlacementFloor</c>: a parity test in which both
    /// sides read the port's own floor would be comparing the port with itself.
    /// </para>
    /// </remarks>
    public sealed class SeaFloor
    {
        public const float ClearanceMetres = 0.05f;

        private readonly BedShape _bed;
        private readonly float _topY;

        public SeaFloor(float depthMetres, BedShape bed = null)
        {
            _bed = bed != null && bed.HasRelief ? bed : null;
            _topY = -depthMetres;
        }

        public bool HasRelief => _bed != null;

        public float FloorYAt(float x, float z) =>
            _bed != null ? (float)_bed.FloorY(x, z) : _topY;

        public float MinimumPlacementY(float x, float z, float boundingRadius) =>
            FloorYAt(x, z) + UnityEngine.Mathf.Max(0f, boundingRadius) + ClearanceMetres;
    }
}
