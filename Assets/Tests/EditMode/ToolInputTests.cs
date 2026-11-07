using System.Collections.Generic;
using LordsLegacy.Game.Tools.Shared;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    public class ToolInputTests
    {
        private static readonly Vector2 Cur = new Vector2(100, 100);
        private static readonly int[] Order = { 2, 1, 0 };

        private static HitCandidate P(ToolHitKind k, int id, float dx)
        {
            return HitCandidate.Point(k, id, Cur + new Vector2(dx, 0));
        }

        [Test]
        public void Priority_VertexBeatsEverything()
        {
            var list = new List<HitCandidate> { P(ToolHitKind.ConfirmedObject, 4, 0), P(ToolHitKind.Edge, 3, 0), P(ToolHitKind.Handle, 2, 0), P(ToolHitKind.Vertex, 1, 10) };
            var h = ToolHitTester.Test(list, Cur);
            Assert.AreEqual(ToolHitKind.Vertex, h.Kind);
            Assert.AreEqual(1, h.Id);
        }

        [Test]
        public void Priority_HandleBeatsEdge()
        {
            var list = new List<HitCandidate> { P(ToolHitKind.ConfirmedObject, 4, 0), P(ToolHitKind.Edge, 3, 0), P(ToolHitKind.Handle, 2, 5) };
            Assert.AreEqual(ToolHitKind.Handle, ToolHitTester.Test(list, Cur).Kind);
        }

        [Test]
        public void Priority_EdgeBeatsConfirmed()
        {
            var list = new List<HitCandidate> { P(ToolHitKind.ConfirmedObject, 4, 0), P(ToolHitKind.Edge, 3, 8) };
            Assert.AreEqual(ToolHitKind.Edge, ToolHitTester.Test(list, Cur).Kind);
        }

        [Test]
        public void Priority_ConfirmedBeatsGround()
        {
            var h = ToolHitTester.Test(new List<HitCandidate> { P(ToolHitKind.ConfirmedObject, 4, 3) }, Cur);
            Assert.AreEqual(ToolHitKind.ConfirmedObject, h.Kind);
            Assert.AreEqual(4, h.Id);
        }

        [Test]
        public void NothingInRange_IsGround()
        {
            var h = ToolHitTester.Test(new List<HitCandidate> { P(ToolHitKind.Vertex, 1, 13) }, Cur);
            Assert.AreEqual(ToolHitKind.Ground, h.Kind);
            Assert.AreEqual(-1, h.Id);
            Assert.AreEqual(ToolHitKind.Ground, ToolHitTester.Test(new List<HitCandidate>(), Cur).Kind);
        }

        [Test]
        public void Radius_ExactlyTwelveHits_CustomRadiusRespected()
        {
            var list = new List<HitCandidate> { P(ToolHitKind.Vertex, 1, 12) };
            Assert.AreEqual(ToolHitKind.Vertex, ToolHitTester.Test(list, Cur).Kind);
            Assert.AreEqual(ToolHitKind.Ground, ToolHitTester.Test(list, Cur, 11f).Kind);
        }

        [Test]
        public void Tie_NearestWithinKindWins()
        {
            var list = new List<HitCandidate> { P(ToolHitKind.Vertex, 1, 9), P(ToolHitKind.Vertex, 2, -3), P(ToolHitKind.Vertex, 3, 6) };
            Assert.AreEqual(2, ToolHitTester.Test(list, Cur).Id);
        }

        [Test]
        public void Edge_UsesDistanceToSegment()
        {
            var edge = new HitCandidate(ToolHitKind.Edge, 7, new Vector2(0, 105), new Vector2(200, 105));
            var h = ToolHitTester.Test(new List<HitCandidate> { edge }, Cur);
            Assert.AreEqual(ToolHitKind.Edge, h.Kind);
            Assert.AreEqual(5f, h.Distance, 0.001f);
        }

        [Test]
        public void Click_WithinThreshold()
        {
            var t = new PressTracker();
            t.Press(PressButton.Left, Cur);
            t.Move(Cur + new Vector2(4, 0));
            Assert.AreEqual(PressKind.Click, t.Release(PressButton.Left, Cur + new Vector2(4, 0), 0f).Kind);
        }

        [Test]
        public void Drag_PastThreshold_EvenIfReturned()
        {
            var t = new PressTracker();
            t.Press(PressButton.Left, Cur);
            t.Move(Cur + new Vector2(5, 0));
            Assert.IsTrue(t.IsDragging(PressButton.Left));
            Assert.AreEqual(PressKind.Drag, t.Release(PressButton.Left, Cur, 0f).Kind);
        }

        [Test]
        public void RightDrag_IsNeverARightClick()
        {
            var t = new PressTracker();
            t.Press(PressButton.Right, Cur);
            t.Move(Cur + new Vector2(20, 0));
            var r = t.Release(PressButton.Right, Cur + new Vector2(20, 0), 0f);
            Assert.AreEqual(PressKind.Drag, r.Kind);
            Assert.AreEqual(PressButton.Right, r.Button);
        }

        [Test]
        public void Buttons_AreTrackedSeparately()
        {
            var t = new PressTracker();
            t.Press(PressButton.Left, Cur);
            t.Press(PressButton.Right, Cur);
            t.Move(Cur + new Vector2(10, 0));
            Assert.IsTrue(t.IsDragging(PressButton.Left));
            Assert.IsTrue(t.IsDragging(PressButton.Right));
            t.Release(PressButton.Left, Cur + new Vector2(10, 0), 0f);
            Assert.IsFalse(t.IsDown(PressButton.Left));
            Assert.IsTrue(t.IsDown(PressButton.Right));
            Assert.AreEqual(PressKind.None, t.Release(PressButton.Left, Cur, 0f).Kind);
            t.Release(PressButton.Right, Cur, 0f);
            t.Press(PressButton.Right, Cur);
            Assert.AreEqual(PressKind.Click, t.Release(PressButton.Right, Cur, 1f).Kind);
        }

        [Test]
        public void DoubleClick_WithinTimeAndDistance()
        {
            var t = new PressTracker(0.5f);
            t.Press(PressButton.Left, Cur);
            t.Release(PressButton.Left, Cur, 1f);
            t.Press(PressButton.Left, Cur + new Vector2(6, 0));
            Assert.AreEqual(PressKind.DoubleClick, t.Release(PressButton.Left, Cur + new Vector2(6, 0), 1.4f).Kind);
            t.Press(PressButton.Left, Cur);
            Assert.AreEqual(PressKind.Click, t.Release(PressButton.Left, Cur, 1.5f).Kind);
        }

        [Test]
        public void NotDoubleClick_TooSlowOrTooFar()
        {
            var t = new PressTracker(0.5f);
            t.Press(PressButton.Left, Cur);
            t.Release(PressButton.Left, Cur, 1f);
            t.Press(PressButton.Left, Cur);
            Assert.AreEqual(PressKind.Click, t.Release(PressButton.Left, Cur, 1.6f).Kind);
            t.Press(PressButton.Left, Cur + new Vector2(7, 0));
            Assert.AreEqual(PressKind.Click, t.Release(PressButton.Left, Cur + new Vector2(7, 0), 1.7f).Kind);
        }

        [Test]
        public void Snap_KindOrderBeatsDistance()
        {
            var l = new List<SnapCandidate> { new SnapCandidate(0, 10, 1f), new SnapCandidate(2, 11, 9f) };
            Assert.IsTrue(SnapPicker.TryPick(l, Order, 12f, true, out var r));
            Assert.AreEqual(11, r.Id);
        }

        [Test]
        public void Snap_NearestWithinKind_AndLimit()
        {
            var l = new List<SnapCandidate> { new SnapCandidate(1, 1, 8f), new SnapCandidate(1, 2, 3f), new SnapCandidate(2, 3, 13f) };
            Assert.IsTrue(SnapPicker.TryPick(l, Order, 12f, true, out var r));
            Assert.AreEqual(2, r.Id);
            Assert.IsFalse(SnapPicker.TryPick(l, Order, 2f, true, out _));
        }

        [Test]
        public void Snap_Off_ReturnsNone()
        {
            var l = new List<SnapCandidate> { new SnapCandidate(2, 1, 0f) };
            Assert.IsFalse(SnapPicker.TryPick(l, Order, 12f, false, out _));
        }

        [Test]
        public void Snap_KindNotInOrder_Ignored()
        {
            var l = new List<SnapCandidate> { new SnapCandidate(9, 1, 0f) };
            Assert.IsFalse(SnapPicker.TryPick(l, Order, 12f, true, out _));
        }
    }
}
