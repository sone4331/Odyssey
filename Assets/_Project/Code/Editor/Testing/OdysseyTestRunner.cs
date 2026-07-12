using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Odyssey.Editor.Testing
{
    [InitializeOnLoad]
    public static class OdysseyTestRunner
    {
        private const string RequestPath = "Temp/RunOdysseyEditModeTests.request";
        private const string ResultPath = "Temp/OdysseyEditModeTestResult.txt";

        private static TestRunnerApi _api;
        private static ResultCallbacks _callbacks;

        static OdysseyTestRunner()
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            File.Delete(RequestPath);
            EditorApplication.delayCall += RunEditModeTests;
        }

        [MenuItem("Odyssey/Tests/Run EditMode Tests")]
        public static void RunEditModeTests()
        {
            File.Delete(ResultPath);
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new ResultCallbacks();
            _api.RegisterCallbacks(_callbacks);

            var settings = new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "Odyssey.EditModeTests" }
            })
            {
                runSynchronously = true
            };

            _api.Execute(settings);
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var summary = $"Passed={result.PassCount};Failed={result.FailCount};Skipped={result.SkipCount};State={result.ResultState}";
                File.WriteAllText(ResultPath, summary);
                if (result.FailCount == 0)
                {
                    Debug.Log("Odyssey EditMode tests: " + summary);
                }
                else
                {
                    Debug.LogError("Odyssey EditMode tests: " + summary + "\n" + result.Message);
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
