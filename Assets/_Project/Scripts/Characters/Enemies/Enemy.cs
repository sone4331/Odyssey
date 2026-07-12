using UnityEngine;
using UnityEngine.AI;

namespace Odyssey.Characters.Enemies
{
    /// <summary>
    /// 纯净版怪物 AI 控制器 (代码绝对控制流)
    /// 包含：NavMesh自动寻路、绝对状态机控制、霸体硬直、防滑冰机制、精准物理伤害判定、受击反馈及死亡物理击飞
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        [Header("--- 基础核心属性 ---")]
        [Tooltip("怪物的最大生命值")]
        public int Health = 3;            
        [Tooltip("怪物每次咬人造成的伤害")]
        public int AttackDamage = 1;      

        [Header("--- AI 寻敌与战斗参数 ---")]
        [Tooltip("视野范围：玩家进入这个距离，怪物开始追踪")]
        public float ChaseRange = 10f;    
        [Tooltip("攻击范围：玩家进入这个距离，怪物停下并张嘴攻击")]
        public float AttackRange = 2f;    
        [Tooltip("攻击冷却：咬完一口后，需要在原地发呆喘气多久才能咬下一口")]
        public float AttackCooldown = 2f; 

        // ==========================================
        // 内部核心组件与状态变量
        // ==========================================
        private Animator _animator;       // 纯净版动画机 (无需任何连线)
        private NavMeshAgent _agent;      // 寻路导航代理
        private Transform _targetPlayer;  // 记录玩家的 Transform 位置
        
        private float _lastAttackTime;    // 记录上一次成功发动攻击的时间，用于计算冷却
        private float _actionLockTimer;   // 【霸体锁】：只要这个值大于0，怪物的大脑就会停止思考，强行把当前的动作（攻击/挨打）做完
        private bool _isDead = false;     // 生死状态标记，防止尸体诈尸

        private string _currentAnim = ""; // 记录当前正在播放的动画片段名字，防止同一帧重复调用导致动画重置

        // ==========================================
        // 死亡物理模拟 (抛物线击飞) 专用变量
        // ==========================================
        private Vector3 _deathFlyDirection; // 尸体被击飞的三维方向
        private float _deathFlySpeed;       // 尸体在空中的飞行初速度

        private void Awake()
        {
            // 在游戏唤醒时，自动获取身上的关键组件
            _animator = GetComponent<Animator>();
            _agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            // 开局自动扫描全图，寻找身上贴着 "Player" 标签的主角
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _targetPlayer = playerObj.transform;
        }

        private void Update()
        {
            // --------------------------------------------------------
            // 阶段 1：死亡托管状态 (尸体变成抛物线物理道具)
            // --------------------------------------------------------
            if (_isDead)
            {
                // 让尸体顺着击飞方向移动
                transform.position += _deathFlyDirection * _deathFlySpeed * Time.deltaTime;
                // 每帧减少向上的力，模拟真实世界的重力下坠效果
                _deathFlyDirection.y -= 5f * Time.deltaTime; 
                // 让怪物尸体在半空中迅速缩小，形成“化为灰烬消失”的视觉效果
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * 3f);
                
                return; // 死人不需要思考寻路，直接退出 Update
            }

            // --------------------------------------------------------
            // 阶段 2：安全自检 (防报错机制)
            // --------------------------------------------------------
            // 如果玩家丢了，或者怪物当前由于物理碰撞被挤出了蓝色的寻路网格，立刻罢工，防止系统红字崩溃
            if (_targetPlayer == null || !_agent.isOnNavMesh) return;

            // --------------------------------------------------------
            // 阶段 3：霸体动作锁与绝对物理刹车
            // --------------------------------------------------------
            if (_actionLockTimer > 0)
            {
                _actionLockTimer -= Time.deltaTime; // 倒计时
                
                // 【核心防滑冰】：只要处于攻击或挨打的动作中，每一帧都强制清除怪物的速度和惯性！
                // 绝不允许怪物在播放原地动画时，像推土机一样滑行推着玩家走。
                if (_agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero; 
                }
                return; // 处于硬直霸体中，退出当前帧，不执行下面的距离判断
            }

            // --------------------------------------------------------
            // 阶段 4：正常的 AI 距离判定与行为树
            // --------------------------------------------------------
            float distanceToPlayer = Vector3.Distance(transform.position, _targetPlayer.position);

