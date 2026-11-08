using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="CursorMarker"/> with a synthetic SimHost.</summary>
    public class CursorMarkerTests
    {
        private GameObject _hostGo;
        private GameObject _rootGo;
        private SimHost _host;
        private CursorMarker _marker;

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            _rootGo = new GameObject("ToolRoot");
            _marker = CursorMarker.Create(_rootGo.transform, _host);
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootGo != null) Object.Destroy(_rootGo);
            if (_hostGo != null) Object.Destroy(_hostGo);
        }

        [Test]
        public void ShowPlacesMarkerAtSimHeightPlusOffsetAndHideTurnsItOff()
        {
            _marker.SetShape(CursorMarkerShape.Ring, 2f);
            _marker.Show(30f, 40f, false);

            Assert.IsTrue(_marker.IsShown);
            Assert.AreEqual(30f, _marker.transform.position.x, 0.001f);
            Assert.AreEqual(40f, _marker.transform.position.z, 0.001f);
            float expectedY = _host.World.Terrain.HeightAt(30f, 40f) + CursorMarker.HeightOffsetM;
            Assert.AreEqual(expectedY, _marker.transform.position.y, 0.001f);
            Assert.AreEqual(_marker.transform, _rootGo.transform.GetChild(0), "marker must be a child of the tool root");

            _marker.Hide();
            Assert.IsFalse(_marker.IsShown);
        }

        [Test]
        public void SnappedShowSwitchesToADifferentColour()
        {
            _marker.SetShape(CursorMarkerShape.Sphere, 1f);
            _marker.Show(20f, 20f, false);
            Assert.AreEqual(CursorMarker.NormalColor, _marker.CurrentColor);
            Assert.IsFalse(_marker.IsSnapped);

            _marker.Show(20f, 20f, true);
            Assert.IsTrue(_marker.IsSnapped);
            Assert.AreEqual(CursorMarker.SnappedColor, _marker.CurrentColor);
            Assert.AreNotEqual(CursorMarker.NormalColor, CursorMarker.SnappedColor);
            Assert.AreEqual(CursorMarker.SnappedColor,
                _marker.GetComponent<MeshRenderer>().sharedMaterial.color);

            _marker.Show(20f, 20f, false);
            Assert.AreEqual(CursorMarker.NormalColor,
                _marker.GetComponent<MeshRenderer>().sharedMaterial.color);
        }

        [Test]
        public void MarkerHasNoCollider()
        {
            _marker.SetShape(CursorMarkerShape.Ring, 2f);
            _marker.Show(10f, 10f, false);
            Assert.IsNull(_marker.GetComponent<Collider>());
        }

        [Test]
        public void RingAndSphereUseCallerDiameter()
        {
            _marker.SetShape(CursorMarkerShape.Ring, 2f);
            Bounds ring = _marker.CurrentMesh.bounds;
            Assert.AreEqual(2f, ring.size.x, 0.01f);
            Assert.AreEqual(2f, ring.size.z, 0.01f);
            Assert.AreEqual(0f, ring.size.y, 0.001f);

            _marker.SetShape(CursorMarkerShape.Sphere, 3f);
            Bounds sphere = _marker.CurrentMesh.bounds;
            Assert.AreEqual(CursorMarkerShape.Sphere, _marker.Shape);
            Assert.AreEqual(3f, _marker.DiameterM, 0.001f);
            Assert.AreEqual(3f, sphere.size.x, 0.05f);
            Assert.AreEqual(3f, sphere.size.y, 0.05f);
        }
    }
}
