using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Encounters;
using Odyssey.Editor.Config;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Odyssey.Editor.Gameplay
{
    /// <summary>
    /// 自动生成干净的 Spitter、投射物、两组遭遇和巡逻路线，是玩法切片资产装配的唯一 Editor 入口。
    /// 采用 Builder 与幂等资产管线：复用官方美术但移除教学脚本，重复执行会更新同一资产而不会堆叠对象。
    /// </summary>
    public static class SinglePlayerSliceBuilder
    {
        private const string ScenePath = "Assets/_Project/Content/Scenes/Level_01.unity";
        private const string SliceRootName = "玩法切片_战斗遭遇";
        private const string OriginalSpitterPath = "Assets/3DGamekitLite/Prefabs/Characters/Enemies/Spitter/Spitter.prefab";
        private const string OriginalProjectilePath = "Assets/3DGamekitLite/Prefabs/VFX/Characters/Enemies/Spitter/Spit.prefab";
        private const string OriginalControllerPath = "Assets/3DGamekitLite/Art/Animations/Animators/Characters/Spitter.controller";
        private const string HitEffectPath = "Assets/3DGamekitLite/Prefabs/VFX/Weapons/HitParticle.prefab";
        private const string HitAudioPath = "Assets/3DGamekitLite/Audio/Enemies/Chomper/Damaged/Chomper_Gets_Hit_Impact_01.ogg";
        private const string EvadeAudioPath = "Assets/3DGamekitLite/Audio/Ellen/Weapon/RubbleImpact/Ancient_Alien_Staff_Rubble_Impact_01.ogg";
        private const string DoorAudioPath = "Assets/3DGamekitLite/Audio/Interactables/Doors/Door_Open_Big_01.ogg";
        private const string GeneratedFolder = "Assets/_Project/Content/Prefabs/Combat";
        private const string ProjectilePrefabPath = GeneratedFolder + "/SpitterProjectile.prefab";
        private const string SpitterPrefabPath = GeneratedFolder + "/Spitter.prefab";
        private const string SpitterControllerPath = "Assets/_Project/Content/Animations/Enemies/SpitterAnimator.controller";
        private const string TelegraphMaterialPath = GeneratedFolder + "/SpitterTelegraph.mat";
        private const string DoorMaterialPath = GeneratedFolder + "/EncounterDoor.mat";

        /// <summary>
        /// 依次创建可复用内容资产并装配 Level_01；全部引用设置完成后才保存场景，避免留下半成品状态。
        /// </summary>
        [MenuItem("Odyssey/场景/搭建单机玩法切片")]
        public static void BuildSinglePlayerSlice()
        {
            // 场景资产依赖攻击方式和射程配置；先强制完成同一套导表校验，避免 CSV 已更新但运行时资产仍是旧版本。
            GameConfigImporter.ImportAll();
            EnsureFolder(GeneratedFolder);
            var controller = BuildCleanSpitterController();
            var projectilePrefab = BuildProjectilePrefab();
            var spitterPrefab = BuildSpitterPrefab(controller, projectilePrefab);
            ConfigureScene(spitterPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("单机玩法切片搭建完成：两组独立战区、六只怪物、巡逻路线、HUD、蓝色出口和命中反馈已配置。");
        }

        private static AnimatorController BuildCleanSpitterController()
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(OriginalControllerPath);
            if (source == null)
            {
                throw new InvalidOperationException("未找到 3D Game Kit Lite 的 Spitter Animator Controller。");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SpitterControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(SpitterControllerPath);
            }

            var stateMachine = controller.layers[0].stateMachine;
            var requiredStates = new[] { "Idle", "Fleeing", "Attack", "TopHit" };
            var requiredStateSet = new HashSet<string>(requiredStates, StringComparer.Ordinal);
            var generatedClips = AssetDatabase.LoadAllAssetsAtPath(SpitterControllerPath)
                .OfType<AnimationClip>()
                .Where(clip => clip.name.StartsWith("Spitter_", StringComparison.Ordinal))
                .ToDictionary(clip => clip.name, StringComparer.Ordinal);
            foreach (var child in stateMachine.states
                         .Where(child => !requiredStateSet.Contains(child.state.name))
                         .ToArray())
            {
                stateMachine.RemoveState(child.state);
            }

            foreach (var child in stateMachine.stateMachines.ToArray())
            {
                stateMachine.RemoveStateMachine(child.stateMachine);
            }

            foreach (var obsoleteClip in generatedClips
                         .Where(pair => !requiredStateSet.Contains(pair.Key.Substring("Spitter_".Length)))
                         .Select(pair => pair.Value)
                         .ToArray())
            {
                generatedClips.Remove(obsoleteClip.name);
                Object.DestroyImmediate(obsoleteClip, true);
            }

            foreach (var stateName in requiredStates)
            {
                var sourceMotion = FindMotion(source.layers[0].stateMachine, stateName);
                var sourceClip = FindRepresentativeClip(sourceMotion);
                if (sourceClip == null)
                {
                    throw new InvalidOperationException($"官方 Spitter Animator 中未找到动作状态“{stateName}”。");
                }

                var clipName = "Spitter_" + stateName;
                if (!generatedClips.TryGetValue(clipName, out var motion))
                {
                    motion = new AnimationClip { name = clipName };
                    AssetDatabase.AddObjectToAsset(motion, controller);
                    generatedClips.Add(clipName, motion);
                }

                EditorUtility.CopySerialized(sourceClip, motion);
                motion.name = clipName;
                AnimationUtility.SetAnimationEvents(motion, Array.Empty<AnimationEvent>());
                var state = stateMachine.states
                                .FirstOrDefault(child => child.state.name == stateName)
                                .state ??
                            stateMachine.AddState(stateName);
                foreach (var transition in state.transitions.ToArray())
                {
                    state.RemoveTransition(transition);
                }

                state.motion = motion;
                state.speed = stateName == "Fleeing" ? 1.1f : 1f;
            }

            stateMachine.defaultState = stateMachine.states.First(child => child.state.name == "Idle").state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// 从官方状态或 BlendTree 中选取代表性动作并在随后复制，避免把原控制器的条件、事件和状态机行为带入项目。
        /// </summary>
        private static AnimationClip FindRepresentativeClip(Motion motion)
        {
            if (motion is AnimationClip clip)
            {
                return clip;
            }

            if (motion is BlendTree tree)
            {
                foreach (var child in tree.children)
                {
                    var nested = FindRepresentativeClip(child.motion);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 递归查找官方控制器中的指定状态，仅借用动作资源，不复用其状态机结构。
        /// </summary>
        private static Motion FindMotion(AnimatorStateMachine machine, string stateName)
        {
            foreach (var child in machine.states)
            {
                if (child.state.name == stateName)
                {
                    return child.state.motion;
                }
            }

            foreach (var child in machine.stateMachines)
            {
                var nested = FindMotion(child.stateMachine, stateName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static GameObject BuildProjectilePrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalProjectilePath);
            if (source == null)
            {
                throw new InvalidOperationException("未找到 3D Game Kit Lite 的 Spitter 投射物资源。");
            }

            var root = new GameObject("SpitterProjectile");
            try
            {
                root.AddComponent<EnemyProjectile>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "投射物表现";
                visual.transform.SetParent(root.transform, false);
                StripGameplayComponents(visual);
                SetLayerRecursively(root, 0);
                return PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildSpitterPrefab(
            RuntimeAnimatorController controller,
            GameObject projectilePrefab)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalSpitterPath);
            if (source == null)
            {
                throw new InvalidOperationException("未找到 3D Game Kit Lite 的 Spitter 角色资源。");
            }

            var sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(sourceInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var animator = sourceInstance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Object.DestroyImmediate(sourceInstance);
                throw new InvalidOperationException("Spitter 资源缺少 Animator。");
            }

            var root = animator.gameObject;
            if (root != sourceInstance)
            {
                root.transform.SetParent(null, true);
                Object.DestroyImmediate(sourceInstance);
            }

            root.name = "Spitter";
            StripGameplayComponents(root);
            animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;

            var collision = root.AddComponent<CapsuleCollider>();
            collision.center = new Vector3(0f, 0.5f, 0f);
            collision.height = 1f;
            collision.radius = 0.4f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.agentTypeID = ResolveEnemyAgentTypeId();
            agent.speed = 4.5f;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 5.5f;
            agent.radius = 0.35f;
            agent.height = 1f;
            // Prefab 默认保持关闭，遭遇开始时先采样 NavMesh 再启用，避免场景反序列化阶段在离网格稍远处创建 Agent 报错。
            agent.enabled = false;

            var projectileOrigin = new GameObject("投射物发射点").transform;
            projectileOrigin.SetParent(root.transform, false);
            projectileOrigin.localPosition = new Vector3(0f, 0.78f, 0.48f);

            var telegraph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            telegraph.name = "攻击前摇提示";
            Object.DestroyImmediate(telegraph.GetComponent<Collider>());
            telegraph.transform.SetParent(projectileOrigin, false);
            telegraph.transform.localScale = Vector3.one * 0.28f;
            telegraph.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
                TelegraphMaterialPath,
                new Color(0.25f, 1f, 0.2f, 1f),
                emission: true);
            telegraph.SetActive(false);

            var enemy = root.AddComponent<Enemy>();
            var serializedEnemy = new SerializedObject(enemy);
            serializedEnemy.FindProperty("configId").stringValue = "spitter";
            serializedEnemy.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            serializedEnemy.FindProperty("projectileOrigin").objectReferenceValue = projectileOrigin;
            serializedEnemy.FindProperty("attackTelegraph").objectReferenceValue = telegraph;
            serializedEnemy.ApplyModifiedPropertiesWithoutUndo();

            var enemyLayer = LayerMask.NameToLayer("Enemy");
            SetLayerRecursively(root, enemyLayer < 0 ? 9 : enemyLayer);
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, SpitterPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureScene(GameObject spitterPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var oldRoot = GameObject.Find(SliceRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            foreach (var oldSpitter in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(enemy => enemy.ConfigId == "spitter")
                         .ToArray())
            {
                Object.DestroyImmediate(oldSpitter.gameObject);
            }

            var player = Object.FindFirstObjectByType<PlayerController>();
            var existingEnemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(enemy => enemy.ConfigId == "chomper")
                .OrderBy(enemy => player == null ? 0f : Vector3.Distance(player.transform.position, enemy.transform.position))
                .ToArray();
            if (player == null || existingEnemies.Length < 4)
            {
                throw new InvalidOperationException("Level_01 必须包含玩家和至少四个 Chomper，才能搭建两组玩法切片。");
            }

            var selectedEnemies = existingEnemies.Take(4).ToArray();
            for (var index = 4; index < existingEnemies.Length; index++)
            {
                existingEnemies[index].gameObject.SetActive(false);
            }

            foreach (var enemy in selectedEnemies)
            {
                enemy.gameObject.SetActive(true);
            }

            var groups = PairIntoNearestGroups(selectedEnemies)
                .OrderBy(group => Vector3.Distance(player.transform.position, GetGroupCenter(group)))
                .ToArray();
            var root = new GameObject(SliceRootName);
            root.transform.position = Vector3.zero;
            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                BuildEncounterGroup(
                    root.transform,
                    scene,
                    player,
                    groups[groupIndex],
                    spitterPrefab,
                    groupIndex);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// 把四只既有 Chomper 按空间距离配成两组，避免按 Hierarchy 顺序把地图两端的怪物错误分到同一战区。
        /// 四个点只有三种配对方式，直接枚举比引入通用聚类算法更清晰且更符合当前规模。
        /// </summary>
        private static IReadOnlyList<Enemy[]> PairIntoNearestGroups(IReadOnlyList<Enemy> enemies)
        {
            var pairings = new[]
            {
                new[] { 0, 1, 2, 3 },
                new[] { 0, 2, 1, 3 },
                new[] { 0, 3, 1, 2 }
            };
            var best = pairings
                .OrderBy(pairing =>
                    Vector3.Distance(enemies[pairing[0]].transform.position, enemies[pairing[1]].transform.position) +
                    Vector3.Distance(enemies[pairing[2]].transform.position, enemies[pairing[3]].transform.position))
                .First();
            return new[]
            {
                new[] { enemies[best[0]], enemies[best[1]] },
                new[] { enemies[best[2]], enemies[best[3]] }
            };
        }

        private static Vector3 GetGroupCenter(IReadOnlyList<Enemy> group)
        {
            return (group[0].transform.position + group[1].transform.position) * 0.5f;
        }

        /// <summary>
        /// 创建一组独立的触发器、两只近战怪、一只远程怪、巡逻路线、HUD 和蓝色出口。
        /// 每组只通过实例事件协作，完成第一组不会错误开启第二组的门。
        /// </summary>
        private static void BuildEncounterGroup(
            Transform container,
            UnityEngine.SceneManagement.Scene scene,
            PlayerController player,
            Enemy[] chompers,
            GameObject spitterPrefab,
            int groupIndex)
        {
            var displayName = groupIndex == 0 ? "第一战区" : "第二战区";
            var groupRoot = new GameObject(groupIndex == 0 ? "第一战斗组" : "第二战斗组");
            groupRoot.transform.SetParent(container, false);
            var arenaCenter = GetGroupCenter(chompers);
            var awayFromPlayer = Vector3.ProjectOnPlane(arenaCenter - player.transform.position, Vector3.up).normalized;
            if (awayFromPlayer == Vector3.zero)
            {
                awayFromPlayer = player.transform.forward;
            }

            var desiredSpitterPosition =
                arenaCenter + awayFromPlayer * 3.5f + Vector3.Cross(Vector3.up, awayFromPlayer) * 2f;
            desiredSpitterPosition = ResolveSpitterNavMeshPosition(desiredSpitterPosition, chompers[1].transform.position);
            var spitterObject = (GameObject)PrefabUtility.InstantiatePrefab(spitterPrefab, scene);
            spitterObject.name = $"{displayName}_Spitter_远程威胁";
            spitterObject.transform.position = desiredSpitterPosition;
            var spitter = spitterObject.GetComponent<Enemy>();

            chompers[0].name = $"{displayName}_Chomper_1";
            chompers[1].name = $"{displayName}_Chomper_2";
            var encounter = groupRoot.AddComponent<CombatEncounterController>();
            var serializedEncounter = new SerializedObject(encounter);
            serializedEncounter.FindProperty("displayName").stringValue = displayName;
            var participants = serializedEncounter.FindProperty("participants");
            participants.arraySize = 3;
            participants.GetArrayElementAtIndex(0).objectReferenceValue = chompers[0];
            participants.GetArrayElementAtIndex(1).objectReferenceValue = chompers[1];
            participants.GetArrayElementAtIndex(2).objectReferenceValue = spitter;
            serializedEncounter.ApplyModifiedPropertiesWithoutUndo();

            BuildEncounterTrigger(
                groupRoot.transform,
                encounter,
                arenaCenter,
                player.transform.position,
                includePlayerSpawn: groupIndex == 0);
            BuildPatrolRoute(groupRoot.transform, chompers[0], awayFromPlayer, 0);
            BuildPatrolRoute(groupRoot.transform, chompers[1], awayFromPlayer, 1);
            BuildPatrolRoute(groupRoot.transform, spitter, awayFromPlayer, 2);
            BuildDoor(groupRoot.transform, encounter, arenaCenter, awayFromPlayer);
            BuildHud(groupRoot.transform, encounter, groupIndex);
            BuildFeedback(groupRoot.transform, encounter, player);
        }

        private static void BuildEncounterTrigger(
            Transform parent,
            CombatEncounterController encounter,
            Vector3 arenaCenter,
            Vector3 playerPosition,
            bool includePlayerSpawn)
        {
            var triggerObject = new GameObject("玩家进入触发区");
            triggerObject.transform.SetParent(parent, true);
            var planarCenter = includePlayerSpawn
                ? (arenaCenter + playerPosition) * 0.5f
                : arenaCenter;
            triggerObject.transform.position = planarCenter + Vector3.up * 1.5f;
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = includePlayerSpawn
                ? new Vector3(
                    Mathf.Abs(arenaCenter.x - playerPosition.x) + 6f,
                    4f,
                    Mathf.Abs(arenaCenter.z - playerPosition.z) + 6f)
                : new Vector3(16f, 4f, 16f);
            var body = triggerObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var adapter = triggerObject.AddComponent<CombatEncounterTrigger>();
            var serializedTrigger = new SerializedObject(adapter);
            serializedTrigger.FindProperty("encounter").objectReferenceValue = encounter;
            serializedTrigger.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 为一只怪物创建三个可视化巡逻点并写入 EnemyPatrolRoute；所有点都按该 Agent 类型采样到 NavMesh。
        /// </summary>
        private static void BuildPatrolRoute(
            Transform parent,
            Enemy enemy,
            Vector3 groupForward,
            int participantIndex)
        {
            var routeRoot = new GameObject($"巡逻路线_{enemy.name}");
            routeRoot.transform.SetParent(parent, true);
            routeRoot.transform.position = Vector3.zero;
            var side = Vector3.Cross(Vector3.up, groupForward).normalized;
            var directionSign = participantIndex % 2 == 0 ? 1f : -1f;
            var desiredPositions = new[]
            {
                enemy.transform.position,
                enemy.transform.position + side * (2.5f * directionSign),
                enemy.transform.position + groupForward * 2f - side * (1.8f * directionSign)
            };
            var points = new Transform[desiredPositions.Length];
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                throw new InvalidOperationException($"怪物“{enemy.name}”缺少 NavMeshAgent，无法创建巡逻路线。");
            }

            var queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            for (var pointIndex = 0; pointIndex < desiredPositions.Length; pointIndex++)
            {
                if (!NavMesh.SamplePosition(desiredPositions[pointIndex], out var hit, 4f, queryFilter))
                {
                    throw new InvalidOperationException($"无法为怪物“{enemy.name}”的巡逻点 {pointIndex + 1} 找到 NavMesh。");
                }

                var pointObject = new GameObject($"巡逻点_{pointIndex + 1}");
                pointObject.transform.SetParent(routeRoot.transform, true);
                pointObject.transform.position = hit.position;
                points[pointIndex] = pointObject.transform;
            }

            var route = enemy.GetComponent<EnemyPatrolRoute>();
            if (route == null)
            {
                route = enemy.gameObject.AddComponent<EnemyPatrolRoute>();
            }

            var serializedRoute = new SerializedObject(route);
            var patrolPoints = serializedRoute.FindProperty("patrolPoints");
            patrolPoints.arraySize = points.Length;
            for (var index = 0; index < points.Length; index++)
            {
                patrolPoints.GetArrayElementAtIndex(index).objectReferenceValue = points[index];
            }

            serializedRoute.FindProperty("waitDuration").floatValue = 0.65f + participantIndex * 0.15f;
            serializedRoute.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(route);
        }

        /// <summary>
        /// 在编辑阶段把远程怪位置固化到已烘焙 NavMesh。优先保留设计坐标；若旧怪物只是摆在装饰平台或网格边缘，
        /// 则退回到已验证可导航的近战怪附近，避免把运行时纠错当成场景配置方案。
        /// </summary>
        private static Vector3 ResolveSpitterNavMeshPosition(Vector3 desiredPosition, Vector3 fallbackPosition)
        {
            var queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = ResolveEnemyAgentTypeId(),
                areaMask = NavMesh.AllAreas
            };
            if (NavMesh.SamplePosition(desiredPosition, out var desiredHit, 12f, queryFilter))
            {
                return desiredHit.position;
            }

            var fallbackOffset = fallbackPosition + Vector3.right * 3f;
            if (NavMesh.SamplePosition(fallbackOffset, out var fallbackHit, 6f, queryFilter))
            {
                Debug.LogWarning("远程怪原始位置附近没有 NavMesh，已自动移动到战斗区域内的安全位置。");
                return fallbackHit.position;
            }

            throw new InvalidOperationException("无法为远程怪找到可导航位置，请先检查 Level_01 的 NavMesh 烘焙结果。");
        }

        /// <summary>
        /// 从项目 NavMesh 设置中选择与 3D Game Kit Lite 小型怪物高度匹配的 Agent 类型。
        /// 不硬编码 Unity 生成的类型 ID，避免项目迁移或重新创建 Agent 类型后预制体悄悄失效。
        /// </summary>
        private static int ResolveEnemyAgentTypeId()
        {
            const float expectedEnemyHeight = 1f;
            const float allowedHeightDifference = 0.15f;
            for (var index = 0; index < NavMesh.GetSettingsCount(); index++)
            {
                var settings = NavMesh.GetSettingsByIndex(index);
                if (Mathf.Abs(settings.agentHeight - expectedEnemyHeight) <= allowedHeightDifference)
                {
                    return settings.agentTypeID;
                }
            }

            throw new InvalidOperationException("NavMesh 设置中缺少高度约 1 米的小型怪物 Agent 类型。");
        }

        private static void BuildDoor(
            Transform parent,
            CombatEncounterController encounter,
            Vector3 arenaCenter,
            Vector3 forward)
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "蓝色战斗出口_击败本组敌人后开启";
            door.transform.SetParent(parent, true);
            door.transform.position = arenaCenter + forward * 7f + Vector3.up * 2f;
            door.transform.rotation = Quaternion.LookRotation(forward);
            door.transform.localScale = new Vector3(5f, 4f, 0.6f);
            door.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
                DoorMaterialPath,
                new Color(0.05f, 0.55f, 0.8f, 1f),
                emission: true);

            var audioSource = door.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorAudioPath);
            var doorView = door.AddComponent<CombatEncounterDoor>();
            var serializedDoor = new SerializedObject(doorView);
            serializedDoor.FindProperty("encounter").objectReferenceValue = encounter;
            serializedDoor.FindProperty("audioSource").objectReferenceValue = audioSource;
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHud(Transform parent, CombatEncounterController encounter, int displayIndex)
        {
            var canvasObject = new GameObject("战斗目标HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var textObject = new GameObject("战斗状态文字", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -55f - displayIndex * 44f);
            rect.sizeDelta = new Vector2(720f, 64f);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = $"{encounter.DisplayName}：进入区域，击败敌人后蓝色出口开启";

            var view = canvasObject.AddComponent<CombatEncounterView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("encounter").objectReferenceValue = encounter;
            serializedView.FindProperty("statusText").objectReferenceValue = text;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildFeedback(
            Transform parent,
            CombatEncounterController encounter,
            PlayerController player)
        {
            var feedbackObject = new GameObject("战斗反馈");
            feedbackObject.transform.SetParent(parent, false);
            var audioSource = feedbackObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            var impulse = feedbackObject.AddComponent<CinemachineImpulseSource>();
            var feedback = feedbackObject.AddComponent<CombatImpactFeedback>();
            var serializedFeedback = new SerializedObject(feedback);
            serializedFeedback.FindProperty("encounter").objectReferenceValue = encounter;
            serializedFeedback.FindProperty("player").objectReferenceValue = player;
            serializedFeedback.FindProperty("hitEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(HitEffectPath);
            serializedFeedback.FindProperty("evadeEffectPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(HitEffectPath);
            serializedFeedback.FindProperty("audioSource").objectReferenceValue = audioSource;
            serializedFeedback.FindProperty("hitClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(HitAudioPath);
            serializedFeedback.FindProperty("evadeClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(EvadeAudioPath);
            serializedFeedback.FindProperty("impulseSource").objectReferenceValue = impulse;
            serializedFeedback.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StripGameplayComponents(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(body);
            }

            foreach (var agent in root.GetComponentsInChildren<NavMeshAgent>(true))
            {
                Object.DestroyImmediate(agent);
            }

            foreach (var controller in root.GetComponentsInChildren<CharacterController>(true))
            {
                Object.DestroyImmediate(controller);
            }
        }

        private static Material GetOrCreateMaterial(string path, Color color, bool emission)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
