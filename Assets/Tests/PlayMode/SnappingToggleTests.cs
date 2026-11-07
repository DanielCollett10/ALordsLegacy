using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    public class SnappingToggleTests
    {
        private GameObject _uiGo;
        private PanelSettings _panel;
        private TopBarController _bar;
        private Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            // A virtual keyboard, so the ToggleSnapping action (bound to <Keyboard>/alt) has a device to read.
            _keyboard = InputSystem.AddDevice<Keyboard>();

            _uiGo = new GameObject("TopBar");
            _uiGo.SetActive(false);
            _panel = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _bar = _uiGo.AddComponent<TopBarController>();
            _uiGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_panel);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        }

        private Button SnappingButton() =>
            _uiGo.GetComponent<UIDocument>().rootVisualElement.Q<Button>("snapping-toggle");

        /// <summary>Sends a full keyboard state (the given keys down) and processes one Input System update.</summary>
        private void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        [Test]
        public void SettingDefaultsAreOnAndTwelvePixels()
        {
            var settings = new ViewSettings();
            Assert.IsTrue(settings.Snapping);
            Assert.AreEqual(12f, settings.SnapDistancePx);
        }

        [Test]
        public void ButtonIsNotFocusableAndStartsOn()
        {
            var button = SnappingButton();
            Assert.IsNotNull(button);
            Assert.IsFalse(button.focusable);
            Assert.AreEqual("Snapping: On", button.text);
        }

        [Test]
        public void ButtonClickTogglesSettingAndLabel()
        {
            var button = SnappingButton();
            _bar.ToggleSnapping(); // the same method the button's clicked event calls
            Assert.IsFalse(_bar.ViewSettings.Snapping);
            Assert.AreEqual("Snapping: Off", button.text);

            _bar.ToggleSnapping();
            Assert.IsTrue(_bar.ViewSettings.Snapping);
            Assert.AreEqual("Snapping: On", button.text);
        }

        [Test]
        public void AltPressTogglesSnappingAndLabel()
        {
            var button = SnappingButton();
            SetKeys(Key.LeftAlt);
            Assert.IsFalse(_bar.ViewSettings.Snapping);
            Assert.AreEqual("Snapping: Off", button.text);

            SetKeys(); // release
            SetKeys(Key.LeftAlt); // second press
            Assert.IsTrue(_bar.ViewSettings.Snapping);
            Assert.AreEqual("Snapping: On", button.text);
        }

        [Test]
        public void HoldingAltDoesNotToggleRepeatedly()
        {
            SetKeys(Key.LeftAlt);
            Assert.IsFalse(_bar.ViewSettings.Snapping);

            // Frames where Alt stays down, including repeated identical state events (OS key repeat).
            for (int i = 0; i < 10; i++) SetKeys(Key.LeftAlt);
            Assert.IsFalse(_bar.ViewSettings.Snapping, "holding Alt must toggle once, not every frame");

            SetKeys(); // release, no toggle on release
            Assert.IsFalse(_bar.ViewSettings.Snapping);
        }

        [Test]
        public void HotkeyStopsWhenTopBarIsDisabled()
        {
            _uiGo.SetActive(false);
            SetKeys(Key.LeftAlt);
            Assert.IsTrue(_bar.ViewSettings.Snapping);
        }
    }
}
