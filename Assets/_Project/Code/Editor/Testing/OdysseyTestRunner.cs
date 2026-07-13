using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Odyssey.Editor.Testing
{
    /// <summary>
    /// 为菜单和 Unity batchmode 提供统一的 EditMode 测试入口，并把结果写入可由外部脚本读取的文件。
    /// 采用 Adapter 模式封装 TestRunnerApi 生命周期，避免构建脚本依赖 Unity 测试回调细节。
    /// </summary>
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

        [MenuItem("Odyssey/测试/运行 EditMode 测试")]
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
                    Debug.Log("Odyssey EditMode 测试：" + summary);
                }
                else
                {
                    Debug.LogError("Odyssey EditMode 测试：" + summary + "\n" + result.Message);
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