            if (distanceToPlayer <= AttackRange)
            {
                // [行为 A]：玩家进入贴身范围
                _agent.isStopped = true; 
                _agent.velocity = Vector3.zero; // 瞬间踩死刹车

                // 判断攻击技能是否冷却完毕
                if (Time.time >= _lastAttackTime + AttackCooldown)
                {
                    ExecuteAttack(); // 发动攻击
                }
                else
                {
                    PlayAnimation("Idle"); // 冷却中，原地播放待机动画喘气
                }
            }
            else if (distanceToPlayer <= ChaseRange)
            {
                // [行为 B]：玩家在视野内，但不够近
                _agent.isStopped = false; // 松开刹车
                _agent.SetDestination(_targetPlayer.position); // 把寻路终点设为玩家脚底
                PlayAnimation("Run"); // 强制切入奔跑动画
            }
            else
            {
                // [行为 C]：玩家跑得太远，脱离仇恨
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero; 
                PlayAnimation("Idle"); // 找不到目标，原地发呆
            }
        }

        // ==========================================
        // 执行攻击动作
        // ==========================================
        private void ExecuteAttack()
        {
            _lastAttackTime = Time.time; // 刷新攻击时间戳，进入冷却
            
            // 给怪物 1.2 秒的“动作霸体锁”。这 1.2 秒内它不能移动，必须乖乖把咬人动画从头到尾播完！
            _actionLockTimer = 1.2f; 
            
            // 攻击瞬间强行计算指向玩家的方向，并旋转过去，防止玩家横向移动导致怪物“咬空气”
            Vector3 direction = (_targetPlayer.position - transform.position).normalized;
            direction.y = 0; // 锁定 Y 轴，防止怪物向上或向下翻转
            if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);

            PlayAnimation("Attack"); // 发送播放攻击动画的指令
        }

        // ==========================================
        // 绝对动画控制台 (空间传送魔法)
        // ==========================================
        private void PlayAnimation(string animName)
        {
            // 如果动画机已经在播放这个目标动画，就不做任何事，防止画面鬼畜重置
            if (_currentAnim == animName) return;
            
            // 使用 CrossFade (交叉淡入)，用 0.1 秒的平滑过渡，强制命令动画机跳到新动作。
            // 这种写法可以完全无视 Animator 窗口里的那些白色箭头连线！
            _animator.CrossFade(animName, 0.1f);
            _currentAnim = animName; // 记录当前状态
        }

        // ==========================================
        // 🔥 终极伤害判定引擎 (由攻击动画中的 Event 自动触发)
        // ==========================================
        public void AttackBegin() 
        {
            if (_isDead) return; // 死了就不准造成伤害

            // 1. 设置判定中心：将判定球抬高到怪物胸口位置 (Vector3.up * 1f)，并微微向前推一点 (forward * 0.5f)
            Vector3 attackPos = transform.position + Vector3.up * 1f + transform.forward * 0.5f;
            
            // 2. 划定杀伤范围：生成一个半径高达 2.0f 的超大空气判定球！
            // 这个球大到连怪物的整个前半身都完全笼罩，彻底解决站着不动打不到的“穿模丢失” Bug。
            Collider[] hits = Physics.OverlapSphere(attackPos, 2.0f);

            // 3. 遍历所有被判定球碰到的物体
            foreach (var hit in hits)
            {
                // 终极霸道抓取：无视标签是否贴在子物体上，直接穿透层级，向父物体寻找玩家的控制脚本
                var player = hit.GetComponentInParent<Odyssey.Characters.Player.PlayerController>();
                
                if (player != null) 
                {
                    // 找到玩家，呼叫玩家的扣血逻辑，并把怪物自己的位置传过去，用来计算击退方向
                    player.TakeDamage(AttackDamage, transform.position); 
                    
                    // 只要咬中一口就立刻跳出循环，防止一瞬间被多个不同部位的碰撞体重复触发扣血
                    break; 
                }
            }
        }

        // ==========================================
        // 挨打受击逻辑 (由玩家的武器脚本呼叫)
        // ==========================================
        public void TakeDamage(int damage)
        {
            if (_isDead) return;

            Health -= damage; // 扣减血量
            
            if (Health <= 0) 
            {
                Die(); // 血量归零，触发死亡
            }
            else
            {
                // 没死，触发受击表现
                // 在 1 到 4 之间随机抽一个数字，拼接出 Hit1, Hit2 等动画名，让挨打动作不枯燥
                int randomHit = Random.Range(1, 5); 
                PlayAnimation("Hit" + randomHit); 
                
                _currentAnim = ""; // 清空当前动画记录，允许短时间内连续挨打
                
                // 给怪物 0.5 秒的“脑震荡硬直”，这期间它不能反击也不能跑，必须乖乖后仰
                _actionLockTimer = 0.5f; 
                
                if (_agent != null && _agent.isOnNavMesh) 
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero; // 挨打瞬间踩死物理刹车，防止滑冰
                }
            }
        }

        // ==========================================
        // 死亡与物理击飞逻辑
        // ==========================================
        private void Die()
        {
            _isDead = true; // 贴上死亡标签
            
            // 1. 变成幽灵：关闭导航和碰撞体，防止尸体变成一堵墙挡住玩家的路
            if (_agent != null) _agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col) col.enabled = false;
            
            // 2. 计算被击飞的方向 (玩家看向怪物的方向)
            if (_targetPlayer != null)
            {
                _deathFlyDirection = (transform.position - _targetPlayer.position).normalized;
                _deathFlyDirection.y = 1.5f; // 加上向上的升力，形成斜向后的抛物线
            }
            else
            {
                _deathFlyDirection = Vector3.up; // 备用方案：直挺挺向上飞
            }
            
            _deathFlySpeed = 8f; // 设置击飞的初始爆发速度

            // 3. 关闭动画机，让怪物保持死前最后一个动作，变成“兵马俑”飞出去
            if (_animator != null) _animator.enabled = false;

            // 4. 定时炸弹：2秒后把这个被缩小成灰的物体，彻彻底底从内存中删除
            Destroy(gameObject, 2f); 
        }

        // ==========================================
        // 官方废弃动画事件吸收器 (垃圾桶)
        // 作用：如果官方模型里的动画片段偷偷带着这些事件，而代码里没写，控制台就会疯狂报错中断程序。
        // 写下这些空函数，就能像黑洞一样把报错全部无害化吸收掉。
        // ==========================================
        public void AttackEnd() { }   
        public void PlayStep() { }    
        public void Grunt() { }       
        public void MeleeAttackStart() { } 
        public void MeleeAttackEnd() { } 
    }
}