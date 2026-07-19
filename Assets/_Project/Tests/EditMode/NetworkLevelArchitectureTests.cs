using System.Linq;
using NUnit.Framework;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Encounters;
using Odyssey.Networking;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Odyssey.Tests
{
    /// <summary>
    /// 验证原关卡合作联机的静态装配和 Host 攻击规则，避免构建工具遗漏组件或网络校验在重构后失效。
    /// 采用资产契约测试与纯规则测试；只检查作品集实际需要的边界，不搭建通用网络测试框架。
    /// </summary>
    public sealed class NetworkLevelArchitectureTests
    {
        private const string ScenePath = "Assets/_Project/Content/Scenes/Level_01.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Content/Prefabs/Network/CoopPlayer.prefab";
        private const string ProjectilePrefabPath = "Assets/_Project/Content/Prefabs/Combat/SpitterProjectile.prefab";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        [Test]
        public void CoopPlayerPrefab_ReusesOriginalPlayerAndContainsOwnerSynchronizationBoundary()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            Assert.That(prefab, Is.Not.Null, "合作玩家 Prefab 未生成");
            Assert.That(prefab.GetComponent<PlayerController>(), Is.Not.Null, "合作玩家没有复用原 PlayerController");
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null, "合作玩家缺少 CharacterController");
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null, "合作玩家缺少 NetworkObject");
            Assert.That(prefab.GetComponent<OwnerAuthoritativeNetworkTransform>(), Is.Not.Null,
                "合作玩家缺少 Owner 权威位移同步");
            var networkAnimator = prefab.GetComponent<OwnerAuthoritativeNetworkAnimator>();
            Assert.That(networkAnimator, Is.Not.Null, "合作玩家缺少 Owner 权威动画同步");
            Assert.That(networkAnimator.Animator, Is.SameAs(prefab.GetComponent<Animator>()),
                "NetworkAnimator 没有绑定实际玩家 Animator");
            Assert.That(prefab.GetComponent<NetworkPlayerAdapter>(), Is.Not.Null, "合作玩家缺少 Host 战斗适配器");
        }

        [Test]
        public void ProjectilePrefab_ContainsServerAuthoritativeNetworkLifecycle()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);

            Assert.That(prefab, Is.Not.Null, "Spitter 投射物 Prefab 不存在");
            Assert.That(prefab.GetComponent<EnemyProjectile>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkProjectileAdapter>(), Is.Not.Null);
        }

        [Test]
        public void Level01_ContainsOneSessionSixNetworkEnemiesTwoEncountersAndOneGate()
        {
            var scene = OpenLevelForInspection(out var shouldClose);
            try
            {
                var roots = scene.GetRootGameObjects();
                var session = roots.SelectMany(root =>
                        root.GetComponentsInChildren<GameplaySessionController>(true))
                    .SingleOrDefault();
                Assert.That(session, Is.Not.Null, "Level_01 缺少场景级会话控制器");

                var manager = session.GetComponent<NetworkManager>();
                Assert.That(manager, Is.Not.Null);
                Assert.That(session.GetComponent<UnityTransport>(), Is.Not.Null);
                Assert.That(session.GetComponent<SinglePlayerTransport>(), Is.Not.Null);
                Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.True, "未启用 NGO 场景同步");
                Assert.That(manager.NetworkConfig.PlayerPrefab,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath)));
                var networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
                Assert.That(manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(networkPrefabs), Is.True,
                    "NetworkManager 没有注册项目自有网络 Prefab 列表");

                var enemyAdapters = roots.SelectMany(root =>
                        root.GetComponentsInChildren<NetworkEnemyAdapter>(true))
                    .ToArray();
                Assert.That(enemyAdapters.Length, Is.EqualTo(6), "原关卡应有六只 Host 权威怪物");
                foreach (var adapter in enemyAdapters)
                {
                    Assert.That(adapter.GetComponent<NetworkObject>(), Is.Not.Null);
                    Assert.That(adapter.GetComponent<NetworkTransform>(), Is.Not.Null);
                    Assert.That(adapter.GetComponent<NetworkAnimator>(), Is.Not.Null);
                    Assert.That(adapter.GetComponent<Enemy>().enabled, Is.False,
                        $"{adapter.name} 在会话开始前没有冻结行为树");
                    var agent = adapter.GetComponent<NavMeshAgent>();
                    Assert.That(agent == null || !agent.enabled, Is.True,
                        $"{adapter.name} 在会话开始前没有冻结导航");
                }

                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<NetworkEncounterAdapter>(true)).Count(),
                    Is.EqualTo(2), "两个原有战区没有全部接入权威快照");
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<NetworkGateAdapter>(true)).Count(),
                    Is.EqualTo(1), "第一战区隔离门没有接入权威状态");
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<PlayerController>(true)).Count(),
                    Is.Zero, "Level_01 仍固定放置玩家，双开时会产生额外角色");
            }
            finally
            {
                if (shouldClose)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void NetworkAttackRules_RejectInvalidCommandsWithExplainableReasons()
        {
            AssertDecision(NetworkAttackRules.Validate(2, 1, true, 2d, 1d, true, 1f, 1.5f),
                true, NetworkAttackRejection.None);
            AssertDecision(NetworkAttackRules.Validate(1, 1, true, 2d, 1d, true, 1f, 1.5f),
                false, NetworkAttackRejection.DuplicateOrExpired);
            AssertDecision(NetworkAttackRules.Validate(2, 1, false, 2d, 1d, true, 1f, 1.5f),
                false, NetworkAttackRejection.AttackerDead);
            AssertDecision(NetworkAttackRules.Validate(2, 1, true, 0.5d, 1d, true, 1f, 1.5f),
                false, NetworkAttackRejection.Cooldown);
            AssertDecision(NetworkAttackRules.Validate(2, 1, true, 2d, 1d, false, 0f, 1.5f),
                false, NetworkAttackRejection.NoTarget);
            AssertDecision(NetworkAttackRules.Validate(2, 1, true, 2d, 1d, true, 2f, 1.5f),
                false, NetworkAttackRejection.OutOfRange);
            Assert.That(NetworkAttackRules.ToChinese(NetworkAttackRejection.OutOfRange),
                Is.EqualTo("目标超出攻击距离"));
        }

        [Test]
        public void NetworkComboValidator_RejectsJumpedAndExpiredComboSegments()
        {
            var valid = new NetworkComboSequenceValidator(0.9d);
            Assert.That(valid.TryAdvance(1, 1d), Is.True);
            Assert.That(valid.TryAdvance(2, 1.2d), Is.True);
            Assert.That(valid.TryAdvance(3, 1.4d), Is.True);
            Assert.That(valid.TryAdvance(4, 1.6d), Is.True);
            Assert.That(valid.ExpectedComboIndex, Is.EqualTo(1));

            var jumped = new NetworkComboSequenceValidator(0.9d);
            Assert.That(jumped.TryAdvance(1, 1d), Is.True);
            Assert.That(jumped.TryAdvance(3, 1.1d), Is.False);
            Assert.That(jumped.ExpectedComboIndex, Is.EqualTo(1));

            var expired = new NetworkComboSequenceValidator(0.9d);
            Assert.That(expired.TryAdvance(1, 1d), Is.True);
            Assert.That(expired.TryAdvance(2, 2d), Is.False);
        }

        [TestCase("127.0.0.1", true)]
        [TestCase("192.168.1.8", true)]
        [TestCase("localhost", false)]
        [TestCase("300.1.1.1", false)]
        public void SessionAddressValidation_AcceptsOnlyIpv4(string value, bool expected)
        {
            Assert.That(GameplaySessionController.IsValidIpv4(value), Is.EqualTo(expected));
        }

        private static Scene OpenLevelForInspection(out bool shouldClose)
        {
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                shouldClose = false;
                return loaded;
            }

            shouldClose = true;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        private static void AssertDecision(
            NetworkAttackDecision decision,
            bool accepted,
            NetworkAttackRejection rejection)
        {
            Assert.That(decision.Accepted, Is.EqualTo(accepted));
            Assert.That(decision.Rejection, Is.EqualTo(rejection));
        }
    }
}
