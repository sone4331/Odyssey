using System.IO;
using System.Linq;
using Cinemachine;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Encounters;
using Odyssey.Networking;
using Odyssey.Systems;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Odyssey.Editor.Networking
{
    /// <summary>
    /// 把现有 Level_01 原地转换为可选择单机、Host 或 Client 的合作关卡，并生成唯一的合作玩家 Prefab。
    /// 采用幂等 Builder 模式集中编辑器装配；运行时依赖仍是显式组件引用，不引入通用关卡生成框架。
    /// </summary>
    public static class CoopLevelNetworkBuilder
    {
        public const string ScenePath = "Assets/_Project/Content/Scenes/Level_01.unity";
        public const string PlayerPrefabPath = "Assets/_Project/Content/Prefabs/Network/CoopPlayer.prefab";
        public const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string ProjectilePrefabPath = "Assets/_Project/Content/Prefabs/Combat/SpitterProjectile.prefab";
        private const string RuntimeRootName = "合作联机_会话";
        private const string SpawnRootName = "合作联机_出生点";
        private const string EncounterNetworkRootName = "合作联机_战区状态";
        private const string BuildOutputPath = "Builds/CoopLevel/OdysseyCoop.exe";

        [MenuItem("Odyssey/联机/搭建原关卡合作联机")]
        public static void BuildLevel()
        {
            EnsureFolders();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sourcePlayer = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            // 重复执行时场景已没有固定玩家，因此必须先读取上次生成的出生点，再删除生成根节点。
            // 这条顺序保证“一键构建”不会在第二次运行时把合作玩家错误迁移到世界原点。
            var spawnPosition = ResolveSpawnPosition(sourcePlayer);
            RemoveGeneratedRoots();
            var playerPrefab = BuildPlayerPrefab(sourcePlayer);
            var projectilePrefab = ConfigureProjectilePrefab();
            var prefabList = BuildNetworkPrefabList(playerPrefab, projectilePrefab);

            if (sourcePlayer != null && !PrefabUtility.IsPartOfPrefabAsset(sourcePlayer))
            {
                Object.DestroyImmediate(sourcePlayer.gameObject);
            }

            var spawnPoints = BuildSpawnPoints(spawnPosition);
            ConfigureEnemies();
            var encounterAdapters = ConfigureEncounters();
            ConfigureGate();
            BuildSessionRuntime(playerPrefab, prefabList, spawnPoints);
            ClearLegacyPlayerReferences();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"原关卡合作联机搭建完成：玩家 Prefab、六只怪物、{encounterAdapters} 个战区和门禁已接入 Host 权威同步。");
        }

        [MenuItem("Odyssey/联机/构建原关卡双开演示")]
        public static void BuildWindowsDemo()
        {
            BuildLevel();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildOutputPath) ?? "Builds/CoopLevel");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"原关卡合作联机构建失败：{report.summary.result}");
            }

            Debug.Log($"原关卡合作联机构建完成：{Path.GetFullPath(BuildOutputPath)}");
            EditorUtility.RevealInFinder(BuildOutputPath);
        }

        private static GameObject BuildPlayerPrefab(PlayerController sourcePlayer)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (sourcePlayer == null)
            {
                if (existing == null)
                {
                    throw new System.InvalidOperationException("场景中没有可迁移的玩家，且合作玩家 Prefab 尚未生成。");
                }

                return existing;
            }

            var copy = Object.Instantiate(sourcePlayer.gameObject);
            copy.name = "合作玩家";
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var player = copy.GetComponent<PlayerController>();
            player.RespawnPoint = null;
            player.enabled = false;

            RemoveComponent<NetworkPlayerAdapter>(copy);
            RemoveComponent<OwnerAuthoritativeNetworkAnimator>(copy);
            RemoveComponent<OwnerAuthoritativeNetworkTransform>(copy);
            RemoveComponent<NetworkObject>(copy);

            copy.AddComponent<NetworkObject>();
            copy.AddComponent<OwnerAuthoritativeNetworkTransform>();
            var networkAnimator = copy.AddComponent<OwnerAuthoritativeNetworkAnimator>();
            networkAnimator.Animator = copy.GetComponent<Animator>();
            copy.AddComponent<NetworkPlayerAdapter>();
            var animator = copy.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PlayerPrefabPath);
            Object.DestroyImmediate(copy);
            return prefab == null
                ? throw new System.InvalidOperationException("合作玩家 Prefab 保存失败。")
                : prefab;
        }

        private static GameObject ConfigureProjectilePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectilePrefabPath);
            try
            {
                EnsureComponent<NetworkObject>(root);
                EnsureComponent<NetworkTransform>(root);
                EnsureComponent<NetworkProjectileAdapter>(root);
                PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        }

        private static NetworkPrefabsList BuildNetworkPrefabList(GameObject playerPrefab, GameObject projectilePrefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            }

            while (list.PrefabList.Count > 0)
            {
                list.Remove(list.PrefabList[0]);
            }

            list.Add(new NetworkPrefab { Prefab = playerPrefab });
            list.Add(new NetworkPrefab { Prefab = projectilePrefab });
            EditorUtility.SetDirty(list);
            return list;
        }

        private static void ConfigureEnemies()
        {
            foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var gameObject = enemy.gameObject;
                EnsureComponent<NetworkObject>(gameObject);
                EnsureComponent<NetworkTransform>(gameObject);
                var networkAnimator = EnsureComponent<NetworkAnimator>(gameObject);
                networkAnimator.Animator = gameObject.GetComponent<Animator>();
                EnsureComponent<NetworkEnemyAdapter>(gameObject);
                enemy.enabled = false;
                var agent = gameObject.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                }
            }
        }

        private static int ConfigureEncounters()
        {
            var root = new GameObject(EncounterNetworkRootName);
            var encounters = Object.FindObjectsByType<CombatEncounterController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var encounter in encounters)
            {
                encounter.enabled = false;
                var stateObject = new GameObject($"{encounter.DisplayName}_网络状态");
                stateObject.transform.SetParent(root.transform, false);
                stateObject.AddComponent<NetworkObject>();
                var adapter = stateObject.AddComponent<NetworkEncounterAdapter>();
                SetObjectReference(adapter, "encounter", encounter);
            }

            return encounters.Length;
        }

        private static void ConfigureGate()
        {
            var gate = Object.FindFirstObjectByType<EncounterClearanceGate>(FindObjectsInactive.Include);
            var pressure = Object.FindFirstObjectByType<EncounterClearancePressurePlate>(FindObjectsInactive.Include);
            if (gate == null || pressure == null)
            {
                throw new System.InvalidOperationException("原关卡缺少清怪隔离门或踏板，无法配置权威门禁。");
            }

            EnsureComponent<NetworkObject>(gate.gameObject);
            var adapter = EnsureComponent<NetworkGateAdapter>(gate.gameObject);
            SetObjectReference(adapter, "gate", gate);
            SetObjectReference(adapter, "pressurePlate", pressure);
            pressure.enabled = false;
        }

        private static void BuildSessionRuntime(
            GameObject playerPrefab,
            NetworkPrefabsList prefabList,
            Transform[] spawnPoints)
        {
            var runtime = new GameObject(RuntimeRootName);
            var manager = runtime.AddComponent<NetworkManager>();
            var transport = runtime.AddComponent<UnityTransport>();
            runtime.AddComponent<SinglePlayerTransport>();
            var binder = runtime.AddComponent<GameplayLocalViewBinder>();
            var session = runtime.AddComponent<GameplaySessionController>();
            var saveManager = runtime.AddComponent<SaveManager>();

            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab,
                TickRate = 30,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                ForceSamePrefabs = true,
                EnableNetworkLogs = true
            };
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            SetObjectArray(session, "spawnPoints", spawnPoints.Cast<Object>().ToArray());
            SetObjectReference(session, "localViewBinder", binder);
            SetObjectReference(binder, "healthUi", Object.FindFirstObjectByType<PlayerHealthUI>(FindObjectsInactive.Include));
            SetObjectReference(binder, "saveManager", saveManager);
            var freeLook = Object.FindFirstObjectByType<CinemachineFreeLook>(FindObjectsInactive.Include);
            SetObjectReference(binder, "freeLookCamera", freeLook);
            SetObjectReference(
                binder,
                "cameraInputProvider",
                freeLook == null ? null : freeLook.GetComponent<CinemachineInputProvider>());
            SetObjectArray(
                binder,
                "impactFeedbacks",
                Object.FindObjectsByType<CombatImpactFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Cast<Object>()
                    .ToArray());
            GameMenuSceneBuilder.Build(runtime, session, binder, saveManager);
        }

        private static Transform[] BuildSpawnPoints(Vector3 origin)
        {
            var root = new GameObject(SpawnRootName);
            var host = new GameObject("Host 出生点").transform;
            var client = new GameObject("Client 出生点").transform;
            host.SetParent(root.transform, false);
            client.SetParent(root.transform, false);
            host.position = origin + Vector3.left * 0.8f;
            client.position = origin + Vector3.right * 0.8f;
            return new[] { host, client };
        }

        private static Vector3 ResolveSpawnPosition(PlayerController player)
        {
            if (player == null)
            {
                var existing = GameObject.Find(SpawnRootName);
                if (existing == null)
                {
                    return Vector3.zero;
                }

                var points = existing.GetComponentsInChildren<Transform>(true)
                    .Where(transform => transform != existing.transform)
                    .ToArray();
                return points.Length == 0
                    ? existing.transform.position
                    : points.Aggregate(Vector3.zero, (sum, point) => sum + point.position) / points.Length;
            }

            return player.RespawnPoint == null ? player.transform.position : player.RespawnPoint.position;
        }

        private static void ClearLegacyPlayerReferences()
        {
            var healthUi = Object.FindFirstObjectByType<PlayerHealthUI>(FindObjectsInactive.Include);
            if (healthUi != null)
            {
                healthUi.Player = null;
                EditorUtility.SetDirty(healthUi);
            }

            foreach (var camera in Object.FindObjectsByType<CinemachineVirtualCamera>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (camera.Follow == null || camera.Follow.GetComponentInParent<PlayerController>() != null)
                {
                    camera.Follow = null;
                    camera.LookAt = null;
                    EditorUtility.SetDirty(camera);
                }
            }

            foreach (var camera in Object.FindObjectsByType<CinemachineFreeLook>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (camera.Follow == null || camera.Follow.GetComponentInParent<PlayerController>() != null)
                {
                    camera.Follow = null;
                    camera.LookAt = null;
                    EditorUtility.SetDirty(camera);
                }
            }
        }

        private static void RemoveGeneratedRoots()
        {
            foreach (var name in new[] { RuntimeRootName, SpawnRootName, EncounterNetworkRootName })
            {
                var existing = GameObject.Find(name);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static void RemoveComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string fieldName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Content/Prefabs", "Network");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
