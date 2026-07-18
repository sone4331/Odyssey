using System;
using System.Collections;
using Odyssey.Core.Abilities;
using Odyssey.Gameplay.Characters;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;
using Odyssey.Inputs;
using Odyssey.Unity.Config;
using UnityEngine;
using UnityEngine.Serialization;

namespace Odyssey.Characters.Player
{
    /// <summary>
    /// 玩家对象的 Unity 门面与组合根，负责装配输入、移动轴、动作轴、生命和配置适配器。
    /// 采用 Facade、Composition Root 与正交状态机模式：本类只协调子系统生命周期，不再承载各状态的逐帧实现。
    /// 保留原有序列化字段名和动画事件入口，是为了在完整重构时维持场景、Prefab 与动画剪辑的向后兼容。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IConfigTarget<PlayerConfigData>, IDamageable
    {
        public const string AttackAbilityId = PlayerRuntimeSystems.AttackAbilityId;
        public const string DashAbilityId = PlayerRuntimeSystems.DashAbilityId;
        public const string HitAbilityId = PlayerRuntimeSystems.HitAbilityId;

        [Header("配置")]
        [SerializeField] private string configId = "player";
        [Tooltip("由 Input System 资产提供输入快照与动作事件。")]
        public InputReader InputReader;

        [Header("基础移动")]
        public float Gravity = -15f;
        public float VerticalVelocity;
        public float WalkSpeed = 4f;
        public float RunSpeed = 8f;
        public float GroundAcceleration = 20f;
        public float GroundDeceleration = 25f;
        public float MinTurnSpeed = 400f;
        public float MaxTurnSpeed = 1200f;
        [Tooltip("关卡地面与斜坡所在层，用于坡面投影和脚部贴地。")]
        public LayerMask GroundLayer;

        [Header("冲刺")]
        public float DashForce = 20f;
        public float DashDuration = 0.2f;
        public float GroundDashCooldown = 0.5f;

        [Header("空中动作")]
        public bool CanAirJump;
        public bool CanAirDash;
        public float JumpHeight = 3f;
        public float ChargeJumpHeight = 5f;
        public float MinChargeTime = 0.5f;
        public float AirJumpHeight = 2f;

        [Header("墙面动作")]
        public LayerMask WallLayer;
        public float WallSlideSpeed = -3f;
        public float WallJumpUpForce = 12f;
        public float WallJumpSideForce = 10f;

        [Header("战斗")]
        public float AttackRange = 1.5f;
        public int AttackDamage = 1;
        public float AttackCooldown = 0.5f;
        public float AttackAdvanceSpeed = 1.5f;
        public LayerMask EnemyLayer;

        [Header("生命")]
        [FormerlySerializedAs("MaxHealth")]
        [SerializeField] private int maxHealth = 5;
        [FormerlySerializedAs("CurrentHealth")]
        [SerializeField] private int startingHealth = 5;
        public bool IsInvincible;

        [Header("复活")]
        public Transform RespawnPoint;
        public float RespawnDelay = 3f;

        private PlayerRuntimeSystems _runtime;
        private PlayerConfigData _appliedConfig;
        private PlayerLocomotionRuntime _locomotion;
        private PlayerActionRuntime _actions;
        private bool _isDead;
        private bool _started;

        public CharacterController Controller { get; private set; }
        public Animator Animator { get; private set; }
        internal PlayerAnimationDriver Animation { get; private set; }
        public Transform MainCameraTransform { get; private set; }
        public bool MovementEnabled { get; set; }
        public int MaxHealth => maxHealth;
        public string ConfigId => configId;
        public int CurrentHealth => _runtime?.Health.Current ?? Mathf.Clamp(startingHealth, 0, maxHealth);
        public IAbilitySystem Abilities => _runtime?.Abilities;
        public PlayerLocomotionStateId LocomotionState => _locomotion?.CurrentStateId ?? PlayerLocomotionStateId.Grounded;
        public PlayerActionStateId ActionState => _actions?.CurrentStateId ?? PlayerActionStateId.Free;
        public float CurrentPlanarSpeed => _locomotion?.CurrentPlanarSpeed ?? 0f;
        public Vector3 DesiredMoveDirection => _locomotion?.DesiredMoveDirection ?? Vector3.zero;
        public float GroundSlopeAngle => _locomotion?.GroundSlopeAngle ?? 0f;
        public bool WallClearanceActive => _locomotion?.WallClearanceActive ?? false;
        public bool IsDamageImmune => IsInvincible ||
                                      (Abilities?.Tags.Has(PlayerRuntimeSystems.InvulnerableTag) ?? false);

        public event Action<HealthChanged> HealthChanged;
        public event Action RuntimeConfigured;
        public event Action<DamageRequest> DamageEvaded;

        private void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Animator = GetComponentInChildren<Animator>();
            if (Animator == null)
            {
                throw new InvalidOperationException("玩家对象缺少 Animator，无法驱动角色表现。");
            }

            Animation = new PlayerAnimationDriver(Animator);
            var mainCamera = Camera.main;
            MainCameraTransform = mainCamera == null ? transform : mainCamera.transform;
            if (mainCamera == null)
            {
                Debug.LogWarning("未找到 MainCamera，玩家移动将临时使用自身朝向作为相机方向。", this);
            }

            RebuildRuntimeSystems(CreateInspectorConfig(), startingHealth);
            _locomotion = new PlayerLocomotionRuntime(this);
            _actions = new PlayerActionRuntime(this);
        }

