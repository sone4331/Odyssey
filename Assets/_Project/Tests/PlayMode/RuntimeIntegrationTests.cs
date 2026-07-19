using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.AI;
using Odyssey.Gameplay.Config;
using Odyssey.Encounters;
using Odyssey.Unity.UI;
using Odyssey.Networking;
using Cinemachine;
using Unity.Netcode.Transports.SinglePlayer;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// 在真实 Level_01 生命周期中验证 Bootstrap 注入、动态血条、暂停副作用与怪物决策闭环。
    /// 采用少量端到端冒烟测试覆盖模块边界，避免重复纯 C# 规格或搭建庞大的场景测试框架。
    /// </summary>
    public sealed class RuntimeIntegrationTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplayScene()
        {
            Time.timeScale = 1f;
            var operation = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            // 多等待一帧，让 RuntimeInitialize、场景 Installer 和各组件 Start 完成装配。
            yield return null;
            yield return null;

            var session = Object.FindFirstObjectByType<GameplaySessionController>();
            if (session != null && !session.IsStarted)
            {
                Assert.That(session.StartSinglePlayer(), Is.True, "PlayMode 测试无法启动原关卡单机传输");
            }

            for (var frame = 0;
                 frame < 120 && Object.FindFirstObjectByType<PlayerController>() == null;
                 frame++)
            {
                yield return null;
            }

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreGlobalState()
        {
            Time.timeScale = 1f;
            var session = Object.FindFirstObjectByType<GameplaySessionController>();
            if (session != null)
            {
                session.Shutdown();
                Object.Destroy(session.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_AppliesSixHealthAndBuildsSixIcons()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var healthUi = Object.FindFirstObjectByType<PlayerHealthUI>();
            var heartContainer = GameObject.Find("HeartContainer");

            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            Assert.That(healthUi, Is.Not.Null, "场景中未找到血量 UI");
            Assert.That(heartContainer, Is.Not.Null, "场景中未找到生命图标容器");
            Assert.That(player.MaxHealth, Is.EqualTo(6), "Bootstrap 未应用导表后的六点生命配置");
            Assert.That(player.CurrentHealth, Is.EqualTo(6), "玩家初始生命未与最大生命同步");
            Assert.That(heartContainer.transform.childCount, Is.EqualTo(6), "血量图标池未扩容到六格");
            Assert.That((player.WallLayer.value & (1 << 16)) != 0, Is.True,
                "玩家未配置关卡墙面层，墙滑和墙跳分支不可达");
            Assert.That((player.GroundLayer.value & (1 << 16)) != 0, Is.True,
                "玩家未配置关卡地面层，斜坡投影和脚部贴地不可用");
            Assert.That(player.WalkSpeed, Is.EqualTo(4f));
            Assert.That(player.RunSpeed, Is.EqualTo(8f));
            Assert.That(player.Controller.center.y, Is.EqualTo(0.901f).Within(0.001f));
            Assert.That(player.Controller.skinWidth, Is.EqualTo(0.035f).Within(0.001f));
            Assert.That(player.GetComponent<Odyssey.Characters.Player.PlayerFootPlacementController>(), Is.Not.Null,
                "玩家 Prefab 缺少脚部贴地控制器");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SinglePlayerSession_UsesNoPortTransportAndKeepsLocalHealthAuthority()
        {
            var session = Object.FindFirstObjectByType<GameplaySessionController>();
            var player = Object.FindFirstObjectByType<PlayerController>();

            Assert.That(session, Is.Not.Null);
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.SinglePlayer));
            Assert.That(session.Manager.NetworkConfig.NetworkTransport, Is.TypeOf<SinglePlayerTransport>(),
                "单机模式没有使用 NGO 原生无端口传输");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.RespawnPoint, Is.Not.Null, "单机运行时没有恢复 Level_01 复活点引用");

            player.SetHealth(player.MaxHealth - 2, "单机存档恢复测试");
            yield return null;

            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth - 2),
                "单机生命仍被网络权威接管，读取存档无法恢复实际生命");
        }

        [UnityTest]
        public IEnumerator SinglePlayer_ExactColliderOverlapDamagesOnce()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var enemy = enemies.First(candidate => candidate.AttackDamage > 0 && candidate.CurrentHealth > 0);
            foreach (var candidate in enemies)
            {
                candidate.enabled = false;
                var navigation = candidate.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navigation != null)
                {
                    navigation.enabled = false;
                }
            }

            var bodyCollider = enemy.GetComponentsInChildren<Collider>(true)
                .First(candidate => !candidate.isTrigger && candidate.enabled);
            Assert.That(bodyCollider, Is.Not.Null, "近战怪物缺少可用于接触伤害的身体碰撞体");

            player.SetHealth(player.MaxHealth, "单机碰撞体重叠测试");
            player.Controller.enabled = false;
            player.transform.position = bodyCollider.bounds.center - player.transform.rotation * player.Controller.center;
            player.Controller.enabled = true;
            Physics.SyncTransforms();
            var healthBeforeContact = player.CurrentHealth;

            Assert.That(player.TryGetTouchingEnemy(out var touchingEnemy), Is.True,
                "玩家真实胶囊与怪物身体碰撞体重叠后没有返回接触目标");
            Assert.That(touchingEnemy, Is.EqualTo(enemy));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(player.CurrentHealth, Is.EqualTo(healthBeforeContact - enemy.AttackDamage),
                "单机碰撞体重叠后没有在物理帧内结算一次接触伤害");
            yield return new WaitForSeconds(0.3f);
            Assert.That(player.CurrentHealth, Is.EqualTo(healthBeforeContact - enemy.AttackDamage),
                "受伤保护期间持续重叠导致了重复扣血");
        }

        [UnityTest]
        public IEnumerator EscapeMenu_AtomicallyUpdatesLocalInputAndTimeScale()
        {
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var binder = Object.FindFirstObjectByType<GameplayLocalViewBinder>();
            Assert.That(menu, Is.Not.Null, "场景中未找到统一菜单控制器");
            Assert.That(binder, Is.Not.Null, "场景中未找到本地视图绑定器");

            menu.OpenPauseMenu();
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(menu.CurrentPage, Is.EqualTo(GameMenuPage.PauseMenu));
            Assert.That(binder.IsGameplayInputEnabled, Is.False);

            menu.ResumeGame();
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(menu.CurrentPage, Is.EqualTo(GameMenuPage.Gameplay));
            Assert.That(binder.IsGameplayInputEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator LocalOwner_BindsFreeLookCameraAndCameraInput()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var binder = Object.FindFirstObjectByType<GameplayLocalViewBinder>();
            var freeLook = Object.FindFirstObjectByType<CinemachineFreeLook>();

            Assert.That(player, Is.Not.Null);
            Assert.That(binder, Is.Not.Null);
            Assert.That(freeLook, Is.Not.Null, "Level_01 缺少 FreeLook 摄像机");
            Assert.That(binder.LocalPlayer, Is.SameAs(player));
            Assert.That(freeLook.Follow, Is.Not.Null, "FreeLook 没有绑定本机 Owner");
            Assert.That(freeLook.Follow, Is.SameAs(player.transform),
                "FreeLook Follow 应保持联机前语义：跟随玩家根节点");
            Assert.That(freeLook.LookAt, Is.Not.Null);
            Assert.That(freeLook.LookAt.name, Is.EqualTo("CamLookAtTarget"),
                "FreeLook 错误使用了位于角色前方十米的 StrafeCamTarget");
            Assert.That(freeLook.LookAt.GetComponentInParent<PlayerController>(), Is.SameAs(player));
            Assert.That(freeLook.LookAt.localPosition.y, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(freeLook.GetComponent<CinemachineInputProvider>().enabled, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerAnimator_DrivesAttackHitAndRecoveryWithoutTransitions()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");

            InvokePlayerCommand(player, "HandleAttackRequested");
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Attack));
            AssertAnimatorState(player.Animator, "EllenCombo_1");

            player.TakeDamage(1, player.transform.position - player.transform.forward);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Hit));
            AssertAnimatorState(player.Animator, "EllenHitFront");

            yield return new WaitForSeconds(0.6f);
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Free));
            AssertAnimatorState(player.Animator, "Locomotion", "受击结束");
        }

        [UnityTest]
        public IEnumerator PlayerAnimator_AttackAndGroundDashNaturallyReturnToLocomotion()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");

            InvokePlayerCommand(player, "HandleAttackRequested");
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Attack));

            yield return new WaitForSeconds(1.2f);
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Free),
                "攻击动画结束后动作轴未恢复空闲");
            AssertAnimatorState(player.Animator, "Locomotion", "攻击自然结束");

            InvokePlayerCommand(player, "HandleDashRequested");
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Dash));

            yield return new WaitForSeconds(player.DashDuration + 0.1f);
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Free),
                "冲刺结束后动作轴未恢复空闲");
            AssertAnimatorState(player.Animator, "Locomotion", "地面冲刺自然结束");
        }

        [UnityTest]
        public IEnumerator PlayerAnimator_ActionOwnsAnimationAndBlocksJumpInput()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            Assert.That(player.InputReader, Is.Not.Null, "玩家缺少输入适配器");

            InvokePlayerCommand(player, "HandleAttackRequested");
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Attack));

            SetJumpPressed(player, true);
            yield return new WaitForSeconds(0.25f);
            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Grounded),
                "攻击占用控制权时，移动轴仍错误响应了跳跃输入");
            AssertAnimatorState(player.Animator, "EllenCombo_1", "攻击期间的动画所有权");
            SetJumpPressed(player, false);
        }

        [UnityTest]
        public IEnumerator PlayerMovement_SmoothlyChangesSpeedAndPreservesRunSpeedAfterDash()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");

            SetMovementValue(player, Vector2.up);
            yield return null;
            var firstFrameSpeed = player.CurrentPlanarSpeed;
            Assert.That(firstFrameSpeed, Is.GreaterThan(0f));
            Assert.That(firstFrameSpeed, Is.LessThan(player.WalkSpeed), "玩家首帧直接跳到了完整步行速度");

            yield return new WaitForSeconds(0.3f);
            Assert.That(player.CurrentPlanarSpeed, Is.EqualTo(player.WalkSpeed).Within(0.15f));

            SetMovementValue(player, Vector2.zero);
            yield return null;
            Assert.That(player.CurrentPlanarSpeed, Is.GreaterThan(0f), "松开输入后速度被瞬间清零");
            yield return new WaitForSeconds(0.25f);
            Assert.That(player.CurrentPlanarSpeed, Is.EqualTo(0f).Within(0.05f));

            SetMovementValue(player, Vector2.up);
            yield return null;
            InvokePlayerCommand(player, "HandleDashRequested");
            yield return null;
            Assert.That(player.ActionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Dash),
                "保持方向时未进入冲刺动作");

            yield return new WaitForSeconds(player.DashDuration + 0.05f);
            Assert.That(player.ActionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Free),
                "冲刺时长结束后动作轴未恢复自由状态");
            Assert.That(player.CurrentPlanarSpeed, Is.EqualTo(player.RunSpeed).Within(0.15f),
                "冲刺结束且保持方向时没有直接继承最大奔跑速度");

            SetMovementValue(player, Vector2.zero);
            yield return new WaitForSeconds(0.4f);
            Assert.That(player.CurrentPlanarSpeed, Is.EqualTo(0f).Within(0.05f),
                "冲刺速度继承在松开方向后没有恢复普通减速规则");
        }

        [UnityTest]
        public IEnumerator PlayerDash_RejectsDamageUntilAbilityEnds()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            var healthBefore = player.CurrentHealth;

            InvokePlayerCommand(player, "HandleDashRequested");
            yield return null;
            Assert.That(player.ActionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Dash),
                "玩家未进入冲刺，无法验证无敌窗口");
            Assert.That(player.IsDamageImmune, Is.True, "冲刺 Ability 没有授予无敌标签");

            var rejected = player.TryTakeDamage(1, player.transform.position - player.transform.forward, "测试投射物");
            Assert.That(rejected.Accepted, Is.False, "冲刺期间的伤害没有被统一入口拒绝");
            Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore));

            yield return new WaitForSeconds(player.DashDuration + 0.05f);
            Assert.That(player.IsDamageImmune, Is.False, "冲刺结束后无敌标签仍然残留");
        }

        [UnityTest]
        public IEnumerator EnemyProjectile_IgnoresOwnerHitsPlayerOnceAndExpires()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到投射物目标");
            var playerPosition = player.transform.position;

            // 本用例只验证单个投射物的命中次数与生命周期，先停用关卡内自主运行的敌人，
            // 避免它们在投射物飞行期间通过接触或攻击额外扣血，造成跨系统测试污染。
            foreach (var sceneEnemy in Object.FindObjectsByType<Enemy>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                sceneEnemy.gameObject.SetActive(false);
            }

            var ownerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ownerObject.name = "投射物测试发射者";
            ownerObject.transform.position = playerPosition - Vector3.forward * 2f;
            var owner = ownerObject.AddComponent<Enemy>();
            // 发射者只作为友军过滤与来源坐标，不应在本用例中自行运行行为树并追加近战伤害。
            owner.enabled = false;

            var projectileObject = new GameObject("投射物命中测试");
            projectileObject.transform.position = ownerObject.transform.position + Vector3.up * 0.9f;
            var projectile = projectileObject.AddComponent<EnemyProjectile>();
            var targetDirection = player.transform.position + Vector3.up * 0.9f - projectileObject.transform.position;
            projectile.Initialize(owner, targetDirection, 20f, 1, 1f);
            var healthBefore = player.CurrentHealth;

            for (var frame = 0; frame < 30 && projectileObject != null; frame++)
            {
                yield return null;
            }

            Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore - 1),
                "投射物没有忽略发射者并对玩家结算一次伤害");

            var expiringObject = new GameObject("投射物超时测试");
            expiringObject.transform.position = playerPosition + Vector3.up * 20f;
            var expiring = expiringObject.AddComponent<EnemyProjectile>();
            expiring.Initialize(owner, Vector3.up, 1f, 1, 0.02f);
            Object.Destroy(ownerObject);
            yield return null;
            yield return new WaitForSeconds(0.05f);
            Assert.That(expiring == null || expiring.IsResolved, Is.True,
                "发射者死亡后，投射物没有继续推进自己的超时销毁生命周期");

            if (projectileObject != null) Object.Destroy(projectileObject);
            if (expiringObject != null) Object.Destroy(expiringObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SinglePlayerEncounter_TwoGroupsPatrolStartAndCompleteIndependently()
        {
            var encounters = Object.FindObjectsByType<CombatEncounterController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderBy(encounter => encounter.DisplayName)
                .ToArray();
            Assert.That(encounters.Length, Is.EqualTo(2), "场景应包含位于不同位置的两组独立遭遇");
            var encounterCenters = encounters
                .Select(encounter => encounter.Participants.Aggregate(
                    Vector3.zero,
                    (sum, enemy) => sum + enemy.transform.position) / encounter.Participants.Count)
                .ToArray();
            Assert.That(Vector3.Distance(encounterCenters[0], encounterCenters[1]),
                Is.GreaterThan(10f),
                "两组遭遇仍堆在地图同一区域，没有形成分段玩法");
            yield return null;
            Assert.That(encounters.All(encounter =>
                    encounter.State == Odyssey.Gameplay.Encounters.CombatEncounterState.Active),
                Is.True,
                "战区统计没有在场景开始后进入 Active");
            Assert.That(encounters.Select(encounter => encounter.SequenceIndex),
                Is.EqualTo(new[] { 0, 1 }),
                "战区没有固化唯一且稳定的关卡顺序");
            var player = Object.FindFirstObjectByType<PlayerController>();

            // 关闭玩家目标，确保六只怪物都处于纯巡逻分支，而不是被感知或攻击逻辑干扰。
            player.gameObject.SetActive(false);
            var patrolEnemies = encounters.SelectMany(encounter => encounter.Participants).ToArray();
            var patrolStartPositions = patrolEnemies
                .Select(enemy => enemy.transform.position)
                .ToArray();
            yield return new WaitForSeconds(2.5f);
            for (var index = 0; index < patrolEnemies.Length; index++)
            {
                var enemy = patrolEnemies[index];
                var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                var animator = enemy.GetComponent<Animator>();
                Assert.That(animator.applyRootMotion, Is.False,
                    $"怪物“{enemy.name}”仍由 Root Motion 覆盖 NavMeshAgent 位移");
                Assert.That(agent.updatePosition && agent.updateRotation, Is.True,
                    $"怪物“{enemy.name}”没有让 NavMeshAgent 驱动 Transform");
                Assert.That(Vector3.Distance(enemy.transform.position, patrolStartPositions[index]),
                    Is.GreaterThan(0.25f),
                    $"怪物“{enemy.name}”只播放跑步动画但没有在巡逻点之间产生实际位移");
                Assert.That(agent.pathStatus, Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete),
                    $"怪物“{enemy.name}”的巡逻目标不在同一块可达 NavMesh");
            }

            foreach (var encounter in encounters)
            {
                Assert.That(encounter.Participants.Count, Is.EqualTo(3),
                    $"{encounter.DisplayName}应由两名近战怪和一名远程怪组成");
                var completionCount = 0;
                encounter.EncounterCompleted += () => completionCount++;

                yield return null;
                foreach (var enemy in encounter.Participants)
                {
                    var route = enemy.GetComponent<EnemyPatrolRoute>();
                    Assert.That(route, Is.Not.Null, $"遭遇参与者“{enemy.name}”缺少巡逻路线组件");
                    Assert.That(route.HasValidRoute, Is.True,
                        $"遭遇参与者“{enemy.name}”没有有效巡逻点");
                    Assert.That(route.PatrolPoints.Count, Is.EqualTo(6));
                    Assert.That(route.MaximumPointDistance, Is.GreaterThanOrEqualTo(12f),
                        $"遭遇参与者“{enemy.name}”的巡逻网络没有覆盖主要战区");
                    foreach (var patrolPoint in route.PatrolPoints)
                    {
                        var queryFilter = new UnityEngine.AI.NavMeshQueryFilter
                        {
                            agentTypeID = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().agentTypeID,
                            areaMask = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().areaMask
                        };
                        Assert.That(UnityEngine.AI.NavMesh.FindClosestEdge(
                                patrolPoint.position,
                                out var edge,
                                queryFilter),
                            Is.True,
                            $"巡逻点“{patrolPoint.name}”不在有效 NavMesh 上");
                        Assert.That(edge.distance, Is.GreaterThanOrEqualTo(1.8f),
                            $"巡逻点“{patrolPoint.name}”仍贴近墙角或狭窄边缘");
                    }
                    var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    Assert.That(agent.enabled && agent.isOnNavMesh, Is.True,
                        $"遭遇参与者“{enemy.name}”没有稳定绑定到 NavMesh");
                    if (enemy.ConfigId == "spitter")
                    {
                        Assert.That(enemy.AttackMode, Is.EqualTo(EnemyAttackMode.Projectile),
                            "Spitter 没有在战斗前完成远程配置装配");
                    }

                    enemy.TakeDamage(enemy.CurrentHealth);
                }

                yield return null;
                Assert.That(completionCount, Is.EqualTo(1),
                    $"{encounter.DisplayName}全部敌人击败后没有且仅完成一次");
                Assert.That(encounter.RemainingEnemies, Is.Zero);
            }

            Assert.That(GameObject.Find("蓝色战斗出口_击败本组敌人后开启"), Is.Null,
                "场景仍残留没有玩法语义的蓝色测试板");
        }

        [UnityTest]
        public IEnumerator FirstEncounterGate_RequiresClearThenFreshPressurePlateEntry()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var encounter = Object.FindObjectsByType<CombatEncounterController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.DisplayName == "第一战区");
            var plate = Object.FindFirstObjectByType<EncounterClearancePressurePlate>();
            var gate = Object.FindFirstObjectByType<EncounterClearanceGate>();
            Assert.That(plate, Is.Not.Null, "场景没有用项目自有组件接管原踏板");
            Assert.That(gate, Is.Not.Null, "场景没有用项目自有组件接管原隔离门");
            Assert.That(Object.FindObjectsByType<EncounterClearancePressurePlate>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(1),
                "场景残留多套清怪踏板，旧引用可能绕过第一战区条件");
            Assert.That(Object.FindObjectsByType<EncounterClearanceGate>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(1),
                "场景残留多套隔离门控制器，旧门禁可能提前开门");
            Assert.That(GetHierarchyPath(plate.transform), Does.EndWith("ExampleLevel/Mechanism1/PressurePad"),
                "项目门禁没有接管主流程 Mechanism1 踏板");
            Assert.That(GetHierarchyPath(gate.transform), Does.EndWith("ExampleLevel/FinalBuilding/DoorHuge"),
                "项目门禁没有接管通往第二战区的 FinalBuilding 隔离门");
            Assert.That(plate.RequiredEncounter, Is.SameAs(encounter),
                "清怪踏板没有稳定绑定第一战区，可能错误统计另一组怪物");

            var plateCollider = plate.GetComponent<Collider>();
            Assert.That(plateCollider, Is.Not.Null, "项目门禁没有挂在原踏板 Trigger 上");
            AssertThirdPartyCommandComponentsDisabled(plate.transform, gate.transform);

            var closedPosition = gate.CurrentMovingPartLocalPosition;
            Assert.That(closedPosition, Is.EqualTo(gate.ClosedLocalPosition),
                "隔离门进入场景时没有强制恢复关闭姿态");
            MovePlayer(player, plateCollider.bounds.center + Vector3.up * 0.2f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.4f);
            Assert.That(gate.IsOpening || gate.IsOpen, Is.False, "清怪前踩踏板错误开启了项目门禁");
            Assert.That(gate.CurrentMovingPartLocalPosition, Is.EqualTo(closedPosition),
                "原 3D Game Kit 踏板命令仍在旁路移动隔离门");

            foreach (var enemy in encounter.Participants)
            {
                enemy.TakeDamage(enemy.CurrentHealth);
            }

            yield return null;
            Assert.That(encounter.State, Is.EqualTo(Odyssey.Gameplay.Encounters.CombatEncounterState.Completed));
            Assert.That(gate.IsOpening || gate.IsOpen, Is.False, "玩家站在板上清怪时隔离门自动开启");

            MovePlayer(player, plateCollider.bounds.center + Vector3.right * 5f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            MovePlayer(player, plateCollider.bounds.center + Vector3.up * 0.2f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(gate.IsOpening, Is.True, "清怪后重新踩入踏板没有开启隔离门");
        }

        [UnityTest]
        public IEnumerator SceneEnemy_TransitionsFromPatrolToChaseAndAttack()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var encounter = Object.FindObjectsByType<CombatEncounterController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderBy(candidate => candidate.DisplayName)
                .First();
            var enemy = encounter.Participants.First(candidate => candidate.ConfigId == "chomper");
            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Assert.That(agent.enabled && agent.isOnNavMesh, Is.True, "场景近战怪没有绑定 NavMesh");

            var chaseDirection = Vector3.ProjectOnPlane(player.transform.position - enemy.transform.position, Vector3.up);
            if (chaseDirection.sqrMagnitude < 0.01f)
            {
                chaseDirection = enemy.transform.forward;
            }

            var queryFilter = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };
            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(
                    enemy.transform.position + chaseDirection.normalized * 5f,
                    out var chasePoint,
                    4f,
                    queryFilter),
                Is.True,
                "近战怪附近找不到用于追击回归的玩家落点");
            MovePlayer(player, chasePoint.position);
            yield return new WaitForSeconds(0.15f);
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Chase),
                "玩家进入追击范围后，场景怪物没有从 Patrol 切换到 Chase");
            var distanceBeforeChase = Vector3.Distance(enemy.transform.position, player.transform.position);
            yield return new WaitForSeconds(0.6f);
            Assert.That(agent.hasPath, Is.True, "Chase 目标已经产生，但 NavMeshAgent 没有提交追击路径");
            Assert.That(Vector3.Distance(enemy.transform.position, player.transform.position),
                Is.LessThan(distanceBeforeChase - 0.1f),
                "怪物进入 Chase 后没有实际缩短与玩家的距离");

            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(
                    enemy.transform.position + chaseDirection.normalized * 1.8f,
                    out var attackPoint,
                    2f,
                    queryFilter),
                Is.True,
                "近战怪附近找不到用于攻击回归的玩家落点");
            MovePlayer(player, attackPoint.position);
            var healthBeforeAttack = player.CurrentHealth;
            yield return new WaitForSeconds(0.15f);
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Attack),
                "玩家进入攻击范围后，场景怪物没有从 Chase 切换到 Attack");
            yield return new WaitForSeconds(0.7f);
            Assert.That(player.CurrentHealth, Is.EqualTo(healthBeforeAttack),
                "玩家未与怪物身体碰撞体重叠时，攻击动画或距离判断仍然造成了伤害");

            var bodyCollider = enemy.GetComponentsInChildren<Collider>(true)
                .First(candidate => !candidate.isTrigger && candidate.enabled);
            MovePlayer(player, bodyCollider.bounds.center - player.transform.rotation * player.Controller.center);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.CurrentHealth, Is.LessThan(healthBeforeAttack),
                "玩家与怪物身体碰撞体重叠后没有立即受到接触伤害");

            MovePlayer(player, enemy.transform.position + Vector3.forward * (enemy.ForgetRange + 3f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Patrol),
                "玩家脱离丢失范围后，怪物没有放弃目标并恢复巡逻");
        }

        [UnityTest]
        public IEnumerator PlayerAttack_AnimationWindowDamagesEachEnemyOnlyOnce()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");

            var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enemyObject.name = "动画命中窗口测试敌人";
            enemyObject.layer = 23;
            enemyObject.transform.position = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.5f;
            var enemy = enemyObject.AddComponent<Enemy>();

            InvokePlayerCommand(player, "HandleAttackRequested");
            yield return null;
            player.MeleeAttackStart();
            yield return null;
            Assert.That(enemy.CurrentHealth, Is.EqualTo(2), "动画命中窗口未造成一次伤害");

            player.MeleeAttackStart();
            yield return null;
            Assert.That(enemy.CurrentHealth, Is.EqualTo(2), "同一段攻击对同一敌人重复结算了伤害");
            player.MeleeAttackEnd();

            Object.Destroy(enemyObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerFootPlacement_OnlyOwnsGroundedFreePresentation()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            var placement = player == null
                ? null
                : player.GetComponent<Odyssey.Characters.Player.PlayerFootPlacementController>();
            Assert.That(placement, Is.Not.Null, "玩家缺少脚部贴地控制器");

            yield return new WaitForSeconds(0.15f);
            Assert.That(placement.CurrentWeight, Is.GreaterThan(0.9f), "地面空闲时脚部 Rig 未启用");

            var bones = player.GetComponentsInChildren<Transform>(true);
            var leftFoot = System.Array.Find(bones, transform => transform.name == "Ellen_Left_Foot");
            var rightFoot = System.Array.Find(bones, transform => transform.name == "Ellen_Right_Foot");
            Assert.That(leftFoot, Is.Not.Null, "未找到 Ellen 左脚骨骼");
            Assert.That(rightFoot, Is.Not.Null, "未找到 Ellen 右脚骨骼");
            Assert.That(Vector3.Distance(leftFoot.position, rightFoot.position), Is.GreaterThan(0.4f),
                "脚部 Target 把双腿向身体中心拉拢，站立姿势已经变形");

            foreach (var constraint in player.GetComponentsInChildren<TwoBoneIKConstraint>(true))
            {
                Assert.That(constraint.data.targetRotationWeight, Is.EqualTo(0f),
                    $"{constraint.name} 仍在覆盖 Generic 脚骨旋转");
            }

            SetMovementValue(player, Vector2.up);
            yield return new WaitForSeconds(0.2f);
            Assert.That(placement.CurrentWeight, Is.LessThan(0.1f), "移动期间脚部 Rig 仍在锁死走跑动画");
            SetMovementValue(player, Vector2.zero);
            yield return new WaitForSeconds(0.35f);
            Assert.That(placement.CurrentWeight, Is.GreaterThan(0.9f), "停止移动后脚部 Rig 未恢复站立贴地");

            InvokePlayerCommand(player, "HandleAttackRequested");
            yield return new WaitForSeconds(0.15f);
            Assert.That(placement.CurrentWeight, Is.LessThan(0.1f), "攻击期间脚部 Rig 仍在覆盖动作姿势");
        }

        [UnityTest]
        public IEnumerator PlayerWallClearance_PushesIdleSilhouetteOutOfWall()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "墙边安全半径测试墙";
            wall.layer = 16;
            wall.transform.localScale = new Vector3(0.1f, 3f, 3f);
            wall.transform.position = player.transform.position + player.transform.right * 0.5f + Vector3.up;
            var distanceBefore = Vector3.Distance(player.transform.position, wall.transform.position);

            yield return null;
            var distanceAfter = Vector3.Distance(player.transform.position, wall.transform.position);
            Assert.That(player.WallClearanceActive, Is.True, "墙面进入安全半径后未激活常态保护");
            Assert.That(distanceAfter, Is.GreaterThan(distanceBefore + 0.005f),
                "常态墙边保护未把 Ellen 手臂轮廓推出墙面");

            Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerEnvironmentSensor_StompsAtHighSpeedAndDetectsNearbyWall()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            var originalPosition = player.transform.position;

            SetJumpPressed(player, true);
            yield return new WaitForSeconds(0.05f);
            SetJumpPressed(player, false);
            yield return null;
            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Airborne),
                "踩踏测试开始前玩家未进入空中状态");

            var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enemyObject.name = "高速踩踏测试敌人";
            enemyObject.layer = FindFirstLayer(player.EnemyLayer);
            enemyObject.transform.localScale = new Vector3(1f, 0.5f, 1f);
            enemyObject.transform.position = originalPosition + player.transform.right * 2f + Vector3.up * 0.25f;
            var enemy = enemyObject.AddComponent<Enemy>();
            enemy.enabled = false;

            player.Controller.enabled = false;
            // 脚底离敌人顶部仅保留 5 厘米，确保测试不依赖 Test Runner 是否限帧；
            // 高速覆盖由 -20m/s 速度参与动态扫描距离这一事实单独验证。
            player.transform.position = enemyObject.transform.position + Vector3.up * 0.3f;
            player.Controller.enabled = true;
            player.VerticalVelocity = -20f;
            yield return null;

            Assert.That(enemy.CurrentHealth, Is.EqualTo(2),
                "动态脚底扫描没有在高速下落时命中敌人顶部");
            Assert.That(player.VerticalVelocity, Is.GreaterThan(0f),
                "踩踏伤害被接受后玩家没有向上反弹");

            Object.Destroy(enemyObject);
            yield return null;

            var moveDirection = Vector3.ProjectOnPlane(player.MainCameraTransform.forward, Vector3.up).normalized;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "胶囊墙面感知测试墙";
            wall.layer = FindFirstLayer(player.WallLayer);
            wall.transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            wall.transform.localScale = new Vector3(3f, 3f, 0.1f);

            player.Controller.enabled = false;
            player.transform.position = originalPosition + Vector3.up * 1.5f;
            player.Controller.enabled = true;
            wall.transform.position = player.transform.position +
                                      moveDirection * (player.Controller.radius + 0.1f) +
                                      Vector3.up;
            player.VerticalVelocity = -5f;
            SetMovementValue(player, Vector2.up);
            for (var frame = 0;
                 frame < 10 &&
                 player.LocomotionState != Odyssey.Gameplay.Characters.PlayerLocomotionStateId.WallSlide;
                 frame++)
            {
                yield return null;
            }

            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.WallSlide),
                "胶囊墙面感知没有在玩家下降并朝墙移动时进入 WallSlide");

            SetMovementValue(player, Vector2.zero);
            Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerAnimator_DrivesJumpApexAndFallWithoutTransitions()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            Assert.That(player.InputReader, Is.Not.Null, "玩家缺少输入适配器");
            var placement = player.GetComponent<Odyssey.Characters.Player.PlayerFootPlacementController>();
            Assert.That(placement, Is.Not.Null, "玩家缺少脚部贴地控制器");

            SetJumpPressed(player, true);
            yield return new WaitForSeconds(0.1f);
            SetJumpPressed(player, false);
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Airborne));
            AssertAnimatorState(player.Animator, "Airborne");
            Assert.That(player.Animator.GetFloat("VerticalSpeed"), Is.GreaterThan(0f));

            player.VerticalVelocity = -1f;
            yield return null;
            yield return new WaitForFixedUpdate();
            AssertAnimatorState(player.Animator, "Airborne");
            Assert.That(player.Animator.GetFloat("VerticalSpeed"), Is.LessThan(0f));

            player.VerticalVelocity = -8f;
            SetJumpPressed(player, false);
            for (var frame = 0;
                 frame < 180 &&
                 player.LocomotionState != Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Grounded;
                 frame++)
            {
                yield return null;
            }

            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Grounded),
                "玩家落地后仍停留在空中移动状态");
            yield return null;
            AssertAnimatorState(player.Animator, "Landing");
            Assert.That(placement.CurrentWeight, Is.LessThan(0.1f),
                "Landing 姿势尚未结束时脚部 Rig 提前锁定了双脚 Target");
            // Landing 本体与回到 Locomotion 的交叉淡化合计约 0.4 秒；再留出约 0.15 秒验证脚部 Rig 平滑淡入。
            yield return new WaitForSeconds(0.55f);
            AssertAnimatorState(player.Animator, "Locomotion");
            Assert.That(placement.CurrentWeight, Is.GreaterThan(0.9f),
                "回到稳定 Locomotion 后脚部 Rig 未重新校准并启用");

            var bones = player.GetComponentsInChildren<Transform>(true);
            var leftFoot = System.Array.Find(bones, transform => transform.name == "Ellen_Left_Foot");
            var rightFoot = System.Array.Find(bones, transform => transform.name == "Ellen_Right_Foot");
            Assert.That(Vector3.Distance(leftFoot.position, rightFoot.position), Is.GreaterThan(0.4f),
                "Landing 返回 Locomotion 后脚部 Target 仍锁在落地坐标，导致双腿劈叉");
        }

        [UnityTest]
        public IEnumerator PlayerAnimator_DeathAndRespawnRestoreAllRuntimeStates()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            player.RespawnDelay = 0.05f;

            player.TakeDamage(player.CurrentHealth, player.transform.position - player.transform.forward);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.enabled, Is.False, "死亡后玩家控制器仍在执行玩法更新");
            AssertAnimatorState(player.Animator, "EllenDeath");

            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            Assert.That(player.enabled, Is.True, "复活后玩家控制器未重新启用");
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
            Assert.That(player.ActionState, Is.EqualTo(Odyssey.Gameplay.Characters.PlayerActionStateId.Free));
            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Grounded));
            AssertAnimatorState(player.Animator, "Locomotion");
        }

        [UnityTest]
        public IEnumerator EnemyRuntime_TransitionsThroughChaseAttackAndTargetLoss()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到 AI 感知目标");

            var root = new GameObject("运行时 AI 测试敌人");
            root.transform.position = player.transform.position + Vector3.forward * 5f;
            var enemy = root.AddComponent<Enemy>();

            yield return null;
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Chase));

            root.transform.position = player.transform.position + Vector3.forward;
            yield return null;
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Attack));

            root.transform.position = player.transform.position + Vector3.forward * 20f;
            yield return null;
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Idle),
                "无巡逻路线的测试怪物在目标丢失后没有回到待机");

            Object.Destroy(root);
            yield return null;
        }

        /// <summary>
        /// 接受当前状态或正在交叉淡化的下一状态，验证代码驱动目标已经提交给 Animator。
        /// </summary>
        private static void AssertAnimatorState(Animator animator, string expectedState, string context = null)
        {
            var expectedHash = Animator.StringToHash(expectedState);
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            Assert.That(
                current.shortNameHash == expectedHash || next.shortNameHash == expectedHash,
                Is.True,
                $"{context ?? "动画状态检查"}：Animator 未进入或过渡到状态“{expectedState}”。" +
                $"当前哈希={current.shortNameHash}，下一状态哈希={next.shortNameHash}，" +
                $"是否过渡={animator.IsInTransition(0)}，当前进度={current.normalizedTime:F2}。");
        }

        private static void MovePlayer(PlayerController player, Vector3 position)
        {
            player.Controller.enabled = false;
            player.transform.position = position;
            player.Controller.enabled = true;
        }

        private static void AssertThirdPartyCommandComponentsDisabled(
            Transform plate,
            Transform gate)
        {
            var blockedTypes = new[]
            {
                "Gamekit3D.GameCommands.SendOnTriggerEnter",
                "Gamekit3D.InteractOnTrigger",
                "Gamekit3D.GameCommands.SimpleTranslator",
                "Gamekit3D.GameCommands.GameCommandReceiver"
            };
            var nearbyBehaviours = Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(behaviour => behaviour != null &&
                                    blockedTypes.Contains(behaviour.GetType().FullName) &&
                                    (Vector3.Distance(behaviour.transform.position, plate.position) < 8f ||
                                     Vector3.Distance(behaviour.transform.position, gate.position) < 8f));
            foreach (var behaviour in nearbyBehaviours)
            {
                Assert.That(behaviour.enabled, Is.False,
                    $"第三方组件“{behaviour.GetType().FullName}”仍可能绕过清怪条件触发门");
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

        private static void SetJumpPressed(PlayerController player, bool pressed)
        {
            var field = player.InputReader.GetType().GetField(
                "<IsJumpPressed>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "输入适配器的跳跃快照字段不存在");
            field.SetValue(player.InputReader, pressed);
        }

        private static void SetMovementValue(PlayerController player, Vector2 value)
        {
            var field = player.InputReader.GetType().GetField(
                "<MovementValue>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "输入适配器的移动快照字段不存在");
            field.SetValue(player.InputReader, value);
        }

        private static void InvokePlayerCommand(PlayerController player, string methodName)
        {
            var method = typeof(PlayerController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到玩家命令入口：{methodName}");
            method.Invoke(player, null);
        }

        private static int FindFirstLayer(LayerMask mask)
        {
            for (var layer = 0; layer < 32; layer++)
            {
                if ((mask.value & (1 << layer)) != 0)
                {
                    return layer;
                }
            }

            Assert.Fail("测试所需的 LayerMask 未配置任何层");
            return 0;
        }
    }
}
