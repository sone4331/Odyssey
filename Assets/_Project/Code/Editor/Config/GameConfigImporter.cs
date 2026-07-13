using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Odyssey.Gameplay.Config;
using Odyssey.Unity.Config;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Editor.Config
{
    /// <summary>
    /// 编排 CSV 读取、类型转换、领域校验和 ScriptableObject 生成，是设计数据进入运行时的唯一 Editor 入口。
    /// 采用 Pipeline 与 Anti-Corruption Layer，阻止文本格式和无效数据渗透到 Gameplay 或构建产物。
    /// </summary>
    public static class GameConfigImporter
    {
        public const string PlayerCsvPath = "Assets/_Project/Data/Design/Player.csv";
        public const string EnemyCsvPath = "Assets/_Project/Data/Design/Enemy.csv";
        public const string OutputDirectory = "Assets/_Project/Data/Runtime/Resources/Config";
        public const string OutputAssetPath = OutputDirectory + "/GameConfigDatabase.asset";

        /// <summary>
        /// 按“全部解析、全部校验、最后写资产”的事务顺序导入配置。
        /// 任一表失败时不会覆盖现有有效资产，确保运行时数据库始终来自完整一致的数据集。
        /// </summary>
        [MenuItem("Odyssey/配置/导入并校验全部 CSV")]
        public static void ImportAll()
        {
            var players = ReadPlayers(PlayerCsvPath);
            var enemies = ReadEnemies(EnemyCsvPath);
            Validate(players, enemies);

            EnsureFolders(OutputDirectory);
            var asset = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(OutputAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GameConfigAsset>();
                AssetDatabase.CreateAsset(asset, OutputAssetPath);
            }

            asset.Replace(players, enemies);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"已将 {players.Count} 条玩家配置和 {enemies.Count} 条敌人配置导入到 {OutputAssetPath}。");
        }

        private static List<PlayerConfigEntry> ReadPlayers(string path)
        {
            var table = ReadTable(path);
            var entries = new List<PlayerConfigEntry>(table.Rows.Count);
            foreach (var row in table.Rows)
            {
                entries.Add(new PlayerConfigEntry
                {
                    id = row["id"],
                    walkSpeed = ParseFloat(row["walkSpeed"], path, "walkSpeed"),
                    runSpeed = ParseFloat(row["runSpeed"], path, "runSpeed"),
                    gravity = ParseFloat(row["gravity"], path, "gravity"),
                    dashForce = ParseFloat(row["dashForce"], path, "dashForce"),
                    dashDuration = ParseFloat(row["dashDuration"], path, "dashDuration"),
                    dashCooldown = ParseFloat(row["dashCooldown"], path, "dashCooldown"),
                    jumpHeight = ParseFloat(row["jumpHeight"], path, "jumpHeight"),
                    chargeJumpHeight = ParseFloat(row["chargeJumpHeight"], path, "chargeJumpHeight"),
                    minChargeTime = ParseFloat(row["minChargeTime"], path, "minChargeTime"),
                    airJumpHeight = ParseFloat(row["airJumpHeight"], path, "airJumpHeight"),
                    wallSlideSpeed = ParseFloat(row["wallSlideSpeed"], path, "wallSlideSpeed"),
                    wallJumpUpForce = ParseFloat(row["wallJumpUpForce"], path, "wallJumpUpForce"),
                    wallJumpSideForce = ParseFloat(row["wallJumpSideForce"], path, "wallJumpSideForce"),
                    attackDamage = ParseInt(row["attackDamage"], path, "attackDamage"),
                    attackRange = ParseFloat(row["attackRange"], path, "attackRange"),
                    attackCooldown = ParseFloat(row["attackCooldown"], path, "attackCooldown"),
                    maxHealth = ParseInt(row["maxHealth"], path, "maxHealth")
                });
            }

            return entries;
        }

        private static List<EnemyConfigEntry> ReadEnemies(string path)
        {
            var table = ReadTable(path);
            var entries = new List<EnemyConfigEntry>(table.Rows.Count);
            foreach (var row in table.Rows)
            {
                entries.Add(new EnemyConfigEntry
                {
                    id = row["id"],
                    chaseRange = ParseFloat(row["chaseRange"], path, "chaseRange"),
                    attackRange = ParseFloat(row["attackRange"], path, "attackRange")
                });
            }

            return entries;
        }

        private static CsvTable ReadTable(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                throw new FileNotFoundException("未找到配置源文件。", assetPath);
            }

            return CsvTableParser.Parse(File.ReadAllText(assetPath));
        }

        private static void Validate(IEnumerable<PlayerConfigEntry> players, IEnumerable<EnemyConfigEntry> enemies)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var errors = new List<string>();

            foreach (var entry in players)
            {
                AppendErrors(entry.id, GameConfigValidator.Validate(entry.ToData()), ids, errors);
            }

            foreach (var entry in enemies)
            {
                AppendErrors(entry.id, GameConfigValidator.Validate(entry.ToData()), ids, errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidDataException("游戏配置导入失败：\n" + string.Join("\n", errors));
            }
        }

        private static void AppendErrors(
            string id,
            ConfigValidationResult validation,
            ISet<string> ids,
            ICollection<string> errors)
        {
            if (!ids.Add(id))
            {
                errors.Add($"全局配置 ID“{id}”重复。");
            }

            foreach (var error in validation.Errors)
            {
                errors.Add($"配置“{id}”：{error}");
            }
        }

        private static float ParseFloat(string value, string path, string column)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                throw new FormatException($"{path}：列“{column}”包含无效浮点数“{value}”。");
            }

            return result;
        }

        private static int ParseInt(string value, string path, string column)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                throw new FormatException($"{path}：列“{column}”包含无效整数“{value}”。");
            }

            return result;
        }

        private static void EnsureFolders(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
