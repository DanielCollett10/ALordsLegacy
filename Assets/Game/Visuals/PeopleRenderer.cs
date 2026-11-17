using System.Collections.Generic;
using LordsLegacy.Sim.People;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws every citizen and ox from <c>SimHost.World.People</c> with <c>Graphics.RenderMeshInstanced</c>: no
    /// GameObjects per agent and no colliders (decisions 0008, 0009). Position is lerped from the agent's
    /// <c>PreviousX/Z</c> to <c>X/Z</c> by <see cref="SimHost.TickFraction"/> (a paused Sim shows the current position),
    /// height comes from the Sim terrain and facing from <c>FacingRad</c>. Walking shows a placeholder vertical bob
    /// whose rate is metres walked per real second × <see cref="ViewSettings.WalkStepsPerMetre"/> (see <see cref="WalkBob"/>),
    /// so a road walker steps faster; standing agents do not bob.
    /// </summary>
    public sealed class PeopleRenderer : MonoBehaviour
    {
        /// <summary>Age in years below which a citizen is drawn with the child visual.</summary>
        public const double AdultAgeYears = 16;

        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private ViewSettings _settings;
        private readonly List<Matrix4x4> _adults = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _children = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _oxen = new List<Matrix4x4>();
        private readonly Dictionary<int, double> _citizenPhase = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _oxPhase = new Dictionary<int, double>();

        private readonly List<Matrix4x4> _crates = new List<Matrix4x4>();

        /// <summary>Food crates drawn on carriers in the last draw (MK-43). For tests.</summary>
        public int CarriedCrateCount => _crates.Count;

        /// <summary>Instances submitted in the last draw: adults plus children.</summary>
        public int CitizenInstanceCount => _adults.Count + _children.Count;

        /// <summary>Child instances submitted in the last draw.</summary>
        public int ChildInstanceCount => _children.Count;

        /// <summary>Ox instances submitted in the last draw.</summary>
        public int OxInstanceCount => _oxen.Count;

        /// <summary>Gait phase (in steps) of a citizen, or 0. For tests.</summary>
        public double CitizenPhase(int id) => _citizenPhase.TryGetValue(id, out var p) ? p : 0;

        /// <summary>Height in metres of the walking bob at its peak.</summary>
        public float BobAmplitudeM { get; set; } = 0.08f;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry, ViewSettings settings = null)
        {
            host = simHost;
            registry = visualRegistry;
            if (settings != null) _settings = settings;
        }

        /// <summary>Shares the presentation settings (the top bar's instance).</summary>
        public void SetViewSettings(ViewSettings settings) { _settings = settings; }

        // Shares the top bar's settings when one is in the scene (found once), else private defaults.
        private ViewSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    var bar = FindFirstObjectByType<TopBarController>();
                    _settings = bar != null ? bar.ViewSettings : new ViewSettings();
                }
                return _settings;
            }
        }

        private void Update()
        {
            Draw(Time.deltaTime);
        }

        /// <summary>Rebuilds the matrices for this frame, advances gait phases by <paramref name="dt"/> and draws.</summary>
        public void Draw(float dt)
        {
            _adults.Clear(); _children.Clear(); _oxen.Clear();
            if (host == null || !host.IsInitialized || registry == null) return;

            var world = host.World;
            float t = world.Paused ? 1f : Mathf.Clamp01((float)host.TickFraction);
            double rate = host.Pacer.GameMinutesPerRealSecond;
            double spm = Settings.WalkStepsPerMetre;
            bool paused = world.Paused;
            double ageing = world.Tuning.AgeingRate;
            long now = world.Tick;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var adultDef = registry.Get(VisualSizes.CitizenAdult);
            var childDef = registry.Get(VisualSizes.CitizenChild);
            var oxDef = registry.Get(VisualSizes.Ox);

            _crates.Clear();
            var carriers = CarryingAssignees(world);
            registry.TryGet(VisualSizes.ItemFoodCrate, out var crateDef);

            foreach (var c in world.People.Citizens)
            {
                bool child = c.AgeYears(now, ageing) < AdultAgeYears;
                var def = child ? childDef : adultDef;
                var m = Matrix(def, child ? VisualSizes.CitizenChild : VisualSizes.CitizenAdult, c, t, dt, rate, spm, paused,
                    _citizenPhase, c.Id, world, ref min, ref max);
                (child ? _children : _adults).Add(m);
                if (crateDef != null && carriers.Contains(c.Id))
                {
                    // MK-43: a food crate held at chest height in front of the carrier, bobbing with them.
                    var body = m.GetPosition();
                    var fwd = Quaternion.Euler(0f, (float)(c.FacingRad * Mathf.Rad2Deg), 0f) * Vector3.forward;
                    _crates.Add(VisualSizes.Place(crateDef, VisualSizes.ItemFoodCrate,
                        new Vector3(body.x, body.y + 0.1f, body.z) + fwd * 0.4f, (float)(c.FacingRad * Mathf.Rad2Deg)));
                }
            }
            foreach (var o in world.People.Oxen)
                _oxen.Add(Matrix(oxDef, VisualSizes.Ox, o, t, dt, rate, spm, paused, _oxPhase, o.Id, world, ref min, ref max));

            // T-509: forget gait phases of citizens that left, so ids never pile up.
            if (_citizenPhase.Count > world.People.CitizenCount)
            {
                var gone = new List<int>();
                foreach (int k in _citizenPhase.Keys) if (world.People.FindCitizen(k) == null) gone.Add(k);
                foreach (int k in gone) _citizenPhase.Remove(k);
            }

            if (_adults.Count + _children.Count + _oxen.Count == 0) return;
            var b = new Bounds();
            b.SetMinMax(min - Vector3.one * 4f, max + Vector3.one * 4f);
            InstancedDraw.Draw(adultDef, _adults, b);
            InstancedDraw.Draw(childDef, _children, b);
            InstancedDraw.Draw(oxDef, _oxen, b);
            InstancedDraw.Draw(crateDef, _crates, b);
        }

        // Ids of citizens whose live task carries food (a stall stocking or granary fetch trip with a non-empty carrier).
        private static HashSet<int> CarryingAssignees(LordsLegacy.Sim.Core.SimWorld world)
        {
            var set = new HashSet<int>();
            foreach (var task in world.Tasks.All)
            {
                if (task.IsFinished || !task.AssigneeId.HasValue || !task.CarrierId.HasValue) continue;
                var carrier = world.Inventory.Get(task.CarrierId.Value);
                if (carrier != null && carrier.Total > 0 && carrier.Kind == LordsLegacy.Sim.Inventory.LocationKind.Carrier
                    && IsFoodTrip(task.Def.Id)) set.Add(task.AssigneeId.Value);
            }
            return set;
        }

        private static bool IsFoodTrip(string taskDefId) =>
            taskDefId == LordsLegacy.Sim.Markets.StoreFamilySystem.StockStallTaskId
            || taskDefId == LordsLegacy.Sim.Food.FoodTripSystem.FetchFoodTaskId;

        private Matrix4x4 Matrix(VisualDefinition def, string id, IMovable a, float t, float dt, double rate, double spm,
            bool paused, Dictionary<int, double> phases, int key, LordsLegacy.Sim.Core.SimWorld world,
            ref Vector3 min, ref Vector3 max)
        {
            float x = (float)(a.PreviousX + (a.X - a.PreviousX) * t);
            float z = (float)(a.PreviousZ + (a.Z - a.PreviousZ) * t);
            float y = world.Terrain.HeightAt(x, z);

            double steps = WalkBob.StepsPerRealSecond(a, rate, spm, paused);
            phases.TryGetValue(key, out double phase);
            if (steps > 0) phase = (phase + steps * dt) % 2.0; else phase = 0;
            phases[key] = phase;
            y += (float)WalkBob.Offset(phase, BobAmplitudeM);

            var p = new Vector3(x, y, z);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p + Vector3.up * 3f);
            return VisualSizes.Place(def, id, p, (float)(a.FacingRad * Mathf.Rad2Deg));
        }
    }
}
