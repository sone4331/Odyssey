using Odyssey.Characters.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 让 Spitter 投射物只在 Host 移动和碰撞，Client 通过 NetworkTransform 显示同一权威轨迹。
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkProjectileAdapter : NetworkBehaviour
    {
        private EnemyProjectile _projectile;

        private void Awake()
        {
            _projectile = GetComponent<EnemyProjectile>();
            if (_projectile != null)
            {
                _projectile.enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (_projectile == null)
            {
                return;
            }

            _projectile.UsesExternalDespawn = IsServer;
            _projectile.enabled = IsServer;
            if (IsServer)
            {
                _projectile.Resolved += HandleResolved;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_projectile != null)
            {
                _projectile.Resolved -= HandleResolved;
            }
        }

        private void HandleResolved(EnemyProjectile _)
        {
            if (IsServer && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}
