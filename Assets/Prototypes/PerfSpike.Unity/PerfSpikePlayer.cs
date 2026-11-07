using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace LordsLegacy.Prototypes.PerfSpike.Unity
{
    /// <summary>
    /// THROWAWAY (T-013). Lives in the benchmark-only scene. On start it runs <see cref="BenchmarkRunner"/> on a
    /// worker thread (no input needed), writes perf-results.md next to the exe, then quits the player.
    /// </summary>
    public sealed class PerfSpikePlayer : MonoBehaviour
    {
        private Thread _worker;
        private volatile bool _done;
        private volatile string _status = "starting";
        private string _report;
        private string _environment;
        private string _outputPath;
        private bool _finished;

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            _environment = "Unity " + Application.unityVersion + ", "
                + (Application.isEditor ? "Editor" : "Player") + ", " + SystemInfo.operatingSystem;
            _outputPath = ResolveOutputPath();

            // Captured here: SystemInfo may only be read on the main thread.
            string hardware = "CPU: " + SystemInfo.processorType + " (" + SystemInfo.processorCount
                + " logical)\nRAM: " + SystemInfo.systemMemorySize + " MB";
            string env = _environment;

            _worker = new Thread(() =>
            {
                try
                {
                    string body = BenchmarkRunner.Run(env, line => _status = line);
                    _report = "# PerfSpike results\n\n```\n" + hardware + "\n\n" + body + "\n```\n";
                }
                catch (Exception ex)
                {
                    _report = "# PerfSpike FAILED\n\n```\n" + hardware + "\n\n" + ex + "\n```\n";
                }
                _done = true;
            });
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void Update()
        {
            if (!_done || _finished) return;
            _finished = true;
            try { File.WriteAllText(_outputPath, _report); }
            catch (Exception ex) { Debug.LogError("[PerfSpike] could not write results: " + ex); }
            Debug.Log("[PerfSpike] wrote " + _outputPath);
            Quit();
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(20, 20, 900, 60), "PerfSpike benchmark running (window closes when done): " + _status);
        }

        private static string ResolveOutputPath()
        {
            // Player: folder containing the exe. Editor: project root.
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(dir, "perf-results.md");
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
