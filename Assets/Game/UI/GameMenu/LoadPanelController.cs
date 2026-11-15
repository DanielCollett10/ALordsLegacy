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
                string real = DateTimeOffset.FromUnixTimeSeconds(e.SortTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
                string text = e.IsReadable
                    ? e.Name + "\n" + real + " · " + calendar.Format(e.Header.GameTick)
                    : e.Name + "\n" + e.UnreadableReason;
                var row = new Button(() => TryLoad(e)) { name = "load-row", text = text };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.whiteSpace = WhiteSpace.Normal;
                row.focusable = false;
                row.style.flexGrow = 1;
                row.style.marginBottom = 0;
                if (!e.IsReadable) StyleUnreadable(row);

                var container = new VisualElement { name = "load-row-container" };
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Stretch;
                container.style.marginBottom = 4;
                container.Add(row);
                container.Add(MakeBinButton(e));
                _list.Add(container);
                _rows.Add(row);
            }
        }

        // SV-34: a save this build can't read stays in the list greyed out, with Load disabled. Its bin still works.
        private static void StyleUnreadable(Button row)
        {
            row.SetEnabled(false);
            row.style.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            row.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
            row.style.opacity = 0.7f;
        }

        /// <summary>Asks to delete a save through the shared <see cref="ConfirmDialog"/> (SV-23). Confirm deletes and refreshes.</summary>
        public void RequestDelete(SaveEntry entry)
        {
            ConfirmDialog.Show("Delete \"" + entry.Name + "\"? This can't be undone.", "Delete", () =>
            {
                try { (Store ?? (Store = SaveStore.Default())).Delete(entry.Path); }
                catch (Exception ex)
                {
                    Build();
                    _note.text = "Delete failed: " + ex.Message;
                    return;
                }
                Refresh();
            });
        }

        // The bin is drawn from plain elements (lid, handle, body), so there is no texture that can be missing.
        private Button MakeBinButton(SaveEntry e)
        {
            var bin = new Button(() => RequestDelete(e)) { name = "load-delete" };
            bin.focusable = false;
            bin.tooltip = "Delete this save";
            bin.style.width = 36;
            bin.style.marginLeft = 4;
            bin.style.marginRight = 0;
            bin.style.marginTop = 0;
            bin.style.marginBottom = 0;
            bin.style.paddingLeft = bin.style.paddingRight = bin.style.paddingTop = bin.style.paddingBottom = 0;
            bin.style.backgroundColor = new Color(0.45f, 0.2f, 0.15f, 1f);
            bin.style.alignItems = Align.Center;
            bin.style.justifyContent = Justify.Center;
            var ink = new Color(0.97f, 0.95f, 0.9f, 1f);
            Func<float, float, float, float, string, VisualElement> part = (w, h, mt, r, n) =>
            {
                var v = new VisualElement { name = n, pickingMode = PickingMode.Ignore };
                v.style.width = w;
                v.style.height = h;
                v.style.marginTop = mt;
                v.style.backgroundColor = ink;
                v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = r;
                return v;
            };
            bin.Add(part(8, 3, 0, 0, "bin-handle"));
            bin.Add(part(20, 3, 1, 0, "bin-lid"));
            bin.Add(part(14, 16, 1, 3, "bin-body"));
            return bin;
        }

        /// <summary>
        /// Loads a save. Reads only the header first; a map id or hash mismatch shows the SV-30 message, sets nothing and does
        /// not reload. Returns true when the scene reload was started.
        /// </summary>
        public bool TryLoad(SaveEntry entry)
        {
            Build();
            if (!entry.IsReadable)
            {
                _note.text = entry.UnreadableReason;
                return false;
            }
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
