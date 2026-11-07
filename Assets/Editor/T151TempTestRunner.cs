using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// TEMPORARY for T-151: runs tests and writes Temp/t151_results.txt. Delete before finishing.
public static class T151TempTestRunner
{
    private const string Out = "Temp/t151_results.txt";
    private static TestRunnerApi _api;
    private static Callbacks _cb;

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => File.WriteAllText(Out, "STARTED\n");
        public void RunFinished(ITestResultAdaptor result) =>
            File.AppendAllText(Out, "TOTALS passed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount + "\n");
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;
            File.AppendAllText(Out, result.TestStatus + " " + result.Test.FullName +
                (result.TestStatus == TestStatus.Failed ? " :: " + result.Message : "") + "\n");
        }
    }

    [MenuItem("Tools/T151 Run Road+Area PlayMode")]
    public static void RunRoadArea() => Run(TestMode.PlayMode, new[] { "LordsLegacy.Tests.PlayMode.RoadToolTests", "LordsLegacy.Tests.PlayMode.AreaToolTests" });

    [MenuItem("Tools/T151 Run All PlayMode")]
    public static void RunAllPlay() => Run(TestMode.PlayMode, null);

    [MenuItem("Tools/T151 Run All EditMode")]
    public static void RunAllEdit() => Run(TestMode.EditMode, null);

    private static void Run(TestMode mode, string[] names)
    {
        _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _cb = new Callbacks();
        _api.RegisterCallbacks(_cb);
        var f = new Filter { testMode = mode };
        if (names != null) f.testNames = names;
        _api.Execute(new ExecutionSettings(f));
    }
}
