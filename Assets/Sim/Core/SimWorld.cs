using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// Root of the simulation. Owns the clock and, later, all game state.
    /// Pure C#: no UnityEngine. One tick is one game minute. The Unity layer calls
    /// <see cref="Step"/> and reads state to render it.
    /// </summary>
    public sealed class SimWorld
    {
        private readonly GameCalendar _calendar;

        /// <summary>The tuning this world was built from.</summary>
        public SimTuning Tuning { get; }

        /// <summary>Number of ticks simulated since the world was created. One tick = one game minute.</summary>
        public long Tick { get; private set; }

        /// <summary>Calendar date and time for the current tick (named Now to avoid UnityEngine.Time).</summary>
        public GameTime Now => _calendar.At(Tick);

        /// <summary>True while the simulation is paused; <see cref="Step"/> does nothing.</summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Every command passed to <see cref="Execute"/>, accepted or rejected, with its tick.
        /// Never null; grows for the life of the world.
        /// </summary>
        public CommandLog CommandLog { get; } = new CommandLog();

        /// <summary>The ground. Never null; a <see cref="FlatHeightField"/> when built without a map.</summary>
        public IHeightField Terrain { get; }

        /// <summary>All trees. Never null; empty when built without a map.</summary>
        public TreeRegistry Trees { get; }

        /// <summary>The road graph. Never null; empty at the start.</summary>
        public RoadNetwork Roads { get; } = new RoadNetwork();

        /// <summary>All player areas. Never null; empty at the start.</summary>
        public AreaRegistry Areas { get; }

        /// <summary>Builds a world whose terrain and trees come from baked map data.</summary>
        public static SimWorld FromMapData(MapData map, SimTuning tuning)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var trees = new TreeRegistry();
            foreach (var t in map.Trees) trees.Add(t.Id, t.X, t.Z, t.TrunkLengthM);
            return new SimWorld(tuning, map.Heights, trees);
        }

        /// <summary>Creates a world with flat terrain and no trees.</summary>
        public SimWorld(SimTuning tuning) : this(tuning, new FlatHeightField(), new TreeRegistry())
        {
        }

        private SimWorld(SimTuning tuning, IHeightField terrain, TreeRegistry trees)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Terrain = terrain;
            Trees = trees;
            Areas = new AreaRegistry(tuning.SpatialIndexCellM);
            _calendar = new GameCalendar(tuning.StartMonth, tuning.StartDay, tuning.StartHour,
                tuning.StartMinute, tuning.StartWeekday, tuning.DaysPerYear);
        }

        /// <summary>Pause the simulation. Safe to call when already paused.</summary>
        public void Pause() => Paused = true;

        /// <summary>Resume the simulation. Safe to call when not paused.</summary>
        public void Resume() => Paused = false;

        /// <summary>Advance the simulation by <paramref name="ticks"/> game minutes; nothing while paused.</summary>
        public void Step(int ticks = 1)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (Paused) return;
            Tick += ticks;
        }

        /// <summary>
        /// Runs a player command now: <see cref="ISimCommand.Validate"/>, then
        /// <see cref="ISimCommand.Apply"/> only if accepted. Works while paused and never changes
        /// <see cref="Tick"/>. Every call is appended to <see cref="CommandLog"/> with the current tick.
        /// A rejected command changes nothing. Decision 0011.
        /// </summary>
        /// <param name="command">The command to run. Must not be null.</param>
        /// <returns>The accepted or rejected result. Never null.</returns>
        public CommandResult Execute(ISimCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            CommandLog.Append(Tick, command);
            CommandResult result = command.Validate(this);
            if (result == null) throw new InvalidOperationException("Validate returned null for " + command.GetType().Name);
            if (result.IsRejected) return result;
            result = command.Apply(this);
            if (result == null) throw new InvalidOperationException("Apply returned null for " + command.GetType().Name);
            return result;
        }
    }
}
