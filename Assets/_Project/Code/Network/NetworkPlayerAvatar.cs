using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Networking
{
    /// <summary>
    /// 表示 NetworkArena 中的一名玩家，负责采集 Owner 输入并由 Host 统一模拟移动、校验攻击和修改生命。
    /// 采用 Command + Server Authority + Replicated State：客户端只提交意图，NetworkVariable 与 NetworkTransform 复制最终事实。
    /// 这样可以清楚展示状态同步与防作弊边界，同时不复制完整单机 PlayerController，避免两套复杂玩法实现互相污染。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(CharacterController))]
    public sealed class NetworkPlayerAvatar : NetworkBehaviour
    {
        private const int MaximumHealth = 5;
        private const int AttackDamage = 1;
        private const float MoveSpeed = 6f;
        private const float TurnSpeed = 900f;
        private const float Gravity = -20f;
        private const float AttackRange = 2.25f;
        private const float AttackCooldown = 0.65f;
        private const float InputSendInterval = 0.05f;
        private const float InputTimeout = 0.3f;
        private const float RespawnDelay = 2f;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int LocomotionState = Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.EllenCombo_1");
        private static readonly int HitState = Animator.StringToHash("Base Layer.EllenHitFront");
        private static readonly int DeathState = Animator.StringToHash("Base Layer.EllenDeath");

        private readonly NetworkVariable<int> _currentHealth = new NetworkVariable<int>(
            MaximumHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _normalizedSpeed = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterController _controller;
        private NetworkTransform _networkTransform;
        private Animator _animator;
        private Vector2 _serverMoveInput;
        private uint _lastMovementSequence;
        private uint _lastAttackSequence;
        private uint _localMovementSequence;
        private uint _localAttackSequence;
        private double _lastInputServerTime;
        private double _nextAttackServerTime;
        private float _nextInputSendTime;
        private float _verticalVelocity;
        private bool _respawning;
        private Renderer[] _renderers;
        private Coroutine _presentationRecovery;

        public int CurrentHealth => _currentHealth.Value;
        public int MaxHealth => MaximumHealth;
        public uint LastLocalAttackSequence => _localAttackSequence;
        public bool LastAttackAccepted { get; private set; }
        public NetworkAttackRejection LastAttackRejection { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _networkTransform = GetComponent<NetworkTransform>();
            _animator = GetComponent<Animator>();
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                foreach (var candidate in GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.runtimeAnimatorController != null)
                    {
                        _animator = candidate;
                        break;
                    }
                }
            }
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        public override void OnNetworkSpawn()
        {
            _controller.enabled = IsServer;
            _currentHealth.OnValueChanged += HandleHealthChanged;
            _normalizedSpeed.OnValueChanged += HandleSpeedChanged;
            ApplyPlayerTint();

            if (IsServer)
            {
                TeleportToSpawn();
            }

            if (IsOwner)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    camera.GetComponent<NetworkArenaCameraFollow>()?.SetTarget(transform);
                }
            }

            PlayLocomotion();
        }

        public override void OnNetworkDespawn()
        {
            _currentHealth.OnValueChanged -= HandleHealthChanged;
            _normalizedSpeed.OnValueChanged -= HandleSpeedChanged;
        }

        private void Update()
        {
            if (IsOwner && IsSpawned)
            {
                CaptureOwnerInput();
            }

            if (IsServer && IsSpawned)
            {
                SimulateOnHost(Time.deltaTime);
            }

            if (_animator != null)
            {
                _animator.SetFloat(SpeedParameter, _normalizedSpeed.Value, 0.08f, Time.deltaTime);
            }
        }

        /// <summary>
        /// Owner 只上传归一化移动输入与递增序号；位置从不由 Client 直接写入。
        /// 20Hz 输入发送足以支撑演示，并避免每个渲染帧发送可靠消息造成无意义带宽开销。
        /// </summary>
        private void CaptureOwnerInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var input = new Vector2(
                ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed),
                ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed));
            input = Vector2.ClampMagnitude(input, 1f);

            if (Time.unscaledTime >= _nextInputSendTime)
            {
                _nextInputSendTime = Time.unscaledTime + InputSendInterval;
                SubmitMovementServerRpc(input, ++_localMovementSequence);
            }

            var attackPressed = keyboard.jKey.wasPressedThisFrame ||
                                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (attackPressed)
            {
                SubmitAttackServerRpc(++_localAttackSequence);
            }
        }

        private void SimulateOnHost(float deltaTime)
        {
            if (_currentHealth.Value <= 0 || _respawning)
            {
                _normalizedSpeed.Value = 0f;
                return;
            }

            var inputExpired = NetworkManager.ServerTime.Time - _lastInputServerTime > InputTimeout;
            var input = inputExpired ? Vector2.zero : _serverMoveInput;
            var direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, TurnSpeed * deltaTime);
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity += Gravity * deltaTime;
            var velocity = direction * MoveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * deltaTime);
            _normalizedSpeed.Value = direction.magnitude;
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitMovementServerRpc(Vector2 input, uint sequence, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || sequence <= _lastMovementSequence)
            {
                return;
            }

            _lastMovementSequence = sequence;
            _serverMoveInput = Vector2.ClampMagnitude(input, 1f);
            _lastInputServerTime = NetworkManager.ServerTime.Time;
        }

        /// <summary>
        /// Host 按“序号→存活→冷却→目标→距离”的固定顺序校验攻击，并自行选择前方目标。
        /// 客户端不能指定伤害值或直接修改生命，因此篡改 RPC 参数也无法越权造成任意伤害。
        /// </summary>
        [ServerRpc]
        private void SubmitAttackServerRpc(uint sequence, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            var target = FindBestTarget(out var targetDistance);
            var decision = NetworkAttackRules.Validate(
                sequence,
                _lastAttackSequence,
                _currentHealth.Value > 0,
                NetworkManager.ServerTime.Time,
                _nextAttackServerTime,
                target != null,
                targetDistance,
                AttackRange);

            if (sequence > _lastAttackSequence)
            {
                _lastAttackSequence = sequence;
            }

            if (decision.Accepted)
            {
                _nextAttackServerTime = NetworkManager.ServerTime.Time + AttackCooldown;
                target.ApplyDamageOnHost(AttackDamage);
                PlayAttackClientRpc();
            }

            SendAttackResult(rpcParams.Receive.SenderClientId, sequence, decision);
        }

        private NetworkPlayerAvatar FindBestTarget(out float distance)
        {
            NetworkPlayerAvatar best = null;
            distance = float.PositiveInfinity;
            if (!IsServer)
            {
                return null;
            }

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var playerObject = client.PlayerObject;
                if (playerObject == null || playerObject == NetworkObject)
                {
                    continue;
                }

                var candidate = playerObject.GetComponent<NetworkPlayerAvatar>();
                if (candidate == null || candidate.CurrentHealth <= 0)
                {
                    continue;
                }

                var offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                var candidateDistance = offset.magnitude;
                if (candidateDistance >= distance)
                {
                    continue;
                }

                if (candidateDistance > 0.001f && Vector3.Dot(transform.forward, offset / candidateDistance) < 0.2f)
                {
                    continue;
                }

                best = candidate;
                distance = candidateDistance;
            }

            return best;
        }

        private void ApplyDamageOnHost(int amount)
        {
            if (!IsServer || _currentHealth.Value <= 0)
            {
                return;
            }

            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - Mathf.Max(0, amount));
            if (_currentHealth.Value == 0 && !_respawning)
            {
                StartCoroutine(RespawnOnHost());
            }
        }

        private IEnumerator RespawnOnHost()
        {
            _respawning = true;
            _serverMoveInput = Vector2.zero;
            yield return new WaitForSeconds(RespawnDelay);
            TeleportToSpawn();
            _currentHealth.Value = MaximumHealth;
            _respawning = false;
        }

        private void TeleportToSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            var wasEnabled = _controller.enabled;
            _controller.enabled = false;
            var position = NetworkSpawnPoints.Get(OwnerClientId);
            transform.SetPositionAndRotation(position, Quaternion.identity);
            _networkTransform.Teleport(position, Quaternion.identity, transform.localScale);
            _controller.enabled = wasEnabled;
            _verticalVelocity = -2f;
        }

        private void SendAttackResult(ulong clientId, uint sequence, NetworkAttackDecision decision)
        {
            var parameters = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            AttackResultClientRpc(sequence, decision.Accepted, decision.Rejection, parameters);
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
        private void PlayAttackClientRpc()
        {
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime(AttackState, 0.06f);
                BeginPresentationRecovery(0.55f);
            }
        }

        private void HandleHealthChanged(int previous, int current)
        {
            if (_animator == null)
            {
                return;
            }

            if (current <= 0)
            {
                CancelPresentationRecovery();
                _animator.CrossFadeInFixedTime(DeathState, 0.12f);
            }
            else if (previous <= 0)
            {
                PlayLocomotion();
            }
            else if (current < previous)
            {
                _animator.CrossFadeInFixedTime(HitState, 0.08f);
                BeginPresentationRecovery(0.35f);
            }
        }

        private void HandleSpeedChanged(float _, float current)
        {
            if (current > 0.01f && _animator != null && !_animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"))
            {
                PlayLocomotion();
            }
        }

        private void PlayLocomotion()
        {
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime(LocomotionState, 0.08f);
            }
        }

        private void BeginPresentationRecovery(float delay)
        {
            CancelPresentationRecovery();
            _presentationRecovery = StartCoroutine(RecoverPresentation(delay));
        }

        private IEnumerator RecoverPresentation(float delay)
        {
            yield return new WaitForSeconds(delay);
            _presentationRecovery = null;
            if (_currentHealth.Value > 0)
            {
                PlayLocomotion();
            }
        }

        private void CancelPresentationRecovery()
        {
            if (_presentationRecovery == null)
            {
                return;
            }

            StopCoroutine(_presentationRecovery);
            _presentationRecovery = null;
        }

        private void ApplyPlayerTint()
        {
            var tint = OwnerClientId % 2 == 0 ? new Color(0.25f, 0.7f, 1f) : new Color(1f, 0.45f, 0.25f);
            foreach (var item in _renderers)
            {
                if (item == null || item is ParticleSystemRenderer || item is TrailRenderer)
                {
                    continue;
                }

                var material = item.material;
                if (material != null && material.HasProperty("_Color"))
                {
                    material.color = Color.Lerp(material.color, tint, 0.35f);
                }
            }
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        /// <summary>
        /// 接收原 Ellen 连击动画自带的命中窗口事件。网络伤害已由 Host 命令校验结算，
        /// 因此这里只消费表现事件，避免动画事件在独立联机角色上产生“无接收者”警告或重复伤害。
        /// </summary>
        public void MeleeAttackStart(int _ = 0)
        {
        }

        public void MeleeAttackEnd()
        {
        }
    }
}
