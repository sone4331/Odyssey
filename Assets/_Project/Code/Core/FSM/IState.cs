namespace Odyssey.Core.FSM
{
    /// <summary>
    /// 定义纯 C# 状态的生命周期协议，状态通过返回转移意图而不是直接修改状态机。
    /// 这是 State 模式的领域边界，目的是让转移集中提交并避免旧状态在同一帧继续执行副作用。
    /// </summary>
    public interface IState<TStateId>
    {
        void Enter();
        void Exit();
        StateTransition<TStateId> Tick(float deltaTime);
    }
}
