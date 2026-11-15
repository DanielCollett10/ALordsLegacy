using System;
using LordsLegacy.Sim.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Autosave into the single <c>Autosave</c> slot every <see cref="_intervalMinutes"/> real minutes (saving.md SV-11).
    /// The count runs while the Sim is loaded and the Esc menu is closed; Sim pause does not stop it. Any save (autosave or
    /// manual) restarts the count. A short "Saved" note is shown at the bottom of the screen after each autosave (SV-12).
    /// Manual saves are never touched: they go to new files through <see cref="SavePanelController"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AutosaveController : MonoBehaviour
    {
        /// <summary>Length of the "Saved" note on screen, in real seconds.</summary>
        public const float NoticeSeconds = 2f;

        [SerializeField] private SimHost _host;
        [SerializeField] private float _intervalMinutes = 30f;

        private AutosaveTimer _timer;
        private SavePanelController _savePanel;
        private Label _note;
        private float _noticeLeft;

        /// <summary>Store used for writing. Defaults to <c>persistentDataPath/Saves</c>; tests replace it.</summary>
        public SaveStore Store { get; set; }

        /// <summary>Supplies the real creation time as Unix seconds. Tests replace it.</summary>
        public Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>The interval in real minutes (serialized, default 30).</summary>
        public float IntervalMinutes => _intervalMinutes;

        /// <summary>The countdown. Null until the component is enabled.</summary>
        public AutosaveTimer Timer => _timer;

        /// <summary>Path of the last autosave file written, or null.</summary>
        public string LastAutosavePath { get; private set; }

        /// <summary>The "Saved" note label (null before the component is enabled).</summary>
        public Label NoteLabel => _note;

        /// <summary>Sets the SimHost (for code that adds the component at run time). Call before enabling.</summary>
        public void SetHost(SimHost host) { _host = host; }

        /// <summary>Changes the interval and restarts the count. Minutes must be positive.</summary>
        public void SetIntervalMinutes(float minutes)
        {
            _intervalMinutes = minutes;
            _timer = new AutosaveTimer(minutes * 60.0);
        }

        private void OnEnable()
        {
            if (_timer == null) SetIntervalMinutes(_intervalMinutes);
            BuildNote();
            SubscribeToManualSaves();
        }

        private void OnDisable()
        {
            if (_savePanel != null) _savePanel.Saved -= OnManualSaved;
            _savePanel = null;
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
            if (_noticeLeft > 0 && _note != null)
            {
                _noticeLeft -= Time.unscaledDeltaTime;
                if (_noticeLeft <= 0) _note.style.display = DisplayStyle.None;
            }
            if (_savePanel == null) SubscribeToManualSaves();
        }

        /// <summary>
        /// Adds real seconds to the countdown and autosaves when it is due. Does nothing while no world is loaded or the
        /// menu is open (SV-11). Update calls this with the unscaled frame time.
        /// </summary>
        public void Advance(float realSeconds)
        {
            if (_timer == null) return;
            var host = Host();
            if (host == null || !host.IsInitialized || GameMenuState.IsOpen) return;
            _timer.Advance(realSeconds);
            if (_timer.IsDue) AutosaveNow();
        }

        /// <summary>Writes the autosave slot now (overwriting it) and restarts the count. Returns true on success.</summary>
        public bool AutosaveNow()
        {
            var host = Host();
            if (host == null || !host.IsInitialized || host.Map == null) return false;
            if (_timer == null) SetIntervalMinutes(_intervalMinutes);
            try
            {
                byte[] bytes = SimSave.Write(host.World, SaveStore.AutosaveStem, Clock(), host.Map.MapId, host.Map.ContentHash);
                LastAutosavePath = (Store ?? (Store = SaveStore.Default())).WriteAutosave(bytes);
                ShowNote("Saved");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Autosave failed: " + e);
                return false;
            }
            finally
            {
                // Restart even on failure, so a broken disk does not retry every frame.
                _timer.Restart();
            }
        }

        /// <summary>Restarts the count after a manual save (SV-11). Called by the Save panel's Saved event.</summary>
        public void OnManualSaved()
        {
            _timer?.Restart();
        }

        private void SubscribeToManualSaves()
        {
            if (_savePanel != null) return;
            _savePanel = FindFirstObjectByType<SavePanelController>();
            if (_savePanel != null) _savePanel.Saved += OnManualSaved;
        }

        private SimHost Host() => _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());

        private void ShowNote(string text)
        {
            if (_note == null) return;
            _note.text = text;
            _note.style.display = DisplayStyle.Flex;
            _noticeLeft = NoticeSeconds;
        }

        private void BuildNote()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            if (_note != null && _note.panel == root.panel) return;
            // The note is presentation only: it must never block clicks on the map or the menu.
            root.pickingMode = PickingMode.Ignore;
            _note = new Label("Saved") { name = "autosave-note", pickingMode = PickingMode.Ignore };
            _note.style.position = Position.Absolute;
            _note.style.left = 0;
            _note.style.right = 0;
            _note.style.bottom = 16;
            _note.style.fontSize = 14;
            _note.style.unityTextAlign = TextAnchor.MiddleCenter;
            _note.style.color = new Color(150f / 255f, 230f / 255f, 150f / 255f);
            _note.style.display = DisplayStyle.None;
            root.Add(_note);
        }
    }
}
