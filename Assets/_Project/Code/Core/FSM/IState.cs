namespace Odyssey.Core.FSM
{
    public interface IState<TStateId>
    {
        void Enter();
        void Exit();
        StateTransition<TStateId> Tick(float deltaTime);
    }
}
