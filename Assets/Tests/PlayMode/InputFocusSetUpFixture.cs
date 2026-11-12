using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>
    /// Makes synthetic keyboard/mouse events (InputSystem.QueueStateEvent) work whether or not the
    /// Game view / player window has focus (a Test Runner run has none). Runs once for every
    /// PlayMode test in this namespace: swaps in a test-only copy of the InputSettings that ignores
    /// focus, and restores the original settings afterwards. Game code and project settings are untouched.
    /// </summary>
    [SetUpFixture]
    public class InputFocusSetUpFixture
    {
        private InputSettings _original;
        private InputSettings _testSettings;

        [OneTimeSetUp]
        public void ForceInputWithoutFocus()
        {
            _original = InputSystem.settings;
            _testSettings = ScriptableObject.CreateInstance<InputSettings>();
            if (_original != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_original), _testSettings);
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _testSettings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = _testSettings;
        }

        [OneTimeTearDown]
        public void RestoreInputSettings()
        {
            if (_original != null) InputSystem.settings = _original;
            if (_testSettings != null) Object.DestroyImmediate(_testSettings);
        }
    }
}
