using LordsLegacy.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the shared <see cref="ConfirmDialog"/> (T-396): message, buttons, input block, Escape.</summary>
    public class ConfirmDialogTests
    {
        private Keyboard _keyboard;
        private int _confirms;

        [SetUp]
        public void SetUp()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _confirms = 0;
            ConfirmDialog.Cancel();
        }

        [TearDown]
        public void TearDown()
        {
            ConfirmDialog.Cancel();
            var host = Object.FindFirstObjectByType<ConfirmDialogHost>();
            if (host != null)
            {
                var doc = host.GetComponent<UIDocument>();
                var ps = doc.panelSettings;
                Object.DestroyImmediate(host.gameObject);
                if (ps != null) Object.DestroyImmediate(ps);
            }
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        }

        private VisualElement Root() => Object.FindFirstObjectByType<ConfirmDialogHost>().GetComponent<UIDocument>().rootVisualElement;

        /// <summary>
        /// Queues a full keyboard state (the given keys down). It is processed by the next frame's Input System update,
        /// before Update runs, so <c>wasPressedThisFrame</c> is true in that frame's Update. The caller yields one frame.
        /// </summary>
        private void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        }

        /// <summary>
        /// Presses a dialog button. The buttons' clicked handlers are exactly these two calls, and a synthetic ClickEvent does
        /// not reach a Button's clickable, so this calls them directly (like ToolPanelView.ClickCancel in other tests).
        /// The button elements are still checked to exist.
        /// </summary>
        private void Click(string name)
        {
            Assert.IsNotNull(Root().Q<Button>(name), name + " must exist");
            if (name == "confirm-dialog-cancel") ConfirmDialog.Cancel();
            else ConfirmDialog.Confirm();
        }

        [Test]
        public void ShowOpensWithMessageConfirmTextAndBlocksGameInput()
        {
            Assert.IsFalse(GameMenuState.IsOpen);
            ConfirmDialog.Show("Deconstruct this hut? Half its logs come back.", "Deconstruct", () => _confirms++);

            Assert.IsTrue(ConfirmDialog.IsOpen);
            Assert.IsTrue(GameMenuState.IsOpen, "An open dialog must block tool and camera input.");
            Assert.AreEqual("Deconstruct this hut? Half its logs come back.", Root().Q<Label>("confirm-dialog-message").text);
            Assert.AreEqual("Deconstruct", Root().Q<Button>("confirm-dialog-confirm").text);
            Assert.AreEqual("Cancel", Root().Q<Button>("confirm-dialog-cancel").text);
            Assert.AreEqual(DisplayStyle.Flex, Root().Q("confirm-dialog-backdrop").style.display.value);
            Assert.AreEqual(0, _confirms, "Showing must not confirm.");
        }

        [Test]
        public void CancelButtonClosesWithoutConfirming()
        {
            ConfirmDialog.Show("Delete save 'Autumn'?", "Delete", () => _confirms++);
            Click("confirm-dialog-cancel");

            Assert.IsFalse(ConfirmDialog.IsOpen);
            Assert.IsFalse(GameMenuState.IsOpen, "Closing must release the input block.");
            Assert.AreEqual(0, _confirms);
            Assert.AreEqual(DisplayStyle.None, Root().Q("confirm-dialog-backdrop").style.display.value);
        }

        [Test]
        public void ConfirmButtonClosesAndRunsActionOnce()
        {
            ConfirmDialog.Show("Deconstruct the work area?", "Deconstruct", () => _confirms++);
            Click("confirm-dialog-confirm");

            Assert.IsFalse(ConfirmDialog.IsOpen);
            Assert.IsFalse(GameMenuState.IsOpen);
            Assert.AreEqual(1, _confirms);

            Click("confirm-dialog-confirm");
            Assert.AreEqual(1, _confirms, "A closed dialog must not run its action again.");
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator EscapeCancelsWithoutConfirming()
        {
            ConfirmDialog.Show("Deconstruct?", "Deconstruct", () => _confirms++);
            yield return null;

            SetKeys(Key.Escape);
            yield return null;
            SetKeys();

            Assert.IsFalse(ConfirmDialog.IsOpen, "Escape must cancel the dialog.");
            Assert.IsFalse(GameMenuState.IsOpen);
            Assert.AreEqual(0, _confirms);
        }

        private sealed class StubTool : LordsLegacy.Game.Tools.ITool
        {
            public string Id => "stub";
            public bool HasDraft => false;
            public void Activate() { }
            public void Deactivate() { }
            public void Cancel() { }
            public bool HandleEscape() { return false; }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator EscapeThatClosesDialogDoesNotDeselectTool()
        {
            var go = new GameObject("ToolController");
            var ctl = go.AddComponent<LordsLegacy.Game.Tools.ToolController>();
            ctl.Register(new StubTool());
            ctl.Toggle("stub");
            Assert.IsNotNull(ctl.ActiveTool);

            ConfirmDialog.Show("Deconstruct?", "Deconstruct", () => { });
            yield return null;
            SetKeys(Key.Escape);
            yield return null;
            try
            {
                Assert.IsFalse(ConfirmDialog.IsOpen);
                Assert.IsTrue(ConfirmDialog.OwnsEscape, "The Escape that closed the dialog stays owned for the rest of the frame.");
                Assert.IsNotNull(ctl.ActiveTool, "That Escape must not also deselect the tool.");
                SetKeys();
                yield return null;
                Assert.IsFalse(ConfirmDialog.OwnsEscape, "Ownership ends with the frame.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShowingAgainReplacesMessageAndAction()
        {
            ConfirmDialog.Show("First", "One", () => Assert.Fail("The replaced action must not run."));
            ConfirmDialog.Show("Second", "Two", () => _confirms++);
            Assert.AreEqual("Second", Root().Q<Label>("confirm-dialog-message").text);

            Click("confirm-dialog-confirm");
            Assert.AreEqual(1, _confirms);
        }
    }
}
