using System.Collections.Generic;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    public class ToolControllerTests
    {
        private sealed class FakeTool : ITool
        {
            public readonly List<string> Calls;
            public bool EscapeConsumes;
            public FakeTool(string id, List<string> calls) { Id = id; Calls = calls; }
            public string Id { get; }
            public bool HasDraft => EscapeConsumes;
            public void Activate() => Calls.Add(Id + ".Activate");
            public void Deactivate() => Calls.Add(Id + ".Deactivate");
            public void Cancel() => Calls.Add(Id + ".Cancel");
            public bool HandleEscape() { Calls.Add(Id + ".Escape"); return EscapeConsumes; }
        }

        private GameObject _uiGo, _ctlGo, _camGo;
        private PanelSettings _panel;
        private BuildBarView _view;
        private ToolController _ctl;
        private CameraRig _rig;
        private List<string> _calls;
        private FakeTool _a, _b;

        [SetUp]
        public void SetUp()
        {
            _uiGo = new GameObject("BuildBar");
            _uiGo.SetActive(false);
            _panel = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _view = _uiGo.AddComponent<BuildBarView>();
            _uiGo.SetActive(true);

            _camGo = new GameObject("Rig");
            _rig = _camGo.AddComponent<CameraRig>();

            _ctlGo = new GameObject("Tools");
            _ctlGo.SetActive(false);
            _ctl = _ctlGo.AddComponent<ToolController>();
            _ctl.SetBar(_view);
            _ctl.SetCamera(_rig);
            _ctlGo.SetActive(true);

            _calls = new List<string>();
            _a = new FakeTool("road", _calls);
            _b = new FakeTool("test-area", _calls);
            _ctl.Register(_a);
            _ctl.Register(_b);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ctlGo);
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_panel);
        }

        [Test]
        public void BarShowsEntriesWithEnabledStatesTooltipsAndNoFocus()
        {
            Assert.IsNotNull(_view.GetButton("road"));
            Assert.IsFalse(_view.GetButton("road").ClassListContains("build-bar__button--disabled"));
            Assert.IsFalse(_view.GetButton("test-area").ClassListContains("build-bar__button--disabled"));
            Assert.IsFalse(_view.GetButton("test-work-area").ClassListContains("build-bar__button--disabled"));
            Assert.IsFalse(_view.GetButton("woodcutter").ClassListContains("build-bar__button--disabled"));
            foreach (var id in new[] { "storage-yard", "house", "granary" })
            {
                Assert.AreEqual("Coming later", _view.GetButton(id).tooltip, id);
                Assert.IsTrue(_view.GetButton(id).ClassListContains("build-bar__button--disabled"), id);
            }
            foreach (var e in ToolEntry.Defaults()) Assert.IsFalse(_view.GetButton(e.Id).focusable, e.Id);
        }

        [Test]
        public void ClickingButtonActivatesToolAndHighlightsIt()
        {
            _view.Click("road");
            Assert.AreSame(_a, _ctl.ActiveTool);
            Assert.IsTrue(_view.GetButton("road").ClassListContains("build-bar__button--active"));
            Assert.IsTrue(_rig.ToolActive);
        }

        [Test]
        public void SecondClickDeactivates()
        {
            _view.Click("road");
            _view.Click("road");
            Assert.IsNull(_ctl.ActiveTool);
            Assert.IsFalse(_view.GetButton("road").ClassListContains("build-bar__button--active"));
            Assert.IsFalse(_rig.ToolActive);
        }

        [Test]
        public void DisabledButtonDoesNothing()
        {
            // "house" is greyed out ("Coming later"); test-area is enabled since T-114.
            _view.Click("house");
            Assert.IsNull(_ctl.ActiveTool);
        }

        [Test]
        public void SwitchingCancelsOldToolThenActivatesNew()
        {
            _ctl.Toggle("road");
            _calls.Clear();
            _ctl.Toggle("test-area");
            CollectionAssert.AreEqual(new[] { "road.Cancel", "road.Deactivate", "test-area.Activate" }, _calls);
            Assert.AreSame(_b, _ctl.ActiveTool);
        }

        [Test]
        public void EscapeGoesToToolFirstAndStopsIfConsumed()
        {
            _ctl.Toggle("road");
            _a.EscapeConsumes = true;
            _calls.Clear();
            Assert.IsTrue(_ctl.HandleEscape());
            CollectionAssert.AreEqual(new[] { "road.Escape" }, _calls);
            Assert.AreSame(_a, _ctl.ActiveTool);
        }

        [Test]
        public void EscapeDeactivatesWhenToolReportsIdle()
        {
            _ctl.Toggle("road");
            _calls.Clear();
            _ctl.HandleEscape();
            CollectionAssert.AreEqual(new[] { "road.Escape", "road.Cancel", "road.Deactivate" }, _calls);
            Assert.IsNull(_ctl.ActiveTool);
        }

        [Test]
        public void EscapeWithNoToolIsNotHandled()
        {
            Assert.IsFalse(_ctl.HandleEscape());
        }

        [Test]
        public void CameraIgnoresLeftAndRightWhileToolActiveButNotMiddle()
        {
            Assert.IsTrue(_rig.AcceptsMouseButton(0));
            Assert.IsTrue(_rig.AcceptsMouseButton(1));
            _ctl.Toggle("road");
            Assert.IsFalse(_rig.AcceptsMouseButton(0));
            Assert.IsFalse(_rig.AcceptsMouseButton(1));
            Assert.IsTrue(_rig.AcceptsMouseButton(2));
            _ctl.Toggle("road");
            Assert.IsTrue(_rig.AcceptsMouseButton(0));
        }

        [Test]
        public void PointerOverUiIsFalseByDefault()
        {
            Assert.IsFalse(_ctl.IsPointerOverUi);
        }
    }
}
