using System.Collections;
using Odyssey.Characters.Enemies;
using Odyssey.Gameplay.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Odyssey.Networking
{
    /// <summary>
    /// 让原 Enemy 行为树只在 Host 运行，并复制怪物生命、位移、动画、受击反馈与网络销毁。
    /// 采用 Server Authority Adapter；Client 保留 Collider 和表现对象，但不执行 AI、NavMesh 或伤害规则。
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkEnemyAdapter : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _health = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Enemy _enemy;
        private NavMeshAgent _agent;

        public int AuthoritativeHealth => _health.Value;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _agent = GetComponent<NavMeshAgent>();
            if (_enemy != null)
            {
                _enemy.enabled = false;
            }

            if (_agent != null)
            {
                _agent.enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (_enemy == null)
            {
                return;
            }

            _enemy.UsesExternalDespawn = IsServer;
            if (IsServer)
            {
                _health.Value = _enemy.CurrentHealth;
                _enemy.Damaged += HandleDamaged;
                _enemy.Defeated += HandleDefeated;
                _enemy.ProjectileCreated += HandleProjectileCreated;
                _enemy.enabled = true;
            }
            else
            {
                _enemy.enabled = false;
                if (_agent != null)
                {
                    _agent.enabled = false;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_enemy == null)
            {
                return;
            }

            _enemy.Damaged -= HandleDamaged;
            _enemy.Defeated -= HandleDefeated;
            _enemy.ProjectileCreated -= HandleProjectileCreated;
        }

        private void HandleDamaged(Enemy enemy, DamageResult result)
        {
            if (!IsServer || enemy == null || !result.Accepted)
            {
                return;
            }

            _health.Value = enemy.CurrentHealth;
            PresentDamageClientRpc(result.AppliedAmount, result.Killed);
        }

        [ClientRpc]
        private void PresentDamageClientRpc(int appliedAmount, bool killed)
        {
            if (!IsServer)
            {
                _enemy?.PresentReplicatedDamage(appliedAmount, killed);
            }
        }

        private void HandleDefeated(Enemy _)
        {
            if (IsServer)
            {
                StartCoroutine(DespawnAfterDeath());
            }
        }

        private IEnumerator DespawnAfterDeath()
        {
            yield return new WaitForSeconds(2f);
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void HandleProjectileCreated(GameObject projectileObject)
        {
            if (!IsServer || projectileObject == null)
            {
                return;
            }

            var networkObject = projectileObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("Spitter 投射物缺少 NetworkObject，Host 已拒绝生成。", projectileObject);
                Destroy(projectileObject);
                return;
            }

            networkObject.Spawn(true);
        }
    }
}
