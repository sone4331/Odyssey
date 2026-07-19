using Odyssey.Characters.Player;
using System;
using UnityEngine;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 负责单个敌方投射物的飞行、连续碰撞查询、伤害提交和超时销毁。
    /// 采用自包含对象与 NonAlloc SphereCast，既避免高速投射物穿透，也不为低频攻击引入全局对象池或管理器。
    /// </summary>
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private const float DefaultLifetime = 5f;
        private const float ProbeRadius = 0.16f;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private Enemy _owner;
        private Vector3 _sourcePosition;
        private Vector3 _direction;
        private float _speed;
        private float _remainingLifetime;
        private int _damage;
        private bool _initialized;
        private bool _resolved;

        public bool IsInitialized => _initialized;
        public bool IsResolved => _resolved;
        public bool UsesExternalDespawn { get; set; }
        public event Action<EnemyProjectile> Resolved;

        /// <summary>
        /// 由发射者一次性提交不可变飞行参数，投射物不会在后续帧追踪目标或读取敌人配置。
        /// 固定发射方向让前摇和闪避具有可读性，也便于后续 Host 复制同一权威轨迹。
        /// </summary>
        public void Initialize(
            Enemy owner,
            Vector3 direction,
            float speed,
            int damage,
            float lifetime = DefaultLifetime)
        {
            _owner = owner;
            _sourcePosition = owner == null ? transform.position : owner.transform.position;
            _direction = direction.sqrMagnitude <= 0.0001f ? transform.forward : direction.normalized;
            _speed = Mathf.Max(0.01f, speed);
            _damage = Mathf.Max(1, damage);
            _remainingLifetime = Mathf.Max(0.01f, lifetime);
            _initialized = true;
            _resolved = false;
        }

        private void Update()
        {
            if (!IsInitialized || _resolved)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            _remainingLifetime -= deltaTime;
            if (_remainingLifetime <= 0f)
            {
                Resolve();
                return;
            }

            var distance = _speed * deltaTime;
            var count = Physics.SphereCastNonAlloc(
                transform.position,
                ProbeRadius,
                _direction,
                _hitBuffer,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            var nearestDistance = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (var index = 0; index < count; index++)
            {
                var hit = _hitBuffer[index];
                if (hit.collider == null || IsFriendlyCollider(hit.collider) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestCollider = hit.collider;
            }

            if (nearestCollider != null)
            {
                transform.position += _direction * nearestDistance;
                var player = nearestCollider.GetComponentInParent<PlayerController>();
                if (player != null)
                {
                    player.TryTakeDamage(_damage, _sourcePosition, "enemy_projectile");
                }

                Resolve();
                return;
            }

            transform.position += _direction * distance;
        }

        private bool IsFriendlyCollider(Collider candidate)
        {
            return (_owner != null && candidate.transform.IsChildOf(_owner.transform)) ||
                   candidate.GetComponentInParent<Enemy>() != null;
        }

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            Resolved?.Invoke(this);
            if (!UsesExternalDespawn)
            {
                Destroy(gameObject);
            }
        }
    }
}
