using System.IO;
using System.Linq;
using Odyssey.Characters.Player;
using Odyssey.Networking;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Editor.Networking
{
    /// <summary>
    /// 自动生成独立 NetworkArena 场景、网络玩家 Prefab 与本机演示构建，保证仓库克隆后无需手工拖拽即可复现。
    /// 采用 Builder 模式集中处理一次性的编辑器装配；运行时仍保持普通显式引用，不引入反射容器或全局 Manager。
    /// 该工具只覆盖联机技术切片的固定结构，不扩展为通用关卡编辑器，避免作品集范围内的过度设计。
    /// </summary>
    public static class NetworkArenaBuilder
    {
        public const string ScenePath = "Assets/_Project/Content/Scenes/NetworkArena.unity";
        public const string PlayerPrefabPath = "Assets/_Project/Content/Prefabs/Network/NetworkPlayer.prefab";
        private const string EllenPrefabPath = "Assets/_Project/Content/Prefabs/Characters/Ellen.prefab";
        private const string PlayerAnimatorPath = "Assets/_Project/Content/Animations/Player/PlayerAnimator.controller";
        private const string MaterialFolder = "Assets/_Project/Content/Materials/Network";
        private const string BuildOutputPath = "Builds/NetworkArena/OdysseyNetworkArena.exe";

        [MenuItem("Odyssey/联机/重新搭建本地联机场景")]
        public static void BuildArena()
        {
            EnsureFolders();
            var playerPrefab = BuildNetworkPlayerPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            BuildCameraAndLight();
            BuildNetworkRuntime(playerPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("本地联机场景已完成搭建：NetworkArena。可直接点击 Play 后选择创建 Host 或加入 Client。");
        }

        [MenuItem("Odyssey/联机/构建 Windows 双开演示")]
        public static void BuildWindowsDemo()
        {
            BuildArena();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildOutputPath) ?? "Builds/NetworkArena");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"联机演示构建失败：{report.summary.result}");
            }

            Debug.Log($"联机演示构建完成：{Path.GetFullPath(BuildOutputPath)}");
            EditorUtility.RevealInFinder(BuildOutputPath);
        }

        private static GameObject BuildNetworkPlayerPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(EllenPrefabPath);
            if (source == null)
            {
                throw new FileNotFoundException("未找到 Ellen Prefab，无法创建联机玩家。", EllenPrefabPath);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "联机玩家";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var footPlacement = instance.GetComponent<PlayerFootPlacementController>();
            if (footPlacement != null)
            {
                Object.DestroyImmediate(footPlacement);
            }

            foreach (var component in instance.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.GetType().Name == "RigBuilder")
                {
                    Object.DestroyImmediate(component);
                }
            }

            var footRig = instance.transform.Find("表现层脚部Rig");
            if (footRig != null)
            {
                footRig.gameObject.SetActive(false);
            }

            instance.AddComponent<NetworkObject>();
            instance.AddComponent<NetworkTransform>();
            instance.AddComponent<NetworkPlayerAvatar>();
            var animator = instance.GetComponent<Animator>();
            if (animator != null)
            {
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
                if (controller == null)
                {
                    throw new FileNotFoundException("未找到项目自有玩家 Animator Controller。", PlayerAnimatorPath);
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PlayerPrefabPath);
            Object.DestroyImmediate(instance);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("联机玩家 Prefab 保存失败。");
            }

            return prefab;
        }

        private static void BuildEnvironment()
        {
            var floorMaterial = GetOrCreateMaterial("竞技场地面.mat", new Color(0.12f, 0.17f, 0.22f));
            var wallMaterial = GetOrCreateMaterial("竞技场边界.mat", new Color(0.08f, 0.35f, 0.48f));
            var padBlue = GetOrCreateMaterial("出生点蓝.mat", new Color(0.1f, 0.6f, 1f));
            var padOrange = GetOrCreateMaterial("出生点橙.mat", new Color(1f, 0.35f, 0.08f));

            CreatePrimitive("竞技场地面", PrimitiveType.Cube, new Vector3(0f, -0.3f, 0f), new Vector3(24f, 0.6f, 20f), floorMaterial);
            CreatePrimitive("北侧边界", PrimitiveType.Cube, new Vector3(0f, 1.5f, 10f), new Vector3(25f, 3f, 0.5f), wallMaterial);
            CreatePrimitive("南侧边界", PrimitiveType.Cube, new Vector3(0f, 1.5f, -10f), new Vector3(25f, 3f, 0.5f), wallMaterial);
            CreatePrimitive("西侧边界", PrimitiveType.Cube, new Vector3(-12f, 1.5f, 0f), new Vector3(0.5f, 3f, 20f), wallMaterial);
            CreatePrimitive("东侧边界", PrimitiveType.Cube, new Vector3(12f, 1.5f, 0f), new Vector3(0.5f, 3f, 20f), wallMaterial);

            var leftPad = CreatePrimitive("Host 出生点", PrimitiveType.Cylinder, new Vector3(-4f, 0.02f, 0f), new Vector3(1.7f, 0.05f, 1.7f), padBlue);
            var rightPad = CreatePrimitive("Client 出生点", PrimitiveType.Cylinder, new Vector3(4f, 0.02f, 0f), new Vector3(1.7f, 0.05f, 1.7f), padOrange);
            Object.DestroyImmediate(leftPad.GetComponent<Collider>());
            Object.DestroyImmediate(rightPad.GetComponent<Collider>());

            CreatePrimitive("中央掩体", PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(1.5f, 2f, 4f), wallMaterial);
        }

        private static void BuildCameraAndLight()
        {
            var cameraObject = new GameObject("主摄像机", typeof(Camera), typeof(AudioListener), typeof(NetworkArenaCameraFollow));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 9f, -10f), Quaternion.Euler(34f, 0f, 0f));
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;

            var lightObject = new GameObject("主方向光", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.48f);
        }

        private static void BuildNetworkRuntime(GameObject playerPrefab)
        {
            var runtime = new GameObject(
                "联机运行时",
                typeof(NetworkManager),
                typeof(UnityTransport),
                typeof(NetworkSessionController),
                typeof(NetworkArenaHud));
            var manager = runtime.GetComponent<NetworkManager>();
            var transport = runtime.GetComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab,
                TickRate = 30,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                ForceSamePrefabs = true,
                EnableNetworkLogs = true
            };
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var result = GameObject.CreatePrimitive(primitiveType);
            result.name = name;
            result.transform.SetPositionAndRotation(position, Quaternion.identity);
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static Material GetOrCreateMaterial(string fileName, Color color)
        {
            var path = $"{MaterialFolder}/{fileName}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null)
                {
                    throw new System.InvalidOperationException("未找到 Standard Shader，无法创建联机场景材质。");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Code/Editor", "Networking");
            EnsureFolder("Assets/_Project/Content/Prefabs", "Network");
            EnsureFolder("Assets/_Project/Content", "Materials");
            EnsureFolder("Assets/_Project/Content/Materials", "Network");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void EnsureBuildSettings()
        {
            var paths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => path != ScenePath)
                .ToList();
            paths.Add(ScenePath);
            EditorBuildSettings.scenes = paths
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }
    }
}
