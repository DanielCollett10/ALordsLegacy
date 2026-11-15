using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Calendar;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Fills the Load panel of the game menu (SV-20..22, SV-30): saves newest first with name, real date/time and game date.
    /// Picking one reads only its header and compares map id and hash with the installed map; on a mismatch it shows the
    /// SV-30 message and changes nothing. On a match it sets <see cref="PendingLoad"/> and reloads the Game scene.
    /// </summary>
    [RequireComponent(typeof(GameMenuView))]
    public sealed class LoadPanelController : MonoBehaviour
    {
        /// <summary>Name of the scene reloaded by a load.</summary>
        public const string GameSceneName = "Game";

        /// <summary>Shown when the save was made on another map (SV-30).</summary>
        public const string MismatchMessage = "This save was made on a different map and cannot be loaded.";

        [SerializeField] private SimHost _host;

        private GameMenuView _menu;
        private VisualElement _list;
        private Label _note;
        private VisualElement _builtFor;
        private readonly List<Button> _rows = new List<Button>();

        /// <summary>Store used for reading. Defaults to <c>persistentDataPath/Saves</c>; tests replace it.</summary>
        public SaveStore Store { get; set; }

        /// <summary>Reloads the Game scene. Tests may replace it.</summary>
        public Action<string> SceneLoader { get; set; } = name => SceneManager.LoadScene(name);

        /// <summary>The save rows, newest first.</summary>
        public IReadOnlyList<Button> Rows => _rows;

        /// <summary>The note under the list (empty, or the SV-30 message).</summary>
        public Label NoteLabel => _note;

        /// <summary>Sets the SimHost (for code that adds the component at run time).</summary>
        public void SetHost(SimHost host) { _host = host; }

        private void OnEnable()
        {
            _menu = GetComponent<GameMenuView>();
            _menu.LoadOpened += Refresh;
        }

        private void OnDisable()
        {
            if (_menu != null) _menu.LoadOpened -= Refresh;
        }

        /// <summary>Rebuilds the list from the store.</summary>
        public void Refresh()
        {
            Build();
            _note.text = "";
            _list.Clear();
            _rows.Clear();
            var host = Host();
            var calendar = host != null && host.IsInitialized ? CalendarOf(host) : new GameCalendar();
            var entries = (Store ?? (Store = SaveStore.Default())).List();
            if (entries.Count == 0)
            {
                _list.Add(new Label("No saves yet.") { name = "load-empty" });
                return;
            }
            foreach (var entry in entries)
            {
                var e = entry;
                string real = DateTimeOffset.FromUnixTimeSeconds(e.Header.CreatedRealTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
                string text = e.Header.Name + "\n" + real + " · " + calendar.Format(e.Header.GameTick);
                var row = new Button(() => TryLoad(e)) { name = "load-row", text = text };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.marginBottom = 4;
                row.focusable = false;
                _list.Add(row);
                _rows.Add(row);
            }
        }

        /// <summary>
        /// Loads a save. Reads only the header first; a map id or hash mismatch shows the SV-30 message, sets nothing and does
        /// not reload. Returns true when the scene reload was started.
        /// </summary>
        public bool TryLoad(SaveEntry entry)
        {
            Build();
            var host = Host();
            if (host == null || !host.IsInitialized || host.Map == null)
            {
                _note.text = "Cannot load: the game is not running.";
                return false;
            }
            var h = entry.Header;
            if (h.MapId != host.Map.MapId || h.MapContentHash != host.Map.ContentHash)
            {
                _note.text = MismatchMessage;
                return false;
            }
            byte[] bytes;
            try { bytes = (Store ?? (Store = SaveStore.Default())).ReadAll(entry.Path); }
            catch (Exception ex)
            {
                _note.text = "Load failed: " + ex.Message;
                return false;
            }
            PendingLoad.Set(bytes);
            SceneLoader(GameSceneName);
            return true;
        }

        private static GameCalendar CalendarOf(SimHost host)
        {
            var t = host.World.Tuning;
            return new GameCalendar(t.StartMonth, t.StartDay, t.StartHour, t.StartMinute, t.StartWeekday, t.DaysPerYear);
        }

        private SimHost Host() => _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());

        private void Build()
        {
            var content = _menu.LoadContent;
            if (content == null) return;
            if (_builtFor == content && _list != null) return;
            content.Clear();
            _list = new ScrollView { name = "load-list" };
            _list.style.maxHeight = 320;
            _note = new Label("") { name = "load-note" };
            _note.style.color = new Color(240f / 255f, 150f / 255f, 130f / 255f);
            _note.style.whiteSpace = WhiteSpace.Normal;
            _note.style.marginTop = 6;
            content.Add(_list);
            content.Add(_note);
            _builtFor = content;
        }
    }
}
