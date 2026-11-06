using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    public class CameraRigTests
    {
        private GameObject _go;
        private CameraRig _rig;
        private IHeightField _terrain;

        [SetUp]
        public void SetUp()
        {
            _terrain = SyntheticMap.CreateRamp().Heights;
            _go = new GameObject("Rig");
            _rig = _go.AddComponent<CameraRig>();
            _rig.SetTerrain(_terrain);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private void AssertValid()
        {
            Bounds2 b = _terrain.Bounds;
            Vector3 p = _go.transform.position;
            Assert.GreaterOrEqual(p.x, b.MinX); Assert.LessOrEqual(p.x, b.MaxX);
            Assert.GreaterOrEqual(p.z, b.MinZ); Assert.LessOrEqual(p.z, b.MaxZ);
            Assert.GreaterOrEqual(p.y, _terrain.HeightAt(p.x, p.z) + _rig.GroundMargin - 0.001f);
        }

        [Test]
        public void SetPosePlacesCamera()
        {
            _rig.SetPose(100, 100, 50, 45, 90);
            Assert.AreEqual(100f, _go.transform.position.x, 0.01f);
            Assert.AreEqual(_terrain.HeightAt(100, 100) + 50f, _go.transform.position.y, 0.01f);
            Assert.AreEqual(45f, _rig.PitchDeg, 0.01f);
            Assert.AreEqual(90f, _rig.YawDeg, 0.01f);
        }

        [Test]
        public void PanSpeedScalesWithHeight()
        {
            _rig.SetPose(100, 100, 10, 45, 0);
            _rig.Move(0, 1, 0.1f);
            float low = _rig.Z - 100;
            _rig.SetPose(100, 100, 100, 45, 0);
            _rig.Move(0, 1, 0.1f);
            float high = _rig.Z - 100;
            Assert.Greater(high, low * 2f);
        }

        [Test]
        public void DrivingToEveryEdgeAndMinZoomStaysInBoundsAndAboveGround()
        {
            float[][] dirs = { new[] { 1f, 0f }, new[] { -1f, 0f }, new[] { 0f, 1f }, new[] { 0f, -1f }, new[] { 1f, 1f }, new[] { -1f, -1f } };
            foreach (float yaw in new[] { 0f, 37f, 180f })
            foreach (var d in dirs)
            {
                _rig.SetPose(100, 100, 100, 60, yaw);
                for (int i = 0; i < 200; i++) { _rig.Move(d[0], d[1], 0.1f); AssertValid(); }
                for (int i = 0; i < 100; i++) { _rig.Zoom(1f); AssertValid(); }
                Assert.AreEqual(_rig.MinHeight, _rig.HeightAboveGround, 0.001f);
                for (int i = 0; i < 100; i++) { _rig.Zoom(-1f); AssertValid(); }
                Assert.AreEqual(_rig.MaxHeight, _rig.HeightAboveGround, 0.001f);
            }
        }

        [Test]
        public void RotateWrapsYaw()
        {
            _rig.SetPose(100, 100, 50, 45, 350);
            _rig.Rotate(20);
            Assert.AreEqual(10f, _rig.YawDeg, 0.01f);
        }
    }
}
