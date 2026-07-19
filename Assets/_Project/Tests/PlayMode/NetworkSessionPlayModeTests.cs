using System.Collections;
using System.Linq;
using NUnit.Framework;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Encounters;
using Odyssey.Networking;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// 在真实 Level_01 中验证 Host 启动、Owner 生成以及 AI 与关卡权威组件解冻。
    /// 采用单进程 Host 冒烟测试覆盖运行时装配；双进程数据复制由 Windows 双开验收验证，避免引入额外测试框架。
    /// </summary>
    public sealed class NetworkSessionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLevelWithoutStartingSession()
        {
            Time.timeScale = 1f;
            var operation = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ShutdownSession()
        {
            Time.timeScale = 1f;
            var session = Object.FindFirstObjectByType<GameplaySessionController>();
            if (session != null)
            {
                session.Shutdown();
                Object.Destroy(session.gameObject);
            }

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupMenu_IsClickableAndFreezesGameplayBeforeModeSelection()
        {
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            Assert.That(menu, Is.Not.Null, "Level_01 缺少全屏主菜单");
            Assert.That(menu.CurrentPage, Is.EqualTo(GameMenuPage.MainMenu));
            Assert.That(menu.IsMenuVisible, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Cursor.visible, Is.True);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null,
                "EventSystem 没有使用新输入系统 UI 模块");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostSession_SpawnsOwnerAndEnablesOnlyHostGameplayAuthority()
        {
            var session = Object.FindFirstObjectByType<GameplaySessionController>();
            Assert.That(session, Is.Not.Null, "Level_01 缺少合作会话入口");
            Assert.That(Object.FindFirstObjectByType<PlayerController>(), Is.Null,
                "会话开始前不应存在固定玩家");

            Assert.That(session.StartHost(), Is.True, "Host 启动失败，可能是 UDP 7777 被占用");
            for (var frame = 0;
                 frame < 180 && Object.FindFirstObjectByType<PlayerController>() == null;
                 frame++)
            {
                yield return null;
            }

            yield return null;
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            var playerAdapter = player == null ? null : player.GetComponent<NetworkPlayerAdapter>();
            Assert.That(session.Mode, Is.EqualTo(GameplaySessionMode.Host));
            Assert.That(session.Manager.NetworkConfig.NetworkTransport, Is.TypeOf<UnityTransport>());
            Assert.That(session.ConnectedPlayerCount, Is.EqualTo(1));
            Assert.That(player, Is.Not.Null, "Host 没有生成本机 Owner");
            Assert.That(playerAdapter, Is.Not.Null);
            Assert.That(playerAdapter.IsOwner, Is.True);
            Assert.That(playerAdapter.IsServer, Is.True);
            Assert.That(player.enabled, Is.True, "本机 Owner 没有启用原玩家控制器");

            var enemies = Object.FindObjectsByType<NetworkEnemyAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(6));
            Assert.That(enemies.All(adapter => adapter.IsSpawned && adapter.IsServer), Is.True,
                "六只怪物没有全部成为 Host 权威场景对象");
            Assert.That(enemies.All(adapter => adapter.GetComponent<Enemy>().enabled), Is.True,
                "Host 启动后行为树没有解冻");
            Assert.That(enemies.All(adapter =>
                    adapter.GetComponent<NavMeshAgent>() == null || adapter.GetComponent<NavMeshAgent>().enabled),
                Is.True,
                "Host 启动后怪物导航没有恢复");

            var encounters = Object.FindObjectsByType<NetworkEncounterAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(encounters.Length, Is.EqualTo(2));
            Assert.That(encounters.All(adapter => adapter.IsSpawned && adapter.IsServer), Is.True);
            Assert.That(Object.FindFirstObjectByType<EncounterClearancePressurePlate>().enabled, Is.True,
                "Host 没有取得踏板条件判断权");
        }

        [UnityTest]
        public IEnumerator HostAuthority_ContactWithEnemyDamagesPlayerOnlyOnceDuringProtectionWindow()
        {
            var session = Object.FindFirstObjectByType<GameplaySessionController>();
            Assert.That(session.StartHost(), Is.True, "Host 启动失败，无法验证接触伤害");
            for (var frame = 0;
                 frame < 180 && Object.FindFirstObjectByType<PlayerController>() == null;
                 frame++)
            {
                yield return null;
            }

            var player = Object.FindFirstObjectByType<PlayerController>();
            var playerAdapter = player == null ? null : player.GetComponent<NetworkPlayerAdapter>();
            var enemy = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .First(candidate => candidate.AttackDamage > 0 && candidate.CurrentHealth > 0);
            Assert.That(playerAdapter, Is.Not.Null);

            // 关闭双方玩法更新，只保留 Collider 和 NetworkPlayerAdapter，确保扣血确实来自 Host 接触复核，
            // 而不是本机 PlayerController.OnControllerColliderHit 或怪物行为树的咬击时序。
            player.enabled = false;
            enemy.enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            player.Controller.enabled = false;
            player.transform.position = enemy.transform.position;
            player.Controller.enabled = true;
            Physics.SyncTransforms();
            var healthBeforeContact = playerAdapter.CurrentHealth;

            yield return new WaitForSeconds(0.35f);

            Assert.That(playerAdapter.CurrentHealth, Is.EqualTo(healthBeforeContact - enemy.AttackDamage),
                "Host 没有结算怪物接触伤害，或保护期内发生了重复扣血");
        }
    }
}
