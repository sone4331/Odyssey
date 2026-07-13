using UnityEngine;
using Odyssey.Inputs; // 引用我们的输入系统
using Odyssey.Core.FSM; // 引用我们的核心状态机
using Odyssey.Core.Abilities;
using Odyssey.Gameplay.Characters;
using Odyssey.Gameplay.Combat;
using Odyssey.Gameplay.Config;
using Odyssey.Unity.Config;
using UnityEngine.Serialization;

namespace Odyssey.Characters.Player
{
    // RequireComponent 确保你挂这个脚本时，Unity会自动帮你挂上 CharacterController
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IPlayerConfigTarget
    {
        public const string AttackAbilityId = PlayerRuntimeSystems.AttackAbilityId;
        public const string DashAbilityId = PlayerRuntimeSystems.DashAbilityId;
        public const string HitAbilityId = PlayerRuntimeSystems.HitAbilityId;

        [Header("配置")]
        [SerializeField] private string configId = "player";
        [Tooltip("输入信号源")]
        public InputReader InputReader; // 记得在Inspector里把 PlayerInputReader 拖进去
        
        [Header("基础设置")]
        // 在 Settings 区域添加一个变量
        public float Gravity = -15f; // 重力加速度
        // 在组件区域添加一个 public 变量，让状态机能修改它
        public float VerticalVelocity; // 当前的垂直速度（跳跃/下落时用）
        
        [Header("移动设置")]
        public float WalkSpeed = 6f;
        public float RunSpeed = 10f; // 加速跑速度

        [Header("冲刺设置")]
        public float DashForce = 20f;        // 冲刺速度
        public float DashDuration = 0.2f;    // 冲刺持续时间 (短促有力)
        public float GroundDashCooldown = 0.5f; // 地面冲刺冷却 (防止无限连冲)

        [Header("动作机制")]
        public bool CanAirJump = false;    
        public bool CanAirDash = false;    // [新增] 空中冲刺开关
        
        [Header("跳跃设置")]
        public float JumpHeight = 3f;      // 普通跳/未蓄满的高度 (原先是4，按你要求改为3)
        public float ChargeJumpHeight = 5f; // [新增] 蓄力跳高度
        public float MinChargeTime = 0.5f;  // [新增] 蓄力判定时间 (超过0.5秒算蓄力)
        public float AirJumpHeight = 2f;   // 空中小跳高度保持不变
        
        [Header("墙面机制")]
        public LayerMask WallLayer; // 【重要】记得去Unity设置一个 Layer 叫 "Wall"，并把墙壁物体设为此层
        public float WallSlideSpeed = -3f; // 滑墙时的下落速度 (负数)
        public float WallJumpUpForce = 12f; // 蹬墙跳：向上的力
        public float WallJumpSideForce = 10f; // 蹬墙跳：向反方向弹开的力
        
        [Header("战斗设置")]
        public float AttackRange = 1.5f; // 攻击距离
        public int AttackDamage = 1;     // 攻击伤害
        public float AttackCooldown = 0.5f; // 连击间隔 (防止无限点)
        public LayerMask EnemyLayer;     // 敌人的层级 (记得去 Inspector 勾选 Enemy)
        
        [Header("生命值")]
        [FormerlySerializedAs("MaxHealth")]
        [SerializeField] private int maxHealth = 5;
        [FormerlySerializedAs("CurrentHealth")]
        [SerializeField] private int startingHealth = 5;
        public bool IsInvincible = false; // 是否无敌
        
        [Header("复活设置")]
        public Transform RespawnPoint; // 复活点位置
        public float RespawnDelay = 3f; // 死亡倒地后，黑屏/等待几秒复活
        private bool _isDead = false;   // 防止死亡期间重复触发扣血
        

        // --- 组件引用 ---
        // 对外公开(public)或者用属性(Property)，因为状态机需要访问它们
        public CharacterController Controller { get; private set; }
        public Animator Animator { get; private set; }
        public Transform MainCameraTransform { get; private set; }
        public int MaxHealth => maxHealth;
        public string ConfigId => configId;
        public int CurrentHealth => _runtime?.Health.Current ?? Mathf.Clamp(startingHealth, 0, maxHealth);
        public IAbilitySystem Abilities => _runtime?.Abilities;
        public event System.Action<HealthChanged> HealthChanged;

        private PlayerRuntimeSystems _runtime;
        private PlayerConfigData _appliedConfig;
        
        
        
        // --- 状态机 ---
        public StateMachine<PlayerController> StateMachine { get; private set; }

        private void Awake()
        {
            // 获取身上的组件
            Controller = GetComponent<CharacterController>();
            Animator = GetComponentInChildren<Animator>(); // 动画通常在子物体模型上

            RebuildRuntimeSystems(CreateInspectorConfig(), startingHealth);
            
            // 初始化状态机
            StateMachine = new StateMachine<PlayerController>();
            MainCameraTransform = Camera.main.transform;
        }

        private void Start()
        {
            // 锁定鼠标到屏幕中心并隐藏
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // 启动状态机！
            StateMachine.Initialize(new PlayerIdleState(this));
        }

        private void Update()
        {
            // 每帧驱动状态机思考
            StateMachine.Tick();
        }
        
        // 可选：把 InputReader 的事件订阅放在 OnEnable/OnDisable 也是好习惯
        // 但为了简单，我们稍后在具体的 State 里去订阅
        
        // Unity 角色控制器专属碰撞检测函数（当玩家撞到物体时触发）
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // 如果碰到的物体属于 Enemy 层，且玩家当前不是无敌状态
            if (!IsInvincible && ((1 << hit.gameObject.layer) & EnemyLayer) != 0)
            {
                TakeDamage(1, hit.transform.position);
            }
        }

