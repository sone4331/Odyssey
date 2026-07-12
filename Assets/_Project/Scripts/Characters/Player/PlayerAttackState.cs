using UnityEngine;
using Odyssey.Characters.Enemies;

namespace Odyssey.Characters.Player
{
    public class PlayerAttackState : PlayerState
    {
        private int _comboIndex;
        private bool _nextComboQueued;
        private bool _hasDealtDamage;
        private float _stateTimer;

        public PlayerAttackState(PlayerController controller, int comboIndex) : base(controller)
        {
            _comboIndex = comboIndex;
        }

        public override void Enter()
        {
            base.Enter();
            _core.VerticalVelocity = 0f;
            _core.Controller.Move(Vector3.zero);
            _core.Animator.SetFloat("Speed", 0f);

            _core.Animator.ResetTrigger("Attack");
            _core.Animator.SetInteger("ComboIndex", _comboIndex);
            _core.Animator.SetTrigger("Attack");

            _nextComboQueued = false;
            _hasDealtDamage = false;
            _stateTimer = 0f;

            // 第一刀转身面向前方
            if (_comboIndex == 1)
            {
                Vector3 camForward = _core.MainCameraTransform.forward;
                camForward.y = 0;
                if (camForward != Vector3.zero) _core.transform.forward = camForward.normalized;
            }

            _core.InputReader.AttackEvent += OnAttack;
        }

        public override void Exit()
        {
            base.Exit();
            _core.InputReader.AttackEvent -= OnAttack;
        }

        private void OnAttack() { _nextComboQueued = true; }

        public override void Tick()
        {
            _core.Controller.Move(Vector3.zero); // 持续锁死防滑步
            _stateTimer += Time.deltaTime;
            AnimatorStateInfo info = _core.Animator.GetCurrentAnimatorStateInfo(0);

            // 保护期：超过0.2秒才开始判定，防止读到上一个动画
            if (info.IsTag("Attack") || _stateTimer > 0.2f)
            {
                float realTime = (_stateTimer < 0.2f) ? 0f : info.normalizedTime;

                // 30% 进度时打出伤害
                if (realTime >= 0.3f && !_hasDealtDamage)
                {
                    ApplyDamage();
                    _hasDealtDamage = true;
                }

                // 40% ~ 90% 允许输入下一招
                if (realTime >= 0.4f && realTime < 0.9f)
                {
                    if (_nextComboQueued && _comboIndex < 4)
                    {
                        _core.StateMachine.ChangeState(new PlayerAttackState(_core, _comboIndex + 1));
                    }
                }

                // 播完回待机
                if (realTime >= 0.95f) 
                {
                    _core.StateMachine.ChangeState(new PlayerIdleState(_core));
                }
            }
        }

        private void ApplyDamage()
        {
            // 判定球向下偏移，兼容 Chomper 这种矮子怪
            Vector3 attackPos = _core.transform.position + _core.transform.forward * 1f + Vector3.up * 0.5f;
            Collider[] hits = Physics.OverlapSphere(attackPos, _core.AttackRange, _core.EnemyLayer);

            foreach (var hit in hits)
            {
                Enemy enemy = hit.GetComponent<Enemy>();
                if (enemy == null) enemy = hit.GetComponentInParent<Enemy>(); // 兼容层级
                
                if (enemy != null) enemy.TakeDamage(_core.AttackDamage);
            }
        }
    }
}