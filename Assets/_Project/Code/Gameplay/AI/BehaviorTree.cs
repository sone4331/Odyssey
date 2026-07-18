using System;
using System.Collections.Generic;

namespace Odyssey.Gameplay.AI
{
    /// <summary>
    /// 表示行为节点本帧的执行结果。运行表示下一帧继续，成功或失败表示本次节点生命周期已经结束。
    /// 这是行为树各层之间唯一的控制协议，让纯 C# 决策代码不依赖 Unity 的帧循环。
    /// </summary>
    public enum BehaviorStatus
    {
        Running,
        Success,
        Failure
    }

    /// <summary>
    /// 为所有行为节点提供统一的进入、执行、退出和中断生命周期。
    /// 采用模板方法模式集中管理生命周期，避免每个动作节点各自处理“只进入一次”和“切换时清理”。
    /// </summary>
    public abstract class BehaviorNode<TContext>
    {
        protected BehaviorNode(string name)
        {
            Name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
        }

        public string Name { get; }
        public bool IsActive { get; private set; }

        public BehaviorStatus Tick(TContext context)
        {
            if (!IsActive)
            {
                IsActive = true;
                OnEnter(context);
            }

            var status = OnTick(context);
            if (status != BehaviorStatus.Running)
            {
                OnExit(context, status);
                IsActive = false;
            }

            return status;
        }

        /// <summary>
        /// 由高优先级分支抢占当前节点时调用，确保导航路径、攻击前摇等副作用能够被明确撤销。
        /// </summary>
        public void Abort(TContext context)
        {
            if (!IsActive)
            {
                return;
            }

            OnAbort(context);
            OnExit(context, BehaviorStatus.Failure);
            IsActive = false;
        }

        internal virtual void AppendActivePath(ICollection<string> path)
        {
            if (IsActive)
            {
                path.Add(Name);
            }
        }

        protected virtual void OnEnter(TContext context) { }
        protected abstract BehaviorStatus OnTick(TContext context);
        protected virtual void OnAbort(TContext context) { }
        protected virtual void OnExit(TContext context, BehaviorStatus status) { }
    }

    /// <summary>
    /// 每帧从最高优先级重新检查子节点，并中断上帧仍在运行但已失去优先权的分支。
    /// 采用响应式选择器而非记忆选择器，是为了让死亡、受击和目标丢失能在下一帧立即生效。
    /// </summary>
    public sealed class ReactiveSelector<TContext> : BehaviorNode<TContext>
    {
        private readonly IReadOnlyList<BehaviorNode<TContext>> _children;
        private int _activeIndex = -1;

        public ReactiveSelector(string name, params BehaviorNode<TContext>[] children) : base(name)
        {
            _children = children ?? throw new ArgumentNullException(nameof(children));
        }

        protected override BehaviorStatus OnTick(TContext context)
        {
            for (var index = 0; index < _children.Count; index++)
            {
                var status = _children[index].Tick(context);
                if (status == BehaviorStatus.Failure)
                {
                    continue;
                }

                AbortPreviousChild(context, index);
                _activeIndex = status == BehaviorStatus.Running ? index : -1;
                return status;
            }

            AbortPreviousChild(context, -1);
            _activeIndex = -1;
            return BehaviorStatus.Failure;
        }

        protected override void OnAbort(TContext context)
        {
            AbortPreviousChild(context, -1);
            _activeIndex = -1;
        }

        internal override void AppendActivePath(ICollection<string> path)
        {
            base.AppendActivePath(path);
            if (_activeIndex >= 0)
            {
                _children[_activeIndex].AppendActivePath(path);
            }
        }

        private void AbortPreviousChild(TContext context, int nextIndex)
        {
            if (_activeIndex >= 0 && _activeIndex != nextIndex)
            {
                _children[_activeIndex].Abort(context);
            }
        }
    }