        // --- 环境伤害检测 (酸水池、岩浆等 Trigger) ---
        private void OnTriggerStay(Collider other)
        {
            // 如果碰到了 Tag 为 "Acid" 的水池，并且当前不在无敌状态
            if (other.CompareTag("Acid") && !IsInvincible)
            {
                // 巧妙的用法：把攻击者位置设为玩家自身的位置 (transform.position)
                // 这样算出来的 knockbackDir 就是 Vector3.zero，
                // 玩家挨打后不会向后退，而是像马里奥掉进岩浆一样原地直上直下地弹跳！
                TakeDamage(1, transform.position);
            }
        }

        public void TakeDamage(int damage, Vector3 attackerPos)
        {
            // 如果处于无敌状态，或者已经死了，就不再受伤
            if (IsInvincible || _isDead) return; 

            var damageResult = _runtime.Health.Apply(new DamageRequest(damage, "enemy"));
            if (!damageResult.Accepted)
            {
                return;
            }

            if (damageResult.Killed)
            {
                _isDead = true; // 标记为已死亡
                Animator.SetTrigger("Die"); 
        
                // 开启复活协程
                StartCoroutine(RespawnRoutine()); 
                return;
            }

            StartCoroutine(InvincibilityRoutine());

            Vector3 knockbackDir = (transform.position - attackerPos).normalized;
            knockbackDir.y = 0; 
            if (knockbackDir != Vector3.zero) transform.forward = -knockbackDir;

            // 数值设定
            VerticalVelocity = 5f; 
            Vector3 knockbackMomentum = knockbackDir * 12f; 

            // 【删除】这里不再需要写 CanAirJump = false 了，全部交给 HitState 处理

            transform.position += Vector3.up * 0.1f;
            if (TryActivateAbility(HitAbilityId))
            {
                StateMachine.ChangeState(new PlayerHitState(this, knockbackMomentum));
            }
        }

        public bool TryActivateAbility(string abilityId)
        {
            return Abilities.TryActivate(abilityId, Time.time).Succeeded;
        }

        public void EndAbility(string abilityId)
        {
            Abilities.End(abilityId);
        }

        public void SetHealth(int value, string sourceId = "external")
        {
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

        public void Apply(PlayerConfigData config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }

            if (ReferenceEquals(_appliedConfig, config))
            {
                return;
            }

            var preservedHealth = CurrentHealth;
            Gravity = config.Gravity;
            WalkSpeed = config.WalkSpeed;
            RunSpeed = config.RunSpeed;
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
            maxHealth = config.MaxHealth;
            RebuildRuntimeSystems(config, preservedHealth);
            _appliedConfig = config;
        }

        private PlayerConfigData CreateInspectorConfig()
        {
            return new PlayerConfigData(
                configId, WalkSpeed, RunSpeed, Gravity, DashForce, DashDuration,
                GroundDashCooldown, JumpHeight, ChargeJumpHeight, MinChargeTime,
                AirJumpHeight, WallSlideSpeed, WallJumpUpForce, WallJumpSideForce,
                AttackDamage, AttackRange, AttackCooldown, Mathf.Max(1, maxHealth));
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
        
        // 无敌帧计时器（协程）
        private System.Collections.IEnumerator InvincibilityRoutine()
        {
            IsInvincible = true;
    
            // （可选）让模型闪烁：如果需要可以控制 Renderer 的 enabled 开关
            // WaitForSeconds 可以控制无敌时间，比如 1.5 秒
            yield return new WaitForSeconds(1.5f);
    
            IsInvincible = false;
        }
        
        private System.Collections.IEnumerator RespawnRoutine()
        {
            // 1. 禁用控制器，防止死后玩家还能按键盘移动
            this.enabled = false; 

            // 2. 等待设定的复活时间（让死亡动画播完）
            yield return new WaitForSeconds(RespawnDelay);

            // 3. 恢复满血，取消死亡标记
            _runtime.Health.Reset("respawn");
            _isDead = false;

            // 4. 【极度关键】传送 CharacterController 必须先将其禁用！
            // 否则 Unity 的物理系统会抗拒瞬间移动，把你拉回原地。
            Controller.enabled = false;
    
            if (RespawnPoint != null)
            {
                transform.position = RespawnPoint.position;
            }
            else
            {
                Debug.LogWarning("你没有设置 RespawnPoint，将在原地复活！");
            }
    
            Controller.enabled = true; // 传送到位后，重新启用碰撞体

            // 5. 强制动画机回到正常站立状态（防止卡在 Death 躺地上的最后一帧）
            // 前提：你 Animator 里那个橘黄色的基础移动状态叫 "Locomotion"
            Animator.Play("Locomotion", 0, 0f); 

            // 6. 重新激活脚本，并重置状态机为待机
            this.enabled = true;
            StateMachine.ChangeState(new PlayerIdleState(this));
        }

        // 接收动画事件，防止红字报错
        public void MeleeAttackStart() {}
        public void MeleeAttackEnd() {}
    }
    
}
