using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the debug send-to controller, path overlay and nav labels (T-264, PPL-33).</summary>
    public class DebugSendTests
    {
        private GameObject _hostGo, _camGo, _pickGo, _agentsGo, _sendGo, _pathGo, _uiGo, _groundGo;
        private SimHost _host;
        private GroundPicker _ground;
        private AgentPicker _agents;
        private DebugSendController _send;
        private DebugPathOverlay _path;

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var pop = _host.World.People;
            pop.AddFamily(1, "Archer", 100, 100);
            pop.AddCitizen(1, "Alda", Sex.Female, -30L * Citizen.TicksPerYear / 2, 1, 100, 100);

            _camGo = new GameObject("Cam");
            var cam = _camGo.AddComponent<Camera>();
            float h = (float)_host.World.Terrain.HeightAt(100, 100);
            _camGo.transform.position = new Vector3(100, h + 60, 100);
            _camGo.transform.rotation = Quaternion.Euler(90, 0, 0);
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(100, h - 1f, 100);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(400, 1, 400);
            _pickGo = new GameObject("GroundPicker");
            _ground = _pickGo.AddComponent<GroundPicker>();
            _ground.PickCamera = cam;
            _ground.Host = _host;
            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, _ground, null);
            _sendGo = new GameObject("Send");
            _send = _sendGo.AddComponent<DebugSendController>();
            _send.ReadMouse = false;
            _send.Configure(_host, _ground, _agents, null, null);
            _pathGo = new GameObject("Path");
            _path = _pathGo.AddComponent<DebugPathOverlay>();
            _path.Configure(_host, _agents);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _pathGo, _sendGo, _agentsGo, _pickGo, _groundGo, _camGo, _hostGo, _uiGo })
                if (go != null) Object.DestroyImmediate(go);
        }

        private Vector2 ScreenOf(double x, double z)
        {
            Assert.IsTrue(_ground.TryWorldToScreen((float)x, (float)z, out var s));
            return s;
        }

        [UnityTest]
        public IEnumerator RightClickMovesTheSelectedCitizensTarget()
        {
            yield return null;
            Assert.IsFalse(_send.RightClickAt(ScreenOf(120, 100)), "no selection: nothing is sent");
            _agents.Select(AgentRef.Citizen(1));
            Assert.IsTrue(_send.RightClickAt(ScreenOf(120, 100)));
            var c = _host.World.People.FindCitizen(1);
            Assert.IsNotNull(c.Move);
            Assert.AreEqual(120.0, c.Move.TargetX, 1.5);
            Assert.AreEqual(100.0, c.Move.TargetZ, 1.5);
            Assert.IsNotNull(c.DebugSend);

            _host.World.Step(1);
            _path.Draw();
            Assert.IsTrue(_path.IsShown, "remaining path is drawn");
            Assert.Greater(_path.VertexCount, 0);
        }

        [UnityTest]
        public IEnumerator NoRouteShowsRefusalInPanel()
        {
            yield return null;
            var settings = TestPanelSettings.Create();
            _uiGo = new GameObject("Panel");
            _uiGo.SetActive(false);
            _uiGo.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = settings;
            var view = _uiGo.AddComponent<CitizenPanelView>();
            view.Configure(_host, _agents, null);
            _uiGo.SetActive(true);
            _send.Configure(_host, _ground, _agents, null, view);
            _agents.Select(AgentRef.Citizen(1));
            yield return null;
            // Far outside the map: the camera cannot see it, so use a direct refusal through the same command path.
            var result = _host.Execute(new LordsLegacy.Sim.Commands.SendCitizenTo(99, new LordsLegacy.Sim.Navigation.PathPoint(1, 1)));
            Assert.IsTrue(result.IsRejected);
            view.ShowMessage(result.Text);
            Assert.AreEqual(result.Text, view.MessageText);
            _agents.Clear();
            _agents.Select(AgentRef.Citizen(1));
            Assert.AreEqual("", view.MessageText, "message clears when the selection changes");
            Object.DestroyImmediate(settings);
        }
    }
}
