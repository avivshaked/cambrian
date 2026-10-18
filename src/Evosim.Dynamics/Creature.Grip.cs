using System;
using Evosim.Core;

namespace Evosim.Dynamics
{
    // D162's grip, per link carrying the anchoring cell (round 59, step 4). Null on every body of
    // a world whose table has no anchoring cell, and on every body with no anchoring part, so such
    // a body carries nothing new and its step adds no float. Anchor.Apply is the only writer
    // during a step; the farm drains the owed price between steps.
    public sealed partial class Creature
    {
        /// <summary>Whether each link carries the anchoring cell; null unless the body has one.</summary>
        public bool[] Anchors;

        /// <summary>Whether each anchoring link holds a grip now.</summary>
        public bool[] GripHeld;

        /// <summary>Where each held link's origin was when it gripped, world, 3 per link.</summary>
        public double[] GripAnchor;

        /// <summary>Each held link's world orientation when it gripped, as a matrix, 9 per link.</summary>
        public double[] GripAttitude;

        /// <summary>
        /// Where each held link met the rock when it gripped, 3 per link: the touch record's
        /// point, kept for the record and the films. The hold pulls the origin, not this point.
        /// </summary>
        public double[] GripPoint;

        /// <summary>Each grip's strength, 1 when it forms and falling with the decay.</summary>
        public double[] GripStrength;

        /// <summary>
        /// Seconds the body's reserve has stood below the cell's threshold without a break —
        /// the lag of D162's reserve rule. 0 while the reserve is above it.
        /// </summary>
        public double ReserveLowSeconds;

        /// <summary>Joules the grips formed since the last drain owe, to be charged to the reserve.</summary>
        public double GripOwedJoules;

        /// <summary>Grips formed and broken over the body's life, and link-seconds held.</summary>
        public long GripsFormed;

        public long GripsBroken;

        public double GripHeldSeconds;

        /// <summary>Whether this body has a part that can grip in this world.</summary>
        public bool CanGrip => Anchors != null;

        private void InitGrip(Phenotype phenotype, SolverConfig config)
        {
            if (config.Anchoring == null) return;

            bool any = false;
            for (int i = 0; i < Links && !any; i++)
                any = phenotype.Parts[i].CellTypeId == CellTypeIds.Anchoring;
            if (!any) return;

            if (!config.ContactPerPart)
            {
                throw new ArgumentException(
                    "A body with an anchoring part needs ContactPerPart: the grip forms on the touch " +
                    "of each link's own sphere (D162).");
            }

            Anchors = new bool[Links];
            for (int i = 0; i < Links; i++) Anchors[i] = phenotype.Parts[i].CellTypeId == CellTypeIds.Anchoring;

            GripHeld = new bool[Links];
            GripAnchor = new double[3 * Links];
            GripAttitude = new double[9 * Links];
            GripPoint = new double[3 * Links];
            GripStrength = new double[Links];

            // The touch record the grip forms on, kept whether or not the world senses it.
            if (RockTouched == null)
            {
                RockTouched = new bool[Links];
                RockPoint = new double[3 * Links];
                RockNormal = new double[3 * Links];
            }
        }

        /// <summary>Returns the joules owed for grips formed since the last call, and clears them.</summary>
        public double DrainGripOwed()
        {
            double owed = GripOwedJoules;
            GripOwedJoules = 0;
            return owed;
        }

        /// <summary>How many of this body's links hold a grip now.</summary>
        public int GripsHeld
        {
            get
            {
                if (GripHeld == null) return 0;
                int n = 0;
                for (int i = 0; i < Links; i++) if (GripHeld[i]) n++;
                return n;
            }
        }

        /// <summary>
        /// Takes over the grips of the body this one replaces, link by link through the developer's
        /// part map, so a plan change neither drops a grip nor charges for it again. A link the map
        /// does not reach, or one no longer anchoring, has none.
        /// </summary>
        private void AdoptGripsFrom(Creature previous, int[] previousPartOfPart)
        {
            ReserveLowSeconds = previous.ReserveLowSeconds;
            GripOwedJoules = previous.GripOwedJoules;
            GripsFormed = previous.GripsFormed;
            GripsBroken = previous.GripsBroken;
            GripHeldSeconds = previous.GripHeldSeconds;

            if (GripHeld == null || previous.GripHeld == null) return;

            for (int i = 0; i < Links; i++)
            {
                int from = previousPartOfPart[i];
                if (!Anchors[i] || from < 0 || from >= previous.Links || !previous.GripHeld[from]) continue;

                GripHeld[i] = true;
                GripStrength[i] = previous.GripStrength[from];
                Array.Copy(previous.GripAnchor, 3 * from, GripAnchor, 3 * i, 3);
                Array.Copy(previous.GripAttitude, 9 * from, GripAttitude, 9 * i, 9);
                Array.Copy(previous.GripPoint, 3 * from, GripPoint, 3 * i, 3);
            }
        }
    }
}
