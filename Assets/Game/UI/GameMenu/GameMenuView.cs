using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Esc game menu (saving.md SV-01..04): Resume, Save, Load, Options, Quit. Opening pauses the Sim and sets
    /// <see cref="GameMenuState.IsOpen"/> so tool, camera and picking input are ignored; closing puts the pause state back
    /// as it was before. Options and Quit are shown but disabled (SV-03). Save and Load open their own panels, whose
    /// content areas (<see cref="SaveContent"/>, <see cref="LoadContent"/>) are filled by the save and load tasks.
    /// The Esc key is read by <c>ToolController</c>, which decides between tool Escape, closing and opening.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameMenuView : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private StyleSheet _styleSheet;

        private VisualElement _root, _main, _savePanel, _loadPanel;
        private Button _resume, _save, _load, _options, _quit;
        private bool _wasPaused;
        private bool _open;

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => _open;

        /// <summary>True when the main button list is showing (not the Save or Load panel).</summary>
        public bool ShowingMain => _open && Shown(_main);

        /// <summary>True when the Save panel is showing.</summary>
        public bool ShowingSave => _open && Shown(_savePanel);

        /// <summary>True when the Load panel is showing.</summary>
        public bool ShowingLoad => _open && Shown(_loadPanel);

        /// <summary>The menu root element, for layout checks.</summary>
        public VisualElement RootElement => _root;

        /// <summary>The Options button.</summary>
        public Button OptionsButton => _options;

        /// <summary>The Quit button.</summary>
        public Button QuitButton => _quit;

        /// <summary>Container the save task fills with the name field and Save button.</summary>
        public VisualElement SaveContent { get; private set; }

        /// <summary>Container the load task fills with the save list.</summary>
        public VisualElement LoadContent { get; private set; }

        /// <summary>Raised when the Save panel opens.</summary>
        public event Action SaveOpened;

        /// <summary>Raised when the Load panel opens.</summary>
        public event Action LoadOpened;

        /// <summary>Sets the SimHost (for code that adds the component at run time). Call before enabling.</summary>
        public void SetHost(SimHost host) { _host = host; }

        /// <summary>Opens the menu: remembers the pause state, pauses the Sim, blocks game input. No-op if open.</summary>
        public void Open()
        {
            if (_open) return;
            EnsureBuilt();
            var host = Host();
            _wasPaused = host != null && host.IsInitialized && host.World.Paused;
            if (host != null && host.IsInitialized) host.World.Pause();
            _open = true;
            GameMenuState.MenuOpen = true;
            ShowMain();
            _root.style.display = DisplayStyle.Flex;
        }

        /// <summary>Closes the menu and restores the pause state it had when opened. No-op if closed.</summary>
        public void Close()
        {
            if (!_open) return;
            _open = false;
            GameMenuState.MenuOpen = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            var host = Host();
            if (!_wasPaused && host != null && host.IsInitialized) host.World.Resume();
        }

        /// <summary>Esc while the menu is open: Save or Load panel go back to the main list, the main list closes the menu.</summary>
        public void HandleEscape()
        {
            if (!_open) return;
            if (ShowingMain) Close(); else ShowMain();
        }

        /// <summary>Resume button.</summary>
        public void ClickResume() => Close();

        /// <summary>Save button: shows the Save panel.</summary>
        public void ClickSave()
        {
            if (!_open) return;
            Show(_savePanel);
            SaveOpened?.Invoke();
        }

        /// <summary>Load button: shows the Load panel.</summary>
        public void ClickLoad()
        {
            if (!_open) return;
            Show(_loadPanel);
            LoadOpened?.Invoke();
        }

        /// <summary>Back button of the Save and Load panels.</summary>
        public void ClickBack()
        {
            if (_open) ShowMain();
        }

        private static bool Shown(VisualElement e) => e != null && e.style.display.value != DisplayStyle.None;

        private SimHost Host() => _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());

        private void ShowMain() => Show(_main);

        private void Show(VisualElement which)
        {
            _main.style.display = which == _main ? DisplayStyle.Flex : DisplayStyle.None;
            _savePanel.style.display = which == _savePanel ? DisplayStyle.Flex : DisplayStyle.None;
            _loadPanel.style.display = which == _loadPanel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnEnable() => EnsureBuilt();

        private void OnDisable()
        {
            if (_open) Close();
        }

        private void Update()
        {
            // The UIDocument can recreate its root (panel settings reload); rebuild if ours was dropped.
            var doc = GetComponent<UIDocument>();
            if (_root == null || _root.panel == null || (doc.rootVisualElement != null && _root.panel != doc.rootVisualElement.panel))
            {
                bool wasOpen = _open;
                Build();
                if (wasOpen && _root != null) { _root.style.display = DisplayStyle.Flex; ShowMain(); }
            }
        }

        private void EnsureBuilt()
        {
            if (_root == null || _root.panel == null) Build();
        }

        private void Build()
        {
            var doc = GetComponent<UIDocument>();
            doc.sortingOrder = 100;
            var host = doc.rootVisualElement;
            if (host == null) return;
            host.Clear();
            if (doc.visualTreeAsset != null) doc.visualTreeAsset.CloneTree(host);
            else BuildFallback(host);
            if (_styleSheet != null) host.styleSheets.Add(_styleSheet);

            _root = host.Q<VisualElement>("game-menu");
            _main = host.Q<VisualElement>("game-menu-main");
            _savePanel = host.Q<VisualElement>("game-menu-save-panel");
            _loadPanel = host.Q<VisualElement>("game-menu-load-panel");
            SaveContent = host.Q<VisualElement>("game-menu-save-content");
            LoadContent = host.Q<VisualElement>("game-menu-load-content");
            _resume = host.Q<Button>("game-menu-resume");
            _save = host.Q<Button>("game-menu-save");
            _load = host.Q<Button>("game-menu-load");
            _options = host.Q<Button>("game-menu-options");
            _quit = host.Q<Button>("game-menu-quit");
            _resume.clicked += ClickResume;
            _save.clicked += ClickSave;
            _load.clicked += ClickLoad;
            host.Q<Button>("game-menu-save-back").clicked += ClickBack;
            host.Q<Button>("game-menu-load-back").clicked += ClickBack;
            _options.SetEnabled(false);
            _quit.SetEnabled(false);

            // The overlay swallows pointer events so nothing underneath reacts while the menu is open.
            _root.pickingMode = PickingMode.Position;
            ShowMain();
            _root.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
            UiFocusRules.Apply(host);
        }

        // Used when the UIDocument has no UXML assigned (tests): same element names, plain styles.
        private static void BuildFallback(VisualElement host)
        {
            var root = new VisualElement { name = "game-menu" };
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            var main = Box("game-menu-main", "Menu");
            foreach (var pair in new[] { "resume", "save", "load", "options", "quit" })
                main.Add(new Button { name = "game-menu-" + pair, text = char.ToUpper(pair[0]) + pair.Substring(1) });
            root.Add(main);
            root.Add(SubPanel("save"));
            root.Add(SubPanel("load"));
            host.Add(root);
        }

        private static VisualElement SubPanel(string id)
        {
            var box = Box("game-menu-" + id + "-panel", char.ToUpper(id[0]) + id.Substring(1));
            box.Add(new VisualElement { name = "game-menu-" + id + "-content" });
            box.Add(new Button { name = "game-menu-" + id + "-back", text = "Back" });
            return box;
        }

        private static VisualElement Box(string name, string title)
        {
            var box = new VisualElement { name = name };
            box.style.minWidth = 260;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 16;
            box.style.backgroundColor = new Color(30f / 255f, 24f / 255f, 18f / 255f);
            box.Add(new Label(title));
            return box;
        }
    }
}
