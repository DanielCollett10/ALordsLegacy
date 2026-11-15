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
        private readonly List<Button> _bins = new List<Button>();

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
            _bins.Clear();
            var entries = (Store ?? (Store = SaveStore.Default())).ListAll();
            if (entries.Count == 0)
            {
                _list.Add(new Label("No saves yet.") { name = "load-empty" });
                return;
            }
            foreach (var entry in entries)
            {
                var e = entry;
                string real = DateTimeOffset.FromUnixTimeSeconds(e.CreatedRealTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
                string text = e.IsReadable
                    ? e.Name + "\n" + real + " · " + calendar.Format(e.Header.GameTick)
                    : e.Name + "\n" + real + " · " + e.UnreadableReason;
                var container = new VisualElement { name = "load-row-container" };
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Stretch;
                container.style.marginBottom = 4;
                var row = new Button(() => TryLoad(e)) { name = "load-row", text = text };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.flexGrow = 1;
                row.style.marginBottom = 0;
                row.focusable = false;
                if (!e.IsReadable)
                {
                    row.SetEnabled(false);
                    row.style.color = new Color(0.55f, 0.55f, 0.55f);
                }
                var bin = new Button(() => AskDelete(e)) { name = "load-delete", tooltip = "Delete this save" };
                bin.focusable = false;
                bin.style.width = 32;
                bin.style.marginLeft = 4;
                bin.style.marginRight = 0;
                bin.style.backgroundColor = new Color(0.25f, 0.18f, 0.18f);
                bin.generateVisualContent += DrawBin;
                container.Add(row);
                container.Add(bin);
                _list.Add(container);
                _rows.Add(row);
                _bins.Add(bin);
            }
        }

        /// <summary>The recycle-bin buttons, one per row in the same order as <see cref="Rows"/> (SV-23).</summary>
        public IReadOnlyList<Button> DeleteButtons => _bins;

        /// <summary>Asks to delete a save. Delete removes the file and refreshes the list; Cancel changes nothing.</summary>
        public void AskDelete(SaveEntry entry)
        {
            ConfirmDialog.Show("Delete \"" + entry.Name + "\"? This can't be undone.", "Delete", () =>
            {
                var store = Store ?? (Store = SaveStore.Default());
                bool ok = store.Delete(entry.Path);
                Refresh();
                if (!ok) _note.text = "Could not delete the save.";
            });
        }

        private static void DrawBin(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            if (r.width < 4 || r.height < 4) return;
            var p = ctx.painter2D;
            float s = Mathf.Min(r.width, r.height) * 0.5f;
            var c = r.center;
            p.fillColor = new Color(0.92f, 0.78f, 0.75f);
            p.strokeColor = p.fillColor;
            p.lineWidth = 2;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - s * 0.7f, c.y - s * 0.55f));
            p.LineTo(new Vector2(c.x + s * 0.7f, c.y - s * 0.55f));
            p.LineTo(new Vector2(c.x + s * 0.55f, c.y + s));
            p.LineTo(new Vector2(c.x - s * 0.55f, c.y + s));
            p.ClosePath();
            p.Fill();
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - s, c.y - s * 0.8f));
            p.LineTo(new Vector2(c.x + s, c.y - s * 0.8f));
            p.MoveTo(new Vector2(c.x - s * 0.3f, c.y - s * 0.8f));
            p.LineTo(new Vector2(c.x - s * 0.3f, c.y - s));
            p.LineTo(new Vector2(c.x + s * 0.3f, c.y - s));
            p.LineTo(new Vector2(c.x + s * 0.3f, c.y - s * 0.8f));
            p.Stroke();
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
            if (!entry.IsReadable)
            {
                _note.text = entry.UnreadableReason;
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
