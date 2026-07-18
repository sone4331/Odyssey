using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Odyssey.Editor.Testing
{
    /// <summary>
    /// 为菜单和 Unity batchmode 提供统一的 EditMode/PlayMode 测试入口，并把结果写入可由外部脚本读取的文件。
    /// 采用 Adapter 模式封装 TestRunnerApi 生命周期，避免构建脚本依赖 Unity 测试回调细节。
    /// </summary>
    [InitializeOnLoad]
    public static class OdysseyTestRunner
    {
        private const string RequestPath = "Temp/RunOdysseyEditModeTests.request";
        private const string ResultPath = "Temp/OdysseyEditModeTestResult.txt";
        private const string PlayModeResultPath = "Temp/OdysseyPlayModeTestResult.txt";
        private const string PlayModeRunningPath = "Temp/OdysseyPlayModeTests.running";

        private static TestRunnerApi _api;
        private static ResultCallbacks _callbacks;

        static OdysseyTestRunner()
        {
            if (!File.Exists(RequestPath))
            {
                if (File.Exists(PlayModeRunningPath))
                {
                    EditorApplication.delayCall += RegisterPlayModeResultCallback;
                }

                return;
            }

            File.Delete(RequestPath);
            EditorApplication.delayCall += RunEditModeTests;
        }

        [MenuItem("Odyssey/测试/运行 EditMode 测试")]
        public static void RunEditModeTests()
        {
            RunTests(
                TestMode.EditMode,
                "Odyssey.EditModeTests",
                ResultPath,
                "EditMode",
                runSynchronously: true);
        }

        /// <summary>
        /// 运行真实场景生命周期测试；异步执行是 Unity PlayMode Test Runner 的要求，结果仍写入固定文件供外部验收。
        /// </summary>
        [MenuItem("Odyssey/测试/运行 PlayMode 测试")]
        public static void RunPlayModeTests()
        {
            File.WriteAllText(PlayModeRunningPath, "正在运行");
            RunTests(
                TestMode.PlayMode,
                "Odyssey.PlayModeTests",
                PlayModeResultPath,
                "PlayMode",
                runSynchronously: false);
        }

        private static void RegisterPlayModeResultCallback()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new ResultCallbacks(PlayModeResultPath, "PlayMode", PlayModeRunningPath);
            _api.RegisterCallbacks(_callbacks);
        }

        private static void RunTests(
            TestMode mode,
            string assemblyName,
            string resultPath,
            string displayName,
            bool runSynchronously)
        {
            File.Delete(resultPath);
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new ResultCallbacks(
                resultPath,
                displayName,
                mode == TestMode.PlayMode ? PlayModeRunningPath : null);
            _api.RegisterCallbacks(_callbacks);

            var settings = new ExecutionSettings(new Filter
            {
                testMode = mode,
                assemblyNames = new[] { assemblyName }
            })
            {
                runSynchronously = runSynchronously
            };

            _api.Execute(settings);
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly string _resultPath;
            private readonly string _displayName;
            private readonly List<string> _failures = new List<string>();
            private readonly string _runningMarkerPath;

            public ResultCallbacks(string resultPath, string displayName, string runningMarkerPath = null)
            {
                _resultPath = resultPath;
                _displayName = displayName;
                _runningMarkerPath = runningMarkerPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var summary = $"Passed={result.PassCount};Failed={result.FailCount};Skipped={result.SkipCount};State={result.ResultState}";
                var details = _failures.Count == 0
                    ? summary
                    : summary + "\n" + string.Join("\n\n", _failures);
                File.WriteAllText(_resultPath, details);
                if (!string.IsNullOrEmpty(_runningMarkerPath))
                {
                    File.Delete(_runningMarkerPath);
                }
                if (result.FailCount == 0)
                {
                    Debug.Log($"Odyssey {_displayName} 测试：" + summary);
                }
                else
                {
                    Debug.LogError($"Odyssey {_displayName} 测试：" + details);
                }

                ReleaseCallbacks();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.FailCount <= 0)
                {
                    return;
                }

                _failures.Add(
                    $"失败用例：{result.FullName}\n{result.Message}\n{result.StackTrace}".Trim());
            }
        }

        /// <summary>
        /// 每次测试完成后注销回调并销毁 API 实例，避免 PlayMode 跨域回调继续监听下一次 EditMode 运行并覆盖结果文件。
        /// </summary>
        private static void ReleaseCallbacks()
        {
            if (_api != null && _callbacks != null)
            {
                _api.UnregisterCallbacks(_callbacks);
            }

            if (_api != null)
            {
                Object.DestroyImmediate(_api);
            }

            _api = null;
            _callbacks = null;
        }
    }
}
