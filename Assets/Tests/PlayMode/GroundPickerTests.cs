using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>MVP-0-03: the cursor readout at a known point matches SimWorld.Terrain. No real mouse is used.</summary>
    public class GroundPickerTests
    {
        private const string SharedPanelPath = "Assets/Game/UI/PanelSettings.asset";

        private GameObject _hostGo, _camGo, _groundGo, _pickGo, _uiGo;
        private SimHost _host;
        private Camera _cam;
        private GroundPicker _picker;
        private GroundReadoutView _view;
        private PanelSettings _panel;

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("SimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.AutoUpdate = false;
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());

            // Camera straight down over (100, 100), 300 m up.
            _camGo = new GameObject("PickCamera");
            _camGo.transform.SetPositionAndRotation(new Vector3(100f, 300f, 100f), Quaternion.Euler(90f, 0f, 0f));
            _cam = _camGo.AddComponent<Camera>();

            // Flat stand-in for the map-1 TerrainCollider: top face at y = 0, covering x,z in [-500, 1500].
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(500f, -0.5f, 500f);
            var box = _groundGo.AddComponent<BoxCollider>();
            box.size = new Vector3(2000f, 1f, 2000f);
            Physics.SyncTransforms();

            _pickGo = new GameObject("GroundPicker");
            _picker = _pickGo.AddComponent<GroundPicker>();
            _picker.PickCamera = _cam;
            _picker.Host = _host;

            _uiGo = new GameObject("GroundReadout");
            _uiGo.SetActive(false);
            _panel = LoadSharedPanel();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _view = _uiGo.AddComponent<GroundReadoutView>();
            _view.Picker = _picker;
            _view.FollowMouse = false;
            _uiGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_pickGo);
            Object.DestroyImmediate(_groundGo);
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_hostGo);
        }

        private static PanelSettings LoadSharedPanel()
        {
#if UNITY_EDITOR
            var shared = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(SharedPanelPath);
            Assert.IsNotNull(shared, "Shared PanelSettings missing; run 'Lords Legacy/Create Shared PanelSettings'.");
            return shared;
#else
            return ScriptableObject.CreateInstance<PanelSettings>();
#endif
        }

        [Test]
        public void ReadoutAtKnownPointsMatchesSimTerrain()
        {
            var points = new[] { new Vector2(100f, 100f), new Vector2(37.5f, 60.25f), new Vector2(150f, 20f) };
            foreach (var p in points)
            {
                // Geometry check uses an explicit downward ray so it does not depend on Camera matrices.
                var ray = new Ray(new Vector3(p.x, 300f, p.y), Vector3.down);
                Assert.IsTrue(_picker.TryPickRay(ray, out var sample), "No ground hit at " + p);
                Assert.IsTrue(sample.HasValue);
                Assert.AreEqual(p.x, sample.X, 0.05f, "x at " + p);
                Assert.AreEqual(p.y, sample.Z, 0.05f, "z at " + p);
                Assert.AreEqual(_host.World.Terrain.HeightAt(sample.X, sample.Z), sample.HeightM, 1e-4f, "height at " + p);
                Assert.AreEqual(_host.World.Terrain.SlopeDegAt(sample.X, sample.Z), sample.SlopeDeg, 1e-4f, "slope at " + p);

                _view.Show(sample);
                Assert.AreEqual(sample.Format(), _view.Text);
                Assert.IsTrue(_view.Text.Contains("height " + sample.HeightM.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        [Test]
        public void ScreenCentreHitsCameraAxisPoint()
        {
            // Screen centre looks straight down the camera axis, so it must hit (100, 100) regardless of projection details.
            var centre = new Vector2(_cam.pixelWidth * 0.5f, _cam.pixelHeight * 0.5f);
            Assert.IsTrue(_picker.TryPickScreen(centre, out var s));
            Assert.AreEqual(100f, s.X, 0.05f);
            Assert.AreEqual(100f, s.Z, 0.05f);
            Assert.AreEqual(_host.World.Terrain.HeightAt(s.X, s.Z), s.HeightM, 1e-4f);
            Assert.AreEqual(_host.World.Terrain.SlopeDegAt(s.X, s.Z), s.SlopeDeg, 1e-4f);
        }

        [Test]
        public void ReadAtMatchesSimTerrainWithoutRay()
        {
            var s = _picker.ReadAt(37.5f, 60.25f);
            Assert.IsTrue(s.HasValue);
            Assert.AreEqual(_host.World.Terrain.HeightAt(37.5f, 60.25f), s.HeightM, 1e-4f);
            Assert.AreEqual(_host.World.Terrain.SlopeDegAt(37.5f, 60.25f), s.SlopeDeg, 1e-4f);
        }

        [Test]
        public void MissReturnsEmptySample()
        {
            // Ray pointing up from above the ground cannot hit anything.
            Assert.IsFalse(_picker.TryPickRay(new Ray(new Vector3(100f, 300f, 100f), Vector3.up), out var sample));
            Assert.IsFalse(sample.HasValue);
            Assert.AreEqual(GroundSample.None.Format(), sample.Format());
        }
    }
}
