using System.Collections;
using System.Collections.Generic;
using Odyssey.Characters.Enemies;
using Odyssey.Characters.Player;
using Odyssey.Gameplay.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Odyssey.Networking
{
    /// <summary>
    /// 把原 PlayerController 适配为双人合作网络角色：Owner 保留完整手感，Host 权威处理攻击、生命、冲刺无敌与复活。
    /// 采用 Adapter、Command 和 Replicated State；客户端从不提交目标、伤害值、生命值或无敌持续时间。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(OwnerAuthoritativeNetworkTransform), typeof(OwnerAuthoritativeNetworkAnimator))]
    public sealed class NetworkPlayerAdapter : NetworkBehaviour, IPlayerAttackResolver, IExternalPlayerDamageAuthority
    {
        private const float ComboContinuationWindow = 0.9f;
        private const float PositionAuditInterval = 0.2f;
        private const float PositionTolerance = 1.5f;
        private const float DamageInvulnerabilityDuration = 1.5f;
        private static readonly Collider[] AttackBuffer = new Collider[24];

        private readonly NetworkVariable<int> _health = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _invulnerableUntil = new NetworkVariable<double>(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkComboSequenceValidator _comboValidator =
            new NetworkComboSequenceValidator(ComboContinuationWindow);

        private PlayerController _player;
        private GameplaySessionController _session;
        private uint _localAttackSequence;
        private uint _lastAttackSequence;
        private uint _localDashSequence;
        private uint _lastDashSequence;
        private double _nextAttackChainTime;
        private double _nextDashTime;
        private double _nextPositionAuditTime;
        private Vector3 _lastAcceptedPosition;
        private bool _respawning;
        private bool _usesNetworkGameplayAuthority;

        public int CurrentHealth => _health.Value;
        public bool IsDamageImmune => IsSpawned && NetworkManager != null &&
                                      NetworkManager.ServerTime.Time < _invulnerableUntil.Value;
        public uint LastAttackSequence => _localAttackSequence;
        public bool LastAttackAccepted { get; private set; }
        public NetworkAttackRejection LastAttackRejection { get; private set; }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        /// <summary>
        /// 在网络对象生成后完成一次装配：先恢复场景依赖，再根据会话模式选择单机本地规则或双人 Host 权威端口。
        /// 该分支是复用同一 Player Prefab 的关键，SinglePlayerTransport 不会意外改变已验证的单机存档与复活语义。
        /// </summary>
        public override void OnNetworkSpawn()
        {
            _session = FindFirstObjectByType<GameplaySessionController>();
            _session?.RegisterLocalPlayer(IsOwner ? _player : null);
            var installer = FindFirstObjectByType<Odyssey.Bootstrap.GameplaySceneInstaller>();
            installer?.InstallRuntimePlayer(_player);

            // SinglePlayerTransport 只统一 NGO 对象生命周期，不改变原单机玩法权威。
            // 这样存档读取、暂停、生命和复活仍复用已经验证过的本地管线；只有 Host/Client 才接管这些端口。
            _usesNetworkGameplayAuthority = _session != null && _session.IsMultiplayer;
            if (_usesNetworkGameplayAuthority)
            {
                _player.SetAttackResolver(this);
                _player.SetExternalDamageAuthority(this);
                _player.DashAuthorityRequested += HandleDashRequested;
            }
            else if (IsOwner && _session != null)
            {
                // Prefab 不能保存对 Level_01 场景 Transform 的引用，单机生成后在组合根边界恢复复活点。
                _player.RespawnPoint = _session.GetSpawnPoint(OwnerClientId);
            }

            _player.enabled = IsOwner;
            foreach (var footPlacement in GetComponentsInChildren<PlayerFootPlacementController>(true))
            {
                footPlacement.enabled = IsOwner;
            }

            if (IsServer)
            {
                _health.Value = _player.MaxHealth;
                _lastAcceptedPosition = transform.position;
            }

            ConfigurePlayerCollision();
            ApplyPlayerTint();
        }

        public override void OnNetworkDespawn()
        {
            if (_player != null)
            {
                if (_usesNetworkGameplayAuthority)
                {
                    _player.DashAuthorityRequested -= HandleDashRequested;
                    _player.SetAttackResolver(null);
                    _player.SetExternalDamageAuthority(null);
                }
            }
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (IsOwner || _respawning || NetworkManager.ServerTime.Time < _nextPositionAuditTime)
            {
                return;
            }

            var now = NetworkManager.ServerTime.Time;
            var elapsed = Mathf.Max(PositionAuditInterval, (float)(now - (_nextPositionAuditTime - PositionAuditInterval)));
            _nextPositionAuditTime = now + PositionAuditInterval;
            var maximumSpeed = Mathf.Max(_player.RunSpeed, _player.DashForce);
            var allowedDistance = maximumSpeed * elapsed + PositionTolerance;
            if (Vector3.Distance(_lastAcceptedPosition, transform.position) > allowedDistance)
            {
                SendPositionCorrection(OwnerClientId, _lastAcceptedPosition);
                return;
            }

            _lastAcceptedPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (IsServer)
            {
                CheckEnemyContactOnHost();
            }
        }

        /// <summary>
        /// 接收 Owner 动画命中窗口并发送命令序号；此处不做命中查询或扣血，避免本机表现提前成为玩法事实。
        /// </summary>
        public void Resolve(PlayerController player, int comboIndex)
        {
            if (!IsOwner || player != _player)
            {
                return;
            }

            LastAttackAccepted = false;
            LastAttackRejection = NetworkAttackRejection.None;
            RequestAttackServerRpc(++_localAttackSequence, comboIndex);
        }

        /// <summary>
        /// 只允许 Host 改写网络生命和无敌截止时间，再把已提交的结果广播给客户端表现。
        /// 同一入口覆盖接触与投射物伤害，确保无敌窗口不会因伤害来源不同而失效。
        /// </summary>
        public DamageResult TryTakeDamage(int damage, Vector3 attackerPosition, string sourceId)
        {
            if (!IsServer || _health.Value <= 0 || damage <= 0)
            {
                return new DamageResult(false, 0, _health.Value <= 0);
            }

            var request = new DamageRequest(damage, sourceId);
            if (IsDamageImmune)
            {
                SendEvadePresentation(OwnerClientId, request);
                return new DamageResult(false, 0, false);
            }

            var previous = _health.Value;
            _health.Value = Mathf.Max(0, previous - damage);
            // 与单机受伤后的 1.5 秒保护保持一致，避免接触检测和咬击在同一瞬间重复扣除多格生命。
            _invulnerableUntil.Value = System.Math.Max(
                _invulnerableUntil.Value,
                NetworkManager.ServerTime.Time + DamageInvulnerabilityDuration);
            var applied = previous - _health.Value;
            var killed = _health.Value == 0;
            PresentDamageClientRpc(previous, _health.Value, attackerPosition, sourceId);
            if (killed && !_respawning)
            {
                StartCoroutine(RespawnOnHost());
            }

            return new DamageResult(true, applied, killed);
        }

        /// <summary>
        /// 在 Host 上复用 PlayerController 的真实胶囊重叠查询并提交权威伤害。
        /// Client 只复制位置和生命结果，不维护另一套接触尺寸，也不能自行决定伤害。
        /// </summary>
        private void CheckEnemyContactOnHost()
        {
            if (!_usesNetworkGameplayAuthority || _respawning || _health.Value <= 0 ||
                _player == null || _player.Controller == null)
            {
                return;
            }

            if (!_player.TryGetTouchingEnemy(out var enemy))
            {
                return;
            }

            TryTakeDamage(enemy.AttackDamage, enemy.transform.position, "enemy_contact");
        }

        /// <summary>
        /// 将 Owner 的攻击意图转为 Host 判定：验证发送者、序号、连击、冷却、目标和距离后才执行范围命中。
        /// 即使请求被拒绝也回传结果，Owner 可以保持动画表现并显示可调试的拒绝原因。
        /// </summary>
        [ServerRpc]
        private void RequestAttackServerRpc(uint sequence, int comboIndex, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            var now = NetworkManager.ServerTime.Time;
            var sequenceValid = sequence > _lastAttackSequence;
            if (sequenceValid)
            {
                _lastAttackSequence = sequence;
            }

            // 重复或过期命令不得推进连击状态，否则攻击者可以用旧包重置 Host 的期望段次。
            var comboValid = sequenceValid && _comboValidator.TryAdvance(comboIndex, now);
            var target = FindNearestEnemyInFront(out var distance);
            var decision = !sequenceValid
                ? new NetworkAttackDecision(false, NetworkAttackRejection.DuplicateOrExpired)
                : !comboValid
                    ? new NetworkAttackDecision(false, NetworkAttackRejection.InvalidCombo)
                    : NetworkAttackRules.Validate(
                        sequence,
                        sequence - 1,
                        _health.Value > 0,
                        now,
                        comboIndex == 1 ? _nextAttackChainTime : 0d,
                        target != null,
                        distance,
                        _player.AttackRange);

            if (comboIndex == 1 && sequenceValid && comboValid)
            {
                _nextAttackChainTime = now + _player.AttackCooldown;
            }

            if (decision.Accepted)
            {
                ApplyAttackToEnemies();
            }

            SendAttackResult(OwnerClientId, sequence, decision);
        }

        /// <summary>
        /// 冲刺请求只更新 Host 的无敌/冷却时间轴；实际位移仍由 Owner 的表现同步承担。
        /// 这使手感和可影响伤害的规则分别拥有唯一写入者。
        /// </summary>
        [ServerRpc]
        private void RequestDashServerRpc(uint sequence, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId ||
                sequence <= _lastDashSequence ||
                _health.Value <= 0)
            {
                return;
            }

            _lastDashSequence = sequence;
            var now = NetworkManager.ServerTime.Time;
            if (now < _nextDashTime)
            {
                return;
            }

            _invulnerableUntil.Value = now + Mathf.Max(0.05f, _player.DashDuration);
            _nextDashTime = now + Mathf.Max(_player.DashDuration, _player.GroundDashCooldown);
        }

        private Enemy FindNearestEnemyInFront(out float distance)
        {
            Enemy nearest = null;
            distance = float.PositiveInfinity;
            foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.CurrentHealth <= 0)
                {
                    continue;
                }

                var offset = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
                var candidateDistance = offset.magnitude;
                if (candidateDistance <= 0.001f || candidateDistance >= distance ||
                    Vector3.Dot(transform.forward, offset / candidateDistance) < 0.2f)
                {
                    continue;
                }

                nearest = enemy;
                distance = candidateDistance;
            }

            return nearest;
        }

        /// <summary>
        /// 在 Host 以 NonAlloc 球形查询复核攻击范围，并按 Enemy 去重后提交统一伤害入口。
        /// Collider 缓冲区只是查询优化；真正的目标合法性已由前置规则和前方夹角共同限制。
        /// </summary>
        private void ApplyAttackToEnemies()
        {
            var center = transform.position + transform.forward + Vector3.up * 0.5f;
            var count = Physics.OverlapSphereNonAlloc(
                center,
                _player.AttackRange,
                AttackBuffer,
                _player.EnemyLayer,
                QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<Enemy>();
            for (var index = 0; index < count; index++)
            {
                var enemy = AttackBuffer[index].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.CurrentHealth <= 0 || !damaged.Add(enemy))
                {
                    continue;
                }

                var direction = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up).normalized;
                if (Vector3.Dot(transform.forward, direction) >= 0.2f)
                {
                    enemy.TakeDamage(_player.AttackDamage);
                }
            }
        }

        /// <summary>
        /// 死亡后仅由 Host 等待、选取出生点和恢复生命，再定向通知 Owner 修正位置与表现。
        /// 复活期间屏蔽重复接触伤害，防止多帧碰撞启动多个协程。
        /// </summary>
        private IEnumerator RespawnOnHost()
        {
            _respawning = true;
            yield return new WaitForSeconds(_player.RespawnDelay);
            var position = _session == null ? transform.position : _session.GetSpawnPosition(OwnerClientId);
            _health.Value = _player.MaxHealth;
            _invulnerableUntil.Value = NetworkManager.ServerTime.Time + 1f;
            _lastAcceptedPosition = position;
            SendRespawn(OwnerClientId, position, _health.Value);
            _respawning = false;
        }

        private void HandleDashRequested()
        {
            if (IsOwner)
            {
                RequestDashServerRpc(++_localDashSequence);
            }
        }

        private void SendAttackResult(ulong clientId, uint sequence, NetworkAttackDecision decision)
        {
            AttackResultClientRpc(
                sequence,
                decision.Accepted,
                decision.Rejection,
                CreateTarget(clientId));
        }

        [ClientRpc]
        private void AttackResultClientRpc(
            uint sequence,
            bool accepted,
            NetworkAttackRejection rejection,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            LastAttackAccepted = accepted;
            LastAttackRejection = rejection;
        }

        [ClientRpc]
        private void PresentDamageClientRpc(
            int previous,
            int current,
            Vector3 attackerPosition,
            string sourceId,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner)
            {
                _player.PresentAuthoritativeDamage(previous, current, attackerPosition, sourceId, true);
            }
        }

        private void SendEvadePresentation(ulong clientId, DamageRequest request)
        {
            PresentEvadeClientRpc(request.Amount, request.SourceId, CreateTarget(clientId));
        }

        [ClientRpc]
        private void PresentEvadeClientRpc(int amount, string sourceId, ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner)
            {
                _player.PresentAuthoritativeEvade(new DamageRequest(amount, sourceId));
            }
        }

        private void SendRespawn(ulong clientId, Vector3 position, int restoredHealth)
        {
            PresentRespawnClientRpc(position, restoredHealth, CreateTarget(clientId));
        }

        [ClientRpc]
        private void PresentRespawnClientRpc(
            Vector3 position,
            int restoredHealth,
            ClientRpcParams clientRpcParams = default)
        {
            if (IsOwner)
            {
                _player.PresentAuthoritativeRespawn(position, restoredHealth, true);
            }
        }

        private void SendPositionCorrection(ulong clientId, Vector3 position)
        {
            CorrectPositionClientRpc(position, CreateTarget(clientId));
        }

        [ClientRpc]
        private void CorrectPositionClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner || _player.Controller == null)
            {
                return;
            }

            _player.Controller.enabled = false;
            transform.position = position;
            _player.Controller.enabled = true;
        }

        private static ClientRpcParams CreateTarget(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
        }

        private void ConfigurePlayerCollision()
        {
            if (_player.Controller == null)
            {
                return;
            }

            foreach (var other in FindObjectsByType<NetworkPlayerAdapter>(FindObjectsSortMode.None))
            {
                if (other == this || other._player == null || other._player.Controller == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(_player.Controller, other._player.Controller, true);
            }
        }

        private void ApplyPlayerTint()
        {
            var tint = OwnerClientId % 2 == 0 ? new Color(0.25f, 0.7f, 1f) : new Color(1f, 0.45f, 0.25f);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                {
                    continue;
                }

                var material = renderer.material;
                if (material != null && material.HasProperty("_Color"))
                {
                    material.color = Color.Lerp(material.color, tint, 0.25f);
                }
            }
        }
    }
}
