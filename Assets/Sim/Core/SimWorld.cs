using System;
using System.Collections.Generic;
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
    /// <para>
    /// Partial by convention (T-249): the core state and behaviour live in this file. Each subsystem adds its state
    /// and helpers in <c>Assets/Sim/Core/SimWorld.&lt;Area&gt;.cs</c> so tasks do not all edit this file. Its
    /// per-tick logic is an <see cref="ISimSystem"/> listed in <see cref="SimSystems"/>, not code added to
    /// <see cref="Step"/>.
    /// </para>
    /// </summary>
    public sealed partial class SimWorld
    {
        private readonly GameCalendar _calendar;
        private readonly ISimSystem[] _systems;

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

        /// <summary>The road types this world knows. Never null; today's defaults unless passed in (decision 0019).</summary>
        public RoadTypeCatalog RoadTypes { get; }

        /// <summary>The area types this world knows. Never null; today's defaults unless passed in (decision 0019).</summary>
        public AreaTypeCatalog AreaTypes { get; }

        /// <summary>
        /// Builds a world whose terrain and trees come from baked map data. Null catalogs mean the default
        /// definitions.
        /// </summary>
        public static SimWorld FromMapData(MapData map, SimTuning tuning, RoadTypeCatalog roadTypes = null,
            AreaTypeCatalog areaTypes = null, IEnumerable<ISimSystem> systems = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var trees = new TreeRegistry();
            foreach (var t in map.Trees) trees.Add(t.Id, t.X, t.Z, t.TrunkLengthM);
            return new SimWorld(tuning, map.Heights, trees, roadTypes, areaTypes, systems);
        }

        /// <summary>
        /// Creates a world with flat terrain and no trees. Null catalogs mean the default definitions.
        /// Null <paramref name="systems"/> means <see cref="SimSystems.CreateDefault"/>; a list given here runs in the
        /// order given (tests use this).
        /// </summary>
        public SimWorld(SimTuning tuning, RoadTypeCatalog roadTypes = null, AreaTypeCatalog areaTypes = null,
            IEnumerable<ISimSystem> systems = null)
            : this(tuning, new FlatHeightField(), new TreeRegistry(), roadTypes, areaTypes, systems)
        {
        }

        private SimWorld(SimTuning tuning, IHeightField terrain, TreeRegistry trees, RoadTypeCatalog roadTypes,
            AreaTypeCatalog areaTypes, IEnumerable<ISimSystem> systems)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Terrain = terrain;
            Trees = trees;
            RoadTypes = roadTypes ?? LordsLegacy.Sim.Roads.RoadTypes.DefaultCatalog;
            AreaTypes = areaTypes ?? LordsLegacy.Sim.Areas.AreaTypes.DefaultCatalog;
            Areas = new AreaRegistry(tuning.SpatialIndexCellM, AreaTypes);
            _calendar = new GameCalendar(tuning.StartMonth, tuning.StartDay, tuning.StartHour,
                tuning.StartMinute, tuning.StartWeekday, tuning.DaysPerYear);
            // Copy the list so the run order is fixed for the life of this world.
            _systems = new List<ISimSystem>(systems ?? SimSystems.CreateDefault()).ToArray();
        }

        /// <summary>Pause the simulation. Safe to call when already paused.</summary>
        public void Pause() => Paused = true;

        /// <summary>Resume the simulation. Safe to call when not paused.</summary>
        public void Resume() => Paused = false;

        /// <summary>
        /// Advance the simulation by <paramref name="ticks"/> game minutes; nothing while paused. Each tick first
        /// advances <see cref="Tick"/> by one, then runs every system's <see cref="ISimSystem.Step"/> in
        /// <see cref="SimSystems"/> order. With no systems this only advances <see cref="Tick"/>.
        /// </summary>
        public void Step(int ticks = 1)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (Paused) return;
            for (int i = 0; i < ticks; i++)
            {
                Tick++;
                for (int s = 0; s < _systems.Length; s++) _systems[s].Step(this);
            }
        }

        /// <summary>
        /// Runs a player command now: <see cref="ISimCommand.Validate"/>, then
        /// <see cref="ISimCommand.Apply"/> only if accepted. Works while paused and never changes
        /// <see cref="Tick"/>. Every call is appended to <see cref="CommandLog"/> with the current tick.
        /// A rejected command changes nothing. After an accepted command, every system's
        /// <see cref="ISimSystem.OnCommandApplied"/> runs in <see cref="SimSystems"/> order. Decision 0011.
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
            if (result.IsAccepted)
            {
                for (int s = 0; s < _systems.Length; s++) _systems[s].OnCommandApplied(this, result);
            }
            return result;
        }
    }
}
