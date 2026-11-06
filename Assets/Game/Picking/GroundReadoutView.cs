using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Shows the ground under the mouse cursor (x, z, height, slope) in one UI Toolkit label. Reads
    /// <see cref="GroundPicker"/> only; holds no game rules. The UIDocument should use the shared
    /// PanelSettings (<c>Assets/Game/UI/PanelSettings.asset</c>). If no UXML is assigned the label is built in code.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GroundReadoutView : MonoBehaviour
    {
        /// <summary>Name of the label element in the UXML.</summary>
        public const string LabelName = "ground-readout";

        [SerializeField] private GroundPicker _picker;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private StyleSheet _styleSheet;

        private Label _label;

        /// <summary>When true, Update picks under the mouse each frame. Tests set false and call <see cref="Show"/>.</summary>
        public bool FollowMouse { get; set; } = true;

        /// <summary>Picker to read from. Null means the first GroundPicker in the scene.</summary>
        public GroundPicker Picker { get => _picker; set => _picker = value; }

        /// <summary>The sample currently shown.</summary>
        public GroundSample Current { get; private set; } = GroundSample.None;

        /// <summary>The text currently shown in the label (empty before the label exists).</summary>
        public string Text => _label == null ? "" : _label.text;

        /// <summary>Shows a sample in the label.</summary>
        public void Show(GroundSample sample)
        {
            Current = sample;
            if (_label != null) _label.text = sample.Format();
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            if (_visualTree != null) _visualTree.CloneTree(root);
            else BuildFallback(root);
            if (_styleSheet != null) root.styleSheets.Add(_styleSheet);
            _label = root.Q<Label>(LabelName);
            Show(Current);
        }

        private static void BuildFallback(VisualElement root)
        {
            var label = new Label();
            label.name = LabelName;
            label.text = GroundSample.None.Format();
            root.Add(label);
        }

        private void Update()
        {
            if (!FollowMouse) return;
            if (_picker == null) _picker = FindFirstObjectByType<GroundPicker>();
            var mouse = Mouse.current;
            if (_picker == null || mouse == null) return;
            Show(_picker.TryPickScreen(mouse.position.ReadValue(), out var sample) ? sample : GroundSample.None);
        }
    }
}
