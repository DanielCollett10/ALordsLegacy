using System;
using LordsLegacy.Sim.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Fills the Save panel of the game menu (SV-10, SV-12, SV-13): a name field pre-filled with the game date, a Confirm
    /// button that writes <c>SimSave.Write</c> with the real creation time (Unix seconds, read here on the Unity side),
    /// and a "Saved" note. The menu pauses the Sim, so the save is taken between ticks.
    /// </summary>
    [RequireComponent(typeof(GameMenuView))]
    public sealed class SavePanelController : MonoBehaviour
    {
        [SerializeField] private SimHost _host;

        private GameMenuView _menu;
        private TextField _nameField;
        private Button _confirm;
        private Label _note;
        private VisualElement _builtFor;

        /// <summary>Store used for writing. Defaults to <c>persistentDataPath/Saves</c>; tests replace it.</summary>
        public SaveStore Store { get; set; }

        /// <summary>Supplies the real creation time as Unix seconds. Tests replace it.</summary>
        public Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>The name field.</summary>
        public TextField NameField => _nameField;

        /// <summary>The Confirm button.</summary>
        public Button ConfirmButton => _confirm;

        /// <summary>The note under the button ("Saved" after a successful save, a reason after a failure).</summary>
        public Label NoteLabel => _note;

        /// <summary>Path of the last file written, or null.</summary>
        public string LastSavedPath { get; private set; }

        /// <summary>Raised after a manual save succeeds (the autosave timer restarts on it, SV-11).</summary>
        public event Action Saved;

        /// <summary>Sets the SimHost (for code that adds the component at run time).</summary>
        public void SetHost(SimHost host) { _host = host; }

        private void OnEnable()
        {
            _menu = GetComponent<GameMenuView>();
            _menu.SaveOpened += OnSaveOpened;
        }

        private void OnDisable()
        {
            if (_menu != null) _menu.SaveOpened -= OnSaveOpened;
        }

        private void OnSaveOpened()
        {
            Build();
            Prefill();
            _note.text = "";
        }

        /// <summary>Puts the current game date in the name field.</summary>
        public void Prefill()
        {
            var host = Host();
            _nameField.value = host != null && host.IsInitialized ? host.World.Now.Format() : "Save";
        }

        /// <summary>Confirm: writes a new save file. Returns true on success and shows "Saved".</summary>
        public bool Confirm()
        {
            Build();
            var host = Host();
            if (host == null || !host.IsInitialized || host.Map == null)
            {
                _note.text = "Cannot save: the game is not running.";
                return false;
            }
            try
            {
                string name = string.IsNullOrWhiteSpace(_nameField.value) ? host.World.Now.Format() : _nameField.value.Trim();
                byte[] bytes = SimSave.Write(host.World, name, Clock(), host.Map.MapId, host.Map.ContentHash);
                LastSavedPath = (Store ?? (Store = SaveStore.Default())).WriteNew(name, bytes);
                _note.text = "Saved";
                Saved?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Save failed: " + e);
                _note.text = "Save failed: " + e.Message;
                return false;
            }
        }

        private SimHost Host() => _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());

        private void Build()
        {
            var content = _menu.SaveContent;
            if (content == null) return;
            if (_builtFor == content && _nameField != null) return;
            content.Clear();
            _nameField = new TextField("Name") { name = "save-name" };
            _confirm = new Button(() => Confirm()) { name = "save-confirm", text = "Confirm" };
            _note = new Label("") { name = "save-note" };
            // Explicit light colours: the panel background is dark and default label text is not guaranteed to be readable.
            var light = new Color(240f / 255f, 230f / 255f, 210f / 255f);
            _nameField.labelElement.style.color = light;
            _nameField.labelElement.style.minWidth = 50;
            // The typed text sits in the inner input element: give it a dark background so light text is readable.
            var input = _nameField.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.color = light;
                input.style.backgroundColor = new Color(55f / 255f, 46f / 255f, 36f / 255f);
            }
            _nameField.style.marginBottom = 6;
            _confirm.style.paddingTop = _confirm.style.paddingBottom = 6;
            _note.style.color = new Color(150f / 255f, 230f / 255f, 150f / 255f);
            _note.style.fontSize = 14;
            _note.style.minHeight = 20;
            _note.style.marginTop = 6;
            _note.style.unityTextAlign = TextAnchor.MiddleCenter;
            content.Add(_nameField);
            content.Add(_confirm);
            content.Add(_note);
            _builtFor = content;
            // The name field must keep keyboard focus for typing; the button follows UIK-01 and never takes focus.
            _confirm.focusable = false;
        }
    }
}
