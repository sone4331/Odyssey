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
    [InitializeOnLoad]
    public static class SinglePlayerSliceBuilder
    {
        private const string AutomationRequestPath = "Temp/BuildSinglePlayerSlice.request";
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

        static SinglePlayerSliceBuilder()
        {
            if (!System.IO.File.Exists(AutomationRequestPath))
            {
                return;
            }

            System.IO.File.Delete(AutomationRequestPath);
            EditorApplication.delayCall += BuildWhenEditorReady;
        }

        /// <summary>
        /// 供本地自动验收在 Unity 完成编译后调用同一 Builder；如果编辑器仍处于运行模式，先安全退出再重试。
        /// 复用菜单入口而不复制搭建逻辑，保证人工点击和自动验收生成完全相同的场景结果。
        /// </summary>
        private static void BuildWhenEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += BuildWhenEditorReady;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BuildWhenEditorReady;
                return;
            }

            BuildSinglePlayerSlice();
        }

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
            Debug.Log("单机玩法切片搭建完成：两组战区、六只自主行为树怪物、宽范围巡逻网络、清怪踏板门禁、HUD 和命中反馈已配置。");
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
            ClearExistingClearanceOverrides();
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
            var gameplayStartPosition = player != null && player.RespawnPoint != null
                ? player.RespawnPoint.position
                : player == null ? Vector3.zero : player.transform.position;
            var existingEnemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(enemy => enemy.ConfigId == "chomper")
                .OrderBy(enemy => Vector3.Distance(gameplayStartPosition, enemy.transform.position))
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
                .OrderBy(group => GetGameplayPathDistance(gameplayStartPosition, group))
                .ThenBy(group => GetNearestGroupDistance(gameplayStartPosition, group))
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
                    groupIndex,
                    gameplayStartPosition);
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

        private static float GetNearestGroupDistance(Vector3 position, IReadOnlyList<Enemy> group)
        {
            return group.Min(enemy => Vector3.Distance(position, enemy.transform.position));
        }

        /// <summary>
        /// 用 NavMesh 实际路径长度判定关卡先后顺序，而不是使用可能穿过墙体和隔离门的直线距离。
        /// 这是场景构建阶段的一次性拓扑判断；结果会固化为 sequenceIndex，运行时不再根据巡逻中的怪物位置重新编号。
        /// </summary>
        private static float GetGameplayPathDistance(Vector3 startPosition, IReadOnlyList<Enemy> group)
        {
            var agent = group.Select(enemy => enemy.GetComponent<NavMeshAgent>())
                .FirstOrDefault(candidate => candidate != null);
            if (agent == null)
            {
                return float.MaxValue;
            }

            var queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            if (!NavMesh.SamplePosition(startPosition, out var startHit, 4f, queryFilter))
            {
                return float.MaxValue;
            }

            var bestDistance = float.MaxValue;
            foreach (var enemy in group)
            {
                if (!NavMesh.SamplePosition(enemy.transform.position, out var enemyHit, 4f, queryFilter))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(startHit.position, enemyHit.position, queryFilter, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                var length = 0f;
                for (var cornerIndex = 1; cornerIndex < path.corners.Length; cornerIndex++)
                {
                    length += Vector3.Distance(path.corners[cornerIndex - 1], path.corners[cornerIndex]);
                }

                bestDistance = Mathf.Min(bestDistance, length);
            }

            return bestDistance;
        }

        /// <summary>
        /// 创建一组两只近战怪、一只远程怪、共享巡逻网络、HUD 和反馈。
        /// 怪物从场景启动起自主感知，遭遇控制器只统计死亡结果，不再承担 AI 激活职责。
        /// </summary>
        private static void BuildEncounterGroup(
            Transform container,
            UnityEngine.SceneManagement.Scene scene,
            PlayerController player,
            Enemy[] chompers,
            GameObject spitterPrefab,
            int groupIndex,
            Vector3 gameplayStartPosition)
        {
            var displayName = groupIndex == 0 ? "第一战区" : "第二战区";
            var groupRoot = new GameObject(groupIndex == 0 ? "第一战斗组" : "第二战斗组");
            groupRoot.transform.SetParent(container, false);
            var arenaCenter = GetGroupCenter(chompers);
            var awayFromPlayer = Vector3.ProjectOnPlane(arenaCenter - gameplayStartPosition, Vector3.up).normalized;
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
            serializedEncounter.FindProperty("sequenceIndex").intValue = groupIndex;
            serializedEncounter.FindProperty("displayName").stringValue = displayName;
            var participants = serializedEncounter.FindProperty("participants");
            participants.arraySize = 3;
            participants.GetArrayElementAtIndex(0).objectReferenceValue = chompers[0];
            participants.GetArrayElementAtIndex(1).objectReferenceValue = chompers[1];
            participants.GetArrayElementAtIndex(2).objectReferenceValue = spitter;
            serializedEncounter.ApplyModifiedPropertiesWithoutUndo();

            BuildSharedPatrolNetwork(
                groupRoot.transform,
                new[] { chompers[0], chompers[1], spitter },
                arenaCenter,
                awayFromPlayer);
            if (groupIndex == 0)
            {
                ConfigureExistingClearanceGate(encounter, arenaCenter);
            }

            BuildHud(groupRoot.transform, encounter, groupIndex);
            BuildFeedback(groupRoot.transform, encounter, player);
        }

        /// <summary>
        /// 为同一战区生成六个处于同一 NavMesh 连通区域的巡逻点，并把三只怪物放到不同起始点。
        /// 只有“采样到网格”并不足以证明可达，因此每个候选点都必须从锚点计算出完整路径后才能进入路线。
        /// </summary>
        private static void BuildSharedPatrolNetwork(
            Transform parent,
            IReadOnlyList<Enemy> enemies,
            Vector3 arenaCenter,
            Vector3 groupForward)
        {
            var routeRoot = new GameObject("共享宽范围巡逻网络");
            routeRoot.transform.SetParent(parent, true);
            routeRoot.transform.position = Vector3.zero;
            groupForward = Vector3.ProjectOnPlane(groupForward, Vector3.up).normalized;
            if (groupForward.sqrMagnitude < 0.01f)
            {
                groupForward = Vector3.forward;
            }

            var side = Vector3.Cross(Vector3.up, groupForward).normalized;
            var agent = enemies.Select(enemy => enemy == null ? null : enemy.GetComponent<NavMeshAgent>())
                .FirstOrDefault(candidate => candidate != null);
            if (agent == null)
            {
                throw new InvalidOperationException("战区怪物缺少 NavMeshAgent，无法创建共享巡逻网络。");
            }

            var queryFilter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            if (!NavMesh.SamplePosition(enemies[0].transform.position, out var anchorHit, 4f, queryFilter))
            {
                throw new InvalidOperationException("第一只怪物附近没有对应类型的 NavMesh，无法建立巡逻锚点。");
            }

            var openCandidates = new List<Vector3>();
            var searchCenter = Vector3.Lerp(anchorHit.position, arenaCenter, 0.35f);
            var radii = new[] { 4f, 6f, 8f, 10f, 12f, 14f, 16f };
            for (var angleIndex = 0; angleIndex < 24; angleIndex++)
            {
                var angle = angleIndex * 15f * Mathf.Deg2Rad;
                foreach (var radius in radii)
                {
                    var desired = searchCenter +
                                  groupForward * (Mathf.Cos(angle) * radius) +
                                  side * (Mathf.Sin(angle) * radius);
                    if (!NavMesh.SamplePosition(desired, out var hit, 1.5f, queryFilter) ||
                        Mathf.Abs(hit.position.y - anchorHit.position.y) > 1f ||
                        Vector3.Distance(
                            Vector3.ProjectOnPlane(hit.position, Vector3.up),
                            Vector3.ProjectOnPlane(desired, Vector3.up)) > 1.5f ||
                        openCandidates.Any(position => Vector3.Distance(position, hit.position) < 1.5f) ||
                        !HasCompletePath(anchorHit.position, hit.position, queryFilter) ||
                        !HasOpenPatrolArea(hit.position, queryFilter))
                    {
                        continue;
                    }

                    openCandidates.Add(hit.position);
                }
            }

            if (openCandidates.Count < 6)
            {
                throw new InvalidOperationException(
                    $"战区只找到 {openCandidates.Count} 个远离墙角且同区可达的候选点，未达到六点路线要求。");
            }

            var reachablePositions = SelectWidePatrolPoints(openCandidates, searchCenter, groupForward, side);
            var points = new Transform[reachablePositions.Count];
            for (var pointIndex = 0; pointIndex < reachablePositions.Count; pointIndex++)
            {
                var nextIndex = (pointIndex + 1) % reachablePositions.Count;
                if (!HasCompletePath(reachablePositions[pointIndex], reachablePositions[nextIndex], queryFilter))
                {
                    throw new InvalidOperationException($"巡逻点 {pointIndex + 1} 无法到达巡逻点 {nextIndex + 1}。");
                }

                var pointObject = new GameObject($"共享巡逻点_{pointIndex + 1}");
                pointObject.transform.SetParent(routeRoot.transform, true);
                pointObject.transform.position = reachablePositions[pointIndex];
                points[pointIndex] = pointObject.transform;
            }

            var maximumDistance = points
                .SelectMany((first, index) => points.Skip(index + 1).Select(second => Vector3.Distance(first.position, second.position)))
                .DefaultIfEmpty(0f)
                .Max();
            if (maximumDistance < 12f)
            {
                throw new InvalidOperationException(
                    $"共享巡逻网络跨度只有 {maximumDistance:F1} 米，未达到 12 米的战区覆盖要求。");
            }

            for (var enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                var enemy = enemies[enemyIndex];
                var startIndex = enemyIndex * 2;
                ConfigureCodeDrivenNavigation(enemy, points[startIndex].position);
                var route = enemy.GetComponent<EnemyPatrolRoute>();
                if (route == null)
                {
                    route = enemy.gameObject.AddComponent<EnemyPatrolRoute>();
                }

                var serializedRoute = new SerializedObject(route);
                var patrolPoints = serializedRoute.FindProperty("patrolPoints");
                patrolPoints.arraySize = points.Length;
                for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    patrolPoints.GetArrayElementAtIndex(pointIndex).objectReferenceValue = points[pointIndex];
                }

                serializedRoute.FindProperty("initialPointIndex").intValue = startIndex;
                serializedRoute.FindProperty("waitDuration").floatValue = 0.55f + enemyIndex * 0.15f;
                serializedRoute.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(route);
            }
        }

        private static bool HasCompletePath(
            Vector3 source,
            Vector3 destination,
            NavMeshQueryFilter queryFilter)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(source, destination, queryFilter, path) &&
                   path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>
        /// 使用最远点采样从开阔候选中选出覆盖范围最大的六点，再按环绕战区中心的角度排序成稳定环路。
        /// 该贪心算法只解决当前固定六点的小规模装配问题，比引入通用聚类或路径编辑器更直接。
        /// </summary>
        private static List<Vector3> SelectWidePatrolPoints(
            IReadOnlyList<Vector3> candidates,
            Vector3 center,
            Vector3 forward,
            Vector3 side)
        {
            const int requiredCount = 6;
            var selected = new List<Vector3>
            {
                candidates.OrderBy(position => Vector3.Distance(position, center)).First()
            };
            while (selected.Count < requiredCount)
            {
                var next = candidates
                    .Where(candidate => !selected.Contains(candidate))
                    .OrderByDescending(candidate => selected.Min(chosen => Vector3.Distance(candidate, chosen)))
                    .ThenBy(candidate => Mathf.Abs(Vector3.Distance(candidate, center) - 9f))
                    .First();
                selected.Add(next);
            }

            selected = selected
                .OrderBy(position =>
                {
                    var offset = Vector3.ProjectOnPlane(position - center, Vector3.up);
                    return Mathf.Atan2(Vector3.Dot(offset, side), Vector3.Dot(offset, forward));
                })
                .ToList();
            var maximumDistance = selected.Max(first => selected.Max(second => Vector3.Distance(first, second)));
            if (maximumDistance < 12f)
            {
                throw new InvalidOperationException("开阔候选点过于集中，无法形成跨度至少 12 米的巡逻路线。");
            }

            return selected;
        }

        /// <summary>
        /// 从候选点向八个水平方向检查约两米的可行走空间，至少六个方向保持连通才视为开阔巡逻区。
        /// 该 NavMesh 净空判定会排除墙角、窄缝和装饰物边缘，不额外维护人工障碍物标签。
        /// </summary>
        private static bool HasOpenPatrolArea(Vector3 position, NavMeshQueryFilter queryFilter)
        {
            const float clearanceRadius = 1.8f;
            const float sampleTolerance = 0.75f;
            if (!NavMesh.FindClosestEdge(position, out var closestEdge, queryFilter) ||
                closestEdge.distance < clearanceRadius)
            {
                return false;
            }

            var openDirections = 0;
            for (var directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                var angle = directionIndex * 45f * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var desired = position + direction * clearanceRadius;
                if (!NavMesh.SamplePosition(desired, out var hit, sampleTolerance, queryFilter) ||
                    Vector3.Distance(hit.position, desired) > sampleTolerance ||
                    !HasCompletePath(position, hit.position, queryFilter))
                {
                    continue;
                }

                openDirections++;
            }

            return openDirections >= 5;
        }

        /// <summary>
        /// 将怪物放到已验证的路线起点，并明确关闭 Root Motion、开启 Agent 位置与旋转同步。
        /// Animator 只表现跑步；若让零位移动画继续拥有根节点权威，它会在每帧末尾覆盖 NavMeshAgent 位移。
        /// </summary>
        private static void ConfigureCodeDrivenNavigation(Enemy enemy, Vector3 startPosition)
        {
            var animator = enemy.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                EditorUtility.SetDirty(animator);
            }

            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                throw new InvalidOperationException($"怪物“{enemy.name}”缺少 NavMeshAgent。");
            }

            var wasEnabled = agent.enabled;
            if (wasEnabled)
            {
                agent.enabled = false;
            }

            enemy.transform.position = startPosition;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.enabled = wasEnabled;
            EditorUtility.SetDirty(enemy.transform);
            EditorUtility.SetDirty(agent);
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

        /// <summary>
        /// 接管主流程中 Mechanism1 踏板与 FinalBuilding 隔离门，移除原教学命令并装配项目自有双条件门禁。
        /// Level_01 同时包含教学示例门，必须使用稳定层级路径而不是“离怪物最近”，否则会接管错误机关。
        /// </summary>
        private static void ConfigureExistingClearanceGate(
            CombatEncounterController encounter,
            Vector3 arenaCenter)
        {
            var sceneTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var doorCandidate = sceneTransforms.FirstOrDefault(transform =>
                GetHierarchyPath(transform).EndsWith(
                    "ExampleLevel/FinalBuilding/DoorHuge",
                    StringComparison.Ordinal));
            if (doorCandidate == null)
            {
                throw new InvalidOperationException("未找到主流程路径 ExampleLevel/FinalBuilding/DoorHuge，拒绝误接管教学示例门。");
            }

            var doorRoot = ResolvePrefabRoot(doorCandidate.gameObject).transform;
            var padCandidate = sceneTransforms.FirstOrDefault(transform =>
                GetHierarchyPath(transform).EndsWith(
                    "ExampleLevel/Mechanism1/PressurePad",
                    StringComparison.Ordinal));
            if (padCandidate == null)
            {
                throw new InvalidOperationException("未找到主流程路径 ExampleLevel/Mechanism1/PressurePad，无法配置清怪门禁。");
            }

            var padRoot = ResolvePrefabRoot(padCandidate.gameObject).transform;

            RemoveThirdPartyBehaviours(padRoot.gameObject,
                "Gamekit3D.GameCommands.SendOnTriggerEnter",
                "Gamekit3D.InteractOnTrigger");
            RemoveThirdPartyBehaviours(doorRoot.gameObject,
                "Gamekit3D.GameCommands.SimpleTranslator",
                "Gamekit3D.GameCommands.GameCommandReceiver");

            var movingBody = doorRoot.GetComponentsInChildren<Rigidbody>(true)
                .OrderByDescending(body => Mathf.Abs(body.transform.localPosition.y))
                .FirstOrDefault();
            if (movingBody == null)
            {
                throw new InvalidOperationException("DoorHuge 缺少可移动 Rigidbody 门板。");
            }

            var sourceMovingPart = PrefabUtility.GetCorrespondingObjectFromSource(movingBody.transform);
            var closedLocalPosition = sourceMovingPart == null
                ? movingBody.transform.localPosition
                : sourceMovingPart.localPosition;
            movingBody.transform.localPosition = closedLocalPosition;
            movingBody.position = movingBody.transform.position;

            var audioSource = doorRoot.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = doorRoot.gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorAudioPath);
            var gate = doorRoot.GetComponent<EncounterClearanceGate>();
            if (gate == null)
            {
                gate = doorRoot.gameObject.AddComponent<EncounterClearanceGate>();
            }
            var serializedGate = new SerializedObject(gate);
            serializedGate.FindProperty("movingPart").objectReferenceValue = movingBody.transform;
            serializedGate.FindProperty("closedLocalPosition").vector3Value = closedLocalPosition;
            serializedGate.FindProperty("openLocalOffset").vector3Value = Vector3.down * 10.1f;
            serializedGate.FindProperty("audioSource").objectReferenceValue = audioSource;
            serializedGate.ApplyModifiedPropertiesWithoutUndo();

            var triggerCollider = padRoot.GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(collider => collider.isTrigger) ??
                padRoot.GetComponentsInChildren<Collider>(true).FirstOrDefault();
            if (triggerCollider == null)
            {
                throw new InvalidOperationException("PressurePad 缺少用于检测玩家的 Collider。");
            }

            triggerCollider.isTrigger = true;
            var pressurePlate = triggerCollider.GetComponent<EncounterClearancePressurePlate>();
            if (pressurePlate == null)
            {
                pressurePlate = triggerCollider.gameObject.AddComponent<EncounterClearancePressurePlate>();
            }
            var serializedPlate = new SerializedObject(pressurePlate);
            serializedPlate.FindProperty("encounter").objectReferenceValue = encounter;
            serializedPlate.FindProperty("gate").objectReferenceValue = gate;
            serializedPlate.FindProperty("triggerVolume").objectReferenceValue = triggerCollider;
            serializedPlate.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"第一战区门禁已接管：踏板“{padRoot.name}” → 隔离门“{doorRoot.name}”。");
        }

        /// <summary>
        /// 清除前几次场景构建遗留的项目门禁，并把被接管过的门恢复到 Prefab 定义的关闭姿态。
        /// Builder 必须幂等：重复执行后场景中始终只能存在一套 Plate 与 Gate，不能让旧引用形成旁路。
        /// </summary>
        private static void ClearExistingClearanceOverrides()
        {
            foreach (var plate in Object.FindObjectsByType<EncounterClearancePressurePlate>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var root = ResolvePrefabRoot(plate.gameObject);
                RemoveThirdPartyBehaviours(root,
                    "Gamekit3D.GameCommands.SendOnTriggerEnter",
                    "Gamekit3D.InteractOnTrigger");
                Object.DestroyImmediate(plate);
            }

            foreach (var gate in Object.FindObjectsByType<EncounterClearanceGate>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var serializedGate = new SerializedObject(gate);
                var movingPart = serializedGate.FindProperty("movingPart").objectReferenceValue as Transform;
                if (movingPart != null)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(movingPart);
                    if (source != null)
                    {
                        movingPart.localPosition = source.localPosition;
                        EditorUtility.SetDirty(movingPart);
                    }
                }

                var root = ResolvePrefabRoot(gate.gameObject);
                RemoveThirdPartyBehaviours(root,
                    "Gamekit3D.GameCommands.SimpleTranslator",
                    "Gamekit3D.GameCommands.GameCommandReceiver");
                Object.DestroyImmediate(gate);
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }

        private static GameObject ResolvePrefabRoot(GameObject candidate)
        {
            // 只接管最近一层 PressurePad/DoorHuge Prefab，避免误伤外层房间 Prefab 中的其他机关。
            return PrefabUtility.GetNearestPrefabInstanceRoot(candidate) ?? candidate;
        }

        private static void RemoveThirdPartyBehaviours(GameObject root, params string[] typeNames)
        {
            var names = new HashSet<string>(typeNames, StringComparer.Ordinal);
            var matchedBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(behaviour => behaviour != null && names.Contains(behaviour.GetType().FullName))
                // GameCommandReceiver 被 SimpleTranslator 依赖，必须最后删除，否则 Unity 会拒绝移除组件。
                .OrderBy(behaviour => behaviour.GetType().FullName ==
                                      "Gamekit3D.GameCommands.GameCommandReceiver")
                .ToArray();
            foreach (var behaviour in matchedBehaviours)
            {
                Object.DestroyImmediate(behaviour);
            }
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
            text.text = $"{encounter.DisplayName}：清理全部敌人";

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