        private void OnEnable()
        {
            if (InputReader == null)
            {
                Debug.LogError("玩家缺少 InputReader，无法接收攻击和冲刺输入。", this);
                return;
            }

            InputReader.AttackRequested += HandleAttackRequested;
            InputReader.DashRequested += HandleDashRequested;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            MovementEnabled = true;
            _actions.Initialize();
            _locomotion.Initialize();
            _started = true;
        }

        private void Update()
        {
            if (!_started || _isDead)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            _actions.Tick(deltaTime);
            _locomotion.Tick(deltaTime, !_actions.BlocksLocomotion);
        }

        private void OnDisable()
        {
            if (InputReader == null)
            {
                return;
            }

            InputReader.AttackRequested -= HandleAttackRequested;
            InputReader.DashRequested -= HandleDashRequested;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (((1 << hit.gameObject.layer) & EnemyLayer) != 0)
            {
                TryTakeDamage(1, hit.transform.position, "enemy_contact");
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Acid"))
            {
                TryTakeDamage(1, transform.position, "acid");
            }
        }

        /// <summary>
        /// 在 Scene 视图展示地面探测方向与 Ellen 常态手臂安全半径，便于调试斜坡和狭窄通道。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = WallClearanceActive ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.75f, PlayerWallClearanceSolver.VisualRadius);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.25f, PlayerWallClearanceSolver.VisualRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position + Vector3.down * 0.25f);
        }

        /// <summary>
        /// 通过共享 Health 管线提交伤害，再把 DamageResult 翻译为死亡或受击动作。
        /// 领域状态先提交、表现后响应，保证 UI、动画和未来 Host 权威网络使用同一份生命事实。
        /// </summary>
        public void TakeDamage(int damage, Vector3 attackerPosition)
        {
            TryTakeDamage(damage, attackerPosition, "enemy");
        }

