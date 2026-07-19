using System.Collections;
using System.Linq;
using NUnit.Framework;
using Odyssey.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// 在真实 NetworkArena 中启动一次 Host，验证传输、连接审批、玩家生成与服务器权威组件能够形成最小运行闭环。
    /// 测试结束后主动关闭会话并恢复单机场景，避免 NetworkManager.Singleton 污染其他 PlayMode 用例。
    /// </summary>
    public sealed class NetworkArenaIntegrationTests
    {
        private const string ScenePath = "Assets/_Project/Content/Scenes/NetworkArena.unity";

        [UnitySetUp]
        public IEnumerator LoadNetworkArena()
        {
            var operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ShutdownAndRestoreGameplayScene()
        {
            var session = Object.FindFirstObjectByType<NetworkSessionController>();
            session?.Shutdown();
            yield return null;

            var operation = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Host_StartsAndSpawnsOneAuthoritativePlayer()
        {
            var session = Object.FindFirstObjectByType<NetworkSessionController>();
            Assert.That(session, Is.Not.Null, "联机场景缺少会话门面");
            Assert.That(session.Manager.NetworkConfig.EnableSceneManagement, Is.False, "独立竞技场不应启用网络场景切换");
            Assert.That(session.Manager.NetworkConfig.PlayerPrefab, Is.Not.Null, "NetworkManager 未注册玩家 Prefab");

            Assert.That(session.StartHost(), Is.True, "Host 启动失败，可能存在端口占用或传输配置错误");
            for (var frame = 0; frame < 120 && !Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsSortMode.None).Any(); frame++)
            {
                yield return null;
            }

            var players = Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsSortMode.None);
            Assert.That(session.Manager.IsHost, Is.True);
            Assert.That(session.ConnectedPlayerCount, Is.EqualTo(1));
            Assert.That(players, Has.Length.EqualTo(1), "Host 连接后没有自动生成权威玩家");
            Assert.That(players[0].IsServer, Is.True);
            Assert.That(players[0].IsOwner, Is.True);
            Assert.That(players[0].CurrentHealth, Is.EqualTo(players[0].MaxHealth));
            Assert.That(NetworkManager.Singleton, Is.SameAs(session.Manager));
        }
    }
}
