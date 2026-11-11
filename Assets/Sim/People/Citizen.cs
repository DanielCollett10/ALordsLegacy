using System;

namespace LordsLegacy.Sim.People
{
    /// <summary>One citizen (PPL-10, PPL-14). Position and facing are mutable; the rest is fixed at creation.</summary>
    public sealed class Citizen : IMovable
    {
        /// <summary>Game minutes (ticks) in one calendar year: 365 days of 1440 minutes.</summary>
        public const long TicksPerYear = 365L * 24 * 60;

        /// <summary>Creates a citizen record. Use <see cref="Population.AddCitizen"/>.</summary>
        public Citizen(int id, string name, Sex sex, long birthTick, int familyId, double x, double z,
            double facingRad = 0, Activity activity = Activity.MillingAbout)
        {
            Id = id;
            Name = name ?? string.Empty;
            Sex = sex;
            BirthTick = birthTick;
            FamilyId = familyId;
            X = x;
            Z = z;
            PreviousX = x;
            PreviousZ = z;
            FacingRad = facingRad;
            Activity = activity;
        }

        /// <summary>Unique id; never reused.</summary>
        public int Id { get; }

        /// <summary>Given name.</summary>
        public string Name { get; }

        /// <summary>Sex.</summary>
        public Sex Sex { get; }

        /// <summary>Birth moment as a Sim tick; negative means before the game started.</summary>
        public long BirthTick { get; }

        /// <summary>Id of the family this citizen belongs to.</summary>
        public int FamilyId { get; }

        /// <summary>Position x in metres.</summary>
        public double X { get; set; }

        /// <summary>Position z in metres.</summary>
        public double Z { get; set; }

        /// <summary>Facing in radians.</summary>
        public double FacingRad { get; set; }

        /// <summary>Current activity.</summary>
        public Activity Activity { get; set; }

        /// <inheritdoc />
        public double PreviousX { get; set; }

        /// <inheritdoc />
        public double PreviousZ { get; set; }

        /// <inheritdoc />
        public double CurrentMultiplier { get; set; } = 1.0;

        /// <inheritdoc />
        public MoveOrder Move { get; set; }

        /// <summary>
        /// Age in years at tick <paramref name="nowTick"/>: calendar time since birth times
        /// <paramref name="ageingRate"/> (POP-27, <c>SimTuning.AgeingRate</c>, default 2×). Never below 0.
        /// </summary>
        public double AgeYears(long nowTick, double ageingRate)
        {
            if (double.IsNaN(ageingRate) || ageingRate < 0) throw new ArgumentOutOfRangeException(nameof(ageingRate));
            long elapsed = nowTick - BirthTick;
            if (elapsed <= 0) return 0;
            return elapsed / (double)TicksPerYear * ageingRate;
        }
    }
}
