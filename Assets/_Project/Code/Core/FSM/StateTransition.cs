namespace Odyssey.Core.FSM
{
    /// <summary>
    /// 表示状态在本次 Tick 后提交的转移意图，是状态与状态机之间的不可变消息。
    /// 使用值对象而非回调，使“保持当前状态”和“切换到目标状态”能够被测试并显式表达。
    /// </summary>
    public readonly struct StateTransition<TStateId>
    {
        private StateTransition(bool isRequested, TStateId target)
        {
            IsRequested = isRequested;
            Target = target;
        }

        public bool IsRequested { get; }
        public TStateId Target { get; }

        public static StateTransition<TStateId> None => default;

        public static StateTransition<TStateId> To(TStateId target)
        {
            return new StateTransition<TStateId>(true, target);
        }
    }
}
