using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

[InitializeOnLoad]
public static class TmpTestRun
{
    static readonly string Out = "Temp/tmp-test-results.txt";
    static TmpTestRun()
    {
        var api = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Cb());
    }

    public static void Run(string mode, string name)
    {
        File.WriteAllText(Out, "RUNNING\n");
        var api = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
        var f = new Filter { testMode = mode == "play" ? TestMode.PlayMode : TestMode.EditMode };
        if (!string.IsNullOrEmpty(name)) f.testNames = new[] { name };
        api.Execute(new ExecutionSettings(f));
    }

    class Cb : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor r)
        {
            if (!r.HasChildren) File.AppendAllText(Out, r.TestStatus + " " + r.Test.FullName + "\n" + (r.TestStatus == TestStatus.Failed ? r.Message + "\n" : ""));
        }
        public void RunFinished(ITestResultAdaptor r)
        {
            File.AppendAllText(Out, "DONE pass=" + r.PassCount + " fail=" + r.FailCount + " skip=" + r.SkipCount + "\n");
        }
    }
}
