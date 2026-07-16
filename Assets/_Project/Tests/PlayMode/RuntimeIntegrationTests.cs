using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.AI;
using Odyssey.Systems;
using Odyssey.Unity.UI;
using UnityEngine;
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
        }

        [UnityTearDown]
        public IEnumerator RestoreGlobalState()
        {
            Time.timeScale = 1f;
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

            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseFacade_AtomicallyUpdatesPanelAndTimeScale()
        {
            var saveManager = Object.FindFirstObjectByType<SaveManager>();
            var pauseMenu = saveManager == null
                ? null
                : saveManager.transform.Find("PauseMenu")?.gameObject;

            Assert.That(saveManager, Is.Not.Null, "场景中未找到暂停与存档门面");
            Assert.That(pauseMenu, Is.Not.Null, "场景中未找到暂停面板");

            saveManager.PauseGame();
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(pauseMenu.activeSelf, Is.True);

            saveManager.ResumeGame();
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(pauseMenu.activeSelf, Is.False);
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
        public IEnumerator PlayerAnimator_DrivesJumpApexAndFallWithoutTransitions()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "场景中未找到玩家");
            Assert.That(player.InputReader, Is.Not.Null, "玩家缺少输入适配器");

            SetJumpPressed(player, true);
            yield return new WaitForSeconds(0.1f);
            SetJumpPressed(player, false);
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.That(player.LocomotionState,
                Is.EqualTo(Odyssey.Gameplay.Characters.PlayerLocomotionStateId.Airborne));
            AssertAnimatorState(player.Animator, "EllenJumpGoesUp");

            player.VerticalVelocity = -1f;
            yield return null;
            yield return new WaitForFixedUpdate();
            AssertAnimatorState(player.Animator, "EllenJumpGoesDown");

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
            yield return new WaitForFixedUpdate();
            AssertAnimatorState(player.Animator, "Locomotion");
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
        public IEnumerator EnemyRuntime_TransitionsThroughChaseAttackAndRetreat()
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

            enemy.TakeDamage(2);
            yield return new WaitForSeconds(0.6f);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(1));
            Assert.That(enemy.CurrentGoal, Is.EqualTo(EnemyGoal.Retreat),
                "默认三点生命敌人在剩余一点生命时未进入撤退目标");

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

        private static void SetJumpPressed(PlayerController player, bool pressed)
        {
            var field = player.InputReader.GetType().GetField(
                "<IsJumpPressed>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "输入适配器的跳跃快照字段不存在");
            field.SetValue(player.InputReader, pressed);
        }

        private static void InvokePlayerCommand(PlayerController player, string methodName)
        {
            var method = typeof(PlayerController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到玩家命令入口：{methodName}");
            method.Invoke(player, null);
        }
    }
}
