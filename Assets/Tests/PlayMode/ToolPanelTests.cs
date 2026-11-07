using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    public class ToolPanelTests
    {
        private GameObject _uiGo;
        private PanelSettings _panel;
        private ToolPanelView _view;

        [SetUp]
        public void SetUp()
        {
            _uiGo = new GameObject("ToolPanel");
            _uiGo.SetActive(false);
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _view = _uiGo.AddComponent<ToolPanelView>();
            _uiGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_panel);
        }

        private VisualElement Root() => _uiGo.GetComponent<UIDocument>().rootVisualElement;
        private VisualElement PanelElement() => Root().Q<VisualElement>("tool-panel");
        private Button CancelButton() => Root().Q<Button>("tool-panel-cancel");
        private Button ConfirmButton() => Root().Q<Button>("tool-panel-confirm");

        [Test]
        public void HiddenByDefault()
        {
            Assert.IsFalse(_view.IsShown);
            Assert.AreEqual(DisplayStyle.None, PanelElement().style.display.value);
        }

        [Test]
        public void ButtonsAreNotFocusable()
        {
            Assert.IsFalse(CancelButton().focusable);
            Assert.IsFalse(ConfirmButton().focusable);
        }

        [Test]
        public void ShowSetsTextsAndVisibility()
        {
            _view.Show("Road · 3 points · 142 m · Valid", "Left-click add point", true);
            Assert.IsTrue(_view.IsShown);
            Assert.AreEqual(DisplayStyle.Flex, PanelElement().style.display.value);
            Assert.AreEqual("Road · 3 points · 142 m · Valid", Root().Q<Label>("tool-panel-status").text);
            Assert.AreEqual("Left-click add point", Root().Q<Label>("tool-panel-hint").text);

            _view.Hide();
            Assert.IsFalse(_view.IsShown);
            Assert.AreEqual(DisplayStyle.None, PanelElement().style.display.value);
        }

        [Test]
        public void ConfirmDisabledWhenInvalid()
        {
            _view.Show("Road · Invalid: too steep", "hint", false);
            Assert.IsFalse(_view.IsConfirmEnabled);
            Assert.IsFalse(ConfirmButton().enabledSelf);

            _view.Show("Road · Valid", "hint", true);
            Assert.IsTrue(_view.IsConfirmEnabled);
            Assert.IsTrue(ConfirmButton().enabledSelf);
        }

        [Test]
        public void ConfirmEventDoesNotFireWhenInvalid()
        {
            int confirmed = 0;
            _view.Confirmed += () => confirmed++;
            _view.Show("Road · Invalid", "hint", false);

            _view.ClickConfirm();
            Assert.AreEqual(0, confirmed);

            _view.Show("Road · Valid", "hint", true);
            _view.ClickConfirm();
            Assert.AreEqual(1, confirmed);
        }

        [Test]
        public void CancelEventFiresWhenShown()
        {
            int cancelled = 0;
            _view.Cancelled += () => cancelled++;

            _view.ClickCancel();
            Assert.AreEqual(0, cancelled, "Cancel must be ignored while hidden.");

            _view.Show("Road · Valid", "hint", true);
            _view.ClickCancel();
            Assert.AreEqual(1, cancelled);
        }

        [Test]
        public void HideDoesNotRaiseEvents()
        {
            int cancelled = 0, confirmed = 0;
            _view.Cancelled += () => cancelled++;
            _view.Confirmed += () => confirmed++;
            _view.Show("Road · Valid", "hint", true);
            _view.Hide();
            _view.ClickConfirm();
            _view.ClickCancel();
            Assert.AreEqual(0, cancelled);
            Assert.AreEqual(0, confirmed);
        }
    }
}
