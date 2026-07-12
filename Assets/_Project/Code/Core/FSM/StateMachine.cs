using System;
using System.Collections.Generic;

namespace Odyssey.Core.FSM
{
    public sealed class StateMachine<TStateId>
    {
        private readonly IReadOnlyDictionary<TStateId, IState<TStateId>> _states;
        private IState<TStateId> _currentState;

        public StateMachine(IReadOnlyDictionary<TStateId, IState<TStateId>> states)
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
                throw new KeyNotFoundException($"No state is registered for '{id}'.");
            }

            return state;
        }

        private void EnsureInitialized()
        {
            if (_currentState == null)
            {
                throw new InvalidOperationException("The state machine has not been initialized.");
            }
        }
    }
}
