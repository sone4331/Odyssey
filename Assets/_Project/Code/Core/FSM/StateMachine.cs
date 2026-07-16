using System;
using System.Collections.Generic;

namespace Odyssey.Core.FSM
{
    /// <summary>
    /// 管理状态生命周期，并在 Tick 完成后集中提交转移请求。
    /// 采用延迟提交的 State 模式，确保当前状态退出后不会继续污染新状态的同帧逻辑，同时保持纯 C# 可测试性。
    /// </summary>
    public sealed class DeferredStateMachine<TStateId>
    {
        private readonly IReadOnlyDictionary<TStateId, IState<TStateId>> _states;
        private IState<TStateId> _currentState;

        public DeferredStateMachine(IReadOnlyDictionary<TStateId, IState<TStateId>> states)
        {
            _states = states ?? throw new ArgumentNullException(nameof(states));
        }

        public TStateId CurrentId { get; private set; }

        public void Initialize(TStateId initialState)
        {
            CurrentId = initialState;
            _currentState = Resolve(initialState);
            _currentState.Enter();
        }

        /// <summary>
        /// 强制结束当前状态并回到指定状态，用于复活、重新开局等明确的生命周期边界。
        /// 采用显式 Reset 而不是重复调用 Initialize，保证旧状态的事件订阅和临时资源一定经过 Exit 清理。
        /// </summary>
        public void Reset(TStateId targetState)
        {
            var nextState = Resolve(targetState);
            _currentState?.Exit();
            CurrentId = targetState;
            _currentState = nextState;
            _currentState.Enter();
        }

        /// <summary>
        /// 先执行当前状态并保存转移意图，再由状态机统一完成 Exit、替换和 Enter。
        /// 转移阶段集中化是状态一致性的关键约束，状态实现不得绕过该顺序直接切换实例。
        /// </summary>
        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            var transition = _currentState.Tick(deltaTime);
            if (!transition.IsRequested || EqualityComparer<TStateId>.Default.Equals(CurrentId, transition.Target))
            {
                return;
            }

            var nextState = Resolve(transition.Target);
            _currentState.Exit();
            CurrentId = transition.Target;
            _currentState = nextState;
            _currentState.Enter();
        }

        private IState<TStateId> Resolve(TStateId id)
        {
            if (!_states.TryGetValue(id, out var state) || state == null)
            {
                throw new KeyNotFoundException($"未注册 ID 为“{id}”的状态。");
            }

            return state;
        }

        private void EnsureInitialized()
        {
            if (_currentState == null)
            {
                throw new InvalidOperationException("状态机尚未初始化。");
            }
        }
    }
}
