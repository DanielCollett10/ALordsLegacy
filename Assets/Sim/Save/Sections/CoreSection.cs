using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Core save section (decision 0034, saving.md SV-14): tick, pause flag, random state, every
    /// <see cref="SimTuning"/> value and both type catalogs. Works on a <see cref="CoreState"/>, not a SimWorld,
    /// so it can be read before the world is built. The tuning in the save is the tuning the loaded world uses.
    /// Adding a SimTuning property means adding it to the tuning writer and reader here (a test checks this).
    /// </summary>
    public static class CoreSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int Id = 1;

        /// <summary>Writes <paramref name="state"/> as a complete section.</summary>
        public static void Write(SaveWriter w, CoreState state)
        {
            w.BeginSection(Id);
            WriteBody(w, state);
            w.EndSection();
        }

        /// <summary>Writes the section body (no id or length).</summary>
        public static void WriteBody(SaveWriter w, CoreState s)
        {
            w.WriteInt64(s.Tick);
            w.WriteBool(s.Paused);
            w.WriteUInt64(s.RandomState);
            WriteTuning(w, s.Tuning);
            WriteRoadTypes(w, s.RoadTypes);
            WriteAreaTypes(w, s.AreaTypes);
            w.WriteBool(s.EntryPoint.HasValue); // T-497
            w.WriteInt64(s.EntryPoint?.X ?? 0);
            w.WriteInt64(s.EntryPoint?.Z ?? 0);
            w.WriteInt32(s.SettlementEntries.Count); // T-498, decision 0045
            foreach (SettlementEntry e in s.SettlementEntries)
            {
                w.WriteInt64(e.Tick);
                w.WriteString(e.Text);
            }
        }

        /// <summary>Reads a section body written by <see cref="WriteBody"/>.</summary>
        public static CoreState Read(SaveReader r)
        {
            var s = new CoreState();
            s.Tick = r.ReadInt64();
            s.Paused = r.ReadBool();
            s.RandomState = r.ReadUInt64();
            s.Tuning = ReadTuning(r);
            s.RoadTypes = ReadRoadTypes(r);
            s.AreaTypes = ReadAreaTypes(r);
            bool hasEntry = r.ReadBool();
            long ex = r.ReadInt64(), ez = r.ReadInt64();
            if (hasEntry) s.EntryPoint = new LordsLegacy.Sim.Geometry.PointMm(ex, ez);
            int ne = r.ReadCount(12);
            for (int i = 0; i < ne; i++)
            {
                long tick = r.ReadInt64();
                string text = r.ReadString();
                s.SettlementEntries.Add(new SettlementEntry(tick, text));
            }
            return s;
        }

        /// <summary>
        /// Chooses the tuning for a loaded world: always the one saved in <paramref name="saved"/>; the tuning the
        /// caller passed in (<paramref name="passedIn"/>, may be null) is ignored (SV-14).
        /// </summary>
        public static SimTuning ChooseTuning(CoreState saved, SimTuning passedIn) => saved.Tuning;

        private static void WriteTuning(SaveWriter w, SimTuning t)
        {
            w.WriteDouble(t.RealMinutesPerGameDayAt1x);
            w.WriteInt32Array(t.Speeds);
            w.WriteInt32(t.MaxTicksPerFrame);
            w.WriteInt32(t.StartMonth);
            w.WriteInt32(t.StartDay);
            w.WriteInt32(t.StartHour);
            w.WriteInt32(t.StartMinute);
            w.WriteInt32((int)t.StartWeekday);
            w.WriteDouble(t.AgeingRate);
            w.WriteInt32(t.DaysPerYear);
            w.WriteDouble(t.MinDaylightHours);
            w.WriteDouble(t.MaxDaylightHours);
            w.WriteBool(t.RestDayEnabled);
            w.WriteInt32(t.SundayMealMinutes);
            w.WriteDouble(t.MinSleepHours);
            w.WriteDouble(t.WalkSpeedMPerGameMinute);
            w.WriteDouble(t.OxSpeedMPerGameMinute);
            w.WriteInt32(t.OxLoadLogs);
            w.WriteInt32(t.MaxBuildersPerSite);
            w.WriteInt32(t.OxLoadMinutes);
            w.WriteInt32(t.OxUnloadMinutes);
            w.WriteDouble(t.MillRadiusM);
            w.WriteInt32(t.MillStayMinMinutes);
            w.WriteInt32(t.MillStayMaxMinutes);
            w.WriteInt32(t.WakeStaggerMaxMinutes);
            w.WriteInt32(t.DebugLingerMinutes);
            w.WriteInt32(t.DispatchIntervalMinutes);
            w.WriteDouble(t.StandardLogLengthM);
            w.WriteDouble(t.TrunkLengthMinM);
            w.WriteDouble(t.TrunkLengthMaxM);
            w.WriteInt32(t.StartingOxen);
            w.WriteInt32(t.WorldSeed);
            w.WriteDouble(t.RoadSampleSpacingM);
            w.WriteDouble(t.RoadMinPointSpacingM);
            w.WriteDouble(t.RoadTreeClearanceM);
            w.WriteDouble(t.CurveFlattenSpacingM);
            w.WriteDouble(t.SlopeSampleGridM);
            w.WriteDouble(t.NavCellSizeM);
            w.WriteDouble(t.MaxWalkSlopeDeg);
            w.WriteDouble(t.OverlapToleranceM2);
            w.WriteDouble(t.SpatialIndexCellM);
            w.WriteDouble(t.DeconstructTimeShare);
            w.WriteDouble(t.DeconstructRefundShare);
            w.WriteInt32(t.StartingMeat);
            w.WriteDouble(t.AdultRations);
            w.WriteDouble(t.ChildRations);
            w.WriteDouble(t.MarketRangeM);
            w.WriteInt32(t.BushBaskets);
            w.WriteInt32(t.BushRegrowDays);
            w.WriteInt32(t.BushCount);
            w.WriteDouble(t.BushTreeClearM);
            w.WriteDouble(t.BushEdgeM);
            w.WriteDouble(t.BushClearingChance);
            w.WriteInt32(t.ForagerMinAge);
            w.WriteInt32(t.ForagerHutCapacity);
            w.WriteInt32(t.GatherMinutesPerBasket);
            w.WriteInt32(t.HandCarryItems);
            w.WriteDouble(t.StallCellWidthM);
            w.WriteDouble(t.StallCellDepthM);
            w.WriteDouble(t.MarketMinAreaM2);
            w.WriteDouble(t.MarketMaxAreaM2);
            w.WriteDouble(t.PileClearanceM);
            w.WriteInt32(t.HungryDaysToLeave);
            w.WriteInt32(t.ArrivalFoodDays);
            w.WriteInt32(t.ArrivalNoHungerDays);
            w.WriteInt32(t.ArrivalGapDays);
            w.WriteDouble(t.MapEdgeToleranceM);
        }

        private static SimTuning ReadTuning(SaveReader r)
        {
            var t = new SimTuning();
            t.RealMinutesPerGameDayAt1x = r.ReadDouble();
            t.Speeds = r.ReadInt32Array();
            t.MaxTicksPerFrame = r.ReadInt32();
            t.StartMonth = r.ReadInt32();
            t.StartDay = r.ReadInt32();
            t.StartHour = r.ReadInt32();
            t.StartMinute = r.ReadInt32();
            t.StartWeekday = (Weekday)r.ReadInt32();
            t.AgeingRate = r.ReadDouble();
            t.DaysPerYear = r.ReadInt32();
            t.MinDaylightHours = r.ReadDouble();
            t.MaxDaylightHours = r.ReadDouble();
            t.RestDayEnabled = r.ReadBool();
            t.SundayMealMinutes = r.ReadInt32();
            t.MinSleepHours = r.ReadDouble();
            t.WalkSpeedMPerGameMinute = r.ReadDouble();
            t.OxSpeedMPerGameMinute = r.ReadDouble();
            t.OxLoadLogs = r.ReadInt32();
            t.MaxBuildersPerSite = r.ReadInt32();
            t.OxLoadMinutes = r.ReadInt32();
            t.OxUnloadMinutes = r.ReadInt32();
            t.MillRadiusM = r.ReadDouble();
            t.MillStayMinMinutes = r.ReadInt32();
            t.MillStayMaxMinutes = r.ReadInt32();
            t.WakeStaggerMaxMinutes = r.ReadInt32();
            t.DebugLingerMinutes = r.ReadInt32();
            t.DispatchIntervalMinutes = r.ReadInt32();
            t.StandardLogLengthM = r.ReadDouble();
            t.TrunkLengthMinM = r.ReadDouble();
            t.TrunkLengthMaxM = r.ReadDouble();
            t.StartingOxen = r.ReadInt32();
            t.WorldSeed = r.ReadInt32();
            t.RoadSampleSpacingM = r.ReadDouble();
            t.RoadMinPointSpacingM = r.ReadDouble();
            t.RoadTreeClearanceM = r.ReadDouble();
            t.CurveFlattenSpacingM = r.ReadDouble();
            t.SlopeSampleGridM = r.ReadDouble();
            t.NavCellSizeM = r.ReadDouble();
            t.MaxWalkSlopeDeg = r.ReadDouble();
            t.OverlapToleranceM2 = r.ReadDouble();
            t.SpatialIndexCellM = r.ReadDouble();
            t.DeconstructTimeShare = r.ReadDouble();
            t.DeconstructRefundShare = r.ReadDouble();
            t.StartingMeat = r.ReadInt32();
            t.AdultRations = r.ReadDouble();
            t.ChildRations = r.ReadDouble();
            t.MarketRangeM = r.ReadDouble();
            t.BushBaskets = r.ReadInt32();
            t.BushRegrowDays = r.ReadInt32();
            t.BushCount = r.ReadInt32();
            t.BushTreeClearM = r.ReadDouble();
            t.BushEdgeM = r.ReadDouble();
            t.BushClearingChance = r.ReadDouble();
            t.ForagerMinAge = r.ReadInt32();
            t.ForagerHutCapacity = r.ReadInt32();
            t.GatherMinutesPerBasket = r.ReadInt32();
            t.HandCarryItems = r.ReadInt32();
            t.StallCellWidthM = r.ReadDouble();
            t.StallCellDepthM = r.ReadDouble();
            t.MarketMinAreaM2 = r.ReadDouble();
            t.MarketMaxAreaM2 = r.ReadDouble();
            t.PileClearanceM = r.ReadDouble();
            t.HungryDaysToLeave = r.ReadInt32();
            t.ArrivalFoodDays = r.ReadInt32();
            t.ArrivalNoHungerDays = r.ReadInt32();
            t.ArrivalGapDays = r.ReadInt32();
            t.MapEdgeToleranceM = r.ReadDouble();
            return t;
        }

        private static void WriteRoadTypes(SaveWriter w, RoadTypeCatalog c)
        {
            w.WriteInt32(c.All.Count);
            foreach (var d in c.All)
            {
                w.WriteString(d.Id);
                w.WriteDouble(d.WidthM);
                w.WriteDouble(d.MaxGrade);
                w.WriteDouble(d.SpeedMultiplier);
                w.WriteString(d.VisualId ?? "");
            }
        }

        private static RoadTypeCatalog ReadRoadTypes(SaveReader r)
        {
            int n = r.ReadCount(4);
            var list = new List<RoadTypeDef>(n);
            for (int i = 0; i < n; i++)
            {
                string id = r.ReadString();
                double width = r.ReadDouble();
                double grade = r.ReadDouble();
                double speed = r.ReadDouble();
                string visual = r.ReadString();
                list.Add(new RoadTypeDef(id, width, grade, speed, visual));
            }
            return new RoadTypeCatalog(list);
        }

        private static void WriteAreaTypes(SaveWriter w, AreaTypeCatalog c)
        {
            w.WriteInt32(c.All.Count);
            foreach (var d in c.All)
            {
                w.WriteString(d.Id);
                w.WriteString(d.NamePrefix ?? "");
                w.WriteInt32((int)d.Kind);
                w.WriteInt32(d.MinPoints);
                w.WriteInt32(d.MaxPoints);
                w.WriteDouble(d.MinAreaM2);
                w.WriteDouble(d.MaxAreaM2);
                w.WriteBool(d.MaxSlopeDegrees.HasValue);
                w.WriteDouble(d.MaxSlopeDegrees ?? 0);
                w.WriteBool(d.ClearsTrees);
                w.WriteBool(d.BlocksRoads);
                w.WriteDouble(d.NearRoadReachM);
            }
        }

        private static AreaTypeCatalog ReadAreaTypes(SaveReader r)
        {
            int n = r.ReadCount(4);
            var list = new List<AreaTypeDef>(n);
            for (int i = 0; i < n; i++)
            {
                string id = r.ReadString();
                string prefix = r.ReadString();
                var kind = (AreaKind)r.ReadInt32();
                int min = r.ReadInt32();
                int max = r.ReadInt32();
                double minA = r.ReadDouble();
                double maxA = r.ReadDouble();
                bool hasSlope = r.ReadBool();
                double slope = r.ReadDouble();
                bool clears = r.ReadBool();
                bool blocks = r.ReadBool();
                double reach = r.ReadDouble();
                list.Add(new AreaTypeDef(id, prefix, kind, min, max, minA, maxA,
                    hasSlope ? slope : (double?)null, clears, blocks, reach));
            }
            return new AreaTypeCatalog(list);
        }
    }
}
