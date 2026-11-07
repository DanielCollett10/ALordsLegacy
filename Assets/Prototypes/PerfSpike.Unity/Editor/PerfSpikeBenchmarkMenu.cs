using System;
using System.IO;
using LordsLegacy.Prototypes.PerfSpike.Unity;
using UnityEditor;
using UnityEngine;

namespace LordsLegacy.Prototypes.PerfSpike.UnityEditorMenu
{
    /// <summary>
    /// THROWAWAY (T-012). Menu entry that runs <see cref="BenchmarkRunner"/> inside the open Editor (Mono) and logs
    /// the report. A copy is also written to Logs/perf-editor-mono.txt (Logs/ is not committed).
    /// </summary>
    public static class PerfSpikeBenchmarkMenu
    {
        /// <summary>Runs the standard benchmark and logs the report.</summary>
        [MenuItem("LordsLegacy/Perf/Run benchmark in Editor")]
        public static void RunBenchmarkInEditor()
        {
            string environment = "Unity " + Application.unityVersion + ", Mono " + Environment.Version
                + " (Editor), " + SystemInfo.operatingSystem + ", " + SystemInfo.processorType
                + " x" + SystemInfo.processorCount;

            string report = BenchmarkRunner.Run(environment, line => Debug.Log("[PerfSpike] " + line));
            Debug.Log("[PerfSpike] Report:\n" + report);

            string logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "perf-editor-mono.txt"), report);
        }
    }
}
