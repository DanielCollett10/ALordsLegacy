using System.Collections;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the house panel, house/plot picking and the citizen home line (T-410, HM-21, HM-22).</summary>
    public class HousePanelTests
    {
        private GameObject _hostGo, _camGo, _groundGo, _agentsGo, _houseUi, _citizenUi;
        private UnityEngine.UIElements.PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private AgentPicker _agents;
        private HousePanelView _house;
        private CitizenPanelView _citizen;
        private int _plotId, _houseId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 20);
            var family = w.People.AddFamily(1, "Archer", 10, 10);
            w.People.AddCitizen(1, "Alda", Sex.Female, -4_000_000, 1, 10, 10);
            w.People.AddCitizen(2, "Bran", Sex.Male, -4_000_000, 1, 10, 10);
            Assert.IsNotNull(family);
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(new[] { M(40, 51.25), M(60, 51.25), M(60, 61.25), M(40, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            var r = _host.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _plotId = (int)r.Created[0];
            _houseId = (int)r.Created[1];

            _camGo = new GameObject("Cam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 30f;
            _camGo.transform.position = new Vector3(50f, 50f, 56f);
            _camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(50f, -0.5f, 56f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(1000f, 1f, 1000f);
            var picker = new GameObject("Picker").AddComponent<GroundPicker>();
            picker.transform.SetParent(_groundGo.transform, false);
            picker.PickCamera = _cam; picker.Host = _host;

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, picker, null);

            _settings = TestPanelSettings.Create();
            _houseUi = new GameObject("HousePanel");
            _houseUi.SetActive(false);
            _houseUi.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _house = _houseUi.AddComponent<HousePanelView>();
            _house.Configure(_host, _agents, null);
            _houseUi.SetActive(true);
            _citizenUi = new GameObject("CitizenPanel");
            _citizenUi.SetActive(false);
            _citizenUi.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _citizen = _citizenUi.AddComponent<CitizenPanelView>();
            _citizen.Configure(_host, _agents, null);
            _citizenUi.SetActive(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            ConfirmDialog.Cancel();
            foreach (var go in new[] { _citizenUi, _houseUi, _agentsGo, _groundGo, _camGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private Vector2 ScreenOf(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        // A family that lives in a house, for checking the home line text.
        private static Family HomedFamily()
        {
            var f = new Family(9, "Archer", 0, 0);
            f.GetType().GetProperty("HomeHouseId").SetValue(f, (int?)5);
            return f;
        }

        [UnityTest]
        public IEnumerator ClickingTheHouseOrItsPlotSelectsTheHouse()
        {
            var fp = _host.World.Footprints.Get(_houseId);
            var c = fp.Centre;
            Assert.IsTrue(_agents.ClickAt(ScreenOf((float)c.XMetres, (float)c.ZMetres)));
            Assert.IsTrue(_agents.HasHouse);
            Assert.AreEqual(_houseId, _agents.SelectedHouseId);
            _agents.ClickAt(new Vector2(-50f, -50f));
            Assert.IsFalse(_agents.HasHouse, "Clicking empty ground clears the house.");

            // A point on the plot but away from the house footprint selects it through the plot.
            Assert.IsTrue(AgentPicker.TryPickHouse(_host.World, M(40.5, 51.8), out int viaPlot));
            Assert.AreEqual(_houseId, viaPlot);
            Assert.IsNotNull(_host.World.Areas.Get(_plotId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnbuiltHouseShowsStateEmptyPriorityAndConfirmedDeconstructRemovesIt()
        {
            _agents.SelectHouse(_houseId);
            yield return null;
            Assert.IsTrue(_house.IsShown);
            StringAssert.StartsWith("State: Delivering", _house.StateText);
            Assert.AreEqual("Empty", _house.HouseholdText);
            Assert.IsTrue(_house.PriorityVisible);
            Assert.AreEqual("High priority: off", _house.PriorityText);
            _house.ClickPriority();
            Assert.AreEqual(ConstructionPriority.High, _host.World.Construction.Get(_houseId).Priority);
            Assert.AreEqual("High priority: on", _house.PriorityText);
            var b = _house.PanelElement.panel != null ? _house.PanelElement.worldBound : default;
            Assert.Greater(b.width, 0f);

            _house.ClickDeconstruct();
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.IsNotNull(_host.World.Footprints.Get(_houseId), "Cancel keeps the house.");
            _house.ClickDeconstruct();
            ConfirmDialog.Confirm();
            Assert.IsNull(_host.World.Footprints.Get(_houseId));
            Assert.IsNull(_host.World.Areas.Get(_plotId), "The plot goes with the unbuilt house.");
            yield return null;
            Assert.IsFalse(_house.IsShown);
        }

        [UnityTest]
        public IEnumerator BuiltHouseShowsHouseholdAndCitizenShowsHomeLine()
        {
            var w = _host.World;
            var family = w.People.FindFamily(1);
            _agents.Select(AgentRef.Citizen(1));
            yield return null;
            Assert.AreEqual("Homeless (camp)", _citizen.HomeText);

            w.Construction.Remove(_houseId);
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(_houseId), family));
            _agents.SelectHouse(_houseId);
            yield return null;
            Assert.IsTrue(_house.IsShown);
            Assert.IsFalse(_citizen.IsShown, "Selecting a house clears the citizen.");
            Assert.AreEqual("State: Built", _house.StateText);
            Assert.AreEqual("Household: Archer", _house.HouseholdText);
            Assert.AreEqual("Members: Alda, Bran", _house.MembersText);
            Assert.IsFalse(_house.PriorityVisible, "No priority once built.");
            Assert.IsTrue(_house.DeconstructVisible);

            _house.ClickDeconstruct();
            ConfirmDialog.Confirm();
            Assert.IsNotNull(w.Deconstruction.Get(_houseId));
            yield return null;
            StringAssert.StartsWith("State: Deconstructing", _house.StateText);

            _agents.Select(AgentRef.Citizen(1));
            yield return null;
            Assert.IsFalse(_house.IsShown);
            Assert.AreEqual("Homeless (camp)", _citizen.HomeText, "Marking the house evicts the family (HM-14).");
            Assert.AreEqual("Home: Archer house", CitizenPanelView.HomeLine(HomedFamily()));
        }

        [UnityTest]
        public IEnumerator HousePanelShowsTheHouseLarderByTypeAndDaysLeft()
        {
            var w = _host.World;
            var family = w.People.Families.First();
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(_houseId), family));
            w.Inventory.Produce(Larders.ForHome(w, w.Homes.Get(_houseId)), Items.Meat, 2);
            w.Inventory.Produce(Larders.ForHome(w, w.Homes.Get(_houseId)), Items.Berries, 3);
            _agents.SelectHouse(_houseId);
            yield return null;

            Assert.IsTrue(_house.IsShown);
            StringAssert.Contains("Meat: 2 (6 rations)", _house.LarderText);
            StringAssert.Contains("Berries: 3 (3 rations)", _house.LarderText);
            StringAssert.Contains("Mushrooms: 0", _house.LarderText);
            double days = 9.0 / (2 * w.Tuning.AdultRations);
            StringAssert.Contains("Days left: " + days.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), _house.LarderText);
            Assert.AreEqual("", _house.HungryText, "Fed household is not Hungry.");
            var b = _house.PanelElement.worldBound;
            Assert.Greater(b.width, 0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HousePanelShowsHungryWhenTheHouseLarderRanShortAtMidnight()
        {
            var w = _host.World;
            var family = w.People.Families.First();
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(_houseId), family));
            _agents.SelectHouse(_houseId);
            // Empty larder: the household eats nothing at the next midnight and is Hungry (FD-16).
            int guard = 0;
            do { w.Step(); guard++; } while (!(w.Now.Hour == 0 && w.Now.Minute == 0) && guard < 5000);
            yield return null;

            Assert.IsTrue(family.IsHungry);
            Assert.AreEqual("Hungry", _house.HungryText);
            StringAssert.Contains("Days left: 0.0", _house.LarderText);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MarketAccessLineFollowsMarketPlacementAndRemoval()
        {
            var w = _host.World;
            _agents.SelectHouse(_houseId);
            yield return null;
            Assert.IsTrue(_house.IsShown);
            // No camp in this fixture and no market yet: the house has no access (MK-44).
            Assert.AreEqual("Market access: No", _house.MarketAccessText);

            var r = _host.Execute(new DesignateArea(AreaShape.Straight(new[] { M(90, 90), M(130, 90), M(130, 130), M(90, 130) }), AreaTypes.MarketId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int areaId = w.Markets.Current.AreaId;
            yield return null;
            Assert.AreEqual("Market access: Yes", _house.MarketAccessText, "A market within walking range of the house gives access.");

            r = _host.Execute(new DeleteArea(areaId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            yield return null;
            Assert.AreEqual("Market access: No", _house.MarketAccessText, "Removing the market takes access away.");
        }
    }
}
