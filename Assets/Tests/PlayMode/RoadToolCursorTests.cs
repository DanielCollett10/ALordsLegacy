using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests that the road tool shows the cursor marker at the pointer and hides it on deactivate.</summary>
    public class RoadToolCursorTests
    {
        private GameObject _hostGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private RoadTool _tool;
        private ToolController _controller;
        private Camera _cam;

        [TearDown]
        public void TearDown()
        {
            if (_toolGo != null) Object.Destroy(_toolGo);
            if (_panelGo != null) Object.Destroy(_panelGo);
            if (_camGo != null) Object.Destroy(_camGo);
            if (_groundGo != null) Object.Destroy(_groundGo);
            if (_hostGo != null) Object.Destroy(_hostGo);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
        }

        private IEnumerator Setup()
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;

            _camGo = new GameObject("TestCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 60f;
            _camGo.transform.position = new Vector3(100f, 50f, 100f);
            _camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _groundGo = new GameObject("TestGround");
            _groundGo.transform.position = new Vector3(100f, -0.5f, 100f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(1000f, 1f, 1000f);

            var pickerGo = new GameObject("Picker");
            pickerGo.transform.SetParent(_groundGo.transform, false);
            var picker = pickerGo.AddComponent<GroundPicker>();
            picker.PickCamera = _cam;
            picker.Host = _host;

            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            var panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("RoadTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<RoadTool>();
            _tool.Configure(_host, picker, null, panel, _controller);
            _tool.SetViewSettings(new ViewSettings());
            _controller.Register(_tool);
            yield return null; // let physics see the ground collider
            _controller.Toggle(RoadTool.ToolId);
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        [UnityTest]
        public IEnumerator PointerOverGroundShowsRingAtSimHeightAndDeactivateHidesIt()
        {
            yield return Setup();
            Assert.IsNull(_toolGo.GetComponentInChildren<CursorMarker>(), "no marker before the first pointer move");

            _tool.PointerMove(Screen(100f, 100f));

            var marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.IsNotNull(marker, "the road tool must create the cursor marker as a child");
            Assert.IsTrue(marker.IsShown);
            Assert.AreEqual(CursorMarkerShape.Ring, marker.Shape);
            Assert.AreEqual(2.5f, marker.DiameterM, 0.01f, "ring diameter is the dirt path width");
            Assert.AreEqual(100f, marker.transform.position.x, 0.2f);
            Assert.AreEqual(100f, marker.transform.position.z, 0.2f);
            float groundY = _host.World.Terrain.HeightAt(marker.transform.position.x, marker.transform.position.z);
            Assert.AreEqual(groundY + CursorMarker.HeightOffsetM, marker.transform.position.y, 0.001f);

            _tool.Deactivate();
            Assert.IsFalse(marker.IsShown, "deactivating the road tool hides the marker");
        }
    }
}