        /// <summary>
        /// 统一校验死亡、受击无敌和 Ability 标签后提交伤害，并返回可供投射物与未来 Host 使用的权威结果。
        /// 冲刺无敌属于玩法规则而非碰撞特例，因此接触伤害与投射物都会经过同一入口。
        /// </summary>
        public DamageResult TryTakeDamage(int damage, Vector3 attackerPosition, string sourceId)
        {
            var request = new DamageRequest(damage, sourceId);
            if (_isDead || _runtime == null)
            {
                return new DamageResult(false, 0, _isDead);
            }

            if (IsDamageImmune)
            {
                DamageEvaded?.Invoke(request);
                return new DamageResult(false, 0, false);
            }

            var result = _runtime.Health.Apply(request);
            if (!result.Accepted)
            {
                return result;
            }

            if (result.Killed)
            {
                _isDead = true;
                Animation.PlayDeath();
                StartCoroutine(RespawnRoutine());
                return result;
            }

            StartCoroutine(InvincibilityRoutine());
            var knockbackDirection = (transform.position - attackerPosition).normalized;
            knockbackDirection.y = 0f;
            if (knockbackDirection != Vector3.zero)
            {
                transform.forward = -knockbackDirection;
            }

            VerticalVelocity = 5f;
            transform.position += Vector3.up * 0.1f;
            if (TryActivateAbility(HitAbilityId))
            {
                _actions.RequestHit(knockbackDirection * 12f);
            }

            return result;
        }

        /// <summary>
        /// 实现领域伤害端口，供不需要击退方向的调用方与网络权威层使用；现有 Unity 命中入口仍可额外提供攻击者位置。
        /// </summary>
        public DamageResult Apply(DamageRequest request)
        {
            var attackerPosition = transform.position - transform.forward;
            return TryTakeDamage(request.Amount, attackerPosition, request.SourceId);
        }

        /// <summary>
        /// 统一调用 AbilitySystem 的激活校验，调用方只接收成功与否，不直接修改标签或冷却。
        /// </summary>
        public bool TryActivateAbility(string abilityId)
        {
            return Abilities != null && Abilities.TryActivate(abilityId, Time.time).Succeeded;
        }

        /// <summary>
        /// 结束指定动作的 Ability 生命周期；重复结束是安全的幂等操作。
        /// </summary>
        public void EndAbility(string abilityId)
        {
            Abilities?.End(abilityId);
        }

        /// <summary>
        /// 由会接管位移的动作清空移动轴速度；动作层不直接访问移动状态实例，保持两条状态轴单向协调。
        /// </summary>
        internal void StopPlanarMotion()
        {
            _locomotion?.StopPlanarMotion();
        }

        /// <summary>
        /// 在冲刺正常结束时把最大奔跑速度交还给移动轴，避免动作轴直接修改移动状态内部字段。
        /// 该入口只负责正交状态机之间的边界协调，不改变普通移动、攻击或受击的起步规则。
        /// </summary>
        internal void ResumePlanarMotionAtMaximumSpeed(Vector3 dashDirection)
        {
            _locomotion?.ResumeAtMaximumSpeed(dashDirection);
        }

        /// <summary>
        /// 为存档和调试工具提供统一生命设置入口，禁止外部直接改写序列化字段。
        /// </summary>
        public void SetHealth(int value, string sourceId = "external")
        {
            if (_runtime == null)
            {
                return;
            }

            var target = Mathf.Clamp(value, 0, MaxHealth);
            if (target < _runtime.Health.Current)
            {
                _runtime.Health.Apply(new DamageRequest(_runtime.Health.Current - target, sourceId));
            }
            else if (target > _runtime.Health.Current)
            {
                _runtime.Health.Restore(target - _runtime.Health.Current, sourceId);
            }
        }

        /// <summary>
        /// 应用导表后的玩家配置，并在重建领域运行时时保留当前生命。
        /// 配置层只更新数值，不创建场景对象或控制 UI，保持数据管线与表现层解耦。
        /// </summary>
        public void Apply(PlayerConfigData config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (ReferenceEquals(_appliedConfig, config))
            {
                return;
            }

            var preservedHealth = _appliedConfig == null ? config.MaxHealth : CurrentHealth;
            Gravity = config.Gravity;
            WalkSpeed = config.WalkSpeed;
            RunSpeed = config.RunSpeed;
            GroundAcceleration = config.GroundAcceleration;
            GroundDeceleration = config.GroundDeceleration;
            MinTurnSpeed = config.MinTurnSpeed;
            MaxTurnSpeed = config.MaxTurnSpeed;
            DashForce = config.DashForce;
            DashDuration = config.DashDuration;
            GroundDashCooldown = config.DashCooldown;
            JumpHeight = config.JumpHeight;
            ChargeJumpHeight = config.ChargeJumpHeight;
            MinChargeTime = config.MinChargeTime;
            AirJumpHeight = config.AirJumpHeight;
            WallSlideSpeed = config.WallSlideSpeed;
            WallJumpUpForce = config.WallJumpUpForce;
            WallJumpSideForce = config.WallJumpSideForce;
            AttackDamage = config.AttackDamage;
            AttackRange = config.AttackRange;
            AttackCooldown = config.AttackCooldown;
            AttackAdvanceSpeed = config.AttackAdvanceSpeed;
            maxHealth = config.MaxHealth;
            RebuildRuntimeSystems(config, preservedHealth);
            _appliedConfig = config;
            RuntimeConfigured?.Invoke();
        }

