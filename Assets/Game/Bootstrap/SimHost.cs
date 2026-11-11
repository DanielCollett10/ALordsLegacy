using System.Diagnostics;
using System.IO;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>Owns the SimWorld and TickPacer, loads the map and drives ticks (decision 0006).</summary>
    public sealed class SimHost : MonoBehaviour
    {
        /// <summary>Map file name under StreamingAssets/Maps.</summary>
        public const string DefaultMapFile = "testmap.lsmap";

        /// <summary>When false, Update does nothing and callers (tests) call <see cref="Tick"/>.</summary>
        public bool AutoUpdate { get; set; } = true;

        /// <summary>The simulation. Null until initialised. Views must not write to it.</summary>
        public SimWorld World { get; private set; }

        /// <summary>Real-time pacer. Null until initialised.</summary>
        public TickPacer Pacer { get; private set; }

        /// <summary>Sim cost measurements.</summary>
        public SimStats Stats { get; } = new SimStats();

        /// <summary>Fraction of the next tick already elapsed, [0,1).</summary>
        public double TickFraction => Pacer == null ? 0 : Pacer.Fraction;

        /// <summary>True once <see cref="Initialize"/> has run.</summary>
        public bool IsInitialized => World != null;

        [SerializeField, Tooltip("Sim values (decision 0019). Null uses the code defaults. Read once at Start.")]
        private SimConfig _config;

        /// <summary>The config asset, or null for code defaults. Set before the world is built.</summary>
        public SimConfig Config { get => _config; set => _config = value; }

        [SerializeField, Tooltip("Start layout applied after the world is built (camp, people, oxen, trunk road). Null = no camp.")]
        private StartLayoutConfig _startLayout;

        /// <summary>The start layout asset, or null for no camp. Set before the world is built.</summary>
        public StartLayoutConfig StartLayout { get => _startLayout; set => _startLayout = value; }

        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>Creates the world and pacer from the tuning and map; type catalogs come from the config (or defaults).</summary>
        public void Initialize(SimTuning tuning, MapData map)
        {
            Initialize(tuning, map, _config != null ? _config.BuildRoadCatalog() : null,
                _config != null ? _config.BuildAreaCatalog() : null);
        }

        /// <summary>Creates the world and pacer with explicit type catalogs (null = code defaults).</summary>
        public void Initialize(SimTuning tuning, MapData map, RoadTypeCatalog roadTypes, AreaTypeCatalog areaTypes)
        {
            World = SimWorld.FromMapData(map, tuning, roadTypes, areaTypes);
            Pacer = new TickPacer(tuning);
            if (_startLayout != null) LordsLegacy.Sim.Start.StartLayoutApplier.Apply(World, _startLayout.ToLayout());
        }

        private void Start()
        {
            if (IsInitialized) return;
            string path = Path.Combine(Application.streamingAssetsPath, "Maps", DefaultMapFile);
            var map = TryLoadMap(path);
            if (map != null) Initialize(_config != null ? _config.BuildTuning() : new SimTuning(), map);
        }

        /// <summary>Reads a map file; logs a clear error and returns null if missing or unreadable.</summary>
        public static MapData TryLoadMap(string path)
        {
            const string hint = " Bake it with the menu item 'Lords Legacy/Map/Bake Test Map' (map-2).";
            try
            {
                if (!File.Exists(path))
                {
                    UnityEngine.Debug.LogError("SimHost: map file not found at '" + path + "'." + hint);
                    return null;
                }
                return MapDataIO.Read(File.ReadAllBytes(path));
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("SimHost: could not read map file '" + path + "': " + e.Message + hint);
                return null;
            }
        }

        private void Update()
        {
            if (AutoUpdate) Tick(Time.deltaTime);
        }

        /// <summary>Advances the Sim by the given real seconds (no ticks and a pacer reset while paused).</summary>
        public void Tick(float realSeconds)
        {
            if (World == null) return;
            if (World.Paused)
            {
                Pacer.Reset();
                Stats.Record(0, 0, _clock.Elapsed.TotalSeconds);
                return;
            }
            int n = Pacer.Advance(realSeconds);
            long start = Stopwatch.GetTimestamp();
            World.Step(n);
            Stats.Record(Stopwatch.GetTimestamp() - start, n, _clock.Elapsed.TotalSeconds);
        }

        /// <summary>Sends a command to the Sim (the only way views change it) and returns the result. Works while paused.</summary>
        public CommandResult Execute(ISimCommand cmd)
        {
            if (World == null) return CommandResult.Rejected(CommandRejectCode.NotAllowed, "The simulation is not running.");
            return World.Execute(cmd);
        }

        /// <summary>Pauses or resumes the Sim.</summary>
        public void TogglePause()
        {
            if (World == null) return;
            if (World.Paused) World.Resume(); else World.Pause();
        }
    }
}
