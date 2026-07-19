using NUnit.Framework;
using Odyssey.Characters.Player;
using Odyssey.Networking;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests
{
    /// <summary>
    /// 验证 Host 权威攻击规则与联机资产边界，防止场景搭建或重构把安全校验退化为客户端直写状态。
    /// 采用纯规则测试加少量资产契约测试，覆盖真正容易回退的边界，不复制 NGO 自身测试套件。
    /// </summary>
    public sealed class NetworkArchitectureTests
    {
        [Test]
        public void AttackRules_AcceptValidRequestAndExplainEveryRejection()
        {
            var accepted = NetworkAttackRules.Validate(2, 1, true, 10d, 9d, true, 1.5f, 2f);
            var duplicate = NetworkAttackRules.Validate(1, 1, true, 10d, 9d, true, 1.5f, 2f);
            var dead = NetworkAttackRules.Validate(2, 1, false, 10d, 9d, true, 1.5f, 2f);
            var cooldown = NetworkAttackRules.Validate(2, 1, true, 8d, 9d, true, 1.5f, 2f);
            var noTarget = NetworkAttackRules.Validate(2, 1, true, 10d, 9d, false, 0f, 2f);
            var outOfRange = NetworkAttackRules.Validate(2, 1, true, 10d, 9d, true, 3f, 2f);

            Assert.That(accepted.Accepted, Is.True);
            Assert.That(duplicate.Rejection, Is.EqualTo(NetworkAttackRejection.DuplicateOrExpired));
            Assert.That(dead.Rejection, Is.EqualTo(NetworkAttackRejection.AttackerDead));
            Assert.That(cooldown.Rejection, Is.EqualTo(NetworkAttackRejection.Cooldown));
            Assert.That(noTarget.Rejection, Is.EqualTo(NetworkAttackRejection.NoTarget));
            Assert.That(outOfRange.Rejection, Is.EqualTo(NetworkAttackRejection.OutOfRange));
            Assert.That(NetworkAttackRules.ToChinese(outOfRange.Rejection), Is.EqualTo("目标超出攻击距离"));
        }

        [Test]
        public void SessionAddress_OnlyAcceptsIpv4ForLightweightLanConnection()
        {
            Assert.That(NetworkSessionController.IsValidAddress("127.0.0.1"), Is.True);
            Assert.That(NetworkSessionController.IsValidAddress("192.168.1.10"), Is.True);
            Assert.That(NetworkSessionController.IsValidAddress("localhost"), Is.False);
            Assert.That(NetworkSessionController.IsValidAddress("300.1.1.1"), Is.False);
        }

        [Test]
        public void SpawnPoints_SeparateHostAndClient()
        {
            Assert.That(NetworkSpawnPoints.Get(0), Is.EqualTo(new Vector3(-4f, 0.05f, 0f)));
            Assert.That(NetworkSpawnPoints.Get(1), Is.EqualTo(new Vector3(4f, 0.05f, 0f)));
            Assert.That(Vector3.Distance(NetworkSpawnPoints.Get(0), NetworkSpawnPoints.Get(1)), Is.GreaterThan(AttackRangeForSpawnSafety));
        }

        [Test]
        public void NetworkPlayerPrefab_HasOnlyRequiredNetworkingBoundary()
        {
            const string path = "Assets/_Project/Content/Prefabs/Network/NetworkPlayer.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, "未生成联机玩家 Prefab");
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null, "联机玩家缺少 NetworkObject");
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null, "联机玩家缺少 Host 权威位置同步");
            Assert.That(prefab.GetComponent<NetworkPlayerAvatar>(), Is.Not.Null, "联机玩家缺少命令与状态适配器");
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null, "Host 缺少碰撞移动组件");
            Assert.That(prefab.GetComponent<PlayerController>(), Is.Null, "联机技术切片不应复用单机控制器写入位置或生命");
            var animator = prefab.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null, "联机玩家根节点缺少 Animator");
            Assert.That(
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo("Assets/_Project/Content/Animations/Player/PlayerAnimator.controller"),
                "联机玩家必须显式使用项目自有动画控制器，不能依赖来源 Prefab 的旧控制器");
        }

        private const float AttackRangeForSpawnSafety = 2.25f;
    }
}
