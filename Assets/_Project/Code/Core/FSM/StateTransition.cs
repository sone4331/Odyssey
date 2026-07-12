namespace Odyssey.Core.FSM
{
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
