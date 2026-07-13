using System;
using System.Collections.Generic;
using UnityEditor;

namespace Odyssey.Editor.Config
{
    /// <summary>
    /// 合并同一编辑器周期内的配置源文件变化，并在延迟回调中执行一次导入。
    /// 采用 Gate 模式隔离“是否需要导入”和 Unity AssetPostprocessor，避免生成资产再次触发递归导入。
    /// </summary>
    public sealed class GameConfigImportTrigger
    {
        private readonly HashSet<string> _sourcePaths;
        private bool _scheduled;

        public GameConfigImportTrigger(params string[] sourcePaths)
        {
            _sourcePaths = new HashSet<string>(sourcePaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public bool TrySchedule(
            IEnumerable<string> changedPaths,
            Action<Action> enqueue,
            Action import)
        {
            if (_scheduled || changedPaths == null || enqueue == null || import == null)
            {
                return false;
            }

            var sourceChanged = false;
            foreach (var path in changedPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && _sourcePaths.Contains(path.Replace('\\', '/')))
                {
                    sourceChanged = true;
                    break;
                }
            }

            if (!sourceChanged)
            {
                return false;
            }

            _scheduled = true;
            enqueue(() =>
            {
                try
                {
                    import();
                }
                finally
                {
                    _scheduled = false;
                }
            });
            return true;
        }
    }

    /// <summary>
    /// 监听 Unity 资产数据库中的配置 CSV 变化，并把实际导入延迟到当前导入周期结束后。
    /// 该适配器只关注设计源文件，生成的 ScriptableObject 不会再次触发自身。
    /// </summary>
    public sealed class GameConfigAutoImporter : AssetPostprocessor
    {
        private static readonly GameConfigImportTrigger Trigger = new GameConfigImportTrigger(
            GameConfigImporter.PlayerCsvPath,
            GameConfigImporter.EnemyCsvPath);

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var changedPaths = new List<string>();
            AddRange(changedPaths, importedAssets);
            AddRange(changedPaths, deletedAssets);
            AddRange(changedPaths, movedAssets);
            AddRange(changedPaths, movedFromAssetPaths);

            Trigger.TrySchedule(
                changedPaths,
                action => EditorApplication.delayCall += () => action(),
                GameConfigImporter.ImportAll);
        }

        private static void AddRange(ICollection<string> destination, IEnumerable<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var path in source)
            {
                destination.Add(path);
            }
        }
    }
}