        private void HandleAttackRequested()
        {
            if (_isDead || _actions == null || LocomotionState != PlayerLocomotionStateId.Grounded)
            {
                return;
            }

            _actions.RequestAttack();
        }

        private void HandleDashRequested()
        {
            if (_isDead || _actions == null)
            {
                return;
            }

            if (LocomotionState != PlayerLocomotionStateId.Grounded && !CanAirDash)
            {
                return;
            }

            _actions.RequestDash();
        }

        private PlayerConfigData CreateInspectorConfig()
        {
            return new PlayerConfigData(
                configId,
                WalkSpeed,
                RunSpeed,
                Gravity,
                DashForce,
                DashDuration,
                GroundDashCooldown,
                JumpHeight,
                ChargeJumpHeight,
                MinChargeTime,
                AirJumpHeight,
                WallSlideSpeed,
                WallJumpUpForce,
                WallJumpSideForce,
                AttackDamage,
                AttackRange,
                AttackCooldown,
                Mathf.Max(1, maxHealth),
                GroundAcceleration,
                GroundDeceleration,
                MinTurnSpeed,
                MaxTurnSpeed,
                AttackAdvanceSpeed);
        }

        private void RebuildRuntimeSystems(PlayerConfigData config, int healthToPreserve)
        {
            if (_runtime != null)
            {
                _runtime.Health.Changed -= OnHealthChanged;
            }

            _runtime = new PlayerRuntimeSystems(config, healthToPreserve);
            _runtime.Health.Changed += OnHealthChanged;
        }

        private void OnHealthChanged(HealthChanged change)
        {
            HealthChanged?.Invoke(change);
        }

        private IEnumerator InvincibilityRoutine()
        {
            IsInvincible = true;
            yield return new WaitForSeconds(1.5f);
            IsInvincible = false;
        }

        /// <summary>
        /// 在明确的死亡生命周期边界完成等待、传送、领域重置和两条状态轴复位。
        /// 采用编排器模式集中处理顺序，避免移动、生命和动画各自启动互相竞争的复活协程。
        /// </summary>
        private IEnumerator RespawnRoutine()
        {
            enabled = false;
            yield return new WaitForSeconds(RespawnDelay);

            _runtime.Health.Reset("respawn");
            _isDead = false;
            VerticalVelocity = 0f;
            Controller.enabled = false;
            if (RespawnPoint != null)
            {
                transform.position = RespawnPoint.position;
            }
            else
            {
                Debug.LogWarning("未配置 RespawnPoint，玩家将在当前位置复活。", this);
            }

            Controller.enabled = true;
            Animation.PlayGrounded();
            _actions.Reset();
            _locomotion.Reset();
            enabled = true;
        }

        /// <summary>
        /// 保留官方动画剪辑的事件签名，并把动画作者标注的挥击时段转交给动作轴作为唯一命中窗口。
        /// </summary>
        public void MeleeAttackStart(int throwing = 0)
        {
            _actions?.OpenAttackWindow();
        }

        /// <summary>
        /// 保留动画剪辑中的旧事件签名，避免资源迁移期间出现 Missing Method 警告。
        /// </summary>
        public void MeleeAttackEnd()
        {
            _actions?.CloseAttackWindow();
        }
    }
}
