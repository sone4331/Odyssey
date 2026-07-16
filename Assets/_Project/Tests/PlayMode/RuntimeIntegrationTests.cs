using System.Collections;
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
    }
}