    /// <summary>
    /// 按顺序重新验证条件与动作，任一子节点失败即失败，遇到运行节点则保留该分支。
    /// 采用响应式 Sequence 保证“玩家仍在感知范围内”等前置条件不会只在进入分支时检查一次。
    /// </summary>
    public sealed class Sequence<TContext> : BehaviorNode<TContext>
    {
        private readonly IReadOnlyList<BehaviorNode<TContext>> _children;
        private int _activeIndex = -1;

        public Sequence(string name, params BehaviorNode<TContext>[] children) : base(name)
        {
            _children = children ?? throw new ArgumentNullException(nameof(children));
        }

        protected override BehaviorStatus OnTick(TContext context)
        {
            for (var index = 0; index < _children.Count; index++)
            {
                var status = _children[index].Tick(context);
                if (status == BehaviorStatus.Success)
                {
                    continue;
                }

                AbortPreviousChild(context, status == BehaviorStatus.Running ? index : -1);
                _activeIndex = status == BehaviorStatus.Running ? index : -1;
                return status;
            }

            AbortPreviousChild(context, -1);
            _activeIndex = -1;
            return BehaviorStatus.Success;
        }

        protected override void OnAbort(TContext context)
        {
            AbortPreviousChild(context, -1);
            _activeIndex = -1;
        }

        internal override void AppendActivePath(ICollection<string> path)
        {
            base.AppendActivePath(path);
            if (_activeIndex >= 0)
            {
                _children[_activeIndex].AppendActivePath(path);
            }
        }

        private void AbortPreviousChild(TContext context, int nextIndex)
        {
            if (_activeIndex >= 0 && _activeIndex != nextIndex)
            {
                _children[_activeIndex].Abort(context);
            }
        }
    }

    /// <summary>
    /// 将只读判断包装为叶节点。条件不产生场景副作用，因此每帧都可以安全地重新求值。
    /// </summary>
    public sealed class ConditionNode<TContext> : BehaviorNode<TContext>
    {
        private readonly Func<TContext, bool> _condition;

        public ConditionNode(string name, Func<TContext, bool> condition) : base(name)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        protected override BehaviorStatus OnTick(TContext context)
        {
            return _condition(context) ? BehaviorStatus.Success : BehaviorStatus.Failure;
        }
    }

    /// <summary>
    /// 将具体动作委托包装为叶节点，并把行为树中断显式转发给动作适配器。
    /// 这是 Strategy 与 Adapter 的边界：树只决定做什么，Unity 适配器决定如何导航和播放动画。
    /// </summary>
    public sealed class ActionNode<TContext> : BehaviorNode<TContext>
    {
        private readonly Func<TContext, BehaviorStatus> _action;
        private readonly Action<TContext> _abort;

        public ActionNode(
            string name,
            Func<TContext, BehaviorStatus> action,
            Action<TContext> abort = null) : base(name)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _abort = abort;
        }

        protected override BehaviorStatus OnTick(TContext context) => _action(context);
        protected override void OnAbort(TContext context) => _abort?.Invoke(context);
    }

    /// <summary>
    /// 持有一棵行为树并提供稳定的逐帧入口和当前运行路径。
    /// Runner 不拥有全局状态，每只怪物各持有一个实例，因而不存在单例之间的隐式共享。
    /// </summary>
    public sealed class BehaviorTreeRunner<TContext>
    {
        private readonly BehaviorNode<TContext> _root;
        private readonly List<string> _activePath = new List<string>();

        public BehaviorTreeRunner(BehaviorNode<TContext> root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public string CurrentPath { get; private set; } = "尚未运行";

        public BehaviorStatus Tick(TContext context)
        {
            var status = _root.Tick(context);
            _activePath.Clear();
            _root.AppendActivePath(_activePath);
            CurrentPath = _activePath.Count == 0 ? "本帧已完成" : string.Join(" > ", _activePath);
            return status;
        }

        public void Abort(TContext context)
        {
            _root.Abort(context);
            CurrentPath = "已中断";
        }
    }
}
